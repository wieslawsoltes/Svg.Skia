/*
 * Svg.Skia SVG rendering library.
 * Copyright (C) 2023  Wiesław Šoltés
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TestApp.ViewModels;
using TestApp.Views;

namespace TestApp;

public class App : Application
{
    private const string ConfigurationPath = "TestApp.json";

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var mainWindowViewModel = new MainWindowViewModel();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (File.Exists(ConfigurationPath))
            {
                try
                {
                    using var stream = File.OpenRead(ConfigurationPath);
                    mainWindowViewModel.LoadConfiguration(stream);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    Debug.WriteLine(ex.StackTrace);
                }
            }

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };

            desktop.Exit += (_, _) =>
            {
                using var stream = File.OpenWrite(ConfigurationPath);
                mainWindowViewModel.SaveConfiguration(stream);
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            if (File.Exists(ConfigurationPath))
            {
                try
                {
                    using var stream = File.OpenRead(ConfigurationPath);
                    mainWindowViewModel.LoadConfiguration(stream);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    Debug.WriteLine(ex.StackTrace);
                }
            }

            single.MainView = new MainView
            {
                DataContext = mainWindowViewModel
            };

            single.MainView.DetachedFromVisualTree += (_, _) =>
            {
                using var stream = File.OpenWrite(ConfigurationPath);
                mainWindowViewModel.SaveConfiguration(stream);
            }; 
        }

        base.OnFrameworkInitializationCompleted();
    }
}
