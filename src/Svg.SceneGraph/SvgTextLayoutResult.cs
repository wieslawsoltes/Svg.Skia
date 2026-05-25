#nullable enable

using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg;

namespace Svg.Skia;

internal sealed class SvgTextLayoutResult
{
    private readonly IReadOnlyList<SvgTextLayoutLine> _lines;
    private readonly IReadOnlyList<SvgTextRenderCommand> _renderCommands;
    private readonly IReadOnlyList<SvgTextLayoutDiagnostic> _diagnostics;

    public SvgTextLayoutResult(
        IEnumerable<SvgTextLayoutLine>? lines,
        IEnumerable<SvgTextRenderCommand>? renderCommands,
        SvgTextDomMetrics? domMetrics,
        SKRect bounds,
        float computedTextLength,
        IEnumerable<SvgTextLayoutDiagnostic>? diagnostics = null)
    {
        _lines = SvgTextLayoutModel.Freeze(lines);
        _renderCommands = SvgTextLayoutModel.Freeze(renderCommands);
        DomMetrics = domMetrics ?? SvgTextDomMetrics.Empty;
        Bounds = bounds;
        ComputedTextLength = computedTextLength;
        _diagnostics = SvgTextLayoutModel.Freeze(diagnostics);
    }

    public IReadOnlyList<SvgTextLayoutLine> Lines => _lines;

    public int LineCount => _lines.Count;

    public IReadOnlyList<SvgTextRenderCommand> RenderCommands => _renderCommands;

    public int RenderCommandCount => _renderCommands.Count;

    public SvgTextDomMetrics DomMetrics { get; }

    public bool HasDomMetrics => DomMetrics.NumberOfChars > 0;

    public SKRect Bounds { get; }

    public float ComputedTextLength { get; }

    public IReadOnlyList<SvgTextLayoutDiagnostic> Diagnostics => _diagnostics;

    public int DiagnosticCount => _diagnostics.Count;

    public bool HasDiagnostics => _diagnostics.Count > 0;
}

internal sealed class SvgTextWrappedLayoutResult
{
    private readonly IReadOnlyList<SvgTextWrappedLayoutLine> _lines;

    public SvgTextWrappedLayoutResult(
        IEnumerable<SvgTextWrappedLayoutLine>? lines,
        SvgTextDomMetrics? domMetrics,
        SKRect bounds,
        float computedTextLength)
    {
        _lines = SvgTextLayoutModel.Freeze(lines);
        DomMetrics = domMetrics ?? SvgTextDomMetrics.Empty;
        Bounds = bounds;
        ComputedTextLength = computedTextLength;
    }

    public IReadOnlyList<SvgTextWrappedLayoutLine> Lines => _lines;

    public SvgTextDomMetrics DomMetrics { get; }

    public SKRect Bounds { get; }

    public float ComputedTextLength { get; }
}

internal sealed class SvgTextWrappedLayoutLine
{
    private readonly IReadOnlyList<SvgTextWrappedClusterPlacement> _placements;
    private readonly IReadOnlyList<SvgTextLayoutResolvedRun> _visualRuns;

    public SvgTextWrappedLayoutLine(
        int lineIndex,
        SvgTextLayoutFlow flow,
        SKPoint baselineOrigin,
        float inlineSize,
        float naturalAdvance,
        float renderedAdvance,
        int inlineProgression,
        IEnumerable<SvgTextWrappedClusterPlacement>? placements,
        IEnumerable<SvgTextLayoutResolvedRun>? visualRuns,
        SvgTextOverflowMarkerPlacement? overflowMarker,
        SKRect bounds)
    {
        LineIndex = lineIndex;
        Flow = flow;
        BaselineOrigin = baselineOrigin;
        InlineSize = inlineSize;
        NaturalAdvance = naturalAdvance;
        RenderedAdvance = renderedAdvance;
        InlineProgression = inlineProgression < 0 ? -1 : 1;
        _placements = SvgTextLayoutModel.Freeze(placements);
        _visualRuns = SvgTextLayoutModel.Freeze(visualRuns);
        OverflowMarker = overflowMarker;
        Bounds = bounds;
    }

