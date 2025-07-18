using System;
using System.Collections.Generic;
using System.Linq;
using SK = SkiaSharp;
using Svg.Transforms;
using Svg;
using Svg.Model.Drawables;
using Svg.Pathing;
using Shim = ShimSkiaSharp;

namespace AvalonDraw.Services;

public class PathService
{
    public struct PathPoint
    {
        public SvgPathSegment Segment;
        public int Type; // 0=end,1=ctrl1,2=ctrl2
        public Shim.SKPoint Point;
    }

    public enum SegmentTool
    {
        Line,
        Cubic,
        Quadratic,
        Arc,
        Move
    }

    private bool _editing;
    private SvgPath? _path;
    private DrawableBase? _drawable;
    private readonly List<PathPoint> _points = new();
    private int _activeIndex = -1;
    private Shim.SKMatrix _matrix;
    private Shim.SKMatrix _inverse;
    private SegmentTool _segmentTool = SegmentTool.Line;

    public bool IsEditing => _editing;
    public SvgPath? EditPath => _path;
    public DrawableBase? EditDrawable { get => _drawable; set => _drawable = value; }
    public IReadOnlyList<PathPoint> PathPoints => _points;
    public int ActivePoint { get => _activeIndex; set => _activeIndex = value; }
    public Shim.SKMatrix PathMatrix => _matrix;
    public Shim.SKMatrix PathInverse => _inverse;
    public SegmentTool CurrentSegmentTool { get => _segmentTool; set => _segmentTool = value; }

    public void Start(SvgPath path, DrawableBase drawable)
    {
        _editing = true;
        _path = path;
        _drawable = drawable;
        _points.Clear();
        MakePathAbsolute(path);
        var segs = path.PathData;
        var cur = new Shim.SKPoint(0,0);
        foreach (var seg in segs)
        {
            switch (seg)
            {
                case SvgMoveToSegment mv:
                    cur = new Shim.SKPoint(mv.End.X, mv.End.Y);
                    _points.Add(new PathPoint { Segment = mv, Type = 0, Point = cur });
                    break;
                case SvgLineSegment ln:
                    cur = new Shim.SKPoint(ln.End.X, ln.End.Y);
                    _points.Add(new PathPoint { Segment = ln, Type = 0, Point = cur });
                    break;
                case SvgCubicCurveSegment c:
                    var p1 = new Shim.SKPoint(c.FirstControlPoint.X, c.FirstControlPoint.Y);
                    var p2 = new Shim.SKPoint(c.SecondControlPoint.X, c.SecondControlPoint.Y);
                    var end = new Shim.SKPoint(c.End.X, c.End.Y);
                    _points.Add(new PathPoint { Segment = c, Type = 1, Point = p1 });
                    _points.Add(new PathPoint { Segment = c, Type = 2, Point = p2 });
                    _points.Add(new PathPoint { Segment = c, Type = 0, Point = end });
                    cur = end;
                    break;
                case SvgQuadraticCurveSegment q:
                    var cp = new Shim.SKPoint(q.ControlPoint.X, q.ControlPoint.Y);
                    var qe = new Shim.SKPoint(q.End.X, q.End.Y);
                    _points.Add(new PathPoint { Segment = q, Type = 1, Point = cp });
                    _points.Add(new PathPoint { Segment = q, Type = 0, Point = qe });
                    cur = qe;
                    break;
                case SvgArcSegment a:
                    var ae = new Shim.SKPoint(a.End.X, a.End.Y);
                    _points.Add(new PathPoint { Segment = a, Type = 0, Point = ae });
                    cur = ae;
                    break;
            }
        }
        _matrix = drawable.TotalTransform;
        if (!_matrix.TryInvert(out _inverse))
            _inverse = Shim.SKMatrix.CreateIdentity();
    }

    public void Stop()
    {
        _editing = false;
        _path = null;
        _drawable = null;
        _points.Clear();
        _activeIndex = -1;
    }

