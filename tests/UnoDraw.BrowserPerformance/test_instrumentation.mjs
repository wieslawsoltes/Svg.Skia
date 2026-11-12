import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import vm from "node:vm";

const source = readFileSync(new URL("instrumentation.js", import.meta.url), "utf8");

function browser() {
    let clock = 0;
    let requestId = 0;
    const callbacks = new Map();
    const listeners = new Map();
    class WebGLRenderingContext {
        drawArrays(mode, first, count) {
            if (arguments.length < 3) throw new TypeError("drawArrays requires three arguments");
            this.lastDraw = { mode, first, count };
            return this;
        }
        drawElements(mode, count, type, offset) {
            if (arguments.length < 4) throw new TypeError("drawElements requires four arguments");
            if (count < 0) throw new Error("invalid draw");
        }
        flush() {}
        texImage2D() {}
        texSubImage2D() {}
        getExtension() { return null; }
        getParameter() { return "test renderer"; }
    }
    class GPUCanvasContext { getCurrentTexture() { return {}; } }
    class GPUQueue { submit() {} writeTexture() {} }
    class HTMLCanvasElement {
        constructor(connected = true, width = 1440, height = 960) {
            this.isConnected = connected;
            this.width = width;
            this.height = height;
            this.context = null;
        }
        getBoundingClientRect() { return { width: this.width, height: this.height }; }
        getContext(kind) {
            if (!this.context) this.context = kind === "webgpu" ? new GPUCanvasContext() : new WebGLRenderingContext();
            return this.context;
        }
    }
    const environment = {
        HTMLCanvasElement, WebGLRenderingContext, GPUCanvasContext, GPUQueue,
        performance: { now: () => clock },
        requestAnimationFrame(callback) { callbacks.set(++requestId, callback); return requestId; },
        cancelAnimationFrame(id) { callbacks.delete(id); },
        addEventListener(name, callback) { listeners.set(name, callback); },
        advance(value) { clock += value; },
    };
    environment.window = environment;
    const context = vm.createContext(environment);
    vm.runInContext(source, context);
    return {
        context, api: context.__unoDrawBenchmark, Canvas: HTMLCanvasElement, Queue: GPUQueue,
        frame() {
            clock += 16;
            const current = [...callbacks.values()];
            callbacks.clear();
            for (const callback of current) callback(clock);
        },
        input(type, extra = {}) { listeners.get(type)({ type, isTrusted: true, ...extra }); },
        get pendingCallbacks() { return callbacks.size; },
    };
}

test("observer never schedules a frame until an interval is explicitly started", () => {
    const app = browser();
    assert.equal(app.pendingCallbacks, 0);
    const canvas = new app.Canvas();
    canvas.getContext("webgl").drawArrays(4, 0, 3);
    app.api.selectCanvas();
    assert.equal(app.pendingCallbacks, 0);
});

test("idle rAF activity is not claimed as graphics or physical presentation", () => {
    const app = browser();
    new app.Canvas().getContext("webgl");
    app.api.selectCanvas();
    app.api.start("idle", []);
    for (let index = 0; index < 5; index++) app.frame();
    const sample = app.api.stop();
    assert.equal(sample.raf_ms.length, 5);
    assert.equal(sample.graphics_active_intervals_ms.length, 0);
    assert.equal(sample.webgl_flush_ms.length, 0);
    assert.equal(sample.application_raf_callback_ms.length, 0);
    assert.equal(sample.completed_render_callback_ms.length, 0);
    assert.equal(app.pendingCallbacks, 0);
});

test("detached capability probes cannot prove application rendering", () => {
    const app = browser();
    const real = new app.Canvas().getContext("webgl");
    const probe = new app.Canvas(false, 4096, 4096).getContext("webgl");
    app.api.selectCanvas();
    app.api.start("pan", ["pointermove"]);
    app.frame();
    probe.drawArrays(4, 0, 3);
    probe.flush();
    app.input("pointermove");
    real.drawArrays(4, 0, 3);
    real.drawArrays(4, 0, 3);
    real.flush();
    const sample = app.api.stop();
    assert.equal(sample.calls["webgl.drawArrays"], 2);
    assert.equal(sample.webgl_flush_ms.length, 1);
    assert.equal(sample.graphics_active_intervals_ms.length, 1);
    assert.equal(sample.input_to_next_graphics_ms.length, 1);
});

