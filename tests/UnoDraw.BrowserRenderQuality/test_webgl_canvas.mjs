import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import vm from "node:vm";

const source = readFileSync(new URL(
    "../../samples/UnoDraw/Platforms/WebAssembly/Rendering/WebGlSkiaCanvas.js", import.meta.url), "utf8");

function deferred() {
    let resolve, reject;
    const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
    return { promise, resolve, reject };
}

const drain = () => new Promise(resolve => setImmediate(resolve));

function fixture() {
    const calls = [], failures = [], validations = [], completions = [];
    let initialized = 0;
    const gl = {
        drawingBufferColorSpace: "display-p3", lost: false, losses: 0,
        attributes: { alpha: true, premultipliedAlpha: true, stencil: true, antialias: false, preserveDrawingBuffer: false },
        getContextAttributes() { return this.attributes; },
        getParameter(name) { return name === this.UNPACK_ALIGNMENT ? 4 : 0; },
        pixelStorei(name, value) { calls.push({ method: "pixelStorei", name, value }); },
        texImage2D(...args) { calls.push({ method: "texImage2D", args }); },
        texSubImage2D(...args) { calls.push({ method: "texSubImage2D", args }); },
        isContextLost() { return this.lost; },
        getExtension(name) { return name === "WEBGL_lose_context" ? { loseContext: () => this.losses++ } : null; }
    };
    const names = [
        "UNPACK_ALIGNMENT", "UNPACK_ROW_LENGTH", "UNPACK_SKIP_ROWS", "UNPACK_SKIP_PIXELS",
        "UNSIGNED_SHORT_5_6_5", "UNSIGNED_SHORT_4_4_4_4", "UNSIGNED_SHORT_5_5_5_1",
        "UNSIGNED_INT_2_10_10_10_REV", "UNSIGNED_INT_10F_11F_11F_REV", "UNSIGNED_INT_5_9_9_9_REV", "UNSIGNED_INT_24_8",
        "BYTE", "UNSIGNED_BYTE", "SHORT", "UNSIGNED_SHORT", "HALF_FLOAT", "INT", "UNSIGNED_INT", "FLOAT",
        "RED", "RED_INTEGER", "ALPHA", "LUMINANCE", "DEPTH_COMPONENT", "RG", "RG_INTEGER", "LUMINANCE_ALPHA",
        "RGB", "RGB_INTEGER", "RGBA", "RGBA_INTEGER"
    ];
    names.forEach((name, index) => { gl[name] = 0x1000 + index; });
    const listeners = new Map();
    const canvas = {
        id: "", hidden: false, width: 0, height: 0, removed: false,
        setAttribute() {},
        addEventListener(name, listener) { listeners.set(name, listener); },
        removeEventListener(name, listener) { assert.equal(listeners.get(name), listener); listeners.delete(name); },
        getContext(kind) { assert.equal(kind, "webgl2"); return gl; },
        remove() { this.removed = true; }
    };
    const environment = {
        document: { createElement(name) { assert.equal(name, "canvas"); return canvas; }, body: { appendChild(value) { assert.equal(value, canvas); } } }
    };
    vm.createContext(environment);
    vm.runInContext(source, environment);
    const api = environment.UnoDrawWebGlSkia;
    const state = api.createCanvas(reason => failures.push(reason), () => initialized++);
    api.prepareCanvas(state);
    canvas.width = 1250; canvas.height = 1000;
    const texture = { width: canvas.width, height: canvas.height, format: "rgba8unorm" };
    const device = {
        pushes: 0, pops: 0,
        pushErrorScope(filter) { assert.equal(filter, "validation"); this.pushes++; },
        popErrorScope() { this.pops++; const value = deferred(); validations.push(value); return value.promise; }
    };
    const queue = {
        copyExternalImageToTexture(source, destination, extent) { calls.push({ method: "copy", source, destination, extent }); },
        onSubmittedWorkDone() { const value = deferred(); completions.push(value); return value.promise; }
    };
    return { api, state, canvas, gl, listeners, calls, failures, validations, completions, texture, device, queue,
        initialized: () => initialized, copy: () => api.copyCanvas(state, device, queue, texture, canvas.width, canvas.height) };
}

test("the real transfer preserves native dimensions, premultiplication, sRGB and orientation", async () => {
    const f = fixture();
    assert.equal(f.canvas.hidden, true);
    assert.equal(f.gl.drawingBufferColorSpace, "srgb");
    f.copy();
    const transfer = f.calls.find(call => call.method === "copy");
    assert.equal(transfer.source.source, f.canvas);
    assert.equal(transfer.source.flipY, false);
    assert.equal(transfer.destination.texture, f.texture);
    assert.equal(transfer.destination.premultipliedAlpha, true);
    assert.equal(transfer.destination.colorSpace, "srgb");
    assert.deepEqual({ ...transfer.extent }, { width: 1250, height: 1000, depthOrArrayLayers: 1 });
    assert.equal(f.initialized(), 0);
    f.validations[0].resolve(null);
    await drain();
    assert.equal(f.initialized(), 0);
    assert.equal(f.completions.length, 1);
    f.completions[0].resolve();
    await drain();
    assert.equal(f.initialized(), 1);
    f.copy(); f.validations[1].resolve(null);
    await drain();
    assert.equal(f.completions.length, 1, "completion observation is initialization-only");
    assert.equal(f.device.pushes, f.device.pops);
});

