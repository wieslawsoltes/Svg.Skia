namespace Svg.JavaScript;

public sealed class SvgJavaScriptWindow
{
    private readonly SvgJavaScriptRuntime _runtime;
    private readonly SvgJavaScriptDocument _document;

    internal SvgJavaScriptWindow(SvgJavaScriptRuntime runtime, SvgJavaScriptDocument document)
    {
        _runtime = runtime;
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

    public void alert(object? message)
    {
        _ = message;
    }

    public int setTimeout(object? handler)
    {
        return setTimeout(handler, null);
    }

    public int setTimeout(object? handler, object? delay)
    {
        return _runtime.SetTimeout(handler, delay);
    }

    public void clearTimeout(int id)
    {
        _runtime.ClearTimeout(id);
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
        return string.IsNullOrWhiteSpace(name) ? string.Empty : _element.GetComputedStyleProperty(name);
    }
}
