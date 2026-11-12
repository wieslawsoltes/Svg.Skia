using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SkiaSharp;
using Shim = ShimSkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>
/// Retains immutable artboard geometry for repainting an editor view. This cache
/// borrows the controller; it never owns or disposes its document or pictures.
/// </summary>
public sealed class SvgEditorArtboardCache
{
    private SvgEditorController? _controller;
    private SvgDocument? _document;
    private SKPicture? _picture;
    private long _documentRevision;
    private long _renderRevision;
    private Shim.SKMatrix _rootTransform;
    private SKRect _pictureBounds;
    private IReadOnlyList<SvgEditorArtboard> _artboards = Array.Empty<SvgEditorArtboard>();
    private SKRect _bounds;

    /// <summary>
    /// Camera and selection changes reuse the snapshot. Document edits must use
    /// the controller's transaction or Refresh API, as they do for rendered artwork.
    /// The controller's live GetArtboards API remains available for raw DOM inspection.
    /// </summary>
    public IReadOnlyList<SvgEditorArtboard> GetArtboards(SvgEditorController? controller)
    {
        if (controller?.Document is not { } document || controller.Renderer.Picture is not { } picture)
        {
            Clear();
            return _artboards;
        }

        // A renderer can also change its root transform or picture through the
        // animation API without a controller transaction. Include both in the key.
        var root = controller.GetSceneNode(document) ?? controller.Renderer.RetainedSceneGraph?.Root;
        var rootTransform = root?.TotalTransform ?? Shim.SKMatrix.Identity;
        var pictureBounds = picture.CullRect;
        if (ReferenceEquals(_controller, controller) && ReferenceEquals(_document, document)
            && ReferenceEquals(_picture, picture) && _documentRevision == controller.DocumentRevision
            && _renderRevision == controller.RenderRevision && _rootTransform == rootTransform
            && _pictureBounds == pictureBounds)
            return _artboards;

        var artboards = controller.GetArtboards();
        // GetArtboards returns a fresh snapshot. Hide its writable array/list so a
        // consumer cannot corrupt subsequent frames through an IList downcast.
        _artboards = artboards.Count == 0 ? Array.Empty<SvgEditorArtboard>()
            : new ReadOnlyCollection<SvgEditorArtboard>(artboards as IList<SvgEditorArtboard>
                ?? new List<SvgEditorArtboard>(artboards));
        _bounds = _artboards.Count == 0 ? SKRect.Empty : _artboards[0].Bounds;
        for (var index = 1; index < _artboards.Count; index++)
            _bounds = SKRect.Union(_bounds, _artboards[index].Bounds);

        _controller = controller;
        _document = document;
        _picture = picture;
        _documentRevision = controller.DocumentRevision;
        _renderRevision = controller.RenderRevision;
        _rootTransform = rootTransform;
        _pictureBounds = pictureBounds;
        return _artboards;
    }

    /// <summary>Returns the union of the current cached picture-coordinate artboards.</summary>
    public SKRect GetBounds(SvgEditorController? controller)
    {
        GetArtboards(controller);
        return _bounds;
    }

    /// <summary>Releases borrowed references when the owning view unloads or changes documents.</summary>
    public void Clear()
    {
        _controller = null;
        _document = null;
        _picture = null;
        _artboards = Array.Empty<SvgEditorArtboard>();
        _bounds = SKRect.Empty;
    }
}
