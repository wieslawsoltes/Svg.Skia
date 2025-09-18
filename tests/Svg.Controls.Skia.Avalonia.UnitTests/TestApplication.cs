using Avalonia;
using Avalonia.Headless;
using Avalonia.Platform;

[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(Avalonia.Svg.Skia.UnitTests.SvgControlsSkiaAvaloniaTestsAppBuilder))]

namespace Avalonia.Svg.Skia.UnitTests;

internal static class SvgControlsSkiaAvaloniaTestsAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<SvgControlsSkiaAvaloniaTestsApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .LogToTrace();
}

internal sealed class SvgControlsSkiaAvaloniaTestsApp : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        AssetLoader.SetDefaultAssembly(typeof(SvgControlsSkiaAvaloniaTestsApp).Assembly);
        base.OnFrameworkInitializationCompleted();
    }
}
