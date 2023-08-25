using System;
using System.Collections.Generic;
using System.Diagnostics;
#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp;
#endif
using Svg.Pathing;

namespace Svg.Model;

public static partial class SvgExtensions
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
#if USE_SKIASHARP
        internal static List<(SKPoint Point, byte Type)> GetPathTypes(this SKPath path)
        {
            // System.Drawing.Drawing2D.GraphicsPath.PathTypes
            // System.Drawing.Drawing2D.PathPointType
            // byte -> PathPointType
            var pathTypes = new List<(SKPoint Point, byte Type)>();

            using var iterator = path.CreateRawIterator();
            var points = new SKPoint[4];
            var pathVerb = SKPathVerb.Move;

            (SKPoint Point, byte Type) lastPoint = (default, 0);
            while ((pathVerb = iterator.Next(points)) != SKPathVerb.Done)
            {
                switch (pathVerb)
                {
                    case SKPathVerb.Move:
                    {
                        var point0 = new SKPoint(points[0].X, points[0].Y);
                        pathTypes.Add((point0, (byte)PathPointType.Start));
                        lastPoint = (point0, (byte)PathPointType.Start);
                        break;
                    }
                    case SKPathVerb.Line:
                    {
                        var point1 = new SKPoint(points[1].X, points[1].Y);
                        pathTypes.Add((point1, (byte)PathPointType.Line));
                        lastPoint = (point1, (byte)PathPointType.Line);
                        break;
                    }
                    case SKPathVerb.Cubic:
                    {
                        
                        var point1 = new SKPoint(points[1].X, points[1].Y);
                        var point2 = new SKPoint(points[2].X, points[2].Y);
                        var point3 = new SKPoint(points[3].X, points[3].Y);
                        pathTypes.Add((point1, (byte)PathPointType.Bezier));
                        pathTypes.Add((point2, (byte)PathPointType.Bezier));
                        pathTypes.Add((point3, (byte)PathPointType.Bezier));
                        lastPoint = (point3, (byte)PathPointType.Bezier);
                        break;
                    }
                    case SKPathVerb.Quad:
                    {
                        var point1 = new SKPoint(points[1].X, points[1].Y);
                        var point2 = new SKPoint(points[2].X, points[2].Y);
                        pathTypes.Add((point1, (byte)PathPointType.Bezier));
                        pathTypes.Add((point2, (byte)PathPointType.Bezier));
                        lastPoint = (point2, (byte)PathPointType.Bezier);
                        break;
                    }
                    case SKPathVerb.Conic:
                    {
                        var quads = SKPath.ConvertConicToQuads(points[0], points[1], points[2], iterator.ConicWeight(), 1);

                        var point1 = new SKPoint(quads[1].X, quads[1].Y);
                        var point2 = new SKPoint(quads[2].X, quads[2].Y);
                        pathTypes.Add((point1, (byte)PathPointType.Bezier));
                        pathTypes.Add((point2, (byte)PathPointType.Bezier));
                        
                        var point3 = new SKPoint(quads[3].X, quads[3].Y);
                        var point4 = new SKPoint(quads[4].X, quads[4].Y);
                        pathTypes.Add((point3, (byte)PathPointType.Bezier));
                        pathTypes.Add((point4, (byte)PathPointType.Bezier));
                        
                        lastPoint = (point4, (byte)PathPointType.Bezier);
                        
                        break;
                    }
                    case SKPathVerb.Close:
                    {
                        lastPoint = (lastPoint.Point, (byte)(lastPoint.Type | (byte)PathPointType.CloseSubpath));
                        pathTypes[pathTypes.Count - 1] = lastPoint;
                        break;
                    }
                }
            }

            return pathTypes;
        }
#else
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
                        foreach (var nexPoint in addPolyPathCommand.Points)
                        {
                            var point1 = new SKPoint(nexPoint.X, nexPoint.Y);
                            pathTypes.Add((point1, (byte)PathPointType.Start));
                        }

                        var point = addPolyPathCommand.Points[addPolyPathCommand.Points.Count - 1];
                        lastPoint = (point, (byte)PathPointType.Line);
                    }
                    break;
                }
                default:
                    break;
            }
        }

        return pathTypes;
    }
