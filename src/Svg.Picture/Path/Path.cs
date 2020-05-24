using System;
using System.Collections.Generic;

namespace Svg.Picture
{
    public class Path : IDisposable
    {
        public PathFillType FillType { get; set; }
        public IList<PathCommand>? Commands { get; set; }

        public bool IsEmpty => Commands == null || Commands.Count == 0;

        public Rect Bounds => GetBounds();

        public Path()
        {
            Commands = new List<PathCommand>();
        }

        private void ComputePointBounds(float x, float y, ref Rect bounds)
        {
            bounds.Left = Math.Min(x, bounds.Left);
            bounds.Right = Math.Max(x, bounds.Right);
            bounds.Top = Math.Min(y, bounds.Top);
            bounds.Bottom = Math.Max(y, bounds.Bottom);
        }

        private Rect GetBounds()
        {
            if (Commands == null || Commands.Count == 0)
            {
                return Rect.Empty;
            }

            var bounds = new Rect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

            foreach (var pathCommand in Commands)
            {
                switch (pathCommand)
                {
                    case MoveToPathCommand moveToPathCommand:
                        {
                            var x = moveToPathCommand.X;
                            var y = moveToPathCommand.Y;
                            ComputePointBounds(x, y, ref bounds);
                        }
                        break;
                    case LineToPathCommand lineToPathCommand:
                        {
                            var x = lineToPathCommand.X;
                            var y = lineToPathCommand.Y;
                            ComputePointBounds(x, y, ref bounds);
                        }
                        break;
                    case ArcToPathCommand arcToPathCommand:
                        {
                            var x = arcToPathCommand.X;
                            var y = arcToPathCommand.Y;
                            ComputePointBounds(x, y, ref bounds);
                        }
                        break;
                    case QuadToPathCommand quadToPathCommand:
                        {
                            var x0 = quadToPathCommand.X0;
                            var y0 = quadToPathCommand.Y0;
                            var x1 = quadToPathCommand.X1;
                            var y1 = quadToPathCommand.Y1;
                            ComputePointBounds(x0, y0, ref bounds);
                            ComputePointBounds(x1, y1, ref bounds);
                        }
                        break;
                    case CubicToPathCommand cubicToPathCommand:
                        {
                            var x0 = cubicToPathCommand.X0;
                            var y0 = cubicToPathCommand.Y0;
                            var x1 = cubicToPathCommand.X1;
                            var y1 = cubicToPathCommand.Y1;
                            var x2 = cubicToPathCommand.X2;
                            var y2 = cubicToPathCommand.Y2;
                            ComputePointBounds(x0, y0, ref bounds);
                            ComputePointBounds(x1, y1, ref bounds);
                            ComputePointBounds(x2, y2, ref bounds);
                        }
                        break;
                    case ClosePathCommand _:
                        break;
                    case AddRectPathCommand addRectPathCommand:
                        {
                            var rect = addRectPathCommand.Rect;
                            ComputePointBounds(rect.Left, rect.Top, ref bounds);
                            ComputePointBounds(rect.Right, rect.Bottom, ref bounds);
                        }
                        break;
                    case AddRoundRectPathCommand addRoundRectPathCommand:
                        {
                            var rect = addRoundRectPathCommand.Rect;
                            ComputePointBounds(rect.Left, rect.Top, ref bounds);
                            ComputePointBounds(rect.Right, rect.Bottom, ref bounds);
                        }
                        break;
                    case AddOvalPathCommand addOvalPathCommand:
                        {
                            var rect = addOvalPathCommand.Rect;
                            ComputePointBounds(rect.Left, rect.Top, ref bounds);
                            ComputePointBounds(rect.Right, rect.Bottom, ref bounds);
                        }
                        break;
                    case AddCirclePathCommand addCirclePathCommand:
                        {
                            var x = addCirclePathCommand.X;
                            var y = addCirclePathCommand.Y;
                            var radius = addCirclePathCommand.Radius;
                            ComputePointBounds(x - radius, y - radius, ref bounds);
                            ComputePointBounds(x + radius, y + radius, ref bounds);
                        }
                        break;
                    case AddPolyPathCommand addPolyPathCommand:
                        {
                            if (addPolyPathCommand.Points != null)
                            {
                                var points = addPolyPathCommand.Points;
                                foreach (var point in points)
                                {
                                    ComputePointBounds(point.X, point.Y, ref bounds);
                                }
                            }
                        }
                        break;
                    default:
                        break;
                }
            }

            return bounds;
        }

        public void MoveTo(float x, float y)
        {
            Commands?.Add(new MoveToPathCommand(x, y));
        }

        public void LineTo(float x, float y)
        {
            Commands?.Add(new LineToPathCommand(x, y));
        }

        public void ArcTo(float rx, float ry, float xAxisRotate, PathArcSize largeArc, PathDirection sweep, float x, float y)
        {
            Commands?.Add(new ArcToPathCommand(rx, ry, xAxisRotate, largeArc, sweep, x, y));
        }

        public void QuadTo(float x0, float y0, float x1, float y1)
        {
            Commands?.Add(new QuadToPathCommand(x0, y0, x1, y1));
        }

        public void CubicTo(float x0, float y0, float x1, float y1, float x2, float y2)
        {
            Commands?.Add(new CubicToPathCommand(x0, y0, x1, y1, x2, y2));
        }

        public void Close()
        {
            Commands?.Add(new ClosePathCommand());
        }

        public void AddRect(Rect rect)
        {
            Commands?.Add(new AddRectPathCommand(rect));
        }

        public void AddRoundRect(Rect rect, float rx, float ry)
        {
            Commands?.Add(new AddRoundRectPathCommand(rect, rx, ry));
        }

        public void AddOval(Rect rect)
        {
            Commands?.Add(new AddOvalPathCommand(rect));
        }

        public void AddCircle(float x, float y, float radius)
        {
            Commands?.Add(new AddCirclePathCommand(x, y, radius));
        }

        public void AddPoly(Point[] points, bool close = true)
        {
            Commands?.Add(new AddPolyPathCommand(points, close));
        }

        public void Dispose()
        {
        }
    }
}
