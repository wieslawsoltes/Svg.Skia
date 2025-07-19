using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace AvalonDraw;

public partial class ExportOptionsWindow : Window
{
    private readonly TextBox _widthBox;
    private readonly TextBox _heightBox;
    private readonly ColorPicker _backgroundPicker;

    public ExportOptionsWindow(double width, double height, Color background)
    {
        InitializeComponent();
        _widthBox = this.FindControl<TextBox>("WidthBox");
        _heightBox = this.FindControl<TextBox>("HeightBox");
        _backgroundPicker = this.FindControl<ColorPicker>("BackgroundPicker");
        _widthBox.Text = width.ToString(CultureInfo.InvariantCulture);
        _heightBox.Text = height.ToString(CultureInfo.InvariantCulture);
        _backgroundPicker.Color = background;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public double PageWidth { get; private set; }
    public double PageHeight { get; private set; }
    public Color Background { get; private set; }

    private void OkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!double.TryParse(_widthBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var w))
            w = 0;
        if (!double.TryParse(_heightBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
            h = 0;
        PageWidth = w;
        PageHeight = h;
        Background = _backgroundPicker.Color;
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