    public void AddPoint(Shim.SKPoint point)
    {
        if (_path == null)
            return;
        SvgPathSegment seg = _segmentTool switch
        {
            SegmentTool.Line => new SvgLineSegment(false, new System.Drawing.PointF(point.X, point.Y)),
            SegmentTool.Cubic => new SvgCubicCurveSegment(false,
                new System.Drawing.PointF(float.NaN, float.NaN),
                new System.Drawing.PointF(point.X, point.Y),
                new System.Drawing.PointF(point.X, point.Y)),
            SegmentTool.Quadratic => new SvgQuadraticCurveSegment(false,
                new System.Drawing.PointF(point.X, point.Y),
                new System.Drawing.PointF(point.X, point.Y)),
            SegmentTool.Arc => new SvgArcSegment(10,10,0,SvgArcSize.Small,SvgArcSweep.Positive,false,
                new System.Drawing.PointF(point.X, point.Y)),
            SegmentTool.Move => new SvgMoveToSegment(false, new System.Drawing.PointF(point.X, point.Y)),
            _ => new SvgLineSegment(false, new System.Drawing.PointF(point.X, point.Y))
        };
        _path.PathData.Add(seg);
        if (seg is SvgCubicCurveSegment cc)
        {
            _points.Add(new PathPoint { Segment = cc, Type = 1, Point = new Shim.SKPoint(float.IsNaN(cc.FirstControlPoint.X) ? point.X : cc.FirstControlPoint.X, float.IsNaN(cc.FirstControlPoint.Y) ? point.Y : cc.FirstControlPoint.Y) });
            _points.Add(new PathPoint { Segment = cc, Type = 2, Point = new Shim.SKPoint(cc.SecondControlPoint.X, cc.SecondControlPoint.Y) });
            _points.Add(new PathPoint { Segment = cc, Type = 0, Point = point });
        }
        else if (seg is SvgQuadraticCurveSegment qc)
        {
            _points.Add(new PathPoint { Segment = qc, Type = 1, Point = new Shim.SKPoint(qc.ControlPoint.X, qc.ControlPoint.Y) });
            _points.Add(new PathPoint { Segment = qc, Type = 0, Point = point });
        }
        else
        {
            _points.Add(new PathPoint { Segment = seg, Type = 0, Point = point });
        }
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

    public void MoveActivePoint(Shim.SKPoint local)
    {
        if (_path == null || _activeIndex < 0)
            return;
        var pp = _points[_activeIndex];
        pp.Point = local;
        _points[_activeIndex] = pp;
        UpdatePathPoint(pp);
        _path.OnPathUpdated();
    }

    public void MakeSmooth(int index)
    {
        if (_path == null || index < 0 || index >= _points.Count)
            return;
        if (_points[index].Type != 0)
            return;

        var anchor = _points[index].Point;
        int prev = index - 1;
        int next = index + 1;
        if (prev >= 0 && _points[prev].Type != 0)
        {
            var pPrev = _points[prev];
            if (next < _points.Count && _points[next].Type != 0)
            {
                var reflected = Reflect(pPrev.Point, anchor);
                var pNext = _points[next];
                pNext.Point = reflected;
                _points[next] = pNext;
                UpdatePathPoint(pNext);
            }
        }
        else if (next < _points.Count && _points[next].Type != 0)
        {
            var pNext = _points[next];
            var reflected = Reflect(pNext.Point, anchor);
            if (prev >= 0 && _points[prev].Type != 0)
            {
                var pPrev = _points[prev];
                pPrev.Point = reflected;
                _points[prev] = pPrev;
                UpdatePathPoint(pPrev);
            }
        }
        _path.OnPathUpdated();
    }

    public void MakeCorner(int index)
    {
        if (_path == null || index < 0 || index >= _points.Count)
            return;
        if (_points[index].Type != 0)
            return;

        int prev = index - 1;
        int next = index + 1;
        if (prev >= 0 && _points[prev].Type != 0)
        {
            var pPrev = _points[prev];
            pPrev.Point = _points[index].Point;
            _points[prev] = pPrev;
            UpdatePathPoint(pPrev);
        }
        if (next < _points.Count && _points[next].Type != 0)
        {
            var pNext = _points[next];
            pNext.Point = _points[index].Point;
            _points[next] = pNext;
            UpdatePathPoint(pNext);
        }
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

    private static Shim.SKPoint Reflect(Shim.SKPoint point, Shim.SKPoint mirror)
    {
        var dx = Math.Abs(mirror.X - point.X);
        var dy = Math.Abs(mirror.Y - point.Y);
        var x = mirror.X + (mirror.X >= point.X ? dx : -dx);
        var y = mirror.Y + (mirror.Y >= point.Y ? dy : -dy);
        return new Shim.SKPoint(x, y);
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

    public static SK.SKPoint[] ConvertPoints(SvgPointCollection pc)
    {
        var pts = new SK.SKPoint[pc.Count / 2];
        for (int i = 0; i + 1 < pc.Count; i += 2)
            pts[i / 2] = new SK.SKPoint((float)pc[i].Value, (float)pc[i + 1].Value);
        return pts;
    }

    public static void AddPathSegments(SK.SKPath path, SvgPathSegmentList segments)
    {
        var cur = new SK.SKPoint();
        foreach (var seg in segments)
        {
            switch (seg)
            {
                case SvgMoveToSegment mv:
                    cur = new SK.SKPoint(mv.End.X, mv.End.Y);
                    path.MoveTo(cur);
                    break;
                case SvgLineSegment ln:
                    cur = new SK.SKPoint(ln.End.X, ln.End.Y);
                    path.LineTo(cur);
                    break;
                case SvgCubicCurveSegment c:
                    path.CubicTo(new SK.SKPoint(c.FirstControlPoint.X, c.FirstControlPoint.Y),
                        new SK.SKPoint(c.SecondControlPoint.X, c.SecondControlPoint.Y),
                        new SK.SKPoint(c.End.X, c.End.Y));
                    cur = new SK.SKPoint(c.End.X, c.End.Y);
                    break;
                case SvgQuadraticCurveSegment q:
                    path.QuadTo(new SK.SKPoint(q.ControlPoint.X, q.ControlPoint.Y),
                        new SK.SKPoint(q.End.X, q.End.Y));
                    cur = new SK.SKPoint(q.End.X, q.End.Y);
                    break;
                case SvgArcSegment a:
                    path.LineTo(a.End.X, a.End.Y);
                    cur = new SK.SKPoint(a.End.X, a.End.Y);
                    break;
                case SvgClosePathSegment _:
                    path.Close();
                    break;
            }
        }
    }

    public static SK.SKPath? ElementToPath(SvgVisualElement element)
    {
        var path = new SK.SKPath
        {
            FillType = element.FillRule == SvgFillRule.EvenOdd ? SK.SKPathFillType.EvenOdd : SK.SKPathFillType.Winding
        };
        switch (element)
        {
            case SvgPath sp when sp.PathData is { } d:
                AddPathSegments(path, d);
                return path;
            case SvgRectangle r:
                path.AddRect(SK.SKRect.Create((float)r.X.Value, (float)r.Y.Value, (float)r.Width.Value, (float)r.Height.Value));
                return path;
            case SvgCircle c:
                path.AddCircle((float)c.CenterX.Value, (float)c.CenterY.Value, (float)c.Radius.Value);
                return path;
            case SvgEllipse e:
                path.AddOval(SK.SKRect.Create(
                    (float)(e.CenterX.Value - e.RadiusX.Value),
                    (float)(e.CenterY.Value - e.RadiusY.Value),
                    (float)(e.RadiusX.Value * 2),
                    (float)(e.RadiusY.Value * 2)));
                return path;
            case SvgPolyline pl:
                path.AddPoly(ConvertPoints(pl.Points), false);
                return path;
            case SvgPolygon pg:
                path.AddPoly(ConvertPoints(pg.Points), true);
                return path;
            case SvgLine ln:
                path.MoveTo((float)ln.StartX.Value, (float)ln.StartY.Value);
                path.LineTo((float)ln.EndX.Value, (float)ln.EndY.Value);
                return path;
            default:
                return null;
        }
    }

    public static string ToSvgPathData(SK.SKPath skPath)
    {
        var sb = new System.Text.StringBuilder();
        using var iter = skPath.CreateRawIterator();
        var pts = new SK.SKPoint[4];
        while (true)
        {
            var verb = iter.Next(pts);
            switch (verb)
            {
                case SK.SKPathVerb.Move:
                    sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "M{0},{1} ", pts[0].X, pts[0].Y);
                    break;
                case SK.SKPathVerb.Line:
                    sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "L{0},{1} ", pts[1].X, pts[1].Y);
                    break;
                case SK.SKPathVerb.Quad:
                case SK.SKPathVerb.Conic:
                    sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Q{0},{1} {2},{3} ", pts[1].X, pts[1].Y, pts[2].X, pts[2].Y);
                    break;
                case SK.SKPathVerb.Cubic:
                    sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "C{0},{1} {2},{3} {4},{5} ", pts[1].X, pts[1].Y, pts[2].X, pts[2].Y, pts[3].X, pts[3].Y);
                    break;
                case SK.SKPathVerb.Close:
                    sb.Append("Z ");
                    break;
                case SK.SKPathVerb.Done:
                    return sb.ToString().Trim();
            }
        }
    }

