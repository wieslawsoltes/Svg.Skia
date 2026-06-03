// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.Pathing;

namespace Svg.Model.Services;

internal static class PathingService
{
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
                case AddRectPathCommand addRectPathCommand:
                    {
                        var rect = addRectPathCommand.Rect;
                        pathTypes.Add((rect.TopLeft, (byte)PathPointType.Start));
                        pathTypes.Add((new SKPoint(rect.Right, rect.Top), (byte)PathPointType.Line));
                        pathTypes.Add((rect.BottomRight, (byte)PathPointType.Line));
                        lastPoint = (new SKPoint(rect.Left, rect.Bottom), (byte)((byte)PathPointType.Line | (byte)PathPointType.CloseSubpath));
                        pathTypes.Add(lastPoint);
                        break;
                    }
                case AddRoundRectPathCommand addRoundRectPathCommand:
                    {
                        AppendRoundRectPathTypes(pathTypes, addRoundRectPathCommand.Rect, addRoundRectPathCommand.Rx, addRoundRectPathCommand.Ry);
                        lastPoint = pathTypes[pathTypes.Count - 1];
                        break;
                    }
                case AddOvalPathCommand addOvalPathCommand:
                    {
                        var previousCount = pathTypes.Count;
                        AppendOvalPathTypes(pathTypes, addOvalPathCommand.Rect);
                        if (pathTypes.Count > previousCount)
                        {
                            lastPoint = pathTypes[pathTypes.Count - 1];
                        }
                        break;
                    }
                case AddCirclePathCommand addCirclePathCommand:
                    {
                        var radius = addCirclePathCommand.Radius;
                        var previousCount = pathTypes.Count;
                        AppendOvalPathTypes(
                            pathTypes,
                            SKRect.Create(
                                addCirclePathCommand.X - radius,
                                addCirclePathCommand.Y - radius,
                                radius * 2f,
                                radius * 2f));
                        if (pathTypes.Count > previousCount)
                        {
                            lastPoint = pathTypes[pathTypes.Count - 1];
                        }
                        break;
                    }
                default:
                    break;
            }
        }

        return pathTypes;
    }

    private static void AppendRoundRectPathTypes(List<(SKPoint Point, byte Type)> pathTypes, SKRect rect, float rx, float ry)
    {
        rx = Math.Min(Math.Abs(rx), rect.Width / 2f);
        ry = Math.Min(Math.Abs(ry), rect.Height / 2f);
        if (rx <= 0f || ry <= 0f)
        {
            pathTypes.Add((rect.TopLeft, (byte)PathPointType.Start));
            pathTypes.Add((new SKPoint(rect.Right, rect.Top), (byte)PathPointType.Line));
            pathTypes.Add((rect.BottomRight, (byte)PathPointType.Line));
            pathTypes.Add((new SKPoint(rect.Left, rect.Bottom), (byte)((byte)PathPointType.Line | (byte)PathPointType.CloseSubpath)));
            return;
        }

        var kx = rx * 0.55228475f;
        var ky = ry * 0.55228475f;
        pathTypes.Add((new SKPoint(rect.Left + rx, rect.Top), (byte)PathPointType.Start));
        pathTypes.Add((new SKPoint(rect.Right - rx, rect.Top), (byte)PathPointType.Line));
        AppendCubicPathTypes(pathTypes, new SKPoint(rect.Right - rx + kx, rect.Top), new SKPoint(rect.Right, rect.Top + ry - ky), new SKPoint(rect.Right, rect.Top + ry));
        pathTypes.Add((new SKPoint(rect.Right, rect.Bottom - ry), (byte)PathPointType.Line));
        AppendCubicPathTypes(pathTypes, new SKPoint(rect.Right, rect.Bottom - ry + ky), new SKPoint(rect.Right - rx + kx, rect.Bottom), new SKPoint(rect.Right - rx, rect.Bottom));
        pathTypes.Add((new SKPoint(rect.Left + rx, rect.Bottom), (byte)PathPointType.Line));
        AppendCubicPathTypes(pathTypes, new SKPoint(rect.Left + rx - kx, rect.Bottom), new SKPoint(rect.Left, rect.Bottom - ry + ky), new SKPoint(rect.Left, rect.Bottom - ry));
        pathTypes.Add((new SKPoint(rect.Left, rect.Top + ry), (byte)PathPointType.Line));
        AppendCubicPathTypes(pathTypes, new SKPoint(rect.Left, rect.Top + ry - ky), new SKPoint(rect.Left + rx - kx, rect.Top), new SKPoint(rect.Left + rx, rect.Top));
        pathTypes[pathTypes.Count - 1] = (pathTypes[pathTypes.Count - 1].Point, (byte)(pathTypes[pathTypes.Count - 1].Type | (byte)PathPointType.CloseSubpath));
    }

    private static void AppendOvalPathTypes(List<(SKPoint Point, byte Type)> pathTypes, SKRect rect)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        var cx = (rect.Left + rect.Right) / 2f;
        var cy = (rect.Top + rect.Bottom) / 2f;
        var rx = rect.Width / 2f;
        var ry = rect.Height / 2f;
        var kx = rx * 0.55228475f;
        var ky = ry * 0.55228475f;

        pathTypes.Add((new SKPoint(cx + rx, cy), (byte)PathPointType.Start));
        AppendCubicPathTypes(pathTypes, new SKPoint(cx + rx, cy + ky), new SKPoint(cx + kx, cy + ry), new SKPoint(cx, cy + ry));
        AppendCubicPathTypes(pathTypes, new SKPoint(cx - kx, cy + ry), new SKPoint(cx - rx, cy + ky), new SKPoint(cx - rx, cy));
        AppendCubicPathTypes(pathTypes, new SKPoint(cx - rx, cy - ky), new SKPoint(cx - kx, cy - ry), new SKPoint(cx, cy - ry));
        AppendCubicPathTypes(pathTypes, new SKPoint(cx + kx, cy - ry), new SKPoint(cx + rx, cy - ky), new SKPoint(cx + rx, cy));
        pathTypes[pathTypes.Count - 1] = (pathTypes[pathTypes.Count - 1].Point, (byte)(pathTypes[pathTypes.Count - 1].Type | (byte)PathPointType.CloseSubpath));
    }

    private static void AppendCubicPathTypes(List<(SKPoint Point, byte Type)> pathTypes, SKPoint control1, SKPoint control2, SKPoint end)
    {
        pathTypes.Add((control1, (byte)PathPointType.Bezier));
        pathTypes.Add((control2, (byte)PathPointType.Bezier));
        pathTypes.Add((end, (byte)PathPointType.Bezier));
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
        if (svgPathSegmentList is null || svgPathSegmentList.Count <= 0)
        {
            return default;
        }

        var fillType = svgFillRule == SvgFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        if (TryCreateClosedLinePath(svgPathSegmentList, fillType, out var linePath))
        {
            return linePath;
        }

        var skPath = new SKPath
        {
            FillType = fillType
        };

        var hasCurrentPoint = false;
        var hasRenderableCommand = false;
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
                        if (isLast)
                        {
                            return skPath;
                        }

                        var end = ToAbsolute(svgMoveToSegment.End, svgMoveToSegment.IsRelative, start);
                        skPath.MoveTo(end.X, end.Y);
                        start = end;
                        prevMove = end;
                        hasCurrentPoint = true;
                        hasLastCubicSecondControlPoint = false;
                        hasLastQuadraticControlPoint = false;
                        break;
                    }
                case SvgLineSegment svgLineSegment:
                    {
                        if (hasCurrentPoint == false)
                        {
                            return default;
                        }
                        var end = ToAbsolute(svgLineSegment.End, svgLineSegment.IsRelative, start);
                        skPath.LineTo(end.X, end.Y);
                        start = end;
                        hasRenderableCommand = true;
                        hasLastCubicSecondControlPoint = false;
                        hasLastQuadraticControlPoint = false;
                        break;
                    }
                case SvgCubicCurveSegment svgCubicCurveSegment:
                    {
                        if (hasCurrentPoint == false)
                        {
                            return default;
                        }

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
                        hasRenderableCommand = true;
                        lastCubicSecondControlPoint = second;
                        hasLastCubicSecondControlPoint = true;
                        hasLastQuadraticControlPoint = false;
                        break;
                    }
                case SvgQuadraticCurveSegment svgQuadraticCurveSegment:
                    {
                        if (hasCurrentPoint == false)
                        {
                            return default;
                        }

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
                        hasRenderableCommand = true;
                        lastQuadraticControlPoint = controlPoint;
                        hasLastQuadraticControlPoint = true;
                        hasLastCubicSecondControlPoint = false;
                        break;
                    }
                case SvgArcSegment svgArcSegment:
                    {
                        if (hasCurrentPoint == false)
                        {
                            return default;
                        }
                        var rx = svgArcSegment.RadiusX;
                        var ry = svgArcSegment.RadiusY;
                        var end = ToAbsolute(svgArcSegment.End, svgArcSegment.IsRelative, start);
                        if (rx <= float.Epsilon || ry <= float.Epsilon)
                        {
                            skPath.LineTo(end.X, end.Y);
                        }
                        else if (!NearlyEqual(start, end))
                        {
                            var xAxisRotate = svgArcSegment.Angle;
                            var largeArc = svgArcSegment.Size == SvgArcSize.Small ? SKPathArcSize.Small : SKPathArcSize.Large;
                            var sweep = svgArcSegment.Sweep == SvgArcSweep.Negative ? SKPathDirection.CounterClockwise : SKPathDirection.Clockwise;
                            skPath.ArcTo(rx, ry, xAxisRotate, largeArc, sweep, end.X, end.Y);
                        }
                        start = end;
                        hasRenderableCommand = true;
                        hasLastCubicSecondControlPoint = false;
                        hasLastQuadraticControlPoint = false;
                        break;
                    }
                case SvgClosePathSegment _:
                    {
                        if (hasCurrentPoint == false)
                        {
                            continue;
                        }
                        skPath.Close();
                        start = prevMove;
                        hasRenderableCommand = true;
                        hasLastCubicSecondControlPoint = false;
                        hasLastQuadraticControlPoint = false;
                        break;
                    }
            }
        }

        if (hasCurrentPoint && hasRenderableCommand == false)
        {
            return default;
        }

        return skPath;
    }

    private static bool TryCreateClosedLinePath(SvgPathSegmentList svgPathSegmentList, SKPathFillType fillType, out SKPath? path)
    {
        path = null;

        if (svgPathSegmentList.Count < 4 ||
            svgPathSegmentList[0] is not SvgMoveToSegment moveTo ||
            svgPathSegmentList[svgPathSegmentList.Count - 1] is not SvgClosePathSegment)
        {
            return false;
        }

        for (var i = 1; i < svgPathSegmentList.Count - 1; i++)
        {
            if (svgPathSegmentList[i] is not SvgLineSegment)
            {
                return false;
            }
        }

        var points = new SKPoint[svgPathSegmentList.Count - 1];
        var start = System.Drawing.PointF.Empty;
        var end = ToAbsolute(moveTo.End, moveTo.IsRelative, start);
        points[0] = new SKPoint(end.X, end.Y);
        start = end;

        for (var i = 1; i < svgPathSegmentList.Count - 1; i++)
        {
            var lineTo = (SvgLineSegment)svgPathSegmentList[i];
            end = ToAbsolute(lineTo.End, lineTo.IsRelative, start);
            points[i] = new SKPoint(end.X, end.Y);
            start = end;
        }

        if (points.Length < 3)
        {
            return false;
        }

        path = new SKPath
        {
            FillType = fillType
        };
        path.AddPoly(points, close: true);
        return true;
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
        var fillType = svgFillRule == SvgFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        var skPath = new SKPath
        {
            FillType = fillType
        };

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

        return skPath;
    }

    internal static SKPath? ToPath(this SvgCircle svgCircle, SvgFillRule svgFillRule, SKRect skViewport)
    {
        var fillType = svgFillRule == SvgFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        var skPath = new SKPath
        {
            FillType = fillType
        };

        var cx = svgCircle.CenterX.ToDeviceValue(UnitRenderingType.Horizontal, svgCircle, skViewport);
        var cy = svgCircle.CenterY.ToDeviceValue(UnitRenderingType.Vertical, svgCircle, skViewport);
        var radius = svgCircle.Radius.ToDeviceValue(UnitRenderingType.Other, svgCircle, skViewport);

        if (radius <= 0f)
        {
            return default;
        }

        skPath.AddCircle(cx, cy, radius);

        return skPath;
    }

    internal static SKPath? ToPath(this SvgEllipse svgEllipse, SvgFillRule svgFillRule, SKRect skViewport)
    {
        return SvgGeometryService.TryCreateEquivalentPath(svgEllipse, svgFillRule, skViewport, out var path)
            ? path
            : default;
    }

    internal static SKPath? ToPath(this SvgLine svgLine, SvgFillRule svgFillRule, SKRect skViewport)
    {
        var fillType = svgFillRule == SvgFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        var skPath = new SKPath
        {
            FillType = fillType
        };

        var x0 = svgLine.StartX.ToDeviceValue(UnitRenderingType.Horizontal, svgLine, skViewport);
        var y0 = svgLine.StartY.ToDeviceValue(UnitRenderingType.Vertical, svgLine, skViewport);
        var x1 = svgLine.EndX.ToDeviceValue(UnitRenderingType.Horizontal, svgLine, skViewport);
        var y1 = svgLine.EndY.ToDeviceValue(UnitRenderingType.Vertical, svgLine, skViewport);

        skPath.MoveTo(x0, y0);
        skPath.LineTo(x1, y1);

        return skPath;
    }

    private static bool NearlyEqual(System.Drawing.PointF left, System.Drawing.PointF right)
    {
        return Math.Abs(left.X - right.X) <= float.Epsilon &&
               Math.Abs(left.Y - right.Y) <= float.Epsilon;
    }
}
