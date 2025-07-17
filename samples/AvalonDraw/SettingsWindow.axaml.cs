using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvalonDraw;

public partial class SettingsWindow : Window
{
    private readonly CheckBox _snapCheckBox;
    private readonly CheckBox _gridCheckBox;
    private readonly TextBox _gridSizeBox;

    public SettingsWindow(bool snapToGrid, double gridSize, bool showGrid)
    {
        InitializeComponent();
        _snapCheckBox = this.FindControl<CheckBox>("SnapCheckBox");
        _gridCheckBox = this.FindControl<CheckBox>("GridCheckBox");
        _gridSizeBox = this.FindControl<TextBox>("GridSizeBox");
        _snapCheckBox.IsChecked = snapToGrid;
        _gridCheckBox.IsChecked = showGrid;
        _gridSizeBox.Text = gridSize.ToString(CultureInfo.InvariantCulture);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public bool SnapToGrid { get; private set; }
    public bool ShowGrid { get; private set; }
    public double GridSize { get; private set; }

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SnapToGrid = _snapCheckBox.IsChecked ?? false;
        ShowGrid = _gridCheckBox.IsChecked ?? false;
        if (double.TryParse(_gridSizeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var size))
            GridSize = size;
        else
            GridSize = 1.0;
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}
