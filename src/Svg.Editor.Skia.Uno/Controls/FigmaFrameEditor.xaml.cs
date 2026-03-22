using System.Collections.Generic;
using System.Linq;
using Svg.Editor.Core;
using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class FigmaFrameEditor : UserControl
{
    public static readonly DependencyProperty IsEditableProperty =
        DependencyProperty.Register(
            nameof(IsEditable),
            typeof(bool),
            typeof(FigmaFrameEditor),
            new PropertyMetadata(false, OnStatePropertyChanged));

    public static readonly DependencyProperty CanUsePresetsProperty =
        DependencyProperty.Register(
            nameof(CanUsePresets),
            typeof(bool),
            typeof(FigmaFrameEditor),
            new PropertyMetadata(false, OnStatePropertyChanged));

    public static readonly DependencyProperty ShowHeaderProperty =
        DependencyProperty.Register(
            nameof(ShowHeader),
            typeof(bool),
            typeof(FigmaFrameEditor),
            new PropertyMetadata(true, OnStatePropertyChanged));

    public static readonly DependencyProperty ContainerKindProperty =
        DependencyProperty.Register(
            nameof(ContainerKind),
            typeof(FrameContainerKind),
            typeof(FigmaFrameEditor),
            new PropertyMetadata(FrameContainerKind.Frame, OnStatePropertyChanged));

    public static readonly DependencyProperty SelectedPresetIdProperty =
        DependencyProperty.Register(
            nameof(SelectedPresetId),
            typeof(string),
            typeof(FigmaFrameEditor),
            new PropertyMetadata(EditorFramePresetItem.CustomId, OnStatePropertyChanged));

    public static readonly DependencyProperty SummaryTextProperty =
        DependencyProperty.Register(
            nameof(SummaryText),
            typeof(string),
            typeof(FigmaFrameEditor),
            new PropertyMetadata(string.Empty, OnStatePropertyChanged));

    public static readonly DependencyProperty PresetsProperty =
        DependencyProperty.Register(
            nameof(Presets),
            typeof(IEnumerable<EditorFramePresetItem>),
            typeof(FigmaFrameEditor),
            new PropertyMetadata(null, OnStatePropertyChanged));

    private bool _isUpdatingState;

    public FigmaFrameEditor()
    {
        InitializeComponent();
        UpdateVisualState();
    }

    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    public bool CanUsePresets
    {
        get => (bool)GetValue(CanUsePresetsProperty);
        set => SetValue(CanUsePresetsProperty, value);
    }

    public bool ShowHeader
    {
        get => (bool)GetValue(ShowHeaderProperty);
        set => SetValue(ShowHeaderProperty, value);
    }

    public FrameContainerKind ContainerKind
    {
        get => (FrameContainerKind)GetValue(ContainerKindProperty);
        set => SetValue(ContainerKindProperty, value);
    }

    public string SelectedPresetId
    {
        get => (string)GetValue(SelectedPresetIdProperty);
        set => SetValue(SelectedPresetIdProperty, value);
    }

    public string SummaryText
    {
        get => (string)GetValue(SummaryTextProperty);
        set => SetValue(SummaryTextProperty, value);
    }

    public IEnumerable<EditorFramePresetItem>? Presets
    {
        get => (IEnumerable<EditorFramePresetItem>?)GetValue(PresetsProperty);
        set => SetValue(PresetsProperty, value);
    }

    private static void OnStatePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FigmaFrameEditor)d).UpdateVisualState();
    }

    private void OnKindSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingState
            || sender is not ComboBox { SelectedItem: ComboBoxItem { Tag: string rawValue } }
            || !Enum.TryParse<FrameContainerKind>(rawValue, true, out var kind))
        {
            return;
        }

        ContainerKind = kind;
    }

    private void OnPresetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingState || sender is not ComboBox comboBox)
        {
            return;
        }

        if (comboBox.SelectedValue is string selectedId && !string.IsNullOrWhiteSpace(selectedId))
        {
            SelectedPresetId = selectedId;
        }
    }

    private void OnCompactKindSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        OnKindSelectionChanged(sender, e);
    }

    private void OnCompactPresetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        OnPresetSelectionChanged(sender, e);
    }

    private void UpdateVisualState()
    {
        _isUpdatingState = true;
        try
        {
            var showCompactPreset = CanUsePresets
                && !string.Equals(SelectedPresetId, EditorFramePresetItem.CustomId, StringComparison.Ordinal);

            HeaderGrid.Visibility = ShowHeader ? Visibility.Visible : Visibility.Collapsed;
            HintTextBlock.Visibility = ShowHeader ? Visibility.Visible : Visibility.Collapsed;
            CompactEditorStack.Visibility = ShowHeader ? Visibility.Collapsed : Visibility.Visible;
            EditorStack.Visibility = ShowHeader ? Visibility.Visible : Visibility.Collapsed;
            EditorStack.Opacity = IsEditable ? 1.0 : 0.55;
            CompactEditorStack.Opacity = IsEditable ? 1.0 : 0.55;
            PresetStack.Visibility = CanUsePresets ? Visibility.Visible : Visibility.Collapsed;
            CompactPresetComboBox.Visibility = showCompactPreset ? Visibility.Visible : Visibility.Collapsed;
            CompactKindComboBox.Width = showCompactPreset ? 132 : 180;
            SummaryBorder.Visibility = ShowHeader ? Visibility.Visible : Visibility.Collapsed;
            KindComboBox.IsEnabled = IsEditable;
            PresetComboBox.IsEnabled = IsEditable && CanUsePresets;
            CompactKindComboBox.IsEnabled = IsEditable;
            CompactPresetComboBox.IsEnabled = IsEditable && CanUsePresets;

            HintTextBlock.Text = !IsEditable
                ? "Select a single SVG group, frame, or section to edit the container."
                : ContainerKind switch
                {
                    FrameContainerKind.Frame => "Frames are SVG groups with a background rect, preset sizing, clipping, and auto layout support.",
                    FrameContainerKind.Section => "Sections are SVG-backed organizational containers. They keep the background bounds but do not run auto layout.",
                    _ => "Groups keep the raw SVG hierarchy with no generated background or preset sizing."
                };

            var kindItem = KindComboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag as string, ContainerKind.ToString(), StringComparison.OrdinalIgnoreCase));
            KindComboBox.SelectedItem = kindItem;
            CompactKindComboBox.SelectedItem = CompactKindComboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag as string, ContainerKind.ToString(), StringComparison.OrdinalIgnoreCase));

            PresetComboBox.ItemsSource = Presets;
            PresetComboBox.SelectedValue = SelectedPresetId;
            CompactPresetComboBox.ItemsSource = Presets;
            CompactPresetComboBox.SelectedValue = SelectedPresetId;
        }
        finally
        {
            _isUpdatingState = false;
        }
    }
}
