using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class SvgEditorTopBar : UserControl
{
    private readonly Flyout _zoomFlyout;
    private readonly SvgEditorZoomMenu _zoomMenu;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(ISvgEditorShellViewModel),
            typeof(SvgEditorTopBar),
            new PropertyMetadata(null));

    public SvgEditorTopBar()
    {
        InitializeComponent();
        _zoomMenu = new SvgEditorZoomMenu();
        _zoomMenu.CommandRequested += OnZoomMenuCommandRequested;
        _zoomMenu.ZoomPercentRequested += OnZoomPercentRequested;
        _zoomFlyout = new Flyout
        {
            Content = _zoomMenu
        };
    }

    public ISvgEditorShellViewModel? ViewModel
    {
        get => (ISvgEditorShellViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event RoutedEventHandler? ResetRequested;

    public event RoutedEventHandler? FitRequested;

    public event RoutedEventHandler? ShareRequested;

    public event RoutedEventHandler? MainMenuRequested;

    public event RoutedEventHandler? LeftPanelToggleRequested;

    public event EventHandler<EditorMainMenuCommandEventArgs>? ZoomMenuCommandRequested;

    public event EventHandler<ZoomPercentRequestedEventArgs>? ZoomPercentRequested;

    private void OnMainMenuClick(object sender, RoutedEventArgs e)
    {
        MainMenuRequested?.Invoke(MainMenuButton, e);
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        ResetRequested?.Invoke(sender, e);
    }

    private void OnLeftPanelToggleClick(object sender, RoutedEventArgs e)
    {
        LeftPanelToggleRequested?.Invoke(sender, e);
    }

    private void OnFitClick(object sender, RoutedEventArgs e)
    {
        FitRequested?.Invoke(sender, e);
    }

    private void OnShareClick(object sender, RoutedEventArgs e)
    {
        ShareRequested?.Invoke(sender, e);
    }

    private void OnZoomMenuClick(object sender, RoutedEventArgs e)
    {
        _zoomMenu.ViewModel = ViewModel;
        _zoomMenu.PrepareForOpen();
        _zoomFlyout.ShowAt(
            ZoomMenuButton,
            new FlyoutShowOptions
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
                ShowMode = FlyoutShowMode.Transient
            });
    }

    private void OnZoomMenuCommandRequested(object? sender, EditorMainMenuCommandEventArgs e)
    {
        ZoomMenuCommandRequested?.Invoke(this, e);
        _zoomFlyout.Hide();
    }

    private void OnZoomPercentRequested(object? sender, ZoomPercentRequestedEventArgs e)
    {
        ZoomPercentRequested?.Invoke(this, e);
        _zoomFlyout.Hide();
    }
}
