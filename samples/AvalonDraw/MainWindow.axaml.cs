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
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Svg.Skia;
using SK = SkiaSharp;
using Shim = ShimSkiaSharp;
using Svg;
using Svg.Skia;
using Svg.Model;
using Svg.Model.Drawables;
using Svg.Model.Services;
using Svg.Transforms;
using Svg.Pathing;
using AvalonDraw.Services;
using Tool = AvalonDraw.Services.ToolService.Tool;

namespace AvalonDraw;

public partial class MainWindow : Window
{
    private DrawableBase? _selectedDrawable;
    private SvgVisualElement? _selectedElement;
    private SvgElement? _selectedSvgElement;
    private SvgDocument? _document;
    private string? _currentFile;

    private readonly PropertiesService _propertiesService = new();
    public ObservableCollection<PropertyEntry> Properties => _propertiesService.Properties;
    public ObservableCollection<PropertyEntry> FilteredProperties => _propertiesService.FilteredProperties;
    private ObservableCollection<SvgNode> Nodes { get; } = new();
    public ObservableCollection<string> Ids => _propertiesService.Ids;
    private readonly HashSet<string> _expandedIds = new();
    private HashSet<string> _filterBackup = new();

    private SvgElement? _clipboard;
    private string? _clipboardXml;

    private bool _wireframeEnabled;
    private bool _filtersDisabled;
    private bool _snapToGrid;
    private bool _showGrid;
    private double _gridSize = 10.0;

