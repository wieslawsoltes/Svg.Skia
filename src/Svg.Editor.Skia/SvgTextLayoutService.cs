using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Svg.Model;
using Svg.Skia;
using SK = ShimSkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>
/// Shaped, grapheme-safe paragraph composition over linked rectangular frames. Uses the
/// renderer asset loader for HarfBuzz advances, installed/document fonts and font fallback.
/// Line breaking is greedy with Unicode whitespace, CJK and explicit hyphen opportunities.
/// </summary>
public static class SvgTextLayoutService
{
    public const int MaximumCharacters = 200000;
    public const int MaximumFrames = 128;
    private const int MaximumLines = 20000;

    public static SvgTextLayoutResult Layout(SvgTextStory story)
    {
        using var renderer = new SKSvg();
        return Layout(story, renderer.AssetLoader);
    }

    public static SvgTextLayoutResult Layout(SvgTextStory story, ISvgAssetLoader assets)
    {
        Validate(story);
        if (assets is null)
            throw new ArgumentNullException(nameof(assets));
        var result = new List<SvgTextLayoutLine>();
        var text = story.Text;
        var measure = new MeasureContext(story, assets);
        var frameIndex = 0;
        var columnIndex = 0;
        var top = story.Frames[0].Y + story.Frames[0].Padding;
        var offset = 0;
        var paragraphIndex = 0;

        bool AdvanceFrame()
        {
            var current = story.Frames[frameIndex];
            columnIndex++;
            if (columnIndex >= current.Columns)
            {
                columnIndex = 0;
                frameIndex++;
            }
            if (frameIndex >= story.Frames.Count)
                return false;
            top = story.Frames[frameIndex].Y + story.Frames[frameIndex].Padding;
            return true;
        }

        while (offset < text.Length)
        {
            var paragraphEnd = text.IndexOf('\n', offset);
            if (paragraphEnd < 0)
                paragraphEnd = text.Length;
            var format = story.ParagraphOverrides.TryGetValue(paragraphIndex, out var custom) ? custom : story.Paragraph;
            var firstLine = true;
            var paragraphStart = offset;
            do
            {
                if (result.Count >= MaximumLines)
                    throw new InvalidOperationException("The text story exceeds the 20,000-line layout limit.");
                var frame = story.Frames[frameIndex];
                var columnWidth = (frame.Width - 2 * frame.Padding - (frame.Columns - 1) * frame.ColumnGap) / frame.Columns;
                var indent = firstLine ? format.FirstLineIndent : 0;
                var available = columnWidth - format.LeftIndent - format.RightIndent - indent;
                if (available <= 0)
                    throw new ArgumentException("Paragraph indents leave no room in a text frame.", nameof(story));
                var end = FindLineEnd(text, offset, paragraphEnd, available, measure, format.RightToLeft);
                var visibleEnd = end;
                if (end < paragraphEnd)
                    while (visibleEnd > offset && IsCollapsibleSpace(text[visibleEnd - 1]))
                        visibleEnd--;
                var runs = measure.Runs(offset, visibleEnd - offset, format.RightToLeft);
                var metrics = measure.Metrics(runs, offset);
                var lineHeight = Math.Max(metrics.Ascent + metrics.Descent, metrics.Size * format.LineHeight);
                var before = firstLine ? format.SpaceBefore : 0;
                var baseline = top + before + metrics.Ascent + Math.Max(0, lineHeight - metrics.Ascent - metrics.Descent) / 2;
                if (baseline + metrics.Descent > frame.Y + frame.Height - frame.Padding + 0.001f)
                {
                    if (!AdvanceFrame())
                        return Overflow(result, text, offset);
                    continue;
                }

                var width = runs.Sum(run => run.Advance);
                var remaining = Math.Max(0, available - width);
                var endsParagraph = end == paragraphEnd;
                var align = format.Alignment;
                var left = frame.X + frame.Padding + columnIndex * (columnWidth + frame.ColumnGap) + format.LeftIndent;
                if (!format.RightToLeft)
                    left += indent;
                var x = left + (align == SvgParagraphAlignment.Center ? remaining / 2
                    : (align == SvgParagraphAlignment.End && !format.RightToLeft) || (align == SvgParagraphAlignment.Start && format.RightToLeft) ? remaining : 0);
                var spaces = runs.Sum(run => run.Text.Count(c => c == ' '));
                var extraSpace = align == SvgParagraphAlignment.Justify && !endsParagraph && spaces > 0 ? remaining / spaces : 0;
                var cursor = x;
                // Each run is shaped as a unit. For explicit RTL paragraphs, place logical
                // runs from the right; SVG's bidi algorithm shapes each run on its own.
                if (format.RightToLeft)
                    cursor += width + extraSpace * spaces;
                foreach (var run in runs)
                {
                    var advance = run.Advance + extraSpace * run.Text.Count(c => c == ' ');
                    if (format.RightToLeft)
                        cursor -= advance;
                    run.X = cursor;
                    if (!format.RightToLeft)
                        cursor += advance;
                }
                result.Add(new SvgTextLayoutLine
                {
                    FrameIndex = frameIndex,
                    ColumnIndex = columnIndex,
                    ParagraphIndex = paragraphIndex,
                    Start = offset,
                    Length = visibleEnd - offset,
                    X = x,
                    Baseline = baseline,
                    Width = width + extraSpace * spaces,
                    AvailableWidth = available,
                    JustificationSpacing = extraSpace,
                    RightToLeft = format.RightToLeft,
                    EndsParagraph = endsParagraph,
                    Runs = runs.AsReadOnly()
                });
                top += before + lineHeight;
                offset = end;
                firstLine = false;
                if (endsParagraph)
                    break;
            } while (offset < paragraphEnd || firstLine);
            top += format.SpaceAfter;
            offset = paragraphEnd < text.Length ? paragraphEnd + 1 : paragraphEnd;
            paragraphIndex++;
            if (offset == paragraphStart && offset < text.Length)
                throw new InvalidOperationException("Text layout did not advance.");
        }
        return Overflow(result, text, text.Length);
    }

