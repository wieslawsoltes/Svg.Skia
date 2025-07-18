using System;
using Avalonia;
using Svg;
using Svg.Pathing;

namespace AvalonDraw.Services;

public class ToolService
{
    public enum Tool
    {
        Select,
        MultiSelect,
        PathSelect,
        PolygonSelect,
        PolylineSelect,
        Line,
        Rect,
        Circle,
        Ellipse,
        Polygon,
        Polyline,
        Text,
        PathLine,
        PathCubic,
        PathQuadratic,
        PathArc,
        PathMove,
        Symbol
    }

    public Tool CurrentTool { get; private set; } = Tool.Select;

    public event Action<Tool, Tool>? ToolChanged;

    public void SetTool(Tool tool)
    {
        if (tool == CurrentTool)
            return;
        var old = CurrentTool;
        CurrentTool = tool;
        ToolChanged?.Invoke(old, tool);
    }

    public string? SymbolId { get; set; }

    public SvgVisualElement? CreateElement(Tool tool, SvgElement parent, ShimSkiaSharp.SKPoint start)
    {
        return tool switch
        {
            Tool.Line => new SvgLine
            {
                StartX = new SvgUnit(SvgUnitType.User, start.X),
                StartY = new SvgUnit(SvgUnitType.User, start.Y),
                EndX = new SvgUnit(SvgUnitType.User, start.X),
                EndY = new SvgUnit(SvgUnitType.User, start.Y),
                Stroke = new SvgColourServer(System.Drawing.Color.Black),
                StrokeWidth = new SvgUnit(1f)
            },
            Tool.Rect => new SvgRectangle
            {
                X = new SvgUnit(SvgUnitType.User, start.X),
                Y = new SvgUnit(SvgUnitType.User, start.Y),
                Width = new SvgUnit(SvgUnitType.User, 0),
                Height = new SvgUnit(SvgUnitType.User, 0)
            },
            Tool.Circle => new SvgCircle
            {
                CenterX = new SvgUnit(SvgUnitType.User, start.X),
                CenterY = new SvgUnit(SvgUnitType.User, start.Y),
                Radius = new SvgUnit(SvgUnitType.User, 0)
            },
            Tool.Ellipse => new SvgEllipse
            {
                CenterX = new SvgUnit(SvgUnitType.User, start.X),
                CenterY = new SvgUnit(SvgUnitType.User, start.Y),
                RadiusX = new SvgUnit(SvgUnitType.User, 0),
                RadiusY = new SvgUnit(SvgUnitType.User, 0)
            },
            Tool.Polygon => new SvgPolygon
            {
                Points = new SvgPointCollection
                {
                    new SvgUnit(SvgUnitType.User, start.X), new SvgUnit(SvgUnitType.User, start.Y),
                    new SvgUnit(SvgUnitType.User, start.X), new SvgUnit(SvgUnitType.User, start.Y)
                },
                Stroke = new SvgColourServer(System.Drawing.Color.Black),
                StrokeWidth = new SvgUnit(1f)
            },
            Tool.Polyline => new SvgPolyline
            {
                Points = new SvgPointCollection
                {
                    new SvgUnit(SvgUnitType.User, start.X), new SvgUnit(SvgUnitType.User, start.Y),
                    new SvgUnit(SvgUnitType.User, start.X), new SvgUnit(SvgUnitType.User, start.Y)
                },
                Stroke = new SvgColourServer(System.Drawing.Color.Black),
                StrokeWidth = new SvgUnit(1f)
            },
            Tool.Text => new SvgText
            {
                X = new SvgUnitCollection { new SvgUnit(SvgUnitType.User, start.X) },
                Y = new SvgUnitCollection { new SvgUnit(SvgUnitType.User, start.Y) },
                Text = "Text"
            },
            Tool.PathLine => CreatePath(start, Tool.PathLine),
            Tool.PathCubic => CreatePath(start, Tool.PathCubic),
            Tool.PathQuadratic => CreatePath(start, Tool.PathQuadratic),
            Tool.PathArc => CreatePath(start, Tool.PathArc),
            Tool.PathMove => CreatePath(start, Tool.PathMove),
            Tool.Symbol when !string.IsNullOrEmpty(SymbolId) => new SvgUse
            {
                ReferencedElement = new Uri($"#{SymbolId}", UriKind.Relative),
                X = new SvgUnit(SvgUnitType.User, start.X),
                Y = new SvgUnit(SvgUnitType.User, start.Y)
            },
            _ => null!
        };
    }