test("a scoped GPU validation error fails the actual transfer and never reports initialization", async () => {
    const f = fixture();
    f.copy();
    f.validations[0].resolve({ message: "invalid destination texture" });
    await drain();
    assert.match(f.failures[0], /validation failed.*invalid destination/);
    assert.equal(f.initialized(), 0);
    assert.throws(f.copy, /invalid destination/);
    assert.equal(f.failures.length, 1);
});

test("queue completion rejection cannot count as a successful bridge", async () => {
    const f = fixture();
    f.copy(); f.validations[0].resolve(null);
    await drain();
    f.completions[0].reject(new Error("device lost"));
    await drain();
    assert.match(f.failures[0], /device lost/);
    assert.equal(f.initialized(), 0);
});

test("synchronous copy failure still pops the scope and cannot initialize", async () => {
    const f = fixture();
    f.queue.copyExternalImageToTexture = () => { throw new Error("copy refused"); };
    assert.throws(f.copy, /copy refused/);
    assert.equal(f.device.pushes, 1);
    assert.equal(f.device.pops, 1);
    f.validations[0].resolve(null);
    await drain();
    assert.equal(f.completions.length, 0);
    assert.equal(f.initialized(), 0);
});

test("dimension and format mismatches fail before any browser copy", () => {
    const f = fixture();
    f.texture.width--;
    assert.throws(f.copy, /dimensions or format differ/);
    f.texture.width++;
    f.texture.format = "bgra8unorm";
    assert.throws(f.copy, /dimensions or format differ/);
    assert.equal(f.device.pushes, 0);
});

test("context loss is reported once and disposal detaches callbacks and releases the owned context", async () => {
    const f = fixture();
    f.copy();
    f.listeners.get("webglcontextlost")();
    f.listeners.get("webglcontextlost")();
    assert.equal(f.failures.length, 1);
    assert.match(f.failures[0], /context was lost/);
    f.api.disposeCanvas(f.state);
    f.api.disposeCanvas(f.state);
    assert.equal(f.listeners.size, 0);
    assert.equal(f.canvas.removed, true);
    assert.equal(f.gl.losses, 1);
    assert.equal(f.state.canvas, null);
    f.validations[0].resolve(null);
    await drain();
    assert.equal(f.initialized(), 0);
    assert.equal(f.completions.length, 0);
});

test("unloading while the initialization copy is in flight cannot resurrect a bridge", async () => {
    const f = fixture();
    f.copy(); f.validations[0].resolve(null);
    await drain();
    f.api.disposeCanvas(f.state);
    f.completions[0].resolve();
    await drain();
    assert.equal(f.initialized(), 0);
    assert.equal(f.failures.length, 0);
});

test("glyph-atlas uploads retain row length, alignment and skip rows/pixels without copying bytes", () => {
    const f = fixture();
    const buffer = new ArrayBuffer(256);
    const cropped = new Uint8Array(buffer, 16, 16);
    f.gl.pixelStorei(f.gl.UNPACK_ROW_LENGTH, 8);
    f.gl.pixelStorei(f.gl.UNPACK_SKIP_ROWS, 1);
    f.gl.pixelStorei(f.gl.UNPACK_SKIP_PIXELS, 1);
    f.gl.texSubImage2D(0, 0, 3, 4, 2, 2, f.gl.RGBA, f.gl.UNSIGNED_BYTE, cropped);
    const upload = f.calls.at(-1).args[8];
    assert.equal(upload.buffer, buffer);
    assert.equal(upload.byteOffset, 16);
    assert.equal(upload.byteLength, 76); // two 32-byte rows, then three RGBA pixels
    assert.equal(cropped.byteLength, 16);
    assert.deepEqual(f.calls.filter(call => call.method === "pixelStorei").map(call => call.value), [8, 1, 1]);
});

test("stride repair preserves a packed 16-bit upload type and never extends beyond its backing memory", () => {
    const f = fixture();
    f.gl.pixelStorei(f.gl.UNPACK_ALIGNMENT, 8);
    f.gl.pixelStorei(f.gl.UNPACK_ROW_LENGTH, 5);
    const buffer = new ArrayBuffer(128);
    const cropped = new Uint16Array(buffer, 8, 6);
    f.gl.texImage2D(0, 0, 0, 3, 2, 0, f.gl.RGB, f.gl.UNSIGNED_SHORT_5_6_5, cropped);
    const upload = f.calls.at(-1).args[8];
    assert.ok(upload instanceof Uint16Array);
    assert.equal(upload.buffer, buffer);
    assert.equal(upload.byteOffset, 8);
    assert.equal(upload.byteLength, 22); // one aligned 16-byte row plus six bytes
    const tooSmall = new Uint16Array(new ArrayBuffer(12));
    f.gl.texImage2D(0, 0, 0, 3, 2, 0, f.gl.RGB, f.gl.UNSIGNED_SHORT_5_6_5, tooSmall);
    assert.equal(f.calls.at(-1).args[8], tooSmall, "let WebGL reject an actually invalid source");
});

test("a context without the requested premultiplied coverage semantics is rejected", () => {
    const f = fixture();
    f.gl.attributes.premultipliedAlpha = false;
    assert.throws(() => f.api.prepareCanvas(f.state), /alpha and coverage semantics/);
});
