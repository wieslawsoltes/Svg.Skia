using System;
using System.Linq;

namespace Svg.Editor.Skia;

public sealed partial class SvgEditorController
{
    /// <summary>
    /// Creates an editable SVG cropped to one named artboard. The source document,
    /// selection, renderer, active artboard, save checkpoint and history are unchanged.
    /// Resource references retain the original base URI for the caller to rebase
    /// when writing to a different destination.
    /// </summary>
    public string GetArtboardSvg(string id)
    {
        ThrowIfDisposed();
        if (id is null)
            throw new ArgumentNullException(nameof(id));
        if (Document is not { } document)
            throw new InvalidOperationException("Open a document before exporting an artboard.");
        var board = GetArtboards().FirstOrDefault(candidate => candidate.Id == id);
        if (board.Id is null)
            throw new InvalidOperationException("The artboard is no longer in this document.");
        if (board.IsImplicit)
            return GetSvg();
        using var snapshot = new SvgEditorController();
        snapshot.LoadDocument(ParseDocument(GetSvg(), document.BaseUri));
        // Locks govern authoring, not read-only output. Only the private snapshot
        // receives the viewport change; all lock metadata remains in its SVG.
        snapshot.SetDocumentViewport(board.Bounds, false);
        return snapshot.GetSvg();
    }
}
