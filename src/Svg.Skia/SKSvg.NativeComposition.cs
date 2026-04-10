using System;
using System.Collections.Generic;
using System.Linq;
using ShimSkiaSharp;
using Svg;
using Svg.Model;

namespace Svg.Skia;

public partial class SKSvg
{
    private SvgDocument? _nativeCompositionSourceDocument;
    private SvgSceneDocument? _nativeCompositionSourceScene;
    private int[]? _nativeCompositionAnimatedChildIndexes;
    private string[]? _nativeCompositionAnimatedTargetKeys;
    private SKRect _nativeCompositionSourceBounds;
    private DrawAttributes _nativeCompositionIgnoreAttributes;

    public bool SupportsNativeComposition
    {
        get
        {
            return TryGetRenderableNativeCompositionAnimatedChildIndexes(out _, out _, out _);
        }
    }

    public bool TryCreateNativeCompositionScene(out SvgNativeCompositionScene? scene)
    {
        scene = null;

        if (!TryGetNativeCompositionState(
                out var sourceScene,
                out var currentScene,
                out var animatedChildIndexes,
                out var sourceBounds))
        {
            return false;
        }

        var animatedChildIndexSet = new HashSet<int>(animatedChildIndexes);
        var layers = new List<SvgNativeCompositionLayer>(sourceScene.Root.Children.Count);

        for (var i = 0; i < sourceScene.Root.Children.Count; i++)
        {
            var isAnimated = animatedChildIndexSet.Contains(i);
            var layerScene = isAnimated ? currentScene : sourceScene;
            if (!TryGetNativeCompositionRootNode(layerScene, i, out var layerNode) || layerNode is null)
            {
                layers.Add(CreateHiddenNativeCompositionLayer(i, isAnimated));
                continue;
            }

            layers.Add(CreateNativeCompositionLayer(layerScene, layerNode, i, isAnimated));
        }

        scene = new SvgNativeCompositionScene(sourceBounds, layers);
        return true;
    }

    public bool TryCreateNativeCompositionFrame(out SvgNativeCompositionFrame? frame)
    {
        frame = null;

        if (!TryGetNativeCompositionState(
                out _,
                out var currentScene,
                out var animatedChildIndexes,
                out var sourceBounds))
        {
            return false;
        }

        var layers = new List<SvgNativeCompositionLayer>(animatedChildIndexes.Count);
        foreach (var animatedChildIndex in animatedChildIndexes)
        {
            if (animatedChildIndex < 0 || animatedChildIndex >= currentScene.Root.Children.Count)
            {
                return false;
            }

            if (!TryGetNativeCompositionRootNode(currentScene, animatedChildIndex, out var layerNode) || layerNode is null)
            {
                layers.Add(CreateHiddenNativeCompositionLayer(animatedChildIndex, isAnimated: true));
                continue;
            }

            layers.Add(CreateNativeCompositionLayer(currentScene, layerNode, animatedChildIndex, isAnimated: true));
        }

        frame = new SvgNativeCompositionFrame(sourceBounds, layers);
        return true;
    }

    private bool TryGetNativeCompositionState(
        out SvgSceneDocument sourceScene,
        out SvgSceneDocument currentScene,
        out IReadOnlyList<int> animatedChildIndexes,
        out SKRect sourceBounds)
    {
        sourceScene = null!;
        currentScene = null!;
        animatedChildIndexes = Array.Empty<int>();
        sourceBounds = SKRect.Empty;

        if (!TryGetRenderableNativeCompositionAnimatedChildIndexes(out sourceScene, out var renderableAnimatedChildIndexes, out sourceBounds) ||
            AnimationController is not { } animationController)
        {
            return false;
        }

        animatedChildIndexes = renderableAnimatedChildIndexes;
        if (!TryGetRetainedSceneForDocument(GetNativeCompositionDocument(animationController), sourceBounds, out currentScene))
        {
            return false;
        }

        return true;
    }

