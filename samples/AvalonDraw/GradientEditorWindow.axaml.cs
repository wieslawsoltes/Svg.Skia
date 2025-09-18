using System.Collections.ObjectModel;
using System.Linq;
using AvalonDraw.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace AvalonDraw;

public partial class GradientEditorWindow : Window
{
    private readonly DataGrid _grid;
    private readonly ObservableCollection<GradientStopInfo> _stops;

    public GradientEditorWindow(ObservableCollection<GradientStopInfo> stops)
    {
        InitializeComponent();
        Resources["ColorStringConverter"] = new ColorStringConverter();
        _grid = this.FindControl<DataGrid>("StopsGrid");
        _stops = new ObservableCollection<GradientStopInfo>(stops.Select(s => new GradientStopInfo { Offset = s.Offset, Color = s.Color }));
        _grid.ItemsSource = _stops;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public ObservableCollection<GradientStopInfo> Result { get; private set; } = new();

    private void AddButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _stops.Add(new GradientStopInfo { Offset = 0.0, Color = "#000000" });
    }

    private void RemoveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_grid.SelectedItem is GradientStopInfo info)
            _stops.Remove(info);
    }

    private void OkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Result = new ObservableCollection<GradientStopInfo>(_stops.Select(s => new GradientStopInfo { Offset = s.Offset, Color = s.Color }));
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