    private static SvgPath CreatePath(ShimSkiaSharp.SKPoint start, Tool tool)
    {
        var path = new SvgPath
        {
            Stroke = new SvgColourServer(System.Drawing.Color.Black),
            StrokeWidth = new SvgUnit(1f)
        };
        var list = new SvgPathSegmentList
        {
            new SvgMoveToSegment(false, new System.Drawing.PointF(start.X, start.Y))
        };
        SvgPathSegment seg = tool switch
        {
            Tool.PathCubic => new SvgCubicCurveSegment(false,
                new System.Drawing.PointF(start.X, start.Y),
                new System.Drawing.PointF(start.X, start.Y),
                new System.Drawing.PointF(start.X, start.Y)),
            Tool.PathQuadratic => new SvgQuadraticCurveSegment(false,
                new System.Drawing.PointF(start.X, start.Y),
                new System.Drawing.PointF(start.X, start.Y)),
            Tool.PathArc => new SvgArcSegment(10, 10, 0, SvgArcSize.Small, SvgArcSweep.Positive, false,
                new System.Drawing.PointF(start.X, start.Y)),
            Tool.PathMove => new SvgMoveToSegment(false, new System.Drawing.PointF(start.X, start.Y)),
            _ => new SvgLineSegment(false, new System.Drawing.PointF(start.X, start.Y))
        };
        list.Add(seg);
        path.PathData = list;
        return path;
    }

    public void UpdateElement(SvgVisualElement element, Tool tool, ShimSkiaSharp.SKPoint start, ShimSkiaSharp.SKPoint current, bool snapToGrid, Func<float, float> snap)
    {
        switch (tool)
        {
            case Tool.Line when element is SvgLine ln:
                ln.EndX = new SvgUnit(ln.EndX.Type, snapToGrid ? snap(current.X) : current.X);
                ln.EndY = new SvgUnit(ln.EndY.Type, snapToGrid ? snap(current.Y) : current.Y);
                break;
            case Tool.Rect when element is SvgRectangle r:
                var x = Math.Min(start.X, current.X);
                var y = Math.Min(start.Y, current.Y);
                var w = Math.Abs(current.X - start.X);
                var h = Math.Abs(current.Y - start.Y);
                if (snapToGrid)
                {
                    x = snap(x); y = snap(y); w = snap(w); h = snap(h);
                }
                r.X = new SvgUnit(r.X.Type, x);
                r.Y = new SvgUnit(r.Y.Type, y);
                r.Width = new SvgUnit(r.Width.Type, w);
                r.Height = new SvgUnit(r.Height.Type, h);
                break;
            case Tool.Circle when element is SvgCircle c:
                var cx = (start.X + current.X) / 2f;
                var cy = (start.Y + current.Y) / 2f;
                var rv = Math.Max(Math.Abs(current.X - start.X), Math.Abs(current.Y - start.Y)) / 2f;
                if (snapToGrid)
                {
                    cx = snap(cx); cy = snap(cy); rv = snap(rv);
                }
                c.CenterX = new SvgUnit(c.CenterX.Type, cx);
                c.CenterY = new SvgUnit(c.CenterY.Type, cy);
                c.Radius = new SvgUnit(c.Radius.Type, rv);
                break;
            case Tool.Ellipse when element is SvgEllipse el:
                var ecx = (start.X + current.X) / 2f;
                var ecy = (start.Y + current.Y) / 2f;
                var rx = Math.Abs(current.X - start.X) / 2f;
                var ry = Math.Abs(current.Y - start.Y) / 2f;
                if (snapToGrid)
                {
                    ecx = snap(ecx); ecy = snap(ecy); rx = snap(rx); ry = snap(ry);
                }
                el.CenterX = new SvgUnit(el.CenterX.Type, ecx);
                el.CenterY = new SvgUnit(el.CenterY.Type, ecy);
                el.RadiusX = new SvgUnit(el.RadiusX.Type, rx);
                el.RadiusY = new SvgUnit(el.RadiusY.Type, ry);
                break;
            case Tool.Polygon when element is SvgPolygon pg:
                if (pg.Points.Count >= 2)
                {
                    var px = snapToGrid ? snap(current.X) : current.X;
                    var py = snapToGrid ? snap(current.Y) : current.Y;
                    pg.Points[pg.Points.Count - 2] = new SvgUnit(pg.Points[0].Type, px);
                    pg.Points[pg.Points.Count - 1] = new SvgUnit(pg.Points[1].Type, py);
                }
                break;
            case Tool.Polyline when element is SvgPolyline pl:
                if (pl.Points.Count >= 2)
                {
                    var plx = snapToGrid ? snap(current.X) : current.X;
                    var ply = snapToGrid ? snap(current.Y) : current.Y;
                    pl.Points[pl.Points.Count - 2] = new SvgUnit(pl.Points[0].Type, plx);
                    pl.Points[pl.Points.Count - 1] = new SvgUnit(pl.Points[1].Type, ply);
                }
                break;
            case Tool.Text when element is SvgTextBase txt:
                if (txt.X.Count == 0)
                    txt.X.Add(new SvgUnit(SvgUnitType.User, 0));
                if (txt.Y.Count == 0)
                    txt.Y.Add(new SvgUnit(SvgUnitType.User, 0));
                var tx = snapToGrid ? snap(current.X) : current.X;
                var ty = snapToGrid ? snap(current.Y) : current.Y;
                txt.X[0] = new SvgUnit(txt.X[0].Type, tx);
                txt.Y[0] = new SvgUnit(txt.Y[0].Type, ty);
                break;
            case Tool.PathLine when element is SvgPath p:
                if (p.PathData.Count >= 2 && p.PathData[1] is SvgLineSegment lnSeg)
                {
                    var px = snapToGrid ? snap(current.X) : current.X;
                    var py = snapToGrid ? snap(current.Y) : current.Y;
                    lnSeg.End = new System.Drawing.PointF(px, py);
                }
                break;
            case Tool.PathCubic when element is SvgPath pc && pc.PathData[1] is SvgCubicCurveSegment cc:
                var cx1 = snapToGrid ? snap(current.X) : current.X;
                var cy1 = snapToGrid ? snap(current.Y) : current.Y;
                cc.SecondControlPoint = new System.Drawing.PointF(cx1, cy1);
                cc.End = new System.Drawing.PointF(cx1, cy1);
                break;
            case Tool.PathQuadratic when element is SvgPath pq && pq.PathData[1] is SvgQuadraticCurveSegment qc:
                var qx = snapToGrid ? snap(current.X) : current.X;
                var qy = snapToGrid ? snap(current.Y) : current.Y;
                qc.ControlPoint = new System.Drawing.PointF(qx, qy);
                qc.End = new System.Drawing.PointF(qx, qy);
                break;
            case Tool.PathArc when element is SvgPath pa && pa.PathData[1] is SvgArcSegment ac:
                var ax = snapToGrid ? snap(current.X) : current.X;
                var ay = snapToGrid ? snap(current.Y) : current.Y;
                ac.End = new System.Drawing.PointF(ax, ay);
                break;
            case Tool.PathMove when element is SvgPath pm && pm.PathData[1] is SvgMoveToSegment mv:
                var mx = snapToGrid ? snap(current.X) : current.X;
                var my = snapToGrid ? snap(current.Y) : current.Y;
                mv.End = new System.Drawing.PointF(mx, my);
                break;
            case Tool.Symbol when element is SvgUse use:
                var ux = snapToGrid ? snap(current.X) : current.X;
                var uy = snapToGrid ? snap(current.Y) : current.Y;
                use.X = new SvgUnit(use.X.Type, ux);
                use.Y = new SvgUnit(use.Y.Type, uy);
                break;
        }
    }

