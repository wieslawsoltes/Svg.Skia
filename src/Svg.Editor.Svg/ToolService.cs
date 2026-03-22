using System;
using System.Linq;
using Svg;
using Svg.DataTypes;
using Svg.Editor.Core;
using Svg.Pathing;

namespace Svg.Editor.Svg;

public class ToolService
{
    public enum Tool
    {
        Select,
        Hand,
        Scale,
        MultiSelect,
        PathSelect,
        PolygonSelect,
        PolylineSelect,
        Line,
        Frame,
        Section,
        Slice,
        Rect,
        Circle,
        Ellipse,
        Arrow,
        Star,
        Polygon,
        Polyline,
        Text,
        TextPath,
        TextArea,
        PathLine,
        PathCubic,
        PathQuadratic,
        PathArc,
        PathMove,
        Symbol,
        Image,
        Freehand,
        Brush,
        Pencil,
        Comment
    }

    public const string SemanticToolAttribute = "data-svgskia-tool";
    public const string SliceFlagAttribute = "data-svgskia-slice";
    public const string ArrowMarkerId = "svg-editor-arrow-marker";
    public const float DefaultTextFontSize = 16f;

    public Tool CurrentTool { get; private set; } = Tool.Select;

    public float CurrentStrokeWidth { get; set; } = 1f;

