using System;
using Avalonia;
using Svg;

namespace AvalonDraw.Services;

public class ToolService
{
    public enum Tool
    {
        Select,
        PathSelect,
        PolygonSelect,
        PolylineSelect,
        Line,
        Rect,
        Circle,
        Ellipse,
        Polygon,
        Polyline
    }

    public Tool CurrentTool { get; private set; } = Tool.Select;

    public void SetTool(Tool tool)
    {
        CurrentTool = tool;
    }

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
            _ => null!
        };
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
            pg.Points.Insert(pg.Points.Count - 2, new SvgUnit(SvgUnitType.User, x));
            pg.Points.Insert(pg.Points.Count - 2 + 1, new SvgUnit(SvgUnitType.User, y));
        }
        else if (element is SvgPolyline pl)
        {
            var x = snapToGrid ? snap(point.X) : point.X;
            var y = snapToGrid ? snap(point.Y) : point.Y;
            pl.Points.Insert(pl.Points.Count - 2, new SvgUnit(SvgUnitType.User, x));
            pl.Points.Insert(pl.Points.Count - 2 + 1, new SvgUnit(SvgUnitType.User, y));
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