    public int LineIndex { get; }

    public SvgTextLayoutFlow Flow { get; }

    public SKPoint BaselineOrigin { get; }

    public float InlineSize { get; }

    public float NaturalAdvance { get; }

    public float RenderedAdvance { get; }

    public int InlineProgression { get; }

    public IReadOnlyList<SvgTextWrappedClusterPlacement> Placements => _placements;

    public IReadOnlyList<SvgTextLayoutResolvedRun> VisualRuns => _visualRuns;

    public SvgTextOverflowMarkerPlacement? OverflowMarker { get; }

    public SKRect Bounds { get; }
}

internal readonly record struct SvgTextWrappedClusterPlacement(
    int ClusterIndex,
    SvgTextIndexRange Utf16Range,
    string Text,
    SvgTextDirection Direction,
    SKPoint Point,
    float InlineOffset,
    float Advance,
    float ScaleX);

internal readonly record struct SvgTextOverflowMarkerPlacement(
    string Text,
    SKPoint Point,
    float InlineOffset,
    float Advance);

internal sealed class SvgTextLayoutLine
{
    private readonly IReadOnlyList<SvgTextLineFragment> _logicalFragments;
    private readonly IReadOnlyList<SvgTextLineFragment> _visualFragments;
    private readonly IReadOnlyList<SvgTextPositionedSpan> _positionedSpans;

    public SvgTextLayoutLine(
        int lineIndex,
        SvgTextLayoutFlow flow,
        SKPoint baselineOrigin,
        float inlineStart,
        float inlineSize,
        float advance,
        SKRect bounds,
        SvgTextShapeLineArea? shapeArea,
        IEnumerable<SvgTextLineFragment>? logicalFragments,
        IEnumerable<SvgTextLineFragment>? visualFragments = null,
        IEnumerable<SvgTextPositionedSpan>? positionedSpans = null,
        SKRect? clip = null,
        bool endsWithForcedBreak = false,
        int inlineProgression = 1,
        float baselineOffset = 0f)
    {
        LineIndex = lineIndex;
        Flow = flow;
        BaselineOrigin = baselineOrigin;
        InlineStart = inlineStart;
        InlineSize = inlineSize;
        Advance = advance;
        Bounds = bounds;
        ShapeArea = shapeArea;
        _logicalFragments = SvgTextLayoutModel.Freeze(logicalFragments);
        _visualFragments = SvgTextLayoutModel.Freeze(visualFragments ?? _logicalFragments);
        _positionedSpans = SvgTextLayoutModel.Freeze(positionedSpans);
        Clip = clip;
        EndsWithForcedBreak = endsWithForcedBreak;
        InlineProgression = inlineProgression < 0 ? -1 : 1;
        BaselineOffset = baselineOffset;
    }

    public int LineIndex { get; }

    public SvgTextLayoutFlow Flow { get; }

    public SKPoint BaselineOrigin { get; }

    public float InlineStart { get; }

    public float InlineSize { get; }

    public float Advance { get; }

    public SKRect Bounds { get; }

    public SvgTextShapeLineArea? ShapeArea { get; }

    public IReadOnlyList<SvgTextLineFragment> LogicalFragments => _logicalFragments;

    public IReadOnlyList<SvgTextLineFragment> VisualFragments => _visualFragments;

    public IReadOnlyList<SvgTextPositionedSpan> PositionedSpans => _positionedSpans;

    public SKRect? Clip { get; }

    public bool EndsWithForcedBreak { get; }

    public int InlineProgression { get; }

    public float BaselineOffset { get; }
}

internal sealed class SvgTextLineFragment
{
    private readonly IReadOnlyList<SvgTextCodepointPlacement> _placements;

