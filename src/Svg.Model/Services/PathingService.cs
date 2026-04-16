// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.Pathing;

namespace Svg.Model.Services;

internal static class PathingService
{
    private const int SharedSvgPathDataCacheLimit = 2048;
    private const int SharedShapePathCacheLimit = 1024;

    [Flags]
    internal enum PathPointType : byte
    {
        Start = 0,
        Line = 1,
        Bezier = 3,
        Bezier3 = 3,
        PathTypeMask = 0x7,
        DashMode = 0x10,
        PathMarker = 0x20,
        CloseSubpath = 0x80
    }

    private readonly struct SvgPathDataSignature : IEquatable<SvgPathDataSignature>
    {
        public SvgPathDataSignature(SceneGraphPathDataHash pathDataHash, SvgFillRule fillRule)
        {
            SegmentCount = pathDataHash.SegmentCount;
            FillRule = fillRule;
            Hash1 = pathDataHash.Hash1;
            Hash2 = pathDataHash.Hash2;
        }

        public int SegmentCount { get; }
        public SvgFillRule FillRule { get; }
        public int Hash1 { get; }
        public int Hash2 { get; }

        public bool Equals(SvgPathDataSignature other)
        {
            return SegmentCount == other.SegmentCount &&
                   FillRule == other.FillRule &&
                   Hash1 == other.Hash1 &&
                   Hash2 == other.Hash2;
        }

        public override bool Equals(object? obj)
        {
            return obj is SvgPathDataSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SegmentCount;
                hash = (hash * 397) ^ (int)FillRule;
                hash = (hash * 397) ^ Hash1;
                hash = (hash * 397) ^ Hash2;
                return hash;
            }
        }
    }

    private static readonly ConcurrentDictionary<SvgPathDataSignature, SKPath> s_sharedSvgPathDataCache = new();

    private enum SharedShapePathKind : byte
    {
        Rectangle = 1,
        Circle = 2,
        Ellipse = 3,
        Line = 4
    }

    private readonly struct SharedShapePathSignature : IEquatable<SharedShapePathSignature>
    {
        public SharedShapePathSignature(
            SharedShapePathKind kind,
            SvgFillRule fillRule,
            float value0,
            float value1,
            float value2,
            float value3,
            float value4,
            float value5)
        {
            Kind = kind;
            FillRule = fillRule;
            Value0 = value0;
            Value1 = value1;
            Value2 = value2;
            Value3 = value3;
            Value4 = value4;
            Value5 = value5;
        }

        public SharedShapePathKind Kind { get; }
        public SvgFillRule FillRule { get; }
        public float Value0 { get; }
        public float Value1 { get; }
        public float Value2 { get; }
        public float Value3 { get; }
        public float Value4 { get; }
        public float Value5 { get; }

        public bool Equals(SharedShapePathSignature other)
        {
            return Kind == other.Kind &&
                   FillRule == other.FillRule &&
                   Value0.Equals(other.Value0) &&
                   Value1.Equals(other.Value1) &&
                   Value2.Equals(other.Value2) &&
                   Value3.Equals(other.Value3) &&
                   Value4.Equals(other.Value4) &&
                   Value5.Equals(other.Value5);
        }

        public override bool Equals(object? obj)
        {
            return obj is SharedShapePathSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ (int)FillRule;
                hash = (hash * 397) ^ Value0.GetHashCode();
                hash = (hash * 397) ^ Value1.GetHashCode();
                hash = (hash * 397) ^ Value2.GetHashCode();
                hash = (hash * 397) ^ Value3.GetHashCode();
                hash = (hash * 397) ^ Value4.GetHashCode();
                hash = (hash * 397) ^ Value5.GetHashCode();
                return hash;
            }
        }
    }

    private static readonly ConcurrentDictionary<SharedShapePathSignature, SKPath> s_sharedShapePathCache = new();

    internal static List<(SKPoint Point, byte Type)> GetPathTypes(this SKPath path)
    {
        // System.Drawing.Drawing2D.GraphicsPath.PathTypes
        // System.Drawing.Drawing2D.PathPointType
        // byte -> PathPointType
        var pathTypes = new List<(SKPoint Point, byte Type)>();

        if (path.Commands is null)
        {
            return pathTypes;
        }
        (SKPoint Point, byte Type) lastPoint = (default, 0);
        foreach (var pathCommand in path.Commands)
        {
            switch (pathCommand)
            {
                case MoveToPathCommand moveToPathCommand:
                    {
                        var point0 = new SKPoint(moveToPathCommand.X, moveToPathCommand.Y);
                        pathTypes.Add((point0, (byte)PathPointType.Start));
                        lastPoint = (point0, (byte)PathPointType.Start);
                        break;
                    }
                case LineToPathCommand lineToPathCommand:
                    {
                        var point1 = new SKPoint(lineToPathCommand.X, lineToPathCommand.Y);
                        pathTypes.Add((point1, (byte)PathPointType.Line));
                        lastPoint = (point1, (byte)PathPointType.Line);
                        break;
                    }
                case CubicToPathCommand cubicToPathCommand:
                    {
                        var point1 = new SKPoint(cubicToPathCommand.X0, cubicToPathCommand.Y0);
                        var point2 = new SKPoint(cubicToPathCommand.X1, cubicToPathCommand.Y1);
                        var point3 = new SKPoint(cubicToPathCommand.X2, cubicToPathCommand.Y2);
                        pathTypes.Add((point1, (byte)PathPointType.Bezier));
                        pathTypes.Add((point2, (byte)PathPointType.Bezier));
                        pathTypes.Add((point3, (byte)PathPointType.Bezier));
                        lastPoint = (point3, (byte)PathPointType.Bezier);
                        break;
                    }
                case QuadToPathCommand quadToPathCommand:
                    {
                        var point1 = new SKPoint(quadToPathCommand.X0, quadToPathCommand.Y0);
                        var point2 = new SKPoint(quadToPathCommand.X1, quadToPathCommand.Y1);
                        pathTypes.Add((point1, (byte)PathPointType.Bezier));
                        pathTypes.Add((point2, (byte)PathPointType.Bezier));
                        lastPoint = (point2, (byte)PathPointType.Bezier);
                        break;
                    }
                case ArcToPathCommand arcToPathCommand:
                    {
                        var point1 = new SKPoint(arcToPathCommand.X, arcToPathCommand.Y);
                        pathTypes.Add((point1, (byte)PathPointType.Bezier));
                        lastPoint = (point1, (byte)PathPointType.Bezier);
                        break;
                    }
                case ClosePathCommand:
                    {
                        lastPoint = (lastPoint.Point, (byte)(lastPoint.Type | (byte)PathPointType.CloseSubpath));
                        pathTypes[pathTypes.Count - 1] = lastPoint;
                        break;
                    }
                case AddPolyPathCommand addPolyPathCommand:
                    {
                        if (addPolyPathCommand.Points is { } && addPolyPathCommand.Points.Count > 0)
                        {
                            for (var i = 0; i < addPolyPathCommand.Points.Count; i++)
                            {
                                var nextPoint = addPolyPathCommand.Points[i];
                                var type = i == 0
                                    ? (byte)PathPointType.Start
                                    : (byte)PathPointType.Line;
                                var point1 = new SKPoint(nextPoint.X, nextPoint.Y);
                                pathTypes.Add((point1, type));
                            }

                            var point = addPolyPathCommand.Points[addPolyPathCommand.Points.Count - 1];
                            lastPoint = (point, (byte)PathPointType.Line);
                            if (addPolyPathCommand.Close)
                            {
                                lastPoint = (lastPoint.Point, (byte)(lastPoint.Type | (byte)PathPointType.CloseSubpath));
                                pathTypes[pathTypes.Count - 1] = lastPoint;
                            }
                        }
                        break;
                    }
                default:
                    break;
            }
        }

        return pathTypes;
    }

    internal static System.Drawing.PointF Reflect(System.Drawing.PointF point, System.Drawing.PointF mirror)
    {
        var dx = Math.Abs(mirror.X - point.X);
        var dy = Math.Abs(mirror.Y - point.Y);

        var x = mirror.X + (mirror.X >= point.X ? dx : -dx);
        var y = mirror.Y + (mirror.Y >= point.Y ? dy : -dy);

        return new System.Drawing.PointF(x, y);
    }

    internal static System.Drawing.PointF ToAbsolute(System.Drawing.PointF point, bool isRelative, System.Drawing.PointF start)
    {
        if (float.IsNaN(point.X))
        {
            point.X = start.X;
        }
        else if (isRelative)
        {
            point.X += start.X;
        }

        if (float.IsNaN(point.Y))
        {
            point.Y = start.Y;
        }
        else if (isRelative)
        {
            point.Y += start.Y;
        }

        return point;
    }

    internal static SKPath? ToPath(this SvgPathSegmentList? svgPathSegmentList, SvgFillRule svgFillRule)
    {
        return ToPath(svgPathSegmentList, svgFillRule, null);
    }

    internal static SKPath? ToPath(this SvgPathSegmentList? svgPathSegmentList, SvgFillRule svgFillRule, SceneGraphPathDataHash? pathDataHash)
    {
        if (svgPathSegmentList is null || svgPathSegmentList.Count <= 0)
        {
            return default;
        }

        var hash = pathDataHash is { } cachedHash && cachedHash.SegmentCount == svgPathSegmentList.Count
            ? cachedHash
            : SceneGraphPathDataHashFactory.Create(svgPathSegmentList);
        var signature = new SvgPathDataSignature(hash, svgFillRule);
        if (s_sharedSvgPathDataCache.TryGetValue(signature, out var sharedPath))
        {
            return sharedPath.DeepClone();
        }

        var fillType = svgFillRule == SvgFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        var skPath = new SKPath
        {
            FillType = fillType
        };

        var isEndFigure = false;
        var haveFigure = false;
        var start = System.Drawing.PointF.Empty;
        var prevMove = start;
        var lastCubicSecondControlPoint = System.Drawing.PointF.Empty;
        var hasLastCubicSecondControlPoint = false;
        var lastQuadraticControlPoint = System.Drawing.PointF.Empty;
        var hasLastQuadraticControlPoint = false;

        for (var i = 0; i < svgPathSegmentList.Count; i++)
        {
            var svgSegment = svgPathSegmentList[i];
            var isLast = i == svgPathSegmentList.Count - 1;

            switch (svgSegment)
            {
                case SvgMoveToSegment svgMoveToSegment:
                    {
                        if (isEndFigure && haveFigure == false)
                        {
                            return default;
                        }

                        if (isLast)
                        {
                            return skPath;
                        }

                        isEndFigure = true;
                        haveFigure = true;
                        var end = ToAbsolute(svgMoveToSegment.End, svgMoveToSegment.IsRelative, start);
                        skPath.MoveTo(end.X, end.Y);
                        start = end;
                        prevMove = end;
                        hasLastCubicSecondControlPoint = false;
                        hasLastQuadraticControlPoint = false;
                        break;
                    }
                case SvgLineSegment svgLineSegment:
                    {
                        if (isEndFigure == false)
                        {
                            return default;
                        }
                        haveFigure = true;
                        var end = ToAbsolute(svgLineSegment.End, svgLineSegment.IsRelative, start);
                        skPath.LineTo(end.X, end.Y);
                        start = end;
                        hasLastCubicSecondControlPoint = false;
                        hasLastQuadraticControlPoint = false;
                        break;
                    }
                case SvgCubicCurveSegment svgCubicCurveSegment:
                    {
                        if (isEndFigure == false)
                        {
                            return default;
                        }
                        haveFigure = true;

                        var firstControlPoint = svgCubicCurveSegment.FirstControlPoint;
                        if (float.IsNaN(firstControlPoint.X) || float.IsNaN(firstControlPoint.Y))
                        {
                            if (hasLastCubicSecondControlPoint)
                            {
                                firstControlPoint = Reflect(lastCubicSecondControlPoint, start);
                            }
                            else
                            {
                                firstControlPoint = start;
                            }
                        }
                        else
                        {
                            firstControlPoint = ToAbsolute(firstControlPoint, svgCubicCurveSegment.IsRelative, start);
                        }

                        var end = ToAbsolute(svgCubicCurveSegment.End, svgCubicCurveSegment.IsRelative, start);
                        var first = firstControlPoint;
                        var second = ToAbsolute(svgCubicCurveSegment.SecondControlPoint, svgCubicCurveSegment.IsRelative, start);
                        skPath.CubicTo(first.X, first.Y, second.X, second.Y, end.X, end.Y);
                        start = end;
                        lastCubicSecondControlPoint = second;
                        hasLastCubicSecondControlPoint = true;
                        hasLastQuadraticControlPoint = false;
                        break;
                    }
                case SvgQuadraticCurveSegment svgQuadraticCurveSegment:
                    {
                        if (isEndFigure == false)
                        {
                            return default;
                        }
                        haveFigure = true;

                        var controlPoint = svgQuadraticCurveSegment.ControlPoint;
                        if (float.IsNaN(controlPoint.X) || float.IsNaN(controlPoint.Y))
                        {
                            if (hasLastQuadraticControlPoint)
                            {
                                controlPoint = Reflect(lastQuadraticControlPoint, start);
                            }
                            else
                            {
                                controlPoint = start;
                            }
                        }
                        else
                        {
                            controlPoint = ToAbsolute(controlPoint, svgQuadraticCurveSegment.IsRelative, start);
                        }

                        var end = ToAbsolute(svgQuadraticCurveSegment.End, svgQuadraticCurveSegment.IsRelative, start);

                        skPath.QuadTo(controlPoint.X, controlPoint.Y, end.X, end.Y);
                        start = end;
                        lastQuadraticControlPoint = controlPoint;
                        hasLastQuadraticControlPoint = true;
                        hasLastCubicSecondControlPoint = false;
                        break;
                    }
                case SvgArcSegment svgArcSegment:
                    {
                        if (isEndFigure == false)
                        {
                            return default;
                        }
                        haveFigure = true;
                        var rx = svgArcSegment.RadiusX;
                        var ry = svgArcSegment.RadiusY;
                        var xAxisRotate = svgArcSegment.Angle;
                        var largeArc = svgArcSegment.Size == SvgArcSize.Small ? SKPathArcSize.Small : SKPathArcSize.Large;
                        var sweep = svgArcSegment.Sweep == SvgArcSweep.Negative ? SKPathDirection.CounterClockwise : SKPathDirection.Clockwise;
                        var end = ToAbsolute(svgArcSegment.End, svgArcSegment.IsRelative, start);
                        skPath.ArcTo(rx, ry, xAxisRotate, largeArc, sweep, end.X, end.Y);
                        start = end;
                        hasLastCubicSecondControlPoint = false;
                        hasLastQuadraticControlPoint = false;
                        break;
                    }
                case SvgClosePathSegment _:
                    {
                        if (isEndFigure == false && haveFigure == false)
                        {
                            continue;
                        }
                        if (isEndFigure == false)
                        {
                            return default;
                        }
                        if (haveFigure == false)
                        {
                            return default;
                        }
                        isEndFigure = false;
                        haveFigure = false;
                        skPath.Close();
                        start = prevMove;
                        hasLastCubicSecondControlPoint = false;
                        hasLastQuadraticControlPoint = false;
                        break;
                    }
            }
        }

        if (isEndFigure && haveFigure == false)
        {
            return default;
        }

        s_sharedSvgPathDataCache[signature] = skPath.DeepClone();
        TrimSharedSvgPathDataCacheIfNeeded();
        return skPath;
    }

    private static void TrimSharedSvgPathDataCacheIfNeeded()
    {
        if (s_sharedSvgPathDataCache.Count > SharedSvgPathDataCacheLimit)
        {
            s_sharedSvgPathDataCache.Clear();
        }
    }

    internal static SKPath? ToPath(this SvgPointCollection svgPointCollection, SvgFillRule svgFillRule, bool isClosed, SKRect skViewport)
    {
        if (svgPointCollection.Count < 4)
        {
            return null;
        }

        var fillType = svgFillRule == SvgFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        var skPath = new SKPath
        {
            FillType = fillType
        };

        var skPoints = new SKPoint[svgPointCollection.Count / 2];

        for (var i = 0; i + 1 < svgPointCollection.Count; i += 2)
        {
            var x = svgPointCollection[i].ToDeviceValue(UnitRenderingType.Other, null, skViewport);
            var y = svgPointCollection[i + 1].ToDeviceValue(UnitRenderingType.Other, null, skViewport);
            skPoints[i / 2] = new SKPoint(x, y);
        }

        skPath.AddPoly(skPoints, isClosed);

        return skPath;
    }

    internal static SKPath? ToPath(this SvgRectangle svgRectangle, SvgFillRule svgFillRule, SKRect skViewport)
    {
        var x = svgRectangle.X.ToDeviceValue(UnitRenderingType.Horizontal, svgRectangle, skViewport);
        var y = svgRectangle.Y.ToDeviceValue(UnitRenderingType.Vertical, svgRectangle, skViewport);
        var width = svgRectangle.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgRectangle, skViewport);
        var height = svgRectangle.Height.ToDeviceValue(UnitRenderingType.Vertical, svgRectangle, skViewport);
        var rx = svgRectangle.CornerRadiusX.ToDeviceValue(UnitRenderingType.Horizontal, svgRectangle, skViewport);
        var ry = svgRectangle.CornerRadiusY.ToDeviceValue(UnitRenderingType.Vertical, svgRectangle, skViewport);

        if (width <= 0f || height <= 0f)
        {
            return default;
        }

        if (rx < 0f && ry < 0f)
        {
            rx = 0f;
            ry = 0f;
        }

        if (rx == 0f || ry == 0f)
        {
            rx = 0f;
            ry = 0f;
        }

        if (rx < 0f)
        {
            rx = Math.Abs(rx);
        }

        if (ry < 0f)
        {
            ry = Math.Abs(ry);
        }

        if (rx > 0f)
        {
            var halfWidth = width / 2f;
            if (rx > halfWidth)
            {
                rx = halfWidth;
            }
        }

        if (ry > 0f)
        {
            var halfHeight = height / 2f;
            if (ry > halfHeight)
            {
                ry = halfHeight;
            }
        }

        var signature = new SharedShapePathSignature(
            SharedShapePathKind.Rectangle,
            svgFillRule,
            x,
            y,
            width,
            height,
            rx,
            ry);
        if (s_sharedShapePathCache.TryGetValue(signature, out var sharedPath))
        {
            return sharedPath.DeepClone();
        }

        var fillType = svgFillRule == SvgFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        var skPath = new SKPath
        {
            FillType = fillType
        };
        var isRound = rx > 0f && ry > 0f;
        var skRectBounds = SKRect.Create(x, y, width, height);

        if (isRound)
        {
            skPath.AddRoundRect(skRectBounds, rx, ry);
        }
        else
        {
            skPath.AddRect(skRectBounds);
        }

        s_sharedShapePathCache[signature] = skPath.DeepClone();
        TrimSharedShapePathCacheIfNeeded();
        return skPath;
    }

    internal static SKPath? ToPath(this SvgCircle svgCircle, SvgFillRule svgFillRule, SKRect skViewport)
    {
        var cx = svgCircle.CenterX.ToDeviceValue(UnitRenderingType.Horizontal, svgCircle, skViewport);
        var cy = svgCircle.CenterY.ToDeviceValue(UnitRenderingType.Vertical, svgCircle, skViewport);
        var radius = svgCircle.Radius.ToDeviceValue(UnitRenderingType.Other, svgCircle, skViewport);

        if (radius <= 0f)
        {
            return default;
        }

        var signature = new SharedShapePathSignature(
            SharedShapePathKind.Circle,
            svgFillRule,
            cx,
            cy,
            radius,
            0f,
            0f,
            0f);
        if (s_sharedShapePathCache.TryGetValue(signature, out var sharedPath))
        {
            return sharedPath.DeepClone();
        }

        var fillType = svgFillRule == SvgFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        var skPath = new SKPath
        {
            FillType = fillType
        };
        skPath.AddCircle(cx, cy, radius);

        s_sharedShapePathCache[signature] = skPath.DeepClone();
        TrimSharedShapePathCacheIfNeeded();
        return skPath;
    }

    internal static SKPath? ToPath(this SvgEllipse svgEllipse, SvgFillRule svgFillRule, SKRect skViewport)
    {
        var cx = svgEllipse.CenterX.ToDeviceValue(UnitRenderingType.Horizontal, svgEllipse, skViewport);
        var cy = svgEllipse.CenterY.ToDeviceValue(UnitRenderingType.Vertical, svgEllipse, skViewport);
        var rx = svgEllipse.RadiusX.ToDeviceValue(UnitRenderingType.Other, svgEllipse, skViewport);
        var ry = svgEllipse.RadiusY.ToDeviceValue(UnitRenderingType.Other, svgEllipse, skViewport);

        if (rx <= 0f || ry <= 0f)
        {
            return default;
        }

        var signature = new SharedShapePathSignature(
            SharedShapePathKind.Ellipse,
            svgFillRule,
            cx,
            cy,
            rx,
            ry,
            0f,
            0f);
        if (s_sharedShapePathCache.TryGetValue(signature, out var sharedPath))
        {
            return sharedPath.DeepClone();
        }

        var fillType = svgFillRule == SvgFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        var skPath = new SKPath
        {
            FillType = fillType
        };
        var skRectBounds = SKRect.Create(cx - rx, cy - ry, rx + rx, ry + ry);

        skPath.AddOval(skRectBounds);

        s_sharedShapePathCache[signature] = skPath.DeepClone();
        TrimSharedShapePathCacheIfNeeded();
        return skPath;
    }

    internal static SKPath? ToPath(this SvgLine svgLine, SvgFillRule svgFillRule, SKRect skViewport)
    {
        var x0 = svgLine.StartX.ToDeviceValue(UnitRenderingType.Horizontal, svgLine, skViewport);
        var y0 = svgLine.StartY.ToDeviceValue(UnitRenderingType.Vertical, svgLine, skViewport);
        var x1 = svgLine.EndX.ToDeviceValue(UnitRenderingType.Horizontal, svgLine, skViewport);
        var y1 = svgLine.EndY.ToDeviceValue(UnitRenderingType.Vertical, svgLine, skViewport);

        var signature = new SharedShapePathSignature(
            SharedShapePathKind.Line,
            svgFillRule,
            x0,
            y0,
            x1,
            y1,
            0f,
            0f);
        if (s_sharedShapePathCache.TryGetValue(signature, out var sharedPath))
        {
            return sharedPath.DeepClone();
        }

        var fillType = svgFillRule == SvgFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        var skPath = new SKPath
        {
            FillType = fillType
        };
        skPath.MoveTo(x0, y0);
        skPath.LineTo(x1, y1);

        s_sharedShapePathCache[signature] = skPath.DeepClone();
        TrimSharedShapePathCacheIfNeeded();
        return skPath;
    }

    private static void TrimSharedShapePathCacheIfNeeded()
    {
        if (s_sharedShapePathCache.Count > SharedShapePathCacheLimit)
        {
            s_sharedShapePathCache.Clear();
        }
    }
}
