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
using Svg.Model;
using Svg.Model.Drawables;
using Svg.Model.Services;
using Svg.Transforms;

namespace HitTestEditorSample;

public partial class MainWindow : Window
{
    private DrawableBase? _selectedDrawable;
    private SvgVisualElement? _selectedElement;
    private SvgElement? _selectedSvgElement;
    private SvgDocument? _document;

    private ObservableCollection<PropertyEntry> Properties { get; } = new();
    private ObservableCollection<SvgNode> Nodes { get; } = new();
    private readonly HashSet<string> _expandedIds = new();
    private HashSet<string> _filterBackup = new();

    private SvgElement? _clipboard;
    private string? _clipboardXml;

    private bool _wireframeEnabled;
    private bool _filtersDisabled;
    private bool _snapToGrid;
    private double _gridSize = 10.0;

    private readonly SK.SKColor _boundsColor = SK.SKColors.Red;

    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();
    private readonly List<Type> _elementTypes = typeof(SvgElement).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(SvgElement)) && !t.IsAbstract)
        .OrderBy(t => t.Name).ToList();

    private string _filter = string.Empty;
    private SvgNode? _dragNode;
    private Point _treeDragStart;
    private bool _treeDragging;

    private bool _isDragging;
    private Shim.SKPoint _dragStart;
    private SvgVisualElement? _dragElement;
    private List<(PropertyInfo Prop, SvgUnit Unit, char Axis)>? _dragProps;
    private float _dragTextX;
    private float _dragTextY;
    private float _dragTransX;
    private float _dragTransY;

    private const float HandleSize = 6f;
    private bool _isResizing;
    private bool _isRotating;
    private int _resizeHandle;
    private Shim.SKPoint _resizeStart;
    private Shim.SKPoint _resizeStartLocal;
    private Shim.SKMatrix _resizeMatrix;
    private Shim.SKMatrix _resizeInverse;
    private SK.SKRect _startRect;
    private SvgVisualElement? _resizeElement;
    private SvgVisualElement? _rotateElement;
    private SK.SKPoint _rotateStart;
    private SK.SKPoint _rotateCenter;
    private float _startAngle;

    private bool _isPanning;
    private Point _panStart;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        DataContext = this;
        AddHandler(DragDrop.DragOverEvent, Window_OnDragOver);
        AddHandler(DragDrop.DropEvent, Window_OnDrop);
        KeyDown += MainWindow_OnKeyDown;
        SvgView.PointerWheelChanged += SvgView_OnPointerWheelChanged;
        DocumentTree.AddHandler(PointerPressedEvent, DocumentTree_OnPointerPressed, RoutingStrategies.Tunnel);
        DocumentTree.AddHandler(PointerMovedEvent, DocumentTree_OnPointerMoved, RoutingStrategies.Tunnel);
        DocumentTree.AddHandler(PointerReleasedEvent, DocumentTree_OnPointerReleased, RoutingStrategies.Tunnel);
        DocumentTree.AddHandler(DragDrop.DropEvent, DocumentTree_OnDrop);
        DocumentTree.AddHandler(DragDrop.DragOverEvent, DocumentTree_OnDragOver);
        _wireframeEnabled = false;
        _filtersDisabled = false;
        _snapToGrid = false;
        _gridSize = 10.0;
        SvgView.Wireframe = false;
        if (SvgView.SkSvg is { } initSvg)
            initSvg.IgnoreAttributes = DrawAttributes.None;
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
        {
            skSvg2.FromSvgDocument(_document);
            skSvg2.OnDraw += SvgView_OnDraw;
        }

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
            _selectedSvgElement = null;
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

    private async void ExportElementMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedDrawable is null || SvgView.SkSvg is null)
            return;
        var dialog = new SaveFileDialog
        {
            Filters = new()
            {
                new FileDialogFilter { Name = "PNG", Extensions = { "png" } },
                new FileDialogFilter { Name = "All", Extensions = { "*" } }
            },
            DefaultExtension = "png"
        };
        var path = await dialog.ShowAsync(this);
        if (string.IsNullOrEmpty(path))
            return;

        var bounds = _selectedDrawable.TransformedBounds;
        if (!(bounds.Width > 0) || !(bounds.Height > 0))
            return;

        var picture = _selectedDrawable.Snapshot(bounds);
        var skPicture = SvgView.SkSvg!.SkiaModel.ToSKPicture(picture);
        if (skPicture is null)
            return;
        using var stream = File.OpenWrite(path);
        skPicture.ToImage(stream, SK.SKColors.Transparent, SK.SKEncodedImageFormat.Png, 100, 1f, 1f,
            SK.SKColorType.Rgba8888, SK.SKAlphaType.Premul, SvgView.SkSvg.Settings.Srgb);
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
                _selectedSvgElement = null;
                SvgView.InvalidateVisual();
            }
        }
    }

    private void LoadProperties(SvgElement element)
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
        if (element is SvgTextBase txt)
        {
            var prop = element.GetType().GetProperty(nameof(SvgTextBase.Text));
            if (prop is { })
            {
                var value = txt.Text;
                var entry = new PropertyEntry("Text", prop, value);
                entry.PropertyChanged += PropertyEntryOnPropertyChanged;
                Properties.Add(entry);
            }
        }
    }

    private void SvgView_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetPosition(SvgView);
        if (e.GetCurrentPoint(SvgView).Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _panStart = point;
            e.Pointer.Capture(SvgView);
            return;
        }
        if (SvgView.SkSvg is { } skSvg && SvgView.TryGetPicturePoint(point, out var pp))
        {
            // check handles first
            if (_selectedDrawable is { } sel && _selectedElement is { })
            {
                var bounds = GetBoundsInfo(sel);
                var handle = HitHandle(bounds, new SK.SKPoint(pp.X, pp.Y), out var center);
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
                    _resizeStart = new Shim.SKPoint(pp.X, pp.Y);
                    _startRect = SvgView.SkSvg!.SkiaModel.ToSKRect(sel.GeometryBounds);
                    _resizeMatrix = sel.TotalTransform;
                    if (!_resizeMatrix.TryInvert(out _resizeInverse))
                        _resizeInverse = Shim.SKMatrix.CreateIdentity();
                    _resizeStartLocal = _resizeInverse.MapPoint(_resizeStart);
                    e.Pointer.Capture(SvgView);
                    return;
                }
            }

            _selectedDrawable = skSvg.HitTestDrawables(pp).FirstOrDefault();
            _selectedElement = skSvg.HitTestElements(pp).OfType<SvgVisualElement>().FirstOrDefault();
            _selectedSvgElement = _selectedElement;
            if (_selectedSvgElement is { })
            {
                LoadProperties(_selectedSvgElement);
                SelectNodeFromElement(_selectedSvgElement);
                if (_selectedElement is { })
                    TryStartDrag(_selectedElement, new Shim.SKPoint(pp.X, pp.Y), e);
            }
            UpdateSelectedDrawable();
            SvgView.InvalidateVisual();
        }
    }

    private void SvgView_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(SvgView);

        if (_isPanning)
        {
            var dx = point.X - _panStart.X;
            var dy = point.Y - _panStart.Y;
            _panStart = point;
            SvgView.PanX += dx;
            SvgView.PanY += dy;
            SvgView.InvalidateVisual();
            return;
        }

        if (SvgView.TryGetPicturePoint(point, out var pp))
        {
            var skp = new SK.SKPoint(pp.X, pp.Y);
            if (_isDragging && _dragElement is { } dragEl)
            {
                var dx = pp.X - _dragStart.X;
                var dy = pp.Y - _dragStart.Y;
                if (_dragProps is { })
                {
                    foreach (var (Prop, Unit, Axis) in _dragProps)
                    {
                        var delta = Axis == 'x' ? dx : dy;
                        var val = Unit.Value + delta;
                        if (_snapToGrid)
                            val = Snap(val);
                        Prop.SetValue(dragEl, new SvgUnit(Unit.Type, val));
                    }
                }
                else if (dragEl is SvgTextBase txt)
                {
                    if (txt.X.Count > 0)
                        txt.X[0] = new SvgUnit(txt.X[0].Type, _snapToGrid ? Snap(_dragTextX + dx) : _dragTextX + dx);
                    if (txt.Y.Count > 0)
                        txt.Y[0] = new SvgUnit(txt.Y[0].Type, _snapToGrid ? Snap(_dragTextY + dy) : _dragTextY + dy);
                }
                else
                {
                    var tx = _dragTransX + dx;
                    var ty = _dragTransY + dy;
                    if (_snapToGrid)
                    {
                        tx = Snap(tx);
                        ty = Snap(ty);
                    }
                    SetTranslation(dragEl, tx, ty);
                }
                SvgView.SkSvg!.FromSvgDocument(_document);
                UpdateSelectedDrawable();
                SvgView.InvalidateVisual();
            }
            else if (_isResizing && _resizeElement is { })
            {
                var local = _resizeInverse.MapPoint(new Shim.SKPoint(skp.X, skp.Y));
                var dx = local.X - _resizeStartLocal.X;
                var dy = local.Y - _resizeStartLocal.Y;
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
        else if (_isPanning)
        {
            _isPanning = false;
        }
        e.Pointer.Capture(null);
    }

    private void SvgView_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var zoom = SvgView.Zoom;
        zoom *= e.Delta.Y > 0 ? 1.1 : 0.9;
        if (zoom < 0.1) zoom = 0.1;
        if (zoom > 10) zoom = 10;
        SvgView.Zoom = zoom;
        SvgView.InvalidateVisual();
    }

    private void ApplyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedSvgElement is null || _document is null)
            return;
        SaveUndoState();
        foreach (var entry in Properties)
        {
            entry.Apply(_selectedSvgElement);
        }
        SvgView.SkSvg!.FromSvgDocument(_document);
        UpdateSelectedDrawable();
        SaveExpandedNodes();
        foreach (var entry in Properties)
            entry.PropertyChanged -= PropertyEntryOnPropertyChanged;
        Properties.Clear();
        BuildTree();
        if (_selectedSvgElement is { })
            SelectNodeFromElement(_selectedSvgElement);
        SvgView.InvalidateVisual();
    }

    private void PropertyEntryOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is PropertyEntry entry && _selectedSvgElement is { } && _document is { })
        {
            SaveUndoState();
            entry.Apply(_selectedSvgElement);
            SvgView.SkSvg!.FromSvgDocument(_document);
            UpdateSelectedDrawable();
            SvgView.InvalidateVisual();
        }
    }

    private void TryStartDrag(SvgVisualElement element, Shim.SKPoint start, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(SvgView).Properties.IsLeftButtonPressed)
            return;

        if (GetDragProperties(element, out var props))
        {
            _dragProps = props;
            _isDragging = true;
            _dragStart = start;
            _dragElement = element;
            args.Pointer.Capture(SvgView);
            return;
        }

        _dragProps = null;
        _isDragging = true;
        _dragStart = start;
        _dragElement = element;
        if (element is SvgTextBase txt)
        {
            _dragTextX = txt.X.Count > 0 ? txt.X[0].Value : 0f;
            _dragTextY = txt.Y.Count > 0 ? txt.Y[0].Value : 0f;
        }
        else
        {
            var (tx, ty) = GetTranslation(element);
            _dragTransX = tx;
            _dragTransY = ty;
        }
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
            case SvgTextBase:
                props = null!;
                return false;
            case SvgPath:
                props = null!;
                return false;
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

    private struct BoundsInfo
    {
        public SK.SKPoint TL, TR, BR, BL;
        public SK.SKPoint TopMid, RightMid, BottomMid, LeftMid;
        public SK.SKPoint Center, RotHandle;
    }

    private static SK.SKPoint Mid(SK.SKPoint a, SK.SKPoint b) => new((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);

    private BoundsInfo GetBoundsInfo(DrawableBase drawable)
    {
        var rect = drawable.GeometryBounds;
        var m = drawable.TotalTransform;
        var tl = SvgView.SkSvg!.SkiaModel.ToSKPoint(m.MapPoint(new Shim.SKPoint(rect.Left, rect.Top)));
        var tr = SvgView.SkSvg!.SkiaModel.ToSKPoint(m.MapPoint(new Shim.SKPoint(rect.Right, rect.Top)));
        var br = SvgView.SkSvg!.SkiaModel.ToSKPoint(m.MapPoint(new Shim.SKPoint(rect.Right, rect.Bottom)));
        var bl = SvgView.SkSvg!.SkiaModel.ToSKPoint(m.MapPoint(new Shim.SKPoint(rect.Left, rect.Bottom)));
        var topMid = Mid(tl, tr);
        var rightMid = Mid(tr, br);
        var bottomMid = Mid(br, bl);
        var leftMid = Mid(bl, tl);
        var center = Mid(tl, br);
        var edge = new SK.SKPoint(tr.X - tl.X, tr.Y - tl.Y);
        var len = (float)Math.Sqrt(edge.X * edge.X + edge.Y * edge.Y);
        var normal = len > 0 ? new SK.SKPoint(-edge.Y / len, edge.X / len) : new SK.SKPoint(0, -1);
        var rotHandle = new SK.SKPoint(topMid.X - normal.X * 20f, topMid.Y - normal.Y * 20f);
        return new BoundsInfo
        {
            TL = tl,
            TR = tr,
            BR = br,
            BL = bl,
            TopMid = topMid,
            RightMid = rightMid,
            BottomMid = bottomMid,
            LeftMid = leftMid,
            Center = center,
            RotHandle = rotHandle
        };
    }

    private int HitHandle(BoundsInfo b, SK.SKPoint pt, out SK.SKPoint center)
    {
        center = b.Center;
        var hs = HandleSize / 2f;
        var handlePts = new[] { b.TL, b.TopMid, b.TR, b.RightMid, b.BR, b.BottomMid, b.BL, b.LeftMid };
        for (int i = 0; i < handlePts.Length; i++)
        {
            var r = new SK.SKRect(handlePts[i].X - hs, handlePts[i].Y - hs, handlePts[i].X + hs, handlePts[i].Y + hs);
            if (r.Contains(pt))
                return i;
        }
        if (SK.SKPoint.Distance(b.RotHandle, pt) <= HandleSize)
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

    private (float X, float Y) GetTranslation(SvgVisualElement? element)
    {
        if (element?.Transforms is { } t)
        {
            var tr = t.OfType<Svg.Transforms.SvgTranslate>().FirstOrDefault();
            if (tr is { })
                return (tr.X, tr.Y);
        }
        return (0f, 0f);
    }

    private void SetTranslation(SvgVisualElement element, float x, float y)
    {
        if (_snapToGrid)
        {
            x = Snap(x);
            y = Snap(y);
        }
        var col = new Svg.Transforms.SvgTransformCollection();
        col.Add(new Svg.Transforms.SvgTranslate(x, y));
        element.Transforms = col;
    }

    private float Snap(float value)
    {
        if (!_snapToGrid || _gridSize <= 0)
            return value;
        return (float)(Math.Round(value / _gridSize) * _gridSize);
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
            case SvgCircle circle:
                ResizeCircle(circle, handle, dx, dy);
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
            if (_snapToGrid)
            {
                x = Snap(x);
                y = Snap(y);
                w = Snap(w);
                hgt = Snap(hgt);
            }
            el.X = new SvgUnit(el.X.Type, x);
            el.Y = new SvgUnit(el.Y.Type, y);
            el.Width = new SvgUnit(el.Width.Type, w);
            el.Height = new SvgUnit(el.Height.Type, hgt);
        }

        void ResizeCircle(SvgCircle c, int h, float ddx, float ddy)
        {
            float x = _startRect.Left;
            float y = _startRect.Top;
            float w = _startRect.Width;
            float hgt = _startRect.Height;
            switch (h)
            {
                case 0: x += ddx; y += ddy; w = _startRect.Right - x; hgt = _startRect.Bottom - y; break;
                case 1: y += ddy; hgt = _startRect.Bottom - y; break;
                case 2: y += ddy; w += ddx; hgt = _startRect.Bottom - y; break;
                case 3: w += ddx; break;
                case 4: w += ddx; hgt += ddy; break;
                case 5: hgt += ddy; break;
                case 6: x += ddx; w = _startRect.Right - x; hgt += ddy; break;
                case 7: x += ddx; w = _startRect.Right - x; break;
            }
            var cx = x + w / 2f;
            var cy = y + hgt / 2f;
            var r = Math.Max(w, hgt) / 2f;
            if (_snapToGrid)
            {
                cx = Snap(cx);
                cy = Snap(cy);
                r = Snap(r);
            }
            c.CenterX = new SvgUnit(c.CenterX.Type, cx);
            c.CenterY = new SvgUnit(c.CenterY.Type, cy);
            c.Radius = new SvgUnit(c.Radius.Type, r);
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
        var info = GetBoundsInfo(_selectedDrawable);
        using (var path = new SK.SKPath())
        {
            path.MoveTo(info.TL);
            path.LineTo(info.TR);
            path.LineTo(info.BR);
            path.LineTo(info.BL);
            path.Close();
            e.Canvas.DrawPath(path, paint);
        }

        var hs = HandleSize / 2f;
        var pts = new[] { info.TL, info.TopMid, info.TR, info.RightMid, info.BR, info.BottomMid, info.BL, info.LeftMid };
        foreach (var pt in pts)
            e.Canvas.DrawRect(pt.X - hs, pt.Y - hs, HandleSize, HandleSize, paint);

        e.Canvas.DrawLine(info.TopMid, info.RotHandle, paint);
        e.Canvas.DrawCircle(info.RotHandle, hs, paint);
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
        {
            var node = CreateNodeFiltered(_document, null);
            if (node is not null)
                Nodes.Add(node);
        }
        Dispatcher.UIThread.Post(() =>
        {
            RestoreExpandedNodes();
            if (_selectedSvgElement is { } sel && Nodes.Count > 0)
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
            if (_selectedSvgElement is SvgVisualElement)
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

    private SvgNode? CreateNodeFiltered(SvgElement element, SvgNode? parent)
    {
        bool Match(SvgElement el)
        {
            if (string.IsNullOrEmpty(_filter))
                return true;
            return (!string.IsNullOrEmpty(el.ID) && el.ID.Contains(_filter, StringComparison.OrdinalIgnoreCase)) ||
                   el.GetType().Name.Contains(_filter, StringComparison.OrdinalIgnoreCase);
        }

        var node = new SvgNode(element, parent);
        foreach (var child in element.Children.OfType<SvgElement>())
        {
            var childNode = CreateNodeFiltered(child, node);
            if (childNode is not null)
                node.Children.Add(childNode);
        }

        if (Match(element) || node.Children.Count > 0)
            return node;
        return null;
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
            _selectedSvgElement = node.Element;
            _selectedElement = node.Element as SvgVisualElement;
            UpdateSelectedDrawable();
            LoadProperties(_selectedSvgElement);
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

    private static bool IsAncestor(SvgNode parent, SvgNode child)
    {
        var p = child.Parent;
        while (p is not null)
        {
            if (p == parent)
                return true;
            p = p.Parent;
        }
        return false;
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
        var parent = _selectedSvgElement ?? _document;
        parent.Children.Add(element);
        SvgView.SkSvg!.FromSvgDocument(_document);
        BuildTree();
    }

    private void RemoveElementMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedSvgElement is SvgElement { Parent: { } parent } && _document is { })
        {
            SaveUndoState();
            parent.Children.Remove(_selectedSvgElement);
            _selectedElement = null;
            _selectedSvgElement = null;
            _selectedDrawable = null;
            SvgView.SkSvg!.FromSvgDocument(_document);
            BuildTree();
            SvgView.InvalidateVisual();
        }
    }

    private void CopyElementMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedSvgElement is null)
            return;
        _clipboard = (SvgElement)_selectedSvgElement.DeepCopy();
        _clipboardXml = _selectedSvgElement.GetXML();
    }

    private void PasteElementMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null || _clipboard is null)
            return;

        SaveUndoState();
        var parent = _selectedSvgElement?.Parent as SvgElement ?? _document;
        var clone = (SvgElement)_clipboard.DeepCopy();
        parent.Children.Add(clone);
        SvgView.SkSvg!.FromSvgDocument(_document);
        BuildTree();
        SelectNodeFromElement(clone);
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

    private void WireframeMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        _wireframeEnabled = !_wireframeEnabled;
        SvgView.Wireframe = _wireframeEnabled;
        SvgView.InvalidateVisual();
    }

    private void FiltersMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        _filtersDisabled = !_filtersDisabled;
        SvgView.DisableFilters = _filtersDisabled;
        if (SvgView.SkSvg is { } skSvg && _document is { })
            skSvg.FromSvgDocument(_document);
        SvgView.InvalidateVisual();
    }

    private async void SettingsMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_snapToGrid, _gridSize);
        var result = await win.ShowDialog<bool?>(this);
        if (result == true)
        {
            _snapToGrid = win.SnapToGrid;
            _gridSize = win.GridSize;
        }
    }

    private void MainWindow_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _selectedDrawable = null;
            _selectedElement = null;
            _selectedSvgElement = null;
            DocumentTree.SelectedItem = null;
            SvgView.InvalidateVisual();
        }
    }

    private void FilterBox_OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox tb)
        {
            var newFilter = tb.Text ?? string.Empty;
            var wasFiltered = !string.IsNullOrEmpty(_filter);
            var willBeFiltered = !string.IsNullOrEmpty(newFilter);

            if (!wasFiltered && willBeFiltered)
            {
                _filterBackup = new HashSet<string>(_expandedIds);
                SaveExpandedNodes();
            }
            else if (wasFiltered && !willBeFiltered)
            {
                _filter = newFilter;
                _expandedIds.Clear();
                foreach (var id in _filterBackup)
                    _expandedIds.Add(id);
                BuildTree();
                return;
            }
            else
            {
                SaveExpandedNodes();
            }

            _filter = newFilter;
            BuildTree();
        }
    }

    private void DocumentTree_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _dragNode = DocumentTree.SelectedItem as SvgNode;
        _treeDragStart = e.GetCurrentPoint(DocumentTree).Position;
        _treeDragging = false;
    }

    private async void DocumentTree_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragNode is { } node && e.GetCurrentPoint(DocumentTree).Properties.IsLeftButtonPressed)
        {
            var pos = e.GetCurrentPoint(DocumentTree).Position;
            if (!_treeDragging)
            {
                if (Math.Abs(pos.X - _treeDragStart.X) < 4 && Math.Abs(pos.Y - _treeDragStart.Y) < 4)
                    return;
                _treeDragging = true;
            }
            var data = new DataObject();
            data.Set("SvgNode", node);
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
            _dragNode = null;
            _treeDragging = false;
        }
    }

    private void DocumentTree_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragNode = null;
        _treeDragging = false;
    }

    private void DocumentTree_OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains("SvgNode"))
            e.DragEffects = DragDropEffects.Move;
    }

    private void DocumentTree_OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains("SvgNode"))
            return;
        if (e.Source is Control { DataContext: SvgNode target } && e.Data.Get("SvgNode") is SvgNode node)
        {
            if (node == target || IsAncestor(node, target) || node.Parent is null)
                return;
            SaveUndoState();
            node.Parent.Element.Children.Remove(node.Element);
            target.Element.Children.Add(node.Element);
            BuildTree();
            SelectNodeFromElement(node.Element);
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
