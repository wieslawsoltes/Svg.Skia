using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using ShimSkiaSharp;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

internal static partial class SvgSceneTextCompiler
{
    private static readonly Regex s_multipleSpaces = new(@" {2,}", RegexOptions.Compiled);
    private static readonly Regex s_numberPrefix = new(@"^[+-]?(?:(?:\d+\.\d*)|(?:\d+)|(?:\.\d+))(?:[eE][+-]?\d+)?", RegexOptions.Compiled);
    private static readonly ConcurrentDictionary<SimpleCodepointAdvanceCacheKey, float> s_simpleCodepointAdvanceCache = new();
    private static readonly ConcurrentDictionary<NaturalCodepointAdvanceCacheKey, float[]> s_naturalCodepointAdvanceCache = new();
    private const int MaxEllipseSteps = 128;
    private const float FullCircleRadians = 2f * (float)Math.PI;
    private const float SyntheticSmallCapsScale = 0.75f;
    private const float TextLengthTolerance = 1.5f;
    private const int SimpleCodepointAdvanceCacheLimit = 4096;
    private const int NaturalCodepointAdvanceCacheLimit = 1024;
    private const string RawTextDecorationAttributeKey = "__svgskia:text-decoration-raw";

    private readonly record struct SequentialTextRun(SvgTextBase StyleSource, string Text);

    private readonly record struct ShapedSequentialRunSegment(SvgTextBase StyleSource, ushort[] Glyphs, SKPoint[] Points);

    private readonly record struct ResolvedSequentialCompileRun(
        SvgTextBase StyleSource,
        string DrawText,
        SKTypeface? Typeface,
        float Advance,
        SKRect RelativeBounds);

    private readonly record struct LogicalBidiRun(int StartCharIndex, int Length, int Direction);

    private readonly record struct TextPathRun(SvgTextBase StyleSource, string Text, float Dx, float Dy);

    private readonly record struct PositionedTextPathRun(SvgTextBase StyleSource, string Text, PositionedCodepointPlacement[] Placements);

    private readonly record struct PositionedCodepointRun(SvgTextBase StyleSource, string Text, PositionedCodepointPlacement[] Placements);

    private readonly record struct PathSample(SKPoint Point, float Distance, bool StartsSubpath);

    private readonly record struct ResolvedFallbackCodepoint(string Text, SKPaint Paint, float Advance);

    private readonly record struct SimpleCodepointAdvanceCacheKey(
        int AssetLoaderId,
        string Codepoint,
        float TextSize,
        bool LcdRenderText,
        bool SubpixelText,
        SKTextEncoding TextEncoding,
        string? TypefaceFamilyName,
        SKFontStyleWeight TypefaceWeight,
        SKFontStyleWidth TypefaceWidth,
        SKFontStyleSlant TypefaceSlant);

    private readonly record struct NaturalCodepointAdvanceCacheKey(
        int AssetLoaderId,
        string Text,
        float TextSize,
        bool LcdRenderText,
        bool SubpixelText,
        SKTextEncoding TextEncoding,
        string? TypefaceFamilyName,
        SKFontStyleWeight TypefaceWeight,
        SKFontStyleWidth TypefaceWidth,
        SKFontStyleSlant TypefaceSlant,
        bool RightToLeft,
        bool RequiresSyntheticSmallCaps,
        bool UsesBrowserCompatibleRunTypeface);

    private readonly record struct PositionedCodepointPlacement(SKPoint Point, float RotationDegrees, float ScaleX, float ScaleOriginX);

    private readonly record struct VerticalTextRunPlacement(string Text, PositionedCodepointPlacement Placement, float Advance);

    private readonly record struct TextDecorationLayer(SvgVisualElement PaintSource, SvgTextBase MetricsSource, SvgTextDecoration Decorations);

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

    private sealed class FlattenedTextCodepoint
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
        SvgSceneCompiler.AssignRetainedVisualState(node, svgTextBase);
        SvgSceneCompiler.AssignRetainedResourceKeys(node, svgTextBase, getElementAddressKey);
        node.SupportsFillHitTest = SvgScenePaintingService.IsValidFill(svgTextBase);
        node.SupportsStrokeHitTest = SvgScenePaintingService.IsValidStroke(svgTextBase, SKRect.Empty);

        if (TryCompileSequentialText(svgTextBase, viewport, ignoreAttributes, assetLoader, out var compiledGeometryBounds, out var sequentialModel))
        {
            node.GeometryBounds = compiledGeometryBounds;
            node.TransformedBounds = node.TotalTransform.MapRect(compiledGeometryBounds);
            node.LocalModel = sequentialModel;
            if (node.LocalModel is null)
            {
                node.IsRenderable = false;
            }

            return true;
        }

