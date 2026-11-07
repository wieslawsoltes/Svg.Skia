using System;
using SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>Pure geometry shared by canvas previews and committed tool operations.</summary>
public static class SvgEditorToolGeometry
{
    public static bool IsShape(SvgEditorInteractionMode mode) => mode is SvgEditorInteractionMode.RoundedRectangle
        or SvgEditorInteractionMode.RegularPolygon or SvgEditorInteractionMode.Star
        or SvgEditorInteractionMode.RectangularGrid or SvgEditorInteractionMode.PolarGrid or SvgEditorInteractionMode.Spiral;

    /// <summary>Creates one bounded vector path; no document state or rendering resources are retained.</summary>
    public static SKPath CreateShape(SvgEditorInteractionMode mode, SKPoint start, SKPoint end, int divisions = 6)
    {
        Validate(start);
        Validate(end);
        if (!IsShape(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (divisions < 3 || divisions > 64)
            throw new ArgumentOutOfRangeException(nameof(divisions));
        var rect = new SKRect(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));
        if (!Finite(rect.Width) || !Finite(rect.Height))
            throw new ArgumentOutOfRangeException(nameof(end));
        using var path = new SKPathBuilder();
        switch (mode)
        {
            case SvgEditorInteractionMode.RoundedRectangle:
                var radius = Math.Min(rect.Width, rect.Height) * 0.15f;
                path.AddRoundRect(rect, radius, radius);
                break;
            case SvgEditorInteractionMode.RegularPolygon:
            case SvgEditorInteractionMode.Star:
                var star = mode == SvgEditorInteractionMode.Star;
                var count = star ? divisions * 2 : divisions;
                for (var index = 0; index < count; index++)
                {
                    var angle = -Math.PI / 2 + index * Math.PI * 2 / count;
                    var factor = star && index % 2 == 1 ? 0.42 : 1;
                    var point = new SKPoint(rect.MidX + (float)(Math.Cos(angle) * rect.Width * factor / 2),
                        rect.MidY + (float)(Math.Sin(angle) * rect.Height * factor / 2));
                    if (index == 0)
                        path.MoveTo(point);
                    else
                        path.LineTo(point);
                }
                path.Close();
                break;
            case SvgEditorInteractionMode.RectangularGrid:
                for (var index = 0; index <= divisions; index++)
                {
                    var x = rect.Left + rect.Width * index / divisions;
                    var y = rect.Top + rect.Height * index / divisions;
                    path.MoveTo(x, rect.Top);
                    path.LineTo(x, rect.Bottom);
                    path.MoveTo(rect.Left, y);
                    path.LineTo(rect.Right, y);
                }
                break;
            case SvgEditorInteractionMode.PolarGrid:
                for (var index = 1; index <= divisions; index++)
                {
                    var rx = rect.Width * index / (divisions * 2f);
                    var ry = rect.Height * index / (divisions * 2f);
                    path.AddOval(new SKRect(rect.MidX - rx, rect.MidY - ry, rect.MidX + rx, rect.MidY + ry));
                }
                for (var index = 0; index < divisions; index++)
                {
                    var angle = index * Math.PI * 2 / divisions;
                    path.MoveTo(rect.MidX, rect.MidY);
                    path.LineTo(rect.MidX + (float)(Math.Cos(angle) * rect.Width / 2), rect.MidY + (float)(Math.Sin(angle) * rect.Height / 2));
                }
                break;
            case SvgEditorInteractionMode.Spiral:
                // Quarter-turn cubics form an Archimedean spiral with an exact derivative
                // at both ends of every segment, instead of a zoom-dependent polyline.
                var turns = Math.Min(divisions, 12);
                var total = turns * Math.PI * 2;
                SKPoint Point(double t) => new(rect.MidX + (float)(rect.Width / 2 * t / total * Math.Cos(t)),
                    rect.MidY + (float)(rect.Height / 2 * t / total * Math.Sin(t)));
                SKPoint Derivative(double t) => new((float)(rect.Width / (2 * total) * (Math.Cos(t) - t * Math.Sin(t))),
                    (float)(rect.Height / (2 * total) * (Math.Sin(t) + t * Math.Cos(t))));
                path.MoveTo(Point(0));
                for (var index = 0; index < turns * 4; index++)
                {
                    var a = index * Math.PI / 2;
                    var b = (index + 1) * Math.PI / 2;
                    var p0 = Point(a);
                    var p1 = Point(b);
                    var d0 = Derivative(a);
                    var d1 = Derivative(b);
                    var step = (float)((b - a) / 3);
                    path.CubicTo(p0.X + step * d0.X, p0.Y + step * d0.Y,
                        p1.X - step * d1.X, p1.Y - step * d1.Y, p1.X, p1.Y);
                }
                break;
        }
        return path.Detach();
    }

    /// <summary>Creates a document-space transform around the selected artwork's center.</summary>
    public static SKMatrix CreateTransform(SvgEditorInteractionMode mode, SKRect bounds, SKPoint start, SKPoint current, bool constrain)
    {
        Validate(start);
        Validate(current);
        Validate(new SKPoint(bounds.Left, bounds.Top));
        Validate(new SKPoint(bounds.Right, bounds.Bottom));
        var center = new SKPoint(bounds.MidX, bounds.MidY);
        var x = start.X - center.X;
        var y = start.Y - center.Y;
        var dx = current.X - center.X;
        var dy = current.Y - center.Y;
        if (mode == SvgEditorInteractionMode.Rotate)
        {
            var angle = (Math.Atan2(dy, dx) - Math.Atan2(y, x)) * 180 / Math.PI;
            if (constrain)
                angle = Math.Round(angle / 45) * 45;
            return SKMatrix.CreateRotationDegrees((float)angle, center.X, center.Y);
        }
        if (mode == SvgEditorInteractionMode.Scale)
        {
            var sx = Math.Abs(x) > 0.0001 ? dx / x : 1 + (current.X - start.X) / Math.Max(1, bounds.Width / 2);
            var sy = Math.Abs(y) > 0.0001 ? dy / y : 1 + (current.Y - start.Y) / Math.Max(1, bounds.Height / 2);
            if (constrain)
                sx = sy = Math.Abs(sx - 1) > Math.Abs(sy - 1) ? sx : sy;
            // A sign-changing scale is a reflection; a singular transform would make
            // future inverse-coordinate tools unusable, so clamp its magnitude only.
            sx = ClampScale(sx);
            sy = ClampScale(sy);
            return SKMatrix.CreateScale(sx, sy, center.X, center.Y);
        }
        if (mode == SvgEditorInteractionMode.Reflect)
        {
            var angle = Math.Atan2(dy, dx);
            if (constrain)
                angle = Math.Round(angle / (Math.PI / 4)) * Math.PI / 4;
            var c = (float)Math.Cos(2 * angle);
            var s = (float)Math.Sin(2 * angle);
            return new SKMatrix(c, s, center.X - c * center.X - s * center.Y,
                s, -c, center.Y - s * center.X + c * center.Y, 0, 0, 1);
        }
        throw new ArgumentOutOfRangeException(nameof(mode));
    }

    private static float ClampScale(float scale)
        => Math.Max(0.001f, Math.Min(10000f, Math.Abs(scale))) * (scale < 0 ? -1 : 1);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static void Validate(SKPoint point)
    {
        if (!Finite(point.X) || !Finite(point.Y))
            throw new ArgumentOutOfRangeException(nameof(point));
    }
}
