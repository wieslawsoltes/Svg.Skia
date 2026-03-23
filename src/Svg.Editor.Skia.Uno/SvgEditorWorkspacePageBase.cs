using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Media;
using Svg;
using Svg.Editor.Core;
using Svg.Editor.Skia;
using Svg.Editor.Skia.Uno;
using Svg.Editor.Skia.Uno.Controls;
using Svg.Editor.Skia.Uno.Models;
using Svg.Editor.Svg;
using Svg.Editor.Svg.Models;
using Svg.FilterEffects;
using Svg.Model.Drawables;
using Svg.Model.Services;
using Svg.Pathing;
using Svg.Transforms;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Shim = ShimSkiaSharp;
using SK = SkiaSharp;

namespace Svg.Editor.Skia.Uno;

public partial class SvgEditorWorkspacePage : Page, ISvgEditorShellViewModel, INotifyPropertyChanged
{
    private const float DragThreshold = 0.5f;
    private const float PathPointHandleSize = 10f;
    private const float PathCloseTolerancePixels = 12f;
    private const long VectorDoubleClickMilliseconds = 400;
    private const double VectorDoubleClickTolerancePixels = 6.0;
    private const long SelectionCycleClickMilliseconds = 700;
    private const double SelectionCycleClickTolerancePixels = 6.0;
    private const double DefaultGridSize = 16.0;
    private const double RulerStripSize = 28.0;
    private const double UtilityRailWidth = 54.0;
    private const double SidebarWidth = 314.0;
    private const double MarqueeThreshold = 4.0;
    private const string EffectsMetadataKey = "data-svgskia-effects";
    private const string LibraryIdAttribute = "data-library-id";
    private const string LibraryNameAttribute = "data-library-name";
    private const string LibraryPublisherAttribute = "data-library-publisher";
    private const string LibraryVersionAttribute = "data-library-version";
    private const string LibrarySourceSymbolAttribute = "data-library-symbol-id";
    private const string LibraryManagedAttribute = "data-library-managed";
    private const float DefaultContainerMinSize = 120f;

    private readonly AlignService _alignService = new();
    private readonly AutoLayoutService _autoLayoutService = new();
    private readonly ClipboardSnapshot _clipboard = new();
    private readonly SvgDocumentService _documentService = new();
    private readonly SvgEditorMainMenu _mainMenu = new();
    private readonly PathService _pathService = new();
    private readonly PropertiesService _propertiesService = new();
    private readonly FigmaSelectionContextMenu _selectionContextMenu = new();
    private readonly SymbolService _symbolService = new();
    private readonly ToolService _toolService = new();
    private readonly SelectionService _selectionService = new();
    private readonly List<EditorPageState> _pageStates = [];
    private readonly List<SvgVisualElement> _selectedElements = new();
    private readonly List<DrawableBase> _selectedDrawables = new();
    private readonly List<DrawableBase> _allDrawables = new();
    private readonly Dictionary<SvgVisualElement, (float X, float Y)> _dragStartTranslations = new();
    private readonly HashSet<SvgElement> _collapsedElements = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, SvgVisualElement> _contextMenuLayers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EditorPageState> _contextMenuPages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SampleLibraryDefinition> _libraryCatalog = new(StringComparer.Ordinal);

    private EditorPageState? _activePage;
    private StorageFile? _currentSvgFile;
    private SvgDocument? _document;
    private EditorComponentItem? _selectedComponentAsset;
    private SvgVisualElement? _selectedElement;
    private DrawableBase? _selectedDrawable;
    private SvgVisualElement? _newElement;
    private SvgVisualElement? _resizeElement;
    private SvgVisualElement? _rotateElement;
    private bool _isCreating;
    private bool _isDragging;
    private bool _isPanning;
    private bool _isResizing;
    private bool _isRotating;
    private bool _isMarqueeSelecting;
    private bool _isAssetsView;
    private bool _isLeftPanelCollapsed;
    private bool _isPrototypeInspector;
    private bool _marqueeAdditive;
    private bool _marqueeToggle;
    private bool _marqueePreferFrameChildren;
    private Point _panStart;
    private Point _marqueeStartView;
    private Point _marqueeCurrentView;
    private Shim.SKPoint _dragStartPicture;
    private readonly List<Shim.SKPoint> _freehandPoints = new();
    private Shim.SKPoint _newStart;
    private int _resizeHandle = -1;
    private Shim.SKMatrix _resizeInverse;
    private Shim.SKPoint _resizeStartLocal;
    private SK.SKRect _startRect;
    private float _startTransX;
    private float _startTransY;
    private float _startScaleX = 1f;
    private float _startScaleY = 1f;
    private SK.SKPoint _rotateCenter;
    private SK.SKPoint _rotateCenterLocal;
    private SK.SKPoint _rotateStart;
    private float _startAngle;
    private string _outlineFilter = string.Empty;
    private string _propertyFilter = string.Empty;
    private string _documentTitle = "Untitled";
    private string _pageTitle = "Page 1";
    private string _pageSubtitle = "1 frame";
    private string _canvasLabel = "Frame 1";
    private string _currentToolLabel = "Move";
    private string _viewportLabel = "100%";
    private string _selectionSummary = "No selection";
    private string _canvasStatus = "Loading editor surface...";
    private string _selectedTitle = "No element selected";
    private string _selectedSubtitle = "Pick or draw a vector object on the canvas.";
    private string _selectedIconGlyph = "•";
    private string _quickX = "-";
    private string _quickY = "-";
    private string _quickWidth = "-";
    private string _quickHeight = "-";
    private string _quickRotation = "-";
    private string _quickOpacity = "-";
    private string _quickCornerRadius = "-";
    private string _quickFill = "No fill";
    private string _quickStroke = "No stroke";
    private string _quickStrokeWidth = "-";
    private string _selectionColorOverflowLabel = string.Empty;
    private string _librariesSummary = "No connected libraries";
    private Color _fillColor = Color.FromArgb(255, 224, 65, 65);
    private bool _isFillEnabled = true;
    private bool _isFillColorEditable;
    private bool _isUpdatingFillState;
    private bool _isUpdatingSelectionColorItems;
    private bool _isUpdatingEffectsState;
    private bool _isUpdatingFrameState;
    private bool _isUpdatingAutoLayoutState;
    private bool _isGridVisible = true;
    private bool _isSnapEnabled;
    private bool _isWireframeEnabled;
    private bool _areFiltersDisabled;
    private bool _areRulersVisible = true;
    private bool _isAutoLayoutEnabled;
    private FrameContainerKind _frameKind = FrameContainerKind.Group;
    private AutoLayoutFlow _autoLayoutFlow = AutoLayoutFlow.Vertical;
    private AutoLayoutSizeMode _autoLayoutWidthMode = AutoLayoutSizeMode.Fixed;
    private AutoLayoutSizeMode _autoLayoutHeightMode = AutoLayoutSizeMode.Fixed;
    private AutoLayoutAlignment _autoLayoutHorizontalAlignment = AutoLayoutAlignment.Start;
    private AutoLayoutAlignment _autoLayoutVerticalAlignment = AutoLayoutAlignment.Start;
    private string _autoLayoutWidth = "-";
    private string _autoLayoutHeight = "-";
    private string _autoLayoutGap = "24";
    private string _autoLayoutPaddingHorizontal = "24";
    private string _autoLayoutPaddingVertical = "24";
    private string _selectedFramePresetId = EditorFramePresetItem.CustomId;
    private bool _isAutoLayoutClipContent;
    private bool _hasContextMenuPicturePoint;
    private bool _isActionsPaletteOpen;
    private int _generatedId;
    private string? _clipboardXml;
    private Shim.SKRect? _clipboardBounds;
    private Shim.SKPoint _contextMenuPicturePoint;
    private long _lastVectorClickTicks;
    private Point _lastVectorClickViewPoint;
    private long _lastSelectionClickTicks;
    private Point _lastSelectionClickViewPoint;
    private SvgVisualElement[] _lastSelectionClickHits = [];
    private int _lastSelectionClickIndex = -1;
    private SvgGroup? _selectionScopeFrame;
    private bool _pendingSelectionPress;
    private bool _pendingSelectionAdditive;
    private bool _pendingSelectionToggle;
    private bool _pendingSelectionFrameBackgroundOnly;
    private Point _pendingSelectionViewPoint;
    private Shim.SKPoint _pendingSelectionPicturePoint;
    private SvgVisualElement[] _pendingSelectionHits = [];
    private SvgVisualElement? _pendingSelectionDragElement;

    public SvgEditorWorkspacePage()
    {
        ToolButtons = new ObservableCollection<EditorToolDefinition>(SvgEditorToolCatalog.CreateDefault());
        ToolGroups = new ObservableCollection<EditorToolGroupDefinition>(SvgEditorToolCatalog.CreateGroups(ToolButtons.ToArray()));
        ObjectNodes = new ObservableCollection<EditorObjectNode>();
        FilteredProperties = _propertiesService.FilteredProperties;
        DocumentColorSwatches = new ObservableCollection<ColorSwatchItem>();
        SelectionColorSwatches = new ObservableCollection<ColorSwatchItem>();
        SelectionColorItems = new ObservableCollection<EditorSelectionColorItem>();
        EffectItems = new ObservableCollection<EditorEffectItem>();
        ComponentAssets = new ObservableCollection<EditorComponentItem>();
        Libraries = new ObservableCollection<EditorLibraryItem>();
        FramePresets = new ObservableCollection<EditorFramePresetItem>(SvgEditorFramePresetCatalog.CreateDefault());
        Pages = new ObservableCollection<EditorPageItem>();
        HorizontalRulerMarks = new ObservableCollection<RulerMark>();
        VerticalRulerMarks = new ObservableCollection<RulerMark>();
        HorizontalRulerMarkers = new ObservableCollection<RulerMarker>();
        VerticalRulerMarkers = new ObservableCollection<RulerMarker>();
        EffectItems.CollectionChanged += OnEffectItemsCollectionChanged;
        SelectionColorItems.CollectionChanged += OnSelectionColorItemsCollectionChanged;

        _selectionService.GridSize = _gridSize;
        _selectionService.SnapToGrid = _isSnapEnabled;
        _mainMenu.CommandRequested += OnMainMenuCommandRequested;
        _selectionContextMenu.CommandRequested += OnSelectionContextMenuCommandRequested;

        Loaded += OnLoaded;
        KeyDown += OnWorkspaceKeyDown;
    }

    protected virtual Grid CanvasHostControl => throw new NotSupportedException("CanvasHostControl must be provided by the derived page.");

    protected virtual Grid EditorSurfaceHostControl => throw new NotSupportedException("EditorSurfaceHostControl must be provided by the derived page.");

    protected virtual global::Uno.Svg.Skia.Svg EditorSvgControl => throw new NotSupportedException("EditorSvgControl must be provided by the derived page.");

    protected virtual SvgEditorOverlayCanvas EditorOverlayControl => throw new NotSupportedException("EditorOverlayControl must be provided by the derived page.");

    protected virtual Canvas? InlineTextEditorLayerControl => null;

    protected virtual TextBox? InlineTextEditorControl => null;

    protected Grid CanvasHost => CanvasHostControl;

    protected Grid EditorSurfaceHost => EditorSurfaceHostControl;

    protected global::Uno.Svg.Skia.Svg EditorSvg => EditorSvgControl;

    protected SvgEditorOverlayCanvas EditorOverlay => EditorOverlayControl;

    protected Canvas? InlineTextEditorLayer => InlineTextEditorLayerControl;

    protected TextBox? InlineTextEditor => InlineTextEditorControl;

    protected void InitializeEditorView()
    {
        EditorOverlay.SvgView = EditorSvg;
        EditorOverlay.GridSize = _gridSize;
        EditorOverlay.ShowGrid = _isGridVisible;
        EditorOverlay.SnapToGrid = _isSnapEnabled;
        InitializeCanvasDropPreview();

        EditorSvg.Wireframe = _isWireframeEnabled;
        EditorSvg.DisableFilters = _areFiltersDisabled;
    }

    public ObservableCollection<EditorToolDefinition> ToolButtons { get; }

    public ObservableCollection<EditorToolGroupDefinition> ToolGroups { get; }

    public ObservableCollection<EditorObjectNode> ObjectNodes { get; }

    public ObservableCollection<PropertyEntry> FilteredProperties { get; }

    public ObservableCollection<ColorSwatchItem> DocumentColorSwatches { get; }

    public ObservableCollection<ColorSwatchItem> SelectionColorSwatches { get; }

    public ObservableCollection<EditorSelectionColorItem> SelectionColorItems { get; }

    public string SelectionColorOverflowLabel
    {
        get => _selectionColorOverflowLabel;
        private set => SetField(ref _selectionColorOverflowLabel, value);
    }

    public ObservableCollection<EditorEffectItem> EffectItems { get; }

    public ObservableCollection<EditorComponentItem> ComponentAssets { get; }

    public ObservableCollection<EditorLibraryItem> Libraries { get; }

    public ObservableCollection<EditorFramePresetItem> FramePresets { get; }

    public ObservableCollection<EditorPageItem> Pages { get; }

    public string LibrariesSummary
    {
        get => _librariesSummary;
        private set => SetField(ref _librariesSummary, value);
    }

    public ObservableCollection<RulerMark> HorizontalRulerMarks { get; }

    public ObservableCollection<RulerMark> VerticalRulerMarks { get; }

    public ObservableCollection<RulerMarker> HorizontalRulerMarkers { get; }

    public ObservableCollection<RulerMarker> VerticalRulerMarkers { get; }

    public string DocumentTitle
    {
        get => _documentTitle;
        private set => SetField(ref _documentTitle, value);
    }

    public string PageTitle
    {
        get => _pageTitle;
        private set => SetField(ref _pageTitle, value);
    }

    public string PageSubtitle
    {
        get => _pageSubtitle;
        private set => SetField(ref _pageSubtitle, value);
    }

    public string CanvasLabel
    {
        get => _canvasLabel;
        private set => SetField(ref _canvasLabel, value);
    }

    public string CurrentToolLabel
    {
        get => _currentToolLabel;
        private set => SetField(ref _currentToolLabel, value);
    }

    public string ViewportLabel
    {
        get => _viewportLabel;
        private set => SetField(ref _viewportLabel, value);
    }

    public string SelectionSummary
    {
        get => _selectionSummary;
        private set => SetField(ref _selectionSummary, value);
    }

    public string CanvasStatus
    {
        get => _canvasStatus;
        private set => SetField(ref _canvasStatus, value);
    }

    public string SelectedTitle
    {
        get => _selectedTitle;
        private set => SetField(ref _selectedTitle, value);
    }

    public string SelectedSubtitle
    {
        get => _selectedSubtitle;
        private set => SetField(ref _selectedSubtitle, value);
    }

    public string SelectedIconGlyph
    {
        get => _selectedIconGlyph;
        private set => SetField(ref _selectedIconGlyph, value);
    }

    public string QuickX
    {
        get => _quickX;
        private set => SetField(ref _quickX, value);
    }

    public string QuickY
    {
        get => _quickY;
        private set => SetField(ref _quickY, value);
    }

    public string QuickWidth
    {
        get => _quickWidth;
        private set => SetField(ref _quickWidth, value);
    }

    public string QuickHeight
    {
        get => _quickHeight;
        private set => SetField(ref _quickHeight, value);
    }

    public string QuickRotation
    {
        get => _quickRotation;
        private set => SetField(ref _quickRotation, value);
    }

    public string QuickOpacity
    {
        get => _quickOpacity;
        private set => SetField(ref _quickOpacity, value);
    }

    public string QuickCornerRadius
    {
        get => _quickCornerRadius;
        private set => SetField(ref _quickCornerRadius, value);
    }

    public string QuickFill
    {
        get => _quickFill;
        private set => SetField(ref _quickFill, value);
    }

    public string QuickStroke
    {
        get => _quickStroke;
        private set => SetField(ref _quickStroke, value);
    }

    public string QuickStrokeWidth
    {
        get => _quickStrokeWidth;
        private set => SetField(ref _quickStrokeWidth, value);
    }

    public string ComponentPrimaryLabel
    {
        get
        {
            if (_selectedElement is SvgUse use && ResolveComponentSymbol(use) is { } symbol)
            {
                return $"Instance of {GetComponentDisplayName(symbol)}";
            }

            if (_selectedComponentAsset is not null)
            {
                return $"Ready to insert {_selectedComponentAsset.Name}";
            }

            if (CanCreateComponent)
            {
                return "Create a component";
            }

            return ComponentAssets.Count == 0
                ? "No components on this page"
                : "Select a component asset";
        }
    }

    public string ComponentSecondaryLabel
    {
        get
        {
            if (_selectedElement is SvgUse use && ResolveComponentSymbol(use) is { } symbol)
            {
                return $"This instance points to {GetComponentDisplayName(symbol)}. Swap it to another asset or detach it into editable SVG layers.";
            }

            if (_selectedComponentAsset is not null)
            {
                return "Insert the selected asset onto the canvas, or pick an instance to swap it without breaking layout.";
            }

            return "Convert the current SVG selection into a reusable symbol and place instances with the component tool.";
        }
    }

    public EditorComponentItem? SelectedComponentAsset
    {
        get => _selectedComponentAsset;
        set
        {
            if (string.Equals(_selectedComponentAsset?.AssetKey, value?.AssetKey, StringComparison.Ordinal))
            {
                return;
            }

            SelectComponentAsset(value, activateSymbolTool: false);
        }
    }

    public Color FillColor
    {
        get => _fillColor;
        set
        {
            if (SetField(ref _fillColor, value) && !_isUpdatingFillState)
            {
                ApplyFillState();
            }
        }
    }

    public bool IsFillEnabled
    {
        get => _isFillEnabled;
        set
        {
            if (SetField(ref _isFillEnabled, value) && !_isUpdatingFillState)
            {
                ApplyFillState();
            }
        }
    }

    public bool IsFillColorEditable
    {
        get => _isFillColorEditable;
        private set => SetField(ref _isFillColorEditable, value);
    }

    public bool CanEditFrame => TryGetEditableContainer(out _);

    public bool CanUseFramePresets => TryGetEditableContainer(out var group)
        && FrameService.GetContainerKind(group) is FrameContainerKind.Frame or FrameContainerKind.Section;

    public FrameContainerKind FrameKind
    {
        get => _frameKind;
        set
        {
            if (SetField(ref _frameKind, value) && !_isUpdatingFrameState)
            {
                ApplyFrameKindState();
            }
        }
    }

    public string SelectedFramePresetId
    {
        get => _selectedFramePresetId;
        set
        {
            if (SetField(ref _selectedFramePresetId, value) && !_isUpdatingFrameState)
            {
                ApplyFramePresetState();
            }
        }
    }

    public string FrameSummary
    {
        get
        {
            if (!TryGetEditableContainer(out var group))
            {
                return "Select a single SVG group to convert it into a frame, section, or keep it as a plain group.";
            }

            return FrameService.GetContainerKind(group) switch
            {
                FrameContainerKind.Frame => "Frames use an SVG group plus a generated background rect, support presets, clipping, and auto layout.",
                FrameContainerKind.Section => "Sections stay SVG-backed and resizable, but act as organizational containers instead of auto layout frames.",
                _ => "Groups keep the raw SVG hierarchy with no generated background. Switch to Frame or Section to add explicit container bounds."
            };
        }
    }

    public bool CanEditEffects => _selectedElements.Count == 1 && _selectedElement is not null;

    public bool CanAlignSelection => _selectedElements.Count >= 2;

    public bool CanDistributeSelection => _selectedElements.Count >= 3;

    public bool CanRotateSelection => _selectedElements.Count > 0;

    public bool CanEditCornerRadius => _selectedElements.Count == 1 && TryGetCornerRadiusTarget(_selectedElement, out _);

    public bool CanEditAutoLayout => TryGetAutoLayoutFrame(out _);

    public bool IsAutoLayoutEnabled
    {
        get => _isAutoLayoutEnabled;
        set
        {
            if (SetField(ref _isAutoLayoutEnabled, value) && !_isUpdatingAutoLayoutState)
            {
                ApplyAutoLayoutState();
            }
        }
    }

    public AutoLayoutFlow AutoLayoutFlow
    {
        get => _autoLayoutFlow;
        set
        {
            if (SetField(ref _autoLayoutFlow, value) && !_isUpdatingAutoLayoutState)
            {
                ApplyAutoLayoutState();
            }
        }
    }

    public AutoLayoutSizeMode AutoLayoutWidthMode
    {
        get => _autoLayoutWidthMode;
        set
        {
            if (SetField(ref _autoLayoutWidthMode, value) && !_isUpdatingAutoLayoutState)
            {
                ApplyAutoLayoutState();
            }
        }
    }

    public AutoLayoutSizeMode AutoLayoutHeightMode
    {
        get => _autoLayoutHeightMode;
        set
        {
            if (SetField(ref _autoLayoutHeightMode, value) && !_isUpdatingAutoLayoutState)
            {
                ApplyAutoLayoutState();
            }
        }
    }

    public AutoLayoutAlignment AutoLayoutHorizontalAlignment
    {
        get => _autoLayoutHorizontalAlignment;
        set
        {
            if (SetField(ref _autoLayoutHorizontalAlignment, value) && !_isUpdatingAutoLayoutState)
            {
                ApplyAutoLayoutState();
            }
        }
    }

    public AutoLayoutAlignment AutoLayoutVerticalAlignment
    {
        get => _autoLayoutVerticalAlignment;
        set
        {
            if (SetField(ref _autoLayoutVerticalAlignment, value) && !_isUpdatingAutoLayoutState)
            {
                ApplyAutoLayoutState();
            }
        }
    }

    public string AutoLayoutWidth
    {
        get => _autoLayoutWidth;
        set
        {
            if (SetField(ref _autoLayoutWidth, value) && !_isUpdatingAutoLayoutState)
            {
                ApplyAutoLayoutState();
            }
        }
    }

    public string AutoLayoutHeight
    {
        get => _autoLayoutHeight;
        set
        {
            if (SetField(ref _autoLayoutHeight, value) && !_isUpdatingAutoLayoutState)
            {
                ApplyAutoLayoutState();
            }
        }
    }

    public string AutoLayoutGap
    {
        get => _autoLayoutGap;
        set
        {
            if (SetField(ref _autoLayoutGap, value) && !_isUpdatingAutoLayoutState)
            {
                ApplyAutoLayoutState();
            }
        }
    }

    public string AutoLayoutPaddingHorizontal
    {
        get => _autoLayoutPaddingHorizontal;
        set
        {
            if (SetField(ref _autoLayoutPaddingHorizontal, value) && !_isUpdatingAutoLayoutState)
            {
                ApplyAutoLayoutState();
            }
        }
    }

    public string AutoLayoutPaddingVertical
    {
        get => _autoLayoutPaddingVertical;
        set
        {
            if (SetField(ref _autoLayoutPaddingVertical, value) && !_isUpdatingAutoLayoutState)
            {
                ApplyAutoLayoutState();
            }
        }
    }

    public bool IsAutoLayoutClipContent
    {
        get => _isAutoLayoutClipContent;
        set
        {
            if (SetField(ref _isAutoLayoutClipContent, value) && !_isUpdatingAutoLayoutState)
            {
                ApplyAutoLayoutState();
            }
        }
    }

    public bool IsGridVisible
    {
        get => _isGridVisible;
        set => SetField(ref _isGridVisible, value);
    }

    public bool IsSnapEnabled
    {
        get => _isSnapEnabled;
        set => SetField(ref _isSnapEnabled, value);
    }

    public bool IsWireframeEnabled
    {
        get => _isWireframeEnabled;
        set => SetField(ref _isWireframeEnabled, value);
    }

    public bool AreFiltersDisabled
    {
        get => _areFiltersDisabled;
        set => SetField(ref _areFiltersDisabled, value);
    }

    public bool AreRulersVisible
    {
        get => _areRulersVisible;
        set
        {
            if (!SetField(ref _areRulersVisible, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(HorizontalRulerLength));
            RaisePropertyChanged(nameof(VerticalRulerLength));
        }
    }

    public GridLength HorizontalRulerLength => AreRulersVisible
        ? new GridLength(RulerStripSize)
        : new GridLength(0.0);

    public GridLength VerticalRulerLength => AreRulersVisible
        ? new GridLength(RulerStripSize)
        : new GridLength(0.0);

    public bool CanCreateComponent => CanCreateComponentSelection();

    public bool CanInsertComponentInstance => _document is not null && _selectedComponentAsset is not null;

    public bool CanSwapComponentInstance => _selectedElement is SvgUse && _selectedComponentAsset is not null;

    public bool CanDetachComponentInstance => _selectedElement is SvgUse;

    public bool IsLayersViewActive => !_isAssetsView;

    public bool IsAssetsViewActive => _isAssetsView;

    public bool IsLeftPanelCollapsed => _isLeftPanelCollapsed;

    public GridLength UtilityRailColumnWidth => _isLeftPanelCollapsed
        ? new GridLength(0.0)
        : new GridLength(UtilityRailWidth);

    public GridLength SidebarColumnWidth => _isLeftPanelCollapsed
        ? new GridLength(0.0)
        : new GridLength(SidebarWidth);

    public Visibility LayersPanelVisibility => _isAssetsView ? Visibility.Collapsed : Visibility.Visible;

    public Visibility AssetsPanelVisibility => _isAssetsView ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LeftPanelVisibility => _isLeftPanelCollapsed ? Visibility.Collapsed : Visibility.Visible;

    public bool IsDesignInspectorActive => !_isPrototypeInspector && !_isCommentsInspector && !_isDevInspector;

    public bool IsPrototypeInspectorActive => _isPrototypeInspector;

    public Visibility DesignInspectorVisibility => _isPrototypeInspector || _isCommentsInspector || _isDevInspector ? Visibility.Collapsed : Visibility.Visible;

    public Visibility PrototypeInspectorVisibility => _isPrototypeInspector ? Visibility.Visible : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ResetArtworkAsync();
    }

    private async Task ResetArtworkAsync()
    {
        _pageStates.Clear();
        Pages.Clear();
        Libraries.Clear();
        _libraryCatalog.Clear();
        _currentSvgFile = null;

        foreach (var state in CreateInitialPageStates())
        {
            _pageStates.Add(state);
            Pages.Add(state.Page);
        }

        if (_pageStates.Count == 0)
        {
            CanvasStatus = "Unable to build the sample page set.";
            return;
        }

        DocumentTitle = "Untitled";
        SetTool(ToolService.Tool.Select);
        _propertiesService.Properties.Clear();
        _propertiesService.FilteredProperties.Clear();
        _generatedId = 0;
        ResetCommentState();
        InitializeLibraries();

        await SwitchToPageAsync(_pageStates[0], selectDefaultSelection: true, resetViewport: true);
    }

    protected void OnLayersTabClick(object sender, RoutedEventArgs e)
    {
        SetSidebarMode(showAssets: false);
    }

    protected void OnAssetsTabClick(object sender, RoutedEventArgs e)
    {
        SetSidebarMode(showAssets: true);
    }

    protected async void OnManageLibrariesRequested(object sender, RoutedEventArgs e)
    {
        await ShowLibrariesManagerAsync();
    }

    protected async void OnPageAddRequested(object sender, RoutedEventArgs e)
    {
        await AddBlankPageAsync();
    }

    protected async void OnPageSelectionRequested(object sender, PageRequestedEventArgs e)
    {
        var state = _pageStates.FirstOrDefault(page => ReferenceEquals(page.Page, e.Page));
        if (state is null || ReferenceEquals(state, _activePage))
        {
            return;
        }

        await SwitchToPageAsync(state, selectDefaultSelection: false, resetViewport: false);
    }

    private async Task AddBlankPageAsync()
    {
        var state = CreateBlankPageState($"Page {_pageStates.Count + 1}");
        _pageStates.Add(state);
        Pages.Add(state.Page);
        UpdatePageSelectionState(state);
        await SwitchToPageAsync(state, selectDefaultSelection: true, resetViewport: true);
    }

    protected void OnComponentAssetRequested(object sender, ComponentRequestedEventArgs e)
    {
        SelectComponentAsset(e.Component, activateSymbolTool: true);
        SetSidebarMode(showAssets: true);
        CanvasStatus = $"{e.Component.Name} ready to place. Click on the stage to insert a new instance.";
    }

    protected void OnDesignInspectorClick(object sender, RoutedEventArgs e)
    {
        if (_toolService.CurrentTool == ToolService.Tool.Comment)
        {
            SetTool(ToolService.Tool.Select);
        }

        SetInspectorMode(showPrototype: false);
    }

    protected void OnPrototypeInspectorClick(object sender, RoutedEventArgs e)
    {
        if (_toolService.CurrentTool == ToolService.Tool.Comment)
        {
            SetTool(ToolService.Tool.Select);
        }

        SetInspectorMode(showPrototype: true);
    }

    protected void OnToolButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: EditorToolDefinition taggedTool })
        {
            SetTool(taggedTool.Tool);
            return;
        }

        if (sender is FrameworkElement { DataContext: EditorToolDefinition tool })
        {
            SetTool(tool.Tool);
        }
    }

    protected async void OnResetArtworkClick(object sender, RoutedEventArgs e)
    {
        await ResetArtworkAsync();
    }

    protected void OnFitViewClick(object sender, RoutedEventArgs e)
    {
        EditorSvg.Zoom = 1.0;
        EditorSvg.PanX = 0.0;
        EditorSvg.PanY = 0.0;
        RefreshComputedState();
        RefreshOverlay();
    }

    protected void OnShareClick(object sender, RoutedEventArgs e)
    {
        CanvasStatus = "Share/export chrome is not wired yet. The live SVG AST remains the source of truth for export and publishing flows.";
    }

    protected async void OnActionsRequested(object sender, RoutedEventArgs e)
    {
        await ShowActionsPaletteAsync();
    }

    protected void OnLeftPanelToggleRequested(object sender, RoutedEventArgs e)
    {
        ToggleLeftPanel();
    }

    protected void OnMainMenuRequested(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement target)
        {
            return;
        }

        _mainMenu.State = BuildMainMenuState();
        _mainMenu.ShowAt(target);
    }

    protected void OnWorkspaceKeyDown(object sender, KeyRoutedEventArgs e)
    {
        HandleGlobalShortcutKey(e);
    }

    protected void OnZoomInClick(object sender, RoutedEventArgs e)
    {
        ZoomAroundCenter(1.12);
    }

    protected void OnZoomOutClick(object sender, RoutedEventArgs e)
    {
        ZoomAroundCenter(1.0 / 1.12);
    }

    protected void OnCreateFrameRequested(object sender, RoutedEventArgs e)
    {
        CreateFrameAtViewportCenter();
    }

    protected void OnDuplicateFrameRequested(object sender, RoutedEventArgs e)
    {
        DuplicateActiveFrame();
    }

    protected void OnCreateComponentRequested(object sender, RoutedEventArgs e)
    {
        CreateComponentFromSelection();
    }

    protected void OnInsertComponentRequested(object sender, RoutedEventArgs e)
    {
        InsertComponentInstanceAtViewportCenter();
    }

    protected void OnSwapComponentRequested(object sender, RoutedEventArgs e)
    {
        SwapSelectedInstance();
    }

    protected void OnDetachComponentRequested(object sender, RoutedEventArgs e)
    {
        DetachSelectedInstance();
    }

    protected void OnOutlineContextRequested(object sender, OutlineContextRequestedEventArgs e)
    {
        var visual = e.Node.Element as SvgVisualElement;
        if (visual is not null && !_selectedElements.Contains(visual))
        {
            ApplySelection([visual], visual);
        }

        _hasContextMenuPicturePoint = TryGetElementAnchorPoint(e.Node.Element, out _contextMenuPicturePoint);
        if (!_hasContextMenuPicturePoint)
        {
            _contextMenuPicturePoint = GetDefaultInsertPoint();
            _hasContextMenuPicturePoint = true;
        }

        ShowSelectionContextMenu(e.Target, e.Position, BuildOutlineLayerItems(e.Node.Element));
    }

    protected void OnCanvasPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (EditorSvg.Document is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(EditorSvg).Position;
        var factor = e.GetCurrentPoint(CanvasHost).Properties.MouseWheelDelta > 0 ? 1.08 : 1.0 / 1.08;
        EditorSvg.ZoomToPoint(EditorSvg.Zoom * factor, point);
        RefreshComputedState();
        RefreshOverlay();
        e.Handled = true;
    }

    protected void OnCanvasPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_document is null)
        {
            return;
        }

        if (IsInlineTextEditing)
        {
            if (IsInlineTextEditorSource(e.OriginalSource as DependencyObject))
            {
                return;
            }

            CommitInlineTextEdit();
        }

        CanvasHost.Focus(FocusState.Programmatic);

        var hostPoint = e.GetCurrentPoint(CanvasHost).Position;
        var viewPoint = e.GetCurrentPoint(EditorSvg).Position;
        var properties = e.GetCurrentPoint(CanvasHost).Properties;
        var modifiers = e.KeyModifiers;
        var additive = modifiers.HasFlag(VirtualKeyModifiers.Shift);
        var toggle = IsToggleModifier(modifiers);

        if (IsMiddlePointerPanPress(properties))
        {
            ResetSelectionClickCycle();
            _isPanning = true;
            _panStart = hostPoint;
            CanvasHost.CapturePointer(e.Pointer);
            e.Handled = true;
            return;
        }

        if (_toolService.CurrentTool == ToolService.Tool.Hand && !properties.IsRightButtonPressed)
        {
            ResetSelectionClickCycle();
            _isPanning = true;
            _panStart = hostPoint;
            CanvasHost.CapturePointer(e.Pointer);
            e.Handled = true;
            return;
        }

        if (properties.IsRightButtonPressed)
        {
            if (_toolService.CurrentTool == ToolService.Tool.Comment)
            {
                ResetSelectionClickCycle();
                CancelCommentDraft();
                e.Handled = true;
                return;
            }

            if (_toolService.CurrentTool == ToolService.Tool.PathLine && IsVectorPathDrawing)
            {
                FinishVectorPath(cancelDrawing: false);
            }

            if (IsSelectionTool(_toolService.CurrentTool))
            {
                var hits = GetSelectionHits(viewPoint);
                if (hits.Count == 0)
                {
                    ResetSelectionClickCycle();
                    SelectElement(null, additive, toggle);
                }
                else if (!hits.Any(hit => _selectedElements.Contains(hit)))
                {
                    ResetSelectionClickCycle();
                    SelectElement(hits[0], additive, toggle);
                }
            }

            e.Handled = true;
            return;
        }

        if (!TryGetPicturePoint(e, out var picturePoint))
        {
            return;
        }

        if (HandleCommentToolPointerPressed(e, viewPoint, picturePoint))
        {
            return;
        }

        if (_toolService.CurrentTool == ToolService.Tool.PathLine)
        {
            ResetSelectionClickCycle();
            if (HandleVectorToolPointerPressed(e, picturePoint, viewPoint))
            {
                return;
            }
        }

        if (_toolService.CurrentTool is ToolService.Tool.Text or ToolService.Tool.TextArea)
        {
            var editableText = GetVisualHits(viewPoint).OfType<SvgTextBase>().FirstOrDefault();
            if (editableText is not null)
            {
                ResetSelectionClickCycle();
                SelectElement(editableText);
                if (StartInlineTextEdit(editableText))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        if (IsSelectionTool(_toolService.CurrentTool))
        {
            if (TryStartTransform(picturePoint))
            {
                CancelPendingSelectionPress();
                ResetSelectionClickCycle();
                CanvasHost.CapturePointer(e.Pointer);
                e.Handled = true;
                return;
            }

            var hits = GetSelectionHits(viewPoint);
            if (hits.Count > 0)
            {
                var frameBackgroundOnly = IsFrameBackgroundSelectionOnly(hits);
                if (frameBackgroundOnly)
                {
                    CancelPendingSelectionPress();
                    BeginPendingSelectionPress(viewPoint, picturePoint, hits, additive, toggle, frameBackgroundOnly: true);
                    CanvasHost.CapturePointer(e.Pointer);
                }
                else if (additive || toggle)
                {
                    CancelPendingSelectionPress();
                    var hit = GetSelectionTraversalHit(viewPoint, additive, toggle, hits);
                    SelectElement(hit, additive, toggle);
                }
                else
                {
                    BeginPendingSelectionPress(viewPoint, picturePoint, hits);
                    CanvasHost.CapturePointer(e.Pointer);
                }
            }
            else
            {
                CancelPendingSelectionPress();
                ResetSelectionClickCycle();
                StartMarqueeSelection(viewPoint, additive, toggle);
                CanvasHost.CapturePointer(e.Pointer);
            }

            e.Handled = true;
            return;
        }

        var newElement = CreateElementAt(picturePoint, out var startPoint);
        if (newElement is null)
        {
            return;
        }

        ResetSelectionClickCycle();
        _newStart = startPoint;
        _newElement = newElement;
        _isCreating = true;
        _freehandPoints.Clear();
        if (newElement is SvgPath && IsFreehandTool(_toolService.CurrentTool))
        {
            _freehandPoints.Add(startPoint);
        }

        SelectElement(newElement);
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: false);
        CanvasHost.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    protected void OnCanvasRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (_document is null)
        {
            return;
        }

        if (_toolService.CurrentTool == ToolService.Tool.Comment)
        {
            CancelCommentDraft();
            e.Handled = true;
            return;
        }

        if (_toolService.CurrentTool == ToolService.Tool.PathLine && IsVectorPathDrawing)
        {
            e.Handled = true;
            return;
        }

        var position = e.GetPosition(CanvasHost);

        var editorPoint = CanvasHost.TransformToVisual(EditorSvg).TransformPoint(position);
        var layerItems = BuildCanvasLayerItems(editorPoint);
        if (EditorSvg.TryGetPicturePoint(editorPoint, out var picturePoint))
        {
            _contextMenuPicturePoint = new Shim.SKPoint(picturePoint.X, picturePoint.Y);
            _hasContextMenuPicturePoint = true;
        }

        if (!_hasContextMenuPicturePoint)
        {
            _contextMenuPicturePoint = GetDefaultInsertPoint();
            _hasContextMenuPicturePoint = true;
        }

        if (_selectedElements.Count == 0 && layerItems.Length == 0)
        {
            return;
        }

        ShowSelectionContextMenu(CanvasHost, position, layerItems);
        e.Handled = true;
    }

    protected void OnCanvasHostKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (HandleCommentShortcutKey(e))
        {
            return;
        }

        if (HandleCanvasShortcutKey(e))
        {
            return;
        }

        if (e.Key is not (VirtualKey.Application or VirtualKey.F10)
            || e.Key == VirtualKey.F10 && !e.KeyStatus.IsMenuKeyDown && !e.KeyStatus.IsExtendedKey)
        {
            if (e.Key != VirtualKey.F10 || !IsShiftPressed())
            {
                return;
            }
        }

        if (_selectedElements.Count == 0)
        {
            return;
        }

        Point? anchorPosition = null;
        if (TryGetSelectionViewCenter(out var selectionCenter))
        {
            anchorPosition = EditorSvg.TransformToVisual(CanvasHost).TransformPoint(selectionCenter);
            if (EditorSvg.TryGetPicturePoint(selectionCenter, out var picturePoint))
            {
                _contextMenuPicturePoint = new Shim.SKPoint(picturePoint.X, picturePoint.Y);
                _hasContextMenuPicturePoint = true;
            }
        }

        if (!_hasContextMenuPicturePoint)
        {
            _contextMenuPicturePoint = GetDefaultInsertPoint();
            _hasContextMenuPicturePoint = true;
        }

        ShowSelectionContextMenu(CanvasHost, anchorPosition, Array.Empty<SelectionContextMenuLayerItem>());
        e.Handled = true;
    }

    protected void OnCanvasPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var hostPoint = e.GetCurrentPoint(CanvasHost).Position;
        var viewPoint = e.GetCurrentPoint(EditorSvg).Position;
        var properties = e.GetCurrentPoint(CanvasHost).Properties;

        if (TryPromoteMiddlePointerPan(hostPoint, properties))
        {
            e.Handled = true;
            return;
        }

        if (_isPanning)
        {
            var dx = hostPoint.X - _panStart.X;
            var dy = hostPoint.Y - _panStart.Y;
            _panStart = hostPoint;
            EditorSvg.PanX += dx;
            EditorSvg.PanY += dy;
            RefreshComputedState();
            RefreshOverlay();
            e.Handled = true;
            return;
        }

        if (_isMarqueeSelecting)
        {
            _marqueeCurrentView = viewPoint;
            RefreshOverlay();
            return;
        }

        if (!TryGetPicturePoint(e, out var picturePoint) || _document is null)
        {
            return;
        }

        TryStartPendingSelectionDrag(viewPoint);
        if (_isMarqueeSelecting)
        {
            e.Handled = true;
            return;
        }

        if (_pathService.ActivePoint >= 0)
        {
            MoveActivePathPoint(picturePoint);
            return;
        }

        if (IsVectorPathDrawing)
        {
            UpdateVectorPreview(picturePoint);
            return;
        }

        if (_isCreating && _newElement is not null)
        {
            var creationPoint = GetCurrentCreationPoint(_newElement, picturePoint);
            if (_newElement is SvgTextBase textElement
                && _toolService.CurrentTool is ToolService.Tool.Text or ToolService.Tool.TextArea)
            {
                UpdateTextCreationPreview(textElement, _newStart, creationPoint);
                RefreshDocumentVisual(rebuildOutline: false, reloadProperties: false);
            }
            else if (IsFreehandTool(_toolService.CurrentTool))
            {
                var current = creationPoint;
                if (_freehandPoints.Count == 0)
                {
                    _freehandPoints.Add(current);
                }

                var last = _freehandPoints[^1];
                if (Math.Abs(last.X - current.X) > DragThreshold || Math.Abs(last.Y - current.Y) > DragThreshold)
                {
                    _freehandPoints.Add(current);
                    _toolService.AddFreehandPoint(_newElement, current, _isSnapEnabled, _selectionService.Snap);
                    RefreshDocumentVisual(rebuildOutline: false, reloadProperties: false);
                }
            }
            else
            {
                _toolService.UpdateElement(
                    _newElement,
                    _toolService.CurrentTool,
                    _newStart,
                    creationPoint,
                    _isSnapEnabled,
                    _selectionService.Snap);

                RefreshDocumentVisual(rebuildOutline: false, reloadProperties: false);
            }

            return;
        }

        if (_isDragging && _selectedElements.Count > 0)
        {
            var dx = picturePoint.X - _dragStartPicture.X;
            var dy = picturePoint.Y - _dragStartPicture.Y;
            foreach (var element in _selectedElements)
            {
                if (_dragStartTranslations.TryGetValue(element, out var origin))
                {
                    _selectionService.SetTranslation(element, origin.X + dx, origin.Y + dy);
                }
            }

            RefreshDocumentVisual(rebuildOutline: false, reloadProperties: false);
            return;
        }

        if (_isResizing && _resizeElement is not null)
        {
            var currentLocal = _resizeInverse.MapPoint(new Shim.SKPoint(picturePoint.X, picturePoint.Y));
            var dx = currentLocal.X - _resizeStartLocal.X;
            var dy = currentLocal.Y - _resizeStartLocal.Y;
            if (_resizeElement is SvgTextBase textElement && HasTextBoxRect(textElement))
            {
                ResizeTextBox(textElement, _resizeHandle, dx, dy);
            }
            else
            {
                _selectionService.ResizeElement(
                    _resizeElement,
                    _resizeHandle,
                    dx,
                    dy,
                    _startRect,
                    _startTransX,
                    _startTransY,
                    _startScaleX,
                    _startScaleY);
            }

            RefreshDocumentVisual(rebuildOutline: false, reloadProperties: false);
            return;
        }

        if (_isRotating && _rotateElement is not null)
        {
            var current = new SK.SKPoint(picturePoint.X, picturePoint.Y);
            var startAngle = Math.Atan2(_rotateStart.Y - _rotateCenter.Y, _rotateStart.X - _rotateCenter.X);
            var currentAngle = Math.Atan2(current.Y - _rotateCenter.Y, current.X - _rotateCenter.X);
            var delta = (float)((currentAngle - startAngle) * 180.0 / Math.PI);
            _selectionService.SetRotation(_rotateElement, _startAngle + delta, _rotateCenterLocal);
            RefreshDocumentVisual(rebuildOutline: false, reloadProperties: false);
        }
    }

    protected void OnCanvasPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        CanvasHost.ReleasePointerCapture(e.Pointer);

        var properties = e.GetCurrentPoint(CanvasHost).Properties;
        if ((_isPanning || _pendingSelectionPress || _isMarqueeSelecting) && IsMiddlePointerPanRelease(properties))
        {
            CancelPendingSelectionPress();
            CancelMarqueeSelection();
            EndCanvasPan();
            e.Handled = true;
            return;
        }

        if (_pathService.ActivePoint >= 0)
        {
            _pathService.ActivePoint = -1;
            RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
            return;
        }

        if (_isCreating)
        {
            if (IsVectorPathDrawing)
            {
                RefreshOverlay();
                return;
            }

            if (_newElement is SvgTextBase textElement
                && _toolService.CurrentTool is ToolService.Tool.Text or ToolService.Tool.TextArea)
            {
                CompleteTextCreationAndEdit(textElement);
                return;
            }

            _isCreating = false;
            _newElement = null;
            _freehandPoints.Clear();
            RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
            return;
        }

        if (_isDragging || _isResizing || _isRotating)
        {
            _isDragging = false;
            _isResizing = false;
            _isRotating = false;
            _dragStartTranslations.Clear();
            RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
            return;
        }

        if (_isMarqueeSelecting)
        {
            CompleteMarqueeSelection();
            return;
        }

        if (_pendingSelectionPress)
        {
            CompletePendingSelectionPress();
            return;
        }

        if (_isPanning)
        {
            EndCanvasPan();
        }
    }

    protected void OnCanvasPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        EndCanvasPan();
    }

    protected void OnCanvasPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        EndCanvasPan();
    }

    protected void OnCanvasHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshOverlay();
        RefreshComputedState();
        LayoutInlineTextEditor();
    }

    protected void OnOutlineSelectionRequested(object sender, OutlineSelectionRequestedEventArgs e)
    {
        if (e.Selection.Count == 0)
        {
            ApplySelection(Array.Empty<SvgVisualElement>(), null);
            return;
        }

        var selection = e.Selection
            .Select(static node => PromoteOutlineSelectionElement(node.Element))
            .OfType<SvgVisualElement>()
            .Distinct()
            .ToList();

        var primary = PromoteOutlineSelectionElement(e.PrimaryNode?.Element) as SvgVisualElement ?? selection.LastOrDefault();
        ApplySelection(selection, primary);
    }

    protected void OnOutlineExpansionRequested(object sender, OutlineNodeRequestEventArgs e)
    {
        var node = e.Node;
        if (!node.HasChildren)
        {
            return;
        }

        if (_collapsedElements.Contains(node.Element))
        {
            _collapsedElements.Remove(node.Element);
        }
        else
        {
            _collapsedElements.Add(node.Element);
        }

        RebuildObjectNodes();
    }

    protected void OnObjectVisibilityChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: EditorObjectNode node } || _document is null)
        {
            return;
        }

        var visible = !IsElementVisible(node.Element);
        SetElementVisible(node.Element, visible);
        node.IsVisible = visible;
        RefreshDocumentVisual(rebuildOutline: false, reloadProperties: false);
    }

    protected void OnObjectLockChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: EditorObjectNode node } || _document is null)
        {
            return;
        }

        var locked = !IsElementLocked(node.Element);
        SetElementLocked(node.Element, locked);
        node.IsLocked = locked;
        RefreshDocumentVisual(rebuildOutline: false, reloadProperties: false);
    }

    protected void OnOutlineFilterTextChanged(object sender, TextChangedEventArgs e)
    {
        _outlineFilter = (sender as TextBox)?.Text ?? string.Empty;
        RebuildObjectNodes();
    }

    protected void OnPropertyFilterTextChanged(object sender, TextChangedEventArgs e)
    {
        _propertyFilter = (sender as TextBox)?.Text ?? string.Empty;
        _propertiesService.ApplyFilter(_propertyFilter);
    }

    protected void OnPropertyValueCommitted(object sender, RoutedEventArgs e)
    {
        ApplyPropertyChanges();
    }

    protected void OnPropertyValueKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            ApplyPropertyChanges();
        }
    }

    protected void OnQuickFieldCommitted(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            ApplyQuickFieldChanges(textBox);
        }
    }

    protected void OnQuickFieldKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && sender is TextBox textBox)
        {
            ApplyQuickFieldChanges(textBox);
            e.Handled = true;
        }
    }

    protected void OnViewportToggleChanged(object sender, RoutedEventArgs e)
    {
        ApplyViewportOptions();
    }

    protected void OnAlignSelectionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string rawTag })
        {
            return;
        }

        if (!Enum.TryParse<AlignService.AlignType>(rawTag, ignoreCase: true, out var alignType))
        {
            return;
        }

        var items = GetSelectedItemsForAlignment();
        if (items.Count < 2)
        {
            return;
        }

        _alignService.Align(items, alignType);
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
    }

    protected void OnDistributeSelectionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string rawTag })
        {
            return;
        }

        var type = rawTag.Equals("Vertical", StringComparison.OrdinalIgnoreCase)
            ? AlignService.DistributeType.Vertical
            : AlignService.DistributeType.Horizontal;

        var items = GetSelectedItemsForAlignment();
        if (items.Count < 3)
        {
            return;
        }

        _alignService.Distribute(items, type);
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
    }

    private void AlignSelection(AlignService.AlignType type)
    {
        var items = GetSelectedItemsForAlignment();
        if (items.Count < 2)
        {
            return;
        }

        _alignService.Align(items, type);
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
    }

    private void DistributeSelection(AlignService.DistributeType type)
    {
        var items = GetSelectedItemsForAlignment();
        if (items.Count < 3)
        {
            return;
        }

        _alignService.Distribute(items, type);
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
    }

    private void ApplyViewportOptions()
    {
        _selectionService.GridSize = _gridSize;
        _selectionService.SnapToGrid = _isSnapEnabled;
        EditorSvg.Wireframe = _isWireframeEnabled;
        EditorSvg.DisableFilters = _areFiltersDisabled;

        EditorOverlay.GridSize = _gridSize;
        EditorOverlay.ShowGrid = _isGridVisible;
        EditorOverlay.SnapToGrid = _isSnapEnabled;

        if (_activePage is not null)
        {
            _activePage.GridSize = _gridSize;
            _activePage.IsGridVisible = _isGridVisible;
            _activePage.IsSnapEnabled = _isSnapEnabled;
        }

        RefreshComputedState();
        RefreshOverlay();
    }

    private void ApplyPropertyChanges()
    {
        if (_selectedElement is null)
        {
            return;
        }

        _propertiesService.ApplyAll(_selectedElement);
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
    }

    private void ApplyFillState()
    {
        if (_selectedElement is null || !IsFillColorEditable || _selectedElements.Count != 1)
        {
            return;
        }

        if (!IsFillEnabled)
        {
            _selectedElement.Fill = SvgPaintServer.None;
            _selectedElement.FillOpacity = 1f;
            ClearPaintStyleLink(_selectedElement, EditorPaintTarget.Fill);
            RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
            return;
        }

        var solidFill = _selectedElement.Fill as SvgColourServer;
        var drawingColor = System.Drawing.Color.FromArgb(255, FillColor.R, FillColor.G, FillColor.B);
        if (solidFill is null || ReferenceEquals(solidFill, SvgPaintServer.None) || ReferenceEquals(solidFill, SvgPaintServer.Inherit) || ReferenceEquals(solidFill, SvgPaintServer.NotSet))
        {
            _selectedElement.Fill = new SvgColourServer(drawingColor);
        }
        else
        {
            solidFill.Colour = drawingColor;
        }

        _selectedElement.FillOpacity = FillColor.A / 255f;
        ClearPaintStyleLink(_selectedElement, EditorPaintTarget.Fill);
        RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
    }

    private void RefreshAutoLayoutInspectorState()
    {
        _isUpdatingAutoLayoutState = true;
        try
        {
            if (TryGetAutoLayoutFrame(out var frame) && _autoLayoutService.TryGetFrameBackground(frame, out var background))
            {
                var settings = _autoLayoutService.ReadSettings(frame);
                IsAutoLayoutEnabled = settings.IsEnabled;
                AutoLayoutFlow = settings.Flow;
                AutoLayoutWidthMode = settings.WidthMode;
                AutoLayoutHeightMode = settings.HeightMode;
                AutoLayoutHorizontalAlignment = settings.HorizontalAlignment;
                AutoLayoutVerticalAlignment = settings.VerticalAlignment;
                AutoLayoutWidth = background.Width.Value.ToString("0.##", CultureInfo.InvariantCulture);
                AutoLayoutHeight = background.Height.Value.ToString("0.##", CultureInfo.InvariantCulture);
                AutoLayoutGap = settings.Gap.ToString("0.##", CultureInfo.InvariantCulture);
                AutoLayoutPaddingHorizontal = settings.PaddingHorizontal.ToString("0.##", CultureInfo.InvariantCulture);
                AutoLayoutPaddingVertical = settings.PaddingVertical.ToString("0.##", CultureInfo.InvariantCulture);
                IsAutoLayoutClipContent = settings.ClipContent;
            }
            else
            {
                IsAutoLayoutEnabled = false;
                AutoLayoutFlow = AutoLayoutFlow.Vertical;
                AutoLayoutWidthMode = AutoLayoutSizeMode.Fixed;
                AutoLayoutHeightMode = AutoLayoutSizeMode.Fixed;
                AutoLayoutHorizontalAlignment = AutoLayoutAlignment.Start;
                AutoLayoutVerticalAlignment = AutoLayoutAlignment.Start;
                AutoLayoutWidth = "-";
                AutoLayoutHeight = "-";
                AutoLayoutGap = "24";
                AutoLayoutPaddingHorizontal = "24";
                AutoLayoutPaddingVertical = "24";
                IsAutoLayoutClipContent = false;
            }
        }
        finally
        {
            _isUpdatingAutoLayoutState = false;
        }

        RaisePropertyChanged(nameof(CanEditAutoLayout));
    }

    private void RefreshFrameInspectorState()
    {
        _isUpdatingFrameState = true;
        try
        {
            if (TryGetEditableContainer(out var group))
            {
                FrameKind = FrameService.GetContainerKind(group);
                if (FrameService.TryGetBackground(group, out var background))
                {
                    SelectedFramePresetId = FindMatchingFramePresetId(background.Width.Value, background.Height.Value)
                        ?? FrameService.GetPresetId(group)
                        ?? EditorFramePresetItem.CustomId;
                }
                else
                {
                    SelectedFramePresetId = EditorFramePresetItem.CustomId;
                }
            }
            else
            {
                FrameKind = FrameContainerKind.Group;
                SelectedFramePresetId = EditorFramePresetItem.CustomId;
            }
        }
        finally
        {
            _isUpdatingFrameState = false;
        }

        RaisePropertyChanged(nameof(CanEditFrame));
        RaisePropertyChanged(nameof(CanUseFramePresets));
        RaisePropertyChanged(nameof(FrameSummary));
    }

    private void ApplyAutoLayoutState()
    {
        if (_document is null || !TryGetAutoLayoutFrame(out var frame) || !TryBuildAutoLayoutSettings(frame, out var settings))
        {
            return;
        }

        _autoLayoutService.WriteSettings(frame, settings);
        _autoLayoutService.UpdateClipPath(_document, frame, settings.IsEnabled && settings.ClipContent);
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
    }

    private void ApplyFrameKindState()
    {
        if (_document is null || !TryGetEditableContainer(out var group))
        {
            return;
        }

        ConvertContainerKind(group, FrameKind);
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection([group], group);
    }

    private void ApplyFramePresetState()
    {
        if (_document is null
            || !TryGetEditableContainer(out var group)
            || FrameService.GetContainerKind(group) == FrameContainerKind.Group
            || string.IsNullOrWhiteSpace(SelectedFramePresetId)
            || string.Equals(SelectedFramePresetId, EditorFramePresetItem.CustomId, StringComparison.Ordinal)
            || FramePresets.FirstOrDefault(item => string.Equals(item.Id, SelectedFramePresetId, StringComparison.Ordinal)) is not { } preset)
        {
            return;
        }

        if (!FrameService.TryGetBackground(group, out var background))
        {
            return;
        }

        background.Width = new SvgUnit(background.Width.Type, Math.Max((float)preset.Width, DefaultContainerMinSize));
        background.Height = new SvgUnit(background.Height.Type, Math.Max((float)preset.Height, DefaultContainerMinSize));
        FrameService.SetPresetId(group, preset.Id);
        FrameService.SyncMetadata(group);

        if (FrameService.GetContainerKind(group) != FrameContainerKind.Frame)
        {
            DisableAutoLayout(group);
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection([group], group);
    }

    private bool TryGetAutoLayoutFrame(out SvgGroup frame)
    {
        frame = null!;
        if (!TryGetEditableContainer(out var group)
            || !FrameService.IsFrameLikeGroup(group)
            || FrameService.GetContainerKind(group) != FrameContainerKind.Frame)
        {
            return false;
        }

        frame = group;
        return true;
    }

    private bool TryBuildAutoLayoutSettings(SvgGroup frame, out AutoLayoutSettings settings)
    {
        settings = new AutoLayoutSettings
        {
            IsEnabled = IsAutoLayoutEnabled,
            Flow = AutoLayoutFlow,
            WidthMode = AutoLayoutWidthMode,
            HeightMode = AutoLayoutHeightMode,
            HorizontalAlignment = AutoLayoutHorizontalAlignment,
            VerticalAlignment = AutoLayoutVerticalAlignment,
            ClipContent = IsAutoLayoutClipContent
        };

        if (!_autoLayoutService.TryGetFrameBackground(frame, out var background))
        {
            return false;
        }

        if (TryParseDouble(AutoLayoutWidth, out var width))
        {
            background.Width = new SvgUnit(background.Width.Type, Math.Max((float)width, 1f));
        }

        if (TryParseDouble(AutoLayoutHeight, out var height))
        {
            background.Height = new SvgUnit(background.Height.Type, Math.Max((float)height, 1f));
        }

        settings.Gap = TryParseDouble(AutoLayoutGap, out var gap) ? Math.Max((float)gap, 0f) : 24f;
        settings.PaddingHorizontal = TryParseDouble(AutoLayoutPaddingHorizontal, out var paddingHorizontal) ? Math.Max((float)paddingHorizontal, 0f) : 24f;
        settings.PaddingVertical = TryParseDouble(AutoLayoutPaddingVertical, out var paddingVertical) ? Math.Max((float)paddingVertical, 0f) : 24f;
        return true;
    }

    private bool TryGetEditableContainer(out SvgGroup group)
    {
        group = null!;
        if (_selectedElements.Count != 1 || _selectedElement is not SvgGroup candidate)
        {
            return false;
        }

        if (_autoLayoutService.IsFrameContentGroup(candidate) || IsInsideSymbol(candidate))
        {
            return false;
        }

        group = candidate;
        return true;
    }

    private string? FindMatchingFramePresetId(float width, float height)
    {
        const double tolerance = 0.5;
        return FramePresets
            .FirstOrDefault(item =>
                !item.IsCustom
                && Math.Abs(item.Width - width) <= tolerance
                && Math.Abs(item.Height - height) <= tolerance)
            ?.Id;
    }

    private void ConvertContainerKind(SvgGroup group, FrameContainerKind targetKind)
    {
        var currentKind = FrameService.GetContainerKind(group);
        if (currentKind == targetKind && (targetKind == FrameContainerKind.Group || FrameService.TryGetBackground(group, out _)))
        {
            return;
        }

        if (targetKind == FrameContainerKind.Group)
        {
            ConvertContainerToPlainGroup(group);
            return;
        }

        if (!FrameService.IsFrameLikeGroup(group))
        {
            var bounds = GetContainerBoundsForConversion(group);
            FrameService.SetContainerKind(group, targetKind);
            var background = FrameService.EnsureBackgroundRect(group, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            FrameService.ApplyDefaultAppearance(background, targetKind);
            var contentGroup = _autoLayoutService.EnsureContentGroup(group);
            if (string.IsNullOrWhiteSpace(contentGroup.ID) && !string.IsNullOrWhiteSpace(group.ID))
            {
                contentGroup.ID = $"{group.ID}-content";
            }

            FrameService.SetPresetId(group, null);
            FrameService.SyncMetadata(group);
        }
        else
        {
            FrameService.SetContainerKind(group, targetKind);
            if (FrameService.TryGetBackground(group, out var background))
            {
                FrameService.ApplyDefaultAppearance(background, targetKind);
                FrameService.SyncMetadata(group);
            }
        }

        if (targetKind != FrameContainerKind.Frame)
        {
            DisableAutoLayout(group);
        }
    }

    private void ConvertContainerToPlainGroup(SvgGroup group)
    {
        if (_document is null)
        {
            return;
        }

        DisableAutoLayout(group);

        var contentGroup = group.Children
            .OfType<SvgGroup>()
            .FirstOrDefault(_autoLayoutService.IsFrameContentGroup);
        var children = new List<SvgElement>();

        if (contentGroup is not null)
        {
            foreach (var child in contentGroup.Children.OfType<SvgElement>().ToList())
            {
                contentGroup.Children.Remove(child);
                children.Add(child);
            }

            foreach (var child in group.Children
                         .OfType<SvgElement>()
                         .Where(child => !ReferenceEquals(child, contentGroup) && !FrameService.IsFrameBackground(child))
                         .ToList())
            {
                group.Children.Remove(child);
                children.Add(child);
            }
        }
        else
        {
            foreach (var child in group.Children
                         .OfType<SvgElement>()
                         .Where(child => !FrameService.IsFrameBackground(child))
                         .ToList())
            {
                group.Children.Remove(child);
                children.Add(child);
            }
        }

        if (FrameService.TryGetBackground(group, out var background))
        {
            group.Children.Remove(background);
        }

        if (contentGroup is not null)
        {
            group.Children.Remove(contentGroup);
        }

        FrameService.SetContainerKind(group, FrameContainerKind.Group);
        foreach (var child in children)
        {
            group.Children.Add(child);
        }
    }

    private void DisableAutoLayout(SvgGroup group)
    {
        if (_document is null)
        {
            return;
        }

        _autoLayoutService.WriteSettings(group, new AutoLayoutSettings());
        _autoLayoutService.UpdateClipPath(_document, group, false);
    }

    private Shim.SKRect GetContainerBoundsForConversion(SvgGroup group)
    {
        if (TryGetFrameBackground(group, out var background))
        {
            return new Shim.SKRect(
                background.X.Value,
                background.Y.Value,
                background.X.Value + background.Width.Value,
                background.Y.Value + background.Height.Value);
        }

        if (TryGetGroupContentBounds(group, out var bounds))
        {
            var extraWidth = Math.Max(DefaultContainerMinSize - bounds.Width, 0f);
            var extraHeight = Math.Max(DefaultContainerMinSize - bounds.Height, 0f);
            return new Shim.SKRect(
                bounds.Left - (extraWidth / 2f),
                bounds.Top - (extraHeight / 2f),
                bounds.Right + (extraWidth / 2f),
                bounds.Bottom + (extraHeight / 2f));
        }

        var insertPoint = GetDefaultInsertPoint();
        return new Shim.SKRect(
            insertPoint.X - 240f,
            insertPoint.Y - 180f,
            insertPoint.X + 240f,
            insertPoint.Y + 180f);
    }

    private bool TryGetGroupContentBounds(SvgGroup group, out Shim.SKRect bounds)
    {
        bounds = default;
        var visuals = group
            .Descendants()
            .OfType<SvgVisualElement>()
            .Where(element => !FrameService.IsFrameBackground(element))
            .ToList();

        return visuals.Count > 0 && TryGetElementBounds(visuals, out bounds);
    }

    protected void OnEffectItemsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<EditorEffectItem>())
            {
                item.PropertyChanged -= OnEffectItemPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<EditorEffectItem>())
            {
                item.PropertyChanged += OnEffectItemPropertyChanged;
            }
        }

        if (_isUpdatingEffectsState)
        {
            return;
        }

        ApplyEffectsState();
    }

    protected void OnEffectItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isUpdatingEffectsState || sender is not EditorEffectItem)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(EditorEffectItem.Kind):
            case nameof(EditorEffectItem.IsEnabled):
            case nameof(EditorEffectItem.OffsetX):
            case nameof(EditorEffectItem.OffsetY):
            case nameof(EditorEffectItem.Blur):
            case nameof(EditorEffectItem.Spread):
            case nameof(EditorEffectItem.Scale):
            case nameof(EditorEffectItem.Amount):
            case nameof(EditorEffectItem.Distortion):
            case nameof(EditorEffectItem.Saturation):
            case nameof(EditorEffectItem.Color):
                ApplyEffectsState();
                break;
        }
    }

    private void RefreshEffectsInspectorState()
    {
        _isUpdatingEffectsState = true;
        try
        {
            EffectItems.Clear();

            if (_selectedElements.Count == 1 && _selectedElement is not null)
            {
                foreach (var effect in LoadEffectItems(_selectedElement))
                {
                    EffectItems.Add(effect);
                }
            }
        }
        finally
        {
            _isUpdatingEffectsState = false;
        }

        RaisePropertyChanged(nameof(CanEditEffects));
    }

    private void ApplyEffectsState()
    {
        if (_document is null || _selectedElement is null || _selectedElements.Count != 1)
        {
            return;
        }

        if (EffectItems.Count == 0)
        {
            RemoveEffectFilter(_selectedElement);
            RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
            return;
        }

        var definitions = EnsureDefinitions(_document);
        var filter = EnsureEffectFilter(_selectedElement, definitions);
        BuildEffectFilter(_selectedElement, filter, EffectItems);
        _selectedElement.Filter = new Uri($"#{filter.ID}", UriKind.Relative);
        RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
    }

    private void BuildEffectFilter(SvgVisualElement element, SvgFilter filter, IReadOnlyCollection<EditorEffectItem> effects)
    {
        filter.Children.Clear();
        filter.CustomAttributes[EffectsMetadataKey] = SerializeEffects(effects);
        ConfigureEffectFilterRegion(element, filter, effects);

        var currentInput = SvgFilterPrimitive.SourceGraphic;
        var activeEffects = effects.Where(static item => item.IsEnabled).ToList();
        if (activeEffects.Count == 0)
        {
            AppendPassThrough(filter, currentInput, "fx-pass");
            return;
        }

        for (var index = 0; index < activeEffects.Count; index++)
        {
            currentInput = AppendEffectPrimitiveStack(filter, activeEffects[index], index, currentInput);
        }
    }

    private static string AppendEffectPrimitiveStack(SvgFilter filter, EditorEffectItem effect, int index, string currentInput)
    {
        var prefix = $"fx-{index}";

        return effect.Kind switch
        {
            EditorEffectKind.DropShadow => AppendDropShadow(filter, effect, prefix, currentInput),
            EditorEffectKind.InnerShadow => AppendInnerShadow(filter, effect, prefix, currentInput),
            EditorEffectKind.LayerBlur => AppendLayerBlur(filter, effect, prefix, currentInput),
            EditorEffectKind.BackgroundBlur => AppendBackgroundBlur(filter, effect, prefix, currentInput),
            EditorEffectKind.Noise => AppendNoise(filter, effect, prefix, currentInput),
            EditorEffectKind.Texture => AppendTexture(filter, effect, prefix, currentInput),
            EditorEffectKind.Glass => AppendGlass(filter, effect, prefix, currentInput),
            _ => AppendPassThrough(filter, currentInput, $"{prefix}-out")
        };
    }

    private static string AppendDropShadow(SvgFilter filter, EditorEffectItem effect, string prefix, string currentInput)
    {
        var alpha = AppendAlphaMask(filter, currentInput, $"{prefix}-alpha");
        var spread = AppendSpreadMorphology(filter, alpha, effect.Spread, $"{prefix}-spread");
        var offset = AppendOffset(filter, spread, effect.OffsetX, effect.OffsetY, $"{prefix}-offset");
        var blur = AppendBlur(filter, offset, effect.Blur, $"{prefix}-blur");
        var shadow = AppendColoredMask(filter, blur, effect.Color, $"{prefix}-shadow");
        return AppendMerge(filter, $"{prefix}-merge", shadow, currentInput);
    }

    private static string AppendInnerShadow(SvgFilter filter, EditorEffectItem effect, string prefix, string currentInput)
    {
        var alpha = AppendAlphaMask(filter, currentInput, $"{prefix}-alpha");
        var spread = AppendSpreadMorphology(filter, alpha, effect.Spread, $"{prefix}-spread");
        var offset = AppendOffset(filter, spread, effect.OffsetX, effect.OffsetY, $"{prefix}-offset");
        var blur = AppendBlur(filter, offset, effect.Blur, $"{prefix}-blur");
        var innerMask = AppendComposite(filter, alpha, blur, SvgCompositeOperator.Out, $"{prefix}-inner-mask");
        var shadow = AppendColoredMask(filter, innerMask, effect.Color, $"{prefix}-inner-color");
        return AppendMerge(filter, $"{prefix}-merge", currentInput, shadow);
    }

    private static string AppendLayerBlur(SvgFilter filter, EditorEffectItem effect, string prefix, string currentInput)
    {
        return AppendBlur(filter, currentInput, effect.Blur, $"{prefix}-blur");
    }

    private static string AppendBackgroundBlur(SvgFilter filter, EditorEffectItem effect, string prefix, string currentInput)
    {
        var alpha = AppendAlphaMask(filter, currentInput, $"{prefix}-alpha");
        var blur = AppendBlur(filter, SvgFilterPrimitive.BackgroundImage, effect.Blur, $"{prefix}-bg-blur");
        var clipped = AppendComposite(filter, blur, alpha, SvgCompositeOperator.In, $"{prefix}-bg-clip");
        return AppendMerge(filter, $"{prefix}-merge", clipped, currentInput);
    }

    private static string AppendNoise(SvgFilter filter, EditorEffectItem effect, string prefix, string currentInput)
    {
        var alpha = AppendAlphaMask(filter, currentInput, $"{prefix}-alpha");
        var turbulence = AppendTurbulence(
            filter,
            $"{prefix}-noise",
            Math.Max(0.001, effect.Scale),
            Math.Max(0.001, effect.Scale * 1.35),
            Math.Clamp((int)Math.Round(effect.Amount / 8.0), 1, 5),
            indexSeed: prefix.GetHashCode(),
            SvgTurbulenceType.FractalNoise);
        var clipped = AppendComposite(filter, turbulence, alpha, SvgCompositeOperator.In, $"{prefix}-clip");
        var colored = AppendColoredMask(filter, clipped, effect.Color, $"{prefix}-color");
        return AppendBlend(filter, colored, currentInput, SvgBlendMode.Overlay, $"{prefix}-blend");
    }

    private static string AppendTexture(SvgFilter filter, EditorEffectItem effect, string prefix, string currentInput)
    {
        var alpha = AppendAlphaMask(filter, currentInput, $"{prefix}-alpha");
        var turbulence = AppendTurbulence(
            filter,
            $"{prefix}-texture",
            Math.Max(0.001, effect.Scale),
            Math.Max(0.001, effect.Scale * 0.85),
            Math.Clamp((int)Math.Round(effect.Amount / 10.0), 1, 6),
            indexSeed: prefix.GetHashCode(),
            SvgTurbulenceType.Turbulence);
        var displacement = new SvgDisplacementMap
        {
            Input = currentInput,
            Input2 = turbulence,
            Scale = (float)Math.Max(0.0, effect.Distortion),
            XChannelSelector = SvgChannelSelector.R,
            YChannelSelector = SvgChannelSelector.G,
            Result = $"{prefix}-displace"
        };
        filter.Children.Add(displacement);

        var clipped = AppendComposite(filter, turbulence, alpha, SvgCompositeOperator.In, $"{prefix}-clip");
        var colored = AppendColoredMask(filter, clipped, effect.Color, $"{prefix}-color");
        return AppendBlend(filter, colored, displacement.Result, SvgBlendMode.Multiply, $"{prefix}-blend");
    }

    private static string AppendGlass(SvgFilter filter, EditorEffectItem effect, string prefix, string currentInput)
    {
        var alpha = AppendAlphaMask(filter, currentInput, $"{prefix}-alpha");
        var blur = AppendBlur(filter, SvgFilterPrimitive.BackgroundImage, effect.Blur, $"{prefix}-bg-blur");
        var turbulence = AppendTurbulence(
            filter,
            $"{prefix}-noise",
            0.018,
            0.025,
            3,
            indexSeed: prefix.GetHashCode(),
            SvgTurbulenceType.FractalNoise);
        var displacement = new SvgDisplacementMap
        {
            Input = blur,
            Input2 = turbulence,
            Scale = (float)Math.Max(0.0, effect.Distortion),
            XChannelSelector = SvgChannelSelector.R,
            YChannelSelector = SvgChannelSelector.G,
            Result = $"{prefix}-displace"
        };
        filter.Children.Add(displacement);

        var saturation = new SvgColourMatrix
        {
            Input = displacement.Result,
            Type = SvgColourMatrixType.Saturate,
            Values = Math.Max(0.0, effect.Saturation).ToString("0.###", CultureInfo.InvariantCulture),
            Result = $"{prefix}-saturate"
        };
        filter.Children.Add(saturation);

        var clipped = AppendComposite(filter, saturation.Result, alpha, SvgCompositeOperator.In, $"{prefix}-clip");
        var tint = AppendColoredMask(filter, alpha, effect.Color, $"{prefix}-tint");
        var blend = AppendBlend(filter, tint, clipped, SvgBlendMode.Screen, $"{prefix}-glass");
        return AppendMerge(filter, $"{prefix}-merge", blend, currentInput);
    }

    private static string AppendAlphaMask(SvgFilter filter, string input, string resultKey)
    {
        var alpha = new SvgColourMatrix
        {
            Input = input,
            Type = SvgColourMatrixType.Matrix,
            Values = "0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1 0",
            Result = resultKey
        };
        filter.Children.Add(alpha);
        return resultKey;
    }

    private static string AppendSpreadMorphology(SvgFilter filter, string input, double spread, string resultKey)
    {
        if (Math.Abs(spread) < 0.01)
        {
            return input;
        }

        var radius = (float)Math.Abs(spread);
        var morphology = new SvgMorphology
        {
            Input = input,
            Operator = spread >= 0.0 ? SvgMorphologyOperator.Dilate : SvgMorphologyOperator.Erode,
            Radius = new SvgNumberCollection { radius, radius },
            Result = resultKey
        };
        filter.Children.Add(morphology);
        return resultKey;
    }

    private static string AppendOffset(SvgFilter filter, string input, double x, double y, string resultKey)
    {
        if (Math.Abs(x) < 0.01 && Math.Abs(y) < 0.01)
        {
            return input;
        }

        var offset = new SvgOffset
        {
            Input = input,
            Dx = new SvgUnit(SvgUnitType.User, (float)x),
            Dy = new SvgUnit(SvgUnitType.User, (float)y),
            Result = resultKey
        };
        filter.Children.Add(offset);
        return resultKey;
    }

    private static string AppendBlur(SvgFilter filter, string input, double blur, string resultKey)
    {
        if (blur < 0.01)
        {
            return input;
        }

        var gaussianBlur = new SvgGaussianBlur
        {
            Input = input,
            StdDeviation = new SvgNumberCollection { (float)blur, (float)blur },
            Result = resultKey
        };
        filter.Children.Add(gaussianBlur);
        return resultKey;
    }

    private static string AppendColoredMask(SvgFilter filter, string input, Color color, string prefix)
    {
        var floodResult = $"{prefix}-flood";
        var flood = new SvgFlood
        {
            FloodColor = new SvgColourServer(System.Drawing.Color.FromArgb(255, color.R, color.G, color.B)),
            FloodOpacity = color.A / 255f,
            Result = floodResult
        };
        filter.Children.Add(flood);

        var composite = new SvgComposite
        {
            Input = floodResult,
            Input2 = input,
            Operator = SvgCompositeOperator.In,
            Result = $"{prefix}-paint"
        };
        filter.Children.Add(composite);
        return composite.Result;
    }

    private static string AppendComposite(SvgFilter filter, string input, string input2, SvgCompositeOperator operation, string resultKey)
    {
        var composite = new SvgComposite
        {
            Input = input,
            Input2 = input2,
            Operator = operation,
            Result = resultKey
        };
        filter.Children.Add(composite);
        return resultKey;
    }

    private static string AppendBlend(SvgFilter filter, string input, string input2, SvgBlendMode mode, string resultKey)
    {
        var blend = new SvgBlend
        {
            Input = input,
            Input2 = input2,
            Mode = mode,
            Result = resultKey
        };
        filter.Children.Add(blend);
        return resultKey;
    }

    private static string AppendMerge(SvgFilter filter, string resultKey, params string[] inputs)
    {
        var merge = new SvgMerge
        {
            Result = resultKey
        };

        foreach (var input in inputs.Where(static item => !string.IsNullOrWhiteSpace(item)))
        {
            merge.Children.Add(new SvgMergeNode { Input = input });
        }

        filter.Children.Add(merge);
        return resultKey;
    }

    private static string AppendPassThrough(SvgFilter filter, string currentInput, string resultKey)
    {
        return AppendMerge(filter, resultKey, currentInput);
    }

    private static string AppendTurbulence(SvgFilter filter, string resultKey, double freqX, double freqY, int octaves, int indexSeed, SvgTurbulenceType type)
    {
        var turbulence = new SvgTurbulence
        {
            BaseFrequency = new SvgNumberCollection { (float)freqX, (float)freqY },
            NumOctaves = octaves,
            Seed = Math.Abs(indexSeed % 999),
            StitchTiles = SvgStitchType.NoStitch,
            Type = type,
            Result = resultKey
        };
        filter.Children.Add(turbulence);
        return resultKey;
    }

    private void ConfigureEffectFilterRegion(SvgVisualElement element, SvgFilter filter, IReadOnlyCollection<EditorEffectItem> effects)
    {
        var bounds = GetEffectBounds(element);
        var padding = ComputeEffectPadding(effects);

        filter.FilterUnits = SvgCoordinateUnits.UserSpaceOnUse;
        filter.PrimitiveUnits = SvgCoordinateUnits.UserSpaceOnUse;
        filter.X = new SvgUnit(SvgUnitType.User, bounds.Left - padding);
        filter.Y = new SvgUnit(SvgUnitType.User, bounds.Top - padding);
        filter.Width = new SvgUnit(SvgUnitType.User, Math.Max(1f, bounds.Width + (padding * 2f)));
        filter.Height = new SvgUnit(SvgUnitType.User, Math.Max(1f, bounds.Height + (padding * 2f)));
    }

    private float ComputeEffectPadding(IReadOnlyCollection<EditorEffectItem> effects)
    {
        var padding = 32f;
        foreach (var effect in effects)
        {
            padding = Math.Max(
                padding,
                (float)(
                    Math.Abs(effect.OffsetX)
                    + Math.Abs(effect.OffsetY)
                    + (effect.Blur * 3.0)
                    + Math.Abs(effect.Spread)
                    + Math.Abs(effect.Distortion)
                    + 24.0));
        }

        return padding;
    }

    private SK.SKRect GetEffectBounds(SvgVisualElement element)
    {
        if (_selectedDrawable is not null && ReferenceEquals(element, _selectedElement))
        {
            return GetResizeRect(element, _selectedDrawable);
        }

        if (EditorSvg.SkSvg?.Drawable is DrawableBase root && FindDrawable(root, element) is { } drawable)
        {
            return GetResizeRect(element, drawable);
        }

        return _document is null
            ? SK.SKRect.Create(0f, 0f, 256f, 256f)
            : SK.SKRect.Create(0f, 0f, Math.Max(1f, _document.Width.Value), Math.Max(1f, _document.Height.Value));
    }

    private List<EditorEffectItem> LoadEffectItems(SvgVisualElement element)
    {
        var filter = ResolveEffectFilter(element);
        if (filter is null)
        {
            return [];
        }

        if (filter.CustomAttributes.TryGetValue(EffectsMetadataKey, out var serialized))
        {
            var items = DeserializeEffects(serialized);
            if (items.Count > 0)
            {
                return items;
            }
        }

        var inferred = new List<EditorEffectItem>();
        if (TryInferDropShadow(filter, out var dropShadow))
        {
            inferred.Add(dropShadow);
        }
        else if (TryInferLayerBlur(filter, out var layerBlur))
        {
            inferred.Add(layerBlur);
        }

        return inferred;
    }

    private static bool TryInferLayerBlur(SvgFilter filter, out EditorEffectItem effect)
    {
        effect = default!;
        var blur = filter.Children.OfType<SvgGaussianBlur>().FirstOrDefault();
        if (blur is null || filter.Children.Count != 1)
        {
            return false;
        }

        var sigma = blur.StdDeviation.FirstOrDefault();
        effect = EditorEffectItem.CreateDefault(EditorEffectKind.LayerBlur);
        effect.Blur = sigma;
        return true;
    }

    private static bool TryInferDropShadow(SvgFilter filter, out EditorEffectItem effect)
    {
        effect = default!;
        var blur = filter.Children.OfType<SvgGaussianBlur>().FirstOrDefault();
        var offset = filter.Children.OfType<SvgOffset>().FirstOrDefault();
        var flood = filter.Children.OfType<SvgFlood>().FirstOrDefault();
        if (blur is null || offset is null || flood is null)
        {
            return false;
        }

        var spread = filter.Children.OfType<SvgMorphology>().FirstOrDefault();
        var color = flood.FloodColor as SvgColourServer;
        effect = EditorEffectItem.CreateDefault(EditorEffectKind.DropShadow);
        effect.OffsetX = offset.Dx.Value;
        effect.OffsetY = offset.Dy.Value;
        effect.Blur = blur.StdDeviation.FirstOrDefault();
        effect.Spread = spread?.Radius.FirstOrDefault() ?? 0f;
        if (color is not null)
        {
            effect.Color = Color.FromArgb(
                (byte)Math.Clamp((int)Math.Round(flood.FloodOpacity * 255f), 0, 255),
                color.Colour.R,
                color.Colour.G,
                color.Colour.B);
        }

        return true;
    }

    private static string SerializeEffects(IEnumerable<EditorEffectItem> effects)
    {
        return string.Join(
            ";",
            effects.Select(static effect =>
                string.Join(
                    "|",
                    effect.Kind,
                    effect.IsEnabled ? "1" : "0",
                    effect.OffsetX.ToString("0.###", CultureInfo.InvariantCulture),
                    effect.OffsetY.ToString("0.###", CultureInfo.InvariantCulture),
                    effect.Blur.ToString("0.###", CultureInfo.InvariantCulture),
                    effect.Spread.ToString("0.###", CultureInfo.InvariantCulture),
                    effect.Scale.ToString("0.###", CultureInfo.InvariantCulture),
                    effect.Amount.ToString("0.###", CultureInfo.InvariantCulture),
                    effect.Distortion.ToString("0.###", CultureInfo.InvariantCulture),
                    effect.Saturation.ToString("0.###", CultureInfo.InvariantCulture),
                    $"{effect.Color.A:X2}{effect.Color.R:X2}{effect.Color.G:X2}{effect.Color.B:X2}")));
    }

    private static List<EditorEffectItem> DeserializeEffects(object? serialized)
    {
        var value = serialized?.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var effects = new List<EditorEffectItem>();

        foreach (var entry in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split('|');
            if (parts.Length != 11 || !Enum.TryParse<EditorEffectKind>(parts[0], true, out var kind))
            {
                continue;
            }

            var effect = EditorEffectItem.CreateDefault(kind);
            effect.IsEnabled = parts[1] == "1";
            effect.OffsetX = ParseOrDefault(parts[2], effect.OffsetX);
            effect.OffsetY = ParseOrDefault(parts[3], effect.OffsetY);
            effect.Blur = ParseOrDefault(parts[4], effect.Blur);
            effect.Spread = ParseOrDefault(parts[5], effect.Spread);
            effect.Scale = ParseOrDefault(parts[6], effect.Scale);
            effect.Amount = ParseOrDefault(parts[7], effect.Amount);
            effect.Distortion = ParseOrDefault(parts[8], effect.Distortion);
            effect.Saturation = ParseOrDefault(parts[9], effect.Saturation);
            if (TryParseColorToken(parts[10], out var color))
            {
                effect.Color = color;
            }

            effect.RaiseDisplayState();
            effects.Add(effect);
        }

        return effects;
    }

    private static double ParseOrDefault(string? text, double fallback)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static bool TryParseColorToken(string? text, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text) || text.Length != 8)
        {
            return false;
        }

        if (byte.TryParse(text.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var a)
            && byte.TryParse(text.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            && byte.TryParse(text.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            && byte.TryParse(text.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            color = Color.FromArgb(a, r, g, b);
            return true;
        }

        return false;
    }

    private SvgFilter? ResolveEffectFilter(SvgVisualElement element)
    {
        return element.Filter is null
            ? null
            : element.OwnerDocument?.GetElementById(element.Filter.ToString()) as SvgFilter;
    }

    private void RemoveEffectFilter(SvgVisualElement element)
    {
        var filter = ResolveEffectFilter(element);
        if (_document is not null && filter?.Parent is SvgDefinitionList definitions)
        {
            definitions.Children.Remove(filter);
        }

        element.Filter = null!;
    }

    private SvgDefinitionList EnsureDefinitions(SvgDocument document)
    {
        var definitions = document.Children.OfType<SvgDefinitionList>().FirstOrDefault();
        if (definitions is not null)
        {
            return definitions;
        }

        definitions = new SvgDefinitionList
        {
            ID = "defs-generated"
        };
        document.Children.Insert(0, definitions);
        return definitions;
    }

    private SvgFilter EnsureEffectFilter(SvgVisualElement element, SvgDefinitionList definitions)
    {
        EnsureElementId(element);
        var filterId = $"fx-{element.ID}";
        var filter = definitions.Children.OfType<SvgFilter>().FirstOrDefault(existing => string.Equals(existing.ID, filterId, StringComparison.Ordinal));
        if (filter is not null)
        {
            return filter;
        }

        filter = new SvgFilter
        {
            ID = filterId
        };
        definitions.Children.Add(filter);
        return filter;
    }

    private void ApplyQuickFieldChanges(TextBox textBox)
    {
        if (_selectedElements.Count == 0 || textBox.Tag is not string propertyName)
        {
            return;
        }

        if (!TryParseDouble(textBox.Text, out var value))
        {
            RefreshQuickInspector();
            return;
        }

        var selectionBounds = GetSelectionBounds();
        if (selectionBounds is null)
        {
            return;
        }

        switch (propertyName)
        {
            case "X":
                {
                    var dx = (float)(value - selectionBounds.Value.Left);
                    TranslateSelection(dx, 0f);
                    break;
                }
            case "Y":
                {
                    var dy = (float)(value - selectionBounds.Value.Top);
                    TranslateSelection(0f, dy);
                    break;
                }
            case "Width" when _selectedElements.Count == 1 && _selectedElement is not null:
                {
                    if (!TryResizeSingleElement(_selectedElement, (float)value, null))
                    {
                        RefreshQuickInspector();
                        return;
                    }

                    break;
                }
            case "Height" when _selectedElements.Count == 1 && _selectedElement is not null:
                {
                    if (!TryResizeSingleElement(_selectedElement, null, (float)value))
                    {
                        RefreshQuickInspector();
                        return;
                    }

                    break;
                }
            case "Rotation" when _selectedElements.Count == 1 && _selectedElement is not null:
                {
                    var center = _selectedDrawable is not null
                        ? SelectionService.GetLocalCenter(_selectedDrawable)
                        : new SK.SKPoint(
                            (selectionBounds.Value.Left + selectionBounds.Value.Right) / 2f,
                            (selectionBounds.Value.Top + selectionBounds.Value.Bottom) / 2f);
                    _selectionService.SetRotation(_selectedElement, (float)value, center);
                    break;
                }
            case "Opacity" when _selectedElement is not null:
                {
                    _selectedElement.Opacity = (float)Math.Clamp(value / 100.0, 0.0, 1.0);
                    break;
                }
            case "StrokeWidth" when _selectedElements.Count == 1 && _selectedElement is not null:
                {
                    _selectedElement.StrokeWidth = new SvgUnit(Math.Max((float)value, 0f));
                    ClearPaintStyleLink(_selectedElement, EditorPaintTarget.Stroke);
                    break;
                }
            case "CornerRadius" when _selectedElements.Count == 1 && TryGetCornerRadiusTarget(_selectedElement, out var radiusTarget):
                {
                    var radius = Math.Max((float)value, 0f);
                    radiusTarget.CornerRadiusX = new SvgUnit(radiusTarget.CornerRadiusX.Type, radius);
                    radiusTarget.CornerRadiusY = new SvgUnit(radiusTarget.CornerRadiusY.Type, radius);
                    break;
                }
            default:
                RefreshQuickInspector();
                return;
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
    }

    private IReadOnlyList<EditorPageState> CreateInitialPageStates()
    {
        return new[] { CreateBlankPageState("Page 1") };
    }

    private EditorPageState? CreatePageStateFromSvg(string title, string svg)
    {
        var document = _documentService.FromSvg(svg);
        if (document is null)
        {
            return null;
        }

        EnsureIds(document);
        ConfigureFrameGroups(document);

        var state = new EditorPageState(title, "1 frame", document);
        UpdatePageMetadata(state);
        return state;
    }

    private EditorPageState CreateStoryboardPageState(string title)
    {
        var document = CreateDocument(1800, 1200);
        var primaryFrame = CreateFrameGroup("Desktop - 1", 136f, 88f, 1220f, 820f, System.Drawing.Color.White);
        primaryFrame.Children.Add(new SvgText
        {
            ID = "title-1",
            X = new SvgUnitCollection { new(SvgUnitType.User, 214f) },
            Y = new SvgUnitCollection { new(SvgUnitType.User, 196f) },
            Text = "Checkout flow",
            FontFamily = "Open Sans",
            FontSize = new SvgUnit(26f),
            Fill = new SvgColourServer(System.Drawing.Color.FromArgb(17, 24, 39))
        });
        primaryFrame.Children.Add(CreateRect("hero-card", 214f, 240f, 360f, 180f, 28f, System.Drawing.Color.FromArgb(232, 244, 255)));
        primaryFrame.Children.Add(CreateRect("hero-accent", 612f, 240f, 520f, 320f, 24f, System.Drawing.Color.FromArgb(225, 236, 255)));
        primaryFrame.Children.Add(CreateRect("cta", 214f, 458f, 164f, 52f, 18f, System.Drawing.Color.FromArgb(13, 153, 255)));
        primaryFrame.Children.Add(CreateRect("summary", 612f, 602f, 264f, 136f, 22f, System.Drawing.Color.FromArgb(247, 247, 245)));
        primaryFrame.Children.Add(CreateEllipse("token", 944f, 666f, 108f, 108f, System.Drawing.Color.FromArgb(255, 214, 96)));
        document.Children.Add(primaryFrame);

        var secondaryFrame = CreateFrameGroup("Mobile - 1", 1420f, 160f, 296f, 636f, System.Drawing.Color.FromArgb(252, 252, 251));
        secondaryFrame.Children.Add(CreateRect("mobile-header", 1454f, 212f, 228f, 54f, 18f, System.Drawing.Color.FromArgb(238, 244, 247)));
        secondaryFrame.Children.Add(CreateRect("mobile-panel", 1454f, 304f, 228f, 180f, 20f, System.Drawing.Color.FromArgb(245, 242, 238)));
        secondaryFrame.Children.Add(CreateRect("mobile-cta", 1454f, 706f, 228f, 48f, 18f, System.Drawing.Color.FromArgb(13, 153, 255)));
        document.Children.Add(secondaryFrame);

        EnsureIds(document);
        ConfigureFrameGroups(document);

        var state = new EditorPageState(title, "2 frames", document);
        UpdatePageMetadata(state);
        return state;
    }

    private EditorPageState CreateBlankPageState(string title)
    {
        var document = CreateDocument(1800, 1200);
        document.Children.Add(CreateFrameGroup($"Frame {_pageStates.Count + 1}", 120f, 96f, 1280f, 820f, System.Drawing.Color.White));
        EnsureIds(document);
        ConfigureFrameGroups(document);

        var state = new EditorPageState(title, "1 frame", document);
        UpdatePageMetadata(state);
        return state;
    }

    private async Task SwitchToPageAsync(EditorPageState state, bool selectDefaultSelection, bool resetViewport)
    {
        PersistActivePageState();

        _activePage = state;
        _document = state.Document;
        _selectedElement = null;
        _selectedDrawable = null;
        _selectedElements.Clear();
        _selectedDrawables.Clear();
        _newElement = null;
        _resizeElement = null;
        _rotateElement = null;
        _isCreating = false;
        _isDragging = false;
        _isPanning = false;
        _isResizing = false;
        _isRotating = false;
        _isMarqueeSelecting = false;
        _dragStartTranslations.Clear();
        _freehandPoints.Clear();
        _collapsedElements.Clear();
        foreach (var collapsed in state.CollapsedElements)
        {
            _collapsedElements.Add(collapsed);
        }

        UpdatePageSelectionState(state);
        ConfigureFrameGroups(state.Document);
        SyncLibrariesToDocument(state.Document);

        _gridSize = state.GridSize;
        _isGridVisible = state.IsGridVisible;
        _isSnapEnabled = state.IsSnapEnabled;
        _selectionService.GridSize = _gridSize;
        _selectionService.SnapToGrid = _isSnapEnabled;
        EditorOverlay.GridSize = _gridSize;
        EditorOverlay.ShowGrid = _isGridVisible;
        EditorOverlay.SnapToGrid = _isSnapEnabled;

        EditorSvg.Source = _documentService.GetXml(state.Document);
        EditorSvg.Zoom = resetViewport ? 1.0 : state.Zoom;
        EditorSvg.PanX = resetViewport ? 0.0 : state.PanX;
        EditorSvg.PanY = resetViewport ? 0.0 : state.PanY;

        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (EditorSvg.SkSvg is not null && EditorSvg.ReloadFromDocument(state.Document))
            {
                break;
            }

            await Task.Delay(10);
        }

        ApplyAutoLayoutPasses(state.Document);
        EditorSvg.ReloadFromDocument(state.Document);
        RefreshDocumentSwatches();
        RefreshComponentAssets();
        RebuildObjectNodes();
        SyncCommentsForPage(state);

        var selection = state.SelectedElements
            .Where(element => ReferenceEquals(element.OwnerDocument, state.Document))
            .ToList();
        var primary = state.PrimarySelection is not null && selection.Contains(state.PrimarySelection)
            ? state.PrimarySelection
            : selection.LastOrDefault();
        if (selection.Count == 0 && selectDefaultSelection)
        {
            primary = GetDefaultSelection(state.Document);
            selection = primary is null ? [] : [primary];
        }

        ApplySelection(selection, primary);
        if (state.SelectedCommentThread is not null && CommentThreads.Contains(state.SelectedCommentThread))
        {
            SelectCommentThread(state.SelectedCommentThread, focusTarget: false);
        }
    }

    private void PersistActivePageState()
    {
        if (_activePage is null)
        {
            return;
        }

        _activePage.SelectedElements.Clear();
        _activePage.SelectedElements.AddRange(_selectedElements.Where(element => ReferenceEquals(element.OwnerDocument, _activePage.Document)));
        _activePage.PrimarySelection = ReferenceEquals(_selectedElement?.OwnerDocument, _activePage.Document) ? _selectedElement : null;
        _activePage.CollapsedElements.Clear();
        foreach (var collapsed in _collapsedElements.Where(element => ReferenceEquals(element.OwnerDocument, _activePage.Document)))
        {
            _activePage.CollapsedElements.Add(collapsed);
        }

        _activePage.Zoom = EditorSvg.Zoom;
        _activePage.PanX = EditorSvg.PanX;
        _activePage.PanY = EditorSvg.PanY;
        _activePage.GridSize = _gridSize;
        _activePage.IsGridVisible = _isGridVisible;
        _activePage.IsSnapEnabled = _isSnapEnabled;
        PersistCommentState();
        UpdatePageMetadata(_activePage);
    }

    private void UpdatePageSelectionState(EditorPageState selectedPage)
    {
        foreach (var page in _pageStates)
        {
            page.Page.IsSelected = ReferenceEquals(page, selectedPage);
        }
    }

    private SvgVisualElement? GetDefaultSelection(SvgDocument document)
    {
        return GetFrameGroups(document).Cast<SvgVisualElement>().FirstOrDefault()
            ?? document.Descendants().OfType<SvgVisualElement>().FirstOrDefault();
    }

    private SvgVisualElement? CreateElementAt(Shim.SKPoint picturePoint, out Shim.SKPoint localStart)
    {
        localStart = picturePoint;
        if (_document is null)
        {
            return null;
        }

        var parent = GetCreationParent(picturePoint);
        if (!TryMapPointToElementLocal(parent, picturePoint, out localStart))
        {
            localStart = picturePoint;
        }

        var element = _toolService.CreateElement(_toolService.CurrentTool, parent, localStart);
        if (element is null)
        {
            return null;
        }

        EnsureElementId(element);
        parent.Children.Add(element);
        return element;
    }

    private bool IsSelectionTool(ToolService.Tool tool)
    {
        return tool is ToolService.Tool.Select or ToolService.Tool.Scale;
    }

    private bool IsFreehandTool(ToolService.Tool tool)
    {
        return ToolService.IsFreehandTool(tool);
    }

    private bool IsVectorPathDrawing => _isCreating && _newElement is SvgPath && _toolService.CurrentTool == ToolService.Tool.PathLine;

    private bool HandleCanvasShortcutKey(KeyRoutedEventArgs e)
    {
        if (HandleGlobalShortcutKey(e))
        {
            return true;
        }

        if (e.Key == VirtualKey.Escape)
        {
            if (IsVectorPathDrawing)
            {
                FinishVectorPath(cancelDrawing: true);
                e.Handled = true;
                return true;
            }

            if (_pathService.IsEditing)
            {
                _pathService.ActivePoint = -1;
                _pathService.Stop();
                RefreshOverlay();
                RefreshComputedState();
                e.Handled = true;
                return true;
            }
        }

        if (e.Key == VirtualKey.Enter && IsVectorPathDrawing)
        {
            FinishVectorPath(cancelDrawing: false);
            e.Handled = true;
            return true;
        }

        if (!IsInlineTextEditing
            && _selectedElement is SvgTextBase selectedText
            && (e.Key == VirtualKey.F2
                || e.Key == VirtualKey.Enter && !IsShiftPressed()))
        {
            if (StartInlineTextEdit(selectedText, selectAll: false))
            {
                e.Handled = true;
                return true;
            }
        }

        if (e.Key is VirtualKey.Delete or VirtualKey.Back && _pathService.IsEditing && _pathService.ActivePoint >= 0)
        {
            _pathService.RemoveActivePoint();
            RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
            e.Handled = true;
            return true;
        }

        if (IsPrimaryCommandModifierPressed() || IsAltPressed())
        {
            return false;
        }

        var isShift = IsShiftPressed();

        switch (e.Key)
        {
            case VirtualKey.V:
                if (isShift)
                {
                    return false;
                }

                SetTool(ToolService.Tool.Select);
                e.Handled = true;
                return true;
            case VirtualKey.C:
                if (isShift)
                {
                    return false;
                }

                SetTool(ToolService.Tool.Comment);
                e.Handled = true;
                return true;
            case VirtualKey.H:
                if (isShift)
                {
                    return false;
                }

                SetTool(ToolService.Tool.Hand);
                e.Handled = true;
                return true;
            case VirtualKey.K:
                if (isShift)
                {
                    return false;
                }

                SetTool(ToolService.Tool.Scale);
                e.Handled = true;
                return true;
            case VirtualKey.P:
                SetTool(isShift ? ToolService.Tool.Pencil : ToolService.Tool.PathLine);
                e.Handled = true;
                return true;
            case VirtualKey.R:
                if (isShift)
                {
                    return false;
                }

                SetTool(ToolService.Tool.Rect);
                e.Handled = true;
                return true;
            case VirtualKey.O:
                if (isShift)
                {
                    return false;
                }

                SetTool(ToolService.Tool.Ellipse);
                e.Handled = true;
                return true;
            case VirtualKey.L:
                SetTool(isShift ? ToolService.Tool.Arrow : ToolService.Tool.Line);
                e.Handled = true;
                return true;
            case VirtualKey.T:
                if (isShift)
                {
                    return false;
                }

                SetTool(ToolService.Tool.Text);
                e.Handled = true;
                return true;
            case VirtualKey.U:
                if (isShift)
                {
                    return false;
                }

                SetTool(ToolService.Tool.Symbol);
                if (_selectedComponentAsset is null)
                {
                    SetSidebarMode(showAssets: true);
                    CanvasStatus = "Select a component asset, then click on the stage to place an instance.";
                }
                e.Handled = true;
                return true;
            case VirtualKey.F:
                if (isShift)
                {
                    return false;
                }

                SetTool(ToolService.Tool.Frame);
                e.Handled = true;
                return true;
            case VirtualKey.S:
                SetTool(isShift ? ToolService.Tool.Section : ToolService.Tool.Slice);
                e.Handled = true;
                return true;
            case VirtualKey.B:
                if (isShift)
                {
                    return false;
                }

                SetTool(ToolService.Tool.Brush);
                e.Handled = true;
                return true;
            default:
                return false;
        }
    }

    private bool HandleGlobalShortcutKey(KeyRoutedEventArgs e)
    {
        if (e.Handled)
        {
            return true;
        }

        if (e.Key == VirtualKey.K && IsPrimaryCommandModifierPressed() && !IsAltPressed())
        {
            _ = ShowActionsPaletteAsync();
            e.Handled = true;
            return true;
        }

        return false;
    }

    private bool HandleVectorToolPointerPressed(PointerRoutedEventArgs e, Shim.SKPoint picturePoint, Point viewPoint)
    {
        var isDoubleClick = IsVectorDoubleClick(viewPoint);

        if (TryStartPathPointDrag(picturePoint))
        {
            CanvasHost.CapturePointer(e.Pointer);
            e.Handled = true;
            return true;
        }

        if (_pathService.IsEditing && isDoubleClick)
        {
            AddPathPointAt(picturePoint);
            e.Handled = true;
            return true;
        }

        if (IsVectorPathDrawing)
        {
            CommitVectorPoint(picturePoint, finalizeAfter: isDoubleClick);
            e.Handled = true;
            return true;
        }

        var hitVectorElement = GetVisualHits(viewPoint)
            .FirstOrDefault(CanEditAsVectorPath);
        if (hitVectorElement is not null
            && TrySelectEditableVectorElement(hitVectorElement, out _))
        {
            if (TryStartPathPointDrag(picturePoint))
            {
                CanvasHost.CapturePointer(e.Pointer);
            }

            e.Handled = true;
            return true;
        }

        StartVectorPath(picturePoint);
        e.Handled = true;
        return true;
    }

    private bool TryStartPathPointDrag(Shim.SKPoint picturePoint)
    {
        if (!_pathService.IsEditing)
        {
            return false;
        }

        var index = _pathService.HitPoint(new SK.SKPoint(picturePoint.X, picturePoint.Y), PathPointHandleSize, GetCanvasScale());
        if (index < 0)
        {
            return false;
        }

        _pathService.ActivePoint = index;
        RefreshOverlay();
        return true;
    }

    private void MoveActivePathPoint(Shim.SKPoint picturePoint)
    {
        if (_pathService.ActivePoint < 0)
        {
            return;
        }

        picturePoint = GetSnappedPicturePoint(picturePoint);
        var local = _pathService.PathInverse.MapPoint(picturePoint);

        _pathService.MoveActivePoint(local);
        RefreshDocumentVisual(rebuildOutline: false, reloadProperties: false);
    }

    private void StartVectorPath(Shim.SKPoint picturePoint)
    {
        var created = CreateElementAt(picturePoint, out var localStart);
        if (created is not SvgPath path)
        {
            return;
        }

        _newElement = path;
        _newStart = localStart;
        _isCreating = true;
        _pathService.CurrentSegmentTool = PathService.SegmentTool.Line;
        SelectElement(path);
        StartPathEditing(path);
        CanvasStatus = "Vector path started. Click to add anchors, double-click or press Enter to finish, and right-click or Escape to cancel.";
    }

    private void UpdateVectorPreview(Shim.SKPoint picturePoint)
    {
        if (_newElement is not SvgPath path)
        {
            return;
        }

        if (!TryGetPathLocalPoint(path, picturePoint, out var localPoint))
        {
            localPoint = picturePoint;
        }

        if (_isSnapEnabled)
        {
            localPoint = new Shim.SKPoint(_selectionService.Snap(localPoint.X), _selectionService.Snap(localPoint.Y));
        }

        if (path.PathData.LastOrDefault() is SvgLineSegment preview)
        {
            preview.End = new System.Drawing.PointF(localPoint.X, localPoint.Y);
            path.OnPathUpdated();
            RefreshDocumentVisual(rebuildOutline: false, reloadProperties: false);
        }
    }

    private void CommitVectorPoint(Shim.SKPoint picturePoint, bool finalizeAfter)
    {
        if (_newElement is not SvgPath path)
        {
            return;
        }

        if (ShouldCloseVectorPath(path, picturePoint))
        {
            CloseVectorPath(path);
            return;
        }

        if (!TryGetPathLocalPoint(path, picturePoint, out var localPoint))
        {
            localPoint = picturePoint;
        }

        if (_isSnapEnabled)
        {
            localPoint = new Shim.SKPoint(_selectionService.Snap(localPoint.X), _selectionService.Snap(localPoint.Y));
        }

        if (path.PathData.LastOrDefault() is SvgLineSegment preview)
        {
            preview.End = new System.Drawing.PointF(localPoint.X, localPoint.Y);
        }

        path.PathData.Add(new SvgLineSegment(false, new System.Drawing.PointF(localPoint.X, localPoint.Y)));
        path.OnPathUpdated();
        RefreshDocumentVisual(rebuildOutline: false, reloadProperties: false);

        if (finalizeAfter)
        {
            FinishVectorPath(cancelDrawing: false);
            return;
        }

        StartPathEditing(path);
        CanvasStatus = "Vector point added. Click to continue drawing, or press Enter to finish the path.";
    }

    private void FinishVectorPath(bool cancelDrawing)
    {
        if (_newElement is not SvgPath path)
        {
            if (!cancelDrawing && _pathService.IsEditing)
            {
                _pathService.ActivePoint = -1;
                RefreshOverlay();
            }

            return;
        }

        var removePath = cancelDrawing;
        if (!cancelDrawing && path.PathData.LastOrDefault() is SvgLineSegment preview)
        {
            if (!TryGetLastCommittedPathPoint(path, out var committedPoint)
                || NearlyEqual(committedPoint, new Shim.SKPoint(preview.End.X, preview.End.Y)))
            {
                path.PathData.Remove(preview);
            }
        }

        if (!cancelDrawing && GetPathAnchorCount(path) < 2)
        {
            removePath = true;
        }

        _isCreating = false;
        _newElement = null;

        if (removePath)
        {
            if (path.Parent is SvgElement parent)
            {
                parent.Children.Remove(path);
            }

            _pathService.Stop();
            RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
            ApplySelection(Array.Empty<SvgVisualElement>(), null);
            CanvasStatus = "Vector drawing cancelled.";
            return;
        }

        path.OnPathUpdated();
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        SelectElement(path);
        StartPathEditing(path);
        CanvasStatus = path.PathData.OfType<SvgClosePathSegment>().Any()
            ? "Vector path closed."
            : "Vector path finished.";
    }

    private void CloseVectorPath(SvgPath path)
    {
        if (path.PathData.LastOrDefault() is SvgLineSegment preview)
        {
            path.PathData.Remove(preview);
        }

        if (path.PathData.LastOrDefault() is not SvgClosePathSegment)
        {
            path.PathData.Add(new SvgClosePathSegment(false));
        }

        FinishVectorPath(cancelDrawing: false);
    }

    private void AddPathPointAt(Shim.SKPoint picturePoint)
    {
        if (!_pathService.IsEditing)
        {
            return;
        }

        picturePoint = GetSnappedPicturePoint(picturePoint);
        var local = _pathService.PathInverse.MapPoint(picturePoint);

        _pathService.AddPoint(local);
        RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
        CanvasStatus = "Added a new vector point.";
    }

    private void StartPathEditing(SvgPath path)
    {
        if (PathService.NormalizeEditablePath(path))
        {
            RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
        }

        if (_selectedDrawable is null || !ReferenceEquals(_selectedElement, path))
        {
            RefreshSelectedDrawables();
        }

        var drawable = _selectedDrawable ?? _selectedDrawables.FirstOrDefault(candidate => ReferenceEquals(candidate.Element, path));
        if (drawable is null)
        {
            return;
        }

        _pathService.CurrentSegmentTool = PathService.SegmentTool.Line;
        _pathService.Start(path, drawable);
        RefreshOverlay();
    }

    private Shim.SKPoint GetSnappedPicturePoint(Shim.SKPoint picturePoint)
    {
        if (!_isSnapEnabled)
        {
            return picturePoint;
        }

        return new Shim.SKPoint(
            _selectionService.Snap(picturePoint.X),
            _selectionService.Snap(picturePoint.Y));
    }

    private void SyncPathEditingSelection()
    {
        if (_selectedElements.Count == 1 && _toolService.CurrentTool == ToolService.Tool.PathLine)
        {
            if (_selectedElement is SvgPath selectedPath)
            {
                StartPathEditing(selectedPath);
                return;
            }

            if (!_isPromotingVectorSelection
                && _selectedElement is SvgVisualElement selectedElement
                && TrySelectEditableVectorElement(selectedElement, out _))
            {
                return;
            }
        }

        if (_pathService.IsEditing)
        {
            _pathService.Stop();
        }
    }

    private void SyncPathEditingSession()
    {
        if (!_pathService.IsEditing || _pathService.EditPath is not SvgPath editPath || EditorSvg.SkSvg?.Drawable is not DrawableBase root)
        {
            return;
        }

        var activePoint = _pathService.ActivePoint;
        var drawable = FindDrawable(root, editPath);
        if (drawable is null)
        {
            _pathService.Stop();
            return;
        }

        _pathService.Start(editPath, drawable);
        if (activePoint >= 0 && activePoint < _pathService.PathPoints.Count)
        {
            _pathService.ActivePoint = activePoint;
        }
    }

    private bool TryGetPathLocalPoint(SvgPath path, Shim.SKPoint picturePoint, out Shim.SKPoint localPoint)
    {
        if (path.Parent is SvgElement parent)
        {
            return TryMapPointToElementLocal(parent, picturePoint, out localPoint);
        }

        localPoint = picturePoint;
        return true;
    }

    private bool ShouldCloseVectorPath(SvgPath path, Shim.SKPoint picturePoint)
    {
        if (GetPathAnchorCount(path) < 3 || !TryGetFirstPathPoint(path, out var firstPoint))
        {
            return false;
        }

        if (!TryMapPathPointToView(path, firstPoint, out var firstView) || !EditorSvg.TryGetViewPoint(picturePoint, out var currentView))
        {
            return false;
        }

        return Distance(firstView, currentView) <= PathCloseTolerancePixels;
    }

    private bool TryMapPathPointToView(SvgPath path, Shim.SKPoint point, out Point viewPoint)
    {
        viewPoint = default;
        if (!TryMapPathPointToPicture(path, point, out var picturePoint))
        {
            return false;
        }

        return EditorSvg.TryGetViewPoint(picturePoint, out viewPoint);
    }

    private bool TryMapPathPointToPicture(SvgPath path, Shim.SKPoint point, out Shim.SKPoint picturePoint)
    {
        picturePoint = point;
        if (path.Parent is not SvgElement parent)
        {
            return true;
        }

        if (EditorSvg.SkSvg?.Drawable is not DrawableBase root)
        {
            return false;
        }

        var drawable = FindDrawable(root, parent);
        if (drawable is null)
        {
            return false;
        }

        picturePoint = drawable.TotalTransform.MapPoint(point);
        return true;
    }

    private static bool TryGetFirstPathPoint(SvgPath path, out Shim.SKPoint point)
    {
        if (path.PathData.FirstOrDefault() is SvgMoveToSegment move)
        {
            point = new Shim.SKPoint(move.End.X, move.End.Y);
            return true;
        }

        point = default;
        return false;
    }

    private static bool TryGetLastCommittedPathPoint(SvgPath path, out Shim.SKPoint point)
    {
        point = default;
        for (var index = path.PathData.Count - 2; index >= 0; index--)
        {
            switch (path.PathData[index])
            {
                case SvgMoveToSegment move:
                    point = new Shim.SKPoint(move.End.X, move.End.Y);
                    return true;
                case SvgLineSegment line:
                    point = new Shim.SKPoint(line.End.X, line.End.Y);
                    return true;
                case SvgCubicCurveSegment cubic:
                    point = new Shim.SKPoint(cubic.End.X, cubic.End.Y);
                    return true;
                case SvgQuadraticCurveSegment quadratic:
                    point = new Shim.SKPoint(quadratic.End.X, quadratic.End.Y);
                    return true;
                case SvgArcSegment arc:
                    point = new Shim.SKPoint(arc.End.X, arc.End.Y);
                    return true;
            }
        }

        return false;
    }

    private static int GetPathAnchorCount(SvgPath path)
    {
        var count = 0;
        foreach (var segment in path.PathData)
        {
            if (segment is SvgMoveToSegment
                || segment is SvgLineSegment
                || segment is SvgCubicCurveSegment
                || segment is SvgQuadraticCurveSegment
                || segment is SvgArcSegment)
            {
                count++;
            }
        }

        return count;
    }

    private static bool NearlyEqual(Shim.SKPoint left, Shim.SKPoint right, float tolerance = 0.01f)
    {
        return Math.Abs(left.X - right.X) <= tolerance && Math.Abs(left.Y - right.Y) <= tolerance;
    }

    private static double Distance(Point left, Point right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private bool TryStartTransform(Shim.SKPoint picturePoint)
    {
        if (_selectedElements.Count != 1
            || _selectedElement is null
            || _selectedDrawable is null
            || EditorSvg.SkSvg is null
            || IsElementLocked(_selectedElement)
            || !CanResize(_selectedElement))
        {
            return false;
        }

        var scale = GetCanvasScale();
        _selectionService.NormalizeWorldTranslation(_selectedElement);
        var bounds = _selectionService.GetBoundsInfo(_selectedDrawable, EditorSvg.SkSvg, GetCanvasScale);
        var handle = _selectionService.HitHandle(bounds, new SK.SKPoint(picturePoint.X, picturePoint.Y), scale, out var center);
        if (handle < 0)
        {
            return false;
        }

        if (handle == 8)
        {
            _isRotating = true;
            _rotateElement = _selectedElement;
            _rotateCenter = center;
            _rotateCenterLocal = SelectionService.GetLocalCenter(_selectedDrawable);
            _rotateStart = new SK.SKPoint(picturePoint.X, picturePoint.Y);
            _startAngle = _selectionService.GetRotation(_selectedElement);
            return true;
        }

        _isResizing = true;
        _resizeElement = _selectedElement;
        _resizeHandle = handle;
        _resizeStartLocal = new Shim.SKPoint(picturePoint.X, picturePoint.Y);

        _startRect = GetResizeRect(_selectedElement, _selectedDrawable);
        if (_startRect.Width == 0f)
        {
            _startRect.Right = _startRect.Left + 0.01f;
        }

        if (_startRect.Height == 0f)
        {
            _startRect.Bottom = _startRect.Top + 0.01f;
        }

        var resizeMatrix = _selectedDrawable.TotalTransform;
        if (!resizeMatrix.TryInvert(out _resizeInverse))
        {
            _resizeInverse = Shim.SKMatrix.CreateIdentity();
        }

        _resizeStartLocal = _resizeInverse.MapPoint(_resizeStartLocal);
        (_startTransX, _startTransY) = _selectionService.GetTranslation(_resizeElement);
        (_startScaleX, _startScaleY) = _selectionService.GetScale(_resizeElement);
        return true;
    }

    private void SelectElement(SvgVisualElement? element, bool additive = false, bool toggle = false)
    {
        if (element is null)
        {
            if (!additive && !toggle)
            {
                ApplySelection(Array.Empty<SvgVisualElement>(), null);
            }

            return;
        }

        var selection = _selectedElements.ToList();

        if (toggle)
        {
            if (!selection.Remove(element))
            {
                selection.Add(element);
            }

            ApplySelection(selection, selection.LastOrDefault());
            return;
        }

        if (additive)
        {
            if (!selection.Contains(element))
            {
                selection.Add(element);
            }

            ApplySelection(selection, element);
            return;
        }

        ApplySelection(new[] { element }, element);
    }

    private void ApplySelection(IEnumerable<SvgVisualElement> elements, SvgVisualElement? primary)
    {
        _selectedElements.Clear();
        foreach (var element in elements)
        {
            if (!_selectedElements.Contains(element))
            {
                _selectedElements.Add(element);
            }
        }

        _selectedElement = primary;
        if (_selectedElement is not null && !_selectedElements.Contains(_selectedElement))
        {
            _selectedElement = _selectedElements.LastOrDefault();
        }

        if (_selectedElement is null)
        {
            _selectedElement = _selectedElements.LastOrDefault();
        }

        UpdateSelectionScope();
        RefreshSelectedDrawables();
        SyncPathEditingSelection();
        SyncOutlineSelectionState();

        if (_selectedElement is not null)
        {
            _propertiesService.UpdateIdList(_document);
            _propertiesService.LoadProperties(_selectedElement);

            if (_selectedElements.Count > 1)
            {
                SelectedTitle = $"{_selectedElements.Count} layers selected";
                SelectedSubtitle = "Mixed selection";
                SelectedIconGlyph = "◫";
            }
            else
            {
                SelectedTitle = string.IsNullOrWhiteSpace(_selectedElement.ID)
                    ? GetElementTypeLabel(_selectedElement)
                    : _selectedElement.ID!;
                SelectedSubtitle = GetElementTypeLabel(_selectedElement);
                SelectedIconGlyph = EditorObjectNode.GetIconGlyph(_selectedElement);
            }
        }
        else
        {
            _propertiesService.Properties.Clear();
            _propertiesService.FilteredProperties.Clear();
            SelectedTitle = "No element selected";
            SelectedSubtitle = "Pick or draw a vector object on the canvas.";
            SelectedIconGlyph = "•";
        }

        RefreshEffectsInspectorState();
        RefreshFrameInspectorState();
        RefreshAutoLayoutInspectorState();
        UpdateComponentSelectionState();
        RefreshOverlay();
        RefreshComputedState();
    }

    private void RefreshSelectedDrawables()
    {
        _selectedDrawables.Clear();
        _allDrawables.Clear();
        _selectedDrawable = null;

        if (EditorSvg.SkSvg?.Drawable is not DrawableBase root)
        {
            return;
        }

        AppendDrawable(root, _allDrawables);

        foreach (var element in _selectedElements)
        {
            var drawable = FindDrawable(root, element);
            if (drawable is null)
            {
                continue;
            }

            _selectedDrawables.Add(drawable);
            if (ReferenceEquals(element, _selectedElement))
            {
                _selectedDrawable = drawable;
            }
        }

        if (_selectedDrawable is null)
        {
            _selectedDrawable = _selectedDrawables.FirstOrDefault();
        }
    }

    private void RefreshDocumentVisual(bool rebuildOutline, bool reloadProperties)
    {
        if (_document is null)
        {
            return;
        }

        var refreshDerivedDocumentState = rebuildOutline || reloadProperties;
        ConfigureFrameGroups(_document);
        var rendererIsSynchronized = ApplyAutoLayoutPasses(_document);
        if (!rendererIsSynchronized)
        {
            EditorSvg.ReloadFromDocument(_document);
        }

        RefreshSelectedDrawables();
        SyncPathEditingSession();

        if (reloadProperties && _selectedElement is not null)
        {
            _propertiesService.UpdateIdList(_document);
            _propertiesService.LoadProperties(_selectedElement);
            _propertiesService.ApplyFilter(_propertyFilter);
        }

        if (rebuildOutline)
        {
            RebuildObjectNodes();
        }
        else
        {
            UpdateOutlineNodeSelectionState();
        }

        if (_activePage is not null)
        {
            UpdatePageMetadata(_activePage);
        }

        if (refreshDerivedDocumentState)
        {
            RefreshDocumentSwatches();
        }

        RefreshFrameInspectorState();
        RefreshAutoLayoutInspectorState();
        if (rebuildOutline)
        {
            RefreshComponentAssets();
        }

        RefreshOverlay();
        RefreshComputedState();
    }

    private bool ApplyAutoLayoutPasses(SvgDocument document)
    {
        var frames = document.Descendants()
            .OfType<SvgGroup>()
            .Where(IsFrameGroup)
            .ToList();

        if (!frames.Any(frame => _autoLayoutService.ReadSettings(frame).IsEnabled))
        {
            return false;
        }

        for (var pass = 0; pass < 3; pass++)
        {
            if (!EditorSvg.ReloadFromDocument(document))
            {
                return false;
            }

            var changed = false;
            foreach (var frame in frames)
            {
                changed |= _autoLayoutService.ApplyLayout(document, frame, GetDrawableBounds);
            }

            if (!changed)
            {
                return true;
            }
        }

        return false;
    }

    private SK.SKRect? GetDrawableBounds(SvgVisualElement element)
    {
        if (EditorSvg.SkSvg?.Drawable is not DrawableBase root)
        {
            return null;
        }

        var bounds = FindDrawable(root, element)?.TransformedBounds;
        return bounds is null
            ? null
            : new SK.SKRect(bounds.Value.Left, bounds.Value.Top, bounds.Value.Right, bounds.Value.Bottom);
    }

    private void RefreshOverlay()
    {
        EditorOverlay.AllDrawables = _allDrawables;
        EditorOverlay.SelectedDrawables = _pathService.IsEditing
            ? Array.Empty<DrawableBase>()
            : _selectedDrawables;
        EditorOverlay.IsPathEditing = _pathService.IsEditing;
        EditorOverlay.EditPath = _pathService.EditPath;
        EditorOverlay.EditPathDrawable = _pathService.EditDrawable;
        EditorOverlay.PathPoints = _pathService.PathPoints;
        EditorOverlay.PathMatrix = _pathService.PathMatrix;
        EditorOverlay.ActivePathPoint = _pathService.ActivePoint;
        EditorOverlay.ShowSelectionAnnotations = _isDragging || _isResizing || _isRotating || _isCreating;
        EditorOverlay.Marquee = _isMarqueeSelecting ? NormalizeRect(_marqueeStartView, _marqueeCurrentView) : null;
        EditorOverlay.Invalidate();
    }

    private void RefreshComputedState()
    {
        if (_activePage is not null)
        {
            UpdatePageMetadata(_activePage);
        }

        RefreshCommentPositions();
        RefreshCommentsSummary();

        ViewportLabel = $"{EditorSvg.Zoom * 100.0:F0}%";
        CurrentToolLabel = GetToolLabel(_toolService.CurrentTool);
        SelectionSummary = _selectedElements.Count switch
        {
            0 => "No selection",
            1 => $"{GetElementTypeLabel(_selectedElement!)} selected",
            _ => $"{_selectedElements.Count} selected"
        };

        RefreshDevModeState();

        CanvasStatus = _isCommentsInspector
            ? _selectedCommentThread is not null
                ? $"{_selectedCommentThread.ThreadLabel} • {_selectedCommentThread.TargetLabel}"
                : CommentsSummary
            : _isDevInspector
                ? $"{SelectedDevCodeSnippetTitle} • {DevMeasurementSummary}"
            : _selectedElements.Count switch
            {
                0 => $"{PageTitle} • {PageSubtitle}",
                1 => $"{SelectedTitle} • {CanvasLabel}",
                _ => $"{_selectedElements.Count} layers selected • {CanvasLabel}"
            };

        RefreshQuickInspector();
        RebuildRulers();
    }

    private void RefreshQuickInspector()
    {
        var selectionBounds = GetSelectionBounds();
        if (selectionBounds is null)
        {
            QuickX = "-";
            QuickY = "-";
            QuickWidth = "-";
            QuickHeight = "-";
            QuickRotation = "-";
            QuickOpacity = "-";
            QuickCornerRadius = "-";
            QuickFill = "No fill";
            QuickStroke = "No stroke";
            QuickStrokeWidth = "-";
            _isUpdatingFillState = true;
            _isUpdatingStrokeState = true;
            try
            {
                IsFillColorEditable = false;
                IsFillEnabled = false;
                IsStrokeColorEditable = false;
                IsStrokeEnabled = false;
            }
            finally
            {
                _isUpdatingFillState = false;
                _isUpdatingStrokeState = false;
            }

            RaisePropertyChanged(nameof(CanAlignSelection));
            RaisePropertyChanged(nameof(CanDistributeSelection));
            RaisePropertyChanged(nameof(CanRotateSelection));
            RaisePropertyChanged(nameof(CanBooleanCombineSelection));
            RaisePropertyChanged(nameof(CanFlattenSelectionToPath));
            RaisePropertyChanged(nameof(CanShowVectorOperations));
            RaisePropertyChanged(nameof(CanEditCornerRadius));
            RaisePropertyChanged(nameof(CanEditBlendMode));
            RefreshBlendModeInspectorState();
            RefreshSelectionColorPreview();
            RefreshDevModeState();
            return;
        }

        QuickX = selectionBounds.Value.Left.ToString("0.##", CultureInfo.InvariantCulture);
        QuickY = selectionBounds.Value.Top.ToString("0.##", CultureInfo.InvariantCulture);
        QuickWidth = selectionBounds.Value.Width.ToString("0.##", CultureInfo.InvariantCulture);
        QuickHeight = selectionBounds.Value.Height.ToString("0.##", CultureInfo.InvariantCulture);

        if (_selectedElements.Count == 1 && _selectedElement is not null)
        {
            QuickRotation = _selectionService.GetRotation(_selectedElement).ToString("0.##", CultureInfo.InvariantCulture);
            QuickOpacity = (_selectedElement.Opacity * 100f).ToString("0.##", CultureInfo.InvariantCulture);
            QuickFill = FormatPaintWithLibraryStyle(_selectedElement, EditorPaintTarget.Fill);
            QuickStroke = FormatPaintWithLibraryStyle(_selectedElement, EditorPaintTarget.Stroke);
            QuickStrokeWidth = _selectedElement.StrokeWidth.Value.ToString("0.##", CultureInfo.InvariantCulture);
            QuickCornerRadius = TryGetCornerRadiusTarget(_selectedElement, out var radiusTarget)
                ? GetCornerRadiusValue(radiusTarget).ToString("0.##", CultureInfo.InvariantCulture)
                : "-";
            RefreshFillInspectorState(_selectedElement);
            RefreshStrokeInspectorState(_selectedElement);
        }
        else
        {
            QuickRotation = "Mixed";
            QuickOpacity = "Mixed";
            QuickCornerRadius = "Mixed";
            QuickFill = "Mixed";
            QuickStroke = "Mixed";
            QuickStrokeWidth = "Mixed";
            _isUpdatingFillState = true;
            _isUpdatingStrokeState = true;
            try
            {
                IsFillColorEditable = false;
                IsFillEnabled = false;
                IsStrokeColorEditable = false;
                IsStrokeEnabled = false;
            }
            finally
            {
                _isUpdatingFillState = false;
                _isUpdatingStrokeState = false;
            }
        }

        RaisePropertyChanged(nameof(CanAlignSelection));
        RaisePropertyChanged(nameof(CanDistributeSelection));
        RaisePropertyChanged(nameof(CanRotateSelection));
        RaisePropertyChanged(nameof(CanBooleanCombineSelection));
        RaisePropertyChanged(nameof(CanFlattenSelectionToPath));
        RaisePropertyChanged(nameof(CanShowVectorOperations));
        RaisePropertyChanged(nameof(CanEditCornerRadius));
        RaisePropertyChanged(nameof(CanEditBlendMode));
        RefreshBlendModeInspectorState();
        RefreshSelectionColorPreview();
        RefreshDevModeState();
    }

    private static bool TryGetCornerRadiusTarget(SvgElement? element, out SvgRectangle rectangle)
    {
        switch (element)
        {
            case SvgRectangle rect:
                rectangle = rect;
                return true;
            case SvgGroup group when FrameService.TryGetBackground(group, out var background):
                rectangle = background;
                return true;
            default:
                rectangle = null!;
                return false;
        }
    }

    private static float GetCornerRadiusValue(SvgRectangle rectangle)
    {
        return Math.Max(rectangle.CornerRadiusX.Value, rectangle.CornerRadiusY.Value);
    }

    private static string GetElementTypeLabel(SvgElement element)
    {
        if (ToolService.TryGetSemanticTool(element, out var semanticTool))
        {
            return semanticTool switch
            {
                ToolService.Tool.Slice => "Slice",
                ToolService.Tool.Arrow => "Arrow",
                ToolService.Tool.Star => "Star",
                ToolService.Tool.Brush => "Brush stroke",
                ToolService.Tool.Pencil => "Pencil stroke",
                ToolService.Tool.Freehand => "Freehand stroke",
                _ => SvgEditorToolCatalog.GetLabel(semanticTool)
            };
        }

        return element switch
        {
            SvgPath => "Vector",
            SvgGroup group when FrameService.GetContainerKind(group) != FrameContainerKind.Group => FrameService.GetContainerLabel(FrameService.GetContainerKind(group)),
            SvgGroup => "Group",
            _ => SvgElementInfo.GetElementName(element.GetType())
        };
    }

    private void RefreshFillInspectorState(SvgVisualElement element)
    {
        _isUpdatingFillState = true;
        try
        {
            switch (element.Fill)
            {
                case SvgColourServer colorServer when !ReferenceEquals(colorServer, SvgPaintServer.None)
                                                     && !ReferenceEquals(colorServer, SvgPaintServer.Inherit)
                                                     && !ReferenceEquals(colorServer, SvgPaintServer.NotSet):
                    FillColor = Color.FromArgb(
                        (byte)Math.Clamp((int)Math.Round(element.FillOpacity * 255f), 0, 255),
                        colorServer.Colour.R,
                        colorServer.Colour.G,
                        colorServer.Colour.B);
                    IsFillEnabled = true;
                    IsFillColorEditable = true;
                    break;
                case null:
                case SvgPaintServer paintServer when ReferenceEquals(paintServer, SvgPaintServer.None):
                    IsFillEnabled = false;
                    IsFillColorEditable = true;
                    break;
                default:
                    if (TryResolveEffectivePaintColor(element, EditorPaintTarget.Fill, out var inheritedFillColor))
                    {
                        FillColor = inheritedFillColor;
                    }

                    IsFillEnabled = false;
                    IsFillColorEditable = true;
                    break;
            }
        }
        finally
        {
            _isUpdatingFillState = false;
        }
    }

    private static bool TryResolveEffectivePaintColor(SvgVisualElement element, EditorPaintTarget target, out Color color)
    {
        SvgElement? current = element;
        while (current is SvgVisualElement visual)
        {
            var paint = target == EditorPaintTarget.Stroke ? visual.Stroke : visual.Fill;
            var opacity = target == EditorPaintTarget.Stroke ? visual.StrokeOpacity : visual.FillOpacity;

            if (TryGetExplicitPaintColor(paint, opacity, out color))
            {
                return true;
            }

            if (paint is null
                || ReferenceEquals(paint, SvgPaintServer.Inherit)
                || ReferenceEquals(paint, SvgPaintServer.NotSet))
            {
                current = visual.Parent;
                continue;
            }

            break;
        }

        color = default;
        return false;
    }

    private static bool TryGetExplicitPaintColor(SvgPaintServer? paint, float opacity, out Color color)
    {
        color = default;
        if (paint is not SvgColourServer colorServer
            || ReferenceEquals(paint, SvgPaintServer.None)
            || ReferenceEquals(paint, SvgPaintServer.Inherit)
            || ReferenceEquals(paint, SvgPaintServer.NotSet))
        {
            return false;
        }

        color = Color.FromArgb(
            (byte)Math.Clamp((int)Math.Round(opacity * 255f), 0, 255),
            colorServer.Colour.R,
            colorServer.Colour.G,
            colorServer.Colour.B);
        return true;
    }

    private void RefreshDocumentSwatches()
    {
        if (_document is null)
        {
            DocumentColorSwatches.Clear();
            return;
        }

        var seen = new HashSet<int>();
        var swatches = new List<ColorSwatchItem>();

        foreach (var element in _document.Descendants().OfType<SvgVisualElement>())
        {
            AppendSwatch(swatches, seen, element.Fill, element.FillOpacity);
            AppendSwatch(swatches, seen, element.Stroke, element.StrokeOpacity);
        }

        foreach (var flood in _document.Descendants().OfType<SvgFlood>())
        {
            AppendSwatch(swatches, seen, flood.FloodColor, flood.FloodOpacity);
        }

        foreach (var definition in _libraryCatalog.Values.Where(definition => definition.Item.IsEnabled || definition.Item.IsMissing))
        {
            foreach (var swatch in definition.Swatches)
            {
                var color = swatch.Color;
                var key = HashCode.Combine(color.A, color.R, color.G, color.B);
                if (!seen.Add(key))
                {
                    continue;
                }

                var label = definition.Item.IsCurrentFile
                    ? swatch.Label
                    : $"{definition.Item.Name} · {swatch.Label}";
                swatches.Add(new ColorSwatchItem(color, label));
            }
        }

        DocumentColorSwatches.Clear();
        foreach (var swatch in swatches)
        {
            DocumentColorSwatches.Add(swatch);
        }

        RefreshSelectionColorPreview();
    }

    private void RefreshSelectionColorPreview()
    {
        var items = BuildSelectionColorItems();

        _isUpdatingSelectionColorItems = true;
        try
        {
            SelectionColorItems.Clear();
            foreach (var item in items)
            {
                SelectionColorItems.Add(item);
            }
        }
        finally
        {
            _isUpdatingSelectionColorItems = false;
        }

        SelectionColorSwatches.Clear();
        foreach (var item in items.Take(3))
        {
            SelectionColorSwatches.Add(new ColorSwatchItem(item.Color, target: item.Target, strokeWidth: item.StrokeWidth));
        }

        SelectionColorOverflowLabel = items.Count > 3
            ? $"+{items.Count - 3}"
            : string.Empty;
    }

    private static void AppendSwatch(List<ColorSwatchItem> swatches, HashSet<int> seen, SvgPaintServer? paint, float opacity)
    {
        if (paint is not SvgColourServer colorServer
            || ReferenceEquals(paint, SvgPaintServer.None)
            || ReferenceEquals(paint, SvgPaintServer.Inherit)
            || ReferenceEquals(paint, SvgPaintServer.NotSet))
        {
            return;
        }

        var alpha = (byte)Math.Clamp((int)Math.Round(opacity * 255f), 0, 255);
        var color = Color.FromArgb(alpha, colorServer.Colour.R, colorServer.Colour.G, colorServer.Colour.B);
        var key = HashCode.Combine(color.A, color.R, color.G, color.B);
        if (!seen.Add(key))
        {
            return;
        }

        swatches.Add(new ColorSwatchItem(color));
    }

    private static SvgDocument CreateDocument(float width, float height)
    {
        return new SvgDocument
        {
            Width = new SvgUnit(width),
            Height = new SvgUnit(height),
            ViewBox = new SvgViewBox(0, 0, width, height)
        };
    }

    private static SvgGroup CreateFrameGroup(string id, float x, float y, float width, float height, System.Drawing.Color fillColor)
    {
        var group = new SvgGroup
        {
            ID = id
        };
        FrameService.SetContainerKind(group, FrameContainerKind.Frame);

        var background = FrameService.CreateBackgroundRect(
            $"{id.ToLowerInvariant().Replace(" ", "-", StringComparison.Ordinal)}-bg",
            x,
            y,
            width,
            height,
            FrameContainerKind.Frame);
        background.Fill = new SvgColourServer(fillColor);
        group.Children.Add(background);
        group.Children.Add(new SvgGroup
        {
            ID = $"{id.ToLowerInvariant().Replace(" ", "-", StringComparison.Ordinal)}-content",
            CustomAttributes =
            {
                [AutoLayoutService.FrameContentAttribute] = "true"
            }
        });
        FrameService.SyncMetadata(group);
        return group;
    }

    private static SvgRectangle CreateRect(string id, float x, float y, float width, float height, float radius, System.Drawing.Color fillColor)
    {
        return new SvgRectangle
        {
            ID = id,
            X = new SvgUnit(SvgUnitType.User, x),
            Y = new SvgUnit(SvgUnitType.User, y),
            Width = new SvgUnit(SvgUnitType.User, width),
            Height = new SvgUnit(SvgUnitType.User, height),
            CornerRadiusX = new SvgUnit(SvgUnitType.User, radius),
            CornerRadiusY = new SvgUnit(SvgUnitType.User, radius),
            Fill = new SvgColourServer(fillColor)
        };
    }

    private static SvgEllipse CreateEllipse(string id, float x, float y, float width, float height, System.Drawing.Color fillColor)
    {
        return new SvgEllipse
        {
            ID = id,
            CenterX = new SvgUnit(SvgUnitType.User, x + (width / 2f)),
            CenterY = new SvgUnit(SvgUnitType.User, y + (height / 2f)),
            RadiusX = new SvgUnit(SvgUnitType.User, width / 2f),
            RadiusY = new SvgUnit(SvgUnitType.User, height / 2f),
            Fill = new SvgColourServer(fillColor)
        };
    }

    private void EnsureIds(SvgDocument document)
    {
        EnsureElementId(document, document);
        foreach (var element in document.Descendants().OfType<SvgElement>())
        {
            EnsureElementId(element, document);
        }
    }

    private void ConfigureFrameGroups(SvgDocument document)
    {
        foreach (var group in document.Descendants().OfType<SvgGroup>().ToList())
        {
            if (IsFrameGroup(group))
            {
                _autoLayoutService.EnsureContentGroup(group);
                if (!group.CustomAttributes.ContainsKey(FrameService.FrameKindAttribute))
                {
                    FrameService.SetContainerKind(group, FrameContainerKind.Frame);
                }
                SyncFrameMetadata(group);
                var settings = _autoLayoutService.ReadSettings(group);
                _autoLayoutService.UpdateClipPath(
                    document,
                    group,
                    FrameService.GetContainerKind(group) == FrameContainerKind.Frame && settings.IsEnabled && settings.ClipContent);
                continue;
            }

            var background = group.Children
                .OfType<SvgRectangle>()
                .FirstOrDefault(static rect =>
                    string.Equals(rect.ID, "frame", StringComparison.OrdinalIgnoreCase)
                    || FrameService.IsFrameBackground(rect));
            if (background is null)
            {
                continue;
            }

            FrameService.SetContainerKind(group, FrameContainerKind.Frame);
            background.CustomAttributes[FrameService.FrameBackgroundAttribute] = "true";
            _autoLayoutService.EnsureContentGroup(group);
            SyncFrameMetadata(group);
            var inheritedSettings = _autoLayoutService.ReadSettings(group);
            _autoLayoutService.UpdateClipPath(document, group, inheritedSettings.IsEnabled && inheritedSettings.ClipContent);
        }
    }

    private static IEnumerable<SvgGroup> GetFrameGroups(SvgDocument document)
    {
        return document.Descendants().OfType<SvgGroup>().Where(IsFrameGroup);
    }

    private SvgGroup? GetActiveFrame()
    {
        if (_selectedElement is SvgGroup selectedGroup && IsFrameGroup(selectedGroup))
        {
            return selectedGroup;
        }

        if (_selectedElement is not null)
        {
            foreach (var parent in _selectedElement.Parents.OfType<SvgGroup>())
            {
                if (IsFrameGroup(parent))
                {
                    return parent;
                }
            }
        }

        return _document is null ? null : GetFrameGroups(_document).FirstOrDefault();
    }

    private void UpdatePageMetadata(EditorPageState state)
    {
        var containers = GetFrameGroups(state.Document).ToList();
        var frameCount = containers.Count(group => FrameService.GetContainerKind(group) == FrameContainerKind.Frame);
        var sectionCount = containers.Count(group => FrameService.GetContainerKind(group) == FrameContainerKind.Section);
        state.Page.Subtitle = frameCount > 0 && sectionCount == 0
            ? (frameCount == 1 ? "1 frame" : $"{frameCount} frames")
            : sectionCount > 0 && frameCount == 0
                ? (sectionCount == 1 ? "1 section" : $"{sectionCount} sections")
                : containers.Count == 1
                    ? "1 container"
                    : $"{containers.Count} containers";

        if (!ReferenceEquals(_activePage, state))
        {
            return;
        }

        PageTitle = state.Page.Title;
        PageSubtitle = state.Page.Subtitle;
        CanvasLabel = GetCanvasLabel();
    }

    private string GetCanvasLabel()
    {
        var activeFrame = GetActiveFrame();
        if (activeFrame is not null)
        {
            return string.IsNullOrWhiteSpace(activeFrame.ID) ? "Frame" : activeFrame.ID!;
        }

        return _activePage?.Page.Title ?? "Canvas";
    }

    private void CreateFrameAtViewportCenter()
    {
        if (_document is null)
        {
            return;
        }

        var left = 120f;
        var top = 96f;
        var width = 960f;
        var height = 640f;

        if (TryGetVisiblePictureBounds(out var visibleLeft, out var visibleTop, out var visibleRight, out var visibleBottom))
        {
            width = (float)Math.Clamp((visibleRight - visibleLeft) * 0.55, 420.0, 1280.0);
            height = (float)Math.Clamp((visibleBottom - visibleTop) * 0.65, 320.0, 900.0);
            left = (float)((visibleLeft + visibleRight - width) / 2.0);
            top = (float)((visibleTop + visibleBottom - height) / 2.0);
        }

        var frame = CreateFrameGroup($"Frame {_generatedId + 1}", left, top, width, height, System.Drawing.Color.White);
        EnsureElementTreeIds(frame);
        _document.Children.Add(frame);
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection([frame], frame);
    }

    private void DuplicateActiveFrame()
    {
        if (_document is null || GetActiveFrame() is not SvgGroup frame || frame.Parent is not SvgElement parent)
        {
            return;
        }

        var clone = (SvgGroup)frame.DeepCopy();
        EnsureIds(_document);
        EnsureElementTreeIds(clone);
        clone.CustomAttributes.Remove(AutoLayoutService.ClipPathIdAttribute);
        var index = parent.Children.IndexOf(frame);
        parent.Children.Insert(index + 1, clone);

        var (tx, ty) = _selectionService.GetTranslation(clone);
        _selectionService.SetTranslation(clone, tx + 40f, ty + 40f);

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection([clone], clone);
    }

    private void InitializeLibraries()
    {
        RegisterLibrary(CreateCurrentFileLibraryDefinition());
        RegisterLibrary(CreateIosLibraryDefinition());
        RegisterLibrary(CreateMaterialLibraryDefinition());
        RegisterLibrary(CreateSimpleLibraryDefinition());
        EnsureCurrentFileStyleCatalog();

        if (_libraryCatalog.TryGetValue("material-3", out var material))
        {
            material.Item.IsEnabled = true;
        }

        if (_libraryCatalog.TryGetValue("simple-design-system", out var simple))
        {
            simple.Item.IsEnabled = true;
        }

        SyncLibrariesAcrossPages();
        RefreshLibrariesState();
    }

    private void RegisterLibrary(SampleLibraryDefinition definition)
    {
        _libraryCatalog[definition.Item.Id] = definition;
        Libraries.Add(definition.Item);
    }

    private void RefreshLibrariesState()
    {
        UpdateLibraryMissingStates();
        UpdateCurrentFileLibraryOverview();
        RefreshLibraryPaintStyles();
        RefreshStyleCatalogs();
        LibrariesSummary = BuildLibrariesSummary();

        if (_document is not null)
        {
            RefreshDocumentSwatches();
            RefreshComponentAssets();
        }
    }

    private void SyncLibrariesAcrossPages()
    {
        foreach (var state in _pageStates)
        {
            SyncLibrariesToDocument(state.Document);
        }
    }

    private void SyncLibrariesToDocument(SvgDocument document)
    {
        foreach (var definition in _libraryCatalog.Values.Where(static item => !item.Item.IsCurrentFile))
        {
            if (definition.Item.IsEnabled)
            {
                UpsertLibrarySymbols(document, definition);
            }
            else if (!definition.Item.IsMissing)
            {
                RemoveLibrarySymbols(document, definition.Item.Id);
            }
        }

        EnsureIds(document);
    }

    private void UpsertLibrarySymbols(SvgDocument document, SampleLibraryDefinition definition)
    {
        RemoveLibrarySymbols(document, definition.Item.Id);

        var defs = EnsureDefinitions(document);
        foreach (var asset in definition.Assets)
        {
            var symbol = asset.Symbol;
            var clone = (SvgSymbol)symbol.DeepCopy();
            var sourceId = string.IsNullOrWhiteSpace(symbol.ID) ? CreateUniqueId("library-symbol") : symbol.ID!;
            clone.ID = BuildImportedLibrarySymbolId(definition.Item.Id, sourceId);
            clone.CustomAttributes[LibraryIdAttribute] = definition.Item.Id;
            clone.CustomAttributes[LibraryNameAttribute] = definition.Item.Name;
            clone.CustomAttributes[LibraryPublisherAttribute] = definition.Item.Publisher;
            clone.CustomAttributes[LibraryVersionAttribute] = definition.Item.InstalledVersion.ToString(CultureInfo.InvariantCulture);
            clone.CustomAttributes[LibrarySourceSymbolAttribute] = sourceId;
            clone.CustomAttributes[LibraryManagedAttribute] = "true";
            defs.Children.Add(clone);
        }
    }

    private static void RemoveLibrarySymbols(SvgDocument document, string libraryId)
    {
        var defs = document.Children.OfType<SvgDefinitionList>().FirstOrDefault();
        if (defs is null)
        {
            return;
        }

        var toRemove = defs.Children
            .OfType<SvgSymbol>()
            .Where(symbol => symbol.CustomAttributes.TryGetValue(LibraryIdAttribute, out var value)
                && string.Equals(value, libraryId, StringComparison.Ordinal))
            .Cast<SvgElement>()
            .ToList();

        foreach (var symbol in toRemove)
        {
            defs.Children.Remove(symbol);
        }
    }

    private void UpdateLibraryMissingStates()
    {
        foreach (var definition in _libraryCatalog.Values.Where(static item => !item.Item.IsCurrentFile))
        {
            definition.Item.IsMissing = !definition.Item.IsEnabled && IsLibraryUsedAnywhere(definition.Item.Id);
        }
    }

    private bool IsLibraryUsedAnywhere(string libraryId)
    {
        foreach (var state in _pageStates)
        {
            if (state.Document.Descendants().OfType<SvgUse>().Any(use =>
                    ResolveLibraryId(ResolveComponentSymbol(use)) is { } importedLibraryId
                    && string.Equals(importedLibraryId, libraryId, StringComparison.Ordinal))
                || state.Document.Descendants().OfType<SvgVisualElement>().Any(element =>
                    ElementUsesLibraryPaintStyle(element, libraryId)
                    || ElementUsesLibraryTextStyle(element, libraryId)
                    || ElementUsesLibraryEffectStyle(element, libraryId))
                || PageUsesLibraryLayoutGuideStyle(state, libraryId))
            {
                return true;
            }
        }

        return false;
    }

    private async Task ShowLibrariesManagerAsync()
    {
        if (XamlRoot is null)
        {
            CanvasStatus = "Libraries manager requires an active XAML root.";
            return;
        }

        var manager = new FigmaLibrariesManager
        {
            Libraries = Libraries,
            Components = ComponentAssets,
            DocumentTitle = DocumentTitle,
            MissingLibraryCount = Libraries.Count(static item => item.IsMissing),
            Width = Math.Clamp(XamlRoot.Size.Width - 72.0, 720.0, 1180.0),
            Height = Math.Clamp(XamlRoot.Size.Height - 88.0, 560.0, 820.0)
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Content = manager
        };

        manager.CloseRequested += (_, _) => dialog.Hide();
        manager.CommandRequested += async (_, args) =>
        {
            switch (args.Command)
            {
                case EditorLibraryCommand.PublishCurrentFile:
                    PublishCurrentFileLibrary();
                    break;
                case EditorLibraryCommand.ToggleLibrary:
                    if (args.Library is not null)
                    {
                        ToggleLibraryConnection(args.Library);
                    }
                    break;
                case EditorLibraryCommand.UpdateLibrary:
                    if (args.Library is not null)
                    {
                        ApplyLibraryUpdate(args.Library);
                    }
                    break;
                case EditorLibraryCommand.ViewMissingLibraries:
                    await ShowMissingLibrariesAsync();
                    break;
            }

            manager.MissingLibraryCount = Libraries.Count(static item => item.IsMissing);
            manager.RefreshView();
        };

        await dialog.ShowAsync();
    }

    private void ToggleLibraryConnection(EditorLibraryItem library)
    {
        if (library.IsCurrentFile)
        {
            PublishCurrentFileLibrary();
            return;
        }

        library.IsEnabled = !library.IsEnabled;
        if (library.IsEnabled && library.InstalledVersion < library.AvailableVersion)
        {
            library.InstalledVersion = library.AvailableVersion;
            library.HasUpdate = false;
        }

        SyncLibrariesAcrossPages();
        if (library.IsEnabled)
        {
            ReapplyLibraryPaintStylesAcrossPages(library.Id);
        }
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        RefreshLibrariesState();

        CanvasStatus = library.IsEnabled
            ? $"Connected {library.Name} to this file."
            : library.IsMissing
                ? $"{library.Name} was disconnected, but existing instances still reference it."
                : $"Removed {library.Name} from this file.";
    }

    private void ApplyLibraryUpdate(EditorLibraryItem library)
    {
        if (library.IsCurrentFile)
        {
            PublishCurrentFileLibrary();
            return;
        }

        library.InstalledVersion = library.AvailableVersion;
        library.HasUpdate = false;
        library.IsEnabled = true;

        SyncLibrariesAcrossPages();
        ReapplyLibraryPaintStylesAcrossPages(library.Id);
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        RefreshLibrariesState();

        CanvasStatus = $"Updated {library.Name} to version {library.AvailableVersion}.";
    }

    private void ApplyAvailableLibraryUpdates()
    {
        var updates = Libraries.Where(static item => !item.IsCurrentFile && item.HasUpdate).ToList();
        foreach (var library in updates)
        {
            library.InstalledVersion = library.AvailableVersion;
            library.HasUpdate = false;
            library.IsEnabled = true;
        }

        SyncLibrariesAcrossPages();
        foreach (var library in updates)
        {
            ReapplyLibraryPaintStylesAcrossPages(library.Id);
        }
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        RefreshLibrariesState();
        CanvasStatus = updates.Count == 0
            ? "All connected libraries are already up to date."
            : $"Updated {updates.Count} connected libraries.";
    }

    private void PublishCurrentFileLibrary()
    {
        if (!_libraryCatalog.TryGetValue("current-file", out var definition))
        {
            return;
        }

        definition.Assets.Clear();
        definition.Assets.AddRange(BuildPublishedFileAssets());
        MergePublishedPaintStyles(definition);
        MergePublishedTextStyles(definition);
        MergePublishedEffectStyles(definition);
        MergePublishedLayoutGuideStyles(definition);
        EnsureDefaultCurrentFileStyles(definition);

        var library = definition.Item;
        var nextVersion = library.IsPublished ? library.AvailableVersion + 1 : 1;
        library.IsCurrentFile = true;
        library.IsPublished = true;
        library.AvailableVersion = nextVersion;
        library.InstalledVersion = library.AvailableVersion;
        library.ComponentCount = definition.Assets.Count;
        library.ColorCount = definition.Swatches.Count;
        library.Description = $"Published from {DocumentTitle}. Enable it from other files or use it as the source for reusable assets and styles.";
        library.PreviewLabel = DocumentTitle;

        ReapplyLibraryPaintStylesAcrossPages("current-file");
        ReapplyLibraryTextStylesAcrossPages("current-file");
        ReapplyLibraryEffectStylesAcrossPages("current-file");
        ReapplyLayoutGuideStylesAcrossPages("current-file");
        RefreshLibrariesState();
        CanvasStatus = library.ComponentCount == 0
            ? $"Published {DocumentTitle} as a library shell with no reusable components yet."
            : $"Published {DocumentTitle} as library version {library.AvailableVersion}.";
    }

    private List<SampleLibraryAssetDefinition> BuildPublishedFileAssets()
    {
        var result = new List<SampleLibraryAssetDefinition>();
        var usedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var state in _pageStates)
        {
            var defs = state.Document.Children.OfType<SvgDefinitionList>().FirstOrDefault();
            if (defs is null)
            {
                continue;
            }

            foreach (var symbol in defs.Children.OfType<SvgSymbol>())
            {
                if (ResolveLibraryId(symbol) is not null)
                {
                    continue;
                }

                var clone = (SvgSymbol)symbol.DeepCopy();
                var baseId = string.IsNullOrWhiteSpace(symbol.ID) ? NormalizeToken(state.Page.Title) : symbol.ID!;
                var uniqueId = baseId;
                var index = 2;
                while (!usedIds.Add(uniqueId))
                {
                    uniqueId = $"{baseId}-{index++}";
                }

                clone.ID = uniqueId;
                result.Add(new SampleLibraryAssetDefinition(
                    clone,
                    GetComponentDisplayName(clone),
                    "Published",
                    $"{state.Page.Title} published"));
            }
        }

        return result;
    }

    private List<ColorSwatchItem> BuildPublishedFileSwatches()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var swatches = new List<ColorSwatchItem>();

        foreach (var state in _pageStates)
        {
            foreach (var element in state.Document.Descendants().OfType<SvgVisualElement>())
            {
                AppendPublishedPaintStyle(
                    swatches,
                    seen,
                    element.Fill,
                    element.FillOpacity,
                    EditorPaintTarget.Fill,
                    strokeWidth: 1.0);
                AppendPublishedPaintStyle(
                    swatches,
                    seen,
                    element.Stroke,
                    element.StrokeOpacity,
                    EditorPaintTarget.Stroke,
                    element.StrokeWidth.Value);
            }

            foreach (var flood in state.Document.Descendants().OfType<SvgFlood>())
            {
                if (flood.FloodColor is SvgColourServer floodColor)
                {
                    var color = Color.FromArgb(
                        (byte)Math.Clamp((int)Math.Round(flood.FloodOpacity * 255f), 0, 255),
                        floodColor.Colour.R,
                        floodColor.Colour.G,
                        floodColor.Colour.B);
                    var style = CreateLibraryPaintStyle(
                        "current-file",
                        DocumentTitle,
                        BuildPaintStyleLabel(color, EditorPaintTarget.Fill, 1.0),
                        color,
                        EditorPaintTarget.Fill,
                        "Fill styles",
                        "published flood fill style");
                    if (seen.Add(style.StyleId))
                    {
                        swatches.Add(style);
                    }
                }
            }
        }

        return swatches;
    }

    private string BuildLibrariesSummary()
    {
        var connected = Libraries.Count(static item => !item.IsCurrentFile && item.IsEnabled);
        var updates = Libraries.Count(static item => !item.IsCurrentFile && item.HasUpdate);
        var missing = Libraries.Count(static item => item.IsMissing);
        var parts = new List<string>
        {
            connected == 1 ? "1 connected" : $"{connected} connected"
        };

        if (updates > 0)
        {
            parts.Add(updates == 1 ? "1 update" : $"{updates} updates");
        }

        if (missing > 0)
        {
            parts.Add(missing == 1 ? "1 missing" : $"{missing} missing");
        }

        return string.Join(" · ", parts);
    }

    private void UpdateCurrentFileLibraryOverview()
    {
        if (!_libraryCatalog.TryGetValue("current-file", out var definition))
        {
            return;
        }

        definition.Item.Name = DocumentTitle;
        definition.Item.PreviewLabel = DocumentTitle;
        definition.Item.ComponentCount = ComponentAssets.Count(item => string.Equals(item.LibraryId, "current-file", StringComparison.Ordinal));

        if (_document is null)
        {
            definition.Item.ColorCount = 0;
            return;
        }

        definition.Item.ColorCount = definition.Swatches.Count;
    }

    private async Task ShowMissingLibrariesAsync()
    {
        var missing = Libraries
            .Where(static item => item.IsMissing)
            .Select(static item => $"{item.Name} · {item.Publisher}")
            .ToList();

        if (missing.Count == 0)
        {
            CanvasStatus = "There are no missing libraries in this file.";
            return;
        }

        await ShowInfoDialogAsync(
            "Missing libraries",
            string.Join("\n", missing));
    }

    private SampleLibraryDefinition CreateCurrentFileLibraryDefinition()
    {
        var item = new EditorLibraryItem("current-file", DocumentTitle, "Local workspace", EditorLibraryCategory.ThisFile)
        {
            IsCurrentFile = true,
            Description = "Publish local components and shared styles from this file as a reusable library.",
            PreviewLabel = DocumentTitle,
            PreviewPrimaryColor = Color.FromArgb(255, 13, 153, 255),
            PreviewSecondaryColor = Color.FromArgb(255, 243, 249, 255),
            PreviewAccentColor = Color.FromArgb(255, 17, 24, 39)
        };

        return new SampleLibraryDefinition(item, [], []);
    }

    private SampleLibraryDefinition CreateIosLibraryDefinition()
    {
        var item = new EditorLibraryItem("ios-ipados-26", "iOS and iPadOS 26 UI Kit", "Apple", EditorLibraryCategory.UiKit)
        {
            Description = "System cards, callouts, and button primitives tuned for Apple platform surfaces.",
            PreviewLabel = "iOS and iPadOS 26\nUI Kit",
            PreviewPrimaryColor = Color.FromArgb(255, 31, 111, 235),
            PreviewSecondaryColor = Color.FromArgb(255, 224, 240, 255),
            PreviewAccentColor = Color.FromArgb(255, 0, 188, 212),
            ComponentCount = 8,
            ColorCount = 6
        };

        return new SampleLibraryDefinition(
            item,
            [
                CreateLibraryAsset(CreateLibraryCardSymbol("ios-home-summary", 280f, 168f, System.Drawing.Color.White, System.Drawing.Color.FromArgb(31, 111, 235), "Home Screen"), "Home Screen Summary", "Widgets", "ios home widget card"),
                CreateLibraryAsset(CreateLibraryCardSymbol("ios-lock-summary", 280f, 168f, System.Drawing.Color.FromArgb(245, 248, 252), System.Drawing.Color.FromArgb(0, 188, 212), "Lock Screen"), "Lock Screen Module", "Widgets", "ios lock screen widget"),
                CreateLibraryAsset(CreateLibraryCardSymbol("ios-callout-card", 300f, 188f, System.Drawing.Color.White, System.Drawing.Color.FromArgb(59, 130, 246), "Callout"), "Callout Card", "Widgets", "callout summary sheet"),
                CreateLibraryAsset(CreateLibraryMetricSymbol("ios-weather-tile", 180f, 108f, System.Drawing.Color.White, System.Drawing.Color.FromArgb(17, 24, 39), "68°", "Cupertino"), "Weather Tile", "Widgets", "metric weather tile"),
                CreateLibraryAsset(CreateLibraryButtonSymbol("ios-pill-button", 164f, 52f, System.Drawing.Color.FromArgb(31, 111, 235), System.Drawing.Color.White, "Continue"), "Continue Button", "Controls", "button cta primary"),
                CreateLibraryAsset(CreateLibraryButtonSymbol("ios-ghost-button", 164f, 52f, System.Drawing.Color.FromArgb(232, 244, 255), System.Drawing.Color.FromArgb(31, 111, 235), "Later"), "Secondary Button", "Controls", "button secondary pill"),
                CreateLibraryAsset(CreateLibraryChipSymbol("ios-filter-chip", 132f, 42f, System.Drawing.Color.FromArgb(230, 245, 255), System.Drawing.Color.FromArgb(31, 111, 235), "Selected"), "Selected Chip", "Controls", "chip selected filter"),
                CreateLibraryAsset(CreateLibraryDeviceSymbol("ios-device-frame", 220f, 440f, System.Drawing.Color.FromArgb(245, 248, 252), System.Drawing.Color.FromArgb(184, 198, 216)), "Device Frame", "Templates", "phone mock device")
            ],
            [
                CreateLibraryPaintStyle("ios-ipados-26", "iOS and iPadOS 26 UI Kit", "Primary fill", Color.FromArgb(255, 31, 111, 235), EditorPaintTarget.Fill, "Fill styles", "ios primary fill"),
                CreateLibraryPaintStyle("ios-ipados-26", "iOS and iPadOS 26 UI Kit", "Surface fill", Color.FromArgb(255, 245, 248, 252), EditorPaintTarget.Fill, "Fill styles", "ios surface fill"),
                CreateLibraryPaintStyle("ios-ipados-26", "iOS and iPadOS 26 UI Kit", "Accent fill", Color.FromArgb(255, 0, 188, 212), EditorPaintTarget.Fill, "Fill styles", "ios accent fill"),
                CreateLibraryPaintStyle("ios-ipados-26", "iOS and iPadOS 26 UI Kit", "Ink fill", Color.FromArgb(255, 17, 24, 39), EditorPaintTarget.Fill, "Fill styles", "ios ink fill"),
                CreateLibraryPaintStyle("ios-ipados-26", "iOS and iPadOS 26 UI Kit", "Divider stroke", Color.FromArgb(255, 184, 198, 216), EditorPaintTarget.Stroke, "Stroke styles", "ios divider stroke", 1.5),
                CreateLibraryPaintStyle("ios-ipados-26", "iOS and iPadOS 26 UI Kit", "Selection stroke", Color.FromArgb(255, 31, 111, 235), EditorPaintTarget.Stroke, "Stroke styles", "ios selection stroke", 2.0)
            ]);
    }

    private SampleLibraryDefinition CreateMaterialLibraryDefinition()
    {
        var item = new EditorLibraryItem("material-3", "Material 3 Design Kit", "Material Design", EditorLibraryCategory.UiKit)
        {
            Description = "Buttons, sheets, and chips using a bright Material palette and rounded geometry.",
            PreviewLabel = "Material 3\nDesign Kit",
            PreviewPrimaryColor = Color.FromArgb(255, 124, 58, 237),
            PreviewSecondaryColor = Color.FromArgb(255, 244, 232, 255),
            PreviewAccentColor = Color.FromArgb(255, 236, 72, 153),
            ComponentCount = 8,
            ColorCount = 6,
            IsEnabled = false,
            InstalledVersion = 1,
            AvailableVersion = 2,
            HasUpdate = true
        };

        return new SampleLibraryDefinition(
            item,
            [
                CreateLibraryAsset(CreateLibraryCardSymbol("m3-support-card", 296f, 184f, System.Drawing.Color.FromArgb(250, 244, 255), System.Drawing.Color.FromArgb(124, 58, 237), "Support card"), "Support Card", "Cards", "support card surface"),
                CreateLibraryAsset(CreateLibraryCardSymbol("m3-promo-card", 296f, 184f, System.Drawing.Color.FromArgb(255, 245, 251), System.Drawing.Color.FromArgb(236, 72, 153), "Promo card"), "Promo Card", "Cards", "promo card marketing"),
                CreateLibraryAsset(CreateLibraryCardSymbol("m3-sheet-card", 296f, 184f, System.Drawing.Color.FromArgb(252, 247, 255), System.Drawing.Color.FromArgb(168, 85, 247), "Bottom sheet"), "Bottom Sheet", "Cards", "sheet surface modal"),
                CreateLibraryAsset(CreateLibraryButtonSymbol("m3-button", 172f, 54f, System.Drawing.Color.FromArgb(124, 58, 237), System.Drawing.Color.White, "Primary"), "Primary Button", "Buttons", "filled primary button"),
                CreateLibraryAsset(CreateLibraryButtonSymbol("m3-tonal-button", 172f, 54f, System.Drawing.Color.FromArgb(236, 229, 255), System.Drawing.Color.FromArgb(124, 58, 237), "Tonal"), "Tonal Button", "Buttons", "tonal button"),
                CreateLibraryAsset(CreateLibraryChipSymbol("m3-chip", 118f, 42f, System.Drawing.Color.FromArgb(236, 229, 255), System.Drawing.Color.FromArgb(124, 58, 237), "Enabled"), "Assist Chip", "Chips", "assist chip enabled"),
                CreateLibraryAsset(CreateLibraryChipSymbol("m3-filter-chip", 136f, 42f, System.Drawing.Color.FromArgb(255, 240, 248), System.Drawing.Color.FromArgb(236, 72, 153), "Filter"), "Filter Chip", "Chips", "filter chip"),
                CreateLibraryAsset(CreateLibraryMetricSymbol("m3-stat", 156f, 96f, System.Drawing.Color.White, System.Drawing.Color.FromArgb(54, 44, 92), "89%", "Engagement"), "Engagement Tile", "Widgets", "metric stat widget")
            ],
            [
                CreateLibraryPaintStyle("material-3", "Material 3 Design Kit", "Primary fill", Color.FromArgb(255, 124, 58, 237), EditorPaintTarget.Fill, "Fill styles", "material primary fill"),
                CreateLibraryPaintStyle("material-3", "Material 3 Design Kit", "Accent fill", Color.FromArgb(255, 236, 72, 153), EditorPaintTarget.Fill, "Fill styles", "material accent fill"),
                CreateLibraryPaintStyle("material-3", "Material 3 Design Kit", "Surface fill", Color.FromArgb(255, 250, 244, 255), EditorPaintTarget.Fill, "Fill styles", "material surface fill"),
                CreateLibraryPaintStyle("material-3", "Material 3 Design Kit", "Ink fill", Color.FromArgb(255, 54, 44, 92), EditorPaintTarget.Fill, "Fill styles", "material ink fill"),
                CreateLibraryPaintStyle("material-3", "Material 3 Design Kit", "Outline stroke", Color.FromArgb(255, 124, 58, 237), EditorPaintTarget.Stroke, "Stroke styles", "material outline stroke", 1.0),
                CreateLibraryPaintStyle("material-3", "Material 3 Design Kit", "Strong stroke", Color.FromArgb(255, 236, 72, 153), EditorPaintTarget.Stroke, "Stroke styles", "material strong stroke", 2.0)
            ]);
    }

    private SampleLibraryDefinition CreateSimpleLibraryDefinition()
    {
        var item = new EditorLibraryItem("simple-design-system", "Simple Design System", "Figma Community", EditorLibraryCategory.Team)
        {
            Description = "A compact team system with dashboard cards, CTA buttons, and metric chips.",
            PreviewLabel = "Simple",
            PreviewPrimaryColor = Color.FromArgb(255, 17, 24, 39),
            PreviewSecondaryColor = Color.FromArgb(255, 245, 247, 250),
            PreviewAccentColor = Color.FromArgb(255, 13, 153, 255),
            ComponentCount = 8,
            ColorCount = 6
        };

        return new SampleLibraryDefinition(
            item,
            [
                CreateLibraryAsset(CreateLibraryCardSymbol("simple-hero-card", 320f, 192f, System.Drawing.Color.FromArgb(17, 24, 39), System.Drawing.Color.FromArgb(13, 153, 255), "Hero module"), "Hero Module", "Patterns", "hero card pattern"),
                CreateLibraryAsset(CreateLibraryCardSymbol("simple-feature-card", 300f, 184f, System.Drawing.Color.White, System.Drawing.Color.FromArgb(17, 24, 39), "Feature card"), "Feature Card", "Patterns", "feature card product"),
                CreateLibraryAsset(CreateLibraryCardSymbol("simple-info-card", 280f, 176f, System.Drawing.Color.FromArgb(245, 247, 250), System.Drawing.Color.FromArgb(13, 153, 255), "Info panel"), "Info Panel", "Patterns", "info panel surface"),
                CreateLibraryAsset(CreateLibraryButtonSymbol("simple-action", 156f, 50f, System.Drawing.Color.FromArgb(13, 153, 255), System.Drawing.Color.White, "Get started"), "CTA Button", "Controls", "button cta"),
                CreateLibraryAsset(CreateLibraryButtonSymbol("simple-secondary", 156f, 50f, System.Drawing.Color.FromArgb(232, 244, 255), System.Drawing.Color.FromArgb(13, 153, 255), "Learn more"), "Secondary Button", "Controls", "secondary button"),
                CreateLibraryAsset(CreateLibraryMetricSymbol("simple-metric", 136f, 92f, System.Drawing.Color.White, System.Drawing.Color.FromArgb(17, 24, 39), "42%", "+8 today"), "Metric Tile", "Dashboard", "metric dashboard stat"),
                CreateLibraryAsset(CreateLibraryMetricSymbol("simple-kpi", 136f, 92f, System.Drawing.Color.White, System.Drawing.Color.FromArgb(17, 24, 39), "12", "New leads"), "KPI Tile", "Dashboard", "kpi dashboard stat"),
                CreateLibraryAsset(CreateLibraryChipSymbol("simple-chip", 124f, 40f, System.Drawing.Color.FromArgb(17, 24, 39), System.Drawing.Color.White, "Active"), "Status Chip", "Controls", "chip status")
            ],
            [
                CreateLibraryPaintStyle("simple-design-system", "Simple Design System", "Ink fill", Color.FromArgb(255, 17, 24, 39), EditorPaintTarget.Fill, "Fill styles", "simple ink fill"),
                CreateLibraryPaintStyle("simple-design-system", "Simple Design System", "Blue fill", Color.FromArgb(255, 13, 153, 255), EditorPaintTarget.Fill, "Fill styles", "simple blue fill"),
                CreateLibraryPaintStyle("simple-design-system", "Simple Design System", "Surface fill", Color.FromArgb(255, 245, 247, 250), EditorPaintTarget.Fill, "Fill styles", "simple surface fill"),
                CreateLibraryPaintStyle("simple-design-system", "Simple Design System", "Muted fill", Color.FromArgb(255, 98, 108, 128), EditorPaintTarget.Fill, "Fill styles", "simple muted fill"),
                CreateLibraryPaintStyle("simple-design-system", "Simple Design System", "Accent stroke", Color.FromArgb(255, 13, 153, 255), EditorPaintTarget.Stroke, "Stroke styles", "simple accent stroke", 1.5),
                CreateLibraryPaintStyle("simple-design-system", "Simple Design System", "Outline stroke", Color.FromArgb(255, 17, 24, 39), EditorPaintTarget.Stroke, "Stroke styles", "simple outline stroke", 2.0)
            ]);
    }

    private static SampleLibraryAssetDefinition CreateLibraryAsset(
        SvgSymbol symbol,
        string name,
        string sectionName,
        string searchKeywords = "")
    {
        return new SampleLibraryAssetDefinition(symbol, name, sectionName, searchKeywords);
    }

    private static SvgSymbol CreateLibraryCardSymbol(string id, float width, float height, System.Drawing.Color fill, System.Drawing.Color accent, string title)
    {
        var symbol = CreateSymbolShell(id, width, height);
        symbol.Children.Add(CreateRect($"{id}-bg", 0, 0, width, height, 28f, fill));
        symbol.Children.Add(CreateRect($"{id}-accent", 18f, 18f, width - 36f, 12f, 6f, accent));
        symbol.Children.Add(CreateRect($"{id}-body", 18f, 48f, width - 36f, height - 86f, 18f, System.Drawing.Color.FromArgb(255, 255, 255)));
        symbol.Children.Add(CreateLibraryText($"{id}-title", 24f, 112f, title, 22f, System.Drawing.Color.FromArgb(17, 24, 39), 700));
        symbol.Children.Add(CreateLibraryText($"{id}-meta", 24f, height - 26f, "Reusable component", 13f, System.Drawing.Color.FromArgb(107, 114, 128), 500));
        return symbol;
    }

    private static SvgSymbol CreateLibraryButtonSymbol(string id, float width, float height, System.Drawing.Color fill, System.Drawing.Color textColor, string label)
    {
        var symbol = CreateSymbolShell(id, width, height);
        symbol.Children.Add(CreateRect($"{id}-bg", 0f, 0f, width, height, height / 2f, fill));
        symbol.Children.Add(CreateLibraryText($"{id}-label", width / 2f, (height / 2f) + 6f, label, 18f, textColor, 700, centered: true));
        return symbol;
    }

    private static SvgSymbol CreateLibraryChipSymbol(string id, float width, float height, System.Drawing.Color fill, System.Drawing.Color textColor, string label)
    {
        var symbol = CreateSymbolShell(id, width, height);
        symbol.Children.Add(CreateRect($"{id}-bg", 0f, 0f, width, height, height / 2f, fill));
        symbol.Children.Add(CreateEllipse($"{id}-dot", 14f, 10f, 22f, 22f, textColor));
        symbol.Children.Add(CreateLibraryText($"{id}-label", 48f, 27f, label, 16f, textColor, 600));
        return symbol;
    }

    private static SvgSymbol CreateLibraryMetricSymbol(string id, float width, float height, System.Drawing.Color fill, System.Drawing.Color textColor, string value, string subtitle)
    {
        var symbol = CreateSymbolShell(id, width, height);
        symbol.Children.Add(CreateRect($"{id}-bg", 0, 0, width, height, 24f, fill));
        symbol.Children.Add(CreateLibraryText($"{id}-value", 18f, 42f, value, 28f, textColor, 700));
        symbol.Children.Add(CreateLibraryText($"{id}-subtitle", 18f, 70f, subtitle, 13f, System.Drawing.Color.FromArgb(107, 114, 128), 500));
        return symbol;
    }

    private static SvgSymbol CreateLibraryDeviceSymbol(string id, float width, float height, System.Drawing.Color fill, System.Drawing.Color stroke)
    {
        var symbol = CreateSymbolShell(id, width, height);
        symbol.Children.Add(CreateRect($"{id}-shell", 0f, 0f, width, height, 48f, fill));
        var screen = CreateRect($"{id}-screen", 18f, 24f, width - 36f, height - 48f, 34f, System.Drawing.Color.White);
        screen.Stroke = new SvgColourServer(stroke);
        screen.StrokeWidth = new SvgUnit(1.5f);
        symbol.Children.Add(screen);
        symbol.Children.Add(CreateRect($"{id}-header", 44f, 52f, width - 88f, 14f, 7f, stroke));
        symbol.Children.Add(CreateRect($"{id}-panel", 36f, 98f, width - 72f, 112f, 20f, System.Drawing.Color.FromArgb(237, 244, 255)));
        symbol.Children.Add(CreateRect($"{id}-cta", 58f, height - 110f, width - 116f, 44f, 22f, System.Drawing.Color.FromArgb(31, 111, 235)));
        return symbol;
    }

    private static SvgSymbol CreateSymbolShell(string id, float width, float height)
    {
        return new SvgSymbol
        {
            ID = id,
            ViewBox = new SvgViewBox(0f, 0f, width, height),
            AspectRatio = new SvgAspectRatio(SvgPreserveAspectRatio.none),
            CustomAttributes =
            {
                ["width"] = width.ToString(CultureInfo.InvariantCulture),
                ["height"] = height.ToString(CultureInfo.InvariantCulture)
            }
        };
    }

    private static SvgText CreateLibraryText(
        string id,
        float x,
        float y,
        string text,
        float fontSize,
        System.Drawing.Color fillColor,
        ushort weight,
        bool centered = false)
    {
        return new SvgText
        {
            ID = id,
            X = new SvgUnitCollection { new(SvgUnitType.User, x) },
            Y = new SvgUnitCollection { new(SvgUnitType.User, y) },
            Text = text,
            FontFamily = "Open Sans",
            FontSize = new SvgUnit(fontSize),
            FontWeight = GetFontWeight(weight),
            TextAnchor = centered ? SvgTextAnchor.Middle : SvgTextAnchor.Start,
            Fill = new SvgColourServer(fillColor)
        };
    }

    private static SvgFontWeight GetFontWeight(ushort weight)
    {
        return weight switch
        {
            >= 900 => SvgFontWeight.W900,
            >= 800 => SvgFontWeight.W800,
            >= 700 => SvgFontWeight.W700,
            >= 600 => SvgFontWeight.W600,
            >= 500 => SvgFontWeight.W500,
            >= 400 => SvgFontWeight.W400,
            >= 300 => SvgFontWeight.W300,
            >= 200 => SvgFontWeight.W200,
            _ => SvgFontWeight.W100
        };
    }

    private bool EnsureComponentAssetImported(EditorComponentItem asset, out EditorComponentItem resolvedAsset)
    {
        resolvedAsset = asset;
        if (_document is null)
        {
            return false;
        }

        if (!asset.IsLibraryAsset)
        {
            resolvedAsset = ComponentAssets.FirstOrDefault(item =>
                string.Equals(item.AssetKey, asset.AssetKey, StringComparison.Ordinal)) ?? asset;
            return true;
        }

        if (!_libraryCatalog.TryGetValue(asset.LibraryId, out var definition))
        {
            return false;
        }

        if (!definition.Item.IsEnabled)
        {
            definition.Item.IsEnabled = true;
            if (definition.Item.InstalledVersion < definition.Item.AvailableVersion)
            {
                definition.Item.InstalledVersion = definition.Item.AvailableVersion;
                definition.Item.HasUpdate = false;
            }

            SyncLibrariesAcrossPages();
            RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
            RefreshLibrariesState();
        }
        else if (!DocumentContainsImportedSymbol(_document, asset.DocumentSymbolId))
        {
            SyncLibrariesToDocument(_document);
            RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
            RefreshLibrariesState();
        }

        resolvedAsset = ComponentAssets.FirstOrDefault(item =>
                            string.Equals(item.AssetKey, asset.AssetKey, StringComparison.Ordinal))
                        ?? asset;

        return DocumentContainsImportedSymbol(_document, resolvedAsset.DocumentSymbolId);
    }

    private static bool DocumentContainsImportedSymbol(SvgDocument document, string? symbolId)
    {
        if (string.IsNullOrWhiteSpace(symbolId))
        {
            return false;
        }

        return document.Children
            .OfType<SvgDefinitionList>()
            .SelectMany(defs => defs.Children.OfType<SvgSymbol>())
            .Any(symbol => string.Equals(symbol.ID, symbolId, StringComparison.Ordinal));
    }

    private static string BuildLocalAssetKey(string? symbolId)
    {
        return $"current-file:{NormalizeToken(symbolId ?? "component")}";
    }

    private static string BuildLibraryAssetKey(string libraryId, string sourceId)
    {
        return $"{NormalizeToken(libraryId)}:{NormalizeToken(sourceId)}";
    }

    private static string? GetComponentAssetKey(SvgSymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }

        if (ResolveLibraryId(symbol) is { } libraryId
            && symbol.CustomAttributes.TryGetValue(LibrarySourceSymbolAttribute, out var sourceId)
            && !string.IsNullOrWhiteSpace(sourceId))
        {
            return BuildLibraryAssetKey(libraryId, sourceId);
        }

        return BuildLocalAssetKey(symbol.ID);
    }

    private static string BuildImportedLibrarySymbolId(string libraryId, string sourceId)
    {
        return $"library-{NormalizeToken(libraryId)}-{NormalizeToken(sourceId)}";
    }

    private static string? ResolveLibraryId(SvgSymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }

        return symbol.CustomAttributes.TryGetValue(LibraryIdAttribute, out var value)
            ? value
            : null;
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "item";
        }

        var buffer = new char[value.Length];
        var length = 0;
        foreach (var character in value)
        {
            buffer[length++] = char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : '-';
        }

        return new string(buffer, 0, length).Trim('-');
    }

    private void RefreshComponentAssets()
    {
        if (_document is null)
        {
            ComponentAssets.Clear();
            _selectedComponentAsset = null;
            RaiseComponentStateChanged();
            return;
        }

        var preferredAssetKey = (_selectedElement as SvgUse is { } use ? GetComponentAssetKey(ResolveComponentSymbol(use)) : null)
            ?? _selectedComponentAsset?.AssetKey;

        _symbolService.Load(_document);

        ComponentAssets.Clear();
        EditorComponentItem? selectedAsset = null;
        foreach (var entry in _symbolService.Symbols)
        {
            if (ResolveLibraryId(entry.Symbol) is not null)
            {
                continue;
            }

            var item = new EditorComponentItem(entry);
            item.AssetKey = BuildLocalAssetKey(entry.Symbol.ID);
            item.LibraryId = "current-file";
            item.DocumentSymbolId = entry.Symbol.ID ?? string.Empty;
            item.SectionName = "This file";
            item.SourceName = "This file";
            item.SourceSubtitle = "Local component";
            item.SearchKeywords = "local component current file";
            item.IsUpdateAvailable = false;

            ComponentAssets.Add(item);

            if (!string.IsNullOrWhiteSpace(preferredAssetKey)
                && string.Equals(item.AssetKey, preferredAssetKey, StringComparison.Ordinal))
            {
                selectedAsset = item;
            }
        }

        foreach (var definition in _libraryCatalog.Values.Where(static item => !item.Item.IsCurrentFile))
        {
            foreach (var asset in definition.Assets)
            {
                var sourceSymbolId = asset.Symbol.ID ?? CreateUniqueId("library-symbol");
                var item = new EditorComponentItem(new SymbolEntry(asset.Symbol, asset.Name))
                {
                    AssetKey = BuildLibraryAssetKey(definition.Item.Id, sourceSymbolId),
                    LibraryId = definition.Item.Id,
                    DocumentSymbolId = BuildImportedLibrarySymbolId(definition.Item.Id, sourceSymbolId),
                    SectionName = asset.SectionName,
                    SearchKeywords = asset.SearchKeywords,
                    SourceName = definition.Item.Name,
                    SourceSubtitle = definition.Item.HasUpdate
                        ? "Library component · Update available"
                        : definition.Item.IsMissing
                            ? "Library component · Missing source"
                            : definition.Item.IsEnabled
                                ? "Library component"
                                : "Browse asset · Click to connect",
                    IsUpdateAvailable = definition.Item.HasUpdate
                };

                ComponentAssets.Add(item);

                if (!string.IsNullOrWhiteSpace(preferredAssetKey)
                    && string.Equals(item.AssetKey, preferredAssetKey, StringComparison.Ordinal))
                {
                    selectedAsset = item;
                }
            }
        }

        _selectedComponentAsset = selectedAsset;
        UpdateCurrentFileLibraryOverview();
        UpdateComponentSelectionState();
    }

    private void UpdateComponentSelectionState()
    {
        if (_selectedElement is SvgUse use && ResolveComponentSymbol(use) is { } instanceSymbol)
        {
            var selectedAssetKey = GetComponentAssetKey(instanceSymbol);
            _selectedComponentAsset = ComponentAssets.FirstOrDefault(item =>
                string.Equals(item.AssetKey, selectedAssetKey, StringComparison.Ordinal));
        }
        else if (_selectedComponentAsset is not null)
        {
            var selectedId = _selectedComponentAsset.AssetKey;
            _selectedComponentAsset = ComponentAssets.FirstOrDefault(item =>
                string.Equals(item.AssetKey, selectedId, StringComparison.Ordinal));
        }

        foreach (var asset in ComponentAssets)
        {
            asset.IsSelected = ReferenceEquals(asset, _selectedComponentAsset);
        }

        if (_selectedComponentAsset is not null)
        {
            _toolService.SymbolId = string.IsNullOrWhiteSpace(_selectedComponentAsset.DocumentSymbolId)
                ? _selectedComponentAsset.Symbol.ID
                : _selectedComponentAsset.DocumentSymbolId;
            var (symbolWidth, symbolHeight) = GetComponentSize(_selectedComponentAsset.Symbol);
            _toolService.SymbolWidth = symbolWidth;
            _toolService.SymbolHeight = symbolHeight;
        }
        else
        {
            _toolService.SymbolId = null;
            _toolService.SymbolWidth = 160f;
            _toolService.SymbolHeight = 160f;
        }

        RaiseComponentStateChanged();
    }

    private void SelectComponentAsset(EditorComponentItem? item, bool activateSymbolTool)
    {
        var selected = item is null
            ? null
            : ComponentAssets.FirstOrDefault(asset =>
                string.Equals(asset.AssetKey, item.AssetKey, StringComparison.Ordinal));

        if (selected is not null)
        {
            EnsureComponentAssetImported(selected, out selected);
        }

        _selectedComponentAsset = selected;

        UpdateComponentSelectionState();

        if (activateSymbolTool && _selectedComponentAsset is not null)
        {
            SetTool(ToolService.Tool.Symbol);
        }
    }

    private bool CanCreateComponentSelection()
    {
        return TryGetCreatableComponentSelection(out _, out _, out _, out _);
    }

    private bool TryGetCreatableComponentSelection(
        out List<SvgVisualElement> selection,
        out SvgElement? parent,
        out Shim.SKRect selectionBounds,
        out string? failureMessage)
    {
        selection = [];
        parent = null;
        selectionBounds = default;
        failureMessage = null;

        if (_document is null || _selectedElements.Count == 0)
        {
            failureMessage = "Select one or more layers to create a component.";
            return false;
        }

        selection = _selectedElements
            .Where(element => !_selectedElements.Any(candidate =>
                !ReferenceEquals(candidate, element)
                && element.Parents.OfType<SvgElement>().Any(parentElement => ReferenceEquals(parentElement, candidate))))
            .Distinct()
            .ToList();

        if (selection.Count == 0)
        {
            failureMessage = "Select one or more layers to create a component.";
            return false;
        }

        if (selection.Count == 1 && selection[0] is SvgUse)
        {
            failureMessage = "Select editable SVG layers instead of an instance when creating a component.";
            return false;
        }

        if (selection.Any(IsInsideSymbol))
        {
            failureMessage = "Nested symbol content can't be converted into a new component.";
            return false;
        }

        var parents = new List<SvgElement>();
        foreach (var element in selection)
        {
            if (element.Parent is not SvgElement parentElement)
            {
                continue;
            }

            if (!parents.Any(existing => ReferenceEquals(existing, parentElement)))
            {
                parents.Add(parentElement);
            }
        }

        if (parents.Count != 1)
        {
            failureMessage = "All selected layers need to share the same parent before creating a component.";
            return false;
        }

        parent = parents[0];
        if (parent is SvgDefinitionList || parent is SvgSymbol)
        {
            failureMessage = "Definition nodes can't be turned into reusable page components from the canvas.";
            return false;
        }

        if (!TryGetElementBounds(selection, out selectionBounds))
        {
            failureMessage = "Unable to resolve the selection bounds for the new component.";
            return false;
        }

        return true;
    }

    private void CreateComponentFromSelection()
    {
        if (!TryGetCreatableComponentSelection(out var selection, out var parent, out var selectionBounds, out var failureMessage)
            || _document is null
            || parent is null)
        {
            CanvasStatus = failureMessage ?? "Component creation is unavailable for the current selection.";
            return;
        }

        var orderedSelection = selection
            .OrderBy(element => parent.Children.IndexOf(element))
            .ToList();
        var insertIndex = orderedSelection.Min(element => parent.Children.IndexOf(element));
        var width = Math.Max(selectionBounds.Width, 1f);
        var height = Math.Max(selectionBounds.Height, 1f);
        var symbolId = CreateUniqueId("component");

        var symbol = new SvgSymbol
        {
            ID = symbolId,
            ViewBox = new SvgViewBox(0f, 0f, width, height),
            AspectRatio = new SvgAspectRatio(SvgPreserveAspectRatio.none)
        };
        symbol.CustomAttributes["data-component"] = "true";
        symbol.CustomAttributes["width"] = width.ToString(CultureInfo.InvariantCulture);
        symbol.CustomAttributes["height"] = height.ToString(CultureInfo.InvariantCulture);

        var wrapper = new SvgGroup
        {
            Transforms = new SvgTransformCollection
            {
                new SvgMatrix(new List<float> { 1f, 0f, 0f, 1f, -selectionBounds.Left, -selectionBounds.Top })
            }
        };

        foreach (var element in orderedSelection)
        {
            wrapper.Children.Add((SvgElement)element.DeepCopy());
        }

        symbol.Children.Add(wrapper);

        foreach (var element in orderedSelection)
        {
            parent.Children.Remove(element);
        }

        _symbolService.AddSymbol(_document, symbol);

        var instance = CreateComponentInstance(
            symbol,
            selectionBounds.Left,
            selectionBounds.Top,
            width,
            height);
        parent.Children.Insert(insertIndex, instance);

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        SelectComponentAsset(ComponentAssets.FirstOrDefault(item =>
            string.Equals(item.AssetKey, BuildLocalAssetKey(symbolId), StringComparison.Ordinal)), activateSymbolTool: false);
        ApplySelection([instance], instance);
        SetSidebarMode(showAssets: true);
        CanvasStatus = $"Created component {GetComponentDisplayName(symbol)} and replaced the selection with an instance.";
    }

    private void InsertComponentInstanceAtViewportCenter()
    {
        if (_document is null || _selectedComponentAsset is null)
        {
            CanvasStatus = "Select a component asset before inserting an instance.";
            return;
        }

        if (!EnsureComponentAssetImported(_selectedComponentAsset, out var selectedAsset))
        {
            CanvasStatus = "The selected component asset isn't available in this file yet.";
            return;
        }

        var center = GetDefaultInsertPoint();
        var parent = GetCreationParent(center);
        if (!TryMapPointToElementLocal(parent, center, out var localCenter))
        {
            localCenter = center;
        }

        var (width, height) = GetComponentSize(selectedAsset.Symbol);
        var instance = CreateComponentInstance(
            selectedAsset.Symbol,
            localCenter.X - (width / 2f),
            localCenter.Y - (height / 2f),
            width,
            height);

        parent.Children.Add(instance);
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection([instance], instance);
        CanvasStatus = $"Inserted a new {selectedAsset.Name} instance.";
    }

    private void SwapSelectedInstance()
    {
        if (_selectedElement is not SvgUse use || _selectedComponentAsset is null)
        {
            CanvasStatus = "Select an instance and a component asset to swap.";
            return;
        }

        if (!EnsureComponentAssetImported(_selectedComponentAsset, out var selectedAsset))
        {
            CanvasStatus = "The selected asset couldn't be prepared for this file.";
            return;
        }

        use.ReferencedElement = new Uri($"#{selectedAsset.DocumentSymbolId}", UriKind.Relative);

        if (!selectedAsset.SymbolEntry.Apply(use) && string.IsNullOrWhiteSpace(selectedAsset.DocumentSymbolId))
        {
            CanvasStatus = "The selected asset couldn't be applied to this instance.";
            return;
        }

        if (use.Width.Value <= 0f || use.Height.Value <= 0f)
        {
            var (width, height) = GetComponentSize(selectedAsset.Symbol);
            use.Width = new SvgUnit(use.Width.Type, width);
            use.Height = new SvgUnit(use.Height.Type, height);
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection([use], use);
        CanvasStatus = $"Swapped the instance to {selectedAsset.Name}.";
    }

    private void DetachSelectedInstance()
    {
        if (_selectedElement is not SvgUse use
            || use.Parent is not SvgElement parent
            || ResolveComponentSymbol(use) is not SvgSymbol symbol)
        {
            CanvasStatus = "Select an instance to detach it into editable SVG layers.";
            return;
        }

        var detached = new SvgGroup
        {
            ID = CreateUniqueId("group")
        };

        if (use.Transforms is { Count: > 0 })
        {
            detached.Transforms = (SvgTransformCollection)use.Transforms.Clone();
        }

        var sourceWidth = symbol.ViewBox.Width > 0f ? symbol.ViewBox.Width : Math.Max(GetComponentSize(symbol).Width, 1f);
        var sourceHeight = symbol.ViewBox.Height > 0f ? symbol.ViewBox.Height : Math.Max(GetComponentSize(symbol).Height, 1f);
        var targetWidth = use.Width.Value > 0f ? use.Width.Value : sourceWidth;
        var targetHeight = use.Height.Value > 0f ? use.Height.Value : sourceHeight;

        var viewportGroup = new SvgGroup
        {
            Transforms = new SvgTransformCollection
            {
                new SvgMatrix(new List<float>
                {
                    targetWidth / Math.Max(sourceWidth, 0.0001f),
                    0f,
                    0f,
                    targetHeight / Math.Max(sourceHeight, 0.0001f),
                    use.X.Value,
                    use.Y.Value
                })
            }
        };

        SvgElement contentRoot = viewportGroup;
        if (symbol.Transforms is { Count: > 0 })
        {
            var symbolTransformGroup = new SvgGroup
            {
                Transforms = (SvgTransformCollection)symbol.Transforms.Clone()
            };
            viewportGroup.Children.Add(symbolTransformGroup);
            contentRoot = symbolTransformGroup;
        }

        foreach (var child in symbol.Children.OfType<SvgElement>())
        {
            contentRoot.Children.Add((SvgElement)child.DeepCopy());
        }

        detached.Children.Add(viewportGroup);
        EnsureElementTreeIds(detached);

        var index = parent.Children.IndexOf(use);
        parent.Children.Remove(use);
        parent.Children.Insert(index, detached);

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection([detached], detached);
        CanvasStatus = $"Detached {GetComponentDisplayName(symbol)} into editable SVG layers.";
    }

    protected async void OnMainMenuCommandRequested(object? sender, EditorMainMenuCommandEventArgs e)
    {
        await ExecuteMainMenuCommandAsync(e.Command);
    }

    protected async void OnZoomMenuCommandRequested(object? sender, EditorMainMenuCommandEventArgs e)
    {
        await ExecuteMainMenuCommandAsync(e.Command);
    }

    protected void OnZoomPercentRequested(object? sender, ZoomPercentRequestedEventArgs e)
    {
        ZoomToPercent(e.ZoomPercent);
    }

    private async Task ExecuteMainMenuCommandAsync(EditorMainMenuCommand command)
    {
        switch (command)
        {
            case EditorMainMenuCommand.OpenActionsPalette:
                await ShowActionsPaletteAsync();
                break;
            case EditorMainMenuCommand.OpenSvgFile:
                await OpenSvgFileAsync();
                break;
            case EditorMainMenuCommand.SaveSvgFile:
                await SaveSvgFileAsync();
                break;
            case EditorMainMenuCommand.NewPage:
                await AddBlankPageAsync();
                break;
            case EditorMainMenuCommand.InsertFrame:
                CreateFrameAtViewportCenter();
                break;
            case EditorMainMenuCommand.DuplicateFrame:
                DuplicateActiveFrame();
                break;
            case EditorMainMenuCommand.ResetArtwork:
                await ResetArtworkAsync();
                break;
            case EditorMainMenuCommand.CopyDocumentSvg:
                CopyDocumentSvgToSystemClipboard();
                break;
            case EditorMainMenuCommand.CopySelectionSvg:
                CopySelectionSvgToSystemClipboard();
                break;
            case EditorMainMenuCommand.CopyDevSvgSnippet:
            case EditorMainMenuCommand.CopyDevCssSnippet:
            case EditorMainMenuCommand.CopyDevXamlSnippet:
            case EditorMainMenuCommand.CopyDevCSharpSnippet:
                CopyDevSnippetToClipboard(command);
                break;
            case EditorMainMenuCommand.CopySelection:
                CopySelectionToClipboard();
                break;
            case EditorMainMenuCommand.PasteHere:
                PasteClipboardHere();
                break;
            case EditorMainMenuCommand.PasteReplace:
                PasteClipboardReplace();
                break;
            case EditorMainMenuCommand.DuplicateSelection:
                DuplicateSelection();
                break;
            case EditorMainMenuCommand.DeleteSelection:
                DeleteSelection();
                break;
            case EditorMainMenuCommand.SelectAll:
                SelectAllObjects();
                break;
            case EditorMainMenuCommand.SelectNone:
                ApplySelection(Array.Empty<SvgVisualElement>(), null);
                break;
            case EditorMainMenuCommand.ToggleGrid:
                IsGridVisible = !IsGridVisible;
                ApplyViewportOptions();
                break;
            case EditorMainMenuCommand.ToggleSnap:
                IsSnapEnabled = !IsSnapEnabled;
                ApplyViewportOptions();
                break;
            case EditorMainMenuCommand.ToggleWireframe:
                IsWireframeEnabled = !IsWireframeEnabled;
                ApplyViewportOptions();
                break;
            case EditorMainMenuCommand.ToggleDisableFilters:
                AreFiltersDisabled = !AreFiltersDisabled;
                ApplyViewportOptions();
                break;
            case EditorMainMenuCommand.ZoomIn:
                ZoomAroundCenter(1.12);
                break;
            case EditorMainMenuCommand.ZoomOut:
                ZoomAroundCenter(1.0 / 1.12);
                break;
            case EditorMainMenuCommand.ZoomTo50:
                ZoomToPercent(50.0);
                break;
            case EditorMainMenuCommand.ZoomTo100:
                ZoomTo100Percent();
                break;
            case EditorMainMenuCommand.ZoomTo200:
                ZoomToPercent(200.0);
                break;
            case EditorMainMenuCommand.ZoomToFit:
                OnFitViewClick(this, new RoutedEventArgs());
                break;
            case EditorMainMenuCommand.ZoomToSelection:
                ZoomToSelection();
                break;
            case EditorMainMenuCommand.ToggleRulers:
                AreRulersVisible = !AreRulersVisible;
                ApplyViewportOptions();
                break;
            case EditorMainMenuCommand.ToggleLayersPanel:
                SetSidebarMode(showAssets: false);
                break;
            case EditorMainMenuCommand.ToggleAssetsPanel:
                SetSidebarMode(showAssets: true);
                break;
            case EditorMainMenuCommand.ToggleDesignInspector:
                if (_toolService.CurrentTool == ToolService.Tool.Comment)
                {
                    SetTool(ToolService.Tool.Select);
                }
                SetInspectorMode(showPrototype: false);
                break;
            case EditorMainMenuCommand.TogglePrototypeInspector:
                if (_toolService.CurrentTool == ToolService.Tool.Comment)
                {
                    SetTool(ToolService.Tool.Select);
                }
                SetInspectorMode(showPrototype: true);
                break;
            case EditorMainMenuCommand.ToggleDevInspector:
                if (_toolService.CurrentTool == ToolService.Tool.Comment)
                {
                    SetTool(ToolService.Tool.Select);
                }
                SetDevInspectorActive(!_isDevInspector);
                RefreshComputedState();
                break;
            case EditorMainMenuCommand.ToggleCommentsInspector:
                SetTool(_toolService.CurrentTool == ToolService.Tool.Comment ? ToolService.Tool.Select : ToolService.Tool.Comment);
                break;
            case EditorMainMenuCommand.SelectTool:
                SetTool(ToolService.Tool.Select);
                break;
            case EditorMainMenuCommand.HandTool:
                SetTool(ToolService.Tool.Hand);
                break;
            case EditorMainMenuCommand.ScaleTool:
                SetTool(ToolService.Tool.Scale);
                break;
            case EditorMainMenuCommand.CommentTool:
                SetTool(ToolService.Tool.Comment);
                break;
            case EditorMainMenuCommand.PenTool:
                SetTool(ToolService.Tool.PathLine);
                break;
            case EditorMainMenuCommand.PencilTool:
                SetTool(ToolService.Tool.Pencil);
                break;
            case EditorMainMenuCommand.RectangleTool:
                SetTool(ToolService.Tool.Rect);
                break;
            case EditorMainMenuCommand.EllipseTool:
                SetTool(ToolService.Tool.Ellipse);
                break;
            case EditorMainMenuCommand.LineTool:
                SetTool(ToolService.Tool.Line);
                break;
            case EditorMainMenuCommand.ArrowTool:
                SetTool(ToolService.Tool.Arrow);
                break;
            case EditorMainMenuCommand.TextTool:
                SetTool(ToolService.Tool.Text);
                break;
            case EditorMainMenuCommand.PolygonTool:
                SetTool(ToolService.Tool.Polygon);
                break;
            case EditorMainMenuCommand.StarTool:
                SetTool(ToolService.Tool.Star);
                break;
            case EditorMainMenuCommand.ImageTool:
                SetTool(ToolService.Tool.Image);
                break;
            case EditorMainMenuCommand.BrushTool:
                SetTool(ToolService.Tool.Brush);
                break;
            case EditorMainMenuCommand.FrameTool:
                SetTool(ToolService.Tool.Frame);
                break;
            case EditorMainMenuCommand.SectionTool:
                SetTool(ToolService.Tool.Section);
                break;
            case EditorMainMenuCommand.SliceTool:
                SetTool(ToolService.Tool.Slice);
                break;
            case EditorMainMenuCommand.InstanceTool:
                SetTool(ToolService.Tool.Symbol);
                if (_selectedComponentAsset is null)
                {
                    SetSidebarMode(showAssets: true);
                    CanvasStatus = "Select a component asset, then click on the stage to place an instance.";
                }
                break;
            case EditorMainMenuCommand.GroupSelection:
                GroupSelection();
                break;
            case EditorMainMenuCommand.FrameSelection:
                FrameSelection();
                break;
            case EditorMainMenuCommand.UngroupSelection:
                UngroupSelection();
                break;
            case EditorMainMenuCommand.UseAsMask:
                UseSelectionAsMask();
                break;
            case EditorMainMenuCommand.ToggleVisibility:
                ToggleSelectionVisibility();
                break;
            case EditorMainMenuCommand.ToggleLock:
                ToggleSelectionLock();
                break;
            case EditorMainMenuCommand.BringToFront:
                BringSelectionToFront();
                break;
            case EditorMainMenuCommand.BringForward:
                BringSelectionForward();
                break;
            case EditorMainMenuCommand.SendBackward:
                SendSelectionBackward();
                break;
            case EditorMainMenuCommand.SendToBack:
                SendSelectionToBack();
                break;
            case EditorMainMenuCommand.FlipHorizontal:
                FlipSelectionContext(horizontal: true);
                break;
            case EditorMainMenuCommand.FlipVertical:
                FlipSelectionContext(horizontal: false);
                break;
            case EditorMainMenuCommand.RotateLeft90:
                RotateSelection(-90f);
                break;
            case EditorMainMenuCommand.RotateRight90:
                RotateSelection(90f);
                break;
            case EditorMainMenuCommand.Rotate180:
                RotateSelection(180f);
                break;
            case EditorMainMenuCommand.AlignLeft:
                AlignSelection(AlignService.AlignType.Left);
                break;
            case EditorMainMenuCommand.AlignHorizontalCenters:
                AlignSelection(AlignService.AlignType.HCenter);
                break;
            case EditorMainMenuCommand.AlignRight:
                AlignSelection(AlignService.AlignType.Right);
                break;
            case EditorMainMenuCommand.AlignTop:
                AlignSelection(AlignService.AlignType.Top);
                break;
            case EditorMainMenuCommand.AlignVerticalCenters:
                AlignSelection(AlignService.AlignType.VCenter);
                break;
            case EditorMainMenuCommand.AlignBottom:
                AlignSelection(AlignService.AlignType.Bottom);
                break;
            case EditorMainMenuCommand.DistributeHorizontal:
                DistributeSelection(AlignService.DistributeType.Horizontal);
                break;
            case EditorMainMenuCommand.DistributeVertical:
                DistributeSelection(AlignService.DistributeType.Vertical);
                break;
            case EditorMainMenuCommand.BooleanUnion:
                ApplyBooleanSelection(SK.SKPathOp.Union, "Union", "union");
                break;
            case EditorMainMenuCommand.BooleanSubtract:
                ApplyBooleanSelection(SK.SKPathOp.Difference, "Subtract", "subtract");
                break;
            case EditorMainMenuCommand.BooleanIntersect:
                ApplyBooleanSelection(SK.SKPathOp.Intersect, "Intersect", "intersect");
                break;
            case EditorMainMenuCommand.BooleanExclude:
                ApplyBooleanSelection(SK.SKPathOp.Xor, "Exclude", "exclude");
                break;
            case EditorMainMenuCommand.FlattenSelection:
                FlattenSelection();
                break;
            case EditorMainMenuCommand.OutlineStroke:
                OutlineSelectionStroke();
                break;
            case EditorMainMenuCommand.ManageLibraries:
                await ShowLibrariesManagerAsync();
                break;
            case EditorMainMenuCommand.PublishCurrentFileLibrary:
                PublishCurrentFileLibrary();
                break;
            case EditorMainMenuCommand.UpdateLibraries:
                ApplyAvailableLibraryUpdates();
                break;
            case EditorMainMenuCommand.ShowKeyboardShortcuts:
                await ShowInfoDialogAsync(
                    "Keyboard shortcuts",
                    "Canvas and menus:\n\n"
                    + "Cmd/Ctrl+K Actions palette\n"
                    + "Cmd/Ctrl+O Open SVG\nCmd/Ctrl+S Save SVG\n"
                    + "V Move\nH Hand\nK Scale\nP Pen\nShift+P Pencil\nB Brush\n"
                    + "F Frame\nS Slice\nShift+S Section\nR Rectangle\nL Line\nShift+L Arrow\nO Ellipse\nT Text\nU Instance\n"
                    + "Cmd/Ctrl+C Copy selection\nCmd/Ctrl+V Paste here\n"
                    + "Shift+Cmd/Ctrl+R Paste to replace\nCmd/Ctrl+D Duplicate selection\n"
                    + "Shift+Cmd/Ctrl+G Frame selection\nCmd/Ctrl+G Group selection\n"
                    + "Shift+Cmd/Ctrl+U Union\nShift+Cmd/Ctrl+D Subtract\nShift+Cmd/Ctrl+I Intersect\nShift+Cmd/Ctrl+E Exclude\n"
                    + "Shift+Cmd/Ctrl+O Outline stroke\nCmd/Ctrl+A Select all\n"
                    + "Shift+F10 or Menu key Open selection context menu");
                break;
            case EditorMainMenuCommand.ShowAbout:
                await ShowInfoDialogAsync(
                    "About Uno SVG Studio",
                    "Uno SVG Studio is a sample SVG editor built on Svg.Controls.Skia.Uno, Svg.Editor.Svg, and Svg.Editor.Skia.\n\n"
                    + "The live SVG AST stays as the source of truth while the Uno shell provides editing chrome, hit testing, overlays, and inspectors.");
                break;
        }
    }

    private EditorMainMenuState BuildMainMenuState()
    {
        var selectionRoots = GetSelectionRoots();
        var items = GetSelectedItemsForAlignment();
        var hasSelection = selectionRoots.Count > 0;
        var hasClipboard = _clipboard.Element is not null;
        var hasSharedParent = TryGetSelectionParent(selectionRoots, out _);
        var canUngroup = selectionRoots.Count == 1
            && _selectedElement is SvgGroup group
            && !IsFrameGroup(group)
            && !_autoLayoutService.IsFrameContentGroup(group);
        var canBooleanCombine = CanApplyBooleanPathOperation(selectionRoots);
        var canFlattenToPath = CanFlattenPathSelection(selectionRoots);
        var canOutlineStroke = selectionRoots.Count > 0 && selectionRoots.All(CanOutlineStroke);
        var visibilityText = hasSelection && selectionRoots.All(IsElementVisible) ? "Hide selection" : "Show selection";
        var lockText = hasSelection && selectionRoots.All(IsElementLocked) ? "Unlock selection" : "Lock selection";

        return new EditorMainMenuState
        {
            CanOpenSvgFile = true,
            CanSaveSvgFile = _document is not null,
            CanCreatePage = true,
            CanInsertFrame = _document is not null,
            CanDuplicateFrame = GetActiveFrame() is not null,
            CanResetArtwork = true,
            CanCopyDocumentSvg = _document is not null,
            CanCopySelectionSvg = hasSelection,
            CanCopyDevSvgSnippet = _document is not null,
            CanCopyDevCssSnippet = _document is not null,
            CanCopyDevXamlSnippet = _document is not null,
            CanCopyDevCSharpSnippet = _document is not null,
            CanCopySelection = hasSelection,
            CanPasteHere = hasClipboard,
            CanPasteReplace = hasClipboard && hasSelection && hasSharedParent,
            CanDuplicateSelection = hasSelection,
            CanDeleteSelection = hasSelection,
            CanSelectAll = _document is not null,
            CanSelectNone = _selectedElements.Count > 0,
            IsGridVisible = IsGridVisible,
            IsSnapEnabled = IsSnapEnabled,
            IsWireframeEnabled = IsWireframeEnabled,
            AreFiltersDisabled = AreFiltersDisabled,
            IsRulersVisible = AreRulersVisible,
            CanZoomToSelection = GetSelectionBounds() is not null,
            IsLayersPanelVisible = !_isAssetsView,
            IsAssetsPanelVisible = _isAssetsView,
            IsDesignInspectorActive = !_isPrototypeInspector && !_isCommentsInspector && !_isDevInspector,
            IsPrototypeInspectorActive = _isPrototypeInspector,
            IsDevInspectorActive = _isDevInspector,
            IsCommentsInspectorActive = _isCommentsInspector,
            CanGroupSelection = selectionRoots.Count > 1 && hasSharedParent,
            CanFrameSelection = hasSelection && hasSharedParent && !selectionRoots.Any(IsInsideSymbol),
            CanUngroupSelection = canUngroup,
            CanUseAsMask = selectionRoots.Count >= 2 && hasSharedParent,
            CanToggleVisibility = hasSelection,
            VisibilityText = visibilityText,
            CanToggleLock = hasSelection,
            LockText = lockText,
            CanBringToFront = hasSelection && hasSharedParent,
            CanBringForward = hasSelection && hasSharedParent,
            CanSendBackward = hasSelection && hasSharedParent,
            CanSendToBack = hasSelection && hasSharedParent,
            CanFlipHorizontal = items.Count > 0,
            CanFlipVertical = items.Count > 0,
            CanRotateSelection = items.Count > 0,
            CanAlignSelection = items.Count >= 2,
            CanDistributeSelection = items.Count >= 3,
            CanBooleanCombineSelection = canBooleanCombine,
            CanFlattenSelection = canFlattenToPath,
            CanOutlineStroke = canOutlineStroke,
            CanManageLibraries = true,
            CanPublishCurrentFileLibrary = true,
            CanUpdateLibraries = Libraries.Any(static item => !item.IsCurrentFile && item.HasUpdate)
        };
    }

    private async Task ShowActionsPaletteAsync()
    {
        if (_isActionsPaletteOpen)
        {
            return;
        }

        if (XamlRoot is null)
        {
            CanvasStatus = "Actions requires an active XAML root.";
            return;
        }

        var palette = new FigmaActionsPalette
        {
            Items = BuildActionPaletteItems(BuildMainMenuState())
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            DefaultButton = ContentDialogButton.None,
            RequestedTheme = ElementTheme.Light,
            Content = palette
        };

        EditorActionPaletteItem? requestedItem = null;

        palette.ActionRequested += (_, args) =>
        {
            requestedItem = args.Item;
            dialog.Hide();
        };
        palette.CloseRequested += (_, _) => dialog.Hide();
        dialog.Opened += (_, _) => palette.FocusSearchBox();

        _isActionsPaletteOpen = true;
        try
        {
            await dialog.ShowAsync();
        }
        finally
        {
            _isActionsPaletteOpen = false;
        }

        if (requestedItem is not null)
        {
            await ExecuteActionPaletteItemAsync(requestedItem);
        }
    }

    private async Task ExecuteActionPaletteItemAsync(EditorActionPaletteItem item)
    {
        switch (item.Kind)
        {
            case EditorActionPaletteItemKind.Command when item.Command is EditorMainMenuCommand command:
                await ExecuteMainMenuCommandAsync(command);
                break;
            case EditorActionPaletteItemKind.ComponentAsset when item.Component is not null:
                SelectComponentAsset(item.Component, activateSymbolTool: true);
                SetSidebarMode(showAssets: true);
                CanvasStatus = $"{item.Component.Name} ready to place. Click on the stage to insert a new instance.";
                break;
            case EditorActionPaletteItemKind.ToggleLibrary when item.Library is not null:
                ToggleLibraryConnection(item.Library);
                break;
            case EditorActionPaletteItemKind.UpdateLibrary when item.Library is not null:
                ApplyLibraryUpdate(item.Library);
                break;
        }
    }

    private List<EditorActionPaletteItem> BuildActionPaletteItems(EditorMainMenuState state)
    {
        var items = new List<EditorActionPaletteItem>();
        var sortOrder = 0;

        void AddCommandAction(
            EditorMainMenuCommand command,
            string title,
            string subtitle,
            string keywords,
            bool isEnabled,
            EditorActionPaletteTab tab = EditorActionPaletteTab.All,
            bool isSuggested = false,
            bool isCommonSetting = false,
            bool isToggle = false,
            bool isChecked = false)
        {
            items.Add(new EditorActionPaletteItem
            {
                Kind = EditorActionPaletteItemKind.Command,
                Command = command,
                Title = title,
                Subtitle = subtitle,
                Keywords = keywords,
                ShortcutText = GetActionShortcutText(command),
                IsEnabled = isEnabled,
                IsSuggested = isSuggested,
                IsCommonSetting = isCommonSetting,
                IsToggle = isToggle,
                IsChecked = isChecked,
                IconKind = GetActionIcon(command),
                Tab = tab,
                SortOrder = sortOrder++
            });
        }

        AddCommandAction(EditorMainMenuCommand.OpenSvgFile, "Open SVG", "File", "open import svg file document", state.CanOpenSvgFile, isSuggested: true);
        AddCommandAction(EditorMainMenuCommand.SaveSvgFile, "Save SVG", "File", "save export svg file document", state.CanSaveSvgFile, isSuggested: state.CanSaveSvgFile);
        AddCommandAction(EditorMainMenuCommand.NewPage, "New page", "File", "page canvas document", state.CanCreatePage);
        AddCommandAction(EditorMainMenuCommand.InsertFrame, "Insert frame", "File", "frame artboard container", state.CanInsertFrame, isSuggested: state.CanInsertFrame);
        AddCommandAction(EditorMainMenuCommand.DuplicateFrame, "Duplicate active frame", "File", "duplicate frame artboard", state.CanDuplicateFrame);
        AddCommandAction(EditorMainMenuCommand.ResetArtwork, "Reset artwork", "File", "reset sample artwork", state.CanResetArtwork);
        AddCommandAction(EditorMainMenuCommand.CopyDocumentSvg, "Copy document SVG", "File", "copy svg document export", state.CanCopyDocumentSvg);
        AddCommandAction(EditorMainMenuCommand.CopySelectionSvg, "Copy selection SVG", "File", "copy selection svg export", state.CanCopySelectionSvg);
        AddCommandAction(EditorMainMenuCommand.CopyDevSvgSnippet, "Copy Dev SVG", "Dev Mode", "copy dev svg snippet export", state.CanCopyDevSvgSnippet, isSuggested: state.IsDevInspectorActive);
        AddCommandAction(EditorMainMenuCommand.CopyDevCssSnippet, "Copy CSS", "Dev Mode", "copy css snippet export", state.CanCopyDevCssSnippet);
        AddCommandAction(EditorMainMenuCommand.CopyDevXamlSnippet, "Copy Uno XAML", "Dev Mode", "copy xaml snippet uno control", state.CanCopyDevXamlSnippet);
        AddCommandAction(EditorMainMenuCommand.CopyDevCSharpSnippet, "Copy C#", "Dev Mode", "copy csharp snippet uno control", state.CanCopyDevCSharpSnippet);

        AddCommandAction(EditorMainMenuCommand.CopySelection, "Copy selection", "Edit", "copy current selection", state.CanCopySelection);
        AddCommandAction(EditorMainMenuCommand.PasteHere, "Paste here", "Edit", "paste clipboard selection", state.CanPasteHere);
        AddCommandAction(EditorMainMenuCommand.PasteReplace, "Paste to replace", "Edit", "paste replace selection", state.CanPasteReplace);
        AddCommandAction(EditorMainMenuCommand.DuplicateSelection, "Duplicate selection", "Edit", "duplicate copy selection", state.CanDuplicateSelection, isSuggested: state.CanDuplicateSelection);
        AddCommandAction(EditorMainMenuCommand.DeleteSelection, "Delete selection", "Edit", "delete remove selection", state.CanDeleteSelection);
        AddCommandAction(EditorMainMenuCommand.SelectAll, "Select all", "Edit", "select all layers objects", state.CanSelectAll, isSuggested: state.CanSelectAll);
        AddCommandAction(EditorMainMenuCommand.SelectNone, "Select none", "Edit", "clear selection", state.CanSelectNone);

        AddCommandAction(EditorMainMenuCommand.ToggleGrid, "Show grid", "Common settings", "grid canvas guides", true, isCommonSetting: true, isToggle: true, isChecked: state.IsGridVisible);
        AddCommandAction(EditorMainMenuCommand.ToggleSnap, "Snap to geometry", "Common settings", "snap geometry points", true, isCommonSetting: true, isToggle: true, isChecked: state.IsSnapEnabled);
        AddCommandAction(EditorMainMenuCommand.ToggleRulers, "Show rulers", "Common settings", "rulers measurement guides", true, isCommonSetting: true, isToggle: true, isChecked: state.IsRulersVisible);
        AddCommandAction(EditorMainMenuCommand.ToggleWireframe, "Wireframe mode", "Common settings", "wireframe vector outlines", true, isCommonSetting: true, isToggle: true, isChecked: state.IsWireframeEnabled);
        AddCommandAction(EditorMainMenuCommand.ToggleDisableFilters, "Flat filters", "Common settings", "disable filters fast preview", true, isCommonSetting: true, isToggle: true, isChecked: state.AreFiltersDisabled);
        AddCommandAction(EditorMainMenuCommand.ToggleLayersPanel, "Layers panel", "Common settings", "show layers sidebar", true, isCommonSetting: true, isToggle: true, isChecked: state.IsLayersPanelVisible);
        AddCommandAction(EditorMainMenuCommand.ToggleAssetsPanel, "Assets panel", "Common settings", "show assets sidebar", true, isCommonSetting: true, isToggle: true, isChecked: state.IsAssetsPanelVisible);
        AddCommandAction(EditorMainMenuCommand.ToggleDesignInspector, "Design inspector", "Common settings", "design inspector properties", true, isCommonSetting: true, isToggle: true, isChecked: state.IsDesignInspectorActive);
        AddCommandAction(EditorMainMenuCommand.TogglePrototypeInspector, "Prototype inspector", "Common settings", "prototype inspector", true, isCommonSetting: true, isToggle: true, isChecked: state.IsPrototypeInspectorActive);
        AddCommandAction(EditorMainMenuCommand.ToggleDevInspector, "Dev mode", "Common settings", "developer inspect code export measurements", true, isCommonSetting: true, isToggle: true, isChecked: state.IsDevInspectorActive);
        AddCommandAction(EditorMainMenuCommand.ToggleCommentsInspector, "Comments", "Common settings", "comments discussion review", true, isCommonSetting: true, isToggle: true, isChecked: state.IsCommentsInspectorActive);
        AddCommandAction(EditorMainMenuCommand.ZoomIn, "Zoom in", "View", "zoom canvas in", true);
        AddCommandAction(EditorMainMenuCommand.ZoomOut, "Zoom out", "View", "zoom canvas out", true);
        AddCommandAction(EditorMainMenuCommand.ZoomTo50, "Zoom to 50%", "View", "zoom to fifty percent", true);
        AddCommandAction(EditorMainMenuCommand.ZoomTo100, "Zoom to 100%", "View", "zoom reset hundred percent", true);
        AddCommandAction(EditorMainMenuCommand.ZoomTo200, "Zoom to 200%", "View", "zoom to two hundred percent", true);
        AddCommandAction(EditorMainMenuCommand.ZoomToFit, "Zoom to fit", "View", "fit artwork to viewport", true);
        AddCommandAction(EditorMainMenuCommand.ZoomToSelection, "Zoom to selection", "View", "fit selection in viewport", state.CanZoomToSelection, isSuggested: state.CanZoomToSelection);

        AddCommandAction(EditorMainMenuCommand.SelectTool, "Move tool", "Insert", "move select tool cursor", true);
        AddCommandAction(EditorMainMenuCommand.HandTool, "Hand tool", "Insert", "hand tool pan canvas", true);
        AddCommandAction(EditorMainMenuCommand.ScaleTool, "Scale tool", "Insert", "scale selection transform", true);
        AddCommandAction(EditorMainMenuCommand.CommentTool, "Comment tool", "Insert", "comment review annotate", true, isSuggested: true);
        AddCommandAction(EditorMainMenuCommand.FrameTool, "Frame tool", "Insert", "frame artboard tool", true);
        AddCommandAction(EditorMainMenuCommand.SectionTool, "Section tool", "Insert", "section region tool", true);
        AddCommandAction(EditorMainMenuCommand.SliceTool, "Slice tool", "Insert", "slice export region tool", true);
        AddCommandAction(EditorMainMenuCommand.PenTool, "Pen tool", "Insert", "pen vector path tool", true);
        AddCommandAction(EditorMainMenuCommand.PencilTool, "Pencil tool", "Insert", "draw freehand pencil", true);
        AddCommandAction(EditorMainMenuCommand.RectangleTool, "Rectangle tool", "Insert", "draw rectangle", true);
        AddCommandAction(EditorMainMenuCommand.LineTool, "Line tool", "Insert", "draw line", true);
        AddCommandAction(EditorMainMenuCommand.ArrowTool, "Arrow tool", "Insert", "draw arrow", true);
        AddCommandAction(EditorMainMenuCommand.EllipseTool, "Ellipse tool", "Insert", "draw ellipse circle", true);
        AddCommandAction(EditorMainMenuCommand.TextTool, "Text tool", "Insert", "insert text", true);
        AddCommandAction(EditorMainMenuCommand.PolygonTool, "Polygon tool", "Insert", "draw polygon", true);
        AddCommandAction(EditorMainMenuCommand.StarTool, "Star tool", "Insert", "draw star", true);
        AddCommandAction(EditorMainMenuCommand.ImageTool, "Image / video tool", "Insert", "place image video", true);
        AddCommandAction(EditorMainMenuCommand.BrushTool, "Brush tool", "Insert", "freehand brush", true);
        AddCommandAction(EditorMainMenuCommand.InstanceTool, "Instance tool", "Insert", "component instance symbol", true);

        AddCommandAction(EditorMainMenuCommand.GroupSelection, "Group selection", "Object", "group layers selection", state.CanGroupSelection, isSuggested: state.CanGroupSelection);
        AddCommandAction(EditorMainMenuCommand.FrameSelection, "Frame selection", "Object", "frame selected layers", state.CanFrameSelection, isSuggested: state.CanFrameSelection);
        AddCommandAction(EditorMainMenuCommand.UngroupSelection, "Ungroup selection", "Object", "ungroup layers", state.CanUngroupSelection);
        AddCommandAction(EditorMainMenuCommand.UseAsMask, "Use as mask", "Object", "mask selected objects", state.CanUseAsMask);
        AddCommandAction(EditorMainMenuCommand.ToggleVisibility, state.VisibilityText, "Object", "show hide selected objects", state.CanToggleVisibility);
        AddCommandAction(EditorMainMenuCommand.ToggleLock, state.LockText, "Object", "lock unlock selected objects", state.CanToggleLock);

        AddCommandAction(EditorMainMenuCommand.BringToFront, "Bring to front", "Arrange", "bring selection to front", state.CanBringToFront);
        AddCommandAction(EditorMainMenuCommand.BringForward, "Bring forward", "Arrange", "move selection forward", state.CanBringForward);
        AddCommandAction(EditorMainMenuCommand.SendBackward, "Send backward", "Arrange", "move selection backward", state.CanSendBackward);
        AddCommandAction(EditorMainMenuCommand.SendToBack, "Send to back", "Arrange", "send selection to back", state.CanSendToBack);
        AddCommandAction(EditorMainMenuCommand.FlipHorizontal, "Flip horizontal", "Arrange", "flip selection horizontally", state.CanFlipHorizontal);
        AddCommandAction(EditorMainMenuCommand.FlipVertical, "Flip vertical", "Arrange", "flip selection vertically", state.CanFlipVertical);
        AddCommandAction(EditorMainMenuCommand.RotateLeft90, "Rotate 90° left", "Arrange", "rotate selection left", state.CanRotateSelection);
        AddCommandAction(EditorMainMenuCommand.RotateRight90, "Rotate 90° right", "Arrange", "rotate selection right", state.CanRotateSelection);
        AddCommandAction(EditorMainMenuCommand.Rotate180, "Rotate 180°", "Arrange", "rotate selection one eighty", state.CanRotateSelection);
        AddCommandAction(EditorMainMenuCommand.AlignLeft, "Align left", "Arrange", "align selection left", state.CanAlignSelection);
        AddCommandAction(EditorMainMenuCommand.AlignHorizontalCenters, "Align horizontal centers", "Arrange", "align horizontal centers", state.CanAlignSelection);
        AddCommandAction(EditorMainMenuCommand.AlignRight, "Align right", "Arrange", "align selection right", state.CanAlignSelection);
        AddCommandAction(EditorMainMenuCommand.AlignTop, "Align top", "Arrange", "align selection top", state.CanAlignSelection);
        AddCommandAction(EditorMainMenuCommand.AlignVerticalCenters, "Align vertical centers", "Arrange", "align vertical centers", state.CanAlignSelection);
        AddCommandAction(EditorMainMenuCommand.AlignBottom, "Align bottom", "Arrange", "align selection bottom", state.CanAlignSelection);
        AddCommandAction(EditorMainMenuCommand.DistributeHorizontal, "Distribute horizontal", "Arrange", "distribute horizontal spacing", state.CanDistributeSelection);
        AddCommandAction(EditorMainMenuCommand.DistributeVertical, "Distribute vertical", "Arrange", "distribute vertical spacing", state.CanDistributeSelection);

        AddCommandAction(EditorMainMenuCommand.BooleanUnion, "Union", "Vector", "combine selection with union", state.CanBooleanCombineSelection);
        AddCommandAction(EditorMainMenuCommand.BooleanSubtract, "Subtract", "Vector", "combine selection with subtract difference", state.CanBooleanCombineSelection);
        AddCommandAction(EditorMainMenuCommand.BooleanIntersect, "Intersect", "Vector", "combine selection with intersect", state.CanBooleanCombineSelection);
        AddCommandAction(EditorMainMenuCommand.BooleanExclude, "Exclude", "Vector", "combine selection with exclude xor", state.CanBooleanCombineSelection);
        AddCommandAction(EditorMainMenuCommand.FlattenSelection, "Flatten selection", "Vector", "convert selection to path", state.CanFlattenSelection);
        AddCommandAction(EditorMainMenuCommand.OutlineStroke, "Outline stroke", "Vector", "convert stroke to outline", state.CanOutlineStroke);

        AddCommandAction(EditorMainMenuCommand.ManageLibraries, "Manage libraries", "Libraries", "manage component libraries", state.CanManageLibraries, tab: EditorActionPaletteTab.Libraries, isSuggested: state.CanManageLibraries);
        AddCommandAction(EditorMainMenuCommand.PublishCurrentFileLibrary, "Publish this file", "Libraries", "publish current file library", state.CanPublishCurrentFileLibrary, tab: EditorActionPaletteTab.Libraries);
        AddCommandAction(EditorMainMenuCommand.UpdateLibraries, "Update connected libraries", "Libraries", "update connected libraries", state.CanUpdateLibraries, tab: EditorActionPaletteTab.Libraries);

        AddCommandAction(EditorMainMenuCommand.ShowKeyboardShortcuts, "Keyboard shortcuts", "Help", "show keyboard shortcuts", true);
        AddCommandAction(EditorMainMenuCommand.ShowAbout, "About Uno SVG Studio", "Help", "about svg studio", true);

        foreach (var asset in ComponentAssets
                     .OrderBy(item => item.SourceName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.SectionName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            items.Add(new EditorActionPaletteItem
            {
                Kind = EditorActionPaletteItemKind.ComponentAsset,
                Component = asset,
                Title = asset.Name,
                Subtitle = $"{asset.SourceName} · {asset.SectionName}",
                Keywords = $"{asset.SearchText} component symbol asset instance",
                IconKind = asset.IsLibraryAsset ? FigmaIconKind.Library : FigmaIconKind.Instance,
                Tab = EditorActionPaletteTab.Assets,
                SortOrder = sortOrder++
            });
        }

        foreach (var library in Libraries
                     .Where(static item => !item.IsCurrentFile)
                     .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            items.Add(new EditorActionPaletteItem
            {
                Kind = EditorActionPaletteItemKind.ToggleLibrary,
                Library = library,
                Title = library.IsEnabled ? $"Remove {library.Name}" : $"Add {library.Name}",
                Subtitle = $"{library.Publisher} · {library.StatusLabel}",
                Keywords = $"{library.SearchText} add remove library",
                IconKind = library.CategoryIconKind,
                Tab = EditorActionPaletteTab.Libraries,
                SortOrder = sortOrder++
            });

            if (library.HasUpdate)
            {
                items.Add(new EditorActionPaletteItem
                {
                    Kind = EditorActionPaletteItemKind.UpdateLibrary,
                    Library = library,
                    Title = $"Update {library.Name}",
                    Subtitle = $"{library.Publisher} · v{library.InstalledVersion} → v{library.AvailableVersion}",
                    Keywords = $"{library.SearchText} update library refresh",
                    IconKind = FigmaIconKind.Refresh,
                    Tab = EditorActionPaletteTab.Libraries,
                    SortOrder = sortOrder++
                });
            }
        }

        return items;
    }

    private static FigmaIconKind GetActionIcon(EditorMainMenuCommand command)
    {
        return command switch
        {
            EditorMainMenuCommand.OpenSvgFile or EditorMainMenuCommand.SaveSvgFile => FigmaIconKind.Page,
            EditorMainMenuCommand.NewPage => FigmaIconKind.Page,
            EditorMainMenuCommand.InsertFrame or EditorMainMenuCommand.DuplicateFrame or EditorMainMenuCommand.FrameTool or EditorMainMenuCommand.FrameSelection => FigmaIconKind.Frame,
            EditorMainMenuCommand.ResetArtwork or EditorMainMenuCommand.UpdateLibraries => FigmaIconKind.Refresh,
            EditorMainMenuCommand.CopyDocumentSvg or EditorMainMenuCommand.CopySelectionSvg or EditorMainMenuCommand.CopyDevSvgSnippet => FigmaIconKind.Page,
            EditorMainMenuCommand.CopyDevCssSnippet or EditorMainMenuCommand.CopyDevXamlSnippet or EditorMainMenuCommand.CopyDevCSharpSnippet => FigmaIconKind.Code,
            EditorMainMenuCommand.CopySelection or EditorMainMenuCommand.PasteHere or EditorMainMenuCommand.PasteReplace or EditorMainMenuCommand.DuplicateSelection or EditorMainMenuCommand.DeleteSelection or EditorMainMenuCommand.SelectAll or EditorMainMenuCommand.SelectNone => FigmaIconKind.Menu,
            EditorMainMenuCommand.ToggleGrid => FigmaIconKind.Frame,
            EditorMainMenuCommand.ToggleSnap => FigmaIconKind.Adjust,
            EditorMainMenuCommand.ToggleRulers => FigmaIconKind.Search,
            EditorMainMenuCommand.ToggleWireframe or EditorMainMenuCommand.OutlineStroke or EditorMainMenuCommand.FlattenSelection => FigmaIconKind.Vector,
            EditorMainMenuCommand.BooleanUnion or EditorMainMenuCommand.BooleanSubtract or EditorMainMenuCommand.BooleanIntersect or EditorMainMenuCommand.BooleanExclude => FigmaIconKind.Boolean,
            EditorMainMenuCommand.ToggleDisableFilters => FigmaIconKind.Droplet,
            EditorMainMenuCommand.ZoomIn or EditorMainMenuCommand.ZoomOut or EditorMainMenuCommand.ZoomTo50 or EditorMainMenuCommand.ZoomTo100 or EditorMainMenuCommand.ZoomTo200 or EditorMainMenuCommand.ZoomToFit or EditorMainMenuCommand.ZoomToSelection => FigmaIconKind.Search,
            EditorMainMenuCommand.ToggleLayersPanel or EditorMainMenuCommand.ToggleAssetsPanel or EditorMainMenuCommand.ToggleDesignInspector or EditorMainMenuCommand.TogglePrototypeInspector => FigmaIconKind.Menu,
            EditorMainMenuCommand.ToggleDevInspector => FigmaIconKind.Code,
            EditorMainMenuCommand.ToggleCommentsInspector or EditorMainMenuCommand.CommentTool => FigmaIconKind.Comment,
            EditorMainMenuCommand.SelectTool => FigmaIconKind.Move,
            EditorMainMenuCommand.HandTool => FigmaIconKind.Hand,
            EditorMainMenuCommand.ScaleTool => FigmaIconKind.Scale,
            EditorMainMenuCommand.PenTool => FigmaIconKind.Pen,
            EditorMainMenuCommand.PencilTool => FigmaIconKind.Pencil,
            EditorMainMenuCommand.RectangleTool => FigmaIconKind.Rectangle,
            EditorMainMenuCommand.EllipseTool => FigmaIconKind.Ellipse,
            EditorMainMenuCommand.LineTool => FigmaIconKind.Line,
            EditorMainMenuCommand.ArrowTool => FigmaIconKind.Arrow,
            EditorMainMenuCommand.TextTool => FigmaIconKind.Text,
            EditorMainMenuCommand.PolygonTool => FigmaIconKind.Polygon,
            EditorMainMenuCommand.StarTool => FigmaIconKind.Star,
            EditorMainMenuCommand.ImageTool => FigmaIconKind.Image,
            EditorMainMenuCommand.BrushTool => FigmaIconKind.Droplet,
            EditorMainMenuCommand.SectionTool => FigmaIconKind.Section,
            EditorMainMenuCommand.SliceTool => FigmaIconKind.Slice,
            EditorMainMenuCommand.InstanceTool => FigmaIconKind.Instance,
            EditorMainMenuCommand.GroupSelection or EditorMainMenuCommand.UngroupSelection => FigmaIconKind.Group,
            EditorMainMenuCommand.UseAsMask => FigmaIconKind.Image,
            EditorMainMenuCommand.ToggleVisibility => FigmaIconKind.Eye,
            EditorMainMenuCommand.ToggleLock => FigmaIconKind.Section,
            EditorMainMenuCommand.BringToFront or EditorMainMenuCommand.BringForward or EditorMainMenuCommand.SendBackward or EditorMainMenuCommand.SendToBack
                or EditorMainMenuCommand.FlipHorizontal or EditorMainMenuCommand.FlipVertical or EditorMainMenuCommand.RotateLeft90 or EditorMainMenuCommand.RotateRight90
                or EditorMainMenuCommand.Rotate180 or EditorMainMenuCommand.AlignLeft or EditorMainMenuCommand.AlignHorizontalCenters or EditorMainMenuCommand.AlignRight
                or EditorMainMenuCommand.AlignTop or EditorMainMenuCommand.AlignVerticalCenters or EditorMainMenuCommand.AlignBottom
                or EditorMainMenuCommand.DistributeHorizontal or EditorMainMenuCommand.DistributeVertical => FigmaIconKind.Adjust,
            EditorMainMenuCommand.ManageLibraries or EditorMainMenuCommand.PublishCurrentFileLibrary => FigmaIconKind.Library,
            EditorMainMenuCommand.ShowKeyboardShortcuts => FigmaIconKind.Actions,
            EditorMainMenuCommand.ShowAbout => FigmaIconKind.Library,
            _ => FigmaIconKind.Search
        };
    }

    private static string GetActionShortcutText(EditorMainMenuCommand command)
    {
        return command switch
        {
            EditorMainMenuCommand.OpenActionsPalette => "⌘K",
            EditorMainMenuCommand.OpenSvgFile => "⌘O",
            EditorMainMenuCommand.SaveSvgFile => "⌘S",
            EditorMainMenuCommand.NewPage => "⌘N",
            EditorMainMenuCommand.ToggleDevInspector => "⇧D",
            EditorMainMenuCommand.CopySelection => "⌘C",
            EditorMainMenuCommand.PasteHere => "⌘V",
            EditorMainMenuCommand.PasteReplace => "⇧⌘R",
            EditorMainMenuCommand.DuplicateSelection => "⌘D",
            EditorMainMenuCommand.DeleteSelection => "⌫",
            EditorMainMenuCommand.SelectAll => "⌘A",
            EditorMainMenuCommand.SelectNone => "Esc",
            EditorMainMenuCommand.ZoomIn => "⌘+",
            EditorMainMenuCommand.ZoomOut => "⌘−",
            EditorMainMenuCommand.ZoomTo50 => "50%",
            EditorMainMenuCommand.ZoomTo100 => "⌘0",
            EditorMainMenuCommand.ZoomTo200 => "200%",
            EditorMainMenuCommand.ZoomToFit => "⇧1",
            EditorMainMenuCommand.ZoomToSelection => "⇧2",
            EditorMainMenuCommand.ToggleRulers => "⇧R",
            EditorMainMenuCommand.SelectTool => "V",
            EditorMainMenuCommand.HandTool => "H",
            EditorMainMenuCommand.ScaleTool => "K",
            EditorMainMenuCommand.CommentTool => "C",
            EditorMainMenuCommand.PenTool => "P",
            EditorMainMenuCommand.PencilTool => "⇧P",
            EditorMainMenuCommand.RectangleTool => "R",
            EditorMainMenuCommand.EllipseTool => "O",
            EditorMainMenuCommand.LineTool => "L",
            EditorMainMenuCommand.ArrowTool => "⇧L",
            EditorMainMenuCommand.TextTool => "T",
            EditorMainMenuCommand.StarTool => string.Empty,
            EditorMainMenuCommand.ImageTool => string.Empty,
            EditorMainMenuCommand.FrameTool => "F",
            EditorMainMenuCommand.SectionTool => "⇧S",
            EditorMainMenuCommand.SliceTool => "S",
            EditorMainMenuCommand.BrushTool => "B",
            EditorMainMenuCommand.InstanceTool => "U",
            EditorMainMenuCommand.GroupSelection => "⌘G",
            EditorMainMenuCommand.FrameSelection => "⇧⌘G",
            EditorMainMenuCommand.UseAsMask => "⇧⌘M",
            EditorMainMenuCommand.ToggleVisibility => "⇧⌘H",
            EditorMainMenuCommand.ToggleLock => "⇧⌘L",
            EditorMainMenuCommand.FlipHorizontal => "⇧H",
            EditorMainMenuCommand.FlipVertical => "⇧V",
            EditorMainMenuCommand.BooleanUnion => "⇧⌘U",
            EditorMainMenuCommand.BooleanSubtract => "⇧⌘D",
            EditorMainMenuCommand.BooleanIntersect => "⇧⌘I",
            EditorMainMenuCommand.BooleanExclude => "⇧⌘E",
            EditorMainMenuCommand.FlattenSelection => "⇧⌘F",
            EditorMainMenuCommand.OutlineStroke => "⇧⌘O",
            EditorMainMenuCommand.ManageLibraries => "⌥⇧⌘L",
            _ => string.Empty
        };
    }

    private async Task OpenSvgFileAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder
        };
        picker.FileTypeFilter.Add(".svg");

        StorageFile? file;
        try
        {
            file = await picker.PickSingleFileAsync();
        }
        catch (Exception ex)
        {
            CanvasStatus = $"Open SVG failed: {ex.Message}";
            return;
        }

        if (file is null)
        {
            return;
        }

        string svg;
        try
        {
            svg = await FileIO.ReadTextAsync(file);
        }
        catch (Exception ex)
        {
            CanvasStatus = $"Unable to read {file.Name}: {ex.Message}";
            return;
        }

        var pageState = CreatePageStateFromSvg(file.DisplayName, svg);
        if (pageState is null)
        {
            CanvasStatus = $"{file.Name} is not a valid SVG document.";
            return;
        }

        await LoadWorkspaceFromSvgAsync(pageState, file.DisplayName, file);
        CanvasStatus = $"Opened {file.Name}.";
    }

    private async Task SaveSvgFileAsync()
    {
        if (_document is null)
        {
            CanvasStatus = "There is no SVG document to save.";
            return;
        }

        var svg = _documentService.GetXml(_document);

        if (_currentSvgFile is not null && await TryWriteSvgToFileAsync(_currentSvgFile, svg))
        {
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            SuggestedFileName = string.IsNullOrWhiteSpace(DocumentTitle) ? "Untitled" : DocumentTitle
        };
        picker.FileTypeChoices.Add("SVG file", new List<string> { ".svg" });

        StorageFile? file;
        try
        {
            file = await picker.PickSaveFileAsync();
        }
        catch (Exception ex)
        {
            CanvasStatus = $"Save SVG failed: {ex.Message}";
            return;
        }

        if (file is null)
        {
            return;
        }

        await TryWriteSvgToFileAsync(file, svg);
    }

    private async Task<bool> TryWriteSvgToFileAsync(StorageFile file, string svg)
    {
        try
        {
            await FileIO.WriteTextAsync(file, svg);
            _currentSvgFile = file;
            DocumentTitle = file.DisplayName;
            UpdateCurrentFileLibraryOverview();
            CanvasStatus = $"Saved {file.Name}.";
            return true;
        }
        catch (Exception ex)
        {
            if (_currentSvgFile is not null && string.Equals(_currentSvgFile.Name, file.Name, StringComparison.Ordinal))
            {
                _currentSvgFile = null;
            }

            CanvasStatus = $"Unable to save {file.Name}: {ex.Message}";
            return false;
        }
    }

    private async Task LoadWorkspaceFromSvgAsync(EditorPageState state, string documentTitle, StorageFile file)
    {
        _pageStates.Clear();
        Pages.Clear();
        Libraries.Clear();
        _libraryCatalog.Clear();
        _currentSvgFile = file;
        _selectedComponentAsset = null;

        _pageStates.Add(state);
        Pages.Add(state.Page);

        DocumentTitle = string.IsNullOrWhiteSpace(documentTitle) ? Path.GetFileNameWithoutExtension(file.Name) : documentTitle;
        SetTool(ToolService.Tool.Select);
        _propertiesService.Properties.Clear();
        _propertiesService.FilteredProperties.Clear();
        _generatedId = 0;
        ResetCommentState();
        InitializeLibraries();

        await SwitchToPageAsync(state, selectDefaultSelection: true, resetViewport: true);
    }

    protected async void OnSelectionContextMenuCommandRequested(object? sender, SelectionContextMenuCommandEventArgs e)
    {
        switch (e.Command)
        {
            case SelectionContextMenuCommand.Copy:
                CopySelectionToClipboard();
                break;
            case SelectionContextMenuCommand.PasteHere:
                PasteClipboardHere();
                break;
            case SelectionContextMenuCommand.PasteReplace:
                PasteClipboardReplace();
                break;
            case SelectionContextMenuCommand.SelectLayer:
                SelectLayerFromContextMenu(e.Parameter);
                break;
            case SelectionContextMenuCommand.MoveToPage:
                await MoveSelectionToPageAsync(e.Parameter);
                break;
            case SelectionContextMenuCommand.BringToFront:
                BringSelectionToFront();
                break;
            case SelectionContextMenuCommand.SendToBack:
                SendSelectionToBack();
                break;
            case SelectionContextMenuCommand.ConvertToSection:
                ConvertSelectionToSection();
                break;
            case SelectionContextMenuCommand.GroupSelection:
                GroupSelection();
                break;
            case SelectionContextMenuCommand.FrameSelection:
                FrameSelection();
                break;
            case SelectionContextMenuCommand.Ungroup:
                UngroupSelection();
                break;
            case SelectionContextMenuCommand.Rename:
                await RenameSelectionAsync();
                break;
            case SelectionContextMenuCommand.BooleanUnion:
                ApplyBooleanSelection(SK.SKPathOp.Union, "Union", "union");
                break;
            case SelectionContextMenuCommand.BooleanSubtract:
                ApplyBooleanSelection(SK.SKPathOp.Difference, "Subtract", "subtract");
                break;
            case SelectionContextMenuCommand.BooleanIntersect:
                ApplyBooleanSelection(SK.SKPathOp.Intersect, "Intersect", "intersect");
                break;
            case SelectionContextMenuCommand.BooleanExclude:
                ApplyBooleanSelection(SK.SKPathOp.Xor, "Exclude", "exclude");
                break;
            case SelectionContextMenuCommand.Flatten:
                FlattenSelection();
                break;
            case SelectionContextMenuCommand.OutlineStroke:
                OutlineSelectionStroke();
                break;
            case SelectionContextMenuCommand.UseAsMask:
                UseSelectionAsMask();
                break;
            case SelectionContextMenuCommand.RemoveAutoLayout:
                RemoveSelectionAutoLayout();
                break;
            case SelectionContextMenuCommand.SetAutoLayoutHorizontal:
                ApplySelectionAutoLayoutFlow(AutoLayoutFlow.Horizontal);
                break;
            case SelectionContextMenuCommand.SetAutoLayoutVertical:
                ApplySelectionAutoLayoutFlow(AutoLayoutFlow.Vertical);
                break;
            case SelectionContextMenuCommand.SetAutoLayoutWrap:
                ApplySelectionAutoLayoutFlow(AutoLayoutFlow.Wrap);
                break;
            case SelectionContextMenuCommand.SetAutoLayoutGrid:
                ApplySelectionAutoLayoutFlow(AutoLayoutFlow.Grid);
                break;
            case SelectionContextMenuCommand.ToggleAutoLayoutClipContent:
                ToggleSelectionAutoLayoutClipContent();
                break;
            case SelectionContextMenuCommand.CreateComponent:
                CreateComponentFromSelection();
                break;
            case SelectionContextMenuCommand.ToggleVisibility:
                ToggleSelectionVisibility();
                break;
            case SelectionContextMenuCommand.ToggleLock:
                ToggleSelectionLock();
                break;
            case SelectionContextMenuCommand.FlipHorizontal:
                FlipSelectionContext(horizontal: true);
                break;
            case SelectionContextMenuCommand.FlipVertical:
                FlipSelectionContext(horizontal: false);
                break;
        }
    }

    private void ShowSelectionContextMenu(FrameworkElement target, Point? position, IReadOnlyList<SelectionContextMenuLayerItem> layerItems)
    {
        _selectionContextMenu.State = BuildSelectionContextMenuState(layerItems);
        _selectionContextMenu.ShowAt(target, position);
    }

    private SelectionContextMenuState BuildSelectionContextMenuState(IReadOnlyList<SelectionContextMenuLayerItem> layerItems)
    {
        var selectionRoots = GetSelectionRoots();
        var pageItems = BuildContextMenuPageItems(selectionRoots);
        var hasClipboard = _clipboard.Element is not null;
        var hasSelection = selectionRoots.Count > 0;
        var hasSharedParent = TryGetSelectionParent(selectionRoots, out _);
        var canBooleanCombine = CanApplyBooleanPathOperation(selectionRoots);
        var canFlatten = CanFlattenPathSelection(selectionRoots);
        var canOutlineStroke = selectionRoots.Count > 0 && selectionRoots.All(CanOutlineStroke);
        var canConvertToSection = TryGetEditableContainer(out var editableContainer)
            && FrameService.GetContainerKind(editableContainer) != FrameContainerKind.Section;
        var canUngroup = selectionRoots.Count == 1
            && _selectedElement is SvgGroup group
            && !IsFrameGroup(group)
            && !_autoLayoutService.IsFrameContentGroup(group);
        var frameGroup = selectionRoots.Count == 1
            && _selectedElement is SvgGroup selected
            && IsFrameGroup(selected)
            && FrameService.GetContainerKind(selected) == FrameContainerKind.Frame
            ? selected
            : null;
        var autoLayoutSettings = frameGroup is not null
            ? _autoLayoutService.ReadSettings(frameGroup)
            : new AutoLayoutSettings();
        var canCreateComponent = CanCreateComponentSelection();
        var visibilityText = selectionRoots.Count > 0 && selectionRoots.All(IsElementVisible)
            ? "Hide selection"
            : "Show selection";
        var lockText = selectionRoots.Count > 0 && selectionRoots.All(IsElementLocked)
            ? "Unlock selection"
            : "Lock selection";

        return new SelectionContextMenuState
        {
            CanCopy = hasSelection,
            CanPasteHere = hasClipboard,
            CanPasteReplace = hasClipboard && hasSelection && hasSharedParent,
            CanMoveToPage = hasSelection && pageItems.Length > 0,
            CanBringToFront = hasSelection && hasSharedParent,
            CanSendToBack = hasSelection && hasSharedParent,
            CanConvertToSection = canConvertToSection,
            CanGroupSelection = selectionRoots.Count > 1 && hasSharedParent,
            CanFrameSelection = hasSelection && hasSharedParent && !selectionRoots.Any(IsInsideSymbol),
            CanUngroup = canUngroup,
            CanRename = selectionRoots.Count == 1,
            CanBooleanCombine = canBooleanCombine,
            CanFlatten = canFlatten,
            CanOutlineStroke = canOutlineStroke,
            CanUseAsMask = selectionRoots.Count >= 2 && hasSharedParent,
            CanRemoveAutoLayout = frameGroup is not null && autoLayoutSettings.IsEnabled,
            ShowLayoutOptions = frameGroup is not null,
            CanSetAutoLayoutHorizontal = frameGroup is not null,
            CanSetAutoLayoutVertical = frameGroup is not null,
            CanSetAutoLayoutWrap = frameGroup is not null,
            CanSetAutoLayoutGrid = frameGroup is not null,
            CanToggleAutoLayoutClipContent = frameGroup is not null,
            IsAutoLayoutClipContent = autoLayoutSettings.ClipContent,
            CanCreateComponent = canCreateComponent,
            CanToggleVisibility = hasSelection,
            VisibilityText = visibilityText,
            CanToggleLock = hasSelection,
            LockText = lockText,
            CanFlipHorizontal = hasSelection,
            CanFlipVertical = hasSelection,
            PageItems = pageItems,
            LayerItems = layerItems
        };
    }

    private SelectionContextMenuPageItem[] BuildContextMenuPageItems(IReadOnlyList<SvgVisualElement> selectionRoots)
    {
        _contextMenuPages.Clear();

        if (_activePage is null || selectionRoots.Count == 0)
        {
            return [];
        }

        var items = new List<SelectionContextMenuPageItem>();
        for (var index = 0; index < _pageStates.Count; index++)
        {
            var state = _pageStates[index];
            if (ReferenceEquals(state, _activePage))
            {
                continue;
            }

            var key = $"page-{index}";
            _contextMenuPages[key] = state;
            items.Add(new SelectionContextMenuPageItem(key, state.Page.Title));
        }

        return items.ToArray();
    }

    private SelectionContextMenuLayerItem[] BuildCanvasLayerItems(Point viewPoint)
    {
        _contextMenuLayers.Clear();

        return GetVisualHits(viewPoint, includeLocked: true)
            .Select(CreateSelectionContextMenuLayerItem)
            .ToArray();
    }

    private SelectionContextMenuLayerItem[] BuildOutlineLayerItems(SvgElement element)
    {
        _contextMenuLayers.Clear();

        var layers = new List<SvgElement>();
        if (ShouldShowInOutline(element))
        {
            layers.Add(element);
        }

        foreach (var parent in element.Parents.OfType<SvgElement>())
        {
            if (!ShouldShowInOutline(parent) || layers.Any(existing => ReferenceEquals(existing, parent)))
            {
                continue;
            }

            layers.Add(parent);
        }

        return layers
            .Select(CreateSelectionContextMenuLayerItem)
            .ToArray();
    }

    private SelectionContextMenuLayerItem CreateSelectionContextMenuLayerItem(SvgElement element)
    {
        EnsureElementId(element);
        if (element is SvgVisualElement visual)
        {
            _contextMenuLayers[element.ID!] = visual;
        }

        var label = string.IsNullOrWhiteSpace(element.ID)
            ? SvgElementInfo.GetElementName(element.GetType())
            : element.ID!;
        var subtitle = string.IsNullOrWhiteSpace(element.ID)
            ? null
            : SvgElementInfo.GetElementName(element.GetType());
        var icon = EditorObjectNode.GetIconKind(element);
        var isSelected = element is SvgVisualElement selectedVisual && _selectedElements.Contains(selectedVisual);
        return new SelectionContextMenuLayerItem(element.ID!, label, subtitle, icon, isSelected);
    }

    private void SelectLayerFromContextMenu(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || _document is null)
        {
            return;
        }

        if (!_contextMenuLayers.TryGetValue(key, out var visual))
        {
            visual = _document
                .Descendants()
                .OfType<SvgVisualElement>()
                .FirstOrDefault(element => string.Equals(element.ID, key, StringComparison.Ordinal));
        }

        if (visual is null)
        {
            return;
        }

        ApplySelection([visual], visual);
    }

    private async Task MoveSelectionToPageAsync(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)
            || _document is null
            || _activePage is null
            || !_contextMenuPages.TryGetValue(key, out var targetPage))
        {
            return;
        }

        var selectionRoots = GetSelectionRoots();
        if (selectionRoots.Count == 0)
        {
            return;
        }

        var movedElements = new List<SvgVisualElement>();
        foreach (var element in selectionRoots)
        {
            var clone = (SvgElement)element.DeepCopy();
            EnsureElementTreeIds(clone, targetPage.Document);
            EnsureReferencedSymbolsInDocument(clone, targetPage.Document);

            targetPage.Document.Children.Add(clone);
            if (clone is SvgVisualElement visual)
            {
                movedElements.Add(visual);
            }
        }

        foreach (var parentGroup in selectionRoots
                     .Where(static element => element.Parent is SvgElement)
                     .GroupBy(static element => (SvgElement)element.Parent!))
        {
            var parent = parentGroup.Key;
            foreach (var element in parentGroup.OrderByDescending(parent.Children.IndexOf))
            {
                parent.Children.Remove(element);
            }
        }

        foreach (var moved in selectionRoots.Cast<SvgElement>())
        {
            _collapsedElements.Remove(moved);
            _activePage.CollapsedElements.Remove(moved);
            foreach (var descendant in moved.Descendants().OfType<SvgElement>())
            {
                _collapsedElements.Remove(descendant);
                _activePage.CollapsedElements.Remove(descendant);
            }
        }

        _selectedElements.Clear();
        _selectedDrawables.Clear();
        _selectedElement = null;
        _selectedDrawable = null;
        _activePage.SelectedElements.Clear();
        _activePage.PrimarySelection = null;

        targetPage.SelectedElements.Clear();
        targetPage.SelectedElements.AddRange(movedElements);
        targetPage.PrimarySelection = movedElements.LastOrDefault();

        UpdatePageMetadata(_activePage);
        UpdatePageMetadata(targetPage);
        await SwitchToPageAsync(targetPage, selectDefaultSelection: false, resetViewport: false);
        CanvasStatus = movedElements.Count == 1
            ? $"Moved {movedElements[0].ID ?? "layer"} to {targetPage.Page.Title}."
            : $"Moved {movedElements.Count} layers to {targetPage.Page.Title}.";
    }

    private void ConvertSelectionToSection()
    {
        if (!TryGetEditableContainer(out var group))
        {
            CanvasStatus = "Select a group or frame to convert it to a section.";
            return;
        }

        if (FrameService.GetContainerKind(group) == FrameContainerKind.Section)
        {
            return;
        }

        ConvertContainerKind(group, FrameContainerKind.Section);
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection([group], group);
        CanvasStatus = $"Converted {group.ID ?? "container"} to a section.";
    }

    private async Task RenameSelectionAsync()
    {
        if (_selectedElements.Count != 1 || _selectedElement is null || _document is null)
        {
            return;
        }

        var currentName = _selectedElement.ID ?? SvgElementInfo.GetElementName(_selectedElement.GetType());
        var proposedName = await ShowTextInputDialogAsync("Rename layer", "Layer name", currentName);
        if (string.IsNullOrWhiteSpace(proposedName))
        {
            return;
        }

        var sanitizedName = SanitizeElementId(proposedName, SvgElementInfo.GetElementName(_selectedElement.GetType()));
        if (string.Equals(currentName, sanitizedName, StringComparison.Ordinal))
        {
            return;
        }

        _selectedElement.ID = MakeUniqueElementId(sanitizedName, _document, _selectedElement);
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection([_selectedElement], _selectedElement);
        CanvasStatus = $"Renamed the layer to {_selectedElement.ID}.";
    }

    private async Task<string?> ShowTextInputDialogAsync(string title, string placeholderText, string text)
    {
        if (XamlRoot is null)
        {
            CanvasStatus = "This action requires an active XAML root.";
            return null;
        }

        var input = new TextBox
        {
            PlaceholderText = placeholderText,
            Text = text,
            MinWidth = 320,
            MaxWidth = 420
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = input,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        dialog.Opened += (_, _) =>
        {
            input.Focus(FocusState.Programmatic);
            input.SelectAll();
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? input.Text.Trim() : null;
    }

    private void CopySelectionToClipboard()
    {
        var selectionRoots = GetSelectionRoots();
        if (selectionRoots.Count == 0)
        {
            return;
        }

        SvgElement snapshot;
        if (selectionRoots.Count == 1)
        {
            snapshot = (SvgElement)selectionRoots[0].DeepCopy();
        }
        else
        {
            var wrapper = new SvgGroup
            {
                ID = CreateUniqueId("clipboard")
            };
            wrapper.CustomAttributes["data-editor-clipboard"] = "selection";
            foreach (var element in selectionRoots)
            {
                wrapper.Children.Add((SvgElement)element.DeepCopy());
            }

            snapshot = wrapper;
        }

        _clipboard.Element = snapshot;
        _clipboardXml = snapshot.GetXML();
        _clipboard.Xml = _clipboardXml;
        _clipboardBounds = TryGetElementBounds(selectionRoots, out var bounds) ? bounds : null;
        CanvasStatus = selectionRoots.Count == 1
            ? $"Copied {selectionRoots[0].ID ?? SvgElementInfo.GetElementName(selectionRoots[0].GetType())}."
            : $"Copied {selectionRoots.Count} layers.";
    }

    private void PasteClipboardHere()
    {
        var targetPoint = _hasContextMenuPicturePoint ? _contextMenuPicturePoint : GetDefaultInsertPoint();
        PasteClipboard(targetPoint, replaceSelection: false);
    }

    private void PasteClipboardReplace()
    {
        var targetPoint = TryGetSelectionBoundsCenter(out var selectionCenter)
            ? selectionCenter
            : (_hasContextMenuPicturePoint ? _contextMenuPicturePoint : GetDefaultInsertPoint());
        PasteClipboard(targetPoint, replaceSelection: true);
    }

    private void PasteClipboard(Shim.SKPoint targetPoint, bool replaceSelection)
    {
        if (_document is null || _clipboard.Element is null)
        {
            return;
        }

        var pastedElements = ExtractClipboardElements();
        if (pastedElements.Count == 0)
        {
            return;
        }

        SvgElement parent;
        var insertIndex = 0;
        var replacedCount = 0;

        if (replaceSelection)
        {
            var selectionRoots = GetSelectionRoots();
            if (!TryGetSelectionParent(selectionRoots, out var selectionParent))
            {
                CanvasStatus = "Paste to replace requires the selected layers to share one parent.";
                return;
            }

            parent = selectionParent!;
            insertIndex = selectionRoots
                .Select(parent.Children.IndexOf)
                .Where(index => index >= 0)
                .DefaultIfEmpty(parent.Children.Count)
                .Min();

            foreach (var element in selectionRoots
                         .OrderByDescending(parent.Children.IndexOf))
            {
                parent.Children.Remove(element);
                replacedCount++;
            }
        }
        else
        {
            parent = GetCreationParent(targetPoint);
            insertIndex = parent.Children.Count;
        }

        if (_clipboardBounds is { } clipboardBounds)
        {
            var centerX = (clipboardBounds.Left + clipboardBounds.Right) / 2f;
            var centerY = (clipboardBounds.Top + clipboardBounds.Bottom) / 2f;
            var dx = targetPoint.X - centerX;
            var dy = targetPoint.Y - centerY;

            foreach (var element in pastedElements.OfType<SvgVisualElement>())
            {
                ApplyTranslationDelta(element, dx, dy);
            }
        }

        foreach (var element in pastedElements)
        {
            EnsureElementTreeIds(element);
            if (insertIndex >= parent.Children.Count)
            {
                parent.Children.Add(element);
            }
            else
            {
                parent.Children.Insert(insertIndex, element);
            }

            insertIndex++;
        }

        var pastedVisuals = pastedElements.OfType<SvgVisualElement>().ToList();
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection(pastedVisuals, pastedVisuals.LastOrDefault());

        CanvasStatus = replaceSelection
            ? $"Pasted {pastedVisuals.Count} layer{(pastedVisuals.Count == 1 ? string.Empty : "s")} to replace {replacedCount} selection{(replacedCount == 1 ? string.Empty : "s")}."
            : $"Pasted {pastedVisuals.Count} layer{(pastedVisuals.Count == 1 ? string.Empty : "s")} into the current canvas.";
    }

    private List<SvgElement> ExtractClipboardElements()
    {
        if (_clipboard.Element is null)
        {
            return [];
        }

        var snapshot = (SvgElement)_clipboard.Element.DeepCopy();
        if (snapshot is SvgGroup group
            && group.CustomAttributes.TryGetValue("data-editor-clipboard", out var marker)
            && string.Equals(marker, "selection", StringComparison.OrdinalIgnoreCase))
        {
            group.CustomAttributes.Remove("data-editor-clipboard");
            var elements = group.Children.OfType<SvgElement>().ToList();
            foreach (var child in elements.ToList())
            {
                group.Children.Remove(child);
            }

            return elements;
        }

        return [snapshot];
    }

    private void CopyDocumentSvgToSystemClipboard()
    {
        if (_document is null)
        {
            return;
        }

        TrySetClipboardText(_documentService.GetXml(_document), "Copied the current document SVG to the system clipboard.");
    }

    private void CopySelectionSvgToSystemClipboard()
    {
        var selectionRoots = GetSelectionRoots();
        if (selectionRoots.Count == 0)
        {
            return;
        }

        string svg;
        if (selectionRoots.Count == 1)
        {
            svg = selectionRoots[0].GetXML();
        }
        else
        {
            var wrapper = new SvgGroup
            {
                ID = "selection"
            };

            foreach (var element in selectionRoots)
            {
                wrapper.Children.Add((SvgElement)element.DeepCopy());
            }

            svg = wrapper.GetXML();
        }

        TrySetClipboardText(svg, "Copied the selected SVG markup to the system clipboard.");
    }

    private void TrySetClipboardText(string text, string successMessage)
    {
        try
        {
            var dataPackage = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
            CanvasStatus = successMessage;
        }
        catch
        {
            CanvasStatus = "The system clipboard is not available in this environment.";
        }
    }

    private void DuplicateSelection()
    {
        var selectionRoots = GetSelectionRoots();
        if (selectionRoots.Count == 0)
        {
            return;
        }

        var duplicatedVisuals = new List<SvgVisualElement>();
        foreach (var parentGroup in selectionRoots
                     .Where(static element => element.Parent is SvgElement)
                     .GroupBy(static element => (SvgElement)element.Parent!))
        {
            var parent = parentGroup.Key;
            var ordered = parentGroup.OrderBy(parent.Children.IndexOf).ToList();
            var insertIndex = ordered.Max(parent.Children.IndexOf) + 1;
            foreach (var element in ordered)
            {
                var clone = (SvgElement)element.DeepCopy();
                EnsureElementTreeIds(clone);
                if (clone is SvgVisualElement visual)
                {
                    ApplyTranslationDelta(visual, 24f, 24f);
                    duplicatedVisuals.Add(visual);
                }

                if (insertIndex >= parent.Children.Count)
                {
                    parent.Children.Add(clone);
                }
                else
                {
                    parent.Children.Insert(insertIndex, clone);
                }

                insertIndex++;
            }
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection(duplicatedVisuals, duplicatedVisuals.LastOrDefault());
        CanvasStatus = duplicatedVisuals.Count == 1
            ? "Duplicated the selected layer."
            : $"Duplicated {duplicatedVisuals.Count} selected layers.";
    }

    private void DeleteSelection()
    {
        var selectionRoots = GetSelectionRoots();
        if (selectionRoots.Count == 0)
        {
            return;
        }

        foreach (var parentGroup in selectionRoots
                     .Where(static element => element.Parent is SvgElement)
                     .GroupBy(static element => (SvgElement)element.Parent!))
        {
            var parent = parentGroup.Key;
            foreach (var element in parentGroup.OrderByDescending(parent.Children.IndexOf))
            {
                parent.Children.Remove(element);
            }
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection(Array.Empty<SvgVisualElement>(), null);
        CanvasStatus = selectionRoots.Count == 1
            ? "Deleted the selected layer."
            : $"Deleted {selectionRoots.Count} selected layers.";
    }

    private void SelectAllObjects()
    {
        if (_document is null)
        {
            return;
        }

        var allVisuals = _document
            .Descendants()
            .OfType<SvgVisualElement>()
            .Where(ShouldShowInOutline)
            .Where(IsElementVisible)
            .Distinct()
            .ToList();
        if (allVisuals.Count == 0)
        {
            return;
        }

        var selection = allVisuals
            .Where(element => !allVisuals.Any(candidate =>
                !ReferenceEquals(candidate, element)
                && element.Parents.OfType<SvgElement>().Any(parent => ReferenceEquals(parent, candidate))))
            .ToList();

        ApplySelection(selection, selection.LastOrDefault());
        CanvasStatus = $"Selected {selection.Count} layer{(selection.Count == 1 ? string.Empty : "s")}.";
    }

    private void BringSelectionToFront()
    {
        var selectionRoots = GetSelectionRoots();
        if (!TryGetSelectionParent(selectionRoots, out var parent))
        {
            return;
        }

        var ordered = selectionRoots.OrderBy(parent!.Children.IndexOf).ToList();
        foreach (var element in ordered)
        {
            parent.Children.Remove(element);
        }

        foreach (var element in ordered)
        {
            parent.Children.Add(element);
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection(ordered, ordered.LastOrDefault());
        CanvasStatus = "Moved the selection to the front.";
    }

    private void BringSelectionForward()
    {
        var selectionRoots = GetSelectionRoots();
        if (!TryGetSelectionParent(selectionRoots, out var parent))
        {
            return;
        }

        var selected = new HashSet<SvgVisualElement>(selectionRoots);
        var ordered = selectionRoots.OrderByDescending(parent!.Children.IndexOf).ToList();
        foreach (var element in ordered)
        {
            var index = parent.Children.IndexOf(element);
            if (index < 0 || index >= parent.Children.Count - 1)
            {
                continue;
            }

            var next = parent.Children[index + 1];
            if (next is SvgVisualElement nextVisual && selected.Contains(nextVisual))
            {
                continue;
            }

            parent.Children.RemoveAt(index);
            parent.Children.Insert(index + 1, element);
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection(selectionRoots, _selectedElement);
        CanvasStatus = "Moved the selection forward.";
    }

    private void SendSelectionToBack()
    {
        var selectionRoots = GetSelectionRoots();
        if (!TryGetSelectionParent(selectionRoots, out var parent))
        {
            return;
        }

        var ordered = selectionRoots.OrderBy(parent!.Children.IndexOf).ToList();
        foreach (var element in ordered)
        {
            parent.Children.Remove(element);
        }

        var insertIndex = parent is SvgDocument
            ? parent.Children.TakeWhile(static child => child is SvgDefinitionList).Count()
            : 0;
        foreach (var element in ordered)
        {
            parent.Children.Insert(insertIndex++, element);
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection(ordered, ordered.LastOrDefault());
        CanvasStatus = "Moved the selection to the back.";
    }

    private void SendSelectionBackward()
    {
        var selectionRoots = GetSelectionRoots();
        if (!TryGetSelectionParent(selectionRoots, out var parent))
        {
            return;
        }

        var selected = new HashSet<SvgVisualElement>(selectionRoots);
        var ordered = selectionRoots.OrderBy(parent!.Children.IndexOf).ToList();
        foreach (var element in ordered)
        {
            var index = parent.Children.IndexOf(element);
            if (index <= 0)
            {
                continue;
            }

            var previous = parent.Children[index - 1];
            if (previous is SvgVisualElement previousVisual && selected.Contains(previousVisual))
            {
                continue;
            }

            parent.Children.RemoveAt(index);
            parent.Children.Insert(index - 1, element);
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection(selectionRoots, _selectedElement);
        CanvasStatus = "Moved the selection backward.";
    }

    private void GroupSelection()
    {
        var selectionRoots = GetSelectionRoots();
        if (selectionRoots.Count < 2 || !TryGetSelectionParent(selectionRoots, out var parent))
        {
            CanvasStatus = "Select at least two sibling layers to group them.";
            return;
        }

        var ordered = selectionRoots.OrderBy(parent!.Children.IndexOf).ToList();
        var group = new SvgGroup
        {
            ID = CreateUniqueId("group")
        };

        var insertIndex = ordered.Min(parent.Children.IndexOf);
        if (insertIndex >= parent.Children.Count)
        {
            parent.Children.Add(group);
        }
        else
        {
            parent.Children.Insert(insertIndex, group);
        }

        foreach (var element in ordered)
        {
            parent.Children.Remove(element);
            group.Children.Add(element);
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection([group], group);
        CanvasStatus = $"Grouped {ordered.Count} layers.";
    }

    private void FrameSelection()
    {
        var selectionRoots = GetSelectionRoots();
        if (selectionRoots.Count == 0
            || !TryGetSelectionParent(selectionRoots, out var parent)
            || !TryGetElementBounds(selectionRoots, out var bounds))
        {
            CanvasStatus = "Select one or more sibling layers to frame them.";
            return;
        }

        const float padding = 24f;
        var frame = CreateFrameGroup(
            $"Frame {_generatedId + 1}",
            bounds.Left - padding,
            bounds.Top - padding,
            bounds.Width + (padding * 2f),
            bounds.Height + (padding * 2f),
            System.Drawing.Color.White);
        EnsureElementTreeIds(frame);

        var contentGroup = _autoLayoutService.EnsureContentGroup(frame);
        var ordered = selectionRoots.OrderBy(parent!.Children.IndexOf).ToList();
        var insertIndex = ordered.Min(parent.Children.IndexOf);
        if (insertIndex >= parent.Children.Count)
        {
            parent.Children.Add(frame);
        }
        else
        {
            parent.Children.Insert(insertIndex, frame);
        }

        foreach (var element in ordered)
        {
            parent.Children.Remove(element);
            contentGroup.Children.Add(element);
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection([frame], frame);
        CanvasStatus = $"Framed {ordered.Count} layers.";
    }

    private void UngroupSelection()
    {
        if (_selectedElements.Count != 1
            || _selectedElement is not SvgGroup group
            || IsFrameGroup(group)
            || _autoLayoutService.IsFrameContentGroup(group)
            || group.Parent is not SvgElement parent)
        {
            CanvasStatus = "Select a regular group to ungroup it.";
            return;
        }

        var children = group.Children.OfType<SvgElement>().ToList();
        var insertIndex = parent.Children.IndexOf(group);
        parent.Children.Remove(group);
        foreach (var child in children)
        {
            group.Children.Remove(child);
            parent.Children.Insert(insertIndex++, child);
        }

        var visuals = children.OfType<SvgVisualElement>().ToList();
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection(visuals, visuals.LastOrDefault());
        CanvasStatus = $"Ungrouped {group.ID ?? "group"} into {visuals.Count} layers.";
    }

    private void FlattenSelection()
    {
        var selectionRoots = GetSelectionRoots();
        if (selectionRoots.Count == 0)
        {
            return;
        }

        if (!TryGetSelectionParent(selectionRoots, out var parent))
        {
            CanvasStatus = "Flatten requires the selected layers to share a single parent.";
            return;
        }

        var ordered = selectionRoots.OrderBy(parent!.Children.IndexOf).ToList();
        var converted = new List<SvgPath>();
        foreach (var element in ordered)
        {
            if (!TryConvertElementToPath(element, out var path))
            {
                CanvasStatus = "Only geometric vector layers can be flattened right now.";
                return;
            }

            converted.Add(path);
        }

        SvgVisualElement nextSelection;
        if (converted.Count == 1)
        {
            var original = ordered[0];
            var replacement = converted[0];
            var index = parent.Children.IndexOf(original);
            parent.Children.Remove(original);
            if (index >= parent.Children.Count)
            {
                parent.Children.Add(replacement);
            }
            else
            {
                parent.Children.Insert(index, replacement);
            }

            nextSelection = replacement;
        }
        else
        {
            var vectorGroup = new SvgGroup
            {
                ID = CreateUniqueId("vector")
            };
            var insertIndex = ordered.Min(parent.Children.IndexOf);
            foreach (var element in ordered.OrderByDescending(parent.Children.IndexOf))
            {
                parent.Children.Remove(element);
            }

            foreach (var path in converted)
            {
                vectorGroup.Children.Add(path);
            }

            if (insertIndex >= parent.Children.Count)
            {
                parent.Children.Add(vectorGroup);
            }
            else
            {
                parent.Children.Insert(insertIndex, vectorGroup);
            }

            nextSelection = vectorGroup;
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection([nextSelection], nextSelection);
        CanvasStatus = "Flattened the selection into editable vector paths.";
    }

    private void OutlineSelectionStroke()
    {
        var selectionRoots = GetSelectionRoots();
        if (selectionRoots.Count == 0 || !TryGetSelectionParent(selectionRoots, out var parent))
        {
            return;
        }

        var replacements = new List<SvgPath>();
        foreach (var element in selectionRoots.OrderBy(parent!.Children.IndexOf).ToList())
        {
            if (!CanOutlineStroke(element))
            {
                CanvasStatus = "Only stroked vector layers can be outlined right now.";
                return;
            }

            var path = _pathService.OffsetPath(element, Math.Max(element.StrokeWidth.Value, 1f) / 2f);
            if (path is null)
            {
                CanvasStatus = "Unable to outline the current stroke selection.";
                return;
            }

            path.ID = string.IsNullOrWhiteSpace(element.ID) ? CreateUniqueId("outlined") : element.ID;
            path.Fill = element.Stroke;
            path.FillOpacity = element.StrokeOpacity;
            path.Stroke = SvgPaintServer.None;
            path.StrokeWidth = new SvgUnit(SvgUnitType.User, 0f);
            path.Opacity = element.Opacity;
            path.Transforms = CloneTransforms(element.Transforms);

            var index = parent.Children.IndexOf(element);
            parent.Children.Remove(element);
            if (index >= parent.Children.Count)
            {
                parent.Children.Add(path);
            }
            else
            {
                parent.Children.Insert(index, path);
            }

            replacements.Add(path);
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection(replacements, replacements.LastOrDefault());
        CanvasStatus = $"Outlined the stroke on {replacements.Count} layer{(replacements.Count == 1 ? string.Empty : "s")}.";
    }

    private void UseSelectionAsMask()
    {
        var selectionRoots = GetSelectionRoots();
        if (selectionRoots.Count < 2 || !TryGetSelectionParent(selectionRoots, out var parent) || _document is null)
        {
            CanvasStatus = "Select at least two sibling layers to create a mask.";
            return;
        }

        var ordered = selectionRoots.OrderBy(parent!.Children.IndexOf).ToList();
        var mask = ordered[^1];
        var content = ordered.Take(ordered.Count - 1).ToList();
        if (content.Count == 0)
        {
            return;
        }

        var insertIndex = ordered.Min(parent.Children.IndexOf);
        foreach (var element in ordered.OrderByDescending(parent.Children.IndexOf))
        {
            parent.Children.Remove(element);
        }

        var defs = EnsureDefinitions(_document);
        var clipId = CreateUniqueId("clip");
        var clip = new SvgClipPath
        {
            ID = clipId
        };
        var clipElement = (SvgElement)mask.DeepCopy();
        EnsureElementTreeIds(clipElement);
        clip.Children.Add(clipElement);
        defs.Children.Add(clip);

        var group = new SvgGroup
        {
            ID = CreateUniqueId("masked")
        };
        group.ClipPath = new Uri($"#{clipId}", UriKind.Relative);
        foreach (var element in content)
        {
            group.Children.Add(element);
        }

        if (insertIndex >= parent.Children.Count)
        {
            parent.Children.Add(group);
        }
        else
        {
            parent.Children.Insert(insertIndex, group);
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection([group], group);
        CanvasStatus = $"Applied {mask.ID ?? SvgElementInfo.GetElementName(mask.GetType())} as a clipping mask.";
    }

    private void RemoveSelectionAutoLayout()
    {
        if (_document is null || !TryGetAutoLayoutFrame(out var frame))
        {
            return;
        }

        var settings = _autoLayoutService.ReadSettings(frame);
        settings.IsEnabled = false;
        settings.ClipContent = false;
        _autoLayoutService.WriteSettings(frame, settings);
        _autoLayoutService.UpdateClipPath(_document, frame, false);
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        CanvasStatus = $"Removed auto layout from {frame.ID ?? "frame"}.";
    }

    private void ApplySelectionAutoLayoutFlow(AutoLayoutFlow flow)
    {
        if (_document is null || !TryGetAutoLayoutFrame(out var frame))
        {
            return;
        }

        var settings = _autoLayoutService.ReadSettings(frame);
        settings.IsEnabled = true;
        settings.Flow = flow;
        _autoLayoutService.WriteSettings(frame, settings);
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        CanvasStatus = $"Applied {flow} auto layout to {frame.ID ?? "frame"}.";
    }

    private void ToggleSelectionAutoLayoutClipContent()
    {
        if (_document is null || !TryGetAutoLayoutFrame(out var frame))
        {
            return;
        }

        var settings = _autoLayoutService.ReadSettings(frame);
        settings.IsEnabled = true;
        settings.ClipContent = !settings.ClipContent;
        _autoLayoutService.WriteSettings(frame, settings);
        _autoLayoutService.UpdateClipPath(_document, frame, settings.ClipContent);
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        CanvasStatus = settings.ClipContent
            ? $"Enabled clip content on {frame.ID ?? "frame"}."
            : $"Disabled clip content on {frame.ID ?? "frame"}.";
    }

    private void ToggleSelectionVisibility()
    {
        var selectionRoots = GetSelectionRoots();
        if (selectionRoots.Count == 0)
        {
            return;
        }

        var visible = selectionRoots.All(IsElementVisible);
        foreach (var element in selectionRoots)
        {
            SetElementVisible(element, !visible);
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection(selectionRoots, _selectedElement);
        CanvasStatus = visible ? "Hid the selection." : "Showed the selection.";
    }

    private void ToggleSelectionLock()
    {
        var selectionRoots = GetSelectionRoots();
        if (selectionRoots.Count == 0)
        {
            return;
        }

        var unlock = selectionRoots.All(IsElementLocked);
        foreach (var element in selectionRoots)
        {
            SetElementLocked(element, !unlock);
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection(selectionRoots, _selectedElement);
        CanvasStatus = unlock ? "Unlocked the selection." : "Locked the selection.";
    }

    private void FlipSelectionContext(bool horizontal)
    {
        var items = GetSelectedItemsForAlignment();
        if (items.Count == 0 || EditorSvg.SkSvg is null)
        {
            return;
        }

        foreach (var (element, drawable) in items)
        {
            _selectionService.NormalizeWorldTranslation(element);
            var center = SelectionService.GetLocalCenter(drawable);
            if (horizontal)
            {
                _selectionService.FlipHorizontal(element, center);
            }
            else
            {
                _selectionService.FlipVertical(element, center);
            }
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        CanvasStatus = horizontal ? "Flipped the selection horizontally." : "Flipped the selection vertically.";
    }

    private List<SvgVisualElement> GetSelectionRoots()
    {
        return _selectedElements
            .Where(element => !_selectedElements.Any(candidate =>
                !ReferenceEquals(candidate, element)
                && element.Parents.OfType<SvgElement>().Any(parent => ReferenceEquals(parent, candidate))))
            .Distinct()
            .ToList();
    }

    private static bool TryGetSelectionParent(IReadOnlyList<SvgVisualElement> selectionRoots, out SvgElement? parent)
    {
        parent = null;
        if (selectionRoots.Count == 0)
        {
            return false;
        }

        parent = selectionRoots[0].Parent as SvgElement;
        if (parent is null)
        {
            return false;
        }

        var parentElement = parent;
        return selectionRoots.All(element => ReferenceEquals(element.Parent, parentElement));
    }

    private static bool CanOutlineStroke(SvgVisualElement element)
    {
        return element.Stroke is not null
            && !ReferenceEquals(element.Stroke, SvgPaintServer.None)
            && !ReferenceEquals(element.Stroke, SvgPaintServer.NotSet)
            && !ReferenceEquals(element.Stroke, SvgPaintServer.Inherit)
            && element.StrokeWidth.Value > 0f
            && PathService.ElementToPath(element) is not null;
    }

    private static SvgTransformCollection? CloneTransforms(SvgTransformCollection? transforms)
    {
        return transforms is null ? null : (SvgTransformCollection)transforms.Clone();
    }

    private void ApplyTranslationDelta(SvgVisualElement element, float dx, float dy)
    {
        _selectionService.NormalizeWorldTranslation(element);
        var (tx, ty) = _selectionService.GetTranslation(element);
        _selectionService.SetTranslation(element, tx + dx, ty + dy);
    }

    private bool TryGetElementAnchorPoint(SvgElement element, out Shim.SKPoint point)
    {
        point = default;
        switch (element)
        {
            case SvgGroup group when TryGetFrameBackground(group, out var background):
                point = new Shim.SKPoint(
                    background.X.Value + (background.Width.Value / 2f),
                    background.Y.Value + (background.Height.Value / 2f));
                return true;
            case SvgVisualElement visual when TryGetElementBounds([visual], out var bounds):
                point = new Shim.SKPoint(
                    (bounds.Left + bounds.Right) / 2f,
                    (bounds.Top + bounds.Bottom) / 2f);
                return true;
            default:
                return false;
        }
    }

    private bool TryGetSelectionBoundsCenter(out Shim.SKPoint point)
    {
        point = default;
        if (GetSelectionBounds() is not { } bounds)
        {
            return false;
        }

        point = new Shim.SKPoint(
            (bounds.Left + bounds.Right) / 2f,
            (bounds.Top + bounds.Bottom) / 2f);
        return true;
    }

    private bool TryGetSelectionViewCenter(out Point point)
    {
        point = default;
        if (!TryGetSelectionBoundsCenter(out var picturePoint)
            || !EditorSvg.TryGetViewPoint(picturePoint, out var viewPoint))
        {
            return false;
        }

        point = new Point(viewPoint.X, viewPoint.Y);
        return true;
    }

    private static bool IsElementVisible(SvgElement element)
    {
        return !string.Equals(element.Display, "none", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(element.Visibility, "hidden", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetElementVisible(SvgElement element, bool visible)
    {
        element.Visibility = visible ? "visible" : "hidden";
        element.Display = visible ? "inline" : "none";
    }

    private static bool IsElementLocked(SvgElement? element)
    {
        if (element is null)
        {
            return false;
        }

        if (element.CustomAttributes.TryGetValue("data-locked", out var flag)
            && string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return element.Parents.OfType<SvgElement>().Any(parent =>
            parent.CustomAttributes.TryGetValue("data-locked", out var parentFlag)
            && string.Equals(parentFlag, "true", StringComparison.OrdinalIgnoreCase));
    }

    private static void SetElementLocked(SvgElement element, bool locked)
    {
        if (locked)
        {
            element.CustomAttributes["data-locked"] = "true";
        }
        else
        {
            element.CustomAttributes.Remove("data-locked");
        }
    }

    private SvgUse CreateComponentInstance(SvgSymbol symbol, float x, float y, float width, float height)
    {
        return new SvgUse
        {
            ID = CreateUniqueId("instance"),
            ReferencedElement = new Uri($"#{symbol.ID}", UriKind.Relative),
            X = new SvgUnit(SvgUnitType.User, x),
            Y = new SvgUnit(SvgUnitType.User, y),
            Width = new SvgUnit(SvgUnitType.User, Math.Max(width, 1f)),
            Height = new SvgUnit(SvgUnitType.User, Math.Max(height, 1f))
        };
    }

    private Shim.SKPoint GetDefaultInsertPoint()
    {
        if (GetActiveFrame() is SvgGroup frame && TryGetFrameBackground(frame, out var background))
        {
            return new Shim.SKPoint(
                background.X.Value + (background.Width.Value / 2f),
                background.Y.Value + (background.Height.Value / 2f));
        }

        if (TryGetVisiblePictureBounds(out var left, out var top, out var right, out var bottom))
        {
            return new Shim.SKPoint((float)((left + right) / 2.0), (float)((top + bottom) / 2.0));
        }

        var viewBox = _document?.ViewBox ?? default;
        return new Shim.SKPoint(
            viewBox.MinX + (viewBox.Width / 2f),
            viewBox.MinY + (viewBox.Height / 2f));
    }

    private bool TryGetElementBounds(IEnumerable<SvgVisualElement> elements, out Shim.SKRect bounds)
    {
        bounds = default;

        if (EditorSvg.SkSvg?.Drawable is not DrawableBase root)
        {
            return false;
        }

        var hasBounds = false;
        foreach (var element in elements)
        {
            var drawable = FindDrawable(root, element);
            if (drawable is null)
            {
                continue;
            }

            bounds = hasBounds ? UnionRect(bounds, drawable.TransformedBounds) : drawable.TransformedBounds;
            hasBounds = true;
        }

        return hasBounds;
    }

    private SvgSymbol? ResolveComponentSymbol(SvgUse use)
    {
        if (_document is null)
        {
            return null;
        }

        var reference = use.ReferencedElement?.OriginalString;
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var symbolId = reference.TrimStart('#');
        return _document.Children
            .OfType<SvgDefinitionList>()
            .SelectMany(defs => defs.Children.OfType<SvgSymbol>())
            .FirstOrDefault(symbol => string.Equals(symbol.ID, symbolId, StringComparison.Ordinal));
    }

    private void EnsureReferencedSymbolsInDocument(SvgElement root, SvgDocument targetDocument)
    {
        if (root is SvgUse directUse)
        {
            EnsureReferencedSymbolInDocument(directUse, targetDocument);
        }

        foreach (var use in root.Descendants().OfType<SvgUse>())
        {
            EnsureReferencedSymbolInDocument(use, targetDocument);
        }
    }

    private void EnsureReferencedSymbolInDocument(SvgUse use, SvgDocument targetDocument)
    {
        var symbol = ResolveComponentSymbol(use);
        if (symbol is null || string.IsNullOrWhiteSpace(symbol.ID))
        {
            return;
        }

        var existing = targetDocument.Children
            .OfType<SvgDefinitionList>()
            .SelectMany(defs => defs.Children.OfType<SvgSymbol>())
            .Any(existingSymbol => string.Equals(existingSymbol.ID, symbol.ID, StringComparison.Ordinal));
        if (existing)
        {
            return;
        }

        var defs = EnsureDefinitions(targetDocument);
        var clone = (SvgSymbol)symbol.DeepCopy();
        clone.ID = symbol.ID;
        defs.Children.Add(clone);
    }

    private bool IsInsideSymbol(SvgElement element)
    {
        return element.Parent is SvgDefinitionList
            || element.Parents.OfType<SvgElement>().Any(parent => parent is SvgSymbol or SvgDefinitionList);
    }

    private string CreateUniqueId(string prefix)
    {
        return CreateUniqueId(prefix, _document);
    }

    private string CreateUniqueId(string prefix, SvgDocument? document)
    {
        prefix = SanitizeElementId(prefix, "element");

        string candidate;
        do
        {
            candidate = $"{prefix}-{++_generatedId}";
        } while (IsElementIdInUse(document, candidate));

        return candidate;
    }

    private void RaiseComponentStateChanged()
    {
        RaisePropertyChanged(nameof(ComponentPrimaryLabel));
        RaisePropertyChanged(nameof(ComponentSecondaryLabel));
        RaisePropertyChanged(nameof(SelectedComponentAsset));
        RaisePropertyChanged(nameof(CanCreateComponent));
        RaisePropertyChanged(nameof(CanInsertComponentInstance));
        RaisePropertyChanged(nameof(CanSwapComponentInstance));
        RaisePropertyChanged(nameof(CanDetachComponentInstance));
    }

    private static (float Width, float Height) GetComponentSize(SvgSymbol symbol)
    {
        var width = symbol.ViewBox.Width;
        var height = symbol.ViewBox.Height;

        if (width <= 0f
            && symbol.CustomAttributes.TryGetValue("width", out var widthValue)
            && float.TryParse(widthValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedWidth))
        {
            width = parsedWidth;
        }

        if (height <= 0f
            && symbol.CustomAttributes.TryGetValue("height", out var heightValue)
            && float.TryParse(heightValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedHeight))
        {
            height = parsedHeight;
        }

        if (width <= 0f)
        {
            width = 160f;
        }

        if (height <= 0f)
        {
            height = 120f;
        }

        return (width, height);
    }

    private static string GetComponentDisplayName(SvgSymbol symbol)
    {
        return string.IsNullOrWhiteSpace(symbol.ID) ? "Component" : symbol.ID!;
    }

    private SvgElement GetCreationParent(Shim.SKPoint picturePoint)
    {
        if (_document is null || _toolService.CurrentTool is ToolService.Tool.Frame or ToolService.Tool.Section)
        {
            return _document ?? new SvgDocument();
        }

        if (_selectedElement is SvgGroup selectedGroup && !IsFrameGroup(selectedGroup))
        {
            return selectedGroup;
        }

        if (GetFrameAtPoint(picturePoint) is SvgGroup hitFrame)
        {
            return _autoLayoutService.EnsureContentGroup(hitFrame);
        }

        if (GetActiveFrame() is SvgGroup activeFrame)
        {
            return _autoLayoutService.EnsureContentGroup(activeFrame);
        }

        return _document;
    }

    private SvgGroup? GetFrameAtPoint(Shim.SKPoint picturePoint)
    {
        if (_document is null || !EditorSvg.TryGetViewPoint(picturePoint, out var viewPoint))
        {
            return null;
        }

        return PromoteFrameHit(GetVisualHit(new Point(viewPoint.X, viewPoint.Y))) as SvgGroup;
    }

    private bool TryMapPointToElementLocal(SvgElement element, Shim.SKPoint picturePoint, out Shim.SKPoint localPoint)
    {
        localPoint = picturePoint;
        if (_document is null || ReferenceEquals(element, _document))
        {
            return true;
        }

        if (EditorSvg.SkSvg?.Drawable is not DrawableBase root)
        {
            return false;
        }

        var drawable = FindDrawable(root, element);
        if (drawable is null)
        {
            return false;
        }

        if (!drawable.TotalTransform.TryInvert(out var inverse))
        {
            return false;
        }

        localPoint = inverse.MapPoint(picturePoint);
        return true;
    }

    private Shim.SKPoint GetCurrentCreationPoint(SvgVisualElement element, Shim.SKPoint picturePoint)
    {
        if (element.Parent is SvgElement parent && TryMapPointToElementLocal(parent, picturePoint, out var localPoint))
        {
            return localPoint;
        }

        return picturePoint;
    }

    private SvgVisualElement? PromoteFrameHit(SvgVisualElement? hit)
    {
        if (hit is SvgRectangle rect
            && FrameService.IsFrameBackground(rect)
            && rect.Parent is SvgGroup group
            && IsFrameGroup(group))
        {
            return group;
        }

        return hit;
    }

    private SK.SKRect GetResizeRect(SvgVisualElement element, DrawableBase drawable)
    {
        return EditorSvg.SkSvg!.SkiaModel.ToSKRect(SelectionService.GetInteractiveBounds(drawable));
    }

    private bool TryResizeSingleElement(SvgVisualElement element, float? width, float? height)
    {
        switch (element)
        {
            case SvgTextBase text when IsAreaText(text) && HasTextBoxRect(text):
                if (TryGetTextBoxRect(text, out var textRect))
                {
                    var updatedRect = new SK.SKRect(
                        textRect.Left,
                        textRect.Top,
                        textRect.Left + Math.Max(MinimumTextAreaWidth, width ?? textRect.Width),
                        textRect.Top + Math.Max(MinimumTextAreaHeight, height ?? textRect.Height));
                    SetTextBoxRect(text, updatedRect);
                    ApplyTextContentLayout(text, GetEditableTextContent(text));
                    return true;
                }

                return false;
            case SvgRectangle rectangle:
                if (width.HasValue)
                {
                    rectangle.Width = new SvgUnit(rectangle.Width.Type, Math.Max(1f, width.Value));
                }

                if (height.HasValue)
                {
                    rectangle.Height = new SvgUnit(rectangle.Height.Type, Math.Max(1f, height.Value));
                }

                return true;
            case global::Svg.SvgImage image:
                if (width.HasValue)
                {
                    image.Width = new SvgUnit(image.Width.Type, Math.Max(1f, width.Value));
                }

                if (height.HasValue)
                {
                    image.Height = new SvgUnit(image.Height.Type, Math.Max(1f, height.Value));
                }

                return true;
            case SvgUse use:
                if (width.HasValue)
                {
                    use.Width = new SvgUnit(use.Width.Type, Math.Max(1f, width.Value));
                }

                if (height.HasValue)
                {
                    use.Height = new SvgUnit(use.Height.Type, Math.Max(1f, height.Value));
                }

                return true;
            case SvgGroup group when TryGetFrameBackground(group, out var background):
                if (width.HasValue)
                {
                    background.Width = new SvgUnit(background.Width.Type, Math.Max(120f, width.Value));
                }

                if (height.HasValue)
                {
                    background.Height = new SvgUnit(background.Height.Type, Math.Max(120f, height.Value));
                }

                SyncFrameMetadata(group);
                return true;
            default:
                return false;
        }
    }

    private void EnsureElementTreeIds(SvgElement root)
    {
        EnsureElementTreeIds(root, _document);
    }

    private void EnsureElementTreeIds(SvgElement root, SvgDocument? document)
    {
        EnsureElementId(root, document);
        foreach (var child in root.Descendants().OfType<SvgElement>())
        {
            EnsureElementId(child, document);
        }
    }

    private static bool TryGetFrameBackground(SvgGroup group, out SvgRectangle background)
    {
        return FrameService.TryGetBackground(group, out background);
    }

    private static SvgElement? PromoteOutlineSelectionElement(SvgElement? element)
    {
        if (element is SvgRectangle rect
            && FrameService.IsFrameBackground(rect)
            && rect.Parent is SvgGroup group
            && IsFrameGroup(group))
        {
            return group;
        }

        return element;
    }

    private static bool IsFrameGroup(SvgElement? element)
    {
        return FrameService.IsFrameLikeGroup(element);
    }

    private static void SyncFrameMetadata(SvgGroup group)
    {
        FrameService.SyncMetadata(group);
    }

    private void RebuildObjectNodes()
    {
        ObjectNodes.Clear();

        if (_document is null)
        {
            return;
        }

        foreach (var child in _document.Children.OfType<SvgElement>())
        {
            if (child is SvgDefinitionList)
            {
                continue;
            }

            AppendObjectNode(child, 0);
        }
    }

    private void AppendObjectNode(SvgElement element, int depth)
    {
        if (element is SvgDefinitionList || IsInsideSymbol(element))
        {
            return;
        }

        var showNode = ShouldShowInOutline(element) && PassesOutlineFilter(element);
        var nextDepth = depth;

        if (showNode)
        {
            var node = new EditorObjectNode(element, depth)
            {
                IsSelected = element is SvgVisualElement visual && _selectedElements.Contains(visual),
                IsExpanded = !_collapsedElements.Contains(element)
            };
            ObjectNodes.Add(node);
            nextDepth = depth + 1;

            if (!node.IsExpanded)
            {
                return;
            }
        }

        if (!HasChildren(element))
        {
            return;
        }

        foreach (var child in element.Children.OfType<SvgElement>())
        {
            AppendObjectNode(child, nextDepth);
        }
    }

    private bool ShouldShowInOutline(SvgElement element)
    {
        if (_autoLayoutService.IsFrameContentGroup(element)
            || element is SvgDefinitionList
            || FrameService.IsFrameBackground(element)
            || IsInsideSymbol(element))
        {
            return false;
        }

        return element is SvgGroup
            || element is SvgVisualElement;
    }

    private bool PassesOutlineFilter(SvgElement element)
    {
        if (string.IsNullOrWhiteSpace(_outlineFilter))
        {
            return true;
        }

        var name = SvgElementInfo.GetElementName(element.GetType());
        return name.Contains(_outlineFilter, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(element.ID) && element.ID.Contains(_outlineFilter, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasChildren(SvgElement element)
    {
        return element.Children is { Count: > 0 };
    }

    private void SyncOutlineSelectionState()
    {
        var requiresRebuild = false;

        foreach (var selected in _selectedElements.OfType<SvgElement>())
        {
            foreach (var parent in selected.Parents.OfType<SvgElement>())
            {
                if (_collapsedElements.Remove(parent))
                {
                    requiresRebuild = true;
                }
            }

            if (!requiresRebuild && !ObjectNodes.Any(node => ReferenceEquals(node.Element, selected)))
            {
                requiresRebuild = true;
            }
        }

        if (requiresRebuild)
        {
            RebuildObjectNodes();
        }

        UpdateOutlineNodeSelectionState();
    }

    private void UpdateOutlineNodeSelectionState()
    {
        foreach (var node in ObjectNodes)
        {
            node.IsSelected = node.Element is SvgVisualElement visual && _selectedElements.Contains(visual);
        }
    }

    private SvgVisualElement? GetVisualHit(Point viewPoint)
    {
        return GetVisualHits(viewPoint).FirstOrDefault();
    }

    private List<SvgVisualElement> GetSelectionHits(Point viewPoint, bool includeLocked = false)
    {
        var hits = GetVisualHits(viewPoint, includeLocked);
        if (hits.Count == 0)
        {
            if (_selectionScopeFrame is not null && !IsPointInsideFrame(viewPoint, _selectionScopeFrame))
            {
                _selectionScopeFrame = null;
            }

            return hits;
        }

        if (GetFrameScopeEntryCandidate(viewPoint, hits) is { } selectedFrame)
        {
            var frameHits = FilterHitsToFrameScope(hits, selectedFrame, includeFrame: false);
            if (frameHits.Count > 0)
            {
                return frameHits;
            }
        }

        if (_selectionScopeFrame is not { } scopeFrame)
        {
            return hits;
        }

        if (!IsPointInsideFrame(viewPoint, scopeFrame))
        {
            _selectionScopeFrame = null;
            return hits;
        }

        var scopedHits = FilterHitsToFrameScope(hits, scopeFrame, includeFrame: true);
        return scopedHits.Count > 0 ? scopedHits : hits;
    }

    private List<SvgVisualElement> GetVisualHits(Point viewPoint, bool includeLocked = false)
    {
        var hits = CollectVisualHits(EditorSvg.HitTestElements(viewPoint), includeLocked);
        return PrioritizeFrameSelectionHits(hits);
    }

    private SvgVisualElement? GetSelectionTraversalHit(Point viewPoint, bool additive, bool toggle, IReadOnlyList<SvgVisualElement>? providedHits = null)
    {
        var hits = providedHits ?? GetVisualHits(viewPoint);
        if (hits.Count == 0)
        {
            ResetSelectionClickCycle();
            return null;
        }

        var now = Environment.TickCount64;
        var cycleIndex = 0;
        if (CanCycleSelectionHits(viewPoint, hits, now))
        {
            cycleIndex = (_lastSelectionClickIndex + 1) % hits.Count;
        }
        else
        {
            cycleIndex = GetPreferredSelectionCycleIndex(hits, preferUnselected: additive || toggle);
        }

        _lastSelectionClickTicks = now;
        _lastSelectionClickViewPoint = viewPoint;
        _lastSelectionClickHits = hits.ToArray();
        _lastSelectionClickIndex = cycleIndex;
        return hits[cycleIndex];
    }

    private int GetPreferredSelectionCycleIndex(IReadOnlyList<SvgVisualElement> hits, bool preferUnselected = false)
    {
        if (preferUnselected)
        {
            for (var index = 0; index < hits.Count; index++)
            {
                if (!_selectedElements.Contains(hits[index]))
                {
                    return index;
                }
            }
        }

        if (_selectedElement is not null)
        {
            for (var index = 0; index < hits.Count; index++)
            {
                if (ReferenceEquals(hits[index], _selectedElement))
                {
                    return index;
                }
            }
        }

        for (var index = 0; index < hits.Count; index++)
        {
            if (_selectedElements.Contains(hits[index]))
            {
                return index;
            }
        }

        return 0;
    }

    private bool CanCycleSelectionHits(Point viewPoint, IReadOnlyList<SvgVisualElement> hits, long now)
    {
        if (_lastSelectionClickIndex < 0 || _lastSelectionClickHits.Length != hits.Count)
        {
            return false;
        }

        var elapsed = now - _lastSelectionClickTicks;
        if (elapsed < 0 || elapsed > SelectionCycleClickMilliseconds)
        {
            return false;
        }

        var dx = viewPoint.X - _lastSelectionClickViewPoint.X;
        var dy = viewPoint.Y - _lastSelectionClickViewPoint.Y;
        if (Math.Sqrt((dx * dx) + (dy * dy)) > SelectionCycleClickTolerancePixels)
        {
            return false;
        }

        for (var index = 0; index < hits.Count; index++)
        {
            if (!ReferenceEquals(hits[index], _lastSelectionClickHits[index]))
            {
                return false;
            }
        }

        return true;
    }

    private void ResetSelectionClickCycle()
    {
        _lastSelectionClickTicks = 0;
        _lastSelectionClickViewPoint = default;
        _lastSelectionClickHits = [];
        _lastSelectionClickIndex = -1;
    }

    private void UpdateSelectionScope()
    {
        SvgGroup? scopeFrame = null;

        foreach (var element in _selectedElements)
        {
            var frame = GetNearestFrameAncestor(element);
            if (frame is null)
            {
                _selectionScopeFrame = null;
                return;
            }

            if (scopeFrame is null)
            {
                scopeFrame = frame;
                continue;
            }

            if (!ReferenceEquals(scopeFrame, frame))
            {
                _selectionScopeFrame = null;
                return;
            }
        }

        _selectionScopeFrame = scopeFrame;
    }

    private SvgGroup? GetFrameScopeEntryCandidate(Point viewPoint, IReadOnlyList<SvgVisualElement> hits)
    {
        if (_selectedElement is not SvgGroup selectedFrame
            || !IsFrameGroup(selectedFrame)
            || !IsPointInsideFrame(viewPoint, selectedFrame))
        {
            return null;
        }

        return FilterHitsToFrameScope(hits, selectedFrame, includeFrame: false).Count > 0
            ? selectedFrame
            : null;
    }

    private List<SvgVisualElement> FilterHitsToFrameScope(IReadOnlyList<SvgVisualElement> hits, SvgGroup frame, bool includeFrame)
    {
        var scopedHits = new List<SvgVisualElement>();
        foreach (var hit in hits)
        {
            if (ReferenceEquals(hit, frame))
            {
                if (includeFrame)
                {
                    scopedHits.Add(hit);
                }

                continue;
            }

            if (IsElementInsideFrame(hit, frame))
            {
                scopedHits.Add(hit);
            }
        }

        return scopedHits;
    }

    private SvgGroup? GetNearestFrameAncestor(SvgVisualElement? element)
    {
        if (element is null)
        {
            return null;
        }

        return element.Parents
            .OfType<SvgGroup>()
            .FirstOrDefault(IsFrameGroup);
    }

    private bool IsPointInsideFrame(Point viewPoint, SvgGroup frame)
    {
        if (!EditorSvg.TryGetPicturePoint(viewPoint, out var picturePoint)
            || !TryGetFramePictureBounds(frame, out var frameBounds))
        {
            return false;
        }

        return picturePoint.X >= frameBounds.Left
            && picturePoint.X <= frameBounds.Right
            && picturePoint.Y >= frameBounds.Top
            && picturePoint.Y <= frameBounds.Bottom;
    }

    private static bool TryGetFramePictureBounds(SvgGroup frame, out Shim.SKRect bounds)
    {
        bounds = default;
        if (!TryGetFrameBackground(frame, out var background))
        {
            return false;
        }

        bounds = new Shim.SKRect(
            background.X.Value,
            background.Y.Value,
            background.X.Value + background.Width.Value,
            background.Y.Value + background.Height.Value);
        return true;
    }

    private void BeginPendingSelectionPress(
        Point viewPoint,
        Shim.SKPoint picturePoint,
        IReadOnlyList<SvgVisualElement> hits,
        bool additive = false,
        bool toggle = false,
        bool frameBackgroundOnly = false)
    {
        _pendingSelectionPress = true;
        _pendingSelectionAdditive = additive;
        _pendingSelectionToggle = toggle;
        _pendingSelectionFrameBackgroundOnly = frameBackgroundOnly;
        _pendingSelectionViewPoint = viewPoint;
        _pendingSelectionPicturePoint = picturePoint;
        _pendingSelectionHits = hits.ToArray();
        _pendingSelectionDragElement = hits.FirstOrDefault(hit => _selectedElements.Contains(hit)) ?? hits[0];
    }

    private void CancelPendingSelectionPress()
    {
        _pendingSelectionPress = false;
        _pendingSelectionAdditive = false;
        _pendingSelectionToggle = false;
        _pendingSelectionFrameBackgroundOnly = false;
        _pendingSelectionViewPoint = default;
        _pendingSelectionPicturePoint = default;
        _pendingSelectionHits = [];
        _pendingSelectionDragElement = null;
    }

    private void CompletePendingSelectionPress()
    {
        var hits = _pendingSelectionHits;
        var viewPoint = _pendingSelectionViewPoint;
        var additive = _pendingSelectionAdditive;
        var toggle = _pendingSelectionToggle;
        CancelPendingSelectionPress();

        if (hits.Length == 0)
        {
            if (!additive && !toggle)
            {
                ApplySelection(Array.Empty<SvgVisualElement>(), null);
            }

            return;
        }

        if (additive || toggle)
        {
            var targetHit = GetSelectionTraversalHit(viewPoint, additive, toggle, hits);
            SelectElement(targetHit, additive, toggle);
            return;
        }

        var hit = GetSelectionTraversalHit(viewPoint, additive: false, toggle: false, hits);
        SelectElement(hit, additive: false, toggle: false);
    }

    private void TryStartPendingSelectionDrag(Point viewPoint)
    {
        if (!_pendingSelectionPress)
        {
            return;
        }

        var dx = viewPoint.X - _pendingSelectionViewPoint.X;
        var dy = viewPoint.Y - _pendingSelectionViewPoint.Y;
        if (Math.Sqrt((dx * dx) + (dy * dy)) <= DragThreshold)
        {
            return;
        }

        var dragElement = _pendingSelectionDragElement;
        var dragStartPicture = _pendingSelectionPicturePoint;
        var dragStartView = _pendingSelectionViewPoint;
        var additive = _pendingSelectionAdditive;
        var toggle = _pendingSelectionToggle;
        var frameBackgroundOnly = _pendingSelectionFrameBackgroundOnly;
        CancelPendingSelectionPress();
        ResetSelectionClickCycle();

        if (frameBackgroundOnly && ShouldStartFrameBackgroundMarquee(dragElement))
        {
            StartMarqueeSelection(dragStartView, additive, toggle, preferFrameChildren: true);
            _marqueeCurrentView = viewPoint;
            RefreshOverlay();
            return;
        }

        if (dragElement is null)
        {
            return;
        }

        if (!_selectedElements.Contains(dragElement))
        {
            SelectElement(dragElement);
        }

        _isDragging = true;
        _dragStartPicture = dragStartPicture;
        _dragStartTranslations.Clear();
        foreach (var element in _selectedElements)
        {
            _selectionService.NormalizeWorldTranslation(element);
            _dragStartTranslations[element] = _selectionService.GetTranslation(element);
        }
    }

    private bool ShouldStartFrameBackgroundMarquee(SvgVisualElement? dragElement)
    {
        if (dragElement is not SvgGroup frame || !IsFrameGroup(frame))
        {
            return false;
        }

        if (_selectionScopeFrame is not null && ReferenceEquals(_selectionScopeFrame, frame))
        {
            return true;
        }

        return _selectedElements.Any(element => IsElementInsideFrame(element, frame));
    }

    private bool TryGetPicturePoint(PointerRoutedEventArgs e, out Shim.SKPoint picturePoint)
    {
        picturePoint = default;
        var point = e.GetCurrentPoint(EditorSvg).Position;
        if (!EditorSvg.TryGetPicturePoint(point, out var mapped))
        {
            return false;
        }

        picturePoint = new Shim.SKPoint(mapped.X, mapped.Y);
        return true;
    }

    private void SetTool(ToolService.Tool tool)
    {
        CancelPendingSelectionPress();
        ResetSelectionClickCycle();
        CommitInlineTextEdit();

        if (_toolService.CurrentTool == ToolService.Tool.PathLine && tool != ToolService.Tool.PathLine)
        {
            if (IsVectorPathDrawing)
            {
                FinishVectorPath(cancelDrawing: false);
            }
            else if (_pathService.IsEditing)
            {
                _pathService.Stop();
            }
        }

        if (tool != ToolService.Tool.Comment)
        {
            CancelCommentDraft();
            SetCommentsInspectorActive(false);
        }

        _toolService.SetTool(tool);
        foreach (var item in ToolButtons)
        {
            item.IsSelected = item.Tool == tool;
        }

        foreach (var group in ToolGroups)
        {
            group.SyncSelection();
        }

        if (tool == ToolService.Tool.PathLine)
        {
            _pathService.CurrentSegmentTool = PathService.SegmentTool.Line;
            SyncPathEditingSelection();
        }

        if (tool == ToolService.Tool.Comment)
        {
            SetCommentsInspectorActive(true);
        }

        CurrentToolLabel = GetToolLabel(tool);
        RefreshComputedState();
        RefreshOverlay();
    }

    private void SetSidebarMode(bool showAssets)
    {
        if (_isAssetsView == showAssets)
        {
            return;
        }

        _isAssetsView = showAssets;
        RaisePropertyChanged(nameof(IsLayersViewActive));
        RaisePropertyChanged(nameof(IsAssetsViewActive));
        RaisePropertyChanged(nameof(LayersPanelVisibility));
        RaisePropertyChanged(nameof(AssetsPanelVisibility));
    }

    private void ToggleLeftPanel()
    {
        _isLeftPanelCollapsed = !_isLeftPanelCollapsed;
        RaisePropertyChanged(nameof(IsLeftPanelCollapsed));
        RaisePropertyChanged(nameof(UtilityRailColumnWidth));
        RaisePropertyChanged(nameof(SidebarColumnWidth));
        RaisePropertyChanged(nameof(LeftPanelVisibility));
    }

    private void SetInspectorMode(bool showPrototype)
    {
        if (_isPrototypeInspector == showPrototype && !_isCommentsInspector && !_isDevInspector)
        {
            return;
        }

        _isCommentsInspector = false;
        _isPrototypeInspector = showPrototype;
        _isDevInspector = false;
        RaisePropertyChanged(nameof(IsDesignInspectorActive));
        RaisePropertyChanged(nameof(IsPrototypeInspectorActive));
        RaisePropertyChanged(nameof(IsDevInspectorActive));
        RaisePropertyChanged(nameof(IsCommentsInspectorActive));
        RaisePropertyChanged(nameof(DesignInspectorVisibility));
        RaisePropertyChanged(nameof(PrototypeInspectorVisibility));
        RaisePropertyChanged(nameof(DevInspectorVisibility));
        RaisePropertyChanged(nameof(CommentsInspectorVisibility));
        RaisePropertyChanged(nameof(InspectorTabsVisibility));
        RaisePropertyChanged(nameof(InspectorSelectionVisibility));
    }

    private void ZoomAroundCenter(double factor)
    {
        var center = new Point(EditorSvg.ActualWidth / 2.0, EditorSvg.ActualHeight / 2.0);
        EditorSvg.ZoomToPoint(EditorSvg.Zoom * factor, center);
        RefreshComputedState();
        RefreshOverlay();
    }

    private void ZoomTo100Percent()
    {
        var center = new Point(EditorSvg.ActualWidth / 2.0, EditorSvg.ActualHeight / 2.0);
        EditorSvg.ZoomToPoint(1.0, center);
        RefreshComputedState();
        RefreshOverlay();
    }

    private void ZoomToPercent(double zoomPercent)
    {
        var center = new Point(EditorSvg.ActualWidth / 2.0, EditorSvg.ActualHeight / 2.0);
        var zoom = Math.Clamp(zoomPercent / 100.0, 0.05, 64.0);
        EditorSvg.ZoomToPoint(zoom, center);
        RefreshComputedState();
        RefreshOverlay();
    }

    private void ZoomToSelection()
    {
        if (!TryGetSelectionViewCenter(out var selectionCenter)
            || GetSelectionBounds() is not { } selectionBounds)
        {
            return;
        }

        if (!EditorSvg.TryGetViewPoint(new Shim.SKPoint(selectionBounds.Left, selectionBounds.Top), out var topLeft)
            || !EditorSvg.TryGetViewPoint(new Shim.SKPoint(selectionBounds.Right, selectionBounds.Bottom), out var bottomRight))
        {
            return;
        }

        var currentWidth = Math.Max(1.0, Math.Abs(bottomRight.X - topLeft.X));
        var currentHeight = Math.Max(1.0, Math.Abs(bottomRight.Y - topLeft.Y));
        var desiredWidth = Math.Max(160.0, EditorSvg.ActualWidth * 0.7);
        var desiredHeight = Math.Max(160.0, EditorSvg.ActualHeight * 0.7);
        var factor = Math.Min(desiredWidth / currentWidth, desiredHeight / currentHeight);
        var zoom = Math.Clamp(EditorSvg.Zoom * factor, 0.05, 64.0);

        EditorSvg.ZoomToPoint(zoom, selectionCenter);

        var canvasCenter = new Point(EditorSvg.ActualWidth / 2.0, EditorSvg.ActualHeight / 2.0);
        if (TryGetSelectionBoundsCenter(out var selectionPictureCenter)
            && EditorSvg.TryGetViewPoint(selectionPictureCenter, out var pinnedCenter))
        {
            EditorSvg.PanX += canvasCenter.X - pinnedCenter.X;
            EditorSvg.PanY += canvasCenter.Y - pinnedCenter.Y;
        }
        else
        {
            EditorSvg.PanX += canvasCenter.X - selectionCenter.X;
            EditorSvg.PanY += canvasCenter.Y - selectionCenter.Y;
        }

        RefreshComputedState();
        RefreshOverlay();
    }

    private void RotateSelection(float delta)
    {
        var items = GetSelectedItemsForAlignment();
        if (items.Count == 0 || EditorSvg.SkSvg is null)
        {
            return;
        }

        foreach (var (element, drawable) in items)
        {
            var center = SelectionService.GetLocalCenter(drawable);
            var angle = _selectionService.GetRotation(element) + delta;
            _selectionService.SetRotation(element, angle, center);
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        CanvasStatus = $"Rotated the selection by {delta:0.#}°.";
    }

    private async Task ShowInfoDialogAsync(string title, string message)
    {
        if (XamlRoot is null)
        {
            CanvasStatus = message.Replace('\n', ' ');
            return;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.WrapWholeWords,
                MaxWidth = 520
            },
            CloseButtonText = "Close",
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }

    private float GetCanvasScale()
    {
        if (!EditorSvg.TryGetViewMatrix(out var matrix))
        {
            return 1f;
        }

        return MathF.Max(0.0001f, MathF.Sqrt((matrix.M11 * matrix.M11) + (matrix.M12 * matrix.M12)));
    }

    private void StartMarqueeSelection(Point viewPoint, bool additive, bool toggle, bool preferFrameChildren = false)
    {
        ResetSelectionClickCycle();
        _isMarqueeSelecting = true;
        _marqueeAdditive = additive;
        _marqueeToggle = toggle;
        _marqueePreferFrameChildren = preferFrameChildren;
        _marqueeStartView = viewPoint;
        _marqueeCurrentView = viewPoint;
        RefreshOverlay();
    }

    private void CompleteMarqueeSelection()
    {
        _isMarqueeSelecting = false;
        EditorOverlay.Marquee = null;

        var marqueeRect = NormalizeRect(_marqueeStartView, _marqueeCurrentView);
        if (marqueeRect is null || marqueeRect.Value.Width < MarqueeThreshold || marqueeRect.Value.Height < MarqueeThreshold)
        {
            if (!_marqueeAdditive && !_marqueeToggle)
            {
                ApplySelection(Array.Empty<SvgVisualElement>(), null);
            }

            _marqueeAdditive = false;
            _marqueeToggle = false;
            _marqueePreferFrameChildren = false;
            RefreshOverlay();
            return;
        }

        if (EditorSvg.SkSvg is null
            || !EditorSvg.TryGetPicturePoint(new Point(marqueeRect.Value.Left, marqueeRect.Value.Top), out var startPoint)
            || !EditorSvg.TryGetPicturePoint(new Point(marqueeRect.Value.Right, marqueeRect.Value.Bottom), out var endPoint))
        {
            _marqueeAdditive = false;
            _marqueeToggle = false;
            _marqueePreferFrameChildren = false;
            RefreshOverlay();
            return;
        }

        var pictureRect = CreatePictureRect(startPoint, endPoint);
        var hits = CollectVisualHits(EditorSvg.SkSvg.HitTestElements(pictureRect));
        if (_marqueePreferFrameChildren)
        {
            hits = PruneFrameContainerMarqueeHits(hits);
        }

        if (_marqueeToggle)
        {
            var selection = _selectedElements.ToList();
            foreach (var hit in hits)
            {
                if (!selection.Remove(hit))
                {
                    selection.Add(hit);
                }
            }

            ApplySelection(selection, selection.LastOrDefault());
        }
        else if (_marqueeAdditive)
        {
            var selection = _selectedElements.ToList();
            foreach (var hit in hits)
            {
                if (!selection.Contains(hit))
                {
                    selection.Add(hit);
                }
            }

            ApplySelection(selection, selection.LastOrDefault());
        }
        else
        {
            ApplySelection(hits, hits.LastOrDefault());
        }

        _marqueeAdditive = false;
        _marqueeToggle = false;
        _marqueePreferFrameChildren = false;
        RefreshOverlay();
    }

    private List<SvgVisualElement> CollectVisualHits(IEnumerable<SvgElement> hits, bool includeLocked = false)
    {
        return hits
            .OfType<SvgVisualElement>()
            .Where(static element => !string.Equals(element.Display, "none", StringComparison.OrdinalIgnoreCase))
            .Select(PromoteFrameHit)
            .OfType<SvgVisualElement>()
            .Where(element => includeLocked || !IsElementLocked(element))
            .Distinct()
            .ToList();
    }

    private List<SvgVisualElement> PrioritizeFrameSelectionHits(List<SvgVisualElement> hits)
    {
        if (hits.Count < 2 || GetPrimaryFrameSelectionHit(hits) is not { } frame)
        {
            return hits;
        }

        var containsFrameChildren = hits.Any(hit => !ReferenceEquals(hit, frame) && IsElementInsideFrame(hit, frame));
        if (!containsFrameChildren)
        {
            return hits;
        }

        var reordered = new List<SvgVisualElement> { frame };
        foreach (var hit in hits)
        {
            if (!ReferenceEquals(hit, frame))
            {
                reordered.Add(hit);
            }
        }

        return reordered;
    }

    private List<SvgVisualElement> PruneFrameContainerMarqueeHits(List<SvgVisualElement> hits)
    {
        if (hits.Count < 2)
        {
            return hits;
        }

        return hits
            .Where(hit => hit is not SvgGroup frame
                || !IsFrameGroup(frame)
                || !hits.Any(other => !ReferenceEquals(other, hit) && IsElementInsideFrame(other, frame)))
            .ToList();
    }

    private static bool IsFrameBackgroundSelectionOnly(IReadOnlyList<SvgVisualElement> hits)
    {
        return hits.Count == 1
            && hits[0] is SvgGroup group
            && IsFrameGroup(group);
    }

    private static bool IsElementInsideFrame(SvgElement element, SvgGroup frame)
    {
        if (ReferenceEquals(element, frame))
        {
            return false;
        }

        return element.Parents.OfType<SvgGroup>().Any(parent => ReferenceEquals(parent, frame));
    }

    private SvgGroup? GetPrimaryFrameSelectionHit(IReadOnlyList<SvgVisualElement> hits)
    {
        foreach (var hit in hits)
        {
            if (hit is SvgGroup group && IsFrameGroup(group))
            {
                return group;
            }

            var parentFrame = hit.Parents
                .OfType<SvgGroup>()
                .FirstOrDefault(group => IsFrameGroup(group) && !IsElementLocked(group));
            if (parentFrame is not null)
            {
                return parentFrame;
            }
        }

        return null;
    }

    private void TranslateSelection(float dx, float dy)
    {
        foreach (var element in _selectedElements)
        {
            _selectionService.NormalizeWorldTranslation(element);
            var (tx, ty) = _selectionService.GetTranslation(element);
            _selectionService.SetTranslation(element, tx + dx, ty + dy);
        }
    }

    private List<(SvgVisualElement Element, DrawableBase Drawable)> GetSelectedItemsForAlignment()
    {
        var result = new List<(SvgVisualElement Element, DrawableBase Drawable)>();
        foreach (var element in _selectedElements)
        {
            var drawable = _selectedDrawables.FirstOrDefault(item => ReferenceEquals(item.Element, element));
            if (drawable is not null)
            {
                result.Add((element, drawable));
            }
        }

        return result;
    }

    private void RebuildRulers()
    {
        HorizontalRulerMarks.Clear();
        VerticalRulerMarks.Clear();
        HorizontalRulerMarkers.Clear();
        VerticalRulerMarkers.Clear();

        if (!AreRulersVisible)
        {
            return;
        }

        if (EditorSvg.ActualWidth <= 0.0 || EditorSvg.ActualHeight <= 0.0)
        {
            return;
        }

        if (!TryGetSvgOriginInCanvasHost(out var origin))
        {
            return;
        }

        if (!TryGetVisiblePictureBounds(out var left, out var top, out var right, out var bottom))
        {
            return;
        }

        var step = CalculateNiceRulerStep(72.0 / Math.Max(GetCanvasScale(), 0.0001f));
        BuildHorizontalRulerMarks(origin.X, left, right, step);
        BuildVerticalRulerMarks(origin.Y, top, bottom, step);
        BuildSelectionRulerMarkers(origin);
    }

    private Shim.SKRect? GetSelectionBounds()
    {
        if (_selectedDrawables.Count == 0)
        {
            return null;
        }

        var firstBounds = SelectionService.GetTransformedInteractiveBounds(_selectedDrawables[0]);
        var result = new Shim.SKRect(firstBounds.Left, firstBounds.Top, firstBounds.Right, firstBounds.Bottom);
        for (var index = 1; index < _selectedDrawables.Count; index++)
        {
            var bounds = SelectionService.GetTransformedInteractiveBounds(_selectedDrawables[index]);
            result = UnionRect(result, new Shim.SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
        }

        return result;
    }

    private static Shim.SKRect UnionRect(Shim.SKRect left, Shim.SKRect right)
    {
        return new Shim.SKRect(
            Math.Min(left.Left, right.Left),
            Math.Min(left.Top, right.Top),
            Math.Max(left.Right, right.Right),
            Math.Max(left.Bottom, right.Bottom));
    }

    private static Rect? NormalizeRect(Point start, Point end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var right = Math.Max(start.X, end.X);
        var bottom = Math.Max(start.Y, end.Y);

        if (double.IsNaN(left) || double.IsNaN(top) || double.IsNaN(right) || double.IsNaN(bottom))
        {
            return null;
        }

        return new Rect(left, top, right - left, bottom - top);
    }

    private static Shim.SKRect CreatePictureRect(Shim.SKPoint start, Shim.SKPoint end)
    {
        return new Shim.SKRect(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Max(start.X, end.X),
            Math.Max(start.Y, end.Y));
    }

    private static string FormatPaint(SvgPaintServer? paint)
    {
        return paint switch
        {
            null => "none",
            _ when ReferenceEquals(paint, SvgPaintServer.None) => "none",
            _ when ReferenceEquals(paint, SvgPaintServer.NotSet) => "inherit",
            _ when ReferenceEquals(paint, SvgPaintServer.Inherit) => "inherit",
            _ => paint.ToString() ?? paint.GetType().Name
        };
    }

    private static bool TryParseDouble(string? text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private static bool IsToggleModifier(VirtualKeyModifiers modifiers)
    {
        return modifiers.HasFlag(VirtualKeyModifiers.Control)
            || modifiers.HasFlag(VirtualKeyModifiers.Windows);
    }

    private bool IsVectorDoubleClick(Point viewPoint)
    {
        var now = Environment.TickCount64;
        var elapsed = now - _lastVectorClickTicks;
        var dx = viewPoint.X - _lastVectorClickViewPoint.X;
        var dy = viewPoint.Y - _lastVectorClickViewPoint.Y;
        var isDoubleClick = elapsed >= 0
            && elapsed <= VectorDoubleClickMilliseconds
            && Math.Sqrt((dx * dx) + (dy * dy)) <= VectorDoubleClickTolerancePixels;

        _lastVectorClickTicks = now;
        _lastVectorClickViewPoint = viewPoint;
        return isDoubleClick;
    }

    private static bool HasKeyboardModifierPressed()
    {
        return (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down
            || (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down
            || (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down
            || (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down
            || (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }

    private static bool IsPrimaryCommandModifierPressed()
    {
        return (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down
            || (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down
            || (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }

    private static bool IsAltPressed()
    {
        return (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu) & CoreVirtualKeyStates.Down)
            == CoreVirtualKeyStates.Down;
    }

    private static bool IsShiftPressed()
    {
        return (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down)
            == CoreVirtualKeyStates.Down;
    }

    private void BuildHorizontalRulerMarks(double offset, double left, double right, double step)
    {
        for (var value = RoundDownToStep(left, step); value <= RoundUpToStep(right, step); value += step)
        {
            if (!TryProjectHorizontal(value, offset, out var position))
            {
                continue;
            }

            HorizontalRulerMarks.Add(new RulerMark(FormatRulerValue(value), position, Math.Abs(value) < (step / 2.0) ? 16.0 : 12.0, Math.Abs(value) < (step / 2.0)));
        }
    }

    private void BuildVerticalRulerMarks(double offset, double top, double bottom, double step)
    {
        for (var value = RoundDownToStep(top, step); value <= RoundUpToStep(bottom, step); value += step)
        {
            if (!TryProjectVertical(value, offset, out var position))
            {
                continue;
            }

            VerticalRulerMarks.Add(new RulerMark(FormatRulerValue(value), position, Math.Abs(value) < (step / 2.0) ? 16.0 : 12.0, Math.Abs(value) < (step / 2.0)));
        }
    }

    private void BuildSelectionRulerMarkers(Point origin)
    {
        var selectionBounds = GetSelectionBounds();
        if (selectionBounds is null)
        {
            return;
        }

        if (TryProjectHorizontal(selectionBounds.Value.Left, origin.X, out var horizontalStart)
            && TryProjectHorizontal(selectionBounds.Value.Right, origin.X, out var horizontalEnd)
            && TryProjectHorizontal((selectionBounds.Value.Left + selectionBounds.Value.Right) / 2.0, origin.X, out var horizontalCenter))
        {
            HorizontalRulerMarkers.Add(
                new RulerMarker(
                    horizontalStart,
                    horizontalEnd,
                    horizontalCenter,
                    Math.Round(selectionBounds.Value.Width).ToString(CultureInfo.InvariantCulture)));
        }

        if (TryProjectVertical(selectionBounds.Value.Top, origin.Y, out var verticalStart)
            && TryProjectVertical(selectionBounds.Value.Bottom, origin.Y, out var verticalEnd)
            && TryProjectVertical((selectionBounds.Value.Top + selectionBounds.Value.Bottom) / 2.0, origin.Y, out var verticalCenter))
        {
            VerticalRulerMarkers.Add(
                new RulerMarker(
                    verticalStart,
                    verticalEnd,
                    verticalCenter,
                    Math.Round(selectionBounds.Value.Height).ToString(CultureInfo.InvariantCulture)));
        }
    }

    private bool TryGetVisiblePictureBounds(out double left, out double top, out double right, out double bottom)
    {
        left = 0.0;
        top = 0.0;
        right = 0.0;
        bottom = 0.0;

        if (!EditorSvg.TryGetPicturePoint(new Point(0.0, 0.0), out var start)
            || !EditorSvg.TryGetPicturePoint(new Point(EditorSvg.ActualWidth, EditorSvg.ActualHeight), out var end))
        {
            return false;
        }

        left = Math.Min(start.X, end.X);
        top = Math.Min(start.Y, end.Y);
        right = Math.Max(start.X, end.X);
        bottom = Math.Max(start.Y, end.Y);
        return true;
    }

    private bool TryGetSvgOriginInCanvasHost(out Point origin)
    {
        origin = default;

        if (EditorSvg.ActualWidth <= 0.0 || EditorSvg.ActualHeight <= 0.0)
        {
            return false;
        }

        try
        {
            origin = EditorSvg.TransformToVisual(CanvasHost).TransformPoint(new Point(0.0, 0.0));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryProjectHorizontal(double pictureX, double offset, out double position)
    {
        position = 0.0;
        if (!EditorSvg.TryGetViewPoint(new Shim.SKPoint((float)pictureX, 0f), out var localPoint))
        {
            return false;
        }

        position = offset + localPoint.X;
        return true;
    }

    private bool TryProjectVertical(double pictureY, double offset, out double position)
    {
        position = 0.0;
        if (!EditorSvg.TryGetViewPoint(new Shim.SKPoint(0f, (float)pictureY), out var localPoint))
        {
            return false;
        }

        position = offset + localPoint.Y;
        return true;
    }

    private static double CalculateNiceRulerStep(double targetValue)
    {
        if (targetValue <= 0.0)
        {
            return 10.0;
        }

        var exponent = Math.Floor(Math.Log10(targetValue));
        var fraction = targetValue / Math.Pow(10.0, exponent);
        var niceFraction = fraction switch
        {
            <= 1.0 => 1.0,
            <= 2.0 => 2.0,
            <= 5.0 => 5.0,
            _ => 10.0
        };

        return niceFraction * Math.Pow(10.0, exponent);
    }

    private static double RoundDownToStep(double value, double step)
    {
        return Math.Floor(value / step) * step;
    }

    private static double RoundUpToStep(double value, double step)
    {
        return Math.Ceiling(value / step) * step;
    }

    private static string FormatRulerValue(double value)
    {
        return Math.Round(value).ToString(CultureInfo.InvariantCulture);
    }

    private static DrawableBase? FindDrawable(DrawableBase drawable, SvgElement element)
    {
        if (ReferenceEquals(drawable.Element, element))
        {
            return drawable;
        }

        if (drawable is DrawableContainer container)
        {
            foreach (var child in container.ChildrenDrawables)
            {
                var result = FindDrawable(child, element);
                if (result is not null)
                {
                    return result;
                }
            }
        }

        return null;
    }

    private static void AppendDrawable(DrawableBase drawable, List<DrawableBase> drawables)
    {
        if (drawable.Element is SvgVisualElement element
            && !string.Equals(element.Display, "none", StringComparison.OrdinalIgnoreCase))
        {
            drawables.Add(drawable);
        }

        if (drawable is DrawableContainer container)
        {
            foreach (var child in container.ChildrenDrawables)
            {
                AppendDrawable(child, drawables);
            }
        }
    }

    private bool CanResize(SvgVisualElement element)
    {
        if (element is SvgTextBase text)
        {
            return IsAreaText(text) && HasTextBoxRect(text);
        }

        return element is SvgRectangle
        || element is SvgEllipse
        || IsFrameGroup(element)
        || element is global::Svg.SvgImage
        || element is SvgUse
        || element is SvgCircle
        || element is SvgLine
        || element is SvgPolyline
        || element is SvgPolygon
        || element is SvgPath;
    }

    private void EnsureElementId(SvgElement element)
    {
        EnsureElementId(element, _document);
    }

    private void EnsureElementId(SvgElement element, SvgDocument? document)
    {
        if (!string.IsNullOrWhiteSpace(element.ID)
            && !IsElementIdInUse(document, element.ID!, element))
        {
            return;
        }

        var prefix = SvgElementInfo.GetElementName(element.GetType()).Replace("-", string.Empty, StringComparison.Ordinal);
        element.ID = CreateUniqueId(prefix, document);
    }

    private static bool IsElementIdInUse(SvgDocument? document, string id, SvgElement? exclude = null)
    {
        if (document is null || string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (string.Equals(document.ID, id, StringComparison.Ordinal) && !ReferenceEquals(document, exclude))
        {
            return true;
        }

        return document.Descendants().OfType<SvgElement>().Any(existing =>
            !ReferenceEquals(existing, exclude)
            && string.Equals(existing.ID, id, StringComparison.Ordinal));
    }

    private string MakeUniqueElementId(string proposedId, SvgDocument? document, SvgElement? exclude = null)
    {
        var sanitized = SanitizeElementId(proposedId, "layer");
        if (!IsElementIdInUse(document, sanitized, exclude))
        {
            return sanitized;
        }

        var suffix = 2;
        string candidate;
        do
        {
            candidate = $"{sanitized}-{suffix++}";
        } while (IsElementIdInUse(document, candidate, exclude));

        return candidate;
    }

    private static string SanitizeElementId(string text, string fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        var builder = new System.Text.StringBuilder(text.Length);
        var lastWasSeparator = false;
        foreach (var character in text.Trim())
        {
            if (char.IsLetterOrDigit(character) || character is '_' or '-')
            {
                builder.Append(character);
                lastWasSeparator = false;
            }
            else if (char.IsWhiteSpace(character) || character is '.' or ':' or '/')
            {
                if (!lastWasSeparator)
                {
                    builder.Append('-');
                    lastWasSeparator = true;
                }
            }
        }

        var sanitized = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }

    private static string GetToolLabel(ToolService.Tool tool)
    {
        return SvgEditorToolCatalog.GetLabel(tool);
    }

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }

    private static bool IsMiddlePointerPanPress(PointerPointProperties properties)
    {
        return properties.IsMiddleButtonPressed
            || properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonPressed;
    }

    private static bool IsMiddlePointerPanRelease(PointerPointProperties properties)
    {
        return properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonReleased;
    }

    private bool TryPromoteMiddlePointerPan(Point hostPoint, PointerPointProperties properties)
    {
        if (_isPanning
            || !properties.IsMiddleButtonPressed
            || _isDragging
            || _isResizing
            || _isRotating
            || _isCreating
            || _pathService.ActivePoint >= 0
            || IsVectorPathDrawing)
        {
            return false;
        }

        var canceledSelectionGesture = false;
        if (_pendingSelectionPress)
        {
            CancelPendingSelectionPress();
            canceledSelectionGesture = true;
        }

        if (_isMarqueeSelecting)
        {
            CancelMarqueeSelection();
            canceledSelectionGesture = true;
        }

        if (!canceledSelectionGesture)
        {
            return false;
        }

        ResetSelectionClickCycle();
        _isPanning = true;
        _panStart = hostPoint;
        RefreshOverlay();
        return true;
    }

    private void CancelMarqueeSelection()
    {
        if (!_isMarqueeSelecting)
        {
            return;
        }

        _isMarqueeSelecting = false;
        EditorOverlay.Marquee = null;
        _marqueeAdditive = false;
        _marqueeToggle = false;
        _marqueePreferFrameChildren = false;
        RefreshOverlay();
    }

    private void EndCanvasPan()
    {
        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        RefreshComputedState();
    }

    private const string DefaultSvgDocument =
        """
        <svg width="1440" height="960" viewBox="0 0 1440 960" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="panel" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#ffffff" />
              <stop offset="100%" stop-color="#f5f2ee" />
            </linearGradient>
            <linearGradient id="purple" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#8b2cf5" />
              <stop offset="100%" stop-color="#6122d2" />
            </linearGradient>
          </defs>
          <g id="desktop-1">
            <rect id="frame" x="96" y="54" width="1160" height="796" rx="0" fill="url(#panel)" stroke="#d7d8dd" stroke-width="1.5" />
            <rect id="header-guide" x="96" y="78" width="1160" height="2" fill="#ff9e95" />
            <rect id="middle-guide" x="96" y="352" width="1160" height="2" fill="#ff9e95" />
            <rect id="left-guide" x="362" y="54" width="2" height="796" fill="#ff9e95" />
            <g id="text-block">
              <rect id="thumb" x="286" y="176" width="58" height="58" fill="#d6d7da" />
              <text id="heading-1" x="350" y="158" font-size="22" fill="#2a2a2a" font-family="Open Sans">dxgdfg</text>
              <text id="heading-2" x="350" y="198" font-size="22" fill="#2a2a2a" font-family="Open Sans">dfg</text>
              <text id="heading-3" x="350" y="238" font-size="22" fill="#2a2a2a" font-family="Open Sans">sdgdg</text>
              <text id="heading-4" x="350" y="278" font-size="22" fill="#2a2a2a" font-family="Open Sans">dgdg</text>
            </g>
            <g id="frame-3">
              <rect id="card" x="930" y="162" width="210" height="336" fill="#d3d0cf" />
              <rect id="card-accent" x="970" y="198" width="72" height="78" fill="#ff2f3d" />
              <rect id="card-panel" x="970" y="322" width="124" height="76" fill="url(#purple)" />
              <rect id="card-footer" x="970" y="444" width="58" height="30" fill="#d23434" />
            </g>
            <g id="token-row">
              <rect id="token-square" x="404" y="486" width="44" height="44" fill="#d7d7da" />
              <g id="bag-green">
                <path id="bag-green-handle" d="M346 586 C346 570 358 558 374 558 C390 558 402 570 402 586" fill="none" stroke="#50b86f" stroke-width="6" />
                <rect id="bag-green-body" x="334" y="574" width="80" height="56" rx="8" fill="none" stroke="#50b86f" stroke-width="6" />
                <circle id="bag-green-lock" cx="374" cy="602" r="4" fill="#50b86f" />
              </g>
              <g id="bag-yellow">
                <path id="bag-yellow-handle" d="M424 586 C424 570 436 558 452 558 C468 558 480 570 480 586" fill="none" stroke="#f3cd53" stroke-width="6" />
                <rect id="bag-yellow-body" x="412" y="580" width="80" height="50" rx="8" fill="none" stroke="#f3cd53" stroke-width="6" />
                <circle id="bag-yellow-lock" cx="452" cy="604" r="4" fill="#f3cd53" />
              </g>
              <ellipse id="dashed-ellipse" cx="560" cy="600" rx="42" ry="26" fill="none" stroke="#58c77f" stroke-width="4" stroke-dasharray="5 6" />
            </g>
            <g id="icons">
              <circle id="arrow-up-ring" cx="432" cy="742" r="46" fill="none" stroke="#111111" stroke-width="8" />
              <path id="arrow-up" d="M404 752 L432 726 L460 752" fill="none" stroke="#111111" stroke-width="8" stroke-linecap="round" stroke-linejoin="round" />
              <circle id="arrow-down-ring" cx="612" cy="742" r="46" fill="none" stroke="#111111" stroke-width="8" />
              <path id="arrow-down" d="M584 730 L612 756 L640 730" fill="none" stroke="#111111" stroke-width="8" stroke-linecap="round" stroke-linejoin="round" />
            </g>
            <g id="btc-mark">
              <rect id="btc-frame" x="170" y="316" width="90" height="90" fill="none" stroke="#f1c75b" stroke-width="3" />
              <circle id="btc-ring" cx="216" cy="362" r="42" fill="none" stroke="#f1c75b" stroke-width="4" stroke-dasharray="3 4" />
              <text id="btc-text" x="200" y="378" font-size="46" fill="#f1c75b" font-family="Open Sans">₿</text>
            </g>
          </g>
        </svg>
        """;
}
