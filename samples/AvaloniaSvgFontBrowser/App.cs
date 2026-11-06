// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace AvaloniaSvgFontBrowser;

public sealed class App : Application
{
    private FontRegressionView? _view;

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime lifetime)
        {
            _view = new FontRegressionView();
            lifetime.MainView = _view;
            _view.RunChecks();
        }

        base.OnFrameworkInitializationCompleted();
    }

    internal void RunChecks() => _view?.RunChecks();
}
