using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AvalonDraw.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using Svg;
using Svg.Model;
using Svg.Model.Drawables;
using Svg.Model.Services;
using Svg.Pathing;
using Svg.Skia;
using Svg.Transforms;
using Shim = ShimSkiaSharp;
using SK = SkiaSharp;
using Tool = AvalonDraw.Services.ToolService.Tool;

namespace AvalonDraw;

public partial class MainWindow : Window
{
    private struct DragInfo
    {
        public SvgVisualElement Element;
        public List<(PropertyInfo Prop, SvgUnit Unit, char Axis)>? Props;
        public float TextX;
        public float TextY;
        public float TransX;
        public float TransY;
    }
    private DrawableBase? _selectedDrawable;
    private SvgVisualElement? _selectedElement;
    private SvgElement? _selectedSvgElement;
    private SvgDocument? _document;
    private string? _currentFile;

    private readonly PropertiesService _propertiesService = new();
    private readonly LayerService _layerService = new();
    private readonly PatternService _patternService = new();
    private readonly BrushService _brushService = new();
    private readonly SymbolService _symbolService = new();
    private readonly AppearanceService _appearanceService = new();
    public ObservableCollection<PropertyEntry> Properties => _propertiesService.Properties;
    public ObservableCollection<PropertyEntry> FilteredProperties => _propertiesService.FilteredProperties;
    private ObservableCollection<SvgNode> Nodes { get; } = new();
    public ObservableCollection<string> Ids => _propertiesService.Ids;
    public ObservableCollection<ArtboardInfo> Artboards { get; } = new();
    public ObservableCollection<LayerService.LayerEntry> Layers => _layerService.Layers;
    public ObservableCollection<PatternService.PatternEntry> Patterns => _patternService.Patterns;
    public ObservableCollection<BrushService.BrushEntry> BrushStyles => _brushService.Brushes;
    public ObservableCollection<SymbolService.SymbolEntry> Symbols => _symbolService.Symbols;
    public ObservableCollection<AppearanceService.StyleEntry> Styles => _appearanceService.Styles;
    private ArtboardInfo? _selectedArtboard;
    private LayerService.LayerEntry? _selectedLayer;
    private PatternService.PatternEntry? _selectedPattern;
    private BrushService.BrushEntry? _selectedBrush;
    private SymbolService.SymbolEntry? _selectedSymbol;
    private AppearanceService.StyleEntry? _selectedStyle;
    private ListBox? _artboardList;
    private TreeView? _layerTree;
    private ListBox? _swatchList;
    private ListBox? _brushList;
    private ListBox? _symbolList;
    private ListBox? _styleList;
    private readonly HashSet<string> _expandedIds = new();
    private HashSet<string> _filterBackup = new();

    private TextBlock? _panZoomLabel;

    private SvgElement? _clipboard;
    private string? _clipboardXml;

