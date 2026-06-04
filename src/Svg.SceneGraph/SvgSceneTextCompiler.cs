using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using ShimSkiaSharp;
using Svg;
using Svg.Model;
using Svg.Model.Services;
using Svg.Pathing;

namespace Svg.Skia;

internal static partial class SvgSceneTextCompiler
{
    private static readonly Regex s_multipleSpaces = new(@" {2,}", RegexOptions.Compiled);
    private static readonly Regex s_numberPrefix = new(@"^[+-]?(?:(?:\d+\.\d*)|(?:\d+)|(?:\.\d+))(?:[eE][+-]?\d+)?", RegexOptions.Compiled);
    private static readonly Regex s_cssUrlReferences = new(@"url\(([^)]*)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly ConcurrentDictionary<string, string[]> s_splitCodepointCache = new(StringComparer.Ordinal);
    private static readonly string[] s_asciiCodepointStrings = CreateAsciiCodepointStrings();
    private const int SplitCodepointCacheLimit = 4096;
    private const int MaxEllipseSteps = 128;
    private const int MaxDefaultTextPathCurveSteps = 768;
    private const int MaxTextPathCurveSteps = 8192;
    private const int TextPathGeometryCacheLimit = 256;
    private const int FallbackCodepointCacheLimit = 4096;
    private const int MinCompactPositionedTextRunCodepoints = 24;
    private const float MaxTextPathSamplingScale = 64f;
    private const float TextPathCurveSamplesPerUnit = 192f;
    private const float TextPathTangentEpsilon = 1e-12f;
    private const float FullCircleRadians = 2f * (float)Math.PI;
    private const float SyntheticSmallCapsScale = 0.75f;
    private const float TextLengthTolerance = 1.5f;
    private const string TextDecorationAttributeName = "text-decoration";
    private static readonly object s_textPathGeometryCacheLock = new();
    private static readonly Dictionary<TextPathGeometryCacheKey, TextPathGeometryCacheEntry> s_textPathGeometryCache = new();
    private static readonly Queue<TextPathGeometryCacheKey> s_textPathGeometryCacheOrder = new();
    private static readonly object s_textPathPlannerSampleCacheLock = new();
    private static readonly ConditionalWeakTable<List<PathSample>, TextPathPlannerSampleCacheEntry> s_textPathPlannerSampleCache = new();
    private static readonly ConditionalWeakTable<SvgDocument, FallbackCodepointResolver> s_fallbackCodepointResolvers = new();

    private enum TextPaintPhase
    {
        Fill,
        Stroke,
        Decorations
    }

    private enum InlineSizeFlow
    {
        HorizontalLeftToRight,
        HorizontalRightToLeft,
        VerticalRightToLeftColumns,
        VerticalLeftToRightColumns
    }

    private readonly record struct SequentialTextRun(SvgTextBase StyleSource, string Text);

    private readonly record struct ShapedSequentialRunSegment(SvgTextBase StyleSource, ushort[] Glyphs, SKPoint[] Points);

    private readonly record struct InlineSizeTextRun(SvgTextBase StyleSource, string Text, float Advance, int SourceCodepointIndex = -1);

    private sealed record MixedScriptSpacingRunLayout(
        string PrefixText,
        float PrefixAdvance,
        float BoundaryAdvance,
        ShapedGlyphRun SuffixGlyphRun,
        SKPaint SuffixPaint,
        float SuffixAdvance,
        string TrailingText,
        float TrailingAdvance,
        float TotalAdvance);

    private readonly record struct InlineSizeTextSegment(InlineSizeTextRun[] Runs, float Advance, bool IsWhitespace, bool ForcesLineBreak);

    private readonly record struct InlineSizeLineBreakOptions(
        bool OverflowWrapAnywhere,
        bool WordBreakBreakAll,
        bool WordBreakKeepAll,
        bool LineBreakAnywhere,
        bool LineBreakLoose,
        bool StrictLineBreak)
    {
        public bool AllowsCharacterBreaks => OverflowWrapAnywhere || WordBreakBreakAll || LineBreakAnywhere;
    }

    private readonly record struct InlineSizeLineAreaFragment(float Start, float End)
    {
        public float InlineSize => Math.Max(0f, End - Start);
    }

    private readonly record struct InlineSizeLineArea(
        float Start,
        float InlineSize,
        float BlockCoordinate,
        InlineSizeLineAreaFragment[]? Fragments = null,
        int InlineProgression = 1);

    private readonly record struct InlineSizeLogicalLine(InlineSizeTextRun[] Runs, float Advance, InlineSizeLineArea Area, int LineIndex);

    private readonly record struct WrappedInlineSizeTextLengthLine(PositionedCodepointRun[] Runs, float StartX, float BaselineY, float Advance);

    private readonly record struct CssCornerRadii(
        float TopLeftX,
        float TopLeftY,
        float TopRightX,
        float TopRightY,
        float BottomRightX,
        float BottomRightY,
        float BottomLeftX,
        float BottomLeftY)
    {
        public bool IsEmpty =>
            TopLeftX <= 0f && TopLeftY <= 0f &&
            TopRightX <= 0f && TopRightY <= 0f &&
            BottomRightX <= 0f && BottomRightY <= 0f &&
            BottomLeftX <= 0f && BottomLeftY <= 0f;
    }

    private readonly record struct InlineSizeTextShape(
        SKRect Bounds,
        PathSample[] Samples,
        float Padding = 0f,
        float Margin = 0f,
        SKPathFillType FillType = SKPathFillType.Winding)
    {
        public SKRect EffectiveBounds
        {
            get
            {
                var inset = Padding - Margin;
                var bounds = new SKRect(
                    Bounds.Left + inset,
                    Bounds.Top + inset,
                    Bounds.Right - inset,
                    Bounds.Bottom - inset);
                return bounds.Right > bounds.Left && bounds.Bottom > bounds.Top
                    ? bounds
                    : SKRect.Empty;
            }
        }

        public List<(float Start, float End)> ResolveHorizontalIntervals(float lineTop, float lineBottom)
        {
            var intervals = new List<(float Start, float End)>();
            var bounds = EffectiveBounds;
            if (bounds.Width <= 0f ||
                bounds.Height <= 0f ||
                lineBottom <= bounds.Top ||
                lineTop >= bounds.Bottom)
            {
                return intervals;
            }

            if (Samples.Length < 2)
            {
                intervals.Add((bounds.Left, bounds.Right));
                return intervals;
            }

            var lineCenterY = Math.Max(bounds.Top, Math.Min(bounds.Bottom, (lineTop + lineBottom) * 0.5f));
            var intersections = new List<(float Coordinate, int WindingDelta)>();
            for (var i = 1; i < Samples.Length; i++)
            {
                if (Samples[i].StartsSubpath)
                {
                    continue;
                }

                var start = Samples[i - 1].Point;
                var end = Samples[i].Point;
                if (Math.Abs(end.Y - start.Y) <= 0.0001f)
                {
                    continue;
                }

                if (!((start.Y <= lineCenterY && end.Y > lineCenterY) ||
                      (end.Y <= lineCenterY && start.Y > lineCenterY)))
                {
                    continue;
                }

                var t = (lineCenterY - start.Y) / (end.Y - start.Y);
                intersections.Add((start.X + ((end.X - start.X) * t), end.Y > start.Y ? 1 : -1));
            }

            intersections.Sort(static (left, right) =>
            {
                var coordinateComparison = left.Coordinate.CompareTo(right.Coordinate);
                return coordinateComparison != 0
                    ? coordinateComparison
                    : left.WindingDelta.CompareTo(right.WindingDelta);
            });
            var edgeInset = Padding - Margin;
            if (FillType == SKPathFillType.EvenOdd)
            {
                for (var i = 0; i + 1 < intersections.Count; i += 2)
                {
                    AddInterval(intersections[i].Coordinate, intersections[i + 1].Coordinate);
                }

                return intervals;
            }

            var winding = 0;
            float? startX = null;
            for (var i = 0; i < intersections.Count; i++)
            {
                var wasInside = winding != 0;
                winding += intersections[i].WindingDelta;
                var isInside = winding != 0;
                if (!wasInside && isInside)
                {
                    startX = intersections[i].Coordinate;
                }
                else if (wasInside && !isInside && startX.HasValue)
                {
                    AddInterval(startX.Value, intersections[i].Coordinate);
                    startX = null;
                }
            }

            void AddInterval(float rawLeft, float rawRight)
            {
                var left = Math.Max(bounds.Left, Math.Min(rawLeft, rawRight) + edgeInset);
                var right = Math.Min(bounds.Right, Math.Max(rawLeft, rawRight) - edgeInset);
                if (right > left + TextLengthTolerance)
                {
                    intervals.Add((left, right));
                }
            }

            return intervals;
        }

        public List<(float Start, float End)> ResolveVerticalIntervals(float lineLeft, float lineRight)
        {
            var intervals = new List<(float Start, float End)>();
            var bounds = EffectiveBounds;
            if (bounds.Width <= 0f ||
                bounds.Height <= 0f ||
                lineRight <= bounds.Left ||
                lineLeft >= bounds.Right)
            {
                return intervals;
            }

            if (Samples.Length < 2)
            {
                intervals.Add((bounds.Top, bounds.Bottom));
                return intervals;
            }

            var lineCenterX = Math.Max(bounds.Left, Math.Min(bounds.Right, (lineLeft + lineRight) * 0.5f));
            var intersections = new List<(float Coordinate, int WindingDelta)>();
            for (var i = 1; i < Samples.Length; i++)
            {
                if (Samples[i].StartsSubpath)
                {
                    continue;
                }

                var start = Samples[i - 1].Point;
                var end = Samples[i].Point;
                if (Math.Abs(end.X - start.X) <= 0.0001f)
                {
                    continue;
                }

                if (!((start.X <= lineCenterX && end.X > lineCenterX) ||
                      (end.X <= lineCenterX && start.X > lineCenterX)))
                {
                    continue;
                }

                var t = (lineCenterX - start.X) / (end.X - start.X);
                intersections.Add((start.Y + ((end.Y - start.Y) * t), end.X > start.X ? 1 : -1));
            }

            intersections.Sort(static (top, bottom) =>
            {
                var coordinateComparison = top.Coordinate.CompareTo(bottom.Coordinate);
                return coordinateComparison != 0
                    ? coordinateComparison
                    : top.WindingDelta.CompareTo(bottom.WindingDelta);
            });
            var edgeInset = Padding - Margin;
            if (FillType == SKPathFillType.EvenOdd)
            {
                for (var i = 0; i + 1 < intersections.Count; i += 2)
                {
                    AddInterval(intersections[i].Coordinate, intersections[i + 1].Coordinate);
                }

                return intervals;
            }

            var winding = 0;
            float? startY = null;
            for (var i = 0; i < intersections.Count; i++)
            {
                var wasInside = winding != 0;
                winding += intersections[i].WindingDelta;
                var isInside = winding != 0;
                if (!wasInside && isInside)
                {
                    startY = intersections[i].Coordinate;
                }
                else if (wasInside && !isInside && startY.HasValue)
                {
                    AddInterval(startY.Value, intersections[i].Coordinate);
                    startY = null;
                }
            }

            void AddInterval(float rawTop, float rawBottom)
            {
                var top = Math.Max(bounds.Top, Math.Min(rawTop, rawBottom) + edgeInset);
                var bottom = Math.Min(bounds.Bottom, Math.Max(rawTop, rawBottom) - edgeInset);
                if (bottom > top + TextLengthTolerance)
                {
                    intervals.Add((top, bottom));
                }
            }

            return intervals;
        }

        public SvgTextLineShape ToTextLineShape()
        {
            var samples = Samples.Length == 0
                ? Array.Empty<SvgTextLineShapeSample>()
                : new SvgTextLineShapeSample[Samples.Length];
            for (var i = 0; i < Samples.Length; i++)
            {
                samples[i] = new SvgTextLineShapeSample(
                    Samples[i].Point,
                    Samples[i].Distance,
                    Samples[i].StartsSubpath,
                    Samples[i].ClosesSubpath);
            }

            return new SvgTextLineShape(Bounds, samples, Padding, Margin, FillType);
        }
    }

    private sealed class InlineSizeTextArea
    {
        public InlineSizeTextArea(
            SKRect bounds,
            InlineSizeTextShape? insideShape,
            InlineSizeTextShape[] subtractShapes,
            bool isShapeInside,
            bool isVertical,
            InlineSizeFlow flow,
            bool isInlineDirectionReversed = false)
            : this(
                bounds,
                insideShape.HasValue ? new[] { insideShape.Value } : Array.Empty<InlineSizeTextShape>(),
                subtractShapes,
                isShapeInside,
                isVertical,
                flow,
                shapeFirstBaselineOffset: 0f,
                isInlineDirectionReversed: isInlineDirectionReversed)
        {
        }

        public InlineSizeTextArea(
            SKRect bounds,
            InlineSizeTextShape[] insideShapes,
            InlineSizeTextShape[] subtractShapes,
            bool isShapeInside,
            bool isVertical,
            InlineSizeFlow flow,
            float shapeFirstBaselineOffset,
            bool isInlineDirectionReversed = false)
        {
            Bounds = bounds;
            InsideShapes = insideShapes;
            SubtractShapes = subtractShapes;
            IsShapeInside = isShapeInside;
            IsVertical = isVertical;
            Flow = flow;
            ShapeFirstBaselineOffset = shapeFirstBaselineOffset;
            _lineAreaProvider = new SvgTextLineAreaProvider(
                bounds,
                ToTextLineShapes(insideShapes),
                ToTextLineShapes(subtractShapes),
                isShapeInside,
                isVertical,
                ToTextLineAreaFlow(flow),
                isInlineDirectionReversed,
                shapeFirstBaselineOffset);
        }

        private readonly SvgTextLineAreaProvider _lineAreaProvider;

        public SKRect Bounds { get; }

        public IReadOnlyList<InlineSizeTextShape> InsideShapes { get; }

        public IReadOnlyList<InlineSizeTextShape> SubtractShapes { get; }

        public bool IsShapeInside { get; }

        public bool HasShapeSubtract => SubtractShapes.Count > 0;

        public bool IsVertical { get; }

        public InlineSizeFlow Flow { get; }

        public float ShapeFirstBaselineOffset { get; }

        public InlineSizeLineArea ResolveLineArea(float blockCoordinate, float lineAdvance)
        {
            return ToInlineSizeLineArea(_lineAreaProvider.ResolveLineArea(blockCoordinate, lineAdvance));
        }

        public InlineSizeLineArea ResolveWrappedLineArea(int lineIndex, float firstBlockCoordinate, float lineAdvance, int blockProgression)
        {
            return ToInlineSizeLineArea(_lineAreaProvider.ResolveWrappedLineArea(lineIndex, firstBlockCoordinate, lineAdvance, blockProgression));
        }

        public int GetMaxWrappedLineSearchCount(float firstBlockCoordinate, float lineAdvance, int segmentCount, int blockProgression = 1)
        {
            return _lineAreaProvider.GetMaxWrappedLineSearchCount(firstBlockCoordinate, lineAdvance, segmentCount, blockProgression);
        }

        private static InlineSizeLineArea ToInlineSizeLineArea(SvgTextLineArea area)
        {
            var fragments = area.Fragments.Length == 0
                ? Array.Empty<InlineSizeLineAreaFragment>()
                : new InlineSizeLineAreaFragment[area.Fragments.Length];
            for (var i = 0; i < area.Fragments.Length; i++)
            {
                fragments[i] = new InlineSizeLineAreaFragment(area.Fragments[i].Start, area.Fragments[i].End);
            }

            return new InlineSizeLineArea(area.Start, area.InlineSize, area.BlockCoordinate, fragments, area.InlineProgression);
        }

        private static SvgTextLineShape[] ToTextLineShapes(IReadOnlyList<InlineSizeTextShape> shapes)
        {
            if (shapes.Count == 0)
            {
                return Array.Empty<SvgTextLineShape>();
            }

            var textLineShapes = new SvgTextLineShape[shapes.Count];
            for (var i = 0; i < shapes.Count; i++)
            {
                textLineShapes[i] = shapes[i].ToTextLineShape();
            }

            return textLineShapes;
        }

        private static SvgTextLineAreaFlow ToTextLineAreaFlow(InlineSizeFlow flow)
        {
            return flow switch
            {
                InlineSizeFlow.HorizontalRightToLeft => SvgTextLineAreaFlow.HorizontalRightToLeft,
                InlineSizeFlow.VerticalRightToLeftColumns => SvgTextLineAreaFlow.VerticalRightToLeftColumns,
                InlineSizeFlow.VerticalLeftToRightColumns => SvgTextLineAreaFlow.VerticalLeftToRightColumns,
                _ => SvgTextLineAreaFlow.HorizontalLeftToRight
            };
        }
    }

    private sealed class InlineSizeTextLine
    {
        public InlineSizeTextLine(
            InlineSizeTextRun[] runs,
            InlineSizeTextRun[] visualRuns,
            SKRect clipRect,
            float startX,
            float baselineY,
            float logicalAdvance,
            bool placeVisualRunsRightToLeft,
            bool shouldClip)
        {
            Runs = runs;
            VisualRuns = visualRuns;
            ClipRect = clipRect;
            StartX = startX;
            BaselineY = baselineY;
            LogicalAdvance = logicalAdvance;
            PlaceVisualRunsRightToLeft = placeVisualRunsRightToLeft;
            ShouldClip = shouldClip;
        }

        public IReadOnlyList<InlineSizeTextRun> Runs { get; }

        public IReadOnlyList<InlineSizeTextRun> VisualRuns { get; }

        public SKRect ClipRect { get; }

        public float StartX { get; }

        public float BaselineY { get; }

        public float LogicalAdvance { get; }

        public bool PlaceVisualRunsRightToLeft { get; }

        public bool ShouldClip { get; }
    }

    private sealed class InlineSizeTextOverflowLayout
    {
        public InlineSizeTextOverflowLayout(
            InlineSizeTextLine[] lines,
            float finalX,
            float finalY)
        {
            Lines = lines;
            FinalX = finalX;
            FinalY = finalY;
        }

        public IReadOnlyList<InlineSizeTextLine> Lines { get; }

        public float FinalX { get; }

        public float FinalY { get; }
    }

    private sealed class WrappedInlineSizeTextLengthLayout
    {
        public WrappedInlineSizeTextLengthLayout(
            WrappedInlineSizeTextLengthLine[] lines,
            float finalX,
            float finalY)
        {
            Lines = lines;
            FinalX = finalX;
            FinalY = finalY;
        }

        public IReadOnlyList<WrappedInlineSizeTextLengthLine> Lines { get; }

        public float FinalX { get; }

        public float FinalY { get; }
    }

    private readonly record struct ResolvedSequentialCompileRun(
        SvgTextBase StyleSource,
        string DrawText,
        SKTypeface? Typeface,
        float Advance,
        SKRect RelativeBounds);

    private readonly record struct AlignedSequentialCompileRun(
        SvgTextBase StyleSource,
        string Text,
        PositionedCodepointPlacement[] Placements,
        IReadOnlyList<string> Codepoints,
        float Advance,
        float BoundaryAdvance);

    private readonly record struct SimplePositionedSequentialCompileRun(
        SvgTextBase StyleSource,
        string Text,
        SKPoint[] Points,
        SKTypeface Typeface,
        SKRect RelativeBounds,
        float Advance,
        float BoundaryAdvance);

    private readonly record struct SimpleScaledTextLengthSequentialCompileRun(
        SvgTextBase StyleSource,
        string Text,
        SKTypeface Typeface,
        float ScaleX,
        SKRect RelativeBounds,
        float Advance);

    private readonly record struct LogicalBidiRun(int StartCharIndex, int Length, SvgTextDirection Direction);

    private readonly record struct TextPathRun(SvgTextBase StyleSource, SvgTextBase TextPathSource, string Text, float Dx, float Dy, float? X, float? Y);

    private readonly record struct InlineSizeTextPathFragment(
        SvgTextPath TextPath,
        TextPathRun[] Runs,
        IReadOnlyList<PathSample> PathSamples,
        bool IsClosedLoop,
        SKRect GeometryBounds,
        float PathOffset,
        float VerticalOffset,
        float AnchorX,
        float BaselineY,
        float FinalX,
        float FinalY,
        float TotalAdvance);

    private enum InlineSizeTextPathFlowRunKind
    {
        Text,
        TextPath
    }

    private readonly record struct InlineSizeTextPathFlowRun(
        InlineSizeTextPathFlowRunKind Kind,
        SvgTextBase StyleSource,
        string Text,
        float Advance,
        SvgTextPath? TextPath = null,
        TextPathRun[]? TextPathRuns = null,
        IReadOnlyList<PathSample>? PathSamples = null,
        bool IsClosedLoop = false,
        SKRect GeometryBounds = default,
        float PathLength = 0f,
        float ResolvedStartOffset = 0f,
        float? SpecifiedLengthOverride = null);

    private readonly record struct InlineSizeTextPathFlowSegment(int[] RunIndexes, float Advance, bool IsWhitespace, bool ForcesLineBreak);

    private readonly record struct InlineSizeTextPathFlowLine(int[] RunIndexes, float Advance, float StartX, float BaselineY);

    private sealed class InlineSizeTextPathFlowLayout
    {
        public InlineSizeTextPathFlowLayout(
            InlineSizeTextPathFlowRun[] runs,
            InlineSizeTextPathFlowLine[] lines,
            float finalX,
            float finalY)
        {
            Runs = runs;
            Lines = lines;
            FinalX = finalX;
            FinalY = finalY;
        }

        public IReadOnlyList<InlineSizeTextPathFlowRun> Runs { get; }

        public IReadOnlyList<InlineSizeTextPathFlowLine> Lines { get; }

        public float FinalX { get; }

        public float FinalY { get; }
    }

    private readonly record struct PositionedTextPathRun(
        SvgTextBase StyleSource,
        string Text,
        PositionedCodepointPlacement[] Placements,
        SKRect FastBounds,
        bool HasFastBounds);

    private readonly record struct DirectTextPathRenderGroup(SKRect GeometryBounds, List<PositionedTextPathRun> Runs);

    private readonly record struct StretchedTextPathRun(SvgTextBase StyleSource, SvgTextBase CommandSource, SvgTextBase FilterSource, string Text, SKPath Path, IReadOnlyList<StretchedTextPathDecorationRun> Decorations);

    private readonly record struct StretchedTextPathDecorationRun(TextDecorationLayer Layer, SKPath Path);

    private readonly record struct StretchedTextPathCluster(string Text, float NaturalOffset, float NaturalAdvance, SKPath? NaturalPath = null);

    private readonly record struct PositionedCodepointRun(SvgTextBase StyleSource, string Text, PositionedCodepointPlacement[] Placements);

    private struct WrappedTextLengthRunGroup
    {
        public WrappedTextLengthRunGroup(SvgTextBase styleSource, int start, int end, int renderedCount)
        {
            StyleSource = styleSource;
            Start = start;
            End = end;
            CharCount = 0;
            PlacementIndex = 0;
            Placements = new PositionedCodepointPlacement[renderedCount];
        }

        public SvgTextBase StyleSource { get; }

        public int Start { get; }

        public int End { get; }

        public int CharCount { get; private set; }

        public int PlacementIndex { get; private set; }

        public PositionedCodepointPlacement[] Placements { get; }

        public void AddPlacement(int charLength, PositionedCodepointPlacement placement)
        {
            CharCount += charLength;
            Placements[PlacementIndex] = placement;
            PlacementIndex++;
        }
    }

    private sealed class FlattenedCodepointTextList : IReadOnlyList<string>
    {
        private readonly IReadOnlyList<FlattenedTextCodepoint> _codepoints;
        private readonly int _start;
        private readonly int _count;

        public FlattenedCodepointTextList(IReadOnlyList<FlattenedTextCodepoint> codepoints)
            : this(codepoints, 0, codepoints.Count)
        {
        }

        public FlattenedCodepointTextList(IReadOnlyList<FlattenedTextCodepoint> codepoints, int start, int count)
        {
            _codepoints = codepoints;
            _start = start;
            _count = count;
        }

        public int Count => _count;

        public string this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _codepoints[_start + index].Codepoint;
            }
        }

        public IEnumerator<string> GetEnumerator()
        {
            var end = _start + _count;
            for (var i = _start; i < end; i++)
            {
                yield return _codepoints[i].Codepoint;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private readonly record struct PathSample(SKPoint Point, float Distance, bool StartsSubpath, bool ClosesSubpath);

    private enum TextPathGeometrySourceKind
    {
        InlinePathData,
        ReferencedElement
    }

    private readonly struct TextPathGeometryCacheKey : IEquatable<TextPathGeometryCacheKey>
    {
        private readonly TextPathGeometrySourceKind _sourceKind;
        private readonly object _sourceIdentity;
        private readonly long _geometrySignature;
        private readonly SKMatrix _transform;
        private readonly SvgTextPathSide _side;
        private readonly float _samplingScale;
        private readonly float _viewportLeft;
        private readonly float _viewportTop;
        private readonly float _viewportRight;
        private readonly float _viewportBottom;
        private readonly int _hashCode;

        public TextPathGeometryCacheKey(
            TextPathGeometrySourceKind sourceKind,
            object sourceIdentity,
            long geometrySignature,
            SKMatrix transform,
            SvgTextPathSide side,
            float samplingScale,
            SKRect viewport)
        {
            _sourceKind = sourceKind;
            _sourceIdentity = sourceIdentity;
            _geometrySignature = geometrySignature;
            _transform = transform;
            _side = side;
            _samplingScale = samplingScale;
            _viewportLeft = viewport.Left;
            _viewportTop = viewport.Top;
            _viewportRight = viewport.Right;
            _viewportBottom = viewport.Bottom;
            _hashCode = CreateHashCode(
                sourceKind,
                sourceIdentity,
                geometrySignature,
                transform,
                side,
                samplingScale,
                viewport);
        }

        public bool Equals(TextPathGeometryCacheKey other)
        {
            return _sourceKind == other._sourceKind &&
                   ReferenceEquals(_sourceIdentity, other._sourceIdentity) &&
                   _geometrySignature == other._geometrySignature &&
                   SvgSceneTextCompiler.MatrixEquals(_transform, other._transform) &&
                   _side == other._side &&
                   _samplingScale == other._samplingScale &&
                   _viewportLeft == other._viewportLeft &&
                   _viewportTop == other._viewportTop &&
                   _viewportRight == other._viewportRight &&
                   _viewportBottom == other._viewportBottom;
        }

        public override bool Equals(object? obj) => obj is TextPathGeometryCacheKey other && Equals(other);

        public override int GetHashCode() => _hashCode;

        private static int CreateHashCode(
            TextPathGeometrySourceKind sourceKind,
            object sourceIdentity,
            long geometrySignature,
            SKMatrix transform,
            SvgTextPathSide side,
            float samplingScale,
            SKRect viewport)
        {
            var hash = 17;
            hash = SvgSceneTextCompiler.CombineHash(hash, (int)sourceKind);
            hash = SvgSceneTextCompiler.CombineHash(hash, RuntimeHelpers.GetHashCode(sourceIdentity));
            hash = SvgSceneTextCompiler.CombineHash(hash, geometrySignature.GetHashCode());
            hash = SvgSceneTextCompiler.CombineHash(hash, transform.ScaleX.GetHashCode());
            hash = SvgSceneTextCompiler.CombineHash(hash, transform.SkewX.GetHashCode());
            hash = SvgSceneTextCompiler.CombineHash(hash, transform.TransX.GetHashCode());
            hash = SvgSceneTextCompiler.CombineHash(hash, transform.SkewY.GetHashCode());
            hash = SvgSceneTextCompiler.CombineHash(hash, transform.ScaleY.GetHashCode());
            hash = SvgSceneTextCompiler.CombineHash(hash, transform.TransY.GetHashCode());
            hash = SvgSceneTextCompiler.CombineHash(hash, transform.Persp0.GetHashCode());
            hash = SvgSceneTextCompiler.CombineHash(hash, transform.Persp1.GetHashCode());
            hash = SvgSceneTextCompiler.CombineHash(hash, transform.Persp2.GetHashCode());
            hash = SvgSceneTextCompiler.CombineHash(hash, (int)side);
            hash = SvgSceneTextCompiler.CombineHash(hash, samplingScale.GetHashCode());
            hash = SvgSceneTextCompiler.CombineHash(hash, viewport.Left.GetHashCode());
            hash = SvgSceneTextCompiler.CombineHash(hash, viewport.Top.GetHashCode());
            hash = SvgSceneTextCompiler.CombineHash(hash, viewport.Right.GetHashCode());
            hash = SvgSceneTextCompiler.CombineHash(hash, viewport.Bottom.GetHashCode());
            return hash;
        }
    }

    private sealed class TextPathGeometryCacheEntry
    {
        public TextPathGeometryCacheEntry(
            SvgPath? svgPath,
            SKPath skPath,
            SKRect geometryBounds,
            List<PathSample> pathSamples,
            float pathLength,
            bool isClosedLoop)
        {
            SvgPath = svgPath;
            SkPath = skPath;
            GeometryBounds = geometryBounds;
            PathSamples = pathSamples;
            PathLength = pathLength;
            IsClosedLoop = isClosedLoop;
        }

        public SvgPath? SvgPath { get; }

        public SKPath SkPath { get; }

        public SKRect GeometryBounds { get; }

        public List<PathSample> PathSamples { get; }

        public float PathLength { get; }

        public bool IsClosedLoop { get; }
    }

    private readonly struct FallbackCodepointCacheKey : IEquatable<FallbackCodepointCacheKey>
    {
        public FallbackCodepointCacheKey(SvgTextBase svgTextBase, string text, SKPaint paint)
        {
            Text = text;
            FontFamily = svgTextBase.FontFamily;
            FontVariant = svgTextBase.FontVariant;
            Style = paint.Style;
            IsAntialias = paint.IsAntialias;
            IsDither = paint.IsDither;
            StrokeWidth = paint.StrokeWidth;
            StrokeCap = paint.StrokeCap;
            StrokeJoin = paint.StrokeJoin;
            StrokeMiter = paint.StrokeMiter;
            IsStrokeNonScaling = paint.IsStrokeNonScaling;
            TextSize = paint.TextSize;
            TextAlign = paint.TextAlign;
            LcdRenderText = paint.LcdRenderText;
            SubpixelText = paint.SubpixelText;
            TextEncoding = paint.TextEncoding;
            FontFeatureSettings = paint.FontFeatureSettings;
            FontKerning = paint.FontKerning;
            FontVariantLigatures = paint.FontVariantLigatures;
            Color = paint.Color;
            Shader = paint.Shader;
            ColorFilter = paint.ColorFilter;
            ImageFilter = paint.ImageFilter;
            PathEffect = paint.PathEffect;
            BlendMode = paint.BlendMode;
            FilterQuality = paint.FilterQuality;

            if (paint.Typeface is { } typeface)
            {
                HasTypeface = true;
                TypefaceFamilyName = typeface.FamilyName;
                TypefaceWeight = typeface.FontWeight;
                TypefaceWidth = typeface.FontWidth;
                TypefaceSlant = typeface.FontSlant;
            }
            else
            {
                HasTypeface = false;
                TypefaceFamilyName = null;
                TypefaceWeight = default;
                TypefaceWidth = default;
                TypefaceSlant = default;
            }
        }

        private string Text { get; }
        private string? FontFamily { get; }
        private SvgFontVariant FontVariant { get; }
        private SKPaintStyle Style { get; }
        private bool IsAntialias { get; }
        private bool IsDither { get; }
        private float StrokeWidth { get; }
        private SKStrokeCap StrokeCap { get; }
        private SKStrokeJoin StrokeJoin { get; }
        private float StrokeMiter { get; }
        private bool IsStrokeNonScaling { get; }
        private float TextSize { get; }
        private SKTextAlign TextAlign { get; }
        private bool LcdRenderText { get; }
        private bool SubpixelText { get; }
        private SKTextEncoding TextEncoding { get; }
        private string? FontFeatureSettings { get; }
        private string? FontKerning { get; }
        private string? FontVariantLigatures { get; }
        private SKColor? Color { get; }
        private SKShader? Shader { get; }
        private SKColorFilter? ColorFilter { get; }
        private SKImageFilter? ImageFilter { get; }
        private SKPathEffect? PathEffect { get; }
        private SKBlendMode BlendMode { get; }
        private SKFilterQuality FilterQuality { get; }
        private bool HasTypeface { get; }
        private string? TypefaceFamilyName { get; }
        private SKFontStyleWeight TypefaceWeight { get; }
        private SKFontStyleWidth TypefaceWidth { get; }
        private SKFontStyleSlant TypefaceSlant { get; }

        public bool Equals(FallbackCodepointCacheKey other)
        {
            return string.Equals(Text, other.Text, StringComparison.Ordinal) &&
                   string.Equals(FontFamily, other.FontFamily, StringComparison.Ordinal) &&
                   FontVariant == other.FontVariant &&
                   Style == other.Style &&
                   IsAntialias == other.IsAntialias &&
                   IsDither == other.IsDither &&
                   StrokeWidth.Equals(other.StrokeWidth) &&
                   StrokeCap == other.StrokeCap &&
                   StrokeJoin == other.StrokeJoin &&
                   StrokeMiter.Equals(other.StrokeMiter) &&
                   IsStrokeNonScaling == other.IsStrokeNonScaling &&
                   TextSize.Equals(other.TextSize) &&
                   TextAlign == other.TextAlign &&
                   LcdRenderText == other.LcdRenderText &&
                   SubpixelText == other.SubpixelText &&
                   TextEncoding == other.TextEncoding &&
                   string.Equals(FontFeatureSettings, other.FontFeatureSettings, StringComparison.Ordinal) &&
                   string.Equals(FontKerning, other.FontKerning, StringComparison.Ordinal) &&
                   string.Equals(FontVariantLigatures, other.FontVariantLigatures, StringComparison.Ordinal) &&
                   Color.Equals(other.Color) &&
                   ReferenceEquals(Shader, other.Shader) &&
                   ReferenceEquals(ColorFilter, other.ColorFilter) &&
                   ReferenceEquals(ImageFilter, other.ImageFilter) &&
                   ReferenceEquals(PathEffect, other.PathEffect) &&
                   BlendMode == other.BlendMode &&
                   FilterQuality == other.FilterQuality &&
                   HasTypeface == other.HasTypeface &&
                   string.Equals(TypefaceFamilyName, other.TypefaceFamilyName, StringComparison.Ordinal) &&
                   TypefaceWeight == other.TypefaceWeight &&
                   TypefaceWidth == other.TypefaceWidth &&
                   TypefaceSlant == other.TypefaceSlant;
        }

        public override bool Equals(object? obj)
        {
            return obj is FallbackCodepointCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(Text);
                hash = (hash * 397) ^ (FontFamily is null ? 0 : StringComparer.Ordinal.GetHashCode(FontFamily));
                hash = (hash * 397) ^ (int)FontVariant;
                hash = (hash * 397) ^ (int)Style;
                hash = (hash * 397) ^ (IsAntialias ? 1 : 0);
                hash = (hash * 397) ^ (IsDither ? 1 : 0);
                hash = (hash * 397) ^ StrokeWidth.GetHashCode();
                hash = (hash * 397) ^ (int)StrokeCap;
                hash = (hash * 397) ^ (int)StrokeJoin;
                hash = (hash * 397) ^ StrokeMiter.GetHashCode();
                hash = (hash * 397) ^ (IsStrokeNonScaling ? 1 : 0);
                hash = (hash * 397) ^ TextSize.GetHashCode();
                hash = (hash * 397) ^ (int)TextAlign;
                hash = (hash * 397) ^ (LcdRenderText ? 1 : 0);
                hash = (hash * 397) ^ (SubpixelText ? 1 : 0);
                hash = (hash * 397) ^ (int)TextEncoding;
                hash = (hash * 397) ^ (FontFeatureSettings is null ? 0 : StringComparer.Ordinal.GetHashCode(FontFeatureSettings));
                hash = (hash * 397) ^ (FontKerning is null ? 0 : StringComparer.Ordinal.GetHashCode(FontKerning));
                hash = (hash * 397) ^ (FontVariantLigatures is null ? 0 : StringComparer.Ordinal.GetHashCode(FontVariantLigatures));
                hash = (hash * 397) ^ Color.GetHashCode();
                hash = (hash * 397) ^ (Shader is null ? 0 : RuntimeHelpers.GetHashCode(Shader));
                hash = (hash * 397) ^ (ColorFilter is null ? 0 : RuntimeHelpers.GetHashCode(ColorFilter));
                hash = (hash * 397) ^ (ImageFilter is null ? 0 : RuntimeHelpers.GetHashCode(ImageFilter));
                hash = (hash * 397) ^ (PathEffect is null ? 0 : RuntimeHelpers.GetHashCode(PathEffect));
                hash = (hash * 397) ^ (int)BlendMode;
                hash = (hash * 397) ^ (int)FilterQuality;
                hash = (hash * 397) ^ (HasTypeface ? 1 : 0);
                hash = (hash * 397) ^ (TypefaceFamilyName is null ? 0 : StringComparer.Ordinal.GetHashCode(TypefaceFamilyName));
                hash = (hash * 397) ^ (int)TypefaceWeight;
                hash = (hash * 397) ^ (int)TypefaceWidth;
                hash = (hash * 397) ^ (int)TypefaceSlant;
                return hash;
            }
        }
    }

    private sealed class FallbackCodepointResolver
    {
        private readonly Dictionary<FallbackCodepointCacheKey, ResolvedFallbackCodepoint> _cache = new();
        private readonly object _lock = new();

        public bool TryGet(SvgTextBase svgTextBase, string codepoint, SKPaint paint, out ResolvedFallbackCodepoint resolved)
        {
            lock (_lock)
            {
                return _cache.TryGetValue(new FallbackCodepointCacheKey(svgTextBase, codepoint, paint), out resolved);
            }
        }

        public ResolvedFallbackCodepoint Resolve(
            SvgTextBase svgTextBase,
            string codepoint,
            SKPaint paint,
            ISvgAssetLoader assetLoader)
        {
            var key = new FallbackCodepointCacheKey(svgTextBase, codepoint, paint);
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var resolved))
                {
                    return resolved;
                }
            }

            var resolvedFallback = ResolveFallbackCodepoint(svgTextBase, codepoint, paint, assetLoader);
            lock (_lock)
            {
                _cache[key] = resolvedFallback;
                if (_cache.Count > FallbackCodepointCacheLimit)
                {
                    _cache.Clear();
                    _cache[key] = resolvedFallback;
                }
            }

            return resolvedFallback;
        }

        public ResolvedFallbackCodepoint ResolveWithLocalBounds(
            SvgTextBase svgTextBase,
            string codepoint,
            SKPaint paint,
            ISvgAssetLoader assetLoader)
        {
            var key = new FallbackCodepointCacheKey(svgTextBase, codepoint, paint);
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var resolved) && resolved.HasLocalBounds)
                {
                    return resolved;
                }

                if (_cache.TryGetValue(key, out resolved))
                {
                    var resolvedWithBounds = AddLocalBounds(resolved, assetLoader);
                    _cache[key] = resolvedWithBounds;
                    return resolvedWithBounds;
                }
            }

            var resolvedFallback = AddLocalBounds(ResolveFallbackCodepoint(svgTextBase, codepoint, paint, assetLoader), assetLoader);
            lock (_lock)
            {
                _cache[key] = resolvedFallback;
                if (_cache.Count > FallbackCodepointCacheLimit)
                {
                    _cache.Clear();
                    _cache[key] = resolvedFallback;
                }
            }

            return resolvedFallback;
        }
    }

    private static FallbackCodepointResolver GetFallbackCodepointResolver(SvgTextBase svgTextBase)
    {
        return svgTextBase.OwnerDocument is { } ownerDocument
            ? s_fallbackCodepointResolvers.GetValue(ownerDocument, static _ => new FallbackCodepointResolver())
            : new FallbackCodepointResolver();
    }

    private static ResolvedFallbackCodepoint AddLocalBounds(ResolvedFallbackCodepoint resolved, ISvgAssetLoader assetLoader)
    {
        return TryGetRenderedTextLocalBounds(resolved.Text, resolved.Paint, assetLoader, out var localBounds)
            ? resolved with { LocalBounds = localBounds, HasLocalBounds = true }
            : resolved;
    }

    private readonly record struct ResolvedFallbackCodepoint(string Text, SKPaint Paint, float Advance, SKRect LocalBounds, bool HasLocalBounds)
    {
        public ResolvedFallbackCodepoint(string text, SKPaint paint, float advance)
            : this(text, paint, advance, default, false)
        {
        }
    }

    private readonly record struct PositionedCodepointPlacement(SKPoint Point, float RotationDegrees, float ScaleX, float ScaleOriginX, float InlineOffset = float.NaN);

    private readonly record struct VerticalTextRunPlacement(string Text, PositionedCodepointPlacement Placement, float Advance);

    private readonly record struct TextDecorationLayer(SvgVisualElement PaintSource, SvgTextBase MetricsSource, SvgTextDecoration Decorations);

    private sealed class TextPathPlannerSampleCacheEntry
    {
        public TextPathPlannerSampleCacheEntry(
            int count,
            float firstDistance,
            float lastDistance,
            SvgTextPathLayoutPlanner.PathSample[] samples)
        {
            Count = count;
            FirstDistance = firstDistance;
            LastDistance = lastDistance;
            Samples = samples;
        }

        public int Count { get; }

        public float FirstDistance { get; }

        public float LastDistance { get; }

        public SvgTextPathLayoutPlanner.PathSample[] Samples { get; }
    }

    private readonly struct TextRunAdvanceCacheKey : IEquatable<TextRunAdvanceCacheKey>
    {
        private readonly SvgTextBase _styleSource;
        private readonly string _text;

        public TextRunAdvanceCacheKey(SvgTextBase styleSource, string text)
        {
            _styleSource = styleSource;
            _text = text;
        }

        public bool Equals(TextRunAdvanceCacheKey other)
        {
            return ReferenceEquals(_styleSource, other._styleSource) &&
                   string.Equals(_text, other._text, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is TextRunAdvanceCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (RuntimeHelpers.GetHashCode(_styleSource) * 397) ^ StringComparer.Ordinal.GetHashCode(_text);
            }
        }
    }

    private static SvgTextPathLayoutPlanner.PathSample[] CreateTextPathPlannerSamples(IReadOnlyList<PathSample> pathSamples)
    {
        var plannerSamples = new SvgTextPathLayoutPlanner.PathSample[pathSamples.Count];
        for (var i = 0; i < pathSamples.Count; i++)
        {
            plannerSamples[i] = new SvgTextPathLayoutPlanner.PathSample(
                pathSamples[i].Point,
                pathSamples[i].Distance,
                pathSamples[i].StartsSubpath,
                pathSamples[i].ClosesSubpath);
        }

        return plannerSamples;
    }

    private static SvgTextPathLayoutPlanner.PathSample[] GetTextPathPlannerSamples(IReadOnlyList<PathSample> pathSamples)
    {
        if (pathSamples is not List<PathSample> sampleList)
        {
            return CreateTextPathPlannerSamples(pathSamples);
        }

        var firstDistance = sampleList.Count > 0 ? sampleList[0].Distance : 0f;
        var lastDistance = sampleList.Count > 0 ? sampleList[sampleList.Count - 1].Distance : 0f;
        lock (s_textPathPlannerSampleCacheLock)
        {
            if (s_textPathPlannerSampleCache.TryGetValue(sampleList, out var cached) &&
                cached.Count == sampleList.Count &&
                cached.FirstDistance == firstDistance &&
                cached.LastDistance == lastDistance)
            {
                return cached.Samples;
            }

            var plannerSamples = CreateTextPathPlannerSamples(sampleList);
            s_textPathPlannerSampleCache.Remove(sampleList);
            s_textPathPlannerSampleCache.Add(
                sampleList,
                new TextPathPlannerSampleCacheEntry(
                    sampleList.Count,
                    firstDistance,
                    lastDistance,
                    plannerSamples));
            return plannerSamples;
        }
    }

    private sealed class PictureFilterSource : ISvgSceneFilterSource
    {
        private readonly SKPicture? _sourceGraphic;
        private readonly SKPicture? _fillPaint;
        private readonly SKPicture? _strokePaint;
        private readonly SKPicture? _backgroundImage;

        public PictureFilterSource(SKPicture? sourceGraphic, SKPicture? fillPaint, SKPicture? strokePaint, SKPicture? backgroundImage = null)
        {
            _sourceGraphic = sourceGraphic;
            _fillPaint = fillPaint;
            _strokePaint = strokePaint;
            _backgroundImage = backgroundImage;
        }

        public SKPicture? SourceGraphic(SKRect? clip) => _sourceGraphic;

        public SKPicture? BackgroundImage(SKRect? clip) => _backgroundImage;

        public SKPicture? FillPaint(SKRect? clip) => _fillPaint;

        public SKPicture? StrokePaint(SKRect? clip) => _strokePaint;
    }

    private struct FlattenedTextCodepoint
    {
        public FlattenedTextCodepoint(SvgTextBase styleSource, string codepoint)
        {
            StyleSource = styleSource;
            Codepoint = codepoint;
        }

        public SvgTextBase StyleSource { get; }
        public string Codepoint { get; }
        public float? X { get; set; }
        public float? Y { get; set; }
        public float Dx { get; set; }
        public float Dy { get; set; }
        public float? Rotation { get; set; }
    }

    private sealed class RotationState
    {
        private readonly float[] _values;
        private int _index;
        private float _currentValue;

        public RotationState(float[] values)
        {
            _values = values;
            _index = 0;
            _currentValue = values[0];
        }

        public float[]? Consume(int count)
        {
            if (count <= 0)
            {
                return null;
            }

            var rotations = new float[count];
            for (var i = 0; i < count; i++)
            {
                if (_index < _values.Length)
                {
                    _currentValue = _values[_index];
                }

                rotations[i] = _currentValue;
                _index++;
            }

            return rotations;
        }
    }

    private sealed class AbsolutePositionState
    {
        private readonly float[] _xs;
        private readonly float[] _ys;
        private int _xIndex;
        private int _yIndex;

        public AbsolutePositionState(float[]? xs, float[]? ys)
        {
            _xs = xs ?? Array.Empty<float>();
            _ys = ys ?? Array.Empty<float>();
        }

        public bool HasAnyPositions => _xs.Length > 0 || _ys.Length > 0;

        public float[]? GetRemainingXValues()
        {
            return GetRemainingValues(_xs, _xIndex);
        }

        public float[]? GetRemainingYValues()
        {
            return GetRemainingValues(_ys, _yIndex);
        }

        public void BuildEffectiveAbsolutePositions(int codepointCount, List<float> xs, List<float> ys)
        {
            BuildEffectiveValues(_xs, _xIndex, codepointCount, xs);
            BuildEffectiveValues(_ys, _yIndex, codepointCount, ys);
        }

        public void Consume(int count)
        {
            if (count <= 0)
            {
                return;
            }

            _xIndex = Math.Min(_xs.Length, _xIndex + count);
            _yIndex = Math.Min(_ys.Length, _yIndex + count);
        }

        private static void BuildEffectiveValues(float[] values, int index, int count, List<float> target)
        {
            if (values.Length <= index || count <= 0)
            {
                return;
            }

            var available = Math.Min(count, values.Length - index);
            for (var i = 0; i < available; i++)
            {
                target.Add(values[index + i]);
            }
        }

        private static float[]? GetRemainingValues(float[] values, int index)
        {
            if (values.Length <= index)
            {
                return null;
            }

            var remaining = new float[values.Length - index];
            Array.Copy(values, index, remaining, 0, remaining.Length);
            return remaining;
        }
    }

    internal static bool TryMeasureGeometryBounds(
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out SKRect geometryBounds)
    {
        geometryBounds = EstimateGeometryBounds(svgTextBase, viewport, assetLoader);
        return !geometryBounds.IsEmpty;
    }

    public static bool TryCompile(
        SvgTextBase svgTextBase,
        SKRect viewport,
        SKMatrix parentTotalTransform,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references,
        DrawAttributes ignoreAttributes,
        string? elementAddressKey,
        string? compilationRootKey,
        bool isCompilationRootBoundary,
        Func<SvgElement?, string?>? getElementAddressKey,
        bool mayContainMixBlendModeDeclaration,
        bool mayContainIsolationDeclaration,
        bool mayContainCursorDeclaration,
        bool mayContainEnableBackgroundDeclaration,
        SvgSceneContextPaint? contextPaint,
        out SvgSceneNode? node)
    {
        node = new SvgSceneNode(
            SvgSceneNodeKindExtensions.FromElement(svgTextBase),
            svgTextBase,
            elementAddressKey,
            svgTextBase.GetType().Name,
            compilationRootKey,
            isCompilationRootBoundary)
        {
            CompilationStrategy = SvgSceneCompilationStrategy.DirectRetained,
            IsAntialias = PaintingService.IsAntialias(svgTextBase),
            Transform = TransformsService.ToMatrix(svgTextBase.Transforms)
        };

        node.TotalTransform = parentTotalTransform.PreConcat(node.Transform);
        node.IsRenderable = HasFeatures(svgTextBase, ignoreAttributes) && MaskingService.CanDraw(svgTextBase, ignoreAttributes);
        node.HitTestTargetElement = svgTextBase;
        SvgSceneCompiler.AssignRetainedVisualState(
            node,
            svgTextBase,
            mayContainMixBlendModeDeclaration,
            mayContainIsolationDeclaration,
            mayContainCursorDeclaration,
            mayContainEnableBackgroundDeclaration);
        SvgSceneCompiler.AssignRetainedResourceKeys(node, svgTextBase, getElementAddressKey);
        node.SupportsFillHitTest = SvgScenePaintingService.IsValidFill(svgTextBase);
        node.SupportsStrokeHitTest = SvgScenePaintingService.IsValidStroke(svgTextBase, SKRect.Empty);

        if (TryCompileSimpleScaledTextLengthText(svgTextBase, viewport, ignoreAttributes, assetLoader, getElementAddressKey, contextPaint, out var simpleScaledTextLengthGeometryBounds, out var simpleScaledTextLengthModel))
        {
            node.GeometryBounds = simpleScaledTextLengthGeometryBounds;
            node.Transform = TransformsService.ToMatrix(svgTextBase.Transforms, svgTextBase, simpleScaledTextLengthGeometryBounds, viewport);
            node.TotalTransform = parentTotalTransform.PreConcat(node.Transform);
            node.TransformedBounds = node.TotalTransform.MapRect(simpleScaledTextLengthGeometryBounds);
            AssignTextContentMetrics(node);
            node.LocalModel = simpleScaledTextLengthModel;
            if (node.LocalModel is null)
            {
                node.IsRenderable = false;
            }

            return true;
        }

        if (TryCompileSimpleTextLengthSpacingText(svgTextBase, viewport, ignoreAttributes, assetLoader, getElementAddressKey, contextPaint, out var simpleTextLengthGeometryBounds, out var simpleTextLengthModel))
        {
            node.GeometryBounds = simpleTextLengthGeometryBounds;
            node.Transform = TransformsService.ToMatrix(svgTextBase.Transforms, svgTextBase, simpleTextLengthGeometryBounds, viewport);
            node.TotalTransform = parentTotalTransform.PreConcat(node.Transform);
            node.TransformedBounds = node.TotalTransform.MapRect(simpleTextLengthGeometryBounds);
            AssignTextContentMetrics(node);
            node.LocalModel = simpleTextLengthModel;
            if (node.LocalModel is null)
            {
                node.IsRenderable = false;
            }

            return true;
        }

        if (TryCompileSequentialText(svgTextBase, viewport, ignoreAttributes, assetLoader, getElementAddressKey, contextPaint, out var compiledGeometryBounds, out var sequentialModel))
        {
            node.GeometryBounds = compiledGeometryBounds;
            node.Transform = TransformsService.ToMatrix(svgTextBase.Transforms, svgTextBase, compiledGeometryBounds, viewport);
            node.TotalTransform = parentTotalTransform.PreConcat(node.Transform);
            node.TransformedBounds = node.TotalTransform.MapRect(compiledGeometryBounds);
            AssignTextContentMetrics(node);
            node.LocalModel = sequentialModel;
            if (node.LocalModel is null)
            {
                node.IsRenderable = false;
            }

            return true;
        }

        if (TryCompileDirectTextPathText(svgTextBase, viewport, ignoreAttributes, assetLoader, references, getElementAddressKey, contextPaint, out var textPathGeometryBounds, out var textPathModel))
        {
            node.GeometryBounds = textPathGeometryBounds;
            node.Transform = TransformsService.ToMatrix(svgTextBase.Transforms, svgTextBase, textPathGeometryBounds, viewport);
            node.TotalTransform = parentTotalTransform.PreConcat(node.Transform);
            node.TransformedBounds = node.TotalTransform.MapRect(textPathGeometryBounds);
            AssignTextContentMetrics(node);
            node.LocalModel = textPathModel;
            if (node.LocalModel is null)
            {
                node.IsRenderable = false;
            }

            return true;
        }

        var geometryBounds = EstimateGeometryBounds(svgTextBase, viewport, assetLoader);
        node.GeometryBounds = geometryBounds;
        node.Transform = TransformsService.ToMatrix(svgTextBase.Transforms, svgTextBase, geometryBounds, viewport);
        node.TotalTransform = parentTotalTransform.PreConcat(node.Transform);
        node.TransformedBounds = node.TotalTransform.MapRect(geometryBounds);
        AssignTextContentMetrics(node);

        if (!node.IsRenderable)
        {
            return true;
        }

        var cullRect = CreateTextLocalCullRect(geometryBounds);
        if (cullRect.IsEmpty)
        {
            node.IsRenderable = false;
            return true;
        }

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        DrawText(
            svgTextBase,
            viewport,
            ignoreAttributes | DrawAttributes.ClipPath | DrawAttributes.Mask | DrawAttributes.Opacity,
            canvas,
            assetLoader,
            references,
            geometryBounds,
            getElementAddressKey,
            contextPaint);
        var localModel = recorder.EndRecording();
        node.LocalModel = localModel.Commands is { Count: > 0 } ? localModel : null;

        if (node.LocalModel is null)
        {
            node.IsRenderable = false;
        }

        return true;
    }

    private static bool TryCompileSimpleScaledTextLengthText(
        SvgTextBase svgTextBase,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint,
        out SKRect geometryBounds,
        out SKPicture? localModel)
    {
        geometryBounds = SKRect.Empty;
        localModel = null;

        if (!HasOwnTextLengthAdjustment(svgTextBase) ||
            HasInlineSizeLayout(svgTextBase) ||
            HasSequentialTextRunBarriers(svgTextBase) ||
            GetOwnLengthAdjust(svgTextBase) != SvgTextLengthAdjust.SpacingAndGlyphs ||
            !TryCollectSequentialTextRuns(svgTextBase, requireAnchorContent: false, IsTextReferenceRenderingEnabled(assetLoader), trimLeadingWhitespaceAtStart: true, out var runs) ||
            runs.Count == 0)
        {
            return false;
        }

        if (!TryCompileSimpleScaledTextLengthSequentialText(
                svgTextBase,
                runs,
                viewport,
                ignoreAttributes,
                assetLoader,
                getElementAddressKey,
                contextPaint,
                out geometryBounds,
                out localModel))
        {
            return false;
        }

        return true;
    }

    private static bool TryCompileSimpleTextLengthSpacingText(
        SvgTextBase svgTextBase,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint,
        out SKRect geometryBounds,
        out SKPicture? localModel)
    {
        geometryBounds = SKRect.Empty;
        localModel = null;

        if (!HasOwnTextLengthAdjustment(svgTextBase) ||
            HasInlineSizeLayout(svgTextBase) ||
            HasSequentialTextRunBarriers(svgTextBase) ||
            GetOwnLengthAdjust(svgTextBase) != SvgTextLengthAdjust.Spacing ||
            !TryCollectSequentialTextRuns(svgTextBase, requireAnchorContent: false, IsTextReferenceRenderingEnabled(assetLoader), trimLeadingWhitespaceAtStart: true, out var runs) ||
            runs.Count == 0)
        {
            return false;
        }

        return TryCompileSimpleAlignedTextLengthSpacingSequentialText(
            svgTextBase,
            runs,
            viewport,
            ignoreAttributes,
            assetLoader,
            getElementAddressKey,
            contextPaint,
            out geometryBounds,
            out localModel);
    }

    private static void AssignTextContentMetrics(SvgSceneNode node)
    {
        node.SetLazyTextContentMetrics();
    }

    private static bool TryCompileSequentialText(
        SvgTextBase svgTextBase,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint,
        out SKRect geometryBounds,
        out SKPicture? localModel)
    {
        geometryBounds = SKRect.Empty;
        localModel = null;

        if (HasInlineSizeLayout(svgTextBase) ||
            HasPreparedSequentialTextContainerBarriers(svgTextBase) ||
            !TryCollectSequentialTextRuns(svgTextBase, requireAnchorContent: false, IsTextReferenceRenderingEnabled(assetLoader), trimLeadingWhitespaceAtStart: true, out var runs) ||
            runs.Count == 0)
        {
            return false;
        }

        if (!CanUseSequentialCompileFastPath(runs) ||
            (MayUseSvgFontSequentialTextRuns(runs, assetLoader) &&
             !CanPrepareSequentialTextRuns(runs, viewport, assetLoader)) ||
            !TryResolveSequentialCompileRuns(runs, viewport, assetLoader, out var resolvedRuns))
        {
            return TryCompileAlignedSequentialText(
                svgTextBase,
                runs,
                viewport,
                ignoreAttributes,
                assetLoader,
                getElementAddressKey,
                contextPaint,
                out geometryBounds,
                out localModel);
        }

        var x = svgTextBase.X.Count >= 1
            ? svgTextBase.X[0].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, viewport)
            : 0f;
        var y = svgTextBase.Y.Count >= 1
            ? svgTextBase.Y[0].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, viewport)
            : 0f;
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
        var currentX = x + baselineShift.X;
        var currentY = y + baselineShift.Y;
        ApplyInitialSequentialOffsets(svgTextBase, viewport, ref currentX, ref currentY);

        var textAlign = GetTextAnchorAlign(svgTextBase, viewport);
        var totalAdvance = 0f;
        for (var i = 0; i < resolvedRuns.Count; i++)
        {
            totalAdvance += resolvedRuns[i].Advance;
        }

        var inlineOrigin = GetAlignedStartCoordinate(currentX, totalAdvance, textAlign);
        var runX = inlineOrigin;
        for (var i = 0; i < resolvedRuns.Count; i++)
        {
            UnionBounds(ref geometryBounds, OffsetRect(resolvedRuns[i].RelativeBounds, runX, currentY));
            ApplyInlineAdvance(resolvedRuns[i].StyleSource, ref runX, ref currentY, resolvedRuns[i].Advance);
        }

        var cullRect = CreateTextLocalCullRect(geometryBounds);
        if (cullRect.IsEmpty)
        {
            return true;
        }

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        DrawResolvedSequentialCompileRuns(resolvedRuns, inlineOrigin, currentY, geometryBounds, ignoreAttributes, canvas, assetLoader, getElementAddressKey, contextPaint);
        var recordedModel = recorder.EndRecording();
        localModel = recordedModel.Commands is { Count: > 0 } ? recordedModel : null;
        return true;
    }

    private static bool TryCompileAlignedSequentialText(
        SvgTextBase svgTextBase,
        IReadOnlyList<SequentialTextRun> runs,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint,
        out SKRect geometryBounds,
        out SKPicture? localModel)
    {
        geometryBounds = SKRect.Empty;
        localModel = null;

        if (TryCompileSimpleAlignedSpacingSequentialText(
                svgTextBase,
                runs,
                viewport,
                ignoreAttributes,
                assetLoader,
                getElementAddressKey,
                contextPaint,
                out geometryBounds,
                out localModel))
        {
            return true;
        }

        if (TryCompileSimpleAlignedTextLengthSpacingSequentialText(
                svgTextBase,
                runs,
                viewport,
                ignoreAttributes,
                assetLoader,
                getElementAddressKey,
                contextPaint,
                out geometryBounds,
                out localModel))
        {
            return true;
        }

        if (!TryResolveAlignedSequentialCompileRuns(runs, viewport, assetLoader, out var resolvedRuns, out var totalAdvance))
        {
            return false;
        }

        var x = svgTextBase.X.Count >= 1
            ? svgTextBase.X[0].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, viewport)
            : 0f;
        var y = svgTextBase.Y.Count >= 1
            ? svgTextBase.Y[0].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, viewport)
            : 0f;
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
        var currentX = x + baselineShift.X;
        var currentY = y + baselineShift.Y;
        ApplyInitialSequentialOffsets(svgTextBase, viewport, ref currentX, ref currentY);

        var textAlign = GetTextAnchorAlign(svgTextBase, viewport);
        var inlineOrigin = GetAlignedStartCoordinate(currentX, totalAdvance, textAlign);
        var runX = inlineOrigin;
        var runY = currentY;
        for (var i = 0; i < resolvedRuns.Count; i++)
        {
            var run = resolvedRuns[i];
            OffsetCodepointPlacements(run.Placements, runX, runY);
            var runBounds = MeasureCodepointPlacementBounds(
                run.StyleSource,
                run.Text,
                run.Codepoints,
                run.Placements,
                viewport,
                assetLoader,
                out _);
            UnionBounds(ref geometryBounds, runBounds);
            ApplyInlineAdvance(run.StyleSource, ref runX, ref runY, run.Advance + run.BoundaryAdvance);
        }

        var cullRect = CreateTextLocalCullRect(geometryBounds);
        if (cullRect.IsEmpty)
        {
            return true;
        }

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        DrawResolvedAlignedSequentialCompileRuns(resolvedRuns, geometryBounds, ignoreAttributes, canvas, assetLoader, getElementAddressKey, contextPaint);
        var recordedModel = recorder.EndRecording();
        localModel = recordedModel.Commands is { Count: > 0 } ? recordedModel : null;
        return true;
    }

    private static bool TryCompileSimpleAlignedSpacingSequentialText(
        SvgTextBase svgTextBase,
        IReadOnlyList<SequentialTextRun> runs,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint,
        out SKRect geometryBounds,
        out SKPicture? localModel)
    {
        geometryBounds = SKRect.Empty;
        localModel = null;

        if (runs.Count == 1)
        {
            if (!TryCreateSimpleAlignedSpacingSequentialCompileRun(runs[0], viewport, assetLoader, out var resolvedRun))
            {
                return false;
            }

            var singleX = svgTextBase.X.Count >= 1
                ? svgTextBase.X[0].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, viewport)
                : 0f;
            var singleY = svgTextBase.Y.Count >= 1
                ? svgTextBase.Y[0].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, viewport)
                : 0f;
            var singleBaselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
            var singleCurrentX = singleX + singleBaselineShift.X;
            var singleCurrentY = singleY + singleBaselineShift.Y;
            ApplyInitialSequentialOffsets(svgTextBase, viewport, ref singleCurrentX, ref singleCurrentY);

            var singleTextAlign = GetTextAnchorAlign(svgTextBase, viewport);
            var singleInlineOrigin = GetAlignedStartCoordinate(singleCurrentX, resolvedRun.Advance, singleTextAlign);
            OffsetPositionedTextBlobPoints(resolvedRun.Points, singleInlineOrigin, singleCurrentY);
            geometryBounds = OffsetRect(resolvedRun.RelativeBounds, singleInlineOrigin, singleCurrentY);

            var singleCullRect = CreateTextLocalCullRect(geometryBounds);
            if (singleCullRect.IsEmpty)
            {
                return true;
            }

            var singleRecorder = new SKPictureRecorder();
            var singleCanvas = singleRecorder.BeginRecording(singleCullRect);
            DrawResolvedSimpleAlignedSpacingSequentialCompileRun(resolvedRun, geometryBounds, ignoreAttributes, singleCanvas, assetLoader, getElementAddressKey, contextPaint);
            var singleRecordedModel = singleRecorder.EndRecording();
            localModel = singleRecordedModel.Commands is { Count: > 0 } ? singleRecordedModel : null;
            return true;
        }

        if (!TryResolveSimpleAlignedSpacingSequentialCompileRuns(runs, viewport, assetLoader, out var resolvedRuns, out var totalAdvance))
        {
            return false;
        }

        var x = svgTextBase.X.Count >= 1
            ? svgTextBase.X[0].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, viewport)
            : 0f;
        var y = svgTextBase.Y.Count >= 1
            ? svgTextBase.Y[0].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, viewport)
            : 0f;
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
        var currentX = x + baselineShift.X;
        var currentY = y + baselineShift.Y;
        ApplyInitialSequentialOffsets(svgTextBase, viewport, ref currentX, ref currentY);

        var textAlign = GetTextAnchorAlign(svgTextBase, viewport);
        var inlineOrigin = GetAlignedStartCoordinate(currentX, totalAdvance, textAlign);
        var runX = inlineOrigin;
        var runY = currentY;
        for (var i = 0; i < resolvedRuns.Count; i++)
        {
            var run = resolvedRuns[i];
            OffsetPositionedTextBlobPoints(run.Points, runX, runY);
            UnionBounds(ref geometryBounds, OffsetRect(run.RelativeBounds, runX, runY));
            ApplyInlineAdvance(run.StyleSource, ref runX, ref runY, run.Advance + run.BoundaryAdvance);
        }

        var cullRect = CreateTextLocalCullRect(geometryBounds);
        if (cullRect.IsEmpty)
        {
            return true;
        }

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        DrawResolvedSimpleAlignedSpacingSequentialCompileRuns(resolvedRuns, geometryBounds, ignoreAttributes, canvas, assetLoader, getElementAddressKey, contextPaint);
        var recordedModel = recorder.EndRecording();
        localModel = recordedModel.Commands is { Count: > 0 } ? recordedModel : null;
        return true;
    }

    private static bool TryCompileSimpleAlignedTextLengthSpacingSequentialText(
        SvgTextBase svgTextBase,
        IReadOnlyList<SequentialTextRun> runs,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint,
        out SKRect geometryBounds,
        out SKPicture? localModel)
    {
        geometryBounds = SKRect.Empty;
        localModel = null;

        if (!TryCreateSimpleAlignedTextLengthSpacingSequentialCompileRun(svgTextBase, runs, viewport, assetLoader, out var resolvedRun))
        {
            return false;
        }

        var x = svgTextBase.X.Count >= 1
            ? svgTextBase.X[0].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, viewport)
            : 0f;
        var y = svgTextBase.Y.Count >= 1
            ? svgTextBase.Y[0].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, viewport)
            : 0f;
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
        var currentX = x + baselineShift.X;
        var currentY = y + baselineShift.Y;
        ApplyInitialSequentialOffsets(svgTextBase, viewport, ref currentX, ref currentY);

        var textAlign = GetTextAnchorAlign(svgTextBase, viewport);
        var inlineOrigin = GetAlignedStartCoordinate(currentX, resolvedRun.Advance, textAlign);
        OffsetPositionedTextBlobPoints(resolvedRun.Points, inlineOrigin, currentY);
        geometryBounds = OffsetRect(resolvedRun.RelativeBounds, inlineOrigin, currentY);

        var cullRect = CreateTextLocalCullRect(geometryBounds);
        if (cullRect.IsEmpty)
        {
            return true;
        }

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        DrawResolvedSimpleAlignedSpacingSequentialCompileRun(resolvedRun, geometryBounds, ignoreAttributes, canvas, assetLoader, getElementAddressKey, contextPaint);
        var recordedModel = recorder.EndRecording();
        localModel = recordedModel.Commands is { Count: > 0 } ? recordedModel : null;
        return true;
    }

    private static bool TryCompileSimpleScaledTextLengthSequentialText(
        SvgTextBase svgTextBase,
        IReadOnlyList<SequentialTextRun> runs,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint,
        out SKRect geometryBounds,
        out SKPicture? localModel)
    {
        geometryBounds = SKRect.Empty;
        localModel = null;

        if (!TryCreateSimpleScaledTextLengthSequentialCompileRun(svgTextBase, runs, viewport, assetLoader, out var resolvedRun))
        {
            return false;
        }

        var x = svgTextBase.X.Count >= 1
            ? svgTextBase.X[0].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, viewport)
            : 0f;
        var y = svgTextBase.Y.Count >= 1
            ? svgTextBase.Y[0].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, viewport)
            : 0f;
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
        var currentX = x + baselineShift.X;
        var currentY = y + baselineShift.Y;
        ApplyInitialSequentialOffsets(svgTextBase, viewport, ref currentX, ref currentY);

        var textAlign = GetTextAnchorAlign(svgTextBase, viewport);
        var drawX = GetAlignedStartCoordinate(currentX, resolvedRun.Advance, textAlign);
        geometryBounds = OffsetRect(resolvedRun.RelativeBounds, drawX, currentY);

        var cullRect = CreateTextLocalCullRect(geometryBounds);
        if (cullRect.IsEmpty)
        {
            return true;
        }

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        DrawResolvedSimpleScaledTextLengthSequentialCompileRun(resolvedRun, drawX, currentY, geometryBounds, ignoreAttributes, canvas, assetLoader, getElementAddressKey, contextPaint);
        var recordedModel = recorder.EndRecording();
        localModel = recordedModel.Commands is { Count: > 0 } ? recordedModel : null;
        return true;
    }

    private static bool TryCompileDirectTextPathText(
        SvgTextBase svgTextBase,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint,
        out SKRect geometryBounds,
        out SKPicture? localModel)
    {
        geometryBounds = SKRect.Empty;
        localModel = null;

        if (HasInlineSizeLayout(svgTextBase) ||
            HasPreparedSequentialTextContainerBarriers(svgTextBase))
        {
            return false;
        }

        var drawIgnoreAttributes = ignoreAttributes | DrawAttributes.ClipPath | DrawAttributes.Mask | DrawAttributes.Opacity;
        if (!TryCollectDirectTextPathChildren(svgTextBase, drawIgnoreAttributes, out var textPaths))
        {
            return false;
        }

        var x = svgTextBase.X.Count >= 1
            ? svgTextBase.X[0].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, viewport)
            : 0f;
        var y = svgTextBase.Y.Count >= 1
            ? svgTextBase.Y[0].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, viewport)
            : 0f;
        var currentX = x;
        var currentY = y;
        var useCurrentPositionOffset = true;
        var groups = new List<DirectTextPathRenderGroup>(textPaths.Count);

        for (var i = 0; i < textPaths.Count; i++)
        {
            if (!TryCreateDirectTextPathRenderGroup(
                    textPaths[i],
                    ref currentX,
                    ref currentY,
                    useCurrentPositionOffset,
                    viewport,
                    assetLoader,
                    out var group,
                    out var runBounds))
            {
                return false;
            }

            useCurrentPositionOffset = false;
            groups.Add(group);
            UnionBounds(ref geometryBounds, runBounds);
        }

        var cullRect = CreateTextLocalCullRect(geometryBounds);
        if (cullRect.IsEmpty)
        {
            return true;
        }

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            DrawPositionedTextPathRuns(
                group.Runs,
                viewport,
                group.GeometryBounds,
                drawIgnoreAttributes,
                canvas,
                assetLoader,
                references,
                getElementAddressKey,
                contextPaint);
        }

        var recordedModel = recorder.EndRecording();
        localModel = recordedModel.Commands is { Count: > 0 } ? recordedModel : null;
        return true;
    }

    private static bool TryCollectDirectTextPathChildren(
        SvgTextBase svgTextBase,
        DrawAttributes ignoreAttributes,
        out List<SvgTextPath> textPaths)
    {
        textPaths = new List<SvgTextPath>();
        var contentNodes = GetContentNodeList(svgTextBase);
        if (contentNodes.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < contentNodes.Count; i++)
        {
            switch (contentNodes[i])
            {
                case SvgTextPath { Method: SvgTextPathMethod.Align } svgTextPath
                    when CanRenderTextSubtree(svgTextPath, ignoreAttributes) &&
                         !HasRecursiveTextPathReference(svgTextPath):
                    textPaths.Add(svgTextPath);
                    break;

                case SvgTextPath:
                case SvgElement:
                    return false;

                default:
                    if (!string.IsNullOrWhiteSpace(contentNodes[i].Content))
                    {
                        return false;
                    }

                    break;
            }
        }

        return textPaths.Count > 0;
    }

    private static bool TryCreateDirectTextPathRenderGroup(
        SvgTextPath svgTextPath,
        ref float currentX,
        ref float currentY,
        bool useCurrentPositionOffset,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out DirectTextPathRenderGroup group,
        out SKRect runBounds)
    {
        group = default;
        runBounds = SKRect.Empty;

        if (!TryResolveTextPathGeometry(svgTextPath, viewport, out _, out var skPath, out var geometryBounds, out var pathSamples, out var pathLength, out var isClosedLoop) ||
            !TryCollectTextPathRuns(svgTextPath, viewport, out var runs) ||
            runs.Count == 0)
        {
            return false;
        }

        ResolveTextPathChunkOffsets(svgTextPath, useCurrentPositionOffset, currentX, currentY, viewport, assetLoader, pathSamples, out var horizontalOffset, out var verticalOffset);
        var startOffset = horizontalOffset + ResolveTextPathStartOffset(svgTextPath, skPath, viewport, pathLength);
        var hOffset = ResolveTextPathHorizontalOffset(svgTextPath, startOffset, pathLength, geometryBounds, runs, assetLoader);

        if (!TryCreateTextPathRunPlacements(runs, pathSamples, isClosedLoop, hOffset, verticalOffset, viewport, geometryBounds, assetLoader, out var positionedRuns, out var endOffset, out var endVOffset))
        {
            return false;
        }

        var fallbackResolver = positionedRuns.Count > 0
            ? GetFallbackCodepointResolver(positionedRuns[0].StyleSource)
            : GetFallbackCodepointResolver(svgTextPath);
        for (var i = 0; i < positionedRuns.Count; i++)
        {
            var bounds = GetPositionedTextPathRunBounds(positionedRuns[i], geometryBounds, assetLoader, fallbackResolver);
            UnionBounds(ref runBounds, bounds);
        }

        var textPathCursorDistance = GetTextPathCursorDistance(svgTextPath, pathLength, endOffset);
        AdvanceTextPathPosition(pathSamples, textPathCursorDistance, endVOffset, isClosedLoop, ref currentX, ref currentY);

        group = new DirectTextPathRenderGroup(geometryBounds, positionedRuns);
        return true;
    }

    private static bool HasInlineSizeLayout(SvgTextBase svgTextBase)
    {
        return HasExplicitInlineSizeLayout(svgTextBase) ||
               HasNonNoneCssTextProperty(svgTextBase.ShapeInside);
    }

    private static bool HasExplicitInlineSizeLayout(SvgTextBase svgTextBase)
    {
        return !string.IsNullOrWhiteSpace(svgTextBase.InlineSize) &&
               !svgTextBase.InlineSize.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase);
    }

    private static bool CanUseSequentialCompileFastPath(IReadOnlyList<SequentialTextRun> runs)
    {
        for (var i = 0; i < runs.Count; i++)
        {
            if (HasPerGlyphLayoutAdjustments(runs[i].StyleSource, runs[i].Text) ||
                !IsSimpleAsciiSequentialCompileText(runs[i].Text))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MayUseSvgFontSequentialTextRuns(
        IReadOnlyList<SequentialTextRun> runs,
        ISvgAssetLoader assetLoader)
    {
        for (var i = 0; i < runs.Count; i++)
        {
            if (SvgFontTextRenderer.HasFontEntries(runs[i].StyleSource, assetLoader))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasGenericSequentialCompileFontFamily(SvgTextBase svgTextBase)
    {
        var fontFamily = svgTextBase.FontFamily;
        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            return true;
        }

        var remaining = fontFamily.AsSpan();
        while (remaining.Length > 0)
        {
            var commaIndex = remaining.IndexOf(',');
            var family = commaIndex >= 0 ? remaining.Slice(0, commaIndex) : remaining;
            family = TrimFontFamilyToken(family);
            if (!family.IsEmpty)
            {
                if (!IsGenericSequentialCompileFontFamily(family))
                {
                    return false;
                }
            }

            if (commaIndex < 0)
            {
                break;
            }

            remaining = remaining.Slice(commaIndex + 1);
        }

        return true;
    }

    private static ReadOnlySpan<char> TrimFontFamilyToken(ReadOnlySpan<char> family)
    {
        family = family.Trim();
        while (!family.IsEmpty && (family[0] == '\'' || family[0] == '"'))
        {
            family = family.Slice(1);
        }

        while (!family.IsEmpty && (family[family.Length - 1] == '\'' || family[family.Length - 1] == '"'))
        {
            family = family.Slice(0, family.Length - 1);
        }

        return family.Trim();
    }

    private static bool IsGenericSequentialCompileFontFamily(ReadOnlySpan<char> family)
    {
        return family.Equals("sans-serif".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
               family.Equals("serif".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
               family.Equals("monospace".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
               family.Equals("cursive".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
               family.Equals("fantasy".AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSimpleAsciiSequentialCompileText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch > 0x7F ||
                char.IsControl(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveSequentialCompileRuns(
        IReadOnlyList<SequentialTextRun> runs,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out List<ResolvedSequentialCompileRun> resolvedRuns)
    {
        resolvedRuns = new List<ResolvedSequentialCompileRun>(runs.Count);
        for (var i = 0; i < runs.Count; i++)
        {
            if (!TryResolveSequentialCompileRun(runs[i], geometryBounds, assetLoader, out var resolvedRun))
            {
                resolvedRuns.Clear();
                return false;
            }

            resolvedRuns.Add(resolvedRun);
        }

        return resolvedRuns.Count > 0;
    }

    private static bool TryResolveSequentialCompileRun(
        SequentialTextRun run,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out ResolvedSequentialCompileRun resolvedRun)
    {
        resolvedRun = default;
        var lineStats = MeasureLineStats(run.StyleSource, run.Text, geometryBounds, assetLoader);
        if (!lineStats.UsesResolvedRunTypeface ||
            string.IsNullOrEmpty(lineStats.DrawText))
        {
            return false;
        }

        resolvedRun = new ResolvedSequentialCompileRun(
            run.StyleSource,
            lineStats.DrawText,
            lineStats.Typeface,
            lineStats.Advance,
            lineStats.RelativeBounds);
        return true;
    }

    private static void DrawResolvedSequentialCompileRuns(
        IReadOnlyList<ResolvedSequentialCompileRun> resolvedRuns,
        float startX,
        float startY,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint)
    {
        var currentX = startX;
        var currentY = startY;
        for (var i = 0; i < resolvedRuns.Count; i++)
        {
            var run = resolvedRuns[i];
            using var commandSource = PushTextCommandSource(canvas, run.StyleSource, getElementAddressKey);
            _ = DrawTextPaintOrder(run.StyleSource, includeFill: true, includeStroke: true, includeDecorations: true, phase =>
            {
                switch (phase)
                {
                    case TextPaintPhase.Fill:
                        if (SvgScenePaintingService.IsValidFill(run.StyleSource))
                        {
                            var fillPaint = SvgScenePaintingService.GetFillPaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                            if (fillPaint is not null)
                            {
                                PaintingService.SetPaintText(run.StyleSource, geometryBounds, fillPaint);
                                fillPaint.TextAlign = SKTextAlign.Left;
                                fillPaint.Typeface = run.Typeface;
                                canvas.DrawText(run.DrawText, currentX, currentY, fillPaint);
                            }
                        }

                        break;

                    case TextPaintPhase.Stroke:
                        if (SvgScenePaintingService.IsValidStroke(run.StyleSource, geometryBounds))
                        {
                            var strokePaint = SvgScenePaintingService.GetStrokePaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                            if (strokePaint is not null)
                            {
                                PaintingService.SetPaintText(run.StyleSource, geometryBounds, strokePaint);
                                strokePaint.TextAlign = SKTextAlign.Left;
                                strokePaint.Typeface = run.Typeface;
                                canvas.DrawText(run.DrawText, currentX, currentY, strokePaint);
                            }
                        }

                        break;

                    case TextPaintPhase.Decorations:
                        DrawResolvedTextDecorations(
                            run.StyleSource,
                            run.DrawText,
                            currentX,
                            currentY,
                            geometryBounds,
                            ignoreAttributes,
                            canvas,
                            assetLoader,
                            rotations: null,
                            forceLeftAlign: true,
                            contextPaint);
                        break;
                }

                return 0f;
            });

            ApplyInlineAdvance(run.StyleSource, ref currentX, ref currentY, run.Advance);
        }
    }

    private static bool TryResolveAlignedSequentialCompileRuns(
        IReadOnlyList<SequentialTextRun> runs,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out List<AlignedSequentialCompileRun> resolvedRuns,
        out float totalAdvance)
    {
        resolvedRuns = new List<AlignedSequentialCompileRun>(runs.Count);
        totalAdvance = 0f;

        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (!CanUseAlignedSequentialCompileRun(run, geometryBounds, assetLoader) ||
                !TryCreateAlignedCodepointPlacements(
                    run.StyleSource,
                    run.Text,
                    anchorX: 0f,
                    anchorY: 0f,
                    geometryBounds,
                    SKTextAlign.Left,
                    assetLoader,
                    explicitRotations: null,
                    out var placements,
                    out var runAdvance,
                    out var codepoints,
                    out _))
            {
                resolvedRuns.Clear();
                totalAdvance = 0f;
                return false;
            }

            if (codepoints is null)
            {
                resolvedRuns.Clear();
                totalAdvance = 0f;
                return false;
            }

            var boundaryAdvance = GetSequentialRunBoundaryAdvance(runs, i, geometryBounds);
            resolvedRuns.Add(new AlignedSequentialCompileRun(
                run.StyleSource,
                run.Text,
                placements,
                codepoints,
                runAdvance,
                boundaryAdvance));
            totalAdvance += runAdvance + boundaryAdvance;
        }

        return resolvedRuns.Count > 0 && totalAdvance > 0f;
    }

    private static bool TryResolveSimpleAlignedSpacingSequentialCompileRuns(
        IReadOnlyList<SequentialTextRun> runs,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out List<SimplePositionedSequentialCompileRun> resolvedRuns,
        out float totalAdvance)
    {
        resolvedRuns = new List<SimplePositionedSequentialCompileRun>(runs.Count);
        totalAdvance = 0f;

        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (!TryCreateSimpleAlignedSpacingSequentialCompileRun(run, geometryBounds, assetLoader, out var resolvedRun))
            {
                resolvedRuns.Clear();
                totalAdvance = 0f;
                return false;
            }

            var boundaryAdvance = GetSequentialRunBoundaryAdvance(runs, i, geometryBounds);
            resolvedRun = resolvedRun with { BoundaryAdvance = boundaryAdvance };
            resolvedRuns.Add(resolvedRun);
            totalAdvance += resolvedRun.Advance + boundaryAdvance;
        }

        return resolvedRuns.Count > 0 && totalAdvance > 0f;
    }

    private static bool TryCreateSimpleAlignedTextLengthSpacingSequentialCompileRun(
        SvgTextBase svgTextBase,
        IReadOnlyList<SequentialTextRun> runs,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out SimplePositionedSequentialCompileRun resolvedRun)
    {
        resolvedRun = default;
        if (runs.Count != 1)
        {
            return false;
        }

        var run = runs[0];
        var styleSource = run.StyleSource;
        var text = run.Text;
        if (!CanUseSimpleAlignedTextLengthSpacingSequentialCompileRun(svgTextBase, run, geometryBounds, assetLoader))
        {
            return false;
        }

        if (!TryGetOwnTextLength(svgTextBase, geometryBounds, isVertical: false, out var specifiedLength) ||
            specifiedLength <= TextLengthTolerance)
        {
            return false;
        }

        var codepoints = SplitCodepointsReadOnly(text);
        if (codepoints.Count < 2 || codepoints.Count != text.Length)
        {
            return false;
        }

        var paint = CreateTextMetricsPaint(styleSource, geometryBounds);
        paint.TextAlign = SKTextAlign.Left;
        if (!TryCreateSingleSpanShapingPaint(text, paint, assetLoader, out var shapingPaint) ||
            shapingPaint.Typeface is null ||
            !TryGetRenderedTextLocalBounds(text, shapingPaint, assetLoader, out var localBounds) ||
            localBounds.IsEmpty)
        {
            return false;
        }

        var naturalAdvances = MeasureNaturalCodepointAdvances(styleSource, text, codepoints, geometryBounds, assetLoader);
        if (naturalAdvances.Length != codepoints.Count)
        {
            return false;
        }

        var naturalLength = 0f;
        for (var i = 0; i < naturalAdvances.Length; i++)
        {
            var advance = naturalAdvances[i];
            if (!IsFinitePositionedTextBlobCoordinate(advance) || advance < 0f)
            {
                return false;
            }

            naturalLength += advance;
        }

        if (naturalLength <= TextLengthTolerance ||
            Math.Abs(naturalLength - specifiedLength) <= TextLengthTolerance)
        {
            return false;
        }

        var gapCount = codepoints.Count - 1;
        var extraGapAdvance = (specifiedLength - naturalLength) / gapCount;
        if (!IsFinitePositionedTextBlobCoordinate(extraGapAdvance))
        {
            return false;
        }

        var points = new SKPoint[codepoints.Count];
        var currentX = 0f;
        for (var i = 0; i < codepoints.Count; i++)
        {
            points[i] = new SKPoint(currentX, 0f);
            if (i >= codepoints.Count - 1)
            {
                continue;
            }

            var clusterAdvance = naturalAdvances[i] + extraGapAdvance;
            if (!IsFinitePositionedTextBlobCoordinate(clusterAdvance) || clusterAdvance < 0f)
            {
                return false;
            }

            currentX += clusterAdvance;
        }

        var finalAdvance = currentX + naturalAdvances[naturalAdvances.Length - 1];
        if (!IsFinitePositionedTextBlobCoordinate(finalAdvance) ||
            Math.Abs(finalAdvance - specifiedLength) > TextLengthTolerance)
        {
            return false;
        }

        var insertedSpacing = extraGapAdvance * gapCount;
        var relativeBounds = new SKRect(
            localBounds.Left,
            localBounds.Top,
            localBounds.Right + Math.Max(0f, insertedSpacing),
            localBounds.Bottom);
        if (relativeBounds.IsEmpty)
        {
            return false;
        }

        resolvedRun = new SimplePositionedSequentialCompileRun(
            styleSource,
            text,
            points,
            shapingPaint.Typeface,
            relativeBounds,
            specifiedLength,
            BoundaryAdvance: 0f);
        return true;
    }

    private static bool TryCreateSimpleScaledTextLengthSequentialCompileRun(
        SvgTextBase svgTextBase,
        IReadOnlyList<SequentialTextRun> runs,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out SimpleScaledTextLengthSequentialCompileRun resolvedRun)
    {
        resolvedRun = default;
        if (runs.Count != 1)
        {
            return false;
        }

        var run = runs[0];
        var styleSource = run.StyleSource;
        var text = run.Text;
        if (!CanUseSimpleScaledTextLengthSequentialCompileRun(svgTextBase, run, geometryBounds, assetLoader) ||
            !TryGetOwnTextLength(svgTextBase, geometryBounds, isVertical: false, out var specifiedLength) ||
            specifiedLength <= TextLengthTolerance)
        {
            return false;
        }

        var naturalAdvance = MeasureNaturalTextAdvanceHorizontal(styleSource, text, geometryBounds, assetLoader);
        if (naturalAdvance <= TextLengthTolerance ||
            Math.Abs(naturalAdvance - specifiedLength) <= TextLengthTolerance)
        {
            return false;
        }

        var scaleX = specifiedLength / naturalAdvance;
        if (!IsFinitePositionedTextBlobCoordinate(scaleX) || scaleX <= 0f)
        {
            return false;
        }

        var paint = CreateTextMetricsPaint(styleSource, geometryBounds);
        paint.TextAlign = SKTextAlign.Left;
        if (!TryCreateSingleSpanShapingPaint(text, paint, assetLoader, out var shapingPaint) ||
            shapingPaint.Typeface is null ||
            !TryGetRenderedTextLocalBounds(text, shapingPaint, assetLoader, out var localBounds) ||
            localBounds.IsEmpty)
        {
            return false;
        }

        var relativeBounds = CreateScaledTextLengthRelativeBounds(localBounds, specifiedLength, scaleX);
        if (relativeBounds.IsEmpty)
        {
            return false;
        }

        resolvedRun = new SimpleScaledTextLengthSequentialCompileRun(
            styleSource,
            text,
            shapingPaint.Typeface,
            scaleX,
            relativeBounds,
            specifiedLength);
        return true;
    }

    private static bool CanUseSimpleScaledTextLengthSequentialCompileRun(
        SvgTextBase lengthSource,
        SequentialTextRun run,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var styleSource = run.StyleSource;
        if (!ReferenceEquals(styleSource, lengthSource) ||
            styleSource is SvgAltGlyph ||
            string.IsNullOrEmpty(run.Text) ||
            !IsSimpleAsciiSequentialCompileText(run.Text) ||
            IsVerticalWritingMode(styleSource) ||
            HasMultipleTextPositionValues(styleSource) ||
            HasRotateValues(styleSource) ||
            HasInheritedRotateValues(styleSource) ||
            HasNonBaselineShift(styleSource) ||
            GetOwnLengthAdjust(lengthSource) != SvgTextLengthAdjust.SpacingAndGlyphs ||
            ResolveTextDecorationLayers(styleSource).Count > 0 ||
            HasEffectiveSpacingAdjustments(styleSource, run.Text) ||
            RequiresSyntheticSmallCaps(styleSource, run.Text) ||
            ShouldUseBrowserCompatibleRunTypeface(styleSource, run.Text) ||
            ContainsMixedStrongDirections(run.Text) ||
            HasCustomTextOpenTypePaintProperty(styleSource) ||
            !TryResolveSimpleTextMetricsFontSize(styleSource, geometryBounds, out _) ||
            !HasSupportedAlignedSequentialCompileTextLength(lengthSource))
        {
            return false;
        }

        var paint = CreateTextMetricsPaint(styleSource, geometryBounds);
        return !SvgFontTextRenderer.TryGetLayout(styleSource, run.Text, paint, assetLoader, out _);
    }

    private static SKRect CreateScaledTextLengthRelativeBounds(SKRect localBounds, float specifiedLength, float scaleX)
    {
        var scaledLeft = localBounds.Left * scaleX;
        var scaledRight = localBounds.Right * scaleX;
        return new SKRect(
            Math.Min(scaledLeft, 0f),
            localBounds.Top,
            Math.Max(scaledRight, specifiedLength),
            localBounds.Bottom);
    }

    private static bool CanUseSimpleAlignedTextLengthSpacingSequentialCompileRun(
        SvgTextBase lengthSource,
        SequentialTextRun run,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var styleSource = run.StyleSource;
        if (styleSource is SvgAltGlyph ||
            string.IsNullOrEmpty(run.Text) ||
            !IsSimpleAsciiSequentialCompileText(run.Text) ||
            IsVerticalWritingMode(styleSource) ||
            HasRotateValues(styleSource) ||
            HasInheritedRotateValues(styleSource) ||
            HasNonBaselineShift(styleSource) ||
            (!ReferenceEquals(styleSource, lengthSource) && HasOwnTextLengthAdjustment(styleSource)) ||
            GetOwnLengthAdjust(lengthSource) != SvgTextLengthAdjust.Spacing ||
            ResolveTextDecorationLayers(styleSource).Count > 0 ||
            HasEffectiveSpacingAdjustments(styleSource, run.Text) ||
            RequiresSyntheticSmallCaps(styleSource, run.Text) ||
            ContainsMixedStrongDirections(run.Text) ||
            HasCustomTextOpenTypePaintProperty(styleSource) ||
            !TryResolveSimpleTextMetricsFontSize(styleSource, geometryBounds, out _) ||
            !HasSupportedAlignedSequentialCompileTextLength(lengthSource))
        {
            return false;
        }

        var paint = CreateTextMetricsPaint(styleSource, geometryBounds);
        return !SvgFontTextRenderer.TryGetLayout(styleSource, run.Text, paint, assetLoader, out _);
    }

    private static bool TryCreateSimpleAlignedSpacingSequentialCompileRun(
        SequentialTextRun run,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out SimplePositionedSequentialCompileRun resolvedRun)
    {
        resolvedRun = default;
        var styleSource = run.StyleSource;
        var text = run.Text;
        if (!CanUseSimpleAlignedSpacingSequentialCompileRun(run, geometryBounds, assetLoader))
        {
            return false;
        }

        var codepoints = SplitCodepointsReadOnly(text);
        if (codepoints.Count < 2 || codepoints.Count != text.Length)
        {
            return false;
        }

        var paint = CreateTextMetricsPaint(styleSource, geometryBounds);
        paint.TextAlign = SKTextAlign.Left;
        if (!TryCreateSingleSpanShapingPaint(text, paint, assetLoader, out var shapingPaint) ||
            shapingPaint.Typeface is null ||
            !TryGetRenderedTextLocalBounds(text, shapingPaint, assetLoader, out var localBounds) ||
            localBounds.IsEmpty)
        {
            return false;
        }

        var naturalAdvances = MeasureNaturalCodepointAdvances(styleSource, text, codepoints, geometryBounds, assetLoader);
        if (naturalAdvances.Length != codepoints.Count)
        {
            return false;
        }

        var letterSpacingUnit = styleSource.LetterSpacing;
        var wordSpacingUnit = styleSource.WordSpacing;
        var hasLetterSpacingAdjustment = HasSpacingAdjustment(letterSpacingUnit) && !SuppressesLetterSpacingForRun(codepoints);
        var hasWordSpacingAdjustment = HasSpacingAdjustment(wordSpacingUnit);
        var fixedLetterSpacing = hasLetterSpacingAdjustment
            ? letterSpacingUnit.ToDeviceValue(UnitRenderingType.Horizontal, styleSource, geometryBounds)
            : 0f;
        var fixedWordSpacing = hasWordSpacingAdjustment
            ? wordSpacingUnit.ToDeviceValue(UnitRenderingType.Horizontal, styleSource, geometryBounds)
            : 0f;

        if (fixedLetterSpacing < 0f || fixedWordSpacing < 0f)
        {
            return false;
        }

        var points = new SKPoint[codepoints.Count];
        var currentX = 0f;
        var currentY = 0f;
        var insertedSpacing = 0f;
        for (var i = 0; i < codepoints.Count; i++)
        {
            points[i] = new SKPoint(currentX, currentY);
            if (i >= codepoints.Count - 1)
            {
                continue;
            }

            var clusterAdvance = naturalAdvances[i];
            if (hasLetterSpacingAdjustment && SupportsLetterSpacing(codepoints[i]))
            {
                clusterAdvance += fixedLetterSpacing;
                insertedSpacing += fixedLetterSpacing;
            }

            if (hasWordSpacingAdjustment && IsWhitespaceCodepoint(codepoints[i]))
            {
                clusterAdvance += fixedWordSpacing;
                insertedSpacing += fixedWordSpacing;
            }

            if (!IsFinitePositionedTextBlobCoordinate(clusterAdvance) || clusterAdvance < 0f)
            {
                return false;
            }

            currentX += clusterAdvance;
        }

        var lastAdvance = naturalAdvances[naturalAdvances.Length - 1];
        if (!IsFinitePositionedTextBlobCoordinate(lastAdvance) || lastAdvance < 0f)
        {
            return false;
        }

        var totalAdvance = currentX + lastAdvance;
        if (!IsFinitePositionedTextBlobCoordinate(totalAdvance) || totalAdvance <= 0f)
        {
            return false;
        }

        var relativeBounds = new SKRect(
            localBounds.Left,
            localBounds.Top,
            localBounds.Right + insertedSpacing,
            localBounds.Bottom);
        if (relativeBounds.IsEmpty)
        {
            return false;
        }

        resolvedRun = new SimplePositionedSequentialCompileRun(
            styleSource,
            text,
            points,
            shapingPaint.Typeface,
            relativeBounds,
            totalAdvance,
            BoundaryAdvance: 0f);
        return true;
    }

    private static bool CanUseSimpleAlignedSpacingSequentialCompileRun(
        SequentialTextRun run,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var styleSource = run.StyleSource;
        if (styleSource is SvgAltGlyph ||
            string.IsNullOrEmpty(run.Text) ||
            !IsSimpleAsciiSequentialCompileText(run.Text) ||
            IsVerticalWritingMode(styleSource) ||
            HasRotateValues(styleSource) ||
            HasInheritedRotateValues(styleSource) ||
            HasNonBaselineShift(styleSource) ||
            HasOwnTextLengthAdjustment(styleSource) ||
            ResolveTextDecorationLayers(styleSource).Count > 0 ||
            RequiresSyntheticSmallCaps(styleSource, run.Text) ||
            ContainsMixedStrongDirections(run.Text) ||
            HasCustomTextOpenTypePaintProperty(styleSource) ||
            !TryResolveSimpleTextMetricsFontSize(styleSource, geometryBounds, out _) ||
            !HasSupportedAlignedSequentialCompileSpacing(styleSource, run.Text))
        {
            return false;
        }

        if ((HasSpacingAdjustment(styleSource.LetterSpacing) && styleSource.LetterSpacing.Type == SvgUnitType.Percentage) ||
            (HasSpacingAdjustment(styleSource.WordSpacing) && styleSource.WordSpacing.Type == SvgUnitType.Percentage))
        {
            return false;
        }

        var paint = CreateTextMetricsPaint(styleSource, geometryBounds);
        return !SvgFontTextRenderer.TryGetLayout(styleSource, run.Text, paint, assetLoader, out _);
    }

    private static bool CanUseAlignedSequentialCompileRun(
        SequentialTextRun run,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var styleSource = run.StyleSource;
        if (styleSource is SvgAltGlyph ||
            string.IsNullOrEmpty(run.Text) ||
            !IsSimpleAsciiSequentialCompileText(run.Text) ||
            IsVerticalWritingMode(styleSource) ||
            HasRotateValues(styleSource) ||
            HasInheritedRotateValues(styleSource) ||
            HasNonBaselineShift(styleSource) ||
            ResolveTextDecorationLayers(styleSource).Count > 0 ||
            RequiresSyntheticSmallCaps(styleSource, run.Text) ||
            ContainsMixedStrongDirections(run.Text) ||
            HasCustomTextOpenTypePaintProperty(styleSource) ||
            !TryResolveSimpleTextMetricsFontSize(styleSource, geometryBounds, out _))
        {
            return false;
        }

        var hasTextLengthAdjustment = HasSupportedAlignedSequentialCompileTextLength(styleSource);
        if (!HasSupportedAlignedSequentialCompileSpacing(styleSource, run.Text) &&
            !hasTextLengthAdjustment)
        {
            return false;
        }

        var paint = CreateTextMetricsPaint(styleSource, geometryBounds);
        return !SvgFontTextRenderer.TryGetLayout(styleSource, run.Text, paint, assetLoader, out _);
    }

    private static bool HasSupportedAlignedSequentialCompileSpacing(SvgTextBase styleSource, string text)
    {
        var hasLetterSpacing = HasSpacingAdjustment(styleSource.LetterSpacing);
        var hasWordSpacing = HasSpacingAdjustment(styleSource.WordSpacing);
        if (!hasLetterSpacing && !hasWordSpacing)
        {
            return false;
        }

        if ((hasLetterSpacing && styleSource.LetterSpacing.Type == SvgUnitType.Percentage) ||
            (hasWordSpacing && styleSource.WordSpacing.Type == SvgUnitType.Percentage))
        {
            return false;
        }

        return HasEffectiveSpacingAdjustments(styleSource, text);
    }

    private static bool HasSupportedAlignedSequentialCompileTextLength(SvgTextBase styleSource)
    {
        if (!HasOwnTextLengthAdjustment(styleSource))
        {
            return false;
        }

        return styleSource.TextLength.Type is not SvgUnitType.Percentage
            and not SvgUnitType.Em
            and not SvgUnitType.Ex;
    }

    private static void OffsetCodepointPlacements(
        PositionedCodepointPlacement[] placements,
        float offsetX,
        float offsetY)
    {
        if (placements.Length == 0 ||
            (Math.Abs(offsetX) <= 0.001f && Math.Abs(offsetY) <= 0.001f))
        {
            return;
        }

        for (var i = 0; i < placements.Length; i++)
        {
            var placement = placements[i];
            placements[i] = new PositionedCodepointPlacement(
                new SKPoint(placement.Point.X + offsetX, placement.Point.Y + offsetY),
                placement.RotationDegrees,
                placement.ScaleX,
                placement.ScaleOriginX + offsetX,
                placement.InlineOffset);
        }
    }

    private static void OffsetPositionedTextBlobPoints(
        SKPoint[] points,
        float offsetX,
        float offsetY)
    {
        if (points.Length == 0 ||
            (Math.Abs(offsetX) <= 0.001f && Math.Abs(offsetY) <= 0.001f))
        {
            return;
        }

        for (var i = 0; i < points.Length; i++)
        {
            points[i] = new SKPoint(points[i].X + offsetX, points[i].Y + offsetY);
        }
    }

    private static void DrawResolvedAlignedSequentialCompileRuns(
        IReadOnlyList<AlignedSequentialCompileRun> resolvedRuns,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint)
    {
        for (var i = 0; i < resolvedRuns.Count; i++)
        {
            var run = resolvedRuns[i];
            using var commandSource = PushTextCommandSource(canvas, run.StyleSource, getElementAddressKey);
            _ = DrawTextPaintOrder(run.StyleSource, includeFill: true, includeStroke: true, includeDecorations: false, phase =>
            {
                switch (phase)
                {
                    case TextPaintPhase.Fill:
                        if (SvgScenePaintingService.IsValidFill(run.StyleSource))
                        {
                            var fillPaint = SvgScenePaintingService.GetFillPaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                            if (fillPaint is not null)
                            {
                                if (!TryDrawPositionedTextBlob(run.StyleSource, run.Text, run.Placements, geometryBounds, fillPaint, canvas, assetLoader))
                                {
                                    _ = DrawCodepointPlacements(run.StyleSource, run.Text, run.Placements, geometryBounds, fillPaint, canvas, assetLoader);
                                }
                            }
                        }

                        break;

                    case TextPaintPhase.Stroke:
                        if (SvgScenePaintingService.IsValidStroke(run.StyleSource, geometryBounds))
                        {
                            var strokePaint = SvgScenePaintingService.GetStrokePaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                            if (strokePaint is not null)
                            {
                                if (!TryDrawPositionedTextBlob(run.StyleSource, run.Text, run.Placements, geometryBounds, strokePaint, canvas, assetLoader))
                                {
                                    _ = DrawCodepointPlacements(run.StyleSource, run.Text, run.Placements, geometryBounds, strokePaint, canvas, assetLoader);
                                }
                            }
                        }

                        break;
                }

                return 0f;
            });
        }
    }

    private static void DrawResolvedSimpleAlignedSpacingSequentialCompileRuns(
        IReadOnlyList<SimplePositionedSequentialCompileRun> resolvedRuns,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint)
    {
        for (var i = 0; i < resolvedRuns.Count; i++)
        {
            DrawResolvedSimpleAlignedSpacingSequentialCompileRun(resolvedRuns[i], geometryBounds, ignoreAttributes, canvas, assetLoader, getElementAddressKey, contextPaint);
        }
    }

    private static void DrawResolvedSimpleAlignedSpacingSequentialCompileRun(
        SimplePositionedSequentialCompileRun run,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint)
    {
        using var commandSource = PushTextCommandSource(canvas, run.StyleSource, getElementAddressKey);
        SKTextBlob? textBlob = null;
        _ = DrawTextPaintOrder(run.StyleSource, includeFill: true, includeStroke: true, includeDecorations: false, phase =>
        {
            switch (phase)
            {
                case TextPaintPhase.Fill:
                    if (SvgScenePaintingService.IsValidFill(run.StyleSource))
                    {
                        var fillPaint = SvgScenePaintingService.GetFillPaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                        if (fillPaint is not null)
                        {
                            DrawSimplePositionedTextBlob(run, geometryBounds, fillPaint, canvas, ref textBlob);
                        }
                    }

                    break;

                case TextPaintPhase.Stroke:
                    if (SvgScenePaintingService.IsValidStroke(run.StyleSource, geometryBounds))
                    {
                        var strokePaint = SvgScenePaintingService.GetStrokePaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                        if (strokePaint is not null)
                        {
                            DrawSimplePositionedTextBlob(run, geometryBounds, strokePaint, canvas, ref textBlob);
                        }
                    }

                    break;
            }

            return 0f;
        });
    }

    private static void DrawResolvedSimpleScaledTextLengthSequentialCompileRun(
        SimpleScaledTextLengthSequentialCompileRun run,
        float drawX,
        float drawY,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint)
    {
        using var commandSource = PushTextCommandSource(canvas, run.StyleSource, getElementAddressKey);
        _ = DrawTextPaintOrder(run.StyleSource, includeFill: true, includeStroke: true, includeDecorations: false, phase =>
        {
            switch (phase)
            {
                case TextPaintPhase.Fill:
                    if (SvgScenePaintingService.IsValidFill(run.StyleSource))
                    {
                        var fillPaint = SvgScenePaintingService.GetFillPaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                        if (fillPaint is not null)
                        {
                            DrawSimpleScaledTextLengthRun(run, drawX, drawY, geometryBounds, fillPaint, canvas);
                        }
                    }

                    break;

                case TextPaintPhase.Stroke:
                    if (SvgScenePaintingService.IsValidStroke(run.StyleSource, geometryBounds))
                    {
                        var strokePaint = SvgScenePaintingService.GetStrokePaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                        if (strokePaint is not null)
                        {
                            DrawSimpleScaledTextLengthRun(run, drawX, drawY, geometryBounds, strokePaint, canvas);
                        }
                    }

                    break;
            }

            return 0f;
        });
    }

    private static void DrawSimplePositionedTextBlob(
        SimplePositionedSequentialCompileRun run,
        SKRect geometryBounds,
        SKPaint paint,
        SKCanvas canvas,
        ref SKTextBlob? textBlob)
    {
        PaintingService.SetPaintText(run.StyleSource, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;
        paint.Typeface = run.Typeface;

        if (textBlob is null)
        {
            var font = new SKFont(run.Typeface, paint.TextSize)
            {
                Subpixel = paint.SubpixelText,
                Edging = paint.LcdRenderText ? SKFontEdging.SubpixelAntialias : SKFontEdging.Antialias
            };
            textBlob = SKTextBlob.CreatePositioned(run.Text, font, run.Points);
        }

        canvas.DrawText(textBlob, 0f, 0f, paint);
    }

    private static void DrawSimpleScaledTextLengthRun(
        SimpleScaledTextLengthSequentialCompileRun run,
        float drawX,
        float drawY,
        SKRect geometryBounds,
        SKPaint paint,
        SKCanvas canvas)
    {
        PaintingService.SetPaintText(run.StyleSource, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;
        paint.Typeface = run.Typeface;

        var font = new SKFont(run.Typeface, paint.TextSize, run.ScaleX)
        {
            Subpixel = paint.SubpixelText,
            Edging = paint.LcdRenderText ? SKFontEdging.SubpixelAntialias : SKFontEdging.Antialias
        };
        canvas.DrawText(run.Text, drawX, drawY, SKTextAlign.Left, font, paint);
    }

    private static bool TryDrawPositionedTextBlob(
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement[] placements,
        SKRect geometryBounds,
        SKPaint paint,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader)
    {
        if (string.IsNullOrEmpty(text) ||
            placements.Length == 0 ||
            placements.Length != text.Length ||
            !IsSimpleAsciiSequentialCompileText(text) ||
            RequiresSyntheticSmallCaps(svgTextBase, text))
        {
            return false;
        }

        var scaleX = placements[0].ScaleX;
        var scaleOriginX = placements[0].ScaleOriginX;
        if (!IsFinitePositionedTextBlobCoordinate(scaleX) ||
            scaleX <= 0f ||
            !IsFinitePositionedTextBlobCoordinate(scaleOriginX))
        {
            return false;
        }

        var useFontScale = !NearlyEquals(scaleX, 1f);
        var points = new SKPoint[placements.Length];
        for (var i = 0; i < placements.Length; i++)
        {
            var placement = placements[i];
            if (placement.RotationDegrees != 0f ||
                !NearlyEquals(placement.ScaleX, scaleX) ||
                !NearlyEquals(placement.ScaleOriginX, scaleOriginX) ||
                !IsFinitePositionedTextBlobCoordinate(placement.Point.X) ||
                !IsFinitePositionedTextBlobCoordinate(placement.Point.Y))
            {
                return false;
            }

            points[i] = useFontScale
                ? new SKPoint(scaleOriginX + ((placement.Point.X - scaleOriginX) * scaleX), placement.Point.Y)
                : placement.Point;
        }

        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, text, paint, assetLoader, out _))
        {
            return false;
        }

        if (!TryCreateSingleRunShapingPaint(text, paint, assetLoader, out var blobPaint) ||
            blobPaint.Typeface is null)
        {
            return false;
        }

        var font = new SKFont(blobPaint.Typeface, blobPaint.TextSize, scaleX)
        {
            Subpixel = blobPaint.SubpixelText,
            Edging = blobPaint.LcdRenderText ? SKFontEdging.SubpixelAntialias : SKFontEdging.Antialias
        };

        var textBlob = SKTextBlob.CreatePositioned(text, font, points);
        canvas.DrawText(textBlob, 0f, 0f, blobPaint);
        return true;
    }

    private static bool TryDrawSimpleScaledTextLengthRun(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect geometryBounds,
        SKTextAlign textAlign,
        SKPaint paint,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        float[]? rotations,
        out float advance)
    {
        advance = 0f;
        if (string.IsNullOrEmpty(text) ||
            rotations is { Length: > 0 } ||
            !IsSimpleAsciiSequentialCompileText(text) ||
            IsVerticalWritingMode(svgTextBase) ||
            HasMultipleTextPositionValues(svgTextBase) ||
            HasEffectiveSpacingAdjustments(svgTextBase, text) ||
            GetOwnLengthAdjust(svgTextBase) != SvgTextLengthAdjust.SpacingAndGlyphs ||
            RequiresSyntheticSmallCaps(svgTextBase, text) ||
            ShouldUseBrowserCompatibleRunTypeface(svgTextBase, text) ||
            !TryGetOwnTextLength(svgTextBase, geometryBounds, isVertical: false, out var specifiedLength))
        {
            return false;
        }

        var naturalAdvance = MeasureNaturalTextAdvanceHorizontal(svgTextBase, text, geometryBounds, assetLoader);
        if (naturalAdvance <= TextLengthTolerance ||
            Math.Abs(naturalAdvance - specifiedLength) <= TextLengthTolerance)
        {
            return false;
        }

        var scaleX = specifiedLength / naturalAdvance;
        if (!IsFinitePositionedTextBlobCoordinate(scaleX) || scaleX <= 0f)
        {
            return false;
        }

        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, text, paint, assetLoader, out _))
        {
            return false;
        }

        if (!TryCreateSingleSpanShapingPaint(text, paint, assetLoader, out var runPaint) ||
            runPaint.Typeface is null)
        {
            return false;
        }

        var font = new SKFont(runPaint.Typeface, runPaint.TextSize, scaleX)
        {
            Subpixel = runPaint.SubpixelText,
            Edging = runPaint.LcdRenderText ? SKFontEdging.SubpixelAntialias : SKFontEdging.Antialias
        };
        var drawX = GetAlignedStartCoordinate(anchorX, specifiedLength, textAlign);
        canvas.DrawText(text, drawX, anchorY, SKTextAlign.Left, font, runPaint);
        advance = specifiedLength;
        return true;
    }

    private static bool IsFinitePositionedTextBlobCoordinate(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);

    private static SKRect OffsetRect(SKRect rect, float x, float y)
    {
        return new SKRect(rect.Left + x, rect.Top + y, rect.Right + x, rect.Bottom + y);
    }


    private static SKRect EstimateGeometryBounds(SvgTextBase svgTextBase, SKRect viewport, ISvgAssetLoader assetLoader)
    {
        var x = svgTextBase.X.Count >= 1 ? svgTextBase.X[0].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, viewport) : 0f;
        var y = svgTextBase.Y.Count >= 1 ? svgTextBase.Y[0].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, viewport) : 0f;
        var currentX = x;
        var currentY = y;
        var bounds = SKRect.Empty;
        MeasureTextBase(svgTextBase, ref currentX, ref currentY, viewport, assetLoader, ref bounds, inheritedRotationState: null, inheritedAbsolutePositionState: null, trimLeadingWhitespaceAtStart: true);
        return bounds;
    }

    private static void DrawText(
        SvgTextBase svgTextBase,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references,
        SKRect geometryBounds,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint)
    {
        var xs = new List<float>();
        var ys = new List<float>();
        var dxs = new List<float>();
        var dys = new List<float>();
        GetPositionsX(svgTextBase, viewport, assetLoader, xs);
        GetPositionsY(svgTextBase, viewport, assetLoader, ys);
        GetPositionsDX(svgTextBase, viewport, assetLoader, dxs);
        GetPositionsDY(svgTextBase, viewport, assetLoader, dys);

        var x = xs.Count >= 1 ? xs[0] : 0f;
        var y = ys.Count >= 1 ? ys[0] : 0f;
        var currentX = x;
        var currentY = y;

        DrawTextBase(svgTextBase, ref currentX, ref currentY, viewport, ignoreAttributes, canvas, assetLoader, references, geometryBounds, getElementAddressKey, inheritedRotationState: null, inheritedAbsolutePositionState: null, trimLeadingWhitespaceAtStart: true, contextPaint);
    }

    internal static SKPath? CreateClipPath(SvgTextBase svgTextBase, SKRect viewport, ISvgAssetLoader assetLoader)
    {
        var geometryBounds = EstimateGeometryBounds(svgTextBase, viewport, assetLoader);
        if (geometryBounds.IsEmpty)
        {
            return null;
        }

        var path = new SKPath();

        var xs = new List<float>();
        var ys = new List<float>();
        var dxs = new List<float>();
        var dys = new List<float>();
        GetPositionsX(svgTextBase, viewport, assetLoader, xs);
        GetPositionsY(svgTextBase, viewport, assetLoader, ys);
        GetPositionsDX(svgTextBase, viewport, assetLoader, dxs);
        GetPositionsDY(svgTextBase, viewport, assetLoader, dys);

        var x = xs.Count >= 1 ? xs[0] : 0f;
        var y = ys.Count >= 1 ? ys[0] : 0f;
        var currentX = x;
        var currentY = y;

        if (TryAppendSequentialTextRunsClipPath(svgTextBase, ref currentX, ref currentY, viewport, geometryBounds, assetLoader, path, trimLeadingWhitespaceAtStart: true))
        {
            return path.IsEmpty ? null : path;
        }

        var useInitialPosition = true;
        var trimLeadingWhitespace = true;
        var previousEndedWithSpace = false;
        AppendTextClipPathNodes(
            GetContentNodeList(svgTextBase),
            svgTextBase,
            ref currentX,
            ref currentY,
            ref useInitialPosition,
            ref trimLeadingWhitespace,
            ref previousEndedWithSpace,
            viewport,
            assetLoader,
            geometryBounds,
            path,
            rotationState: ResolveRotationState(svgTextBase, null),
            absolutePositionState: null);
        return path.IsEmpty ? null : path;
    }

    private static void DrawTextBase(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references,
        SKRect rootGeometryBounds,
        Func<SvgElement?, string?>? getElementAddressKey,
        RotationState? inheritedRotationState,
        AbsolutePositionState? inheritedAbsolutePositionState,
        bool trimLeadingWhitespaceAtStart,
        SvgSceneContextPaint? contextPaint)
    {
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
        var localCurrentX = currentX + baselineShift.X;
        var localCurrentY = currentY + baselineShift.Y;
        var rotationState = ResolveRotationState(svgTextBase, inheritedRotationState);
        var absolutePositionState = ResolveAbsolutePositionState(svgTextBase, inheritedAbsolutePositionState, viewport);

        if (TryDrawSharedInlineSizeTextLayout(svgTextBase, ref localCurrentX, ref localCurrentY, viewport, ignoreAttributes, canvas, assetLoader, rootGeometryBounds, getElementAddressKey, trimLeadingWhitespaceAtStart, contextPaint))
        {
            currentX = localCurrentX - baselineShift.X;
            currentY = localCurrentY - baselineShift.Y;
            return;
        }

        if (TryDrawWrappedInlineSizeTextLengthLayout(svgTextBase, ref localCurrentX, ref localCurrentY, viewport, ignoreAttributes, canvas, assetLoader, rootGeometryBounds, getElementAddressKey, trimLeadingWhitespaceAtStart, contextPaint))
        {
            currentX = localCurrentX - baselineShift.X;
            currentY = localCurrentY - baselineShift.Y;
            return;
        }

        if (TryDrawFlattenedTextLengthLayout(svgTextBase, ref localCurrentX, ref localCurrentY, viewport, ignoreAttributes, canvas, assetLoader, rootGeometryBounds, getElementAddressKey, trimLeadingWhitespaceAtStart, contextPaint))
        {
            currentX = localCurrentX - baselineShift.X;
            currentY = localCurrentY - baselineShift.Y;
            return;
        }

        if (inheritedRotationState is null &&
            inheritedAbsolutePositionState is null &&
            TryDrawFlattenedRotatedSvgFontLayout(svgTextBase, ref localCurrentX, ref localCurrentY, viewport, ignoreAttributes, canvas, assetLoader, rootGeometryBounds, getElementAddressKey, trimLeadingWhitespaceAtStart, contextPaint))
        {
            currentX = localCurrentX - baselineShift.X;
            currentY = localCurrentY - baselineShift.Y;
            return;
        }

        if (inheritedRotationState is null &&
            inheritedAbsolutePositionState is null &&
            TryDrawSequentialTextRuns(svgTextBase, ref localCurrentX, ref localCurrentY, viewport, rootGeometryBounds, ignoreAttributes, canvas, assetLoader, getElementAddressKey, trimLeadingWhitespaceAtStart, contextPaint))
        {
            currentX = localCurrentX - baselineShift.X;
            currentY = localCurrentY - baselineShift.Y;
            return;
        }

        var useInitialPosition = true;
        var trimLeadingWhitespace = trimLeadingWhitespaceAtStart;
        var previousEndedWithSpace = false;
        DrawTextNodes(
            GetContentNodeList(svgTextBase),
            svgTextBase,
            ref localCurrentX,
            ref localCurrentY,
            ref useInitialPosition,
            ref trimLeadingWhitespace,
            ref previousEndedWithSpace,
            viewport,
            ignoreAttributes,
            canvas,
            assetLoader,
            references,
            rootGeometryBounds,
            getElementAddressKey,
            rotationState,
            absolutePositionState,
            contextPaint);
        currentX = localCurrentX - baselineShift.X;
        currentY = localCurrentY - baselineShift.Y;
    }

    private static bool TryAppendSequentialTextRunsClipPath(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        SKPath path,
        bool trimLeadingWhitespaceAtStart)
    {
        if (HasPreparedSequentialTextContainerBarriers(svgTextBase))
        {
            return false;
        }

        if (!TryCollectSequentialTextRuns(svgTextBase, requireAnchorContent: false, IsTextReferenceRenderingEnabled(assetLoader), trimLeadingWhitespaceAtStart, out var runs))
        {
            return false;
        }

        PreparedSequentialText? preparedText = null;
        ApplyInitialSequentialOffsets(svgTextBase, viewport, ref currentX, ref currentY);
        var isVertical = IsVerticalWritingMode(svgTextBase);
        var textAlign = GetTextAnchorAlign(svgTextBase, geometryBounds);
        if (textAlign == SKTextAlign.Left)
        {
            var startAlignedX = currentX;
            var startAlignedY = currentY;
            for (var i = 0; i < runs.Count; i++)
            {
                AppendTextStringPathAlignedLeft(runs[i].StyleSource, runs[i].Text, ref startAlignedX, ref startAlignedY, geometryBounds, assetLoader, path);
            }

            currentX = startAlignedX;
            currentY = startAlignedY;
            return true;
        }

        if (!TryPrepareSequentialTextRuns(runs, geometryBounds, assetLoader, out preparedText) ||
            preparedText is null)
        {
            return false;
        }

        var totalAdvance = preparedText.TotalAdvance;
        var inlineOrigin = isVertical
            ? GetVerticalInlineStartCoordinate(svgTextBase, currentY, totalAdvance, textAlign)
            : GetAlignedStartCoordinate(currentX, totalAdvance, textAlign);
        var drawX = isVertical ? currentX : inlineOrigin;
        var drawY = isVertical ? inlineOrigin : currentY;

        for (var i = 0; i < preparedText.Runs.Count; i++)
        {
            var preparedRun = preparedText.Runs[i];
            AppendTextStringPathAlignedLeft(preparedRun.StyleSource, preparedRun.Text, ref drawX, ref drawY, geometryBounds, assetLoader, path);
        }

        currentX = drawX;
        currentY = drawY;

        return true;
    }

    private static SvgTextBase CreateAnchorTextStyleSource(SvgAnchor svgAnchor)
    {
        // Text layout still flows through the surrounding SvgTextBase, but anchor-scoped CSS needs
        // a style source whose inheritance chain runs through the <a> element. Without that, mixed
        // text runs such as `prefix <a>link</a> suffix` measure and draw the anchor glyphs with the
        // parent text container's fill/font state, so :link styling never reaches the linked span.
        var scopedStyleSource = new SvgTextSpan();
        scopedStyleSource._parent = svgAnchor;
        return scopedStyleSource;
    }

    private static IDisposable PushTextCommandSource(
        SKCanvas canvas,
        SvgTextBase svgTextBase,
        Func<SvgElement?, string?>? getElementAddressKey)
    {
        var sourceElement = ResolveTextCommandSourceElement(svgTextBase);
        var addressKey = (getElementAddressKey ?? SvgSceneCompiler.TryGetElementAddressKey)(sourceElement);
        return canvas.PushCommandSource(sourceElement.ID, addressKey, sourceElement.GetType().Name);
    }

    private static SvgElement ResolveTextCommandSourceElement(SvgTextBase svgTextBase)
    {
        return string.IsNullOrWhiteSpace(svgTextBase.ID) && svgTextBase.Parent is SvgAnchor svgAnchor
            ? svgAnchor
            : svgTextBase;
    }

    private static float DrawTextPaintOrder(
        SvgVisualElement paintOrderSource,
        bool includeFill,
        bool includeStroke,
        bool includeDecorations,
        Func<TextPaintPhase, float> drawPhase)
    {
        var advance = 0f;
        for (var i = 0; i < 3; i++)
        {
            var phase = GetTextPaintPhase(paintOrderSource, i);
            if (!ShouldDrawTextPaintPhase(phase, includeFill, includeStroke, includeDecorations))
            {
                continue;
            }

            advance = Math.Max(advance, drawPhase(phase));
        }

        return advance;
    }

    private static TextPaintPhase GetTextPaintPhase(SvgVisualElement paintOrderSource, int index)
    {
        return paintOrderSource.PaintOrder switch
        {
            SvgPaintOrder.FillMarkersStroke => index switch
            {
                0 => TextPaintPhase.Fill,
                1 => TextPaintPhase.Decorations,
                _ => TextPaintPhase.Stroke
            },
            SvgPaintOrder.StrokeFillMarkers => index switch
            {
                0 => TextPaintPhase.Stroke,
                1 => TextPaintPhase.Fill,
                _ => TextPaintPhase.Decorations
            },
            SvgPaintOrder.StrokeMarkersFill => index switch
            {
                0 => TextPaintPhase.Stroke,
                1 => TextPaintPhase.Decorations,
                _ => TextPaintPhase.Fill
            },
            SvgPaintOrder.MarkersFillStroke => index switch
            {
                0 => TextPaintPhase.Decorations,
                1 => TextPaintPhase.Fill,
                _ => TextPaintPhase.Stroke
            },
            SvgPaintOrder.MarkersStrokeFill => index switch
            {
                0 => TextPaintPhase.Decorations,
                1 => TextPaintPhase.Stroke,
                _ => TextPaintPhase.Fill
            },
            _ => index switch
            {
                0 => TextPaintPhase.Fill,
                1 => TextPaintPhase.Stroke,
                _ => TextPaintPhase.Decorations
            }
        };
    }

    private static bool ShouldDrawTextPaintPhase(
        TextPaintPhase phase,
        bool includeFill,
        bool includeStroke,
        bool includeDecorations)
    {
        return phase switch
        {
            TextPaintPhase.Fill => includeFill,
            TextPaintPhase.Stroke => includeStroke,
            TextPaintPhase.Decorations => includeDecorations,
            _ => false
        };
    }

    private static void AppendTextClipPathNodes(
        IEnumerable<ISvgNode> contentNodes,
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        ref bool useInitialPosition,
        ref bool trimLeadingWhitespace,
        ref bool previousEndedWithSpace,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        SKRect rootGeometryBounds,
        SKPath path,
        RotationState? rotationState,
        AbsolutePositionState? absolutePositionState)
    {
        var contentNodeList = ToContentNodeList(contentNodes);
        for (var nodeIndex = 0; nodeIndex < contentNodeList.Count; nodeIndex++)
        {
            var node = contentNodeList[nodeIndex];
            if (useInitialPosition &&
                (node is SvgAnchor || node is SvgTextBase))
            {
                ApplyInitialChildContainerOffsets(svgTextBase, viewport, assetLoader, ref currentX, ref currentY);
            }

            switch (node)
            {
                case SvgAnchor svgAnchor:
                    if (!CanRenderTextSubtree(svgAnchor))
                    {
                        break;
                    }

                    var anchorStyleSource = CreateAnchorTextStyleSource(svgAnchor);
                    AppendTextClipPathNodes(GetContentNodeList(svgAnchor), anchorStyleSource, ref currentX, ref currentY, ref useInitialPosition, ref trimLeadingWhitespace, ref previousEndedWithSpace, viewport, assetLoader, rootGeometryBounds, path, rotationState, absolutePositionState);
                    break;

                case not SvgTextBase:
                    var rawContent = node.Content;
                    if (string.IsNullOrEmpty(node.Content))
                    {
                        break;
                    }

                    var text = PrepareText(
                        svgTextBase,
                        node.Content,
                        trimLeadingWhitespace: trimLeadingWhitespace,
                        trimTrailingWhitespace: IsTerminalContentNode(contentNodeList, nodeIndex));
                    if (previousEndedWithSpace &&
                        CollapsesTextWhitespace(svgTextBase) &&
                        !string.IsNullOrEmpty(text) &&
                        text![0] == ' ')
                    {
                        text = text.TrimStart(' ');
                    }

                    if (string.IsNullOrEmpty(text) &&
                        !string.IsNullOrWhiteSpace(rawContent) &&
                        CollapsesTextWhitespace(svgTextBase) &&
                        !previousEndedWithSpace &&
                        HasRenderableTextContentBefore(contentNodeList, nodeIndex) &&
                        HasRenderableTextContentAfter(contentNodeList, nodeIndex))
                    {
                        text = " ";
                    }

                    if (string.IsNullOrEmpty(text))
                    {
                        break;
                    }

                    var codepointCount = CountCodepoints(text!);
                    var xs = new List<float>();
                    var ys = new List<float>();
                    var dxs = new List<float>();
                    var dys = new List<float>();
                    absolutePositionState?.BuildEffectiveAbsolutePositions(codepointCount, xs, ys);
                    if (absolutePositionState is null)
                    {
                        GetPositionsX(svgTextBase, viewport, assetLoader, xs);
                        GetPositionsY(svgTextBase, viewport, assetLoader, ys);
                    }

                    GetPositionsDX(svgTextBase, viewport, assetLoader, dxs);
                    GetPositionsDY(svgTextBase, viewport, assetLoader, dys);
                    var rotations = ConsumeRotations(rotationState, text!);

                    if (useInitialPosition &&
                        TryCreatePositionedCodepointPoints(svgTextBase, text!, xs, ys, dxs, dys, currentX, currentY, rootGeometryBounds, assetLoader, rotations, out var positionedPoints))
                    {
                        AppendPositionedTextStringPath(svgTextBase, text!, positionedPoints, rootGeometryBounds, assetLoader, path, rotations);
                        MeasurePositionedTextStringBounds(svgTextBase, text!, positionedPoints, rootGeometryBounds, assetLoader, rotations, out var positionedAdvance);
                        MoveToAfterPositionedRun(svgTextBase, positionedPoints[positionedPoints.Length - 1], positionedAdvance, out currentX, out currentY);
                        useInitialPosition = false;
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = text!.EndsWith(" ", StringComparison.Ordinal);
                        absolutePositionState?.Consume(codepointCount);
                        break;
                    }

                    var resetX = useInitialPosition && xs.Count >= 1;
                    var resetY = useInitialPosition && ys.Count >= 1;
                    var x = resetX ? xs[0] : currentX;
                    var y = resetY ? ys[0] : currentY;
                    var dx = useInitialPosition && dxs.Count >= 1 ? dxs[0] : 0f;
                    var dy = useInitialPosition && dys.Count >= 1 ? dys[0] : 0f;
                    currentX = x + dx;
                    currentY = y + dy;
                    ApplyBaselineShiftToResetInitialAxes(svgTextBase, viewport, assetLoader, resetX, resetY, ref currentX, ref currentY);
                    AppendTextStringPath(svgTextBase, text!, currentX, currentY, rootGeometryBounds, assetLoader, path, rotations);
                    MeasureTextStringBounds(svgTextBase, text!, currentX, currentY, rootGeometryBounds, assetLoader, rotations, out var advance);
                    ApplyInlineAdvance(svgTextBase, ref currentX, ref currentY, advance);
                    useInitialPosition = false;
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = text!.EndsWith(" ", StringComparison.Ordinal);
                    absolutePositionState?.Consume(codepointCount);
                    break;

                case SvgAltGlyph svgAltGlyph:
                    if (!CanRenderTextSubtree(svgAltGlyph) ||
                        !IsEmptyAltGlyph(svgAltGlyph))
                    {
                        break;
                    }

                    var altGlyphXs = new List<float>();
                    var altGlyphYs = new List<float>();
                    var altGlyphDxs = new List<float>();
                    var altGlyphDys = new List<float>();
                    if (absolutePositionState is null)
                    {
                        GetPositionsX(svgAltGlyph, viewport, assetLoader, altGlyphXs);
                        GetPositionsY(svgAltGlyph, viewport, assetLoader, altGlyphYs);
                    }

                    GetPositionsDX(svgAltGlyph, viewport, assetLoader, altGlyphDxs);
                    GetPositionsDY(svgAltGlyph, viewport, assetLoader, altGlyphDys);
                    var altGlyphResetX = useInitialPosition && altGlyphXs.Count >= 1;
                    var altGlyphResetY = useInitialPosition && altGlyphYs.Count >= 1;
                    var altGlyphX = altGlyphResetX ? altGlyphXs[0] : currentX;
                    var altGlyphY = altGlyphResetY ? altGlyphYs[0] : currentY;
                    var altGlyphDx = useInitialPosition && altGlyphDxs.Count >= 1 ? altGlyphDxs[0] : 0f;
                    var altGlyphDy = useInitialPosition && altGlyphDys.Count >= 1 ? altGlyphDys[0] : 0f;
                    currentX = altGlyphX + altGlyphDx;
                    currentY = altGlyphY + altGlyphDy;
                    ApplyBaselineShiftToResetInitialAxes(svgAltGlyph, viewport, assetLoader, altGlyphResetX, altGlyphResetY, ref currentX, ref currentY);
                    AppendTextStringPath(svgAltGlyph, string.Empty, currentX, currentY, rootGeometryBounds, assetLoader, path, rotations: null);
                    MeasureTextStringBounds(svgAltGlyph, string.Empty, currentX, currentY, rootGeometryBounds, assetLoader, rotations: null, out var altGlyphAdvance);
                    ApplyInlineAdvance(svgAltGlyph, ref currentX, ref currentY, altGlyphAdvance);
                    useInitialPosition = false;
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = false;
                    break;

                case SvgTextPath svgTextPath:
                    if (!CanRenderTextSubtree(svgTextPath))
                    {
                        break;
                    }

                    var appendedTextPathClip = AppendTextPathClip(svgTextPath, ref currentX, ref currentY, useInitialPosition, viewport, assetLoader, path);
                    useInitialPosition = false;
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = EndsWithCollapsedSpace(svgTextPath);
                    if (appendedTextPathClip == TextPathRenderResult.MissingGeometry &&
                        ShouldAbortFollowingContentAfterFailedTextPath(contentNodeList, nodeIndex))
                    {
                        return;
                    }

                    break;

                case SvgTextRef svgTextRef:
                    {
                        if (ShouldSuppressInlineTextReferenceContent(contentNodeList, nodeIndex))
                        {
                            break;
                        }

                        if (!CanRenderTextSubtree(svgTextRef) ||
                            !IsTextReferenceRenderingEnabled(assetLoader) ||
                            SvgService.HasRecursiveReference(svgTextRef, static e => SvgService.GetEffectiveReferenceUri(e, e.ReferencedElement), new HashSet<Uri>()) ||
                            !TryResolveTextReferenceContent(svgTextRef, out var rawReferencedText))
                        {
                            break;
                        }

                        var referencedClipText = PrepareResolvedContent(svgTextRef, rawReferencedText!, trimLeadingWhitespace, previousEndedWithSpace);
                        if (string.IsNullOrEmpty(referencedClipText))
                        {
                            break;
                        }

                        var referencedCodepointCount = CountCodepoints(referencedClipText!);
                        var referencedXs = new List<float>();
                        var referencedYs = new List<float>();
                        var referencedDxs = new List<float>();
                        var referencedDys = new List<float>();
                        absolutePositionState?.BuildEffectiveAbsolutePositions(referencedCodepointCount, referencedXs, referencedYs);
                        if (absolutePositionState is null)
                        {
                            GetPositionsX(svgTextRef, viewport, assetLoader, referencedXs);
                            GetPositionsY(svgTextRef, viewport, assetLoader, referencedYs);
                        }

                        GetPositionsDX(svgTextRef, viewport, assetLoader, referencedDxs);
                        GetPositionsDY(svgTextRef, viewport, assetLoader, referencedDys);
                        var referencedClipRotations = ConsumeRotations(rotationState, referencedClipText!);

                        if (useInitialPosition &&
                            TryCreatePositionedCodepointPoints(svgTextRef, referencedClipText!, referencedXs, referencedYs, referencedDxs, referencedDys, currentX, currentY, rootGeometryBounds, assetLoader, referencedClipRotations, out var referencedClipPoints))
                        {
                            AppendPositionedTextStringPath(svgTextRef, referencedClipText!, referencedClipPoints, rootGeometryBounds, assetLoader, path, referencedClipRotations);
                            MeasurePositionedTextStringBounds(svgTextRef, referencedClipText!, referencedClipPoints, rootGeometryBounds, assetLoader, referencedClipRotations, out var referencedClipAdvance);
                            MoveToAfterPositionedRun(svgTextRef, referencedClipPoints[referencedClipPoints.Length - 1], referencedClipAdvance, out currentX, out currentY);
                            useInitialPosition = false;
                            trimLeadingWhitespace = false;
                            previousEndedWithSpace = referencedClipText!.EndsWith(" ", StringComparison.Ordinal);
                            absolutePositionState?.Consume(referencedCodepointCount);
                            break;
                        }

                        var referencedClipResetX = useInitialPosition && referencedXs.Count >= 1;
                        var referencedClipResetY = useInitialPosition && referencedYs.Count >= 1;
                        var referencedClipX = referencedClipResetX ? referencedXs[0] : currentX;
                        var referencedClipY = referencedClipResetY ? referencedYs[0] : currentY;
                        var referencedClipDx = useInitialPosition && referencedDxs.Count >= 1 ? referencedDxs[0] : 0f;
                        var referencedClipDy = useInitialPosition && referencedDys.Count >= 1 ? referencedDys[0] : 0f;
                        currentX = referencedClipX + referencedClipDx;
                        currentY = referencedClipY + referencedClipDy;
                        ApplyBaselineShiftToResetInitialAxes(svgTextRef, viewport, assetLoader, referencedClipResetX, referencedClipResetY, ref currentX, ref currentY);
                        AppendTextStringPath(svgTextRef, referencedClipText!, currentX, currentY, rootGeometryBounds, assetLoader, path, referencedClipRotations);
                        MeasureTextStringBounds(svgTextRef, referencedClipText!, currentX, currentY, rootGeometryBounds, assetLoader, referencedClipRotations, out var referencedClipStringAdvance);
                        ApplyInlineAdvance(svgTextRef, ref currentX, ref currentY, referencedClipStringAdvance);
                        useInitialPosition = false;
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = referencedClipText!.EndsWith(" ", StringComparison.Ordinal);
                        absolutePositionState?.Consume(referencedCodepointCount);
                        break;
                    }

                case SvgTextSpan svgTextSpan:
                    if (!CanRenderTextSubtree(svgTextSpan))
                    {
                        break;
                    }

                    var childTrimLeadingWhitespace = trimLeadingWhitespace || previousEndedWithSpace || StartsPositionedTextChunk(svgTextSpan);
                    AppendTextClipPathBase(
                        svgTextSpan,
                        ref currentX,
                        ref currentY,
                        viewport,
                        assetLoader,
                        rootGeometryBounds,
                        path,
                        rotationState,
                        absolutePositionState,
                        childTrimLeadingWhitespace);
                    AdvanceInheritedAbsolutePositionState(absolutePositionState, svgTextSpan, childTrimLeadingWhitespace);
                    AdvanceInheritedRotationState(rotationState, svgTextSpan, childTrimLeadingWhitespace);
                    useInitialPosition = false;
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = EndsWithCollapsedSpace(svgTextSpan);
                    break;
            }
        }
    }

    private static void AppendTextClipPathBase(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        SKRect rootGeometryBounds,
        SKPath path,
        RotationState? inheritedRotationState,
        AbsolutePositionState? inheritedAbsolutePositionState,
        bool trimLeadingWhitespaceAtStart)
    {
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
        var localCurrentX = currentX + baselineShift.X;
        var localCurrentY = currentY + baselineShift.Y;
        var rotationState = ResolveRotationState(svgTextBase, inheritedRotationState);
        var absolutePositionState = ResolveAbsolutePositionState(svgTextBase, inheritedAbsolutePositionState, viewport);

        if (inheritedRotationState is null &&
            inheritedAbsolutePositionState is null &&
            TryAppendSequentialTextRunsClipPath(svgTextBase, ref localCurrentX, ref localCurrentY, viewport, rootGeometryBounds, assetLoader, path, trimLeadingWhitespaceAtStart))
        {
            currentX = localCurrentX - baselineShift.X;
            currentY = localCurrentY - baselineShift.Y;
            return;
        }

        var useInitialPosition = true;
        var trimLeadingWhitespace = trimLeadingWhitespaceAtStart;
        var previousEndedWithSpace = false;
        AppendTextClipPathNodes(
            GetContentNodeList(svgTextBase),
            svgTextBase,
            ref localCurrentX,
            ref localCurrentY,
            ref useInitialPosition,
            ref trimLeadingWhitespace,
            ref previousEndedWithSpace,
            viewport,
            assetLoader,
            rootGeometryBounds,
            path,
            rotationState,
            absolutePositionState);
        currentX = localCurrentX - baselineShift.X;
        currentY = localCurrentY - baselineShift.Y;
    }

    private static void DrawTextNodes(
        IEnumerable<ISvgNode> contentNodes,
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        ref bool useInitialPosition,
        ref bool trimLeadingWhitespace,
        ref bool previousEndedWithSpace,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references,
        SKRect rootGeometryBounds,
        Func<SvgElement?, string?>? getElementAddressKey,
        RotationState? rotationState,
        AbsolutePositionState? absolutePositionState,
        SvgSceneContextPaint? contextPaint)
    {
        var contentNodeList = ToContentNodeList(contentNodes);
        for (var nodeIndex = 0; nodeIndex < contentNodeList.Count; nodeIndex++)
        {
            var node = contentNodeList[nodeIndex];
            if (useInitialPosition &&
                (node is SvgAnchor || node is SvgTextBase))
            {
                ApplyInitialChildContainerOffsets(svgTextBase, viewport, assetLoader, ref currentX, ref currentY);
            }

            switch (node)
            {
                case SvgAnchor svgAnchor:
                    if (!CanRenderTextSubtree(svgAnchor, ignoreAttributes))
                    {
                        break;
                    }

                    var anchorStyleSource = CreateAnchorTextStyleSource(svgAnchor);
                    DrawTextNodes(GetContentNodeList(svgAnchor), anchorStyleSource, ref currentX, ref currentY, ref useInitialPosition, ref trimLeadingWhitespace, ref previousEndedWithSpace, viewport, ignoreAttributes, canvas, assetLoader, references, rootGeometryBounds, getElementAddressKey, rotationState, absolutePositionState, contextPaint);
                    break;

                case not SvgTextBase:
                    var rawContent = node.Content;
                    if (string.IsNullOrEmpty(node.Content))
                    {
                        break;
                    }

                    var text = PrepareText(
                        svgTextBase,
                        node.Content,
                        trimLeadingWhitespace: trimLeadingWhitespace,
                        trimTrailingWhitespace: IsTerminalContentNode(contentNodeList, nodeIndex));
                    if (previousEndedWithSpace &&
                        CollapsesTextWhitespace(svgTextBase) &&
                        !string.IsNullOrEmpty(text) &&
                        text![0] == ' ')
                    {
                        text = text.TrimStart(' ');
                    }

                    if (string.IsNullOrEmpty(text) &&
                        !string.IsNullOrWhiteSpace(rawContent) &&
                        CollapsesTextWhitespace(svgTextBase) &&
                        !previousEndedWithSpace &&
                        HasRenderableTextContentBefore(contentNodeList, nodeIndex) &&
                        HasRenderableTextContentAfter(contentNodeList, nodeIndex))
                    {
                        text = " ";
                    }

                    var isValidFill = SvgScenePaintingService.IsValidFill(svgTextBase);
                    var isValidStroke = SvgScenePaintingService.IsValidStroke(svgTextBase, rootGeometryBounds);

                    if ((!isValidFill && !isValidStroke) || string.IsNullOrEmpty(text))
                    {
                        break;
                    }

                    var codepointCount = CountCodepoints(text!);
                    var xs = new List<float>();
                    var ys = new List<float>();
                    var dxs = new List<float>();
                    var dys = new List<float>();
                    absolutePositionState?.BuildEffectiveAbsolutePositions(codepointCount, xs, ys);
                    if (absolutePositionState is null)
                    {
                        GetPositionsX(svgTextBase, viewport, assetLoader, xs);
                        GetPositionsY(svgTextBase, viewport, assetLoader, ys);
                    }

                    GetPositionsDX(svgTextBase, viewport, assetLoader, dxs);
                    GetPositionsDY(svgTextBase, viewport, assetLoader, dys);
                    var rotations = ConsumeRotations(rotationState, text!);

                    if (useInitialPosition &&
                        TryCreatePositionedCodepointPoints(svgTextBase, text!, xs, ys, dxs, dys, currentX, currentY, rootGeometryBounds, assetLoader, rotations, out var positionedPoints))
                    {
                        using var commandSource = PushTextCommandSource(canvas, svgTextBase, getElementAddressKey);
                        var advance = DrawTextPaintOrder(svgTextBase, includeFill: true, includeStroke: true, includeDecorations: true, phase =>
                        {
                            switch (phase)
                            {
                                case TextPaintPhase.Fill:
                                    if (SvgScenePaintingService.IsValidFill(svgTextBase))
                                    {
                                        var fillPaint = SvgScenePaintingService.GetFillPaint(svgTextBase, rootGeometryBounds, assetLoader, ignoreAttributes, contextPaint);
                                        if (fillPaint is not null)
                                        {
                                            return DrawPositionedTextRuns(svgTextBase, text!, positionedPoints, rootGeometryBounds, fillPaint, canvas, assetLoader, rotations);
                                        }
                                    }

                                    break;

                                case TextPaintPhase.Stroke:
                                    if (SvgScenePaintingService.IsValidStroke(svgTextBase, rootGeometryBounds))
                                    {
                                        var strokePaint = SvgScenePaintingService.GetStrokePaint(svgTextBase, rootGeometryBounds, assetLoader, ignoreAttributes, contextPaint);
                                        if (strokePaint is not null)
                                        {
                                            return DrawPositionedTextRuns(svgTextBase, text!, positionedPoints, rootGeometryBounds, strokePaint, canvas, assetLoader, rotations);
                                        }
                                    }

                                    break;

                                case TextPaintPhase.Decorations:
                                    var decorationLayers = ResolveTextDecorationLayers(svgTextBase);
                                    if (decorationLayers.Count > 0)
                                    {
                                        DrawTextDecorations(
                                            decorationLayers,
                                            svgTextBase,
                                            text!,
                                            CreatePositionedCodepointPlacements(svgTextBase, text!, positionedPoints, rotations),
                                            rootGeometryBounds,
                                            ignoreAttributes,
                                            canvas,
                                            assetLoader,
                                            contextPaint);
                                    }

                                    break;
                            }

                            return 0f;
                        });

                        MoveToAfterPositionedRun(svgTextBase, positionedPoints[positionedPoints.Length - 1], advance, out currentX, out currentY);
                        useInitialPosition = false;
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = text!.EndsWith(" ", StringComparison.Ordinal);
                        absolutePositionState?.Consume(codepointCount);
                        break;
                    }

                    var resetX = useInitialPosition && xs.Count >= 1;
                    var resetY = useInitialPosition && ys.Count >= 1;
                    var x = resetX ? xs[0] : currentX;
                    var y = resetY ? ys[0] : currentY;
                    var dx = useInitialPosition && dxs.Count >= 1 ? dxs[0] : 0f;
                    var dy = useInitialPosition && dys.Count >= 1 ? dys[0] : 0f;
                    currentX = x + dx;
                    currentY = y + dy;
                    ApplyBaselineShiftToResetInitialAxes(svgTextBase, viewport, assetLoader, resetX, resetY, ref currentX, ref currentY);
                    DrawTextString(svgTextBase, text!, ref currentX, ref currentY, rootGeometryBounds, ignoreAttributes, canvas, assetLoader, references, getElementAddressKey, rotations, contextPaint);
                    useInitialPosition = false;
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = text!.EndsWith(" ", StringComparison.Ordinal);
                    absolutePositionState?.Consume(codepointCount);
                    break;

                case SvgAltGlyph svgAltGlyph:
                    if (!CanRenderTextSubtree(svgAltGlyph, ignoreAttributes) ||
                        !IsEmptyAltGlyph(svgAltGlyph))
                    {
                        break;
                    }

                    var altGlyphFill = SvgScenePaintingService.IsValidFill(svgAltGlyph);
                    var altGlyphStroke = SvgScenePaintingService.IsValidStroke(svgAltGlyph, rootGeometryBounds);
                    if (!altGlyphFill && !altGlyphStroke)
                    {
                        break;
                    }

                    var altGlyphXs = new List<float>();
                    var altGlyphYs = new List<float>();
                    var altGlyphDxs = new List<float>();
                    var altGlyphDys = new List<float>();
                    if (absolutePositionState is null)
                    {
                        GetPositionsX(svgAltGlyph, viewport, assetLoader, altGlyphXs);
                        GetPositionsY(svgAltGlyph, viewport, assetLoader, altGlyphYs);
                    }

                    GetPositionsDX(svgAltGlyph, viewport, assetLoader, altGlyphDxs);
                    GetPositionsDY(svgAltGlyph, viewport, assetLoader, altGlyphDys);
                    var altGlyphResetX = useInitialPosition && altGlyphXs.Count >= 1;
                    var altGlyphResetY = useInitialPosition && altGlyphYs.Count >= 1;
                    var altGlyphX = altGlyphResetX ? altGlyphXs[0] : currentX;
                    var altGlyphY = altGlyphResetY ? altGlyphYs[0] : currentY;
                    var altGlyphDx = useInitialPosition && altGlyphDxs.Count >= 1 ? altGlyphDxs[0] : 0f;
                    var altGlyphDy = useInitialPosition && altGlyphDys.Count >= 1 ? altGlyphDys[0] : 0f;
                    currentX = altGlyphX + altGlyphDx;
                    currentY = altGlyphY + altGlyphDy;
                    ApplyBaselineShiftToResetInitialAxes(svgAltGlyph, viewport, assetLoader, altGlyphResetX, altGlyphResetY, ref currentX, ref currentY);
                    DrawTextString(svgAltGlyph, string.Empty, ref currentX, ref currentY, rootGeometryBounds, ignoreAttributes, canvas, assetLoader, references, getElementAddressKey, rotations: null, contextPaint);
                    useInitialPosition = false;
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = false;
                    break;

                case SvgTextPath svgTextPath:
                    if (!CanRenderTextSubtree(svgTextPath, ignoreAttributes))
                    {
                        break;
                    }

                    var drewTextPath = DrawTextPath(svgTextPath, ref currentX, ref currentY, useInitialPosition, viewport, ignoreAttributes, canvas, assetLoader, references, getElementAddressKey, contextPaint);
                    useInitialPosition = false;
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = EndsWithCollapsedSpace(svgTextPath);
                    if (drewTextPath == TextPathRenderResult.MissingGeometry &&
                        ShouldAbortFollowingContentAfterFailedTextPath(contentNodeList, nodeIndex))
                    {
                        return;
                    }

                    break;

                case SvgTextRef svgTextRef:
                    {
                        if (ShouldSuppressInlineTextReferenceContent(contentNodeList, nodeIndex))
                        {
                            break;
                        }

                        if (!CanRenderTextSubtree(svgTextRef, ignoreAttributes) ||
                            !IsTextReferenceRenderingEnabled(assetLoader) ||
                            SvgService.HasRecursiveReference(svgTextRef, static e => SvgService.GetEffectiveReferenceUri(e, e.ReferencedElement), new HashSet<Uri>()) ||
                            !TryResolveTextReferenceContent(svgTextRef, out var rawReferencedText))
                        {
                            break;
                        }

                        var referencedText = PrepareResolvedContent(svgTextRef, rawReferencedText!, trimLeadingWhitespace, previousEndedWithSpace);
                        var referencedFill = SvgScenePaintingService.IsValidFill(svgTextRef);
                        var referencedStroke = SvgScenePaintingService.IsValidStroke(svgTextRef, rootGeometryBounds);
                        if ((!referencedFill && !referencedStroke) || string.IsNullOrEmpty(referencedText))
                        {
                            break;
                        }

                        var referencedCodepointCount = CountCodepoints(referencedText!);
                        var referencedXs = new List<float>();
                        var referencedYs = new List<float>();
                        var referencedDxs = new List<float>();
                        var referencedDys = new List<float>();
                        absolutePositionState?.BuildEffectiveAbsolutePositions(referencedCodepointCount, referencedXs, referencedYs);
                        if (absolutePositionState is null)
                        {
                            GetPositionsX(svgTextRef, viewport, assetLoader, referencedXs);
                            GetPositionsY(svgTextRef, viewport, assetLoader, referencedYs);
                        }

                        GetPositionsDX(svgTextRef, viewport, assetLoader, referencedDxs);
                        GetPositionsDY(svgTextRef, viewport, assetLoader, referencedDys);
                        var referencedRotations = ConsumeRotations(rotationState, referencedText!);

                        if (useInitialPosition &&
                            TryCreatePositionedCodepointPoints(svgTextRef, referencedText!, referencedXs, referencedYs, referencedDxs, referencedDys, currentX, currentY, rootGeometryBounds, assetLoader, referencedRotations, out var referencedPoints))
                        {
                            using var commandSource = PushTextCommandSource(canvas, svgTextRef, getElementAddressKey);
                            var advance = DrawTextPaintOrder(svgTextRef, includeFill: true, includeStroke: true, includeDecorations: true, phase =>
                            {
                                switch (phase)
                                {
                                    case TextPaintPhase.Fill:
                                        if (SvgScenePaintingService.IsValidFill(svgTextRef))
                                        {
                                            var fillPaint = SvgScenePaintingService.GetFillPaint(svgTextRef, rootGeometryBounds, assetLoader, ignoreAttributes, contextPaint);
                                            if (fillPaint is not null)
                                            {
                                                return DrawPositionedTextRuns(svgTextRef, referencedText!, referencedPoints, rootGeometryBounds, fillPaint, canvas, assetLoader, referencedRotations);
                                            }
                                        }

                                        break;

                                    case TextPaintPhase.Stroke:
                                        if (SvgScenePaintingService.IsValidStroke(svgTextRef, rootGeometryBounds))
                                        {
                                            var strokePaint = SvgScenePaintingService.GetStrokePaint(svgTextRef, rootGeometryBounds, assetLoader, ignoreAttributes, contextPaint);
                                            if (strokePaint is not null)
                                            {
                                                return DrawPositionedTextRuns(svgTextRef, referencedText!, referencedPoints, rootGeometryBounds, strokePaint, canvas, assetLoader, referencedRotations);
                                            }
                                        }

                                        break;

                                    case TextPaintPhase.Decorations:
                                        var decorationLayers = ResolveTextDecorationLayers(svgTextRef);
                                        if (decorationLayers.Count > 0)
                                        {
                                            DrawTextDecorations(
                                                decorationLayers,
                                                svgTextRef,
                                                referencedText!,
                                                CreatePositionedCodepointPlacements(svgTextRef, referencedText!, referencedPoints, referencedRotations),
                                                rootGeometryBounds,
                                                ignoreAttributes,
                                                canvas,
                                                assetLoader,
                                                contextPaint);
                                        }

                                        break;
                                }

                                return 0f;
                            });

                            MoveToAfterPositionedRun(svgTextRef, referencedPoints[referencedPoints.Length - 1], advance, out currentX, out currentY);
                            useInitialPosition = false;
                            trimLeadingWhitespace = false;
                            previousEndedWithSpace = referencedText!.EndsWith(" ", StringComparison.Ordinal);
                            absolutePositionState?.Consume(referencedCodepointCount);
                            break;
                        }

                        var referencedResetX = useInitialPosition && referencedXs.Count >= 1;
                        var referencedResetY = useInitialPosition && referencedYs.Count >= 1;
                        var referencedX = referencedResetX ? referencedXs[0] : currentX;
                        var referencedY = referencedResetY ? referencedYs[0] : currentY;
                        var referencedDx = useInitialPosition && referencedDxs.Count >= 1 ? referencedDxs[0] : 0f;
                        var referencedDy = useInitialPosition && referencedDys.Count >= 1 ? referencedDys[0] : 0f;
                        currentX = referencedX + referencedDx;
                        currentY = referencedY + referencedDy;
                        ApplyBaselineShiftToResetInitialAxes(svgTextRef, viewport, assetLoader, referencedResetX, referencedResetY, ref currentX, ref currentY);
                        DrawTextString(svgTextRef, referencedText!, ref currentX, ref currentY, rootGeometryBounds, ignoreAttributes, canvas, assetLoader, references, getElementAddressKey, referencedRotations, contextPaint);
                        useInitialPosition = false;
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = referencedText!.EndsWith(" ", StringComparison.Ordinal);
                        absolutePositionState?.Consume(referencedCodepointCount);
                        break;
                    }

                case SvgTextSpan svgTextSpan:
                    if (!CanRenderTextSubtree(svgTextSpan, ignoreAttributes))
                    {
                        break;
                    }

                    var childTrimLeadingWhitespace = trimLeadingWhitespace || previousEndedWithSpace || StartsPositionedTextChunk(svgTextSpan);
                    DrawTextBase(
                        svgTextSpan,
                        ref currentX,
                        ref currentY,
                        viewport,
                        ignoreAttributes,
                        canvas,
                        assetLoader,
                        references,
                        rootGeometryBounds,
                        getElementAddressKey,
                        rotationState,
                        absolutePositionState,
                        childTrimLeadingWhitespace,
                        contextPaint);
                    AdvanceInheritedAbsolutePositionState(absolutePositionState, svgTextSpan, childTrimLeadingWhitespace);
                    AdvanceInheritedRotationState(rotationState, svgTextSpan, childTrimLeadingWhitespace);
                    useInitialPosition = false;
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = EndsWithCollapsedSpace(svgTextSpan);
                    break;
            }
        }
    }

    private static bool TryDrawWrappedInlineSizeTextLengthLayout(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        SKRect geometryBounds,
        Func<SvgElement?, string?>? getElementAddressKey,
        bool trimLeadingWhitespaceAtStart,
        SvgSceneContextPaint? contextPaint)
    {
        if (!TryCreateWrappedInlineSizeTextLengthLayout(
                svgTextBase,
                currentX,
                currentY,
                viewport,
                geometryBounds,
                assetLoader,
                trimLeadingWhitespaceAtStart,
                out var layout) ||
            layout is null)
        {
            return false;
        }

        for (var lineIndex = 0; lineIndex < layout.Lines.Count; lineIndex++)
        {
            var line = layout.Lines[lineIndex];
            for (var runIndex = 0; runIndex < line.Runs.Length; runIndex++)
            {
                var run = line.Runs[runIndex];
                using var commandSource = PushTextCommandSource(canvas, run.StyleSource, getElementAddressKey);
                _ = DrawTextPaintOrder(run.StyleSource, includeFill: true, includeStroke: true, includeDecorations: true, phase =>
                {
                    switch (phase)
                    {
                        case TextPaintPhase.Fill:
                            if (SvgScenePaintingService.IsValidFill(run.StyleSource))
                            {
                                var fillPaint = SvgScenePaintingService.GetFillPaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                                if (fillPaint is not null)
                                {
                                    if (!TryDrawPositionedTextBlob(run.StyleSource, run.Text, run.Placements, geometryBounds, fillPaint, canvas, assetLoader))
                                    {
                                        _ = DrawCodepointPlacements(run.StyleSource, run.Text, run.Placements, geometryBounds, fillPaint, canvas, assetLoader);
                                    }
                                }
                            }

                            break;

                        case TextPaintPhase.Stroke:
                            if (SvgScenePaintingService.IsValidStroke(run.StyleSource, geometryBounds))
                            {
                                var strokePaint = SvgScenePaintingService.GetStrokePaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                                if (strokePaint is not null)
                                {
                                    if (!TryDrawPositionedTextBlob(run.StyleSource, run.Text, run.Placements, geometryBounds, strokePaint, canvas, assetLoader))
                                    {
                                        _ = DrawCodepointPlacements(run.StyleSource, run.Text, run.Placements, geometryBounds, strokePaint, canvas, assetLoader);
                                    }
                                }
                            }

                            break;

                        case TextPaintPhase.Decorations:
                            DrawTextDecorations(
                                ResolveTextDecorationLayers(run.StyleSource),
                                run.StyleSource,
                                run.Text,
                                run.Placements,
                                geometryBounds,
                                ignoreAttributes,
                                canvas,
                                assetLoader,
                                contextPaint);
                            break;
                    }

                    return 0f;
                });
            }
        }

        currentX = layout.FinalX;
        currentY = layout.FinalY;
        return true;
    }

    private static bool TryDrawFlattenedTextLengthLayout(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        SKRect geometryBounds,
        Func<SvgElement?, string?>? getElementAddressKey,
        bool trimLeadingWhitespaceAtStart,
        SvgSceneContextPaint? contextPaint)
    {
        if (!TryCreateFlattenedTextLengthRuns(svgTextBase, currentX, currentY, viewport, geometryBounds, assetLoader, trimLeadingWhitespaceAtStart, out var runs, out var totalAdvance, out var finalY))
        {
            return false;
        }

        for (var i = 0; i < runs.Length; i++)
        {
            var run = runs[i];
            using var commandSource = PushTextCommandSource(canvas, run.StyleSource, getElementAddressKey);
            _ = DrawTextPaintOrder(run.StyleSource, includeFill: true, includeStroke: true, includeDecorations: true, phase =>
            {
                switch (phase)
                {
                    case TextPaintPhase.Fill:
                        if (SvgScenePaintingService.IsValidFill(run.StyleSource))
                        {
                            var fillPaint = SvgScenePaintingService.GetFillPaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                            if (fillPaint is not null)
                            {
                                if (!TryDrawPositionedTextBlob(run.StyleSource, run.Text, run.Placements, geometryBounds, fillPaint, canvas, assetLoader))
                                {
                                    _ = DrawCodepointPlacements(run.StyleSource, run.Text, run.Placements, geometryBounds, fillPaint, canvas, assetLoader);
                                }
                            }
                        }

                        break;

                    case TextPaintPhase.Stroke:
                        if (SvgScenePaintingService.IsValidStroke(run.StyleSource, geometryBounds))
                        {
                            var strokePaint = SvgScenePaintingService.GetStrokePaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                            if (strokePaint is not null)
                            {
                                if (!TryDrawPositionedTextBlob(run.StyleSource, run.Text, run.Placements, geometryBounds, strokePaint, canvas, assetLoader))
                                {
                                    _ = DrawCodepointPlacements(run.StyleSource, run.Text, run.Placements, geometryBounds, strokePaint, canvas, assetLoader);
                                }
                            }
                        }

                        break;

                    case TextPaintPhase.Decorations:
                        DrawTextDecorations(
                            ResolveTextDecorationLayers(run.StyleSource),
                            run.StyleSource,
                            run.Text,
                            run.Placements,
                            geometryBounds,
                            ignoreAttributes,
                            canvas,
                            assetLoader,
                            contextPaint);
                        break;
                }

                return 0f;
            });
        }

        currentX = ApplyTextAnchor(svgTextBase, currentX, geometryBounds, totalAdvance) + totalAdvance;
        currentY = finalY;
        return true;
    }

    private static bool HasRenderableTextContentBefore(IReadOnlyList<ISvgNode> contentNodes, int index)
    {
        for (var i = index - 1; i >= 0; i--)
        {
            if (contentNodes[i] is SvgTextBase textBase)
            {
                if (CanRenderTextSubtree(textBase) && CountRenderedTextCodepoints(textBase, StartsPositionedTextChunk(textBase)) > 0)
                {
                    return true;
                }

                continue;
            }

            if (!string.IsNullOrWhiteSpace(contentNodes[i].Content))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRenderableTextContentAfter(IReadOnlyList<ISvgNode> contentNodes, int index)
    {
        for (var i = index + 1; i < contentNodes.Count; i++)
        {
            if (contentNodes[i] is SvgTextBase textBase)
            {
                if (CanRenderTextSubtree(textBase) && CountRenderedTextCodepoints(textBase, StartsPositionedTextChunk(textBase)) > 0)
                {
                    return true;
                }

                continue;
            }

            if (!string.IsNullOrWhiteSpace(contentNodes[i].Content))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldSuppressInlineTextReferenceContent(IReadOnlyList<ISvgNode> contentNodes, int index)
    {
        return contentNodes[index] is SvgTextRef svgTextRef &&
               HasInlineTextReferenceFallbackContent(svgTextRef) &&
               HasRenderableTextContentBefore(contentNodes, index) &&
               HasRenderableTextContentAfter(contentNodes, index);
    }

    private static bool HasInlineTextReferenceFallbackContent(SvgTextRef svgTextRef)
    {
        foreach (var node in GetContentNodes(svgTextRef))
        {
            if (node is SvgElement)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(node.Content))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldAbortFollowingContentAfterFailedTextPath(IReadOnlyList<ISvgNode> contentNodes, int index)
    {
        return HasRenderableTextContentAfter(contentNodes, index);
    }

    private static bool HasRenderableTextBaseSibling(IReadOnlyList<ISvgNode> contentNodes, int index, int step)
    {
        for (var i = index + step; i >= 0 && i < contentNodes.Count; i += step)
        {
            if (contentNodes[i] is SvgTextBase textBase)
            {
                return CanRenderTextSubtree(textBase);
            }

            if (!string.IsNullOrWhiteSpace(contentNodes[i].Content))
            {
                return false;
            }
        }

        return false;
    }

    private static void DrawTextString(
        SvgTextBase svgTextBase,
        string text,
        ref float x,
        ref float y,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references,
        Func<SvgElement?, string?>? getElementAddressKey,
        float[]? rotations,
        SvgSceneContextPaint? contextPaint)
    {
        using var commandSource = PushTextCommandSource(canvas, svgTextBase, getElementAddressKey);
        var drawX = x;
        var drawY = y;
        var advance = DrawTextPaintOrder(svgTextBase, includeFill: true, includeStroke: true, includeDecorations: true, phase =>
        {
            switch (phase)
            {
                case TextPaintPhase.Fill:
                    if (SvgScenePaintingService.IsValidFill(svgTextBase))
                    {
                        var fillPaint = SvgScenePaintingService.GetFillPaint(svgTextBase, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                        if (fillPaint is not null)
                        {
                            return DrawTextRuns(svgTextBase, text, drawX, drawY, geometryBounds, fillPaint, canvas, assetLoader, rotations);
                        }
                    }

                    break;

                case TextPaintPhase.Stroke:
                    if (SvgScenePaintingService.IsValidStroke(svgTextBase, geometryBounds))
                    {
                        var strokePaint = SvgScenePaintingService.GetStrokePaint(svgTextBase, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                        if (strokePaint is not null)
                        {
                            return DrawTextRuns(svgTextBase, text, drawX, drawY, geometryBounds, strokePaint, canvas, assetLoader, rotations);
                        }
                    }

                    break;

                case TextPaintPhase.Decorations:
                    DrawResolvedTextDecorations(svgTextBase, text, drawX, drawY, geometryBounds, ignoreAttributes, canvas, assetLoader, rotations, forceLeftAlign: false, contextPaint);
                    break;
            }

            return 0f;
        });
        ApplyInlineAdvance(svgTextBase, ref x, ref y, advance);
    }

    private static void AppendTextStringPath(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        SKPath path,
        float[]? rotations)
    {
        AppendTextRunsPath(svgTextBase, text, anchorX, anchorY, geometryBounds, assetLoader, path, forceLeftAlign: false, rotations);
    }

    private static void AppendTextStringPathAlignedLeft(
        SvgTextBase svgTextBase,
        string text,
        ref float x,
        ref float y,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        SKPath path,
        float[]? rotations = null)
    {
        var advance = AppendTextRunsPath(svgTextBase, text, x, y, geometryBounds, assetLoader, path, forceLeftAlign: true, rotations);
        ApplyInlineAdvance(svgTextBase, ref x, ref y, advance);
    }

    private static float AppendTextRunsPath(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        SKPath targetPath,
        bool forceLeftAlign,
        float[]? rotations)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        var textAlign = forceLeftAlign ? SKTextAlign.Left : paint.TextAlign;

        if (TryGetUniformRunRotation(svgTextBase, text, rotations, out var uniformRotation))
        {
            var rotatedPath = new SKPath();
            var rotatedAdvance = AppendTextRunsPath(svgTextBase, text, anchorX, anchorY, geometryBounds, assetLoader, rotatedPath, forceLeftAlign, Array.Empty<float>());
            RotatePath(rotatedPath, new SKPoint(anchorX, anchorY), uniformRotation);
            AppendPathCommands(targetPath, rotatedPath);
            return rotatedAdvance;
        }

        var isVertical = IsVerticalWritingMode(svgTextBase);
        if (isVertical &&
            TryCreateVerticalTextRunPlacements(svgTextBase, text, anchorX, anchorY, geometryBounds, textAlign, assetLoader, rotations, out var verticalPlacements, out var verticalAdvance))
        {
            _ = AppendVerticalTextRunPlacementsPath(svgTextBase, verticalPlacements, geometryBounds, assetLoader, targetPath);
            return verticalAdvance;
        }

        if ((isVertical || HasPerGlyphLayoutAdjustments(svgTextBase, text)) &&
            TryCreateAlignedCodepointPlacements(
                svgTextBase,
                text,
                anchorX,
                anchorY,
                geometryBounds,
                textAlign,
                assetLoader,
                rotations,
                out var placements,
                out var totalAdvance))
        {
            AppendCodepointPlacementsPath(svgTextBase, text, placements, geometryBounds, assetLoader, targetPath);
            return totalAdvance;
        }

        var currentX = anchorX;
        SvgFontTextRenderer.SvgFontLayout? svgFontLayout = null;
        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, text, paint, assetLoader, out var candidateLayout))
        {
            svgFontLayout = candidateLayout;
        }

        if (!forceLeftAlign)
        {
            var naturalTotalAdvance = 0f;
            if (svgFontLayout is not null)
            {
                naturalTotalAdvance = EnsureWhitespaceAdvance(text, paint, assetLoader, svgFontLayout.Advance);
            }
            else
            {
                var typefaceSpans = assetLoader.FindTypefaces(text, paint);
                if (typefaceSpans.Count > 0)
                {
                    for (var i = 0; i < typefaceSpans.Count; i++)
                    {
                        naturalTotalAdvance += typefaceSpans[i].Advance;
                    }
                }
                else
                {
                    var bounds = new SKRect();
                    naturalTotalAdvance = assetLoader.MeasureText(text, paint, ref bounds);
                }

                naturalTotalAdvance = EnsureWhitespaceAdvance(text, paint, assetLoader, naturalTotalAdvance);
            }

            if (paint.TextAlign == SKTextAlign.Center)
            {
                currentX -= naturalTotalAdvance * 0.5f;
            }
            else if (paint.TextAlign == SKTextAlign.Right)
            {
                currentX -= naturalTotalAdvance;
            }
        }

        paint.TextAlign = SKTextAlign.Left;
        var isRightToLeft = IsRightToLeft(svgTextBase);
        if (svgFontLayout is not null)
        {
            svgFontLayout.AppendPath(targetPath, currentX, anchorY);
            return svgFontLayout.Advance;
        }

        var fallbackText = GetBrowserCompatibleFallbackText(svgTextBase, text, assetLoader);
        var advance = 0f;
        var spans = assetLoader.FindTypefaces(fallbackText, paint);
        if (spans.Count == 0)
        {
            AppendPathCommands(targetPath, assetLoader.GetTextPath(fallbackText, paint, currentX, anchorY));
            var bounds = new SKRect();
            return assetLoader.MeasureText(fallbackText, paint, ref bounds);
        }

        var startIndex = isRightToLeft ? spans.Count - 1 : 0;
        var endIndex = isRightToLeft ? -1 : spans.Count;
        var step = isRightToLeft ? -1 : 1;
        for (var i = startIndex; i != endIndex; i += step)
        {
            var span = spans[i];
            var localPaint = paint.Clone();
            localPaint.Typeface = span.Typeface;
            AppendPathCommands(targetPath, assetLoader.GetTextPath(span.Text, localPaint, currentX, anchorY));
            currentX += span.Advance;
            advance += span.Advance;
        }

        return advance;
    }

    private static void AppendPositionedTextStringPath(
        SvgTextBase svgTextBase,
        string text,
        SKPoint[] points,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        SKPath path,
        float[]? rotations)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

        var pointIndex = 0;
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            var point = points[pointIndex];
            var rotation = GetRotationDegrees(rotations, pointIndex);
            pointIndex++;
            var localPaint = paint.Clone();
            if (SvgFontTextRenderer.TryGetLayout(svgTextBase, codepoint, localPaint, assetLoader, out var svgFontLayout) &&
                svgFontLayout is not null)
            {
                AppendPositionedLayoutPath(path, svgFontLayout, point, rotation);
                continue;
            }

            var fallbackCodepoint = GetBrowserCompatibleFallbackText(svgTextBase, codepoint, assetLoader);
            var typefaceSpans = assetLoader.FindTypefaces(fallbackCodepoint, localPaint);
            if (typefaceSpans.Count > 0)
            {
                localPaint.Typeface = typefaceSpans[0].Typeface;
            }

            AppendPositionedTextPath(path, fallbackCodepoint, point, rotation, localPaint, assetLoader);
        }
    }

    private static float DrawTextRuns(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect geometryBounds,
        SKPaint paint,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        float[]? rotations)
    {
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);

        var textAlign = paint.TextAlign;
        if (TryDrawRotatedShapedGlyphRun(svgTextBase, text, anchorX, anchorY, geometryBounds, textAlign, paint, canvas, assetLoader, rotations, out var rotatedShapedAdvance))
        {
            return rotatedShapedAdvance;
        }

        var isVertical = IsVerticalWritingMode(svgTextBase);
        if (isVertical &&
            TryCreateVerticalTextRunPlacements(svgTextBase, text, anchorX, anchorY, geometryBounds, textAlign, assetLoader, rotations, out var verticalPlacements, out var verticalAdvance))
        {
            _ = DrawVerticalTextRunPlacements(svgTextBase, verticalPlacements, geometryBounds, paint, canvas, assetLoader);
            return verticalAdvance;
        }

        if (TryCreateMixedScriptSpacingRunLayout(svgTextBase, text, geometryBounds, paint, assetLoader, out var mixedLayout) &&
            mixedLayout is not null)
        {
            var mixedStartX = GetAlignedStartX(anchorX, mixedLayout.TotalAdvance, textAlign);
            DrawMixedScriptSpacingRun(svgTextBase, mixedLayout, mixedStartX, anchorY, geometryBounds, paint, canvas, assetLoader);
            return mixedLayout.TotalAdvance;
        }

        if (TryDrawSimpleScaledTextLengthRun(svgTextBase, text, anchorX, anchorY, geometryBounds, textAlign, paint, canvas, assetLoader, rotations, out var scaledTextLengthAdvance))
        {
            return scaledTextLengthAdvance;
        }

        if ((isVertical || HasPerGlyphLayoutAdjustments(svgTextBase, text)) &&
            TryCreateAlignedCodepointPlacements(svgTextBase, text, anchorX, anchorY, geometryBounds, textAlign, assetLoader, rotations, out var placements, out var totalAdvance))
        {
            if (!TryDrawPositionedTextBlob(svgTextBase, text, placements, geometryBounds, paint, canvas, assetLoader))
            {
                _ = DrawCodepointPlacements(svgTextBase, text, placements, geometryBounds, paint, canvas, assetLoader);
            }

            return totalAdvance;
        }

        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, text, paint, assetLoader, out var svgFontLayout) && svgFontLayout is not null)
        {
            var svgAdvance = EnsureWhitespaceAdvance(text, paint, assetLoader, svgFontLayout.Advance);
            var alignedStartX = GetAlignedStartX(anchorX, svgAdvance, textAlign);

            paint.TextAlign = SKTextAlign.Left;
            svgFontLayout.Draw(canvas, paint, alignedStartX, anchorY);
            return svgAdvance;
        }

        if (RequiresSyntheticSmallCaps(svgTextBase, text))
        {
            var smallCapsAdvance = DrawSyntheticSmallCapsRuns(svgTextBase, text, anchorX, anchorY, textAlign, paint, canvas, assetLoader);
            return smallCapsAdvance;
        }

        var fallbackText = GetBrowserCompatibleFallbackText(svgTextBase, text, assetLoader);
        if (TryCreateBrowserCompatibleFullRunPaint(svgTextBase, fallbackText, paint, assetLoader, out var fullRunPaint, out var shapedText))
        {
            fullRunPaint.TextAlign = SKTextAlign.Left;
            if (TryCreateBrowserBidiShapedGlyphRun(svgTextBase, fallbackText, fullRunPaint, assetLoader, out var bidiShapedRun, out var bidiShapedAdvance))
            {
                var shapedRunStartX = GetAlignedStartX(anchorX, bidiShapedAdvance, textAlign);
                DrawShapedGlyphRun(bidiShapedRun, shapedRunStartX, anchorY, fullRunPaint, canvas);
                return bidiShapedAdvance;
            }

            var fullRunMeasureBounds = new SKRect();
            var fullRunAdvance = EnsureWhitespaceAdvance(
                fallbackText,
                fullRunPaint,
                assetLoader,
                assetLoader.MeasureText(shapedText, fullRunPaint, ref fullRunMeasureBounds));

            if (TryCreateBrowserShapedGlyphRun(svgTextBase, fallbackText, fullRunPaint, assetLoader, out var shapedRun, out var shapedAdvance))
            {
                var shapedRunStartX = GetAlignedStartX(anchorX, shapedAdvance, textAlign);
                DrawShapedGlyphRun(shapedRun, shapedRunStartX, anchorY, fullRunPaint, canvas);
                return shapedAdvance;
            }

            var fullRunStartX = GetAlignedStartX(anchorX, fullRunAdvance, textAlign);
            canvas.DrawText(shapedText, fullRunStartX, anchorY, fullRunPaint);
            return fullRunAdvance;
        }

        var usesVisualSpanText = TryGetBrowserCompatibleVisualText(svgTextBase, fallbackText, out var visualText);
        var spanText = usesVisualSpanText
            ? visualText
            : fallbackText;
        var typefaceSpans = assetLoader.FindTypefaces(spanText, paint);
        var naturalTotalAdvance = 0f;
        if (typefaceSpans.Count == 0)
        {
            var scratchBounds = new SKRect();
            naturalTotalAdvance = assetLoader.MeasureText(spanText, paint, ref scratchBounds);
        }
        else
        {
            foreach (var span in typefaceSpans)
            {
                naturalTotalAdvance += span.Advance;
            }
        }

        naturalTotalAdvance = EnsureWhitespaceAdvance(fallbackText, paint, assetLoader, naturalTotalAdvance);

        var currentX = GetAlignedStartX(anchorX, naturalTotalAdvance, textAlign);
        var startX = currentX;

        paint.TextAlign = SKTextAlign.Left;
        if (typefaceSpans.Count == 1)
        {
            var typefaceSpan = typefaceSpans[0];
            paint.Typeface = typefaceSpan.Typeface;
            if (TryCreateBrowserShapedGlyphRun(svgTextBase, spanText, paint, assetLoader, out var shapedRun, out var shapedAdvance))
            {
                DrawShapedGlyphRun(shapedRun, GetAlignedStartX(anchorX, shapedAdvance, textAlign), anchorY, paint, canvas);
                return shapedAdvance;
            }

            var runPaint = paint.Clone();
            canvas.DrawText(typefaceSpan.Text, currentX, anchorY, runPaint);
            return naturalTotalAdvance;
        }

        if (typefaceSpans.Count == 0)
        {
            canvas.DrawText(spanText, currentX, anchorY, paint);
            return naturalTotalAdvance;
        }

        var isRightToLeft = !usesVisualSpanText && IsRightToLeft(svgTextBase);
        var startIndex = isRightToLeft ? typefaceSpans.Count - 1 : 0;
        var endIndex = isRightToLeft ? -1 : typefaceSpans.Count;
        var step = isRightToLeft ? -1 : 1;
        for (var i = startIndex; i != endIndex; i += step)
        {
            var typefaceSpan = typefaceSpans[i];
            paint.Typeface = typefaceSpan.Typeface;
            canvas.DrawText(typefaceSpan.Text, currentX, anchorY, paint);
            currentX += typefaceSpan.Advance;
            if (i + step != endIndex)
            {
                paint = paint.Clone();
            }
        }

        return naturalTotalAdvance;
    }

    private static bool IsRightToLeft(SvgTextBase svgTextBase)
    {
        return SvgTextBidiResolver.IsRightToLeft(svgTextBase);
    }

    private static bool IsVerticalWritingMode(SvgTextBase svgTextBase)
    {
        return PaintingService.IsVerticalWritingMode(svgTextBase);
    }

    private static int GetInlineAdvanceDirection(SvgTextBase svgTextBase)
    {
        return UsesVerticalRightToLeftInlineDirection(svgTextBase) ? -1 : 1;
    }

    private static SKTextAlign GetLogicalStartTextAlign(SvgTextBase svgTextBase)
    {
        return UsesVerticalRightToLeftInlineDirection(svgTextBase)
            ? SKTextAlign.Right
            : SKTextAlign.Left;
    }

    private static bool UsesVerticalRightToLeftInlineDirection(SvgTextBase svgTextBase)
    {
        if (!IsVerticalWritingMode(svgTextBase) || !IsRightToLeft(svgTextBase))
        {
            return false;
        }

        var writingMode = GetInheritedTextAttribute(svgTextBase, "writing-mode")?.Trim();
        return writingMode is not null &&
               (writingMode.Equals("vertical-rl", StringComparison.OrdinalIgnoreCase) ||
                writingMode.Equals("vertical-lr", StringComparison.OrdinalIgnoreCase));
    }

    private static bool UsesCssVerticalWritingMode(SvgTextBase svgTextBase)
    {
        var writingMode = GetInheritedTextAttribute(svgTextBase, "writing-mode")?.Trim();
        return writingMode is not null &&
               (writingMode.Equals("vertical-rl", StringComparison.OrdinalIgnoreCase) ||
                writingMode.Equals("vertical-lr", StringComparison.OrdinalIgnoreCase));
    }

    private static SvgDominantBaseline GetDefaultDominantBaseline(SvgTextBase svgTextBase)
    {
        return UsesCssVerticalWritingMode(svgTextBase) ? SvgDominantBaseline.Central : SvgDominantBaseline.Alphabetic;
    }

    private static void ApplyInlineAdvance(SvgTextBase svgTextBase, ref float currentX, ref float currentY, float advance)
    {
        if (IsVerticalWritingMode(svgTextBase))
        {
            currentY += advance * GetInlineAdvanceDirection(svgTextBase);
        }
        else
        {
            currentX += advance;
        }
    }

    private static void MoveToAfterPositionedRun(SvgTextBase svgTextBase, SKPoint lastPoint, float advance, out float currentX, out float currentY)
    {
        currentX = lastPoint.X;
        currentY = lastPoint.Y;
        ApplyInlineAdvance(svgTextBase, ref currentX, ref currentY, advance);
    }

    private static SKPoint GetBaselineShiftVector(SvgTextBase svgTextBase, SKRect viewport, ISvgAssetLoader assetLoader)
    {
        var baselineShift = GetBaselineOffset(svgTextBase, viewport, assetLoader);
        return IsVerticalWritingMode(svgTextBase)
            ? new SKPoint(-baselineShift, 0f)
            : new SKPoint(0f, baselineShift);
    }

    private static float GetCodepointRotationDegrees(SvgTextBase svgTextBase, string codepoint, float[]? rotations, int index)
    {
        var rotation = GetRotationDegrees(rotations, index);
        if (!IsVerticalWritingMode(svgTextBase))
        {
            return rotation;
        }

        return rotation + GetVerticalGlyphRotationDegrees(svgTextBase, codepoint);
    }

    private static float GetVerticalGlyphRotationDegrees(SvgTextBase svgTextBase, string codepoint)
    {
        var glyphOrientation = GetInheritedTextAttribute(svgTextBase, "glyph-orientation-vertical");
        if (!string.IsNullOrWhiteSpace(glyphOrientation))
        {
            glyphOrientation = glyphOrientation!.Trim();
            if (!glyphOrientation.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                if (glyphOrientation.EndsWith("deg", StringComparison.OrdinalIgnoreCase))
                {
                    glyphOrientation = glyphOrientation.Substring(0, glyphOrientation.Length - 3);
                }

                if (float.TryParse(glyphOrientation, NumberStyles.Float, CultureInfo.InvariantCulture, out var explicitRotation))
                {
                    return IsUprightVerticalCodepoint(codepoint)
                        ? explicitRotation
                        : explicitRotation - 90f;
                }
            }
        }

        return IsUprightVerticalCodepoint(codepoint) ? 0f : -90f;
    }

    private static bool IsUprightVerticalCodepoint(string codepoint)
    {
        if (string.IsNullOrEmpty(codepoint))
        {
            return true;
        }

        var scalar = char.ConvertToUtf32(codepoint, 0);
        return scalar switch
        {
            >= 0x1100 and <= 0x11FF => true, // Hangul Jamo
            >= 0x2E80 and <= 0x2FFF => true, // CJK Radicals / punctuation
            >= 0x3000 and <= 0x30FF => true, // CJK punctuation, Hiragana, Katakana
            >= 0x3100 and <= 0x312F => true, // Bopomofo
            >= 0x3130 and <= 0x318F => true, // Hangul Compatibility Jamo
            >= 0x3190 and <= 0x31EF => true, // Kanbun / phonetic extensions
            >= 0x31F0 and <= 0x31FF => true, // Katakana Phonetic Extensions
            >= 0x3200 and <= 0x4DBF => true, // Enclosed CJK / CJK ext A
            >= 0x4E00 and <= 0xA4CF => true, // CJK unified / Yi
            >= 0xAC00 and <= 0xD7AF => true, // Hangul syllables
            >= 0xF900 and <= 0xFAFF => true, // CJK compatibility ideographs
            >= 0xFE10 and <= 0xFE1F => true, // Vertical forms
            >= 0xFE30 and <= 0xFE6F => true, // CJK compatibility forms / small forms
            >= 0xFF01 and <= 0xFF60 => true, // Fullwidth ASCII variants
            >= 0xFFE0 and <= 0xFFE6 => true, // Fullwidth symbol variants
            _ => false
        };
    }

    private static bool NearlyEquals(float left, float right)
    {
        return Math.Abs(left - right) <= 0.001f;
    }

    private static bool TryCreateVerticalTextRunPlacements(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect geometryBounds,
        SKTextAlign textAlign,
        ISvgAssetLoader assetLoader,
        float[]? explicitRotations,
        out VerticalTextRunPlacement[] placements,
        out float totalAdvance)
    {
        placements = Array.Empty<VerticalTextRunPlacement>();
        totalAdvance = 0f;

        if (!IsVerticalWritingMode(svgTextBase) ||
            string.IsNullOrEmpty(text) ||
            HasEffectiveSpacingAdjustments(svgTextBase, text) ||
            HasOwnTextLengthAdjustment(svgTextBase))
        {
            return false;
        }

        var codepoints = SplitCodepointsReadOnly(text);
        if (codepoints.Count == 0)
        {
            return false;
        }

        var rotations = explicitRotations ?? GetPositionedRotations(svgTextBase, codepoints.Count);
        var segments = new List<(string Text, float Rotation)>();
        var builder = new StringBuilder();
        var currentRotation = 0f;

        void FlushSegment()
        {
            if (builder.Length == 0)
            {
                return;
            }

            segments.Add((builder.ToString(), currentRotation));
            builder.Clear();
        }

        for (var i = 0; i < codepoints.Count; i++)
        {
            var codepoint = codepoints[i];
            var rotation = GetCodepointRotationDegrees(svgTextBase, codepoint, rotations, i);
            var upright = NearlyEquals(rotation, 0f) && IsUprightVerticalCodepoint(codepoint);
            if (upright)
            {
                FlushSegment();
                segments.Add((codepoint, rotation));
                continue;
            }

            if (builder.Length == 0)
            {
                builder.Append(codepoint);
                currentRotation = rotation;
                continue;
            }

            if (NearlyEquals(rotation, currentRotation))
            {
                builder.Append(codepoint);
                continue;
            }

            FlushSegment();
            builder.Append(codepoint);
            currentRotation = rotation;
        }

        FlushSegment();
        if (segments.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < segments.Count; i++)
        {
            totalAdvance += MeasureNaturalTextAdvanceHorizontal(svgTextBase, segments[i].Text, geometryBounds, assetLoader);
        }

        var inlineDirection = GetInlineAdvanceDirection(svgTextBase);
        var currentY = 0f;
        placements = new VerticalTextRunPlacement[segments.Count];
        for (var i = 0; i < segments.Count; i++)
        {
            var segmentAdvance = MeasureNaturalTextAdvanceHorizontal(svgTextBase, segments[i].Text, geometryBounds, assetLoader);
            var tempPlacement = new PositionedCodepointPlacement(new SKPoint(0f, 0f), segments[i].Rotation, 1f, 0f);
            var tempRun = new VerticalTextRunPlacement(segments[i].Text, tempPlacement, segmentAdvance);
            var tempBounds = MeasureVerticalTextRunPlacementsBounds(svgTextBase, new[] { tempRun }, geometryBounds, assetLoader, out _);
            var placementX = -((tempBounds.Left + tempBounds.Right) * 0.5f);
            var placementY = inlineDirection < 0
                ? currentY - tempBounds.Bottom
                : currentY - tempBounds.Top;
            var placement = new PositionedCodepointPlacement(new SKPoint(placementX, placementY), segments[i].Rotation, 1f, placementX);
            placements[i] = new VerticalTextRunPlacement(segments[i].Text, placement, segmentAdvance);
            currentY += segmentAdvance * inlineDirection;
        }

        var measuredBounds = MeasureVerticalTextRunPlacementsBounds(svgTextBase, placements, geometryBounds, assetLoader, out _);
        var alignedTop = GetAlignedStartCoordinate(anchorY, measuredBounds.Height, textAlign);
        var offsetX = anchorX;
        var offsetY = alignedTop - measuredBounds.Top;

        for (var i = 0; i < placements.Length; i++)
        {
            var point = new SKPoint(
                placements[i].Placement.Point.X + offsetX,
                placements[i].Placement.Point.Y + offsetY);
            placements[i] = new VerticalTextRunPlacement(
                placements[i].Text,
                new PositionedCodepointPlacement(point, placements[i].Placement.RotationDegrees, placements[i].Placement.ScaleX, point.X),
                placements[i].Advance);
        }

        return true;
    }

    private static float DrawVerticalTextRunPlacements(
        SvgTextBase svgTextBase,
        VerticalTextRunPlacement[] placements,
        SKRect geometryBounds,
        SKPaint paint,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader)
    {
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

        var totalAdvance = 0f;
        for (var i = 0; i < placements.Length; i++)
        {
            var placement = placements[i];
            totalAdvance += placement.Advance;
            var localPaint = paint.Clone();
            if (SvgFontTextRenderer.TryGetLayout(svgTextBase, placement.Text, localPaint, assetLoader, out var svgFontLayout) &&
                svgFontLayout is not null)
            {
                DrawPositionedLayout(svgFontLayout, placement.Placement, localPaint, canvas);
                continue;
            }

            var fallbackText = GetBrowserCompatibleFallbackText(svgTextBase, placement.Text, assetLoader);
            if (TryCreateBrowserCompatibleFullRunPaint(svgTextBase, fallbackText, localPaint, assetLoader, out var fullRunPaint, out var shapedText))
            {
                DrawPositionedText(shapedText, placement.Placement, fullRunPaint, canvas);
                continue;
            }

            var spans = assetLoader.FindTypefaces(fallbackText, localPaint);
            if (spans.Count == 0)
            {
                DrawPositionedText(fallbackText, placement.Placement, localPaint, canvas);
                continue;
            }

            var localOffsetX = 0f;
            for (var spanIndex = 0; spanIndex < spans.Count; spanIndex++)
            {
                var spanPaint = localPaint.Clone();
                spanPaint.Typeface = spans[spanIndex].Typeface;
                var spanPlacement = new PositionedCodepointPlacement(
                    new SKPoint(placement.Placement.Point.X + localOffsetX, placement.Placement.Point.Y),
                    placement.Placement.RotationDegrees,
                    1f,
                    placement.Placement.Point.X + localOffsetX);
                DrawPositionedText(ApplyBrowserCompatibleBidiControls(svgTextBase, spans[spanIndex].Text), spanPlacement, spanPaint, canvas);
                localOffsetX += spans[spanIndex].Advance;
            }
        }

        return totalAdvance;
    }

    private static float AppendVerticalTextRunPlacementsPath(
        SvgTextBase svgTextBase,
        VerticalTextRunPlacement[] placements,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        SKPath targetPath)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

        var totalAdvance = 0f;
        for (var i = 0; i < placements.Length; i++)
        {
            var placement = placements[i];
            totalAdvance += placement.Advance;
            var localPaint = paint.Clone();
            if (SvgFontTextRenderer.TryGetLayout(svgTextBase, placement.Text, localPaint, assetLoader, out var svgFontLayout) &&
                svgFontLayout is not null)
            {
                AppendPositionedLayoutPath(targetPath, svgFontLayout, placement.Placement);
                continue;
            }

            var fallbackText = GetBrowserCompatibleFallbackText(svgTextBase, placement.Text, assetLoader);
            var spans = assetLoader.FindTypefaces(fallbackText, localPaint);
            if (spans.Count == 0)
            {
                AppendPositionedTextPath(targetPath, fallbackText, placement.Placement, localPaint, assetLoader);
                continue;
            }

            var localOffsetX = 0f;
            for (var spanIndex = 0; spanIndex < spans.Count; spanIndex++)
            {
                var spanPaint = localPaint.Clone();
                spanPaint.Typeface = spans[spanIndex].Typeface;
                var spanPlacement = new PositionedCodepointPlacement(
                    new SKPoint(placement.Placement.Point.X + localOffsetX, placement.Placement.Point.Y),
                    placement.Placement.RotationDegrees,
                    1f,
                    placement.Placement.Point.X + localOffsetX);
                AppendPositionedTextPath(targetPath, ApplyBrowserCompatibleBidiControls(svgTextBase, spans[spanIndex].Text), spanPlacement, spanPaint, assetLoader);
                localOffsetX += spans[spanIndex].Advance;
            }
        }

        return totalAdvance;
    }

    private static SKRect MeasureVerticalTextRunPlacementsBounds(
        SvgTextBase svgTextBase,
        VerticalTextRunPlacement[] placements,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out float advance)
    {
        var path = new SKPath();
        advance = AppendVerticalTextRunPlacementsPath(svgTextBase, placements, geometryBounds, assetLoader, path);
        return path.Bounds;
    }

    private static float DrawPositionedTextRuns(
        SvgTextBase svgTextBase,
        string text,
        SKPoint[] points,
        SKRect geometryBounds,
        SKPaint paint,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        float[]? rotations)
    {
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;
        var placements = CreatePositionedCodepointPlacements(svgTextBase, text, points, rotations);

        var fallbackText = GetBrowserCompatibleFallbackText(svgTextBase, text, assetLoader);
        if (!HasPositionedSvgFontLayouts(svgTextBase, text, paint, assetLoader))
        {
            return DrawPositionedTextRunsFallback(svgTextBase, fallbackText, placements, paint, canvas, assetLoader);
        }

        var advance = 0f;
        var placementIndex = 0;
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            var placement = placements[placementIndex++];
            var localPaint = paint.Clone();
            if (SvgFontTextRenderer.TryGetLayout(svgTextBase, codepoint, localPaint, assetLoader, out var svgFontLayout) &&
                svgFontLayout is not null)
            {
                DrawPositionedLayout(svgFontLayout, placement, localPaint, canvas);
                advance = svgFontLayout.Advance;
                continue;
            }

            var fallbackCodepoint = GetBrowserCompatibleFallbackText(svgTextBase, codepoint, assetLoader);
            var typefaceSpans = assetLoader.FindTypefaces(fallbackCodepoint, localPaint);
            if (typefaceSpans.Count > 0)
            {
                localPaint.Typeface = typefaceSpans[0].Typeface;
                DrawPositionedText(typefaceSpans[0].Text, placement, localPaint, canvas);
                advance = typefaceSpans[0].Advance;
                continue;
            }

            DrawPositionedText(fallbackCodepoint, placement, localPaint, canvas);
            var fallbackBounds = new SKRect();
            advance = assetLoader.MeasureText(fallbackCodepoint, localPaint, ref fallbackBounds);
        }

        return advance;
    }

    private static bool HasPositionedSvgFontLayouts(
        SvgTextBase svgTextBase,
        string text,
        SKPaint paint,
        ISvgAssetLoader assetLoader)
    {
        if (!SvgFontTextRenderer.HasFontEntries(svgTextBase, assetLoader))
        {
            return false;
        }

        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            var localPaint = paint.Clone();
            if (SvgFontTextRenderer.TryGetLayout(svgTextBase, codepoint, localPaint, assetLoader, out var svgFontLayout) &&
                svgFontLayout is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static float DrawPositionedTextRunsFallback(
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement[] placements,
        SKPaint paint,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader)
    {
        var advance = 0f;
        var placementIndex = 0;
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            var placement = placements[placementIndex++];
            var resolved = ResolveFallbackCodepoint(svgTextBase, codepoint, paint, assetLoader);
            DrawPositionedText(resolved.Text, placement, resolved.Paint, canvas);
            advance = resolved.Advance;
        }

        return advance;
    }

    private static bool TryDrawRotatedShapedGlyphRun(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect geometryBounds,
        SKTextAlign textAlign,
        SKPaint paint,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        float[]? rotations,
        out float advance)
    {
        advance = 0f;
        if (!TryGetUniformRunRotation(svgTextBase, text, rotations, out var rotation) ||
            !TryCreateDirectedShapedGlyphRun(svgTextBase, text, paint, assetLoader, out var shapingPaint, out var shapedRun, out advance))
        {
            return false;
        }

        var startX = GetAlignedStartX(anchorX, advance, textAlign);
        for (var clusterStart = 0; clusterStart < shapedRun.Glyphs.Length;)
        {
            var cluster = shapedRun.Clusters[clusterStart];
            var clusterEnd = clusterStart + 1;
            while (clusterEnd < shapedRun.Clusters.Length && shapedRun.Clusters[clusterEnd] == cluster)
            {
                clusterEnd++;
            }

            var glyphCount = clusterEnd - clusterStart;
            var glyphs = new ushort[glyphCount];
            var glyphPoints = new SKPoint[glyphCount];
            for (var i = 0; i < glyphCount; i++)
            {
                var glyphIndex = clusterStart + i;
                glyphs[i] = shapedRun.Glyphs[glyphIndex];
                glyphPoints[i] = new SKPoint(startX + shapedRun.Points[glyphIndex].X, anchorY + shapedRun.Points[glyphIndex].Y);
            }

            var clusterPoint = glyphPoints[0];
            var textBlob = SKTextBlob.CreatePositionedGlyphs(
                glyphs,
                glyphPoints);
            canvas.Save();
            canvas.SetMatrix(SKMatrix.CreateRotationDegrees(rotation, clusterPoint.X, clusterPoint.Y));
            canvas.DrawText(textBlob, 0f, 0f, shapingPaint);
            canvas.Restore();

            clusterStart = clusterEnd;
        }

        return true;
    }

    private static bool TryCreateDirectedShapedGlyphRun(
        SvgTextBase svgTextBase,
        string text,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out SKPaint shapingPaint,
        out ShapedGlyphRun shapedRun,
        out float advance)
    {
        shapingPaint = paint.Clone();
        shapedRun = default;
        advance = 0f;
        if (string.IsNullOrEmpty(text) ||
            assetLoader is not ISvgTextDirectedGlyphRunResolver glyphRunResolver)
        {
            return false;
        }

        var fallbackText = GetBrowserCompatibleFallbackText(svgTextBase, text, assetLoader);
        if (string.IsNullOrEmpty(fallbackText))
        {
            return false;
        }

        if (assetLoader is ISvgTextRunTypefaceResolver runTypefaceResolver)
        {
            var runTypeface = runTypefaceResolver.FindRunTypeface(fallbackText, shapingPaint);
            if (runTypeface is not null)
            {
                shapingPaint.Typeface = runTypeface;
            }
        }

        var containsCursiveText = ContainsCursiveTrackingCodepoint(fallbackText);
        if (containsCursiveText && ContainsMixedStrongDirections(fallbackText))
        {
            return false;
        }

        var shapedFallbackText = ApplyBrowserCompatibleBidiControls(svgTextBase, fallbackText);
        var rightToLeft = containsCursiveText || IsRightToLeft(svgTextBase);
        if (!glyphRunResolver.TryShapeGlyphRun(shapedFallbackText, shapingPaint, rightToLeft, out shapedRun) ||
            shapedRun.Glyphs.Length == 0 ||
            shapedRun.Points.Length != shapedRun.Glyphs.Length ||
            shapedRun.Clusters.Length != shapedRun.Glyphs.Length)
        {
            shapedRun = default;
            return false;
        }

        advance = EnsureWhitespaceAdvance(fallbackText, shapingPaint, assetLoader, shapedRun.Advance);
        return advance > 0f;
    }

    private static float DrawCodepointPlacements(
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement[] placements,
        SKRect geometryBounds,
        SKPaint paint,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        FallbackCodepointResolver? fallbackResolver = null)
    {
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

        if (TryDrawSimpleAsciiCodepointPlacements(svgTextBase, text, placements, geometryBounds, paint, canvas, assetLoader, out var simpleAdvance))
        {
            return simpleAdvance;
        }

        fallbackResolver ??= GetFallbackCodepointResolver(svgTextBase);
        if (TryDrawGraphemeClusterPlacements(svgTextBase, text, placements, paint, canvas, assetLoader, fallbackResolver, out var clusterAdvance))
        {
            return clusterAdvance;
        }

        var advance = 0f;
        var placementIndex = 0;
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            var placement = placements[placementIndex++];
            if (fallbackResolver.TryGet(svgTextBase, codepoint, paint, out var cached))
            {
                DrawPositionedText(cached.Text, placement, cached.Paint, canvas);
                advance = cached.Advance * placement.ScaleX;
                continue;
            }

            var localPaint = paint.Clone();
            if (SvgFontTextRenderer.TryGetLayout(svgTextBase, codepoint, localPaint, assetLoader, out var svgFontLayout) &&
                svgFontLayout is not null)
            {
                DrawPositionedLayout(svgFontLayout, placement, localPaint, canvas);
                advance = svgFontLayout.Advance * placement.ScaleX;
                continue;
            }

            var resolved = fallbackResolver.Resolve(svgTextBase, codepoint, localPaint, assetLoader);
            DrawPositionedText(resolved.Text, placement, resolved.Paint, canvas);
            advance = resolved.Advance * placement.ScaleX;
        }

        return advance;
    }

    private static bool TryDrawSimpleAsciiCodepointPlacements(
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement[] placements,
        SKRect geometryBounds,
        SKPaint paint,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        out float advance)
    {
        advance = 0f;
        if (!TryCreateSimpleAsciiPositionedRunPaint(svgTextBase, text, placements, paint, assetLoader, out var runPaint, out var codepoints))
        {
            return false;
        }

        if (TryDrawCompactPositionedTextRun(codepoints, placements, runPaint, canvas))
        {
            return true;
        }

        for (var i = 0; i < codepoints.Count; i++)
        {
            DrawPositionedText(codepoints[i], placements[i], runPaint, canvas);
        }

        return true;
    }

    private static bool TryDrawCompactPositionedTextRun(
        IReadOnlyList<string> codepoints,
        PositionedCodepointPlacement[] placements,
        SKPaint paint,
        SKCanvas canvas)
    {
        if (codepoints.Count < MinCompactPositionedTextRunCodepoints ||
            codepoints.Count != placements.Length ||
            HasCustomTextShapingProperties(paint))
        {
            return false;
        }

        var fragments = new PositionedTextRunFragment[codepoints.Count];
        for (var i = 0; i < codepoints.Count; i++)
        {
            var placement = placements[i];
            fragments[i] = new PositionedTextRunFragment(
                codepoints[i],
                placement.Point,
                placement.RotationDegrees,
                placement.ScaleX,
                placement.ScaleOriginX);
        }

        canvas.DrawPositionedTextRun(fragments, paint);
        return true;
    }

    private static bool HasCustomTextShapingProperties(SKPaint paint)
    {
        return !string.IsNullOrEmpty(paint.FontFeatureSettings) ||
               !string.IsNullOrEmpty(paint.FontKerning) ||
               !string.IsNullOrEmpty(paint.FontVariantLigatures);
    }

    private static bool TryDrawGraphemeClusterPlacements(
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement[] placements,
        SKPaint paint,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        FallbackCodepointResolver fallbackResolver,
        out float advance)
    {
        advance = 0f;
        if (string.IsNullOrEmpty(text) ||
            placements.Length <= 1 ||
            IsSimpleAsciiSequentialCompileText(text))
        {
            return false;
        }

        var codepoints = SplitCodepointsReadOnly(text);
        if (codepoints.Count != placements.Length)
        {
            return false;
        }

        var clusterStarts = SvgTextBoundaryResolver.Default.GetGraphemeClusterStartCharIndexes(text);
        if (clusterStarts.Count == 0)
        {
            return false;
        }

        if (clusterStarts.Count >= codepoints.Count)
        {
            return false;
        }

        var codepointOffsets = CreateCodepointCharOffsets(codepoints);
        var hasMultiCodepointCluster = false;
        for (var clusterIndex = 0; clusterIndex < clusterStarts.Count; clusterIndex++)
        {
            var clusterStart = clusterStarts[clusterIndex];
            var clusterEnd = clusterIndex + 1 < clusterStarts.Count
                ? clusterStarts[clusterIndex + 1]
                : text.Length;
            var firstCodepointIndex = GetCodepointIndexFromCharOffset(codepointOffsets, clusterStart);
            var endCodepointIndex = GetTextPathCodepointBoundaryIndex(codepointOffsets, clusterEnd);
            if (firstCodepointIndex < 0 || endCodepointIndex < 0)
            {
                return false;
            }

            if (endCodepointIndex - firstCodepointIndex > 1)
            {
                hasMultiCodepointCluster = true;
                break;
            }
        }

        if (!hasMultiCodepointCluster)
        {
            return false;
        }

        for (var clusterIndex = 0; clusterIndex < clusterStarts.Count; clusterIndex++)
        {
            var clusterStart = clusterStarts[clusterIndex];
            var clusterEnd = clusterIndex + 1 < clusterStarts.Count
                ? clusterStarts[clusterIndex + 1]
                : text.Length;
            var firstCodepointIndex = GetCodepointIndexFromCharOffset(codepointOffsets, clusterStart);
            var endCodepointIndex = GetTextPathCodepointBoundaryIndex(codepointOffsets, clusterEnd);
            if (firstCodepointIndex < 0 || endCodepointIndex <= firstCodepointIndex || endCodepointIndex > placements.Length)
            {
                return false;
            }

            var clusterText = text.Substring(clusterStart, clusterEnd - clusterStart);
            var placement = placements[firstCodepointIndex];
            if (fallbackResolver.TryGet(svgTextBase, clusterText, paint, out var cached))
            {
                DrawPositionedText(cached.Text, placement, cached.Paint, canvas);
                advance = cached.Advance * placement.ScaleX;
                continue;
            }

            var localPaint = paint.Clone();
            if (SvgFontTextRenderer.TryGetLayout(svgTextBase, clusterText, localPaint, assetLoader, out var svgFontLayout) &&
                svgFontLayout is not null)
            {
                DrawPositionedLayout(svgFontLayout, placement, localPaint, canvas);
                advance = svgFontLayout.Advance * placement.ScaleX;
                continue;
            }

            var resolved = fallbackResolver.Resolve(svgTextBase, clusterText, localPaint, assetLoader);
            DrawPositionedText(resolved.Text, placement, resolved.Paint, canvas);
            advance = resolved.Advance * placement.ScaleX;
        }

        return true;
    }

    private static void AppendCodepointPlacementsPath(
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement[] placements,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        SKPath path,
        FallbackCodepointResolver? fallbackResolver = null)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;
        fallbackResolver ??= GetFallbackCodepointResolver(svgTextBase);

        var placementIndex = 0;
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            var placement = placements[placementIndex++];
            if (fallbackResolver.TryGet(svgTextBase, codepoint, paint, out var cached))
            {
                AppendPositionedTextPath(path, cached.Text, placement, cached.Paint, assetLoader);
                continue;
            }

            var localPaint = paint.Clone();
            if (SvgFontTextRenderer.TryGetLayout(svgTextBase, codepoint, localPaint, assetLoader, out var svgFontLayout) &&
                svgFontLayout is not null)
            {
                AppendPositionedLayoutPath(path, svgFontLayout, placement);
                continue;
            }

            var resolved = fallbackResolver.Resolve(svgTextBase, codepoint, localPaint, assetLoader);
            AppendPositionedTextPath(path, resolved.Text, placement, resolved.Paint, assetLoader);
        }
    }

    private static SKRect MeasureCodepointPlacementBounds(
        SvgTextBase svgTextBase,
        string text,
        IReadOnlyList<string> codepoints,
        PositionedCodepointPlacement[] placements,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out float advance,
        FallbackCodepointResolver? fallbackResolver = null)
    {
        if (codepoints.Count != placements.Length)
        {
            return MeasureCodepointPlacementBounds(svgTextBase, text, placements, geometryBounds, assetLoader, out advance, fallbackResolver);
        }

        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

        var bounds = SKRect.Empty;
        advance = 0f;
        if (TryMeasureSimpleAsciiCodepointPlacementBounds(svgTextBase, text, placements, paint, assetLoader, out bounds))
        {
            return bounds;
        }

        fallbackResolver ??= GetFallbackCodepointResolver(svgTextBase);
        for (var i = 0; i < codepoints.Count; i++)
        {
            if (TryMeasurePositionedCodepointBounds(svgTextBase, codepoints[i], placements[i], paint, assetLoader, fallbackResolver, out var candidateBounds, out var candidateAdvance))
            {
                UnionBounds(ref bounds, candidateBounds);
                advance = candidateAdvance;
            }
        }

        return bounds;
    }

    private static SKRect MeasureCodepointPlacementBounds(
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement[] placements,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out float advance,
        FallbackCodepointResolver? fallbackResolver = null)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

        var bounds = SKRect.Empty;
        advance = 0f;
        if (TryMeasureSimpleAsciiCodepointPlacementBounds(svgTextBase, text, placements, paint, assetLoader, out bounds))
        {
            return bounds;
        }

        fallbackResolver ??= GetFallbackCodepointResolver(svgTextBase);
        var placementIndex = 0;
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            var placement = placements[placementIndex++];
            if (TryMeasurePositionedCodepointBounds(svgTextBase, codepoint, placement, paint, assetLoader, fallbackResolver, out var candidateBounds, out var candidateAdvance))
            {
                UnionBounds(ref bounds, candidateBounds);
                advance = candidateAdvance;
            }
        }

        return bounds;
    }

    private static bool TryMeasureSimpleAsciiCodepointPlacementBounds(
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement[] placements,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out SKRect bounds)
    {
        bounds = SKRect.Empty;
        if (!TryCreateSimpleAsciiPositionedRunPaint(svgTextBase, text, placements, paint, assetLoader, out var runPaint, out var codepoints))
        {
            return false;
        }

        var hasMetrics = false;
        var metrics = default(SKFontMetrics);
        var spaceAdvance = float.NaN;
        for (var i = 0; i < codepoints.Count; i++)
        {
            var placement = placements[i];
            SKRect candidate;
            if (TryGetRenderedTextLocalBounds(codepoints[i], runPaint, assetLoader, out var glyphBounds))
            {
                candidate = new SKRect(
                    placement.Point.X + glyphBounds.Left,
                    placement.Point.Y + glyphBounds.Top,
                    placement.Point.X + glyphBounds.Right,
                    placement.Point.Y + glyphBounds.Bottom);
            }
            else
            {
                if (!hasMetrics)
                {
                    metrics = assetLoader.GetFontMetrics(runPaint);
                    hasMetrics = true;
                }

                var glyphAdvance = codepoints[i] == " "
                    ? spaceAdvance
                    : float.NaN;
                if (!IsValidPositiveAdvance(glyphAdvance))
                {
                    var glyphMeasureBounds = new SKRect();
                    glyphAdvance = assetLoader.MeasureText(codepoints[i], runPaint, ref glyphMeasureBounds);
                    if (codepoints[i] == " ")
                    {
                        spaceAdvance = glyphAdvance;
                    }
                }

                candidate = new SKRect(
                    placement.Point.X,
                    placement.Point.Y + metrics.Ascent,
                    placement.Point.X + glyphAdvance,
                    placement.Point.Y + metrics.Descent);
            }

            candidate = ScaleBoundsX(candidate, GetScalePivot(placement), placement.ScaleX);
            candidate = RotateBounds(candidate, placement.Point, placement.RotationDegrees);
            UnionBounds(ref bounds, candidate);
        }

        return true;
    }

    private static bool TryCreateSimpleAsciiPositionedBoundsPaint(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out SKPaint runPaint,
        out SKFontMetrics metrics,
        out float padding)
    {
        runPaint = new SKPaint();
        metrics = default;
        padding = 0f;

        if (!IsSimpleAsciiSequentialCompileText(text) ||
            RequiresSyntheticSmallCaps(svgTextBase, text))
        {
            return false;
        }

        PaintingService.SetPaintText(svgTextBase, geometryBounds, runPaint);
        runPaint.TextAlign = SKTextAlign.Left;

        var fallbackText = GetBrowserCompatibleFallbackText(svgTextBase, text, assetLoader);
        if (!string.Equals(fallbackText, text, StringComparison.Ordinal) ||
            HasPositionedSvgFontLayouts(svgTextBase, text, runPaint, assetLoader) ||
            !TryCreateSingleSpanShapingPaint(text, runPaint, assetLoader, out runPaint) ||
            runPaint.Typeface is null)
        {
            return false;
        }

        metrics = assetLoader.GetFontMetrics(runPaint);
        padding = Math.Max(1f, runPaint.TextSize * 0.25f);
        return metrics.Descent > metrics.Ascent;
    }

    private static SKRect CreateFastPositionedTextBounds(
        PositionedCodepointPlacement placement,
        float advance,
        SKFontMetrics metrics,
        float padding)
    {
        var bounds = new SKRect(
            placement.Point.X - padding,
            placement.Point.Y + metrics.Ascent - padding,
            placement.Point.X + advance + padding,
            placement.Point.Y + metrics.Descent + padding);
        bounds = ScaleBoundsX(bounds, GetScalePivot(placement), placement.ScaleX);
        return RotateBounds(bounds, placement.Point, placement.RotationDegrees);
    }

    private static bool TryCreateSimpleAsciiPositionedRunPaint(
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement[] placements,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out SKPaint runPaint,
        out IReadOnlyList<string> codepoints)
    {
        runPaint = paint;
        codepoints = Array.Empty<string>();
        if (placements.Length == 0 ||
            !IsSimpleAsciiSequentialCompileText(text) ||
            RequiresSyntheticSmallCaps(svgTextBase, text))
        {
            return false;
        }

        var fallbackText = GetBrowserCompatibleFallbackText(svgTextBase, text, assetLoader);
        if (!string.Equals(fallbackText, text, StringComparison.Ordinal))
        {
            return false;
        }

        if (HasPositionedSvgFontLayouts(svgTextBase, text, paint, assetLoader) ||
            !TryCreateSingleSpanShapingPaint(text, paint, assetLoader, out runPaint) ||
            runPaint.Typeface is null)
        {
            return false;
        }

        codepoints = SplitCodepointsReadOnly(text);
        return codepoints.Count == placements.Length;
    }

    private static bool TryMeasurePositionedCodepointBounds(
        SvgTextBase svgTextBase,
        string codepoint,
        PositionedCodepointPlacement placement,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        FallbackCodepointResolver fallbackResolver,
        out SKRect bounds,
        out float advance)
    {
        if (fallbackResolver.TryGet(svgTextBase, codepoint, paint, out var cached))
        {
            if (!cached.HasLocalBounds)
            {
                cached = fallbackResolver.ResolveWithLocalBounds(svgTextBase, codepoint, paint, assetLoader);
            }

            return TryMeasureResolvedFallbackCodepointBounds(cached, placement, assetLoader, out bounds, out advance);
        }

        var localPaint = paint.Clone();
        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, codepoint, localPaint, assetLoader, out var svgFontLayout) &&
            svgFontLayout is not null)
        {
            bounds = svgFontLayout.GetBounds(placement.Point.X, placement.Point.Y);
            bounds = ScaleBoundsX(bounds, GetScalePivot(placement), placement.ScaleX);
            bounds = RotateBounds(bounds, placement.Point, placement.RotationDegrees);
            advance = svgFontLayout.Advance * placement.ScaleX;
            return true;
        }

        var resolved = fallbackResolver.ResolveWithLocalBounds(svgTextBase, codepoint, localPaint, assetLoader);
        return TryMeasureResolvedFallbackCodepointBounds(resolved, placement, assetLoader, out bounds, out advance);
    }

    private static bool TryMeasureResolvedFallbackCodepointBounds(
        ResolvedFallbackCodepoint resolved,
        PositionedCodepointPlacement placement,
        ISvgAssetLoader assetLoader,
        out SKRect bounds,
        out float advance)
    {
        if (resolved.HasLocalBounds)
        {
            bounds = new SKRect(
                placement.Point.X + resolved.LocalBounds.Left,
                placement.Point.Y + resolved.LocalBounds.Top,
                placement.Point.X + resolved.LocalBounds.Right,
                placement.Point.Y + resolved.LocalBounds.Bottom);
        }
        else if (TryGetRenderedTextLocalBounds(resolved.Text, resolved.Paint, assetLoader, out var glyphBounds))
        {
            bounds = new SKRect(
                placement.Point.X + glyphBounds.Left,
                placement.Point.Y + glyphBounds.Top,
                placement.Point.X + glyphBounds.Right,
                placement.Point.Y + glyphBounds.Bottom);
        }
        else
        {
            var metrics = assetLoader.GetFontMetrics(resolved.Paint);
            bounds = new SKRect(
                placement.Point.X,
                placement.Point.Y + metrics.Ascent,
                placement.Point.X + resolved.Advance,
                placement.Point.Y + metrics.Descent);
        }

        bounds = ScaleBoundsX(bounds, GetScalePivot(placement), placement.ScaleX);
        bounds = RotateBounds(bounds, placement.Point, placement.RotationDegrees);
        advance = resolved.Advance * placement.ScaleX;
        return true;
    }

    private static bool TryGetCodepointDecorationExtents(
        SvgTextBase svgTextBase,
        string codepoint,
        PositionedCodepointPlacement placement,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        FallbackCodepointResolver? fallbackResolver,
        out float leftOffset,
        out float rightOffset)
    {
        leftOffset = 0f;
        rightOffset = 0f;
        fallbackResolver ??= GetFallbackCodepointResolver(svgTextBase);

        if (fallbackResolver.TryGet(svgTextBase, codepoint, paint, out var cached))
        {
            rightOffset = cached.Advance;
            return rightOffset > leftOffset;
        }

        var localPaint = paint.Clone();
        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, codepoint, localPaint, assetLoader, out var svgFontLayout) &&
            svgFontLayout is not null)
        {
            leftOffset = 0f;
            rightOffset = svgFontLayout.Advance;
            return rightOffset > leftOffset;
        }

        var resolved = fallbackResolver.Resolve(svgTextBase, codepoint, localPaint, assetLoader);
        leftOffset = 0f;
        rightOffset = resolved.Advance;
        return rightOffset > leftOffset;
    }

    private static bool TryGetRenderedTextLocalBounds(
        string text,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out SKRect bounds)
    {
        var canCache = text.Length <= RenderedTextLocalBoundsCacheMaxTextLength;
        var cacheKey = canCache ? CreateRenderedTextLocalBoundsCacheKey(assetLoader, text, paint) : default;
        if (canCache && s_renderedTextLocalBoundsCache.TryGetValue(cacheKey, out bounds))
        {
            return true;
        }

        var path = assetLoader.GetTextPath(text, paint, 0f, 0f);
        if (path is not null && !path.IsEmpty)
        {
            bounds = path.Bounds;
            return CacheRenderedTextLocalBoundsIfNeeded(canCache, cacheKey, bounds);
        }

        bounds = new SKRect();
        assetLoader.MeasureText(text, paint, ref bounds);
        return CacheRenderedTextLocalBoundsIfNeeded(canCache, cacheKey, bounds);
    }

    private static RenderedTextLocalBoundsCacheKey CreateRenderedTextLocalBoundsCacheKey(
        ISvgAssetLoader assetLoader,
        string text,
        SKPaint paint)
    {
        return new RenderedTextLocalBoundsCacheKey(
            RuntimeHelpers.GetHashCode(assetLoader),
            text,
            paint.TextSize,
            paint.LcdRenderText,
            paint.SubpixelText,
            paint.TextEncoding,
            paint.TextAlign,
            paint.FontFeatureSettings,
            paint.FontKerning,
            paint.FontVariantLigatures,
            paint.Typeface?.FamilyName,
            paint.Typeface?.FontWeight ?? SKFontStyleWeight.Normal,
            paint.Typeface?.FontWidth ?? SKFontStyleWidth.Normal,
            paint.Typeface?.FontSlant ?? SKFontStyleSlant.Upright);
    }

    private static bool CacheRenderedTextLocalBoundsIfNeeded(
        bool canCache,
        RenderedTextLocalBoundsCacheKey cacheKey,
        SKRect bounds)
    {
        if (bounds.IsEmpty)
        {
            return false;
        }

        if (canCache)
        {
            s_renderedTextLocalBoundsCache.TryAdd(cacheKey, bounds);
            TrimRenderedTextLocalBoundsCacheIfNeeded();
        }

        return true;
    }

    private static void TrimRenderedTextLocalBoundsCacheIfNeeded()
    {
        if (s_renderedTextLocalBoundsCache.Count > RenderedTextLocalBoundsCacheLimit)
        {
            s_renderedTextLocalBoundsCache.Clear();
        }
    }

    private static bool HasLinearDecorations(IReadOnlyList<PositionedCodepointPlacement> placements)
    {
        if (placements.Count == 0)
        {
            return false;
        }

        var baselineY = placements[0].Point.Y;
        for (var i = 0; i < placements.Count; i++)
        {
            if (placements[i].RotationDegrees != 0f || placements[i].Point.Y != baselineY)
            {
                return false;
            }
        }

        return true;
    }

    private static TextPathRenderResult DrawTextPath(
        SvgTextPath svgTextPath,
        ref float currentX,
        ref float currentY,
        bool useCurrentPositionOffset,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint)
    {
        if (!HasFeatures(svgTextPath, ignoreAttributes) ||
            !MaskingService.CanDraw(svgTextPath, ignoreAttributes) ||
            HasRecursiveTextPathReference(svgTextPath))
        {
            return TextPathRenderResult.NotRendered;
        }

        if (!TryResolveTextPathGeometry(svgTextPath, viewport, out _, out var skPath, out var geometryBounds, out var pathSamples, out var pathLength, out var isClosedLoop))
        {
            return TextPathRenderResult.MissingGeometry;
        }

        if (!TryCollectTextPathRuns(svgTextPath, viewport, out var runs))
        {
            return TextPathRenderResult.NotRendered;
        }

        ResolveTextPathChunkOffsets(svgTextPath, useCurrentPositionOffset, currentX, currentY, viewport, assetLoader, pathSamples, out var horizontalOffset, out var verticalOffset);
        var startOffset = horizontalOffset + ResolveTextPathStartOffset(svgTextPath, skPath, viewport, pathLength);
        var hOffset = ResolveTextPathHorizontalOffset(svgTextPath, startOffset, pathLength, geometryBounds, runs, assetLoader);

        if (svgTextPath.Method == SvgTextPathMethod.Stretch)
        {
            if (!TryCreateStretchedTextPathRunPaths(runs, pathSamples, pathLength, isClosedLoop, hOffset, verticalOffset, viewport, geometryBounds, assetLoader, out var stretchedRuns, out var stretchedEndOffset, out var stretchedEndVOffset))
            {
                return TextPathRenderResult.NotRendered;
            }

            DrawStretchedTextPathRuns(stretchedRuns, viewport, geometryBounds, ignoreAttributes, canvas, assetLoader, references, getElementAddressKey, contextPaint);
            var cursorDistance = GetTextPathCursorDistance(svgTextPath, pathLength, stretchedEndOffset);
            AdvanceTextPathPosition(pathSamples, cursorDistance, stretchedEndVOffset, isClosedLoop, ref currentX, ref currentY);
            return TextPathRenderResult.Rendered;
        }

        if (!TryCreateTextPathRunPlacements(runs, pathSamples, isClosedLoop, hOffset, verticalOffset, viewport, geometryBounds, assetLoader, out var positionedRuns, out var endOffset, out var endVOffset))
        {
            return TextPathRenderResult.NotRendered;
        }

        DrawPositionedTextPathRuns(positionedRuns, viewport, geometryBounds, ignoreAttributes, canvas, assetLoader, references, getElementAddressKey, contextPaint);
        var textPathCursorDistance = GetTextPathCursorDistance(svgTextPath, pathLength, endOffset);
        AdvanceTextPathPosition(pathSamples, textPathCursorDistance, endVOffset, isClosedLoop, ref currentX, ref currentY);
        return TextPathRenderResult.Rendered;
    }

    private static TextPathRenderResult AppendTextPathClip(
        SvgTextPath svgTextPath,
        ref float currentX,
        ref float currentY,
        bool useCurrentPositionOffset,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        SKPath path)
    {
        if (HasRecursiveTextPathReference(svgTextPath))
        {
            return TextPathRenderResult.NotRendered;
        }

        if (!TryResolveTextPathGeometry(svgTextPath, viewport, out _, out var skPath, out var geometryBounds, out var pathSamples, out var pathLength, out var isClosedLoop))
        {
            return TextPathRenderResult.MissingGeometry;
        }

        if (!TryCollectTextPathRuns(svgTextPath, viewport, out var runs) || runs.Count == 0)
        {
            return TextPathRenderResult.NotRendered;
        }

        ResolveTextPathChunkOffsets(svgTextPath, useCurrentPositionOffset, currentX, currentY, viewport, assetLoader, pathSamples, out var horizontalOffset, out var verticalOffset);
        var startOffset = horizontalOffset + ResolveTextPathStartOffset(svgTextPath, skPath, viewport, pathLength);
        var hOffset = ResolveTextPathHorizontalOffset(svgTextPath, startOffset, pathLength, geometryBounds, runs, assetLoader);

        if (svgTextPath.Method == SvgTextPathMethod.Stretch)
        {
            if (!TryCreateStretchedTextPathRunPaths(runs, pathSamples, pathLength, isClosedLoop, hOffset, verticalOffset, viewport, geometryBounds, assetLoader, out var stretchedRuns, out var stretchedEndOffset, out var stretchedEndVOffset))
            {
                return TextPathRenderResult.NotRendered;
            }

            for (var i = 0; i < stretchedRuns.Count; i++)
            {
                AppendPathCommands(path, stretchedRuns[i].Path);
            }

            var cursorDistance = GetTextPathCursorDistance(svgTextPath, pathLength, stretchedEndOffset);
            AdvanceTextPathPosition(pathSamples, cursorDistance, stretchedEndVOffset, isClosedLoop, ref currentX, ref currentY);
            return TextPathRenderResult.Rendered;
        }

        if (!TryCreateTextPathRunPlacements(runs, pathSamples, isClosedLoop, hOffset, verticalOffset, viewport, geometryBounds, assetLoader, out var positionedRuns, out var endOffset, out var endVOffset))
        {
            return TextPathRenderResult.NotRendered;
        }

        var fallbackResolver = positionedRuns.Count > 0
            ? GetFallbackCodepointResolver(positionedRuns[0].StyleSource)
            : GetFallbackCodepointResolver(svgTextPath);
        for (var i = 0; i < positionedRuns.Count; i++)
        {
            AppendCodepointPlacementsPath(positionedRuns[i].StyleSource, positionedRuns[i].Text, positionedRuns[i].Placements, geometryBounds, assetLoader, path, fallbackResolver);
        }

        var textPathCursorDistance = GetTextPathCursorDistance(svgTextPath, pathLength, endOffset);
        AdvanceTextPathPosition(pathSamples, textPathCursorDistance, endVOffset, isClosedLoop, ref currentX, ref currentY);
        return TextPathRenderResult.Rendered;
    }

    private static void DrawTextRef(
        SvgTextRef svgTextRef,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references,
        SKRect rootGeometryBounds,
        Func<SvgElement?, string?>? getElementAddressKey,
        RotationState? rotationState)
    {
        if (!IsTextReferenceRenderingEnabled(assetLoader))
        {
            return;
        }

        if (!HasFeatures(svgTextRef, ignoreAttributes) ||
            !MaskingService.CanDraw(svgTextRef, ignoreAttributes) ||
            SvgService.HasRecursiveReference(svgTextRef, static e => SvgService.GetEffectiveReferenceUri(e, e.ReferencedElement), new HashSet<Uri>()))
        {
            return;
        }

        var svgReferencedText = SvgService.GetReference<SvgTextBase>(svgTextRef, SvgService.GetEffectiveReferenceUri(svgTextRef, svgTextRef.ReferencedElement));
        if (svgReferencedText is null)
        {
            return;
        }

        DrawTextBase(svgReferencedText, ref currentX, ref currentY, viewport, ignoreAttributes, canvas, assetLoader, references, rootGeometryBounds, getElementAddressKey, rotationState, inheritedAbsolutePositionState: null, trimLeadingWhitespaceAtStart: true, contextPaint: null);
    }

    private static void AppendTextRefClip(
        SvgTextRef svgTextRef,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        SKRect rootGeometryBounds,
        SKPath path,
        RotationState? rotationState)
    {
        if (!IsTextReferenceRenderingEnabled(assetLoader))
        {
            return;
        }

        if (SvgService.HasRecursiveReference(svgTextRef, static e => SvgService.GetEffectiveReferenceUri(e, e.ReferencedElement), new HashSet<Uri>()))
        {
            return;
        }

        var svgReferencedText = SvgService.GetReference<SvgTextBase>(svgTextRef, SvgService.GetEffectiveReferenceUri(svgTextRef, svgTextRef.ReferencedElement));
        if (svgReferencedText is null)
        {
            return;
        }

        AppendTextClipPathBase(svgReferencedText, ref currentX, ref currentY, viewport, assetLoader, rootGeometryBounds, path, rotationState, inheritedAbsolutePositionState: null, trimLeadingWhitespaceAtStart: true);
    }

    private static void MeasureTextBase(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds,
        RotationState? inheritedRotationState,
        AbsolutePositionState? inheritedAbsolutePositionState,
        bool trimLeadingWhitespaceAtStart)
    {
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
        var localCurrentX = currentX + baselineShift.X;
        var localCurrentY = currentY + baselineShift.Y;
        var rotationState = ResolveRotationState(svgTextBase, inheritedRotationState);
        var absolutePositionState = ResolveAbsolutePositionState(svgTextBase, inheritedAbsolutePositionState, viewport);

        if (TryMeasureSharedInlineSizeTextLayout(svgTextBase, ref localCurrentX, ref localCurrentY, viewport, assetLoader, ref bounds, trimLeadingWhitespaceAtStart))
        {
            currentX = localCurrentX - baselineShift.X;
            currentY = localCurrentY - baselineShift.Y;
            return;
        }

        if (TryMeasureWrappedInlineSizeTextLengthLayout(svgTextBase, ref localCurrentX, ref localCurrentY, viewport, assetLoader, ref bounds, trimLeadingWhitespaceAtStart))
        {
            currentX = localCurrentX - baselineShift.X;
            currentY = localCurrentY - baselineShift.Y;
            return;
        }

        if (TryMeasureFlattenedTextLengthLayout(svgTextBase, ref localCurrentX, ref localCurrentY, viewport, assetLoader, ref bounds, trimLeadingWhitespaceAtStart))
        {
            currentX = localCurrentX - baselineShift.X;
            currentY = localCurrentY - baselineShift.Y;
            return;
        }

        if (inheritedRotationState is null &&
            inheritedAbsolutePositionState is null &&
            TryMeasureSequentialTextRuns(svgTextBase, ref localCurrentX, ref localCurrentY, viewport, assetLoader, ref bounds, trimLeadingWhitespaceAtStart))
        {
            currentX = localCurrentX - baselineShift.X;
            currentY = localCurrentY - baselineShift.Y;
            return;
        }

        var useInitialPosition = true;
        var trimLeadingWhitespace = trimLeadingWhitespaceAtStart;
        var previousEndedWithSpace = false;
        MeasureTextNodes(
            GetContentNodeList(svgTextBase),
            svgTextBase,
            ref localCurrentX,
            ref localCurrentY,
            ref useInitialPosition,
            ref trimLeadingWhitespace,
            ref previousEndedWithSpace,
            viewport,
            assetLoader,
            ref bounds,
            rotationState,
            absolutePositionState);
        currentX = localCurrentX - baselineShift.X;
        currentY = localCurrentY - baselineShift.Y;
    }

    private static void MeasureTextNodes(
        IEnumerable<ISvgNode> contentNodes,
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        ref bool useInitialPosition,
        ref bool trimLeadingWhitespace,
        ref bool previousEndedWithSpace,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds,
        RotationState? rotationState,
        AbsolutePositionState? absolutePositionState)
    {
        var contentNodeList = ToContentNodeList(contentNodes);
        for (var nodeIndex = 0; nodeIndex < contentNodeList.Count; nodeIndex++)
        {
            var node = contentNodeList[nodeIndex];
            switch (node)
            {
                case SvgAnchor svgAnchor:
                    if (!CanRenderTextSubtree(svgAnchor))
                    {
                        break;
                    }

                    var anchorStyleSource = CreateAnchorTextStyleSource(svgAnchor);
                    MeasureTextNodes(GetContentNodeList(svgAnchor), anchorStyleSource, ref currentX, ref currentY, ref useInitialPosition, ref trimLeadingWhitespace, ref previousEndedWithSpace, viewport, assetLoader, ref bounds, rotationState, absolutePositionState);
                    break;

                case not SvgTextBase:
                    var rawContent = node.Content;
                    if (string.IsNullOrEmpty(node.Content))
                    {
                        break;
                    }

                    var text = PrepareText(
                        svgTextBase,
                        node.Content,
                        trimLeadingWhitespace: trimLeadingWhitespace,
                        trimTrailingWhitespace: IsTerminalContentNode(contentNodeList, nodeIndex));
                    if (previousEndedWithSpace &&
                        CollapsesTextWhitespace(svgTextBase) &&
                        !string.IsNullOrEmpty(text) &&
                        text![0] == ' ')
                    {
                        text = text.TrimStart(' ');
                    }

                    if (string.IsNullOrEmpty(text) &&
                        !string.IsNullOrWhiteSpace(rawContent) &&
                        CollapsesTextWhitespace(svgTextBase) &&
                        !previousEndedWithSpace &&
                        HasRenderableTextContentBefore(contentNodeList, nodeIndex) &&
                        HasRenderableTextContentAfter(contentNodeList, nodeIndex))
                    {
                        text = " ";
                    }

                    if (string.IsNullOrEmpty(text))
                    {
                        break;
                    }

                    var codepointCount = CountCodepoints(text!);
                    var xs = new List<float>();
                    var ys = new List<float>();
                    var dxs = new List<float>();
                    var dys = new List<float>();
                    absolutePositionState?.BuildEffectiveAbsolutePositions(codepointCount, xs, ys);
                    if (absolutePositionState is null)
                    {
                        GetPositionsX(svgTextBase, viewport, assetLoader, xs);
                        GetPositionsY(svgTextBase, viewport, assetLoader, ys);
                    }

                    GetPositionsDX(svgTextBase, viewport, assetLoader, dxs);
                    GetPositionsDY(svgTextBase, viewport, assetLoader, dys);
                    var rotations = ConsumeRotations(rotationState, text!);

                    if (useInitialPosition &&
                        TryCreatePositionedCodepointPoints(svgTextBase, text!, xs, ys, dxs, dys, currentX, currentY, viewport, assetLoader, rotations, out var positionedPoints))
                    {
                        var positionedTextBounds = MeasurePositionedTextStringBounds(svgTextBase, text!, positionedPoints, viewport, assetLoader, rotations, out var positionedAdvance);
                        UnionBounds(ref bounds, positionedTextBounds);
                        MoveToAfterPositionedRun(svgTextBase, positionedPoints[positionedPoints.Length - 1], positionedAdvance, out currentX, out currentY);
                        useInitialPosition = false;
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = text!.EndsWith(" ", StringComparison.Ordinal);
                        absolutePositionState?.Consume(codepointCount);
                        break;
                    }

                    var resetX = useInitialPosition && xs.Count >= 1;
                    var resetY = useInitialPosition && ys.Count >= 1;
                    var x = resetX ? xs[0] : currentX;
                    var y = resetY ? ys[0] : currentY;
                    var dx = useInitialPosition && dxs.Count >= 1 ? dxs[0] : 0f;
                    var dy = useInitialPosition && dys.Count >= 1 ? dys[0] : 0f;
                    currentX = x + dx;
                    currentY = y + dy;
                    ApplyBaselineShiftToResetInitialAxes(svgTextBase, viewport, assetLoader, resetX, resetY, ref currentX, ref currentY);

                    var textBounds = MeasureTextStringBounds(svgTextBase, text!, currentX, currentY, viewport, assetLoader, rotations, out var advance);
                    UnionBounds(ref bounds, textBounds);
                    ApplyInlineAdvance(svgTextBase, ref currentX, ref currentY, advance);
                    useInitialPosition = false;
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = text!.EndsWith(" ", StringComparison.Ordinal);
                    absolutePositionState?.Consume(codepointCount);
                    break;

                case SvgTextPath svgTextPath:
                    if (!CanRenderTextSubtree(svgTextPath))
                    {
                        break;
                    }

                    var measuredTextPath = MeasureTextPath(svgTextPath, ref currentX, ref currentY, useInitialPosition, viewport, assetLoader, ref bounds);
                    useInitialPosition = false;
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = EndsWithCollapsedSpace(svgTextPath);
                    if (measuredTextPath == TextPathRenderResult.MissingGeometry &&
                        ShouldAbortFollowingContentAfterFailedTextPath(contentNodeList, nodeIndex))
                    {
                        return;
                    }

                    break;

                case SvgTextRef svgTextRef:
                    {
                        if (ShouldSuppressInlineTextReferenceContent(contentNodeList, nodeIndex))
                        {
                            break;
                        }

                        if (!CanRenderTextSubtree(svgTextRef) ||
                            !IsTextReferenceRenderingEnabled(assetLoader) ||
                            SvgService.HasRecursiveReference(svgTextRef, static e => SvgService.GetEffectiveReferenceUri(e, e.ReferencedElement), new HashSet<Uri>()) ||
                            !TryResolveTextReferenceContent(svgTextRef, out var rawReferencedText))
                        {
                            break;
                        }

                        var referencedMeasureText = PrepareResolvedContent(svgTextRef, rawReferencedText!, trimLeadingWhitespace, previousEndedWithSpace);
                        if (string.IsNullOrEmpty(referencedMeasureText))
                        {
                            break;
                        }

                        var referencedCodepointCount = CountCodepoints(referencedMeasureText!);
                        var referencedXs = new List<float>();
                        var referencedYs = new List<float>();
                        var referencedDxs = new List<float>();
                        var referencedDys = new List<float>();
                        absolutePositionState?.BuildEffectiveAbsolutePositions(referencedCodepointCount, referencedXs, referencedYs);
                        if (absolutePositionState is null)
                        {
                            GetPositionsX(svgTextRef, viewport, assetLoader, referencedXs);
                            GetPositionsY(svgTextRef, viewport, assetLoader, referencedYs);
                        }

                        GetPositionsDX(svgTextRef, viewport, assetLoader, referencedDxs);
                        GetPositionsDY(svgTextRef, viewport, assetLoader, referencedDys);
                        var referencedMeasureRotations = ConsumeRotations(rotationState, referencedMeasureText!);

                        if (useInitialPosition &&
                            TryCreatePositionedCodepointPoints(svgTextRef, referencedMeasureText!, referencedXs, referencedYs, referencedDxs, referencedDys, currentX, currentY, viewport, assetLoader, referencedMeasureRotations, out var referencedMeasurePoints))
                        {
                            var referencedTextBounds = MeasurePositionedTextStringBounds(svgTextRef, referencedMeasureText!, referencedMeasurePoints, viewport, assetLoader, referencedMeasureRotations, out var referencedPositionedAdvance);
                            UnionBounds(ref bounds, referencedTextBounds);
                            MoveToAfterPositionedRun(svgTextRef, referencedMeasurePoints[referencedMeasurePoints.Length - 1], referencedPositionedAdvance, out currentX, out currentY);
                            useInitialPosition = false;
                            trimLeadingWhitespace = false;
                            previousEndedWithSpace = referencedMeasureText!.EndsWith(" ", StringComparison.Ordinal);
                            absolutePositionState?.Consume(referencedCodepointCount);
                            break;
                        }

                        var referencedMeasureResetX = useInitialPosition && referencedXs.Count >= 1;
                        var referencedMeasureResetY = useInitialPosition && referencedYs.Count >= 1;
                        var referencedMeasureX = referencedMeasureResetX ? referencedXs[0] : currentX;
                        var referencedMeasureY = referencedMeasureResetY ? referencedYs[0] : currentY;
                        var referencedMeasureDx = useInitialPosition && referencedDxs.Count >= 1 ? referencedDxs[0] : 0f;
                        var referencedMeasureDy = useInitialPosition && referencedDys.Count >= 1 ? referencedDys[0] : 0f;
                        currentX = referencedMeasureX + referencedMeasureDx;
                        currentY = referencedMeasureY + referencedMeasureDy;
                        ApplyBaselineShiftToResetInitialAxes(svgTextRef, viewport, assetLoader, referencedMeasureResetX, referencedMeasureResetY, ref currentX, ref currentY);

                        var referencedMeasuredBounds = MeasureTextStringBounds(svgTextRef, referencedMeasureText!, currentX, currentY, viewport, assetLoader, referencedMeasureRotations, out var referencedMeasureAdvance);
                        UnionBounds(ref bounds, referencedMeasuredBounds);
                        ApplyInlineAdvance(svgTextRef, ref currentX, ref currentY, referencedMeasureAdvance);
                        useInitialPosition = false;
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = referencedMeasureText!.EndsWith(" ", StringComparison.Ordinal);
                        absolutePositionState?.Consume(referencedCodepointCount);
                        break;
                    }

                case SvgTextSpan svgTextSpan:
                    if (!CanRenderTextSubtree(svgTextSpan))
                    {
                        break;
                    }

                    var childTrimLeadingWhitespace = trimLeadingWhitespace || previousEndedWithSpace || StartsPositionedTextChunk(svgTextSpan);
                    MeasureTextBase(
                        svgTextSpan,
                        ref currentX,
                        ref currentY,
                        viewport,
                        assetLoader,
                        ref bounds,
                        rotationState,
                        absolutePositionState,
                        childTrimLeadingWhitespace);
                    AdvanceInheritedAbsolutePositionState(absolutePositionState, svgTextSpan, childTrimLeadingWhitespace);
                    AdvanceInheritedRotationState(rotationState, svgTextSpan, childTrimLeadingWhitespace);
                    useInitialPosition = false;
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = EndsWithCollapsedSpace(svgTextSpan);
                    break;
            }
        }
    }

    private static bool TryMeasureWrappedInlineSizeTextLengthLayout(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds,
        bool trimLeadingWhitespaceAtStart)
    {
        if (!TryCreateWrappedInlineSizeTextLengthLayout(
                svgTextBase,
                currentX,
                currentY,
                viewport,
                viewport,
                assetLoader,
                trimLeadingWhitespaceAtStart,
                out var layout) ||
            layout is null)
        {
            return false;
        }

        for (var lineIndex = 0; lineIndex < layout.Lines.Count; lineIndex++)
        {
            var line = layout.Lines[lineIndex];
            for (var runIndex = 0; runIndex < line.Runs.Length; runIndex++)
            {
                var runBounds = MeasureCodepointPlacementBounds(
                    line.Runs[runIndex].StyleSource,
                    line.Runs[runIndex].Text,
                    line.Runs[runIndex].Placements,
                    viewport,
                    assetLoader,
                    out _);
                UnionBounds(ref bounds, runBounds);
            }
        }

        currentX = layout.FinalX;
        currentY = layout.FinalY;
        return true;
    }

    private static bool TryMeasureFlattenedTextLengthLayout(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds,
        bool trimLeadingWhitespaceAtStart)
    {
        if (!TryCreateFlattenedTextLengthRuns(svgTextBase, currentX, currentY, viewport, viewport, assetLoader, trimLeadingWhitespaceAtStart, out var runs, out var totalAdvance, out var finalY))
        {
            return false;
        }

        for (var i = 0; i < runs.Length; i++)
        {
            var runBounds = MeasureCodepointPlacementBounds(runs[i].StyleSource, runs[i].Text, runs[i].Placements, viewport, assetLoader, out _);
            UnionBounds(ref bounds, runBounds);
        }

        currentX = ApplyTextAnchor(svgTextBase, currentX, viewport, totalAdvance) + totalAdvance;
        currentY = finalY;
        return true;
    }

    private static TextPathRenderResult MeasureTextPath(
        SvgTextPath svgTextPath,
        ref float currentX,
        ref float currentY,
        bool useCurrentPositionOffset,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds)
    {
        if (HasRecursiveTextPathReference(svgTextPath))
        {
            return TextPathRenderResult.NotRendered;
        }

        if (!TryResolveTextPathGeometry(svgTextPath, viewport, out _, out var skPath, out var geometryBounds, out var pathSamples, out var pathLength, out var isClosedLoop))
        {
            return TextPathRenderResult.MissingGeometry;
        }

        if (!TryCollectTextPathRuns(svgTextPath, viewport, out var runs) || runs.Count == 0)
        {
            return TextPathRenderResult.NotRendered;
        }

        ResolveTextPathChunkOffsets(svgTextPath, useCurrentPositionOffset, currentX, currentY, viewport, assetLoader, pathSamples, out var horizontalOffset, out var verticalOffset);
        var startOffset = horizontalOffset + ResolveTextPathStartOffset(svgTextPath, skPath, viewport, pathLength);
        var hOffset = ResolveTextPathHorizontalOffset(svgTextPath, startOffset, pathLength, geometryBounds, runs, assetLoader);

        if (svgTextPath.Method == SvgTextPathMethod.Stretch)
        {
            if (!TryCreateStretchedTextPathRunPaths(runs, pathSamples, pathLength, isClosedLoop, hOffset, verticalOffset, viewport, geometryBounds, assetLoader, out var stretchedRuns, out var stretchedEndOffset, out var stretchedEndVOffset))
            {
                return TextPathRenderResult.NotRendered;
            }

            for (var i = 0; i < stretchedRuns.Count; i++)
            {
                UnionBounds(ref bounds, stretchedRuns[i].Path.Bounds);
            }

            var cursorDistance = GetTextPathCursorDistance(svgTextPath, pathLength, stretchedEndOffset);
            AdvanceTextPathPosition(pathSamples, cursorDistance, stretchedEndVOffset, isClosedLoop, ref currentX, ref currentY);
            return TextPathRenderResult.Rendered;
        }

        if (!TryCreateTextPathRunPlacements(runs, pathSamples, isClosedLoop, hOffset, verticalOffset, viewport, geometryBounds, assetLoader, out var positionedRuns, out var endOffset, out var endVOffset))
        {
            return TextPathRenderResult.NotRendered;
        }

        var fallbackResolver = positionedRuns.Count > 0
            ? GetFallbackCodepointResolver(positionedRuns[0].StyleSource)
            : GetFallbackCodepointResolver(svgTextPath);
        for (var i = 0; i < positionedRuns.Count; i++)
        {
            var runBounds = MeasureCodepointPlacementBounds(positionedRuns[i].StyleSource, positionedRuns[i].Text, positionedRuns[i].Placements, geometryBounds, assetLoader, out _, fallbackResolver);
            UnionBounds(ref bounds, runBounds);
        }

        var textPathCursorDistance = GetTextPathCursorDistance(svgTextPath, pathLength, endOffset);
        AdvanceTextPathPosition(pathSamples, textPathCursorDistance, endVOffset, isClosedLoop, ref currentX, ref currentY);
        return TextPathRenderResult.Rendered;
    }

    private static void MeasureTextRef(
        SvgTextRef svgTextRef,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds,
        RotationState? rotationState)
    {
        if (!IsTextReferenceRenderingEnabled(assetLoader))
        {
            return;
        }

        if (SvgService.HasRecursiveReference(svgTextRef, static e => SvgService.GetEffectiveReferenceUri(e, e.ReferencedElement), new HashSet<Uri>()))
        {
            return;
        }

        var svgReferencedText = SvgService.GetReference<SvgTextBase>(svgTextRef, SvgService.GetEffectiveReferenceUri(svgTextRef, svgTextRef.ReferencedElement));
        if (svgReferencedText is null)
        {
            return;
        }

        MeasureTextBase(svgReferencedText, ref currentX, ref currentY, viewport, assetLoader, ref bounds, rotationState, inheritedAbsolutePositionState: null, trimLeadingWhitespaceAtStart: true);
    }

    private static SKRect MeasureTextStringBounds(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        float[]? rotations,
        out float advance)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, viewport, paint);

        if (TryGetUniformRunRotation(svgTextBase, text, rotations, out var uniformRotation))
        {
            var bounds = MeasureTextStringBounds(svgTextBase, text, anchorX, anchorY, viewport, assetLoader, Array.Empty<float>(), out advance);
            return RotateBounds(bounds, new SKPoint(anchorX, anchorY), uniformRotation);
        }

        var isVertical = IsVerticalWritingMode(svgTextBase);
        if (isVertical &&
            TryCreateVerticalTextRunPlacements(svgTextBase, text, anchorX, anchorY, viewport, paint.TextAlign, assetLoader, rotations, out var verticalPlacements, out var verticalAdvance))
        {
            advance = verticalAdvance;
            return MeasureVerticalTextRunPlacementsBounds(svgTextBase, verticalPlacements, viewport, assetLoader, out _);
        }

        if ((isVertical || HasPerGlyphLayoutAdjustments(svgTextBase, text)) &&
            TryCreateAlignedCodepointPlacements(svgTextBase, text, anchorX, anchorY, viewport, paint.TextAlign, assetLoader, rotations, out var placements, out var totalAdvance))
        {
            advance = totalAdvance;
            return MeasureCodepointPlacementBounds(svgTextBase, text, placements, viewport, assetLoader, out _);
        }

        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, text, paint, assetLoader, out var svgFontLayout) && svgFontLayout is not null)
        {
            advance = EnsureWhitespaceAdvance(text, paint, assetLoader, svgFontLayout.Advance);
            var svgStartX = anchorX;
            if (paint.TextAlign == SKTextAlign.Center)
            {
                svgStartX -= advance * 0.5f;
            }
            else if (paint.TextAlign == SKTextAlign.Right)
            {
                svgStartX -= advance;
            }

            return ExpandTextBoundsWithAdvanceBox(svgTextBase, svgFontLayout.GetBounds(svgStartX, anchorY), svgStartX, anchorY, advance, paint, assetLoader);
        }

        if (RequiresSyntheticSmallCaps(svgTextBase, text))
        {
            return MeasureSyntheticSmallCapsBounds(svgTextBase, text, anchorX, anchorY, paint.TextAlign, paint, assetLoader, out advance);
        }

        var naturalTotalAdvance = MeasureTextAdvance(svgTextBase, text, viewport, assetLoader);
        var startX = paint.TextAlign switch
        {
            SKTextAlign.Center => anchorX - (naturalTotalAdvance * 0.5f),
            SKTextAlign.Right => anchorX - naturalTotalAdvance,
            _ => anchorX
        };

        if (TryMeasureFallbackTextBounds(svgTextBase, text, startX, anchorY, paint, assetLoader, out var measuredBounds, out advance))
        {
            return ExpandTextBoundsWithAdvanceBox(svgTextBase, measuredBounds, startX, anchorY, advance, paint, assetLoader);
        }

        var metrics = assetLoader.GetFontMetrics(paint);
        advance = naturalTotalAdvance;
        return new SKRect(startX, anchorY + metrics.Ascent, startX + naturalTotalAdvance, anchorY + metrics.Descent);
    }

    private static SKRect MeasurePositionedTextStringBounds(
        SvgTextBase svgTextBase,
        string text,
        SKPoint[] points,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        float[]? rotations,
        out float advance)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, viewport, paint);
        paint.TextAlign = SKTextAlign.Left;

        var bounds = SKRect.Empty;
        advance = 0f;
        var placements = CreatePositionedCodepointPlacements(svgTextBase, text, points, rotations);

        var placementIndex = 0;
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            var placement = placements[placementIndex];
            var localPaint = paint.Clone();
            if (SvgFontTextRenderer.TryGetLayout(svgTextBase, codepoint, localPaint, assetLoader, out var svgFontLayout) &&
                svgFontLayout is not null)
            {
                UnionBounds(ref bounds, RotateBounds(svgFontLayout.GetBounds(placement.Point.X, placement.Point.Y), placement.Point, placement.RotationDegrees));
                advance = svgFontLayout.Advance;
                placementIndex++;
                continue;
            }

            var resolved = ResolveFallbackCodepoint(svgTextBase, codepoint, localPaint, assetLoader);
            MeasurePositionedCodepoints(resolved.Text, placements, resolved.Paint, assetLoader, ref bounds, ref placementIndex, ref advance);
        }

        return bounds;
    }

    private static void UnionBounds(ref SKRect bounds, SKRect candidate)
    {
        if (candidate.IsEmpty)
        {
            return;
        }

        bounds = bounds.IsEmpty
            ? candidate
            : SKRect.Union(bounds, candidate);
    }

    private static void GetPositionsX(SvgTextBase svgTextBase, SKRect viewport, ISvgAssetLoader assetLoader, List<float> xs)
    {
        for (var i = 0; i < svgTextBase.X.Count; i++)
        {
            xs.Add(ResolveTextUnitValue(svgTextBase.X[i], UnitRenderingType.HorizontalOffset, svgTextBase, viewport, assetLoader));
        }
    }

    private static void GetPositionsY(SvgTextBase svgTextBase, SKRect viewport, ISvgAssetLoader assetLoader, List<float> ys)
    {
        for (var i = 0; i < svgTextBase.Y.Count; i++)
        {
            ys.Add(ResolveTextUnitValue(svgTextBase.Y[i], UnitRenderingType.VerticalOffset, svgTextBase, viewport, assetLoader));
        }
    }

    private static void GetPositionsDX(SvgTextBase svgTextBase, SKRect viewport, ISvgAssetLoader assetLoader, List<float> dxs)
    {
        for (var i = 0; i < svgTextBase.Dx.Count; i++)
        {
            dxs.Add(ResolveTextUnitValue(svgTextBase.Dx[i], UnitRenderingType.HorizontalOffset, svgTextBase, viewport, assetLoader));
        }
    }

    private static void GetPositionsDY(SvgTextBase svgTextBase, SKRect viewport, ISvgAssetLoader assetLoader, List<float> dys)
    {
        for (var i = 0; i < svgTextBase.Dy.Count; i++)
        {
            dys.Add(ResolveTextUnitValue(svgTextBase.Dy[i], UnitRenderingType.VerticalOffset, svgTextBase, viewport, assetLoader));
        }
    }

    private static float ResolveTextUnitValue(
        SvgUnit unit,
        UnitRenderingType renderingType,
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader)
    {
        return unit.Type switch
        {
            SvgUnitType.Em => GetTextFontSize(svgTextBase, viewport) * unit.Value,
            SvgUnitType.Ex => ResolveTextXHeight(svgTextBase, viewport, assetLoader) * unit.Value,
            _ => unit.ToDeviceValue(renderingType, svgTextBase, viewport)
        };
    }

    private static float GetTextFontSize(SvgTextBase svgTextBase, SKRect viewport)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, viewport, paint);
        return paint.TextSize;
    }

    private static float ResolveTextXHeight(SvgTextBase svgTextBase, SKRect viewport, ISvgAssetLoader assetLoader)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, viewport, paint);
        paint.TextAlign = SKTextAlign.Left;

        if (TryGetRenderedTextLocalBounds("x", paint, assetLoader, out var xBounds) && !xBounds.IsEmpty)
        {
            return Math.Max(0f, xBounds.Height);
        }

        var metrics = assetLoader.GetFontMetrics(paint);
        return Math.Max(0f, paint.TextSize * 0.5f + Math.Min(0f, metrics.Ascent * 0.1f));
    }

    private static bool TryCreatePositionedCodepointPoints(
        SvgTextBase svgTextBase,
        string text,
        IReadOnlyList<float> xs,
        IReadOnlyList<float> ys,
        IReadOnlyList<float> dxs,
        IReadOnlyList<float> dys,
        float initialX,
        float initialY,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        float[]? rotations,
        out SKPoint[] points)
    {
        var hasPerGlyphPositioning = xs.Count > 1 || ys.Count > 1 || dxs.Count > 1 || dys.Count > 1;
        if (string.IsNullOrEmpty(text) ||
            !hasPerGlyphPositioning)
        {
            points = Array.Empty<SKPoint>();
            return false;
        }

        var codepoints = SplitCodepointsReadOnly(text);
        var codepointCount = codepoints.Count;
        if (codepointCount == 0)
        {
            points = Array.Empty<SKPoint>();
            return false;
        }

        points = new SKPoint[codepointCount];
        var useContextualAdvances = xs.Count <= 1;
        var naturalAdvances = useContextualAdvances
            ? MeasureNaturalCodepointAdvances(svgTextBase, text, codepoints, geometryBounds, assetLoader)
            : null;
        var currentX = initialX;
        var currentY = initialY;
        var baselineShift = GetBaselineShiftVector(svgTextBase, geometryBounds, assetLoader);
        for (var i = 0; i < codepointCount; i++)
        {
            var resetX = i < xs.Count;
            var resetY = i < ys.Count;
            if (i < xs.Count)
            {
                currentX = xs[i];
            }

            if (i < ys.Count)
            {
                currentY = ys[i];
            }

            if (i < dxs.Count)
            {
                currentX += dxs[i];
            }

            if (i < dys.Count)
            {
                currentY += dys[i];
            }

            if (resetX)
            {
                currentX += baselineShift.X;
            }

            if (resetY)
            {
                currentY += baselineShift.Y;
            }

            points[i] = new SKPoint(currentX, currentY);
            var inlineAdvance = naturalAdvances is not null
                ? naturalAdvances[i]
                : MeasureTextAdvance(svgTextBase, codepoints[i], geometryBounds, assetLoader);
            ApplyInlineAdvance(svgTextBase, ref currentX, ref currentY, inlineAdvance);
        }

        return true;
    }

    private static PositionedCodepointPlacement[] CreatePositionedCodepointPlacements(
        SvgTextBase svgTextBase,
        string text,
        SKPoint[] points,
        float[]? rotations)
    {
        var codepoints = SplitCodepointsReadOnly(text);
        if (points.Length == 0 || codepoints.Count == 0)
        {
            return Array.Empty<PositionedCodepointPlacement>();
        }

        var placements = new PositionedCodepointPlacement[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            var codepoint = i < codepoints.Count ? codepoints[i] : string.Empty;
            placements[i] = new PositionedCodepointPlacement(points[i], GetCodepointRotationDegrees(svgTextBase, codepoint, rotations, i), 1f, points[i].X);
        }

        return placements;
    }

    private static void MeasurePositionedCodepoints(
        string text,
        PositionedCodepointPlacement[] placements,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds,
        ref int placementIndex,
        ref float advance)
    {
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            var glyphBounds = new SKRect();
            var glyphAdvance = assetLoader.MeasureText(codepoint, paint, ref glyphBounds);
            var metrics = assetLoader.GetFontMetrics(paint);
            var placement = placements[placementIndex++];
            var candidate = glyphBounds.IsEmpty
                ? new SKRect(placement.Point.X, placement.Point.Y + metrics.Ascent, placement.Point.X + glyphAdvance, placement.Point.Y + metrics.Descent)
                : new SKRect(placement.Point.X + glyphBounds.Left, placement.Point.Y + glyphBounds.Top, placement.Point.X + glyphBounds.Right, placement.Point.Y + glyphBounds.Bottom);
            UnionBounds(ref bounds, RotateBounds(candidate, placement.Point, placement.RotationDegrees));
            advance = glyphAdvance;
        }
    }

    private static int CountCodepoints(string text)
    {
        return text.Length - CountLowSurrogates(text);
    }

    private static int GetLastCodepointStart(string text)
    {
        return text.Length - (char.IsLowSurrogate(text[text.Length - 1]) ? 2 : 1);
    }

    private static bool TryReadNextCodepoint(string text, ref int charIndex, out string codepoint)
    {
        if (charIndex >= text.Length)
        {
            codepoint = string.Empty;
            return false;
        }

        var start = charIndex++;
        if (text[start] < 0x80)
        {
            codepoint = s_asciiCodepointStrings[text[start]];
            return true;
        }

        if (charIndex < text.Length && char.IsHighSurrogate(text[start]) && char.IsLowSurrogate(text[charIndex]))
        {
            charIndex++;
        }

        codepoint = text.Substring(start, charIndex - start);
        return true;
    }

    private static string[] CreateAsciiCodepointStrings()
    {
        var values = new string[128];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = new string((char)i, 1);
        }

        return values;
    }

    private static int CountLowSurrogates(string text)
    {
        var count = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsLowSurrogate(text[i]))
            {
                count++;
            }
        }

        return count;
    }

    private static float[]? GetPositionedRotations(SvgTextBase svgTextBase, int codepointCount)
    {
        if (codepointCount <= 0)
        {
            return null;
        }

        if (TryParseRotateValues(svgTextBase, out var rotations))
        {
            return ExpandRotateValues(rotations!, codepointCount);
        }

        for (SvgElement? current = svgTextBase.Parent; current is not null; current = current.Parent)
        {
            if (current is SvgTextBase textBase &&
                TryParseRotateValues(textBase, out rotations))
            {
                return ExpandRotateValues(rotations!, codepointCount);
            }
        }

        return null;
    }

    private static bool TryGetUniformRunRotation(SvgTextBase svgTextBase, string text, float[]? rotations, out float rotation)
    {
        rotation = 0f;
        if (string.IsNullOrEmpty(text) ||
            rotations is null ||
            rotations.Length == 0 ||
            IsVerticalWritingMode(svgTextBase) ||
            HasMultipleTextPositionValues(svgTextBase) ||
            HasEffectiveSpacingAdjustments(svgTextBase, text) ||
            HasOwnTextLengthAdjustment(svgTextBase) ||
            HasNonBaselineShift(svgTextBase) ||
            !ContainsCursiveTrackingCodepoint(text))
        {
            return false;
        }

        rotation = rotations[0];
        if (NearlyEquals(rotation, 0f))
        {
            return false;
        }

        for (var i = 1; i < rotations.Length; i++)
        {
            if (!NearlyEquals(rotations[i], rotation))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasMultipleTextPositionValues(SvgTextBase svgTextBase)
    {
        return svgTextBase.X.Count > 1 ||
               svgTextBase.Y.Count > 1 ||
               svgTextBase.Dx.Count > 1 ||
               svgTextBase.Dy.Count > 1;
    }

    private static bool TryParseRotateValues(SvgTextBase svgTextBase, out float[]? values)
    {
        values = null;
        if (string.IsNullOrWhiteSpace(svgTextBase.Rotate))
        {
            return false;
        }

        var tokens = svgTextBase.Rotate.Split(new[] { ',', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        var parsed = new List<float>(tokens.Length);
        for (var i = 0; i < tokens.Length; i++)
        {
            if (TryParseRotateToken(tokens[i], out var rotation))
            {
                parsed.Add(rotation);
            }
        }

        if (parsed.Count == 0)
        {
            return false;
        }

        values = parsed.ToArray();
        return true;
    }

    private static bool TryParseRotateToken(string token, out float value)
    {
        if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        var match = s_numberPrefix.Match(token);
        return match.Success &&
               float.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static float[] ExpandRotateValues(float[] values, int codepointCount)
    {
        var rotations = new float[codepointCount];
        var lastRotation = values[0];
        for (var i = 0; i < codepointCount; i++)
        {
            if (i < values.Length)
            {
                lastRotation = values[i];
            }

            rotations[i] = lastRotation;
        }

        return rotations;
    }

    private static float GetRotationDegrees(float[]? rotations, int index)
    {
        return rotations is not null && index >= 0 && index < rotations.Length
            ? rotations[index]
            : 0f;
    }

    private static RotationState? ResolveRotationState(SvgTextBase svgTextBase, RotationState? inheritedRotationState)
    {
        return TryParseRotateValues(svgTextBase, out var values)
            ? new RotationState(values!)
            : inheritedRotationState;
    }

    private static AbsolutePositionState? ResolveAbsolutePositionState(SvgTextBase svgTextBase, AbsolutePositionState? inheritedAbsolutePositionState, SKRect viewport)
    {
        float[]? inheritedXs = inheritedAbsolutePositionState?.GetRemainingXValues();
        float[]? inheritedYs = inheritedAbsolutePositionState?.GetRemainingYValues();
        var ownXs = CreateAbsolutePositionArray(svgTextBase.X, UnitRenderingType.HorizontalOffset, svgTextBase, viewport);
        var ownYs = CreateAbsolutePositionArray(svgTextBase.Y, UnitRenderingType.VerticalOffset, svgTextBase, viewport);

        var effectiveXs = ownXs ?? inheritedXs;
        var effectiveYs = ownYs ?? inheritedYs;
        if (ownXs is not null && inheritedXs is not null && ownXs.Length < inheritedXs.Length)
        {
            effectiveXs = new float[inheritedXs.Length];
            Array.Copy(ownXs, effectiveXs, ownXs.Length);
            Array.Copy(inheritedXs, ownXs.Length, effectiveXs, ownXs.Length, inheritedXs.Length - ownXs.Length);
        }

        if (ownYs is not null && inheritedYs is not null && ownYs.Length < inheritedYs.Length)
        {
            effectiveYs = new float[inheritedYs.Length];
            Array.Copy(ownYs, effectiveYs, ownYs.Length);
            Array.Copy(inheritedYs, ownYs.Length, effectiveYs, ownYs.Length, inheritedYs.Length - ownYs.Length);
        }

        var state = new AbsolutePositionState(effectiveXs, effectiveYs);
        return state.HasAnyPositions ? state : null;
    }

    private static float[]? CreateAbsolutePositionArray(SvgUnitCollection units, UnitRenderingType renderingType, SvgTextBase svgTextBase, SKRect viewport)
    {
        if (units.Count == 0)
        {
            return null;
        }

        var values = new float[units.Count];
        for (var i = 0; i < units.Count; i++)
        {
            values[i] = units[i].ToDeviceValue(renderingType, svgTextBase, viewport);
        }

        return values;
    }

    private static float[]? ConsumeRotations(RotationState? rotationState, string text)
    {
        return rotationState?.Consume(CountCodepoints(text));
    }

    private static void AdvanceInheritedRotationState(RotationState? inheritedRotationState, SvgTextSpan svgTextSpan, bool trimLeadingWhitespaceAtStart)
    {
        if (inheritedRotationState is null || !HasRotateValues(svgTextSpan))
        {
            return;
        }

        var consumedCodepoints = CountRenderedTextCodepoints(svgTextSpan, trimLeadingWhitespaceAtStart);
        if (consumedCodepoints > 0)
        {
            inheritedRotationState.Consume(consumedCodepoints);
        }
    }

    private static void AdvanceInheritedAbsolutePositionState(AbsolutePositionState? inheritedAbsolutePositionState, SvgTextBase svgTextBase, bool trimLeadingWhitespaceAtStart)
    {
        var consumedCodepoints = CountRenderedTextCodepoints(svgTextBase, trimLeadingWhitespaceAtStart);
        if (consumedCodepoints > 0)
        {
            inheritedAbsolutePositionState?.Consume(consumedCodepoints);
        }
    }

    private static void DrawPositionedLayout(
        SvgFontTextRenderer.SvgFontLayout svgFontLayout,
        SKPoint point,
        float rotationDegrees,
        SKPaint paint,
        SKCanvas canvas)
    {
        DrawPositionedLayout(svgFontLayout, new PositionedCodepointPlacement(point, rotationDegrees, 1f, point.X), paint, canvas);
    }

    private static void DrawPositionedLayout(
        SvgFontTextRenderer.SvgFontLayout svgFontLayout,
        PositionedCodepointPlacement placement,
        SKPaint paint,
        SKCanvas canvas)
    {
        if (placement.RotationDegrees == 0f && placement.ScaleX == 1f)
        {
            svgFontLayout.Draw(canvas, paint, placement.Point.X, placement.Point.Y);
            return;
        }

        canvas.Save();
        if (placement.RotationDegrees != 0f)
        {
            canvas.SetMatrix(SKMatrix.CreateRotationDegrees(placement.RotationDegrees, placement.Point.X, placement.Point.Y));
        }

        if (placement.ScaleX != 1f)
        {
            var scalePivot = GetScalePivot(placement);
            canvas.SetMatrix(SKMatrix.CreateScale(placement.ScaleX, 1f, scalePivot.X, scalePivot.Y));
        }

        svgFontLayout.Draw(canvas, paint, placement.Point.X, placement.Point.Y);
        canvas.Restore();
    }

    private static void DrawPositionedText(
        string text,
        SKPoint point,
        float rotationDegrees,
        SKPaint paint,
        SKCanvas canvas)
    {
        DrawPositionedText(text, new PositionedCodepointPlacement(point, rotationDegrees, 1f, point.X), paint, canvas);
    }

    private static void DrawPositionedText(
        string text,
        PositionedCodepointPlacement placement,
        SKPaint paint,
        SKCanvas canvas)
    {
        if (placement.RotationDegrees == 0f && placement.ScaleX == 1f)
        {
            canvas.DrawText(text, placement.Point.X, placement.Point.Y, paint);
            return;
        }

        canvas.Save();
        if (placement.RotationDegrees != 0f)
        {
            canvas.SetMatrix(SKMatrix.CreateRotationDegrees(placement.RotationDegrees, placement.Point.X, placement.Point.Y));
        }

        if (placement.ScaleX != 1f)
        {
            var scalePivot = GetScalePivot(placement);
            canvas.SetMatrix(SKMatrix.CreateScale(placement.ScaleX, 1f, scalePivot.X, scalePivot.Y));
        }

        canvas.DrawText(text, placement.Point.X, placement.Point.Y, paint);
        canvas.Restore();
    }

    private static void AppendPositionedLayoutPath(
        SKPath targetPath,
        SvgFontTextRenderer.SvgFontLayout svgFontLayout,
        SKPoint point,
        float rotationDegrees)
    {
        AppendPositionedLayoutPath(targetPath, svgFontLayout, new PositionedCodepointPlacement(point, rotationDegrees, 1f, point.X));
    }

    private static void AppendPositionedLayoutPath(
        SKPath targetPath,
        SvgFontTextRenderer.SvgFontLayout svgFontLayout,
        PositionedCodepointPlacement placement)
    {
        if (placement.RotationDegrees == 0f && placement.ScaleX == 1f)
        {
            svgFontLayout.AppendPath(targetPath, placement.Point.X, placement.Point.Y);
            return;
        }

        var rotatedPath = new SKPath();
        svgFontLayout.AppendPath(rotatedPath, placement.Point.X, placement.Point.Y);
        if (placement.ScaleX != 1f)
        {
            ScalePathX(rotatedPath, GetScalePivot(placement), placement.ScaleX);
        }

        if (placement.RotationDegrees != 0f)
        {
            RotatePath(rotatedPath, placement.Point, placement.RotationDegrees);
        }

        AppendPathCommands(targetPath, rotatedPath);
    }

    private static void AppendPositionedTextPath(
        SKPath targetPath,
        string text,
        SKPoint point,
        float rotationDegrees,
        SKPaint paint,
        ISvgAssetLoader assetLoader)
    {
        AppendPositionedTextPath(targetPath, text, new PositionedCodepointPlacement(point, rotationDegrees, 1f, point.X), paint, assetLoader);
    }

    private static void AppendPositionedTextPath(
        SKPath targetPath,
        string text,
        PositionedCodepointPlacement placement,
        SKPaint paint,
        ISvgAssetLoader assetLoader)
    {
        var textPath = assetLoader.GetTextPath(text, paint, placement.Point.X, placement.Point.Y);
        if (textPath is null)
        {
            return;
        }

        if (placement.ScaleX != 1f)
        {
            ScalePathX(textPath, GetScalePivot(placement), placement.ScaleX);
        }

        if (placement.RotationDegrees != 0f)
        {
            RotatePath(textPath, placement.Point, placement.RotationDegrees);
        }

        AppendPathCommands(targetPath, textPath);
    }

    private static SKRect ScaleBoundsX(SKRect bounds, SKPoint pivot, float scaleX)
    {
        if (scaleX == 1f || bounds.IsEmpty)
        {
            return bounds;
        }

        var matrix = SKMatrix.CreateScale(scaleX, 1f, pivot.X, pivot.Y);
        return matrix.MapRect(bounds);
    }

    private static SKPoint GetScalePivot(PositionedCodepointPlacement placement)
    {
        return new SKPoint(placement.ScaleOriginX, placement.Point.Y);
    }

    private static SKRect RotateBounds(SKRect bounds, SKPoint pivot, float rotationDegrees)
    {
        if (rotationDegrees == 0f || bounds.IsEmpty)
        {
            return bounds;
        }

        var radians = rotationDegrees * ((float)Math.PI / 180f);
        var cos = (float)Math.Cos(radians);
        var sin = (float)Math.Sin(radians);

        var topLeft = RotatePoint(new SKPoint(bounds.Left, bounds.Top), pivot, cos, sin);
        var topRight = RotatePoint(new SKPoint(bounds.Right, bounds.Top), pivot, cos, sin);
        var bottomLeft = RotatePoint(new SKPoint(bounds.Left, bounds.Bottom), pivot, cos, sin);
        var bottomRight = RotatePoint(new SKPoint(bounds.Right, bounds.Bottom), pivot, cos, sin);

        var left = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
        var top = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
        var right = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
        var bottom = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));
        return new SKRect(left, top, right, bottom);
    }

    private static SKPoint RotatePoint(SKPoint point, SKPoint pivot, float cos, float sin)
    {
        var dx = point.X - pivot.X;
        var dy = point.Y - pivot.Y;
        return new SKPoint(
            pivot.X + (dx * cos) - (dy * sin),
            pivot.Y + (dx * sin) + (dy * cos));
    }

    private static void RotatePath(SKPath path, SKPoint pivot, float rotationDegrees)
    {
        if (rotationDegrees == 0f || path.Commands is null || path.Commands.Count == 0)
        {
            return;
        }

        var radians = rotationDegrees * ((float)Math.PI / 180f);
        var cos = (float)Math.Cos(radians);
        var sin = (float)Math.Sin(radians);

        for (var i = 0; i < path.Commands.Count; i++)
        {
            path.Commands[i] = path.Commands[i] switch
            {
                MoveToPathCommand moveTo => new MoveToPathCommand(
                    RotatePoint(new SKPoint(moveTo.X, moveTo.Y), pivot, cos, sin).X,
                    RotatePoint(new SKPoint(moveTo.X, moveTo.Y), pivot, cos, sin).Y),
                LineToPathCommand lineTo => new LineToPathCommand(
                    RotatePoint(new SKPoint(lineTo.X, lineTo.Y), pivot, cos, sin).X,
                    RotatePoint(new SKPoint(lineTo.X, lineTo.Y), pivot, cos, sin).Y),
                QuadToPathCommand quadTo => new QuadToPathCommand(
                    RotatePoint(new SKPoint(quadTo.X0, quadTo.Y0), pivot, cos, sin).X,
                    RotatePoint(new SKPoint(quadTo.X0, quadTo.Y0), pivot, cos, sin).Y,
                    RotatePoint(new SKPoint(quadTo.X1, quadTo.Y1), pivot, cos, sin).X,
                    RotatePoint(new SKPoint(quadTo.X1, quadTo.Y1), pivot, cos, sin).Y),
                CubicToPathCommand cubicTo => new CubicToPathCommand(
                    RotatePoint(new SKPoint(cubicTo.X0, cubicTo.Y0), pivot, cos, sin).X,
                    RotatePoint(new SKPoint(cubicTo.X0, cubicTo.Y0), pivot, cos, sin).Y,
                    RotatePoint(new SKPoint(cubicTo.X1, cubicTo.Y1), pivot, cos, sin).X,
                    RotatePoint(new SKPoint(cubicTo.X1, cubicTo.Y1), pivot, cos, sin).Y,
                    RotatePoint(new SKPoint(cubicTo.X2, cubicTo.Y2), pivot, cos, sin).X,
                    RotatePoint(new SKPoint(cubicTo.X2, cubicTo.Y2), pivot, cos, sin).Y),
                ArcToPathCommand arcTo => new ArcToPathCommand(
                    arcTo.Rx,
                    arcTo.Ry,
                    arcTo.XAxisRotate + rotationDegrees,
                    arcTo.LargeArc,
                    arcTo.Sweep,
                    RotatePoint(new SKPoint(arcTo.X, arcTo.Y), pivot, cos, sin).X,
                    RotatePoint(new SKPoint(arcTo.X, arcTo.Y), pivot, cos, sin).Y),
                AddPolyPathCommand poly => new AddPolyPathCommand(RotatePoints(poly.Points, pivot, cos, sin), poly.Close),
                AddCirclePathCommand circle => new AddCirclePathCommand(
                    RotatePoint(new SKPoint(circle.X, circle.Y), pivot, cos, sin).X,
                    RotatePoint(new SKPoint(circle.X, circle.Y), pivot, cos, sin).Y,
                    circle.Radius),
                _ => path.Commands[i]
            };
        }
    }

    private static void ScalePathX(SKPath path, SKPoint pivot, float scaleX)
    {
        if (scaleX == 1f || path.Commands is null || path.Commands.Count == 0)
        {
            return;
        }

        static float ScaleCoordinate(float value, float pivotCoordinate, float scale)
        {
            return pivotCoordinate + ((value - pivotCoordinate) * scale);
        }

        for (var i = 0; i < path.Commands.Count; i++)
        {
            path.Commands[i] = path.Commands[i] switch
            {
                MoveToPathCommand moveTo => new MoveToPathCommand(
                    ScaleCoordinate(moveTo.X, pivot.X, scaleX),
                    moveTo.Y),
                LineToPathCommand lineTo => new LineToPathCommand(
                    ScaleCoordinate(lineTo.X, pivot.X, scaleX),
                    lineTo.Y),
                QuadToPathCommand quadTo => new QuadToPathCommand(
                    ScaleCoordinate(quadTo.X0, pivot.X, scaleX),
                    quadTo.Y0,
                    ScaleCoordinate(quadTo.X1, pivot.X, scaleX),
                    quadTo.Y1),
                CubicToPathCommand cubicTo => new CubicToPathCommand(
                    ScaleCoordinate(cubicTo.X0, pivot.X, scaleX),
                    cubicTo.Y0,
                    ScaleCoordinate(cubicTo.X1, pivot.X, scaleX),
                    cubicTo.Y1,
                    ScaleCoordinate(cubicTo.X2, pivot.X, scaleX),
                    cubicTo.Y2),
                ArcToPathCommand arcTo => new ArcToPathCommand(
                    arcTo.Rx * Math.Abs(scaleX),
                    arcTo.Ry,
                    arcTo.XAxisRotate,
                    arcTo.LargeArc,
                    arcTo.Sweep,
                    ScaleCoordinate(arcTo.X, pivot.X, scaleX),
                    arcTo.Y),
                AddPolyPathCommand poly => new AddPolyPathCommand(ScalePointsX(poly.Points, pivot.X, scaleX), poly.Close),
                AddCirclePathCommand circle => new AddOvalPathCommand(SKRect.Create(
                    ScaleCoordinate(circle.X - circle.Radius, pivot.X, scaleX),
                    circle.Y - circle.Radius,
                    circle.Radius * 2f * Math.Abs(scaleX),
                    circle.Radius * 2f)),
                AddRectPathCommand rect => new AddRectPathCommand(SKRect.Create(
                    ScaleCoordinate(rect.Rect.Left, pivot.X, scaleX),
                    rect.Rect.Top,
                    rect.Rect.Width * Math.Abs(scaleX),
                    rect.Rect.Height)),
                AddRoundRectPathCommand roundRect => new AddRoundRectPathCommand(
                    SKRect.Create(
                        ScaleCoordinate(roundRect.Rect.Left, pivot.X, scaleX),
                        roundRect.Rect.Top,
                        roundRect.Rect.Width * Math.Abs(scaleX),
                        roundRect.Rect.Height),
                    roundRect.Rx * Math.Abs(scaleX),
                    roundRect.Ry),
                AddOvalPathCommand oval => new AddOvalPathCommand(SKRect.Create(
                    ScaleCoordinate(oval.Rect.Left, pivot.X, scaleX),
                    oval.Rect.Top,
                    oval.Rect.Width * Math.Abs(scaleX),
                    oval.Rect.Height)),
                _ => path.Commands[i]
            };
        }
    }

    private static void TranslatePath(SKPath path, float dx, float dy)
    {
        if ((Math.Abs(dx) <= 0.001f && Math.Abs(dy) <= 0.001f) ||
            path.Commands is null ||
            path.Commands.Count == 0)
        {
            return;
        }

        static SKPoint TranslatePoint(SKPoint point, float dx, float dy)
        {
            return new SKPoint(point.X + dx, point.Y + dy);
        }

        static IList<SKPoint>? TranslatePoints(IList<SKPoint>? points, float dx, float dy)
        {
            if (points is null)
            {
                return null;
            }

            var translated = new SKPoint[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                translated[i] = TranslatePoint(points[i], dx, dy);
            }

            return translated;
        }

        static SKRect TranslateRect(SKRect rect, float dx, float dy)
        {
            return new SKRect(rect.Left + dx, rect.Top + dy, rect.Right + dx, rect.Bottom + dy);
        }

        for (var i = 0; i < path.Commands.Count; i++)
        {
            path.Commands[i] = path.Commands[i] switch
            {
                MoveToPathCommand moveTo => new MoveToPathCommand(moveTo.X + dx, moveTo.Y + dy),
                LineToPathCommand lineTo => new LineToPathCommand(lineTo.X + dx, lineTo.Y + dy),
                QuadToPathCommand quadTo => new QuadToPathCommand(
                    quadTo.X0 + dx,
                    quadTo.Y0 + dy,
                    quadTo.X1 + dx,
                    quadTo.Y1 + dy),
                CubicToPathCommand cubicTo => new CubicToPathCommand(
                    cubicTo.X0 + dx,
                    cubicTo.Y0 + dy,
                    cubicTo.X1 + dx,
                    cubicTo.Y1 + dy,
                    cubicTo.X2 + dx,
                    cubicTo.Y2 + dy),
                ArcToPathCommand arcTo => new ArcToPathCommand(
                    arcTo.Rx,
                    arcTo.Ry,
                    arcTo.XAxisRotate,
                    arcTo.LargeArc,
                    arcTo.Sweep,
                    arcTo.X + dx,
                    arcTo.Y + dy),
                AddPolyPathCommand poly => new AddPolyPathCommand(TranslatePoints(poly.Points, dx, dy), poly.Close),
                AddCirclePathCommand circle => new AddCirclePathCommand(circle.X + dx, circle.Y + dy, circle.Radius),
                AddRectPathCommand rect => new AddRectPathCommand(TranslateRect(rect.Rect, dx, dy)),
                AddRoundRectPathCommand roundRect => new AddRoundRectPathCommand(TranslateRect(roundRect.Rect, dx, dy), roundRect.Rx, roundRect.Ry),
                AddOvalPathCommand oval => new AddOvalPathCommand(TranslateRect(oval.Rect, dx, dy)),
                _ => path.Commands[i]
            };
        }
    }

    private static IList<SKPoint>? RotatePoints(IList<SKPoint>? points, SKPoint pivot, float cos, float sin)
    {
        if (points is null)
        {
            return null;
        }

        var rotated = new List<SKPoint>(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            rotated.Add(RotatePoint(points[i], pivot, cos, sin));
        }

        return rotated;
    }

    private static IList<SKPoint>? ScalePointsX(IList<SKPoint>? points, float pivotX, float scaleX)
    {
        if (points is null)
        {
            return null;
        }

        var scaled = new List<SKPoint>(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            scaled.Add(new SKPoint(pivotX + ((points[i].X - pivotX) * scaleX), points[i].Y));
        }

        return scaled;
    }

    private static float GetAlignedStartCoordinate(float anchorCoordinate, float totalAdvance, SKTextAlign textAlign)
    {
        return textAlign switch
        {
            SKTextAlign.Center => anchorCoordinate - (totalAdvance * 0.5f),
            SKTextAlign.Right => anchorCoordinate - totalAdvance,
            _ => anchorCoordinate
        };
    }

    private static float GetAlignedStartX(float anchorX, float totalAdvance, SKTextAlign textAlign)
    {
        return GetAlignedStartCoordinate(anchorX, totalAdvance, textAlign);
    }

    private static void DrawResolvedTextDecorations(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        float[]? rotations,
        bool forceLeftAlign,
        SvgSceneContextPaint? contextPaint)
    {
        var decorationLayers = ResolveTextDecorationLayers(svgTextBase);
        if (decorationLayers.Count == 0)
        {
            return;
        }

        var alignmentPaint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, alignmentPaint);
        var textAlign = forceLeftAlign ? SKTextAlign.Left : alignmentPaint.TextAlign;

        var isVertical = IsVerticalWritingMode(svgTextBase);
        if ((isVertical || HasPerGlyphLayoutAdjustments(svgTextBase, text)) &&
            TryCreateAlignedCodepointPlacements(svgTextBase, text, anchorX, anchorY, geometryBounds, textAlign, assetLoader, rotations, out var placements, out _))
        {
            DrawTextDecorations(decorationLayers, svgTextBase, text, placements, geometryBounds, ignoreAttributes, canvas, assetLoader, contextPaint);
            return;
        }

        var totalAdvance = MeasureNaturalTextAdvance(svgTextBase, text, geometryBounds, assetLoader);
        if (totalAdvance <= 0f)
        {
            return;
        }

        var startX = forceLeftAlign ? anchorX : GetAlignedStartX(anchorX, totalAdvance, textAlign);
        DrawTextDecorations(decorationLayers, startX, anchorY, totalAdvance, geometryBounds, ignoreAttributes, canvas, assetLoader, contextPaint);
    }

    private static void DrawTextDecorations(
        IReadOnlyList<TextDecorationLayer> decorationLayers,
        float startX,
        float baselineY,
        float advance,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        SvgSceneContextPaint? contextPaint)
    {
        if (advance <= 0f || decorationLayers.Count == 0)
        {
            return;
        }

        for (var i = 0; i < decorationLayers.Count; i++)
        {
            DrawTextDecorationLayer(decorationLayers[i], startX, baselineY, advance, geometryBounds, ignoreAttributes, canvas, assetLoader, contextPaint);
        }
    }

    private static void DrawTextDecorations(
        IReadOnlyList<TextDecorationLayer> decorationLayers,
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement[] placements,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        SvgSceneContextPaint? contextPaint)
    {
        if (placements.Length == 0 || decorationLayers.Count == 0)
        {
            return;
        }

        if (HasLinearDecorations(placements))
        {
            var decorationBounds = MeasureCodepointPlacementBounds(svgTextBase, text, placements, geometryBounds, assetLoader, out _);
            var totalAdvance = decorationBounds.Right - decorationBounds.Left;
            if (!decorationBounds.IsEmpty && totalAdvance > 0f)
            {
                DrawTextDecorations(decorationLayers, decorationBounds.Left, placements[0].Point.Y, totalAdvance, geometryBounds, ignoreAttributes, canvas, assetLoader, contextPaint);
            }

            return;
        }

        var codepoints = SplitCodepointsReadOnly(text);
        if (codepoints.Count == 0 || codepoints.Count != placements.Length)
        {
            return;
        }

        for (var layerIndex = 0; layerIndex < decorationLayers.Count; layerIndex++)
        {
            DrawPositionedTextDecorationLayer(decorationLayers[layerIndex], svgTextBase, text, placements, geometryBounds, ignoreAttributes, canvas, assetLoader, contextPaint);
        }
    }

    private static SKPoint TransformDecorationPoint(PositionedCodepointPlacement placement, float offsetX, float offsetY)
    {
        var point = new SKPoint(placement.Point.X + offsetX, placement.Point.Y + offsetY);
        if (placement.ScaleX != 1f)
        {
            var scalePivot = GetScalePivot(placement);
            var scaleMatrix = SKMatrix.CreateScale(placement.ScaleX, 1f, scalePivot.X, scalePivot.Y);
            point = scaleMatrix.MapPoint(point);
        }

        if (placement.RotationDegrees == 0f)
        {
            return point;
        }

        var radians = placement.RotationDegrees * ((float)Math.PI / 180f);
        return RotatePoint(point, placement.Point, (float)Math.Cos(radians), (float)Math.Sin(radians));
    }

    private static IReadOnlyList<TextDecorationLayer> ResolveTextDecorationLayers(SvgTextBase svgTextBase)
    {
        var layers = new Stack<TextDecorationLayer>();
        for (SvgElement? current = svgTextBase; current is not null; current = current.Parent)
        {
            if (current is not SvgVisualElement ||
                !TryGetOwnTextDecoration(current, out var decorations))
            {
                continue;
            }

            if (!ShouldApplyDecorationLayer(svgTextBase, current))
            {
                continue;
            }

            var paintSource = ResolveDecorationPaintSource(svgTextBase, current);
            var metricsSource = svgTextBase;
            layers.Push(new TextDecorationLayer(paintSource, metricsSource, decorations));
        }

        return layers.Count > 0
            ? layers.ToList()
            : Array.Empty<TextDecorationLayer>();
    }

    private static bool HasTextDecorationLayers(SvgTextBase svgTextBase)
    {
        for (SvgElement? current = svgTextBase; current is not null; current = current.Parent)
        {
            if (current is SvgVisualElement &&
                TryGetOwnTextDecoration(current, out _) &&
                ShouldApplyDecorationLayer(svgTextBase, current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldApplyDecorationLayer(SvgTextBase leafTextBase, SvgElement decorator)
    {
        return true;
    }

    private static SvgTextBase ResolveDecorationPaintSource(SvgTextBase leafTextBase, SvgElement decorator)
    {
        if (decorator is SvgTextBase decoratorTextBase)
        {
            return decoratorTextBase;
        }

        for (SvgElement? current = leafTextBase; current is not null && !ReferenceEquals(current, decorator); current = current.Parent)
        {
            if (current is SvgTextBase textBase &&
                textBase is not SvgTextSpan)
            {
                return textBase;
            }
        }

        return leafTextBase;
    }

    private static bool TryGetOwnTextDecoration(SvgElement element, out SvgTextDecoration decorations)
    {
        decorations = SvgTextDecoration.None;
        if (element.CustomAttributes.TryGetValue(SvgStyleAttributeNames.RawTextDecorationAttributeKey, out var rawValue))
        {
            return TryParseTextDecorationValue(rawValue, out decorations) &&
                   HasRenderableDecorations(decorations);
        }

        return element.TryGetAttribute(TextDecorationAttributeName, out var attributeValue) &&
               (TryParseTextDecorationValue(attributeValue, out decorations) ||
                TryParseTextDecorationEnumValue(attributeValue, out decorations)) &&
               HasRenderableDecorations(decorations);
    }

    private static bool TryParseTextDecorationValue(string? rawValue, out SvgTextDecoration decorations)
    {
        decorations = SvgTextDecoration.None;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        if (rawValue!.IndexOf(',') >= 0)
        {
            return false;
        }

        var tokens = rawValue
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        if (tokens.Length == 1 && string.Equals(tokens[0].Trim(), "inherit", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i].Trim();
            if (string.Equals(token, "none", StringComparison.OrdinalIgnoreCase))
            {
                decorations = SvgTextDecoration.None;
                return tokens.Length == 1;
            }

            if (string.Equals(token, "inherit", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(token, "underline", StringComparison.OrdinalIgnoreCase))
            {
                decorations |= SvgTextDecoration.Underline;
                continue;
            }

            if (string.Equals(token, "overline", StringComparison.OrdinalIgnoreCase))
            {
                decorations |= SvgTextDecoration.Overline;
                continue;
            }

            if (string.Equals(token, "line-through", StringComparison.OrdinalIgnoreCase))
            {
                decorations |= SvgTextDecoration.LineThrough;
                continue;
            }

            if (string.Equals(token, "blink", StringComparison.OrdinalIgnoreCase))
            {
                decorations |= SvgTextDecoration.Blink;
                continue;
            }

            return false;
        }

        return decorations != SvgTextDecoration.None;
    }

    private static bool TryParseTextDecorationEnumValue(string? rawValue, out SvgTextDecoration decorations)
    {
        decorations = SvgTextDecoration.None;
        var value = rawValue?.Trim();
        if (string.IsNullOrEmpty(value) ||
            char.IsDigit(value![0]) ||
            value[0] == '+' ||
            value[0] == '-')
        {
            return false;
        }

        return Enum.TryParse(value, ignoreCase: true, out decorations) &&
               !decorations.HasFlag(SvgTextDecoration.None);
    }

    private static bool HasRenderableDecorations(SvgTextDecoration decorations)
    {
        return decorations.HasFlag(SvgTextDecoration.Underline) ||
               decorations.HasFlag(SvgTextDecoration.Overline) ||
               decorations.HasFlag(SvgTextDecoration.LineThrough);
    }

    private static void DrawTextDecorationLayer(
        TextDecorationLayer layer,
        float startX,
        float baselineY,
        float advance,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        SvgSceneContextPaint? contextPaint)
    {
        if (advance <= 0f)
        {
            return;
        }

        var metricsPaint = CreateTextMetricsPaint(layer.MetricsSource, geometryBounds);
        var metrics = assetLoader.GetFontMetrics(metricsPaint);
        var fillPaint = SvgScenePaintingService.IsValidFill(layer.PaintSource)
            ? SvgScenePaintingService.GetFillPaint(layer.PaintSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint)
            : null;
        var strokePaint = SvgScenePaintingService.IsValidStroke(layer.PaintSource, geometryBounds)
            ? SvgScenePaintingService.GetStrokePaint(layer.PaintSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint)
            : null;

        if (fillPaint is null && strokePaint is null)
        {
            return;
        }

        DrawLinearDecorationKinds(layer, startX, baselineY, advance, metricsPaint, metrics, fillPaint, strokePaint, canvas);
    }

    private static void DrawPositionedTextDecorationLayer(
        TextDecorationLayer layer,
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement[] placements,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        SvgSceneContextPaint? contextPaint)
    {
        var metricsPaint = CreateTextMetricsPaint(layer.MetricsSource, geometryBounds);
        var metrics = assetLoader.GetFontMetrics(metricsPaint);
        var fillPaint = SvgScenePaintingService.IsValidFill(layer.PaintSource)
            ? SvgScenePaintingService.GetFillPaint(layer.PaintSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint)
            : null;
        var strokePaint = SvgScenePaintingService.IsValidStroke(layer.PaintSource, geometryBounds)
            ? SvgScenePaintingService.GetStrokePaint(layer.PaintSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint)
            : null;

        if ((fillPaint is null && strokePaint is null) || placements.Length == 0)
        {
            return;
        }

        var codepoints = SplitCodepointsReadOnly(text);
        if (codepoints.Count == 0 || codepoints.Count != placements.Length)
        {
            return;
        }

        var naturalAdvances = MeasureNaturalCodepointAdvances(svgTextBase, text, codepoints, geometryBounds, assetLoader);
        var fallbackResolver = GetFallbackCodepointResolver(svgTextBase);
        for (var placementIndex = 0; placementIndex < placements.Length; placementIndex++)
        {
            var placement = placements[placementIndex];
            var leftOffset = 0f;
            var rightOffset = placementIndex < naturalAdvances.Length ? naturalAdvances[placementIndex] : 0f;

            if (!IsValidPositiveAdvance(rightOffset) &&
                !TryGetCodepointDecorationExtents(svgTextBase, codepoints[placementIndex], placement, metricsPaint, assetLoader, fallbackResolver, out leftOffset, out rightOffset))
            {
                continue;
            }

            if (rightOffset <= leftOffset)
            {
                continue;
            }

            DrawPositionedDecorationKinds(layer, placement, leftOffset, rightOffset, metricsPaint, metrics, fillPaint, strokePaint, canvas);
        }
    }

    private static void DrawLinearDecorationKinds(
        TextDecorationLayer layer,
        float startX,
        float baselineY,
        float advance,
        SKPaint metricsPaint,
        SKFontMetrics metrics,
        SKPaint? fillPaint,
        SKPaint? strokePaint,
        SKCanvas canvas)
    {
        if (layer.Decorations.HasFlag(SvgTextDecoration.Overline) &&
            TryCreateLinearDecorationPath(startX, baselineY, advance, metricsPaint, metrics, SvgTextDecoration.Overline, out var overlinePath))
        {
            DrawDecorationPath(overlinePath, layer.PaintSource, fillPaint, strokePaint, canvas);
        }

        if (layer.Decorations.HasFlag(SvgTextDecoration.LineThrough) &&
            TryCreateLinearDecorationPath(startX, baselineY, advance, metricsPaint, metrics, SvgTextDecoration.LineThrough, out var lineThroughPath))
        {
            DrawDecorationPath(lineThroughPath, layer.PaintSource, fillPaint, strokePaint, canvas);
        }

        if (layer.Decorations.HasFlag(SvgTextDecoration.Underline) &&
            TryCreateLinearDecorationPath(startX, baselineY, advance, metricsPaint, metrics, SvgTextDecoration.Underline, out var underlinePath))
        {
            DrawDecorationPath(underlinePath, layer.PaintSource, fillPaint, strokePaint, canvas);
        }
    }

    private static void DrawPositionedDecorationKinds(
        TextDecorationLayer layer,
        PositionedCodepointPlacement placement,
        float leftOffset,
        float rightOffset,
        SKPaint metricsPaint,
        SKFontMetrics metrics,
        SKPaint? fillPaint,
        SKPaint? strokePaint,
        SKCanvas canvas)
    {
        if (layer.Decorations.HasFlag(SvgTextDecoration.Overline) &&
            TryCreatePositionedDecorationPath(placement, leftOffset, rightOffset, metricsPaint, metrics, SvgTextDecoration.Overline, out var overlinePath))
        {
            DrawDecorationPath(overlinePath, layer.PaintSource, fillPaint, strokePaint, canvas);
        }

        if (layer.Decorations.HasFlag(SvgTextDecoration.LineThrough) &&
            TryCreatePositionedDecorationPath(placement, leftOffset, rightOffset, metricsPaint, metrics, SvgTextDecoration.LineThrough, out var lineThroughPath))
        {
            DrawDecorationPath(lineThroughPath, layer.PaintSource, fillPaint, strokePaint, canvas);
        }

        if (layer.Decorations.HasFlag(SvgTextDecoration.Underline) &&
            TryCreatePositionedDecorationPath(placement, leftOffset, rightOffset, metricsPaint, metrics, SvgTextDecoration.Underline, out var underlinePath))
        {
            DrawDecorationPath(underlinePath, layer.PaintSource, fillPaint, strokePaint, canvas);
        }
    }

    private static bool TryCreateLinearDecorationPath(
        float startX,
        float baselineY,
        float advance,
        SKPaint metricsPaint,
        SKFontMetrics metrics,
        SvgTextDecoration decorationKind,
        out SKPath path)
    {
        path = new SKPath();
        if (!TryGetDecorationBand(metricsPaint, metrics, decorationKind, out var topOffset, out var bottomOffset))
        {
            return false;
        }

        var top = baselineY + topOffset;
        var bottom = baselineY + bottomOffset;
        var rectTop = Math.Min(top, bottom);
        var rectBottom = Math.Max(top, bottom);
        var height = rectBottom - rectTop;
        if (advance <= 0f || height <= 0f)
        {
            return false;
        }

        path.AddRect(SKRect.Create(startX, rectTop, advance, height));
        return true;
    }

    private static bool TryCreatePositionedDecorationPath(
        PositionedCodepointPlacement placement,
        float leftOffset,
        float rightOffset,
        SKPaint metricsPaint,
        SKFontMetrics metrics,
        SvgTextDecoration decorationKind,
        out SKPath path)
    {
        path = new SKPath();
        if (!TryGetDecorationBand(metricsPaint, metrics, decorationKind, out var topOffset, out var bottomOffset))
        {
            return false;
        }

        var points = new[]
        {
            TransformDecorationPoint(placement, leftOffset, topOffset),
            TransformDecorationPoint(placement, rightOffset, topOffset),
            TransformDecorationPoint(placement, rightOffset, bottomOffset),
            TransformDecorationPoint(placement, leftOffset, bottomOffset)
        };
        path.AddPoly(points, close: true);
        return true;
    }

    private static void DrawDecorationPath(
        SKPath path,
        SvgVisualElement paintOrderSource,
        SKPaint? fillPaint,
        SKPaint? strokePaint,
        SKCanvas canvas)
    {
        _ = DrawTextPaintOrder(paintOrderSource, fillPaint is not null, strokePaint is not null, includeDecorations: false, phase =>
        {
            switch (phase)
            {
                case TextPaintPhase.Fill:
                    canvas.DrawPath(path, fillPaint!);
                    break;

                case TextPaintPhase.Stroke:
                    canvas.DrawPath(path, strokePaint!);
                    break;
            }

            return 0f;
        });
    }

    private static bool TryGetDecorationBand(
        SKPaint metricsPaint,
        SKFontMetrics metrics,
        SvgTextDecoration decorationKind,
        out float topOffset,
        out float bottomOffset)
    {
        topOffset = 0f;
        bottomOffset = 0f;

        var fallbackThickness = Math.Max(1f, metricsPaint.TextSize * 0.05f);
        switch (decorationKind)
        {
            case SvgTextDecoration.Overline:
                {
                    var thickness = GetDecorationThickness(metrics.UnderlineThickness, fallbackThickness);
                    var center = metrics.Ascent;
                    topOffset = center - (thickness * 0.5f);
                    bottomOffset = center + (thickness * 0.5f);
                    return true;
                }
            case SvgTextDecoration.LineThrough:
                {
                    var thickness = GetDecorationThickness(metrics.StrikeoutThickness, fallbackThickness);
                    var center = metrics.StrikeoutPosition.GetValueOrDefault((metrics.Ascent + metrics.Descent) * 0.35f);
                    topOffset = center - (thickness * 0.5f);
                    bottomOffset = center + (thickness * 0.5f);
                    return true;
                }
            case SvgTextDecoration.Underline:
                {
                    var thickness = GetDecorationThickness(metrics.UnderlineThickness, fallbackThickness);
                    var center = metrics.UnderlinePosition.GetValueOrDefault(Math.Max(metrics.Descent * 0.5f, metricsPaint.TextSize * 0.08f));
                    topOffset = center - (thickness * 0.5f);
                    bottomOffset = center + (thickness * 0.5f);
                    return true;
                }
            default:
                return false;
        }
    }

    private static float GetDecorationThickness(float? explicitThickness, float fallbackThickness)
    {
        var thickness = explicitThickness.GetValueOrDefault();
        return thickness > 0f ? thickness : fallbackThickness;
    }

    private static SKPaint CreateTextMetricsPaint(SvgTextBase svgTextBase, SKRect geometryBounds)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;
        return paint;
    }

    private static IEnumerable<ISvgNode> GetContentNodes(SvgElement element)
    {
        if (element.Nodes is null || element.Nodes.Count < 1)
        {
            foreach (var child in element.Children)
            {
                if (child is ISvgNode svgNode &&
                    child is not ISvgDescriptiveElement &&
                    child is not NonSvgElement)
                {
                    yield return svgNode;
                }
            }
        }
        else
        {
            foreach (var node in element.Nodes)
            {
                if (node is NonSvgElement)
                {
                    continue;
                }

                yield return node;
            }
        }
    }

    private static IReadOnlyList<ISvgNode> GetContentNodeList(SvgElement element)
    {
        if (element.Nodes is { Count: > 0 } nodes)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is NonSvgElement)
                {
                    var filteredNodes = new List<ISvgNode>(nodes.Count - 1);
                    for (var j = 0; j < nodes.Count; j++)
                    {
                        if (nodes[j] is not NonSvgElement)
                        {
                            filteredNodes.Add(nodes[j]);
                        }
                    }

                    return filteredNodes;
                }
            }

            return nodes as IReadOnlyList<ISvgNode> ?? nodes.ToList();
        }

        if (element.Children.Count == 0)
        {
            return Array.Empty<ISvgNode>();
        }

        var contentNodes = new List<ISvgNode>(element.Children.Count);
        foreach (var child in element.Children)
        {
            if (child is ISvgNode svgNode &&
                child is not ISvgDescriptiveElement &&
                child is not NonSvgElement)
            {
                contentNodes.Add(svgNode);
            }
        }

        return contentNodes;
    }

    private static IReadOnlyList<ISvgNode> ToContentNodeList(IEnumerable<ISvgNode> contentNodes)
    {
        return contentNodes as IReadOnlyList<ISvgNode> ?? contentNodes.ToList();
    }

    private static bool IsEmptyAltGlyph(SvgAltGlyph altGlyph)
    {
        var contentNodes = GetContentNodeList(altGlyph);
        for (var i = 0; i < contentNodes.Count; i++)
        {
            if (contentNodes[i] is SvgTextBase ||
                !string.IsNullOrEmpty(contentNodes[i].Content))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryDrawSequentialTextRuns(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        bool trimLeadingWhitespaceAtStart,
        SvgSceneContextPaint? contextPaint)
    {
        var hasInlineSizeLayout = HasInlineSizeLayout(svgTextBase);
        if (!hasInlineSizeLayout &&
            HasPreparedSequentialTextContainerBarriers(svgTextBase))
        {
            return false;
        }

        if (hasInlineSizeLayout &&
            TryDrawInlineSizeTextPathFragment(
                svgTextBase,
                ref currentX,
                ref currentY,
                viewport,
                geometryBounds,
                ignoreAttributes,
                canvas,
                assetLoader,
                getElementAddressKey,
                contextPaint))
        {
            return true;
        }

        var preservePreLineBreaks = hasInlineSizeLayout && PreservesInlineLineBreaksInTextSubtree(svgTextBase);
        if (!TryCollectSequentialTextRuns(svgTextBase, requireAnchorContent: false, IsTextReferenceRenderingEnabled(assetLoader), trimLeadingWhitespaceAtStart, out var runs, preservePreLineBreaks))
        {
            return false;
        }

        if (TryDrawInlineSizeTextOverflowRuns(
                svgTextBase,
                runs,
                ref currentX,
                ref currentY,
                viewport,
                geometryBounds,
                ignoreAttributes,
                canvas,
                assetLoader,
                getElementAddressKey,
                contextPaint))
        {
            return true;
        }

        if (hasInlineSizeLayout &&
            HasPreparedSequentialTextContainerBarriers(svgTextBase))
        {
            return false;
        }

        if (preservePreLineBreaks &&
            !TryCollectSequentialTextRuns(svgTextBase, requireAnchorContent: false, IsTextReferenceRenderingEnabled(assetLoader), trimLeadingWhitespaceAtStart, out runs))
        {
            return false;
        }

        if (TryDrawShapedSequentialTextRuns(svgTextBase, runs, ref currentX, ref currentY, viewport, geometryBounds, ignoreAttributes, canvas, assetLoader, getElementAddressKey, contextPaint))
        {
            return true;
        }

        ApplyInitialSequentialOffsets(svgTextBase, viewport, ref currentX, ref currentY);
        var isVertical = IsVerticalWritingMode(svgTextBase);
        var textAlign = GetTextAnchorAlign(svgTextBase, geometryBounds);
        if (textAlign == SKTextAlign.Left)
        {
            var startAlignedX = currentX;
            var startAlignedY = currentY;
            for (var i = 0; i < runs.Count; i++)
            {
                DrawTextStringAlignedLeft(runs[i].StyleSource, runs[i].Text, ref startAlignedX, ref startAlignedY, geometryBounds, ignoreAttributes, canvas, assetLoader, getElementAddressKey, contextPaint);
                ApplyInlineAdvance(runs[i].StyleSource, ref startAlignedX, ref startAlignedY, GetSequentialRunBoundaryAdvance(runs, i, geometryBounds));
            }

            currentX = startAlignedX;
            currentY = startAlignedY;
            return true;
        }

        if (!TryPrepareSequentialTextRuns(runs, geometryBounds, assetLoader, out var preparedText) ||
            preparedText is null)
        {
            return false;
        }

        var totalAdvance = preparedText.TotalAdvance;
        var inlineOrigin = isVertical
            ? GetVerticalInlineStartCoordinate(svgTextBase, currentY, totalAdvance, textAlign)
            : GetAlignedStartCoordinate(currentX, totalAdvance, textAlign);
        var drawX = isVertical ? currentX : inlineOrigin;
        var drawY = isVertical ? inlineOrigin : currentY;

        for (var i = 0; i < preparedText.Runs.Count; i++)
        {
            var preparedRun = preparedText.Runs[i];
            DrawTextStringAlignedLeft(preparedRun.StyleSource, preparedRun.Text, ref drawX, ref drawY, geometryBounds, ignoreAttributes, canvas, assetLoader, getElementAddressKey, contextPaint);
            ApplyInlineAdvance(preparedRun.StyleSource, ref drawX, ref drawY, GetPreparedSequentialRunBoundaryAdvance(preparedText.Runs, i, geometryBounds));
        }

        currentX = drawX;
        currentY = drawY;

        return true;
    }

    private static bool TryMeasureSequentialTextRuns(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds,
        bool trimLeadingWhitespaceAtStart)
    {
        var hasInlineSizeLayout = HasInlineSizeLayout(svgTextBase);
        if (!hasInlineSizeLayout &&
            HasPreparedSequentialTextContainerBarriers(svgTextBase))
        {
            return false;
        }

        if (hasInlineSizeLayout &&
            TryMeasureInlineSizeTextPathFragment(
                svgTextBase,
                ref currentX,
                ref currentY,
                viewport,
                assetLoader,
                ref bounds))
        {
            return true;
        }

        var preservePreLineBreaks = hasInlineSizeLayout && PreservesInlineLineBreaksInTextSubtree(svgTextBase);
        if (!TryCollectSequentialTextRuns(svgTextBase, requireAnchorContent: false, IsTextReferenceRenderingEnabled(assetLoader), trimLeadingWhitespaceAtStart, out var runs, preservePreLineBreaks))
        {
            return false;
        }

        if (TryMeasureInlineSizeTextOverflowRuns(svgTextBase, runs, ref currentX, ref currentY, viewport, assetLoader, ref bounds))
        {
            return true;
        }

        if (hasInlineSizeLayout &&
            HasPreparedSequentialTextContainerBarriers(svgTextBase))
        {
            return false;
        }

        if (preservePreLineBreaks &&
            !TryCollectSequentialTextRuns(svgTextBase, requireAnchorContent: false, IsTextReferenceRenderingEnabled(assetLoader), trimLeadingWhitespaceAtStart, out runs))
        {
            return false;
        }

        if (TryMeasureShapedSequentialTextRuns(svgTextBase, runs, ref currentX, ref currentY, viewport, assetLoader, ref bounds))
        {
            return true;
        }

        ApplyInitialSequentialOffsets(svgTextBase, viewport, ref currentX, ref currentY);
        var isVertical = IsVerticalWritingMode(svgTextBase);
        var textAlign = GetTextAnchorAlign(svgTextBase, viewport);
        if (!hasInlineSizeLayout &&
            !isVertical &&
            textAlign == SKTextAlign.Left &&
            CanMeasureSequentialTextRunsDirectly(runs))
        {
            var startAlignedX = currentX;
            var startAlignedY = currentY;
            for (var i = 0; i < runs.Count; i++)
            {
                var run = runs[i];
                var runBounds = MeasureTextStringBoundsAlignedLeft(run.StyleSource, run.Text, startAlignedX, startAlignedY, viewport, assetLoader, rotations: null, out var advance);
                UnionBounds(ref bounds, runBounds);
                ApplyInlineAdvance(run.StyleSource, ref startAlignedX, ref startAlignedY, advance + GetSequentialRunBoundaryAdvance(runs, i, viewport));
            }

            currentX = startAlignedX;
            currentY = startAlignedY;
            return true;
        }

        if (!TryPrepareSequentialTextRuns(runs, viewport, assetLoader, out var preparedText) ||
            preparedText is null)
        {
            return false;
        }

        if (textAlign == SKTextAlign.Left)
        {
            var startAlignedX = currentX;
            var startAlignedY = currentY;
            for (var i = 0; i < preparedText.Runs.Count; i++)
            {
                var preparedRun = preparedText.Runs[i];
                var runBounds = MeasureTextStringBoundsAlignedLeft(preparedRun.StyleSource, preparedRun.Text, startAlignedX, startAlignedY, viewport, assetLoader, rotations: null, out _);
                UnionBounds(ref bounds, runBounds);
                ApplyInlineAdvance(preparedRun.StyleSource, ref startAlignedX, ref startAlignedY, preparedRun.Advance);
            }

            currentX = startAlignedX;
            currentY = startAlignedY;
            return true;
        }

        var totalAdvance = preparedText.TotalAdvance;
        var inlineOrigin = isVertical
            ? GetVerticalInlineStartCoordinate(svgTextBase, currentY, totalAdvance, textAlign)
            : GetAlignedStartCoordinate(currentX, totalAdvance, textAlign);
        var drawX = isVertical ? currentX : inlineOrigin;
        var drawY = isVertical ? inlineOrigin : currentY;

        for (var i = 0; i < preparedText.Runs.Count; i++)
        {
            var preparedRun = preparedText.Runs[i];
            var runBounds = MeasureTextStringBoundsAlignedLeft(preparedRun.StyleSource, preparedRun.Text, drawX, drawY, viewport, assetLoader, rotations: null, out _);
            UnionBounds(ref bounds, runBounds);
            ApplyInlineAdvance(preparedRun.StyleSource, ref drawX, ref drawY, preparedRun.Advance);
        }

        currentX = drawX;
        currentY = drawY;

        return true;
    }

    private static bool CanMeasureSequentialTextRunsDirectly(
        IReadOnlyList<SequentialTextRun> runs)
    {
        if (runs.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (IsVerticalWritingMode(run.StyleSource) ||
                HasPerGlyphLayoutAdjustments(run.StyleSource, run.Text) ||
                !IsSimpleAsciiSequentialCompileText(run.Text))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryDrawInlineSizeTextOverflowRuns(
        SvgTextBase svgTextBase,
        IReadOnlyList<SequentialTextRun> runs,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint)
    {
        if (!TryCreateInlineSizeTextOverflowLayout(svgTextBase, runs, currentX, currentY, viewport, geometryBounds, assetLoader, out var layout) ||
            layout is null)
        {
            return false;
        }

        for (var lineIndex = 0; lineIndex < layout.Lines.Count; lineIndex++)
        {
            var line = layout.Lines[lineIndex];
            if (line.ShouldClip)
            {
                canvas.Save();
                canvas.ClipRect(line.ClipRect, SKClipOperation.Intersect);
            }

            if (line.PlaceVisualRunsRightToLeft)
            {
                var cursorX = GetInlineSizeVisualRunCursorX(line);
                for (var i = 0; i < line.VisualRuns.Count; i++)
                {
                    var run = line.VisualRuns[i];
                    var drawX = TakeInlineSizeVisualRunX(line, run, ref cursorX);
                    var drawY = line.BaselineY;
                    DrawTextStringAlignedLeft(
                        run.StyleSource,
                        run.Text,
                        ref drawX,
                        ref drawY,
                        geometryBounds,
                        ignoreAttributes,
                        canvas,
                        assetLoader,
                        getElementAddressKey,
                        contextPaint);
                }
            }
            else
            {
                var drawX = line.StartX;
                var drawY = line.BaselineY;
                for (var i = 0; i < line.VisualRuns.Count; i++)
                {
                    var run = line.VisualRuns[i];
                    DrawTextStringAlignedLeft(
                        run.StyleSource,
                        run.Text,
                        ref drawX,
                        ref drawY,
                        geometryBounds,
                        ignoreAttributes,
                        canvas,
                        assetLoader,
                        getElementAddressKey,
                        contextPaint);
                }
            }

            if (line.ShouldClip)
            {
                canvas.Restore();
            }
        }

        currentX = layout.FinalX;
        currentY = layout.FinalY;
        return true;
    }

    private static bool TryMeasureInlineSizeTextOverflowRuns(
        SvgTextBase svgTextBase,
        IReadOnlyList<SequentialTextRun> runs,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds)
    {
        if (!TryCreateInlineSizeTextOverflowLayout(svgTextBase, runs, currentX, currentY, viewport, viewport, assetLoader, out var layout) ||
            layout is null)
        {
            return false;
        }

        for (var lineIndex = 0; lineIndex < layout.Lines.Count; lineIndex++)
        {
            var line = layout.Lines[lineIndex];
            if (line.PlaceVisualRunsRightToLeft)
            {
                var cursorX = GetInlineSizeVisualRunCursorX(line);
                for (var i = 0; i < line.VisualRuns.Count; i++)
                {
                    var run = line.VisualRuns[i];
                    var drawX = TakeInlineSizeVisualRunX(line, run, ref cursorX);
                    var drawY = line.BaselineY;
                    var runBounds = MeasureTextStringBoundsAlignedLeft(run.StyleSource, run.Text, drawX, drawY, viewport, assetLoader, rotations: null, out _);
                    if (!line.ShouldClip)
                    {
                        UnionBounds(ref bounds, runBounds);
                    }
                    else if (TryIntersectRect(runBounds, line.ClipRect, out var clippedRunBounds))
                    {
                        UnionBounds(ref bounds, clippedRunBounds);
                    }
                }
            }
            else
            {
                var drawX = line.StartX;
                var drawY = line.BaselineY;
                for (var i = 0; i < line.VisualRuns.Count; i++)
                {
                    var run = line.VisualRuns[i];
                    var runBounds = MeasureTextStringBoundsAlignedLeft(run.StyleSource, run.Text, drawX, drawY, viewport, assetLoader, rotations: null, out var advance);
                    if (!line.ShouldClip)
                    {
                        UnionBounds(ref bounds, runBounds);
                    }
                    else if (TryIntersectRect(runBounds, line.ClipRect, out var clippedRunBounds))
                    {
                        UnionBounds(ref bounds, clippedRunBounds);
                    }

                    ApplyInlineAdvance(run.StyleSource, ref drawX, ref drawY, advance);
                }
            }
        }

        currentX = layout.FinalX;
        currentY = layout.FinalY;
        return true;
    }

    private static bool TryDrawInlineSizeTextPathFragment(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint)
    {
        if (TryDrawInlineSizeTextPathFlowLayout(
                svgTextBase,
                ref currentX,
                ref currentY,
                viewport,
                geometryBounds,
                ignoreAttributes,
                canvas,
                assetLoader,
                getElementAddressKey,
                contextPaint))
        {
            return true;
        }

        if (!TryCreateInlineSizeTextPathFragment(svgTextBase, currentX, currentY, viewport, geometryBounds, assetLoader, out var fragment))
        {
            return false;
        }

        var drawX = fragment.AnchorX;
        var drawY = fragment.BaselineY;
        if (DrawTextPath(fragment.TextPath, ref drawX, ref drawY, useCurrentPositionOffset: true, viewport, ignoreAttributes, canvas, assetLoader, references: null, getElementAddressKey, contextPaint) != TextPathRenderResult.Rendered)
        {
            return false;
        }

        currentX = fragment.FinalX;
        currentY = fragment.FinalY;
        return true;
    }

    private static bool TryMeasureInlineSizeTextPathFragment(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds)
    {
        if (TryMeasureInlineSizeTextPathFlowLayout(svgTextBase, ref currentX, ref currentY, viewport, assetLoader, ref bounds))
        {
            return true;
        }

        if (!TryCreateInlineSizeTextPathFragment(svgTextBase, currentX, currentY, viewport, viewport, assetLoader, out var fragment))
        {
            return false;
        }

        var measureX = fragment.AnchorX;
        var measureY = fragment.BaselineY;
        if (MeasureTextPath(fragment.TextPath, ref measureX, ref measureY, useCurrentPositionOffset: true, viewport, assetLoader, ref bounds) != TextPathRenderResult.Rendered)
        {
            return false;
        }

        currentX = fragment.FinalX;
        currentY = fragment.FinalY;
        return true;
    }

    private static bool TryDrawInlineSizeTextPathFlowLayout(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint)
    {
        if (!TryCreateInlineSizeTextPathFlowLayout(svgTextBase, currentX, currentY, viewport, geometryBounds, assetLoader, out var layout) ||
            layout is null)
        {
            return false;
        }

        for (var lineIndex = 0; lineIndex < layout.Lines.Count; lineIndex++)
        {
            var line = layout.Lines[lineIndex];
            var drawX = line.StartX;
            var drawY = line.BaselineY;
            for (var runIndex = 0; runIndex < line.RunIndexes.Length; runIndex++)
            {
                var run = layout.Runs[line.RunIndexes[runIndex]];
                if (run.Kind == InlineSizeTextPathFlowRunKind.Text)
                {
                    var runStartX = drawX;
                    var runStartY = drawY;
                    DrawTextStringAlignedLeft(
                        run.StyleSource,
                        run.Text,
                        ref drawX,
                        ref drawY,
                        geometryBounds,
                        ignoreAttributes,
                        canvas,
                        assetLoader,
                        getElementAddressKey,
                        contextPaint);
                    drawX = runStartX + run.Advance;
                    drawY = runStartY;
                }
                else
                {
                    if (!TryDrawInlineSizeTextPathFlowRun(
                            run,
                            drawX,
                            drawY,
                            viewport,
                            geometryBounds,
                            ignoreAttributes,
                            canvas,
                            assetLoader,
                            getElementAddressKey,
                            contextPaint))
                    {
                        return false;
                    }

                    drawX += run.Advance;
                }
            }
        }

        currentX = layout.FinalX;
        currentY = layout.FinalY;
        return true;
    }

    private static bool TryMeasureInlineSizeTextPathFlowLayout(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds)
    {
        if (!TryCreateInlineSizeTextPathFlowLayout(svgTextBase, currentX, currentY, viewport, viewport, assetLoader, out var layout) ||
            layout is null)
        {
            return false;
        }

        for (var lineIndex = 0; lineIndex < layout.Lines.Count; lineIndex++)
        {
            var line = layout.Lines[lineIndex];
            var drawX = line.StartX;
            var drawY = line.BaselineY;
            for (var runIndex = 0; runIndex < line.RunIndexes.Length; runIndex++)
            {
                var run = layout.Runs[line.RunIndexes[runIndex]];
                if (run.Kind == InlineSizeTextPathFlowRunKind.Text)
                {
                    var runBounds = MeasureTextStringBoundsAlignedLeft(run.StyleSource, run.Text, drawX, drawY, viewport, assetLoader, rotations: null, out _);
                    UnionBounds(ref bounds, runBounds);
                    drawX += run.Advance;
                }
                else
                {
                    if (!TryMeasureInlineSizeTextPathFlowRun(run, drawX, drawY, viewport, assetLoader, ref bounds))
                    {
                        return false;
                    }

                    drawX += run.Advance;
                }
            }
        }

        currentX = layout.FinalX;
        currentY = layout.FinalY;
        return true;
    }

    private static bool TryDrawInlineSizeTextPathFlowRun(
        InlineSizeTextPathFlowRun run,
        float startX,
        float baselineY,
        SKRect viewport,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint)
    {
        if (!TryResolveInlineSizeTextPathFlowOffsets(run, startX, baselineY, viewport, assetLoader, out var hOffset, out var verticalOffset) ||
            run.TextPath is null ||
            run.TextPathRuns is null ||
            run.PathSamples is null)
        {
            return false;
        }

        if (run.TextPath.Method == SvgTextPathMethod.Stretch)
        {
            if (!TryCreateStretchedTextPathRunPaths(run.TextPathRuns, run.PathSamples, run.PathLength, run.IsClosedLoop, hOffset, verticalOffset, viewport, run.GeometryBounds, assetLoader, out var stretchedRuns, out _, out _, run.SpecifiedLengthOverride))
            {
                return false;
            }

            DrawStretchedTextPathRuns(stretchedRuns, viewport, geometryBounds, ignoreAttributes, canvas, assetLoader, references: null, getElementAddressKey, contextPaint);
            return true;
        }

        if (!TryCreateTextPathRunPlacements(run.TextPathRuns, run.PathSamples, run.IsClosedLoop, hOffset, verticalOffset, viewport, run.GeometryBounds, assetLoader, out var positionedRuns, out _, out _, run.SpecifiedLengthOverride))
        {
            return false;
        }

        DrawPositionedTextPathRuns(positionedRuns, viewport, geometryBounds, ignoreAttributes, canvas, assetLoader, references: null, getElementAddressKey, contextPaint);
        return true;
    }

    private static bool TryMeasureInlineSizeTextPathFlowRun(
        InlineSizeTextPathFlowRun run,
        float startX,
        float baselineY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds)
    {
        if (!TryResolveInlineSizeTextPathFlowOffsets(run, startX, baselineY, viewport, assetLoader, out var hOffset, out var verticalOffset) ||
            run.TextPath is null ||
            run.TextPathRuns is null ||
            run.PathSamples is null)
        {
            return false;
        }

        if (run.TextPath.Method == SvgTextPathMethod.Stretch)
        {
            if (!TryCreateStretchedTextPathRunPaths(run.TextPathRuns, run.PathSamples, run.PathLength, run.IsClosedLoop, hOffset, verticalOffset, viewport, run.GeometryBounds, assetLoader, out var stretchedRuns, out _, out _, run.SpecifiedLengthOverride))
            {
                return false;
            }

            for (var i = 0; i < stretchedRuns.Count; i++)
            {
                UnionBounds(ref bounds, stretchedRuns[i].Path.Bounds);
            }

            return true;
        }

        if (!TryCreateTextPathRunPlacements(run.TextPathRuns, run.PathSamples, run.IsClosedLoop, hOffset, verticalOffset, viewport, run.GeometryBounds, assetLoader, out var positionedRuns, out _, out _, run.SpecifiedLengthOverride))
        {
            return false;
        }

        var fallbackResolver = positionedRuns.Count > 0
            ? GetFallbackCodepointResolver(positionedRuns[0].StyleSource)
            : GetFallbackCodepointResolver(run.TextPath!);
        for (var i = 0; i < positionedRuns.Count; i++)
        {
            var runBounds = GetPositionedTextPathRunBounds(positionedRuns[i], run.GeometryBounds, assetLoader, fallbackResolver);
            UnionBounds(ref bounds, runBounds);
        }

        return true;
    }

    private static bool TryResolveInlineSizeTextPathFlowOffsets(
        InlineSizeTextPathFlowRun run,
        float startX,
        float baselineY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out float hOffset,
        out float verticalOffset)
    {
        hOffset = 0f;
        verticalOffset = 0f;
        if (run.TextPath is null ||
            run.PathSamples is null ||
            run.Advance <= 0f)
        {
            return false;
        }

        var textPathAlign = GetTextAnchorAlign(run.TextPath, run.GeometryBounds);
        var anchorX = GetAnchorCoordinateFromAlignedStart(startX, run.Advance, textPathAlign);
        ResolveTextPathChunkOffsets(run.TextPath, useCurrentPositionOffset: true, anchorX, baselineY, viewport, assetLoader, run.PathSamples, out var horizontalOffset, out verticalOffset);
        var startOffset = horizontalOffset + run.ResolvedStartOffset;
        hOffset = ApplyTextAnchor(run.TextPath, startOffset, run.GeometryBounds, run.Advance);
        hOffset = ApplyTextPathSideOffset(run.TextPath, hOffset, run.PathLength, run.Advance);
        return true;
    }

    private static bool TryCreateInlineSizeTextPathFlowLayout(
        SvgTextBase svgTextBase,
        float currentX,
        float currentY,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out InlineSizeTextPathFlowLayout? layout)
    {
        layout = null;
        if (!HasInlineSizeLayout(svgTextBase) ||
            IsVerticalWritingMode(svgTextBase) ||
            IsRightToLeft(svgTextBase) ||
            ResolveTextOverflowMarker(svgTextBase) is not null ||
            HasActiveShapeTextLayout(svgTextBase) ||
            HasNonNoneCssTextProperty(svgTextBase.ShapeSubtract))
        {
            return false;
        }

        if (!TryCollectInlineSizeTextPathFlowSegments(svgTextBase, viewport, geometryBounds, assetLoader, out var runs, out var segments) ||
            runs.Length == 0 ||
            segments.Length == 0 ||
            !runs.Any(static run => run.Kind == InlineSizeTextPathFlowRunKind.TextPath))
        {
            return false;
        }

        if (!TryApplyRootTextLengthToInlineSizeTextPathFlow(svgTextBase, viewport, runs, segments))
        {
            return false;
        }

        ApplyInitialSequentialOffsets(svgTextBase, viewport, ref currentX, ref currentY);
        var textAlign = GetTextAnchorAlign(svgTextBase, geometryBounds);
        if (!TryCreateInlineSizeTextArea(
                svgTextBase,
                Array.Empty<PreparedSequentialRun>(),
                currentX,
                currentY,
                viewport,
                geometryBounds,
                assetLoader,
                textAlign,
                out var textArea,
                out var firstLineBlockCoordinate) ||
            textArea is null ||
            textArea.IsVertical ||
            GetInlineSizeTextAreaExtent(textArea) <= 0f)
        {
            return false;
        }

        var lineAdvanceRuns = CreateInlineSizeTextPathLineAdvanceRuns(runs);
        var lineAdvance = GetInlineSizeLineAdvance(lineAdvanceRuns, geometryBounds, assetLoader);
        var maxLineSearchCount = textArea.GetMaxWrappedLineSearchCount(firstLineBlockCoordinate, lineAdvance, segments.Length, blockProgression: 1);
        if (maxLineSearchCount <= 0)
        {
            return false;
        }

        var logicalLines = AllowsInlineSizeTextPathFlowWrapping(svgTextBase, runs)
            ? CreateWrappedInlineSizeTextPathFlowLines(
                segments,
                runs,
                lineIndex => textArea.ResolveWrappedLineArea(lineIndex, firstLineBlockCoordinate, lineAdvance, blockProgression: 1),
                maxLineSearchCount,
                PreservesLineEdgeWhitespace(runs))
            : CreateSingleLineInlineSizeTextPathFlowLines(segments, runs, textArea.ResolveLineArea(firstLineBlockCoordinate, lineAdvance));
        if (logicalLines.Count == 0)
        {
            return false;
        }

        var lines = new List<InlineSizeTextPathFlowLine>(logicalLines.Count);
        var finalX = currentX;
        var finalY = currentY;
        for (var i = 0; i < logicalLines.Count; i++)
        {
            var logicalLine = logicalLines[i];
            if (logicalLine.Area.InlineSize <= 0f ||
                logicalLine.RunIndexes.Length == 0)
            {
                continue;
            }

            var startX = logicalLine.Area.Start;
            var baselineY = logicalLine.Area.BlockCoordinate;
            lines.Add(new InlineSizeTextPathFlowLine(logicalLine.RunIndexes, logicalLine.Advance, startX, baselineY));
            finalX = startX + logicalLine.Advance;
            finalY = baselineY;
        }

        if (lines.Count == 0)
        {
            return false;
        }

        layout = new InlineSizeTextPathFlowLayout(runs, lines.ToArray(), finalX, finalY);
        return true;
    }

    private readonly record struct InlineSizeTextPathLogicalLine(int[] RunIndexes, float Advance, InlineSizeLineArea Area);

    private static List<InlineSizeTextPathLogicalLine> CreateWrappedInlineSizeTextPathFlowLines(
        IReadOnlyList<InlineSizeTextPathFlowSegment> segments,
        IReadOnlyList<InlineSizeTextPathFlowRun> runs,
        Func<int, InlineSizeLineArea> resolveLineArea,
        int maxLineSearchCount,
        bool preserveLineEdgeWhitespace)
    {
        var sharedSegments = new SvgSharedTextLayoutSegment[segments.Count];
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var sharedRuns = new SvgSharedTextLayoutRun[segment.RunIndexes.Length];
            for (var j = 0; j < segment.RunIndexes.Length; j++)
            {
                var runIndex = segment.RunIndexes[j];
                var run = runs[runIndex];
                sharedRuns[j] = new SvgSharedTextLayoutRun(run.StyleSource, run.Text, run.Advance, runIndex);
            }

            sharedSegments[i] = new SvgSharedTextLayoutSegment(sharedRuns, segment.Advance, segment.IsWhitespace, segment.ForcesLineBreak);
        }

        var sharedLines = SvgSharedTextLayoutEngine.CreateWrappedLogicalLines(
            sharedSegments,
            lineIndex =>
            {
                var area = resolveLineArea(lineIndex);
                return new SvgSharedTextLineArea(area.Start, area.InlineSize, area.BlockCoordinate, ToSharedFragments(area.Fragments), area.InlineProgression);
            },
            maxLineSearchCount,
            preserveLineEdgeWhitespace,
            TextLengthTolerance,
            run => run.SourceCodepointIndex >= 0 &&
                   run.SourceCodepointIndex < runs.Count &&
                   runs[run.SourceCodepointIndex].Kind == InlineSizeTextPathFlowRunKind.Text &&
                   IsWhitespaceOnlyText(runs[run.SourceCodepointIndex].Text));

        var lines = new List<InlineSizeTextPathLogicalLine>(sharedLines.Count);
        for (var i = 0; i < sharedLines.Count; i++)
        {
            var sharedLine = sharedLines[i];
            var runIndexes = new int[sharedLine.Runs.Length];
            for (var j = 0; j < sharedLine.Runs.Length; j++)
            {
                runIndexes[j] = sharedLine.Runs[j].SourceCodepointIndex;
            }

            var area = new InlineSizeLineArea(
                sharedLine.Area.Start,
                sharedLine.Area.InlineSize,
                sharedLine.Area.BlockCoordinate,
                ToInlineFragments(sharedLine.Area.Fragments),
                sharedLine.Area.InlineProgression);
            lines.Add(new InlineSizeTextPathLogicalLine(runIndexes, sharedLine.Advance, area));
        }

        return lines;

        static SvgSharedTextLineAreaFragment[] ToSharedFragments(InlineSizeLineAreaFragment[]? fragments)
        {
            if (fragments is not { Length: > 0 })
            {
                return Array.Empty<SvgSharedTextLineAreaFragment>();
            }

            var sharedFragments = new SvgSharedTextLineAreaFragment[fragments.Length];
            for (var i = 0; i < fragments.Length; i++)
            {
                sharedFragments[i] = new SvgSharedTextLineAreaFragment(fragments[i].Start, fragments[i].End);
            }

            return sharedFragments;
        }

        static InlineSizeLineAreaFragment[] ToInlineFragments(SvgSharedTextLineAreaFragment[]? fragments)
        {
            if (fragments is not { Length: > 0 })
            {
                return Array.Empty<InlineSizeLineAreaFragment>();
            }

            var inlineFragments = new InlineSizeLineAreaFragment[fragments.Length];
            for (var i = 0; i < fragments.Length; i++)
            {
                inlineFragments[i] = new InlineSizeLineAreaFragment(fragments[i].Start, fragments[i].End);
            }

            return inlineFragments;
        }
    }

    private static List<InlineSizeTextPathLogicalLine> CreateSingleLineInlineSizeTextPathFlowLines(
        IReadOnlyList<InlineSizeTextPathFlowSegment> segments,
        IReadOnlyList<InlineSizeTextPathFlowRun> runs,
        InlineSizeLineArea area)
    {
        var runIndexes = new List<int>();
        var advance = 0f;
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (segment.ForcesLineBreak)
            {
                break;
            }

            if (segment.IsWhitespace && runIndexes.Count == 0 && !PreservesLineEdgeWhitespace(runs))
            {
                continue;
            }

            runIndexes.AddRange(segment.RunIndexes);
            advance += segment.Advance;
        }

        return runIndexes.Count > 0 && area.InlineSize > 0f
            ? new List<InlineSizeTextPathLogicalLine> { new(runIndexes.ToArray(), advance, area) }
            : new List<InlineSizeTextPathLogicalLine>();
    }

    private static InlineSizeTextRun[] CreateInlineSizeTextPathLineAdvanceRuns(IReadOnlyList<InlineSizeTextPathFlowRun> runs)
    {
        var lineAdvanceRuns = new InlineSizeTextRun[runs.Count];
        for (var i = 0; i < runs.Count; i++)
        {
            lineAdvanceRuns[i] = new InlineSizeTextRun(runs[i].StyleSource, string.IsNullOrEmpty(runs[i].Text) ? "M" : runs[i].Text, runs[i].Advance);
        }

        return lineAdvanceRuns;
    }

    private static bool AllowsInlineSizeTextPathFlowWrapping(SvgTextBase svgTextBase, IReadOnlyList<InlineSizeTextPathFlowRun> runs)
    {
        if (!AllowsInlineSizeWrapping(svgTextBase))
        {
            return false;
        }

        for (var i = 0; i < runs.Count; i++)
        {
            if (!AllowsInlineSizeWrapping(runs[i].StyleSource))
            {
                return false;
            }
        }

        return true;
    }

    private static bool PreservesLineEdgeWhitespace(IReadOnlyList<InlineSizeTextPathFlowRun> runs)
    {
        for (var i = 0; i < runs.Count; i++)
        {
            if (PreservesLineEdgeWhitespace(runs[i].StyleSource))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryApplyRootTextLengthToInlineSizeTextPathFlow(
        SvgTextBase svgTextBase,
        SKRect viewport,
        InlineSizeTextPathFlowRun[] runs,
        InlineSizeTextPathFlowSegment[] segments)
    {
        if (!HasOwnTextLengthAdjustment(svgTextBase))
        {
            return true;
        }

        if (!TryGetOwnTextLength(svgTextBase, viewport, isVertical: false, out var specifiedLength) ||
            specifiedLength <= 0f)
        {
            return false;
        }

        var naturalAdvance = 0f;
        var lastAdjustableRunIndex = -1;
        for (var i = 0; i < runs.Length; i++)
        {
            var run = runs[i];
            if (run.Advance <= TextLengthTolerance)
            {
                continue;
            }

            naturalAdvance += run.Advance;
            lastAdjustableRunIndex = i;
        }

        if (naturalAdvance <= TextLengthTolerance ||
            lastAdjustableRunIndex < 0)
        {
            return false;
        }

        var assignedAdvance = 0f;
        for (var i = 0; i < runs.Length; i++)
        {
            var run = runs[i];
            if (run.Advance <= TextLengthTolerance)
            {
                continue;
            }

            var targetAdvance = i == lastAdjustableRunIndex
                ? Math.Max(0f, specifiedLength - assignedAdvance)
                : specifiedLength * (run.Advance / naturalAdvance);
            assignedAdvance += targetAdvance;

            if (run.Kind == InlineSizeTextPathFlowRunKind.TextPath &&
                run.TextPathRuns is { Length: > 0 } textPathRuns)
            {
                var adjustedTextPathRuns = new TextPathRun[textPathRuns.Length];
                for (var textPathRunIndex = 0; textPathRunIndex < textPathRuns.Length; textPathRunIndex++)
                {
                    adjustedTextPathRuns[textPathRunIndex] = textPathRuns[textPathRunIndex] with { TextPathSource = svgTextBase };
                }

                runs[i] = run with
                {
                    Advance = targetAdvance,
                    TextPathRuns = adjustedTextPathRuns,
                    SpecifiedLengthOverride = targetAdvance
                };
                continue;
            }

            runs[i] = run with { Advance = targetAdvance };
        }

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var segmentAdvance = 0f;
            for (var runIndex = 0; runIndex < segment.RunIndexes.Length; runIndex++)
            {
                var index = segment.RunIndexes[runIndex];
                if (index >= 0 && index < runs.Length)
                {
                    segmentAdvance += runs[index].Advance;
                }
            }

            segments[i] = segment with { Advance = segmentAdvance };
        }

        return true;
    }

    private static bool TryCollectInlineSizeTextPathFlowSegments(
        SvgTextBase svgTextBase,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out InlineSizeTextPathFlowRun[] runs,
        out InlineSizeTextPathFlowSegment[] segments)
    {
        var flowRuns = new List<InlineSizeTextPathFlowRun>();
        var flowSegments = new List<InlineSizeTextPathFlowSegment>();
        var segmentRunIndexes = new List<int>();
        var advanceCache = new Dictionary<TextRunAdvanceCacheKey, float>();
        float segmentAdvance = 0f;
        bool? segmentIsWhitespace = null;
        var trimLeadingWhitespace = true;
        var previousEndedWithSpace = false;

        runs = Array.Empty<InlineSizeTextPathFlowRun>();
        segments = Array.Empty<InlineSizeTextPathFlowSegment>();

        if (!Visit(GetContentNodeList(svgTextBase), svgTextBase, ref trimLeadingWhitespace, ref previousEndedWithSpace))
        {
            return false;
        }

        FlushSegment();
        if (flowRuns.Count == 0 || flowSegments.Count == 0)
        {
            return false;
        }

        runs = flowRuns.ToArray();
        segments = flowSegments.ToArray();
        return true;

        bool Visit(IReadOnlyList<ISvgNode> contentNodes, SvgTextBase styleSource, ref bool trimLeadingWhitespaceRef, ref bool previousEndedWithSpaceRef)
        {
            for (var nodeIndex = 0; nodeIndex < contentNodes.Count; nodeIndex++)
            {
                var node = contentNodes[nodeIndex];
                switch (node)
                {
                    case SvgAnchor svgAnchor:
                        if (!CanRenderTextSubtree(svgAnchor))
                        {
                            break;
                        }

                        if (!Visit(GetContentNodeList(svgAnchor), CreateAnchorTextStyleSource(svgAnchor), ref trimLeadingWhitespaceRef, ref previousEndedWithSpaceRef))
                        {
                            return false;
                        }

                        break;

                    case SvgTextSpan svgTextSpan:
                        if (!CanRenderTextSubtree(svgTextSpan))
                        {
                            break;
                        }

                        if (HasExplicitTextPositioning(svgTextSpan) ||
                            HasOwnTextLengthAdjustment(svgTextSpan) ||
                            IsVerticalWritingMode(svgTextSpan) ||
                            IsRightToLeft(svgTextSpan))
                        {
                            return false;
                        }

                        var childTrimLeadingWhitespace = trimLeadingWhitespaceRef || previousEndedWithSpaceRef;
                        var childPreviousEndedWithSpace = false;
                        if (!Visit(GetContentNodeList(svgTextSpan), svgTextSpan, ref childTrimLeadingWhitespace, ref childPreviousEndedWithSpace))
                        {
                            return false;
                        }

                        if (childPreviousEndedWithSpace || flowRuns.Count > 0)
                        {
                            trimLeadingWhitespaceRef = false;
                            previousEndedWithSpaceRef = childPreviousEndedWithSpace;
                        }

                        break;

                    case SvgTextPath textPath:
                        if (!CanRenderTextSubtree(textPath))
                        {
                            break;
                        }

                        FlushSegment();
                        if (!TryCreateInlineSizeTextPathFlowRun(textPath, viewport, geometryBounds, assetLoader, advanceCache, out var textPathRun))
                        {
                            return false;
                        }

                        var textPathRunIndex = flowRuns.Count;
                        flowRuns.Add(textPathRun);
                        flowSegments.Add(new InlineSizeTextPathFlowSegment(new[] { textPathRunIndex }, textPathRun.Advance, IsWhitespace: false, ForcesLineBreak: false));
                        trimLeadingWhitespaceRef = false;
                        previousEndedWithSpaceRef = EndsWithCollapsedSpace(textPath);
                        break;

                    case SvgTextRef:
                        return false;

                    case not SvgTextBase:
                        var rawContent = node.Content;
                        if (string.IsNullOrEmpty(rawContent))
                        {
                            break;
                        }

                        var text = PrepareText(
                            styleSource,
                            rawContent,
                            trimLeadingWhitespace: trimLeadingWhitespaceRef,
                            trimTrailingWhitespace: IsTerminalContentNode(contentNodes, nodeIndex),
                            preservePreLineBreaks: PreservesInlineLineBreaks(styleSource));
                        if (previousEndedWithSpaceRef &&
                            CollapsesTextWhitespace(styleSource) &&
                            !string.IsNullOrEmpty(text) &&
                            text![0] == ' ')
                        {
                            text = text.TrimStart(' ');
                        }

                        if (string.IsNullOrEmpty(text))
                        {
                            break;
                        }

                        AppendTextPieces(styleSource, text!, geometryBounds, assetLoader);
                        trimLeadingWhitespaceRef = false;
                        previousEndedWithSpaceRef = text!.EndsWith(" ", StringComparison.Ordinal);
                        break;
                }
            }

            return true;
        }

        void FlushSegment()
        {
            if (segmentRunIndexes.Count == 0 || !segmentIsWhitespace.HasValue)
            {
                return;
            }

            flowSegments.Add(new InlineSizeTextPathFlowSegment(segmentRunIndexes.ToArray(), segmentAdvance, segmentIsWhitespace.Value, ForcesLineBreak: false));
            segmentRunIndexes.Clear();
            segmentAdvance = 0f;
            segmentIsWhitespace = null;
        }

        void AppendLineBreak()
        {
            FlushSegment();
            flowSegments.Add(new InlineSizeTextPathFlowSegment(Array.Empty<int>(), 0f, IsWhitespace: true, ForcesLineBreak: true));
        }

        void AppendTextPiece(SvgTextBase styleSource, string text, bool isWhitespace)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (segmentIsWhitespace.HasValue && segmentIsWhitespace.Value != isWhitespace)
            {
                FlushSegment();
            }

            var advance = MeasureTextAdvanceCached(styleSource, text, geometryBounds, assetLoader, advanceCache);
            var runIndex = flowRuns.Count;
            flowRuns.Add(new InlineSizeTextPathFlowRun(InlineSizeTextPathFlowRunKind.Text, styleSource, text, advance));
            segmentRunIndexes.Add(runIndex);
            segmentAdvance += advance;
            segmentIsWhitespace = isWhitespace;
        }

        void AppendTextPieces(SvgTextBase styleSource, string text, SKRect bounds, ISvgAssetLoader loader)
        {
            var breakSpaces = GetInlineSizeWhiteSpaceModel(styleSource).BreaksAfterEveryPreservedSpace;
            var lineBreakOptions = GetInlineSizeLineBreakOptions(styleSource);
            var codepoints = SplitCodepointsReadOnly(text);
            var piece = new StringBuilder(text.Length);
            bool? pieceIsWhitespace = null;
            var bidiFormattingDepth = 0;
            string? previousCodepoint = null;
            for (var i = 0; i < codepoints.Count; i++)
            {
                var codepoint = codepoints[i];
                var nextCodepoint = i + 1 < codepoints.Count ? codepoints[i + 1] : null;
                if (codepoint == "\n")
                {
                    if (piece.Length > 0 && pieceIsWhitespace.HasValue)
                    {
                        AppendTextPiece(styleSource, piece.ToString(), pieceIsWhitespace.Value);
                        piece.Clear();
                        pieceIsWhitespace = null;
                    }

                    AppendLineBreak();
                    previousCodepoint = null;
                    continue;
                }

                if (IsInlineSizeInvisibleBreakOpportunity(codepoint, previousCodepoint, nextCodepoint, bidiFormattingDepth > 0))
                {
                    if (piece.Length > 0 && pieceIsWhitespace.HasValue)
                    {
                        AppendTextPiece(styleSource, piece.ToString(), pieceIsWhitespace.Value);
                        piece.Clear();
                        pieceIsWhitespace = null;
                    }

                    FlushSegment();
                    previousCodepoint = codepoint;
                    continue;
                }

                var codepointIsWhitespace = IsInlineSizeBreakOpportunityWhitespace(codepoint, previousCodepoint, nextCodepoint, bidiFormattingDepth > 0);
                if (breakSpaces && codepointIsWhitespace)
                {
                    if (piece.Length > 0 && pieceIsWhitespace.HasValue)
                    {
                        AppendTextPiece(styleSource, piece.ToString(), pieceIsWhitespace.Value);
                        piece.Clear();
                        pieceIsWhitespace = null;
                    }

                    AppendTextPiece(styleSource, codepoint, isWhitespace: true);
                    FlushSegment();
                    bidiFormattingDepth = UpdateInlineSizeBidiFormattingDepth(bidiFormattingDepth, codepoint);
                    previousCodepoint = codepoint;
                    continue;
                }

                if (!codepointIsWhitespace &&
                    bidiFormattingDepth == 0 &&
                    !IsInlineSizeNoBreakAdjacentFormatControl(previousCodepoint) &&
                    !IsInlineSizeNoBreakAdjacentFormatControl(nextCodepoint) &&
                    IsInlineSizeCharacterBreakOpportunity(codepoints, i, lineBreakOptions))
                {
                    if (piece.Length > 0 && pieceIsWhitespace.HasValue)
                    {
                        AppendTextPiece(styleSource, piece.ToString(), pieceIsWhitespace.Value);
                        piece.Clear();
                        pieceIsWhitespace = null;
                    }

                    AppendTextPiece(styleSource, codepoint, isWhitespace: false);
                    FlushSegment();
                    bidiFormattingDepth = UpdateInlineSizeBidiFormattingDepth(bidiFormattingDepth, codepoint);
                    previousCodepoint = codepoint;
                    continue;
                }

                if (pieceIsWhitespace.HasValue && pieceIsWhitespace.Value != codepointIsWhitespace)
                {
                    AppendTextPiece(styleSource, piece.ToString(), pieceIsWhitespace.Value);
                    piece.Clear();
                }

                pieceIsWhitespace = codepointIsWhitespace;
                piece.Append(codepoint);
                bidiFormattingDepth = UpdateInlineSizeBidiFormattingDepth(bidiFormattingDepth, codepoint);
                previousCodepoint = codepoint;
            }

            if (piece.Length > 0 && pieceIsWhitespace.HasValue)
            {
                AppendTextPiece(styleSource, piece.ToString(), pieceIsWhitespace.Value);
            }
        }
    }

    private static bool TryCreateInlineSizeTextPathFlowRun(
        SvgTextPath textPath,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        IDictionary<TextRunAdvanceCacheKey, float>? advanceCache,
        out InlineSizeTextPathFlowRun run)
    {
        run = default;
        if (IsVerticalWritingMode(textPath) ||
            IsRightToLeft(textPath) ||
            HasInlineSizeLayout(textPath) ||
            HasNestedTextPathContent(textPath) ||
            HasRecursiveTextPathReference(textPath) ||
            !TryResolveTextPathGeometry(textPath, viewport, out _, out var skPath, out var textPathGeometryBounds, out var pathSamples, out var pathLength, out var isClosedLoop) ||
            !TryCollectTextPathRuns(textPath, viewport, out var textPathRuns) ||
            textPathRuns.Count == 0 ||
            !TryMeasureInlineSizeTextPathRunsAdvance(textPath, textPathRuns, viewport, textPathGeometryBounds, assetLoader, out var totalAdvance, advanceCache) ||
            totalAdvance <= 0f)
        {
            return false;
        }

        var text = string.Concat(textPathRuns.Select(static textPathRun => textPathRun.Text));
        run = new InlineSizeTextPathFlowRun(
            InlineSizeTextPathFlowRunKind.TextPath,
            textPath,
            text,
            totalAdvance,
            textPath,
            textPathRuns.ToArray(),
            pathSamples,
            isClosedLoop,
            textPathGeometryBounds,
            pathLength,
            ResolveTextPathStartOffset(textPath, skPath, viewport, pathLength));
        return true;
    }

    private static bool TryCreateInlineSizeTextPathFragment(
        SvgTextBase svgTextBase,
        float currentX,
        float currentY,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out InlineSizeTextPathFragment fragment)
    {
        fragment = default;
        if (!TryGetSingleInlineSizeTextPathChild(svgTextBase, out var textPath) ||
            textPath is null ||
            IsVerticalWritingMode(svgTextBase) ||
            IsVerticalWritingMode(textPath) ||
            IsRightToLeft(svgTextBase) ||
            HasOwnTextLengthAdjustment(svgTextBase) ||
            textPath.Method == SvgTextPathMethod.Stretch ||
            ResolveTextOverflowMarker(svgTextBase) is not null ||
            HasActiveShapeTextLayout(svgTextBase) ||
            HasNonNoneCssTextProperty(svgTextBase.ShapeSubtract) ||
            HasInlineSizeLayout(textPath) ||
            HasNestedTextPathContent(textPath) ||
            svgTextBase.Dx.Count > 1 ||
            svgTextBase.Dy.Count > 0 ||
            HasRecursiveTextPathReference(textPath) ||
            !TryResolveTextPathGeometry(textPath, viewport, out _, out var skPath, out var textPathGeometryBounds, out var pathSamples, out var pathLength, out var isClosedLoop) ||
            !TryCollectTextPathRuns(textPath, viewport, out var runs) ||
            runs.Count == 0)
        {
            return false;
        }

        if (HasOwnTextLengthAdjustment(textPath) && runs.Count != 1)
        {
            return false;
        }

        if (!TryMeasureInlineSizeTextPathRunsAdvance(textPath, runs, viewport, textPathGeometryBounds, assetLoader, out var totalAdvance))
        {
            return false;
        }

        if (totalAdvance <= 0f)
        {
            return false;
        }

        ApplyInitialSequentialOffsets(svgTextBase, viewport, ref currentX, ref currentY);

        if (!TryGetInlineSize(svgTextBase, viewport, assetLoader, isVertical: false, out var inlineSize) ||
            inlineSize <= 0f ||
            totalAdvance > inlineSize + TextLengthTolerance)
        {
            return false;
        }

        var textAlign = GetTextAnchorAlign(svgTextBase, geometryBounds);
        var contentStart = GetAlignedStartCoordinate(currentX, inlineSize, textAlign);
        var textPathAlign = GetTextAnchorAlign(textPath, textPathGeometryBounds);
        var anchorX = GetAnchorCoordinateFromAlignedStart(contentStart, totalAdvance, textPathAlign);
        ResolveTextPathChunkOffsets(textPath, useCurrentPositionOffset: true, anchorX, currentY, viewport, assetLoader, pathSamples, out var horizontalOffset, out var verticalOffset);

        var startOffset = horizontalOffset + ResolveTextPathStartOffset(textPath, skPath, viewport, pathLength);
        var pathOffset = ApplyTextAnchor(textPath, startOffset, textPathGeometryBounds, totalAdvance);
        pathOffset = ApplyTextPathSideOffset(textPath, pathOffset, pathLength, totalAdvance);

        fragment = new InlineSizeTextPathFragment(
            textPath,
            runs.ToArray(),
            pathSamples,
            isClosedLoop,
            textPathGeometryBounds,
            pathOffset,
            verticalOffset,
            anchorX,
            currentY,
            contentStart + totalAdvance,
            currentY,
            totalAdvance);
        return true;
    }

    private static bool TryGetSingleInlineSizeTextPathChild(SvgTextBase svgTextBase, out SvgTextPath? textPath)
    {
        textPath = null;
        return TryGetSingleInlineSizeTextPathChild(GetContentNodeList(svgTextBase), ref textPath) && textPath is not null;
    }

    private static bool TryGetSingleInlineSizeTextPathChild(IReadOnlyList<ISvgNode> contentNodes, ref SvgTextPath? textPath)
    {
        for (var i = 0; i < contentNodes.Count; i++)
        {
            var node = contentNodes[i];
            switch (node)
            {
                case SvgTextPath candidate:
                    if (!CanRenderTextSubtree(candidate) ||
                        textPath is not null)
                    {
                        return false;
                    }

                    textPath = candidate;
                    break;

                case SvgAnchor svgAnchor:
                    if (!CanRenderTextSubtree(svgAnchor) ||
                        !TryGetSingleInlineSizeTextPathChild(GetContentNodeList(svgAnchor), ref textPath))
                    {
                        return false;
                    }

                    break;

                case SvgTextSpan svgTextSpan:
                    if (!CanRenderTextSubtree(svgTextSpan) ||
                        HasExplicitTextPositioning(svgTextSpan) ||
                        !TryGetSingleInlineSizeTextPathChild(GetContentNodeList(svgTextSpan), ref textPath))
                    {
                        return false;
                    }

                    break;

                case not SvgTextBase:
                    if (!string.IsNullOrWhiteSpace(node.Content))
                    {
                        return false;
                    }

                    break;

                default:
                    return false;
            }
        }

        return true;
    }

    private static bool HasNestedTextPathContent(SvgTextPath svgTextPath)
    {
        var contentNodes = GetContentNodeList(svgTextPath);
        for (var i = 0; i < contentNodes.Count; i++)
        {
            if (contentNodes[i] is SvgTextPath ||
                contentNodes[i] is SvgTextBase childTextBase && HasNestedTextPathContent(childTextBase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasNestedTextPathContent(SvgTextBase svgTextBase)
    {
        var contentNodes = GetContentNodeList(svgTextBase);
        for (var i = 0; i < contentNodes.Count; i++)
        {
            if (contentNodes[i] is SvgTextPath ||
                contentNodes[i] is SvgTextBase childTextBase && HasNestedTextPathContent(childTextBase))
            {
                return true;
            }
        }

        return false;
    }

    private static float GetAnchorCoordinateFromAlignedStart(float alignedStart, float totalAdvance, SKTextAlign textAlign)
    {
        return textAlign switch
        {
            SKTextAlign.Center => alignedStart + (totalAdvance * 0.5f),
            SKTextAlign.Right => alignedStart + totalAdvance,
            _ => alignedStart
        };
    }

    private static bool TryCreateInlineSizeTextOverflowLayout(
        SvgTextBase svgTextBase,
        IReadOnlyList<SequentialTextRun> runs,
        float currentX,
        float currentY,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out InlineSizeTextOverflowLayout? layout)
    {
        layout = null;
        if (!CanUseInlineSizeTextOverflowLayout(svgTextBase, runs, geometryBounds, assetLoader) ||
            !TryPrepareInlineSizeSequentialTextRuns(svgTextBase, runs, geometryBounds, assetLoader, out var preparedText) ||
            preparedText is null)
        {
            return false;
        }

        ApplyInitialSequentialOffsets(svgTextBase, viewport, ref currentX, ref currentY);

        var textAlign = GetTextAnchorAlign(svgTextBase, geometryBounds);
        if (!TryCreateInlineSizeTextArea(
                svgTextBase,
                preparedText.Runs,
                currentX,
                currentY,
                viewport,
                geometryBounds,
                assetLoader,
                textAlign,
                out var textArea,
                out var firstLineBlockCoordinate) ||
            textArea is null ||
            GetInlineSizeTextAreaExtent(textArea) <= 0f)
        {
            return false;
        }

        var allowsWrapping = AllowsInlineSizeWrapping(svgTextBase, preparedText.Runs);
        return allowsWrapping
            ? TryCreateWrappedInlineSizeTextLayout(
                svgTextBase,
                preparedText.Runs,
                textArea,
                firstLineBlockCoordinate,
                textAlign,
                geometryBounds,
                assetLoader,
                out layout)
            : TryCreateSingleLineInlineSizeTextLayout(
                svgTextBase,
                preparedText.Runs,
                preparedText.TotalAdvance,
                textArea,
                firstLineBlockCoordinate,
                textAlign,
                geometryBounds,
                assetLoader,
                out layout);
    }

    private static bool TryPrepareInlineSizeSequentialTextRuns(
        SvgTextBase svgTextBase,
        IReadOnlyList<SequentialTextRun> runs,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out PreparedSequentialText? preparedText)
    {
        var canIgnoreRootTextLength = HasOwnTextLengthAdjustment(svgTextBase) &&
                                      HasExplicitLineBreakRun(runs);
        for (var i = 0; i < runs.Count; i++)
        {
            if (!CanPrepareInlineSizeTextRun(
                    runs[i],
                    geometryBounds,
                    assetLoader,
                    canIgnoreRootTextLength && ReferenceEquals(runs[i].StyleSource, svgTextBase)))
            {
                preparedText = null;
                return false;
            }
        }

        if (runs.Count == 0 ||
            !s_preparedTextEngine.TryPrepareSequentialText(runs, geometryBounds, assetLoader, out var prepared) ||
            prepared.Runs.Count != runs.Count)
        {
            preparedText = null;
            return false;
        }

        preparedText = prepared;
        return true;
    }

    private static float GetInlineSizeTextAreaExtent(InlineSizeTextArea textArea)
    {
        return textArea.IsVertical ? textArea.Bounds.Height : textArea.Bounds.Width;
    }

    private static InlineSizeFlow GetInlineSizeFlow(SvgTextBase svgTextBase)
    {
        if (IsVerticalWritingMode(svgTextBase))
        {
            return IsVerticalLeftToRightBlockFlow(svgTextBase)
                ? InlineSizeFlow.VerticalLeftToRightColumns
                : InlineSizeFlow.VerticalRightToLeftColumns;
        }

        return IsRightToLeft(svgTextBase)
            ? InlineSizeFlow.HorizontalRightToLeft
            : InlineSizeFlow.HorizontalLeftToRight;
    }

    private static bool IsVerticalInlineSizeFlow(InlineSizeFlow flow)
    {
        return flow is InlineSizeFlow.VerticalRightToLeftColumns or InlineSizeFlow.VerticalLeftToRightColumns;
    }

    private static bool IsHorizontalRightToLeftInlineSizeFlow(InlineSizeFlow flow)
    {
        return flow == InlineSizeFlow.HorizontalRightToLeft;
    }

    private static int GetInlineSizeBlockProgression(InlineSizeFlow flow)
    {
        return flow == InlineSizeFlow.VerticalRightToLeftColumns ? -1 : 1;
    }

    private static bool IsVerticalLeftToRightBlockFlow(SvgTextBase svgTextBase)
    {
        var writingMode = GetInheritedTextAttribute(svgTextBase, "writing-mode");
        return string.Equals(writingMode?.Trim(), "vertical-lr", StringComparison.OrdinalIgnoreCase);
    }

    private static float SumInlineSizeTextRunAdvances(IReadOnlyList<InlineSizeTextRun> runs)
    {
        var advance = 0f;
        for (var i = 0; i < runs.Count; i++)
        {
            advance += runs[i].Advance;
        }

        return advance;
    }

    private static InlineSizeTextRun[] CreateVisualInlineSizeTextRuns(
        SvgTextBase svgTextBase,
        IReadOnlyList<InlineSizeTextRun> logicalRuns,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        if (!NeedsInlineSizeBidiOrdering(svgTextBase, logicalRuns))
        {
            return logicalRuns.ToArray();
        }

        var combinedText = string.Concat(logicalRuns.Select(static run => run.Text));
        if (!CanUseInlineSizeBidiOrdering(svgTextBase, combinedText))
        {
            return logicalRuns.ToArray();
        }

        var baseDirection = IsRightToLeft(svgTextBase) ? SvgTextDirection.RightToLeft : SvgTextDirection.LeftToRight;
        var bidiRuns = CreateLogicalBidiRuns(svgTextBase, combinedText, baseDirection, logicalRuns);
        if (bidiRuns.Count <= 1)
        {
            return logicalRuns.ToArray();
        }

        var runEndIndices = new int[logicalRuns.Count];
        var charIndex = 0;
        for (var i = 0; i < logicalRuns.Count; i++)
        {
            charIndex += logicalRuns[i].Text.Length;
            runEndIndices[i] = charIndex;
        }

        var visualRuns = new List<InlineSizeTextRun>(logicalRuns.Count);
        foreach (var bidiRun in bidiRuns)
        {
            var startCharIndex = bidiRun.StartCharIndex;
            var endCharIndex = startCharIndex + bidiRun.Length;
            while (startCharIndex < endCharIndex)
            {
                var runIndex = GetSequentialRunIndex(runEndIndices, startCharIndex);
                var previousRunEnd = runIndex == 0 ? 0 : runEndIndices[runIndex - 1];
                var chunkEndCharIndex = Math.Min(endCharIndex, runEndIndices[runIndex]);
                if (chunkEndCharIndex <= startCharIndex)
                {
                    break;
                }

                var sourceRun = logicalRuns[runIndex];
                var chunk = sourceRun.Text.Substring(startCharIndex - previousRunEnd, chunkEndCharIndex - startCharIndex);
                if (!string.IsNullOrEmpty(chunk))
                {
                    var advance = MeasureTextAdvance(sourceRun.StyleSource, chunk, geometryBounds, assetLoader);
                    visualRuns.Add(new InlineSizeTextRun(sourceRun.StyleSource, chunk, advance, startCharIndex));
                }

                startCharIndex = chunkEndCharIndex;
            }
        }

        return visualRuns.Count > 0 ? visualRuns.ToArray() : logicalRuns.ToArray();
    }

    private static bool ShouldPlacePlainTextInlineSizeLineRightToLeft(
        SvgTextBase svgTextBase,
        bool isVertical,
        IReadOnlyList<InlineSizeTextRun> logicalRuns)
    {
        if (isVertical ||
            SvgTextBidiResolver.ResolveUnicodeBidi(svgTextBase) != SvgUnicodeBidiMode.PlainText)
        {
            return false;
        }

        for (var runIndex = 0; runIndex < logicalRuns.Count; runIndex++)
        {
            var text = logicalRuns[runIndex].Text;
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            var codepoints = SplitCodepointsReadOnly(text);
            for (var i = 0; i < codepoints.Count; i++)
            {
                var direction = SvgTextBidiResolver.GetStrongDirection(codepoints[i]);
                if (direction != 0)
                {
                    return direction < 0;
                }
            }
        }

        return false;
    }

    private static float GetInlineSizeVisualRunCursorX(InlineSizeTextLine line)
    {
        return line.PlaceVisualRunsRightToLeft
            ? line.StartX + SumInlineSizeTextRunAdvances(line.VisualRuns)
            : line.StartX;
    }

    private static float TakeInlineSizeVisualRunX(InlineSizeTextLine line, InlineSizeTextRun run, ref float cursorX)
    {
        if (line.PlaceVisualRunsRightToLeft)
        {
            cursorX -= run.Advance;
            return cursorX;
        }

        var runX = cursorX;
        cursorX += run.Advance;
        return runX;
    }

    private static bool NeedsInlineSizeBidiOrdering(SvgTextBase svgTextBase, IReadOnlyList<InlineSizeTextRun> logicalRuns)
    {
        if (logicalRuns.Count == 0)
        {
            return false;
        }

        var combinedText = string.Concat(logicalRuns.Select(static run => run.Text));
        if (SvgTextBidiResolver.NeedsVisualOrdering(svgTextBase, combinedText))
        {
            return true;
        }

        var baseDirection = IsRightToLeft(svgTextBase) ? SvgTextDirection.RightToLeft : SvgTextDirection.LeftToRight;
        var paragraphMode = SvgTextBidiResolver.ResolveUnicodeBidi(svgTextBase);
        for (var i = 0; i < logicalRuns.Count; i++)
        {
            var mode = SvgTextBidiResolver.ResolveUnicodeBidi(logicalRuns[i].StyleSource);
            if (mode == SvgUnicodeBidiMode.Normal)
            {
                continue;
            }

            var direction = SvgTextBidiResolver.ResolveDirection(logicalRuns[i].StyleSource);
            if (mode != paragraphMode || direction != baseDirection)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCreateSingleLineInlineSizeTextLayout(
        SvgTextBase svgTextBase,
        IReadOnlyList<PreparedSequentialRun> preparedRuns,
        float totalAdvance,
        InlineSizeTextArea textArea,
        float firstLineBlockCoordinate,
        SKTextAlign textAlign,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out InlineSizeTextOverflowLayout? layout)
    {
        layout = null;
        var flow = GetInlineSizeFlow(svgTextBase);
        var isVertical = IsVerticalInlineSizeFlow(flow);
        var lineAdvance = GetInlineSizeLineAdvance(preparedRuns, geometryBounds, assetLoader);
        var area = textArea.IsShapeInside
            ? textArea.ResolveWrappedLineArea(0, firstLineBlockCoordinate, lineAdvance, GetInlineSizeBlockProgression(flow))
            : textArea.ResolveLineArea(firstLineBlockCoordinate, lineAdvance);
        if (area.InlineSize <= 0f)
        {
            return false;
        }

        var contentStart = area.Start;
        var inlineSize = area.InlineSize;
        var inlineDirection = isVertical ? area.InlineProgression : 1;
        var overflows = totalAdvance > inlineSize + TextLengthTolerance;
        if (!overflows && textAlign == SKTextAlign.Left && !textArea.IsShapeInside && !textArea.HasShapeSubtract)
        {
            return false;
        }

        var marker = overflows ? ResolveTextOverflowMarker(svgTextBase) : null;
        var layoutRuns = marker is null
            ? CreateInlineSizeTextRuns(preparedRuns)
            : CreateEllipsizedInlineSizeTextRuns(preparedRuns, inlineSize, marker, geometryBounds, assetLoader);
        if (layoutRuns.Length == 0)
        {
            return false;
        }

        var renderedAdvance = SumInlineSizeTextRunAdvances(layoutRuns);
        var blockCoordinate = area.BlockCoordinate;
        var drawX = isVertical
            ? blockCoordinate
            : IsHorizontalRightToLeftInlineSizeFlow(flow)
                ? contentStart + inlineSize - renderedAdvance
                : contentStart;
        var drawY = isVertical
            ? inlineDirection < 0 ? contentStart + inlineSize : contentStart
            : blockCoordinate;
        var clipRect = overflows
            ? CreateInlineSizeClipRect(svgTextBase, layoutRuns, drawX, drawY, contentStart, inlineSize, geometryBounds, assetLoader)
            : SKRect.Empty;
        if (overflows && clipRect.IsEmpty)
        {
            return false;
        }

        var visualRuns = CreateVisualInlineSizeTextRuns(svgTextBase, layoutRuns, geometryBounds, assetLoader);
        var placeVisualRunsRightToLeft = ShouldPlacePlainTextInlineSizeLineRightToLeft(svgTextBase, isVertical, layoutRuns);
        var line = new InlineSizeTextLine(layoutRuns, visualRuns, clipRect, drawX, drawY, totalAdvance, placeVisualRunsRightToLeft, overflows);
        layout = new InlineSizeTextOverflowLayout(
            new[] { line },
            isVertical ? drawX : drawX + totalAdvance,
            isVertical ? drawY + (totalAdvance * inlineDirection) : drawY);
        return true;
    }

    private static bool TryCreateWrappedInlineSizeTextLayout(
        SvgTextBase svgTextBase,
        IReadOnlyList<PreparedSequentialRun> preparedRuns,
        InlineSizeTextArea textArea,
        float firstLineBlockCoordinate,
        SKTextAlign textAlign,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out InlineSizeTextOverflowLayout? layout)
    {
        layout = null;
        var segments = CreateInlineSizeWrapSegments(preparedRuns, geometryBounds, assetLoader);
        if (segments.Count == 0)
        {
            return false;
        }

        var flow = GetInlineSizeFlow(svgTextBase);
        var isVertical = IsVerticalInlineSizeFlow(flow);
        var blockProgression = GetInlineSizeBlockProgression(flow);
        var lineAdvance = GetInlineSizeLineAdvance(preparedRuns, geometryBounds, assetLoader);
        var maxLineSearchCount = textArea.GetMaxWrappedLineSearchCount(firstLineBlockCoordinate, lineAdvance, segments.Count, blockProgression);
        if (maxLineSearchCount <= 0)
        {
            return false;
        }

        var logicalLines = CreateWrappedInlineSizeLogicalLines(
            segments,
            lineIndex => textArea.ResolveWrappedLineArea(lineIndex, firstLineBlockCoordinate, lineAdvance, blockProgression),
            maxLineSearchCount,
            PreservesLineEdgeWhitespace(preparedRuns));
        if (logicalLines.Count == 0)
        {
            return false;
        }

        var lines = new List<InlineSizeTextLine>(logicalLines.Count);
        var marker = ResolveTextOverflowMarker(svgTextBase);
        var finalX = textArea.Bounds.Left;
        var finalY = firstLineBlockCoordinate;
        var hasClippedLine = false;
        for (var i = 0; i < logicalLines.Count; i++)
        {
            var logicalLine = logicalLines[i];
            var contentStart = logicalLine.Area.Start;
            var inlineSize = logicalLine.Area.InlineSize;
            var inlineDirection = isVertical ? logicalLine.Area.InlineProgression : 1;
            if (inlineSize <= 0f)
            {
                continue;
            }

            var currentBlockCoordinate = logicalLine.Area.BlockCoordinate;
            var lineOverflows = logicalLine.Advance > inlineSize + TextLengthTolerance;
            var lineRuns = lineOverflows && marker is not null
                ? CreateEllipsizedInlineSizeTextRuns(logicalLine.Runs, inlineSize, marker, geometryBounds, assetLoader)
                : logicalLine.Runs;
            if (lineRuns.Length == 0)
            {
                continue;
            }

            var renderedAdvance = SumInlineSizeTextRunAdvances(lineRuns);
            var drawX = isVertical
                ? currentBlockCoordinate
                : IsHorizontalRightToLeftInlineSizeFlow(flow)
                    ? contentStart + inlineSize - renderedAdvance
                    : contentStart;
            var drawY = isVertical
                ? inlineDirection < 0 ? contentStart + inlineSize : contentStart
                : currentBlockCoordinate;
            var clipRect = lineOverflows
                ? CreateInlineSizeClipRect(svgTextBase, lineRuns, drawX, drawY, contentStart, inlineSize, geometryBounds, assetLoader)
                : SKRect.Empty;
            if (lineOverflows && clipRect.IsEmpty)
            {
                return false;
            }

            var visualLineRuns = CreateVisualInlineSizeTextRuns(svgTextBase, lineRuns, geometryBounds, assetLoader);
            var placeVisualRunsRightToLeft = ShouldPlacePlainTextInlineSizeLineRightToLeft(svgTextBase, isVertical, lineRuns);
            lines.Add(new InlineSizeTextLine(lineRuns, visualLineRuns, clipRect, drawX, drawY, logicalLine.Advance, placeVisualRunsRightToLeft, lineOverflows));
            hasClippedLine |= lineOverflows;
            finalX = isVertical ? drawX : drawX + logicalLine.Advance;
            finalY = isVertical ? drawY + (logicalLine.Advance * inlineDirection) : drawY;
        }

        if (lines.Count == 0 ||
            (lines.Count == 1 && !hasClippedLine && textAlign == SKTextAlign.Left && !textArea.IsShapeInside && !textArea.HasShapeSubtract))
        {
            return false;
        }

        layout = new InlineSizeTextOverflowLayout(lines.ToArray(), finalX, finalY);
        return true;
    }

    private static bool TryCreateWrappedInlineSizeTextLengthLayout(
        SvgTextBase svgTextBase,
        float currentX,
        float currentY,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        bool trimLeadingWhitespaceAtStart,
        out WrappedInlineSizeTextLengthLayout? layout)
    {
        layout = null;
        if (!CanUseWrappedInlineSizeTextLengthLayout(svgTextBase) ||
            !TryGetOwnTextLength(svgTextBase, viewport, isVertical: false, out var specifiedLength) ||
            specifiedLength <= 0f ||
            !TryCollectFlattenedTextCodepoints(
                svgTextBase,
                trimLeadingWhitespaceAtStart,
                viewport,
                assetLoader,
                applyRootPositions: false,
                preservePreLineBreaks: PreservesInlineLineBreaksInTextSubtree(svgTextBase),
                out var flattenedCodepoints) ||
            flattenedCodepoints.Count == 0 ||
            ContainsFlattenedAbsolutePositions(flattenedCodepoints) ||
            !AllowsInlineSizeWrapping(flattenedCodepoints))
        {
            return false;
        }

        var combinedText = CreateFlattenedText(flattenedCodepoints, 0, flattenedCodepoints.Count);
        if (ContainsMixedStrongDirections(combinedText))
        {
            return false;
        }

        var naturalAdvances = MeasureFlattenedNaturalAdvances(flattenedCodepoints, geometryBounds, assetLoader);
        var preparedRuns = new PreparedSequentialRun[flattenedCodepoints.Count];
        for (var i = 0; i < flattenedCodepoints.Count; i++)
        {
            preparedRuns[i] = new PreparedSequentialRun(flattenedCodepoints[i].StyleSource, flattenedCodepoints[i].Codepoint, naturalAdvances[i]);
        }

        ApplyInitialSequentialOffsets(svgTextBase, viewport, ref currentX, ref currentY);

        var textAlign = GetTextAnchorAlign(svgTextBase, geometryBounds);
        if (!TryCreateInlineSizeTextArea(
                svgTextBase,
                preparedRuns,
                currentX,
                currentY,
                viewport,
                geometryBounds,
                assetLoader,
                textAlign,
                out var textArea,
                out var firstLineBlockCoordinate) ||
            textArea is null ||
            textArea.IsVertical ||
            GetInlineSizeTextAreaExtent(textArea) <= 0f)
        {
            return false;
        }

        var segments = CreateInlineSizeWrapSegments(flattenedCodepoints, naturalAdvances);
        if (segments.Count == 0)
        {
            return false;
        }

        var lineAdvance = GetInlineSizeLineAdvance(preparedRuns, geometryBounds, assetLoader);
        var maxLineSearchCount = textArea.GetMaxWrappedLineSearchCount(firstLineBlockCoordinate, lineAdvance, segments.Count, blockProgression: 1);
        if (maxLineSearchCount <= 0)
        {
            return false;
        }

        var logicalLines = CreateWrappedInlineSizeLogicalLines(
            segments,
            lineIndex => textArea.ResolveWrappedLineArea(lineIndex, firstLineBlockCoordinate, lineAdvance, blockProgression: 1),
            maxLineSearchCount,
            PreservesLineEdgeWhitespace(flattenedCodepoints));
        if (logicalLines.Count <= 1)
        {
            return false;
        }

        var targetLineLengths = CreateWrappedTextLengthLineTargets(
            logicalLines,
            flattenedCodepoints,
            naturalAdvances,
            geometryBounds,
            specifiedLength);
        var lines = new List<WrappedInlineSizeTextLengthLine>(logicalLines.Count);
        var finalX = currentX;
        var finalY = currentY;
        for (var i = 0; i < logicalLines.Count; i++)
        {
            var logicalLine = logicalLines[i];
            if (logicalLine.Area.InlineSize <= 0f)
            {
                continue;
            }

            var lineRuns = logicalLine.Runs;
            var sourceRunCount = CountSourceCodepointRuns(lineRuns);
            if (sourceRunCount == 0)
            {
                continue;
            }

            var lineNaturalAdvance = GetWrappedTextLengthLineNaturalAdvance(lineRuns, flattenedCodepoints, naturalAdvances, geometryBounds);
            var targetLineLength = i < targetLineLengths.Length ? targetLineLengths[i] : lineNaturalAdvance;
            var lengthAdjust = GetOwnLengthAdjust(svgTextBase);
            var scaleGlyphs = lengthAdjust == SvgTextLengthAdjust.SpacingAndGlyphs;
            var glyphScaleX = scaleGlyphs && lineNaturalAdvance > 0f
                ? targetLineLength / lineNaturalAdvance
                : 1f;
            var extraGapAdvance = !scaleGlyphs && sourceRunCount > 1
                ? (targetLineLength - lineNaturalAdvance) / (sourceRunCount - 1)
                : 0f;
            if (scaleGlyphs && (!IsValidPositiveAdvance(lineNaturalAdvance) || glyphScaleX <= 0f))
            {
                return false;
            }

            var drawX = logicalLine.Area.Start;
            var drawY = logicalLine.Area.BlockCoordinate;
            if (!TryCreateWrappedTextLengthLineRuns(
                    lineRuns,
                    flattenedCodepoints,
                    naturalAdvances,
                    drawX,
                    drawY,
                    geometryBounds,
                    extraGapAdvance,
                    glyphScaleX,
                    scaleGlyphs,
                    out var positionedRuns))
            {
                return false;
            }

            lines.Add(new WrappedInlineSizeTextLengthLine(positionedRuns, drawX, drawY, targetLineLength));
            finalX = drawX + targetLineLength;
            finalY = drawY;
        }

        if (lines.Count <= 1)
        {
            return false;
        }

        layout = new WrappedInlineSizeTextLengthLayout(lines.ToArray(), finalX, finalY);
        return true;
    }

    private static bool CanUseWrappedInlineSizeTextLengthLayout(SvgTextBase svgTextBase)
    {
        return HasOwnTextLengthAdjustment(svgTextBase) &&
               HasExplicitInlineSizeLayout(svgTextBase) &&
               GetOwnLengthAdjust(svgTextBase) is SvgTextLengthAdjust.Spacing or SvgTextLengthAdjust.SpacingAndGlyphs &&
               AllowsInlineSizeWrapping(svgTextBase) &&
               !IsVerticalWritingMode(svgTextBase) &&
               !IsRightToLeft(svgTextBase) &&
               !HasRotateValues(svgTextBase) &&
               !HasNonBaselineShift(svgTextBase) &&
               !HasUnsupportedRootWrappedTextLengthPositioning(svgTextBase) &&
               !HasActiveShapeTextLayout(svgTextBase) &&
               !HasAbsolutelyPositionedDescendantTextChunk(svgTextBase) &&
               !ContainsUnsupportedFlattenedTextLengthContent(svgTextBase) &&
               ResolveTextOverflowMarker(svgTextBase) is null;
    }

    private static float[] CreateWrappedTextLengthLineTargets(
        IReadOnlyList<InlineSizeLogicalLine> logicalLines,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        SKRect geometryBounds,
        float specifiedLength)
    {
        var targets = new float[logicalLines.Count];
        if (logicalLines.Count == 0 || specifiedLength <= 0f)
        {
            return targets;
        }

        var naturalLineAdvances = new float[logicalLines.Count];
        var totalNaturalAdvance = 0f;
        for (var i = 0; i < logicalLines.Count; i++)
        {
            var naturalAdvance = GetWrappedTextLengthLineNaturalAdvance(logicalLines[i].Runs, codepoints, naturalAdvances, geometryBounds);
            naturalLineAdvances[i] = Math.Max(0f, naturalAdvance);
            totalNaturalAdvance += naturalLineAdvances[i];
        }

        if (totalNaturalAdvance <= TextLengthTolerance)
        {
            var equalTarget = specifiedLength / logicalLines.Count;
            for (var i = 0; i < targets.Length; i++)
            {
                targets[i] = equalTarget;
            }

            return targets;
        }

        var assigned = 0f;
        var lastNonEmptyLine = -1;
        for (var i = 0; i < targets.Length; i++)
        {
            if (naturalLineAdvances[i] <= 0f)
            {
                targets[i] = 0f;
                continue;
            }

            lastNonEmptyLine = i;
            targets[i] = specifiedLength * naturalLineAdvances[i] / totalNaturalAdvance;
            assigned += targets[i];
        }

        if (lastNonEmptyLine >= 0)
        {
            targets[lastNonEmptyLine] += specifiedLength - assigned;
        }

        return targets;
    }

    private static bool HasUnsupportedRootWrappedTextLengthPositioning(SvgTextBase svgTextBase)
    {
        return svgTextBase.X.Count > 1 ||
               svgTextBase.Y.Count > 1 ||
               svgTextBase.Dx.Count > 1 ||
               svgTextBase.Dy.Count > 1;
    }

    private static bool ContainsFlattenedAbsolutePositions(IReadOnlyList<FlattenedTextCodepoint> codepoints)
    {
        for (var i = 0; i < codepoints.Count; i++)
        {
            if (codepoints[i].X.HasValue || codepoints[i].Y.HasValue)
            {
                return true;
            }
        }

        return false;
    }

    private static bool AllowsInlineSizeWrapping(IReadOnlyList<FlattenedTextCodepoint> codepoints)
    {
        for (var i = 0; i < codepoints.Count; i++)
        {
            if (!AllowsInlineSizeWrapping(codepoints[i].StyleSource))
            {
                return false;
            }
        }

        return true;
    }

    private static bool PreservesLineEdgeWhitespace(IReadOnlyList<FlattenedTextCodepoint> codepoints)
    {
        for (var i = 0; i < codepoints.Count; i++)
        {
            if (PreservesLineEdgeWhitespace(codepoints[i].StyleSource))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountSourceCodepointRuns(IReadOnlyList<InlineSizeTextRun> runs)
    {
        var count = 0;
        for (var i = 0; i < runs.Count; i++)
        {
            if (runs[i].SourceCodepointIndex >= 0)
            {
                count++;
            }
        }

        return count;
    }

    private static float GetWrappedTextLengthLineNaturalAdvance(
        IReadOnlyList<InlineSizeTextRun> runs,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        SKRect geometryBounds)
    {
        var advance = 0f;
        var sourceRunCount = CountSourceCodepointRuns(runs);
        var sourceRunIndex = 0;
        for (var i = 0; i < runs.Count; i++)
        {
            var sourceIndex = runs[i].SourceCodepointIndex;
            if (sourceIndex < 0 || sourceIndex >= codepoints.Count)
            {
                continue;
            }

            advance += naturalAdvances[sourceIndex];
            if (sourceRunIndex < sourceRunCount - 1)
            {
                advance += GetInterCodepointSpacingAdvance(codepoints[sourceIndex], naturalAdvances[sourceIndex], geometryBounds);
            }

            sourceRunIndex++;
        }

        return advance;
    }

    private static float GetInterCodepointSpacingAdvance(
        FlattenedTextCodepoint codepoint,
        float naturalAdvance,
        SKRect geometryBounds)
    {
        var styleSource = codepoint.StyleSource;
        var spacing = 0f;
        if (SupportsLetterSpacing(codepoint.Codepoint))
        {
            spacing += ResolveSpacingValue(styleSource, styleSource.LetterSpacing, geometryBounds, naturalAdvance);
        }

        if (IsWhitespaceCodepoint(codepoint.Codepoint))
        {
            spacing += ResolveSpacingValue(styleSource, styleSource.WordSpacing, geometryBounds, naturalAdvance);
        }

        return spacing;
    }

    private static bool TryCreateWrappedTextLengthLineRuns(
        IReadOnlyList<InlineSizeTextRun> lineRuns,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        float drawX,
        float drawY,
        SKRect geometryBounds,
        float extraGapAdvance,
        float glyphScaleX,
        bool scaleGlyphs,
        out PositionedCodepointRun[] positionedRuns)
    {
        positionedRuns = Array.Empty<PositionedCodepointRun>();
        var sourceRunCount = CountSourceCodepointRuns(lineRuns);
        if (sourceRunCount == 0)
        {
            return false;
        }

        var groupCount = 0;
        var groupSearchStart = 0;
        while (TryGetNextWrappedTextLengthGroup(
                   lineRuns,
                   codepoints,
                   ref groupSearchStart,
                   out _,
                   out _,
                   out _,
                   out _))
        {
            groupCount++;
        }

        if (groupCount == 0)
        {
            return false;
        }

        var groups = new WrappedTextLengthRunGroup[groupCount];
        groupSearchStart = 0;
        var groupIndex = 0;
        while (TryGetNextWrappedTextLengthGroup(
                   lineRuns,
                   codepoints,
                   ref groupSearchStart,
                   out var groupStart,
                   out var groupEnd,
                   out var groupStyleSource,
                   out var renderedCount))
        {
            groups[groupIndex] = new WrappedTextLengthRunGroup(groupStyleSource, groupStart, groupEnd, renderedCount);
            groupIndex++;
        }

        var currentOffset = 0f;
        var currentYOffset = 0f;
        var sourceRunIndex = 0;
        groupIndex = 0;
        for (var i = 0; i < lineRuns.Count; i++)
        {
            var sourceIndex = lineRuns[i].SourceCodepointIndex;
            if (sourceIndex < 0 || sourceIndex >= codepoints.Count)
            {
                continue;
            }

            while (groupIndex < groups.Length && i >= groups[groupIndex].End)
            {
                groupIndex++;
            }

            if (groupIndex >= groups.Length)
            {
                return false;
            }

            var codepoint = codepoints[sourceIndex];
            currentOffset += codepoint.Dx;
            currentYOffset += codepoint.Dy;
            var placementX = drawX + currentOffset;
            var placementY = drawY + currentYOffset;
            var placement = new PositionedCodepointPlacement(
                new SKPoint(placementX, placementY),
                0f,
                glyphScaleX,
                scaleGlyphs ? drawX : placementX);

            ref var group = ref groups[groupIndex];
            group.AddPlacement(codepoint.Codepoint.Length, placement);

            if (sourceRunIndex < sourceRunCount - 1)
            {
                var clusterAdvance = scaleGlyphs
                    ? naturalAdvances[sourceIndex] * glyphScaleX
                    : naturalAdvances[sourceIndex] + GetInterCodepointSpacingAdvance(codepoint, naturalAdvances[sourceIndex], geometryBounds) + extraGapAdvance;
                currentOffset += IsValidPositiveAdvance(clusterAdvance) ? clusterAdvance : 0f;
            }

            sourceRunIndex++;
        }

        var groupedRuns = new PositionedCodepointRun[groups.Length];
        for (var i = 0; i < groups.Length; i++)
        {
            var group = groups[i];
            var text = CreateWrappedTextLengthRunText(lineRuns, codepoints, group.Start, group.End, group.CharCount);
            groupedRuns[i] = new PositionedCodepointRun(group.StyleSource, text, group.Placements);
        }

        positionedRuns = groupedRuns;
        return true;
    }

    private static bool TryGetNextWrappedTextLengthGroup(
        IReadOnlyList<InlineSizeTextRun> lineRuns,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        ref int start,
        out int groupStart,
        out int groupEnd,
        out SvgTextBase styleSource,
        out int renderedCount)
    {
        while (start < lineRuns.Count &&
               !IsValidWrappedTextLengthSourceIndex(lineRuns[start].SourceCodepointIndex, codepoints.Count))
        {
            start++;
        }

        if (start >= lineRuns.Count)
        {
            groupStart = 0;
            groupEnd = 0;
            styleSource = null!;
            renderedCount = 0;
            return false;
        }

        groupStart = start;
        var sourceIndex = lineRuns[start].SourceCodepointIndex;
        styleSource = codepoints[sourceIndex].StyleSource;
        renderedCount = 0;

        while (start < lineRuns.Count)
        {
            sourceIndex = lineRuns[start].SourceCodepointIndex;
            if (!IsValidWrappedTextLengthSourceIndex(sourceIndex, codepoints.Count))
            {
                start++;
                continue;
            }

            if (!ReferenceEquals(codepoints[sourceIndex].StyleSource, styleSource))
            {
                break;
            }

            renderedCount++;
            start++;
        }

        groupEnd = start;
        return renderedCount > 0;
    }

    private static string CreateWrappedTextLengthRunText(
        IReadOnlyList<InlineSizeTextRun> lineRuns,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        int start,
        int end,
        int charCount)
    {
        if (charCount == 0)
        {
            return string.Empty;
        }

#if NET6_0_OR_GREATER
        return string.Create(
            charCount,
            (LineRuns: lineRuns, Codepoints: codepoints, Start: start, End: end),
            static (destination, state) =>
            {
                var offset = 0;
                for (var i = state.Start; i < state.End; i++)
                {
                    var sourceIndex = state.LineRuns[i].SourceCodepointIndex;
                    if (!IsValidWrappedTextLengthSourceIndex(sourceIndex, state.Codepoints.Count))
                    {
                        continue;
                    }

                    var codepoint = state.Codepoints[sourceIndex].Codepoint.AsSpan();
                    codepoint.CopyTo(destination.Slice(offset));
                    offset += codepoint.Length;
                }
            });
#else
        var chars = new char[charCount];
        var offset = 0;
        for (var i = start; i < end; i++)
        {
            var sourceIndex = lineRuns[i].SourceCodepointIndex;
            if (!IsValidWrappedTextLengthSourceIndex(sourceIndex, codepoints.Count))
            {
                continue;
            }

            var codepoint = codepoints[sourceIndex].Codepoint;
            codepoint.CopyTo(0, chars, offset, codepoint.Length);
            offset += codepoint.Length;
        }

        return new string(chars);
#endif
    }

    private static bool IsValidWrappedTextLengthSourceIndex(int sourceIndex, int codepointCount)
    {
        return sourceIndex >= 0 && sourceIndex < codepointCount;
    }

    private static bool AllowsInlineSizeWrapping(SvgTextBase svgTextBase, IReadOnlyList<PreparedSequentialRun> runs)
    {
        if (!AllowsInlineSizeWrapping(svgTextBase))
        {
            return false;
        }

        for (var i = 0; i < runs.Count; i++)
        {
            if (!AllowsInlineSizeWrapping(runs[i].StyleSource))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllowsInlineSizeWrapping(SvgTextBase svgTextBase)
    {
        return GetInlineSizeWhiteSpaceModel(svgTextBase).AllowsSoftWrapping;
    }

    private static SvgWhiteSpace GetInlineSizeWhiteSpace(SvgTextBase svgTextBase)
    {
        return HasDeclaredWhiteSpace(svgTextBase) &&
               svgTextBase.ComputedStyle.TryGetWhiteSpace(out var whiteSpace)
            ? whiteSpace
            : svgTextBase.WhiteSpace;
    }

    private static SvgTextWhiteSpaceModel GetInlineSizeWhiteSpaceModel(SvgTextBase svgTextBase)
    {
        var whiteSpace = GetInlineSizeWhiteSpace(svgTextBase);
        if (!HasDeclaredWhiteSpace(svgTextBase))
        {
            return SvgTextWhiteSpaceModel.FromLegacy(whiteSpace);
        }

        var collapse = ParseWhiteSpaceCollapse(svgTextBase.ComputedStyle.WhiteSpaceCollapse);
        var wrapMode = ParseTextWrapMode(svgTextBase.ComputedStyle.TextWrapMode);
        var trim = ParseWhiteSpaceTrim(svgTextBase.ComputedStyle.WhiteSpaceTrim);
        return new SvgTextWhiteSpaceModel(collapse, wrapMode, trim);
    }

    private static bool PreservesLineEdgeWhitespace(SvgWhiteSpace whiteSpace)
    {
        return SvgTextWhiteSpaceModel.FromLegacy(whiteSpace).PreservesLineEdgeWhitespace;
    }

    private static bool PreservesLineEdgeWhitespace(SvgTextBase svgTextBase)
    {
        return GetInlineSizeWhiteSpaceModel(svgTextBase).PreservesLineEdgeWhitespace;
    }

    private static bool PreservesLineEdgeWhitespace(IReadOnlyList<PreparedSequentialRun> runs)
    {
        for (var i = 0; i < runs.Count; i++)
        {
            if (PreservesLineEdgeWhitespace(runs[i].StyleSource))
            {
                return true;
            }
        }

        return false;
    }

    private static SvgTextWhiteSpaceCollapseMode ParseWhiteSpaceCollapse(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "preserve" => SvgTextWhiteSpaceCollapseMode.Preserve,
            "preserve-breaks" => SvgTextWhiteSpaceCollapseMode.PreserveBreaks,
            "break-spaces" => SvgTextWhiteSpaceCollapseMode.BreakSpaces,
            "preserve-spaces" => SvgTextWhiteSpaceCollapseMode.PreserveSpaces,
            "discard" => SvgTextWhiteSpaceCollapseMode.Discard,
            _ => SvgTextWhiteSpaceCollapseMode.Collapse
        };
    }

    private static SvgTextWrapMode ParseTextWrapMode(string? value)
    {
        return string.Equals(value?.Trim(), "nowrap", StringComparison.OrdinalIgnoreCase)
            ? SvgTextWrapMode.NoWrap
            : SvgTextWrapMode.Wrap;
    }

    private static SvgTextWhiteSpaceTrimMode ParseWhiteSpaceTrim(string? value)
    {
        var trim = SvgTextWhiteSpaceTrimMode.None;
        if (string.IsNullOrWhiteSpace(value))
        {
            return trim;
        }

        var tokens = value!.Split([' ', '\t', '\r', '\n', '\f'], StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < tokens.Length; i++)
        {
            trim |= tokens[i].Trim().ToLowerInvariant() switch
            {
                "discard-before" => SvgTextWhiteSpaceTrimMode.DiscardBefore,
                "discard-after" => SvgTextWhiteSpaceTrimMode.DiscardAfter,
                "discard-inner" => SvgTextWhiteSpaceTrimMode.DiscardInner,
                _ => SvgTextWhiteSpaceTrimMode.None
            };
        }

        return trim;
    }

    private static List<InlineSizeTextSegment> CreateInlineSizeWrapSegments(
        IReadOnlyList<PreparedSequentialRun> runs,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var segments = new List<InlineSizeTextSegment>();
        var segmentRuns = new List<InlineSizeTextRun>();
        var advanceCache = new Dictionary<TextRunAdvanceCacheKey, float>();
        float segmentAdvance = 0f;
        bool? segmentIsWhitespace = null;
        void FlushSegment()
        {
            if (segmentRuns.Count == 0 || !segmentIsWhitespace.HasValue)
            {
                return;
            }

            segments.Add(new InlineSizeTextSegment(segmentRuns.ToArray(), segmentAdvance, segmentIsWhitespace.Value, ForcesLineBreak: false));
            segmentRuns.Clear();
            segmentAdvance = 0f;
            segmentIsWhitespace = null;
        }

        void AppendLineBreak()
        {
            FlushSegment();
            segments.Add(new InlineSizeTextSegment(Array.Empty<InlineSizeTextRun>(), 0f, IsWhitespace: true, ForcesLineBreak: true));
        }

        void AppendPiece(SvgTextBase styleSource, string text, bool isWhitespace)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (segmentIsWhitespace.HasValue && segmentIsWhitespace.Value != isWhitespace)
            {
                FlushSegment();
            }

            var advance = MeasureTextAdvanceCached(styleSource, text, geometryBounds, assetLoader, advanceCache);
            segmentIsWhitespace = isWhitespace;
            segmentRuns.Add(new InlineSizeTextRun(styleSource, text, advance));
            segmentAdvance += advance;
        }

        for (var runIndex = 0; runIndex < runs.Count; runIndex++)
        {
            var run = runs[runIndex];
            if (string.IsNullOrEmpty(run.Text))
            {
                continue;
            }

            var breakSpaces = GetInlineSizeWhiteSpaceModel(run.StyleSource).BreaksAfterEveryPreservedSpace;
            var lineBreakOptions = GetInlineSizeLineBreakOptions(run.StyleSource);
            var codepoints = SplitCodepointsReadOnly(run.Text);
            var piece = new StringBuilder(run.Text.Length);
            bool? pieceIsWhitespace = null;
            var bidiFormattingDepth = 0;
            string? previousCodepoint = null;
            for (var i = 0; i < codepoints.Count; i++)
            {
                var codepoint = codepoints[i];
                var nextCodepoint = i + 1 < codepoints.Count ? codepoints[i + 1] : null;
                if (codepoint == "\n")
                {
                    if (piece.Length > 0 && pieceIsWhitespace.HasValue)
                    {
                        AppendPiece(run.StyleSource, piece.ToString(), pieceIsWhitespace.Value);
                        piece.Clear();
                        pieceIsWhitespace = null;
                    }

                    AppendLineBreak();
                    previousCodepoint = null;
                    continue;
                }

                if (IsInlineSizeInvisibleBreakOpportunity(codepoint, previousCodepoint, nextCodepoint, bidiFormattingDepth > 0))
                {
                    if (piece.Length > 0 && pieceIsWhitespace.HasValue)
                    {
                        AppendPiece(run.StyleSource, piece.ToString(), pieceIsWhitespace.Value);
                        piece.Clear();
                        pieceIsWhitespace = null;
                    }

                    FlushSegment();
                    previousCodepoint = codepoint;
                    continue;
                }

                var codepointIsWhitespace = IsInlineSizeBreakOpportunityWhitespace(
                    codepoint,
                    previousCodepoint,
                    nextCodepoint,
                    bidiFormattingDepth > 0);
                if (breakSpaces && codepointIsWhitespace)
                {
                    if (piece.Length > 0 && pieceIsWhitespace.HasValue)
                    {
                        AppendPiece(run.StyleSource, piece.ToString(), pieceIsWhitespace.Value);
                        piece.Clear();
                        pieceIsWhitespace = null;
                    }

                    AppendPiece(run.StyleSource, codepoint, isWhitespace: true);
                    FlushSegment();
                    bidiFormattingDepth = UpdateInlineSizeBidiFormattingDepth(bidiFormattingDepth, codepoint);
                    previousCodepoint = codepoint;
                    continue;
                }

                if (!codepointIsWhitespace &&
                    bidiFormattingDepth == 0 &&
                    !IsInlineSizeNoBreakAdjacentFormatControl(previousCodepoint) &&
                    !IsInlineSizeNoBreakAdjacentFormatControl(nextCodepoint) &&
                    IsInlineSizeCharacterBreakOpportunity(codepoints, i, lineBreakOptions))
                {
                    if (piece.Length > 0 && pieceIsWhitespace.HasValue)
                    {
                        AppendPiece(run.StyleSource, piece.ToString(), pieceIsWhitespace.Value);
                        piece.Clear();
                        pieceIsWhitespace = null;
                    }

                    AppendPiece(run.StyleSource, codepoint, isWhitespace: false);
                    FlushSegment();
                    bidiFormattingDepth = UpdateInlineSizeBidiFormattingDepth(bidiFormattingDepth, codepoint);
                    previousCodepoint = codepoint;
                    continue;
                }

                if (pieceIsWhitespace.HasValue && pieceIsWhitespace.Value != codepointIsWhitespace)
                {
                    AppendPiece(run.StyleSource, piece.ToString(), pieceIsWhitespace.Value);
                    piece.Clear();
                }

                pieceIsWhitespace = codepointIsWhitespace;
                piece.Append(codepoint);
                bidiFormattingDepth = UpdateInlineSizeBidiFormattingDepth(bidiFormattingDepth, codepoint);
                previousCodepoint = codepoint;
            }

            if (piece.Length > 0 && pieceIsWhitespace.HasValue)
            {
                AppendPiece(run.StyleSource, piece.ToString(), pieceIsWhitespace.Value);
            }
        }

        FlushSegment();
        return segments;
    }

    private static List<InlineSizeTextSegment> CreateInlineSizeWrapSegments(
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances)
    {
        var segments = new List<InlineSizeTextSegment>();
        var segmentRuns = new List<InlineSizeTextRun>();
        float segmentAdvance = 0f;
        bool? segmentIsWhitespace = null;
        void FlushSegment()
        {
            if (segmentRuns.Count == 0 || !segmentIsWhitespace.HasValue)
            {
                return;
            }

            segments.Add(new InlineSizeTextSegment(segmentRuns.ToArray(), segmentAdvance, segmentIsWhitespace.Value, ForcesLineBreak: false));
            segmentRuns.Clear();
            segmentAdvance = 0f;
            segmentIsWhitespace = null;
        }

        void AppendLineBreak()
        {
            FlushSegment();
            segments.Add(new InlineSizeTextSegment(Array.Empty<InlineSizeTextRun>(), 0f, IsWhitespace: true, ForcesLineBreak: true));
        }

        void AppendCodepoint(int codepointIndex, bool isWhitespace)
        {
            if (segmentIsWhitespace.HasValue && segmentIsWhitespace.Value != isWhitespace)
            {
                FlushSegment();
            }

            var codepoint = codepoints[codepointIndex];
            var advance = naturalAdvances[codepointIndex];
            segmentIsWhitespace = isWhitespace;
            segmentRuns.Add(new InlineSizeTextRun(codepoint.StyleSource, codepoint.Codepoint, advance, codepointIndex));
            segmentAdvance += advance;
        }

        var codepointTexts = new FlattenedCodepointTextList(codepoints);
        var bidiFormattingDepth = 0;
        string? previousCodepoint = null;
        for (var i = 0; i < codepoints.Count; i++)
        {
            var codepoint = codepoints[i].Codepoint;
            var styleSource = codepoints[i].StyleSource;
            var nextCodepoint = i + 1 < codepoints.Count ? codepoints[i + 1].Codepoint : null;
            if (codepoint == "\n")
            {
                AppendLineBreak();
                previousCodepoint = null;
                continue;
            }

            if (IsInlineSizeInvisibleBreakOpportunity(codepoint, previousCodepoint, nextCodepoint, bidiFormattingDepth > 0))
            {
                FlushSegment();
                previousCodepoint = codepoint;
                continue;
            }

            var codepointIsWhitespace = IsInlineSizeBreakOpportunityWhitespace(
                codepoint,
                previousCodepoint,
                nextCodepoint,
                bidiFormattingDepth > 0);
            if (GetInlineSizeWhiteSpaceModel(styleSource).BreaksAfterEveryPreservedSpace && codepointIsWhitespace)
            {
                AppendCodepoint(i, isWhitespace: true);
                FlushSegment();
                bidiFormattingDepth = UpdateInlineSizeBidiFormattingDepth(bidiFormattingDepth, codepoint);
                previousCodepoint = codepoint;
                continue;
            }

            if (!codepointIsWhitespace &&
                bidiFormattingDepth == 0 &&
                !IsInlineSizeNoBreakAdjacentFormatControl(previousCodepoint) &&
                !IsInlineSizeNoBreakAdjacentFormatControl(nextCodepoint) &&
                IsInlineSizeCharacterBreakOpportunity(codepointTexts, i, GetInlineSizeLineBreakOptions(styleSource)))
            {
                AppendCodepoint(i, isWhitespace: false);
                FlushSegment();
                bidiFormattingDepth = UpdateInlineSizeBidiFormattingDepth(bidiFormattingDepth, codepoint);
                previousCodepoint = codepoint;
                continue;
            }

            AppendCodepoint(i, codepointIsWhitespace);
            bidiFormattingDepth = UpdateInlineSizeBidiFormattingDepth(bidiFormattingDepth, codepoint);
            previousCodepoint = codepoint;
        }

        FlushSegment();
        return segments;
    }

    private static List<InlineSizeLogicalLine> CreateWrappedInlineSizeLogicalLines(
        IReadOnlyList<InlineSizeTextSegment> segments,
        Func<int, InlineSizeLineArea> resolveLineArea,
        int maxLineSearchCount,
        bool preserveLineEdgeWhitespace)
    {
        static SvgSharedTextLayoutRun ToSharedRun(InlineSizeTextRun run)
        {
            return new SvgSharedTextLayoutRun(run.StyleSource, run.Text, run.Advance, run.SourceCodepointIndex);
        }

        static InlineSizeTextRun ToInlineRun(SvgSharedTextLayoutRun run)
        {
            return new InlineSizeTextRun(run.StyleSource, run.Text, run.Advance, run.SourceCodepointIndex);
        }

        var sharedSegments = new SvgSharedTextLayoutSegment[segments.Count];
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var runs = new SvgSharedTextLayoutRun[segment.Runs.Length];
            for (var j = 0; j < segment.Runs.Length; j++)
            {
                runs[j] = ToSharedRun(segment.Runs[j]);
            }

            sharedSegments[i] = new SvgSharedTextLayoutSegment(runs, segment.Advance, segment.IsWhitespace, segment.ForcesLineBreak);
        }

        var sharedLines = SvgSharedTextLayoutEngine.CreateWrappedLogicalLines(
            sharedSegments,
            lineIndex =>
            {
                var area = resolveLineArea(lineIndex);
                return new SvgSharedTextLineArea(area.Start, area.InlineSize, area.BlockCoordinate, ToSharedFragments(area.Fragments), area.InlineProgression);
            },
            maxLineSearchCount,
            preserveLineEdgeWhitespace,
            TextLengthTolerance,
            run => IsWhitespaceOnlyText(run.Text));

        var lines = new List<InlineSizeLogicalLine>(sharedLines.Count);
        for (var i = 0; i < sharedLines.Count; i++)
        {
            var sharedLine = sharedLines[i];
            var runs = new InlineSizeTextRun[sharedLine.Runs.Length];
            for (var j = 0; j < sharedLine.Runs.Length; j++)
            {
                runs[j] = ToInlineRun(sharedLine.Runs[j]);
            }

            var area = new InlineSizeLineArea(
                sharedLine.Area.Start,
                sharedLine.Area.InlineSize,
                sharedLine.Area.BlockCoordinate,
                ToInlineFragments(sharedLine.Area.Fragments),
                sharedLine.Area.InlineProgression);
            lines.Add(new InlineSizeLogicalLine(runs, sharedLine.Advance, area, sharedLine.LineIndex));
        }

        return lines;

        static SvgSharedTextLineAreaFragment[] ToSharedFragments(InlineSizeLineAreaFragment[]? fragments)
        {
            if (fragments is not { Length: > 0 })
            {
                return Array.Empty<SvgSharedTextLineAreaFragment>();
            }

            var sharedFragments = new SvgSharedTextLineAreaFragment[fragments.Length];
            for (var i = 0; i < fragments.Length; i++)
            {
                sharedFragments[i] = new SvgSharedTextLineAreaFragment(fragments[i].Start, fragments[i].End);
            }

            return sharedFragments;
        }

        static InlineSizeLineAreaFragment[] ToInlineFragments(SvgSharedTextLineAreaFragment[]? fragments)
        {
            if (fragments is not { Length: > 0 })
            {
                return Array.Empty<InlineSizeLineAreaFragment>();
            }

            var inlineFragments = new InlineSizeLineAreaFragment[fragments.Length];
            for (var i = 0; i < fragments.Length; i++)
            {
                inlineFragments[i] = new InlineSizeLineAreaFragment(fragments[i].Start, fragments[i].End);
            }

            return inlineFragments;
        }
    }

    private static bool CanUseInlineSizeTextOverflowLayout(
        SvgTextBase svgTextBase,
        IReadOnlyList<SequentialTextRun> runs,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        if (!HasInlineSizeLayout(svgTextBase) ||
            runs.Count == 0 ||
            (HasOwnTextLengthAdjustment(svgTextBase) &&
             AllowsInlineSizeWrapping(svgTextBase) &&
             !HasExplicitLineBreakRun(runs)))
        {
            return false;
        }

        var canIgnoreRootTextLength = HasOwnTextLengthAdjustment(svgTextBase) &&
                                      HasExplicitLineBreakRun(runs);
        var combinedText = string.Concat(runs.Select(static run => run.Text));
        if (SvgTextBidiResolver.NeedsVisualOrdering(svgTextBase, combinedText) &&
            !CanUseInlineSizeBidiOrdering(svgTextBase, combinedText))
        {
            return false;
        }

        for (var i = 0; i < runs.Count; i++)
        {
            if ((!ReferenceEquals(runs[i].StyleSource, svgTextBase) && HasActiveShapeTextLayout(runs[i].StyleSource)) ||
                !CanPrepareInlineSizeTextRun(
                    runs[i],
                    geometryBounds,
                    assetLoader,
                    canIgnoreRootTextLength && ReferenceEquals(runs[i].StyleSource, svgTextBase)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasExplicitLineBreakRun(IReadOnlyList<SequentialTextRun> runs)
    {
        for (var i = 0; i < runs.Count; i++)
        {
            if (runs[i].Text.IndexOf('\n') >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInlineSizeBreakOpportunityWhitespace(
        string codepoint,
        string? previousCodepoint = null,
        string? nextCodepoint = null,
        bool insideBidiFormatting = false)
    {
        return SvgTextLineBreakPlanner.IsBreakOpportunityWhitespace(
            codepoint,
            previousCodepoint,
            nextCodepoint,
            insideBidiFormatting);
    }

    private static bool IsInlineSizeInvisibleBreakOpportunity(
        string codepoint,
        string? previousCodepoint = null,
        string? nextCodepoint = null,
        bool insideBidiFormatting = false)
    {
        return SvgTextLineBreakPlanner.IsInvisibleBreakOpportunity(
            codepoint,
            previousCodepoint,
            nextCodepoint,
            insideBidiFormatting);
    }

    private static int UpdateInlineSizeBidiFormattingDepth(int depth, string codepoint)
    {
        return SvgTextLineBreakPlanner.UpdateBidiFormattingDepth(depth, codepoint);
    }

    private static bool IsInlineSizeNoBreakAdjacentFormatControl(string? codepoint)
    {
        return SvgTextLineBreakPlanner.IsNoBreakAdjacentFormatControl(codepoint);
    }

    private static InlineSizeLineBreakOptions GetInlineSizeLineBreakOptions(SvgTextBase styleSource)
    {
        var overflowWrap = styleSource.ComputedStyle.OverflowWrap;
        var wordBreak = styleSource.ComputedStyle.WordBreak;
        var lineBreak = styleSource.ComputedStyle.LineBreak;
        return new InlineSizeLineBreakOptions(
            OverflowWrapAnywhere: IsCssIdentifier(overflowWrap, "anywhere", "break-word") ||
                                  IsCssIdentifier(wordBreak, "break-word"),
            WordBreakBreakAll: IsCssIdentifier(wordBreak, "break-all"),
            WordBreakKeepAll: IsCssIdentifier(wordBreak, "keep-all"),
            LineBreakAnywhere: IsCssIdentifier(lineBreak, "anywhere"),
            LineBreakLoose: IsCssIdentifier(lineBreak, "loose"),
            StrictLineBreak: IsCssIdentifier(lineBreak, "strict"));
    }

    private static bool IsCssIdentifier(string? value, params string[] identifiers)
    {
        for (var i = 0; i < identifiers.Length; i++)
        {
            if (string.Equals(value, identifiers[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInlineSizeCharacterBreakOpportunity(
        IReadOnlyList<string> codepoints,
        int index,
        InlineSizeLineBreakOptions options)
    {
        return AllowsInlineSizeSoftWrapOpportunity(codepoints, index, options);
    }

    private static bool CanBreakAfterInlineSizeCodepoint(
        IReadOnlyList<string> codepoints,
        int index,
        bool ignoreLineBreakProhibitions)
    {
        var currentScalar = char.ConvertToUtf32(codepoints[index], 0);
        if (IsCombiningOrJoiningCodepoint(currentScalar) ||
            (!ignoreLineBreakProhibitions && IsLineBreakProhibitedAfter(currentScalar, default)))
        {
            return false;
        }

        if (index + 1 >= codepoints.Count)
        {
            return true;
        }

        var nextScalar = char.ConvertToUtf32(codepoints[index + 1], 0);
        return !IsCombiningOrJoiningCodepoint(nextScalar) &&
               (ignoreLineBreakProhibitions || !IsLineBreakProhibitedBefore(nextScalar, default));
    }

    private static bool IsDashInlineBreakCodepoint(int scalar)
    {
        return scalar is 0x002D or 0x058A or 0x05BE or 0x1400 or 0x1806 or 0x2010 or 0x2012 or 0x2013 or 0x2014 or 0x2E17 or 0x30A0 or 0xFE31 or 0xFE32 or 0xFE58 or 0xFE63 or 0xFF0D or 0x10EAD;
    }

    private static bool IsCjkInlineBreakCodepoint(int scalar)
    {
        return scalar switch
        {
            >= 0x3040 and <= 0x30FF => true,
            >= 0x31F0 and <= 0x31FF => true,
            >= 0x3400 and <= 0x4DBF => true,
            >= 0x4E00 and <= 0x9FFF => true,
            >= 0xAC00 and <= 0xD7AF => true,
            >= 0xF900 and <= 0xFAFF => true,
            >= 0x20000 and <= 0x2A6DF => true,
            >= 0x2A700 and <= 0x2B73F => true,
            >= 0x2B740 and <= 0x2B81F => true,
            >= 0x2B820 and <= 0x2CEAF => true,
            >= 0x2CEB0 and <= 0x2EBEF => true,
            >= 0x30000 and <= 0x3134F => true,
            _ => false
        };
    }

    private static bool IsLineBreakAllowedAfterPunctuation(int scalar)
    {
        return scalar switch
        {
            0x0021 or 0x0025 or 0x0029 or 0x002C or 0x002E or 0x003A or 0x003B or 0x003F or 0x005D or 0x007D => true,
            0x2019 or 0x201D => true,
            0x3001 or 0x3002 or 0x3005 or 0x3009 or 0x300B or 0x300D or 0x300F => true,
            0x3011 or 0x3015 or 0x3017 or 0x3019 or 0x301B or 0x301E or 0x301F => true,
            0xFF01 or 0xFF05 or 0xFF09 or 0xFF0C or 0xFF0E or 0xFF1A or 0xFF1B or 0xFF1F or 0xFF3D or 0xFF5D => true,
            _ => false
        };
    }

    private static bool IsLineBreakProhibitedBefore(int scalar, InlineSizeLineBreakOptions options)
    {
        return scalar switch
        {
            0x0021 or 0x0025 or 0x0029 or 0x002C or 0x002E or 0x003A or 0x003B or 0x003F or 0x005D or 0x007D => true,
            0x2019 or 0x201D => true,
            0x3001 or 0x3002 or 0x3005 or 0x3009 or 0x300B or 0x300D or 0x300F => true,
            0x3011 or 0x3015 or 0x3017 or 0x3019 or 0x301B or 0x301E or 0x301F => true,
            0xFF01 or 0xFF05 or 0xFF09 or 0xFF0C or 0xFF0E or 0xFF1A or 0xFF1B or 0xFF1F or 0xFF3D or 0xFF5D => true,
            _ => options.StrictLineBreak && IsStrictLineBreakProhibitedBefore(scalar)
        };
    }

    private static bool IsLineBreakProhibitedAfter(int scalar, InlineSizeLineBreakOptions options)
    {
        return scalar switch
        {
            0x0028 or 0x005B or 0x007B => true,
            0x2018 or 0x201C => true,
            0x3008 or 0x300A or 0x300C or 0x300E or 0x3010 or 0x3014 or 0x3016 or 0x3018 or 0x301A => true,
            0xFF08 or 0xFF3B or 0xFF5B => true,
            _ => false
        };
    }

    private static bool IsStrictLineBreakProhibitedBefore(int scalar)
    {
        return scalar switch
        {
            0x3041 or 0x3043 or 0x3045 or 0x3047 or 0x3049 or 0x3063 or 0x3083 or 0x3085 or 0x3087 or 0x308E => true,
            0x3095 or 0x3096 => true,
            0x30A1 or 0x30A3 or 0x30A5 or 0x30A7 or 0x30A9 or 0x30C3 or 0x30E3 or 0x30E5 or 0x30E7 or 0x30EE => true,
            0x30F5 or 0x30F6 => true,
            0x31F0 or 0x31F1 or 0x31F2 or 0x31F3 or 0x31F4 or 0x31F5 or 0x31F6 or 0x31F7 or 0x31F8 or 0x31F9 or 0x31FA or 0x31FB or 0x31FC or 0x31FD or 0x31FE or 0x31FF => true,
            0xFF67 or 0xFF68 or 0xFF69 or 0xFF6A or 0xFF6B or 0xFF6C or 0xFF6D or 0xFF6E or 0xFF6F => true,
            _ => false
        };
    }

    private static bool IsCombiningOrJoiningCodepoint(int scalar)
    {
        return SvgTextBoundaryResolver.Default.IsCombiningOrJoiningCodepoint(scalar);
    }

    private static bool CanPrepareInlineSizeTextRun(
        SequentialTextRun run,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        bool ignoreOwnTextLength = false)
    {
        if (!IsVerticalWritingMode(run.StyleSource))
        {
            return CanPrepareSequentialTextRun(run, geometryBounds, assetLoader, ignoreOwnTextLength);
        }

        return (!HasOwnTextLengthAdjustment(run.StyleSource) || ignoreOwnTextLength) &&
               !HasEffectiveSpacingAdjustments(run.StyleSource, run.Text);
    }

    private static bool HasActiveShapeTextLayout(SvgTextBase svgTextBase)
    {
        return HasNonNoneCssTextProperty(svgTextBase.ShapeInside);
    }

    private static bool HasNonNoneCssTextProperty(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value!.Trim();
        return !trimmed.Equals("none", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasFiniteVerticalBounds(SKRect bounds)
    {
        return !float.IsNaN(bounds.Top) &&
               !float.IsNaN(bounds.Bottom) &&
               !float.IsInfinity(bounds.Top) &&
               !float.IsInfinity(bounds.Bottom);
    }

    private static bool HasFiniteHorizontalBounds(SKRect bounds)
    {
        return !float.IsNaN(bounds.Left) &&
               !float.IsNaN(bounds.Right) &&
               !float.IsInfinity(bounds.Left) &&
               !float.IsInfinity(bounds.Right);
    }

    private static bool TryCreateInlineSizeTextArea(
        SvgTextBase svgTextBase,
        IReadOnlyList<PreparedSequentialRun> preparedRuns,
        float currentX,
        float currentY,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        SKTextAlign textAlign,
        out InlineSizeTextArea? textArea,
        out float firstLineBlockCoordinate)
    {
        textArea = null;
        var isVertical = IsVerticalWritingMode(svgTextBase);
        firstLineBlockCoordinate = isVertical ? currentX : currentY;

        if (TryCreateShapeInsideTextArea(
                svgTextBase,
                preparedRuns,
                viewport,
                geometryBounds,
                assetLoader,
                isVertical,
                out textArea,
                out var shapeFirstLineBlockCoordinate))
        {
            firstLineBlockCoordinate = shapeFirstLineBlockCoordinate;
            return true;
        }

        if (HasActiveShapeTextLayout(svgTextBase))
        {
            return false;
        }

        if (!TryGetInlineSize(svgTextBase, viewport, assetLoader, isVertical, out var inlineSize) ||
            inlineSize <= 0f)
        {
            return false;
        }

        if (!TryResolveShapeSubtractShapes(svgTextBase, viewport, geometryBounds, assetLoader, isVertical, out var subtractShapes))
        {
            return false;
        }

        if (isVertical)
        {
            var contentStartY = GetAlignedStartCoordinate(currentY, inlineSize, textAlign);
            var flow = GetInlineSizeFlow(svgTextBase);
            textArea = new InlineSizeTextArea(
                new SKRect(float.NegativeInfinity, contentStartY, float.PositiveInfinity, contentStartY + inlineSize),
                insideShape: null,
                subtractShapes: subtractShapes,
                isShapeInside: false,
                isVertical: true,
                flow: flow,
                isInlineDirectionReversed: GetInlineAdvanceDirection(svgTextBase) < 0);
        }
        else
        {
            var contentStartX = GetAlignedStartCoordinate(currentX, inlineSize, textAlign);
            textArea = new InlineSizeTextArea(
                new SKRect(contentStartX, float.NegativeInfinity, contentStartX + inlineSize, float.PositiveInfinity),
                insideShape: null,
                subtractShapes: subtractShapes,
                isShapeInside: false,
                isVertical: false,
                flow: GetInlineSizeFlow(svgTextBase));
        }

        return true;
    }

    private static bool TryCreateShapeInsideTextArea(
        SvgTextBase svgTextBase,
        IReadOnlyList<PreparedSequentialRun> preparedRuns,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        bool isVertical,
        out InlineSizeTextArea? textArea,
        out float firstLineBlockCoordinate)
    {
        textArea = null;
        firstLineBlockCoordinate = 0f;
        if (!HasNonNoneCssTextProperty(svgTextBase.ShapeInside))
        {
            return false;
        }

        var shapePadding = ResolveShapeSpacing(svgTextBase, "shape-padding", viewport, assetLoader, isVertical);
        if (!TryResolveShapeTextShapes(svgTextBase, svgTextBase.ShapeInside, viewport, geometryBounds, assetLoader, shapePadding, 0f, isVertical, out var shapes) ||
            shapes.Length == 0)
        {
            return false;
        }

        if (!TryResolveShapeSubtractShapes(svgTextBase, viewport, geometryBounds, assetLoader, isVertical, out var subtractShapes))
        {
            return false;
        }

        var bounds = GetShapeTextBounds(shapes);
        var firstBaselineOffset = GetShapeInsideFirstBaselineOffset(preparedRuns, geometryBounds, assetLoader);
        var firstShapeBounds = shapes[0].EffectiveBounds;
        var flow = GetInlineSizeFlow(svgTextBase);
        firstLineBlockCoordinate = isVertical
            ? GetInlineSizeBlockProgression(flow) < 0
                ? firstShapeBounds.Right - firstBaselineOffset
                : firstShapeBounds.Left + firstBaselineOffset
            : firstShapeBounds.Top + firstBaselineOffset;
        textArea = new InlineSizeTextArea(
            bounds,
            shapes,
            subtractShapes,
            isShapeInside: true,
            isVertical: isVertical,
            flow: flow,
            shapeFirstBaselineOffset: firstBaselineOffset,
            isInlineDirectionReversed: isVertical && GetInlineAdvanceDirection(svgTextBase) < 0);
        return true;
    }

    private static bool TryResolveShapeSubtractShapes(
        SvgTextBase svgTextBase,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        bool isVertical,
        out InlineSizeTextShape[] subtractShapes)
    {
        subtractShapes = Array.Empty<InlineSizeTextShape>();
        if (!HasNonNoneCssTextProperty(svgTextBase.ShapeSubtract))
        {
            return true;
        }

        var shapeMargin = ResolveShapeSpacing(svgTextBase, "shape-margin", viewport, assetLoader, isVertical);
        return TryResolveShapeTextShapes(svgTextBase, svgTextBase.ShapeSubtract, viewport, geometryBounds, assetLoader, 0f, shapeMargin, isVertical, out subtractShapes, skipInvalidTokens: true);
    }

    private static bool TryResolveShapeTextShapes(
        SvgTextBase svgTextBase,
        string? value,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        float padding,
        float margin,
        bool isVertical,
        out InlineSizeTextShape[] shapes,
        bool skipInvalidTokens = false)
    {
        shapes = Array.Empty<InlineSizeTextShape>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var tokens = SplitCssShapeReferenceList(value!);
        if (tokens.Length == 0)
        {
            return false;
        }

        var resolvedShapes = new List<InlineSizeTextShape>(tokens.Length);
        for (var i = 0; i < tokens.Length; i++)
        {
            if (!TryResolveShapeTextShapeToken(svgTextBase, tokens[i], viewport, geometryBounds, assetLoader, padding, margin, isVertical, out var shape) ||
                shape.Bounds.Width <= 0f ||
                shape.Bounds.Height <= 0f)
            {
                if (skipInvalidTokens)
                {
                    continue;
                }

                return false;
            }

            resolvedShapes.Add(shape);
        }

        shapes = resolvedShapes.ToArray();
        return skipInvalidTokens || shapes.Length > 0;
    }

    private static bool TryResolveShapeTextShapeToken(
        SvgTextBase svgTextBase,
        string value,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        float padding,
        float margin,
        bool isVertical,
        out InlineSizeTextShape shape)
    {
        if (TryResolveBasicShapeTextShape(svgTextBase, value, viewport, geometryBounds, assetLoader, padding, margin, out shape))
        {
            return true;
        }

        if (TryCreateCssReferenceUri(value, out var uri) &&
            TryResolveShapeTextShape(svgTextBase, uri, viewport, assetLoader, padding, margin, isVertical, out shape))
        {
            return true;
        }

        return TryResolveImageShapeTextShape(svgTextBase, value, viewport, geometryBounds, assetLoader, padding, margin, out shape);
    }

    private static bool TryResolveImageShapeTextShape(
        SvgTextBase svgTextBase,
        string value,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        float padding,
        float margin,
        out InlineSizeTextShape shape)
    {
        shape = default;
        if (!TryParseImageShapeToken(value, out var href, out var shapeBoxKeyword))
        {
            return false;
        }

        var image = SvgService.GetImage(href, svgTextBase, assetLoader);
        if (image is not SKImage skImage ||
            skImage.Data is null ||
            !TryResolveImageShapeAlpha(skImage, assetLoader, out var imageWidth, out var imageHeight, out var alpha))
        {
            return false;
        }

        SKRect referenceBox;
        if (string.IsNullOrWhiteSpace(shapeBoxKeyword))
        {
            referenceBox = SKRect.Create(0f, 0f, imageWidth, imageHeight);
        }
        else if (!TryResolveShapeBoxKeyword(svgTextBase, shapeBoxKeyword, viewport, geometryBounds, out referenceBox, out _))
        {
            return false;
        }

        if (!SvgCssShapeImageSampler.TryCreateAlphaPath(imageWidth, imageHeight, alpha, referenceBox, ResolveShapeImageThreshold(svgTextBase), out var path))
        {
            return false;
        }

        var samples = BuildPathSamples(path);
        var bounds = samples.Count > 0 ? GetPathSampleBounds(samples) : path.Bounds;
        shape = new InlineSizeTextShape(bounds, samples.ToArray(), padding, margin, path.FillType);
        return bounds.Width > 0f && bounds.Height > 0f;
    }

    private static bool TryResolveImageShapeAlpha(
        SKImage image,
        ISvgAssetLoader assetLoader,
        out int width,
        out int height,
        out byte[] alpha)
    {
        if (assetLoader is ISvgImageAlphaProvider alphaProvider &&
            alphaProvider.TryGetImageAlpha(image, out width, out height, out alpha) &&
            width > 0 &&
            height > 0 &&
            alpha.Length >= width * height)
        {
            return true;
        }

        if (SvgCssShapeImageSampler.TryDecodeAlpha(image.Data, out width, out height, out alpha))
        {
            return true;
        }

        width = 0;
        height = 0;
        alpha = Array.Empty<byte>();
        return false;
    }

    private static bool TryParseImageShapeToken(string value, out string href, out string shapeBoxKeyword)
    {
        href = string.Empty;
        shapeBoxKeyword = string.Empty;
        var imageValue = value.Trim();
        if (TrySplitLeadingShapeBoxKeyword(imageValue, out var leadingShapeBoxKeyword, out var leadingImageValue) &&
            TryCreateCssReferenceUriText(leadingImageValue, out _, out href))
        {
            shapeBoxKeyword = leadingShapeBoxKeyword;
            return true;
        }

        if (TrySplitTrailingShapeBoxKeyword(imageValue, out var trailingImageValue, out var trailingShapeBoxKeyword) &&
            TryCreateCssReferenceUriText(trailingImageValue, out _, out href))
        {
            shapeBoxKeyword = trailingShapeBoxKeyword;
            return true;
        }

        return TryCreateCssReferenceUriText(imageValue, out _, out href);
    }

    private static SKRect GetShapeTextBounds(IReadOnlyList<InlineSizeTextShape> shapes)
    {
        if (shapes.Count == 0)
        {
            return SKRect.Empty;
        }

        var bounds = shapes[0].Bounds;
        if (!shapes[0].EffectiveBounds.IsEmpty)
        {
            bounds = shapes[0].EffectiveBounds;
        }

        for (var i = 1; i < shapes.Count; i++)
        {
            var shapeBounds = shapes[i].EffectiveBounds.IsEmpty ? shapes[i].Bounds : shapes[i].EffectiveBounds;
            bounds = new SKRect(
                Math.Min(bounds.Left, shapeBounds.Left),
                Math.Min(bounds.Top, shapeBounds.Top),
                Math.Max(bounds.Right, shapeBounds.Right),
                Math.Max(bounds.Bottom, shapeBounds.Bottom));
        }

        return bounds;
    }

    private static float GetShapeInsideFirstBaseline(
        IReadOnlyList<PreparedSequentialRun> preparedRuns,
        SKRect shapeBounds,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        return shapeBounds.Top + GetShapeInsideFirstBaselineOffset(preparedRuns, geometryBounds, assetLoader);
    }

    private static float GetShapeInsideFirstBaselineOffset(
        IReadOnlyList<PreparedSequentialRun> preparedRuns,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var styleSource = preparedRuns.Count > 0 ? preparedRuns[0].StyleSource : null;
        for (var i = 0; i < preparedRuns.Count; i++)
        {
            if (!string.IsNullOrEmpty(preparedRuns[i].Text))
            {
                styleSource = preparedRuns[i].StyleSource;
                break;
            }
        }

        if (styleSource is null)
        {
            return 0f;
        }

        var paint = new SKPaint();
        PaintingService.SetPaintText(styleSource, geometryBounds, paint);
        var metrics = assetLoader.GetFontMetrics(paint);
        EnsureUsableFontMetrics(ref metrics, paint.TextSize);
        var lineAdvance = ResolveInlineSizeLineAdvance(styleSource, geometryBounds, assetLoader, paint, metrics);
        return Math.Max(-metrics.Ascent, lineAdvance * 0.85f);
    }

    private static bool TryResolveShapeTextBounds(
        SvgTextBase svgTextBase,
        string? value,
        SKRect viewport,
        out SKRect bounds)
    {
        bounds = SKRect.Empty;
        if (!TryResolveShapeTextShape(svgTextBase, value, viewport, out var shape))
        {
            return false;
        }

        bounds = shape.Bounds;
        return bounds.Width > 0f && bounds.Height > 0f;
    }

    private static bool TryResolveShapeTextShape(
        SvgTextBase svgTextBase,
        string? value,
        SKRect viewport,
        out InlineSizeTextShape shape)
    {
        shape = default;
        if (!TryCreateCssReferenceUri(value, out var uri))
        {
            return false;
        }

        return TryResolveShapeTextShape(svgTextBase, uri, viewport, out shape);
    }

    private static bool TryResolveShapeTextShape(
        SvgTextBase svgTextBase,
        Uri uri,
        SKRect viewport,
        out InlineSizeTextShape shape)
    {
        return TryResolveShapeTextShape(svgTextBase, uri, viewport, assetLoader: null, padding: 0f, margin: 0f, isVertical: false, out shape);
    }

    private static bool TryResolveShapeTextShape(
        SvgTextBase svgTextBase,
        Uri uri,
        SKRect viewport,
        ISvgAssetLoader? assetLoader,
        float padding,
        float margin,
        bool isVertical,
        out InlineSizeTextShape shape)
    {
        shape = default;
        var referencedElement = SvgService.GetReference<SvgElement>(svgTextBase, uri);
        if (referencedElement is null ||
            !SvgSceneCompiler.TryGetDirectVisualPath(referencedElement, viewport, out var path) ||
            path is null ||
            path.IsEmpty)
        {
            return false;
        }

        var samples = BuildPathSamples(path);
        if (samples.Count > 0 && referencedElement is SvgVisualElement referencedVisualElement)
        {
            var transform = GetTextPathReferenceTransform(referencedVisualElement);
            if (!IsIdentityTransform(transform))
            {
                samples = TransformPathSamples(samples, transform);
            }
        }

        var bounds = samples.Count > 0 ? GetPathSampleBounds(samples) : path.Bounds;
        var referencedMargin = assetLoader is not null &&
                               TryResolveShapeSpacing(referencedElement, "shape-margin", svgTextBase, viewport, assetLoader, isVertical, out var ownMargin)
            ? ownMargin
            : margin;
        shape = new InlineSizeTextShape(bounds, samples.ToArray(), padding, referencedMargin, path.FillType);
        return bounds.Width > 0f && bounds.Height > 0f;
    }

    private static float ResolveShapeSpacing(
        SvgTextBase svgTextBase,
        string attributeName,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        bool isVertical)
    {
        return TryGetComputedShapeSpacing(svgTextBase, attributeName, out var rawSpacing) &&
               TryResolveShapeSpacingValue(rawSpacing, svgTextBase, viewport, assetLoader, isVertical, out var spacing)
            ? spacing
            : 0f;
    }

    private static bool TryResolveShapeSpacing(
        SvgElement element,
        string attributeName,
        SvgTextBase contextText,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        bool isVertical,
        out float spacing)
    {
        spacing = 0f;
        return TryGetComputedShapeSpacing(element, attributeName, out var rawSpacing) &&
               TryResolveShapeSpacingValue(rawSpacing, contextText, viewport, assetLoader, isVertical, out spacing);
    }

    private static bool TryGetComputedShapeSpacing(SvgElement element, string attributeName, out string? value)
    {
        value = attributeName switch
        {
            "shape-padding" => element.ShapePadding,
            "shape-margin" => element.ShapeMargin,
            _ => null
        };

        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryResolveShapeSpacingValue(
        string? value,
        SvgTextBase contextText,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        bool isVertical,
        out float spacing)
    {
        spacing = 0f;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var unit = SvgUnitConverter.Parse(value.AsSpan().Trim());
            spacing = ResolveTextUnitValue(unit, isVertical ? UnitRenderingType.Vertical : UnitRenderingType.Horizontal, contextText, viewport, assetLoader);
            spacing = Math.Max(0f, spacing);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static float ResolveShapeImageThreshold(SvgTextBase svgTextBase)
    {
        var value = svgTextBase.ShapeImageThreshold;
        return !string.IsNullOrWhiteSpace(value) &&
               float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold)
            ? Math.Max(0f, Math.Min(1f, threshold))
            : 0f;
    }

    private static bool TryResolveBasicShapeTextShape(
        SvgTextBase svgTextBase,
        string? value,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        float padding,
        float margin,
        out InlineSizeTextShape shape)
    {
        shape = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var originalShapeValue = value!.Trim();
        var trimmed = originalShapeValue;
        if (!TryResolveShapeBox(svgTextBase, trimmed, viewport, geometryBounds, out var referenceBox, out trimmed))
        {
            return false;
        }

        if (!TryBuildBasicShapePath(trimmed, referenceBox, out var path) &&
            !referenceBox.Equals(viewport) &&
            TryResolveShapeBox(svgTextBase, originalShapeValue, viewport, SKRect.Empty, out var fallbackReferenceBox, out trimmed))
        {
            _ = TryBuildBasicShapePath(trimmed, fallbackReferenceBox, out path);
        }

        if (path.IsEmpty)
        {
            return false;
        }

        var samples = BuildPathSamples(path);
        var bounds = samples.Count > 0 ? GetPathSampleBounds(samples) : path.Bounds;
        shape = new InlineSizeTextShape(bounds, samples.ToArray(), padding, margin, path.FillType);
        return bounds.Width > 0f && bounds.Height > 0f;

        bool TryBuildBasicShapePath(string shapeText, SKRect shapeReferenceBox, out SKPath shapePath)
        {
            shapePath = new SKPath();
            if (string.IsNullOrWhiteSpace(shapeText))
            {
                shapePath.AddRect(shapeReferenceBox);
            }
            else if (TryParseInsetShape(shapeText, svgTextBase, shapeReferenceBox, assetLoader, out var insetRect, out var insetCornerRadii))
            {
                AppendInsetShapePath(shapePath, insetRect, insetCornerRadii);
            }
            else if (TryParseCircleShape(shapeText, svgTextBase, shapeReferenceBox, assetLoader, out var circleX, out var circleY, out var circleRadius))
            {
                shapePath.AddCircle(circleX, circleY, circleRadius);
            }
            else if (TryParseEllipseShape(shapeText, svgTextBase, shapeReferenceBox, assetLoader, out var ellipseRect))
            {
                shapePath.AddOval(ellipseRect);
            }
            else if (TryParsePolygonShape(shapeText, svgTextBase, shapeReferenceBox, assetLoader, out var points, out var polygonFillType))
            {
                shapePath.FillType = polygonFillType;
                shapePath.AddPoly(points, close: true);
            }

            return !shapePath.IsEmpty;
        }
    }

    private static bool TryResolveShapeBox(
        SvgTextBase svgTextBase,
        string value,
        SKRect viewport,
        SKRect geometryBounds,
        out SKRect referenceBox,
        out string shapeValue)
    {
        referenceBox = viewport;
        shapeValue = value.Trim();
        if (string.IsNullOrWhiteSpace(shapeValue))
        {
            return false;
        }

        if (IsCssShapeBoxKeyword(shapeValue))
        {
            return TryResolveShapeBoxKeyword(svgTextBase, shapeValue, viewport, geometryBounds, out referenceBox, out shapeValue);
        }

        if (TrySplitLeadingShapeBoxKeyword(shapeValue, out var leadingShapeBoxKeyword, out var leadingBasicShape))
        {
            if (string.IsNullOrWhiteSpace(leadingBasicShape) ||
                !TryResolveShapeBoxKeyword(svgTextBase, leadingShapeBoxKeyword, viewport, geometryBounds, out referenceBox, out _))
            {
                return false;
            }

            shapeValue = leadingBasicShape;
            return true;
        }

        if (!TrySplitTrailingShapeBoxKeyword(shapeValue, out var basicShape, out var shapeBoxKeyword))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(basicShape) ||
            !TryResolveShapeBoxKeyword(svgTextBase, shapeBoxKeyword, viewport, geometryBounds, out referenceBox, out _))
        {
            return false;
        }

        shapeValue = basicShape;
        return true;
    }

    private static bool TryResolveShapeBoxKeyword(
        SvgTextBase svgTextBase,
        string keyword,
        SKRect viewport,
        SKRect geometryBounds,
        out SKRect referenceBox,
        out string shapeValue)
    {
        referenceBox = SKRect.Empty;
        shapeValue = string.Empty;
        if (keyword.Equals("view-box", StringComparison.OrdinalIgnoreCase))
        {
            referenceBox = viewport;
            return referenceBox.Width > 0f && referenceBox.Height > 0f;
        }

        if (!IsCssShapeBoxKeyword(keyword))
        {
            return false;
        }

        if (!geometryBounds.IsEmpty &&
            geometryBounds.Width > 0f &&
            geometryBounds.Height > 0f)
        {
            referenceBox = ResolveSvgShapeBoxKeyword(svgTextBase, keyword, geometryBounds, viewport);
            return referenceBox.Width > 0f && referenceBox.Height > 0f;
        }

        referenceBox = viewport;
        return referenceBox.Width > 0f && referenceBox.Height > 0f;
    }

    private static SKRect ResolveSvgShapeBoxKeyword(SvgTextBase svgTextBase, string keyword, SKRect geometryBounds, SKRect viewport)
    {
        if (keyword.Equals("stroke-box", StringComparison.OrdinalIgnoreCase) ||
            keyword.Equals("border-box", StringComparison.OrdinalIgnoreCase) ||
            keyword.Equals("margin-box", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveShapeStrokeBox(svgTextBase, geometryBounds, viewport);
        }

        return geometryBounds;
    }

    private static SKRect ResolveShapeStrokeBox(SvgTextBase svgTextBase, SKRect geometryBounds, SKRect viewport)
    {
        if (!SvgScenePaintingService.IsValidStroke(svgTextBase, geometryBounds))
        {
            return geometryBounds;
        }

        var strokeWidth = svgTextBase.StrokeWidth.ToDeviceValue(UnitRenderingType.Other, svgTextBase, viewport);
        if (strokeWidth <= 0f)
        {
            return geometryBounds;
        }

        return new SKRect(
            geometryBounds.Left - (strokeWidth * 0.5f),
            geometryBounds.Top - (strokeWidth * 0.5f),
            geometryBounds.Right + (strokeWidth * 0.5f),
            geometryBounds.Bottom + (strokeWidth * 0.5f));
    }

    private static bool TrySplitLeadingShapeBoxKeyword(string value, out string shapeBoxKeyword, out string basicShape)
    {
        shapeBoxKeyword = string.Empty;
        basicShape = value;
        var separatorIndex = -1;
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsWhiteSpace(value[i]))
            {
                separatorIndex = i;
                break;
            }
        }

        if (separatorIndex < 0)
        {
            return false;
        }

        var candidate = value.Substring(0, separatorIndex).Trim();
        if (!IsCssShapeBoxKeyword(candidate))
        {
            return false;
        }

        shapeBoxKeyword = candidate;
        basicShape = value.Substring(separatorIndex + 1).Trim();
        return true;
    }

    private static bool TrySplitTrailingShapeBoxKeyword(string value, out string basicShape, out string shapeBoxKeyword)
    {
        basicShape = value;
        shapeBoxKeyword = string.Empty;
        var lastSeparatorIndex = -1;
        var depth = 0;
        var quote = '\0';

        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (quote != '\0')
            {
                if (ch == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (ch == '\'' || ch == '"')
            {
                quote = ch;
                continue;
            }

            if (ch == '(')
            {
                depth++;
                continue;
            }

            if (ch == ')' && depth > 0)
            {
                depth--;
                continue;
            }

            if (depth == 0 && char.IsWhiteSpace(ch))
            {
                lastSeparatorIndex = i;
            }
        }

        if (lastSeparatorIndex < 0)
        {
            return false;
        }

        var candidate = value.Substring(lastSeparatorIndex + 1).Trim();
        if (!IsCssShapeBoxKeyword(candidate))
        {
            return false;
        }

        basicShape = value.Substring(0, lastSeparatorIndex).Trim();
        shapeBoxKeyword = candidate;
        return true;
    }

    private static bool IsCssShapeBoxKeyword(string value)
    {
        return value.Equals("view-box", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("fill-box", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("stroke-box", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("margin-box", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("border-box", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("padding-box", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("content-box", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseInsetShape(
        string value,
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out SKRect rect,
        out CssCornerRadii cornerRadii)
    {
        rect = SKRect.Empty;
        cornerRadii = default;
        if (!TryGetCssFunctionArguments(value, "inset", out var arguments))
        {
            return false;
        }

        if (!TrySplitCssShapeKeyword(arguments, "round", out var insetArguments, out var radiusArguments))
        {
            insetArguments = arguments;
            radiusArguments = string.Empty;
        }

        if (!TryParseInsetOffsets(insetArguments, svgTextBase, viewport, assetLoader, out rect))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(radiusArguments) ||
               TryParseInsetRoundRadii(radiusArguments, svgTextBase, rect, assetLoader, out cornerRadii);
    }

    private static bool TryParseInsetOffsets(
        string value,
        SvgTextBase svgTextBase,
        SKRect referenceBox,
        ISvgAssetLoader assetLoader,
        out SKRect rect)
    {
        rect = SKRect.Empty;
        var tokens = SplitCssShapeTokens(value);
        if (tokens.Count is < 1 or > 4)
        {
            return false;
        }

        if (!TryResolveCssShapeLength(tokens[0], svgTextBase, referenceBox, assetLoader, UnitRenderingType.Vertical, out var top))
        {
            return false;
        }

        var right = top;
        var bottom = top;
        var left = top;
        if (tokens.Count >= 2 &&
            !TryResolveCssShapeLength(tokens[1], svgTextBase, referenceBox, assetLoader, UnitRenderingType.Horizontal, out right))
        {
            return false;
        }

        if (tokens.Count >= 3 &&
            !TryResolveCssShapeLength(tokens[2], svgTextBase, referenceBox, assetLoader, UnitRenderingType.Vertical, out bottom))
        {
            return false;
        }

        if (tokens.Count >= 4 &&
            !TryResolveCssShapeLength(tokens[3], svgTextBase, referenceBox, assetLoader, UnitRenderingType.Horizontal, out left))
        {
            return false;
        }

        if (tokens.Count == 2)
        {
            bottom = top;
            left = right;
        }
        else if (tokens.Count == 3)
        {
            left = right;
        }

        rect = new SKRect(referenceBox.Left + left, referenceBox.Top + top, referenceBox.Right - right, referenceBox.Bottom - bottom);
        return rect.Width > 0f && rect.Height > 0f;
    }

    private static bool TryParseInsetRoundRadii(
        string value,
        SvgTextBase svgTextBase,
        SKRect referenceBox,
        ISvgAssetLoader assetLoader,
        out CssCornerRadii cornerRadii)
    {
        cornerRadii = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string horizontalArguments;
        string verticalArguments;
        if (TrySplitCssShapeSlash(value, out horizontalArguments, out verticalArguments))
        {
            if (string.IsNullOrWhiteSpace(horizontalArguments) ||
                string.IsNullOrWhiteSpace(verticalArguments))
            {
                return false;
            }
        }
        else
        {
            horizontalArguments = value;
            verticalArguments = value;
        }

        if (!TryResolveCssCornerRadiusValues(horizontalArguments, svgTextBase, referenceBox, assetLoader, UnitRenderingType.Horizontal, out var topLeftX, out var topRightX, out var bottomRightX, out var bottomLeftX) ||
            !TryResolveCssCornerRadiusValues(verticalArguments, svgTextBase, referenceBox, assetLoader, UnitRenderingType.Vertical, out var topLeftY, out var topRightY, out var bottomRightY, out var bottomLeftY))
        {
            return false;
        }

        cornerRadii = NormalizeCssCornerRadii(
            referenceBox,
            new CssCornerRadii(topLeftX, topLeftY, topRightX, topRightY, bottomRightX, bottomRightY, bottomLeftX, bottomLeftY));
        return true;
    }

    private static bool TryResolveCssCornerRadiusValues(
        string value,
        SvgTextBase svgTextBase,
        SKRect referenceBox,
        ISvgAssetLoader assetLoader,
        UnitRenderingType renderingType,
        out float topLeft,
        out float topRight,
        out float bottomRight,
        out float bottomLeft)
    {
        topLeft = 0f;
        topRight = 0f;
        bottomRight = 0f;
        bottomLeft = 0f;

        var tokens = SplitCssShapeTokens(value);
        if (tokens.Count is < 1 or > 4)
        {
            return false;
        }

        var resolved = new float[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
        {
            if (!TryResolveCssShapeLength(tokens[i], svgTextBase, referenceBox, assetLoader, renderingType, out resolved[i]) ||
                resolved[i] < 0f)
            {
                return false;
            }
        }

        topLeft = resolved[0];
        topRight = tokens.Count >= 2 ? resolved[1] : resolved[0];
        bottomRight = tokens.Count >= 3 ? resolved[2] : resolved[0];
        bottomLeft = tokens.Count >= 4
            ? resolved[3]
            : tokens.Count >= 2
                ? resolved[1]
                : resolved[0];
        return true;
    }

    private static CssCornerRadii NormalizeCssCornerRadii(SKRect rect, CssCornerRadii radii)
    {
        var factor = 1f;

        void Reduce(float sum, float basis)
        {
            if (sum > 0f && sum > basis)
            {
                factor = Math.Min(factor, basis / sum);
            }
        }

        Reduce(radii.TopLeftX + radii.TopRightX, rect.Width);
        Reduce(radii.BottomLeftX + radii.BottomRightX, rect.Width);
        Reduce(radii.TopLeftY + radii.BottomLeftY, rect.Height);
        Reduce(radii.TopRightY + radii.BottomRightY, rect.Height);

        return factor < 1f
            ? new CssCornerRadii(
                radii.TopLeftX * factor,
                radii.TopLeftY * factor,
                radii.TopRightX * factor,
                radii.TopRightY * factor,
                radii.BottomRightX * factor,
                radii.BottomRightY * factor,
                radii.BottomLeftX * factor,
                radii.BottomLeftY * factor)
            : radii;
    }

    private static bool TrySplitCssShapeKeyword(string value, string keyword, out string before, out string after)
    {
        before = value;
        after = string.Empty;
        var depth = 0;
        var quote = '\0';
        for (var i = 0; i <= value.Length - keyword.Length; i++)
        {
            var ch = value[i];
            if (quote != '\0')
            {
                if (ch == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (ch == '\'' || ch == '"')
            {
                quote = ch;
                continue;
            }

            if (ch == '(')
            {
                depth++;
                continue;
            }

            if (ch == ')' && depth > 0)
            {
                depth--;
                continue;
            }

            if (depth == 0 &&
                IsCssIdentifierBoundary(value, i - 1) &&
                IsCssIdentifierBoundary(value, i + keyword.Length) &&
                string.Compare(value, i, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                before = value.Substring(0, i).Trim();
                after = value.Substring(i + keyword.Length).Trim();
                return true;
            }
        }

        return false;
    }

    private static bool TrySplitCssShapeSlash(string value, out string before, out string after)
    {
        before = value;
        after = string.Empty;
        var depth = 0;
        var quote = '\0';
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (quote != '\0')
            {
                if (ch == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (ch == '\'' || ch == '"')
            {
                quote = ch;
                continue;
            }

            if (ch == '(')
            {
                depth++;
                continue;
            }

            if (ch == ')' && depth > 0)
            {
                depth--;
                continue;
            }

            if (depth == 0 && ch == '/')
            {
                before = value.Substring(0, i).Trim();
                after = value.Substring(i + 1).Trim();
                return true;
            }
        }

        return false;
    }

    private static bool IsCssIdentifierBoundary(string value, int index)
    {
        return index < 0 ||
               index >= value.Length ||
               char.IsWhiteSpace(value[index]) ||
               value[index] is ',' or '/';
    }

    private static void AppendInsetShapePath(SKPath path, SKRect rect, CssCornerRadii radii)
    {
        if (radii.IsEmpty)
        {
            path.AddRect(rect);
            return;
        }

        const float arcKappa = 0.552284749831f;

        path.MoveTo(rect.Left + radii.TopLeftX, rect.Top);
        path.LineTo(rect.Right - radii.TopRightX, rect.Top);
        if (radii.TopRightX > 0f && radii.TopRightY > 0f)
        {
            var cx = rect.Right - radii.TopRightX;
            var cy = rect.Top + radii.TopRightY;
            path.CubicTo(
                cx + (arcKappa * radii.TopRightX),
                rect.Top,
                rect.Right,
                cy - (arcKappa * radii.TopRightY),
                rect.Right,
                cy);
        }
        else
        {
            path.LineTo(rect.Right, rect.Top);
        }

        path.LineTo(rect.Right, rect.Bottom - radii.BottomRightY);
        if (radii.BottomRightX > 0f && radii.BottomRightY > 0f)
        {
            var cx = rect.Right - radii.BottomRightX;
            var cy = rect.Bottom - radii.BottomRightY;
            path.CubicTo(
                rect.Right,
                cy + (arcKappa * radii.BottomRightY),
                cx + (arcKappa * radii.BottomRightX),
                rect.Bottom,
                cx,
                rect.Bottom);
        }
        else
        {
            path.LineTo(rect.Right, rect.Bottom);
        }

        path.LineTo(rect.Left + radii.BottomLeftX, rect.Bottom);
        if (radii.BottomLeftX > 0f && radii.BottomLeftY > 0f)
        {
            var cx = rect.Left + radii.BottomLeftX;
            var cy = rect.Bottom - radii.BottomLeftY;
            path.CubicTo(
                cx - (arcKappa * radii.BottomLeftX),
                rect.Bottom,
                rect.Left,
                cy + (arcKappa * radii.BottomLeftY),
                rect.Left,
                cy);
        }
        else
        {
            path.LineTo(rect.Left, rect.Bottom);
        }

        path.LineTo(rect.Left, rect.Top + radii.TopLeftY);
        if (radii.TopLeftX > 0f && radii.TopLeftY > 0f)
        {
            var cx = rect.Left + radii.TopLeftX;
            var cy = rect.Top + radii.TopLeftY;
            path.CubicTo(
                rect.Left,
                cy - (arcKappa * radii.TopLeftY),
                cx - (arcKappa * radii.TopLeftX),
                rect.Top,
                cx,
                rect.Top);
        }
        else
        {
            path.LineTo(rect.Left, rect.Top);
        }

        path.Close();
    }

    private static bool TryParseCircleShape(
        string value,
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out float centerX,
        out float centerY,
        out float radius)
    {
        centerX = (viewport.Left + viewport.Right) * 0.5f;
        centerY = (viewport.Top + viewport.Bottom) * 0.5f;
        radius = 0f;
        if (!TryGetCssFunctionArguments(value, "circle", out var arguments))
        {
            return false;
        }

        SplitShapeAtKeyword(arguments, "at", out var radiusPart, out var centerPart);
        if (!TryResolveCssShapeCenter(centerPart, svgTextBase, viewport, assetLoader, ref centerX, ref centerY))
        {
            return false;
        }

        radiusPart = radiusPart.Trim();
        if (string.IsNullOrWhiteSpace(radiusPart))
        {
            radius = GetCircleShapeRadius(centerX, centerY, viewport, closestSide: true);
        }
        else if (radiusPart.Equals("closest-side", StringComparison.OrdinalIgnoreCase))
        {
            radius = GetCircleShapeRadius(centerX, centerY, viewport, closestSide: true);
        }
        else if (radiusPart.Equals("farthest-side", StringComparison.OrdinalIgnoreCase))
        {
            radius = GetCircleShapeRadius(centerX, centerY, viewport, closestSide: false);
        }
        else if (!TryResolveCssShapeLength(radiusPart, svgTextBase, viewport, assetLoader, UnitRenderingType.Other, out radius))
        {
            return false;
        }

        return radius > 0f;
    }

    private static float GetCircleShapeRadius(float centerX, float centerY, SKRect viewport, bool closestSide)
    {
        var left = Math.Abs(centerX - viewport.Left);
        var right = Math.Abs(viewport.Right - centerX);
        var top = Math.Abs(centerY - viewport.Top);
        var bottom = Math.Abs(viewport.Bottom - centerY);
        return closestSide
            ? Math.Min(Math.Min(left, right), Math.Min(top, bottom))
            : Math.Max(Math.Max(left, right), Math.Max(top, bottom));
    }

    private static bool TryParseEllipseShape(
        string value,
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out SKRect rect)
    {
        rect = SKRect.Empty;
        if (!TryGetCssFunctionArguments(value, "ellipse", out var arguments))
        {
            return false;
        }

        SplitShapeAtKeyword(arguments, "at", out var radiusPart, out var centerPart);
        var centerX = (viewport.Left + viewport.Right) * 0.5f;
        var centerY = (viewport.Top + viewport.Bottom) * 0.5f;
        if (!TryResolveCssShapeCenter(centerPart, svgTextBase, viewport, assetLoader, ref centerX, ref centerY))
        {
            return false;
        }

        var tokens = SplitCssShapeTokens(radiusPart);
        float rx;
        float ry;
        if (tokens.Count == 0)
        {
            rx = GetEllipseShapeRadius(centerX, viewport.Left, viewport.Right, closestSide: true);
            ry = GetEllipseShapeRadius(centerY, viewport.Top, viewport.Bottom, closestSide: true);
        }
        else if (tokens.Count == 1)
        {
            if (!TryResolveCssEllipseRadius(tokens[0], svgTextBase, viewport, assetLoader, UnitRenderingType.Horizontal, centerX, viewport.Left, viewport.Right, out rx) ||
                !TryResolveCssEllipseRadius(tokens[0], svgTextBase, viewport, assetLoader, UnitRenderingType.Vertical, centerY, viewport.Top, viewport.Bottom, out ry))
            {
                return false;
            }
        }
        else if (tokens.Count == 2)
        {
            if (!TryResolveCssEllipseRadius(tokens[0], svgTextBase, viewport, assetLoader, UnitRenderingType.Horizontal, centerX, viewport.Left, viewport.Right, out rx) ||
                !TryResolveCssEllipseRadius(tokens[1], svgTextBase, viewport, assetLoader, UnitRenderingType.Vertical, centerY, viewport.Top, viewport.Bottom, out ry))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        rect = new SKRect(centerX - rx, centerY - ry, centerX + rx, centerY + ry);
        return rect.Width > 0f && rect.Height > 0f;
    }

    private static bool TryResolveCssEllipseRadius(
        string token,
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        UnitRenderingType renderingType,
        float center,
        float nearSide,
        float farSide,
        out float radius)
    {
        radius = 0f;
        if (token.Equals("closest-side", StringComparison.OrdinalIgnoreCase))
        {
            radius = GetEllipseShapeRadius(center, nearSide, farSide, closestSide: true);
            return radius > 0f;
        }

        if (token.Equals("farthest-side", StringComparison.OrdinalIgnoreCase))
        {
            radius = GetEllipseShapeRadius(center, nearSide, farSide, closestSide: false);
            return radius > 0f;
        }

        return TryResolveCssShapeLength(token, svgTextBase, viewport, assetLoader, renderingType, out radius) &&
               radius > 0f;
    }

    private static float GetEllipseShapeRadius(float center, float nearSide, float farSide, bool closestSide)
    {
        var nearDistance = Math.Abs(center - nearSide);
        var farDistance = Math.Abs(farSide - center);
        return closestSide
            ? Math.Min(nearDistance, farDistance)
            : Math.Max(nearDistance, farDistance);
    }

    private static bool TryParsePolygonShape(
        string value,
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out SKPoint[] points,
        out SKPathFillType fillType)
    {
        points = Array.Empty<SKPoint>();
        fillType = SKPathFillType.Winding;
        if (!TryGetCssFunctionArguments(value, "polygon", out var arguments))
        {
            return false;
        }

        var parts = SplitCssShapeList(arguments);
        var resolvedPoints = new List<SKPoint>(parts.Length);
        var startIndex = 0;
        if (parts.Length > 0)
        {
            if (parts[0].Equals("evenodd", StringComparison.OrdinalIgnoreCase))
            {
                fillType = SKPathFillType.EvenOdd;
                startIndex = 1;
            }
            else if (parts[0].Equals("nonzero", StringComparison.OrdinalIgnoreCase))
            {
                fillType = SKPathFillType.Winding;
                startIndex = 1;
            }
        }

        for (var i = startIndex; i < parts.Length; i++)
        {
            var tokens = SplitCssShapeTokens(parts[i]);
            if (tokens.Count != 2 ||
                !TryResolveCssShapeLength(tokens[0], svgTextBase, viewport, assetLoader, UnitRenderingType.Horizontal, out var x) ||
                !TryResolveCssShapeLength(tokens[1], svgTextBase, viewport, assetLoader, UnitRenderingType.Vertical, out var y))
            {
                return false;
            }

            resolvedPoints.Add(new SKPoint(viewport.Left + x, viewport.Top + y));
        }

        points = resolvedPoints.ToArray();
        return resolvedPoints.Count >= 3;
    }

    private static bool TryGetCssFunctionArguments(string value, string functionName, out string arguments)
    {
        arguments = string.Empty;
        var prefix = functionName + "(";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !value.EndsWith(")", StringComparison.Ordinal))
        {
            return false;
        }

        arguments = value.Substring(prefix.Length, value.Length - prefix.Length - 1).Trim();
        return true;
    }

    private static void SplitShapeAtKeyword(string value, string keyword, out string before, out string after)
    {
        var marker = " " + keyword + " ";
        var index = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            before = value;
            after = string.Empty;
            return;
        }

        before = value.Substring(0, index);
        after = value.Substring(index + marker.Length);
    }

    private static List<string> SplitCssShapeTokens(string value)
    {
        return value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(static token => token.Trim())
            .Where(static token => token.Length > 0)
            .ToList();
    }

    private static string[] SplitCssShapeList(string value)
    {
        return value
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => part.Trim())
            .Where(static part => part.Length > 0)
            .ToArray();
    }

    private static string[] SplitCssShapeReferenceList(string value)
    {
        var parts = new List<string>();
        var start = 0;
        var depth = 0;
        var quote = '\0';

        void AddPart(int end)
        {
            if (end <= start)
            {
                return;
            }

            var part = value.Substring(start, end - start).Trim();
            if (part.Length > 0)
            {
                if (IsCssShapeBoxKeyword(part) &&
                    parts.Count > 0 &&
                    (IsCssBasicShapeFunction(parts[parts.Count - 1]) ||
                     IsCssImageShapeFunction(parts[parts.Count - 1])))
                {
                    parts[parts.Count - 1] = parts[parts.Count - 1] + " " + part;
                }
                else if ((IsCssBasicShapeFunction(part) ||
                          IsCssImageShapeFunction(part)) &&
                    parts.Count > 0 &&
                    IsCssShapeBoxKeyword(parts[parts.Count - 1]))
                {
                    parts[parts.Count - 1] = parts[parts.Count - 1] + " " + part;
                }
                else
                {
                    parts.Add(part);
                }
            }
        }

        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (quote != '\0')
            {
                if (ch == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (ch == '\'' || ch == '"')
            {
                quote = ch;
                continue;
            }

            if (ch == '(')
            {
                depth++;
                continue;
            }

            if (ch == ')' && depth > 0)
            {
                depth--;
                continue;
            }

            if (depth == 0 && (char.IsWhiteSpace(ch) || ch == ','))
            {
                AddPart(i);
                start = i + 1;
            }
        }

        AddPart(value.Length);
        return parts.ToArray();
    }

    private static bool IsCssBasicShapeFunction(string value)
    {
        var trimmed = value.Trim();
        return (trimmed.StartsWith("inset(", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("circle(", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("ellipse(", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("polygon(", StringComparison.OrdinalIgnoreCase)) &&
               trimmed.EndsWith(")", StringComparison.Ordinal);
    }

    private static bool IsCssImageShapeFunction(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("url(", StringComparison.OrdinalIgnoreCase) &&
               trimmed.EndsWith(")", StringComparison.Ordinal);
    }

    private static bool TryResolveCssShapeCenter(
        string value,
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref float centerX,
        ref float centerY)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var tokens = SplitCssShapeTokens(value);
        if (tokens.Count == 1)
        {
            if (tokens[0].Equals("center", StringComparison.OrdinalIgnoreCase))
            {
                centerX = (viewport.Left + viewport.Right) * 0.5f;
                centerY = (viewport.Top + viewport.Bottom) * 0.5f;
                return true;
            }

            if (tokens[0].Equals("left", StringComparison.OrdinalIgnoreCase) ||
                tokens[0].Equals("right", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryResolveCssShapeLength(tokens[0], svgTextBase, viewport, assetLoader, UnitRenderingType.Horizontal, out var resolvedX))
                {
                    return false;
                }

                centerX = viewport.Left + resolvedX;
                return true;
            }

            if (tokens[0].Equals("top", StringComparison.OrdinalIgnoreCase) ||
                tokens[0].Equals("bottom", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryResolveCssShapeLength(tokens[0], svgTextBase, viewport, assetLoader, UnitRenderingType.Vertical, out var resolvedY))
                {
                    return false;
                }

                centerY = viewport.Top + resolvedY;
                return true;
            }

            if (!TryResolveCssShapeLength(tokens[0], svgTextBase, viewport, assetLoader, UnitRenderingType.Horizontal, out var singleX))
            {
                return false;
            }

            centerX = viewport.Left + singleX;
            return true;
        }

        if (tokens.Count != 2)
        {
            return tokens.Count == 4 &&
                   TryResolveCssPositionPair(tokens[0], tokens[1], svgTextBase, viewport, assetLoader, UnitRenderingType.Horizontal, viewport.Left, viewport.Right, out centerX) &&
                   TryResolveCssPositionPair(tokens[2], tokens[3], svgTextBase, viewport, assetLoader, UnitRenderingType.Vertical, viewport.Top, viewport.Bottom, out centerY);
        }

        if (!TryResolveCssShapeLength(tokens[0], svgTextBase, viewport, assetLoader, UnitRenderingType.Horizontal, out var x) ||
            !TryResolveCssShapeLength(tokens[1], svgTextBase, viewport, assetLoader, UnitRenderingType.Vertical, out var y))
        {
            return false;
        }

        centerX = viewport.Left + x;
        centerY = viewport.Top + y;
        return true;
    }

    private static bool TryResolveCssPositionPair(
        string side,
        string offset,
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        UnitRenderingType renderingType,
        float nearSide,
        float farSide,
        out float coordinate)
    {
        coordinate = 0f;
        if (!TryResolveCssShapeLength(offset, svgTextBase, viewport, assetLoader, renderingType, out var resolvedOffset))
        {
            return false;
        }

        if (side.Equals("left", StringComparison.OrdinalIgnoreCase) ||
            side.Equals("top", StringComparison.OrdinalIgnoreCase))
        {
            coordinate = nearSide + resolvedOffset;
            return true;
        }

        if (side.Equals("right", StringComparison.OrdinalIgnoreCase) ||
            side.Equals("bottom", StringComparison.OrdinalIgnoreCase))
        {
            coordinate = farSide - resolvedOffset;
            return true;
        }

        return false;
    }

    private static bool TryResolveCssShapeLength(
        string token,
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        UnitRenderingType renderingType,
        out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (token.EndsWith("%", StringComparison.Ordinal))
        {
            var raw = token.Substring(0, token.Length - 1);
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage))
            {
                return false;
            }

            var basis = renderingType == UnitRenderingType.Vertical
                ? viewport.Height
                : renderingType == UnitRenderingType.Horizontal
                    ? viewport.Width
                    : Math.Min(viewport.Width, viewport.Height);
            value = basis * percentage / 100f;
            return true;
        }

        if (token.Equals("center", StringComparison.OrdinalIgnoreCase))
        {
            value = renderingType == UnitRenderingType.Vertical ? viewport.Height * 0.5f : viewport.Width * 0.5f;
            return true;
        }

        if (token.Equals("left", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("top", StringComparison.OrdinalIgnoreCase))
        {
            value = 0f;
            return true;
        }

        if (token.Equals("right", StringComparison.OrdinalIgnoreCase))
        {
            value = viewport.Width;
            return true;
        }

        if (token.Equals("bottom", StringComparison.OrdinalIgnoreCase))
        {
            value = viewport.Height;
            return true;
        }

        try
        {
            var unit = SvgUnitConverter.Parse(token.AsSpan().Trim());
            value = ResolveTextUnitValue(unit, renderingType, svgTextBase, viewport, assetLoader);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCreateCssReferenceUris(string? value, out List<Uri> uris)
    {
        uris = new List<Uri>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var matches = s_cssUrlReferences.Matches(value);
        if (matches.Count == 0)
        {
            if (!TryCreateCssReferenceUri(value, out var singleUri))
            {
                return false;
            }

            uris.Add(singleUri);
            return true;
        }

        for (var i = 0; i < matches.Count; i++)
        {
            var rawUri = matches[i].Groups[1].Value.Trim();
            if (rawUri.Length >= 2 &&
                ((rawUri[0] == '\'' && rawUri[rawUri.Length - 1] == '\'') ||
                 (rawUri[0] == '"' && rawUri[rawUri.Length - 1] == '"')))
            {
                rawUri = rawUri.Substring(1, rawUri.Length - 2);
            }

            if (string.IsNullOrWhiteSpace(rawUri) ||
                !Uri.TryCreate(rawUri, UriKind.RelativeOrAbsolute, out var parsedUri))
            {
                return false;
            }

            uris.Add(parsedUri);
        }

        return uris.Count > 0;
    }

    private static bool TryCreateCssReferenceUri(string? value, out Uri uri)
    {
        return TryCreateCssReferenceUriText(value, out uri, out _);
    }

    private static bool TryCreateCssReferenceUriText(string? value, out Uri uri, out string normalizedValue)
    {
        uri = default!;
        normalizedValue = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        normalizedValue = value!.Trim();
        if (normalizedValue.StartsWith("url(", StringComparison.OrdinalIgnoreCase) &&
            normalizedValue.EndsWith(")", StringComparison.Ordinal))
        {
            normalizedValue = normalizedValue.Substring(4, normalizedValue.Length - 5).Trim();
        }

        if (normalizedValue.Length >= 2 &&
            ((normalizedValue[0] == '\'' && normalizedValue[normalizedValue.Length - 1] == '\'') ||
             (normalizedValue[0] == '"' && normalizedValue[normalizedValue.Length - 1] == '"')))
        {
            normalizedValue = normalizedValue.Substring(1, normalizedValue.Length - 2);
        }

        if (string.IsNullOrWhiteSpace(normalizedValue) ||
            !Uri.TryCreate(normalizedValue, UriKind.RelativeOrAbsolute, out var parsedUri))
        {
            return false;
        }

        uri = parsedUri;
        return true;
    }

    private static bool TryGetInlineSize(
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        bool isVertical,
        out float inlineSize)
    {
        inlineSize = 0f;
        var rawInlineSize = svgTextBase.InlineSize;
        if (string.IsNullOrWhiteSpace(rawInlineSize) ||
            rawInlineSize.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var unit = SvgUnitConverter.Parse(rawInlineSize.AsSpan().Trim());
            inlineSize = ResolveTextUnitValue(unit, isVertical ? UnitRenderingType.Vertical : UnitRenderingType.Horizontal, svgTextBase, viewport, assetLoader);
            return inlineSize >= 0f;
        }
        catch
        {
            inlineSize = 0f;
            return false;
        }
    }

    private static string? ResolveTextOverflowMarker(SvgTextBase svgTextBase)
    {
        var textOverflow = svgTextBase.TextOverflow;
        if (string.IsNullOrWhiteSpace(textOverflow) ||
            textOverflow.Trim().Equals("clip", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        textOverflow = textOverflow.Trim();
        if (textOverflow.Equals("ellipsis", StringComparison.OrdinalIgnoreCase))
        {
            return "\u2026";
        }

        return TryUnquoteCssString(textOverflow, out var marker)
            ? marker
            : null;
    }

    private static bool TryUnquoteCssString(string value, out string marker)
    {
        marker = string.Empty;
        if (value.Length < 2)
        {
            return false;
        }

        var quote = value[0];
        if ((quote != '"' && quote != '\'') || value[value.Length - 1] != quote)
        {
            return false;
        }

        marker = value.Substring(1, value.Length - 2);
        return true;
    }

    private static InlineSizeTextRun[] CreateInlineSizeTextRuns(IReadOnlyList<PreparedSequentialRun> runs)
    {
        var result = new List<InlineSizeTextRun>(runs.Count);
        for (var i = 0; i < runs.Count; i++)
        {
            if (!string.IsNullOrEmpty(runs[i].Text))
            {
                result.Add(new InlineSizeTextRun(runs[i].StyleSource, runs[i].Text, runs[i].Advance));
            }
        }

        return result.ToArray();
    }

    private static InlineSizeTextRun[] CreateEllipsizedInlineSizeTextRuns(
        IReadOnlyList<PreparedSequentialRun> runs,
        float inlineSize,
        string marker,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var markerStyle = GetLastTextRunStyle(runs);
        var markerAdvance = string.IsNullOrEmpty(marker)
            ? 0f
            : MeasureTextAdvance(markerStyle, marker, geometryBounds, assetLoader);
        var availableTextAdvance = Math.Max(0f, inlineSize - markerAdvance);
        var result = new List<InlineSizeTextRun>(runs.Count + 1);
        var usedAdvance = 0f;

        for (var i = 0; i < runs.Count; i++)
        {
            var remainingAdvance = availableTextAdvance - usedAdvance;
            if (remainingAdvance <= 0f)
            {
                break;
            }

            var trimmedText = TakeTextByAdvance(runs[i].StyleSource, runs[i].Text, remainingAdvance, geometryBounds, assetLoader, out var trimmedAdvance);
            if (!string.IsNullOrEmpty(trimmedText))
            {
                result.Add(new InlineSizeTextRun(runs[i].StyleSource, trimmedText, trimmedAdvance));
                usedAdvance += trimmedAdvance;
            }

            if (trimmedText.Length < runs[i].Text.Length)
            {
                break;
            }
        }

        if (!string.IsNullOrEmpty(marker))
        {
            result.Add(new InlineSizeTextRun(markerStyle, marker, markerAdvance));
        }

        return result.ToArray();
    }

    private static InlineSizeTextRun[] CreateEllipsizedInlineSizeTextRuns(
        IReadOnlyList<InlineSizeTextRun> runs,
        float inlineSize,
        string marker,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        return CreateEllipsizedInlineSizeTextRuns(
            runs,
            GetLastTextRunStyle(runs),
            inlineSize,
            marker,
            geometryBounds,
            assetLoader);
    }

    private static InlineSizeTextRun[] CreateEllipsizedInlineSizeTextRuns(
        IReadOnlyList<InlineSizeTextRun> runs,
        SvgTextBase markerStyle,
        float inlineSize,
        string marker,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var markerAdvance = string.IsNullOrEmpty(marker)
            ? 0f
            : MeasureTextAdvance(markerStyle, marker, geometryBounds, assetLoader);
        var availableTextAdvance = Math.Max(0f, inlineSize - markerAdvance);
        var result = new List<InlineSizeTextRun>(runs.Count + 1);
        var usedAdvance = 0f;

        for (var i = 0; i < runs.Count; i++)
        {
            var remainingAdvance = availableTextAdvance - usedAdvance;
            if (remainingAdvance <= 0f)
            {
                break;
            }

            var trimmedText = TakeTextByAdvance(runs[i].StyleSource, runs[i].Text, remainingAdvance, geometryBounds, assetLoader, out var trimmedAdvance);
            if (!string.IsNullOrEmpty(trimmedText))
            {
                result.Add(new InlineSizeTextRun(runs[i].StyleSource, trimmedText, trimmedAdvance));
                usedAdvance += trimmedAdvance;
            }

            if (trimmedText.Length < runs[i].Text.Length)
            {
                break;
            }
        }

        if (!string.IsNullOrEmpty(marker))
        {
            result.Add(new InlineSizeTextRun(markerStyle, marker, markerAdvance));
        }

        return result.ToArray();
    }

    private static SvgTextBase GetLastTextRunStyle(IReadOnlyList<PreparedSequentialRun> runs)
    {
        for (var i = runs.Count - 1; i >= 0; i--)
        {
            if (!string.IsNullOrEmpty(runs[i].Text))
            {
                return runs[i].StyleSource;
            }
        }

        return runs[0].StyleSource;
    }

    private static SvgTextBase GetLastTextRunStyle(IReadOnlyList<InlineSizeTextRun> runs)
    {
        for (var i = runs.Count - 1; i >= 0; i--)
        {
            if (!string.IsNullOrEmpty(runs[i].Text))
            {
                return runs[i].StyleSource;
            }
        }

        return runs[0].StyleSource;
    }

    private static float GetInlineSizeLineAdvance(
        IReadOnlyList<PreparedSequentialRun> runs,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        return GetInlineSizeLineAdvance(CreateInlineSizeTextRuns(runs), geometryBounds, assetLoader);
    }

    private static float GetInlineSizeLineAdvance(
        IReadOnlyList<InlineSizeTextRun> runs,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var lineAdvance = 0f;
        for (var i = 0; i < runs.Count; i++)
        {
            var paint = new SKPaint();
            PaintingService.SetPaintText(runs[i].StyleSource, geometryBounds, paint);
            var metrics = assetLoader.GetFontMetrics(paint);
            EnsureUsableFontMetrics(ref metrics, paint.TextSize);
            lineAdvance = Math.Max(lineAdvance, ResolveInlineSizeLineAdvance(runs[i].StyleSource, geometryBounds, assetLoader, paint, metrics));
        }

        return lineAdvance > 0f ? lineAdvance : 12f;
    }

    private static float ResolveInlineSizeLineAdvance(
        SvgTextBase styleSource,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        SKPaint paint,
        SKFontMetrics metrics)
    {
        var normalAdvance = Math.Max(metrics.Descent - metrics.Ascent, paint.TextSize * 1.2f);
        if (!TryResolveLineHeight(styleSource, geometryBounds, assetLoader, paint.TextSize, out var lineHeight) ||
            lineHeight <= 0f)
        {
            return normalAdvance;
        }

        return lineHeight;
    }

    private static bool TryResolveLineHeight(
        SvgTextBase styleSource,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        float fontSize,
        out float lineHeight)
    {
        lineHeight = 0f;
        var rawLineHeight = styleSource.ComputedStyle.LineHeight;
        if (string.IsNullOrWhiteSpace(rawLineHeight) ||
            rawLineHeight.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = rawLineHeight.Trim();
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var multiplier))
        {
            lineHeight = fontSize * multiplier;
            return true;
        }

        try
        {
            var unit = SvgUnitConverter.Parse(value.AsSpan());
            lineHeight = unit.Type == SvgUnitType.Percentage
                ? fontSize * unit.Value / 100f
                : ResolveTextUnitValue(unit, UnitRenderingType.Vertical, styleSource, geometryBounds, assetLoader);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string TakeTextByAdvance(
        SvgTextBase styleSource,
        string text,
        float maxAdvance,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out float advance)
    {
        advance = 0f;
        if (string.IsNullOrEmpty(text) || maxAdvance <= 0f)
        {
            return string.Empty;
        }

        var codepoints = SplitCodepointsReadOnly(text);
        var naturalAdvances = MeasureNaturalCodepointAdvances(styleSource, text, codepoints, geometryBounds, assetLoader);
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < codepoints.Count; i++)
        {
            var codepointAdvance = i < naturalAdvances.Length ? naturalAdvances[i] : MeasureTextAdvance(styleSource, codepoints[i], geometryBounds, assetLoader);
            if (advance + codepointAdvance > maxAdvance + TextLengthTolerance)
            {
                break;
            }

            builder.Append(codepoints[i]);
            advance += codepointAdvance;
        }

        return builder.ToString();
    }

    private static SKRect CreateInlineSizeClipRect(
        SvgTextBase svgTextBase,
        IReadOnlyList<InlineSizeTextRun> runs,
        float startX,
        float startY,
        float clipInlineStart,
        float inlineSize,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        if (IsVerticalWritingMode(svgTextBase))
        {
            var left = float.PositiveInfinity;
            var right = float.NegativeInfinity;

            void IncludeVerticalMetrics(SvgTextBase styleSource)
            {
                var paint = new SKPaint();
                PaintingService.SetPaintText(styleSource, geometryBounds, paint);
                var metrics = assetLoader.GetFontMetrics(paint);
                EnsureUsableFontMetrics(ref metrics, paint.TextSize);
                left = Math.Min(left, startX + metrics.Top);
                right = Math.Max(right, startX + metrics.Bottom);
            }

            if (runs.Count == 0)
            {
                IncludeVerticalMetrics(svgTextBase);
            }
            else
            {
                for (var i = 0; i < runs.Count; i++)
                {
                    IncludeVerticalMetrics(runs[i].StyleSource);
                }
            }

            if (float.IsInfinity(left) || float.IsInfinity(right) || right <= left)
            {
                return SKRect.Empty;
            }

            return new SKRect(left, clipInlineStart, right, clipInlineStart + inlineSize);
        }

        var top = float.PositiveInfinity;
        var bottom = float.NegativeInfinity;

        void IncludeMetrics(SvgTextBase styleSource)
        {
            var paint = new SKPaint();
            PaintingService.SetPaintText(styleSource, geometryBounds, paint);
            var metrics = assetLoader.GetFontMetrics(paint);
            EnsureUsableFontMetrics(ref metrics, paint.TextSize);
            top = Math.Min(top, startY + metrics.Top);
            bottom = Math.Max(bottom, startY + metrics.Bottom);
        }

        if (runs.Count == 0)
        {
            IncludeMetrics(svgTextBase);
        }
        else
        {
            for (var i = 0; i < runs.Count; i++)
            {
                IncludeMetrics(runs[i].StyleSource);
            }
        }

        if (float.IsInfinity(top) || float.IsInfinity(bottom) || bottom <= top)
        {
            return SKRect.Empty;
        }

        return new SKRect(clipInlineStart, top, clipInlineStart + inlineSize, bottom);
    }

    private static bool TryIntersectRect(SKRect left, SKRect right, out SKRect intersection)
    {
        intersection = SKRect.Empty;
        if (left.IsEmpty || right.IsEmpty)
        {
            return false;
        }

        var x1 = Math.Max(left.Left, right.Left);
        var y1 = Math.Max(left.Top, right.Top);
        var x2 = Math.Min(left.Right, right.Right);
        var y2 = Math.Min(left.Bottom, right.Bottom);
        if (x2 <= x1 || y2 <= y1)
        {
            return false;
        }

        intersection = new SKRect(x1, y1, x2, y2);
        return true;
    }

    private static bool TryDrawShapedSequentialTextRuns(
        SvgTextBase svgTextBase,
        IReadOnlyList<SequentialTextRun> runs,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint)
    {
        if (!TryShapeSequentialRuns(svgTextBase, runs, geometryBounds, assetLoader, out var combinedText, out var totalAdvance, out var segments))
        {
            return false;
        }

        ApplyInitialSequentialOffsets(svgTextBase, viewport, ref currentX, ref currentY);
        var inlineOrigin = ApplyTextAnchor(svgTextBase, currentX, geometryBounds, totalAdvance);
        var drawX = inlineOrigin;
        var drawY = currentY;

        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].Glyphs.Length == 0 || segments[i].Points.Length == 0)
            {
                continue;
            }

            var absolutePoints = OffsetPoints(segments[i].Points, drawX, drawY);
            var textBlob = SKTextBlob.CreatePositionedGlyphs(segments[i].Glyphs, absolutePoints);
            using var commandSource = PushTextCommandSource(canvas, segments[i].StyleSource, getElementAddressKey);

            _ = DrawTextPaintOrder(segments[i].StyleSource, includeFill: true, includeStroke: true, includeDecorations: false, phase =>
            {
                switch (phase)
                {
                    case TextPaintPhase.Fill:
                        if (SvgScenePaintingService.IsValidFill(segments[i].StyleSource))
                        {
                            var fillPaint = SvgScenePaintingService.GetFillPaint(segments[i].StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                            if (fillPaint is not null)
                            {
                                PaintingService.SetPaintText(segments[i].StyleSource, geometryBounds, fillPaint);
                                fillPaint.TextAlign = SKTextAlign.Left;
                                canvas.DrawText(textBlob, 0f, 0f, fillPaint);
                            }
                        }

                        break;

                    case TextPaintPhase.Stroke:
                        if (SvgScenePaintingService.IsValidStroke(segments[i].StyleSource, geometryBounds))
                        {
                            var strokePaint = SvgScenePaintingService.GetStrokePaint(segments[i].StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                            if (strokePaint is not null)
                            {
                                PaintingService.SetPaintText(segments[i].StyleSource, geometryBounds, strokePaint);
                                strokePaint.TextAlign = SKTextAlign.Left;
                                canvas.DrawText(textBlob, 0f, 0f, strokePaint);
                            }
                        }

                        break;
                }

                return 0f;
            });
        }

        currentX = inlineOrigin + totalAdvance;
        currentY = drawY;
        return true;
    }

    private static bool TryCreateBrowserShapedGlyphRun(
        SvgTextBase svgTextBase,
        string text,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out ShapedGlyphRun shapedRun,
        out float advance)
    {
        shapedRun = default;
        advance = 0f;
        var containsCursiveText = ContainsCursiveTrackingCodepoint(text);
        var containsEmojiText = ContainsEmojiPresentationCodepoint(text);
        if (string.IsNullOrEmpty(text) ||
            (!containsCursiveText && !containsEmojiText) ||
            HasEffectiveSpacingAdjustments(svgTextBase, text) ||
            assetLoader is not ISvgTextDirectedGlyphRunResolver glyphRunResolver)
        {
            return false;
        }

        if (containsCursiveText && ContainsMixedStrongDirections(text))
        {
            return false;
        }

        var shapedText = ApplyBrowserCompatibleBidiControls(svgTextBase, text);
        var rightToLeft = containsCursiveText || IsRightToLeft(svgTextBase);
        if (!glyphRunResolver.TryShapeGlyphRun(shapedText, paint, rightToLeft, out shapedRun) ||
            shapedRun.Glyphs.Length == 0 ||
            shapedRun.Points.Length != shapedRun.Glyphs.Length)
        {
            shapedRun = default;
            return false;
        }

        advance = EnsureWhitespaceAdvance(text, paint, assetLoader, shapedRun.Advance);
        return advance > 0f;
    }

    private static bool TryCreateBrowserBidiShapedGlyphRun(
        SvgTextBase svgTextBase,
        string text,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out ShapedGlyphRun shapedRun,
        out float advance)
    {
        shapedRun = default;
        advance = 0f;
        if (string.IsNullOrEmpty(text) ||
            SvgTextBidiResolver.ResolveUnicodeBidi(svgTextBase) != SvgUnicodeBidiMode.Normal ||
            SvgTextBidiResolver.ResolveDirection(svgTextBase) != SvgTextDirection.RightToLeft ||
            !ContainsMixedStrongDirections(text) ||
            HasEffectiveSpacingAdjustments(svgTextBase, text) ||
            assetLoader is not ISvgTextDirectedGlyphRunResolver glyphRunResolver)
        {
            return false;
        }

        var visualRuns = CreateLogicalBidiRuns(svgTextBase, text, SvgTextDirection.RightToLeft);
        if (visualRuns.Count <= 1)
        {
            return false;
        }

        var glyphs = new List<ushort>();
        var points = new List<SKPoint>();
        var clusters = new List<int>();
        var currentAdvance = 0f;
        for (var i = 0; i < visualRuns.Count; i++)
        {
            var visualRun = visualRuns[i];
            var runText = text.Substring(visualRun.StartCharIndex, visualRun.Length);
            if (!glyphRunResolver.TryShapeGlyphRun(runText, paint, visualRun.Direction == SvgTextDirection.RightToLeft, out var run) ||
                run.Glyphs.Length == 0 ||
                run.Points.Length != run.Glyphs.Length ||
                run.Clusters.Length != run.Glyphs.Length)
            {
                return false;
            }

            for (var glyphIndex = 0; glyphIndex < run.Glyphs.Length; glyphIndex++)
            {
                glyphs.Add(run.Glyphs[glyphIndex]);
                points.Add(new SKPoint(run.Points[glyphIndex].X + currentAdvance, run.Points[glyphIndex].Y));
                clusters.Add(visualRun.StartCharIndex + run.Clusters[glyphIndex]);
            }

            currentAdvance += run.Advance;
        }

        if (glyphs.Count == 0 || currentAdvance <= 0f)
        {
            return false;
        }

        advance = EnsureWhitespaceAdvance(text, paint, assetLoader, currentAdvance);
        shapedRun = new ShapedGlyphRun(glyphs.ToArray(), points.ToArray(), clusters.ToArray(), advance);
        return true;
    }

    private static bool TryCreateMixedScriptSpacingRunLayout(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out MixedScriptSpacingRunLayout? layout)
    {
        layout = null;
        if (!CanNeedMixedScriptSpacingRunLayout(svgTextBase, text, assetLoader) ||
            assetLoader is not ISvgTextDirectedGlyphRunResolver glyphRunResolver)
        {
            return false;
        }

        var firstCursiveCharIndex = -1;
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            if (IsCursiveTrackingCodepoint(codepoint))
            {
                firstCursiveCharIndex = charIndex - codepoint.Length;
                break;
            }
        }

        if (firstCursiveCharIndex <= 0 || firstCursiveCharIndex >= text.Length)
        {
            return false;
        }

        var prefixText = text.Substring(0, firstCursiveCharIndex);
        var suffixText = text.Substring(firstCursiveCharIndex);
        if (string.IsNullOrWhiteSpace(prefixText) ||
            string.IsNullOrWhiteSpace(suffixText))
        {
            return false;
        }

        var trailingText = string.Empty;
        var trailingAdvance = 0f;
        var trailingStart = GetLastCodepointStart(suffixText);
        if (trailingStart > 0)
        {
            var trailingCodepoint = suffixText.Substring(trailingStart);
            if (IsNeutralTrailingPunctuation(trailingCodepoint))
            {
                trailingText = trailingCodepoint;
                suffixText = suffixText.Substring(0, trailingStart);
                if (string.IsNullOrWhiteSpace(suffixText))
                {
                    return false;
                }
            }
        }

        var suffixPaint = paint.Clone();
        if (assetLoader is ISvgTextRunTypefaceResolver runTypefaceResolver)
        {
            var runTypeface = runTypefaceResolver.FindRunTypeface(suffixText, suffixPaint);
            if (runTypeface is not null)
            {
                suffixPaint.Typeface = runTypeface;
            }
        }

        if (suffixPaint.Typeface is null)
        {
            var spans = assetLoader.FindTypefaces(suffixText, suffixPaint);
            if (spans.Count != 1)
            {
                return false;
            }

            suffixPaint.Typeface = spans[0].Typeface;
        }

        if (!glyphRunResolver.TryShapeGlyphRun(suffixText, suffixPaint, rightToLeft: true, out var suffixGlyphRun) ||
            suffixGlyphRun.Glyphs.Length == 0 ||
            suffixGlyphRun.Points.Length != suffixGlyphRun.Glyphs.Length)
        {
            return false;
        }

        var prefixAdvance = MeasureCodepointSpacingAdvance(svgTextBase, prefixText, geometryBounds, assetLoader, includeTrailingSpacing: false);
        var boundaryAdvance = GetTextRunTrailingSpacingAdvance(svgTextBase, prefixText, geometryBounds);
        var suffixAdvance = EnsureWhitespaceAdvance(suffixText, suffixPaint, assetLoader, suffixGlyphRun.Advance);
        if (!string.IsNullOrEmpty(trailingText))
        {
            var trailingBounds = new SKRect();
            trailingAdvance = EnsureWhitespaceAdvance(trailingText, suffixPaint, assetLoader, assetLoader.MeasureText(trailingText, suffixPaint, ref trailingBounds));
        }

        if (prefixAdvance <= 0f || suffixAdvance <= 0f)
        {
            return false;
        }

        layout = new MixedScriptSpacingRunLayout(
            prefixText,
            prefixAdvance,
            boundaryAdvance,
            suffixGlyphRun,
            suffixPaint,
            suffixAdvance,
            trailingText,
            trailingAdvance,
            prefixAdvance + boundaryAdvance + suffixAdvance + trailingAdvance);
        return true;
    }

    private static bool IsNeutralTrailingPunctuation(string codepoint)
    {
        return codepoint is "." or "," or ":" or ";" or "!" or "?";
    }

    private static float MeasureCodepointSpacingAdvance(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        bool includeTrailingSpacing)
    {
        var codepoints = SplitCodepointsReadOnly(text);
        if (codepoints.Count == 0)
        {
            return 0f;
        }

        var naturalAdvances = MeasureNaturalCodepointAdvances(svgTextBase, text, codepoints, geometryBounds, assetLoader);
        var hasLetterSpacing = HasSpacingAdjustment(svgTextBase.LetterSpacing) && !SuppressesLetterSpacingForRun(codepoints);
        var hasWordSpacing = HasSpacingAdjustment(svgTextBase.WordSpacing);
        var totalAdvance = 0f;
        for (var i = 0; i < codepoints.Count; i++)
        {
            totalAdvance += naturalAdvances[i];
            if (i < codepoints.Count - 1 || includeTrailingSpacing)
            {
                if (hasLetterSpacing && SupportsLetterSpacing(codepoints[i]))
                {
                    totalAdvance += ResolveSpacingValue(svgTextBase, svgTextBase.LetterSpacing, geometryBounds, naturalAdvances[i]);
                }

                if (hasWordSpacing && IsWhitespaceCodepoint(codepoints[i]))
                {
                    totalAdvance += ResolveSpacingValue(svgTextBase, svgTextBase.WordSpacing, geometryBounds, naturalAdvances[i]);
                }
            }
        }

        return totalAdvance;
    }

    private static void DrawMixedScriptSpacingRun(
        SvgTextBase svgTextBase,
        MixedScriptSpacingRunLayout layout,
        float x,
        float y,
        SKRect geometryBounds,
        SKPaint paint,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader)
    {
        if (TryCreateAlignedCodepointPlacements(
                svgTextBase,
                layout.PrefixText,
                x,
                y,
                geometryBounds,
                SKTextAlign.Left,
                assetLoader,
                explicitRotations: null,
                out var prefixPlacements,
                out _))
        {
            _ = DrawCodepointPlacements(svgTextBase, layout.PrefixText, prefixPlacements, geometryBounds, paint, canvas, assetLoader);
        }
        else
        {
            canvas.DrawText(layout.PrefixText, x, y, paint);
        }

        var suffixX = x + layout.PrefixAdvance + layout.BoundaryAdvance;
        DrawShapedGlyphRun(layout.SuffixGlyphRun, suffixX, y, layout.SuffixPaint, canvas);
        if (!string.IsNullOrEmpty(layout.TrailingText))
        {
            canvas.DrawText(layout.TrailingText, suffixX + layout.SuffixAdvance, y, layout.SuffixPaint);
        }
    }

    private static void DrawShapedGlyphRun(
        ShapedGlyphRun shapedRun,
        float x,
        float y,
        SKPaint paint,
        SKCanvas canvas)
    {
        var absolutePoints = OffsetPoints(shapedRun.Points, x, y);
        var textBlob = SKTextBlob.CreatePositionedGlyphs(shapedRun.Glyphs, absolutePoints);
        canvas.DrawText(textBlob, 0f, 0f, paint);
    }

    private static bool TryMeasureShapedSequentialTextRuns(
        SvgTextBase svgTextBase,
        IReadOnlyList<SequentialTextRun> runs,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds)
    {
        if (!TryShapeSequentialRuns(svgTextBase, runs, viewport, assetLoader, out var combinedText, out var totalAdvance, out _))
        {
            return false;
        }

        ApplyInitialSequentialOffsets(svgTextBase, viewport, ref currentX, ref currentY);
        var inlineOrigin = ApplyTextAnchor(svgTextBase, currentX, viewport, totalAdvance);
        var runBounds = MeasureTextStringBoundsAlignedLeft(runs[0].StyleSource, combinedText, inlineOrigin, currentY, viewport, assetLoader, rotations: null, out _);
        UnionBounds(ref bounds, runBounds);
        currentX = inlineOrigin + totalAdvance;
        return true;
    }

    private static bool TryShapeSequentialRuns(
        SvgTextBase svgTextBase,
        IReadOnlyList<SequentialTextRun> runs,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out string combinedText,
        out float totalAdvance,
        out List<ShapedSequentialRunSegment> segments)
    {
        combinedText = string.Empty;
        totalAdvance = 0f;
        segments = new List<ShapedSequentialRunSegment>();
        if (runs.Count < 2 ||
            IsVerticalWritingMode(svgTextBase) ||
            assetLoader is not ISvgTextDirectedGlyphRunResolver glyphRunResolver ||
            !MayNeedShapedSequentialRuns(svgTextBase, runs))
        {
            return false;
        }

        combinedText = string.Concat(runs.Select(static run => run.Text));
        if (!SvgTextBidiResolver.NeedsVisualOrdering(svgTextBase, combinedText) &&
            !NeedsSequentialBidiOrdering(svgTextBase, runs))
        {
            return false;
        }

        if (!CanUseShapedSequentialRuns(runs, geometryBounds))
        {
            return false;
        }

        var runEndIndices = new int[runs.Count];
        var charIndex = 0;
        for (var i = 0; i < runs.Count; i++)
        {
            charIndex += runs[i].Text.Length;
            runEndIndices[i] = charIndex;
        }

        var segmentBuilders = new List<(SvgTextBase StyleSource, List<ushort> Glyphs, List<SKPoint> Points)>();
        var currentSegmentRunIndex = -1;
        List<ushort>? currentSegmentGlyphs = null;
        List<SKPoint>? currentSegmentPoints = null;

        void StartSegment(int runIndex)
        {
            currentSegmentRunIndex = runIndex;
            currentSegmentGlyphs = new List<ushort>();
            currentSegmentPoints = new List<SKPoint>();
            segmentBuilders.Add((runs[runIndex].StyleSource, currentSegmentGlyphs, currentSegmentPoints));
        }

        var shapingPaint = CreateTextMetricsPaint(runs[0].StyleSource, geometryBounds);
        shapingPaint.TextAlign = SKTextAlign.Left;
        var baseDirection = IsRightToLeft(svgTextBase) ? SvgTextDirection.RightToLeft : SvgTextDirection.LeftToRight;
        var bidiRuns = CreateLogicalBidiRuns(svgTextBase, combinedText, baseDirection, runs);
        if (bidiRuns.Count == 0)
        {
            return false;
        }

        foreach (var bidiRun in bidiRuns)
        {
            var bidiText = combinedText.Substring(bidiRun.StartCharIndex, bidiRun.Length);
            if (!glyphRunResolver.TryShapeGlyphRun(bidiText, shapingPaint, bidiRun.Direction == SvgTextDirection.RightToLeft, out var shapedRun) ||
                shapedRun.Glyphs.Length == 0 ||
                shapedRun.Points.Length != shapedRun.Glyphs.Length ||
                shapedRun.Clusters.Length != shapedRun.Glyphs.Length)
            {
                return false;
            }

            for (var i = 0; i < shapedRun.Glyphs.Length; i++)
            {
                var cluster = bidiRun.StartCharIndex + shapedRun.Clusters[i];
                var runIndex = GetSequentialRunIndex(runEndIndices, cluster);
                if (currentSegmentRunIndex != runIndex || currentSegmentGlyphs is null || currentSegmentPoints is null)
                {
                    StartSegment(runIndex);
                }

                currentSegmentGlyphs!.Add(shapedRun.Glyphs[i]);
                currentSegmentPoints!.Add(new SKPoint(shapedRun.Points[i].X + totalAdvance, shapedRun.Points[i].Y));
            }

            totalAdvance += shapedRun.Advance;
        }

        for (var i = 0; i < segmentBuilders.Count; i++)
        {
            segments.Add(new ShapedSequentialRunSegment(
                segmentBuilders[i].StyleSource,
                segmentBuilders[i].Glyphs.ToArray(),
                segmentBuilders[i].Points.ToArray()));
        }

        return segments.Any(static segment => segment.Glyphs.Length > 0);
    }

    private static bool MayNeedShapedSequentialRuns(
        SvgTextBase svgTextBase,
        IReadOnlyList<SequentialTextRun> runs)
    {
        if (GetInheritedTextAttribute(svgTextBase, "direction") is not null ||
            GetInheritedTextAttribute(svgTextBase, "unicode-bidi") is not null)
        {
            return true;
        }

        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (!ReferenceEquals(run.StyleSource, svgTextBase) &&
                (GetInheritedTextAttribute(run.StyleSource, "direction") is not null ||
                 GetInheritedTextAttribute(run.StyleSource, "unicode-bidi") is not null))
            {
                return true;
            }

            if (ContainsCursiveTrackingCodepoint(run.Text))
            {
                return true;
            }
        }

        return false;
    }

    private static List<LogicalBidiRun> CreateLogicalBidiRuns(SvgTextBase svgTextBase, string text, SvgTextDirection baseDirection)
    {
        var visualRuns = SvgTextBidiResolver.CreateVisualRuns(text, baseDirection, SvgTextBidiResolver.ResolveUnicodeBidi(svgTextBase));
        return ToLogicalBidiRuns(visualRuns);
    }

    private static List<LogicalBidiRun> CreateLogicalBidiRuns(
        SvgTextBase svgTextBase,
        string text,
        SvgTextDirection baseDirection,
        IReadOnlyList<InlineSizeTextRun> sourceRuns)
    {
        var spans = CreateBidiSpans(svgTextBase, sourceRuns);
        var visualRuns = spans.Length == 0
            ? SvgTextBidiResolver.CreateVisualRuns(text, baseDirection, SvgTextBidiResolver.ResolveUnicodeBidi(svgTextBase))
            : SvgTextBidiResolver.CreateVisualRuns(text, baseDirection, SvgTextBidiResolver.ResolveUnicodeBidi(svgTextBase), spans);
        return ToLogicalBidiRuns(visualRuns);
    }

    private static List<LogicalBidiRun> CreateLogicalBidiRuns(
        SvgTextBase svgTextBase,
        string text,
        SvgTextDirection baseDirection,
        IReadOnlyList<SequentialTextRun> sourceRuns)
    {
        var spans = CreateBidiSpans(svgTextBase, sourceRuns);
        var visualRuns = spans.Length == 0
            ? SvgTextBidiResolver.CreateVisualRuns(text, baseDirection, SvgTextBidiResolver.ResolveUnicodeBidi(svgTextBase))
            : SvgTextBidiResolver.CreateVisualRuns(text, baseDirection, SvgTextBidiResolver.ResolveUnicodeBidi(svgTextBase), spans);
        return ToLogicalBidiRuns(visualRuns);
    }

    private static List<LogicalBidiRun> ToLogicalBidiRuns(IReadOnlyList<SvgTextBidiRun> visualRuns)
    {
        var runs = new List<LogicalBidiRun>(visualRuns.Count);
        for (var i = 0; i < visualRuns.Count; i++)
        {
            runs.Add(new LogicalBidiRun(visualRuns[i].StartCharIndex, visualRuns[i].Length, visualRuns[i].Direction));
        }

        return runs;
    }

    private static SvgTextBidiSpan[] CreateBidiSpans(SvgTextBase svgTextBase, IReadOnlyList<InlineSizeTextRun> sourceRuns)
    {
        var spans = new List<SvgTextBidiSpan>();
        var charIndex = 0;
        for (var i = 0; i < sourceRuns.Count; i++)
        {
            var run = sourceRuns[i];
            AddBidiSpanIfNeeded(svgTextBase, run.StyleSource, charIndex, run.Text.Length, spans);
            charIndex += run.Text.Length;
        }

        return spans.ToArray();
    }

    private static SvgTextBidiSpan[] CreateBidiSpans(SvgTextBase svgTextBase, IReadOnlyList<SequentialTextRun> sourceRuns)
    {
        var spans = new List<SvgTextBidiSpan>();
        var charIndex = 0;
        for (var i = 0; i < sourceRuns.Count; i++)
        {
            var run = sourceRuns[i];
            AddBidiSpanIfNeeded(svgTextBase, run.StyleSource, charIndex, run.Text.Length, spans);
            charIndex += run.Text.Length;
        }

        return spans.ToArray();
    }

    private static void AddBidiSpanIfNeeded(
        SvgTextBase paragraph,
        SvgTextBase styleSource,
        int startCharIndex,
        int length,
        List<SvgTextBidiSpan> spans)
    {
        if (length <= 0)
        {
            return;
        }

        var mode = SvgTextBidiResolver.ResolveUnicodeBidi(styleSource);
        if (mode == SvgUnicodeBidiMode.Normal)
        {
            return;
        }

        var direction = SvgTextBidiResolver.ResolveDirection(styleSource);
        if (ReferenceEquals(styleSource, paragraph) &&
            mode == SvgTextBidiResolver.ResolveUnicodeBidi(paragraph) &&
            direction == SvgTextBidiResolver.ResolveDirection(paragraph))
        {
            return;
        }

        spans.Add(new SvgTextBidiSpan(startCharIndex, length, direction, mode));
    }

    private static bool NeedsSequentialBidiOrdering(SvgTextBase svgTextBase, IReadOnlyList<SequentialTextRun> sourceRuns)
    {
        var paragraphMode = SvgTextBidiResolver.ResolveUnicodeBidi(svgTextBase);
        var paragraphDirection = SvgTextBidiResolver.ResolveDirection(svgTextBase);
        for (var i = 0; i < sourceRuns.Count; i++)
        {
            var mode = SvgTextBidiResolver.ResolveUnicodeBidi(sourceRuns[i].StyleSource);
            if (mode == SvgUnicodeBidiMode.Normal)
            {
                continue;
            }

            var direction = SvgTextBidiResolver.ResolveDirection(sourceRuns[i].StyleSource);
            if (mode != paragraphMode || direction != paragraphDirection)
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanUseShapedSequentialRuns(IReadOnlyList<SequentialTextRun> runs, SKRect geometryBounds)
    {
        if (runs.Count < 2)
        {
            return false;
        }

        var referencePaint = CreateTextMetricsPaint(runs[0].StyleSource, geometryBounds);
        for (var i = 0; i < runs.Count; i++)
        {
            if (ResolveTextDecorationLayers(runs[i].StyleSource).Count > 0 ||
                RequiresSyntheticSmallCaps(runs[i].StyleSource, runs[i].Text) ||
                HasPerGlyphLayoutAdjustments(runs[i].StyleSource, runs[i].Text))
            {
                return false;
            }

            var candidatePaint = CreateTextMetricsPaint(runs[i].StyleSource, geometryBounds);
            if (!HasCompatibleShapingPaint(referencePaint, candidatePaint))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasCompatibleShapingPaint(SKPaint left, SKPaint right)
    {
        static (string Family, int Weight, int Width, int Slant) GetTypefaceSignature(SKTypeface? typeface)
        {
            return typeface is null
                ? (string.Empty, 0, 0, 0)
                : (typeface.FamilyName ?? string.Empty, (int)typeface.FontWeight, (int)typeface.FontWidth, (int)typeface.FontSlant);
        }

        return Math.Abs(left.TextSize - right.TextSize) <= 0.001f &&
               left.TextEncoding == right.TextEncoding &&
               GetTypefaceSignature(left.Typeface) == GetTypefaceSignature(right.Typeface);
    }

    private static int GetSequentialRunIndex(IReadOnlyList<int> runEndIndices, int cluster)
    {
        var low = 0;
        var high = runEndIndices.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            if (cluster < runEndIndices[middle])
            {
                high = middle - 1;
            }
            else
            {
                low = middle + 1;
            }
        }

        return Math.Min(low, runEndIndices.Count - 1);
    }

    private static SKPoint[] OffsetPoints(IReadOnlyList<SKPoint> points, float offsetX, float offsetY)
    {
        var result = new SKPoint[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            result[i] = new SKPoint(points[i].X + offsetX, points[i].Y + offsetY);
        }

        return result;
    }

    private static bool TryCollectSequentialTextRuns(
        SvgTextBase svgTextBase,
        bool requireAnchorContent,
        bool textReferencesEnabled,
        bool trimLeadingWhitespaceAtStart,
        out List<SequentialTextRun> runs,
        bool preservePreLineBreaks = false)
    {
        runs = new List<SequentialTextRun>();
        var hasAnchorContent = false;
        var trimLeadingWhitespace = trimLeadingWhitespaceAtStart;
        var previousEndedWithSpace = false;
        if (!TryCollectSequentialTextRuns(GetContentNodeList(svgTextBase), svgTextBase, runs, ref hasAnchorContent, ref trimLeadingWhitespace, ref previousEndedWithSpace, textReferencesEnabled, preservePreLineBreaks))
        {
            return false;
        }

        return runs.Count > 0 && (!requireAnchorContent || hasAnchorContent);
    }

    private static bool TryCollectSequentialTextRuns(
        IEnumerable<ISvgNode> contentNodes,
        SvgTextBase styleSource,
        List<SequentialTextRun> runs,
        ref bool hasAnchorContent,
        ref bool trimLeadingWhitespace,
        ref bool previousEndedWithSpace,
        bool textReferencesEnabled,
        bool preservePreLineBreaks = false)
    {
        var contentNodeList = ToContentNodeList(contentNodes);
        for (var nodeIndex = 0; nodeIndex < contentNodeList.Count; nodeIndex++)
        {
            var node = contentNodeList[nodeIndex];
            switch (node)
            {
                case SvgAnchor svgAnchor:
                    if (!CanRenderTextSubtree(svgAnchor))
                    {
                        break;
                    }

                    hasAnchorContent = true;
                    if (!TryCollectSequentialTextRuns(GetContentNodeList(svgAnchor), CreateAnchorTextStyleSource(svgAnchor), runs, ref hasAnchorContent, ref trimLeadingWhitespace, ref previousEndedWithSpace, textReferencesEnabled, preservePreLineBreaks))
                    {
                        return false;
                    }

                    break;

                case SvgAltGlyph svgAltGlyph when IsEmptyAltGlyph(svgAltGlyph):
                    return false;

                case SvgTextSpan svgTextSpan:
                    if (!CanRenderTextSubtree(svgTextSpan))
                    {
                        break;
                    }

                    if (HasExplicitTextPositioning(svgTextSpan))
                    {
                        return false;
                    }

                    var childTrimLeadingWhitespace = trimLeadingWhitespace || previousEndedWithSpace;
                    var childPreviousEndedWithSpace = false;
                    var beforeChildRuns = runs.Count;
                    if (!TryCollectSequentialTextRuns(GetContentNodeList(svgTextSpan), svgTextSpan, runs, ref hasAnchorContent, ref childTrimLeadingWhitespace, ref childPreviousEndedWithSpace, textReferencesEnabled, preservePreLineBreaks))
                    {
                        return false;
                    }

                    if (runs.Count > beforeChildRuns || childPreviousEndedWithSpace)
                    {
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = childPreviousEndedWithSpace;
                    }

                    break;

                case SvgTextPath:
                    return false;

                case SvgTextRef svgTextRef:
                    if (ShouldSuppressInlineTextReferenceContent(contentNodeList, nodeIndex))
                    {
                        break;
                    }

                    if (!CanRenderTextSubtree(svgTextRef) ||
                        HasExplicitTextPositioning(svgTextRef) ||
                        !textReferencesEnabled ||
                        !TryResolveTextReferenceContent(svgTextRef, out var referencedText))
                    {
                        return false;
                    }

                    var preparedReferencedText = PrepareResolvedContent(
                        svgTextRef,
                        referencedText,
                        trimLeadingWhitespace,
                        previousEndedWithSpace);
                    if (string.IsNullOrEmpty(preparedReferencedText))
                    {
                        break;
                    }

                    runs.Add(new SequentialTextRun(svgTextRef, preparedReferencedText!));
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = preparedReferencedText!.EndsWith(" ", StringComparison.Ordinal);
                    break;

                case not SvgTextBase:
                    if (string.IsNullOrEmpty(node.Content))
                    {
                        break;
                    }

                    var text = PrepareText(
                        styleSource,
                        node.Content,
                        trimLeadingWhitespace: trimLeadingWhitespace,
                        trimTrailingWhitespace: IsTerminalContentNode(contentNodeList, nodeIndex),
                        preservePreLineBreaks: preservePreLineBreaks);
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (previousEndedWithSpace &&
                            CollapsesTextWhitespace(styleSource) &&
                            text![0] == ' ')
                        {
                            text = text.TrimStart(' ');
                        }

                        if (string.IsNullOrEmpty(text))
                        {
                            break;
                        }

                        runs.Add(new SequentialTextRun(styleSource, text!));
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = text!.EndsWith(" ", StringComparison.Ordinal);
                    }

                    break;
            }
        }

        return true;
    }

    private static bool TryCollectTextPathRuns(SvgTextPath svgTextPath, SKRect viewport, out List<TextPathRun> runs)
    {
        runs = new List<TextPathRun>();
        var trimLeadingWhitespace = true;
        var previousEndedWithSpace = false;
        if (!TryCollectTextPathRuns(GetContentNodeList(svgTextPath), svgTextPath, svgTextPath, viewport, runs, ref trimLeadingWhitespace, ref previousEndedWithSpace) ||
            runs.Count == 0)
        {
            return false;
        }

        return true;
    }

    private static float GetSequentialRunBoundaryAdvance(
        IReadOnlyList<SequentialTextRun> runs,
        int runIndex,
        SKRect geometryBounds)
    {
        if (runIndex < 0 || runIndex >= runs.Count - 1)
        {
            return 0f;
        }

        return GetTextRunTrailingSpacingAdvance(runs[runIndex].StyleSource, runs[runIndex].Text, geometryBounds);
    }

    private static float GetPreparedSequentialRunBoundaryAdvance(
        IReadOnlyList<PreparedSequentialRun> runs,
        int runIndex,
        SKRect geometryBounds)
    {
        if (runIndex < 0 || runIndex >= runs.Count - 1)
        {
            return 0f;
        }

        return GetTextRunTrailingSpacingAdvance(runs[runIndex].StyleSource, runs[runIndex].Text, geometryBounds);
    }

    private static float GetTextRunTrailingSpacingAdvance(
        SvgTextBase styleSource,
        string text,
        SKRect geometryBounds)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        var lastCodepointStart = GetLastCodepointStart(text);
        if (lastCodepointStart < 0 || lastCodepointStart >= text.Length)
        {
            return 0f;
        }

        var codepoint = text.Substring(lastCodepointStart);
        var spacing = 0f;
        if (!SuppressesLetterSpacingForRun(text) && SupportsLetterSpacing(codepoint))
        {
            spacing += ResolveSpacingValue(styleSource, styleSource.LetterSpacing, geometryBounds, 0f);
        }

        if (IsWhitespaceCodepoint(codepoint))
        {
            spacing += ResolveSpacingValue(styleSource, styleSource.WordSpacing, geometryBounds, 0f);
        }

        return spacing;
    }

    private static bool TryCollectTextPathRuns(
        IEnumerable<ISvgNode> contentNodes,
        SvgTextBase styleSource,
        SvgTextBase textPathSource,
        SKRect viewport,
        List<TextPathRun> runs,
        ref bool trimLeadingWhitespace,
        ref bool previousEndedWithSpace)
    {
        var contentNodeList = ToContentNodeList(contentNodes);
        for (var nodeIndex = 0; nodeIndex < contentNodeList.Count; nodeIndex++)
        {
            var node = contentNodeList[nodeIndex];
            switch (node)
            {
                case SvgAnchor svgAnchor:
                    if (!CanRenderTextSubtree(svgAnchor))
                    {
                        break;
                    }

                    if (!TryCollectTextPathRuns(GetContentNodeList(svgAnchor), CreateAnchorTextStyleSource(svgAnchor), textPathSource, viewport, runs, ref trimLeadingWhitespace, ref previousEndedWithSpace))
                    {
                        return false;
                    }

                    break;

                case SvgTextSpan svgTextSpan:
                    if (!CanRenderTextSubtree(svgTextSpan))
                    {
                        break;
                    }

                    var firstRunIndex = runs.Count;
                    var childTrimLeadingWhitespace = trimLeadingWhitespace || previousEndedWithSpace;
                    var childPreviousEndedWithSpace = false;
                    if (!TryCollectTextPathRuns(GetContentNodeList(svgTextSpan), svgTextSpan, textPathSource, viewport, runs, ref childTrimLeadingWhitespace, ref childPreviousEndedWithSpace))
                    {
                        return false;
                    }

                    if (runs.Count > firstRunIndex)
                    {
                        var dx = GetTextPathRunOffset(svgTextSpan.Dx, UnitRenderingType.HorizontalOffset, svgTextSpan, viewport);
                        var dy = GetTextPathRunOffset(svgTextSpan.Dy, UnitRenderingType.VerticalOffset, svgTextSpan, viewport);
                        var x = GetTextPathRunAbsolutePosition(svgTextSpan.X, UnitRenderingType.HorizontalOffset, svgTextSpan, viewport);
                        var y = GetTextPathRunAbsolutePosition(svgTextSpan.Y, UnitRenderingType.VerticalOffset, svgTextSpan, viewport);
                        runs[firstRunIndex] = runs[firstRunIndex] with
                        {
                            Dx = runs[firstRunIndex].Dx + dx,
                            Dy = runs[firstRunIndex].Dy + dy,
                            X = x ?? runs[firstRunIndex].X,
                            Y = y ?? runs[firstRunIndex].Y
                        };

                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = childPreviousEndedWithSpace;
                    }

                    break;

                case SvgTextPath:
                    break;

                case SvgTextRef:
                    return false;

                case not SvgTextBase:
                    if (string.IsNullOrEmpty(node.Content))
                    {
                        break;
                    }

                    var text = PrepareText(
                        styleSource,
                        node.Content,
                        trimLeadingWhitespace: trimLeadingWhitespace,
                        trimTrailingWhitespace: IsTerminalContentNode(contentNodeList, nodeIndex));
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (previousEndedWithSpace &&
                            CollapsesTextWhitespace(styleSource) &&
                            text![0] == ' ')
                        {
                            text = text.TrimStart(' ');
                        }

                        if (!string.IsNullOrEmpty(text))
                        {
                            runs.Add(new TextPathRun(styleSource, textPathSource, text!, 0f, 0f, null, null));
                            trimLeadingWhitespace = false;
                            previousEndedWithSpace = text!.EndsWith(" ", StringComparison.Ordinal);
                        }
                    }

                    break;
            }
        }

        return true;
    }

    private static float GetTextPathRunOffset(SvgUnitCollection values, UnitRenderingType renderingType, SvgTextBase svgTextBase, SKRect viewport)
    {
        return values is { Count: > 0 }
            ? values[0].ToDeviceValue(renderingType, svgTextBase, viewport)
            : 0f;
    }

    private static float? GetTextPathRunAbsolutePosition(SvgUnitCollection values, UnitRenderingType renderingType, SvgTextBase svgTextBase, SKRect viewport)
    {
        return values is { Count: > 0 }
            ? values[0].ToDeviceValue(renderingType, svgTextBase, viewport)
            : null;
    }

    private static bool HasExplicitStartOffset(SvgTextPath svgTextPath)
    {
        return svgTextPath.StartOffset != SvgUnit.None && svgTextPath.StartOffset != SvgUnit.Empty;
    }

    private static bool HasInlineTextPathGeometry(SvgTextPath svgTextPath)
    {
        return svgTextPath.PathData is { Count: > 0 };
    }

    private static SvgTextBase ResolveTextPathLengthSource(TextPathRun run)
    {
        return HasOwnTextLengthAdjustment(run.StyleSource)
            ? run.StyleSource
            : run.TextPathSource;
    }

    private static SvgTextBase ResolveTextPathFilterSource(TextPathRun run)
    {
        return run.StyleSource is SvgVisualElement visualElement &&
               visualElement.Filter is not null &&
               !FilterEffectsService.IsNone(visualElement.Filter)
            ? run.StyleSource
            : run.TextPathSource;
    }

    private static bool HasRecursiveTextPathReference(SvgTextPath svgTextPath)
    {
        return !HasInlineTextPathGeometry(svgTextPath) &&
               SvgService.HasRecursiveReference(svgTextPath, static e => SvgService.GetEffectiveReferenceUri(e, e.ReferencedPath), new HashSet<Uri>());
    }

    private static float GetTextPathInitialGlyphOffset(IReadOnlyList<TextPathRun> runs, SKRect geometryBounds, ISvgAssetLoader assetLoader)
    {
        var advanceCache = new Dictionary<TextRunAdvanceCacheKey, float>();
        for (var runIndex = 0; runIndex < runs.Count; runIndex++)
        {
            var run = runs[runIndex];
            if (string.IsNullOrEmpty(run.Text))
            {
                continue;
            }

            var codepoints = SplitCodepointsReadOnly(run.Text);
            for (var i = 0; i < codepoints.Count; i++)
            {
                var advance = MeasureTextAdvanceCached(run.StyleSource, codepoints[i], geometryBounds, assetLoader, advanceCache);
                if (advance > 0f)
                {
                    return advance * 0.5f;
                }
            }
        }

        return 0f;
    }

    private static float MeasureTextPathRunsAdvance(IReadOnlyList<TextPathRun> runs, SKRect geometryBounds, ISvgAssetLoader assetLoader)
    {
        var advanceCache = new Dictionary<TextRunAdvanceCacheKey, float>();
        var totalAdvance = 0f;
        for (var i = 0; i < runs.Count; i++)
        {
            totalAdvance += MeasureTextAdvanceCached(runs[i].StyleSource, runs[i].Text, geometryBounds, assetLoader, advanceCache);
        }

        return totalAdvance;
    }

    private static float ResolveTextPathHorizontalOffset(
        SvgTextPath svgTextPath,
        float startOffset,
        float pathLength,
        SKRect geometryBounds,
        IReadOnlyList<TextPathRun> runs,
        ISvgAssetLoader assetLoader)
    {
        var textAlign = GetTextAnchorAlign(svgTextPath, geometryBounds);
        if (textAlign == SKTextAlign.Left &&
            (svgTextPath.Side != SvgTextPathSide.Right || pathLength <= 0f))
        {
            return startOffset;
        }

        var totalAdvance = MeasureTextPathRunsAdvance(runs, geometryBounds, assetLoader);
        var hOffset = GetAlignedStartCoordinate(startOffset, totalAdvance, textAlign);
        return ApplyTextPathSideOffset(svgTextPath, hOffset, pathLength, totalAdvance);
    }

    private static bool TryMeasureInlineSizeTextPathRunsAdvance(
        SvgTextPath textPath,
        IReadOnlyList<TextPathRun> runs,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out float totalAdvance,
        IDictionary<TextRunAdvanceCacheKey, float>? advanceCache = null)
    {
        totalAdvance = 0f;
        if (runs.Count == 0)
        {
            return false;
        }

        advanceCache ??= new Dictionary<TextRunAdvanceCacheKey, float>();

        if (HasOwnTextLengthAdjustment(textPath))
        {
            return runs.Count == 1 &&
                   TryGetOwnTextLength(textPath, viewport, IsVerticalWritingMode(textPath), out totalAdvance) &&
                   totalAdvance > 0f;
        }

        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (run.X.HasValue)
            {
                totalAdvance = Math.Max(0f, run.X.Value);
            }

            totalAdvance = Math.Max(0f, totalAdvance + run.Dx);
            var lengthSource = ResolveTextPathLengthSource(run);
            if (!ReferenceEquals(lengthSource, run.TextPathSource) &&
                TryGetOwnTextLength(lengthSource, viewport, IsVerticalWritingMode(run.StyleSource), out var specifiedLength) &&
                specifiedLength > 0f)
            {
                totalAdvance += specifiedLength;
                continue;
            }

            totalAdvance += MeasureTextAdvanceCached(run.StyleSource, run.Text, geometryBounds, assetLoader, advanceCache);
        }

        return totalAdvance > 0f;
    }

    private static void DrawPositionedTextPathRuns(
        IReadOnlyList<PositionedTextPathRun> runs,
        SKRect viewport,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint)
    {
        if (runs.Count == 0)
        {
            return;
        }

        var fallbackResolver = GetFallbackCodepointResolver(runs[0].StyleSource);
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (TryDrawFilteredPositionedTextPathRun(run, viewport, geometryBounds, ignoreAttributes, canvas, assetLoader, references, getElementAddressKey, fallbackResolver, contextPaint))
            {
                continue;
            }

            DrawPositionedTextPathRun(run, geometryBounds, ignoreAttributes, canvas, assetLoader, getElementAddressKey, includeFill: true, includeStroke: true, includeDecorations: true, fallbackResolver, contextPaint);
        }
    }

    private static SKRect GetPositionedTextPathRunBounds(
        PositionedTextPathRun run,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        FallbackCodepointResolver? fallbackResolver)
    {
        return run.HasFastBounds && !run.FastBounds.IsEmpty
            ? run.FastBounds
            : MeasureCodepointPlacementBounds(
                run.StyleSource,
                run.Text,
                run.Placements,
                geometryBounds,
                assetLoader,
                out _,
                fallbackResolver);
    }

    private static void DrawStretchedTextPathRuns(
        IReadOnlyList<StretchedTextPathRun> runs,
        SKRect viewport,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint)
    {
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (TryDrawFilteredStretchedTextPathRun(run, viewport, geometryBounds, ignoreAttributes, canvas, assetLoader, references, getElementAddressKey, contextPaint))
            {
                continue;
            }

            DrawStretchedTextPathRun(run, geometryBounds, ignoreAttributes, canvas, assetLoader, getElementAddressKey, includeFill: true, includeStroke: true, includeDecorations: true, contextPaint);
        }
    }

    private static void DrawStretchedTextPathRun(
        StretchedTextPathRun run,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        bool includeFill,
        bool includeStroke,
        bool includeDecorations,
        SvgSceneContextPaint? contextPaint)
    {
        using var commandSource = PushTextCommandSource(canvas, run.CommandSource, getElementAddressKey);
        var drawDecorations = includeDecorations && run.Decorations.Count > 0;
        _ = DrawTextPaintOrder(run.StyleSource, includeFill, includeStroke, drawDecorations, phase =>
        {
            switch (phase)
            {
                case TextPaintPhase.Fill:
                    if (SvgScenePaintingService.IsValidFill(run.StyleSource))
                    {
                        var fillPaint = SvgScenePaintingService.GetFillPaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                        if (fillPaint is not null)
                        {
                            canvas.DrawPath(run.Path, fillPaint);
                        }
                    }

                    break;

                case TextPaintPhase.Stroke:
                    if (SvgScenePaintingService.IsValidStroke(run.StyleSource, geometryBounds))
                    {
                        var strokePaint = SvgScenePaintingService.GetStrokePaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                        if (strokePaint is not null)
                        {
                            canvas.DrawPath(run.Path, strokePaint);
                        }
                    }

                    break;

                case TextPaintPhase.Decorations:
                    DrawStretchedTextPathDecorations(run.Decorations, geometryBounds, ignoreAttributes, canvas, assetLoader, contextPaint);
                    break;
            }

            return 0f;
        });
    }

    private static void DrawStretchedTextPathDecorations(
        IReadOnlyList<StretchedTextPathDecorationRun> decorations,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        SvgSceneContextPaint? contextPaint)
    {
        for (var i = 0; i < decorations.Count; i++)
        {
            var decoration = decorations[i];
            var fillPaint = SvgScenePaintingService.IsValidFill(decoration.Layer.PaintSource)
                ? SvgScenePaintingService.GetFillPaint(decoration.Layer.PaintSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint)
                : null;
            var strokePaint = SvgScenePaintingService.IsValidStroke(decoration.Layer.PaintSource, geometryBounds)
                ? SvgScenePaintingService.GetStrokePaint(decoration.Layer.PaintSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint)
                : null;

            if (fillPaint is null && strokePaint is null)
            {
                continue;
            }

            DrawDecorationPath(decoration.Path, decoration.Layer.PaintSource, fillPaint, strokePaint, canvas);
        }
    }

    private static void DrawPositionedTextPathRun(
        PositionedTextPathRun run,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        bool includeFill,
        bool includeStroke,
        bool includeDecorations,
        FallbackCodepointResolver fallbackResolver,
        SvgSceneContextPaint? contextPaint)
    {
        using var commandSource = PushTextCommandSource(canvas, run.StyleSource, getElementAddressKey);
        var drawDecorations = includeDecorations && HasTextDecorationLayers(run.StyleSource);
        _ = DrawTextPaintOrder(run.StyleSource, includeFill, includeStroke, drawDecorations, phase =>
        {
            switch (phase)
            {
                case TextPaintPhase.Fill:
                    if (SvgScenePaintingService.IsValidFill(run.StyleSource))
                    {
                        var fillPaint = SvgScenePaintingService.GetFillPaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                        if (fillPaint is not null)
                        {
                            _ = DrawCodepointPlacements(run.StyleSource, run.Text, run.Placements, geometryBounds, fillPaint, canvas, assetLoader, fallbackResolver);
                        }
                    }

                    break;

                case TextPaintPhase.Stroke:
                    if (SvgScenePaintingService.IsValidStroke(run.StyleSource, geometryBounds))
                    {
                        var strokePaint = SvgScenePaintingService.GetStrokePaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                        if (strokePaint is not null)
                        {
                            _ = DrawCodepointPlacements(run.StyleSource, run.Text, run.Placements, geometryBounds, strokePaint, canvas, assetLoader, fallbackResolver);
                        }
                    }

                    break;

                case TextPaintPhase.Decorations:
                    DrawTextDecorations(
                        ResolveTextDecorationLayers(run.StyleSource),
                        run.StyleSource,
                        run.Text,
                        run.Placements,
                        geometryBounds,
                        ignoreAttributes,
                        canvas,
                        assetLoader,
                        contextPaint);
                    break;
            }

            return 0f;
        });
    }

    private static bool TryDrawFilteredStretchedTextPathRun(
        StretchedTextPathRun run,
        SKRect viewport,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint)
    {
        if (ignoreAttributes.HasFlag(DrawAttributes.Filter) ||
            run.FilterSource is not SvgVisualElement visualElement ||
            !SvgSceneFilterContext.HasFilterDeclaration(visualElement))
        {
            return false;
        }

        var runBounds = MeasureStretchedTextPathRunBounds(run);
        if (runBounds.IsEmpty)
        {
            return true;
        }

        using var commandSource = PushTextCommandSource(canvas, run.CommandSource, getElementAddressKey);
        if (TryCreateSimpleTextPathRunFilterPaint(visualElement, runBounds, viewport, out var simpleFilterPaint, out var simpleFilterClip))
        {
            if (simpleFilterPaint is null)
            {
                return true;
            }

            canvas.Save();
            if (simpleFilterClip is { } resolvedSimpleFilterClip)
            {
                canvas.ClipRect(resolvedSimpleFilterClip, SKClipOperation.Intersect);
                canvas.SaveLayer(resolvedSimpleFilterClip, simpleFilterPaint);
            }
            else
            {
                canvas.SaveLayer(simpleFilterPaint);
            }
            DrawStretchedTextPathRun(run, geometryBounds, ignoreAttributes, canvas, assetLoader, getElementAddressKey, includeFill: true, includeStroke: true, includeDecorations: true, contextPaint);
            canvas.Restore();
            canvas.Restore();
            return true;
        }

        var sourceGraphic = RecordStretchedTextPathRunPicture(run, geometryBounds, ignoreAttributes, assetLoader, runBounds, getElementAddressKey, includeFill: true, includeStroke: true, includeDecorations: true, contextPaint);
        if (sourceGraphic is null)
        {
            return true;
        }

        var fillPaint = SvgScenePaintingService.IsValidFill(run.StyleSource)
            ? RecordStretchedTextPathRunPicture(run, geometryBounds, ignoreAttributes, assetLoader, runBounds, getElementAddressKey, includeFill: true, includeStroke: false, includeDecorations: false, contextPaint)
            : null;
        var strokePaint = SvgScenePaintingService.IsValidStroke(run.StyleSource, geometryBounds)
            ? RecordStretchedTextPathRunPicture(run, geometryBounds, ignoreAttributes, assetLoader, runBounds, getElementAddressKey, includeFill: false, includeStroke: true, includeDecorations: false, contextPaint)
            : null;

        var filterContext = new SvgSceneFilterContext(
            CreateAdHocSceneDocument(visualElement.OwnerDocument, viewport, assetLoader, ignoreAttributes),
            visualElement,
            runBounds,
            viewport,
            new PictureFilterSource(sourceGraphic, fillPaint, strokePaint),
            assetLoader,
            CreateFilterReferences(visualElement, references));

        if (!filterContext.IsValid)
        {
            return true;
        }

        if (filterContext.FilterPaint is null)
        {
            return false;
        }

        canvas.Save();
        if (filterContext.FilterClip is { } filterClip)
        {
            canvas.ClipRect(filterClip, SKClipOperation.Intersect);
            canvas.SaveLayer(filterClip, filterContext.FilterPaint);
        }
        else
        {
            canvas.SaveLayer(filterContext.FilterPaint);
        }
        canvas.DrawPicture(sourceGraphic);
        canvas.Restore();
        canvas.Restore();
        return true;
    }

    private static bool TryDrawFilteredPositionedTextPathRun(
        PositionedTextPathRun run,
        SKRect viewport,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references,
        Func<SvgElement?, string?>? getElementAddressKey,
        FallbackCodepointResolver fallbackResolver,
        SvgSceneContextPaint? contextPaint)
    {
        if (ignoreAttributes.HasFlag(DrawAttributes.Filter) ||
            run.StyleSource is not SvgVisualElement visualElement ||
            !SvgSceneFilterContext.HasFilterDeclaration(visualElement))
        {
            return false;
        }

        var runBounds = MeasureCodepointPlacementBounds(run.StyleSource, run.Text, run.Placements, geometryBounds, assetLoader, out _, fallbackResolver);
        if (runBounds.IsEmpty)
        {
            return true;
        }

        using var commandSource = PushTextCommandSource(canvas, run.StyleSource, getElementAddressKey);
        if (TryCreateSimpleTextPathRunFilterPaint(visualElement, runBounds, viewport, out var simpleFilterPaint, out var simpleFilterClip))
        {
            if (simpleFilterPaint is null)
            {
                return true;
            }

            canvas.Save();
            if (simpleFilterClip is { } resolvedSimpleFilterClip)
            {
                canvas.ClipRect(resolvedSimpleFilterClip, SKClipOperation.Intersect);
                canvas.SaveLayer(resolvedSimpleFilterClip, simpleFilterPaint);
            }
            else
            {
                canvas.SaveLayer(simpleFilterPaint);
            }
            DrawPositionedTextPathRun(run, geometryBounds, ignoreAttributes, canvas, assetLoader, getElementAddressKey, includeFill: true, includeStroke: true, includeDecorations: true, fallbackResolver, contextPaint);
            canvas.Restore();
            canvas.Restore();
            return true;
        }

        var sourceGraphic = RecordPositionedTextPathRunPicture(run, geometryBounds, ignoreAttributes, assetLoader, runBounds, getElementAddressKey, includeFill: true, includeStroke: true, includeDecorations: true, fallbackResolver, contextPaint);
        if (sourceGraphic is null)
        {
            return true;
        }

        var fillPaint = SvgScenePaintingService.IsValidFill(run.StyleSource)
            ? RecordPositionedTextPathRunPicture(run, geometryBounds, ignoreAttributes, assetLoader, runBounds, getElementAddressKey, includeFill: true, includeStroke: false, includeDecorations: false, fallbackResolver, contextPaint)
            : null;
        var strokePaint = SvgScenePaintingService.IsValidStroke(run.StyleSource, geometryBounds)
            ? RecordPositionedTextPathRunPicture(run, geometryBounds, ignoreAttributes, assetLoader, runBounds, getElementAddressKey, includeFill: false, includeStroke: true, includeDecorations: false, fallbackResolver, contextPaint)
            : null;

        var filterContext = new SvgSceneFilterContext(
            CreateAdHocSceneDocument(visualElement.OwnerDocument, viewport, assetLoader, ignoreAttributes),
            visualElement,
            runBounds,
            viewport,
            new PictureFilterSource(sourceGraphic, fillPaint, strokePaint),
            assetLoader,
            CreateFilterReferences(visualElement, references));

        if (!filterContext.IsValid)
        {
            return true;
        }

        if (filterContext.FilterPaint is null)
        {
            return false;
        }

        canvas.Save();
        if (filterContext.FilterClip is { } filterClip)
        {
            canvas.ClipRect(filterClip, SKClipOperation.Intersect);
            canvas.SaveLayer(filterClip, filterContext.FilterPaint);
        }
        else
        {
            canvas.SaveLayer(filterContext.FilterPaint);
        }
        canvas.DrawPicture(sourceGraphic);
        canvas.Restore();
        canvas.Restore();
        return true;
    }

    private static bool TryCreateSimpleTextPathRunFilterPaint(
        SvgVisualElement visualElement,
        SKRect runBounds,
        SKRect viewport,
        out SKPaint? filterPaint,
        out SKRect? filterClip)
    {
        filterPaint = null;
        filterClip = null;

        if (!TryGetLinkedFilters(visualElement, out var linkedFilters) ||
            linkedFilters.Count == 0)
        {
            return false;
        }

        Svg.FilterEffects.SvgFilter? firstChildren = null;
        Svg.FilterEffects.SvgFilter? firstX = null;
        Svg.FilterEffects.SvgFilter? firstY = null;
        Svg.FilterEffects.SvgFilter? firstWidth = null;
        Svg.FilterEffects.SvgFilter? firstHeight = null;
        Svg.FilterEffects.SvgFilter? firstFilterUnits = null;
        Svg.FilterEffects.SvgFilter? firstPrimitiveUnits = null;

        for (var i = 0; i < linkedFilters.Count; i++)
        {
            var filter = linkedFilters[i];
            if (firstChildren is null && filter.Children.Count > 0)
            {
                firstChildren = filter;
            }

            if (firstX is null && SvgService.TryGetAttribute(filter, "x", out _))
            {
                firstX = filter;
            }

            if (firstY is null && SvgService.TryGetAttribute(filter, "y", out _))
            {
                firstY = filter;
            }

            if (firstWidth is null && SvgService.TryGetAttribute(filter, "width", out _))
            {
                firstWidth = filter;
            }

            if (firstHeight is null && SvgService.TryGetAttribute(filter, "height", out _))
            {
                firstHeight = filter;
            }

            if (firstFilterUnits is null && SvgService.TryGetAttribute(filter, "filterUnits", out _))
            {
                firstFilterUnits = filter;
            }

            if (firstPrimitiveUnits is null && SvgService.TryGetAttribute(filter, "primitiveUnits", out _))
            {
                firstPrimitiveUnits = filter;
            }
        }

        if (firstChildren is null)
        {
            return false;
        }

        var primitives = firstChildren.Children.OfType<Svg.FilterEffects.SvgFilterPrimitive>().ToList();
        if (primitives.Count != 1 ||
            primitives[0] is not Svg.FilterEffects.SvgGaussianBlur gaussianBlur)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(gaussianBlur.Input) &&
            !string.Equals(gaussianBlur.Input, Svg.FilterEffects.SvgFilterPrimitive.SourceGraphic, StringComparison.Ordinal))
        {
            return false;
        }

        var xUnit = firstX?.X ?? new SvgUnit(SvgUnitType.Percentage, -10f);
        var yUnit = firstY?.Y ?? new SvgUnit(SvgUnitType.Percentage, -10f);
        var widthUnit = firstWidth?.Width ?? new SvgUnit(SvgUnitType.Percentage, 120f);
        var heightUnit = firstHeight?.Height ?? new SvgUnit(SvgUnitType.Percentage, 120f);
        var filterUnits = firstFilterUnits?.FilterUnits ?? SvgCoordinateUnits.ObjectBoundingBox;
        var primitiveUnits = firstPrimitiveUnits?.PrimitiveUnits ?? SvgCoordinateUnits.UserSpaceOnUse;

        var filterRegion = TransformsService.CalculateRect(xUnit, yUnit, widthUnit, heightUnit, filterUnits, runBounds, viewport, firstChildren);
        if (filterRegion is null)
        {
            return false;
        }

        gaussianBlur.StdDeviation.GetOptionalNumbers(0f, 0f, out var sigmaX, out var sigmaY);
        if (primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            var value = TransformsService.CalculateOtherPercentageValue(runBounds);
            sigmaX *= value;
            sigmaY *= value;
        }

        if (sigmaX < 0f || sigmaY < 0f)
        {
            return false;
        }

        filterPaint = new SKPaint
        {
            Style = SKPaintStyle.StrokeAndFill,
            ImageFilter = SKImageFilter.CreateBlur(sigmaX, sigmaY, null, filterRegion)
        };
        filterClip = filterRegion;
        return true;
    }

    private static bool TryGetLinkedFilters(SvgVisualElement visualElement, out List<Svg.FilterEffects.SvgFilter> filters)
    {
        filters = new List<Svg.FilterEffects.SvgFilter>();

        var currentFilter = SvgService.GetReference<Svg.FilterEffects.SvgFilter>(visualElement, SvgSceneFilterContext.GetFilterReferenceUri(visualElement));
        if (currentFilter is null)
        {
            return false;
        }

        var uris = new HashSet<Uri>();
        do
        {
            filters.Add(currentFilter);
            if (SvgService.HasRecursiveReference(currentFilter, static e => SvgService.GetEffectiveReferenceUri(e, e.Href), uris))
            {
                return filters.Count > 0;
            }

            currentFilter = SvgService.GetReference<Svg.FilterEffects.SvgFilter>(currentFilter, SvgService.GetEffectiveReferenceUri(currentFilter, currentFilter.Href));
        } while (currentFilter is not null);

        return filters.Count > 0;
    }

    private static SKPicture? RecordPositionedTextPathRunPicture(
        PositionedTextPathRun run,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        ISvgAssetLoader assetLoader,
        SKRect runBounds,
        Func<SvgElement?, string?>? getElementAddressKey,
        bool includeFill,
        bool includeStroke,
        bool includeDecorations,
        FallbackCodepointResolver fallbackResolver,
        SvgSceneContextPaint? contextPaint)
    {
        if (runBounds.IsEmpty)
        {
            return null;
        }

        var recorder = new SKPictureRecorder();
        var pictureCanvas = recorder.BeginRecording(runBounds);
        DrawPositionedTextPathRun(run, geometryBounds, ignoreAttributes | DrawAttributes.Filter, pictureCanvas, assetLoader, getElementAddressKey, includeFill, includeStroke, includeDecorations, fallbackResolver, contextPaint);
        return recorder.EndRecording();
    }

    private static SKRect MeasureStretchedTextPathRunBounds(StretchedTextPathRun run)
    {
        var bounds = run.Path.Bounds;
        for (var i = 0; i < run.Decorations.Count; i++)
        {
            UnionBounds(ref bounds, run.Decorations[i].Path.Bounds);
        }

        return bounds;
    }

    private static SKPicture? RecordStretchedTextPathRunPicture(
        StretchedTextPathRun run,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        ISvgAssetLoader assetLoader,
        SKRect runBounds,
        Func<SvgElement?, string?>? getElementAddressKey,
        bool includeFill,
        bool includeStroke,
        bool includeDecorations,
        SvgSceneContextPaint? contextPaint)
    {
        if (runBounds.IsEmpty)
        {
            return null;
        }

        var recorder = new SKPictureRecorder();
        var pictureCanvas = recorder.BeginRecording(runBounds);
        DrawStretchedTextPathRun(run, geometryBounds, ignoreAttributes | DrawAttributes.Filter, pictureCanvas, assetLoader, getElementAddressKey, includeFill, includeStroke, includeDecorations, contextPaint);
        return recorder.EndRecording();
    }

    private static SvgSceneDocument CreateAdHocSceneDocument(
        SvgDocument? sourceDocument,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        DrawAttributes ignoreAttributes)
    {
        var root = new SvgSceneNode(
            SvgSceneNodeKind.Fragment,
            sourceDocument,
            elementAddressKey: null,
            elementTypeName: sourceDocument?.GetType().Name ?? nameof(SvgDocument),
            compilationRootKey: null,
            isCompilationRootBoundary: false)
        {
            IsRenderable = false,
            IsVisible = true,
            Transform = SKMatrix.Identity,
            TotalTransform = SKMatrix.Identity,
            GeometryBounds = viewport,
            TransformedBounds = viewport
        };

        return new SvgSceneDocument(sourceDocument, viewport, viewport, root, assetLoader, ignoreAttributes);
    }

    private static HashSet<Uri>? CreateFilterReferences(SvgVisualElement visualElement, HashSet<Uri>? references)
    {
        return SvgService.ExtendImageReferences(references, visualElement.OwnerDocument);
    }

    private static bool TryCreateTextPathRunPlacements(
        IReadOnlyList<TextPathRun> runs,
        IReadOnlyList<PathSample> pathSamples,
        bool isClosedLoop,
        float startOffset,
        float baseVOffset,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out List<PositionedTextPathRun> positionedRuns,
        out float endOffset,
        out float endVOffset,
        float? specifiedLengthOverride = null)
    {
        positionedRuns = new List<PositionedTextPathRun>();
        endOffset = startOffset;
        endVOffset = baseVOffset;

        if (runs.Count == 0 || pathSamples.Count < 2)
        {
            return false;
        }

        var currentOffset = startOffset;
        var currentVOffset = baseVOffset;
        var runLengthOverrides = CreateTextPathRunLengthOverrides(runs, viewport, geometryBounds, assetLoader, isVertical: false, specifiedLengthOverride);
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            ApplyTextPathAbsolutePosition(pathSamples, startOffset, baseVOffset, run.X, run.Y, ref currentOffset, ref currentVOffset);
            currentOffset += run.Dx;
            currentVOffset += run.Dy;

            var lengthSource = ResolveTextPathLengthSource(run);
            var lengthOverride = ResolveTextPathRunLengthOverride(runs.Count, i, run, lengthSource, runLengthOverrides, specifiedLengthOverride);
            if (TryCreateTextPathCodepointPlacements(run.StyleSource, lengthSource, run.Text, currentOffset, currentVOffset, pathSamples, isClosedLoop, viewport, geometryBounds, assetLoader, out var renderedText, out var placements, out var advance, out var fastBounds, specifiedLengthOverride: lengthOverride))
            {
                positionedRuns.Add(new PositionedTextPathRun(run.StyleSource, renderedText, placements, fastBounds, !fastBounds.IsEmpty));
            }

            currentOffset += advance;
        }

        endOffset = currentOffset;
        endVOffset = currentVOffset;
        return positionedRuns.Count > 0;
    }

    private static float?[]? CreateTextPathRunLengthOverrides(
        IReadOnlyList<TextPathRun> runs,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        bool isVertical,
        float? specifiedLengthOverride = null)
    {
        if (runs.Count <= 1)
        {
            return null;
        }

        var specifiedLength = specifiedLengthOverride.GetValueOrDefault();
        if (!specifiedLengthOverride.HasValue)
        {
            var textPathSource = runs[0].TextPathSource;
            if (!TryGetOwnTextLength(textPathSource, viewport, isVertical, out specifiedLength))
            {
                return null;
            }
        }

        if (specifiedLength <= 0f)
        {
            return null;
        }

        var naturalAdvances = new float[runs.Count];
        var adjustableAdvance = 0f;
        var lastAdjustableRunIndex = -1;
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (!ReferenceEquals(ResolveTextPathLengthSource(run), run.TextPathSource))
            {
                continue;
            }

            var advance = MeasureTextAdvance(run.StyleSource, run.Text, geometryBounds, assetLoader);
            if (!IsValidPositiveAdvance(advance))
            {
                continue;
            }

            naturalAdvances[i] = advance;
            adjustableAdvance += advance;
            lastAdjustableRunIndex = i;
        }

        if (adjustableAdvance <= TextLengthTolerance ||
            lastAdjustableRunIndex < 0)
        {
            return null;
        }

        var overrides = new float?[runs.Count];
        var assignedLength = 0f;
        for (var i = 0; i < runs.Count; i++)
        {
            if (naturalAdvances[i] <= 0f)
            {
                continue;
            }

            var targetLength = i == lastAdjustableRunIndex
                ? Math.Max(0f, specifiedLength - assignedLength)
                : specifiedLength * (naturalAdvances[i] / adjustableAdvance);
            overrides[i] = targetLength;
            assignedLength += targetLength;
        }

        return overrides;
    }

    private static float? ResolveTextPathRunLengthOverride(
        int runCount,
        int runIndex,
        TextPathRun run,
        SvgTextBase lengthSource,
        IReadOnlyList<float?>? runLengthOverrides,
        float? specifiedLengthOverride)
    {
        if (!ReferenceEquals(lengthSource, run.TextPathSource))
        {
            return null;
        }

        if (runCount == 1 && specifiedLengthOverride.HasValue)
        {
            return specifiedLengthOverride;
        }

        return runLengthOverrides is not null && runIndex >= 0 && runIndex < runLengthOverrides.Count
            ? runLengthOverrides[runIndex]
            : null;
    }

    private static void ApplyTextPathAbsolutePosition(
        IReadOnlyList<PathSample> pathSamples,
        float baseOffset,
        float baseVOffset,
        float? x,
        float? y,
        ref float currentOffset,
        ref float currentVOffset)
    {
        if (!x.HasValue && !y.HasValue)
        {
            return;
        }

        if (x.HasValue)
        {
            currentOffset = baseOffset + x.Value;
            currentVOffset = baseVOffset;
            return;
        }

        _ = y;
    }

    private static void ApplyTextPathUserSpaceOffset(
        IReadOnlyList<PathSample> pathSamples,
        float dx,
        float dy,
        ref float currentOffset,
        ref float currentVOffset)
    {
        if (Math.Abs(dx) <= 0.001f && Math.Abs(dy) <= 0.001f)
        {
            return;
        }

        if (!TryGetTextPathPoint(currentOffset, currentVOffset, pathSamples, out var currentPoint))
        {
            currentOffset += dx;
            currentVOffset += dy;
            return;
        }

        var targetPoint = new SKPoint(currentPoint.X + dx, currentPoint.Y + dy);
        if (TryProjectPointOntoTextPath(pathSamples, targetPoint, out var projectedOffset, out var projectedVOffset))
        {
            currentOffset = projectedOffset;
            currentVOffset = projectedVOffset;
            return;
        }

        currentOffset += dx;
        currentVOffset += dy;
    }

    private static bool TryCreateStretchedTextPathRunPaths(
        IReadOnlyList<TextPathRun> runs,
        IReadOnlyList<PathSample> pathSamples,
        float pathLength,
        bool isClosedLoop,
        float startOffset,
        float baseVOffset,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out List<StretchedTextPathRun> stretchedRuns,
        out float endOffset,
        out float endVOffset,
        float? specifiedLengthOverride = null)
    {
        stretchedRuns = new List<StretchedTextPathRun>();
        endOffset = startOffset;
        endVOffset = baseVOffset;
        if (runs.Count == 0 || pathSamples.Count < 2 || pathLength <= 0f)
        {
            return false;
        }

        var currentOffset = startOffset;
        var currentVOffset = baseVOffset;
        var runLengthOverrides = CreateTextPathRunLengthOverrides(runs, viewport, geometryBounds, assetLoader, isVertical: false, specifiedLengthOverride);
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            ApplyTextPathAbsolutePosition(pathSamples, startOffset, baseVOffset, run.X, run.Y, ref currentOffset, ref currentVOffset);
            currentOffset += run.Dx;
            currentVOffset += run.Dy;

            var lengthSource = ResolveTextPathLengthSource(run);
            var lengthOverride = ResolveTextPathRunLengthOverride(runs.Count, i, run, lengthSource, runLengthOverrides, specifiedLengthOverride);
            if (TryCreateStretchedTextPathRunPath(run.StyleSource, lengthSource, run.Text, currentOffset, currentVOffset, pathSamples, pathLength, isClosedLoop, viewport, geometryBounds, assetLoader, out var stretchedText, out var stretchedPath, out var decorations, out var advance, specifiedLengthOverride: lengthOverride))
            {
                stretchedRuns.Add(new StretchedTextPathRun(run.StyleSource, run.TextPathSource, ResolveTextPathFilterSource(run), stretchedText, stretchedPath, decorations));
            }

            currentOffset += advance;
        }

        endOffset = currentOffset;
        endVOffset = currentVOffset;
        return stretchedRuns.Count > 0;
    }

    private static bool TryCreateStretchedTextPathRunPath(
        SvgTextBase svgTextBase,
        SvgTextBase lengthSource,
        string text,
        float startOffset,
        float baseVOffset,
        IReadOnlyList<PathSample> pathSamples,
        float pathLength,
        bool isClosedLoop,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out string stretchedText,
        out SKPath stretchedPath,
        out IReadOnlyList<StretchedTextPathDecorationRun> decorations,
        out float totalAdvance,
        float? specifiedLengthOverride = null)
    {
        stretchedText = string.Empty;
        stretchedPath = new SKPath();
        decorations = Array.Empty<StretchedTextPathDecorationRun>();
        var naturalAdvance = MeasureNaturalTextAdvance(svgTextBase, text, geometryBounds, assetLoader);
        totalAdvance = MeasureTextAdvance(svgTextBase, text, geometryBounds, assetLoader);
        if (string.IsNullOrEmpty(text) || totalAdvance <= 0f)
        {
            return false;
        }

        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;
        stretchedText = GetBrowserCompatibleFallbackText(svgTextBase, text, assetLoader);
        if (TryCreateBrowserCompatibleFullRunPaint(svgTextBase, stretchedText, paint, assetLoader, out var runPaint, out var shapedText))
        {
            paint = runPaint;
            stretchedText = shapedText;
        }

        var baselineVOffset = baseVOffset + GetBaselineOffset(svgTextBase, viewport, assetLoader);
        var clusterText = RemoveBidiFormattingControls(stretchedText);
        var effectiveClusterText = string.IsNullOrEmpty(clusterText) ? stretchedText : clusterText;
        var isRightToLeftTextRun = IsRightToLeft(svgTextBase) || ContainsRightToLeftStrongDirection(effectiveClusterText);
        var hasEffectiveSpacingAdjustments = HasEffectiveSpacingAdjustments(svgTextBase, effectiveClusterText);
        var hasShapedGlyphPath = TryCreateShapedStretchedTextPathRunOutline(
            svgTextBase,
            effectiveClusterText,
            paint,
            isRightToLeftTextRun,
            assetLoader,
            out var shapedGlyphPath);

        var specifiedLength = 0f;
        var hasOwnTextLength = specifiedLengthOverride.HasValue;
        if (hasOwnTextLength)
        {
            specifiedLength = specifiedLengthOverride.GetValueOrDefault();
        }
        else
        {
            hasOwnTextLength = TryGetOwnTextLength(lengthSource, viewport, isVertical: false, out specifiedLength);
        }

        var hasActiveTextLengthAdjustment = hasOwnTextLength &&
                                            specifiedLength > 0f &&
                                            Math.Abs(specifiedLength - naturalAdvance) > TextLengthTolerance;
        if (hasActiveTextLengthAdjustment)
        {
            var lengthAdjust = GetOwnLengthAdjust(lengthSource);
            if ((lengthAdjust == SvgTextLengthAdjust.Spacing ||
                 lengthAdjust == SvgTextLengthAdjust.SpacingAndGlyphs ||
                 hasEffectiveSpacingAdjustments) &&
                TryCreateClusterAdjustedStretchedTextPathRunPath(
                    svgTextBase,
                    effectiveClusterText,
                    paint,
                    isRightToLeftTextRun,
                    naturalAdvance,
                    specifiedLength,
                    lengthAdjust == SvgTextLengthAdjust.Spacing,
                    startOffset,
                    baselineVOffset,
                    pathSamples,
                    pathLength,
                    isClosedLoop,
                    geometryBounds,
                    assetLoader,
                    lengthAdjust == SvgTextLengthAdjust.SpacingAndGlyphs,
                    out stretchedPath,
                    out totalAdvance))
            {
                decorations = CreateStretchedTextPathDecorationRuns(svgTextBase, totalAdvance, pathSamples, pathLength, isClosedLoop, startOffset, baselineVOffset, geometryBounds, assetLoader);
                return true;
            }

            if (lengthAdjust == SvgTextLengthAdjust.Spacing &&
                isRightToLeftTextRun &&
                naturalAdvance > 0f &&
                TryCreateScaledStretchedTextPathRunPath(
                    string.IsNullOrEmpty(clusterText) ? stretchedText : clusterText,
                    paint,
                    naturalAdvance,
                    specifiedLength,
                    startOffset,
                    baselineVOffset,
                    pathSamples,
                    pathLength,
                    isClosedLoop,
                    assetLoader,
                    out stretchedPath))
            {
                totalAdvance = specifiedLength;
                decorations = CreateStretchedTextPathDecorationRuns(svgTextBase, totalAdvance, pathSamples, pathLength, isClosedLoop, startOffset, baselineVOffset, geometryBounds, assetLoader);
                return true;
            }

            if (lengthAdjust == SvgTextLengthAdjust.SpacingAndGlyphs && naturalAdvance > 0f)
            {
                var adjustedGlyphPath = hasShapedGlyphPath && shapedGlyphPath is not null
                    ? shapedGlyphPath.DeepClone()
                    : assetLoader.GetTextPath(stretchedText, paint, 0f, 0f);
                if (adjustedGlyphPath is null || adjustedGlyphPath.IsEmpty)
                {
                    return false;
                }

                ScalePathX(adjustedGlyphPath, new SKPoint(0f, 0f), specifiedLength / naturalAdvance);
                totalAdvance = specifiedLength;

                if (!TryWarpTextOutlinePath(adjustedGlyphPath, pathSamples, pathLength, isClosedLoop, startOffset, baselineVOffset, out stretchedPath))
                {
                    return false;
                }

                decorations = CreateStretchedTextPathDecorationRuns(svgTextBase, totalAdvance, pathSamples, pathLength, isClosedLoop, startOffset, baselineVOffset, geometryBounds, assetLoader);
                return true;
            }

            totalAdvance = specifiedLength;
        }
        else if (hasEffectiveSpacingAdjustments &&
                 TryCreateClusterAdjustedStretchedTextPathRunPath(
                     svgTextBase,
                     effectiveClusterText,
                     paint,
                     isRightToLeftTextRun,
                     naturalAdvance,
                     0f,
                     false,
                     startOffset,
                     baselineVOffset,
                     pathSamples,
                     pathLength,
                     isClosedLoop,
                     geometryBounds,
                     assetLoader,
                     false,
                     out stretchedPath,
                     out totalAdvance))
        {
            decorations = CreateStretchedTextPathDecorationRuns(svgTextBase, totalAdvance, pathSamples, pathLength, isClosedLoop, startOffset, baselineVOffset, geometryBounds, assetLoader);
            return true;
        }

        var glyphPath = hasShapedGlyphPath && shapedGlyphPath is not null
            ? shapedGlyphPath
            : assetLoader.GetTextPath(stretchedText, paint, 0f, 0f);
        if (glyphPath is null || glyphPath.IsEmpty)
        {
            return false;
        }

        if (!TryWarpTextOutlinePath(glyphPath, pathSamples, pathLength, isClosedLoop, startOffset, baselineVOffset, out stretchedPath))
        {
            return false;
        }

        decorations = CreateStretchedTextPathDecorationRuns(svgTextBase, totalAdvance, pathSamples, pathLength, isClosedLoop, startOffset, baselineVOffset, geometryBounds, assetLoader);
        return true;
    }

    private static bool TryCreateShapedStretchedTextPathRunOutline(
        SvgTextBase svgTextBase,
        string text,
        SKPaint paint,
        bool isRightToLeft,
        ISvgAssetLoader assetLoader,
        out SKPath glyphPath)
    {
        glyphPath = new SKPath();
        return assetLoader is ISvgTextGlyphRunPathResolver pathResolver &&
               TryShapeStretchTextRun(svgTextBase, text, paint, isRightToLeft, assetLoader, out var shapedRun) &&
               TryCreateGlyphRunPath(pathResolver, shapedRun, paint, out glyphPath);
    }

    private static bool TryCreateGlyphRunPath(
        ISvgTextGlyphRunPathResolver pathResolver,
        ShapedGlyphRun shapedRun,
        SKPaint paint,
        out SKPath glyphPath)
    {
        glyphPath = new SKPath();
        if (shapedRun.Glyphs.Length == 0 ||
            shapedRun.Points.Length != shapedRun.Glyphs.Length ||
            !pathResolver.TryGetGlyphRunPath(shapedRun, paint, 0f, 0f, out glyphPath))
        {
            return false;
        }

        return glyphPath is { IsEmpty: false } && HasMeaningfulPathExtent(glyphPath);
    }

    private static bool TryCreateScaledStretchedTextPathRunPath(
        string text,
        SKPaint paint,
        float naturalAdvance,
        float specifiedLength,
        float startOffset,
        float baseVOffset,
        IReadOnlyList<PathSample> pathSamples,
        float pathLength,
        bool isClosedLoop,
        ISvgAssetLoader assetLoader,
        out SKPath stretchedPath)
    {
        stretchedPath = new SKPath();
        if (string.IsNullOrEmpty(text) || naturalAdvance <= 0f || specifiedLength <= 0f)
        {
            return false;
        }

        var adjustedGlyphPath = assetLoader.GetTextPath(text, paint, 0f, 0f);
        if (adjustedGlyphPath is null || adjustedGlyphPath.IsEmpty)
        {
            return false;
        }

        TranslatePath(adjustedGlyphPath, -adjustedGlyphPath.Bounds.Left, 0f);
        ScalePathX(adjustedGlyphPath, new SKPoint(0f, 0f), specifiedLength / naturalAdvance);
        return TryWarpTextOutlinePath(adjustedGlyphPath, pathSamples, pathLength, isClosedLoop, startOffset, baseVOffset, out stretchedPath);
    }

    private static bool TryCreateClusterAdjustedStretchedTextPathRunPath(
        SvgTextBase svgTextBase,
        string text,
        SKPaint paint,
        bool isRightToLeft,
        float naturalAdvance,
        float targetAdvance,
        bool distributeTextLengthGap,
        float startOffset,
        float baseVOffset,
        IReadOnlyList<PathSample> pathSamples,
        float pathLength,
        bool isClosedLoop,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        bool scaleGlyphsAndSpacing,
        out SKPath stretchedPath,
        out float adjustedAdvance)
    {
        stretchedPath = new SKPath();
        adjustedAdvance = 0f;
        if (naturalAdvance <= 0f ||
            !TryCreateStretchedTextPathClusters(svgTextBase, text, paint, isRightToLeft, geometryBounds, assetLoader, out var clusters) ||
            clusters.Count == 0)
        {
            return false;
        }

        var plannerClusters = new SvgTextPathLayoutPlanner.StretchClusterInput[clusters.Count];
        for (var i = 0; i < clusters.Count; i++)
        {
            var spacingAfter = i < clusters.Count - 1
                ? ResolveStretchClusterSpacing(svgTextBase, clusters[i], geometryBounds)
                : 0f;
            plannerClusters[i] = new SvgTextPathLayoutPlanner.StretchClusterInput(
                clusters[i].NaturalOffset,
                clusters[i].NaturalAdvance,
                spacingAfter);
        }

        if (!SvgTextPathLayoutPlanner.TryCreateStretchClusterPlan(
                plannerClusters,
                naturalAdvance,
                targetAdvance,
                distributeTextLengthGap,
                scaleGlyphsAndSpacing,
                out var stretchPlan))
        {
            return false;
        }

        var appendedPath = false;
        adjustedAdvance = stretchPlan.AdjustedAdvance;
        for (var i = 0; i < stretchPlan.Placements.Count; i++)
        {
            var placement = stretchPlan.Placements[i];
            var cluster = clusters[placement.ClusterIndex];
            var adjustedOffset = placement.AdjustedOffset;
            var scaleX = placement.ScaleX;
            var clusterPath = cluster.NaturalPath?.DeepClone() ??
                              assetLoader.GetTextPath(
                                  cluster.Text,
                                  paint,
                                  scaleX != 1f ? cluster.NaturalOffset : isRightToLeft ? 0f : adjustedOffset,
                                  0f);
            if (clusterPath is null || clusterPath.IsEmpty)
            {
                continue;
            }

            if (scaleX != 1f)
            {
                ScalePathX(clusterPath, new SKPoint(0f, 0f), scaleX);
                TranslatePath(clusterPath, adjustedOffset - (cluster.NaturalOffset * scaleX), 0f);
            }
            else if (cluster.NaturalPath is not null)
            {
                TranslatePath(clusterPath, adjustedOffset - cluster.NaturalOffset, 0f);
            }
            else if (isRightToLeft)
            {
                TranslatePath(clusterPath, adjustedOffset - clusterPath.Bounds.Left, 0f);
            }

            if (!TryWarpTextOutlinePath(clusterPath, pathSamples, pathLength, isClosedLoop, startOffset, baseVOffset, out var warpedClusterPath))
            {
                return false;
            }

            if (!appendedPath)
            {
                stretchedPath.FillType = warpedClusterPath.FillType;
            }

            AppendPathCommands(stretchedPath, warpedClusterPath);
            appendedPath = true;
        }

        if (!appendedPath || stretchedPath.IsEmpty || !HasMeaningfulPathExtent(stretchedPath))
        {
            return false;
        }

        if (!isRightToLeft)
        {
            return true;
        }

        var minimumRtlSpacingExtent = Math.Max(TextLengthTolerance, Math.Min(25f, adjustedAdvance * 0.25f));
        return Math.Abs(stretchedPath.Bounds.Width) > minimumRtlSpacingExtent;
    }

    private static float ResolveStretchClusterSpacing(
        SvgTextBase svgTextBase,
        StretchedTextPathCluster cluster,
        SKRect geometryBounds)
    {
        var spacing = 0f;
        if (SupportsLetterSpacing(cluster.Text))
        {
            spacing += ResolveSpacingValue(svgTextBase, svgTextBase.LetterSpacing, geometryBounds, cluster.NaturalAdvance);
        }

        if (IsWhitespaceCodepoint(cluster.Text))
        {
            spacing += ResolveSpacingValue(svgTextBase, svgTextBase.WordSpacing, geometryBounds, cluster.NaturalAdvance);
        }

        return spacing;
    }

    private static bool HasMeaningfulPathExtent(SKPath path)
    {
        var bounds = path.Bounds;
        return Math.Max(Math.Abs(bounds.Width), Math.Abs(bounds.Height)) > TextLengthTolerance;
    }

    private static string RemoveBidiFormattingControls(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        StringBuilder? builder = null;
        for (var i = 0; i < text.Length; i++)
        {
            if (!IsBidiFormattingControl(text[i]))
            {
                builder?.Append(text[i]);
                continue;
            }

            if (builder is null)
            {
                builder = new StringBuilder(text.Length);
                if (i > 0)
                {
                    builder.Append(text, 0, i);
                }
            }
        }

        return builder?.ToString() ?? text;
    }

    private static bool IsBidiFormattingControl(char ch)
    {
        return ch is '\u061C' or '\u200E' or '\u200F' or
            >= '\u202A' and <= '\u202E' or
            >= '\u2066' and <= '\u2069';
    }

    private static bool TryCreateStretchedTextPathClusters(
        SvgTextBase svgTextBase,
        string text,
        SKPaint paint,
        bool isRightToLeft,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out List<StretchedTextPathCluster> clusters)
    {
        return TryCreateShapedStretchedTextPathClusters(svgTextBase, text, paint, isRightToLeft, assetLoader, out clusters) ||
               TryCreateFallbackStretchedTextPathClusters(svgTextBase, text, geometryBounds, assetLoader, out clusters);
    }

    private static bool TryCreateShapedStretchedTextPathClusters(
        SvgTextBase svgTextBase,
        string text,
        SKPaint paint,
        bool isRightToLeft,
        ISvgAssetLoader assetLoader,
        out List<StretchedTextPathCluster> clusters)
    {
        clusters = new List<StretchedTextPathCluster>();
        if (string.IsNullOrEmpty(text) ||
            !TryShapeStretchTextRun(svgTextBase, text, paint, isRightToLeft, assetLoader, out var shapedRun) ||
            shapedRun.Glyphs.Length == 0 ||
            shapedRun.Points.Length != shapedRun.Glyphs.Length ||
            shapedRun.Clusters.Length != shapedRun.Glyphs.Length)
        {
            return false;
        }

        if (!SvgTextPathLayoutPlanner.TryCreateStretchTextClusterRanges(text, shapedRun.Clusters, out var clusterRanges))
        {
            return false;
        }

        var pathResolver = assetLoader as ISvgTextGlyphRunPathResolver;
        var glyphIndex = 0;
        var emittedClusters = new HashSet<int>();
        while (glyphIndex < shapedRun.Glyphs.Length)
        {
            var clusterCharIndex = shapedRun.Clusters[glyphIndex];
            if (clusterCharIndex < 0 ||
                clusterCharIndex >= text.Length ||
                !TryGetStretchTextClusterRange(clusterRanges, clusterCharIndex, out var clusterRange) ||
                !emittedClusters.Add(clusterRange.Start) ||
                !TryGetStretchClusterText(text, clusterRange, out var clusterText))
            {
                clusters.Clear();
                return false;
            }

            var clusterGlyphStart = glyphIndex;
            var clusterPointX = shapedRun.Points[glyphIndex].X;
            glyphIndex++;
            while (glyphIndex < shapedRun.Glyphs.Length &&
                   TryGetStretchTextClusterRange(clusterRanges, shapedRun.Clusters[glyphIndex], out var nextClusterRange) &&
                   nextClusterRange.Start == clusterRange.Start)
            {
                glyphIndex++;
            }

            var clusterGlyphCount = glyphIndex - clusterGlyphStart;
            var nextPointX = glyphIndex < shapedRun.Points.Length
                ? shapedRun.Points[glyphIndex].X
                : shapedRun.Advance;
            var clusterAdvance = Math.Max(0f, Math.Abs(nextPointX - clusterPointX));
            SKPath? clusterPath = null;
            if (pathResolver is not null &&
                TryCreateShapedClusterGlyphRunPath(pathResolver, shapedRun, clusterGlyphStart, clusterGlyphCount, paint, out var shapedClusterPath))
            {
                clusterPath = shapedClusterPath;
            }

            clusters.Add(new StretchedTextPathCluster(
                clusterText,
                clusterPointX,
                clusterAdvance,
                clusterPath));
        }

        return clusters.Count > 0;
    }

    private static bool TryCreateShapedClusterGlyphRunPath(
        ISvgTextGlyphRunPathResolver pathResolver,
        ShapedGlyphRun shapedRun,
        int glyphStart,
        int glyphCount,
        SKPaint paint,
        out SKPath clusterPath)
    {
        clusterPath = new SKPath();
        if (glyphStart < 0 ||
            glyphCount <= 0 ||
            glyphStart + glyphCount > shapedRun.Glyphs.Length ||
            shapedRun.Points.Length != shapedRun.Glyphs.Length ||
            shapedRun.Clusters.Length != shapedRun.Glyphs.Length)
        {
            return false;
        }

        var glyphs = new ushort[glyphCount];
        var points = new SKPoint[glyphCount];
        var clusters = new int[glyphCount];
        Array.Copy(shapedRun.Glyphs, glyphStart, glyphs, 0, glyphCount);
        Array.Copy(shapedRun.Points, glyphStart, points, 0, glyphCount);
        Array.Copy(shapedRun.Clusters, glyphStart, clusters, 0, glyphCount);

        return TryCreateGlyphRunPath(
            pathResolver,
            new ShapedGlyphRun(glyphs, points, clusters, shapedRun.Advance),
            paint,
            out clusterPath);
    }

    private static bool TryGetStretchTextClusterRange(
        IReadOnlyList<SvgTextPathLayoutPlanner.StretchTextClusterRange> ranges,
        int charIndex,
        out SvgTextPathLayoutPlanner.StretchTextClusterRange range)
    {
        for (var i = 0; i < ranges.Count; i++)
        {
            var candidate = ranges[i];
            if (charIndex >= candidate.Start && charIndex < candidate.End)
            {
                range = candidate;
                return true;
            }

            if (charIndex < candidate.Start)
            {
                break;
            }
        }

        range = default;
        return false;
    }

    private static bool TryGetStretchClusterText(
        string text,
        SvgTextPathLayoutPlanner.StretchTextClusterRange range,
        out string clusterText)
    {
        clusterText = string.Empty;
        if (range.Start < 0 ||
            range.End <= range.Start ||
            range.End > text.Length)
        {
            return false;
        }

        clusterText = text.Substring(range.Start, range.End - range.Start);
        return clusterText.Length > 0;
    }

    private static bool TryGetSortedClusterStarts(string text, IReadOnlyList<int> shapedClusters, out int[] clusterStarts)
    {
        clusterStarts = Array.Empty<int>();
        if (string.IsNullOrEmpty(text) || shapedClusters.Count == 0)
        {
            return false;
        }

        var starts = new SortedSet<int>();
        for (var i = 0; i < shapedClusters.Count; i++)
        {
            var clusterStart = shapedClusters[i];
            if (clusterStart < 0 || clusterStart >= text.Length)
            {
                return false;
            }

            starts.Add(clusterStart);
        }

        clusterStarts = starts.ToArray();
        return clusterStarts.Length > 0;
    }

    private static bool TryGetClusterText(string text, IReadOnlyList<int> clusterStarts, int clusterStart, out string clusterText)
    {
        clusterText = string.Empty;
        var startIndex = 0;
        while (startIndex < clusterStarts.Count && clusterStarts[startIndex] < clusterStart)
        {
            startIndex++;
        }

        if (startIndex >= clusterStarts.Count || clusterStarts[startIndex] != clusterStart)
        {
            return false;
        }

        var clusterEnd = startIndex + 1 < clusterStarts.Count
            ? clusterStarts[startIndex + 1]
            : text.Length;
        if (clusterEnd <= clusterStart || clusterEnd > text.Length)
        {
            return false;
        }

        clusterText = text.Substring(clusterStart, clusterEnd - clusterStart);
        return clusterText.Length > 0;
    }

    private static bool TryShapeStretchTextRun(
        SvgTextBase svgTextBase,
        string text,
        SKPaint paint,
        bool isRightToLeft,
        ISvgAssetLoader assetLoader,
        out ShapedGlyphRun shapedRun)
    {
        if (assetLoader is ISvgTextDirectedGlyphRunResolver directedGlyphRunResolver)
        {
            return directedGlyphRunResolver.TryShapeGlyphRun(text, paint, isRightToLeft, out shapedRun);
        }

        if (assetLoader is ISvgTextGlyphRunResolver glyphRunResolver)
        {
            return glyphRunResolver.TryShapeGlyphRun(text, paint, out shapedRun);
        }

        shapedRun = default;
        return false;
    }

    private static bool TryCreateFallbackStretchedTextPathClusters(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out List<StretchedTextPathCluster> clusters)
    {
        clusters = new List<StretchedTextPathCluster>();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (!SvgTextPathLayoutPlanner.TryCreateFallbackStretchTextClusterRanges(text, out var textElementRanges) ||
            textElementRanges.Length == 0)
        {
            return false;
        }

        var prefixBuilder = new StringBuilder();
        var previousPrefixAdvance = 0f;
        for (var i = 0; i < textElementRanges.Length; i++)
        {
            if (!TryGetStretchClusterText(text, textElementRanges[i], out var textElement))
            {
                clusters.Clear();
                return false;
            }

            prefixBuilder.Append(textElement);
            var prefixAdvance = MeasureNaturalTextAdvance(svgTextBase, prefixBuilder.ToString(), geometryBounds, assetLoader);
            clusters.Add(new StretchedTextPathCluster(
                textElement,
                previousPrefixAdvance,
                Math.Max(0f, prefixAdvance - previousPrefixAdvance)));
            previousPrefixAdvance = prefixAdvance;
        }

        return clusters.Count > 0;
    }

    private static IReadOnlyList<StretchedTextPathDecorationRun> CreateStretchedTextPathDecorationRuns(
        SvgTextBase svgTextBase,
        float totalAdvance,
        IReadOnlyList<PathSample> pathSamples,
        float pathLength,
        bool isClosedLoop,
        float startOffset,
        float baseVOffset,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var decorationLayers = ResolveTextDecorationLayers(svgTextBase);
        if (decorationLayers.Count == 0 || totalAdvance <= 0f)
        {
            return Array.Empty<StretchedTextPathDecorationRun>();
        }

        var decorations = new List<StretchedTextPathDecorationRun>();
        for (var i = 0; i < decorationLayers.Count; i++)
        {
            var layer = decorationLayers[i];
            var metricsPaint = CreateTextMetricsPaint(layer.MetricsSource, geometryBounds);
            var metrics = assetLoader.GetFontMetrics(metricsPaint);

            AppendStretchedTextPathDecorationRun(layer, totalAdvance, metricsPaint, metrics, SvgTextDecoration.Overline, pathSamples, pathLength, isClosedLoop, startOffset, baseVOffset, decorations);
            AppendStretchedTextPathDecorationRun(layer, totalAdvance, metricsPaint, metrics, SvgTextDecoration.LineThrough, pathSamples, pathLength, isClosedLoop, startOffset, baseVOffset, decorations);
            AppendStretchedTextPathDecorationRun(layer, totalAdvance, metricsPaint, metrics, SvgTextDecoration.Underline, pathSamples, pathLength, isClosedLoop, startOffset, baseVOffset, decorations);
        }

        return decorations.Count > 0
            ? decorations
            : Array.Empty<StretchedTextPathDecorationRun>();
    }

    private static void AppendStretchedTextPathDecorationRun(
        TextDecorationLayer layer,
        float totalAdvance,
        SKPaint metricsPaint,
        SKFontMetrics metrics,
        SvgTextDecoration decorationKind,
        IReadOnlyList<PathSample> pathSamples,
        float pathLength,
        bool isClosedLoop,
        float startOffset,
        float baseVOffset,
        List<StretchedTextPathDecorationRun> decorations)
    {
        if (!layer.Decorations.HasFlag(decorationKind) ||
            !TryCreateLinearDecorationPath(0f, 0f, totalAdvance, metricsPaint, metrics, decorationKind, out var decorationPath) ||
            !TryWarpTextOutlinePath(decorationPath, pathSamples, pathLength, isClosedLoop, startOffset, baseVOffset, out var stretchedDecorationPath))
        {
            return;
        }

        decorations.Add(new StretchedTextPathDecorationRun(layer, stretchedDecorationPath));
    }

    private static bool TryWarpTextOutlinePath(
        SKPath glyphPath,
        IReadOnlyList<PathSample> pathSamples,
        float pathLength,
        bool isClosedLoop,
        float startOffset,
        float baseVOffset,
        out SKPath stretchedPath)
    {
        var plannerSamples = GetTextPathPlannerSamples(pathSamples);
        var options = new SvgTextPathLayoutPlanner.MappingOptions(pathLength, isClosedLoop, startOffset, baseVOffset);
        return SvgTextPathLayoutPlanner.TryWarpTextOutlinePathOrFallback(glyphPath, glyphPath.Bounds, plannerSamples, options, out stretchedPath);
    }

    private static bool TryCreateTextPathCodepointPlacements(
        SvgTextBase svgTextBase,
        SvgTextBase lengthSource,
        string text,
        float startOffset,
        float baseVOffset,
        IReadOnlyList<PathSample> pathSamples,
        bool isClosedLoop,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out string renderedText,
        out PositionedCodepointPlacement[] placements,
        out float totalAdvance,
        out SKRect fastBounds,
        float? specifiedLengthOverride = null)
    {
        renderedText = string.Empty;
        placements = Array.Empty<PositionedCodepointPlacement>();
        totalAdvance = 0f;
        fastBounds = SKRect.Empty;

        if (string.IsNullOrEmpty(text) || pathSamples.Count < 2)
        {
            return false;
        }

        var codepoints = SplitCodepointsReadOnly(text);
        if (codepoints.Count == 0)
        {
            return false;
        }

        var pathLength = pathSamples[pathSamples.Count - 1].Distance;
        var isSimpleAsciiText = IsSimpleAsciiSequentialCompileText(text);
        var naturalAdvances = MeasureNaturalCodepointAdvances(svgTextBase, text, codepoints, geometryBounds, assetLoader);
        var placementAdvances = CreateTextPathPlacementAdvances(text, codepoints, naturalAdvances);
        var letterSpacingUnit = svgTextBase.LetterSpacing;
        var wordSpacingUnit = svgTextBase.WordSpacing;
        var hasLetterSpacingAdjustment = HasSpacingAdjustment(letterSpacingUnit) &&
                                         (isSimpleAsciiText || !SuppressesLetterSpacingForRun(text));
        var hasWordSpacingAdjustment = HasSpacingAdjustment(wordSpacingUnit);
        var letterSpacingIsPercentage = hasLetterSpacingAdjustment && letterSpacingUnit.Type == SvgUnitType.Percentage;
        var wordSpacingIsPercentage = hasWordSpacingAdjustment && wordSpacingUnit.Type == SvgUnitType.Percentage;
        var fixedLetterSpacing = hasLetterSpacingAdjustment && !letterSpacingIsPercentage
            ? letterSpacingUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgTextBase, geometryBounds)
            : 0f;
        var fixedWordSpacing = hasWordSpacingAdjustment && !wordSpacingIsPercentage
            ? wordSpacingUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgTextBase, geometryBounds)
            : 0f;
        var naturalLength = 0f;
        for (var i = 0; i < codepoints.Count; i++)
        {
            naturalLength += naturalAdvances[i];
            if (i >= codepoints.Count - 1)
            {
                continue;
            }

            if (hasLetterSpacingAdjustment && SupportsTextPathLetterSpacing(isSimpleAsciiText, text, codepoints, i))
            {
                naturalLength += letterSpacingIsPercentage
                    ? ResolveSpacingValue(svgTextBase, letterSpacingUnit, geometryBounds, naturalAdvances[i])
                    : fixedLetterSpacing;
            }

            if (hasWordSpacingAdjustment && IsTextPathWhitespace(isSimpleAsciiText, text, codepoints, i))
            {
                naturalLength += wordSpacingIsPercentage
                    ? ResolveSpacingValue(svgTextBase, wordSpacingUnit, geometryBounds, naturalAdvances[i])
                    : fixedWordSpacing;
            }
        }

        totalAdvance = naturalLength;
        var glyphScaleX = 1f;
        var extraGapAdvance = 0f;
        var scaleRunFromStart = false;
        var specifiedLength = specifiedLengthOverride ??
                              (TryGetOwnTextLength(lengthSource, viewport, IsVerticalWritingMode(svgTextBase), out var ownSpecifiedLength)
                                  ? ownSpecifiedLength
                                  : 0f);
        var hasActiveTextLengthAdjustment = specifiedLength > 0f &&
                                            Math.Abs(naturalLength - specifiedLength) > TextLengthTolerance;
        if (hasActiveTextLengthAdjustment)
        {
            if (GetOwnLengthAdjust(lengthSource) == SvgTextLengthAdjust.Spacing && codepoints.Count > 1)
            {
                extraGapAdvance = (specifiedLength - totalAdvance) / (codepoints.Count - 1);
                totalAdvance = specifiedLength;
            }
            else if (totalAdvance > 0f)
            {
                glyphScaleX = specifiedLength / totalAdvance;
                scaleRunFromStart = true;
                totalAdvance = specifiedLength;
            }
        }

        var rotations = GetPositionedRotations(svgTextBase, codepoints.Count);
        var currentVOffset = baseVOffset + GetBaselineOffset(svgTextBase, viewport, assetLoader);
        var plannerSamples = GetTextPathPlannerSamples(pathSamples);
        var placementBuffer = new PositionedCodepointPlacement[codepoints.Count];
        var visibleStartIndex = -1;
        var visibleCount = 0;
        var placementOffset = startOffset;
        var closedLoopEndOffset = isClosedLoop && pathLength > 0f
            ? startOffset + pathLength
            : float.PositiveInfinity;
        var pathSegmentIndex = 1;
        var previousSampleOffset = float.NegativeInfinity;
        var canCreateFastBounds = TryCreateSimpleAsciiPositionedBoundsPaint(
            svgTextBase,
            text,
            geometryBounds,
            assetLoader,
            out _,
            out var fastBoundsMetrics,
            out var fastBoundsPadding);

        for (var i = 0; i < codepoints.Count; i++)
        {
            var glyphAdvance = scaleRunFromStart
                ? placementAdvances[i] * glyphScaleX
                : placementAdvances[i];
            if (!IsValidPositiveAdvance(glyphAdvance))
            {
                glyphAdvance = 0f;
            }

            var letterSpacing = 0f;
            var wordSpacing = 0f;
            if (i < codepoints.Count - 1)
            {
                if (hasLetterSpacingAdjustment && SupportsTextPathLetterSpacing(isSimpleAsciiText, text, codepoints, i))
                {
                    letterSpacing = letterSpacingIsPercentage
                        ? ResolveSpacingValue(svgTextBase, letterSpacingUnit, geometryBounds, naturalAdvances[i])
                        : fixedLetterSpacing;
                }

                if (hasWordSpacingAdjustment && IsTextPathWhitespace(isSimpleAsciiText, text, codepoints, i))
                {
                    wordSpacing = wordSpacingIsPercentage
                        ? ResolveSpacingValue(svgTextBase, wordSpacingUnit, geometryBounds, naturalAdvances[i])
                        : fixedWordSpacing;
                }
            }

            var clusterAdvance = glyphAdvance + letterSpacing + wordSpacing;
            if (!scaleRunFromStart)
            {
                clusterAdvance += extraGapAdvance;
            }

            var glyphMidOffset = placementOffset + (glyphAdvance * 0.5f);
            if (glyphMidOffset >= closedLoopEndOffset)
            {
                break;
            }

            var sampleOffset = glyphMidOffset;
            if (isClosedLoop && pathLength > 0f)
            {
                sampleOffset = NormalizeClosedPathDistance(glyphMidOffset, pathLength);
            }
            else if (glyphMidOffset <= 0f)
            {
                placementOffset += clusterAdvance;
                continue;
            }

            if (!isClosedLoop && glyphMidOffset >= pathLength)
            {
                break;
            }

            if (sampleOffset < previousSampleOffset)
            {
                pathSegmentIndex = 1;
            }

            previousSampleOffset = sampleOffset;

            if (!SvgTextPathLayoutPlanner.TryGetPointAndTangent(plannerSamples, sampleOffset, ref pathSegmentIndex, out var rawPoint, out var tangent))
            {
                return false;
            }

            var codepointRotationDegrees = rotations is null
                ? 0f
                : GetCodepointRotationDegrees(svgTextBase, codepoints[i], rotations, i);
            var angleDegrees = (float)(Math.Atan2(tangent.Y, tangent.X) * 180d / Math.PI);
            var finalAngleDegrees = angleDegrees + codepointRotationDegrees;
            var baselineDirection = RotateTextPathTangent(tangent, codepointRotationDegrees);
            var baselineNormal = new SKPoint(-baselineDirection.Y, baselineDirection.X);
            var point = new SKPoint(
                rawPoint.X + (baselineNormal.X * currentVOffset) - (baselineDirection.X * glyphAdvance * 0.5f),
                rawPoint.Y + (baselineNormal.Y * currentVOffset) - (baselineDirection.Y * glyphAdvance * 0.5f));

            if (visibleStartIndex < 0)
            {
                visibleStartIndex = i;
            }

            var placement = new PositionedCodepointPlacement(
                point,
                finalAngleDegrees,
                scaleRunFromStart ? glyphScaleX : 1f,
                point.X,
                placementOffset);
            placementBuffer[visibleCount++] = placement;

            if (canCreateFastBounds)
            {
                var unscaledAdvance = IsValidPositiveAdvance(placementAdvances[i])
                    ? placementAdvances[i]
                    : 0f;
                var candidateBounds = CreateFastPositionedTextBounds(
                    placement,
                    unscaledAdvance,
                    fastBoundsMetrics,
                    fastBoundsPadding);
                UnionBounds(ref fastBounds, candidateBounds);
            }

            if (i < codepoints.Count - 1)
            {
                placementOffset += clusterAdvance;
            }
        }

        if (visibleCount == 0)
        {
            return false;
        }

        if (visibleCount == placementBuffer.Length)
        {
            placements = placementBuffer;
            renderedText = text;
        }
        else
        {
            placements = new PositionedCodepointPlacement[visibleCount];
            Array.Copy(placementBuffer, placements, visibleCount);
            renderedText = CreateCodepointRangeText(codepoints, visibleStartIndex, visibleCount);
        }

        return true;
    }

    private static SKPoint RotateTextPathTangent(SKPoint tangent, float rotationDegrees)
    {
        if (Math.Abs(rotationDegrees) <= 0.001f)
        {
            return tangent;
        }

        var rotationRadians = rotationDegrees * ((float)Math.PI / 180f);
        var cos = (float)Math.Cos(rotationRadians);
        var sin = (float)Math.Sin(rotationRadians);
        return new SKPoint(
            (tangent.X * cos) - (tangent.Y * sin),
            (tangent.X * sin) + (tangent.Y * cos));
    }

    private static string CreateCodepointRangeText(IReadOnlyList<string> codepoints, int startIndex, int count)
    {
        if (count <= 0 || startIndex < 0)
        {
            return string.Empty;
        }

        if (count == 1)
        {
            return codepoints[startIndex];
        }

        var length = 0;
        for (var i = 0; i < count; i++)
        {
            length += codepoints[startIndex + i].Length;
        }

        var builder = new StringBuilder(length);
        for (var i = 0; i < count; i++)
        {
            builder.Append(codepoints[startIndex + i]);
        }

        return builder.ToString();
    }

    private static IReadOnlyList<float> CreateTextPathPlacementAdvances(
        string text,
        IReadOnlyList<string> codepoints,
        IReadOnlyList<float> naturalAdvances)
    {
        if (naturalAdvances.Count >= codepoints.Count &&
            (codepoints.Count <= 1 || IsSimpleAsciiSequentialCompileText(text) || !MayNeedClusteredNaturalCodepointAdvances(codepoints)))
        {
            return naturalAdvances;
        }

        var advances = new float[codepoints.Count];
        for (var i = 0; i < codepoints.Count && i < naturalAdvances.Count; i++)
        {
            advances[i] = naturalAdvances[i];
        }

        if (string.IsNullOrEmpty(text) || codepoints.Count <= 1)
        {
            return advances;
        }

        var clusterStarts = SvgTextBoundaryResolver.Default.GetGraphemeClusterStartCharIndexes(text);
        if (clusterStarts.Count == 0)
        {
            return advances;
        }

        if (clusterStarts.Count >= codepoints.Count)
        {
            return advances;
        }

        var codepointCharOffsets = CreateCodepointCharOffsets(codepoints);
        for (var clusterIndex = 0; clusterIndex < clusterStarts.Count; clusterIndex++)
        {
            var clusterStart = clusterStarts[clusterIndex];
            var clusterEnd = clusterIndex + 1 < clusterStarts.Count
                ? clusterStarts[clusterIndex + 1]
                : text.Length;
            var firstCodepointIndex = GetCodepointIndexFromCharOffset(codepointCharOffsets, clusterStart);
            var endCodepointIndex = GetTextPathCodepointBoundaryIndex(codepointCharOffsets, clusterEnd);
            if (firstCodepointIndex < 0 || endCodepointIndex < 0)
            {
                continue;
            }

            var codepointCount = endCodepointIndex - firstCodepointIndex;
            if (codepointCount <= 1)
            {
                continue;
            }

            var clusterAdvance = 0f;
            for (var i = firstCodepointIndex; i < endCodepointIndex && i < naturalAdvances.Count; i++)
            {
                clusterAdvance += naturalAdvances[i];
            }

            if (!IsValidPositiveAdvance(clusterAdvance))
            {
                continue;
            }

            var codepointAdvance = clusterAdvance / codepointCount;
            for (var i = firstCodepointIndex; i < endCodepointIndex; i++)
            {
                advances[i] = codepointAdvance;
            }
        }

        return advances;
    }

    private static int GetTextPathCodepointBoundaryIndex(IReadOnlyList<int> codepointCharOffsets, int charOffset)
    {
        for (var i = 0; i < codepointCharOffsets.Count; i++)
        {
            if (codepointCharOffsets[i] == charOffset)
            {
                return i;
            }
        }

        return -1;
    }

    private static float GetTextPathCursorDistance(SvgTextPath svgTextPath, float pathLength, float textEndOffset)
        => IsVerticalWritingMode(svgTextPath) ? textEndOffset : pathLength;

    private static void AdvanceTextPathPosition(
        IReadOnlyList<PathSample> pathSamples,
        float distance,
        float endVOffset,
        bool isClosedLoop,
        ref float currentX,
        ref float currentY)
    {
        if (pathSamples.Count > 1 && isClosedLoop)
        {
            var pathLength = pathSamples[pathSamples.Count - 1].Distance;
            distance = NormalizeClosedPathDistance(distance, pathLength);
        }

        if (!TryGetTextPathCurrentPosition(pathSamples, distance, endVOffset, out var endPoint))
        {
            return;
        }

        currentX = endPoint.X;
        currentY = endPoint.Y;
    }

    private static bool TryGetTextPathCurrentPosition(
        IReadOnlyList<PathSample> pathSamples,
        float distance,
        float vOffset,
        out SKPoint point)
    {
        var plannerSamples = GetTextPathPlannerSamples(pathSamples);
        return SvgTextPathLayoutPlanner.TryGetCurrentPosition(plannerSamples, distance, vOffset, isClosedLoop: false, out point, out _);
    }

    private static bool TryProjectPointOntoTextPath(
        IReadOnlyList<PathSample> pathSamples,
        SKPoint targetPoint,
        out float distance,
        out float vOffset)
    {
        var plannerSamples = GetTextPathPlannerSamples(pathSamples);
        if (SvgTextPathLayoutPlanner.TryProjectPointOntoPath(plannerSamples, targetPoint, out distance, out vOffset))
        {
            return true;
        }

        distance = pathSamples.Count > 0 ? pathSamples[0].Distance : 0f;
        vOffset = pathSamples.Count > 0 ? Distance(pathSamples[0].Point, targetPoint) : 0f;
        return pathSamples.Count > 0;
    }

    private static bool TryGetTextPathPoint(
        float distance,
        float vOffset,
        IReadOnlyList<PathSample> pathSamples,
        out SKPoint point,
        out SKPoint tangent)
    {
        point = default;
        tangent = new SKPoint(1f, 0f);
        if (!TryGetPathPointAndTangent(pathSamples, distance, out var rawPoint, out tangent))
        {
            return false;
        }

        var normal = Normalize(new SKPoint(-tangent.Y, tangent.X));
        point = new SKPoint(rawPoint.X + (normal.X * vOffset), rawPoint.Y + (normal.Y * vOffset));
        return true;
    }

    private static bool TryGetTextPathPoint(
        float distance,
        float vOffset,
        IReadOnlyList<PathSample> pathSamples,
        out SKPoint point)
    {
        return TryGetTextPathPoint(distance, vOffset, pathSamples, out point, out _);
    }

    private static float NormalizeClosedPathDistance(float distance, float pathLength)
    {
        if (pathLength <= 0f)
        {
            return distance;
        }

        var normalized = distance % pathLength;
        return normalized < 0f
            ? normalized + pathLength
            : normalized;
    }

    private static void ResolveTextPathChunkOffsets(
        SvgTextPath svgTextPath,
        bool useCurrentPositionOffset,
        float currentX,
        float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        IReadOnlyList<PathSample> pathSamples,
        out float horizontalOffset,
        out float verticalOffset)
    {
        horizontalOffset = 0f;
        verticalOffset = 0f;

        if (svgTextPath.Parent is SvgTextBase parentTextBase &&
            !IsVerticalWritingMode(svgTextPath) &&
            parentTextBase.Dy.Count > 0)
        {
            verticalOffset = ResolveTextUnitValue(parentTextBase.Dy[0], UnitRenderingType.VerticalOffset, parentTextBase, viewport, assetLoader);
        }

        if (useCurrentPositionOffset)
        {
            horizontalOffset = currentX;
            return;
        }
    }

    private static int EstimatePathSampleCapacity(SKPath path, float samplingScale)
    {
        if (path.Commands is null || path.Commands.Count == 0)
        {
            return 0;
        }

        samplingScale = NormalizeTextPathSamplingScale(samplingScale);
        var capacity = 0;
        var current = default(SKPoint);
        var figureStart = default(SKPoint);
        var hasCurrent = false;

        void AddCapacity(int count)
        {
            if (count <= 0)
            {
                return;
            }

            capacity = capacity > int.MaxValue - count
                ? int.MaxValue
                : capacity + count;
        }

        void CloseCurrentSample()
        {
            current = figureStart;
            AddCapacity(1);
        }

        void AppendEllipseSamples(float cx, float cy, float rx, float ry)
        {
            if (rx <= 0f || ry <= 0f)
            {
                return;
            }

            var steps = ResolveTextPathFixedSteps(MaxEllipseSteps, samplingScale, MaxEllipseSteps, MaxTextPathCurveSteps);
            AddCapacity(steps + 1);
            current = new SKPoint(cx + rx, cy);
            figureStart = current;
            hasCurrent = true;
        }

        void AppendRoundRectSamples(SKRect rect, float rx, float ry)
        {
            if (rect.Width <= 0f || rect.Height <= 0f)
            {
                return;
            }

            rx = Math.Min(Math.Abs(rx), rect.Width / 2f);
            ry = Math.Min(Math.Abs(ry), rect.Height / 2f);
            if (rx <= 0f || ry <= 0f)
            {
                AddCapacity(5);
                current = rect.TopLeft;
                figureStart = current;
                hasCurrent = true;
                return;
            }

            var arcSteps = ResolveTextPathFixedSteps(32, samplingScale, 32, MaxTextPathCurveSteps);
            AddCapacity(6 + (4 * arcSteps));
            current = new SKPoint(rect.Left + rx, rect.Top);
            figureStart = current;
            hasCurrent = true;
        }

        for (var i = 0; i < path.Commands.Count; i++)
        {
            switch (path.Commands[i])
            {
                case MoveToPathCommand moveTo:
                    current = new SKPoint(moveTo.X, moveTo.Y);
                    figureStart = current;
                    hasCurrent = true;
                    AddCapacity(1);
                    break;

                case LineToPathCommand lineTo when hasCurrent:
                    current = new SKPoint(lineTo.X, lineTo.Y);
                    AddCapacity(1);
                    break;

                case QuadToPathCommand quadTo when hasCurrent:
                    {
                        var control = new SKPoint(quadTo.X0, quadTo.Y0);
                        var end = new SKPoint(quadTo.X1, quadTo.Y1);
                        AddCapacity(ResolveTextPathCurveSteps(ApproximateQuadraticLength(current, control, end), samplingScale, 96));
                        current = end;
                    }
                    break;

                case CubicToPathCommand cubicTo when hasCurrent:
                    {
                        var control1 = new SKPoint(cubicTo.X0, cubicTo.Y0);
                        var control2 = new SKPoint(cubicTo.X1, cubicTo.Y1);
                        var end = new SKPoint(cubicTo.X2, cubicTo.Y2);
                        AddCapacity(ResolveTextPathCurveSteps(ApproximateCubicLength(current, control1, control2, end), samplingScale, 128));
                        current = end;
                    }
                    break;

                case ArcToPathCommand arcTo when hasCurrent:
                    {
                        var end = new SKPoint(arcTo.X, arcTo.Y);
                        AddCapacity(TryGetArcParameters(current, end, arcTo.Rx, arcTo.Ry, arcTo.XAxisRotate, arcTo.LargeArc, arcTo.Sweep, out var parameters)
                            ? ResolveArcSampleSteps(parameters, samplingScale)
                            : 1);
                        current = end;
                    }
                    break;

                case ClosePathCommand _ when hasCurrent:
                    CloseCurrentSample();
                    break;

                case AddRectPathCommand addRect:
                    if (addRect.Rect.Width > 0f && addRect.Rect.Height > 0f)
                    {
                        AddCapacity(5);
                        current = addRect.Rect.TopLeft;
                        figureStart = current;
                        hasCurrent = true;
                    }

                    break;

                case AddRoundRectPathCommand addRoundRect:
                    AppendRoundRectSamples(addRoundRect.Rect, addRoundRect.Rx, addRoundRect.Ry);
                    break;

                case AddOvalPathCommand addOval:
                    AppendEllipseSamples(
                        (addOval.Rect.Left + addOval.Rect.Right) / 2f,
                        (addOval.Rect.Top + addOval.Rect.Bottom) / 2f,
                        addOval.Rect.Width / 2f,
                        addOval.Rect.Height / 2f);
                    break;

                case AddCirclePathCommand addCircle:
                    AppendEllipseSamples(addCircle.X, addCircle.Y, Math.Abs(addCircle.Radius), Math.Abs(addCircle.Radius));
                    break;

                case AddPolyPathCommand addPoly when addPoly.Points is { Count: > 0 } points:
                    AddCapacity(points.Count + (addPoly.Close ? 1 : 0));
                    current = addPoly.Close ? points[0] : points[points.Count - 1];
                    figureStart = points[0];
                    hasCurrent = true;
                    break;
            }
        }

        return capacity;
    }

    private static List<PathSample> BuildPathSamples(SKPath path, float samplingScale = 1f)
    {
        var samples = new List<PathSample>(EstimatePathSampleCapacity(path, samplingScale));
        if (path.Commands is null || path.Commands.Count == 0)
        {
            return samples;
        }

        samplingScale = NormalizeTextPathSamplingScale(samplingScale);
        var current = default(SKPoint);
        var figureStart = default(SKPoint);
        var hasCurrent = false;
        var totalDistance = 0f;

        void AppendSample(SKPoint next)
        {
            if (!hasCurrent)
            {
                current = next;
                figureStart = next;
                samples.Add(new PathSample(next, totalDistance, false, false));
                hasCurrent = true;
                return;
            }

            totalDistance += Distance(current, next);
            current = next;
            samples.Add(new PathSample(next, totalDistance, false, false));
        }

        void MoveToSample(SKPoint next)
        {
            current = next;
            figureStart = next;
            samples.Add(new PathSample(current, totalDistance, samples.Count > 0, false));
            hasCurrent = true;
        }

        void CloseCurrentSample()
        {
            totalDistance += Distance(current, figureStart);
            current = figureStart;
            samples.Add(new PathSample(figureStart, totalDistance, false, true));
        }

        void AppendEllipseSamples(float cx, float cy, float rx, float ry)
        {
            if (rx <= 0f || ry <= 0f)
            {
                return;
            }

            var steps = ResolveTextPathFixedSteps(MaxEllipseSteps, samplingScale, MaxEllipseSteps, MaxTextPathCurveSteps);
            MoveToSample(new SKPoint(cx + rx, cy));
            for (var step = 1; step <= steps; step++)
            {
                var radians = step * (2f * (float)Math.PI / steps);
                AppendSample(new SKPoint(
                    cx + (rx * (float)Math.Cos(radians)),
                    cy + (ry * (float)Math.Sin(radians))));
            }

            samples[samples.Count - 1] = samples[samples.Count - 1] with
            {
                ClosesSubpath = true
            };
        }

        void AppendRoundRectSamples(SKRect rect, float rx, float ry)
        {
            if (rect.Width <= 0f || rect.Height <= 0f)
            {
                return;
            }

            rx = Math.Min(Math.Abs(rx), rect.Width / 2f);
            ry = Math.Min(Math.Abs(ry), rect.Height / 2f);
            if (rx <= 0f || ry <= 0f)
            {
                MoveToSample(rect.TopLeft);
                AppendSample(rect.TopRight);
                AppendSample(rect.BottomRight);
                AppendSample(rect.BottomLeft);
                CloseCurrentSample();
                return;
            }

            var arcSteps = ResolveTextPathFixedSteps(32, samplingScale, 32, MaxTextPathCurveSteps);

            void AppendCorner(float cx, float cy, float startRadians, float sweepRadians)
            {
                for (var step = 1; step <= arcSteps; step++)
                {
                    var radians = startRadians + (sweepRadians * step / arcSteps);
                    AppendSample(new SKPoint(
                        cx + (rx * (float)Math.Cos(radians)),
                        cy + (ry * (float)Math.Sin(radians))));
                }
            }

            MoveToSample(new SKPoint(rect.Left + rx, rect.Top));
            AppendSample(new SKPoint(rect.Right - rx, rect.Top));
            AppendCorner(rect.Right - rx, rect.Top + ry, -0.5f * (float)Math.PI, 0.5f * (float)Math.PI);
            AppendSample(new SKPoint(rect.Right, rect.Bottom - ry));
            AppendCorner(rect.Right - rx, rect.Bottom - ry, 0f, 0.5f * (float)Math.PI);
            AppendSample(new SKPoint(rect.Left + rx, rect.Bottom));
            AppendCorner(rect.Left + rx, rect.Bottom - ry, 0.5f * (float)Math.PI, 0.5f * (float)Math.PI);
            AppendSample(new SKPoint(rect.Left, rect.Top + ry));
            AppendCorner(rect.Left + rx, rect.Top + ry, (float)Math.PI, 0.5f * (float)Math.PI);
            CloseCurrentSample();
        }

        for (var i = 0; i < path.Commands.Count; i++)
        {
            switch (path.Commands[i])
            {
                case MoveToPathCommand moveTo:
                    current = new SKPoint(moveTo.X, moveTo.Y);
                    figureStart = current;
                    samples.Add(new PathSample(current, totalDistance, true, false));
                    hasCurrent = true;
                    break;

                case LineToPathCommand lineTo when hasCurrent:
                    AppendSample(new SKPoint(lineTo.X, lineTo.Y));
                    break;

                case QuadToPathCommand quadTo when hasCurrent:
                    {
                        var start = current;
                        var control = new SKPoint(quadTo.X0, quadTo.Y0);
                        var end = new SKPoint(quadTo.X1, quadTo.Y1);
                        var steps = ResolveTextPathCurveSteps(ApproximateQuadraticLength(start, control, end), samplingScale, 96);
                        for (var step = 1; step <= steps; step++)
                        {
                            AppendSample(EvaluateQuadratic(start, control, end, step / (float)steps));
                        }
                    }
                    break;

                case CubicToPathCommand cubicTo when hasCurrent:
                    {
                        var start = current;
                        var control1 = new SKPoint(cubicTo.X0, cubicTo.Y0);
                        var control2 = new SKPoint(cubicTo.X1, cubicTo.Y1);
                        var end = new SKPoint(cubicTo.X2, cubicTo.Y2);
                        var steps = ResolveTextPathCurveSteps(ApproximateCubicLength(start, control1, control2, end), samplingScale, 128);
                        for (var step = 1; step <= steps; step++)
                        {
                            AppendSample(EvaluateCubic(start, control1, control2, end, step / (float)steps));
                        }
                    }
                    break;

                case ArcToPathCommand arcTo when hasCurrent:
                    if (!TryAppendArcSamples(current, arcTo, samplingScale, AppendSample))
                    {
                        AppendSample(new SKPoint(arcTo.X, arcTo.Y));
                    }

                    break;

                case ClosePathCommand _ when hasCurrent:
                    CloseCurrentSample();
                    break;

                case AddRectPathCommand addRect:
                    if (addRect.Rect.Width > 0f && addRect.Rect.Height > 0f)
                    {
                        MoveToSample(addRect.Rect.TopLeft);
                        AppendSample(addRect.Rect.TopRight);
                        AppendSample(addRect.Rect.BottomRight);
                        AppendSample(addRect.Rect.BottomLeft);
                        CloseCurrentSample();
                    }

                    break;

                case AddRoundRectPathCommand addRoundRect:
                    AppendRoundRectSamples(addRoundRect.Rect, addRoundRect.Rx, addRoundRect.Ry);
                    break;

                case AddOvalPathCommand addOval:
                    AppendEllipseSamples(
                        (addOval.Rect.Left + addOval.Rect.Right) / 2f,
                        (addOval.Rect.Top + addOval.Rect.Bottom) / 2f,
                        addOval.Rect.Width / 2f,
                        addOval.Rect.Height / 2f);
                    break;

                case AddCirclePathCommand addCircle:
                    AppendEllipseSamples(addCircle.X, addCircle.Y, Math.Abs(addCircle.Radius), Math.Abs(addCircle.Radius));
                    break;

                case AddPolyPathCommand addPoly when addPoly.Points is { Count: > 0 } points:
                    MoveToSample(points[0]);
                    for (var pointIndex = 1; pointIndex < points.Count; pointIndex++)
                    {
                        AppendSample(points[pointIndex]);
                    }

                    if (addPoly.Close)
                    {
                        CloseCurrentSample();
                    }

                    break;
            }
        }

        return samples;
    }

    private static bool TryGetPathPointAndTangent(
        IReadOnlyList<PathSample> pathSamples,
        float distance,
        out SKPoint point,
        out SKPoint tangent)
    {
        point = default;
        tangent = new SKPoint(1f, 0f);
        if (pathSamples.Count == 0)
        {
            return false;
        }

        if (pathSamples.Count == 1)
        {
            point = pathSamples[0].Point;
            return true;
        }

        if (distance <= 0f)
        {
            point = pathSamples[0].Point;
            tangent = GetPathStartTangent(pathSamples);
            return true;
        }

        for (var i = 1; i < pathSamples.Count; i++)
        {
            var previous = pathSamples[i - 1];
            var current = pathSamples[i];
            if (current.StartsSubpath)
            {
                continue;
            }

            if (distance > current.Distance)
            {
                continue;
            }

            var segmentLength = current.Distance - previous.Distance;
            if (segmentLength <= 0f)
            {
                point = current.Point;
                tangent = ResolvePathSegmentTangent(pathSamples, i);
                return true;
            }

            var t = (distance - previous.Distance) / segmentLength;
            var deltaX = current.Point.X - previous.Point.X;
            var deltaY = current.Point.Y - previous.Point.Y;
            point = new SKPoint(
                previous.Point.X + (deltaX * t),
                previous.Point.Y + (deltaY * t));
            tangent = ResolvePathSegmentTangent(pathSamples, i);
            return true;
        }

        point = pathSamples[pathSamples.Count - 1].Point;
        tangent = GetPathEndTangent(pathSamples);
        return true;
    }

    private static bool TryGetPathPointAndTangent(
        IReadOnlyList<PathSample> pathSamples,
        float distance,
        ref int preferredSegmentIndex,
        out SKPoint point,
        out SKPoint tangent)
    {
        point = default;
        tangent = new SKPoint(1f, 0f);
        if (pathSamples.Count == 0)
        {
            return false;
        }

        if (pathSamples.Count == 1)
        {
            point = pathSamples[0].Point;
            preferredSegmentIndex = 1;
            return true;
        }

        if (distance <= 0f)
        {
            point = pathSamples[0].Point;
            tangent = GetPathStartTangent(pathSamples);
            preferredSegmentIndex = FindNextUsablePathSegmentIndex(pathSamples, 1);
            return true;
        }

        var lastIndex = pathSamples.Count - 1;
        if (distance >= pathSamples[lastIndex].Distance)
        {
            point = pathSamples[lastIndex].Point;
            tangent = GetPathEndTangent(pathSamples);
            preferredSegmentIndex = lastIndex;
            return true;
        }

        preferredSegmentIndex = ResolvePathSegmentIndex(pathSamples, distance, preferredSegmentIndex);
        var previous = pathSamples[preferredSegmentIndex - 1];
        var current = pathSamples[preferredSegmentIndex];
        var segmentLength = current.Distance - previous.Distance;
        if (segmentLength <= 0f)
        {
            point = current.Point;
            tangent = ResolvePathSegmentTangent(pathSamples, preferredSegmentIndex);
            return true;
        }

        var t = (distance - previous.Distance) / segmentLength;
        var deltaX = current.Point.X - previous.Point.X;
        var deltaY = current.Point.Y - previous.Point.Y;
        point = new SKPoint(
            previous.Point.X + (deltaX * t),
            previous.Point.Y + (deltaY * t));
        tangent = ResolvePathSegmentTangent(pathSamples, preferredSegmentIndex);
        return true;
    }

    private static int ResolvePathSegmentIndex(IReadOnlyList<PathSample> pathSamples, float distance, int preferredSegmentIndex)
    {
        var count = pathSamples.Count;
        preferredSegmentIndex = FindNextUsablePathSegmentIndex(pathSamples, preferredSegmentIndex < 1 ? 1 : preferredSegmentIndex);
        if (preferredSegmentIndex < count)
        {
            var previous = pathSamples[preferredSegmentIndex - 1];
            var current = pathSamples[preferredSegmentIndex];
            if (distance >= previous.Distance && distance <= current.Distance)
            {
                return preferredSegmentIndex;
            }

            if (distance > current.Distance)
            {
                return BinarySearchPathSegmentIndex(pathSamples, distance, preferredSegmentIndex + 1, count - 1);
            }
        }

        return BinarySearchPathSegmentIndex(pathSamples, distance, 1, count - 1);
    }

    private static int BinarySearchPathSegmentIndex(IReadOnlyList<PathSample> pathSamples, float distance, int low, int high)
    {
        if (low > high)
        {
            return pathSamples.Count - 1;
        }

        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (pathSamples[mid].Distance < distance)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return FindNextUsablePathSegmentIndex(pathSamples, low);
    }

    private static int FindNextUsablePathSegmentIndex(IReadOnlyList<PathSample> pathSamples, int startIndex)
    {
        for (var i = Math.Max(1, startIndex); i < pathSamples.Count; i++)
        {
            if (!pathSamples[i].StartsSubpath)
            {
                return i;
            }
        }

        return pathSamples.Count - 1;
    }

    private static float ResolveTextPathStartOffset(SvgTextPath svgTextPath, SKPath skPath, SKRect viewport, float pathLength)
    {
        var startOffset = svgTextPath.StartOffset;
        if (startOffset == SvgUnit.None || startOffset == SvgUnit.Empty)
        {
            return 0f;
        }

        if (IsPercentageStartOffset(svgTextPath, startOffset))
        {
            var effectivePathLength = pathLength > 0f
                ? pathLength
                : EstimatePathLength(skPath);
            return effectivePathLength * (startOffset.Value / 100f);
        }

        return startOffset.ToDeviceValue(UnitRenderingType.Other, svgTextPath, viewport);
    }

    private static bool TryCreateTextPathGeometryCacheKey(
        SvgTextPath svgTextPath,
        SKRect viewport,
        out TextPathGeometryCacheKey cacheKey,
        out SvgElement? referencedElement,
        out SvgPath? svgPath,
        out SvgVisualElement? referencedVisualElement,
        out SKMatrix transform,
        out float samplingScale)
    {
        cacheKey = default;
        referencedElement = null;
        svgPath = null;
        referencedVisualElement = null;

        if (HasInlineTextPathGeometry(svgTextPath))
        {
            var pathData = svgTextPath.PathData;
            transform = SKMatrix.Identity;
            samplingScale = GetTextPathSamplingScale(svgTextPath, transform);
            cacheKey = new TextPathGeometryCacheKey(
                TextPathGeometrySourceKind.InlinePathData,
                pathData,
                CreatePathSegmentListGeometrySignature(pathData),
                transform,
                svgTextPath.Side,
                samplingScale,
                viewport);
            return true;
        }

        transform = SKMatrix.Identity;
        samplingScale = 1f;
        referencedElement = ResolveTextPathGeometryReference(svgTextPath);
        if (referencedElement is null)
        {
            return false;
        }

        svgPath = referencedElement as SvgPath;
        referencedVisualElement = referencedElement as SvgVisualElement;
        transform = GetTextPathReferenceTransform(referencedVisualElement);
        samplingScale = GetTextPathSamplingScale(svgTextPath, transform);
        cacheKey = new TextPathGeometryCacheKey(
            TextPathGeometrySourceKind.ReferencedElement,
            referencedElement,
            CreateReferencedElementGeometrySignature(referencedElement),
            transform,
            svgTextPath.Side,
            samplingScale,
            viewport);
        return true;
    }

    private static SvgElement? ResolveTextPathGeometryReference(SvgTextPath svgTextPath)
    {
        return TryResolveSameDocumentTextPathReference(svgTextPath, out var referencedElement)
            ? referencedElement
            : SvgService.GetReference<SvgElement>(svgTextPath, SvgService.GetEffectiveReferenceUri(svgTextPath, svgTextPath.ReferencedPath));
    }

    private static bool TryResolveSameDocumentTextPathReference(SvgTextPath svgTextPath, out SvgElement? referencedElement)
    {
        referencedElement = null;
        if (svgTextPath.TryGetEffectiveHrefString(out var hrefText))
        {
            return TryResolveSameDocumentFragmentReference(svgTextPath, hrefText, out referencedElement);
        }

        var referencedPath = svgTextPath.ReferencedPath;
        if (referencedPath is null ||
            !TryGetSameDocumentFragmentReferenceId(referencedPath, out var referenceId))
        {
            return false;
        }

        referencedElement = svgTextPath.OwnerDocument?.IdManager.GetElementById(referenceId);
        return true;
    }

    private static bool TryResolveSameDocumentFragmentReference(SvgElement owner, string? hrefText, out SvgElement? referencedElement)
    {
        referencedElement = null;
        if (!TryGetSameDocumentFragmentReferenceId(hrefText, out var referenceId))
        {
            return false;
        }

        referencedElement = owner.OwnerDocument?.IdManager.GetElementById(referenceId);
        return true;
    }

    private static bool TryGetSameDocumentFragmentReferenceId(Uri uri, out string referenceId)
    {
        referenceId = string.Empty;
        if (uri.IsAbsoluteUri || string.IsNullOrEmpty(uri.OriginalString))
        {
            return false;
        }

        return TryGetSameDocumentFragmentReferenceId(uri.OriginalString, out referenceId);
    }

    private static bool TryGetSameDocumentFragmentReferenceId(string? hrefText, out string referenceId)
    {
        referenceId = string.Empty;
        if (hrefText is null || hrefText.Length == 0)
        {
            return false;
        }

        var start = 0;
        var end = hrefText.Length - 1;
        while (start <= end && char.IsWhiteSpace(hrefText[start]))
        {
            start++;
        }

        while (end >= start && char.IsWhiteSpace(hrefText[end]))
        {
            end--;
        }

        if (start > end ||
            hrefText[start] != '#' ||
            start == end)
        {
            return false;
        }

        for (var i = start + 1; i <= end; i++)
        {
            if (hrefText[i] == '%' || char.IsWhiteSpace(hrefText[i]))
            {
                return false;
            }
        }

        referenceId = hrefText.Substring(start + 1, end - start);
        return true;
    }

    private static bool TryGetCachedTextPathGeometry(
        TextPathGeometryCacheKey cacheKey,
        out SvgPath? svgPath,
        out SKPath skPath,
        out SKRect geometryBounds,
        out List<PathSample> pathSamples,
        out float pathLength,
        out bool isClosedLoop)
    {
        lock (s_textPathGeometryCacheLock)
        {
            if (s_textPathGeometryCache.TryGetValue(cacheKey, out var entry))
            {
                svgPath = entry.SvgPath;
                skPath = entry.SkPath;
                geometryBounds = entry.GeometryBounds;
                pathSamples = entry.PathSamples;
                pathLength = entry.PathLength;
                isClosedLoop = entry.IsClosedLoop;
                return true;
            }
        }

        svgPath = null;
        skPath = new SKPath();
        geometryBounds = SKRect.Empty;
        pathSamples = new List<PathSample>();
        pathLength = 0f;
        isClosedLoop = false;
        return false;
    }

    private static void AddCachedTextPathGeometry(
        TextPathGeometryCacheKey cacheKey,
        SvgPath? svgPath,
        SKPath skPath,
        SKRect geometryBounds,
        List<PathSample> pathSamples,
        float pathLength,
        bool isClosedLoop)
    {
        lock (s_textPathGeometryCacheLock)
        {
            if (s_textPathGeometryCache.ContainsKey(cacheKey))
            {
                return;
            }

            s_textPathGeometryCache[cacheKey] = new TextPathGeometryCacheEntry(
                svgPath,
                skPath,
                geometryBounds,
                pathSamples,
                pathLength,
                isClosedLoop);
            s_textPathGeometryCacheOrder.Enqueue(cacheKey);

            while (s_textPathGeometryCache.Count > TextPathGeometryCacheLimit &&
                   s_textPathGeometryCacheOrder.Count > 0)
            {
                s_textPathGeometryCache.Remove(s_textPathGeometryCacheOrder.Dequeue());
            }
        }
    }

    private static long CreateReferencedElementGeometrySignature(SvgElement referencedElement)
    {
        var signature = MixSignature(17, referencedElement.GetType().FullName);
        signature = referencedElement switch
        {
            SvgPath svgPath => MixSvgPathGeometrySignature(signature, svgPath),
            SvgRectangle svgRectangle => MixSvgRectangleGeometrySignature(signature, svgRectangle),
            SvgCircle svgCircle => MixSvgCircleGeometrySignature(signature, svgCircle),
            SvgEllipse svgEllipse => MixSvgEllipseGeometrySignature(signature, svgEllipse),
            SvgLine svgLine => MixSvgLineGeometrySignature(signature, svgLine),
            SvgPolyline svgPolyline => MixSvgPolylineGeometrySignature(signature, svgPolyline),
            SvgPolygon svgPolygon => MixSvgPolygonGeometrySignature(signature, svgPolygon),
            _ => signature
        };

        return signature;
    }

    private static long MixSvgPathGeometrySignature(long signature, SvgPath svgPath)
    {
        signature = MixSignature(signature, (int)svgPath.FillRule);
        signature = MixSignature(signature, svgPath.PathLength);
        signature = MixPathSegmentListGeometrySignature(signature, GetRawPathData(svgPath) ?? svgPath.PathData);
        signature = MixCascadedCssPropertySignature(signature, svgPath, "d");
        return signature;
    }

    private static SvgPathSegmentList? GetRawPathData(SvgPath svgPath)
    {
        return svgPath.Attributes.TryGetValue("d", out var pathData) && pathData is SvgPathSegmentList segments
            ? segments
            : null;
    }

    private static long MixSvgRectangleGeometrySignature(long signature, SvgRectangle svgRectangle)
    {
        signature = MixPathBasedGeometrySignature(signature, svgRectangle);
        signature = MixGeometryUnitPropertySignature(signature, svgRectangle, "x", svgRectangle.X);
        signature = MixGeometryUnitPropertySignature(signature, svgRectangle, "y", svgRectangle.Y);
        signature = MixGeometryUnitPropertySignature(signature, svgRectangle, "width", svgRectangle.Width);
        signature = MixGeometryUnitPropertySignature(signature, svgRectangle, "height", svgRectangle.Height);
        signature = MixGeometryUnitPropertySignature(signature, svgRectangle, "rx", svgRectangle.CornerRadiusX);
        signature = MixGeometryUnitPropertySignature(signature, svgRectangle, "ry", svgRectangle.CornerRadiusY);
        return signature;
    }

    private static long MixSvgCircleGeometrySignature(long signature, SvgCircle svgCircle)
    {
        signature = MixPathBasedGeometrySignature(signature, svgCircle);
        signature = MixGeometryUnitPropertySignature(signature, svgCircle, "cx", svgCircle.CenterX);
        signature = MixGeometryUnitPropertySignature(signature, svgCircle, "cy", svgCircle.CenterY);
        signature = MixGeometryUnitPropertySignature(signature, svgCircle, "r", svgCircle.Radius);
        return signature;
    }

    private static long MixSvgEllipseGeometrySignature(long signature, SvgEllipse svgEllipse)
    {
        signature = MixPathBasedGeometrySignature(signature, svgEllipse);
        signature = MixGeometryUnitPropertySignature(signature, svgEllipse, "cx", svgEllipse.CenterX);
        signature = MixGeometryUnitPropertySignature(signature, svgEllipse, "cy", svgEllipse.CenterY);
        signature = MixGeometryUnitPropertySignature(signature, svgEllipse, "rx", svgEllipse.RadiusX);
        signature = MixGeometryUnitPropertySignature(signature, svgEllipse, "ry", svgEllipse.RadiusY);
        return signature;
    }

    private static long MixSvgLineGeometrySignature(long signature, SvgLine svgLine)
    {
        signature = MixPathBasedGeometrySignature(signature, svgLine);
        signature = MixGeometryUnitPropertySignature(signature, svgLine, "x1", svgLine.StartX);
        signature = MixGeometryUnitPropertySignature(signature, svgLine, "y1", svgLine.StartY);
        signature = MixGeometryUnitPropertySignature(signature, svgLine, "x2", svgLine.EndX);
        signature = MixGeometryUnitPropertySignature(signature, svgLine, "y2", svgLine.EndY);
        return signature;
    }

    private static long MixSvgPolylineGeometrySignature(long signature, SvgPolyline svgPolyline)
    {
        signature = MixPathBasedGeometrySignature(signature, svgPolyline);
        return MixPointCollectionSignature(signature, svgPolyline.Points);
    }

    private static long MixSvgPolygonGeometrySignature(long signature, SvgPolygon svgPolygon)
    {
        signature = MixPathBasedGeometrySignature(signature, svgPolygon);
        return MixPointCollectionSignature(signature, svgPolygon.Points);
    }

    private static long MixPathBasedGeometrySignature(long signature, SvgElement element)
    {
        signature = MixSignature(signature, element switch
        {
            SvgPath svgPath => (int)svgPath.FillRule,
            SvgRectangle svgRectangle => (int)svgRectangle.FillRule,
            SvgCircle svgCircle => (int)svgCircle.FillRule,
            SvgEllipse svgEllipse => (int)svgEllipse.FillRule,
            SvgLine svgLine => (int)svgLine.FillRule,
            SvgPolyline svgPolyline => (int)svgPolyline.FillRule,
            SvgPolygon svgPolygon => (int)svgPolygon.FillRule,
            _ => (int)SvgFillRule.NonZero
        });

        signature = MixSignature(signature, element switch
        {
            SvgPath svgPath => svgPath.PathLength,
            SvgRectangle svgRectangle => svgRectangle.PathLength,
            SvgCircle svgCircle => svgCircle.PathLength,
            SvgEllipse svgEllipse => svgEllipse.PathLength,
            SvgLine svgLine => svgLine.PathLength,
            SvgPolyline svgPolyline => svgPolyline.PathLength,
            SvgPolygon svgPolygon => svgPolygon.PathLength,
            _ => 0f
        });
        return signature;
    }

    private static long MixGeometryUnitPropertySignature(long signature, SvgElement element, string propertyName, SvgUnit fallback)
    {
        signature = MixSignature(signature, fallback.Type.GetHashCode());
        signature = MixSignature(signature, fallback.Value);
        signature = MixSignature(signature, fallback.IsEmpty ? 1 : 0);
        return MixComputedPropertySignature(signature, element, propertyName);
    }

    private static long MixCascadedCssPropertySignature(long signature, SvgElement element, string propertyName)
    {
        if (element.TryGetOwnCascadedCssDeclarationValue(propertyName, out var ownCssValue))
        {
            signature = MixSignature(signature, 1);
            signature = MixSignature(signature, ownCssValue);
            if (string.Equals(ownCssValue.Trim(), "inherit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ownCssValue.Trim(), "unset", StringComparison.OrdinalIgnoreCase))
            {
                signature = MixComputedStylePropertySignature(signature, element, propertyName);
            }
        }
        else
        {
            signature = MixSignature(signature, 0);
        }

        return signature;
    }

    private static long MixComputedPropertySignature(long signature, SvgElement element, string propertyName)
    {
        if (element.TryGetAttribute(propertyName, out var attributeValue))
        {
            signature = MixSignature(signature, 1);
            signature = MixSignature(signature, attributeValue);
        }
        else
        {
            signature = MixSignature(signature, 0);
        }

        if (element.TryGetOwnCascadedCssDeclarationValue(propertyName, out var ownCssValue))
        {
            signature = MixSignature(signature, 1);
            signature = MixSignature(signature, ownCssValue);
            if (string.Equals(ownCssValue.Trim(), "inherit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ownCssValue.Trim(), "unset", StringComparison.OrdinalIgnoreCase))
            {
                signature = MixComputedStylePropertySignature(signature, element, propertyName);
            }
        }
        else
        {
            signature = MixSignature(signature, 0);
        }

        return signature;
    }

    private static long MixComputedStylePropertySignature(long signature, SvgElement element, string propertyName)
    {
        if (element.ComputedStyle.TryGetPropertyValue(propertyName, out var computedValue))
        {
            signature = MixSignature(signature, 1);
            signature = MixSignature(signature, computedValue);
        }
        else
        {
            signature = MixSignature(signature, 0);
        }

        return signature;
    }

    private static long CreatePathSegmentListGeometrySignature(SvgPathSegmentList pathData)
    {
        return MixPathSegmentListGeometrySignature(23, pathData);
    }

    private static long MixPathSegmentListGeometrySignature(long signature, SvgPathSegmentList? pathData)
    {
        if (pathData is null)
        {
            return MixSignature(signature, 0);
        }

        signature = MixSignature(signature, pathData.Count);
        for (var i = 0; i < pathData.Count; i++)
        {
            signature = MixPathSegmentSignature(signature, pathData[i]);
        }

        return signature;
    }

    private static long MixPathSegmentSignature(long signature, SvgPathSegment segment)
    {
        signature = segment switch
        {
            SvgMoveToSegment => MixSignature(signature, 1),
            SvgLineSegment => MixSignature(signature, 2),
            SvgCubicCurveSegment => MixSignature(signature, 3),
            SvgQuadraticCurveSegment => MixSignature(signature, 4),
            SvgArcSegment => MixSignature(signature, 5),
            SvgClosePathSegment => MixSignature(signature, 6),
            _ => MixSignature(signature, segment.GetType().FullName)
        };

        signature = MixSignature(signature, segment.IsRelative ? 1 : 0);
        signature = MixPointSignature(signature, segment.End);
        signature = segment switch
        {
            SvgCubicCurveSegment cubic => MixPointSignature(MixPointSignature(signature, cubic.FirstControlPoint), cubic.SecondControlPoint),
            SvgQuadraticCurveSegment quadratic => MixPointSignature(signature, quadratic.ControlPoint),
            SvgArcSegment arc => MixArcSegmentSignature(signature, arc),
            _ => signature
        };
        return signature;
    }

    private static long MixArcSegmentSignature(long signature, SvgArcSegment arc)
    {
        signature = MixSignature(signature, arc.RadiusX);
        signature = MixSignature(signature, arc.RadiusY);
        signature = MixSignature(signature, arc.Angle);
        signature = MixSignature(signature, (int)arc.Sweep);
        signature = MixSignature(signature, (int)arc.Size);
        return signature;
    }

    private static long MixPointCollectionSignature(long signature, SvgPointCollection? points)
    {
        if (points is null)
        {
            return MixSignature(signature, 0);
        }

        signature = MixSignature(signature, points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            signature = MixSignature(signature, points[i].Type.GetHashCode());
            signature = MixSignature(signature, points[i].Value);
            signature = MixSignature(signature, points[i].IsEmpty ? 1 : 0);
        }

        return signature;
    }

    private static long MixPointSignature(long signature, System.Drawing.PointF point)
    {
        signature = MixSignature(signature, point.X);
        signature = MixSignature(signature, point.Y);
        return signature;
    }

    private static long MixSignature(long signature, string? value)
    {
        if (value is null)
        {
            return MixSignature(signature, 0);
        }

        signature = MixSignature(signature, value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            signature = MixSignature(signature, value[i]);
        }

        return signature;
    }

    private static long MixSignature(long signature, float value)
    {
        return MixSignature(signature, value.GetHashCode());
    }

    private static long MixSignature(long signature, int value)
    {
        unchecked
        {
            return (signature * 397L) ^ (uint)value;
        }
    }

    private static int CombineHash(int hash, int value)
    {
        unchecked
        {
            return (hash * 397) ^ value;
        }
    }

    private static bool MatrixEquals(SKMatrix left, SKMatrix right)
    {
        return left.ScaleX == right.ScaleX &&
               left.SkewX == right.SkewX &&
               left.TransX == right.TransX &&
               left.SkewY == right.SkewY &&
               left.ScaleY == right.ScaleY &&
               left.TransY == right.TransY &&
               left.Persp0 == right.Persp0 &&
               left.Persp1 == right.Persp1 &&
               left.Persp2 == right.Persp2;
    }

    private static bool TryResolveTextPathGeometry(
        SvgTextPath svgTextPath,
        SKRect viewport,
        out SvgPath? svgPath,
        out SKPath skPath,
        out SKRect geometryBounds,
        out List<PathSample> pathSamples,
        out float pathLength,
        out bool isClosedLoop)
    {
        if (!TryCreateTextPathGeometryCacheKey(
                svgTextPath,
                viewport,
                out var cacheKey,
                out var referencedElement,
                out svgPath,
                out var referencedVisualElement,
                out var transform,
                out var samplingScale))
        {
            skPath = new SKPath();
            geometryBounds = SKRect.Empty;
            pathSamples = new List<PathSample>();
            pathLength = 0f;
            isClosedLoop = false;
            return false;
        }

        if (TryGetCachedTextPathGeometry(
                cacheKey,
                out svgPath,
                out skPath,
                out geometryBounds,
                out pathSamples,
                out pathLength,
                out isClosedLoop))
        {
            return true;
        }

        if (HasInlineTextPathGeometry(svgTextPath))
        {
            skPath = svgTextPath.PathData.ToPath(SvgFillRule.NonZero) ?? new SKPath();
        }
        else
        {
            skPath = referencedElement is not null &&
                     SvgSceneCompiler.TryGetDirectVisualPath(referencedElement, viewport, out var referencedPath) &&
                     referencedPath is not null
                ? referencedPath
                : new SKPath();
        }

        geometryBounds = SKRect.Empty;
        pathSamples = new List<PathSample>();
        pathLength = 0f;
        isClosedLoop = false;
        if (skPath.IsEmpty)
        {
            return false;
        }

        pathSamples = BuildPathSamples(skPath, samplingScale);
        if (pathSamples.Count < 2)
        {
            return false;
        }

        if (!IsIdentityTransform(transform))
        {
            pathSamples = TransformPathSamples(pathSamples, transform);
        }

        pathLength = pathSamples[pathSamples.Count - 1].Distance;
        if (TryGetAuthorPathLength(svgPath ?? referencedVisualElement, out var authorPathLength))
        {
            pathSamples = ScalePathSampleDistances(pathSamples, authorPathLength);
            pathLength = authorPathLength;
        }

        if (svgTextPath.Side == SvgTextPathSide.Right)
        {
            pathSamples = ReversePathSamples(pathSamples);
            pathLength = pathSamples[pathSamples.Count - 1].Distance;
        }

        isClosedLoop = IsSingleClosedSubpath(pathSamples);
        geometryBounds = GetPathSampleBounds(pathSamples);
        if (pathLength <= 0f)
        {
            return false;
        }

        AddCachedTextPathGeometry(
            cacheKey,
            svgPath,
            skPath,
            geometryBounds,
            pathSamples,
            pathLength,
            isClosedLoop);
        return true;
    }

    private static SKMatrix GetTextPathReferenceTransform(SvgVisualElement? svgVisualElement)
    {
        return svgVisualElement is { Transforms.Count: > 0 }
            ? TransformsService.ToMatrix(svgVisualElement.Transforms)
            : SKMatrix.Identity;
    }

    private static float GetTextPathSamplingScale(SvgTextPath svgTextPath, SKMatrix referenceTransform)
    {
        var scale = GetTransformMaxScale(referenceTransform);
        for (SvgElement? current = svgTextPath; current is not null; current = current.Parent)
        {
            if (current is SvgVisualElement { Transforms.Count: > 0 } visualElement)
            {
                scale *= GetTransformMaxScale(TransformsService.ToMatrix(visualElement.Transforms));
            }
        }

        return NormalizeTextPathSamplingScale(scale);
    }

    private static float GetTransformMaxScale(SKMatrix matrix)
    {
        if (IsIdentityTransform(matrix))
        {
            return 1f;
        }

        var scaleX = (float)Math.Sqrt((matrix.ScaleX * matrix.ScaleX) + (matrix.SkewY * matrix.SkewY));
        var scaleY = (float)Math.Sqrt((matrix.SkewX * matrix.SkewX) + (matrix.ScaleY * matrix.ScaleY));
        var scale = Math.Max(scaleX, scaleY);
        return scale > 0f ? scale : 1f;
    }

    private static bool IsIdentityTransform(SKMatrix matrix)
    {
        return matrix.ScaleX == SKMatrix.Identity.ScaleX &&
               matrix.SkewX == SKMatrix.Identity.SkewX &&
               matrix.TransX == SKMatrix.Identity.TransX &&
               matrix.SkewY == SKMatrix.Identity.SkewY &&
               matrix.ScaleY == SKMatrix.Identity.ScaleY &&
               matrix.TransY == SKMatrix.Identity.TransY &&
               matrix.Persp0 == SKMatrix.Identity.Persp0 &&
               matrix.Persp1 == SKMatrix.Identity.Persp1 &&
               matrix.Persp2 == SKMatrix.Identity.Persp2;
    }

    private static List<PathSample> TransformPathSamples(IReadOnlyList<PathSample> pathSamples, SKMatrix transform)
    {
        var transformed = new List<PathSample>(pathSamples.Count);
        var totalDistance = 0f;
        for (var i = 0; i < pathSamples.Count; i++)
        {
            var mappedPoint = transform.MapPoint(pathSamples[i].Point);
            if (i > 0 && !pathSamples[i].StartsSubpath)
            {
                totalDistance += Distance(transformed[i - 1].Point, mappedPoint);
            }

            transformed.Add(new PathSample(mappedPoint, totalDistance, pathSamples[i].StartsSubpath, pathSamples[i].ClosesSubpath));
        }

        return transformed;
    }

    private static List<PathSample> ReversePathSamples(IReadOnlyList<PathSample> pathSamples)
    {
        var reversed = new List<PathSample>(pathSamples.Count);
        if (pathSamples.Count == 0)
        {
            return reversed;
        }

        var totalDistance = pathSamples[pathSamples.Count - 1].Distance;
        var isClosedLoop = IsSingleClosedSubpath(pathSamples);
        for (var i = pathSamples.Count - 1; i >= 0; i--)
        {
            var startsSubpath = i < pathSamples.Count - 1 && pathSamples[i + 1].StartsSubpath;
            var closesSubpath = isClosedLoop && i == 0;
            reversed.Add(new PathSample(
                pathSamples[i].Point,
                totalDistance - pathSamples[i].Distance,
                startsSubpath,
                closesSubpath));
        }

        return reversed;
    }

    private static List<PathSample> ScalePathSampleDistances(IReadOnlyList<PathSample> pathSamples, float pathLength)
    {
        var scaled = new List<PathSample>(pathSamples.Count);
        if (pathSamples.Count == 0)
        {
            return scaled;
        }

        var measuredLength = pathSamples[pathSamples.Count - 1].Distance;
        if (measuredLength <= 0f || pathLength <= 0f)
        {
            scaled.AddRange(pathSamples);
            return scaled;
        }

        var scale = pathLength / measuredLength;
        for (var i = 0; i < pathSamples.Count; i++)
        {
            scaled.Add(pathSamples[i] with
            {
                Distance = pathSamples[i].Distance * scale
            });
        }

        return scaled;
    }

    private static bool IsSingleClosedSubpath(IReadOnlyList<PathSample> pathSamples)
    {
        if (pathSamples.Count < 2 || !pathSamples[pathSamples.Count - 1].ClosesSubpath)
        {
            return false;
        }

        for (var i = 1; i < pathSamples.Count; i++)
        {
            if (pathSamples[i].StartsSubpath)
            {
                return false;
            }
        }

        return Distance(pathSamples[0].Point, pathSamples[pathSamples.Count - 1].Point) <= 0.001f;
    }

    private static bool TryGetAuthorPathLength(SvgElement? element, out float pathLength)
    {
        pathLength = element switch
        {
            SvgPath { PathLength: > 0f } path => path.PathLength,
            SvgCircle { PathLength: > 0f } circle => circle.PathLength,
            SvgEllipse { PathLength: > 0f } ellipse => ellipse.PathLength,
            SvgLine { PathLength: > 0f } line => line.PathLength,
            SvgPolyline { PathLength: > 0f } polyline => polyline.PathLength,
            SvgPolygon { PathLength: > 0f } polygon => polygon.PathLength,
            SvgRectangle { PathLength: > 0f } rectangle => rectangle.PathLength,
            _ => 0f
        };

        return pathLength > 0f;
    }

    private static SKPoint GetPathStartTangent(IReadOnlyList<PathSample> pathSamples)
    {
        for (var i = 1; i < pathSamples.Count; i++)
        {
            if (!TryGetPathSegmentTangent(pathSamples, i, out var tangent))
            {
                continue;
            }

            return tangent;
        }

        return new SKPoint(1f, 0f);
    }

    private static SKPoint GetPathEndTangent(IReadOnlyList<PathSample> pathSamples)
    {
        for (var i = pathSamples.Count - 1; i >= 1; i--)
        {
            if (!TryGetPathSegmentTangent(pathSamples, i, out var tangent))
            {
                continue;
            }

            return tangent;
        }

        return new SKPoint(1f, 0f);
    }

    private static SKPoint ResolvePathSegmentTangent(IReadOnlyList<PathSample> pathSamples, int segmentIndex)
    {
        if (TryGetPathSegmentTangent(pathSamples, segmentIndex, out var tangent))
        {
            return tangent;
        }

        for (var i = segmentIndex + 1; i < pathSamples.Count; i++)
        {
            if (TryGetPathSegmentTangent(pathSamples, i, out tangent))
            {
                return tangent;
            }
        }

        for (var i = segmentIndex - 1; i >= 1; i--)
        {
            if (TryGetPathSegmentTangent(pathSamples, i, out tangent))
            {
                return tangent;
            }
        }

        return new SKPoint(1f, 0f);
    }

    private static bool TryGetPathSegmentTangent(IReadOnlyList<PathSample> pathSamples, int segmentIndex, out SKPoint tangent)
    {
        tangent = new SKPoint(1f, 0f);
        if (segmentIndex <= 0 ||
            segmentIndex >= pathSamples.Count ||
            pathSamples[segmentIndex].StartsSubpath)
        {
            return false;
        }

        return TryNormalize(
            new SKPoint(
                pathSamples[segmentIndex].Point.X - pathSamples[segmentIndex - 1].Point.X,
                pathSamples[segmentIndex].Point.Y - pathSamples[segmentIndex - 1].Point.Y),
            out tangent);
    }

    private static SKRect GetPathSampleBounds(IReadOnlyList<PathSample> pathSamples)
    {
        if (pathSamples.Count == 0)
        {
            return SKRect.Empty;
        }

        var left = pathSamples[0].Point.X;
        var top = pathSamples[0].Point.Y;
        var right = left;
        var bottom = top;
        for (var i = 1; i < pathSamples.Count; i++)
        {
            var point = pathSamples[i].Point;
            left = Math.Min(left, point.X);
            top = Math.Min(top, point.Y);
            right = Math.Max(right, point.X);
            bottom = Math.Max(bottom, point.Y);
        }

        return new SKRect(left, top, right, bottom);
    }

    private static bool IsPercentageStartOffset(SvgTextPath svgTextPath, SvgUnit startOffset)
    {
        if (startOffset.Type == SvgUnitType.Percentage)
        {
            return true;
        }

        return svgTextPath.TryGetAttribute("startOffset", out var rawStartOffset) &&
               rawStartOffset.TrimEnd().EndsWith("%", StringComparison.Ordinal);
    }

    private static float EstimatePathLength(SKPath path)
    {
        if (path.Commands is null || path.Commands.Count == 0)
        {
            return 0f;
        }

        var total = 0f;
        var current = default(SKPoint);
        var figureStart = default(SKPoint);
        var hasCurrent = false;

        for (var i = 0; i < path.Commands.Count; i++)
        {
            switch (path.Commands[i])
            {
                case MoveToPathCommand moveTo:
                    current = new SKPoint(moveTo.X, moveTo.Y);
                    figureStart = current;
                    hasCurrent = true;
                    break;

                case LineToPathCommand lineTo when hasCurrent:
                    {
                        var next = new SKPoint(lineTo.X, lineTo.Y);
                        total += Distance(current, next);
                        current = next;
                    }
                    break;

                case QuadToPathCommand quadTo when hasCurrent:
                    {
                        var c1 = new SKPoint(quadTo.X0, quadTo.Y0);
                        var end = new SKPoint(quadTo.X1, quadTo.Y1);
                        total += ApproximateQuadraticLength(current, c1, end);
                        current = end;
                    }
                    break;

                case CubicToPathCommand cubicTo when hasCurrent:
                    {
                        var c1 = new SKPoint(cubicTo.X0, cubicTo.Y0);
                        var c2 = new SKPoint(cubicTo.X1, cubicTo.Y1);
                        var end = new SKPoint(cubicTo.X2, cubicTo.Y2);
                        total += ApproximateCubicLength(current, c1, c2, end);
                        current = end;
                    }
                    break;

                case ArcToPathCommand arcTo when hasCurrent:
                    {
                        total += ApproximateArcLength(current, arcTo);
                        current = new SKPoint(arcTo.X, arcTo.Y);
                    }
                    break;

                case ClosePathCommand _ when hasCurrent:
                    total += Distance(current, figureStart);
                    current = figureStart;
                    break;
            }
        }

        return total;
    }

    private static float ApproximateQuadraticLength(SKPoint start, SKPoint control, SKPoint end)
    {
        const int steps = 24;
        var length = 0f;
        var previous = start;

        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            var point = EvaluateQuadratic(start, control, end, t);
            length += Distance(previous, point);
            previous = point;
        }

        return length;
    }

    private static float ApproximateCubicLength(SKPoint start, SKPoint control1, SKPoint control2, SKPoint end)
    {
        const int steps = 32;
        var length = 0f;
        var previous = start;

        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            var point = EvaluateCubic(start, control1, control2, end, t);
            length += Distance(previous, point);
            previous = point;
        }

        return length;
    }

    private static float ApproximateArcLength(SKPoint start, ArcToPathCommand arcTo)
    {
        var end = new SKPoint(arcTo.X, arcTo.Y);
        if (!TryGetArcParameters(start, end, arcTo.Rx, arcTo.Ry, arcTo.XAxisRotate, arcTo.LargeArc, arcTo.Sweep, out var parameters))
        {
            return Distance(start, end);
        }

        var length = 0f;
        var previous = start;
        AppendArcSamples(parameters, point =>
        {
            if (NearlyEquals(previous, point))
            {
                return;
            }

            length += Distance(previous, point);
            previous = point;
        });
        return length;
    }

    private static bool TryAppendArcSamples(SKPoint start, ArcToPathCommand arcTo, float samplingScale, Action<SKPoint> appendSample)
    {
        var end = new SKPoint(arcTo.X, arcTo.Y);
        if (!TryGetArcParameters(start, end, arcTo.Rx, arcTo.Ry, arcTo.XAxisRotate, arcTo.LargeArc, arcTo.Sweep, out var parameters))
        {
            return false;
        }

        AppendArcSamples(parameters, appendSample, samplingScale);
        return true;
    }

    private static void AppendArcSamples(ArcParameters parameters, Action<SKPoint> appendSample, float samplingScale = 1f)
    {
        var steps = ResolveArcSampleSteps(parameters, samplingScale);
        for (var i = 1; i <= steps; i++)
        {
            var theta = parameters.StartAngle + (parameters.DeltaAngle * i / steps);
            var cosTheta = (float)Math.Cos(theta);
            var sinTheta = (float)Math.Sin(theta);
            appendSample(new SKPoint(
                (parameters.CosPhi * parameters.Rx * cosTheta) - (parameters.SinPhi * parameters.Ry * sinTheta) + parameters.Center.X,
                (parameters.SinPhi * parameters.Rx * cosTheta) + (parameters.CosPhi * parameters.Ry * sinTheta) + parameters.Center.Y));
        }
    }

    private static int ResolveArcSampleSteps(ArcParameters parameters, float samplingScale)
    {
        var approxLength = Math.Abs(parameters.DeltaAngle) * Math.Max(parameters.Rx, parameters.Ry);
        var normalizedScale = NormalizeTextPathSamplingScale(samplingScale);
        return ClampSteps((int)Math.Ceiling(approxLength * normalizedScale / 4f), 6, ResolveTextPathSamplingMaxSteps(normalizedScale, MaxEllipseSteps));
    }

    private static bool TryGetArcParameters(
        SKPoint start,
        SKPoint end,
        float rx,
        float ry,
        float angle,
        SKPathArcSize largeArc,
        SKPathDirection sweep,
        out ArcParameters parameters)
    {
        parameters = default;

        rx = Math.Abs(rx);
        ry = Math.Abs(ry);
        if (rx <= float.Epsilon || ry <= float.Epsilon || NearlyEquals(start, end))
        {
            return false;
        }

        var phi = angle * (float)Math.PI / 180f;
        var cosPhi = (float)Math.Cos(phi);
        var sinPhi = (float)Math.Sin(phi);

        var dx2 = (start.X - end.X) / 2f;
        var dy2 = (start.Y - end.Y) / 2f;
        var x1p = (cosPhi * dx2) + (sinPhi * dy2);
        var y1p = (-sinPhi * dx2) + (cosPhi * dy2);

        var rxsq = rx * rx;
        var rysq = ry * ry;
        var x1psq = x1p * x1p;
        var y1psq = y1p * y1p;

        var lambda = (x1psq / rxsq) + (y1psq / rysq);
        if (lambda > 1f)
        {
            var factor = (float)Math.Sqrt(lambda);
            rx *= factor;
            ry *= factor;
            rxsq = rx * rx;
            rysq = ry * ry;
        }

        var denominator = (rxsq * y1psq) + (rysq * x1psq);
        if (denominator <= float.Epsilon)
        {
            return false;
        }

        var sign = (largeArc == SKPathArcSize.Large) == (sweep == SKPathDirection.Clockwise) ? -1f : 1f;
        var sq = ((rxsq * rysq) - (rxsq * y1psq) - (rysq * x1psq)) / denominator;
        sq = Math.Max(sq, 0f);
        var coef = sign * (float)Math.Sqrt(sq);
        var cxp = coef * (rx * y1p / ry);
        var cyp = coef * (-ry * x1p / rx);

        var center = new SKPoint(
            (cosPhi * cxp) - (sinPhi * cyp) + ((start.X + end.X) / 2f),
            (sinPhi * cxp) + (cosPhi * cyp) + ((start.Y + end.Y) / 2f));

        var startAngle = (float)Math.Atan2((y1p - cyp) / ry, (x1p - cxp) / rx);
        var endAngle = (float)Math.Atan2((-y1p - cyp) / ry, (-x1p - cxp) / rx);
        var deltaAngle = endAngle - startAngle;
        if (sweep != SKPathDirection.Clockwise && deltaAngle > 0f)
        {
            deltaAngle -= FullCircleRadians;
        }
        else if (sweep == SKPathDirection.Clockwise && deltaAngle < 0f)
        {
            deltaAngle += FullCircleRadians;
        }

        parameters = new ArcParameters(center, rx, ry, startAngle, deltaAngle, cosPhi, sinPhi);
        return true;
    }

    private static SKPoint EvaluateQuadratic(SKPoint start, SKPoint control, SKPoint end, float t)
    {
        var oneMinusT = 1f - t;
        return new SKPoint(
            (oneMinusT * oneMinusT * start.X) + (2f * oneMinusT * t * control.X) + (t * t * end.X),
            (oneMinusT * oneMinusT * start.Y) + (2f * oneMinusT * t * control.Y) + (t * t * end.Y));
    }

    private static SKPoint EvaluateCubic(SKPoint start, SKPoint control1, SKPoint control2, SKPoint end, float t)
    {
        var oneMinusT = 1f - t;
        var oneMinusTSquared = oneMinusT * oneMinusT;
        var tSquared = t * t;
        return new SKPoint(
            (oneMinusTSquared * oneMinusT * start.X) +
            (3f * oneMinusTSquared * t * control1.X) +
            (3f * oneMinusT * tSquared * control2.X) +
            (tSquared * t * end.X),
            (oneMinusTSquared * oneMinusT * start.Y) +
            (3f * oneMinusTSquared * t * control1.Y) +
            (3f * oneMinusT * tSquared * control2.Y) +
            (tSquared * t * end.Y));
    }

    private static float Distance(SKPoint left, SKPoint right)
    {
        var dx = right.X - left.X;
        var dy = right.Y - left.Y;
        return (float)Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static int ClampSteps(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static float NormalizeTextPathSamplingScale(float samplingScale)
    {
        if (float.IsNaN(samplingScale) || float.IsInfinity(samplingScale) || samplingScale <= 1f)
        {
            return 1f;
        }

        return Math.Min(samplingScale, MaxTextPathSamplingScale);
    }

    private static int ResolveTextPathCurveSteps(float approximateLength, float samplingScale, int minSteps)
    {
        var normalizedScale = NormalizeTextPathSamplingScale(samplingScale);
        var maxSteps = ResolveTextPathSamplingMaxSteps(normalizedScale, MaxDefaultTextPathCurveSteps);
        var scaledLength = approximateLength * TextPathCurveSamplesPerUnit * normalizedScale;
        if (float.IsNaN(scaledLength) || float.IsInfinity(scaledLength) || scaledLength > maxSteps)
        {
            return maxSteps;
        }

        return ClampSteps((int)Math.Ceiling(scaledLength), minSteps, maxSteps);
    }

    private static int ResolveTextPathSamplingMaxSteps(float normalizedSamplingScale, int defaultMaxSteps)
    {
        return normalizedSamplingScale <= 1f ? defaultMaxSteps : MaxTextPathCurveSteps;
    }

    private static int ResolveTextPathFixedSteps(int baseSteps, float samplingScale, int minSteps, int maxSteps)
    {
        var scaledSteps = baseSteps * NormalizeTextPathSamplingScale(samplingScale);
        if (float.IsNaN(scaledSteps) || float.IsInfinity(scaledSteps) || scaledSteps > maxSteps)
        {
            return maxSteps;
        }

        return ClampSteps((int)Math.Ceiling(scaledSteps), minSteps, maxSteps);
    }

    private static float ClampFloat(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static bool NearlyEquals(SKPoint left, SKPoint right)
    {
        return Math.Abs(left.X - right.X) <= 0.001f &&
               Math.Abs(left.Y - right.Y) <= 0.001f;
    }

    private static SKPoint Normalize(SKPoint value)
    {
        return TryNormalize(value, out var normalized)
            ? normalized
            : new SKPoint(1f, 0f);
    }

    private static bool TryNormalize(SKPoint value, out SKPoint normalized)
    {
        var length = (float)Math.Sqrt((value.X * value.X) + (value.Y * value.Y));
        if (length <= TextPathTangentEpsilon)
        {
            normalized = new SKPoint(1f, 0f);
            return false;
        }

        normalized = new SKPoint(value.X / length, value.Y / length);
        return true;
    }

    private static bool HasExplicitTextPositioning(SvgTextBase svgTextBase)
    {
        return (svgTextBase.X?.Count ?? 0) > 0 ||
               (svgTextBase.Y?.Count ?? 0) > 0 ||
               (svgTextBase.Dx?.Count ?? 0) > 0 ||
               (svgTextBase.Dy?.Count ?? 0) > 0 ||
               HasRotateValues(svgTextBase) ||
               HasNonBaselineShift(svgTextBase);
    }

    private static bool StartsPositionedTextChunk(SvgTextBase svgTextBase)
    {
        return (svgTextBase.X?.Count ?? 0) > 0 ||
               (svgTextBase.Y?.Count ?? 0) > 0 ||
               (svgTextBase.Dx?.Count ?? 0) > 0 ||
               (svgTextBase.Dy?.Count ?? 0) > 0 ||
               HasNonBaselineShift(svgTextBase);
    }

    private static bool CanUseFlattenedTextLengthLayout(SvgTextBase svgTextBase)
    {
        return HasOwnTextLengthAdjustment(svgTextBase) &&
               !IsVerticalWritingMode(svgTextBase) &&
               !HasRotateValues(svgTextBase) &&
               !HasNonBaselineShift(svgTextBase) &&
               !PreservesInlineLineBreaksInTextSubtree(svgTextBase) &&
               (HasPositionedDescendantTextChunk(svgTextBase) ||
                (HasExplicitInlineSizeLayout(svgTextBase) && !HasActiveShapeTextLayout(svgTextBase))) &&
               !ContainsUnsupportedFlattenedTextLengthContent(svgTextBase);
    }

    private static bool HasPositionedDescendantTextChunk(SvgElement element)
    {
        foreach (var node in GetContentNodes(element))
        {
            if (node is SvgTextBase textBase)
            {
                if (StartsPositionedTextChunk(textBase))
                {
                    return true;
                }

                if (HasPositionedDescendantTextChunk(textBase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasAbsolutelyPositionedDescendantTextChunk(SvgElement element)
    {
        foreach (var node in GetContentNodes(element))
        {
            if (node is not SvgTextBase textBase)
            {
                continue;
            }

            if (textBase.X.Count > 0 || textBase.Y.Count > 0)
            {
                return true;
            }

            if (HasAbsolutelyPositionedDescendantTextChunk(textBase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsUnsupportedFlattenedTextLengthContent(SvgElement element)
    {
        foreach (var node in GetContentNodes(element))
        {
            switch (node)
            {
                case SvgTextPath:
                case SvgTextRef:
                    return true;
                case SvgTextBase textBase:
                    if (HasRotateValues(textBase) ||
                        HasNonBaselineShift(textBase) ||
                        IsVerticalWritingMode(textBase) ||
                        ContainsUnsupportedFlattenedTextLengthContent(textBase))
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    private static void ApplyInitialSequentialOffsets(SvgTextBase svgTextBase, SKRect viewport, ref float currentX, ref float currentY)
    {
        if (svgTextBase.Parent is SvgTextBase)
        {
            return;
        }

        if (svgTextBase.Dx.Count > 0)
        {
            currentX += svgTextBase.Dx[0].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, viewport);
        }

        if (svgTextBase.Dy.Count > 0)
        {
            currentY += svgTextBase.Dy[0].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, viewport);
        }
    }

    private static void ApplyInitialChildContainerOffsets(SvgTextBase svgTextBase, SKRect viewport, ISvgAssetLoader assetLoader, ref float currentX, ref float currentY)
    {
        var resetX = false;
        var resetY = false;
        if (svgTextBase.X.Count > 0)
        {
            currentX = ResolveTextUnitValue(svgTextBase.X[0], UnitRenderingType.HorizontalOffset, svgTextBase, viewport, assetLoader);
            resetX = true;
        }

        if (svgTextBase.Y.Count > 0)
        {
            currentY = ResolveTextUnitValue(svgTextBase.Y[0], UnitRenderingType.VerticalOffset, svgTextBase, viewport, assetLoader);
            resetY = true;
        }

        if (svgTextBase.Dx.Count > 0)
        {
            currentX += ResolveTextUnitValue(svgTextBase.Dx[0], UnitRenderingType.HorizontalOffset, svgTextBase, viewport, assetLoader);
        }

        if (svgTextBase.Dy.Count > 0)
        {
            currentY += ResolveTextUnitValue(svgTextBase.Dy[0], UnitRenderingType.VerticalOffset, svgTextBase, viewport, assetLoader);
        }

        ApplyBaselineShiftToResetInitialAxes(svgTextBase, viewport, assetLoader, resetX, resetY, ref currentX, ref currentY);
    }

    private static void ApplyBaselineShiftToResetInitialAxes(
        SvgTextBase svgTextBase,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        bool resetX,
        bool resetY,
        ref float currentX,
        ref float currentY)
    {
        if (!resetX && !resetY)
        {
            return;
        }

        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport, assetLoader);
        if (resetX)
        {
            currentX += baselineShift.X;
        }

        if (resetY)
        {
            currentY += baselineShift.Y;
        }
    }

    private static bool HasSequentialTextRunBarriers(SvgTextBase svgTextBase)
    {
        if (HasRotateValues(svgTextBase) || HasNonBaselineShift(svgTextBase))
        {
            return true;
        }

        var hasOwnInitialPositioning = svgTextBase.X.Count > 0 || svgTextBase.Y.Count > 0 || svgTextBase.Dx.Count > 0 || svgTextBase.Dy.Count > 0;
        if (!hasOwnInitialPositioning)
        {
            return false;
        }

        if (svgTextBase.Parent is not SvgTextBase)
        {
            return svgTextBase.X.Count > 1 ||
                   svgTextBase.Y.Count > 1 ||
                   svgTextBase.Dx.Count > 1 ||
                   svgTextBase.Dy.Count > 1;
        }

        return true;
    }

    private static bool HasPreparedSequentialTextContainerBarriers(SvgTextBase svgTextBase)
    {
        return HasSequentialTextRunBarriers(svgTextBase) ||
               ContainsPreparedSequentialTextSubtreeBarrier(svgTextBase);
    }

    private static bool ContainsPreparedSequentialTextSubtreeBarrier(SvgTextBase svgTextBase)
    {
        if (IsVerticalWritingMode(svgTextBase) ||
            HasOwnTextLengthAdjustment(svgTextBase))
        {
            return true;
        }

        foreach (var node in GetContentNodes(svgTextBase))
        {
            if (node is SvgTextBase childTextBase &&
                CanRenderTextSubtree(childTextBase) &&
                ContainsPreparedSequentialTextSubtreeBarrier(childTextBase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTextReferenceRenderingEnabled(ISvgAssetLoader assetLoader)
    {
        return assetLoader is not ISvgTextReferenceRenderingOptions { EnableTextReferences: false };
    }

    private static bool CanRenderTextSubtree(SvgElement svgElement)
    {
        return CanRenderTextSubtree(svgElement, DrawAttributes.None);
    }

    private static bool CanRenderTextSubtree(SvgElement svgElement, DrawAttributes ignoreAttributes)
    {
        return HasFeatures(svgElement, ignoreAttributes) &&
               (svgElement is not SvgVisualElement visualElement || MaskingService.CanDraw(visualElement, ignoreAttributes));
    }

    private readonly record struct ArcParameters(
        SKPoint Center,
        float Rx,
        float Ry,
        float StartAngle,
        float DeltaAngle,
        float CosPhi,
        float SinPhi);

    private enum TextPathRenderResult
    {
        NotRendered,
        Rendered,
        MissingGeometry
    }

    private static bool HasPerGlyphLayoutAdjustments(SvgTextBase svgTextBase, string? text = null)
    {
        return HasRotateValues(svgTextBase) ||
               HasInheritedRotateValues(svgTextBase) ||
               HasNonBaselineShift(svgTextBase) ||
               (text is null
                   ? HasSpacingAdjustments(svgTextBase)
                   : HasEffectiveSpacingAdjustments(svgTextBase, text)) ||
               HasOwnTextLengthAdjustment(svgTextBase);
    }

    private static bool HasInheritedRotateValues(SvgTextBase svgTextBase)
    {
        for (SvgElement? current = svgTextBase.Parent; current is not null; current = current.Parent)
        {
            if (current is SvgTextBase inheritedTextBase &&
                !string.IsNullOrWhiteSpace(inheritedTextBase.Rotate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSpacingAdjustments(SvgTextBase svgTextBase)
    {
        return HasSpacingAdjustment(svgTextBase.LetterSpacing) ||
               HasSpacingAdjustment(svgTextBase.WordSpacing);
    }

    private static bool CanNeedMixedScriptSpacingRunLayout(
        SvgTextBase svgTextBase,
        string text,
        ISvgAssetLoader assetLoader)
    {
        return !string.IsNullOrEmpty(text) &&
               HasSpacingAdjustment(svgTextBase.LetterSpacing) &&
               ContainsMixedStrongDirections(text) &&
               assetLoader is ISvgTextDirectedGlyphRunResolver;
    }

    private static bool HasEffectiveSpacingAdjustments(SvgTextBase svgTextBase, string text)
    {
        if (string.IsNullOrEmpty(text) ||
            !HasSpacingAdjustments(svgTextBase))
        {
            return false;
        }

        return HasEffectiveSpacingAdjustmentsByScalar(svgTextBase, text);
    }

    private static bool HasEffectiveSpacingAdjustmentsByScalar(SvgTextBase svgTextBase, string text)
    {
        var hasLetterSpacing = HasSpacingAdjustment(svgTextBase.LetterSpacing);
        var hasWordSpacing = HasSpacingAdjustment(svgTextBase.WordSpacing);
        if (!hasLetterSpacing && !hasWordSpacing)
        {
            return false;
        }

        var codepointCount = 0;
        var hasLetterSpacingCandidate = false;
        var hasWordSpacingCandidate = false;
        var hasCursiveCodepoint = false;
        var hasSupportedNonWhitespaceCodepoint = false;
        var hasPreviousScalar = false;
        var previousScalar = 0;
        var charIndex = 0;

        while (TryReadNextScalar(text, ref charIndex, out var scalar))
        {
            codepointCount++;
            if (hasPreviousScalar)
            {
                if (hasLetterSpacing && !IsCursiveTrackingScalar(previousScalar))
                {
                    hasLetterSpacingCandidate = true;
                }

                if (hasWordSpacing && IsWhitespaceScalar(previousScalar))
                {
                    hasWordSpacingCandidate = true;
                }
            }

            if (!IsWhitespaceScalar(scalar))
            {
                if (IsCursiveTrackingScalar(scalar))
                {
                    hasCursiveCodepoint = true;
                }
                else
                {
                    hasSupportedNonWhitespaceCodepoint = true;
                }
            }

            hasPreviousScalar = true;
            previousScalar = scalar;
        }

        if (codepointCount < 2)
        {
            return false;
        }

        var suppressesLetterSpacing = hasCursiveCodepoint && !hasSupportedNonWhitespaceCodepoint;
        return (hasLetterSpacing && !suppressesLetterSpacing && hasLetterSpacingCandidate) ||
               (hasWordSpacing && hasWordSpacingCandidate);
    }

    private static bool HasEffectiveSpacingAdjustments(SvgTextBase svgTextBase, IReadOnlyList<string> codepoints)
    {
        if (codepoints.Count < 2)
        {
            return false;
        }

        var hasLetterSpacing = HasSpacingAdjustment(svgTextBase.LetterSpacing) && !SuppressesLetterSpacingForRun(codepoints);
        var hasWordSpacing = HasSpacingAdjustment(svgTextBase.WordSpacing);
        if (!hasLetterSpacing && !hasWordSpacing)
        {
            return false;
        }

        for (var i = 0; i < codepoints.Count - 1; i++)
        {
            if (hasLetterSpacing && SupportsLetterSpacing(codepoints[i]))
            {
                return true;
            }

            if (hasWordSpacing && IsWhitespaceCodepoint(codepoints[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SuppressesLetterSpacingForRun(string text)
    {
        return !string.IsNullOrEmpty(text) && SuppressesLetterSpacingForRun(SplitCodepointsReadOnly(text));
    }

    private static bool ContainsCursiveTrackingCodepoint(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var charIndex = 0;
        while (TryReadNextScalar(text, ref charIndex, out var scalar))
        {
            if (IsCursiveTrackingScalar(scalar))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsEmojiPresentationCodepoint(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var charIndex = 0;
        while (TryReadNextScalar(text, ref charIndex, out var scalar))
        {
            if (IsEmojiPresentationScalar(scalar))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsBrowserCompatibleRunCodepoint(string text)
    {
        var charIndex = 0;
        while (TryReadNextScalar(text, ref charIndex, out var scalar))
        {
            if (IsEmojiPresentationScalar(scalar) || IsCursiveTrackingScalar(scalar))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SuppressesLetterSpacingForRun(IReadOnlyList<string> codepoints)
    {
        var hasCursiveCodepoint = false;
        var hasSupportedNonWhitespaceCodepoint = false;
        for (var i = 0; i < codepoints.Count; i++)
        {
            var codepoint = codepoints[i];
            if (string.IsNullOrEmpty(codepoint) || IsWhitespaceCodepoint(codepoint))
            {
                continue;
            }

            if (IsCursiveTrackingCodepoint(codepoint))
            {
                hasCursiveCodepoint = true;
                continue;
            }

            if (SupportsLetterSpacing(codepoint))
            {
                hasSupportedNonWhitespaceCodepoint = true;
            }
        }

        return hasCursiveCodepoint && !hasSupportedNonWhitespaceCodepoint;
    }

    private static bool HasOwnTextLengthAdjustment(SvgTextBase svgTextBase)
    {
        return svgTextBase.TryGetAttribute("textLength", out var rawTextLength) &&
               !string.IsNullOrWhiteSpace(rawTextLength);
    }

    private static bool TryGetOwnTextLength(SvgTextBase svgTextBase, SKRect viewport, bool isVertical, out float specifiedLength)
    {
        specifiedLength = 0f;
        if (!HasOwnTextLengthAdjustment(svgTextBase))
        {
            return false;
        }

        specifiedLength = svgTextBase.TextLength.ToDeviceValue(
            isVertical ? UnitRenderingType.Vertical : UnitRenderingType.Horizontal,
            svgTextBase,
            viewport);
        return specifiedLength > 0f;
    }

    private static SvgTextLengthAdjust GetOwnLengthAdjust(SvgTextBase svgTextBase)
    {
        return svgTextBase.TryGetAttribute("lengthAdjust", out var rawLengthAdjust) &&
               !string.IsNullOrWhiteSpace(rawLengthAdjust)
            ? svgTextBase.LengthAdjust
            : SvgTextLengthAdjust.Spacing;
    }

    private static bool HasSpacingAdjustment(SvgUnit spacing)
    {
        return spacing != SvgUnit.None &&
               spacing != SvgUnit.Empty &&
               spacing.Value != 0f;
    }

    private static float ResolveSpacingValue(SvgTextBase svgTextBase, SvgUnit spacing, SKRect geometryBounds, float clusterAdvance)
    {
        if (spacing == SvgUnit.None || spacing == SvgUnit.Empty)
        {
            return 0f;
        }

        if (spacing.Type == SvgUnitType.Percentage)
        {
            var basis = ResolveSpacingPercentageBasis(svgTextBase, geometryBounds, clusterAdvance);
            return basis * (spacing.Value / 100f);
        }

        return spacing.ToDeviceValue(UnitRenderingType.Horizontal, svgTextBase, geometryBounds);
    }

    private static float ResolveSpacingPercentageBasis(SvgTextBase svgTextBase, SKRect geometryBounds, float fallbackAdvance)
    {
        var metricsPaint = CreateTextMetricsPaint(svgTextBase, geometryBounds);
        return metricsPaint.TextSize > 0f
            ? metricsPaint.TextSize * 1.5f
            : Math.Max(0f, fallbackAdvance);
    }

    private static bool IsWhitespaceCodepoint(string codepoint)
    {
        return codepoint.Length > 0 && char.IsWhiteSpace(codepoint, 0);
    }

    private static bool IsTextPathWhitespace(
        bool isSimpleAsciiText,
        string text,
        IReadOnlyList<string> codepoints,
        int index)
    {
        return isSimpleAsciiText
            ? (uint)index < (uint)text.Length && char.IsWhiteSpace(text[index])
            : IsWhitespaceCodepoint(codepoints[index]);
    }

    private static bool SupportsLetterSpacing(string codepoint)
    {
        if (string.IsNullOrEmpty(codepoint))
        {
            return false;
        }

        return !IsCursiveTrackingCodepoint(codepoint);
    }

    private static bool SupportsTextPathLetterSpacing(
        bool isSimpleAsciiText,
        string text,
        IReadOnlyList<string> codepoints,
        int index)
    {
        return isSimpleAsciiText
            ? (uint)index < (uint)text.Length
            : SupportsLetterSpacing(codepoints[index]);
    }

    private static bool IsCursiveTrackingCodepoint(string codepoint)
    {
        if (string.IsNullOrEmpty(codepoint))
        {
            return false;
        }

        var scalar = char.ConvertToUtf32(codepoint, 0);
        return IsCursiveTrackingScalar(scalar);
    }

    private static bool IsCursiveTrackingScalar(int scalar)
    {
        return scalar switch
        {
            >= 0x0600 and <= 0x06FF => true, // Arabic
            >= 0x0750 and <= 0x077F => true, // Arabic Supplement
            >= 0x0870 and <= 0x089F => true, // Arabic Extended-B
            >= 0x08A0 and <= 0x08FF => true, // Arabic Extended-A
            >= 0x0700 and <= 0x074F => true, // Syriac
            >= 0x07C0 and <= 0x07FF => true, // NKo
            >= 0x0840 and <= 0x085F => true, // Mandaic
            >= 0x1800 and <= 0x18AF => true, // Mongolian
            >= 0xA840 and <= 0xA87F => true, // Phags-pa
            _ => false
        };
    }

    private static bool IsEmojiPresentationCodepoint(string codepoint)
    {
        if (string.IsNullOrEmpty(codepoint))
        {
            return false;
        }

        var scalar = char.ConvertToUtf32(codepoint, 0);
        return IsEmojiPresentationScalar(scalar);
    }

    private static bool IsEmojiPresentationScalar(int scalar)
    {
        return scalar switch
        {
            >= 0x1F000 and <= 0x1FAFF => true,
            >= 0x2600 and <= 0x27BF => true,
            _ => false
        };
    }

    private static bool TryReadNextScalar(string text, ref int charIndex, out int scalar)
    {
        if (charIndex >= text.Length)
        {
            scalar = 0;
            return false;
        }

        var start = charIndex++;
        if (charIndex < text.Length && char.IsHighSurrogate(text[start]) && char.IsLowSurrogate(text[charIndex]))
        {
            scalar = char.ConvertToUtf32(text[start], text[charIndex]);
            charIndex++;
            return true;
        }

        scalar = text[start];
        return true;
    }

    private static bool IsRightToLeftWritingModeValue(string? writingMode)
    {
        if (string.IsNullOrWhiteSpace(writingMode))
        {
            return false;
        }

        var value = writingMode.AsSpan().Trim();
        return value.Equals("rl".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
               value.Equals("rl-tb".AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidPositiveAdvance(float advance)
    {
        return !float.IsNaN(advance) && !float.IsInfinity(advance) && advance > 0f;
    }

    private static bool IsWhitespaceOnlyText(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsWhiteSpace(text, i))
            {
                return false;
            }
        }

        return text.Length > 0;
    }

    private static IReadOnlyList<string> SplitCodepointsReadOnly(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<string>();
        }

        if (s_splitCodepointCache.TryGetValue(text, out var cachedCodepoints))
        {
            return cachedCodepoints;
        }

        var codepoints = SplitCodepointsUncached(text);
        var codepointArray = codepoints.Count == 0 ? Array.Empty<string>() : codepoints.ToArray();
        s_splitCodepointCache.TryAdd(text, codepointArray);
        TrimSplitCodepointCacheIfNeeded();
        return codepointArray;
    }

    private static List<string> SplitCodepoints(string text)
    {
        var codepoints = SplitCodepointsReadOnly(text);
        return codepoints.Count == 0 ? [] : new List<string>(codepoints);
    }

    private static List<string> SplitCodepointsUncached(string text)
    {
        var codepoints = new List<string>(text.Length);
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            codepoints.Add(codepoint);
        }

        return codepoints;
    }

    private static void TrimSplitCodepointCacheIfNeeded()
    {
        if (s_splitCodepointCache.Count > SplitCodepointCacheLimit)
        {
            s_splitCodepointCache.Clear();
        }
    }

    private static float[] MeasureNaturalCodepointAdvances(
        SvgTextBase svgTextBase,
        IReadOnlyList<string> codepoints,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        return s_preparedTextEngine.MeasureNaturalCodepointAdvances(svgTextBase, codepoints, geometryBounds, assetLoader);
    }

    private static float[] MeasureNaturalCodepointAdvances(
        SvgTextBase svgTextBase,
        string text,
        IReadOnlyList<string> codepoints,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        return MeasureNaturalCodepointAdvancesCore(svgTextBase, text, codepoints, geometryBounds, assetLoader);
    }

    private static bool TryMeasureNaturalCodepointAdvancesFromSimpleShapedRun(
        SvgTextBase svgTextBase,
        string text,
        IReadOnlyList<string> codepoints,
        SKRect geometryBounds,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        bool isRightToLeft,
        bool requiresSyntheticSmallCaps,
        bool usesBrowserCompatibleRunTypeface,
        out float[] advances)
    {
        advances = Array.Empty<float>();
        if (codepoints.Count <= 1)
        {
            return false;
        }

        if (isRightToLeft ||
            ContainsMixedStrongDirections(text) ||
            requiresSyntheticSmallCaps ||
            usesBrowserCompatibleRunTypeface ||
            !IsSimpleCodepointAdvanceShapingText(codepoints))
        {
            return false;
        }

        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, text, paint, assetLoader, out _))
        {
            return false;
        }

        if (!TryCreateSingleRunShapingPaint(text, paint, assetLoader, out var shapingPaint) ||
            !TryShapeCodepointRun(text, shapingPaint, assetLoader, out var shapedRun))
        {
            return false;
        }

        if (!TryExtractSimpleShapedRunClusterAdvances(codepoints, shapedRun, out var runAdvances))
        {
            return false;
        }

        return TryCreatePrefixEquivalentSimpleRunAdvances(
            svgTextBase,
            codepoints,
            geometryBounds,
            runAdvances,
            shapedRun.Advance,
            shapingPaint,
            assetLoader,
            out advances);
    }

    private static bool TryMeasureNaturalCodepointAdvancesFromClusteredShapedRun(
        string text,
        IReadOnlyList<string> codepoints,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        bool isRightToLeft,
        bool requiresSyntheticSmallCaps,
        bool usesBrowserCompatibleRunTypeface,
        out float[] advances)
    {
        advances = Array.Empty<float>();
        if (string.IsNullOrEmpty(text) ||
            codepoints.Count <= 1 ||
            isRightToLeft ||
            requiresSyntheticSmallCaps ||
            usesBrowserCompatibleRunTypeface ||
            !MayNeedClusteredNaturalCodepointAdvances(codepoints))
        {
            return false;
        }

        if (!TryCreateSingleRunShapingPaint(text, paint, assetLoader, out var shapingPaint) ||
            !TryShapeCodepointRun(text, shapingPaint, assetLoader, out var shapedRun) ||
            !TryExtractClusteredShapedRunAdvances(text, codepoints, shapedRun, out advances))
        {
            advances = Array.Empty<float>();
            return false;
        }

        return true;
    }

    private static bool MayNeedClusteredNaturalCodepointAdvances(IReadOnlyList<string> codepoints)
    {
        for (var i = 0; i < codepoints.Count; i++)
        {
            var codepoint = codepoints[i];
            if (string.IsNullOrEmpty(codepoint))
            {
                continue;
            }

            if (codepoint.Length != 1)
            {
                return true;
            }

            var scalar = char.ConvertToUtf32(codepoint, 0);
            if (scalar > 0x024F)
            {
                return true;
            }

            var category = CharUnicodeInfo.GetUnicodeCategory(codepoint, 0);
            if (category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark
                or UnicodeCategory.Format
                or UnicodeCategory.Surrogate)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetCachedNaturalCodepointAdvances(
        NaturalCodepointAdvanceCacheKey cacheKey,
        out float[] advances)
    {
        if (s_naturalCodepointAdvanceCache.TryGetValue(cacheKey, out var cachedAdvances))
        {
            advances = cachedAdvances;
            return true;
        }

        advances = Array.Empty<float>();
        return false;
    }

    private static void CacheNaturalCodepointAdvances(
        NaturalCodepointAdvanceCacheKey cacheKey,
        float[] advances)
    {
        s_naturalCodepointAdvanceCache.TryAdd(cacheKey, advances);
        TrimNaturalCodepointAdvanceCacheIfNeeded();
    }

    private static NaturalCodepointAdvanceCacheKey CreateNaturalCodepointAdvanceCacheKey(
        ISvgAssetLoader assetLoader,
        string text,
        SKPaint paint,
        bool isRightToLeft,
        bool requiresSyntheticSmallCaps,
        bool usesBrowserCompatibleRunTypeface)
    {
        return new NaturalCodepointAdvanceCacheKey(
            RuntimeHelpers.GetHashCode(assetLoader),
            text,
            paint.TextSize,
            paint.LcdRenderText,
            paint.SubpixelText,
            paint.TextEncoding,
            paint.FontFeatureSettings,
            paint.FontKerning,
            paint.FontVariantLigatures,
            paint.Typeface?.FamilyName,
            paint.Typeface?.FontWeight ?? SKFontStyleWeight.Normal,
            paint.Typeface?.FontWidth ?? SKFontStyleWidth.Normal,
            paint.Typeface?.FontSlant ?? SKFontStyleSlant.Upright,
            isRightToLeft,
            requiresSyntheticSmallCaps,
            usesBrowserCompatibleRunTypeface);
    }

    private static bool TryCreateSingleRunShapingPaint(
        string text,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out SKPaint shapingPaint)
    {
        shapingPaint = paint.Clone();

        if (assetLoader is ISvgTextRunTypefaceResolver runTypefaceResolver)
        {
            var runTypeface = runTypefaceResolver.FindRunTypeface(text, shapingPaint);
            if (runTypeface is not null)
            {
                shapingPaint.Typeface = runTypeface;
                return true;
            }
        }

        var spans = assetLoader.FindTypefaces(text, shapingPaint);
        if (spans.Count != 1 || spans[0].Text.Length != text.Length)
        {
            return false;
        }

        if (spans[0].Typeface is { } typeface)
        {
            shapingPaint.Typeface = typeface;
        }

        return shapingPaint.Typeface is not null;
    }

    private static bool TryCreateSingleSpanShapingPaint(
        string text,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out SKPaint shapingPaint)
    {
        shapingPaint = paint;
        var spans = assetLoader.FindTypefaces(text, paint);
        if (spans.Count != 1 || spans[0].Text.Length != text.Length)
        {
            return false;
        }

        shapingPaint = paint.Clone();
        if (spans[0].Typeface is { } typeface)
        {
            shapingPaint.Typeface = typeface;
        }

        return shapingPaint.Typeface is not null;
    }

    private static bool TryShapeCodepointRun(
        string text,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out ShapedGlyphRun shapedRun)
    {
        if (assetLoader is ISvgTextDirectedGlyphRunResolver directedGlyphRunResolver)
        {
            return directedGlyphRunResolver.TryShapeGlyphRun(text, paint, rightToLeft: false, out shapedRun);
        }

        if (assetLoader is ISvgTextGlyphRunResolver glyphRunResolver)
        {
            return glyphRunResolver.TryShapeGlyphRun(text, paint, out shapedRun);
        }

        shapedRun = default;
        return false;
    }

    private static bool TryExtractSimpleShapedRunClusterAdvances(
        IReadOnlyList<string> codepoints,
        ShapedGlyphRun shapedRun,
        out float[] runAdvances)
    {
        runAdvances = Array.Empty<float>();
        if (shapedRun.Glyphs.Length == 0 ||
            shapedRun.Points.Length != shapedRun.Glyphs.Length ||
            shapedRun.Clusters.Length != shapedRun.Glyphs.Length)
        {
            return false;
        }

        var codepointCharOffsets = new int[codepoints.Count + 1];
        var charIndex = 0;
        for (var i = 0; i < codepoints.Count; i++)
        {
            codepointCharOffsets[i] = charIndex;
            charIndex += codepoints[i].Length;
        }

        codepointCharOffsets[codepoints.Count] = charIndex;

        runAdvances = new float[codepoints.Count];
        var glyphIndex = 0;
        for (var codepointIndex = 0; codepointIndex < codepoints.Count; codepointIndex++)
        {
            if (glyphIndex >= shapedRun.Glyphs.Length)
            {
                runAdvances = Array.Empty<float>();
                return false;
            }

            var clusterStart = shapedRun.Clusters[glyphIndex];
            if (clusterStart != codepointCharOffsets[codepointIndex])
            {
                runAdvances = Array.Empty<float>();
                return false;
            }

            var clusterPointX = shapedRun.Points[glyphIndex].X;
            glyphIndex++;
            while (glyphIndex < shapedRun.Glyphs.Length && shapedRun.Clusters[glyphIndex] == clusterStart)
            {
                glyphIndex++;
            }

            var nextPointX = glyphIndex < shapedRun.Points.Length
                ? shapedRun.Points[glyphIndex].X
                : shapedRun.Advance;
            var advance = Math.Max(0f, nextPointX - clusterPointX);
            runAdvances[codepointIndex] = advance;
        }

        if (glyphIndex != shapedRun.Glyphs.Length)
        {
            runAdvances = Array.Empty<float>();
            return false;
        }

        return true;
    }

    private static bool TryExtractClusteredShapedRunAdvances(
        string text,
        IReadOnlyList<string> codepoints,
        ShapedGlyphRun shapedRun,
        out float[] advances)
    {
        advances = Array.Empty<float>();
        if (shapedRun.Glyphs.Length == 0 ||
            shapedRun.Points.Length != shapedRun.Glyphs.Length ||
            shapedRun.Clusters.Length != shapedRun.Glyphs.Length)
        {
            return false;
        }

        var resolver = SvgDefaultTextBoundaryResolver.Instance;
        var clusterStarts = new SortedSet<int>(resolver.GetGraphemeClusterStartCharIndexes(text));
        clusterStarts.Add(text.Length);
        var codepointCharOffsets = new int[codepoints.Count + 1];
        var charIndex = 0;
        for (var i = 0; i < codepoints.Count; i++)
        {
            codepointCharOffsets[i] = charIndex;
            charIndex += codepoints[i].Length;
        }

        codepointCharOffsets[codepoints.Count] = charIndex;
        if (charIndex != text.Length)
        {
            return false;
        }

        advances = new float[codepoints.Count];
        var glyphIndex = 0;
        var emittedCluster = false;
        var hasNonTrivialCluster = false;
        for (var codepointIndex = 0; codepointIndex < codepoints.Count; codepointIndex++)
        {
            var clusterStart = codepointCharOffsets[codepointIndex];
            if (!clusterStarts.Contains(clusterStart))
            {
                hasNonTrivialCluster = true;
                continue;
            }

            var view = clusterStarts.GetViewBetween(clusterStart + 1, text.Length);
            if (view.Count == 0)
            {
                return false;
            }

            var clusterEnd = view.Min;
            if (!TryFindShapedClusterAdvance(shapedRun, clusterStart, clusterEnd, ref glyphIndex, out var clusterAdvance))
            {
                return false;
            }

            if (IsValidPositiveAdvance(clusterAdvance))
            {
                advances[codepointIndex] = clusterAdvance;
                emittedCluster = true;
            }

            var nextCodepointEnd = codepointCharOffsets[Math.Min(codepoints.Count, codepointIndex + 1)];
            if (clusterEnd > nextCodepointEnd || clusterAdvance <= 0f)
            {
                hasNonTrivialCluster = true;
            }
        }

        return emittedCluster && hasNonTrivialCluster;
    }

    private static bool TryFindShapedClusterAdvance(
        ShapedGlyphRun shapedRun,
        int clusterStart,
        int clusterEnd,
        ref int glyphIndex,
        out float advance)
    {
        advance = 0f;
        while (glyphIndex < shapedRun.Clusters.Length && shapedRun.Clusters[glyphIndex] < clusterStart)
        {
            glyphIndex++;
        }

        if (glyphIndex >= shapedRun.Clusters.Length || shapedRun.Clusters[glyphIndex] >= clusterEnd)
        {
            return true;
        }

        var firstGlyphIndex = glyphIndex;
        var clusterPointX = shapedRun.Points[firstGlyphIndex].X;
        while (glyphIndex < shapedRun.Clusters.Length &&
               shapedRun.Clusters[glyphIndex] >= clusterStart &&
               shapedRun.Clusters[glyphIndex] < clusterEnd)
        {
            glyphIndex++;
        }

        var nextPointX = glyphIndex < shapedRun.Points.Length
            ? shapedRun.Points[glyphIndex].X
            : shapedRun.Advance;
        advance = Math.Max(0f, nextPointX - clusterPointX);
        return true;
    }

    private static bool TryCreatePrefixEquivalentSimpleRunAdvances(
        SvgTextBase svgTextBase,
        IReadOnlyList<string> codepoints,
        SKRect geometryBounds,
        IReadOnlyList<float> runAdvances,
        float totalAdvance,
        SKPaint shapingPaint,
        ISvgAssetLoader assetLoader,
        out float[] advances)
    {
        advances = Array.Empty<float>();
        if (codepoints.Count == 0 || runAdvances.Count != codepoints.Count)
        {
            return false;
        }

        var cache = new Dictionary<string, float>(StringComparer.Ordinal);
        var prefixEquivalentAdvances = new float[codepoints.Count];
        var accumulatedAdvance = 0f;
        var previousRunDelta = 0f;

        for (var i = 0; i < codepoints.Count; i++)
        {
            if (!TryMeasureSimpleRunCodepointAdvance(codepoints[i], shapingPaint, assetLoader, cache, out var isolatedAdvance))
            {
                return false;
            }

            var prefixEquivalentAdvance = isolatedAdvance + previousRunDelta;
            if (!IsValidPositiveAdvance(prefixEquivalentAdvance))
            {
                prefixEquivalentAdvance = 0f;
            }

            prefixEquivalentAdvances[i] = prefixEquivalentAdvance;
            accumulatedAdvance += prefixEquivalentAdvance;
            previousRunDelta = runAdvances[i] - isolatedAdvance;
        }

        const float epsilon = 0.01f;
        if (Math.Abs(previousRunDelta) > epsilon || Math.Abs(accumulatedAdvance - totalAdvance) > epsilon)
        {
            return false;
        }

        if (!HasMatchingSampledPrefixMeasurements(svgTextBase, codepoints, geometryBounds, assetLoader, prefixEquivalentAdvances))
        {
            return false;
        }

        advances = prefixEquivalentAdvances;
        return true;
    }

    private static bool HasMatchingSampledPrefixMeasurements(
        SvgTextBase svgTextBase,
        IReadOnlyList<string> codepoints,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        IReadOnlyList<float> advances)
    {
        if (codepoints.Count == 0 || advances.Count != codepoints.Count)
        {
            return false;
        }

        var sampleIndices = GetPrefixValidationSampleIndices(codepoints);
        if (sampleIndices.Count == 0)
        {
            return false;
        }

        const float epsilon = 0.05f;
        var builder = new StringBuilder();
        var accumulatedAdvance = 0f;
        var sampleCursor = 0;

        for (var i = 0; i < codepoints.Count; i++)
        {
            builder.Append(codepoints[i]);
            accumulatedAdvance += advances[i];

            if (sampleCursor >= sampleIndices.Count || i != sampleIndices[sampleCursor])
            {
                continue;
            }

            var measuredAdvance = MeasureNaturalTextAdvance(svgTextBase, builder.ToString(), geometryBounds, assetLoader);
            if (Math.Abs(measuredAdvance - accumulatedAdvance) > epsilon)
            {
                return false;
            }

            sampleCursor++;
        }

        return sampleCursor == sampleIndices.Count;
    }

    private static List<int> GetPrefixValidationSampleIndices(IReadOnlyList<string> codepoints)
    {
        var count = codepoints.Count;
        if (count == 0)
        {
            return [];
        }

        if (count <= 8)
        {
            var allIndices = new List<int>(count);
            for (var i = 0; i < count; i++)
            {
                allIndices.Add(i);
            }

            return allIndices;
        }

        var samples = new SortedSet<int>
        {
            0,
            1,
            count / 2,
            count - 2,
            count - 1
        };

        for (var i = 0; i < count; i++)
        {
            if (IsWhitespaceCodepoint(codepoints[i]))
            {
                samples.Add(i);
                break;
            }
        }

        for (var i = count - 1; i >= 0; i--)
        {
            if (IsWhitespaceCodepoint(codepoints[i]))
            {
                samples.Add(i);
                break;
            }
        }

        return [.. samples];
    }

    private static bool TryMeasureSimpleRunCodepointAdvance(
        string codepoint,
        SKPaint shapingPaint,
        ISvgAssetLoader assetLoader,
        IDictionary<string, float> cache,
        out float advance)
    {
        if (cache.TryGetValue(codepoint, out advance))
        {
            return true;
        }

        var cacheKey = CreateSimpleCodepointAdvanceCacheKey(assetLoader, codepoint, shapingPaint);
        if (s_simpleCodepointAdvanceCache.TryGetValue(cacheKey, out advance))
        {
            cache[codepoint] = advance;
            return true;
        }

        var bounds = new SKRect();
        advance = assetLoader.MeasureText(codepoint, shapingPaint, ref bounds);
        advance = EnsureWhitespaceAdvance(codepoint, shapingPaint, assetLoader, advance);
        if (!IsValidPositiveAdvance(advance))
        {
            advance = 0f;
        }

        cache[codepoint] = advance;
        s_simpleCodepointAdvanceCache.TryAdd(cacheKey, advance);
        TrimSimpleCodepointAdvanceCacheIfNeeded();
        return true;
    }

    private static SimpleCodepointAdvanceCacheKey CreateSimpleCodepointAdvanceCacheKey(
        ISvgAssetLoader assetLoader,
        string codepoint,
        SKPaint shapingPaint)
    {
        return new SimpleCodepointAdvanceCacheKey(
            RuntimeHelpers.GetHashCode(assetLoader),
            codepoint,
            shapingPaint.TextSize,
            shapingPaint.LcdRenderText,
            shapingPaint.SubpixelText,
            shapingPaint.TextEncoding,
            shapingPaint.FontFeatureSettings,
            shapingPaint.FontKerning,
            shapingPaint.FontVariantLigatures,
            shapingPaint.Typeface?.FamilyName,
            shapingPaint.Typeface?.FontWeight ?? SKFontStyleWeight.Normal,
            shapingPaint.Typeface?.FontWidth ?? SKFontStyleWidth.Normal,
            shapingPaint.Typeface?.FontSlant ?? SKFontStyleSlant.Upright);
    }

    private static void TrimSimpleCodepointAdvanceCacheIfNeeded()
    {
        if (s_simpleCodepointAdvanceCache.Count > SimpleCodepointAdvanceCacheLimit)
        {
            s_simpleCodepointAdvanceCache.Clear();
        }
    }

    private static void TrimNaturalCodepointAdvanceCacheIfNeeded()
    {
        if (s_naturalCodepointAdvanceCache.Count > NaturalCodepointAdvanceCacheLimit)
        {
            s_naturalCodepointAdvanceCache.Clear();
        }
    }

    private static bool IsSimpleCodepointAdvanceShapingText(IReadOnlyList<string> codepoints)
    {
        for (var i = 0; i < codepoints.Count; i++)
        {
            if (!IsSimpleCodepointAdvanceShapingCodepoint(codepoints[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSimpleCodepointAdvanceShapingCodepoint(string codepoint)
    {
        if (string.IsNullOrEmpty(codepoint))
        {
            return false;
        }

        var scalar = char.ConvertToUtf32(codepoint, 0);
        if (char.IsWhiteSpace(codepoint, 0))
        {
            return true;
        }

        if (scalar > 0x024F)
        {
            return false;
        }

        if (char.IsControl(codepoint, 0))
        {
            return false;
        }

        return CharUnicodeInfo.GetUnicodeCategory(codepoint, 0) is not UnicodeCategory.NonSpacingMark
            and not UnicodeCategory.SpacingCombiningMark
            and not UnicodeCategory.EnclosingMark
            and not UnicodeCategory.Format
            and not UnicodeCategory.Surrogate
            and not UnicodeCategory.OtherNotAssigned;
    }

    private static float MeasureContextualWhitespaceAdvance(
        SvgTextBase svgTextBase,
        string prefixText,
        string whitespaceCodepoint,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        const string sentinel = "x";
        var withWhitespace = prefixText + whitespaceCodepoint + sentinel;
        var withoutWhitespace = prefixText + sentinel;
        var withWhitespaceAdvance = MeasureNaturalTextAdvance(svgTextBase, withWhitespace, geometryBounds, assetLoader);
        var withoutWhitespaceAdvance = MeasureNaturalTextAdvance(svgTextBase, withoutWhitespace, geometryBounds, assetLoader);
        return withWhitespaceAdvance - withoutWhitespaceAdvance;
    }

    private static float GetPositionedDecorationsAdvance(
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement[] placements,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        if (placements.Length == 0)
        {
            return 0f;
        }

        var codepoints = SplitCodepointsReadOnly(text);
        if (codepoints.Count == 0 || codepoints.Count != placements.Length)
        {
            return 0f;
        }

        var advances = MeasureNaturalCodepointAdvances(svgTextBase, text, codepoints, geometryBounds, assetLoader);
        var lastIndex = advances.Length - 1;
        var start = TransformDecorationPoint(placements[0], 0f, 0f);
        var end = TransformDecorationPoint(placements[lastIndex], advances[lastIndex], 0f);
        if (placements[0].RotationDegrees == 0f && placements[lastIndex].RotationDegrees == 0f)
        {
            return Math.Max(0f, end.X - start.X);
        }

        var totalAdvance = 0f;
        for (var i = 0; i < placements.Length - 1; i++)
        {
            totalAdvance += Math.Max(0f, placements[i + 1].Point.X - placements[i].Point.X);
        }

        return totalAdvance + Math.Max(0f, advances[lastIndex] * placements[lastIndex].ScaleX);
    }

    private static float EnsureWhitespaceAdvance(string text, SKPaint paint, ISvgAssetLoader assetLoader, float candidateAdvance)
    {
        if (!IsWhitespaceOnlyText(text))
        {
            return candidateAdvance;
        }

        var minimumReasonableAdvance = Math.Max(1f, paint.TextSize * 0.2f);
        if (candidateAdvance >= minimumReasonableAdvance)
        {
            return candidateAdvance;
        }

        var bounds = new SKRect();
        var sentinelAdvance = assetLoader.MeasureText("x" + text + "x", paint, ref bounds);
        bounds = new SKRect();
        var baselineAdvance = assetLoader.MeasureText("xx", paint, ref bounds);
        return Math.Max(candidateAdvance, sentinelAdvance - baselineAdvance);
    }

    private static bool TryCreateAlignedCodepointPlacements(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect geometryBounds,
        SKTextAlign textAlign,
        ISvgAssetLoader assetLoader,
        float[]? explicitRotations,
        out PositionedCodepointPlacement[] placements,
        out float totalAdvance)
    {
        return TryCreateAlignedCodepointPlacements(
            svgTextBase,
            text,
            anchorX,
            anchorY,
            geometryBounds,
            textAlign,
            assetLoader,
            explicitRotations,
            out placements,
            out totalAdvance,
            out _,
            out _);
    }

    private static bool TryCreateAlignedCodepointPlacements(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect geometryBounds,
        SKTextAlign textAlign,
        ISvgAssetLoader assetLoader,
        float[]? explicitRotations,
        out PositionedCodepointPlacement[] placements,
        out float totalAdvance,
        out IReadOnlyList<string>? codepoints,
        out float[]? naturalAdvances)
    {
        placements = Array.Empty<PositionedCodepointPlacement>();
        totalAdvance = 0f;
        codepoints = null;
        naturalAdvances = null;
        var isVertical = IsVerticalWritingMode(svgTextBase);
        var hasExplicitRotationValues = explicitRotations is { Length: > 0 };

        if (string.IsNullOrEmpty(text) || (!hasExplicitRotationValues && !HasPerGlyphLayoutAdjustments(svgTextBase, text) && !isVertical))
        {
            return false;
        }

        var resolvedCodepoints = SplitCodepointsReadOnly(text);
        if (resolvedCodepoints.Count == 0)
        {
            return false;
        }

        codepoints = resolvedCodepoints;
        var hasEffectiveSpacingAdjustments = HasEffectiveSpacingAdjustments(svgTextBase, resolvedCodepoints);

        naturalAdvances = MeasureNaturalCodepointAdvances(svgTextBase, text, resolvedCodepoints, geometryBounds, assetLoader);
        var letterSpacingUnit = svgTextBase.LetterSpacing;
        var wordSpacingUnit = svgTextBase.WordSpacing;
        var hasLetterSpacingAdjustment = HasSpacingAdjustment(letterSpacingUnit) && !SuppressesLetterSpacingForRun(resolvedCodepoints);
        var hasWordSpacingAdjustment = HasSpacingAdjustment(wordSpacingUnit);
        var letterSpacingIsPercentage = hasLetterSpacingAdjustment && letterSpacingUnit.Type == SvgUnitType.Percentage;
        var wordSpacingIsPercentage = hasWordSpacingAdjustment && wordSpacingUnit.Type == SvgUnitType.Percentage;
        var fixedLetterSpacing = hasLetterSpacingAdjustment && !letterSpacingIsPercentage
            ? letterSpacingUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgTextBase, geometryBounds)
            : 0f;
        var fixedWordSpacing = hasWordSpacingAdjustment && !wordSpacingIsPercentage
            ? wordSpacingUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgTextBase, geometryBounds)
            : 0f;
        var naturalLength = 0f;
        for (var i = 0; i < resolvedCodepoints.Count; i++)
        {
            naturalLength += naturalAdvances[i];
            if (i < resolvedCodepoints.Count - 1)
            {
                if (hasLetterSpacingAdjustment && SupportsLetterSpacing(resolvedCodepoints[i]))
                {
                    naturalLength += letterSpacingIsPercentage
                        ? ResolveSpacingValue(svgTextBase, letterSpacingUnit, geometryBounds, naturalAdvances[i])
                        : fixedLetterSpacing;
                }

                if (hasWordSpacingAdjustment && IsWhitespaceCodepoint(resolvedCodepoints[i]))
                {
                    naturalLength += wordSpacingIsPercentage
                        ? ResolveSpacingValue(svgTextBase, wordSpacingUnit, geometryBounds, naturalAdvances[i])
                        : fixedWordSpacing;
                }
            }
        }

        var specifiedLength = TryGetOwnTextLength(svgTextBase, geometryBounds, isVertical, out var ownSpecifiedLength)
            ? ownSpecifiedLength
            : 0f;
        var hasActiveTextLengthAdjustment = specifiedLength > 0f &&
                                            Math.Abs(naturalLength - specifiedLength) > TextLengthTolerance;
        if (!hasExplicitRotationValues &&
            !hasEffectiveSpacingAdjustments &&
            !hasActiveTextLengthAdjustment &&
            !isVertical)
        {
            return false;
        }

        var glyphScaleX = 1f;
        var extraGapAdvance = 0f;
        var scaleRunFromStart = false;
        totalAdvance = naturalLength;

        if (hasActiveTextLengthAdjustment)
        {
            if (GetOwnLengthAdjust(svgTextBase) == SvgTextLengthAdjust.Spacing && resolvedCodepoints.Count > 1)
            {
                extraGapAdvance = (specifiedLength - totalAdvance) / (resolvedCodepoints.Count - 1);
                totalAdvance = specifiedLength;
            }
            else if (totalAdvance > 0f)
            {
                glyphScaleX = specifiedLength / totalAdvance;
                scaleRunFromStart = true;
                totalAdvance = specifiedLength;
            }
        }

        var rotations = explicitRotations is not null ? explicitRotations : GetPositionedRotations(svgTextBase, resolvedCodepoints.Count);
        var currentX = anchorX;
        var currentY = anchorY;
        if (isVertical)
        {
            currentY = GetVerticalInlineStartCoordinate(svgTextBase, anchorY, totalAdvance, textAlign);
        }
        else
        {
            currentX = GetAlignedStartCoordinate(anchorX, totalAdvance, textAlign);
        }

        var scaleOriginX = currentX;
        placements = new PositionedCodepointPlacement[resolvedCodepoints.Count];
        for (var i = 0; i < resolvedCodepoints.Count; i++)
        {
            placements[i] = new PositionedCodepointPlacement(
                new SKPoint(currentX, currentY),
                GetCodepointRotationDegrees(svgTextBase, resolvedCodepoints[i], rotations, i),
                glyphScaleX,
                scaleRunFromStart ? scaleOriginX : currentX);

            if (i >= resolvedCodepoints.Count - 1)
            {
                continue;
            }

            var clusterAdvance = naturalAdvances[i];
            if (hasLetterSpacingAdjustment && SupportsLetterSpacing(resolvedCodepoints[i]))
            {
                clusterAdvance += letterSpacingIsPercentage
                    ? ResolveSpacingValue(svgTextBase, letterSpacingUnit, geometryBounds, naturalAdvances[i])
                    : fixedLetterSpacing;
                if (!IsValidPositiveAdvance(clusterAdvance))
                {
                    clusterAdvance = 0f;
                }
            }

            if (hasWordSpacingAdjustment && IsWhitespaceCodepoint(resolvedCodepoints[i]))
            {
                clusterAdvance += wordSpacingIsPercentage
                    ? ResolveSpacingValue(svgTextBase, wordSpacingUnit, geometryBounds, naturalAdvances[i])
                    : fixedWordSpacing;
            }

            if (!scaleRunFromStart)
            {
                clusterAdvance += extraGapAdvance;
            }

            ApplyInlineAdvance(svgTextBase, ref currentX, ref currentY, clusterAdvance);
        }

        return true;
    }

    private static float GetVerticalInlineStartCoordinate(
        SvgTextBase svgTextBase,
        float anchorY,
        float totalAdvance,
        SKTextAlign textAlign)
    {
        if (GetInlineAdvanceDirection(svgTextBase) > 0)
        {
            return GetAlignedStartCoordinate(anchorY, totalAdvance, textAlign);
        }

        return textAlign switch
        {
            SKTextAlign.Center => anchorY + (totalAdvance * 0.5f),
            SKTextAlign.Left => anchorY + totalAdvance,
            _ => anchorY
        };
    }

    private static float MeasureSequentialTextRuns(
        IReadOnlyList<SequentialTextRun> runs,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        if (TryPrepareSequentialTextRuns(runs, geometryBounds, assetLoader, out var preparedText) &&
            preparedText is not null)
        {
            return preparedText.TotalAdvance;
        }

        var totalAdvance = 0f;
        for (var i = 0; i < runs.Count; i++)
        {
            totalAdvance += s_preparedTextEngine.MeasureAdvance(runs[i].StyleSource, runs[i].Text, geometryBounds, assetLoader);
        }

        return totalAdvance;
    }

    private static float MeasureTextAdvance(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        return s_preparedTextEngine.MeasureAdvance(svgTextBase, text, geometryBounds, assetLoader);
    }

    private static float MeasureTextAdvanceCached(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        IDictionary<TextRunAdvanceCacheKey, float>? cache)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        if (cache is null)
        {
            return MeasureTextAdvance(svgTextBase, text, geometryBounds, assetLoader);
        }

        var cacheKey = new TextRunAdvanceCacheKey(svgTextBase, text);
        if (cache.TryGetValue(cacheKey, out var cachedAdvance))
        {
            return cachedAdvance;
        }

        var advance = MeasureTextAdvance(svgTextBase, text, geometryBounds, assetLoader);
        cache[cacheKey] = advance;
        return advance;
    }

    private static float MeasureNaturalTextAdvance(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        return s_preparedTextEngine.MeasureNaturalWidth(svgTextBase, text, geometryBounds, assetLoader);
    }

    private static float MeasureNaturalTextAdvanceHorizontal(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var isRightToLeft = IsRightToLeft(svgTextBase);
        var requiresSyntheticSmallCaps = RequiresSyntheticSmallCaps(svgTextBase, text);
        var usesBrowserCompatibleRunTypeface = ShouldUseBrowserCompatibleRunTypeface(svgTextBase, text);
        var hasSimpleCacheKey = TryCreateSimpleNaturalTextAdvanceCacheKey(
            svgTextBase,
            assetLoader,
            text,
            geometryBounds,
            isRightToLeft,
            requiresSyntheticSmallCaps,
            usesBrowserCompatibleRunTypeface,
            out var simpleCacheKey);
        if (hasSimpleCacheKey &&
            TryGetCachedSimpleNaturalTextAdvance(simpleCacheKey, out var cachedSimpleAdvance))
        {
            return cachedSimpleAdvance;
        }

        var paint = CreateTextMetricsPaint(svgTextBase, geometryBounds);
        var cacheKey = CreateNaturalTextAdvanceCacheKey(
            svgTextBase,
            assetLoader,
            text,
            paint,
            isRightToLeft,
            requiresSyntheticSmallCaps,
            usesBrowserCompatibleRunTypeface);
        if (TryGetCachedNaturalTextAdvance(cacheKey, out var cachedAdvance))
        {
            if (hasSimpleCacheKey)
            {
                CacheSimpleNaturalTextAdvance(simpleCacheKey, cachedAdvance);
            }

            return cachedAdvance;
        }

        var advance = MeasureNaturalTextAdvanceHorizontalUncached(svgTextBase, text, paint, assetLoader);
        CacheNaturalTextAdvance(cacheKey, advance);
        if (hasSimpleCacheKey)
        {
            CacheSimpleNaturalTextAdvance(simpleCacheKey, advance);
        }

        return advance;
    }

    private static bool TryGetCachedSimpleNaturalTextAdvance(
        SimpleNaturalTextAdvanceCacheKey cacheKey,
        out float advance)
    {
        return s_simpleNaturalTextAdvanceCache.TryGetValue(cacheKey, out advance);
    }

    private static void CacheSimpleNaturalTextAdvance(
        SimpleNaturalTextAdvanceCacheKey cacheKey,
        float advance)
    {
        s_simpleNaturalTextAdvanceCache.TryAdd(cacheKey, advance);
        TrimSimpleNaturalTextAdvanceCacheIfNeeded();
    }

    private static bool TryCreateSimpleNaturalTextAdvanceCacheKey(
        SvgTextBase svgTextBase,
        ISvgAssetLoader assetLoader,
        string text,
        SKRect geometryBounds,
        bool isRightToLeft,
        bool requiresSyntheticSmallCaps,
        bool usesBrowserCompatibleRunTypeface,
        out SimpleNaturalTextAdvanceCacheKey cacheKey)
    {
        cacheKey = default;
        if (svgTextBase is SvgAltGlyph ||
            HasCustomTextOpenTypePaintProperty(svgTextBase) ||
            !TryResolveSimpleTextMetricsFontSize(svgTextBase, geometryBounds, out var textSize))
        {
            return false;
        }

        var ownerDocument = svgTextBase.OwnerDocument;
        var resolvedWeight = PaintingService.ResolveFontWeight(svgTextBase, svgTextBase.FontWeight);
        cacheKey = new SimpleNaturalTextAdvanceCacheKey(
            RuntimeHelpers.GetHashCode(assetLoader),
            ownerDocument is null ? 0 : RuntimeHelpers.GetHashCode(ownerDocument),
            text,
            svgTextBase.FontFamily,
            svgTextBase.FontStyle,
            svgTextBase.FontVariant,
            svgTextBase.FontWeight,
            SvgTextBidiResolver.ResolveDirection(svgTextBase),
            SvgTextBidiResolver.ResolveUnicodeBidi(svgTextBase),
            GetNaturalTextAdvanceLanguage(svgTextBase),
            assetLoader.EnableSvgFonts,
            textSize,
            PaintingService.ToFontStyleWeight(resolvedWeight),
            PaintingService.ToFontStyleWidth(svgTextBase.FontStretch),
            PaintingService.ToFontStyleSlant(svgTextBase.FontStyle),
            isRightToLeft,
            requiresSyntheticSmallCaps,
            usesBrowserCompatibleRunTypeface);
        return true;
    }

    private static bool HasCustomTextOpenTypePaintProperty(SvgElement element)
    {
        for (SvgElement? current = element; current is not null; current = current.Parent)
        {
            if (HasCustomTextOpenTypePaintProperty(current, "font-feature-settings", "normal") ||
                HasCustomTextOpenTypePaintProperty(current, "font-kerning", "auto") ||
                HasCustomTextOpenTypePaintProperty(current, "font-variant-ligatures", "normal"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCustomTextOpenTypePaintProperty(
        SvgElement element,
        string propertyName,
        string defaultValue)
    {
        if (!element.ComputedStyle.TryGetPropertyValue(propertyName, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.AsSpan().Trim();
        return !trimmed.Equals("inherit".AsSpan(), StringComparison.OrdinalIgnoreCase) &&
               !trimmed.Equals("unset".AsSpan(), StringComparison.OrdinalIgnoreCase) &&
               !trimmed.Equals("initial".AsSpan(), StringComparison.OrdinalIgnoreCase) &&
               !trimmed.Equals(defaultValue.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveSimpleTextMetricsFontSize(
        SvgElement element,
        SKRect geometryBounds,
        out float textSize)
    {
        const int maxFontSizeInheritanceDepth = 256;
        var depth = 0;
        for (SvgElement? current = element; current is not null; current = current.Parent)
        {
            if (depth++ > maxFontSizeInheritanceDepth)
            {
                textSize = 12f;
                return true;
            }

            var status = TryResolveSimpleSpecifiedFontSize(current, geometryBounds, out textSize);
            if (status == SimpleFontSizeResolutionStatus.Resolved)
            {
                return true;
            }

            if (status == SimpleFontSizeResolutionStatus.Unsupported)
            {
                return false;
            }
        }

        textSize = 12f;
        return true;
    }

    private static SimpleFontSizeResolutionStatus TryResolveSimpleSpecifiedFontSize(
        SvgElement element,
        SKRect geometryBounds,
        out float textSize)
    {
        if (element is SvgTextBase textBase)
        {
            var fontSize = textBase.FontSize;
            if (fontSize != SvgUnit.None &&
                fontSize != SvgUnit.Empty)
            {
                return TryResolveSimpleFontSizeUnit(fontSize, element, geometryBounds, out textSize);
            }
        }

        if (element.ComputedStyle.TryGetPropertyValue("font-size", out var rawFontSize) &&
            !string.IsNullOrWhiteSpace(rawFontSize))
        {
            try
            {
                var fontSize = SvgUnitConverter.Parse(rawFontSize.AsSpan().Trim());
                if (fontSize != SvgUnit.None &&
                    fontSize != SvgUnit.Empty)
                {
                    return TryResolveSimpleFontSizeUnit(fontSize, element, geometryBounds, out textSize);
                }
            }
            catch (FormatException)
            {
                textSize = 0f;
                return SimpleFontSizeResolutionStatus.Unsupported;
            }
        }

        textSize = 0f;
        return SimpleFontSizeResolutionStatus.NotSpecified;
    }

    private static SimpleFontSizeResolutionStatus TryResolveSimpleFontSizeUnit(
        SvgUnit fontSize,
        SvgElement element,
        SKRect geometryBounds,
        out float textSize)
    {
        if (fontSize.Type is SvgUnitType.Percentage or SvgUnitType.Em or SvgUnitType.Ex)
        {
            textSize = 0f;
            return SimpleFontSizeResolutionStatus.Unsupported;
        }

        textSize = fontSize.ToDeviceValue(UnitRenderingType.Vertical, element, geometryBounds);
        return SimpleFontSizeResolutionStatus.Resolved;
    }

    private static void TrimSimpleNaturalTextAdvanceCacheIfNeeded()
    {
        if (s_simpleNaturalTextAdvanceCache.Count > SimpleNaturalTextAdvanceCacheLimit)
        {
            s_simpleNaturalTextAdvanceCache.Clear();
        }
    }

    private enum SimpleFontSizeResolutionStatus
    {
        NotSpecified,
        Resolved,
        Unsupported
    }

    private static float MeasureNaturalTextAdvanceHorizontalUncached(
        SvgTextBase svgTextBase,
        string text,
        SKPaint paint,
        ISvgAssetLoader assetLoader)
    {
        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, text, paint, assetLoader, out var svgFontLayout) &&
            svgFontLayout is not null)
        {
            return EnsureWhitespaceAdvance(text, paint, assetLoader, svgFontLayout.Advance);
        }

        if (RequiresSyntheticSmallCaps(svgTextBase, text))
        {
            return MeasureSyntheticSmallCapsAdvance(svgTextBase, text, paint, assetLoader);
        }

        var fallbackText = GetBrowserCompatibleFallbackText(svgTextBase, text, assetLoader);
        if (TryCreateBrowserCompatibleFullRunPaint(svgTextBase, fallbackText, paint, assetLoader, out var fullRunPaint, out var shapedText))
        {
            var fullRunMeasureBounds = new SKRect();
            return EnsureWhitespaceAdvance(
                fallbackText,
                fullRunPaint,
                assetLoader,
                assetLoader.MeasureText(shapedText, fullRunPaint, ref fullRunMeasureBounds));
        }

        var spans = assetLoader.FindTypefaces(fallbackText, paint);
        if (spans.Count > 0)
        {
            var totalAdvance = 0f;
            for (var i = 0; i < spans.Count; i++)
            {
                totalAdvance += spans[i].Advance;
            }

            return EnsureWhitespaceAdvance(fallbackText, paint, assetLoader, totalAdvance);
        }

        var bounds = new SKRect();
        return EnsureWhitespaceAdvance(fallbackText, paint, assetLoader, assetLoader.MeasureText(fallbackText, paint, ref bounds));
    }

    private static float ApplyTextAnchor(SvgTextBase svgTextBase, float anchorCoordinate, SKRect geometryBounds, float totalAdvance)
    {
        return GetAlignedStartCoordinate(anchorCoordinate, totalAdvance, GetTextAnchorAlign(svgTextBase, geometryBounds));
    }

    private static float ApplyTextPathSideOffset(SvgTextPath svgTextPath, float hOffset, float pathLength, float totalAdvance)
    {
        if (svgTextPath.Side != SvgTextPathSide.Right || pathLength <= 0f)
        {
            return hOffset;
        }

        return pathLength - hOffset - totalAdvance;
    }

    private static SKTextAlign GetTextAnchorAlign(SvgTextBase svgTextBase, SKRect geometryBounds)
    {
        if (IsVerticalWritingMode(svgTextBase))
        {
            var isRightToLeft = IsRightToLeft(svgTextBase);
            return svgTextBase.TextAnchor switch
            {
                SvgTextAnchor.Middle => SKTextAlign.Center,
                SvgTextAnchor.End => isRightToLeft ? SKTextAlign.Left : SKTextAlign.Right,
                _ => isRightToLeft ? SKTextAlign.Right : SKTextAlign.Left
            };
        }

        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        return paint.TextAlign;
    }

    private static bool TryCreateBrowserCompatibleFullRunPaint(
        SvgTextBase svgTextBase,
        string text,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out SKPaint runPaint,
        out string shapedText)
    {
        runPaint = paint.Clone();
        shapedText = text;

        if (string.IsNullOrEmpty(text) ||
            assetLoader is not ISvgTextRunTypefaceResolver resolver ||
            !ShouldUseBrowserCompatibleRunTypeface(svgTextBase, text))
        {
            return false;
        }

        var runTypeface = resolver.FindRunTypeface(text, runPaint);
        if (runTypeface is null)
        {
            return false;
        }

        runPaint.Typeface = runTypeface;
        shapedText = ApplyBrowserCompatibleBidiControls(svgTextBase, text);
        return !string.IsNullOrEmpty(shapedText);
    }

    private static bool ShouldUseBrowserCompatibleRunTypeface(SvgTextBase svgTextBase, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return SvgTextBidiResolver.ResolveDirection(svgTextBase) == SvgTextDirection.RightToLeft ||
               SvgTextBidiResolver.ResolveUnicodeBidi(svgTextBase) != SvgUnicodeBidiMode.Normal ||
               ContainsBrowserCompatibleRunCodepoint(text);
    }

    private static void DrawTextStringAlignedLeft(
        SvgTextBase svgTextBase,
        string text,
        ref float x,
        ref float y,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        Func<SvgElement?, string?>? getElementAddressKey,
        SvgSceneContextPaint? contextPaint,
        float[]? rotations = null)
    {
        using var commandSource = PushTextCommandSource(canvas, svgTextBase, getElementAddressKey);
        var drawX = x;
        var drawY = y;
        var advance = DrawTextPaintOrder(svgTextBase, includeFill: true, includeStroke: true, includeDecorations: true, phase =>
        {
            switch (phase)
            {
                case TextPaintPhase.Fill:
                    if (SvgScenePaintingService.IsValidFill(svgTextBase))
                    {
                        var fillPaint = SvgScenePaintingService.GetFillPaint(svgTextBase, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                        if (fillPaint is not null)
                        {
                            return DrawTextRunsAlignedLeft(svgTextBase, text, drawX, drawY, geometryBounds, fillPaint, canvas, assetLoader, rotations);
                        }
                    }

                    break;

                case TextPaintPhase.Stroke:
                    if (SvgScenePaintingService.IsValidStroke(svgTextBase, geometryBounds))
                    {
                        var strokePaint = SvgScenePaintingService.GetStrokePaint(svgTextBase, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                        if (strokePaint is not null)
                        {
                            return DrawTextRunsAlignedLeft(svgTextBase, text, drawX, drawY, geometryBounds, strokePaint, canvas, assetLoader, rotations);
                        }
                    }

                    break;

                case TextPaintPhase.Decorations:
                    DrawResolvedTextDecorations(svgTextBase, text, drawX, drawY, geometryBounds, ignoreAttributes, canvas, assetLoader, rotations, forceLeftAlign: true, contextPaint);
                    break;
            }

            return 0f;
        });
        ApplyInlineAdvance(svgTextBase, ref x, ref y, advance);
    }

    private static float DrawTextRunsAlignedLeft(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect geometryBounds,
        SKPaint paint,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        float[]? rotations)
    {
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

        var inlineStartAlign = GetLogicalStartTextAlign(svgTextBase);
        var isVertical = IsVerticalWritingMode(svgTextBase);
        if (isVertical &&
            TryCreateVerticalTextRunPlacements(svgTextBase, text, anchorX, anchorY, geometryBounds, inlineStartAlign, assetLoader, rotations, out var verticalPlacements, out var verticalAdvance))
        {
            _ = DrawVerticalTextRunPlacements(svgTextBase, verticalPlacements, geometryBounds, paint, canvas, assetLoader);
            return verticalAdvance;
        }

        if (CanNeedMixedScriptSpacingRunLayout(svgTextBase, text, assetLoader) &&
            TryCreateMixedScriptSpacingRunLayout(svgTextBase, text, geometryBounds, paint, assetLoader, out var mixedLayout) &&
            mixedLayout is not null)
        {
            DrawMixedScriptSpacingRun(svgTextBase, mixedLayout, anchorX, anchorY, geometryBounds, paint, canvas, assetLoader);
            return mixedLayout.TotalAdvance;
        }

        if (TryDrawSimpleScaledTextLengthRun(svgTextBase, text, anchorX, anchorY, geometryBounds, inlineStartAlign, paint, canvas, assetLoader, rotations, out var scaledTextLengthAdvance))
        {
            return scaledTextLengthAdvance;
        }

        if ((isVertical || HasPerGlyphLayoutAdjustments(svgTextBase, text)) &&
            TryCreateAlignedCodepointPlacements(svgTextBase, text, anchorX, anchorY, geometryBounds, inlineStartAlign, assetLoader, rotations, out var placements, out var totalAdvance))
        {
            if (!TryDrawPositionedTextBlob(svgTextBase, text, placements, geometryBounds, paint, canvas, assetLoader))
            {
                _ = DrawCodepointPlacements(svgTextBase, text, placements, geometryBounds, paint, canvas, assetLoader);
            }

            return totalAdvance;
        }

        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, text, paint, assetLoader, out var svgFontLayout) && svgFontLayout is not null)
        {
            var svgAdvance = EnsureWhitespaceAdvance(text, paint, assetLoader, svgFontLayout.Advance);
            svgFontLayout.Draw(canvas, paint, anchorX, anchorY);
            return svgAdvance;
        }

        if (RequiresSyntheticSmallCaps(svgTextBase, text))
        {
            var smallCapsAdvance = DrawSyntheticSmallCapsRuns(svgTextBase, text, anchorX, anchorY, SKTextAlign.Left, paint, canvas, assetLoader);
            return smallCapsAdvance;
        }

        var fallbackText = GetBrowserCompatibleFallbackText(svgTextBase, text, assetLoader);
        if (TryCreateBrowserCompatibleFullRunPaint(svgTextBase, fallbackText, paint, assetLoader, out var fullRunPaint, out var shapedText))
        {
            if (TryCreateBrowserBidiShapedGlyphRun(svgTextBase, fallbackText, fullRunPaint, assetLoader, out var bidiShapedRun, out var bidiShapedAdvance))
            {
                DrawShapedGlyphRun(bidiShapedRun, anchorX, anchorY, fullRunPaint, canvas);
                return bidiShapedAdvance;
            }

            var fullRunMeasureBounds = new SKRect();
            var measuredAdvance = EnsureWhitespaceAdvance(
                fallbackText,
                fullRunPaint,
                assetLoader,
                assetLoader.MeasureText(shapedText, fullRunPaint, ref fullRunMeasureBounds));

            if (TryCreateBrowserShapedGlyphRun(svgTextBase, fallbackText, fullRunPaint, assetLoader, out var shapedRun, out var shapedAdvance))
            {
                DrawShapedGlyphRun(shapedRun, anchorX, anchorY, fullRunPaint, canvas);
                return shapedAdvance;
            }

            canvas.DrawText(shapedText, anchorX, anchorY, fullRunPaint);
            return measuredAdvance;
        }

        var spanText = TryGetBrowserCompatibleVisualText(svgTextBase, fallbackText, out var visualText)
            ? visualText
            : fallbackText;
        var typefaceSpans = assetLoader.FindTypefaces(spanText, paint);
        if (typefaceSpans.Count == 0)
        {
            var scratchBounds = new SKRect();
            var measuredAdvance = EnsureWhitespaceAdvance(spanText, paint, assetLoader, assetLoader.MeasureText(spanText, paint, ref scratchBounds));
            canvas.DrawText(spanText, anchorX, anchorY, paint);
            return measuredAdvance;
        }

        if (typefaceSpans.Count == 1)
        {
            var typefaceSpan = typefaceSpans[0];
            paint.Typeface = typefaceSpan.Typeface;
            if (TryCreateBrowserShapedGlyphRun(svgTextBase, spanText, paint, assetLoader, out var shapedRun, out var shapedAdvance))
            {
                DrawShapedGlyphRun(shapedRun, anchorX, anchorY, paint, canvas);
                return shapedAdvance;
            }

            var runPaint = paint.Clone();
            canvas.DrawText(typefaceSpan.Text, anchorX, anchorY, runPaint);
            return EnsureWhitespaceAdvance(spanText, paint, assetLoader, typefaceSpan.Advance);
        }

        var currentX = anchorX;
        var naturalTotalAdvance = 0f;
        for (var i = 0; i < typefaceSpans.Count; i++)
        {
            var typefaceSpan = typefaceSpans[i];
            paint.Typeface = typefaceSpan.Typeface;
            canvas.DrawText(typefaceSpan.Text, currentX, anchorY, paint);
            currentX += typefaceSpan.Advance;
            naturalTotalAdvance += typefaceSpan.Advance;
            if (i + 1 < typefaceSpans.Count)
            {
                paint = paint.Clone();
            }
        }

        naturalTotalAdvance = EnsureWhitespaceAdvance(spanText, paint, assetLoader, naturalTotalAdvance);
        return naturalTotalAdvance;
    }

    private static SKRect MeasureTextStringBoundsAlignedLeft(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        float[]? rotations,
        out float advance)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, viewport, paint);
        paint.TextAlign = SKTextAlign.Left;

        var inlineStartAlign = GetLogicalStartTextAlign(svgTextBase);
        var isVertical = IsVerticalWritingMode(svgTextBase);
        if (isVertical &&
            TryCreateVerticalTextRunPlacements(svgTextBase, text, anchorX, anchorY, viewport, inlineStartAlign, assetLoader, rotations, out var verticalPlacements, out var verticalAdvance))
        {
            advance = verticalAdvance;
            return MeasureVerticalTextRunPlacementsBounds(svgTextBase, verticalPlacements, viewport, assetLoader, out _);
        }

        if ((isVertical || HasPerGlyphLayoutAdjustments(svgTextBase, text)) &&
            TryCreateAlignedCodepointPlacements(svgTextBase, text, anchorX, anchorY, viewport, inlineStartAlign, assetLoader, rotations, out var placements, out var totalAdvance))
        {
            advance = totalAdvance;
            return MeasureCodepointPlacementBounds(svgTextBase, text, placements, viewport, assetLoader, out _);
        }

        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, text, paint, assetLoader, out var svgFontLayout) && svgFontLayout is not null)
        {
            advance = EnsureWhitespaceAdvance(text, paint, assetLoader, svgFontLayout.Advance);
            return ExpandTextBoundsWithAdvanceBox(svgTextBase, svgFontLayout.GetBounds(anchorX, anchorY), anchorX, anchorY, advance, paint, assetLoader);
        }

        if (RequiresSyntheticSmallCaps(svgTextBase, text))
        {
            return MeasureSyntheticSmallCapsBounds(svgTextBase, text, anchorX, anchorY, SKTextAlign.Left, paint, assetLoader, out advance);
        }

        if (TryMeasureFallbackTextBounds(svgTextBase, text, anchorX, anchorY, paint, assetLoader, out var measuredBounds, out advance))
        {
            return ExpandTextBoundsWithAdvanceBox(svgTextBase, measuredBounds, anchorX, anchorY, advance, paint, assetLoader);
        }

        advance = MeasureTextAdvance(svgTextBase, text, viewport, assetLoader);
        var metrics = assetLoader.GetFontMetrics(paint);
        return new SKRect(anchorX, anchorY + metrics.Ascent, anchorX + advance, anchorY + metrics.Descent);
    }

    private static bool TryMeasureFallbackTextBounds(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out SKRect bounds,
        out float advance)
    {
        bounds = SKRect.Empty;
        var fallbackText = GetBrowserCompatibleFallbackText(svgTextBase, text, assetLoader);
        if (string.IsNullOrEmpty(fallbackText))
        {
            advance = 0f;
            return false;
        }

        var currentX = anchorX;
        advance = 0f;
        if (TryCreateBrowserCompatibleFullRunPaint(svgTextBase, fallbackText, paint, assetLoader, out var fullRunPaint, out var shapedText))
        {
            var fullRunMeasureBounds = new SKRect();
            advance = EnsureWhitespaceAdvance(
                fallbackText,
                fullRunPaint,
                assetLoader,
                assetLoader.MeasureText(shapedText, fullRunPaint, ref fullRunMeasureBounds));

            if (!fullRunMeasureBounds.IsEmpty)
            {
                bounds = new SKRect(
                    anchorX + fullRunMeasureBounds.Left,
                    anchorY + fullRunMeasureBounds.Top,
                    anchorX + fullRunMeasureBounds.Right,
                    anchorY + fullRunMeasureBounds.Bottom);
            }
            else if (TryGetRenderedTextLocalBounds(shapedText, fullRunPaint, assetLoader, out var fullRunBounds))
            {
                bounds = new SKRect(
                    anchorX + fullRunBounds.Left,
                    anchorY + fullRunBounds.Top,
                    anchorX + fullRunBounds.Right,
                    anchorY + fullRunBounds.Bottom);
            }

            bounds = ExpandTextBoundsWithAdvanceBox(svgTextBase, bounds, anchorX, anchorY, advance, fullRunPaint, assetLoader);
            return true;
        }

        var spans = assetLoader.FindTypefaces(fallbackText, paint);
        if (spans.Count > 0)
        {
            for (var i = 0; i < spans.Count; i++)
            {
                var localPaint = paint.Clone();
                localPaint.Typeface = spans[i].Typeface;

                var spanBounds = SKRect.Empty;
                var spanMeasureBounds = new SKRect();
                var measuredAdvance = assetLoader.MeasureText(spans[i].Text, localPaint, ref spanMeasureBounds);
                if (!spanMeasureBounds.IsEmpty)
                {
                    spanBounds = spanMeasureBounds;
                }
                else if (TryGetRenderedTextLocalBounds(spans[i].Text, localPaint, assetLoader, out var renderedBounds))
                {
                    spanBounds = renderedBounds;
                }

                var spanAdvance = EnsureWhitespaceAdvance(spans[i].Text, localPaint, assetLoader, spans[i].Advance > 0f ? spans[i].Advance : measuredAdvance);
                if (!spanBounds.IsEmpty)
                {
                    UnionBounds(ref bounds, new SKRect(
                        currentX + spanBounds.Left,
                        anchorY + spanBounds.Top,
                        currentX + spanBounds.Right,
                        anchorY + spanBounds.Bottom));
                }

                UnionBounds(ref bounds, GetTextAdvanceBox(svgTextBase, currentX, anchorY, spanAdvance, localPaint, assetLoader));
                currentX += spanAdvance;
                advance += spanAdvance;
            }

            return !bounds.IsEmpty || advance > 0f;
        }

        var measureBounds = new SKRect();
        advance = EnsureWhitespaceAdvance(fallbackText, paint, assetLoader, assetLoader.MeasureText(fallbackText, paint, ref measureBounds));
        var textBounds = measureBounds;
        if (textBounds.IsEmpty &&
            !TryGetRenderedTextLocalBounds(fallbackText, paint, assetLoader, out textBounds))
        {
            textBounds = new SKRect();
        }

        if (textBounds.IsEmpty)
        {
            bounds = GetTextAdvanceBox(svgTextBase, anchorX, anchorY, advance, paint, assetLoader);
            return advance > 0f;
        }

        bounds = new SKRect(
            anchorX + textBounds.Left,
            anchorY + textBounds.Top,
            anchorX + textBounds.Right,
            anchorY + textBounds.Bottom);
        bounds = ExpandTextBoundsWithAdvanceBox(svgTextBase, bounds, anchorX, anchorY, advance, paint, assetLoader);
        return true;
    }

    private static SKRect ExpandTextBoundsWithAdvanceBox(
        SvgTextBase svgTextBase,
        SKRect bounds,
        float anchorX,
        float anchorY,
        float advance,
        SKPaint paint,
        ISvgAssetLoader assetLoader)
    {
        var advanceBounds = GetTextAdvanceBox(svgTextBase, anchorX, anchorY, advance, paint, assetLoader);
        if (bounds.IsEmpty)
        {
            return advanceBounds;
        }

        UnionBounds(ref bounds, advanceBounds);
        return bounds;
    }

    private static SKRect GetTextAdvanceBox(
        SvgTextBase svgTextBase,
        float anchorX,
        float anchorY,
        float advance,
        SKPaint paint,
        ISvgAssetLoader assetLoader)
    {
        var metrics = assetLoader.GetFontMetrics(paint);
        if (!IsVerticalWritingMode(svgTextBase))
        {
            return new SKRect(anchorX, anchorY + metrics.Ascent, anchorX + advance, anchorY + metrics.Descent);
        }

        var endY = anchorY + (advance * GetInlineAdvanceDirection(svgTextBase));
        return new SKRect(
            anchorX + metrics.Ascent,
            Math.Min(anchorY, endY),
            anchorX + metrics.Descent,
            Math.Max(anchorY, endY));
    }

    private static string? PrepareText(
        SvgTextBase svgTextBase,
        string? value,
        bool trimLeadingWhitespace = true,
        bool trimTrailingWhitespace = false,
        bool preservePreLineBreaks = false)
    {
        // Normalize text before either preformatted rendering or guarded inline-size layout.
        value = ApplyTransformation(svgTextBase, value);
        if (value is null)
        {
            return null;
        }

        var whiteSpace = GetTextWhiteSpaceModel(svgTextBase);
        var preserveLineBreaks = preservePreLineBreaks && whiteSpace.PreservesSegmentBreaks;
        value = NormalizeTextWhiteSpace(value, whiteSpace, preserveLineBreaks);

        if (whiteSpace.DiscardsDocumentWhiteSpace)
        {
            return RemoveCssDocumentWhiteSpace(value);
        }

        value = ApplyWhiteSpaceTrim(value, whiteSpace, trimLeadingWhitespace, trimTrailingWhitespace);
        if (whiteSpace.PreservesTextWhitespace)
        {
            return value;
        }

        var normalizedValue = trimTrailingWhitespace
            ? (IsCssDocumentWhiteSpaceOnly(value)
                ? TrimCssDocumentWhiteSpace(value)
                : trimLeadingWhitespace
                    ? TrimCssDocumentWhiteSpace(value)
                    : TrimCssDocumentWhiteSpaceEnd(value))
            : trimLeadingWhitespace
                ? TrimCssDocumentWhiteSpaceStart(value)
                : value;

        return s_multipleSpaces.Replace(normalizedValue, " ");
    }

    private static SvgTextWhiteSpaceModel GetTextWhiteSpaceModel(SvgTextBase svgTextBase)
    {
        if (HasDeclaredWhiteSpace(svgTextBase))
        {
            return GetInlineSizeWhiteSpaceModel(svgTextBase);
        }

        return svgTextBase.SpaceHandling == XmlSpaceHandling.Preserve
            ? SvgTextWhiteSpaceModel.FromLegacy(SvgWhiteSpace.Pre)
            : SvgTextWhiteSpaceModel.FromLegacy(svgTextBase.WhiteSpace);
    }

    private static string NormalizeTextWhiteSpace(string value, SvgTextWhiteSpaceModel whiteSpace, bool preserveLineBreaks)
    {
        var builder = new StringBuilder(value)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        switch (whiteSpace.Collapse)
        {
            case SvgTextWhiteSpaceCollapseMode.Preserve:
            case SvgTextWhiteSpaceCollapseMode.BreakSpaces:
                if (!preserveLineBreaks)
                {
                    builder.Replace('\n', ' ');
                }

                builder.Replace('\t', ' ');
                return builder.ToString();

            case SvgTextWhiteSpaceCollapseMode.PreserveBreaks:
                if (!preserveLineBreaks)
                {
                    builder.Replace('\n', ' ');
                }

                builder.Replace('\t', ' ');
                return builder.ToString();

            case SvgTextWhiteSpaceCollapseMode.PreserveSpaces:
            case SvgTextWhiteSpaceCollapseMode.Collapse:
            case SvgTextWhiteSpaceCollapseMode.Discard:
            default:
                return builder
                    .Replace('\n', ' ')
                    .Replace('\t', ' ')
                    .ToString();
        }
    }

    private static string ApplyWhiteSpaceTrim(
        string value,
        SvgTextWhiteSpaceModel whiteSpace,
        bool trimLeadingWhitespace,
        bool trimTrailingWhitespace)
    {
        if (whiteSpace.Trim.HasFlag(SvgTextWhiteSpaceTrimMode.DiscardInner))
        {
            value = TrimCssDocumentWhiteSpaceAroundLineBreaks(value);
        }

        if (trimLeadingWhitespace && whiteSpace.TrimsLeadingWhitespace)
        {
            value = TrimCssDocumentWhiteSpaceStart(value);
        }

        if (trimTrailingWhitespace && whiteSpace.TrimsTrailingWhitespace)
        {
            value = TrimCssDocumentWhiteSpaceEnd(value);
        }

        return value;
    }

    private static bool IsCssDocumentWhiteSpaceOnly(string value)
    {
        if (value.Length == 0)
        {
            return true;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (!IsCssDocumentWhiteSpace(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCssDocumentWhiteSpace(char ch)
    {
        return ch is ' ' or '\t' or '\n' or '\r' or '\f';
    }

    private static string TrimCssDocumentWhiteSpace(string value)
    {
        return TrimCssDocumentWhiteSpaceEnd(TrimCssDocumentWhiteSpaceStart(value));
    }

    private static string TrimCssDocumentWhiteSpaceStart(string value)
    {
        var start = 0;
        while (start < value.Length && IsCssDocumentWhiteSpace(value[start]))
        {
            start++;
        }

        return start == 0 ? value : value.Substring(start);
    }

    private static string TrimCssDocumentWhiteSpaceEnd(string value)
    {
        var end = value.Length;
        while (end > 0 && IsCssDocumentWhiteSpace(value[end - 1]))
        {
            end--;
        }

        return end == value.Length ? value : value.Substring(0, end);
    }

    private static string RemoveCssDocumentWhiteSpace(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (!IsCssDocumentWhiteSpace(value[i]))
            {
                builder.Append(value[i]);
            }
        }

        return builder.ToString();
    }

    private static string TrimCssDocumentWhiteSpaceAroundLineBreaks(string value)
    {
        if (value.IndexOf('\n') < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\n')
            {
                builder.Append(value[i]);
                continue;
            }

            while (builder.Length > 0 && IsCssDocumentWhiteSpace(builder[builder.Length - 1]) && builder[builder.Length - 1] != '\n')
            {
                builder.Length--;
            }

            builder.Append('\n');
            while (i + 1 < value.Length && IsCssDocumentWhiteSpace(value[i + 1]) && value[i + 1] != '\n')
            {
                i++;
            }
        }

        return builder.ToString();
    }

    private static bool PreservesTextWhitespace(SvgTextBase svgTextBase)
    {
        if (HasDeclaredWhiteSpace(svgTextBase))
        {
            var model = GetInlineSizeWhiteSpaceModel(svgTextBase);
            return !model.DiscardsDocumentWhiteSpace && model.PreservesTextWhitespace;
        }

        return svgTextBase.SpaceHandling == XmlSpaceHandling.Preserve ||
               svgTextBase.WhiteSpace is SvgWhiteSpace.Pre or SvgWhiteSpace.PreWrap or SvgWhiteSpace.BreakSpaces;
    }

    private static bool UsesPreLineWhitespace(SvgTextBase svgTextBase)
    {
        return HasDeclaredWhiteSpace(svgTextBase)
            ? GetInlineSizeWhiteSpaceModel(svgTextBase).Collapse == SvgTextWhiteSpaceCollapseMode.PreserveBreaks
            : svgTextBase.WhiteSpace == SvgWhiteSpace.PreLine;
    }

    private static bool UsesPreLineWhitespaceInTextSubtree(SvgTextBase svgTextBase)
    {
        if (UsesPreLineWhitespace(svgTextBase))
        {
            return true;
        }

        var contentNodes = GetContentNodeList(svgTextBase);
        for (var i = 0; i < contentNodes.Count; i++)
        {
            if (contentNodes[i] is SvgTextBase childTextBase &&
                UsesPreLineWhitespaceInTextSubtree(childTextBase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PreservesInlineLineBreaks(SvgTextBase svgTextBase)
    {
        return HasDeclaredWhiteSpace(svgTextBase)
            ? GetInlineSizeWhiteSpaceModel(svgTextBase).PreservesSegmentBreaks
            : svgTextBase.WhiteSpace is SvgWhiteSpace.PreLine or SvgWhiteSpace.PreWrap or SvgWhiteSpace.BreakSpaces;
    }

    private static bool PreservesInlineLineBreaksInTextSubtree(SvgTextBase svgTextBase)
    {
        if (PreservesInlineLineBreaks(svgTextBase))
        {
            return true;
        }

        var contentNodes = GetContentNodeList(svgTextBase);
        for (var i = 0; i < contentNodes.Count; i++)
        {
            if (contentNodes[i] is SvgTextBase childTextBase &&
                PreservesInlineLineBreaksInTextSubtree(childTextBase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasDeclaredWhiteSpace(SvgElement element)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if ((current.TryGetOwnCascadedStyleValue("white-space", out var value) ||
                 current.TryGetOwnCascadedStyleValue("white-space-collapse", out value) ||
                 current.TryGetOwnCascadedStyleValue("text-wrap-mode", out value) ||
                 current.TryGetOwnCascadedStyleValue("white-space-trim", out value)) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CollapsesTextWhitespace(SvgTextBase svgTextBase)
    {
        return !PreservesTextWhitespace(svgTextBase);
    }

    private static string? PrepareResolvedContent(SvgTextBase svgTextBase, string? value, bool trimLeadingWhitespace, bool previousEndedWithSpace)
    {
        var prepared = PrepareText(svgTextBase, value, trimLeadingWhitespace);
        if (previousEndedWithSpace &&
            CollapsesTextWhitespace(svgTextBase) &&
            !string.IsNullOrEmpty(prepared) &&
            prepared![0] == ' ')
        {
            prepared = prepared.TrimStart(' ');
        }

        return prepared;
    }

    private static bool TryDrawFlattenedRotatedSvgFontLayout(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        SKRect geometryBounds,
        Func<SvgElement?, string?>? getElementAddressKey,
        bool trimLeadingWhitespaceAtStart,
        SvgSceneContextPaint? contextPaint)
    {
        if (!assetLoader.EnableSvgFonts ||
            IsVerticalWritingMode(svgTextBase) ||
            GetTextAnchorAlign(svgTextBase, geometryBounds) != SKTextAlign.Left ||
            HasOwnTextLengthAdjustment(svgTextBase) ||
            !ContainsRotateValuesInTextSubtree(svgTextBase) ||
            !TryCollectFlattenedTextCodepointsWithRotations(svgTextBase, trimLeadingWhitespaceAtStart, viewport, assetLoader, out var flattenedCodepoints) ||
            flattenedCodepoints.Count == 0 ||
            !flattenedCodepoints.Any(static codepoint => codepoint.Rotation.HasValue))
        {
            return false;
        }

        var codepoints = flattenedCodepoints.Select(static codepoint => codepoint.Codepoint).ToArray();
        var text = string.Concat(codepoints);
        var metricsPaint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, metricsPaint);
        metricsPaint.TextAlign = SKTextAlign.Left;
        if (!SvgFontTextRenderer.TryGetLayout(svgTextBase, text, metricsPaint, assetLoader, out var fullLayout) ||
            fullLayout is null)
        {
            return false;
        }

        var naturalAdvances = MeasureNaturalCodepointAdvances(svgTextBase, text, codepoints, geometryBounds, assetLoader);
        if (naturalAdvances.Length != flattenedCodepoints.Count)
        {
            return false;
        }

        var activeX = currentX;
        var activeY = currentY;
        var placements = new PositionedCodepointPlacement[flattenedCodepoints.Count];
        for (var i = 0; i < flattenedCodepoints.Count; i++)
        {
            var flattened = flattenedCodepoints[i];
            if (flattened.Y.HasValue)
            {
                activeY = flattened.Y.Value;
            }

            if (flattened.X.HasValue)
            {
                activeX = flattened.X.Value;
            }

            activeX += flattened.Dx;
            activeY += flattened.Dy;

            placements[i] = new PositionedCodepointPlacement(
                new SKPoint(activeX, activeY),
                flattened.Rotation ?? 0f,
                1f,
                activeX);

            if (i >= flattenedCodepoints.Count - 1)
            {
                continue;
            }

            var clusterAdvance = naturalAdvances[i];
            var styleSource = flattened.StyleSource;
            if (SupportsLetterSpacing(flattened.Codepoint))
            {
                clusterAdvance += ResolveSpacingValue(styleSource, styleSource.LetterSpacing, geometryBounds, naturalAdvances[i]);
            }

            if (IsWhitespaceCodepoint(flattened.Codepoint))
            {
                clusterAdvance += ResolveSpacingValue(styleSource, styleSource.WordSpacing, geometryBounds, naturalAdvances[i]);
            }

            if (!IsValidPositiveAdvance(clusterAdvance))
            {
                clusterAdvance = 0f;
            }

            activeX += clusterAdvance;
        }

        var finalIndex = flattenedCodepoints.Count - 1;
        currentX = placements[finalIndex].Point.X + naturalAdvances[finalIndex];
        currentY = placements[finalIndex].Point.Y;

        var groupStart = 0;
        while (groupStart < flattenedCodepoints.Count)
        {
            var groupStyle = flattenedCodepoints[groupStart].StyleSource;
            var builder = new StringBuilder();
            var groupPlacements = new List<PositionedCodepointPlacement>();
            var groupIndex = groupStart;
            while (groupIndex < flattenedCodepoints.Count && ReferenceEquals(flattenedCodepoints[groupIndex].StyleSource, groupStyle))
            {
                builder.Append(flattenedCodepoints[groupIndex].Codepoint);
                groupPlacements.Add(placements[groupIndex]);
                groupIndex++;
            }

            var runText = builder.ToString();
            var runPlacements = groupPlacements.ToArray();
            using var commandSource = PushTextCommandSource(canvas, groupStyle, getElementAddressKey);
            _ = DrawTextPaintOrder(groupStyle, includeFill: true, includeStroke: true, includeDecorations: true, phase =>
            {
                switch (phase)
                {
                    case TextPaintPhase.Fill:
                        if (SvgScenePaintingService.IsValidFill(groupStyle))
                        {
                            var fillPaint = SvgScenePaintingService.GetFillPaint(groupStyle, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                            if (fillPaint is not null)
                            {
                                _ = DrawCodepointPlacements(groupStyle, runText, runPlacements, geometryBounds, fillPaint, canvas, assetLoader);
                            }
                        }

                        break;

                    case TextPaintPhase.Stroke:
                        if (SvgScenePaintingService.IsValidStroke(groupStyle, geometryBounds))
                        {
                            var strokePaint = SvgScenePaintingService.GetStrokePaint(groupStyle, geometryBounds, assetLoader, ignoreAttributes, contextPaint);
                            if (strokePaint is not null)
                            {
                                _ = DrawCodepointPlacements(groupStyle, runText, runPlacements, geometryBounds, strokePaint, canvas, assetLoader);
                            }
                        }

                        break;

                    case TextPaintPhase.Decorations:
                        var decorationLayers = ResolveTextDecorationLayers(groupStyle);
                        if (decorationLayers.Count > 0)
                        {
                            DrawTextDecorations(
                                decorationLayers,
                                groupStyle,
                                runText,
                                runPlacements,
                                geometryBounds,
                                ignoreAttributes,
                                canvas,
                                assetLoader,
                                contextPaint);
                        }

                        break;
                }

                return 0f;
            });

            groupStart = groupIndex;
        }

        return true;
    }

    private static bool ContainsRotateValuesInTextSubtree(SvgTextBase svgTextBase)
    {
        if (HasRotateValues(svgTextBase))
        {
            return true;
        }

        foreach (var node in GetContentNodes(svgTextBase))
        {
            if (node is SvgTextBase childTextBase &&
                CanRenderTextSubtree(childTextBase) &&
                ContainsRotateValuesInTextSubtree(childTextBase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCreateFlattenedTextLengthRuns(
        SvgTextBase svgTextBase,
        float currentX,
        float currentY,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        bool trimLeadingWhitespaceAtStart,
        out PositionedCodepointRun[] runs,
        out float totalAdvance,
        out float finalY)
    {
        runs = Array.Empty<PositionedCodepointRun>();
        totalAdvance = 0f;
        finalY = currentY;

        if (!CanUseFlattenedTextLengthLayout(svgTextBase) ||
            !TryGetOwnTextLength(svgTextBase, viewport, isVertical: false, out var specifiedLength) ||
            specifiedLength <= 0f ||
            !TryCollectFlattenedTextCodepoints(svgTextBase, trimLeadingWhitespaceAtStart, viewport, assetLoader, out var flattenedCodepoints) ||
            flattenedCodepoints.Count == 0)
        {
            return false;
        }

        var naturalAdvances = MeasureFlattenedNaturalAdvances(flattenedCodepoints, geometryBounds, assetLoader);
        var stepAdvances = CreateFlattenedBaseStepAdvances(flattenedCodepoints, naturalAdvances, geometryBounds);
        var glyphScales = CreateUnitGlyphScales(flattenedCodepoints.Count);
        ApplyDescendantFlattenedTextLengthAdjustments(
            flattenedCodepoints,
            naturalAdvances,
            stepAdvances,
            glyphScales,
            svgTextBase,
            viewport,
            geometryBounds);
        var adjustedRootLength = false;
        if (!HasFlattenedTextLengthChunkBreaks(flattenedCodepoints))
        {
            adjustedRootLength = ApplyFlattenedTextLengthAdjustment(
                svgTextBase,
                specifiedLength,
                flattenedCodepoints,
                naturalAdvances,
                stepAdvances,
                glyphScales,
                0,
                flattenedCodepoints.Count);
        }
        else
        {
            var chunks = GetFlattenedTextLengthChunkRanges(flattenedCodepoints).ToArray();
            adjustedRootLength = TryApplyAncestorTextLengthToNestedChunks(
                svgTextBase,
                specifiedLength,
                flattenedCodepoints,
                naturalAdvances,
                stepAdvances,
                glyphScales,
                chunks);
            if (!adjustedRootLength)
            {
                foreach (var chunk in chunks)
                {
                    adjustedRootLength |= ApplyFlattenedTextLengthAdjustment(
                        svgTextBase,
                        specifiedLength,
                        flattenedCodepoints,
                        naturalAdvances,
                        stepAdvances,
                        glyphScales,
                        chunk.Start,
                        chunk.Count);
                }
            }
        }

        if (!adjustedRootLength)
        {
            return false;
        }

        totalAdvance = specifiedLength;

        var defaultX = ApplyTextAnchor(svgTextBase, currentX, geometryBounds, totalAdvance);
        var activeY = currentY;
        var placements = new PositionedCodepointPlacement[flattenedCodepoints.Count];
        for (var i = 0; i < flattenedCodepoints.Count; i++)
        {
            var explicitX = flattenedCodepoints[i].X;
            var explicitY = flattenedCodepoints[i].Y;
            if (explicitY.HasValue)
            {
                activeY = explicitY.Value;
            }

            if (explicitX.HasValue)
            {
                defaultX = explicitX.Value;
            }

            defaultX += flattenedCodepoints[i].Dx;
            activeY += flattenedCodepoints[i].Dy;

            var placementX = defaultX;
            var placementY = activeY;
            placements[i] = new PositionedCodepointPlacement(
                new SKPoint(placementX, placementY),
                0f,
                glyphScales[i],
                glyphScales[i] != 1f ? currentX : placementX);

            if (i >= flattenedCodepoints.Count - 1)
            {
                continue;
            }

            defaultX += stepAdvances[i];
        }

        finalY = activeY;
        if (TryCreateSingleStyleFlattenedTextLengthRun(flattenedCodepoints, placements, out var singleRun))
        {
            runs = [singleRun];
            return true;
        }

        var groupCount = CountFlattenedStyleGroups(flattenedCodepoints);
        if (groupCount <= 0)
        {
            return false;
        }

        runs = new PositionedCodepointRun[groupCount];
        var runIndex = 0;
        var groupStart = 0;
        while (groupStart < flattenedCodepoints.Count)
        {
            var groupStyle = flattenedCodepoints[groupStart].StyleSource;
            var groupIndex = groupStart;
            while (groupIndex < flattenedCodepoints.Count && ReferenceEquals(flattenedCodepoints[groupIndex].StyleSource, groupStyle))
            {
                groupIndex++;
            }

            runs[runIndex] = CreateFlattenedPositionedRun(groupStyle, flattenedCodepoints, placements, groupStart, groupIndex - groupStart);
            runIndex++;
            groupStart = groupIndex;
        }

        return runIndex > 0;
    }

    private static float[] CreateUnitGlyphScales(int count)
    {
        var glyphScales = new float[count];
        for (var i = 0; i < glyphScales.Length; i++)
        {
            glyphScales[i] = 1f;
        }

        return glyphScales;
    }

    private static bool TryCreateSingleStyleFlattenedTextLengthRun(
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        PositionedCodepointPlacement[] placements,
        out PositionedCodepointRun run)
    {
        run = default;
        if (codepoints.Count == 0 || placements.Length != codepoints.Count)
        {
            return false;
        }

        var styleSource = codepoints[0].StyleSource;
        for (var i = 1; i < codepoints.Count; i++)
        {
            if (!ReferenceEquals(codepoints[i].StyleSource, styleSource))
            {
                return false;
            }
        }

        run = new PositionedCodepointRun(styleSource, CreateFlattenedText(codepoints, 0, codepoints.Count), placements);
        return true;
    }

    private static int CountFlattenedStyleGroups(IReadOnlyList<FlattenedTextCodepoint> codepoints)
    {
        var groupCount = 0;
        var groupStart = 0;
        while (groupStart < codepoints.Count)
        {
            var groupStyle = codepoints[groupStart].StyleSource;
            var groupIndex = groupStart + 1;
            while (groupIndex < codepoints.Count && ReferenceEquals(codepoints[groupIndex].StyleSource, groupStyle))
            {
                groupIndex++;
            }

            groupCount++;
            groupStart = groupIndex;
        }

        return groupCount;
    }

    private static PositionedCodepointRun CreateFlattenedPositionedRun(
        SvgTextBase styleSource,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        PositionedCodepointPlacement[] placements,
        int start,
        int count)
    {
        var runPlacements = new PositionedCodepointPlacement[count];
        Array.Copy(placements, start, runPlacements, 0, count);
        return new PositionedCodepointRun(styleSource, CreateFlattenedText(codepoints, start, count), runPlacements);
    }

    private static string CreateFlattenedText(IReadOnlyList<FlattenedTextCodepoint> codepoints, int start, int count)
    {
        var end = Math.Min(codepoints.Count, start + count);
        if (start >= end)
        {
            return string.Empty;
        }

        var charCount = 0;
        for (var i = start; i < end; i++)
        {
            charCount += codepoints[i].Codepoint.Length;
        }

        if (charCount == 0)
        {
            return string.Empty;
        }

#if NET6_0_OR_GREATER
        return string.Create(
            charCount,
            (Codepoints: codepoints, Start: start, End: end),
            static (destination, state) =>
            {
                var offset = 0;
                for (var i = state.Start; i < state.End; i++)
                {
                    var codepoint = state.Codepoints[i].Codepoint.AsSpan();
                    codepoint.CopyTo(destination.Slice(offset));
                    offset += codepoint.Length;
                }
            });
#else
        var chars = new char[charCount];
        var offset = 0;
        for (var i = start; i < end; i++)
        {
            var codepoint = codepoints[i].Codepoint;
            codepoint.CopyTo(0, chars, offset, codepoint.Length);
            offset += codepoint.Length;
        }

        return new string(chars);
#endif
    }

    private static float[] MeasureFlattenedNaturalAdvances(
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var advances = new float[codepoints.Count];
        var start = 0;
        while (start < codepoints.Count)
        {
            var styleSource = codepoints[start].StyleSource;
            var end = start + 1;
            while (end < codepoints.Count && ReferenceEquals(codepoints[end].StyleSource, styleSource))
            {
                end++;
            }

            MeasureFlattenedNaturalAdvancesRange(
                codepoints,
                start,
                end - start,
                styleSource,
                geometryBounds,
                assetLoader,
                advances);
            start = end;
        }

        return advances;
    }

    private static void MeasureFlattenedNaturalAdvancesRange(
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        int start,
        int count,
        SvgTextBase styleSource,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        float[] advances)
    {
        if (count <= 0)
        {
            return;
        }

        if (count == 1)
        {
            advances[start] = MeasureNaturalTextAdvance(styleSource, codepoints[start].Codepoint, geometryBounds, assetLoader);
            return;
        }

        var runCodepoints = new FlattenedCodepointTextList(codepoints, start, count);
        var runText = CreateFlattenedText(codepoints, start, count);
        var runAdvances = MeasureNaturalCodepointAdvances(styleSource, runText, runCodepoints, geometryBounds, assetLoader);
        if (runAdvances.Length == count)
        {
            Array.Copy(runAdvances, 0, advances, start, count);
            return;
        }

        for (var i = 0; i < count; i++)
        {
            advances[start + i] = MeasureNaturalTextAdvance(styleSource, codepoints[start + i].Codepoint, geometryBounds, assetLoader);
        }
    }

    private static float[] CreateFlattenedBaseStepAdvances(
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        SKRect geometryBounds)
    {
        var stepAdvances = new float[Math.Max(0, codepoints.Count - 1)];
        for (var i = 0; i < stepAdvances.Length; i++)
        {
            if (!IsEffectiveFlattenedTextLengthGap(codepoints, i))
            {
                stepAdvances[i] = 0f;
                continue;
            }

            var styleSource = codepoints[i].StyleSource;
            var stepAdvance = naturalAdvances[i];
            var letterSpacing = ResolveSpacingValue(styleSource, styleSource.LetterSpacing, geometryBounds, naturalAdvances[i]);
            if (SupportsLetterSpacing(codepoints[i].Codepoint))
            {
                stepAdvance += letterSpacing;
            }

            var wordSpacing = ResolveSpacingValue(styleSource, styleSource.WordSpacing, geometryBounds, naturalAdvances[i]);
            if (IsWhitespaceCodepoint(codepoints[i].Codepoint))
            {
                stepAdvance += wordSpacing;
            }

            stepAdvances[i] = Math.Max(0f, stepAdvance);
        }

        return stepAdvances;
    }

    private static void ApplyDescendantFlattenedTextLengthAdjustments(
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        float[] stepAdvances,
        float[] glyphScales,
        SvgTextBase rootText,
        SKRect viewport,
        SKRect geometryBounds)
    {
        var start = 0;
        while (start < codepoints.Count)
        {
            var styleSource = codepoints[start].StyleSource;
            var end = start + 1;
            while (end < codepoints.Count && ReferenceEquals(codepoints[end].StyleSource, styleSource))
            {
                end++;
            }

            if (!ReferenceEquals(styleSource, rootText) &&
                HasOwnTextLengthAdjustment(styleSource) &&
                TryGetOwnTextLength(styleSource, viewport, IsVerticalWritingMode(styleSource), out var specifiedLength) &&
                specifiedLength > 0f)
            {
                _ = ApplyFlattenedTextLengthAdjustment(
                    styleSource,
                    specifiedLength,
                    codepoints,
                    naturalAdvances,
                    stepAdvances,
                    glyphScales,
                    start,
                    end - start);
            }

            start = end;
        }
    }

    private static bool ApplyFlattenedTextLengthAdjustment(
        SvgTextBase lengthSource,
        float specifiedLength,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        float[] stepAdvances,
        float[] glyphScales,
        int start,
        int count)
    {
        if (count <= 0 || specifiedLength <= 0f)
        {
            return false;
        }

        var naturalLength = GetFlattenedTextLengthRangeAdvance(codepoints, naturalAdvances, stepAdvances, glyphScales, start, count);
        if (naturalLength <= TextLengthTolerance || Math.Abs(naturalLength - specifiedLength) <= TextLengthTolerance)
        {
            return false;
        }

        if (GetOwnLengthAdjust(lengthSource) == SvgTextLengthAdjust.Spacing)
        {
            var gapCount = CountFlattenedTextLengthGaps(codepoints, start, count);
            if (gapCount == 0)
            {
                return false;
            }

            AddFlattenedTextLengthGapAdvance(codepoints, stepAdvances, start, count, (specifiedLength - naturalLength) / gapCount);
            return true;
        }

        var glyphScale = specifiedLength / naturalLength;
        if (glyphScale <= 0f)
        {
            return false;
        }

        for (var i = start; i < start + count; i++)
        {
            glyphScales[i] *= glyphScale;
            if (i < start + count - 1 && i < stepAdvances.Length)
            {
                stepAdvances[i] = Math.Max(0f, stepAdvances[i] * glyphScale);
            }
        }
        return true;
    }

    private static bool TryApplyAncestorTextLengthToNestedChunks(
        SvgTextBase rootText,
        float specifiedLength,
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        float[] stepAdvances,
        IReadOnlyList<float> glyphScales,
        IReadOnlyList<(int Start, int Count)> chunks)
    {
        var firstEligibleChunkStart = -1;
        List<int>? extraEligibleChunkStarts = null;
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            if (chunk.Count <= 1 ||
                chunk.Start < 0 ||
                chunk.Start >= codepoints.Count ||
                ReferenceEquals(codepoints[chunk.Start].StyleSource, rootText) ||
                !HasOwnTextLengthAdjustment(codepoints[chunk.Start].StyleSource))
            {
                continue;
            }

            if (firstEligibleChunkStart < 0)
            {
                firstEligibleChunkStart = chunk.Start;
            }
            else
            {
                extraEligibleChunkStarts ??= new List<int>();
                extraEligibleChunkStarts.Add(chunk.Start);
            }
        }

        if (firstEligibleChunkStart < 0)
        {
            return false;
        }

        var naturalLength = GetFlattenedTextLengthRangeAdvance(
            codepoints,
            naturalAdvances,
            stepAdvances,
            glyphScales,
            0,
            codepoints.Count);
        naturalLength += naturalAdvances[firstEligibleChunkStart] * glyphScales[firstEligibleChunkStart];
        if (extraEligibleChunkStarts is { })
        {
            for (var i = 0; i < extraEligibleChunkStarts.Count; i++)
            {
                var chunkStart = extraEligibleChunkStarts[i];
                naturalLength += naturalAdvances[chunkStart] * glyphScales[chunkStart];
            }
        }

        if (naturalLength <= TextLengthTolerance || Math.Abs(naturalLength - specifiedLength) <= TextLengthTolerance)
        {
            return false;
        }

        var eligibleChunkCount = 1 + (extraEligibleChunkStarts?.Count ?? 0);
        var extraChunkAdvance = (specifiedLength - naturalLength) / eligibleChunkCount;
        if (firstEligibleChunkStart >= 0 && firstEligibleChunkStart < stepAdvances.Length)
        {
            stepAdvances[firstEligibleChunkStart] += extraChunkAdvance;
        }

        if (extraEligibleChunkStarts is { })
        {
            for (var i = 0; i < extraEligibleChunkStarts.Count; i++)
            {
                var stepIndex = extraEligibleChunkStarts[i];
                if (stepIndex >= 0 && stepIndex < stepAdvances.Length)
                {
                    stepAdvances[stepIndex] += extraChunkAdvance;
                }
            }
        }

        return true;
    }

    private static bool HasFlattenedTextLengthChunkBreaks(IReadOnlyList<FlattenedTextCodepoint> codepoints)
    {
        for (var i = 1; i < codepoints.Count; i++)
        {
            if (codepoints[i].X.HasValue || codepoints[i].Y.HasValue)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<(int Start, int Count)> GetFlattenedTextLengthChunkRanges(
        IReadOnlyList<FlattenedTextCodepoint> codepoints)
    {
        var start = 0;
        for (var i = 1; i < codepoints.Count; i++)
        {
            if (!codepoints[i].X.HasValue && !codepoints[i].Y.HasValue)
            {
                continue;
            }

            yield return (start, i - start);
            start = i;
        }

        if (start < codepoints.Count)
        {
            yield return (start, codepoints.Count - start);
        }
    }

    private static float GetFlattenedTextLengthRangeAdvance(
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        IReadOnlyList<float> naturalAdvances,
        IReadOnlyList<float> stepAdvances,
        IReadOnlyList<float> glyphScales,
        int start,
        int count)
    {
        if (count <= 0 || start < 0 || start >= codepoints.Count)
        {
            return 0f;
        }

        var end = Math.Min(codepoints.Count, start + count);
        var advance = 0f;
        for (var i = start; i < end; i++)
        {
            if (i < end - 1)
            {
                advance += i < stepAdvances.Count ? stepAdvances[i] : 0f;
            }
            else
            {
                advance += naturalAdvances[i] * glyphScales[i];
            }
        }

        return Math.Max(0f, advance);
    }

    private static int CountFlattenedTextLengthGaps(
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        int start,
        int count)
    {
        var gapCount = 0;
        var end = Math.Min(codepoints.Count, start + count);
        for (var i = start; i < end - 1; i++)
        {
            if (IsEffectiveFlattenedTextLengthGap(codepoints, i))
            {
                gapCount++;
            }
        }

        return gapCount;
    }

    private static void AddFlattenedTextLengthGapAdvance(
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        float[] stepAdvances,
        int start,
        int count,
        float extraGapAdvance)
    {
        var end = Math.Min(codepoints.Count, start + count);
        for (var i = start; i < end - 1; i++)
        {
            if (IsEffectiveFlattenedTextLengthGap(codepoints, i))
            {
                stepAdvances[i] += extraGapAdvance;
            }
        }
    }

    private static bool IsEffectiveFlattenedTextLengthGap(
        IReadOnlyList<FlattenedTextCodepoint> codepoints,
        int stepIndex)
    {
        return stepIndex >= 0 &&
               stepIndex + 1 < codepoints.Count &&
               !codepoints[stepIndex + 1].X.HasValue;
    }

    private static bool TryCollectFlattenedTextCodepoints(
        SvgTextBase svgTextBase,
        bool trimLeadingWhitespaceAtStart,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out List<FlattenedTextCodepoint> codepoints)
    {
        return TryCollectFlattenedTextCodepoints(
            svgTextBase,
            trimLeadingWhitespaceAtStart,
            viewport,
            assetLoader,
            applyRootPositions: true,
            preservePreLineBreaks: false,
            out codepoints);
    }

    private static bool TryCollectFlattenedTextCodepointsWithRotations(
        SvgTextBase svgTextBase,
        bool trimLeadingWhitespaceAtStart,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out List<FlattenedTextCodepoint> codepoints)
    {
        codepoints = new List<FlattenedTextCodepoint>();
        var trimLeadingWhitespace = trimLeadingWhitespaceAtStart;
        var previousEndedWithSpace = false;
        var rotationState = ResolveRotationState(svgTextBase, null);
        if (!TryCollectFlattenedTextCodepoints(
                GetContentNodeList(svgTextBase),
                svgTextBase,
                codepoints,
                ref trimLeadingWhitespace,
                ref previousEndedWithSpace,
                viewport,
                assetLoader,
                preservePreLineBreaks: false,
                assignRotations: true,
                rotationState))
        {
            return false;
        }

        ApplyExplicitPositionsToFlattenedRange(svgTextBase, codepoints, 0, codepoints.Count, viewport, assetLoader);
        return codepoints.Count > 0;
    }

    private static bool TryCollectFlattenedTextCodepoints(
        SvgTextBase svgTextBase,
        bool trimLeadingWhitespaceAtStart,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        bool applyRootPositions,
        bool preservePreLineBreaks,
        out List<FlattenedTextCodepoint> codepoints)
    {
        codepoints = new List<FlattenedTextCodepoint>();
        var trimLeadingWhitespace = trimLeadingWhitespaceAtStart;
        var previousEndedWithSpace = false;
        if (!TryCollectFlattenedTextCodepoints(GetContentNodeList(svgTextBase), svgTextBase, codepoints, ref trimLeadingWhitespace, ref previousEndedWithSpace, viewport, assetLoader, preservePreLineBreaks))
        {
            return false;
        }

        InjectCollapsedSiblingSpaces(svgTextBase, codepoints);
        if (applyRootPositions)
        {
            ApplyExplicitPositionsToFlattenedRange(svgTextBase, codepoints, 0, codepoints.Count, viewport, assetLoader);
        }

        return codepoints.Count > 0;
    }

    private static bool TryCollectFlattenedTextCodepoints(
        IEnumerable<ISvgNode> contentNodes,
        SvgTextBase styleSource,
        List<FlattenedTextCodepoint> codepoints,
        ref bool trimLeadingWhitespace,
        ref bool previousEndedWithSpace,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        bool preservePreLineBreaks,
        bool assignRotations = false,
        RotationState? rotationState = null)
    {
        var contentNodeList = ToContentNodeList(contentNodes);
        for (var nodeIndex = 0; nodeIndex < contentNodeList.Count; nodeIndex++)
        {
            var node = contentNodeList[nodeIndex];
            switch (node)
            {
                case SvgAnchor svgAnchor:
                    if (!CanRenderTextSubtree(svgAnchor))
                    {
                        break;
                    }

                    if (!TryCollectFlattenedTextCodepoints(GetContentNodeList(svgAnchor), CreateAnchorTextStyleSource(svgAnchor), codepoints, ref trimLeadingWhitespace, ref previousEndedWithSpace, viewport, assetLoader, preservePreLineBreaks, assignRotations, rotationState))
                    {
                        return false;
                    }

                    break;

                case SvgTextSpan svgTextSpan:
                    {
                        if (!CanRenderTextSubtree(svgTextSpan))
                        {
                            break;
                        }

                        var childStart = codepoints.Count;
                        var childTrimLeadingWhitespace = trimLeadingWhitespace || previousEndedWithSpace || StartsPositionedTextChunk(svgTextSpan);
                        var childPreviousEndedWithSpace = false;
                        var childRotationState = assignRotations ? ResolveRotationState(svgTextSpan, rotationState) : null;
                        if (!TryCollectFlattenedTextCodepoints(GetContentNodeList(svgTextSpan), svgTextSpan, codepoints, ref childTrimLeadingWhitespace, ref childPreviousEndedWithSpace, viewport, assetLoader, preservePreLineBreaks, assignRotations, childRotationState))
                        {
                            return false;
                        }

                        var childCount = codepoints.Count - childStart;
                        ApplyExplicitPositionsToFlattenedRange(svgTextSpan, codepoints, childStart, childCount, viewport, assetLoader);
                        if (assignRotations &&
                            rotationState is not null &&
                            HasRotateValues(svgTextSpan) &&
                            childCount > 0)
                        {
                            rotationState.Consume(childCount);
                        }

                        if (childCount > 0 || childPreviousEndedWithSpace)
                        {
                            trimLeadingWhitespace = false;
                            previousEndedWithSpace = childPreviousEndedWithSpace;
                        }

                        break;
                    }

                case SvgTextPath:
                case SvgTextRef:
                    return false;

                case not SvgTextBase:
                    var rawContent = node.Content;
                    if (string.IsNullOrEmpty(node.Content))
                    {
                        break;
                    }

                    string? text;
                    if (!string.IsNullOrWhiteSpace(rawContent) &&
                        HasRenderableTextBaseSibling(contentNodeList, nodeIndex, -1) &&
                        HasRenderableTextBaseSibling(contentNodeList, nodeIndex, 1) &&
                        CollapsesTextWhitespace(styleSource))
                    {
                        text = " ";
                    }
                    else if (!string.IsNullOrWhiteSpace(rawContent) &&
                        CollapsesTextWhitespace(styleSource) &&
                        HasRenderableTextContentBefore(contentNodeList, nodeIndex) &&
                        HasRenderableTextContentAfter(contentNodeList, nodeIndex))
                    {
                        text = " ";
                    }
                    else
                    {
                        text = PrepareText(
                            styleSource,
                            node.Content,
                            trimLeadingWhitespace: trimLeadingWhitespace,
                            trimTrailingWhitespace: IsTerminalContentNode(contentNodeList, nodeIndex),
                            preservePreLineBreaks: preservePreLineBreaks);
                    }

                    if (previousEndedWithSpace &&
                        CollapsesTextWhitespace(styleSource) &&
                        !string.IsNullOrEmpty(text) &&
                        text![0] == ' ')
                    {
                        text = text.TrimStart(' ');
                    }

                    if (string.IsNullOrEmpty(text))
                    {
                        break;
                    }

                    var charIndex = 0;
                    var rotationIndex = 0;
                    var rotations = assignRotations ? ConsumeRotations(rotationState, text!) : null;
                    while (TryReadNextCodepoint(text!, ref charIndex, out var codepoint))
                    {
                        var flattenedCodepoint = new FlattenedTextCodepoint(styleSource, codepoint);
                        if (rotations is not null && rotationIndex < rotations.Length)
                        {
                            flattenedCodepoint.Rotation = rotations[rotationIndex];
                        }

                        codepoints.Add(flattenedCodepoint);
                        rotationIndex++;
                    }

                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = text!.EndsWith(" ", StringComparison.Ordinal);
                    break;
            }
        }

        return true;
    }

    private static void InjectCollapsedSiblingSpaces(SvgTextBase svgTextBase, List<FlattenedTextCodepoint> codepoints, bool assignRotations = false)
    {
        if (PreservesTextWhitespace(svgTextBase))
        {
            return;
        }

        var contentNodes = GetContentNodeList(svgTextBase);
        var insertionIndex = 0;
        for (var nodeIndex = 0; nodeIndex < contentNodes.Count; nodeIndex++)
        {
            switch (contentNodes[nodeIndex])
            {
                case SvgTextBase childTextBase:
                    insertionIndex += CountRenderedTextCodepoints(childTextBase, StartsPositionedTextChunk(childTextBase));
                    break;

                default:
                    {
                        var contentNode = contentNodes[nodeIndex];
                        if (contentNode is SvgTextBase)
                        {
                            break;
                        }

                        if (!string.IsNullOrEmpty(contentNode.Content) &&
                            string.IsNullOrWhiteSpace(contentNode.Content) &&
                            HasRenderableTextBaseSibling(contentNodes, nodeIndex, -1) &&
                            HasRenderableTextBaseSibling(contentNodes, nodeIndex, 1))
                        {
                            var flattenedSpace = new FlattenedTextCodepoint(svgTextBase, " ");
                            if (assignRotations)
                            {
                                flattenedSpace.Rotation = GetFlattenedRotationAt(svgTextBase, insertionIndex);
                            }

                            codepoints.Insert(insertionIndex++, flattenedSpace);
                            break;
                        }

                        var prepared = PrepareText(
                            svgTextBase,
                            contentNode.Content,
                            trimLeadingWhitespace: false,
                            trimTrailingWhitespace: IsTerminalContentNode(contentNodes, nodeIndex));
                        if (!string.IsNullOrEmpty(prepared))
                        {
                            insertionIndex += CountCodepoints(prepared!);
                        }

                        break;
                    }
            }
        }
    }

    private static float GetFlattenedRotationAt(SvgTextBase svgTextBase, int index)
    {
        var rotations = GetPositionedRotations(svgTextBase, index + 1);
        return GetRotationDegrees(rotations, index);
    }

    private static void ApplyExplicitPositionsToFlattenedRange(
        SvgTextBase svgTextBase,
        List<FlattenedTextCodepoint> codepoints,
        int startIndex,
        int count,
        SKRect viewport,
        ISvgAssetLoader assetLoader)
    {
        if (count <= 0)
        {
            return;
        }

        for (var i = 0; i < count && i < svgTextBase.X.Count; i++)
        {
            var index = startIndex + i;
            var codepoint = codepoints[index];
            codepoint.X = ResolveTextUnitValue(svgTextBase.X[i], UnitRenderingType.HorizontalOffset, svgTextBase, viewport, assetLoader);
            codepoints[index] = codepoint;
        }

        for (var i = 0; i < count && i < svgTextBase.Y.Count; i++)
        {
            var index = startIndex + i;
            var codepoint = codepoints[index];
            codepoint.Y = ResolveTextUnitValue(svgTextBase.Y[i], UnitRenderingType.VerticalOffset, svgTextBase, viewport, assetLoader);
            codepoints[index] = codepoint;
        }

        for (var i = 0; i < count && i < svgTextBase.Dx.Count; i++)
        {
            var index = startIndex + i;
            var codepoint = codepoints[index];
            codepoint.Dx = ResolveTextUnitValue(svgTextBase.Dx[i], UnitRenderingType.HorizontalOffset, svgTextBase, viewport, assetLoader);
            codepoints[index] = codepoint;
        }

        for (var i = 0; i < count && i < svgTextBase.Dy.Count; i++)
        {
            var index = startIndex + i;
            var codepoint = codepoints[index];
            codepoint.Dy = ResolveTextUnitValue(svgTextBase.Dy[i], UnitRenderingType.VerticalOffset, svgTextBase, viewport, assetLoader);
            codepoints[index] = codepoint;
        }
    }

    private static int CountRenderedTextCodepoints(SvgTextBase svgTextBase, bool trimLeadingWhitespaceAtStart)
    {
        var trimLeadingWhitespace = trimLeadingWhitespaceAtStart;
        var previousEndedWithSpace = false;
        return CountRenderedTextCodepoints(GetContentNodeList(svgTextBase), svgTextBase, ref trimLeadingWhitespace, ref previousEndedWithSpace);
    }

    private static int CountRenderedTextCodepoints(
        IEnumerable<ISvgNode> contentNodes,
        SvgTextBase svgTextBase,
        ref bool trimLeadingWhitespace,
        ref bool previousEndedWithSpace)
    {
        var count = 0;
        var contentNodeList = ToContentNodeList(contentNodes);
        for (var nodeIndex = 0; nodeIndex < contentNodeList.Count; nodeIndex++)
        {
            var node = contentNodeList[nodeIndex];
            switch (node)
            {
                case SvgAnchor svgAnchor:
                    if (!CanRenderTextSubtree(svgAnchor))
                    {
                        break;
                    }

                    count += CountRenderedTextCodepoints(GetContentNodeList(svgAnchor), CreateAnchorTextStyleSource(svgAnchor), ref trimLeadingWhitespace, ref previousEndedWithSpace);
                    break;

                case SvgTextSpan svgTextSpan:
                    {
                        if (!CanRenderTextSubtree(svgTextSpan))
                        {
                            break;
                        }

                        var childTrimLeadingWhitespace = trimLeadingWhitespace || previousEndedWithSpace || StartsPositionedTextChunk(svgTextSpan);
                        var childPreviousEndedWithSpace = false;
                        count += CountRenderedTextCodepoints(GetContentNodeList(svgTextSpan), svgTextSpan, ref childTrimLeadingWhitespace, ref childPreviousEndedWithSpace);
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = childPreviousEndedWithSpace;
                        break;
                    }

                case SvgTextRef svgTextRef when TryResolveTextReferenceContent(svgTextRef, out var rawReferencedText):
                    {
                        if (!CanRenderTextSubtree(svgTextRef))
                        {
                            break;
                        }

                        if (ShouldSuppressInlineTextReferenceContent(contentNodeList, nodeIndex))
                        {
                            break;
                        }

                        var prepared = PrepareResolvedContent(svgTextRef, rawReferencedText!, trimLeadingWhitespace, previousEndedWithSpace);
                        if (!string.IsNullOrEmpty(prepared))
                        {
                            count += CountCodepoints(prepared!);
                            trimLeadingWhitespace = false;
                            previousEndedWithSpace = prepared!.EndsWith(" ", StringComparison.Ordinal);
                        }

                        break;
                    }

                case SvgTextPath:
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = false;
                    break;

                case not SvgTextBase:
                    if (string.IsNullOrEmpty(node.Content))
                    {
                        break;
                    }

                    var text = PrepareText(
                        svgTextBase,
                        node.Content,
                        trimLeadingWhitespace: trimLeadingWhitespace,
                        trimTrailingWhitespace: IsTerminalContentNode(contentNodeList, nodeIndex));
                    if (previousEndedWithSpace &&
                        CollapsesTextWhitespace(svgTextBase) &&
                        !string.IsNullOrEmpty(text) &&
                        text![0] == ' ')
                    {
                        text = text.TrimStart(' ');
                    }

                    if (!string.IsNullOrEmpty(text))
                    {
                        count += CountCodepoints(text!);
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = text!.EndsWith(" ", StringComparison.Ordinal);
                    }

                    break;
            }
        }

        return count;
    }

    private static bool EndsWithCollapsedSpace(SvgElement element)
    {
        if (element is not SvgTextBase textBase)
        {
            return false;
        }

        var contentNodes = GetContentNodeList(element);
        for (var i = contentNodes.Count - 1; i >= 0; i--)
        {
            switch (contentNodes[i])
            {
                case SvgAnchor svgAnchor when CanRenderTextSubtree(svgAnchor) && EndsWithCollapsedSpace(svgAnchor):
                    return true;

                case SvgTextBase childTextBase when CanRenderTextSubtree(childTextBase) && EndsWithCollapsedSpace(childTextBase):
                    return true;

                case not SvgTextBase:
                    if (string.IsNullOrEmpty(contentNodes[i].Content))
                    {
                        continue;
                    }

                    var text = PrepareText(
                        textBase,
                        contentNodes[i].Content,
                        trimLeadingWhitespace: false,
                        trimTrailingWhitespace: IsTerminalContentNode(contentNodes, i));
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    return text!.EndsWith(" ", StringComparison.Ordinal);
            }
        }

        return false;
    }

    private static bool IsTerminalContentNode(IReadOnlyList<ISvgNode> contentNodes, int index)
    {
        for (var i = index + 1; i < contentNodes.Count; i++)
        {
            if (contentNodes[i] is SvgTextBase textBase)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(contentNodes[i].Content))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveTextReferenceContent(SvgTextRef svgTextRef, out string? content)
    {
        content = null;
        var referenceUri = SvgService.GetEffectiveReferenceUri(svgTextRef, svgTextRef.ReferencedElement);
        if (!IsSameDocumentTextReference(referenceUri))
        {
            return false;
        }

        var referencedElement = SvgService.GetReference<SvgElement>(svgTextRef, referenceUri);
        if (referencedElement is null ||
            referencedElement is SvgUnknownElement or NonSvgElement)
        {
            return false;
        }

        var builder = new StringBuilder();
        if (!TryAppendReferencedElementContent(referencedElement, builder, new HashSet<SvgElement>()))
        {
            return false;
        }

        content = builder.ToString();
        return !string.IsNullOrEmpty(content);
    }

    private static bool IsSameDocumentTextReference(Uri? referenceUri)
    {
        if (referenceUri is null || referenceUri.IsAbsoluteUri)
        {
            return false;
        }

        var rawReference = referenceUri.OriginalString;
        if (string.IsNullOrWhiteSpace(rawReference))
        {
            return false;
        }

        var fragmentIndex = rawReference.IndexOf('#');
        if (fragmentIndex < 0)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(rawReference.Substring(0, fragmentIndex));
    }

    private static bool TryAppendReferencedElementContent(SvgElement referencedElement, StringBuilder builder, HashSet<SvgElement> visited)
    {
        if (referencedElement is SvgUnknownElement or NonSvgElement)
        {
            return false;
        }

        if (!visited.Add(referencedElement))
        {
            return false;
        }

        foreach (var node in GetContentNodes(referencedElement))
        {
            switch (node)
            {
                case SvgTextRef:
                    visited.Remove(referencedElement);
                    return false;

                case SvgElement nestedChildElement:
                    if (!TryAppendReferencedElementContent(nestedChildElement, builder, visited))
                    {
                        visited.Remove(referencedElement);
                        return false;
                    }

                    break;

                default:
                    if (!string.IsNullOrEmpty(node.Content))
                    {
                        builder.Append(node.Content);
                    }

                    break;
            }
        }

        visited.Remove(referencedElement);
        return true;
    }

    private static string GetBrowserCompatibleFallbackText(SvgTextBase svgTextBase, string text, ISvgAssetLoader assetLoader)
    {
        return text;
    }

    private static string ApplyBrowserCompatibleBidiControls(SvgTextBase svgTextBase, string text)
    {
        return SvgTextBidiResolver.ApplyBrowserCompatibleControls(svgTextBase, text);
    }

    private static bool TryGetBrowserCompatibleVisualText(SvgTextBase svgTextBase, string text, out string visualText)
    {
        return SvgTextBidiResolver.TryGetVisualText(
            text,
            SvgTextBidiResolver.ResolveDirection(svgTextBase),
            SvgTextBidiResolver.ResolveUnicodeBidi(svgTextBase),
            out visualText);
    }

    private static bool TryGetVisualBidiText(string text, string? direction, string? unicodeBidi, out string visualText)
    {
        var baseDirection = string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase)
            ? SvgTextDirection.RightToLeft
            : SvgTextDirection.LeftToRight;
        var mode = unicodeBidi?.Trim().ToLowerInvariant() switch
        {
            "embed" => SvgUnicodeBidiMode.Embed,
            "isolate" => SvgUnicodeBidiMode.Isolate,
            "bidi-override" => SvgUnicodeBidiMode.BidiOverride,
            "isolate-override" => SvgUnicodeBidiMode.IsolateOverride,
            "plaintext" => SvgUnicodeBidiMode.PlainText,
            _ => SvgUnicodeBidiMode.Normal
        };
        return SvgTextBidiResolver.TryGetVisualText(text, baseDirection, mode, out visualText);
    }

    private static bool ContainsMixedStrongDirections(string text)
    {
        return SvgTextBidiResolver.ContainsMixedStrongDirections(text);
    }

    private static bool ContainsRightToLeftStrongDirection(string text)
    {
        return SvgTextBidiResolver.ContainsRightToLeftStrongDirection(text);
    }

    private static bool CanUseInlineSizeBidiOrdering(SvgTextBase svgTextBase, string text)
    {
        if (string.IsNullOrEmpty(text) ||
            !SvgTextBidiResolver.NeedsVisualOrdering(svgTextBase, text) ||
            IsVerticalWritingMode(svgTextBase))
        {
            return false;
        }

        return true;
    }

    private static bool CanUseSimpleInlineSizeBidiOrdering(SvgTextBase svgTextBase, string text)
    {
        return CanUseInlineSizeBidiOrdering(svgTextBase, text);
    }

    private static bool ContainsExplicitBidiControlCodepoint(string text)
    {
        return SvgTextBidiResolver.ContainsExplicitBidiControlCodepoint(text);
    }

    private static bool IsExplicitBidiControlCodepoint(string codepoint)
    {
        return SvgTextBidiResolver.IsExplicitBidiControlCodepoint(codepoint);
    }

    private static string ReverseByCodepoint(string text)
    {
        var codepoints = SplitCodepoints(text);
        codepoints.Reverse();
        return string.Concat(codepoints);
    }

    private static string ReorderRunsForRightToLeftBase(string text)
    {
        var codepoints = SplitCodepointsReadOnly(text);
        if (codepoints.Count == 0)
        {
            return text;
        }

        var resolvedDirections = ResolveBidiDirections(codepoints, baseDirection: -1);
        var runs = new List<(int Direction, string Text)>();
        var builder = new StringBuilder();
        var currentDirection = resolvedDirections[0];

        for (var i = 0; i < codepoints.Count; i++)
        {
            if (i > 0 && resolvedDirections[i] != currentDirection)
            {
                runs.Add((currentDirection, builder.ToString()));
                builder.Clear();
                currentDirection = resolvedDirections[i];
            }

            builder.Append(codepoints[i]);
        }

        if (builder.Length > 0)
        {
            runs.Add((currentDirection, builder.ToString()));
        }

        runs.Reverse();
        return string.Concat(runs.Select(run => run.Text));
    }

    private static int[] ResolveBidiDirections(IReadOnlyList<string> codepoints, int baseDirection)
    {
        var directions = new int[codepoints.Count];
        for (var i = 0; i < codepoints.Count; i++)
        {
            directions[i] = GetBidiStrongDirection(codepoints[i]);
        }

        for (var i = 0; i < directions.Length; i++)
        {
            if (directions[i] != 0)
            {
                continue;
            }

            var previousDirection = 0;
            for (var previousIndex = i - 1; previousIndex >= 0; previousIndex--)
            {
                if (directions[previousIndex] != 0)
                {
                    previousDirection = directions[previousIndex];
                    break;
                }
            }

            var nextDirection = 0;
            for (var nextIndex = i + 1; nextIndex < directions.Length; nextIndex++)
            {
                if (directions[nextIndex] != 0)
                {
                    nextDirection = directions[nextIndex];
                    break;
                }
            }

            directions[i] = nextDirection == 0 && previousDirection != 0
                ? baseDirection
                : previousDirection != 0 && previousDirection == nextDirection
                ? previousDirection
                : baseDirection == -1 && (previousDirection == -1 || nextDirection == -1)
                    ? -1
                    : previousDirection != 0
                        ? previousDirection
                    : nextDirection != 0
                        ? nextDirection
                        : baseDirection;
        }

        return directions;
    }

    private static int GetBidiStrongDirection(string codepoint)
    {
        return SvgTextBidiResolver.GetStrongDirection(codepoint);
    }

    private static bool IsRightToLeftCodepoint(int scalar)
    {
        return scalar switch
        {
            >= 0x0590 and <= 0x08FF => true,
            >= 0xFB1D and <= 0xFDFF => true,
            >= 0xFE70 and <= 0xFEFF => true,
            >= 0x10800 and <= 0x10FFF => true,
            >= 0x1E800 and <= 0x1EEFF => true,
            _ => false
        };
    }

    private static bool IsLeftToRightCodepoint(int scalar)
    {
        return scalar switch
        {
            >= 'A' and <= 'Z' => true,
            >= 'a' and <= 'z' => true,
            >= '0' and <= '9' => true,
            _ => char.IsLetterOrDigit(char.ConvertFromUtf32(scalar), 0)
        };
    }

    private static string? GetInheritedTextAttribute(SvgTextBase svgTextBase, string attributeName)
    {
        for (SvgElement? current = svgTextBase; current is not null; current = current.Parent)
        {
            if (current.TryGetOwnCascadedStyleValue(attributeName, out var cascadedValue) &&
                !string.IsNullOrWhiteSpace(cascadedValue))
            {
                return cascadedValue;
            }

            if (current.TryGetAttribute(attributeName, out var value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string GetCodepointStableUpperInvariant(string codepoint)
    {
        var upper = codepoint.ToUpperInvariant();
        return CountCodepoints(upper) == CountCodepoints(codepoint)
            ? upper
            : codepoint;
    }

    private static string? ApplyTransformation(SvgTextBase svgTextBase, string? value)
    {
        if (value is null)
        {
            return null;
        }

        return svgTextBase.TextTransformation switch
        {
            SvgTextTransformation.Capitalize => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value),
            SvgTextTransformation.Uppercase => value.ToUpper(CultureInfo.CurrentCulture),
            SvgTextTransformation.Lowercase => value.ToLower(CultureInfo.CurrentCulture),
            _ => value
        };
    }

    private static bool RequiresSyntheticSmallCaps(SvgTextBase svgTextBase, string text)
    {
        if (svgTextBase.FontVariant != SvgFontVariant.SmallCaps || string.IsNullOrEmpty(text))
        {
            return false;
        }

        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            if (!string.Equals(codepoint, GetCodepointStableUpperInvariant(codepoint), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static ResolvedFallbackCodepoint ResolveFallbackCodepoint(
        SvgTextBase svgTextBase,
        string codepoint,
        SKPaint paint,
        ISvgAssetLoader assetLoader)
    {
        var resolvedText = codepoint;
        var resolvedPaint = paint.Clone();

        if (svgTextBase.FontVariant == SvgFontVariant.SmallCaps)
        {
            var upper = GetCodepointStableUpperInvariant(codepoint);
            if (!string.Equals(codepoint, upper, StringComparison.Ordinal))
            {
                resolvedText = upper;
                resolvedPaint.TextSize *= SyntheticSmallCapsScale;
            }
        }

        var spans = assetLoader.FindTypefaces(resolvedText, resolvedPaint);
        if (spans.Count > 0)
        {
            resolvedPaint.Typeface = spans[0].Typeface;
            return new ResolvedFallbackCodepoint(spans[0].Text, resolvedPaint, spans[0].Advance);
        }

        var bounds = new SKRect();
        var advance = assetLoader.MeasureText(resolvedText, resolvedPaint, ref bounds);
        return new ResolvedFallbackCodepoint(resolvedText, resolvedPaint, advance);
    }

    private static float DrawSyntheticSmallCapsRuns(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKTextAlign textAlign,
        SKPaint paint,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader)
    {
        var totalAdvance = MeasureSyntheticSmallCapsAdvance(svgTextBase, text, paint, assetLoader);
        var currentX = textAlign switch
        {
            SKTextAlign.Center => anchorX - (totalAdvance * 0.5f),
            SKTextAlign.Right => anchorX - totalAdvance,
            _ => anchorX
        };

        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            var resolved = ResolveFallbackCodepoint(svgTextBase, codepoint, paint, assetLoader);
            canvas.DrawText(resolved.Text, currentX, anchorY, resolved.Paint);
            currentX += resolved.Advance;
        }

        return totalAdvance;
    }

    private static float MeasureSyntheticSmallCapsAdvance(
        SvgTextBase svgTextBase,
        string text,
        SKPaint paint,
        ISvgAssetLoader assetLoader)
    {
        var totalAdvance = 0f;
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            totalAdvance += ResolveFallbackCodepoint(svgTextBase, codepoint, paint, assetLoader).Advance;
        }

        return totalAdvance;
    }

    private static SKRect MeasureSyntheticSmallCapsBounds(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKTextAlign textAlign,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out float advance)
    {
        advance = MeasureSyntheticSmallCapsAdvance(svgTextBase, text, paint, assetLoader);
        var currentX = textAlign switch
        {
            SKTextAlign.Center => anchorX - (advance * 0.5f),
            SKTextAlign.Right => anchorX - advance,
            _ => anchorX
        };

        var bounds = SKRect.Empty;
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            var resolved = ResolveFallbackCodepoint(svgTextBase, codepoint, paint, assetLoader);
            var metrics = assetLoader.GetFontMetrics(resolved.Paint);
            UnionBounds(ref bounds, new SKRect(currentX, anchorY + metrics.Ascent, currentX + resolved.Advance, anchorY + metrics.Descent));
            currentX += resolved.Advance;
        }

        return bounds;
    }

    private static bool HasRotateValues(SvgTextBase svgTextBase)
    {
        return !string.IsNullOrWhiteSpace(svgTextBase.Rotate);
    }

    private static bool HasNonBaselineShift(SvgTextBase svgTextBase)
    {
        var baselineShift = svgTextBase.BaselineShift;
        return !string.IsNullOrWhiteSpace(baselineShift) &&
               !baselineShift.Trim().Equals("baseline", StringComparison.OrdinalIgnoreCase);
    }

    private static float GetBaselineOffset(SvgTextBase svgTextBase, SKRect viewport, ISvgAssetLoader assetLoader)
    {
        return GetDominantBaselineOffset(svgTextBase, viewport, assetLoader) + GetBaselineShift(svgTextBase, viewport);
    }

    private static float GetDominantBaselineOffset(SvgTextBase svgTextBase, SKRect viewport, ISvgAssetLoader assetLoader)
    {
        var baseline = ResolveBaselineIdentifier(svgTextBase);
        if (baseline == SvgDominantBaseline.Auto || baseline == SvgDominantBaseline.Inherit)
        {
            baseline = GetDefaultDominantBaseline(svgTextBase);
        }

        if (baseline == SvgDominantBaseline.Alphabetic)
        {
            return 0f;
        }

        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, viewport, paint);
        var metrics = assetLoader.GetFontMetrics(paint);
        EnsureUsableFontMetrics(ref metrics, paint.TextSize);

        if (TryGetSvgFontFaceBaselineOffset(svgTextBase, baseline, paint.TextSize, out var svgFontBaselineOffset))
        {
            return svgFontBaselineOffset;
        }

        return -SvgTextBaselineResolver.GetNativeBaselineLineOffset(metrics, baseline);
    }

    private static SvgDominantBaseline ResolveBaselineIdentifier(SvgTextBase svgTextBase)
    {
        if (TryGetAlignmentBaseline(svgTextBase, out var alignmentBaseline))
        {
            return alignmentBaseline;
        }

        var baseline = svgTextBase.DominantBaseline;
        if (baseline == SvgDominantBaseline.Inherit)
        {
            return ResolveInheritedDominantBaseline(svgTextBase);
        }

#pragma warning disable CS0618
        return baseline switch
        {
            SvgDominantBaseline.UseScript => ResolveScriptDominantBaseline(svgTextBase),
            SvgDominantBaseline.NoChange or SvgDominantBaseline.ResetSize => ResolveInheritedDominantBaseline(svgTextBase),
            _ => baseline
        };
#pragma warning restore CS0618
    }

    private static bool TryGetAlignmentBaseline(SvgTextBase svgTextBase, out SvgDominantBaseline baseline)
    {
        baseline = SvgDominantBaseline.Auto;
        if ((!svgTextBase.ComputedStyle.TryGetPropertyValue("alignment-baseline", out var value) ||
             string.IsNullOrWhiteSpace(value)) &&
            (!svgTextBase.TryGetAttribute("alignment-baseline", out value) ||
             string.IsNullOrWhiteSpace(value)))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "baseline":
            case "alphabetic":
                baseline = SvgDominantBaseline.Alphabetic;
                return true;

            case "auto":
                baseline = SvgDominantBaseline.Auto;
                return true;

            case "inherit":
                baseline = ResolveInheritedDominantBaseline(svgTextBase);
                return true;

            case "middle":
            case "center":
                baseline = SvgDominantBaseline.Middle;
                return true;

            case "central":
                baseline = SvgDominantBaseline.Central;
                return true;

            case "mathematical":
                baseline = SvgDominantBaseline.Mathematical;
                return true;

            case "ideographic":
                baseline = SvgDominantBaseline.Ideographic;
                return true;

            case "hanging":
                baseline = SvgDominantBaseline.Hanging;
                return true;

            case "text-before-edge":
            case "before-edge":
                baseline = SvgDominantBaseline.TextBeforeEdge;
                return true;

            case "text-after-edge":
            case "after-edge":
                baseline = SvgDominantBaseline.TextAfterEdge;
                return true;

            case "text-top":
                baseline = SvgDominantBaseline.TextTop;
                return true;

            case "text-bottom":
                baseline = SvgDominantBaseline.TextBottom;
                return true;

            default:
                return false;
        }
    }

    private static SvgDominantBaseline ResolveInheritedDominantBaseline(SvgTextBase svgTextBase)
    {
        for (SvgElement? current = svgTextBase.Parent; current is not null; current = current.Parent)
        {
            if (current is SvgTextBase parentText)
            {
                var baseline = parentText.DominantBaseline;
                if (baseline != SvgDominantBaseline.Inherit)
                {
                    return baseline == SvgDominantBaseline.Auto
                        ? GetDefaultDominantBaseline(parentText)
                        : baseline;
                }
            }
        }

        return GetDefaultDominantBaseline(svgTextBase);
    }

    private static SvgDominantBaseline ResolveScriptDominantBaseline(SvgTextBase svgTextBase)
    {
        var text = GetRawTextContent(svgTextBase);
        if (string.IsNullOrEmpty(text))
        {
            return GetDefaultDominantBaseline(svgTextBase);
        }

        var cjkCount = 0;
        var mathCount = 0;
        var hangingCount = 0;
        var latinCount = 0;
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            if (string.IsNullOrEmpty(codepoint) || char.IsWhiteSpace(codepoint, 0))
            {
                continue;
            }

            var scalar = char.ConvertToUtf32(codepoint, 0);
            if (SvgTextBaselineResolver.IsCjkBaselineScript(scalar))
            {
                cjkCount++;
            }
            else if (SvgTextBaselineResolver.IsMathematicalBaselineScript(scalar))
            {
                mathCount++;
            }
            else if (SvgTextBaselineResolver.IsHangingBaselineScript(scalar))
            {
                hangingCount++;
            }
            else if (char.IsLetterOrDigit(codepoint, 0))
            {
                latinCount++;
            }
        }

        if (cjkCount > latinCount && cjkCount >= mathCount && cjkCount >= hangingCount)
        {
            return SvgDominantBaseline.Ideographic;
        }

        if (mathCount > latinCount && mathCount >= hangingCount)
        {
            return SvgDominantBaseline.Mathematical;
        }

        if (hangingCount > latinCount)
        {
            return SvgDominantBaseline.Hanging;
        }

        return SvgDominantBaseline.Alphabetic;
    }

    private static string GetRawTextContent(SvgElement element)
    {
        var builder = new StringBuilder();
        AppendRawTextContent(element, builder, new HashSet<SvgElement>());
        return builder.ToString();
    }

    private static void AppendRawTextContent(SvgElement element, StringBuilder builder, HashSet<SvgElement> visited)
    {
        if (!visited.Add(element))
        {
            return;
        }

        foreach (var node in GetContentNodes(element))
        {
            if (node is SvgElement childElement)
            {
                AppendRawTextContent(childElement, builder, visited);
            }
            else if (!string.IsNullOrEmpty(node.Content))
            {
                builder.Append(node.Content);
            }
        }

        visited.Remove(element);
    }

    private static bool TryGetSvgFontFaceBaselineOffset(
        SvgTextBase svgTextBase,
        SvgDominantBaseline baseline,
        float textSize,
        out float offset)
    {
        offset = 0f;
        var fontFace = FindMatchingSvgFontFace(svgTextBase);
        if (fontFace is null ||
            fontFace.UnitsPerEm <= 0f ||
            textSize <= 0f)
        {
            return false;
        }

        var coordinate = baseline switch
        {
            SvgDominantBaseline.Alphabetic => fontFace.Alphabetic,
            SvgDominantBaseline.Ideographic when fontFace.Ideographic != float.MinValue => fontFace.Ideographic,
            SvgDominantBaseline.Hanging when fontFace.Hanging != float.MinValue => fontFace.Hanging,
            SvgDominantBaseline.Mathematical when fontFace.Mathematical != float.MinValue => fontFace.Mathematical,
            SvgDominantBaseline.TextBeforeEdge or SvgDominantBaseline.TextTop => fontFace.Ascent,
            SvgDominantBaseline.TextAfterEdge or SvgDominantBaseline.TextBottom => -fontFace.Descent,
            SvgDominantBaseline.Middle when fontFace.XHeight != float.MinValue => fontFace.XHeight * 0.5f,
            SvgDominantBaseline.Middle when fontFace.CapHeight != float.MinValue => fontFace.CapHeight * 0.5f,
            SvgDominantBaseline.Central => (fontFace.Ascent - fontFace.Descent) * 0.5f,
            _ => float.MinValue
        };

        if (coordinate == float.MinValue)
        {
            return false;
        }

        offset = coordinate * textSize / fontFace.UnitsPerEm;
        return true;
    }

    private static SvgFontFace? FindMatchingSvgFontFace(SvgTextBase svgTextBase)
    {
        var document = svgTextBase.OwnerDocument;
        if (document is null)
        {
            return null;
        }

        var family = NormalizeFontFamilyName(svgTextBase.FontFamily);
        if (string.IsNullOrWhiteSpace(family))
        {
            return null;
        }

        foreach (var fontFace in document.Descendants().OfType<SvgFontFace>())
        {
            if (string.Equals(NormalizeFontFamilyName(fontFace.FontFamily), family, StringComparison.OrdinalIgnoreCase))
            {
                return fontFace;
            }
        }

        return null;
    }

    private static string? NormalizeFontFamilyName(string? family)
    {
        if (string.IsNullOrWhiteSpace(family))
        {
            return null;
        }

        var first = family!.Split(',')[0].Trim();
        if (first.Length >= 2 &&
            ((first[0] == '\'' && first[first.Length - 1] == '\'') ||
             (first[0] == '"' && first[first.Length - 1] == '"')))
        {
            first = first.Substring(1, first.Length - 2);
        }

        return first.Trim();
    }

    private static void EnsureUsableFontMetrics(ref SKFontMetrics metrics, float textSize)
    {
        if (metrics.Ascent != 0f || metrics.Descent != 0f || metrics.Top != 0f || metrics.Bottom != 0f)
        {
            return;
        }

        metrics.Ascent = -textSize * 0.8f;
        metrics.Descent = textSize * 0.2f;
        metrics.Top = metrics.Ascent;
        metrics.Bottom = metrics.Descent;
    }

    private static float GetBaselineShift(SvgTextBase svgTextBase, SKRect viewport)
    {
        var baselineShiftText = svgTextBase.BaselineShift;
        if (string.IsNullOrWhiteSpace(baselineShiftText))
        {
            return 0f;
        }

        baselineShiftText = baselineShiftText.Trim().ToLowerInvariant();
        return baselineShiftText switch
        {
            "baseline" => 0f,
            "sub" => new SvgUnit(SvgUnitType.Ex, 1f).ToDeviceValue(UnitRenderingType.Vertical, svgTextBase, viewport),
            "super" => -new SvgUnit(SvgUnitType.Ex, 1f).ToDeviceValue(UnitRenderingType.Vertical, svgTextBase, viewport),
            _ => TryParseBaselineShift(svgTextBase, viewport, baselineShiftText, out var shift) ? -shift : 0f
        };
    }

    private static bool TryParseBaselineShift(SvgTextBase svgTextBase, SKRect viewport, string baselineShiftText, out float shift)
    {
        var converter = new SvgUnitConverter();
        SvgUnit unit;
        try
        {
            if (converter.ConvertFromInvariantString(baselineShiftText) is not SvgUnit parsedUnit)
            {
                shift = 0f;
                return false;
            }

            unit = parsedUnit;
        }
        catch (FormatException)
        {
            shift = 0f;
            return false;
        }

        if (unit.Type == SvgUnitType.Percentage)
        {
            var fontSize = svgTextBase.FontSize;
            var basis = (fontSize == SvgUnit.None || fontSize == SvgUnit.Empty)
                ? 12f
                : fontSize.ToDeviceValue(UnitRenderingType.Vertical, svgTextBase, viewport);
            shift = basis * unit.Value / 100f;
            return true;
        }

        shift = unit.ToDeviceValue(UnitRenderingType.Vertical, svgTextBase, viewport);
        return true;
    }

    private static bool HasFeatures(SvgElement element, DrawAttributes ignoreAttributes)
    {
        var hasRequiredFeatures = ignoreAttributes.HasFlag(DrawAttributes.RequiredFeatures) || element.HasRequiredFeatures();
        var hasRequiredExtensions = ignoreAttributes.HasFlag(DrawAttributes.RequiredExtensions) || element.HasRequiredExtensions();
        var hasSystemLanguage = ignoreAttributes.HasFlag(DrawAttributes.SystemLanguage) || element.HasSystemLanguage();
        return hasRequiredFeatures && hasRequiredExtensions && hasSystemLanguage;
    }

    private static void AppendPathCommands(SKPath targetPath, SKPath? sourcePath)
    {
        if (targetPath.Commands is null || sourcePath?.Commands is not { Count: > 0 } commands)
        {
            return;
        }

        for (var i = 0; i < commands.Count; i++)
        {
            targetPath.Commands.Add(commands[i].DeepClone());
        }
    }

    private static SKRect CreateLocalCullRect(SKRect bounds)
    {
        if (bounds.IsEmpty || bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return SKRect.Empty;
        }

        return new SKRect(
            (float)Math.Floor(bounds.Left),
            (float)Math.Floor(bounds.Top),
            (float)Math.Ceiling(bounds.Right),
            (float)Math.Ceiling(bounds.Bottom));
    }

    private static SKRect CreateTextLocalCullRect(SKRect bounds)
    {
        if (bounds.IsEmpty)
        {
            return SKRect.Empty;
        }

        // Text bounds come from font metrics and can be slightly tighter than the final
        // platform rasterization, especially around antialiased glyph edges.
        const float padding = 1f;
        var paddedBounds = new SKRect(
            bounds.Left - padding,
            bounds.Top - padding,
            bounds.Right + padding,
            bounds.Bottom + padding);
        return CreateLocalCullRect(paddedBounds);
    }
}
