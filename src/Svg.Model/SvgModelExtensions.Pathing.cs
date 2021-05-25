using System;
using System.Collections.Generic;
using System.Diagnostics;
using Svg.Model.Primitives;
using Svg.Model.Primitives.PathCommands;
using Svg.Pathing;

namespace Svg.Model
{
    public static partial class SvgModelExtensions
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

        internal static List<(Point Point, byte Type)> GetPathTypes(this Path path)
        {
            // System.Drawing.Drawing2D.GraphicsPath.PathTypes
            // System.Drawing.Drawing2D.PathPointType
            // byte -> PathPointType
            var pathTypes = new List<(Point Point, byte Type)>();

            if (path.Commands is null)
            {
                return pathTypes;
            }
            (Point Point, byte Type) lastPoint = (default, 0);
            foreach (var pathCommand in path.Commands)
            {
                switch (pathCommand)
                {
                    case MoveToPathCommand moveToPathCommand:
                        {
                            var point0 = new Point(moveToPathCommand.X, moveToPathCommand.Y);
                            pathTypes.Add((point0, (byte)PathPointType.Start));
                            lastPoint = (point0, (byte)PathPointType.Start);
                        }
                        break;

                    case LineToPathCommand lineToPathCommand:
                        {
                            var point1 = new Point(lineToPathCommand.X, lineToPathCommand.Y);
                            pathTypes.Add((point1, (byte)PathPointType.Line));
                            lastPoint = (point1, (byte)PathPointType.Line);
                        }
                        break;

                    case CubicToPathCommand cubicToPathCommand:
                        {
                            var point1 = new Point(cubicToPathCommand.X0, cubicToPathCommand.Y0);
                            var point2 = new Point(cubicToPathCommand.X1, cubicToPathCommand.Y1);
                            var point3 = new Point(cubicToPathCommand.X2, cubicToPathCommand.Y2);
                            pathTypes.Add((point1, (byte)PathPointType.Bezier));
                            pathTypes.Add((point2, (byte)PathPointType.Bezier));
                            pathTypes.Add((point3, (byte)PathPointType.Bezier));
                            lastPoint = (point3, (byte)PathPointType.Bezier);
                        }
                        break;

                    case QuadToPathCommand quadToPathCommand:
                        {
                            var point1 = new Point(quadToPathCommand.X0, quadToPathCommand.Y0);
                            var point2 = new Point(quadToPathCommand.X1, quadToPathCommand.Y1);
                            pathTypes.Add((point1, (byte)PathPointType.Bezier));
                            pathTypes.Add((point2, (byte)PathPointType.Bezier));
                            lastPoint = (point2, (byte)PathPointType.Bezier);
                        }
                        break;

                    case ArcToPathCommand arcToPathCommand:
                        {
                            var point1 = new Point(arcToPathCommand.X, arcToPathCommand.Y);
                            pathTypes.Add((point1, (byte)PathPointType.Bezier));
                            lastPoint = (point1, (byte)PathPointType.Bezier);
                        }
                        break;

                    case ClosePathCommand:
                        {
                            lastPoint = (lastPoint.Point, (byte)(lastPoint.Type | (byte)PathPointType.CloseSubpath));
                            pathTypes[pathTypes.Count - 1] = lastPoint;
                        }
                        break;

                    case AddPolyPathCommand addPolyPathCommand:
                        {
                            if (addPolyPathCommand.Points is { } && addPolyPathCommand.Points.Count > 0)
                            {
                                foreach (var nexPoint in addPolyPathCommand.Points)
                                {
                                    var point1 = new Point(nexPoint.X, nexPoint.Y);
                                    pathTypes.Add((point1, (byte)PathPointType.Start));
                                }

                                var point = addPolyPathCommand.Points[addPolyPathCommand.Points.Count - 1];
                                lastPoint = (point, (byte)PathPointType.Line);
                            }
                        }
                        break;

                    default:
                        Debug.WriteLine($"Not implemented path point for {pathCommand?.GetType()} type.");
                        break;
                }
            }

            return pathTypes;
        }

