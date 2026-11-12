using System;
using System.Runtime.CompilerServices;
using SkiaSharp;
using Svg.Skia;
using Shim = ShimSkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>
/// Identifies the rendered document content independently of editor selection.
/// Views add their own camera, appearance, and active-artboard state as needed.
/// </summary>
public readonly struct SvgEditorRenderStamp : IEquatable<SvgEditorRenderStamp>
{
    private readonly SvgEditorController? _controller;
    private readonly SvgDocument? _document;
    private readonly SKSvg? _renderer;
    private readonly SKPicture? _picture;
    private readonly Shim.SKPicture? _model;
    private readonly long _documentRevision;
    private readonly long _renderRevision;
    private readonly bool _wireframe;
    private readonly Shim.SKMatrix _rootTransform;
    private readonly SKRect _pictureBounds;

    private SvgEditorRenderStamp(SvgEditorController controller)
    {
        _controller = controller;
        _document = controller.Document;
        _renderer = controller.Renderer;
        _picture = _renderer.Picture;
        _model = _renderer.Model;
        _documentRevision = controller.DocumentRevision;
        _renderRevision = controller.RenderRevision;
        _wireframe = _renderer.Wireframe;
        var root = _document is null ? null : controller.GetSceneNode(_document) ?? _renderer.RetainedSceneGraph?.Root;
        _rootTransform = root?.TotalTransform ?? Shim.SKMatrix.Identity;
        _pictureBounds = _picture?.CullRect ?? SKRect.Empty;
    }

    /// <summary>Captures content already owned by the controller; no document is cloned or parsed.</summary>
    public static SvgEditorRenderStamp Capture(SvgEditorController? controller)
        => controller is null ? default : new SvgEditorRenderStamp(controller);

    public bool Equals(SvgEditorRenderStamp other)
        => ReferenceEquals(_controller, other._controller) && ReferenceEquals(_document, other._document)
        && ReferenceEquals(_renderer, other._renderer) && ReferenceEquals(_picture, other._picture)
        && ReferenceEquals(_model, other._model) && _documentRevision == other._documentRevision
        && _renderRevision == other._renderRevision && _wireframe == other._wireframe
        && _rootTransform == other._rootTransform && _pictureBounds == other._pictureBounds;

    public override bool Equals(object? obj) => obj is SvgEditorRenderStamp other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = IdentityHash(_controller);
            hash = hash * 31 + IdentityHash(_document);
            hash = hash * 31 + IdentityHash(_renderer);
            hash = hash * 31 + IdentityHash(_picture);
            hash = hash * 31 + IdentityHash(_model);
            hash = hash * 31 + _documentRevision.GetHashCode();
            hash = hash * 31 + _renderRevision.GetHashCode();
            hash = hash * 31 + _wireframe.GetHashCode();
            hash = hash * 31 + _rootTransform.GetHashCode();
            return hash * 31 + _pictureBounds.GetHashCode();
        }
    }

    public static bool operator ==(SvgEditorRenderStamp left, SvgEditorRenderStamp right) => left.Equals(right);
    public static bool operator !=(SvgEditorRenderStamp left, SvgEditorRenderStamp right) => !left.Equals(right);

    private static int IdentityHash(object? value) => value is null ? 0 : RuntimeHelpers.GetHashCode(value);
}
