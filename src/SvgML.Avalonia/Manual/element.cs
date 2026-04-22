using System.ComponentModel;
using System.Globalization;

namespace SvgML;

public class elementTypeConverter : TypeConverter
{
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(content);
    }

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        return value is content content
            ? content.Content
            : string.Empty;
    }

    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        return new content
        {
            Content = value as string ?? string.Empty
        };
    }
}

[TypeConverter(typeof(elementTypeConverter))]
public abstract partial class element
{
    public static implicit operator element(string value)
    {
        return new content
        {
            Content = value
        };
    }
}
