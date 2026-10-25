using Uno.Foundation;

namespace UnoDraw;

public sealed partial class App
{
    partial void OnPlatformWindowReady() => BrowserStartup.ReleaseInitialFrame();
}

internal static class BrowserStartup
{
    // Uno 6.5 schedules a resize frame while its browser host is still starting,
    // before Window.CurrentSafe exists. Defer only that startup interval. Restore
    // the original scheduler as soon as our window is activated; rendering errors
    // remain visible and every later frame follows Uno's normal WebGL path.
    internal static void DeferInitialFrame() => WebAssemblyRuntime.InvokeJS("""
        (() => {
            const renderer = globalThis.Uno?.UI?.Runtime?.Skia?.BrowserRenderer;
            if (!renderer || typeof renderer.invalidate !== 'function') return '';
            const original = renderer.invalidate;
            const pending = new Set();
            let ready = false;
            renderer.invalidate = function(instance) {
                if (ready) return original.call(renderer, instance);
                pending.add(instance);
            };
            globalThis.__unoDrawReleaseInitialFrame = () => {
                ready = true;
                renderer.invalidate = original;
                delete globalThis.__unoDrawReleaseInitialFrame;
                for (const instance of pending) original.call(renderer, instance);
                pending.clear();
            };
            return '';
        })()
        """);

    internal static void ReleaseInitialFrame() => WebAssemblyRuntime.InvokeJS("globalThis.__unoDrawReleaseInitialFrame?.(); ''");
}
