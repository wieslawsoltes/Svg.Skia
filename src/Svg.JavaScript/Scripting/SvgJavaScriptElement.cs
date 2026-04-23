using System;
using System.Globalization;
using System.Linq;
using Svg;

namespace Svg.JavaScript;

public sealed class SvgJavaScriptElement
{
    private readonly SvgJavaScriptRuntime _runtime;
    private readonly SvgJavaScriptDocument _document;

    internal SvgJavaScriptElement(SvgJavaScriptRuntime runtime, SvgJavaScriptDocument document, SvgElement element)
    {
        _runtime = runtime;
        _document = document;
        Element = element;
        style = new SvgJavaScriptStyleDeclaration(this);
    }

    internal SvgElement Element { get; }

    public string nodeName => tagName;

    public int nodeType => 1;

    public string tagName => SvgJavaScriptDocument.GetElementName(Element);

    public string id
    {
        get => Element.ID ?? string.Empty;
        set => setAttribute("id", value);
    }

    public SvgJavaScriptDocument ownerDocument => _document;

    public SvgJavaScriptElement? parentNode => Element.Parent is null ? null : _document.GetOrCreateElement(Element.Parent);

    public SvgJavaScriptElement? parentElement => parentNode;

    public SvgJavaScriptStyleDeclaration style { get; }

    public object? firstChild => _document.WrapNode(GetNodes().FirstOrDefault(), Element);

    public object? nextSibling => GetSibling(1);

    public object? previousSibling => GetSibling(-1);

    public object[] childNodes => GetNodes().Select(node => _document.WrapNode(node, Element)!).ToArray();

    public object[] children => Element.Children.Select(child => (object)_document.GetOrCreateElement(child)).ToArray();

    public string textContent
    {
        get => GetTextContent(Element);
        set => SetTextContent(value);
    }

    public string innerHTML
    {
        get => textContent;
        set => textContent = value;
    }

    public SvgJavaScriptAnimatedString href => new(this, "href");

    public SvgJavaScriptAnimatedString className => new(this, "class");

    public SvgJavaScriptAnimatedLength x => new(this, "x");

    public SvgJavaScriptAnimatedLength y => new(this, "y");

    public SvgJavaScriptAnimatedLength width => new(this, "width");

    public SvgJavaScriptAnimatedLength height => new(this, "height");

    public string getAttribute(string name)
    {
        if (name is null)
        {
            return string.Empty;
        }

        if (Element.TryGetAttribute(name, out var value))
        {
            return value ?? string.Empty;
        }

        return Element.CustomAttributes.TryGetValue(name, out value) ? value ?? string.Empty : string.Empty;
    }

    public void setAttribute(string name, object? value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (!Element.TrySetAnimationValue(name, text))
        {
            Element.CustomAttributes[name] = text;
        }

        _runtime.MarkMutation();
    }

