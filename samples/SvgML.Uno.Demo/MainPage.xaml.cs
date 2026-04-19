using Svg;

namespace SvgML.Uno.Demo;

public sealed partial class MainPage : Page
{
    private const string ColdCss = "#accent-shape, #accent-bar { fill: #2563eb; } #outline-shape { stroke: #0f172a; stroke-width: 4; }";
    private const string WarmCss = "#accent-shape, #accent-bar { fill: #ef4444; } #outline-shape { stroke: #7c2d12; stroke-width: 4; }";
    private const string OrbitPath = "M40 80 C40 30 200 30 200 80 C200 130 40 130 40 80 Z";

    private bool _useWarmTheme;

    public MainPage()
    {
        InitializeComponent();

        StyledSvg.CurrentCss = ColdCss;
        OrbitAnimation.path = OrbitPath;
        OrbitAnimation.dur = "4.5s";
        OrbitAnimation.repeatCount = "indefinite";
        OrbitAnimation.rotate = "auto";

        RotorAnimation.attributeName = "transform";
        RotorAnimation.type = SvgAnimateTransformType.Rotate;
        RotorAnimation.from = "0 120 80";
        RotorAnimation.to = "360 120 80";
        RotorAnimation.dur = "3.4s";
        RotorAnimation.repeatCount = "indefinite";
    }

    private void OnSwapThemeClick(object sender, RoutedEventArgs e)
    {
        _useWarmTheme = !_useWarmTheme;
        StyledSvg.CurrentCss = _useWarmTheme ? WarmCss : ColdCss;
        ThemeStatusText.Text = $"Current theme: {(_useWarmTheme ? "ember" : "cobalt")}";
    }
}
