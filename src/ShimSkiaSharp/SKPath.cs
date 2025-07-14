// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;

namespace ShimSkiaSharp;

public abstract record PathCommand;

public record AddCirclePathCommand(float X, float Y, float Radius) : PathCommand;

public record AddOvalPathCommand(SKRect Rect) : PathCommand;

public record AddPolyPathCommand(IList<SKPoint>? Points, bool Close) : PathCommand;

public record AddRectPathCommand(SKRect Rect) : PathCommand;

public record AddRoundRectPathCommand(SKRect Rect, float Rx, float Ry) : PathCommand;

public record ArcToPathCommand(float Rx, float Ry, float XAxisRotate, SKPathArcSize LargeArc, SKPathDirection Sweep, float X, float Y) : PathCommand;

public record ClosePathCommand : PathCommand;

public record CubicToPathCommand(float X0, float Y0, float X1, float Y1, float X2, float Y2) : PathCommand;

public record LineToPathCommand(float X, float Y) : PathCommand;

public record MoveToPathCommand(float X, float Y) : PathCommand;

public record QuadToPathCommand(float X0, float Y0, float X1, float Y1) : PathCommand;

public class SKPath
{
    public SKPathFillType FillType { get; set; }

    public IList<PathCommand>? Commands { get; private set; }

    public bool IsEmpty => Commands is null || Commands.Count == 0;

    public SKRect Bounds => GetBounds();

    public SKPath()
    {
        Commands = new List<PathCommand>();
    }

    private static void ComputePointBounds(float x, float y, ref SKRect bounds)
    {
        bounds.Left = Math.Min(x, bounds.Left);
        bounds.Right = Math.Max(x, bounds.Right);
        bounds.Top = Math.Min(y, bounds.Top);
        bounds.Bottom = Math.Max(y, bounds.Bottom);
    }

    private static void AddLineBounds(float x0, float y0, float x1, float y1, ref SKRect bounds)
    {
        if (x0 < x1)
        {
            bounds.Left = Math.Min(x0, bounds.Left);
            bounds.Right = Math.Max(x1, bounds.Right);
        }
        else
        {
            bounds.Left = Math.Min(x1, bounds.Left);
            bounds.Right = Math.Max(x0, bounds.Right);
        }

        if (y0 < y1)
        {
            bounds.Top = Math.Min(y0, bounds.Top);
            bounds.Bottom = Math.Max(y1, bounds.Bottom);
        }
        else
        {
            bounds.Top = Math.Min(y1, bounds.Top);
            bounds.Bottom = Math.Max(y0, bounds.Bottom);
        }
    }

    private static float Quad(float a, float b, float c, float t)
    {
        var mt = 1f - t;
        return mt * mt * a + 2f * mt * t * b + t * t * c;
    }

    private static void AddQuadBounds(SKPoint p0, SKPoint p1, SKPoint p2, ref SKRect bounds)
    {
        ComputePointBounds(p0.X, p0.Y, ref bounds);
        ComputePointBounds(p2.X, p2.Y, ref bounds);

        var denomX = p0.X - 2f * p1.X + p2.X;
        if (Math.Abs(denomX) > float.Epsilon)
        {
            var t = (p0.X - p1.X) / denomX;
            if (t > 0f && t < 1f)
            {
                var x = Quad(p0.X, p1.X, p2.X, t);
                var y = Quad(p0.Y, p1.Y, p2.Y, t);
                ComputePointBounds(x, y, ref bounds);
            }
        }

        var denomY = p0.Y - 2f * p1.Y + p2.Y;
        if (Math.Abs(denomY) > float.Epsilon)
        {
            var t = (p0.Y - p1.Y) / denomY;
            if (t > 0f && t < 1f)
            {
                var x = Quad(p0.X, p1.X, p2.X, t);
                var y = Quad(p0.Y, p1.Y, p2.Y, t);
                ComputePointBounds(x, y, ref bounds);
            }
        }
    }

    private static float Cubic(float a, float b, float c, float d, float t)
    {
        var mt = 1f - t;
        return mt * mt * mt * a + 3f * mt * mt * t * b + 3f * mt * t * t * c + t * t * t * d;
    }

    private static IEnumerable<float> SolveCubicDerivative(float a, float b, float c, float d)
    {
        var A = -a + 3f * b - 3f * c + d;
        var B = 2f * (a - 2f * b + c);
        var C = b - a;

        if (Math.Abs(A) < float.Epsilon)
        {
            if (Math.Abs(B) < float.Epsilon)
                yield break;

            var t = -C / B;
            if (t > 0f && t < 1f)
                yield return t;
            yield break;
        }

        var discriminant = B * B - 4f * A * C;
        if (discriminant < 0f)
            yield break;

        var sqrt = (float)Math.Sqrt(discriminant);
        var q = -B / (2f * A);
        var r = sqrt / (2f * A);

        var t1 = q + r;
        if (t1 > 0f && t1 < 1f)
            yield return t1;

        var t2 = q - r;
        if (t2 > 0f && t2 < 1f)
            yield return t2;
    }

