using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

internal static partial class SvgSceneTextCompiler
{
    internal sealed class AlignedCodepointPlacementBenchmarkInput
    {
        public AlignedCodepointPlacementBenchmarkInput(
            SvgTextBase styleSource,
            string text,
            float anchorX,
            float anchorY,
            SKRect geometryBounds,
            SKTextAlign textAlign,
            float[]? explicitRotations)
        {
            StyleSource = styleSource;
            Text = text;
            AnchorX = anchorX;
            AnchorY = anchorY;
            GeometryBounds = geometryBounds;
            TextAlign = textAlign;
            ExplicitRotations = explicitRotations;
        }

        public SvgTextBase StyleSource { get; }

        public string Text { get; }

        public float AnchorX { get; }

        public float AnchorY { get; }

        public SKRect GeometryBounds { get; }

        public SKTextAlign TextAlign { get; }

        public float[]? ExplicitRotations { get; }
    }

    internal sealed class TextPathCodepointPlacementBenchmarkInput
    {
        internal readonly object PathSamples;
        internal readonly float[] VisibleGlyphMidOffsets;
        internal readonly float[] VisibleGlyphAdvances;
        internal readonly float[] VisibleCodepointRotationDegrees;
        internal readonly SKPoint[] ResolvedRawPoints;
        internal readonly SKPoint[] ResolvedTangents;
        internal readonly float PlacementScaleX;
        internal readonly float CurrentVOffset;
        internal readonly bool IsClosedLoop;

        internal TextPathCodepointPlacementBenchmarkInput(
            SvgTextBase styleSource,
            string text,
            float startOffset,
            float baseVOffset,
            SKRect viewport,
            SKRect geometryBounds,
            object pathSamples,
            float[] visibleGlyphMidOffsets,
            float[] visibleGlyphAdvances,
            float[] visibleCodepointRotationDegrees,
            SKPoint[] resolvedRawPoints,
            SKPoint[] resolvedTangents,
            float placementScaleX,
            float currentVOffset,
            bool isClosedLoop)
        {
            StyleSource = styleSource;
            Text = text;
            StartOffset = startOffset;
            BaseVOffset = baseVOffset;
            Viewport = viewport;
            GeometryBounds = geometryBounds;
            PathSamples = pathSamples;
            VisibleGlyphMidOffsets = visibleGlyphMidOffsets;
            VisibleGlyphAdvances = visibleGlyphAdvances;
            VisibleCodepointRotationDegrees = visibleCodepointRotationDegrees;
            ResolvedRawPoints = resolvedRawPoints;
            ResolvedTangents = resolvedTangents;
            PlacementScaleX = placementScaleX;
            CurrentVOffset = currentVOffset;
            IsClosedLoop = isClosedLoop;
        }

        public SvgTextBase StyleSource { get; }

        public string Text { get; }

        public float StartOffset { get; }

        public float BaseVOffset { get; }

        public SKRect Viewport { get; }

        public SKRect GeometryBounds { get; }

        public int PathSampleCount => ((IReadOnlyList<PathSample>)PathSamples).Count;

        public int VisibleGlyphCount => VisibleGlyphMidOffsets.Length;
    }

    internal static AlignedCodepointPlacementBenchmarkInput CreateAlignedCodepointPlacementBenchmarkInput(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        return new AlignedCodepointPlacementBenchmarkInput(
            svgTextBase,
            text,
            anchorX: 0f,
            anchorY: 0f,
            geometryBounds,
            paint.TextAlign,
            explicitRotations: null);
    }

    internal static bool TryBenchmarkAlignedCodepointPlacements(
        AlignedCodepointPlacementBenchmarkInput input,
        ISvgAssetLoader assetLoader,
        out int placementCount,
        out float totalAdvance)
    {
        var succeeded = TryCreateAlignedCodepointPlacements(
            input.StyleSource,
            input.Text,
            input.AnchorX,
            input.AnchorY,
            input.GeometryBounds,
            input.TextAlign,
            assetLoader,
            input.ExplicitRotations,
            out var placements,
            out totalAdvance);

        placementCount = succeeded ? placements.Length : 0;
        return succeeded;
    }

