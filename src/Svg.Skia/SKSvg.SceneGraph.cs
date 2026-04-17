using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg;
using Svg.Model.Services;

namespace Svg.Skia;

public partial class SKSvg
{
    private readonly struct RetainedNodePictureCacheKey : IEquatable<RetainedNodePictureCacheKey>
    {
        public RetainedNodePictureCacheKey(string addressKey, bool hasClip, float left, float top, float right, float bottom)
        {
            AddressKey = addressKey;
            HasClip = hasClip;
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public string AddressKey { get; }
        public bool HasClip { get; }
        public float Left { get; }
        public float Top { get; }
        public float Right { get; }
        public float Bottom { get; }

        public static RetainedNodePictureCacheKey Create(string addressKey, SKRect? clip)
        {
            if (clip is { } clipRect)
            {
                return new RetainedNodePictureCacheKey(
                    addressKey,
                    hasClip: true,
                    clipRect.Left,
                    clipRect.Top,
                    clipRect.Right,
                    clipRect.Bottom);
            }

            return new RetainedNodePictureCacheKey(
                addressKey,
                hasClip: false,
                left: 0f,
                top: 0f,
                right: 0f,
                bottom: 0f);
        }

        public bool Equals(RetainedNodePictureCacheKey other)
        {
            return string.Equals(AddressKey, other.AddressKey, StringComparison.Ordinal) &&
                   HasClip == other.HasClip &&
                   Left.Equals(other.Left) &&
                   Top.Equals(other.Top) &&
                   Right.Equals(other.Right) &&
                   Bottom.Equals(other.Bottom);
        }

        public override bool Equals(object? obj)
        {
            return obj is RetainedNodePictureCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StringComparer.Ordinal.GetHashCode(AddressKey);
                hashCode = (hashCode * 397) ^ HasClip.GetHashCode();
                hashCode = (hashCode * 397) ^ Left.GetHashCode();
                hashCode = (hashCode * 397) ^ Top.GetHashCode();
                hashCode = (hashCode * 397) ^ Right.GetHashCode();
                hashCode = (hashCode * 397) ^ Bottom.GetHashCode();
                return hashCode;
            }
        }
    }

    private SvgSceneDocument? _retainedSceneGraph;
    private bool _retainedSceneGraphDirty = true;
    private SkiaSharp.SKPicture? _retainedPicture;
    private Dictionary<RetainedNodePictureCacheKey, SkiaSharp.SKPicture>? _retainedNodePictures;

    public SvgSceneDocument? RetainedSceneGraph
    {
        get
        {
            _ = TryEnsureRetainedSceneGraph(out var sceneDocument);
            return sceneDocument;
        }
    }

    public bool HasRetainedSceneGraph => RetainedSceneGraph is not null;

    /// <summary>
    /// Gets a cached native picture recorded directly from the current retained scene graph.
    /// </summary>
    public virtual SkiaSharp.SKPicture? RetainedPicture
    {
        get
        {
            SkiaSharp.SKPicture? retainedPicture;

            lock (Sync)
            {
                retainedPicture = _retainedPicture;
                if (retainedPicture is not null)
                {
                    return retainedPicture;
                }
            }

            if (!TryEnsureRetainedSceneGraph(out var sceneDocument) || sceneDocument is null)
            {
                return null;
            }

            var newPicture = SkiaModel.ToSKPicture(sceneDocument);
            if (newPicture is null)
            {
                return null;
            }

            lock (Sync)
            {
                if (!ReferenceEquals(_retainedSceneGraph, sceneDocument) || _retainedSceneGraphDirty)
                {
                    newPicture.Dispose();
                    return _retainedPicture;
                }

                if (_retainedPicture is { } existing)
                {
                    newPicture.Dispose();
                    return existing;
                }

                _retainedPicture = newPicture;
                return newPicture;
            }
        }
    }

