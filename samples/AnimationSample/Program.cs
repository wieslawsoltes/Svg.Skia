using System;
using Avalonia;
using Avalonia.Controls.Skia;

namespace AnimationSample;

internal class Program
{
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
    {
        GC.KeepAlive(typeof(SKPictureControl).Assembly);
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .LogToTrace();
    }
}