    public SvgPath? ApplyPathOp(SvgVisualElement? element, SvgVisualElement? clip, SK.SKPathOp op)
    {
        if (element is null || clip is null)
            return null;

        var p1 = ElementToPath(element);
        var p2 = ElementToPath(clip);
        if (p1 is null || p2 is null)
            return null;

        var result = p1.Op(p2, op);
        if (result is null)
            return null;

        var data = ToSvgPathData(result);
        var segs = SvgPathBuilder.Parse(data.AsSpan());

        if (element is SvgPath sp)
        {
            sp.PathData = segs;
            return sp;
        }

        var path = new SvgPath { PathData = segs };
        path.Fill = element.Fill;
        path.Stroke = element.Stroke;
        path.StrokeWidth = element.StrokeWidth;
        path.Transforms = new SvgTransformCollection();
        if (element.Transforms is { } tx)
        {
            foreach (var t in tx)
                path.Transforms.Add(t);
        }
        if (!string.IsNullOrEmpty(element.ID))
            path.ID = element.ID;

        if (element.Parent is SvgElement parent)
        {
            var index = parent.Children.IndexOf(element);
            if (index >= 0)
                parent.Children[index] = path;
            else
                parent.Children.Add(path);
        }

        return path;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static System.Drawing.PointF Lerp(System.Drawing.PointF a, System.Drawing.PointF b, float t)
        => new System.Drawing.PointF(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t));

