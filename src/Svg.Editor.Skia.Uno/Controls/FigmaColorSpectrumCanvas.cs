using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using SK = SkiaSharp;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed class FigmaColorSpectrumCanvas : SKCanvasElement
{
    private bool _isDragging;
    private double _hue;
    private double _saturation = 1.0;
    private double _value = 1.0;

    public FigmaColorSpectrumCanvas()
    {
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCanceled += OnPointerCanceled;
    }

    public event EventHandler<(double Saturation, double Value)>? SelectionChanged;

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

    public double Saturation
    {
        get => _saturation;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_saturation - clamped) < 0.001)
            {
                return;
            }

            _saturation = clamped;
            Invalidate();
        }
    }

    public double Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_value - clamped) < 0.001)
            {
                return;
            }

            _value = clamped;
            Invalidate();
        }
    }

    protected override void RenderOverride(SK.SKCanvas canvas, Size area)
    {
        var width = Math.Max(1f, (float)area.Width);
        var height = Math.Max(1f, (float)area.Height);
        var rect = new SK.SKRect(0f, 0f, width, height);
        var radius = 14f;

        using var clipPath = new SK.SKPath();
        clipPath.AddRoundRect(rect, radius, radius);
        canvas.Save();
        canvas.ClipPath(clipPath, SK.SKClipOperation.Intersect, true);

        using var basePaint = new SK.SKPaint
        {
            IsAntialias = true,
            Color = new SK.SKColor(
                ColorPickerColorHelper.FromHsv(Hue, 1.0, 1.0).R,
                ColorPickerColorHelper.FromHsv(Hue, 1.0, 1.0).G,
                ColorPickerColorHelper.FromHsv(Hue, 1.0, 1.0).B)
        };
        canvas.DrawRect(rect, basePaint);

        using var whitePaint = new SK.SKPaint
        {
            IsAntialias = true,
            Shader = SK.SKShader.CreateLinearGradient(
                new SK.SKPoint(rect.Left, rect.Top),
                new SK.SKPoint(rect.Right, rect.Top),
                new[]
                {
                    new SK.SKColor(255, 255, 255, 255),
                    new SK.SKColor(255, 255, 255, 0)
                },
                null,
                SK.SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(rect, whitePaint);

        using var blackPaint = new SK.SKPaint
        {
            IsAntialias = true,
            Shader = SK.SKShader.CreateLinearGradient(
                new SK.SKPoint(rect.Left, rect.Top),
                new SK.SKPoint(rect.Left, rect.Bottom),
                new[]
                {
                    new SK.SKColor(0, 0, 0, 0),
                    new SK.SKColor(0, 0, 0, 255)
                },
                null,
                SK.SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(rect, blackPaint);
        canvas.Restore();

        var markerX = (float)(Saturation * width);
        var markerY = (float)((1.0 - Value) * height);

        using var shadowPaint = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Stroke,
            StrokeWidth = 5f,
            Color = new SK.SKColor(0, 0, 0, 34)
        };
        using var ringPaint = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            Color = SK.SKColors.White
        };
        using var corePaint = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = new SK.SKColor(0, 0, 0, 110)
        };

        var center = new SK.SKPoint(markerX, markerY);
        canvas.DrawCircle(center, 10f, shadowPaint);
        canvas.DrawCircle(center, 10f, ringPaint);
        canvas.DrawCircle(center, 10f, corePaint);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = true;
        CapturePointer(e.Pointer);
        UpdateSelection(e.GetCurrentPoint(this).Position);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        UpdateSelection(e.GetCurrentPoint(this).Position);
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
        UpdateSelection(e.GetCurrentPoint(this).Position);
        e.Handled = true;
    }

    private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
        ReleasePointerCapture(e.Pointer);
    }

    private void UpdateSelection(Point point)
    {
        var width = Math.Max(1.0, ActualWidth);
        var height = Math.Max(1.0, ActualHeight);
        Saturation = Math.Clamp(point.X / width, 0.0, 1.0);
        Value = 1.0 - Math.Clamp(point.Y / height, 0.0, 1.0);
        SelectionChanged?.Invoke(this, (Saturation, Value));
    }
}
