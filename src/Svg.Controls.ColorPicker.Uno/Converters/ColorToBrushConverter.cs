using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Svg.Controls.ColorPicker.Uno.Converters;

public sealed class ColorToBrushConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        return value is Color color ? new SolidColorBrush(color) : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is SolidColorBrush brush)
        {
            return brush.Color;
        }

        return Color.FromArgb(0, 0, 0, 0);
    }
}
