using Microsoft.UI.Xaml;
using Windows.UI;

namespace Svg.Controls.ColorPicker.Uno.Models;

public sealed class ColorSwatchItem
{
    public ColorSwatchItem(
        Color color,
        string? label,
        PaintStyleTarget target,
        string? styleId,
        string? libraryId,
        string? libraryName,
        string? sectionName,
        string? searchKeywords,
        double strokeWidth = 1.0,
        string? description = null)
        : this(
            color,
            label,
            target,
            ColorPickerPaintMode.Solid,
            styleId,
            libraryId,
            libraryName,
            sectionName,
            searchKeywords,
            strokeWidth,
            description)
    {
    }

    public ColorSwatchItem(
        Color color,
        string? label = null,
        PaintStyleTarget target = PaintStyleTarget.Both,
        ColorPickerPaintMode paintMode = ColorPickerPaintMode.Solid,
        string? styleId = null,
        string? libraryId = null,
        string? libraryName = null,
        string? sectionName = null,
        string? searchKeywords = null,
        double strokeWidth = 1.0,
        string? description = null)
    {
        Color = color;
        Label = string.IsNullOrWhiteSpace(label)
            ? ColorPickerColorHelper.ToHexRgb(color)
            : label;
        Description = description ?? string.Empty;
        Target = target;
        PaintMode = paintMode;
        StyleId = styleId ?? string.Empty;
        LibraryId = libraryId ?? string.Empty;
        LibraryName = libraryName ?? string.Empty;
        SectionName = string.IsNullOrWhiteSpace(sectionName) ? "Paint styles" : sectionName;
        SearchKeywords = searchKeywords ?? string.Empty;
        StrokeWidth = strokeWidth;
    }

    public Color Color { get; }

    public string Label { get; }

    public string Description { get; }

    public PaintStyleTarget Target { get; }

    public ColorPickerPaintMode PaintMode { get; }

    public string StyleId { get; }

    public string LibraryId { get; }

    public string LibraryName { get; }

    public string SectionName { get; }

    public string SearchKeywords { get; }

    public double StrokeWidth { get; }

    public bool IsLibraryStyle => !string.IsNullOrWhiteSpace(StyleId) && !string.IsNullOrWhiteSpace(LibraryId);

    public bool SupportsFill => Target is PaintStyleTarget.Fill or PaintStyleTarget.Both;

    public bool SupportsStroke => Target is PaintStyleTarget.Stroke or PaintStyleTarget.Both;

    public string ModeLabel => ColorPickerPaintModeHelper.GetDisplayName(PaintMode);

    public string SearchText => $"{Label} {LibraryName} {SectionName} {SearchKeywords} {ModeLabel}";

    public string LibrarySummary => string.IsNullOrWhiteSpace(LibraryName)
        ? SectionName
        : $"{LibraryName} · {SectionName}";

    public string OpacityLabel => $"{ColorPickerColorHelper.ToPercent(Color.A)}%";

    public string StrokeWidthLabel => $"{StrokeWidth:0.##} px";

    public Thickness StrokePreviewThickness => new(Math.Clamp(StrokeWidth, 1.0, 3.0));

    public Visibility StrokePreviewVisibility => SupportsStroke ? Visibility.Visible : Visibility.Collapsed;

    public string StyleSummary => Target switch
    {
        PaintStyleTarget.Fill when PaintMode == ColorPickerPaintMode.Solid => $"Fill style · {OpacityLabel}",
        PaintStyleTarget.Fill => $"{ModeLabel} · {OpacityLabel}",
        PaintStyleTarget.Stroke when PaintMode == ColorPickerPaintMode.Solid => $"Stroke style · {StrokeWidthLabel}",
        PaintStyleTarget.Stroke => $"{ModeLabel} · {StrokeWidthLabel}",
        _ when PaintMode == ColorPickerPaintMode.Solid => $"Paint style · {OpacityLabel}",
        _ => $"{ModeLabel} · {OpacityLabel}"
    };
}
