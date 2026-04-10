using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ShimSkiaSharp;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

internal static class SvgSceneTextCompiler
{
    private static readonly Regex s_multipleSpaces = new(@" {2,}", RegexOptions.Compiled);

    private readonly record struct SequentialTextRun(SvgTextBase StyleSource, string Text);

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
        SvgSceneCompiler.AssignRetainedResourceKeys(node, svgTextBase);
        node.OpacityValue = SvgScenePaintingService.AdjustSvgOpacity(svgTextBase.Opacity);
        node.Fill = SvgScenePaintingService.IsValidFill(svgTextBase)
            ? SvgScenePaintingService.GetFillPaint(svgTextBase, SKRect.Empty, assetLoader, ignoreAttributes)
            : null;
        node.Stroke = SvgScenePaintingService.IsValidStroke(svgTextBase, SKRect.Empty)
            ? SvgScenePaintingService.GetStrokePaint(svgTextBase, SKRect.Empty, assetLoader, ignoreAttributes)
            : null;
        node.SupportsFillHitTest = SvgScenePaintingService.IsValidFill(svgTextBase);
        node.SupportsStrokeHitTest = SvgScenePaintingService.IsValidStroke(svgTextBase, SKRect.Empty);
        node.StrokeWidth = node.Stroke?.StrokeWidth ?? 0f;

        var geometryBounds = EstimateGeometryBounds(svgTextBase, viewport, assetLoader);
        node.GeometryBounds = geometryBounds;
        node.TransformedBounds = node.TotalTransform.MapRect(geometryBounds);

        if (!node.IsRenderable)
        {
            return true;
        }

        var cullRect = CreateLocalCullRect(geometryBounds);
        if (cullRect.IsEmpty)
        {
            node.IsRenderable = false;
            return true;
        }

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        DrawText(svgTextBase, viewport, ignoreAttributes | DrawAttributes.ClipPath | DrawAttributes.Mask | DrawAttributes.Opacity | DrawAttributes.Filter, canvas, assetLoader, references);
        var localModel = recorder.EndRecording();
        node.LocalModel = localModel.Commands is { Count: > 0 } ? localModel : null;

        if (node.LocalModel is null)
        {
            node.IsRenderable = false;
        }

