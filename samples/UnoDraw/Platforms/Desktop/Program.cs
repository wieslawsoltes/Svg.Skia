using Svg.Skia;
using Uno.UI.Hosting;

namespace UnoDraw;

internal static class Program
{
    internal static string? StartupFile { get; private set; }

    [STAThread]
    private static void Main(string[] args)
    {
        StartupFile = args.FirstOrDefault(argument =>
            argument.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
            argument.EndsWith(".svgz", StringComparison.OrdinalIgnoreCase));
        SKSvgJavaScriptRuntime.Register();
        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();
        host.Run();
    }
}
