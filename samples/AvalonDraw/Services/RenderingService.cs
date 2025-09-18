using System.Collections.Generic;
using System.Linq;
using AvalonDraw;
using AvalonDraw.Services;
using Svg;
using Svg.Model.Drawables;
using Svg.Pathing;
using static AvalonDraw.Services.SelectionService;
using Shim = ShimSkiaSharp;
using SK = SkiaSharp;

// Provides rendering helpers for editor overlays

namespace AvalonDraw.Services;

public class RenderingService
{
    private readonly PathService _pathService;
    private readonly ToolService _toolService;

    private readonly SK.SKColor _boundsColor = SK.SKColors.Red;
    private readonly SK.SKColor _segmentColor = SK.SKColors.OrangeRed;
    private readonly SK.SKColor _controlColor = SK.SKColors.SkyBlue;

    private const float HandleSize = 10f;

    private static void DrawProfile(SK.SKCanvas canvas, SK.SKPath path, StrokeProfile profile, SK.SKPaint paint)
    {
        using var measure = new SK.SKPathMeasure(path, false);
        var length = measure.Length;
        var pts = profile.Points.OrderBy(p => p.Offset).ToList();
        for (var i = 1; i < pts.Count; i++)
        {
            using var seg = new SK.SKPath();
            var start = (float)pts[i - 1].Offset * length;
            var end = (float)pts[i].Offset * length;
            if (measure.GetSegment(start, end, seg, true))
            {
                paint.StrokeWidth = (float)((pts[i - 1].Width + pts[i].Width) / 2.0);
                canvas.DrawPath(seg, paint);
            }
        }
    }

    public RenderingService(PathService pathService, ToolService toolService)
    {
        _pathService = pathService;
        _toolService = toolService;
    }


