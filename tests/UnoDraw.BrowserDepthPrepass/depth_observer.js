// Supplemental observer only. It never changes a descriptor, command or input.
(() => {
    "use strict";
    const counts = {initialized: false, renderPasses: 0, depthPasses: 0,
        colorOnlyPasses: 0, unknownPasses: 0};
    const prototype = globalThis.GPUCommandEncoder?.prototype;
    const descriptor = prototype && Object.getOwnPropertyDescriptor(prototype, "beginRenderPass");
    if (descriptor && typeof descriptor.value === "function") {
        const original = descriptor.value;
        Object.defineProperty(prototype, "beginRenderPass", {...descriptor,
            value: function (input) {
                // Preserve native exceptions and every argument. Inspect only after
                // the native call succeeds, without invoking descriptor getters.
                const pass = original.apply(this, arguments);
                counts.renderPasses++;
                try {
                    const attachment = Object.getOwnPropertyDescriptor(input, "depthStencilAttachment");
                    if (!attachment) {
                        // An inherited attachment cannot be assumed absent.
                        if ("depthStencilAttachment" in input) counts.unknownPasses++;
                        else counts.colorOnlyPasses++;
                    } else if (!("value" in attachment)) {
                        counts.unknownPasses++;
                    } else if (attachment.value == null) {
                        counts.colorOnlyPasses++;
                    } else {
                        const view = Object.getOwnPropertyDescriptor(attachment.value, "view");
                        if (view && "value" in view && view.value != null) counts.depthPasses++;
                        else counts.unknownPasses++;
                    }
                } catch {
                    counts.unknownPasses++;
                }
                return pass;
            }});
        counts.initialized = true;
    }
    Object.defineProperty(globalThis, "__unoDrawDepthProbe", {
        value: Object.freeze({snapshot: () => ({...counts})}),
        configurable: false, writable: false
    });
})();
