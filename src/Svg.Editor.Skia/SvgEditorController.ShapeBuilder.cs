using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>One connected, nonoverlapping filled region in a provisional shape arrangement.</summary>
public sealed class SvgShapeBuilderRegion
{
    internal SvgShapeBuilderRegion(int sourceIndex, SKPath geometry) { SourceIndex = sourceIndex; Geometry = geometry; }
    public int SourceIndex { get; }
    /// <summary>Picture-space geometry, owned by its draft.</summary>
    public SKPath Geometry { get; }
}

/// <summary>Owns the native arrangement until the gesture is committed or cancelled.</summary>
public sealed class SvgShapeBuilderDraft : IDisposable
{
    internal SvgShapeBuilderDraft(SvgEditorController owner, string sourceXml, SvgEditorSelectionContext selection,
        SvgVisualElement[] originals, SKMatrix[] inverses, IReadOnlyList<SKPath> sourcePaths, IReadOnlyList<SvgShapeBuilderRegion> regions)
    { Owner = owner; SourceXml = sourceXml; Selection = selection; Originals = originals; Inverses = inverses; SourcePaths = sourcePaths; Regions = regions; }
    internal SvgEditorController Owner { get; }
    internal string SourceXml { get; }
    internal SvgEditorSelectionContext Selection { get; }
    internal SvgVisualElement[] Originals { get; }
    internal SKMatrix[] Inverses { get; }
    internal IReadOnlyList<SKPath> SourcePaths { get; }
    internal bool Applied { get; set; }
    internal bool IsDisposed { get; private set; }
    public IReadOnlyList<SvgShapeBuilderRegion> Regions { get; }

    public int HitTest(SKPoint point)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(SvgShapeBuilderDraft));
        for (var index = Regions.Count - 1; index >= 0; index--)
            if (Regions[index].Geometry.Contains(point.X, point.Y))
                return index;
        return -1;
    }

    /// <summary>Includes cells crossed between pointer samples, even when the cell is narrow.</summary>
    public IReadOnlyList<int> HitTestSegment(SKPoint start, SKPoint end, float tolerance)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(SvgShapeBuilderDraft));
        using var sweep = SvgPathCuttingService.CreateSweep(new[] { start, end }, tolerance * 2);
        var result = new List<int>();
        for (var index = 0; index < Regions.Count; index++)
        {
            var path = Regions[index].Geometry;
            if (!Overlaps(path.Bounds, sweep.Bounds))
                continue;
            using var hit = path.Op(sweep, SKPathOp.Intersect);
            if (hit is not null && !hit.IsEmpty && !hit.Bounds.IsEmpty)
                result.Add(index);
        }
        return result;
    }

    public void Dispose()
    {
        if (IsDisposed)
            return;
        IsDisposed = true;
        foreach (var source in SourcePaths)
            source.Dispose();
        foreach (var region in Regions)
            region.Geometry.Dispose();
    }

    private static bool Overlaps(SKRect a, SKRect b) => a.Left <= b.Right && a.Right >= b.Left && a.Top <= b.Bottom && a.Bottom >= b.Top;
}

