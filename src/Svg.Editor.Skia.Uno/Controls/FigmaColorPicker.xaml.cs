using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.UI.Xaml.Media;
using Svg.Editor.Skia.Uno.Models;
using Windows.System;
using Windows.UI;

namespace Svg.Editor.Skia.Uno.Controls;

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
            typeof(EditorPaintTarget),
            typeof(FigmaColorPicker),
            new PropertyMetadata(EditorPaintTarget.Fill, OnLibraryStylesChanged));

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

    private bool _isInternalUpdate;
    private bool _isLibrariesTab;
    private bool _isUpdatingLibrarySources;
    private ColorValueDisplayMode _displayMode = ColorValueDisplayMode.Rgb;
    private readonly ObservableCollection<ColorSwatchItem> _visibleLibraryStyles = [];
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

        ApplyColorToControls(SelectedColor);
        UpdateSwatches();
        UpdateLibraryStyles();
        UpdateTabState();
        UpdateChromeVisibility();
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

    public EditorPaintTarget PaintTarget
    {
        get => (EditorPaintTarget)GetValue(PaintTargetProperty);
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

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (FigmaColorPicker)d;
        if (e.NewValue is Color color)
        {
            picker.ApplyColorToControls(color);
        }
    }

    private static void OnSwatchesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FigmaColorPicker)d).UpdateSwatches();
    }

    private static void OnLibraryStylesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FigmaColorPicker)d).UpdateLibraryStyles();
    }

    private static void OnPickerChromePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FigmaColorPicker)d).UpdateChromeVisibility();
    }

    private void OnCustomTabClick(object sender, RoutedEventArgs e)
    {
        _isLibrariesTab = false;
        UpdateTabState();
    }

    private void OnLibrariesTabClick(object sender, RoutedEventArgs e)
    {
        _isLibrariesTab = true;
        UpdateTabState();
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
                    ParseStrokeWidth(CurrentStrokeWidthText)));
            return;
        }

        var collection = Swatches as ObservableCollection<ColorSwatchItem> ?? _defaultSwatches;
        var color = SelectedColor;
        var argb = (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
        if (!collection.Any(item =>
            ((item.Color.A << 24) | (item.Color.R << 16) | (item.Color.G << 8) | item.Color.B) == argb))
        {
            collection.Insert(0, new ColorSwatchItem(color));
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

        SelectedColor = swatch.Color;
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

        SelectedColor = swatch.Color;
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
                EditorPaintTarget.Fill => style.SupportsFill,
                EditorPaintTarget.Stroke => style.SupportsStroke,
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
