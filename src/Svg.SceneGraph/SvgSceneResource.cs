using System;
using System.Collections.Generic;
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
    private readonly Dictionary<SvgSceneBoundsPayloadCacheKey, SvgSceneClipPayload> _clipPayloads = new();
    private readonly Dictionary<SvgSceneBoundsPayloadCacheKey, SvgSceneMaskPayload> _maskPayloads = new();
    private readonly Dictionary<SvgSceneResourcePayloadCacheKey, SvgSceneFilterPayload> _filterPayloads = new();
    private readonly Dictionary<SvgSceneSharedFilterPayloadCacheKey, SvgSceneFilterPayload> _sharedFilterPayloads = new();

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

    internal SvgSceneClipPayload? ResolveClipPayload(SvgSceneDocument sceneDocument, SvgSceneNode targetNode)
    {
        if (Kind != SvgSceneResourceKind.ClipPath || SourceElement is not SvgClipPath svgClipPath)
        {
            return null;
        }

        var cacheKey = CreateBoundsPayloadCacheKey(targetNode);
        if (_clipPayloads.TryGetValue(cacheKey, out var cachedPayload))
        {
            return cachedPayload;
        }

        var clipPath = SvgSceneClipCompiler.CompileClipPath(svgClipPath, targetNode.GeometryBounds, sceneDocument.AssetLoader);
        if (clipPath is null || !HasClipGeometry(clipPath))
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
        var cacheKey = CreateBoundsPayloadCacheKey(targetNode, targetBounds);
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
            var maskType = GetMaskType(svgMask);

            var payload = new SvgSceneMaskPayload(maskNode, CreateMaskPaint(), CreateMaskDstInPaint(maskType));
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

        var cacheKey = CreatePayloadCacheKey(targetNode, includeFilterDeclaration: true);
        if (_filterPayloads.TryGetValue(cacheKey, out var cachedPayload))
        {
            return cachedPayload;
        }

        var filterDeclaration = GetOwnFilterDeclaration(targetNode);
        var canUseSharedFilterPayload = IsSingleUrlFilterDeclaration(filterDeclaration);
        var sharedCacheKey = CreateSharedFilterPayloadCacheKey(filterDeclaration, targetNode.GeometryBounds);
        if (canUseSharedFilterPayload && _sharedFilterPayloads.TryGetValue(sharedCacheKey, out cachedPayload))
        {
            _filterPayloads.Add(cacheKey, cachedPayload);
            return cachedPayload;
        }

        var initialReferenceUri = targetNode.Element.OwnerDocument?.BaseUri;
        var filterContext = new SvgSceneFilterContext(
            sceneDocument,
            visualElement,
            targetNode.GeometryBounds,
            sceneDocument.CompilationViewport,
            new SvgSceneFilterSource(sceneDocument, targetNode),
            sceneDocument.AssetLoader,
            references: null,
            targetTransform: targetNode.TotalTransform,
            initialReferenceUri: initialReferenceUri);

        var payload = filterContext.FilterPaint is { } filterPaint
            ? new SvgSceneFilterPayload(
                filterPaint,
                filterContext.FilterClip,
                isValid: true,
                filterContext.UsesGlobalLayer,
                filterContext.GlobalClip)
            : filterContext.IsValid
                ? null
                : SvgSceneFilterPayload.Invalid(filterContext.FilterClip);

        if (payload is { })
        {
            _filterPayloads.Add(cacheKey, payload);
            if (canUseSharedFilterPayload &&
                payload.IsValid &&
                filterContext.CanReuseFilterPaintAcrossTargets &&
                !_sharedFilterPayloads.ContainsKey(sharedCacheKey))
            {
                _sharedFilterPayloads.Add(sharedCacheKey, payload);
            }
        }

        return payload;
    }

    private static SvgSceneResourcePayloadCacheKey CreatePayloadCacheKey(
        SvgSceneNode targetNode,
        SKRect? effectiveBounds = null,
        bool includeFilterDeclaration = false)
    {
        var key = targetNode.ElementAddressKey
                  ?? targetNode.ElementId
                  ?? targetNode.ElementTypeName;
        var compilationRootKey = targetNode.CompilationRootKey ?? string.Empty;
        var filterDeclaration = includeFilterDeclaration ? GetOwnFilterDeclaration(targetNode) : string.Empty;
        var bounds = effectiveBounds ?? targetNode.GeometryBounds;
        var transform = targetNode.TotalTransform;

        return new SvgSceneResourcePayloadCacheKey(
            key,
            compilationRootKey,
            filterDeclaration,
            bounds,
            transform);
    }

    private static SvgSceneBoundsPayloadCacheKey CreateBoundsPayloadCacheKey(SvgSceneNode targetNode, SKRect? effectiveBounds = null)
    {
        var key = targetNode.ElementAddressKey
                  ?? targetNode.ElementId
                  ?? targetNode.ElementTypeName;
        var compilationRootKey = targetNode.CompilationRootKey ?? string.Empty;
        return new SvgSceneBoundsPayloadCacheKey(key, compilationRootKey, effectiveBounds ?? targetNode.GeometryBounds);
    }

    private static SvgSceneSharedFilterPayloadCacheKey CreateSharedFilterPayloadCacheKey(string filterDeclaration, SKRect bounds)
        => new(filterDeclaration, bounds);

    private static string GetOwnFilterDeclaration(SvgSceneNode targetNode)
        => targetNode.Element is SvgVisualElement visualElement &&
           visualElement.TryGetOwnCascadedStyleValue("filter", out var value)
            ? value
            : string.Empty;

    private static bool IsSingleUrlFilterDeclaration(string value)
    {
        var index = 0;
        SkipWhitespace(value, ref index);
        if (value.Length - index < 4 ||
            !value.AsSpan(index, 4).Equals("url(".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var start = index + 4;
        var current = start;
        var quote = '\0';
        while (current < value.Length)
        {
            var ch = value[current];
            if (quote != '\0')
            {
                if (ch == quote)
                {
                    quote = '\0';
                }

                current++;
                continue;
            }

            if (ch == '"' || ch == '\'')
            {
                quote = ch;
                current++;
                continue;
            }

            if (ch == ')')
            {
                var inner = value.Substring(start, current - start).Trim();
                if (inner.Length >= 2 &&
                    ((inner[0] == '"' && inner[inner.Length - 1] == '"') ||
                     (inner[0] == '\'' && inner[inner.Length - 1] == '\'')))
                {
                    inner = inner.Substring(1, inner.Length - 2).Trim();
                }

                if (inner.Length <= 0 || !Uri.TryCreate(inner, UriKind.RelativeOrAbsolute, out _))
                {
                    return false;
                }

                current++;
                SkipWhitespace(value, ref current);
                return current == value.Length;
            }

            current++;
        }

        return false;
    }

    private static void SkipWhitespace(string value, ref int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }
    }

    private static SKPaint CreateMaskPaint()
    {
        return new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.StrokeAndFill
        };
    }

    private static SKPaint CreateMaskDstInPaint(MaskType maskType)
    {
        return new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.StrokeAndFill,
            BlendMode = SKBlendMode.DstIn,
            Color = FilterEffectsService.s_transparentBlack,
            ColorFilter = maskType == MaskType.Alpha ? null : SKColorFilter.CreateLumaColor()
        };
    }

    private static SvgSceneMaskPayload CreateEmptyMaskPayload(SvgMask svgMask)
    {
        var maskNode = new SvgSceneNode(
            SvgSceneNodeKind.Mask,
            svgMask,
            SvgSceneCompiler.TryGetElementAddressKey(svgMask),
            SvgSceneCompiler.GetElementTypeName(svgMask),
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

        var maskType = GetMaskType(svgMask);

        SvgSceneCompiler.AssignRetainedVisualState(maskNode, svgMask);
        SvgSceneCompiler.AssignRetainedResourceKeys(maskNode, svgMask);
        return new SvgSceneMaskPayload(maskNode, CreateMaskPaint(), CreateMaskDstInPaint(maskType));
    }

    private static MaskType GetMaskType(SvgMask mask)
    {
        if (mask.TryGetOwnCascadedStyleValue("mask-type", out var maskTypeStr) &&
            !string.IsNullOrWhiteSpace(maskTypeStr))
        {
            return string.Equals(maskTypeStr.Trim(), "alpha", StringComparison.OrdinalIgnoreCase)
                ? MaskType.Alpha
                : MaskType.Luminance;
        }

        return MaskType.Luminance;
    }

    private static bool HasClipGeometry(ClipPath? clipPath)
    {
        if (clipPath is null)
        {
            return false;
        }

        if (clipPath.Clips is { Count: > 0 })
        {
            return true;
        }

        return HasClipGeometry(clipPath.Clip);
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

internal readonly struct SvgSceneResourcePayloadCacheKey : IEquatable<SvgSceneResourcePayloadCacheKey>
{
    private readonly string? _nodeKey;
    private readonly string? _compilationRootKey;
    private readonly string? _filterDeclaration;
    private readonly SKRect _bounds;
    private readonly SKMatrix _transform;

    public SvgSceneResourcePayloadCacheKey(
        string? nodeKey,
        string? compilationRootKey,
        string? filterDeclaration,
        SKRect bounds,
        SKMatrix transform)
    {
        _nodeKey = nodeKey;
        _compilationRootKey = compilationRootKey;
        _filterDeclaration = filterDeclaration;
        _bounds = bounds;
        _transform = transform;
    }

    public bool Equals(SvgSceneResourcePayloadCacheKey other)
    {
        return string.Equals(_nodeKey, other._nodeKey, StringComparison.Ordinal) &&
               string.Equals(_compilationRootKey, other._compilationRootKey, StringComparison.Ordinal) &&
               string.Equals(_filterDeclaration, other._filterDeclaration, StringComparison.Ordinal) &&
               RectEquals(_bounds, other._bounds) &&
               MatrixEquals(_transform, other._transform);
    }

    public override bool Equals(object? obj)
        => obj is SvgSceneResourcePayloadCacheKey other && Equals(other);

    public override int GetHashCode()
    {
        var hash = SvgSceneResourceHash.CombineString(SvgSceneResourceHash.Seed, _nodeKey);
        hash = SvgSceneResourceHash.CombineString(hash, _compilationRootKey);
        hash = SvgSceneResourceHash.CombineString(hash, _filterDeclaration);
        hash = SvgSceneResourceHash.Combine(hash, _bounds.Left);
        hash = SvgSceneResourceHash.Combine(hash, _bounds.Top);
        hash = SvgSceneResourceHash.Combine(hash, _bounds.Right);
        hash = SvgSceneResourceHash.Combine(hash, _bounds.Bottom);
        hash = SvgSceneResourceHash.Combine(hash, _transform.ScaleX);
        hash = SvgSceneResourceHash.Combine(hash, _transform.SkewX);
        hash = SvgSceneResourceHash.Combine(hash, _transform.TransX);
        hash = SvgSceneResourceHash.Combine(hash, _transform.SkewY);
        hash = SvgSceneResourceHash.Combine(hash, _transform.ScaleY);
        hash = SvgSceneResourceHash.Combine(hash, _transform.TransY);
        hash = SvgSceneResourceHash.Combine(hash, _transform.Persp0);
        hash = SvgSceneResourceHash.Combine(hash, _transform.Persp1);
        return SvgSceneResourceHash.Combine(hash, _transform.Persp2);
    }

    private static bool RectEquals(SKRect left, SKRect right)
    {
        return left.Left == right.Left &&
               left.Top == right.Top &&
               left.Right == right.Right &&
               left.Bottom == right.Bottom;
    }

    private static bool MatrixEquals(SKMatrix left, SKMatrix right)
    {
        return left.ScaleX == right.ScaleX &&
               left.SkewX == right.SkewX &&
               left.TransX == right.TransX &&
               left.SkewY == right.SkewY &&
               left.ScaleY == right.ScaleY &&
               left.TransY == right.TransY &&
               left.Persp0 == right.Persp0 &&
               left.Persp1 == right.Persp1 &&
               left.Persp2 == right.Persp2;
    }
}

internal static class SvgSceneResourceHash
{
    public const int Seed = 17;

    public static int CombineString(int hash, string? value)
        => Combine(hash, value is null ? 0 : StringComparer.Ordinal.GetHashCode(value));

    public static int Combine<T>(int hash, T value)
        => Combine(hash, EqualityComparer<T>.Default.GetHashCode(value!));

    private static int Combine(int hash, int value)
        => unchecked((hash * 397) ^ value);
}

internal readonly struct SvgSceneBoundsPayloadCacheKey : IEquatable<SvgSceneBoundsPayloadCacheKey>
{
    private readonly string? _nodeKey;
    private readonly string? _compilationRootKey;
    private readonly SKRect _bounds;

    public SvgSceneBoundsPayloadCacheKey(string? nodeKey, string? compilationRootKey, SKRect bounds)
    {
        _nodeKey = nodeKey;
        _compilationRootKey = compilationRootKey;
        _bounds = bounds;
    }

    public bool Equals(SvgSceneBoundsPayloadCacheKey other)
    {
        return string.Equals(_nodeKey, other._nodeKey, StringComparison.Ordinal) &&
               string.Equals(_compilationRootKey, other._compilationRootKey, StringComparison.Ordinal) &&
               RectEquals(_bounds, other._bounds);
    }

    public override bool Equals(object? obj)
        => obj is SvgSceneBoundsPayloadCacheKey other && Equals(other);

    public override int GetHashCode()
    {
        var hash = SvgSceneResourceHash.CombineString(SvgSceneResourceHash.Seed, _nodeKey);
        hash = SvgSceneResourceHash.CombineString(hash, _compilationRootKey);
        hash = SvgSceneResourceHash.Combine(hash, _bounds.Left);
        hash = SvgSceneResourceHash.Combine(hash, _bounds.Top);
        hash = SvgSceneResourceHash.Combine(hash, _bounds.Right);
        return SvgSceneResourceHash.Combine(hash, _bounds.Bottom);
    }

    private static bool RectEquals(SKRect left, SKRect right)
    {
        return left.Left == right.Left &&
               left.Top == right.Top &&
               left.Right == right.Right &&
               left.Bottom == right.Bottom;
    }
}

internal readonly struct SvgSceneSharedFilterPayloadCacheKey : IEquatable<SvgSceneSharedFilterPayloadCacheKey>
{
    private readonly string? _filterDeclaration;
    private readonly SKRect _bounds;

    public SvgSceneSharedFilterPayloadCacheKey(string? filterDeclaration, SKRect bounds)
    {
        _filterDeclaration = filterDeclaration;
        _bounds = bounds;
    }

    public bool Equals(SvgSceneSharedFilterPayloadCacheKey other)
    {
        return string.Equals(_filterDeclaration, other._filterDeclaration, StringComparison.Ordinal) &&
               RectEquals(_bounds, other._bounds);
    }

    public override bool Equals(object? obj)
        => obj is SvgSceneSharedFilterPayloadCacheKey other && Equals(other);

    public override int GetHashCode()
    {
        var hash = SvgSceneResourceHash.CombineString(SvgSceneResourceHash.Seed, _filterDeclaration);
        hash = SvgSceneResourceHash.Combine(hash, _bounds.Left);
        hash = SvgSceneResourceHash.Combine(hash, _bounds.Top);
        hash = SvgSceneResourceHash.Combine(hash, _bounds.Right);
        return SvgSceneResourceHash.Combine(hash, _bounds.Bottom);
    }

    private static bool RectEquals(SKRect left, SKRect right)
    {
        return left.Left == right.Left &&
               left.Top == right.Top &&
               left.Right == right.Right &&
               left.Bottom == right.Bottom;
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
    public SvgSceneFilterPayload(
        SKPaint? filterPaint,
        SKRect? filterClip,
        bool isValid,
        bool usesGlobalLayer = false,
        SKRect? globalClip = null)
    {
        FilterPaint = filterPaint;
        FilterClip = filterClip;
        IsValid = isValid;
        UsesGlobalLayer = usesGlobalLayer;
        GlobalClip = globalClip;
    }

    public static SvgSceneFilterPayload Invalid(SKRect? filterClip)
        => new(null, filterClip, isValid: false);

    public SKPaint? FilterPaint { get; }

    public SKRect? FilterClip { get; }

    public bool IsValid { get; }

    public bool UsesGlobalLayer { get; }

    public SKRect? GlobalClip { get; }
}
