using System.Collections.ObjectModel;
using System.Linq;
using AvalonDraw.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace AvalonDraw;

public partial class StrokeProfileEditorWindow : Window
{
    private readonly DataGrid _grid;
    private readonly ObservableCollection<StrokePointInfo> _points;

    public StrokeProfileEditorWindow(ObservableCollection<StrokePointInfo> points)
    {
        InitializeComponent();
        _grid = this.FindControl<DataGrid>("PointsGrid");
        _points = new ObservableCollection<StrokePointInfo>(points.Select(p => new StrokePointInfo { Offset = p.Offset, Width = p.Width }));
        _grid.ItemsSource = _points;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public ObservableCollection<StrokePointInfo> Result { get; private set; } = new();

    private void AddButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _points.Add(new StrokePointInfo { Offset = 0.0, Width = 1.0 });
    }

    private void RemoveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_grid.SelectedItem is StrokePointInfo info)
            _points.Remove(info);
    }

    private void OkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Result = new ObservableCollection<StrokePointInfo>(_points.Select(p => new StrokePointInfo { Offset = p.Offset, Width = p.Width }));
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}