    private static SvgTextLayoutResult Overflow(List<SvgTextLayoutLine> lines, string text, int start) => new()
    {
        Lines = lines.AsReadOnly(),
        OverflowStart = start,
        OverflowLength = text.Length - start,
        OverflowText = text.Substring(start)
    };

    private static int FindLineEnd(string text, int start, int limit, float width, MeasureContext measure, bool rtl)
    {
        if (start == limit)
            return start;
        var boundaries = measure.Boundaries;
        var first = boundaries.BinarySearch(start);
        var last = boundaries.BinarySearch(limit);
        if (first < 0 || last < 0)
            throw new InvalidOperationException("A line boundary divided a grapheme.");
        var count = last - first;
        var low = 0;
        var high = Math.Min(1, count);
        while (high < count && measure.Width(start, boundaries[first + high] - start, rtl) <= width)
        {
            low = high;
            high = Math.Min(count, high * 2);
        }
        while (low < high)
        {
            var middle = low + (high - low + 1) / 2;
            if (measure.Width(start, boundaries[first + middle] - start, rtl) <= width + 0.001f)
                low = middle;
            else
                high = middle - 1;
        }
        // An indivisible grapheme wider than a frame remains intact and is reported by
        // the line width. Splitting UTF-16, combining sequences or emoji is never allowed.
        var candidate = boundaries[first + Math.Max(1, low)];
        if (candidate == limit)
            return candidate;
        for (var index = Math.Max(1, low); index > 0; index--)
        {
            var boundary = boundaries[first + index];
            var previous = text[boundary - 1];
            if (IsCollapsibleSpace(previous) || previous is '-' or '\u2010' or '\u200b'
                || IsCjk(previous) && boundary < limit && IsCjk(text[boundary]))
                return boundary;
        }
        return candidate;
    }

    internal static List<int> GraphemeBoundaries(string text, int start, int limit)
    {
        var result = new List<int> { start };
        var enumerator = StringInfo.GetTextElementEnumerator(text, start);
        var previousRegional = false;
        var regionalCount = 0;
        while (enumerator.MoveNext())
        {
            var at = enumerator.ElementIndex;
            if (at >= limit)
                break;
            var code = char.ConvertToUtf32(text, at);
            // Recent runtimes already combine an RI pair into one text element. Only
            // augment the older runtime behavior when this element is a single RI.
            var regional = code >= 0x1F1E6 && code <= 0x1F1FF && enumerator.GetTextElement().Length == 2;
            var join = at > start && (text[at - 1] == '\u200d' || code == 0x200D || code is >= 0xFE00 and <= 0xFE0F
                || code is >= 0x1F3FB and <= 0x1F3FF || regional && previousRegional && regionalCount % 2 == 1);
            if (at > start && !join)
                result.Add(at);
            regionalCount = regional ? regionalCount + 1 : 0;
            previousRegional = regional;
        }
        result.Add(limit);
        return result;
    }

