using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Svg.Editor.Skia.Uno.Converters;

public sealed class BooleanToSidebarTabBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush s_activeBrush = new(ColorHelper.FromArgb(255, 231, 243, 255));
    private static readonly SolidColorBrush s_inactiveBrush = new(ColorHelper.FromArgb(0, 0, 0, 0));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? s_activeBrush : s_inactiveBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
