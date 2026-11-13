using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Svg.Editor.Skia.Uno;
using Uno.Foundation;

namespace UnoDraw;

/// <summary>Explicit browser benchmark hooks; normal editor sessions collect no samples.</summary>
public static partial class BrowserRenderDiagnostics
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    internal static void Initialize()
    {
        EditorRenderDiagnostics.Enabled = WebAssemblyRuntime.InvokeJS(
            "new URLSearchParams(globalThis.location.search).get('renderDiagnostics') === '1'") == "true";
        if (WebAssemblyRuntime.InvokeJS(
            "new URLSearchParams(globalThis.location.search).get('inputDiagnostics') === '1'") == "true")
        {
            // Source-editor quality checks observe the existing modal lifecycle.
            // Keep input tracing opt-in and out of every primary timing trial.
            Environment.SetEnvironmentVariable("UNODRAW_EDITOR_INPUT_TRACE", "1");
        }
        if (WebAssemblyRuntime.InvokeJS(
            "new URLSearchParams(globalThis.location.search).get('perfDiagnostics') === '1'") == "true")
        {
            // The compositor reads these once during initialization. Frame logs
            // are a separate diagnostic pass, never part of the primary timings.
            Environment.SetEnvironmentVariable("UNO_LOG_FRAME_PHASES", "1");
            Environment.SetEnvironmentVariable("UNO_WEBGPU_STATS", "1");
        }
    }

    /// <summary>Returns counters without invalidating a visual or subscribing to frame events.</summary>
    [JSExport]
    public static string Snapshot(bool reset)
        => JsonSerializer.Serialize(EditorRenderDiagnostics.Snapshot(reset), s_jsonOptions);
}
