using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg;
using Svg.Model;

namespace Svg.Skia;

public partial class SKSvg
{
    private readonly struct StaticNodeCanvasState
    {
        public StaticNodeCanvasState(
            bool hasBaseSave,
            bool hasMaskLayer,
            bool hasOpacityLayer,
            bool hasFilterLayer,
            bool hasStandaloneFilterOutput,
            SKRect? layerBounds)
        {
            HasBaseSave = hasBaseSave;
            HasMaskLayer = hasMaskLayer;
            HasOpacityLayer = hasOpacityLayer;
            HasFilterLayer = hasFilterLayer;
            HasStandaloneFilterOutput = hasStandaloneFilterOutput;
            LayerBounds = layerBounds;
        }

        public bool HasBaseSave { get; }

        public bool HasMaskLayer { get; }

        public bool HasOpacityLayer { get; }

        public bool HasFilterLayer { get; }

        public bool HasStandaloneFilterOutput { get; }

        public SKRect? LayerBounds { get; }
    }

    private sealed class AnimationLayerRootInfo
    {
        public AnimationLayerRootInfo(string compilationRootKey, SvgSceneNode rootNode, int order)
        {
            CompilationRootKey = compilationRootKey;
            RootNode = rootNode;
            Order = order;
        }

        public string CompilationRootKey { get; }

        public SvgSceneNode RootNode { get; }

        public int Order { get; }
    }

    private sealed class RetainedAnimationLayerEntry
    {
        public RetainedAnimationLayerEntry(string compilationRootKey, SvgSceneNode rootNode, int order)
        {
            CompilationRootKey = compilationRootKey;
            RootNode = rootNode;
            Order = order;
            IsDirty = true;
        }

        public string CompilationRootKey { get; }

        public SvgSceneNode RootNode { get; private set; }

        public int Order { get; private set; }

        public SKPicture? Model { get; private set; }

        public SkiaSharp.SKPicture? Picture { get; private set; }

        public bool IsDirty { get; private set; }

        public bool Sync(SvgSceneNode rootNode, int order)
        {
            var changed = !ReferenceEquals(RootNode, rootNode) || Order != order;
            RootNode = rootNode;
            Order = order;
            if (changed)
            {
                IsDirty = true;
            }

            return changed;
        }

        public void MarkDirty()
        {
            IsDirty = true;
        }

        public void Rebuild(
            SKSvg owner,
            SvgSceneDocument sceneDocument,
            DrawAttributes ignoreAttributes,
            List<SkiaSharp.SKPicture> deferredDisposals)
        {
            if (!IsDirty && Model is { } && Picture is { })
            {
                return;
            }

            var newModel = RecordOverlayNodeModel(sceneDocument, RootNode, ignoreAttributes);
            var newPicture = newModel is null ? null : owner.SkiaModel.ToSKPicture(newModel);

            owner.ReplaceRegisteredPicture(Model, Picture, newModel, newPicture, deferredDisposals);

            Model = newModel;
            Picture = newPicture;
            IsDirty = false;
        }

        public void Dispose(SKSvg owner, List<SkiaSharp.SKPicture>? deferredDisposals = null)
        {
            owner.DisposeRegisteredPicture(Model, Picture, deferredDisposals);
            Model = null;
            Picture = null;
            IsDirty = false;
        }
    }

    private string[]? _animationLayerTargetKeys;
    private RetainedAnimationLayerEntry[]? _animationLayerEntries;
    private Dictionary<string, RetainedAnimationLayerEntry>? _animationLayerEntriesByKey;
    private SKRect? _animationLayerBounds;
    private SKPicture? _staticAnimationLayerModel;
    private SKPicture? _dynamicAnimationLayerModel;
    private SkiaSharp.SKPicture? _staticAnimationLayerPicture;
    private SkiaSharp.SKPicture? _dynamicAnimationLayerPicture;

    public bool UsesAnimationLayerCaching { get; private set; }

