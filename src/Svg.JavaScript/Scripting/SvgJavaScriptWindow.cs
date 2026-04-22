namespace Svg.JavaScript;

public sealed class SvgJavaScriptWindow
{
    private readonly SvgJavaScriptDocument _document;

    internal SvgJavaScriptWindow(SvgJavaScriptDocument document)
    {
        _document = document;
    }

    public SvgJavaScriptDocument document => _document;

    public SvgJavaScriptComputedStyle getComputedStyle(SvgJavaScriptElement element)
    {
        return getComputedStyle(element, null);
    }

    public SvgJavaScriptComputedStyle getComputedStyle(SvgJavaScriptElement element, object? pseudoElement)
    {
        _ = pseudoElement;
        return new SvgJavaScriptComputedStyle(element);
    }
}

public sealed class SvgJavaScriptComputedStyle
{
    private readonly SvgJavaScriptElement _element;

    internal SvgJavaScriptComputedStyle(SvgJavaScriptElement element)
    {
        _element = element;
    }

    public string getPropertyValue(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? string.Empty : _element.getAttribute(name);
    }
}
