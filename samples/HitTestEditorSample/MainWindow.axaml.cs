using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Threading;
using System.Reflection;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using SK = SkiaSharp;
using Shim = ShimSkiaSharp;
using Svg;
using Svg.Skia;
using Svg.Model.Drawables;
using Svg.Model.Services;

namespace HitTestEditorSample;

public partial class MainWindow : Window
{
    private DrawableBase? _selectedDrawable;
    private SvgVisualElement? _selectedElement;
    private SvgDocument? _document;

    private ObservableCollection<PropertyEntry> Properties { get; } = new();
    private ObservableCollection<SvgNode> Nodes { get; } = new();
    private readonly HashSet<string> _expandedIds = new();

    private readonly SK.SKColor _boundsColor = SK.SKColors.Red;

    private bool _isDragging;
    private Shim.SKPoint _dragStart;
    private SvgVisualElement? _dragElement;
    private List<(PropertyInfo Prop, SvgUnit Unit, char Axis)>? _dragProps;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        DataContext = this;
        LoadDocument("Assets/__tiger.svg");
    }

    private void LoadDocument(string path)
    {
        // try load from Avalonia resources first
        var uri = new Uri($"avares://HitTestEditorSample/{path}");

        if (SvgView.SkSvg is { } skSvg)
            skSvg.OnDraw -= SvgView_OnDraw;

        if (AssetLoader.Exists(uri))
        {
            using var stream = AssetLoader.Open(uri);
            _document = SvgService.Open(stream);
            SvgView.Path = uri.ToString();
        }
        else if (File.Exists(path))
        {
            _document = SvgService.Open(path);
            SvgView.Path = path;
        }
        else
        {
            _document = null;
            SvgView.Path = null;
            Console.WriteLine($"SVG document '{path}' not found.");
        }

        if (SvgView.SkSvg is { } skSvg2)
            skSvg2.OnDraw += SvgView_OnDraw;

        SaveExpandedNodes();
        BuildTree();
    }

    private async void OpenMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filters = new()
            {
                new FileDialogFilter { Name = "Svg", Extensions = { "svg", "svgz" } },
                new FileDialogFilter { Name = "All", Extensions = { "*" } }
            }
        };
        var result = await dialog.ShowAsync(this);
        var file = result?.FirstOrDefault();
        if (!string.IsNullOrEmpty(file))
        {
            LoadDocument(file);
            foreach (var entry in Properties)
                entry.PropertyChanged -= PropertyEntryOnPropertyChanged;
            Properties.Clear();
            _selectedDrawable = null;
            _selectedElement = null;
            SvgView.InvalidateVisual();
        }
    }

    private async void SaveMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null)
            return;
        var dialog = new SaveFileDialog
        {
            Filters = new()
            {
                new FileDialogFilter { Name = "Svg", Extensions = { "svg" } },
                new FileDialogFilter { Name = "All", Extensions = { "*" } }
            },
            DefaultExtension = "svg"
        };
        var path = await dialog.ShowAsync(this);
        if (!string.IsNullOrEmpty(path))
        {
            _document.Write(path);
        }
    }

    private void LoadProperties(SvgVisualElement element)
    {
        foreach (var e in Properties)
            e.PropertyChanged -= PropertyEntryOnPropertyChanged;
        Properties.Clear();
        var props = element.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<SvgAttributeAttribute>() != null);
        foreach (var prop in props)
        {
            var converter = TypeDescriptor.GetConverter(prop.PropertyType);
            var value = prop.GetValue(element);
            string? str = null;
            if (value is not null)
            {
                try
                {
                    str = converter.ConvertToInvariantString(value);
                }
                catch (Exception)
                {
                    str = value.ToString();
                }
            }
            var entry = new PropertyEntry(prop.GetCustomAttribute<SvgAttributeAttribute>()!.Name, prop, str);
            entry.PropertyChanged += PropertyEntryOnPropertyChanged;
            Properties.Add(entry);
        }
    }

    private void SvgView_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetPosition(SvgView);
        if (SvgView.SkSvg is { } skSvg && SvgView.TryGetPicturePoint(point, out var pp))
        {
            _selectedDrawable = skSvg.HitTestDrawables(pp).FirstOrDefault();
            _selectedElement = skSvg.HitTestElements(pp).OfType<SvgVisualElement>().FirstOrDefault();
            if (_selectedElement is { })
            {
                LoadProperties(_selectedElement);
                SelectNodeFromElement(_selectedElement);
                TryStartDrag(_selectedElement, pp, e);
            }
            UpdateSelectedDrawable();
            SvgView.InvalidateVisual();
        }
    }

    private void SvgView_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _dragElement is null || _dragProps is null)
            return;
        var point = e.GetPosition(SvgView);
        if (SvgView.TryGetPicturePoint(point, out var pp))
        {
            var dx = pp.X - _dragStart.X;
            var dy = pp.Y - _dragStart.Y;
            foreach (var (Prop, Unit, Axis) in _dragProps)
            {
                var delta = Axis == 'x' ? dx : dy;
                Prop.SetValue(_dragElement, new SvgUnit(Unit.Type, Unit.Value + delta));
            }
            SvgView.SkSvg!.FromSvgDocument(_document);
            UpdateSelectedDrawable();
            SvgView.InvalidateVisual();
        }
    }

    private void SvgView_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging)
            return;
        _isDragging = false;
        if (_dragElement is { })
        {
            LoadProperties(_dragElement);
        }
        e.Pointer.Capture(null);
    }

    private void ApplyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedElement is null || _document is null)
            return;
        foreach (var entry in Properties)
        {
            entry.Apply(_selectedElement);
        }
        SvgView.SkSvg!.FromSvgDocument(_document);
        UpdateSelectedDrawable();
        SaveExpandedNodes();
        foreach (var entry in Properties)
            entry.PropertyChanged -= PropertyEntryOnPropertyChanged;
        Properties.Clear();
        BuildTree();
        SelectNodeFromElement(_selectedElement);
        SvgView.InvalidateVisual();
    }

    private void PropertyEntryOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is PropertyEntry entry && _selectedElement is { } && _document is { })
        {
            entry.Apply(_selectedElement);
            SvgView.SkSvg!.FromSvgDocument(_document);
            UpdateSelectedDrawable();
            SvgView.InvalidateVisual();
        }
    }

    private void TryStartDrag(SvgVisualElement element, Shim.SKPoint start, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(SvgView).Properties.IsLeftButtonPressed)
            return;

        if (!GetDragProperties(element, out var props))
            return;

        _dragProps = props;
        _isDragging = true;
        _dragStart = start;
        _dragElement = element;
        args.Pointer.Capture(SvgView);
    }

    private static bool GetDragProperties(SvgVisualElement element, out List<(PropertyInfo Prop, SvgUnit Unit, char Axis)> props)
    {
        var list = new List<(PropertyInfo Prop, SvgUnit Unit, char Axis)>();
        switch (element)
        {
            case SvgRectangle rect:
            case Svg.SvgImage img:
            case SvgUse use:
                Add("X", 'x');
                Add("Y", 'y');
                break;
            case SvgCircle circle:
            case SvgEllipse ellipse:
                Add("CenterX", 'x');
                Add("CenterY", 'y');
                break;
            case SvgLine line:
                Add("StartX", 'x');
                Add("StartY", 'y');
                Add("EndX", 'x');
                Add("EndY", 'y');
                break;
            default:
                props = null!;
                return false;
        }
        props = list;
        return props.Count > 0;

        void Add(string name, char axis)
        {
            var p = element.GetType().GetProperty(name);
            if (p != null && p.PropertyType == typeof(SvgUnit))
            {
                var unit = (SvgUnit)p.GetValue(element)!;
                list.Add((p, unit, axis));
            }
        }
    }

    private void SvgView_OnDraw(object? sender, SKSvgDrawEventArgs e)
    {
        if (_selectedDrawable is null)
            return;
        using var paint = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Stroke,
            Color = _boundsColor
        };
        var rect = SvgView.SkSvg!.SkiaModel.ToSKRect(_selectedDrawable.TransformedBounds);
        e.Canvas.DrawRect(rect, paint);
    }

    public class PropertyEntry : INotifyPropertyChanged
    {
        public string Name { get; }
        public PropertyInfo Property { get; }
        private string? _value;
        public string? Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }

        private readonly TypeConverter _converter;

        public PropertyEntry(string name, PropertyInfo property, string? value)
        {
            Name = name;
            Property = property;
            _converter = TypeDescriptor.GetConverter(property.PropertyType);
            _value = value;
        }

        public void Apply(object target)
        {
            try
            {
                var converted = _converter.ConvertFromInvariantString(Value);
                Property.SetValue(target, converted);
            }
            catch
            {
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private void BuildTree()
    {
        Nodes.Clear();
        if (_document is { })
            Nodes.Add(CreateNode(_document, null));
        Dispatcher.UIThread.Post(() =>
        {
            RestoreExpandedNodes();
            if (_selectedElement is { } sel && Nodes.Count > 0)
            {
                var node = FindNode(Nodes[0], sel);
                if (node is not null)
                {
                    AddAncestorIds(node);
                    RestoreExpandedNodes();
                    DocumentTree.SelectedItem = node;
                    DocumentTree.ScrollIntoView(node);
                }
            }
            if (_expandedIds.Count == 0)
                ExpandAll();
            if (_selectedElement is { })
                UpdateSelectedDrawable();
            SvgView.InvalidateVisual();
        });
    }

    private SvgNode CreateNode(SvgElement element, SvgNode? parent = null)
    {
        var node = new SvgNode(element, parent);
        foreach (var child in element.Children.OfType<SvgElement>())
            node.Children.Add(CreateNode(child, node));
        return node;
    }

    private DrawableBase? FindDrawable(DrawableBase drawable, SvgElement element)
    {
        if (drawable.Element == element)
            return drawable;
        if (drawable is DrawableContainer container)
        {
            foreach (var child in container.ChildrenDrawables)
            {
                var found = FindDrawable(child, element);
                if (found is { })
                    return found;
            }
        }
        return null;
    }

    private void DocumentTree_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is SvgNode node)
        {
            _selectedElement = node.Element as SvgVisualElement;
            UpdateSelectedDrawable();
            if (_selectedElement is { })
            {
                LoadProperties(_selectedElement);
            }
            else
            {
                foreach (var entry in Properties)
                    entry.PropertyChanged -= PropertyEntryOnPropertyChanged;
                Properties.Clear();
            }
            DocumentTree.ScrollIntoView(node);
            SvgView.InvalidateVisual();
        }
    }

    private void UpdateSelectedDrawable()
    {
        if (_selectedElement is { } element && SvgView.SkSvg?.Drawable is DrawableBase drawable)
            _selectedDrawable = FindDrawable(drawable, element);
        else
            _selectedDrawable = null;
    }

    private SvgNode? FindNode(SvgNode node, SvgElement element)
    {
        if (node.Element == element)
            return node;
        foreach (var child in node.Children)
        {
            var found = FindNode(child, element);
            if (found is not null)
                return found;
        }
        return null;
    }

    private void SelectNodeFromElement(SvgElement element)
    {
        if (Nodes.Count == 0)
            return;
        var node = FindNode(Nodes[0], element);
        if (node is not null)
        {
            AddAncestorIds(node);
            RestoreExpandedNodes();
            DocumentTree.SelectedItem = node;
            DocumentTree.ScrollIntoView(node);
        }
    }

    private void ExpandAll()
    {
        if (DocumentTree.ContainerFromIndex(0) is TreeViewItem item)
            DocumentTree.ExpandSubTree(item);
    }

    private void SaveExpandedNodes()
    {
        _expandedIds.Clear();
        if (DocumentTree.ContainerFromIndex(0) is TreeViewItem item && Nodes.Count > 0)
            SaveExpandedNodes(item, Nodes[0]);
    }

    private void SaveExpandedNodes(TreeViewItem item, SvgNode node)
    {
        if (!string.IsNullOrEmpty(node.Element.ID) && item.IsExpanded)
            _expandedIds.Add(node.Element.ID);
        for (var i = 0; i < node.Children.Count; i++)
        {
            if (item.ItemContainerGenerator.ContainerFromIndex(i) is TreeViewItem child)
                SaveExpandedNodes(child, node.Children[i]);
        }
    }

    private void RestoreExpandedNodes()
    {
        if (_expandedIds.Count == 0)
            return;
        if (DocumentTree.ContainerFromIndex(0) is TreeViewItem item && Nodes.Count > 0)
            RestoreExpandedNodes(item, Nodes[0]);
    }

    private void RestoreExpandedNodes(TreeViewItem item, SvgNode node)
    {
        if (!string.IsNullOrEmpty(node.Element.ID) && _expandedIds.Contains(node.Element.ID))
            item.IsExpanded = true;
        for (var i = 0; i < node.Children.Count; i++)
        {
            if (item.ItemContainerGenerator.ContainerFromIndex(i) is TreeViewItem child)
                RestoreExpandedNodes(child, node.Children[i]);
        }
    }

    private void AddAncestorIds(SvgNode node)
    {
        var parent = node.Parent;
        while (parent is not null)
        {
            if (!string.IsNullOrEmpty(parent.Element.ID))
                _expandedIds.Add(parent.Element.ID);
            parent = parent.Parent;
        }
    }
}

public class SvgNode
{
    public SvgElement Element { get; }
    public ObservableCollection<SvgNode> Children { get; } = new();
    public SvgNode? Parent { get; }
    public string Label { get; }

    public SvgNode(SvgElement element, SvgNode? parent)
    {
        Element = element;
        Parent = parent;
        var name = element.GetType().Name;
        Label = string.IsNullOrEmpty(element.ID)
            ? name
            : $"{name} ({element.ID})";
    }
}