    private bool TryInitializeAnimationLayerCaching(SvgSceneDocument sceneDocument)
    {
        if (AnimationController is null)
        {
            return false;
        }

        if (AnimationController.HasDocumentRootAnimationTargets())
        {
            DisableAnimationLayerCaching();
            return false;
        }

        var animatedTargetKeys = AnimationController.GetAnimatedTargetAddressKeys();
        if (animatedTargetKeys.Count == 0)
        {
            DisableAnimationLayerCaching();
            return false;
        }

        var deferredDisposals = new List<SkiaSharp.SKPicture>();
        try
        {
            if (!TryRefreshAnimationLayerEntries(
                    sceneDocument,
                    animatedTargetKeys,
                    markAllDirty: true,
                    deferredDisposals,
                    out _))
            {
                DisableAnimationLayerCaching();
                return false;
            }

            lock (Sync)
            {
                WaitForDrawsLocked();
                UsesAnimationLayerCaching = true;
                DisposeDeferredPictures(deferredDisposals);
            }

            return true;
        }
        catch
        {
            DisposeDeferredPictures(deferredDisposals);
            DisableAnimationLayerCaching();
            return false;
        }
    }

    private void DisableAnimationLayerCaching()
    {
        lock (Sync)
        {
            if (!HasAnimationLayerCachingStateLocked())
            {
                return;
            }

            WaitForDrawsLocked();

            if (_animationLayerEntries is { Length: > 0 } entries)
            {
                for (var i = 0; i < entries.Length; i++)
                {
                    entries[i].Dispose(this);
                }
            }

            DisposeRegisteredPicture(_staticAnimationLayerModel, _staticAnimationLayerPicture);
            DisposeRegisteredPicture(_dynamicAnimationLayerModel, _dynamicAnimationLayerPicture);

            _animationLayerTargetKeys = null;
            _animationLayerEntries = null;
            _animationLayerEntriesByKey = null;
            _animationLayerBounds = null;
            _staticAnimationLayerModel = null;
            _dynamicAnimationLayerModel = null;
            _staticAnimationLayerPicture = null;
            _dynamicAnimationLayerPicture = null;
            UsesAnimationLayerCaching = false;
        }
    }

    private bool HasAnimationLayerCachingStateLocked()
    {
        return UsesAnimationLayerCaching ||
               _animationLayerTargetKeys is not null ||
               _animationLayerEntries is not null ||
               _animationLayerEntriesByKey is not null ||
               _animationLayerBounds is not null ||
               _staticAnimationLayerModel is not null ||
               _dynamicAnimationLayerModel is not null ||
               _staticAnimationLayerPicture is not null ||
               _dynamicAnimationLayerPicture is not null;
    }

    private bool TryRenderAnimationLayerFrame(
        SvgSceneDocument sceneDocument,
        SvgAnimationFrameState frameState,
        SvgAnimationFrameState? previousState)
    {
        if (!UsesAnimationLayerCaching)
        {
            return false;
        }

        var deferredDisposals = new List<SkiaSharp.SKPicture>();
        var completed = false;

        try
        {
            if (!TryRefreshAnimationLayerEntries(
                    sceneDocument,
                    _animationLayerTargetKeys ?? Array.Empty<string>(),
                    markAllDirty: previousState is null,
                    deferredDisposals,
                    out var layerEntries))
            {
                return false;
            }

            if (previousState is null)
            {
                for (var i = 0; i < layerEntries.Length; i++)
                {
                    layerEntries[i].MarkDirty();
                }
            }
            else
            {
                for (var i = 0; i < layerEntries.Length; i++)
                {
                    if (layerEntries[i].RootNode.IsDirty)
                    {
                        layerEntries[i].MarkDirty();
                    }
                }

                foreach (var attribute in frameState.EnumerateDirtyAttributes(previousState))
                {
                    if (!TryMarkAnimationLayerEntriesDirty(
                            sceneDocument,
                            layerEntries,
                            attribute.TargetAddress.Key,
                            IsInheritedAnimationAttribute(attribute.AttributeName)))
                    {
                        return false;
                    }
                }

                foreach (var removedAttribute in frameState.EnumerateRemovedAttributes(previousState))
                {
                    if (!TryGetAddressKey(removedAttribute.Key, out var addressKey) ||
                        !TryMarkAnimationLayerEntriesDirty(
                            sceneDocument,
                            layerEntries,
                            addressKey,
                            IsInheritedAnimationAttribute(removedAttribute.AttributeName)))
                    {
                        return false;
                    }
                }
            }

            for (var i = 0; i < layerEntries.Length; i++)
            {
                layerEntries[i].Rebuild(this, sceneDocument, IgnoreAttributes, deferredDisposals);
            }

            var cullRect = _animationLayerBounds ?? sceneDocument.CullRect;
            var dynamicLayerModel = RecordDynamicLayerModel(layerEntries, cullRect);
            var dynamicLayerPicture = SkiaModel.ToSKPicture(dynamicLayerModel);
            var compositeModel = ComposeAnimationLayerModel(_staticAnimationLayerModel, dynamicLayerModel, cullRect);

            lock (Sync)
            {
                WaitForDrawsLocked();

                Model = compositeModel;

                ReplaceRegisteredPicture(
                    _dynamicAnimationLayerModel,
                    _dynamicAnimationLayerPicture,
                    dynamicLayerModel,
                    dynamicLayerPicture,
                    deferredDisposals);

                _dynamicAnimationLayerModel = dynamicLayerModel;
                _dynamicAnimationLayerPicture = dynamicLayerPicture;

                ClearRetainedPictureLocked();
                ClearSaveImageCacheLocked();
                _picture?.Dispose();
                _picture = null;

                WireframePicture?.Dispose();
                WireframePicture = null;

                sceneDocument.ClearDirty();
                DisposeDeferredPictures(deferredDisposals);
            }

            completed = true;
            return true;
        }
        finally
        {
            if (!completed)
            {
                DisposeDeferredPictures(deferredDisposals);
            }
        }
    }

