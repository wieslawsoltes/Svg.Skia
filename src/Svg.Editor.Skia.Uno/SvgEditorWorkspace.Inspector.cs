using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Svg.Editor.Core;
using Svg.Editor.Skia;
using Windows.UI;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private readonly Dictionary<TextBox, Action> _inspectorCommits = new();
    private bool _committingInspector;
    private StackPanel? _inspectorSection;
    private SvgElement? _lastInspectorElement;
    private SvgDocument? _lastInspectorDocument;
    private long _lastInspectorRevision = -1;
    private SvgVisualElement[] _lastInspectorSelection = Array.Empty<SvgVisualElement>();

    private void CommitInspectorEdits()
    {
        if (_refreshing || _committingInspector || _disposed) return;
        _committingInspector = true;
        try
        {
            CommitStudioEdits();
            foreach (var commit in _inspectorCommits.Values.ToArray()) commit();
            Focus(FocusState.Programmatic);
        }
        finally { _committingInspector = false; }
    }

    private sealed class ElementItem
    {
        public ElementItem(SvgElement element) => Element = element;
        public SvgElement Element { get; }
        public override string ToString()
        {
            var type = SvgElementInfo.GetElementName(Element.GetType());
            var name = string.IsNullOrEmpty(Element.ID) ? type : Element.ID;
            return $"{(Element.Display == "none" || Element.Visibility == "hidden" ? "◌ " : "")}{(SvgEditorController.IsLocked(Element) ? "▣ " : "")}{name}";
        }
    }
    private void RefreshOutline()
    {
        if (_outlineDragging) return;
        _rebuildingOutline = true;
        try
        {
            var expanded = new HashSet<SvgElement>();
            void Remember(TreeViewNode node)
            {
                if (node.IsExpanded && node.Content is ElementItem item) expanded.Add(item.Element);
                foreach (var child in node.Children) Remember(child);
            }
            foreach (var root in DocumentOutline.RootNodes) Remember(root);
            DocumentOutline.RootNodes.Clear();
            if (Controller?.Document is not { } document) return;
            var query = OutlineSearch.Text.Trim();
            TreeViewNode? Create(SvgElement element, int depth)
            {
                var item = new ElementItem(element);
                var node = new TreeViewNode { Content = item, IsExpanded = expanded.Contains(element) || depth == 0 || query.Length > 0 };
                foreach (var child in element.Children.Reverse()) if (Create(child, depth + 1) is { } childNode) node.Children.Add(childNode);
                if (query.Length > 0 && node.Children.Count == 0 && !item.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
                    && !SvgElementInfo.GetElementName(element.GetType()).Contains(query, StringComparison.OrdinalIgnoreCase)) return null;
                return node;
            }
            foreach (var element in document.Children.Reverse()) if (Create(element, 0) is { } node) DocumentOutline.RootNodes.Add(node);
            foreach (var node in EnumerateOutlineNodes(DocumentOutline.RootNodes))
                if (node.Content is ElementItem { Element: SvgVisualElement visual } && Controller!.SelectedElements.Contains(visual)) DocumentOutline.SelectedNodes.Add(node);
        }
        finally { _rebuildingOutline = false; }
    }
    private void OutlineSearch_TextChanged(object sender, TextChangedEventArgs args) { if (!_refreshing) RefreshOutline(); }
    private void DocumentOutline_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        var item = args.InvokedItem as ElementItem ?? (args.InvokedItem as TreeViewNode)?.Content as ElementItem;
        if (item is null || Controller is not { } controller) return;
        ApplySafely(() =>
        {
            CommitInspectorEdits(); Canvas.CancelGesture();
            var element = ReferenceEquals(item.Element.OwnerDocument, controller.Document) ? item.Element : controller.Document?.GetElementById(item.Element.ID);
            if (element is null) return;
            if (element is SvgVisualElement visual)
            {
                if (!controller.SelectedElements.Contains(visual)) controller.Select(visual, IsDown(Windows.System.VirtualKey.Control) || IsDown(Windows.System.VirtualKey.Shift), allowLocked: true);
            }
            else controller.ClearSelection();
            _inspectedElement = element; RefreshInspector(); RefreshStudioPanels();
        });
    }
    private void RefreshResources()
    {
        ResourceList.ItemsSource = Controller?.Document?.Descendants()
            .Where(element => element is SvgGradientServer or SvgPatternServer or SvgSymbol or SvgClipPath || element.GetType().Name == "SvgFilter")
            .Select(element => new ElementItem(element)).ToList();
    }
    private void ResourceList_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_refreshing || ResourceList.SelectedItem is not ElementItem item || Controller is not { } controller) return;
        ApplySafely(() =>
        {
            CommitInspectorEdits();
            if (item.Element is SvgGradientServer or SvgPatternServer && controller.SelectedElements.Count > 0) controller.SetFill($"url(#{item.Element.ID})");
            else if (item.Element is SvgSymbol symbol)
                controller.AddElement(new SvgUse { ReferencedElement = new Uri("#" + symbol.ID, UriKind.Relative), X = new SvgUnit(40), Y = new SvgUnit(40), Width = new SvgUnit(200), Height = new SvgUnit(200) }, "Insert symbol");
            else { controller.ClearSelection(); _inspectedElement = item.Element; RefreshInspector(); }
        });
    }

    private void RefreshInspector()
    {
        if (Controller is not { } controller) return;
        var selected = controller.SelectedElements;
        var element = _inspectedElement ?? controller.Document;
        // Camera, theme, settings and tab-docking changes must not recreate in-progress
        // editor fields. A document edit or actual selection change still rebinds them.
        if (ReferenceEquals(element, _lastInspectorElement) && ReferenceEquals(controller.Document, _lastInspectorDocument)
            && _lastInspectorRevision == controller.DocumentRevision && _lastInspectorSelection.SequenceEqual(selected)) return;
        var wasRefreshing = _refreshing; _refreshing = true;
        var expanded = InspectorContent.Children.OfType<StudioSection>().ToDictionary(section => section.Header, section => section.IsExpanded);
        try
        {
            _inspectorCommits.Clear(); InspectorContent.Children.Clear(); _inspectorSection = null;
            if (element is null) return;
            SelectionBadge.Text = selected.Count > 1 ? $"{selected.Count} objects" : SvgElementInfo.GetElementName(element.GetType());
            SelectionName.Text = selected.Count > 1 ? "Multiple selection" : string.IsNullOrEmpty(element.ID) ? (element is SvgDocument ? "Artboard" : SvgElementInfo.GetElementName(element.GetType())) : element.ID;
            if (selected.Any(SvgEditorController.IsEditLocked))
            {
                AddSection("Locked"); AddInspector(StudioHint("Unlock this object or its parent layer to edit it.")); AddInspector(ActionButton("unlock", "Unlock selection"));
            }
            else if (selected.Count == 0 && element is SvgDocument document)
            {
                AddSection("Document");
                var width = NumberInput("Width", document.Width.Value, 1, 1000000);
                var height = NumberInput("Height", document.Height.Value, 1, 1000000);
                width.ValueChanged += (_, _) => ChangeArtboard(width.Value, height.Value);
                height.ValueChanged += (_, _) => ChangeArtboard(width.Value, height.Value);
                AddInspector(StudioPair(width, height));
                AddInspector(StudioHint("Select artwork for contextual properties. Panels remain open as you move between tools and documents."));
                AddInspector(StudioPair(ActionButton("artboard", "Page setup…"), ActionButton("preflight", "Preflight…")));
            }
            else if (selected.Count > 0)
            {
                if (controller.SelectionBounds is { } bounds)
                {
                    AddSection("Transform");
                    var x = NumberInput("X", bounds.Left, -1000000, 1000000); var y = NumberInput("Y", bounds.Top, -1000000, 1000000);
                    var width = NumberInput("W", bounds.Width, 0.001, 1000000); var height = NumberInput("H", bounds.Height, 0.001, 1000000);
                    x.ValueChanged += (_, _) => ApplySafely(() => { if (controller.SelectionBounds is { } current) controller.MoveSelection((float)x.Value - current.Left, 0); });
                    y.ValueChanged += (_, _) => ApplySafely(() => { if (controller.SelectionBounds is { } current) controller.MoveSelection(0, (float)y.Value - current.Top); });
                    width.ValueChanged += (_, _) => ApplySafely(() => { if (controller.SelectionBounds is { Width: > 0 } current) controller.TransformSelection(SKMatrix.CreateScale((float)width.Value / current.Width, 1, current.Left, current.Top)); });
                    height.ValueChanged += (_, _) => ApplySafely(() => { if (controller.SelectionBounds is { Height: > 0 } current) controller.TransformSelection(SKMatrix.CreateScale(1, (float)height.Value / current.Height, current.Left, current.Top)); });
                    AddInspector(StudioPair(x, y)); AddInspector(StudioPair(width, height));
                    AddInspector(ActionButton("panel-transform", "Reference point and linked scaling"));
                }
                AddSection("Appearance");
                var first = selected[0];
                var fillMixed = selected.Skip(1).Any(item => PaintText(item.Fill) != PaintText(first.Fill));
                var strokeMixed = selected.Skip(1).Any(item => PaintText(item.Stroke) != PaintText(first.Stroke));
                var fill = new StudioPaintChip { Label = "Fill", Paint = PaintText(first.Fill), IsMixed = fillMixed };
                var paintStroke = new StudioPaintChip { Label = "Stroke", Paint = PaintText(first.Stroke), IsMixed = strokeMixed };
                fill.Click += (_, _) => OpenStudioPaint(fill, false); paintStroke.Click += (_, _) => OpenStudioPaint(paintStroke, true);
                AddInspector(StudioPair(fill, paintStroke));
                var stroke = NumberInput("Weight", first.StrokeWidth.Value, 0, 10000);
                if (selected.Skip(1).Any(item => item.StrokeWidth != first.StrokeWidth)) { stroke.Value = double.NaN; stroke.PlaceholderText = "Mixed"; }
                stroke.ValueChanged += (_, _) => { if (double.IsFinite(stroke.Value)) ApplySafely(() => controller.SetStrokeWidth((float)stroke.Value)); };
                var opacity = NumberInput("Opacity %", first.Opacity * 100, 0, 100);
                if (selected.Skip(1).Any(item => item.Opacity != first.Opacity)) { opacity.Value = double.NaN; opacity.PlaceholderText = "Mixed"; }
                opacity.ValueChanged += (_, _) => { if (double.IsFinite(opacity.Value)) ApplySafely(() => controller.SetOpacity((float)opacity.Value / 100)); };
                AddInspector(StudioPair(stroke, opacity));
                AddInspector(StudioPair(ActionButton("panel-stroke", "Stroke options"), ActionButton("panel-appearance", "Appearance")));
                if (first is SvgTextBase)
                {
                    AddSection("Typography");
                    AddInspector(StudioPair(ActionButton("panel-typography", "Character panel"), ActionButton("text", "Rich text…")));
                    AddInspector(ActionButton("textframes", "Paragraphs and linked frames…"));
                }
                AddSection("Quick actions");
                var actions = new StudioWrapPanel { Spacing = 5 };
                foreach (var command in new[] { ("group", "Group"), ("ungroup", "Ungroup"), ("duplicate", "Duplicate"), ("lock", "Lock"), ("hide", "Hide"), ("panel-geometry", "Geometry") })
                    actions.Children.Add(ActionButton(command.Item1, command.Item2));
                AddInspector(actions);
            }
            else
            {
                AddSection("SVG resource"); AddInspector(StudioHint("Edit the selected resource through the SVG attributes panel."));
                AddInspector(ActionButton("panel-attributes", "Open SVG attributes"));
            }
            foreach (var section in InspectorContent.Children.OfType<StudioSection>())
                if (expanded.TryGetValue(section.Header, out var isExpanded)) section.IsExpanded = isExpanded;
            RefreshAttributeFields(element);
            _lastInspectorElement = element; _lastInspectorDocument = controller.Document; _lastInspectorRevision = controller.DocumentRevision; _lastInspectorSelection = selected.ToArray();
        }
        finally { _refreshing = wasRefreshing; }
    }
    private void AddSection(string title)
    {
        _inspectorSection = new StackPanel { Spacing = 8 };
        InspectorContent.Children.Add(new StudioSection { Header = title, Content = _inspectorSection });
    }
    private void AddInspector(UIElement element) => (_inspectorSection ?? InspectorContent).Children.Add(element);
    private static string PaintText(SvgPaintServer? paint)
    {
        if (paint is null) return "none";
        try { return TypeDescriptor.GetConverter(typeof(SvgPaintServer)).ConvertToInvariantString(paint) ?? "none"; }
        catch { return paint.ToString() ?? "none"; }
    }
    private void RefreshAttributeFields(SvgElement element)
    {
        foreach (var field in AttributeFields.Children.OfType<TextBox>()) _inspectorCommits.Remove(field);
        AttributeFields.Children.Clear();
        var filter = AttributeSearch.Text?.Trim() ?? string.Empty;
        foreach (var property in element.GetType().GetProperties().Where(property => property.CanRead && property.CanWrite))
        {
            var attribute = property.GetCustomAttribute<SvgAttributeAttribute>();
            if (attribute is null || !attribute.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            var converter = TypeDescriptor.GetConverter(property.PropertyType);
            if (!converter.CanConvertFrom(typeof(string)) || !converter.CanConvertTo(typeof(string))) continue;
            string value;
            try { value = converter.ConvertToInvariantString(property.GetValue(element)) ?? string.Empty; }
            catch { continue; }
            AttributeFields.Children.Add(TextField(attribute.Name, value, text =>
            {
                if (attribute.Name == "id") { Controller!.RenameElementId(element, text); return; }
                var converted = converter.ConvertFromInvariantString(text);
                Controller!.Execute($"Set {attribute.Name}", () => property.SetValue(element, converted));
            }));
        }
        foreach (var pair in element.CustomAttributes.ToArray().Where(pair => pair.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            AttributeFields.Children.Add(TextField(pair.Key, pair.Value, value => Controller!.Execute($"Set {pair.Key}", () => element.CustomAttributes[pair.Key] = value)));
        foreach (var field in AttributeFields.Children.OfType<Control>()) field.IsEnabled = !SvgEditorController.IsEditLocked(element);
    }
    private void AttributeSearch_TextChanged(object sender, TextChangedEventArgs args)
    { if (!_refreshing && (_inspectedElement ?? Controller?.Document) is { } element) RefreshAttributeFields(element); }
    private TextBox TextField(string label, string value, Action<string> apply, bool multiline = false)
    {
        var field = new TextBox { Header = label, Text = value, AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap };
        AutomationProperties.SetName(field, label);
        void Commit()
        {
            if (_refreshing || !field.IsEnabled || field.Text == value) return;
            var updated = field.Text; apply(updated); value = updated;
        }
        _inspectorCommits.Add(field, Commit);
        field.LostFocus += (_, _) => { if (!_committingInspector) ApplySafely(Commit); };
        field.KeyDown += (_, args) => { if (args.Key == Windows.System.VirtualKey.Enter && !multiline) { ApplySafely(Commit); args.Handled = true; } };
        return field;
    }
    private static NumberBox NumberInput(string label, double value, double minimum, double maximum) => new()
    {
        Header = label, Value = double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : minimum,
        Minimum = minimum, Maximum = maximum, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        SmallChange = 1, LargeChange = 10, NumberFormatter = new EditorNumberFormatter(), HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private Button ActionButton(string command, string label)
    {
        var button = new Button { Content = label, Tag = command, Style = (Style)Resources["CompactButton"], HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += Command_Click; return button;
    }
    private static SolidColorBrush BrushFromHex(string hex)
    {
        var color = SKColor.Parse(hex); return new SolidColorBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
    }
    private void ApplySafely(Action action)
    {
        if (_refreshing) return;
        if (_committingInspector) { action(); return; }
        try { action(); } catch (Exception exception) { _ = ShowErrorAsync(exception.Message); }
    }
    private void ChangeArtboard(double width, double height)
    {
        if (Controller?.Document is not { } document || !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0
            || document.Width.Value == (float)width && document.Height.Value == (float)height) return;
        ApplySafely(() => Controller.Execute("Resize artboard", () =>
        {
            var viewBox = document.ViewBox; var previousWidth = document.Width; var previousHeight = document.Height;
            document.Width = new SvgUnit(previousWidth.Type, (float)width); document.Height = new SvgUnit(previousHeight.Type, (float)height);
            if (viewBox.Width > 0 && viewBox.Height > 0 && previousWidth.Value > 0 && previousHeight.Value > 0)
                document.ViewBox = new SvgViewBox(viewBox.MinX, viewBox.MinY, viewBox.Width * (float)width / previousWidth.Value, viewBox.Height * (float)height / previousHeight.Value);
        }));
    }
}
