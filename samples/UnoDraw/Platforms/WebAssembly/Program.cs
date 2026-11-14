using Microsoft.Extensions.Logging;
using Uno.Foundation;
using Uno.UI.Composition.Drawing;
using Uno.UI.Composition.Skia;
using Uno.UI.Composition.WebGpu;
using Uno.UI.Hosting;

namespace UnoDraw;

public static class Program
{
    public static async Task Main(string[] args)
    {
        BrowserStorageSynchronization.Initialize();
        BrowserRenderDiagnostics.Initialize();
        InitializeLogging();
        // Use direct Skia/WebGL by default. WebGPU is an explicit startup-only
        // option, with the same Skia fallback and no document setting changes.
        var preference = WebAssemblyRuntime.InvokeJS(
            "new URLSearchParams(globalThis.location.search).get('renderer') || ''").Trim();
        var preferWebGpu = string.Equals(preference, "webgpu", StringComparison.OrdinalIgnoreCase);
        if (preferWebGpu)
            Rendering.WebGpuSkiaCanvasBridge.Register();
        var builder = UnoPlatformHostBuilder.Create().App(() => new App());
        if (preferWebGpu)
            builder = builder.GraphicsBackend(Rendering.WebGpuSkiaCanvasBridge.ObserveWebGpuProvider(
                WebGpuBackend.CreateGraphicsProvider()));
        Console.WriteLine(preferWebGpu
            ? "UnoDraw browser renderer preference: WebGPU, then Skia (WebGL, then software)."
            : "UnoDraw browser renderer preference: Skia (WebGL, then software).");
        var host = builder
            .GraphicsBackend(Rendering.WebGpuSkiaCanvasBridge.ObserveSkiaProvider(
                SkiaBackend.CreateGraphicsProvider(GraphicsContextKind.WebGL, GraphicsContextKind.Software)))
            .UseWebAssembly()
            .Build();
        BrowserStartup.DeferInitialFrame();
        await host.RunAsync();
    }

    private static void InitializeLogging()
    {
        // The host negotiates its renderer before the first frame. Keep its actual
        // selection visible in Release builds, including a browser's Skia fallback.
        var factory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new Uno.Extensions.Logging.WebAssembly.WebAssemblyConsoleLoggerProvider());
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddFilter("Uno.UI.Composition.Drawing", LogLevel.Information);
            builder.AddFilter("Uno.WinUI.Graphics2DSK", LogLevel.Information);
        });
        Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = factory;
        Uno.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
    }
}