    public bool TryEnsureRetainedSceneGraph(out SvgSceneDocument? sceneDocument)
    {
        SvgDocument? sourceDocument;

        lock (Sync)
        {
            if (!_retainedSceneGraphDirty)
            {
                sceneDocument = _retainedSceneGraph;
                return sceneDocument is not null;
            }

            sourceDocument = _animatedDocument ?? _sourceDocument;
        }

        if (sourceDocument is null)
        {
            lock (Sync)
            {
                ClearRetainedPictureLocked();
                _retainedSceneGraph = null;
                _retainedSceneGraphDirty = false;
                sceneDocument = null;
            }

            return false;
        }

        if (!SvgSceneRuntime.TryCompile(sourceDocument, AssetLoader, IgnoreAttributes, GetStandaloneViewport(), out var compiledSceneDocument))
        {
            lock (Sync)
            {
                ClearRetainedPictureLocked();
                _retainedSceneGraph = null;
                _retainedSceneGraphDirty = false;
                sceneDocument = null;
            }

            return false;
        }

        lock (Sync)
        {
            ClearRetainedPictureLocked();
            _retainedSceneGraph = compiledSceneDocument;
            _retainedSceneGraphDirty = false;
            sceneDocument = compiledSceneDocument;
        }

        return true;
    }

    public SKPicture? CreateRetainedSceneGraphModel()
    {
        return RetainedSceneGraph is { } sceneDocument
            ? sceneDocument.CreateModel()
            : null;
    }

    public SkiaSharp.SKPicture? CreateRetainedSceneGraphPicture()
    {
        return TryEnsureRetainedSceneGraph(out var sceneDocument) && sceneDocument is not null
            ? SkiaModel.ToSKPicture(sceneDocument)
            : null;
    }

    public bool TryGetRetainedSceneNode(string addressKey, out SvgSceneNode? node)
    {
        if (TryEnsureRetainedSceneGraph(out var sceneDocument) && sceneDocument is not null)
        {
            return sceneDocument.TryGetNode(addressKey, out node);
        }

        node = null;
        return false;
    }

    public bool TryGetRetainedSceneNode(SvgElement element, out SvgSceneNode? node)
    {
        if (element is null)
        {
            throw new System.ArgumentNullException(nameof(element));
        }

        return TryGetRetainedSceneNode(SvgSceneCompiler.TryGetElementAddressKey(element) ?? string.Empty, out node);
    }

    public bool TryGetRetainedSceneNodes(string addressKey, out IReadOnlyList<SvgSceneNode> nodes)
    {
        if (TryEnsureRetainedSceneGraph(out var sceneDocument) && sceneDocument is not null)
        {
            return sceneDocument.TryGetNodes(addressKey, out nodes);
        }

        nodes = System.Array.Empty<SvgSceneNode>();
        return false;
    }

    public bool TryGetRetainedSceneNodes(SvgElement element, out IReadOnlyList<SvgSceneNode> nodes)
    {
        if (element is null)
        {
            throw new System.ArgumentNullException(nameof(element));
        }

        return TryGetRetainedSceneNodes(SvgSceneCompiler.TryGetElementAddressKey(element) ?? string.Empty, out nodes);
    }

    public bool TryGetRetainedSceneNodeById(string id, out SvgSceneNode? node)
    {
        if (TryEnsureRetainedSceneGraph(out var sceneDocument) && sceneDocument is not null)
        {
            return sceneDocument.TryGetNodeById(id, out node);
        }

        node = null;
        return false;
    }

    public bool TryGetRetainedSceneResource(string addressKey, out SvgSceneResource? resource)
    {
        if (TryEnsureRetainedSceneGraph(out var sceneDocument) && sceneDocument is not null)
        {
            return sceneDocument.TryGetResource(addressKey, out resource);
        }

        resource = null;
        return false;
    }

    public bool TryGetRetainedSceneResourceById(string id, out SvgSceneResource? resource)
    {
        if (TryEnsureRetainedSceneGraph(out var sceneDocument) && sceneDocument is not null)
        {
            return sceneDocument.TryGetResourceById(id, out resource);
        }

        resource = null;
        return false;
    }

