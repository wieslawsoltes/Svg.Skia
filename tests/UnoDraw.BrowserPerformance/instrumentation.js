// Observes browser work without invalidating Uno or subscribing to CompositionTarget.Rendering.
// Loaded before application scripts so the same observer works with Uno 6 and Uno 7.
(() => {
    "use strict";
    const nativeRaf = window.requestAnimationFrame.bind(window);
    const nativeCancelRaf = window.cancelAnimationFrame.bind(window);
    const contexts = [];
    const byContext = new WeakMap();
    const wrapped = new Set();
    let target = null;
    let sample = null;
    let observer = 0;
    let frame = 0;
    let lastGraphicsFrame = -1;
    let pendingInputs = [];
    let acquiredTargetTexture = false;

    function wrap(prototype, name, observe) {
        if (!prototype) return;
        const descriptor = Object.getOwnPropertyDescriptor(prototype, name);
        if (!descriptor || typeof descriptor.value !== "function") return;
        const original = descriptor.value;
        Object.defineProperty(prototype, name, {
            ...descriptor,
            value: function (...args) {
                const result = original.apply(this, args);
                observe(this, args, result);
                return result;
            }
        });
        wrapped.add(name);
    }

    function register(canvas, kind, context) {
        if (!context || byContext.has(context)) return;
        const record = { canvas, kind, context, drawCalls: 0, presentations: 0 };
        byContext.set(context, record);
        contexts.push(record);
    }

    wrap(globalThis.HTMLCanvasElement?.prototype, "getContext", (canvas, args, context) =>
        register(canvas, args[0], context));
    wrap(globalThis.OffscreenCanvas?.prototype, "getContext", (canvas, args, context) =>
        register(canvas, args[0], context));

    function graphics(record, name, draws = false) {
        if (draws) record.drawCalls++;
        else record.presentations++;
        if (!sample || record !== target) return;
        sample.calls[name] = (sample.calls[name] || 0) + 1;
        // Only the first draw in an animation interval needs a timestamp. Never
        // allocate an event object for every primitive in a large SVG.
        if (lastGraphicsFrame !== frame || pendingInputs.length) {
            const now = performance.now();
            if (lastGraphicsFrame !== frame) {
                sample.graphics_active_intervals_ms.push(now);
                lastGraphicsFrame = frame;
            }
            for (const input of pendingInputs) {
                sample.input_to_next_graphics_ms.push(now - input);
            }
            pendingInputs = [];
        }
    }

    for (const prototype of [globalThis.WebGLRenderingContext?.prototype,
                             globalThis.WebGL2RenderingContext?.prototype]) {
        for (const name of ["drawArrays", "drawElements", "drawArraysInstanced",
                            "drawElementsInstanced", "drawRangeElements"]) {
            wrap(prototype, name, context => {
                const record = byContext.get(context);
                if (record) graphics(record, "webgl." + name, true);
            });
        }
        wrap(prototype, "flush", context => {
            const record = byContext.get(context);
            if (!record) return;
            if (sample && record === target) sample.webgl_flush_ms.push(performance.now());
            graphics(record, "webgl.flush");
        });
        for (const name of ["texImage2D", "texSubImage2D"]) {
            wrap(prototype, name, (context, args) => {
                if (!sample || byContext.get(context) !== target) return;
                sample.calls["webgl." + name] = (sample.calls["webgl." + name] || 0) + 1;
                const data = args[args.length - 1];
                // Array-buffer uploads are exact; DOM-image overloads are not
                // assigned an invented byte count.
                if (ArrayBuffer.isView(data)) sample.known_upload_bytes += data.byteLength;
                else sample.uploads_with_unknown_size++;
            });
        }
    }

    wrap(globalThis.GPUCanvasContext?.prototype, "getCurrentTexture", context => {
        const record = byContext.get(context);
        if (!record) return;
        record.presentations++;
        if (record === target) acquiredTargetTexture = true;
    });
    wrap(globalThis.GPUQueue?.prototype, "submit", () => {
        if (!target || target.kind !== "webgpu" || !acquiredTargetTexture) return;
        acquiredTargetTexture = false;
        if (sample) sample.webgpu_canvas_submit_ms.push(performance.now());
        graphics(target, "webgpu.canvasSubmit");
    });
    wrap(globalThis.GPUQueue?.prototype, "writeTexture", (_, args) => {
        if (!sample || target?.kind !== "webgpu") return;
        sample.calls["webgpu.writeTexture"] = (sample.calls["webgpu.writeTexture"] || 0) + 1;
        const data = args[1];
        if (typeof data?.byteLength === "number") sample.known_upload_bytes += data.byteLength;
        else sample.uploads_with_unknown_size++;
    });

    for (const name of ["drawImage", "putImageData"]) {
        wrap(globalThis.CanvasRenderingContext2D?.prototype, name, context => {
            const record = byContext.get(context);
            if (record) graphics(record, "canvas2d." + name, true);
        });
    }

    window.requestAnimationFrame = callback => nativeRaf(timestamp => {
        const current = sample;
        const begin = current ? performance.now() : 0;
        try { return callback(timestamp); }
        finally {
            if (current && sample === current) {
                current.application_raf_callback_ms.push(performance.now() - begin);
            }
        }
    });

    function tick(timestamp) {
        if (!sample) return;
        sample.raf_ms.push(timestamp);
        frame++;
        observer = nativeRaf(tick);
    }

    for (const name of ["pointermove", "pointerdown", "wheel", "keydown"]) {
        window.addEventListener(name, event => {
            if (!sample || !event.isTrusted || !sample.input_types.includes(event.type)) return;
            if (event.type === "keydown" && ["Control", "Shift", "Alt", "Meta"].includes(event.key)) return;
            sample.input_events++;
            pendingInputs.push(performance.now());
        }, { capture: true, passive: true });
    }

    function describe(record) {
        const bounds = record.canvas.getBoundingClientRect?.();
        const result = {
            kind: record.kind,
            connected: record.canvas.isConnected === true,
            width: record.canvas.width, height: record.canvas.height,
            css_width: bounds?.width ?? null, css_height: bounds?.height ?? null,
            draw_calls: record.drawCalls, presentations: record.presentations,
            target: record === target
        };
        if (record.kind.startsWith("webgl") && record === target) {
            const extension = record.context.getExtension("WEBGL_debug_renderer_info");
            result.renderer = extension
                ? record.context.getParameter(extension.UNMASKED_RENDERER_WEBGL)
                : record.context.getParameter(record.context.RENDERER);
        }
        return result;
    }

    Object.defineProperty(globalThis, "__unoDrawBenchmark", { value: Object.freeze({
        selectCanvas() {
            const visible = contexts.filter(record => {
                const bounds = record.canvas.getBoundingClientRect?.();
                return record.canvas.isConnected && bounds?.width > 100 && bounds?.height > 100;
            });
            visible.sort((a, b) => b.canvas.width * b.canvas.height - a.canvas.width * a.canvas.height);
            target = visible[0] ?? null;
            if (!target) throw new Error("No visible application canvas context was observed");
            return contexts.map(describe);
        },
        contexts: () => contexts.map(describe),
        nextFrame: () => new Promise(resolve => nativeRaf(resolve)),
        start(workload, inputTypes) {
            if (sample) throw new Error("A benchmark interval is already active");
            frame = 0;
            lastGraphicsFrame = -1;
            pendingInputs = [];
            sample = {
                workload, input_types: inputTypes, started_ms: performance.now(),
                raf_ms: [], graphics_active_intervals_ms: [],
                application_raf_callback_ms: [], input_to_next_graphics_ms: [],
                webgpu_canvas_submit_ms: [], webgl_flush_ms: [],
                input_events: 0, calls: {}, known_upload_bytes: 0, uploads_with_unknown_size: 0
            };
            observer = nativeRaf(tick);
        },
        stop() {
            if (!sample) throw new Error("No benchmark interval is active");
            nativeCancelRaf(observer);
            const result = sample;
            result.ended_ms = performance.now();
            result.inputs_without_later_graphics = pendingInputs.length;
            result.observed_methods = [...wrapped].sort();
            sample = null;
            pendingInputs = [];
            return result;
        }
    }) });
})();
