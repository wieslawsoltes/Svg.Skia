using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Svg;

namespace AvalonDraw.Services;

public class PropertyEntry : INotifyPropertyChanged
{
    public string Name { get; }
    public PropertyInfo? Property { get; }
    private readonly Action<object, string?>? _setter;
    private readonly TypeConverter? _converter;
    private string? _value;

    public IEnumerable<string>? Options { get; init; }
    public IEnumerable<string>? Suggestions { get; set; }

    public string? Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    public PropertyEntry(string name, PropertyInfo property, string? value)
    {
        Name = name;
        Property = property;
        _converter = TypeDescriptor.GetConverter(property.PropertyType);
        _value = value;
        if (property.PropertyType.IsEnum)
            Options = Enum.GetNames(property.PropertyType);
        else if (IsUriProperty(property))
            Suggestions = null; // filled later
    }

    protected PropertyEntry(string name, string? value, Action<object, string?> setter)
    {
        Name = name;
        _setter = setter;
        _value = value;
    }

    public static PropertyEntry CreateAttribute(string name, string? value, Action<object, string?> setter)
        => new PropertyEntry(name, value, setter);

    public virtual void Apply(object target)
    {
        try
        {
            if (Property is { } prop)
            {
                var converted = _converter!.ConvertFromInvariantString(Value);
                prop.SetValue(target, converted);
            }
            else
            {
                _setter?.Invoke(target, Value);
            }
        }
        catch
        {
        }
    }

    private static bool IsUriProperty(PropertyInfo prop)
    {
        if (prop.PropertyType == typeof(Uri) || typeof(SvgPaintServer).IsAssignableFrom(prop.PropertyType))
            return true;
        var tc = prop.GetCustomAttribute<TypeConverterAttribute>();
        if (tc != null && tc.ConverterTypeName == typeof(UriTypeConverter).FullName)
            return true;
        if (prop.Name.Contains("Href", StringComparison.OrdinalIgnoreCase))
            return true;
        var svgAttr = prop.GetCustomAttribute<SvgAttributeAttribute>();
        if (svgAttr != null && svgAttr.Name.Contains("href", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifyChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    }
}
