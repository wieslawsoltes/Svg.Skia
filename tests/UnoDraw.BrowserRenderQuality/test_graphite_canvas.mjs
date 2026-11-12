import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import vm from "node:vm";

const source = readFileSync(new URL(
    "../../samples/UnoDraw/Platforms/WebAssembly/Rendering/WebGpuGraphiteCanvas.js", import.meta.url), "utf8");

function deferred() {
    let resolve, reject;
    const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
    return { promise, resolve, reject };
}

const settle = () => new Promise(resolve => setImmediate(resolve));

function fixture(search = "") {
    const calls = [], validations = [], completions = [], failures = [];
    let initialized = 0;
    let clock = 0;
    const environment = { performance: { now: () => clock++ }, URLSearchParams, location: { search } };
    vm.createContext(environment);
    vm.runInContext(source, environment);
    const api = environment.UnoDrawGraphite;
    const device = {
        pushErrorScope(kind) { assert.equal(kind, "validation"); calls.push("push"); },
        popErrorScope() {
            calls.push("pop");
            const value = deferred(); validations.push(value); return value.promise;
        },
        destroy() { throw new Error("a borrowed device cannot be destroyed"); }
    };
    const queue = {
        onSubmittedWorkDone() {
            calls.push("completion");
            const value = deferred(); completions.push(value); return value.promise;
        },
        submit() { throw new Error("the lifetime module cannot submit graphics work"); }
    };
    const state = api.createState(device, queue, reason => failures.push(reason), () => initialized++);
    return { api, state, device, queue, environment, calls, validations, completions, failures,
        initialized: () => initialized,
        submissions: () => environment.__unoDrawGraphiteDocumentSubmissions,
        frame(submitted = true) { api.beginFrame(state); api.endFrame(state, submitted); } };
}

test("coverage defaults preserve the native setting and the explicit raster diagnostic requests one", () => {
    for (const [search, count] of [
        ["", 0], ["?canvasBridge=graphite", 0],
        ["?canvasBridge=graphite&graphiteCoverage=default", 0],
        ["?canvasBridge=graphite&graphiteCoverage=raster", 1]
    ]) {
        const f = fixture(search);
        assert.equal(f.api.getInternalMultisampleCount(), count);
        assert.equal(f.submissions(), 0);
        assert.equal(f.initialized(), 0);
        assert.deepEqual(f.calls, [], "reading coverage does not submit or wait for GPU work");
    }
});

test("coverage is parsed only once and remains fixed across later states and URL changes", () => {
    for (const [initial, changed, count] of [
        ["?graphiteCoverage=raster", "?graphiteCoverage=default", 1],
        ["", "?graphiteCoverage=raster", 0]
    ]) {
        const f = fixture(initial);
        f.environment.location.search = changed;
        f.api.createState(f.device, f.queue, () => {}, () => {});
        assert.equal(f.api.getInternalMultisampleCount(), count);
        Object.defineProperty(f.environment.location, "search", {
            get() { throw new Error("query must not be read again"); }
        });
        assert.equal(f.api.getInternalMultisampleCount(), count);
        assert.deepEqual(f.calls, []);
    }
});

test("unknown or duplicate coverage selectors cannot silently claim a different experiment", () => {
    for (const search of [
        "?graphiteCoverage=", "?graphiteCoverage=unknown", "?graphiteCoverage=Raster",
        "?graphiteCoverage=default&graphiteCoverage=raster",
        "?graphiteCoverage=raster&graphiteCoverage=raster"
    ])
        assert.throws(() => fixture(search), /graphiteCoverage must be specified once as default or raster/);
});

test("creating the lifetime observer performs no drawing, queue wait or initialization", () => {
    const f = fixture();
    assert.equal(f.submissions(), 0);
    assert.equal(f.initialized(), 0);
    assert.deepEqual(f.calls, []);
    assert.throws(() => f.api.createState(null, f.queue, () => {}, () => {}), /unavailable/);
});

test("the shared-device scope closes synchronously and initialization waits for validation and completion", async () => {
    const f = fixture();
    f.frame();
    assert.deepEqual(f.calls, ["push", "pop", "completion"]);
    assert.equal(f.state.open, false);
    assert.equal(f.submissions(), 1);
    assert.equal(f.initialized(), 0);
    f.completions[0].resolve();
    await settle();
    assert.equal(f.initialized(), 0);
    f.validations[0].resolve(null);
    await settle();
    assert.equal(f.initialized(), 1);
    assert.equal(f.failures.length, 0);
});

test("discarded document frames never increment submissions or wait for initial queue completion", async () => {
    const f = fixture();
    f.frame(false);
    assert.equal(f.submissions(), 0);
    assert.equal(f.completions.length, 0);
    f.validations[0].resolve(null);
    await settle();
    assert.equal(f.initialized(), 0);
    f.frame();
    f.validations[1].resolve(null); f.completions[0].resolve();
    await settle();
    assert.equal(f.initialized(), 1);
    assert.equal(f.submissions(), 1);
});

test("multiple accepted frames share the initial completion and initialize only once", async () => {
    const f = fixture();
    f.frame(); f.frame();
    assert.equal(f.completions.length, 1);
    f.validations[0].resolve(null); f.validations[1].resolve(null); f.completions[0].resolve();
    await settle();
    f.frame(); f.validations[2].resolve(null);
    await settle();
    assert.equal(f.submissions(), 3);
    assert.equal(f.initialized(), 1);
    assert.equal(f.completions.length, 1, "queue waits are not part of every frame");
});

