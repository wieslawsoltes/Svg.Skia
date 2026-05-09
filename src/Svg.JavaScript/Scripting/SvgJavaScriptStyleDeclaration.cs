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

    public string opacity
    {
        get => getPropertyValue("opacity");
        set => setProperty("opacity", value);
    }

    public string getPropertyValue(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? string.Empty : _element.GetStyleProperty(name);
    }

    public void setProperty(string name, object? value)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _element.SetStyleProperty(name, value);
        }
    }

    public string removeProperty(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return _element.RemoveStyleProperty(name);
        }

        return string.Empty;
    }
}
