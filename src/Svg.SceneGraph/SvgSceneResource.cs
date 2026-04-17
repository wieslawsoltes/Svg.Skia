using System;
using System.Collections.Generic;
using System.Globalization;
using ShimSkiaSharp;
using Svg;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

public sealed class SvgSceneResource
{
    private readonly HashSet<string> _subtreeAddresses = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dependencyKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reverseDependencyKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dependentCompilationRoots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgSceneClipPayload> _clipPayloads = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgSceneMaskPayload> _maskPayloads = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgSceneFilterPayload> _filterPayloads = new(StringComparer.Ordinal);

    internal SvgSceneResource(string key, SvgSceneResourceKind kind, SvgElement sourceElement, string? addressKey)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Kind = kind;
        SourceElement = sourceElement ?? throw new ArgumentNullException(nameof(sourceElement));
        AddressKey = addressKey;
        Id = sourceElement.ID;
        SubtreeAddresses = new ReadOnlySetView<string>(_subtreeAddresses);
        DependencyKeys = new ReadOnlySetView<string>(_dependencyKeys);
        ReverseDependencyKeys = new ReadOnlySetView<string>(_reverseDependencyKeys);
        DependentCompilationRoots = new ReadOnlySetView<string>(_dependentCompilationRoots);
    }

    public string Key { get; }

    public SvgSceneResourceKind Kind { get; }

    public SvgElement SourceElement { get; }

    public string? AddressKey { get; }

    public string? Id { get; }

    public IReadOnlyCollection<string> SubtreeAddresses { get; }

    public IReadOnlyCollection<string> DependencyKeys { get; }

    public IReadOnlyCollection<string> ReverseDependencyKeys { get; }

    public IReadOnlyCollection<string> DependentCompilationRoots { get; }

    internal void AddSubtreeAddress(string addressKey)
    {
        if (!string.IsNullOrWhiteSpace(addressKey))
        {
            _subtreeAddresses.Add(addressKey);
        }
    }

    internal void AddDependency(string dependencyKey)
    {
        if (!string.IsNullOrWhiteSpace(dependencyKey) && !string.Equals(Key, dependencyKey, StringComparison.Ordinal))
        {
            _dependencyKeys.Add(dependencyKey);
        }
    }

    internal void AddReverseDependency(string dependencyKey)
    {
        if (!string.IsNullOrWhiteSpace(dependencyKey) && !string.Equals(Key, dependencyKey, StringComparison.Ordinal))
        {
            _reverseDependencyKeys.Add(dependencyKey);
        }
    }

    internal void AddDependentCompilationRoot(string compilationRootKey)
    {
        if (!string.IsNullOrWhiteSpace(compilationRootKey))
        {
            _dependentCompilationRoots.Add(compilationRootKey);
        }
    }

    internal void RemoveDependentCompilationRoot(string compilationRootKey)
    {
        if (!string.IsNullOrWhiteSpace(compilationRootKey))
        {
            _dependentCompilationRoots.Remove(compilationRootKey);
        }
    }

    internal SvgSceneClipPayload? ResolveClipPayload(SvgSceneDocument sceneDocument, SvgSceneNode targetNode)
    {
        if (Kind != SvgSceneResourceKind.ClipPath || SourceElement is not SvgClipPath svgClipPath)
        {
            return null;
        }

        var cacheKey = CreatePayloadCacheKey(targetNode);
        if (_clipPayloads.TryGetValue(cacheKey, out var cachedPayload))
        {
            return cachedPayload;
        }

        var clipPath = SvgSceneClipCompiler.CompileClipPath(svgClipPath, targetNode.GeometryBounds, sceneDocument.AssetLoader);
        if (clipPath?.Clips is not { Count: > 0 })
        {
            return null;
        }

        var payload = new SvgSceneClipPayload(clipPath);
        _clipPayloads.Add(cacheKey, payload);
        return payload;
    }

    internal SvgSceneMaskPayload? ResolveMaskPayload(SvgSceneDocument sceneDocument, SvgSceneNode targetNode)
    {
        if (Kind != SvgSceneResourceKind.Mask || SourceElement is not SvgMask svgMask)
        {
            return null;
        }

        var targetBounds = targetNode.GeometryBounds;
        var cacheKey = CreatePayloadCacheKey(targetNode, targetBounds);
        if (_maskPayloads.TryGetValue(cacheKey, out var cachedPayload))
        {
            return cachedPayload;
        }

        if (svgMask.MaskUnits == SvgCoordinateUnits.ObjectBoundingBox &&
            (targetBounds.Width <= 0f || targetBounds.Height <= 0f))
        {
            var emptyPayload = CreateEmptyMaskPayload(svgMask);
            _maskPayloads.Add(cacheKey, emptyPayload);
            return emptyPayload;
        }

        if (!sceneDocument.TryEnterMaskResolution(Key))
        {
            return null;
        }

        try
        {
            var maskNode = SvgSceneCompiler.CompileMaskNode(
                svgMask,
                targetBounds,
                sceneDocument.CompilationViewport,
                sceneDocument.AssetLoader,
                sceneDocument.IgnoreAttributes);
            if (maskNode is null)
            {
                return null;
            }

            sceneDocument.ResolveRuntimePayloadTree(maskNode);

            var payload = new SvgSceneMaskPayload(maskNode, CreateMaskPaint(), CreateMaskDstInPaint());
            _maskPayloads.Add(cacheKey, payload);
            return payload;
        }
        finally
        {
            sceneDocument.ExitMaskResolution(Key);
        }
    }

    internal SvgSceneFilterPayload? ResolveFilterPayload(SvgSceneDocument sceneDocument, SvgSceneNode targetNode)
    {
        if (Kind != SvgSceneResourceKind.Filter || targetNode.Element is not SvgVisualElement visualElement)
        {
            return null;
        }

        var cacheKey = CreatePayloadCacheKey(targetNode);
        if (_filterPayloads.TryGetValue(cacheKey, out var cachedPayload))
        {
            return cachedPayload;
        }

        var references = targetNode.Element.OwnerDocument?.BaseUri is { } baseUri
            ? new HashSet<Uri> { baseUri }
            : null;
        var filterContext = new SvgSceneFilterContext(
            sceneDocument,
            visualElement,
            targetNode.GeometryBounds,
            sceneDocument.CompilationViewport,
            new SvgSceneFilterSource(sceneDocument, targetNode),
            sceneDocument.AssetLoader,
            references);

        var payload = filterContext.FilterPaint is { } filterPaint
            ? new SvgSceneFilterPayload(
                filterPaint.DeepClone(),
                filterContext.FilterClip,
                isValid: true,
                requiresInputCarrier: filterContext.RequiresInputCarrier,
                canSkipStandaloneFilterClip: filterContext.CanSkipStandaloneFilterClip)
            : SvgSceneFilterPayload.Invalid(filterContext.FilterClip);

        _filterPayloads.Add(cacheKey, payload);
        return payload;
    }

    private static string CreatePayloadCacheKey(SvgSceneNode targetNode, SKRect? effectiveBounds = null)
    {
        var key = targetNode.ElementAddressKey
                  ?? targetNode.ElementId
                  ?? targetNode.ElementTypeName;
        var compilationRootKey = targetNode.CompilationRootKey ?? string.Empty;
        var bounds = effectiveBounds ?? targetNode.GeometryBounds;
        var transform = targetNode.TotalTransform;

        return string.Join(
            "|",
            key,
            compilationRootKey,
            string.Join(
                ",",
                bounds.Left.ToString(CultureInfo.InvariantCulture),
                bounds.Top.ToString(CultureInfo.InvariantCulture),
                bounds.Right.ToString(CultureInfo.InvariantCulture),
                bounds.Bottom.ToString(CultureInfo.InvariantCulture)),
            string.Join(
                ",",
                transform.ScaleX.ToString(CultureInfo.InvariantCulture),
                transform.SkewX.ToString(CultureInfo.InvariantCulture),
                transform.TransX.ToString(CultureInfo.InvariantCulture),
                transform.SkewY.ToString(CultureInfo.InvariantCulture),
                transform.ScaleY.ToString(CultureInfo.InvariantCulture),
                transform.TransY.ToString(CultureInfo.InvariantCulture),
                transform.Persp0.ToString(CultureInfo.InvariantCulture),
                transform.Persp1.ToString(CultureInfo.InvariantCulture),
                transform.Persp2.ToString(CultureInfo.InvariantCulture)));
    }

    private static SKPaint CreateMaskPaint()
    {
        return new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.StrokeAndFill
        };
    }

    private static SKPaint CreateMaskDstInPaint()
    {
        return new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.StrokeAndFill,
            BlendMode = SKBlendMode.DstIn,
            Color = FilterEffectsService.s_transparentBlack,
            ColorFilter = SKColorFilter.CreateLumaColor()
        };
    }

    private static SvgSceneMaskPayload CreateEmptyMaskPayload(SvgMask svgMask)
    {
        var maskNode = new SvgSceneNode(
            SvgSceneNodeKind.Mask,
            svgMask,
            SvgSceneCompiler.TryGetElementAddressKey(svgMask),
            svgMask.GetType().Name,
            compilationRootKey: null,
            isCompilationRootBoundary: false)
        {
            CompilationStrategy = SvgSceneCompilationStrategy.DirectRetained,
            IsRenderable = true,
            IsAntialias = PaintingService.IsAntialias(svgMask),
            GeometryBounds = SKRect.Empty,
            Transform = SKMatrix.Identity,
            TotalTransform = SKMatrix.Identity,
            TransformedBounds = SKRect.Empty
        };

        SvgSceneCompiler.AssignRetainedVisualState(maskNode, svgMask);
        SvgSceneCompiler.AssignRetainedResourceKeys(maskNode, svgMask);
        return new SvgSceneMaskPayload(maskNode, CreateMaskPaint(), CreateMaskDstInPaint());
    }

    private sealed class ReadOnlySetView<T> : IReadOnlyCollection<T> where T : notnull
    {
        private readonly HashSet<T> _source;

        public ReadOnlySetView(HashSet<T> source)
        {
            _source = source;
        }

        public int Count => _source.Count;

        public bool Contains(T item) => _source.Contains(item);

        public IEnumerator<T> GetEnumerator() => _source.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

internal sealed class SvgSceneClipPayload
{
    public SvgSceneClipPayload(ClipPath clipPath)
    {
        ClipPath = clipPath;
    }

    public ClipPath ClipPath { get; }
}

internal sealed class SvgSceneMaskPayload
{
    public SvgSceneMaskPayload(SvgSceneNode maskNode, SKPaint maskPaint, SKPaint maskDstIn)
    {
        MaskNode = maskNode;
        MaskPaint = maskPaint;
        MaskDstIn = maskDstIn;
    }

    public SvgSceneNode MaskNode { get; }

    public SKPaint MaskPaint { get; }

    public SKPaint MaskDstIn { get; }
}

internal sealed class SvgSceneFilterPayload
{
    public SvgSceneFilterPayload(SKPaint? filterPaint, SKRect? filterClip, bool isValid, bool requiresInputCarrier = false, bool canSkipStandaloneFilterClip = false)
    {
        FilterPaint = filterPaint;
        FilterClip = filterClip;
        IsValid = isValid;
        RequiresInputCarrier = requiresInputCarrier;
        CanSkipStandaloneFilterClip = canSkipStandaloneFilterClip;
    }

    public static SvgSceneFilterPayload Invalid(SKRect? filterClip)
        => new(null, filterClip, isValid: false);

    public SKPaint? FilterPaint { get; }

    public SKRect? FilterClip { get; }

    public bool IsValid { get; }

    public bool RequiresInputCarrier { get; }

    public bool CanSkipStandaloneFilterClip { get; }
}