    private static SvgPathSegment? LerpSegment(SvgPathSegment s1, SvgPathSegment s2, float t)
    {
        if (s1.GetType() != s2.GetType())
            return null;
        return s1 switch
        {
            SvgMoveToSegment mv1 when s2 is SvgMoveToSegment mv2 => new SvgMoveToSegment(false, Lerp(mv1.End, mv2.End, t)),
            SvgLineSegment ln1 when s2 is SvgLineSegment ln2 => new SvgLineSegment(false, Lerp(ln1.End, ln2.End, t)),
            SvgCubicCurveSegment c1 when s2 is SvgCubicCurveSegment c2 => new SvgCubicCurveSegment(false,
                Lerp(c1.FirstControlPoint, c2.FirstControlPoint, t),
                Lerp(c1.SecondControlPoint, c2.SecondControlPoint, t),
                Lerp(c1.End, c2.End, t)),
            SvgQuadraticCurveSegment q1 when s2 is SvgQuadraticCurveSegment q2 => new SvgQuadraticCurveSegment(false,
                Lerp(q1.ControlPoint, q2.ControlPoint, t),
                Lerp(q1.End, q2.End, t)),
            SvgArcSegment a1 when s2 is SvgArcSegment a2 => new SvgArcSegment(
                Lerp(a1.RadiusX, a2.RadiusX, t),
                Lerp(a1.RadiusY, a2.RadiusY, t),
                Lerp(a1.Angle, a2.Angle, t),
                a1.Size,
                a1.Sweep,
                false,
                Lerp(a1.End, a2.End, t)),
            SvgClosePathSegment _ when s2 is SvgClosePathSegment => new SvgClosePathSegment(false),
            _ => null,
        };
    }

    private static SvgPathSegmentList? LerpPath(SvgPathSegmentList from, SvgPathSegmentList to, float t)
    {
        if (from.Count != to.Count)
            return null;
        var list = new SvgPathSegmentList();
        for (int i = 0; i < from.Count; i++)
        {
            var seg = LerpSegment(from[i], to[i], t);
            if (seg is null)
                return null;
            list.Add(seg);
        }
        return list;
    }

    public IList<SvgPath> Blend(SvgPath? from, SvgPath? to, int steps)
    {
        var list = new List<SvgPath>();
        if (from is null || to is null || steps <= 0)
            return list;

        var d1 = from.PathData;
        var d2 = to.PathData;
        if (d1 is null || d2 is null)
            return list;

        for (int i = 1; i <= steps; i++)
        {
            var t = i / (float)(steps + 1);
            var segs = LerpPath(d1, d2, t);
            if (segs is null)
                continue;
            list.Add(new SvgPath { PathData = segs });
        }

        return list;
    }
}