    private bool TryDrawAnimationLayers(SkiaSharp.SKCanvas canvas)
    {
        if (!UsesAnimationLayerCaching)
        {
            return false;
        }

        SkiaSharp.SKPicture? staticLayerPicture;
        SkiaSharp.SKPicture? dynamicLayerPicture;
        lock (Sync)
        {
            staticLayerPicture = _staticAnimationLayerPicture;
            dynamicLayerPicture = _dynamicAnimationLayerPicture;
        }

        if (staticLayerPicture is null && dynamicLayerPicture is null)
        {
            return false;
        }

        if (staticLayerPicture is { })
        {
            canvas.DrawPicture(staticLayerPicture);
        }

        if (dynamicLayerPicture is { })
        {
            canvas.DrawPicture(dynamicLayerPicture);
        }

        return true;
    }

    private bool TryRefreshAnimationLayerEntries(
        SvgSceneDocument sceneDocument,
        IReadOnlyList<string> animatedTargetKeys,
        bool markAllDirty,
        List<SkiaSharp.SKPicture> deferredDisposals,
        out RetainedAnimationLayerEntry[] layerEntries)
    {
        layerEntries = Array.Empty<RetainedAnimationLayerEntry>();

        if (animatedTargetKeys.Count == 0 ||
            !TryCollectAnimationLayerRootInfos(sceneDocument, animatedTargetKeys, out var rootInfos, out var cullRect))
        {
            return false;
        }

        var existingEntriesByKey = _animationLayerEntriesByKey ?? new Dictionary<string, RetainedAnimationLayerEntry>(StringComparer.Ordinal);
        var nextEntriesByKey = new Dictionary<string, RetainedAnimationLayerEntry>(rootInfos.Count, StringComparer.Ordinal);
        layerEntries = new RetainedAnimationLayerEntry[rootInfos.Count];

        var topologyChanged = _animationLayerEntries is null ||
                              _animationLayerEntries.Length != rootInfos.Count ||
                              !_animationLayerBounds.HasValue ||
                              !AreRectsEqual(_animationLayerBounds.Value, cullRect);

        for (var i = 0; i < rootInfos.Count; i++)
        {
            var rootInfo = rootInfos[i];
            if (!existingEntriesByKey.TryGetValue(rootInfo.CompilationRootKey, out var entry))
            {
                entry = new RetainedAnimationLayerEntry(rootInfo.CompilationRootKey, rootInfo.RootNode, rootInfo.Order);
                topologyChanged = true;
            }
            else
            {
                topologyChanged |= entry.Sync(rootInfo.RootNode, rootInfo.Order);
            }

            if (markAllDirty)
            {
                entry.MarkDirty();
            }

            layerEntries[i] = entry;
            nextEntriesByKey[rootInfo.CompilationRootKey] = entry;

            if (!topologyChanged &&
                _animationLayerEntries is { } existingEntries &&
                existingEntries.Length > i &&
                !string.Equals(existingEntries[i].CompilationRootKey, entry.CompilationRootKey, StringComparison.Ordinal))
            {
                topologyChanged = true;
            }
        }

        foreach (var existingEntry in existingEntriesByKey)
        {
            if (!nextEntriesByKey.ContainsKey(existingEntry.Key))
            {
                existingEntry.Value.Dispose(this, deferredDisposals);
                topologyChanged = true;
            }
        }

        SKPicture? newStaticLayerModel = null;
        SkiaSharp.SKPicture? newStaticLayerPicture = null;
        if (topologyChanged || _staticAnimationLayerModel is null || _staticAnimationLayerPicture is null)
        {
            newStaticLayerModel = RecordStaticLayerModel(sceneDocument, rootInfos, cullRect, IgnoreAttributes);
            newStaticLayerPicture = SkiaModel.ToSKPicture(newStaticLayerModel);
            if (newStaticLayerPicture is null)
            {
                return false;
            }
        }

        lock (Sync)
        {
            WaitForDrawsLocked();

            _animationLayerTargetKeys = animatedTargetKeys is string[] keys ? (string[])keys.Clone() : new List<string>(animatedTargetKeys).ToArray();
            _animationLayerEntries = layerEntries;
            _animationLayerEntriesByKey = nextEntriesByKey;
            _animationLayerBounds = cullRect;

            if (newStaticLayerModel is not null && newStaticLayerPicture is not null)
            {
                ReplaceRegisteredPicture(
                    _staticAnimationLayerModel,
                    _staticAnimationLayerPicture,
                    newStaticLayerModel,
                    newStaticLayerPicture,
                    deferredDisposals);

                _staticAnimationLayerModel = newStaticLayerModel;
                _staticAnimationLayerPicture = newStaticLayerPicture;
            }
        }

        return true;
    }

