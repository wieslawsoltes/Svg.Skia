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

    private static bool TryCompile(
        SvgDocument? sourceDocument,
        SKRect cullRect,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        SvgSceneCompileContext compileContext,
        out SvgSceneDocument? sceneDocument)
    {
        sceneDocument = null;

        if (sourceDocument is null)
        {
            return false;
        }

        var viewport = cullRect;
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
            var rootNode = CompileElementNode(
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

            sceneDocument = new SvgSceneDocument(
                sourceDocument,
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
        var addressKey = TryGetElementAddressKey(element);
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

    internal static IEnumerable<SvgElement> EnumerateReferencedElements(SvgElement element)
    {
        var results = new List<SvgElement>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(SvgElement? dependencyElement)
        {
            if (dependencyElement is null)
            {
                return;
            }

            var dependencyAddressKey = TryGetElementAddressKey(dependencyElement) ?? dependencyElement.ID;
            if (string.IsNullOrWhiteSpace(dependencyAddressKey) || !seen.Add(dependencyAddressKey))
            {
                return;
            }

            results.Add(dependencyElement);
        }

        if (element is SvgVisualElement visualElement)
        {
            Add(ResolveReference(visualElement, visualElement.ClipPath));
            Add(ResolveReference(visualElement, visualElement.Filter));
            Add(ResolveReference(visualElement, GetUriAttribute(visualElement, "mask")));
            Add(GetResolvedPaintServerElement(visualElement, visualElement.Fill));
            Add(GetResolvedPaintServerElement(visualElement, visualElement.Stroke));
        }

        if (element is SvgMarkerElement markerElement)
        {
            Add(ResolveReference(markerElement, markerElement.MarkerStart));
            Add(ResolveReference(markerElement, markerElement.MarkerMid));
            Add(ResolveReference(markerElement, markerElement.MarkerEnd));
        }

        if (element is SvgUse svgUse)
        {
            Add(ResolveReference(svgUse, svgUse.ReferencedElement));
        }

        if (element is SvgGradientServer gradientServer)
        {
            Add(SvgDeferredPaintServer.TryGet<SvgGradientServer>(gradientServer.InheritGradient, gradientServer));
        }

        if (element is SvgPatternServer patternServer)
        {
            Add(SvgDeferredPaintServer.TryGet<SvgPatternServer>(patternServer.InheritGradient, patternServer));
        }

        if (element is SvgTextRef textRef)
        {
            Add(ResolveReference(textRef, textRef.ReferencedElement));
        }

        if (element is SvgTextPath textPath)
        {
            Add(ResolveReference(textPath, textPath.ReferencedPath));
        }

        return results;
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
                     out var structuralNode))
        {
            node = structuralNode;
        }
        else if (TryCompileDirectNonRenderingNode(
                     element,
                     parentTotalTransform,
                     compilationRootKey,
                     createOwnCompilationRootBoundary,
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
            for (var i = 0; i < element.Children.Count; i++)
            {
                if (CompileElementNode(
                        element.Children[i],
                        viewport,
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

        var elementAddressKey = TryGetElementAddressKey(element);
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
        AssignRetainedResourceKeys(node, element);

        return true;
    }

    private static bool TryCompileDirectStructuralNode(
        SvgElement element,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        DrawAttributes ignoreAttributes,
        string? compilationRootKey,
        bool createOwnCompilationRootBoundary,
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
                            PaintingService.IsAntialias(svgDocument),
                            isRenderable: false,
                            suppressSubtreeRendering: true,
                            SKMatrix.Identity,
                            parentTotalTransform);
                        return true;
                    }

                    var fragmentViewport = GetFragmentViewport(svgDocument, viewport, out var x, out var y, out var size);
                    var transform = TransformsService.ToMatrix(svgDocument.Transforms);
                    var viewBoxTransform = TransformsService.ToMatrix(svgDocument.ViewBox, svgDocument.AspectRatio, x, y, size.Width, size.Height);
                    transform = transform.PreConcat(viewBoxTransform);

                    node = CreateDirectStructuralNode(
                        svgDocument,
                        compilationRootKey,
                        createOwnCompilationRootBoundary,
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
                            PaintingService.IsAntialias(svgFragment),
                            isRenderable: false,
                            suppressSubtreeRendering: true,
                            SKMatrix.Identity,
                            parentTotalTransform);
                        return true;
                    }

                    var fragmentViewport = GetFragmentViewport(svgFragment, viewport, out var x, out var y, out var size);
                    var transform = TransformsService.ToMatrix(svgFragment.Transforms);
                    var viewBoxTransform = TransformsService.ToMatrix(svgFragment.ViewBox, svgFragment.AspectRatio, x, y, size.Width, size.Height);
                    transform = transform.PreConcat(viewBoxTransform);

                    node = CreateDirectStructuralNode(
                        svgFragment,
                        compilationRootKey,
                        createOwnCompilationRootBoundary,
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
        bool isAntialias,
        bool isRenderable,
        bool suppressSubtreeRendering,
        SKMatrix transform,
        SKMatrix parentTotalTransform)
    {
        var elementAddressKey = TryGetElementAddressKey(element);
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
        AssignRetainedResourceKeys(node, element);
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

        if (!node.IsRenderable)
        {
            return;
        }

        if (!ignoreAttributes.HasFlag(DrawAttributes.Opacity))
        {
            node.Opacity = SvgScenePaintingService.GetOpacityPaint(svgGroup.Opacity);
            node.OpacityValue = SvgScenePaintingService.AdjustSvgOpacity(svgGroup.Opacity);
        }
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
        node.Opacity = ignoreAttributes.HasFlag(DrawAttributes.Opacity)
            ? null
            : SvgScenePaintingService.GetOpacityPaint(svgAnchor.Opacity);
        node.OpacityValue = ignoreAttributes.HasFlag(DrawAttributes.Opacity)
            ? 1f
            : SvgScenePaintingService.AdjustSvgOpacity(svgAnchor.Opacity);
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
        node.Opacity = ignoreAttributes.HasFlag(DrawAttributes.Opacity)
            ? null
            : SvgScenePaintingService.GetOpacityPaint(svgSwitch.Opacity);
        node.OpacityValue = ignoreAttributes.HasFlag(DrawAttributes.Opacity)
            ? 1f
            : SvgScenePaintingService.AdjustSvgOpacity(svgSwitch.Opacity);
    }

    private static void FinalizeDirectFragmentNode(
        SvgSceneNode node,
        SvgFragment svgFragment,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        DrawAttributes ignoreAttributes)
    {
        FinalizeDirectStructuralBounds(node, parentTotalTransform);

        if (!node.IsRenderable)
        {
            return;
        }

        if (!ignoreAttributes.HasFlag(DrawAttributes.Opacity))
        {
            node.Opacity = SvgScenePaintingService.GetOpacityPaint(svgFragment.Opacity);
            node.OpacityValue = SvgScenePaintingService.AdjustSvgOpacity(svgFragment.Opacity);
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
        out float x,
        out float y,
        out SKSize size)
    {
        var svgFragmentParent = svgFragment.Parent;

        x = svgFragmentParent is null ? 0f : svgFragment.X.ToDeviceValue(UnitRenderingType.Horizontal, svgFragment, viewport);
        y = svgFragmentParent is null ? 0f : svgFragment.Y.ToDeviceValue(UnitRenderingType.Vertical, svgFragment, viewport);
        size = SvgService.GetDimensions(svgFragment, viewport);

        if (size.Width > 0f && size.Height > 0f)
        {
            return SKRect.Create(x, y, size.Width, size.Height);
        }

        return viewport.IsEmpty ? SKRect.Empty : viewport;
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
            var textElementAddressKey = TryGetElementAddressKey(element);
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
                out node);
        }

        if (!TryGetDirectVisualElement(element, out var visualElement) ||
            visualElement is null)
        {
            return false;
        }

        _ = TryGetDirectVisualPath(element, viewport, out var path);

        var elementAddressKey = TryGetElementAddressKey(element);
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
        node.Transform = TransformsService.ToMatrix(visualElement.Transforms);
        node.TotalTransform = parentTotalTransform.PreConcat(node.Transform);
        node.TransformedBounds = node.TotalTransform.MapRect(node.GeometryBounds);
        node.HitTestPath = path?.DeepClone();
        node.SupportsFillHitTest = SvgScenePaintingService.IsValidFill(visualElement);
        node.SupportsStrokeHitTest = SvgScenePaintingService.IsValidStroke(visualElement, node.GeometryBounds);
        node.HitTestTargetElement = GetDefaultHitTestTargetElement(node, element);
        AssignRetainedVisualState(node, element);
        AssignRetainedResourceKeys(node, element);
        var markerElement = visualElement as SvgMarkerElement;

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

        var localModel = CreateDirectPathModel(visualElement, path, node.GeometryBounds, assetLoader, ignoreAttributes, out var canKeepRenderable);
        node.LocalModel = localModel;
        node.Fill = SvgScenePaintingService.IsValidFill(visualElement)
            ? SvgScenePaintingService.GetFillPaint(visualElement, node.GeometryBounds, assetLoader, ignoreAttributes)
            : null;
        node.Stroke = SvgScenePaintingService.IsValidStroke(visualElement, node.GeometryBounds)
            ? SvgScenePaintingService.GetStrokePaint(visualElement, node.GeometryBounds, assetLoader, ignoreAttributes)
            : null;
        node.StrokeWidth = node.Stroke?.StrokeWidth ?? 0f;
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

        if (!ignoreAttributes.HasFlag(DrawAttributes.Opacity))
        {
            node.Opacity = SvgScenePaintingService.GetOpacityPaint(visualElement.Opacity);
            node.OpacityValue = SvgScenePaintingService.AdjustSvgOpacity(visualElement.Opacity);
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
        var elementAddressKey = TryGetElementAddressKey(svgUse);
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
        AssignRetainedResourceKeys(useNode, svgUse);
        AssignRetainedVisualState(useNode, svgUse);

        var x = svgUse.X.ToDeviceValue(UnitRenderingType.Horizontal, svgUse, viewport);
        var y = svgUse.Y.ToDeviceValue(UnitRenderingType.Vertical, svgUse, viewport);
        var width = svgUse.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgUse, viewport);
        var height = svgUse.Height.ToDeviceValue(UnitRenderingType.Vertical, svgUse, viewport);

        if (width <= 0f)
        {
            width = new SvgUnit(SvgUnitType.Percentage, 100f).ToDeviceValue(UnitRenderingType.Horizontal, svgUse, viewport);
        }

        if (height <= 0f)
        {
            height = new SvgUnit(SvgUnitType.Percentage, 100f).ToDeviceValue(UnitRenderingType.Vertical, svgUse, viewport);
        }

        var hasRecursiveReference = SvgService.HasRecursiveReference(svgUse, static element => element.ReferencedElement, new HashSet<Uri>());
        var referencedElement = hasRecursiveReference
            ? null
            : SvgService.GetReference<SvgElement>(svgUse, svgUse.ReferencedElement);

        var useTransform = TransformsService.ToMatrix(svgUse.Transforms);
        if (referencedElement is not SvgSymbol)
        {
            useTransform = useTransform.PreConcat(SKMatrix.CreateTranslation(x, y));
        }

        if (referencedElement is SvgFragment svgFragment &&
            TryCreateUseFragmentScaleTransform(svgFragment, width, height, out var fragmentScaleTransform))
        {
            useTransform = useTransform.PreConcat(fragmentScaleTransform);
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

        var referencedNode = WithTemporaryParent(referencedElement, svgUse, () =>
        {
            referencedElement.InvalidateChildPaths();

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

    private static bool TryCreateUseFragmentScaleTransform(
        SvgFragment svgFragment,
        float width,
        float height,
        out SKMatrix transform)
    {
        transform = SKMatrix.Identity;

        var viewBox = svgFragment.ViewBox;
        if (viewBox == SvgViewBox.Empty ||
            Math.Abs(viewBox.Width) <= float.Epsilon ||
            Math.Abs(viewBox.Height) <= float.Epsilon ||
            Math.Abs(width - viewBox.Width) <= float.Epsilon ||
            Math.Abs(height - viewBox.Height) <= float.Epsilon)
        {
            return false;
        }

        transform = SKMatrix.CreateScale(width / viewBox.Width, height / viewBox.Height);
        return true;
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
        var elementAddressKey = TryGetElementAddressKey(svgImage);
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
        AssignRetainedResourceKeys(node, svgImage);
        AssignRetainedVisualState(node, svgImage);

        var width = svgImage.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgImage, viewport);
        var height = svgImage.Height.ToDeviceValue(UnitRenderingType.Vertical, svgImage, viewport);
        var x = svgImage.Location.X.ToDeviceValue(UnitRenderingType.Horizontal, svgImage, viewport);
        var y = svgImage.Location.Y.ToDeviceValue(UnitRenderingType.Vertical, svgImage, viewport);

        node.Transform = TransformsService.ToMatrix(svgImage.Transforms);
        node.TotalTransform = parentTotalTransform.PreConcat(node.Transform);

        if (!node.IsRenderable || width <= 0f || height <= 0f || string.IsNullOrWhiteSpace(svgImage.Href))
        {
            node.IsRenderable = false;
            return true;
        }

        var uri = SvgService.GetImageDocumentUri(SvgService.GetImageUri(svgImage.Href, svgImage));
        var references = CreateReferences(svgImage);
        if (references is { } && references.Contains(uri))
        {
            node.IsRenderable = false;
            return true;
        }

        var image = SvgService.GetImage(svgImage.Href, svgImage, assetLoader);
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

        var destClip = SKRect.Create(x, y, width, height);
        var destRect = TransformsService.CalculateRect(svgImage.AspectRatio, srcRect, destClip);
        var usesReferencedSvgViewport =
            image is SvgDocument svgDocumentImage &&
            ShouldUseReferencedSvgViewport(svgDocumentImage, uri) &&
            svgImage.AspectRatio.Align != SvgPreserveAspectRatio.none;
        var geometryBounds = usesReferencedSvgViewport ? destClip : destRect;
        node.GeometryBounds = geometryBounds;
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

        if (svgSymbol.CustomAttributes.TryGetValue("width", out var widthString) &&
            new SvgUnitConverter().ConvertFromString(widthString) is SvgUnit symbolWidth)
        {
            width = symbolWidth.ToDeviceValue(UnitRenderingType.Horizontal, svgSymbol, viewport);
        }

        if (svgSymbol.CustomAttributes.TryGetValue("height", out var heightString) &&
            new SvgUnitConverter().ConvertFromString(heightString) is SvgUnit symbolHeight)
        {
            height = symbolHeight.ToDeviceValue(UnitRenderingType.Vertical, svgSymbol, viewport);
        }

        var node = new SvgSceneNode(
            SvgSceneNodeKind.Fragment,
            svgSymbol,
            TryGetElementAddressKey(svgSymbol),
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
        AssignRetainedResourceKeys(node, svgSymbol);
        AssignRetainedVisualState(node, svgSymbol);

        var transform = TransformsService.ToMatrix(svgSymbol.Transforms);
        var viewBoxTransform = TransformsService.ToMatrix(svgSymbol.ViewBox, svgSymbol.AspectRatio, x, y, width, height);
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
            node.Overflow = SKRect.Create(x, y, width, height);
        }

        for (var i = 0; i < svgSymbol.Children.Count; i++)
        {
            if (CompileElementNode(
                    svgSymbol.Children[i],
                    viewport,
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
        SvgMarkerElement markerElement,
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
            !SvgService.HasRecursiveReference(markerElement, static element => element.MarkerStart, new HashSet<Uri>()))
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
            !SvgService.HasRecursiveReference(markerElement, static element => element.MarkerMid, new HashSet<Uri>()))
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
                    var previousIndex = lastIndex - 1;
                    while (previousIndex > startIndex &&
                           pathTypes[previousIndex].Point.X == pathTypes[lastIndex].Point.X &&
                           pathTypes[previousIndex].Point.Y == pathTypes[lastIndex].Point.Y)
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

        var markerEnd = GetEffectiveMarkerReferenceUri(markerElement, "marker-end", static element => element.MarkerEnd);
        if (markerEnd is not null &&
            !SvgService.HasRecursiveReference(markerElement, static element => element.MarkerEnd, new HashSet<Uri>()))
        {
            var marker = SvgService.GetReference<SvgMarker>(markerElement, markerEnd);
            if (marker is not null)
            {
                var index = pathLength - 1;
                var refPoint1 = pathTypes[index].Point;
                if (HasCloseSubpath(pathTypes[index].Type))
                {
                    var startIndex = GetSubpathStartIndex(pathTypes, index);
                    refPoint1 = pathTypes[startIndex].Point;
                }

                if (pathLength > 1)
                {
                    index--;
                    while (index > 0 &&
                           pathTypes[index].Point.X == refPoint1.X &&
                           pathTypes[index].Point.Y == refPoint1.Y)
                    {
                        index--;
                    }
                }

                var refPoint2 = pathLength == 1 ? refPoint1 : pathTypes[index].Point;
                if (TryCompileDirectMarkerNode(
                        marker,
                        markerElement,
                        refPoint1,
                        refPoint2,
                        pathTypes[pathLength - 1].Point,
                        isStartMarker: false,
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
            TryGetElementAddressKey(svgMarker),
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
        AssignRetainedResourceKeys(node, svgMarker);
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

        var childNode = CompileElementNode(
            markerElement,
            viewport,
            node.TotalTransform,
            assetLoader,
            DrawAttributes.Display | ignoreAttributes,
            compilationRootKey,
            createOwnCompilationRootBoundary: false,
            compileContext);

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
        SvgMarkerElement markerElement,
        string attributeName,
        Func<SvgMarkerElement, Uri?> localSelector)
    {
        if (localSelector(markerElement) is { } localValue)
        {
            return localValue;
        }

        for (var current = markerElement.Parent; current is not null; current = current.Parent)
        {
            if (TryGetUriAttribute(current, attributeName) is { } inheritedSpecific)
            {
                return inheritedSpecific;
            }
        }

        return null;
    }

    private static Uri? TryGetUriAttribute(SvgElement element, string attributeName)
    {
        if (!element.TryGetAttribute(attributeName, out var rawValue) ||
            string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

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
        AssignRetainedResourceKeys(root, owner);

        var compileContext = new SvgSceneCompileContext();
        _ = compileContext.TryEnter(owner.OwnerDocument, out var documentKey);

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
        return new SvgSceneDocument(
            owner.OwnerDocument,
            GetEffectiveDocumentCullRect(cullRect, root),
            viewport,
            root,
            assetLoader,
            ignoreAttributes);
    }

    private static HashSet<Uri>? CreateReferences(SvgElement element)
    {
        return SvgService.ExtendImageReferences(null, element.OwnerDocument);
    }

    private static bool TryGetDirectVisualPath(SvgElement element, SKRect viewport, out SKPath? path)
    {
        path = element switch
        {
            SvgPath svgPath => svgPath.PathData?.ToPath(svgPath.FillRule),
            SvgRectangle svgRectangle => svgRectangle.ToPath(svgRectangle.FillRule, viewport),
            SvgCircle svgCircle => svgCircle.ToPath(svgCircle.FillRule, viewport),
            SvgEllipse svgEllipse => svgEllipse.ToPath(svgEllipse.FillRule, viewport),
            SvgLine svgLine => svgLine.ToPath(svgLine.FillRule, viewport),
            SvgPolyline svgPolyline => svgPolyline.Points?.ToPath(svgPolyline.FillRule, false, viewport),
            SvgPolygon svgPolygon => svgPolygon.Points?.ToPath(svgPolygon.FillRule, true, viewport),
            _ => null
        };

        return path is not null;
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

    private static SKPicture? CreateDirectPathModel(
        SvgVisualElement visualElement,
        SKPath path,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        out bool canKeepRenderable)
    {
        canKeepRenderable = true;

        var fill = default(SKPaint);
        var stroke = default(SKPaint);
        var canDrawFill = true;
        var canDrawStroke = true;

        if (SvgScenePaintingService.IsValidFill(visualElement))
        {
            fill = SvgScenePaintingService.GetFillPaint(visualElement, geometryBounds, assetLoader, ignoreAttributes);
            if (fill is null)
            {
                canDrawFill = false;
            }
        }

        if (SvgScenePaintingService.IsValidStroke(visualElement, geometryBounds))
        {
            stroke = SvgScenePaintingService.GetStrokePaint(visualElement, geometryBounds, assetLoader, ignoreAttributes);
            if (stroke is null)
            {
                canDrawStroke = false;
            }
        }

        if (canDrawFill && !canDrawStroke)
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

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        if (fill is not null)
        {
            canvas.DrawPath(path, fill);
        }

        if (stroke is not null)
        {
            canvas.DrawPath(path, stroke);
        }

        var picture = recorder.EndRecording();
        return picture.Commands is { Count: > 0 } ? picture : null;
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
        node.RefreshElementIdentity(TryGetElementAddressKey(node.Element));
        AssignRetainedVisualState(node, node.Element);
        AssignRetainedResourceKeys(node, node.Element);

        if (node.MaskNode is { } maskNode)
        {
            RefreshGeneratedElementAddresses(maskNode);
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            RefreshGeneratedElementAddresses(node.Children[i]);
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

    private static SvgElement? ResolveReference(SvgElement owner, Uri? uri)
    {
        return uri is null ? null : owner.OwnerDocument?.GetElementById(uri.ToString());
    }

    private static string? TryGetResourceKey(SvgElement owner, Uri? uri)
    {
        return TryGetElementAddressKey(ResolveReference(owner, uri));
    }

    private static string? TryGetMaskResourceKey(SvgElement element)
    {
        if (!element.TryGetAttribute("mask", out string maskValue) || string.IsNullOrWhiteSpace(maskValue))
        {
            return null;
        }

        var svgMask = element.GetUriElementReference<SvgMask>("mask", new HashSet<Uri>());
        return TryGetElementAddressKey(svgMask);
    }

    internal static void AssignRetainedResourceKeys(SvgSceneNode node, SvgElement? element)
    {
        node.ClipResourceKey = null;
        node.MaskResourceKey = null;
        node.FilterResourceKey = null;

        if (element is not SvgVisualElement visualElement)
        {
            return;
        }

        node.ClipResourceKey = TryGetResourceKey(visualElement, visualElement.ClipPath);
        node.MaskResourceKey = TryGetMaskResourceKey(visualElement);
        node.FilterResourceKey = TryGetResourceKey(visualElement, visualElement.Filter);
    }

    internal static void AssignRetainedVisualState(SvgSceneNode node, SvgElement? element)
    {
        node.PointerEvents = SvgPointerEvents.VisiblePainted;
        node.IsVisible = true;
        node.IsDisplayNone = false;
        node.Cursor = null;
        node.CreatesBackgroundLayer = false;
        node.BackgroundClip = null;

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
    }

    private static Uri? GetUriAttribute(SvgElement element, string name)
    {
        return element.TryGetAttribute(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? new Uri(value, UriKind.RelativeOrAbsolute)
            : null;
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

        var node = new SvgSceneNode(
            SvgSceneNodeKind.Mask,
            svgMask,
            TryGetElementAddressKey(svgMask),
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
        AssignRetainedResourceKeys(node, svgMask);

        var compileContext = new SvgSceneCompileContext();
        _ = compileContext.TryEnter(svgMask.OwnerDocument, out var documentKey);

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
