using System;
using ShimSkiaSharp;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

public static class SvgSceneRenderer
{
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
        bool ignoreRootOpacity = false)
    {
        var cullRect = clip ?? SvgSceneNodeBoundsService.GetRenderableBounds(node);
        if (cullRect.IsEmpty)
        {
            return null;
        }

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        RenderNodeToCanvas(sceneDocument, node, canvas, ignoreAttributes, until, enableRootTransform, ignoreRootOpacity);
        return recorder.EndRecording();
    }

    internal static bool RenderNodeToCanvas(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKCanvas canvas,
        DrawAttributes ignoreAttributes = DrawAttributes.None,
        SvgSceneNode? until = null,
        bool enableTransform = true,
        bool ignoreCurrentOpacity = false)
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

        canvas.Save();

        var enableClip = !ignoreAttributes.HasFlag(DrawAttributes.ClipPath);
        if (node.Overflow is { } overflow)
        {
            canvas.ClipRect(overflow, SKClipOperation.Intersect);
        }

        if (enableTransform && !node.Transform.IsIdentity)
        {
            canvas.SetMatrix(node.Transform);
        }

        if (node.Clip is { } clip)
        {
            canvas.ClipRect(clip, SKClipOperation.Intersect);
        }

        if (node.ClipPath is { } clipPath && enableClip)
        {
            canvas.ClipPath(clipPath, SKClipOperation.Intersect, node.IsAntialias);
        }

        if (node.InnerClip is { } innerClip)
        {
            canvas.ClipRect(innerClip, SKClipOperation.Intersect);
        }

        var enableMask = !ignoreAttributes.HasFlag(DrawAttributes.Mask);
        var enableOpacity = !ignoreAttributes.HasFlag(DrawAttributes.Opacity) && !ignoreCurrentOpacity;
        var enableFilter = !ignoreAttributes.HasFlag(DrawAttributes.Filter);

        if (node.MaskPaint is { } maskPaint && node.MaskNode is not null && enableMask)
        {
            canvas.SaveLayer(maskPaint);
        }

        if (node.Opacity is { } opacity && enableOpacity)
        {
            canvas.SaveLayer(opacity);
        }

        if (node.Filter is { } filter && enableFilter)
        {
            if (node.FilterClip is { } filterClip)
            {
                canvas.ClipRect(filterClip, SKClipOperation.Intersect);
            }

            canvas.SaveLayer(filter);
        }

        if (node.IsRenderable && node.LocalModel is { } localModel)
        {
            canvas.DrawPicture(localModel);
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            if (!RenderNodeToCanvas(sceneDocument, node.Children[i], canvas, ignoreAttributes, until))
            {
                RestoreNode(canvas, node, enableMask, enableOpacity, enableFilter);
                return false;
            }
        }

        if (node.MaskNode is { } maskNode && node.MaskDstIn is { } maskDstIn && enableMask)
        {
            canvas.SaveLayer(maskDstIn);
            RenderNodeToCanvas(sceneDocument, maskNode, canvas, ignoreAttributes, until: null);
            canvas.Restore();
        }

        RestoreNode(canvas, node, enableMask, enableOpacity, enableFilter);
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

        canvas.Save();

        if (node.Overflow is { } overflow)
        {
            canvas.ClipRect(overflow, SKClipOperation.Intersect);
        }

        if (enableTransform && !node.Transform.IsIdentity)
        {
            canvas.SetMatrix(node.Transform);
        }

        if (node.Clip is { } clip)
        {
            canvas.ClipRect(clip, SKClipOperation.Intersect);
        }

        if (node.ClipPath is { } clipPath)
        {
            canvas.ClipPath(clipPath, SKClipOperation.Intersect, node.IsAntialias);
        }

        if (node.InnerClip is { } innerClip)
        {
            canvas.ClipRect(innerClip, SKClipOperation.Intersect);
        }

        var enableMask = node.MaskPaint is not null && node.MaskNode is not null && !isOnUntilPath;
        var enableOpacity = node.Opacity is not null && !isOnUntilPath;
        var enableFilter = node.Filter is not null && !isOnUntilPath;

        if (enableMask)
        {
            canvas.SaveLayer(node.MaskPaint!);
        }

        if (enableOpacity)
        {
            canvas.SaveLayer(node.Opacity!);
        }

        if (enableFilter)
        {
            if (node.FilterClip is { } filterClip)
            {
                canvas.ClipRect(filterClip, SKClipOperation.Intersect);
            }

            canvas.SaveLayer(node.Filter!);
        }

        if (node.IsRenderable && node.LocalModel is { } localModel)
        {
            canvas.DrawPicture(localModel);
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            if (!RenderBackgroundToCanvasCore(sceneDocument, node.Children[i], canvas, until, enableTransform: true))
            {
                RestoreNode(canvas, node, enableMask, enableOpacity, enableFilter);
                return false;
            }
        }

        if (enableMask && node.MaskNode is { } maskNode && node.MaskDstIn is { } maskDstIn)
        {
            canvas.SaveLayer(maskDstIn);
            RenderNodeToCanvas(sceneDocument, maskNode, canvas, until: null);
            canvas.Restore();
        }

        RestoreNode(canvas, node, enableMask, enableOpacity, enableFilter);
        return true;
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

    private static void RestoreNode(
        SKCanvas canvas,
        SvgSceneNode node,
        bool enableMask,
        bool enableOpacity,
        bool enableFilter)
    {
        if (node.Filter is not null && enableFilter)
        {
            canvas.Restore();
        }

        if (node.Opacity is not null && enableOpacity)
        {
            canvas.Restore();
        }

        if (node.MaskNode is not null && enableMask)
        {
            canvas.Restore();
        }

        canvas.Restore();
    }
}