    private bool TryMarkAnimationLayerEntriesDirty(
        SvgSceneDocument sceneDocument,
        IReadOnlyList<RetainedAnimationLayerEntry> layerEntries,
        string targetAddressKey,
        bool includeInheritedDescendants)
    {
        if (string.IsNullOrWhiteSpace(targetAddressKey))
        {
            return false;
        }

        var impactedCompilationRoots = new HashSet<string>(sceneDocument.GetCompilationRootsForMutation(targetAddressKey), StringComparer.Ordinal);
        if (includeInheritedDescendants)
        {
            for (var i = 0; i < layerEntries.Count; i++)
            {
                var rootAddressKey = layerEntries[i].RootNode.ElementAddressKey;
                if (IsSameOrDescendantAddress(rootAddressKey, targetAddressKey))
                {
                    impactedCompilationRoots.Add(layerEntries[i].CompilationRootKey);
                }
            }
        }

        if (impactedCompilationRoots.Count == 0)
        {
            return false;
        }

        var matched = false;
        for (var i = 0; i < layerEntries.Count; i++)
        {
            if (!impactedCompilationRoots.Contains(layerEntries[i].CompilationRootKey))
            {
                continue;
            }

            layerEntries[i].MarkDirty();
            matched = true;
        }

        return matched;
    }

    private void ReplaceRegisteredPicture(
        SKPicture? oldModel,
        SkiaSharp.SKPicture? oldPicture,
        SKPicture? newModel,
        SkiaSharp.SKPicture? newPicture,
        List<SkiaSharp.SKPicture> deferredDisposals)
    {
        DisposeRegisteredPicture(oldModel, oldPicture, deferredDisposals);

        if (newModel is { } model && newPicture is { })
        {
            SkiaModel.RegisterCachedPicture(model, newPicture);
        }
    }

    private void DisposeRegisteredPicture(
        SKPicture? model,
        SkiaSharp.SKPicture? picture,
        List<SkiaSharp.SKPicture>? deferredDisposals = null)
    {
        SkiaModel.UnregisterCachedPicture(model);

        if (picture is null)
        {
            return;
        }

        if (deferredDisposals is null)
        {
            picture.Dispose();
        }
        else
        {
            deferredDisposals.Add(picture);
        }
    }

    private static void DisposeDeferredPictures(List<SkiaSharp.SKPicture> deferredDisposals)
    {
        for (var i = 0; i < deferredDisposals.Count; i++)
        {
            deferredDisposals[i].Dispose();
        }

        deferredDisposals.Clear();
    }

