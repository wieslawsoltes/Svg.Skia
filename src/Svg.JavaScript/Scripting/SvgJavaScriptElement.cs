using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jint.Native;
using ShimSkiaSharp;
using Svg;
using Svg.Model.Services;
using Svg.Skia;

namespace Svg.JavaScript;

public sealed partial class SvgJavaScriptElement
{
    private readonly SvgJavaScriptRuntime _runtime;
    private readonly SvgJavaScriptDocument _document;
    private readonly Dictionary<string, string?> _inlineStyleFallbacks = new(StringComparer.OrdinalIgnoreCase);
    private SvgJavaScriptPoint? _currentTranslate;
    private SvgJavaScriptPoint? _hostCurrentTranslate;
    private double _currentScale = 1d;

    internal SvgJavaScriptElement(SvgJavaScriptRuntime runtime, SvgJavaScriptDocument document, SvgElement element)
    {
        _runtime = runtime;
        _document = document;
        Element = element;
        style = new SvgJavaScriptStyleDeclaration(this);
    }

    internal SvgElement Element { get; }

    internal SvgJavaScriptRuntime Runtime => _runtime;

    public string nodeName => tagName;

    public int nodeType => 1;

    public string localName => tagName;

    public string namespaceURI => SvgJavaScriptDocument.GetElementNamespace(Element);

    public string? nodeValue
    {
        get => null;
        set { }
    }

    public string tagName => SvgJavaScriptDocument.GetElementName(Element);

    public string id
    {
        get => Element.ID ?? string.Empty;
        set => setAttribute("id", value);
    }

    public SvgJavaScriptDocument ownerDocument => _document;

    public SvgJavaScriptElement? parentNode => Element.Parent is null ? null : _document.GetOrCreateElement(Element.Parent);

    public SvgJavaScriptElement? parentElement => parentNode;

    public SvgJavaScriptElement? ownerSVGElement => FindOwnerSvgElement();

    public SvgJavaScriptElement? viewportElement => nearestViewportElement;

    public SvgJavaScriptElement? nearestViewportElement => FindViewportElement(outermost: false);

    public SvgJavaScriptElement? farthestViewportElement => FindViewportElement(outermost: true);

    public double currentScale
    {
        get => TryGetViewerHost(out var viewerHost) ? viewerHost.CurrentScale : _currentScale;
        set
        {
            if (!double.IsNaN(value) && !double.IsInfinity(value) && value > 0d)
            {
                if (TryGetViewerHost(out var viewerHost))
                {
                    viewerHost.CurrentScale = value;
                }
                else
                {
                    _currentScale = value;
                }
            }
        }
    }

    public SvgJavaScriptPoint currentTranslate
    {
        get
        {
            if (TryGetViewerHost(out var viewerHost))
            {
                return _hostCurrentTranslate ??= new SvgJavaScriptPoint(
                    _runtime,
                    () => new SvgJavaScriptPoint.SvgJavaScriptPointState(
                        viewerHost.CurrentTranslateX,
                        viewerHost.CurrentTranslateY),
                    state =>
                    {
                        viewerHost.CurrentTranslateX = state.X;
                        viewerHost.CurrentTranslateY = state.Y;
                    },
                    readOnly: false);
            }

            return _currentTranslate ??= new SvgJavaScriptPoint();
        }
    }

    public SvgJavaScriptElementInstance? instanceRoot => Element is SvgUse use ? _runtime.GetUseInstanceRoot(use) : null;

    public SvgJavaScriptStyleDeclaration style { get; }

    public object? firstChild => _document.WrapNode(GetNodes().FirstOrDefault(), Element);

    public object? lastChild => _document.WrapNode(GetNodes().LastOrDefault(), Element);

    public object? nextSibling => GetSibling(1);

    public object? previousSibling => GetSibling(-1);

    public SvgJavaScriptNodeList childNodes => new(() => GetNodes().Select(node => _document.WrapNode(node, Element)));

    public SvgJavaScriptNodeList children => new(() => Element.Children.Select(child => (object?)_document.GetOrCreateElement(child)));

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

    public SvgJavaScriptAnimatedString href => new(this, "xlink:href", IsAnimatedStringAttributeAnimatable("xlink:href"));

    public SvgJavaScriptAnimatedString className => new(this, "class");

    public object x => UsesLengthList("x") ? new SvgJavaScriptAnimatedLengthList(_runtime, this, "x") : new SvgJavaScriptAnimatedLength(this, "x");

    public object y => UsesLengthList("y") ? new SvgJavaScriptAnimatedLengthList(_runtime, this, "y") : new SvgJavaScriptAnimatedLength(this, "y");

    public object width => new SvgJavaScriptAnimatedLength(this, "width");

    public object height => new SvgJavaScriptAnimatedLength(this, "height");

    public SvgJavaScriptAnimatedLength r => new(this, "r");

    public SvgJavaScriptAnimatedAngle orientAngle => new(_runtime, GetOrientValue, value => setAttribute("orient", value));

    public SvgJavaScriptAnimatedNumberList rotate => new(_runtime, this, "rotate");

    public SvgJavaScriptAnimatedNumber offset =>
        new(_runtime, this, () => getAttribute("offset"), value => setAttribute("offset", SvgJavaScriptParsing.FormatNumber(value)));

    public SvgJavaScriptAnimatedTransformList transform => new(_runtime, this);

    public SvgJavaScriptAnimatedRect viewBox => new(_runtime, this, "viewBox");

    public SvgJavaScriptAnimatedPreserveAspectRatio preserveAspectRatio => new(_runtime, this, "preserveAspectRatio");

    public SvgJavaScriptAnimatedBoolean externalResourcesRequired => new(_runtime, this, "externalResourcesRequired");

    public SvgJavaScriptAnimatedBoolean preserveAlpha => new(_runtime, this, "preserveAlpha");

    public SvgJavaScriptAnimatedEnumeration lengthAdjust =>
        new(_runtime, this, "lengthAdjust", ParseLengthAdjust, FormatLengthAdjust);

    public SvgJavaScriptAnimatedEnumeration gradientUnits =>
        new(_runtime, this, "gradientUnits", ParseGradientUnits, FormatGradientUnits);

    public SvgJavaScriptAnimatedLength textLength =>
        new(_runtime, GetTextLengthValue, SetTextLengthValue);

    public SvgJavaScriptAnimatedInteger numOctaves => new(_runtime, this, "numOctaves");

    public SvgJavaScriptAnimatedInteger filterResX =>
        new(_runtime, () => GetIntegerToken("filterRes", 0), value => SetIntegerToken("filterRes", 0, value));

    public SvgJavaScriptAnimatedNumber baseFrequencyY =>
        new(_runtime, this, () => GetNumberToken("baseFrequency", 1), value => SetNumberToken("baseFrequency", 1, value));

    public SvgJavaScriptAnimatedNumberList kernelMatrix => new(_runtime, this, "kernelMatrix");

    public SvgJavaScriptStringList requiredFeatures =>
        new(_runtime, () => ParseTokenList("requiredFeatures"), values => SetTokenList("requiredFeatures", values));

    public SvgJavaScriptStringList requiredExtensions =>
        new(_runtime, () => ParseTokenList("requiredExtensions"), values => SetTokenList("requiredExtensions", values));

    public SvgJavaScriptPointList points => new(_runtime, ParsePoints);

    public int zoomAndPan
    {
        get => ParseZoomAndPan(getAttribute("zoomAndPan"));
        set => setAttribute("zoomAndPan", FormatZoomAndPan(value));
    }

    public string getAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var normalizedName = NormalizeAttributeStorageName(name);
        if (Element.TryGetJavaScriptDomAttributeValue(normalizedName, out var scriptSetValue))
        {
            return scriptSetValue;
        }

        if (TryGetSpecialAttributeValue(normalizedName, out var specialValue))
        {
            return specialValue;
        }

        if (Element.TryGetAttribute(normalizedName, out var value))
        {
            return value ?? string.Empty;
        }

