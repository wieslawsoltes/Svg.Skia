// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using Avalonia.Threading;

[assembly: SupportedOSPlatform("browser")]

namespace AvaloniaSvgFontBrowser;

internal static partial class Program
{
    public static Task Main(string[] args)
    {
        // This setting is deliberately checked on every loaded SVG below. Applications
        // can configure SvgSource without depending on its legacy implementation fields.
        SvgSource.ConfigureDefaultSettings(settings => settings.EnableTextSelectionRendering = false);

        var builder = AppBuilder.Configure<App>()
            .WithInterFont()
            .LogToTrace();

        // The default mode deliberately only registers Inter. This explicit second
        // mode also verifies the documented application-default configuration.
        if (args.Length > 0 && args[0] == "inter-default")
        {
            builder.With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" });
        }

        return builder.StartBrowserAppAsync("out");
    }

    [JSImport("report", "fontDiagnostics")]
    internal static partial void Report(string message, bool passed);

    [JSExport]
    public static void RunChecksAgain()
    {
        Dispatcher.UIThread.Post(() => (Application.Current as App)?.RunChecks());
    }
}
