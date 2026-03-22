using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.UI;
using SK = SkiaSharp;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed class FigmaAlphaSliderCanvas : SKCanvasElement
{
    private bool _isDragging;
    private double _alpha = 1.0;
    private Color _baseColor = Color.FromArgb(255, 255, 0, 0);

    public FigmaAlphaSliderCanvas()
    {
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCanceled += OnPointerCanceled;
    }

    public event EventHandler<double>? AlphaChanged;

    public double Alpha
    {
        get => _alpha;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_alpha - clamped) < 0.001)
            {
                return;
            }

            _alpha = clamped;
            Invalidate();
        }
    }

    public Color BaseColor
    {
        get => _baseColor;
        set
        {
            if (_baseColor.Equals(value))
            {
                return;
            }

            _baseColor = value;
            Invalidate();
        }
    }

    protected override void RenderOverride(SK.SKCanvas canvas, Size area)
    {
        var width = Math.Max(1f, (float)area.Width);
        var height = Math.Max(1f, (float)area.Height);
        var rect = new SK.SKRect(0f, 0f, width, height);

        using var clipPath = new SK.SKPath();
        clipPath.AddRoundRect(rect, 12f, 12f);
        canvas.Save();
        canvas.ClipPath(clipPath, SK.SKClipOperation.Intersect, true);
        DrawCheckerboard(canvas, rect);

        using var alphaPaint = new SK.SKPaint
        {
            IsAntialias = true,
            Shader = SK.SKShader.CreateLinearGradient(
                new SK.SKPoint(rect.Left, rect.Top),
                new SK.SKPoint(rect.Right, rect.Top),
                new[]
                {
                    new SK.SKColor(BaseColor.R, BaseColor.G, BaseColor.B, 0),
                    new SK.SKColor(BaseColor.R, BaseColor.G, BaseColor.B, 255)
                },
                null,
                SK.SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(rect, alphaPaint);
        canvas.Restore();

        using var stroke = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = new SK.SKColor(209, 213, 219)
        };
        canvas.DrawRoundRect(rect, 12f, 12f, stroke);

        DrawThumb(canvas, rect, Alpha);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = true;
        CapturePointer(e.Pointer);
        UpdateAlpha(e.GetCurrentPoint(this).Position);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        UpdateAlpha(e.GetCurrentPoint(this).Position);
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
        UpdateAlpha(e.GetCurrentPoint(this).Position);
        e.Handled = true;
    }

    private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
        ReleasePointerCapture(e.Pointer);
    }

    private void UpdateAlpha(Point point)
    {
        var width = Math.Max(1.0, ActualWidth);
        Alpha = Math.Clamp(point.X / width, 0.0, 1.0);
        AlphaChanged?.Invoke(this, Alpha);
    }

    private static void DrawCheckerboard(SK.SKCanvas canvas, SK.SKRect rect)
    {
        const float cellSize = 10f;
        using var light = new SK.SKPaint { Color = new SK.SKColor(255, 255, 255) };
        using var dark = new SK.SKPaint { Color = new SK.SKColor(225, 227, 230) };

        for (var y = rect.Top; y < rect.Bottom; y += cellSize)
        {
            for (var x = rect.Left; x < rect.Right; x += cellSize)
            {
                var isDark = (((int)((x - rect.Left) / cellSize)) + ((int)((y - rect.Top) / cellSize))) % 2 == 0;
                canvas.DrawRect(
                    new SK.SKRect(x, y, Math.Min(x + cellSize, rect.Right), Math.Min(y + cellSize, rect.Bottom)),
                    isDark ? dark : light);
            }
        }
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
