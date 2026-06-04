using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using ShimSkiaSharp;
using Svg;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

public sealed class SvgSceneDocument
{
    private readonly Dictionary<string, NodeAddressSet> _nodesByAddress = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgSceneNode> _nodesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgSceneNode> _compilationRootsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CompilationRootKeySet> _compilationRootsByDependentAddress = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgSceneResource> _resourcesByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgSceneResource> _resourcesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<SvgSceneResource>> _resourcesByAddress = new(StringComparer.Ordinal);
    private readonly HashSet<string> _resolvingMaskResourceKeys = new(StringComparer.Ordinal);
    private readonly List<SvgSceneResource> _resourceGraphActiveResources = new();
    private readonly Dictionary<string, HashSet<string>> _resourceGraphPendingDependencyKeys = new(StringComparer.Ordinal);
    private readonly Stack<(SvgElement Element, SvgSceneResource? ResourceToExit, bool IsExit)> _resourceGraphTraversalStack = new();
    private readonly List<string> _compilationRootSubtreeActiveKeys = new();
    private readonly Stack<(SvgElement Element, int AddedCompilationRootCount, bool IsExit)> _compilationRootSubtreeTraversalStack = new();
    private readonly List<string> _nodeDependencyActiveCompilationRootKeys = new();
    private readonly Stack<SvgElement> _elementTraversalStack = new();
    private readonly Stack<SvgSceneNode> _runtimePayloadTraversalStack = new();
    private readonly ReadOnlyDictionary<string, SvgSceneNode> _readOnlyNodesById;
    private readonly ReadOnlyDictionary<string, SvgSceneResource> _readOnlyResourcesById;
    private bool _runtimePayloadTraversalStackInUse;
    private bool _mayContainResourceElements = true;
    private bool _mayContainReferenceDependencies = true;
    private bool _mayContainMarkerReferenceDeclarations = true;
    private bool _mayContainClipPathDeclarations = true;

    internal SvgSceneDocument(
        SvgDocument? sourceDocument,
        SKRect cullRect,
        SKRect compilationViewport,
        SvgSceneNode root,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        bool? mayContainMarkerReferenceDeclarations = null)
    {
        SourceDocument = sourceDocument;
        CullRect = cullRect;
        CompilationViewport = compilationViewport;
        Root = root ?? throw new ArgumentNullException(nameof(root));
        AssetLoader = assetLoader ?? throw new ArgumentNullException(nameof(assetLoader));
        IgnoreAttributes = ignoreAttributes;
        _readOnlyNodesById = new ReadOnlyDictionary<string, SvgSceneNode>(_nodesById);
        _readOnlyResourcesById = new ReadOnlyDictionary<string, SvgSceneResource>(_resourcesById);
        RebuildIndexesAndDependencies(mayContainMarkerReferenceDeclarations);
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

        var current = (SvgElement)SourceDocument;
        var index = 0;

        while (index < addressKey.Length)
        {
            var childIndex = 0;
            var hasDigit = false;
            while (index < addressKey.Length && addressKey[index] != '/')
            {
                var digit = addressKey[index] - '0';
                if ((uint)digit > 9)
                {
                    return false;
                }

                hasDigit = true;
                if (childIndex > (int.MaxValue - digit) / 10)
                {
                    return false;
                }

                childIndex = (childIndex * 10) + digit;
                index++;
            }

            if (!hasDigit || childIndex < 0 || childIndex >= current.Children.Count)
            {
                return false;
            }

            current = current.Children[childIndex];

            if (index < addressKey.Length)
            {
                index++;
                if (index >= addressKey.Length)
                {
                    return false;
                }
            }
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
        if (string.IsNullOrWhiteSpace(addressKey))
        {
            return Array.Empty<string>();
        }

        var hasDirectCompilationRoots = _compilationRootsByDependentAddress.TryGetValue(addressKey, out var directCompilationRoots);
        var hasResources = _resourcesByAddress.TryGetValue(addressKey, out var resources);
        if (hasDirectCompilationRoots && !hasResources)
        {
            return directCompilationRoots;
        }

        HashSet<string>? results = null;

        if (hasDirectCompilationRoots)
        {
            results = new HashSet<string>(directCompilationRoots, StringComparer.Ordinal);
        }

        if (hasResources)
        {
            var visitedResources = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < resources!.Count; i++)
            {
                results ??= new HashSet<string>(StringComparer.Ordinal);
                CollectResourceDependents(resources[i], results, visitedResources);
            }
        }

        return results is null ? Array.Empty<string>() : results;
    }

