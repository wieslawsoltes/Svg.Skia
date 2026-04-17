using System;
using Svg.Model;
using Svg.Model.Services;
using NativeCanvas = SkiaSharp.SKCanvas;
using NativeClipOperation = SkiaSharp.SKClipOperation;
using NativePicture = SkiaSharp.SKPicture;
using NativePictureRecorder = SkiaSharp.SKPictureRecorder;
using NativeRect = SkiaSharp.SKRect;

namespace Svg.Skia;

public partial class SkiaModel
{
    private readonly struct NativeNodeCanvasState
    {
        public NativeNodeCanvasState(
            bool hasBaseSave,
            bool hasMaskLayer,
            bool hasOpacityLayer,
            bool hasFilterLayer,
            bool hasStandaloneFilterOutput)
        {
            HasBaseSave = hasBaseSave;
            HasMaskLayer = hasMaskLayer;
            HasOpacityLayer = hasOpacityLayer;
            HasFilterLayer = hasFilterLayer;
            HasStandaloneFilterOutput = hasStandaloneFilterOutput;
        }

        public bool HasBaseSave { get; }
        public bool HasMaskLayer { get; }
        public bool HasOpacityLayer { get; }
        public bool HasFilterLayer { get; }
        public bool HasStandaloneFilterOutput { get; }
    }

    internal NativePicture? ToSKPicture(SvgSceneDocument? sceneDocument)
    {
        if (sceneDocument is null)
        {
            return null;
        }

        var cullRect = sceneDocument.CullRect;
        if (cullRect.IsEmpty)
        {
            cullRect = sceneDocument.Root.TransformedBounds;
        }

        if (cullRect.IsEmpty)
        {
            return null;
        }

        using var recorder = new NativePictureRecorder();
        using var canvas = recorder.BeginRecording(ToSKRect(cullRect));
        RenderSceneNodeToNativeCanvas(sceneDocument, sceneDocument.Root, canvas);
        return recorder.EndRecording();
    }

    internal NativePicture? ToSKPicture(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        ShimSkiaSharp.SKRect? clip = null)
    {
        if (sceneDocument is null)
        {
            throw new ArgumentNullException(nameof(sceneDocument));
        }

        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var cullRect = clip ?? GetRenderableBounds(node);
        if (cullRect.IsEmpty)
        {
            return null;
        }

        using var recorder = new NativePictureRecorder();
        using var canvas = recorder.BeginRecording(ToSKRect(cullRect));
        RenderSceneNodeToNativeCanvas(sceneDocument, node, canvas);
        return recorder.EndRecording();
    }

    private bool RenderSceneNodeToNativeCanvas(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        NativeCanvas canvas,
        DrawAttributes ignoreAttributes = DrawAttributes.None,
        SvgSceneNode? until = null,
        bool enableTransform = true,
        bool ignoreCurrentOpacity = false,
        bool ignoreCurrentMask = false,
        bool ignoreCurrentFilter = false)
    {
        if (until is not null && ReferenceEquals(node, until))
        {
            return false;
        }

        if (node.IsDisplayNone || node.SuppressSubtreeRendering)
        {
            return true;
        }

        var enableClip = !ignoreAttributes.HasFlag(DrawAttributes.ClipPath);
        var enableMask = !ignoreAttributes.HasFlag(DrawAttributes.Mask) && !ignoreCurrentMask;
        var enableOpacity = !ignoreAttributes.HasFlag(DrawAttributes.Opacity) && !ignoreCurrentOpacity;
        var enableFilter = !ignoreAttributes.HasFlag(DrawAttributes.Filter) && !ignoreCurrentFilter;
        var state = ApplyNativeNodeCanvasState(
            node,
            canvas,
            enableTransform,
            enableClip,
            enableMask,
            enableOpacity,
            enableFilter);

        if (state.HasStandaloneFilterOutput)
        {
            DrawStandaloneFilterOutputToNativeCanvas(node, canvas);
        }
        else if (node.IsRenderable)
        {
            DrawNodeLocalVisualsToNativeCanvas(node, canvas);
        }

        if (!state.HasStandaloneFilterOutput)
        {
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (!RenderSceneNodeToNativeCanvas(sceneDocument, node.Children[i], canvas, ignoreAttributes, until))
                {
                    RestoreNativeNode(canvas, state);
                    return false;
                }
            }
        }

        if (state.HasMaskLayer && node.MaskNode is { } maskNode && node.MaskDstIn is { } maskDstIn)
        {
            SaveLayer(canvas, maskDstIn, ResolveCurrentLayerBounds(node));
            RenderSceneNodeToNativeCanvas(sceneDocument, maskNode, canvas, ignoreAttributes, until: null);
            canvas.Restore();
        }

