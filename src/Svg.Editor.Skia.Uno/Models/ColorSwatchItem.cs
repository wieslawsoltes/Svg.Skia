using Microsoft.UI.Xaml;
using Windows.UI;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class ColorSwatchItem
{
    public ColorSwatchItem(
        Color color,
        string? label = null,
        EditorPaintTarget target = EditorPaintTarget.Both,
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

    public EditorPaintTarget Target { get; }

    public string StyleId { get; }

    public string LibraryId { get; }

    public string LibraryName { get; }

    public string SectionName { get; }

    public string SearchKeywords { get; }

    public double StrokeWidth { get; }

    public bool IsLibraryStyle => !string.IsNullOrWhiteSpace(StyleId) && !string.IsNullOrWhiteSpace(LibraryId);

    public bool SupportsFill => Target is EditorPaintTarget.Fill or EditorPaintTarget.Both;

    public bool SupportsStroke => Target is EditorPaintTarget.Stroke or EditorPaintTarget.Both;

    public string SearchText => $"{Label} {LibraryName} {SectionName} {SearchKeywords}";

    public string LibrarySummary => string.IsNullOrWhiteSpace(LibraryName)
        ? SectionName
        : $"{LibraryName} · {SectionName}";

    public string OpacityLabel => $"{ColorPickerColorHelper.ToPercent(Color.A)}%";

    public string StrokeWidthLabel => $"{StrokeWidth:0.##} px";

    public Thickness StrokePreviewThickness => new(Math.Clamp(StrokeWidth, 1.0, 3.0));

    public Visibility StrokePreviewVisibility => SupportsStroke ? Visibility.Visible : Visibility.Collapsed;

    public string StyleSummary => Target switch
    {
        EditorPaintTarget.Fill => $"Fill style · {OpacityLabel}",
        EditorPaintTarget.Stroke => $"Stroke style · {StrokeWidthLabel}",
        _ => $"Paint style · {OpacityLabel}"
    };
}