    internal void RebuildIndexesAndDependencies()
    {
        RebuildIndexesAndDependencies(knownMarkerReferenceDeclarations: null);
    }

    private void RebuildIndexesAndDependencies(bool? knownMarkerReferenceDeclarations)
    {
        var addressKeyCache = new SvgElementAddressKeyCache();
        var (hasResourceElements, hasReferenceDependencies, hasMarkerReferenceDeclarations, hasClipPathDeclarations, addressableElementCount) =
            AnalyzeDependencyRequirements(knownMarkerReferenceDeclarations);
        _mayContainResourceElements = hasResourceElements;
        _mayContainReferenceDependencies = hasReferenceDependencies;
        _mayContainMarkerReferenceDeclarations = hasMarkerReferenceDeclarations;
        _mayContainClipPathDeclarations = hasClipPathDeclarations;
        ClearIndexesAndDependencies();
        ReindexNodes();
        if (hasResourceElements)
        {
            RebuildResourceGraph(addressKeyCache, hasMarkerReferenceDeclarations, hasClipPathDeclarations);
        }

        var canUseNodeSubtreeRegistration = !hasReferenceDependencies &&
                                            addressableElementCount > 0 &&
                                            _nodesByAddress.Count >= addressableElementCount;
        RegisterNodeDependencies(
            addressKeyCache,
            hasReferenceDependencies,
            hasMarkerReferenceDeclarations,
            hasClipPathDeclarations,
            canUseNodeSubtreeRegistration);
        ResolveRuntimePayloads(addressKeyCache);
        Revision++;
    }

    internal void ClearIndexesAndDependencies()
    {
        _nodesByAddress.Clear();
        _nodesById.Clear();
        _compilationRootsByKey.Clear();
        _compilationRootsByDependentAddress.Clear();
        _resourcesByKey.Clear();
        _resourcesById.Clear();
        _resourcesByAddress.Clear();
    }

    internal void ReindexNodes()
    {
        var traversalStack = _runtimePayloadTraversalStackInUse
            ? new Stack<SvgSceneNode>()
            : _runtimePayloadTraversalStack;
        var wasTraversalStackInUse = _runtimePayloadTraversalStackInUse;
        _runtimePayloadTraversalStackInUse = true;
        traversalStack.Clear();
        traversalStack.Push(Root);

        try
        {
            while (traversalStack.Count > 0)
            {
                var node = traversalStack.Pop();
                if (!string.IsNullOrWhiteSpace(node.ElementAddressKey))
                {
                    RegisterNodeAddress(node.ElementAddressKey!, node);
                }

                if (!string.IsNullOrWhiteSpace(node.ElementId) && !_nodesById.ContainsKey(node.ElementId!))
                {
                    _nodesById.Add(node.ElementId!, node);
                }

                if (node.IsCompilationRootBoundary && !string.IsNullOrWhiteSpace(node.CompilationRootKey))
                {
                    _compilationRootsByKey[node.CompilationRootKey!] = node;
                }

                if (node.MaskNode is { } maskNode)
                {
                    traversalStack.Push(maskNode);
                }

                for (var i = node.Children.Count - 1; i >= 0; i--)
                {
                    traversalStack.Push(node.Children[i]);
                }
            }
        }
        finally
        {
            traversalStack.Clear();
            _runtimePayloadTraversalStackInUse = wasTraversalStackInUse;
        }
    }

    internal void RebuildResourceGraph(SvgElementAddressKeyCache addressKeyCache)
    {
        if (!_mayContainResourceElements)
        {
            return;
        }

        RebuildResourceGraph(
            addressKeyCache,
            _mayContainMarkerReferenceDeclarations,
            _mayContainClipPathDeclarations);
    }

    private void RebuildResourceGraph(
        SvgElementAddressKeyCache addressKeyCache,
        bool includeMarkerReferences,
        bool includeClipPathReferences)
    {
        if (SourceDocument is null)
        {
            return;
        }

        var activeResources = _resourceGraphActiveResources;
        var pendingDependencyKeysByResource = _resourceGraphPendingDependencyKeys;
        var traversalStack = _resourceGraphTraversalStack;
        activeResources.Clear();
        pendingDependencyKeysByResource.Clear();
        traversalStack.Clear();
        traversalStack.Push((SourceDocument, null, false));

        try
        {
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

                if (activeResources.Count > 0 &&
                    SvgSceneCompiler.MayReferenceOtherElements(element, includeMarkerReferences, includeClipPathReferences))
                {
                    SvgSceneCompiler.VisitReferencedElements(
                        element,
                        static (_, dependencyAddressKey, state) =>
                        {
                            for (var i = 0; i < state.ActiveResources.Count; i++)
                            {
                                var resource = state.ActiveResources[i];
                                if (!state.PendingDependencyKeysByResource.TryGetValue(resource.Key, out var dependencyKeys))
                                {
                                    dependencyKeys = new HashSet<string>(StringComparer.Ordinal);
                                    state.PendingDependencyKeysByResource.Add(resource.Key, dependencyKeys);
                                }

                                dependencyKeys.Add(dependencyAddressKey);
                            }
                        },
                        includeMarkerReferences,
                        includeClipPathReferences,
                        addressKeyCache.GetOrCreate,
                        (ActiveResources: activeResources, PendingDependencyKeysByResource: pendingDependencyKeysByResource));
                }

                for (var i = element.Children.Count - 1; i >= 0; i--)
                {
                    traversalStack.Push((element.Children[i], null, false));
                }
            }
        }
        finally
        {
            activeResources.Clear();
            traversalStack.Clear();
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

        pendingDependencyKeysByResource.Clear();
    }

