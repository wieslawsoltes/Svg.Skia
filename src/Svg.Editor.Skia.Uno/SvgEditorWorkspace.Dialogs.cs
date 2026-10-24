using System.ComponentModel;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Svg.Editor.Core;
using Svg.Editor.Skia;
using Svg.Editor.Svg;
using Svg.Pathing;
using Svg.Transforms;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private static readonly XNamespace SvgNamespace = "http://www.w3.org/2000/svg";

    private double DialogContentWidth(double preferred) => Math.Max(220, Math.Min(preferred, (XamlRoot?.Size.Width ?? ActualWidth) - 80));

    private ContentDialog MakeDialog(string title, object content, string primary = "Apply", string close = "Cancel")
    {
        if (content is FrameworkElement element)
            element.MaxWidth = Math.Min(element.MaxWidth, DialogContentWidth(double.MaxValue));
        var scroll = content as ScrollViewer ?? new ScrollViewer { Content = content };
        scroll.MaxHeight = Math.Min(scroll.MaxHeight, Math.Max(160, (XamlRoot?.Size.Height ?? ActualHeight) - 200));
        scroll.VerticalScrollMode = ScrollMode.Enabled;
        scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        scroll.HorizontalScrollMode = ScrollMode.Disabled;
        scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = scroll,
            PrimaryButtonText = primary,
            CloseButtonText = close,
            DefaultButton = ContentDialogButton.Primary,
            RequestedTheme = ActualTheme
        };
        // The default Fluent dialog cap is narrower than the mesh/brush editors.
        // Allow their requested width while retaining a margin on small screens.
        var maximumWidth = Math.Max(260, Math.Min(1120, (XamlRoot?.Size.Width ?? ActualWidth) - 32));
        dialog.Resources["ContentDialogMaxWidth"] = maximumWidth;
        dialog.Resources["ContentDialogMinWidth"] = Math.Min(320, maximumWidth);
        // Keep the content viewport inside the dialog instead of letting the
        // default height cap clip controls that the ScrollViewer thinks fit.
        dialog.Resources["ContentDialogMaxHeight"] = Math.Max(240, (XamlRoot?.Size.Height ?? ActualHeight) - 32);
        return dialog;
    }

    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        if (_dialogVisible)
            return ContentDialogResult.None;
        _dialogVisible = true;
        try
        { return await dialog.ShowAsync(); }
        finally
        {
            _dialogVisible = false;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!_disposed && !_dialogVisible)
                    Focus(FocusState.Programmatic);
            });
        }
    }

    private async Task ShowErrorAsync(string message)
    {
        StatusText.Text = message;
        if (_dialogVisible || XamlRoot is null)
            return;
        var dialog = MakeDialog("The change could not be completed", new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 }, string.Empty, "Close");
        await ShowDialogAsync(dialog);
    }

    private async Task ShowCommandsAsync()
    {
        var search = new TextBox { PlaceholderText = "Type a command, tool or shortcut…" };
        var list = new ListView { ItemsSource = _commands, MaxHeight = 400, Width = DialogContentWidth(480), SelectionMode = ListViewSelectionMode.Single };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(search);
        content.Children.Add(list);
        var dialog = MakeDialog("Command search", content, "Run");
        list.SelectedIndex = _commands.Count > 0 ? 0 : -1;
        search.TextChanged += (_, _) =>
        {
            var matches = _commands.Where(command => command.ToString().Contains(search.Text, StringComparison.OrdinalIgnoreCase)).ToList();
            list.ItemsSource = matches;
            list.SelectedIndex = matches.Count > 0 ? 0 : -1;
            dialog.IsPrimaryButtonEnabled = matches.Count > 0;
        };
        dialog.Opened += (_, _) => search.Focus(FocusState.Programmatic);
        list.IsItemClickEnabled = true;
        EditorCommand? chosen = null;
        search.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter && list.SelectedItem is EditorCommand command)
            {
                chosen = command;
                args.Handled = true;
                dialog.Hide();
            }
            else if (args.Key is Windows.System.VirtualKey.Down or Windows.System.VirtualKey.Up && list.Items.Count > 0)
            {
                var direction = args.Key == Windows.System.VirtualKey.Down ? 1 : -1;
                list.SelectedIndex = Math.Clamp(list.SelectedIndex + direction, 0, list.Items.Count - 1);
                list.ScrollIntoView(list.SelectedItem);
                args.Handled = true;
            }
        };
        list.ItemClick += (_, args) => { chosen = args.ClickedItem as EditorCommand; dialog.Hide(); };
        var result = await ShowDialogAsync(dialog);
        chosen ??= result == ContentDialogResult.Primary ? list.SelectedItem as EditorCommand : null;
        if (chosen is not null)
            await RunCommandAsync(chosen.Id);
    }

    private async Task ShowHelpAsync()
    {
        var text = "TOOLS\nV  Select    A  Nodes    R  Rectangle    E  Ellipse\nP  Bézier pen    B  Pencil    T  Text    L  Line\nG  Polygon    O  Polyline    C  Circle    Q  Quadratic\n\nCANVAS\nHold Space or use the middle button to pan.\nThe mouse wheel zooms around the pointer.\nShift+1 fits the artboard; Shift+2 fits the selection.\nEsc cancels a gesture; Enter finishes a path.\nShift extends selection and constrains shape proportions.\nArrow keys nudge 1 unit; Shift+arrows nudge 10.\n\nDOCUMENT\nCtrl+N new    Ctrl+O open    Ctrl+S save\nCtrl+Shift+S save as    Ctrl+E export\nCtrl+Z undo    Ctrl+Shift+Z redo\nCtrl+C / X / V copy / cut / paste    Ctrl+D duplicate\nCtrl+G group    Ctrl+Shift+G ungroup\nCtrl+K command search    Tab focus canvas";
        await ShowDialogAsync(MakeDialog("Your workspace shortcuts", new ScrollViewer
        {
            MaxHeight = 500,
            Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13, LineHeight = 21 }
        }, string.Empty, "Done"));
    }

    private async Task NewDocumentDialogAsync()
    {
        var width = NumberInput("Width", 1200, 1, 1000000);
        var height = NumberInput("Height", 800, 1, 1000000);
        var content = new StackPanel { Spacing = 16, Width = DialogContentWidth(320) };
        var presets = new ComboBox { Header = "Preset", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var name in new[] { "Landscape · 1200 × 800", "Square · 1080 × 1080", "Portrait · 800 × 1200", "HD · 1920 × 1080", "A4 · 794 × 1123" })
            presets.Items.Add(name);
        presets.SelectedIndex = 0;
        presets.SelectionChanged += (_, _) =>
        {
            var size = presets.SelectedIndex switch { 1 => (1080, 1080), 2 => (800, 1200), 3 => (1920, 1080), 4 => (794, 1123), _ => (1200, 800) };
            width.Value = size.Item1;
            height.Value = size.Item2;
        };
        content.Children.Add(presets);
        content.Children.Add(width);
        content.Children.Add(height);
        if (await ShowDialogAsync(MakeDialog("A new idea starts here", content, "Create document")) == ContentDialogResult.Primary)
            NewDocument((float)width.Value, (float)height.Value);
    }

    private async Task ArtboardDialogAsync()
    {
        if (Controller?.Document is not { } document)
            return;
        var originalWidth = document.Width.Value;
        var originalHeight = document.Height.Value;
        var width = NumberInput("Width", originalWidth, 1, 1000000);
        var height = NumberInput("Height", originalHeight, 1, 1000000);
        var grid = NumberInput("Grid spacing", Controller.Session.Settings.GridSize, 0.1, 10000);
        var panel = new StackPanel { Spacing = 14, Width = DialogContentWidth(300) };
        panel.Children.Add(width);
        panel.Children.Add(height);
        panel.Children.Add(grid);
        if (await ShowDialogAsync(MakeDialog("Artboard and grid", panel)) != ContentDialogResult.Primary)
            return;
        if (width.Value != originalWidth || height.Value != originalHeight)
            ChangeArtboard(width.Value, height.Value);
        Controller.Session.Settings.GridSize = grid.Value;
        Controller.Selection.GridSize = grid.Value;
        Canvas.Invalidate();
    }

    private async Task EditSourceAsync()
    {
        if (Controller is not { } controller)
            return;
        var editor = SourceEditor(controller.GetSvg());
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Microsoft.UI.Colors.IndianRed) };
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(editor);
        content.Children.Add(error);
        var dialog = MakeDialog("SVG source", content, "Apply SVG");
        dialog.PrimaryButtonClick += (_, args) =>
        {
            try
            { controller.ReplaceSvg(editor.Text); }
            catch (Exception exception) { args.Cancel = true; error.Text = exception.Message; }
        };
        await ShowDialogAsync(dialog);
    }

    private TextBox SourceEditor(string source) => new()
    {
        Text = source,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        FontFamily = new FontFamily("monospace"),
        FontSize = 12,
        Width = DialogContentWidth(560),
        Height = Math.Min(420, Math.Max(160, (XamlRoot?.Size.Height ?? ActualHeight) - 260)),
        IsSpellCheckEnabled = false
    };

    private async Task EditTextAsync()
    {
        if (Controller is not { } controller || controller.SelectedElements.FirstOrDefault() is not SvgTextBase text)
            throw new InvalidOperationException("Select a text object, or draw one with the text tool.");
        controller.EnsureSelectionEditable();
        var originalText = text.Text ?? string.Empty;
        var originalFont = text.FontFamily ?? "sans-serif";
        var originalSize = text.FontSize.Value > 0 ? text.FontSize.Value : 24;
        var originalTracking = text.LetterSpacing.Value;
        var value = new TextBox { Header = "Text", Text = originalText, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 140 };
        var font = new TextBox { Header = "Font family", Text = originalFont };
        var size = NumberInput("Font size", originalSize, 1, 10000);
        var tracking = NumberInput("Letter spacing", originalTracking, -1000, 1000);
        var content = new StackPanel { Spacing = 12, Width = DialogContentWidth(380) };
        content.Children.Add(value);
        content.Children.Add(font);
        content.Children.Add(size);
        content.Children.Add(tracking);
        if (await ShowDialogAsync(MakeDialog("Typography", content)) != ContentDialogResult.Primary)
            return;
        controller.SetTypography(
            text: value.Text != originalText ? value.Text : null,
            fontFamily: font.Text != originalFont ? font.Text : null,
            fontSize: size.Value != originalSize ? new SvgUnit((float)size.Value) : null,
            letterSpacing: tracking.Value != originalTracking ? new SvgUnit((float)tracking.Value) : null);
    }

    private async Task InsertElementAsync()
    {
        if (Controller is not { } controller)
            return;
        var editor = SourceEditor("<rect x=\"40\" y=\"40\" width=\"200\" height=\"120\" rx=\"20\" fill=\"#7061dc\" />");
        if (await ShowDialogAsync(MakeDialog("Insert SVG elements", editor, "Insert")) != ContentDialogResult.Primary)
            return;
        var xml = editor.Text.Trim();
        if (!xml.StartsWith("<svg", StringComparison.Ordinal))
            xml = $"<svg xmlns=\"{SvgNamespace}\">{xml}</svg>";
        controller.Paste(xml, 0, 0);
    }

    private async Task TransformDialogAsync()
    {
        if (Controller is not { } controller || controller.SelectionBounds is not { } bounds)
            return;
        var x = NumberInput("Move X", 0, -1000000, 1000000);
        var y = NumberInput("Move Y", 0, -1000000, 1000000);
        var sx = NumberInput("Scale X %", 100, -10000, 10000);
        var sy = NumberInput("Scale Y %", 100, -10000, 10000);
        var rotation = NumberInput("Rotation °", 0, -36000, 36000);
        var skew = NumberInput("Skew X °", 0, -85, 85);
        var content = new StackPanel { Spacing = 10, Width = DialogContentWidth(320) };
        foreach (var field in new[] { x, y, sx, sy, rotation, skew })
            content.Children.Add(field);
        if (await ShowDialogAsync(MakeDialog("Transform selection", new ScrollViewer { Content = content, MaxHeight = 480 })) != ContentDialogResult.Primary)
            return;
        var center = new SKPoint(bounds.MidX, bounds.MidY);
        var transform = SKMatrix.CreateTranslation((float)x.Value, (float)y.Value);
        transform = SKMatrix.Concat(transform, SKMatrix.CreateRotationDegrees((float)rotation.Value, center.X, center.Y));
        var local = SKMatrix.Concat(SKMatrix.CreateSkew((float)Math.Tan(skew.Value * Math.PI / 180), 0), SKMatrix.CreateScale((float)sx.Value / 100, (float)sy.Value / 100));
        local = SKMatrix.Concat(SKMatrix.CreateTranslation(center.X, center.Y), SKMatrix.Concat(local, SKMatrix.CreateTranslation(-center.X, -center.Y)));
        controller.TransformSelection(SKMatrix.Concat(transform, local));
    }

    private void CreateLayer()
    {
        if (Controller?.Document is not { } document)
            return;
        var group = new SvgGroup { ID = UniqueId("layer") };
        group.CustomAttributes["data-unodraw-layer"] = "true";
        Controller.AddElement(group, "New layer");
    }

    private string UniqueId(string prefix)
    {
        XmlConvert.VerifyNCName(prefix);
        var ids = new HashSet<string>(Controller?.Document?.Descendants().Select(element => element.ID).OfType<string>() ?? Array.Empty<string>());
        if (Controller?.Document?.ID is { } rootId)
            ids.Add(rootId);
        if (!ids.Contains(prefix))
            return prefix;
        var number = 2;
        while (ids.Contains($"{prefix}-{number}"))
            number++;
        return $"{prefix}-{number}";
    }

    private SvgDefinitionList GetDefinitions()
    {
        var document = Controller!.Document!;
        var defs = document.Children.OfType<SvgDefinitionList>().FirstOrDefault();
        if (defs is not null)
            return defs;
        defs = new SvgDefinitionList();
        document.Children.Insert(0, defs);
        return defs;
    }

    private SvgElement ParseResource(XElement resource)
    {
        var xml = new XElement(SvgNamespace + "svg", resource).ToString(SaveOptions.DisableFormatting);
        var document = new SvgDocumentService().FromSvg(xml) ?? throw new XmlException("The SVG resource could not be parsed.");
        var element = document.Children.First();
        document.Children.Remove(element);
        return element;
    }

    private static SvgPaintServer ParsePaint(string text) => (SvgPaintServer)TypeDescriptor.GetConverter(typeof(SvgPaintServer)).ConvertFromInvariantString(text)!;

    private async Task GradientDialogAsync()
    {
        if (Controller is not { } controller || controller.SelectedElements.Count == 0)
            throw new InvalidOperationException("Select an object to give it a gradient fill.");
        controller.EnsureSelectionEditable();
        var type = new ComboBox { Header = "Type", HorizontalAlignment = HorizontalAlignment.Stretch };
        type.Items.Add("Linear");
        type.Items.Add("Radial");
        type.SelectedIndex = 0;
        var angle = NumberInput("Angle °", 0, -360, 360);
        var stops = new TextBox { Header = "Stops: position, color, opacity (one per line)", Text = "0, #7061dc, 1\n0.5, #b2a5ef, 1\n1, #f2b99e, 1", AcceptsReturn = true, Height = 160, FontFamily = new FontFamily("monospace") };
        var panel = new StackPanel { Spacing = 14, Width = DialogContentWidth(420) };
        panel.Children.Add(type);
        panel.Children.Add(angle);
        panel.Children.Add(stops);
        if (await ShowDialogAsync(MakeDialog("Gradient fill", panel)) != ContentDialogResult.Primary)
            return;
        var id = UniqueId("gradient");
        var gradient = new XElement(SvgNamespace + (type.SelectedIndex == 1 ? "radialGradient" : "linearGradient"), new XAttribute("id", id));
        if (type.SelectedIndex == 0)
            gradient.Add(new XAttribute("x1", "0%"), new XAttribute("x2", "100%"), new XAttribute("y1", "50%"), new XAttribute("y2", "50%"), new XAttribute("gradientTransform", $"rotate({angle.Value.ToString(CultureInfo.InvariantCulture)} .5 .5)"));
        var parsedStops = new List<(double Offset, string Color, double Opacity)>();
        foreach (var line in stops.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = line.Split(',', StringSplitOptions.TrimEntries);
            if (pieces.Length is < 2 or > 3 || !double.TryParse(pieces[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var offset))
                throw new FormatException("Each stop needs a position from 0 to 1, a color, and optional opacity.");
            var opacity = pieces.Length == 3 ? double.Parse(pieces[2], CultureInfo.InvariantCulture) : 1;
            _ = ParsePaint(pieces[1]);
            if (!double.IsFinite(offset) || !double.IsFinite(opacity) || offset is < 0 or > 1 || opacity is < 0 or > 1)
                throw new FormatException("Gradient positions and opacity must be between 0 and 1.");
            parsedStops.Add((offset, pieces[1], opacity));
        }
        if (parsedStops.Count < 2)
            throw new FormatException("A gradient needs at least two stops.");
        foreach (var stop in parsedStops.OrderBy(stop => stop.Offset))
            gradient.Add(new XElement(SvgNamespace + "stop", new XAttribute("offset", stop.Offset), new XAttribute("stop-color", stop.Color), new XAttribute("stop-opacity", stop.Opacity)));
        var resource = ParseResource(gradient);
        var paint = ParsePaint($"url(#{id})");
        controller.Execute("Create gradient", () =>
        {
            GetDefinitions().Children.Add(resource);
            foreach (var element in controller.SelectedElements)
                element.Fill = paint;
        });
    }

    private async Task PatternDialogAsync()
    {
        if (Controller is not { } controller || controller.SelectedElements.Count == 0)
            throw new InvalidOperationException("Select an object to give it a pattern fill.");
        controller.EnsureSelectionEditable();
        var tile = NumberInput("Tile size", 24, 1, 10000);
        var editor = new TextBox { Header = "Tile SVG", AcceptsReturn = true, Height = 180, Width = DialogContentWidth(440), Text = "<circle cx=\"12\" cy=\"12\" r=\"4\" fill=\"#7061dc\"/>" };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(tile);
        panel.Children.Add(editor);
        if (await ShowDialogAsync(MakeDialog("Pattern fill", panel)) != ContentDialogResult.Primary)
            return;
        var id = UniqueId("pattern");
        var children = XElement.Parse($"<g xmlns=\"{SvgNamespace}\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">{editor.Text}</g>");
        var pattern = new XElement(SvgNamespace + "pattern", new XAttribute("id", id), new XAttribute("patternUnits", "userSpaceOnUse"), new XAttribute("width", tile.Value), new XAttribute("height", tile.Value), children.Elements());
        var resource = ParseResource(pattern);
        controller.Execute("Create pattern", () =>
        {
            controller.EnsureSelectionEditable();
            var adopted = controller.AddResource(resource);
            var paint = ParsePaint($"url(#{adopted.ID})");
            foreach (var element in controller.SelectedElements)
                element.Fill = paint;
        });
    }

    private async Task CreateSymbolAsync()
    {
        if (Controller is not { } controller || controller.SelectionBounds is null)
            throw new InvalidOperationException("Select artwork to make a reusable symbol.");
        var name = new TextBox { Header = "Symbol ID", Text = UniqueId("symbol"), Width = DialogContentWidth(320) };
        if (await ShowDialogAsync(MakeDialog("Create symbol", name)) != ContentDialogResult.Primary)
            return;
        var id = UniqueId(name.Text.Trim());
        controller.CreateSymbol(id);
    }

    private async Task EditStylesAsync()
    {
        if (Controller is not { } controller)
            return;
        var xml = XDocument.Parse(controller.GetSvg(), LoadOptions.PreserveWhitespace);
        var style = xml.Root!.Elements().FirstOrDefault(element => element.Name.LocalName == "style");
        var editor = SourceEditor(style?.Value ?? "/* SVG document styles */\n.accent { fill: #7061dc; }");
        if (await ShowDialogAsync(MakeDialog("Document CSS", editor)) != ContentDialogResult.Primary)
            return;
        if (style is null)
        { style = new XElement(SvgNamespace + "style"); xml.Root.AddFirst(style); }
        style.Value = editor.Text;
        controller.ReplaceSvg(xml.ToString(SaveOptions.DisableFormatting), "Edit CSS");
    }

    private void CreateClippingMask() => Controller?.CreateClippingMask();

    private async Task ShadowDialogAsync()
    {
        if (Controller is not { } controller || controller.SelectedElements.Count == 0)
            return;
        controller.EnsureSelectionEditable();
        var x = NumberInput("Offset X", 0, -10000, 10000);
        var y = NumberInput("Offset Y", 8, -10000, 10000);
        var blur = NumberInput("Blur", 8, 0, 1000);
        var opacity = NumberInput("Opacity %", 20, 0, 100);
        var panel = new StackPanel { Spacing = 12, Width = DialogContentWidth(320) };
        foreach (var field in new[] { x, y, blur, opacity })
            panel.Children.Add(field);
        if (await ShowDialogAsync(MakeDialog("Drop shadow", panel)) != ContentDialogResult.Primary)
            return;
        var id = UniqueId("shadow");
        var filter = new XElement(SvgNamespace + "filter", new XAttribute("id", id), new XAttribute("x", "-50%"), new XAttribute("y", "-50%"), new XAttribute("width", "200%"), new XAttribute("height", "200%"),
            new XElement(SvgNamespace + "feGaussianBlur", new XAttribute("in", "SourceAlpha"), new XAttribute("stdDeviation", blur.Value), new XAttribute("result", "blur")),
            new XElement(SvgNamespace + "feOffset", new XAttribute("in", "blur"), new XAttribute("dx", x.Value), new XAttribute("dy", y.Value), new XAttribute("result", "offset")),
            new XElement(SvgNamespace + "feFlood", new XAttribute("flood-color", "#000000"), new XAttribute("flood-opacity", opacity.Value / 100), new XAttribute("result", "color")),
            new XElement(SvgNamespace + "feComposite", new XAttribute("in", "color"), new XAttribute("in2", "offset"), new XAttribute("operator", "in"), new XAttribute("result", "shadow")),
            new XElement(SvgNamespace + "feMerge", new XElement(SvgNamespace + "feMergeNode", new XAttribute("in", "shadow")), new XElement(SvgNamespace + "feMergeNode", new XAttribute("in", "SourceGraphic"))));
        var resource = ParseResource(filter);
        controller.Execute("Add drop shadow", () =>
        {
            GetDefinitions().Children.Add(resource);
            foreach (var element in controller.SelectedElements)
                element.Filter = new Uri("#" + id, UriKind.Relative);
        });
    }

    private async Task OffsetPathAsync()
    {
        if (Controller is not { } controller || controller.SelectedElements.Count == 0)
            return;
        controller.EnsureSelectionEditable();
        var distance = NumberInput("Distance in document units", 10, -10000, 10000);
        if (await ShowDialogAsync(MakeDialog("Offset path", distance)) != ContentDialogResult.Primary)
            return;
        controller.OffsetSelection((float)distance.Value);
    }

    private async Task BlendPathsAsync()
    {
        if (Controller is not { } controller || controller.SelectedElements.Count != 2 || controller.SelectedElements.Any(element => element is not SvgPath))
            throw new InvalidOperationException("Select two paths with matching segment structures.");
        controller.EnsureSelectionEditable();
        var from = (SvgPath)controller.SelectedElements[0];
        var to = (SvgPath)controller.SelectedElements[1];
        if (!ReferenceEquals(from.Parent, to.Parent) || (from.Transforms?.ToString() ?? "") != (to.Transforms?.ToString() ?? ""))
            throw new InvalidOperationException("Blend paths must share a parent and transform.");
        var steps = NumberInput("Intermediate steps", 5, 1, 200);
        if (await ShowDialogAsync(MakeDialog("Blend paths", steps)) != ContentDialogResult.Primary)
            return;
        var paths = controller.Paths.Blend(from, to, (int)steps.Value);
        if (paths.Count == 0)
            throw new InvalidOperationException("The selected path segment structures do not match.");
        controller.Execute("Blend paths", () =>
        {
            foreach (var path in paths)
            {
                path.ID = UniqueId("blend");
                path.Fill = from.Fill;
                path.Stroke = from.Stroke;
                path.StrokeWidth = from.StrokeWidth;
                path.Transforms = from.Transforms?.Clone() as SvgTransformCollection;
                from.Parent.Children.Add(path);
            }
        });
    }

    private void EditNode(bool smooth)
    {
        if (Canvas.ActiveNodeIndex < 0)
            throw new InvalidOperationException("Choose the node tool and select a path node first.");
        Canvas.EditActiveNode(smooth);
    }
}
