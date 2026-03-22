using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using SK = SkiaSharp;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed class FigmaHueSliderCanvas : SKCanvasElement
{
    private bool _isDragging;
    private double _hue;

    public FigmaHueSliderCanvas()
    {
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCanceled += OnPointerCanceled;
    }

    public event EventHandler<double>? HueChanged;

    public double Hue
    {
        get => _hue;
        set
        {
            var normalized = ((value % 360.0) + 360.0) % 360.0;
            if (Math.Abs(_hue - normalized) < 0.001)
            {
                return;
            }

            _hue = normalized;
            Invalidate();
        }
    }

    protected override void RenderOverride(SK.SKCanvas canvas, Size area)
    {
        var width = Math.Max(1f, (float)area.Width);
        var height = Math.Max(1f, (float)area.Height);
        var rect = new SK.SKRect(0f, 0f, width, height);

        DrawTrackBase(canvas, rect);

        using var huePaint = new SK.SKPaint
        {
            IsAntialias = true,
            Shader = SK.SKShader.CreateLinearGradient(
                new SK.SKPoint(rect.Left, rect.Top),
                new SK.SKPoint(rect.Right, rect.Top),
                new[]
                {
                    new SK.SKColor(255, 0, 0),
                    new SK.SKColor(255, 255, 0),
                    new SK.SKColor(0, 255, 0),
                    new SK.SKColor(0, 255, 255),
                    new SK.SKColor(0, 0, 255),
                    new SK.SKColor(255, 0, 255),
                    new SK.SKColor(255, 0, 0)
                },
                new[] { 0f, 0.17f, 0.33f, 0.5f, 0.67f, 0.83f, 1f },
                SK.SKShaderTileMode.Clamp)
        };

        using var clipPath = new SK.SKPath();
        clipPath.AddRoundRect(rect, 12f, 12f);
        canvas.Save();
        canvas.ClipPath(clipPath, SK.SKClipOperation.Intersect, true);
        canvas.DrawRect(rect, huePaint);
        canvas.Restore();

        DrawThumb(canvas, rect, Hue / 360.0);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = true;
        CapturePointer(e.Pointer);
        UpdateHue(e.GetCurrentPoint(this).Position);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        UpdateHue(e.GetCurrentPoint(this).Position);
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        ReleasePointerCapture(e.Pointer);
        UpdateHue(e.GetCurrentPoint(this).Position);
        e.Handled = true;
    }

    private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
        ReleasePointerCapture(e.Pointer);
    }

    private void UpdateHue(Point point)
    {
        var width = Math.Max(1.0, ActualWidth);
        Hue = Math.Clamp(point.X / width, 0.0, 1.0) * 360.0;
        HueChanged?.Invoke(this, Hue);
    }

    private static void DrawTrackBase(SK.SKCanvas canvas, SK.SKRect rect)
    {
        using var stroke = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = new SK.SKColor(209, 213, 219)
        };
        canvas.DrawRoundRect(rect, 12f, 12f, stroke);
    }

    private static void DrawThumb(SK.SKCanvas canvas, SK.SKRect rect, double position)
    {
        var x = (float)(rect.Left + (rect.Width * Math.Clamp(position, 0.0, 1.0)));
        var center = new SK.SKPoint(x, rect.MidY);

        using var shadow = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Fill,
            Color = new SK.SKColor(0, 0, 0, 28)
        };
        using var fill = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Fill,
            Color = SK.SKColors.White
        };
        using var stroke = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = new SK.SKColor(0, 0, 0, 72)
        };

        canvas.DrawCircle(center, 16f, shadow);
        canvas.DrawCircle(center, 14f, fill);
        canvas.DrawCircle(center, 14f, stroke);
    }
}
