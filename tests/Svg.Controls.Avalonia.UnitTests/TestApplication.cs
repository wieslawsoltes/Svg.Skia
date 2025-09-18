using Avalonia;
using Avalonia.Headless;
using Avalonia.Platform;

[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(Avalonia.Svg.UnitTests.SvgControlsAvaloniaTestsAppBuilder))]

namespace Avalonia.Svg.UnitTests;

internal static class SvgControlsAvaloniaTestsAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<SvgControlsAvaloniaTestsApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .LogToTrace();
}

internal sealed class SvgControlsAvaloniaTestsApp : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        AssetLoader.SetDefaultAssembly(typeof(SvgControlsAvaloniaTestsApp).Assembly);
        base.OnFrameworkInitializationCompleted();
    }
}