public sealed partial class SvgEditorController
{
    /// <summary>
    /// Prepares connected cells from selected paths with the existing path-division
    /// engine. All preparation is read-only; the document retains its original shapes.
    /// </summary>
    public SvgShapeBuilderDraft BeginShapeBuilder(int maximumRegions = 4096)
    {
        ThrowIfDisposed();
        EnsureSelectionEditable();
        if (IsEditing)
            throw new InvalidOperationException("Complete the current edit before building shapes.");
        if (_selection.Count == 0)
            throw new InvalidOperationException("Select overlapping paths or shapes before using Shape Builder.");
        if (maximumRegions < 1 || maximumRegions > 4096)
            throw new ArgumentOutOfRangeException(nameof(maximumRegions));
        var originals = GetCuttingTargets();
        if (originals.Length == 0)
            throw new InvalidOperationException("Select paths or basic shapes before using Shape Builder.");
        if (originals.Length > SvgPathDivisionService.MaximumOperands)
            throw new InvalidOperationException("Use Shape Builder with at most 128 objects at a time.");
        var paths = new List<SKPath>();
        var regions = new List<SvgShapeBuilderRegion>();
        var inverses = new SKMatrix[originals.Length];
        var transferred = false;
        try
        {
            for (var index = 0; index < originals.Length; index++)
            {
                var source = originals[index];
                ValidateDivisionOperand(source);
                var scene = GetSceneNode(source) ?? throw new InvalidOperationException("The shape has no rendered geometry.");
                var world = ToMatrix(scene.TotalTransform);
                if (!world.TryInvert(out inverses[index]))
                    throw new InvalidOperationException("A shape inside a singular transform cannot be combined.");
                var geometry = CreateGeometryPath(source) ?? throw new InvalidOperationException("Convert the selected artwork to paths first.");
                geometry.Transform(world);
                paths.Add(geometry);
            }
            using var divided = SvgPathDivisionService.Divide(paths, maximumRegions);
            foreach (var piece in divided.Pieces)
            {
                var connected = SplitShapeBuilderComponents(piece.Geometry);
                try
                {
                    if (regions.Count + connected.Count > maximumRegions)
                        throw new InvalidOperationException("The shape arrangement has too many regions. Select fewer objects.");
                    foreach (var component in connected)
                        regions.Add(new SvgShapeBuilderRegion(piece.SourceIndex, component));
                    connected.Clear();
                }
                finally { foreach (var component in connected) component.Dispose(); }
            }
            var draft = new SvgShapeBuilderDraft(this, GetSvg(), new SvgEditorSelectionContext(this), originals, inverses, paths.AsReadOnly(), regions.AsReadOnly());
            transferred = true;
            return draft;
        }
        finally
        {
            if (!transferred)
            {
                foreach (var path in paths)
                    path.Dispose();
                foreach (var region in regions)
                    region.Geometry.Dispose();
            }
        }
    }

    /// <summary>Joins or removes the touched cells in one undoable SVG edit.</summary>
    public bool ApplyShapeBuilder(SvgShapeBuilderDraft prepared, IReadOnlyList<int> regionIndices, bool subtract = false)
    {
        ThrowIfDisposed();
        if (prepared is null)
            throw new ArgumentNullException(nameof(prepared));
        if (regionIndices is null)
            throw new ArgumentNullException(nameof(regionIndices));
        if (prepared.IsDisposed || prepared.Applied || !ReferenceEquals(prepared.Owner, this) || !prepared.Selection.IsCurrent(this)
            || !string.Equals(prepared.SourceXml, GetSvg(), StringComparison.Ordinal))
            throw new InvalidOperationException("The shape arrangement is stale. Start the gesture again.");
        EnsureSelectionEditable();
        var selected = new HashSet<int>(regionIndices);
        if (selected.Count == 0)
            return false;
        if (selected.Any(index => index < 0 || index >= prepared.Regions.Count))
            throw new ArgumentOutOfRangeException(nameof(regionIndices));
        var firstRegion = prepared.Regions[regionIndices[0]];
        SKPath? merged = null;
        try
        {
            foreach (var index in regionIndices.Distinct())
            {
                var region = prepared.Regions[index];
                if (merged is null)
                    merged = new SKPath(region.Geometry);
                else
                {
                    var combined = merged.Op(region.Geometry, SKPathOp.Union) ?? throw new InvalidOperationException("The selected regions could not be combined.");
                    merged.Dispose();
                    merged = combined;
                }
            }
            var affected = new HashSet<int>();
            for (var index = 0; index < prepared.SourcePaths.Count; index++)
            {
                using var overlap = prepared.SourcePaths[index].Op(merged!, SKPathOp.Intersect)
                    ?? throw new InvalidOperationException("The affected shapes could not be resolved.");
                if (!overlap.IsEmpty && !overlap.Bounds.IsEmpty)
                    affected.Add(index);
            }
            var replacements = affected.ToDictionary(index => index, _ => new List<SvgPath>());
            for (var index = 0; index < prepared.Regions.Count; index++)
            {
                var region = prepared.Regions[index];
                if (selected.Contains(index) || !affected.Contains(region.SourceIndex))
                    continue;
                using var local = region.Geometry.ToWinding() ?? throw new InvalidOperationException("A shape-builder region could not be normalized.");
                local.Transform(prepared.Inverses[region.SourceIndex]);
                var paths = replacements[region.SourceIndex];
                paths.Add(CloneCutPath(prepared.Originals[region.SourceIndex], local, paths.Count == 0));
            }
            if (!subtract)
            {
                using var local = merged!.ToWinding() ?? throw new InvalidOperationException("The combined region could not be normalized.");
                local.Transform(prepared.Inverses[firstRegion.SourceIndex]);
                var paths = replacements[firstRegion.SourceIndex];
                paths.Add(CloneCutPath(prepared.Originals[firstRegion.SourceIndex], local, paths.Count == 0));
            }
            Execute(subtract ? "Remove shape regions" : "Build shape regions", () =>
            {
                _selection.Clear();
                for (var index = 0; index < prepared.Originals.Length; index++)
                {
                    var source = prepared.Originals[index];
                    if (!replacements.TryGetValue(index, out var paths))
                    { _selection.Add(source); continue; }
                    var parent = source.Parent!;
                    var insertion = parent.Children.IndexOf(source);
                    parent.Children.Remove(source);
                    foreach (var path in paths)
                    {
                        parent.Children.Insert(insertion++, path);
                        RebindPaintServers(path);
                        _selection.Add(path);
                    }
                }
            });
            prepared.Applied = true;
            return true;
        }
        finally { merged?.Dispose(); }
    }

