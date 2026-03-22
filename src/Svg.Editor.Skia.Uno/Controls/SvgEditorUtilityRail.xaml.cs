namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class SvgEditorUtilityRail : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(ISvgEditorShellViewModel),
            typeof(SvgEditorUtilityRail),
            new PropertyMetadata(null));

    public SvgEditorUtilityRail()
    {
        InitializeComponent();
    }

    public ISvgEditorShellViewModel? ViewModel
    {
        get => (ISvgEditorShellViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event RoutedEventHandler? MainMenuRequested;

    public event RoutedEventHandler? LeftPanelToggleRequested;

    public event RoutedEventHandler? LayersTabRequested;

    public event RoutedEventHandler? AssetsTabRequested;

    public event RoutedEventHandler? ActionsRequested;

    private void OnMainMenuClick(object sender, RoutedEventArgs e)
    {
        MainMenuRequested?.Invoke(sender, e);
    }

    private void OnLeftPanelToggleClick(object sender, RoutedEventArgs e)
    {
        LeftPanelToggleRequested?.Invoke(sender, e);
    }

    private void OnLayersClick(object sender, RoutedEventArgs e)
    {
        LayersTabRequested?.Invoke(sender, e);
    }

    private void OnAssetsClick(object sender, RoutedEventArgs e)
    {
        AssetsTabRequested?.Invoke(sender, e);
    }

    private void OnActionsClick(object sender, RoutedEventArgs e)
    {
        ActionsRequested?.Invoke(sender, e);
    }
}