    public SvgSceneMutationResult ApplyRetainedSceneMutation(SvgElement element, IReadOnlyCollection<string>? changedAttributes = null)
    {
        if (!TryEnsureRetainedSceneGraph(out var sceneDocument) || sceneDocument is null)
        {
            return new SvgSceneMutationResult(false, 0, 0);
        }

        var result = sceneDocument.ApplyMutation(element, changedAttributes);
        if (result.Succeeded)
        {
            InvalidateRetainedPicture();
        }

        return result;
    }

    public SvgSceneMutationResult ApplyRetainedSceneMutation(string addressKey, IReadOnlyCollection<string>? changedAttributes = null)
    {
        if (!TryEnsureRetainedSceneGraph(out var sceneDocument) || sceneDocument is null)
        {
            return new SvgSceneMutationResult(false, 0, 0);
        }

        var result = sceneDocument.ApplyMutation(addressKey, changedAttributes);
        if (result.Succeeded)
        {
            InvalidateRetainedPicture();
        }

        return result;
    }

    public SvgSceneMutationResult ApplyRetainedSceneMutationById(string id, IReadOnlyCollection<string>? changedAttributes = null)
    {
        if (!TryEnsureRetainedSceneGraph(out var sceneDocument) || sceneDocument is null)
        {
            return new SvgSceneMutationResult(false, 0, 0);
        }

        var result = sceneDocument.ApplyMutationById(id, changedAttributes);
        if (result.Succeeded)
        {
            InvalidateRetainedPicture();
        }

        return result;
    }

    public bool TryApplyRetainedSceneMutationAndRender(
        SvgElement element,
        IReadOnlyCollection<string>? changedAttributes,
        out SvgSceneMutationResult? result)
    {
        result = null;
        if (!TryEnsureRetainedSceneGraph(out var sceneDocument) || sceneDocument is null)
        {
            return false;
        }

        result = sceneDocument.ApplyMutation(element, changedAttributes);
        if (!result.Succeeded)
        {
            return false;
        }

        DisableAnimationLayerCaching();
        return RenderRetainedSceneDocument(sceneDocument) is not null;
    }

    public bool TryApplyRetainedSceneMutationAndRender(
        string addressKey,
        IReadOnlyCollection<string>? changedAttributes,
        out SvgSceneMutationResult? result)
    {
        result = null;
        if (!TryEnsureRetainedSceneGraph(out var sceneDocument) || sceneDocument is null)
        {
            return false;
        }

        result = sceneDocument.ApplyMutation(addressKey, changedAttributes);
        if (!result.Succeeded)
        {
            return false;
        }

        DisableAnimationLayerCaching();
        return RenderRetainedSceneDocument(sceneDocument) is not null;
    }

    public bool TryApplyRetainedSceneMutationByIdAndRender(
        string id,
        IReadOnlyCollection<string>? changedAttributes,
        out SvgSceneMutationResult? result)
    {
        result = null;
        if (!TryEnsureRetainedSceneGraph(out var sceneDocument) || sceneDocument is null)
        {
            return false;
        }

        result = sceneDocument.ApplyMutationById(id, changedAttributes);
        if (!result.Succeeded)
        {
            return false;
        }

        DisableAnimationLayerCaching();
        return RenderRetainedSceneDocument(sceneDocument) is not null;
    }

    public SKPicture? CreateRetainedSceneNodeModel(SvgSceneNode node, SKRect? clip = null)
    {
        if (!TryEnsureRetainedSceneGraph(out var sceneDocument) ||
            sceneDocument is null ||
            !TryResolveRetainedSceneNode(sceneDocument, node, out var currentNode) ||
            currentNode is null)
        {
            return null;
        }

        return sceneDocument.CreateNodeModel(currentNode, clip);
    }

    public SkiaSharp.SKPicture? CreateRetainedSceneNodePicture(SvgSceneNode node, SKRect? clip = null)
    {
        if (!TryEnsureRetainedSceneGraph(out var sceneDocument) ||
            sceneDocument is null ||
            !TryResolveRetainedSceneNode(sceneDocument, node, out var currentNode) ||
            currentNode is null)
        {
            return null;
        }

        return SkiaModel.ToSKPicture(sceneDocument, currentNode, clip);
    }

