using SkiaSharp;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorCanvas
{
    private readonly List<SKPoint> _vertexPoints = new();
    private SvgPolygon? _vertexOriginal;
    private SKMatrix _vertexMatrix = SKMatrix.Identity;
    private SKMatrix _vertexInverse = SKMatrix.Identity;
    private int _vertexIndex = -1;

    private void RefreshVertexPreview()
    {
        var previous = _vertexOriginal;
        var previousIndex = _vertexIndex;
        ClearVertexPreview();
        if (!IsNodeTool(Tool) || !CanEditSelection || Controller?.SelectedElements.Count != 1
            || Controller.SelectedElements[0] is not SvgPolygon polygon
            || Controller.GetSceneNode(polygon) is not { } scene)
        {
            return;
        }

        _vertexMatrix = ToSkia(scene.TotalTransform);
        if (!_vertexMatrix.TryInvert(out _vertexInverse))
        {
            return;
        }

        _vertexOriginal = polygon;
        _vertexPoints.AddRange(SvgEditorVertexService.GetVertices(polygon));
        if (ReferenceEquals(previous, polygon) && previousIndex >= 0 && previousIndex < _vertexPoints.Count)
        {
            _vertexIndex = previousIndex;
        }
    }

    private void ClearVertexPreview()
    {
        _vertexOriginal = null;
        _vertexPoints.Clear();
        _vertexIndex = -1;
    }

    private bool TryBeginVertex(SKPoint view)
    {
        if (_vertexOriginal is null || SvgEditorController.IsEditLocked(_vertexOriginal))
        {
            return false;
        }

        for (var i = 0; i < _vertexPoints.Count; i++)
        {
            if (SKPoint.Distance(Viewport.WorldToView(_vertexMatrix.MapPoint(_vertexPoints[i])), view) <= 7)
            {
                _vertexIndex = i;
                _gesture = Gesture.Vertex;
                Invalidate();
                return true;
            }
        }

        return false;
    }

    private void CommitVertexMove()
    {
        var original = _vertexOriginal;
        var index = _vertexIndex;
        if (Controller is null || original is null || SvgEditorController.IsEditLocked(original)
            || index < 0 || index >= _vertexPoints.Count)
        {
            return;
        }

        var point = _vertexPoints[index];
        Controller.Execute("Move vertex", () => SvgEditorVertexService.MoveVertex(original, index, point));
    }

    private void DeleteActiveVertex()
    {
        var original = _vertexOriginal;
        var index = _vertexIndex;
        if (Controller is null || original is null || SvgEditorController.IsEditLocked(original) || index < 0)
        {
            return;
        }

        Controller.Execute("Delete vertex", () => SvgEditorVertexService.RemoveVertex(original, index));
        _vertexIndex = Math.Min(index, _vertexPoints.Count - 1);
        Invalidate();
    }

    /// <summary>Inserts a vertex on a nearby polygon/polyline edge using picture-space coordinates.</summary>
    public bool InsertVertexAt(SKPoint point)
    {
        var original = _vertexOriginal;
        if (Controller is null || original is null || SvgEditorController.IsEditLocked(original) || _vertexPoints.Count < 2)
        {
            return false;
        }

        var segments = original is SvgPolyline ? _vertexPoints.Count - 1 : _vertexPoints.Count;
        var closest = -1;
        var closestPoint = default(SKPoint);
        var distanceLimit = (float)(12 / Zoom);
        for (var i = 0; i < segments; i++)
        {
            var start = _vertexMatrix.MapPoint(_vertexPoints[i]);
            var end = _vertexMatrix.MapPoint(_vertexPoints[(i + 1) % _vertexPoints.Count]);
            var edge = Subtract(end, start);
            var relative = Subtract(point, start);
            var squaredLength = edge.X * edge.X + edge.Y * edge.Y;
            var t = squaredLength > 0
                ? Math.Clamp((relative.X * edge.X + relative.Y * edge.Y) / squaredLength, 0, 1) : 0;
            // A double click on an existing vertex selects it; it must not insert a duplicate.
            if (t <= 0.001 || t >= 0.999)
            {
                continue;
            }

            var projected = new SKPoint(start.X + edge.X * t, start.Y + edge.Y * t);
            var distance = SKPoint.Distance(point, projected);
            if (distance < distanceLimit)
            {
                distanceLimit = distance;
                closest = i;
                closestPoint = projected;
            }
        }

        if (closest < 0)
        {
            return false;
        }

        var local = _vertexInverse.MapPoint(closestPoint);
        Controller.Execute("Insert vertex", () => SvgEditorVertexService.InsertVertex(original, closest + 1, local));
        _vertexIndex = closest + 1;
        Invalidate();
        return true;
    }

    private void DrawVertices(SKCanvas canvas, CanvasResources resources)
    {
        resources.Stroke.Color = resources.Accent;
        resources.Stroke.StrokeWidth = 1;
        var segments = _vertexOriginal is SvgPolyline ? _vertexPoints.Count - 1 : _vertexPoints.Count;
        for (var i = 0; i < segments; i++)
        {
            var start = Viewport.WorldToView(_vertexMatrix.MapPoint(_vertexPoints[i]));
            var end = Viewport.WorldToView(_vertexMatrix.MapPoint(_vertexPoints[(i + 1) % _vertexPoints.Count]));
            canvas.DrawLine(start, end, resources.Stroke);
        }

        for (var i = 0; i < _vertexPoints.Count; i++)
        {
            var point = Viewport.WorldToView(_vertexMatrix.MapPoint(_vertexPoints[i]));
            resources.Fill.Color = i == _vertexIndex ? resources.Accent : SKColors.White;
            var handle = SKRect.Create(point.X - 3.5f, point.Y - 3.5f, 7, 7);
            canvas.DrawRect(handle, resources.Fill);
            canvas.DrawRect(handle, resources.Stroke);
        }
    }
}
