using System.Collections.Generic;

#nullable enable

namespace Svg;

public abstract partial class SvgElement
{
    private Dictionary<string, string>? _javaScriptDomAttributeValues;

    internal bool TryGetJavaScriptDomAttributeValue(string name, out string value)
    {
        if (_javaScriptDomAttributeValues is { } &&
            _javaScriptDomAttributeValues.TryGetValue(name, out var storedValue))
        {
            value = storedValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    internal void SetJavaScriptDomAttributeValue(string name, string value)
    {
        _javaScriptDomAttributeValues = _javaScriptDomAttributeValues ?? new Dictionary<string, string>();
        _javaScriptDomAttributeValues[name] = value;
    }

    internal void CopyJavaScriptDomAttributeValuesTo(SvgElement target)
    {
        if (_javaScriptDomAttributeValues is null || _javaScriptDomAttributeValues.Count == 0)
        {
            return;
        }

        target._javaScriptDomAttributeValues = new Dictionary<string, string>(_javaScriptDomAttributeValues);
    }

    internal void ClearJavaScriptDomAttributeValue(string name)
    {
        if (_javaScriptDomAttributeValues is null)
        {
            return;
        }

        _javaScriptDomAttributeValues.Remove(name);
    }

    partial void CopyCustomStateTo(SvgElement target)
    {
        CopyJavaScriptDomAttributeValuesTo(target);
    }
}
