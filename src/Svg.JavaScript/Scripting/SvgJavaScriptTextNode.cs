using System.Linq;
using Svg;

namespace Svg.JavaScript;

public sealed class SvgJavaScriptTextNode
{
    private readonly SvgJavaScriptDocument _document;
    private SvgElement? _parent;

    internal SvgJavaScriptTextNode(SvgJavaScriptDocument document, SvgContentNode node, SvgElement? parent)
    {
        _document = document;
        Node = node;
        _parent = parent;
    }

    internal SvgContentNode Node { get; }

    public string nodeName => "#text";

    public int nodeType => 3;

    public string textContent
    {
        get => Node.Content ?? string.Empty;
        set
        {
            Node.Content = value ?? string.Empty;
            SyncParentContent();
            _document.MarkMutation();
        }
    }

    public string data
    {
        get => textContent;
        set => textContent = value;
    }

    public SvgJavaScriptElement? parentNode => _parent is null ? null : _document.GetOrCreateElement(_parent);

    public object? nextSibling => GetSibling(1);

    public object? previousSibling => GetSibling(-1);

    internal void SetParent(SvgElement? parent)
    {
        _parent = parent;
    }

    internal void DetachFromParent()
    {
        if (_parent is null)
        {
            return;
        }

        var oldParent = _parent;
        oldParent.Nodes.Remove(Node);
        SyncParentContent(oldParent);
        _parent = null;
    }

    private object? GetSibling(int offset)
    {
        if (_parent is null)
        {
            return null;
        }

        var nodes = _parent.Nodes.Count > 0 ? _parent.Nodes.ToArray() : _parent.Children.Cast<ISvgNode>().ToArray();
        var index = System.Array.IndexOf(nodes, Node);
        var siblingIndex = index + offset;
        return index < 0 || siblingIndex < 0 || siblingIndex >= nodes.Length
            ? null
            : _document.WrapNode(nodes[siblingIndex], _parent);
    }

    private void SyncParentContent()
    {
        if (_parent is null)
        {
            return;
        }

        SyncParentContent(_parent);
    }

    private static void SyncParentContent(SvgElement parent)
    {
        parent.Content = string.Concat(parent.Nodes.OfType<SvgContentNode>().Select(node => node.Content));
    }
}
