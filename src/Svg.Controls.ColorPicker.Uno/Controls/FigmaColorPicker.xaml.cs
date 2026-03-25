using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Svg.Controls.ColorPicker.Uno.Models;
using Windows.Foundation;
using Windows.System;
using Windows.UI;

namespace Svg.Controls.ColorPicker.Uno.Controls;

public sealed partial class FigmaColorPicker : UserControl
{
    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color),
            typeof(FigmaColorPicker),
            new PropertyMetadata(Color.FromArgb(255, 224, 65, 65), OnSelectedColorChanged));

    public static readonly DependencyProperty SwatchesProperty =
        DependencyProperty.Register(
            nameof(Swatches),
            typeof(IEnumerable<ColorSwatchItem>),
            typeof(FigmaColorPicker),
            new PropertyMetadata(null, OnSwatchesChanged));

    public static readonly DependencyProperty LibraryStylesProperty =
        DependencyProperty.Register(
            nameof(LibraryStyles),
            typeof(IEnumerable<ColorSwatchItem>),
            typeof(FigmaColorPicker),
            new PropertyMetadata(null, OnLibraryStylesChanged));

    public static readonly DependencyProperty PaintTargetProperty =
        DependencyProperty.Register(
            nameof(PaintTarget),
            typeof(PaintStyleTarget),
            typeof(FigmaColorPicker),
            new PropertyMetadata(PaintStyleTarget.Fill, OnLibraryStylesChanged));

    public static readonly DependencyProperty CurrentStrokeWidthTextProperty =
        DependencyProperty.Register(
            nameof(CurrentStrokeWidthText),
            typeof(string),
            typeof(FigmaColorPicker),
            new PropertyMetadata("1"));

    public static readonly DependencyProperty ShowAddButtonProperty =
        DependencyProperty.Register(
            nameof(ShowAddButton),
            typeof(bool),
            typeof(FigmaColorPicker),
            new PropertyMetadata(true, OnPickerChromePropertyChanged));

    public static readonly DependencyProperty ShowCloseButtonProperty =
        DependencyProperty.Register(
            nameof(ShowCloseButton),
            typeof(bool),
            typeof(FigmaColorPicker),
            new PropertyMetadata(true, OnPickerChromePropertyChanged));

    public static readonly DependencyProperty SelectedPaintModeProperty =
        DependencyProperty.Register(
            nameof(SelectedPaintMode),
            typeof(ColorPickerPaintMode),
            typeof(FigmaColorPicker),
            new PropertyMetadata(ColorPickerPaintMode.Solid, OnSelectedPaintModeChanged));

    public static readonly DependencyProperty IsEyeDropperActiveProperty =
        DependencyProperty.Register(
            nameof(IsEyeDropperActive),
            typeof(bool),
            typeof(FigmaColorPicker),
            new PropertyMetadata(false, OnEyeDropperStateChanged));

    private sealed class GradientModeState
    {
        public ObservableCollection<ColorPickerGradientStop> Stops { get; } = [];

        public int SelectedIndex { get; set; }

        public double Angle { get; set; } = 45.0;

        public double CenterX { get; set; } = 0.5;

        public double CenterY { get; set; } = 0.45;

        public double Radius { get; set; } = 0.78;
    }

    private sealed class AssetModeState
    {
        public string Source { get; set; } = string.Empty;

        public string Option { get; set; } = string.Empty;

        public string SecondaryText { get; set; } = string.Empty;

        public bool Toggle { get; set; } = true;
    }

    private readonly ObservableCollection<ColorSwatchItem> _defaultSwatches =
        new(
        [
            new ColorSwatchItem(Color.FromArgb(255, 0, 0, 0)),
            new ColorSwatchItem(Color.FromArgb(255, 248, 214, 104)),
            new ColorSwatchItem(Color.FromArgb(255, 92, 193, 126)),
            new ColorSwatchItem(Color.FromArgb(255, 211, 211, 211)),
            new ColorSwatchItem(Color.FromArgb(255, 255, 255, 255)),
            new ColorSwatchItem(Color.FromArgb(255, 253, 213, 106)),
            new ColorSwatchItem(Color.FromArgb(255, 250, 44, 60)),
            new ColorSwatchItem(Color.FromArgb(255, 203, 203, 203)),
            new ColorSwatchItem(Color.FromArgb(255, 198, 48, 59)),
            new ColorSwatchItem(Color.FromArgb(255, 117, 47, 230)),
            new ColorSwatchItem(Color.FromArgb(255, 24, 190, 171))
        ]);

    private readonly ObservableCollection<ColorSwatchItem> _visibleLibraryStyles = [];
    private readonly ObservableCollection<ColorSwatchItem> _eyeDropperPalette = [];
    private readonly Dictionary<ColorPickerPaintMode, GradientModeState> _gradientStates = new()
    {
        [ColorPickerPaintMode.LinearGradient] = new GradientModeState(),
        [ColorPickerPaintMode.RadialGradient] = new GradientModeState()
    };

    private readonly Dictionary<ColorPickerPaintMode, AssetModeState> _assetStates = new()
    {
        [ColorPickerPaintMode.ImagePaint] = new AssetModeState { Option = "Fill", Toggle = true },
        [ColorPickerPaintMode.VideoPaint] = new AssetModeState { Option = "Loop", Toggle = true },
        [ColorPickerPaintMode.ImageAsset] = new AssetModeState { Option = "Tile", Toggle = false }
    };

    private bool _isInternalUpdate;
    private bool _isLibrariesTab;
    private bool _isUpdatingLibrarySources;
    private bool _isUpdatingSelectedColorFromModeState;
    private bool _isUpdatingGradientUi;
    private bool _isUpdatingAssetUi;
    private ColorValueDisplayMode _displayMode = ColorValueDisplayMode.Rgb;
    private double _hue;
    private double _saturation;
    private double _value = 1.0;
    private double _alpha = 1.0;

    public FigmaColorPicker()
    {
        InitializeComponent();

        SpectrumCanvas.SelectionChanged += OnSpectrumSelectionChanged;
        HueSlider.HueChanged += OnHueChanged;
        AlphaSlider.AlphaChanged += OnAlphaChanged;

        FormatComboBox.SelectedIndex = 0;
        PaletteSourceComboBox.SelectedIndex = 0;
        LibrariesStylesListView.ItemsSource = _visibleLibraryStyles;
        EyeDropperPaletteGrid.ItemsSource = _eyeDropperPalette;

        ApplyColorToControls(SelectedColor);
        UpdateSwatches();
        UpdateLibraryStyles();
        UpdateTabState();
        UpdateChromeVisibility();
        UpdatePaintModeState(pushSelectedColor: false);
        UpdateEyeDropperState();
    }

    public event EventHandler? CloseRequested;

    public event EventHandler<PaintStyleRequestedEventArgs>? PaintStyleRequested;

    public event EventHandler<PaintStyleCreateRequestedEventArgs>? CreateStyleRequested;

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
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

    public bool ShowAddButton
    {
        get => (bool)GetValue(ShowAddButtonProperty);
        set => SetValue(ShowAddButtonProperty, value);
    }

    public bool ShowCloseButton
    {
        get => (bool)GetValue(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    public ColorPickerPaintMode SelectedPaintMode
    {
        get => (ColorPickerPaintMode)GetValue(SelectedPaintModeProperty);
        set => SetValue(SelectedPaintModeProperty, value);
    }

    public bool IsEyeDropperActive
    {
        get => (bool)GetValue(IsEyeDropperActiveProperty);
        set => SetValue(IsEyeDropperActiveProperty, value);
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (FigmaColorPicker)d;
        if (e.NewValue is Color color)
        {
            picker.ApplyColorToControls(color);
            picker.SyncPaintStateFromSelectedColor(color);
            picker.UpdatePaintPreview();
            picker.RebuildEyeDropperPalette();
        }
    }

    private static void OnSwatchesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (FigmaColorPicker)d;
        picker.UpdateSwatches();
        picker.RebuildEyeDropperPalette();
    }

    private static void OnLibraryStylesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (FigmaColorPicker)d;
        picker.UpdateLibraryStyles();
        picker.RebuildEyeDropperPalette();
    }

    private static void OnPickerChromePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FigmaColorPicker)d).UpdateChromeVisibility();
    }

    private static void OnSelectedPaintModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FigmaColorPicker)d).UpdatePaintModeState();
    }

    private static void OnEyeDropperStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FigmaColorPicker)d).UpdateEyeDropperState();
    }

    private void OnCustomTabClick(object sender, RoutedEventArgs e)
    {
        _isLibrariesTab = false;
        UpdateTabState();
    }

    private void OnLibrariesTabClick(object sender, RoutedEventArgs e)
    {
        _isLibrariesTab = true;
        IsEyeDropperActive = false;
        UpdateTabState();
    }

    private void OnPaintModeButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string rawMode }
            || !Enum.TryParse<ColorPickerPaintMode>(rawMode, out var mode))
        {
            return;
        }

        SelectedPaintMode = mode;
        IsEyeDropperActive = false;
    }

    private void OnEyeDropperEnableClick(object sender, RoutedEventArgs e)
    {
        IsEyeDropperActive = true;
    }

    private void OnEyeDropperDisableClick(object sender, RoutedEventArgs e)
    {
        IsEyeDropperActive = false;
    }

    private void OnInlineEyeDropperClick(object sender, RoutedEventArgs e)
    {
        IsEyeDropperActive = !IsEyeDropperActive;
    }

    private void OnAddSwatchClick(object sender, RoutedEventArgs e)
    {
        if (_isLibrariesTab)
        {
            CreateStyleRequested?.Invoke(
                this,
                new PaintStyleCreateRequestedEventArgs(
                    SelectedColor,
                    PaintTarget,
                    ParseStrokeWidth(CurrentStrokeWidthText),
                    SelectedPaintMode));
            return;
        }

        var collection = Swatches as ObservableCollection<ColorSwatchItem> ?? _defaultSwatches;
        var color = SelectedColor;
        var argb = (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
        if (!collection.Any(item =>
            ((item.Color.A << 24) | (item.Color.R << 16) | (item.Color.G << 8) | item.Color.B) == argb))
        {
            collection.Insert(0, new ColorSwatchItem(color, paintMode: SelectedPaintMode));
        }

        UpdateSwatches();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSpectrumSelectionChanged(object? sender, (double Saturation, double Value) e)
    {
        _saturation = e.Saturation;
        _value = e.Value;
        CommitColorFromState();
    }

    private void OnHueChanged(object? sender, double hue)
    {
        _hue = hue;
        CommitColorFromState();
    }

    private void OnAlphaChanged(object? sender, double alpha)
    {
        _alpha = alpha;
        CommitColorFromState();
    }

    private void OnFormatSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _displayMode = FormatComboBox.SelectedItem switch
        {
            ComboBoxItem item when string.Equals(item.Tag as string, "Hsl", StringComparison.OrdinalIgnoreCase) => ColorValueDisplayMode.Hsl,
            ComboBoxItem item when string.Equals(item.Tag as string, "Hex", StringComparison.OrdinalIgnoreCase) => ColorValueDisplayMode.Hex,
            _ => ColorValueDisplayMode.Rgb
        };

        NumericFieldsBorder.Visibility = _displayMode == ColorValueDisplayMode.Hex ? Visibility.Collapsed : Visibility.Visible;
        HexFieldsBorder.Visibility = _displayMode == ColorValueDisplayMode.Hex ? Visibility.Visible : Visibility.Collapsed;
        UpdateFieldValues();
    }

    private void OnChannelLostFocus(object sender, RoutedEventArgs e)
    {
        CommitFromTextFields();
    }

    private void OnChannelKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            CommitFromTextFields();
        }
    }

    private void OnHexLostFocus(object sender, RoutedEventArgs e)
    {
        CommitFromHexFields();
    }

    private void OnHexKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            CommitFromHexFields();
        }
    }

    private void OnSwatchItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ColorSwatchItem swatch)
        {
            return;
        }

        SelectedPaintMode = swatch.PaintMode;
        SelectedColor = swatch.Color;
        IsEyeDropperActive = false;
    }

    private void OnGradientStopItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ColorPickerGradientStop stop)
        {
            return;
        }

        var state = GetActiveGradientState();
        if (state is null)
        {
            return;
        }

        var index = state.Stops.IndexOf(stop);
        if (index < 0)
        {
            return;
        }

        state.SelectedIndex = index;
        ApplyGradientSelection(pushSelectedColor: true);
        UpdatePaintPreview();
    }

    private void OnAddGradientStopClick(object sender, RoutedEventArgs e)
    {
        var state = GetActiveGradientState();
        if (state is null)
        {
            return;
        }

        EnsureGradientStateInitialized(state, SelectedColor);
        if (state.Stops.Count == 0)
        {
            return;
        }

        var selected = GetSelectedGradientStop(state) ?? state.Stops[0];
        var selectedIndex = Math.Max(0, state.Stops.IndexOf(selected));
        var nextOffset = selectedIndex < state.Stops.Count - 1
            ? state.Stops[selectedIndex + 1].Offset
            : 1.0;
        var newOffset = (selected.Offset + nextOffset) / 2.0;
        if (Math.Abs(newOffset - selected.Offset) < 0.01)
        {
            newOffset = Math.Min(selected.Offset + 0.1, 1.0);
        }

        var stop = new ColorPickerGradientStop(selected.Color, newOffset);
        state.Stops.Insert(selectedIndex + 1, stop);
        SortGradientStops(state, stop);
        UpdateGradientStopLabels(state.Stops);
        ApplyGradientSelection(pushSelectedColor: true);
        UpdatePaintPreview();
    }

    private void OnRemoveGradientStopClick(object sender, RoutedEventArgs e)
    {
        var state = GetActiveGradientState();
        if (state is null || state.Stops.Count <= 2)
        {
            return;
        }

        var selected = GetSelectedGradientStop(state);
        if (selected is null)
        {
            return;
        }

        var selectedIndex = Math.Max(0, state.Stops.IndexOf(selected));
        state.Stops.Remove(selected);
        state.SelectedIndex = Math.Clamp(selectedIndex - 1, 0, state.Stops.Count - 1);
        UpdateGradientStopLabels(state.Stops);
        ApplyGradientSelection(pushSelectedColor: true);
        UpdatePaintPreview();
    }

    private void OnGradientStopOffsetChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingGradientUi)
        {
            return;
        }

        var state = GetActiveGradientState();
        var selected = state is null ? null : GetSelectedGradientStop(state);
        if (state is null || selected is null)
        {
            return;
        }

        var selectedIndex = state.Stops.IndexOf(selected);
        var min = selectedIndex > 0 ? state.Stops[selectedIndex - 1].Offset + 0.01 : 0.0;
        var max = selectedIndex < state.Stops.Count - 1 ? state.Stops[selectedIndex + 1].Offset - 0.01 : 1.0;
        selected.Offset = Math.Clamp(e.NewValue / 100.0, min, max);
        SortGradientStops(state, selected);
        UpdateGradientStopLabels(state.Stops);
        ApplyGradientSelection(pushSelectedColor: false);
        UpdatePaintPreview();
    }

    private void OnLinearAngleChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingGradientUi || !_gradientStates.TryGetValue(ColorPickerPaintMode.LinearGradient, out var state))
        {
            return;
        }

        state.Angle = e.NewValue;
        UpdatePaintPreview();
    }

    private void OnRadialCenterXChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingGradientUi || !_gradientStates.TryGetValue(ColorPickerPaintMode.RadialGradient, out var state))
        {
            return;
        }

        state.CenterX = Math.Clamp(e.NewValue / 100.0, 0.0, 1.0);
        UpdatePaintPreview();
    }

    private void OnRadialCenterYChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingGradientUi || !_gradientStates.TryGetValue(ColorPickerPaintMode.RadialGradient, out var state))
        {
            return;
        }

        state.CenterY = Math.Clamp(e.NewValue / 100.0, 0.0, 1.0);
        UpdatePaintPreview();
    }

    private void OnRadialRadiusChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingGradientUi || !_gradientStates.TryGetValue(ColorPickerPaintMode.RadialGradient, out var state))
        {
            return;
        }

        state.Radius = Math.Clamp(e.NewValue / 100.0, 0.1, 1.0);
        UpdatePaintPreview();
    }

    private void OnAssetSourceTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingAssetUi)
        {
            return;
        }

        var state = GetActiveAssetState();
        if (state is null)
        {
            return;
        }

        state.Source = AssetSourceTextBox.Text ?? string.Empty;
        UpdatePaintPreview();
    }

    private void OnAssetOptionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingAssetUi)
        {
            return;
        }

        var state = GetActiveAssetState();
        if (state is null)
        {
            return;
        }

        state.Option = (AssetOptionComboBox.SelectedItem as ComboBoxItem)?.Content as string ?? state.Option;
        UpdatePaintPreview();
    }

    private void OnAssetSecondaryTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingAssetUi)
        {
            return;
        }

        var state = GetActiveAssetState();
        if (state is null)
        {
            return;
        }

        state.SecondaryText = AssetSecondaryTextBox.Text ?? string.Empty;
        UpdatePaintPreview();
    }

    private void OnAssetToggleChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingAssetUi)
        {
            return;
        }

        var state = GetActiveAssetState();
        if (state is null)
        {
            return;
        }

        state.Toggle = AssetToggleCheckBox.IsChecked == true;
        UpdatePaintPreview();
    }

    private void OnEyeDropperPaletteItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ColorSwatchItem swatch)
        {
            return;
        }

        SelectedColor = swatch.Color;
        IsEyeDropperActive = false;
    }

    private void OnLibrarySearchTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateLibraryStyles();
    }

    private void OnLibrarySourceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingLibrarySources)
        {
            return;
        }

        UpdateLibraryStyles();
    }

    private void OnLibraryStyleItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ColorSwatchItem swatch)
        {
            return;
        }

        SelectedPaintMode = swatch.PaintMode;
        SelectedColor = swatch.Color;
        IsEyeDropperActive = false;
        PaintStyleRequested?.Invoke(this, new PaintStyleRequestedEventArgs(swatch, PaintTarget));
    }

    private void ApplyColorToControls(Color color)
    {
        if (_isInternalUpdate)
        {
            return;
        }

        var hsv = ColorPickerColorHelper.ToHsv(color);
        _hue = hsv.Hue;
        _saturation = hsv.Saturation;
        _value = hsv.Value;
        _alpha = color.A / 255.0;

        SpectrumCanvas.Hue = _hue;
        SpectrumCanvas.Saturation = _saturation;
        SpectrumCanvas.Value = _value;

        HueSlider.Hue = _hue;
        AlphaSlider.BaseColor = Color.FromArgb(255, color.R, color.G, color.B);
        AlphaSlider.Alpha = _alpha;

        UpdateFieldValues();
    }

    private void CommitColorFromState()
    {
        _isInternalUpdate = true;
        try
        {
            SelectedColor = ColorPickerColorHelper.FromHsv(_hue, _saturation, _value, _alpha);
        }
        finally
        {
            _isInternalUpdate = false;
        }

        ApplyColorToControls(SelectedColor);
    }

    private void CommitFromTextFields()
    {
        if (_displayMode == ColorValueDisplayMode.Hex)
        {
            CommitFromHexFields();
            return;
        }

        if (!TryParseDouble(Channel1TextBox.Text, out var first)
            || !TryParseDouble(Channel2TextBox.Text, out var second)
            || !TryParseDouble(Channel3TextBox.Text, out var third)
            || !TryParseDouble(AlphaPercentTextBox.Text, out var alphaPercent))
        {
            UpdateFieldValues();
            return;
        }

        switch (_displayMode)
        {
            case ColorValueDisplayMode.Rgb:
                SelectedColor = Color.FromArgb(
                    ColorPickerColorHelper.FromPercent(alphaPercent),
                    (byte)Math.Clamp((int)Math.Round(first), 0, 255),
                    (byte)Math.Clamp((int)Math.Round(second), 0, 255),
                    (byte)Math.Clamp((int)Math.Round(third), 0, 255));
                break;
            case ColorValueDisplayMode.Hsl:
                SelectedColor = ColorPickerColorHelper.FromHsl(
                    Math.Clamp(first, 0.0, 360.0),
                    Math.Clamp(second / 100.0, 0.0, 1.0),
                    Math.Clamp(third / 100.0, 0.0, 1.0),
                    Math.Clamp(alphaPercent / 100.0, 0.0, 1.0));
                break;
        }
    }

    private void CommitFromHexFields()
    {
        if (!TryParseDouble(HexAlphaPercentTextBox.Text, out var alphaPercent)
            || !ColorPickerColorHelper.TryParseHex(HexTextBox.Text, ColorPickerColorHelper.FromPercent(alphaPercent), out var color))
        {
            UpdateFieldValues();
            return;
        }

        SelectedColor = color;
    }

    private void UpdateFieldValues()
    {
        switch (_displayMode)
        {
            case ColorValueDisplayMode.Hsl:
                var hsl = ColorPickerColorHelper.ToHsl(SelectedColor);
                Channel1TextBox.Text = Math.Round(hsl.Hue).ToString(CultureInfo.InvariantCulture);
                Channel2TextBox.Text = Math.Round(hsl.Saturation * 100.0).ToString(CultureInfo.InvariantCulture);
                Channel3TextBox.Text = Math.Round(hsl.Lightness * 100.0).ToString(CultureInfo.InvariantCulture);
                AlphaPercentTextBox.Text = ColorPickerColorHelper.ToPercent(SelectedColor.A).ToString(CultureInfo.InvariantCulture);
                HexTextBox.Text = ColorPickerColorHelper.ToHexRgb(SelectedColor);
                HexAlphaPercentTextBox.Text = ColorPickerColorHelper.ToPercent(SelectedColor.A).ToString(CultureInfo.InvariantCulture);
                break;
            case ColorValueDisplayMode.Hex:
                HexTextBox.Text = ColorPickerColorHelper.ToHexRgb(SelectedColor);
                HexAlphaPercentTextBox.Text = ColorPickerColorHelper.ToPercent(SelectedColor.A).ToString(CultureInfo.InvariantCulture);
                break;
            default:
                Channel1TextBox.Text = SelectedColor.R.ToString(CultureInfo.InvariantCulture);
                Channel2TextBox.Text = SelectedColor.G.ToString(CultureInfo.InvariantCulture);
                Channel3TextBox.Text = SelectedColor.B.ToString(CultureInfo.InvariantCulture);
                AlphaPercentTextBox.Text = ColorPickerColorHelper.ToPercent(SelectedColor.A).ToString(CultureInfo.InvariantCulture);
                HexTextBox.Text = ColorPickerColorHelper.ToHexRgb(SelectedColor);
                HexAlphaPercentTextBox.Text = ColorPickerColorHelper.ToPercent(SelectedColor.A).ToString(CultureInfo.InvariantCulture);
                break;
        }
    }

    private void SyncPaintStateFromSelectedColor(Color color)
    {
        if (_isUpdatingSelectedColorFromModeState)
        {
            return;
        }

        var gradientState = GetActiveGradientState();
        if (gradientState is null)
        {
            return;
        }

        EnsureGradientStateInitialized(gradientState, color);
        var selectedStop = GetSelectedGradientStop(gradientState);
        if (selectedStop is null)
        {
            return;
        }

        selectedStop.Color = color;
        ApplyGradientSelection(pushSelectedColor: false);
    }

    private void UpdateSwatches()
    {
        var source = (Swatches is IEnumerable<ColorSwatchItem> swatches && swatches.Any())
            ? swatches
            : _defaultSwatches;

        SwatchGrid.ItemsSource = source;
    }

    private void UpdateLibraryStyles()
    {
        var styles = (LibraryStyles ?? Enumerable.Empty<ColorSwatchItem>())
            .Where(style => PaintTarget switch
            {
                PaintStyleTarget.Fill => style.SupportsFill,
                PaintStyleTarget.Stroke => style.SupportsStroke,
                _ => true
            })
            .ToList();

        var selectedLibraryId = RebuildLibrarySourceItems(styles, GetSelectedLibraryId());
        var search = LibrarySearchTextBox.Text?.Trim();
        IEnumerable<ColorSwatchItem> filtered = styles;
        if (!string.IsNullOrWhiteSpace(selectedLibraryId))
        {
            filtered = filtered.Where(style => string.Equals(style.LibraryId, selectedLibraryId, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(style => style.SearchText.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        _visibleLibraryStyles.Clear();
        foreach (var style in filtered.OrderBy(style => style.LibraryName, StringComparer.OrdinalIgnoreCase)
                                       .ThenBy(style => style.SectionName, StringComparer.OrdinalIgnoreCase)
                                       .ThenBy(style => style.Label, StringComparer.OrdinalIgnoreCase))
        {
            _visibleLibraryStyles.Add(style);
        }

        LibrariesEmptyStateText.Visibility = _visibleLibraryStyles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private string? GetSelectedLibraryId()
    {
        return (LibrariesSourceComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
    }

    private string? RebuildLibrarySourceItems(List<ColorSwatchItem> styles, string? selectedLibraryId)
    {
        var libraryGroups = styles
            .Where(style => style.IsLibraryStyle)
            .GroupBy(style => style.LibraryId)
            .Select(group => new
            {
                LibraryId = group.Key,
                Label = group.First().LibraryName
            })
            .OrderBy(group => group.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var desiredSelection = selectedLibraryId;
        _isUpdatingLibrarySources = true;
        try
        {
            LibrariesSourceComboBox.Items.Clear();
            LibrariesSourceComboBox.Items.Add(new ComboBoxItem
            {
                Content = "All libraries",
                Tag = null
            });

            foreach (var group in libraryGroups)
            {
                LibrariesSourceComboBox.Items.Add(new ComboBoxItem
                {
                    Content = group.Label,
                    Tag = group.LibraryId
                });
            }

            var selection = LibrariesSourceComboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag as string, desiredSelection, StringComparison.Ordinal))
                ?? LibrariesSourceComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();

            if (!ReferenceEquals(LibrariesSourceComboBox.SelectedItem, selection))
            {
                LibrariesSourceComboBox.SelectedItem = selection;
            }

            return selection?.Tag as string;
        }
        finally
        {
            _isUpdatingLibrarySources = false;
        }
    }

    private void UpdateTabState()
    {
        var activeBrush = Resources["PickerChipBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(255, 242, 242, 240));
        var transparentBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        CustomTabBorder.Background = _isLibrariesTab ? transparentBrush : activeBrush;
        LibrariesTabBorder.Background = _isLibrariesTab ? activeBrush : transparentBrush;

        CustomPanel.Visibility = _isLibrariesTab ? Visibility.Collapsed : Visibility.Visible;
        LibrariesPanel.Visibility = _isLibrariesTab ? Visibility.Visible : Visibility.Collapsed;
        UpdateEyeDropperState();
    }

    private void UpdateChromeVisibility()
    {
        if (AddSwatchButton is not null)
        {
            AddSwatchButton.Visibility = ShowAddButton ? Visibility.Visible : Visibility.Collapsed;
        }

        if (ClosePickerButton is not null)
        {
            ClosePickerButton.Visibility = ShowCloseButton ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdatePaintModeState(bool pushSelectedColor = true)
    {
        if (PaintModeTitleText is null)
        {
            return;
        }

        var gradientState = GetActiveGradientState();
        if (gradientState is not null)
        {
            EnsureGradientStateInitialized(gradientState, SelectedColor);
            ApplyGradientSelection(pushSelectedColor);
        }
        else if (GradientStopsGrid is not null)
        {
            GradientStopsGrid.ItemsSource = null;
            SelectedGradientStopText.Text = "Switch to a gradient mode to edit multiple color stops.";
            GradientStopOffsetSlider.IsEnabled = false;
            RemoveGradientStopButton.IsEnabled = false;
        }

        UpdatePaintModeButtons();
        UpdateGradientPanels();
        UpdateAssetPanels();
        UpdatePaintPreview();
        RebuildEyeDropperPalette();
    }

    private void UpdatePaintModeButtons()
    {
        ApplyToolbarButtonState(SolidModeBorder, SelectedPaintMode == ColorPickerPaintMode.Solid);
        ApplyToolbarButtonState(LinearGradientModeBorder, SelectedPaintMode == ColorPickerPaintMode.LinearGradient);
        ApplyToolbarButtonState(RadialGradientModeBorder, SelectedPaintMode == ColorPickerPaintMode.RadialGradient);
        ApplyToolbarButtonState(ImagePaintModeBorder, SelectedPaintMode == ColorPickerPaintMode.ImagePaint);
        ApplyToolbarButtonState(VideoPaintModeBorder, SelectedPaintMode == ColorPickerPaintMode.VideoPaint);
        ApplyToolbarButtonState(ImageAssetModeBorder, SelectedPaintMode == ColorPickerPaintMode.ImageAsset);
        ApplyToolbarButtonState(EyeDropperEnableBorder, !IsEyeDropperActive);
        ApplyToolbarButtonState(EyeDropperDisableBorder, IsEyeDropperActive);
    }

    private void UpdateGradientPanels()
    {
        var isGradient = ColorPickerPaintModeHelper.IsGradientMode(SelectedPaintMode);
        GradientToolsPanel.Visibility = isGradient ? Visibility.Visible : Visibility.Collapsed;
        LinearGradientSettingsPanel.Visibility = SelectedPaintMode == ColorPickerPaintMode.LinearGradient ? Visibility.Visible : Visibility.Collapsed;
        RadialGradientSettingsPanel.Visibility = SelectedPaintMode == ColorPickerPaintMode.RadialGradient ? Visibility.Visible : Visibility.Collapsed;

        if (!isGradient)
        {
            return;
        }

        var state = GetActiveGradientState();
        if (state is null)
        {
            return;
        }

        _isUpdatingGradientUi = true;
        try
        {
            LinearAngleSlider.Value = state.Angle;
            RadialCenterXSlider.Value = state.CenterX * 100.0;
            RadialCenterYSlider.Value = state.CenterY * 100.0;
            RadialRadiusSlider.Value = state.Radius * 100.0;
        }
        finally
        {
            _isUpdatingGradientUi = false;
        }
    }

    private void UpdateAssetPanels()
    {
        var isAsset = ColorPickerPaintModeHelper.IsAssetMode(SelectedPaintMode);
        AssetToolsPanel.Visibility = isAsset ? Visibility.Visible : Visibility.Collapsed;
        if (!isAsset)
        {
            return;
        }

        UpdateAssetControlContent();
    }

    private void UpdateEyeDropperState()
    {
        if (EyeDropperPanel is null)
        {
            return;
        }

        EyeDropperPanel.Visibility = !_isLibrariesTab && IsEyeDropperActive ? Visibility.Visible : Visibility.Collapsed;
        InlineEyeDropperIcon.Kind = IsEyeDropperActive ? PickerIconKind.DropletOff : PickerIconKind.Droplet;
        InlineEyeDropperButton.Background = GetResourceBrush(IsEyeDropperActive ? "PickerSectionBrush" : "PickerChipBrush", Color.FromArgb(255, 242, 244, 247));
        InlineEyeDropperButton.BorderBrush = GetResourceBrush(IsEyeDropperActive ? "TextBrush" : "PickerDividerBrush", Color.FromArgb(255, 228, 231, 236));
        InlineEyeDropperButton.BorderThickness = IsEyeDropperActive ? new Thickness(1.2) : new Thickness(1.0);
        InlineEyeDropperButton.Opacity = IsEyeDropperActive ? 1.0 : 0.86;
        RebuildEyeDropperPalette();
    }

    private void UpdatePaintPreview()
    {
        if (PaintModeTitleText is null)
        {
            return;
        }

        PaintModeTitleText.Text = ColorPickerPaintModeHelper.GetDisplayName(SelectedPaintMode);
        PaintModeSubtitleText.Text = GetPaintModeSubtitle(SelectedPaintMode);
        PaintModePreviewIcon.Kind = GetPaintModeIcon(SelectedPaintMode);
        PaintModePreviewIcon.IconFill = CreateIconFillBrush();
        PaintPreviewSurface.Background = CreatePreviewBrush();
        PaintPreviewPrimaryText.Text = GetPaintPreviewPrimaryText();
        PaintPreviewSecondaryText.Text = GetPaintPreviewSecondaryText();
    }

    private void UpdateAssetControlContent()
    {
        var state = GetActiveAssetState();
        if (state is null)
        {
            return;
        }

        _isUpdatingAssetUi = true;
        try
        {
            AssetPanelCaptionText.Text = ColorPickerPaintModeHelper.GetDisplayName(SelectedPaintMode);
            AssetPanelHintText.Text = GetAssetPanelHintText(SelectedPaintMode);
            AssetSourceLabelText.Text = GetAssetSourceLabel(SelectedPaintMode);
            AssetSourceTextBox.PlaceholderText = GetAssetSourcePlaceholder(SelectedPaintMode);
            AssetSourceTextBox.Text = state.Source;
            AssetOptionLabelText.Text = GetAssetOptionLabel(SelectedPaintMode);
            AssetSecondaryLabelText.Text = GetAssetSecondaryLabel(SelectedPaintMode);
            AssetSecondaryTextBox.PlaceholderText = GetAssetSecondaryPlaceholder(SelectedPaintMode);
            AssetSecondaryTextBox.Text = state.SecondaryText;
            AssetToggleCheckBox.Content = GetAssetToggleLabel(SelectedPaintMode);
            AssetToggleCheckBox.IsChecked = state.Toggle;
            RebuildAssetOptions(GetAssetOptions(SelectedPaintMode), state.Option);
        }
        finally
        {
            _isUpdatingAssetUi = false;
        }
    }

    private void RebuildAssetOptions(IEnumerable<string> options, string selectedOption)
    {
        AssetOptionComboBox.Items.Clear();
        foreach (var option in options)
        {
            AssetOptionComboBox.Items.Add(new ComboBoxItem { Content = option });
        }

        var selection = AssetOptionComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content as string, selectedOption, StringComparison.Ordinal))
            ?? AssetOptionComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
        AssetOptionComboBox.SelectedItem = selection;
    }

    private void RebuildEyeDropperPalette()
    {
        if (EyeDropperPaletteGrid is null)
        {
            return;
        }

        _eyeDropperPalette.Clear();
        var source = ColorPickerPaintModeHelper.BuildEyeDropperPalette(
            SelectedColor,
            (Swatches is IEnumerable<ColorSwatchItem> swatches && swatches.Any()) ? swatches : _defaultSwatches,
            LibraryStyles);
        foreach (var swatch in source.Take(18))
        {
            _eyeDropperPalette.Add(swatch);
        }
    }

    private GradientModeState? GetActiveGradientState()
    {
        return _gradientStates.TryGetValue(SelectedPaintMode, out var state) ? state : null;
    }

    private AssetModeState? GetActiveAssetState()
    {
        return _assetStates.TryGetValue(SelectedPaintMode, out var state) ? state : null;
    }

    private void EnsureGradientStateInitialized(GradientModeState state, Color baseColor)
    {
        if (state.Stops.Count > 0)
        {
            return;
        }

        foreach (var stop in ColorPickerPaintModeHelper.CreateDefaultGradientStops(baseColor))
        {
            state.Stops.Add(stop);
        }

        state.SelectedIndex = 0;
        UpdateGradientStopLabels(state.Stops);
    }

    private void ApplyGradientSelection(bool pushSelectedColor)
    {
        var state = GetActiveGradientState();
        if (state is null)
        {
            return;
        }

        EnsureGradientStateInitialized(state, SelectedColor);
        if (state.Stops.Count == 0)
        {
            return;
        }

        state.SelectedIndex = Math.Clamp(state.SelectedIndex, 0, state.Stops.Count - 1);
        for (var index = 0; index < state.Stops.Count; index++)
        {
            state.Stops[index].IsSelected = index == state.SelectedIndex;
        }

        GradientStopsGrid.ItemsSource = state.Stops;
        GradientStopOffsetSlider.IsEnabled = true;
        RemoveGradientStopButton.IsEnabled = state.Stops.Count > 2;

        var selected = state.Stops[state.SelectedIndex];
        SelectedGradientStopText.Text = selected.Summary;

        _isUpdatingGradientUi = true;
        try
        {
            GradientStopOffsetSlider.Value = selected.Offset * 100.0;
        }
        finally
        {
            _isUpdatingGradientUi = false;
        }

        if (!pushSelectedColor)
        {
            return;
        }

        _isUpdatingSelectedColorFromModeState = true;
        try
        {
            SelectedColor = selected.Color;
        }
        finally
        {
            _isUpdatingSelectedColorFromModeState = false;
        }
    }

    private ColorPickerGradientStop? GetSelectedGradientStop(GradientModeState state)
    {
        if (state.Stops.Count == 0)
        {
            return null;
        }

        state.SelectedIndex = Math.Clamp(state.SelectedIndex, 0, state.Stops.Count - 1);
        return state.Stops[state.SelectedIndex];
    }

    private void SortGradientStops(GradientModeState state, ColorPickerGradientStop? preferredSelection = null)
    {
        var ordered = state.Stops.OrderBy(stop => stop.Offset).ToList();
        if (!ordered.SequenceEqual(state.Stops))
        {
            state.Stops.Clear();
            foreach (var stop in ordered)
            {
                state.Stops.Add(stop);
            }
        }

        if (preferredSelection is null)
        {
            state.SelectedIndex = Math.Clamp(state.SelectedIndex, 0, Math.Max(state.Stops.Count - 1, 0));
            return;
        }

        state.SelectedIndex = Math.Max(0, state.Stops.IndexOf(preferredSelection));
    }

    private static void UpdateGradientStopLabels(IList<ColorPickerGradientStop> stops)
    {
        for (var index = 0; index < stops.Count; index++)
        {
            stops[index].Name = index switch
            {
                0 => "Start",
                var last when last == stops.Count - 1 => "End",
                _ => $"Stop {index + 1}"
            };
        }
    }

    private Brush CreatePreviewBrush()
    {
        return SelectedPaintMode switch
        {
            ColorPickerPaintMode.LinearGradient => CreateLinearGradientPreviewBrush(),
            ColorPickerPaintMode.RadialGradient => CreateRadialGradientPreviewBrush(),
            ColorPickerPaintMode.ImagePaint or ColorPickerPaintMode.VideoPaint or ColorPickerPaintMode.ImageAsset => CreateAssetPreviewBrush(),
            _ => new SolidColorBrush(SelectedColor)
        };
    }

    private Brush CreateLinearGradientPreviewBrush()
    {
        var state = _gradientStates[ColorPickerPaintMode.LinearGradient];
        EnsureGradientStateInitialized(state, SelectedColor);

        var radians = state.Angle * Math.PI / 180.0;
        var dx = Math.Cos(radians) * 0.5;
        var dy = Math.Sin(radians) * 0.5;
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(Math.Clamp(0.5 - dx, 0.0, 1.0), Math.Clamp(0.5 + dy, 0.0, 1.0)),
            EndPoint = new Point(Math.Clamp(0.5 + dx, 0.0, 1.0), Math.Clamp(0.5 - dy, 0.0, 1.0))
        };

        foreach (var stop in state.Stops.OrderBy(stop => stop.Offset))
        {
            brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop
            {
                Color = stop.Color,
                Offset = stop.Offset
            });
        }

        return brush;
    }

    private Brush CreateRadialGradientPreviewBrush()
    {
        var state = _gradientStates[ColorPickerPaintMode.RadialGradient];
        EnsureGradientStateInitialized(state, SelectedColor);

        var brush = new RadialGradientBrush
        {
            Center = new Point(state.CenterX, state.CenterY),
            RadiusX = state.Radius,
            RadiusY = state.Radius
        };

        foreach (var stop in state.Stops.OrderBy(stop => stop.Offset))
        {
            brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop
            {
                Color = stop.Color,
                Offset = stop.Offset
            });
        }

        return brush;
    }

    private Brush CreateAssetPreviewBrush()
    {
        var accent = Color.FromArgb(220, SelectedColor.R, SelectedColor.G, SelectedColor.B);
        var tail = SelectedPaintMode switch
        {
            ColorPickerPaintMode.VideoPaint => Color.FromArgb(255, 17, 24, 39),
            ColorPickerPaintMode.ImagePaint => Color.FromArgb(255, 240, 244, 248),
            _ => Color.FromArgb(255, 228, 231, 236)
        };

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0.0, 0.0),
            EndPoint = new Point(1.0, 1.0)
        };
        brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = accent, Offset = 0.0 });
        brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = tail, Offset = 1.0 });
        return brush;
    }

    private Brush CreateIconFillBrush()
    {
        var fillAlpha = SelectedPaintMode == ColorPickerPaintMode.Solid ? (byte)255 : (byte)96;
        return new SolidColorBrush(Color.FromArgb(fillAlpha, SelectedColor.R, SelectedColor.G, SelectedColor.B));
    }

    private string GetPaintModeSubtitle(ColorPickerPaintMode mode) => mode switch
    {
        ColorPickerPaintMode.Solid => "Solid color fill with RGB, HSL, HEX, swatches, and libraries.",
        ColorPickerPaintMode.LinearGradient => "Edit multiple color stops and control the gradient angle.",
        ColorPickerPaintMode.RadialGradient => "Edit multiple color stops and tune the center and radius.",
        ColorPickerPaintMode.ImagePaint => "Configure an image-backed paint with crop and tint metadata.",
        ColorPickerPaintMode.VideoPaint => "Configure a video-backed paint with poster and playback metadata.",
        ColorPickerPaintMode.ImageAsset => "Configure a reusable image asset or texture reference.",
        _ => "Paint"
    };

    private string GetPaintPreviewPrimaryText()
    {
        return SelectedPaintMode switch
        {
            ColorPickerPaintMode.Solid => $"#{ColorPickerColorHelper.ToHexRgb(SelectedColor)} · {ColorPickerColorHelper.ToPercent(SelectedColor.A)}% opacity",
            ColorPickerPaintMode.LinearGradient => GetLinearPreviewPrimaryText(),
            ColorPickerPaintMode.RadialGradient => GetRadialPreviewPrimaryText(),
            _ => GetAssetPreviewPrimaryText()
        };
    }

    private string GetPaintPreviewSecondaryText()
    {
        return SelectedPaintMode switch
        {
            ColorPickerPaintMode.Solid => "The current solid paint color is reflected across the spectrum, sliders, and numeric fields.",
            ColorPickerPaintMode.LinearGradient => GetLinearPreviewSecondaryText(),
            ColorPickerPaintMode.RadialGradient => GetRadialPreviewSecondaryText(),
            _ => GetAssetPreviewSecondaryText()
        };
    }

    private string GetLinearPreviewPrimaryText()
    {
        var state = _gradientStates[ColorPickerPaintMode.LinearGradient];
        return $"{state.Stops.Count} stops · {Math.Round(state.Angle):0}° angle";
    }

    private string GetLinearPreviewSecondaryText()
    {
        return GetSelectedGradientStop(_gradientStates[ColorPickerPaintMode.LinearGradient])?.Summary
            ?? "Add a stop to start shaping the gradient.";
    }

    private string GetRadialPreviewPrimaryText()
    {
        var state = _gradientStates[ColorPickerPaintMode.RadialGradient];
        return $"{state.Stops.Count} stops · center {Math.Round(state.CenterX * 100.0):0}% / {Math.Round(state.CenterY * 100.0):0}%";
    }

    private string GetRadialPreviewSecondaryText()
    {
        var state = _gradientStates[ColorPickerPaintMode.RadialGradient];
        var stopSummary = GetSelectedGradientStop(state)?.Summary ?? "Select a stop";
        return $"Radius {Math.Round(state.Radius * 100.0):0}% · {stopSummary}";
    }

    private string GetAssetPreviewPrimaryText()
    {
        var state = GetActiveAssetState();
        if (state is null || string.IsNullOrWhiteSpace(state.Source))
        {
            return "No source selected yet";
        }

        return state.Source;
    }

    private string GetAssetPreviewSecondaryText()
    {
        var state = GetActiveAssetState();
        if (state is null)
        {
            return string.Empty;
        }

        var detail = SelectedPaintMode switch
        {
            ColorPickerPaintMode.ImagePaint => $"{state.Option} · {(state.Toggle ? "Respect aspect" : "Free crop")}",
            ColorPickerPaintMode.VideoPaint => $"{state.Option} · {(state.Toggle ? "Loop preview" : "Manual preview")}",
            ColorPickerPaintMode.ImageAsset => $"{state.Option} · {(state.Toggle ? "High contrast" : "Normal contrast")}",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(state.SecondaryText))
        {
            return detail;
        }

        return $"{detail} · {state.SecondaryText}";
    }

    private string GetAssetPanelHintText(ColorPickerPaintMode mode) => mode switch
    {
        ColorPickerPaintMode.ImagePaint => "Use the main color controls to define an overlay tint while this panel captures image-specific metadata.",
        ColorPickerPaintMode.VideoPaint => "Use the main color controls to define an overlay tint while this panel captures video-specific metadata.",
        ColorPickerPaintMode.ImageAsset => "Use the main color controls to define a texture tint while this panel captures asset metadata.",
        _ => string.Empty
    };

    private string GetAssetSourceLabel(ColorPickerPaintMode mode) => mode switch
    {
        ColorPickerPaintMode.ImagePaint => "Image source",
        ColorPickerPaintMode.VideoPaint => "Video source",
        ColorPickerPaintMode.ImageAsset => "Asset source",
        _ => "Source"
    };

    private string GetAssetSourcePlaceholder(ColorPickerPaintMode mode) => mode switch
    {
        ColorPickerPaintMode.ImagePaint => "ms-appx:///Assets/hero-photo.png",
        ColorPickerPaintMode.VideoPaint => "ms-appx:///Assets/promo-loop.mp4",
        ColorPickerPaintMode.ImageAsset => "ms-appx:///Assets/paper-texture.png",
        _ => "Source"
    };

    private string GetAssetOptionLabel(ColorPickerPaintMode mode) => mode switch
    {
        ColorPickerPaintMode.ImagePaint => "Crop mode",
        ColorPickerPaintMode.VideoPaint => "Playback",
        ColorPickerPaintMode.ImageAsset => "Repeat mode",
        _ => "Mode"
    };

    private string GetAssetSecondaryLabel(ColorPickerPaintMode mode) => mode switch
    {
        ColorPickerPaintMode.ImagePaint => "Focal point",
        ColorPickerPaintMode.VideoPaint => "Poster frame",
        ColorPickerPaintMode.ImageAsset => "Asset tag",
        _ => "Details"
    };

    private string GetAssetSecondaryPlaceholder(ColorPickerPaintMode mode) => mode switch
    {
        ColorPickerPaintMode.ImagePaint => "50% / 50%",
        ColorPickerPaintMode.VideoPaint => "intro-frame.png",
        ColorPickerPaintMode.ImageAsset => "paper / organic / neutral",
        _ => "Details"
    };

    private string GetAssetToggleLabel(ColorPickerPaintMode mode) => mode switch
    {
        ColorPickerPaintMode.ImagePaint => "Keep the image locked to its aspect ratio",
        ColorPickerPaintMode.VideoPaint => "Loop the video preview metadata",
        ColorPickerPaintMode.ImageAsset => "Boost contrast for texture overlays",
        _ => "Enabled"
    };

    private IEnumerable<string> GetAssetOptions(ColorPickerPaintMode mode) => mode switch
    {
        ColorPickerPaintMode.ImagePaint => ["Fill", "Fit", "Crop", "Tile"],
        ColorPickerPaintMode.VideoPaint => ["Loop", "Play once", "Muted loop", "Poster only"],
        ColorPickerPaintMode.ImageAsset => ["Tile", "Stretch", "Contain", "Cover"],
        _ => ["Default"]
    };

    private PickerIconKind GetPaintModeIcon(ColorPickerPaintMode mode) => mode switch
    {
        ColorPickerPaintMode.Solid => PickerIconKind.SolidPaint,
        ColorPickerPaintMode.LinearGradient => PickerIconKind.GradientLinear,
        ColorPickerPaintMode.RadialGradient => PickerIconKind.GradientRadial,
        ColorPickerPaintMode.ImagePaint => PickerIconKind.ImagePaint,
        ColorPickerPaintMode.VideoPaint => PickerIconKind.VideoPaint,
        ColorPickerPaintMode.ImageAsset => PickerIconKind.Image,
        _ => PickerIconKind.SolidPaint
    };

    private void ApplyToolbarButtonState(Border border, bool isActive)
    {
        var transparentBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        border.Background = isActive ? GetResourceBrush("PickerSectionBrush", Color.FromArgb(255, 248, 250, 252)) : transparentBrush;
        border.BorderBrush = isActive ? GetResourceBrush("TextBrush", Color.FromArgb(255, 17, 24, 39)) : transparentBrush;
        border.BorderThickness = isActive ? new Thickness(1) : new Thickness(0);
        border.Opacity = isActive ? 1.0 : 0.62;
    }

    private Brush GetResourceBrush(string key, Color fallback)
    {
        return Resources[key] as Brush ?? new SolidColorBrush(fallback);
    }

    private static double ParseStrokeWidth(string? rawText)
    {
        if (TryParseDouble(rawText, out var width))
        {
            return Math.Clamp(width, 0.25, 64.0);
        }

        return 1.0;
    }

    private static bool TryParseDouble(string? text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }
}
