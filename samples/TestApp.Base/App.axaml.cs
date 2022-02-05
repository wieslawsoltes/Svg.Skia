using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TestApp.ViewModels;
using TestApp.Views;

namespace TestApp;

public class App : Application
{
    private const string ConfigurationPath = "TestApp.Base.json";

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var mainWindowViewModel = new MainWindowViewModel();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            mainWindowViewModel.LoadConfiguration(ConfigurationPath);

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };

            desktop.Exit += (_, _) =>
            {
                mainWindowViewModel.SaveConfiguration(ConfigurationPath);
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            mainWindowViewModel.LoadConfiguration(ConfigurationPath);

            single.MainView = new MainView
            {
                DataContext = mainWindowViewModel
            };

            single.MainView.DetachedFromVisualTree += (_, _) =>
            {
                mainWindowViewModel.SaveConfiguration(ConfigurationPath);
            }; 
        }

        base.OnFrameworkInitializationCompleted();
    }
}