    internal void RegisterNodeDependencies(SvgElementAddressKeyCache addressKeyCache)
    {
        RegisterNodeDependencies(addressKeyCache, _mayContainReferenceDependencies);
    }

    internal void RegisterNodeDependencies(SvgElementAddressKeyCache addressKeyCache, bool includeReferencedDependencies)
    {
        RegisterNodeDependencies(
            addressKeyCache,
            includeReferencedDependencies,
            _mayContainMarkerReferenceDeclarations,
            _mayContainClipPathDeclarations);
    }

    private void RegisterNodeDependencies(
        SvgElementAddressKeyCache addressKeyCache,
        bool includeReferencedDependencies,
        bool includeMarkerReferences,
        bool includeClipPathReferences,
        bool useNodeSubtreeRegistration = false)
    {
        if (_compilationRootsByKey.Count == 0)
        {
            return;
        }

        if (useNodeSubtreeRegistration)
        {
            RegisterCompilationRootSubtreeAddressesFromNodes();
        }
        else
        {
            RegisterCompilationRootSubtreeAddresses(addressKeyCache);
        }

        if (!includeReferencedDependencies)
        {
            return;
        }

        var activeCompilationRootKeys = _nodeDependencyActiveCompilationRootKeys;
        activeCompilationRootKeys.Clear();
        try
        {
            RegisterNodeDependencies(Root, activeCompilationRootKeys, addressKeyCache, includeMarkerReferences, includeClipPathReferences);
        }
        finally
        {
            activeCompilationRootKeys.Clear();
        }
    }

    private (bool HasResourceElements, bool HasReferenceDependencies, bool HasMarkerReferenceDeclarations, bool HasClipPathDeclarations, int AddressableElementCount) AnalyzeDependencyRequirements(bool? knownMarkerReferenceDeclarations)
    {
        if (SourceDocument is null)
        {
            return (false, false, false, false, 0);
        }

        var hasResourceElements = false;
        var hasReferenceDependencies = false;
        var hasMarkerReferenceDeclarations = knownMarkerReferenceDeclarations.GetValueOrDefault();
        var hasClipPathDeclarations = false;
        var addressableElementCount = 0;
        var traversalStack = _elementTraversalStack;
        traversalStack.Clear();
        traversalStack.Push(SourceDocument);

        while (traversalStack.Count > 0)
        {
            var element = traversalStack.Pop();
            if (!ReferenceEquals(element, SourceDocument))
            {
                addressableElementCount++;
            }

            if (!hasResourceElements &&
                SvgSceneCompiler.TryGetResourceKind(element, out _))
            {
                hasResourceElements = true;
            }

            if (!knownMarkerReferenceDeclarations.HasValue &&
                !hasMarkerReferenceDeclarations &&
                SvgSceneCompiler.HasOwnMarkerReferenceDeclarationCandidate(element))
            {
                hasMarkerReferenceDeclarations = true;
            }

            if (!hasClipPathDeclarations &&
                SvgSceneCompiler.HasOwnClipPathDeclarationCandidate(element))
            {
                hasClipPathDeclarations = true;
            }

            if (!hasReferenceDependencies &&
                SvgSceneCompiler.MayReferenceOtherElements(element, hasMarkerReferenceDeclarations, hasClipPathDeclarations))
            {
                hasReferenceDependencies = true;
            }

            if (hasResourceElements &&
                hasReferenceDependencies &&
                (knownMarkerReferenceDeclarations.HasValue || hasMarkerReferenceDeclarations) &&
                hasClipPathDeclarations)
            {
                break;
            }

            for (var i = element.Children.Count - 1; i >= 0; i--)
            {
                traversalStack.Push(element.Children[i]);
            }
        }

        traversalStack.Clear();

        return (hasResourceElements, hasReferenceDependencies, hasMarkerReferenceDeclarations, hasClipPathDeclarations, addressableElementCount);
    }

