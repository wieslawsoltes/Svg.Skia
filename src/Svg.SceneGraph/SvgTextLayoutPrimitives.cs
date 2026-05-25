#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ShimSkiaSharp;
using Svg;
using Svg.Model;

namespace Svg.Skia;

internal enum SvgTextLayoutFlow
{
    HorizontalLeftToRight,
    HorizontalRightToLeft,
    VerticalRightToLeftColumns,
    VerticalLeftToRightColumns
}

internal enum SvgTextPlacementKind
{
    Inline,
    Positioned,
    Wrapped,
    TextPath,
    StretchedTextPath
}

internal enum SvgTextPaintPhase
{
    Fill,
    Stroke,
    Decorations
}

internal enum SvgTextPathLayoutMethod
{
    Align,
    Stretch
}

internal readonly record struct SvgTextWrappedLayoutOptions(
    SvgTextLayoutFlow Flow,
    SKPoint Origin,
    float InlineSize,
    float LineAdvance,
    SvgTextWrappingOptions Wrapping,
    float TextLength = 0f,
    SvgTextLengthAdjust LengthAdjust = SvgTextLengthAdjust.Spacing,
    string? OverflowMarker = null,
    float OverflowMarkerAdvance = 0f)
{
    public float EffectiveInlineSize => Math.Max(0f, InlineSize);

    public float EffectiveLineAdvance => Math.Max(0f, LineAdvance);

    public bool HasTextLength => TextLength > 0f;

    public bool HasOverflowMarker => !string.IsNullOrEmpty(OverflowMarker) && OverflowMarkerAdvance >= 0f;
}

internal enum SvgTextRenderCommandKind
{
    Text,
    PositionedText,
    GlyphRun,
    TextPath,
    Path
}

internal enum SvgTextShapeSourceKind
{
    None,
    LayoutBox,
    BasicShape,
    ReferencedElement,
    ImageAlpha
}

internal readonly record struct SvgTextIndexRange(int Start, int Length)
{
    public static SvgTextIndexRange Empty { get; } = new(0, 0);

    public int End => Start + Length;

    public bool IsEmpty => Length <= 0;
}

internal readonly record struct SvgTextSourceRange(
    SvgTextBase? Source,
    SvgTextIndexRange Utf16Range,
    SvgTextIndexRange CodepointRange)
{
    public static SvgTextSourceRange Empty { get; } = new(null, SvgTextIndexRange.Empty, SvgTextIndexRange.Empty);
}

internal readonly record struct SvgTextFontSelection(
    string? FamilyName,
    float Size,
    SKFontStyleWeight Weight,
    SKFontStyleWidth Width,
    SKFontStyleSlant Slant,
    SKTextEncoding TextEncoding,
    bool LcdRenderText,
    bool SubpixelText,
    SKTypeface? Typeface = null);

internal readonly record struct SvgTextSpacingStyle(
    float LetterSpacing,
    float WordSpacing,
    float TextLength,
    bool AdjustGlyphs,
    bool AdjustSpacing);

internal enum SvgTextWhiteSpaceCollapseMode
{
    Collapse,
    Preserve,
    PreserveBreaks,
    BreakSpaces,
    PreserveSpaces,
    Discard
}

internal enum SvgTextWrapMode
{
    Wrap,
    NoWrap
}

[Flags]
internal enum SvgTextWhiteSpaceTrimMode
{
    None = 0,
    DiscardBefore = 1,
    DiscardAfter = 2,
    DiscardInner = 4
}

