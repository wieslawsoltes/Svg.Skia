using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Svg.JavaScript;

public sealed class SvgJavaScriptAnimatedString
{
    private readonly SvgJavaScriptElement _element;
    private readonly string _attributeName;
    private readonly bool _isAnimatable;

    internal SvgJavaScriptAnimatedString(SvgJavaScriptElement element, string attributeName, bool isAnimatable = true)
    {
        _element = element;
        _attributeName = attributeName;
        _isAnimatable = isAnimatable;
    }

    public string baseVal
    {
        get => _element.GetBaseAttributeValue(_attributeName);
        set => _element.setAttribute(_attributeName, value);
    }

    public string animVal => _isAnimatable ? _element.getAttribute(_attributeName) : baseVal;
}

public sealed class SvgJavaScriptAnimatedLength
{
    private readonly SvgJavaScriptLength _baseVal;
    private readonly SvgJavaScriptLength _animVal;

    internal SvgJavaScriptAnimatedLength(SvgJavaScriptElement element, string attributeName)
    {
        _baseVal = new SvgJavaScriptLength(element, attributeName, false);
        _animVal = new SvgJavaScriptLength(element, attributeName, true);
    }

    internal SvgJavaScriptAnimatedLength(SvgJavaScriptRuntime runtime, Func<string> getter, Action<string>? setter)
    {
        _baseVal = new SvgJavaScriptLength(runtime, getter, setter, false);
        _animVal = new SvgJavaScriptLength(runtime, getter, null, true);
    }

    public SvgJavaScriptLength baseVal => _baseVal;

    public SvgJavaScriptLength animVal => _animVal;
}

public sealed class SvgJavaScriptLength
{
    private readonly SvgJavaScriptElement? _element;
    private readonly SvgJavaScriptRuntime? _runtime;
    private readonly string _attributeName;
    private readonly Func<string>? _getter;
    private readonly Action<string>? _setter;
    private readonly bool _readOnly;
    private string? _detachedValue;

    internal SvgJavaScriptLength(SvgJavaScriptElement element, string attributeName, bool readOnly)
    {
        _element = element;
        _runtime = element.Runtime;
        _attributeName = attributeName;
        _readOnly = readOnly;
    }

    internal SvgJavaScriptLength(SvgJavaScriptRuntime runtime, bool readOnly)
    {
        _runtime = runtime;
        _attributeName = "__detachedLength";
        _readOnly = readOnly;
        _detachedValue = "0";
    }

    internal SvgJavaScriptLength(SvgJavaScriptRuntime runtime, Func<string> getter, Action<string>? setter, bool readOnly)
    {
        _runtime = runtime;
        _attributeName = "__dynamicLength";
        _getter = getter;
        _setter = setter;
        _readOnly = readOnly;
    }

    public string valueAsString
    {
        get => GetRawValue();
        set => SetRawValue(value);
    }

    public double value
    {
        get => ParseNumber(GetRawValue());
        set => SetRawValue(SvgJavaScriptParsing.FormatNumber(value));
    }

    public double valueInSpecifiedUnits
    {
        get => ParseNumber(GetRawValue());
        set => SetRawValue(SvgJavaScriptParsing.FormatNumber(value));
    }

    public ushort unitType => ParseUnitType(GetRawValue());

    private string GetRawValue()
    {
        if (_detachedValue is not null)
        {
            return _detachedValue;
        }

        if (_getter is not null)
        {
            return _getter();
        }

        var rawValue = _element?.getAttribute(_attributeName);
        if (!string.IsNullOrEmpty(rawValue))
        {
            return rawValue;
        }

        return GetDefaultValue();
    }

    private void SetRawValue(string? value)
    {
        EnsureWritable();
        if (!TryNormalizeLength(value, out var normalized))
        {
            (_runtime ?? _element?.Runtime)?.ThrowDomException(12, "Invalid SVG length.");
        }

        if (_detachedValue is not null)
        {
            _detachedValue = normalized;
        }
        else if (_setter is not null)
        {
            _setter(normalized);
        }
        else
        {
            _element?.setAttribute(_attributeName, normalized);
        }
    }

    private void EnsureWritable()
    {
        if (_readOnly)
        {
            (_runtime ?? _element?.Runtime)?.ThrowDomException(7, "This SVGLength is read only.");
        }
    }

    private static bool TryNormalizeLength(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            normalized = "0";
            return true;
        }

