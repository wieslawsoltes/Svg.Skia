using System.Globalization;
using System.Xml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Svg.Editor.Skia;
using Svg.Editor.Svg;
using Svg.Editor.Svg.Models;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private async Task StrokeProfileDialogAsync()
    {
        if (Controller is not { } controller || controller.SelectedElements.Count != 1
            || controller.SelectedElements[0] is not SvgPath path)
            throw new InvalidOperationException("Select one path. Lines and shapes can first be converted with Path → Convert to paths.");
        controller.EnsureSelectionEditable();
        using var centerline = SvgVariableStrokeService.GetCenterline(path);

        var profile = path.CustomAttributes.TryGetValue(SvgVariableStrokeService.ProfileAttribute, out var stored)
            || path.CustomAttributes.TryGetValue("stroke-profile", out stored)
            ? StrokeProfile.Parse(stored) : new StrokeProfile();
        if (stored is null)
        {
            // The retained scene has already resolved physical and percentage SVG units.
            var resolvedWidth = controller.GetSceneNode(path)?.StrokeWidth ?? path.StrokeWidth.Value;
            foreach (var point in profile.Points)
                point.Width = resolvedWidth > 0 ? resolvedWidth : Math.Max(1, path.StrokeWidth.Value);
        }
        var points = profile.Points.Select(point => new StrokePointInfo { Offset = point.Offset, Width = point.Width }).ToList();
        var maximumWidth = Math.Max(100000, points.Where(point => double.IsFinite(point.Width))
            .Select(point => point.Width).DefaultIfEmpty(0).Max());
        var originalPaint = path.CustomAttributes.ContainsKey(SvgVariableStrokeService.CenterlineAttribute) ? path.Fill : path.Stroke;
        var paintText = PaintText(originalPaint);
        if (string.IsNullOrWhiteSpace(paintText) || paintText is "none" or "inherit")
            paintText = controller.CurrentFill is "none" or "inherit" ? "#7061dc" : controller.CurrentFill;

        var preset = new ComboBox { Header = "Profile", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var name in new[] { "Custom", "Even", "Taper both ends", "Taper start", "Taper end", "Brush" })
            preset.Items.Add(name);
        preset.SelectedIndex = 0;
        var width = NumberInput("Peak width", Math.Max(0.000001, points.Max(point => point.Width)), 0.000001, maximumWidth);
        var cap = new ComboBox { Header = "Ends", HorizontalAlignment = HorizontalAlignment.Stretch };
        cap.Items.Add("Flat");
        cap.Items.Add("Round");
        cap.Items.Add("Square");
        cap.SelectedIndex = path.CustomAttributes.TryGetValue(SvgVariableStrokeService.CapAttribute, out var savedCap)
            ? string.Equals(savedCap, nameof(SKStrokeCap.Round), StringComparison.OrdinalIgnoreCase) ? 1
                : string.Equals(savedCap, nameof(SKStrokeCap.Square), StringComparison.OrdinalIgnoreCase) ? 2 : 0
            : path.StrokeLineCap == SvgStrokeLineCap.Round ? 1 : path.StrokeLineCap == SvgStrokeLineCap.Square ? 2 : 0;
        var paint = new TextBox { Header = "Paint · color or url(#swatch)", Text = paintText };
        var rows = new StackPanel { Spacing = 8 };
        var add = new Button { Content = "Add width point", HorizontalAlignment = HorizontalAlignment.Left };
        var error = PaintErrorText();
        using var preview = new StrokeWidthPreview(centerline) { Height = 150 };
        var panel = new StackPanel { Spacing = 12, MaxWidth = DialogContentWidth(560), Width = DialogContentWidth(560) };
        panel.Children.Add(new TextBlock
        {
            Text = "Convert this path to a filled stroke outline. Reopen this dialog to adjust widths until you edit the outline’s nodes.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72
        });
        panel.Children.Add(preview);
        panel.Children.Add(PaintColumns(preset, width, cap));
        panel.Children.Add(new TextBlock
        {
            Text = "Widths use the path’s SVG units and follow its transform. Closed contours need equal first and last widths; end caps apply to open contours.",
            FontSize = 12,
            Opacity = 0.65,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(rows);
        panel.Children.Add(add);
        panel.Children.Add(paint);
        panel.Children.Add(error);
        var dialog = MakeDialog("Width profile", new ScrollViewer { Content = panel, MaxHeight = 560 }, "Apply profile");
        var updating = false;

        StrokeProfile CurrentProfile()
        {
            var result = new StrokeProfile();
            result.Points.Clear();
            foreach (var point in points.OrderBy(point => point.Offset))
                result.Points.Add(new StrokePointInfo { Offset = point.Offset, Width = point.Width });
            return result;
        }

        SvgPaintServer CurrentPaint()
        {
            var result = ParsePaint(paint.Text.Trim());
            var resolved = SvgDeferredPaintServer.TryGet<SvgPaintServer>(result, path);
            if (resolved is null || resolved == SvgPaintServer.None || resolved == SvgPaintServer.NotSet || resolved == SvgPaintServer.Inherit)
                throw new FormatException("Choose a color or the ID of an existing document paint.");
            return result;
        }

        void UpdatePreview()
        {
            if (updating)
                return;
            try
            {
                var selectedPaint = CurrentPaint();
                var color = SvgDeferredPaintServer.TryGet<SvgColourServer>(selectedPaint, path)?.Colour;
                preview.Update(CurrentProfile(), cap.SelectedIndex == 1 ? SKStrokeCap.Round : cap.SelectedIndex == 2 ? SKStrokeCap.Square : SKStrokeCap.Butt,
                    color is { } c ? new SKColor(c.R, c.G, c.B, c.A) : new SKColor(112, 97, 220));
                error.Text = string.Empty;
                dialog.IsPrimaryButtonEnabled = true;
            }
            catch (Exception exception)
            {
                error.Text = exception.Message;
                dialog.IsPrimaryButtonEnabled = false;
            }
        }

        void RebuildRows()
        {
            updating = true;
            rows.Children.Clear();
            foreach (var point in points.OrderBy(point => point.Offset))
            {
                var position = NumberInput("Position %", point.Offset * 100, 0, 100);
                var pointWidth = NumberInput("Width", point.Width, 0, maximumWidth);
                var remove = new Button
                {
                    Content = "−",
                    VerticalAlignment = VerticalAlignment.Bottom,
                    IsEnabled = points.Count > 2,
                    Width = 38,
                    Padding = new Thickness(0)
                };
                ToolTipService.SetToolTip(remove, "Remove width point");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(remove, "Remove width point");
                position.ValueChanged += (_, _) =>
                {
                    if (updating)
                        return;
                    point.Offset = position.Value / 100;
                    preset.SelectedIndex = 0;
                    UpdatePreview();
                };
                pointWidth.ValueChanged += (_, _) =>
                {
                    if (updating)
                        return;
                    point.Width = pointWidth.Value;
                    preset.SelectedIndex = 0;
                    UpdatePreview();
                };
                remove.Click += (_, _) => { points.Remove(point); preset.SelectedIndex = 0; RebuildRows(); UpdatePreview(); };
                rows.Children.Add(PaintColumns(position, pointWidth, remove));
            }
            add.IsEnabled = points.Count < 24;
            updating = false;
        }

        void SetPreset()
        {
            if (updating || preset.SelectedIndex == 0)
                return;
            var peak = width.Value;
            points.Clear();
            var values = preset.SelectedIndex switch
            {
                2 => new[] { (0d, 0d), (0.5, peak), (1d, 0d) },
                3 => new[] { (0d, 0d), (1d, peak) },
                4 => new[] { (0d, peak), (1d, 0d) },
                5 => new[] { (0d, peak * 0.15), (0.2, peak), (0.65, peak * 0.7), (1d, 0d) },
                _ => new[] { (0d, peak), (1d, peak) }
            };
            foreach (var (offset, value) in values)
                points.Add(new StrokePointInfo { Offset = offset, Width = value });
            RebuildRows();
            UpdatePreview();
        }

        preset.SelectionChanged += (_, _) => SetPreset();
        width.ValueChanged += (_, _) =>
        {
            if (updating)
                return;
            if (preset.SelectedIndex != 0)
                SetPreset();
            else
            {
                var previous = points.Max(point => point.Width);
                foreach (var point in points)
                    point.Width = previous > 0 ? point.Width * width.Value / previous : width.Value;
                RebuildRows();
                UpdatePreview();
            }
        };
        cap.SelectionChanged += (_, _) => UpdatePreview();
        paint.TextChanged += (_, _) => UpdatePreview();
        add.Click += (_, _) =>
        {
            var ordered = points.OrderBy(point => point.Offset).ToArray();
            if (ordered.Length < 2)
            {
                points.Add(new StrokePointInfo { Offset = ordered.Length == 0 || ordered[0].Offset > 0 ? 0 : 1, Width = width.Value });
            }
            else
            {
                var pair = Enumerable.Range(0, ordered.Length - 1)
                    .OrderByDescending(index => ordered[index + 1].Offset - ordered[index].Offset).First();
                points.Add(new StrokePointInfo
                {
                    Offset = (ordered[pair].Offset + ordered[pair + 1].Offset) / 2,
                    Width = (ordered[pair].Width + ordered[pair + 1].Width) / 2
                });
            }
            preset.SelectedIndex = 0;
            RebuildRows();
            UpdatePreview();
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            try
            {
                var selectedPaint = CurrentPaint();
                var selectedProfile = CurrentProfile();
                controller.Execute("Apply width profile", () =>
                {
                    controller.EnsureSelectionEditable();
                    SvgVariableStrokeService.Apply(path, selectedProfile, selectedPaint,
                        cap.SelectedIndex == 1 ? SKStrokeCap.Round : cap.SelectedIndex == 2 ? SKStrokeCap.Square : SKStrokeCap.Butt);
                });
            }
            catch (Exception exception) { args.Cancel = true; error.Text = exception.Message; }
        };
        RebuildRows();
        UpdatePreview();
        await ShowDialogAsync(dialog);
    }

    private async Task SwatchesDialogAsync()
    {
        if (Controller is not { } controller || controller.Document is not { } document)
            return;
        var brushes = new BrushService();
        brushes.LoadSwatches(document);
        // The legacy browser classifies equal stop colors as swatches. An opacity gradient is
        // still a gradient, so do not offer to flatten it through a solid-color editor.
        var swatches = brushes.Swatches.Where(entry => entry.Swatch.Stops.Count > 0
            && entry.Swatch.Stops.All(stop => Math.Abs(stop.StopOpacity - entry.Swatch.Stops[0].StopOpacity) < 0.00001f)).ToArray();
        var palette = new ComboBox { Header = "Document palette", HorizontalAlignment = HorizontalAlignment.Stretch };
        palette.Items.Add(new ComboBoxItem { Content = "＋ New swatch" });
        foreach (var entry in swatches)
        {
            var color = entry.Swatch.Stops[0].GetColor(entry.Swatch);
            var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            label.Children.Add(new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(
                    (byte)Math.Round(color.A * entry.Swatch.Stops[0].StopOpacity), color.R, color.G, color.B))
            });
            label.Children.Add(new TextBlock { Text = entry.Name, VerticalAlignment = VerticalAlignment.Center });
            palette.Items.Add(new ComboBoxItem { Content = label, Tag = entry });
        }
        palette.SelectedIndex = 0;
        var name = new TextBox { Header = "Swatch name", Text = UniqueId("swatch") };
        var spotName = new TextBox { Header = "Spot ink name · optional", PlaceholderText = "Leave empty for a process color", MaxLength = 128 };
        var hex = new TextBox { Header = "Color · #RRGGBB", Text = "#7061dc", MaxLength = 7 };
        var opacity = NumberInput("Opacity %", 100, 0, 100);
        var sample = new Border { Height = 54, CornerRadius = new CornerRadius(9), Background = BrushFromHex(hex.Text) };
        var target = new ComboBox { Header = "Apply to", HorizontalAlignment = HorizontalAlignment.Stretch };
        target.Items.Add("Palette only");
        target.Items.Add("Selection fill");
        target.Items.Add("Selection stroke");
        target.SelectedIndex = controller.SelectedElements.Count > 0 ? 1 : 0;
        var error = PaintErrorText();
        var panel = new StackPanel { Spacing = 14, MaxWidth = DialogContentWidth(520), Width = DialogContentWidth(520) };
        panel.Children.Add(new TextBlock
        {
            Text = "Save reusable colors in this SVG. Updating a shared swatch recolors every linked object.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72
        });
        panel.Children.Add(palette);
        panel.Children.Add(sample);
        panel.Children.Add(name);
        panel.Children.Add(spotName);
        panel.Children.Add(PaintColumns(hex, opacity));
        panel.Children.Add(target);
        panel.Children.Add(error);
        var dialog = MakeDialog("Document swatches", new ScrollViewer { Content = panel, MaxHeight = 560 }, "Create swatch");
        var loading = false;

        System.Drawing.Color CurrentColor()
        {
            var text = hex.Text.Trim();
            if (text.Length != 7 || text[0] != '#'
                || !uint.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
                throw new FormatException("Enter a color as #RRGGBB, for example #7061dc.");
            if (!double.IsFinite(opacity.Value) || opacity.Value < 0 || opacity.Value > 100)
                throw new FormatException("Opacity must be between 0 and 100%.");
            return System.Drawing.Color.FromArgb(255, (int)(rgb >> 16), (int)((rgb >> 8) & 255), (int)(rgb & 255));
        }

        void RefreshColor()
        {
            if (loading)
                return;
            try
            {
                var color = CurrentColor();
                sample.Background = new SolidColorBrush(Windows.UI.Color.FromArgb((byte)Math.Round(opacity.Value * 2.55), color.R, color.G, color.B));
                error.Text = string.Empty;
                dialog.IsPrimaryButtonEnabled = true;
            }
            catch (Exception exception) { error.Text = exception.Message; dialog.IsPrimaryButtonEnabled = false; }
        }

        palette.SelectionChanged += (_, _) =>
        {
            loading = true;
            if (palette.SelectedItem is ComboBoxItem { Tag: SwatchEntry entry })
            {
                var color = entry.Swatch.Stops[0].GetColor(entry.Swatch);
                name.Text = string.IsNullOrEmpty(entry.Swatch.ID) ? UniqueId("swatch") : entry.Swatch.ID;
                name.IsReadOnly = !string.IsNullOrEmpty(entry.Swatch.ID);
                spotName.Text = entry.Swatch.CustomAttributes.TryGetValue("data-unodraw-spot-name", out var ink) ? ink : string.Empty;
                hex.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                opacity.Value = entry.Swatch.Stops[0].StopOpacity * color.A / 255d * 100;
                dialog.PrimaryButtonText = "Update swatch";
            }
            else
            {
                name.Text = UniqueId("swatch");
                name.IsReadOnly = false;
                spotName.Text = string.Empty;
                dialog.PrimaryButtonText = "Create swatch";
            }
            loading = false;
            RefreshColor();
        };
        hex.TextChanged += (_, _) => RefreshColor();
        opacity.ValueChanged += (_, _) => RefreshColor();
        dialog.PrimaryButtonClick += (_, args) =>
        {
            try
            {
                var color = CurrentColor();
                var id = name.Text.Trim();
                XmlConvert.VerifyNCName(id);
                var existing = (palette.SelectedItem as ComboBoxItem)?.Tag as SwatchEntry;
                if ((document.GetElementById(id) is { } conflict && !ReferenceEquals(conflict, existing?.Swatch)) || document.ID == id)
                    throw new InvalidOperationException("That swatch name is already used in this document.");
                if (existing is not null && SvgEditorController.IsEditLocked(existing.Swatch))
                    throw new InvalidOperationException("Unlock this swatch or its parent before editing it.");
                if (target.SelectedIndex != 0)
                {
                    if (controller.SelectedElements.Count == 0)
                        throw new InvalidOperationException("Select artwork to apply the swatch, or choose Palette only.");
                    controller.EnsureSelectionEditable();
                }
                controller.Execute(existing is null ? "Create color swatch" : "Edit color swatch", () =>
                {
                    var resource = existing?.Swatch;
                    if (resource is null)
                    {
                        resource = new SvgLinearGradientServer { ID = id };
                        // Reuse the existing swatch model's canonical two-stop representation.
                        new SwatchEntry(resource, id).Color = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                        GetDefinitions().Children.Add(resource);
                    }
                    else if (string.IsNullOrEmpty(resource.ID))
                        resource.ID = id;
                    var ink = spotName.Text.Trim();
                    if (ink.Length > 0)
                        resource.CustomAttributes["data-unodraw-spot-name"] = ink;
                    else
                        resource.CustomAttributes.Remove("data-unodraw-spot-name");
                    // Keep stop IDs, metadata and other child nodes on existing resources.
                    foreach (var stop in resource.Stops)
                    {
                        stop.StopColor = new SvgColourServer(color);
                        stop.StopOpacity = (float)(opacity.Value / 100);
                    }
                    if (target.SelectedIndex != 0)
                    {
                        var linkedPaint = ParsePaint($"url(#{resource.ID})");
                        foreach (var element in controller.SelectedElements)
                            if (target.SelectedIndex == 1)
                                element.Fill = linkedPaint;
                            else
                                element.Stroke = linkedPaint;
                    }
                });
            }
            catch (Exception exception) { args.Cancel = true; error.Text = exception.Message; }
        };
        await ShowDialogAsync(dialog);
    }

    private static TextBlock PaintErrorText() => new()
    {
        TextWrapping = TextWrapping.Wrap,
        Foreground = new SolidColorBrush(Microsoft.UI.Colors.IndianRed),
        FontSize = 12
    };


    private Panel PaintColumns(params UIElement[] controls)
    {
        if ((XamlRoot?.Size.Width ?? ActualWidth) < 650)
        {
            var stack = new StackPanel { Spacing = 8 };
            foreach (var control in controls)
                stack.Children.Add(control);
            return stack;
        }
        var grid = new Grid { ColumnSpacing = 10 };
        for (var index = 0; index < controls.Length; index++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = controls[index] is Button ? GridLength.Auto : new GridLength(1, GridUnitType.Star)
            });
            Grid.SetColumn(controls[index], index);
            grid.Children.Add(controls[index]);
        }
        return grid;
    }

    private sealed class StrokeWidthPreview : SKCanvasElement, IDisposable
    {
        private readonly SKPath _centerline;
        private readonly SKPaint _fill = new() { IsAntialias = true, Color = new SKColor(112, 97, 220) };
        private readonly SKPaint _line = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, Color = new SKColor(130, 139, 160, 140), StrokeWidth = 0 };
        private SKPath? _outline;

        public StrokeWidthPreview(SKPath centerline) => _centerline = centerline;

        public void Update(StrokeProfile profile, SKStrokeCap cap, SKColor color)
        {
            var outline = SvgVariableStrokeService.CreateOutline(_centerline, profile, cap);
            _outline?.Dispose();
            _outline = outline;
            _fill.Color = color;
            Invalidate();
        }

        protected override void RenderOverride(SKCanvas canvas, Size area)
        {
            if (_outline is null || area.Width <= 28 || area.Height <= 28)
                return;
            using var restore = new SKAutoCanvasRestore(canvas);
            canvas.ClipRect(SKRect.Create((float)area.Width, (float)area.Height));
            var bounds = _outline.Bounds;
            var scale = Math.Min((area.Width - 28) / Math.Max(1, bounds.Width), (area.Height - 28) / Math.Max(1, bounds.Height));
            canvas.Translate((float)(area.Width / 2), (float)(area.Height / 2));
            canvas.Scale((float)scale);
            canvas.Translate(-bounds.MidX, -bounds.MidY);
            canvas.DrawPath(_outline, _fill);
            canvas.DrawPath(_centerline, _line);
        }

        public new void Dispose()
        {
            _outline?.Dispose();
            _fill.Dispose();
            _line.Dispose();
            base.Dispose();
        }
    }
}