    public string CurrentFontFamily { get; set; } = "Arial";
    public SvgFontWeight CurrentFontWeight { get; set; } = SvgFontWeight.Normal;
    public float CurrentLetterSpacing { get; set; } = 0f;
    public float CurrentWordSpacing { get; set; } = 0f;

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
    public float SymbolWidth { get; set; } = 160f;
    public float SymbolHeight { get; set; } = 160f;
    public string? ReferenceId { get; set; }
    public string? ImageHref { get; set; }

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
                StrokeWidth = new SvgUnit(CurrentStrokeWidth)
            },
            Tool.Arrow => CreateArrow(parent, start),
            Tool.Frame => CreateFrame(start, FrameContainerKind.Frame),
            Tool.Section => CreateFrame(start, FrameContainerKind.Section),
            Tool.Slice => CreateSlice(start),
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
            Tool.Star => CreateStar(start),
            Tool.Polygon => new SvgPolygon
            {
                Points = new SvgPointCollection
                {
                    new SvgUnit(SvgUnitType.User, start.X), new SvgUnit(SvgUnitType.User, start.Y),
                    new SvgUnit(SvgUnitType.User, start.X), new SvgUnit(SvgUnitType.User, start.Y)
                },
                Stroke = new SvgColourServer(System.Drawing.Color.Black),
                StrokeWidth = new SvgUnit(CurrentStrokeWidth)
            },
            Tool.Polyline => new SvgPolyline
            {
                Points = new SvgPointCollection
                {
                    new SvgUnit(SvgUnitType.User, start.X), new SvgUnit(SvgUnitType.User, start.Y),
                    new SvgUnit(SvgUnitType.User, start.X), new SvgUnit(SvgUnitType.User, start.Y)
                },
                Stroke = new SvgColourServer(System.Drawing.Color.Black),
                StrokeWidth = new SvgUnit(CurrentStrokeWidth)
            },
            Tool.Text => new SvgText
            {
                X = new SvgUnitCollection { new SvgUnit(SvgUnitType.User, start.X) },
                Y = new SvgUnitCollection { new SvgUnit(SvgUnitType.User, start.Y) },
                Text = string.Empty,
                FontSize = new SvgUnit(DefaultTextFontSize),
                FontFamily = CurrentFontFamily,
                FontWeight = CurrentFontWeight,
                LetterSpacing = new SvgUnit(SvgUnitType.User, CurrentLetterSpacing),
                WordSpacing = new SvgUnit(SvgUnitType.User, CurrentWordSpacing)
            },
            Tool.TextPath when !string.IsNullOrEmpty(ReferenceId) => new SvgTextPath
            {
                ReferencedPath = new Uri($"#{ReferenceId}", UriKind.Relative),
                StartOffset = new SvgUnit(SvgUnitType.User, 0),
                Text = string.Empty,
                FontSize = new SvgUnit(DefaultTextFontSize),
                FontFamily = CurrentFontFamily,
                FontWeight = CurrentFontWeight,
                LetterSpacing = new SvgUnit(SvgUnitType.User, CurrentLetterSpacing),
                WordSpacing = new SvgUnit(SvgUnitType.User, CurrentWordSpacing)
            },
            Tool.TextArea when !string.IsNullOrEmpty(ReferenceId) => new SvgText
            {
                X = new SvgUnitCollection { new SvgUnit(SvgUnitType.User, start.X) },
                Y = new SvgUnitCollection { new SvgUnit(SvgUnitType.User, start.Y) },
                ClipPath = new Uri($"#{ReferenceId}", UriKind.Relative),
                Text = string.Empty,
                FontSize = new SvgUnit(DefaultTextFontSize),
                FontFamily = CurrentFontFamily,
                FontWeight = CurrentFontWeight,
                LetterSpacing = new SvgUnit(SvgUnitType.User, CurrentLetterSpacing),
                WordSpacing = new SvgUnit(SvgUnitType.User, CurrentWordSpacing)
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
                Y = new SvgUnit(SvgUnitType.User, start.Y),
                Width = new SvgUnit(SvgUnitType.User, Math.Max(SymbolWidth, 1f)),
                Height = new SvgUnit(SvgUnitType.User, Math.Max(SymbolHeight, 1f))
            },
            Tool.Image when !string.IsNullOrEmpty(ImageHref) => new SvgImage
            {
                X = new SvgUnit(SvgUnitType.User, start.X),
                Y = new SvgUnit(SvgUnitType.User, start.Y),
                Width = new SvgUnit(SvgUnitType.User, 0),
                Height = new SvgUnit(SvgUnitType.User, 0),
                Href = ImageHref
            },
            Tool.Image => new SvgImage
            {
                X = new SvgUnit(SvgUnitType.User, start.X),
                Y = new SvgUnit(SvgUnitType.User, start.Y),
                Width = new SvgUnit(SvgUnitType.User, 0),
                Height = new SvgUnit(SvgUnitType.User, 0),
                Href = CreatePlaceholderImageHref()
            },
            Tool.Freehand => CreateFreehand(start),
            Tool.Brush => CreateFreehand(start, 6f, Tool.Brush),
            Tool.Pencil => CreateFreehand(start, 2f, Tool.Pencil),
            _ => null!
        };
    }

    private SvgPath CreatePath(ShimSkiaSharp.SKPoint start, Tool tool)
    {
        var path = new SvgPath
        {
            Stroke = new SvgColourServer(System.Drawing.Color.Black),
            StrokeWidth = new SvgUnit(CurrentStrokeWidth)
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

    private SvgPath CreateFreehand(ShimSkiaSharp.SKPoint start)
    {
        return CreateFreehand(start, Math.Max(CurrentStrokeWidth, 1f), Tool.Freehand);
    }

    private SvgPath CreateFreehand(ShimSkiaSharp.SKPoint start, float strokeWidth, Tool tool)
    {
        var path = new SvgPath
        {
            Fill = SvgPaintServer.None,
            Stroke = new SvgColourServer(System.Drawing.Color.Black),
            StrokeWidth = new SvgUnit(strokeWidth),
            StrokeLineCap = SvgStrokeLineCap.Round,
            StrokeLineJoin = SvgStrokeLineJoin.Round,
            PathData = new SvgPathSegmentList
            {
                new SvgMoveToSegment(false, new System.Drawing.PointF(start.X, start.Y))
            }
        };

        SetSemanticTool(path, tool);
        return path;
    }

    public void UpdateElement(SvgVisualElement element, Tool tool, ShimSkiaSharp.SKPoint start, ShimSkiaSharp.SKPoint current, bool snapToGrid, Func<float, float> snap)
    {
        switch (tool)
        {
            case Tool.Line or Tool.Arrow when element is SvgLine ln:
                ln.EndX = new SvgUnit(ln.EndX.Type, snapToGrid ? snap(current.X) : current.X);
                ln.EndY = new SvgUnit(ln.EndY.Type, snapToGrid ? snap(current.Y) : current.Y);
                break;
            case Tool.Frame or Tool.Section when element is SvgGroup frame:
                var fx = Math.Min(start.X, current.X);
                var fy = Math.Min(start.Y, current.Y);
                var fw = Math.Abs(current.X - start.X);
                var fh = Math.Abs(current.Y - start.Y);
                if (snapToGrid)
                {
                    fx = snap(fx);
                    fy = snap(fy);
                    fw = snap(fw);
                    fh = snap(fh);
                }

                UpdateFrame(frame, fx, fy, fw, fh);
                break;
            case Tool.Rect or Tool.Slice when element is SvgRectangle r:
                var x = Math.Min(start.X, current.X);
                var y = Math.Min(start.Y, current.Y);
                var w = Math.Abs(current.X - start.X);
                var h = Math.Abs(current.Y - start.Y);
                if (snapToGrid)
                {
                    x = snap(x);
                    y = snap(y);
                    w = snap(w);
                    h = snap(h);
                }
                r.X = new SvgUnit(r.X.Type, x);
                r.Y = new SvgUnit(r.Y.Type, y);
                r.Width = new SvgUnit(r.Width.Type, w);
                r.Height = new SvgUnit(r.Height.Type, h);
                break;
            case Tool.Image when element is SvgImage img:
                var ix = Math.Min(start.X, current.X);
                var iy = Math.Min(start.Y, current.Y);
                var iw = Math.Abs(current.X - start.X);
                var ih = Math.Abs(current.Y - start.Y);
                if (snapToGrid)
                {
                    ix = snap(ix);
                    iy = snap(iy);
                    iw = snap(iw);
                    ih = snap(ih);
                }
                img.X = new SvgUnit(img.X.Type, ix);
                img.Y = new SvgUnit(img.Y.Type, iy);
                img.Width = new SvgUnit(img.Width.Type, iw);
                img.Height = new SvgUnit(img.Height.Type, ih);
                break;
            case Tool.Circle when element is SvgCircle c:
                var cx = (start.X + current.X) / 2f;
                var cy = (start.Y + current.Y) / 2f;
                var rv = Math.Max(Math.Abs(current.X - start.X), Math.Abs(current.Y - start.Y)) / 2f;
                if (snapToGrid)
                {
                    cx = snap(cx);
                    cy = snap(cy);
                    rv = snap(rv);
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
                    ecx = snap(ecx);
                    ecy = snap(ecy);
                    rx = snap(rx);
                    ry = snap(ry);
                }
                el.CenterX = new SvgUnit(el.CenterX.Type, ecx);
                el.CenterY = new SvgUnit(el.CenterY.Type, ecy);
                el.RadiusX = new SvgUnit(el.RadiusX.Type, rx);
                el.RadiusY = new SvgUnit(el.RadiusY.Type, ry);
                break;
            case Tool.Star when element is SvgPolygon star:
                UpdateStar(star, start, current, snapToGrid, snap);
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
            case Tool.TextPath when element is SvgTextPath tp:
                var so = snapToGrid ? snap(current.X) : current.X;
                tp.StartOffset = new SvgUnit(tp.StartOffset.Type, so);
                break;
            case Tool.TextArea when element is SvgText tArea:
                if (tArea.X.Count == 0)
                    tArea.X.Add(new SvgUnit(SvgUnitType.User, 0));
                if (tArea.Y.Count == 0)
                    tArea.Y.Add(new SvgUnit(SvgUnitType.User, 0));
                var tax = snapToGrid ? snap(current.X) : current.X;
                var tay = snapToGrid ? snap(current.Y) : current.Y;
                tArea.X[0] = new SvgUnit(tArea.X[0].Type, tax);
                tArea.Y[0] = new SvgUnit(tArea.Y[0].Type, tay);
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

    public void AddFreehandPoint(SvgVisualElement element, ShimSkiaSharp.SKPoint point, bool snapToGrid, Func<float, float> snap)
    {
        if (element is not SvgPath path)
            return;
        var x = snapToGrid ? snap(point.X) : point.X;
        var y = snapToGrid ? snap(point.Y) : point.Y;
        if (path.PathData.Count == 0)
            path.PathData.Add(new SvgMoveToSegment(false, new System.Drawing.PointF(x, y)));
        else
            path.PathData.Add(new SvgLineSegment(false, new System.Drawing.PointF(x, y)));
    }

    public static bool IsFreehandTool(Tool tool)
    {
        return tool is Tool.Freehand or Tool.Brush or Tool.Pencil;
    }

    public static bool TryGetSemanticTool(SvgElement? element, out Tool tool)
    {
        if (element?.CustomAttributes.TryGetValue(SemanticToolAttribute, out var value) == true
            && Enum.TryParse(value, true, out tool))
        {
            return true;
        }

        tool = default;
        return false;
    }

    public static void SetSemanticTool(SvgElement element, Tool tool)
    {
        element.CustomAttributes[SemanticToolAttribute] = tool.ToString();
    }

    private static SvgGroup CreateFrame(ShimSkiaSharp.SKPoint start, FrameContainerKind kind)
    {
        var background = FrameService.CreateBackgroundRect(
            id: null,
            x: start.X,
            y: start.Y,
            width: 0,
            height: 0,
            kind: kind);

        var content = new SvgGroup();
        content.CustomAttributes[AutoLayoutService.FrameContentAttribute] = "true";

        var group = new SvgGroup();
        FrameService.SetContainerKind(group, kind);
        group.Children.Add(background);
        group.Children.Add(content);
        FrameService.SyncMetadata(group);
        return group;
    }

    private static SvgRectangle CreateSlice(ShimSkiaSharp.SKPoint start)
    {
        var rect = new SvgRectangle
        {
            X = new SvgUnit(SvgUnitType.User, start.X),
            Y = new SvgUnit(SvgUnitType.User, start.Y),
            Width = new SvgUnit(SvgUnitType.User, 0),
            Height = new SvgUnit(SvgUnitType.User, 0),
            Fill = SvgPaintServer.None,
            Stroke = new SvgColourServer(System.Drawing.Color.FromArgb(9, 112, 218)),
            StrokeWidth = new SvgUnit(1f),
            StrokeDashArray = new SvgUnitCollection
            {
                new(6f),
                new(4f)
            }
        };

        rect.CustomAttributes[SliceFlagAttribute] = "true";
        SetSemanticTool(rect, Tool.Slice);
        return rect;
    }

    private SvgLine CreateArrow(SvgElement parent, ShimSkiaSharp.SKPoint start)
    {
        var document = parent as SvgDocument ?? parent.OwnerDocument;
        if (document is not null)
        {
            EnsureArrowMarker(document);
        }

        var line = new SvgLine
        {
            StartX = new SvgUnit(SvgUnitType.User, start.X),
            StartY = new SvgUnit(SvgUnitType.User, start.Y),
            EndX = new SvgUnit(SvgUnitType.User, start.X),
            EndY = new SvgUnit(SvgUnitType.User, start.Y),
            Stroke = new SvgColourServer(System.Drawing.Color.Black),
            StrokeWidth = new SvgUnit(Math.Max(CurrentStrokeWidth, 2f)),
            MarkerEnd = new Uri($"url(#{ArrowMarkerId})", UriKind.Relative)
        };

        SetSemanticTool(line, Tool.Arrow);
        return line;
    }

    private static void EnsureArrowMarker(SvgDocument document)
    {
        var definitions = document.Children.OfType<SvgDefinitionList>().FirstOrDefault();
        if (definitions is null)
        {
            definitions = new SvgDefinitionList
            {
                ID = "editor-defs"
            };
            document.Children.Insert(0, definitions);
        }

        if (definitions.Children.OfType<SvgMarker>().Any(marker => string.Equals(marker.ID, ArrowMarkerId, StringComparison.Ordinal)))
        {
            return;
        }

        var arrowHead = new SvgPolygon
        {
            Fill = new SvgColourServer(System.Drawing.Color.Black),
            Stroke = SvgPaintServer.None,
            Points = new SvgPointCollection
            {
                new(0f), new(0f),
                new(8f), new(4f),
                new(0f), new(8f),
                new(2.25f), new(4f)
            }
        };

        var marker = new SvgMarker
        {
            ID = ArrowMarkerId,
            MarkerUnits = SvgMarkerUnits.StrokeWidth,
            MarkerWidth = 8,
            MarkerHeight = 8,
            RefX = 7,
            RefY = 4,
            Orient = new SvgOrient { IsAuto = true }
        };
        marker.Children.Add(arrowHead);
        definitions.Children.Add(marker);
    }

    private SvgPolygon CreateStar(ShimSkiaSharp.SKPoint start)
    {
        var polygon = new SvgPolygon
        {
            Fill = SvgPaintServer.None,
            Stroke = new SvgColourServer(System.Drawing.Color.Black),
            StrokeWidth = new SvgUnit(Math.Max(CurrentStrokeWidth, 1f))
        };

        SetSemanticTool(polygon, Tool.Star);
        UpdateStar(polygon, start, start, snapToGrid: false, static value => value);
        return polygon;
    }

    private static void UpdateStar(SvgPolygon polygon, ShimSkiaSharp.SKPoint start, ShimSkiaSharp.SKPoint current, bool snapToGrid, Func<float, float> snap)
    {
        var left = Math.Min(start.X, current.X);
        var top = Math.Min(start.Y, current.Y);
        var width = Math.Abs(current.X - start.X);
        var height = Math.Abs(current.Y - start.Y);

        if (snapToGrid)
        {
            left = snap(left);
            top = snap(top);
            width = snap(width);
            height = snap(height);
        }

        var centerX = left + (width / 2f);
        var centerY = top + (height / 2f);
        var outerRadiusX = width / 2f;
        var outerRadiusY = height / 2f;
        var innerRadiusX = outerRadiusX * 0.45f;
        var innerRadiusY = outerRadiusY * 0.45f;

        var points = new SvgPointCollection();
        for (var index = 0; index < 10; index++)
        {
            var angle = (-Math.PI / 2.0) + (index * (Math.PI / 5.0));
            var radiusX = index % 2 == 0 ? outerRadiusX : innerRadiusX;
            var radiusY = index % 2 == 0 ? outerRadiusY : innerRadiusY;
            points.Add(new SvgUnit(SvgUnitType.User, centerX + ((float)Math.Cos(angle) * radiusX)));
            points.Add(new SvgUnit(SvgUnitType.User, centerY + ((float)Math.Sin(angle) * radiusY)));
        }

        polygon.Points = points;
    }

    private static string CreatePlaceholderImageHref()
    {
        const string svg =
            "<svg xmlns='http://www.w3.org/2000/svg' width='240' height='180' viewBox='0 0 240 180'>"
            + "<rect width='240' height='180' rx='18' fill='#F3F4F6'/>"
            + "<circle cx='84' cy='66' r='18' fill='#D2D9E2'/>"
            + "<path d='M42 136l42-46 28 31 30-29 56 44H42z' fill='#BCC6D2'/>"
            + "</svg>";

        return $"data:image/svg+xml;utf8,{Uri.EscapeDataString(svg)}";
    }

    private static void UpdateFrame(SvgGroup frame, float x, float y, float width, float height)
    {
        if (!FrameService.TryGetBackground(frame, out var background))
        {
            background = FrameService.CreateBackgroundRect(
                id: null,
                x: x,
                y: y,
                width: width,
                height: height,
                kind: FrameService.GetContainerKind(frame));
            frame.Children.Insert(0, background);
        }

        background.X = new SvgUnit(background.X.Type, x);
        background.Y = new SvgUnit(background.Y.Type, y);
        background.Width = new SvgUnit(background.Width.Type, width);
        background.Height = new SvgUnit(background.Height.Type, height);
        FrameService.SyncMetadata(frame);
    }
}
