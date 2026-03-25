using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Svg.Controls.ColorPicker.Uno;
using Svg.Controls.ColorPicker.Uno.Models;
using Windows.UI;

namespace UnoColorPickerSample;

public sealed partial class MainPage : Page, INotifyPropertyChanged
{
    private Color _selectedColor = Color.FromArgb(255, 224, 65, 65);
    private PaintStyleTarget _selectedPaintTarget = PaintStyleTarget.Fill;
    private ColorPickerPaintMode _selectedPaintMode = ColorPickerPaintMode.Solid;
    private bool _isEyeDropperActive;
    private string _currentStrokeWidthText = "2";
    private string _statusText = "Use the toolbar to switch between solid, gradient, image, video, and asset paint modes, then save or apply styles from the Libraries tab.";

    public MainPage()
    {
        InitializeComponent();
        Swatches = new ObservableCollection<ColorSwatchItem>(
        [
            new ColorSwatchItem(Color.FromArgb(255, 224, 65, 65), label: "Coral red"),
            new ColorSwatchItem(Color.FromArgb(255, 24, 190, 171), label: "Seafoam"),
            new ColorSwatchItem(Color.FromArgb(255, 117, 47, 230), label: "Violet", paintMode: ColorPickerPaintMode.LinearGradient),
            new ColorSwatchItem(Color.FromArgb(255, 248, 214, 104), label: "Warm sand", paintMode: ColorPickerPaintMode.RadialGradient),
            new ColorSwatchItem(Color.FromArgb(255, 17, 24, 39), label: "Ink", paintMode: ColorPickerPaintMode.ImagePaint)
        ]);

        LibraryStyles = new ObservableCollection<ColorSwatchItem>(
        [
            new ColorSwatchItem(Color.FromArgb(255, 99, 102, 241), label: "Brand / Primary", target: PaintStyleTarget.Fill, styleId: "brand-primary", libraryId: "sample-library", libraryName: "Starter kit", sectionName: "Fill styles", searchKeywords: "brand primary fill"),
            new ColorSwatchItem(Color.FromArgb(255, 14, 165, 233), label: "Brand / Accent", target: PaintStyleTarget.Fill, styleId: "brand-accent", libraryId: "sample-library", libraryName: "Starter kit", sectionName: "Fill styles", searchKeywords: "accent fill"),
            new ColorSwatchItem(Color.FromArgb(255, 167, 139, 250), label: "Hero / Aurora", target: PaintStyleTarget.Fill, paintMode: ColorPickerPaintMode.LinearGradient, styleId: "hero-aurora", libraryId: "sample-library", libraryName: "Starter kit", sectionName: "Gradient styles", searchKeywords: "hero aurora linear gradient"),
            new ColorSwatchItem(Color.FromArgb(255, 248, 113, 113), label: "Glow / Halo", target: PaintStyleTarget.Fill, paintMode: ColorPickerPaintMode.RadialGradient, styleId: "glow-halo", libraryId: "sample-library", libraryName: "Starter kit", sectionName: "Gradient styles", searchKeywords: "glow halo radial gradient"),
            new ColorSwatchItem(Color.FromArgb(255, 71, 84, 103), label: "Photo / Overlay", target: PaintStyleTarget.Fill, paintMode: ColorPickerPaintMode.ImagePaint, styleId: "photo-overlay", libraryId: "sample-library", libraryName: "Starter kit", sectionName: "Asset styles", searchKeywords: "photo image overlay"),
            new ColorSwatchItem(Color.FromArgb(255, 15, 23, 42), label: "Promo / Reel", target: PaintStyleTarget.Fill, paintMode: ColorPickerPaintMode.VideoPaint, styleId: "promo-reel", libraryId: "sample-library", libraryName: "Starter kit", sectionName: "Asset styles", searchKeywords: "video reel motion"),
            new ColorSwatchItem(Color.FromArgb(255, 120, 113, 108), label: "Texture / Paper", target: PaintStyleTarget.Both, paintMode: ColorPickerPaintMode.ImageAsset, styleId: "texture-paper", libraryId: "sample-library", libraryName: "Starter kit", sectionName: "Asset styles", searchKeywords: "texture paper asset", strokeWidth: 1.5),
            new ColorSwatchItem(Color.FromArgb(255, 17, 24, 39), label: "Outline / Strong", target: PaintStyleTarget.Stroke, styleId: "outline-strong", libraryId: "sample-library", libraryName: "Starter kit", sectionName: "Stroke styles", searchKeywords: "outline stroke", strokeWidth: 2),
            new ColorSwatchItem(Color.FromArgb(255, 52, 64, 84), label: "Shared / Neutral", target: PaintStyleTarget.Both, styleId: "shared-neutral", libraryId: "sample-library", libraryName: "Starter kit", sectionName: "Shared styles", searchKeywords: "neutral both", strokeWidth: 1.5)
        ]);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ColorSwatchItem> Swatches { get; }

    public ObservableCollection<ColorSwatchItem> LibraryStyles { get; }

    public Color SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (SetField(ref _selectedColor, value))
            {
                RaisePropertyChanged(nameof(SelectedColorHex));
                RaisePropertyChanged(nameof(SelectedColorRgba));
            }
        }
    }

    public PaintStyleTarget SelectedPaintTarget
    {
        get => _selectedPaintTarget;
        set => SetField(ref _selectedPaintTarget, value);
    }

    public ColorPickerPaintMode SelectedPaintMode
    {
        get => _selectedPaintMode;
        set
        {
            if (SetField(ref _selectedPaintMode, value))
            {
                RaisePropertyChanged(nameof(SelectedPaintModeLabel));
            }
        }
    }

    public bool IsEyeDropperActive
    {
        get => _isEyeDropperActive;
        set
        {
            if (SetField(ref _isEyeDropperActive, value))
            {
                RaisePropertyChanged(nameof(EyeDropperStateLabel));
            }
        }
    }

    public string CurrentStrokeWidthText
    {
        get => _currentStrokeWidthText;
        set => SetField(ref _currentStrokeWidthText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string SelectedColorHex => $"#{ColorPickerColorHelper.ToHexRgb(SelectedColor)}";

    public string SelectedColorRgba => $"RGBA {SelectedColor.R}, {SelectedColor.G}, {SelectedColor.B}, {ColorPickerColorHelper.ToPercent(SelectedColor.A)}%";

    public string SelectedPaintModeLabel => ColorPickerPaintModeHelper.GetDisplayName(SelectedPaintMode);

    public string EyeDropperStateLabel => IsEyeDropperActive ? "Palette sampling active" : "Off";

    private void OnPaintTargetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedItem: ComboBoxItem item }
            || item.Tag is not string rawTarget
            || !Enum.TryParse<PaintStyleTarget>(rawTarget, true, out var target))
        {
            return;
        }

        SelectedPaintTarget = target;
        StatusText = $"Filtering library styles for the {target} paint target.";
    }

    private void OnPaintStyleRequested(object sender, PaintStyleRequestedEventArgs e)
    {
        SelectedPaintMode = e.PaintMode;
        SelectedColor = e.Style.Color;
        IsEyeDropperActive = false;
        StatusText = $"Applied {e.Style.Label} ({ColorPickerPaintModeHelper.GetDisplayName(e.PaintMode).ToLowerInvariant()}) for {e.Target}.";
    }

    private void OnCreateStyleRequested(object sender, PaintStyleCreateRequestedEventArgs e)
    {
        var styleName = e.Target switch
        {
            PaintStyleTarget.Fill => "Saved fill style",
            PaintStyleTarget.Stroke => "Saved stroke style",
            _ => "Saved shared style"
        };

        var sectionName = e.Target switch
        {
            PaintStyleTarget.Fill => "Fill styles",
            PaintStyleTarget.Stroke => "Stroke styles",
            _ => "Shared styles"
        };

        var modeLabel = ColorPickerPaintModeHelper.GetDisplayName(e.PaintMode);
        var styleId = $"sample-created-{LibraryStyles.Count + 1}";
        var strokeWidth = TryParseStrokeWidth(CurrentStrokeWidthText);
        var swatch = new ColorSwatchItem(
            e.Color,
            label: $"{styleName} · {modeLabel}",
            target: e.Target,
            paintMode: e.PaintMode,
            styleId: styleId,
            libraryId: "sample-library",
            libraryName: "Starter kit",
            sectionName: sectionName,
            searchKeywords: $"created sample style {modeLabel.ToLowerInvariant()}",
            strokeWidth: e.Target == PaintStyleTarget.Stroke ? strokeWidth : e.StrokeWidth,
            description: "Created from the standalone color picker sample.");

        LibraryStyles.Insert(0, swatch);
        StatusText = $"Saved {swatch.Label} to the sample library.";
    }

    private static double TryParseStrokeWidth(string? rawValue)
    {
        return double.TryParse(rawValue, out var strokeWidth)
            ? Math.Clamp(strokeWidth, 0.25, 64.0)
            : 1.0;
    }

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }
}
