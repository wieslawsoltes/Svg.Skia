import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import vm from "node:vm";
import test from "node:test";

const source = readFileSync(new URL("depth_observer.js", import.meta.url), "utf8");

function observed(original) {
    const prototype = {beginRenderPass: original};
    const context = vm.createContext({GPUCommandEncoder: {prototype}});
    vm.runInContext(source, context);
    return {prototype, snapshot: () => JSON.parse(JSON.stringify(context.__unoDrawDepthProbe.snapshot()))};
}

test("native receiver, arguments, return values and descriptor objects are preserved", () => {
    const receiver = {}, view = {}, result = {}, input = {depthStencilAttachment: {view}};
    const o = observed(function (...args) {
        assert.equal(this, receiver);
        assert.equal(args[0], input);
        assert.equal(args[1], "extra");
        return result;
    });
    assert.equal(o.prototype.beginRenderPass.call(receiver, input, "extra"), result);
    assert.equal(input.depthStencilAttachment.view, view);
    assert.deepEqual(o.snapshot(), {initialized: true, renderPasses: 1, depthPasses: 1,
        colorOnlyPasses: 0, unknownPasses: 0});
});

test("color-only and depth-bearing native descriptors are counted separately", () => {
    const o = observed(() => ({}));
    for (const input of [{}, {depthStencilAttachment: null}, {depthStencilAttachment: undefined},
        {depthStencilAttachment: {view: {}}}]) o.prototype.beginRenderPass(input);
    assert.deepEqual(o.snapshot(), {initialized: true, renderPasses: 4, depthPasses: 1,
        colorOnlyPasses: 3, unknownPasses: 0});
});

test("the observer never invokes descriptor getters a second time", () => {
    let reads = 0;
    const input = {get depthStencilAttachment() { reads++; return {view: {}}; }};
    const o = observed(input => { void input.depthStencilAttachment; return {}; });
    o.prototype.beginRenderPass(input);
    assert.equal(reads, 1);
    assert.equal(o.snapshot().unknownPasses, 1);
    assert.equal(o.snapshot().depthPasses, 0);
});

test("inherited and malformed attachments are ambiguous, never accepted as disabled", () => {
    const o = observed(() => ({}));
    const inherited = Object.create({depthStencilAttachment: {view: {}}});
    for (const input of [inherited, {depthStencilAttachment: {}},
        {depthStencilAttachment: {get view() { throw Error("must not invoke"); }}}])
        o.prototype.beginRenderPass(input);
    assert.equal(o.snapshot().unknownPasses, 3);
    assert.equal(o.snapshot().colorOnlyPasses, 0);
});

test("native exceptions are preserved and do not count a successful pass", () => {
    const failure = Error("native validation");
    const o = observed(() => { throw failure; });
    assert.throws(() => o.prototype.beginRenderPass({}), error => error === failure);
    assert.equal(o.snapshot().renderPasses, 0);
});

test("missing native API remains explicitly uninitialized", () => {
    const context = vm.createContext({});
    vm.runInContext(source, context);
    assert.equal(context.__unoDrawDepthProbe.snapshot().initialized, false);
});
