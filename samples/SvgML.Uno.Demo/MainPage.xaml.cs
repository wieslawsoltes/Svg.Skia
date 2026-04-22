namespace SvgML.Uno.Demo;

public sealed partial class MainPage : Page
{
    private const string ColdCss = "#accent-shape, #accent-bar { fill: #2563eb; } #outline-shape { stroke: #0f172a; stroke-width: 4; }";
    private const string WarmCss = "#accent-shape, #accent-bar { fill: #ef4444; } #outline-shape { stroke: #7c2d12; stroke-width: 4; }";

    private bool _selectedInitialTab;
    private bool _useWarmTheme;

    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_selectedInitialTab)
        {
            return;
        }

        _selectedInitialTab = true;
        DemoPivot.SelectedIndex = 1;
    }

    private void OnSwapThemeClick(object sender, RoutedEventArgs e)
    {
        _useWarmTheme = !_useWarmTheme;
        StyledSvg.CurrentCss = _useWarmTheme ? WarmCss : ColdCss;
        ThemeStatusText.Text = $"Current theme: {(_useWarmTheme ? "ember" : "cobalt")}";
    }
}
