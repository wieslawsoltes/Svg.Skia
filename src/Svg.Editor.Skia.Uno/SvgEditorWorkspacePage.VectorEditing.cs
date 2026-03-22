using Svg;
using Svg.Editor.Svg;
using Svg.Pathing;
using Svg.Transforms;

namespace Svg.Editor.Skia.Uno;

public partial class SvgEditorWorkspacePage
{
    private const float CubicArcKappa = 0.552284749831f;

    private bool _isPromotingVectorSelection;

    private bool CanEditAsVectorPath(SvgVisualElement? element)
    {
        return element switch
        {
            null => false,
            SvgPath => true,
            SvgRectangle rectangle when !FrameService.IsFrameBackground(rectangle) && !IsSliceElement(rectangle) => true,
            SvgCircle => true,
            SvgEllipse => true,
            SvgPolyline => true,
            SvgPolygon => true,
            SvgLine => true,
            _ => false
        };
    }

    private static bool IsSliceElement(SvgVisualElement element)
    {
        return element.CustomAttributes.TryGetValue(ToolService.SliceFlagAttribute, out var rawValue)
            && bool.TryParse(rawValue, out var isSlice)
            && isSlice;
    }

    private bool TrySelectEditableVectorElement(SvgVisualElement element, out SvgPath editablePath)
    {
        editablePath = null!;
        if (!CanEditAsVectorPath(element))
        {
            return false;
        }

        var originalLabel = GetElementTypeLabel(element);
        if (!TryPromoteElementToEditablePath(element, out editablePath))
        {
            return false;
        }

        _isPromotingVectorSelection = true;
        try
        {
            ApplySelection([editablePath], editablePath);
        }
        finally
        {
            _isPromotingVectorSelection = false;
        }

        StartPathEditing(editablePath);
        CanvasStatus = originalLabel.Equals("Vector", StringComparison.OrdinalIgnoreCase)
            ? "Editing vector path."
            : $"Editing {originalLabel.ToLowerInvariant()} as a vector path.";
        return true;
    }

    private bool TryPromoteElementToEditablePath(SvgVisualElement element, out SvgPath path)
    {
        if (element is SvgPath existingPath)
        {
            path = existingPath;
            return true;
        }

        path = new SvgPath();
        if (!TryConvertElementToPath(element, out var convertedPath))
        {
            return false;
        }

        if (element.Parent is not SvgElement parent)
        {
            return false;
        }

        var index = parent.Children.IndexOf(element);
        if (index < 0)
        {
            return false;
        }

        parent.Children[index] = convertedPath;
        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        path = convertedPath;
        return true;
    }

    private bool TryConvertElementToPath(SvgVisualElement element, out SvgPath path)
    {
        path = new SvgPath();
        if (!TryCreatePathData(element, out var pathData))
        {
            return false;
        }

        path = new SvgPath
        {
            ID = string.IsNullOrWhiteSpace(element.ID) ? CreateUniqueId("path") : element.ID,
            PathData = pathData,
            Fill = element.Fill,
            FillOpacity = element.FillOpacity,
            Stroke = element.Stroke,
            StrokeOpacity = element.StrokeOpacity,
            StrokeWidth = element.StrokeWidth,
            Opacity = element.Opacity,
            FillRule = element.FillRule,
            StrokeLineCap = element.StrokeLineCap,
            StrokeLineJoin = element.StrokeLineJoin,
            StrokeMiterLimit = element.StrokeMiterLimit,
            StrokeDashOffset = element.StrokeDashOffset,
            Display = element.Display,
            Visibility = element.Visibility,
            Filter = element.Filter,
            ClipPath = element.ClipPath,
            ColorInterpolation = element.ColorInterpolation
        };
        path.Transforms = CloneTransforms(element.Transforms);
        path.StrokeDashArray = CloneUnitCollection(element.StrokeDashArray);
        CopyCustomAttributes(element, path);

        if (element is SvgMarkerElement sourceMarker && path is SvgMarkerElement targetMarker)
        {
            targetMarker.MarkerStart = sourceMarker.MarkerStart;
            targetMarker.MarkerMid = sourceMarker.MarkerMid;
            targetMarker.MarkerEnd = sourceMarker.MarkerEnd;
        }

        return true;
    }

