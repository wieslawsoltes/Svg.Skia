using System.Globalization;
using System.Numerics;
using Svg;
using Svg.Editor.Skia;
using Svg.Editor.Svg.Models;
using Svg.Model.Drawables;
using Svg.Pathing;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using SK = SkiaSharp;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed class SvgEditorOverlayCanvas : SKCanvasElement
{
    private const float GuidePixelTolerance = 6f;
    private const float AnnotationGapPixels = 10f;
    private const float AnnotationTickPixels = 6f;
    private const float PathHandleSize = 10f;

    private IReadOnlyList<DrawableBase> _selectedDrawables = Array.Empty<DrawableBase>();
    private IReadOnlyList<DrawableBase> _allDrawables = Array.Empty<DrawableBase>();
    private IReadOnlyList<PathPoint> _pathPoints = Array.Empty<PathPoint>();
    private Rect? _marquee;

    public SvgEditorOverlayCanvas()
    {
        OverlayRenderer = new SvgEditorOverlayRenderer();
        SelectionService = new SelectionService();
    }

    public global::Uno.Svg.Skia.Svg? SvgView { get; set; }

    public SvgEditorOverlayRenderer OverlayRenderer { get; }

    public SelectionService SelectionService { get; }

    public bool ShowGrid { get; set; }

    public bool SnapToGrid { get; set; }

    public double GridSize { get; set; } = 16.0;

    public IReadOnlyList<DrawableBase> SelectedDrawables
    {
        get => _selectedDrawables;
        set => _selectedDrawables = value ?? Array.Empty<DrawableBase>();
    }

    public IReadOnlyList<DrawableBase> AllDrawables
    {
        get => _allDrawables;
        set => _allDrawables = value ?? Array.Empty<DrawableBase>();
    }

    public Rect? Marquee
    {
        get => _marquee;
        set => _marquee = value;
    }

    public bool IsPathEditing { get; set; }

    public bool ShowSelectionAnnotations { get; set; }

    public SvgPath? EditPath { get; set; }

    public DrawableBase? EditPathDrawable { get; set; }

    public IReadOnlyList<PathPoint> PathPoints
    {
        get => _pathPoints;
        set => _pathPoints = value ?? Array.Empty<PathPoint>();
    }

    public ShimSkiaSharp.SKMatrix PathMatrix { get; set; } = ShimSkiaSharp.SKMatrix.CreateIdentity();

    public int ActivePathPoint { get; set; } = -1;

    protected override void RenderOverride(SK.SKCanvas canvas, Size area)
    {
        var svgView = SvgView;
        var skSvg = svgView?.SkSvg;
        var picture = svgView?.Picture;

        if (svgView is not null
            && skSvg is not null
            && picture is not null
            && svgView.TryGetViewMatrix(out var matrix))
        {
            using var restore = new SK.SKAutoCanvasRestore(canvas, true);
            var svgOrigin = svgView.TransformToVisual(this).TransformPoint(new Point(0.0, 0.0));
            canvas.Translate((float)svgOrigin.X, (float)svgOrigin.Y);
            var skMatrix = ToSkMatrix(matrix);
            canvas.Concat(in skMatrix);

            var pictureBounds = picture.CullRect;
            OverlayRenderer.Draw(
                canvas,
                picture,
                pictureBounds,
                null,
                GetCanvasScale(matrix),
                SnapToGrid,
                ShowGrid,
                GridSize,
                Array.Empty<LayerEntry>(),
                null,
                SelectedDrawables.ToList(),
                drawable => SelectionService.GetBoundsInfo(drawable, skSvg, () => GetCanvasScale(matrix)),
                false,
                null,
                false,
                Array.Empty<ShimSkiaSharp.SKPoint>(),
                ShimSkiaSharp.SKMatrix.CreateIdentity());

            if (ShowSelectionAnnotations)
            {
                DrawHelperGuides(canvas, GetCanvasScale(matrix));
            }

            DrawPathEditorOverlay(canvas, GetCanvasScale(matrix));
        }

        if (Marquee is { } marquee)
        {
            using var fill = new SK.SKPaint
            {
                IsAntialias = true,
                Style = SK.SKPaintStyle.Fill,
                Color = new SK.SKColor(84, 160, 255, 28)
            };

            using var stroke = new SK.SKPaint
            {
                IsAntialias = true,
                Style = SK.SKPaintStyle.Stroke,
                Color = new SK.SKColor(84, 160, 255),
                StrokeWidth = 1.5f,
                PathEffect = SK.SKPathEffect.CreateDash(new float[] { 6f, 4f }, 0)
            };

            var rect = new SK.SKRect(
                (float)marquee.X,
                (float)marquee.Y,
                (float)(marquee.X + marquee.Width),
                (float)(marquee.Y + marquee.Height));

            canvas.DrawRect(rect, fill);
            canvas.DrawRect(rect, stroke);
        }
    }

    private void DrawPathEditorOverlay(SK.SKCanvas canvas, float scale)
    {
        if (!IsPathEditing || EditPath is null || EditPathDrawable is null)
        {
            return;
        }

        using var segmentPaint = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Stroke,
            Color = new SK.SKColor(13, 153, 255),
            StrokeWidth = 2f / scale,
            PathEffect = SK.SKPathEffect.CreateDash(new float[] { 6f / scale, 4f / scale }, 0)
        };
        using var controlPaint = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Stroke,
            Color = new SK.SKColor(111, 207, 255),
            StrokeWidth = 1f / scale,
            PathEffect = SK.SKPathEffect.CreateDash(new float[] { 4f / scale, 4f / scale }, 0)
        };
        using var anchorFill = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Fill,
            Color = SK.SKColors.White
        };
        using var activeAnchorFill = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Fill,
            Color = new SK.SKColor(13, 153, 255)
        };
        using var controlFill = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Fill,
            Color = new SK.SKColor(223, 244, 255)
        };
        using var stroke = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Stroke,
            Color = new SK.SKColor(13, 153, 255),
            StrokeWidth = 1.25f / scale
        };

        DrawPathSegments(canvas, EditPath, segmentPaint, controlPaint);

        var handleHalf = (PathHandleSize / scale) / 2f;
        for (var index = 0; index < PathPoints.Count; index++)
        {
            var point = PathMatrix.MapPoint(PathPoints[index].Point);
            if (PathPoints[index].Type == 0)
            {
                var fill = index == ActivePathPoint ? activeAnchorFill : anchorFill;
                canvas.DrawRect(point.X - handleHalf, point.Y - handleHalf, handleHalf * 2f, handleHalf * 2f, fill);
                canvas.DrawRect(point.X - handleHalf, point.Y - handleHalf, handleHalf * 2f, handleHalf * 2f, stroke);
            }
            else
            {
                canvas.DrawCircle(point.X, point.Y, handleHalf * 0.82f, controlFill);
                canvas.DrawCircle(point.X, point.Y, handleHalf * 0.82f, stroke);
            }
        }
    }

    private void DrawPathSegments(SK.SKCanvas canvas, SvgPath path, SK.SKPaint segmentPaint, SK.SKPaint controlPaint)
    {
        var current = new ShimSkiaSharp.SKPoint();
        var start = new ShimSkiaSharp.SKPoint();
        var hasStart = false;

        foreach (var segment in path.PathData)
        {
            switch (segment)
            {
                case SvgMoveToSegment move:
                    current = new ShimSkiaSharp.SKPoint(move.End.X, move.End.Y);
                    start = current;
                    hasStart = true;
                    break;
                case SvgLineSegment line:
                    {
                        var end = new ShimSkiaSharp.SKPoint(line.End.X, line.End.Y);
                        DrawPathLine(canvas, current, end, segmentPaint);
                        current = end;
                        break;
                    }
                case SvgCubicCurveSegment cubic:
                    {
                        var controlOne = new ShimSkiaSharp.SKPoint(cubic.FirstControlPoint.X, cubic.FirstControlPoint.Y);
                        var controlTwo = new ShimSkiaSharp.SKPoint(cubic.SecondControlPoint.X, cubic.SecondControlPoint.Y);
                        var end = new ShimSkiaSharp.SKPoint(cubic.End.X, cubic.End.Y);
                        DrawPathLine(canvas, current, controlOne, controlPaint);
                        DrawPathLine(canvas, end, controlTwo, controlPaint);
                        DrawPathLine(canvas, current, controlOne, segmentPaint);
                        DrawPathLine(canvas, controlOne, controlTwo, segmentPaint);
                        DrawPathLine(canvas, controlTwo, end, segmentPaint);
                        current = end;
                        break;
                    }
                case SvgQuadraticCurveSegment quadratic:
                    {
                        var control = new ShimSkiaSharp.SKPoint(quadratic.ControlPoint.X, quadratic.ControlPoint.Y);
                        var end = new ShimSkiaSharp.SKPoint(quadratic.End.X, quadratic.End.Y);
                        DrawPathLine(canvas, current, control, controlPaint);
                        DrawPathLine(canvas, end, control, controlPaint);
                        DrawPathLine(canvas, current, control, segmentPaint);
                        DrawPathLine(canvas, control, end, segmentPaint);
                        current = end;
                        break;
                    }
                case SvgArcSegment arc:
                    {
                        var end = new ShimSkiaSharp.SKPoint(arc.End.X, arc.End.Y);
                        DrawPathLine(canvas, current, end, segmentPaint);
                        current = end;
                        break;
                    }
                case SvgClosePathSegment _ when hasStart:
                    DrawPathLine(canvas, current, start, segmentPaint);
                    current = start;
                    break;
            }
        }
    }

    private void DrawPathLine(SK.SKCanvas canvas, ShimSkiaSharp.SKPoint start, ShimSkiaSharp.SKPoint end, SK.SKPaint paint)
    {
        var mappedStart = PathMatrix.MapPoint(start);
        var mappedEnd = PathMatrix.MapPoint(end);
        canvas.DrawLine(mappedStart.X, mappedStart.Y, mappedEnd.X, mappedEnd.Y, paint);
    }

    private void DrawHelperGuides(SK.SKCanvas canvas, float scale)
    {
        if (_selectedDrawables.Count == 0)
        {
            return;
        }

        var selectionRects = _selectedDrawables
            .Select(GetGuideBounds)
            .Where(static rect => rect.HasValue)
            .Select(static rect => rect!.Value)
            .ToArray();

        if (selectionRects.Length == 0)
        {
            return;
        }

        var selectionBounds = selectionRects[0];
        for (var index = 1; index < selectionRects.Length; index++)
        {
            selectionBounds = Union(selectionBounds, selectionRects[index]);
        }

        var candidates = _allDrawables
            .Where(drawable => !_selectedDrawables.Any(selected => ReferenceEquals(selected, drawable)))
            .Where(static drawable => drawable.Element is SvgVisualElement element
                                      && !string.Equals(element.Display, "none", StringComparison.OrdinalIgnoreCase))
            .Select(GetGuideBounds)
            .Where(static rect => rect.HasValue)
            .Select(static rect => rect!.Value)
            .ToArray();

        DrawSelectionSizeBadge(canvas, selectionBounds, scale);
        DrawAlignmentGuides(canvas, selectionBounds, candidates, scale);
        DrawSpacingAnnotations(canvas, selectionBounds, candidates, scale);
    }

    private static SK.SKRect? GetGuideBounds(DrawableBase drawable)
    {
        var bounds = drawable.TransformedBounds;
        if (bounds.IsEmpty)
        {
            return null;
        }

        return new SK.SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
    }

    private static void DrawSelectionSizeBadge(SK.SKCanvas canvas, SK.SKRect selectionBounds, float scale)
    {
        var label = $"{MathF.Round(selectionBounds.Width):0} × {MathF.Round(selectionBounds.Height):0}";
        using var text = CreateTextPaint(SK.SKColors.White);
        using var font = CreateFont(12f / scale, SK.SKFontStyleWeight.SemiBold);
        using var fill = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Fill,
            Color = new SK.SKColor(13, 153, 255)
        };

        var width = Math.Max(34f / scale, font.MeasureText(label, text) + (14f / scale));
        var height = 22f / scale;
        var left = selectionBounds.MidX - (width / 2f);
        var top = MathF.Max(6f / scale, selectionBounds.Top - (28f / scale));
        var badge = new SK.SKRoundRect(new SK.SKRect(left, top, left + width, top + height), 6f / scale, 6f / scale);

        canvas.DrawRoundRect(badge, fill);
        canvas.DrawText(label, badge.Rect.Left + (7f / scale), badge.Rect.MidY + (4f / scale), font, text);
    }

    private static void DrawAlignmentGuides(SK.SKCanvas canvas, SK.SKRect selectionBounds, IReadOnlyList<SK.SKRect> candidates, float scale)
    {
        var tolerance = GuidePixelTolerance / scale;
        var verticalGuides = new List<GuideLine>();
        var horizontalGuides = new List<GuideLine>();

        foreach (var candidate in candidates)
        {
            TryAppendGuide(
                selectionBounds.Left,
                candidate.Left,
                tolerance,
                new GuideLine(false, candidate.Left, Math.Min(selectionBounds.Top, candidate.Top), Math.Max(selectionBounds.Bottom, candidate.Bottom)),
                verticalGuides);
            TryAppendGuide(
                selectionBounds.Left,
                candidate.MidX,
                tolerance,
                new GuideLine(false, candidate.MidX, Math.Min(selectionBounds.Top, candidate.Top), Math.Max(selectionBounds.Bottom, candidate.Bottom)),
                verticalGuides);
            TryAppendGuide(
                selectionBounds.Left,
                candidate.Right,
                tolerance,
                new GuideLine(false, candidate.Right, Math.Min(selectionBounds.Top, candidate.Top), Math.Max(selectionBounds.Bottom, candidate.Bottom)),
                verticalGuides);
            TryAppendGuide(
                selectionBounds.MidX,
                candidate.Left,
                tolerance,
                new GuideLine(false, candidate.Left, Math.Min(selectionBounds.Top, candidate.Top), Math.Max(selectionBounds.Bottom, candidate.Bottom)),
                verticalGuides);
            TryAppendGuide(
                selectionBounds.MidX,
                candidate.MidX,
                tolerance,
                new GuideLine(false, candidate.MidX, Math.Min(selectionBounds.Top, candidate.Top), Math.Max(selectionBounds.Bottom, candidate.Bottom)),
                verticalGuides);
            TryAppendGuide(
                selectionBounds.MidX,
                candidate.Right,
                tolerance,
                new GuideLine(false, candidate.Right, Math.Min(selectionBounds.Top, candidate.Top), Math.Max(selectionBounds.Bottom, candidate.Bottom)),
                verticalGuides);
            TryAppendGuide(
                selectionBounds.Right,
                candidate.Left,
                tolerance,
                new GuideLine(false, candidate.Left, Math.Min(selectionBounds.Top, candidate.Top), Math.Max(selectionBounds.Bottom, candidate.Bottom)),
                verticalGuides);
            TryAppendGuide(
                selectionBounds.Right,
                candidate.MidX,
                tolerance,
                new GuideLine(false, candidate.MidX, Math.Min(selectionBounds.Top, candidate.Top), Math.Max(selectionBounds.Bottom, candidate.Bottom)),
                verticalGuides);
            TryAppendGuide(
                selectionBounds.Right,
                candidate.Right,
                tolerance,
                new GuideLine(false, candidate.Right, Math.Min(selectionBounds.Top, candidate.Top), Math.Max(selectionBounds.Bottom, candidate.Bottom)),
                verticalGuides);

            TryAppendGuide(
                selectionBounds.Top,
                candidate.Top,
                tolerance,
                new GuideLine(true, candidate.Top, Math.Min(selectionBounds.Left, candidate.Left), Math.Max(selectionBounds.Right, candidate.Right)),
                horizontalGuides);
            TryAppendGuide(
                selectionBounds.Top,
                candidate.MidY,
                tolerance,
                new GuideLine(true, candidate.MidY, Math.Min(selectionBounds.Left, candidate.Left), Math.Max(selectionBounds.Right, candidate.Right)),
                horizontalGuides);
            TryAppendGuide(
                selectionBounds.Top,
                candidate.Bottom,
                tolerance,
                new GuideLine(true, candidate.Bottom, Math.Min(selectionBounds.Left, candidate.Left), Math.Max(selectionBounds.Right, candidate.Right)),
                horizontalGuides);
            TryAppendGuide(
                selectionBounds.MidY,
                candidate.Top,
                tolerance,
                new GuideLine(true, candidate.Top, Math.Min(selectionBounds.Left, candidate.Left), Math.Max(selectionBounds.Right, candidate.Right)),
                horizontalGuides);
            TryAppendGuide(
                selectionBounds.MidY,
                candidate.MidY,
                tolerance,
                new GuideLine(true, candidate.MidY, Math.Min(selectionBounds.Left, candidate.Left), Math.Max(selectionBounds.Right, candidate.Right)),
                horizontalGuides);
            TryAppendGuide(
                selectionBounds.MidY,
                candidate.Bottom,
                tolerance,
                new GuideLine(true, candidate.Bottom, Math.Min(selectionBounds.Left, candidate.Left), Math.Max(selectionBounds.Right, candidate.Right)),
                horizontalGuides);
            TryAppendGuide(
                selectionBounds.Bottom,
                candidate.Top,
                tolerance,
                new GuideLine(true, candidate.Top, Math.Min(selectionBounds.Left, candidate.Left), Math.Max(selectionBounds.Right, candidate.Right)),
                horizontalGuides);
            TryAppendGuide(
                selectionBounds.Bottom,
                candidate.MidY,
                tolerance,
                new GuideLine(true, candidate.MidY, Math.Min(selectionBounds.Left, candidate.Left), Math.Max(selectionBounds.Right, candidate.Right)),
                horizontalGuides);
            TryAppendGuide(
                selectionBounds.Bottom,
                candidate.Bottom,
                tolerance,
                new GuideLine(true, candidate.Bottom, Math.Min(selectionBounds.Left, candidate.Left), Math.Max(selectionBounds.Right, candidate.Right)),
                horizontalGuides);
        }

        using var paint = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Stroke,
            Color = new SK.SKColor(13, 153, 255),
            StrokeWidth = 1.25f / scale
        };

        foreach (var guide in verticalGuides.OrderBy(guide => guide.Length).Take(3))
        {
            canvas.DrawLine(guide.Position, guide.Start - (AnnotationGapPixels / scale), guide.Position, guide.End + (AnnotationGapPixels / scale), paint);
        }

        foreach (var guide in horizontalGuides.OrderBy(guide => guide.Length).Take(3))
        {
            canvas.DrawLine(guide.Start - (AnnotationGapPixels / scale), guide.Position, guide.End + (AnnotationGapPixels / scale), guide.Position, paint);
        }
    }

    private static void DrawSpacingAnnotations(SK.SKCanvas canvas, SK.SKRect selectionBounds, IReadOnlyList<SK.SKRect> candidates, float scale)
    {
        var measurements = new List<MeasurementAnnotation>();

        var left = FindNearestHorizontalMeasurement(selectionBounds, candidates, searchLeft: true, scale);
        if (left is not null)
        {
            measurements.Add(left);
        }

        var right = FindNearestHorizontalMeasurement(selectionBounds, candidates, searchLeft: false, scale);
        if (right is not null)
        {
            measurements.Add(right);
        }

        var top = FindNearestVerticalMeasurement(selectionBounds, candidates, searchTop: true, scale);
        if (top is not null)
        {
            measurements.Add(top);
        }

        var bottom = FindNearestVerticalMeasurement(selectionBounds, candidates, searchTop: false, scale);
        if (bottom is not null)
        {
            measurements.Add(bottom);
        }

        using var line = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Stroke,
            Color = new SK.SKColor(255, 92, 122),
            StrokeWidth = 1.25f / scale
        };
        using var labelFill = new SK.SKPaint
        {
            IsAntialias = true,
            Style = SK.SKPaintStyle.Fill,
            Color = new SK.SKColor(255, 92, 122)
        };
        using var labelText = CreateTextPaint(SK.SKColors.White);
        using var labelFont = CreateFont(11f / scale, SK.SKFontStyleWeight.SemiBold);

        foreach (var measurement in measurements)
        {
            if (measurement.IsHorizontal)
            {
                canvas.DrawLine(measurement.Start, measurement.Cross, measurement.End, measurement.Cross, line);
                canvas.DrawLine(measurement.Start, measurement.Cross - (AnnotationTickPixels / scale), measurement.Start, measurement.Cross + (AnnotationTickPixels / scale), line);
                canvas.DrawLine(measurement.End, measurement.Cross - (AnnotationTickPixels / scale), measurement.End, measurement.Cross + (AnnotationTickPixels / scale), line);
                DrawMeasurementLabel(canvas, measurement.Label, labelFill, labelText, labelFont, (measurement.Start + measurement.End) / 2f, measurement.Cross - (16f / scale), scale, horizontal: true);
            }
            else
            {
                canvas.DrawLine(measurement.Cross, measurement.Start, measurement.Cross, measurement.End, line);
                canvas.DrawLine(measurement.Cross - (AnnotationTickPixels / scale), measurement.Start, measurement.Cross + (AnnotationTickPixels / scale), measurement.Start, line);
                canvas.DrawLine(measurement.Cross - (AnnotationTickPixels / scale), measurement.End, measurement.Cross + (AnnotationTickPixels / scale), measurement.End, line);
                DrawMeasurementLabel(canvas, measurement.Label, labelFill, labelText, labelFont, measurement.Cross + (16f / scale), (measurement.Start + measurement.End) / 2f, scale, horizontal: false);
            }
        }
    }

    private static MeasurementAnnotation? FindNearestHorizontalMeasurement(SK.SKRect selectionBounds, IReadOnlyList<SK.SKRect> candidates, bool searchLeft, float scale)
    {
        var bestGap = float.MaxValue;
        MeasurementAnnotation? measurement = null;

        foreach (var candidate in candidates)
        {
            var overlap = GetOverlap(selectionBounds.Top, selectionBounds.Bottom, candidate.Top, candidate.Bottom);
            if (overlap <= 0f)
            {
                continue;
            }

            var gap = searchLeft ? selectionBounds.Left - candidate.Right : candidate.Left - selectionBounds.Right;
            if (gap <= (2f / scale) || gap >= bestGap)
            {
                continue;
            }

            bestGap = gap;
            measurement = new MeasurementAnnotation(
                true,
                searchLeft ? candidate.Right : selectionBounds.Right,
                searchLeft ? selectionBounds.Left : candidate.Left,
                Math.Max(selectionBounds.Top, candidate.Top) + (overlap / 2f),
                MathF.Round(gap).ToString("0", CultureInfo.InvariantCulture));
        }

        return measurement;
    }

    private static MeasurementAnnotation? FindNearestVerticalMeasurement(SK.SKRect selectionBounds, IReadOnlyList<SK.SKRect> candidates, bool searchTop, float scale)
    {
        var bestGap = float.MaxValue;
        MeasurementAnnotation? measurement = null;

        foreach (var candidate in candidates)
        {
            var overlap = GetOverlap(selectionBounds.Left, selectionBounds.Right, candidate.Left, candidate.Right);
            if (overlap <= 0f)
            {
                continue;
            }

            var gap = searchTop ? selectionBounds.Top - candidate.Bottom : candidate.Top - selectionBounds.Bottom;
            if (gap <= (2f / scale) || gap >= bestGap)
            {
                continue;
            }

            bestGap = gap;
            measurement = new MeasurementAnnotation(
                false,
                searchTop ? candidate.Bottom : selectionBounds.Bottom,
                searchTop ? selectionBounds.Top : candidate.Top,
                Math.Max(selectionBounds.Left, candidate.Left) + (overlap / 2f),
                MathF.Round(gap).ToString("0", CultureInfo.InvariantCulture));
        }

        return measurement;
    }

    private static void DrawMeasurementLabel(SK.SKCanvas canvas, string label, SK.SKPaint fill, SK.SKPaint text, SK.SKFont font, float x, float y, float scale, bool horizontal)
    {
        var width = Math.Max(18f / scale, font.MeasureText(label, text) + (10f / scale));
        var height = 18f / scale;
        SK.SKRoundRect badge;

        if (horizontal)
        {
            badge = new SK.SKRoundRect(new SK.SKRect(x - (width / 2f), y - height, x + (width / 2f), y), 5f / scale, 5f / scale);
            canvas.DrawRoundRect(badge, fill);
            canvas.DrawText(label, badge.Rect.Left + (5f / scale), badge.Rect.MidY + (3.5f / scale), font, text);
            return;
        }

        badge = new SK.SKRoundRect(new SK.SKRect(x, y - (width / 2f), x + height, y + (width / 2f)), 5f / scale, 5f / scale);
        canvas.DrawRoundRect(badge, fill);
        canvas.Save();
        canvas.Translate(badge.Rect.MidX + (3f / scale), badge.Rect.Bottom - (5f / scale));
        canvas.RotateDegrees(-90f);
        canvas.DrawText(label, 0f, 0f, font, text);
        canvas.Restore();
    }

    private static void TryAppendGuide(float valueA, float valueB, float tolerance, GuideLine candidate, List<GuideLine> guides)
    {
        if (MathF.Abs(valueA - valueB) > tolerance)
        {
            return;
        }

        if (guides.Any(existing => MathF.Abs(existing.Position - candidate.Position) <= tolerance
                                   && MathF.Abs(existing.Start - candidate.Start) <= tolerance
                                   && MathF.Abs(existing.End - candidate.End) <= tolerance))
        {
            return;
        }

        guides.Add(candidate);
    }

    private static float GetOverlap(float startA, float endA, float startB, float endB)
    {
        return MathF.Min(endA, endB) - MathF.Max(startA, startB);
    }

    private static SK.SKRect Union(SK.SKRect left, SK.SKRect right)
    {
        return new SK.SKRect(
            MathF.Min(left.Left, right.Left),
            MathF.Min(left.Top, right.Top),
            MathF.Max(left.Right, right.Right),
            MathF.Max(left.Bottom, right.Bottom));
    }

    private static SK.SKPaint CreateTextPaint(SK.SKColor color)
    {
        return new SK.SKPaint
        {
            IsAntialias = true,
            Color = color
        };
    }

    private static SK.SKFont CreateFont(float size, SK.SKFontStyleWeight weight)
    {
        return new SK.SKFont(SK.SKTypeface.FromFamilyName("SF Pro Text", weight, SK.SKFontStyleWidth.Normal, SK.SKFontStyleSlant.Upright), size, 1f, 0f);
    }

    private static float GetCanvasScale(Matrix3x2 matrix)
    {
        return MathF.Max(0.0001f, MathF.Sqrt((matrix.M11 * matrix.M11) + (matrix.M12 * matrix.M12)));
    }

    private static SK.SKMatrix ToSkMatrix(Matrix3x2 matrix)
    {
        return new SK.SKMatrix
        {
            ScaleX = matrix.M11,
            SkewX = matrix.M21,
            TransX = matrix.M31,
            SkewY = matrix.M12,
            ScaleY = matrix.M22,
            TransY = matrix.M32,
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1
        };
    }

    private sealed record GuideLine(bool IsHorizontal, float Position, float Start, float End)
    {
        public float Length => End - Start;
    }

    private sealed record MeasurementAnnotation(bool IsHorizontal, float Start, float End, float Cross, string Label);
}