    public void removeAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        Element.ClearAnimationValue(name);
        Element.CustomAttributes.Remove(name);
        _runtime.MarkMutation();
    }

    public bool hasAttribute(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && Element.ContainsAttribute(name);
    }

    public object appendChild(object child)
    {
        switch (child)
        {
            case SvgJavaScriptElement childElement:
                AppendElement(childElement);
                return childElement;
            case SvgJavaScriptTextNode textNode:
                AppendText(textNode);
                return textNode;
            default:
                return child;
        }
    }

    public object removeChild(object child)
    {
        switch (child)
        {
            case SvgJavaScriptElement childElement:
                RemoveElement(childElement.Element);
                return childElement;
            case SvgJavaScriptTextNode textNode:
                Element.Nodes.Remove(textNode.Node);
                textNode.SetParent(null);
                SyncContentFromNodes();
                _runtime.MarkMutation();
                return textNode;
            default:
                return child;
        }
    }

    public SvgJavaScriptRect getBBox()
    {
        return GetElementBounds(Element) ?? SvgJavaScriptRect.Empty;
    }

    internal string GetStyleProperty(string name)
    {
        return getAttribute(name);
    }

    internal void SetStyleProperty(string name, object? value)
    {
        setAttribute(name, value);
    }

    private void AppendElement(SvgJavaScriptElement childElement)
    {
        RemoveElementFromParent(childElement.Element);
        Element.Children.Add(childElement.Element);
        if (!Element.Nodes.Contains(childElement.Element))
        {
            Element.Nodes.Add(childElement.Element);
        }

        _runtime.MarkMutation();
    }

    private void AppendText(SvgJavaScriptTextNode textNode)
    {
        textNode.DetachFromParent();
        Element.Nodes.Add(textNode.Node);
        textNode.SetParent(Element);
        SyncContentFromNodes();
        _runtime.MarkMutation();
    }

    private void RemoveElement(SvgElement childElement)
    {
        Element.Children.Remove(childElement);
        Element.Nodes.Remove(childElement);
        _runtime.MarkMutation();
    }

    private static void RemoveElementFromParent(SvgElement element)
    {
        var parent = element.Parent;
        if (parent is null)
        {
            return;
        }

        parent.Children.Remove(element);
        parent.Nodes.Remove(element);
    }

    private void SetTextContent(string? value)
    {
        Element.Children.Clear();
        Element.Nodes.Clear();
        Element.Content = value ?? string.Empty;
        if (!string.IsNullOrEmpty(Element.Content))
        {
            Element.Nodes.Add(new SvgContentNode { Content = Element.Content });
        }

        _runtime.MarkMutation();
    }

    private void SyncContentFromNodes()
    {
        Element.Content = string.Concat(Element.Nodes.OfType<SvgContentNode>().Select(node => node.Content));
    }

    private object? GetSibling(int offset)
    {
        var parent = Element.Parent;
        if (parent is null)
        {
            return null;
        }

        var nodes = parent.Nodes.Count > 0 ? parent.Nodes : parent.Children.Cast<ISvgNode>().ToList();
        var index = nodes.IndexOf(Element);
        var siblingIndex = index + offset;
        return index < 0 || siblingIndex < 0 || siblingIndex >= nodes.Count
            ? null
            : _document.WrapNode(nodes[siblingIndex], parent);
    }

    private System.Collections.Generic.IReadOnlyList<ISvgNode> GetNodes()
    {
        if (Element.Nodes.Count > 0)
        {
            return Element.Nodes.ToArray();
        }

        return Element.Children.Cast<ISvgNode>().ToArray();
    }

    private static string GetTextContent(SvgElement element)
    {
        if (!string.IsNullOrEmpty(element.Content))
        {
            return element.Content;
        }

        if (element.Nodes.Count > 0)
        {
            return string.Concat(element.Nodes.Select(static node =>
                node is SvgElement childElement ? GetTextContent(childElement) : node.Content));
        }

        return string.Concat(element.Children.Select(GetTextContent));
    }

    private static SvgJavaScriptRect? GetElementBounds(SvgElement element)
    {
        if (element is SvgRectangle)
        {
            var x = GetFloatAttribute(element, "x");
            var y = GetFloatAttribute(element, "y");
            return new SvgJavaScriptRect(
                x,
                y,
                GetFloatAttribute(element, "width"),
                GetFloatAttribute(element, "height"));
        }

        if (element is SvgCircle)
        {
            var cx = GetFloatAttribute(element, "cx");
            var cy = GetFloatAttribute(element, "cy");
            var r = GetFloatAttribute(element, "r");
            return new SvgJavaScriptRect(cx - r, cy - r, r + r, r + r);
        }

        if (element is SvgEllipse)
        {
            var cx = GetFloatAttribute(element, "cx");
            var cy = GetFloatAttribute(element, "cy");
            var rx = GetFloatAttribute(element, "rx");
            var ry = GetFloatAttribute(element, "ry");
            return new SvgJavaScriptRect(cx - rx, cy - ry, rx + rx, ry + ry);
        }

        if (element is SvgLine)
        {
            var x1 = GetFloatAttribute(element, "x1");
            var y1 = GetFloatAttribute(element, "y1");
            var x2 = GetFloatAttribute(element, "x2");
            var y2 = GetFloatAttribute(element, "y2");
            var left = Math.Min(x1, x2);
            var top = Math.Min(y1, y2);
            return new SvgJavaScriptRect(left, top, Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        }

        SvgJavaScriptRect? bounds = null;
        foreach (var child in element.Children)
        {
            var childBounds = GetElementBounds(child);
            if (childBounds is null)
            {
                continue;
            }

            bounds = bounds is null ? childBounds : Union(bounds, childBounds);
        }

        return bounds;
    }

    private static SvgJavaScriptRect Union(SvgJavaScriptRect first, SvgJavaScriptRect second)
    {
        var left = Math.Min(first.x, second.x);
        var top = Math.Min(first.y, second.y);
        var right = Math.Max(first.x + first.width, second.x + second.width);
        var bottom = Math.Max(first.y + first.height, second.y + second.height);
        return new SvgJavaScriptRect(left, top, right - left, bottom - top);
    }

    private static float GetFloatAttribute(SvgElement element, string name)
    {
        if (!element.TryGetAttribute(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return 0f;
        }

        var text = value.Trim();
        while (text.Length > 0 && !char.IsDigit(text[text.Length - 1]) && text[text.Length - 1] != '.')
        {
            text = text.Substring(0, text.Length - 1);
        }

        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0f;
    }
}

public sealed class SvgJavaScriptRect
{
    public static readonly SvgJavaScriptRect Empty = new(0, 0, 0, 0);

    public SvgJavaScriptRect(float x, float y, float width, float height)
    {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
    }

    public float x { get; }

    public float y { get; }

    public float width { get; }

    public float height { get; }
}
