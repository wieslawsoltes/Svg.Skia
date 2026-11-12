(() => {
    if (globalThis.UnoDrawWebGlSkia) return;
    let nextCanvasId = 0;

    function fail(state, reason) {
        if (state.disposed || state.failure) return;
        state.failure = String(reason);
        state.unavailable?.(state.failure);
    }

    // Emscripten 3.1.56 can truncate typed-array uploads when its >2GB heap path
    // meets Skia's glyph-atlas row stride. Repair only that context's undersized
    // view, preserving every pixel-store parameter. No Uno JS internals are used.
    // The same browser requirement is documented in Uno 7.0.32:
    // https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/Uno.UI.Runtime.WebAssembly.Browser/ts/Runtime/EmscriptenWebGL.ts
    function preserveUploadStride(gl) {
        const stores = [gl.UNPACK_ALIGNMENT, gl.UNPACK_ROW_LENGTH, gl.UNPACK_SKIP_ROWS, gl.UNPACK_SKIP_PIXELS];
        const values = stores.map(value => gl.getParameter(value));
        const pixelStore = gl.pixelStorei;
        gl.pixelStorei = function (name, value) {
            const index = stores.indexOf(name);
            if (index !== -1) values[index] = value;
            return pixelStore.call(gl, name, value);
        };

        function pixelBytes(format, type) {
            if ([gl.UNSIGNED_SHORT_5_6_5, gl.UNSIGNED_SHORT_4_4_4_4, gl.UNSIGNED_SHORT_5_5_5_1].includes(type)) return 2;
            if ([gl.UNSIGNED_INT_2_10_10_10_REV, gl.UNSIGNED_INT_10F_11F_11F_REV, gl.UNSIGNED_INT_5_9_9_9_REV, gl.UNSIGNED_INT_24_8].includes(type)) return 4;
            const component = [gl.BYTE, gl.UNSIGNED_BYTE].includes(type) ? 1
                : [gl.SHORT, gl.UNSIGNED_SHORT, gl.HALF_FLOAT].includes(type) ? 2
                : [gl.INT, gl.UNSIGNED_INT, gl.FLOAT].includes(type) ? 4 : 0;
            const channels = [gl.RED, gl.RED_INTEGER, gl.ALPHA, gl.LUMINANCE, gl.DEPTH_COMPONENT].includes(format) ? 1
                : [gl.RG, gl.RG_INTEGER, gl.LUMINANCE_ALPHA].includes(format) ? 2
                : [gl.RGB, gl.RGB_INTEGER].includes(format) ? 3
                : [gl.RGBA, gl.RGBA_INTEGER].includes(format) ? 4 : 0;
            return component * channels;
        }

        function repair(args, widthIndex, heightIndex, formatIndex, typeIndex) {
            const view = args[8];
            const width = args[widthIndex], height = args[heightIndex];
            if (!ArrayBuffer.isView(view) || width <= 0 || height <= 0 ||
                !(values[1] || values[2] || values[3])) return;
            const bytes = pixelBytes(args[formatIndex], args[typeIndex]);
            if (!bytes) return;
            const stride = Math.ceil((values[1] || width) * bytes / values[0]) * values[0];
            const length = (values[2] + height - 1) * stride + (values[3] + width) * bytes;
            if (view.byteLength >= length || view.byteOffset + length > view.buffer.byteLength) return;
            const elements = Math.ceil(length / view.BYTES_PER_ELEMENT);
            if (view.byteOffset + elements * view.BYTES_PER_ELEMENT <= view.buffer.byteLength)
                args[8] = new view.constructor(view.buffer, view.byteOffset, elements);
        }

        for (const [method, width, height, format, type] of [
            ["texImage2D", 3, 4, 6, 7], ["texSubImage2D", 4, 5, 6, 7]
        ]) {
            const original = gl[method];
            gl[method] = function (...args) {
                if (args.length === 9) repair(args, width, height, format, type);
                return original.apply(gl, args);
            };
        }
    }

    globalThis.UnoDrawWebGlSkia = {
        createCanvas(unavailable, initialized) {
            const canvas = document.createElement("canvas");
            canvas.id = `unodraw-webgl-document-${++nextCanvasId}`;
            canvas.width = canvas.height = 1;
            canvas.hidden = true;
            canvas.setAttribute("aria-hidden", "true");
            const state = {
                canvas, selector: `#${canvas.id}`, unavailable, initialized,
                gl: null, disposed: false, failure: null, initializationPending: false
            };
            state.lost = () => fail(state, "the WebGL2 document context was lost");
            canvas.addEventListener("webglcontextlost", state.lost);
            document.body.appendChild(canvas);
            return state;
        },

        prepareCanvas(state) {
            const gl = state.canvas.getContext("webgl2");
            const attributes = gl?.getContextAttributes();
            if (!attributes || !attributes.alpha || !attributes.premultipliedAlpha ||
                !attributes.stencil || attributes.antialias || attributes.preserveDrawingBuffer)
                throw new Error("The WebGL2 context does not preserve the required alpha and coverage semantics.");
            if ("drawingBufferColorSpace" in gl) {
                gl.drawingBufferColorSpace = "srgb";
                if (gl.drawingBufferColorSpace !== "srgb")
                    throw new Error("The WebGL2 drawing buffer is not sRGB encoded.");
            }
            preserveUploadStride(gl);
            state.gl = gl;
        },

        copyCanvas(state, device, queue, texture, width, height) {
            if (state.disposed || state.failure || !state.gl || state.gl.isContextLost())
                throw new Error(state.failure || "The WebGL2 canvas is unavailable.");
            if (state.canvas.width !== width || state.canvas.height !== height ||
                texture.width !== width || texture.height !== height || texture.format !== "rgba8unorm")
                throw new Error("The WebGL2 source and WebGPU target pixel dimensions or format differ.");

            // Capture in the same JS task as Skia's WebGL submission. There is no
            // awaited readback or glFinish. Chromium/Linux may still choose an
            // internal CPU copy; this API does not promise a GPU-only transfer.
            // https://www.w3.org/TR/webgpu/#dom-gpuqueue-copyexternalimagetotexture
            device.pushErrorScope("validation");
            let submitted = false;
            try {
                queue.copyExternalImageToTexture(
                    { source: state.canvas, flipY: false },
                    { texture, premultipliedAlpha: true, colorSpace: "srgb" },
                    { width, height, depthOrArrayLayers: 1 });
                submitted = true;
            } finally {
                device.popErrorScope().then(error => {
                    if (error) {
                        fail(state, `WebGPU canvas transfer validation failed: ${error.message}`);
                    } else if (submitted && !state.disposed && !state.failure && !state.initializationPending) {
                        state.initializationPending = true;
                        // Report actual validated transfer completion, rather than
                        // merely successful context creation. Never block a paint.
                        return queue.onSubmittedWorkDone().then(() => {
                            if (!state.disposed && !state.failure) state.initialized?.();
                        });
                    }
                }).catch(error => fail(state, `WebGPU canvas transfer failed: ${error.message || error}`));
            }
        },

        disposeCanvas(state) {
            if (state.disposed) return;
            state.disposed = true;
            state.unavailable = state.initialized = null;
            state.canvas.removeEventListener("webglcontextlost", state.lost);
            // Native Ganesh resources and Emscripten's context handle have already
            // been released. This is our own context, never the compositor's.
            state.gl?.getExtension("WEBGL_lose_context")?.loseContext();
            state.canvas.remove();
            state.canvas = state.gl = state.lost = null;
        }
    };
})();