internal readonly record struct SvgTextWhiteSpaceModel(
    SvgTextWhiteSpaceCollapseMode Collapse,
    SvgTextWrapMode WrapMode,
    SvgTextWhiteSpaceTrimMode Trim)
{
    public static SvgTextWhiteSpaceModel Normal { get; } = new(
        SvgTextWhiteSpaceCollapseMode.Collapse,
        SvgTextWrapMode.Wrap,
        SvgTextWhiteSpaceTrimMode.None);

    public bool AllowsSoftWrapping => WrapMode == SvgTextWrapMode.Wrap;

    public bool PreservesSegmentBreaks =>
        Collapse is SvgTextWhiteSpaceCollapseMode.Preserve or
                    SvgTextWhiteSpaceCollapseMode.PreserveBreaks or
                    SvgTextWhiteSpaceCollapseMode.BreakSpaces;

    public bool PreservesTextWhitespace =>
        Collapse is SvgTextWhiteSpaceCollapseMode.Preserve or
                    SvgTextWhiteSpaceCollapseMode.PreserveSpaces or
                    SvgTextWhiteSpaceCollapseMode.BreakSpaces;

    public bool PreservesLineEdgeWhitespace =>
        Collapse is SvgTextWhiteSpaceCollapseMode.Preserve or
                    SvgTextWhiteSpaceCollapseMode.PreserveSpaces or
                    SvgTextWhiteSpaceCollapseMode.BreakSpaces;

    public bool BreaksAfterEveryPreservedSpace => Collapse == SvgTextWhiteSpaceCollapseMode.BreakSpaces;

    public bool DiscardsDocumentWhiteSpace => Collapse == SvgTextWhiteSpaceCollapseMode.Discard;

    public bool CollapsesTextWhitespace => !PreservesTextWhitespace || DiscardsDocumentWhiteSpace;

    public bool TrimsLeadingWhitespace =>
        Trim.HasFlag(SvgTextWhiteSpaceTrimMode.DiscardBefore) ||
        Trim.HasFlag(SvgTextWhiteSpaceTrimMode.DiscardInner);

    public bool TrimsTrailingWhitespace =>
        Trim.HasFlag(SvgTextWhiteSpaceTrimMode.DiscardAfter) ||
        Trim.HasFlag(SvgTextWhiteSpaceTrimMode.DiscardInner);

    public bool HangsEndOfLineWhitespace =>
        Collapse is SvgTextWhiteSpaceCollapseMode.Collapse or
                    SvgTextWhiteSpaceCollapseMode.PreserveBreaks ||
        (Collapse == SvgTextWhiteSpaceCollapseMode.Preserve && WrapMode == SvgTextWrapMode.Wrap);

    public bool MeasuresEndOfLineWhitespace => Collapse == SvgTextWhiteSpaceCollapseMode.BreakSpaces;

    public static SvgTextWhiteSpaceModel FromLegacy(SvgWhiteSpace whiteSpace)
    {
        return whiteSpace switch
        {
            SvgWhiteSpace.NoWrap => Normal with { WrapMode = SvgTextWrapMode.NoWrap },
            SvgWhiteSpace.Pre => new SvgTextWhiteSpaceModel(
                SvgTextWhiteSpaceCollapseMode.Preserve,
                SvgTextWrapMode.NoWrap,
                SvgTextWhiteSpaceTrimMode.None),
            SvgWhiteSpace.PreWrap => new SvgTextWhiteSpaceModel(
                SvgTextWhiteSpaceCollapseMode.Preserve,
                SvgTextWrapMode.Wrap,
                SvgTextWhiteSpaceTrimMode.None),
            SvgWhiteSpace.PreLine => new SvgTextWhiteSpaceModel(
                SvgTextWhiteSpaceCollapseMode.PreserveBreaks,
                SvgTextWrapMode.Wrap,
                SvgTextWhiteSpaceTrimMode.None),
            SvgWhiteSpace.BreakSpaces => new SvgTextWhiteSpaceModel(
                SvgTextWhiteSpaceCollapseMode.BreakSpaces,
                SvgTextWrapMode.Wrap,
                SvgTextWhiteSpaceTrimMode.None),
            _ => Normal
        };
    }

    public bool TryGetLegacy(out SvgWhiteSpace whiteSpace)
    {
        if (Trim != SvgTextWhiteSpaceTrimMode.None)
        {
            whiteSpace = SvgWhiteSpace.Normal;
            return false;
        }

        if (Collapse == SvgTextWhiteSpaceCollapseMode.Collapse)
        {
            whiteSpace = WrapMode == SvgTextWrapMode.NoWrap ? SvgWhiteSpace.NoWrap : SvgWhiteSpace.Normal;
            return true;
        }

        if (Collapse == SvgTextWhiteSpaceCollapseMode.Preserve)
        {
            whiteSpace = WrapMode == SvgTextWrapMode.NoWrap ? SvgWhiteSpace.Pre : SvgWhiteSpace.PreWrap;
            return true;
        }

        if (Collapse == SvgTextWhiteSpaceCollapseMode.PreserveBreaks && WrapMode == SvgTextWrapMode.Wrap)
        {
            whiteSpace = SvgWhiteSpace.PreLine;
            return true;
        }

        if (Collapse == SvgTextWhiteSpaceCollapseMode.BreakSpaces && WrapMode == SvgTextWrapMode.Wrap)
        {
            whiteSpace = SvgWhiteSpace.BreakSpaces;
            return true;
        }

        whiteSpace = SvgWhiteSpace.Normal;
        return false;
    }
}

