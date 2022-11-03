using System.Runtime.Versioning;
using Avalonia;
using Avalonia.ReactiveUI;
using Avalonia.Web;
using TestApp;

[assembly:SupportedOSPlatform("browser")]

internal class Program
{
    private static void Main(string[] args) 
        => BuildAvaloniaApp().SetupBrowserApp("out");

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseReactiveUI();
}