    internal static TextPathCodepointPlacementBenchmarkInput CreateTextPathCodepointPlacementBenchmarkInput(
        SvgTextPath svgTextPath,
        string text,
        SKRect viewport,
        ISvgAssetLoader assetLoader)
    {
        if (!TryResolveTextPathGeometry(svgTextPath, viewport, out _, out var skPath, out var geometryBounds, out var pathSamples, out var pathLength, out var isClosedLoop))
        {
            throw new InvalidOperationException($"Failed to resolve textPath geometry for '{svgTextPath.ID ?? text}'.");
        }

        var resolvedStartOffset = ResolveTextPathStartOffset(svgTextPath, skPath, viewport, pathLength);
        BuildTextPathBenchmarkPlacementPlan(
            svgTextPath,
            text,
            resolvedStartOffset,
            baseVOffset: 0f,
            viewport,
            geometryBounds,
            pathSamples,
            isClosedLoop,
            assetLoader,
            out var visibleGlyphMidOffsets,
            out var visibleGlyphAdvances,
            out var visibleCodepointRotationDegrees,
            out var placementScaleX,
            out var currentVOffset);

        var resolvedRawPoints = new SKPoint[visibleGlyphMidOffsets.Length];
        var resolvedTangents = new SKPoint[visibleGlyphMidOffsets.Length];
        var preferredSegmentIndex = 1;
        for (var i = 0; i < visibleGlyphMidOffsets.Length; i++)
        {
            if (!TryGetPathPointAndTangent(pathSamples, visibleGlyphMidOffsets[i], ref preferredSegmentIndex, out resolvedRawPoints[i], out resolvedTangents[i]))
            {
                throw new InvalidOperationException($"Failed to resolve textPath midpoint {i} for '{svgTextPath.ID ?? text}'.");
            }
        }

        return new TextPathCodepointPlacementBenchmarkInput(
            svgTextPath,
            text,
            resolvedStartOffset,
            baseVOffset: 0f,
            viewport,
            geometryBounds,
            pathSamples,
            visibleGlyphMidOffsets,
            visibleGlyphAdvances,
            visibleCodepointRotationDegrees,
            resolvedRawPoints,
            resolvedTangents,
            placementScaleX,
            currentVOffset,
            isClosedLoop);
    }

    internal static int BenchmarkResolveTextPathGeometry(SvgTextPath svgTextPath, SKRect viewport)
    {
        return TryResolveTextPathGeometry(svgTextPath, viewport, out _, out _, out _, out var pathSamples, out _, out _)
            ? pathSamples.Count
            : 0;
    }

    internal static bool TryBenchmarkTextPathCodepointPlacements(
        TextPathCodepointPlacementBenchmarkInput input,
        ISvgAssetLoader assetLoader,
        out int placementCount,
        out float totalAdvance)
    {
        var succeeded = TryCreateTextPathCodepointPlacements(
            input.StyleSource,
            input.StyleSource,
            input.Text,
            input.StartOffset,
            input.BaseVOffset,
            (IReadOnlyList<PathSample>)input.PathSamples,
            input.IsClosedLoop,
            input.Viewport,
            input.GeometryBounds,
            assetLoader,
            out _,
            out var placements,
            out totalAdvance,
            out _);

        placementCount = succeeded ? placements.Length : 0;
        return succeeded;
    }

    internal static float BenchmarkTextPathMidpointLookup(TextPathCodepointPlacementBenchmarkInput input)
    {
        var pathSamples = (IReadOnlyList<PathSample>)input.PathSamples;
        var preferredSegmentIndex = 1;
        var checksum = 0f;
        for (var i = 0; i < input.VisibleGlyphMidOffsets.Length; i++)
        {
            if (!TryGetPathPointAndTangent(pathSamples, input.VisibleGlyphMidOffsets[i], ref preferredSegmentIndex, out var rawPoint, out var tangent))
            {
                continue;
            }

            checksum += rawPoint.X + rawPoint.Y + tangent.X + tangent.Y;
        }

        return checksum;
    }

