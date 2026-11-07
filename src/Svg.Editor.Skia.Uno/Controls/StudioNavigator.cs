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

namespace Svg.Editor.Skia.Uno;

/// <summary>
/// A live overview that borrows the active renderer and camera. Navigating never
/// parses, clones, disposes, or edits the document or its shared render resources.
/// </summary>
public sealed class StudioNavigator : UserControl, IDisposable
{
    private static readonly InputSystemCursor s_hand = InputSystemCursor.Create(InputSystemCursorShape.Hand);
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
    private readonly SKPaint _paint = new() { IsAntialias = true };
    private SvgEditorController? _controller;
    private SKSvg? _subscribedRenderer;
    private SvgEditorViewport? _viewport;
    private uint? _pointer;
    private SKPoint _dragOffset;
    private SKPoint _dragOrigin;
    private SKColor _background, _border, _accent;
    private bool _disposed;

    public StudioNavigator()
    {
        IsTabStop = true;
        UseSystemFocusVisuals = true;
        MinHeight = 120;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        ManipulationMode = ManipulationModes.None;
        ProtectedCursor = s_hand;
        Resources.MergedDictionaries.Add(new EditorTheme());
        _surface = new NavigatorSurface(this) { IsHitTestVisible = false };
        _frame = new Border { BorderThickness = new Thickness(1), Child = _surface };
        Content = _frame;
        AutomationProperties.SetName(this, "Document navigator");
        AutomationProperties.SetHelpText(this, "Click or drag to move the view. Arrows pan, plus and minus zoom, Home fits all artboards, and Escape cancels a drag.");
        ToolTipService.SetToolTip(this, "Click or drag to move the view · scroll to zoom · Home fits all artboards");
        ApplyPalette();
        ActualThemeChanged += OnThemeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
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
        if (_controller is not null)
        {
            _controller.Changed += OnSourceChanged;
            SubscribeRenderer();
        }
        if (_viewport is not null)
            _viewport.Changed += OnViewportChanged;
        IsEnabled = controller?.Document is not null && viewport is not null;
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
        _frame.Background = (Brush)Resources["StudioCanvas"];
        _frame.BorderBrush = (Brush)Resources["StudioBorder"];
        SKColor Color(string key)
        {
            var color = ((SolidColorBrush)Resources[key]).Color;
            return new SKColor(color.R, color.G, color.B, color.A);
        }
        _background = Color("StudioCanvas");
        _border = Color("StudioBorder");
        _accent = Color("StudioAccent");
    }

    private bool TryOverview(double width, double height, out Overview overview)
    {
        overview = default;
        if (_controller?.Renderer.Picture is not { } picture || width <= 16 || height <= 16)
            return false;
        var bounds = _controller.GetArtboardsBounds();
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
        using var restore = new SKAutoCanvasRestore(canvas);
        canvas.ClipRect(SKRect.Create((float)area.Width, (float)area.Height));
        canvas.Clear(_background);
        if (!TryOverview(area.Width, area.Height, out var overview) || _controller is null)
            return;
        var page = overview.ToView(overview.Bounds);
        var artboards = _controller.GetArtboards();
        _paint.Style = SKPaintStyle.Fill;
        _paint.Color = SKColors.White;
        foreach (var artboard in artboards)
            canvas.DrawRect(overview.ToView(artboard.Bounds), _paint);
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
        foreach (var artboard in artboards)
        {
            _paint.Color = artboard.Id == active ? _accent : _border;
            canvas.DrawRect(overview.ToView(artboard.Bounds), _paint);
        }
        if (_viewport is null)
            return;
        var visible = SKRect.Intersect(_viewport.VisibleWorldBounds, overview.Bounds);
        if (visible.IsEmpty)
            return;
        var view = overview.ToView(visible);
        _paint.Style = SKPaintStyle.Fill;
        _paint.Color = _accent.WithAlpha(28);
        canvas.DrawRect(view, _paint);
        _paint.Style = SKPaintStyle.Stroke;
        _paint.StrokeWidth = 1.5f;
        _paint.Color = _accent;
        canvas.DrawRect(view, _paint);
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
        _surface.Invalidate();
    }
    private void OnViewportChanged(object? sender, EventArgs args)
    {
        if (_disposed)
            return;
        _surface.Invalidate();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }
    private void OnThemeChanged(FrameworkElement sender, object args) { ApplyPalette(); _surface.Invalidate(); }
    private void OnLoaded(object sender, RoutedEventArgs args) { ApplyPalette(); _surface.Invalidate(); }
    private void OnUnloaded(object sender, RoutedEventArgs args) => EndDrag(false);
    private void OnSizeChanged(object sender, SizeChangedEventArgs args) => _surface.Invalidate();

    public new void Dispose()
    {
        if (_disposed)
            return;
        EndDrag(false);
        _disposed = true;
        Unsubscribe();
        _controller = null;
        _viewport = null;
        ActualThemeChanged -= OnThemeChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        SizeChanged -= OnSizeChanged;
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