    private static void AddCubicBounds(SKPoint p0, SKPoint p1, SKPoint p2, SKPoint p3, ref SKRect bounds)
    {
        ComputePointBounds(p0.X, p0.Y, ref bounds);
        ComputePointBounds(p3.X, p3.Y, ref bounds);

        foreach (var t in SolveCubicDerivative(p0.X, p1.X, p2.X, p3.X))
        {
            var x = Cubic(p0.X, p1.X, p2.X, p3.X, t);
            var y = Cubic(p0.Y, p1.Y, p2.Y, p3.Y, t);
            ComputePointBounds(x, y, ref bounds);
        }

        foreach (var t in SolveCubicDerivative(p0.Y, p1.Y, p2.Y, p3.Y))
        {
            var x = Cubic(p0.X, p1.X, p2.X, p3.X, t);
            var y = Cubic(p0.Y, p1.Y, p2.Y, p3.Y, t);
            ComputePointBounds(x, y, ref bounds);
        }
    }

    private static void AddArcBounds(SKPoint p0, SKPoint p1, float rx, float ry, float angle, SKPathArcSize largeArc, SKPathDirection sweep, ref SKRect bounds)
    {
        if (rx <= 0f || ry <= 0f)
        {
            ComputePointBounds(p0.X, p0.Y, ref bounds);
            ComputePointBounds(p1.X, p1.Y, ref bounds);
            return;
        }

        var phi = angle * (float)Math.PI / 180f;
        var cosPhi = (float)Math.Cos(phi);
        var sinPhi = (float)Math.Sin(phi);

        var dx2 = (p0.X - p1.X) / 2f;
        var dy2 = (p0.Y - p1.Y) / 2f;

        var x1p = cosPhi * dx2 + sinPhi * dy2;
        var y1p = -sinPhi * dx2 + cosPhi * dy2;

        rx = Math.Abs(rx);
        ry = Math.Abs(ry);

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

        var sign = (largeArc == SKPathArcSize.Large) == (sweep == SKPathDirection.Clockwise) ? -1f : 1f;
        var sq = (rxsq * rysq - rxsq * y1psq - rysq * x1psq) / (rxsq * y1psq + rysq * x1psq);
        sq = Math.Max(sq, 0f);
        var coef = sign * (float)Math.Sqrt(sq);
        var cxp = coef * (rx * y1p / ry);
        var cyp = coef * (-ry * x1p / rx);

        var cx = cosPhi * cxp - sinPhi * cyp + (p0.X + p1.X) / 2f;
        var cy = sinPhi * cxp + cosPhi * cyp + (p0.Y + p1.Y) / 2f;

        var startAngle = (float)Math.Atan2((y1p - cyp) / ry, (x1p - cxp) / rx);
        var endAngle = (float)Math.Atan2((-y1p - cyp) / ry, (-x1p - cxp) / rx);
        var sweepFlag = sweep == SKPathDirection.Clockwise;
        var deltaAngle = endAngle - startAngle;
        if (!sweepFlag && deltaAngle > 0)
            deltaAngle -= 2f * (float)Math.PI;
        else if (sweepFlag && deltaAngle < 0)
            deltaAngle += 2f * (float)Math.PI;

        const int segments = 20;
        for (var i = 0; i <= segments; i++)
        {
            var theta = startAngle + deltaAngle * i / segments;
            var cosTheta = (float)Math.Cos(theta);
            var sinTheta = (float)Math.Sin(theta);
            var x = cosPhi * rx * cosTheta - sinPhi * ry * sinTheta + cx;
            var y = sinPhi * rx * cosTheta + cosPhi * ry * sinTheta + cy;
            ComputePointBounds(x, y, ref bounds);
        }
    }

