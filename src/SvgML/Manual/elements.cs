using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

namespace SvgML;

public class elementsTypeConverter : TypeConverter
{
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(elements);
    }

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        var elements = (elements)value;

        return string.Concat(elements.OfType<content>().Select(x => x.Content));
    }

    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        return new elements(new content
        {
            Content = (string)value
        });
    }
}

[TypeConverter(typeof(elementsTypeConverter))]
public class elements : ObservableCollection<element>
{
    public elements()
    {
    }

    public elements(IEnumerable<element> items) : base(items)
    {
    }

    public elements(params element[] items) : base(items)
    {
    }
}
