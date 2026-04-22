using System;
using System.Globalization;

namespace Svg.JavaScript;

public sealed class SvgJavaScriptAnimatedString
{
    private readonly SvgJavaScriptElement _element;
    private readonly string _attributeName;

    internal SvgJavaScriptAnimatedString(SvgJavaScriptElement element, string attributeName)
    {
        _element = element;
        _attributeName = attributeName;
    }

    public string baseVal
    {
        get => _element.getAttribute(_attributeName);
        set => _element.setAttribute(_attributeName, value);
    }

    public string animVal => baseVal;
}

public sealed class SvgJavaScriptAnimatedLength
{
    private readonly SvgJavaScriptElement _element;
    private readonly string _attributeName;
    private readonly SvgJavaScriptLength _baseVal;

    internal SvgJavaScriptAnimatedLength(SvgJavaScriptElement element, string attributeName)
    {
        _element = element;
        _attributeName = attributeName;
        _baseVal = new SvgJavaScriptLength(element, attributeName);
    }

    public SvgJavaScriptLength baseVal => _baseVal;

    public SvgJavaScriptLength animVal => _baseVal;

    internal string RawValue
    {
        get => _element.getAttribute(_attributeName);
        set => _element.setAttribute(_attributeName, value);
    }
}

public sealed class SvgJavaScriptLength
{
    private readonly SvgJavaScriptElement _element;
    private readonly string _attributeName;

    internal SvgJavaScriptLength(SvgJavaScriptElement element, string attributeName)
    {
        _element = element;
        _attributeName = attributeName;
    }

    public string valueAsString
    {
        get => _element.getAttribute(_attributeName);
        set => _element.setAttribute(_attributeName, value);
    }

    public double value
    {
        get => ParseNumber(valueAsString);
        set => _element.setAttribute(_attributeName, value.ToString(CultureInfo.InvariantCulture));
    }

    public double valueInSpecifiedUnits
    {
        get => ParseNumber(valueAsString);
        set => _element.setAttribute(_attributeName, value.ToString(CultureInfo.InvariantCulture));
    }

    private static double ParseNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0d;
        }

        var text = value.Trim();
        while (text.Length > 0 && !char.IsDigit(text[text.Length - 1]) && text[text.Length - 1] != '.')
        {
            text = text.Substring(0, text.Length - 1);
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0d;
    }
}
