using Avalonia;

namespace AvaloniaSKPictureImageSample
{
    internal class Program
    {
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(new X11PlatformOptions
                {
                })
                .UseSkia()
                .LogToTrace();
    }
}