#endif
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
        var skPath = new SKPath
        {
            FillType = fillType
        };

        var isEndFigure = false;
        var haveFigure = false;
        var start = System.Drawing.PointF.Empty;
        var prevMove = start;
        var points = new List<System.Drawing.PointF>();

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
                    points.Add(start);
                    points.Add(end);
                    start = end;
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
                        var prev = svgPathSegmentList.IndexOf(svgCubicCurveSegment) - 1;
                        if (prev >= 0 && svgPathSegmentList[prev] is SvgCubicCurveSegment)
                        {
                            var prevSecondControlPoint = points[points.Count - 2];
                            firstControlPoint = Reflect(new System.Drawing.PointF(prevSecondControlPoint.X, prevSecondControlPoint.Y), start);
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
                    points.Add(start);
                    points.Add(first);
                    points.Add(second);
                    points.Add(end);
                    start = end;
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
                        var prev = svgPathSegmentList.IndexOf(svgQuadraticCurveSegment) - 1;
                        if (prev >= 0 && svgPathSegmentList[prev] is SvgQuadraticCurveSegment)
                        {
                            var prevControlPoint = points[points.Count - 2];
                            controlPoint = Reflect(new System.Drawing.PointF(prevControlPoint.X, prevControlPoint.Y), start);
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
                    points.Add(start);
                    points.Add(controlPoint);
                    points.Add(end);
                    start = end;
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
                    points.Add(start);
                    points.Add(end);
                    start = end;
                    break;
                }
                case SvgClosePathSegment _:
                {
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
                    break;
                } 
            }
        }

        if (isEndFigure && haveFigure == false)
        {
            return default;
        }

        return skPath;
    }

    internal static SKPath? ToPath(this SvgPointCollection svgPointCollection, SvgFillRule svgFillRule, bool isClosed, SKRect skViewport)
    {
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
        var fillType = svgFillRule == SvgFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        var skPath = new SKPath
        {
            FillType = fillType
        };

        var cx = svgEllipse.CenterX.ToDeviceValue(UnitRenderingType.Horizontal, svgEllipse, skViewport);
        var cy = svgEllipse.CenterY.ToDeviceValue(UnitRenderingType.Vertical, svgEllipse, skViewport);
        var rx = svgEllipse.RadiusX.ToDeviceValue(UnitRenderingType.Other, svgEllipse, skViewport);
        var ry = svgEllipse.RadiusY.ToDeviceValue(UnitRenderingType.Other, svgEllipse, skViewport);

        if (rx <= 0f || ry <= 0f)
        {
            return default;
        }

        var skRectBounds = SKRect.Create(cx - rx, cy - ry, rx + rx, ry + ry);

        skPath.AddOval(skRectBounds);

        return skPath;
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

#if USE_SKIASHARP
    internal static SkiaSharp.SKPath? ToSKPath(ClipPath? clipPath)
    {
        if (clipPath?.Clips is null)
        {
            return null;
        }

        var skPathResult = default(SkiaSharp.SKPath);

        foreach (var clip in clipPath.Clips)
        {
            if (clip.Path is null)
            {
                return null;
            }

            var skPath = clip.Path;
            var skPathClip = ToSKPath(clip.Clip);
            if (skPathClip is { }) skPath = clip.Path.Op(skPathClip, SkiaSharp.SKPathOp.Intersect);

            if (clip.Transform is { })
            {
                var skMatrix = clip.Transform.Value;
                skPath.Transform(skMatrix);
            }

            if (skPathResult is null)
            {
                skPathResult = skPath;
            }
            else
            {
                var result = skPathResult.Op(skPath, SkiaSharp.SKPathOp.Union);
                skPathResult = result;
            }
        }

        if (skPathResult is { })
        {
            if (clipPath.Clip?.Clips is { })
            {
                var skPathClip = ToSKPath(clipPath.Clip);
                if (skPathClip is { }) skPathResult = skPathResult.Op(skPathClip, SkiaSharp.SKPathOp.Intersect);
            }

            if (clipPath.Transform is { })
            {
                var skMatrix = clipPath.Transform.Value;
                skPathResult.Transform(skMatrix);
            }
        }

        return skPathResult;
    }
#endif
}
