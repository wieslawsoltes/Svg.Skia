using System.Diagnostics;
using System.Threading;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Svg.Editor.Core;
using Svg.Editor.Svg;
using Svg.Pathing;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.System;
using Shim = ShimSkiaSharp;

namespace Svg.Editor.Skia.Uno;

/// <summary>
/// SVG editing through Uno's Skia canvas element. The host supplies a Skia canvas or
/// a compatibility bridge for another compositor; document pictures are reused
/// through camera changes and pointer previews.
/// </summary>
public sealed partial class SvgEditorCanvas : SKCanvasElement, ISkiaCanvasRenderer, IDisposable
{
    private enum Gesture { None, Pan, Pinch, Marquee, Move, Resize, Rotate, Create, Node, Vertex, Transform, Zoom, Gradient, Artboard, EditorShape, AreaType, Scissors, CutSweep, Lasso, Anchor, ShapeBuilder, Deformation, SymbolSpray, SymbolBrush, PathRefinement, BlobStroke, Measure }

    private readonly record struct PenNode(SKPoint Position, SKPoint Tangent);

    private static readonly InputSystemCursor s_arrow = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
    private static readonly InputSystemCursor s_cross = InputSystemCursor.Create(InputSystemCursorShape.Cross);
    private static readonly InputSystemCursor s_hand = InputSystemCursor.Create(InputSystemCursorShape.Hand);

    private readonly List<SKPoint> _freehandPoints = new();
    private readonly List<PenNode> _penNodes = new();
    private readonly SvgEditorTouchNavigation _touchNavigation = new();
    private readonly SvgEditorArtboardCache _artboardCache = new();
    private readonly PathService _nodePreview = new();
    private SvgEditorController? _controller;
    private SvgEditorToolKind _tool;
    private Gesture _gesture;
    private uint? _activePointer;
    private PointerDeviceType? _activePointerDevice;
    private bool _suppressTouchDoubleTap;
    private SKPoint _startView;
    private SKPoint _lastView;
    private SKPoint _startWorld;
    private SKPoint _currentWorld;
    private SKRect _startBounds;
    private SKMatrix _previewTransform = SKMatrix.Identity;
    private SKPathBuilder? _freehandPreview;
    private SKPath? _nodeOutline;
    private SvgPath? _nodeOriginal;
    private int _nodeIndex = -1;
    private int _handle = -1;
    private bool _moved;
    private bool _shift;
    private bool _additive;
    private bool _panMode;
    private bool _showGrid;
    private bool _showTransparency;
    private bool _isDarkTheme;
    private bool _isLoaded;
    private bool _needsFit = true;
    private bool _disposed;
    private CanvasResources? _resources;
    private readonly InteractionSurface _interactionSurface;
    private double _lastDocumentRenderMilliseconds;
    private double _lastInteractionRenderMilliseconds;
    private int _fullInvalidatePending;
    private bool _interactionRenderPending = true;
    private bool _hasDocumentRenderState;
    private DocumentRenderState _documentRenderState;

    private readonly record struct DocumentRenderState(SvgEditorRenderStamp Content,
        SKMatrix Camera, double Width, double Height, bool Grid, double GridSize, bool Transparency, bool DarkTheme);

    private DocumentRenderState CaptureDocumentRenderState() => new(SvgEditorRenderStamp.Capture(Controller),
        Viewport.Matrix, Viewport.Width, Viewport.Height, ShowGrid, Controller?.Selection.GridSize ?? 10,
        ShowTransparency, IsDarkTheme);

