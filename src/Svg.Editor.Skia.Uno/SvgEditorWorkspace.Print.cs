using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Svg.Editor.Skia;
using Svg.Skia;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.Storage.Pickers;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private async Task PrintDialogAsync()
    {
        if (_current is not { } document || document.Controller.Document is not { } svg || document.Controller.Renderer.Picture is null)
            return;
        var controller = document.Controller;
        var frameTime = controller.Renderer.AnimationTime;
        using var picture = SvgRenderSnapshotService.Capture(controller.Renderer);
        SvgPrintSettings initial;
        try
        { initial = SvgPrintSettings.FromDocument(svg); }
        catch (InvalidOperationException) { initial = new SvgPrintSettings(); }
        var width = NumberInput("Trim width · mm", initial.WidthMillimeters, 1, 5000);
        var height = NumberInput("Trim height · mm", initial.HeightMillimeters, 1, 5000);
        var margin = NumberInput("Inner margin · mm", initial.MarginMillimeters, 0, 1000);
        var bleed = NumberInput("Bleed · mm", initial.BleedMillimeters, 0, 100);
        var overlap = NumberInput("Tile overlap · mm", initial.TileOverlapMillimeters, 0, 1000);
        var scale = NumberInput("Scale · %", initial.Scale * 100, 0.01, 10000);
        var dpi = NumberInput("Raster resolution · dpi", initial.Dpi, 36, 2400);
        var ink = NumberInput("Generic ink limit · %", initial.TotalInkLimit * 100, 100, 400);
        var black = NumberInput("Generic black generation · %", initial.BlackGeneration * 100, 0, 100);
        var layout = new ComboBox { Header = "Placement", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var item in new[] { "Fit to page", "Actual size / custom scale", "Tile across pages" })
            layout.Items.Add(item);
        layout.SelectedIndex = (int)initial.Layout;
        var color = new ComboBox { Header = "PDF color and content", HorizontalAlignment = HorizontalAlignment.Stretch };
        color.Items.Add("RGB · retain vectors and text");
        color.Items.Add("CMYK · flatten at selected resolution");
        color.SelectedIndex = (int)initial.ColorMode;
        var namedInks = svg.Descendants().Where(element => element.CustomAttributes.ContainsKey(SvgSpotSeparationService.SpotNameAttribute))
            .Select(element => element.CustomAttributes[SvgSpotSeparationService.SpotNameAttribute]).Distinct(StringComparer.Ordinal).ToArray();
        var convertSpots = new CheckBox
        {
            Content = "Convert named inks to process colors in PDF",
            Visibility = namedInks.Length == 0 ? Visibility.Collapsed : Visibility.Visible
        };
        var crop = new CheckBox { Content = "Crop marks", IsChecked = initial.CropMarks };
        var registration = new CheckBox { Content = "Registration marks", IsChecked = initial.RegistrationMarks };
        var preset = new ComboBox { Header = "Trim preset", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var item in new[] { "Custom", "A4 · 210 × 297 mm", "A3 · 297 × 420 mm", "Letter · 215.9 × 279.4 mm", "A5 · 148 × 210 mm" })
            preset.Items.Add(item);
        preset.SelectedIndex = 0;
        var profileButton = new Button { Content = "Load press ICC profile…", HorizontalAlignment = HorizontalAlignment.Stretch };
        var clearProfile = new Button { Content = "Clear profile" };
        var profileLabel = new TextBlock { Text = "Generic CMYK · no press profile selected", TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        var settingsPanel = new StackPanel { Spacing = 12 };
        settingsPanel.Children.Add(preset);
        settingsPanel.Children.Add(PaintColumns(width, height));
        settingsPanel.Children.Add(PaintColumns(margin, bleed));
        settingsPanel.Children.Add(layout);
        settingsPanel.Children.Add(PaintColumns(scale, overlap));
        settingsPanel.Children.Add(PaintColumns(crop, registration));
        settingsPanel.Children.Add(color);
        settingsPanel.Children.Add(convertSpots);
        if (namedInks.Length > 0)
            settingsPanel.Children.Add(new TextBlock { Text = "Named inks: " + string.Join(", ", namedInks) + ". Export separations to retain these ink plates.", TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        settingsPanel.Children.Add(dpi);
        settingsPanel.Children.Add(PaintColumns(black, ink));
        settingsPanel.Children.Add(PaintColumns(profileButton, clearProfile));
        settingsPanel.Children.Add(profileLabel);
        var pageChoice = new ComboBox { Header = "Page preview", HorizontalAlignment = HorizontalAlignment.Stretch };
        using var preview = new PrintPreviewCanvas { Height = 290, HorizontalAlignment = HorizontalAlignment.Stretch };
        var summary = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        var issues = new ListView { MaxHeight = 190, SelectionMode = ListViewSelectionMode.Single };
        var preflight = new Button { Content = "Run preflight", HorizontalAlignment = HorizontalAlignment.Stretch };
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Microsoft.UI.Colors.IndianRed) };
        var reviewPanel = new StackPanel { Spacing = 10 };
        reviewPanel.Children.Add(pageChoice);
        reviewPanel.Children.Add(preview);
        reviewPanel.Children.Add(summary);
        reviewPanel.Children.Add(preflight);
        reviewPanel.Children.Add(issues);
        reviewPanel.Children.Add(error);
        var narrow = XamlRoot is { } root && root.Size.Width < 850;
        var content = new Grid { ColumnSpacing = 24, RowSpacing = 16, Width = DialogContentWidth(narrow ? 360 : 720) };
        if (narrow)
        {
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(reviewPanel, 1);
        }
        else
        {
            content.ColumnDefinitions.Add(new ColumnDefinition());
            content.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(reviewPanel, 1);
        }
        content.Children.Add(settingsPanel);
        content.Children.Add(reviewPanel);
        var dialog = MakeDialog("Print studio", new ScrollViewer { Content = content, MaxHeight = 620 }, "Export PDF", "Close");
        dialog.Resources["ContentDialogMaxWidth"] = 900d;
        dialog.SecondaryButtonText = "Export separations";
        SvgIccCmykProfile? profile = null;
        IReadOnlyList<SvgPrintPage> pages = Array.Empty<SvgPrintPage>();
        var updating = false;
        void ShowIssues(IEnumerable<string> messages) => issues.ItemsSource = messages.Select(message => new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = Math.Max(180, reviewPanel.ActualWidth - 48)
        }).ToArray();
        SvgPrintSettings ReadSettings() => new()
        {
            WidthMillimeters = width.Value,
            HeightMillimeters = height.Value,
            MarginMillimeters = margin.Value,
            BleedMillimeters = bleed.Value,
            TileOverlapMillimeters = overlap.Value,
            Scale = scale.Value / 100,
            Dpi = dpi.Value,
            CropMarks = crop.IsChecked == true,
            RegistrationMarks = registration.IsChecked == true,
            Layout = (SvgPrintLayout)Math.Max(0, layout.SelectedIndex),
            ColorMode = (SvgPrintColorMode)Math.Max(0, color.SelectedIndex),
            TotalInkLimit = ink.Value / 100,
            BlackGeneration = black.Value / 100
        };
        void Update()
        {
            if (updating)
                return;
            updating = true;
            try
            {
                var settings = ReadSettings();
                pages = SvgPrintLayoutService.CreatePages(picture.CullRect, settings);
                var selected = Math.Clamp(pageChoice.SelectedIndex, 0, pages.Count - 1);
                pageChoice.ItemsSource = pages.Select(page => $"Page {page.Number} · column {page.Column + 1}, row {page.Row + 1}").ToArray();
                pageChoice.SelectedIndex = selected;
                preview.Update(picture, pages[selected], settings);
                summary.Text = $"{pages.Count} page(s) · trim {settings.WidthMillimeters:0.##} × {settings.HeightMillimeters:0.##} mm\n" +
                    $"Media including bleed and marks: {pages[0].MediaSize.Width / SvgPrintLayoutService.PointsPerMillimeter:0.##} × {pages[0].MediaSize.Height / SvgPrintLayoutService.PointsPerMillimeter:0.##} mm";
                error.Text = string.Empty;
                dialog.IsPrimaryButtonEnabled = dialog.IsSecondaryButtonEnabled = true;
            }
            catch (Exception exception)
            {
                error.Text = exception.Message;
                dialog.IsPrimaryButtonEnabled = dialog.IsSecondaryButtonEnabled = false;
            }
            finally { updating = false; }
        }
        foreach (var number in new[] { width, height, margin, bleed, overlap, scale, dpi, ink, black })
            number.ValueChanged += (_, _) => Update();
        layout.SelectionChanged += (_, _) => Update();
        color.SelectionChanged += (_, _) => Update();
        crop.Checked += (_, _) => Update();
        crop.Unchecked += (_, _) => Update();
        registration.Checked += (_, _) => Update();
        registration.Unchecked += (_, _) => Update();
        pageChoice.SelectionChanged += (_, _) =>
        {
            if (!updating && pageChoice.SelectedIndex >= 0 && pageChoice.SelectedIndex < pages.Count)
                preview.Update(picture, pages[pageChoice.SelectedIndex], ReadSettings());
        };
        preset.SelectionChanged += (_, _) =>
        {
            var size = preset.SelectedIndex switch { 1 => (210d, 297d), 2 => (297d, 420d), 3 => (215.9, 279.4), 4 => (148d, 210d), _ => (width.Value, height.Value) };
            width.Value = size.Item1;
            height.Value = size.Item2;
        };
        profileButton.Click += async (_, _) =>
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add(".icc");
                picker.FileTypeFilter.Add(".icm");
                InitializePicker(picker);
                if (await picker.PickSingleFileAsync() is not { } file)
                    return;
                using var stream = await file.OpenStreamForReadAsync();
                if (stream.Length > 32 * 1024 * 1024)
                    throw new InvalidOperationException("The ICC profile exceeds 32 MiB.");
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory);
                profile = new SvgIccCmykProfile(memory.ToArray());
                profileLabel.Text = profile.Description + " · " + profile.TransformTag;
                black.IsEnabled = ink.IsEnabled = false;
                color.SelectedIndex = 1;
                Update();
            }
            catch (Exception exception) { error.Text = exception.Message; }
        };
        clearProfile.Click += (_, _) => { profile = null; profileLabel.Text = "Generic CMYK · no press profile selected"; black.IsEnabled = ink.IsEnabled = true; };
        preflight.Click += (_, _) =>
        {
            try
            {
                var results = SvgPreflightService.Inspect(controller, ReadSettings(), profile is not null);
                ShowIssues(results.Count == 0 ? new[] { "Preflight passed · no issues detected" } : results.Select(issue => issue.ToString()));
            }
            catch (Exception exception) { error.Text = exception.Message; }
        };
        void Validate(ContentDialogButtonClickEventArgs args, bool separations)
        {
            try
            {
                ReadSettings().Validate();
                if (namedInks.Length > 0)
                {
                    if (!separations && convertSpots.IsChecked != true)
                        throw new InvalidOperationException("Choose process-color conversion for this PDF, or export separations to retain named inks.");
                    if (separations)
                    {
                        var analysis = SvgSpotSeparationService.Analyze(svg);
                        if (!analysis.CanSeparate)
                        {
                            ShowIssues(analysis.Issues.Select(issue => issue.ToString()));
                            throw new InvalidOperationException("Resolve the named-ink separation issues listed in preflight before exporting plates.");
                        }
                    }
                }
            }
            catch (Exception exception) { args.Cancel = true; error.Text = exception.Message; }
        }
        dialog.PrimaryButtonClick += (_, args) => Validate(args, false);
        dialog.SecondaryButtonClick += (_, args) => Validate(args, true);
        Update();
        var result = await ShowDialogAsync(dialog);
        preview.Clear();
        if (result == ContentDialogResult.None)
            return;
        var settings = ReadSettings();
        settings.Apply(controller);
        // Clone the native DOM before asynchronous I/O. This preserves typed paints
        // and resource context, and prevents later edits from changing the print job.
        var source = (SvgDocument)controller.Document!.DeepCopy<SvgDocument>();
        source.BaseUri = controller.Document.BaseUri;
        var plates = result == ContentDialogResult.Secondary;
        var save = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(document.Name) + (plates ? "-plates" : "-print"), DefaultFileExtension = plates ? ".zip" : ".pdf" };
        save.FileTypeChoices.Add(plates ? "Ink separation package" : "Print PDF", new[] { plates ? ".zip" : ".pdf" });
        InitializePicker(save);
        if (await save.PickSaveFileAsync() is not { } destination)
            return;
        StatusText.Text = plates ? "Preparing ink plates…" : "Preparing print PDF…";
        var bytes = await Task.Run(() =>
        {
            using var renderer = new SKSvg();
            renderer.FromSvgDocument(source);
            renderer.SetAnimationTime(frameTime);
            using var output = new MemoryStream();
            using var artwork = SvgRenderSnapshotService.Capture(renderer);
            if (plates && namedInks.Length > 0)
                SvgPrintService.ExportSpotSeparations(renderer.SourceDocument!, output, settings, profile);
            else if (plates)
                SvgPrintService.ExportSeparations(artwork, output, settings, profile, source.GetXML());
            else
                SvgPrintService.ExportPdf(artwork, output, settings, profile);
            return output.ToArray();
        });
        await WriteFileAsync(destination, bytes);
        StatusText.Text = $"Exported {pages.Count} print page(s) to {destination.Name}";
    }

    private async Task PreflightDialogAsync()
    {
        if (Controller is not { Document: { } document } controller)
            return;
        var issues = SvgPreflightService.Inspect(controller, SvgPrintSettings.FromDocument(document));
        var panel = new StackPanel { Spacing = 12, Width = DialogContentWidth(380) };
        panel.Children.Add(new TextBlock { Text = issues.Count == 0 ? "No issues detected." : $"{issues.Count(issue => issue.Severity == SvgPreflightSeverity.Error)} errors · {issues.Count(issue => issue.Severity == SvgPreflightSeverity.Warning)} warnings", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        foreach (var issue in issues)
        {
            var button = new Button { Content = new TextBlock { Text = issue.ToString(), TextWrapping = TextWrapping.Wrap }, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
            button.Click += (_, _) =>
            {
                if (issue.ElementId is { } id && document.GetElementById(id) is SvgVisualElement element)
                { controller.Select(element, allowLocked: true); Canvas.FitSelection(); }
            };
            panel.Children.Add(button);
        }
        await ShowDialogAsync(MakeDialog("Document preflight", new ScrollViewer { Content = panel, MaxHeight = 520 }, string.Empty, "Close"));
    }

    private sealed class PrintPreviewCanvas : SKCanvasElement, IDisposable
    {
        private SKPicture? _picture;
        private SvgPrintPage? _page;
        private SvgPrintSettings? _settings;
        public void Update(SKPicture picture, SvgPrintPage page, SvgPrintSettings settings)
        { _picture = picture; _page = page; _settings = settings; Invalidate(); }
        public void Clear() { _picture = null; _page = null; _settings = null; }
        protected override void RenderOverride(SKCanvas canvas, Size area)
        {
            if (_picture is null || _page is null || _settings is null || area.Width <= 16 || area.Height <= 16)
                return;
            using var restore = new SKAutoCanvasRestore(canvas);
            canvas.ClipRect(SKRect.Create((float)area.Width, (float)area.Height));
            canvas.Clear(new SKColor(47, 48, 55));
            var scale = (float)Math.Min((area.Width - 16) / _page.MediaSize.Width, (area.Height - 16) / _page.MediaSize.Height);
            canvas.Translate(((float)area.Width - _page.MediaSize.Width * scale) / 2, ((float)area.Height - _page.MediaSize.Height * scale) / 2);
            canvas.Scale(scale);
            canvas.ClipRect(SKRect.Create(_page.MediaSize));
            SvgPrintService.DrawPage(canvas, _picture, _page, _settings);
            using var trim = new SKPaint { Color = new SKColor(112, 97, 220, 180), Style = SKPaintStyle.Stroke, StrokeWidth = 1 / scale };
            canvas.DrawRect(_page.TrimBox, trim);
        }
        public new void Dispose() { Clear(); base.Dispose(); }
    }
}
