using Uno.Svg.Skia;
using Uno.UI.Hosting;

namespace UnoTestApp;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        SvgSource.SkiaModel.Settings.EnableJavaScript = true;
        SvgSource.SkiaModel.Settings.EnableExternalJavaScript = true;

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