        var text = value.Trim();
        var suffix = GetUnitSuffix(text);
        var numeric = suffix.Length == 0 ? text : text.Substring(0, text.Length - suffix.Length);
        if (!double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        normalized = SvgJavaScriptParsing.FormatNumber(parsed) + suffix;
        return true;
    }

    private static string GetUnitSuffix(string value)
    {
        if (value.EndsWith("%", StringComparison.Ordinal))
        {
            return "%";
        }

        if (value.Length >= 2)
        {
            var suffix = value.Substring(value.Length - 2, 2).ToLowerInvariant();
            if (suffix is "em" or "ex" or "px" or "cm" or "mm" or "in" or "pt" or "pc")
            {
                return suffix;
            }
        }

        return string.Empty;
    }

    private static ushort ParseUnitType(string value)
    {
        return GetUnitSuffix(value) switch
        {
            "" => 1,
            "%" => 2,
            "em" => 3,
            "ex" => 4,
            "px" => 5,
            "cm" => 6,
            "mm" => 7,
            "in" => 8,
            "pt" => 9,
            "pc" => 10,
            _ => 0
        };
    }

    private static double ParseNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0d;
        }

        var text = value.Trim();
        var suffix = GetUnitSuffix(text);
        var numeric = suffix.Length == 0 ? text : text.Substring(0, text.Length - suffix.Length);
        return double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0d;
    }

    private string GetDefaultValue()
    {
        if (_element?.Element is SvgDocument or SvgFragment)
        {
            return _attributeName switch
            {
                "x" or "y" => "0",
                "width" or "height" => "100%",
                _ => string.Empty
            };
        }

        return string.Empty;
    }
}

public sealed class SvgJavaScriptAnimatedAngle
{
    private readonly SvgJavaScriptAngle _baseVal;
    private readonly SvgJavaScriptAngle _animVal;

    internal SvgJavaScriptAnimatedAngle(SvgJavaScriptRuntime runtime, Func<string> getter, Action<string> setter)
    {
        _baseVal = new SvgJavaScriptAngle(runtime, getter, setter, false);
        _animVal = new SvgJavaScriptAngle(runtime, getter, null, true);
    }

    public SvgJavaScriptAngle baseVal => _baseVal;

    public SvgJavaScriptAngle animVal => _animVal;
}

public sealed class SvgJavaScriptAngle
{
    private readonly SvgJavaScriptRuntime _runtime;
    private readonly Func<string> _getter;
    private readonly Action<string>? _setter;
    private readonly bool _readOnly;

    internal SvgJavaScriptAngle(SvgJavaScriptRuntime runtime, Func<string> getter, Action<string>? setter, bool readOnly)
    {
        _runtime = runtime;
        _getter = getter;
        _setter = setter;
        _readOnly = readOnly;
    }

    public double value
    {
        get => ParseValue(_getter());
        set => valueAsString = SvgJavaScriptParsing.FormatNumber(value);
    }

    public string valueAsString
    {
        get => _getter();
        set
        {
            EnsureWritable();
            if (!TryNormalize(value, out var normalized))
            {
                _runtime.ThrowDomException(12, "Invalid SVG angle.");
            }

            _setter?.Invoke(normalized);
        }
    }

    public ushort unitType => ParseUnitType(_getter());

    private void EnsureWritable()
    {
        if (_readOnly)
        {
            _runtime.ThrowDomException(7, "This SVGAngle is read only.");
        }
    }

