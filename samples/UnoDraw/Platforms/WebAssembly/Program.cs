using Uno.UI.Hosting;

namespace UnoDraw;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseWebAssembly()
            .Build();
        BrowserStartup.DeferInitialFrame();
        await host.RunAsync();
    }
}
