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
using Svg.Transforms;
using Windows.UI;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private readonly Dictionary<TextBox, Action> _inspectorCommits = new();
    private bool _committingInspector;

    // Flush the current document's fields before selection, tab, file or command boundaries.
    // Validation failures propagate so the requested boundary cannot silently discard input.
    private void CommitInspectorEdits()
    {
        if (_refreshing || _committingInspector || _disposed)
            return;
        _committingInspector = true;
        try
        {
            foreach (var commit in _inspectorCommits.Values.ToArray())
                commit();
            Focus(FocusState.Programmatic); // Commits the focused NumberBox before selection changes.
        }
        finally
        {
            _committingInspector = false;
        }
    }

    private sealed class ElementItem
    {
        public ElementItem(SvgElement element) => Element = element;
        public SvgElement Element { get; }
        public override string ToString()
        {
            var type = SvgElementInfo.GetElementName(Element.GetType());
            var name = string.IsNullOrEmpty(Element.ID) ? type : Element.ID;
            var hidden = Element.Display == "none" || Element.Visibility == "hidden";
            var locked = SvgEditorController.IsLocked(Element);
            return $"{(hidden ? "◌ " : "")}{(locked ? "▣ " : "")}{name}";
        }
    }

    private void RefreshOutline()
    {
        if (_outlineDragging)
            return;
        _rebuildingOutline = true;
        try
        {
            var expanded = new HashSet<SvgElement>();
            void Remember(TreeViewNode node)
            {
                if (node.IsExpanded && node.Content is ElementItem item)
                    expanded.Add(item.Element);
                foreach (var child in node.Children)
                    Remember(child);
            }
            foreach (var root in DocumentOutline.RootNodes)
                Remember(root);
            DocumentOutline.RootNodes.Clear();
            if (Controller?.Document is not { } document)
                return;
            var query = OutlineSearch.Text.Trim();
            TreeViewNode? Create(SvgElement element, int depth)
            {
                var item = new ElementItem(element);
                var node = new TreeViewNode { Content = item, IsExpanded = expanded.Contains(element) || depth == 0 || query.Length > 0 };
                foreach (var child in element.Children.Reverse())
                    if (Create(child, depth + 1) is { } childNode)
                        node.Children.Add(childNode);
                if (query.Length > 0 && node.Children.Count == 0 && !item.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
                    && !SvgElementInfo.GetElementName(element.GetType()).Contains(query, StringComparison.OrdinalIgnoreCase))
                    return null;
                return node;
            }
            foreach (var element in document.Children.Reverse())
                if (Create(element, 0) is { } node)
                    DocumentOutline.RootNodes.Add(node);
            foreach (var node in EnumerateOutlineNodes(DocumentOutline.RootNodes))
                if (node.Content is ElementItem { Element: SvgVisualElement visual } && Controller!.SelectedElements.Contains(visual))
                    DocumentOutline.SelectedNodes.Add(node);
        }
        finally
        {
            _rebuildingOutline = false;
        }
    }

    private void OutlineSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_refreshing)
            RefreshOutline();
    }

    private void DocumentOutline_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        var item = args.InvokedItem as ElementItem ?? (args.InvokedItem as TreeViewNode)?.Content as ElementItem;
        if (item is null || Controller is not { } controller)
            return;
        ApplySafely(() =>
        {
            CommitInspectorEdits();
            Canvas.CancelGesture();
            var element = ReferenceEquals(item.Element.OwnerDocument, controller.Document)
                ? item.Element : controller.Document?.GetElementById(item.Element.ID);
            if (element is null)
                return;
            if (element is SvgVisualElement visual)
            {
                if (!controller.SelectedElements.Contains(visual))
                    controller.Select(visual, IsDown(Windows.System.VirtualKey.Control) || IsDown(Windows.System.VirtualKey.Shift), allowLocked: true);
            }
            else
                controller.ClearSelection();
            _inspectedElement = element;
            RefreshInspector();
        });
    }

    private void RefreshResources()
    {
        var resources = Controller?.Document?.Descendants()
            .Where(element => element is SvgGradientServer or SvgPatternServer or SvgSymbol or SvgClipPath || element.GetType().Name == "SvgFilter")
            .Select(element => new ElementItem(element)).ToList();
        ResourceList.ItemsSource = resources;
    }

    private void ResourceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || ResourceList.SelectedItem is not ElementItem item || Controller is not { } controller)
            return;
        ApplySafely(() =>
        {
            CommitInspectorEdits();
            if (item.Element is SvgGradientServer or SvgPatternServer && controller.SelectedElements.Count > 0)
                controller.SetFill($"url(#{item.Element.ID})");
            else if (item.Element is SvgSymbol symbol)
            {
                controller.AddElement(new SvgUse
                {
                    ReferencedElement = new Uri("#" + symbol.ID, UriKind.Relative),
                    X = new SvgUnit(40),
                    Y = new SvgUnit(40),
                    Width = new SvgUnit(200),
                    Height = new SvgUnit(200)
                }, "Insert symbol");
            }
            else
            {
                controller.ClearSelection();
                _inspectedElement = item.Element;
                RefreshInspector();
            }
        });
    }

    private void RefreshInspector()
    {
        if (Controller is not { } controller)
            return;
        var wasRefreshing = _refreshing;
        _refreshing = true;
        try
        {
            _inspectorCommits.Clear();
            var selected = controller.SelectedElements;
            var element = _inspectedElement ?? controller.Document;
            InspectorContent.Children.Clear();
            if (element is null)
                return;
            SelectionBadge.Text = selected.Count > 1 ? $"{selected.Count} objects" : SvgElementInfo.GetElementName(element.GetType());
            SelectionName.Text = selected.Count > 1 ? "Multiple selection" : string.IsNullOrEmpty(element.ID) ? (element is SvgDocument ? "Artboard" : SvgElementInfo.GetElementName(element.GetType())) : element.ID;
            if (selected.Any(SvgEditorController.IsEditLocked))
            {
                AddSection("LOCKED");
                InspectorContent.Children.Add(new TextBlock
                {
                    Text = "Unlock this object or its parent layer to edit it.",
                    TextWrapping = TextWrapping.Wrap
                });
                InspectorContent.Children.Add(ActionButton("unlock", "Unlock selection"));
                RefreshAttributeFields(element);
                return;
            }
            if (selected.Count == 0 && element is SvgDocument document)
            {
                AddSection("CANVAS");
                var dimensions = new Grid { ColumnSpacing = 8 };
                dimensions.ColumnDefinitions.Add(new ColumnDefinition());
                dimensions.ColumnDefinitions.Add(new ColumnDefinition());
                var width = NumberInput("Width", document.Width.Value, 1, 1000000);
                var height = NumberInput("Height", document.Height.Value, 1, 1000000);
                Grid.SetColumn(height, 1);
                dimensions.Children.Add(width);
                dimensions.Children.Add(height);
                width.ValueChanged += (_, _) => ChangeArtboard(width.Value, height.Value);
                height.ValueChanged += (_, _) => ChangeArtboard(width.Value, height.Value);
                InspectorContent.Children.Add(dimensions);
                InspectorContent.Children.Add(new TextBlock
                {
                    Text = "Select an object to edit its appearance. Every SVG element and resource is available in the document outline.",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Opacity = 0.65
                });
            }
            else if (selected.Count > 0)
            {
                if (controller.SelectionBounds is { } bounds)
                {
                    AddSection("TRANSFORM");
                    var fields = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
                    fields.ColumnDefinitions.Add(new ColumnDefinition());
                    fields.ColumnDefinitions.Add(new ColumnDefinition());
                    fields.RowDefinitions.Add(new RowDefinition());
                    fields.RowDefinitions.Add(new RowDefinition());
                    var x = NumberInput("X", bounds.Left, -1000000, 1000000);
                    var y = NumberInput("Y", bounds.Top, -1000000, 1000000);
                    var width = NumberInput("W", bounds.Width, 0.001, 1000000);
                    var height = NumberInput("H", bounds.Height, 0.001, 1000000);
                    x.ValueChanged += (_, _) => ApplySafely(() => controller.MoveSelection((float)x.Value - bounds.Left, 0));
                    y.ValueChanged += (_, _) => ApplySafely(() => controller.MoveSelection(0, (float)y.Value - bounds.Top));
                    width.ValueChanged += (_, _) => ApplySafely(() => controller.TransformSelection(SKMatrix.CreateScale((float)width.Value / Math.Max(bounds.Width, 0.001f), 1, bounds.Left, bounds.Top)));
                    height.ValueChanged += (_, _) => ApplySafely(() => controller.TransformSelection(SKMatrix.CreateScale(1, (float)height.Value / Math.Max(bounds.Height, 0.001f), bounds.Left, bounds.Top)));
                    fields.Children.Add(x);
                    fields.Children.Add(y);
                    fields.Children.Add(width);
                    fields.Children.Add(height);
                    Grid.SetColumn(y, 1);
                    Grid.SetRow(width, 1);
                    Grid.SetRow(height, 1);
                    Grid.SetColumn(height, 1);
                    InspectorContent.Children.Add(fields);
                }
                AddSection("APPEARANCE");
                var first = selected[0];
                InspectorContent.Children.Add(TextField("Fill", PaintText(first.Fill), value => controller.SetFill(value)));
                InspectorContent.Children.Add(TextField("Stroke", PaintText(first.Stroke), value => controller.SetStroke(value)));
                var stroke = NumberInput("Stroke width", first.StrokeWidth.Value, 0, 10000);
                stroke.ValueChanged += (_, _) => ApplySafely(() => controller.SetStrokeWidth((float)stroke.Value));
                InspectorContent.Children.Add(stroke);
                var opacity = NumberInput("Opacity %", first.Opacity * 100, 0, 100);
                opacity.ValueChanged += (_, _) => ApplySafely(() => controller.SetOpacity((float)opacity.Value / 100));
                InspectorContent.Children.Add(opacity);
                var swatches = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                foreach (var color in new[] { "#7061dc", "#92b8a8", "#efb694", "#f7f2e9", "#202333", "none" })
                {
                    var button = new Button
                    {
                        Width = 29,
                        Height = 29,
                        MinWidth = 0,
                        Padding = new Thickness(0),
                        CornerRadius = new CornerRadius(8),
                        Background = color == "none" ? new SolidColorBrush(Microsoft.UI.Colors.Transparent) : BrushFromHex(color),
                        Content = color == "none" ? "∅" : null
                    };
                    ToolTipService.SetToolTip(button, color);
                    AutomationProperties.SetName(button, "Fill " + color);
                    button.Click += (_, _) => ApplySafely(() => controller.SetFill(color));
                    swatches.Children.Add(button);
                }
                InspectorContent.Children.Add(swatches);
                if (first is SvgTextBase text)
                {
                    AddSection("TYPOGRAPHY");
                    InspectorContent.Children.Add(TextField("Text", text.Text ?? string.Empty, controller.SetText, multiline: true));
                    InspectorContent.Children.Add(TextField("Font family", text.FontFamily ?? "sans-serif", value => controller.SetAttribute("font-family", value)));
                    InspectorContent.Children.Add(TextField("Font size", text.FontSize.ToString(), value => controller.SetAttribute("font-size", value)));
                }
                AddSection("QUICK ACTIONS");
                var actions = new Grid { ColumnSpacing = 6, RowSpacing = 6 };
                actions.ColumnDefinitions.Add(new ColumnDefinition());
                actions.ColumnDefinitions.Add(new ColumnDefinition());
                actions.RowDefinitions.Add(new RowDefinition());
                actions.RowDefinitions.Add(new RowDefinition());
                var commands = new[] { ("group", "Group"), ("duplicate", "Duplicate"), ("lock", "Lock"), ("hide", "Hide") };
                for (var i = 0; i < commands.Length; i++)
                {
                    var button = ActionButton(commands[i].Item1, commands[i].Item2);
                    Grid.SetRow(button, i / 2);
                    Grid.SetColumn(button, i % 2);
                    actions.Children.Add(button);
                }
                InspectorContent.Children.Add(actions);
            }
            RefreshAttributeFields(element);
        }
        finally
        {
            _refreshing = wasRefreshing;
        }
    }

    private void AddSection(string title) => InspectorContent.Children.Add(new TextBlock { Text = title, Style = (Style)Resources["SectionLabel"] });

    private static string PaintText(SvgPaintServer? paint)
    {
        if (paint is null)
            return "none";
        try
        { return TypeDescriptor.GetConverter(typeof(SvgPaintServer)).ConvertToInvariantString(paint) ?? "none"; }
        catch { return paint.ToString() ?? "none"; }
    }

    private void RefreshAttributeFields(SvgElement element)
    {
        foreach (var field in AttributeFields.Children.OfType<TextBox>())
            _inspectorCommits.Remove(field);
        AttributeFields.Children.Clear();
        var filter = AttributeSearch.Text?.Trim() ?? string.Empty;
        foreach (var property in element.GetType().GetProperties().Where(property => property.CanRead && property.CanWrite))
        {
            var attribute = property.GetCustomAttribute<SvgAttributeAttribute>();
            if (attribute is null || !attribute.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;
            var converter = TypeDescriptor.GetConverter(property.PropertyType);
            if (!converter.CanConvertFrom(typeof(string)) || !converter.CanConvertTo(typeof(string)))
                continue;
            string value;
            try
            { value = converter.ConvertToInvariantString(property.GetValue(element)) ?? string.Empty; }
            catch { continue; }
            AttributeFields.Children.Add(TextField(attribute.Name, value, text =>
            {
                if (attribute.Name == "id")
                {
                    Controller!.RenameElementId(element, text);
                    return;
                }
                var converted = converter.ConvertFromInvariantString(text);
                Controller!.Execute($"Set {attribute.Name}", () => property.SetValue(element, converted));
            }));
        }
        foreach (var pair in element.CustomAttributes.ToArray().Where(pair => pair.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            AttributeFields.Children.Add(TextField(pair.Key, pair.Value, value => Controller!.Execute($"Set {pair.Key}", () => element.CustomAttributes[pair.Key] = value)));
        foreach (var field in AttributeFields.Children.OfType<Control>())
            field.IsEnabled = !SvgEditorController.IsEditLocked(element);
    }

    private void AttributeSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_refreshing && (_inspectedElement ?? Controller?.Document) is { } element)
            RefreshAttributeFields(element);
    }

    private TextBox TextField(string label, string value, Action<string> apply, bool multiline = false)
    {
        var field = new TextBox { Header = label, Text = value, AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap };
        AutomationProperties.SetName(field, label);
        void Commit()
        {
            if (_refreshing || !field.IsEnabled || field.Text == value)
                return;
            var updated = field.Text;
            apply(updated);
            value = updated;
        }
        _inspectorCommits.Add(field, Commit);
        field.LostFocus += (_, _) =>
        {
            if (!_committingInspector)
                ApplySafely(Commit);
        };
        field.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter && !multiline)
            {
                ApplySafely(Commit);
                args.Handled = true;
            }
        };
        return field;
    }

    private static NumberBox NumberInput(string label, double value, double minimum, double maximum) => new()
    {
        Header = label,
        Value = double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : minimum,
        Minimum = minimum,
        Maximum = maximum,
        SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        SmallChange = 1,
        LargeChange = 10,
        NumberFormatter = new EditorNumberFormatter(),
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    private Button ActionButton(string command, string label)
    {
        var button = new Button { Content = label, Tag = command, Style = (Style)Resources["CompactButton"], HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += Command_Click;
        return button;
    }

    private static SolidColorBrush BrushFromHex(string hex)
    {
        var color = SKColor.Parse(hex);
        return new SolidColorBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
    }

    private void ApplySafely(Action action)
    {
        if (_refreshing)
            return;
        if (_committingInspector)
        {
            action();
            return;
        }
        try
        { action(); }
        catch (Exception exception) { _ = ShowErrorAsync(exception.Message); }
    }

    private void ChangeArtboard(double width, double height)
    {
        if (Controller?.Document is not { } document || !double.IsFinite(width) || !double.IsFinite(height)
            || width <= 0 || height <= 0 || (document.Width.Value == (float)width && document.Height.Value == (float)height))
            return;
        ApplySafely(() => Controller.Execute("Resize artboard", () =>
        {
            var viewBox = document.ViewBox;
            var previousWidth = document.Width;
            var previousHeight = document.Height;
            document.Width = new SvgUnit(previousWidth.Type, (float)width);
            document.Height = new SvgUnit(previousHeight.Type, (float)height);
            if (viewBox.Width > 0 && viewBox.Height > 0 && previousWidth.Value > 0 && previousHeight.Value > 0)
                document.ViewBox = new SvgViewBox(viewBox.MinX, viewBox.MinY,
                    viewBox.Width * (float)width / previousWidth.Value,
                    viewBox.Height * (float)height / previousHeight.Value);
        }));
    }
}
