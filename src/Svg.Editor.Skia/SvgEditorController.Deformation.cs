using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>
/// An owned, disposable preview. Paths are picture-space outlines borrowed by the caller
/// for drawing; do not mutate or dispose them individually. Local SVG data is captured
/// independently, so rendering a preview cannot alter the prepared document edit.
/// </summary>
public sealed class SvgDeformationPreview : IDisposable
{
    private readonly ReadOnlyCollection<SKPath> _paths;

    internal SvgDeformationPreview(SvgEditorController owner, SvgDocument document, string source, string[] selection,
        SvgDeformationKind kind, SvgVisualElement[] originals, string[] localData, List<SKPath> paths)
    {
        Owner = owner;
        Document = document;
        Source = source;
        Selection = selection;
        Kind = kind;
        Originals = originals;
        LocalData = localData;
        _paths = paths.AsReadOnly();
    }

    public IReadOnlyList<SKPath> Paths => _paths;
    public bool HasChanges => Originals.Length != 0;
    public SvgDeformationKind Kind { get; }
    public bool IsDisposed { get; private set; }
    internal SvgEditorController Owner { get; }
    internal SvgDocument Document { get; }
    internal string Source { get; }
    internal string[] Selection { get; }
    internal SvgVisualElement[] Originals { get; }
    internal string[] LocalData { get; }
    internal bool Applied { get; set; }

    public void Dispose()
    {
        if (IsDisposed)
            return;
        IsDisposed = true;
        foreach (var path in _paths)
            path.Dispose();
    }
}

public sealed partial class SvgEditorController
{
    /// <summary>
    /// Computes a nonlinear brush stroke against selected editable paths and primitives.
    /// Preparation owns geometry snapshots and never changes the document, selection,
    /// renderer or history. Groups expand to their visible editable descendants.
    /// </summary>
    public SvgDeformationPreview PrepareDeformation(SvgDeformationKind kind, IReadOnlyList<SKPoint> pictureStroke,
        SvgDeformationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        SvgPathDeformationService.ValidateStroke(pictureStroke);
        var settings = options ?? new SvgDeformationOptions();
        return PrepareDeformationCore(kind, settings, path =>
        {
            var deformed = SvgPathDeformationService.Deform(path, kind, pictureStroke, settings, out var changed, cancellationToken);
            return (deformed, changed);
        }, cancellationToken);
    }

    /// <summary>Prepares a held brush using elapsed samples, without creating intermediate document edits.</summary>
    public SvgDeformationPreview PrepareDeformation(SvgDeformationKind kind, IReadOnlyList<SvgDeformationSample> pictureStroke,
        SvgDeformationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        SvgPathDeformationService.ValidateStroke(pictureStroke);
        var settings = options ?? new SvgDeformationOptions();
        return PrepareDeformationCore(kind, settings, path =>
        {
            var deformed = SvgPathDeformationService.Deform(path, kind, pictureStroke, settings, out var changed, cancellationToken);
            return (deformed, changed);
        }, cancellationToken);
    }