        RestoreNativeNode(canvas, state);
        return true;
    }

    private NativeNodeCanvasState ApplyNativeNodeCanvasState(
        SvgSceneNode node,
        NativeCanvas canvas,
        bool enableTransform,
        bool enableClipPath,
        bool enableMask,
        bool enableOpacity,
        bool enableFilter)
    {
        var hasTransform = enableTransform && !node.Transform.IsIdentity;
        var hasClipPath = enableClipPath && node.ClipPath is not null;
        var hasMaskLayer = enableMask && node.MaskPaint is not null && node.MaskNode is not null;
        var canFoldOpacity = enableOpacity && SvgScenePaintingService.CanFoldOpacityIntoLeafDirectDraw(node);
        var hasOpacityLayer = enableOpacity && node.Opacity is not null && !canFoldOpacity;
        var hasStandaloneFilterOutput = enableFilter && node.StandaloneFilterModel is not null;
        var hasFilterLayer = enableFilter && node.Filter is not null && !hasStandaloneFilterOutput;
        var needsStandaloneFilterClip = hasStandaloneFilterOutput && !node.CanSkipStandaloneFilterClip;
        var layerBounds = (hasMaskLayer || hasOpacityLayer)
            ? ResolveCurrentLayerBounds(node)
            : null;
        ShimSkiaSharp.SKRect? filterLayerBounds = hasFilterLayer && node.FilterClip is { } filterClipCandidate && !filterClipCandidate.IsEmpty
            ? filterClipCandidate
            : null;
        var hasBaseSave = node.Overflow is not null ||
            hasTransform ||
            node.Clip is not null ||
            hasClipPath ||
            node.InnerClip is not null ||
            ((hasFilterLayer || needsStandaloneFilterClip) && node.FilterClip is not null);

        if (hasBaseSave)
        {
            canvas.Save();
        }

        if (node.Overflow is { } overflow)
        {
            canvas.ClipRect(ToSKRect(overflow), NativeClipOperation.Intersect);
        }

        if (hasTransform)
        {
            var matrix = ToSKMatrix(node.Transform);
            canvas.Concat(ref matrix);
        }

        if (node.Clip is { } clip)
        {
            canvas.ClipRect(ToSKRect(clip), NativeClipOperation.Intersect);
        }

        if (hasClipPath && node.ClipPath is { } clipPath)
        {
            using var nativeClipPath = ToSKPath(clipPath);
            if (nativeClipPath is not null)
            {
                canvas.ClipPath(nativeClipPath, NativeClipOperation.Intersect, node.IsAntialias);
            }
        }

        if (node.InnerClip is { } innerClip)
        {
            canvas.ClipRect(ToSKRect(innerClip), NativeClipOperation.Intersect);
        }

        if ((hasFilterLayer || needsStandaloneFilterClip) && node.FilterClip is { } filterClip)
        {
            canvas.ClipRect(ToSKRect(filterClip), NativeClipOperation.Intersect);
        }

        if (hasMaskLayer)
        {
            SaveLayer(canvas, node.MaskPaint, layerBounds);
        }

        if (hasOpacityLayer)
        {
            SaveLayer(canvas, node.Opacity, layerBounds);
        }

        if (hasFilterLayer)
        {
            SaveLayer(canvas, node.Filter, filterLayerBounds);
        }

        return new NativeNodeCanvasState(hasBaseSave, hasMaskLayer, hasOpacityLayer, hasFilterLayer, hasStandaloneFilterOutput);
    }

    private void DrawNodeLocalVisualsToNativeCanvas(SvgSceneNode node, NativeCanvas canvas)
    {
        if (node.LocalModel is { } localModel)
        {
            if (TryGetCachedPicture(localModel, out var cachedPicture))
            {
                canvas.DrawPicture(cachedPicture);
            }
            else
            {
                Draw(localModel, canvas);
            }

            return;
        }

        if (node.LocalPath is not { } localPath)
        {
            return;
        }

        var nativePath = GetRenderPath(localPath);
        if (nativePath is null)
        {
            return;
        }

        if (SvgScenePaintingService.TryGetOpacityAdjustedLeafDirectPaints(node, out var adjustedFill, out var adjustedStroke))
        {
            if (adjustedFill is not null)
            {
                var adjustedFillPaint = GetRenderPaint(adjustedFill);
                if (adjustedFillPaint is not null)
                {
                    canvas.DrawPath(nativePath, adjustedFillPaint);
                }
            }

            if (adjustedStroke is not null)
            {
                var adjustedStrokePaint = GetRenderPaint(adjustedStroke);
                if (adjustedStrokePaint is not null)
                {
                    canvas.DrawPath(nativePath, adjustedStrokePaint);
                }
            }

            return;
        }

        if (node.LocalFill is { } localFill)
        {
            var fillPaint = GetRenderPaint(localFill);
            if (fillPaint is not null)
            {
                canvas.DrawPath(nativePath, fillPaint);
            }
        }

        if (node.LocalStroke is { } localStroke)
        {
            var strokePaint = GetRenderPaint(localStroke);
            if (strokePaint is not null)
            {
                canvas.DrawPath(nativePath, strokePaint);
            }
        }
    }

    private void DrawStandaloneFilterOutputToNativeCanvas(SvgSceneNode node, NativeCanvas canvas)
    {
        if (node.RequiresFilterInputCarrier &&
            node.Filter is { } filterPaint &&
            node.FilterClip is { } filterClip &&
            !filterClip.IsEmpty)
        {
            using var nativeFilterPaint = ToSKPaint(filterPaint);
            if (nativeFilterPaint is not null)
            {
                canvas.DrawRect(ToSKRect(filterClip), nativeFilterPaint);
                return;
            }

            return;
        }

        if (node.StandaloneFilterModel is not { } standaloneFilterModel)
        {
            return;
        }

        if (TryGetCachedPicture(standaloneFilterModel, out var cachedPicture))
        {
            canvas.DrawPicture(cachedPicture);
        }
        else
        {
            Draw(standaloneFilterModel, canvas);
        }
    }

    private void RestoreNativeNode(NativeCanvas canvas, NativeNodeCanvasState state)
    {
        if (state.HasFilterLayer)
        {
            canvas.Restore();
        }

        if (state.HasOpacityLayer)
        {
            canvas.Restore();
        }

        if (state.HasMaskLayer)
        {
            canvas.Restore();
        }

        if (state.HasBaseSave)
        {
            canvas.Restore();
        }
    }

    private void SaveLayer(NativeCanvas canvas, ShimSkiaSharp.SKPaint? paint, ShimSkiaSharp.SKRect? bounds = null)
    {
        var nativePaint = GetRenderPaint(paint);
        if (bounds is { } layerBounds)
        {
            canvas.SaveLayer(ToSKRect(layerBounds), nativePaint);
        }
        else if (nativePaint is not null)
        {
            canvas.SaveLayer(nativePaint);
        }
        else
        {
            canvas.SaveLayer();
        }
    }

    private static ShimSkiaSharp.SKRect? ResolveCurrentLayerBounds(SvgSceneNode node)
    {
        var localBounds = GetLocalRenderablePaintBounds(node);
        if (localBounds.IsEmpty)
        {
            return null;
        }

        return GetPixelAlignedBounds(localBounds);
    }

    private static ShimSkiaSharp.SKRect GetRenderableBounds(SvgSceneNode? node)
    {
        if (node is null)
        {
            return ShimSkiaSharp.SKRect.Empty;
        }

        var bounds = node.IsRenderable ? node.TransformedBounds : ShimSkiaSharp.SKRect.Empty;
        if (node.StandaloneFilterModel is { } standaloneFilterModel && !standaloneFilterModel.CullRect.IsEmpty)
        {
            bounds = UnionNonEmpty(bounds, node.TotalTransform.MapRect(standaloneFilterModel.CullRect));
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            bounds = UnionNonEmpty(bounds, GetRenderableBounds(node.Children[i]));
        }

        return bounds;
    }

    private static ShimSkiaSharp.SKRect GetRenderablePaintBounds(SvgSceneNode? node)
    {
        return SvgSceneNodeBoundsService.GetRenderablePaintBounds(node);
    }

    private static ShimSkiaSharp.SKRect GetLocalRenderablePaintBounds(SvgSceneNode? node)
    {
        return SvgSceneNodeBoundsService.GetLocalRenderablePaintBounds(node);
    }

    private static ShimSkiaSharp.SKRect GetPixelAlignedBounds(ShimSkiaSharp.SKRect bounds)
    {
        if (bounds.IsEmpty)
        {
            return bounds;
        }

        var left = (float)System.Math.Floor(bounds.Left);
        var top = (float)System.Math.Floor(bounds.Top);
        var right = (float)System.Math.Ceiling(bounds.Right);
        var bottom = (float)System.Math.Ceiling(bounds.Bottom);

        if (right <= left || bottom <= top)
        {
            return bounds;
        }

        return ShimSkiaSharp.SKRect.Create(left, top, right - left, bottom - top);
    }

    private static ShimSkiaSharp.SKRect UnionNonEmpty(ShimSkiaSharp.SKRect current, ShimSkiaSharp.SKRect next)
    {
        if (current.IsEmpty)
        {
            return next;
        }

        if (next.IsEmpty)
        {
            return current;
        }

        var left = Math.Min(current.Left, next.Left);
        var top = Math.Min(current.Top, next.Top);
        var right = Math.Max(current.Right, next.Right);
        var bottom = Math.Max(current.Bottom, next.Bottom);
        return new ShimSkiaSharp.SKRect(left, top, right, bottom);
    }
}