    private bool _wireframeEnabled;
    private bool _filtersDisabled;
    private bool _snapToGrid;
    private bool _showGrid;
    private double _gridSize = 10.0;
    private bool _includeHidden;


    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();
    private readonly List<Type> _elementTypes = typeof(SvgElement).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(SvgElement)) && !t.IsAbstract)
        .OrderBy(t => GetElementName(t)).ToList();

    private ContextMenu? _treeMenu;
    private ContextMenu? _pathMenu;
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
    private List<DragInfo>? _multiDragInfos;

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
    private bool _isSkewing;
    private float _startSkewX;
    private float _startSkewY;
    private bool _skewMode;

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
    private readonly RenderingService _renderingService;
    private readonly AlignService _alignService = new();
    private readonly SelectionService _selectionService = new();

    private bool _polyEditing;
    private SvgVisualElement? _editPolyElement;
    private DrawableBase? _editPolyDrawable;
    private bool _editPolyline;
    private readonly List<Shim.SKPoint> _polyPoints = new();
    private int _activePolyPoint = -1;
    private Shim.SKMatrix _polyMatrix;
    private Shim.SKMatrix _polyInverse;

    private bool _boxSelecting;
    private Point _boxStart;
    private Point _boxEnd;
    private SK.SKPoint _boxStartPicture;
    private SK.SKPoint _boxEndPicture;
    private readonly List<SvgVisualElement> _multiSelected = new();
    private readonly List<DrawableBase> _multiDrawables = new();
    private SK.SKRect _multiBounds = SK.SKRect.Empty;
    private readonly List<Shim.SKPoint> _freehandPoints = new();
    private TextBox? _strokeWidthBox;

    public MainWindow()
    {
        _renderingService = new RenderingService(_pathService, _toolService);
        Resources["PropertyEditorTemplate"] = new FuncDataTemplate<PropertyEntry>((entry, ns) =>
        {
            if (entry.Property?.Name == nameof(SvgVisualElement.Fill))
            {
                var list = new ObservableCollection<string>(_patternService.Patterns
                    .Where(p => !string.IsNullOrEmpty(p.Pattern.ID))
                    .Select(p => $"url(#{p.Pattern.ID})"));
                list.Insert(0, "New Pattern");
                var box = new AutoCompleteBox
                {
                    ItemsSource = list,
                    MinimumPrefixLength = 0,
                    VerticalAlignment = VerticalAlignment.Center
                };
                box[!AutoCompleteBox.TextProperty] = new Binding("Value") { Mode = BindingMode.TwoWay };
                box.GotFocus += (_, _) => box.IsDropDownOpen = true;
                box.SelectionChanged += async (_, _) =>
                {
                    if (box.SelectedItem as string == "New Pattern")
                    {
                        var dlg = new PatternEditorWindow();
                        var result = await dlg.ShowDialog<bool>(this);
                        if (result && dlg.Result is { } pat)
                        {
                            if (string.IsNullOrEmpty(pat.ID))
                                pat.ID = $"pattern{_patternService.Patterns.Count + 1}";
                            if (_document is { })
                                _patternService.AddPattern(_document, pat);
                            list.Add($"url(#{pat.ID})");
                            box.SelectedItem = $"url(#{pat.ID})";
                        }
                    }
                };
                return box;
            }
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
            if (entry is GradientMeshEntry meshEntry)
            {
                var btn = new Button { Content = "Edit Mesh", VerticalAlignment = VerticalAlignment.Center };
                btn.Click += async (_, _) =>
                {
                    var dlg = new GradientMeshEditorWindow(meshEntry.Mesh);
                    await dlg.ShowDialog(this);
                };
                return btn;
            }
            if (entry is GradientStopsEntry gEntry)
            {
                var btn = new Button { Content = entry.Value ?? "Edit", VerticalAlignment = VerticalAlignment.Center };
                btn.Click += async (_, _) =>
                {
                    var dlg = new GradientEditorWindow(gEntry.Stops);
                    var result = await dlg.ShowDialog<bool>(this);
                    if (result)
                    {
                        gEntry.Stops.Clear();
                        foreach (var s in dlg.Result)
                            gEntry.Stops.Add(s);
                        gEntry.UpdateValue();
                        gEntry.NotifyChanged();
                    }
                };
                return btn;
            }
            if (entry is StrokeProfileEntry spEntry)
            {
                var btn = new Button { Content = entry.Value ?? "Edit", VerticalAlignment = VerticalAlignment.Center };
                btn.Click += async (_, _) =>
                {
                    var dlg = new StrokeProfileEditorWindow(spEntry.Points);
                    var result = await dlg.ShowDialog<bool>(this);
                    if (result)
                    {
                        spEntry.Points.Clear();
                        foreach (var p in dlg.Result)
                            spEntry.Points.Add(p);
                        spEntry.UpdateValue();
                        spEntry.NotifyChanged();
                    }
                };
                return btn;
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
        _toolService.ToolChanged += ToolServiceOnToolChanged;
        _toolService.SetTool(Tool.Select);
        _treeMenu = BuildTreeContextMenu();
        _pathMenu = BuildPathContextMenu();
        DocumentTree.ContextMenu = _treeMenu;
        _dropIndicator = this.FindControl<Border>("DropIndicator");
        _panZoomLabel = this.FindControl<TextBlock>("PanZoomLabel");
        _artboardList = this.FindControl<ListBox>("ArtboardList");
        _layerTree = this.FindControl<TreeView>("LayerTree");
        _swatchList = this.FindControl<ListBox>("SwatchList");
        _brushList = this.FindControl<ListBox>("BrushList");
        _symbolList = this.FindControl<ListBox>("SymbolList");
        _styleList = this.FindControl<ListBox>("StyleList");
        _strokeWidthBox = this.FindControl<TextBox>("StrokeWidthBox");
        if (_strokeWidthBox is { })
            _strokeWidthBox.Text = _toolService.CurrentStrokeWidth.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _wireframeEnabled = false;
        _filtersDisabled = false;
        _snapToGrid = false;
        _showGrid = false;
        _gridSize = 10.0;
        _includeHidden = false;
        _selectionService.SnapToGrid = _snapToGrid;
        _selectionService.GridSize = _gridSize;
        SvgView.Wireframe = false;
        if (SvgView.SkSvg is { } initSvg)
            initSvg.IgnoreAttributes = DrawAttributes.None;
        LoadDocument("Assets/__tiger.svg");
        UpdateStatusBar();
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
            if (_document is null)
            {
                Console.WriteLine($"Failed to load SVG resource '{path}'.");
            }
            SvgView.Path = uri.ToString();
        }
        else if (File.Exists(path))
        {
            _document = SvgService.Open(path);
            if (_document is null)
            {
                Console.WriteLine($"Failed to load SVG file '{path}'.");
            }
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
        else if (_document is null)
        {
            return;
        }

        SvgView.Zoom = 1.0;
        SvgView.PanX = 0;
        SvgView.PanY = 0;
        UpdateStatusBar();

        SaveExpandedNodes();
        _currentFile = path;
        UpdateTitle();
        BuildTree();
        UpdateArtboards();
        UpdateLayers();
        UpdatePatterns();
        UpdateBrushes();
        UpdateStyles();
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

    private async void PlaceImageMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null)
            return;
        var dialog = new OpenFileDialog
        {
            Filters = new()
            {
                new FileDialogFilter { Name = "Images", Extensions = { "png", "jpg", "jpeg", "bmp" } },
                new FileDialogFilter { Name = "All", Extensions = { "*" } }
            }
        };
        var result = await dialog.ShowAsync(this);
        var file = result?.FirstOrDefault();
        if (string.IsNullOrEmpty(file))
            return;
        byte[] data;
        try
        {
            data = await File.ReadAllBytesAsync(file);
        }
        catch
        {
            return;
        }
        var ext = Path.GetExtension(file).Trim('.').ToLowerInvariant();
        if (ext == "jpg")
            ext = "jpeg";
        var href = $"data:image/{ext};base64,{Convert.ToBase64String(data)}";
        _toolService.ImageHref = href;
        _toolService.SetTool(Tool.Image);
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
        if (_toolService.CurrentTool == Tool.MultiSelect && e.GetCurrentPoint(SvgView).Properties.IsLeftButtonPressed)
        {
            if (_pathService.IsEditing)
                _pathService.Stop();
            _boxSelecting = true;
            _boxStart = point;
            _boxEnd = point;
            if (SvgView.TryGetPicturePoint(point, out var sp))
            {
                _boxStartPicture = new SK.SKPoint((float)sp.X, (float)sp.Y);
                _boxEndPicture = _boxStartPicture;
            }
            e.Pointer.Capture(SvgView);
            return;
        }
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
                    new Shim.SKPoint((float)rp.X, (float)rp.Y), _snapToGrid, _selectionService.Snap);
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
                    var sx = _snapToGrid ? _selectionService.Snap(p.X) : p.X;
                    var sy = _snapToGrid ? _selectionService.Snap(p.Y) : p.Y;
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
                            _newStart, _newStart, _snapToGrid, _selectionService.Snap);
                        SvgView.SkSvg!.FromSvgDocument(_document);
                        _creating = true;
                    }
                }
                else if (_newElement is { })
                {
                    SaveUndoState();
                    var x = _snapToGrid ? _selectionService.Snap(p.X) : p.X;
                    var y = _snapToGrid ? _selectionService.Snap(p.Y) : p.Y;
                    _toolService.AddPolygonPoint(_newElement, _toolService.CurrentTool,
                        new Shim.SKPoint((float)x, (float)y), _snapToGrid, _selectionService.Snap);
                    SvgView.SkSvg!.FromSvgDocument(_document);
                    _toolService.UpdateElement(_newElement, _toolService.CurrentTool,
                        _newStart, new Shim.SKPoint((float)x, (float)y), _snapToGrid, _selectionService.Snap);
                    SvgView.SkSvg!.FromSvgDocument(_document);
                    UpdateSelectedDrawable();
                    SvgView.InvalidateVisual();
                }
            }
            return;
        }

        if ((_toolService.CurrentTool == Tool.Line || _toolService.CurrentTool == Tool.Rect || _toolService.CurrentTool == Tool.Circle || _toolService.CurrentTool == Tool.Ellipse ||
             _toolService.CurrentTool == Tool.Text || _toolService.CurrentTool == Tool.TextPath || _toolService.CurrentTool == Tool.TextArea ||
             _toolService.CurrentTool == Tool.Symbol || _toolService.CurrentTool == Tool.Image ||
             _toolService.CurrentTool == Tool.PathLine || _toolService.CurrentTool == Tool.PathCubic || _toolService.CurrentTool == Tool.PathQuadratic || _toolService.CurrentTool == Tool.PathArc || _toolService.CurrentTool == Tool.PathMove) &&
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
                        _startAngle = _selectionService.GetRotation(_rotateElement);
                        e.Pointer.Capture(SvgView);
                        return;
                    }
                    SaveUndoState();
                    if (_skewMode)
                        _isSkewing = true;
                    else
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
                    (_startTransX, _startTransY) = _selectionService.GetTranslation(_resizeElement);
                    (_startScaleX, _startScaleY) = _selectionService.GetScale(_resizeElement);
                    (_startSkewX, _startSkewY) = _selectionService.GetSkew(_resizeElement);
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
                var idx = _selectionService.HitPolyPoint(_polyPoints, _polyMatrix, new SK.SKPoint(pp.X, pp.Y), GetCanvasScale());
                if (idx >= 0)
                {
                    _activePolyPoint = idx;
                    e.Pointer.Capture(SvgView);
                    return;
                }
            }

            var hits = skSvg.HitTestElements(pp).OfType<SvgVisualElement>().ToList();
            if (!_includeHidden)
                hits = hits.Where(IsElementVisible).ToList();
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
                if (_toolService.CurrentTool == Tool.MultiSelect && _multiSelected.Count > 0 && !_multiBounds.IsEmpty)
                {
                    if (_multiBounds.Contains(pp.X, pp.Y))
                    {
                        StartDrag(_multiSelected[0], new Shim.SKPoint(pp.X, pp.Y), e.Pointer);
                        SvgView.InvalidateVisual();
                        return;
                    }
                }

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

        if (_boxSelecting)
        {
            _boxEnd = point;
            if (SvgView.TryGetPicturePoint(point, out var sp))
            {
                _boxEndPicture = new SK.SKPoint((float)sp.X, (float)sp.Y);
            }
            SvgView.InvalidateVisual();
            return;
        }

        if (_creating && SvgView.TryGetPicturePoint(point, out var cp) && _newElement is { })
        {
            var cur = new Shim.SKPoint(cp.X, cp.Y);
            if (_toolService.CurrentTool == Tool.Freehand)
            {
                var last = _freehandPoints[^1];
                if (Math.Abs(last.X - cur.X) > DragThreshold || Math.Abs(last.Y - cur.Y) > DragThreshold)
                {
                    _freehandPoints.Add(cur);
                    _toolService.AddFreehandPoint(_newElement, cur, _snapToGrid, _selectionService.Snap);
                    SvgView.SkSvg!.FromSvgDocument(_document);
                    UpdateSelectedDrawable();
                    SvgView.InvalidateVisual();
                }
            }
            else
            {
                _toolService.UpdateElement(_newElement, _toolService.CurrentTool,
                    _newStart, cur, _snapToGrid, _selectionService.Snap);
                SvgView.SkSvg!.FromSvgDocument(_document);
                UpdateSelectedDrawable();
                if (_pathService.IsEditing)
                    _pathService.EditDrawable = _selectedDrawable;
                SvgView.InvalidateVisual();
            }
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
            UpdateStatusBar();
            SvgView.InvalidateVisual();
            return;
        }

        if (_pathService.ActivePoint >= 0 && SvgView.TryGetPicturePoint(point, out var ppe))
        {
            var loc = _pathService.PathInverse.MapPoint(new Shim.SKPoint((float)ppe.X, (float)ppe.Y));
            if (_snapToGrid)
                loc = new Shim.SKPoint(_selectionService.Snap(loc.X), _selectionService.Snap(loc.Y));
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
                loc = new Shim.SKPoint(_selectionService.Snap(loc.X), _selectionService.Snap(loc.Y));
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
            if (_isDragging && _multiDragInfos is { } infos)
            {
                var dx = pp.X - _dragStart.X;
                var dy = pp.Y - _dragStart.Y;
                foreach (var info in infos)
                {
                    if (info.Props is { } pr)
                    {
                        foreach (var (Prop, Unit, Axis) in pr)
                        {
                            var delta = Axis == 'x' ? dx : dy;
                            var val = Unit.Value + delta;
                            if (_snapToGrid)
                                val = _selectionService.Snap(val);
                            Prop.SetValue(info.Element, new SvgUnit(Unit.Type, val));
                        }
                    }
                    else if (info.Element is SvgTextBase txt)
                    {
                        if (txt.X.Count > 0)
                            txt.X[0] = new SvgUnit(txt.X[0].Type, _snapToGrid ? _selectionService.Snap(info.TextX + dx) : info.TextX + dx);
                        if (txt.Y.Count > 0)
                            txt.Y[0] = new SvgUnit(txt.Y[0].Type, _snapToGrid ? _selectionService.Snap(info.TextY + dy) : info.TextY + dy);
                    }
                    else
                    {
                        var tx = info.TransX + dx;
                        var ty = info.TransY + dy;
                        if (_snapToGrid)
                        {
                            tx = _selectionService.Snap(tx);
                            ty = _selectionService.Snap(ty);
                        }
                        _selectionService.SetTranslation(info.Element, tx, ty);
                    }
                }
                SvgView.SkSvg!.FromSvgDocument(_document);
                UpdateSelectedDrawable();
                SvgView.InvalidateVisual();
            }
            else if (_isDragging && _dragElement is { } dragEl)
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
                            val = _selectionService.Snap(val);
                        Prop.SetValue(dragEl, new SvgUnit(Unit.Type, val));
                    }
                }
                else if (dragEl is SvgTextBase txt)
                {
                    if (txt.X.Count > 0)
                        txt.X[0] = new SvgUnit(txt.X[0].Type, _snapToGrid ? _selectionService.Snap(_dragTextX + dx) : _dragTextX + dx);
                    if (txt.Y.Count > 0)
                        txt.Y[0] = new SvgUnit(txt.Y[0].Type, _snapToGrid ? _selectionService.Snap(_dragTextY + dy) : _dragTextY + dy);
                }
                else
                {
                    var tx = _dragTransX + dx;
                    var ty = _dragTransY + dy;
                    if (_snapToGrid)
                    {
                        tx = _selectionService.Snap(tx);
                        ty = _selectionService.Snap(ty);
                    }
                    _selectionService.SetTranslation(dragEl, tx, ty);
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
                _selectionService.ResizeElement(_resizeElement, _resizeHandle, dx, dy, _startRect, _startTransX, _startTransY, _startScaleX, _startScaleY);
                SvgView.SkSvg!.FromSvgDocument(_document);
                UpdateSelectedDrawable();
                SvgView.InvalidateVisual();
            }
            else if (_isSkewing && _resizeElement is { })
            {
                var local = _resizeInverse.MapPoint(new Shim.SKPoint(skp.X, skp.Y));
                var dx = local.X - _resizeStartLocal.X;
                var dy = local.Y - _resizeStartLocal.Y;
                _selectionService.SkewElement(_resizeElement, _resizeHandle, dx, dy, _startRect, _startSkewX, _startSkewY);
                SvgView.SkSvg!.FromSvgDocument(_document);
                UpdateSelectedDrawable();
                SvgView.InvalidateVisual();
            }
            else if (_isRotating && _rotateElement is { })
            {
                var a1 = Math.Atan2(_rotateStart.Y - _rotateCenter.Y, _rotateStart.X - _rotateCenter.X);
                var a2 = Math.Atan2(skp.Y - _rotateCenter.Y, skp.X - _rotateCenter.X);
                var delta = (float)((a2 - a1) * 180.0 / Math.PI);
                _selectionService.SetRotation(_rotateElement, _startAngle + delta, _rotateCenter);
                SvgView.SkSvg!.FromSvgDocument(_document);
                UpdateSelectedDrawable();
                SvgView.InvalidateVisual();
            }
        }
    }

    private void SvgView_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_boxSelecting)
        {
            _boxSelecting = false;
            e.Pointer.Capture(null);
            if (SvgView.SkSvg is { } svg)
            {
                if (SvgView.TryGetPicturePoint(_boxStart, out var tp1))
                    _boxStartPicture = new SK.SKPoint((float)tp1.X, (float)tp1.Y);
                if (SvgView.TryGetPicturePoint(_boxEnd, out var tp2))
                    _boxEndPicture = new SK.SKPoint((float)tp2.X, (float)tp2.Y);
                var p1 = _boxStartPicture;
                var p2 = _boxEndPicture;
                var rect = new Shim.SKRect(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y), Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y));
                var elements = svg.HitTestElements(rect).OfType<SvgVisualElement>();
                if (!_includeHidden)
                    elements = elements.Where(IsElementVisible);
                var hits = elements.ToList();
                var mods = e.KeyModifiers;
                if ((mods & KeyModifiers.Shift) != 0)
                {
                    foreach (var el in hits)
                        if (!_multiSelected.Contains(el))
                            _multiSelected.Add(el);
                }
                else if ((mods & KeyModifiers.Control) != 0)
                {
                    foreach (var el in hits)
                    {
                        if (_multiSelected.Contains(el))
                            _multiSelected.Remove(el);
                        else
                            _multiSelected.Add(el);
                    }
                }
                else
                {
                    _multiSelected.Clear();
                    foreach (var el in hits)
                        _multiSelected.Add(el);
                }
                UpdateSelectedDrawable();
                if (_multiSelected.Count > 0)
                {
                    _selectedElement = _multiSelected[0];
                    _selectedSvgElement = _selectedElement;
                    LoadProperties(_selectedSvgElement);
                    SelectNodeFromElement(_selectedSvgElement);
                }
                SvgView.InvalidateVisual();
            }
            return;
        }
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
                var element = _hitElements[_hitIndex];
                if (_toolService.CurrentTool == Tool.MultiSelect)
                {
                    var mods = e.KeyModifiers;
                    if ((mods & KeyModifiers.Shift) != 0)
                    {
                        if (!_multiSelected.Contains(element))
                            _multiSelected.Add(element);
                    }
                    else if ((mods & KeyModifiers.Control) != 0)
                    {
                        if (_multiSelected.Contains(element))
                            _multiSelected.Remove(element);
                        else
                            _multiSelected.Add(element);
                    }
                    else
                    {
                        _multiSelected.Clear();
                        _multiSelected.Add(element);
                    }
                    _selectedElement = element;
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
                    _selectedElement = element;
                    _selectedDrawable = skSvg.HitTestDrawables(_pressPoint).FirstOrDefault(d => d.Element == _selectedElement);
                    _selectedSvgElement = _selectedElement;
                    if (_pathService.IsEditing && _pathService.EditPath != _selectedElement)
                        _pathService.Stop();
                    LoadProperties(_selectedSvgElement);
                    SelectNodeFromElement(_selectedSvgElement);
                    UpdateSelectedDrawable();
                    SvgView.InvalidateVisual();
                }
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
            if (_multiDragInfos is { })
            {
                SaveUndoState();
                if (_multiSelected.Count > 0)
                    LoadProperties(_multiSelected[0]);
                _multiDragInfos = null;
            }
            else if (_dragElement is { })
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
                    if (_toolService.CurrentTool == Tool.Freehand && _newElement is SvgPath fp && _freehandPoints.Count > 1)
                    {
                        fp.PathData = PathService.MakeSmooth(_freehandPoints);
                        if (_selectedBrush is { })
                            fp.CustomAttributes["stroke-profile"] = _selectedBrush.Profile.ToString();
                        SvgView.SkSvg!.FromSvgDocument(_document);
                    }
                    LoadProperties(_newElement);
                    _selectedElement = _newElement;
                    _selectedSvgElement = _newElement;
                    UpdateSelectedDrawable();
                }
                _newElement = null;
                _freehandPoints.Clear();
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
        else if (_isSkewing)
        {
            _isSkewing = false;
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
            UpdateStatusBar();
        }
        e.Pointer.Capture(null);
    }

    private void SvgView_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var factor = e.Delta.Y > 0 ? 1.1 : 0.9;
        SvgView.ZoomToPoint(SvgView.Zoom * factor, e.GetPosition(SvgView));
        UpdateStatusBar();
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
        if (_toolService.CurrentTool == Tool.MultiSelect && _multiSelected.Count > 1)
        {
            SaveUndoState();
            _multiDragInfos = new List<DragInfo>();
            foreach (var el in _multiSelected)
            {
                if (_selectionService.GetDragProperties(el, out var p))
                {
                    _multiDragInfos.Add(new DragInfo
                    {
                        Element = el,
                        Props = p
                    });
                }
                else if (el is SvgTextBase t)
                {
                    _multiDragInfos.Add(new DragInfo
                    {
                        Element = el,
                        Props = null,
                        TextX = t.X.Count > 0 ? t.X[0].Value : 0f,
                        TextY = t.Y.Count > 0 ? t.Y[0].Value : 0f
                    });
                }
                else
                {
                    var (tx, ty) = _selectionService.GetTranslation(el);
                    _multiDragInfos.Add(new DragInfo
                    {
                        Element = el,
                        Props = null,
                        TransX = tx,
                        TransY = ty
                    });
                }
            }
            _isDragging = true;
            _dragStart = start;
            _dragElement = null;
            _dragProps = null;
            pointer.Capture(SvgView);
            return;
        }

        if (_selectionService.GetDragProperties(element, out var props))
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
            var (tx, ty) = _selectionService.GetTranslation(element);
            _dragTransX = tx;
            _dragTransY = ty;
        }
        pointer.Capture(SvgView);
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
            var sc = (float)(sx * SvgView.Zoom);
            if (float.IsNaN(sc) || float.IsInfinity(sc) || sc <= 0f)
                sc = 1f;
            return sc;
        }

        return (float)SvgView.Zoom;
    }

    private static SK.SKPoint Mid(SK.SKPoint a, SK.SKPoint b) => new((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);

    private SelectionService.BoundsInfo GetBoundsInfo(DrawableBase drawable)
        => _selectionService.GetBoundsInfo(drawable, SvgView.SkSvg!, () => GetCanvasScale());

    private int HitHandle(SelectionService.BoundsInfo b, SK.SKPoint pt, out SK.SKPoint center)
        => _selectionService.HitHandle(b, pt, GetCanvasScale(), out center);
    private void UpdateNewElement(Shim.SKPoint current)
    {
        if (_newElement is null)
            return;
        _toolService.UpdateElement(_newElement, _toolService.CurrentTool,
            _newStart, current, _snapToGrid, _selectionService.Snap);
    }

    private void SvgView_OnDraw(object? sender, SKSvgDrawEventArgs e)
    {
        var scale = GetCanvasScale(e.Canvas);
        var artboardRect = _selectedArtboard is { } ab ? SK.SKRect.Create(0, 0, ab.Width, ab.Height) : (SK.SKRect?)null;
        _renderingService.Draw(e.Canvas,
            SvgView.SkSvg?.Picture,
            SvgView.SkSvg?.Picture?.CullRect,
            artboardRect,
            scale,
            _snapToGrid,
            _showGrid,
            _gridSize,
            Layers,
            _selectedLayer,
            _multiDrawables.Count > 0 ? (IList<DrawableBase>)_multiDrawables : (_selectedDrawable is { } d ? new List<DrawableBase> { d } : new List<DrawableBase>()),
            GetBoundsInfo,
            _polyEditing,
            _editPolyDrawable,
            _editPolyline,
            _polyPoints,
            _polyMatrix);

        if (_boxSelecting)
        {
            using var paint = new SK.SKPaint
            {
                IsAntialias = true,
                Style = SK.SKPaintStyle.Stroke,
                Color = SK.SKColors.SkyBlue,
                StrokeWidth = 1f / scale,
                PathEffect = SK.SKPathEffect.CreateDash(new float[] { 4f / scale, 4f / scale }, 0)
            };
            var sp1 = _boxStartPicture;
            var sp2 = _boxEndPicture;
            var r = SK.SKRect.Create(Math.Min(sp1.X, sp2.X), Math.Min(sp1.Y, sp2.Y), Math.Abs(sp2.X - sp1.X), Math.Abs(sp2.Y - sp1.Y));
            e.Canvas.DrawRect(r, paint);
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
        UpdateArtboards();
        UpdateLayers();
        UpdatePatterns();
        UpdateBrushes();
        UpdateSymbols();
        UpdateStyles();
    }

    private void ApplyPropertyFilter()
    {
        _propertiesService.ApplyFilter(_propertyFilter);
    }

    private void UpdateIdList()
    {
        _propertiesService.UpdateIdList(_document);
    }

    private void UpdateArtboards()
    {
        Artboards.Clear();
        if (_document is null)
            return;
        foreach (var g in _document.Children.OfType<SvgGroup>())
        {
            if (g.CustomAttributes.TryGetValue("data-artboard", out var flag) && string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase))
            {
                g.CustomAttributes.TryGetValue("width", out var wStr);
                g.CustomAttributes.TryGetValue("height", out var hStr);
                float.TryParse(wStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var w);
                float.TryParse(hStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var h);
                var name = string.IsNullOrEmpty(g.ID) ? $"Artboard {Artboards.Count + 1}" : g.ID;
                Artboards.Add(new ArtboardInfo(g, name, w, h));
            }
        }
        if (Artboards.Count > 0)
        {
            _selectedArtboard = Artboards[0];
            if (_artboardList is { })
                _artboardList.SelectedIndex = 0;
        }
        SvgView.InvalidateVisual();
    }

    private void UpdateLayers()
    {
        _layerService.Load(_document, SvgView.SkSvg?.Drawable as DrawableBase);
        if (Layers.Count > 0)
        {
            _selectedLayer = Layers[0];
            if (_layerTree is { })
                _layerTree.SelectedItem = _selectedLayer;
        }
    }

    private void UpdatePatterns()
    {
        _patternService.Load(_document);
        if (Patterns.Count > 0)
        {
            _selectedPattern = Patterns[0];
            if (_swatchList is { })
                _swatchList.SelectedIndex = 0;
        }
    }

    private void UpdateBrushes()
    {
        if (BrushStyles.Count > 0)
        {
            _selectedBrush = BrushStyles[0];
            _toolService.CurrentStrokeWidth = (float)_selectedBrush.Profile.Points.First().Width;
            if (_brushList is { })
                _brushList.SelectedIndex = 0;
        }
    }

    private void UpdateSymbols()
    {
        _symbolService.Load(_document);
        if (Symbols.Count > 0)
        {
            _selectedSymbol = Symbols[0];
            if (_symbolList is { })
                _symbolList.SelectedIndex = 0;
        }
    }

    private void UpdateStyles()
    {
        _appearanceService.Load(_document);
        if (Styles.Count > 0)
        {
            _selectedStyle = Styles[0];
            if (_styleList is { })
                _styleList.SelectedIndex = 0;
        }
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
        if (_multiSelected.Count > 0 && SvgView.SkSvg?.Drawable is DrawableBase root)
        {
            _multiDrawables.Clear();
            _multiBounds = SK.SKRect.Empty;
            foreach (var el in _multiSelected)
            {
                var d = FindDrawable(root, el);
                if (d is { })
                {
                    _multiDrawables.Add(d);
                    var b = SelectionService.GetBoundsRect(GetBoundsInfo(d));
                    _multiBounds = _multiBounds.IsEmpty ? b : SK.SKRect.Union(_multiBounds, b);
                }
            }
            _selectedDrawable = _multiDrawables.FirstOrDefault();
        }
        else if (_selectedElement is { } element && SvgView.SkSvg?.Drawable is DrawableBase drawable)
        {
            _selectedDrawable = FindDrawable(drawable, element);
            _multiDrawables.Clear();
            _multiBounds = SK.SKRect.Empty;
        }
        else
        {
            _selectedDrawable = null;
            _multiDrawables.Clear();
            _multiBounds = SK.SKRect.Empty;
        }
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

    private void UpdateStatusBar()
    {
        if (_panZoomLabel is { })
            _panZoomLabel.Text = $"Zoom: {SvgView.Zoom * 100:0}%  Pan: {SvgView.PanX:0},{SvgView.PanY:0}";
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

    private static bool IsElementVisible(SvgElement element)
    {
        var vis = !string.Equals(element.Visibility, "hidden", StringComparison.OrdinalIgnoreCase) &&
                  !string.Equals(element.Visibility, "collapse", StringComparison.OrdinalIgnoreCase);
        var disp = !string.Equals(element.Display, "none", StringComparison.OrdinalIgnoreCase);
        return vis && disp;
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

    private ContextMenu BuildPathContextMenu()
    {
        var menu = new ContextMenu();
        var offsetItem = new MenuItem { Header = "Offset Path" };
        offsetItem.Click += OffsetPathMenuItem_Click;
        var simplifyItem = new MenuItem { Header = "Simplify Path" };
        simplifyItem.Click += SimplifyPathMenuItem_Click;
        menu.Items.Add(offsetItem);
        menu.Items.Add(simplifyItem);
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
        if (_document is null)
            return;

        if (_toolService.CurrentTool == Tool.MultiSelect && _multiSelected.Count > 1)
        {
            SaveUndoState();
            foreach (var el in _multiSelected.ToList())
            {
                if (el.Parent is SvgElement p)
                    p.Children.Remove(el);
            }
            _multiSelected.Clear();
            _multiDrawables.Clear();
            _multiBounds = SK.SKRect.Empty;
            _selectedElement = null;
            _selectedSvgElement = null;
            _selectedDrawable = null;
            SvgView.SkSvg!.FromSvgDocument(_document);
            BuildTree();
            SvgView.InvalidateVisual();
            return;
        }

        if (_selectedSvgElement is SvgElement { Parent: { } parent })
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

    private void GroupMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null || _multiSelected.Count < 2)
            return;

        var first = _multiSelected[0];
        if (first.Parent is not SvgElement parent)
            return;
        SaveUndoState();
        var index = parent.Children.IndexOf(first);
        var group = new SvgGroup();
        parent.Children.Insert(index, group);

        foreach (var el in _multiSelected.ToList())
        {
            if (el.Parent is SvgElement p)
                p.Children.Remove(el);
            group.Children.Add(el);
        }

        _multiSelected.Clear();
        _multiDrawables.Clear();
        _multiBounds = SK.SKRect.Empty;
        _selectedSvgElement = group;
        _selectedElement = null;
        _selectedDrawable = null;
        SvgView.SkSvg!.FromSvgDocument(_document);
        BuildTree();
        SelectNodeFromElement(group);
    }

    private void UngroupMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null || _selectedSvgElement is not SvgGroup group)
            return;

        if (group.Parent is not SvgElement parent)
            return;

        SaveUndoState();
        var index = parent.Children.IndexOf(group);
        var children = group.Children.OfType<SvgElement>().ToList();
        parent.Children.Remove(group);
        foreach (var child in children)
        {
            parent.Children.Insert(index++, child);
        }

        _selectedSvgElement = parent;
        _selectedElement = parent as SvgVisualElement;
        SvgView.SkSvg!.FromSvgDocument(_document);
        BuildTree();
        SelectNodeFromElement(parent);
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
        var win = new TextEditorWindow(_document.GetXML(), _toolService.CurrentFontFamily,
            _toolService.CurrentFontWeight, _toolService.CurrentLetterSpacing, _toolService.CurrentWordSpacing);
        var ok = await win.ShowDialog<bool>(this);
        if (ok)
        {
            SaveUndoState();
            _document = SvgService.FromSvg(win.TextResult);
            SvgView.SkSvg!.FromSvgDocument(_document);
            BuildTree();
        }
    }

    private async void EditContentMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedSvgElement is SvgTextBase txt && _document is { })
        {
            var win = new TextEditorWindow(
                txt.Text,
                txt.FontFamily,
                txt.FontWeight,
                txt.LetterSpacing.Value,
                txt.WordSpacing.Value);
            var ok2 = await win.ShowDialog<bool>(this);
            if (ok2)
            {
                SaveUndoState();
                txt.Text = win.TextResult;
                txt.FontFamily = win.FontFamilyResult;
                txt.FontWeight = win.FontWeightResult;
                txt.LetterSpacing = new SvgUnit(SvgUnitType.User, win.LetterSpacingResult);
                txt.WordSpacing = new SvgUnit(SvgUnitType.User, win.WordSpacingResult);
                SvgView.SkSvg!.FromSvgDocument(_document);
                UpdateSelectedDrawable();
                LoadProperties(txt);
                SvgView.InvalidateVisual();
            }
        }
    }

    private async void CreateSymbolMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedSvgElement is not SvgVisualElement vis || _document is null)
            return;
        var nameWin = new SymbolNameWindow();
        var symId = await nameWin.ShowDialog<string?>(this);
        if (string.IsNullOrEmpty(symId))
            return;
        SaveUndoState();
        var symbol = new SvgSymbol { ID = symId };
        symbol.Children.Add((SvgElement)vis.DeepCopy());
        _symbolService.AddSymbol(_document, symbol);
        var use = new SvgUse { ReferencedElement = new Uri($"#{symId}", UriKind.Relative) };
        if (vis.Parent is SvgElement parent)
        {
            var idx = parent.Children.IndexOf(vis);
            parent.Children.Insert(idx, use);
            parent.Children.Remove(vis);
        }
        SvgView.SkSvg!.FromSvgDocument(_document);
        BuildTree();
        SelectNodeFromElement(use);
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

    private void SkewModeMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        _skewMode = !_skewMode;
    }

    private void ResetViewButton_Click(object? sender, RoutedEventArgs e)
    {
        SvgView.Zoom = 1.0;
        SvgView.PanX = 0;
        SvgView.PanY = 0;
        UpdateStatusBar();
        SvgView.InvalidateVisual();
    }

    private void ArtboardList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ArtboardInfo info)
        {
            _selectedArtboard = info;
            SvgView.InvalidateVisual();
        }
    }

    private void LayerTree_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is LayerService.LayerEntry info)
        {
            _selectedLayer = info;
            SvgView.InvalidateVisual();
        }
    }

    private void SwatchList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is PatternService.PatternEntry info)
        {
            _selectedPattern = info;
        }
    }

    private void BrushList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is BrushService.BrushEntry info)
        {
            _selectedBrush = info;
            _toolService.CurrentStrokeWidth = (float)info.Profile.Points.First().Width;
        }
    }

    private void SymbolList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is SymbolService.SymbolEntry info)
        {
            _selectedSymbol = info;
            if (_selectedSvgElement is SvgVisualElement ve)
            {
                SaveUndoState();
                info.Apply(ve);
                SvgView.SkSvg!.FromSvgDocument(_document);
                UpdateSelectedDrawable();
                SaveExpandedNodes();
                BuildTree();
                SelectNodeFromElement(ve);
                SvgView.InvalidateVisual();
            }
        }
    }

    private void StyleList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is AppearanceService.StyleEntry info)
        {
            _selectedStyle = info;
            if (_selectedSvgElement is SvgVisualElement ve)
            {
                SaveUndoState();
                info.Apply(ve);
                SvgView.SkSvg!.FromSvgDocument(_document);
                UpdateSelectedDrawable();
                SaveExpandedNodes();
                BuildTree();
                SelectNodeFromElement(ve);
                SvgView.InvalidateVisual();
            }
        }
    }

    private void SelectToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        _toolService.SetTool(Tool.Select);
    }

    private void MultiSelectToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        _toolService.SetTool(Tool.MultiSelect);
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

    private void TextToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        _toolService.SetTool(Tool.Text);
    }

    private void TextPathToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        if (_selectedSvgElement is SvgVisualElement ve && !string.IsNullOrEmpty(ve.ID))
            _toolService.ReferenceId = ve.ID;
        else
            _toolService.ReferenceId = null;
        _toolService.SetTool(Tool.TextPath);
    }

    private void TextAreaToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        if (_selectedSvgElement is SvgVisualElement ve && !string.IsNullOrEmpty(ve.ID))
            _toolService.ReferenceId = ve.ID;
        else
            _toolService.ReferenceId = null;
        _toolService.SetTool(Tool.TextArea);
    }

    private void PathLineToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        _toolService.SetTool(Tool.PathLine);
        _pathService.CurrentSegmentTool = PathService.SegmentTool.Line;
    }
    private void PathCubicToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        _toolService.SetTool(Tool.PathCubic);
        _pathService.CurrentSegmentTool = PathService.SegmentTool.Cubic;
    }
    private void PathQuadraticToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        _toolService.SetTool(Tool.PathQuadratic);
        _pathService.CurrentSegmentTool = PathService.SegmentTool.Quadratic;
    }
    private void PathArcToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        _toolService.SetTool(Tool.PathArc);
        _pathService.CurrentSegmentTool = PathService.SegmentTool.Arc;
    }
    private void PathMoveToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        _toolService.SetTool(Tool.PathMove);
        _pathService.CurrentSegmentTool = PathService.SegmentTool.Move;
    }

    private void FreehandToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        _toolService.SetTool(Tool.Freehand);
    }

    private async void SymbolToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing)
            _pathService.Stop();
        if (_document is null)
            return;
        var ids = _symbolService.Symbols
            .Select(s => s.Symbol.ID)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();
        if (ids.Count == 0)
            return;
        var win = new SymbolSelectWindow(ids!);
        var result = await win.ShowDialog<string?>(this);
        if (result is null)
            return;
        _toolService.SymbolId = result;
        _toolService.SetTool(Tool.Symbol);
    }

    private void MultiSelectToolMenuItem_Click(object? sender, RoutedEventArgs e) => MultiSelectToolButton_Click(sender, e);

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
    private void TextToolMenuItem_Click(object? sender, RoutedEventArgs e) => TextToolButton_Click(sender, e);
    private void TextPathToolMenuItem_Click(object? sender, RoutedEventArgs e) => TextPathToolButton_Click(sender, e);
    private void TextAreaToolMenuItem_Click(object? sender, RoutedEventArgs e) => TextAreaToolButton_Click(sender, e);
    private void PathLineToolMenuItem_Click(object? sender, RoutedEventArgs e) => PathLineToolButton_Click(sender, e);
    private void PathCubicToolMenuItem_Click(object? sender, RoutedEventArgs e) => PathCubicToolButton_Click(sender, e);
    private void PathQuadraticToolMenuItem_Click(object? sender, RoutedEventArgs e) => PathQuadraticToolButton_Click(sender, e);
    private void PathArcToolMenuItem_Click(object? sender, RoutedEventArgs e) => PathArcToolButton_Click(sender, e);
    private void PathMoveToolMenuItem_Click(object? sender, RoutedEventArgs e) => PathMoveToolButton_Click(sender, e);
    private void SymbolToolMenuItem_Click(object? sender, RoutedEventArgs e) => SymbolToolButton_Click(sender, e);
    private void FreehandToolMenuItem_Click(object? sender, RoutedEventArgs e) => FreehandToolButton_Click(sender, e);

    private async void SettingsMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_snapToGrid, _gridSize, _showGrid, _includeHidden);
        var result = await win.ShowDialog<bool?>(this);
        if (result == true)
        {
            _snapToGrid = win.SnapToGrid;
            _showGrid = win.ShowGrid;
            _gridSize = win.GridSize;
            _includeHidden = win.IncludeHidden;
            _selectionService.SnapToGrid = _snapToGrid;
            _selectionService.GridSize = _gridSize;
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
                case Key.V:
                    SelectToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.X:
                    MultiSelectToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.N:
                    PathToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.G:
                    PolygonSelectToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.Y:
                    PolylineSelectToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.L:
                    LineToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.R:
                    RectToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.C:
                    CircleToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.E:
                    EllipseToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.P:
                    PolygonToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.O:
                    PolylineToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.T:
                    TextToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.B:
                    PathLineToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.M:
                    PathMoveToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.Q:
                    PathQuadraticToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.A:
                    PathArcToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.J:
                    PathCubicToolButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.K:
                    CornerPointMenuItem_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.S:
                    SmoothPointMenuItem_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.U:
                    UniteMenuItem_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D:
                    SubtractMenuItem_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.I:
                    IntersectMenuItem_Click(this, new RoutedEventArgs());
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

    private void StrokeWidthBox_OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox tb &&
            float.TryParse(tb.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w))
        {
            _toolService.CurrentStrokeWidth = w;
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
                {
                    DocumentTree.SelectedItem = node;
                    DocumentTree.ContextMenu = node.Element is SvgPath ? _pathMenu : _treeMenu;
                }
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
                var w = c.Bounds.Width;
                if (localY < h * 0.25)
                {
                    _dropPosition = DropPosition.Before;
                    ShowDropIndicator(topLeft.Y, topLeft.X, w, _dropPosition);
                }
                else if (localY > h * 0.75)
                {
                    _dropPosition = DropPosition.After;
                    ShowDropIndicator(topLeft.Y + h, topLeft.X, w, _dropPosition);
                }
                else
                {
                    _dropPosition = DropPosition.Inside;
                    ShowDropIndicator(topLeft.Y + h / 2, topLeft.X, w, _dropPosition);
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
                        if (index < 0)
                            index = parentBefore.Children.Count;
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
                        if (index < 0)
                            index = parentAfter.Children.Count - 1;
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

    private void ShowDropIndicator(double y, double left, double width, DropPosition pos)
    {
        if (_dropIndicator is null)
            return;
        _dropIndicator.Margin = new Thickness(left, y - 1, 0, 0);
        _dropIndicator.Width = width;
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


    private void ApplyPathOp(SK.SKPathOp op)
    {
        if (_document is null)
            return;

        var elements = new List<SvgVisualElement>();
        if (_multiSelected.Count >= 2)
        {
            foreach (var el in _multiSelected)
                if (el is SvgVisualElement ve)
                    elements.Add(ve);
        }
        else if (_selectedElement is SvgVisualElement target && _clipboard is SvgVisualElement clip)
        {
            elements.Add(target);
            elements.Add(clip);
        }
        else
        {
            return;
        }

        SaveUndoState();

        var result = elements[0];
        for (int i = 1; i < elements.Count; i++)
        {
            var r = _pathService.ApplyPathOp(result, elements[i], op);
            if (r is null)
                return;
            result = r;
        }

        SvgView.SkSvg!.FromSvgDocument(_document);
        BuildTree();
        SelectNodeFromElement(result);
    }

    private void BlendSelected()
    {
        if (_document is null || _multiSelected.Count != 2)
            return;

        if (_multiSelected[0] is not SvgPath from || _multiSelected[1] is not SvgPath to)
            return;
        if (from.Parent is not SvgElement parent)
            return;

        SaveUndoState();

        var blends = _pathService.Blend(from, to, 5);
        var index = parent.Children.IndexOf(to);
        foreach (var p in blends)
        {
            parent.Children.Insert(index++, p);
        }

        SvgView.SkSvg!.FromSvgDocument(_document);
        BuildTree();
    }

    private void SmoothPointMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing && _pathService.ActivePoint >= 0 && _document is { })
        {
            SaveUndoState();
            _pathService.MakeSmooth(_pathService.ActivePoint);
            SvgView.SkSvg!.FromSvgDocument(_document);
            UpdateSelectedDrawable();
            SvgView.InvalidateVisual();
        }
    }

    private void CornerPointMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_pathService.IsEditing && _pathService.ActivePoint >= 0 && _document is { })
        {
            SaveUndoState();
            _pathService.MakeCorner(_pathService.ActivePoint);
            SvgView.SkSvg!.FromSvgDocument(_document);
            UpdateSelectedDrawable();
            SvgView.InvalidateVisual();
        }
    }

    private void OffsetPathMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedElement is SvgPath sp && _document is { })
        {
            SaveUndoState();
            var result = _pathService.OffsetPath(sp, 10f);
            if (result is null)
                return;
            if (sp.Parent is SvgElement parent)
                parent.Children.Add(result);
            SvgView.SkSvg!.FromSvgDocument(_document);
            BuildTree();
            SelectNodeFromElement(result);
        }
    }

    private void SimplifyPathMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedElement is SvgPath sp && _document is { })
        {
            SaveUndoState();
            _pathService.SimplifyPath(sp);
            SvgView.SkSvg!.FromSvgDocument(_document);
            UpdateSelectedDrawable();
            SvgView.InvalidateVisual();
        }
    }

    private void UniteMenuItem_Click(object? sender, RoutedEventArgs e) => ApplyPathOp(SK.SKPathOp.Union);
    private void SubtractMenuItem_Click(object? sender, RoutedEventArgs e) => ApplyPathOp(SK.SKPathOp.Difference);
    private void IntersectMenuItem_Click(object? sender, RoutedEventArgs e) => ApplyPathOp(SK.SKPathOp.Intersect);
    private void ExcludeMenuItem_Click(object? sender, RoutedEventArgs e) => ApplyPathOp(SK.SKPathOp.Xor);
    private void DivideMenuItem_Click(object? sender, RoutedEventArgs e) => ApplyPathOp(SK.SKPathOp.ReverseDifference);
    private void TrimMenuItem_Click(object? sender, RoutedEventArgs e) => ApplyPathOp(SK.SKPathOp.Difference);
    private void CreateClippingMaskMenuItem_Click(object? sender, RoutedEventArgs e) => CreateClippingMask();
    private void BlendMenuItem_Click(object? sender, RoutedEventArgs e) => BlendSelected();

    private void AlignLeftMenuItem_Click(object? sender, RoutedEventArgs e) => AlignSelected(AlignService.AlignType.Left);
    private void AlignHCenterMenuItem_Click(object? sender, RoutedEventArgs e) => AlignSelected(AlignService.AlignType.HCenter);
    private void AlignRightMenuItem_Click(object? sender, RoutedEventArgs e) => AlignSelected(AlignService.AlignType.Right);
    private void AlignTopMenuItem_Click(object? sender, RoutedEventArgs e) => AlignSelected(AlignService.AlignType.Top);
    private void AlignVCenterMenuItem_Click(object? sender, RoutedEventArgs e) => AlignSelected(AlignService.AlignType.VCenter);
    private void AlignBottomMenuItem_Click(object? sender, RoutedEventArgs e) => AlignSelected(AlignService.AlignType.Bottom);
    private void DistributeHMenuItem_Click(object? sender, RoutedEventArgs e) => DistributeSelected(AlignService.DistributeType.Horizontal);
    private void DistributeVMenuItem_Click(object? sender, RoutedEventArgs e) => DistributeSelected(AlignService.DistributeType.Vertical);
    private void FlipHMenuItem_Click(object? sender, RoutedEventArgs e) => FlipSelected(true);
    private void FlipVMenuItem_Click(object? sender, RoutedEventArgs e) => FlipSelected(false);
    private void NewLayerMenuItem_Click(object? sender, RoutedEventArgs e) => LayerAdd();
    private void DeleteLayerMenuItem_Click(object? sender, RoutedEventArgs e) => LayerDelete();
    private void MoveLayerUpMenuItem_Click(object? sender, RoutedEventArgs e) => LayerUp();
    private void MoveLayerDownMenuItem_Click(object? sender, RoutedEventArgs e) => LayerDown();
    private void LockLayerMenuItem_Click(object? sender, RoutedEventArgs e) => LayerLock();
    private void UnlockLayerMenuItem_Click(object? sender, RoutedEventArgs e) => LayerUnlock();
    private async void CreateStyleMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null || _selectedSvgElement is not SvgVisualElement ve)
            return;
        var win = new SymbolNameWindow();
        var name = await win.ShowDialog<string?>(this);
        if (string.IsNullOrWhiteSpace(name))
            return;
        SaveUndoState();
        var entry = AppearanceService.StyleEntry.FromElement(name!, ve);
        _appearanceService.AddOrUpdateStyle(_document, entry);
        UpdateStyles();
    }

    private void UpdateStyleMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null || _selectedStyle is null || _selectedSvgElement is not SvgVisualElement ve)
            return;
        SaveUndoState();
        var entry = AppearanceService.StyleEntry.FromElement(_selectedStyle.Name, ve);
        _appearanceService.AddOrUpdateStyle(_document, entry);
        UpdateStyles();
    }

    private void DeleteStyleMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null || _selectedStyle is null)
            return;
        SaveUndoState();
        _appearanceService.RemoveStyle(_document, _selectedStyle);
        _selectedStyle = null;
        UpdateStyles();
    }
    private void BringForwardMenuItem_Click(object? sender, RoutedEventArgs e) => BringForward();
    private void SendBackwardMenuItem_Click(object? sender, RoutedEventArgs e) => SendBackward();
    private void LayerAdd_Click(object? sender, RoutedEventArgs e) => LayerAdd();
    private void LayerDelete_Click(object? sender, RoutedEventArgs e) => LayerDelete();
    private void LayerUp_Click(object? sender, RoutedEventArgs e) => LayerUp();
    private void LayerDown_Click(object? sender, RoutedEventArgs e) => LayerDown();
    private void LayerLock_Click(object? sender, RoutedEventArgs e) => LayerLock();
    private void LayerUnlock_Click(object? sender, RoutedEventArgs e) => LayerUnlock();

    private void AlignSelected(AlignService.AlignType type)
    {
        if (_document is null)
            return;
        var list = new List<(SvgVisualElement Element, DrawableBase Drawable)>();
        if (_multiSelected.Count >= 2)
        {
            for (int i = 0; i < _multiSelected.Count && i < _multiDrawables.Count; i++)
                list.Add((_multiSelected[i], _multiDrawables[i]));
        }
        else if (_selectedElement is { } el && _selectedDrawable is { } dr)
        {
            return; // need at least two
        }
        if (list.Count < 2)
            return;
        SaveUndoState();
        _alignService.Align(list, type);
        SvgView.SkSvg!.FromSvgDocument(_document);
        UpdateSelectedDrawable();
        SvgView.InvalidateVisual();
    }

    private void DistributeSelected(AlignService.DistributeType type)
    {
        if (_document is null)
            return;
        var list = new List<(SvgVisualElement Element, DrawableBase Drawable)>();
        if (_multiSelected.Count >= 3)
        {
            for (int i = 0; i < _multiSelected.Count && i < _multiDrawables.Count; i++)
                list.Add((_multiSelected[i], _multiDrawables[i]));
        }
        else
        {
            return; // need at least three
        }
        SaveUndoState();
        _alignService.Distribute(list, type);
        SvgView.SkSvg!.FromSvgDocument(_document);
        UpdateSelectedDrawable();
        SvgView.InvalidateVisual();
    }

    private void FlipSelected(bool horizontal)
    {
        if (_document is null || SvgView.SkSvg is null)
            return;

        var list = new List<(SvgVisualElement Element, DrawableBase Drawable)>();
        if (_toolService.CurrentTool == Tool.MultiSelect && _multiSelected.Count > 0)
        {
            for (int i = 0; i < _multiSelected.Count && i < _multiDrawables.Count; i++)
                list.Add((_multiSelected[i], _multiDrawables[i]));
        }
        else if (_selectedElement is { } el && _selectedDrawable is { } dr)
        {
            list.Add((el, dr));
        }

        if (list.Count == 0)
            return;

        SaveUndoState();
        foreach (var (el, drawable) in list)
        {
            var center = _selectionService.GetBoundsInfo(drawable, SvgView.SkSvg, () => GetCanvasScale()).Center;
            if (horizontal)
                _selectionService.FlipHorizontal(el, center);
            else
                _selectionService.FlipVertical(el, center);
        }

        SvgView.SkSvg.FromSvgDocument(_document);
        UpdateSelectedDrawable();
        SvgView.InvalidateVisual();
    }

    private SvgClipPath CreateClipPath(SvgVisualElement mask, string id)
    {
        if (mask.Parent is SvgElement parent)
            parent.Children.Remove(mask);
        var clip = new SvgClipPath { ID = id };
        clip.Children.Add(mask);
        return clip;
    }

    private void CreateClippingMask()
    {
        if (_document is null || _multiSelected.Count != 2)
            return;

        if (_multiSelected[0] is not SvgVisualElement first || _multiSelected[1] is not SvgVisualElement second)
            return;

        var clipId = string.IsNullOrEmpty(second.ID)
            ? $"clip{_document.Descendants().OfType<SvgClipPath>().Count() + 1}"
            : second.ID;
        second.ID = clipId;

        SaveUndoState();

        var clip = CreateClipPath(second, clipId);
        first.Children.Add(clip);
        first.ClipPath = new Uri($"#{clipId}", UriKind.Relative);

        _multiSelected.Clear();
        _multiDrawables.Clear();
        _multiBounds = SK.SKRect.Empty;

        SvgView.SkSvg!.FromSvgDocument(_document);
        BuildTree();
        SelectNodeFromElement(first);
    }

    private void LayerAdd()
    {
        if (_document is null)
            return;
        SaveUndoState();
        _layerService.AddLayer(_document);
        SvgView.SkSvg!.FromSvgDocument(_document);
        BuildTree();
    }

    private void LayerDelete()
    {
        if (_document is null || _selectedLayer is null)
            return;
        SaveUndoState();
        _layerService.RemoveLayer(_selectedLayer, _document);
        SvgView.SkSvg!.FromSvgDocument(_document);
        BuildTree();
    }

    private void LayerUp()
    {
        if (_document is null || _selectedLayer is null)
            return;
        SaveUndoState();
        _layerService.MoveUp(_selectedLayer, _document);
        SvgView.SkSvg!.FromSvgDocument(_document);
        BuildTree();
    }

    private void LayerDown()
    {
        if (_document is null || _selectedLayer is null)
            return;
        SaveUndoState();
        _layerService.MoveDown(_selectedLayer, _document);
        SvgView.SkSvg!.FromSvgDocument(_document);
        BuildTree();
    }

    private void LayerLock()
    {
        if (_selectedLayer is null)
            return;
        _selectedLayer.Locked = true;
    }

    private void LayerUnlock()
    {
        if (_selectedLayer is null)
            return;
        _selectedLayer.Locked = false;
    }

    private void BringForward()
    {
        if (_document is null || _selectedSvgElement is not SvgElement { Parent: { } parent })
            return;
        var index = parent.Children.IndexOf(_selectedSvgElement);
        if (index < 0 || index >= parent.Children.Count - 1)
            return;
        SaveUndoState();
        parent.Children.RemoveAt(index);
        parent.Children.Insert(index + 1, _selectedSvgElement);
        SvgView.SkSvg!.FromSvgDocument(_document);
        BuildTree();
        SelectNodeFromElement(_selectedSvgElement);
    }

    private void SendBackward()
    {
        if (_document is null || _selectedSvgElement is not SvgElement { Parent: { } parent })
            return;
        var index = parent.Children.IndexOf(_selectedSvgElement);
        if (index <= 0)
            return;
        SaveUndoState();
        parent.Children.RemoveAt(index);
        parent.Children.Insert(index - 1, _selectedSvgElement);
        SvgView.SkSvg!.FromSvgDocument(_document);
        BuildTree();
        SelectNodeFromElement(_selectedSvgElement);
    }

    private void ToolServiceOnToolChanged(Tool oldTool, Tool newTool)
    {
        _boxSelecting = false;
        _multiSelected.Clear();
        _multiDrawables.Clear();
        _multiDragInfos = null;
        _isDragging = false;
        _dragElement = null;
        _dragProps = null;
        _creating = false;
        _newElement = null;
        if (_pathService.IsEditing && newTool != Tool.PathSelect)
            _pathService.Stop();
        if (_polyEditing && newTool != Tool.PolygonSelect && newTool != Tool.PolylineSelect)
            StopPolyEditing();
        _isResizing = false;
        _isSkewing = false;
        _isRotating = false;
        SvgView.InvalidateVisual();
    }

}

public class ArtboardInfo
{
    public SvgGroup Group { get; }
    public string Name { get; }
    public float Width { get; }
    public float Height { get; }

    public ArtboardInfo(SvgGroup group, string name, float width, float height)
    {
        Group = group;
        Name = name;
        Width = width;
        Height = height;
    }

    public override string ToString() => Name;
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