        var geometryBounds = EstimateGeometryBounds(svgTextBase, viewport, assetLoader);
        node.GeometryBounds = geometryBounds;
        node.TransformedBounds = node.TotalTransform.MapRect(geometryBounds);

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
            geometryBounds);
        var localModel = recorder.EndRecording();
        node.LocalModel = localModel.Commands is { Count: > 0 } ? localModel : null;

        if (node.LocalModel is null)
        {
            node.IsRenderable = false;
        }

        return true;
    }

    private static bool TryCompileSequentialText(
        SvgTextBase svgTextBase,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        ISvgAssetLoader assetLoader,
        out SKRect geometryBounds,
        out SKPicture? localModel)
    {
        geometryBounds = SKRect.Empty;
        localModel = null;

        if (HasSequentialTextRunBarriers(svgTextBase) ||
            IsVerticalWritingMode(svgTextBase) ||
            !TryCollectSequentialTextRuns(svgTextBase, requireAnchorContent: false, IsTextReferenceRenderingEnabled(assetLoader), trimLeadingWhitespaceAtStart: true, out var runs) ||
            runs.Count == 0 ||
            !CanUseSequentialCompileFastPath(runs) ||
            !TryResolveSequentialCompileRuns(runs, viewport, assetLoader, out var resolvedRuns))
        {
            return false;
        }

        var x = svgTextBase.X.Count >= 1
            ? svgTextBase.X[0].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, viewport)
            : 0f;
        var y = svgTextBase.Y.Count >= 1
            ? svgTextBase.Y[0].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, viewport)
            : 0f;
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport);
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
        DrawResolvedSequentialCompileRuns(resolvedRuns, inlineOrigin, currentY, geometryBounds, ignoreAttributes, canvas, assetLoader);
        var recordedModel = recorder.EndRecording();
        localModel = recordedModel.Commands is { Count: > 0 } ? recordedModel : null;
        return true;
    }

    private static bool CanUseSequentialCompileFastPath(IReadOnlyList<SequentialTextRun> runs)
    {
        for (var i = 0; i < runs.Count; i++)
        {
            if (!HasGenericSequentialCompileFontFamily(runs[i].StyleSource) ||
                !IsSimpleAsciiSequentialCompileText(runs[i].Text))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasGenericSequentialCompileFontFamily(SvgTextBase svgTextBase)
    {
        var fontFamily = svgTextBase.FontFamily;
        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            return true;
        }

        var families = fontFamily.Split([','], StringSplitOptions.RemoveEmptyEntries);
        if (families.Length == 0)
        {
            return true;
        }

        for (var i = 0; i < families.Length; i++)
        {
            var family = families[i].Trim().Trim('\'', '"');
            if (!family.Equals("sans-serif", StringComparison.OrdinalIgnoreCase) &&
                !family.Equals("serif", StringComparison.OrdinalIgnoreCase) &&
                !family.Equals("monospace", StringComparison.OrdinalIgnoreCase) &&
                !family.Equals("cursive", StringComparison.OrdinalIgnoreCase) &&
                !family.Equals("fantasy", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
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
        var metricsPaint = CreateTextMetricsPaint(run.StyleSource, geometryBounds);
        var fallbackText = GetBrowserCompatibleFallbackText(run.StyleSource, run.Text, assetLoader);
        if (string.IsNullOrEmpty(fallbackText))
        {
            return false;
        }

        if (!TryCreateBrowserCompatibleFullRunPaint(run.StyleSource, fallbackText, metricsPaint, assetLoader, out var fullRunPaint, out var shapedText))
        {
            return false;
        }

        var measureBounds = new SKRect();
        var advance = EnsureWhitespaceAdvance(fallbackText, fullRunPaint, assetLoader, assetLoader.MeasureText(shapedText, fullRunPaint, ref measureBounds));
        var relativeBounds = measureBounds;
        if (relativeBounds.IsEmpty)
        {
            relativeBounds = GetTextAdvanceBox(run.StyleSource, 0f, 0f, advance, fullRunPaint, assetLoader);
        }
        else
        {
            relativeBounds = ExpandTextBoundsWithAdvanceBox(run.StyleSource, relativeBounds, 0f, 0f, advance, fullRunPaint, assetLoader);
        }

        resolvedRun = new ResolvedSequentialCompileRun(run.StyleSource, shapedText, fullRunPaint.Typeface, advance, relativeBounds);
        return true;
    }

    private static void DrawResolvedSequentialCompileRuns(
        IReadOnlyList<ResolvedSequentialCompileRun> resolvedRuns,
        float startX,
        float startY,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader)
    {
        var currentX = startX;
        var currentY = startY;
        for (var i = 0; i < resolvedRuns.Count; i++)
        {
            var run = resolvedRuns[i];
            if (SvgScenePaintingService.IsValidFill(run.StyleSource))
            {
                var fillPaint = SvgScenePaintingService.GetFillPaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes);
                if (fillPaint is not null)
                {
                    PaintingService.SetPaintText(run.StyleSource, geometryBounds, fillPaint);
                    fillPaint.TextAlign = SKTextAlign.Left;
                    fillPaint.Typeface = run.Typeface;
                    canvas.DrawText(run.DrawText, currentX, currentY, fillPaint);
                }
            }

            if (SvgScenePaintingService.IsValidStroke(run.StyleSource, geometryBounds))
            {
                var strokePaint = SvgScenePaintingService.GetStrokePaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes);
                if (strokePaint is not null)
                {
                    PaintingService.SetPaintText(run.StyleSource, geometryBounds, strokePaint);
                    strokePaint.TextAlign = SKTextAlign.Left;
                    strokePaint.Typeface = run.Typeface;
                    canvas.DrawText(run.DrawText, currentX, currentY, strokePaint);
                }
            }

            ApplyInlineAdvance(run.StyleSource, ref currentX, ref currentY, run.Advance);
        }
    }

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
        SKRect geometryBounds)
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

        DrawTextBase(svgTextBase, ref currentX, ref currentY, viewport, ignoreAttributes, canvas, assetLoader, references, geometryBounds, inheritedRotationState: null, inheritedAbsolutePositionState: null, trimLeadingWhitespaceAtStart: true);
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
        RotationState? inheritedRotationState,
        AbsolutePositionState? inheritedAbsolutePositionState,
        bool trimLeadingWhitespaceAtStart)
    {
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport);
        var localCurrentX = currentX + baselineShift.X;
        var localCurrentY = currentY + baselineShift.Y;
        var rotationState = ResolveRotationState(svgTextBase, inheritedRotationState);
        var absolutePositionState = ResolveAbsolutePositionState(svgTextBase, inheritedAbsolutePositionState, viewport);

        if (TryDrawFlattenedTextLengthLayout(svgTextBase, ref localCurrentX, ref localCurrentY, viewport, ignoreAttributes, canvas, assetLoader, rootGeometryBounds, trimLeadingWhitespaceAtStart))
        {
            currentX = localCurrentX - baselineShift.X;
            currentY = localCurrentY - baselineShift.Y;
            return;
        }

        if (inheritedRotationState is null &&
            inheritedAbsolutePositionState is null &&
            TryDrawSequentialTextRuns(svgTextBase, ref localCurrentX, ref localCurrentY, viewport, rootGeometryBounds, ignoreAttributes, canvas, assetLoader, trimLeadingWhitespaceAtStart))
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
            rotationState,
            absolutePositionState);
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
        if (HasSequentialTextRunBarriers(svgTextBase))
        {
            return false;
        }

        if (!TryCollectSequentialTextRuns(svgTextBase, requireAnchorContent: false, IsTextReferenceRenderingEnabled(assetLoader), trimLeadingWhitespaceAtStart, out var runs))
        {
            return false;
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
                AppendTextStringPathAlignedLeft(runs[i].StyleSource, runs[i].Text, ref startAlignedX, ref startAlignedY, geometryBounds, assetLoader, path);
            }

            currentX = startAlignedX;
            currentY = startAlignedY;
            return true;
        }

        var totalAdvance = MeasureSequentialTextRuns(runs, geometryBounds, assetLoader);
        var inlineOrigin = GetAlignedStartCoordinate(isVertical ? currentY : currentX, totalAdvance, textAlign);
        var drawX = isVertical ? currentX : inlineOrigin;
        var drawY = isVertical ? inlineOrigin : currentY;

        for (var i = 0; i < runs.Count; i++)
        {
            AppendTextStringPathAlignedLeft(runs[i].StyleSource, runs[i].Text, ref drawX, ref drawY, geometryBounds, assetLoader, path);
        }

        if (isVertical)
        {
            currentX = drawX;
            currentY = inlineOrigin + totalAdvance;
        }
        else
        {
            currentX = inlineOrigin + totalAdvance;
            currentY = drawY;
        }

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
                        svgTextBase.SpaceHandling != XmlSpaceHandling.Preserve &&
                        !string.IsNullOrEmpty(text) &&
                        text![0] == ' ')
                    {
                        text = text.TrimStart(' ');
                    }

                    if (string.IsNullOrEmpty(text) &&
                        !string.IsNullOrWhiteSpace(rawContent) &&
                        svgTextBase.SpaceHandling != XmlSpaceHandling.Preserve &&
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
                        previousEndedWithSpace = text.EndsWith(" ", StringComparison.Ordinal);
                        absolutePositionState?.Consume(codepointCount);
                        break;
                    }

                    var x = useInitialPosition && xs.Count >= 1 ? xs[0] : currentX;
                    var y = useInitialPosition && ys.Count >= 1 ? ys[0] : currentY;
                    var dx = useInitialPosition && dxs.Count >= 1 ? dxs[0] : 0f;
                    var dy = useInitialPosition && dys.Count >= 1 ? dys[0] : 0f;
                    currentX = x + dx;
                    currentY = y + dy;
                    AppendTextStringPath(svgTextBase, text!, currentX, currentY, rootGeometryBounds, assetLoader, path, rotations);
                    MeasureTextStringBounds(svgTextBase, text!, currentX, currentY, rootGeometryBounds, assetLoader, rotations, out var advance);
                    ApplyInlineAdvance(svgTextBase, ref currentX, ref currentY, advance);
                    useInitialPosition = false;
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = text.EndsWith(" ", StringComparison.Ordinal);
                    absolutePositionState?.Consume(codepointCount);
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
                            SvgService.HasRecursiveReference(svgTextRef, static e => e.ReferencedElement, new HashSet<Uri>()) ||
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
                            previousEndedWithSpace = referencedClipText.EndsWith(" ", StringComparison.Ordinal);
                            absolutePositionState?.Consume(referencedCodepointCount);
                            break;
                        }

                        var referencedClipX = useInitialPosition && referencedXs.Count >= 1 ? referencedXs[0] : currentX;
                        var referencedClipY = useInitialPosition && referencedYs.Count >= 1 ? referencedYs[0] : currentY;
                        var referencedClipDx = useInitialPosition && referencedDxs.Count >= 1 ? referencedDxs[0] : 0f;
                        var referencedClipDy = useInitialPosition && referencedDys.Count >= 1 ? referencedDys[0] : 0f;
                        currentX = referencedClipX + referencedClipDx;
                        currentY = referencedClipY + referencedClipDy;
                        AppendTextStringPath(svgTextRef, referencedClipText!, currentX, currentY, rootGeometryBounds, assetLoader, path, referencedClipRotations);
                        MeasureTextStringBounds(svgTextRef, referencedClipText!, currentX, currentY, rootGeometryBounds, assetLoader, referencedClipRotations, out var referencedClipStringAdvance);
                        ApplyInlineAdvance(svgTextRef, ref currentX, ref currentY, referencedClipStringAdvance);
                        useInitialPosition = false;
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = referencedClipText.EndsWith(" ", StringComparison.Ordinal);
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
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport);
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
                    if (!CanRenderTextSubtree(svgAnchor, ignoreAttributes))
                    {
                        break;
                    }

                    var anchorStyleSource = CreateAnchorTextStyleSource(svgAnchor);
                    DrawTextNodes(GetContentNodeList(svgAnchor), anchorStyleSource, ref currentX, ref currentY, ref useInitialPosition, ref trimLeadingWhitespace, ref previousEndedWithSpace, viewport, ignoreAttributes, canvas, assetLoader, references, rootGeometryBounds, rotationState, absolutePositionState);
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
                        svgTextBase.SpaceHandling != XmlSpaceHandling.Preserve &&
                        !string.IsNullOrEmpty(text) &&
                        text![0] == ' ')
                    {
                        text = text.TrimStart(' ');
                    }

                    if (string.IsNullOrEmpty(text) &&
                        !string.IsNullOrWhiteSpace(rawContent) &&
                        svgTextBase.SpaceHandling != XmlSpaceHandling.Preserve &&
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
                        var fillAdvance = 0f;
                        if (SvgScenePaintingService.IsValidFill(svgTextBase))
                        {
                            var fillPaint = SvgScenePaintingService.GetFillPaint(svgTextBase, rootGeometryBounds, assetLoader, ignoreAttributes);
                            if (fillPaint is not null)
                            {
                                fillAdvance = DrawPositionedTextRuns(svgTextBase, text!, positionedPoints, rootGeometryBounds, fillPaint, canvas, assetLoader, rotations);
                            }
                        }

                        var strokeAdvance = 0f;
                        if (SvgScenePaintingService.IsValidStroke(svgTextBase, rootGeometryBounds))
                        {
                            var strokePaint = SvgScenePaintingService.GetStrokePaint(svgTextBase, rootGeometryBounds, assetLoader, ignoreAttributes);
                            if (strokePaint is not null)
                            {
                                strokeAdvance = DrawPositionedTextRuns(svgTextBase, text!, positionedPoints, rootGeometryBounds, strokePaint, canvas, assetLoader, rotations);
                            }
                        }

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
                                assetLoader);
                        }

                        MoveToAfterPositionedRun(svgTextBase, positionedPoints[positionedPoints.Length - 1], Math.Max(fillAdvance, strokeAdvance), out currentX, out currentY);
                        useInitialPosition = false;
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = text.EndsWith(" ", StringComparison.Ordinal);
                        absolutePositionState?.Consume(codepointCount);
                        break;
                    }

                    var x = useInitialPosition && xs.Count >= 1 ? xs[0] : currentX;
                    var y = useInitialPosition && ys.Count >= 1 ? ys[0] : currentY;
                    var dx = useInitialPosition && dxs.Count >= 1 ? dxs[0] : 0f;
                    var dy = useInitialPosition && dys.Count >= 1 ? dys[0] : 0f;
                    currentX = x + dx;
                    currentY = y + dy;
                    DrawTextString(svgTextBase, text!, ref currentX, ref currentY, rootGeometryBounds, ignoreAttributes, canvas, assetLoader, references, rotations);
                    useInitialPosition = false;
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = text.EndsWith(" ", StringComparison.Ordinal);
                    absolutePositionState?.Consume(codepointCount);
                    break;

                case SvgTextPath svgTextPath:
                    if (!CanRenderTextSubtree(svgTextPath, ignoreAttributes))
                    {
                        break;
                    }

                    var drewTextPath = DrawTextPath(svgTextPath, ref currentX, ref currentY, useInitialPosition, viewport, ignoreAttributes, canvas, assetLoader, references);
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
                            SvgService.HasRecursiveReference(svgTextRef, static e => e.ReferencedElement, new HashSet<Uri>()) ||
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
                            var fillAdvance = 0f;
                            if (SvgScenePaintingService.IsValidFill(svgTextRef))
                            {
                                var fillPaint = SvgScenePaintingService.GetFillPaint(svgTextRef, rootGeometryBounds, assetLoader, ignoreAttributes);
                                if (fillPaint is not null)
                                {
                                    fillAdvance = DrawPositionedTextRuns(svgTextRef, referencedText!, referencedPoints, rootGeometryBounds, fillPaint, canvas, assetLoader, referencedRotations);
                                }
                            }

                            var strokeAdvance = 0f;
                            if (SvgScenePaintingService.IsValidStroke(svgTextRef, rootGeometryBounds))
                            {
                                var strokePaint = SvgScenePaintingService.GetStrokePaint(svgTextRef, rootGeometryBounds, assetLoader, ignoreAttributes);
                                if (strokePaint is not null)
                                {
                                    strokeAdvance = DrawPositionedTextRuns(svgTextRef, referencedText!, referencedPoints, rootGeometryBounds, strokePaint, canvas, assetLoader, referencedRotations);
                                }
                            }

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
                                    assetLoader);
                            }

                            MoveToAfterPositionedRun(svgTextRef, referencedPoints[referencedPoints.Length - 1], Math.Max(fillAdvance, strokeAdvance), out currentX, out currentY);
                            useInitialPosition = false;
                            trimLeadingWhitespace = false;
                            previousEndedWithSpace = referencedText.EndsWith(" ", StringComparison.Ordinal);
                            absolutePositionState?.Consume(referencedCodepointCount);
                            break;
                        }

                        var referencedX = useInitialPosition && referencedXs.Count >= 1 ? referencedXs[0] : currentX;
                        var referencedY = useInitialPosition && referencedYs.Count >= 1 ? referencedYs[0] : currentY;
                        var referencedDx = useInitialPosition && referencedDxs.Count >= 1 ? referencedDxs[0] : 0f;
                        var referencedDy = useInitialPosition && referencedDys.Count >= 1 ? referencedDys[0] : 0f;
                        currentX = referencedX + referencedDx;
                        currentY = referencedY + referencedDy;
                        DrawTextString(svgTextRef, referencedText!, ref currentX, ref currentY, rootGeometryBounds, ignoreAttributes, canvas, assetLoader, references, referencedRotations);
                        useInitialPosition = false;
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = referencedText.EndsWith(" ", StringComparison.Ordinal);
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

    private static bool TryDrawFlattenedTextLengthLayout(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        SKRect geometryBounds,
        bool trimLeadingWhitespaceAtStart)
    {
        if (!TryCreateFlattenedTextLengthRuns(svgTextBase, currentX, currentY, viewport, geometryBounds, assetLoader, trimLeadingWhitespaceAtStart, out var runs, out var totalAdvance, out var finalY))
        {
            return false;
        }

        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (SvgScenePaintingService.IsValidFill(run.StyleSource))
            {
                var fillPaint = SvgScenePaintingService.GetFillPaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes);
                if (fillPaint is not null)
                {
                    _ = DrawCodepointPlacements(run.StyleSource, run.Text, run.Placements, geometryBounds, fillPaint, canvas, assetLoader);
                }
            }

            if (SvgScenePaintingService.IsValidStroke(run.StyleSource, geometryBounds))
            {
                var strokePaint = SvgScenePaintingService.GetStrokePaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes);
                if (strokePaint is not null)
                {
                    _ = DrawCodepointPlacements(run.StyleSource, run.Text, run.Placements, geometryBounds, strokePaint, canvas, assetLoader);
                }
            }

            DrawTextDecorations(
                ResolveTextDecorationLayers(run.StyleSource),
                run.StyleSource,
                run.Text,
                run.Placements,
                geometryBounds,
                ignoreAttributes,
                canvas,
                assetLoader);
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
        float[]? rotations)
    {
        var fillAdvance = 0f;
        if (SvgScenePaintingService.IsValidFill(svgTextBase))
        {
            var fillPaint = SvgScenePaintingService.GetFillPaint(svgTextBase, geometryBounds, assetLoader, ignoreAttributes);
            if (fillPaint is not null)
            {
                fillAdvance = DrawTextRuns(svgTextBase, text, x, y, geometryBounds, fillPaint, canvas, assetLoader, rotations);
            }
        }

        var strokeAdvance = 0f;
        if (SvgScenePaintingService.IsValidStroke(svgTextBase, geometryBounds))
        {
            var strokePaint = SvgScenePaintingService.GetStrokePaint(svgTextBase, geometryBounds, assetLoader, ignoreAttributes);
            if (strokePaint is not null)
            {
                strokeAdvance = DrawTextRuns(svgTextBase, text, x, y, geometryBounds, strokePaint, canvas, assetLoader, rotations);
            }
        }

        DrawResolvedTextDecorations(svgTextBase, text, x, y, geometryBounds, ignoreAttributes, canvas, assetLoader, rotations, forceLeftAlign: false);
        ApplyInlineAdvance(svgTextBase, ref x, ref y, Math.Max(strokeAdvance, fillAdvance));
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

        if (TryCreateVerticalTextRunPlacements(svgTextBase, text, anchorX, anchorY, geometryBounds, textAlign, assetLoader, rotations, out var verticalPlacements, out var verticalAdvance))
        {
            _ = AppendVerticalTextRunPlacementsPath(svgTextBase, verticalPlacements, geometryBounds, assetLoader, targetPath);
            return verticalAdvance;
        }

        if (TryCreateAlignedCodepointPlacements(
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
        if (TryCreateVerticalTextRunPlacements(svgTextBase, text, anchorX, anchorY, geometryBounds, textAlign, assetLoader, rotations, out var verticalPlacements, out var verticalAdvance))
        {
            _ = DrawVerticalTextRunPlacements(svgTextBase, verticalPlacements, geometryBounds, paint, canvas, assetLoader);
            return verticalAdvance;
        }

        if (TryCreateAlignedCodepointPlacements(svgTextBase, text, anchorX, anchorY, geometryBounds, textAlign, assetLoader, rotations, out var placements, out var totalAdvance))
        {
            _ = DrawCodepointPlacements(svgTextBase, text, placements, geometryBounds, paint, canvas, assetLoader);
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
            var fullRunMeasureBounds = new SKRect();
            var fullRunAdvance = EnsureWhitespaceAdvance(
                fallbackText,
                fullRunPaint,
                assetLoader,
                assetLoader.MeasureText(shapedText, fullRunPaint, ref fullRunMeasureBounds));
            var fullRunStartX = GetAlignedStartX(anchorX, fullRunAdvance, textAlign);

            fullRunPaint.TextAlign = SKTextAlign.Left;
            canvas.DrawText(shapedText, fullRunStartX, anchorY, fullRunPaint);
            return fullRunAdvance;
        }

        var typefaceSpans = assetLoader.FindTypefaces(fallbackText, paint);
        var naturalTotalAdvance = 0f;
        if (typefaceSpans.Count == 0)
        {
            var scratchBounds = new SKRect();
            naturalTotalAdvance = assetLoader.MeasureText(fallbackText, paint, ref scratchBounds);
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
        if (typefaceSpans.Count == 0)
        {
            canvas.DrawText(ApplyBrowserCompatibleBidiControls(svgTextBase, fallbackText), currentX, anchorY, paint);
            return naturalTotalAdvance;
        }

        var isRightToLeft = IsRightToLeft(svgTextBase);
        var startIndex = isRightToLeft ? typefaceSpans.Count - 1 : 0;
        var endIndex = isRightToLeft ? -1 : typefaceSpans.Count;
        var step = isRightToLeft ? -1 : 1;
        for (var i = startIndex; i != endIndex; i += step)
        {
            var typefaceSpan = typefaceSpans[i];
            paint.Typeface = typefaceSpan.Typeface;
            canvas.DrawText(ApplyBrowserCompatibleBidiControls(svgTextBase, typefaceSpan.Text), currentX, anchorY, paint);
            currentX += typefaceSpan.Advance;
            paint = paint.Clone();
        }

        return naturalTotalAdvance;
    }

    private static bool IsRightToLeft(SvgTextBase svgTextBase)
    {
        return PaintingService.IsRightToLeft(svgTextBase);
    }

    private static bool IsVerticalWritingMode(SvgTextBase svgTextBase)
    {
        return PaintingService.IsVerticalWritingMode(svgTextBase);
    }

    private static void ApplyInlineAdvance(SvgTextBase svgTextBase, ref float currentX, ref float currentY, float advance)
    {
        if (IsVerticalWritingMode(svgTextBase))
        {
            currentY += advance;
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

    private static SKPoint GetBaselineShiftVector(SvgTextBase svgTextBase, SKRect viewport)
    {
        var baselineShift = GetBaselineShift(svgTextBase, viewport);
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
            glyphOrientation = glyphOrientation.Trim();
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

        var codepoints = SplitCodepoints(text);
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

        var currentY = 0f;
        placements = new VerticalTextRunPlacement[segments.Count];
        for (var i = 0; i < segments.Count; i++)
        {
            var segmentAdvance = MeasureNaturalTextAdvanceHorizontal(svgTextBase, segments[i].Text, geometryBounds, assetLoader);
            var tempPlacement = new PositionedCodepointPlacement(new SKPoint(0f, 0f), segments[i].Rotation, 1f, 0f);
            var tempRun = new VerticalTextRunPlacement(segments[i].Text, tempPlacement, segmentAdvance);
            var tempBounds = MeasureVerticalTextRunPlacementsBounds(svgTextBase, new[] { tempRun }, geometryBounds, assetLoader, out _);
            var placementX = -((tempBounds.Left + tempBounds.Right) * 0.5f);
            var placementY = currentY - tempBounds.Top;
            var placement = new PositionedCodepointPlacement(new SKPoint(placementX, placementY), segments[i].Rotation, 1f, placementX);
            placements[i] = new VerticalTextRunPlacement(segments[i].Text, placement, segmentAdvance);
            currentY += segmentAdvance;
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
        if (!assetLoader.EnableSvgFonts)
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

    private static float DrawCodepointPlacements(
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement[] placements,
        SKRect geometryBounds,
        SKPaint paint,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader)
    {
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

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
                advance = svgFontLayout.Advance * placement.ScaleX;
                continue;
            }

            var resolved = ResolveFallbackCodepoint(svgTextBase, codepoint, localPaint, assetLoader);
            DrawPositionedText(resolved.Text, placement, resolved.Paint, canvas);
            advance = resolved.Advance * placement.ScaleX;
        }

        return advance;
    }

    private static void AppendCodepointPlacementsPath(
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement[] placements,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        SKPath path)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

        var placementIndex = 0;
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            var placement = placements[placementIndex++];
            var localPaint = paint.Clone();
            if (SvgFontTextRenderer.TryGetLayout(svgTextBase, codepoint, localPaint, assetLoader, out var svgFontLayout) &&
                svgFontLayout is not null)
            {
                AppendPositionedLayoutPath(path, svgFontLayout, placement);
                continue;
            }

            var resolved = ResolveFallbackCodepoint(svgTextBase, codepoint, localPaint, assetLoader);
            AppendPositionedTextPath(path, resolved.Text, placement, resolved.Paint, assetLoader);
        }
    }

    private static SKRect MeasureCodepointPlacementBounds(
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement[] placements,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out float advance)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

        var bounds = SKRect.Empty;
        advance = 0f;

        var placementIndex = 0;
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            var placement = placements[placementIndex++];
            if (TryMeasurePositionedCodepointBounds(svgTextBase, codepoint, placement, paint, assetLoader, out var candidateBounds, out var candidateAdvance))
            {
                UnionBounds(ref bounds, candidateBounds);
                advance = candidateAdvance;
            }
        }

        return bounds;
    }

    private static bool TryMeasurePositionedCodepointBounds(
        SvgTextBase svgTextBase,
        string codepoint,
        PositionedCodepointPlacement placement,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        out SKRect bounds,
        out float advance)
    {
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

        var resolved = ResolveFallbackCodepoint(svgTextBase, codepoint, localPaint, assetLoader);
        if (TryGetRenderedTextLocalBounds(resolved.Text, resolved.Paint, assetLoader, out var glyphBounds))
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
        out float leftOffset,
        out float rightOffset)
    {
        leftOffset = 0f;
        rightOffset = 0f;

        var localPaint = paint.Clone();
        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, codepoint, localPaint, assetLoader, out var svgFontLayout) &&
            svgFontLayout is not null)
        {
            leftOffset = 0f;
            rightOffset = svgFontLayout.Advance;
            return rightOffset > leftOffset;
        }

        var resolved = ResolveFallbackCodepoint(svgTextBase, codepoint, localPaint, assetLoader);
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
        var path = assetLoader.GetTextPath(text, paint, 0f, 0f);
        if (path is not null && !path.IsEmpty)
        {
            bounds = path.Bounds;
            return !bounds.IsEmpty;
        }

        bounds = new SKRect();
        assetLoader.MeasureText(text, paint, ref bounds);
        return !bounds.IsEmpty;
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
        HashSet<Uri>? references)
    {
        if (!HasFeatures(svgTextPath, ignoreAttributes) ||
            !MaskingService.CanDraw(svgTextPath, ignoreAttributes) ||
            SvgService.HasRecursiveReference(svgTextPath, static e => e.ReferencedPath, new HashSet<Uri>()))
        {
            return TextPathRenderResult.NotRendered;
        }

        if (!TryResolveTextPathGeometry(svgTextPath, viewport, out var svgPath, out var skPath, out var geometryBounds, out var pathSamples, out var pathLength))
        {
            return TextPathRenderResult.MissingGeometry;
        }

        if (!TryCollectTextPathRuns(svgTextPath, viewport, out var runs))
        {
            return TextPathRenderResult.NotRendered;
        }

        ResolveTextPathChunkOffsets(svgTextPath, useCurrentPositionOffset, currentX, currentY, viewport, assetLoader, pathSamples, out var horizontalOffset, out var verticalOffset);
        var startOffset = horizontalOffset + ResolveTextPathStartOffset(svgTextPath, svgPath, skPath, viewport, pathLength);
        var totalAdvance = MeasureTextPathRunsAdvance(runs, geometryBounds, assetLoader);
        var hOffset = ApplyTextAnchor(svgTextPath, startOffset, geometryBounds, totalAdvance);

        if (!TryCreateTextPathRunPlacements(runs, pathSamples, hOffset, verticalOffset, viewport, geometryBounds, assetLoader, out var positionedRuns, out var endOffset, out var endVOffset))
        {
            return TextPathRenderResult.NotRendered;
        }

        DrawPositionedTextPathRuns(positionedRuns, viewport, geometryBounds, ignoreAttributes, canvas, assetLoader, references);
        AdvanceTextPathPosition(pathSamples, pathLength, endVOffset, ref currentX, ref currentY);
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
        if (SvgService.HasRecursiveReference(svgTextPath, static e => e.ReferencedPath, new HashSet<Uri>()))
        {
            return TextPathRenderResult.NotRendered;
        }

        if (!TryResolveTextPathGeometry(svgTextPath, viewport, out var svgPath, out var skPath, out var geometryBounds, out var pathSamples, out var pathLength))
        {
            return TextPathRenderResult.MissingGeometry;
        }

        if (!TryCollectTextPathRuns(svgTextPath, viewport, out var runs) || runs.Count == 0)
        {
            return TextPathRenderResult.NotRendered;
        }

        ResolveTextPathChunkOffsets(svgTextPath, useCurrentPositionOffset, currentX, currentY, viewport, assetLoader, pathSamples, out var horizontalOffset, out var verticalOffset);
        var startOffset = horizontalOffset + ResolveTextPathStartOffset(svgTextPath, svgPath, skPath, viewport, pathLength);
        var totalAdvance = MeasureTextPathRunsAdvance(runs, geometryBounds, assetLoader);
        var hOffset = ApplyTextAnchor(svgTextPath, startOffset, geometryBounds, totalAdvance);

        if (!TryCreateTextPathRunPlacements(runs, pathSamples, hOffset, verticalOffset, viewport, geometryBounds, assetLoader, out var positionedRuns, out var endOffset, out var endVOffset))
        {
            return TextPathRenderResult.NotRendered;
        }

        for (var i = 0; i < positionedRuns.Count; i++)
        {
            AppendCodepointPlacementsPath(positionedRuns[i].StyleSource, positionedRuns[i].Text, positionedRuns[i].Placements, geometryBounds, assetLoader, path);
        }

        AdvanceTextPathPosition(pathSamples, pathLength, endVOffset, ref currentX, ref currentY);
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
        RotationState? rotationState)
    {
        if (!IsTextReferenceRenderingEnabled(assetLoader))
        {
            return;
        }

        if (!HasFeatures(svgTextRef, ignoreAttributes) ||
            !MaskingService.CanDraw(svgTextRef, ignoreAttributes) ||
            SvgService.HasRecursiveReference(svgTextRef, static e => e.ReferencedElement, new HashSet<Uri>()))
        {
            return;
        }

        var svgReferencedText = SvgService.GetReference<SvgTextBase>(svgTextRef, svgTextRef.ReferencedElement);
        if (svgReferencedText is null)
        {
            return;
        }

        DrawTextBase(svgReferencedText, ref currentX, ref currentY, viewport, ignoreAttributes, canvas, assetLoader, references, rootGeometryBounds, rotationState, inheritedAbsolutePositionState: null, trimLeadingWhitespaceAtStart: true);
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

        if (SvgService.HasRecursiveReference(svgTextRef, static e => e.ReferencedElement, new HashSet<Uri>()))
        {
            return;
        }

        var svgReferencedText = SvgService.GetReference<SvgTextBase>(svgTextRef, svgTextRef.ReferencedElement);
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
        var baselineShift = GetBaselineShiftVector(svgTextBase, viewport);
        var localCurrentX = currentX + baselineShift.X;
        var localCurrentY = currentY + baselineShift.Y;
        var rotationState = ResolveRotationState(svgTextBase, inheritedRotationState);
        var absolutePositionState = ResolveAbsolutePositionState(svgTextBase, inheritedAbsolutePositionState, viewport);

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
                        svgTextBase.SpaceHandling != XmlSpaceHandling.Preserve &&
                        !string.IsNullOrEmpty(text) &&
                        text![0] == ' ')
                    {
                        text = text.TrimStart(' ');
                    }

                    if (string.IsNullOrEmpty(text) &&
                        !string.IsNullOrWhiteSpace(rawContent) &&
                        svgTextBase.SpaceHandling != XmlSpaceHandling.Preserve &&
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
                        previousEndedWithSpace = text.EndsWith(" ", StringComparison.Ordinal);
                        absolutePositionState?.Consume(codepointCount);
                        break;
                    }

                    var x = useInitialPosition && xs.Count >= 1 ? xs[0] : currentX;
                    var y = useInitialPosition && ys.Count >= 1 ? ys[0] : currentY;
                    var dx = useInitialPosition && dxs.Count >= 1 ? dxs[0] : 0f;
                    var dy = useInitialPosition && dys.Count >= 1 ? dys[0] : 0f;
                    currentX = x + dx;
                    currentY = y + dy;

                    var textBounds = MeasureTextStringBounds(svgTextBase, text!, currentX, currentY, viewport, assetLoader, rotations, out var advance);
                    UnionBounds(ref bounds, textBounds);
                    ApplyInlineAdvance(svgTextBase, ref currentX, ref currentY, advance);
                    useInitialPosition = false;
                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = text.EndsWith(" ", StringComparison.Ordinal);
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
                            SvgService.HasRecursiveReference(svgTextRef, static e => e.ReferencedElement, new HashSet<Uri>()) ||
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
                            previousEndedWithSpace = referencedMeasureText.EndsWith(" ", StringComparison.Ordinal);
                            absolutePositionState?.Consume(referencedCodepointCount);
                            break;
                        }

                        var referencedMeasureX = useInitialPosition && referencedXs.Count >= 1 ? referencedXs[0] : currentX;
                        var referencedMeasureY = useInitialPosition && referencedYs.Count >= 1 ? referencedYs[0] : currentY;
                        var referencedMeasureDx = useInitialPosition && referencedDxs.Count >= 1 ? referencedDxs[0] : 0f;
                        var referencedMeasureDy = useInitialPosition && referencedDys.Count >= 1 ? referencedDys[0] : 0f;
                        currentX = referencedMeasureX + referencedMeasureDx;
                        currentY = referencedMeasureY + referencedMeasureDy;

                        var referencedMeasuredBounds = MeasureTextStringBounds(svgTextRef, referencedMeasureText!, currentX, currentY, viewport, assetLoader, referencedMeasureRotations, out var referencedMeasureAdvance);
                        UnionBounds(ref bounds, referencedMeasuredBounds);
                        ApplyInlineAdvance(svgTextRef, ref currentX, ref currentY, referencedMeasureAdvance);
                        useInitialPosition = false;
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = referencedMeasureText.EndsWith(" ", StringComparison.Ordinal);
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

        for (var i = 0; i < runs.Count; i++)
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
        if (SvgService.HasRecursiveReference(svgTextPath, static e => e.ReferencedPath, new HashSet<Uri>()))
        {
            return TextPathRenderResult.NotRendered;
        }

        if (!TryResolveTextPathGeometry(svgTextPath, viewport, out var svgPath, out var skPath, out var geometryBounds, out var pathSamples, out var pathLength))
        {
            return TextPathRenderResult.MissingGeometry;
        }

        if (!TryCollectTextPathRuns(svgTextPath, viewport, out var runs) || runs.Count == 0)
        {
            return TextPathRenderResult.NotRendered;
        }

        ResolveTextPathChunkOffsets(svgTextPath, useCurrentPositionOffset, currentX, currentY, viewport, assetLoader, pathSamples, out var horizontalOffset, out var verticalOffset);
        var startOffset = horizontalOffset + ResolveTextPathStartOffset(svgTextPath, svgPath, skPath, viewport, pathLength);
        var totalAdvance = MeasureTextPathRunsAdvance(runs, geometryBounds, assetLoader);
        var hOffset = ApplyTextAnchor(svgTextPath, startOffset, geometryBounds, totalAdvance);

        if (!TryCreateTextPathRunPlacements(runs, pathSamples, hOffset, verticalOffset, viewport, geometryBounds, assetLoader, out var positionedRuns, out var endOffset, out var endVOffset))
        {
            return TextPathRenderResult.NotRendered;
        }

        for (var i = 0; i < positionedRuns.Count; i++)
        {
            var runBounds = MeasureCodepointPlacementBounds(positionedRuns[i].StyleSource, positionedRuns[i].Text, positionedRuns[i].Placements, geometryBounds, assetLoader, out _);
            UnionBounds(ref bounds, runBounds);
        }

        AdvanceTextPathPosition(pathSamples, pathLength, endVOffset, ref currentX, ref currentY);
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

        if (SvgService.HasRecursiveReference(svgTextRef, static e => e.ReferencedElement, new HashSet<Uri>()))
        {
            return;
        }

        var svgReferencedText = SvgService.GetReference<SvgTextBase>(svgTextRef, svgTextRef.ReferencedElement);
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

        if (TryCreateVerticalTextRunPlacements(svgTextBase, text, anchorX, anchorY, viewport, paint.TextAlign, assetLoader, rotations, out var verticalPlacements, out var verticalAdvance))
        {
            advance = verticalAdvance;
            return MeasureVerticalTextRunPlacementsBounds(svgTextBase, verticalPlacements, viewport, assetLoader, out _);
        }

        if (TryCreateAlignedCodepointPlacements(svgTextBase, text, anchorX, anchorY, viewport, paint.TextAlign, assetLoader, rotations, out var placements, out var totalAdvance))
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

        var codepoints = SplitCodepoints(text);
        var codepointCount = codepoints.Count;
        if (codepointCount == 0)
        {
            points = Array.Empty<SKPoint>();
            return false;
        }

        points = new SKPoint[codepointCount];
        var useContextualAdvances = xs.Count == 0 && ys.Count == 0;
        var naturalAdvances = useContextualAdvances
            ? MeasureNaturalCodepointAdvances(svgTextBase, codepoints, geometryBounds, assetLoader)
            : null;
        var currentX = initialX;
        var currentY = initialY;
        for (var i = 0; i < codepointCount; i++)
        {
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
        var codepoints = SplitCodepoints(text);
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
        if (charIndex < text.Length && char.IsHighSurrogate(text[start]) && char.IsLowSurrogate(text[charIndex]))
        {
            charIndex++;
        }

        codepoint = text.Substring(start, charIndex - start);
        return true;
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
        bool forceLeftAlign)
    {
        var decorationLayers = ResolveTextDecorationLayers(svgTextBase);
        if (decorationLayers.Count == 0)
        {
            return;
        }

        var alignmentPaint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, alignmentPaint);
        var textAlign = forceLeftAlign ? SKTextAlign.Left : alignmentPaint.TextAlign;

        if (TryCreateAlignedCodepointPlacements(svgTextBase, text, anchorX, anchorY, geometryBounds, textAlign, assetLoader, rotations, out var placements, out _))
        {
            DrawTextDecorations(decorationLayers, svgTextBase, text, placements, geometryBounds, ignoreAttributes, canvas, assetLoader);
            return;
        }

        var totalAdvance = MeasureNaturalTextAdvance(svgTextBase, text, geometryBounds, assetLoader);
        if (totalAdvance <= 0f)
        {
            return;
        }

        var startX = forceLeftAlign ? anchorX : GetAlignedStartX(anchorX, totalAdvance, textAlign);
        DrawTextDecorations(decorationLayers, startX, anchorY, totalAdvance, geometryBounds, ignoreAttributes, canvas, assetLoader);
    }

    private static void DrawTextDecorations(
        IReadOnlyList<TextDecorationLayer> decorationLayers,
        float startX,
        float baselineY,
        float advance,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader)
    {
        if (advance <= 0f || decorationLayers.Count == 0)
        {
            return;
        }

        for (var i = 0; i < decorationLayers.Count; i++)
        {
            DrawTextDecorationLayer(decorationLayers[i], startX, baselineY, advance, geometryBounds, ignoreAttributes, canvas, assetLoader);
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
        ISvgAssetLoader assetLoader)
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
                DrawTextDecorations(decorationLayers, decorationBounds.Left, placements[0].Point.Y, totalAdvance, geometryBounds, ignoreAttributes, canvas, assetLoader);
            }

            return;
        }

        var codepoints = SplitCodepoints(text);
        if (codepoints.Count == 0 || codepoints.Count != placements.Length)
        {
            return;
        }

        for (var layerIndex = 0; layerIndex < decorationLayers.Count; layerIndex++)
        {
            DrawPositionedTextDecorationLayer(decorationLayers[layerIndex], svgTextBase, text, placements, geometryBounds, ignoreAttributes, canvas, assetLoader);
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
        return element.CustomAttributes.TryGetValue(RawTextDecorationAttributeKey, out var rawValue) &&
               TryParseTextDecorationValue(rawValue, out decorations) &&
               HasRenderableDecorations(decorations);
    }

    private static bool TryParseTextDecorationValue(string? rawValue, out SvgTextDecoration decorations)
    {
        decorations = SvgTextDecoration.None;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        if (rawValue.IndexOf(',') >= 0)
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
        ISvgAssetLoader assetLoader)
    {
        if (advance <= 0f)
        {
            return;
        }

        var metricsPaint = CreateTextMetricsPaint(layer.MetricsSource, geometryBounds);
        var metrics = assetLoader.GetFontMetrics(metricsPaint);
        var fillPaint = SvgScenePaintingService.IsValidFill(layer.PaintSource)
            ? SvgScenePaintingService.GetFillPaint(layer.PaintSource, geometryBounds, assetLoader, ignoreAttributes)
            : null;
        var strokePaint = SvgScenePaintingService.IsValidStroke(layer.PaintSource, geometryBounds)
            ? SvgScenePaintingService.GetStrokePaint(layer.PaintSource, geometryBounds, assetLoader, ignoreAttributes)
            : null;

        if (fillPaint is null && strokePaint is null)
        {
            return;
        }

        DrawLinearDecorationKinds(layer.Decorations, startX, baselineY, advance, metricsPaint, metrics, fillPaint, strokePaint, canvas);
    }

    private static void DrawPositionedTextDecorationLayer(
        TextDecorationLayer layer,
        SvgTextBase svgTextBase,
        string text,
        PositionedCodepointPlacement[] placements,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader)
    {
        var metricsPaint = CreateTextMetricsPaint(layer.MetricsSource, geometryBounds);
        var metrics = assetLoader.GetFontMetrics(metricsPaint);
        var fillPaint = SvgScenePaintingService.IsValidFill(layer.PaintSource)
            ? SvgScenePaintingService.GetFillPaint(layer.PaintSource, geometryBounds, assetLoader, ignoreAttributes)
            : null;
        var strokePaint = SvgScenePaintingService.IsValidStroke(layer.PaintSource, geometryBounds)
            ? SvgScenePaintingService.GetStrokePaint(layer.PaintSource, geometryBounds, assetLoader, ignoreAttributes)
            : null;

        if ((fillPaint is null && strokePaint is null) || placements.Length == 0)
        {
            return;
        }

        var codepoints = SplitCodepoints(text);
        if (codepoints.Count == 0 || codepoints.Count != placements.Length)
        {
            return;
        }

        var naturalAdvances = MeasureNaturalCodepointAdvances(svgTextBase, codepoints, geometryBounds, assetLoader);
        for (var placementIndex = 0; placementIndex < placements.Length; placementIndex++)
        {
            var placement = placements[placementIndex];
            var leftOffset = 0f;
            var rightOffset = placementIndex < naturalAdvances.Length ? naturalAdvances[placementIndex] : 0f;

            if (!IsValidPositiveAdvance(rightOffset) &&
                !TryGetCodepointDecorationExtents(svgTextBase, codepoints[placementIndex], placement, metricsPaint, assetLoader, out leftOffset, out rightOffset))
            {
                continue;
            }

            if (rightOffset <= leftOffset)
            {
                continue;
            }

            DrawPositionedDecorationKinds(layer.Decorations, placement, leftOffset, rightOffset, metricsPaint, metrics, fillPaint, strokePaint, canvas);
        }
    }

    private static void DrawLinearDecorationKinds(
        SvgTextDecoration decorations,
        float startX,
        float baselineY,
        float advance,
        SKPaint metricsPaint,
        SKFontMetrics metrics,
        SKPaint? fillPaint,
        SKPaint? strokePaint,
        SKCanvas canvas)
    {
        if (decorations.HasFlag(SvgTextDecoration.Overline) &&
            TryCreateLinearDecorationPath(startX, baselineY, advance, metricsPaint, metrics, SvgTextDecoration.Overline, out var overlinePath))
        {
            DrawDecorationPath(overlinePath, fillPaint, strokePaint, canvas);
        }

        if (decorations.HasFlag(SvgTextDecoration.LineThrough) &&
            TryCreateLinearDecorationPath(startX, baselineY, advance, metricsPaint, metrics, SvgTextDecoration.LineThrough, out var lineThroughPath))
        {
            DrawDecorationPath(lineThroughPath, fillPaint, strokePaint, canvas);
        }

        if (decorations.HasFlag(SvgTextDecoration.Underline) &&
            TryCreateLinearDecorationPath(startX, baselineY, advance, metricsPaint, metrics, SvgTextDecoration.Underline, out var underlinePath))
        {
            DrawDecorationPath(underlinePath, fillPaint, strokePaint, canvas);
        }
    }

    private static void DrawPositionedDecorationKinds(
        SvgTextDecoration decorations,
        PositionedCodepointPlacement placement,
        float leftOffset,
        float rightOffset,
        SKPaint metricsPaint,
        SKFontMetrics metrics,
        SKPaint? fillPaint,
        SKPaint? strokePaint,
        SKCanvas canvas)
    {
        if (decorations.HasFlag(SvgTextDecoration.Overline) &&
            TryCreatePositionedDecorationPath(placement, leftOffset, rightOffset, metricsPaint, metrics, SvgTextDecoration.Overline, out var overlinePath))
        {
            DrawDecorationPath(overlinePath, fillPaint, strokePaint, canvas);
        }

        if (decorations.HasFlag(SvgTextDecoration.LineThrough) &&
            TryCreatePositionedDecorationPath(placement, leftOffset, rightOffset, metricsPaint, metrics, SvgTextDecoration.LineThrough, out var lineThroughPath))
        {
            DrawDecorationPath(lineThroughPath, fillPaint, strokePaint, canvas);
        }

        if (decorations.HasFlag(SvgTextDecoration.Underline) &&
            TryCreatePositionedDecorationPath(placement, leftOffset, rightOffset, metricsPaint, metrics, SvgTextDecoration.Underline, out var underlinePath))
        {
            DrawDecorationPath(underlinePath, fillPaint, strokePaint, canvas);
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

    private static void DrawDecorationPath(SKPath path, SKPaint? fillPaint, SKPaint? strokePaint, SKCanvas canvas)
    {
        if (fillPaint is not null)
        {
            canvas.DrawPath(path, fillPaint);
        }

        if (strokePaint is not null)
        {
            canvas.DrawPath(path, strokePaint);
        }
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

    private static bool TryDrawSequentialTextRuns(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        bool trimLeadingWhitespaceAtStart)
    {
        if (HasSequentialTextRunBarriers(svgTextBase))
        {
            return false;
        }

        if (!TryCollectSequentialTextRuns(svgTextBase, requireAnchorContent: false, IsTextReferenceRenderingEnabled(assetLoader), trimLeadingWhitespaceAtStart, out var runs))
        {
            return false;
        }

        if (TryDrawShapedSequentialTextRuns(svgTextBase, runs, ref currentX, ref currentY, viewport, geometryBounds, ignoreAttributes, canvas, assetLoader))
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
                DrawTextStringAlignedLeft(runs[i].StyleSource, runs[i].Text, ref startAlignedX, ref startAlignedY, geometryBounds, ignoreAttributes, canvas, assetLoader);
            }

            currentX = startAlignedX;
            currentY = startAlignedY;
            return true;
        }

        var totalAdvance = MeasureSequentialTextRuns(runs, geometryBounds, assetLoader);
        var inlineOrigin = GetAlignedStartCoordinate(isVertical ? currentY : currentX, totalAdvance, textAlign);
        var drawX = isVertical ? currentX : inlineOrigin;
        var drawY = isVertical ? inlineOrigin : currentY;

        for (var i = 0; i < runs.Count; i++)
        {
            DrawTextStringAlignedLeft(runs[i].StyleSource, runs[i].Text, ref drawX, ref drawY, geometryBounds, ignoreAttributes, canvas, assetLoader);
        }

        if (isVertical)
        {
            currentX = drawX;
            currentY = inlineOrigin + totalAdvance;
        }
        else
        {
            currentX = inlineOrigin + totalAdvance;
            currentY = drawY;
        }

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
        if (HasSequentialTextRunBarriers(svgTextBase))
        {
            return false;
        }

        if (!TryCollectSequentialTextRuns(svgTextBase, requireAnchorContent: false, IsTextReferenceRenderingEnabled(assetLoader), trimLeadingWhitespaceAtStart, out var runs))
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
        if (textAlign == SKTextAlign.Left)
        {
            var startAlignedX = currentX;
            var startAlignedY = currentY;
            for (var i = 0; i < runs.Count; i++)
            {
                var runBounds = MeasureTextStringBoundsAlignedLeft(runs[i].StyleSource, runs[i].Text, startAlignedX, startAlignedY, viewport, assetLoader, rotations: null, out var runAdvance);
                UnionBounds(ref bounds, runBounds);
                ApplyInlineAdvance(runs[i].StyleSource, ref startAlignedX, ref startAlignedY, runAdvance);
            }

            currentX = startAlignedX;
            currentY = startAlignedY;
            return true;
        }

        var totalAdvance = MeasureSequentialTextRuns(runs, viewport, assetLoader);
        var inlineOrigin = GetAlignedStartCoordinate(isVertical ? currentY : currentX, totalAdvance, textAlign);
        var drawX = isVertical ? currentX : inlineOrigin;
        var drawY = isVertical ? inlineOrigin : currentY;

        for (var i = 0; i < runs.Count; i++)
        {
            var runBounds = MeasureTextStringBoundsAlignedLeft(runs[i].StyleSource, runs[i].Text, drawX, drawY, viewport, assetLoader, rotations: null, out var runAdvance);
            UnionBounds(ref bounds, runBounds);
            ApplyInlineAdvance(runs[i].StyleSource, ref drawX, ref drawY, runAdvance);
        }

        if (isVertical)
        {
            currentX = drawX;
            currentY = inlineOrigin + totalAdvance;
        }
        else
        {
            currentX = inlineOrigin + totalAdvance;
            currentY = drawY;
        }

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
        ISvgAssetLoader assetLoader)
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

            if (SvgScenePaintingService.IsValidFill(segments[i].StyleSource))
            {
                var fillPaint = SvgScenePaintingService.GetFillPaint(segments[i].StyleSource, geometryBounds, assetLoader, ignoreAttributes);
                if (fillPaint is not null)
                {
                    PaintingService.SetPaintText(segments[i].StyleSource, geometryBounds, fillPaint);
                    fillPaint.TextAlign = SKTextAlign.Left;
                    canvas.DrawText(textBlob, 0f, 0f, fillPaint);
                }
            }

            if (SvgScenePaintingService.IsValidStroke(segments[i].StyleSource, geometryBounds))
            {
                var strokePaint = SvgScenePaintingService.GetStrokePaint(segments[i].StyleSource, geometryBounds, assetLoader, ignoreAttributes);
                if (strokePaint is not null)
                {
                    PaintingService.SetPaintText(segments[i].StyleSource, geometryBounds, strokePaint);
                    strokePaint.TextAlign = SKTextAlign.Left;
                    canvas.DrawText(textBlob, 0f, 0f, strokePaint);
                }
            }
        }

        currentX = inlineOrigin + totalAdvance;
        currentY = drawY;
        return true;
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
            assetLoader is not ISvgTextDirectedGlyphRunResolver glyphRunResolver)
        {
            return false;
        }

        combinedText = string.Concat(runs.Select(static run => run.Text));
        if (!ContainsMixedStrongDirections(combinedText))
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
        var baseDirection = IsRightToLeft(svgTextBase) ? -1 : 1;
        var bidiRuns = CreateLogicalBidiRuns(combinedText, baseDirection);
        if (bidiRuns.Count == 0)
        {
            return false;
        }

        var visualRuns = baseDirection == -1
            ? bidiRuns.AsEnumerable().Reverse()
            : bidiRuns;
        foreach (var bidiRun in visualRuns)
        {
            var bidiText = combinedText.Substring(bidiRun.StartCharIndex, bidiRun.Length);
            if (!glyphRunResolver.TryShapeGlyphRun(bidiText, shapingPaint, bidiRun.Direction == -1, out var shapedRun) ||
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

    private static List<LogicalBidiRun> CreateLogicalBidiRuns(string text, int baseDirection)
    {
        var codepoints = SplitCodepoints(text);
        if (codepoints.Count == 0)
        {
            return new List<LogicalBidiRun>();
        }

        var charOffsets = new int[codepoints.Count + 1];
        var charIndex = 0;
        for (var i = 0; i < codepoints.Count; i++)
        {
            charOffsets[i] = charIndex;
            charIndex += codepoints[i].Length;
        }

        charOffsets[codepoints.Count] = text.Length;
        var directions = ResolveBidiDirections(codepoints, baseDirection);
        var runs = new List<LogicalBidiRun>();
        var currentStart = 0;
        var currentDirection = directions[0];
        for (var i = 1; i < directions.Length; i++)
        {
            if (directions[i] == currentDirection)
            {
                continue;
            }

            var startCharIndex = charOffsets[currentStart];
            var endCharIndex = charOffsets[i];
            runs.Add(new LogicalBidiRun(startCharIndex, endCharIndex - startCharIndex, currentDirection));
            currentStart = i;
            currentDirection = directions[i];
        }

        runs.Add(new LogicalBidiRun(charOffsets[currentStart], charOffsets[codepoints.Count] - charOffsets[currentStart], currentDirection));
        return runs;
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

    private static bool TryCollectSequentialTextRuns(SvgTextBase svgTextBase, bool requireAnchorContent, bool textReferencesEnabled, bool trimLeadingWhitespaceAtStart, out List<SequentialTextRun> runs)
    {
        runs = new List<SequentialTextRun>();
        var hasAnchorContent = false;
        var trimLeadingWhitespace = trimLeadingWhitespaceAtStart;
        var previousEndedWithSpace = false;
        if (!TryCollectSequentialTextRuns(GetContentNodeList(svgTextBase), svgTextBase, runs, ref hasAnchorContent, ref trimLeadingWhitespace, ref previousEndedWithSpace, textReferencesEnabled))
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
        bool textReferencesEnabled)
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
                    if (!TryCollectSequentialTextRuns(GetContentNodeList(svgAnchor), CreateAnchorTextStyleSource(svgAnchor), runs, ref hasAnchorContent, ref trimLeadingWhitespace, ref previousEndedWithSpace, textReferencesEnabled))
                    {
                        return false;
                    }

                    break;

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
                    if (!TryCollectSequentialTextRuns(GetContentNodeList(svgTextSpan), svgTextSpan, runs, ref hasAnchorContent, ref childTrimLeadingWhitespace, ref childPreviousEndedWithSpace, textReferencesEnabled))
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
                    previousEndedWithSpace = preparedReferencedText.EndsWith(" ", StringComparison.Ordinal);
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
                        trimTrailingWhitespace: IsTerminalContentNode(contentNodeList, nodeIndex));
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (previousEndedWithSpace &&
                            styleSource.SpaceHandling != XmlSpaceHandling.Preserve &&
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
                        previousEndedWithSpace = text.EndsWith(" ", StringComparison.Ordinal);
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
        return TryCollectTextPathRuns(GetContentNodeList(svgTextPath), svgTextPath, viewport, runs, ref trimLeadingWhitespace, ref previousEndedWithSpace) && runs.Count > 0;
    }

    private static bool TryCollectTextPathRuns(
        IEnumerable<ISvgNode> contentNodes,
        SvgTextBase styleSource,
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

                    if (!TryCollectTextPathRuns(GetContentNodeList(svgAnchor), CreateAnchorTextStyleSource(svgAnchor), viewport, runs, ref trimLeadingWhitespace, ref previousEndedWithSpace))
                    {
                        return false;
                    }

                    break;

                case SvgTextSpan svgTextSpan:
                    if (!CanRenderTextSubtree(svgTextSpan))
                    {
                        break;
                    }

                    if (svgTextSpan.X.Count > 0 || svgTextSpan.Y.Count > 0)
                    {
                        return false;
                    }

                    var firstRunIndex = runs.Count;
                    var childTrimLeadingWhitespace = trimLeadingWhitespace || previousEndedWithSpace;
                    var childPreviousEndedWithSpace = false;
                    if (!TryCollectTextPathRuns(GetContentNodeList(svgTextSpan), svgTextSpan, viewport, runs, ref childTrimLeadingWhitespace, ref childPreviousEndedWithSpace))
                    {
                        return false;
                    }

                    if (runs.Count > firstRunIndex)
                    {
                        var dx = GetTextPathRunOffset(svgTextSpan.Dx, UnitRenderingType.HorizontalOffset, svgTextSpan, viewport);
                        var dy = GetTextPathRunOffset(svgTextSpan.Dy, UnitRenderingType.VerticalOffset, svgTextSpan, viewport);
                        runs[firstRunIndex] = runs[firstRunIndex] with
                        {
                            Dx = runs[firstRunIndex].Dx + dx,
                            Dy = runs[firstRunIndex].Dy + dy
                        };

                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = childPreviousEndedWithSpace;
                    }

                    break;

                case SvgTextPath:
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
                            styleSource.SpaceHandling != XmlSpaceHandling.Preserve &&
                            text![0] == ' ')
                        {
                            text = text.TrimStart(' ');
                        }

                        if (!string.IsNullOrEmpty(text))
                        {
                            runs.Add(new TextPathRun(styleSource, text!, 0f, 0f));
                            trimLeadingWhitespace = false;
                            previousEndedWithSpace = text.EndsWith(" ", StringComparison.Ordinal);
                        }
                    }

                    break;
            }
        }

        return true;
    }

    private static float GetTextPathRunOffset(SvgUnitCollection values, UnitRenderingType renderingType, SvgTextBase svgTextBase, SKRect viewport)
    {
        return values.Count > 0
            ? values[0].ToDeviceValue(renderingType, svgTextBase, viewport)
            : 0f;
    }

    private static bool HasExplicitStartOffset(SvgTextPath svgTextPath)
    {
        return svgTextPath.StartOffset != SvgUnit.None && svgTextPath.StartOffset != SvgUnit.Empty;
    }

    private static float GetTextPathInitialGlyphOffset(IReadOnlyList<TextPathRun> runs, SKRect geometryBounds, ISvgAssetLoader assetLoader)
    {
        for (var runIndex = 0; runIndex < runs.Count; runIndex++)
        {
            var run = runs[runIndex];
            if (string.IsNullOrEmpty(run.Text))
            {
                continue;
            }

            var codepoints = SplitCodepoints(run.Text);
            for (var i = 0; i < codepoints.Count; i++)
            {
                var advance = MeasureTextAdvance(run.StyleSource, codepoints[i], geometryBounds, assetLoader);
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
        var totalAdvance = 0f;
        for (var i = 0; i < runs.Count; i++)
        {
            totalAdvance += MeasureTextAdvance(runs[i].StyleSource, runs[i].Text, geometryBounds, assetLoader);
        }

        return totalAdvance;
    }

    private static void DrawPositionedTextPathRuns(
        IReadOnlyList<PositionedTextPathRun> runs,
        SKRect viewport,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references)
    {
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (TryDrawFilteredPositionedTextPathRun(run, viewport, geometryBounds, ignoreAttributes, canvas, assetLoader, references))
            {
                continue;
            }

            DrawPositionedTextPathRun(run, geometryBounds, ignoreAttributes, canvas, assetLoader, includeFill: true, includeStroke: true, includeDecorations: true);
        }
    }

    private static void DrawPositionedTextPathRun(
        PositionedTextPathRun run,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        bool includeFill,
        bool includeStroke,
        bool includeDecorations)
    {
        if (includeFill && SvgScenePaintingService.IsValidFill(run.StyleSource))
        {
            var fillPaint = SvgScenePaintingService.GetFillPaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes);
            if (fillPaint is not null)
            {
                _ = DrawCodepointPlacements(run.StyleSource, run.Text, run.Placements, geometryBounds, fillPaint, canvas, assetLoader);
            }
        }

        if (includeStroke && SvgScenePaintingService.IsValidStroke(run.StyleSource, geometryBounds))
        {
            var strokePaint = SvgScenePaintingService.GetStrokePaint(run.StyleSource, geometryBounds, assetLoader, ignoreAttributes);
            if (strokePaint is not null)
            {
                _ = DrawCodepointPlacements(run.StyleSource, run.Text, run.Placements, geometryBounds, strokePaint, canvas, assetLoader);
            }
        }

        if (includeDecorations)
        {
            DrawTextDecorations(
                ResolveTextDecorationLayers(run.StyleSource),
                run.StyleSource,
                run.Text,
                run.Placements,
                geometryBounds,
                ignoreAttributes,
                canvas,
                assetLoader);
        }
    }

    private static bool TryDrawFilteredPositionedTextPathRun(
        PositionedTextPathRun run,
        SKRect viewport,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references)
    {
        if (ignoreAttributes.HasFlag(DrawAttributes.Filter) ||
            run.StyleSource is not SvgVisualElement visualElement ||
            visualElement.Filter is null ||
            FilterEffectsService.IsNone(visualElement.Filter))
        {
            return false;
        }

        var runBounds = MeasureCodepointPlacementBounds(run.StyleSource, run.Text, run.Placements, geometryBounds, assetLoader, out _);
        if (runBounds.IsEmpty)
        {
            return true;
        }

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
            }

            canvas.SaveLayer(simpleFilterPaint);
            DrawPositionedTextPathRun(run, geometryBounds, ignoreAttributes, canvas, assetLoader, includeFill: true, includeStroke: true, includeDecorations: true);
            canvas.Restore();
            canvas.Restore();
            return true;
        }

        var sourceGraphic = RecordPositionedTextPathRunPicture(run, geometryBounds, ignoreAttributes, assetLoader, runBounds, includeFill: true, includeStroke: true, includeDecorations: true);
        if (sourceGraphic is null)
        {
            return true;
        }

        var fillPaint = SvgScenePaintingService.IsValidFill(run.StyleSource)
            ? RecordPositionedTextPathRunPicture(run, geometryBounds, ignoreAttributes, assetLoader, runBounds, includeFill: true, includeStroke: false, includeDecorations: false)
            : null;
        var strokePaint = SvgScenePaintingService.IsValidStroke(run.StyleSource, geometryBounds)
            ? RecordPositionedTextPathRunPicture(run, geometryBounds, ignoreAttributes, assetLoader, runBounds, includeFill: false, includeStroke: true, includeDecorations: false)
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
        }

        canvas.SaveLayer(filterContext.FilterPaint);
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

        var currentFilter = SvgService.GetReference<Svg.FilterEffects.SvgFilter>(visualElement, visualElement.Filter);
        if (currentFilter is null)
        {
            return false;
        }

        var uris = new HashSet<Uri>();
        do
        {
            filters.Add(currentFilter);
            if (SvgService.HasRecursiveReference(currentFilter, static e => e.Href, uris))
            {
                return filters.Count > 0;
            }

            currentFilter = SvgService.GetReference<Svg.FilterEffects.SvgFilter>(currentFilter, currentFilter.Href);
        } while (currentFilter is not null);

        return filters.Count > 0;
    }

    private static SKPicture? RecordPositionedTextPathRunPicture(
        PositionedTextPathRun run,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        ISvgAssetLoader assetLoader,
        SKRect runBounds,
        bool includeFill,
        bool includeStroke,
        bool includeDecorations)
    {
        if (runBounds.IsEmpty)
        {
            return null;
        }

        var recorder = new SKPictureRecorder();
        var pictureCanvas = recorder.BeginRecording(runBounds);
        DrawPositionedTextPathRun(run, geometryBounds, ignoreAttributes | DrawAttributes.Filter, pictureCanvas, assetLoader, includeFill, includeStroke, includeDecorations);
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
        if (references is { Count: > 0 })
        {
            return new HashSet<Uri>(references);
        }

        return visualElement.OwnerDocument?.BaseUri is { } baseUri
            ? new HashSet<Uri> { baseUri }
            : null;
    }

    private static bool TryCreateTextPathRunPlacements(
        IReadOnlyList<TextPathRun> runs,
        IReadOnlyList<PathSample> pathSamples,
        float startOffset,
        float baseVOffset,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out List<PositionedTextPathRun> positionedRuns,
        out float endOffset,
        out float endVOffset)
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
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            currentOffset += run.Dx;
            currentVOffset += run.Dy;

            if (TryCreateTextPathCodepointPlacements(run.StyleSource, run.Text, currentOffset, currentVOffset, pathSamples, viewport, geometryBounds, assetLoader, out var renderedText, out var placements, out var advance))
            {
                positionedRuns.Add(new PositionedTextPathRun(run.StyleSource, renderedText, placements));
            }

            currentOffset += advance;
        }

        endOffset = currentOffset;
        endVOffset = currentVOffset;
        return positionedRuns.Count > 0;
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

    private static bool TryCreateTextPathCodepointPlacements(
        SvgTextBase svgTextBase,
        string text,
        float startOffset,
        float baseVOffset,
        IReadOnlyList<PathSample> pathSamples,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        out string renderedText,
        out PositionedCodepointPlacement[] placements,
        out float totalAdvance)
    {
        renderedText = string.Empty;
        placements = Array.Empty<PositionedCodepointPlacement>();
        totalAdvance = 0f;

        if (string.IsNullOrEmpty(text) || pathSamples.Count < 2)
        {
            return false;
        }

        var codepoints = SplitCodepoints(text);
        if (codepoints.Count == 0)
        {
            return false;
        }

        var naturalAdvances = MeasureNaturalCodepointAdvances(svgTextBase, codepoints, geometryBounds, assetLoader);
        var letterSpacingUnit = svgTextBase.LetterSpacing;
        var wordSpacingUnit = svgTextBase.WordSpacing;
        var hasLetterSpacingAdjustment = HasSpacingAdjustment(letterSpacingUnit);
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
                    ? naturalAdvances[i] * (letterSpacingUnit.Value / 100f)
                    : fixedLetterSpacing;
            }

            if (hasWordSpacingAdjustment && IsWhitespaceCodepoint(codepoints[i]))
            {
                naturalLength += wordSpacingIsPercentage
                    ? naturalAdvances[i] * (wordSpacingUnit.Value / 100f)
                    : fixedWordSpacing;
            }
        }

        totalAdvance = naturalLength;
        var glyphScaleX = 1f;
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
                glyphScaleX = specifiedLength / totalAdvance;
                scaleRunFromStart = true;
                totalAdvance = specifiedLength;
            }
        }

        var rotations = GetPositionedRotations(svgTextBase, codepoints.Count);
        var currentOffset = startOffset;
        var currentVOffset = baseVOffset + GetBaselineShift(svgTextBase, viewport);
        var pathLength = pathSamples[pathSamples.Count - 1].Distance;
        var pathSegmentIndex = 1;
        var previousGlyphMidOffset = float.NegativeInfinity;
        var visibleText = new StringBuilder();
        var visiblePlacements = new List<PositionedCodepointPlacement>(codepoints.Count);
        for (var i = 0; i < codepoints.Count; i++)
        {
            var glyphAdvance = scaleRunFromStart
                ? naturalAdvances[i] * glyphScaleX
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
                        ? naturalAdvances[i] * (letterSpacingUnit.Value / 100f)
                        : fixedLetterSpacing;
                }

                if (hasWordSpacingAdjustment && IsWhitespaceCodepoint(codepoints[i]))
                {
                    wordSpacing = wordSpacingIsPercentage
                        ? naturalAdvances[i] * (wordSpacingUnit.Value / 100f)
                        : fixedWordSpacing;
                }
            }

            var clusterAdvance = glyphAdvance + letterSpacing + wordSpacing;
            if (!scaleRunFromStart)
            {
                clusterAdvance += extraGapAdvance;
            }

            var glyphMidOffset = currentOffset + (glyphAdvance * 0.5f);
            if (glyphMidOffset <= 0f)
            {
                currentOffset += clusterAdvance;
                continue;
            }

            if (glyphMidOffset >= pathLength)
            {
                break;
            }

            if (glyphMidOffset < previousGlyphMidOffset)
            {
                pathSegmentIndex = 1;
            }

            previousGlyphMidOffset = glyphMidOffset;

            if (!TryGetPathPointAndTangent(pathSamples, glyphMidOffset, ref pathSegmentIndex, out var rawPoint, out var tangent))
            {
                return false;
            }

            var codepointRotationDegrees = GetCodepointRotationDegrees(svgTextBase, codepoints[i], rotations, i);
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
            var point = new SKPoint(
                rawPoint.X + (baselineNormal.X * currentVOffset) - (baselineDirection.X * glyphAdvance * 0.5f),
                rawPoint.Y + (baselineNormal.Y * currentVOffset) - (baselineDirection.Y * glyphAdvance * 0.5f));
            visiblePlacements.Add(new PositionedCodepointPlacement(
                point,
                finalAngleDegrees,
                scaleRunFromStart ? glyphScaleX : 1f,
                point.X));
            visibleText.Append(codepoints[i]);

            if (i >= codepoints.Count - 1)
            {
                continue;
            }

            currentOffset += clusterAdvance;
        }

        renderedText = visibleText.ToString();
        placements = visiblePlacements.ToArray();
        return placements.Length > 0;
    }

    private static void AdvanceTextPathPosition(
        IReadOnlyList<PathSample> pathSamples,
        float pathLength,
        float endVOffset,
        ref float currentX,
        ref float currentY)
    {
        if (!TryGetTextPathCurrentPosition(pathSamples, pathLength, endVOffset, out var endPoint))
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
        point = default;
        if (pathSamples.Count == 0)
        {
            return false;
        }

        if (pathSamples.Count == 1)
        {
            point = pathSamples[0].Point;
            return true;
        }

        var pathLength = pathSamples[pathSamples.Count - 1].Distance;
        if (distance >= 0f && distance <= pathLength)
        {
            return TryGetTextPathPoint(distance, vOffset, pathSamples, out point);
        }

        var useEnd = distance > pathLength;
        var anchorSample = useEnd ? pathSamples[pathSamples.Count - 1] : pathSamples[0];
        var adjacentSample = useEnd ? pathSamples[pathSamples.Count - 2] : pathSamples[1];
        var tangent = Normalize(new SKPoint(
            anchorSample.Point.X - adjacentSample.Point.X,
            anchorSample.Point.Y - adjacentSample.Point.Y));
        if (!useEnd)
        {
            tangent = new SKPoint(-tangent.X, -tangent.Y);
        }

        var normal = Normalize(new SKPoint(-tangent.Y, tangent.X));
        var overshoot = useEnd
            ? distance - pathLength
            : distance;
        point = new SKPoint(
            anchorSample.Point.X + (tangent.X * overshoot) + (normal.X * vOffset),
            anchorSample.Point.Y + (tangent.Y * overshoot) + (normal.Y * vOffset));
        return true;
    }

    private static bool TryProjectPointOntoTextPath(
        IReadOnlyList<PathSample> pathSamples,
        SKPoint targetPoint,
        out float distance,
        out float vOffset)
    {
        distance = 0f;
        vOffset = 0f;
        if (pathSamples.Count < 2)
        {
            return false;
        }

        var bestDistanceSquared = float.PositiveInfinity;
        var found = false;
        for (var i = 1; i < pathSamples.Count; i++)
        {
            var left = pathSamples[i - 1];
            var right = pathSamples[i];
            if (right.StartsSubpath)
            {
                continue;
            }

            var segment = new SKPoint(right.Point.X - left.Point.X, right.Point.Y - left.Point.Y);
            var segmentLength = Distance(left.Point, right.Point);
            if (segmentLength <= 0.001f)
            {
                continue;
            }

            var tangent = new SKPoint(segment.X / segmentLength, segment.Y / segmentLength);
            var pointVector = new SKPoint(targetPoint.X - left.Point.X, targetPoint.Y - left.Point.Y);
            var projectedLength = ClampFloat((pointVector.X * tangent.X) + (pointVector.Y * tangent.Y), 0f, segmentLength);
            var closestPoint = new SKPoint(
                left.Point.X + (tangent.X * projectedLength),
                left.Point.Y + (tangent.Y * projectedLength));
            var delta = new SKPoint(targetPoint.X - closestPoint.X, targetPoint.Y - closestPoint.Y);
            var candidateDistanceSquared = (delta.X * delta.X) + (delta.Y * delta.Y);
            if (candidateDistanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            var normal = new SKPoint(-tangent.Y, tangent.X);
            bestDistanceSquared = candidateDistanceSquared;
            distance = left.Distance + projectedLength;
            vOffset = (delta.X * normal.X) + (delta.Y * normal.Y);
            found = true;
        }

        if (found)
        {
            return true;
        }

        var anchor = pathSamples[0];
        distance = anchor.Distance;
        vOffset = Distance(anchor.Point, targetPoint);
        return true;
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
            useCurrentPositionOffset &&
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

    private static List<PathSample> BuildPathSamples(SKPath path)
    {
        var samples = new List<PathSample>();
        if (path.Commands is null || path.Commands.Count == 0)
        {
            return samples;
        }

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
                samples.Add(new PathSample(next, totalDistance, false));
                hasCurrent = true;
                return;
            }

            totalDistance += Distance(current, next);
            current = next;
            samples.Add(new PathSample(next, totalDistance, false));
        }

        for (var i = 0; i < path.Commands.Count; i++)
        {
            switch (path.Commands[i])
            {
                case MoveToPathCommand moveTo:
                    current = new SKPoint(moveTo.X, moveTo.Y);
                    figureStart = current;
                    samples.Add(new PathSample(current, totalDistance, true));
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
                        var steps = ClampSteps((int)Math.Ceiling(ApproximateQuadraticLength(start, control, end) * 192f), 96, 1024);
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
                        var steps = ClampSteps((int)Math.Ceiling(ApproximateCubicLength(start, control1, control2, end) * 192f), 128, 1024);
                        for (var step = 1; step <= steps; step++)
                        {
                            AppendSample(EvaluateCubic(start, control1, control2, end, step / (float)steps));
                        }
                    }
                    break;

                case ArcToPathCommand arcTo when hasCurrent:
                    if (!TryAppendArcSamples(current, arcTo, AppendSample))
                    {
                        AppendSample(new SKPoint(arcTo.X, arcTo.Y));
                    }

                    break;

                case ClosePathCommand _ when hasCurrent:
                    AppendSample(figureStart);
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
                tangent = Normalize(new SKPoint(current.Point.X - previous.Point.X, current.Point.Y - previous.Point.Y));
                return true;
            }

            var t = (distance - previous.Distance) / segmentLength;
            var deltaX = current.Point.X - previous.Point.X;
            var deltaY = current.Point.Y - previous.Point.Y;
            point = new SKPoint(
                previous.Point.X + (deltaX * t),
                previous.Point.Y + (deltaY * t));
            tangent = new SKPoint(deltaX / segmentLength, deltaY / segmentLength);
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
            tangent = Normalize(new SKPoint(current.Point.X - previous.Point.X, current.Point.Y - previous.Point.Y));
            return true;
        }

        var t = (distance - previous.Distance) / segmentLength;
        var deltaX = current.Point.X - previous.Point.X;
        var deltaY = current.Point.Y - previous.Point.Y;
        point = new SKPoint(
            previous.Point.X + (deltaX * t),
            previous.Point.Y + (deltaY * t));
        tangent = new SKPoint(deltaX / segmentLength, deltaY / segmentLength);
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

    private static float ResolveTextPathStartOffset(SvgTextPath svgTextPath, SvgPath? svgPath, SKPath skPath, SKRect viewport, float transformedPathLength)
    {
        var startOffset = svgTextPath.StartOffset;
        if (startOffset == SvgUnit.None || startOffset == SvgUnit.Empty)
        {
            return 0f;
        }

        if (IsPercentageStartOffset(svgTextPath, startOffset))
        {
            var pathLength = svgPath is { PathLength: > 0f }
                ? svgPath.PathLength
                : transformedPathLength > 0f
                    ? transformedPathLength
                    : EstimatePathLength(skPath);
            return pathLength * (startOffset.Value / 100f);
        }

        return startOffset.ToDeviceValue(UnitRenderingType.Other, svgTextPath, viewport);
    }

    private static bool TryResolveTextPathGeometry(
        SvgTextPath svgTextPath,
        SKRect viewport,
        out SvgPath? svgPath,
        out SKPath skPath,
        out SKRect geometryBounds,
        out List<PathSample> pathSamples,
        out float pathLength)
    {
        svgPath = SvgService.GetReference<SvgPath>(svgTextPath, svgTextPath.ReferencedPath);
        skPath = svgPath?.PathData?.ToPath(svgPath.FillRule) ?? new SKPath();
        geometryBounds = SKRect.Empty;
        pathSamples = new List<PathSample>();
        pathLength = 0f;
        if (skPath.IsEmpty)
        {
            return false;
        }

        pathSamples = BuildPathSamples(skPath);
        if (pathSamples.Count < 2)
        {
            return false;
        }

        var transform = GetTextPathReferenceTransform(svgPath);
        if (!IsIdentityTransform(transform))
        {
            pathSamples = TransformPathSamples(pathSamples, transform);
        }

        geometryBounds = GetPathSampleBounds(pathSamples);
        pathLength = pathSamples[pathSamples.Count - 1].Distance;
        return pathLength > 0f;
    }

    private static SKMatrix GetTextPathReferenceTransform(SvgPath? svgPath)
    {
        return svgPath is SvgVisualElement { Transforms.Count: > 0 } visualElement
            ? TransformsService.ToMatrix(visualElement.Transforms)
            : SKMatrix.Identity;
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

            transformed.Add(new PathSample(mappedPoint, totalDistance, pathSamples[i].StartsSubpath));
        }

        return transformed;
    }

    private static SKPoint GetPathStartTangent(IReadOnlyList<PathSample> pathSamples)
    {
        for (var i = 1; i < pathSamples.Count; i++)
        {
            if (pathSamples[i].StartsSubpath)
            {
                continue;
            }

            return Normalize(new SKPoint(
                pathSamples[i].Point.X - pathSamples[i - 1].Point.X,
                pathSamples[i].Point.Y - pathSamples[i - 1].Point.Y));
        }

        return new SKPoint(1f, 0f);
    }

    private static SKPoint GetPathEndTangent(IReadOnlyList<PathSample> pathSamples)
    {
        for (var i = pathSamples.Count - 1; i >= 1; i--)
        {
            if (pathSamples[i].StartsSubpath)
            {
                continue;
            }

            return Normalize(new SKPoint(
                pathSamples[i].Point.X - pathSamples[i - 1].Point.X,
                pathSamples[i].Point.Y - pathSamples[i - 1].Point.Y));
        }

        return new SKPoint(1f, 0f);
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

    private static bool TryAppendArcSamples(SKPoint start, ArcToPathCommand arcTo, Action<SKPoint> appendSample)
    {
        var end = new SKPoint(arcTo.X, arcTo.Y);
        if (!TryGetArcParameters(start, end, arcTo.Rx, arcTo.Ry, arcTo.XAxisRotate, arcTo.LargeArc, arcTo.Sweep, out var parameters))
        {
            return false;
        }

        AppendArcSamples(parameters, appendSample);
        return true;
    }

    private static void AppendArcSamples(ArcParameters parameters, Action<SKPoint> appendSample)
    {
        var approxLength = Math.Abs(parameters.DeltaAngle) * Math.Max(parameters.Rx, parameters.Ry);
        var steps = ClampSteps((int)Math.Ceiling(approxLength / 4f), 6, MaxEllipseSteps);
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
        var length = (float)Math.Sqrt((value.X * value.X) + (value.Y * value.Y));
        if (length <= 0f)
        {
            return new SKPoint(1f, 0f);
        }

        return new SKPoint(value.X / length, value.Y / length);
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
               HasPositionedDescendantTextChunk(svgTextBase) &&
               !HasAbsolutelyPositionedDescendantTextChunk(svgTextBase) &&
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
        if (svgTextBase.X.Count > 0)
        {
            currentX = ResolveTextUnitValue(svgTextBase.X[0], UnitRenderingType.HorizontalOffset, svgTextBase, viewport, assetLoader);
        }

        if (svgTextBase.Y.Count > 0)
        {
            currentY = ResolveTextUnitValue(svgTextBase.Y[0], UnitRenderingType.VerticalOffset, svgTextBase, viewport, assetLoader);
        }

        if (svgTextBase.Dx.Count > 0)
        {
            currentX += ResolveTextUnitValue(svgTextBase.Dx[0], UnitRenderingType.HorizontalOffset, svgTextBase, viewport, assetLoader);
        }

        if (svgTextBase.Dy.Count > 0)
        {
            currentY += ResolveTextUnitValue(svgTextBase.Dy[0], UnitRenderingType.VerticalOffset, svgTextBase, viewport, assetLoader);
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

    private static bool HasEffectiveSpacingAdjustments(SvgTextBase svgTextBase, string text)
    {
        if (string.IsNullOrEmpty(text) ||
            !HasSpacingAdjustments(svgTextBase))
        {
            return false;
        }

        return HasEffectiveSpacingAdjustments(svgTextBase, SplitCodepoints(text));
    }

    private static bool HasEffectiveSpacingAdjustments(SvgTextBase svgTextBase, IReadOnlyList<string> codepoints)
    {
        if (codepoints.Count < 2)
        {
            return false;
        }

        var hasLetterSpacing = HasSpacingAdjustment(svgTextBase.LetterSpacing);
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
        return spacing == SvgUnit.None || spacing == SvgUnit.Empty
            ? 0f
            : spacing.Type == SvgUnitType.Percentage
                ? clusterAdvance * (spacing.Value / 100f)
                : spacing.ToDeviceValue(UnitRenderingType.Horizontal, svgTextBase, geometryBounds);
    }

    private static bool IsWhitespaceCodepoint(string codepoint)
    {
        return codepoint.Length > 0 && char.IsWhiteSpace(codepoint, 0);
    }

    private static bool SupportsLetterSpacing(string codepoint)
    {
        if (string.IsNullOrEmpty(codepoint))
        {
            return false;
        }

        var scalar = char.ConvertToUtf32(codepoint, 0);
        return scalar switch
        {
            >= 0x0600 and <= 0x06FF => false, // Arabic
            >= 0x0750 and <= 0x077F => false, // Arabic Supplement
            >= 0x0870 and <= 0x089F => false, // Arabic Extended-B
            >= 0x08A0 and <= 0x08FF => false, // Arabic Extended-A
            >= 0x0700 and <= 0x074F => false, // Syriac
            >= 0x07C0 and <= 0x07FF => false, // NKo
            >= 0x0840 and <= 0x085F => false, // Mandaic
            >= 0x1800 and <= 0x18AF => false, // Mongolian
            >= 0xA840 and <= 0xA87F => false, // Phags-pa
            >= 0x0900 and <= 0x097F => false, // Devanagari
            >= 0x0980 and <= 0x09FF => false, // Bengali
            >= 0x0A00 and <= 0x0A7F => false, // Gurmukhi
            >= 0x11600 and <= 0x1165F => false, // Modi
            >= 0x11180 and <= 0x111DF => false, // Sharada
            >= 0xA800 and <= 0xA82F => false, // Syloti Nagri
            >= 0x11480 and <= 0x114DF => false, // Tirhuta
            >= 0x1680 and <= 0x169F => false, // Ogham
            _ => true
        };
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

    private static List<string> SplitCodepoints(string text)
    {
        var codepoints = new List<string>();
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            codepoints.Add(codepoint);
        }

        return codepoints;
    }

    private static float[] MeasureNaturalCodepointAdvances(
        SvgTextBase svgTextBase,
        IReadOnlyList<string> codepoints,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var advances = new float[codepoints.Count];
        if (codepoints.Count == 0)
        {
            return advances;
        }

        if (IsVerticalWritingMode(svgTextBase))
        {
            for (var i = 0; i < codepoints.Count; i++)
            {
                advances[i] = MeasureNaturalTextAdvance(svgTextBase, codepoints[i], geometryBounds, assetLoader);
            }

            return advances;
        }

        var text = string.Concat(codepoints);
        if (string.IsNullOrEmpty(text))
        {
            return advances;
        }

        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

        var isRightToLeft = IsRightToLeft(svgTextBase);
        var requiresSyntheticSmallCaps = RequiresSyntheticSmallCaps(svgTextBase, text);
        var usesBrowserCompatibleRunTypeface = ShouldUseBrowserCompatibleRunTypeface(svgTextBase, text);
        var cacheKey = CreateNaturalCodepointAdvanceCacheKey(
            assetLoader,
            text,
            paint,
            isRightToLeft,
            requiresSyntheticSmallCaps,
            usesBrowserCompatibleRunTypeface);
        if (TryGetCachedNaturalCodepointAdvances(cacheKey, out var cachedAdvances))
        {
            return cachedAdvances;
        }

        if (TryMeasureNaturalCodepointAdvancesFromSimpleShapedRun(
                svgTextBase,
                text,
                codepoints,
                geometryBounds,
                paint,
                assetLoader,
                isRightToLeft,
                requiresSyntheticSmallCaps,
                usesBrowserCompatibleRunTypeface,
                out var shapedAdvances))
        {
            CacheNaturalCodepointAdvances(cacheKey, shapedAdvances);
            return shapedAdvances;
        }

        var builder = new StringBuilder();
        var previousAdvance = 0f;

        for (var i = 0; i < codepoints.Count; i++)
        {
            var prefixText = builder.ToString();
            builder.Append(codepoints[i]);
            var currentAdvance = MeasureNaturalTextAdvance(svgTextBase, builder.ToString(), geometryBounds, assetLoader);
            var codepointAdvance = currentAdvance - previousAdvance;
            if (IsWhitespaceCodepoint(codepoints[i]))
            {
                var contextualWhitespaceAdvance = MeasureContextualWhitespaceAdvance(svgTextBase, prefixText, codepoints[i], geometryBounds, assetLoader);
                if (IsValidPositiveAdvance(contextualWhitespaceAdvance))
                {
                    codepointAdvance = contextualWhitespaceAdvance;
                }
            }

            if (!IsValidPositiveAdvance(codepointAdvance))
            {
                codepointAdvance = 0f;
            }

            advances[i] = codepointAdvance;
            previousAdvance += codepointAdvance;
        }

        CacheNaturalCodepointAdvances(cacheKey, advances);
        return advances;
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

    private static bool TryGetCachedNaturalCodepointAdvances(
        NaturalCodepointAdvanceCacheKey cacheKey,
        out float[] advances)
    {
        if (s_naturalCodepointAdvanceCache.TryGetValue(cacheKey, out var cachedAdvances))
        {
            advances = (float[])cachedAdvances.Clone();
            return true;
        }

        advances = Array.Empty<float>();
        return false;
    }

    private static void CacheNaturalCodepointAdvances(
        NaturalCodepointAdvanceCacheKey cacheKey,
        float[] advances)
    {
        s_naturalCodepointAdvanceCache.TryAdd(cacheKey, (float[])advances.Clone());
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

        var codepoints = SplitCodepoints(text);
        if (codepoints.Count == 0 || codepoints.Count != placements.Length)
        {
            return 0f;
        }

        var advances = MeasureNaturalCodepointAdvances(svgTextBase, codepoints, geometryBounds, assetLoader);
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
        placements = Array.Empty<PositionedCodepointPlacement>();
        totalAdvance = 0f;
        var isVertical = IsVerticalWritingMode(svgTextBase);

        if (string.IsNullOrEmpty(text) || (explicitRotations is null && !HasPerGlyphLayoutAdjustments(svgTextBase, text) && !isVertical))
        {
            return false;
        }

        var codepoints = SplitCodepoints(text);
        if (codepoints.Count == 0)
        {
            return false;
        }

        var hasEffectiveSpacingAdjustments = HasEffectiveSpacingAdjustments(svgTextBase, codepoints);

        var naturalAdvances = MeasureNaturalCodepointAdvances(svgTextBase, codepoints, geometryBounds, assetLoader);
        var letterSpacingUnit = svgTextBase.LetterSpacing;
        var wordSpacingUnit = svgTextBase.WordSpacing;
        var hasLetterSpacingAdjustment = HasSpacingAdjustment(letterSpacingUnit);
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
            if (i < codepoints.Count - 1)
            {
                if (hasLetterSpacingAdjustment && SupportsLetterSpacing(codepoints[i]))
                {
                    naturalLength += letterSpacingIsPercentage
                        ? naturalAdvances[i] * (letterSpacingUnit.Value / 100f)
                        : fixedLetterSpacing;
                }

                if (hasWordSpacingAdjustment && IsWhitespaceCodepoint(codepoints[i]))
                {
                    naturalLength += wordSpacingIsPercentage
                        ? naturalAdvances[i] * (wordSpacingUnit.Value / 100f)
                        : fixedWordSpacing;
                }
            }
        }

        var specifiedLength = TryGetOwnTextLength(svgTextBase, geometryBounds, isVertical, out var ownSpecifiedLength)
            ? ownSpecifiedLength
            : 0f;
        var hasActiveTextLengthAdjustment = specifiedLength > 0f &&
                                            Math.Abs(naturalLength - specifiedLength) > TextLengthTolerance;
        if (explicitRotations is null &&
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
            if (GetOwnLengthAdjust(svgTextBase) == SvgTextLengthAdjust.Spacing && codepoints.Count > 1)
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

        var rotations = explicitRotations ?? GetPositionedRotations(svgTextBase, codepoints.Count);
        var currentX = anchorX;
        var currentY = anchorY;
        if (isVertical)
        {
            currentY = GetAlignedStartCoordinate(anchorY, totalAdvance, textAlign);
        }
        else
        {
            currentX = GetAlignedStartCoordinate(anchorX, totalAdvance, textAlign);
        }

        var scaleOriginX = currentX;
        placements = new PositionedCodepointPlacement[codepoints.Count];
        for (var i = 0; i < codepoints.Count; i++)
        {
            placements[i] = new PositionedCodepointPlacement(
                new SKPoint(currentX, currentY),
                GetCodepointRotationDegrees(svgTextBase, codepoints[i], rotations, i),
                glyphScaleX,
                scaleRunFromStart ? scaleOriginX : currentX);

            if (i >= codepoints.Count - 1)
            {
                continue;
            }

            var clusterAdvance = naturalAdvances[i];
            if (hasLetterSpacingAdjustment && SupportsLetterSpacing(codepoints[i]))
            {
                clusterAdvance += letterSpacingIsPercentage
                    ? naturalAdvances[i] * (letterSpacingUnit.Value / 100f)
                    : fixedLetterSpacing;
                if (!IsValidPositiveAdvance(clusterAdvance))
                {
                    clusterAdvance = 0f;
                }
            }

            if (hasWordSpacingAdjustment && IsWhitespaceCodepoint(codepoints[i]))
            {
                clusterAdvance += wordSpacingIsPercentage
                    ? naturalAdvances[i] * (wordSpacingUnit.Value / 100f)
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

    private static float MeasureSequentialTextRuns(
        IReadOnlyList<SequentialTextRun> runs,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var totalAdvance = 0f;
        for (var i = 0; i < runs.Count; i++)
        {
            totalAdvance += MeasureTextAdvance(runs[i].StyleSource, runs[i].Text, geometryBounds, assetLoader);
        }

        return totalAdvance;
    }

    private static float MeasureTextAdvance(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        if (TryCreateVerticalTextRunPlacements(svgTextBase, text, 0f, 0f, geometryBounds, SKTextAlign.Left, assetLoader, explicitRotations: null, out _, out var verticalAdvance))
        {
            return verticalAdvance;
        }

        if (TryCreateAlignedCodepointPlacements(svgTextBase, text, 0f, 0f, geometryBounds, SKTextAlign.Left, assetLoader, explicitRotations: null, out _, out var totalAdvance))
        {
            return totalAdvance;
        }

        return MeasureNaturalTextAdvance(svgTextBase, text, geometryBounds, assetLoader);
    }

    private static float MeasureNaturalTextAdvance(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        if (IsVerticalWritingMode(svgTextBase))
        {
            var codepoints = SplitCodepoints(text);
            var totalAdvance = 0f;
            for (var i = 0; i < codepoints.Count; i++)
            {
                totalAdvance += MeasureNaturalTextAdvanceHorizontal(svgTextBase, codepoints[i], geometryBounds, assetLoader);
            }

            return totalAdvance;
        }

        return MeasureNaturalTextAdvanceHorizontal(svgTextBase, text, geometryBounds, assetLoader);
    }

    private static float MeasureNaturalTextAdvanceHorizontal(
        SvgTextBase svgTextBase,
        string text,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

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

    private static SKTextAlign GetTextAnchorAlign(SvgTextBase svgTextBase, SKRect geometryBounds)
    {
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

        var direction = GetInheritedTextAttribute(svgTextBase, "direction");
        var unicodeBidi = GetInheritedTextAttribute(svgTextBase, "unicode-bidi");
        if (!string.IsNullOrWhiteSpace(direction) || !string.IsNullOrWhiteSpace(unicodeBidi))
        {
            return true;
        }

        return false;
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
        float[]? rotations = null)
    {
        var fillAdvance = 0f;
        if (SvgScenePaintingService.IsValidFill(svgTextBase))
        {
            var fillPaint = SvgScenePaintingService.GetFillPaint(svgTextBase, geometryBounds, assetLoader, ignoreAttributes);
            if (fillPaint is not null)
            {
                fillAdvance = DrawTextRunsAlignedLeft(svgTextBase, text, x, y, geometryBounds, fillPaint, canvas, assetLoader, rotations);
            }
        }

        var strokeAdvance = 0f;
        if (SvgScenePaintingService.IsValidStroke(svgTextBase, geometryBounds))
        {
            var strokePaint = SvgScenePaintingService.GetStrokePaint(svgTextBase, geometryBounds, assetLoader, ignoreAttributes);
            if (strokePaint is not null)
            {
                strokeAdvance = DrawTextRunsAlignedLeft(svgTextBase, text, x, y, geometryBounds, strokePaint, canvas, assetLoader, rotations);
            }
        }

        DrawResolvedTextDecorations(svgTextBase, text, x, y, geometryBounds, ignoreAttributes, canvas, assetLoader, rotations, forceLeftAlign: true);
        ApplyInlineAdvance(svgTextBase, ref x, ref y, Math.Max(strokeAdvance, fillAdvance));
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

        if (TryCreateVerticalTextRunPlacements(svgTextBase, text, anchorX, anchorY, geometryBounds, SKTextAlign.Left, assetLoader, rotations, out var verticalPlacements, out var verticalAdvance))
        {
            _ = DrawVerticalTextRunPlacements(svgTextBase, verticalPlacements, geometryBounds, paint, canvas, assetLoader);
            return verticalAdvance;
        }

        if (TryCreateAlignedCodepointPlacements(svgTextBase, text, anchorX, anchorY, geometryBounds, SKTextAlign.Left, assetLoader, rotations, out var placements, out var totalAdvance))
        {
            _ = DrawCodepointPlacements(svgTextBase, text, placements, geometryBounds, paint, canvas, assetLoader);
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
            var fullRunMeasureBounds = new SKRect();
            var measuredAdvance = EnsureWhitespaceAdvance(
                fallbackText,
                fullRunPaint,
                assetLoader,
                assetLoader.MeasureText(shapedText, fullRunPaint, ref fullRunMeasureBounds));
            canvas.DrawText(shapedText, anchorX, anchorY, fullRunPaint);
            return measuredAdvance;
        }

        var typefaceSpans = assetLoader.FindTypefaces(fallbackText, paint);
        if (typefaceSpans.Count == 0)
        {
            var scratchBounds = new SKRect();
            var measuredAdvance = EnsureWhitespaceAdvance(fallbackText, paint, assetLoader, assetLoader.MeasureText(fallbackText, paint, ref scratchBounds));
            canvas.DrawText(ApplyBrowserCompatibleBidiControls(svgTextBase, fallbackText), anchorX, anchorY, paint);
            return measuredAdvance;
        }

        var currentX = anchorX;
        var naturalTotalAdvance = 0f;
        foreach (var typefaceSpan in typefaceSpans)
        {
            paint.Typeface = typefaceSpan.Typeface;
            canvas.DrawText(ApplyBrowserCompatibleBidiControls(svgTextBase, typefaceSpan.Text), currentX, anchorY, paint);
            currentX += typefaceSpan.Advance;
            naturalTotalAdvance += typefaceSpan.Advance;
            paint = paint.Clone();
        }

        naturalTotalAdvance = EnsureWhitespaceAdvance(fallbackText, paint, assetLoader, naturalTotalAdvance);
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

        if (TryCreateVerticalTextRunPlacements(svgTextBase, text, anchorX, anchorY, viewport, SKTextAlign.Left, assetLoader, rotations, out var verticalPlacements, out var verticalAdvance))
        {
            advance = verticalAdvance;
            return MeasureVerticalTextRunPlacementsBounds(svgTextBase, verticalPlacements, viewport, assetLoader, out _);
        }

        if (TryCreateAlignedCodepointPlacements(svgTextBase, text, anchorX, anchorY, viewport, SKTextAlign.Left, assetLoader, rotations, out var placements, out var totalAdvance))
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
        return IsVerticalWritingMode(svgTextBase)
            ? new SKRect(anchorX + metrics.Ascent, anchorY, anchorX + metrics.Descent, anchorY + advance)
            : new SKRect(anchorX, anchorY + metrics.Ascent, anchorX + advance, anchorY + metrics.Descent);
    }

    private static string? PrepareText(
        SvgTextBase svgTextBase,
        string? value,
        bool trimLeadingWhitespace = true,
        bool trimTrailingWhitespace = false)
    {
        value = ApplyTransformation(svgTextBase, value);
        if (value is null)
        {
            return null;
        }

        value = new StringBuilder(value)
            .Replace("\r\n", " ")
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ')
            .ToString();

        if (svgTextBase.SpaceHandling == XmlSpaceHandling.Preserve)
        {
            return value;
        }

        var normalizedValue = trimTrailingWhitespace
            ? (string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : trimLeadingWhitespace
                    ? value.Trim()
                    : value.TrimEnd())
            : trimLeadingWhitespace
                ? value.TrimStart()
                : value;

        return s_multipleSpaces.Replace(normalizedValue, " ");
    }

    private static string? PrepareResolvedContent(SvgTextBase svgTextBase, string? value, bool trimLeadingWhitespace, bool previousEndedWithSpace)
    {
        var prepared = PrepareText(svgTextBase, value, trimLeadingWhitespace);
        if (previousEndedWithSpace &&
            svgTextBase.SpaceHandling != XmlSpaceHandling.Preserve &&
            !string.IsNullOrEmpty(prepared) &&
            prepared![0] == ' ')
        {
            prepared = prepared.TrimStart(' ');
        }

        return prepared;
    }

    private static bool TryCreateFlattenedTextLengthRuns(
        SvgTextBase svgTextBase,
        float currentX,
        float currentY,
        SKRect viewport,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        bool trimLeadingWhitespaceAtStart,
        out List<PositionedCodepointRun> runs,
        out float totalAdvance,
        out float finalY)
    {
        runs = new List<PositionedCodepointRun>();
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

        var naturalAdvances = new float[flattenedCodepoints.Count];
        var naturalLength = 0f;
        for (var i = 0; i < flattenedCodepoints.Count; i++)
        {
            naturalAdvances[i] = MeasureNaturalTextAdvance(flattenedCodepoints[i].StyleSource, flattenedCodepoints[i].Codepoint, geometryBounds, assetLoader);
            naturalLength += naturalAdvances[i];
            if (i < flattenedCodepoints.Count - 1)
            {
                var styleSource = flattenedCodepoints[i].StyleSource;
                var letterSpacing = ResolveSpacingValue(styleSource, styleSource.LetterSpacing, geometryBounds, naturalAdvances[i]);
                if (SupportsLetterSpacing(flattenedCodepoints[i].Codepoint))
                {
                    naturalLength += letterSpacing;
                }

                var wordSpacing = ResolveSpacingValue(styleSource, styleSource.WordSpacing, geometryBounds, naturalAdvances[i]);
                if (IsWhitespaceCodepoint(flattenedCodepoints[i].Codepoint))
                {
                    naturalLength += wordSpacing;
                }
            }
        }

        if (Math.Abs(naturalLength - specifiedLength) <= TextLengthTolerance)
        {
            return false;
        }

        var extraGapAdvance = 0f;
        var glyphScaleX = 1f;
        var scaleRunFromStart = false;
        totalAdvance = naturalLength;
        if (GetOwnLengthAdjust(svgTextBase) == SvgTextLengthAdjust.Spacing && flattenedCodepoints.Count > 1)
        {
            extraGapAdvance = (specifiedLength - totalAdvance) / (flattenedCodepoints.Count - 1);
            totalAdvance = specifiedLength;
        }
        else if (totalAdvance > 0f)
        {
            glyphScaleX = specifiedLength / totalAdvance;
            scaleRunFromStart = true;
            totalAdvance = specifiedLength;
        }

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

            var placementX = explicitX ?? defaultX;
            var placementY = explicitY ?? activeY;
            placementX += flattenedCodepoints[i].Dx;
            placementY += flattenedCodepoints[i].Dy;
            placements[i] = new PositionedCodepointPlacement(
                new SKPoint(placementX, placementY),
                0f,
                glyphScaleX,
                scaleRunFromStart ? currentX : placementX);

            if (i >= flattenedCodepoints.Count - 1)
            {
                continue;
            }

            var clusterAdvance = scaleRunFromStart
                ? naturalAdvances[i] * glyphScaleX
                : naturalAdvances[i];
            var styleSource = flattenedCodepoints[i].StyleSource;
            var letterSpacing = ResolveSpacingValue(styleSource, styleSource.LetterSpacing, geometryBounds, naturalAdvances[i]);
            if (!SupportsLetterSpacing(flattenedCodepoints[i].Codepoint))
            {
                letterSpacing = 0f;
            }

            var wordSpacing = ResolveSpacingValue(styleSource, styleSource.WordSpacing, geometryBounds, naturalAdvances[i]);
            if (!IsWhitespaceCodepoint(flattenedCodepoints[i].Codepoint))
            {
                wordSpacing = 0f;
            }

            clusterAdvance += letterSpacing + wordSpacing;
            if (!scaleRunFromStart)
            {
                clusterAdvance += extraGapAdvance;
            }

            if (!IsValidPositiveAdvance(clusterAdvance))
            {
                clusterAdvance = 0f;
            }

            defaultX += clusterAdvance;
        }

        finalY = activeY;
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

            runs.Add(new PositionedCodepointRun(groupStyle, builder.ToString(), groupPlacements.ToArray()));
            groupStart = groupIndex;
        }

        return runs.Count > 0;
    }

    private static bool TryCollectFlattenedTextCodepoints(
        SvgTextBase svgTextBase,
        bool trimLeadingWhitespaceAtStart,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out List<FlattenedTextCodepoint> codepoints)
    {
        codepoints = new List<FlattenedTextCodepoint>();
        var trimLeadingWhitespace = trimLeadingWhitespaceAtStart;
        var previousEndedWithSpace = false;
        if (!TryCollectFlattenedTextCodepoints(GetContentNodeList(svgTextBase), svgTextBase, codepoints, ref trimLeadingWhitespace, ref previousEndedWithSpace, viewport, assetLoader))
        {
            return false;
        }

        InjectCollapsedSiblingSpaces(svgTextBase, codepoints);
        ApplyExplicitPositionsToFlattenedRange(svgTextBase, codepoints, 0, codepoints.Count, viewport, assetLoader);
        return codepoints.Count > 0;
    }

    private static bool TryCollectFlattenedTextCodepoints(
        IEnumerable<ISvgNode> contentNodes,
        SvgTextBase styleSource,
        List<FlattenedTextCodepoint> codepoints,
        ref bool trimLeadingWhitespace,
        ref bool previousEndedWithSpace,
        SKRect viewport,
        ISvgAssetLoader assetLoader)
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

                    if (!TryCollectFlattenedTextCodepoints(GetContentNodeList(svgAnchor), CreateAnchorTextStyleSource(svgAnchor), codepoints, ref trimLeadingWhitespace, ref previousEndedWithSpace, viewport, assetLoader))
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
                        if (!TryCollectFlattenedTextCodepoints(GetContentNodeList(svgTextSpan), svgTextSpan, codepoints, ref childTrimLeadingWhitespace, ref childPreviousEndedWithSpace, viewport, assetLoader))
                        {
                            return false;
                        }

                        var childCount = codepoints.Count - childStart;
                        ApplyExplicitPositionsToFlattenedRange(svgTextSpan, codepoints, childStart, childCount, viewport, assetLoader);
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
                        styleSource.SpaceHandling != XmlSpaceHandling.Preserve)
                    {
                        text = " ";
                    }
                    else if (!string.IsNullOrWhiteSpace(rawContent) &&
                        styleSource.SpaceHandling != XmlSpaceHandling.Preserve &&
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
                            trimTrailingWhitespace: IsTerminalContentNode(contentNodeList, nodeIndex));
                    }

                    if (previousEndedWithSpace &&
                        styleSource.SpaceHandling != XmlSpaceHandling.Preserve &&
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
                    while (TryReadNextCodepoint(text!, ref charIndex, out var codepoint))
                    {
                        codepoints.Add(new FlattenedTextCodepoint(styleSource, codepoint));
                    }

                    trimLeadingWhitespace = false;
                    previousEndedWithSpace = text.EndsWith(" ", StringComparison.Ordinal);
                    break;
            }
        }

        return true;
    }

    private static void InjectCollapsedSiblingSpaces(SvgTextBase svgTextBase, List<FlattenedTextCodepoint> codepoints)
    {
        if (svgTextBase.SpaceHandling == XmlSpaceHandling.Preserve)
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
                            codepoints.Insert(insertionIndex++, new FlattenedTextCodepoint(svgTextBase, " "));
                            break;
                        }

                        var prepared = PrepareText(
                            svgTextBase,
                            contentNode.Content,
                            trimLeadingWhitespace: false,
                            trimTrailingWhitespace: IsTerminalContentNode(contentNodes, nodeIndex));
                        if (!string.IsNullOrEmpty(prepared))
                        {
                            insertionIndex += CountCodepoints(prepared);
                        }

                        break;
                    }
            }
        }
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
            codepoints[startIndex + i].X = ResolveTextUnitValue(svgTextBase.X[i], UnitRenderingType.HorizontalOffset, svgTextBase, viewport, assetLoader);
        }

        for (var i = 0; i < count && i < svgTextBase.Y.Count; i++)
        {
            codepoints[startIndex + i].Y = ResolveTextUnitValue(svgTextBase.Y[i], UnitRenderingType.VerticalOffset, svgTextBase, viewport, assetLoader);
        }

        for (var i = 0; i < count && i < svgTextBase.Dx.Count; i++)
        {
            codepoints[startIndex + i].Dx = ResolveTextUnitValue(svgTextBase.Dx[i], UnitRenderingType.HorizontalOffset, svgTextBase, viewport, assetLoader);
        }

        for (var i = 0; i < count && i < svgTextBase.Dy.Count; i++)
        {
            codepoints[startIndex + i].Dy = ResolveTextUnitValue(svgTextBase.Dy[i], UnitRenderingType.VerticalOffset, svgTextBase, viewport, assetLoader);
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
                            previousEndedWithSpace = prepared.EndsWith(" ", StringComparison.Ordinal);
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
                        svgTextBase.SpaceHandling != XmlSpaceHandling.Preserve &&
                        !string.IsNullOrEmpty(text) &&
                        text![0] == ' ')
                    {
                        text = text.TrimStart(' ');
                    }

                    if (!string.IsNullOrEmpty(text))
                    {
                        count += CountCodepoints(text!);
                        trimLeadingWhitespace = false;
                        previousEndedWithSpace = text.EndsWith(" ", StringComparison.Ordinal);
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

                    return text.EndsWith(" ", StringComparison.Ordinal);
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
        var referencedElement = SvgService.GetReference<SvgElement>(svgTextRef, svgTextRef.ReferencedElement);
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
                case SvgTextRef nestedReference:
                    var nestedElement = SvgService.GetReference<SvgElement>(nestedReference, nestedReference.ReferencedElement);
                    if (nestedElement is null)
                    {
                        continue;
                    }

                    if (!TryAppendReferencedElementContent(nestedElement, builder, visited))
                    {
                        visited.Remove(referencedElement);
                        return false;
                    }

                    break;

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
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var direction = GetInheritedTextAttribute(svgTextBase, "direction");
        var unicodeBidi = GetInheritedTextAttribute(svgTextBase, "unicode-bidi");
        if (TryGetVisualBidiText(text, direction, unicodeBidi, out var visualText))
        {
            return visualText;
        }

        if (string.Equals(unicodeBidi, "bidi-override", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase))
            {
                return "\u202E" + text + "\u202C";
            }

            if (string.Equals(direction, "ltr", StringComparison.OrdinalIgnoreCase))
            {
                return "\u202D" + text + "\u202C";
            }
        }

        if (string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase))
        {
            return "\u202B" + text + "\u202C";
        }

        if (string.Equals(direction, "ltr", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(unicodeBidi, "embed", StringComparison.OrdinalIgnoreCase))
        {
            return "\u202A" + text + "\u202C";
        }

        return text;
    }

    private static bool TryGetVisualBidiText(string text, string? direction, string? unicodeBidi, out string visualText)
    {
        visualText = text;
        if (string.IsNullOrEmpty(text) ||
            !ContainsMixedStrongDirections(text))
        {
            return false;
        }

        if (string.Equals(unicodeBidi, "bidi-override", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase))
            {
                visualText = ReverseByCodepoint(text);
                return !string.Equals(visualText, text, StringComparison.Ordinal);
            }

            return false;
        }

        if (!string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        visualText = ReorderRunsForRightToLeftBase(text);
        return !string.Equals(visualText, text, StringComparison.Ordinal);
    }

    private static bool ContainsMixedStrongDirections(string text)
    {
        var hasLeftToRight = false;
        var hasRightToLeft = false;

        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            switch (GetBidiStrongDirection(codepoint))
            {
                case 1:
                    hasLeftToRight = true;
                    break;
                case -1:
                    hasRightToLeft = true;
                    break;
            }

            if (hasLeftToRight && hasRightToLeft)
            {
                return true;
            }
        }

        return false;
    }

    private static string ReverseByCodepoint(string text)
    {
        var codepoints = SplitCodepoints(text);
        codepoints.Reverse();
        return string.Concat(codepoints);
    }

    private static string ReorderRunsForRightToLeftBase(string text)
    {
        var codepoints = SplitCodepoints(text);
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
        if (string.IsNullOrEmpty(codepoint))
        {
            return 0;
        }

        var scalar = char.ConvertToUtf32(codepoint, 0);
        if (IsRightToLeftCodepoint(scalar))
        {
            return -1;
        }

        return IsLeftToRightCodepoint(scalar) ? 1 : 0;
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
        if (converter.ConvertFromInvariantString(baselineShiftText) is SvgUnit unit)
        {
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

        shift = 0f;
        return false;
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
        if (bounds.IsEmpty)
        {
            return SKRect.Empty;
        }

        return SKRect.Create(
            0f,
            0f,
            Math.Abs(bounds.Left) + bounds.Width,
            Math.Abs(bounds.Top) + bounds.Height);
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
