using System;
using ShimSkiaSharp;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

public static class SvgSceneRenderer
{
    private readonly record struct NodeCanvasState(
        bool HasBaseSave,
        bool HasMaskLayer,
        bool HasOpacityLayer,
        bool HasFilterLayer,
        bool HasStandaloneFilterOutput);

    public static SKPicture? Render(SvgSceneDocument? sceneDocument)
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

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        RenderNodeToCanvas(sceneDocument, sceneDocument.Root, canvas);
        return recorder.EndRecording();
    }

    internal static SKPicture? RenderNodePicture(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKRect? clip = null,
        DrawAttributes ignoreAttributes = DrawAttributes.None,
        SvgSceneNode? until = null,
        bool enableRootTransform = true,
        bool ignoreRootOpacity = false,
        bool ignoreRootMask = false,
        bool ignoreRootFilter = false)
    {
        var cullRect = clip ?? SvgSceneNodeBoundsService.GetRenderableBounds(node);
        if (cullRect.IsEmpty)
        {
            return null;
        }

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        RenderNodeToCanvas(sceneDocument, node, canvas, ignoreAttributes, until, enableRootTransform, ignoreRootOpacity, ignoreRootMask, ignoreRootFilter);
        return recorder.EndRecording();
    }

    internal static bool RenderNodeToCanvas(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKCanvas canvas,
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

        if (node.IsDisplayNone)
        {
            return true;
        }

        if (node.SuppressSubtreeRendering)
        {
            return true;
        }

        var enableClip = !ignoreAttributes.HasFlag(DrawAttributes.ClipPath);
        var enableMask = !ignoreAttributes.HasFlag(DrawAttributes.Mask) && !ignoreCurrentMask;
        var enableOpacity = !ignoreAttributes.HasFlag(DrawAttributes.Opacity) && !ignoreCurrentOpacity;
        var enableFilter = !ignoreAttributes.HasFlag(DrawAttributes.Filter) && !ignoreCurrentFilter;
        var state = ApplyNodeCanvasState(
            node,
            canvas,
            enableTransform,
            enableClip,
            enableMask,
            enableOpacity,
            enableFilter);

        if (state.HasStandaloneFilterOutput)
        {
            DrawStandaloneFilterOutput(node, canvas);
        }
        else if (node.IsRenderable)
        {
            DrawNodeLocalVisuals(node, canvas);
        }

        if (!state.HasStandaloneFilterOutput)
        {
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (!RenderNodeToCanvas(sceneDocument, node.Children[i], canvas, ignoreAttributes, until))
                {
                    RestoreNode(canvas, state);
                    return false;
                }
            }
        }

        if (state.HasMaskLayer && node.MaskNode is { } maskNode && node.MaskDstIn is { } maskDstIn)
        {
            if (ResolveCurrentLayerBounds(node) is { } maskLayerBounds)
            {
                canvas.SaveLayer(maskLayerBounds, maskDstIn);
            }
            else
            {
                canvas.SaveLayer(maskDstIn);
            }

            RenderNodeToCanvas(sceneDocument, maskNode, canvas, ignoreAttributes, until: null);
            canvas.Restore();
        }

        RestoreNode(canvas, state);
        return true;
    }

    internal static bool RenderBackgroundToCanvas(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKCanvas canvas,
        SvgSceneNode until,
        bool enableTransform = true)
    {
        if (until is null)
        {
            throw new ArgumentNullException(nameof(until));
        }

        return RenderBackgroundToCanvasCore(sceneDocument, node, canvas, until, enableTransform);
    }

    private static bool RenderBackgroundToCanvasCore(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKCanvas canvas,
        SvgSceneNode until,
        bool enableTransform)
    {
        if (ReferenceEquals(node, until))
        {
            return false;
        }

        if (node.IsDisplayNone)
        {
            return true;
        }

        if (node.SuppressSubtreeRendering)
        {
            return true;
        }

        var isOnUntilPath = IsSelfOrAncestor(node, until);
        var state = ApplyNodeCanvasState(
            node,
            canvas,
            enableTransform,
            enableClipPath: true,
            enableMask: !isOnUntilPath,
            enableOpacity: !isOnUntilPath,
            enableFilter: !isOnUntilPath);

        if (state.HasStandaloneFilterOutput)
        {
            DrawStandaloneFilterOutput(node, canvas);
        }
        else if (node.IsRenderable)
        {
            DrawNodeLocalVisuals(node, canvas);
        }

        if (!state.HasStandaloneFilterOutput)
        {
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (!RenderBackgroundToCanvasCore(sceneDocument, node.Children[i], canvas, until, enableTransform: true))
                {
                    RestoreNode(canvas, state);
                    return false;
                }
            }
        }

        if (state.HasMaskLayer && node.MaskNode is { } maskNode && node.MaskDstIn is { } maskDstIn)
        {
            if (ResolveCurrentLayerBounds(node) is { } maskLayerBounds)
            {
                canvas.SaveLayer(maskLayerBounds, maskDstIn);
            }
            else
            {
                canvas.SaveLayer(maskDstIn);
            }

            RenderNodeToCanvas(sceneDocument, maskNode, canvas, until: null);
            canvas.Restore();
        }

        RestoreNode(canvas, state);
        return true;
    }

    internal static void DrawNodeLocalVisuals(SvgSceneNode node, SKCanvas canvas)
    {
        if (node.LocalModel is { } localModel)
        {
            canvas.DrawPicture(localModel);
            return;
        }

        if (node.LocalPath is not { } localPath)
        {
            return;
        }

        if (SvgScenePaintingService.TryGetOpacityAdjustedLeafDirectPaints(node, out var adjustedFill, out var adjustedStroke))
        {
            if (adjustedFill is not null)
            {
                canvas.DrawPath(localPath, adjustedFill);
            }

            if (adjustedStroke is not null)
            {
                canvas.DrawPath(localPath, adjustedStroke);
            }

            return;
        }

        if (node.LocalFill is { } localFill)
        {
            canvas.DrawPath(localPath, localFill);
        }

        if (node.LocalStroke is { } localStroke)
        {
            canvas.DrawPath(localPath, localStroke);
        }
    }

    private static void DrawStandaloneFilterOutput(SvgSceneNode node, SKCanvas canvas)
    {
        if (node.StandaloneFilterModel is not { } standaloneFilterModel)
        {
            return;
        }

        canvas.DrawPicture(standaloneFilterModel);
    }

    internal static SKRect? ResolveFilterCarrierBounds(SvgSceneNode node)
    {
        if (node.FilterClip is { } filterClip && !filterClip.IsEmpty)
        {
            return filterClip;
        }

        var localBounds = SvgSceneNodeBoundsService.GetLocalRenderablePaintBounds(node);
        return localBounds.IsEmpty ? null : localBounds;
    }

    private static bool IsSelfOrAncestor(SvgSceneNode node, SvgSceneNode descendant)
    {
        for (var current = descendant; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, node))
            {
                return true;
            }
        }

        return false;
    }

    private static NodeCanvasState ApplyNodeCanvasState(
        SvgSceneNode node,
        SKCanvas canvas,
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
        var layerBounds = (hasMaskLayer || hasOpacityLayer)
            ? ResolveCurrentLayerBounds(node)
            : null;
        SKRect? filterLayerBounds = hasFilterLayer && node.FilterClip is { } filterClipCandidate && !filterClipCandidate.IsEmpty
            ? SvgSceneNodeBoundsService.GetPixelAlignedBounds(filterClipCandidate)
            : null;
        var hasBaseSave = node.Overflow is not null ||
            hasTransform ||
            node.Clip is not null ||
            hasClipPath ||
            node.InnerClip is not null ||
            ((hasFilterLayer || hasStandaloneFilterOutput) && node.FilterClip is not null);

        if (hasBaseSave)
        {
            canvas.Save();
        }

        if (node.Overflow is { } overflow)
        {
            canvas.ClipRect(overflow, SKClipOperation.Intersect);
        }

        if (hasTransform)
        {
            canvas.SetMatrix(node.Transform);
        }

        if (node.Clip is { } clip)
        {
            canvas.ClipRect(clip, SKClipOperation.Intersect);
        }

        if (hasClipPath)
        {
            canvas.ClipPath(node.ClipPath!, SKClipOperation.Intersect, node.IsAntialias);
        }

        if (node.InnerClip is { } innerClip)
        {
            canvas.ClipRect(innerClip, SKClipOperation.Intersect);
        }

        if ((hasFilterLayer || hasStandaloneFilterOutput) && node.FilterClip is { } filterClip)
        {
            canvas.ClipRect(filterClip, SKClipOperation.Intersect);
        }

        if (hasMaskLayer)
        {
            if (layerBounds is { } maskLayerBounds)
            {
                canvas.SaveLayer(maskLayerBounds, node.MaskPaint!);
            }
            else
            {
                canvas.SaveLayer(node.MaskPaint!);
            }
        }

        if (hasOpacityLayer)
        {
            if (layerBounds is { } opacityLayerBounds)
            {
                canvas.SaveLayer(opacityLayerBounds, node.Opacity!);
            }
            else
            {
                canvas.SaveLayer(node.Opacity!);
            }
        }

        if (hasFilterLayer)
        {
            if (filterLayerBounds is { } boundedFilterLayer)
            {
                canvas.SaveLayer(boundedFilterLayer, node.Filter!);
            }
            else
            {
                canvas.SaveLayer(node.Filter!);
            }
        }

        return new NodeCanvasState(hasBaseSave, hasMaskLayer, hasOpacityLayer, hasFilterLayer, hasStandaloneFilterOutput);
    }

    private static void RestoreNode(
        SKCanvas canvas,
        NodeCanvasState state)
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

    private static SKRect? ResolveCurrentLayerBounds(SvgSceneNode node)
    {
        var localBounds = SvgSceneNodeBoundsService.GetLocalRenderablePaintBounds(node);
        if (localBounds.IsEmpty)
        {
            return null;
        }

        return SvgSceneNodeBoundsService.GetPixelAlignedBounds(localBounds);
    }
}