    private static void CopyCustomAttributes(SvgElement source, SvgElement target)
    {
        target.CustomAttributes.Clear();
        foreach (var attribute in source.CustomAttributes)
        {
            target.CustomAttributes[attribute.Key] = attribute.Value;
        }
    }

    private static SvgUnitCollection? CloneUnitCollection(SvgUnitCollection? units)
    {
        if (units is null)
        {
            return null;
        }

        var clone = new SvgUnitCollection();
        foreach (var unit in units)
        {
            clone.Add(new SvgUnit(unit.Type, unit.Value));
        }

        return clone;
    }

    private static bool TryCreatePathData(SvgVisualElement element, out SvgPathSegmentList pathData)
    {
        switch (element)
        {
            case SvgPath path:
                pathData = ClonePathSegments(path.PathData);
                return true;
            case SvgRectangle rectangle:
                pathData = CreateRectanglePathSegments(rectangle);
                return true;
            case SvgCircle circle:
                pathData = CreateEllipsePathSegments(circle.CenterX.Value, circle.CenterY.Value, circle.Radius.Value, circle.Radius.Value);
                return true;
            case SvgEllipse ellipse:
                pathData = CreateEllipsePathSegments(ellipse.CenterX.Value, ellipse.CenterY.Value, ellipse.RadiusX.Value, ellipse.RadiusY.Value);
                return true;
            case SvgLine line:
                pathData = CreateLinePathSegments(line);
                return true;
            case SvgPolyline polyline:
                pathData = CreatePointCollectionPathSegments(polyline.Points, closePath: false);
                return true;
            case SvgPolygon polygon:
                pathData = CreatePointCollectionPathSegments(polygon.Points, closePath: true);
                return true;
            default:
                pathData = new SvgPathSegmentList();
                return false;
        }
    }

    private static SvgPathSegmentList ClonePathSegments(SvgPathSegmentList? segments)
    {
        var clone = new SvgPathSegmentList();
        if (segments is null)
        {
            return clone;
        }

        foreach (var segment in segments)
        {
            clone.Add(ClonePathSegment(segment));
        }

        return clone;
    }

    private static SvgPathSegment ClonePathSegment(SvgPathSegment segment)
    {
        return segment switch
        {
            SvgMoveToSegment move => new SvgMoveToSegment(move.IsRelative, move.End),
            SvgLineSegment line => new SvgLineSegment(line.IsRelative, line.End),
            SvgCubicCurveSegment cubic => new SvgCubicCurveSegment(
                cubic.IsRelative,
                cubic.FirstControlPoint,
                cubic.SecondControlPoint,
                cubic.End),
            SvgQuadraticCurveSegment quadratic => new SvgQuadraticCurveSegment(
                quadratic.IsRelative,
                quadratic.ControlPoint,
                quadratic.End),
            SvgArcSegment arc => new SvgArcSegment(
                arc.RadiusX,
                arc.RadiusY,
                arc.Angle,
                arc.Size,
                arc.Sweep,
                arc.IsRelative,
                arc.End),
            SvgClosePathSegment close => new SvgClosePathSegment(close.IsRelative),
            _ => throw new NotSupportedException($"Unsupported path segment type: {segment.GetType().Name}")
        };
    }

