using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvalonDraw;

public partial class GlassEffectWindow : Window
{
    private readonly TextBox _angleBox;
    private readonly TextBox _intensityBox;
    private readonly TextBox _refractionBox;
    private readonly TextBox _depthBox;
    private readonly TextBox _dispersionBox;
    private readonly TextBox _frostBox;

    public GlassEffectWindow()
    {
        InitializeComponent();
        _angleBox = this.FindControl<TextBox>("AngleBox");
        _intensityBox = this.FindControl<TextBox>("IntensityBox");
        _refractionBox = this.FindControl<TextBox>("RefractionBox");
        _depthBox = this.FindControl<TextBox>("DepthBox");
        _dispersionBox = this.FindControl<TextBox>("DispersionBox");
        _frostBox = this.FindControl<TextBox>("FrostBox");

        _angleBox.Text = "45";
        _intensityBox.Text = "1";
        _refractionBox.Text = "0";
        _depthBox.Text = "1";
        _dispersionBox.Text = "0";
        _frostBox.Text = "5";
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public float Angle { get; private set; }
    public float Intensity { get; private set; }
    public float Refraction { get; private set; }
    public float Depth { get; private set; }
    public float Dispersion { get; private set; }
    public float Frost { get; private set; }

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        float.TryParse(_angleBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var a);
        float.TryParse(_intensityBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var i);
        float.TryParse(_refractionBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var r);
        float.TryParse(_depthBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d);
        float.TryParse(_dispersionBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var di);
        float.TryParse(_frostBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f);

        Angle = a;
        Intensity = i;
        Refraction = r;
        Depth = d;
        Dispersion = di;
        Frost = f;
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}
