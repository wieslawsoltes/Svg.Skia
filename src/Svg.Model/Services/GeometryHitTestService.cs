// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using ShimSkiaSharp;

namespace Svg.Model.Services;

internal static class GeometryHitTestService
{
    private const float MinStrokeTolerance = 0.5f;
    private const int MinCurveSteps = 8;
    private const int MaxCurveSteps = 96;
    private const int MinEllipseSteps = 24;
    private const int MaxEllipseSteps = 128;
    private const float FullCircleRadians = 2f * (float)Math.PI;

    private sealed class FlattenedContour
    {
        public FlattenedContour(List<SKPoint> points, bool closed)
        {
            Points = points;
            Closed = closed;
        }

        public List<SKPoint> Points { get; }

        public bool Closed { get; }
    }

    public static bool ContainsFill(SKPath path, SKPoint point, SKMatrix transform)
    {
        if (!TryMapToLocal(point, transform, out var localPoint))
        {
            return false;
        }

        return ContainsFillLocal(path, localPoint);
    }

    public static bool ContainsStroke(SKPath path, SKPoint point, SKMatrix transform, float strokeWidth)
    {
        if (strokeWidth <= 0f || !TryMapToLocal(point, transform, out var localPoint))
        {
            return false;
        }

        return ContainsStrokeLocal(path, localPoint, Math.Max(strokeWidth / 2f, MinStrokeTolerance));
    }

    public static bool Contains(ClipPath clipPath, SKPoint point)
    {
        return ContainsClipPath(clipPath, point);
    }

    private static bool TryMapToLocal(SKPoint point, SKMatrix transform, out SKPoint localPoint)
    {
        if (transform.IsIdentity)
        {
            localPoint = point;
            return true;
        }

        if (!transform.TryInvert(out var inverse))
        {
            localPoint = default;
            return false;
        }

        localPoint = inverse.MapPoint(point);
        return true;
    }

