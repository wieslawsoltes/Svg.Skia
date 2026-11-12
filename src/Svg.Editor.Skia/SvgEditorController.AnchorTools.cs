using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SkiaSharp;
using Svg.Pathing;

namespace Svg.Editor.Skia;

/// <summary>A provisional anchor conversion; its native preview is owned until disposal.</summary>
public sealed class SvgAnchorEditPreview : IDisposable
{
    internal SvgAnchorEditPreview(SvgEditorController owner, SvgVisualElement source, SvgPath replacement,
        string sourceXml, SvgEditorSelectionContext selection, SKPath geometry, SKPoint anchor, SKPoint? handle)
    { Owner = owner; Source = source; Replacement = replacement; SourceXml = sourceXml; Selection = selection; Geometry = geometry; Anchor = anchor; Handle = handle; }
    internal SvgEditorController Owner { get; }
    internal SvgVisualElement Source { get; }
    internal SvgPath Replacement { get; }
    internal string SourceXml { get; }
    internal SvgEditorSelectionContext Selection { get; }
    internal bool Applied { get; set; }
    internal bool IsDisposed { get; private set; }
    public SKPath Geometry { get; }
    public SKPoint Anchor { get; }
    public SKPoint? Handle { get; }
    public void Dispose() { if (!IsDisposed) { IsDisposed = true; Geometry.Dispose(); } }
}

public sealed partial class SvgEditorController
{
    /// <summary>Inserts an anchor by subdividing the hit curve; its geometry remains unchanged.</summary>
    public bool AddAnchorAt(SKPoint picturePoint, float tolerance = 8)
    {
        ThrowIfDisposed();
        EnsureSelectionEditable();
        if (!IsFinite(tolerance) || tolerance <= 0)
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        if (!IsFinite(picturePoint.X) || !IsFinite(picturePoint.Y))
            throw new ArgumentOutOfRangeException(nameof(picturePoint));
        if (FindAnchorTarget(picturePoint, tolerance * 0.05f) is not null)
            return false;
        SvgVisualElement? target = null;
        SvgPathCutPoint? nearest = null;
        SKPath? geometry = null;
        var inverse = SKMatrix.Identity;
        try
        {
            foreach (var candidate in GetCuttingTargets())
            {
                if (GetSceneNode(candidate) is not { } scene || !ToMatrix(scene.TotalTransform).TryInvert(out var local))
                    continue;
                using var path = CreateGeometryPath(candidate);
                if (path is null)
                    continue;
                path.Transform(ToMatrix(scene.TotalTransform));
                var bounds = path.Bounds;
                if (picturePoint.X < bounds.Left - tolerance || picturePoint.X > bounds.Right + tolerance
                    || picturePoint.Y < bounds.Top - tolerance || picturePoint.Y > bounds.Bottom + tolerance)
                    continue;
                var point = SvgPathCuttingService.FindNearest(path, picturePoint, Math.Max(0.00001f, nearest?.Separation ?? tolerance));
                if (point is null)
                    continue;
                target = candidate;
                nearest = point;
                inverse = local;
                geometry?.Dispose();
                geometry = new SKPath(path);
            }
            if (target is null || nearest is null || geometry is null)
                return false;
            ValidateCuttingTarget(target);
            using var inserted = SvgPathCuttingService.InsertAnchor(geometry, nearest.Value);
            inserted.Transform(inverse);
            var replacement = CloneCutPath(target, inserted, true);
            CommitCuttingResults("Add anchor point", new[] { (target, new List<SvgPath> { replacement }) });
            return true;
        }
        finally { geometry?.Dispose(); }
    }

    /// <summary>Deletes a real anchor using the existing path editor's topology rules.</summary>
    public bool DeleteAnchorAt(SKPoint picturePoint, float tolerance = 8)
    {
        ThrowIfDisposed();
        EnsureSelectionEditable();
        var target = FindAnchorTarget(picturePoint, tolerance);
        if (target is null)
            return false;
        var (source, copy, pointIndex, _, _) = target.Value;
        ValidateCuttingTarget(source);
        var editor = new PathService();
        editor.Start(copy, GetSceneNode(source)!);
        editor.ActivePoint = pointIndex;
        editor.RemoveActivePoint();
        editor.Stop();
        var replacements = new List<SvgPath>();
        if (copy.PathData.Any(segment => segment is not (SvgMoveToSegment or SvgClosePathSegment)))
        {
            using var geometry = PathService.ElementToPath(copy)!;
            replacements.Add(CloneCutPath(source, geometry, true));
        }
        CommitCuttingResults("Delete anchor point", new[] { (source, replacements) });
        return true;
    }

