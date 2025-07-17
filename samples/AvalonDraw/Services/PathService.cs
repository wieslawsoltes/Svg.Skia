using System.Collections.Generic;
using Svg;
using Svg.Model.Drawables;
using Svg.Pathing;
using ShimSkiaSharp;

namespace AvalonDraw.Services;

public class PathService
{
    public struct PathPoint
    {
        public SvgPathSegment Segment;
        public int Type; // 0=end,1=ctrl1,2=ctrl2
        public SKPoint Point;
    }

    private bool _editing;
    private SvgPath? _path;
    private DrawableBase? _drawable;
    private readonly List<PathPoint> _points = new();
    private int _activeIndex = -1;
    private SKMatrix _matrix;
    private SKMatrix _inverse;

    public bool IsEditing => _editing;
    public SvgPath? EditPath => _path;
    public DrawableBase? EditDrawable { get => _drawable; set => _drawable = value; }
    public IReadOnlyList<PathPoint> PathPoints => _points;
    public int ActivePoint { get => _activeIndex; set => _activeIndex = value; }
    public SKMatrix PathMatrix => _matrix;
    public SKMatrix PathInverse => _inverse;

    public void Start(SvgPath path, DrawableBase drawable)
    {
        _editing = true;
        _path = path;
        _drawable = drawable;
        _points.Clear();
        MakePathAbsolute(path);
        var segs = path.PathData;
        var cur = new SKPoint(0,0);
        foreach (var seg in segs)
        {
            switch (seg)
            {
                case SvgMoveToSegment mv:
                    cur = new SKPoint(mv.End.X, mv.End.Y);
                    _points.Add(new PathPoint { Segment = mv, Type = 0, Point = cur });
                    break;
                case SvgLineSegment ln:
                    cur = new SKPoint(ln.End.X, ln.End.Y);
                    _points.Add(new PathPoint { Segment = ln, Type = 0, Point = cur });
                    break;
                case SvgCubicCurveSegment c:
                    var p1 = new SKPoint(c.FirstControlPoint.X, c.FirstControlPoint.Y);
                    var p2 = new SKPoint(c.SecondControlPoint.X, c.SecondControlPoint.Y);
                    var end = new SKPoint(c.End.X, c.End.Y);
                    _points.Add(new PathPoint { Segment = c, Type = 1, Point = p1 });
                    _points.Add(new PathPoint { Segment = c, Type = 2, Point = p2 });
                    _points.Add(new PathPoint { Segment = c, Type = 0, Point = end });
                    cur = end;
                    break;
                case SvgQuadraticCurveSegment q:
                    var cp = new SKPoint(q.ControlPoint.X, q.ControlPoint.Y);
                    var qe = new SKPoint(q.End.X, q.End.Y);
                    _points.Add(new PathPoint { Segment = q, Type = 1, Point = cp });
                    _points.Add(new PathPoint { Segment = q, Type = 0, Point = qe });
                    cur = qe;
                    break;
                case SvgArcSegment a:
                    var ae = new SKPoint(a.End.X, a.End.Y);
                    _points.Add(new PathPoint { Segment = a, Type = 0, Point = ae });
                    cur = ae;
                    break;
            }
        }
        _matrix = drawable.TotalTransform;
        if (!_matrix.TryInvert(out _inverse))
            _inverse = SKMatrix.CreateIdentity();
    }

    public void Stop()
    {
        _editing = false;
        _path = null;
        _drawable = null;
        _points.Clear();
        _activeIndex = -1;
    }

    public void AddPoint(SKPoint point)
    {
        if (_path == null)
            return;
        var seg = new SvgLineSegment(false, new System.Drawing.PointF(point.X, point.Y));
        _path.PathData.Add(seg);
        _points.Add(new PathPoint { Segment = seg, Type = 0, Point = point });
        _path.OnPathUpdated();
    }

    public void RemoveActivePoint()
    {
        if (_path == null || _activeIndex < 0 || _activeIndex >= _points.Count)
            return;
        var seg = _points[_activeIndex].Segment;
        _path.PathData.Remove(seg);
        _points.RemoveAt(_activeIndex);
        _activeIndex = -1;
        _path.OnPathUpdated();
    }

    public void MoveActivePoint(SKPoint local)
    {
        if (_path == null || _activeIndex < 0)
            return;
        var pp = _points[_activeIndex];
        pp.Point = local;
        _points[_activeIndex] = pp;
        UpdatePathPoint(pp);
        _path.OnPathUpdated();
    }

