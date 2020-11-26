using Avalonia;

namespace AvaloniaSample
{
    class Program
    {
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .UseSkia()
                .With(new Win32PlatformOptions()
                {
                    AllowEglInitialization = false
                })
                .LogToDebug();
    }
}