        internal static Path? ToPath(this SvgPathSegmentList svgPathSegmentList, SvgFillRule svgFillRule)
        {
            if (svgPathSegmentList is null || svgPathSegmentList.Count <= 0)
            {
                return default;
            }

            var fillType = svgFillRule == SvgFillRule.EvenOdd ? PathFillType.EvenOdd : PathFillType.Winding;
            var skPath = new Path
            {
                FillType = fillType
            };

            var isEndFigure = false;
            var haveFigure = false;

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
                            else
                            {
                                if (svgPathSegmentList[i + 1] is SvgMoveToSegment)
                                {
                                    return skPath;
                                }

                                if (svgPathSegmentList[i + 1] is SvgClosePathSegment)
                                {
                                    return skPath;
                                }
                            }
                            isEndFigure = true;
                            haveFigure = false;
                            var x = svgMoveToSegment.Start.X;
                            var y = svgMoveToSegment.Start.Y;
                            skPath.MoveTo(x, y);
                        }
                        break;

                    case SvgLineSegment svgLineSegment:
                        {
                            if (isEndFigure == false)
                            {
                                return default;
                            }
                            haveFigure = true;
                            var x = svgLineSegment.End.X;
                            var y = svgLineSegment.End.Y;
                            skPath.LineTo(x, y);
                        }
                        break;

                    case SvgCubicCurveSegment svgCubicCurveSegment:
                        {
                            if (isEndFigure == false)
                            {
                                return default;
                            }
                            haveFigure = true;
                            var x0 = svgCubicCurveSegment.FirstControlPoint.X;
                            var y0 = svgCubicCurveSegment.FirstControlPoint.Y;
                            var x1 = svgCubicCurveSegment.SecondControlPoint.X;
                            var y1 = svgCubicCurveSegment.SecondControlPoint.Y;
                            var x2 = svgCubicCurveSegment.End.X;
                            var y2 = svgCubicCurveSegment.End.Y;
                            skPath.CubicTo(x0, y0, x1, y1, x2, y2);
                        }
                        break;