    private static bool IsCjk(char value) => value is >= '\u2e80' and <= '\ua4cf' or >= '\uac00' and <= '\ud7af' or >= '\uf900' and <= '\ufaff';
    private static bool IsCollapsibleSpace(char value) => value is ' ' or '\t' or '\u200b';
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static void Validate(SvgTextStory story)
    {
        if (story is null)
            throw new ArgumentNullException(nameof(story));
        if (story.Text is null || story.Text.Length > MaximumCharacters || story.Text.IndexOf('\r') >= 0)
            throw new ArgumentException("A story accepts up to 200,000 characters with LF paragraph breaks.", nameof(story));
        System.Xml.XmlConvert.VerifyXmlChars(story.Text);
        if (story.Frames.Count is < 1 or > MaximumFrames)
            throw new ArgumentException("A story needs between 1 and 128 linked frames.", nameof(story));
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var frame in story.Frames)
        {
            if (frame is null || string.IsNullOrWhiteSpace(frame.Key) || !keys.Add(frame.Key)
                || !Finite(frame.X) || !Finite(frame.Y) || !Finite(frame.Width) || !Finite(frame.Height)
                || !Finite(frame.Padding) || !Finite(frame.ColumnGap) || frame.Width <= 0 || frame.Height <= 0
                || frame.Width > 1000000 || frame.Height > 1000000 || Math.Abs(frame.X) > 10000000 || Math.Abs(frame.Y) > 10000000
                || frame.Padding < 0 || frame.ColumnGap < 0 || frame.Columns is < 1 or > 16
                || frame.Height <= 2 * frame.Padding || frame.Width <= 2 * frame.Padding + (frame.Columns - 1) * frame.ColumnGap)
                throw new ArgumentException("Text frame bounds, columns, padding and unique keys must be valid.", nameof(story));
        }
        ValidateStyle(story.DefaultStyle);
        ValidateParagraph(story.Paragraph);
        foreach (var pair in story.ParagraphOverrides)
        {
            if (pair.Key < 0 || pair.Key > story.Text.Count(c => c == '\n'))
                throw new ArgumentException("A paragraph override refers to a missing paragraph.", nameof(story));
            ValidateParagraph(pair.Value);
        }
        var last = 0;
        foreach (var range in story.Styles.OrderBy(range => range.Start))
        {
            if (range.Start < last || range.Length <= 0 || range.Start > story.Text.Length - range.Length
                || !IsScalarBoundary(story.Text, range.Start) || !IsScalarBoundary(story.Text, range.Start + range.Length))
                throw new ArgumentException("Rich-text ranges must be non-overlapping valid UTF-16 ranges.", nameof(story));
            last = range.Start + range.Length;
            ValidateStyle(range.Style);
        }
    }

    private static void ValidateParagraph(SvgParagraphFormat format)
    {
        if (format is null || !Enum.IsDefined(typeof(SvgParagraphAlignment), format.Alignment)
            || !Finite(format.LineHeight) || format.LineHeight < 0.5f || format.LineHeight > 10
            || !Finite(format.SpaceBefore) || !Finite(format.SpaceAfter) || format.SpaceBefore < 0 || format.SpaceAfter < 0
            || !Finite(format.FirstLineIndent) || !Finite(format.LeftIndent) || !Finite(format.RightIndent)
            || format.LeftIndent < 0 || format.RightIndent < 0 || format.FirstLineIndent < -format.LeftIndent)
            throw new ArgumentException("Paragraph spacing and indents must be finite and leave valid text bounds.");
    }

    private static void ValidateStyle(SvgStoryTextStyle style)
    {
        if (style is null || string.IsNullOrWhiteSpace(style.FontFamily) || style.FontFamily.Length > 1024
            || !Finite(style.FontSize) || style.FontSize <= 0 || style.FontSize > 10000
            || style.FontWeight is < 100 or > 900 || !Finite(style.LetterSpacing) || style.LetterSpacing < 0 || style.LetterSpacing > 10000
            || string.IsNullOrWhiteSpace(style.Fill) || style.Fill.Length > 4096 || style.FontFeatureSettings.Length > 1024)
            throw new ArgumentException("Text styles need a valid family, size, weight, paint and non-negative tracking.");
        var converter = TypeDescriptor.GetConverter(typeof(SvgPaintServer));
        _ = converter.ConvertFromInvariantString(style.Fill);
    }

    private static bool IsScalarBoundary(string text, int at)
        => at <= 0 || at >= text.Length || !char.IsLowSurrogate(text[at]) || !char.IsHighSurrogate(text[at - 1]);

    private sealed class MeasureContext
    {
        private readonly SvgTextStory _story;
        private readonly ISvgAssetLoader _assets;
        private readonly SvgStoryStyleRange[] _styles;
        private readonly Dictionary<(int, int, bool), List<SvgTextLayoutRun>> _cache = new();
        public List<int> Boundaries { get; }

        public MeasureContext(SvgTextStory story, ISvgAssetLoader assets)
        {
            _story = story;
            _assets = assets;
            _styles = story.Styles.OrderBy(range => range.Start).ToArray();
            Boundaries = GraphemeBoundaries(story.Text, 0, story.Text.Length);
        }

        public float Width(int start, int length, bool rtl) => Runs(start, length, rtl).Sum(run => run.Advance);

        public List<SvgTextLayoutRun> Runs(int start, int length, bool rtl)
        {
            var key = (start, length, rtl);
            if (_cache.TryGetValue(key, out var cached))
                return cached;
            var result = new List<SvgTextLayoutRun>();
            var end = start + length;
            var at = start;
            while (at < end)
            {
                var range = _styles.FirstOrDefault(value => value.Start <= at && value.Start + value.Length > at);
                var style = range?.Style ?? _story.DefaultStyle;
                var next = range is null ? Math.Min(end, _styles.Where(value => value.Start > at).Select(value => value.Start).DefaultIfEmpty(end).Min())
                    : Math.Min(end, range.Start + range.Length);
                var content = _story.Text.Substring(at, next - at).Replace('\t', ' ');
                var paint = Paint(style);
                var faces = _assets.FindTypefaces(content, paint);
                if (faces.Count == 0)
                    faces.Add(new TypefaceSpan(content, 0, paint.Typeface));
                var faceStart = at;
                foreach (var face in faces)
                {
                    paint.Typeface = face.Typeface ?? paint.Typeface;
                    var bounds = default(SK.SKRect);
                    var width = _assets is ISvgTextDirectedGlyphRunResolver shaper && shaper.TryShapeGlyphRun(face.Text, paint, rtl, out var shaped)
                        ? shaped.Advance : _assets.MeasureText(face.Text, paint, ref bounds);
                    width += style.LetterSpacing * Math.Max(0, GraphemeBoundaries(face.Text, 0, face.Text.Length).Count - 2);
                    if (!Finite(width) || width < 0)
                        throw new InvalidOperationException("The font returned an invalid text advance.");
                    result.Add(new SvgTextLayoutRun
                    {
                        Start = faceStart,
                        Length = face.Text.Length,
                        Text = face.Text,
                        Style = style.Clone(),
                        FontFamily = face.Typeface?.FamilyName ?? style.FontFamily,
                        Advance = width
                    });
                    faceStart += face.Text.Length;
                }
                at = next;
            }
            if (_cache.Count > 512)
                _cache.Clear();
            _cache.Add(key, result);
            return result;
        }

        public (float Ascent, float Descent, float Size) Metrics(List<SvgTextLayoutRun> runs, int offset)
        {
            var ascent = 0f;
            var descent = 0f;
            var size = 0f;
            foreach (var run in runs.Count > 0 ? runs : new List<SvgTextLayoutRun> { new() { Style = _story.DefaultStyle } })
            {
                var paint = Paint(run.Style);
                if (runs.Count > 0)
                    paint.Typeface = SK.SKTypeface.FromFamilyName(run.FontFamily, (SK.SKFontStyleWeight)run.Style.FontWeight,
                        SK.SKFontStyleWidth.Normal, run.Style.Italic ? SK.SKFontStyleSlant.Italic : SK.SKFontStyleSlant.Upright);
                var metrics = _assets.GetFontMetrics(paint);
                ascent = Math.Max(ascent, Math.Max(0, -metrics.Ascent));
                descent = Math.Max(descent, Math.Max(0, metrics.Descent));
                size = Math.Max(size, run.Style.FontSize);
            }
            if (ascent + descent <= 0)
                return (size * 0.8f, size * 0.2f, size);
            return (ascent, descent, size);
        }

        private static SK.SKPaint Paint(SvgStoryTextStyle style) => new()
        {
            Typeface = SK.SKTypeface.FromFamilyName(style.FontFamily, (SK.SKFontStyleWeight)style.FontWeight, SK.SKFontStyleWidth.Normal,
                style.Italic ? SK.SKFontStyleSlant.Italic : SK.SKFontStyleSlant.Upright),
            TextSize = style.FontSize,
            FontFeatureSettings = style.FontFeatureSettings,
            FontKerning = "normal",
            IsAntialias = true,
            SubpixelText = true
        };
    }
}