    public SvgEditorCanvas()
    {
        // Own content remains the document layer. A single child paints transient
        // editing feedback after it, while leaving the document recording intact.
        _interactionSurface = new InteractionSurface(this) { IsHitTestVisible = false };
        // A compositor bridge can append its document island after construction.
        // Keep interaction feedback above that host-owned content as well.
        Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(_interactionSurface, 1);
        Children.Add(_interactionSurface);

        // Uno 7 uses a border visual; a brush supplies its hit-test geometry.
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        // The canvas handles touch directly; parent scrollers must not take over.
        ManipulationMode = ManipulationModes.None;
        Viewport.Changed += OnViewportChanged;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerExited += (_, _) => { _brushPointerInside = false; InvalidateInteraction(); };
        PointerReleased += OnPointerReleased;
        PointerCanceled += OnPointerCanceled;
        PointerCaptureLost += OnPointerCaptureLost;
        PointerWheelChanged += OnPointerWheelChanged;
        DoubleTapped += OnDoubleTapped;
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        LostFocus += (_, _) => RestoreStandaloneNavigation();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
        ActualThemeChanged += OnCanvasThemeChanged;
    }

    public event EventHandler? ViewportChanged;
    public event EventHandler? PointerPositionChanged;
    /// <summary>Raised before pointer capture or selection changes, so a host can commit pending edits.</summary>
    public event EventHandler? InteractionStarting;
    public event EventHandler<SvgEditorInteractionFailedEventArgs>? InteractionFailed;

    public SvgEditorViewport Viewport { get; } = new();
    public double Zoom => Viewport.Zoom;
    public double ZoomPercent => Viewport.Zoom * 100;
    public SKPoint PointerPosition { get; private set; }
    /// <summary>
    /// Sum of the most recent document and interaction preparation times.
    /// Raster uploads and compositor presentation are measured by the host.
    /// </summary>
    public double LastRenderMilliseconds => _lastDocumentRenderMilliseconds + _lastInteractionRenderMilliseconds;

    /// <summary>Invalidates the document and its interaction feedback together.</summary>
    public new void Invalidate()
    {
        if (_disposed)
            return;
        // Match the base API's cross-thread guarantee while marking both layers
        // together on the UI thread, rather than posting independent updates.
        if (DispatcherQueue is { } dispatcher && !dispatcher.HasThreadAccess)
        {
            if (Interlocked.Exchange(ref _fullInvalidatePending, 1) == 0 && !dispatcher.TryEnqueue(() =>
            {
                Volatile.Write(ref _fullInvalidatePending, 0);
                Invalidate();
            }))
                Volatile.Write(ref _fullInvalidatePending, 0);
            return;
        }
        base.Invalidate();
        InvalidateInteraction();
    }

    private void InvalidateInteraction()
    {
        if (!_disposed)
        {
            _interactionRenderPending = true;
            _interactionSurface.Invalidate();
        }
    }

    public SvgEditorController? Controller
    {
        get => _controller;
        set
        {
            if (ReferenceEquals(_controller, value))
            {
                return;
            }

            CancelGesture();
            _penNodes.Clear();
            if (_controller is not null)
            {
                _controller.Changed -= OnControllerChanged;
            }

            _controller = value;
            _artboardCache.Clear();
            if (_controller is not null && _isLoaded)
            {
                _controller.Changed += OnControllerChanged;
            }

            _needsFit = true;
            RefreshNodePreview();
            FitDocument();
            Invalidate();
        }
    }

    public SvgEditorToolKind Tool
    {
        get => _tool;
        set
        {
            if (_tool == value)
            {
                return;
            }

            FinishPath();
            CancelGesture();
            _tool = value;
            RefreshNodePreview();
            UpdateCursor();
            InvalidateInteraction();
        }
    }

    public bool PanMode
    {
        get => _panMode;
        set { _panMode = value; UpdateCursor(); }
    }

    public bool ShowGrid
    {
        get => _showGrid;
        set { _showGrid = value; Invalidate(); }
    }

    public bool ShowTransparency
    {
        get => _showTransparency;
        set { _showTransparency = value; Invalidate(); }
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set { _isDarkTheme = value; Invalidate(); }
    }

    public void FitDocument()
    {
        if (Controller?.Renderer.Picture is not { } picture || Viewport.Width <= 0 || Viewport.Height <= 0)
        {
            _needsFit = true;
            return;
        }

        Viewport.Fit(Controller.GetArtboardsBounds());
        _needsFit = false;
    }

