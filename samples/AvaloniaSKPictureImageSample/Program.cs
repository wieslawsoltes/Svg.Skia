using System;
using Avalonia;
using Avalonia.Controls.Skia;

namespace AvaloniaSKPictureImageSample;

internal class Program
{
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
    {
        GC.KeepAlive(typeof(SKPictureImage).Assembly);
        GC.KeepAlive(typeof(SKPictureControl).Assembly);
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions { })
            .UseSkia()
            .LogToTrace();
    }
}
