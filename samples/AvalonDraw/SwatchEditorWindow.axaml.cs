using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace AvalonDraw;

public partial class SwatchEditorWindow : Window
{
    private readonly ColorPicker _picker;

    public SwatchEditorWindow(string color)
    {
        InitializeComponent();
        _picker = this.FindControl<ColorPicker>("Picker");
        _picker.Color = Color.Parse(color);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public string Result { get; private set; } = "#000000";

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var c = _picker.Color;
        Result = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}
