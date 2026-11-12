using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using Svg.Pathing;

namespace Svg.Editor.Skia;

public enum SvgPathRefinementKind { Smooth, Erase, Join }

/// <summary>Detached local path edits; cancelling or disposing never changes the document.</summary>
public sealed class SvgPathRefinementPreview : IDisposable
{
    internal SvgPathRefinementPreview(SvgEditorController owner, string sourceXml, SvgEditorSelectionContext selection,
        SvgPathRefinementKind kind, IReadOnlyList<(SvgVisualElement Source, List<SvgPath> Pieces)> changes, IReadOnlyList<SKPath> paths)
    { Owner = owner; SourceXml = sourceXml; Selection = selection; Kind = kind; Changes = changes; Paths = paths; }
    internal SvgEditorController Owner { get; }
    internal string SourceXml { get; }
    internal SvgEditorSelectionContext Selection { get; }
    internal IReadOnlyList<(SvgVisualElement Source, List<SvgPath> Pieces)> Changes { get; }
    internal bool Applied { get; set; }
    internal bool IsDisposed { get; private set; }
    public SvgPathRefinementKind Kind { get; }
    public bool HasChanges => Changes.Count > 0;
    public IReadOnlyList<SKPath> Paths { get; }
    public void Dispose() { if (IsDisposed) return; IsDisposed = true; foreach (var path in Paths) path.Dispose(); }
}