internal readonly record struct SvgTextLineBreakPolicy(
    SvgWhiteSpace WhiteSpace,
    SvgTextWhiteSpaceModel WhiteSpaceModel,
    bool OverflowWrapAnywhere,
    bool WordBreakBreakAll,
    bool WordBreakKeepAll,
    bool LineBreakAnywhere,
    bool LineBreakLoose,
    bool StrictLineBreak)
{
    public SvgTextLineBreakPolicy(
        SvgWhiteSpace whiteSpace,
        bool overflowWrapAnywhere,
        bool wordBreakBreakAll,
        bool wordBreakKeepAll,
        bool lineBreakAnywhere,
        bool lineBreakLoose,
        bool strictLineBreak)
        : this(
            whiteSpace,
            SvgTextWhiteSpaceModel.FromLegacy(whiteSpace),
            overflowWrapAnywhere,
            wordBreakBreakAll,
            wordBreakKeepAll,
            lineBreakAnywhere,
            lineBreakLoose,
            strictLineBreak)
    {
    }

    public bool AllowsCharacterBreaks => OverflowWrapAnywhere || WordBreakBreakAll || LineBreakAnywhere;

    public SvgTextLineBreakOptions ToLineBreakOptions() => new(
        OverflowWrapAnywhere,
        WordBreakBreakAll,
        WordBreakKeepAll,
        LineBreakAnywhere,
        LineBreakLoose,
        StrictLineBreak);
}

internal readonly record struct SvgTextCodepoint(
    string Text,
    int Scalar,
    int Utf16Index,
    int Utf16Length,
    int GraphemeClusterIndex,
    bool IsCollapsedWhitespace,
    bool ForcesLineBreak)
{
    public bool IsEmpty => string.IsNullOrEmpty(Text) || Utf16Length <= 0;
}

internal readonly record struct SvgTextGlyph(
    ushort GlyphId,
    int ClusterUtf16Index,
    SKPoint Position,
    float Advance,
    SKRect Bounds);

internal readonly record struct SvgTextCodepointPlacement(
    SKPoint Point,
    float RotationDegrees,
    float ScaleX,
    float ScaleOriginX,
    float InlineOffset,
    float Advance,
    int CodepointIndex,
    SvgTextPlacementKind Kind);

internal readonly record struct SvgTextPathSample(
    SKPoint Point,
    float Distance,
    bool StartsSubpath,
    bool ClosesSubpath);

internal sealed class SvgTextResolvedStyle
{
    public SvgTextResolvedStyle(
        SvgTextBase styleSource,
        SvgTextFontSelection font,
        SvgTextDirection direction,
        SvgUnicodeBidiMode unicodeBidi,
        SvgTextLayoutFlow flow,
        SvgTextAnchor textAnchor,
        SvgTextDecoration textDecoration,
        SvgTextLineBreakPolicy lineBreak,
        SvgTextSpacingStyle spacing,
        float lineHeight,
        SKPoint baselineShift,
        string? elementAddressKey = null,
        string? textOverflow = null,
        string? shapeInside = null,
        string? shapeSubtract = null)
    {
        StyleSource = styleSource ?? throw new ArgumentNullException(nameof(styleSource));
        Font = font;
        Direction = direction;
        UnicodeBidi = unicodeBidi;
        Flow = flow;
        TextAnchor = textAnchor;
        TextDecoration = textDecoration;
        LineBreak = lineBreak;
        Spacing = spacing;
        LineHeight = lineHeight;
        BaselineShift = baselineShift;
        ElementAddressKey = elementAddressKey;
        TextOverflow = textOverflow;
        ShapeInside = shapeInside;
        ShapeSubtract = shapeSubtract;
    }