    private SvgDeformationPreview PrepareDeformationCore(SvgDeformationKind kind, SvgDeformationOptions options,
        Func<SKPath, (SKPath Path, bool Changed)> deform, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (IsEditing)
            throw new InvalidOperationException("Complete the active gesture before preparing a deformation.");
        if (!Enum.IsDefined(typeof(SvgDeformationKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        var document = Document ?? throw new InvalidOperationException("Open a document before deforming artwork.");
        EnsureSelectionEditable();
        var source = GetSvg();
        var selection = CaptureSelection();
        var targets = GetDeformationTargets();
        if (targets.Length > 512)
            throw new InvalidOperationException("Deform at most 512 selected objects at a time.");
        var originals = new List<SvgVisualElement>();
        var localData = new List<string>();
        var paths = new List<SKPath>();
        var points = 0L;
        var characters = 0L;
        try
        {
            foreach (var element in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (element.Children.Count != 0)
                    throw new InvalidOperationException("Bake animated geometry before deforming its path.");
                var node = GetSceneNode(element) ?? throw new InvalidOperationException("The selected artwork has no retained geometry.");
                var world = ToMatrix(node.TotalTransform);
                ValidateMatrix(world);
                if (!world.TryInvert(out var inverse))
                    throw new InvalidOperationException("An object with a singular transform cannot be deformed.");
                using var local = node.HitTestPath is { } retained
                    ? Renderer.SkiaModel.ToSKPath(retained)
                    : CreateGeometryPath(element) ?? throw new InvalidOperationException("Convert the selected artwork to paths before deforming it.");
                local.Transform(world);
                var (deformed, changed) = deform(local);
                if (!changed)
                { deformed.Dispose(); continue; }
                paths.Add(deformed);
                points += deformed.PointCount;
                if (points > options.MaximumPoints)
                    throw new InvalidOperationException("The selected objects exceed the deformation point budget. Deform a smaller selection.");
                using var result = new SKPath(deformed);
                result.Transform(inverse);
                var data = PathService.ToSvgPathData(result);
                characters += data.Length;
                if (characters > 16 * 1024 * 1024)
                    throw new InvalidOperationException("The deformation exceeds the SVG path-data budget. Deform a smaller selection.");
                originals.Add(element);
                localData.Add(data);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new SvgDeformationPreview(this, document, source, selection, kind, originals.ToArray(), localData.ToArray(), paths);
        }
        catch
        {
            foreach (var path in paths)
                path.Dispose();
            throw;
        }
    }

    /// <summary>Applies a current preview as one atomic history entry, preserving IDs, parents and presentation.</summary>
    public bool ApplyDeformation(SvgDeformationPreview prepared)
    {
        ThrowIfDisposed();
        if (prepared is null)
            throw new ArgumentNullException(nameof(prepared));
        if (prepared.IsDisposed)
            throw new ObjectDisposedException(nameof(prepared));
        if (prepared.Applied || !ReferenceEquals(prepared.Owner, this) || !ReferenceEquals(prepared.Document, Document)
            || IsEditing || !string.Equals(prepared.Source, GetSvg(), StringComparison.Ordinal)
            || !prepared.Selection.SequenceEqual(CaptureSelection()))
            throw new InvalidOperationException("The deformation preview is stale or already applied. Draw the stroke again on the current selection.");
        EnsureSelectionEditable();
        if (!prepared.HasChanges)
        { prepared.Applied = true; return false; }
        var replacements = new Dictionary<SvgVisualElement, SvgPath>();
        for (var index = 0; index < prepared.Originals.Length; index++)
        {
            var original = prepared.Originals[index];
            EnsureElementEditable(original);
            var replacement = CloneAsPath(original, prepared.LocalData[index], true);
            // A deformed generated outline is now independent artwork. Retaining its
            // centerline/profile would let a later brush edit silently restore old geometry.
            foreach (var attribute in new[]
            {
                SvgVariableStrokeService.CenterlineAttribute, SvgVariableStrokeService.OutlineAttribute,
                SvgVariableStrokeService.ProfileAttribute, SvgVariableStrokeService.CapAttribute,
                SvgBrushLibraryService.BrushIdAttribute, SvgBrushLibraryService.SettingsAttribute,
                SvgBrushLibraryService.BaseOpacityAttribute, "stroke-profile"
            })
                replacement.CustomAttributes.Remove(attribute);
            replacements.Add(original, replacement);
        }
        var previousSelection = _selection.ToArray();
        Execute(prepared.Kind + " artwork", () =>
        {
            foreach (var pair in replacements)
            {
                var parent = pair.Key.Parent ?? throw new InvalidOperationException("The deformation target was detached.");
                var index = parent.Children.IndexOf(pair.Key);
                parent.Children.Remove(pair.Key);
                parent.Children.Insert(index, pair.Value);
                RebindPaintServers(pair.Value);
            }
            _selection.Clear();
            foreach (var selected in previousSelection)
                _selection.Add(replacements.TryGetValue(selected, out var replacement) ? replacement : selected);
        });
        prepared.Applied = true;
        return true;
    }

    public bool DeformAlong(SvgDeformationKind kind, IReadOnlyList<SKPoint> pictureStroke,
        SvgDeformationOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var preview = PrepareDeformation(kind, pictureStroke, options, cancellationToken);
        return ApplyDeformation(preview);
    }

    public bool DeformAlong(SvgDeformationKind kind, IReadOnlyList<SvgDeformationSample> pictureStroke,
        SvgDeformationOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var preview = PrepareDeformation(kind, pictureStroke, options, cancellationToken);
        return ApplyDeformation(preview);
    }

    private SvgVisualElement[] GetDeformationTargets()
    {
        var result = new List<SvgVisualElement>();
        foreach (var selected in TopLevelSelection())
            Visit(selected);
        return result.Distinct().ToArray();

        void Visit(SvgVisualElement element)
        {
            if (!IsSelectableArtwork(element))
                return;
            if (element is SvgGroup)
            {
                foreach (var descendant in element.Descendants().OfType<SvgVisualElement>())
                {
                    if (descendant is SvgGroup || !IsSelectableArtwork(descendant))
                        continue;
                    Add(descendant);
                }
            }
            else
                Add(element);
        }

        void Add(SvgVisualElement element)
        {
            if (element is not (SvgPath or SvgRectangle or SvgCircle or SvgEllipse or SvgLine or SvgPolygon or SvgPolyline))
                throw new InvalidOperationException("Convert selected text, instances and other objects to paths before deforming them.");
            EnsureElementEditable(element);
            result.Add(element);
        }
    }
}