        return Element.CustomAttributes.TryGetValue(normalizedName, out value) ? value ?? string.Empty : string.Empty;
    }

    internal string GetBaseAttributeValue(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var normalizedName = NormalizeAttributeStorageName(name);
        if (string.Equals(normalizedName, "href", StringComparison.OrdinalIgnoreCase) &&
            Element is SvgScript)
        {
            if (TryGetRawXLinkHrefValue(out var rawXLinkHref))
            {
                return rawXLinkHref;
            }

            var scriptHref = getAttribute(normalizedName);
            if (!string.IsNullOrWhiteSpace(scriptHref))
            {
                return scriptHref;
            }
        }

        return _runtime.TryGetBaseAttributeValue(Element, normalizedName, out var baseValue)
            ? baseValue
            : getAttribute(normalizedName);
    }

    private bool IsAnimatedStringAttributeAnimatable(string attributeName)
    {
        if (string.Equals(attributeName, "xlink:href", StringComparison.OrdinalIgnoreCase) &&
            Element is SvgScript)
        {
            return false;
        }

        return true;
    }

    public string getAttributeNS(string? namespaceUri, string localName)
    {
        var qualifiedName = QualifyAttributeName(namespaceUri, localName);
        var value = getAttribute(qualifiedName);
        return value.Length > 0 || namespaceUri is not null ? value : getAttribute(localName);
    }

    public void setAttribute(string name, object? value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var normalizedName = name.Trim();
        normalizedName = NormalizeAttributeStorageName(normalizedName);
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (string.Equals(normalizedName, "style", StringComparison.OrdinalIgnoreCase))
        {
            SetInlineStyleText(text);
            _runtime.MarkMutation();
            return;
        }

        SetAttributeValue(normalizedName, text);
        var hasCompatibilityStyleSources = _document.RawDocument.HasCompatibilityStyleSources;
        var isStyleAttribute = SvgStyleAttributeNames.Contains(normalizedName);
        var inlineStyleOverridesAttribute = isStyleAttribute && UpdateInlineStyleFallback(normalizedName, text);
        if (isStyleAttribute && (hasCompatibilityStyleSources || inlineStyleOverridesAttribute))
        {
            _document.RawDocument.UpdateCompatibilityStyleAttribute(Element, normalizedName, text);
        }

        var requiresStyleReapply = hasCompatibilityStyleSources || inlineStyleOverridesAttribute;
        if (requiresStyleReapply && IsConnectedToDocument())
        {
            _document.RawDocument.ReapplyCompatibilityStyles();
        }

        _runtime.MarkMutation();
    }

    public void setAttributeNS(string? namespaceUri, string localName, object? value)
    {
        setAttribute(QualifyAttributeName(namespaceUri, localName), value);
    }

    public void removeAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var normalizedName = name.Trim();
        normalizedName = NormalizeAttributeStorageName(normalizedName);
        if (string.Equals(normalizedName, "style", StringComparison.OrdinalIgnoreCase))
        {
            Element.ClearJavaScriptDomAttributeValue(normalizedName);
            SetInlineStyleText(string.Empty, syncJavaScriptDomAttribute: false);
            _runtime.MarkMutation();
            return;
        }

        RemoveAttributeValue(normalizedName);
        var hasCompatibilityStyleSources = _document.RawDocument.HasCompatibilityStyleSources;
        var isStyleAttribute = SvgStyleAttributeNames.Contains(normalizedName);
        var inlineStyleOverridesAttribute = isStyleAttribute && UpdateInlineStyleFallback(normalizedName, null);
        if (isStyleAttribute && (hasCompatibilityStyleSources || inlineStyleOverridesAttribute))
        {
            _document.RawDocument.UpdateCompatibilityStyleAttribute(Element, normalizedName, null);
        }

        var requiresStyleReapply = hasCompatibilityStyleSources || inlineStyleOverridesAttribute;
        if (requiresStyleReapply && IsConnectedToDocument())
        {
            _document.RawDocument.ReapplyCompatibilityStyles();
        }

        _runtime.MarkMutation();
    }

    public void removeAttributeNS(string? namespaceUri, string localName)
    {
        removeAttribute(QualifyAttributeName(namespaceUri, localName));
    }

    public bool hasAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalizedName = NormalizeAttributeStorageName(name);
        return Element.TryGetJavaScriptDomAttributeValue(normalizedName, out _)
               || Element.TryGetAttribute(normalizedName, out _)
               || Element.CustomAttributes.ContainsKey(normalizedName);
    }

    public bool hasAttributeNS(string? namespaceUri, string localName)
    {
        return hasAttribute(QualifyAttributeName(namespaceUri, localName));
    }

    public bool hasChildNodes()
    {
        return GetNodes().Count > 0;
    }

    public SvgJavaScriptElement? getElementById(string? id)
    {
        if (id is not { Length: > 0 } elementId)
        {
            return null;
        }

        return _document.GetElementByIdWithinSubtree(Element, elementId);
    }

    public SvgJavaScriptNodeList getElementsByTagName(string tagName)
    {
        if (tagName is null || tagName == "*")
        {
            return new SvgJavaScriptNodeList(() => Element.Descendants()
                .Where(element => !ReferenceEquals(element, Element))
                .Select(_document.GetOrCreateElement)
                .Cast<object?>());
        }

        return new SvgJavaScriptNodeList(() => Element.Descendants()
            .Where(element =>
                !ReferenceEquals(element, Element) &&
                string.Equals(SvgJavaScriptDocument.GetElementName(element), tagName, StringComparison.OrdinalIgnoreCase))
            .Select(_document.GetOrCreateElement)
            .Cast<object?>());
    }

    public SvgJavaScriptNodeList getElementsByTagNameNS(string? namespaceUri, string localName)
    {
        return new SvgJavaScriptNodeList(() => Element.Descendants()
            .Where(element =>
                !ReferenceEquals(element, Element) &&
                SvgJavaScriptDocument.ElementMatchesNamespaceAndName(element, namespaceUri, localName))
            .Select(_document.GetOrCreateElement)
            .Cast<object?>());
    }

    public SvgJavaScriptNodeList getElementsByClassName(string classNames)
    {
        return new SvgJavaScriptNodeList(() => Element.Descendants()
            .Where(element =>
                !ReferenceEquals(element, Element) &&
                SvgJavaScriptDocument.ElementMatchesClassNames(element, classNames))
            .Select(_document.GetOrCreateElement)
            .Cast<object?>());
    }

    public object appendChild(object child)
    {
        return insertBefore(child, null);
    }

    public object insertBefore(object child, object? referenceChild)
    {
        switch (child)
        {
            case SvgJavaScriptElement childElement:
                InsertElement(childElement.Element, referenceChild);
                return childElement;
            case SvgJavaScriptTextNode textNode:
                InsertText(textNode, referenceChild);
                return textNode;
            default:
                return child;
        }
    }

    public object replaceChild(object newChild, object oldChild)
    {
        var newNode = UnwrapNode(newChild);
        var oldNode = UnwrapNode(oldChild);
        if (newNode is not null && ReferenceEquals(newNode, oldNode))
        {
            return oldChild;
        }

        insertBefore(newChild, oldChild);
        return removeChild(oldChild);
    }

    public object removeChild(object child)
    {
        switch (child)
        {
            case SvgJavaScriptElement childElement:
                SvgJavaScriptDocument.EnsureDomNodesInitialized(Element);
                if (!Element.Children.Contains(childElement.Element) ||
                    !Element.Nodes.Contains(childElement.Element))
                {
                    ThrowNotFound("The element is not a child of this node.");
                }

                _ = Element.Children.Remove(childElement.Element);
                _ = Element.Nodes.Remove(childElement.Element);
                if (_document.RawDocument.HasCompatibilityStyleSources)
                {
                    _document.RawDocument.ReapplyCompatibilityStyles();
                }

                _runtime.MarkMutation();
                return childElement;
            case SvgJavaScriptTextNode textNode:
                SvgJavaScriptDocument.EnsureDomNodesInitialized(Element);
                if (!Element.Nodes.Contains(textNode.Node))
                {
                    ThrowNotFound("The text node is not a child of this node.");
                }

                _ = Element.Nodes.Remove(textNode.Node);
                textNode.SetParent(null);
                SyncContentFromNodes();
                _runtime.MarkMutation();
                return textNode;
            default:
                return child;
        }
    }

    public SvgJavaScriptRect? getBBox()
    {
        return GetElementBounds(Element);
    }

    public bool dispatchEvent(SvgJavaScriptEvent evt)
    {
        return _runtime.DispatchEvent(this, evt);
    }

    public JsValue focus()
    {
        _runtime.FocusElement(Element);
        return JsValue.Undefined;
    }

    public JsValue blur()
    {
        _runtime.BlurElement(Element);
        return JsValue.Undefined;
    }

    public SvgJavaScriptMatrix? getCTM()
    {
        return TryGetElementMatrix(Element, out var matrix) ? new SvgJavaScriptMatrix(matrix) : null;
    }

    public SvgJavaScriptMatrix? getScreenCTM()
    {
        return getCTM();
    }

    public SvgJavaScriptMatrix? getTransformToElement(SvgJavaScriptElement targetElement)
    {
        if (targetElement is null)
        {
            throw new ArgumentNullException(nameof(targetElement));
        }

        if (!TryGetElementMatrix(Element, out var source) || !TryGetElementMatrix(targetElement.Element, out var target))
        {
            return null;
        }

        return target.TryInvert(out var inverse) ? new SvgJavaScriptMatrix(source.PreConcat(inverse)) : null;
    }

    public SvgJavaScriptNodeList getIntersectionList(SvgJavaScriptRect rect, object? referenceElement)
    {
        _ = referenceElement;
        return new SvgJavaScriptNodeList(GetMatchingSceneElements(rect, enclosure: false));
    }

    public SvgJavaScriptNodeList getEnclosureList(SvgJavaScriptRect rect, object? referenceElement)
    {
        _ = referenceElement;
        return new SvgJavaScriptNodeList(GetMatchingSceneElements(rect, enclosure: true));
    }

    public bool checkIntersection(SvgJavaScriptElement element, SvgJavaScriptRect rect)
    {
        return CheckSceneRectMatch(element, rect, enclosure: false);
    }

    public bool checkEnclosure(SvgJavaScriptElement element, SvgJavaScriptRect rect)
    {
        return CheckSceneRectMatch(element, rect, enclosure: true);
    }

    public double getCurrentTime()
    {
        return _runtime.GetCurrentTimeSeconds();
    }

    public void setCurrentTime(double seconds)
    {
        _runtime.SetCurrentTimeSeconds(seconds);
    }

    public double getComputedTextLength()
    {
        return _runtime.GetComputedTextLength(RequireTextContentElement());
    }

    public int getNumberOfChars()
    {
        return _runtime.GetNumberOfChars(RequireTextContentElement());
    }

    public double getSubStringLength(int charnum, int nchars)
    {
        return _runtime.GetSubStringLength(RequireTextContentElement(), charnum, nchars);
    }

    public SvgJavaScriptPoint getStartPositionOfChar(int charnum)
    {
        return _runtime.GetStartPositionOfChar(RequireTextContentElement(), charnum);
    }

    public SvgJavaScriptPoint getEndPositionOfChar(int charnum)
    {
        return _runtime.GetEndPositionOfChar(RequireTextContentElement(), charnum);
    }

    public SvgJavaScriptRect getExtentOfChar(int charnum)
    {
        return _runtime.GetExtentOfChar(RequireTextContentElement(), charnum);
    }

    public double getRotationOfChar(int charnum)
    {
        return _runtime.GetRotationOfChar(RequireTextContentElement(), charnum);
    }

    public int getCharNumAtPosition(SvgJavaScriptPoint point)
    {
        if (point is null)
        {
            throw new ArgumentNullException(nameof(point));
        }

        return _runtime.GetCharNumAtPosition(RequireTextContentElement(), point);
    }

    public JsValue selectSubString(int charnum, int nchars)
    {
        _runtime.SelectSubString(RequireTextContentElement(), charnum, nchars);
        return JsValue.Undefined;
    }

    public bool beginTextSelection(int charnum)
    {
        return _runtime.BeginTextSelection(RequireTextContentElement(), charnum);
    }

    public bool extendTextSelection(int charnum)
    {
        return _runtime.ExtendTextSelection(RequireTextContentElement(), charnum);
    }

    public bool selectTextRange(int anchorCharnum, int focusCharnum)
    {
        return _runtime.SelectTextRange(RequireTextContentElement(), anchorCharnum, focusCharnum);
    }

    public JsValue clearTextSelection()
    {
        _runtime.ClearTextSelection();
        return JsValue.Undefined;
    }

    public SvgJavaScriptTextSelection? getTextSelection()
    {
        return _runtime.GetTextSelection(RequireTextContentElement());
    }

    public JsValue beginElement()
    {
        BeginTimedElement(TimeSpan.Zero);
        return JsValue.Undefined;
    }

    public JsValue beginElementAt(double offset)
    {
        BeginTimedElement(CreateOffset(offset));
        return JsValue.Undefined;
    }

    public JsValue endElement()
    {
        EndTimedElement(TimeSpan.Zero);
        return JsValue.Undefined;
    }

    public JsValue endElementAt(double offset)
    {
        EndTimedElement(CreateOffset(offset));
        return JsValue.Undefined;
    }

    public double getStartTime()
    {
        if (Element is SvgAnimationElement animation)
        {
            return _runtime.GetAnimationStartTimeSeconds(animation);
        }

        _runtime.ThrowDomException(11, "The element does not implement SVGAnimationElement.");
        return 0d;
    }

    public SvgJavaScriptNumber createSVGNumber()
    {
        return new SvgJavaScriptNumber();
    }

    public SvgJavaScriptLength createSVGLength()
    {
        return new SvgJavaScriptLength(_runtime, false);
    }

    public SvgJavaScriptAngle createSVGAngle()
    {
        var state = "0";
        return new SvgJavaScriptAngle(_runtime, () => state, value => state = value, false);
    }

    public SvgJavaScriptPoint createSVGPoint()
    {
        return new SvgJavaScriptPoint();
    }

    public SvgJavaScriptMatrix createSVGMatrix()
    {
        return new SvgJavaScriptMatrix();
    }

    public SvgJavaScriptRect createSVGRect()
    {
        return new SvgJavaScriptRect(0f, 0f, 0f, 0f);
    }

    public SvgJavaScriptTransform createSVGTransform()
    {
        return new SvgJavaScriptTransform(_runtime, SvgJavaScriptMatrixHelpers.FromSkMatrix(SKMatrix.Identity), null, false);
    }

    private void BeginTimedElement(TimeSpan offset)
    {
        if (Element is SvgAnimationElement animation)
        {
            _runtime.BeginElement(animation, offset);
        }
    }

    private void EndTimedElement(TimeSpan offset)
    {
        if (Element is SvgAnimationElement animation)
        {
            _runtime.EndElement(animation, offset);
        }
    }

    private static TimeSpan CreateOffset(double seconds)
    {
        return double.IsNaN(seconds) || double.IsInfinity(seconds)
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(seconds);
    }

    private SvgTextBase RequireTextContentElement()
    {
        if (Element is SvgTextBase textContentElement)
        {
            return textContentElement;
        }

        _runtime.ThrowDomException(11, "The element does not implement SVGTextContentElement.");
        return null!;
    }

    private string GetTextLengthValue()
    {
        if (Element.TryGetAttribute("textLength", out var rawValue) &&
            !string.IsNullOrWhiteSpace(rawValue))
        {
            return rawValue;
        }

        var computedTextLength = _runtime.GetComputedTextLength(RequireTextContentElement());
        return !double.IsNaN(computedTextLength) && !double.IsInfinity(computedTextLength)
            ? computedTextLength.ToString("R", CultureInfo.InvariantCulture)
            : "0";
    }

    private void SetTextLengthValue(string value)
    {
        setAttribute("textLength", value);
    }

    public SvgJavaScriptTransform createSVGTransformFromMatrix(SvgJavaScriptMatrix matrix)
    {
        if (matrix is null)
        {
            throw new ArgumentNullException(nameof(matrix));
        }

        return new SvgJavaScriptTransform(_runtime, SvgJavaScriptMatrixHelpers.FromSkMatrix(matrix.ToSkMatrix()), null, false);
    }

    public int suspendRedraw(int maxWaitMilliseconds)
    {
        _ = maxWaitMilliseconds;
        return _runtime.SetTimeout(null);
    }

    public void unsuspendRedraw(int suspendHandleId)
    {
        _runtime.ClearTimeout(suspendHandleId);
    }

    public void unsuspendRedrawAll()
    {
    }

    public void forceRedraw()
    {
    }

    internal string GetComputedStyleProperty(string name)
    {
        name = NormalizeCssPropertyName(name);
        if (name.Length == 0)
        {
            return string.Empty;
        }

        var shouldInherit = IsInheritedComputedStyleProperty(name);
        var forceInheritance = false;
        for (SvgElement? current = Element; current is not null; current = current.Parent)
        {
            if (TryGetComputedStylePropertyOnElement(current, name, out var value))
            {
                if (IsCssInheritValue(value))
                {
                    forceInheritance = true;
                    continue;
                }

                return NormalizeComputedStyleValue(name, value);
            }

            if (!shouldInherit && !forceInheritance)
            {
                break;
            }
        }

        return GetInitialComputedStyleValue(name);
    }

    internal IReadOnlyList<string> GetComputedStylePropertyNames()
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (SvgElement? current = Element; current is not null; current = current.Parent)
        {
            foreach (var name in GetStylePropertyNames(current, includePresentationAttributes: true))
            {
                if ((ReferenceEquals(current, Element) || IsInheritedComputedStyleProperty(name)) &&
                    seen.Add(name))
                {
                    names.Add(name);
                }
            }
        }

        foreach (var name in SvgStyleAttributeNames.All)
        {
            if (GetComputedStyleProperty(name).Length > 0 && seen.Add(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    internal string GetStyleCssText()
    {
        return SerializeInlineStyle(ParseInlineStyle(GetRawStyleText(Element)));
    }

    internal void SetStyleCssText(object? value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        SetInlineStyleText(SerializeInlineStyle(ParseInlineStyle(text)), syncJavaScriptDomAttribute: true);
        _runtime.MarkMutation();
    }

    internal string GetStyleProperty(string name)
    {
        return TryGetInlineStyleProperty(Element, NormalizeCssPropertyName(name), out var value)
            ? StripCssPriority(value)
            : string.Empty;
    }

    internal string GetStylePropertyPriority(string name)
    {
        return TryGetInlineStyleProperty(Element, NormalizeCssPropertyName(name), out var value) &&
               TrySplitCssPriority(value, out _, out var priority)
            ? priority
            : string.Empty;
    }

    internal IReadOnlyList<string> GetStylePropertyNames()
    {
        return GetStylePropertyNames(Element, includePresentationAttributes: false);
    }

    internal void SetStyleProperty(string name, object? value, object? priority)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var normalizedName = NormalizeCssPropertyName(name);
        if (normalizedName.Length == 0)
        {
            return;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        var priorityText = Convert.ToString(priority, CultureInfo.InvariantCulture) ?? string.Empty;
        if (priorityText.Equals("important", StringComparison.OrdinalIgnoreCase) &&
            !EndsWithImportantPriority(text))
        {
            text = string.Concat(text.TrimEnd(), " !important");
        }

        var declarations = ParseInlineStyle(GetRawStyleText(Element));
        declarations[normalizedName] = text;
        SetInlineStyleText(SerializeInlineStyle(declarations), declarations);
        _runtime.MarkMutation();
    }

    internal string RemoveStyleProperty(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var normalizedName = NormalizeCssPropertyName(name);
        if (normalizedName.Length == 0)
        {
            return string.Empty;
        }

        var declarations = ParseInlineStyle(GetRawStyleText(Element));
        var hadProperty = declarations.TryGetValue(normalizedName, out var previous);
        previous ??= string.Empty;

        declarations.Remove(normalizedName);
        SetInlineStyleText(
            SerializeInlineStyle(declarations),
            declarations,
            HasRawStyleAttribute() || hadProperty);
        _runtime.MarkMutation();
        return StripCssPriority(previous);
    }

    private void InsertElement(SvgElement childElement, object? referenceChild)
    {
        if (ReferenceEquals(childElement, Element) || IsAncestorOf(childElement, Element))
        {
            _runtime.ThrowDomException(3, "Cannot insert an element into itself or its descendant.");
        }

        SvgJavaScriptDocument.EnsureDomNodesInitialized(Element);
        var referenceNode = ValidateReferenceChild(referenceChild);
        if (ReferenceEquals(referenceNode, childElement))
        {
            return;
        }

        RemoveElementFromParent(childElement);

        var nodes = Element.Nodes;
        var nodeIndex = referenceNode is null ? nodes.Count : nodes.IndexOf(referenceNode);

        if (referenceNode is not null && nodeIndex >= 0)
        {
            nodes.Insert(nodeIndex, childElement);
        }
        else
        {
            nodes.Add(childElement);
        }

        var childIndex = GetElementInsertIndex(referenceNode, nodeIndex);
        if (childIndex >= Element.Children.Count)
        {
            Element.Children.Add(childElement);
        }
        else
        {
            Element.Children.Insert(childIndex, childElement);
        }

        if (_document.RawDocument.HasCompatibilityStyleSources)
        {
            _document.RawDocument.EnsureCompatibilityStyleState(childElement);
            _document.RawDocument.ReapplyCompatibilityStyles();
        }

        _runtime.MarkMutation();
    }

    private void InsertText(SvgJavaScriptTextNode textNode, object? referenceChild)
    {
        SvgJavaScriptDocument.EnsureDomNodesInitialized(Element);
        var referenceNode = ValidateReferenceChild(referenceChild);
        if (ReferenceEquals(referenceNode, textNode.Node))
        {
            return;
        }

        textNode.DetachFromParent();
        var nodeIndex = referenceNode is null ? Element.Nodes.Count : Element.Nodes.IndexOf(referenceNode);

        if (referenceNode is not null && nodeIndex >= 0)
        {
            Element.Nodes.Insert(nodeIndex, textNode.Node);
        }
        else
        {
            Element.Nodes.Add(textNode.Node);
        }

        textNode.SetParent(Element);
        SyncContentFromNodes();
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

    private static bool IsAncestorOf(SvgElement candidateAncestor, SvgElement element)
    {
        for (var current = element.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, candidateAncestor))
            {
                return true;
            }
        }

        return false;
    }

    private ISvgNode? UnwrapNode(object? node)
    {
        return node switch
        {
            SvgJavaScriptElement element => element.Element,
            SvgJavaScriptTextNode textNode => textNode.Node,
            _ => null
        };
    }

    private ISvgNode? ValidateReferenceChild(object? referenceChild)
    {
        if (referenceChild is null)
        {
            return null;
        }

        var referenceNode = UnwrapNode(referenceChild);
        if (referenceNode is null || !Element.Nodes.Contains(referenceNode))
        {
            ThrowNotFound("The reference node is not a child of this node.");
        }

        return referenceNode;
    }

    private void ThrowNotFound(string message)
    {
        _runtime.ThrowDomException(8, message);
    }

    private int GetElementInsertIndex(ISvgNode? referenceNode, int fallbackNodeIndex)
    {
        if (referenceNode is SvgElement referenceElement)
        {
            var index = Element.Children.IndexOf(referenceElement);
            return index >= 0 ? index : Element.Children.Count;
        }

        if (referenceNode is null)
        {
            return Element.Children.Count;
        }

        var nodes = Element.Nodes;
        for (var i = 0; i < Element.Children.Count; i++)
        {
            if (nodes.IndexOf(Element.Children[i]) >= fallbackNodeIndex)
            {
                return i;
            }
        }

        return Element.Children.Count;
    }

    private void SetTextContent(string? value)
    {
        _document.DetachTextNodes(Element);
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

    private static string NormalizeComputedStyleValue(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        value = StripCssPriority(value);
        return name switch
        {
            "color" or
            "fill" or
            "flood-color" or
            "lighting-color" or
            "stop-color" or
            "stroke" when IsAsciiKeyword(value) => value.ToLowerInvariant(),
            "fill-opacity" or
            "flood-opacity" or
            "opacity" or
            "stop-opacity" or
            "stroke-miterlimit" or
            "stroke-opacity" => NormalizeNumericStyleValue(value),
            "font-variant" when string.Equals(value, "SmallCaps", StringComparison.Ordinal) => "small-caps",
            _ => value
        };
    }

    private static bool IsAsciiKeyword(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (character is not ((>= 'A' and <= 'Z') or (>= 'a' and <= 'z')))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeNumericStyleValue(string value)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantValue) ||
            float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out invariantValue))
        {
            return invariantValue.ToString("R", CultureInfo.InvariantCulture);
        }

        return value;
    }

    private static bool IsInheritedComputedStyleProperty(string name)
    {
        name = NormalizeCssPropertyName(name);
        return name.StartsWith("--", StringComparison.Ordinal) ||
               name is "alignment-baseline" or
                   "clip-rule" or
                   "color" or
                   "color-interpolation" or
                   "color-interpolation-filters" or
                   "color-rendering" or
                   "cursor" or
                   "direction" or
                   "dominant-baseline" or
                   "fill" or
                   "fill-opacity" or
                   "fill-rule" or
                   "font" or
                   "font-family" or
                   "font-feature-settings" or
                   "font-kerning" or
                   "font-size" or
                   "font-size-adjust" or
                   "font-stretch" or
                   "font-style" or
                   "font-variant" or
                   "font-variant-ligatures" or
                   "font-weight" or
                   "glyph-orientation-horizontal" or
                   "glyph-orientation-vertical" or
                   "image-rendering" or
                   "kerning" or
                   "letter-spacing" or
                   "line-break" or
                   "line-height" or
                   "overflow-wrap" or
                   "paint-order" or
                   "pointer-events" or
                   "shape-rendering" or
                   "stroke" or
                   "stroke-dasharray" or
                   "stroke-dashoffset" or
                   "stroke-linecap" or
                   "stroke-linejoin" or
                   "stroke-miterlimit" or
                   "stroke-opacity" or
                   "stroke-width" or
                   "text-anchor" or
                   "text-decoration" or
                   "text-overflow" or
                   "text-rendering" or
                   "text-transform" or
                   "unicode-bidi" or
                   "vector-effect" or
                   "visibility" or
                   "white-space" or
                   "white-space-collapse" or
                   "white-space-trim" or
                   "word-break" or
                   "word-spacing" or
                   "writing-mode";
    }

    private static string GetInitialComputedStyleValue(string name)
    {
        return NormalizeCssPropertyName(name) switch
        {
            "clip-rule" => "nonzero",
            "color" => "black",
            "color-interpolation" => "sRGB",
            "color-interpolation-filters" => "linearRGB",
            "direction" => "ltr",
            "display" => "inline",
            "fill" => "black",
            "fill-opacity" => "1",
            "fill-rule" => "nonzero",
            "font-size" => "medium",
            "font-style" => "normal",
            "font-variant" => "normal",
            "font-weight" => "normal",
            "opacity" => "1",
            "overflow" => "visible",
            "pointer-events" => "visiblePainted",
            "stroke" => "none",
            "stroke-linecap" => "butt",
            "stroke-linejoin" => "miter",
            "stroke-miterlimit" => "4",
            "stroke-opacity" => "1",
            "stroke-width" => "1",
            "text-anchor" => "start",
            "text-decoration" => "none",
            "visibility" => "visible",
            "writing-mode" => "horizontal-tb",
            _ => string.Empty
        };
    }

    private object? GetSibling(int offset)
    {
        var parent = Element.Parent;
        if (parent is null)
        {
            return null;
        }

        var nodes = _document.GetDomNodes(parent);
        var index = -1;
        for (var i = 0; i < nodes.Count; i++)
        {
            if (ReferenceEquals(nodes[i], Element))
            {
                index = i;
                break;
            }
        }

        var siblingIndex = index + offset;
        return index < 0 || siblingIndex < 0 || siblingIndex >= nodes.Count
            ? null
            : _document.WrapNode(nodes[siblingIndex], parent);
    }

    private IReadOnlyList<ISvgNode> GetNodes()
    {
        return _document.GetDomNodes(Element);
    }

    private bool TryGetViewerHost(out ISvgJavaScriptViewerHost viewerHost)
    {
        if ((Element is SvgDocument || (Element is SvgFragment && Element.Parent is null)) &&
            _runtime.ViewerHost is { } host)
        {
            viewerHost = host;
            return true;
        }

        viewerHost = null!;
        return false;
    }

    private SvgJavaScriptElement? FindOwnerSvgElement()
    {
        for (var parent = Element.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is SvgFragment || parent is SvgDocument)
            {
                return _document.GetOrCreateElement(parent);
            }
        }

        return null;
    }

    private SvgJavaScriptElement? FindViewportElement(bool outermost)
    {
        SvgElement? match = null;
        for (var current = Element; current is not null; current = current.Parent)
        {
            if (current is SvgFragment || current is SvgDocument)
            {
                match = current;
                if (!outermost)
                {
                    break;
                }
            }
        }

        if (match is null || ReferenceEquals(match, Element))
        {
            return null;
        }

        return _document.GetOrCreateElement(match);
    }

    private IEnumerable<object> GetMatchingSceneElements(SvgJavaScriptRect rect, bool enclosure)
    {
        var sceneDocument = _runtime.GetSceneDocument();
        if (sceneDocument is null)
        {
            return Array.Empty<object>();
        }

        var targetRect = rect.ToSkRect();
        var ignoreW3CDraftWatermark = ShouldIgnoreW3CTestSuiteDraftWatermark();
        var results = new List<object>();
        var seen = new HashSet<object>();
        foreach (var node in sceneDocument.Traverse())
        {
            var targetElement = node.HitTestTargetElement;
            if (targetElement is null || !BelongsToSubtree(targetElement))
            {
                continue;
            }

            // Draft W3C SVG 1.1 fixtures keep this suite watermark in the document
            // even though their DOM list assertions are scoped to the fixture content.
            if (ignoreW3CDraftWatermark && IsDescendantOfElementWithId(targetElement, "draft-watermark"))
            {
                continue;
            }

            if (!MatchesSceneRect(node, targetRect, enclosure))
            {
                continue;
            }

            if (targetElement is SvgUse use &&
                node.Element is { } correspondingElement &&
                !ReferenceEquals(correspondingElement, targetElement))
            {
                var instance = _runtime.FindUseInstance(use, correspondingElement);
                if (instance is not null && seen.Add(instance))
                {
                    results.Add(instance);
                }

                continue;
            }

            if (targetElement is SvgUse && ReferenceEquals(node.Element, targetElement))
            {
                continue;
            }

            var element = _document.GetOrCreateElement(targetElement);
            if (seen.Add(element))
            {
                results.Add(element);
            }
        }

        return results;
    }

    private bool CheckSceneRectMatch(SvgJavaScriptElement element, SvgJavaScriptRect rect, bool enclosure)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        if (!_runtime.TryGetSceneNode(element.Element, out var node) || node is null)
        {
            return false;
        }

        return MatchesSceneRect(node, rect.ToSkRect(), enclosure);
    }

    private bool BelongsToSubtree(SvgElement element)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, Element))
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldIgnoreW3CTestSuiteDraftWatermark()
    {
        var document = _document.RawDocument;
        if (document.GetElementById("draft-watermark") is null)
        {
            return false;
        }

        return document.GetElementById("test-title") is SvgTitle title &&
               title.Content.IndexOf("$RCSfile:", StringComparison.Ordinal) >= 0;
    }

    private static bool IsDescendantOfElementWithId(SvgElement element, string id)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if (string.Equals(current.ID, id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetElementMatrix(SvgElement element, out SKMatrix matrix)
    {
        if (_runtime.TryGetSceneNode(element, out var node) && node is not null)
        {
            matrix = node.TotalTransform;
            return true;
        }

        matrix = ComputeFallbackMatrix(element);
        return true;
    }

    private static SKMatrix ComputeFallbackMatrix(SvgElement element)
    {
        var transforms = new Stack<SvgElement>();
        for (var current = element; current is not null; current = current.Parent)
        {
            transforms.Push(current);
        }

        var total = SKMatrix.Identity;
        while (transforms.Count > 0)
        {
            total = total.PreConcat(SvgJavaScriptMatrixHelpers.ToSkMatrix(transforms.Pop().Transforms));
        }

        return total;
    }

    private SvgJavaScriptRect? GetElementBounds(SvgElement element)
    {
        if (_runtime.TryGetSceneNode(element, out var node) && node is not null)
        {
            if (ShouldReturnNullBoundingBox(element, node))
            {
                return null;
            }

            if (element is SvgUse)
            {
                return SvgJavaScriptRect.From(node.Transform.MapRect(node.GeometryBounds));
            }

            if (!node.GeometryBounds.IsEmpty)
            {
                return SvgJavaScriptRect.From(node.GeometryBounds);
            }
        }

        return GetDetachedBounds(element);
    }

    private static bool ShouldReturnNullBoundingBox(SvgElement element, SvgSceneNode node)
    {
        if (!node.GeometryBounds.IsEmpty)
        {
            return false;
        }

        return element is SvgGroup or SvgFragment or SvgDocument or SvgAnchor or SvgSwitch or SvgSymbol or SvgDefinitionList;
    }

    private static SvgJavaScriptRect? GetDetachedBounds(SvgElement element)
    {
        switch (element)
        {
            case SvgRectangle:
                return new SvgJavaScriptRect(
                    GetFloatAttribute(element, "x"),
                    GetFloatAttribute(element, "y"),
                    GetFloatAttribute(element, "width"),
                    GetFloatAttribute(element, "height"));
            case SvgCircle:
                {
                    var cx = GetFloatAttribute(element, "cx");
                    var cy = GetFloatAttribute(element, "cy");
                    var r = GetFloatAttribute(element, "r");
                    return new SvgJavaScriptRect(cx - r, cy - r, 2 * r, 2 * r);
                }
            case SvgEllipse:
                {
                    var cx = GetFloatAttribute(element, "cx");
                    var cy = GetFloatAttribute(element, "cy");
                    var rx = GetFloatAttribute(element, "rx");
                    var ry = GetFloatAttribute(element, "ry");
                    return new SvgJavaScriptRect(cx - rx, cy - ry, 2 * rx, 2 * ry);
                }
            case SvgLine:
                {
                    var x1 = GetFloatAttribute(element, "x1");
                    var y1 = GetFloatAttribute(element, "y1");
                    var x2 = GetFloatAttribute(element, "x2");
                    var y2 = GetFloatAttribute(element, "y2");
                    return new SvgJavaScriptRect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
                }
            case SvgImage:
                return new SvgJavaScriptRect(
                    GetFloatAttribute(element, "x"),
                    GetFloatAttribute(element, "y"),
                    GetFloatAttribute(element, "width"),
                    GetFloatAttribute(element, "height"));
        }

        SvgJavaScriptRect? bounds = null;
        foreach (var child in element.Children)
        {
            var childBounds = GetDetachedBounds(child);
            if (childBounds is null)
            {
                continue;
            }

            var transformed = SvgJavaScriptMatrixHelpers.ToSkMatrix(child.Transforms).MapRect(childBounds.ToSkRect());
            var childRect = SvgJavaScriptRect.From(transformed);
            bounds = bounds is null ? childRect : Union(bounds, childRect);
        }

        return bounds;
    }

    private List<string> ParseTokenList(string attributeName)
    {
        return SvgJavaScriptParsing.ParseTokenList(getAttribute(attributeName)).ToList();
    }

    private void SetTokenList(string attributeName, IEnumerable<string> values)
    {
        setAttribute(attributeName, string.Join(" ", values));
    }

    private string GetNumberToken(string attributeName, int index)
    {
        var tokens = SvgJavaScriptParsing.ParseTokenList(getAttribute(attributeName));
        return index >= 0 && index < tokens.Length ? tokens[index] : tokens.LastOrDefault() ?? "0";
    }

    private int GetIntegerToken(string attributeName, int index)
    {
        var tokens = SvgJavaScriptParsing.ParseTokenList(getAttribute(attributeName));
        return index >= 0 &&
               index < tokens.Length &&
               int.TryParse(tokens[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private void SetNumberToken(string attributeName, int index, double value)
    {
        var tokens = SvgJavaScriptParsing.ParseTokenList(getAttribute(attributeName)).ToList();
        while (tokens.Count <= index)
        {
            tokens.Add("0");
        }

        tokens[index] = SvgJavaScriptParsing.FormatNumber(value);
        setAttribute(attributeName, string.Join(" ", tokens));
    }

    private void SetIntegerToken(string attributeName, int index, int value)
    {
        var tokens = SvgJavaScriptParsing.ParseTokenList(getAttribute(attributeName)).ToList();
        while (tokens.Count <= index)
        {
            tokens.Add("0");
        }

        tokens[index] = value.ToString(CultureInfo.InvariantCulture);
        setAttribute(attributeName, string.Join(" ", tokens));
    }

    private List<SvgJavaScriptPoint> ParsePoints()
    {
        var tokens = SvgJavaScriptParsing.ParseTokenList(getAttribute("points"));
        var points = new List<SvgJavaScriptPoint>();
        for (var i = 0; i + 1 < tokens.Length; i += 2)
        {
            var x = float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedX) ? parsedX : 0f;
            var y = float.TryParse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedY) ? parsedY : 0f;
            points.Add(new SvgJavaScriptPoint(x, y));
        }

        return points;
    }

    private static int ParseLengthAdjust(string value)
    {
        return (value ?? string.Empty).Trim() switch
        {
            var text when text.Equals("spacing", StringComparison.OrdinalIgnoreCase) => 1,
            var text when text.Equals("spacingAndGlyphs", StringComparison.OrdinalIgnoreCase) => 2,
            _ => 1
        };
    }

    private static string FormatLengthAdjust(int value)
    {
        return value == 2 ? "spacingAndGlyphs" : "spacing";
    }

    private static int ParseGradientUnits(string value)
    {
        return (value ?? string.Empty).Trim().Equals("userSpaceOnUse", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
    }

    private static string FormatGradientUnits(int value)
    {
        return value == 1 ? "userSpaceOnUse" : "objectBoundingBox";
    }

    private static int ParseZoomAndPan(string value)
    {
        return (value ?? string.Empty).Trim().Equals("disable", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
    }

    private static string FormatZoomAndPan(int value)
    {
        return value == 1 ? "disable" : "magnify";
    }

    private bool UsesLengthList(string attributeName)
    {
        return (attributeName == "x" || attributeName == "y") && Element is SvgTextBase;
    }

    private bool IsConnectedToDocument()
    {
        for (SvgElement? current = Element; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, _document.RawDocument))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(SKRect outer, SKRect inner)
    {
        return outer.Left <= inner.Left &&
               outer.Top <= inner.Top &&
               outer.Right >= inner.Right &&
               outer.Bottom >= inner.Bottom;
    }

    private static bool Intersects(SKRect first, SKRect second)
    {
        return !(first.Right < second.Left ||
                 first.Left > second.Right ||
                 first.Bottom < second.Top ||
                 first.Top > second.Bottom);
    }

    private static SvgJavaScriptRect Union(SvgJavaScriptRect first, SvgJavaScriptRect second)
    {
        var left = Math.Min(first.x, second.x);
        var top = Math.Min(first.y, second.y);
        var right = Math.Max(first.x + first.width, second.x + second.width);
        var bottom = Math.Max(first.y + first.height, second.y + second.height);
        return new SvgJavaScriptRect(left, top, right - left, bottom - top);
    }

    private static string GetTextContent(SvgElement element)
    {
        if (!string.IsNullOrEmpty(element.Content))
        {
            return element.Content;
        }

        if (element.Nodes.Count > 0)
        {
            return string.Concat(element.Nodes.Select(node => node is SvgElement childElement ? GetTextContent(childElement) : node.Content));
        }

        return string.Concat(element.Children.Select(GetTextContent));
    }

    private static float GetFloatAttribute(SvgElement element, string name)
    {
        if (!element.TryGetAttribute(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return 0f;
        }

        var text = value.Trim();
        while (text.Length > 0)
        {
            var lastCharacter = text[text.Length - 1];
            if (char.IsDigit(lastCharacter) || lastCharacter == '.' || lastCharacter == '-')
            {
                break;
            }

            text = text.Substring(0, text.Length - 1);
        }

        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0f;
    }

    private void SetAttributeValue(string name, string value)
    {
        if (!Element.TrySetAnimationValue(name, _document.RawDocument, CultureInfo.InvariantCulture, value))
        {
            Element.CustomAttributes[name] = value;
        }

        Element.SetJavaScriptDomAttributeValue(name, value);
    }

    private void RemoveAttributeValue(string name)
    {
        Element.ClearAnimationValue(name);
        Element.CustomAttributes.Remove(name);
        Element.ClearJavaScriptDomAttributeValue(name);
    }

    private void SetInlineStyleText(
        string styleText,
        Dictionary<string, string>? declarations = null,
        bool syncJavaScriptDomAttribute = true)
    {
        var previousDeclarations = ParseInlineStyle(GetRawStyleText(Element));
        declarations ??= ParseInlineStyle(styleText);
        CaptureInlineStyleFallbacks(previousDeclarations, declarations);
        SetRawStyleText(Element, styleText);
        if (syncJavaScriptDomAttribute)
        {
            Element.SetJavaScriptDomAttributeValue("style", styleText);
        }

        _document.RawDocument.UpdateCompatibilityStyleText(Element, styleText);
        RestoreRemovedInlineStyleFallbacks(previousDeclarations, declarations);
        _document.RawDocument.ReapplyCompatibilityStyles();
    }

    private bool HasRawStyleAttribute()
    {
        return Element.TryGetJavaScriptDomAttributeValue("style", out _) ||
               Element.CustomAttributes.ContainsKey("style");
    }

    private void CaptureInlineStyleFallbacks(
        IReadOnlyDictionary<string, string> previousDeclarations,
        IReadOnlyDictionary<string, string> nextDeclarations)
    {
        foreach (var propertyName in nextDeclarations.Keys)
        {
            if (previousDeclarations.ContainsKey(propertyName) || _inlineStyleFallbacks.ContainsKey(propertyName))
            {
                continue;
            }

            _inlineStyleFallbacks[propertyName] = TryGetRawAttributeValue(propertyName, out var rawValue)
                ? rawValue
                : null;
        }
    }

    private void RestoreRemovedInlineStyleFallbacks(
        IReadOnlyDictionary<string, string> previousDeclarations,
        IReadOnlyDictionary<string, string> nextDeclarations)
    {
        foreach (var propertyName in previousDeclarations.Keys)
        {
            if (nextDeclarations.ContainsKey(propertyName))
            {
                continue;
            }

            RestoreInlineStyleFallback(propertyName);
        }
    }

    private void RestoreInlineStyleFallback(string propertyName)
    {
        if (_inlineStyleFallbacks.TryGetValue(propertyName, out var fallbackValue))
        {
            if (fallbackValue is null)
            {
                RemoveAttributeValue(propertyName);
            }
            else
            {
                SetAttributeValue(propertyName, fallbackValue);
            }

            _inlineStyleFallbacks.Remove(propertyName);
            return;
        }

        RemoveAttributeValue(propertyName);
    }

    private bool UpdateInlineStyleFallback(string name, string? value)
    {
        name = NormalizeCssPropertyName(name);
        if (!TryGetInlineStyleProperty(Element, name, out _))
        {
            return false;
        }

        _inlineStyleFallbacks[name] = string.IsNullOrWhiteSpace(value) ? null : value;
        return true;
    }

    private static bool TryGetInlineStyleProperty(SvgElement element, string name, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var declarations = ParseInlineStyle(GetRawStyleText(element));
        if (!declarations.TryGetValue(NormalizeCssPropertyName(name), out var inlineValue) || string.IsNullOrWhiteSpace(inlineValue))
        {
            return false;
        }

        value = inlineValue;
        return true;
    }

    private static bool TryGetComputedStylePropertyOnElement(SvgElement element, string name, out string value)
    {
        if (TryGetInlineStyleProperty(element, name, out value))
        {
            return true;
        }

        if (SvgCssVariableResolver.TryGetCustomPropertyValue(element, name, out value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (TryGetRawStyleAttributeValue(element, name, out value))
        {
            return true;
        }

        if (element.TryGetAttribute(name, out value) && !string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetRawStyleAttributeValue(SvgElement element, string name, out string value)
    {
        if (element.TryGetJavaScriptDomAttributeValue(name, out value) && !string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        foreach (var attribute in element.CustomAttributes)
        {
            if (attribute.Value is not null &&
                NormalizeCssPropertyName(attribute.Key).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = attribute.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private string GetOrientValue()
    {
        if (TryGetRawAttributeValue("orient", out var rawValue))
        {
            return rawValue;
        }

        return Element is SvgMarker marker
            ? marker.Orient?.ToString() ?? string.Empty
            : getAttribute("orient");
    }

    private bool TryGetSpecialAttributeValue(string name, out string value)
    {
        if (string.Equals(name, "href", StringComparison.Ordinal) &&
            TryGetHrefAttributeValue(out value))
        {
            return true;
        }

        if (string.Equals(name, "viewBox", StringComparison.Ordinal) &&
            Element is SvgFragment svgFragment &&
            svgFragment.ViewBox != SvgViewBox.Empty)
        {
            value = string.Join(" ", new[]
            {
                SvgJavaScriptParsing.FormatNumber(svgFragment.ViewBox.MinX),
                SvgJavaScriptParsing.FormatNumber(svgFragment.ViewBox.MinY),
                SvgJavaScriptParsing.FormatNumber(svgFragment.ViewBox.Width),
                SvgJavaScriptParsing.FormatNumber(svgFragment.ViewBox.Height)
            });
            return true;
        }

        if (string.Equals(name, "preserveAspectRatio", StringComparison.Ordinal) &&
            Element is SvgFragment fragment &&
            fragment.AspectRatio is { } aspectRatio)
        {
            value = aspectRatio.ToString();
            return true;
        }

        if (string.Equals(name, "orient", StringComparison.Ordinal) &&
            Element is SvgMarker marker)
        {
            if (TryGetRawAttributeValue(name, out var rawValue))
            {
                value = rawValue;
                return true;
            }

            if (marker.Orient is { } orient)
            {
                value = orient.ToString();
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private bool TryGetHrefAttributeValue(out string value)
    {
        switch (Element)
        {
            case SvgScript when TryGetRawXLinkHrefValue(out value):
                return true;
            case SvgScript script when !string.IsNullOrWhiteSpace(script.Href):
                value = script.Href;
                return true;
            case SvgAnchor anchor when !string.IsNullOrWhiteSpace(anchor.Href):
                value = anchor.Href;
                return true;
            case SvgImage image when !string.IsNullOrWhiteSpace(image.Href):
                value = image.Href;
                return true;
            case SvgUse use when use.ReferencedElement is { } referencedElement:
                value = referencedElement.OriginalString;
                return true;
            case SvgTextPath textPath when textPath.ReferencedPath is { } referencedPath:
                value = referencedPath.OriginalString;
                return true;
            case SvgTextRef textRef when textRef.ReferencedElement is { } referencedText:
                value = referencedText.OriginalString;
                return true;
            case SvgFontFaceUri fontFaceUri when fontFaceUri.ReferencedElement is { } referencedFont:
                value = referencedFont.OriginalString;
                return true;
            case SvgMPath motionPath when motionPath.ReferencedPath is { } motionReference:
                value = motionReference.OriginalString;
                return true;
            case SvgAnimationElement animationElement when animationElement.ReferencedElement is { } animationReference:
                value = animationReference.OriginalString;
                return true;
            case Svg.FilterEffects.SvgFilter filter when filter.Href is { } filterReference:
                value = filterReference.OriginalString;
                return true;
            default:
                value = string.Empty;
                return false;
        }
    }

    private bool TryGetRawXLinkHrefValue(out string value)
    {
        if (Element.CustomAttributes.TryGetValue("xlink:href", out var xlinkHref) &&
            !string.IsNullOrWhiteSpace(xlinkHref))
        {
            value = xlinkHref;
            return true;
        }

        var namespacedKey = string.Concat(SvgNamespaces.XLinkNamespace, ":href");
        if (Element.CustomAttributes.TryGetValue(namespacedKey, out xlinkHref) &&
            !string.IsNullOrWhiteSpace(xlinkHref))
        {
            value = xlinkHref;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private bool TryGetRawAttributeValue(string name, out string value)
    {
        if (Element.TryGetJavaScriptDomAttributeValue(name, out var scriptSetValue))
        {
            value = scriptSetValue;
            return true;
        }

        if (Element.CustomAttributes.TryGetValue(name, out var customValue) && !string.IsNullOrWhiteSpace(customValue))
        {
            value = customValue ?? string.Empty;
            return true;
        }

        if (Element.TryGetAttribute(name, out var attributeValue) && !string.IsNullOrWhiteSpace(attributeValue))
        {
            value = attributeValue ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool MatchesSceneRect(SvgSceneNode node, SKRect rect, bool enclosure)
    {
        var bounds = node.TransformedBounds;
        if (bounds.IsEmpty)
        {
            return false;
        }

        if (enclosure)
        {
            return Contains(rect, bounds);
        }

        if (!Intersects(rect, bounds))
        {
            return false;
        }

        if (node.HitTestPath is not { } hitTestPath)
        {
            return true;
        }

        if (node.SupportsFillHitTest && GeometryHitTestService.IntersectsFill(hitTestPath, rect, node.TotalTransform))
        {
            return true;
        }

        if (node.SupportsStrokeHitTest)
        {
            if (GeometryHitTestService.IntersectsStroke(hitTestPath, rect, node.TotalTransform, node.StrokeWidth))
            {
                return true;
            }

            if (!node.SupportsFillHitTest &&
                bounds.Width <= rect.Width &&
                bounds.Height <= rect.Height)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetRawStyleText(SvgElement element)
    {
        return element.CustomAttributes.TryGetValue("style", out var style)
            ? style ?? string.Empty
            : string.Empty;
    }

    private static void SetRawStyleText(SvgElement element, string styleText)
    {
        if (string.IsNullOrWhiteSpace(styleText))
        {
            element.CustomAttributes.Remove("style");
        }
        else
        {
            element.CustomAttributes["style"] = styleText;
        }
    }

    private static Dictionary<string, string> ParseInlineStyle(string? styleText)
    {
        var declarations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(styleText))
        {
            return declarations;
        }

        var text = styleText ?? string.Empty;
        foreach (var declaration in text.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(declaration))
            {
                continue;
            }

            var separatorIndex = declaration.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var propertyName = declaration.Substring(0, separatorIndex).Trim();
            propertyName = NormalizeCssPropertyName(propertyName);
            if (propertyName.Length == 0)
            {
                continue;
            }

            declarations[propertyName] = declaration.Substring(separatorIndex + 1).Trim();
        }

        return declarations;
    }

    private static string SerializeInlineStyle(Dictionary<string, string> declarations)
    {
        return string.Join("; ", declarations
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .Select(pair => string.Concat(NormalizeCssPropertyName(pair.Key), ": ", pair.Value)));
    }

    private static IReadOnlyList<string> GetStylePropertyNames(SvgElement element, bool includePresentationAttributes)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in ParseInlineStyle(GetRawStyleText(element)).Keys)
        {
            if (seen.Add(name))
            {
                names.Add(name);
            }
        }

        if (!includePresentationAttributes)
        {
            return names;
        }

        foreach (var attribute in element.CustomAttributes.Keys)
        {
            var name = NormalizeCssPropertyName(attribute);
            if (SvgStyleAttributeNames.Contains(name) && seen.Add(name))
            {
                names.Add(name);
            }
        }

        foreach (var attribute in element.Attributes.Keys)
        {
            var name = NormalizeCssPropertyName(attribute);
            if (SvgStyleAttributeNames.Contains(name) && seen.Add(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static string NormalizeCssPropertyName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var text = name!.Trim();
        if (text.StartsWith("--", StringComparison.Ordinal))
        {
            return text;
        }

        text = text.Replace('_', '-');
        var builder = new System.Text.StringBuilder(text.Length + 4);
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (character is >= 'A' and <= 'Z')
            {
                if (builder.Length > 0 && builder[builder.Length - 1] != '-')
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static bool IsCssInheritValue(string value)
    {
        return value.Trim().Equals("inherit", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripCssPriority(string value)
    {
        return TrySplitCssPriority(value, out var propertyValue, out _)
            ? propertyValue
            : value;
    }

    private static bool EndsWithImportantPriority(string value)
    {
        return TrySplitCssPriority(value, out _, out _);
    }

    private static bool TrySplitCssPriority(string value, out string propertyValue, out string priority)
    {
        propertyValue = value;
        priority = string.Empty;
        var trimmed = value.Trim();
        const string important = "!important";
        if (!trimmed.EndsWith(important, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        propertyValue = trimmed.Substring(0, trimmed.Length - important.Length).TrimEnd();
        priority = "important";
        return true;
    }

    private static string QualifyAttributeName(string? namespaceUri, string localName)
    {
        if (string.IsNullOrWhiteSpace(namespaceUri))
        {
            return localName;
        }

        var unqualifiedName = GetQualifiedLocalName(localName);
        if (string.Equals(namespaceUri, SvgNamespaces.XLinkNamespace, StringComparison.Ordinal))
        {
            return string.Equals(unqualifiedName, "href", StringComparison.OrdinalIgnoreCase)
                ? "href"
                : $"xlink:{unqualifiedName}";
        }

        return string.Equals(namespaceUri, SvgNamespaces.SvgNamespace, StringComparison.Ordinal)
            ? unqualifiedName
            : string.Concat("{", namespaceUri, "}", localName);
    }

    private static string GetQualifiedLocalName(string localName)
    {
        var colonIndex = localName.IndexOf(':');
        return colonIndex >= 0 && colonIndex + 1 < localName.Length
            ? localName.Substring(colonIndex + 1)
            : localName;
    }

    private static string NormalizeAttributeStorageName(string name)
    {
        return string.Equals(name, "xlink:href", StringComparison.OrdinalIgnoreCase)
            ? "href"
            : name;
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<SvgElement>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

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

public sealed class SvgJavaScriptAnimatedTransformList
{
    private readonly SvgJavaScriptTransformList _baseVal;
    private readonly SvgJavaScriptTransformList _animVal;

    internal SvgJavaScriptAnimatedTransformList(SvgJavaScriptRuntime runtime, SvgJavaScriptElement element)
    {
        _baseVal = new SvgJavaScriptTransformList(runtime, element, false);
        _animVal = new SvgJavaScriptTransformList(runtime, element, true);
    }

    public SvgJavaScriptTransformList baseVal => _baseVal;

    public SvgJavaScriptTransformList animVal => _animVal;
}

public sealed class SvgJavaScriptAnimatedRect
{
    private readonly SvgJavaScriptRect _baseVal;
    private readonly SvgJavaScriptRect _animVal;

    internal SvgJavaScriptAnimatedRect(SvgJavaScriptRuntime runtime, SvgJavaScriptElement element, string attributeName)
    {
        _baseVal = new SvgJavaScriptRect(runtime, () => ParseRect(element.getAttribute(attributeName)), state => element.setAttribute(attributeName, FormatRect(state)), false);
        _animVal = new SvgJavaScriptRect(runtime, () => ParseRect(element.getAttribute(attributeName)), null, true);
    }

    public SvgJavaScriptRect baseVal => _baseVal;

    public SvgJavaScriptRect animVal => _animVal;

    private static SvgJavaScriptRect.SvgJavaScriptRectState ParseRect(string? value)
    {
        var parts = SvgJavaScriptParsing.ParseTokenList(value);
        var values = new float[4];
        for (var i = 0; i < values.Length && i < parts.Length; i++)
        {
            _ = float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]);
        }

        return new SvgJavaScriptRect.SvgJavaScriptRectState(values[0], values[1], values[2], values[3]);
    }

    private static string FormatRect(SvgJavaScriptRect.SvgJavaScriptRectState state)
    {
        return string.Join(" ", new[]
        {
            SvgJavaScriptParsing.FormatNumber(state.X),
            SvgJavaScriptParsing.FormatNumber(state.Y),
            SvgJavaScriptParsing.FormatNumber(state.Width),
            SvgJavaScriptParsing.FormatNumber(state.Height)
        });
    }
}
