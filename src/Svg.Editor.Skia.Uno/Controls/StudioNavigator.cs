using System.Threading;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Svg.Editor.Skia;
using Svg.Skia;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Rectangle = Microsoft.UI.Xaml.Shapes.Rectangle;

namespace Svg.Editor.Skia.Uno;

/// <summary>
/// A live overview that borrows the active renderer and camera. Navigating never
/// parses, clones, disposes, or edits the document or its shared render resources.
/// </summary>
public sealed class StudioNavigator : UserControl, IDisposable
{
    public static readonly DependencyProperty BackgroundBrushProperty = RegisterNavigatorBrush(nameof(BackgroundBrush));
    public static readonly DependencyProperty OutlineBrushProperty = RegisterNavigatorBrush(nameof(OutlineBrush));
    public static readonly DependencyProperty AccentBrushProperty = RegisterNavigatorBrush(nameof(AccentBrush));
    private static readonly InputSystemCursor s_hand = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    private static int s_nextTraceId;
    private sealed class NavigatorSurface(StudioNavigator owner) : SKCanvasElement
    {
        protected override void RenderOverride(SKCanvas canvas, Size area) => owner.Render(canvas, area);
    }

    private readonly record struct Overview(SKRect Bounds, float Scale, float X, float Y)
    {
        public SKPoint ToWorld(Point point) => new((float)((point.X - X) / Scale + Bounds.Left), (float)((point.Y - Y) / Scale + Bounds.Top));
        public SKRect ToView(SKRect bounds) => new(
            X + (bounds.Left - Bounds.Left) * Scale, Y + (bounds.Top - Bounds.Top) * Scale,
            X + (bounds.Right - Bounds.Left) * Scale, Y + (bounds.Bottom - Bounds.Top) * Scale);
    }

    private readonly NavigatorSurface _surface;
    private readonly Border _frame;
    private readonly Canvas _viewportOverlay;
    private readonly Rectangle _viewportIndicator;
    private readonly RectangleGeometry _viewportClip = new();
    private readonly SolidColorBrush _viewportFill = new();
    private readonly SolidColorBrush _viewportStroke = new();
    private readonly SvgEditorArtboardCache _artboardCache = new();
    private readonly SKPaint _paint = new() { IsAntialias = true };
    private readonly SolidColorBrush _fallbackOutlineBrush = new();
    private StudioBrushSubscriptions? _brushSubscriptions;
    private SvgEditorController? _controller;
    private SKSvg? _subscribedRenderer;
    private SvgEditorViewport? _viewport;
    private uint? _pointer;
    private SKPoint _dragOffset;
    private SKPoint _dragOrigin;
    private SKColor _background, _border, _accent;
    private bool _observingBrushes;
    private bool _disposed;
    private readonly int _traceId = Interlocked.Increment(ref s_nextTraceId);
    private int _overviewRenderCount;

    public StudioNavigator()
    {
        IsTabStop = true;
        UseSystemFocusVisuals = true;
        MinHeight = 120;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        ManipulationMode = ManipulationModes.None;
        ProtectedCursor = s_hand;
        _surface = new NavigatorSurface(this) { IsHitTestVisible = false };
        _viewportIndicator = new Rectangle
        {
            IsHitTestVisible = false, StrokeThickness = 1.5, Stroke = _viewportStroke,
            Fill = _viewportFill, UseLayoutRounding = false, Visibility = Visibility.Collapsed
        };
        _viewportOverlay = new Canvas { IsHitTestVisible = false, UseLayoutRounding = false, Clip = _viewportClip };
        _viewportOverlay.Children.Add(_viewportIndicator);
        var layers = new Grid();
        layers.Children.Add(_surface);
        layers.Children.Add(_viewportOverlay);
        _frame = new Border { BorderThickness = new Thickness(1), Child = layers };
        Content = _frame;
        AutomationProperties.SetName(this, "Document navigator");
        AutomationProperties.SetHelpText(this, "Click or drag to move the view. Arrows pan, plus and minus zoom, Home fits all artboards, and Escape cancels a drag.");
        ToolTipService.SetToolTip(this, "Click or drag to move the view · scroll to zoom · Home fits all artboards");
        ApplyPalette();
        ActualThemeChanged += OnThemeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _surface.SizeChanged += OnSurfaceSizeChanged;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCanceled += OnPointerCanceled;
        PointerCaptureLost += OnPointerCaptureLost;
        PointerWheelChanged += OnPointerWheelChanged;
        KeyDown += OnKeyDown;
    }

