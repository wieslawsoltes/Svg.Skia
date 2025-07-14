// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;

namespace ShimSkiaSharp;

internal static class SKPathBoundsHelper
{
    public static void ComputePointBounds(float x, float y, ref SKRect bounds)
    {
        bounds.Left = Math.Min(x, bounds.Left);
        bounds.Right = Math.Max(x, bounds.Right);
        bounds.Top = Math.Min(y, bounds.Top);
        bounds.Bottom = Math.Max(y, bounds.Bottom);
    }

    public static void AddLineBounds(float x0, float y0, float x1, float y1, ref SKRect bounds)
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

    public static void AddQuadBounds(SKPoint p0, SKPoint p1, SKPoint p2, ref SKRect bounds)
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

    public static void AddCubicBounds(SKPoint p0, SKPoint p1, SKPoint p2, SKPoint p3, ref SKRect bounds)
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

    public static void AddArcBounds(SKPoint p0, SKPoint p1, float rx, float ry, float angle, SKPathArcSize largeArc, SKPathDirection sweep, ref SKRect bounds)
    {
        ComputePointBounds(p0.X, p0.Y, ref bounds);
        ComputePointBounds(p1.X, p1.Y, ref bounds);

        if (rx <= 0f || ry <= 0f)
        {
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

        static float NormalizeAngle(float a)
        {
            var twoPi = 2f * (float)Math.PI;
            a %= twoPi;
            if (a < 0f)
                a += twoPi;
            return a;
        }

        static bool IsAngleOnArc(float angle, float start, float sweep)
        {
            var normStart = NormalizeAngle(start);
            var normEnd = NormalizeAngle(start + sweep);
            var normAngle = NormalizeAngle(angle);

            if (sweep >= 0f)
            {
                if (normStart <= normEnd)
                    return normAngle >= normStart && normAngle <= normEnd;
                return normAngle >= normStart || normAngle <= normEnd;
            }
            else
            {
                if (normEnd <= normStart)
                    return normAngle <= normStart && normAngle >= normEnd;
                return normAngle <= normStart || normAngle >= normEnd;
            }
        }

        var candidates = new float[] { 0f, (float)Math.PI / 2f, (float)Math.PI, 3f * (float)Math.PI / 2f };

        foreach (var theta in candidates)
        {
            if (!IsAngleOnArc(theta, startAngle, deltaAngle))
                continue;

            var cosTheta = (float)Math.Cos(theta);
            var sinTheta = (float)Math.Sin(theta);
            var x = cosPhi * rx * cosTheta - sinPhi * ry * sinTheta + cx;
            var y = sinPhi * rx * cosTheta + cosPhi * ry * sinTheta + cy;
            ComputePointBounds(x, y, ref bounds);
        }
    }
}
