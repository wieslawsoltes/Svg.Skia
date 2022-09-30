using System;
using Avalonia;
using Avalonia.Controls.Skia;

namespace AvaloniaControlsSample;

internal class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
    {
        GC.KeepAlive(typeof(SKCanvasControl).Assembly);
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions
            {
            })
            .UseSkia()
            .LogToTrace();
    }
}