    private static List<SKPath> SplitShapeBuilderComponents(SKPath source)
    {
        var contours = new List<SKPath>();
        var probes = new List<SKPoint>();
        var result = new List<SKPath>();
        var transferred = false;
        try
        {
            using var measure = new SKPathMeasure(source, false, 4);
            do
            {
                if (measure.Length <= 0.000001f)
                    continue;
                if (contours.Count >= 4096)
                    throw new InvalidOperationException("A shape-builder region contains too many contours.");
                using var builder = new SKPathBuilder();
                if (!measure.GetSegment(0, measure.Length, builder, true))
                    continue;
                builder.Close();
                var contour = builder.Detach();
                if (contour.Bounds.IsEmpty)
                { contour.Dispose(); continue; }
                measure.GetPositionAndTangent(measure.Length * 0.371f, out var probe, out _);
                contours.Add(contour);
                probes.Add(probe);
            } while (measure.NextContour());
            var parents = Enumerable.Repeat(-1, contours.Count).ToArray();
            for (var index = 0; index < contours.Count; index++)
            {
                var area = double.MaxValue;
                for (var parent = 0; parent < contours.Count; parent++)
                {
                    if (parent == index || !contours[parent].Contains(probes[index].X, probes[index].Y))
                        continue;
                    var bounds = contours[parent].Bounds;
                    var size = (double)bounds.Width * bounds.Height;
                    if (size < area)
                    { parents[index] = parent; area = size; }
                }
            }
            var depths = new int[contours.Count];
            for (var index = 0; index < contours.Count; index++)
                for (var parent = parents[index]; parent >= 0; parent = parents[parent])
                    if (++depths[index] > contours.Count)
                        throw new InvalidOperationException("The shape contours could not be resolved.");
            for (var index = 0; index < contours.Count; index++)
            {
                if (depths[index] % 2 != 0)
                    continue;
                using var builder = new SKPathBuilder();
                builder.AddPath(contours[index]);
                for (var hole = 0; hole < contours.Count; hole++)
                    if (parents[hole] == index && depths[hole] % 2 != 0)
                        builder.AddPath(contours[hole]);
                var component = builder.Detach();
                component.FillType = SKPathFillType.EvenOdd;
                result.Add(component);
            }
            transferred = true;
            return result;
        }
        finally
        {
            foreach (var contour in contours)
                contour.Dispose();
            if (!transferred)
            foreach (var component in result)
                component.Dispose();
        }
    }
}
