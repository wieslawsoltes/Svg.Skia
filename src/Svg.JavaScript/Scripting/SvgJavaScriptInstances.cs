using System;
using System.Collections.Generic;
using System.Linq;
using Svg;
using Svg.Model.Services;

namespace Svg.JavaScript;

public sealed class SvgJavaScriptElementInstance
{
    private readonly SvgJavaScriptRuntime _runtime;
    private readonly SvgJavaScriptDocument _document;
    private readonly ISvgNode _correspondingNode;
    private readonly SvgElement _ownerElement;
    private readonly SvgUse _correspondingUseElement;
    private readonly SvgJavaScriptElementInstance? _parent;
    private readonly List<SvgJavaScriptElementInstance> _children = new();

    internal SvgJavaScriptElementInstance(
        SvgJavaScriptRuntime runtime,
        SvgJavaScriptDocument document,
        ISvgNode correspondingNode,
        SvgElement ownerElement,
        SvgUse correspondingUseElement,
        SvgJavaScriptElementInstance? parent)
    {
        _runtime = runtime;
        _document = document;
        _correspondingNode = correspondingNode;
        _ownerElement = ownerElement;
        _correspondingUseElement = correspondingUseElement;
        _parent = parent;
    }

    public int nodeType => 1;

    public string id => CorrespondingElementRaw?.ID ?? string.Empty;

    public object correspondingElement => _document.WrapNode(_correspondingNode, _ownerElement) ?? _document.createTextNode(string.Empty);

    public SvgJavaScriptElement correspondingUseElement => _document.GetOrCreateElement(_correspondingUseElement);

    public SvgJavaScriptElementInstance? parentNode => _parent;

    public object? firstChild => _children.Count > 0 ? _children[0] : null;

    public object? lastChild => _children.Count > 0 ? _children[_children.Count - 1] : null;

    public object? nextSibling
    {
        get
        {
            if (_parent is null)
            {
                return null;
            }

            var siblings = _parent._children;
            var index = siblings.IndexOf(this);
            return index >= 0 && index + 1 < siblings.Count ? siblings[index + 1] : null;
        }
    }

    public object? previousSibling
    {
        get
        {
            if (_parent is null)
            {
                return null;
            }

            var siblings = _parent._children;
            var index = siblings.IndexOf(this);
            return index > 0 ? siblings[index - 1] : null;
        }
    }

    public SvgJavaScriptNodeList childNodes => new(_children.Cast<object>());

    public bool dispatchEvent(SvgJavaScriptEvent evt)
    {
        return _runtime.DispatchEvent(this, evt);
    }

    internal SvgElement? CorrespondingElementRaw => _correspondingNode as SvgElement;

    internal SvgElement CorrespondingEventElementRaw => CorrespondingElementRaw ?? _ownerElement;

    internal void AddChild(SvgJavaScriptElementInstance child)
    {
        _children.Add(child);
    }

    internal SvgJavaScriptElementInstance? FindFirst(SvgElement correspondingElement)
    {
        if (ReferenceEquals(CorrespondingElementRaw, correspondingElement))
        {
            return this;
        }

        for (var i = 0; i < _children.Count; i++)
        {
            var match = _children[i].FindFirst(correspondingElement);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    internal static SvgJavaScriptElementInstance? CreateTree(
        SvgJavaScriptRuntime runtime,
        SvgJavaScriptDocument document,
        SvgUse use)
    {
        var referencedElement = ResolveReference(use);
        return referencedElement is null
            ? null
            : CreateNode(runtime, document, referencedElement, referencedElement, use, null);
    }

    private static SvgJavaScriptElementInstance CreateNode(
        SvgJavaScriptRuntime runtime,
        SvgJavaScriptDocument document,
        ISvgNode node,
        SvgElement ownerElement,
        SvgUse rootUse,
        SvgJavaScriptElementInstance? parent)
    {
        var instance = new SvgJavaScriptElementInstance(runtime, document, node, ownerElement, rootUse, parent);

        if (node is SvgUse nestedUse)
        {
            var nestedReference = ResolveReference(nestedUse);
            if (nestedReference is not null)
            {
                instance.AddChild(CreateNode(runtime, document, nestedReference, nestedReference, rootUse, instance));
            }

            return instance;
        }

        if (node is not SvgElement element)
        {
            return instance;
        }

        foreach (var child in GetChildNodes(element))
        {
            instance.AddChild(CreateNode(runtime, document, child, element, rootUse, instance));
        }

        return instance;
    }

    private static IEnumerable<ISvgNode> GetChildNodes(SvgElement element)
    {
        if (element.Nodes.Count > 0)
        {
            return element.Nodes.Cast<ISvgNode>();
        }

        if (element.Children.Count <= 1)
        {
            return element.Children.Cast<ISvgNode>();
        }

        var nodes = new List<ISvgNode>(element.Children.Count * 2);
        nodes.Add(new SvgContentNode { Content = "\n" });
        for (var i = 0; i < element.Children.Count; i++)
        {
            nodes.Add(element.Children[i]);
            if (i + 1 < element.Children.Count)
            {
                nodes.Add(new SvgContentNode { Content = "\n" });
            }
        }

        return nodes;
    }

    private static SvgElement? ResolveReference(SvgUse use)
    {
        var hasRecursiveReference = SvgService.HasRecursiveReference(use, static element => element.ReferencedElement, new HashSet<Uri>());
        return hasRecursiveReference
            ? null
            : SvgService.GetReference<SvgElement>(use, use.ReferencedElement);
    }
}