    private bool TryGetRenderableNativeCompositionAnimatedChildIndexes(
        out SvgSceneDocument sourceScene,
        out int[] animatedChildIndexes,
        out SKRect sourceBounds)
    {
        sourceScene = null!;
        animatedChildIndexes = Array.Empty<int>();
        sourceBounds = SKRect.Empty;

        if (SourceDocument is not { } currentSourceDocument ||
            AnimationController is not { } animationController ||
            animationController.HasDocumentRootAnimationTargets() ||
            animationController.GetAnimatedTargetAddressKeys() is not { Count: > 0 } animatedTargetKeys ||
            !TryGetNativeCompositionSourceBounds(out sourceBounds))
        {
            return false;
        }

        if (TryGetCachedNativeCompositionSourceState(
                currentSourceDocument,
                animatedTargetKeys,
                sourceBounds,
                out sourceScene,
                out animatedChildIndexes))
        {
            return true;
        }

        if (!TryGetRetainedSceneForDocument(currentSourceDocument, sourceBounds, out sourceScene))
        {
            return false;
        }

        var renderableIndexes = new SortedSet<int>();
        for (var i = 0; i < animatedTargetKeys.Count; i++)
        {
            if (!TryAddRenderableNativeCompositionAnimatedChildIndexes(sourceScene, animatedTargetKeys[i], renderableIndexes))
            {
                return false;
            }
        }

        if (renderableIndexes.Count == 0)
        {
            InvalidateNativeCompositionState();
            return false;
        }

        animatedChildIndexes = renderableIndexes.ToArray();
        CacheNativeCompositionSourceState(currentSourceDocument, sourceScene, animatedTargetKeys, animatedChildIndexes, sourceBounds);
        return true;
    }