test("simultaneous validation and queue rejection are both observed and fail the owner only once", async () => {
    const f = fixture();
    f.frame();
    f.validations[0].reject(new Error("validation promise failed"));
    f.completions[0].reject(new Error("queue promise failed"));
    await settle();
    assert.equal(f.failures.length, 1);
    assert.match(f.failures[0], /validation promise failed/);
    assert.equal(f.initialized(), 0);
    assert.throws(() => f.api.beginFrame(f.state), /lifetime is invalid/);
});

test("a validation error object rejects a successfully submitted frame and prevents initialization", async () => {
    const f = fixture();
    f.frame();
    f.validations[0].resolve({ message: "invalid wrapped texture" });
    f.completions[0].resolve();
    await settle();
    assert.deepEqual(f.failures, ["invalid wrapped texture"]);
    assert.equal(f.initialized(), 0);
    // The counter records native submissions, not validation or presentation.
    // The failure log independently makes this an unacceptable benchmark run.
    assert.equal(f.submissions(), 1);
});

test("both asynchronous and synchronous completion failures reject initialization", async () => {
    for (const synchronous of [false, true]) {
        const f = fixture();
        if (synchronous) f.queue.onSubmittedWorkDone = () => { throw new Error("queue lost"); };
        f.frame();
        f.validations[0].resolve(null);
        if (!synchronous) f.completions[0].reject(new Error("queue lost"));
        await settle();
        assert.deepEqual(f.failures, ["queue lost"]);
        assert.equal(f.initialized(), 0);
    }
});

test("a synchronous pop failure cannot count or initialize the rejected frame", () => {
    const f = fixture();
    f.device.popErrorScope = () => { throw new Error("scope unavailable"); };
    f.api.beginFrame(f.state);
    assert.throws(() => f.api.endFrame(f.state, true), /scope unavailable/);
    assert.equal(f.state.open, false);
    assert.equal(f.submissions(), 0);
    assert.equal(f.completions.length, 0);
    assert.deepEqual(f.failures, ["scope unavailable"]);
});

test("a failed push does not leave an open scope and a frame cannot reenter", async () => {
    const f = fixture();
    const original = f.device.pushErrorScope;
    f.device.pushErrorScope = () => { throw new Error("push failed"); };
    assert.throws(() => f.api.beginFrame(f.state), /push failed/);
    assert.equal(f.state.open, false);
    f.device.pushErrorScope = original;
    f.api.beginFrame(f.state);
    assert.throws(() => f.api.beginFrame(f.state), /lifetime is invalid/);
    f.api.endFrame(f.state, false);
    f.validations[0].resolve(null);
    await settle();
    assert.equal(f.submissions(), 0);
});

test("retirement removes managed callbacks and late completions cannot resurrect an unloaded owner", async () => {
    const f = fixture();
    f.frame();
    f.api.retire(f.state);
    assert.equal(f.state.unavailable, null);
    assert.equal(f.state.initialized, null);
    assert.throws(() => f.api.beginFrame(f.state), /lifetime is invalid/);
    f.validations[0].reject(new Error("late validation failure"));
    f.completions[0].reject(new Error("late queue failure"));
    await settle();
    assert.equal(f.initialized(), 0);
    assert.deepEqual(f.failures, []);
});

test("retirement distinguishes a drained queue from rejected or synchronously lost work", async () => {
    const f = fixture();
    f.api.retire(f.state);
    const drained = f.api.waitForQueue(f.state);
    f.completions[0].resolve();
    assert.equal(await drained, "");
    const rejected = f.api.waitForQueue(f.state);
    f.completions[1].reject(new Error("device lost before retirement"));
    assert.equal(await rejected, "device lost before retirement");
    f.queue.onSubmittedWorkDone = () => { throw new Error("queue unavailable"); };
    assert.equal(await f.api.waitForQueue(f.state), "queue unavailable");
});

test("the optional drain deduplicates shared queues including retiring states and submits no work", async () => {
    const f = fixture();
    const other = f.api.createState(f.device, f.queue, () => {}, () => {});
    f.api.retire(other);
    const pending = f.environment.__unoDrawGraphiteDrain();
    await settle();
    assert.equal(f.completions.length, 1);
    f.completions[0].resolve();
    const result = await pending;
    assert.equal(result.completed, true);
    assert.equal(result.queueCount, 1);
    assert.ok(result.duration_ms >= 0);
    assert.equal(result.errors.length, 0);
    assert.equal(f.submissions(), 0);
    assert.equal(f.initialized(), 0);
});

test("diagnostic drain reports every failed queue rather than treating rejection as idle", async () => {
    const f = fixture();
    const otherQueue = { onSubmittedWorkDone() { throw new Error("second queue failed"); } };
    f.api.createState(f.device, otherQueue, () => {}, () => {});
    const pending = f.environment.__unoDrawGraphiteDrain();
    await settle();
    f.completions[0].reject(new Error("first queue failed"));
    const result = await pending;
    assert.equal(result.completed, false);
    assert.equal(result.queueCount, 2);
    assert.deepEqual(Array.from(result.errors), ["first queue failed", "second queue failed"]);
});

test("release forgets only owned state and a drain with no remaining queues is unavailable", async () => {
    const f = fixture();
    f.api.releaseState(f.state);
    f.api.releaseState(f.state);
    assert.equal(f.state.device, null);
    assert.equal(f.state.queue, null);
    assert.equal(f.state.unavailable, null);
    assert.equal(f.state.initialized, null);
    const result = await f.environment.__unoDrawGraphiteDrain();
    assert.equal(result.queueCount, 0);
    assert.equal(result.completed, false);
    assert.equal(result.errors.length, 0);
    assert.deepEqual(f.calls, []);
});