    public SvgTextBase StyleSource { get; }

    public string? ElementAddressKey { get; }

    public SvgTextFontSelection Font { get; }

    public SvgTextDirection Direction { get; }

    public SvgUnicodeBidiMode UnicodeBidi { get; }

    public SvgTextLayoutFlow Flow { get; }

    public SvgTextAnchor TextAnchor { get; }

    public SvgTextDecoration TextDecoration { get; }

    public SvgTextLineBreakPolicy LineBreak { get; }

    public SvgTextSpacingStyle Spacing { get; }

    public float LineHeight { get; }

    public SKPoint BaselineShift { get; }

    public string? TextOverflow { get; }

    public string? ShapeInside { get; }

    public string? ShapeSubtract { get; }

    public bool IsRightToLeft => Direction == SvgTextDirection.RightToLeft;

    public bool IsVertical =>
        Flow is SvgTextLayoutFlow.VerticalLeftToRightColumns or SvgTextLayoutFlow.VerticalRightToLeftColumns;
}

internal sealed class SvgTextCodepointRun
{
    private readonly IReadOnlyList<SvgTextCodepoint> _codepoints;

    public SvgTextCodepointRun(
        SvgTextResolvedStyle style,
        string text,
        IEnumerable<SvgTextCodepoint>? codepoints,
        SvgTextSourceRange sourceRange,
        float advance,
        int bidiLevel,
        SvgTextDirection direction)
    {
        Style = style ?? throw new ArgumentNullException(nameof(style));
        Text = text ?? string.Empty;
        _codepoints = SvgTextLayoutModel.Freeze(codepoints);
        SourceRange = sourceRange;
        Advance = advance;
        BidiLevel = bidiLevel;
        Direction = direction;
    }

    public SvgTextResolvedStyle Style { get; }

    public string Text { get; }

    public IReadOnlyList<SvgTextCodepoint> Codepoints => _codepoints;

    public SvgTextSourceRange SourceRange { get; }

    public float Advance { get; }

    public int BidiLevel { get; }

    public SvgTextDirection Direction { get; }
}

internal sealed class SvgTextGlyphRun
{
    private readonly IReadOnlyList<ushort> _glyphs;
    private readonly IReadOnlyList<SKPoint> _points;
    private readonly IReadOnlyList<int> _clusters;

    public SvgTextGlyphRun(
        SvgTextResolvedStyle style,
        string text,
        IEnumerable<ushort>? glyphs,
        IEnumerable<SKPoint>? points,
        IEnumerable<int>? clusters,
        float advance,
        SKRect bounds,
        bool rightToLeft)
    {
        Style = style ?? throw new ArgumentNullException(nameof(style));
        Text = text ?? string.Empty;
        _glyphs = SvgTextLayoutModel.Freeze(glyphs);
        _points = SvgTextLayoutModel.Freeze(points);
        _clusters = SvgTextLayoutModel.Freeze(clusters);
        Advance = advance;
        Bounds = bounds;
        RightToLeft = rightToLeft;
    }

    public SvgTextResolvedStyle Style { get; }

    public string Text { get; }

    public IReadOnlyList<ushort> Glyphs => _glyphs;

    public IReadOnlyList<SKPoint> Points => _points;

    public IReadOnlyList<int> Clusters => _clusters;

    public float Advance { get; }

    public SKRect Bounds { get; }

    public bool RightToLeft { get; }

    public ShapedGlyphRun ToShapedGlyphRun()
    {
        return new ShapedGlyphRun(
            Glyphs.ToArray(),
            Points.ToArray(),
            Clusters.ToArray(),
            Advance);
    }
}

internal static class SvgTextLayoutModel
{
    public static IReadOnlyList<T> Freeze<T>(IEnumerable<T>? values)
    {
        if (values is null)
        {
            return Array.Empty<T>();
        }

        var array = values as T[] ?? values.ToArray();
        return array.Length == 0 ? Array.Empty<T>() : new ReadOnlyCollection<T>(array);
    }
}