    public SvgTextLineFragment(
        SvgTextResolvedStyle style,
        string text,
        SvgTextSourceRange sourceRange,
        float advance,
        SKRect bounds,
        IEnumerable<SvgTextCodepointPlacement>? placements = null,
        SvgTextCodepointRun? codepointRun = null,
        SvgTextGlyphRun? glyphRun = null,
        SvgTextPathRun? textPathRun = null,
        bool isWhitespace = false,
        bool forcesLineBreak = false)
    {
        Style = style ?? throw new ArgumentNullException(nameof(style));
        Text = text ?? string.Empty;
        SourceRange = sourceRange;
        Advance = advance;
        Bounds = bounds;
        _placements = SvgTextLayoutModel.Freeze(placements);
        CodepointRun = codepointRun;
        GlyphRun = glyphRun;
        TextPathRun = textPathRun;
        IsWhitespace = isWhitespace;
        ForcesLineBreak = forcesLineBreak;
    }

    public SvgTextResolvedStyle Style { get; }

    public string Text { get; }

    public SvgTextSourceRange SourceRange { get; }

    public float Advance { get; }

    public SKRect Bounds { get; }

    public IReadOnlyList<SvgTextCodepointPlacement> Placements => _placements;

    public SvgTextCodepointRun? CodepointRun { get; }

    public SvgTextGlyphRun? GlyphRun { get; }

    public SvgTextPathRun? TextPathRun { get; }

    public bool IsWhitespace { get; }

    public bool ForcesLineBreak { get; }
}

internal sealed class SvgTextPositionedSpan
{
    private readonly IReadOnlyList<SvgTextCodepointPlacement> _placements;

    public SvgTextPositionedSpan(
        SvgTextResolvedStyle style,
        string text,
        SvgTextSourceRange sourceRange,
        IEnumerable<SvgTextCodepointPlacement>? placements,
        float advance,
        SKRect bounds,
        SvgTextBase? textLengthSource = null,
        float naturalAdvance = 0f,
        float appliedTextLength = 0f,
        SKPoint baselineOrigin = default)
    {
        Style = style ?? throw new ArgumentNullException(nameof(style));
        Text = text ?? string.Empty;
        SourceRange = sourceRange;
        _placements = SvgTextLayoutModel.Freeze(placements);
        Advance = advance;
        Bounds = bounds;
        TextLengthSource = textLengthSource;
        NaturalAdvance = naturalAdvance;
        AppliedTextLength = appliedTextLength;
        BaselineOrigin = baselineOrigin;
    }

    public SvgTextResolvedStyle Style { get; }

    public string Text { get; }

    public SvgTextSourceRange SourceRange { get; }

    public IReadOnlyList<SvgTextCodepointPlacement> Placements => _placements;

    public float Advance { get; }

    public SKRect Bounds { get; }

    public SvgTextBase? TextLengthSource { get; }

    public float NaturalAdvance { get; }

    public float AppliedTextLength { get; }

    public SKPoint BaselineOrigin { get; }
}

internal sealed class SvgTextShapeLayout
{
    private readonly IReadOnlyList<SvgTextShapeGeometry> _insideShapes;
    private readonly IReadOnlyList<SvgTextShapeGeometry> _subtractShapes;

    public SvgTextShapeLayout(
        SKRect bounds,
        SvgTextLayoutFlow flow,
        IEnumerable<SvgTextShapeGeometry>? insideShapes,
        IEnumerable<SvgTextShapeGeometry>? subtractShapes,
        float firstBaselineOffset)
    {
        Bounds = bounds;
        Flow = flow;
        _insideShapes = SvgTextLayoutModel.Freeze(insideShapes);
        _subtractShapes = SvgTextLayoutModel.Freeze(subtractShapes);
        FirstBaselineOffset = firstBaselineOffset;
    }

    public SKRect Bounds { get; }

    public SvgTextLayoutFlow Flow { get; }

    public IReadOnlyList<SvgTextShapeGeometry> InsideShapes => _insideShapes;

    public IReadOnlyList<SvgTextShapeGeometry> SubtractShapes => _subtractShapes;

    public float FirstBaselineOffset { get; }
}

internal sealed class SvgTextShapeGeometry
{
    private readonly IReadOnlyList<SvgTextPathSample> _samples;
    private readonly SKPath? _path;

