using System;
using System.Collections.Generic;

namespace Svg.Model
{
    public class Path : IDisposable
    {
        public PathFillType FillType;
        public IList<PathCommand>? Commands;

        public bool IsEmpty => Commands?.Count == 0;

        public Rect Bounds => Rect.Empty; // TODO:

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