    public event EventHandler? NavigationStarting;
    public event EventHandler? ViewChanged;
    public event EventHandler<SvgEditorInteractionFailedEventArgs>? NavigationFailed;

    /// <summary>The overview background; null uses the standalone light/dark palette.</summary>
    public Brush? BackgroundBrush { get => (Brush?)GetValue(BackgroundBrushProperty); set => SetValue(BackgroundBrushProperty, value); }
    /// <summary>The frame and inactive-artboard outline brush.</summary>
    public Brush? OutlineBrush { get => (Brush?)GetValue(OutlineBrushProperty); set => SetValue(OutlineBrushProperty, value); }
    /// <summary>The active-artboard and viewport brush, including its opacity.</summary>
    public Brush? AccentBrush { get => (Brush?)GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    private static DependencyProperty RegisterNavigatorBrush(string name)
        => DependencyProperty.Register(name, typeof(Brush), typeof(StudioNavigator), new PropertyMetadata(null, OnNavigatorBrushChanged));

    private static void OnNavigatorBrushChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var navigator = (StudioNavigator)sender;
        if (navigator._disposed)
            return;
        if (navigator._observingBrushes)
            (navigator._brushSubscriptions ??= new StudioBrushSubscriptions(navigator.RefreshPalette)).Observe(args.Property, args.NewValue as Brush);
        navigator.RefreshPalette();
    }

    private void ObserveBrushes()
    {
        var subscriptions = _brushSubscriptions ??= new StudioBrushSubscriptions(RefreshPalette);
        subscriptions.Observe(BackgroundBrushProperty, BackgroundBrush);
        subscriptions.Observe(OutlineBrushProperty, OutlineBrush);
        subscriptions.Observe(AccentBrushProperty, AccentBrush);
    }

    private void RefreshPalette()
    {
        if (_disposed)
            return;
        ApplyPalette();
        UpdateViewportIndicator();
        _surface?.Invalidate();
    }