    private static SvgPathSegmentList CreateRectanglePathSegments(SvgRectangle rectangle)
    {
        var x = rectangle.X.Value;
        var y = rectangle.Y.Value;
        var width = Math.Max(rectangle.Width.Value, 0f);
        var height = Math.Max(rectangle.Height.Value, 0f);
        var rx = Math.Max(rectangle.CornerRadiusX.Value, 0f);
        var ry = Math.Max(rectangle.CornerRadiusY.Value, 0f);

        if (rx <= 0f && ry <= 0f)
        {
            return new SvgPathSegmentList
            {
                new SvgMoveToSegment(false, new System.Drawing.PointF(x, y)),
                new SvgLineSegment(false, new System.Drawing.PointF(x + width, y)),
                new SvgLineSegment(false, new System.Drawing.PointF(x + width, y + height)),
                new SvgLineSegment(false, new System.Drawing.PointF(x, y + height)),
                new SvgClosePathSegment(false)
            };
        }

        if (rx <= 0f)
        {
            rx = ry;
        }

        if (ry <= 0f)
        {
            ry = rx;
        }

        rx = Math.Min(rx, width / 2f);
        ry = Math.Min(ry, height / 2f);

        var ox = rx * CubicArcKappa;
        var oy = ry * CubicArcKappa;
        var right = x + width;
        var bottom = y + height;

        return new SvgPathSegmentList
        {
            new SvgMoveToSegment(false, new System.Drawing.PointF(x + rx, y)),
            new SvgLineSegment(false, new System.Drawing.PointF(right - rx, y)),
            new SvgCubicCurveSegment(
                false,
                new System.Drawing.PointF(right - rx + ox, y),
                new System.Drawing.PointF(right, y + ry - oy),
                new System.Drawing.PointF(right, y + ry)),
            new SvgLineSegment(false, new System.Drawing.PointF(right, bottom - ry)),
            new SvgCubicCurveSegment(
                false,
                new System.Drawing.PointF(right, bottom - ry + oy),
                new System.Drawing.PointF(right - rx + ox, bottom),
                new System.Drawing.PointF(right - rx, bottom)),
            new SvgLineSegment(false, new System.Drawing.PointF(x + rx, bottom)),
            new SvgCubicCurveSegment(
                false,
                new System.Drawing.PointF(x + rx - ox, bottom),
                new System.Drawing.PointF(x, bottom - ry + oy),
                new System.Drawing.PointF(x, bottom - ry)),
            new SvgLineSegment(false, new System.Drawing.PointF(x, y + ry)),
            new SvgCubicCurveSegment(
                false,
                new System.Drawing.PointF(x, y + ry - oy),
                new System.Drawing.PointF(x + rx - ox, y),
                new System.Drawing.PointF(x + rx, y)),
            new SvgClosePathSegment(false)
        };
    }

    private static SvgPathSegmentList CreateEllipsePathSegments(float centerX, float centerY, float radiusX, float radiusY)
    {
        radiusX = Math.Max(radiusX, 0f);
        radiusY = Math.Max(radiusY, 0f);

        var ox = radiusX * CubicArcKappa;
        var oy = radiusY * CubicArcKappa;

        return new SvgPathSegmentList
        {
            new SvgMoveToSegment(false, new System.Drawing.PointF(centerX + radiusX, centerY)),
            new SvgCubicCurveSegment(
                false,
                new System.Drawing.PointF(centerX + radiusX, centerY + oy),
                new System.Drawing.PointF(centerX + ox, centerY + radiusY),
                new System.Drawing.PointF(centerX, centerY + radiusY)),
            new SvgCubicCurveSegment(
                false,
                new System.Drawing.PointF(centerX - ox, centerY + radiusY),
                new System.Drawing.PointF(centerX - radiusX, centerY + oy),
                new System.Drawing.PointF(centerX - radiusX, centerY)),
            new SvgCubicCurveSegment(
                false,
                new System.Drawing.PointF(centerX - radiusX, centerY - oy),
                new System.Drawing.PointF(centerX - ox, centerY - radiusY),
                new System.Drawing.PointF(centerX, centerY - radiusY)),
            new SvgCubicCurveSegment(
                false,
                new System.Drawing.PointF(centerX + ox, centerY - radiusY),
                new System.Drawing.PointF(centerX + radiusX, centerY - oy),
                new System.Drawing.PointF(centerX + radiusX, centerY)),
            new SvgClosePathSegment(false)
        };
    }

    private static SvgPathSegmentList CreateLinePathSegments(SvgLine line)
    {
        return new SvgPathSegmentList
        {
            new SvgMoveToSegment(false, new System.Drawing.PointF(line.StartX.Value, line.StartY.Value)),
            new SvgLineSegment(false, new System.Drawing.PointF(line.EndX.Value, line.EndY.Value))
        };
    }

    private static SvgPathSegmentList CreatePointCollectionPathSegments(SvgPointCollection points, bool closePath)
    {
        var pathData = new SvgPathSegmentList();
        if (points.Count < 2)
        {
            return pathData;
        }

        var firstPoint = new System.Drawing.PointF(points[0].Value, points[1].Value);
        pathData.Add(new SvgMoveToSegment(false, firstPoint));

        for (var index = 2; index + 1 < points.Count; index += 2)
        {
            pathData.Add(new SvgLineSegment(false, new System.Drawing.PointF(points[index].Value, points[index + 1].Value)));
        }

        if (closePath)
        {
            pathData.Add(new SvgClosePathSegment(false));
        }

        return pathData;
    }
}
