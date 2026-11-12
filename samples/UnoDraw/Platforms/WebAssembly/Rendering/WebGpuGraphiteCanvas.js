(() => {
    const states = new Set();
    const errorText = error => String(error?.message || error || "unknown WebGPU error");
    const fail = (state, error) => {
        if (state.retired || state.failed) return;
        state.failed = true;
        state.unavailable(errorText(error));
    };

    globalThis.__unoDrawGraphiteDocumentSubmissions ??= 0;
    // Optional harness diagnostic. It runs only when explicitly called, never
    // from painting or animation, and submits no graphics work of its own.
    globalThis.__unoDrawGraphiteDrain = async () => {
        const queues = [...new Set([...states].map(state => state.queue).filter(Boolean))];
        const started = performance.now();
        const results = await Promise.allSettled(queues.map(queue =>
            Promise.resolve().then(() => queue.onSubmittedWorkDone())));
        const errors = results.filter(result => result.status === "rejected")
            .map(result => errorText(result.reason));
        return { completed: queues.length > 0 && errors.length === 0,
            queueCount: queues.length, duration_ms: performance.now() - started, errors };
    };
    globalThis.UnoDrawGraphite = {
        createState(device, queue, unavailable, initialized) {
            if (!device || !queue) throw new Error("The shared WebGPU device and queue are unavailable.");
            const state = { device, queue, unavailable, initialized, open: false,
                retired: false, failed: false, initializationPending: false, ready: false };
            states.add(state);
            return state;
        },

        beginFrame(state) {
            if (state.retired || state.failed || state.open)
                throw new Error("The Graphite frame lifetime is invalid.");
            // The scope surrounds only the synchronous Graphite work. Pop it
            // before Uno resumes rendering on this same borrowed device.
            state.device.pushErrorScope("validation");
            state.open = true;
        },

        endFrame(state, submitted) {
            if (!state.open) return;
            state.open = false;
            let validation;
            try {
                validation = state.device.popErrorScope();
            } catch (error) {
                fail(state, error);
                throw error;
            }

            // Count actual successful document submissions, never auxiliary
            // canvas paints, context initialization or CPU fallback frames.
            if (submitted && !state.retired && !state.failed)
                globalThis.__unoDrawGraphiteDocumentSubmissions++;

            const first = submitted && !state.retired && !state.failed &&
                !state.ready && !state.initializationPending;
            if (first) state.initializationPending = true;
            let completion = null;
            try {
                if (first) completion = state.queue.onSubmittedWorkDone();
            } catch (error) {
                completion = Promise.reject(error);
            }
            void (async () => {
                try {
                    // Attach both rejection handlers immediately, including when
                    // validation and completion fail in the same frame.
                    const [error] = await Promise.all([validation, completion]);
                    if (error) throw error;
                    if (first && !state.retired && !state.failed) {
                        state.ready = true;
                        state.initialized();
                    }
                } catch (error) {
                    fail(state, error);
                }
            })();
        },

        retire(state) {
            // Pending validation callbacks can finish, but may not resurrect an
            // unloaded canvas or retain its managed owner through their delegates.
            state.retired = true;
            state.unavailable = null;
            state.initialized = null;
        },

        async waitForQueue(state) {
            try {
                await state.queue.onSubmittedWorkDone();
                return "";
            } catch (error) {
                // The managed owner decides whether native destruction is safe.
                // A rejected promise must never be translated to successful idle.
                return errorText(error);
            }
        },

        releaseState(state) {
            states.delete(state);
            state.retired = true;
            state.unavailable = state.initialized = null;
            state.device = state.queue = null;
        }
    };
})();