    public SvgTextShapeGeometry(
        SvgTextShapeSourceKind sourceKind,
        SKRect bounds,
        IEnumerable<SvgTextPathSample>? samples,
        SKPath? path = null,
        float padding = 0f,
        float margin = 0f,
        float imageThreshold = 0f,
        SKPathFillType fillType = SKPathFillType.Winding,
        string? shapeBox = null,
        SvgElement? sourceElement = null)
    {
        SourceKind = sourceKind;
        Bounds = bounds;
        _samples = SvgTextLayoutModel.Freeze(samples);
        _path = path?.DeepClone();
        Padding = padding;
        Margin = margin;
        ImageThreshold = imageThreshold;
        FillType = fillType;
        ShapeBox = shapeBox;
        SourceElement = sourceElement;
    }

    public SvgTextShapeSourceKind SourceKind { get; }

    public SKRect Bounds { get; }

    public IReadOnlyList<SvgTextPathSample> Samples => _samples;

    public float Padding { get; }

    public float Margin { get; }

    public float ImageThreshold { get; }

    public SKPathFillType FillType { get; }

    public string? ShapeBox { get; }

    public SvgElement? SourceElement { get; }

    public SKPath? CreatePath()
    {
        return _path?.DeepClone();
    }
}

internal readonly record struct SvgTextShapeInterval(
    float Start,
    float End,
    SvgTextShapeSourceKind SourceKind,
    bool IsExclusion)
{
    public float Size => Math.Max(0f, End - Start);
}

internal sealed class SvgTextShapeLineArea
{
    private readonly IReadOnlyList<SvgTextShapeInterval> _intervals;

    public SvgTextShapeLineArea(
        int lineIndex,
        float blockCoordinate,
        float start,
        float inlineSize,
        IEnumerable<SvgTextShapeInterval>? intervals)
    {
        LineIndex = lineIndex;
        BlockCoordinate = blockCoordinate;
        Start = start;
        InlineSize = inlineSize;
        _intervals = SvgTextLayoutModel.Freeze(intervals);
    }

    public int LineIndex { get; }

    public float BlockCoordinate { get; }

    public float Start { get; }

    public float InlineSize { get; }

    public IReadOnlyList<SvgTextShapeInterval> Intervals => _intervals;
}

internal sealed class SvgTextPathGeometry
{
    private readonly IReadOnlyList<SvgTextPathSample> _samples;
    private readonly SKPath? _path;

    public SvgTextPathGeometry(
        SvgTextPath textPath,
        SvgTextPathLayoutMethod method,
        SvgTextPathSide side,
        float pathLength,
        float startOffset,
        bool isClosedLoop,
        IEnumerable<SvgTextPathSample>? samples,
        SKPath? path = null)
    {
        TextPath = textPath ?? throw new ArgumentNullException(nameof(textPath));
        Method = method;
        Side = side;
        PathLength = pathLength;
        StartOffset = startOffset;
        IsClosedLoop = isClosedLoop;
        _samples = SvgTextLayoutModel.Freeze(samples);
        _path = path?.DeepClone();
    }

    public SvgTextPath TextPath { get; }

    public SvgTextPathLayoutMethod Method { get; }

    public SvgTextPathSide Side { get; }

    public float PathLength { get; }

    public float StartOffset { get; }

    public bool IsClosedLoop { get; }

    public IReadOnlyList<SvgTextPathSample> Samples => _samples;

    public SKPath? CreatePath()
    {
        return _path?.DeepClone();
    }
}

internal readonly record struct SvgTextPathPlacement(
    SKPoint Point,
    float Distance,
    float VerticalOffset,
    float RotationDegrees,
    float ScaleX,
    float ScaleOriginX,
    int CodepointIndex);

internal sealed class SvgTextPathCluster
{
    private readonly SKPath? _naturalPath;

    public SvgTextPathCluster(
        string text,
        SvgTextIndexRange sourceRange,
        float naturalOffset,
        float naturalAdvance,
        SKPath? naturalPath = null)
    {
        Text = text ?? string.Empty;
        SourceRange = sourceRange;
        NaturalOffset = naturalOffset;
        NaturalAdvance = naturalAdvance;
        _naturalPath = naturalPath?.DeepClone();
    }

