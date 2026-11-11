using Microsoft.Extensions.Logging;
using Uno.UI.Composition.Skia;
using Uno.UI.Composition.WebGpu;
using Uno.UI.Hosting;

namespace UnoDraw;

public static class Program
{
    public static async Task Main(string[] args)
    {
        InitializeLogging();
        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .GraphicsBackend(WebGpuBackend.CreateGraphicsProvider())
            .GraphicsBackend(SkiaBackend.CreateGraphicsProvider())
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
