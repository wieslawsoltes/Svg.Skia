namespace Svg.JavaScript;

public sealed class SvgJavaScriptStyleDeclaration
{
    private readonly SvgJavaScriptElement _element;

    internal SvgJavaScriptStyleDeclaration(SvgJavaScriptElement element)
    {
        _element = element;
    }

    public string display
    {
        get => getPropertyValue("display");
        set => setProperty("display", value);
    }

    public string visibility
    {
        get => getPropertyValue("visibility");
        set => setProperty("visibility", value);
    }

    public string fill
    {
        get => getPropertyValue("fill");
        set => setProperty("fill", value);
    }

    public string stroke
    {
        get => getPropertyValue("stroke");
        set => setProperty("stroke", value);
    }

    public string strokeWidth
    {
        get => getPropertyValue("stroke-width");
        set => setProperty("stroke-width", value);
    }

    public string opacity
    {
        get => getPropertyValue("opacity");
        set => setProperty("opacity", value);
    }

    public string fillOpacity
    {
        get => getPropertyValue("fill-opacity");
        set => setProperty("fill-opacity", value);
    }

    public string strokeOpacity
    {
        get => getPropertyValue("stroke-opacity");
        set => setProperty("stroke-opacity", value);
    }

    public string color
    {
        get => getPropertyValue("color");
        set => setProperty("color", value);
    }

    public string fontSize
    {
        get => getPropertyValue("font-size");
        set => setProperty("font-size", value);
    }

    public string fontFamily
    {
        get => getPropertyValue("font-family");
        set => setProperty("font-family", value);
    }

    public string pointerEvents
    {
        get => getPropertyValue("pointer-events");
        set => setProperty("pointer-events", value);
    }

    public string textDecoration
    {
        get => getPropertyValue("text-decoration");
        set => setProperty("text-decoration", value);
    }

    public string cssText
    {
        get => _element.GetStyleCssText();
        set => _element.SetStyleCssText(value);
    }

    public int length => _element.GetStylePropertyNames().Count;

    public string this[int index] => item(index);

    public string getPropertyValue(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? string.Empty : _element.GetStyleProperty(name);
    }

    public string getPropertyPriority(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? string.Empty : _element.GetStylePropertyPriority(name);
    }

    public void setProperty(string name, object? value)
    {
        setProperty(name, value, null);
    }

    public void setProperty(string name, object? value, object? priority)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        _element.SetStyleProperty(name, value, priority);
    }

    public string removeProperty(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return _element.RemoveStyleProperty(name);
        }

        return string.Empty;
    }

    public string item(int index)
    {
        var names = _element.GetStylePropertyNames();
        return index >= 0 && index < names.Count ? names[index] : string.Empty;
    }
}
