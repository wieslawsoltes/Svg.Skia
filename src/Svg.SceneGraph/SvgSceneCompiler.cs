using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using ShimSkiaSharp;
using Svg;
using Svg.DataTypes;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

public static class SvgSceneCompiler
{
    private const int MaxInitialAddressKeyCacheCapacity = 16 * 1024;

    private sealed class SvgSceneCompileContext
    {
        private const SvgCascadedStyleFeatureFlags AllCascadedStyleFeatureFlags =
            SvgCascadedStyleFeatureFlags.MarkerReference |
            SvgCascadedStyleFeatureFlags.MixBlendMode |
            SvgCascadedStyleFeatureFlags.Isolation |
            SvgCascadedStyleFeatureFlags.ClipPath |
            SvgCascadedStyleFeatureFlags.Mask |
            SvgCascadedStyleFeatureFlags.Filter;

        private string? _activeDocumentKey;
        private HashSet<string>? _activeDocumentKeys;
        private readonly SvgElementAddressKeyCache _addressKeys;
        private readonly Dictionary<SvgScenePaintingService.SolidFillPaintCacheKey, SKPaint> _solidFillPaintCache = new();
        private SvgScenePaintingService.GradientPaintCache? _gradientPaintCache;
        private Dictionary<SvgFragment, SKSize>? _fragmentViewportSizeOverrides;
        private Dictionary<SvgDocument, bool>? _markerReferenceDeclarationsByDocument;
        private Dictionary<ReferenceCacheKey, SvgElement?>? _resolvedReferenceCache;
        private readonly Stack<MarkerReferenceState> _markerReferenceDocumentStack = new();
        private SvgDocument? _activeMarkerReferenceDocument;
        private SvgCascadedStyleFeatureFlags _activeDocumentCascadedStyleFeatureFlags = AllCascadedStyleFeatureFlags;
        private bool _activeMarkerReferenceDeclarationCandidate;
        private bool _activeDocumentMayContainMarkerReferenceDeclarations;

        public SvgSceneCompileContext(int initialAddressCapacity = 0)
        {
            _addressKeys = new SvgElementAddressKeyCache(initialAddressCapacity);
        }

        public SvgSceneContextPaint? ContextPaint { get; private set; }

        public SvgScenePaintingService.GradientPaintCache GradientPaintCache => _gradientPaintCache ??= new();

        public bool ActiveMarkerReferenceDeclarationCandidate => _activeMarkerReferenceDeclarationCandidate;

        public SvgCascadedStyleFeatureFlags ActiveDocumentCascadedStyleFeatureFlags =>
            _activeDocumentCascadedStyleFeatureFlags;

        public bool ActiveDocumentMayContainMarkerReferenceDeclarations => _activeDocumentMayContainMarkerReferenceDeclarations;

        public bool ActiveDocumentMayContainClipPathDeclarations =>
            HasFeatureFlag(_activeDocumentCascadedStyleFeatureFlags, SvgCascadedStyleFeatureFlags.ClipPath);

        public bool ActiveDocumentMayContainMaskDeclarations =>
            HasFeatureFlag(_activeDocumentCascadedStyleFeatureFlags, SvgCascadedStyleFeatureFlags.Mask);

        public bool ActiveDocumentMayContainFilterDeclarations =>
            HasFeatureFlag(_activeDocumentCascadedStyleFeatureFlags, SvgCascadedStyleFeatureFlags.Filter);

        public IDisposable PushContextPaint(SvgVisualElement contextPaintElement, SKRect contextPaintBounds)
        {
            var previousContextPaint = ContextPaint;
            ContextPaint = new SvgSceneContextPaint(contextPaintElement, contextPaintBounds, previousContextPaint);

            return new ContextPaintScope(
                this,
                previousContextPaint);
        }

        public IDisposable PushFragmentViewportSizeOverride(SvgFragment svgFragment, SKSize viewportSize)
        {
            var fragmentViewportSizeOverrides = _fragmentViewportSizeOverrides ??= new Dictionary<SvgFragment, SKSize>();
            var hadPreviousOverride = fragmentViewportSizeOverrides.TryGetValue(svgFragment, out var previousViewportSize);
            fragmentViewportSizeOverrides[svgFragment] = viewportSize;

            return new FragmentViewportSizeOverrideScope(
                this,
                svgFragment,
                hadPreviousOverride,
                previousViewportSize);
        }

        public MarkerReferenceDeclarationScope PushMarkerReferenceDeclarationScope(SvgCascadedStyleFeatureFlags ownFeatureFlags)
        {
            var previousMarkerReferenceDeclarationCandidate = _activeMarkerReferenceDeclarationCandidate;
            var hasOwnMarkerReferenceDeclaration = !previousMarkerReferenceDeclarationCandidate &&
                                                   HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.MarkerReference);
            _activeMarkerReferenceDeclarationCandidate = previousMarkerReferenceDeclarationCandidate || hasOwnMarkerReferenceDeclaration;
            _activeDocumentMayContainMarkerReferenceDeclarations |= hasOwnMarkerReferenceDeclaration;
            return new MarkerReferenceDeclarationScope(this, previousMarkerReferenceDeclarationCandidate);
        }

        public bool TryGetFragmentViewportSizeOverride(SvgFragment svgFragment, out SKSize viewportSize)
        {
            if (_fragmentViewportSizeOverrides is null)
            {
                viewportSize = default;
                return false;
            }

            return _fragmentViewportSizeOverrides.TryGetValue(svgFragment, out viewportSize);
        }

        public bool TryEnter(SvgDocument? document, out string? documentKey)
        {
            documentKey = GetDocumentKey(document);
            if (documentKey is not null && !TryEnterDocumentKey(documentKey))
            {
                return false;
            }

            _markerReferenceDocumentStack.Push(new MarkerReferenceState(
                _activeMarkerReferenceDocument,
                _activeDocumentCascadedStyleFeatureFlags,
                _activeMarkerReferenceDeclarationCandidate,
                _activeDocumentMayContainMarkerReferenceDeclarations));
            _activeMarkerReferenceDocument = document;
            _activeDocumentCascadedStyleFeatureFlags = document is null
                ? AllCascadedStyleFeatureFlags
                : document.GetCascadedStyleFeatureFlags(AllCascadedStyleFeatureFlags);
            _activeMarkerReferenceDeclarationCandidate = false;
            _activeDocumentMayContainMarkerReferenceDeclarations = false;
            return true;
        }

        public bool IsActive(SvgDocument? document)
        {
            var documentKey = GetDocumentKey(document);
            if (documentKey is null)
            {
                return false;
            }

            return _activeDocumentKeys is null
                ? string.Equals(_activeDocumentKey, documentKey, StringComparison.Ordinal)
                : _activeDocumentKeys.Contains(documentKey);
        }

        public void Exit(string? documentKey)
        {
            if (documentKey is not null)
            {
                ExitDocumentKey(documentKey);
            }

            if (_activeMarkerReferenceDocument is not null &&
                (_activeDocumentMayContainMarkerReferenceDeclarations || _markerReferenceDeclarationsByDocument is not null))
            {
                (_markerReferenceDeclarationsByDocument ??= new Dictionary<SvgDocument, bool>())[_activeMarkerReferenceDocument] = _activeDocumentMayContainMarkerReferenceDeclarations;
            }

            if (_markerReferenceDocumentStack.Count == 0)
            {
                _activeMarkerReferenceDocument = null;
                _activeDocumentCascadedStyleFeatureFlags = AllCascadedStyleFeatureFlags;
                _activeMarkerReferenceDeclarationCandidate = false;
                _activeDocumentMayContainMarkerReferenceDeclarations = false;
                return;
            }

            var previous = _markerReferenceDocumentStack.Pop();
            _activeMarkerReferenceDocument = previous.Document;
            _activeDocumentCascadedStyleFeatureFlags = previous.CascadedStyleFeatureFlags;
            _activeMarkerReferenceDeclarationCandidate = previous.MarkerReferenceDeclarationCandidate;
            _activeDocumentMayContainMarkerReferenceDeclarations = previous.MayContainMarkerReferenceDeclarations;
        }

        private bool TryEnterDocumentKey(string documentKey)
        {
            if (_activeDocumentKeys is not null)
            {
                return _activeDocumentKeys.Add(documentKey);
            }

            if (_activeDocumentKey is null)
            {
                _activeDocumentKey = documentKey;
                return true;
            }

            if (string.Equals(_activeDocumentKey, documentKey, StringComparison.Ordinal))
            {
                return false;
            }

            _activeDocumentKeys = new HashSet<string>(StringComparer.Ordinal)
            {
                _activeDocumentKey,
                documentKey
            };
            _activeDocumentKey = null;
            return true;
        }

        private void ExitDocumentKey(string documentKey)
        {
            if (_activeDocumentKeys is not null)
            {
                _activeDocumentKeys.Remove(documentKey);
                return;
            }

            if (string.Equals(_activeDocumentKey, documentKey, StringComparison.Ordinal))
            {
                _activeDocumentKey = null;
            }
        }

        public string? GetElementAddressKey(SvgElement? element)
        {
            return _addressKeys.GetOrCreate(element);
        }

        public string? GetChildElementAddressKey(SvgElement parent, int childIndex)
        {
            return _addressKeys.GetOrCreateChild(parent, childIndex);
        }

        public string? GetClipResourceKey(SvgElement? element)
        {
            if (element is null ||
                !IsClipPathApplicableElement(element))
            {
                return null;
            }

            return GetResourceKey<SvgClipPath>(element, GetClipPathReferenceUri(element));
        }

        public string? GetMaskResourceKey(SvgElement element)
            => GetResourceKey<SvgMask>(element, GetReferenceUri(element, "mask"));

        public string? GetFilterResourceKey(SvgVisualElement visualElement)
            => GetResourceKey<Svg.FilterEffects.SvgFilter>(visualElement, SvgSceneFilterContext.GetFilterReferenceUri(visualElement));

        private string? GetResourceKey<T>(SvgElement owner, Uri? uri)
            where T : SvgElement
        {
            var resolvedElement = ResolveReference<T>(owner, uri);
            return resolvedElement is not null &&
                   resolvedElement.PassesConditionalProcessing(DrawAttributes.None)
                ? GetElementAddressKey(resolvedElement)
                : null;
        }

        private T? ResolveReference<T>(SvgElement owner, Uri? uri)
            where T : SvgElement
        {
            if (!TryCreateReferenceCacheKey(owner, uri, out var key))
            {
                return SvgService.GetReference<T>(owner, uri);
            }

            var resolvedReferenceCache = _resolvedReferenceCache ??= new Dictionary<ReferenceCacheKey, SvgElement?>();
            if (!resolvedReferenceCache.TryGetValue(key, out var resolvedElement))
            {
                resolvedElement = SvgService.GetReference<SvgElement>(owner, uri);
                resolvedReferenceCache.Add(key, resolvedElement);
            }

            return resolvedElement as T;
        }

        public bool TryGetCachedSolidFillPaint(
            SvgVisualElement visualElement,
            DrawAttributes ignoreAttributes,
            out SKPaint? paint)
        {
            paint = null;

            if (!SvgScenePaintingService.TryCreateSolidFillPaintCacheKey(visualElement, ignoreAttributes, out var key))
            {
                return false;
            }

            if (!_solidFillPaintCache.TryGetValue(key, out var cachedPaint))
            {
                cachedPaint = SvgScenePaintingService.CreateSolidFillPaint(key);
                _solidFillPaintCache[key] = cachedPaint;
            }

            paint = cachedPaint;
            return true;
        }

        public bool MayContainMarkerReferenceDeclarations(SvgElement element)
        {
            var document = element as SvgDocument ?? element.OwnerDocument;
            if (document is null)
            {
                return SubtreeMayContainMarkerReferenceDeclarations(element);
            }

            var markerReferenceDeclarationsByDocument = _markerReferenceDeclarationsByDocument;
            if (markerReferenceDeclarationsByDocument is null ||
                !markerReferenceDeclarationsByDocument.TryGetValue(document, out var mayContainMarkerReferences))
            {
                mayContainMarkerReferences = SubtreeMayContainMarkerReferenceDeclarations(document);
                (_markerReferenceDeclarationsByDocument ??= new Dictionary<SvgDocument, bool>())[document] = mayContainMarkerReferences;
            }

            return mayContainMarkerReferences;
        }

        private static string? GetDocumentKey(SvgDocument? document)
        {
            if (document is null)
            {
                return null;
            }

            if (document.BaseUri is { } baseUri)
            {
                return SvgService.GetImageDocumentUri(baseUri).AbsoluteUri;
            }

            return "instance:" + RuntimeHelpers.GetHashCode(document).ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryCreateReferenceCacheKey(
            SvgElement owner,
            Uri? uri,
            out ReferenceCacheKey key)
        {
            key = default;
            if (uri is null)
            {
                return false;
            }

            var document = owner as SvgDocument ?? owner.OwnerDocument;
            if (document is null)
            {
                return false;
            }

            key = new ReferenceCacheKey(
                document,
                uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.OriginalString);
            return true;
        }

        private readonly struct MarkerReferenceState
        {
            public MarkerReferenceState(
                SvgDocument? document,
                SvgCascadedStyleFeatureFlags cascadedStyleFeatureFlags,
                bool markerReferenceDeclarationCandidate,
                bool mayContainMarkerReferenceDeclarations)
            {
                Document = document;
                CascadedStyleFeatureFlags = cascadedStyleFeatureFlags;
                MarkerReferenceDeclarationCandidate = markerReferenceDeclarationCandidate;
                MayContainMarkerReferenceDeclarations = mayContainMarkerReferenceDeclarations;
            }

            public SvgDocument? Document { get; }

            public SvgCascadedStyleFeatureFlags CascadedStyleFeatureFlags { get; }

            public bool MarkerReferenceDeclarationCandidate { get; }

            public bool MayContainMarkerReferenceDeclarations { get; }
        }

        private readonly record struct ReferenceCacheKey(SvgDocument Document, string Uri);

        private sealed class ContextPaintScope : IDisposable
        {
            private readonly SvgSceneCompileContext _compileContext;
            private readonly SvgSceneContextPaint? _previousContextPaint;

            public ContextPaintScope(
                SvgSceneCompileContext compileContext,
                SvgSceneContextPaint? previousContextPaint)
            {
                _compileContext = compileContext;
                _previousContextPaint = previousContextPaint;
            }

            public void Dispose()
            {
                _compileContext.ContextPaint = _previousContextPaint;
            }
        }

        private sealed class FragmentViewportSizeOverrideScope : IDisposable
        {
            private readonly SvgSceneCompileContext _compileContext;
            private readonly SvgFragment _svgFragment;
            private readonly bool _hadPreviousOverride;
            private readonly SKSize _previousViewportSize;

            public FragmentViewportSizeOverrideScope(
                SvgSceneCompileContext compileContext,
                SvgFragment svgFragment,
                bool hadPreviousOverride,
                SKSize previousViewportSize)
            {
                _compileContext = compileContext;
                _svgFragment = svgFragment;
                _hadPreviousOverride = hadPreviousOverride;
                _previousViewportSize = previousViewportSize;
            }

            public void Dispose()
            {
                if (_hadPreviousOverride)
                {
                    _compileContext._fragmentViewportSizeOverrides![_svgFragment] = _previousViewportSize;
                }
                else
                {
                    _compileContext._fragmentViewportSizeOverrides!.Remove(_svgFragment);
                }
            }
        }

        public readonly struct MarkerReferenceDeclarationScope : IDisposable
        {
            private readonly SvgSceneCompileContext? _compileContext;
            private readonly bool _previousMarkerReferenceDeclarationCandidate;

            public MarkerReferenceDeclarationScope(
                SvgSceneCompileContext compileContext,
                bool previousMarkerReferenceDeclarationCandidate)
            {
                _compileContext = compileContext;
                _previousMarkerReferenceDeclarationCandidate = previousMarkerReferenceDeclarationCandidate;
            }

            public void Dispose()
            {
                if (_compileContext is not null)
                {
                    _compileContext._activeMarkerReferenceDeclarationCandidate = _previousMarkerReferenceDeclarationCandidate;
                }
            }
        }

    }

    public static bool TryCompile(
        SvgDocument? sourceDocument,
        SKRect cullRect,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        out SvgSceneDocument? sceneDocument)
    {
        return TryCompile(
            sourceDocument,
            cullRect,
            assetLoader,
            ignoreAttributes,
            CreateCompileContext(sourceDocument),
            out sceneDocument);
    }

    public static bool TryMeasureTextBounds(
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out SKRect geometryBounds)
    {
        if (svgTextBase is null)
        {
            throw new ArgumentNullException(nameof(svgTextBase));
        }

        if (assetLoader is null)
        {
            throw new ArgumentNullException(nameof(assetLoader));
        }

        return SvgSceneTextCompiler.TryMeasureGeometryBounds(svgTextBase, viewport, assetLoader, out geometryBounds);
    }

    private static bool TryCompile(
        SvgDocument? sourceDocument,
        SKRect cullRect,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        SvgSceneCompileContext compileContext,
        out SvgSceneDocument? sceneDocument)
    {
        sceneDocument = null;
        using var documentFontScope = PushDocumentFonts(sourceDocument, assetLoader);

        if (!TryCompileNodeTree(
                sourceDocument,
                cullRect,
                assetLoader,
                ignoreAttributes,
                compileContext,
                out var rootNode,
                out var effectiveCullRect,
                out var viewport))
        {
            return false;
        }

        sceneDocument = new SvgSceneDocument(
            sourceDocument,
            effectiveCullRect,
            viewport,
            rootNode!,
            assetLoader,
            ignoreAttributes,
            sourceDocument is null
                ? false
                : compileContext.MayContainMarkerReferenceDeclarations(sourceDocument));
        return true;
    }

    internal static bool TryCompileNodeTree(
        SvgDocument? sourceDocument,
        SKRect cullRect,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        out SvgSceneNode? rootNode,
        out SKRect effectiveCullRect,
        out SKRect viewport)
    {
        using var documentFontScope = PushDocumentFonts(sourceDocument, assetLoader);
        return TryCompileNodeTree(
            sourceDocument,
            cullRect,
            assetLoader,
            ignoreAttributes,
            CreateCompileContext(sourceDocument),
            out rootNode,
            out effectiveCullRect,
            out viewport);
    }

    private static bool TryCompileNodeTree(
        SvgDocument? sourceDocument,
        SKRect cullRect,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        SvgSceneCompileContext compileContext,
        out SvgSceneNode? rootNode,
        out SKRect effectiveCullRect,
        out SKRect viewport)
    {
        rootNode = null;
        effectiveCullRect = SKRect.Empty;
        viewport = SKRect.Empty;

        if (sourceDocument is null)
        {
            return false;
        }

        viewport = cullRect;
        if (viewport.IsEmpty)
        {
            viewport = SKRect.Create(SvgService.GetDimensions(sourceDocument));
        }

        if (viewport.IsEmpty)
        {
            return false;
        }

        if (!compileContext.TryEnter(sourceDocument, out var documentKey))
        {
            return false;
        }

        try
        {
            rootNode = CompileElementNode(
                sourceDocument,
                viewport,
                SKMatrix.Identity,
                assetLoader,
                ignoreAttributes,
                compilationRootKey: null,
                createOwnCompilationRootBoundary: false,
                compileContext);

            if (rootNode is null)
            {
                return false;
            }

            effectiveCullRect = GetEffectiveDocumentCullRect(cullRect, rootNode);
            return true;
        }
        finally
        {
            compileContext.Exit(documentKey);
        }
    }

    internal static bool TryCompileFragment(
        SvgFragment? sourceFragment,
        SKRect cullRect,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        out SvgSceneDocument? sceneDocument)
    {
        using var documentFontScope = PushDocumentFonts(sourceFragment as SvgDocument ?? sourceFragment?.OwnerDocument, assetLoader);
        return TryCompileFragment(
            sourceFragment,
            cullRect,
            viewport,
            assetLoader,
            ignoreAttributes,
            CreateCompileContext(sourceFragment),
            out sceneDocument);
    }

