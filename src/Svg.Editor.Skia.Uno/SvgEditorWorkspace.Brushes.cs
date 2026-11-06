using System.Globalization;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using Svg.Editor.Skia;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private async Task BrushLibraryDialogAsync()
    {
        if (Controller is not { } controller || controller.Document is not { } document)
            return;
        var library = new ComboBox { Header = "Document brushes", HorizontalAlignment = HorizontalAlignment.Stretch };
        var id = new TextBox { Header = "Resource ID", Text = UniqueId("brush") };
        var name = new TextBox { Header = "Brush name", Text = "New brush", MaxLength = 128 };
        var kind = new ComboBox { Header = "Brush type", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var title in new[] { "Width profile", "Repeating pattern", "Scatter" })
            kind.Items.Add(title);
        kind.SelectedIndex = 0;
        var width = NumberInput("Width · SVG units", 24, 0.0001, 1000000);
        var spacing = NumberInput("Spacing · width multiplier", 1.25, 0.02, 100);
        var rotation = NumberInput("Rotation · degrees", 0, -360000, 360000);
        var scatter = NumberInput("Scatter · width multiplier", 0.5, 0, 10);
        var seed = NumberInput("Random seed", 1, int.MinValue, int.MaxValue);
        var follow = new CheckBox { Content = "Follow the path direction", IsChecked = true };
        var cap = new ComboBox { Header = "Ends", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var title in new[] { "Flat", "Round", "Square" })
            cap.Items.Add(title);
        cap.SelectedIndex = 1;
        var color = new TextBox { Header = "Color · #RRGGBB", Text = "#7061DC", MaxLength = 7 };
        var opacity = NumberInput("Opacity %", 100, 0, 100);
        var profile = new TextBox { Header = "Width profile · position,multiplier", Text = "0,0.1;0.2,1;0.7,0.65;1,0.1", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 16000 };
        var stamp = new TextBox { Header = "Vector stamp · SVG path data", Text = new SvgBrushDefinition().StampPath, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 65536, MaxHeight = 120 };
        var stampFromSelection = new Button { Content = "Use selected path as stamp", IsEnabled = controller.SelectedElements.Count == 1 && controller.SelectedElements[0] is SvgPath };
        var copy = new Button { Content = "Duplicate" };
        var remove = new Button { Content = "Remove" };
        var import = new Button { Content = "Import library…" };
        var export = new Button { Content = "Export library…" };
        var apply = new CheckBox { Content = "Apply to selected paths", IsChecked = controller.SelectedElements.Count > 0 && controller.SelectedElements.All(element => element is SvgPath) };
        var updateLinked = new CheckBox { Content = "Update artwork linked to this brush", IsChecked = true };
        var error = PaintErrorText();
        using var sample = controller.SelectedElements.Count == 1 && controller.SelectedElements[0] is SvgPath selectedPath
            ? SvgVariableStrokeService.GetCenterline(selectedPath)
            : SKPath.ParseSvgPathData("M15 75 C80 -10 160 160 245 50 C285 0 340 35 390 65");
        using var preview = new BrushOutlinePreview(sample) { Height = 170 };
        var panel = new StackPanel { Spacing = 12, MaxWidth = DialogContentWidth(620), Width = DialogContentWidth(620) };
        panel.Children.Add(new TextBlock { Text = "Author reusable vector brushes in this SVG. Save a library to share presets. Artwork remains ordinary editable SVG paths with its original centerline retained.", TextWrapping = TextWrapping.Wrap, Opacity = 0.72 });
        panel.Children.Add(PaintColumns(library, copy, remove));
        panel.Children.Add(preview);
        panel.Children.Add(PaintColumns(name, id));
        panel.Children.Add(PaintColumns(kind, width, cap));
        panel.Children.Add(PaintColumns(color, opacity));
        panel.Children.Add(profile);
        panel.Children.Add(stamp);
        panel.Children.Add(stampFromSelection);
        panel.Children.Add(PaintColumns(spacing, rotation));
        panel.Children.Add(PaintColumns(scatter, seed));
        panel.Children.Add(follow);
        panel.Children.Add(apply);
        panel.Children.Add(updateLinked);
        panel.Children.Add(PaintColumns(import, export));
        panel.Children.Add(error);
        var dialog = MakeDialog("Brush library", new ScrollViewer { Content = panel, MaxHeight = 650 }, "Save brush");
        var loading = false;
        IReadOnlyList<SvgBrushDefinition> brushes = Array.Empty<SvgBrushDefinition>();

        SvgBrushDefinition Current()
        {
            if (color.Text.Length != 7 || color.Text[0] != '#' || !uint.TryParse(color.Text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
                throw new FormatException("Enter the brush color as #RRGGBB.");
            if (!double.IsFinite(opacity.Value) || opacity.Value is < 0 or > 100 || !double.IsFinite(seed.Value) || seed.Value != Math.Floor(seed.Value))
                throw new FormatException("Opacity needs 0–100% and the random seed must be a whole number.");
            return new SvgBrushDefinition
            {
                Id = id.Text.Trim(),
                Name = name.Text.Trim(),
                Kind = (SvgBrushKind)kind.SelectedIndex,
                Width = width.Value,
                Spacing = spacing.Value,
                Rotation = rotation.Value,
                Scatter = scatter.Value,
                Seed = (int)seed.Value,
                FollowTangent = follow.IsChecked == true,
                Cap = cap.SelectedIndex == 1 ? SKStrokeCap.Round : cap.SelectedIndex == 2 ? SKStrokeCap.Square : SKStrokeCap.Butt,
                Color = new SKColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, (byte)Math.Round(opacity.Value * 2.55)),
                Profile = SvgBrushLibraryService.ParseProfile(profile.Text),
                StampPath = stamp.Text.Trim()
            };
        }
        void Refresh()
        {
            if (loading)
                return;
            var widthMode = kind.SelectedIndex == 0;
            profile.Visibility = widthMode ? Visibility.Visible : Visibility.Collapsed;
            cap.IsEnabled = widthMode;
            stamp.Visibility = stampFromSelection.Visibility = widthMode ? Visibility.Collapsed : Visibility.Visible;
            spacing.IsEnabled = rotation.IsEnabled = follow.IsEnabled = !widthMode;
            scatter.IsEnabled = seed.IsEnabled = kind.SelectedIndex == 2;
            try
            {
                preview.Update(Current());
                error.Text = string.Empty;
                dialog.IsPrimaryButtonEnabled = true;
            }
            catch (Exception exception) { error.Text = exception.Message; dialog.IsPrimaryButtonEnabled = false; }
        }
        void LoadDefinition(SvgBrushDefinition definition, bool existing)
        {
            loading = true;
            id.Text = definition.Id;
            id.IsReadOnly = existing;
            name.Text = definition.Name;
            kind.SelectedIndex = (int)definition.Kind;
            width.Value = definition.Width;
            spacing.Value = definition.Spacing;
            rotation.Value = definition.Rotation;
            scatter.Value = definition.Scatter;
            seed.Value = definition.Seed;
            follow.IsChecked = definition.FollowTangent;
            cap.SelectedIndex = definition.Cap == SKStrokeCap.Round ? 1 : definition.Cap == SKStrokeCap.Square ? 2 : 0;
            color.Text = $"#{definition.Color.Red:X2}{definition.Color.Green:X2}{definition.Color.Blue:X2}";
            opacity.Value = definition.Color.Alpha / 255d * 100;
            profile.Text = definition.Profile.ToString();
            stamp.Text = definition.StampPath;
            remove.IsEnabled = existing;
            loading = false;
            Refresh();
        }
        void ReloadLibrary(string? select = null)
        {
            loading = true;
            brushes = SvgBrushLibraryService.Load(document);
            library.Items.Clear();
            library.Items.Add("＋ New brush");
            foreach (var brush in brushes)
                library.Items.Add(brush);
            var selected = brushes.Select((brush, index) => (brush, index)).FirstOrDefault(pair => pair.brush.Id == select);
            library.SelectedIndex = selected.brush is null ? 0 : selected.index + 1;
            loading = false;
            if (library.SelectedIndex > 0)
                LoadDefinition(brushes[library.SelectedIndex - 1], true);
            else
                LoadDefinition(new SvgBrushDefinition { Id = UniqueId("brush") }, false);
        }
        library.SelectionChanged += (_, _) =>
        {
            if (loading)
                return;
            if (library.SelectedIndex > 0)
                LoadDefinition(brushes[library.SelectedIndex - 1], true);
            else
                LoadDefinition(new SvgBrushDefinition { Id = UniqueId("brush") }, false);
        };
        copy.Click += (_, _) =>
        {
            try
            {
                var definition = Current();
                definition.Id = UniqueId("brush");
                definition.Name += " copy";
                loading = true;
                library.SelectedIndex = 0;
                loading = false;
                LoadDefinition(definition, false);
            }
            catch (Exception exception) { error.Text = exception.Message; }
        };
        remove.Click += (_, _) =>
        {
            try
            {
                controller.Execute("Remove brush definition", () => SvgBrushLibraryService.Remove(document, id.Text));
                ReloadLibrary();
            }
            catch (Exception exception) { error.Text = exception.Message; }
        };
        stampFromSelection.Click += (_, _) =>
        {
            if (controller.SelectedElements.Count == 1 && controller.SelectedElements[0] is SvgPath path)
                stamp.Text = path.PathData?.ToString() ?? string.Empty;
        };
        import.Click += async (_, _) =>
        {
            try
            {
                var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
                picker.FileTypeFilter.Add(".unodraw-brushes");
                picker.FileTypeFilter.Add(".xml");
                InitializePicker(picker);
                var file = await picker.PickSingleFileAsync();
                if (file is null)
                    return;
                var definitions = SvgBrushLibraryService.Import(await FileIO.ReadTextAsync(file));
                controller.Execute("Import brush library", () =>
                {
                    foreach (var definition in definitions)
                    {
                        if (document.GetElementById(definition.Id) is not null || document.ID == definition.Id)
                            definition.Id = UniqueId(definition.Id);
                        SvgBrushLibraryService.Save(document, definition, false);
                    }
                });
                ReloadLibrary(definitions.LastOrDefault()?.Id);
            }
            catch (Exception exception) { error.Text = exception.Message; }
        };
        export.Click += async (_, _) =>
        {
            try
            {
                var xml = SvgBrushLibraryService.Export(SvgBrushLibraryService.Load(document));
                var picker = new FileSavePicker { SuggestedFileName = "UnoDraw brushes", DefaultFileExtension = ".unodraw-brushes", SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
                picker.FileTypeChoices.Add("UnoDraw brush library", new List<string> { ".unodraw-brushes" });
                InitializePicker(picker);
                var file = await picker.PickSaveFileAsync();
                if (file is null)
                    return;
                await WriteFileAsync(file, Encoding.UTF8.GetBytes(xml));
                StatusText.Text = "Saved the document brush library";
            }
            catch (Exception exception) { error.Text = exception.Message; }
        };
        foreach (var number in new[] { width, spacing, rotation, scatter, seed, opacity })
            number.ValueChanged += (_, _) => Refresh();
        foreach (var text in new[] { id, name, color, profile, stamp })
            text.TextChanged += (_, _) => Refresh();
        kind.SelectionChanged += (_, _) => Refresh();
        cap.SelectionChanged += (_, _) => Refresh();
        follow.Checked += (_, _) => Refresh();
        follow.Unchecked += (_, _) => Refresh();
        dialog.PrimaryButtonClick += (_, args) =>
        {
            try
            {
                var definition = Current();
                var paths = controller.SelectedElements.OfType<SvgPath>().ToArray();
                if (apply.IsChecked == true)
                {
                    if (paths.Length == 0 || paths.Length != controller.SelectedElements.Count)
                        throw new InvalidOperationException("Select paths to apply this brush. Convert shapes with Path → Convert to paths first.");
                    controller.EnsureSelectionEditable();
                }
                controller.Execute("Save vector brush", () =>
                {
                    SvgBrushLibraryService.Save(document, definition, updateLinked.IsChecked == true);
                    if (apply.IsChecked == true)
                        foreach (var path in paths)
                            SvgBrushLibraryService.Apply(path, definition);
                });
            }
            catch (Exception exception) { args.Cancel = true; error.Text = exception.Message; }
        };
        var initialId = controller.SelectedElements.Count == 1 ? SvgBrushLibraryService.GetBrushId(controller.SelectedElements[0]) : null;
        ReloadLibrary(initialId);
        if (library.SelectedIndex == 0 && controller.SelectedElements.Count == 1 && controller.SelectedElements[0] is SvgPath appliedPath
            && SvgBrushLibraryService.GetAppliedBrush(appliedPath) is { } detached)
        {
            if (document.GetElementById(detached.Id) is not null)
                detached.Id = UniqueId(detached.Id);
            LoadDefinition(detached, false);
        }
        await ShowDialogAsync(dialog);
    }

    private sealed class BrushOutlinePreview : SKCanvasElement, IDisposable
    {
        private readonly SKPath _centerline;
        private SKPath? _outline;
        private SKColor _color;
        public BrushOutlinePreview(SKPath centerline) => _centerline = centerline;
        public void Update(SvgBrushDefinition definition)
        {
            var outline = SvgBrushLibraryService.CreateOutline(_centerline, definition);
            _outline?.Dispose();
            _outline = outline;
            _color = definition.Color;
            Invalidate();
        }
        protected override void RenderOverride(SKCanvas canvas, Size area)
        {
            if (_outline is null || area.Width <= 28 || area.Height <= 28)
                return;
            var bounds = _outline.Bounds;
            var scale = Math.Min((area.Width - 28) / Math.Max(1, bounds.Width), (area.Height - 28) / Math.Max(1, bounds.Height));
            using var restore = new SKAutoCanvasRestore(canvas);
            canvas.ClipRect(SKRect.Create((float)area.Width, (float)area.Height));
            canvas.Translate((float)area.Width / 2, (float)area.Height / 2);
            canvas.Scale((float)scale);
            canvas.Translate(-bounds.MidX, -bounds.MidY);
            using var fill = new SKPaint { IsAntialias = true, Color = _color };
            canvas.DrawPath(_outline, fill);
            fill.Style = SKPaintStyle.Stroke;
            fill.StrokeWidth = 0;
            fill.Color = new SKColor(130, 139, 160, 110);
            canvas.DrawPath(_centerline, fill);
        }
        public new void Dispose() { _outline?.Dispose(); base.Dispose(); }
    }
}