    public void Bind(SvgEditorController? controller, SvgEditorViewport? viewport)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ReferenceEquals(controller, _controller) && ReferenceEquals(viewport, _viewport))
        {
            SubscribeRenderer();
            IsEnabled = controller?.Document is not null && viewport is not null;
            return;
        }
        EndDrag(false);
        Unsubscribe();
        _controller = controller;
        _viewport = viewport;
        _artboardCache.Clear();
        if (_controller is not null)
        {
            _controller.Changed += OnSourceChanged;
            SubscribeRenderer();
        }
        if (_viewport is not null)
            _viewport.Changed += OnViewportChanged;
        IsEnabled = controller?.Document is not null && viewport is not null;
        UpdateViewportIndicator();
        _surface.Invalidate();
    }

    private void Unsubscribe()
    {
        if (_controller is not null)
            _controller.Changed -= OnSourceChanged;
        if (_subscribedRenderer is not null)
            _subscribedRenderer.AnimationInvalidated -= OnSourceChanged;
        _subscribedRenderer = null;
        if (_viewport is not null)
            _viewport.Changed -= OnViewportChanged;
    }

    private void SubscribeRenderer()
    {
        var renderer = _controller?.Renderer;
        if (ReferenceEquals(renderer, _subscribedRenderer))
            return;
        if (_subscribedRenderer is not null)
            _subscribedRenderer.AnimationInvalidated -= OnSourceChanged;
        _subscribedRenderer = renderer;
        if (_subscribedRenderer is not null)
            _subscribedRenderer.AnimationInvalidated += OnSourceChanged;
    }

    private void ApplyPalette()
    {
        // EditorTheme supplies scoped ThemeResource brushes. A standalone control
        // remains usable without merging a local dictionary over its host's palette.
        var dark = ActualTheme == ElementTheme.Dark;
        _background = StudioBrushSubscriptions.Color(BackgroundBrush, dark ? new SKColor(30, 30, 30) : new SKColor(186, 186, 186));
        _border = StudioBrushSubscriptions.Color(OutlineBrush, dark ? new SKColor(93, 93, 93) : new SKColor(166, 166, 166));
        _accent = StudioBrushSubscriptions.Color(AccentBrush, dark ? new SKColor(148, 191, 242) : new SKColor(40, 98, 176));
        _viewportStroke.Color = Windows.UI.Color.FromArgb(_accent.Alpha, _accent.Red, _accent.Green, _accent.Blue);
        _viewportFill.Color = Windows.UI.Color.FromArgb((byte)Math.Round(_accent.Alpha * (28d / 255d)), _accent.Red, _accent.Green, _accent.Blue);
        if (_frame is not null)
        {
            if (OutlineBrush is SolidColorBrush solid)
                _frame.BorderBrush = solid;
            else
            {
                _fallbackOutlineBrush.Color = Windows.UI.Color.FromArgb(_border.Alpha, _border.Red, _border.Green, _border.Blue);
                _frame.BorderBrush = _fallbackOutlineBrush;
            }
            // The Skia surface owns the background. Painting it again on the frame
            // would composite a translucent background brush twice.
        }
    }

    private bool TryOverview(double width, double height, out Overview overview)
    {
        overview = default;
        if (_controller?.Renderer.Picture is not { } picture || width <= 16 || height <= 16)
            return false;
        var bounds = _artboardCache.GetBounds(_controller);
        if (bounds.IsEmpty)
            bounds = picture.CullRect;
        if (!float.IsFinite(bounds.Left) || !float.IsFinite(bounds.Top)
            || !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height) || bounds.Width <= 0 || bounds.Height <= 0)
            return false;
        var scale = (float)Math.Min((width - 16) / bounds.Width, (height - 16) / bounds.Height);
        if (!float.IsFinite(scale) || scale <= 0)
            return false;
        overview = new Overview(bounds, scale, (float)(width - bounds.Width * scale) / 2, (float)(height - bounds.Height * scale) / 2);
        return true;
    }

    private void Render(SKCanvas canvas, Size area)
    {
        if (_disposed)
            return;
        _overviewRenderCount++;
        if (EditorInputTrace.Enabled)
            EditorInputTrace.Write($"navigator-overview-render id={_traceId} count={_overviewRenderCount}");
        using var restore = new SKAutoCanvasRestore(canvas);
        canvas.ClipRect(SKRect.Create((float)area.Width, (float)area.Height));
        canvas.Clear(_background);
        if (!TryOverview(area.Width, area.Height, out var overview) || _controller is null)
            return;
        var page = overview.ToView(overview.Bounds);
        var artboards = _artboardCache.GetArtboards(_controller);
        _paint.Style = SKPaintStyle.Fill;
        _paint.Color = SKColors.White;
        for (var index = 0; index < artboards.Count; index++)
            canvas.DrawRect(overview.ToView(artboards[index].Bounds), _paint);
        using (var documentRestore = new SKAutoCanvasRestore(canvas))
        {
            canvas.ClipRect(page);
            canvas.Translate(overview.X, overview.Y);
            canvas.Scale(overview.Scale);
            canvas.Translate(-overview.Bounds.Left, -overview.Bounds.Top);
            _controller.Renderer.Draw(canvas);
        }
        _paint.Style = SKPaintStyle.Stroke;
        _paint.StrokeWidth = 1;
        var active = _controller.ActiveArtboardId;
        for (var index = 0; index < artboards.Count; index++)
        {
            var artboard = artboards[index];
            _paint.Color = artboard.Id == active ? _accent : _border;
            canvas.DrawRect(overview.ToView(artboard.Bounds), _paint);
        }
    }

    private void UpdateViewportIndicator()
    {
        if (_disposed || _surface is null || _viewportIndicator is null)
            return;
        var width = _surface.ActualWidth;
        var height = _surface.ActualHeight;
        var clip = new Rect(0, 0, Math.Max(0, width), Math.Max(0, height));
        if (_viewportClip.Rect != clip)
            _viewportClip.Rect = clip;
        if (_viewport is null || !TryOverview(width, height, out var overview))
        {
            _viewportIndicator.Visibility = Visibility.Collapsed;
            return;
        }
        var visible = SKRect.Intersect(_viewport.VisibleWorldBounds, overview.Bounds);
        if (visible.IsEmpty)
        {
            _viewportIndicator.Visibility = Visibility.Collapsed;
            return;
        }

        var view = overview.ToView(visible);
        // XAML's rectangle stroke is inset into its layout bounds. Inflate by
        // half the stroke so its center follows the existing Skia rectangle.
        const double halfStroke = 0.75;
        Canvas.SetLeft(_viewportIndicator, view.Left - halfStroke);
        Canvas.SetTop(_viewportIndicator, view.Top - halfStroke);
        _viewportIndicator.Width = view.Width + 2 * halfStroke;
        _viewportIndicator.Height = view.Height + 2 * halfStroke;
        _viewportIndicator.Visibility = Visibility.Visible;
    }

    private void PanTo(SKPoint world)
    {
        if (_viewport is null)
            return;
        var point = _viewport.WorldToView(world);
        _viewport.Pan(_viewport.Width / 2 - point.X, _viewport.Height / 2 - point.Y);
    }

    private bool BeginNavigation()
    {
        if (_disposed || _controller?.Document is null || _viewport is null)
            return false;
        try
        { NavigationStarting?.Invoke(this, EventArgs.Empty); return true; }
        catch (Exception exception)
        {
            NavigationFailed?.Invoke(this, new SvgEditorInteractionFailedEventArgs(exception));
            return false;
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        var point = args.GetCurrentPoint(_surface);
        if (_pointer is not null || (!point.Properties.IsLeftButtonPressed && args.Pointer.PointerDeviceType != PointerDeviceType.Touch)
            || !TryOverview(_surface.ActualWidth, _surface.ActualHeight, out var overview) || !BeginNavigation() || _viewport is null)
            return;
        Focus(FocusState.Pointer);
        if (!CapturePointer(args.Pointer))
            return;
        var world = overview.ToWorld(point.Position);
        var visible = _viewport.VisibleWorldBounds;
        _dragOrigin = new SKPoint(visible.MidX, visible.MidY);
        _dragOffset = visible.Contains(world) ? new SKPoint(world.X - visible.MidX, world.Y - visible.MidY) : default;
        _pointer = args.Pointer.PointerId;
        PanTo(new SKPoint(world.X - _dragOffset.X, world.Y - _dragOffset.Y));
        args.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (_pointer != args.Pointer.PointerId || !TryOverview(_surface.ActualWidth, _surface.ActualHeight, out var overview))
            return;
        var world = overview.ToWorld(args.GetCurrentPoint(_surface).Position);
        PanTo(new SKPoint(world.X - _dragOffset.X, world.Y - _dragOffset.Y));
        args.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (_pointer != args.Pointer.PointerId)
            return;
        EndDrag(false);
        args.Handled = true;
    }
    private void OnPointerCanceled(object sender, PointerRoutedEventArgs args) => EndDrag(true);
    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs args) => EndDrag(false);
    private void EndDrag(bool cancel)
    {
        if (_pointer is null)
            return;
        _pointer = null;
        if (cancel)
            PanTo(_dragOrigin);
        ReleasePointerCaptures();
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs args)
    {
        if (!TryOverview(_surface.ActualWidth, _surface.ActualHeight, out var overview) || !BeginNavigation() || _viewport is null)
            return;
        var point = args.GetCurrentPoint(_surface);
        var world = overview.ToWorld(point.Position);
        _viewport.ZoomAt(Math.Exp(point.Properties.MouseWheelDelta * 0.0015), _viewport.WorldToView(world));
        args.Handled = true;
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Escape && _pointer is not null)
        { EndDrag(true); args.Handled = true; return; }
        bool Down(VirtualKey key) => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;
        if (Down(VirtualKey.Control) || Down(VirtualKey.Menu) || Down(VirtualKey.LeftWindows) || Down(VirtualKey.RightWindows))
            return;
        if (args.Key is not (VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down
            or VirtualKey.Home or VirtualKey.Add or VirtualKey.Subtract or VirtualKey.Number0) && (int)args.Key is not (187 or 189))
            return;
        if (!BeginNavigation() || _viewport is null)
            return;
        var step = Down(VirtualKey.Shift) ? 100 : 20;
        var center = new SKPoint((float)_viewport.Width / 2, (float)_viewport.Height / 2);
        switch (args.Key)
        {
            case VirtualKey.Left:
                _viewport.Pan(step, 0);
                break;
            case VirtualKey.Right:
                _viewport.Pan(-step, 0);
                break;
            case VirtualKey.Up:
                _viewport.Pan(0, step);
                break;
            case VirtualKey.Down:
                _viewport.Pan(0, -step);
                break;
            case VirtualKey.Home:
                if (_controller is { } controller)
                    _viewport.Fit(controller.GetArtboardsBounds());
                break;
            case VirtualKey.Number0:
                _viewport.ResetZoom();
                break;
            case VirtualKey.Add:
                _viewport.ZoomAt(1.25, center);
                break;
            case VirtualKey.Subtract:
                _viewport.ZoomAt(0.8, center);
                break;
            default:
                _viewport.ZoomAt((int)args.Key == 187 ? 1.25 : 0.8, center);
                break;
        }
        args.Handled = true;
    }

    private void OnSourceChanged(object? sender, EventArgs args)
    {
        if (_disposed)
            return;
        SubscribeRenderer();
        IsEnabled = _controller?.Document is not null && _viewport is not null;
        UpdateViewportIndicator();
        _surface.Invalidate();
    }
    private void OnViewportChanged(object? sender, EventArgs args)
    {
        if (_disposed)
            return;
        // Moving the camera changes only this native composition shape. Reusing
        // the overview avoids replaying the SVG and uploading a new raster texture.
        UpdateViewportIndicator();
        if (EditorInputTrace.Enabled)
        {
            var bounds = _artboardCache.GetBounds(_controller);
            if (bounds.IsEmpty)
                bounds = _controller?.Renderer.Picture?.CullRect ?? SKRect.Empty;
            EditorInputTrace.Write(FormattableString.Invariant($"navigator-viewport-update id={_traceId} overviewFrames={_overviewRenderCount} visible={_viewportIndicator.Visibility == Visibility.Visible} camera={_viewport?.OffsetX:R},{_viewport?.OffsetY:R},{_viewport?.Zoom:R} viewport={_viewport?.Width:R},{_viewport?.Height:R} overview={_surface.ActualWidth:R},{_surface.ActualHeight:R} artboards={bounds.Left:R},{bounds.Top:R},{bounds.Right:R},{bounds.Bottom:R} indicator={Canvas.GetLeft(_viewportIndicator):R},{Canvas.GetTop(_viewportIndicator):R},{_viewportIndicator.Width:R},{_viewportIndicator.Height:R}"));
        }
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }
    private void OnThemeChanged(FrameworkElement sender, object args) => RefreshPalette();
    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_disposed)
            return;
        _observingBrushes = true;
        ObserveBrushes();
        RefreshPalette();
    }
    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        EndDrag(false);
        _observingBrushes = false;
        _brushSubscriptions?.Dispose();
        _brushSubscriptions = null;
        _artboardCache.Clear();
    }
    private void OnSurfaceSizeChanged(object sender, SizeChangedEventArgs args)
    {
        UpdateViewportIndicator();
        _surface.Invalidate();
    }

    public new void Dispose()
    {
        if (_disposed)
            return;
        EndDrag(false);
        _disposed = true;
        _observingBrushes = false;
        _brushSubscriptions?.Dispose();
        _brushSubscriptions = null;
        Unsubscribe();
        _controller = null;
        _viewport = null;
        _artboardCache.Clear();
        ActualThemeChanged -= OnThemeChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        _surface.SizeChanged -= OnSurfaceSizeChanged;
        PointerPressed -= OnPointerPressed;
        PointerMoved -= OnPointerMoved;
        PointerReleased -= OnPointerReleased;
        PointerCanceled -= OnPointerCanceled;
        PointerCaptureLost -= OnPointerCaptureLost;
        PointerWheelChanged -= OnPointerWheelChanged;
        KeyDown -= OnKeyDown;
        _surface.Dispose();
        _paint.Dispose();
        base.Dispose();
    }
}
