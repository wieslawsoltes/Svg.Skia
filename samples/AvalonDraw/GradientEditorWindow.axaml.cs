using System.Collections.ObjectModel;
using System.Linq;
using AvalonDraw.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Svg;

namespace AvalonDraw;

public partial class GradientEditorWindow : Window
{
    private readonly DataGrid _grid;
    private readonly ObservableCollection<GradientStopInfo> _stops;
    private readonly ComboBox _typeBox;
    private readonly TextBox _centerXBox;
    private readonly TextBox _centerYBox;
    private readonly TextBox _radiusBox;
    private readonly SvgGradientServer? _gradient;

    public GradientEditorWindow(ObservableCollection<GradientStopInfo> stops, SvgGradientServer? gradient = null)
    {
        InitializeComponent();
        Resources["ColorStringConverter"] = new ColorStringConverter();
        _grid = this.FindControl<DataGrid>("StopsGrid");
        _typeBox = this.FindControl<ComboBox>("TypeBox");
        _centerXBox = this.FindControl<TextBox>("CenterXBox");
        _centerYBox = this.FindControl<TextBox>("CenterYBox");
        _radiusBox = this.FindControl<TextBox>("RadiusBox");
        _gradient = gradient;
        _stops = new ObservableCollection<GradientStopInfo>(stops.Select(s => new GradientStopInfo { Offset = s.Offset, Color = s.Color }));
        _grid.ItemsSource = _stops;

        if (gradient is SvgRadialGradientServer rad)
        {
            _typeBox.SelectedIndex = 1;
            _centerXBox.Text = rad.CenterX.ToString();
            _centerYBox.Text = rad.CenterY.ToString();
            _radiusBox.Text = rad.Radius.ToString();
        }
        else
        {
            _typeBox.SelectedIndex = 0;
            _centerXBox.Text = "50%";
            _centerYBox.Text = "50%";
            _radiusBox.Text = "50%";
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public ObservableCollection<GradientStopInfo> Result { get; private set; } = new();
    public SvgRadialGradientServer? ResultRadial { get; private set; }
    public bool IsRadial => _typeBox.SelectedIndex == 1;

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
        if (IsRadial)
        {
            var rad = _gradient as SvgRadialGradientServer ?? new SvgRadialGradientServer();
            try
            { rad.CenterX = SvgUnitConverter.Parse(_centerXBox.Text); }
            catch { }
            try
            { rad.CenterY = SvgUnitConverter.Parse(_centerYBox.Text); }
            catch { }
            try
            { rad.Radius = SvgUnitConverter.Parse(_radiusBox.Text); }
            catch { }
            ResultRadial = rad;
        }
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