    private readonly SK.SKColor _boundsColor = SK.SKColors.Red;
    private readonly SK.SKColor _segmentColor = SK.SKColors.OrangeRed;
    private readonly SK.SKColor _controlColor = SK.SKColors.SkyBlue;

    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();
    private readonly List<Type> _elementTypes = typeof(SvgElement).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(SvgElement)) && !t.IsAbstract)
        .OrderBy(t => GetElementName(t)).ToList();

    private ContextMenu? _treeMenu;

    private string _filter = string.Empty;
    private string _propertyFilter = string.Empty;
    private SvgNode? _dragNode;
    private Point _treeDragStart;
    private bool _treeDragging;

    private enum DropPosition { None, Inside, Before, After }
    private Border? _dropIndicator;
    private SvgNode? _dropTarget;
    private DropPosition _dropPosition;
    private readonly IBrush _dropBeforeBrush = Brushes.DodgerBlue;
    private readonly IBrush _dropAfterBrush = Brushes.DodgerBlue;
    private readonly IBrush _dropInsideBrush = Brushes.SeaGreen;

    private bool _isDragging;
    private Shim.SKPoint _dragStart;
    private SvgVisualElement? _dragElement;
    private List<(PropertyInfo Prop, SvgUnit Unit, char Axis)>? _dragProps;
    private float _dragTextX;
    private float _dragTextY;
    private float _dragTransX;
    private float _dragTransY;

    // Size of resize/rotate handles in device-independent pixels
    private const float HandleSize = 10f;
    private bool _isResizing;
    private bool _isRotating;
    private int _resizeHandle;
    private Shim.SKPoint _resizeStart;
    private Shim.SKPoint _resizeStartLocal;
    private Shim.SKMatrix _resizeMatrix;
    private Shim.SKMatrix _resizeInverse;
    private SK.SKRect _startRect;
    private float _startTransX;
    private float _startTransY;
    private float _startScaleX = 1f;
    private float _startScaleY = 1f;
    private SvgVisualElement? _resizeElement;
    private SvgVisualElement? _rotateElement;
    private SK.SKPoint _rotateStart;
    private SK.SKPoint _rotateCenter;
    private float _startAngle;

    private bool _isPanning;
    private Point _panStart;

    private List<SvgVisualElement> _hitElements = new();
    private Shim.SKPoint _lastHitPoint;
    private int _hitIndex;

    private bool _pendingPress;
    private Shim.SKPoint _pressPoint;
    private List<SvgVisualElement> _pressHits = new();
    private SvgVisualElement? _pressElement;
    private bool _pressRightButton;
    private const float DragThreshold = 2f;

    private readonly ToolService _toolService = new();

    private SvgVisualElement? _newElement;
    private Shim.SKPoint _newStart;
    private bool _creating;

    private readonly PathService _pathService = new();

    private bool _polyEditing;
    private SvgVisualElement? _editPolyElement;
    private DrawableBase? _editPolyDrawable;
    private bool _editPolyline;
    private readonly List<Shim.SKPoint> _polyPoints = new();
    private int _activePolyPoint = -1;
    private Shim.SKMatrix _polyMatrix;
    private Shim.SKMatrix _polyInverse;

    public MainWindow()
    {
        Resources["PropertyEditorTemplate"] = new FuncDataTemplate<PropertyEntry>((entry, ns) =>
        {
            if (entry.Options is { } opts)
            {
                var box = new AutoCompleteBox
                {
                    ItemsSource = opts,
                    MinimumPrefixLength = 0,
                    VerticalAlignment = VerticalAlignment.Center
                };
                box[!AutoCompleteBox.TextProperty] = new Binding("Value") { Mode = BindingMode.TwoWay };
                box.GotFocus += (_, _) => box.IsDropDownOpen = true;
                return box;
            }
            if (entry.Suggestions is { } sugg)
            {
                var box = new AutoCompleteBox
                {
                    ItemsSource = sugg,
                    MinimumPrefixLength = 0,
                    VerticalAlignment = VerticalAlignment.Center
                };
                box[!AutoCompleteBox.TextProperty] = new Binding("Value") { Mode = BindingMode.TwoWay };
                box.GotFocus += (_, _) => box.IsDropDownOpen = true;
                return box;
            }
            if (entry.Property?.PropertyType == typeof(bool))
            {
                var cb = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
                cb[!ToggleButton.IsCheckedProperty] = new Binding("Value")
                {
                    Mode = BindingMode.TwoWay,
                    Converter = new BooleanStringConverter()
                };
                return cb;
            }
            if (entry.Property?.PropertyType == typeof(SK.SKColor) || entry.Property?.PropertyType == typeof(Color))
            {
                var picker = new ColorPicker { Width = 120, VerticalAlignment = VerticalAlignment.Center };
                picker[!ColorPicker.ColorProperty] = new Binding("Value")
                {
                    Mode = BindingMode.TwoWay,
                    Converter = new ColorStringConverter()
                };
                return picker;
            }
            var tb = new TextBox { VerticalContentAlignment = VerticalAlignment.Center };
            tb[!TextBox.TextProperty] = new Binding("Value") { Mode = BindingMode.TwoWay };
            return tb;
        }, true);
        Resources["EyeIconConverter"] = new EyeIconConverter();
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        DataContext = this;
        _propertiesService.EntryChanged += PropertyEntryOnPropertyChanged;
        ApplyPropertyFilter();
        AddHandler(DragDrop.DragOverEvent, Window_OnDragOver);
        AddHandler(DragDrop.DropEvent, Window_OnDrop);
        KeyDown += MainWindow_OnKeyDown;
        SvgView.PointerWheelChanged += SvgView_OnPointerWheelChanged;
        DocumentTree.AddHandler(PointerPressedEvent, DocumentTree_OnPointerPressed, RoutingStrategies.Tunnel);
        DocumentTree.AddHandler(PointerMovedEvent, DocumentTree_OnPointerMoved, RoutingStrategies.Tunnel);
        DocumentTree.AddHandler(PointerReleasedEvent, DocumentTree_OnPointerReleased, RoutingStrategies.Tunnel);
        DocumentTree.AddHandler(DragDrop.DropEvent, DocumentTree_OnDrop);
        DocumentTree.AddHandler(DragDrop.DragOverEvent, DocumentTree_OnDragOver);
        DocumentTree.AddHandler(DragDrop.DragLeaveEvent, DocumentTree_OnDragLeave);
        _toolService.SetTool(Tool.Select);
        _treeMenu = BuildTreeContextMenu();
        DocumentTree.ContextMenu = _treeMenu;
        _dropIndicator = this.FindControl<Border>("DropIndicator");
        _wireframeEnabled = false;
        _filtersDisabled = false;
        _snapToGrid = false;
        _showGrid = false;
        _gridSize = 10.0;
        SvgView.Wireframe = false;
        if (SvgView.SkSvg is { } initSvg)
            initSvg.IgnoreAttributes = DrawAttributes.None;
        LoadDocument("Assets/__tiger.svg");
    }

    private void LoadDocument(string path)
    {
        // try load from Avalonia resources first
        var uri = new Uri($"avares://AvalonDraw/{path}");

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

        SvgView.Zoom = 1.0;
        SvgView.PanX = 0;
        SvgView.PanY = 0;

        SaveExpandedNodes();
        _currentFile = path;
        UpdateTitle();
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
            _currentFile = path;
            UpdateTitle();
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
                _selectedDrawable = null;
                _selectedElement = null;
                _selectedSvgElement = null;
                SvgView.InvalidateVisual();
            }
        }
    }

    private void LoadProperties(SvgElement element)
    {
        _propertiesService.LoadProperties(element);
        _propertiesService.ApplyFilter(_propertyFilter);
    }

    private async void SvgView_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetPosition(SvgView);
        if (_toolService.CurrentTool == Tool.PathSelect && _pathService.IsEditing && e.ClickCount == 2 && SvgView.TryGetPicturePoint(point, out var addp))
        {
            SaveUndoState();
            _pathService.AddPoint(new Shim.SKPoint((float)addp.X, (float)addp.Y));
            if (_document is { })
            {
                SvgView.SkSvg!.FromSvgDocument(_document);
                UpdateSelectedDrawable();
                SvgView.InvalidateVisual();
            }
            return;
        }
        if ((_toolService.CurrentTool == Tool.PolygonSelect || _toolService.CurrentTool == Tool.PolylineSelect) && _polyEditing && e.ClickCount == 2 && SvgView.TryGetPicturePoint(point, out var addpp))
        {
            AddPolyPoint(new Shim.SKPoint((float)addpp.X, (float)addpp.Y));
            SvgView.SkSvg!.FromSvgDocument(_document);
            UpdateSelectedDrawable();
            SvgView.InvalidateVisual();
            return;
        }
        if ((_toolService.CurrentTool == Tool.Polygon || _toolService.CurrentTool == Tool.Polyline) && e.GetCurrentPoint(SvgView).Properties.IsRightButtonPressed)
        {
            if (_creating && _newElement is { } && SvgView.TryGetPicturePoint(point, out var rp))
            {
                SaveUndoState();
                _toolService.FinalizePolygon(_newElement, _toolService.CurrentTool,
                    new Shim.SKPoint((float)rp.X, (float)rp.Y), _snapToGrid, Snap);
                _creating = false;
                LoadProperties(_newElement);
                _selectedElement = _newElement;
                _selectedSvgElement = _newElement;
                UpdateSelectedDrawable();
                SvgView.SkSvg!.FromSvgDocument(_document);
                SvgView.InvalidateVisual();
                _newElement = null;
            }
            return;
        }

        if ((_toolService.CurrentTool == Tool.Polygon || _toolService.CurrentTool == Tool.Polyline) && e.GetCurrentPoint(SvgView).Properties.IsLeftButtonPressed)
        {
            if (SvgView.SkSvg is { } && SvgView.TryGetPicturePoint(point, out var p) && _document is { })
            {
                SvgElement parent = _selectedSvgElement is SvgGroup grp ? grp : _document!;
                if (!_creating)
                {
                    SaveUndoState();
                    var sx = _snapToGrid ? Snap(p.X) : p.X;
                    var sy = _snapToGrid ? Snap(p.Y) : p.Y;
                    _newStart = new Shim.SKPoint(sx, sy);
                    _newElement = _toolService.CreateElement(_toolService.CurrentTool, parent, _newStart);
                    if (_newElement is { })
                    {
                        parent.Children.Add(_newElement);
                        SvgView.SkSvg!.FromSvgDocument(_document);
                        SaveExpandedNodes();
                        BuildTree();
                        SelectNodeFromElement(_newElement);
                        _selectedElement = _newElement as SvgVisualElement;
                        _selectedSvgElement = _newElement;
                        UpdateSelectedDrawable();
                        LoadProperties(_newElement);
                        SvgView.InvalidateVisual();
                        _toolService.UpdateElement(_newElement, _toolService.CurrentTool,
                            _newStart, _newStart, _snapToGrid, Snap);
                        SvgView.SkSvg!.FromSvgDocument(_document);
                        _creating = true;
                    }
                }
                else if (_newElement is { })
                {
                    SaveUndoState();
                    var x = _snapToGrid ? Snap(p.X) : p.X;
                    var y = _snapToGrid ? Snap(p.Y) : p.Y;
                    _toolService.AddPolygonPoint(_newElement, _toolService.CurrentTool,
                        new Shim.SKPoint((float)x, (float)y), _snapToGrid, Snap);
                    SvgView.SkSvg!.FromSvgDocument(_document);
                    _toolService.UpdateElement(_newElement, _toolService.CurrentTool,
                        _newStart, new Shim.SKPoint((float)x, (float)y), _snapToGrid, Snap);
                    SvgView.SkSvg!.FromSvgDocument(_document);
                    UpdateSelectedDrawable();
                    SvgView.InvalidateVisual();
                }
            }
            return;
        }

        if ((_toolService.CurrentTool == Tool.Line || _toolService.CurrentTool == Tool.Rect || _toolService.CurrentTool == Tool.Circle || _toolService.CurrentTool == Tool.Ellipse) &&
            e.GetCurrentPoint(SvgView).Properties.IsLeftButtonPressed)
        {
            if (SvgView.SkSvg is { } && SvgView.TryGetPicturePoint(point, out var sp) && _document is { })
            {
                SaveUndoState();
                SvgElement parent = _selectedSvgElement is SvgGroup grp ? grp : _document!;
                _newStart = new Shim.SKPoint(sp.X, sp.Y);
                _newElement = _toolService.CreateElement(_toolService.CurrentTool, parent, _newStart);
                if (_newElement is { })
                {
                    parent.Children.Add(_newElement);
                    SvgView.SkSvg!.FromSvgDocument(_document);
                    SaveExpandedNodes();
                    BuildTree();
                    SelectNodeFromElement(_newElement);
                    _selectedElement = _newElement;
                    _selectedSvgElement = _newElement;
                    UpdateSelectedDrawable();
                    LoadProperties(_newElement);
                    SvgView.InvalidateVisual();
                    _creating = true;
                    e.Pointer.Capture(SvgView);
                    return;
                }
            }
        }
        if (e.GetCurrentPoint(SvgView).Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _panStart = point;
            e.Pointer.Capture(SvgView);
            return;
        }
        if (SvgView.SkSvg is { } skSvg && SvgView.TryGetPicturePoint(point, out var pp))
        {
            if (_selectedDrawable is null && _selectedElement is { })
                UpdateSelectedDrawable();
            // check handles first
            if (_selectedDrawable is { } sel && _selectedElement is { })
            {
                var bounds = GetBoundsInfo(sel);
                var handle = HitHandle(bounds, new SK.SKPoint(pp.X, pp.Y), out var center);
                if (handle >= 0)
                {
                    if (handle == 8)
                    {
                        SaveUndoState();
                        _isRotating = true;
                        _rotateElement = _selectedElement;
                        _rotateStart = new SK.SKPoint(pp.X, pp.Y);
                        _rotateCenter = center;
                        _startAngle = GetRotation(_rotateElement);
                        e.Pointer.Capture(SvgView);
                        return;
                    }
                    SaveUndoState();
                    _isResizing = true;
                    _resizeElement = _selectedElement;
                    _resizeHandle = handle;
                    _resizeStart = new Shim.SKPoint(pp.X, pp.Y);
                    _startRect = SvgView.SkSvg!.SkiaModel.ToSKRect(sel.GeometryBounds);
                    if (_startRect.Width == 0)
                        _startRect.Right = _startRect.Left + 0.01f;
                    if (_startRect.Height == 0)
                        _startRect.Bottom = _startRect.Top + 0.01f;
                    _resizeMatrix = sel.TotalTransform;
                    if (!_resizeMatrix.TryInvert(out _resizeInverse))
                        _resizeInverse = Shim.SKMatrix.CreateIdentity();
                    _resizeStartLocal = _resizeInverse.MapPoint(_resizeStart);
                    (_startTransX, _startTransY) = GetTranslation(_resizeElement);
                    (_startScaleX, _startScaleY) = GetScale(_resizeElement);
                    e.Pointer.Capture(SvgView);
                    return;
                }
            }

            if (_pathService.IsEditing)
            {
                var idx = _pathService.HitPoint(new SK.SKPoint(pp.X, pp.Y), HandleSize, GetCanvasScale());
                if (idx >= 0)
                {
                    _pathService.ActivePoint = idx;
                    // start dragging path point
                    e.Pointer.Capture(SvgView);
                    return;
                }
            }
            if (_polyEditing)
            {
                var idx = HitPolyPoint(new SK.SKPoint(pp.X, pp.Y));
                if (idx >= 0)
                {
                    _activePolyPoint = idx;
                    e.Pointer.Capture(SvgView);
                    return;
                }
            }

            var hits = skSvg.HitTestElements(pp).OfType<SvgVisualElement>().ToList();
            if (hits.Count > 0)
            {
                if (_toolService.CurrentTool == Tool.PathSelect && e.GetCurrentPoint(SvgView).Properties.IsLeftButtonPressed)
                {
                    var pathEl = hits.OfType<SvgPath>().FirstOrDefault();
                    if (pathEl is not null && _document is { })
                    {
                        SaveUndoState();
                        var drawable = skSvg.HitTestDrawables(pp).FirstOrDefault(d => d.Element == pathEl);
                        if (drawable is { })
                        {
                            _pathService.Start(pathEl, drawable);
                            UpdateSelectedDrawable();
                            SvgView.InvalidateVisual();
                            return;
                        }
                    }
                }
                else if (_toolService.CurrentTool == Tool.PolygonSelect && e.GetCurrentPoint(SvgView).Properties.IsLeftButtonPressed)
                {
                    var polyEl = hits.OfType<SvgPolygon>().FirstOrDefault();
                    if (polyEl is not null && _document is { })
                    {
                        SaveUndoState();
                        var drawable = skSvg.HitTestDrawables(pp).FirstOrDefault(d => d.Element == polyEl);
                        if (drawable is { })
                        {
                            StartPolyEditing(polyEl, drawable);
                            UpdateSelectedDrawable();
                            SvgView.InvalidateVisual();
                            return;
                        }
                    }
                }
                else if (_toolService.CurrentTool == Tool.PolylineSelect && e.GetCurrentPoint(SvgView).Properties.IsLeftButtonPressed)
                {
                    var polyEl = hits.OfType<SvgPolyline>().FirstOrDefault();
                    if (polyEl is not null && _document is { })
                    {
                        SaveUndoState();
                        var drawable = skSvg.HitTestDrawables(pp).FirstOrDefault(d => d.Element == polyEl);
                        if (drawable is { })
                        {
                            StartPolyEditing(polyEl, drawable);
                            UpdateSelectedDrawable();
                            SvgView.InvalidateVisual();
                            return;
                        }
                    }
                }
                _pressHits = hits;
                _pressPoint = new Shim.SKPoint(pp.X, pp.Y);
                _pressElement = _selectedElement != null && hits.Contains(_selectedElement)
                    ? _selectedElement
                    : hits[0];
                _pressRightButton = e.GetCurrentPoint(SvgView).Properties.IsRightButtonPressed;
                _pendingPress = true;
                if (_pathService.IsEditing && _pathService.EditPath != _pressElement)
                    _pathService.Stop();
                if (_polyEditing && _editPolyElement != _pressElement)
                    StopPolyEditing();
            }
            else
            {
                _selectedDrawable = skSvg.HitTestDrawables(pp).FirstOrDefault();
                _selectedElement = null;
                _selectedSvgElement = null;
                if (_pathService.IsEditing)
                    _pathService.Stop();
                if (_polyEditing)
                    StopPolyEditing();
                UpdateSelectedDrawable();
                SvgView.InvalidateVisual();
            }
        }
    }

    private void SvgView_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(SvgView);

        if (_creating && SvgView.TryGetPicturePoint(point, out var cp) && _newElement is { })
        {
            var cur = new Shim.SKPoint(cp.X, cp.Y);
            _toolService.UpdateElement(_newElement, _toolService.CurrentTool,
                _newStart, cur, _snapToGrid, Snap);
            SvgView.SkSvg!.FromSvgDocument(_document);
            UpdateSelectedDrawable();
            if (_pathService.IsEditing)
                _pathService.EditDrawable = _selectedDrawable;
            SvgView.InvalidateVisual();
            return;
        }

        if (_pendingPress && SvgView.SkSvg is { } hitSvg && SvgView.TryGetPicturePoint(point, out var ppp))
        {
            var dx = Math.Abs(ppp.X - _pressPoint.X);
            var dy = Math.Abs(ppp.Y - _pressPoint.Y);
            if (dx > DragThreshold || dy > DragThreshold)
            {
                _pendingPress = false;
                if (_pressElement is { })
                {
                    if (_selectedElement != _pressElement)
                    {
                        _selectedElement = _pressElement;
                        _selectedDrawable = hitSvg.HitTestDrawables(_pressPoint).FirstOrDefault(d => d.Element == _selectedElement);
                        _selectedSvgElement = _pressElement;
                        LoadProperties(_selectedSvgElement);
                        SelectNodeFromElement(_selectedSvgElement);
                        UpdateSelectedDrawable();
                    }
                    StartDrag(_pressElement, new Shim.SKPoint(ppp.X, ppp.Y), e.Pointer);
                    SvgView.InvalidateVisual();
                }
            }
        }

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

        if (_pathService.ActivePoint >= 0 && SvgView.TryGetPicturePoint(point, out var ppe))
        {
            var loc = _pathService.PathInverse.MapPoint(new Shim.SKPoint((float)ppe.X, (float)ppe.Y));
            if (_snapToGrid)
                loc = new Shim.SKPoint(Snap(loc.X), Snap(loc.Y));
            _pathService.MoveActivePoint(loc);
            if (_document is { })
            {
                SvgView.SkSvg!.FromSvgDocument(_document);
                UpdateSelectedDrawable();
                if (_pathService.IsEditing)
                    _pathService.EditDrawable = _selectedDrawable;
                SvgView.InvalidateVisual();
            }
            return;
        }
        if (_activePolyPoint >= 0 && SvgView.TryGetPicturePoint(point, out var ppep))
        {
            var loc = _polyInverse.MapPoint(new Shim.SKPoint((float)ppep.X, (float)ppep.Y));
            if (_snapToGrid)
                loc = new Shim.SKPoint(Snap(loc.X), Snap(loc.Y));
            _polyPoints[_activePolyPoint] = loc;
            UpdatePolyPoint(_activePolyPoint, loc);
            SvgView.SkSvg!.FromSvgDocument(_document);
            UpdateSelectedDrawable();
            if (_polyEditing)
                _editPolyDrawable = _selectedDrawable;
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
        if (_pendingPress && SvgView.SkSvg is { } skSvg)
        {
            _pendingPress = false;
            if (_pressHits.Count > 0)
            {
                var hits = _pressHits;
                if (Math.Abs(_pressPoint.X - _lastHitPoint.X) < 1 && Math.Abs(_pressPoint.Y - _lastHitPoint.Y) < 1)
                {
                    var idx = hits.IndexOf(_selectedElement!);
                    if (_pressRightButton)
                        _hitIndex = idx >= 0 ? (idx - 1 + hits.Count) % hits.Count : hits.Count - 1;
                    else
                        _hitIndex = idx >= 0 ? (idx + 1) % hits.Count : 0;
                }
                else
                {
                    _hitIndex = 0;
                    _lastHitPoint = _pressPoint;
                }
                _hitElements = hits;
                _selectedElement = _hitElements[_hitIndex];
                _selectedDrawable = skSvg.HitTestDrawables(_pressPoint).FirstOrDefault(d => d.Element == _selectedElement);
                _selectedSvgElement = _selectedElement;
                if (_pathService.IsEditing && _pathService.EditPath != _selectedElement)
                    _pathService.Stop();
                LoadProperties(_selectedSvgElement);
                SelectNodeFromElement(_selectedSvgElement);
                UpdateSelectedDrawable();
                SvgView.InvalidateVisual();
            }
            else
            {
                _selectedDrawable = null;
                _selectedElement = null;
                _selectedSvgElement = null;
                UpdateSelectedDrawable();
                SvgView.InvalidateVisual();
            }
        }
        else if (_isDragging)
        {
            _isDragging = false;
            if (_dragElement is { })
            {
                SaveUndoState();
                LoadProperties(_dragElement);
            }
        }
        else if (_creating)
        {
            if (_toolService.CurrentTool == Tool.Polygon || _toolService.CurrentTool == Tool.Polyline)
            {
                // ignore regular release while drawing polygons
            }
            else
            {
                _creating = false;
                if (_newElement is { })
                {
                    LoadProperties(_newElement);
                    _selectedElement = _newElement;
                    _selectedSvgElement = _newElement;
                    UpdateSelectedDrawable();
                }
                _newElement = null;
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
        else if (_pathService.ActivePoint >= 0)
        {
            _pathService.ActivePoint = -1;
            if (_pathService.IsEditing && _pathService.EditPath is { })
            {
                SaveUndoState();
                LoadProperties(_pathService.EditPath);
            }
        }
        else if (_activePolyPoint >= 0)
        {
            _activePolyPoint = -1;
            if (_polyEditing && _editPolyElement is { })
            {
                SaveUndoState();
                LoadProperties(_editPolyElement);
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
        var factor = e.Delta.Y > 0 ? 1.1 : 0.9;
        SvgView.ZoomToPoint(SvgView.Zoom * factor, e.GetPosition(SvgView));
        SvgView.InvalidateVisual();
    }

    private void ApplyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedSvgElement is null || _document is null)
            return;
        SaveUndoState();
        _propertiesService.ApplyAll(_selectedSvgElement);
        SvgView.SkSvg!.FromSvgDocument(_document);
        UpdateSelectedDrawable();
        SaveExpandedNodes();
        BuildTree();
        if (_selectedSvgElement is { })
            SelectNodeFromElement(_selectedSvgElement);
        SvgView.InvalidateVisual();
    }

    private void PropertyEntryOnPropertyChanged(PropertyEntry entry)
    {
        if (_selectedSvgElement is { } && _document is { })
        {
            SaveUndoState();
            entry.Apply(_selectedSvgElement);
            SvgView.SkSvg!.FromSvgDocument(_document);
            UpdateSelectedDrawable();
            _propertiesService.UpdateIdList(_document);
            SvgView.InvalidateVisual();
        }
    }

    private void VisibilityToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { DataContext: SvgNode node } btn && _document is { })
        {
            node.IsVisible = btn.IsChecked == true;
            SvgView.SkSvg!.FromSvgDocument(_document);
            UpdateSelectedDrawable();
            SvgView.InvalidateVisual();
        }
    }

    private void StartDrag(SvgVisualElement element, Shim.SKPoint start, IPointer pointer)
    {
        if (GetDragProperties(element, out var props))
        {
            SaveUndoState();
            _dragProps = props;
            _isDragging = true;
            _dragStart = start;
            _dragElement = element;
            pointer.Capture(SvgView);
            return;
        }

        SaveUndoState();
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
        pointer.Capture(SvgView);
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


    private float GetCanvasScale(SkiaSharp.SKCanvas? canvas = null)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            if (canvas is { })
            {
                // The canvas has the transformation matrix already applied.
                // Assume uniform scaling and use the X scale to determine the
                // current draw scale.
                return canvas.TotalMatrix.ScaleX;
            }
            return 1f;
        }

        if (SvgView.SkSvg?.Picture is { } pic)
        {
            var viewPort = new Rect(SvgView.Bounds.Size);
            var sourceSize = new Size(pic.CullRect.Width, pic.CullRect.Height);
            var scale = SvgView.Stretch.CalculateScaling(SvgView.Bounds.Size, sourceSize, SvgView.StretchDirection);
            var scaledSize = sourceSize * scale;
            var destRect = viewPort.CenterRect(new Rect(scaledSize)).Intersect(viewPort);
            var sourceRect = new Rect(sourceSize).CenterRect(new Rect(destRect.Size / scale));
            var sx = destRect.Width / sourceRect.Width;
            return (float)(sx * SvgView.Zoom);
        }

        return (float)SvgView.Zoom;
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
        var normal = len > 0
            ? new SK.SKPoint(edge.Y / len, -edge.X / len)
            : new SK.SKPoint(0, -1);
        var scale = GetCanvasScale();
        var rotHandle = new SK.SKPoint(topMid.X + normal.X * 20f / scale, topMid.Y + normal.Y * 20f / scale);
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
        var scale = GetCanvasScale();
        var hs = HandleSize / 2f / scale;
        var handlePts = new[] { b.TL, b.TopMid, b.TR, b.RightMid, b.BR, b.BottomMid, b.BL, b.LeftMid };
        for (int i = 0; i < handlePts.Length; i++)
        {
            var r = new SK.SKRect(handlePts[i].X - hs, handlePts[i].Y - hs, handlePts[i].X + hs, handlePts[i].Y + hs);
            if (r.Contains(pt))
                return i;
        }
        if (SK.SKPoint.Distance(b.RotHandle, pt) <= HandleSize / scale)
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
        if (element.Transforms == null)
            element.Transforms = new SvgTransformCollection();
        var rot = element.Transforms.OfType<SvgRotate>().FirstOrDefault();
        if (rot != null)
        {
            rot.Angle = angle;
            rot.CenterX = center.X;
            rot.CenterY = center.Y;
        }
        else
        {
            element.Transforms.Add(new SvgRotate(angle, center.X, center.Y));
        }
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
        if (element.Transforms == null)
            element.Transforms = new SvgTransformCollection();
        var tr = element.Transforms.OfType<SvgTranslate>().FirstOrDefault();
        if (tr != null)
        {
            tr.X = x;
            tr.Y = y;
        }
        else
        {
            element.Transforms.Add(new SvgTranslate(x, y));
        }
    }

    private (float X, float Y) GetScale(SvgVisualElement? element)
    {
        if (element?.Transforms is { } t)
        {
            var sc = t.OfType<SvgScale>().FirstOrDefault();
            if (sc is { })
                return (sc.X, sc.Y);
        }
        return (1f, 1f);
    }

    private void SetScale(SvgVisualElement element, float x, float y)
    {
        if (element.Transforms == null)
            element.Transforms = new SvgTransformCollection();
        var sc = element.Transforms.OfType<SvgScale>().FirstOrDefault();
        if (sc != null)
        {
            sc.X = x;
            sc.Y = y;
        }
        else
        {
            element.Transforms.Add(new SvgScale(x, y));
        }
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
            case SvgPath path:
                ResizePath(path, handle, dx, dy);
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

        void ResizePath(SvgPath p, int h, float ddx, float ddy)
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
            if (_snapToGrid)
            {
                x = Snap(x); y = Snap(y); w = Snap(w); hgt = Snap(hgt);
            }
            if (w == 0) w = 0.01f; if (hgt == 0) hgt = 0.01f;
            var sx = w / _startRect.Width;
            var sy = hgt / _startRect.Height;
            var tx = x - _startRect.Left;
            var ty = y - _startRect.Top;
            SetScale(p, _startScaleX * sx, _startScaleY * sy);
            SetTranslation(p, _startTransX + tx, _startTransY + ty);
        }
    }

    private void UpdateNewElement(Shim.SKPoint current)
    {
        if (_newElement is null)
            return;
        _toolService.UpdateElement(_newElement, _toolService.CurrentTool,
            _newStart, current, _snapToGrid, Snap);
    }

    private void SvgView_OnDraw(object? sender, SKSvgDrawEventArgs e)
    {
        var scale = GetCanvasScale(e.Canvas);

        if (_snapToGrid && _showGrid && _gridSize > 0 && SvgView.SkSvg?.Picture is { } pic)
        {
            using var gridPaint = new SK.SKPaint
            {
                IsAntialias = false,
                Style = SK.SKPaintStyle.Stroke,
                Color = SK.SKColors.LightGray,
                StrokeWidth = 1f / scale
            };
            var bounds = pic.CullRect;
            for (float x = 0; x <= bounds.Width; x += (float)_gridSize)
                e.Canvas.DrawLine(x, 0, x, bounds.Height, gridPaint);
            for (float y = 0; y <= bounds.Height; y += (float)_gridSize)
                e.Canvas.DrawLine(0, y, bounds.Width, y, gridPaint);
        }

        if (_selectedDrawable is null)
            return;
        using var paint = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Stroke,
            Color = _boundsColor,
            StrokeWidth = 1f / scale
        };
        var hs = HandleSize / 2f / scale;
        var size = HandleSize / scale;
        using var fill = new SK.SKPaint { IsAntialias = true, Style = SK.SKPaintStyle.Fill, Color = SK.SKColors.White };
        var info = GetBoundsInfo(_selectedDrawable);
        if ( _toolService.CurrentTool != Tool.PathSelect &&  _toolService.CurrentTool != Tool.PolygonSelect &&  _toolService.CurrentTool != Tool.PolylineSelect)
        {
            using (var path = new SK.SKPath())
            {
                path.MoveTo(info.TL);
                path.LineTo(info.TR);
                path.LineTo(info.BR);
                path.LineTo(info.BL);
                path.Close();
                e.Canvas.DrawPath(path, paint);
            }

            var pts = new[] { info.TL, info.TopMid, info.TR, info.RightMid, info.BR, info.BottomMid, info.BL, info.LeftMid };
            foreach (var pt in pts)
            {
                e.Canvas.DrawRect(pt.X - hs, pt.Y - hs, size, size, fill);
                e.Canvas.DrawRect(pt.X - hs, pt.Y - hs, size, size, paint);
            }

            e.Canvas.DrawLine(info.TopMid, info.RotHandle, paint);
            e.Canvas.DrawCircle(info.RotHandle, hs, fill);
            e.Canvas.DrawCircle(info.RotHandle, hs, paint);
        }

        if (_pathService.IsEditing && _pathService.EditDrawable == _selectedDrawable)
        {
            using var segPaint = new SK.SKPaint
            {
                IsAntialias = true,
                Style = SK.SKPaintStyle.Stroke,
                Color = _segmentColor,
                StrokeWidth = 2f / scale,
                PathEffect = SK.SKPathEffect.CreateDash(new float[] { 6f / scale, 4f / scale }, 0)
            };
            using var ctrlPaint = new SK.SKPaint
            {
                IsAntialias = true,
                Style = SK.SKPaintStyle.Stroke,
                Color = _controlColor,
                StrokeWidth = 1f / scale,
                PathEffect = SK.SKPathEffect.CreateDash(new float[] { 4f / scale, 4f / scale }, 0)
            };

            if (_pathService.EditPath is { } path)
            {
                var segs = path.PathData;
                var cur = new Shim.SKPoint();
                var start = new Shim.SKPoint();
                bool haveStart = false;
                foreach (var seg in segs)
                {
                    switch (seg)
                    {
                        case SvgMoveToSegment mv:
                            cur = new Shim.SKPoint(mv.End.X, mv.End.Y);
                            if (!haveStart)
                            {
                                start = cur;
                                haveStart = true;
                            }
                            else
                            {
                                start = cur;
                            }
                            break;
                        case SvgLineSegment ln:
                            var lnEnd = new Shim.SKPoint(ln.End.X, ln.End.Y);
                            var scur = _pathService.PathMatrix.MapPoint(cur);
                            var sln = _pathService.PathMatrix.MapPoint(lnEnd);
                            e.Canvas.DrawLine(scur.X, scur.Y, sln.X, sln.Y, segPaint);
                            cur = lnEnd;
                            break;
                        case SvgCubicCurveSegment c:
                            var c1 = new Shim.SKPoint(c.FirstControlPoint.X, c.FirstControlPoint.Y);
                            var c2 = new Shim.SKPoint(c.SecondControlPoint.X, c.SecondControlPoint.Y);
                            var ce = new Shim.SKPoint(c.End.X, c.End.Y);
                            scur = _pathService.PathMatrix.MapPoint(cur);
                            var sc1 = _pathService.PathMatrix.MapPoint(c1);
                            var sc2 = _pathService.PathMatrix.MapPoint(c2);
                            var sce = _pathService.PathMatrix.MapPoint(ce);
                            e.Canvas.DrawLine(scur.X, scur.Y, sc1.X, sc1.Y, ctrlPaint);
                            e.Canvas.DrawLine(sce.X, sce.Y, sc2.X, sc2.Y, ctrlPaint);
                            e.Canvas.DrawLine(scur.X, scur.Y, sc1.X, sc1.Y, segPaint);
                            e.Canvas.DrawLine(sc1.X, sc1.Y, sc2.X, sc2.Y, segPaint);
                            e.Canvas.DrawLine(sc2.X, sc2.Y, sce.X, sce.Y, segPaint);
                            cur = ce;
                            break;
                        case SvgQuadraticCurveSegment q:
                            var qp = new Shim.SKPoint(q.ControlPoint.X, q.ControlPoint.Y);
                            var qe = new Shim.SKPoint(q.End.X, q.End.Y);
                            scur = _pathService.PathMatrix.MapPoint(cur);
                            var sqp = _pathService.PathMatrix.MapPoint(qp);
                            var sqe = _pathService.PathMatrix.MapPoint(qe);
                            e.Canvas.DrawLine(scur.X, scur.Y, sqp.X, sqp.Y, ctrlPaint);
                            e.Canvas.DrawLine(sqe.X, sqe.Y, sqp.X, sqp.Y, ctrlPaint);
                            e.Canvas.DrawLine(scur.X, scur.Y, sqp.X, sqp.Y, segPaint);
                            e.Canvas.DrawLine(sqp.X, sqp.Y, sqe.X, sqe.Y, segPaint);
                            cur = qe;
                            break;
                        case SvgArcSegment a:
                            var ae = new Shim.SKPoint(a.End.X, a.End.Y);
                            scur = _pathService.PathMatrix.MapPoint(cur);
                            var sae = _pathService.PathMatrix.MapPoint(ae);
                            e.Canvas.DrawLine(scur.X, scur.Y, sae.X, sae.Y, segPaint);
                            cur = ae;
                            break;
                        case SvgClosePathSegment _:
                            scur = _pathService.PathMatrix.MapPoint(cur);
                            var sstart = _pathService.PathMatrix.MapPoint(start);
                            e.Canvas.DrawLine(scur.X, scur.Y, sstart.X, sstart.Y, segPaint);
                            cur = start;
                            break;
                    }
                }
            }

            foreach (var p in _pathService.PathPoints)
            {
                var pt = _pathService.PathMatrix.MapPoint(p.Point);
                e.Canvas.DrawRect(pt.X - hs, pt.Y - hs, size, size, fill);
                e.Canvas.DrawRect(pt.X - hs, pt.Y - hs, size, size, paint);
            }
        }
        if (_polyEditing && _editPolyDrawable == _selectedDrawable)
        {
            using var segPaint = new SK.SKPaint
            {
                IsAntialias = true,
                Style = SK.SKPaintStyle.Stroke,
                Color = _segmentColor,
                StrokeWidth = 2f / scale,
                PathEffect = SK.SKPathEffect.CreateDash(new float[] { 6f / scale, 4f / scale }, 0)
            };
            var last = _polyPoints.Count - 1;
            for (int i = 0; i < last; i++)
            {
                var a = _polyMatrix.MapPoint(_polyPoints[i]);
                var b = _polyMatrix.MapPoint(_polyPoints[i + 1]);
                e.Canvas.DrawLine(a.X, a.Y, b.X, b.Y, segPaint);
            }
            if (!_editPolyline && _polyPoints.Count > 2)
            {
                var a = _polyMatrix.MapPoint(_polyPoints[^1]);
                var b = _polyMatrix.MapPoint(_polyPoints[0]);
                e.Canvas.DrawLine(a.X, a.Y, b.X, b.Y, segPaint);
            }
            foreach (var p in _polyPoints)
            {
                var pt = _polyMatrix.MapPoint(p);
                e.Canvas.DrawRect(pt.X - hs, pt.Y - hs, size, size, fill);
                e.Canvas.DrawRect(pt.X - hs, pt.Y - hs, size, size, paint);
            }
        }
    }


    private void BuildTree()
    {
        UpdateIdList();
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

    private void ApplyPropertyFilter()
    {
        _propertiesService.ApplyFilter(_propertyFilter);
    }

    private void UpdateIdList()
    {
        _propertiesService.UpdateIdList(_document);
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
                   GetElementName(el.GetType()).Contains(_filter, StringComparison.OrdinalIgnoreCase);
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
            if (_toolService.CurrentTool == Tool.PathSelect && _selectedElement is SvgPath path && _selectedDrawable is { })
            {
                if (!_pathService.IsEditing || _pathService.EditPath != path)
                    _pathService.Start(path, _selectedDrawable!);
            }
            else if (_toolService.CurrentTool == Tool.PolygonSelect && _selectedElement is SvgPolygon pg && _selectedDrawable is { })
            {
                if (!_polyEditing || _editPolyElement != pg)
                    StartPolyEditing(pg, _selectedDrawable!);
            }
            else if (_toolService.CurrentTool == Tool.PolylineSelect && _selectedElement is SvgPolyline pl && _selectedDrawable is { })
            {
                if (!_polyEditing || _editPolyElement != pl)
                    StartPolyEditing(pl, _selectedDrawable!);
            }
            else if (_pathService.IsEditing)
            {
                _pathService.Stop();
            }
            else if (_polyEditing)
            {
                StopPolyEditing();
            }
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


    private void StartPolyEditing(SvgVisualElement element, DrawableBase drawable)
    {
        _polyEditing = true;
        _editPolyElement = element;
        _editPolyline = element is SvgPolyline;
        _editPolyDrawable = drawable;
        _selectedElement = element;
        _selectedSvgElement = element;
        _polyPoints.Clear();
        var pts = _editPolyline ? ((SvgPolyline)element).Points : ((SvgPolygon)element).Points;
        for (int i = 0; i + 1 < pts.Count; i += 2)
            _polyPoints.Add(new Shim.SKPoint(pts[i].Value, pts[i + 1].Value));
        _polyMatrix = drawable.TotalTransform;
        if (!_polyMatrix.TryInvert(out _polyInverse))
            _polyInverse = Shim.SKMatrix.CreateIdentity();
        UpdateSelectedDrawable();
        LoadProperties(element);
    }

    private void StopPolyEditing()
    {
        _polyEditing = false;
        _editPolyElement = null;
        _editPolyDrawable = null;
        _activePolyPoint = -1;
        _polyPoints.Clear();
        UpdateSelectedDrawable();
    }

    private void UpdatePolyPoint(int index, Shim.SKPoint pt)
    {
        if (_editPolyElement is null)
            return;
        var pts = _editPolyline ? ((SvgPolyline)_editPolyElement).Points : ((SvgPolygon)_editPolyElement).Points;
        if (index * 2 + 1 >= pts.Count)
            return;
        pts[index * 2] = new SvgUnit(pts[index * 2].Type, pt.X);
        pts[index * 2 + 1] = new SvgUnit(pts[index * 2 + 1].Type, pt.Y);
    }

    private void AddPolyPoint(Shim.SKPoint pt)
    {
        if (_editPolyElement is null)
            return;
        SaveUndoState();
        var local = _polyInverse.MapPoint(pt);
        var pts = _editPolyline ? ((SvgPolyline)_editPolyElement).Points : ((SvgPolygon)_editPolyElement).Points;
        pts.Add(new SvgUnit(SvgUnitType.User, local.X));
        pts.Add(new SvgUnit(SvgUnitType.User, local.Y));
        _polyPoints.Add(local);
    }

    private void RemoveActivePolyPoint()
    {
        if (_editPolyElement is null || _activePolyPoint < 0 || _activePolyPoint >= _polyPoints.Count)
            return;
        SaveUndoState();
        var pts = _editPolyline ? ((SvgPolyline)_editPolyElement).Points : ((SvgPolygon)_editPolyElement).Points;
        var idx = _activePolyPoint * 2;
        if (idx + 1 < pts.Count)
        {
            pts.RemoveAt(idx + 1);
            pts.RemoveAt(idx);
        }
        _polyPoints.RemoveAt(_activePolyPoint);
        _activePolyPoint = -1;
    }

    private int HitPolyPoint(SK.SKPoint pt)
    {
        var scale = GetCanvasScale();
        var hs = HandleSize / 2f / scale;
        for (int i = 0; i < _polyPoints.Count; i++)
        {
            var p = _polyMatrix.MapPoint(_polyPoints[i]);
            var r = new SK.SKRect(p.X - hs, p.Y - hs, p.X + hs, p.Y + hs);
            if (r.Contains(pt))
                return i;
        }
        return -1;
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

    private void UpdateTitle()
    {
        var name = string.IsNullOrEmpty(_currentFile) ? string.Empty : $" - {Path.GetFileName(_currentFile)}";
        Title = $"AvalonDraw{name}";
    }

    private void RestoreFromString(string xml)
    {
        _document = SvgService.FromSvg(xml);
        SvgView.SkSvg!.FromSvgDocument(_document);
        SaveExpandedNodes();
        BuildTree();
    }

    internal static string GetElementName(Type type)
    {
        var attr = type.GetCustomAttributes(typeof(SvgElementAttribute), true)
            .OfType<SvgElementAttribute>()
            .FirstOrDefault(a => !string.IsNullOrEmpty(a.ElementName));
        return attr?.ElementName ?? type.Name;
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
        var names = _elementTypes.Select(GetElementName).ToList();
        var win = new InsertElementWindow(names);
        var result = await win.ShowDialog<string?>(this);
        if (result is null)
            return;
        var type = _elementTypes.FirstOrDefault(t => GetElementName(t) == result);
        if (type is null || _document is null)
            return;
        SaveUndoState();
        var element = (SvgElement)Activator.CreateInstance(type)!;
        var parent = _selectedSvgElement ?? _document;
        parent.Children.Add(element);
        SvgView.SkSvg!.FromSvgDocument(_document);
        BuildTree();
    }

    private void InsertElementFromMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: Type type } && DocumentTree.SelectedItem is SvgNode node)
        {
            InsertElement(node.Element, type);
        }
    }

    private ContextMenu BuildTreeContextMenu()
    {
        var menu = new ContextMenu();
        foreach (var group in _elementTypes.GroupBy(t => t.Namespace ?? string.Empty).OrderBy(g => g.Key))
        {
            var groupItem = new MenuItem { Header = string.IsNullOrEmpty(group.Key) ? "Svg" : group.Key };
            foreach (var type in group.OrderBy(t => GetElementName(t)))
            {
                var item = new MenuItem { Header = GetElementName(type), Tag = type };
                item.Click += InsertElementFromMenu_Click;
                groupItem.Items.Add(item);
            }
            menu.Items.Add(groupItem);
        }
        return menu;
    }

    private void InsertElement(SvgElement parent, Type type)
    {
        if (_document is null)
            return;
        SaveUndoState();
        var element = (SvgElement)Activator.CreateInstance(type)!;
        parent.Children.Add(element);
        SvgView.SkSvg!.FromSvgDocument(_document);
        BuildTree();
        SelectNodeFromElement(element);
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
        _currentFile = null;
        UpdateTitle();
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

    private async void EditContentMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedSvgElement is SvgTextBase txt && _document is { })
        {
            var win = new TextEditorWindow(txt.Text);
            var result = await win.ShowDialog<string?>(this);
            if (result is not null)
            {
                SaveUndoState();
                txt.Text = result;
                SvgView.SkSvg!.FromSvgDocument(_document);
                UpdateSelectedDrawable();
                LoadProperties(txt);
                SvgView.InvalidateVisual();
            }
        }
    }

    private async void PreviewMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null)
            return;
        var win = new PreviewWindow(_document);
        await win.ShowDialog(this);
    }

    private void ResetFileMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentFile))
            LoadDocument(_currentFile);
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

    private void SelectToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        _toolService.SetTool(Tool.Select);
    }

    private void PathToolButton_Click(object? sender, RoutedEventArgs e)
    {
        _toolService.SetTool(Tool.PathSelect);
        if (_selectedElement is SvgPath path && _selectedDrawable is { })
        {
            if (!_pathService.IsEditing || _pathService.EditPath != path)
                _pathService.Start(path, _selectedDrawable);
            SvgView.InvalidateVisual();
        }
    }

    private void PolygonSelectToolButton_Click(object? sender, RoutedEventArgs e)
    {
        _toolService.SetTool(Tool.PolygonSelect);
        if (_selectedElement is SvgPolygon poly && _selectedDrawable is { })
        {
            if (!_polyEditing || _editPolyElement != poly)
                StartPolyEditing(poly, _selectedDrawable);
            SvgView.InvalidateVisual();
        }
    }

    private void PolylineSelectToolButton_Click(object? sender, RoutedEventArgs e)
    {
        _toolService.SetTool(Tool.PolylineSelect);
        if (_selectedElement is SvgPolyline pl && _selectedDrawable is { })
        {
            if (!_polyEditing || _editPolyElement != pl)
                StartPolyEditing(pl, _selectedDrawable);
            SvgView.InvalidateVisual();
        }
    }

    private void LineToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        _toolService.SetTool(Tool.Line);
    }
    private void RectToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        _toolService.SetTool(Tool.Rect);
    }
    private void CircleToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        _toolService.SetTool(Tool.Circle);
    }
    private void EllipseToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        _toolService.SetTool(Tool.Ellipse);
    }

    private void PolygonToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        _toolService.SetTool(Tool.Polygon);
    }

    private void PolylineToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        _toolService.SetTool(Tool.Polyline);
    }

    private void SelectToolMenuItem_Click(object? sender, RoutedEventArgs e) => SelectToolButton_Click(sender, e);
    private void PathToolMenuItem_Click(object? sender, RoutedEventArgs e) => PathToolButton_Click(sender, e);
    private void PolygonSelectToolMenuItem_Click(object? sender, RoutedEventArgs e) => PolygonSelectToolButton_Click(sender, e);
    private void PolylineSelectToolMenuItem_Click(object? sender, RoutedEventArgs e) => PolylineSelectToolButton_Click(sender, e);
    private void LineToolMenuItem_Click(object? sender, RoutedEventArgs e) => LineToolButton_Click(sender, e);
    private void RectToolMenuItem_Click(object? sender, RoutedEventArgs e) => RectToolButton_Click(sender, e);
    private void CircleToolMenuItem_Click(object? sender, RoutedEventArgs e) => CircleToolButton_Click(sender, e);
    private void EllipseToolMenuItem_Click(object? sender, RoutedEventArgs e) => EllipseToolButton_Click(sender, e);
    private void PolygonToolMenuItem_Click(object? sender, RoutedEventArgs e) => PolygonToolButton_Click(sender, e);
    private void PolylineToolMenuItem_Click(object? sender, RoutedEventArgs e) => PolylineToolButton_Click(sender, e);

    private async void SettingsMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_snapToGrid, _gridSize, _showGrid);
        var result = await win.ShowDialog<bool?>(this);
        if (result == true)
        {
            _snapToGrid = win.SnapToGrid;
            _showGrid = win.ShowGrid;
            _gridSize = win.GridSize;
            SvgView.InvalidateVisual();
        }
    }

    private void MainWindow_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_pathService.IsEditing)
            {
                _pathService.Stop();
                SvgView.SkSvg!.FromSvgDocument(_document);
                UpdateSelectedDrawable();
                SvgView.InvalidateVisual();
            }
            else
            {
                _selectedDrawable = null;
                _selectedElement = null;
                _selectedSvgElement = null;
                DocumentTree.SelectedItem = null;
                SvgView.InvalidateVisual();
            }
            e.Handled = true;
            return;
        }

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (ctrl)
        {
            switch (e.Key)
            {
                case Key.N:
                    NewMenuItem_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.O:
                    OpenMenuItem_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.S:
                    SaveMenuItem_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.E:
                    ExportElementMenuItem_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.T:
                    EditTextMenuItem_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.R:
                    ResetFileMenuItem_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.I:
                    InsertElementMenuItem_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.C:
                    CopyElementMenuItem_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.V:
                    PasteElementMenuItem_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.Z:
                    UndoMenuItem_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.Y:
                    RedoMenuItem_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
            }
        }
        else
        {
            switch (e.Key)
            {
                case Key.Delete:
                    if (_pathService.IsEditing && _pathService.ActivePoint >= 0)
                    {
                        SaveUndoState();
                        _pathService.RemoveActivePoint();
                        if (_document is { })
                        {
                            SvgView.SkSvg!.FromSvgDocument(_document);
                            UpdateSelectedDrawable();
                            SvgView.InvalidateVisual();
                        }
                    }
                    else if (_polyEditing && _activePolyPoint >= 0)
                        RemoveActivePolyPoint();
                    else
                        RemoveElementMenuItem_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.F5:
                    PreviewMenuItem_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D1:
                    SelectToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D2:
                    PathToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D3:
                    PolygonSelectToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D4:
                    PolylineSelectToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D5:
                    LineToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D6:
                    RectToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D7:
                    CircleToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D8:
                    EllipseToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D9:
                    PolygonToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D0:
                    PolylineToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
            }
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

    private void PropertyFilterBox_OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox tb)
        {
            _propertyFilter = tb.Text ?? string.Empty;
            ApplyPropertyFilter();
        }
    }

    private void DocumentTree_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(DocumentTree);
        if (e.GetCurrentPoint(DocumentTree).Properties.IsRightButtonPressed)
        {
            if (DocumentTree.InputHitTest(pos) is StyledElement c)
            {
                while (c != null && c.DataContext is not SvgNode && c.Parent is StyledElement p)
                    c = p;
                if (c?.DataContext is SvgNode node)
                    DocumentTree.SelectedItem = node;
            }
            return;
        }

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
        if (!e.Data.Contains("SvgNode"))
        {
            HideDropIndicator();
            return;
        }

        e.DragEffects = DragDropEffects.Move;

        var pos = e.GetPosition(DocumentTree);
        if (DocumentTree.InputHitTest(pos) is Control c)
        {
            while (c != null && c.DataContext is not SvgNode && c.Parent is Control p)
                c = p;
            if (c is { DataContext: SvgNode node })
            {
                var topLeft = c.TranslatePoint(new Point(0, 0), DocumentTree) ?? new Point();
                var h = c.Bounds.Height;
                var localY = pos.Y - topLeft.Y;
                _dropTarget = node;
                if (localY < h * 0.25)
                {
                    _dropPosition = DropPosition.Before;
                    ShowDropIndicator(topLeft.Y, topLeft.X, _dropPosition);
                }
                else if (localY > h * 0.75)
                {
                    _dropPosition = DropPosition.After;
                    ShowDropIndicator(topLeft.Y + h, topLeft.X, _dropPosition);
                }
                else
                {
                    _dropPosition = DropPosition.Inside;
                    ShowDropIndicator(topLeft.Y + h / 2, topLeft.X, _dropPosition);
                }
            }
            else
            {
                HideDropIndicator();
            }
        }
        else
        {
            HideDropIndicator();
        }
    }

    private void DocumentTree_OnDragLeave(object? sender, RoutedEventArgs e)
    {
        HideDropIndicator();
        _dropTarget = null;
        _dropPosition = DropPosition.None;
    }

    private void DocumentTree_OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains("SvgNode") || _dropTarget is null)
        {
            HideDropIndicator();
            return;
        }

        if (e.Data.Get("SvgNode") is SvgNode node)
        {
            var target = _dropTarget;
            if (node == target || IsAncestor(node, target) || node.Parent is null)
            {
                HideDropIndicator();
                return;
            }

            SaveUndoState();
            SaveExpandedNodes();
            node.Parent.Element.Children.Remove(node.Element);

            switch (_dropPosition)
            {
                case DropPosition.Before:
                    if (target.Parent?.Element is SvgElement parentBefore)
                    {
                        var index = parentBefore.Children.IndexOf(target.Element);
                        if (index < 0) index = parentBefore.Children.Count;
                        if (index >= parentBefore.Children.Count)
                            parentBefore.Children.Add(node.Element);
                        else
                            parentBefore.Children.Insert(index, node.Element);
                    }
                    break;
                case DropPosition.After:
                    if (target.Parent?.Element is SvgElement parentAfter)
                    {
                        var index = parentAfter.Children.IndexOf(target.Element);
                        if (index < 0) index = parentAfter.Children.Count - 1;
                        if (index + 1 >= parentAfter.Children.Count)
                            parentAfter.Children.Add(node.Element);
                        else
                            parentAfter.Children.Insert(index + 1, node.Element);
                    }
                    break;
                default:
                    target.Element.Children.Add(node.Element);
                    break;
            }

            SvgView.SkSvg!.FromSvgDocument(_document);
            BuildTree();
            SelectNodeFromElement(node.Element);
            SvgView.InvalidateVisual();
        }

        HideDropIndicator();
        _dropTarget = null;
        _dropPosition = DropPosition.None;
    }

    private void ShowDropIndicator(double y, double left, DropPosition pos)
    {
        if (_dropIndicator is null)
            return;
        _dropIndicator.Margin = new Thickness(left, y - 1, 0, 0);
        _dropIndicator.Background = pos switch
        {
            DropPosition.Inside => _dropInsideBrush,
            DropPosition.Before => _dropBeforeBrush,
            DropPosition.After => _dropAfterBrush,
            _ => _dropBeforeBrush
        };
        _dropIndicator.IsVisible = true;
    }

    private void HideDropIndicator()
    {
        if (_dropIndicator is null)
            return;
        _dropIndicator.IsVisible = false;
    }

}

