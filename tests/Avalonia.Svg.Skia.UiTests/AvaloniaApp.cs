using System;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Avalonia.Svg.Skia.UiTests;

public static class AvaloniaApp
{
    public static void Stop()
    {
        var app = GetApp();

        if (app is IDisposable disposable)
        {
            Dispatcher.UIThread.Post(disposable.Dispose);
        }

        if (app != null)
        {
            Dispatcher.UIThread.Post(() => app.Shutdown());
        }
    }

    public static Window? GetMainWindow()
    {
        return GetApp()?.MainWindow;
    }

    private static IClassicDesktopStyleApplicationLifetime? GetApp()
    {
        return (IClassicDesktopStyleApplicationLifetime?)Application.Current?.ApplicationLifetime;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect();
}
