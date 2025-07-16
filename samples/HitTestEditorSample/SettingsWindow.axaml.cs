using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HitTestEditorSample;

public partial class SettingsWindow : Window
{
    public SettingsWindow(bool snapToGrid, double gridSize)
    {
        InitializeComponent();
        SnapCheckBox.IsChecked = snapToGrid;
        GridSizeBox.Text = gridSize.ToString(CultureInfo.InvariantCulture);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public bool SnapToGrid { get; private set; }
    public double GridSize { get; private set; }

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SnapToGrid = SnapCheckBox.IsChecked ?? false;
        if (double.TryParse(GridSizeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var size))
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
