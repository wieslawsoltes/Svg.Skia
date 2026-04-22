using System;
using System.Collections.Generic;
using System.Linq;
using Svg;

namespace Svg.JavaScript;

public sealed class SvgJavaScriptDocument
{
    private static readonly string SvgNamespace = SvgNamespaces.SvgNamespace;
    private readonly Dictionary<SvgElement, SvgJavaScriptElement> _elements = new(new SvgElementReferenceComparer());
    private readonly SvgJavaScriptRuntime _runtime;
    private readonly SvgDocument _document;

    internal SvgJavaScriptDocument(SvgJavaScriptRuntime runtime, SvgDocument document)
    {
        _runtime = runtime;
        _document = document;
        defaultView = new SvgJavaScriptWindow(this);
    }

    public SvgJavaScriptWindow defaultView { get; }

    public SvgJavaScriptElement documentElement => GetOrCreateElement(_document);

    public SvgJavaScriptElement rootElement => documentElement;

    public SvgJavaScriptElement? getElementById(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var element = _document.GetElementById(id);
        return element is null ? null : GetOrCreateElement(element);
    }

    public SvgJavaScriptElement createElementNS(string? namespaceUri, string qualifiedName)
    {
        _ = namespaceUri;
        if (qualifiedName is null)
        {
            throw new ArgumentNullException(nameof(qualifiedName));
        }

        var localName = GetLocalName(qualifiedName);
        return GetOrCreateElement(CreateSvgElement(localName));
    }

    public SvgJavaScriptElement createElement(string qualifiedName)
    {
        return createElementNS(SvgNamespace, qualifiedName);
    }

    public SvgJavaScriptTextNode createTextNode(string? text)
    {
        return new SvgJavaScriptTextNode(this, new SvgContentNode { Content = text ?? string.Empty }, null);
    }

    public object[] getElementsByTagName(string tagName)
    {
        if (tagName is null || tagName == "*")
        {
            return _document.Descendants().Select(GetOrCreateElement).Cast<object>().ToArray();
        }

        return _document.Descendants()
            .Where(element => string.Equals(GetElementName(element), tagName, StringComparison.OrdinalIgnoreCase))
            .Select(GetOrCreateElement)
            .Cast<object>()
            .ToArray();
    }

    internal SvgJavaScriptElement GetOrCreateElement(SvgElement element)
    {
        if (!_elements.TryGetValue(element, out var facade))
        {
            facade = new SvgJavaScriptElement(_runtime, this, element);
            _elements[element] = facade;
        }

        return facade;
    }

    internal object? WrapNode(ISvgNode? node, SvgElement? parent)
    {
        return node switch
        {
            null => null,
            SvgElement element => GetOrCreateElement(element),
            SvgContentNode contentNode => new SvgJavaScriptTextNode(this, contentNode, parent),
            _ => new SvgJavaScriptTextNode(this, new SvgContentNode { Content = node.Content ?? string.Empty }, parent)
        };
    }

    internal void MarkMutation()
    {
        _runtime.MarkMutation();
    }

    internal static string GetElementName(SvgElement element)
    {
        if (element is SvgDocument)
        {
            return "svg";
        }

        if (element is SvgUnknownElement && element.CustomAttributes.TryGetValue("tagName", out var tagName))
        {
            return tagName;
        }

        return element switch
        {
            SvgAnchor _ => "a",
            SvgCircle _ => "circle",
            SvgDefinitionList _ => "defs",
            SvgEllipse _ => "ellipse",
            SvgGroup _ => "g",
            SvgImage _ => "image",
            SvgLine _ => "line",
            SvgPath _ => "path",
            SvgPolyline _ => "polyline",
            SvgPolygon _ => "polygon",
            SvgRectangle _ => "rect",
            SvgScript _ => "script",
            SvgFragment _ => "svg",
            SvgSwitch _ => "switch",
            SvgSymbol _ => "symbol",
            SvgText _ => "text",
            SvgUse _ => "use",
            _ => element.GetType().Name
        };
    }

    private static string GetLocalName(string qualifiedName)
    {
        var colonIndex = qualifiedName.IndexOf(':');
        return colonIndex >= 0 && colonIndex + 1 < qualifiedName.Length
            ? qualifiedName.Substring(colonIndex + 1)
            : qualifiedName;
    }

    private static SvgElement CreateSvgElement(string localName)
    {
        switch (localName)
        {
            case "a":
                return new SvgAnchor();
            case "circle":
                return new SvgCircle();
            case "defs":
                return new SvgDefinitionList();
            case "ellipse":
                return new SvgEllipse();
            case "g":
                return new SvgGroup();
            case "image":
                return new SvgImage();
            case "line":
                return new SvgLine();
            case "path":
                return new SvgPath();
            case "polygon":
                return new SvgPolygon();
            case "polyline":
                return new SvgPolyline();
            case "rect":
                return new SvgRectangle();
            case "script":
                return new SvgScript();
            case "svg":
                return new SvgFragment();
            case "switch":
                return new SvgSwitch();
            case "symbol":
                return new SvgSymbol();
            case "text":
                return new SvgText();
            case "use":
                return new SvgUse();
            default:
                var unknown = new SvgUnknownElement(localName);
                unknown.CustomAttributes["tagName"] = localName;
                return unknown;
        }
    }

    private sealed class SvgElementReferenceComparer : IEqualityComparer<SvgElement>
    {
        public bool Equals(SvgElement? x, SvgElement? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(SvgElement obj)
        {
            return obj.GetHashCode();
        }
    }
}
