using System.Numerics;
using Microsoft.Maui.Controls;

namespace Maui.Svg.Skia;

internal readonly record struct SvgSize(double Width, double Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

internal readonly record struct SvgPoint(double X, double Y);

internal readonly record struct SvgRect(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public SvgRect Intersect(SvgRect other)
    {
        var left = Math.Max(Left, other.Left);
        var top = Math.Max(Top, other.Top);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);

        return right <= left || bottom <= top
            ? default
            : new SvgRect(left, top, right - left, bottom - top);
    }

    public SvgRect CenterRect(SvgRect rect)
    {
        return new SvgRect(
            X + ((Width - rect.Width) / 2.0),
            Y + ((Height - rect.Height) / 2.0),
            rect.Width,
            rect.Height);
    }
}

internal readonly record struct SvgRenderInfo(
    SvgSize ViewportSize,
    SvgRect DestinationRect,
    SvgRect SourceRect,
    Matrix3x2 Matrix)
{
    public bool TryMapToPicture(SvgPoint point, out SvgPoint picturePoint)
    {
        if (!Matrix3x2.Invert(Matrix, out var inverse))
        {
            picturePoint = default;
            return false;
        }

        var mapped = Vector2.Transform(new Vector2((float)point.X, (float)point.Y), inverse);
        picturePoint = new SvgPoint(mapped.X, mapped.Y);
        return true;
    }
}

internal static class SvgRenderLayout
{
    public static SvgSize CalculateSize(
        SvgSize availableSize,
        SvgSize sourceSize,
        Stretch stretch,
        StretchDirection stretchDirection)
    {
        if (sourceSize.IsEmpty)
        {
            return default;
        }

        if (double.IsInfinity(availableSize.Width) && double.IsInfinity(availableSize.Height))
        {
            return sourceSize;
        }

        var (scaleX, scaleY) = CalculateScaling(availableSize, sourceSize, stretch, stretchDirection);
        return new SvgSize(sourceSize.Width * scaleX, sourceSize.Height * scaleY);
    }

    public static bool TryCreateRenderInfo(
        SvgSize viewportSize,
        SvgRect pictureBounds,
        Stretch stretch,
        StretchDirection stretchDirection,
        double zoom,
        double panX,
        double panY,
        out SvgRenderInfo renderInfo)
    {
        renderInfo = default;

        var sourceSize = new SvgSize(pictureBounds.Width, pictureBounds.Height);
        if (viewportSize.IsEmpty || sourceSize.IsEmpty)
        {
            return false;
        }

        zoom = NormalizeZoom(zoom);
        panX = NormalizeOffset(panX);
        panY = NormalizeOffset(panY);

        var viewport = new SvgRect(0, 0, viewportSize.Width, viewportSize.Height);
        var (scaleX, scaleY) = CalculateScaling(viewportSize, sourceSize, stretch, stretchDirection);
        var scaledSize = new SvgRect(0, 0, sourceSize.Width * scaleX, sourceSize.Height * scaleY);
        var destinationRect = viewport.CenterRect(scaledSize).Intersect(viewport);
        if (destinationRect.Width <= 0 || destinationRect.Height <= 0)
        {
            return false;
        }

        var sourceRect = new SvgRect(0, 0, sourceSize.Width, sourceSize.Height)
            .CenterRect(new SvgRect(0, 0, destinationRect.Width / scaleX, destinationRect.Height / scaleY));

        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
        {
            return false;
        }

        var baseMatrix =
            Matrix3x2.CreateScale(
                (float)(destinationRect.Width / sourceRect.Width),
                (float)(destinationRect.Height / sourceRect.Height))
            * Matrix3x2.CreateTranslation(
                (float)(-sourceRect.X + destinationRect.X - pictureBounds.X),
                (float)(-sourceRect.Y + destinationRect.Y - pictureBounds.Y));

        var userMatrix =
            Matrix3x2.CreateScale((float)zoom)
            * Matrix3x2.CreateTranslation((float)panX, (float)panY);

        renderInfo = new SvgRenderInfo(viewportSize, destinationRect, sourceRect, baseMatrix * userMatrix);
        return true;
    }

    public static (double Zoom, double PanX, double PanY) ZoomToPoint(
        double zoom,
        double panX,
        double panY,
        double newZoom,
        SvgPoint point)
    {
        zoom = NormalizeZoom(zoom);
        newZoom = NormalizeZoom(newZoom);
        newZoom = Math.Clamp(newZoom, 0.1, 10.0);
        var zoomFactor = newZoom / zoom;

        return (
            newZoom,
            point.X - ((point.X - panX) * zoomFactor),
            point.Y - ((point.Y - panY) * zoomFactor));
    }

    private static (double ScaleX, double ScaleY) CalculateScaling(
        SvgSize availableSize,
        SvgSize sourceSize,
        Stretch stretch,
        StretchDirection stretchDirection)
    {
        if (sourceSize.IsEmpty)
        {
            return (0, 0);
        }

        var scaleX = 1.0;
        var scaleY = 1.0;

        if (stretch != Stretch.None)
        {
            var hasWidth = !double.IsInfinity(availableSize.Width) && availableSize.Width > 0;
            var hasHeight = !double.IsInfinity(availableSize.Height) && availableSize.Height > 0;

            var candidateScaleX = hasWidth ? availableSize.Width / sourceSize.Width : 1.0;
            var candidateScaleY = hasHeight ? availableSize.Height / sourceSize.Height : 1.0;

            if (stretch == Stretch.Uniform)
            {
                var uniform = hasWidth && !hasHeight
                    ? candidateScaleX
                    : !hasWidth && hasHeight
                        ? candidateScaleY
                        : Math.Min(candidateScaleX, candidateScaleY);
                scaleX = uniform;
                scaleY = uniform;
            }
            else if (stretch == Stretch.UniformToFill)
            {
                var uniform = hasWidth && !hasHeight
                    ? candidateScaleX
                    : !hasWidth && hasHeight
                        ? candidateScaleY
                        : Math.Max(candidateScaleX, candidateScaleY);
                scaleX = uniform;
                scaleY = uniform;
            }
            else
            {
                scaleX = candidateScaleX;
                scaleY = candidateScaleY;
            }
        }

        scaleX = ApplyStretchDirection(scaleX, stretchDirection);
        scaleY = ApplyStretchDirection(scaleY, stretchDirection);

        return (scaleX, scaleY);
    }

    private static double ApplyStretchDirection(double scale, StretchDirection stretchDirection)
    {
        return stretchDirection switch
        {
            StretchDirection.UpOnly => Math.Max(1.0, scale),
            StretchDirection.DownOnly => Math.Min(1.0, scale),
            _ => scale
        };
    }

    private static double NormalizeZoom(double zoom)
    {
        return IsFinite(zoom) && zoom > 0 ? zoom : 1.0;
    }

    private static double NormalizeOffset(double offset)
    {
        return IsFinite(offset) ? offset : 0.0;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