    private void RegisterCompilationRootSubtreeAddressesFromNodes()
    {
        var activeCompilationRootKeys = _compilationRootSubtreeActiveKeys;
        activeCompilationRootKeys.Clear();
        try
        {
            RegisterCompilationRootSubtreeAddressesFromNode(Root, activeCompilationRootKeys);
        }
        finally
        {
            activeCompilationRootKeys.Clear();
        }
    }

    private void RegisterCompilationRootSubtreeAddressesFromNode(
        SvgSceneNode node,
        List<string> activeCompilationRootKeys)
    {
        var addedCompilationRootKey = false;
        if (node.IsCompilationRootBoundary && !string.IsNullOrWhiteSpace(node.CompilationRootKey))
        {
            activeCompilationRootKeys.Add(node.CompilationRootKey!);
            addedCompilationRootKey = true;
        }

        if (activeCompilationRootKeys.Count > 0 && !string.IsNullOrWhiteSpace(node.ElementAddressKey))
        {
            for (var i = 0; i < activeCompilationRootKeys.Count; i++)
            {
                RegisterDependentAddress(node.ElementAddressKey!, activeCompilationRootKeys[i]);
            }
        }

        if (node.MaskNode is { } maskNode)
        {
            RegisterCompilationRootSubtreeAddressesFromNode(maskNode, activeCompilationRootKeys);
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            RegisterCompilationRootSubtreeAddressesFromNode(node.Children[i], activeCompilationRootKeys);
        }

        if (addedCompilationRootKey)
        {
            activeCompilationRootKeys.RemoveAt(activeCompilationRootKeys.Count - 1);
        }
    }

