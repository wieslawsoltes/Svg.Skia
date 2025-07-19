using System.Collections.ObjectModel;
using System.Globalization;
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
    private readonly ComboBox _typeBox;
    private readonly TextBox _centerXBox;
    private readonly TextBox _centerYBox;
    private readonly TextBox _radiusBox;
    private readonly ObservableCollection<GradientStopInfo> _stops;
    private readonly SvgGradientServer _gradient;

    public GradientEditorWindow(SvgGradientServer gradient)
    {
        InitializeComponent();
        Resources["ColorStringConverter"] = new ColorStringConverter();
        _grid = this.FindControl<DataGrid>("StopsGrid");
        _typeBox = this.FindControl<ComboBox>("TypeBox");
        _centerXBox = this.FindControl<TextBox>("CenterXBox");
        _centerYBox = this.FindControl<TextBox>("CenterYBox");
        _radiusBox = this.FindControl<TextBox>("RadiusBox");

        _gradient = gradient;
        _stops = new ObservableCollection<GradientStopInfo>(gradient.Stops.Select(s => new GradientStopInfo { Offset = s.Offset.Value, Color = GradientStopsEntry.ColorToString(s.GetColor(gradient)) }));
        _grid.ItemsSource = _stops;

        if (gradient is SvgRadialGradientServer radial)
        {
            _typeBox.SelectedIndex = 1;
            _centerXBox.Text = radial.CenterX.Value.ToString(CultureInfo.InvariantCulture);
            _centerYBox.Text = radial.CenterY.Value.ToString(CultureInfo.InvariantCulture);
            _radiusBox.Text = radial.Radius.Value.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            _typeBox.SelectedIndex = 0;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public SvgGradientServer? Result { get; private set; }

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
        SvgGradientServer grad;
        if (_typeBox.SelectedIndex == 1)
        {
            var rad = _gradient as SvgRadialGradientServer ?? new SvgRadialGradientServer();
            float.TryParse(_centerXBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var cx);
            float.TryParse(_centerYBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var cy);
            float.TryParse(_radiusBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var r);
            rad.CenterX = new SvgUnit(cx);
            rad.CenterY = new SvgUnit(cy);
            rad.Radius = new SvgUnit(r);
            grad = rad;
        }
        else
        {
            grad = _gradient as SvgLinearGradientServer ?? new SvgLinearGradientServer();
        }

        grad.Children.Clear();
        foreach (var info in _stops)
        {
            var stop = new SvgGradientStop
            {
                Offset = new SvgUnit((float)info.Offset),
                StopColor = new SvgColourServer(GradientStopsEntry.ParseColor(info.Color)),
                StopOpacity = 1f
            };
            grad.Children.Add(stop);
        }
        Result = grad;
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
