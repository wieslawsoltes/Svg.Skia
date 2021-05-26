using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TestApp.ViewModels;
using TestApp.Views;

namespace TestApp
{
    public class App : Application
    {
        private const string ConfigurationPath = "TestApp.json";

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindowViewModel = new MainWindowViewModel();

                mainWindowViewModel.LoadConfiguration(ConfigurationPath);

                desktop.MainWindow = new MainWindow {DataContext = mainWindowViewModel};

                desktop.Exit += (_, _) =>
                {
                    mainWindowViewModel.SaveConfiguration(ConfigurationPath);
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