    private bool TryGetCachedNativeCompositionSourceState(
        SvgDocument sourceDocument,
        IReadOnlyList<string> animatedTargetKeys,
        SKRect sourceBounds,
        out SvgSceneDocument sourceScene,
        out int[] animatedChildIndexes)
    {
        sourceScene = null!;
        animatedChildIndexes = Array.Empty<int>();

        if (_nativeCompositionSourceScene is null ||
            _nativeCompositionAnimatedChildIndexes is null ||
            _nativeCompositionAnimatedTargetKeys is null ||
            !ReferenceEquals(_nativeCompositionSourceDocument, sourceDocument) ||
            _nativeCompositionIgnoreAttributes != IgnoreAttributes ||
            !AreRectsEqual(_nativeCompositionSourceBounds, sourceBounds) ||
            _nativeCompositionAnimatedTargetKeys.Length != animatedTargetKeys.Count)
        {
            return false;
        }

        for (var i = 0; i < animatedTargetKeys.Count; i++)
        {
            if (!string.Equals(_nativeCompositionAnimatedTargetKeys[i], animatedTargetKeys[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        sourceScene = _nativeCompositionSourceScene;
        animatedChildIndexes = _nativeCompositionAnimatedChildIndexes;
        return true;
    }

    private void CacheNativeCompositionSourceState(
        SvgDocument sourceDocument,
        SvgSceneDocument sourceScene,
        IReadOnlyList<string> animatedTargetKeys,
        int[] animatedChildIndexes,
        SKRect sourceBounds)
    {
        _nativeCompositionSourceDocument = sourceDocument;
        _nativeCompositionSourceScene = sourceScene;
        _nativeCompositionAnimatedChildIndexes = animatedChildIndexes;
        _nativeCompositionAnimatedTargetKeys = animatedTargetKeys is string[] keys
            ? (string[])keys.Clone()
            : new List<string>(animatedTargetKeys).ToArray();
        _nativeCompositionSourceBounds = sourceBounds;
        _nativeCompositionIgnoreAttributes = IgnoreAttributes;
    }

    private void InvalidateNativeCompositionState()
    {
        _nativeCompositionSourceDocument = null;
        _nativeCompositionSourceScene = null;
        _nativeCompositionAnimatedChildIndexes = null;
        _nativeCompositionAnimatedTargetKeys = null;
        _nativeCompositionSourceBounds = SKRect.Empty;
        _nativeCompositionIgnoreAttributes = DrawAttributes.None;
    }

    private static bool TryAddRenderableNativeCompositionAnimatedChildIndexes(
        SvgSceneDocument sceneDocument,
        string addressKey,
        ISet<int> renderableIndexes)
    {
        if (!sceneDocument.TryGetNodes(addressKey, out var nodes) || nodes.Count == 0)
        {
            return false;
        }

        var foundRenderableRoot = false;
        for (var i = 0; i < nodes.Count; i++)
        {
            if (!TryGetNativeCompositionRootNode(sceneDocument, nodes[i], out var rootNode, out var documentChildIndex) ||
                rootNode is null ||
                !CanRenderNativeCompositionRoot(rootNode))
            {
                continue;
            }

            renderableIndexes.Add(documentChildIndex);
            foundRenderableRoot = true;
        }

        return foundRenderableRoot;
    }

    private bool TryGetRetainedSceneForDocument(SvgDocument document, SKRect sourceBounds, out SvgSceneDocument sceneDocument)
    {
        if (TryEnsureRetainedSceneGraph(out var retainedSceneDocument) &&
            retainedSceneDocument is not null &&
            ReferenceEquals(retainedSceneDocument.SourceDocument, document))
        {
            sceneDocument = retainedSceneDocument;
            return true;
        }

        if (SvgSceneCompiler.TryCompile(document, sourceBounds, AssetLoader, IgnoreAttributes, out var compiledSceneDocument) &&
            compiledSceneDocument is not null)
        {
            sceneDocument = compiledSceneDocument;
            return true;
        }

        sceneDocument = null!;
        return false;
    }

    private static bool TryGetNativeCompositionRootNode(SvgSceneDocument sceneDocument, int documentChildIndex, out SvgSceneNode? node)
    {
        node = null;
        if (documentChildIndex < 0 || documentChildIndex >= sceneDocument.Root.Children.Count)
        {
            return false;
        }

        node = sceneDocument.Root.Children[documentChildIndex];
        return true;
    }

    private static bool TryGetNativeCompositionRootNode(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        out SvgSceneNode? rootNode,
        out int documentChildIndex)
    {
        rootNode = null;
        documentChildIndex = -1;

        var current = node;
        while (current.Parent is { } parent && !ReferenceEquals(parent, sceneDocument.Root))
        {
            current = parent;
        }

        if (!ReferenceEquals(current.Parent, sceneDocument.Root))
        {
            return false;
        }

        documentChildIndex = -1;
        for (var i = 0; i < sceneDocument.Root.Children.Count; i++)
        {
            if (!ReferenceEquals(sceneDocument.Root.Children[i], current))
            {
                continue;
            }

            documentChildIndex = i;
            break;
        }

        if (documentChildIndex < 0)
        {
            return false;
        }

        rootNode = current;
        return true;
    }

    private SvgDocument GetNativeCompositionDocument(SvgAnimationController animationController)
    {
        if (_animatedDocument is { } animatedDocument)
        {
            return animatedDocument;
        }

        if (_lastRenderedAnimationFrameState is { } renderedFrameState)
        {
            return animationController.CreateAnimatedDocument(renderedFrameState);
        }

        return AnimationTime > TimeSpan.Zero
            ? animationController.CreateAnimatedDocument(AnimationTime)
            : SourceDocument!;
    }

    private bool TryGetNativeCompositionSourceBounds(out SKRect sourceBounds)
    {
        if (Model is { } model)
        {
            sourceBounds = model.CullRect;
            return true;
        }

        if (Picture is { } picture)
        {
            sourceBounds = new SKRect(
                picture.CullRect.Left,
                picture.CullRect.Top,
                picture.CullRect.Right,
                picture.CullRect.Bottom);
            return true;
        }

        sourceBounds = SKRect.Empty;
        return false;
    }

    private SvgNativeCompositionLayer CreateNativeCompositionLayer(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        int documentChildIndex,
        bool isAnimated)
    {
        if (!node.IsRenderable)
        {
            return CreateHiddenNativeCompositionLayer(documentChildIndex, isAnimated);
        }

        var canExtractTranslation = TryGetNativeCompositionTranslation(node, out var nativeTranslation);
        var opacity = TryGetNativeCompositionOpacity(node);
        var canExtractOpacity = opacity < 1f;
        var drawBounds = SvgSceneNodeBoundsService.GetRenderableBounds(node);
        if (canExtractTranslation)
        {
            drawBounds = OffsetRect(drawBounds, -nativeTranslation.X, -nativeTranslation.Y);
        }

        if (drawBounds.IsEmpty || drawBounds.Width <= 0 || drawBounds.Height <= 0)
        {
            return CreateHiddenNativeCompositionLayer(documentChildIndex, isAnimated);
        }

        var picture = RecordNativeCompositionPicture(
            sceneDocument,
            node,
            drawBounds,
            extractOpacity: canExtractOpacity,
            extractTranslation: canExtractTranslation);

        var offset = new SKPoint(
            drawBounds.Left + nativeTranslation.X,
            drawBounds.Top + nativeTranslation.Y);

        return new SvgNativeCompositionLayer(
            GetNativeCompositionDocumentChildIndex(node, documentChildIndex),
            isAnimated,
            picture,
            offset,
            new SKSize(drawBounds.Width, drawBounds.Height),
            opacity,
            isVisible: true);
    }

    private static bool CanRenderNativeCompositionRoot(SvgSceneNode node)
    {
        if (!node.IsRenderable)
        {
            return false;
        }

        var drawBounds = SvgSceneNodeBoundsService.GetRenderableBounds(node);
        if (TryGetNativeCompositionTranslation(node, out var nativeTranslation))
        {
            drawBounds = OffsetRect(drawBounds, -nativeTranslation.X, -nativeTranslation.Y);
        }

        return !drawBounds.IsEmpty && drawBounds.Width > 0 && drawBounds.Height > 0;
    }

    private static SKRect OffsetRect(SKRect rect, float dx, float dy)
    {
        if (rect.IsEmpty || (dx == 0f && dy == 0f))
        {
            return rect;
        }

        return new SKRect(rect.Left + dx, rect.Top + dy, rect.Right + dx, rect.Bottom + dy);
    }

    private static SvgNativeCompositionLayer CreateHiddenNativeCompositionLayer(int documentChildIndex, bool isAnimated)
    {
        return new SvgNativeCompositionLayer(
            documentChildIndex,
            isAnimated,
            picture: null,
            offset: SKPoint.Empty,
            size: SKSize.Empty,
            opacity: 0f,
            isVisible: false);
    }

    private static int GetNativeCompositionDocumentChildIndex(SvgSceneNode node, int fallbackIndex)
    {
        var elementAddressKey = node.ElementAddressKey;
        if (string.IsNullOrWhiteSpace(elementAddressKey))
        {
            return fallbackIndex;
        }

        var resolvedElementAddressKey = elementAddressKey!;
        var separatorIndex = resolvedElementAddressKey.IndexOf('/');
        var topLevelIndexText = separatorIndex >= 0
            ? resolvedElementAddressKey.Substring(0, separatorIndex)
            : resolvedElementAddressKey;

        return int.TryParse(topLevelIndexText, out var topLevelIndex)
            ? topLevelIndex
            : fallbackIndex;
    }

    private SKPicture RecordNativeCompositionPicture(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKRect drawBounds,
        bool extractOpacity,
        bool extractTranslation)
    {
        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SKRect.Create(0f, 0f, drawBounds.Width, drawBounds.Height));

        if (drawBounds.Left != 0f || drawBounds.Top != 0f)
        {
            canvas.SetMatrix(SKMatrix.CreateTranslation(-drawBounds.Left, -drawBounds.Top));
        }

        SvgSceneRenderer.RenderNodeToCanvas(
            sceneDocument,
            node,
            canvas,
            _ignoreAttributes,
            until: null,
            enableTransform: !extractTranslation,
            ignoreCurrentOpacity: extractOpacity);
        return recorder.EndRecording();
    }

    private static bool TryGetNativeCompositionTranslation(SvgSceneNode node, out SKPoint translation)
    {
        translation = SKPoint.Empty;

        var transform = node.Transform;
        if (transform.ScaleX != 1f ||
            transform.ScaleY != 1f ||
            transform.SkewX != 0f ||
            transform.SkewY != 0f ||
            transform.Persp0 != 0f ||
            transform.Persp1 != 0f ||
            transform.Persp2 != 1f ||
            (transform.TransX == 0f && transform.TransY == 0f))
        {
            return false;
        }

        translation = new SKPoint(transform.TransX, transform.TransY);
        return true;
    }

    private static float TryGetNativeCompositionOpacity(SvgSceneNode node)
    {
        var opacity = node.OpacityValue;
        if (opacity >= 1f)
        {
            return 1f;
        }

        if (opacity <= 0f)
        {
            return 0f;
        }

        return opacity >= 1f ? 1f : opacity;
    }
}
