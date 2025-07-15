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
using Svg.Transforms;

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

    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();
    private readonly List<Type> _elementTypes = typeof(SvgElement).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(SvgElement)) && !t.IsAbstract)
        .OrderBy(t => t.Name).ToList();

    private bool _isDragging;
    private Shim.SKPoint _dragStart;
    private SvgVisualElement? _dragElement;
    private List<(PropertyInfo Prop, SvgUnit Unit, char Axis)>? _dragProps;

    private const float HandleSize = 6f;
    private bool _isResizing;
    private bool _isRotating;
    private int _resizeHandle;
    private SK.SKPoint _resizeStart;
    private SK.SKRect _startRect;
    private SvgVisualElement? _resizeElement;
    private SvgVisualElement? _rotateElement;
    private SK.SKPoint _rotateStart;
    private SK.SKPoint _rotateCenter;
    private float _startAngle;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        DataContext = this;
        AddHandler(DragDrop.DragOverEvent, Window_OnDragOver);
        AddHandler(DragDrop.DropEvent, Window_OnDrop);
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

    private void Window_OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.FileNames))
            e.DragEffects = DragDropEffects.Copy;
    }

    private async void Window_OnDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.FileNames))
        {
            var files = e.Data.GetFileNames();
            var file = files?.FirstOrDefault();
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
            // check handles first
            if (_selectedDrawable is { } sel && _selectedElement is { })
            {
                var rect = SvgView.SkSvg.SkiaModel.ToSKRect(sel.TransformedBounds);
                var handle = HitHandle(rect, new SK.SKPoint(pp.X, pp.Y), out var center);
                if (handle >= 0)
                {
                    if (handle == 8)
                    {
                        _isRotating = true;
                        _rotateElement = _selectedElement;
                        _rotateStart = new SK.SKPoint(pp.X, pp.Y);
                        _rotateCenter = center;
                        _startAngle = GetRotation(_rotateElement);
                        e.Pointer.Capture(SvgView);
                        return;
                    }
                    _isResizing = true;
                    _resizeElement = _selectedElement;
                    _resizeHandle = handle;
                    _resizeStart = new SK.SKPoint(pp.X, pp.Y);
                    _startRect = rect;
                    e.Pointer.Capture(SvgView);
                    return;
                }
            }

            _selectedDrawable = skSvg.HitTestDrawables(pp).FirstOrDefault();
            _selectedElement = skSvg.HitTestElements(pp).OfType<SvgVisualElement>().FirstOrDefault();
            if (_selectedElement is { })
            {
                LoadProperties(_selectedElement);
                SelectNodeFromElement(_selectedElement);
                TryStartDrag(_selectedElement, new Shim.SKPoint(pp.X, pp.Y), e);
            }
            UpdateSelectedDrawable();
            SvgView.InvalidateVisual();
        }
    }

    private void SvgView_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(SvgView);
        if (SvgView.TryGetPicturePoint(point, out var pp))
        {
            var skp = new SK.SKPoint(pp.X, pp.Y);
            if (_isDragging && _dragElement is { } dragEl && _dragProps is { })
            {
                var dx = pp.X - _dragStart.X;
                var dy = pp.Y - _dragStart.Y;
                foreach (var (Prop, Unit, Axis) in _dragProps)
                {
                    var delta = Axis == 'x' ? dx : dy;
                    Prop.SetValue(dragEl, new SvgUnit(Unit.Type, Unit.Value + delta));
                }
                SvgView.SkSvg!.FromSvgDocument(_document);
                UpdateSelectedDrawable();
                SvgView.InvalidateVisual();
            }
            else if (_isResizing && _resizeElement is { })
            {
                var dx = skp.X - _resizeStart.X;
                var dy = skp.Y - _resizeStart.Y;
                ResizeElement(_resizeElement, _resizeHandle, dx, dy);
                SvgView.SkSvg!.FromSvgDocument(_document);
                UpdateSelectedDrawable();
                SvgView.InvalidateVisual();
            }
            else if (_isRotating && _rotateElement is { })
            {
                var a1 = Math.Atan2(_rotateStart.Y - _rotateCenter.Y, _rotateStart.X - _rotateCenter.X);
                var a2 = Math.Atan2(skp.Y - _rotateCenter.Y, skp.X - _rotateCenter.X);
                var delta = (float)((a2 - a1) * 180.0 / Math.PI);
                SetRotation(_rotateElement, _startAngle + delta, _rotateCenter);
                SvgView.SkSvg!.FromSvgDocument(_document);
                UpdateSelectedDrawable();
                SvgView.InvalidateVisual();
            }
        }
    }

    private void SvgView_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            if (_dragElement is { })
            {
                SaveUndoState();
                LoadProperties(_dragElement);
            }
        }
        else if (_isResizing)
        {
            _isResizing = false;
            if (_resizeElement is { })
            {
                SaveUndoState();
                LoadProperties(_resizeElement);
            }
        }
        else if (_isRotating)
        {
            _isRotating = false;
            if (_rotateElement is { })
            {
                SaveUndoState();
                LoadProperties(_rotateElement);
            }
        }
        e.Pointer.Capture(null);
    }

    private void ApplyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedElement is null || _document is null)
            return;
        SaveUndoState();
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
            SaveUndoState();
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

    private int HitHandle(SK.SKRect rect, SK.SKPoint pt, out SK.SKPoint center)
    {
        center = new(rect.MidX, rect.MidY);
        var hs = HandleSize / 2f;
        var handles = new[]
        {
            new SK.SKRect(rect.Left - hs, rect.Top - hs, rect.Left + hs, rect.Top + hs), // tl 0
            new SK.SKRect(rect.MidX - hs, rect.Top - hs, rect.MidX + hs, rect.Top + hs), // t 1
            new SK.SKRect(rect.Right - hs, rect.Top - hs, rect.Right + hs, rect.Top + hs), // tr 2
            new SK.SKRect(rect.Right - hs, rect.MidY - hs, rect.Right + hs, rect.MidY + hs), // r 3
            new SK.SKRect(rect.Right - hs, rect.Bottom - hs, rect.Right + hs, rect.Bottom + hs), // br 4
            new SK.SKRect(rect.MidX - hs, rect.Bottom - hs, rect.MidX + hs, rect.Bottom + hs), // b 5
            new SK.SKRect(rect.Left - hs, rect.Bottom - hs, rect.Left + hs, rect.Bottom + hs), // bl 6
            new SK.SKRect(rect.Left - hs, rect.MidY - hs, rect.Left + hs, rect.MidY + hs) // l 7
        };
        for (var i = 0; i < handles.Length; i++)
            if (handles[i].Contains(pt))
                return i;
        var rot = new SK.SKPoint(rect.MidX, rect.Top - 20);
        if (SK.SKPoint.Distance(rot, pt) <= HandleSize)
            return 8;
        return -1;
    }

    private float GetRotation(SvgVisualElement? element)
    {
        if (element?.Transforms is { } t)
        {
            var rot = t.OfType<Svg.Transforms.SvgRotate>().FirstOrDefault();
            return rot?.Angle ?? 0f;
        }
        return 0f;
    }

    private void SetRotation(SvgVisualElement element, float angle, SK.SKPoint center)
    {
        var tr = new Svg.Transforms.SvgRotate(angle, center.X, center.Y);
        var col = new Svg.Transforms.SvgTransformCollection();
        col.Add(tr);
        element.Transforms = col;
    }

    private void ResizeElement(SvgVisualElement element, int handle, float dx, float dy)
    {
        switch (element)
        {
            case SvgRectangle rect:
            case Svg.SvgImage img:
            case SvgUse use:
                ResizeBox((dynamic)element, handle, dx, dy);
                break;
        }

        void ResizeBox(dynamic el, int h, float ddx, float ddy)
        {
            float x = el.X.Value;
            float y = el.Y.Value;
            float w = el.Width.Value;
            float hgt = el.Height.Value;
            switch (h)
            {
                case 0: x = _startRect.Left + ddx; y = _startRect.Top + ddy; w = _startRect.Right - x; hgt = _startRect.Bottom - y; break;
                case 1: y = _startRect.Top + ddy; hgt = _startRect.Bottom - y; break;
                case 2: y = _startRect.Top + ddy; w = _startRect.Width + ddx; hgt = _startRect.Bottom - y; break;
                case 3: w = _startRect.Width + ddx; break;
                case 4: w = _startRect.Width + ddx; hgt = _startRect.Height + ddy; break;
                case 5: hgt = _startRect.Height + ddy; break;
                case 6: x = _startRect.Left + ddx; w = _startRect.Right - x; hgt = _startRect.Height + ddy; break;
                case 7: x = _startRect.Left + ddx; w = _startRect.Right - x; break;
            }
            el.X = new SvgUnit(el.X.Type, x);
            el.Y = new SvgUnit(el.Y.Type, y);
            el.Width = new SvgUnit(el.Width.Type, w);
            el.Height = new SvgUnit(el.Height.Type, hgt);
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

        var hs = HandleSize / 2f;
        var handles = new[]
        {
            new SK.SKRect(rect.Left - hs, rect.Top - hs, rect.Left + hs, rect.Top + hs),
            new SK.SKRect(rect.MidX - hs, rect.Top - hs, rect.MidX + hs, rect.Top + hs),
            new SK.SKRect(rect.Right - hs, rect.Top - hs, rect.Right + hs, rect.Top + hs),
            new SK.SKRect(rect.Right - hs, rect.MidY - hs, rect.Right + hs, rect.MidY + hs),
            new SK.SKRect(rect.Right - hs, rect.Bottom - hs, rect.Right + hs, rect.Bottom + hs),
            new SK.SKRect(rect.MidX - hs, rect.Bottom - hs, rect.MidX + hs, rect.Bottom + hs),
            new SK.SKRect(rect.Left - hs, rect.Bottom - hs, rect.Left + hs, rect.Bottom + hs),
            new SK.SKRect(rect.Left - hs, rect.MidY - hs, rect.Left + hs, rect.MidY + hs)
        };
        foreach (var h in handles)
            e.Canvas.DrawRect(h, paint);

        var rot = new SK.SKPoint(rect.MidX, rect.Top - 20);
        e.Canvas.DrawLine(rect.MidX, rect.Top, rot.X, rot.Y, paint);
        e.Canvas.DrawCircle(rot, hs, paint);
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

    private void SaveUndoState()
    {
        if (_document is null)
            return;
        _undo.Push(_document.GetXML());
        _redo.Clear();
    }

    private void RestoreFromString(string xml)
    {
        _document = SvgService.FromSvg(xml);
        SvgView.SkSvg!.FromSvgDocument(_document);
        SaveExpandedNodes();
        BuildTree();
    }

    private void UndoMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_undo.Count == 0 || _document is null)
            return;
        _redo.Push(_document.GetXML());
        var xml = _undo.Pop();
        RestoreFromString(xml);
    }

    private void RedoMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_redo.Count == 0)
            return;
        if (_document is not null)
            _undo.Push(_document.GetXML());
        var xml = _redo.Pop();
        RestoreFromString(xml);
    }

    private async void InsertElementMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        var names = _elementTypes.Select(t => t.Name).ToList();
        var win = new InsertElementWindow(names);
        var result = await win.ShowDialog<string?>(this);
        if (result is null)
            return;
        var type = _elementTypes.FirstOrDefault(t => t.Name == result);
        if (type is null || _document is null)
            return;
        SaveUndoState();
        var element = (SvgElement)Activator.CreateInstance(type)!;
        var parent = _selectedElement as SvgElement ?? _document;
        parent.Children.Add(element);
        SvgView.SkSvg!.FromSvgDocument(_document);
        BuildTree();
    }

    private void RemoveElementMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedElement?.Parent is { } parent && _document is { })
        {
            SaveUndoState();
            parent.Children.Remove(_selectedElement);
            _selectedElement = null;
            SvgView.SkSvg!.FromSvgDocument(_document);
            BuildTree();
        }
    }

    private void NewMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        SaveUndoState();
        _document = new SvgDocument { Width = 100, Height = 100 };
        SvgView.SkSvg!.FromSvgDocument(_document);
        SaveExpandedNodes();
        BuildTree();
    }

    private async void EditTextMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null)
            return;
        var win = new TextEditorWindow(_document.GetXML());
        var result = await win.ShowDialog<string?>(this);
        if (!string.IsNullOrEmpty(result))
        {
            SaveUndoState();
            _document = SvgService.FromSvg(result);
            SvgView.SkSvg!.FromSvgDocument(_document);
            BuildTree();
        }
    }

    private async void PreviewMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null)
            return;
        var win = new PreviewWindow(_document);
        await win.ShowDialog(this);
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
