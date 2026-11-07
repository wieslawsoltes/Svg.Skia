using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using Svg.Editor.Skia;
using Windows.System;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private void InitializeEditorZoom()
    {
        var input = new TextBox { Header = "Zoom percentage", MinWidth = 180 };
        var status = StudioHint(string.Empty);
        var content = new StackPanel { Spacing = 8 };
        var flyout = new Flyout { Content = content };
        flyout.Opening += (_, _) => { input.Text = Canvas.ZoomPercent.ToString("0.##", CultureInfo.CurrentCulture) + "%"; status.Text = string.Empty; };
        void Apply()
        {
            var text = input.Text.Trim().TrimEnd('%').Trim();
            if ((!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var percentage)
                && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out percentage))
                || !double.IsFinite(percentage) || percentage < SvgEditorViewport.MinimumZoom * 100 || percentage > SvgEditorViewport.MaximumZoom * 100)
            { status.Text = $"Enter a zoom between {SvgEditorViewport.MinimumZoom * 100}% and {SvgEditorViewport.MaximumZoom * 100}%."; return; }
            Canvas.Viewport.SetZoomAt(percentage / 100, new SKPoint((float)Canvas.Viewport.Width / 2, (float)Canvas.Viewport.Height / 2));
            flyout.Hide(); Focus(FocusState.Programmatic);
        }
        input.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Enter) { args.Handled = true; Apply(); }
        };
        content.Children.Add(input);
        var presets = new StudioFieldGrid { MinimumColumnWidth = 54, MaximumColumns = 3 };
        foreach (var percentage in new[] { 25, 50, 75, 100, 150, 200, 400, 800, 1600 })
        {
            var button = new Button { Content = percentage + "%", Style = (Style)Resources["CompactButton"] };
            button.Click += (_, _) => { input.Text = percentage.ToString(CultureInfo.InvariantCulture); Apply(); };
            presets.Children.Add(button);
        }
        content.Children.Add(presets);
        var apply = new Button { Content = "Set zoom", HorizontalAlignment = HorizontalAlignment.Stretch };
        apply.Click += (_, _) => Apply(); content.Children.Add(apply); content.Children.Add(status);
        ZoomLabel.Flyout = flyout;
    }
}
