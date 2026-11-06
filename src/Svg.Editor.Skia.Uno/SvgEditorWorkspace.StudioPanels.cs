using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Svg.Editor.Skia;
using Svg.Editor.Svg;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private readonly List<StudioTextField> _studioTextFields = new();
    private readonly List<(Button Button, Func<bool> Enabled)> _studioActions = new();
    private readonly List<StudioPaintChip> _fillChips = new();
    private readonly List<StudioPaintChip> _strokeChips = new();
    private readonly List<StudioNumericField> _strokeWidths = new();
    private readonly List<StudioNumericField> _opacities = new();
    private StudioNumericField? _studioX, _studioY, _studioW, _studioH, _studioAngle, _studioMiter;
    private StudioTextField? _studioFont, _studioFontSize, _studioText, _studioDash;
    private ComboBox? _studioCap, _studioJoin;
    private StudioReferencePoint? _studioAnchor;
    private CheckBox? _studioAspect;
    private TextBlock? _studioAppearanceSummary, _studioTextHint;
    private bool _syncingStudioPanels;

    private StackPanel StudioPanel(string id, string title)
    {
        var stack = new StackPanel { Spacing = (double)Resources["StudioSectionGap"], Margin = (Thickness)Resources["StudioPanelPadding"] };
        RegisterStudioPanel(id, title, new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        return stack;
    }
    private static StudioFieldGrid StudioPair(UIElement first, UIElement second)
    {
        var grid = new StudioFieldGrid { MinimumColumnWidth = 128 };
        grid.Children.Add(first); grid.Children.Add(second); return grid;
    }
    private StudioNumericField StudioNumber(string title, double minimum, double maximum, Action<SvgEditorController, double> apply)
    {
        var field = new StudioNumericField { Header = title, Minimum = minimum, Maximum = maximum };
        field.ValueEdited += (_, args) =>
        {
            if (_syncingStudioPanels) return;
            ApplySafely(() => StudioEdit(controller => apply(controller, args.Value)));
            RefreshStudioPanels();
        };
        return field;
    }
    private StudioTextField StudioText(string title, Action<SvgEditorController, string> apply, bool multiline = false)
    {
        var field = new StudioTextField(title, multiline);
        field.TextEdited += (_, text) => StudioEdit(controller => apply(controller, text));
        field.ValidationFailed += (_, exception) => StatusText.Text = exception.Message;
        _studioTextFields.Add(field); return field;
    }
    private void StudioEdit(Action<SvgEditorController> action)
    {
        if (_syncingStudioPanels) return;
        var controller = Controller ?? throw new InvalidOperationException("Open a document first.");
        if (controller.SelectedElements.Count == 0) throw new InvalidOperationException("Select artwork first.");
        controller.EnsureSelectionEditable(); Canvas.CancelGesture(); action(controller);
    }
    private void CommitStudioEdits()
    {
        foreach (var field in _studioTextFields) field.Commit();
    }
    private StudioPaintChip StudioPaint(bool stroke)
    {
        var chip = new StudioPaintChip { Label = stroke ? "Stroke" : "Fill" };
        chip.Click += (_, _) => OpenStudioPaint(chip, stroke);
        (stroke ? _strokeChips : _fillChips).Add(chip); return chip;
    }
    private Button StudioCommand(string id, string title, int minimumSelection = 0)
    {
        var button = ActionButton(id, title);
        button.Content = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
        AutomationProperties.SetName(button, title);
        _studioActions.Add((button, () => Controller is { } controller && controller.SelectedElements.Count >= minimumSelection
            && (minimumSelection == 0 || !controller.SelectedElements.Any(SvgEditorController.IsEditLocked))));
        return button;
    }
    private static StudioSection StudioSection(string title, object content) => new() { Header = title, Content = content };
    private TextBlock StudioHint(string text) => new() { Text = text, Style = (Style)Resources["StudioHintStyle"] };

    private void BuildStudioPanels()
    {
        var transform = StudioPanel("transform", "Transform");
        _studioAnchor = new StudioReferencePoint { HorizontalAlignment = HorizontalAlignment.Left };
        _studioAnchor.SelectionChanged += (_, _) => RefreshStudioPanels();
        _studioAspect = new CheckBox { Content = new TextBlock { Text = "Constrain proportions", TextWrapping = TextWrapping.Wrap }, IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(_studioAspect, "Constrain proportions");
        transform.Children.Add(StudioPair(_studioAnchor, _studioAspect));
        _studioX = StudioNumber("X", -1000000, 1000000, (controller, value) =>
        { var pivot = StudioPivot(controller); controller.MoveSelection((float)value - pivot.X, 0); });
        _studioY = StudioNumber("Y", -1000000, 1000000, (controller, value) =>
        { var pivot = StudioPivot(controller); controller.MoveSelection(0, (float)value - pivot.Y); });
        _studioW = StudioNumber("W", 0.001, 1000000, (controller, value) => ScaleStudioSelection(controller, value, true));
        _studioH = StudioNumber("H", 0.001, 1000000, (controller, value) => ScaleStudioSelection(controller, value, false));
        _studioAngle = StudioNumber("Δ°", -36000, 36000, (controller, value) =>
        { var pivot = StudioPivot(controller); controller.TransformSelection(SKMatrix.CreateRotationDegrees((float)value, pivot.X, pivot.Y)); });
        transform.Children.Add(StudioPair(_studioX, _studioY)); transform.Children.Add(StudioPair(_studioW, _studioH));
        transform.Children.Add(_studioAngle);
        transform.Children.Add(StudioHint("Position uses the chosen reference point in canvas coordinates. Rotation is a relative angle."));
        transform.Children.Add(StudioPair(StudioCommand("fliph", "Flip horizontal", 1), StudioCommand("flipv", "Flip vertical", 1)));
        transform.Children.Add(StudioCommand("transform", "Transform precisely…", 1));

        var appearance = StudioPanel("appearance", "Appearance");
        appearance.Children.Add(_studioAppearanceSummary = StudioHint("Select artwork to edit its appearance."));
        appearance.Children.Add(StudioPair(StudioPaint(false), StudioPaint(true)));
        var opacity = StudioNumber("Opacity", 0, 100, (controller, value) => controller.SetOpacity((float)value / 100));
        opacity.Suffix = "%"; _opacities.Add(opacity); appearance.Children.Add(opacity);
        appearance.Children.Add(StudioPair(StudioCommand("swappaints", "Swap fill / stroke", 1), StudioCommand("defaultpaints", "Default paints", 1)));
        appearance.Children.Add(StudioSection("Paint and effects", CommandWrap(new[]
        { ("gradient", "Gradient…"), ("pattern", "Pattern…"), ("mesh", "Mesh…"), ("filterstudio", "Filter Studio…"), ("effect", "Shadow…"), ("removefilters", "Remove filters") }, 1)));
        appearance.Children.Add(StudioSection("Masks and clipping", CommandWrap(new[]
        { ("clip", "Clip"), ("unclip", "Release clip"), ("alphamask", "Alpha mask"), ("luminancemask", "Luminance mask"), ("releasemask", "Release mask"), ("disablemask", "Disable mask") }, 1)));

        var stroke = StudioPanel("stroke", "Stroke");
        var strokeWidth = StudioNumber("Weight", 0, 10000, (controller, value) => controller.SetStrokeWidth((float)value));
        _strokeWidths.Add(strokeWidth); stroke.Children.Add(strokeWidth);
        _studioCap = StudioChoice("Cap", new[] { "butt", "round", "square" }, "stroke-linecap");
        _studioJoin = StudioChoice("Join", new[] { "miter", "round", "bevel" }, "stroke-linejoin");
        stroke.Children.Add(StudioPair(_studioCap, _studioJoin));
        _studioMiter = StudioNumber("Miter", 1, 1000, (controller, value) => controller.SetAttribute("stroke-miterlimit", value.ToString(CultureInfo.InvariantCulture)));
        stroke.Children.Add(_studioMiter);
        _studioDash = StudioText("Dash pattern · SVG units", (controller, text) => controller.SetAttribute("stroke-dasharray", string.IsNullOrWhiteSpace(text) ? "none" : text));
        stroke.Children.Add(_studioDash);
        stroke.Children.Add(StudioHint("Enter alternating dash and gap lengths, for example 8 4. Blank restores a solid stroke."));
        stroke.Children.Add(StudioPair(StudioCommand("strokeprofile", "Width profile…", 1), StudioCommand("brushes", "Brush library…")));

        var align = StudioPanel("align", "Align");
        align.Children.Add(StudioHint("Align and distribute within the current selection."));
        var alignButtons = new StudioWrapPanel { Spacing = 5 };
        foreach (var alignment in Enum.GetValues<AlignService.AlignType>()) alignButtons.Children.Add(StudioCommand("align" + alignment, alignment.ToString(), 2));
        align.Children.Add(StudioSection("Align objects", alignButtons));
        var distribute = new StudioWrapPanel { Spacing = 5 };
        foreach (var direction in Enum.GetValues<AlignService.DistributeType>()) distribute.Children.Add(StudioCommand("distribute" + direction, direction.ToString(), 3));
        align.Children.Add(StudioSection("Distribute objects", distribute));
        align.Children.Add(StudioSection("Stacking order", CommandWrap(new[] { ("front", "To front"), ("back", "To back"), ("forward", "Forward"), ("backward", "Backward") }, 1)));

        var geometry = StudioPanel("geometry", "Geometry");
        geometry.Children.Add(StudioSection("Shape operations", CommandWrap(new[] { ("union", "Unite"), ("subtract", "Subtract"), ("intersect", "Intersect"), ("exclude", "Exclude"), ("divide", "Divide…") }, 2)));
        geometry.Children.Add(StudioSection("Paths", CommandWrap(new[] { ("convertpath", "Convert to paths"), ("simplify", "Simplify"), ("offset", "Offset…"), ("blend", "Blend…") }, 1)));
        geometry.Children.Add(StudioSection("Node editing", CommandWrap(new[] { ("smooth", "Smooth node"), ("corner", "Corner node"), ("finishpath", "Finish path") })));
        geometry.Children.Add(StudioHint("Shape operations remain editable SVG paths. Use the node tool to refine the resulting geometry."));

        var typography = StudioPanel("typography", "Typography");
        typography.Children.Add(_studioTextHint = StudioHint("Select text to edit character properties."));
        _studioFont = StudioText("Font family", (controller, text) => controller.SetAttribute("font-family", text));
        _studioFontSize = StudioText("Font size", (controller, text) => controller.SetAttribute("font-size", text));
        _studioText = StudioText("Plain text content", (controller, text) => controller.SetText(text), true);
        typography.Children.Add(_studioFont); typography.Children.Add(_studioFontSize); typography.Children.Add(_studioText);
        typography.Children.Add(StudioHint("Use rich text editing to preserve independently styled ranges and linked text stories."));
        typography.Children.Add(StudioPair(StudioCommand("text", "Rich text…", 1), StudioCommand("textframes", "Paragraphs / frames…")));

        var effects = StudioPanel("effects", "Effects");
        effects.Children.Add(StudioSection("SVG filter graph", CommandWrap(new[] { ("filterstudio", "Open Filter Studio…"), ("effect", "Drop shadow…"), ("removefilters", "Remove filters") }, 1)));
        effects.Children.Add(StudioSection("Animation", CommandWrap(new[] { ("timeline", "Timeline…"), ("animate", "Play / pause"), ("bakeframe", "Bake current frame") })));
        effects.Children.Add(StudioHint("Filter and animation studios keep their transactional draft workflows. Their commands remain accessible while panel groups are rearranged."));
        ResourcesShelf.Children.Add(StudioSection("Paint libraries", CommandWrap(new[] { ("swatches", "Swatches…"), ("gradient", "Gradient…"), ("pattern", "Pattern…"), ("brushes", "Brushes…"), ("mesh", "Mesh…") })));
        ResourcesShelf.Children.Add(StudioSection("Document resources", CommandWrap(new[] { ("symbol", "Create symbol…"), ("insert", "Insert SVG…"), ("image", "Place image…"), ("style", "Document CSS…") })));
    }

    private StudioFieldGrid CommandWrap(IEnumerable<(string Id, string Label)> commands, int selection = 0)
    {
        var panel = new StudioFieldGrid { MinimumColumnWidth = 128 };
        foreach (var command in commands) panel.Children.Add(StudioCommand(command.Id, command.Label, selection));
        return panel;
    }
    private ComboBox StudioChoice(string label, string[] values, string attribute)
    {
        var combo = new ComboBox { Header = label, ItemsSource = values, HorizontalAlignment = HorizontalAlignment.Stretch };
        combo.SelectionChanged += (_, _) =>
        {
            if (!_syncingStudioPanels && combo.SelectedItem is string value)
                ApplySafely(() => StudioEdit(controller => controller.SetAttribute(attribute, value)));
        };
        return combo;
    }
    private SKPoint StudioPivot(SvgEditorController controller)
    {
        var bounds = controller.SelectionBounds ?? throw new InvalidOperationException("The selection has no finite bounds.");
        var index = _studioAnchor?.SelectedIndex ?? 0;
        return new SKPoint(bounds.Left + bounds.Width * (index % 3) / 2, bounds.Top + bounds.Height * (index / 3) / 2);
    }
    private void ScaleStudioSelection(SvgEditorController controller, double value, bool horizontal)
    {
        var bounds = controller.SelectionBounds ?? throw new InvalidOperationException("The selection has no finite bounds.");
        var size = horizontal ? bounds.Width : bounds.Height;
        if (size <= 0) throw new InvalidOperationException("A zero-size selection cannot be scaled on this axis.");
        var ratio = (float)value / size;
        var pivot = StudioPivot(controller);
        var linked = _studioAspect?.IsChecked == true;
        controller.TransformSelection(SKMatrix.CreateScale(horizontal || linked ? ratio : 1, !horizontal || linked ? ratio : 1, pivot.X, pivot.Y));
    }

    private void BuildStudioContextBar()
    {
        ContextFields.Children.Add(StudioPaint(false)); ContextFields.Children.Add(StudioPaint(true));
        var stroke = StudioNumber("Weight", 0, 10000, (controller, value) => controller.SetStrokeWidth((float)value));
        stroke.Width = 124; _strokeWidths.Add(stroke); ContextFields.Children.Add(stroke);
        var opacity = StudioNumber("Opacity", 0, 100, (controller, value) => controller.SetOpacity((float)value / 100));
        opacity.Width = 138; opacity.Suffix = "%"; _opacities.Add(opacity); ContextFields.Children.Add(opacity);
        foreach (var entry in new[] { ("transform", "Transform"), ("align", "Align"), ("geometry", "Geometry"), ("appearance", "Appearance") })
        {
            var button = new StudioIconButton { Label = entry.Item2 + " panel", Content = entry.Item2 };
            button.Click += (_, _) => ApplySafely(() => OpenStudioPanel(entry.Item1)); ContextFields.Children.Add(button);
        }
        ContextFields.Children.Add(StudioCommand("undo", "Undo")); ContextFields.Children.Add(StudioCommand("redo", "Redo"));
    }

    private void RefreshStudioPanels()
    {
        if (!_studioInitialized || _syncingStudioPanels || Controller is not { } controller) return;
        _syncingStudioPanels = true;
        try
        {
            var selected = controller.SelectedElements;
            var editable = selected.Count > 0 && !selected.Any(SvgEditorController.IsEditLocked);
            ContextSelectionLabel.Text = selected.Count == 0 ? "No selection" : selected.Count == 1 ? SvgElementInfo.GetElementName(selected[0].GetType()) : selected.Count + " objects";
            foreach (var action in _studioActions) action.Button.IsEnabled = action.Enabled();
            bool Mixed(Func<SvgVisualElement, string> value) => selected.Count > 1 && selected.Skip(1).Any(element => value(element) != value(selected[0]));
            var first = selected.FirstOrDefault();
            foreach (var chip in _fillChips) { chip.IsEnabled = editable; chip.IsMixed = Mixed(element => PaintText(element.Fill)); chip.Paint = first is null ? "none" : PaintText(first.Fill); }
            foreach (var chip in _strokeChips) { chip.IsEnabled = editable; chip.IsMixed = Mixed(element => PaintText(element.Stroke)); chip.Paint = first is null ? "none" : PaintText(first.Stroke); }
            foreach (var field in _strokeWidths) { field.IsEnabled = editable; field.Value = first?.StrokeWidth.Value ?? 0; field.IsMixed = Mixed(element => element.StrokeWidth.ToString()); }
            foreach (var field in _opacities) { field.IsEnabled = editable; field.Value = (first?.Opacity ?? 1) * 100; field.IsMixed = Mixed(element => element.Opacity.ToString(CultureInfo.InvariantCulture)); }
            var bounds = controller.SelectionBounds;
            foreach (var field in new[] { _studioX, _studioY, _studioW, _studioH, _studioAngle }) if (field is not null) field.IsEnabled = editable && bounds is not null;
            if (bounds is { } rectangle && _studioX is not null)
            {
                var pivot = StudioPivot(controller); _studioX.Value = pivot.X; _studioY!.Value = pivot.Y;
                _studioW!.Value = rectangle.Width; _studioH!.Value = rectangle.Height; _studioAngle!.Value = 0;
            }
            if (_studioAppearanceSummary is not null) _studioAppearanceSummary.Text = first is null ? "Select artwork to edit its appearance." : ContextSelectionLabel.Text + " · SVG appearance";
            if (_studioMiter is not null) { _studioMiter.IsEnabled = editable; _studioMiter.Value = first?.StrokeMiterLimit ?? 4; }
            if (_studioCap is not null) { _studioCap.IsEnabled = editable; _studioCap.SelectedItem = first?.StrokeLineCap.ToString().ToLowerInvariant(); }
            if (_studioJoin is not null) { _studioJoin.IsEnabled = editable; _studioJoin.SelectedItem = first?.StrokeLineJoin.ToString().ToLowerInvariant(); }
            if (_studioDash is not null) { _studioDash.IsEnabled = editable; _studioDash.Synchronize(first?.StrokeDashArray?.ToString()); }
            var textOnly = editable && selected.All(element => element is SvgTextBase);
            if (_studioFont is not null) { _studioFont.IsEnabled = textOnly; _studioFont.Synchronize((first as SvgTextBase)?.FontFamily, Mixed(element => (element as SvgTextBase)?.FontFamily ?? "")); }
            if (_studioFontSize is not null) { _studioFontSize.IsEnabled = textOnly; _studioFontSize.Synchronize((first as SvgTextBase)?.FontSize.ToString(), Mixed(element => (element as SvgTextBase)?.FontSize.ToString() ?? "")); }
            if (_studioText is not null)
            {
                // Replacing plain text must not accidentally discard tspan formatting.
                _studioText.IsEnabled = textOnly && selected.Count == 1 && first!.Children.Count == 0;
                _studioText.Synchronize((first as SvgTextBase)?.Text);
            }
            if (_studioTextHint is not null) _studioTextHint.Text = textOnly ? "Character properties apply to the selected text objects." : "Select text to edit character properties.";
        }
        finally { _syncingStudioPanels = false; }
    }

    private void OpenStudioPaint(StudioPaintChip anchor, bool stroke)
    {
        if (Controller is not { } controller || controller.SelectedElements.Count == 0) return;
        CommitInspectorEdits();
        var targets = controller.SelectedElements.ToArray();
        var document = controller.Document;
        var input = new TextBox { Header = stroke ? "Stroke paint" : "Fill paint", Text = anchor.IsMixed ? string.Empty : anchor.Paint, PlaceholderText = "#RRGGBB, none, or url(#resource)", MinWidth = 220, MaxLength = 4096 };
        var error = StudioHint(string.Empty);
        var content = new StackPanel { Spacing = 12, Width = Math.Max(220, Math.Min(280, ActualWidth - 80)) };
        var palette = new StudioWrapPanel { Spacing = 5 };
        var flyout = new Flyout { Content = content };
        void Apply(string paint)
        {
            try
            {
                if (!ReferenceEquals(controller, Controller) || !ReferenceEquals(document, controller.Document) || !targets.SequenceEqual(controller.SelectedElements))
                    throw new InvalidOperationException("The paint target changed. Reopen the paint selector.");
                controller.EnsureSelectionEditable();
                if (stroke) controller.SetStroke(paint); else controller.SetFill(paint);
                flyout.Hide();
            }
            catch (Exception exception) { error.Text = exception.Message; }
        }
        foreach (var color in new[] { "none", "#000000", "#FFFFFF", "#435E8E", "#7061DC", "#D24454", "#EE9A42", "#E7CE53", "#4F9B77", "#4A9CC3", "#C774AF", "#9B8071" })
        {
            var chip = new StudioPaintChip { Paint = color, Label = string.Empty, IsCompact = true, MinWidth = 32, Width = 36 };
            chip.Click += (_, _) => Apply(color); palette.Children.Add(chip);
        }
        var apply = new Button { Content = "Apply paint", HorizontalAlignment = HorizontalAlignment.Stretch };
        StudioTheme.ApplyRole(apply, StudioActionRole.Primary);
        apply.Click += (_, _) => Apply(input.Text.Trim());
        input.KeyDown += (_, args) => { if (args.Key == Windows.System.VirtualKey.Enter) { Apply(input.Text.Trim()); args.Handled = true; } };
        content.Children.Add(palette); content.Children.Add(input); content.Children.Add(apply); content.Children.Add(error);
        flyout.ShowAt(anchor);
    }
}