public class SvgNode : INotifyPropertyChanged
{
    public SvgElement Element { get; }
    public ObservableCollection<SvgNode> Children { get; } = new();
    public SvgNode? Parent { get; }
    public string Label { get; }
    private bool _isVisible;

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
                return;
            _isVisible = value;
            Element.Visibility = value ? "visible" : "hidden";
            if (value)
                Element.Display = "inline";
            else
                Element.Display = "none";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SvgNode(SvgElement element, SvgNode? parent)
    {
        Element = element;
        Parent = parent;
        var name = MainWindow.GetElementName(element.GetType());
        Label = string.IsNullOrEmpty(element.ID)
            ? name
            : $"{name} ({element.ID})";
        var vis = !string.Equals(element.Visibility, "hidden", StringComparison.OrdinalIgnoreCase) &&
                  !string.Equals(element.Visibility, "collapse", StringComparison.OrdinalIgnoreCase);
        var disp = !string.Equals(element.Display, "none", StringComparison.OrdinalIgnoreCase);
        _isVisible = vis && disp;
    }
}

public class EyeIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var vis = value as bool? ?? false;
        return Application.Current?.FindResource(vis ? "EyeOpen" : "EyeClosed");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}

public class BooleanStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => bool.TryParse(value as string, out var b) ? b : (object?)false;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? b.ToString() : null;
}

public class ColorStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && Color.TryParse(s, out var c))
            return c;
        return Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Color c ? c.ToString() : null;
}
