using Avalonia;
using Avalonia.ReactiveUI;

namespace TestApp
{
    class Program
    {
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .UseSkia()
                .UseReactiveUI();
    }
}
