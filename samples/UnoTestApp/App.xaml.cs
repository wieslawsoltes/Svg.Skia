using System;
using System.Diagnostics;
using System.IO;
using TestApp.ViewModels;

namespace UnoTestApp;

public sealed partial class App : Application
{
    private const string ConfigurationPath = "TestApp.json";
    private Window? _mainWindow;

    public App()
    {
        InitializeComponent();
        ViewModel = new MainWindowViewModel(new StorageService());
    }

    internal MainWindowViewModel ViewModel { get; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        TryLoadConfiguration();

        _mainWindow = new Window();
        _mainWindow.Closed += OnMainWindowClosed;

        if (_mainWindow.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            rootFrame.NavigationFailed += OnNavigationFailed;
            _mainWindow.Content = rootFrame;
        }

        if (rootFrame.Content is null)
        {
            rootFrame.Navigate(typeof(MainPage), args.Arguments);
        }

        _mainWindow.Activate();
    }

    private void TryLoadConfiguration()
    {
        if (!File.Exists(ConfigurationPath))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(ConfigurationPath);
            ViewModel.LoadConfiguration(stream);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        try
        {
            using var stream = File.Create(ConfigurationPath);
            ViewModel.SaveConfiguration(stream);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private static void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");
    }
}
