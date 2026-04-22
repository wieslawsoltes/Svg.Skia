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
        return value is elements elements
            ? string.Concat(elements.OfType<content>().Select(static x => x.Content))
            : string.Empty;
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
    // MAUI's runtime XAML loader reflects the first public Add(...) overload for collection items.
    // Keep text conversion in TypeConverter/elementTypeConverter instead of adding Add(string).
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
