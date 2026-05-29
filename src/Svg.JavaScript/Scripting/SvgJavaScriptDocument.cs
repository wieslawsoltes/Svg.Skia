using System;
using System.Collections.Generic;
using System.Linq;
using Svg;

namespace Svg.JavaScript;

public sealed class SvgJavaScriptDocument
{
    private static readonly string SvgNamespace = SvgNamespaces.SvgNamespace;
    private static readonly Lazy<Dictionary<string, Func<SvgElement>>> s_elementFactories = new(CreateElementFactories);
    private readonly Dictionary<SvgElement, SvgJavaScriptElement> _elements = new(new SvgElementReferenceComparer());
    private readonly Dictionary<SvgContentNode, SvgJavaScriptTextNode> _textNodes = new(new SvgContentNodeReferenceComparer());
    private readonly SvgJavaScriptRuntime _runtime;
    private readonly SvgDocument _document;

    internal SvgJavaScriptDocument(SvgJavaScriptRuntime runtime, SvgDocument document)
    {
        _runtime = runtime;
        _document = document;
        defaultView = new SvgJavaScriptWindow(runtime, this);
        implementation = new SvgJavaScriptDomImplementation();
    }

    public SvgJavaScriptWindow defaultView { get; }

    public SvgJavaScriptDomImplementation implementation { get; }

    public SvgJavaScriptElement documentElement => GetOrCreateElement(_document);

    public SvgJavaScriptElement rootElement => documentElement;

    public string nodeName => "#document";

    public int nodeType => 9;

    public string? nodeValue
    {
        get => null;
        set { }
    }

    public SvgJavaScriptDocument? ownerDocument => null;

    public SvgJavaScriptElement? firstChild => documentElement;

    public SvgJavaScriptElement? lastChild => documentElement;

    public SvgJavaScriptNodeList childNodes => new(() => new object?[] { documentElement });

    public SvgJavaScriptNodeList children => childNodes;

    public bool hasChildNodes()
    {
        return true;
    }

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
        if (qualifiedName is null)
        {
            throw new ArgumentNullException(nameof(qualifiedName));
        }