    public void FitActiveArtboard()
    {
        if (Controller?.ActiveArtboard is { } artboard && Viewport.Width > 0 && Viewport.Height > 0)
        {
            Viewport.Fit(artboard.Bounds);
            _needsFit = false;
        }
        else
            FitDocument();
    }

    public void FitSelection()
    {
        if (Controller?.SelectionBounds is { } bounds)
        {
            Viewport.Fit(bounds, 72, 16);
        }
        else
        {
            FitDocument();
        }
    }

    public void ZoomIn() => Viewport.ZoomAt(1.25, ViewCenter);
    public void ZoomOut() => Viewport.ZoomAt(0.8, ViewCenter);
    public void ResetZoom() => Viewport.ResetZoom();

    public void CancelGesture()
    {
        CancelDrawingPreview();
        _touchNavigation.Reset();
        ReleasePointerCaptures();
        RefreshNodePreview();
        UpdateCursor();
        InvalidateInteraction();
    }

    private void CancelDrawingPreview()
    {
        ClearPathUtilityPreview();
        ClearAdvancedBrushPreview();
        ClearAnchorPreview();
        CancelGuidePreview();
        // Tools mutate only their preview until release. Clearing it cancels the
        // entire provisional stroke/path without adding a document undo entry.
        ReleaseSelectionGhost();
        _gesture = Gesture.None;
        _activePointer = null;
        _activePointerDevice = null;
        _moved = false;
        _previewTransform = SKMatrix.Identity;
        _freehandPoints.Clear();
        _freehandPreview?.Reset();
        _penNodes.Clear();
    }

    /// <summary>Commits an open pen or polygon path. Escape discards the provisional path.</summary>
    public void FinishPath() => CommitPenPath(false);

    private void OnControllerChanged(object? sender, EventArgs e)
    {
        if (_movingGuideId is not null || _gesture is Gesture.Move or Gesture.Resize or Gesture.Rotate or Gesture.Node or Gesture.Vertex
            or Gesture.Transform or Gesture.Gradient or Gesture.Artboard or Gesture.Scissors or Gesture.CutSweep or Gesture.Lasso or Gesture.Anchor
            or Gesture.ShapeBuilder or Gesture.Deformation or Gesture.SymbolSpray or Gesture.SymbolBrush
            or Gesture.PathRefinement or Gesture.BlobStroke or Gesture.Measure)
        {
            // A command or selection change invalidates the gesture's retained snapshot.
            CancelGesture();
        }
        else
        {
            ReleaseSelectionGhost();
        }

        RefreshNodePreview();
        if (_needsFit)
        {
            FitDocument();
        }

        if (_hasDocumentRenderState && _documentRenderState == CaptureDocumentRenderState())
            InvalidateInteraction();
        else
            Invalidate();
    }

    private void OnViewportChanged(object? sender, EventArgs e)
    {
        Invalidate();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Viewport.Resize(e.NewSize.Width, e.NewSize.Height);
        if (_needsFit)
        {
            FitDocument();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_disposed || _isLoaded)
        {
            return;
        }

        _isLoaded = true;
        if (Controller is not null)
        {
            Controller.Changed += OnControllerChanged;
        }

        Viewport.Resize(ActualWidth, ActualHeight);
        if (_needsFit)
        {
            FitDocument();
        }

        RefreshNodePreview();
        UpdateCursor();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        RestoreStandaloneNavigation();
        _isLoaded = false;
        if (Controller is not null)
        {
            Controller.Changed -= OnControllerChanged;
        }

        CancelGesture();
        ReleaseResources();
    }

    private void UpdateCursor() => ProtectedCursor = PanMode || _gesture is Gesture.Pan or Gesture.Pinch
        ? s_hand : InteractionMode != SvgEditorInteractionMode.None || ZoomMode ? s_cross
        : IsSelectionTool(Tool) ? s_arrow : s_cross;

