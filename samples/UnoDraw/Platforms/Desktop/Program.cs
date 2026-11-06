using Uno.UI.Hosting;

namespace UnoDraw;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        App.StartupFile = args.FirstOrDefault(argument =>
            argument.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
            argument.EndsWith(".svgz", StringComparison.OrdinalIgnoreCase));
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