    /// <summary>
    /// Gets a cached native picture for a retained-scene node without clip overrides.
    /// </summary>
    public SkiaSharp.SKPicture? GetCachedRetainedSceneNodePicture(SvgSceneNode node)
    {
        return GetCachedRetainedSceneNodePicture(node, clip: null);
    }

    /// <summary>
    /// Gets a cached native picture for a retained-scene node, optionally clipped to a stable region.
    /// </summary>
    public SkiaSharp.SKPicture? GetCachedRetainedSceneNodePicture(SvgSceneNode node, SKRect? clip)
    {
        if (!TryEnsureRetainedSceneGraph(out var sceneDocument) ||
            sceneDocument is null ||
            !TryResolveRetainedSceneNode(sceneDocument, node, out var currentNode) ||
            currentNode is null ||
            string.IsNullOrWhiteSpace(currentNode.ElementAddressKey))
        {
            return null;
        }

        var cacheKey = RetainedNodePictureCacheKey.Create(currentNode.ElementAddressKey!, clip);
        lock (Sync)
        {
            if (_retainedNodePictures is { } cachedPictures &&
                cachedPictures.TryGetValue(cacheKey, out var cachedPicture))
            {
                return cachedPicture;
            }
        }

        var newPicture = SkiaModel.ToSKPicture(sceneDocument, currentNode, clip);
        if (newPicture is null)
        {
            return null;
        }

        lock (Sync)
        {
            if (!ReferenceEquals(_retainedSceneGraph, sceneDocument) || _retainedSceneGraphDirty)
            {
                newPicture.Dispose();
                return null;
            }

            _retainedNodePictures ??= new Dictionary<RetainedNodePictureCacheKey, SkiaSharp.SKPicture>();
            if (_retainedNodePictures.TryGetValue(cacheKey, out var existingPicture))
            {
                newPicture.Dispose();
                return existingPicture;
            }

            _retainedNodePictures.Add(cacheKey, newPicture);
            return newPicture;
        }
    }

