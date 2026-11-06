using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SK = SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>An immutable computed edit containing managed SVG path data, not native handles.</summary>
public sealed class SvgPreparedDivision
{
    internal SvgPreparedDivision(SvgEditorController owner, SvgDocument document, string source,
        string[] selection, SvgVisualElement[] originals, (int Source, string Data)[] pieces)
    { Owner = owner; Document = document; Source = source; Selection = selection; Originals = originals; Pieces = pieces; }
    internal SvgEditorController Owner { get; }
    internal SvgDocument Document { get; }
    internal string Source { get; }
    internal string[] Selection { get; }
    internal SvgVisualElement[] Originals { get; }
    internal (int Source, string Data)[] Pieces { get; }
    internal bool Applied { get; set; }
    public int PieceCount => Pieces.Length;
}

public sealed partial class SvgEditorController
{
    /// <summary>
    /// Capture on the editor thread. Only owned geometry snapshots are touched during
    /// computation. ApplyPreparedDivision must be called back on the editor thread.
    /// Disable worker execution on a single-threaded browser to use cooperative slices.
    /// Cancellation during capture or computation never changes the document/history.
    /// </summary>
    public Task<SvgPreparedDivision> PrepareDivisionAsync(int maximumPieces = SvgPathDivisionService.DefaultMaximumPieces,
        CancellationToken cancellationToken = default, IProgress<SvgPathDivisionProgress>? progress = null,
        bool useWorkerThread = true)
    {
        ThrowIfDisposed();
        if (IsEditing) throw new InvalidOperationException("Complete the current gesture before preparing a division.");
        if (maximumPieces < 1) throw new ArgumentOutOfRangeException(nameof(maximumPieces));
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSelectionEditable();
        var elements = TopLevelSelection();
        if (Document is null || elements.Length < 2) throw new InvalidOperationException("Select at least two paths or shapes to divide.");
        var parent = elements[0].Parent;
        if (parent is null || elements.Any(element => !ReferenceEquals(element.Parent, parent)))
            throw new InvalidOperationException("Move the selected objects into the same layer or group before dividing them.");
        if (elements.Length > SvgPathDivisionService.MaximumOperands)
            throw new InvalidOperationException($"Divide at most {SvgPathDivisionService.MaximumOperands} objects at a time.");
        elements = elements.OrderBy(element => parent.Children.IndexOf(element)).ToArray();
        var document = Document;
        var source = GetSvg();
        var selection = CaptureSelection();
        var geometry = new List<SK.SKPath>(elements.Length);
        var inverses = new SK.SKMatrix[elements.Length];
        try
        {
            for (var index = 0; index < elements.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var element = elements[index];
                ValidateDivisionOperand(element);
                var node = GetSceneNode(element) ?? throw new InvalidOperationException("A selected object has no editable geometry.");
                var world = ToMatrix(node.TotalTransform);
                ValidateMatrix(world);
                if (!world.TryInvert(out inverses[index])) throw new InvalidOperationException("A selected object has a singular transform.");
                var path = CreateGeometryPath(element) ?? throw new InvalidOperationException("Convert the selected objects to paths before dividing them.");
                geometry.Add(path);
                path.FillType = element.FillRule == SvgFillRule.EvenOdd ? SK.SKPathFillType.EvenOdd : SK.SKPathFillType.Winding;
                path.Transform(world);
            }
        }
        catch
        {
            foreach (var path in geometry) path.Dispose();
            throw;
        }
        async Task<SvgPreparedDivision> ComputeAsync()
        {
            try
            {
                using var result = await SvgPathDivisionService.DivideAsync(geometry, maximumPieces, cancellationToken, progress).ConfigureAwait(false);
                var pieces = new (int Source, string Data)[result.Pieces.Count];
                var characters = 0L;
                var slice = Stopwatch.StartNew();
                for (var index = 0; index < pieces.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var piece = result.Pieces[index];
                    using var local = piece.Geometry.ToWinding() ?? throw new InvalidOperationException("A divided region could not be normalized for SVG.");
                    local.Transform(inverses[piece.SourceIndex]);
                    var data = PathService.ToSvgPathData(local);
                    characters += data.Length;
                    if (characters > 16 * 1024 * 1024) throw new InvalidOperationException("The divided geometry exceeds the 16 MiB SVG path-data budget. Divide a smaller selection.");
                    pieces[index] = (piece.SourceIndex, data);
                    if (slice.ElapsedMilliseconds >= 8)
                    {
                        await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                        slice.Restart();
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                return new SvgPreparedDivision(this, document, source, selection, elements, pieces);
            }
            finally { foreach (var path in geometry) path.Dispose(); }
        }
        // Do not pass the token to Task.Run: the delegate must execute its finally even
        // when cancellation happens while queued, because it owns the captured paths.
        try { return useWorkerThread ? Task.Run(ComputeAsync) : ComputeAsync(); }
        catch { foreach (var path in geometry) path.Dispose(); throw; }
    }

    public void ApplyPreparedDivision(SvgPreparedDivision prepared)
    {
        ThrowIfDisposed();
        if (prepared is null) throw new ArgumentNullException(nameof(prepared));
        if (prepared.Applied || !ReferenceEquals(prepared.Owner, this) || !ReferenceEquals(prepared.Document, Document) || IsEditing
            || !string.Equals(prepared.Source, GetSvg(), StringComparison.Ordinal) || !prepared.Selection.SequenceEqual(CaptureSelection()))
            throw new InvalidOperationException("The division result is stale or already applied. Recompute it against the current document and selection.");
        EnsureSelectionEditable();
        var elements = prepared.Originals;
        var parent = elements[0].Parent!;
        var replacements = new List<SvgPath>[elements.Length];
        for (var index = 0; index < replacements.Length; index++) replacements[index] = new List<SvgPath>();
        foreach (var piece in prepared.Pieces)
        {
            var path = CloneAsPath(elements[piece.Source], piece.Data, true);
            var siblings = replacements[piece.Source];
            if (siblings.Count != 0 || string.IsNullOrEmpty(path.ID)) path.ID = CreateId("divide");
            siblings.Add(path);
        }
        Execute("Divide paths", () =>
        {
            _selection.Clear();
            for (var index = 0; index < elements.Length; index++)
            {
                var insertion = parent.Children.IndexOf(elements[index]);
                parent.Children.Remove(elements[index]);
                foreach (var path in replacements[index])
                {
                    parent.Children.Insert(insertion++, path);
                    RebindPaintServers(path);
                    _selection.Add(path);
                }
            }
        });
        prepared.Applied = true;
    }
}
