using Svg;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorTextStyleItem
{
    public EditorTextStyleItem(
        string styleId,
        string name,
        string? description,
        string fontFamily,
        SvgFontWeight fontWeight,
        double fontSize,
        double letterSpacing,
        string lineHeightText)
    {
        StyleId = styleId;
        Name = string.IsNullOrWhiteSpace(name) ? "Text" : name.Trim();
        Description = description?.Trim() ?? string.Empty;
        FontFamily = string.IsNullOrWhiteSpace(fontFamily) ? "Open Sans" : fontFamily.Trim();
        FontWeight = fontWeight;
        FontSize = Math.Max(1.0, fontSize);
        LetterSpacing = letterSpacing;
        LineHeightText = string.IsNullOrWhiteSpace(lineHeightText) ? "Auto" : lineHeightText.Trim();
    }

    public string StyleId { get; }

    public string Name { get; }

    public string Description { get; }

    public string FontFamily { get; }

    public SvgFontWeight FontWeight { get; }

    public double FontSize { get; }

    public double LetterSpacing { get; }

    public string LineHeightText { get; }

    public string PreviewText => "Ag";

    public string FontWeightLabel => FontWeight switch
    {
        SvgFontWeight.Bold or SvgFontWeight.W700 => "Bold",
        SvgFontWeight.W500 => "Medium",
        SvgFontWeight.W600 => "Semibold",
        SvgFontWeight.W300 => "Light",
        _ => "Regular"
    };

    public string Summary => $"{FontFamily} · {FontSize:0.##}/{LineHeightText}";

    public EditorTextStyleItem Clone()
    {
        return new EditorTextStyleItem(
            StyleId,
            Name,
            Description,
            FontFamily,
            FontWeight,
            FontSize,
            LetterSpacing,
            LineHeightText);
    }
}
