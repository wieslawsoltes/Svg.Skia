using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Svg.Editor.Skia;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Svg.Editor.Skia.Uno;

/// <summary>Camera-only chrome; updating the ruler never serializes or refreshes the SVG.</summary>
public sealed class StudioRuler : SKCanvasElement, IDisposable
{
    public static readonly DependencyProperty BackgroundBrushProperty = RegisterBrush(nameof(BackgroundBrush));
    public static readonly DependencyProperty ForegroundBrushProperty = RegisterBrush(nameof(ForegroundBrush));
    public static readonly DependencyProperty PointerBrushProperty = RegisterBrush(nameof(PointerBrush));
    private StudioBrushSubscriptions? _brushSubscriptions;
    private readonly SKPaint _paint = new() { IsAntialias = true };
    private readonly SKFont _font = new(SKTypeface.Default, 10);
    private double _origin;
    private double _scale = 1;
    private double? _pointer;
    private bool _dark;
    private bool _disposed;
    public StudioRuler() => ActualThemeChanged += OnThemeChanged;
    public Orientation Orientation { get; set; }
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
        (ruler._brushSubscriptions ??= new StudioBrushSubscriptions(ruler.Invalidate)).Observe(args.Property, args.NewValue as Brush);
        ruler.Invalidate();
    }
    private void OnThemeChanged(FrameworkElement sender, object args) { if (!_disposed) Invalidate(); }
    public void Update(double origin, double scale, double? pointer, bool dark)
    {
        if (_disposed || (_origin == origin && _scale == scale && _pointer == pointer && _dark == dark))
            return;
        _origin = origin;
        _scale = scale;
        _pointer = pointer;
        _dark = dark;
        Invalidate();
    }
    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (_disposed)
            return;
        using var restore = new SKAutoCanvasRestore(canvas);
        canvas.ClipRect(SKRect.Create((float)area.Width, (float)area.Height));
        _paint.Color = StudioBrushSubscriptions.Color(BackgroundBrush, _dark ? new SKColor(41, 41, 41) : new SKColor(222, 222, 222));
        canvas.DrawRect(SKRect.Create((float)area.Width, (float)area.Height), _paint);
        var length = Orientation == Orientation.Horizontal ? area.Width : area.Height;
        _paint.Color = StudioBrushSubscriptions.Color(ForegroundBrush, _dark ? new SKColor(184, 184, 184) : new SKColor(80, 80, 80));
        _paint.StrokeWidth = 1;
        foreach (var tick in SvgRulerMetrics.Create(_origin, _scale, length))
        {
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
        if (_pointer is { } pointer && double.IsFinite(pointer) && pointer >= 0 && pointer <= length)
        {
            _paint.Color = StudioBrushSubscriptions.Color(PointerBrush, _dark ? new SKColor(131, 176, 255) : new SKColor(54, 106, 204));
            if (Orientation == Orientation.Horizontal)
                canvas.DrawLine((float)pointer, 0, (float)pointer, (float)area.Height, _paint);
            else
                canvas.DrawLine(0, (float)pointer, (float)area.Width, (float)pointer, _paint);
        }
    }
    public new void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        ActualThemeChanged -= OnThemeChanged;
        _brushSubscriptions?.Dispose();
        _brushSubscriptions = null;
        _paint.Dispose();
        _font.Dispose();
        base.Dispose();
    }
}
