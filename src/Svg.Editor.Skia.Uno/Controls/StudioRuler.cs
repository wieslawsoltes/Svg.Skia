using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Svg.Editor.Skia;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Rectangle = Microsoft.UI.Xaml.Shapes.Rectangle;

namespace Svg.Editor.Skia.Uno;

/// <summary>Camera-only chrome; updating the ruler never serializes or refreshes the SVG.</summary>
public sealed class StudioRuler : SKCanvasElement, ISkiaCanvasRenderer, IDisposable
{
    public static readonly DependencyProperty BackgroundBrushProperty = RegisterBrush(nameof(BackgroundBrush));
    public static readonly DependencyProperty ForegroundBrushProperty = RegisterBrush(nameof(ForegroundBrush));
    public static readonly DependencyProperty PointerBrushProperty = RegisterBrush(nameof(PointerBrush));
    private StudioBrushSubscriptions? _brushSubscriptions;
    private readonly SKPaint _paint = new() { IsAntialias = true };
    private readonly SKFont _font = new(SKTypeface.Default, 10);
    private readonly SvgRulerTickCache _ticks = new();
    private readonly SolidColorBrush _pointerFill = new();
    private readonly TranslateTransform _pointerTranslation = new();
    private readonly RectangleGeometry _pointerClip = new();
    private readonly Rectangle _pointerMarker;
    private Orientation _orientation;
    private double _origin;
    private double _scale = 1;
    private double? _pointer;
    private bool _dark;
    private bool _disposed;
    public StudioRuler()
    {
        // Uno 7 uses a border visual; a brush supplies its hit-test geometry.
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        // Retain the ticks and labels while moving only a native composition
        // shape. Pointer motion must not rasterize/upload the whole ruler.
        _pointerMarker = new Rectangle
        {
            Fill = _pointerFill,
            RenderTransform = _pointerTranslation,
            IsHitTestVisible = false,
            UseLayoutRounding = false,
            Visibility = Visibility.Collapsed
        };
        var pointerLayer = new Canvas
        {
            IsHitTestVisible = false,
            UseLayoutRounding = false,
            Clip = _pointerClip
        };
        Canvas.SetZIndex(pointerLayer, 1);
        pointerLayer.Children.Add(_pointerMarker);
        Children.Add(pointerLayer);
        ActualThemeChanged += OnThemeChanged;
        SizeChanged += OnSizeChanged;
    }
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation == value)
                return;
            _orientation = value;
            UpdatePointerMarker();
            Invalidate();
        }
    }
    public Brush? BackgroundBrush { get => (Brush?)GetValue(BackgroundBrushProperty); set => SetValue(BackgroundBrushProperty, value); }
    public Brush? ForegroundBrush { get => (Brush?)GetValue(ForegroundBrushProperty); set => SetValue(ForegroundBrushProperty, value); }
    public Brush? PointerBrush { get => (Brush?)GetValue(PointerBrushProperty); set => SetValue(PointerBrushProperty, value); }

    private static DependencyProperty RegisterBrush(string name)
        => DependencyProperty.Register(name, typeof(Brush), typeof(StudioRuler), new PropertyMetadata(null, OnBrushChanged));
    private static void OnBrushChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var ruler = (StudioRuler)sender;
        if (ruler._disposed)
            return;
        (ruler._brushSubscriptions ??= new StudioBrushSubscriptions(ruler.RefreshPalette)).Observe(args.Property, args.NewValue as Brush);
        if (args.Property == PointerBrushProperty)
            ruler.UpdatePointerMarker();
        else
            ruler.Invalidate();
    }
    private void RefreshPalette()
    {
        if (_disposed)
            return;
        UpdatePointerMarker();
        Invalidate();
    }
    private void OnThemeChanged(FrameworkElement sender, object args) => RefreshPalette();
    private void OnSizeChanged(object sender, SizeChangedEventArgs args) => UpdatePointerMarker();
    public void Update(double origin, double scale, double? pointer, bool dark)
    {
        if (_disposed || (_origin == origin && _scale == scale && _pointer == pointer && _dark == dark))
            return;
        var ticksChanged = _origin != origin || _scale != scale || _dark != dark;
        _origin = origin;
        _scale = scale;
        _pointer = pointer;
        _dark = dark;
        UpdatePointerMarker();
        if (ticksChanged)
            Invalidate();
    }
    void ISkiaCanvasRenderer.Render(SKCanvas canvas, Size area) => RenderOverride(canvas, area);

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (_disposed)
            return;
        using var measurement = EditorRenderDiagnostics.Measure(EditorRenderPhase.Ruler);
        using var restore = new SKAutoCanvasRestore(canvas);
        canvas.ClipRect(SKRect.Create((float)area.Width, (float)area.Height));
        _paint.Color = StudioBrushSubscriptions.Color(BackgroundBrush, _dark ? new SKColor(41, 41, 41) : new SKColor(222, 222, 222));
        canvas.DrawRect(SKRect.Create((float)area.Width, (float)area.Height), _paint);
        var length = Orientation == Orientation.Horizontal ? area.Width : area.Height;
        _paint.Color = StudioBrushSubscriptions.Color(ForegroundBrush, _dark ? new SKColor(184, 184, 184) : new SKColor(80, 80, 80));
        _paint.StrokeWidth = 1;
        var ticks = _ticks.GetTicks(_origin, _scale, length);
        for (var index = 0; index < ticks.Count; index++)
        {
            var tick = ticks[index];
            var position = (float)tick.Pixel;
            var extent = tick.IsMajor ? 8 : 4;
            if (Orientation == Orientation.Horizontal)
            {
                canvas.DrawLine(position, (float)area.Height - extent, position, (float)area.Height, _paint);
                if (tick.IsMajor)
                    canvas.DrawText(tick.Label, position + 3, 11, SKTextAlign.Left, _font, _paint);
            }
            else
            {
                canvas.DrawLine((float)area.Width - extent, position, (float)area.Width, position, _paint);
                if (tick.IsMajor)
                {
                    using var labelRestore = new SKAutoCanvasRestore(canvas);
                    canvas.Translate(11, position + 3);
                    canvas.RotateDegrees(90);
                    canvas.DrawText(tick.Label, 0, 0, SKTextAlign.Left, _font, _paint);
                }
            }
        }
    }

    private void UpdatePointerMarker()
    {
        if (_disposed || _pointerMarker is null)
            return;
        var width = Math.Max(0, ActualWidth);
        var height = Math.Max(0, ActualHeight);
        var clip = new Rect(0, 0, width, height);
        if (_pointerClip.Rect != clip)
            _pointerClip.Rect = clip;
        var horizontal = Orientation == Orientation.Horizontal;
        var length = horizontal ? width : height;
        if (_pointer is not { } pointer || !double.IsFinite(pointer) || pointer < 0 || pointer > length
            || width <= 0 || height <= 0)
        {
            _pointerMarker.Visibility = Visibility.Collapsed;
            return;
        }

        var color = StudioBrushSubscriptions.Color(PointerBrush, _dark ? new SKColor(131, 176, 255) : new SKColor(54, 106, 204));
        var pointerColor = Windows.UI.Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
        if (_pointerFill.Color != pointerColor)
            _pointerFill.Color = pointerColor;
        var markerWidth = horizontal ? 1 : width;
        var markerHeight = horizontal ? height : 1;
        if (_pointerMarker.Width != markerWidth)
            _pointerMarker.Width = markerWidth;
        if (_pointerMarker.Height != markerHeight)
            _pointerMarker.Height = markerHeight;
        // A one-unit filled rectangle has the same footprint as the previous
        // centered one-unit Skia stroke, including fractional pointer positions.
        _pointerTranslation.X = horizontal ? pointer - 0.5 : 0;
        _pointerTranslation.Y = horizontal ? 0 : pointer - 0.5;
        _pointerMarker.Visibility = Visibility.Visible;
    }
    public new void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        ActualThemeChanged -= OnThemeChanged;
        SizeChanged -= OnSizeChanged;
        _brushSubscriptions?.Dispose();
        _brushSubscriptions = null;
        _paint.Dispose();
        _font.Dispose();
        base.Dispose();
    }
}