    public SkiaSharp.SKPicture? GetCachedRetainedScenePicture(SvgElement element)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return TryGetRetainedSceneNode(element, out var node) && node is not null
            ? GetCachedRetainedSceneNodePicture(node)
            : null;
    }

    public SkiaSharp.SKPicture? GetCachedRetainedScenePicture(SvgElement element, SKRect? clip)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return TryGetRetainedSceneNode(element, out var node) && node is not null
            ? GetCachedRetainedSceneNodePicture(node, clip)
            : null;
    }

    public SKPicture? CreateRetainedSceneModel(SvgElement element, SKRect? clip = null)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return TryGetRetainedSceneNode(element, out var node) && node is not null
            ? CreateRetainedSceneNodeModel(node, clip)
            : null;
    }

    public SkiaSharp.SKPicture? CreateRetainedScenePicture(SvgElement element, SKRect? clip = null)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        if (!TryEnsureRetainedSceneGraph(out var sceneDocument) ||
            sceneDocument is null ||
            !TryGetRetainedSceneNode(element, out var node) ||
            node is null)
        {
            return null;
        }

        return SkiaModel.ToSKPicture(sceneDocument, node, clip);
    }

    private void InvalidateRetainedSceneGraph()
    {
        lock (Sync)
        {
            if (_retainedSceneGraphDirty &&
                _retainedSceneGraph is null &&
                _model is null &&
                WireframePicture is null &&
                _retainedPicture is null &&
                _retainedNodePictures is null)
            {
                return;
            }

            Model = null;
            WireframePicture?.Dispose();
            WireframePicture = null;
            ClearRetainedPictureLocked();
            _retainedSceneGraphDirty = true;
            _retainedSceneGraph = null;
        }
    }

    private bool TryPrepareRetainedSceneGraphForAnimationFrame(SvgAnimationFrameState frameState, SvgAnimationFrameState? previousFrameState, out SvgSceneDocument? sceneDocument)
    {
        SvgDocument? currentDocument;
        var retainedPictureInvalidated = false;

        lock (Sync)
        {
            currentDocument = _animatedDocument ?? _sourceDocument;
        }

        sceneDocument = null;
        if (currentDocument is null)
        {
            return false;
        }

        if (!TryEnsureRetainedSceneGraph(out sceneDocument) || sceneDocument is null)
        {
            return false;
        }

        if (!ReferenceEquals(sceneDocument.SourceDocument, currentDocument))
        {
            return TryRebuildRetainedSceneGraphForCurrentDocument(currentDocument, out sceneDocument);
        }

        foreach (var dirtyAttribute in frameState.EnumerateDirtyAttributes(previousFrameState))
        {
            if (!sceneDocument.TryResolveElement(dirtyAttribute.TargetAddress.Key, out var targetElement) || targetElement is null)
            {
                return TryRebuildRetainedSceneGraphForCurrentDocument(currentDocument, out sceneDocument);
            }

            var result = sceneDocument.ApplyMutation(targetElement, new[] { dirtyAttribute.AttributeName });
            if (!result.Succeeded)
            {
                return TryRebuildRetainedSceneGraphForCurrentDocument(currentDocument, out sceneDocument);
            }

            if (!retainedPictureInvalidated)
            {
                InvalidateRetainedPicture();
                retainedPictureInvalidated = true;
            }
        }

        foreach (var removedAttribute in frameState.EnumerateRemovedAttributes(previousFrameState))
        {
            if (!sceneDocument.TryResolveElement(removedAttribute.TargetAddress.Key, out var targetElement) || targetElement is null)
            {
                return TryRebuildRetainedSceneGraphForCurrentDocument(currentDocument, out sceneDocument);
            }

            var result = sceneDocument.ApplyMutation(targetElement, new[] { removedAttribute.AttributeName });
            if (!result.Succeeded)
            {
                return TryRebuildRetainedSceneGraphForCurrentDocument(currentDocument, out sceneDocument);
            }

            if (!retainedPictureInvalidated)
            {
                InvalidateRetainedPicture();
                retainedPictureInvalidated = true;
            }
        }

        return true;
    }

    private bool TryRebuildRetainedSceneGraphForCurrentDocument(SvgDocument currentDocument, out SvgSceneDocument? sceneDocument)
    {
        DisableAnimationLayerCaching();

        if (!SvgSceneRuntime.TryCompile(currentDocument, AssetLoader, IgnoreAttributes, GetStandaloneViewport(), out var compiledSceneDocument) ||
            compiledSceneDocument is null)
        {
            InvalidateRetainedSceneGraph();
            sceneDocument = null;
            return false;
        }

        if (!ReferenceEquals(compiledSceneDocument.SourceDocument, currentDocument))
        {
            InvalidateRetainedSceneGraph();
            sceneDocument = null;
            return false;
        }

        lock (Sync)
        {
            ClearRetainedPictureLocked();
            _retainedSceneGraph = compiledSceneDocument;
            _retainedSceneGraphDirty = false;
            sceneDocument = compiledSceneDocument;
        }

        return true;
    }

    private void InvalidateRetainedPicture()
    {
        lock (Sync)
        {
            Model = null;
            WireframePicture?.Dispose();
            WireframePicture = null;
            ClearRetainedPictureLocked();
        }
    }

    private void ClearRetainedPictureLocked()
    {
        _retainedPicture?.Dispose();
        _retainedPicture = null;

        if (_retainedNodePictures is null)
        {
            return;
        }

        foreach (var cachedPicture in _retainedNodePictures.Values)
        {
            cachedPicture.Dispose();
        }

        _retainedNodePictures = null;
    }

    private static bool TryResolveRetainedSceneNode(SvgSceneDocument sceneDocument, SvgSceneNode node, out SvgSceneNode? resolvedNode)
    {
        if (node is null)
        {
            resolvedNode = null;
            return false;
        }

        if (ReferenceEquals(node, sceneDocument.Root))
        {
            resolvedNode = node;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(node.ElementAddressKey) &&
            sceneDocument.TryGetNode(node.ElementAddressKey!, out resolvedNode) &&
            resolvedNode is not null)
        {
            return true;
        }

        resolvedNode = node;
        return true;
    }
}