        return true;
    }

    private static SKRect EstimateGeometryBounds(SvgTextBase svgTextBase, SKRect viewport, ISvgAssetLoader assetLoader)
    {
        var x = svgTextBase.X.Count >= 1 ? svgTextBase.X[0].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, viewport) : 0f;
        var y = svgTextBase.Y.Count >= 1 ? svgTextBase.Y[0].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, viewport) : 0f;
        var dx = svgTextBase.Dx.Count >= 1 ? svgTextBase.Dx[0].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, viewport) : 0f;
        var dy = svgTextBase.Dy.Count >= 1 ? svgTextBase.Dy[0].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, viewport) : 0f;

        var currentX = x + dx;
        var currentY = y + dy;
        var bounds = SKRect.Empty;
        MeasureTextBase(svgTextBase, ref currentX, ref currentY, viewport, assetLoader, ref bounds);
        return bounds;
    }

    private static void DrawText(
        SvgTextBase svgTextBase,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references)
    {
        var xs = new List<float>();
        var ys = new List<float>();
        var dxs = new List<float>();
        var dys = new List<float>();
        GetPositionsX(svgTextBase, viewport, xs);
        GetPositionsY(svgTextBase, viewport, ys);
        GetPositionsDX(svgTextBase, viewport, dxs);
        GetPositionsDY(svgTextBase, viewport, dys);

        var x = xs.Count >= 1 ? xs[0] : 0f;
        var y = ys.Count >= 1 ? ys[0] : 0f;
        var dx = dxs.Count >= 1 ? dxs[0] : 0f;
        var dy = dys.Count >= 1 ? dys[0] : 0f;
        var currentX = x + dx;
        var currentY = y + dy;

        DrawTextBase(svgTextBase, ref currentX, ref currentY, viewport, ignoreAttributes, canvas, assetLoader, references, EstimateGeometryBounds(svgTextBase, viewport, assetLoader));
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
        GetPositionsX(svgTextBase, viewport, xs);
        GetPositionsY(svgTextBase, viewport, ys);
        GetPositionsDX(svgTextBase, viewport, dxs);
        GetPositionsDY(svgTextBase, viewport, dys);

        var x = xs.Count >= 1 ? xs[0] : 0f;
        var y = ys.Count >= 1 ? ys[0] : 0f;
        var dx = dxs.Count >= 1 ? dxs[0] : 0f;
        var dy = dys.Count >= 1 ? dys[0] : 0f;
        var currentX = x + dx;
        var currentY = y + dy;

        if (TryAppendSequentialTextRunsClipPath(svgTextBase, ref currentX, ref currentY, geometryBounds, assetLoader, path))
        {
            return path.IsEmpty ? null : path;
        }

        var useInitialPosition = true;
        AppendTextClipPathNodes(GetContentNodes(svgTextBase), svgTextBase, ref currentX, ref currentY, ref useInitialPosition, viewport, assetLoader, geometryBounds, path);
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
        SKRect rootGeometryBounds)
    {
        if (TryDrawSequentialTextRuns(svgTextBase, ref currentX, ref currentY, rootGeometryBounds, ignoreAttributes, canvas, assetLoader))
        {
            return;
        }

        var useInitialPosition = true;
        DrawTextNodes(GetContentNodes(svgTextBase), svgTextBase, ref currentX, ref currentY, ref useInitialPosition, viewport, ignoreAttributes, canvas, assetLoader, references, rootGeometryBounds);
    }

    private static bool TryAppendSequentialTextRunsClipPath(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        SKPath path)
    {
        if (!TryCollectSequentialTextRuns(svgTextBase, requireAnchorContent: true, out var runs))
        {
            return false;
        }

        var totalAdvance = MeasureSequentialTextRuns(runs, geometryBounds, assetLoader);
        var startX = ApplyTextAnchor(svgTextBase, currentX, geometryBounds, totalAdvance);
        var drawX = startX;

        for (var i = 0; i < runs.Count; i++)
        {
            AppendTextStringPathAlignedLeft(runs[i].StyleSource, runs[i].Text, ref drawX, currentY, geometryBounds, assetLoader, path);
        }

        currentX = startX + totalAdvance;
        return true;
    }

    private static void AppendTextClipPathNodes(
        IEnumerable<ISvgNode> contentNodes,
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        ref bool useInitialPosition,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        SKRect rootGeometryBounds,
        SKPath path)
    {
        foreach (var node in contentNodes)
        {
            switch (node)
            {
                case SvgAnchor svgAnchor:
                    AppendTextClipPathNodes(GetContentNodes(svgAnchor), svgTextBase, ref currentX, ref currentY, ref useInitialPosition, viewport, assetLoader, rootGeometryBounds, path);
                    break;

                case not SvgTextBase:
                    if (string.IsNullOrEmpty(node.Content))
                    {
                        break;
                    }

                    var text = PrepareText(svgTextBase, node.Content, trimLeadingWhitespace: useInitialPosition);
                    if (string.IsNullOrEmpty(text))
                    {
                        break;
                    }

                    var xs = new List<float>();
                    var ys = new List<float>();
                    var dxs = new List<float>();
                    var dys = new List<float>();
                    GetPositionsX(svgTextBase, viewport, xs);
                    GetPositionsY(svgTextBase, viewport, ys);
                    GetPositionsDX(svgTextBase, viewport, dxs);
                    GetPositionsDY(svgTextBase, viewport, dys);

                    if (useInitialPosition &&
                        TryCreatePositionedCodepointPoints(text!, xs, ys, dxs, dys, out var positionedPoints))
                    {
                        AppendPositionedTextStringPath(svgTextBase, text!, positionedPoints, rootGeometryBounds, assetLoader, path);
                        MeasurePositionedTextStringBounds(svgTextBase, text!, positionedPoints, rootGeometryBounds, assetLoader, out var positionedAdvance);
                        currentX = positionedPoints[positionedPoints.Length - 1].X + positionedAdvance;
                        currentY = positionedPoints[positionedPoints.Length - 1].Y;
                        useInitialPosition = false;
                        break;
                    }

                    var x = useInitialPosition && xs.Count >= 1 ? xs[0] : currentX;
                    var y = useInitialPosition && ys.Count >= 1 ? ys[0] : currentY;
                    var dx = useInitialPosition && dxs.Count >= 1 ? dxs[0] : 0f;
                    var dy = useInitialPosition && dys.Count >= 1 ? dys[0] : 0f;
                    currentX = x + dx;
                    currentY = y + dy;
                    AppendTextStringPath(svgTextBase, text!, currentX, currentY, rootGeometryBounds, assetLoader, path);
                    MeasureTextStringBounds(svgTextBase, text!, currentX, currentY, rootGeometryBounds, assetLoader, out var advance);
                    currentX += advance;
                    useInitialPosition = false;
                    break;

                case SvgTextPath svgTextPath:
                    AppendTextPathClip(svgTextPath, ref currentX, ref currentY, viewport, assetLoader, path);
                    useInitialPosition = false;
                    break;

                case SvgTextRef svgTextRef:
                    AppendTextRefClip(svgTextRef, ref currentX, ref currentY, viewport, assetLoader, rootGeometryBounds, path);
                    useInitialPosition = false;
                    break;

                case SvgTextSpan svgTextSpan:
                    AppendTextClipPathBase(svgTextSpan, ref currentX, ref currentY, viewport, assetLoader, rootGeometryBounds, path);
                    useInitialPosition = false;
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
        SKPath path)
    {
        if (TryAppendSequentialTextRunsClipPath(svgTextBase, ref currentX, ref currentY, rootGeometryBounds, assetLoader, path))
        {
            return;
        }

        var useInitialPosition = true;
        AppendTextClipPathNodes(GetContentNodes(svgTextBase), svgTextBase, ref currentX, ref currentY, ref useInitialPosition, viewport, assetLoader, rootGeometryBounds, path);
    }

    private static void DrawTextNodes(
        IEnumerable<ISvgNode> contentNodes,
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        ref bool useInitialPosition,
        SKRect viewport,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader,
        HashSet<Uri>? references,
        SKRect rootGeometryBounds)
    {
        foreach (var node in contentNodes)
        {
            switch (node)
            {
                case SvgAnchor svgAnchor:
                    DrawTextNodes(GetContentNodes(svgAnchor), svgTextBase, ref currentX, ref currentY, ref useInitialPosition, viewport, ignoreAttributes, canvas, assetLoader, references, rootGeometryBounds);
                    break;

                case not SvgTextBase:
                    if (string.IsNullOrEmpty(node.Content))
                    {
                        break;
                    }

                    var text = PrepareText(svgTextBase, node.Content, trimLeadingWhitespace: useInitialPosition);
                    var isValidFill = SvgScenePaintingService.IsValidFill(svgTextBase);
                    var isValidStroke = SvgScenePaintingService.IsValidStroke(svgTextBase, rootGeometryBounds);

                    if ((!isValidFill && !isValidStroke) || string.IsNullOrEmpty(text))
                    {
                        break;
                    }

                    var xs = new List<float>();
                    var ys = new List<float>();
                    var dxs = new List<float>();
                    var dys = new List<float>();
                    GetPositionsX(svgTextBase, viewport, xs);
                    GetPositionsY(svgTextBase, viewport, ys);
                    GetPositionsDX(svgTextBase, viewport, dxs);
                    GetPositionsDY(svgTextBase, viewport, dys);

                    if (useInitialPosition &&
                        TryCreatePositionedCodepointPoints(text!, xs, ys, dxs, dys, out var positionedPoints))
                    {
                        var fillAdvance = 0f;
                        if (SvgScenePaintingService.IsValidFill(svgTextBase))
                        {
                            var fillPaint = SvgScenePaintingService.GetFillPaint(svgTextBase, rootGeometryBounds, assetLoader, ignoreAttributes);
                            if (fillPaint is not null)
                            {
                                fillAdvance = DrawPositionedTextRuns(svgTextBase, text!, positionedPoints, rootGeometryBounds, fillPaint, canvas, assetLoader);
                            }
                        }

                        var strokeAdvance = 0f;
                        if (SvgScenePaintingService.IsValidStroke(svgTextBase, rootGeometryBounds))
                        {
                            var strokePaint = SvgScenePaintingService.GetStrokePaint(svgTextBase, rootGeometryBounds, assetLoader, ignoreAttributes);
                            if (strokePaint is not null)
                            {
                                strokeAdvance = DrawPositionedTextRuns(svgTextBase, text!, positionedPoints, rootGeometryBounds, strokePaint, canvas, assetLoader);
                            }
                        }

                        currentX = positionedPoints[positionedPoints.Length - 1].X + Math.Max(fillAdvance, strokeAdvance);
                        currentY = positionedPoints[positionedPoints.Length - 1].Y;
                        useInitialPosition = false;
                        break;
                    }

                    var x = useInitialPosition && xs.Count >= 1 ? xs[0] : currentX;
                    var y = useInitialPosition && ys.Count >= 1 ? ys[0] : currentY;
                    var dx = useInitialPosition && dxs.Count >= 1 ? dxs[0] : 0f;
                    var dy = useInitialPosition && dys.Count >= 1 ? dys[0] : 0f;
                    currentX = x + dx;
                    currentY = y + dy;
                    DrawTextString(svgTextBase, text!, ref currentX, ref currentY, rootGeometryBounds, ignoreAttributes, canvas, assetLoader, references);
                    useInitialPosition = false;
                    break;

                case SvgTextPath svgTextPath:
                    DrawTextPath(svgTextPath, ref currentX, ref currentY, viewport, ignoreAttributes, canvas, assetLoader, references);
                    useInitialPosition = false;
                    break;

                case SvgTextRef svgTextRef:
                    DrawTextRef(svgTextRef, ref currentX, ref currentY, viewport, ignoreAttributes, canvas, assetLoader, references, rootGeometryBounds);
                    useInitialPosition = false;
                    break;

                case SvgTextSpan svgTextSpan:
                    DrawTextBase(svgTextSpan, ref currentX, ref currentY, viewport, ignoreAttributes, canvas, assetLoader, references, rootGeometryBounds);
                    useInitialPosition = false;
                    break;
            }
        }
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
        HashSet<Uri>? references)
    {
        var fillAdvance = 0f;
        if (SvgScenePaintingService.IsValidFill(svgTextBase))
        {
            var fillPaint = SvgScenePaintingService.GetFillPaint(svgTextBase, geometryBounds, assetLoader, ignoreAttributes);
            if (fillPaint is not null)
            {
                fillAdvance = DrawTextRuns(svgTextBase, text, x, y, geometryBounds, fillPaint, canvas, assetLoader);
            }
        }

        var strokeAdvance = 0f;
        if (SvgScenePaintingService.IsValidStroke(svgTextBase, geometryBounds))
        {
            var strokePaint = SvgScenePaintingService.GetStrokePaint(svgTextBase, geometryBounds, assetLoader, ignoreAttributes);
            if (strokePaint is not null)
            {
                strokeAdvance = DrawTextRuns(svgTextBase, text, x, y, geometryBounds, strokePaint, canvas, assetLoader);
            }
        }

        x += Math.Max(strokeAdvance, fillAdvance);
    }

    private static void AppendTextStringPath(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        SKPath path)
    {
        AppendTextRunsPath(svgTextBase, text, anchorX, anchorY, geometryBounds, assetLoader, path, forceLeftAlign: false);
    }

    private static void AppendTextStringPathAlignedLeft(
        SvgTextBase svgTextBase,
        string text,
        ref float x,
        float y,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        SKPath path)
    {
        x += AppendTextRunsPath(svgTextBase, text, x, y, geometryBounds, assetLoader, path, forceLeftAlign: true);
    }

    private static float AppendTextRunsPath(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect geometryBounds,
        ISvgAssetLoader assetLoader,
        SKPath targetPath,
        bool forceLeftAlign)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);

        var currentX = anchorX;
        SvgFontTextRenderer.SvgFontLayout? svgFontLayout = null;
        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, text, paint, assetLoader, out var candidateLayout))
        {
            svgFontLayout = candidateLayout;
        }

        if (!forceLeftAlign)
        {
            var totalAdvance = 0f;
            if (svgFontLayout is not null)
            {
                totalAdvance = svgFontLayout.Advance;
            }
            else
            {
                var typefaceSpans = assetLoader.FindTypefaces(text, paint);
                if (typefaceSpans.Count > 0)
                {
                    for (var i = 0; i < typefaceSpans.Count; i++)
                    {
                        totalAdvance += typefaceSpans[i].Advance;
                    }
                }
                else
                {
                    var bounds = new SKRect();
                    totalAdvance = assetLoader.MeasureText(text, paint, ref bounds);
                }
            }

            if (paint.TextAlign == SKTextAlign.Center)
            {
                currentX -= totalAdvance * 0.5f;
            }
            else if (paint.TextAlign == SKTextAlign.Right)
            {
                currentX -= totalAdvance;
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
        SKPath path)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

        var pointIndex = 0;
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            var point = points[pointIndex++];
            var localPaint = paint.Clone();
            if (SvgFontTextRenderer.TryGetLayout(svgTextBase, codepoint, localPaint, assetLoader, out var svgFontLayout) &&
                svgFontLayout is not null)
            {
                svgFontLayout.AppendPath(path, point.X, point.Y);
                continue;
            }

            var fallbackCodepoint = GetBrowserCompatibleFallbackText(svgTextBase, codepoint, assetLoader);
            var typefaceSpans = assetLoader.FindTypefaces(fallbackCodepoint, localPaint);
            if (typefaceSpans.Count > 0)
            {
                localPaint.Typeface = typefaceSpans[0].Typeface;
            }

            AppendPathCommands(path, assetLoader.GetTextPath(fallbackCodepoint, localPaint, point.X, point.Y));
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
        ISvgAssetLoader assetLoader)
    {
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);

        var textAlign = paint.TextAlign;
        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, text, paint, assetLoader, out var svgFontLayout) && svgFontLayout is not null)
        {
            var startX = anchorX;
            if (textAlign == SKTextAlign.Center)
            {
                startX -= svgFontLayout.Advance * 0.5f;
            }
            else if (textAlign == SKTextAlign.Right)
            {
                startX -= svgFontLayout.Advance;
            }

            paint.TextAlign = SKTextAlign.Left;
            svgFontLayout.Draw(canvas, paint, startX, anchorY);
            return svgFontLayout.Advance;
        }

        var fallbackText = GetBrowserCompatibleFallbackText(svgTextBase, text, assetLoader);
        var typefaceSpans = assetLoader.FindTypefaces(fallbackText, paint);
        if (typefaceSpans.Count == 0)
        {
            return 0f;
        }

        var totalAdvance = 0f;
        foreach (var span in typefaceSpans)
        {
            totalAdvance += span.Advance;
        }

        var currentX = anchorX;
        if (textAlign == SKTextAlign.Center)
        {
            currentX -= totalAdvance * 0.5f;
        }
        else if (textAlign == SKTextAlign.Right)
        {
            currentX -= totalAdvance;
        }

        paint.TextAlign = SKTextAlign.Left;
        var isRightToLeft = IsRightToLeft(svgTextBase);

        var startIndex = isRightToLeft ? typefaceSpans.Count - 1 : 0;
        var endIndex = isRightToLeft ? -1 : typefaceSpans.Count;
        var step = isRightToLeft ? -1 : 1;
        for (var i = startIndex; i != endIndex; i += step)
        {
            var typefaceSpan = typefaceSpans[i];
            paint.Typeface = typefaceSpan.Typeface;
            canvas.DrawText(typefaceSpan.Text, currentX, anchorY, paint);
            currentX += typefaceSpan.Advance;
            paint = paint.Clone();
        }

        return totalAdvance;
    }

    private static bool IsRightToLeft(SvgTextBase svgTextBase)
    {
        return svgTextBase.TryGetAttribute("direction", out var direction) &&
               direction.Equals("rtl", StringComparison.OrdinalIgnoreCase);
    }

    private static float DrawPositionedTextRuns(
        SvgTextBase svgTextBase,
        string text,
        SKPoint[] points,
        SKRect geometryBounds,
        SKPaint paint,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader)
    {
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

        var fallbackText = GetBrowserCompatibleFallbackText(svgTextBase, text, assetLoader);
        if (!HasPositionedSvgFontLayouts(svgTextBase, text, paint, assetLoader))
        {
            return DrawPositionedTextRunsFallback(fallbackText, points, paint, canvas, assetLoader);
        }

        var advance = 0f;
        var pointIndex = 0;
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            var point = points[pointIndex++];
            var localPaint = paint.Clone();
            if (SvgFontTextRenderer.TryGetLayout(svgTextBase, codepoint, localPaint, assetLoader, out var svgFontLayout) &&
                svgFontLayout is not null)
            {
                svgFontLayout.Draw(canvas, localPaint, point.X, point.Y);
                advance = svgFontLayout.Advance;
                continue;
            }

            var fallbackCodepoint = GetBrowserCompatibleFallbackText(svgTextBase, codepoint, assetLoader);
            var typefaceSpans = assetLoader.FindTypefaces(fallbackCodepoint, localPaint);
            if (typefaceSpans.Count > 0)
            {
                localPaint.Typeface = typefaceSpans[0].Typeface;
                canvas.DrawText(typefaceSpans[0].Text, point.X, point.Y, localPaint);
                advance = typefaceSpans[0].Advance;
                continue;
            }

            canvas.DrawText(fallbackCodepoint, point.X, point.Y, localPaint);
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
        string text,
        SKPoint[] points,
        SKPaint paint,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader)
    {
        var lastCodepointStart = GetLastCodepointStart(text);
        var leadingText = text.Substring(0, lastCodepointStart);
        if (!string.IsNullOrEmpty(leadingText))
        {
            var offset = 0;
            foreach (var typefaceSpan in assetLoader.FindTypefaces(leadingText, paint))
            {
                var localPaint = paint.Clone();
                localPaint.Typeface = typefaceSpan.Typeface;

                var codepointCount = CountCodepoints(typefaceSpan.Text);
                var spanPoints = new SKPoint[codepointCount];
                Array.Copy(points, offset, spanPoints, 0, codepointCount);

                var textBlob = SKTextBlob.CreatePositioned(typefaceSpan.Text, spanPoints);
                canvas.DrawText(textBlob, 0, 0, localPaint);
                offset += codepointCount;
            }
        }

        var trailingText = text.Substring(lastCodepointStart);
        foreach (var typefaceSpan in assetLoader.FindTypefaces(trailingText, paint))
        {
            var localPaint = paint.Clone();
            localPaint.Typeface = typefaceSpan.Typeface;
            canvas.DrawText(typefaceSpan.Text, points[points.Length - 1].X, points[points.Length - 1].Y, localPaint);
            return typefaceSpan.Advance;
        }

        var fallbackPaint = paint.Clone();
        canvas.DrawText(trailingText, points[points.Length - 1].X, points[points.Length - 1].Y, fallbackPaint);
        var fallbackBounds = new SKRect();
        return assetLoader.MeasureText(trailingText, fallbackPaint, ref fallbackBounds);
    }

    private static void DrawTextPath(
        SvgTextPath svgTextPath,
        ref float currentX,
        ref float currentY,
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
            return;
        }

        var svgPath = SvgService.GetReference<SvgPath>(svgTextPath, svgTextPath.ReferencedPath);
        var skPath = svgPath?.PathData?.ToPath(svgPath.FillRule);
        if (skPath is null || skPath.IsEmpty)
        {
            return;
        }

        var geometryBounds = skPath.Bounds;
        var startOffset = svgTextPath.StartOffset.ToDeviceValue(UnitRenderingType.Other, svgTextPath, viewport);
        var hOffset = currentX + startOffset;
        var vOffset = currentY;
        var text = PrepareText(svgTextPath, svgTextPath.Text);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (SvgScenePaintingService.IsValidFill(svgTextPath))
        {
            var fillPaint = SvgScenePaintingService.GetFillPaint(svgTextPath, geometryBounds, assetLoader, ignoreAttributes);
            if (fillPaint is not null)
            {
                PaintingService.SetPaintText(svgTextPath, geometryBounds, fillPaint);
                canvas.DrawTextOnPath(text!, skPath, hOffset, vOffset, fillPaint);
            }
        }

        if (SvgScenePaintingService.IsValidStroke(svgTextPath, geometryBounds))
        {
            var strokePaint = SvgScenePaintingService.GetStrokePaint(svgTextPath, geometryBounds, assetLoader, ignoreAttributes);
            if (strokePaint is not null)
            {
                PaintingService.SetPaintText(svgTextPath, geometryBounds, strokePaint);
                canvas.DrawTextOnPath(text!, skPath, hOffset, vOffset, strokePaint);
            }
        }
    }

    private static void AppendTextPathClip(
        SvgTextPath svgTextPath,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        SKPath path)
    {
        if (SvgService.HasRecursiveReference(svgTextPath, static e => e.ReferencedPath, new HashSet<Uri>()))
        {
            return;
        }

        var svgPath = SvgService.GetReference<SvgPath>(svgTextPath, svgTextPath.ReferencedPath);
        var skPath = svgPath?.PathData?.ToPath(svgPath.FillRule);
        if (skPath is null || skPath.IsEmpty)
        {
            return;
        }

        var text = PrepareText(svgTextPath, svgTextPath.Text);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextPath, skPath.Bounds, paint);
        var metrics = assetLoader.GetFontMetrics(paint);
        var inflate = Math.Max(Math.Abs(metrics.Ascent), Math.Abs(metrics.Descent));
        var pathBounds = skPath.Bounds;
        path.AddRect(SKRect.Create(
            pathBounds.Left,
            pathBounds.Top - inflate,
            pathBounds.Width,
            pathBounds.Height + (inflate * 2f)));
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
        SKRect rootGeometryBounds)
    {
        if (!HasFeatures(svgTextRef, ignoreAttributes) ||
            !MaskingService.CanDraw(svgTextRef, ignoreAttributes) ||
            SvgService.HasRecursiveReference(svgTextRef, static e => e.ReferencedElement, new HashSet<Uri>()))
        {
            return;
        }

        var svgReferencedText = SvgService.GetReference<SvgText>(svgTextRef, svgTextRef.ReferencedElement);
        if (svgReferencedText is null)
        {
            return;
        }

        DrawTextBase(svgReferencedText, ref currentX, ref currentY, viewport, ignoreAttributes, canvas, assetLoader, references, rootGeometryBounds);
    }

    private static void AppendTextRefClip(
        SvgTextRef svgTextRef,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        SKRect rootGeometryBounds,
        SKPath path)
    {
        if (SvgService.HasRecursiveReference(svgTextRef, static e => e.ReferencedElement, new HashSet<Uri>()))
        {
            return;
        }

        var svgReferencedText = SvgService.GetReference<SvgText>(svgTextRef, svgTextRef.ReferencedElement);
        if (svgReferencedText is null)
        {
            return;
        }

        AppendTextClipPathBase(svgReferencedText, ref currentX, ref currentY, viewport, assetLoader, rootGeometryBounds, path);
    }

    private static void MeasureTextBase(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds)
    {
        if (TryMeasureSequentialTextRuns(svgTextBase, ref currentX, ref currentY, viewport, assetLoader, ref bounds))
        {
            return;
        }

        var useInitialPosition = true;
        MeasureTextNodes(GetContentNodes(svgTextBase), svgTextBase, ref currentX, ref currentY, ref useInitialPosition, viewport, assetLoader, ref bounds);
    }

    private static void MeasureTextNodes(
        IEnumerable<ISvgNode> contentNodes,
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        ref bool useInitialPosition,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds)
    {
        foreach (var node in contentNodes)
        {
            switch (node)
            {
                case SvgAnchor svgAnchor:
                    MeasureTextNodes(GetContentNodes(svgAnchor), svgTextBase, ref currentX, ref currentY, ref useInitialPosition, viewport, assetLoader, ref bounds);
                    break;

                case not SvgTextBase:
                    if (string.IsNullOrEmpty(node.Content))
                    {
                        break;
                    }

                    var text = PrepareText(svgTextBase, node.Content, trimLeadingWhitespace: useInitialPosition);
                    if (string.IsNullOrEmpty(text))
                    {
                        break;
                    }

                    var xs = new List<float>();
                    var ys = new List<float>();
                    var dxs = new List<float>();
                    var dys = new List<float>();
                    GetPositionsX(svgTextBase, viewport, xs);
                    GetPositionsY(svgTextBase, viewport, ys);
                    GetPositionsDX(svgTextBase, viewport, dxs);
                    GetPositionsDY(svgTextBase, viewport, dys);

                    if (useInitialPosition &&
                        TryCreatePositionedCodepointPoints(text!, xs, ys, dxs, dys, out var positionedPoints))
                    {
                        var positionedTextBounds = MeasurePositionedTextStringBounds(svgTextBase, text!, positionedPoints, viewport, assetLoader, out var positionedAdvance);
                        UnionBounds(ref bounds, positionedTextBounds);
                        currentX = positionedPoints[positionedPoints.Length - 1].X + positionedAdvance;
                        currentY = positionedPoints[positionedPoints.Length - 1].Y;
                        useInitialPosition = false;
                        break;
                    }

                    var x = useInitialPosition && xs.Count >= 1 ? xs[0] : currentX;
                    var y = useInitialPosition && ys.Count >= 1 ? ys[0] : currentY;
                    var dx = useInitialPosition && dxs.Count >= 1 ? dxs[0] : 0f;
                    var dy = useInitialPosition && dys.Count >= 1 ? dys[0] : 0f;
                    currentX = x + dx;
                    currentY = y + dy;

                    var textBounds = MeasureTextStringBounds(svgTextBase, text!, currentX, currentY, viewport, assetLoader, out var advance);
                    UnionBounds(ref bounds, textBounds);
                    currentX += advance;
                    useInitialPosition = false;
                    break;

                case SvgTextPath svgTextPath:
                    MeasureTextPath(svgTextPath, ref currentX, ref currentY, viewport, assetLoader, ref bounds);
                    useInitialPosition = false;
                    break;

                case SvgTextRef svgTextRef:
                    MeasureTextRef(svgTextRef, ref currentX, ref currentY, viewport, assetLoader, ref bounds);
                    useInitialPosition = false;
                    break;

                case SvgTextSpan svgTextSpan:
                    MeasureTextBase(svgTextSpan, ref currentX, ref currentY, viewport, assetLoader, ref bounds);
                    useInitialPosition = false;
                    break;
            }
        }
    }

    private static void MeasureTextPath(
        SvgTextPath svgTextPath,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds)
    {
        if (SvgService.HasRecursiveReference(svgTextPath, static e => e.ReferencedPath, new HashSet<Uri>()))
        {
            return;
        }

        var svgPath = SvgService.GetReference<SvgPath>(svgTextPath, svgTextPath.ReferencedPath);
        var skPath = svgPath?.PathData?.ToPath(svgPath.FillRule);
        if (skPath is null || skPath.IsEmpty)
        {
            return;
        }

        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextPath, skPath.Bounds, paint);
        var metrics = assetLoader.GetFontMetrics(paint);
        var inflate = Math.Max(Math.Abs(metrics.Ascent), Math.Abs(metrics.Descent));
        var pathBounds = skPath.Bounds;
        var measuredBounds = SKRect.Create(
            pathBounds.Left,
            pathBounds.Top - inflate,
            pathBounds.Width,
            pathBounds.Height + (inflate * 2f));
        UnionBounds(ref bounds, measuredBounds);
    }

    private static void MeasureTextRef(
        SvgTextRef svgTextRef,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds)
    {
        if (SvgService.HasRecursiveReference(svgTextRef, static e => e.ReferencedElement, new HashSet<Uri>()))
        {
            return;
        }

        var svgReferencedText = SvgService.GetReference<SvgText>(svgTextRef, svgTextRef.ReferencedElement);
        if (svgReferencedText is null)
        {
            return;
        }

        MeasureTextBase(svgReferencedText, ref currentX, ref currentY, viewport, assetLoader, ref bounds);
    }

    private static SKRect MeasureTextStringBounds(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out float advance)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, viewport, paint);

        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, text, paint, assetLoader, out var svgFontLayout) && svgFontLayout is not null)
        {
            advance = svgFontLayout.Advance;
            var svgStartX = anchorX;
            if (paint.TextAlign == SKTextAlign.Center)
            {
                svgStartX -= svgFontLayout.Advance * 0.5f;
            }
            else if (paint.TextAlign == SKTextAlign.Right)
            {
                svgStartX -= svgFontLayout.Advance;
            }

            return svgFontLayout.GetBounds(svgStartX, anchorY);
        }

        var fallbackText = GetBrowserCompatibleFallbackText(svgTextBase, text, assetLoader);
        var totalAdvance = 0f;
        var typefaceSpans = assetLoader.FindTypefaces(fallbackText, paint);
        if (typefaceSpans.Count > 0)
        {
            foreach (var span in typefaceSpans)
            {
                totalAdvance += span.Advance;
            }
        }
        else
        {
            var scratchBounds = new SKRect();
            totalAdvance = assetLoader.MeasureText(fallbackText, paint, ref scratchBounds);
        }

        var startX = anchorX;
        if (paint.TextAlign == SKTextAlign.Center)
        {
            startX -= totalAdvance * 0.5f;
        }
        else if (paint.TextAlign == SKTextAlign.Right)
        {
            startX -= totalAdvance;
        }

        var metrics = assetLoader.GetFontMetrics(paint);
        advance = totalAdvance;
        return new SKRect(startX, anchorY + metrics.Ascent, startX + totalAdvance, anchorY + metrics.Descent);
    }

    private static SKRect MeasurePositionedTextStringBounds(
        SvgTextBase svgTextBase,
        string text,
        SKPoint[] points,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out float advance)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, viewport, paint);
        paint.TextAlign = SKTextAlign.Left;

        var bounds = SKRect.Empty;
        advance = 0f;

        var pointIndex = 0;
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            var point = points[pointIndex];
            var localPaint = paint.Clone();
            if (SvgFontTextRenderer.TryGetLayout(svgTextBase, codepoint, localPaint, assetLoader, out var svgFontLayout) &&
                svgFontLayout is not null)
            {
                UnionBounds(ref bounds, svgFontLayout.GetBounds(point.X, point.Y));
                advance = svgFontLayout.Advance;
                pointIndex++;
                continue;
            }

            var fallbackCodepoint = GetBrowserCompatibleFallbackText(svgTextBase, codepoint, assetLoader);
            var typefaceSpans = assetLoader.FindTypefaces(fallbackCodepoint, localPaint);
            if (typefaceSpans.Count > 0)
            {
                localPaint.Typeface = typefaceSpans[0].Typeface;
                MeasurePositionedCodepoints(typefaceSpans[0].Text, points, localPaint, assetLoader, ref bounds, ref pointIndex, ref advance);
                continue;
            }

            MeasurePositionedCodepoints(fallbackCodepoint, points, localPaint, assetLoader, ref bounds, ref pointIndex, ref advance);
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

    private static void GetPositionsX(SvgTextBase svgTextBase, SKRect viewport, List<float> xs)
    {
        for (var i = 0; i < svgTextBase.X.Count; i++)
        {
            xs.Add(svgTextBase.X[i].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, viewport));
        }
    }

    private static void GetPositionsY(SvgTextBase svgTextBase, SKRect viewport, List<float> ys)
    {
        for (var i = 0; i < svgTextBase.Y.Count; i++)
        {
            ys.Add(svgTextBase.Y[i].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, viewport));
        }
    }

    private static void GetPositionsDX(SvgTextBase svgTextBase, SKRect viewport, List<float> dxs)
    {
        for (var i = 0; i < svgTextBase.Dx.Count; i++)
        {
            dxs.Add(svgTextBase.Dx[i].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, viewport));
        }
    }

    private static void GetPositionsDY(SvgTextBase svgTextBase, SKRect viewport, List<float> dys)
    {
        for (var i = 0; i < svgTextBase.Dy.Count; i++)
        {
            dys.Add(svgTextBase.Dy[i].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, viewport));
        }
    }

    private static bool TryCreatePositionedCodepointPoints(
        string text,
        IReadOnlyList<float> xs,
        IReadOnlyList<float> ys,
        IReadOnlyList<float> dxs,
        IReadOnlyList<float> dys,
        out SKPoint[] points)
    {
        var codepointCount = CountCodepoints(text);
        if (xs.Count < 1 || ys.Count < 1 || xs.Count != ys.Count || xs.Count != codepointCount)
        {
            points = Array.Empty<SKPoint>();
            return false;
        }

        points = new SKPoint[codepointCount];
        for (var i = 0; i < codepointCount; i++)
        {
            var dx = dxs.Count >= 1 && i < dxs.Count ? dxs[i] : 0f;
            var dy = dys.Count >= 1 && i < dys.Count ? dys[i] : 0f;
            points[i] = new SKPoint(xs[i] + dx, ys[i] + dy);
        }

        return true;
    }

    private static void MeasurePositionedCodepoints(
        string text,
        SKPoint[] points,
        SKPaint paint,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds,
        ref int pointIndex,
        ref float advance)
    {
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            var glyphBounds = new SKRect();
            var glyphAdvance = assetLoader.MeasureText(codepoint, paint, ref glyphBounds);
            var metrics = assetLoader.GetFontMetrics(paint);
            var point = points[pointIndex++];
            var candidate = glyphBounds.IsEmpty
                ? new SKRect(point.X, point.Y + metrics.Ascent, point.X + glyphAdvance, point.Y + metrics.Descent)
                : new SKRect(point.X + glyphBounds.Left, point.Y + glyphBounds.Top, point.X + glyphBounds.Right, point.Y + glyphBounds.Bottom);
            UnionBounds(ref bounds, candidate);
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

    private static bool TryDrawSequentialTextRuns(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader)
    {
        if (!TryCollectSequentialTextRuns(svgTextBase, requireAnchorContent: true, out var runs))
        {
            return false;
        }

        var totalAdvance = MeasureSequentialTextRuns(runs, geometryBounds, assetLoader);
        var startX = ApplyTextAnchor(svgTextBase, currentX, geometryBounds, totalAdvance);
        var drawX = startX;

        for (var i = 0; i < runs.Count; i++)
        {
            DrawTextStringAlignedLeft(runs[i].StyleSource, runs[i].Text, ref drawX, ref currentY, geometryBounds, ignoreAttributes, canvas, assetLoader);
        }

        currentX = startX + totalAdvance;
        return true;
    }

    private static bool TryMeasureSequentialTextRuns(
        SvgTextBase svgTextBase,
        ref float currentX,
        ref float currentY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        ref SKRect bounds)
    {
        if (!TryCollectSequentialTextRuns(svgTextBase, requireAnchorContent: true, out var runs))
        {
            return false;
        }

        var totalAdvance = MeasureSequentialTextRuns(runs, viewport, assetLoader);
        var startX = ApplyTextAnchor(svgTextBase, currentX, viewport, totalAdvance);
        var drawX = startX;

        for (var i = 0; i < runs.Count; i++)
        {
            var runBounds = MeasureTextStringBoundsAlignedLeft(runs[i].StyleSource, runs[i].Text, drawX, currentY, viewport, assetLoader, out var runAdvance);
            UnionBounds(ref bounds, runBounds);
            drawX += runAdvance;
        }

        currentX = startX + totalAdvance;
        return true;
    }

    private static bool TryCollectSequentialTextRuns(SvgTextBase svgTextBase, bool requireAnchorContent, out List<SequentialTextRun> runs)
    {
        runs = new List<SequentialTextRun>();
        var hasAnchorContent = false;
        if (!TryCollectSequentialTextRuns(GetContentNodes(svgTextBase), svgTextBase, runs, ref hasAnchorContent, isFirstRun: true))
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
        bool isFirstRun)
    {
        foreach (var node in contentNodes)
        {
            switch (node)
            {
                case SvgAnchor svgAnchor:
                    hasAnchorContent = true;
                    if (!TryCollectSequentialTextRuns(GetContentNodes(svgAnchor), styleSource, runs, ref hasAnchorContent, isFirstRun && runs.Count == 0))
                    {
                        return false;
                    }

                    break;

                case SvgTextSpan svgTextSpan:
                    if (HasExplicitTextPositioning(svgTextSpan))
                    {
                        return false;
                    }

                    if (!TryCollectSequentialTextRuns(GetContentNodes(svgTextSpan), svgTextSpan, runs, ref hasAnchorContent, isFirstRun && runs.Count == 0))
                    {
                        return false;
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

                    var text = PrepareText(styleSource, node.Content, trimLeadingWhitespace: isFirstRun && runs.Count == 0);
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (styleSource.SpaceHandling != XmlSpaceHandling.Preserve &&
                            runs.Count > 0 &&
                            runs[runs.Count - 1].Text.EndsWith(" ", StringComparison.Ordinal) &&
                            text![0] == ' ')
                        {
                            text = text.TrimStart(' ');
                        }

                        if (string.IsNullOrEmpty(text))
                        {
                            break;
                        }

                        runs.Add(new SequentialTextRun(styleSource, text!));
                    }

                    break;
            }
        }

        return true;
    }

    private static bool HasExplicitTextPositioning(SvgTextBase svgTextBase)
    {
        return svgTextBase.X.Count > 0 ||
               svgTextBase.Y.Count > 0 ||
               svgTextBase.Dx.Count > 0 ||
               svgTextBase.Dy.Count > 0;
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
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, text, paint, assetLoader, out var svgFontLayout) && svgFontLayout is not null)
        {
            return svgFontLayout.Advance;
        }

        var fallbackText = GetBrowserCompatibleFallbackText(svgTextBase, text, assetLoader);
        var spans = assetLoader.FindTypefaces(fallbackText, paint);
        if (spans.Count > 0)
        {
            var totalAdvance = 0f;
            for (var i = 0; i < spans.Count; i++)
            {
                totalAdvance += spans[i].Advance;
            }

            return totalAdvance;
        }

        var bounds = new SKRect();
        return assetLoader.MeasureText(fallbackText, paint, ref bounds);
    }

    private static float ApplyTextAnchor(SvgTextBase svgTextBase, float anchorX, SKRect geometryBounds, float totalAdvance)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);

        return paint.TextAlign switch
        {
            SKTextAlign.Center => anchorX - (totalAdvance * 0.5f),
            SKTextAlign.Right => anchorX - totalAdvance,
            _ => anchorX
        };
    }

    private static void DrawTextStringAlignedLeft(
        SvgTextBase svgTextBase,
        string text,
        ref float x,
        ref float y,
        SKRect geometryBounds,
        DrawAttributes ignoreAttributes,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader)
    {
        var fillAdvance = 0f;
        if (SvgScenePaintingService.IsValidFill(svgTextBase))
        {
            var fillPaint = SvgScenePaintingService.GetFillPaint(svgTextBase, geometryBounds, assetLoader, ignoreAttributes);
            if (fillPaint is not null)
            {
                fillAdvance = DrawTextRunsAlignedLeft(svgTextBase, text, x, y, geometryBounds, fillPaint, canvas, assetLoader);
            }
        }

        var strokeAdvance = 0f;
        if (SvgScenePaintingService.IsValidStroke(svgTextBase, geometryBounds))
        {
            var strokePaint = SvgScenePaintingService.GetStrokePaint(svgTextBase, geometryBounds, assetLoader, ignoreAttributes);
            if (strokePaint is not null)
            {
                strokeAdvance = DrawTextRunsAlignedLeft(svgTextBase, text, x, y, geometryBounds, strokePaint, canvas, assetLoader);
            }
        }

        x += Math.Max(strokeAdvance, fillAdvance);
    }

    private static float DrawTextRunsAlignedLeft(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect geometryBounds,
        SKPaint paint,
        SKCanvas canvas,
        ISvgAssetLoader assetLoader)
    {
        PaintingService.SetPaintText(svgTextBase, geometryBounds, paint);
        paint.TextAlign = SKTextAlign.Left;

        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, text, paint, assetLoader, out var svgFontLayout) && svgFontLayout is not null)
        {
            svgFontLayout.Draw(canvas, paint, anchorX, anchorY);
            return svgFontLayout.Advance;
        }

        var fallbackText = GetBrowserCompatibleFallbackText(svgTextBase, text, assetLoader);
        var typefaceSpans = assetLoader.FindTypefaces(fallbackText, paint);
        if (typefaceSpans.Count == 0)
        {
            return 0f;
        }

        var currentX = anchorX;
        var totalAdvance = 0f;
        foreach (var typefaceSpan in typefaceSpans)
        {
            paint.Typeface = typefaceSpan.Typeface;
            canvas.DrawText(typefaceSpan.Text, currentX, anchorY, paint);
            currentX += typefaceSpan.Advance;
            totalAdvance += typefaceSpan.Advance;
            paint = paint.Clone();
        }

        return totalAdvance;
    }

    private static SKRect MeasureTextStringBoundsAlignedLeft(
        SvgTextBase svgTextBase,
        string text,
        float anchorX,
        float anchorY,
        SKRect viewport,
        ISvgAssetLoader assetLoader,
        out float advance)
    {
        var paint = new SKPaint();
        PaintingService.SetPaintText(svgTextBase, viewport, paint);
        paint.TextAlign = SKTextAlign.Left;

        if (SvgFontTextRenderer.TryGetLayout(svgTextBase, text, paint, assetLoader, out var svgFontLayout) && svgFontLayout is not null)
        {
            advance = svgFontLayout.Advance;
            return svgFontLayout.GetBounds(anchorX, anchorY);
        }

        advance = MeasureTextAdvance(svgTextBase, text, viewport, assetLoader);
        var metrics = assetLoader.GetFontMetrics(paint);
        return new SKRect(anchorX, anchorY + metrics.Ascent, anchorX + advance, anchorY + metrics.Descent);
    }

    private static string? PrepareText(SvgTextBase svgTextBase, string? value, bool trimLeadingWhitespace = true)
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

        return svgTextBase.SpaceHandling == XmlSpaceHandling.Preserve
            ? value
            : s_multipleSpaces.Replace(trimLeadingWhitespace ? value.TrimStart() : value, " ");
    }

    private static string GetBrowserCompatibleFallbackText(SvgTextBase svgTextBase, string text, ISvgAssetLoader assetLoader)
    {
        if (svgTextBase.FontVariant != SvgFontVariant.SmallCaps || string.IsNullOrEmpty(text))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        var charIndex = 0;
        while (TryReadNextCodepoint(text, ref charIndex, out var codepoint))
        {
            builder.Append(GetCodepointStableUpperInvariant(codepoint));
        }

        return builder.ToString();
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
            SvgTextTransformation.Capitalize => value.ToUpper(CultureInfo.CurrentCulture),
            SvgTextTransformation.Uppercase => value.ToUpper(CultureInfo.CurrentCulture),
            SvgTextTransformation.Lowercase => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value),
            _ => value
        };
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
}
