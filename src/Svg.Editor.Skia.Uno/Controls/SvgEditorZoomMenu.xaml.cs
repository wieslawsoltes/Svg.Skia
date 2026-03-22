using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class SvgEditorZoomMenu : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(ISvgEditorShellViewModel),
            typeof(SvgEditorZoomMenu),
            new PropertyMetadata(null, OnViewModelPropertyChanged));

    private INotifyPropertyChanged? _trackedViewModel;

    public SvgEditorZoomMenu()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    public ISvgEditorShellViewModel? ViewModel
    {
        get => (ISvgEditorShellViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event EventHandler<EditorMainMenuCommandEventArgs>? CommandRequested;

    public event EventHandler<ZoomPercentRequestedEventArgs>? ZoomPercentRequested;

    public void PrepareForOpen()
    {
        ZoomTextBox.Text = ViewModel?.ViewportLabel ?? "100%";
        RefreshState();

        DispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Normal,
            () =>
            {
                ZoomTextBox.Focus(FocusState.Programmatic);
                ZoomTextBox.SelectAll();
            });
    }

    private static void OnViewModelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SvgEditorZoomMenu)d;
        control.Unsubscribe(e.OldValue as INotifyPropertyChanged);
        control.Subscribe(e.NewValue as INotifyPropertyChanged);
        control.RefreshState();
    }

    private void Subscribe(INotifyPropertyChanged? viewModel)
    {
        _trackedViewModel = viewModel;
        if (_trackedViewModel is not null)
        {
            _trackedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void Unsubscribe(INotifyPropertyChanged? viewModel)
    {
        if (viewModel is not null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (ReferenceEquals(_trackedViewModel, viewModel))
        {
            _trackedViewModel = null;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unsubscribe(_trackedViewModel);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshState();
    }

    private void RefreshState()
    {
        if (ViewModel is null)
        {
            SetCheckmark(GridCheckTextBlock, false);
            SetCheckmark(SnapCheckTextBlock, false);
            SetCheckmark(RulersCheckTextBlock, false);
            SetCheckmark(OutlinesCheckTextBlock, false);
            SetCheckmark(CommentsCheckTextBlock, false);
            return;
        }

        SetCheckmark(GridCheckTextBlock, ViewModel.IsGridVisible);
        SetCheckmark(SnapCheckTextBlock, ViewModel.IsSnapEnabled);
        SetCheckmark(RulersCheckTextBlock, ViewModel.AreRulersVisible);
        SetCheckmark(OutlinesCheckTextBlock, ViewModel.IsWireframeEnabled);
        SetCheckmark(CommentsCheckTextBlock, ViewModel.IsCommentsInspectorActive);
    }

    private static void SetCheckmark(TextBlock textBlock, bool isChecked)
    {
        textBlock.Text = isChecked ? "✓" : string.Empty;
    }

    private void OnCommandClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string rawCommand }
            || !Enum.TryParse<EditorMainMenuCommand>(rawCommand, true, out var command))
        {
            return;
        }

        CommandRequested?.Invoke(this, new EditorMainMenuCommandEventArgs(command));
    }

    private void OnZoomTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
        {
            return;
        }

        if (!TryParseZoomPercent(ZoomTextBox.Text, out var zoomPercent))
        {
            ZoomTextBox.Text = ViewModel?.ViewportLabel ?? "100%";
            ZoomTextBox.SelectAll();
            e.Handled = true;
            return;
        }

        ZoomPercentRequested?.Invoke(this, new ZoomPercentRequestedEventArgs(zoomPercent));
        e.Handled = true;
    }

    private static bool TryParseZoomPercent(string? value, out double zoomPercent)
    {
        zoomPercent = 100.0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace("%", string.Empty, StringComparison.Ordinal).Trim();
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out zoomPercent)
            && zoomPercent >= 5.0
            && zoomPercent <= 6400.0;
    }
}
