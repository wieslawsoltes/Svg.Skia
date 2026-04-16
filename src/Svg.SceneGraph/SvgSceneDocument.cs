using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using ShimSkiaSharp;
using Svg;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

public sealed class SvgSceneDocument
{
    private readonly Dictionary<string, List<SvgSceneNode>> _nodesByAddress = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgSceneNode> _nodesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgSceneNode> _compilationRootsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<SvgElement, List<string>> _compilationRootKeysByElement = new(SvgElementReferenceComparer.Instance);
    private readonly Dictionary<string, HashSet<string>> _compilationRootsByDependentAddress = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _dependentAddressesByCompilationRoot = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _dependentResourceKeysByCompilationRoot = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgSceneResource> _resourcesByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgSceneResource> _resourcesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<SvgSceneResource>> _resourcesByAddress = new(StringComparer.Ordinal);
    private readonly HashSet<string> _resolvingMaskResourceKeys = new(StringComparer.Ordinal);
    private readonly ReadOnlyDictionary<string, SvgSceneNode> _readOnlyNodesById;
    private readonly ReadOnlyDictionary<string, SvgSceneResource> _readOnlyResourcesById;

    internal SvgSceneDocument(
        SvgDocument? sourceDocument,
        SKRect cullRect,
        SKRect compilationViewport,
        SvgSceneNode root,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes)
    {
        SourceDocument = sourceDocument;
        CullRect = cullRect;
        CompilationViewport = compilationViewport;
        Root = root ?? throw new ArgumentNullException(nameof(root));
        AssetLoader = assetLoader ?? throw new ArgumentNullException(nameof(assetLoader));
        IgnoreAttributes = ignoreAttributes;
        _readOnlyNodesById = new ReadOnlyDictionary<string, SvgSceneNode>(_nodesById);
        _readOnlyResourcesById = new ReadOnlyDictionary<string, SvgSceneResource>(_resourcesById);
        RebuildIndexesAndDependencies();
    }

    public SvgDocument? SourceDocument { get; }

    public SKRect CullRect { get; }

    public SvgSceneNode Root { get; }

    public IReadOnlyDictionary<string, SvgSceneNode> NodesById => _readOnlyNodesById;

    public IReadOnlyDictionary<string, SvgSceneResource> ResourcesById => _readOnlyResourcesById;

    public long Revision { get; private set; }

    internal SKRect CompilationViewport { get; }

    internal ISvgAssetLoader AssetLoader { get; }

    internal DrawAttributes IgnoreAttributes { get; }

    public IEnumerable<SvgSceneNode> Traverse()
    {
        return Traverse(Root);
    }

    public bool TryGetNodes(string addressKey, out IReadOnlyList<SvgSceneNode> nodes)
    {
        if (_nodesByAddress.TryGetValue(addressKey, out var sceneNodes))
        {
            nodes = sceneNodes;
            return true;
        }

        nodes = Array.Empty<SvgSceneNode>();
        return false;
    }

    public bool TryGetNode(string addressKey, out SvgSceneNode? node)
    {
        if (_nodesByAddress.TryGetValue(addressKey, out var sceneNodes) &&
            sceneNodes.Count > 0)
        {
            node = sceneNodes[0];
            return true;
        }

        node = null;
        return false;
    }