                    case SvgQuadraticCurveSegment svgQuadraticCurveSegment:
                        {
                            if (isEndFigure == false)
                            {
                                return default;
                            }
                            haveFigure = true;
                            var x0 = svgQuadraticCurveSegment.ControlPoint.X;
                            var y0 = svgQuadraticCurveSegment.ControlPoint.Y;
                            var x1 = svgQuadraticCurveSegment.End.X;
                            var y1 = svgQuadraticCurveSegment.End.Y;
                            skPath.QuadTo(x0, y0, x1, y1);
                        }
                        break;

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
                            var largeArc = svgArcSegment.Size == SvgArcSize.Small ? PathArcSize.Small : PathArcSize.Large;
                            var sweep = svgArcSegment.Sweep == SvgArcSweep.Negative ? PathDirection.CounterClockwise : PathDirection.Clockwise;
                            var x = svgArcSegment.End.X;
                            var y = svgArcSegment.End.Y;
                            skPath.ArcTo(rx, ry, xAxisRotate, largeArc, sweep, x, y);
                        }
                        break;

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
                        }
                        break;
                }
            }

            if (isEndFigure && haveFigure == false)
            {
                return default;
            }

            return skPath;
        }

        internal static Path? ToPath(this SvgPointCollection svgPointCollection, SvgFillRule svgFillRule, bool isClosed, Rect skOwnerBounds)
        {
            var fillType = svgFillRule == SvgFillRule.EvenOdd ? PathFillType.EvenOdd : PathFillType.Winding;
            var skPath = new Path
            {
                FillType = fillType
            };

            var skPoints = new Point[svgPointCollection.Count / 2];

            for (var i = 0; i + 1 < svgPointCollection.Count; i += 2)
            {
                var x = svgPointCollection[i].ToDeviceValue(UnitRenderingType.Other, null, skOwnerBounds);
                var y = svgPointCollection[i + 1].ToDeviceValue(UnitRenderingType.Other, null, skOwnerBounds);
                skPoints[i / 2] = new Point(x, y);
            }

            skPath.AddPoly(skPoints, isClosed);

            return skPath;
        }

        internal static Path? ToPath(this SvgRectangle svgRectangle, SvgFillRule svgFillRule, Rect skOwnerBounds)
        {
            var fillType = svgFillRule == SvgFillRule.EvenOdd ? PathFillType.EvenOdd : PathFillType.Winding;
            var skPath = new Path
            {
                FillType = fillType
            };

            var x = svgRectangle.X.ToDeviceValue(UnitRenderingType.Horizontal, svgRectangle, skOwnerBounds);
            var y = svgRectangle.Y.ToDeviceValue(UnitRenderingType.Vertical, svgRectangle, skOwnerBounds);
            var width = svgRectangle.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgRectangle, skOwnerBounds);
            var height = svgRectangle.Height.ToDeviceValue(UnitRenderingType.Vertical, svgRectangle, skOwnerBounds);
            var rx = svgRectangle.CornerRadiusX.ToDeviceValue(UnitRenderingType.Horizontal, svgRectangle, skOwnerBounds);
            var ry = svgRectangle.CornerRadiusY.ToDeviceValue(UnitRenderingType.Vertical, svgRectangle, skOwnerBounds);

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
            var skRectBounds = Rect.Create(x, y, width, height);

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

        internal static Path? ToPath(this SvgCircle svgCircle, SvgFillRule svgFillRule, Rect skOwnerBounds)
        {
            var fillType = svgFillRule == SvgFillRule.EvenOdd ? PathFillType.EvenOdd : PathFillType.Winding;
            var skPath = new Path
            {
                FillType = fillType
            };

            var cx = svgCircle.CenterX.ToDeviceValue(UnitRenderingType.Horizontal, svgCircle, skOwnerBounds);
            var cy = svgCircle.CenterY.ToDeviceValue(UnitRenderingType.Vertical, svgCircle, skOwnerBounds);
            var radius = svgCircle.Radius.ToDeviceValue(UnitRenderingType.Other, svgCircle, skOwnerBounds);

            if (radius <= 0f)
            {
                return default;
            }

            skPath.AddCircle(cx, cy, radius);

            return skPath;
        }

        internal static Path? ToPath(this SvgEllipse svgEllipse, SvgFillRule svgFillRule, Rect skOwnerBounds)
        {
            var fillType = svgFillRule == SvgFillRule.EvenOdd ? PathFillType.EvenOdd : PathFillType.Winding;
            var skPath = new Path
            {
                FillType = fillType
            };

            var cx = svgEllipse.CenterX.ToDeviceValue(UnitRenderingType.Horizontal, svgEllipse, skOwnerBounds);
            var cy = svgEllipse.CenterY.ToDeviceValue(UnitRenderingType.Vertical, svgEllipse, skOwnerBounds);
            var rx = svgEllipse.RadiusX.ToDeviceValue(UnitRenderingType.Other, svgEllipse, skOwnerBounds);
            var ry = svgEllipse.RadiusY.ToDeviceValue(UnitRenderingType.Other, svgEllipse, skOwnerBounds);

            if (rx <= 0f || ry <= 0f)
            {
                return default;
            }

            var skRectBounds = Rect.Create(cx - rx, cy - ry, rx + rx, ry + ry);

            skPath.AddOval(skRectBounds);

            return skPath;
        }

        internal static Path? ToPath(this SvgLine svgLine, SvgFillRule svgFillRule, Rect skOwnerBounds)
        {
            var fillType = svgFillRule == SvgFillRule.EvenOdd ? PathFillType.EvenOdd : PathFillType.Winding;
            var skPath = new Path
            {
                FillType = fillType
            };

            var x0 = svgLine.StartX.ToDeviceValue(UnitRenderingType.Horizontal, svgLine, skOwnerBounds);
            var y0 = svgLine.StartY.ToDeviceValue(UnitRenderingType.Vertical, svgLine, skOwnerBounds);
            var x1 = svgLine.EndX.ToDeviceValue(UnitRenderingType.Horizontal, svgLine, skOwnerBounds);
            var y1 = svgLine.EndY.ToDeviceValue(UnitRenderingType.Vertical, svgLine, skOwnerBounds);

            skPath.MoveTo(x0, y0);
            skPath.LineTo(x1, y1);

            return skPath;
        }
    }
}