    public int HitPoint(SkiaSharp.SKPoint pt, float handleSize, float scale)
    {
        var hs = handleSize / 2f / scale;
        for (int i = 0; i < _points.Count; i++)
        {
            var p = _matrix.MapPoint(_points[i].Point);
            var r = new SkiaSharp.SKRect(p.X - hs, p.Y - hs, p.X + hs, p.Y + hs);
            if (r.Contains(new SkiaSharp.SKPoint(pt.X, pt.Y)))
                return i;
        }
        return -1;
    }

    private static void UpdatePathPoint(PathPoint pp)
    {
        switch (pp.Segment)
        {
            case SvgMoveToSegment mv:
                mv.End = new System.Drawing.PointF(pp.Point.X, pp.Point.Y);
                break;
            case SvgLineSegment ln:
                ln.End = new System.Drawing.PointF(pp.Point.X, pp.Point.Y);
                break;
            case SvgCubicCurveSegment c:
                if (pp.Type == 1)
                    c.FirstControlPoint = new System.Drawing.PointF(pp.Point.X, pp.Point.Y);
                else if (pp.Type == 2)
                    c.SecondControlPoint = new System.Drawing.PointF(pp.Point.X, pp.Point.Y);
                else
                    c.End = new System.Drawing.PointF(pp.Point.X, pp.Point.Y);
                break;
            case SvgQuadraticCurveSegment q:
                if (pp.Type == 1)
                    q.ControlPoint = new System.Drawing.PointF(pp.Point.X, pp.Point.Y);
                else
                    q.End = new System.Drawing.PointF(pp.Point.X, pp.Point.Y);
                break;
            case SvgArcSegment a:
                a.End = new System.Drawing.PointF(pp.Point.X, pp.Point.Y);
                break;
        }
    }

    private static void MakePathAbsolute(SvgPath path)
    {
        var segs = path.PathData;
        var cur = System.Drawing.PointF.Empty;
        for (int i = 0; i < segs.Count; i++)
        {
            switch (segs[i])
            {
                case SvgMoveToSegment mv:
                    var endM = ToAbs(mv.End, mv.IsRelative, cur);
                    mv.End = endM;
                    mv.IsRelative = false;
                    cur = endM;
                    break;
                case SvgLineSegment ln:
                    var endL = ToAbs(ln.End, ln.IsRelative, cur);
                    ln.End = endL;
                    ln.IsRelative = false;
                    cur = endL;
                    break;
                case SvgCubicCurveSegment c:
                    var p1 = c.FirstControlPoint;
                    if (!float.IsNaN(p1.X) && !float.IsNaN(p1.Y))
                        p1 = ToAbs(p1, c.IsRelative, cur);
                    var p2 = ToAbs(c.SecondControlPoint, c.IsRelative, cur);
                    var e = ToAbs(c.End, c.IsRelative, cur);
                    c.FirstControlPoint = p1;
                    c.SecondControlPoint = p2;
                    c.End = e;
                    c.IsRelative = false;
                    cur = e;
                    break;
                case SvgQuadraticCurveSegment q:
                    var cp = q.ControlPoint;
                    if (!float.IsNaN(cp.X) && !float.IsNaN(cp.Y))
                        cp = ToAbs(cp, q.IsRelative, cur);
                    var qe = ToAbs(q.End, q.IsRelative, cur);
                    q.ControlPoint = cp;
                    q.End = qe;
                    q.IsRelative = false;
                    cur = qe;
                    break;
                case SvgArcSegment a:
                    var ae = ToAbs(a.End, a.IsRelative, cur);
                    a.End = ae;
                    a.IsRelative = false;
                    cur = ae;
                    break;
                case SvgClosePathSegment _:
                    break;
            }
        }
        path.PathData.Owner = path;
        path.OnPathUpdated();
    }

    private static System.Drawing.PointF ToAbs(System.Drawing.PointF point, bool isRelative, System.Drawing.PointF start)
    {
        if (float.IsNaN(point.X))
            point.X = start.X;
        else if (isRelative)
            point.X += start.X;

        if (float.IsNaN(point.Y))
            point.Y = start.Y;
        else if (isRelative)
            point.Y += start.Y;

        return point;
    }
}