    private static bool TryCollectAnimationLayerRootInfos(
        SvgSceneDocument sceneDocument,
        IReadOnlyList<string> animatedTargetKeys,
        out List<AnimationLayerRootInfo> rootInfos,
        out SKRect cullRect)
    {
        rootInfos = new List<AnimationLayerRootInfo>();
        cullRect = sceneDocument.CullRect;
        if (cullRect.IsEmpty)
        {
            cullRect = sceneDocument.Root.TransformedBounds;
        }

        if (cullRect.IsEmpty)
        {
            return false;
        }

        var impactedCompilationRoots = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < animatedTargetKeys.Count; i++)
        {
            foreach (var compilationRootKey in sceneDocument.GetCompilationRootsForMutation(animatedTargetKeys[i]))
            {
                if (!string.IsNullOrWhiteSpace(compilationRootKey))
                {
                    impactedCompilationRoots.Add(compilationRootKey);
                }
            }
        }

        var selectedCompilationRoots = PruneCompilationRoots(sceneDocument, impactedCompilationRoots);
        if (selectedCompilationRoots.Count == 0)
        {
            return false;
        }

        var orderLookup = new Dictionary<string, int>(selectedCompilationRoots.Count, StringComparer.Ordinal);
        var selectedRootKeys = new HashSet<string>(selectedCompilationRoots, StringComparer.Ordinal);
        var order = 0;
        PopulateCompilationRootOrder(sceneDocument.Root, selectedRootKeys, orderLookup, ref order);

        for (var i = 0; i < selectedCompilationRoots.Count; i++)
        {
            var compilationRootKey = selectedCompilationRoots[i];
            if (!sceneDocument.TryGetCompilationRoot(compilationRootKey, out var node) ||
                node is null ||
                !orderLookup.TryGetValue(compilationRootKey, out var nodeOrder))
            {
                return false;
            }

            rootInfos.Add(new AnimationLayerRootInfo(compilationRootKey, node, nodeOrder));
        }

