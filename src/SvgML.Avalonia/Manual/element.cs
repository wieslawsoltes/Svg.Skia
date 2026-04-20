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
        var content = (content)value;

        return content.Content;
    }

    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        return new content
        {
            Content = (string)value
        };
    }
}

[TypeConverter(typeof(elementTypeConverter))]
public abstract partial class element
{
}
