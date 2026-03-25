using Svg.Editor.Skia.Uno.Models;
using Windows.UI;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class FigmaColorFieldEditor : UserControl
{
    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color),
            typeof(FigmaColorFieldEditor),
            new PropertyMetadata(Color.FromArgb(255, 224, 65, 65), OnAppearancePropertyChanged));

    public static readonly DependencyProperty IsColorEnabledProperty =
        DependencyProperty.Register(
            nameof(IsColorEnabled),
            typeof(bool),
            typeof(FigmaColorFieldEditor),
            new PropertyMetadata(true, OnAppearancePropertyChanged));

    public static readonly DependencyProperty IsColorEditableProperty =
        DependencyProperty.Register(
            nameof(IsColorEditable),
            typeof(bool),
            typeof(FigmaColorFieldEditor),
            new PropertyMetadata(true, OnAppearancePropertyChanged));

    public static readonly DependencyProperty SummaryTextProperty =
        DependencyProperty.Register(
            nameof(SummaryText),
            typeof(string),
            typeof(FigmaColorFieldEditor),
            new PropertyMetadata(string.Empty, OnAppearancePropertyChanged));

    public static readonly DependencyProperty SwatchesProperty =
        DependencyProperty.Register(
            nameof(Swatches),
            typeof(IEnumerable<ColorSwatchItem>),
            typeof(FigmaColorFieldEditor),
            new PropertyMetadata(null));

    public static readonly DependencyProperty LibraryStylesProperty =
        DependencyProperty.Register(
            nameof(LibraryStyles),
            typeof(IEnumerable<ColorSwatchItem>),
            typeof(FigmaColorFieldEditor),
            new PropertyMetadata(null));

    public static readonly DependencyProperty PaintTargetProperty =
        DependencyProperty.Register(
            nameof(PaintTarget),
            typeof(PaintStyleTarget),
            typeof(FigmaColorFieldEditor),
            new PropertyMetadata(PaintStyleTarget.Fill));

    public static readonly DependencyProperty CurrentStrokeWidthTextProperty =
        DependencyProperty.Register(
            nameof(CurrentStrokeWidthText),
            typeof(string),
            typeof(FigmaColorFieldEditor),
            new PropertyMetadata("1"));

    public static readonly DependencyProperty ShowActionsProperty =
        DependencyProperty.Register(
            nameof(ShowActions),
            typeof(bool),
            typeof(FigmaColorFieldEditor),
            new PropertyMetadata(true, OnAppearancePropertyChanged));

    public FigmaColorFieldEditor()
    {
        InitializeComponent();
        Picker.CloseRequested += OnPickerCloseRequested;
        Picker.PaintStyleRequested += OnPickerPaintStyleRequested;
        Picker.CreateStyleRequested += OnPickerCreateStyleRequested;
        UpdateAppearance();
    }

    public event EventHandler<PaintStyleRequestedEventArgs>? PaintStyleRequested;

    public event EventHandler<PaintStyleCreateRequestedEventArgs>? PaintStyleCreateRequested;

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public bool IsColorEnabled
    {
        get => (bool)GetValue(IsColorEnabledProperty);
        set => SetValue(IsColorEnabledProperty, value);
    }

    public bool IsColorEditable
    {
        get => (bool)GetValue(IsColorEditableProperty);
        set => SetValue(IsColorEditableProperty, value);
    }

    public string SummaryText
    {
        get => (string)GetValue(SummaryTextProperty);
        set => SetValue(SummaryTextProperty, value);
    }

    public IEnumerable<ColorSwatchItem>? Swatches
    {
        get => (IEnumerable<ColorSwatchItem>?)GetValue(SwatchesProperty);
        set => SetValue(SwatchesProperty, value);
    }

    public IEnumerable<ColorSwatchItem>? LibraryStyles
    {
        get => (IEnumerable<ColorSwatchItem>?)GetValue(LibraryStylesProperty);
        set => SetValue(LibraryStylesProperty, value);
    }

    public PaintStyleTarget PaintTarget
    {
        get => (PaintStyleTarget)GetValue(PaintTargetProperty);
        set => SetValue(PaintTargetProperty, value);
    }

    public string CurrentStrokeWidthText
    {
        get => (string)GetValue(CurrentStrokeWidthTextProperty);
        set => SetValue(CurrentStrokeWidthTextProperty, value);
    }

    public bool ShowActions
    {
        get => (bool)GetValue(ShowActionsProperty);
        set => SetValue(ShowActionsProperty, value);
    }

    private static void OnAppearancePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FigmaColorFieldEditor)d).UpdateAppearance();
    }

    private void OnOpenPickerClick(object sender, RoutedEventArgs e)
    {
        if (!IsColorEditable)
        {
            return;
        }

        IsColorEnabled = true;
    }

    private void OnVisibilityToggleClick(object sender, RoutedEventArgs e)
    {
        if (!IsColorEditable)
        {
            VisibilityToggleButton.IsChecked = IsColorEnabled;
            return;
        }

        IsColorEnabled = VisibilityToggleButton.IsChecked ?? false;
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (!IsColorEditable)
        {
            return;
        }

        IsColorEnabled = !IsColorEnabled;
    }

    private void OnPickerCloseRequested(object? sender, EventArgs e)
    {
        PickerFlyout.Hide();
    }

    private void OnPickerPaintStyleRequested(object? sender, PaintStyleRequestedEventArgs e)
    {
        PaintStyleRequested?.Invoke(this, e);
    }

    private void OnPickerCreateStyleRequested(object? sender, PaintStyleCreateRequestedEventArgs e)
    {
        PaintStyleCreateRequested?.Invoke(this, e);
    }

    private void UpdateAppearance()
    {
        var fallbackSummary = IsColorEnabled
            ? $"#{ColorPickerColorHelper.ToHexRgb(SelectedColor)}"
            : "None";
        SummaryTextBlock.Text = string.IsNullOrWhiteSpace(SummaryText)
            ? fallbackSummary
            : SummaryText;
        OpacityTextBlock.Text = IsColorEnabled
            ? $"{ColorPickerColorHelper.ToPercent(SelectedColor.A)} %"
            : string.Empty;

        VisibilityToggleButton.IsChecked = IsColorEnabled;
        VisibilityIcon.Kind = IsColorEnabled ? FigmaIconKind.Eye : FigmaIconKind.EyeOff;

        OpenPickerButton.IsEnabled = IsColorEditable;
        OpenPickerButton.Opacity = IsColorEditable ? 1.0 : 0.58;
        VisibilityToggleButton.IsEnabled = IsColorEditable;
        VisibilityToggleButton.Opacity = IsColorEditable ? 1.0 : 0.58;
        VisibilityToggleButton.Visibility = ShowActions ? Visibility.Visible : Visibility.Collapsed;
        ClearButton.Visibility = ShowActions ? Visibility.Visible : Visibility.Collapsed;
        ActionIcon.Kind = !IsColorEnabled && IsColorEditable ? FigmaIconKind.Add : FigmaIconKind.Minus;
    }
}