    internal static float BenchmarkTextPathPlacementEmission(TextPathCodepointPlacementBenchmarkInput input)
    {
        var checksum = 0f;
        var currentVOffset = input.CurrentVOffset;
        for (var i = 0; i < input.ResolvedRawPoints.Length; i++)
        {
            var rawPoint = input.ResolvedRawPoints[i];
            var tangent = input.ResolvedTangents[i];
            var codepointRotationDegrees = input.VisibleCodepointRotationDegrees[i];
            var angleDegrees = (float)(Math.Atan2(tangent.Y, tangent.X) * 180d / Math.PI);
            var finalAngleDegrees = angleDegrees + codepointRotationDegrees;

            SKPoint baselineDirection;
            if (Math.Abs(codepointRotationDegrees) <= 0.001f)
            {
                baselineDirection = tangent;
            }
            else
            {
                var rotationRadians = codepointRotationDegrees * ((float)Math.PI / 180f);
                var cos = (float)Math.Cos(rotationRadians);
                var sin = (float)Math.Sin(rotationRadians);
                baselineDirection = new SKPoint(
                    (tangent.X * cos) - (tangent.Y * sin),
                    (tangent.X * sin) + (tangent.Y * cos));
            }

            var baselineNormal = new SKPoint(-baselineDirection.Y, baselineDirection.X);
            var glyphAdvance = input.VisibleGlyphAdvances[i];
            var point = new SKPoint(
                rawPoint.X + (baselineNormal.X * currentVOffset) - (baselineDirection.X * glyphAdvance * 0.5f),
                rawPoint.Y + (baselineNormal.Y * currentVOffset) - (baselineDirection.Y * glyphAdvance * 0.5f));
            var placement = new PositionedCodepointPlacement(point, finalAngleDegrees, input.PlacementScaleX, point.X);
            checksum += placement.Point.X + placement.Point.Y + placement.RotationDegrees;
        }

        return checksum;
    }

    private static void BuildTextPathBenchmarkPlacementPlan(
        SvgTextBase svgTextBase,
        string text,
        float startOffset,
        float baseVOffset,
        SKRect viewport,
        SKRect geometryBounds,
        IReadOnlyList<PathSample> pathSamples,
        bool isClosedLoop,
        ISvgAssetLoader assetLoader,
        out float[] visibleGlyphMidOffsets,
        out float[] visibleGlyphAdvances,
        out float[] visibleCodepointRotationDegrees,
        out float placementScaleX,
        out float currentVOffset)
    {
        var codepoints = SplitCodepointsReadOnly(text);
        if (codepoints.Count == 0)
        {
            visibleGlyphMidOffsets = Array.Empty<float>();
            visibleGlyphAdvances = Array.Empty<float>();
            visibleCodepointRotationDegrees = Array.Empty<float>();
            placementScaleX = 1f;
            currentVOffset = baseVOffset + GetBaselineOffset(svgTextBase, viewport, assetLoader);
            return;
        }

        var naturalAdvances = MeasureNaturalCodepointAdvances(svgTextBase, text, codepoints, geometryBounds, assetLoader);
        var letterSpacingUnit = svgTextBase.LetterSpacing;
        var wordSpacingUnit = svgTextBase.WordSpacing;
        var hasLetterSpacingAdjustment = HasSpacingAdjustment(letterSpacingUnit) && !SuppressesLetterSpacingForRun(text);
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

            if (hasLetterSpacingAdjustment && SupportsLetterSpacing(codepoints[i]))
            {
                naturalLength += letterSpacingIsPercentage
                    ? ResolveSpacingValue(svgTextBase, letterSpacingUnit, geometryBounds, naturalAdvances[i])
                    : fixedLetterSpacing;
            }

            if (hasWordSpacingAdjustment && IsWhitespaceCodepoint(codepoints[i]))
            {
                naturalLength += wordSpacingIsPercentage
                    ? ResolveSpacingValue(svgTextBase, wordSpacingUnit, geometryBounds, naturalAdvances[i])
                    : fixedWordSpacing;
            }
        }