    /// <summary>
    /// Prepares a corner click or symmetric-handle drag without changing the source.
    /// Tangents are picture-space vectors and are mapped through the full SVG ancestry.
    /// </summary>
    public SvgAnchorEditPreview? PrepareAnchorConversion(SKPoint picturePoint, SKPoint? tangent = null, float tolerance = 8)
    {
        ThrowIfDisposed();
        EnsureSelectionEditable();
        if (tangent is { } vector && (!IsFinite(vector.X) || !IsFinite(vector.Y)))
            throw new ArgumentOutOfRangeException(nameof(tangent));
        var target = FindAnchorTarget(picturePoint, tolerance);
        if (target is null)
            return null;
        var (source, copy, _, segmentIndex, anchor) = target.Value;
        ValidateCuttingTarget(source);
        var scene = GetSceneNode(source)!;
        var world = ToMatrix(scene.TotalTransform);
        if (!world.TryInvert(out var inverse))
            throw new InvalidOperationException("An anchor inside a singular transform cannot be edited.");
        var before = copy.PathData.ToString();
        var localAnchor = inverse.MapPoint(anchor);
        SKPoint? localTangent = null;
        if (tangent is { } delta)
        {
            var end = inverse.MapPoint(new SKPoint(anchor.X + delta.X, anchor.Y + delta.Y));
            localTangent = new SKPoint(end.X - localAnchor.X, end.Y - localAnchor.Y);
        }
        ConvertAnchorHandles(copy.PathData, segmentIndex, localTangent);
        if (string.Equals(before, copy.PathData.ToString(), StringComparison.Ordinal))
            return null;
        using var local = PathService.ElementToPath(copy)!;
        var replacement = CloneCutPath(source, local, true);
        var geometry = new SKPath(local);
        geometry.Transform(world);
        return new SvgAnchorEditPreview(this, source, replacement, GetSvg(), new SvgEditorSelectionContext(this), geometry,
            anchor, tangent is { } handle ? new SKPoint(anchor.X + handle.X, anchor.Y + handle.Y) : null);
    }

    public void ApplyAnchorConversion(SvgAnchorEditPreview prepared)
    {
        ThrowIfDisposed();
        if (prepared is null)
            throw new ArgumentNullException(nameof(prepared));
        if (prepared.IsDisposed || prepared.Applied || !ReferenceEquals(prepared.Owner, this) || !prepared.Selection.IsCurrent(this)
            || !string.Equals(prepared.SourceXml, GetSvg(), StringComparison.Ordinal))
            throw new InvalidOperationException("The anchor preview is stale. Start the gesture again.");
        EnsureSelectionEditable();
        CommitCuttingResults(prepared.Handle is null ? "Convert anchor to corner" : "Convert anchor to smooth",
            new[] { (prepared.Source, new List<SvgPath> { prepared.Replacement }) });
        prepared.Applied = true;
    }

    private (SvgVisualElement Source, SvgPath Copy, int PointIndex, int SegmentIndex, SKPoint Anchor)? FindAnchorTarget(SKPoint point, float tolerance)
    {
        if (!IsFinite(point.X) || !IsFinite(point.Y))
            throw new ArgumentOutOfRangeException(nameof(point));
        if (!IsFinite(tolerance) || tolerance <= 0)
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        (SvgVisualElement, SvgPath, int, int, SKPoint)? result = null;
        var nearest = tolerance;
        foreach (var source in GetCuttingTargets())
        {
            if (GetSceneNode(source) is not { } scene)
                continue;
            SvgPath copy;
            if (source is SvgPath path)
                copy = (SvgPath)path.DeepCopy();
            else
            {
                using var geometry = CreateGeometryPath(source);
                if (geometry is null)
                    continue;
                copy = new SvgPath { PathData = SvgPathBuilder.Parse(PathService.ToSvgPathData(geometry)) };
            }
            var editor = new PathService();
            editor.Start(copy, scene);
            for (var index = 0; index < editor.PathPoints.Count; index++)
            {
                var anchor = editor.PathPoints[index];
                if (anchor.Type != 0)
                    continue;
                var segmentIndex = copy.PathData.IndexOf(anchor.Segment);
                if (segmentIndex + 1 < copy.PathData.Count && copy.PathData[segmentIndex + 1] is SvgClosePathSegment)
                {
                    var first = segmentIndex;
                    while (first > 0 && copy.PathData[first] is not SvgMoveToSegment)
                        first--;
                    // An explicit closing curve shares the start anchor; expose that
                    // anchor once so conversion always updates both adjacent handles.
                    if (first != segmentIndex && copy.PathData[first].End == anchor.Segment.End)
                        continue;
                }
                var transformed = scene.TotalTransform.MapPoint(anchor.Point);
                var position = new SKPoint(transformed.X, transformed.Y);
                var distance = SKPoint.Distance(point, position);
                if (distance > nearest)
                    continue;
                nearest = distance;
                result = (source, copy, index, segmentIndex, position);
            }
            editor.Stop();
        }
        return result;
    }