    private void HandleInteractionFailure(Exception exception)
    {
        ClearPathUtilityPreview();
        ClearAdvancedBrushPreview();
        ClearAnchorPreview();
        CancelGuidePreview();
        // Controller commands roll back their own transactions. Clear only transient canvas
        // state here; rebuilding a broken node preview while handling failure could throw again.
        ReleaseSelectionGhost();
        _gesture = Gesture.None;
        _activePointer = null;
        _activePointerDevice = null;
        _moved = false;
        _previewTransform = SKMatrix.Identity;
        _penNodes.Clear();
        _touchNavigation.Reset();
        _freehandPoints.Clear();
        _freehandPreview?.Reset();
        _nodePreview.Stop();
        ClearVertexPreview();
        _nodeOriginal = null;
        _nodeOutline?.Dispose();
        _nodeOutline = null;
        ReleasePointerCaptures();
        UpdateCursor();
        InvalidateInteraction();
        InteractionFailed?.Invoke(this, new SvgEditorInteractionFailedEventArgs(exception));
    }

    private static bool IsSelectionTool(SvgEditorToolKind tool) => tool is SvgEditorToolKind.Select
        or SvgEditorToolKind.MultiSelect or SvgEditorToolKind.PathSelect
        or SvgEditorToolKind.PolygonSelect or SvgEditorToolKind.PolylineSelect;

    private static bool IsNodeTool(SvgEditorToolKind tool) => tool is SvgEditorToolKind.PathSelect
        or SvgEditorToolKind.PolygonSelect or SvgEditorToolKind.PolylineSelect;

    private bool CanEditSelection => Controller is { SelectedElements.Count: > 0 } controller
        && controller.SelectedElements.All(element => !SvgEditorController.IsEditLocked(element));

    private static bool IsPenTool(SvgEditorToolKind tool) => tool is SvgEditorToolKind.PathLine
        or SvgEditorToolKind.PathCubic or SvgEditorToolKind.PathQuadratic or SvgEditorToolKind.PathArc
        or SvgEditorToolKind.PathMove or SvgEditorToolKind.Polygon or SvgEditorToolKind.Polyline;

    private static SKPoint GetHandle(SKRect bounds, int index) => index switch
    {
        0 => new(bounds.Left, bounds.Top),
        1 => new(bounds.MidX, bounds.Top),
        2 => new(bounds.Right, bounds.Top),
        3 => new(bounds.Right, bounds.MidY),
        4 => new(bounds.Right, bounds.Bottom),
        5 => new(bounds.MidX, bounds.Bottom),
        6 => new(bounds.Left, bounds.Bottom),
        _ => new(bounds.Left, bounds.MidY)
    };

    private static SKRect RectBetween(SKPoint a, SKPoint b) => new(
        Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));

    private static SKPoint Add(SKPoint a, SKPoint b) => new(a.X + b.X, a.Y + b.Y);
    private static SKPoint Subtract(SKPoint a, SKPoint b) => new(a.X - b.X, a.Y - b.Y);
    private static System.Drawing.PointF PointF(SKPoint point) => new(point.X, point.Y);
    private static SKMatrix ToSkia(Shim.SKMatrix matrix) => new(matrix.ScaleX, matrix.SkewX, matrix.TransX,
        matrix.SkewY, matrix.ScaleY, matrix.TransY, matrix.Persp0, matrix.Persp1, matrix.Persp2);

    public new void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CancelGesture();
        _disposed = true;
        if (Controller is not null)
        {
            Controller.Changed -= OnControllerChanged;
        }

        Viewport.Changed -= OnViewportChanged;
        ActualThemeChanged -= OnCanvasThemeChanged;
        _canvasBrushSubscriptions?.Dispose();
        _canvasBrushSubscriptions = null;
        _controller = null;
        ReleaseResources();
        Children.Remove(_interactionSurface);
        _interactionSurface.Dispose();
        base.Dispose();
    }

    private void ReleaseResources()
    {
        _hasDocumentRenderState = false;
        _documentRenderState = default;
        _artboardCache.Clear();
        ReleaseSelectionGhost();
        _nodePreview.Stop();
        ClearVertexPreview();
        _nodeOriginal = null;
        _resources?.Dispose();
        _resources = null;
        _freehandPreview?.Dispose();
        _freehandPreview = null;
        _nodeOutline?.Dispose();
        _nodeOutline = null;
    }

}