        var totalAdvance = naturalLength;
        placementScaleX = 1f;
        var extraGapAdvance = 0f;
        var scaleRunFromStart = false;
        var specifiedLength = TryGetOwnTextLength(svgTextBase, viewport, IsVerticalWritingMode(svgTextBase), out var ownSpecifiedLength)
            ? ownSpecifiedLength
            : 0f;
        var hasActiveTextLengthAdjustment = specifiedLength > 0f &&
                                            Math.Abs(naturalLength - specifiedLength) > TextLengthTolerance;
        if (hasActiveTextLengthAdjustment)
        {
            if (GetOwnLengthAdjust(svgTextBase) == SvgTextLengthAdjust.Spacing && codepoints.Count > 1)
            {
                extraGapAdvance = (specifiedLength - totalAdvance) / (codepoints.Count - 1);
                totalAdvance = specifiedLength;
            }
            else if (totalAdvance > 0f)
            {
                placementScaleX = specifiedLength / totalAdvance;
                scaleRunFromStart = true;
                totalAdvance = specifiedLength;
            }
        }

        var rotations = GetPositionedRotations(svgTextBase, codepoints.Count);
        var midOffsets = new List<float>(codepoints.Count);
        var advances = new List<float>(codepoints.Count);
        var rotationDegrees = new List<float>(codepoints.Count);
        var currentOffset = startOffset;
        currentVOffset = baseVOffset + GetBaselineOffset(svgTextBase, viewport, assetLoader);
        var pathLength = pathSamples[pathSamples.Count - 1].Distance;
        for (var i = 0; i < codepoints.Count; i++)
        {
            var glyphAdvance = scaleRunFromStart
                ? naturalAdvances[i] * placementScaleX
                : naturalAdvances[i];
            if (!IsValidPositiveAdvance(glyphAdvance))
            {
                glyphAdvance = 0f;
            }

            var letterSpacing = 0f;
            var wordSpacing = 0f;
            if (i < codepoints.Count - 1)
            {
                if (hasLetterSpacingAdjustment && SupportsLetterSpacing(codepoints[i]))
                {
                    letterSpacing = letterSpacingIsPercentage
                        ? ResolveSpacingValue(svgTextBase, letterSpacingUnit, geometryBounds, naturalAdvances[i])
                        : fixedLetterSpacing;
                }

                if (hasWordSpacingAdjustment && IsWhitespaceCodepoint(codepoints[i]))
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

            var glyphMidOffset = currentOffset + (glyphAdvance * 0.5f);
            var sampleOffset = glyphMidOffset;
            if (isClosedLoop && pathLength > 0f)
            {
                sampleOffset = NormalizeClosedPathDistance(glyphMidOffset, pathLength);
            }
            else if (glyphMidOffset <= 0f)
            {
                currentOffset += clusterAdvance;
                continue;
            }

            if (!isClosedLoop && glyphMidOffset >= pathLength)
            {
                break;
            }

            midOffsets.Add(sampleOffset);
            advances.Add(glyphAdvance);
            rotationDegrees.Add(GetCodepointRotationDegrees(svgTextBase, codepoints[i], rotations, i));

            if (i < codepoints.Count - 1)
            {
                currentOffset += clusterAdvance;
            }
        }

        visibleGlyphMidOffsets = midOffsets.ToArray();
        visibleGlyphAdvances = advances.ToArray();
        visibleCodepointRotationDegrees = rotationDegrees.ToArray();
    }
}