    public bool TryGetNode(SvgElement element, out SvgSceneNode? node)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return TryGetNode(SvgSceneCompiler.TryGetElementAddressKey(element) ?? string.Empty, out node);
    }

    public bool TryGetNodeById(string id, out SvgSceneNode? node)
    {
        if (_nodesById.TryGetValue(id, out node))
        {
            return true;
        }

        node = null;
        return false;
    }

    public bool TryGetResourceById(string id, out SvgSceneResource? resource)
    {
        if (_resourcesById.TryGetValue(id, out resource))
        {
            return true;
        }

        resource = null;
        return false;
    }

    public bool TryGetResource(string addressKey, out SvgSceneResource? resource)
    {
        if (_resourcesByKey.TryGetValue(addressKey, out resource))
        {
            return true;
        }

        resource = null;
        return false;
    }

    public bool TryGetElement(string addressKey, out SvgElement? element)
    {
        return TryResolveElement(addressKey, out element);
    }

    public bool TryGetElementById(string id, out SvgElement? element)
    {
        element = null;
        if (SourceDocument is null || string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        element = SourceDocument.GetElementById(id);
        return element is not null;
    }

    public int MarkDirty(string addressKey, bool includeDescendants = false)
    {
        if (!_nodesByAddress.TryGetValue(addressKey, out var nodes))
        {
            return 0;
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            if (includeDescendants)
            {
                nodes[i].MarkSubtreeDirty();
            }
            else
            {
                nodes[i].MarkDirty();
            }
        }

        Revision++;
        return nodes.Count;
    }

    public SvgSceneMutationResult ApplyMutation(SvgElement element, IReadOnlyCollection<string>? changedAttributes = null)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return SvgSceneCompiler.ApplyMutation(this, element, changedAttributes);
    }

    public SvgSceneMutationResult ApplyMutation(string addressKey, IReadOnlyCollection<string>? changedAttributes = null)
    {
        return TryResolveElement(addressKey, out var element) && element is not null
            ? ApplyMutation(element, changedAttributes)
            : new SvgSceneMutationResult(false, 0, 0);
    }

    public SvgSceneMutationResult ApplyMutationById(string id, IReadOnlyCollection<string>? changedAttributes = null)
    {
        return TryGetElementById(id, out var element) && element is not null
            ? ApplyMutation(element, changedAttributes)
            : new SvgSceneMutationResult(false, 0, 0);
    }

    public void ClearDirty()
    {
        Root.ClearDirty();
    }

    public IEnumerable<SvgSceneNode> HitTest(SKPoint point)
    {
        return SvgSceneHitTestService.HitTest(this, point);
    }

    public IEnumerable<SvgSceneNode> HitTest(SKRect rect)
    {
        return SvgSceneHitTestService.HitTest(this, rect);
    }

    public SvgSceneNode? HitTestTopmostNode(SKPoint point)
    {
        return SvgSceneHitTestService.HitTestTopmostNode(this, point);
    }

    public SKPicture? CreateModel()
    {
        return SvgSceneRenderer.Render(this);
    }

    public SKPicture? CreateNodeModel(SvgSceneNode node, SKRect? clip = null)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        return SvgSceneRenderer.RenderNodePicture(this, node, clip);
    }

    internal bool TryGetCompilationRoot(string compilationRootKey, out SvgSceneNode? node)
    {
        if (_compilationRootsByKey.TryGetValue(compilationRootKey, out node))
        {
            return true;
        }

        node = null;
        return false;
    }

    internal bool TryEnterMaskResolution(string resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return false;
        }

        return _resolvingMaskResourceKeys.Add(resourceKey);
    }

    internal void ExitMaskResolution(string resourceKey)
    {
        if (!string.IsNullOrWhiteSpace(resourceKey))
        {
            _resolvingMaskResourceKeys.Remove(resourceKey);
        }
    }

    internal bool TryResolveElement(string addressKey, out SvgElement? element)
    {
        element = null;
        if (SourceDocument is null || string.IsNullOrWhiteSpace(addressKey))
        {
            return false;
        }

        var indexes = addressKey.Split('/');
        var current = (SvgElement)SourceDocument;

        for (var i = 0; i < indexes.Length; i++)
        {
            if (!int.TryParse(indexes[i], out var childIndex) || childIndex < 0 || childIndex >= current.Children.Count)
            {
                return false;
            }

            current = current.Children[childIndex];
        }

        element = current;
        return true;
    }

    internal IReadOnlyList<SvgSceneResource> GetResourcesForAddress(string addressKey)
    {
        return _resourcesByAddress.TryGetValue(addressKey, out var resources)
            ? resources
            : Array.Empty<SvgSceneResource>();
    }

    internal IReadOnlyCollection<string> GetCompilationRootsForMutation(string addressKey)
    {
        var results = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(addressKey))
        {
            return results;
        }

        if (_compilationRootsByDependentAddress.TryGetValue(addressKey, out var directCompilationRoots))
        {
            foreach (var compilationRootKey in directCompilationRoots)
            {
                results.Add(compilationRootKey);
            }
        }

        if (_resourcesByAddress.TryGetValue(addressKey, out var resources))
        {
            var visitedResources = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < resources.Count; i++)
            {
                CollectResourceDependents(resources[i], results, visitedResources);
            }
        }

        return results;
    }

    internal void RebuildIndexesAndDependencies()
    {
        var addressKeyCache = new SvgElementAddressKeyCache();
        ClearIndexesAndDependencies();
        ReindexNodes();

        if (SourceDocument is not null)
        {
            RebuildResourceGraph(addressKeyCache);
            RegisterCompilationRootDependencies(addressKeyCache, includeReferencedDependencies: true);
        }
        else
        {
            RegisterNodeDependenciesFromSceneNodes(Root, new List<string>(), addressKeyCache, includeReferencedDependencies: true);
        }

        ResolveRuntimePayloads(addressKeyCache);
        SvgSceneNodeBoundsService.CacheRenderablePaintBounds(Root);
        Revision++;
    }

    internal void ClearIndexesAndDependencies()
    {
        _nodesByAddress.Clear();
        _nodesById.Clear();
        _compilationRootsByKey.Clear();
        _compilationRootKeysByElement.Clear();
        _compilationRootsByDependentAddress.Clear();
        _dependentAddressesByCompilationRoot.Clear();
        _dependentResourceKeysByCompilationRoot.Clear();
        _resourcesByKey.Clear();
        _resourcesById.Clear();
        _resourcesByAddress.Clear();
    }

    internal void RefreshMutationSubtrees(
        IReadOnlyList<IReadOnlyList<SvgSceneNode>> previousSubtrees,
        IReadOnlyList<SvgSceneNode> currentRoots,
        SvgElementAddressKeyCache addressKeyCache)
    {
        var affectedIds = new HashSet<string>(StringComparer.Ordinal);
        var affectedBoundsAncestors = new HashSet<SvgSceneNode>();

        for (var i = 0; i < previousSubtrees.Count; i++)
        {
            RemoveIndexedSubtree(previousSubtrees[i], affectedIds);
            RemoveCompilationRootDependencyContributions(previousSubtrees[i]);

            if (previousSubtrees[i].Count > 0 &&
                previousSubtrees[i][0].Parent is { } previousParent)
            {
                affectedBoundsAncestors.Add(previousParent);
            }
        }

        for (var i = 0; i < currentRoots.Count; i++)
        {
            AddIndexedSubtree(currentRoots[i], affectedIds);
        }

        RestoreMissingNodeIds(affectedIds);

        for (var i = 0; i < currentRoots.Count; i++)
        {
            RegisterCompilationRootDependenciesForSubtree(currentRoots[i], addressKeyCache);
            ResolveRuntimePayloadTree(currentRoots[i], refreshRetainedMetadata: false, addressKeyCache);
            SvgSceneNodeBoundsService.CacheRenderablePaintBounds(currentRoots[i]);

            if (currentRoots[i].Parent is { } currentParent)
            {
                affectedBoundsAncestors.Add(currentParent);
            }
        }

        foreach (var affectedBoundsAncestor in affectedBoundsAncestors)
        {
            SvgSceneNodeBoundsService.RefreshRenderablePaintBoundsUpward(affectedBoundsAncestor);
        }

        Revision++;
    }

    internal void ReindexNodes()
    {
        foreach (var node in Traverse())
        {
            AddIndexedNode(node);
        }
    }

    private void AddIndexedSubtree(SvgSceneNode root, HashSet<string>? affectedIds = null)
    {
        foreach (var node in Traverse(root))
        {
            AddIndexedNode(node, affectedIds);
        }
    }

    private void AddIndexedNode(SvgSceneNode node, HashSet<string>? affectedIds = null)
    {
        if (!string.IsNullOrWhiteSpace(node.ElementAddressKey))
        {
            if (!_nodesByAddress.TryGetValue(node.ElementAddressKey!, out var list))
            {
                list = new List<SvgSceneNode>();
                _nodesByAddress.Add(node.ElementAddressKey!, list);
            }

            list.Add(node);
        }

        if (!string.IsNullOrWhiteSpace(node.ElementId) && !_nodesById.ContainsKey(node.ElementId!))
        {
            _nodesById.Add(node.ElementId!, node);
            affectedIds?.Remove(node.ElementId!);
        }

        if (node.IsCompilationRootBoundary && !string.IsNullOrWhiteSpace(node.CompilationRootKey))
        {
            _compilationRootsByKey[node.CompilationRootKey!] = node;

            if (node.Element is not null)
            {
                if (!_compilationRootKeysByElement.TryGetValue(node.Element, out var compilationRootKeys))
                {
                    compilationRootKeys = new List<string>();
                    _compilationRootKeysByElement.Add(node.Element, compilationRootKeys);
                }

                if (!compilationRootKeys.Contains(node.CompilationRootKey!))
                {
                    compilationRootKeys.Add(node.CompilationRootKey!);
                }
            }
        }
    }

    private void RemoveIndexedSubtree(IReadOnlyList<SvgSceneNode> nodes, HashSet<string> affectedIds)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            RemoveIndexedNode(nodes[i], affectedIds);
        }
    }

    private void RemoveIndexedNode(SvgSceneNode node, HashSet<string> affectedIds)
    {
        if (!string.IsNullOrWhiteSpace(node.ElementAddressKey) &&
            _nodesByAddress.TryGetValue(node.ElementAddressKey!, out var nodes))
        {
            nodes.Remove(node);
            if (nodes.Count == 0)
            {
                _nodesByAddress.Remove(node.ElementAddressKey!);
            }
        }

        if (!string.IsNullOrWhiteSpace(node.ElementId) &&
            _nodesById.TryGetValue(node.ElementId!, out var indexedNode) &&
            ReferenceEquals(indexedNode, node))
        {
            _nodesById.Remove(node.ElementId!);
            affectedIds.Add(node.ElementId!);
        }

        if (node.IsCompilationRootBoundary && !string.IsNullOrWhiteSpace(node.CompilationRootKey))
        {
            if (_compilationRootsByKey.TryGetValue(node.CompilationRootKey!, out var indexedRoot) &&
                ReferenceEquals(indexedRoot, node))
            {
                _compilationRootsByKey.Remove(node.CompilationRootKey!);
            }

            if (node.Element is not null &&
                _compilationRootKeysByElement.TryGetValue(node.Element, out var compilationRootKeys))
            {
                compilationRootKeys.Remove(node.CompilationRootKey!);
                if (compilationRootKeys.Count == 0)
                {
                    _compilationRootKeysByElement.Remove(node.Element);
                }
            }
        }
    }

    private void RestoreMissingNodeIds(HashSet<string> affectedIds)
    {
        if (affectedIds.Count == 0)
        {
            return;
        }

        foreach (var node in Traverse())
        {
            if (string.IsNullOrWhiteSpace(node.ElementId) ||
                !affectedIds.Contains(node.ElementId!) ||
                _nodesById.ContainsKey(node.ElementId!))
            {
                continue;
            }

            _nodesById.Add(node.ElementId!, node);
            affectedIds.Remove(node.ElementId!);

            if (affectedIds.Count == 0)
            {
                return;
            }
        }
    }

    internal void RebuildResourceGraph(SvgElementAddressKeyCache addressKeyCache)
    {
        if (SourceDocument is null)
        {
            return;
        }

        var activeResources = new List<SvgSceneResource>();
        var pendingDependencyKeysByResource = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var traversalStack = new Stack<(SvgElement Element, SvgSceneResource? ResourceToExit, bool IsExit)>();
        traversalStack.Push((SourceDocument, null, false));

        while (traversalStack.Count > 0)
        {
            var frame = traversalStack.Pop();
            if (frame.IsExit)
            {
                if (frame.ResourceToExit is not null &&
                    activeResources.Count > 0 &&
                    ReferenceEquals(activeResources[activeResources.Count - 1], frame.ResourceToExit))
                {
                    activeResources.RemoveAt(activeResources.Count - 1);
                }

                continue;
            }

            var element = frame.Element;
            var elementAddressKey = addressKeyCache.GetOrCreate(element);
            SvgSceneResource? enteredResource = null;

            if (SvgSceneCompiler.TryGetResourceKind(element, out var resourceKind) &&
                !string.IsNullOrWhiteSpace(elementAddressKey))
            {
                enteredResource = new SvgSceneResource(elementAddressKey!, resourceKind, element, elementAddressKey);
                _resourcesByKey[elementAddressKey!] = enteredResource;

                if (!string.IsNullOrWhiteSpace(enteredResource.Id) && !_resourcesById.ContainsKey(enteredResource.Id!))
                {
                    _resourcesById.Add(enteredResource.Id!, enteredResource);
                }

                activeResources.Add(enteredResource);
                pendingDependencyKeysByResource[enteredResource.Key] = new HashSet<string>(StringComparer.Ordinal);
                traversalStack.Push((element, enteredResource, true));
            }

            if (!string.IsNullOrWhiteSpace(elementAddressKey))
            {
                for (var i = 0; i < activeResources.Count; i++)
                {
                    var resource = activeResources[i];
                    resource.AddSubtreeAddress(elementAddressKey!);
                    if (!_resourcesByAddress.TryGetValue(elementAddressKey!, out var resources))
                    {
                        resources = new List<SvgSceneResource>();
                        _resourcesByAddress.Add(elementAddressKey!, resources);
                    }

                    resources.Add(resource);
                }
            }

            if (activeResources.Count > 0)
            {
                SvgSceneCompiler.VisitReferencedElements(
                    element,
                    static (_, dependencyAddressKey, state) =>
                    {
                        for (var i = 0; i < state.ActiveResources.Count; i++)
                        {
                            state.PendingDependencyKeysByResource[state.ActiveResources[i].Key].Add(dependencyAddressKey);
                        }
                    },
                    addressKeyCache.GetOrCreate,
                    (ActiveResources: activeResources, PendingDependencyKeysByResource: pendingDependencyKeysByResource));
            }

            for (var i = element.Children.Count - 1; i >= 0; i--)
            {
                traversalStack.Push((element.Children[i], null, false));
            }
        }

        foreach (var resource in _resourcesByKey.Values)
        {
            if (!pendingDependencyKeysByResource.TryGetValue(resource.Key, out var dependencyAddressKeys))
            {
                continue;
            }

            foreach (var dependencyAddressKey in dependencyAddressKeys)
            {
                if (_resourcesByKey.TryGetValue(dependencyAddressKey, out var dependencyResource))
                {
                    resource.AddDependency(dependencyResource.Key);
                    dependencyResource.AddReverseDependency(resource.Key);
                }
            }
        }
    }

    private void RebuildSourceDocumentResourceGraphAndDependencies(SvgElementAddressKeyCache addressKeyCache)
    {
        if (SourceDocument is null)
        {
            return;
        }

        var activeResources = new List<SvgSceneResource>();
        var activeCompilationRootKeys = new List<string>();
        var pendingDependencyKeysByResource = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var pendingCompilationRootsByReferencedAddress = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var traversalStack = new Stack<(SvgElement Element, SvgSceneResource? ResourceToExit, int AddedCompilationRootCount, bool IsExit)>();
        traversalStack.Push((SourceDocument, null, 0, false));

        while (traversalStack.Count > 0)
        {
            var frame = traversalStack.Pop();
            if (frame.IsExit)
            {
                if (frame.ResourceToExit is not null &&
                    activeResources.Count > 0 &&
                    ReferenceEquals(activeResources[activeResources.Count - 1], frame.ResourceToExit))
                {
                    activeResources.RemoveAt(activeResources.Count - 1);
                }

                if (frame.AddedCompilationRootCount > 0)
                {
                    activeCompilationRootKeys.RemoveRange(
                        activeCompilationRootKeys.Count - frame.AddedCompilationRootCount,
                        frame.AddedCompilationRootCount);
                }

                continue;
            }

            var element = frame.Element;
            var elementAddressKey = addressKeyCache.GetOrCreate(element);
            SvgSceneResource? enteredResource = null;
            if (SvgSceneCompiler.TryGetResourceKind(element, out var resourceKind) &&
                !string.IsNullOrWhiteSpace(elementAddressKey))
            {
                enteredResource = new SvgSceneResource(elementAddressKey!, resourceKind, element, elementAddressKey);
                _resourcesByKey[elementAddressKey!] = enteredResource;

                if (!string.IsNullOrWhiteSpace(enteredResource.Id) && !_resourcesById.ContainsKey(enteredResource.Id!))
                {
                    _resourcesById.Add(enteredResource.Id!, enteredResource);
                }

                activeResources.Add(enteredResource);
                pendingDependencyKeysByResource[enteredResource.Key] = new HashSet<string>(StringComparer.Ordinal);
            }

            var addedCompilationRootCount = 0;
            if (_compilationRootKeysByElement.TryGetValue(element, out var elementCompilationRootKeys))
            {
                addedCompilationRootCount = elementCompilationRootKeys.Count;
                activeCompilationRootKeys.AddRange(elementCompilationRootKeys);
            }

            if (enteredResource is not null || addedCompilationRootCount > 0)
            {
                traversalStack.Push((element, enteredResource, addedCompilationRootCount, true));
            }

            if (!string.IsNullOrWhiteSpace(elementAddressKey))
            {
                for (var i = 0; i < activeResources.Count; i++)
                {
                    var resource = activeResources[i];
                    resource.AddSubtreeAddress(elementAddressKey!);
                    if (!_resourcesByAddress.TryGetValue(elementAddressKey!, out var resources))
                    {
                        resources = new List<SvgSceneResource>();
                        _resourcesByAddress.Add(elementAddressKey!, resources);
                    }

                    resources.Add(resource);
                }

                for (var i = 0; i < activeCompilationRootKeys.Count; i++)
                {
                    RegisterDependentAddress(elementAddressKey!, activeCompilationRootKeys[i]);
                }
            }

            if ((activeResources.Count > 0 || activeCompilationRootKeys.Count > 0) &&
                SvgSceneCompiler.MayReferenceOtherElements(element))
            {
                SvgSceneCompiler.VisitReferencedElements(
                    element,
                    static (_, dependencyAddressKey, state) =>
                    {
                        for (var i = 0; i < state.ActiveResources.Count; i++)
                        {
                            state.PendingDependencyKeysByResource[state.ActiveResources[i].Key].Add(dependencyAddressKey);
                        }

                        if (state.ActiveCompilationRootKeys.Count == 0)
                        {
                            return;
                        }

                        if (!state.PendingCompilationRootsByReferencedAddress.TryGetValue(dependencyAddressKey, out var compilationRootKeys))
                        {
                            compilationRootKeys = new HashSet<string>(StringComparer.Ordinal);
                            state.PendingCompilationRootsByReferencedAddress.Add(dependencyAddressKey, compilationRootKeys);
                        }

                        for (var i = 0; i < state.ActiveCompilationRootKeys.Count; i++)
                        {
                            compilationRootKeys.Add(state.ActiveCompilationRootKeys[i]);
                        }
                    },
                    addressKeyCache.GetOrCreate,
                    (
                        ActiveResources: activeResources,
                        PendingDependencyKeysByResource: pendingDependencyKeysByResource,
                        ActiveCompilationRootKeys: activeCompilationRootKeys,
                        PendingCompilationRootsByReferencedAddress: pendingCompilationRootsByReferencedAddress));
            }

            for (var i = element.Children.Count - 1; i >= 0; i--)
            {
                traversalStack.Push((element.Children[i], null, 0, false));
            }
        }

        foreach (var resource in _resourcesByKey.Values)
        {
            if (!pendingDependencyKeysByResource.TryGetValue(resource.Key, out var dependencyAddressKeys))
            {
                continue;
            }

            foreach (var dependencyAddressKey in dependencyAddressKeys)
            {
                if (_resourcesByKey.TryGetValue(dependencyAddressKey, out var dependencyResource))
                {
                    resource.AddDependency(dependencyResource.Key);
                    dependencyResource.AddReverseDependency(resource.Key);
                }
            }
        }

        foreach (var entry in pendingCompilationRootsByReferencedAddress)
        {
            var dependencyAddressKey = entry.Key;
            var compilationRootKeys = entry.Value;
            foreach (var compilationRootKey in compilationRootKeys)
            {
                if (_resourcesByKey.TryGetValue(dependencyAddressKey, out var resource))
                {
                    RegisterResourceDependentCompilationRoot(resource, compilationRootKey);
                }
                else
                {
                    RegisterDependentAddress(dependencyAddressKey, compilationRootKey);
                }
            }
        }
    }

    internal void RegisterNodeDependencies(SvgElementAddressKeyCache addressKeyCache)
    {
        RegisterNodeDependencies(addressKeyCache, includeReferencedDependencies: true);
    }

    internal void RegisterNodeDependencies(SvgElementAddressKeyCache addressKeyCache, bool includeReferencedDependencies)
    {
        if (SourceDocument is not null)
        {
            RegisterCompilationRootDependencies(addressKeyCache, includeReferencedDependencies);
            return;
        }

        RegisterNodeDependenciesFromSceneNodes(Root, new List<string>(), addressKeyCache, includeReferencedDependencies);
    }

    private void RegisterCompilationRootDependencies(
        SvgElementAddressKeyCache addressKeyCache,
        bool includeReferencedDependencies)
    {
        if (SourceDocument is null)
        {
            return;
        }

        if (_compilationRootKeysByElement.Count == 0)
        {
            return;
        }

        var activeCompilationRootKeys = new List<string>();
        var pendingDependentAddressesByCompilationRoot = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var pendingResourceKeysByCompilationRoot = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var traversalStack = new Stack<(SvgElement Element, int AddedCompilationRootCount, bool IsExit)>();
        traversalStack.Push((SourceDocument, 0, false));

        while (traversalStack.Count > 0)
        {
            var frame = traversalStack.Pop();
            if (frame.IsExit)
            {
                if (frame.AddedCompilationRootCount > 0)
                {
                    activeCompilationRootKeys.RemoveRange(
                        activeCompilationRootKeys.Count - frame.AddedCompilationRootCount,
                        frame.AddedCompilationRootCount);
                }

                continue;
            }

            var addedCompilationRootCount = 0;
            if (_compilationRootKeysByElement.TryGetValue(frame.Element, out var elementCompilationRootKeys))
            {
                addedCompilationRootCount = elementCompilationRootKeys.Count;
                activeCompilationRootKeys.AddRange(elementCompilationRootKeys);
                traversalStack.Push((frame.Element, addedCompilationRootCount, true));
            }

            if (activeCompilationRootKeys.Count > 0)
            {
                var subtreeAddressKey = addressKeyCache.GetOrCreate(frame.Element);
                if (!string.IsNullOrWhiteSpace(subtreeAddressKey))
                {
                    for (var i = 0; i < activeCompilationRootKeys.Count; i++)
                    {
                        AddPendingDependencyAddress(
                            pendingDependentAddressesByCompilationRoot,
                            activeCompilationRootKeys[i],
                            subtreeAddressKey!);
                    }
                }

                if (includeReferencedDependencies &&
                    SvgSceneCompiler.MayReferenceOtherElements(frame.Element))
                {
                    SvgSceneCompiler.VisitReferencedElements(
                        frame.Element,
                        static (_, dependencyAddressKey, state) =>
                        {
                            for (var i = 0; i < state.ActiveCompilationRootKeys.Count; i++)
                            {
                                var compilationRootKey = state.ActiveCompilationRootKeys[i];
                                if (state.ResourcesByKey.TryGetValue(dependencyAddressKey, out var resource))
                                {
                                    AddPendingResourceDependency(
                                        state.PendingResourceKeysByCompilationRoot,
                                        compilationRootKey,
                                        resource.Key);
                                }
                                else
                                {
                                    AddPendingDependencyAddress(
                                        state.PendingDependentAddressesByCompilationRoot,
                                        compilationRootKey,
                                        dependencyAddressKey);
                                }
                            }
                        },
                        addressKeyCache.GetOrCreate,
                        (
                            ResourcesByKey: _resourcesByKey,
                            ActiveCompilationRootKeys: activeCompilationRootKeys,
                            PendingDependentAddressesByCompilationRoot: pendingDependentAddressesByCompilationRoot,
                            PendingResourceKeysByCompilationRoot: pendingResourceKeysByCompilationRoot));
                }
            }

            for (var i = frame.Element.Children.Count - 1; i >= 0; i--)
            {
                traversalStack.Push((frame.Element.Children[i], 0, false));
            }
        }

        ApplyPendingCompilationRootDependencies(
            pendingDependentAddressesByCompilationRoot,
            pendingResourceKeysByCompilationRoot);
    }

    private void RegisterCompilationRootDependenciesForSubtree(
        SvgSceneNode root,
        SvgElementAddressKeyCache addressKeyCache)
    {
        if (root.Element is null)
        {
            return;
        }

        var activeCompilationRootKeys = new List<string>();
        var pendingDependentAddressesByCompilationRoot = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var pendingResourceKeysByCompilationRoot = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var traversalStack = new Stack<(SvgElement Element, int AddedCompilationRootCount, bool IsExit)>();
        traversalStack.Push((root.Element, 0, false));

        while (traversalStack.Count > 0)
        {
            var frame = traversalStack.Pop();
            if (frame.IsExit)
            {
                if (frame.AddedCompilationRootCount > 0)
                {
                    activeCompilationRootKeys.RemoveRange(
                        activeCompilationRootKeys.Count - frame.AddedCompilationRootCount,
                        frame.AddedCompilationRootCount);
                }

                continue;
            }

            var addedCompilationRootCount = 0;
            if (_compilationRootKeysByElement.TryGetValue(frame.Element, out var elementCompilationRootKeys))
            {
                addedCompilationRootCount = elementCompilationRootKeys.Count;
                activeCompilationRootKeys.AddRange(elementCompilationRootKeys);
                traversalStack.Push((frame.Element, addedCompilationRootCount, true));
            }

            if (activeCompilationRootKeys.Count > 0)
            {
                var subtreeAddressKey = addressKeyCache.GetOrCreate(frame.Element);
                if (!string.IsNullOrWhiteSpace(subtreeAddressKey))
                {
                    for (var i = 0; i < activeCompilationRootKeys.Count; i++)
                    {
                        AddPendingDependencyAddress(
                            pendingDependentAddressesByCompilationRoot,
                            activeCompilationRootKeys[i],
                            subtreeAddressKey!);
                    }
                }

                if (SvgSceneCompiler.MayReferenceOtherElements(frame.Element))
                {
                    SvgSceneCompiler.VisitReferencedElements(
                        frame.Element,
                        static (_, dependencyAddressKey, state) =>
                        {
                            for (var i = 0; i < state.ActiveCompilationRootKeys.Count; i++)
                            {
                                var compilationRootKey = state.ActiveCompilationRootKeys[i];
                                if (state.ResourcesByKey.TryGetValue(dependencyAddressKey, out var resource))
                                {
                                    AddPendingResourceDependency(
                                        state.PendingResourceKeysByCompilationRoot,
                                        compilationRootKey,
                                        resource.Key);
                                }
                                else
                                {
                                    AddPendingDependencyAddress(
                                        state.PendingDependentAddressesByCompilationRoot,
                                        compilationRootKey,
                                        dependencyAddressKey);
                                }
                            }
                        },
                        addressKeyCache.GetOrCreate,
                        (
                            ResourcesByKey: _resourcesByKey,
                            ActiveCompilationRootKeys: activeCompilationRootKeys,
                            PendingDependentAddressesByCompilationRoot: pendingDependentAddressesByCompilationRoot,
                            PendingResourceKeysByCompilationRoot: pendingResourceKeysByCompilationRoot));
                }
            }

            for (var i = frame.Element.Children.Count - 1; i >= 0; i--)
            {
                traversalStack.Push((frame.Element.Children[i], 0, false));
            }
        }

        ApplyPendingCompilationRootDependencies(
            pendingDependentAddressesByCompilationRoot,
            pendingResourceKeysByCompilationRoot);
    }

    private static void AddPendingDependencyAddress(
        Dictionary<string, HashSet<string>> pendingDependentAddressesByCompilationRoot,
        string compilationRootKey,
        string addressKey)
    {
        if (!pendingDependentAddressesByCompilationRoot.TryGetValue(compilationRootKey, out var addresses))
        {
            addresses = new HashSet<string>(StringComparer.Ordinal);
            pendingDependentAddressesByCompilationRoot.Add(compilationRootKey, addresses);
        }

        addresses.Add(addressKey);
    }

    private static void AddPendingResourceDependency(
        Dictionary<string, HashSet<string>> pendingResourceKeysByCompilationRoot,
        string compilationRootKey,
        string resourceKey)
    {
        if (!pendingResourceKeysByCompilationRoot.TryGetValue(compilationRootKey, out var resourceKeys))
        {
            resourceKeys = new HashSet<string>(StringComparer.Ordinal);
            pendingResourceKeysByCompilationRoot.Add(compilationRootKey, resourceKeys);
        }

        resourceKeys.Add(resourceKey);
    }

    private void ApplyPendingCompilationRootDependencies(
        Dictionary<string, HashSet<string>> pendingDependentAddressesByCompilationRoot,
        Dictionary<string, HashSet<string>> pendingResourceKeysByCompilationRoot)
    {
        foreach (var entry in pendingDependentAddressesByCompilationRoot)
        {
            var compilationRootKey = entry.Key;
            var pendingAddresses = entry.Value;

            if (!_dependentAddressesByCompilationRoot.TryGetValue(compilationRootKey, out var dependentAddresses))
            {
                dependentAddresses = new HashSet<string>(StringComparer.Ordinal);
                _dependentAddressesByCompilationRoot.Add(compilationRootKey, dependentAddresses);
            }

            foreach (var addressKey in pendingAddresses)
            {
                dependentAddresses.Add(addressKey);

                if (!_compilationRootsByDependentAddress.TryGetValue(addressKey, out var compilationRootKeys))
                {
                    compilationRootKeys = new HashSet<string>(StringComparer.Ordinal);
                    _compilationRootsByDependentAddress.Add(addressKey, compilationRootKeys);
                }

                compilationRootKeys.Add(compilationRootKey);
            }
        }

        foreach (var entry in pendingResourceKeysByCompilationRoot)
        {
            var compilationRootKey = entry.Key;
            var pendingResourceKeys = entry.Value;

            if (!_dependentResourceKeysByCompilationRoot.TryGetValue(compilationRootKey, out var resourceKeys))
            {
                resourceKeys = new HashSet<string>(StringComparer.Ordinal);
                _dependentResourceKeysByCompilationRoot.Add(compilationRootKey, resourceKeys);
            }

            foreach (var resourceKey in pendingResourceKeys)
            {
                resourceKeys.Add(resourceKey);
                if (_resourcesByKey.TryGetValue(resourceKey, out var resource))
                {
                    resource.AddDependentCompilationRoot(compilationRootKey);
                }
            }
        }
    }

    private void RegisterNodeDependenciesFromSceneNodes(
        SvgSceneNode node,
        List<string> activeCompilationRootKeys,
        SvgElementAddressKeyCache addressKeyCache,
        bool includeReferencedDependencies)
    {
        var addedCompilationRootKey = false;
        if (node.IsCompilationRootBoundary && !string.IsNullOrWhiteSpace(node.CompilationRootKey))
        {
            activeCompilationRootKeys.Add(node.CompilationRootKey!);
            addedCompilationRootKey = true;
        }

        if (activeCompilationRootKeys.Count > 0)
        {
            if (includeReferencedDependencies &&
                node.Element is not null)
            {
                SvgSceneCompiler.VisitReferencedElements(
                    node.Element,
                    static (_, dependencyAddressKey, state) =>
                    {
                        for (var i = 0; i < state.ActiveCompilationRootKeys.Count; i++)
                        {
                            var compilationRootKey = state.ActiveCompilationRootKeys[i];
                            if (state.ResourcesByKey.TryGetValue(dependencyAddressKey, out var resource))
                            {
                                state.SceneDocument.RegisterResourceDependentCompilationRoot(resource, compilationRootKey);
                            }
                            else
                            {
                                state.SceneDocument.RegisterDependentAddress(dependencyAddressKey, compilationRootKey);
                            }
                        }
                    },
                    addressKeyCache.GetOrCreate,
                    (SceneDocument: this, ResourcesByKey: _resourcesByKey, ActiveCompilationRootKeys: activeCompilationRootKeys));
            }
        }

        if (node.MaskNode is { } maskNode)
        {
            RegisterNodeDependenciesFromSceneNodes(maskNode, activeCompilationRootKeys, addressKeyCache, includeReferencedDependencies);
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            RegisterNodeDependenciesFromSceneNodes(node.Children[i], activeCompilationRootKeys, addressKeyCache, includeReferencedDependencies);
        }

        if (addedCompilationRootKey)
        {
            activeCompilationRootKeys.RemoveAt(activeCompilationRootKeys.Count - 1);
        }
    }

    internal void ResolveRuntimePayloads(SvgElementAddressKeyCache addressKeyCache)
    {
        ResolveRuntimePayloadTree(Root, refreshRetainedMetadata: false, addressKeyCache);
    }

    internal void ResolveRuntimePayloadTree(
        SvgSceneNode? root,
        bool refreshRetainedMetadata = false,
        SvgElementAddressKeyCache? addressKeyCache = null)
    {
        if (root is null)
        {
            return;
        }

        foreach (var node in TraverseStructural(root))
        {
            ResolveRuntimePayload(node, refreshRetainedMetadata, addressKeyCache);
        }
    }

    private void ResolveRuntimePayload(
        SvgSceneNode node,
        bool refreshRetainedMetadata,
        SvgElementAddressKeyCache? addressKeyCache)
    {
        if (!node.IsRenderable || node.Element is not SvgElement element)
        {
            return;
        }

        Func<SvgElement?, string?>? getElementAddressKey = null;
        if (refreshRetainedMetadata)
        {
            SvgSceneCompiler.AssignRetainedVisualState(node, element);
            if (addressKeyCache is not null)
            {
                getElementAddressKey = addressKeyCache.GetOrCreate;
            }
        }

        if (node.Kind == SvgSceneNodeKind.Marker)
        {
            node.IsVisible = true;
            node.IsDisplayNone = false;
        }

        var ignoreOpacity = IgnoreAttributes.HasFlag(DrawAttributes.Opacity);
        var opacity = ignoreOpacity
            ? 1f
            : SvgScenePaintingService.AdjustSvgOpacity(element.Opacity);
        node.OpacityValue = opacity;
        node.Opacity = ignoreOpacity
            ? null
            : SvgScenePaintingService.GetOpacityPaintFromAdjusted(opacity);
        SvgSceneCompiler.TryFoldLeafOpacityIntoDirectPathPaint(node);

        if (element is SvgVisualElement visualElement)
        {
            if (refreshRetainedMetadata)
            {
                SvgSceneCompiler.AssignRetainedResourceKeys(node, element, getElementAddressKey);
            }

            var hasOwnPaintPayload = HasOwnPaintPayload(node);
            var strokeWidth = 0f;
            var needsFilterPaintPayload = hasOwnPaintPayload &&
                                         !IgnoreAttributes.HasFlag(DrawAttributes.Filter) &&
                                         !string.IsNullOrEmpty(node.FilterResourceKey);
            var hasValidStroke = hasOwnPaintPayload &&
                                 SvgScenePaintingService.TryGetStrokeWidth(visualElement, node.GeometryBounds, out strokeWidth);
            node.ClipPath = ResolveClipPath(node);
            node.Fill = needsFilterPaintPayload && SvgScenePaintingService.IsValidFill(visualElement)
                ? SvgScenePaintingService.GetFillPaint(visualElement, node.GeometryBounds, AssetLoader, IgnoreAttributes)
                : null;
            node.Stroke = needsFilterPaintPayload && hasValidStroke
                ? SvgScenePaintingService.GetStrokePaint(visualElement, node.GeometryBounds, AssetLoader, IgnoreAttributes, strokeWidth)
                : null;
            node.StrokeWidth = hasValidStroke ? strokeWidth : 0f;
            node.SetMask(null);
            node.MaskPaint = null;
            node.MaskDstIn = null;
            node.Filter = null;
            node.FilterClip = null;
            node.SuppressSubtreeRendering = false;

            if (!IgnoreAttributes.HasFlag(DrawAttributes.Mask))
            {
                if (ResolveMaskPayload(node) is { } maskPayload)
                {
                    node.SetMask(maskPayload.MaskNode);
                    node.MaskPaint = maskPayload.MaskPaint.DeepClone();
                    node.MaskDstIn = maskPayload.MaskDstIn.DeepClone();
                }
            }

            if (!IgnoreAttributes.HasFlag(DrawAttributes.Filter))
            {
                if (visualElement.Filter is { } filter &&
                    !FilterEffectsService.IsNone(filter) &&
                    string.IsNullOrEmpty(node.FilterResourceKey))
                {
                    node.Filter = null;
                    node.FilterClip = null;
                }
                else if (ResolveFilterPayload(node) is { } filterPayload)
                {
                    if (filterPayload.IsValid)
                    {
                        node.Filter = filterPayload.FilterPaint?.DeepClone();
                        node.FilterClip = filterPayload.FilterClip;
                    }
                    else
                    {
                        node.SuppressSubtreeRendering = true;
                    }
                }
            }
        }
    }

    private static bool HasOwnPaintPayload(SvgSceneNode node)
    {
        return node.HasLocalVisuals ||
               node.HitTestPath is not null ||
               node.SupportsFillHitTest ||
               node.SupportsStrokeHitTest;
    }

    private ClipPath? ResolveClipPath(SvgSceneNode node)
    {
        if (IgnoreAttributes.HasFlag(DrawAttributes.ClipPath) || string.IsNullOrEmpty(node.ClipResourceKey))
        {
            return null;
        }

        return _resourcesByKey.TryGetValue(node.ClipResourceKey!, out var resource)
            ? resource.ResolveClipPayload(this, node)?.ClipPath
            : null;
    }

    private SvgSceneMaskPayload? ResolveMaskPayload(SvgSceneNode node)
    {
        if (string.IsNullOrEmpty(node.MaskResourceKey))
        {
            return null;
        }

        return _resourcesByKey.TryGetValue(node.MaskResourceKey!, out var resource)
            ? resource.ResolveMaskPayload(this, node)
            : null;
    }

    private SvgSceneFilterPayload? ResolveFilterPayload(SvgSceneNode node)
    {
        if (string.IsNullOrEmpty(node.FilterResourceKey))
        {
            return null;
        }

        return _resourcesByKey.TryGetValue(node.FilterResourceKey!, out var resource)
            ? resource.ResolveFilterPayload(this, node)
            : null;
    }

    private void RegisterDependentAddress(string addressKey, string compilationRootKey)
    {
        if (!_compilationRootsByDependentAddress.TryGetValue(addressKey, out var compilationRootKeys))
        {
            compilationRootKeys = new HashSet<string>(StringComparer.Ordinal);
            _compilationRootsByDependentAddress.Add(addressKey, compilationRootKeys);
        }

        compilationRootKeys.Add(compilationRootKey);
        TrackCompilationRootAddressDependency(compilationRootKey, addressKey);
    }

    private void RegisterResourceDependentCompilationRoot(SvgSceneResource resource, string compilationRootKey)
    {
        resource.AddDependentCompilationRoot(compilationRootKey);
        if (!_dependentResourceKeysByCompilationRoot.TryGetValue(compilationRootKey, out var resourceKeys))
        {
            resourceKeys = new HashSet<string>(StringComparer.Ordinal);
            _dependentResourceKeysByCompilationRoot.Add(compilationRootKey, resourceKeys);
        }

        resourceKeys.Add(resource.Key);
    }

    private void TrackCompilationRootAddressDependency(string compilationRootKey, string addressKey)
    {
        if (!_dependentAddressesByCompilationRoot.TryGetValue(compilationRootKey, out var addresses))
        {
            addresses = new HashSet<string>(StringComparer.Ordinal);
            _dependentAddressesByCompilationRoot.Add(compilationRootKey, addresses);
        }

        addresses.Add(addressKey);
    }

    private void RemoveCompilationRootDependencyContributions(IReadOnlyList<SvgSceneNode> nodes)
    {
        var removedCompilationRootKeys = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (!node.IsCompilationRootBoundary || string.IsNullOrWhiteSpace(node.CompilationRootKey))
            {
                continue;
            }

            removedCompilationRootKeys.Add(node.CompilationRootKey!);
        }

        foreach (var compilationRootKey in removedCompilationRootKeys)
        {
            RemoveCompilationRootDependencyContributions(compilationRootKey);
        }
    }

    private void RemoveCompilationRootDependencyContributions(string compilationRootKey)
    {
        if (_dependentAddressesByCompilationRoot.TryGetValue(compilationRootKey, out var addresses))
        {
            foreach (var addressKey in addresses)
            {
                if (_compilationRootsByDependentAddress.TryGetValue(addressKey, out var compilationRootKeys))
                {
                    compilationRootKeys.Remove(compilationRootKey);
                    if (compilationRootKeys.Count == 0)
                    {
                        _compilationRootsByDependentAddress.Remove(addressKey);
                    }
                }
            }

            _dependentAddressesByCompilationRoot.Remove(compilationRootKey);
        }

        if (_dependentResourceKeysByCompilationRoot.TryGetValue(compilationRootKey, out var resourceKeys))
        {
            foreach (var resourceKey in resourceKeys)
            {
                if (_resourcesByKey.TryGetValue(resourceKey, out var resource))
                {
                    resource.RemoveDependentCompilationRoot(compilationRootKey);
                }
            }

            _dependentResourceKeysByCompilationRoot.Remove(compilationRootKey);
        }
    }

    private void CollectResourceDependents(SvgSceneResource resource, HashSet<string> results, HashSet<string> visitedResources)
    {
        if (!visitedResources.Add(resource.Key))
        {
            return;
        }

        foreach (var compilationRootKey in resource.DependentCompilationRoots)
        {
            results.Add(compilationRootKey);
        }

        foreach (var reverseDependencyKey in resource.ReverseDependencyKeys)
        {
            if (_resourcesByKey.TryGetValue(reverseDependencyKey, out var reverseDependency))
            {
                CollectResourceDependents(reverseDependency, results, visitedResources);
            }
        }
    }

    private static IEnumerable<SvgSceneNode> Traverse(SvgSceneNode root)
    {
        var stack = new Stack<SvgSceneNode>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            if (current.MaskNode is { } maskNode)
            {
                stack.Push(maskNode);
            }

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }

    private static IEnumerable<SvgSceneNode> TraverseStructural(SvgSceneNode root)
    {
        var stack = new Stack<SvgSceneNode>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }

    private static IEnumerable<SvgElement> TraverseElements(SvgElement root)
    {
        var stack = new Stack<SvgElement>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }

    private sealed class SvgElementReferenceComparer : IEqualityComparer<SvgElement>
    {
        public static readonly SvgElementReferenceComparer Instance = new();

        public bool Equals(SvgElement? x, SvgElement? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(SvgElement obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
