using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Svg.Editor.Skia.Uno.Converters;

public sealed class BooleanToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush s_selectedBrush = new(ColorHelper.FromArgb(255, 226, 242, 255));
    private static readonly SolidColorBrush s_defaultBrush = new(Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? s_selectedBrush : s_defaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