    private static void ConvertAnchorHandles(SvgPathSegmentList segments, int anchorIndex, SKPoint? tangent)
    {
        var anchor = segments[anchorIndex].End;
        var startIndex = anchorIndex;
        while (startIndex > 0 && segments[startIndex] is not SvgMoveToSegment)
            startIndex--;
        var endIndex = anchorIndex + 1;
        while (endIndex < segments.Count && segments[endIndex] is not SvgMoveToSegment)
            endIndex++;
        var closed = endIndex > startIndex && segments[endIndex - 1] is SvgClosePathSegment;
        var incoming = anchorIndex == startIndex ? -1 : anchorIndex;
        if (incoming < 0 && closed && endIndex - startIndex > 2)
        {
            var last = endIndex - 2;
            if (segments[last].End == anchor)
                incoming = last;
            else if (tangent is not null)
            {
                segments.Insert(endIndex - 1, new SvgLineSegment(false, anchor));
                incoming = endIndex - 1;
                endIndex++;
            }
        }
        var outgoing = anchorIndex + 1;
        if (outgoing < endIndex && segments[outgoing] is SvgClosePathSegment && tangent is not null)
        {
            segments.Insert(outgoing, new SvgLineSegment(false, segments[startIndex].End));
            endIndex++;
        }
        if (incoming >= 0)
            SetAnchorHandle(segments, incoming, false, tangent is { } incomingTangent
            ? new PointF(anchor.X - incomingTangent.X, anchor.Y - incomingTangent.Y) : anchor, tangent is not null);
        if (outgoing < endIndex && segments[outgoing] is not (SvgMoveToSegment or SvgClosePathSegment))
            SetAnchorHandle(segments, outgoing, true, tangent is { } outgoingTangent
                ? new PointF(anchor.X + outgoingTangent.X, anchor.Y + outgoingTangent.Y) : anchor, tangent is not null);
    }

    private static void SetAnchorHandle(SvgPathSegmentList segments, int index, bool first, PointF handle, bool convertLine)
    {
        if (segments[index] is SvgCubicCurveSegment cubic)
        {
            if (first)
                cubic.FirstControlPoint = handle;
            else
                cubic.SecondControlPoint = handle;
            return;
        }
        if (segments[index] is SvgQuadraticCurveSegment quadratic && !convertLine)
        { quadratic.ControlPoint = handle; return; }
        if (!convertLine)
            return;
        var start = segments[index - 1].End;
        var end = segments[index].End;
        var c1 = new PointF(start.X + (end.X - start.X) / 3, start.Y + (end.Y - start.Y) / 3);
        var c2 = new PointF(start.X + (end.X - start.X) * 2 / 3, start.Y + (end.Y - start.Y) * 2 / 3);
        if (segments[index] is SvgQuadraticCurveSegment curve)
        {
            c1 = new PointF(start.X + (curve.ControlPoint.X - start.X) * 2 / 3, start.Y + (curve.ControlPoint.Y - start.Y) * 2 / 3);
            c2 = new PointF(end.X + (curve.ControlPoint.X - end.X) * 2 / 3, end.Y + (curve.ControlPoint.Y - end.Y) * 2 / 3);
        }
        segments[index] = new SvgCubicCurveSegment(false, first ? handle : c1, first ? c2 : handle, end);
    }
}
