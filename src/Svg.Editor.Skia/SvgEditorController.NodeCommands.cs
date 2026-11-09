using System;
using SkiaSharp;
using Shim = ShimSkiaSharp;

namespace Svg.Editor.Skia;

public sealed partial class SvgEditorController
{
    /// <summary>
    /// Moves one path point (including a direction handle) or polygon/polyline
    /// vertex by a picture-space delta. Other points and element transforms are retained.
    /// </summary>
    public bool NudgeNode(SvgVisualElement element, int pointIndex, SKPoint pictureDelta)
    {
        ThrowIfDisposed();
        if (element is null)
            throw new ArgumentNullException(nameof(element));
        if (!IsFinite(pictureDelta.X) || !IsFinite(pictureDelta.Y))
            throw new ArgumentOutOfRangeException(nameof(pictureDelta));
        if (!BelongsToDocument(element))
            throw new ArgumentException("The node must belong to this document.", nameof(element));
        EnsureElementEditable(element);
        if (pointIndex < 0 || pictureDelta == SKPoint.Empty || element is not (SvgPath or SvgPolygon))
            return false;
        if (GetSceneNode(element) is not { } scene)
            return false;
        if (!ToMatrix(scene.TotalTransform).TryInvert(out var inverse))
            throw new InvalidOperationException("A node inside a singular transform cannot be moved.");

        SKPoint Offset(SKPoint point)
        {
            // Transform the direction independently of translation, preserving small
            // keyboard increments even when an ancestor has a large translation.
            var next = new SKPoint(point.X + inverse.ScaleX * pictureDelta.X + inverse.SkewX * pictureDelta.Y,
                point.Y + inverse.SkewY * pictureDelta.X + inverse.ScaleY * pictureDelta.Y);
            if (!IsFinite(next.X) || !IsFinite(next.Y))
                throw new InvalidOperationException("The node's transformed coordinates must remain finite.");
            return next;
        }

        if (element is SvgPolygon polygon)
        {
            var vertices = SvgEditorVertexService.GetVertices(polygon);
            if (pointIndex >= vertices.Count || (polygon.Points.Count & 1) != 0)
                return false;
            var next = Offset(vertices[pointIndex]);
            if (next == vertices[pointIndex])
                return false;
            Execute("Nudge vertex", () => SvgEditorVertexService.MoveVertex(polygon, pointIndex, next));
            return true;
        }

        var path = (SvgPath)element;
        var inspection = new PathService();
        SKPoint target;
        try
        {
            // PathService normalizes relative segments. Inspect a clone so a stale
            // index or ineffective delta cannot modify the source outside history.
            inspection.Start((SvgPath)path.DeepCopy(), scene);
            if (pointIndex >= inspection.PathPoints.Count)
                return false;
            var current = inspection.PathPoints[pointIndex].Point;
            target = Offset(new SKPoint(current.X, current.Y));
            if (target.X == current.X && target.Y == current.Y)
                return false;
        }
        finally { inspection.Stop(); }

        Execute("Nudge path point", () =>
        {
            try
            {
                Paths.Start(path, scene);
                Paths.ActivePoint = pointIndex;
                Paths.MoveActivePoint(new Shim.SKPoint(target.X, target.Y));
            }
            finally { Paths.Stop(); }
        });
        return true;
    }
}
