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
    private readonly Dictionary<string, StudioSection> _inspectorSections = new(StringComparer.Ordinal);
    private InspectorNumber? _inspectorDocumentWidth, _inspectorDocumentHeight;
    private InspectorNumber? _inspectorX, _inspectorY, _inspectorWidth, _inspectorHeight, _inspectorWeight, _inspectorOpacity;
    private StudioPaintChip? _inspectorFill, _inspectorStroke;
    private SvgEditorController? _inspectorOwner;
    private SvgDocument? _inspectorOwnerDocument;
    private SvgEditorHierarchyPresentation? _outlinePresentation;
    private SvgEditorResourcePresentation? _resourcePresentation;
    private SvgElement? _lastAttributeElement;
    private SvgDocument? _lastAttributeDocument;
    private long _lastAttributeRevision = -1;

    private sealed class InspectorNumber
    {
        public InspectorNumber(NumberBox field) => Field = field;
        public NumberBox Field { get; }
        public SvgEditorSelectionContext? Target { get; set; }
    }

    private void CommitInspectorEdits()
    {
        if (_refreshing || _committingInspector || _disposed)
            return;
        _committingInspector = true;
        try
        {
            CommitStudioEdits();
            foreach (var commit in _inspectorCommits.Values.ToArray())
                commit();
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
            return $"{(Element.Display == "none" || Element.Visibility == "hidden" ? "[hidden] " : "")}{(SvgEditorController.IsLocked(Element) ? "[locked] " : "")}{name}";
        }
    }
    private void SynchronizeInspectorOwner(SvgEditorController controller)
    {
        if (ReferenceEquals(controller, _inspectorOwner) && ReferenceEquals(controller.Document, _inspectorOwnerDocument))
            return;
        ReleaseInspectorTargets();
        _inspectorOwner = controller;
        _inspectorOwnerDocument = controller.Document;
    }

    private void ReleaseInspectorTargets()
    {
        // Hidden controls survive document switches, but their borrowed model
        // references must not keep a closed/replaced SVG and its renderer alive.
        var wasRefreshing = _refreshing;
        _refreshing = true;
        try
        {
            _inspectorOwner = null;
            _inspectorOwnerDocument = null;
            _lastInspectorElement = _lastAttributeElement = null;
            _lastInspectorDocument = _lastAttributeDocument = null;
            _lastInspectorSelection = Array.Empty<SvgVisualElement>();
            _lastInspectorRevision = _lastAttributeRevision = _outlineRevision = _resourceRevision = -1;
            foreach (var binding in new[] { _inspectorDocumentWidth, _inspectorDocumentHeight, _inspectorX, _inspectorY,
                _inspectorWidth, _inspectorHeight, _inspectorWeight, _inspectorOpacity })
                if (binding is not null)
                    binding.Target = null;
            _inspectorCommits.Clear();
            AttributeFields.Children.Clear();
            _outlinePresentation = null;
            _resourcePresentation = null;
            DocumentOutline.RootNodes.Clear();
            ResourceList.ItemsSource = null;
        }
        finally { _refreshing = wasRefreshing; }
    }

    private void RefreshOutline()
    {
        if (_outlineDragging)
            return;
        var document = Controller?.Document;
        var query = OutlineSearch.Text.Trim();
        if (_outlinePresentation?.IsCurrent(document, query) == true)
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
            if (document is null)
                return;
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
            _outlinePresentation = SvgEditorHierarchyPresentation.Capture(document, query);
        }
        finally { _rebuildingOutline = false; }
    }
    private void OutlineSearch_TextChanged(object sender, TextChangedEventArgs args) { if (!_refreshing) RefreshOutline(); }
    private void DocumentOutline_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        var item = args.InvokedItem as ElementItem ?? (args.InvokedItem as TreeViewNode)?.Content as ElementItem;
        if (item is null || Controller is not { } controller)
            return;
        ApplySafely(() =>
        {
            CommitInspectorEdits();
            Canvas.CancelGesture();
            var element = ReferenceEquals(item.Element.OwnerDocument, controller.Document) ? item.Element : controller.Document?.GetElementById(item.Element.ID);
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
            RefreshStudioPanels();
        });
    }
    private void RefreshResources()
    {
        var document = Controller?.Document;
        if (_resourcePresentation?.IsCurrent(document) == true)
            return;
        var selected = (ResourceList.SelectedItem as ElementItem)?.Element;
        var items = document?.Descendants().Where(SvgEditorResourcePresentation.IsResource)
            .Select(element => new ElementItem(element)).ToList();
        ResourceList.ItemsSource = items;
        if (selected is not null)
            ResourceList.SelectedItem = items?.FirstOrDefault(item => ReferenceEquals(item.Element, selected));
        _resourcePresentation = SvgEditorResourcePresentation.Capture(document);
    }
    private void ResourceList_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_refreshing || ResourceList.SelectedItem is not ElementItem item || Controller is not { } controller)
            return;
        ApplySafely(() =>
        {
            CommitInspectorEdits();
            if (item.Element is SvgGradientServer or SvgPatternServer && controller.SelectedElements.Count > 0)
                controller.SetFill($"url(#{item.Element.ID})");
            else if (item.Element is SvgSymbol symbol)
                controller.AddElement(new SvgUse { ReferencedElement = new Uri("#" + symbol.ID, UriKind.Relative), X = new SvgUnit(40), Y = new SvgUnit(40), Width = new SvgUnit(200), Height = new SvgUnit(200) }, "Insert symbol");
            else
            { controller.ClearSelection(); _inspectedElement = item.Element; RefreshInspector(); }
        });
    }

    private void RefreshInspector()
    {
        if (Controller is not { } controller)
            return;
        var element = _inspectedElement ?? controller.Document;
        if (element is null)
            return;
        var wasRefreshing = _refreshing;
        _refreshing = true;
        try
        {
            if (IsStudioPanelVisible("properties"))
                RefreshInspectorProperties(controller, element);
            if (IsStudioPanelVisible("attributes") && (!ReferenceEquals(element, _lastAttributeElement)
                || !ReferenceEquals(controller.Document, _lastAttributeDocument) || _lastAttributeRevision != controller.DocumentRevision))
            {
                RefreshAttributeFields(element);
                _lastAttributeElement = element;
                _lastAttributeDocument = controller.Document;
                _lastAttributeRevision = controller.DocumentRevision;
            }
        }
        finally { _refreshing = wasRefreshing; }
    }

    private void RefreshInspectorProperties(SvgEditorController controller, SvgElement element)
    {
        var selected = controller.SelectedElements;
        var sameSelection = _lastInspectorSelection.SequenceEqual(selected);
        if (ReferenceEquals(element, _lastInspectorElement) && ReferenceEquals(controller.Document, _lastInspectorDocument)
            && _lastInspectorRevision == controller.DocumentRevision && sameSelection)
            return;

        // Keep the native inputs, their templates, section expansion, and focus alive.
        // Selection changes update values and section visibility, never rebuild a form.
        if (_inspectorSections.Count == 0)
            BuildInspectorProperties();
        var locked = selected.Any(SvgEditorController.IsEditLocked);
        var documentMode = !locked && selected.Count == 0 && element is SvgDocument;
        var selectionMode = !locked && selected.Count > 0;
        var bounds = selectionMode ? controller.SelectionBounds : null;
        ShowInspectorSection("Locked", locked);
        ShowInspectorSection("Document", documentMode);
        ShowInspectorSection("Transform", bounds.HasValue);
        ShowInspectorSection("Appearance", selectionMode);
        ShowInspectorSection("Typography", selectionMode && selected[0] is SvgTextBase);
        ShowInspectorSection("Quick actions", selectionMode);
        ShowInspectorSection("SVG resource", !locked && !documentMode && !selectionMode);
        SelectionBadge.Text = selected.Count > 1 ? $"{selected.Count} objects" : SvgElementInfo.GetElementName(element.GetType());
        SelectionName.Text = selected.Count > 1 ? "Multiple selection" : string.IsNullOrEmpty(element.ID)
            ? element is SvgDocument ? "Artboard" : SvgElementInfo.GetElementName(element.GetType()) : element.ID;
        if (documentMode && element is SvgDocument document)
        {
            SynchronizeInspectorNumber(_inspectorDocumentWidth!, document.Width.Value);
            SynchronizeInspectorNumber(_inspectorDocumentHeight!, document.Height.Value);
        }
        if (bounds is { } selectionBounds)
        {
            SynchronizeInspectorNumber(_inspectorX!, selectionBounds.Left);
            SynchronizeInspectorNumber(_inspectorY!, selectionBounds.Top);
            SynchronizeInspectorNumber(_inspectorWidth!, selectionBounds.Width);
            SynchronizeInspectorNumber(_inspectorHeight!, selectionBounds.Height);
        }
        if (selectionMode)
        {
            var first = selected[0];
            var fill = PaintText(first.Fill);
            var stroke = PaintText(first.Stroke);
            _inspectorFill!.Paint = fill;
            _inspectorFill.IsMixed = selected.Skip(1).Any(item => PaintText(item.Fill) != fill);
            _inspectorStroke!.Paint = stroke;
            _inspectorStroke.IsMixed = selected.Skip(1).Any(item => PaintText(item.Stroke) != stroke);
            SynchronizeInspectorNumber(_inspectorWeight!, first.StrokeWidth.Value, selected.Skip(1).Any(item => item.StrokeWidth != first.StrokeWidth));
            SynchronizeInspectorNumber(_inspectorOpacity!, first.Opacity * 100, selected.Skip(1).Any(item => item.Opacity != first.Opacity));
        }
        _lastInspectorElement = element;
        _lastInspectorDocument = controller.Document;
        _lastInspectorRevision = controller.DocumentRevision;
        if (!sameSelection)
            _lastInspectorSelection = selected.ToArray();
    }

    private void BuildInspectorProperties()
    {
        EditorInputTrace.Write("inspector-properties-created");
        AddSection("Locked");
        AddInspector(StudioHint("Unlock this object or its parent layer to edit it."));
        AddInspector(ActionButton("unlock", "Unlock selection"));

        AddSection("Document");
        _inspectorDocumentWidth = InspectorNumberInput("Width", "DocumentWidth", 1, 1000000,
            (_, value) => ChangeArtboard(value, _inspectorDocumentHeight!.Field.Value));
        _inspectorDocumentHeight = InspectorNumberInput("Height", "DocumentHeight", 1, 1000000,
            (_, value) => ChangeArtboard(_inspectorDocumentWidth!.Field.Value, value));
        AddInspector(StudioPair(_inspectorDocumentWidth.Field, _inspectorDocumentHeight.Field));
        AddInspector(StudioHint("Select artwork for contextual properties. Panels remain open as you move between tools and documents."));
        AddInspector(StudioPair(ActionButton("artboard", "Page setup…"), ActionButton("preflight", "Preflight…")));

        AddSection("Transform");
        _inspectorX = InspectorNumberInput("X", "X", -1000000, 1000000, (controller, value) =>
        { if (controller.SelectionBounds is { } current && current.Left != (float)value) controller.MoveSelection((float)value - current.Left, 0); });
        _inspectorY = InspectorNumberInput("Y", "Y", -1000000, 1000000, (controller, value) =>
        { if (controller.SelectionBounds is { } current && current.Top != (float)value) controller.MoveSelection(0, (float)value - current.Top); });
        _inspectorWidth = InspectorNumberInput("W", "Width", 0.001, 1000000, (controller, value) =>
        { if (controller.SelectionBounds is { Width: > 0 } current && current.Width != (float)value) controller.TransformSelection(SKMatrix.CreateScale((float)value / current.Width, 1, current.Left, current.Top)); });
        _inspectorHeight = InspectorNumberInput("H", "Height", 0.001, 1000000, (controller, value) =>
        { if (controller.SelectionBounds is { Height: > 0 } current && current.Height != (float)value) controller.TransformSelection(SKMatrix.CreateScale(1, (float)value / current.Height, current.Left, current.Top)); });
        AddInspector(StudioPair(_inspectorX.Field, _inspectorY.Field));
        AddInspector(StudioPair(_inspectorWidth.Field, _inspectorHeight.Field));
        AddInspector(ActionButton("panel-transform", "Reference point and linked scaling"));

        AddSection("Appearance");
        _inspectorFill = new StudioPaintChip { Label = "Fill" };
        _inspectorStroke = new StudioPaintChip { Label = "Stroke" };
        _inspectorFill.Click += (_, _) => OpenStudioPaint(_inspectorFill, false);
        _inspectorStroke.Click += (_, _) => OpenStudioPaint(_inspectorStroke, true);
        AddInspector(StudioPair(_inspectorFill, _inspectorStroke));
        _inspectorWeight = InspectorNumberInput("Weight", "Weight", 0, 10000, (controller, value) => controller.SetStrokeWidth((float)value));
        _inspectorOpacity = InspectorNumberInput("Opacity %", "Opacity", 0, 100, (controller, value) => controller.SetOpacity((float)value / 100));
        AddInspector(StudioPair(_inspectorWeight.Field, _inspectorOpacity.Field));
        AddInspector(StudioPair(ActionButton("panel-stroke", "Stroke options"), ActionButton("panel-appearance", "Appearance")));

        AddSection("Typography");
        AddInspector(StudioPair(ActionButton("panel-typography", "Character panel"), ActionButton("text", "Rich text…")));
        AddInspector(ActionButton("textframes", "Paragraphs and linked frames…"));
        AddSection("Quick actions");
        var actions = new StudioWrapPanel { Spacing = 5 };
        foreach (var command in new[] { ("group", "Group"), ("ungroup", "Ungroup"), ("duplicate", "Duplicate"), ("lock", "Lock"), ("hide", "Hide"), ("panel-geometry", "Geometry") })
            actions.Children.Add(ActionButton(command.Item1, command.Item2));
        AddInspector(actions);
        AddSection("SVG resource");
        AddInspector(StudioHint("Edit the selected resource through the SVG attributes panel."));
        AddInspector(ActionButton("panel-attributes", "Open SVG attributes"));
    }

    private InspectorNumber InspectorNumberInput(string label, string id, double minimum, double maximum, Action<SvgEditorController, double> apply)
    {
        var binding = new InspectorNumber(NumberInput(label, minimum, minimum, maximum));
        AutomationProperties.SetAutomationId(binding.Field, "Inspector" + id);
        binding.Field.ValueChanged += (_, args) =>
        {
            if (_refreshing || !double.IsFinite(args.NewValue) || !args.NewValue.Equals(binding.Field.Value) || !binding.Field.IsEnabled)
                return;
            ApplySafely(() =>
            {
                if (_disposed || binding.Target is null)
                    return;
                binding.Target.Validate(Controller);
                apply(Controller!, args.NewValue);
            });
        };
        binding.Field.LostFocus += (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed || InspectorFieldHasFocus(binding.Field))
                return;
            _lastInspectorRevision = -1;
            RefreshWorkspace();
        });
        return binding;
    }

    private void SynchronizeInspectorNumber(InspectorNumber binding, double value, bool mixed = false)
    {
        // Native NumberBox commits on focus loss. Preserve its draft and its original
        // target until that boundary instead of silently retargeting a retained field.
        if (InspectorFieldHasFocus(binding.Field))
            return;
        binding.Field.PlaceholderText = mixed ? "Mixed" : string.Empty;
        var displayed = mixed ? double.NaN : double.IsFinite(value) ? Math.Clamp(value, binding.Field.Minimum, binding.Field.Maximum) : binding.Field.Minimum;
        if (!binding.Field.Value.Equals(displayed))
            binding.Field.Value = displayed;
        if (binding.Target?.IsCurrent(Controller) != true && Controller is { } controller)
            binding.Target = new SvgEditorSelectionContext(controller);
    }

    private static bool InspectorFieldHasFocus(FrameworkElement field)
    {
        if (field.XamlRoot is not { } root)
            return false;
        for (var current = FocusManager.GetFocusedElement(root) as DependencyObject; current is not null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, field))
                return true;
        return false;
    }

    private void ShowInspectorSection(string name, bool visible)
        => _inspectorSections[name].Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    private void AddSection(string title)
    {
        _inspectorSection = new StackPanel { Spacing = 8 };
        var section = new StudioSection { Header = title, Content = _inspectorSection, Visibility = Visibility.Collapsed };
        _inspectorSections.Add(title, section);
        InspectorContent.Children.Add(section);
    }
    private void AddInspector(UIElement element) => (_inspectorSection ?? InspectorContent).Children.Add(element);
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
        var owner = Controller!;
        var document = owner.Document;
        void ValidateTarget()
        {
            if (_disposed || !ReferenceEquals(owner, Controller) || !ReferenceEquals(document, owner.Document)
                || !ReferenceEquals(element, _inspectedElement ?? Controller?.Document))
                throw new InvalidOperationException("The inspected object changed while this attribute was being edited. Reopen the attributes panel before applying it.");
            if (SvgEditorController.IsEditLocked(element))
                throw new InvalidOperationException("Unlock this object or its parent layer before editing its attributes.");
        }
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
                ValidateTarget();
                if (attribute.Name == "id")
                { owner.RenameElementId(element, text); return; }
                var converted = converter.ConvertFromInvariantString(text);
                owner.Execute($"Set {attribute.Name}", () => property.SetValue(element, converted));
            }));
        }
        foreach (var pair in element.CustomAttributes.ToArray().Where(pair => pair.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            AttributeFields.Children.Add(TextField(pair.Key, pair.Value, value =>
            { ValidateTarget(); owner.Execute($"Set {pair.Key}", () => element.CustomAttributes[pair.Key] = value); }));
        foreach (var field in AttributeFields.Children.OfType<Control>())
            field.IsEnabled = !SvgEditorController.IsEditLocked(element);
    }
    private void AttributeSearch_TextChanged(object sender, TextChangedEventArgs args)
    { if (!_refreshing && (_inspectedElement ?? Controller?.Document) is { } element) RefreshAttributeFields(element); }
    private TextBox TextField(string label, string value, Action<string> apply, bool multiline = false)
    {
        var field = new TextBox { Header = label, AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, Text = value };
        var baseline = field.Text;
        AutomationProperties.SetName(field, label);
        void Commit()
        {
            if (_refreshing || !field.IsEnabled || field.Text == baseline)
                return;
            var updated = field.Text;
            apply(updated);
            baseline = updated;
        }
        _inspectorCommits.Add(field, Commit);
        field.LostFocus += (_, _) => { if (!_committingInspector) ApplySafely(Commit); };
        field.KeyDown += (_, args) => { if (args.Key == Windows.System.VirtualKey.Enter && !multiline) { ApplySafely(Commit); args.Handled = true; } };
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
        { action(); return; }
        try
        { action(); }
        catch (Exception exception) { _ = ShowErrorAsync(exception.Message); }
    }
    private void ChangeArtboard(double width, double height)
    {
        if (Controller?.Document is not { } document || !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0
            || document.Width.Value == (float)width && document.Height.Value == (float)height)
            return;
        ApplySafely(() => Controller.Execute("Resize artboard", () =>
        {
            var viewBox = document.ViewBox;
            var previousWidth = document.Width;
            var previousHeight = document.Height;
            document.Width = new SvgUnit(previousWidth.Type, (float)width);
            document.Height = new SvgUnit(previousHeight.Type, (float)height);
            if (viewBox.Width > 0 && viewBox.Height > 0 && previousWidth.Value > 0 && previousHeight.Value > 0)
                document.ViewBox = new SvgViewBox(viewBox.MinX, viewBox.MinY, viewBox.Width * (float)width / previousWidth.Value, viewBox.Height * (float)height / previousHeight.Value);
        }));
    }
}