    private void RegisterCompilationRootSubtreeAddresses(SvgElementAddressKeyCache addressKeyCache)
    {
        if (SourceDocument is null)
        {
            return;
        }

        var compilationRootsByElement = BuildCompilationRootLookup(addressKeyCache);
        if (compilationRootsByElement is null || compilationRootsByElement.Count == 0)
        {
            return;
        }

        var activeCompilationRootKeys = _compilationRootSubtreeActiveKeys;
        var traversalStack = _compilationRootSubtreeTraversalStack;
        activeCompilationRootKeys.Clear();
        traversalStack.Clear();
        traversalStack.Push((SourceDocument, 0, false));

        try
        {
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
                if (compilationRootsByElement.TryGetValue(frame.Element, out var elementCompilationRootKeys))
                {
                    addedCompilationRootCount = elementCompilationRootKeys.Count;
                    for (var i = 0; i < elementCompilationRootKeys.Count; i++)
                    {
                        activeCompilationRootKeys.Add(elementCompilationRootKeys[i]);
                    }

                    traversalStack.Push((frame.Element, addedCompilationRootCount, true));
                }

                var subtreeAddressKey = addressKeyCache.GetOrCreate(frame.Element);
                if (!string.IsNullOrWhiteSpace(subtreeAddressKey))
                {
                    for (var i = 0; i < activeCompilationRootKeys.Count; i++)
                    {
                        RegisterDependentAddress(subtreeAddressKey!, activeCompilationRootKeys[i]);
                    }
                }

                for (var i = frame.Element.Children.Count - 1; i >= 0; i--)
                {
                    traversalStack.Push((frame.Element.Children[i], 0, false));
                }
            }
        }
        finally
        {
            activeCompilationRootKeys.Clear();
            traversalStack.Clear();
        }
    }

    private Dictionary<SvgElement, CompilationRootKeySet>? BuildCompilationRootLookup(SvgElementAddressKeyCache addressKeyCache)
    {
        Dictionary<SvgElement, CompilationRootKeySet>? compilationRootsByElement = null;
        var traversalStack = _runtimePayloadTraversalStackInUse
            ? new Stack<SvgSceneNode>()
            : _runtimePayloadTraversalStack;
        var wasTraversalStackInUse = _runtimePayloadTraversalStackInUse;
        _runtimePayloadTraversalStackInUse = true;
        traversalStack.Clear();
        traversalStack.Push(Root);

        try
        {
            while (traversalStack.Count > 0)
            {
                var node = traversalStack.Pop();
                if (node.IsCompilationRootBoundary &&
                    !string.IsNullOrWhiteSpace(node.CompilationRootKey) &&
                    node.Element is not null)
                {
                    if (node.Element.Children.Count == 0)
                    {
                        var elementAddressKey = node.ElementAddressKey ?? addressKeyCache.GetOrCreate(node.Element);
                        if (!string.IsNullOrWhiteSpace(elementAddressKey))
                        {
                            RegisterDependentAddress(elementAddressKey!, node.CompilationRootKey!);
                        }
                    }
                    else
                    {
                        var compilationRootKeys = default(CompilationRootKeySet);
                        var hasExistingKeys = compilationRootsByElement is not null &&
                                              compilationRootsByElement.TryGetValue(node.Element, out compilationRootKeys);
                        if (compilationRootKeys.Add(node.CompilationRootKey!))
                        {
                            compilationRootsByElement ??= new Dictionary<SvgElement, CompilationRootKeySet>(SvgElementReferenceComparer.Instance);
                            if (hasExistingKeys)
                            {
                                compilationRootsByElement[node.Element] = compilationRootKeys;
                            }
                            else
                            {
                                compilationRootsByElement.Add(node.Element, compilationRootKeys);
                            }
                        }
                    }
                }

                if (node.MaskNode is { } maskNode)
                {
                    traversalStack.Push(maskNode);
                }

                for (var i = node.Children.Count - 1; i >= 0; i--)
                {
                    traversalStack.Push(node.Children[i]);
                }
            }
        }
        finally
        {
            traversalStack.Clear();
            _runtimePayloadTraversalStackInUse = wasTraversalStackInUse;
        }

        return compilationRootsByElement;
    }

    private void RegisterNodeDependencies(
        SvgSceneNode node,
        List<string> activeCompilationRootKeys,
        SvgElementAddressKeyCache addressKeyCache,
        bool includeMarkerReferences,
        bool includeClipPathReferences)
    {
        var addedCompilationRootKey = false;
        if (node.IsCompilationRootBoundary && !string.IsNullOrWhiteSpace(node.CompilationRootKey))
        {
            activeCompilationRootKeys.Add(node.CompilationRootKey!);
            addedCompilationRootKey = true;
        }

        if (activeCompilationRootKeys.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(node.ElementAddressKey))
            {
                for (var i = 0; i < activeCompilationRootKeys.Count; i++)
                {
                    RegisterDependentAddress(node.ElementAddressKey!, activeCompilationRootKeys[i]);
                }
            }

            if (node.Element is not null &&
                SvgSceneCompiler.MayReferenceOtherElements(node.Element, includeMarkerReferences, includeClipPathReferences))
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
                                resource.AddDependentCompilationRoot(compilationRootKey);
                            }
                            else
                            {
                                state.SceneDocument.RegisterDependentAddress(dependencyAddressKey, compilationRootKey);
                            }
                        }
                    },
                    includeMarkerReferences,
                    includeClipPathReferences,
                    addressKeyCache.GetOrCreate,
                    (SceneDocument: this, ResourcesByKey: _resourcesByKey, ActiveCompilationRootKeys: activeCompilationRootKeys));
            }
        }

        if (node.MaskNode is { } maskNode)
        {
            RegisterNodeDependencies(maskNode, activeCompilationRootKeys, addressKeyCache, includeMarkerReferences, includeClipPathReferences);
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            RegisterNodeDependencies(node.Children[i], activeCompilationRootKeys, addressKeyCache, includeMarkerReferences, includeClipPathReferences);
        }

        if (addedCompilationRootKey)
        {
            activeCompilationRootKeys.RemoveAt(activeCompilationRootKeys.Count - 1);
        }
    }

    internal void ResolveRuntimePayloads(SvgElementAddressKeyCache addressKeyCache)
    {
        var gradientPaintCache = new SvgScenePaintingService.GradientPaintCache();
        var opacityPaintCache = new Dictionary<float, SKPaint>();
        var solidFillPaintCache = new Dictionary<SvgScenePaintingService.SolidFillPaintCacheKey, SKPaint>();
        ResolveRuntimePayloadTree(Root, refreshRetainedMetadata: false, addressKeyCache, gradientPaintCache, opacityPaintCache, solidFillPaintCache);
    }

    internal void ResolveRuntimePayloadTree(
        SvgSceneNode? root,
        bool refreshRetainedMetadata = false,
        SvgElementAddressKeyCache? addressKeyCache = null,
        SvgScenePaintingService.GradientPaintCache? gradientPaintCache = null,
        Dictionary<float, SKPaint>? opacityPaintCache = null,
        Dictionary<SvgScenePaintingService.SolidFillPaintCacheKey, SKPaint>? solidFillPaintCache = null)
    {
        if (root is null)
        {
            return;
        }

        gradientPaintCache ??= new SvgScenePaintingService.GradientPaintCache();
        opacityPaintCache ??= new Dictionary<float, SKPaint>();
        solidFillPaintCache ??= new Dictionary<SvgScenePaintingService.SolidFillPaintCacheKey, SKPaint>();

        var traversalStack = _runtimePayloadTraversalStackInUse
            ? new Stack<SvgSceneNode>()
            : _runtimePayloadTraversalStack;
        var wasTraversalStackInUse = _runtimePayloadTraversalStackInUse;
        _runtimePayloadTraversalStackInUse = true;
        traversalStack.Clear();
        traversalStack.Push(root);

        try
        {
            while (traversalStack.Count > 0)
            {
                var node = traversalStack.Pop();
                ResolveRuntimePayload(node, refreshRetainedMetadata, addressKeyCache, gradientPaintCache, opacityPaintCache, solidFillPaintCache);

                for (var i = node.Children.Count - 1; i >= 0; i--)
                {
                    traversalStack.Push(node.Children[i]);
                }
            }
        }
        finally
        {
            traversalStack.Clear();
            _runtimePayloadTraversalStackInUse = wasTraversalStackInUse;
        }
    }

    private void ResolveRuntimePayload(
        SvgSceneNode node,
        bool refreshRetainedMetadata,
        SvgElementAddressKeyCache? addressKeyCache,
        SvgScenePaintingService.GradientPaintCache gradientPaintCache,
        Dictionary<float, SKPaint> opacityPaintCache,
        Dictionary<SvgScenePaintingService.SolidFillPaintCacheKey, SKPaint> solidFillPaintCache)
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

        if (refreshRetainedMetadata)
        {
            SvgSceneCompiler.AssignRetainedResourceKeys(node, element, getElementAddressKey);
        }

        var opacityValue = IgnoreAttributes.HasFlag(DrawAttributes.Opacity)
            ? 1f
            : SvgScenePaintingService.AdjustSvgOpacity(element.Opacity);
        node.OpacityValue = opacityValue;
        node.Opacity = IgnoreAttributes.HasFlag(DrawAttributes.Opacity)
            ? null
            : GetCachedOpacityPaint(opacityValue, opacityPaintCache);

        node.SetMask(null);
        node.MaskPaint = null;
        node.MaskDstIn = null;

        if (!IgnoreAttributes.HasFlag(DrawAttributes.Mask))
        {
            if (ResolveMaskPayload(node) is { } maskPayload)
            {
                node.SetMask(maskPayload.MaskNode);
                node.MaskPaint = maskPayload.MaskPaint;
                node.MaskDstIn = maskPayload.MaskDstIn;
            }
        }

        if (IgnoreAttributes.HasFlag(DrawAttributes.ClipPath))
        {
            node.ClipPath = null;
        }
        else
        {
            var clipPath = ResolveClipPath(node);
            node.ClipPath = clipPath is not null || !_mayContainClipPathDeclarations
                ? clipPath
                : SvgSceneClipCompiler.CompileBasicShapeClipPath(element, node.GeometryBounds, CompilationViewport);
        }

        if (element is SvgVisualElement visualElement)
        {
            var hasOwnPaintPayload = HasOwnPaintPayload(node);
            var resolveFillPayload = hasOwnPaintPayload && RequiresResolvedFillPayload(node);
            var hasFillPayload = node.Kind == SvgSceneNodeKind.Text
                ? node.SupportsFillHitTest
                : SvgScenePaintingService.IsValidFill(visualElement);
            var hasStrokePayload = node.Kind == SvgSceneNodeKind.Text
                ? node.SupportsStrokeHitTest
                : SvgScenePaintingService.IsValidStroke(visualElement, node.GeometryBounds);
            node.Fill = resolveFillPayload && hasFillPayload
                ? GetCachedFillPaint(visualElement, node.GeometryBounds, AssetLoader, IgnoreAttributes, gradientPaintCache, solidFillPaintCache)
                : null;
            node.Stroke = hasOwnPaintPayload && hasStrokePayload
                ? SvgScenePaintingService.GetStrokePaint(visualElement, node.GeometryBounds, AssetLoader, IgnoreAttributes, geometryPath: node.HitTestPath, gradientPaintCache: gradientPaintCache)
                : null;
            node.StrokeWidth = hasOwnPaintPayload ? node.Stroke?.StrokeWidth ?? 0f : 0f;
            node.IsStrokeNonScaling = hasOwnPaintPayload && visualElement.VectorEffect == SvgVectorEffect.NonScalingStroke;
            node.Filter = null;
            node.FilterClip = null;
            node.FilterUsesGlobalLayer = false;
            node.FilterGlobalClip = null;
            node.SuppressSubtreeRendering = false;

            if (!IgnoreAttributes.HasFlag(DrawAttributes.Filter))
            {
                if (ResolveFilterPayload(node) is { } filterPayload)
                {
                    if (filterPayload.IsValid)
                    {
                        node.Filter = filterPayload.FilterPaint;
                        node.FilterClip = filterPayload.FilterClip;
                        node.FilterUsesGlobalLayer = filterPayload.UsesGlobalLayer;
                        node.FilterGlobalClip = filterPayload.GlobalClip;
                    }
                    else
                    {
                        node.SuppressSubtreeRendering = true;
                    }
                }
            }
        }
    }

    private static SKPaint? GetCachedOpacityPaint(float opacityValue, Dictionary<float, SKPaint> opacityPaintCache)
    {
        if (opacityValue >= 1f)
        {
            return null;
        }

        if (!opacityPaintCache.TryGetValue(opacityValue, out var paint))
        {
            paint = SvgScenePaintingService.GetOpacityPaint(opacityValue)!;
            opacityPaintCache[opacityValue] = paint;
        }

        return paint;
    }

    private static SKPaint? GetCachedFillPaint(
        SvgVisualElement visualElement,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes,
        SvgScenePaintingService.GradientPaintCache gradientPaintCache,
        Dictionary<SvgScenePaintingService.SolidFillPaintCacheKey, SKPaint> solidFillPaintCache)
    {
        if (SvgScenePaintingService.TryCreateSolidFillPaintCacheKey(visualElement, ignoreAttributes, out var key))
        {
            if (!solidFillPaintCache.TryGetValue(key, out var paint))
            {
                paint = SvgScenePaintingService.CreateSolidFillPaint(key);
                solidFillPaintCache[key] = paint;
            }

            return paint;
        }

        return SvgScenePaintingService.GetFillPaint(
            visualElement,
            geometryBounds,
            assetLoader,
            ignoreAttributes,
            gradientPaintCache: gradientPaintCache);
    }

    private static bool HasOwnPaintPayload(SvgSceneNode node)
    {
        return node.HasLocalVisuals ||
               node.HitTestPath is not null ||
               node.SupportsFillHitTest ||
               node.SupportsStrokeHitTest;
    }

    private static bool RequiresResolvedFillPayload(SvgSceneNode node)
    {
        return node.Kind != SvgSceneNodeKind.Text ||
               node.HitTestPath is not null;
    }

    private ClipPath? ResolveClipPath(SvgSceneNode node)
    {
        if (IgnoreAttributes.HasFlag(DrawAttributes.ClipPath) || string.IsNullOrWhiteSpace(node.ClipResourceKey))
        {
            return null;
        }

        return _resourcesByKey.TryGetValue(node.ClipResourceKey!, out var resource)
            ? resource.ResolveClipPayload(this, node)?.ClipPath
            : null;
    }

    private SvgSceneMaskPayload? ResolveMaskPayload(SvgSceneNode node)
    {
        if (string.IsNullOrWhiteSpace(node.MaskResourceKey))
        {
            return null;
        }

        return _resourcesByKey.TryGetValue(node.MaskResourceKey!, out var resource)
            ? resource.ResolveMaskPayload(this, node)
            : null;
    }

    private SvgSceneFilterPayload? ResolveFilterPayload(SvgSceneNode node)
    {
        if (string.IsNullOrWhiteSpace(node.FilterResourceKey))
        {
            return ResolveUntrackedFilterPayload(node);
        }

        return _resourcesByKey.TryGetValue(node.FilterResourceKey!, out var resource)
            ? resource.ResolveFilterPayload(this, node)
            : ResolveUntrackedFilterPayload(node);
    }

    private SvgSceneFilterPayload? ResolveUntrackedFilterPayload(SvgSceneNode node)
    {
        if (node.Element is not SvgVisualElement visualElement ||
            !SvgSceneFilterContext.HasFilterDeclaration(visualElement))
        {
            return null;
        }

        var filterContext = new SvgSceneFilterContext(
            this,
            visualElement,
            node.GeometryBounds,
            CompilationViewport,
            new SvgSceneFilterSource(this, node),
            AssetLoader,
            references: null,
            targetTransform: node.TotalTransform,
            initialReferenceUri: node.Element.OwnerDocument?.BaseUri);

        if (filterContext.FilterPaint is { } filterPaint)
        {
            return new SvgSceneFilterPayload(
                filterPaint,
                filterContext.FilterClip,
                isValid: true,
                filterContext.UsesGlobalLayer,
                filterContext.GlobalClip);
        }

        return filterContext.IsValid
            ? null
            : SvgSceneFilterPayload.Invalid(filterContext.FilterClip);
    }

    private void RegisterDependentAddress(string addressKey, string compilationRootKey)
    {
        var hasExistingKeys = _compilationRootsByDependentAddress.TryGetValue(addressKey, out var compilationRootKeys);
        if (compilationRootKeys.Add(compilationRootKey))
        {
            if (hasExistingKeys)
            {
                _compilationRootsByDependentAddress[addressKey] = compilationRootKeys;
            }
            else
            {
                _compilationRootsByDependentAddress.Add(addressKey, compilationRootKeys);
            }
        }
    }

    private void RegisterNodeAddress(string addressKey, SvgSceneNode node)
    {
        var hasExistingNodes = _nodesByAddress.TryGetValue(addressKey, out var nodes);
        nodes.Add(node);
        if (hasExistingNodes)
        {
            _nodesByAddress[addressKey] = nodes;
        }
        else
        {
            _nodesByAddress.Add(addressKey, nodes);
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

    private struct NodeAddressSet : IReadOnlyList<SvgSceneNode>
    {
        private SvgSceneNode? _first;
        private SvgSceneNode? _second;
        private SvgSceneNode[]? _additional;
        private int _additionalCount;

        public readonly int Count =>
            (_first is null ? 0 : 1) +
            (_second is null ? 0 : 1) +
            _additionalCount;

        public readonly SvgSceneNode this[int index]
        {
            get
            {
                if (index == 0 && _first is not null)
                {
                    return _first;
                }

                if (index == 1 && _second is not null)
                {
                    return _second;
                }

                var additionalIndex = index - 2;
                if (_additional is not null &&
                    additionalIndex >= 0 &&
                    additionalIndex < _additionalCount)
                {
                    return _additional[additionalIndex];
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public void Add(SvgSceneNode node)
        {
            if (_first is null)
            {
                _first = node;
                return;
            }

            if (_second is null)
            {
                _second = node;
                return;
            }

            if (_additional is null)
            {
                _additional = new SvgSceneNode[4];
            }
            else if (_additionalCount >= _additional.Length)
            {
                Array.Resize(ref _additional, _additional.Length * 2);
            }

            _additional[_additionalCount++] = node;
        }

        public readonly IEnumerator<SvgSceneNode> GetEnumerator()
        {
            if (_first is not null)
            {
                yield return _first;
            }

            if (_second is not null)
            {
                yield return _second;
            }

            if (_additional is not null)
            {
                for (var i = 0; i < _additionalCount; i++)
                {
                    yield return _additional[i];
                }
            }
        }

        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private struct CompilationRootKeySet : IReadOnlyList<string>
    {
        private string? _first;
        private string? _second;
        private string[]? _additional;
        private int _additionalCount;

        public readonly int Count =>
            (_first is null ? 0 : 1) +
            (_second is null ? 0 : 1) +
            _additionalCount;

        public readonly string this[int index]
        {
            get
            {
                if (index == 0 && _first is not null)
                {
                    return _first;
                }

                if (index == 1 && _second is not null)
                {
                    return _second;
                }

                var additionalIndex = index - 2;
                if (_additional is not null &&
                    additionalIndex >= 0 &&
                    additionalIndex < _additionalCount)
                {
                    return _additional[additionalIndex];
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public bool Add(string compilationRootKey)
        {
            if (_first is null)
            {
                _first = compilationRootKey;
                return true;
            }

            if (string.Equals(_first, compilationRootKey, StringComparison.Ordinal))
            {
                return false;
            }

            if (_second is null)
            {
                _second = compilationRootKey;
                return true;
            }

            if (string.Equals(_second, compilationRootKey, StringComparison.Ordinal))
            {
                return false;
            }

            if (_additional is not null)
            {
                for (var i = 0; i < _additionalCount; i++)
                {
                    if (string.Equals(_additional[i], compilationRootKey, StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            if (_additional is null)
            {
                _additional = new string[4];
            }
            else if (_additionalCount >= _additional.Length)
            {
                Array.Resize(ref _additional, _additional.Length * 2);
            }

            _additional[_additionalCount++] = compilationRootKey;
            return true;
        }

        public readonly IEnumerator<string> GetEnumerator()
        {
            if (_first is not null)
            {
                yield return _first;
            }

            if (_second is not null)
            {
                yield return _second;
            }

            if (_additional is not null)
            {
                for (var i = 0; i < _additionalCount; i++)
                {
                    yield return _additional[i];
                }
            }
        }

        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
