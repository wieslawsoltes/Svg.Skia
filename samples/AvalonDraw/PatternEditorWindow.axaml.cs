using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Svg;
using Svg.Pathing;

namespace AvalonDraw;

public partial class PatternEditorWindow : Window
{
    private readonly TextBox _widthBox;
    private readonly TextBox _heightBox;
    private readonly TextBox _pathBox;

    public PatternEditorWindow()
    {
        InitializeComponent();
        _widthBox = this.FindControl<TextBox>("WidthBox");
        _heightBox = this.FindControl<TextBox>("HeightBox");
        _pathBox = this.FindControl<TextBox>("PathBox");
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public SvgPatternServer? Result { get; private set; }

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        float.TryParse(_widthBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var w);
        float.TryParse(_heightBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var h);
        if (w <= 0)
            w = 10f;
        if (h <= 0)
            h = 10f;
        var pat = new SvgPatternServer
        {
            Width = new SvgUnit(w),
            Height = new SvgUnit(h)
        };
        if (!string.IsNullOrWhiteSpace(_pathBox.Text))
        {
            var path = new SvgPath
            {
                PathData = SvgPathBuilder.Parse(_pathBox.Text),
                Fill = new SvgColourServer(System.Drawing.Color.Black)
            };
            pat.Children.Add(path);
        }
        Result = pat;
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}
