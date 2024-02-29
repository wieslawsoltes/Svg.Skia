using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace UITests
{
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
				Dispatcher.UIThread.Post(() => app.Shutdown());
		}

		public static Window? GetMainWindow() => GetApp()?.MainWindow;

		public static IClassicDesktopStyleApplicationLifetime? GetApp() =>
			(IClassicDesktopStyleApplicationLifetime?)Application.Current?.ApplicationLifetime;

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder
                .Configure<App>()
                .UsePlatformDetect();
	}
}