    public string Text { get; }

    public SvgTextIndexRange SourceRange { get; }

    public float NaturalOffset { get; }

    public float NaturalAdvance { get; }

    public SKPath? CreateNaturalPath()
    {
        return _naturalPath?.DeepClone();
    }
}

internal sealed class SvgTextPathRun
{
    private readonly IReadOnlyList<SvgTextPathPlacement> _placements;
    private readonly IReadOnlyList<SvgTextPathCluster> _clusters;

    public SvgTextPathRun(
        SvgTextResolvedStyle style,
        string text,
        SvgTextPathGeometry geometry,
        IEnumerable<SvgTextPathPlacement>? placements,
        IEnumerable<SvgTextPathCluster>? clusters,
        float advance,
        SKRect bounds)
    {
        Style = style ?? throw new ArgumentNullException(nameof(style));
        Text = text ?? string.Empty;
        Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _placements = SvgTextLayoutModel.Freeze(placements);
        _clusters = SvgTextLayoutModel.Freeze(clusters);
        Advance = advance;
        Bounds = bounds;
    }

    public SvgTextResolvedStyle Style { get; }

    public string Text { get; }

    public SvgTextPathGeometry Geometry { get; }

    public IReadOnlyList<SvgTextPathPlacement> Placements => _placements;

    public IReadOnlyList<SvgTextPathCluster> Clusters => _clusters;

    public float Advance { get; }

    public SKRect Bounds { get; }
}

internal sealed class SvgTextDomMetrics
{
    private readonly IReadOnlyList<SvgTextDomClusterMetric> _clusters;

    public static SvgTextDomMetrics Empty { get; } = new(Array.Empty<SvgTextDomClusterMetric>(), 0, 0f);

    public SvgTextDomMetrics(
        IEnumerable<SvgTextDomClusterMetric>? clusters,
        int numberOfChars,
        float computedTextLength)
    {
        _clusters = SvgTextLayoutModel.Freeze(clusters);
        NumberOfChars = numberOfChars;
        ComputedTextLength = computedTextLength;
    }

    public IReadOnlyList<SvgTextDomClusterMetric> Clusters => _clusters;

    public int NumberOfChars { get; }

    public float ComputedTextLength { get; }

    public bool TryGetCluster(int charnum, out SvgTextDomClusterMetric cluster)
    {
        cluster = default;
        if (charnum < 0 || charnum >= NumberOfChars)
        {
            return false;
        }

        var low = 0;
        var high = _clusters.Count - 1;
        while (low <= high)
        {
            var mid = low + ((high - low) >> 1);
            var candidate = _clusters[mid];
            if (charnum < candidate.StartCharIndex)
            {
                high = mid - 1;
                continue;
            }

            if (charnum >= candidate.StartCharIndex + candidate.CharLength)
            {
                low = mid + 1;
                continue;
            }

            cluster = candidate;
            return true;
        }

        return false;
    }

    public float GetSubStringLength(int charnum, int nchars)
    {
        if (nchars <= 0 || charnum < 0 || charnum >= NumberOfChars)
        {
            return 0f;
        }

        var requestedEndCharIndex = (long)charnum + nchars;
        var endCharIndex = requestedEndCharIndex >= NumberOfChars
            ? NumberOfChars
            : (int)requestedEndCharIndex;
        var minStartOffset = float.PositiveInfinity;
        var maxEndOffset = float.NegativeInfinity;
        for (var i = 0; i < _clusters.Count; i++)
        {
            var cluster = _clusters[i];
            var clusterEndCharIndex = cluster.StartCharIndex + cluster.CharLength;
            if (cluster.StartCharIndex >= endCharIndex)
            {
                break;
            }

            if (clusterEndCharIndex <= charnum)
            {
                continue;
            }

            minStartOffset = Math.Min(minStartOffset, cluster.StartOffset);
            maxEndOffset = Math.Max(maxEndOffset, cluster.EndOffset);
        }

        return float.IsInfinity(minStartOffset) || float.IsInfinity(maxEndOffset)
            ? 0f
            : Math.Max(0f, maxEndOffset - minStartOffset);
    }
}

