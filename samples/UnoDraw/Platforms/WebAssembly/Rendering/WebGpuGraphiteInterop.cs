using System.Runtime.InteropServices.JavaScript;
using Uno.Foundation;

namespace UnoDraw.Rendering;

/// <summary>Scoped validation and asynchronous retirement of borrowed WebGPU resources.</summary>
internal static partial class WebGpuGraphiteInterop
{
    private static bool s_initialized;

    internal static JSObject CreateState(JSObject device, JSObject queue,
        Action<string> unavailable, Action initialized)
    {
        EnsureInitialized();
        return CreateStateCore(device, queue, unavailable, initialized);
    }

    internal static int GetInternalMultisampleCount()
    {
        EnsureInitialized();
        return GetInternalMultisampleCountCore();
    }

    private static void EnsureInitialized()
    {
        if (!s_initialized)
        {
            using var source = typeof(WebGpuGraphiteInterop).Assembly.GetManifestResourceStream(
                "UnoDraw.WebGpuGraphiteCanvas.source")
                ?? throw new InvalidOperationException("The Graphite lifetime module is missing.");
            using var reader = new StreamReader(source);
            WebAssemblyRuntime.InvokeJS(reader.ReadToEnd());
            s_initialized = true;
        }
    }

    [JSImport("globalThis.UnoDrawGraphite.getInternalMultisampleCount")]
    private static partial int GetInternalMultisampleCountCore();

    [JSImport("globalThis.UnoDrawGraphite.createState")]
    private static partial JSObject CreateStateCore(JSObject device, JSObject queue,
        [JSMarshalAs<JSType.Function<JSType.String>>] Action<string> unavailable,
        [JSMarshalAs<JSType.Function>] Action initialized);

    [JSImport("globalThis.UnoDrawGraphite.beginFrame")]
    internal static partial void BeginFrame(JSObject state);

    [JSImport("globalThis.UnoDrawGraphite.endFrame")]
    internal static partial void EndFrame(JSObject state, bool submitted);

    [JSImport("globalThis.UnoDrawGraphite.retire")]
    internal static partial void Retire(JSObject state);

    [JSImport("globalThis.UnoDrawGraphite.waitForQueue")]
    internal static partial Task<string> WaitForQueueAsync(JSObject state);

    [JSImport("globalThis.UnoDrawGraphite.releaseState")]
    private static partial void ReleaseStateCore(JSObject state);

    internal static void ReleaseState(JSObject state)
    {
        try
        {
            ReleaseStateCore(state);
        }
        finally
        {
            // Only this state object is ours. Device/queue proxies returned by
            // Uno are borrowed and are never disposed by this bridge.
            state.Dispose();
        }
    }
}