test("untrusted and modifier-only input cannot satisfy the workload", () => {
    const app = browser();
    new app.Canvas().getContext("webgl");
    app.api.selectCanvas();
    app.api.start("selection", ["keydown"]);
    app.input("keydown", { key: "Control" });
    app.input("keydown", { key: "a", isTrusted: false });
    app.input("pointermove");
    app.input("keydown", { key: "a" });
    const sample = app.api.stop();
    assert.equal(sample.input_events, 1);
    assert.equal(sample.inputs_without_later_graphics, 1);
});

test("WebGPU submission requires the application's acquired canvas texture", () => {
    const app = browser();
    const context = new app.Canvas().getContext("webgpu");
    const queue = new app.Queue();
    app.api.selectCanvas();
    app.api.start("zoom", ["wheel"]);
    queue.submit([]);
    context.getCurrentTexture();
    queue.submit([]);
    queue.submit([]);
    const sample = app.api.stop();
    assert.equal(sample.webgpu_canvas_submit_ms.length, 1);
    assert.equal(sample.graphics_active_intervals_ms.length, 1);
});

test("application callback CPU time excludes the observer's own rAF", () => {
    const app = browser();
    new app.Canvas().getContext("webgl");
    app.api.selectCanvas();
    app.api.start("pan", []);
    vm.runInContext("requestAnimationFrame(() => advance(3))", app.context);
    app.frame();
    app.frame();
    const sample = app.api.stop();
    assert.equal(sample.application_raf_callback_ms.length, 1);
    assert.equal(sample.application_raf_callback_ms[0], 3);
});

test("a throwing graphics call is not recorded as successful drawing", () => {
    const app = browser();
    const context = new app.Canvas().getContext("webgl");
    app.api.selectCanvas();
    app.api.start("edit", []);
    assert.throws(() => context.drawElements(4, -1, 5123, 0));
    const sample = app.api.stop();
    assert.equal(sample.graphics_active_intervals_ms.length, 0);
});

test("completed CPU render callbacks require drawing and a successful return", () => {
    const app = browser();
    app.context.gl = new app.Canvas().getContext("webgl");
    app.api.selectCanvas();
    app.api.start("pan", ["pointermove"]);
    app.input("pointermove");
    vm.runInContext("requestAnimationFrame(() => { gl.drawArrays(4, 0, 3); advance(4); })", app.context);
    app.frame();
    vm.runInContext("requestAnimationFrame(() => { gl.flush(); advance(1); })", app.context);
    app.frame();
    vm.runInContext("requestAnimationFrame(() => { gl.drawArrays(4, 0, 3); throw new Error('failed frame'); })", app.context);
    assert.throws(() => app.frame(), /failed frame/);
    const sample = app.api.stop();
    assert.equal(sample.application_raf_callback_ms.length, 3);
    assert.equal(sample.completed_render_callback_ms.length, 1);
    assert.equal(sample.completed_render_callback_ms[0], 4);
    assert.equal(sample.input_to_completed_render_callback_ms.length, 1);
    assert.equal(sample.inputs_without_completed_render_callback, 0);
});

test("fixed-arity hot wrappers preserve receiver, return value and missing-argument errors", () => {
    const app = browser();
    const gl = new app.Canvas().getContext("webgl");
    app.api.selectCanvas();
    app.api.start("pan", []);
    assert.equal(gl.drawArrays(4, 12, 24), gl);
    assert.deepEqual(gl.lastDraw, { mode: 4, first: 12, count: 24 });
    assert.throws(() => gl.drawArrays(4, 0), /requires three arguments/);
    assert.equal(app.api.stop().calls["webgl.drawArrays"], 1);
});

test("upload byte counter reports supplied buffer size without inventing a transfer extent", () => {
    const app = browser();
    const gl = new app.Canvas().getContext("webgl");
    app.api.selectCanvas();
    app.api.start("pan", []);
    gl.texSubImage2D(0, 0, 0, 0, 2, 2, 0, 0, new Uint8Array(100), 50);
    const sample = app.api.stop();
    assert.equal(sample.supplied_upload_buffer_bytes, 100);
    assert.equal(sample.upload_calls_without_buffer_size, 0);
    assert.equal(sample.known_upload_bytes, undefined);
});