    public void Draw(SK.SKCanvas canvas,
        SK.SKPicture? picture,
        SK.SKRect? rootBounds,
        SK.SKRect? artboardBounds,
        float scale,
        bool snapToGrid,
        bool showGrid,
        double gridSize,
        IEnumerable<LayerService.LayerEntry> layers,
        LayerService.LayerEntry? selectedLayer,
        IList<DrawableBase> selectedDrawables,
        System.Func<DrawableBase, BoundsInfo> getBounds,
        bool polyEditing,
        DrawableBase? editPolyDrawable,
        bool editPolyline,
        IList<Shim.SKPoint> polyPoints,
        Shim.SKMatrix polyMatrix)
    {
        int clipSave = -1;
        if (artboardBounds is { })
        {
            clipSave = canvas.Save();
            canvas.ClipRect(artboardBounds.Value);
        }

        if (snapToGrid && showGrid && gridSize > 0 && picture is { })
        {
            using var gridPaint = new SK.SKPaint
            {
                IsAntialias = false,
                Style = SK.SKPaintStyle.Stroke,
                Color = SK.SKColors.LightGray,
                StrokeWidth = 1f / scale
            };
            var bounds = picture.CullRect;
            for (float x = 0; x <= bounds.Width; x += (float)gridSize)
                canvas.DrawLine(x, 0, x, bounds.Height, gridPaint);
            for (float y = 0; y <= bounds.Height; y += (float)gridSize)
                canvas.DrawLine(0, y, bounds.Width, y, gridPaint);
        }

        if (rootBounds is { })
        {
            using var rootPaint = new SK.SKPaint
            {
                IsAntialias = true,
                Style = SK.SKPaintStyle.Stroke,
                Color = SK.SKColors.Gray,
                StrokeWidth = 1f / scale,
                PathEffect = SK.SKPathEffect.CreateDash(new float[] { 4f / scale, 4f / scale }, 0)
            };
            canvas.DrawRect(rootBounds.Value, rootPaint);
        }

        if (layers is { })
        {
            using var layerPaint = new SK.SKPaint
            {
                IsAntialias = true,
                Style = SK.SKPaintStyle.Stroke,
                Color = SK.SKColors.SeaGreen,
                StrokeWidth = 1f / scale,
                PathEffect = SK.SKPathEffect.CreateDash(new float[] { 4f / scale, 4f / scale }, 0)
            };
            using var selectedPaint = new SK.SKPaint
            {
                IsAntialias = true,
                Style = SK.SKPaintStyle.Stroke,
                Color = SK.SKColors.DodgerBlue,
                StrokeWidth = 1f / scale,
                PathEffect = SK.SKPathEffect.CreateDash(new float[] { 4f / scale, 4f / scale }, 0)
            };
            void DrawLayer(LayerService.LayerEntry info)
            {
                if (info.Drawable is { })
                {
                    var r = info.Drawable.TransformedBounds;
                    var rect = new SK.SKRect(r.Left, r.Top, r.Right, r.Bottom);
                    canvas.DrawRect(rect, info == selectedLayer ? selectedPaint : layerPaint);
                }
                foreach (var child in info.Sublayers)
                    DrawLayer(child);
            }
            foreach (var layer in layers)
                DrawLayer(layer);
        }

        if (selectedDrawables is null || selectedDrawables.Count == 0)
            return;

        using var paint = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Stroke,
            Color = _boundsColor,
            StrokeWidth = 1f / scale
        };
        var hs = HandleSize / 2f / scale;
        var size = HandleSize / scale;
        using var fill = new SK.SKPaint { IsAntialias = true, Style = SK.SKPaintStyle.Fill, Color = SK.SKColors.White };
        foreach (var selectedDrawable in selectedDrawables)
        {
            var info = getBounds(selectedDrawable);

            if (_toolService.CurrentTool != ToolService.Tool.PathSelect &&
                _toolService.CurrentTool != ToolService.Tool.PolygonSelect &&
                _toolService.CurrentTool != ToolService.Tool.PolylineSelect)
            {
                using (var path = new SK.SKPath())
                {
                    path.MoveTo(info.TL);
                    path.LineTo(info.TR);
                    path.LineTo(info.BR);
                    path.LineTo(info.BL);
                    path.Close();
                    canvas.DrawPath(path, paint);
                }

                var pts = new[] { info.TL, info.TopMid, info.TR, info.RightMid, info.BR, info.BottomMid, info.BL, info.LeftMid };
                foreach (var pt in pts)
                {
                    canvas.DrawRect(pt.X - hs, pt.Y - hs, size, size, fill);
                    canvas.DrawRect(pt.X - hs, pt.Y - hs, size, size, paint);
                }

                canvas.DrawLine(info.TopMid, info.RotHandle, paint);
                canvas.DrawCircle(info.RotHandle, hs, fill);
                canvas.DrawCircle(info.RotHandle, hs, paint);
            }

            if (selectedDrawable.Element is SvgVisualElement vis &&
                vis.CustomAttributes.TryGetValue("stroke-profile", out var prof))
            {
                var profile = StrokeProfile.Parse(prof);
                var skPath = PathService.ElementToPath(vis);
                if (skPath is { })
                {
                    using var spaint = new SK.SKPaint { IsAntialias = true, Style = SK.SKPaintStyle.Stroke, Color = SK.SKColors.Black };
                    DrawProfile(canvas, skPath, profile, spaint);
                }
            }

            if (_pathService.IsEditing && _pathService.EditDrawable == selectedDrawable)
            {
                using var segPaint = new SK.SKPaint
                {
                    IsAntialias = true,
                    Style = SK.SKPaintStyle.Stroke,
                    Color = _segmentColor,
                    StrokeWidth = 2f / scale,
                    PathEffect = SK.SKPathEffect.CreateDash(new float[] { 6f / scale, 4f / scale }, 0)
                };
                using var ctrlPaint = new SK.SKPaint
                {
                    IsAntialias = true,
                    Style = SK.SKPaintStyle.Stroke,
                    Color = _controlColor,
                    StrokeWidth = 1f / scale,
                    PathEffect = SK.SKPathEffect.CreateDash(new float[] { 4f / scale, 4f / scale }, 0)
                };

                if (_pathService.EditPath is { } path)
                {
                    var segs = path.PathData;
                    var cur = new Shim.SKPoint();
                    var start = new Shim.SKPoint();
                    bool haveStart = false;
                    foreach (var seg in segs)
                    {
                        switch (seg)
                        {
                            case SvgMoveToSegment mv:
                                cur = new Shim.SKPoint(mv.End.X, mv.End.Y);
                                if (!haveStart)
                                {
                                    start = cur;
                                    haveStart = true;
                                }
                                else
                                {
                                    start = cur;
                                }
                                break;
                            case SvgLineSegment ln:
                                var lnEnd = new Shim.SKPoint(ln.End.X, ln.End.Y);
                                var scur = _pathService.PathMatrix.MapPoint(cur);
                                var sln = _pathService.PathMatrix.MapPoint(lnEnd);
                                canvas.DrawLine(scur.X, scur.Y, sln.X, sln.Y, segPaint);
                                cur = lnEnd;
                                break;
                            case SvgCubicCurveSegment c:
                                var c1 = new Shim.SKPoint(c.FirstControlPoint.X, c.FirstControlPoint.Y);
                                var c2 = new Shim.SKPoint(c.SecondControlPoint.X, c.SecondControlPoint.Y);
                                var ce = new Shim.SKPoint(c.End.X, c.End.Y);
                                scur = _pathService.PathMatrix.MapPoint(cur);
                                var sc1 = _pathService.PathMatrix.MapPoint(c1);
                                var sc2 = _pathService.PathMatrix.MapPoint(c2);
                                var sce = _pathService.PathMatrix.MapPoint(ce);
                                canvas.DrawLine(scur.X, scur.Y, sc1.X, sc1.Y, ctrlPaint);
                                canvas.DrawLine(sce.X, sce.Y, sc2.X, sc2.Y, ctrlPaint);
                                canvas.DrawLine(scur.X, scur.Y, sc1.X, sc1.Y, segPaint);
                                canvas.DrawLine(sc1.X, sc1.Y, sc2.X, sc2.Y, segPaint);
                                canvas.DrawLine(sc2.X, sc2.Y, sce.X, sce.Y, segPaint);
                                cur = ce;
                                break;
                            case SvgQuadraticCurveSegment q:
                                var qp = new Shim.SKPoint(q.ControlPoint.X, q.ControlPoint.Y);
                                var qe = new Shim.SKPoint(q.End.X, q.End.Y);
                                scur = _pathService.PathMatrix.MapPoint(cur);
                                var sqp = _pathService.PathMatrix.MapPoint(qp);
                                var sqe = _pathService.PathMatrix.MapPoint(qe);
                                canvas.DrawLine(scur.X, scur.Y, sqp.X, sqp.Y, ctrlPaint);
                                canvas.DrawLine(sqe.X, sqe.Y, sqp.X, sqp.Y, ctrlPaint);
                                canvas.DrawLine(scur.X, scur.Y, sqp.X, sqp.Y, segPaint);
                                canvas.DrawLine(sqp.X, sqp.Y, sqe.X, sqe.Y, segPaint);
                                cur = qe;
                                break;
                            case SvgArcSegment a:
                                var ae = new Shim.SKPoint(a.End.X, a.End.Y);
                                scur = _pathService.PathMatrix.MapPoint(cur);
                                var sae = _pathService.PathMatrix.MapPoint(ae);
                                canvas.DrawLine(scur.X, scur.Y, sae.X, sae.Y, segPaint);
                                cur = ae;
                                break;
                            case SvgClosePathSegment _:
                                scur = _pathService.PathMatrix.MapPoint(cur);
                                var sstart = _pathService.PathMatrix.MapPoint(start);
                                canvas.DrawLine(scur.X, scur.Y, sstart.X, sstart.Y, segPaint);
                                cur = start;
                                break;
                        }
                    }
                }

                foreach (var p in _pathService.PathPoints)
                {
                    var pt = _pathService.PathMatrix.MapPoint(p.Point);
                    canvas.DrawRect(pt.X - hs, pt.Y - hs, size, size, fill);
                    canvas.DrawRect(pt.X - hs, pt.Y - hs, size, size, paint);
                }
            }

            if (polyEditing && editPolyDrawable == selectedDrawable)
            {
                using var segPaint = new SK.SKPaint
                {
                    IsAntialias = true,
                    Style = SK.SKPaintStyle.Stroke,
                    Color = _segmentColor,
                    StrokeWidth = 2f / scale,
                    PathEffect = SK.SKPathEffect.CreateDash(new float[] { 6f / scale, 4f / scale }, 0)
                };
                var last = polyPoints.Count - 1;
                for (int i = 0; i < last; i++)
                {
                    var a = polyMatrix.MapPoint(polyPoints[i]);
                    var b = polyMatrix.MapPoint(polyPoints[i + 1]);
                    canvas.DrawLine(a.X, a.Y, b.X, b.Y, segPaint);
                }
                if (!editPolyline && polyPoints.Count > 2)
                {
                    var a = polyMatrix.MapPoint(polyPoints[^1]);
                    var b = polyMatrix.MapPoint(polyPoints[0]);
                    canvas.DrawLine(a.X, a.Y, b.X, b.Y, segPaint);
                }
                foreach (var p in polyPoints)
                {
                    var pt = polyMatrix.MapPoint(p);
                    canvas.DrawRect(pt.X - hs, pt.Y - hs, size, size, fill);
                    canvas.DrawRect(pt.X - hs, pt.Y - hs, size, size, paint);
                }
            }

            if (clipSave != -1)
                canvas.Restore();
        }
    }

}