        var localName = GetLocalName(qualifiedName);
        return GetOrCreateElement(CreateElement(namespaceUri, localName));
    }

    public SvgJavaScriptElement createElement(string qualifiedName)
    {
        return createElementNS(SvgNamespace, qualifiedName);
    }

    public SvgJavaScriptTextNode createTextNode(string? text)
    {
        return GetOrCreateTextNode(new SvgContentNode { Content = text ?? string.Empty }, null);
    }

    public SvgJavaScriptEvent createEvent(string? eventInterface)
    {
        _ = eventInterface;
        return new SvgJavaScriptEvent();
    }

    public void clearTextSelection()
    {
        _runtime.ClearTextSelection();
    }

    public SvgJavaScriptTextSelection? getTextSelection()
    {
        return _runtime.GetTextSelection(null);
    }

    public SvgJavaScriptNodeList getElementsByTagName(string tagName)
    {
        if (tagName is null || tagName == "*")
        {
            return new SvgJavaScriptNodeList(() => GetDocumentAndDescendants().Select(GetOrCreateElement).Cast<object?>());
        }

        return new SvgJavaScriptNodeList(() => GetDocumentAndDescendants()
            .Where(element => string.Equals(GetElementName(element), tagName, StringComparison.OrdinalIgnoreCase))
            .Select(GetOrCreateElement)
            .Cast<object?>());
    }

    public SvgJavaScriptNodeList getElementsByTagNameNS(string? namespaceUri, string localName)
    {
        return new SvgJavaScriptNodeList(() => GetDocumentAndDescendants()
            .Where(element => ElementMatchesNamespaceAndName(element, namespaceUri, localName))
            .Select(GetOrCreateElement)
            .Cast<object?>());
    }

    public SvgJavaScriptNodeList getElementsByClassName(string classNames)
    {
        return new SvgJavaScriptNodeList(() => GetDocumentAndDescendants()
            .Where(element => ElementMatchesClassNames(element, classNames))
            .Select(GetOrCreateElement)
            .Cast<object?>());
    }

    internal SvgJavaScriptRuntime Runtime => _runtime;

    internal SvgDocument RawDocument => _document;

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
            SvgContentNode contentNode => GetOrCreateTextNode(contentNode, parent),
            _ => new SvgJavaScriptTextNode(this, new SvgContentNode { Content = node.Content ?? string.Empty }, parent)
        };
    }

    internal IReadOnlyList<ISvgNode> GetDomNodes(SvgElement element)
    {
        EnsureDomNodesInitialized(element);
        return element.Nodes.Count > 0 ? element.Nodes.ToArray() : element.Children.Cast<ISvgNode>().ToArray();
    }

    internal void MarkMutation()
    {
        _runtime.MarkMutation();
    }

    internal SvgJavaScriptElement? GetElementByIdWithinSubtree(SvgElement root, string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        if (string.Equals(root.ID, id, StringComparison.Ordinal))
        {
            return GetOrCreateElement(root);
        }

        var match = root.Descendants().FirstOrDefault(element => !ReferenceEquals(element, root) && string.Equals(element.ID, id, StringComparison.Ordinal));
        return match is null ? null : GetOrCreateElement(match);
    }

    internal SvgJavaScriptTextNode GetOrCreateTextNode(SvgContentNode node, SvgElement? parent)
    {
        if (!_textNodes.TryGetValue(node, out var textNode))
        {
            textNode = new SvgJavaScriptTextNode(this, node, parent);
            _textNodes[node] = textNode;
            return textNode;
        }

        textNode.SetParent(parent);
        return textNode;
    }

    internal void DetachTextNodes(SvgElement parent)
    {
        foreach (var node in parent.Nodes.OfType<SvgContentNode>())
        {
            if (_textNodes.TryGetValue(node, out var textNode))
            {
                textNode.SetParent(null);
            }
        }
    }

    internal static void EnsureDomNodesInitialized(SvgElement element)
    {
        if (element.Nodes.Count > 0 || element.Children.Count == 0)
        {
            return;
        }

        for (var i = 0; i < element.Children.Count; i++)
        {
            element.Nodes.Add(element.Children[i]);
        }
    }

    private IEnumerable<SvgElement> GetDocumentAndDescendants()
    {
        yield return _document;
        foreach (var element in _document.Descendants())
        {
            yield return element;
        }
    }

    internal static string GetElementName(SvgElement element)
    {
        if (element is SvgDocument)
        {
            return "svg";
        }

        if (element is NonSvgElement nonSvgElement)
        {
            return nonSvgElement.Name;
        }

        if (element is SvgUnknownElement && element.CustomAttributes.TryGetValue("tagName", out var tagName))
        {
            return tagName;
        }

        if (!string.IsNullOrEmpty(element.ElementName))
        {
            return element.ElementName;
        }

        return element switch
        {
            _ when SvgElements.ElementNames.TryGetValue(element.GetType(), out var elementName) => elementName,
            _ => element.GetType().Name
        };
    }

    internal static string GetElementNamespace(SvgElement element)
    {
        return string.IsNullOrEmpty(element.ElementNamespace)
            ? SvgNamespace
            : element.ElementNamespace;
    }

    internal static bool ElementMatchesNamespaceAndName(SvgElement element, string? namespaceUri, string localName)
    {
        var namespaceMatches = namespaceUri == "*" ||
                               string.Equals(GetElementNamespace(element), namespaceUri ?? string.Empty, StringComparison.Ordinal);
        var nameMatches = localName == "*" ||
                          string.Equals(GetElementName(element), localName, StringComparison.OrdinalIgnoreCase);
        return namespaceMatches && nameMatches;
    }

    internal static bool ElementMatchesClassNames(SvgElement element, string classNames)
    {
        var requiredClasses = ParseClassNames(classNames);
        if (requiredClasses.Length == 0)
        {
            return false;
        }

        if (!TryGetClassAttribute(element, out var classAttribute))
        {
            return false;
        }

        var actualClasses = ParseClassNames(classAttribute);
        return requiredClasses.All(required => actualClasses.Contains(required, StringComparer.Ordinal));
    }

    private static string GetLocalName(string qualifiedName)
    {
        var colonIndex = qualifiedName.IndexOf(':');
        return colonIndex >= 0 && colonIndex + 1 < qualifiedName.Length
            ? qualifiedName.Substring(colonIndex + 1)
            : qualifiedName;
    }

    private static SvgElement CreateElement(string? namespaceUri, string localName)
    {
        if (string.IsNullOrEmpty(namespaceUri) ||
            string.Equals(namespaceUri, SvgNamespace, StringComparison.Ordinal))
        {
            return CreateSvgElement(localName);
        }

        return new NonSvgElement(localName, namespaceUri);
    }

    private static SvgElement CreateSvgElement(string localName)
    {
        if (s_elementFactories.Value.TryGetValue(localName, out var factory))
        {
            return factory();
        }

        var unknown = new SvgUnknownElement(localName);
        unknown.CustomAttributes["tagName"] = localName;
        return unknown;
    }

    private static string[] ParseClassNames(string? classNames)
    {
        if (string.IsNullOrWhiteSpace(classNames))
        {
            return Array.Empty<string>();
        }

        return classNames!.Split(new[] { ' ', '\t', '\r', '\n', '\f' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool TryGetClassAttribute(SvgElement element, out string classAttribute)
    {
        if (element.TryGetJavaScriptDomAttributeValue("class", out var scriptClassAttribute) &&
            !string.IsNullOrWhiteSpace(scriptClassAttribute))
        {
            classAttribute = scriptClassAttribute;
            return true;
        }

        if (element.TryGetAttribute("class", out var parsedClassAttribute) &&
            !string.IsNullOrWhiteSpace(parsedClassAttribute))
        {
            classAttribute = parsedClassAttribute;
            return true;
        }

        if (element.CustomAttributes.TryGetValue("class", out var customClassAttribute) &&
            !string.IsNullOrWhiteSpace(customClassAttribute))
        {
            classAttribute = customClassAttribute!;
            return true;
        }

        classAttribute = string.Empty;
        return false;
    }

    private static Dictionary<string, Func<SvgElement>> CreateElementFactories()
    {
        var factories = new Dictionary<string, Func<SvgElement>>(StringComparer.Ordinal);
        foreach (var elementInfo in new SvgElementFactory().AvailableElements)
        {
            factories[elementInfo.ElementName] = elementInfo.CreateInstance;
        }

        return factories;
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

    private sealed class SvgContentNodeReferenceComparer : IEqualityComparer<SvgContentNode>
    {
        public bool Equals(SvgContentNode? x, SvgContentNode? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(SvgContentNode obj)
        {
            return obj.GetHashCode();
        }
    }
}
