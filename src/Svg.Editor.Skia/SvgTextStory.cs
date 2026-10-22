using System;
using System.Collections.Generic;

namespace Svg.Editor.Skia;

public enum SvgParagraphAlignment { Start, Center, End, Justify }

/// <summary>A text style expressed in the story's local SVG user units.</summary>
public sealed class SvgStoryTextStyle
{
    public string FontFamily { get; set; } = "sans-serif";
    public float FontSize { get; set; } = 24;
    public int FontWeight { get; set; } = 400;
    public bool Italic { get; set; }
    public string Fill { get; set; } = "#202333";
    public float LetterSpacing { get; set; }
    public string FontFeatureSettings { get; set; } = "normal";
    public SvgStoryTextStyle Clone() => (SvgStoryTextStyle)MemberwiseClone();
}

/// <summary>An explicit rich-text style range. Offsets are UTF-16, like SVG DOM strings.</summary>
public sealed class SvgStoryStyleRange
{
    public int Start { get; set; }
    public int Length { get; set; }
    public SvgStoryTextStyle Style { get; set; } = new();
}

public sealed class SvgParagraphFormat
{
    public SvgParagraphAlignment Alignment { get; set; }
    public float LineHeight { get; set; } = 1.25f;
    public float SpaceBefore { get; set; }
    public float SpaceAfter { get; set; } = 8;
    public float FirstLineIndent { get; set; }
    public float LeftIndent { get; set; }
    public float RightIndent { get; set; }
    public bool RightToLeft { get; set; }
    public SvgParagraphFormat Clone() => (SvgParagraphFormat)MemberwiseClone();
}

/// <summary>Frames flow in collection order, then column order. Coordinates share one SVG group.</summary>
public sealed class SvgTextFrameDefinition
{
    public string Key { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Text frame";
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; } = 360;
    public float Height { get; set; } = 240;
    public float Padding { get; set; } = 12;
    public int Columns { get; set; } = 1;
    public float ColumnGap { get; set; } = 20;
    public SvgTextFrameDefinition Clone() => (SvgTextFrameDefinition)MemberwiseClone();
}

/// <summary>
/// Editable story independent of a UI framework. Newlines delimit paragraphs; rich ranges
/// preserve their own styles when the default typography changes. The layout is emitted as
/// standard SVG text/tspan elements and the complete story survives overflow in metadata.
/// </summary>
public sealed class SvgTextStory
{
    public string Text { get; set; } = string.Empty;
    public SvgStoryTextStyle DefaultStyle { get; set; } = new();
    public SvgParagraphFormat Paragraph { get; set; } = new();
    public List<SvgStoryStyleRange> Styles { get; } = new();
    public Dictionary<int, SvgParagraphFormat> ParagraphOverrides { get; } = new();
    public List<SvgTextFrameDefinition> Frames { get; } = new();
}

public sealed class SvgTextLayoutRun
{
    public int Start { get; internal set; }
    public int Length { get; internal set; }
    public string Text { get; internal set; } = string.Empty;
    public float X { get; internal set; }
    public float Advance { get; internal set; }
    public SvgStoryTextStyle Style { get; internal set; } = new();
    public string FontFamily { get; internal set; } = "sans-serif";
}

public sealed class SvgTextLayoutLine
{
    public int FrameIndex { get; internal set; }
    public int ColumnIndex { get; internal set; }
    public int ParagraphIndex { get; internal set; }
    public int Start { get; internal set; }
    public int Length { get; internal set; }
    public float X { get; internal set; }
    public float Baseline { get; internal set; }
    public float Width { get; internal set; }
    public float AvailableWidth { get; internal set; }
    public float JustificationSpacing { get; internal set; }
    public bool RightToLeft { get; internal set; }
    public bool EndsParagraph { get; internal set; }
    public IReadOnlyList<SvgTextLayoutRun> Runs { get; internal set; } = Array.Empty<SvgTextLayoutRun>();
}

public sealed class SvgTextLayoutResult
{
    public IReadOnlyList<SvgTextLayoutLine> Lines { get; internal set; } = Array.Empty<SvgTextLayoutLine>();
    public int OverflowStart { get; internal set; }
    public int OverflowLength { get; internal set; }
    public bool HasOverflow => OverflowLength > 0;
    public string OverflowText { get; internal set; } = string.Empty;
}
