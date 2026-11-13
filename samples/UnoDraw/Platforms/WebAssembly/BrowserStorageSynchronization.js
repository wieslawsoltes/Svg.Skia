(() => {
    "use strict";

    const idbfs = typeof IDBFS === "object" && IDBFS !== null ? IDBFS : null;
    const marker = Symbol.for("UnoDraw.IndexedDbFlushOrdering");
    if (!idbfs || ["syncfs", "getRemoteSet", "getLocalSet", "reconcile"].some(
        name => typeof idbfs[name] !== "function")) {
        return "unavailable";
    }
    if (idbfs[marker]) {
        return idbfs.syncfs === idbfs[marker] ? "already-installed" : "unavailable";
    }
    if (!Object.isExtensible(idbfs)
        || Object.getOwnPropertyDescriptor(idbfs, "syncfs")?.writable !== true) {
        return "unavailable";
    }

    const original = idbfs.syncfs;
    const ordered = function (mount, populate, callback) {
        // Initial hydration retains Emscripten's original ordering and behavior.
        if (populate) return original.call(this, mount, populate, callback);

        // In the pinned runtime, getLocalSet and the local reads initiated by
        // reconcile are synchronous. Enumerate IndexedDB first so recovery-file
        // rename/delete cannot intervene between the local path snapshot and
        // those reads. IndexedDB still owns the original transaction/commit.
        // Do not retry FS.syncfs: its first-error callback can precede completion
        // of another mount. Every genuine error still reaches the caller.
        return idbfs.getRemoteSet(mount, (remoteError, remote) => {
            if (remoteError) return callback(remoteError);
            return idbfs.getLocalSet(mount, (localError, local) => {
                if (localError) return callback(localError);
                return idbfs.reconcile(local, remote, callback);
            });
        });
    };
    idbfs.syncfs = ordered;
    Object.defineProperty(idbfs, marker, { value: ordered });
    return "installed";
})();