    private SKRect GetBounds()
    {
        if (Commands is null || Commands.Count == 0)
        {
            return SKRect.Empty;
        }

        var bounds = new SKRect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

        var last = new SKPoint();
        var haveLast = false;

        foreach (var pathCommand in Commands)
        {
            switch (pathCommand)
            {
                case MoveToPathCommand moveToPathCommand:
                {
                    var x = moveToPathCommand.X;
                    var y = moveToPathCommand.Y;
                    ComputePointBounds(x, y, ref bounds);
                    last = new SKPoint(x, y);
                    haveLast = true;
                }
                    break;
                case LineToPathCommand lineToPathCommand:
                {
                    var x = lineToPathCommand.X;
                    var y = lineToPathCommand.Y;
                    if (haveLast)
                    {
                        AddLineBounds(last.X, last.Y, x, y, ref bounds);
                    }
                    else
                    {
                        ComputePointBounds(x, y, ref bounds);
                    }
                    last = new SKPoint(x, y);
                    haveLast = true;
                }
                    break;
                case ArcToPathCommand arcToPathCommand:
                {
                    var end = new SKPoint(arcToPathCommand.X, arcToPathCommand.Y);
                    if (haveLast)
                    {
                        AddArcBounds(last, end, arcToPathCommand.Rx, arcToPathCommand.Ry, arcToPathCommand.XAxisRotate, arcToPathCommand.LargeArc, arcToPathCommand.Sweep, ref bounds);
                    }
                    else
                    {
                        ComputePointBounds(end.X, end.Y, ref bounds);
                    }
                    last = end;
                    haveLast = true;
                }
                    break;
                case QuadToPathCommand quadToPathCommand:
                {
                    var p1 = new SKPoint(quadToPathCommand.X0, quadToPathCommand.Y0);
                    var p2 = new SKPoint(quadToPathCommand.X1, quadToPathCommand.Y1);
                    if (haveLast)
                    {
                        AddQuadBounds(last, p1, p2, ref bounds);
                    }
                    else
                    {
                        ComputePointBounds(p1.X, p1.Y, ref bounds);
                        ComputePointBounds(p2.X, p2.Y, ref bounds);
                    }
                    last = p2;
                    haveLast = true;
                }
                    break;
                case CubicToPathCommand cubicToPathCommand:
                {
                    var p1 = new SKPoint(cubicToPathCommand.X0, cubicToPathCommand.Y0);
                    var p2 = new SKPoint(cubicToPathCommand.X1, cubicToPathCommand.Y1);
                    var p3 = new SKPoint(cubicToPathCommand.X2, cubicToPathCommand.Y2);
                    if (haveLast)
                    {
                        AddCubicBounds(last, p1, p2, p3, ref bounds);
                    }
                    else
                    {
                        ComputePointBounds(p1.X, p1.Y, ref bounds);
                        ComputePointBounds(p2.X, p2.Y, ref bounds);
                        ComputePointBounds(p3.X, p3.Y, ref bounds);
                    }
                    last = p3;
                    haveLast = true;
                }
                    break;
                case ClosePathCommand _:
                    break;
                case AddRectPathCommand addRectPathCommand:
                {
                    var rect = addRectPathCommand.Rect;
                    ComputePointBounds(rect.Left, rect.Top, ref bounds);
                    ComputePointBounds(rect.Right, rect.Bottom, ref bounds);
                    last = rect.BottomRight;
                    haveLast = true;
                }
                    break;
                case AddRoundRectPathCommand addRoundRectPathCommand:
                {
                    var rect = addRoundRectPathCommand.Rect;
                    ComputePointBounds(rect.Left, rect.Top, ref bounds);
                    ComputePointBounds(rect.Right, rect.Bottom, ref bounds);
                    last = rect.BottomRight;
                    haveLast = true;
                }
                    break;
                case AddOvalPathCommand addOvalPathCommand:
                {
                    var rect = addOvalPathCommand.Rect;
                    ComputePointBounds(rect.Left, rect.Top, ref bounds);
                    ComputePointBounds(rect.Right, rect.Bottom, ref bounds);
                    last = rect.BottomRight;
                    haveLast = true;
                }
                    break;
                case AddCirclePathCommand addCirclePathCommand:
                {
                    var x = addCirclePathCommand.X;
                    var y = addCirclePathCommand.Y;
                    var radius = addCirclePathCommand.Radius;
                    ComputePointBounds(x - radius, y - radius, ref bounds);
                    ComputePointBounds(x + radius, y + radius, ref bounds);
                    last = new SKPoint(x + radius, y + radius);
                    haveLast = true;
                }
                    break;
                case AddPolyPathCommand addPolyPathCommand:
                {
                    if (addPolyPathCommand.Points is { })
                    {
                        var points = addPolyPathCommand.Points;
                        foreach (var point in points)
                        {
                            ComputePointBounds(point.X, point.Y, ref bounds);
                        }
                        if (points.Count > 0)
                        {
                            last = points[points.Count - 1];
                            haveLast = true;
                        }
                    }
                }
                    break;
            }
        }

        return bounds;
    }

    public void MoveTo(float x, float y) 
        => Commands?.Add(new MoveToPathCommand(x, y));

    public void LineTo(float x, float y) 
        => Commands?.Add(new LineToPathCommand(x, y));

    public void ArcTo(float rx, float ry, float xAxisRotate, SKPathArcSize largeArc, SKPathDirection sweep, float x, float y) 
        => Commands?.Add(new ArcToPathCommand(rx, ry, xAxisRotate, largeArc, sweep, x, y));

    public void QuadTo(float x0, float y0, float x1, float y1) 
        => Commands?.Add(new QuadToPathCommand(x0, y0, x1, y1));

    public void CubicTo(float x0, float y0, float x1, float y1, float x2, float y2) 
        => Commands?.Add(new CubicToPathCommand(x0, y0, x1, y1, x2, y2));

    public void Close() 
        => Commands?.Add(new ClosePathCommand());

    public void AddRect(SKRect rect) 
        => Commands?.Add(new AddRectPathCommand(rect));

    public void AddRoundRect(SKRect rect, float rx, float ry) 
        => Commands?.Add(new AddRoundRectPathCommand(rect, rx, ry));

    public void AddOval(SKRect rect) 
        => Commands?.Add(new AddOvalPathCommand(rect));

    public void AddCircle(float x, float y, float radius) 
        => Commands?.Add(new AddCirclePathCommand(x, y, radius));

    public void AddPoly(SKPoint[] points, bool close = true) 
        => Commands?.Add(new AddPolyPathCommand(points, close));
}
