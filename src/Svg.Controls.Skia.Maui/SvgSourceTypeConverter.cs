using System.ComponentModel;
using System.Globalization;

namespace Maui.Svg.Skia;

public sealed class SvgSourceTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        return value is string path
            ? new SvgSource { Path = path }
            : base.ConvertFrom(context, culture, value);
    }
}
