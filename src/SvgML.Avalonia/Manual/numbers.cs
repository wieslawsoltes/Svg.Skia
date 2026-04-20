using System.ComponentModel;
using System.Globalization;
using Svg;

namespace SvgML;

public class numbersTypeConverter : TypeConverter
{
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(numbers);
    }

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        var numbers = (numbers)value;

        return numbers.Number.ToString();
    }

    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        var s = (string)value;
        return numbers.Parse(s);
    }
}

[TypeConverter(typeof(numbersTypeConverter))]
public class numbers
{
    // TODO: https://github.com/AvaloniaUI/Avalonia/issues/17455
    // public class numbers : SvgNumberCollection

    public SvgNumberCollection? Number { get; set; }

    public numbers()
    {
    }

    public numbers(SvgNumberCollection? number)
    {
        Number = number;
    }

    public static numbers Parse(string s)
    {
        var numberCollection = SvgNumberCollectionConverter.Parse(s.AsSpan());

        return new numbers(numberCollection);
    }

    public override string ToString()
    {
        return Number.ToString();
    }
}