    private static IDisposable? PushDocumentFonts(SvgDocument? document, ISvgAssetLoader assetLoader)
    {
        if (document is not null &&
            assetLoader is ISvgDocumentFontLoader fontLoader)
        {
            return fontLoader.PushDocumentFonts(document);
        }

        return null;
    }

    private static SvgSceneCompileContext CreateCompileContext(SvgElement? root)
    {
        return new SvgSceneCompileContext(EstimateInitialAddressKeyCapacity(root));
    }

    private static int EstimateInitialAddressKeyCapacity(SvgElement? root)
    {
        if (root is null)
        {
            return 0;
        }

        var capacity = root is SvgDocument ? 0 : 1;
        var children = root.Children;
        capacity = AddInitialAddressKeyCapacity(capacity, children.Count);
        for (var i = 0; i < children.Count; i++)
        {
            capacity = AddInitialAddressKeyCapacity(capacity, children[i].Children.Count);
        }

        return capacity;
    }

    private static int AddInitialAddressKeyCapacity(int capacity, int addition)
    {
        if (addition <= 0 || capacity >= MaxInitialAddressKeyCacheCapacity)
        {
            return capacity;
        }

        var next = capacity + addition;
        return next > MaxInitialAddressKeyCacheCapacity || next < capacity
            ? MaxInitialAddressKeyCacheCapacity
            : next;
    }

    private static bool TryCompileFragment(
        SvgFragment? sourceFragment,
        SKRect cullRect,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        SvgSceneCompileContext compileContext,
        out SvgSceneDocument? sceneDocument)
    {
        sceneDocument = null;

        if (sourceFragment is null)
        {
            return false;
        }

        var compilationDocument = sourceFragment as SvgDocument ?? sourceFragment.OwnerDocument;
        if (!compileContext.TryEnter(compilationDocument, out var documentKey))
        {
            return false;
        }

        try
        {
            var rootNode = CompileElementNode(
                sourceFragment,
                viewport,
                SKMatrix.Identity,
                assetLoader,
                ignoreAttributes,
                compilationRootKey: null,
                createOwnCompilationRootBoundary: false,
                compileContext);

            if (rootNode is null)
            {
                return false;
            }

            sceneDocument = new SvgSceneDocument(
                sourceFragment as SvgDocument ?? sourceFragment.OwnerDocument,
                GetEffectiveDocumentCullRect(cullRect, rootNode),
                viewport,
                rootNode,
                assetLoader,
                ignoreAttributes,
                compileContext.ActiveDocumentMayContainMarkerReferenceDeclarations);
            return true;
        }
        finally
        {
            compileContext.Exit(documentKey);
        }
    }

    internal static SvgSceneMutationResult ApplyMutation(
        SvgSceneDocument sceneDocument,
        SvgElement element,
        IReadOnlyCollection<string>? changedAttributes = null)
    {
        var addressKey = new SvgElementAddressKeyCache().GetOrCreate(element);
        if (string.IsNullOrWhiteSpace(addressKey))
        {
            return new SvgSceneMutationResult(true, 0, 0);
        }

        var resources = sceneDocument.GetResourcesForAddress(addressKey!);
        if (RequiresRootSceneRebuild(sceneDocument, element))
        {
            return new SvgSceneMutationResult(false, 0, resources.Count);
        }

        var compilationRootKeys = PruneCompilationRoots(sceneDocument, sceneDocument.GetCompilationRootsForMutation(addressKey!));
        if (compilationRootKeys.Count == 0)
        {
            sceneDocument.RebuildIndexesAndDependencies();
            return new SvgSceneMutationResult(true, 0, resources.Count);
        }

        var compileContext = new SvgSceneCompileContext();
        if (!compileContext.TryEnter(sceneDocument.SourceDocument, out var documentKey))
        {
            return new SvgSceneMutationResult(false, 0, resources.Count);
        }

        try
        {
            for (var i = 0; i < compilationRootKeys.Count; i++)
            {
                var compilationRootKey = compilationRootKeys[i];
                if (!sceneDocument.TryGetCompilationRoot(compilationRootKey, out var currentNode) ||
                    currentNode is null ||
                    !sceneDocument.TryResolveElement(compilationRootKey, out var currentElement) ||
                    currentElement is null)
                {
                    return new SvgSceneMutationResult(false, i, resources.Count);
                }

                var replacement = CompileElementNode(
                    currentElement,
                    sceneDocument.CompilationViewport,
                    currentNode.Parent?.TotalTransform ?? SKMatrix.Identity,
                    sceneDocument.AssetLoader,
                    sceneDocument.IgnoreAttributes,
                    compilationRootKey,
                    createOwnCompilationRootBoundary: true,
                    compileContext);

                if (replacement is null)
                {
                    return new SvgSceneMutationResult(false, i, resources.Count);
                }

                currentNode.ReplaceWith(replacement);
            }

            sceneDocument.RebuildIndexesAndDependencies();
            return new SvgSceneMutationResult(true, compilationRootKeys.Count, resources.Count);
        }
        finally
        {
            compileContext.Exit(documentKey);
        }
    }

    private static bool RequiresRootSceneRebuild(SvgSceneDocument sceneDocument, SvgElement element)
    {
        if (sceneDocument.Root.Element is null)
        {
            return false;
        }

        return ReferenceEquals(sceneDocument.Root.Element, element);
    }

    internal static bool TryGetResourceKind(SvgElement element, out SvgSceneResourceKind kind)
    {
        kind = element switch
        {
            SvgClipPath => SvgSceneResourceKind.ClipPath,
            SvgMask => SvgSceneResourceKind.Mask,
            Svg.FilterEffects.SvgFilter => SvgSceneResourceKind.Filter,
            SvgGradientServer => SvgSceneResourceKind.Gradient,
            SvgPatternServer => SvgSceneResourceKind.Pattern,
            SvgMarker => SvgSceneResourceKind.Marker,
            SvgSymbol => SvgSceneResourceKind.Symbol,
            SvgPaintServer => SvgSceneResourceKind.PaintServer,
            _ => SvgSceneResourceKind.Unknown
        };

        return kind != SvgSceneResourceKind.Unknown;
    }

    internal static IEnumerable<SvgElement> EnumerateReferencedElements(SvgElement element, Func<SvgElement?, string?>? getElementAddressKey = null)
    {
        var results = new List<SvgElement>();
        VisitReferencedElements(element, static (dependencyElement, _, state) => state!.Add(dependencyElement), getElementAddressKey, results);
        return results;
    }

    internal static void VisitReferencedElements<TState>(
        SvgElement element,
        Action<SvgElement, string, TState> visitor,
        Func<SvgElement?, string?>? getElementAddressKey = null,
        TState state = default!)
    {
        VisitReferencedElements(element, visitor, includeMarkerReferences: true, getElementAddressKey, state);
    }

    internal static void VisitReferencedElements<TState>(
        SvgElement element,
        Action<SvgElement, string, TState> visitor,
        bool includeMarkerReferences,
        Func<SvgElement?, string?>? getElementAddressKey = null,
        TState state = default!)
    {
        VisitReferencedElements(element, visitor, includeMarkerReferences, includeClipPathReferences: true, getElementAddressKey, state);
    }

    internal static void VisitReferencedElements<TState>(
        SvgElement element,
        Action<SvgElement, string, TState> visitor,
        bool includeMarkerReferences,
        bool includeClipPathReferences,
        Func<SvgElement?, string?>? getElementAddressKey = null,
        TState state = default!)
    {
        string? firstSeen = null;
        string? secondSeen = null;
        string? thirdSeen = null;
        HashSet<string>? seen = null;

        void Add(SvgElement? dependencyElement)
        {
            if (dependencyElement is null)
            {
                return;
            }

            var dependencyAddressKey = (getElementAddressKey ?? TryGetElementAddressKey)(dependencyElement) ?? dependencyElement.ID;
            if (string.IsNullOrWhiteSpace(dependencyAddressKey))
            {
                return;
            }

            if (!TryAddSeen(dependencyAddressKey!))
            {
                return;
            }

            visitor(dependencyElement, dependencyAddressKey!, state);
        }

        bool TryAddSeen(string dependencyAddressKey)
        {
            if (seen is not null)
            {
                return seen.Add(dependencyAddressKey);
            }

            if (firstSeen is null)
            {
                firstSeen = dependencyAddressKey;
                return true;
            }

            if (string.Equals(firstSeen, dependencyAddressKey, StringComparison.Ordinal))
            {
                return false;
            }

            if (secondSeen is null)
            {
                secondSeen = dependencyAddressKey;
                return true;
            }

            if (string.Equals(secondSeen, dependencyAddressKey, StringComparison.Ordinal))
            {
                return false;
            }

            if (thirdSeen is null)
            {
                thirdSeen = dependencyAddressKey;
                return true;
            }

            if (string.Equals(thirdSeen, dependencyAddressKey, StringComparison.Ordinal))
            {
                return false;
            }

            seen = new HashSet<string>(StringComparer.Ordinal)
            {
                firstSeen,
                secondSeen,
                thirdSeen
            };
            return seen.Add(dependencyAddressKey);
        }

        if (includeClipPathReferences && IsClipPathApplicableElement(element))
        {
            Add(ResolveReference(element, GetClipPathReferenceUri(element)));
        }

        if (element is SvgVisualElement visualElement)
        {
            foreach (var filterUri in SvgSceneFilterContext.GetFilterReferenceUris(visualElement))
            {
                Add(ResolveReference(visualElement, filterUri));
            }

            Add(ResolveReference(visualElement, GetReferenceUri(visualElement, "mask")));
            Add(GetResolvedPaintServerElement(visualElement, visualElement.Fill));
            Add(GetResolvedPaintServerElement(visualElement, visualElement.Stroke));
            if (includeMarkerReferences)
            {
                Add(ResolveReference(visualElement, GetEffectiveMarkerReferenceUri(visualElement, "marker-start", static element => element.MarkerStart)));
                Add(ResolveReference(visualElement, GetEffectiveMarkerReferenceUri(visualElement, "marker-mid", static element => element.MarkerMid)));
                Add(ResolveReference(visualElement, GetEffectiveMarkerReferenceUri(visualElement, "marker-end", static element => element.MarkerEnd)));
            }
        }

        if (element is SvgMask)
        {
            Add(ResolveReference(element, GetReferenceUri(element, "mask")));
        }

        if (includeMarkerReferences && element is SvgMarkerElement markerElement)
        {
            Add(ResolveReference(markerElement, GetComputedMarkerReferenceUri(markerElement, "marker-start")));
            Add(ResolveReference(markerElement, GetComputedMarkerReferenceUri(markerElement, "marker-mid")));
            Add(ResolveReference(markerElement, GetComputedMarkerReferenceUri(markerElement, "marker-end")));
        }

        if (element is SvgUse svgUse)
        {
            Add(ResolveReference(svgUse, SvgService.GetEffectiveReferenceUri(svgUse, svgUse.ReferencedElement)));
        }

        if (element is SvgGradientServer gradientServer)
        {
            Add(SvgDeferredPaintServer.TryGet<SvgGradientServer>(gradientServer.InheritGradient, gradientServer));
        }

        if (element is SvgPatternServer patternServer)
        {
            Add(SvgDeferredPaintServer.TryGet<SvgPatternServer>(patternServer.InheritGradient, patternServer));
        }

        if (element is Svg.FilterEffects.SvgFilter svgFilter)
        {
            Add(ResolveReference(svgFilter, SvgService.GetEffectiveReferenceUri(svgFilter, svgFilter.Href)));
        }

        if (element is Svg.FilterEffects.SvgImage filterImage)
        {
            Add(ResolveReference(filterImage, SvgService.GetEffectiveReferenceUri(filterImage, filterImage.Href)));
        }

        if (element is SvgTextRef textRef)
        {
            Add(ResolveReference(textRef, SvgService.GetEffectiveReferenceUri(textRef, textRef.ReferencedElement)));
        }

        if (element is SvgTextPath textPath && textPath.PathData is not { Count: > 0 })
        {
            Add(ResolveReference(textPath, SvgService.GetEffectiveReferenceUri(textPath, textPath.ReferencedPath)));
        }
    }

    internal static bool MayReferenceOtherElements(SvgElement element)
    {
        return MayReferenceOtherElements(element, includeMarkerReferences: true);
    }

    internal static bool MayReferenceOtherElements(SvgElement element, bool includeMarkerReferences)
    {
        return MayReferenceOtherElements(element, includeMarkerReferences, includeClipPathReferences: true);
    }

    internal static bool MayReferenceOtherElements(SvgElement element, bool includeMarkerReferences, bool includeClipPathReferences)
    {
        if (includeClipPathReferences &&
            IsClipPathApplicableElement(element) &&
            GetClipPathReferenceUri(element) is not null)
        {
            return true;
        }

        if (element is SvgVisualElement visualElement)
        {
            if (SvgSceneFilterContext.HasFilterReference(visualElement) ||
                HasMaskReference(element) ||
                HasPaintServerReference(element, visualElement.Fill) ||
                HasPaintServerReference(element, visualElement.Stroke) ||
                includeMarkerReferences && HasMarkerReference(visualElement))
            {
                return true;
            }
        }

        return element switch
        {
            SvgMarkerElement markerElement when includeMarkerReferences => GetComputedMarkerReferenceUri(markerElement, "marker-start") is not null ||
                                                                           GetComputedMarkerReferenceUri(markerElement, "marker-mid") is not null ||
                                                                           GetComputedMarkerReferenceUri(markerElement, "marker-end") is not null,
            SvgUse svgUse => SvgService.GetEffectiveReferenceUri(svgUse, svgUse.ReferencedElement) is not null,
            SvgGradientServer gradientServer => gradientServer.InheritGradient is not null,
            SvgPatternServer patternServer => patternServer.InheritGradient is not null,
            Svg.FilterEffects.SvgFilter svgFilter => SvgService.GetEffectiveReferenceUri(svgFilter, svgFilter.Href) is not null,
            Svg.FilterEffects.SvgImage filterImage => SvgService.GetEffectiveReferenceUri(filterImage, filterImage.Href) is not null,
            SvgMask svgMask => HasMaskReference(svgMask),
            SvgTextRef textRef => SvgService.GetEffectiveReferenceUri(textRef, textRef.ReferencedElement) is not null,
            SvgTextPath textPath => textPath.PathData is not { Count: > 0 } &&
                                    SvgService.GetEffectiveReferenceUri(textPath, textPath.ReferencedPath) is not null,
            _ => false
        };
    }

    internal static string? TryGetElementAddressKey(SvgElement? element)
    {
        if (element is null || element is SvgDocument)
        {
            return null;
        }

        var address = SvgElementAddress.Create(element);
        if (address.ChildIndexes.Length == 0)
        {
            return null;
        }

        for (var i = 0; i < address.ChildIndexes.Length; i++)
        {
            if (address.ChildIndexes[i] < 0)
            {
                return null;
            }
        }

        return address.Key;
    }

    private static SvgSceneNode? CompileElementNode(
        SvgElement element,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        string? compilationRootKey,
        bool createOwnCompilationRootBoundary,
        SvgSceneCompileContext compileContext)
    {
        SvgSceneNode? node = null;
        var requestedFeatureFlags = SvgCascadedStyleFeatureFlags.None;
        var ownFeatureFlags = SvgCascadedStyleFeatureFlags.None;
        if (compileContext.ActiveDocumentCascadedStyleFeatureFlags != SvgCascadedStyleFeatureFlags.None)
        {
            requestedFeatureFlags = GetRequestedCascadedStyleFeatureFlags(element);
            requestedFeatureFlags = FilterRequestedCascadedStyleFeatureFlags(compileContext, requestedFeatureFlags);
            if (requestedFeatureFlags != SvgCascadedStyleFeatureFlags.None)
            {
                ownFeatureFlags = element.GetOwnCascadedStyleFeatureFlags(requestedFeatureFlags);
            }
        }

        using var markerReferenceScope = HasFeatureFlag(requestedFeatureFlags, SvgCascadedStyleFeatureFlags.MarkerReference)
            ? compileContext.PushMarkerReferenceDeclarationScope(ownFeatureFlags)
            : default;

        if (TryCompileDirectElementNode(
                element,
                viewport,
                parentTotalTransform,
                assetLoader,
                ignoreAttributes,
                compilationRootKey,
                createOwnCompilationRootBoundary,
                ownFeatureFlags,
                compileContext,
                out var directNode))
        {
            node = directNode;
        }
        else if (TryCompileDirectStructuralNode(
                     element,
                     viewport,
                     parentTotalTransform,
                     ignoreAttributes,
                     compilationRootKey,
                     createOwnCompilationRootBoundary,
                     ownFeatureFlags,
                     compileContext,
                     out var structuralNode))
        {
            node = structuralNode;
        }
        else if (TryCompileDirectNonRenderingNode(
                     element,
                     parentTotalTransform,
                     compilationRootKey,
                     createOwnCompilationRootBoundary,
                     ownFeatureFlags,
                     compileContext,
                     out var nonRenderingNode))
        {
            node = nonRenderingNode;
        }
        else
        {
            return null;
        }

        if (node is null)
        {
            return null;
        }

        if (ShouldCompileDomChildren(element))
        {
            var childViewport = element is SvgFragment
                ? GetChildViewport(element, viewport, compileContext)
                : viewport;
            for (var i = 0; i < element.Children.Count; i++)
            {
                var childElement = element.Children[i];
                _ = compileContext.GetChildElementAddressKey(element, i);
                if (CompileElementNode(
                        childElement,
                        childViewport,
                        node.TotalTransform.IsIdentity ? parentTotalTransform : node.TotalTransform,
                        assetLoader,
                        ignoreAttributes,
                        compilationRootKey: null,
                        createOwnCompilationRootBoundary: true,
                        compileContext) is { } childNode)
                {
                    node.AddChild(childNode, element.Children.Count);
                }
            }
        }
        else if (node.CompilationStrategy == SvgSceneCompilationStrategy.DirectRetained &&
                 element is SvgSwitch svgSwitch &&
                 TryGetActiveSwitchChild(svgSwitch, out var activeSwitchChild) &&
                 activeSwitchChild is not null &&
                 CompileElementNode(
                     activeSwitchChild,
                     viewport,
                     node.TotalTransform.IsIdentity ? parentTotalTransform : node.TotalTransform,
                     assetLoader,
                     ignoreAttributes,
                     node.CompilationRootKey,
                     createOwnCompilationRootBoundary: false,
                     compileContext) is { } directSwitchChildNode)
        {
            node.AddChild(directSwitchChildNode);
        }

        if (node.CompilationStrategy == SvgSceneCompilationStrategy.DirectRetained)
        {
            FinalizeDirectStructuralNode(node, element, viewport, parentTotalTransform, ignoreAttributes);
        }

        return node;
    }