        rootInfos.Sort(static (left, right) => left.Order.CompareTo(right.Order));
        return rootInfos.Count > 0;
    }

    private static List<string> PruneCompilationRoots(
        SvgSceneDocument sceneDocument,
        IReadOnlyCollection<string> compilationRootKeys)
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
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
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

            if (skip ||
                string.IsNullOrWhiteSpace(node.CompilationRootKey) ||
                !selected.Add(node.CompilationRootKey!))
            {
                continue;
            }

            result.Add(node.CompilationRootKey!);
        }

        return result;
    }

    private static void PopulateCompilationRootOrder(
        SvgSceneNode node,
        HashSet<string> selectedCompilationRoots,
        Dictionary<string, int> orderLookup,
        ref int order)
    {
        if (node.IsCompilationRootBoundary &&
            !string.IsNullOrWhiteSpace(node.CompilationRootKey) &&
            selectedCompilationRoots.Contains(node.CompilationRootKey!) &&
            !orderLookup.ContainsKey(node.CompilationRootKey!))
        {
            orderLookup.Add(node.CompilationRootKey!, order++);
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            PopulateCompilationRootOrder(node.Children[i], selectedCompilationRoots, orderLookup, ref order);
        }

        if (node.MaskNode is { } maskNode)
        {
            PopulateCompilationRootOrder(maskNode, selectedCompilationRoots, orderLookup, ref order);
        }
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

    private static bool IsInheritedAnimationAttribute(string attributeName)
    {
        return SvgAnimationInvalidation.AffectsDescendantSubtree(attributeName);
    }

    private static bool IsSameOrDescendantAddress(string? candidateAddressKey, string ancestorAddressKey)
    {
        if (string.IsNullOrWhiteSpace(candidateAddressKey) || string.IsNullOrWhiteSpace(ancestorAddressKey))
        {
            return false;
        }

        if (string.Equals(candidateAddressKey, ancestorAddressKey, StringComparison.Ordinal))
        {
            return true;
        }

        return candidateAddressKey!.StartsWith(ancestorAddressKey + "/", StringComparison.Ordinal);
    }

    private static bool TryGetAddressKey(string targetAttributeKey, out string addressKey)
    {
        var separatorIndex = targetAttributeKey.IndexOf('|');
        if (separatorIndex < 0)
        {
            addressKey = targetAttributeKey;
            return !string.IsNullOrWhiteSpace(addressKey);
        }

        addressKey = targetAttributeKey.Substring(0, separatorIndex);
        return !string.IsNullOrWhiteSpace(addressKey);
    }

    private static bool AreRectsEqual(SKRect left, SKRect right)
    {
        return left.Left == right.Left &&
               left.Top == right.Top &&
               left.Right == right.Right &&
               left.Bottom == right.Bottom;
    }

    private static SKPicture RecordStaticLayerModel(
        SvgSceneDocument sceneDocument,
        IReadOnlyList<AnimationLayerRootInfo> rootInfos,
        SKRect cullRect,
        DrawAttributes ignoreAttributes)
    {
        var cutRoots = new HashSet<SvgSceneNode>();
        for (var i = 0; i < rootInfos.Count; i++)
        {
            cutRoots.Add(rootInfos[i].RootNode);
        }

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        _ = RenderStaticNodeToCanvas(sceneDocument.Root, canvas, cutRoots, ignoreAttributes);
        return recorder.EndRecording();
    }

    private static SKPicture RecordDynamicLayerModel(RetainedAnimationLayerEntry[] layerEntries, SKRect cullRect)
    {
        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);

        for (var i = 0; i < layerEntries.Length; i++)
        {
            if (layerEntries[i].Model is { } layerModel)
            {
                canvas.DrawPicture(layerModel);
            }
        }

        return recorder.EndRecording();
    }

    private static SKPicture ComposeAnimationLayerModel(SKPicture? staticLayerModel, SKPicture? dynamicLayerModel, SKRect cullRect)
    {
        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);

        if (staticLayerModel is { })
        {
            canvas.DrawPicture(staticLayerModel);
        }

        if (dynamicLayerModel is { })
        {
            canvas.DrawPicture(dynamicLayerModel);
        }

        return recorder.EndRecording();
    }

    private static SKPicture? RecordOverlayNodeModel(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        DrawAttributes ignoreAttributes)
    {
        var bounds = SvgSceneNodeBoundsService.GetRenderableBounds(node);
        if (bounds.IsEmpty || bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return null;
        }

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(bounds);

        if (!node.TotalTransform.IsIdentity)
        {
            canvas.SetMatrix(node.TotalTransform);
        }

        _ = SvgSceneRenderer.RenderNodeToCanvas(
            sceneDocument,
            node,
            canvas,
            ignoreAttributes,
            until: null,
            enableTransform: false);
        return recorder.EndRecording();
    }

    private static bool RenderStaticNodeToCanvas(
        SvgSceneNode node,
        SKCanvas canvas,
        HashSet<SvgSceneNode> cutRoots,
        DrawAttributes ignoreAttributes)
    {
        if (cutRoots.Contains(node))
        {
            return true;
        }

        if (!node.IsRenderable && node.StandaloneFilterModel is null)
        {
            return true;
        }

        var enableClip = !ignoreAttributes.HasFlag(DrawAttributes.ClipPath);
        var enableMask = !ignoreAttributes.HasFlag(DrawAttributes.Mask);
        var enableOpacity = !ignoreAttributes.HasFlag(DrawAttributes.Opacity);
        var enableFilter = !ignoreAttributes.HasFlag(DrawAttributes.Filter);
        var state = ApplyStaticNodeCanvasState(node, canvas, enableClip, enableMask, enableOpacity, enableFilter);

        if (state.HasStandaloneFilterOutput)
        {
            DrawStandaloneFilterOutput(node, canvas);
        }
        else
        {
            SvgSceneRenderer.DrawNodeLocalVisuals(node, canvas);

            for (var i = 0; i < node.Children.Count; i++)
            {
                if (!RenderStaticNodeToCanvas(node.Children[i], canvas, cutRoots, ignoreAttributes))
                {
                    RestoreStaticNode(canvas, state);
                    return false;
                }
            }
        }

        if (state.HasMaskLayer && node.MaskNode is { } maskNode && node.MaskDstIn is { } maskDstIn)
        {
            if (state.LayerBounds is { } maskLayerBounds)
            {
                canvas.SaveLayer(maskLayerBounds, maskDstIn);
            }
            else
            {
                canvas.SaveLayer(maskDstIn);
            }

            _ = RenderStaticNodeToCanvas(maskNode, canvas, cutRoots, ignoreAttributes);
            canvas.Restore();
        }

        RestoreStaticNode(canvas, state);
        return true;
    }

    private static StaticNodeCanvasState ApplyStaticNodeCanvasState(
        SvgSceneNode node,
        SKCanvas canvas,
        bool enableClipPath,
        bool enableMask,
        bool enableOpacity,
        bool enableFilter)
    {
        var hasTransform = !node.Transform.IsIdentity;
        var hasClipPath = enableClipPath && node.ClipPath is not null;
        var hasMaskLayer = enableMask && node.MaskPaint is not null && node.MaskNode is not null;
        var canFoldOpacity = enableOpacity && SvgScenePaintingService.CanFoldOpacityIntoLeafDirectDraw(node);
        var hasOpacityLayer = enableOpacity && node.Opacity is not null && !canFoldOpacity;
        var hasStandaloneFilterOutput = enableFilter && node.StandaloneFilterModel is not null;
        var hasFilterLayer = enableFilter && node.Filter is not null && !hasStandaloneFilterOutput;
        var layerBounds = (hasMaskLayer || hasOpacityLayer)
            ? ResolveCurrentLayerBounds(node)
            : null;
        SKRect? filterLayerBounds = hasFilterLayer && node.FilterClip is { } filterClipCandidate && !filterClipCandidate.IsEmpty
            ? SvgSceneNodeBoundsService.GetPixelAlignedBounds(filterClipCandidate)
            : null;
        var hasBaseSave = node.Overflow is not null ||
            hasTransform ||
            node.Clip is not null ||
            hasClipPath ||
            node.InnerClip is not null ||
            ((hasFilterLayer || hasStandaloneFilterOutput) && node.FilterClip is not null);

        if (hasBaseSave)
        {
            canvas.Save();
        }

        if (node.Overflow is { } overflow)
        {
            canvas.ClipRect(overflow, SKClipOperation.Intersect);
        }

        if (hasTransform)
        {
            canvas.SetMatrix(node.Transform);
        }

        if (node.Clip is { } clip)
        {
            canvas.ClipRect(clip, SKClipOperation.Intersect);
        }

        if (hasClipPath)
        {
            canvas.ClipPath(node.ClipPath!, SKClipOperation.Intersect, node.IsAntialias);
        }

        if (node.InnerClip is { } innerClip)
        {
            canvas.ClipRect(innerClip, SKClipOperation.Intersect);
        }

        if ((hasFilterLayer || hasStandaloneFilterOutput) && node.FilterClip is { } filterClip)
        {
            canvas.ClipRect(filterClip, SKClipOperation.Intersect);
        }

        if (hasMaskLayer)
        {
            if (layerBounds is { } maskLayerBounds)
            {
                canvas.SaveLayer(maskLayerBounds, node.MaskPaint!);
            }
            else
            {
                canvas.SaveLayer(node.MaskPaint!);
            }
        }

        if (hasOpacityLayer)
        {
            if (layerBounds is { } opacityLayerBounds)
            {
                canvas.SaveLayer(opacityLayerBounds, node.Opacity!);
            }
            else
            {
                canvas.SaveLayer(node.Opacity!);
            }
        }

        if (hasFilterLayer)
        {
            if (filterLayerBounds is { } boundedFilterLayer)
            {
                canvas.SaveLayer(boundedFilterLayer, node.Filter!);
            }
            else
            {
                canvas.SaveLayer(node.Filter!);
            }
        }

        return new StaticNodeCanvasState(hasBaseSave, hasMaskLayer, hasOpacityLayer, hasFilterLayer, hasStandaloneFilterOutput, layerBounds);
    }

    private static void DrawStandaloneFilterOutput(
        SvgSceneNode node,
        SKCanvas canvas)
    {
        if (node.StandaloneFilterModel is not { } standaloneFilterModel)
        {
            return;
        }

        canvas.DrawPicture(standaloneFilterModel);
    }

    private static void RestoreStaticNode(
        SKCanvas canvas,
        StaticNodeCanvasState state)
    {
        if (state.HasFilterLayer)
        {
            canvas.Restore();
        }

        if (state.HasOpacityLayer)
        {
            canvas.Restore();
        }

        if (state.HasMaskLayer)
        {
            canvas.Restore();
        }

        if (state.HasBaseSave)
        {
            canvas.Restore();
        }
    }

    private static SKRect? ResolveCurrentLayerBounds(SvgSceneNode node)
    {
        var localBounds = SvgSceneNodeBoundsService.GetLocalRenderablePaintBounds(node);
        if (localBounds.IsEmpty)
        {
            return null;
        }

        return SvgSceneNodeBoundsService.GetPixelAlignedBounds(localBounds);
    }
}