public sealed partial class SvgEditorController
{
    /// <summary>Prepares a local smooth, centerline erase or endpoint join in picture coordinates.</summary>
    public SvgPathRefinementPreview PreparePathRefinement(SvgPathRefinementKind kind, IReadOnlyList<SKPoint> stroke,
        float radius = 12, float strength = 0.5f)
    {
        ThrowIfDisposed();
        EnsureSelectionEditable();
        if (IsEditing)
            throw new InvalidOperationException("Complete the current edit before refining paths.");
        if (!Enum.IsDefined(typeof(SvgPathRefinementKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        var influence = new SvgPathRefinementService.StrokeInfluence(stroke, radius);
        if (!IsFinite(strength) || strength < 0 || strength > 1)
            throw new ArgumentOutOfRangeException(nameof(strength));
        var targets = GetCuttingTargets();
        if (targets.Length > 512)
            throw new InvalidOperationException("Refine at most 512 selected paths at a time.");
        var changes = new List<(SvgVisualElement Source, List<SvgPath> Pieces)>();
        var previews = new List<SKPath>();
        var transferred = false;
        try
        {
            if (kind == SvgPathRefinementKind.Join)
                PreparePathJoin(targets, influence, changes, previews);
            var pointBudget = 32768;
            var sampleBudget = 131072;
            if (kind != SvgPathRefinementKind.Join)
            foreach (var source in targets)
            {
                if (GetSceneNode(source) is not { } scene)
                    continue;
                var world = ToMatrix(scene.TotalTransform);
                if (!world.TryInvert(out var inverse))
                    throw new InvalidOperationException("A path inside a singular transform cannot be refined.");
                using var geometry = scene.HitTestPath is { } retained ? Renderer.SkiaModel.ToSKPath(retained) : CreateValidatedRefinementGeometry(source);
                if (geometry is null)
                    continue;
                geometry.Transform(world);
                if (!influence.Overlaps(geometry.Bounds))
                    continue;
                if ((pointBudget -= geometry.PointCount) < 0)
                    throw new InvalidOperationException("Refine a selection with fewer than 32,768 path points.");
                IReadOnlyList<SKPath>? pieces;
                if (kind == SvgPathRefinementKind.Smooth)
                {
                    var smooth = SvgPathRefinementService.Smooth(geometry, influence, strength);
                    pieces = smooth is null ? null : new[] { smooth };
                }
                else
                    pieces = SvgPathRefinementService.Erase(geometry, influence, ref sampleBudget);
                if (pieces is null)
                    continue;
                try
                {
                    ValidateRefinementTarget(source, false);
                    var replacements = new List<SvgPath>();
                    foreach (var piece in pieces)
                    {
                        previews.Add(new SKPath(piece));
                        piece.Transform(inverse);
                        replacements.Add(CloneCutPath(source, piece, replacements.Count == 0));
                    }
                    changes.Add((source, replacements));
                }
                finally { foreach (var piece in pieces) piece.Dispose(); }
            }
            var result = new SvgPathRefinementPreview(this, GetSvg(), new SvgEditorSelectionContext(this), kind, changes.AsReadOnly(), previews.AsReadOnly());
            transferred = true;
            return result;
        }
        finally { if (!transferred) foreach (var path in previews) path.Dispose(); }
    }

    /// <summary>Commits a complete path-tool gesture as one guarded history entry.</summary>
    public bool ApplyPathRefinement(SvgPathRefinementPreview prepared)
    {
        ThrowIfDisposed();
        if (prepared is null)
            throw new ArgumentNullException(nameof(prepared));
        if (prepared.IsDisposed || prepared.Applied || !ReferenceEquals(prepared.Owner, this) || !prepared.Selection.IsCurrent(this)
            || !string.Equals(prepared.SourceXml, GetSvg(), StringComparison.Ordinal))
            throw new InvalidOperationException("The path preview is stale. Start the gesture again.");
        EnsureSelectionEditable();
        if (!prepared.HasChanges)
            return false;
        CommitCuttingResults(prepared.Kind == SvgPathRefinementKind.Smooth ? "Smooth paths"
            : prepared.Kind == SvgPathRefinementKind.Erase ? "Erase path segments" : "Join path endpoints", prepared.Changes);
        prepared.Applied = true;
        return true;
    }

    private void PreparePathJoin(SvgVisualElement[] targets, SvgPathRefinementService.StrokeInfluence influence,
        List<(SvgVisualElement Source, List<SvgPath> Pieces)> changes, List<SKPath> previews)
    {
        var sources = new List<(SvgVisualElement Source, SKMatrix Inverse, List<SvgPathRefinementService.Contour> Contours)>();
        var endpoints = new List<(int Source, int Contour, bool Start, SKPoint Point)>();
        foreach (var source in targets)
        {
            if (GetSceneNode(source) is not { } scene)
                continue;
            var matrix = ToMatrix(scene.TotalTransform);
            if (!matrix.TryInvert(out var inverse))
                continue;
            using var path = scene.HitTestPath is { } retained ? Renderer.SkiaModel.ToSKPath(retained) : CreateValidatedRefinementGeometry(source);
            if (path is null)
                continue;
            path.Transform(matrix);
            var contours = SvgPathRefinementService.ReadContours(path);
            var sourceIndex = sources.Count;
            sources.Add((source, inverse, contours));
            for (var index = 0; index < contours.Count; index++)
            {
                var contour = contours[index];
                if (contour.Closed || contour.Segments.Count == 0)
                    continue;
                if (influence.Contains(contour.Start))
                    endpoints.Add((sourceIndex, index, true, contour.Start));
                if (influence.Contains(contour.End))
                    endpoints.Add((sourceIndex, index, false, contour.End));
            }
        }
        if (endpoints.Count < 2)
            return;
        if (endpoints.Count > 256)
            throw new InvalidOperationException("Use a smaller Join brush to target fewer than 256 endpoints.");
        var firstIndex = -1;
        var secondIndex = -1;
        var distance = float.PositiveInfinity;
        for (var first = 0; first < endpoints.Count; first++)
        for (var second = first + 1; second < endpoints.Count; second++)
        {
            var candidate = SKPoint.Distance(endpoints[first].Point, endpoints[second].Point);
            if (candidate < distance)
            { distance = candidate; firstIndex = first; secondIndex = second; }
        }
        if (firstIndex < 0)
            return;
        var a = endpoints[firstIndex];
        var b = endpoints[secondIndex];
        var firstSource = sources[a.Source];
        var secondSource = sources[b.Source];
        ValidateRefinementTarget(firstSource.Source, true);
        if (a.Source != b.Source)
            ValidateRefinementTarget(secondSource.Source, true);
        using var combined = new SKPathBuilder();
        for (var index = 0; index < firstSource.Contours.Count; index++)
            if (index != a.Contour && (a.Source != b.Source || index != b.Contour))
                SvgPathRefinementService.AppendContour(combined, firstSource.Contours[index]);
        var firstContour = firstSource.Contours[a.Contour];
        if (a.Source == b.Source && a.Contour == b.Contour)
        {
            SvgPathRefinementService.AppendContour(combined, firstContour);
            combined.Close();
        }
        else
        {
            SvgPathRefinementService.AppendContour(combined, firstContour, a.Start);
            if (distance > 0.00001f)
                combined.LineTo(b.Point);
            SvgPathRefinementService.AppendContour(combined, secondSource.Contours[b.Contour], !b.Start, false);
        }
        using var joined = combined.Detach();
        previews.Add(new SKPath(joined));
        joined.Transform(firstSource.Inverse);
        changes.Add((firstSource.Source, new List<SvgPath> { CloneCutPath(firstSource.Source, joined, true) }));
        if (a.Source != b.Source)
        {
            using var remainingBuilder = new SKPathBuilder();
            for (var index = 0; index < secondSource.Contours.Count; index++)
                if (index != b.Contour)
                    SvgPathRefinementService.AppendContour(remainingBuilder, secondSource.Contours[index]);
            using var remaining = remainingBuilder.Detach();
            var replacements = new List<SvgPath>();
            if (!remaining.IsEmpty)
            { previews.Add(new SKPath(remaining)); remaining.Transform(secondSource.Inverse); replacements.Add(CloneCutPath(secondSource.Source, remaining, true)); }
            changes.Add((secondSource.Source, replacements));
        }
    }

    /// <summary>Paints a filled outline and optionally joins compatible touching fills.</summary>
    public SvgPath AddBlobStroke(IReadOnlyList<SKPoint> points, float diameter, bool merge = true)
    {
        ThrowIfDisposed();
        if (Document is not { } document)
            throw new InvalidOperationException("Open a document before painting.");
        if (IsLocked(document))
            throw new InvalidOperationException("Unlock the document before painting.");
        using var outline = SvgPathRefinementService.CreateBlobOutline(points, diameter);
        if (outline.IsEmpty)
            throw new ArgumentException("Draw a nonempty brush stroke.", nameof(points));
        var paint = ParsePaint(CurrentFill == "none" ? CurrentStroke : CurrentFill);
        if (paint == SvgPaintServer.None)
            throw new InvalidOperationException("Choose a fill color before painting with Blob Brush.");
        var paintKey = PaintKey(paint);
        var targets = new List<SvgVisualElement>();
        SKPath result = new(outline);
        try
        {
            if (merge && paint is SvgColourServer)
            foreach (var source in document.Children.OfType<SvgVisualElement>().ToArray())
            {
                if (!IsSelectableArtwork(source) || IsEditLocked(source) || source.Children.Count != 0 || source.Opacity != 1 || source.FillOpacity != 1
                    || source.Stroke != SvgPaintServer.None || PaintKey(source.Fill) != paintKey
                    || source.Filter is not null || source.ClipPath is not null || GetSceneNode(source) is not { } scene)
                    continue;
                using var shape = scene.HitTestPath is { } retained ? Renderer.SkiaModel.ToSKPath(retained) : CreateValidatedRefinementGeometry(source);
                if (shape is null)
                    continue;
                shape.Transform(ToMatrix(scene.TotalTransform));
                using var intersection = shape.Op(result, SKPathOp.Intersect);
                if (intersection is null || intersection.IsEmpty || intersection.Bounds.IsEmpty)
                    continue;
                try
                { ValidateRefinementTarget(source, true); }
                catch (InvalidOperationException) { continue; }
                var merged = result.Op(shape, SKPathOp.Union) ?? throw new InvalidOperationException("The brush stroke could not be joined to the touching fill.");
                result.Dispose();
                result = merged;
                targets.Add(source);
            }
            if (targets.Count > 0)
            {
                var first = targets[0];
                var world = ToMatrix(GetSceneNode(first)!.TotalTransform);
                if (!world.TryInvert(out var inverse))
                    throw new InvalidOperationException("A touching fill has a singular transform.");
                result.Transform(inverse);
                var replacement = CloneCutPath(first, result, true);
                replacement.Fill = paint;
                replacement.Stroke = SvgPaintServer.None;
                CommitCuttingResults("Paint and merge blob stroke", targets.Select((source, index) => (source, index == 0 ? new List<SvgPath> { replacement } : new List<SvgPath>())));
                return replacement;
            }
            var created = new SvgPath { PathData = SvgPathBuilder.Parse(PathService.ToSvgPathData(result)), Fill = paint, Stroke = SvgPaintServer.None };
            Execute("Paint blob stroke", () => { AddElementInPictureCoordinates(created, "Paint blob stroke"); created.Fill = paint; created.Stroke = SvgPaintServer.None; });
            return created;
        }
        finally { result.Dispose(); }
    }

    private static void ValidateRefinementTarget(SvgVisualElement source, bool combinesObjects)
    {
        if (source.Children.Count != 0)
            throw new InvalidOperationException("Bake animated geometry before refining its path.");
        if (combinesObjects && (source.Filter is not null || source.ClipPath is not null
            || source.TryGetAttribute("mask", out var mask) && !string.IsNullOrWhiteSpace(mask) && mask != "none"))
            throw new InvalidOperationException("Release object clips, masks and filters before joining its geometry with another object.");
    }

    private SKPath? CreateValidatedRefinementGeometry(SvgVisualElement source)
    {
        ValidateCuttingTarget(source);
        return CreateGeometryPath(source);
    }
}
