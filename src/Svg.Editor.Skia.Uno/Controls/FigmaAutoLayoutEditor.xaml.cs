using System.Linq;
using Microsoft.UI.Xaml.Media;
using Svg.Editor.Core;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class FigmaAutoLayoutEditor : UserControl
{
    public static readonly DependencyProperty IsEditableProperty =
        DependencyProperty.Register(
            nameof(IsEditable),
            typeof(bool),
            typeof(FigmaAutoLayoutEditor),
            new PropertyMetadata(false, OnStatePropertyChanged));

    public static readonly DependencyProperty IsAutoLayoutEnabledProperty =
        DependencyProperty.Register(
            nameof(IsAutoLayoutEnabled),
            typeof(bool),
            typeof(FigmaAutoLayoutEditor),
            new PropertyMetadata(false, OnStatePropertyChanged));

    public static readonly DependencyProperty ShowHeaderProperty =
        DependencyProperty.Register(
            nameof(ShowHeader),
            typeof(bool),
            typeof(FigmaAutoLayoutEditor),
            new PropertyMetadata(true, OnStatePropertyChanged));

    public static readonly DependencyProperty FlowProperty =
        DependencyProperty.Register(
            nameof(Flow),
            typeof(AutoLayoutFlow),
            typeof(FigmaAutoLayoutEditor),
            new PropertyMetadata(AutoLayoutFlow.Vertical, OnStatePropertyChanged));

    public static readonly DependencyProperty WidthModeProperty =
        DependencyProperty.Register(
            nameof(WidthMode),
            typeof(AutoLayoutSizeMode),
            typeof(FigmaAutoLayoutEditor),
            new PropertyMetadata(AutoLayoutSizeMode.Fixed, OnStatePropertyChanged));

    public static readonly DependencyProperty HeightModeProperty =
        DependencyProperty.Register(
            nameof(HeightMode),
            typeof(AutoLayoutSizeMode),
            typeof(FigmaAutoLayoutEditor),
            new PropertyMetadata(AutoLayoutSizeMode.Fixed, OnStatePropertyChanged));

    public static readonly DependencyProperty HorizontalAlignmentModeProperty =
        DependencyProperty.Register(
            nameof(HorizontalAlignmentMode),
            typeof(AutoLayoutAlignment),
            typeof(FigmaAutoLayoutEditor),
            new PropertyMetadata(AutoLayoutAlignment.Start, OnStatePropertyChanged));

    public static readonly DependencyProperty VerticalAlignmentModeProperty =
        DependencyProperty.Register(
            nameof(VerticalAlignmentMode),
            typeof(AutoLayoutAlignment),
            typeof(FigmaAutoLayoutEditor),
            new PropertyMetadata(AutoLayoutAlignment.Start, OnStatePropertyChanged));

    public static readonly DependencyProperty WidthValueProperty =
        DependencyProperty.Register(
            nameof(WidthValue),
            typeof(string),
            typeof(FigmaAutoLayoutEditor),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HeightValueProperty =
        DependencyProperty.Register(
            nameof(HeightValue),
            typeof(string),
            typeof(FigmaAutoLayoutEditor),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty GapValueProperty =
        DependencyProperty.Register(
            nameof(GapValue),
            typeof(string),
            typeof(FigmaAutoLayoutEditor),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PaddingHorizontalValueProperty =
        DependencyProperty.Register(
            nameof(PaddingHorizontalValue),
            typeof(string),
            typeof(FigmaAutoLayoutEditor),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PaddingVerticalValueProperty =
        DependencyProperty.Register(
            nameof(PaddingVerticalValue),
            typeof(string),
            typeof(FigmaAutoLayoutEditor),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsClipContentProperty =
        DependencyProperty.Register(
            nameof(IsClipContent),
            typeof(bool),
            typeof(FigmaAutoLayoutEditor),
            new PropertyMetadata(false));

    public FigmaAutoLayoutEditor()
    {
        InitializeComponent();
        UpdateVisualState();
    }

    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    public bool IsAutoLayoutEnabled
    {
        get => (bool)GetValue(IsAutoLayoutEnabledProperty);
        set => SetValue(IsAutoLayoutEnabledProperty, value);
    }

    public bool ShowHeader
    {
        get => (bool)GetValue(ShowHeaderProperty);
        set => SetValue(ShowHeaderProperty, value);
    }

    public AutoLayoutFlow Flow
    {
        get => (AutoLayoutFlow)GetValue(FlowProperty);
        set => SetValue(FlowProperty, value);
    }

    public AutoLayoutSizeMode WidthMode
    {
        get => (AutoLayoutSizeMode)GetValue(WidthModeProperty);
        set => SetValue(WidthModeProperty, value);
    }

    public AutoLayoutSizeMode HeightMode
    {
        get => (AutoLayoutSizeMode)GetValue(HeightModeProperty);
        set => SetValue(HeightModeProperty, value);
    }

    public AutoLayoutAlignment HorizontalAlignmentMode
    {
        get => (AutoLayoutAlignment)GetValue(HorizontalAlignmentModeProperty);
        set => SetValue(HorizontalAlignmentModeProperty, value);
    }

    public AutoLayoutAlignment VerticalAlignmentMode
    {
        get => (AutoLayoutAlignment)GetValue(VerticalAlignmentModeProperty);
        set => SetValue(VerticalAlignmentModeProperty, value);
    }

    public string WidthValue
    {
        get => (string)GetValue(WidthValueProperty);
        set => SetValue(WidthValueProperty, value);
    }

    public string HeightValue
    {
        get => (string)GetValue(HeightValueProperty);
        set => SetValue(HeightValueProperty, value);
    }

    public string GapValue
    {
        get => (string)GetValue(GapValueProperty);
        set => SetValue(GapValueProperty, value);
    }

    public string PaddingHorizontalValue
    {
        get => (string)GetValue(PaddingHorizontalValueProperty);
        set => SetValue(PaddingHorizontalValueProperty, value);
    }

    public string PaddingVerticalValue
    {
        get => (string)GetValue(PaddingVerticalValueProperty);
        set => SetValue(PaddingVerticalValueProperty, value);
    }

    public bool IsClipContent
    {
        get => (bool)GetValue(IsClipContentProperty);
        set => SetValue(IsClipContentProperty, value);
    }

    private static void OnStatePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FigmaAutoLayoutEditor)d).UpdateVisualState();
    }

    private void OnToggleAutoLayoutClick(object sender, RoutedEventArgs e)
    {
        if (!IsEditable)
        {
            return;
        }

        IsAutoLayoutEnabled = !IsAutoLayoutEnabled;
    }

    private void OnFlowButtonClick(object sender, RoutedEventArgs e)
    {
        if (!IsEditable || sender is not FrameworkElement { Tag: string rawValue })
        {
            return;
        }

        if (Enum.TryParse<AutoLayoutFlow>(rawValue, true, out var flow))
        {
            Flow = flow;
        }
    }

    private void OnAlignmentButtonClick(object sender, RoutedEventArgs e)
    {
        if (!IsEditable || sender is not FrameworkElement { Tag: string rawValue })
        {
            return;
        }

        var parts = rawValue.Split(',');
        if (parts.Length != 2
            || !Enum.TryParse<AutoLayoutAlignment>(parts[0], true, out var horizontal)
            || !Enum.TryParse<AutoLayoutAlignment>(parts[1], true, out var vertical))
        {
            return;
        }

        HorizontalAlignmentMode = horizontal;
        VerticalAlignmentMode = vertical;
    }

    private void OnWidthModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: ComboBoxItem { Tag: string rawValue } }
            && Enum.TryParse<AutoLayoutSizeMode>(rawValue, true, out var mode))
        {
            WidthMode = mode;
        }
    }

    private void OnHeightModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: ComboBoxItem { Tag: string rawValue } }
            && Enum.TryParse<AutoLayoutSizeMode>(rawValue, true, out var mode))
        {
            HeightMode = mode;
        }
    }

    private void UpdateVisualState()
    {
        HeaderGrid.Visibility = ShowHeader ? Visibility.Visible : Visibility.Collapsed;
        ToggleAutoLayoutTextBlock.Text = IsAutoLayoutEnabled ? "Remove" : "Apply";
        ToggleAutoLayoutButton.IsEnabled = IsEditable;
        HintTextBlock.Visibility = ShowHeader ? Visibility.Visible : Visibility.Collapsed;

        EditorStack.Visibility = IsEditable ? Visibility.Visible : Visibility.Collapsed;
        HintTextBlock.Text = !IsEditable
            ? "Select a single frame to enable or edit auto layout."
            : IsAutoLayoutEnabled
                ? "Auto layout writes child positioning back as SVG transforms and uses clip paths when clipping is enabled."
                : "Apply auto layout to the selected frame to pack layers with padding, gap, and clip content.";

        WidthTextBox.IsEnabled = IsEditable && WidthMode == AutoLayoutSizeMode.Fixed;
        HeightTextBox.IsEnabled = IsEditable && HeightMode == AutoLayoutSizeMode.Fixed;
        GapTextBox.IsEnabled = IsEditable;
        PaddingHorizontalTextBox.IsEnabled = IsEditable;
        PaddingVerticalTextBox.IsEnabled = IsEditable;
        ClipContentCheckBox.IsEnabled = IsEditable;
        WidthModeComboBox.IsEnabled = IsEditable;
        HeightModeComboBox.IsEnabled = IsEditable;
        AdvancedSettingsStack.Visibility = IsEditable && IsAutoLayoutEnabled
            ? Visibility.Visible
            : Visibility.Collapsed;

        SelectComboItem(WidthModeComboBox, WidthMode);
        SelectComboItem(HeightModeComboBox, HeightMode);

        SetSegmentState(HorizontalFlowButton, Flow == AutoLayoutFlow.Horizontal);
        SetSegmentState(VerticalFlowButton, Flow == AutoLayoutFlow.Vertical);
        SetSegmentState(WrapFlowButton, Flow == AutoLayoutFlow.Wrap);
        SetSegmentState(GridFlowButton, Flow == AutoLayoutFlow.Grid);

        SetMatrixState(AlignTopLeftButton, AutoLayoutAlignment.Start, AutoLayoutAlignment.Start);
        SetMatrixState(AlignTopCenterButton, AutoLayoutAlignment.Center, AutoLayoutAlignment.Start);
        SetMatrixState(AlignTopRightButton, AutoLayoutAlignment.End, AutoLayoutAlignment.Start);
        SetMatrixState(AlignMiddleLeftButton, AutoLayoutAlignment.Start, AutoLayoutAlignment.Center);
        SetMatrixState(AlignMiddleCenterButton, AutoLayoutAlignment.Center, AutoLayoutAlignment.Center);
        SetMatrixState(AlignMiddleRightButton, AutoLayoutAlignment.End, AutoLayoutAlignment.Center);
        SetMatrixState(AlignBottomLeftButton, AutoLayoutAlignment.Start, AutoLayoutAlignment.End);
        SetMatrixState(AlignBottomCenterButton, AutoLayoutAlignment.Center, AutoLayoutAlignment.End);
        SetMatrixState(AlignBottomRightButton, AutoLayoutAlignment.End, AutoLayoutAlignment.End);
    }

    private void SelectComboItem(ComboBox comboBox, AutoLayoutSizeMode mode)
    {
        var selected = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Tag is string rawValue
                && Enum.TryParse<AutoLayoutSizeMode>(rawValue, true, out var parsed)
                && parsed == mode);
        if (!ReferenceEquals(comboBox.SelectedItem, selected))
        {
            comboBox.SelectedItem = selected;
        }
    }

    private void SetSegmentState(Button button, bool isSelected)
    {
        var foreground = GetBrush(isSelected ? "AccentBrush" : "TextBrush");
        button.Background = GetBrush(isSelected ? "AccentSoftBrush" : "PickerChipBrush");
        button.BorderBrush = GetBrush(isSelected ? "AccentBrush" : "SurfaceStrokeBrush");
        button.Foreground = foreground;

        if (button.Content is FigmaIcon icon)
        {
            icon.IconStroke = foreground;
        }
    }

    private void SetMatrixState(Button button, AutoLayoutAlignment horizontal, AutoLayoutAlignment vertical)
    {
        var isSelected = HorizontalAlignmentMode == horizontal && VerticalAlignmentMode == vertical;
        button.Background = GetBrush(isSelected ? "AccentSoftBrush" : "PickerChipBrush");
        button.BorderBrush = GetBrush(isSelected ? "AccentBrush" : "SurfaceStrokeBrush");
    }

    private Brush GetBrush(string key)
    {
        return (Brush)Resources[key];
    }
}
