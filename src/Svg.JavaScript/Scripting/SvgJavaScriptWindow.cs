using System.Collections.Generic;
using System.Linq;

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
    private readonly IReadOnlyList<string> _propertyNames;
    private readonly SvgJavaScriptElement _element;

    internal SvgJavaScriptComputedStyle(SvgJavaScriptElement element)
    {
        _element = element;
        _propertyNames = element.GetComputedStylePropertyNames();
    }

    public string display => getPropertyValue("display");

    public string visibility => getPropertyValue("visibility");

    public string fill => getPropertyValue("fill");

    public string stroke => getPropertyValue("stroke");

    public string strokeWidth => getPropertyValue("stroke-width");

    public string opacity => getPropertyValue("opacity");

    public string fillOpacity => getPropertyValue("fill-opacity");

    public string strokeOpacity => getPropertyValue("stroke-opacity");

    public string color => getPropertyValue("color");

    public string fontSize => getPropertyValue("font-size");

    public string fontFamily => getPropertyValue("font-family");

    public string pointerEvents => getPropertyValue("pointer-events");

    public string textDecoration => getPropertyValue("text-decoration");

    public int length => _propertyNames.Count;

    public string cssText => string.Join("; ", _propertyNames
        .Select(name => new KeyValuePair<string, string>(name, getPropertyValue(name)))
        .Where(pair => pair.Value.Length > 0)
        .Select(pair => string.Concat(pair.Key, ": ", pair.Value)));

    public string this[int index] => item(index);

    public string getPropertyValue(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? string.Empty : _element.GetComputedStyleProperty(name);
    }

    public string getPropertyPriority(string name)
    {
        _ = name;
        return string.Empty;
    }

    public string item(int index)
    {
        return index >= 0 && index < _propertyNames.Count ? _propertyNames[index] : string.Empty;
    }
}
