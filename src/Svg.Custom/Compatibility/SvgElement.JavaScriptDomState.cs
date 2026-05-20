using System.Collections.Generic;

#nullable enable

namespace Svg;

public abstract partial class SvgElement
{
    private Dictionary<string, string>? _javaScriptDomAttributeValues;
    private Dictionary<string, string>? _compatibilityHrefAttributeValues;
    private bool _hasCompatibilityHrefAttributeValueAfterParse;
    private object? _compatibilityHrefAttributeValueAfterParse;

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

    internal void SetCompatibilityHrefAttributeValue(string namespaceName, string value)
    {
        namespaceName ??= string.Empty;
        _compatibilityHrefAttributeValues ??= new Dictionary<string, string>();
        _compatibilityHrefAttributeValues[namespaceName] = value;
    }

    internal bool TryGetCompatibilityHrefAttributeValue(string namespaceName, out string value)
    {
        namespaceName ??= string.Empty;
        if (_compatibilityHrefAttributeValues is { } &&
            _compatibilityHrefAttributeValues.TryGetValue(namespaceName, out var storedValue))
        {
            value = storedValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    internal bool HasCompatibilityHrefAttributeValues()
    {
        return _compatibilityHrefAttributeValues is { Count: > 0 };
    }

    internal void SetCompatibilityHrefAttributeValueAfterParse(object? value)
    {
        _hasCompatibilityHrefAttributeValueAfterParse = true;
        _compatibilityHrefAttributeValueAfterParse = value;
    }

    internal bool TryGetCompatibilityHrefAttributeValueAfterParse(out object? value)
    {
        if (_hasCompatibilityHrefAttributeValueAfterParse)
        {
            value = _compatibilityHrefAttributeValueAfterParse;
            return true;
        }

        value = null;
        return false;
    }

    partial void CopyCustomStateTo(SvgElement target)
    {
        CopyJavaScriptDomAttributeValuesTo(target);

        if (_compatibilityHrefAttributeValues is null || _compatibilityHrefAttributeValues.Count == 0)
        {
            if (_hasCompatibilityHrefAttributeValueAfterParse)
            {
                target._hasCompatibilityHrefAttributeValueAfterParse = true;
                target._compatibilityHrefAttributeValueAfterParse = _compatibilityHrefAttributeValueAfterParse;
            }

            return;
        }

        target._compatibilityHrefAttributeValues = new Dictionary<string, string>(_compatibilityHrefAttributeValues);
        target._hasCompatibilityHrefAttributeValueAfterParse = _hasCompatibilityHrefAttributeValueAfterParse;
        target._compatibilityHrefAttributeValueAfterParse = _compatibilityHrefAttributeValueAfterParse;
    }
}