    private static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            normalized = "0";
            return true;
        }

        var text = value.Trim();
        var suffix = text.Length >= 4 && text.EndsWith("grad", StringComparison.OrdinalIgnoreCase)
            ? "grad"
            : text.Length >= 3 && text.EndsWith("deg", StringComparison.OrdinalIgnoreCase)
                ? "deg"
                : text.Length >= 3 && text.EndsWith("rad", StringComparison.OrdinalIgnoreCase)
                    ? "rad"
                    : string.Empty;
        var numeric = suffix.Length == 0 ? text : text.Substring(0, text.Length - suffix.Length);
        if (!double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        normalized = SvgJavaScriptParsing.FormatNumber(parsed) + suffix;
        return true;
    }

    private static double ParseValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0d;
        }

        var text = value.Trim();
        if (text.EndsWith("grad", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(0, text.Length - 4);
        }
        else if (text.EndsWith("deg", StringComparison.OrdinalIgnoreCase) ||
                 text.EndsWith("rad", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(0, text.Length - 3);
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0d;
    }

    private static ushort ParseUnitType(string value)
    {
        if (value.EndsWith("grad", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (value.EndsWith("rad", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (value.EndsWith("deg", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 1;
    }
}

public sealed class SvgJavaScriptAnimatedBoolean
{
    private readonly Func<bool> _getter;
    private readonly Action<bool> _setter;

    internal SvgJavaScriptAnimatedBoolean(SvgJavaScriptRuntime runtime, SvgJavaScriptElement element, string attributeName)
    {
        _getter = () => bool.TryParse(element.getAttribute(attributeName), out var parsed) && parsed;
        _setter = value => element.setAttribute(attributeName, value ? "true" : "false");
    }

    public bool baseVal
    {
        get => _getter();
        set => _setter(value);
    }

    public bool animVal => _getter();
}

public sealed class SvgJavaScriptAnimatedInteger
{
    private readonly Func<int> _getter;
    private readonly Action<int> _setter;

    internal SvgJavaScriptAnimatedInteger(SvgJavaScriptRuntime runtime, SvgJavaScriptElement element, string attributeName)
    {
        _ = runtime;
        _getter = () => int.TryParse(element.getAttribute(attributeName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        _setter = value => element.setAttribute(attributeName, value);
    }

    internal SvgJavaScriptAnimatedInteger(SvgJavaScriptRuntime runtime, Func<int> getter, Action<int> setter)
    {
        _ = runtime;
        _getter = getter;
        _setter = setter;
    }

    public int baseVal
    {
        get => _getter();
        set => _setter(value);
    }

    public int animVal => _getter();
}

public sealed class SvgJavaScriptAnimatedNumber
{
    private readonly Func<double> _getter;
    private readonly Action<double> _setter;

    internal SvgJavaScriptAnimatedNumber(SvgJavaScriptRuntime runtime, SvgJavaScriptElement element, Func<string> getter, Action<double> setter)
    {
        _ = runtime;
        _getter = () => double.TryParse(getter(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0d;
        _setter = setter;
    }

    public double baseVal
    {
        get => _getter();
        set => _setter(value);
    }

    public double animVal => _getter();
}

public sealed class SvgJavaScriptNumber
{
    private readonly SvgJavaScriptRuntime? _runtime;
    private readonly Func<string>? _getter;
    private readonly Action<double>? _setter;
    private readonly bool _readOnly;
    private double _value;

    public SvgJavaScriptNumber()
    {
    }

    internal SvgJavaScriptNumber(SvgJavaScriptRuntime runtime, Func<string> getter, Action<double>? setter, bool readOnly)
    {
        _runtime = runtime;
        _getter = getter;
        _setter = setter;
        _readOnly = readOnly;
    }

    public double value
    {
        get
        {
            if (_getter is null)
            {
                return _value;
            }

            return double.TryParse(_getter(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0d;
        }
        set
        {
            if (_readOnly)
            {
                _runtime?.ThrowDomException(7, "This SVGNumber is read only.");
            }

            if (_setter is null)
            {
                _value = value;
                return;
            }

            _setter(value);
        }
    }
}

public sealed class SvgJavaScriptAnimatedEnumeration
{
    private readonly Func<int> _getter;
    private readonly Action<int> _setter;

    internal SvgJavaScriptAnimatedEnumeration(
        SvgJavaScriptRuntime runtime,
        SvgJavaScriptElement element,
        string attributeName,
        Func<string, int> parse,
        Func<int, string> format)
    {
        _ = runtime;
        _getter = () => parse(element.getAttribute(attributeName));
        _setter = value => element.setAttribute(attributeName, format(value));
    }

    public int baseVal
    {
        get => _getter();
        set => _setter(value);
    }

    public int animVal => _getter();
}

public sealed class SvgJavaScriptAnimatedLengthList
{
    private readonly SvgJavaScriptLengthList _baseVal;
    private readonly SvgJavaScriptLengthList _animVal;

    internal SvgJavaScriptAnimatedLengthList(SvgJavaScriptRuntime runtime, SvgJavaScriptElement element, string attributeName)
    {
        _baseVal = new SvgJavaScriptLengthList(runtime, () => ParseLengths(element.getAttribute(attributeName)), values => element.setAttribute(attributeName, string.Join(" ", values)), false);
        _animVal = new SvgJavaScriptLengthList(runtime, () => ParseLengths(element.getAttribute(attributeName)), null, true);
    }

    public SvgJavaScriptLengthList baseVal => _baseVal;

    public SvgJavaScriptLengthList animVal => _animVal;

    private static List<string> ParseLengths(string? value)
    {
        return SvgJavaScriptParsing.ParseTokenList(value).ToList();
    }
}

public sealed class SvgJavaScriptLengthList
{
    private readonly SvgJavaScriptRuntime _runtime;
    private readonly Func<List<string>> _getter;
    private readonly Action<List<string>>? _setter;
    private readonly bool _readOnly;

    internal SvgJavaScriptLengthList(SvgJavaScriptRuntime runtime, Func<List<string>> getter, Action<List<string>>? setter, bool readOnly)
    {
        _runtime = runtime;
        _getter = getter;
        _setter = setter;
        _readOnly = readOnly;
    }

    public int numberOfItems => _getter().Count;

    public int length => numberOfItems;

    public void clear()
    {
        EnsureWritable();
        _setter?.Invoke(new List<string>());
    }

    public SvgJavaScriptLength getItem(int index)
    {
        var values = _getter();
        if (index < 0 || index >= values.Count)
        {
            _runtime.ThrowDomException(1, "Length index is out of range.");
        }

        return CreateLength(values[index]);
    }

    public SvgJavaScriptLength removeItem(int index)
    {
        EnsureWritable();
        var values = _getter();
        if (index < 0 || index >= values.Count)
        {
            _runtime.ThrowDomException(1, "Length index is out of range.");
        }

        var removed = values[index];
        values.RemoveAt(index);
        _setter?.Invoke(values);
        return CreateLength(removed);
    }

    public SvgJavaScriptLength replaceItem(object newItem, int index)
    {
        EnsureWritable();
        var values = _getter();
        if (index < 0 || index >= values.Count)
        {
            _runtime.ThrowDomException(1, "Length index is out of range.");
        }

        var normalized = NormalizeLengthValue(newItem);
        values[index] = normalized;
        _setter?.Invoke(values);
        return CreateLength(normalized);
    }

    public SvgJavaScriptLength appendItem(object newItem)
    {
        EnsureWritable();
        var values = _getter();
        var normalized = NormalizeLengthValue(newItem);
        values.Add(normalized);
        _setter?.Invoke(values);
        return CreateLength(normalized);
    }

    public SvgJavaScriptLength insertItemBefore(object newItem, int index)
    {
        EnsureWritable();
        var values = _getter();
        if (index < 0 || index > values.Count)
        {
            _runtime.ThrowDomException(1, "Length index is out of range.");
        }

        var normalized = NormalizeLengthValue(newItem);
        values.Insert(index, normalized);
        _setter?.Invoke(values);
        return CreateLength(normalized);
    }

    private void EnsureWritable()
    {
        if (_readOnly)
        {
            _runtime.ThrowDomException(7, "This SVGLengthList is read only.");
        }
    }

    private SvgJavaScriptLength CreateLength(string value)
    {
        var length = new SvgJavaScriptLength(_runtime, false);
        length.valueAsString = value;
        return length;
    }

    private static string NormalizeLengthValue(object newItem)
    {
        return newItem switch
        {
            SvgJavaScriptLength length => length.valueAsString,
            null => "0",
            _ => Convert.ToString(newItem, CultureInfo.InvariantCulture) ?? "0"
        };
    }
}

public sealed class SvgJavaScriptAnimatedNumberList
{
    private readonly SvgJavaScriptNumberList _baseVal;
    private readonly SvgJavaScriptNumberList _animVal;

    internal SvgJavaScriptAnimatedNumberList(SvgJavaScriptRuntime runtime, SvgJavaScriptElement element, string attributeName)
    {
        _baseVal = new SvgJavaScriptNumberList(runtime, () => ParseNumbers(element.getAttribute(attributeName)), values => element.setAttribute(attributeName, string.Join(" ", values.Select(SvgJavaScriptParsing.FormatNumber))), false);
        _animVal = new SvgJavaScriptNumberList(runtime, () => ParseNumbers(element.getAttribute(attributeName)), null, true);
    }

    public SvgJavaScriptNumberList baseVal => _baseVal;

    public SvgJavaScriptNumberList animVal => _animVal;

    private static List<double> ParseNumbers(string? value)
    {
        return SvgJavaScriptParsing.ParseTokenList(value)
            .Select(token => double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0d)
            .ToList();
    }
}

public sealed class SvgJavaScriptNumberList
{
    private readonly SvgJavaScriptRuntime _runtime;
    private readonly Func<List<double>> _getter;
    private readonly Action<List<double>>? _setter;
    private readonly bool _readOnly;

    internal SvgJavaScriptNumberList(SvgJavaScriptRuntime runtime, Func<List<double>> getter, Action<List<double>>? setter, bool readOnly)
    {
        _runtime = runtime;
        _getter = getter;
        _setter = setter;
        _readOnly = readOnly;
    }

    public int numberOfItems => _getter().Count;

    public int length => numberOfItems;

    public void clear()
    {
        EnsureWritable();
        _setter?.Invoke(new List<double>());
    }

    public SvgJavaScriptNumber getItem(int index)
    {
        var values = _getter();
        if (index < 0 || index >= values.Count)
        {
            _runtime.ThrowDomException(1, "Number index is out of range.");
        }

        return CreateNumber(values[index]);
    }

    public SvgJavaScriptNumber removeItem(int index)
    {
        EnsureWritable();
        var values = _getter();
        if (index < 0 || index >= values.Count)
        {
            _runtime.ThrowDomException(1, "Number index is out of range.");
        }

        var removed = values[index];
        values.RemoveAt(index);
        _setter?.Invoke(values);
        return CreateNumber(removed);
    }

    public SvgJavaScriptNumber replaceItem(object value, int index)
    {
        EnsureWritable();
        var values = _getter();
        if (index < 0 || index >= values.Count)
        {
            _runtime.ThrowDomException(1, "Number index is out of range.");
        }

        var normalized = NormalizeNumberValue(value);
        values[index] = normalized;
        _setter?.Invoke(values);
        return CreateNumber(normalized);
    }

    private void EnsureWritable()
    {
        if (_readOnly)
        {
            _runtime.ThrowDomException(7, "This SVGNumberList is read only.");
        }
    }

    private static double NormalizeNumberValue(object value)
    {
        return value switch
        {
            SvgJavaScriptNumber number => number.value,
            IConvertible convertible => convertible.ToDouble(CultureInfo.InvariantCulture),
            _ => 0d
        };
    }

    private static SvgJavaScriptNumber CreateNumber(double value)
    {
        return new SvgJavaScriptNumber
        {
            value = value
        };
    }
}

public sealed class SvgJavaScriptStringList
{
    private readonly SvgJavaScriptRuntime _runtime;
    private readonly Func<List<string>> _getter;
    private readonly Action<List<string>>? _setter;

    internal SvgJavaScriptStringList(SvgJavaScriptRuntime runtime, Func<List<string>> getter, Action<List<string>>? setter)
    {
        _runtime = runtime;
        _getter = getter;
        _setter = setter;
    }

    public int numberOfItems => _getter().Count;

    public int length => numberOfItems;

    public string getItem(int index)
    {
        var values = _getter();
        if (index < 0 || index >= values.Count)
        {
            _runtime.ThrowDomException(1, "String index is out of range.");
        }

        return values[index];
    }

    public string appendItem(string newItem)
    {
        var values = _getter();
        values.Add(newItem);
        _setter?.Invoke(values);
        return newItem;
    }

    public string insertItemBefore(string newItem, int index)
    {
        var values = _getter();
        if (index < 0 || index > values.Count)
        {
            _runtime.ThrowDomException(1, "String index is out of range.");
        }

        values.Insert(index, newItem);
        _setter?.Invoke(values);
        return newItem;
    }

    public string replaceItem(string newItem, int index)
    {
        var values = _getter();
        if (index < 0 || index >= values.Count)
        {
            _runtime.ThrowDomException(1, "String index is out of range.");
        }

        values[index] = newItem;
        _setter?.Invoke(values);
        return newItem;
    }

    public string removeItem(int index)
    {
        var values = _getter();
        if (index < 0 || index >= values.Count)
        {
            _runtime.ThrowDomException(1, "String index is out of range.");
        }

        var removed = values[index];
        values.RemoveAt(index);
        _setter?.Invoke(values);
        return removed;
    }

    public void clear()
    {
        _setter?.Invoke(new List<string>());
    }
}