internal readonly record struct SvgTextDomClusterMetric(
    int StartCharIndex,
    int CharLength,
    float StartOffset,
    float EndOffset,
    SKPoint StartPoint,
    SKPoint EndPoint,
    SKRect Extent,
    float RotationDegrees);

internal abstract class SvgTextRenderCommand
{
    protected SvgTextRenderCommand(
        SvgTextRenderCommandKind kind,
        SvgTextPaintPhase phase,
        SvgTextResolvedStyle style,
        SvgTextSourceRange sourceRange,
        SKRect bounds)
    {
        Kind = kind;
        Phase = phase;
        Style = style ?? throw new ArgumentNullException(nameof(style));
        SourceRange = sourceRange;
        Bounds = bounds;
    }

    public SvgTextRenderCommandKind Kind { get; }

    public SvgTextPaintPhase Phase { get; }

    public SvgTextResolvedStyle Style { get; }

    public SvgTextSourceRange SourceRange { get; }

    public SKRect Bounds { get; }
}

internal sealed class SvgTextStringRenderCommand : SvgTextRenderCommand
{
    public SvgTextStringRenderCommand(
        SvgTextPaintPhase phase,
        SvgTextResolvedStyle style,
        SvgTextSourceRange sourceRange,
        string text,
        SKPoint origin,
        SKTextAlign textAlign,
        float advance,
        SKRect bounds)
        : base(SvgTextRenderCommandKind.Text, phase, style, sourceRange, bounds)
    {
        Text = text ?? string.Empty;
        Origin = origin;
        TextAlign = textAlign;
        Advance = advance;
    }

    public string Text { get; }

    public SKPoint Origin { get; }

    public SKTextAlign TextAlign { get; }

    public float Advance { get; }
}

internal sealed class SvgTextPositionedRenderCommand : SvgTextRenderCommand
{
    public SvgTextPositionedRenderCommand(
        SvgTextPaintPhase phase,
        SvgTextPositionedSpan span)
        : base(SvgTextRenderCommandKind.PositionedText, phase, span.Style, span.SourceRange, span.Bounds)
    {
        Span = span ?? throw new ArgumentNullException(nameof(span));
    }

    public SvgTextPositionedSpan Span { get; }
}

internal sealed class SvgTextGlyphRunRenderCommand : SvgTextRenderCommand
{
    public SvgTextGlyphRunRenderCommand(
        SvgTextPaintPhase phase,
        SvgTextSourceRange sourceRange,
        SvgTextGlyphRun glyphRun)
        : base(SvgTextRenderCommandKind.GlyphRun, phase, glyphRun.Style, sourceRange, glyphRun.Bounds)
    {
        GlyphRun = glyphRun ?? throw new ArgumentNullException(nameof(glyphRun));
    }

    public SvgTextGlyphRun GlyphRun { get; }
}

internal sealed class SvgTextPathRenderCommand : SvgTextRenderCommand
{
    public SvgTextPathRenderCommand(
        SvgTextPaintPhase phase,
        SvgTextSourceRange sourceRange,
        SvgTextPathRun run)
        : base(SvgTextRenderCommandKind.TextPath, phase, run.Style, sourceRange, run.Bounds)
    {
        Run = run ?? throw new ArgumentNullException(nameof(run));
    }

    public SvgTextPathRun Run { get; }
}

internal sealed class SvgTextPathOutlineRenderCommand : SvgTextRenderCommand
{
    private readonly SKPath? _path;

    public SvgTextPathOutlineRenderCommand(
        SvgTextPaintPhase phase,
        SvgTextResolvedStyle style,
        SvgTextSourceRange sourceRange,
        SKPath path,
        SKRect bounds)
        : base(SvgTextRenderCommandKind.Path, phase, style, sourceRange, bounds)
    {
        _path = path?.DeepClone() ?? throw new ArgumentNullException(nameof(path));
    }

    public SKPath CreatePath()
    {
        return _path!.DeepClone();
    }
}
