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
    private sealed class SvgSceneCompileContext
    {
        private readonly HashSet<string> _activeDocumentKeys = new(StringComparer.Ordinal);
        private readonly SvgElementAddressKeyCache _addressKeys = new();
        private readonly Dictionary<SvgScenePaintingService.SolidFillPaintCacheKey, SKPaint> _solidFillPaintCache = new();
        private readonly Dictionary<SvgFragment, SKSize> _fragmentViewportSizeOverrides = new();

        public SvgSceneContextPaint? ContextPaint { get; private set; }

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
            var hadPreviousOverride = _fragmentViewportSizeOverrides.TryGetValue(svgFragment, out var previousViewportSize);
            _fragmentViewportSizeOverrides[svgFragment] = viewportSize;

            return new FragmentViewportSizeOverrideScope(
                this,
                svgFragment,
                hadPreviousOverride,
                previousViewportSize);
        }

        public bool TryGetFragmentViewportSizeOverride(SvgFragment svgFragment, out SKSize viewportSize)
        {
            return _fragmentViewportSizeOverrides.TryGetValue(svgFragment, out viewportSize);
        }

        public bool TryEnter(SvgDocument? document, out string? documentKey)
        {
            documentKey = GetDocumentKey(document);
            return documentKey is null || _activeDocumentKeys.Add(documentKey);
        }

        public void Exit(string? documentKey)
        {
            if (documentKey is not null)
            {
                _activeDocumentKeys.Remove(documentKey);
            }
        }

        public string? GetElementAddressKey(SvgElement? element)
        {
            return _addressKeys.GetOrCreate(element);
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

            paint = cachedPaint.Clone();
            return true;
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
                    _compileContext._fragmentViewportSizeOverrides[_svgFragment] = _previousViewportSize;
                }
                else
                {
                    _compileContext._fragmentViewportSizeOverrides.Remove(_svgFragment);
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
            new SvgSceneCompileContext(),
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
            ignoreAttributes);
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
        return TryCompileNodeTree(
            sourceDocument,
            cullRect,
            assetLoader,
            ignoreAttributes,
            new SvgSceneCompileContext(),
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
        return TryCompileFragment(
            sourceFragment,
            cullRect,
            viewport,
            assetLoader,
            ignoreAttributes,
            new SvgSceneCompileContext(),
            out sceneDocument);
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
                ignoreAttributes);
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
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(SvgElement? dependencyElement)
        {
            if (dependencyElement is null)
            {
                return;
            }

            var dependencyAddressKey = (getElementAddressKey ?? TryGetElementAddressKey)(dependencyElement) ?? dependencyElement.ID;
            if (string.IsNullOrWhiteSpace(dependencyAddressKey) || !seen.Add(dependencyAddressKey))
            {
                return;
            }

            visitor(dependencyElement, dependencyAddressKey!, state);
        }

        if (element is SvgVisualElement visualElement)
        {
            Add(ResolveReference(visualElement, visualElement.ClipPath));
            Add(ResolveReference(visualElement, visualElement.Filter));
            Add(ResolveReference(visualElement, GetReferenceUri(visualElement, "mask")));
            Add(GetResolvedPaintServerElement(visualElement, visualElement.Fill));
            Add(GetResolvedPaintServerElement(visualElement, visualElement.Stroke));
            Add(ResolveReference(visualElement, GetEffectiveMarkerReferenceUri(visualElement, "marker-start", static element => element.MarkerStart)));
            Add(ResolveReference(visualElement, GetEffectiveMarkerReferenceUri(visualElement, "marker-mid", static element => element.MarkerMid)));
            Add(ResolveReference(visualElement, GetEffectiveMarkerReferenceUri(visualElement, "marker-end", static element => element.MarkerEnd)));
        }

        if (element is SvgMarkerElement markerElement)
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
        if (element is SvgVisualElement visualElement)
        {
            if (visualElement.ClipPath is not null ||
                visualElement.Filter is not null ||
                HasMaskReference(element) ||
                HasPaintServerReference(element, visualElement.Fill) ||
                HasPaintServerReference(element, visualElement.Stroke) ||
                HasMarkerReference(visualElement))
            {
                return true;
            }
        }

        return element switch
        {
            SvgMarkerElement markerElement => GetComputedMarkerReferenceUri(markerElement, "marker-start") is not null ||
                                              GetComputedMarkerReferenceUri(markerElement, "marker-mid") is not null ||
                                              GetComputedMarkerReferenceUri(markerElement, "marker-end") is not null,
            SvgUse svgUse => SvgService.GetEffectiveReferenceUri(svgUse, svgUse.ReferencedElement) is not null,
            SvgGradientServer gradientServer => gradientServer.InheritGradient is not null,
            SvgPatternServer patternServer => patternServer.InheritGradient is not null,
            Svg.FilterEffects.SvgFilter svgFilter => SvgService.GetEffectiveReferenceUri(svgFilter, svgFilter.Href) is not null,
            Svg.FilterEffects.SvgImage filterImage => SvgService.GetEffectiveReferenceUri(filterImage, filterImage.Href) is not null,
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

        if (TryCompileDirectElementNode(
                element,
                viewport,
                parentTotalTransform,
                assetLoader,
                ignoreAttributes,
                compilationRootKey,
                createOwnCompilationRootBoundary,
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

        var childViewport = GetChildViewport(element, viewport, compileContext);
        if (ShouldCompileDomChildren(element))
        {
            for (var i = 0; i < element.Children.Count; i++)
            {
                if (CompileElementNode(
                        element.Children[i],
                        childViewport,
                        node.TotalTransform.IsIdentity ? parentTotalTransform : node.TotalTransform,
                        assetLoader,
                        ignoreAttributes,
                        compilationRootKey: null,
                        createOwnCompilationRootBoundary: true,
                        compileContext) is { } childNode)
                {
                    node.AddChild(childNode);
                }
            }
        }
        else if (node.CompilationStrategy == SvgSceneCompilationStrategy.DirectRetained &&
                 element is SvgSwitch svgSwitch &&
                 TryGetActiveSwitchChild(svgSwitch, out var activeSwitchChild) &&
                 activeSwitchChild is not null &&
                 CompileElementNode(
                     activeSwitchChild,
                     childViewport,
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
        AssignRetainedVisualState(node, element);
        AssignRetainedResourceKeys(node, element, compileContext.GetElementAddressKey);

        return true;
    }

    private static bool TryCompileDirectStructuralNode(
        SvgElement element,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        DrawAttributes ignoreAttributes,
        string? compilationRootKey,
        bool createOwnCompilationRootBoundary,
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

        AssignRetainedVisualState(node, element);
        AssignRetainedResourceKeys(node, element, compileContext.GetElementAddressKey);
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
                FinalizeDirectGroupNode(node, svgGroup, parentTotalTransform, ignoreAttributes);
                break;
            case SvgAnchor svgAnchor:
                FinalizeDirectAnchorNode(node, svgAnchor, parentTotalTransform, ignoreAttributes);
                break;
            case SvgSwitch svgSwitch:
                FinalizeDirectSwitchNode(node, svgSwitch, parentTotalTransform, ignoreAttributes);
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
        SKMatrix parentTotalTransform,
        DrawAttributes ignoreAttributes)
    {
        FinalizeDirectStructuralBounds(node, parentTotalTransform);
    }

    private static void FinalizeDirectAnchorNode(
        SvgSceneNode node,
        SvgAnchor svgAnchor,
        SKMatrix parentTotalTransform,
        DrawAttributes ignoreAttributes)
    {
        FinalizeDirectStructuralBounds(node, parentTotalTransform);
        node.ClipPath = null;
        node.MaskPaint = null;
        node.MaskDstIn = null;
        node.Filter = null;
        node.FilterClip = null;
    }

    private static void FinalizeDirectSwitchNode(
        SvgSceneNode node,
        SvgSwitch svgSwitch,
        SKMatrix parentTotalTransform,
        DrawAttributes ignoreAttributes)
    {
        FinalizeDirectStructuralBounds(node, parentTotalTransform);
        node.ClipPath = null;
        node.MaskPaint = null;
        node.MaskDstIn = null;
        node.Filter = null;
        node.FilterClip = null;
    }

    private static void FinalizeDirectFragmentNode(
        SvgSceneNode node,
        SvgFragment svgFragment,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        DrawAttributes ignoreAttributes)
    {
        FinalizeDirectStructuralBounds(node, parentTotalTransform);
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
        SKMatrix parentTotalTransform)
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
        node.TotalTransform = parentTotalTransform.PreConcat(node.Transform);
        node.TransformedBounds = node.TotalTransform.MapRect(bounds);
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
                CreateReferences(element),
                ignoreAttributes,
                textElementAddressKey,
                textCompilationRootKey,
                createOwnCompilationRootBoundary && !string.IsNullOrWhiteSpace(textCompilationRootKey),
                compileContext.GetElementAddressKey,
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
        AssignRetainedVisualState(node, element);
        AssignRetainedResourceKeys(node, element, compileContext.GetElementAddressKey);
        var markerElement = HasMarkerReference(visualElement) ? visualElement : null;

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
        AssignRetainedResourceKeys(useNode, svgUse, compileContext.GetElementAddressKey);
        AssignRetainedVisualState(useNode, svgUse);

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
        var referencedNode = WithTemporaryParent(referencedElement, svgUse, () =>
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

        RefreshGeneratedElementAddresses(referencedNode);
        AssignGeneratedHitTestTarget(referencedNode, svgUse);
        useNode.AddChild(referencedNode);
        FinalizeDirectStructuralBounds(useNode, parentTotalTransform);
        node = useNode;
        return true;
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
        AssignRetainedResourceKeys(node, svgImage, compileContext.GetElementAddressKey);
        AssignRetainedVisualState(node, svgImage);

        var hasExplicitWidth = SvgService.TryGetAttribute(svgImage, "width", out _);
        var hasExplicitHeight = SvgService.TryGetAttribute(svgImage, "height", out _);
        var width = SvgGeometryService.GetComputedUnit(svgImage, "width", svgImage.Width).ToDeviceValue(UnitRenderingType.Horizontal, svgImage, viewport);
        var height = SvgGeometryService.GetComputedUnit(svgImage, "height", svgImage.Height).ToDeviceValue(UnitRenderingType.Vertical, svgImage, viewport);
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
            node.IsRenderable = false;
            return true;
        }

        var image = SvgService.GetImage(href!, svgImage, assetLoader);
        if (image is not SKImage && image is not SvgDocument)
        {
            node.IsRenderable = false;
            return true;
        }

        var srcRect = image switch
        {
            SKImage skImage => SKRect.Create(0f, 0f, skImage.Width, skImage.Height),
            SvgDocument svgDocument => CreateSourceRect(svgDocument),
            _ => SKRect.Empty
        };

        if (srcRect.IsEmpty)
        {
            node.IsRenderable = false;
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
        AssignRetainedResourceKeys(node, svgSymbol, compileContext.GetElementAddressKey);
        AssignRetainedVisualState(node, svgSymbol);

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
            if (CompileElementNode(
                    svgSymbol.Children[i],
                    childViewport,
                    node.TotalTransform,
                    assetLoader,
                    ignoreAttributes,
                    compilationRootKey,
                    createOwnCompilationRootBoundary: false,
                    compileContext) is { } childNode)
            {
                node.AddChild(childNode);
            }
        }

        FinalizeDirectStructuralBounds(node, parentTotalTransform);
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
        if (!SvgService.TryGetAttribute(svgSymbol, "refX", out _) &&
            !SvgService.TryGetAttribute(svgSymbol, "refY", out _))
        {
            return viewBoxTransform;
        }

        var refX = svgSymbol.RefX.ToDeviceValue(UnitRenderingType.Horizontal, svgSymbol, viewport);
        var refY = svgSymbol.RefY.ToDeviceValue(UnitRenderingType.Vertical, svgSymbol, viewport);
        var mappedReferencePoint = viewBoxTransform.MapPoint(new SKPoint(refX, refY));
        var deltaX = x - mappedReferencePoint.X;
        var deltaY = y - mappedReferencePoint.Y;
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

    private static SKRect CreateSourceRect(SvgDocument svgDocument)
    {
        var size = SvgService.GetDimensions(svgDocument);
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

            if (child.HasRequiredFeatures() && child.HasRequiredExtensions() && child.HasSystemLanguage())
            {
                activeChild = child;
                return true;
            }
        }

        activeChild = null;
        return false;
    }

    private static T WithTemporaryParent<T>(SvgElement element, SvgElement temporaryParent, Func<T> factory)
    {
        return element.WithTemporaryParent(temporaryParent, factory);
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
        var pathTypes = path.GetPathTypes();
        var pathLength = pathTypes.Count;
        if (pathLength <= 0)
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
                var refPoint1 = pathTypes[0].Point;
                var index = 1;
                while (index < pathLength &&
                       pathTypes[index].Point.X == refPoint1.X &&
                       pathTypes[index].Point.Y == refPoint1.Y)
                {
                    index++;
                }

                var refPoint2 = pathLength == 1 ? refPoint1 : pathTypes[Math.Min(index, pathLength - 1)].Point;
                if (TryCompileDirectMarkerNode(
                        marker,
                        markerElement,
                        refPoint1,
                        refPoint1,
                        refPoint2,
                        isStartMarker: true,
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
            pathLength > 1 &&
            !HasRecursiveMarkerReference(markerElement, static element => element.MarkerMid))
        {
            var marker = SvgService.GetReference<SvgMarker>(markerElement, markerMid);
            if (marker is not null)
            {
                var bezierIndex = -1;
                for (var i = 1; i <= pathLength - 2; i++)
                {
                    if ((pathTypes[i].Type & (byte)PathingService.PathPointType.PathTypeMask) == (byte)PathingService.PathPointType.Bezier)
                    {
                        bezierIndex = (bezierIndex + 1) % 3;
                    }
                    else
                    {
                        bezierIndex = -1;
                    }

                    if (bezierIndex != -1 && bezierIndex != 2)
                    {
                        continue;
                    }

                    if (TryCompileDirectMarkerNode(
                            marker,
                            markerElement,
                            pathTypes[i].Point,
                            pathTypes[i - 1].Point,
                            pathTypes[i].Point,
                            pathTypes[i + 1].Point,
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

                if (HasCloseSubpath(pathTypes[pathLength - 1].Type))
                {
                    var lastIndex = pathLength - 1;
                    var startIndex = GetSubpathStartIndex(pathTypes, lastIndex);
                    if (!AreSamePoint(pathTypes[lastIndex].Point, pathTypes[startIndex].Point))
                    {
                        var previousIndex = lastIndex - 1;
                        while (previousIndex > startIndex &&
                               AreSamePoint(pathTypes[previousIndex].Point, pathTypes[lastIndex].Point))
                        {
                            previousIndex--;
                        }

                        if (TryCompileDirectMarkerNode(
                                marker,
                                markerElement,
                                pathTypes[lastIndex].Point,
                                pathTypes[previousIndex].Point,
                                pathTypes[lastIndex].Point,
                                pathTypes[startIndex].Point,
                                node.GeometryBounds,
                                viewport,
                                markerParentTotalTransform,
                                assetLoader,
                                ignoreAttributes,
                                node.CompilationRootKey,
                                compileContext,
                                out var closingMidMarkerNode) &&
                            closingMidMarkerNode is not null)
                        {
                            AssignGeneratedHitTestTarget(closingMidMarkerNode, hitTestTarget);
                            node.AddChild(closingMidMarkerNode);
                        }
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
                var index = pathLength - 1;
                var refPoint1 = pathTypes[index].Point;
                var isClosedSubpath = HasCloseSubpath(pathTypes[index].Type);
                if (HasCloseSubpath(pathTypes[index].Type))
                {
                    var startIndex = GetSubpathStartIndex(pathTypes, index);
                    refPoint1 = pathTypes[startIndex].Point;
                }

                if (pathLength > 1)
                {
                    index--;
                    while (index > 0 &&
                           AreSamePoint(pathTypes[index].Point, refPoint1))
                    {
                        index--;
                    }
                }

                var refPoint2 = pathLength == 1 ? refPoint1 : pathTypes[index].Point;
                var markerPoint1 = refPoint2;
                var markerPoint2 = pathTypes[pathLength - 1].Point;
                if (isClosedSubpath &&
                    !AreSamePoint(pathTypes[pathLength - 1].Point, refPoint1))
                {
                    markerPoint1 = pathTypes[pathLength - 1].Point;
                    markerPoint2 = refPoint1;
                }

                if (TryCompileDirectMarkerNode(
                        marker,
                        markerElement,
                        refPoint1,
                        markerPoint1,
                        markerPoint2,
                        isStartMarker: false,
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

    private static bool AreSamePoint(SKPoint point1, SKPoint point2)
    {
        return Math.Abs(point1.X - point2.X) <= 0.001f &&
               Math.Abs(point1.Y - point2.Y) <= 0.001f;
    }

    private static bool HasCloseSubpath(byte pathType)
    {
        return (pathType & (byte)PathingService.PathPointType.CloseSubpath) != 0;
    }

    private static int GetSubpathStartIndex(IReadOnlyList<(SKPoint Point, byte Type)> pathTypes, int index)
    {
        for (var current = index; current >= 0; current--)
        {
            if ((pathTypes[current].Type & (byte)PathingService.PathPointType.PathTypeMask) == (byte)PathingService.PathPointType.Start)
            {
                return current;
            }
        }

        return 0;
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
            (angle1 + angle2) / 2f,
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

        if (!TryGetMarkerVisualElement(svgMarker, out var markerElement) ||
            markerElement is null)
        {
            return false;
        }

        var markerMatrix = SKMatrix.Identity;
        markerMatrix = markerMatrix.PreConcat(SKMatrix.CreateTranslation(referencePoint.X, referencePoint.Y));
        markerMatrix = markerMatrix.PreConcat(SKMatrix.CreateRotationDegrees(svgMarker.Orient.IsAuto ? angle : svgMarker.Orient.Angle));

        var strokeWidth = owner.StrokeWidth.ToDeviceValue(UnitRenderingType.Other, svgMarker, viewport);
        var refX = svgMarker.RefX.ToDeviceValue(UnitRenderingType.Horizontal, svgMarker, viewport);
        var refY = svgMarker.RefY.ToDeviceValue(UnitRenderingType.Vertical, svgMarker, viewport);
        var markerWidth = svgMarker.MarkerWidth.ToDeviceValue(UnitRenderingType.Other, svgMarker, viewport);
        var markerHeight = svgMarker.MarkerHeight.ToDeviceValue(UnitRenderingType.Other, svgMarker, viewport);
        var viewBoxScaleX = 1f;
        var viewBoxScaleY = 1f;

        switch (svgMarker.MarkerUnits)
        {
            case SvgMarkerUnits.StrokeWidth:
                markerMatrix = markerMatrix.PreConcat(SKMatrix.CreateScale(strokeWidth, strokeWidth));

                var viewBoxWidth = svgMarker.ViewBox.Width;
                var viewBoxHeight = svgMarker.ViewBox.Height;
                var scaleFactorWidth = viewBoxWidth <= 0f ? 1f : markerWidth / viewBoxWidth;
                var scaleFactorHeight = viewBoxHeight <= 0f ? 1f : markerHeight / viewBoxHeight;
                viewBoxScaleX = Math.Min(scaleFactorWidth, scaleFactorHeight);
                viewBoxScaleY = Math.Min(scaleFactorWidth, scaleFactorHeight);

                markerMatrix = markerMatrix.PreConcat(SKMatrix.CreateTranslation(-refX * viewBoxScaleX, -refY * viewBoxScaleY));
                markerMatrix = markerMatrix.PreConcat(SKMatrix.CreateScale(viewBoxScaleX, viewBoxScaleY));
                break;
            case SvgMarkerUnits.UserSpaceOnUse:
                markerMatrix = markerMatrix.PreConcat(SKMatrix.CreateTranslation(-refX, -refY));
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
        AssignRetainedVisualState(node, svgMarker);
        AssignRetainedResourceKeys(node, svgMarker, compileContext.GetElementAddressKey);
        node.IsVisible = true;
        node.IsDisplayNone = false;

        switch (svgMarker.Overflow)
        {
            case SvgOverflow.Auto:
            case SvgOverflow.Visible:
            case SvgOverflow.Inherit:
                break;
            default:
                node.InnerClip = SKRect.Create(
                    svgMarker.ViewBox.MinX,
                    svgMarker.ViewBox.MinY,
                    markerWidth / viewBoxScaleX,
                    markerHeight / viewBoxScaleY);
                break;
        }

        SvgSceneNode? childNode;
        using (compileContext.PushContextPaint(owner, contextPaintBounds))
        {
            childNode = CompileElementNode(
                markerElement,
                viewport,
                node.TotalTransform,
                assetLoader,
                DrawAttributes.Display | ignoreAttributes,
                compilationRootKey,
                createOwnCompilationRootBoundary: false,
                compileContext);
        }

        if (childNode is null)
        {
            node = null;
            return false;
        }

        ResetGeneratedDisplayState(childNode);
        node.AddChild(childNode);
        FinalizeDirectStructuralBounds(node, parentTotalTransform);
        return true;
    }

    private static void ResetGeneratedDisplayState(SvgSceneNode node)
    {
        node.IsDisplayNone = false;

        for (var i = 0; i < node.Children.Count; i++)
        {
            ResetGeneratedDisplayState(node.Children[i]);
        }
    }

    private static bool TryGetMarkerVisualElement(SvgMarker svgMarker, out SvgVisualElement? markerElement)
    {
        for (var i = 0; i < svgMarker.Children.Count; i++)
        {
            if (svgMarker.Children[i] is SvgVisualElement visualElement)
            {
                markerElement = visualElement;
                return true;
            }
        }

        markerElement = null;
        return false;
    }

    private static Uri? GetEffectiveMarkerReferenceUri(
        SvgVisualElement markerElement,
        string attributeName,
        Func<SvgMarkerElement, Uri?> localSelector)
    {
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
        var hasRequiredFeatures = ignoreAttributes.HasFlag(DrawAttributes.RequiredFeatures) || element.HasRequiredFeatures();
        var hasRequiredExtensions = ignoreAttributes.HasFlag(DrawAttributes.RequiredExtensions) || element.HasRequiredExtensions();
        var hasSystemLanguage = ignoreAttributes.HasFlag(DrawAttributes.SystemLanguage) || element.HasSystemLanguage();
        return hasRequiredFeatures && hasRequiredExtensions && hasSystemLanguage;
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

        AssignRetainedVisualState(root, owner);
        AssignRetainedResourceKeys(root, owner, compileContext.GetElementAddressKey);

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
                    root.AddChild(childNode);
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
            ignoreAttributes);

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
                    compileContext.ContextPaint);
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
                path);
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

        var cullRect = CreateLocalCullRect(path.Bounds);
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

    private static void RefreshGeneratedElementAddresses(SvgSceneNode node)
    {
        var addressKeyCache = new SvgElementAddressKeyCache();
        RefreshGeneratedElementAddresses(node, addressKeyCache.GetOrCreate);
    }

    private static void RefreshGeneratedElementAddresses(SvgSceneNode node, Func<SvgElement?, string?> getElementAddressKey)
    {
        node.RefreshElementIdentity(getElementAddressKey(node.Element));
        AssignRetainedVisualState(node, node.Element);
        AssignRetainedResourceKeys(node, node.Element, getElementAddressKey);

        if (node.MaskNode is { } maskNode)
        {
            RefreshGeneratedElementAddresses(maskNode, getElementAddressKey);
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            RefreshGeneratedElementAddresses(node.Children[i], getElementAddressKey);
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
        if (server is null || server == SvgPaintServer.None || server == SvgPaintServer.Inherit || server == SvgPaintServer.NotSet)
        {
            return null;
        }

        return SvgDeferredPaintServer.TryGet<SvgPaintServer>(server, owner) as SvgElement;
    }

    private static bool HasPaintServerReference(SvgElement owner, SvgPaintServer? server)
    {
        return GetResolvedPaintServerElement(owner, server) is not null;
    }

    private static SvgElement? ResolveReference(SvgElement owner, Uri? uri)
    {
        return uri is null ? null : owner.OwnerDocument?.GetElementById(uri.ToString());
    }

    private static string? TryGetResourceKey(SvgElement owner, Uri? uri, Func<SvgElement?, string?>? getElementAddressKey = null)
    {
        return (getElementAddressKey ?? TryGetElementAddressKey)(ResolveReference(owner, uri));
    }

    private static string? TryGetMaskResourceKey(SvgElement element, Func<SvgElement?, string?>? getElementAddressKey = null)
    {
        var maskUri = GetReferenceUri(element, "mask");
        if (maskUri is null)
        {
            return null;
        }

        var svgMask = SvgService.GetReference<SvgMask>(element, maskUri);
        return (getElementAddressKey ?? TryGetElementAddressKey)(svgMask);
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

        if (element is not SvgVisualElement visualElement)
        {
            return;
        }

        node.ClipResourceKey = TryGetResourceKey(visualElement, visualElement.ClipPath, getElementAddressKey);
        node.MaskResourceKey = TryGetMaskResourceKey(visualElement, getElementAddressKey);
        node.FilterResourceKey = TryGetResourceKey(visualElement, visualElement.Filter, getElementAddressKey);
    }

    internal static void AssignRetainedVisualState(SvgSceneNode node, SvgElement? element)
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

        if (element is not null &&
            TryParseMixBlendMode(element, out var blendMode))
        {
            node.BlendModePaint = new SKPaint
            {
                BlendMode = blendMode
            };
        }

        if (element is not null &&
            element.IsContainerElement() &&
            TryParseIsolation(element))
        {
            node.IsIsolationGroup = true;
        }
    }

    private static bool TryParseIsolation(SvgElement element)
    {
        return element.ComputedStyle.TryGetIsolation(out var isolation) &&
               isolation == SvgIsolation.Isolate;
    }

    private static bool TryParseMixBlendMode(SvgElement element, out SKBlendMode blendMode)
    {
        blendMode = SKBlendMode.SrcOver;
        if (!element.ComputedStyle.TryGetMixBlendMode(out var value))
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

    private static Uri? GetUriAttribute(SvgElement element, string name)
    {
        return GetReferenceUri(element, name);
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
        if (!element.TryGetAttribute("enable-background", out var enableBackground) ||
            string.IsNullOrWhiteSpace(enableBackground))
        {
            return false;
        }

        enableBackground = enableBackground.Trim();
        if (enableBackground.Equals("accumulate", StringComparison.Ordinal))
        {
            return false;
        }

        if (!enableBackground.StartsWith("new", StringComparison.Ordinal))
        {
            return false;
        }

        if (enableBackground.Length <= 3)
        {
            return true;
        }

        var values = enableBackground.Substring(4, enableBackground.Length - 4)
            .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => float.Parse(part.Trim(), CultureInfo.InvariantCulture))
            .ToArray();

        if (values.Length == 4)
        {
            clip = SKRect.Create(values[0], values[1], values[2], values[3]);
        }

        return true;
    }

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
            Transform = transform,
            TotalTransform = transform,
            TransformedBounds = transform.MapRect(maskRect.Value),
            Overflow = maskRect.Value
        };
        AssignRetainedVisualState(node, svgMask);
        AssignRetainedResourceKeys(node, svgMask, compileContext.GetElementAddressKey);

        try
        {
            for (var i = 0; i < svgMask.Children.Count; i++)
            {
                if (CompileElementNode(
                        svgMask.Children[i],
                        childViewport,
                        node.TotalTransform,
                        assetLoader,
                        ignoreAttributes,
                        compilationRootKey: null,
                        createOwnCompilationRootBoundary: false,
                        compileContext) is { } childNode)
                {
                    node.AddChild(childNode);
                }
            }
        }
        finally
        {
            compileContext.Exit(documentKey);
        }

        return node;
    }
}