    private static bool ContainsClipPath(ClipPath? clipPath, SKPoint point)
    {
        if (clipPath is null)
        {
            return true;
        }

        var localPoint = point;
        if (clipPath.Transform is { } transform)
        {
            if (!TryMapToLocal(point, transform, out localPoint))
            {
                return false;
            }
        }

        var hasReferencedGeometry = HasClipGeometry(clipPath.Clip);
        if (hasReferencedGeometry && !ContainsClipPath(clipPath.Clip, localPoint))
        {
            return false;
        }

        var hasLocalGeometry = HasLocalClipGeometry(clipPath);
        if (!hasLocalGeometry)
        {
            return hasReferencedGeometry;
        }

        var localClips = clipPath.Clips;
        if (localClips is null)
        {
            return false;
        }

        foreach (var clip in localClips)
        {
            if (ContainsPathClip(clip, localPoint))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsPathClip(PathClip? clip, SKPoint point)
    {
        if (clip is null)
        {
            return false;
        }

        var localPoint = point;
        if (clip.Transform is { } transform)
        {
            if (!TryMapToLocal(point, transform, out localPoint))
            {
                return false;
            }
        }

        var hasNestedGeometry = HasClipGeometry(clip.Clip);
        if (hasNestedGeometry && !ContainsClipPath(clip.Clip, localPoint))
        {
            return false;
        }

        if (clip.Path?.Commands is not { Count: > 0 })
        {
            return hasNestedGeometry;
        }

        return ContainsFillLocal(clip.Path, localPoint);
    }

    private static bool ContainsFillLocal(SKPath path, SKPoint point)
    {
        var contours = FlattenPath(path);
        if (contours.Count == 0)
        {
            return false;
        }

        return path.FillType switch
        {
            SKPathFillType.EvenOdd => ContainsEvenOdd(contours, point),
            _ => ContainsWinding(contours, point)
        };
    }

    private static bool ContainsStrokeLocal(SKPath path, SKPoint point, float tolerance)
    {
        var contours = FlattenPath(path);
        if (contours.Count == 0)
        {
            return false;
        }

        foreach (var contour in contours)
        {
            var points = contour.Points;
            if (points.Count == 0)
            {
                continue;
            }

            if (points.Count == 1)
            {
                if (Distance(points[0], point) <= tolerance)
                {
                    return true;
                }

                continue;
            }

            for (var i = 1; i < points.Count; i++)
            {
                if (DistanceToSegment(point, points[i - 1], points[i]) <= tolerance)
                {
                    return true;
                }
            }

            if (contour.Closed && DistanceToSegment(point, points[points.Count - 1], points[0]) <= tolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsEvenOdd(List<FlattenedContour> contours, SKPoint point)
    {
        var inside = false;
        foreach (var contour in contours)
        {
            if (contour.Points.Count < 3)
            {
                continue;
            }

            if (CrossingCount(contour.Points, point) % 2 != 0)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool ContainsWinding(List<FlattenedContour> contours, SKPoint point)
    {
        var winding = 0;
        foreach (var contour in contours)
        {
            if (contour.Points.Count < 3)
            {
                continue;
            }

            winding += WindingNumber(contour.Points, point);
        }

        return winding != 0;
    }

    private static int CrossingCount(List<SKPoint> points, SKPoint point)
    {
        var crossings = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            if (((a.Y > point.Y) != (b.Y > point.Y)) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                crossings++;
            }
        }

        return crossings;
    }

    private static int WindingNumber(List<SKPoint> points, SKPoint point)
    {
        var winding = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];

            if (a.Y <= point.Y)
            {
                if (b.Y > point.Y && IsLeft(a, b, point) > 0f)
                {
                    winding++;
                }
            }
            else if (b.Y <= point.Y && IsLeft(a, b, point) < 0f)
            {
                winding--;
            }
        }

        return winding;
    }

    private static float IsLeft(SKPoint a, SKPoint b, SKPoint point)
    {
        return ((b.X - a.X) * (point.Y - a.Y)) - ((point.X - a.X) * (b.Y - a.Y));
    }

    private static bool HasClipGeometry(ClipPath? clipPath)
    {
        if (clipPath is null)
        {
            return false;
        }

        if (HasLocalClipGeometry(clipPath))
        {
            return true;
        }

        return HasClipGeometry(clipPath.Clip);
    }

    private static bool HasLocalClipGeometry(ClipPath clipPath)
    {
        if (clipPath.Clips is not { Count: > 0 })
        {
            return false;
        }

        foreach (var clip in clipPath.Clips)
        {
            if (HasPathClipGeometry(clip))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasPathClipGeometry(PathClip? clip)
    {
        if (clip is null)
        {
            return false;
        }

        if (clip.Path?.Commands is { Count: > 0 })
        {
            return true;
        }

        return HasClipGeometry(clip.Clip);
    }

    private static List<FlattenedContour> FlattenPath(SKPath path)
    {
        var contours = new List<FlattenedContour>();
        if (path.Commands is null || path.Commands.Count == 0)
        {
            return contours;
        }

        List<SKPoint>? currentContour = null;
        var current = default(SKPoint);
        var contourStart = default(SKPoint);
        var haveCurrent = false;

        void FinalizeContour(bool closed)
        {
            if (currentContour is { Count: > 0 })
            {
                contours.Add(new FlattenedContour(currentContour, closed));
            }

            currentContour = null;
        }

        void BeginContour(SKPoint start)
        {
            currentContour = new List<SKPoint> { start };
            contourStart = start;
            current = start;
            haveCurrent = true;
        }

        void EnsureContour()
        {
            if (!haveCurrent)
            {
                return;
            }

            currentContour ??= new List<SKPoint> { current };
        }

        void AddPoint(SKPoint point)
        {
            EnsureContour();

            if (currentContour is { Count: > 0 })
            {
                var last = currentContour[currentContour.Count - 1];
                if (!NearlyEquals(last, point))
                {
                    currentContour.Add(point);
                }
            }
            else
            {
                currentContour = new List<SKPoint> { point };
            }

            current = point;
            haveCurrent = true;
        }

        foreach (var command in path.Commands)
        {
            switch (command)
            {
                case MoveToPathCommand moveTo:
                    FinalizeContour(false);
                    BeginContour(new SKPoint(moveTo.X, moveTo.Y));
                    break;
                case LineToPathCommand lineTo:
                    if (!haveCurrent)
                    {
                        BeginContour(new SKPoint(lineTo.X, lineTo.Y));
                        break;
                    }

                    AddPoint(new SKPoint(lineTo.X, lineTo.Y));
                    break;
                case QuadToPathCommand quadTo:
                    if (!haveCurrent)
                    {
                        BeginContour(new SKPoint(quadTo.X1, quadTo.Y1));
                        break;
                    }

                    EnsureContour();
                    if (currentContour is { })
                    {
                        AppendQuadratic(currentContour, current, new SKPoint(quadTo.X0, quadTo.Y0), new SKPoint(quadTo.X1, quadTo.Y1));
                    }

                    current = new SKPoint(quadTo.X1, quadTo.Y1);
                    break;
                case CubicToPathCommand cubicTo:
                    if (!haveCurrent)
                    {
                        BeginContour(new SKPoint(cubicTo.X2, cubicTo.Y2));
                        break;
                    }

                    EnsureContour();
                    if (currentContour is { })
                    {
                        AppendCubic(
                            currentContour,
                            current,
                            new SKPoint(cubicTo.X0, cubicTo.Y0),
                            new SKPoint(cubicTo.X1, cubicTo.Y1),
                            new SKPoint(cubicTo.X2, cubicTo.Y2));
                    }

                    current = new SKPoint(cubicTo.X2, cubicTo.Y2);
                    break;
                case ArcToPathCommand arcTo:
                    if (!haveCurrent)
                    {
                        BeginContour(new SKPoint(arcTo.X, arcTo.Y));
                        break;
                    }

                    EnsureContour();
                    if (currentContour is { })
                    {
                        AppendArc(
                            currentContour,
                            current,
                            new SKPoint(arcTo.X, arcTo.Y),
                            arcTo.Rx,
                            arcTo.Ry,
                            arcTo.XAxisRotate,
                            arcTo.LargeArc,
                            arcTo.Sweep);
                    }

                    current = new SKPoint(arcTo.X, arcTo.Y);
                    break;
                case ClosePathCommand:
                    if (currentContour is { })
                    {
                        FinalizeContour(true);
                        current = contourStart;
                        haveCurrent = true;
                    }
                    break;
                case AddRectPathCommand addRect:
                    FinalizeContour(false);
                    contours.Add(CreateRectangleContour(addRect.Rect));
                    current = addRect.Rect.BottomRight;
                    contourStart = addRect.Rect.TopLeft;
                    haveCurrent = true;
                    break;
                case AddRoundRectPathCommand addRoundRect:
                    FinalizeContour(false);
                    contours.Add(CreateRoundRectContour(addRoundRect.Rect, addRoundRect.Rx, addRoundRect.Ry));
                    current = addRoundRect.Rect.BottomRight;
                    contourStart = addRoundRect.Rect.TopLeft;
                    haveCurrent = true;
                    break;
                case AddOvalPathCommand addOval:
                    FinalizeContour(false);
                    contours.Add(CreateEllipseContour(addOval.Rect));
                    current = addOval.Rect.BottomRight;
                    contourStart = addOval.Rect.TopLeft;
                    haveCurrent = true;
                    break;
                case AddCirclePathCommand addCircle:
                    FinalizeContour(false);
                    contours.Add(CreateEllipseContour(SKRect.Create(
                        addCircle.X - addCircle.Radius,
                        addCircle.Y - addCircle.Radius,
                        addCircle.Radius * 2f,
                        addCircle.Radius * 2f)));
                    current = new SKPoint(addCircle.X + addCircle.Radius, addCircle.Y);
                    contourStart = new SKPoint(addCircle.X + addCircle.Radius, addCircle.Y);
                    haveCurrent = true;
                    break;
                case AddPolyPathCommand addPoly when addPoly.Points is { Count: > 0 } points:
                    FinalizeContour(false);
                    contours.Add(CreatePolygonContour(points, addPoly.Close));
                    current = points[points.Count - 1];
                    contourStart = points[0];
                    haveCurrent = true;
                    break;
            }
        }

        FinalizeContour(false);

        return contours;
    }

    private static FlattenedContour CreateRectangleContour(SKRect rect)
    {
        var points = new List<SKPoint>
        {
            rect.TopLeft,
            rect.TopRight,
            rect.BottomRight,
            rect.BottomLeft
        };

        return new FlattenedContour(points, closed: true);
    }

    private static FlattenedContour CreatePolygonContour(IList<SKPoint> points, bool closed)
    {
        var contourPoints = new List<SKPoint>(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            contourPoints.Add(points[i]);
        }

        return new FlattenedContour(contourPoints, closed);
    }

    private static FlattenedContour CreateRoundRectContour(SKRect rect, float rx, float ry)
    {
        rx = Math.Min(Math.Abs(rx), rect.Width / 2f);
        ry = Math.Min(Math.Abs(ry), rect.Height / 2f);

        if (rx <= float.Epsilon || ry <= float.Epsilon)
        {
            return CreateRectangleContour(rect);
        }

        var points = new List<SKPoint>
        {
            new(rect.Left + rx, rect.Top),
            new(rect.Right - rx, rect.Top)
        };

        AppendEllipseArc(points, rect.Right - rx, rect.Top + ry, rx, ry, -(float)Math.PI / 2f, (float)Math.PI / 2f);
        points.Add(new SKPoint(rect.Right, rect.Bottom - ry));
        AppendEllipseArc(points, rect.Right - rx, rect.Bottom - ry, rx, ry, 0f, (float)Math.PI / 2f);
        points.Add(new SKPoint(rect.Left + rx, rect.Bottom));
        AppendEllipseArc(points, rect.Left + rx, rect.Bottom - ry, rx, ry, (float)Math.PI / 2f, (float)Math.PI / 2f);
        points.Add(new SKPoint(rect.Left, rect.Top + ry));
        AppendEllipseArc(points, rect.Left + rx, rect.Top + ry, rx, ry, (float)Math.PI, (float)Math.PI / 2f);

        return new FlattenedContour(points, closed: true);
    }

    private static FlattenedContour CreateEllipseContour(SKRect rect)
    {
        var rx = rect.Width / 2f;
        var ry = rect.Height / 2f;
        var cx = rect.Left + rx;
        var cy = rect.Top + ry;
        var steps = ClampSteps((int)Math.Ceiling(ApproximateEllipseCircumference(rx, ry) / 6f), MinEllipseSteps, MaxEllipseSteps);
        var points = new List<SKPoint>(steps);

        for (var i = 0; i < steps; i++)
        {
            var angle = FullCircleRadians * i / steps;
            points.Add(new SKPoint(
                cx + rx * (float)Math.Cos(angle),
                cy + ry * (float)Math.Sin(angle)));
        }

        return new FlattenedContour(points, closed: true);
    }

    private static void AppendQuadratic(List<SKPoint> points, SKPoint p0, SKPoint p1, SKPoint p2)
    {
        var steps = ClampSteps((int)Math.Ceiling((Distance(p0, p1) + Distance(p1, p2)) / 4f), MinCurveSteps, MaxCurveSteps);
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            points.Add(EvaluateQuadratic(p0, p1, p2, t));
        }
    }

    private static void AppendCubic(List<SKPoint> points, SKPoint p0, SKPoint p1, SKPoint p2, SKPoint p3)
    {
        var approxLength = Distance(p0, p1) + Distance(p1, p2) + Distance(p2, p3);
        var steps = ClampSteps((int)Math.Ceiling(approxLength / 4f), MinCurveSteps, MaxCurveSteps);
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            points.Add(EvaluateCubic(p0, p1, p2, p3, t));
        }
    }

    private static void AppendArc(
        List<SKPoint> points,
        SKPoint start,
        SKPoint end,
        float rx,
        float ry,
        float angle,
        SKPathArcSize largeArc,
        SKPathDirection sweep)
    {
        if (!TryGetArcParameters(start, end, rx, ry, angle, largeArc, sweep, out var parameters))
        {
            points.Add(end);
            return;
        }

        var approxLength = Math.Abs(parameters.DeltaAngle) * Math.Max(parameters.Rx, parameters.Ry);
        var steps = ClampSteps((int)Math.Ceiling(approxLength / 4f), 6, MaxEllipseSteps);
        for (var i = 1; i <= steps; i++)
        {
            var theta = parameters.StartAngle + parameters.DeltaAngle * i / steps;
            var cosTheta = (float)Math.Cos(theta);
            var sinTheta = (float)Math.Sin(theta);
            points.Add(new SKPoint(
                parameters.CosPhi * parameters.Rx * cosTheta - parameters.SinPhi * parameters.Ry * sinTheta + parameters.Center.X,
                parameters.SinPhi * parameters.Rx * cosTheta + parameters.CosPhi * parameters.Ry * sinTheta + parameters.Center.Y));
        }
    }

    private static void AppendEllipseArc(List<SKPoint> points, float cx, float cy, float rx, float ry, float startAngle, float sweepAngle)
    {
        var approxLength = Math.Abs(sweepAngle) * Math.Max(rx, ry);
        var steps = ClampSteps((int)Math.Ceiling(approxLength / 4f), 4, MaxEllipseSteps);
        for (var i = 1; i <= steps; i++)
        {
            var theta = startAngle + sweepAngle * i / steps;
            var point = new SKPoint(
                cx + rx * (float)Math.Cos(theta),
                cy + ry * (float)Math.Sin(theta));

            if (!NearlyEquals(points[points.Count - 1], point))
            {
                points.Add(point);
            }
        }
    }

    private static SKPoint EvaluateQuadratic(SKPoint p0, SKPoint p1, SKPoint p2, float t)
    {
        var mt = 1f - t;
        return new SKPoint(
            mt * mt * p0.X + 2f * mt * t * p1.X + t * t * p2.X,
            mt * mt * p0.Y + 2f * mt * t * p1.Y + t * t * p2.Y);
    }

    private static SKPoint EvaluateCubic(SKPoint p0, SKPoint p1, SKPoint p2, SKPoint p3, float t)
    {
        var mt = 1f - t;
        var mt2 = mt * mt;
        var t2 = t * t;
        return new SKPoint(
            mt2 * mt * p0.X + 3f * mt2 * t * p1.X + 3f * mt * t2 * p2.X + t2 * t * p3.X,
            mt2 * mt * p0.Y + 3f * mt2 * t * p1.Y + 3f * mt * t2 * p2.Y + t2 * t * p3.Y);
    }

    private static bool TryGetArcParameters(
        SKPoint start,
        SKPoint end,
        float rx,
        float ry,
        float angle,
        SKPathArcSize largeArc,
        SKPathDirection sweep,
        out ArcParameters parameters)
    {
        parameters = default;

        rx = Math.Abs(rx);
        ry = Math.Abs(ry);
        if (rx <= float.Epsilon || ry <= float.Epsilon || NearlyEquals(start, end))
        {
            return false;
        }

        var phi = angle * (float)Math.PI / 180f;
        var cosPhi = (float)Math.Cos(phi);
        var sinPhi = (float)Math.Sin(phi);

        var dx2 = (start.X - end.X) / 2f;
        var dy2 = (start.Y - end.Y) / 2f;

        var x1p = cosPhi * dx2 + sinPhi * dy2;
        var y1p = -sinPhi * dx2 + cosPhi * dy2;

        var rxsq = rx * rx;
        var rysq = ry * ry;
        var x1psq = x1p * x1p;
        var y1psq = y1p * y1p;

        var lambda = x1psq / rxsq + y1psq / rysq;
        if (lambda > 1f)
        {
            var factor = (float)Math.Sqrt(lambda);
            rx *= factor;
            ry *= factor;
            rxsq = rx * rx;
            rysq = ry * ry;
        }

        var denominator = rxsq * y1psq + rysq * x1psq;
        if (denominator <= float.Epsilon)
        {
            return false;
        }

        var sign = (largeArc == SKPathArcSize.Large) == (sweep == SKPathDirection.Clockwise) ? -1f : 1f;
        var sq = (rxsq * rysq - rxsq * y1psq - rysq * x1psq) / denominator;
        sq = Math.Max(sq, 0f);
        var coef = sign * (float)Math.Sqrt(sq);
        var cxp = coef * (rx * y1p / ry);
        var cyp = coef * (-ry * x1p / rx);

        var center = new SKPoint(
            cosPhi * cxp - sinPhi * cyp + (start.X + end.X) / 2f,
            sinPhi * cxp + cosPhi * cyp + (start.Y + end.Y) / 2f);

        var startAngle = (float)Math.Atan2((y1p - cyp) / ry, (x1p - cxp) / rx);
        var endAngle = (float)Math.Atan2((-y1p - cyp) / ry, (-x1p - cxp) / rx);
        var deltaAngle = endAngle - startAngle;
        if (sweep != SKPathDirection.Clockwise && deltaAngle > 0f)
        {
            deltaAngle -= FullCircleRadians;
        }
        else if (sweep == SKPathDirection.Clockwise && deltaAngle < 0f)
        {
            deltaAngle += FullCircleRadians;
        }

        parameters = new ArcParameters(center, rx, ry, startAngle, deltaAngle, cosPhi, sinPhi);
        return true;
    }

    private static float ApproximateEllipseCircumference(float rx, float ry)
    {
        var h = (float)(Math.Pow(rx - ry, 2f) / Math.Pow(rx + ry, 2f));
        return (float)Math.PI * (rx + ry) * (1f + (3f * h) / (10f + (float)Math.Sqrt(4f - 3f * h)));
    }

    private static int ClampSteps(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static float Distance(SKPoint a, SKPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private static float DistanceToSegment(SKPoint point, SKPoint a, SKPoint b)
    {
        var vx = b.X - a.X;
        var vy = b.Y - a.Y;
        var ux = point.X - a.X;
        var uy = point.Y - a.Y;
        var lengthSquared = vx * vx + vy * vy;
        if (lengthSquared <= float.Epsilon)
        {
            return Distance(point, a);
        }

        var t = (ux * vx + uy * vy) / lengthSquared;
        if (t < 0f)
        {
            t = 0f;
        }
        else if (t > 1f)
        {
            t = 1f;
        }

        var px = a.X + t * vx;
        var py = a.Y + t * vy;
        var dx = point.X - px;
        var dy = point.Y - py;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private static bool NearlyEquals(SKPoint a, SKPoint b)
    {
        return Math.Abs(a.X - b.X) <= 0.001f &&
               Math.Abs(a.Y - b.Y) <= 0.001f;
    }

    private readonly record struct ArcParameters(
        SKPoint Center,
        float Rx,
        float Ry,
        float StartAngle,
        float DeltaAngle,
        float CosPhi,
        float SinPhi);
}