    private static bool TryCompileDirectNonRenderingNode(
        SvgElement element,
        SKMatrix parentTotalTransform,
        string? compilationRootKey,
        bool createOwnCompilationRootBoundary,
        SvgCascadedStyleFeatureFlags ownFeatureFlags,
        SvgSceneCompileContext compileContext,
        out SvgSceneNode? node)
    {
        node = null;

        var isForeignObject = element is SvgForeignObject;
        var isSymbol = element is SvgSymbol;
        var isNonRenderingElement = element is not SvgVisualElement &&
                                    element is not SvgDocument &&
                                    element is not SvgFragment &&
                                    element is not SvgGroup &&
                                    element is not SvgAnchor &&
                                    element is not SvgSwitch;
        if (!isForeignObject && !isSymbol && !isNonRenderingElement)
        {
            return false;
        }

        var elementAddressKey = compileContext.GetElementAddressKey(element);
        var effectiveCompilationRootKey = createOwnCompilationRootBoundary
            ? elementAddressKey
            : compilationRootKey;
        var kind = element switch
        {
            SvgMask => SvgSceneNodeKind.Mask,
            SvgSymbol => SvgSceneNodeKind.Fragment,
            SvgMarker => SvgSceneNodeKind.Marker,
            _ => SvgSceneNodeKind.Container
        };
        var transform = element is SvgVisualElement visualElement
            ? TransformsService.ToMatrix(visualElement.Transforms)
            : SKMatrix.Identity;

        node = new SvgSceneNode(
            kind,
            element,
            elementAddressKey,
            element.GetType().Name,
            effectiveCompilationRootKey,
            createOwnCompilationRootBoundary && !string.IsNullOrWhiteSpace(effectiveCompilationRootKey))
        {
            CompilationStrategy = SvgSceneCompilationStrategy.DirectRetained,
            IsRenderable = false,
            IsAntialias = element is SvgVisualElement visual ? PaintingService.IsAntialias(visual) : true,
            Transform = transform,
            TotalTransform = parentTotalTransform.PreConcat(transform),
            HitTestTargetElement = null
        };
        AssignRetainedVisualState(
            node,
            element,
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.MixBlendMode),
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.Isolation));
        AssignRetainedResourceKeys(node, element, compileContext);

        return true;
    }

    private static bool TryCompileDirectStructuralNode(
        SvgElement element,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        DrawAttributes ignoreAttributes,
        string? compilationRootKey,
        bool createOwnCompilationRootBoundary,
        SvgCascadedStyleFeatureFlags ownFeatureFlags,
        SvgSceneCompileContext compileContext,
        out SvgSceneNode? node)
    {
        node = null;

        switch (element)
        {
            case SvgDocument svgDocument:
                {
                    if (!HasFeatures(svgDocument, ignoreAttributes))
                    {
                        node = CreateDirectStructuralNode(
                            svgDocument,
                            compilationRootKey,
                            createOwnCompilationRootBoundary,
                            ownFeatureFlags,
                            compileContext,
                            PaintingService.IsAntialias(svgDocument),
                            isRenderable: false,
                            suppressSubtreeRendering: true,
                            SKMatrix.Identity,
                            parentTotalTransform);
                        return true;
                    }

                    var fragmentViewport = GetFragmentViewport(svgDocument, viewport, compileContext, out var x, out var y, out var size);
                    var transform = TransformsService.ToMatrix(svgDocument.Transforms);
                    var viewBoxTransform = TransformsService.ToMatrix(svgDocument.ViewBox, svgDocument.AspectRatio, x, y, size.Width, size.Height);
                    transform = transform.PreConcat(viewBoxTransform);

                    node = CreateDirectStructuralNode(
                        svgDocument,
                        compilationRootKey,
                        createOwnCompilationRootBoundary,
                        ownFeatureFlags,
                        compileContext,
                        PaintingService.IsAntialias(svgDocument),
                        isRenderable: true,
                        suppressSubtreeRendering: false,
                        transform,
                        parentTotalTransform);
                    node.GeometryBounds = fragmentViewport;

                    switch (svgDocument.Overflow)
                    {
                        case SvgOverflow.Auto:
                        case SvgOverflow.Visible:
                        case SvgOverflow.Inherit:
                            break;
                        default:
                            node.Overflow = size.IsEmpty
                                ? SKRect.Create(
                                    x,
                                    y,
                                    Math.Abs(fragmentViewport.Left) + fragmentViewport.Width,
                                    Math.Abs(fragmentViewport.Top) + fragmentViewport.Height)
                                : SKRect.Create(x, y, size.Width, size.Height);
                            break;
                    }

                    return true;
                }
            case SvgAnchor svgAnchor:
                {
                    node = CreateDirectStructuralNode(
                        svgAnchor,
                        compilationRootKey,
                        createOwnCompilationRootBoundary,
                        ownFeatureFlags,
                        compileContext,
                        PaintingService.IsAntialias(svgAnchor),
                        isRenderable: true,
                        suppressSubtreeRendering: false,
                        TransformsService.ToMatrix(svgAnchor.Transforms),
                        parentTotalTransform);
                    return true;
                }
            case SvgGroup svgGroup:
                {
                    var hasFeatures = HasFeatures(svgGroup, ignoreAttributes);
                    var isVisible = MaskingService.IsVisible(svgGroup, ignoreAttributes);
                    var isDisplayRendered = MaskingService.IsDisplayRendered(svgGroup, ignoreAttributes);
                    node = CreateDirectStructuralNode(
                        svgGroup,
                        compilationRootKey,
                        createOwnCompilationRootBoundary,
                        ownFeatureFlags,
                        compileContext,
                        PaintingService.IsAntialias(svgGroup),
                        hasFeatures && isVisible && isDisplayRendered,
                        suppressSubtreeRendering: !hasFeatures || !isDisplayRendered,
                        TransformsService.ToMatrix(svgGroup.Transforms),
                        parentTotalTransform);
                    return true;
                }
            case SvgSwitch svgSwitch:
                {
                    var hasFeatures = HasFeatures(svgSwitch, ignoreAttributes);
                    var isVisible = MaskingService.IsVisible(svgSwitch, ignoreAttributes);
                    var isDisplayRendered = MaskingService.IsDisplayRendered(svgSwitch, ignoreAttributes);
                    node = CreateDirectStructuralNode(
                        svgSwitch,
                        compilationRootKey,
                        createOwnCompilationRootBoundary,
                        ownFeatureFlags,
                        compileContext,
                        PaintingService.IsAntialias(svgSwitch),
                        hasFeatures && isVisible && isDisplayRendered,
                        suppressSubtreeRendering: !hasFeatures || !isDisplayRendered,
                        TransformsService.ToMatrix(svgSwitch.Transforms),
                        parentTotalTransform);
                    return true;
                }
            case SvgFragment svgFragment when element is not SvgDocument:
                {
                    if (!HasFeatures(svgFragment, ignoreAttributes))
                    {
                        node = CreateDirectStructuralNode(
                            svgFragment,
                            compilationRootKey,
                            createOwnCompilationRootBoundary,
                            ownFeatureFlags,
                            compileContext,
                            PaintingService.IsAntialias(svgFragment),
                            isRenderable: false,
                            suppressSubtreeRendering: true,
                            SKMatrix.Identity,
                            parentTotalTransform);
                        return true;
                    }

                    var fragmentViewport = GetFragmentViewport(svgFragment, viewport, compileContext, out var x, out var y, out var size);
                    var transform = TransformsService.ToMatrix(svgFragment.Transforms);
                    var viewBoxTransform = TransformsService.ToMatrix(svgFragment.ViewBox, svgFragment.AspectRatio, x, y, size.Width, size.Height);
                    transform = transform.PreConcat(viewBoxTransform);

                    node = CreateDirectStructuralNode(
                        svgFragment,
                        compilationRootKey,
                        createOwnCompilationRootBoundary,
                        ownFeatureFlags,
                        compileContext,
                        PaintingService.IsAntialias(svgFragment),
                        isRenderable: true,
                        suppressSubtreeRendering: false,
                        transform,
                        parentTotalTransform);
                    node.GeometryBounds = fragmentViewport;

                    switch (svgFragment.Overflow)
                    {
                        case SvgOverflow.Auto:
                        case SvgOverflow.Visible:
                        case SvgOverflow.Inherit:
                            break;
                        default:
                            node.Overflow = size.IsEmpty
                                ? SKRect.Create(
                                    x,
                                    y,
                                    Math.Abs(fragmentViewport.Left) + fragmentViewport.Width,
                                    Math.Abs(fragmentViewport.Top) + fragmentViewport.Height)
                                : SKRect.Create(x, y, size.Width, size.Height);
                            break;
                    }

                    return true;
                }
            default:
                return false;
        }
    }

    private static SvgSceneNode CreateDirectStructuralNode(
        SvgElement element,
        string? compilationRootKey,
        bool createOwnCompilationRootBoundary,
        SvgCascadedStyleFeatureFlags ownFeatureFlags,
        SvgSceneCompileContext compileContext,
        bool isAntialias,
        bool isRenderable,
        bool suppressSubtreeRendering,
        SKMatrix transform,
        SKMatrix parentTotalTransform)
    {
        var elementAddressKey = compileContext.GetElementAddressKey(element);
        var effectiveCompilationRootKey = createOwnCompilationRootBoundary
            ? elementAddressKey
            : compilationRootKey;

        var node = new SvgSceneNode(
            SvgSceneNodeKindExtensions.FromElement(element),
            element,
            elementAddressKey,
            element.GetType().Name,
            effectiveCompilationRootKey,
            createOwnCompilationRootBoundary && !string.IsNullOrWhiteSpace(effectiveCompilationRootKey))
        {
            CompilationStrategy = SvgSceneCompilationStrategy.DirectRetained,
            IsAntialias = isAntialias,
            IsRenderable = isRenderable,
            SuppressSubtreeRendering = suppressSubtreeRendering,
            HitTestTargetElement = null,
            Transform = transform,
            TotalTransform = parentTotalTransform.PreConcat(transform)
        };

        AssignRetainedVisualState(
            node,
            element,
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.MixBlendMode),
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.Isolation));
        AssignRetainedResourceKeys(node, element, compileContext);
        return node;
    }

    private static void FinalizeDirectStructuralNode(
        SvgSceneNode node,
        SvgElement element,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        DrawAttributes ignoreAttributes)
    {
        switch (element)
        {
            case SvgDocument svgDocument:
                FinalizeDirectFragmentNode(node, svgDocument, viewport, parentTotalTransform, ignoreAttributes);
                break;
            case SvgGroup svgGroup:
                FinalizeDirectGroupNode(node, svgGroup, viewport, parentTotalTransform, ignoreAttributes);
                break;
            case SvgAnchor svgAnchor:
                FinalizeDirectAnchorNode(node, svgAnchor, viewport, parentTotalTransform, ignoreAttributes);
                break;
            case SvgSwitch svgSwitch:
                FinalizeDirectSwitchNode(node, svgSwitch, viewport, parentTotalTransform, ignoreAttributes);
                break;
            case SvgForeignObject svgForeignObject:
                FinalizeDirectForeignObjectNode(node, svgForeignObject, viewport, parentTotalTransform);
                break;
            case SvgFragment svgFragment when element is not SvgDocument:
                FinalizeDirectFragmentNode(node, svgFragment, viewport, parentTotalTransform, ignoreAttributes);
                break;
        }
    }

    private static void FinalizeDirectGroupNode(
        SvgSceneNode node,
        SvgGroup svgGroup,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        DrawAttributes ignoreAttributes)
    {
        FinalizeDirectStructuralBounds(
            node,
            parentTotalTransform,
            bounds => TransformsService.ApplyTransformOrigin(svgGroup, bounds, viewport, node.Transform));
    }

    private static void FinalizeDirectAnchorNode(
        SvgSceneNode node,
        SvgAnchor svgAnchor,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        DrawAttributes ignoreAttributes)
    {
        FinalizeDirectStructuralBounds(
            node,
            parentTotalTransform,
            bounds => TransformsService.ApplyTransformOrigin(svgAnchor, bounds, viewport, node.Transform));
        node.ClipPath = null;
        node.MaskPaint = null;
        node.MaskDstIn = null;
        node.Filter = null;
        node.FilterClip = null;
        node.FilterUsesGlobalLayer = false;
        node.FilterGlobalClip = null;
    }

    private static void FinalizeDirectSwitchNode(
        SvgSceneNode node,
        SvgSwitch svgSwitch,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        DrawAttributes ignoreAttributes)
    {
        FinalizeDirectStructuralBounds(
            node,
            parentTotalTransform,
            bounds => TransformsService.ApplyTransformOrigin(svgSwitch, bounds, viewport, node.Transform));
        node.ClipPath = null;
        node.MaskPaint = null;
        node.MaskDstIn = null;
        node.Filter = null;
        node.FilterClip = null;
        node.FilterUsesGlobalLayer = false;
        node.FilterGlobalClip = null;
    }

    private static void FinalizeDirectFragmentNode(
        SvgSceneNode node,
        SvgFragment svgFragment,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        DrawAttributes ignoreAttributes)
    {
        var fragmentViewport = node.GeometryBounds;
        FinalizeDirectStructuralBounds(
            node,
            parentTotalTransform,
            bounds =>
            {
                var transform = TransformsService.ToMatrix(svgFragment.Transforms, svgFragment, bounds, viewport);
                var viewBoxTransform = TransformsService.ToMatrix(
                    svgFragment.ViewBox,
                    svgFragment.AspectRatio,
                    fragmentViewport.Left,
                    fragmentViewport.Top,
                    fragmentViewport.Width,
                    fragmentViewport.Height);
                return transform.PreConcat(viewBoxTransform);
            });
    }

    private static void FinalizeDirectForeignObjectNode(
        SvgSceneNode node,
        SvgForeignObject svgForeignObject,
        SKRect viewport,
        SKMatrix parentTotalTransform)
    {
        if (!TryGetForeignObjectBounds(svgForeignObject, viewport, out var bounds))
        {
            FinalizeDirectStructuralBounds(node, parentTotalTransform);
            return;
        }

        node.GeometryBounds = bounds;
        node.TotalTransform = parentTotalTransform.PreConcat(node.Transform);
        node.TransformedBounds = node.TotalTransform.MapRect(bounds);
    }

    private static bool TryGetForeignObjectBounds(SvgForeignObject svgForeignObject, SKRect viewport, out SKRect bounds)
    {
        bounds = default;

        var x = TryGetForeignObjectUnit(svgForeignObject, "x", out var xUnit)
            ? xUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgForeignObject, viewport)
            : 0f;
        var y = TryGetForeignObjectUnit(svgForeignObject, "y", out var yUnit)
            ? yUnit.ToDeviceValue(UnitRenderingType.Vertical, svgForeignObject, viewport)
            : 0f;

        if (!TryGetForeignObjectUnit(svgForeignObject, "width", out var widthUnit)
            || !TryGetForeignObjectUnit(svgForeignObject, "height", out var heightUnit))
        {
            return false;
        }

        var width = widthUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgForeignObject, viewport);
        var height = heightUnit.ToDeviceValue(UnitRenderingType.Vertical, svgForeignObject, viewport);
        if (width <= 0f || height <= 0f)
        {
            return false;
        }

        bounds = SKRect.Create(x, y, width, height);
        return true;
    }

    private static bool TryGetForeignObjectUnit(SvgForeignObject svgForeignObject, string attributeName, out SvgUnit unit)
    {
        unit = default;
        if (!svgForeignObject.TryGetAttribute(attributeName, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            unit = SvgUnitConverter.Parse(value.AsSpan().Trim());
            return !unit.IsEmpty && !unit.IsNone;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void FinalizeDirectStructuralBounds(
        SvgSceneNode node,
        SKMatrix parentTotalTransform,
        Func<SKRect, SKMatrix>? resolveTransform = null)
    {
        var bounds = node.GeometryBounds;
        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            if (child.IsDisplayNone)
            {
                continue;
            }

            var childBounds = child.GeometryBounds;
            if (childBounds.IsEmpty)
            {
                continue;
            }

            if (!child.Transform.IsIdentity)
            {
                childBounds = child.Transform.MapRect(childBounds);
            }

            bounds = bounds.IsEmpty
                ? childBounds
                : SKRect.Union(bounds, childBounds);
        }

        node.GeometryBounds = bounds;
        if (resolveTransform is not null)
        {
            node.Transform = resolveTransform(bounds);
        }

        RefreshNodeTotalTransforms(node, parentTotalTransform);
    }

    private static void RefreshNodeTotalTransforms(
        SvgSceneNode node,
        SKMatrix parentTotalTransform)
    {
        node.TotalTransform = parentTotalTransform.PreConcat(node.Transform);
        node.TransformedBounds = node.TotalTransform.MapRect(node.GeometryBounds);

        for (var i = 0; i < node.Children.Count; i++)
        {
            RefreshNodeTotalTransforms(node.Children[i], node.TotalTransform);
        }

        if (node.MaskNode is { } maskNode)
        {
            RefreshNodeTotalTransforms(maskNode, node.TotalTransform);
        }
    }

    private static SKRect GetFragmentViewport(
        SvgFragment svgFragment,
        SKRect viewport,
        SvgSceneCompileContext compileContext,
        out float x,
        out float y,
        out SKSize size)
    {
        var svgFragmentParent = svgFragment.Parent;

        x = svgFragmentParent is null ? 0f : svgFragment.X.ToDeviceValue(UnitRenderingType.Horizontal, svgFragment, viewport);
        y = svgFragmentParent is null ? 0f : svgFragment.Y.ToDeviceValue(UnitRenderingType.Vertical, svgFragment, viewport);
        size = compileContext.TryGetFragmentViewportSizeOverride(svgFragment, out var viewportSizeOverride)
            ? viewportSizeOverride
            : SvgService.GetDimensions(svgFragment, viewport);

        if (size.Width > 0f && size.Height > 0f)
        {
            return SKRect.Create(x, y, size.Width, size.Height);
        }

        return viewport.IsEmpty ? SKRect.Empty : viewport;
    }

    private static SKRect GetChildViewport(
        SvgElement element,
        SKRect viewport,
        SvgSceneCompileContext compileContext)
    {
        if (element is not SvgFragment svgFragment)
        {
            return viewport;
        }

        if (svgFragment.ViewBox != SvgViewBox.Empty &&
            svgFragment.ViewBox.Width > 0f &&
            svgFragment.ViewBox.Height > 0f)
        {
            return SKRect.Create(
                svgFragment.ViewBox.MinX,
                svgFragment.ViewBox.MinY,
                svgFragment.ViewBox.Width,
                svgFragment.ViewBox.Height);
        }

        var size = compileContext.TryGetFragmentViewportSizeOverride(svgFragment, out var viewportSizeOverride)
            ? viewportSizeOverride
            : SvgService.GetDimensions(svgFragment, viewport);
        return size.Width > 0f && size.Height > 0f
            ? SKRect.Create(0f, 0f, size.Width, size.Height)
            : viewport;
    }

    private static bool TryCompileDirectElementNode(
        SvgElement element,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        string? compilationRootKey,
        bool createOwnCompilationRootBoundary,
        SvgCascadedStyleFeatureFlags ownFeatureFlags,
        SvgSceneCompileContext compileContext,
        out SvgSceneNode? node)
    {
        node = null;

        if (element is SvgUse svgUse)
        {
            return TryCompileDirectUseNode(
                svgUse,
                viewport,
                parentTotalTransform,
                assetLoader,
                ignoreAttributes,
                compilationRootKey,
                createOwnCompilationRootBoundary,
                ownFeatureFlags,
                compileContext,
                out node);
        }

        if (element is SvgImage svgImage)
        {
            return TryCompileDirectImageNode(
                svgImage,
                viewport,
                parentTotalTransform,
                assetLoader,
                ignoreAttributes,
                compilationRootKey,
                createOwnCompilationRootBoundary,
                ownFeatureFlags,
                compileContext,
                out node);
        }

        if (element is SvgTextBase svgTextBase)
        {
            var textElementAddressKey = compileContext.GetElementAddressKey(element);
            var textCompilationRootKey = createOwnCompilationRootBoundary
                ? textElementAddressKey
                : compilationRootKey;

            return SvgSceneTextCompiler.TryCompile(
                svgTextBase,
                viewport,
                parentTotalTransform,
                assetLoader,
                references: null,
                ignoreAttributes,
                textElementAddressKey,
                textCompilationRootKey,
                createOwnCompilationRootBoundary && !string.IsNullOrWhiteSpace(textCompilationRootKey),
                compileContext.GetElementAddressKey,
                HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.MixBlendMode),
                HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.Isolation),
                compileContext.ContextPaint,
                out node);
        }

        if (!TryGetDirectVisualElement(element, out var visualElement) ||
            visualElement is null)
        {
            return false;
        }

        _ = TryGetDirectVisualPath(element, viewport, out var path);

        var elementAddressKey = compileContext.GetElementAddressKey(element);
        var effectiveCompilationRootKey = createOwnCompilationRootBoundary
            ? elementAddressKey
            : compilationRootKey;

        node = new SvgSceneNode(
            SvgSceneNodeKindExtensions.FromElement(element),
            element,
            elementAddressKey,
            element.GetType().Name,
            effectiveCompilationRootKey,
            createOwnCompilationRootBoundary && !string.IsNullOrWhiteSpace(effectiveCompilationRootKey))
        {
            CompilationStrategy = SvgSceneCompilationStrategy.DirectRetained
        };

        var hasFeatures = HasFeatures(element, ignoreAttributes);
        var canDraw = MaskingService.CanDraw(visualElement, ignoreAttributes);
        var isRenderable = hasFeatures && canDraw;
        node.IsRenderable = isRenderable;
        node.IsAntialias = PaintingService.IsAntialias(visualElement);
        node.GeometryBounds = path?.Bounds ?? SKRect.Empty;
        node.Transform = TransformsService.ToMatrix(visualElement.Transforms, visualElement, node.GeometryBounds, viewport);
        node.TotalTransform = parentTotalTransform.PreConcat(node.Transform);
        node.TransformedBounds = node.TotalTransform.MapRect(node.GeometryBounds);
        node.HitTestPath = path;
        node.SupportsFillHitTest = SvgScenePaintingService.IsValidFill(visualElement);
        node.SupportsStrokeHitTest = SvgScenePaintingService.IsValidStroke(visualElement, node.GeometryBounds);
        node.IsStrokeNonScaling = visualElement.VectorEffect == SvgVectorEffect.NonScalingStroke;
        node.HitTestTargetElement = GetDefaultHitTestTargetElement(node, element);
        AssignRetainedVisualState(
            node,
            element,
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.MixBlendMode),
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.Isolation));
        AssignRetainedResourceKeys(node, element, compileContext);
        var markerElement = !ignoreAttributes.HasFlag(DrawAttributes.Markers) &&
            compileContext.ActiveMarkerReferenceDeclarationCandidate &&
            HasMarkerReference(visualElement)
            ? visualElement
            : null;

        if (!isRenderable || path is null || path.IsEmpty)
        {
            if (markerElement is not null &&
                path is not null &&
                !path.IsEmpty)
            {
                AppendDirectMarkers(
                    node,
                    markerElement,
                    path,
                    viewport,
                    parentTotalTransform,
                    assetLoader,
                    ignoreAttributes,
                    compileContext);
            }
            return true;
        }

        var localPath = CreateDirectPathVisual(
            visualElement,
            path,
            node.GeometryBounds,
            assetLoader,
            ignoreAttributes,
            compileContext,
            out var localFill,
            out var localStroke,
            out var canKeepRenderable);
        node.LocalPath = localPath;
        node.LocalFill = localFill;
        node.LocalStroke = localStroke;
        if (!canKeepRenderable)
        {
            node.IsRenderable = false;
            if (markerElement is not null)
            {
                AppendDirectMarkers(
                    node,
                    markerElement,
                    path,
                    viewport,
                    parentTotalTransform,
                    assetLoader,
                    ignoreAttributes,
                    compileContext);
            }
            return true;
        }

        if (markerElement is not null)
        {
            AppendDirectMarkers(
                node,
                markerElement,
                path,
                viewport,
                parentTotalTransform,
                assetLoader,
                ignoreAttributes,
                compileContext);
        }

        return true;
    }

    private static bool TryCompileDirectUseNode(
        SvgUse svgUse,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        string? compilationRootKey,
        bool createOwnCompilationRootBoundary,
        SvgCascadedStyleFeatureFlags ownFeatureFlags,
        SvgSceneCompileContext compileContext,
        out SvgSceneNode? node)
    {
        var elementAddressKey = compileContext.GetElementAddressKey(svgUse);
        var effectiveCompilationRootKey = createOwnCompilationRootBoundary
            ? elementAddressKey
            : compilationRootKey;

        var useNode = new SvgSceneNode(
            SvgSceneNodeKind.Use,
            svgUse,
            elementAddressKey,
            svgUse.GetType().Name,
            effectiveCompilationRootKey,
            createOwnCompilationRootBoundary && !string.IsNullOrWhiteSpace(effectiveCompilationRootKey))
        {
            CompilationStrategy = SvgSceneCompilationStrategy.DirectRetained,
            IsAntialias = PaintingService.IsAntialias(svgUse),
            IsRenderable = HasFeatures(svgUse, ignoreAttributes) && MaskingService.CanDraw(svgUse, ignoreAttributes),
            HitTestTargetElement = svgUse,
            Fill = null,
            Stroke = null
        };
        AssignRetainedResourceKeys(useNode, svgUse, compileContext);
        AssignRetainedVisualState(
            useNode,
            svgUse,
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.MixBlendMode),
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.Isolation));

        var x = SvgGeometryService.GetComputedUnit(svgUse, "x", svgUse.X).ToDeviceValue(UnitRenderingType.Horizontal, svgUse, viewport);
        var y = SvgGeometryService.GetComputedUnit(svgUse, "y", svgUse.Y).ToDeviceValue(UnitRenderingType.Vertical, svgUse, viewport);
        var hasExplicitWidth = HasExplicitUseDimension(svgUse, "width");
        var hasExplicitHeight = HasExplicitUseDimension(svgUse, "height");
        var width = SvgGeometryService.GetComputedUnit(svgUse, "width", svgUse.Width).ToDeviceValue(UnitRenderingType.Horizontal, svgUse, viewport);
        var height = SvgGeometryService.GetComputedUnit(svgUse, "height", svgUse.Height).ToDeviceValue(UnitRenderingType.Vertical, svgUse, viewport);

        var referencedElementUri = SvgService.GetEffectiveReferenceUri(svgUse, svgUse.ReferencedElement);
        var hasRecursiveReference = SvgService.HasRecursiveReference(svgUse, static element => SvgService.GetEffectiveReferenceUri(element, element.ReferencedElement), new HashSet<Uri>());
        var referencedElement = hasRecursiveReference
            ? null
            : SvgService.GetReference<SvgElement>(svgUse, referencedElementUri);

        width = ResolveUseDimension(svgUse, referencedElement, hasExplicitWidth, width, UnitRenderingType.Horizontal, viewport);
        height = ResolveUseDimension(svgUse, referencedElement, hasExplicitHeight, height, UnitRenderingType.Vertical, viewport);

        var useTransform = TransformsService.ToMatrix(svgUse.Transforms);
        if (referencedElement is not SvgSymbol)
        {
            useTransform = useTransform.PreConcat(SKMatrix.CreateTranslation(x, y));
        }

        useNode.Transform = useTransform;
        useNode.TotalTransform = parentTotalTransform.PreConcat(useTransform);

        if (!useNode.IsRenderable ||
            referencedElement is null)
        {
            useNode.IsRenderable = false;
            node = useNode;
            return true;
        }

        var contextPaintBounds = CreateUseContextPaintBounds(referencedElement, x, y, width, height, viewport);
        // Keep generated nodes addressable by the original tree while style resolution uses the <use> parent.
        _ = compileContext.GetElementAddressKey(referencedElement);
        var referencedNode = WithUseInstanceStyleScope(referencedElement, svgUse, () =>
        {
            referencedElement.InvalidateChildPaths();

            using var contextPaintScope = compileContext.PushContextPaint(svgUse, contextPaintBounds);
            using var fragmentViewportScope = referencedElement is SvgFragment svgFragment && referencedElement is not SvgSymbol
                ? compileContext.PushFragmentViewportSizeOverride(svgFragment, new SKSize(width, height))
                : null;
            return referencedElement switch
            {
                SvgSymbol svgSymbol => CompileDirectSymbolReferenceNode(
                    svgSymbol,
                    x,
                    y,
                    width,
                    height,
                    viewport,
                    useNode.TotalTransform,
                    assetLoader,
                    ignoreAttributes,
                    useNode.CompilationRootKey,
                    compileContext),
                _ => CompileElementNode(
                    referencedElement,
                    viewport,
                    useNode.TotalTransform,
                    assetLoader,
                    ignoreAttributes,
                    useNode.CompilationRootKey,
                    createOwnCompilationRootBoundary: false,
                    compileContext)
            };
        });

        if (referencedNode is null)
        {
            useNode.IsRenderable = false;
            node = useNode;
            return true;
        }

        AssignGeneratedHitTestTarget(referencedNode, svgUse);
        useNode.AddChild(referencedNode);
        FinalizeDirectStructuralBounds(
            useNode,
            parentTotalTransform,
            bounds => ResolveUseTransform(svgUse, referencedElement, x, y, bounds, viewport));
        node = useNode;
        return true;
    }

    private static SKMatrix ResolveUseTransform(
        SvgUse svgUse,
        SvgElement? referencedElement,
        float x,
        float y,
        SKRect referenceBounds,
        SKRect viewport)
    {
        var useTransform = TransformsService.ToMatrix(svgUse.Transforms, svgUse, referenceBounds, viewport);
        if (referencedElement is not SvgSymbol)
        {
            useTransform = useTransform.PreConcat(SKMatrix.CreateTranslation(x, y));
        }

        return useTransform;
    }

    private static bool HasExplicitUseDimension(SvgUse svgUse, string name)
    {
        return SvgService.TryGetAttribute(svgUse, name, out _) ||
               svgUse.ComputedStyle.TryGetPropertyValue(name, out var rawValue) &&
               !string.IsNullOrWhiteSpace(rawValue);
    }

    private static SKRect CreateUseContextPaintBounds(
        SvgElement referencedElement,
        float x,
        float y,
        float width,
        float height,
        SKRect viewport)
    {
        if (referencedElement is not SvgSymbol and not SvgFragment &&
            TryGetElementGeometryBounds(referencedElement, viewport, out var referencedBounds) &&
            !referencedBounds.IsEmpty)
        {
            return SKRect.Create(
                x + referencedBounds.Left,
                y + referencedBounds.Top,
                referencedBounds.Width,
                referencedBounds.Height);
        }

        return SKRect.Create(x, y, width, height);
    }

    private static bool TryGetElementGeometryBounds(SvgElement element, SKRect viewport, out SKRect bounds)
    {
        bounds = SKRect.Empty;

        if (TryGetDirectVisualPath(element, viewport, out var path) && path is not null)
        {
            bounds = path.Bounds;
            return !bounds.IsEmpty;
        }

        for (var i = 0; i < element.Children.Count; i++)
        {
            if (!TryGetElementGeometryBounds(element.Children[i], viewport, out var childBounds) ||
                childBounds.IsEmpty)
            {
                continue;
            }

            if (element.Children[i] is SvgVisualElement { Transforms.Count: > 0 } childVisual)
            {
                childBounds = TransformsService.ToMatrix(childVisual.Transforms).MapRect(childBounds);
            }

            bounds = bounds.IsEmpty
                ? childBounds
                : SKRect.Union(bounds, childBounds);
        }

        return !bounds.IsEmpty;
    }

    private static float ResolveUseDimension(
        SvgUse svgUse,
        SvgElement? referencedElement,
        bool hasExplicitDimension,
        float deviceValue,
        UnitRenderingType renderingType,
        SKRect viewport)
    {
        if ((!hasExplicitDimension || deviceValue <= 0f) &&
            TryGetReferencedUseDimension(referencedElement, renderingType, viewport, out var referencedDimension) &&
            referencedDimension > 0f)
        {
            return referencedDimension;
        }

        return deviceValue > 0f
            ? deviceValue
            : new SvgUnit(SvgUnitType.Percentage, 100f).ToDeviceValue(renderingType, svgUse, viewport);
    }

    private static bool TryGetReferencedUseDimension(
        SvgElement? referencedElement,
        UnitRenderingType renderingType,
        SKRect viewport,
        out float dimension)
    {
        dimension = 0f;

        switch (referencedElement)
        {
            case SvgSymbol svgSymbol:
                var symbolDimension = renderingType == UnitRenderingType.Horizontal
                    ? svgSymbol.Width
                    : svgSymbol.Height;
                if (symbolDimension == SvgUnit.None || symbolDimension == SvgUnit.Empty)
                {
                    return false;
                }

                dimension = symbolDimension.ToDeviceValue(renderingType, svgSymbol, viewport);
                return dimension > 0f;

            case SvgFragment svgFragment:
                var size = SvgService.GetDimensions(svgFragment, viewport);
                dimension = renderingType == UnitRenderingType.Horizontal ? size.Width : size.Height;
                return dimension > 0f;

            default:
                return false;
        }
    }

    private static bool TryCompileDirectImageNode(
        SvgImage svgImage,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        string? compilationRootKey,
        bool createOwnCompilationRootBoundary,
        SvgCascadedStyleFeatureFlags ownFeatureFlags,
        SvgSceneCompileContext compileContext,
        out SvgSceneNode? node)
    {
        var elementAddressKey = compileContext.GetElementAddressKey(svgImage);
        var effectiveCompilationRootKey = createOwnCompilationRootBoundary
            ? elementAddressKey
            : compilationRootKey;

        node = new SvgSceneNode(
            SvgSceneNodeKind.Image,
            svgImage,
            elementAddressKey,
            svgImage.GetType().Name,
            effectiveCompilationRootKey,
            createOwnCompilationRootBoundary && !string.IsNullOrWhiteSpace(effectiveCompilationRootKey))
        {
            CompilationStrategy = SvgSceneCompilationStrategy.DirectRetained,
            IsAntialias = PaintingService.IsAntialias(svgImage),
            IsRenderable = HasFeatures(svgImage, ignoreAttributes) && MaskingService.CanDraw(svgImage, ignoreAttributes),
            HitTestTargetElement = svgImage,
            SupportsFillHitTest = true,
            Fill = null,
            Stroke = null
        };
        AssignRetainedResourceKeys(node, svgImage, compileContext);
        AssignRetainedVisualState(
            node,
            svgImage,
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.MixBlendMode),
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.Isolation));

        var widthUnit = SvgGeometryService.GetComputedUnit(svgImage, "width", svgImage.Width, out var widthAuto, out var widthAuthorSpecified);
        var heightUnit = SvgGeometryService.GetComputedUnit(svgImage, "height", svgImage.Height, out var heightAuto, out var heightAuthorSpecified);
        var hasExplicitWidth = widthAuthorSpecified && !widthAuto;
        var hasExplicitHeight = heightAuthorSpecified && !heightAuto;
        var width = widthUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgImage, viewport);
        var height = heightUnit.ToDeviceValue(UnitRenderingType.Vertical, svgImage, viewport);
        var x = SvgGeometryService.GetComputedUnit(svgImage, "x", svgImage.Location.X).ToDeviceValue(UnitRenderingType.Horizontal, svgImage, viewport);
        var y = SvgGeometryService.GetComputedUnit(svgImage, "y", svgImage.Location.Y).ToDeviceValue(UnitRenderingType.Vertical, svgImage, viewport);

        var href = SvgService.GetEffectiveHrefString(svgImage, svgImage.Href);
        if (!node.IsRenderable || string.IsNullOrWhiteSpace(href))
        {
            node.IsRenderable = false;
            return true;
        }

        var uri = SvgService.GetImageDocumentUri(SvgService.GetImageUri(href!, svgImage));
        var references = CreateReferences(svgImage);
        if (references is { } && references.Contains(uri))
        {
            var placeholderRect = SKRect.Create(x, y, width, height);
            if (!TryUseBrokenImagePlaceholder(
                    node,
                    svgImage,
                    placeholderRect,
                    viewport,
                    parentTotalTransform,
                    assetLoader))
            {
                node.IsRenderable = false;
            }

            return true;
        }

        var image = SvgService.GetImage(href!, svgImage, assetLoader);
        if (image is not SKImage && image is not SvgDocument)
        {
            var placeholderRect = SKRect.Create(x, y, width, height);
            if (!TryUseBrokenImagePlaceholder(
                    node,
                    svgImage,
                    placeholderRect,
                    viewport,
                    parentTotalTransform,
                    assetLoader))
            {
                node.IsRenderable = false;
            }

            return true;
        }

        var srcRect = image switch
        {
            SKImage skImage => SKRect.Create(0f, 0f, skImage.Width, skImage.Height),
            SvgDocument svgDocument => CreateSourceRect(svgDocument, SKRect.Create(0f, 0f, width, height)),
            _ => SKRect.Empty
        };

        if (srcRect.IsEmpty)
        {
            var placeholderRect = SKRect.Create(x, y, width, height);
            if (!TryUseBrokenImagePlaceholder(
                    node,
                    svgImage,
                    placeholderRect,
                    viewport,
                    parentTotalTransform,
                    assetLoader))
            {
                node.IsRenderable = false;
            }

            return true;
        }

        ResolveImageAutoSize(srcRect, hasExplicitWidth, hasExplicitHeight, ref width, ref height);
        if (width <= 0f || height <= 0f)
        {
            node.IsRenderable = false;
            return true;
        }

        var destClip = SKRect.Create(x, y, width, height);
        var destRect = TransformsService.CalculateRect(svgImage.AspectRatio, srcRect, destClip);
        var usesReferencedSvgViewport =
            image is SvgDocument svgDocumentImage &&
            ShouldUseReferencedSvgViewport(svgDocumentImage, uri) &&
            svgImage.AspectRatio.Align != SvgPreserveAspectRatio.none;
        var geometryBounds = usesReferencedSvgViewport ? destClip : destRect;
        node.GeometryBounds = geometryBounds;
        node.Transform = TransformsService.ToMatrix(svgImage.Transforms, svgImage, geometryBounds, viewport);
        node.TotalTransform = parentTotalTransform.PreConcat(node.Transform);
        node.TransformedBounds = node.TotalTransform.MapRect(geometryBounds);
        node.Clip = MaskingService.GetClipRect(svgImage.Clip, destClip) ?? destClip;

        switch (image)
        {
            case SKImage skImage:
                node.LocalModel = CreateDirectImageModel(skImage, srcRect, destRect);
                if (node.LocalModel is null)
                {
                    node.IsRenderable = false;
                }
                break;
            case SvgDocument svgDocument:
                if (compileContext.IsActive(svgDocument))
                {
                    if (!TryUseBrokenImagePlaceholder(
                            node,
                            svgImage,
                            destClip,
                            viewport,
                            parentTotalTransform,
                            assetLoader))
                    {
                        node.IsRenderable = false;
                    }

                    return true;
                }

                var fragmentNode = usesReferencedSvgViewport
                    ? CompileEmbeddedSvgDocumentImageSceneNode(
                        svgImage,
                        svgDocument,
                        destClip,
                        assetLoader,
                        ignoreAttributes,
                        node.TotalTransform,
                        node.CompilationRootKey,
                        compileContext)
                    : CompileEmbeddedImageSceneNode(
                        svgImage,
                        svgDocument,
                        srcRect,
                        destRect,
                        assetLoader,
                        ignoreAttributes,
                        node.TotalTransform,
                        node.CompilationRootKey,
                        compileContext);
                if (fragmentNode is null)
                {
                    node.IsRenderable = false;
                    return true;
                }

                AssignGeneratedHitTestTarget(fragmentNode, svgImage);
                node.AddChild(fragmentNode);
                break;
        }

        return true;
    }

    private static SvgSceneNode? CompileDirectSymbolReferenceNode(
        SvgSymbol svgSymbol,
        float x,
        float y,
        float width,
        float height,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        string? compilationRootKey,
        SvgSceneCompileContext compileContext)
    {
        if (!HasFeatures(svgSymbol, ignoreAttributes) || !MaskingService.CanDraw(svgSymbol, ignoreAttributes))
        {
            return null;
        }

        var node = new SvgSceneNode(
            SvgSceneNodeKind.Fragment,
            svgSymbol,
            compileContext.GetElementAddressKey(svgSymbol),
            svgSymbol.GetType().Name,
            compilationRootKey,
            isCompilationRootBoundary: false)
        {
            CompilationStrategy = SvgSceneCompilationStrategy.DirectRetained,
            IsAntialias = PaintingService.IsAntialias(svgSymbol),
            IsRenderable = true,
            HitTestTargetElement = null,
            Fill = null,
            Stroke = null
        };
        AssignRetainedResourceKeys(node, svgSymbol, compileContext);
        var ownFeatureFlags = svgSymbol.GetOwnCascadedStyleFeatureFlags(GetRequestedCascadedStyleFeatureFlags(svgSymbol));
        AssignRetainedVisualState(
            node,
            svgSymbol,
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.MixBlendMode),
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.Isolation));

        var transform = TransformsService.ToMatrix(svgSymbol.Transforms);
        var viewBoxTransform = TransformsService.ToMatrix(svgSymbol.ViewBox, svgSymbol.AspectRatio, x, y, width, height);
        var symbolViewport = SKRect.Create(x, y, width, height);
        viewBoxTransform = ApplySymbolReferencePoint(svgSymbol, viewBoxTransform, x, y, viewport, ref symbolViewport);
        transform = transform.PreConcat(viewBoxTransform);
        node.Transform = transform;
        node.TotalTransform = parentTotalTransform.PreConcat(transform);

        var svgOverflow = SvgOverflow.Hidden;
        if (svgSymbol.TryGetAttribute("overflow", out string overflowString) &&
            new SvgOverflowConverter().ConvertFromString(overflowString) is SvgOverflow parsedOverflow)
        {
            svgOverflow = parsedOverflow;
        }

        if (svgOverflow is not SvgOverflow.Auto and not SvgOverflow.Visible and not SvgOverflow.Inherit)
        {
            node.Overflow = symbolViewport;
        }

        var childViewport = GetSymbolChildViewport(svgSymbol, width, height);
        for (var i = 0; i < svgSymbol.Children.Count; i++)
        {
            var childElement = svgSymbol.Children[i];
            _ = compileContext.GetChildElementAddressKey(svgSymbol, i);
            if (CompileElementNode(
                    childElement,
                    childViewport,
                    node.TotalTransform,
                    assetLoader,
                    ignoreAttributes,
                    compilationRootKey,
                    createOwnCompilationRootBoundary: false,
                    compileContext) is { } childNode)
            {
                node.AddChild(childNode, svgSymbol.Children.Count);
            }
        }

        FinalizeDirectStructuralBounds(
            node,
            parentTotalTransform,
            bounds =>
            {
                var transform = TransformsService.ToMatrix(svgSymbol.Transforms, svgSymbol, bounds, symbolViewport);
                var viewBoxTransform = TransformsService.ToMatrix(svgSymbol.ViewBox, svgSymbol.AspectRatio, x, y, width, height);
                var resolvedSymbolViewport = symbolViewport;
                viewBoxTransform = ApplySymbolReferencePoint(svgSymbol, viewBoxTransform, x, y, viewport, ref resolvedSymbolViewport);
                return transform.PreConcat(viewBoxTransform);
            });
        return node;
    }

    private static SKRect GetSymbolChildViewport(SvgSymbol svgSymbol, float width, float height)
    {
        if (svgSymbol.ViewBox != SvgViewBox.Empty &&
            svgSymbol.ViewBox.Width > 0f &&
            svgSymbol.ViewBox.Height > 0f)
        {
            return SKRect.Create(
                svgSymbol.ViewBox.MinX,
                svgSymbol.ViewBox.MinY,
                svgSymbol.ViewBox.Width,
                svgSymbol.ViewBox.Height);
        }

        return width > 0f && height > 0f
            ? SKRect.Create(0f, 0f, width, height)
            : SKRect.Empty;
    }

    private static SKMatrix ApplySymbolReferencePoint(
        SvgSymbol svgSymbol,
        SKMatrix viewBoxTransform,
        float x,
        float y,
        SKRect viewport,
        ref SKRect symbolViewport)
    {
        var hasRefX = SvgService.TryGetAttribute(svgSymbol, "refX", out _);
        var hasRefY = SvgService.TryGetAttribute(svgSymbol, "refY", out _);
        if (!hasRefX && !hasRefY)
        {
            return viewBoxTransform;
        }

        var deltaX = 0f;
        var deltaY = 0f;
        if (hasRefX)
        {
            var refX = svgSymbol.RefX.ToDeviceValue(UnitRenderingType.Horizontal, svgSymbol, viewport);
            var mappedReferencePoint = viewBoxTransform.MapPoint(new SKPoint(refX, 0f));
            deltaX = x - mappedReferencePoint.X;
        }

        if (hasRefY)
        {
            var refY = svgSymbol.RefY.ToDeviceValue(UnitRenderingType.Vertical, svgSymbol, viewport);
            var mappedReferencePoint = viewBoxTransform.MapPoint(new SKPoint(0f, refY));
            deltaY = y - mappedReferencePoint.Y;
        }

        if (Math.Abs(deltaX) <= float.Epsilon && Math.Abs(deltaY) <= float.Epsilon)
        {
            return viewBoxTransform;
        }

        symbolViewport = SKRect.Create(symbolViewport.Left + deltaX, symbolViewport.Top + deltaY, symbolViewport.Width, symbolViewport.Height);
        return SKMatrix.CreateTranslation(deltaX, deltaY).PreConcat(viewBoxTransform);
    }

    private static SvgSceneNode? CompileEmbeddedImageSceneNode(
        SvgImage svgImage,
        SvgDocument imageDocument,
        SKRect srcRect,
        SKRect destRect,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        SKMatrix parentTotalTransform,
        string? compilationRootKey,
        SvgSceneCompileContext compileContext)
    {
        if (!TryCompile(imageDocument, srcRect, assetLoader, ignoreAttributes, compileContext, out var imageSceneDocument) ||
            imageSceneDocument is null)
        {
            return null;
        }

        var imagePicture = SvgSceneRenderer.Render(imageSceneDocument);
        if (imagePicture is null)
        {
            return null;
        }

        var fragmentTransform = SKMatrix.CreateTranslation(destRect.Left, destRect.Top)
            .PreConcat(SKMatrix.CreateScale(destRect.Width / srcRect.Width, destRect.Height / srcRect.Height));
        var totalTransform = parentTotalTransform.PreConcat(fragmentTransform);

        var node = new SvgSceneNode(
            SvgSceneNodeKind.Fragment,
            element: null,
            elementAddressKey: null,
            elementTypeName: svgImage.GetType().Name,
            compilationRootKey,
            isCompilationRootBoundary: false)
        {
            CompilationStrategy = SvgSceneCompilationStrategy.DirectRetained,
            IsAntialias = PaintingService.IsAntialias(svgImage),
            IsRenderable = true,
            HitTestTargetElement = null,
            LocalModel = imagePicture,
            GeometryBounds = srcRect,
            TransformedBounds = totalTransform.MapRect(srcRect),
            Transform = fragmentTransform,
            TotalTransform = totalTransform
        };
        return node;
    }

    private static SvgSceneNode? CompileEmbeddedSvgDocumentImageSceneNode(
        SvgImage svgImage,
        SvgDocument imageDocument,
        SKRect destClip,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        SKMatrix parentTotalTransform,
        string? compilationRootKey,
        SvgSceneCompileContext compileContext)
    {
        var nestedViewport = SKRect.Create(0f, 0f, destClip.Width, destClip.Height);
        if (!TryCompile(imageDocument, nestedViewport, assetLoader, ignoreAttributes, compileContext, out var imageSceneDocument) ||
            imageSceneDocument is null)
        {
            return null;
        }

        var imagePicture = SvgSceneRenderer.Render(imageSceneDocument);
        if (imagePicture is null)
        {
            return null;
        }

        var fragmentTransform = SKMatrix.CreateTranslation(destClip.Left, destClip.Top);
        var totalTransform = parentTotalTransform.PreConcat(fragmentTransform);

        var node = new SvgSceneNode(
            SvgSceneNodeKind.Fragment,
            element: null,
            elementAddressKey: null,
            elementTypeName: svgImage.GetType().Name,
            compilationRootKey,
            isCompilationRootBoundary: false)
        {
            CompilationStrategy = SvgSceneCompilationStrategy.DirectRetained,
            IsAntialias = PaintingService.IsAntialias(svgImage),
            IsRenderable = true,
            HitTestTargetElement = null,
            LocalModel = imagePicture,
            GeometryBounds = nestedViewport,
            TransformedBounds = totalTransform.MapRect(nestedViewport),
            Transform = fragmentTransform,
            TotalTransform = totalTransform
        };

        return node;
    }

    private static bool ShouldUseReferencedSvgViewport(SvgDocument imageDocument, Uri imageUri)
    {
        if (imageUri.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return HasImplicitDocumentSize(imageDocument);
    }

    private static bool HasImplicitDocumentSize(SvgDocument imageDocument)
    {
        return imageDocument.Width.Type == SvgUnitType.Percentage &&
               imageDocument.Height.Type == SvgUnitType.Percentage &&
               Math.Abs(imageDocument.Width.Value - 100f) <= float.Epsilon &&
               Math.Abs(imageDocument.Height.Value - 100f) <= float.Epsilon;
    }

    private static void ResolveImageAutoSize(SKRect srcRect, bool hasExplicitWidth, bool hasExplicitHeight, ref float width, ref float height)
    {
        if (srcRect.Width <= 0f || srcRect.Height <= 0f)
        {
            return;
        }

        if (!hasExplicitWidth && !hasExplicitHeight)
        {
            width = srcRect.Width;
            height = srcRect.Height;
            return;
        }

        if (!hasExplicitWidth && height > 0f)
        {
            width = height * srcRect.Width / srcRect.Height;
            return;
        }

        if (!hasExplicitHeight && width > 0f)
        {
            height = width * srcRect.Height / srcRect.Width;
        }
    }

    private static SKPicture? CreateDirectImageModel(SKImage image, SKRect srcRect, SKRect destRect)
    {
        var cullRect = CreateLocalCullRect(destRect);
        if (cullRect.IsEmpty)
        {
            return null;
        }

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        canvas.DrawImage(
            image,
            srcRect,
            destRect,
            new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            });

        var picture = recorder.EndRecording();
        return picture.Commands is { Count: > 0 } ? picture : null;
    }

    private static bool TryUseBrokenImagePlaceholder(
        SvgSceneNode node,
        SvgImage svgImage,
        SKRect destRect,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        ISvgAssetLoader assetLoader)
    {
        if (assetLoader is not ISvgBrokenImagePlaceholderOptions { EnableBrokenImagePlaceholders: true } ||
            destRect.IsEmpty ||
            destRect.Width <= 0f ||
            destRect.Height <= 0f)
        {
            return false;
        }

        node.GeometryBounds = destRect;
        node.Transform = TransformsService.ToMatrix(svgImage.Transforms, svgImage, destRect, viewport);
        node.TotalTransform = parentTotalTransform.PreConcat(node.Transform);
        node.TransformedBounds = node.TotalTransform.MapRect(destRect);
        node.Clip = MaskingService.GetClipRect(svgImage.Clip, destRect) ?? destRect;
        node.LocalModel = CreateBrokenImagePlaceholderModel(destRect);
        node.IsRenderable = node.LocalModel is not null;
        return node.IsRenderable;
    }

    private static SKPicture? CreateBrokenImagePlaceholderModel(SKRect destRect)
    {
        var cullRect = CreateLocalCullRect(destRect);
        if (cullRect.IsEmpty)
        {
            return null;
        }

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        var background = new SKPath();
        background.AddRect(destRect);
        canvas.DrawPath(
            background,
            new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = new SKColor(0xF8, 0xF8, 0xF8, 0xFF)
            });

        var border = new SKPath();
        border.AddRect(destRect);
        canvas.DrawPath(
            border,
            new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f,
                IsAntialias = false,
                Color = new SKColor(0x66, 0x66, 0x66, 0xFF)
            });

        var diagonal = new SKPath();
        diagonal.MoveTo(destRect.Left, destRect.Top);
        diagonal.LineTo(destRect.Right, destRect.Bottom);
        diagonal.MoveTo(destRect.Right, destRect.Top);
        diagonal.LineTo(destRect.Left, destRect.Bottom);
        canvas.DrawPath(
            diagonal,
            new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f,
                IsAntialias = false,
                Color = new SKColor(0x99, 0x99, 0x99, 0xFF)
            });

        var picture = recorder.EndRecording();
        return picture.Commands is { Count: > 0 } ? picture : null;
    }

    private static SKRect CreateSourceRect(SvgDocument svgDocument, SKRect imageViewport)
    {
        var size = SvgService.GetDimensions(svgDocument);
        if ((size.Width <= 0f || size.Height <= 0f) && imageViewport.Width > 0f && imageViewport.Height > 0f)
        {
            size = SvgService.GetDimensions(svgDocument, imageViewport);
        }

        return size.Width > 0f && size.Height > 0f
            ? SKRect.Create(0f, 0f, size.Width, size.Height)
            : SKRect.Empty;
    }

    private static bool TryGetActiveSwitchChild(SvgSwitch svgSwitch, out SvgElement? activeChild)
    {
        foreach (var child in svgSwitch.Children)
        {
            if (!child.IsKnownElement())
            {
                continue;
            }

            if (child.PassesConditionalProcessing(DrawAttributes.None))
            {
                activeChild = child;
                return true;
            }
        }

        activeChild = null;
        return false;
    }

    private static T WithUseInstanceStyleScope<T>(SvgElement element, SvgUse useElement, Func<T> factory)
    {
        return element.WithUseInstanceStyleScope(useElement, factory);
    }

    private static void AppendDirectMarkers(
        SvgSceneNode node,
        SvgVisualElement markerElement,
        SKPath path,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        SvgSceneCompileContext compileContext)
    {
        var markerVertices = CreateMarkerVertices(path);
        var markerVertexCount = markerVertices.Count;
        if (markerVertexCount <= 0)
        {
            return;
        }

        var markerParentTotalTransform = node.TotalTransform.IsIdentity ? parentTotalTransform : node.TotalTransform;
        var hitTestTarget = node.HitTestTargetElement ?? node.Element;

        var markerStart = GetEffectiveMarkerReferenceUri(markerElement, "marker-start", static element => element.MarkerStart);
        if (markerStart is not null &&
            !HasRecursiveMarkerReference(markerElement, static element => element.MarkerStart))
        {
            var marker = SvgService.GetReference<SvgMarker>(markerElement, markerStart);
            if (marker is not null)
            {
                var angle = GetMarkerAngle(markerVertices, 0, MarkerPlacement.Start);
                if (marker.Orient.IsAuto && marker.Orient.IsAutoStartReverse)
                {
                    angle += 180f;
                }

                if (TryCompileDirectMarkerNode(
                        marker,
                        markerElement,
                        markerVertices[0].Point,
                        angle,
                        node.GeometryBounds,
                        viewport,
                        markerParentTotalTransform,
                        assetLoader,
                        ignoreAttributes,
                        node.CompilationRootKey,
                        compileContext,
                        out var startMarkerNode) &&
                    startMarkerNode is not null)
                {
                    AssignGeneratedHitTestTarget(startMarkerNode, hitTestTarget);
                    node.AddChild(startMarkerNode);
                }
            }
        }

        var markerMid = GetEffectiveMarkerReferenceUri(markerElement, "marker-mid", static element => element.MarkerMid);
        if (markerMid is not null &&
            markerVertexCount > 2 &&
            !HasRecursiveMarkerReference(markerElement, static element => element.MarkerMid))
        {
            var marker = SvgService.GetReference<SvgMarker>(markerElement, markerMid);
            if (marker is not null)
            {
                for (var i = 1; i <= markerVertexCount - 2; i++)
                {
                    if (TryCompileDirectMarkerNode(
                            marker,
                            markerElement,
                            markerVertices[i].Point,
                            GetMarkerAngle(markerVertices, i, MarkerPlacement.Mid),
                            node.GeometryBounds,
                            viewport,
                            markerParentTotalTransform,
                            assetLoader,
                            ignoreAttributes,
                            node.CompilationRootKey,
                            compileContext,
                            out var midMarkerNode) &&
                        midMarkerNode is not null)
                    {
                        AssignGeneratedHitTestTarget(midMarkerNode, hitTestTarget);
                        node.AddChild(midMarkerNode);
                    }
                }
            }
        }

        var markerEnd = GetEffectiveMarkerReferenceUri(markerElement, "marker-end", static element => element.MarkerEnd);
        if (markerEnd is not null &&
            !HasRecursiveMarkerReference(markerElement, static element => element.MarkerEnd))
        {
            var marker = SvgService.GetReference<SvgMarker>(markerElement, markerEnd);
            if (marker is not null)
            {
                var lastIndex = markerVertexCount - 1;
                if (TryCompileDirectMarkerNode(
                        marker,
                        markerElement,
                        markerVertices[lastIndex].Point,
                        GetMarkerAngle(markerVertices, lastIndex, MarkerPlacement.End),
                        node.GeometryBounds,
                        viewport,
                        markerParentTotalTransform,
                        assetLoader,
                        ignoreAttributes,
                        node.CompilationRootKey,
                        compileContext,
                        out var endMarkerNode) &&
                    endMarkerNode is not null)
                {
                    AssignGeneratedHitTestTarget(endMarkerNode, hitTestTarget);
                    node.AddChild(endMarkerNode);
                }
            }
        }
    }

    private enum MarkerPlacement
    {
        Start,
        Mid,
        End
    }

    private sealed class MarkerVertex
    {
        public MarkerVertex(SKPoint point, bool startsSubpath)
        {
            Point = point;
            StartsSubpath = startsSubpath;
        }

        public SKPoint Point { get; }

        public bool StartsSubpath { get; }

        public SKPoint? IncomingTangent { get; set; }

        public SKPoint? OutgoingTangent { get; set; }
    }

    private static List<MarkerVertex> CreateMarkerVertices(SKPath path)
    {
        var vertices = new List<MarkerVertex>();
        if (path.Commands is not { } commands)
        {
            return vertices;
        }

        var current = default(SKPoint);
        var subpathStart = default(SKPoint);
        var haveCurrent = false;

        foreach (var command in commands)
        {
            switch (command)
            {
                case MoveToPathCommand moveTo:
                    current = new SKPoint(moveTo.X, moveTo.Y);
                    subpathStart = current;
                    haveCurrent = true;
                    AddMarkerVertex(vertices, current, startsSubpath: true);
                    break;

                case LineToPathCommand lineTo when haveCurrent:
                    {
                        var end = new SKPoint(lineTo.X, lineTo.Y);
                        var tangent = Subtract(end, current);
                        AddMarkerSegment(vertices, current, end, tangent, tangent);
                        current = end;
                        break;
                    }

                case QuadToPathCommand quadTo when haveCurrent:
                    {
                        var control = new SKPoint(quadTo.X0, quadTo.Y0);
                        var end = new SKPoint(quadTo.X1, quadTo.Y1);
                        var startTangent = FirstUsableVector(Subtract(control, current), Subtract(end, current));
                        var endTangent = FirstUsableVector(Subtract(end, control), Subtract(end, current));
                        AddMarkerSegment(vertices, current, end, startTangent, endTangent);
                        current = end;
                        break;
                    }

                case CubicToPathCommand cubicTo when haveCurrent:
                    {
                        var control1 = new SKPoint(cubicTo.X0, cubicTo.Y0);
                        var control2 = new SKPoint(cubicTo.X1, cubicTo.Y1);
                        var end = new SKPoint(cubicTo.X2, cubicTo.Y2);
                        var startTangent = FirstUsableVector(
                            Subtract(control1, current),
                            Subtract(control2, current),
                            Subtract(end, current));
                        var endTangent = FirstUsableVector(
                            Subtract(end, control2),
                            Subtract(end, control1),
                            Subtract(end, current));
                        AddMarkerSegment(vertices, current, end, startTangent, endTangent);
                        current = end;
                        break;
                    }

                case ArcToPathCommand arcTo when haveCurrent:
                    {
                        var end = new SKPoint(arcTo.X, arcTo.Y);
                        if (!TryGetArcTangents(current, arcTo, out var startTangent, out var endTangent))
                        {
                            startTangent = Subtract(end, current);
                            endTangent = startTangent;
                        }

                        AddMarkerSegment(vertices, current, end, startTangent, endTangent);
                        current = end;
                        break;
                    }

                case ClosePathCommand when haveCurrent:
                    AddCloseMarkerSegment(vertices, current, subpathStart);
                    current = subpathStart;
                    break;

                case AddPolyPathCommand addPoly:
                    AppendPolyMarkerVertices(vertices, addPoly.Points, addPoly.Close, ref current, ref subpathStart, ref haveCurrent);
                    break;

                case AddRectPathCommand addRect:
                    AppendRectMarkerVertices(vertices, addRect.Rect, ref current, ref subpathStart, ref haveCurrent);
                    break;

                case AddRoundRectPathCommand addRoundRect:
                    AppendRoundRectMarkerVertices(vertices, addRoundRect.Rect, addRoundRect.Rx, addRoundRect.Ry, ref current, ref subpathStart, ref haveCurrent);
                    break;

                case AddOvalPathCommand addOval:
                    AppendOvalMarkerVertices(vertices, addOval.Rect, ref current, ref subpathStart, ref haveCurrent);
                    break;

                case AddCirclePathCommand addCircle:
                    {
                        var radius = addCircle.Radius;
                        AppendOvalMarkerVertices(
                            vertices,
                            SKRect.Create(addCircle.X - radius, addCircle.Y - radius, radius * 2f, radius * 2f),
                            ref current,
                            ref subpathStart,
                            ref haveCurrent);
                        break;
                    }
            }
        }

        return vertices;
    }

    private static void AppendPolyMarkerVertices(
        List<MarkerVertex> vertices,
        IList<SKPoint>? points,
        bool close,
        ref SKPoint current,
        ref SKPoint subpathStart,
        ref bool haveCurrent)
    {
        if (points is not { Count: > 0 })
        {
            return;
        }

        current = points[0];
        subpathStart = current;
        haveCurrent = true;
        AddMarkerVertex(vertices, current, startsSubpath: true);

        for (var i = 1; i < points.Count; i++)
        {
            var end = points[i];
            var tangent = Subtract(end, current);
            AddMarkerSegment(vertices, current, end, tangent, tangent);
            current = end;
        }

        if (close)
        {
            AddCloseMarkerSegment(vertices, current, subpathStart);
            current = subpathStart;
        }
    }

    private static void AppendRectMarkerVertices(
        List<MarkerVertex> vertices,
        SKRect rect,
        ref SKPoint current,
        ref SKPoint subpathStart,
        ref bool haveCurrent)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        var points = new[]
        {
            rect.TopLeft,
            new SKPoint(rect.Right, rect.Top),
            rect.BottomRight,
            new SKPoint(rect.Left, rect.Bottom)
        };
        AppendPolyMarkerVertices(vertices, points, close: true, ref current, ref subpathStart, ref haveCurrent);
    }

    private static void AppendRoundRectMarkerVertices(
        List<MarkerVertex> vertices,
        SKRect rect,
        float rx,
        float ry,
        ref SKPoint current,
        ref SKPoint subpathStart,
        ref bool haveCurrent)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        rx = Math.Min(Math.Abs(rx), rect.Width / 2f);
        ry = Math.Min(Math.Abs(ry), rect.Height / 2f);
        if (rx <= 0f || ry <= 0f)
        {
            AppendRectMarkerVertices(vertices, rect, ref current, ref subpathStart, ref haveCurrent);
            return;
        }

        var kx = rx * 0.55228475f;
        var ky = ry * 0.55228475f;
        current = new SKPoint(rect.Left + rx, rect.Top);
        subpathStart = current;
        haveCurrent = true;
        AddMarkerVertex(vertices, current, startsSubpath: true);

        AddLineMarkerSegment(vertices, ref current, new SKPoint(rect.Right - rx, rect.Top));
        AddCubicMarkerSegment(vertices, ref current, new SKPoint(rect.Right - rx + kx, rect.Top), new SKPoint(rect.Right, rect.Top + ry - ky), new SKPoint(rect.Right, rect.Top + ry));
        AddLineMarkerSegment(vertices, ref current, new SKPoint(rect.Right, rect.Bottom - ry));
        AddCubicMarkerSegment(vertices, ref current, new SKPoint(rect.Right, rect.Bottom - ry + ky), new SKPoint(rect.Right - rx + kx, rect.Bottom), new SKPoint(rect.Right - rx, rect.Bottom));
        AddLineMarkerSegment(vertices, ref current, new SKPoint(rect.Left + rx, rect.Bottom));
        AddCubicMarkerSegment(vertices, ref current, new SKPoint(rect.Left + rx - kx, rect.Bottom), new SKPoint(rect.Left, rect.Bottom - ry + ky), new SKPoint(rect.Left, rect.Bottom - ry));
        AddLineMarkerSegment(vertices, ref current, new SKPoint(rect.Left, rect.Top + ry));
        AddCubicMarkerSegment(vertices, ref current, new SKPoint(rect.Left, rect.Top + ry - ky), new SKPoint(rect.Left + rx - kx, rect.Top), subpathStart);
    }

    private static void AppendOvalMarkerVertices(
        List<MarkerVertex> vertices,
        SKRect rect,
        ref SKPoint current,
        ref SKPoint subpathStart,
        ref bool haveCurrent)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        var cx = (rect.Left + rect.Right) / 2f;
        var cy = (rect.Top + rect.Bottom) / 2f;
        var rx = rect.Width / 2f;
        var ry = rect.Height / 2f;
        var kx = rx * 0.55228475f;
        var ky = ry * 0.55228475f;

        current = new SKPoint(cx + rx, cy);
        subpathStart = current;
        haveCurrent = true;
        AddMarkerVertex(vertices, current, startsSubpath: true);

        AddCubicMarkerSegment(vertices, ref current, new SKPoint(cx + rx, cy + ky), new SKPoint(cx + kx, cy + ry), new SKPoint(cx, cy + ry));
        AddCubicMarkerSegment(vertices, ref current, new SKPoint(cx - kx, cy + ry), new SKPoint(cx - rx, cy + ky), new SKPoint(cx - rx, cy));
        AddCubicMarkerSegment(vertices, ref current, new SKPoint(cx - rx, cy - ky), new SKPoint(cx - kx, cy - ry), new SKPoint(cx, cy - ry));
        AddCubicMarkerSegment(vertices, ref current, new SKPoint(cx + kx, cy - ry), new SKPoint(cx + rx, cy - ky), subpathStart);
    }

    private static void AddLineMarkerSegment(List<MarkerVertex> vertices, ref SKPoint current, SKPoint end)
    {
        var tangent = Subtract(end, current);
        AddMarkerSegment(vertices, current, end, tangent, tangent);
        current = end;
    }

    private static void AddCubicMarkerSegment(List<MarkerVertex> vertices, ref SKPoint current, SKPoint control1, SKPoint control2, SKPoint end)
    {
        var startTangent = FirstUsableVector(
            Subtract(control1, current),
            Subtract(control2, current),
            Subtract(end, current));
        var endTangent = FirstUsableVector(
            Subtract(end, control2),
            Subtract(end, control1),
            Subtract(end, current));
        AddMarkerSegment(vertices, current, end, startTangent, endTangent);
        current = end;
    }

    private static void AddCloseMarkerSegment(List<MarkerVertex> vertices, SKPoint current, SKPoint subpathStart)
    {
        if (vertices.Count <= 0)
        {
            return;
        }

        var tangent = Subtract(subpathStart, current);
        SetOutgoingTangent(vertices[vertices.Count - 1], tangent);
        if (!AreSamePoint(current, subpathStart))
        {
            var closeVertex = AddMarkerVertex(vertices, subpathStart, startsSubpath: false);
            SetIncomingTangent(closeVertex, tangent);
        }
    }

    private static void AddMarkerSegment(
        List<MarkerVertex> vertices,
        SKPoint start,
        SKPoint end,
        SKPoint startTangent,
        SKPoint endTangent)
    {
        if (vertices.Count <= 0 || !AreSamePoint(vertices[vertices.Count - 1].Point, start))
        {
            AddMarkerVertex(vertices, start, startsSubpath: true);
        }

        SetOutgoingTangent(vertices[vertices.Count - 1], startTangent);
        var endVertex = AddMarkerVertex(vertices, end, startsSubpath: false);
        SetIncomingTangent(endVertex, endTangent);
    }

    private static MarkerVertex AddMarkerVertex(List<MarkerVertex> vertices, SKPoint point, bool startsSubpath)
    {
        var vertex = new MarkerVertex(point, startsSubpath);
        vertices.Add(vertex);
        return vertex;
    }

    private static void SetIncomingTangent(MarkerVertex vertex, SKPoint tangent)
    {
        if (IsUsableVector(tangent))
        {
            vertex.IncomingTangent = tangent;
        }
    }

    private static void SetOutgoingTangent(MarkerVertex vertex, SKPoint tangent)
    {
        if (IsUsableVector(tangent))
        {
            vertex.OutgoingTangent = tangent;
        }
    }

    private static float GetMarkerAngle(IReadOnlyList<MarkerVertex> vertices, int index, MarkerPlacement placement)
    {
        var hasIncoming = TryGetIncomingTangent(vertices, index, out var incoming);
        var hasOutgoing = TryGetOutgoingTangent(vertices, index, out var outgoing);

        return placement switch
        {
            MarkerPlacement.Start when hasOutgoing => GetVectorAngle(outgoing),
            MarkerPlacement.Start when hasIncoming => GetVectorAngle(incoming),
            MarkerPlacement.End when hasIncoming => GetVectorAngle(incoming),
            MarkerPlacement.End when hasOutgoing => GetVectorAngle(outgoing),
            MarkerPlacement.Mid when hasIncoming && hasOutgoing => AverageAngles(GetVectorAngle(incoming), GetVectorAngle(outgoing)),
            MarkerPlacement.Mid when hasOutgoing => GetVectorAngle(outgoing),
            MarkerPlacement.Mid when hasIncoming => GetVectorAngle(incoming),
            _ => 0f
        };
    }

    private static bool TryGetIncomingTangent(IReadOnlyList<MarkerVertex> vertices, int index, out SKPoint tangent)
    {
        if (IsUsableVector(vertices[index].IncomingTangent, out tangent))
        {
            return true;
        }

        for (var i = index - 1; i >= 0 && !vertices[i + 1].StartsSubpath; i--)
        {
            if (IsUsableVector(vertices[i].IncomingTangent, out tangent) ||
                IsUsableVector(vertices[i].OutgoingTangent, out tangent))
            {
                return true;
            }
        }

        tangent = default;
        return false;
    }

    private static bool TryGetOutgoingTangent(IReadOnlyList<MarkerVertex> vertices, int index, out SKPoint tangent)
    {
        if (IsUsableVector(vertices[index].OutgoingTangent, out tangent))
        {
            return true;
        }

        for (var i = index + 1; i < vertices.Count && !vertices[i].StartsSubpath; i++)
        {
            if (IsUsableVector(vertices[i].OutgoingTangent, out tangent) ||
                IsUsableVector(vertices[i].IncomingTangent, out tangent))
            {
                return true;
            }
        }

        tangent = default;
        return false;
    }

    private static SKPoint FirstUsableVector(params SKPoint[] vectors)
    {
        for (var i = 0; i < vectors.Length; i++)
        {
            if (IsUsableVector(vectors[i]))
            {
                return vectors[i];
            }
        }

        return default;
    }

    private static bool IsUsableVector(SKPoint vector)
    {
        return Math.Abs(vector.X) > 0.001f ||
               Math.Abs(vector.Y) > 0.001f;
    }

    private static bool IsUsableVector(SKPoint? vector, out SKPoint value)
    {
        if (vector is { } candidate && IsUsableVector(candidate))
        {
            value = candidate;
            return true;
        }

        value = default;
        return false;
    }

    private static SKPoint Subtract(SKPoint point, SKPoint origin)
    {
        return new SKPoint(point.X - origin.X, point.Y - origin.Y);
    }

    private static float GetVectorAngle(SKPoint vector)
    {
        return (float)(Math.Atan2(vector.Y, vector.X) * 180.0 / Math.PI);
    }

    private static float AverageAngles(float angle1, float angle2)
    {
        return angle1 + (NormalizeSignedAngle(angle2 - angle1) / 2f);
    }

    private static float NormalizeSignedAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
        {
            angle -= 360f;
        }
        else if (angle <= -180f)
        {
            angle += 360f;
        }

        return angle;
    }

    private static bool TryGetArcTangents(SKPoint start, ArcToPathCommand arcTo, out SKPoint startTangent, out SKPoint endTangent)
    {
        var end = new SKPoint(arcTo.X, arcTo.Y);
        if (!TryGetArcParameters(start, end, arcTo.Rx, arcTo.Ry, arcTo.XAxisRotate, arcTo.LargeArc, arcTo.Sweep, out var parameters))
        {
            startTangent = default;
            endTangent = default;
            return false;
        }

        var direction = parameters.DeltaAngle < 0f ? -1f : 1f;
        startTangent = GetArcTangent(parameters, parameters.StartAngle, direction);
        endTangent = GetArcTangent(parameters, parameters.StartAngle + parameters.DeltaAngle, direction);
        return true;
    }

    private static SKPoint GetArcTangent(ArcParameters parameters, float angle, float direction)
    {
        var cosTheta = (float)Math.Cos(angle);
        var sinTheta = (float)Math.Sin(angle);
        return new SKPoint(
            ((-parameters.Rx * sinTheta * parameters.CosPhi) - (parameters.Ry * cosTheta * parameters.SinPhi)) * direction,
            ((-parameters.Rx * sinTheta * parameters.SinPhi) + (parameters.Ry * cosTheta * parameters.CosPhi)) * direction);
    }

    private static bool TryGetArcParameters(
        SKPoint start,
        SKPoint end,
        float rx,
        float ry,
        float angle,
        SKPathArcSize largeArc,
        SKPathDirection sweep,
        out ArcParameters parameters)
    {
        parameters = default;

        rx = Math.Abs(rx);
        ry = Math.Abs(ry);
        if (rx <= float.Epsilon || ry <= float.Epsilon || AreSamePoint(start, end))
        {
            return false;
        }

        var phi = angle * (float)Math.PI / 180f;
        var cosPhi = (float)Math.Cos(phi);
        var sinPhi = (float)Math.Sin(phi);

        var dx2 = (start.X - end.X) / 2f;
        var dy2 = (start.Y - end.Y) / 2f;
        var x1p = (cosPhi * dx2) + (sinPhi * dy2);
        var y1p = (-sinPhi * dx2) + (cosPhi * dy2);

        var rxsq = rx * rx;
        var rysq = ry * ry;
        var x1psq = x1p * x1p;
        var y1psq = y1p * y1p;

        var lambda = (x1psq / rxsq) + (y1psq / rysq);
        if (lambda > 1f)
        {
            var factor = (float)Math.Sqrt(lambda);
            rx *= factor;
            ry *= factor;
            rxsq = rx * rx;
            rysq = ry * ry;
        }

        var denominator = (rxsq * y1psq) + (rysq * x1psq);
        if (denominator <= float.Epsilon)
        {
            return false;
        }

        var sign = (largeArc == SKPathArcSize.Large) == (sweep == SKPathDirection.Clockwise) ? -1f : 1f;
        var sq = ((rxsq * rysq) - (rxsq * y1psq) - (rysq * x1psq)) / denominator;
        sq = Math.Max(sq, 0f);
        var coef = sign * (float)Math.Sqrt(sq);
        var cxp = coef * (rx * y1p / ry);
        var cyp = coef * (-ry * x1p / rx);

        var center = new SKPoint(
            (cosPhi * cxp) - (sinPhi * cyp) + ((start.X + end.X) / 2f),
            (sinPhi * cxp) + (cosPhi * cyp) + ((start.Y + end.Y) / 2f));

        var startAngle = (float)Math.Atan2((y1p - cyp) / ry, (x1p - cxp) / rx);
        var endAngle = (float)Math.Atan2((-y1p - cyp) / ry, (-x1p - cxp) / rx);
        var deltaAngle = endAngle - startAngle;
        if (sweep != SKPathDirection.Clockwise && deltaAngle > 0f)
        {
            deltaAngle -= (float)Math.PI * 2f;
        }
        else if (sweep == SKPathDirection.Clockwise && deltaAngle < 0f)
        {
            deltaAngle += (float)Math.PI * 2f;
        }

        parameters = new ArcParameters(center, rx, ry, startAngle, deltaAngle, cosPhi, sinPhi);
        return true;
    }

    private readonly record struct ArcParameters(
        SKPoint Center,
        float Rx,
        float Ry,
        float StartAngle,
        float DeltaAngle,
        float CosPhi,
        float SinPhi);

    private static bool AreSamePoint(SKPoint point1, SKPoint point2)
    {
        return Math.Abs(point1.X - point2.X) <= 0.001f &&
               Math.Abs(point1.Y - point2.Y) <= 0.001f;
    }

    private static bool TryCompileDirectMarkerNode(
        SvgMarker svgMarker,
        SvgVisualElement owner,
        SKPoint referencePoint,
        SKPoint markerPoint1,
        SKPoint markerPoint2,
        bool isStartMarker,
        SKRect contextPaintBounds,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        string? compilationRootKey,
        SvgSceneCompileContext compileContext,
        out SvgSceneNode? node)
    {
        var angle = 0f;
        if (svgMarker.Orient.IsAuto)
        {
            var xDiff = markerPoint2.X - markerPoint1.X;
            var yDiff = markerPoint2.Y - markerPoint1.Y;
            angle = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
            if (isStartMarker && svgMarker.Orient.IsAutoStartReverse)
            {
                angle += 180f;
            }
        }

        return TryCompileDirectMarkerNode(
            svgMarker,
            owner,
            referencePoint,
            angle,
            contextPaintBounds,
            viewport,
            parentTotalTransform,
            assetLoader,
            ignoreAttributes,
            compilationRootKey,
            compileContext,
            out node);
    }

    private static bool TryCompileDirectMarkerNode(
        SvgMarker svgMarker,
        SvgVisualElement owner,
        SKPoint referencePoint,
        SKPoint markerPoint1,
        SKPoint markerPoint2,
        SKPoint markerPoint3,
        SKRect contextPaintBounds,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        string? compilationRootKey,
        SvgSceneCompileContext compileContext,
        out SvgSceneNode? node)
    {
        var xDiff = markerPoint2.X - markerPoint1.X;
        var yDiff = markerPoint2.Y - markerPoint1.Y;
        var angle1 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
        xDiff = markerPoint3.X - markerPoint2.X;
        yDiff = markerPoint3.Y - markerPoint2.Y;
        var angle2 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);

        return TryCompileDirectMarkerNode(
            svgMarker,
            owner,
            referencePoint,
            AverageAngles(angle1, angle2),
            contextPaintBounds,
            viewport,
            parentTotalTransform,
            assetLoader,
            ignoreAttributes,
            compilationRootKey,
            compileContext,
            out node);
    }

    private static bool TryCompileDirectMarkerNode(
        SvgMarker svgMarker,
        SvgVisualElement owner,
        SKPoint referencePoint,
        float angle,
        SKRect contextPaintBounds,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        string? compilationRootKey,
        SvgSceneCompileContext compileContext,
        out SvgSceneNode? node)
    {
        node = null;

        if (!HasMarkerVisualChildren(svgMarker))
        {
            return false;
        }

        var strokeWidth = owner.StrokeWidth.ToDeviceValue(UnitRenderingType.Other, svgMarker, viewport);
        var refX = svgMarker.RefX.ToDeviceValue(UnitRenderingType.Horizontal, svgMarker, viewport);
        var refY = svgMarker.RefY.ToDeviceValue(UnitRenderingType.Vertical, svgMarker, viewport);
        var markerWidth = svgMarker.MarkerWidth.ToDeviceValue(UnitRenderingType.Other, svgMarker, viewport);
        var markerHeight = svgMarker.MarkerHeight.ToDeviceValue(UnitRenderingType.Other, svgMarker, viewport);
        if (markerWidth <= 0f || markerHeight <= 0f)
        {
            return false;
        }

        var markerViewport = SKRect.Create(0f, 0f, markerWidth, markerHeight);
        var viewBoxTransform = HasValidViewBox(svgMarker.ViewBox)
            ? TransformsService.ToMatrix(svgMarker.ViewBox, svgMarker.AspectRatio, 0f, 0f, markerWidth, markerHeight)
            : SKMatrix.Identity;
        var mappedReferencePoint = viewBoxTransform.MapPoint(new SKPoint(refX, refY));

        var markerMatrix = SKMatrix.Identity;
        markerMatrix = markerMatrix.PreConcat(SKMatrix.CreateTranslation(referencePoint.X, referencePoint.Y));
        markerMatrix = markerMatrix.PreConcat(SKMatrix.CreateRotationDegrees(svgMarker.Orient.IsAuto ? angle : svgMarker.Orient.Angle));

        switch (svgMarker.MarkerUnits)
        {
            case SvgMarkerUnits.StrokeWidth:
                markerMatrix = markerMatrix.PreConcat(SKMatrix.CreateScale(strokeWidth, strokeWidth));
                markerMatrix = markerMatrix.PreConcat(SKMatrix.CreateTranslation(-mappedReferencePoint.X, -mappedReferencePoint.Y));
                markerMatrix = markerMatrix.PreConcat(viewBoxTransform);
                break;
            case SvgMarkerUnits.UserSpaceOnUse:
                markerMatrix = markerMatrix.PreConcat(SKMatrix.CreateTranslation(-mappedReferencePoint.X, -mappedReferencePoint.Y));
                markerMatrix = markerMatrix.PreConcat(viewBoxTransform);
                break;
        }

        var transform = TransformsService.ToMatrix(svgMarker.Transforms);
        transform = transform.PreConcat(markerMatrix);

        node = new SvgSceneNode(
            SvgSceneNodeKind.Marker,
            svgMarker,
            compileContext.GetElementAddressKey(svgMarker),
            svgMarker.GetType().Name,
            compilationRootKey,
            isCompilationRootBoundary: false)
        {
            CompilationStrategy = SvgSceneCompilationStrategy.DirectRetained,
            IsRenderable = true,
            IsAntialias = PaintingService.IsAntialias(svgMarker),
            Transform = transform,
            TotalTransform = parentTotalTransform.PreConcat(transform),
            Fill = null,
            Stroke = null
        };
        var ownFeatureFlags = svgMarker.GetOwnCascadedStyleFeatureFlags(GetRequestedCascadedStyleFeatureFlags(svgMarker));
        AssignRetainedVisualState(
            node,
            svgMarker,
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.MixBlendMode),
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.Isolation));
        AssignRetainedResourceKeys(node, svgMarker, compileContext);
        node.IsVisible = true;
        node.IsDisplayNone = false;

        switch (GetMarkerOverflow(svgMarker))
        {
            case SvgOverflow.Auto:
            case SvgOverflow.Visible:
            case SvgOverflow.Inherit:
                break;
            default:
                node.InnerClip = GetMarkerInnerClip(markerViewport, viewBoxTransform);
                break;
        }

        var hasCompiledChild = false;
        using (compileContext.PushContextPaint(owner, contextPaintBounds))
        {
            for (var i = 0; i < svgMarker.Children.Count; i++)
            {
                if (svgMarker.Children[i] is not SvgVisualElement markerChild)
                {
                    continue;
                }

                _ = compileContext.GetChildElementAddressKey(svgMarker, i);
                var childNode = CompileElementNode(
                    markerChild,
                    viewport,
                    node.TotalTransform,
                    assetLoader,
                    DrawAttributes.Display | DrawAttributes.Markers | ignoreAttributes,
                    compilationRootKey,
                    createOwnCompilationRootBoundary: false,
                    compileContext);

                if (childNode is null)
                {
                    continue;
                }

                ResetGeneratedDisplayState(childNode);
                node.AddChild(childNode, svgMarker.Children.Count);
                hasCompiledChild = true;
            }
        }

        if (!hasCompiledChild)
        {
            node = null;
            return false;
        }

        FinalizeDirectStructuralBounds(node, parentTotalTransform);
        return true;
    }

    private static SvgOverflow GetMarkerOverflow(SvgMarker svgMarker)
    {
        return TryGetSpecifiedOverflow(svgMarker, out var overflow)
            ? overflow
            : svgMarker.Overflow;
    }

    private static bool TryGetSpecifiedOverflow(SvgElement element, out SvgOverflow overflow)
    {
        if (element.TryGetOwnCascadedStyleDeclarationValue("overflow", out var styleOverflow) &&
            TryParseOverflow(styleOverflow, out overflow))
        {
            return true;
        }

        if (SvgService.TryGetAttribute(element, "overflow", out var attributeOverflow) &&
            TryParseOverflow(attributeOverflow, out overflow))
        {
            return true;
        }

        overflow = SvgOverflow.Hidden;
        return false;
    }

    private static bool TryParseOverflow(string value, out SvgOverflow overflow)
    {
        try
        {
            if (new SvgOverflowConverter().ConvertFromString(value) is SvgOverflow parsedOverflow)
            {
                overflow = parsedOverflow;
                return true;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or NotSupportedException)
        {
        }

        overflow = SvgOverflow.Hidden;
        return false;
    }

    private static bool HasValidViewBox(SvgViewBox viewBox)
    {
        return viewBox != SvgViewBox.Empty &&
               viewBox.Width > 0f &&
               viewBox.Height > 0f;
    }

    private static SKRect GetMarkerInnerClip(SKRect markerViewport, SKMatrix viewBoxTransform)
    {
        return viewBoxTransform.TryInvert(out var inverse)
            ? inverse.MapRect(markerViewport)
            : markerViewport;
    }

    private static void ResetGeneratedDisplayState(SvgSceneNode node)
    {
        node.IsDisplayNone = false;

        for (var i = 0; i < node.Children.Count; i++)
        {
            ResetGeneratedDisplayState(node.Children[i]);
        }
    }

    private static bool HasMarkerVisualChildren(SvgMarker svgMarker)
    {
        for (var i = 0; i < svgMarker.Children.Count; i++)
        {
            if (svgMarker.Children[i] is SvgVisualElement)
            {
                return true;
            }
        }

        return false;
    }

    private static Uri? GetEffectiveMarkerReferenceUri(
        SvgVisualElement markerElement,
        string attributeName,
        Func<SvgMarkerElement, Uri?> localSelector)
    {
        if (TryGetOwnMarkerReferenceDeclaration(markerElement, attributeName, out var ownReference))
        {
            return ownReference;
        }

        if (GetComputedMarkerReferenceUri(markerElement, attributeName) is { } computedReference)
        {
            return computedReference;
        }

        if (markerElement is SvgMarkerElement svgMarkerElement && localSelector(svgMarkerElement) is { } localReference)
        {
            return localReference;
        }

        return TryGetUriAttribute(markerElement, attributeName);
    }

    private static bool TryGetOwnMarkerReferenceDeclaration(
        SvgElement markerElement,
        string attributeName,
        out Uri? reference)
    {
        if (TryGetOwnMarkerReferenceDeclaration(markerElement, attributeName, out reference, useComputedCssWideValue: true))
        {
            return true;
        }

        return TryGetOwnMarkerReferenceDeclaration(markerElement, "marker", out reference, useComputedCssWideValue: true);
    }

    private static bool TryGetOwnMarkerReferenceDeclaration(
        SvgElement markerElement,
        string propertyName,
        out Uri? reference,
        bool useComputedCssWideValue)
    {
        reference = null;
        if (!markerElement.TryGetOwnCascadedCssDeclarationValue(propertyName, out var rawValue) ||
            string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var normalizedValue = rawValue.Trim();
        if (string.Equals(normalizedValue, "initial", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedValue, "none", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (useComputedCssWideValue &&
            (string.Equals(normalizedValue, "inherit", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(normalizedValue, "unset", StringComparison.OrdinalIgnoreCase)))
        {
            reference = GetComputedMarkerReferenceUri(markerElement, propertyName);
            return true;
        }

        if (!SvgComputedStyleMetadata.For(propertyName).IsValid(normalizedValue))
        {
            return false;
        }

        reference = TryCreateMarkerReferenceUri(normalizedValue);
        return reference is not null;
    }

    private static Uri? GetComputedMarkerReferenceUri(SvgElement markerElement, string attributeName)
    {
        return markerElement.ComputedStyle.TryGetPropertyValue(attributeName, out var rawValue)
            ? TryCreateMarkerReferenceUri(rawValue)
            : null;
    }

    private static bool HasMarkerReference(SvgVisualElement markerElement)
    {
        return GetEffectiveMarkerReferenceUri(markerElement, "marker-start", static element => element.MarkerStart) is not null ||
               GetEffectiveMarkerReferenceUri(markerElement, "marker-mid", static element => element.MarkerMid) is not null ||
               GetEffectiveMarkerReferenceUri(markerElement, "marker-end", static element => element.MarkerEnd) is not null;
    }

    internal static bool SubtreeMayContainMarkerReferenceDeclarations(SvgElement? element)
    {
        if (element is null)
        {
            return false;
        }

        if (HasOwnMarkerReferenceDeclarationCandidate(element))
        {
            return true;
        }

        for (var i = 0; i < element.Children.Count; i++)
        {
            if (SubtreeMayContainMarkerReferenceDeclarations(element.Children[i]))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool HasOwnMarkerReferenceDeclarationCandidate(SvgElement element)
    {
        return HasFeatureFlag(
            element.GetOwnCascadedStyleFeatureFlags(SvgCascadedStyleFeatureFlags.MarkerReference),
            SvgCascadedStyleFeatureFlags.MarkerReference);
    }

    internal static bool SubtreeMayContainClipPathDeclarations(SvgElement? element)
    {
        if (element is null)
        {
            return false;
        }

        if (HasOwnClipPathDeclarationCandidate(element))
        {
            return true;
        }

        for (var i = 0; i < element.Children.Count; i++)
        {
            if (SubtreeMayContainClipPathDeclarations(element.Children[i]))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool HasOwnClipPathDeclarationCandidate(SvgElement element)
    {
        return (element.TryGetOwnCascadedStyleValue("clip-path", out var styleValue) && !string.IsNullOrWhiteSpace(styleValue)) ||
               (element.TryGetAttribute("clip-path", out var attributeValue) && !string.IsNullOrWhiteSpace(attributeValue));
    }

    private static SvgCascadedStyleFeatureFlags GetRequestedCascadedStyleFeatureFlags(SvgElement element)
    {
        var flags = SvgCascadedStyleFeatureFlags.None;

        if (element is SvgVisualElement || element is SvgUse || element.Children.Count > 0)
        {
            flags |= SvgCascadedStyleFeatureFlags.MarkerReference;
        }

        if (element is SvgVisualElement || element.IsContainerElement())
        {
            flags |= SvgCascadedStyleFeatureFlags.MixBlendMode;
        }

        if (element.IsContainerElement())
        {
            flags |= SvgCascadedStyleFeatureFlags.Isolation;
        }

        return flags;
    }

    private static SvgCascadedStyleFeatureFlags FilterRequestedCascadedStyleFeatureFlags(
        SvgSceneCompileContext compileContext,
        SvgCascadedStyleFeatureFlags requestedFeatureFlags)
    {
        return requestedFeatureFlags & compileContext.ActiveDocumentCascadedStyleFeatureFlags;
    }

    private static bool HasFeatureFlag(
        SvgCascadedStyleFeatureFlags flags,
        SvgCascadedStyleFeatureFlags flag)
    {
        return (flags & flag) != 0;
    }

    private static bool HasRecursiveMarkerReference(
        SvgVisualElement markerElement,
        Func<SvgMarkerElement, Uri?> localSelector)
    {
        return markerElement is SvgMarkerElement svgMarkerElement &&
               SvgService.HasRecursiveReference(svgMarkerElement, localSelector, new HashSet<Uri>());
    }

    private static Uri? TryGetUriAttribute(SvgElement element, string attributeName)
    {
        if (!element.TryGetAttribute(attributeName, out var rawValue) ||
            string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return TryCreateMarkerReferenceUri(rawValue);
    }

    private static Uri? TryCreateMarkerReferenceUri(string rawValue)
    {
        var normalizedValue = rawValue.Trim();
        if (string.Equals(normalizedValue, "none", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedValue, "inherit", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (normalizedValue.StartsWith("url(", StringComparison.OrdinalIgnoreCase) &&
            normalizedValue.EndsWith(")", StringComparison.Ordinal))
        {
            normalizedValue = normalizedValue.Substring(4, normalizedValue.Length - 5).Trim();
        }

        if (normalizedValue.Length >= 2 &&
            ((normalizedValue[0] == '\'' && normalizedValue[normalizedValue.Length - 1] == '\'') ||
             (normalizedValue[0] == '"' && normalizedValue[normalizedValue.Length - 1] == '"')))
        {
            normalizedValue = normalizedValue.Substring(1, normalizedValue.Length - 2);
        }

        return string.IsNullOrWhiteSpace(normalizedValue)
            ? null
            : new Uri(normalizedValue, UriKind.RelativeOrAbsolute);
    }

    private static bool HasFeatures(SvgElement element, DrawAttributes ignoreAttributes)
    {
        return element.PassesConditionalProcessing(ignoreAttributes);
    }

    internal static SvgSceneDocument? CompileTemporaryChildrenScene(
        SvgElement owner,
        SvgElementCollection children,
        SKRect cullRect,
        SKRect viewport,
        SKMatrix rootTransform,
        float opacity,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes)
    {
        var compileContext = new SvgSceneCompileContext();
        _ = compileContext.TryEnter(owner.OwnerDocument, out var documentKey);

        var root = new SvgSceneNode(
            SvgSceneNodeKind.Group,
            owner,
            elementAddressKey: null,
            owner.GetType().Name,
            compilationRootKey: null,
            isCompilationRootBoundary: false)
        {
            CompilationStrategy = SvgSceneCompilationStrategy.DirectRetained,
            IsRenderable = true,
            IsAntialias = true,
            Transform = rootTransform,
            TotalTransform = rootTransform,
            Fill = null,
            Stroke = null,
            HitTestTargetElement = null,
            Opacity = SvgScenePaintingService.GetOpacityPaint(opacity),
            OpacityValue = ignoreAttributes.HasFlag(DrawAttributes.Opacity)
                ? 1f
                : SvgScenePaintingService.AdjustSvgOpacity(opacity)
        };

        var ownFeatureFlags = owner.GetOwnCascadedStyleFeatureFlags(GetRequestedCascadedStyleFeatureFlags(owner));
        AssignRetainedVisualState(
            root,
            owner,
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.MixBlendMode),
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.Isolation));
        AssignRetainedResourceKeys(root, owner, compileContext);

        try
        {
            for (var i = 0; i < children.Count; i++)
            {
                if (CompileElementNode(
                        children[i],
                        viewport,
                        root.TotalTransform,
                        assetLoader,
                        ignoreAttributes,
                        compilationRootKey: null,
                        createOwnCompilationRootBoundary: false,
                        compileContext) is { } childNode)
                {
                    root.AddChild(childNode, children.Count);
                }
            }
        }
        finally
        {
            compileContext.Exit(documentKey);
        }

        FinalizeDirectStructuralBounds(root, SKMatrix.Identity);
        var sceneDocument = new SvgSceneDocument(
            owner.OwnerDocument,
            GetEffectiveDocumentCullRect(cullRect, root),
            viewport,
            root,
            assetLoader,
            ignoreAttributes,
            owner.OwnerDocument is null
                ? false
                : compileContext.MayContainMarkerReferenceDeclarations(owner.OwnerDocument));

        // Temporary pattern scenes use a synthetic root opacity that should not be
        // replaced by the owner's own element opacity during scene-document initialization.
        root.Opacity = SvgScenePaintingService.GetOpacityPaint(opacity);
        root.OpacityValue = ignoreAttributes.HasFlag(DrawAttributes.Opacity)
            ? 1f
            : SvgScenePaintingService.AdjustSvgOpacity(opacity);

        return sceneDocument;
    }

    private static HashSet<Uri>? CreateReferences(SvgElement element)
    {
        return SvgService.ExtendImageReferences(null, element.OwnerDocument);
    }

    internal static bool TryGetDirectVisualPath(SvgElement element, SKRect viewport, out SKPath? path)
    {
        return SvgGeometryService.TryCreateEquivalentPath(element, viewport, out path);
    }

    private static bool TryGetDirectVisualElement(SvgElement element, out SvgVisualElement? visualElement)
    {
        visualElement = element switch
        {
            SvgPath svgPath => svgPath,
            SvgRectangle svgRectangle => svgRectangle,
            SvgCircle svgCircle => svgCircle,
            SvgEllipse svgEllipse => svgEllipse,
            SvgLine svgLine => svgLine,
            SvgPolyline svgPolyline => svgPolyline,
            SvgPolygon svgPolygon => svgPolygon,
            _ => null
        };

        return visualElement is not null;
    }

    private static SKPath? CreateDirectPathVisual(
        SvgVisualElement visualElement,
        SKPath path,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        SvgSceneCompileContext compileContext,
        out SKPaint? fill,
        out SKPaint? stroke,
        out bool canKeepRenderable)
    {
        canKeepRenderable = true;

        fill = default;
        stroke = default;
        var canDrawFill = true;
        var canDrawStroke = true;

        if (SvgScenePaintingService.IsValidFill(visualElement))
        {
            fill = compileContext.TryGetCachedSolidFillPaint(visualElement, ignoreAttributes, out var cachedFill)
                ? cachedFill
                : SvgScenePaintingService.GetFillPaint(
                    visualElement,
                    geometryBounds,
                    assetLoader,
                    ignoreAttributes,
                    compileContext.ContextPaint,
                    compileContext.GradientPaintCache);
            if (fill is null)
            {
                canDrawFill = false;
            }
        }

        if (SvgScenePaintingService.IsValidStroke(visualElement, geometryBounds))
        {
            stroke = SvgScenePaintingService.GetStrokePaint(
                visualElement,
                geometryBounds,
                assetLoader,
                ignoreAttributes,
                compileContext.ContextPaint,
                path,
                compileContext.GradientPaintCache);
            if (stroke is null)
            {
                canDrawStroke = false;
            }
        }

        if (canDrawFill && !canDrawStroke && visualElement.Stroke is not SvgContextPaintServer)
        {
            canKeepRenderable = false;
            return null;
        }

        if (fill is null && stroke is null)
        {
            return null;
        }

        var cullRect = CreateLocalCullRect(geometryBounds);
        if (cullRect.IsEmpty)
        {
            return null;
        }

        return path;
    }

    private static SKRect GetEffectiveDocumentCullRect(SKRect cullRect, SvgSceneNode rootNode)
    {
        if (HasPositiveArea(cullRect))
        {
            return cullRect;
        }

        var renderableBounds = SvgSceneNodeBoundsService.GetPixelAlignedBounds(
            SvgSceneNodeBoundsService.GetRenderablePaintBounds(rootNode));
        if (HasPositiveArea(renderableBounds))
        {
            return renderableBounds;
        }

        var transformedBounds = SvgSceneNodeBoundsService.GetPixelAlignedBounds(rootNode.TransformedBounds);
        return HasPositiveArea(transformedBounds)
            ? transformedBounds
            : cullRect;
    }

    private static bool HasPositiveArea(SKRect rect)
    {
        return rect.Width > 0f && rect.Height > 0f;
    }

    private static bool ShouldCompileDomChildren(SvgElement element)
    {
        return element is SvgFragment or SvgGroup or SvgAnchor;
    }

    private static void AssignGeneratedHitTestTarget(SvgSceneNode node, SvgElement? hitTestTargetElement)
    {
        node.HitTestTargetElement = hitTestTargetElement;

        for (var i = 0; i < node.Children.Count; i++)
        {
            AssignGeneratedHitTestTarget(node.Children[i], hitTestTargetElement);
        }
    }

    private static SvgElement? GetDefaultHitTestTargetElement(SvgSceneNode node, SvgElement? element)
    {
        if (element is null)
        {
            return null;
        }

        return node.Kind switch
        {
            SvgSceneNodeKind.Path => element,
            SvgSceneNodeKind.Shape => element,
            SvgSceneNodeKind.Text => element,
            SvgSceneNodeKind.Image => element,
            SvgSceneNodeKind.Use => element,
            SvgSceneNodeKind.Fragment => null,
            SvgSceneNodeKind.Group => null,
            SvgSceneNodeKind.Anchor => null,
            SvgSceneNodeKind.Switch => null,
            SvgSceneNodeKind.Marker => null,
            SvgSceneNodeKind.Mask => null,
            SvgSceneNodeKind.Container => null,
            _ => node.HasLocalVisuals || node.HitTestPath is not null || node.SupportsFillHitTest || node.SupportsStrokeHitTest
                ? element
                : null
        };
    }

    private static SvgElement? GetResolvedPaintServerElement(SvgElement owner, SvgPaintServer? server)
    {
        if (!MayResolvePaintServerResource(server))
        {
            return null;
        }

        var resolvedServer = server is SvgDeferredPaintServer
            ? SvgDeferredPaintServer.TryGet<SvgPaintServer>(server, owner)
            : server;
        var paintServerElement = resolvedServer as SvgElement;
        return IsPaintServerResource(resolvedServer) &&
               paintServerElement is not null &&
               paintServerElement.PassesConditionalProcessing(DrawAttributes.None)
            ? paintServerElement
            : null;
    }

    private static bool MayResolvePaintServerResource(SvgPaintServer? server)
    {
        if (server is null ||
            server == SvgPaintServer.None ||
            server == SvgPaintServer.Inherit ||
            server == SvgPaintServer.NotSet ||
            server is SvgColourServer ||
            server is SvgContextPaintServer)
        {
            return false;
        }

        return server is not SvgDeferredPaintServer deferredServer ||
               IsDeferredPaintServerReference(deferredServer);
    }

    private static bool IsPaintServerResource(SvgPaintServer? server)
    {
        return server is SvgGradientServer or SvgPatternServer;
    }

    private static bool IsDeferredPaintServerReference(SvgDeferredPaintServer deferredServer)
    {
        var value = deferredServer.DeferredId;
        if (string.IsNullOrEmpty(value) ||
            !TryGetTrimmedRange(value, out var start, out var length))
        {
            return false;
        }

        return value[start] == '#' ||
               StartsWithRange(value, start, length, "url(");
    }

    private static bool HasPaintServerReference(SvgElement owner, SvgPaintServer? server)
    {
        return GetResolvedPaintServerElement(owner, server) is not null;
    }

    private static SvgElement? ResolveReference(SvgElement owner, Uri? uri)
    {
        if (uri is null)
        {
            return null;
        }

        var referencedElement = uri.IsAbsoluteUri
            ? SvgService.GetReference<SvgElement>(owner, uri)
            : owner.OwnerDocument?.GetElementById(uri.ToString());
        return referencedElement is not null &&
               referencedElement.PassesConditionalProcessing(DrawAttributes.None)
            ? referencedElement
            : null;
    }

    private static string? TryGetResourceKey(SvgElement owner, Uri? uri, Func<SvgElement?, string?>? getElementAddressKey = null)
    {
        return (getElementAddressKey ?? TryGetElementAddressKey)(ResolveReference(owner, uri));
    }

    private static string? TryGetClipResourceKey(SvgElement? element, Func<SvgElement?, string?>? getElementAddressKey = null)
    {
        if (element is null ||
            !IsClipPathApplicableElement(element))
        {
            return null;
        }

        var clipUri = GetClipPathReferenceUri(element);
        if (clipUri is null)
        {
            return null;
        }

        var svgClipPath = SvgService.GetReference<SvgClipPath>(element, clipUri);
        return svgClipPath is not null &&
               svgClipPath.PassesConditionalProcessing(DrawAttributes.None)
            ? (getElementAddressKey ?? TryGetElementAddressKey)(svgClipPath)
            : null;
    }

    private static string? TryGetMaskResourceKey(SvgElement element, Func<SvgElement?, string?>? getElementAddressKey = null)
    {
        var maskUri = GetReferenceUri(element, "mask");
        if (maskUri is null)
        {
            return null;
        }

        var svgMask = SvgService.GetReference<SvgMask>(element, maskUri);
        return svgMask is not null &&
               svgMask.PassesConditionalProcessing(DrawAttributes.None)
            ? (getElementAddressKey ?? TryGetElementAddressKey)(svgMask)
            : null;
    }

    private static bool HasMaskReference(SvgElement element)
    {
        return GetReferenceUri(element, "mask") is not null;
    }

    internal static void AssignRetainedResourceKeys(SvgSceneNode node, SvgElement? element, Func<SvgElement?, string?>? getElementAddressKey = null)
    {
        node.ClipResourceKey = null;
        node.MaskResourceKey = null;
        node.FilterResourceKey = null;

        if (element is not null &&
            IsClipPathApplicableElement(element))
        {
            node.ClipResourceKey = TryGetClipResourceKey(element, getElementAddressKey);
        }

        if (element is SvgMask)
        {
            node.MaskResourceKey = TryGetMaskResourceKey(element, getElementAddressKey);
            return;
        }

        if (element is not SvgVisualElement visualElement)
        {
            return;
        }

        node.MaskResourceKey = TryGetMaskResourceKey(visualElement, getElementAddressKey);
        node.FilterResourceKey = TryGetResourceKey(visualElement, SvgSceneFilterContext.GetFilterReferenceUri(visualElement), getElementAddressKey);
    }

    private static void AssignRetainedResourceKeys(
        SvgSceneNode node,
        SvgElement? element,
        SvgSceneCompileContext compileContext)
    {
        node.ClipResourceKey = null;
        node.MaskResourceKey = null;
        node.FilterResourceKey = null;

        if (compileContext.ActiveDocumentMayContainClipPathDeclarations &&
            element is not null &&
            IsClipPathApplicableElement(element))
        {
            node.ClipResourceKey = compileContext.GetClipResourceKey(element);
        }

        if (element is SvgMask)
        {
            if (compileContext.ActiveDocumentMayContainMaskDeclarations)
            {
                node.MaskResourceKey = compileContext.GetMaskResourceKey(element);
            }

            return;
        }

        if (element is not SvgVisualElement visualElement)
        {
            return;
        }

        if (compileContext.ActiveDocumentMayContainMaskDeclarations)
        {
            node.MaskResourceKey = compileContext.GetMaskResourceKey(visualElement);
        }

        if (compileContext.ActiveDocumentMayContainFilterDeclarations)
        {
            node.FilterResourceKey = compileContext.GetFilterResourceKey(visualElement);
        }
    }

    internal static void AssignRetainedVisualState(
        SvgSceneNode node,
        SvgElement? element,
        bool mayContainMixBlendModeDeclarations = true,
        bool mayContainIsolationDeclarations = true)
    {
        node.PointerEvents = SvgPointerEvents.VisiblePainted;
        node.IsVisible = true;
        node.IsDisplayNone = false;
        node.Cursor = null;
        node.CreatesBackgroundLayer = false;
        node.BackgroundClip = null;
        node.IsIsolationGroup = false;
        node.BlendModePaint = null;

        if (element is not null &&
            TryGetCursorAttribute(element, out var cursor))
        {
            node.Cursor = cursor;
        }

        if (element is SvgVisualElement visualElement)
        {
            node.PointerEvents = visualElement.PointerEvents;
            node.IsVisible = visualElement.Visible;
            node.IsDisplayNone = string.Equals(visualElement.Display?.Trim(), "none", StringComparison.OrdinalIgnoreCase);
        }

        if (element is not null &&
            element.IsContainerElement() &&
            TryParseEnableBackground(element, out var backgroundClip))
        {
            node.CreatesBackgroundLayer = true;
            node.BackgroundClip = backgroundClip;
        }

        if (mayContainMixBlendModeDeclarations &&
            element is not null &&
            TryParseMixBlendMode(element, out var blendMode))
        {
            node.BlendModePaint = new SKPaint
            {
                BlendMode = blendMode
            };
        }

        if (mayContainIsolationDeclarations &&
            element is not null &&
            element.IsContainerElement() &&
            TryParseIsolation(element))
        {
            node.IsIsolationGroup = true;
        }
    }

    private static bool TryParseIsolation(SvgElement element)
    {
        return TryGetDeclaredComputedStyleValue(element, SvgComputedStyleMetadata.Isolation, out var rawValue) &&
               SvgComputedStyleMetadata.TryParseIsolation(rawValue, out var isolation) &&
               isolation == SvgIsolation.Isolate;
    }

    private static bool TryParseMixBlendMode(SvgElement element, out SKBlendMode blendMode)
    {
        blendMode = SKBlendMode.SrcOver;
        if (!TryGetDeclaredComputedStyleValue(element, SvgComputedStyleMetadata.MixBlendMode, out var rawValue) ||
            !SvgComputedStyleMetadata.TryParseMixBlendMode(rawValue, out var value))
        {
            return false;
        }

        switch (value)
        {
            case SvgMixBlendMode.Multiply:
                blendMode = SKBlendMode.Multiply;
                return true;
            case SvgMixBlendMode.Screen:
                blendMode = SKBlendMode.Screen;
                return true;
            case SvgMixBlendMode.Overlay:
                blendMode = SKBlendMode.Overlay;
                return true;
            case SvgMixBlendMode.Darken:
                blendMode = SKBlendMode.Darken;
                return true;
            case SvgMixBlendMode.Lighten:
                blendMode = SKBlendMode.Lighten;
                return true;
            case SvgMixBlendMode.ColorDodge:
                blendMode = SKBlendMode.ColorDodge;
                return true;
            case SvgMixBlendMode.ColorBurn:
                blendMode = SKBlendMode.ColorBurn;
                return true;
            case SvgMixBlendMode.HardLight:
                blendMode = SKBlendMode.HardLight;
                return true;
            case SvgMixBlendMode.SoftLight:
                blendMode = SKBlendMode.SoftLight;
                return true;
            case SvgMixBlendMode.Difference:
                blendMode = SKBlendMode.Difference;
                return true;
            case SvgMixBlendMode.Exclusion:
                blendMode = SKBlendMode.Exclusion;
                return true;
            case SvgMixBlendMode.Hue:
                blendMode = SKBlendMode.Hue;
                return true;
            case SvgMixBlendMode.Saturation:
                blendMode = SKBlendMode.Saturation;
                return true;
            case SvgMixBlendMode.Color:
                blendMode = SKBlendMode.Color;
                return true;
            case SvgMixBlendMode.Luminosity:
                blendMode = SKBlendMode.Luminosity;
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetDeclaredComputedStyleValue(
        SvgElement element,
        SvgComputedStyleMetadata metadata,
        out string value)
    {
        value = string.Empty;
        if (!element.TryGetOwnCascadedStyleValue(metadata.Name, out var rawValue))
        {
            return false;
        }

        var normalizedValue = rawValue.Trim();
        if (normalizedValue.Length == 0)
        {
            return false;
        }

        if (string.Equals(normalizedValue, "inherit", StringComparison.OrdinalIgnoreCase))
        {
            return element.ComputedStyle.TryGetPropertyValue(metadata.Name, out value) &&
                   !IsInitialComputedStyleValue(metadata, value);
        }

        if (string.Equals(normalizedValue, "initial", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedValue, "unset", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!metadata.IsValid(normalizedValue) ||
            IsInitialComputedStyleValue(metadata, normalizedValue))
        {
            return false;
        }

        value = normalizedValue;
        return true;
    }

    private static bool IsInitialComputedStyleValue(SvgComputedStyleMetadata metadata, string value)
    {
        return metadata.InitialValue is not null &&
               string.Equals(value, metadata.InitialValue, StringComparison.OrdinalIgnoreCase);
    }

    private static Uri? GetUriAttribute(SvgElement element, string name)
    {
        return GetReferenceUri(element, name);
    }

    private static Uri? GetClipPathReferenceUri(SvgElement element)
    {
        if ((!element.TryGetOwnCascadedStyleValue("clip-path", out var value) &&
             !element.TryGetAttribute("clip-path", out value)) ||
            string.IsNullOrEmpty(value) ||
            !TryGetTrimmedRange(value, out var start, out var length))
        {
            return null;
        }

        if (StartsWithRange(value, start, length, "url(") ||
            value[start] == '#')
        {
            return TryCreateMarkerReferenceUri(value);
        }

        return null;
    }

    private static bool IsClipPathApplicableElement(SvgElement element)
    {
        return element is SvgVisualElement || element.IsContainerElement();
    }

    private static Uri? GetReferenceUri(SvgElement element, string name)
    {
        if ((!element.TryGetOwnCascadedStyleValue(name, out var value) &&
             !element.TryGetAttribute(name, out value)) ||
            string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TryCreateMarkerReferenceUri(value);
    }

    private static bool TryGetTrimmedRange(string value, out int start, out int length)
    {
        start = 0;
        var end = value.Length - 1;

        while (start <= end && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            end--;
        }

        length = end - start + 1;
        return length > 0;
    }

    private static bool StartsWithRange(string value, int start, int length, string expected)
    {
        return length >= expected.Length &&
               string.Compare(value, start, expected, 0, expected.Length, StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static bool TryGetCursorAttribute(SvgElement element, out string? cursor)
    {
        cursor = null;
        if (!element.TryGetAttribute("cursor", out var cursorValue) ||
            string.IsNullOrWhiteSpace(cursorValue))
        {
            return false;
        }

        var normalizedCursor = cursorValue.Trim();
        if (string.Equals(normalizedCursor, "inherit", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        cursor = normalizedCursor;
        return true;
    }

    private static bool TryParseEnableBackground(SvgElement element, out SKRect? clip)
    {
        clip = null;
        if ((!element.TryGetOwnCascadedStyleValue("enable-background", out var enableBackground) &&
             !element.TryGetAttribute("enable-background", out enableBackground)) ||
            string.IsNullOrWhiteSpace(enableBackground))
        {
            return false;
        }

        enableBackground = enableBackground.Trim();
        if (enableBackground.Equals("accumulate", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!enableBackground.StartsWith("new", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (enableBackground.Length <= 3)
        {
            return true;
        }

        if (!char.IsWhiteSpace(enableBackground[3]) && enableBackground[3] != ',')
        {
            return false;
        }

        var parts = enableBackground.Substring(4, enableBackground.Length - 4)
            .Split(new[] { ' ', '\t', '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 4)
        {
            return false;
        }

        var values = new float[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                !IsFinite(value))
            {
                return false;
            }

            values[i] = value;
        }

        if (values[2] < 0f || values[3] < 0f)
        {
            return false;
        }

        clip = SKRect.Create(values[0], values[1], values[2], values[3]);
        return true;
    }

    private static bool IsFinite(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);

    private static List<string> PruneCompilationRoots(SvgSceneDocument sceneDocument, IReadOnlyCollection<string> compilationRootKeys)
    {
        var nodes = new List<SvgSceneNode>(compilationRootKeys.Count);
        foreach (var compilationRootKey in compilationRootKeys)
        {
            if (sceneDocument.TryGetCompilationRoot(compilationRootKey, out var node) && node is not null)
            {
                nodes.Add(node);
            }
        }

        nodes.Sort(static (left, right) => GetDepth(left).CompareTo(GetDepth(right)));

        var result = new List<string>(nodes.Count);
        var selected = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            var skip = false;
            for (var current = node.Parent; current is not null; current = current.Parent)
            {
                if (current.IsCompilationRootBoundary &&
                    !string.IsNullOrWhiteSpace(current.CompilationRootKey) &&
                    selected.Contains(current.CompilationRootKey!))
                {
                    skip = true;
                    break;
                }
            }

            if (skip || string.IsNullOrWhiteSpace(node.CompilationRootKey) || !selected.Add(node.CompilationRootKey!))
            {
                continue;
            }

            result.Add(node.CompilationRootKey!);
        }

        return result;
    }

    private static int GetDepth(SvgSceneNode node)
    {
        var depth = 0;
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            depth++;
        }

        return depth;
    }

    private static SKRect CreateLocalCullRect(SKRect bounds)
    {
        if (bounds.IsEmpty)
        {
            return SKRect.Empty;
        }

        return SKRect.Create(
            0f,
            0f,
            Math.Abs(bounds.Left) + bounds.Width,
            Math.Abs(bounds.Top) + bounds.Height);
    }

    internal static SvgSceneNode? CompileMaskNode(
        SvgMask svgMask,
        SKRect targetBounds,
        SKRect compilationViewport,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes)
    {
        var maskRect = TransformsService.CalculateRect(
            svgMask.X,
            svgMask.Y,
            svgMask.Width,
            svgMask.Height,
            svgMask.MaskUnits,
            targetBounds,
            targetBounds,
            svgMask);

        if (maskRect is null)
        {
            return null;
        }

        var childViewport = svgMask.MaskContentUnits == SvgCoordinateUnits.ObjectBoundingBox
            ? SKRect.Create(0f, 0f, 1f, 1f)
            : (compilationViewport.IsEmpty ? targetBounds : compilationViewport);

        var transform = SKMatrix.Identity;
        if (svgMask.MaskContentUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            transform = transform.PreConcat(SKMatrix.CreateTranslation(targetBounds.Left, targetBounds.Top));
            transform = transform.PreConcat(SKMatrix.CreateScale(targetBounds.Width, targetBounds.Height));
        }
        var localMaskClip = CreateLocalMaskClip(maskRect.Value, transform);

        var compileContext = new SvgSceneCompileContext();
        _ = compileContext.TryEnter(svgMask.OwnerDocument, out var documentKey);

        var node = new SvgSceneNode(
            SvgSceneNodeKind.Mask,
            svgMask,
            compileContext.GetElementAddressKey(svgMask),
            svgMask.GetType().Name,
            compilationRootKey: null,
            isCompilationRootBoundary: false)
        {
            CompilationStrategy = SvgSceneCompilationStrategy.DirectRetained,
            IsRenderable = true,
            IsAntialias = PaintingService.IsAntialias(svgMask),
            GeometryBounds = maskRect.Value,
            Clip = localMaskClip,
            Transform = transform,
            TotalTransform = transform,
            TransformedBounds = transform.MapRect(localMaskClip)
        };
        var ownFeatureFlags = svgMask.GetOwnCascadedStyleFeatureFlags(GetRequestedCascadedStyleFeatureFlags(svgMask));
        AssignRetainedVisualState(
            node,
            svgMask,
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.MixBlendMode),
            HasFeatureFlag(ownFeatureFlags, SvgCascadedStyleFeatureFlags.Isolation));
        AssignRetainedResourceKeys(node, svgMask, compileContext);

        try
        {
            for (var i = 0; i < svgMask.Children.Count; i++)
            {
                var childElement = svgMask.Children[i];
                _ = compileContext.GetChildElementAddressKey(svgMask, i);
                if (CompileElementNode(
                        childElement,
                        childViewport,
                        node.TotalTransform,
                        assetLoader,
                        ignoreAttributes,
                        compilationRootKey: null,
                        createOwnCompilationRootBoundary: false,
                        compileContext) is { } childNode)
                {
                    node.AddChild(childNode, svgMask.Children.Count);
                }
            }
        }
        finally
        {
            compileContext.Exit(documentKey);
        }

        return node;
    }

    private static SKRect CreateLocalMaskClip(SKRect maskRect, SKMatrix maskContentTransform)
    {
        if (maskContentTransform.IsIdentity)
        {
            return maskRect;
        }

        return maskContentTransform.TryInvert(out var inverse)
            ? inverse.MapRect(maskRect)
            : maskRect;
    }
}
