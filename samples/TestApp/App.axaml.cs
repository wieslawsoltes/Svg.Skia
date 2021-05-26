using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TestApp.Models;
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

                LoadConfiguration(mainWindowViewModel);

                desktop.MainWindow = new MainWindow {DataContext = mainWindowViewModel};

                desktop.Exit += (_, _) =>
                {
                    SaveConfiguration(mainWindowViewModel);
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void SaveConfiguration(MainWindowViewModel mainWindowViewModel)
        {
            var configuration = new Configuration()
            {
                Paths = mainWindowViewModel.Items?.Select(x => x.Path).ToList(),
                Query = mainWindowViewModel.ItemQuery
            };

            var json = JsonSerializer.Serialize<Configuration>(configuration);
            File.WriteAllText(ConfigurationPath, json);
        }

        private static void LoadConfiguration(MainWindowViewModel mainWindowViewModel)
        {
            if (!File.Exists(ConfigurationPath))
            {
                return;
            }

            var json = File.ReadAllText(ConfigurationPath);
            var configuration = JsonSerializer.Deserialize<Configuration>(json);

            if (configuration?.Paths is { })
            {
                foreach (var path in configuration.Paths)
                {
                    mainWindowViewModel.Items?.Add(new FileItemViewModel(Path.GetFileName(path), path));
                }
            }

            if (configuration?.Query is { })
            {
                mainWindowViewModel.ItemQuery = configuration.Query;
            }
        }
    }
}