    public void AddPolygonPoint(SvgVisualElement element, Tool tool, ShimSkiaSharp.SKPoint point, bool snapToGrid, Func<float, float> snap)
    {
        if (tool != Tool.Polygon && tool != Tool.Polyline)
            return;
        if (element is SvgPolygon pg)
        {
            var x = snapToGrid ? snap(point.X) : point.X;
            var y = snapToGrid ? snap(point.Y) : point.Y;
            var idx = pg.Points.Count - 2;
            pg.Points.Insert(idx, new SvgUnit(SvgUnitType.User, x));
            pg.Points.Insert(idx + 1, new SvgUnit(SvgUnitType.User, y));
        }
        else if (element is SvgPolyline pl)
        {
            var x = snapToGrid ? snap(point.X) : point.X;
            var y = snapToGrid ? snap(point.Y) : point.Y;
            var idx = pl.Points.Count - 2;
            pl.Points.Insert(idx, new SvgUnit(SvgUnitType.User, x));
            pl.Points.Insert(idx + 1, new SvgUnit(SvgUnitType.User, y));
        }
    }

    public void FinalizePolygon(SvgVisualElement element, Tool tool, ShimSkiaSharp.SKPoint point, bool snapToGrid, Func<float, float> snap)
    {
        if (tool != Tool.Polygon && tool != Tool.Polyline)
            return;
        var pts = tool == Tool.Polygon
            ? ((SvgPolygon)element).Points
            : ((SvgPolyline)element).Points;
        var x = snapToGrid ? snap(point.X) : point.X;
        var y = snapToGrid ? snap(point.Y) : point.Y;
        if (pts.Count >= 2)
        {
            pts[pts.Count - 2] = new SvgUnit(pts[0].Type, x);
            pts[pts.Count - 1] = new SvgUnit(pts[1].Type, y);
        }
    }
}
