using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Svg.Editor.Skia;

public enum SvgEditorSelectionBehavior { Replace, Add, Subtract, Toggle }

/// <summary>Criteria combined by the magic wand. RGBA tolerance is measured per channel, from 0 to 255.</summary>
public sealed class SvgMagicWandOptions
{
    public bool MatchFill { get; set; } = true;
    public bool MatchStroke { get; set; }
    public bool MatchStrokeWidth { get; set; }
    public bool MatchOpacity { get; set; }
    public float ColorTolerance { get; set; } = 32;
    public float StrokeWidthTolerance { get; set; } = 1;
    public float OpacityTolerance { get; set; }

    internal void Validate()
    {
        if (!MatchFill && !MatchStroke && !MatchStrokeWidth && !MatchOpacity)
            throw new ArgumentException("Choose at least one appearance criterion.");
        if (!Finite(ColorTolerance) || ColorTolerance < 0 || ColorTolerance > 255)
            throw new ArgumentOutOfRangeException(nameof(ColorTolerance));
        if (!Finite(StrokeWidthTolerance) || StrokeWidthTolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(StrokeWidthTolerance));
        if (!Finite(OpacityTolerance) || OpacityTolerance < 0 || OpacityTolerance > 1)
            throw new ArgumentOutOfRangeException(nameof(OpacityTolerance));
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

public sealed partial class SvgEditorController
{
    /// <summary>
    /// Selects artwork matching the topmost unlocked object at a picture-space point.
    /// A background click clears only a replacement selection. No document edit is created.
    /// </summary>
    public bool SelectByAppearanceAt(SKPoint point, SvgMagicWandOptions? options = null,
        SvgEditorSelectionBehavior behavior = SvgEditorSelectionBehavior.Replace)
    {
        ThrowIfDisposed();
        ValidateSelectionBehavior(behavior);
        (options ??= new SvgMagicWandOptions()).Validate();
        if (!IsFinite(point.X) || !IsFinite(point.Y))
            throw new ArgumentOutOfRangeException(nameof(point));
        var source = Renderer.HitTestElements(new ShimSkiaSharp.SKPoint(point.X, point.Y))
            .OfType<SvgVisualElement>().LastOrDefault(IsWandArtwork);
        if (source is null)
        {
            if (behavior == SvgEditorSelectionBehavior.Replace)
                ApplyEditorSelection(Array.Empty<SvgVisualElement>(), behavior);
            return false;
        }
        SelectByAppearance(source, options, behavior);
        return true;
    }

    /// <summary>
    /// Matches effective inherited paints, including currentColor and CSS variables.
    /// Solid colors use channel tolerance; gradient and pattern paints match by resource identity.
    /// Opacity includes ancestor compositing opacity. Selection never enters undo history.
    /// </summary>
    public void SelectByAppearance(SvgVisualElement source, SvgMagicWandOptions? options = null,
        SvgEditorSelectionBehavior behavior = SvgEditorSelectionBehavior.Replace)
    {
        ThrowIfDisposed();
        ValidateSelectionBehavior(behavior);
        (options ??= new SvgMagicWandOptions()).Validate();
        if (source is null || !BelongsToDocument(source))
            throw new ArgumentException("Choose artwork in the current document.", nameof(source));
        if (!IsWandArtwork(source) || Document is null)
            return;
        var referenceFill = EffectiveSelectionPaint(source, source.Fill, false);
        var referenceStroke = EffectiveSelectionPaint(source, source.Stroke, true);
        var referenceWidth = SelectionStrokeWidth(source);
        var referenceOpacity = SelectionOpacity(source);
        var matches = Document.Descendants().OfType<SvgVisualElement>().Where(IsWandArtwork).Where(element =>
            (!options.MatchFill || MatchesSelectionPaint(referenceFill, EffectiveSelectionPaint(element, element.Fill, false), options.ColorTolerance)) &&
            (!options.MatchStroke || MatchesSelectionPaint(referenceStroke, EffectiveSelectionPaint(element, element.Stroke, true), options.ColorTolerance)) &&
            (!options.MatchStrokeWidth || Math.Abs(referenceWidth - SelectionStrokeWidth(element)) <= options.StrokeWidthTolerance) &&
            (!options.MatchOpacity || Math.Abs(referenceOpacity - SelectionOpacity(element)) <= options.OpacityTolerance)).ToArray();
        ApplyEditorSelection(matches, behavior);
    }

    /// <summary>
    /// Selects painted geometry crossing a closed, even-odd lasso in picture coordinates.
    /// Full containment is optional. Retained transforms, strokes and ancestor clips are respected;
    /// text and image objects without path geometry use their rendered object bounds.
    /// </summary>
    public void SelectInLasso(IReadOnlyList<SKPoint> points,
        SvgEditorSelectionBehavior behavior = SvgEditorSelectionBehavior.Replace, bool fullyContained = false)
    {
        ThrowIfDisposed();
        ValidateSelectionBehavior(behavior);
        using var lasso = SvgEditorSelectionGeometry.CreateLasso(points);
        var matches = new List<SvgVisualElement>();
        if (Document is { } document)
        {
            foreach (var element in document.Descendants().OfType<SvgVisualElement>().Where(IsWandArtwork))
            {
                var node = GetSceneNode(element);
                if (node is null)
                    continue;
                using var geometry = SvgEditorSelectionGeometry.CreateArtwork(node, Renderer.SkiaModel);
                if (geometry is not null && SvgEditorSelectionGeometry.Matches(geometry, lasso, fullyContained))
                    matches.Add(element);
            }
        }
        // Geometry preparation completes before the live selection changes. A bad
        // input or failed native path operation cannot leave a partial selection.
        ApplyEditorSelection(matches, behavior);
    }

    private static bool IsWandArtwork(SvgVisualElement element)
        => element is not (SvgGroup or SvgSwitch) && IsSelectableArtwork(element);

    private void ApplyEditorSelection(IEnumerable<SvgVisualElement> matches, SvgEditorSelectionBehavior behavior)
    {
        var result = behavior == SvgEditorSelectionBehavior.Replace
            ? new List<SvgVisualElement>()
            : _selection.Where(IsSelectableArtwork).ToList();
        foreach (var element in matches.Distinct())
        {
            var present = result.Contains(element);
            if (behavior == SvgEditorSelectionBehavior.Subtract || behavior == SvgEditorSelectionBehavior.Toggle && present)
                result.Remove(element);
            else if (!present)
                result.Add(element);
        }
        var selected = new HashSet<SvgVisualElement>(result);
        result.RemoveAll(element => HierarchyAncestors(element).OfType<SvgVisualElement>().Any(selected.Contains));
        if (_selection.SequenceEqual(result))
            return;
        _selection.Clear();
        _selection.AddRange(result);
        SyncSelection();
        NotifyChanged();
    }

    private static void ValidateSelectionBehavior(SvgEditorSelectionBehavior behavior)
    {
        if (!Enum.IsDefined(typeof(SvgEditorSelectionBehavior), behavior))
            throw new ArgumentOutOfRangeException(nameof(behavior));
    }

    private float SelectionStrokeWidth(SvgVisualElement element)
        => GetSceneNode(element)?.StrokeWidth ?? element.StrokeWidth.Value;

    private static float SelectionOpacity(SvgVisualElement element)
    {
        var opacity = 1f;
        for (SvgElement? current = element; current is not null; current = current.Parent)
            opacity *= Math.Max(0, Math.Min(1, current.Opacity));
        return opacity;
    }

    private readonly struct SelectionPaint
    {
        public SelectionPaint(SKColor color) { Color = color; Resource = null; }
        public SelectionPaint(string resource) { Color = null; Resource = resource; }
        public SKColor? Color { get; }
        public string? Resource { get; }
    }

    private static SelectionPaint EffectiveSelectionPaint(SvgVisualElement owner, SvgPaintServer? paint, bool stroke, int depth = 0)
    {
        if (depth >= 32 || ReferenceEquals(paint, SvgPaintServer.None))
            return new SelectionPaint("none");
        if (paint is null || ReferenceEquals(paint, SvgPaintServer.NotSet) || ReferenceEquals(paint, SvgPaintServer.Inherit))
            return stroke ? new SelectionPaint("none") : new SelectionPaint(SKColors.Black);
        if (paint is SvgCssVariablePaintServer variable)
            return EffectiveSelectionPaint(owner, variable.Resolve(owner), stroke, depth + 1);
        if (paint is SvgDeferredPaintServer deferred)
        {
            if (string.Equals(deferred.DeferredId, "currentColor", StringComparison.OrdinalIgnoreCase))
                return EffectiveSelectionPaint(owner, owner.Color, false, depth + 1);
            var resolved = SvgDeferredPaintServer.TryGet<SvgPaintServer>(deferred, owner);
            if (resolved is not null && !ReferenceEquals(resolved, deferred))
                return EffectiveSelectionPaint(owner, resolved, stroke, depth + 1);
            return new SelectionPaint("unresolved:" + deferred.DeferredId);
        }
        if (paint is SvgColourServer solid)
            return new SelectionPaint(new SKColor(solid.Colour.R, solid.Colour.G, solid.Colour.B, solid.Colour.A));
        return new SelectionPaint(paint.GetType().FullName + ":" + paint.ID);
    }

    private static bool MatchesSelectionPaint(SelectionPaint first, SelectionPaint second, float tolerance)
    {
        if (first.Color is not { } a || second.Color is not { } b)
            return first.Color is null && second.Color is null && string.Equals(first.Resource, second.Resource, StringComparison.Ordinal);
        return Math.Abs(a.Red - b.Red) <= tolerance && Math.Abs(a.Green - b.Green) <= tolerance
            && Math.Abs(a.Blue - b.Blue) <= tolerance && Math.Abs(a.Alpha - b.Alpha) <= tolerance;
    }
}
