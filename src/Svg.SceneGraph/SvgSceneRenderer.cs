using System;
using ShimSkiaSharp;
using Svg;
using Svg.Model;
using Svg.Model.Services;

namespace Svg.Skia;

public static class SvgSceneRenderer
{
    private enum SvgScenePaintPhase
    {
        Fill,
        Stroke,
        Markers
    }

    private static IDisposable? PushDocumentFonts(SvgSceneDocument sceneDocument)
    {
        if (sceneDocument.SourceDocument is not null &&
            sceneDocument.AssetLoader is ISvgDocumentFontLoader fontLoader)
        {
            return fontLoader.PushDocumentFonts(sceneDocument.SourceDocument);
        }

        return null;
    }

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

        using var documentFontScope = PushDocumentFonts(sceneDocument);
        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        RenderNodeToCanvasCore(sceneDocument, sceneDocument.Root, canvas);
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

        using var documentFontScope = PushDocumentFonts(sceneDocument);
        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(cullRect);
        RenderNodeToCanvasCore(sceneDocument, node, canvas, ignoreAttributes, until, enableRootTransform, ignoreRootOpacity, ignoreRootMask, ignoreRootFilter);
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
        using var documentFontScope = PushDocumentFonts(sceneDocument);
        return RenderNodeToCanvasCore(
            sceneDocument,
            node,
            canvas,
            ignoreAttributes,
            until,
            enableTransform,
            ignoreCurrentOpacity,
            ignoreCurrentMask,
            ignoreCurrentFilter);
    }

    private static bool RenderNodeToCanvasCore(
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

        using var commandSource = canvas.PushCommandSource(
            node.ElementId,
            node.ElementAddressKey,
            node.ElementTypeName);

        var enableClip = !ignoreAttributes.HasFlag(DrawAttributes.ClipPath);
        var enableMask = !ignoreAttributes.HasFlag(DrawAttributes.Mask) && !ignoreCurrentMask;
        var enableOpacity = !ignoreAttributes.HasFlag(DrawAttributes.Opacity) && !ignoreCurrentOpacity;
        var enableFilter = !ignoreAttributes.HasFlag(DrawAttributes.Filter) && !ignoreCurrentFilter;
        var enableBlendMode = node.BlendModePaint is not null;
        var enableIsolation = node.IsIsolationGroup &&
            !enableBlendMode &&
            (node.MaskPaint is null || node.MaskNode is null || !enableMask) &&
            (node.Opacity is null || !enableOpacity) &&
            (node.Filter is null || !enableFilter);
        if (IsStateFreeNode(node, enableTransform, enableClip, enableMask, enableOpacity, enableFilter, enableBlendMode, enableIsolation))
        {
            return RenderNodeContentToCanvas(sceneDocument, node, canvas, ignoreAttributes, until);
        }

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

        if (node.ClipPath is { } clipPath && enableClip)
        {
            canvas.ClipPath(clipPath, SKClipOperation.Intersect, node.IsAntialias);
        }

        if (node.InnerClip is { } innerClip)
        {
            canvas.ClipRect(innerClip, SKClipOperation.Intersect);
        }

        if (enableIsolation)
        {
            canvas.SaveLayer(new SKPaint());
        }

        if (enableBlendMode)
        {
            canvas.SaveLayer(node.BlendModePaint!);
        }

        if (node.MaskPaint is { } maskPaint && node.MaskNode is not null && enableMask)
        {
            canvas.SaveLayer(maskPaint);
        }

        if (node.Opacity is { } opacity && enableOpacity)
        {
            canvas.SaveLayer(opacity);
        }

        var enableGlobalFilterLayer = false;
        if (node.Filter is { } filter && enableFilter)
        {
            enableGlobalFilterLayer = SaveFilterLayerToCanvas(node, canvas, filter);
        }

        if (!RenderNodeContentToCanvas(sceneDocument, node, canvas, ignoreAttributes, until))
        {
            RestoreNode(canvas, node, enableMask, enableOpacity, enableFilter, enableBlendMode, enableIsolation, enableGlobalFilterLayer);
            return false;
        }

        if (node.MaskNode is { } maskNode && node.MaskDstIn is { } maskDstIn && enableMask)
        {
            canvas.SaveLayer(maskDstIn);
            RenderNodeToCanvasCore(sceneDocument, maskNode, canvas, ignoreAttributes, until: null);
            canvas.Restore();
        }

        RestoreNode(canvas, node, enableMask, enableOpacity, enableFilter, enableBlendMode, enableIsolation, enableGlobalFilterLayer);
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

        using var documentFontScope = PushDocumentFonts(sceneDocument);
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

        using var commandSource = canvas.PushCommandSource(
            node.ElementId,
            node.ElementAddressKey,
            node.ElementTypeName);

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
        var enableBlendMode = node.BlendModePaint is not null && !isOnUntilPath;
        var enableIsolation = node.IsIsolationGroup &&
            !isOnUntilPath &&
            !enableBlendMode &&
            !enableMask &&
            !enableOpacity &&
            !enableFilter;

        if (enableIsolation)
        {
            canvas.SaveLayer(new SKPaint());
        }

        if (enableBlendMode)
        {
            canvas.SaveLayer(node.BlendModePaint!);
        }

        if (enableMask)
        {
            canvas.SaveLayer(node.MaskPaint!);
        }

        if (enableOpacity)
        {
            canvas.SaveLayer(node.Opacity!);
        }

        var enableGlobalFilterLayer = false;
        if (enableFilter)
        {
            enableGlobalFilterLayer = SaveFilterLayerToCanvas(node, canvas, node.Filter!);
        }

        if (!RenderBackgroundNodeContentToCanvas(sceneDocument, node, canvas, until))
        {
            RestoreNode(canvas, node, enableMask, enableOpacity, enableFilter, enableBlendMode, enableIsolation, enableGlobalFilterLayer);
            return false;
        }

        if (enableMask && node.MaskNode is { } maskNode && node.MaskDstIn is { } maskDstIn)
        {
            canvas.SaveLayer(maskDstIn);
            RenderNodeToCanvasCore(sceneDocument, maskNode, canvas, until: null);
            canvas.Restore();
        }

        RestoreNode(canvas, node, enableMask, enableOpacity, enableFilter, enableBlendMode, enableIsolation, enableGlobalFilterLayer);
        return true;
    }

    internal static void DrawNodeLocalVisuals(SvgSceneNode node, SKCanvas canvas)
    {
        if (node.LocalModel is { } localModel)
        {
            ApplySourceMetadata(localModel, node, overwrite: false);
            canvas.DrawPicture(localModel);
            return;
        }

        if (node.LocalPath is not { } localPath)
        {
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

    private static bool RenderNodeContentToCanvas(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKCanvas canvas,
        DrawAttributes ignoreAttributes,
        SvgSceneNode? until)
    {
        if (!ShouldApplyPaintOrder(node))
        {
            if (node.IsRenderable)
            {
                DrawNodeLocalVisuals(node, canvas);
            }

            return RenderChildrenToCanvas(sceneDocument, node, canvas, ignoreAttributes, until);
        }

        if (!RenderNodePaintOrderPhases(sceneDocument, node, canvas, ignoreAttributes, until))
        {
            return false;
        }

        return RenderNonMarkerChildrenToCanvas(sceneDocument, node, canvas, ignoreAttributes, until);
    }

    private static bool RenderBackgroundNodeContentToCanvas(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKCanvas canvas,
        SvgSceneNode until)
    {
        if (!ShouldApplyPaintOrder(node))
        {
            if (node.IsRenderable)
            {
                DrawNodeLocalVisuals(node, canvas);
            }

            return RenderBackgroundChildrenToCanvas(sceneDocument, node, canvas, until);
        }

        if (!RenderBackgroundNodePaintOrderPhases(sceneDocument, node, canvas, until))
        {
            return false;
        }

        return RenderBackgroundNonMarkerChildrenToCanvas(sceneDocument, node, canvas, until);
    }

    private static bool RenderNodePaintOrderPhases(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKCanvas canvas,
        DrawAttributes ignoreAttributes,
        SvgSceneNode? until)
    {
        return GetEffectivePaintOrder(node) switch
        {
            SvgPaintOrder.FillMarkersStroke =>
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Fill) &&
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Markers) &&
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Stroke),
            SvgPaintOrder.StrokeFillMarkers =>
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Stroke) &&
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Fill) &&
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Markers),
            SvgPaintOrder.StrokeMarkersFill =>
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Stroke) &&
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Markers) &&
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Fill),
            SvgPaintOrder.MarkersFillStroke =>
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Markers) &&
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Fill) &&
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Stroke),
            SvgPaintOrder.MarkersStrokeFill =>
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Markers) &&
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Stroke) &&
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Fill),
            _ =>
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Fill) &&
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Stroke) &&
                RenderNodePaintPhase(sceneDocument, node, canvas, ignoreAttributes, until, SvgScenePaintPhase.Markers)
        };
    }

    private static bool RenderBackgroundNodePaintOrderPhases(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKCanvas canvas,
        SvgSceneNode until)
    {
        return GetEffectivePaintOrder(node) switch
        {
            SvgPaintOrder.FillMarkersStroke =>
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Fill) &&
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Markers) &&
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Stroke),
            SvgPaintOrder.StrokeFillMarkers =>
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Stroke) &&
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Fill) &&
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Markers),
            SvgPaintOrder.StrokeMarkersFill =>
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Stroke) &&
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Markers) &&
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Fill),
            SvgPaintOrder.MarkersFillStroke =>
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Markers) &&
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Fill) &&
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Stroke),
            SvgPaintOrder.MarkersStrokeFill =>
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Markers) &&
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Stroke) &&
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Fill),
            _ =>
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Fill) &&
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Stroke) &&
                RenderBackgroundNodePaintPhase(sceneDocument, node, canvas, until, SvgScenePaintPhase.Markers)
        };
    }

    private static bool RenderNodePaintPhase(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKCanvas canvas,
        DrawAttributes ignoreAttributes,
        SvgSceneNode? until,
        SvgScenePaintPhase phase)
    {
        switch (phase)
        {
            case SvgScenePaintPhase.Fill:
                DrawNodeLocalFill(node, canvas);
                return true;
            case SvgScenePaintPhase.Stroke:
                DrawNodeLocalStroke(node, canvas);
                return true;
            case SvgScenePaintPhase.Markers:
                return RenderMarkerChildrenToCanvas(sceneDocument, node, canvas, ignoreAttributes, until);
            default:
                return true;
        }
    }

    private static bool RenderBackgroundNodePaintPhase(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKCanvas canvas,
        SvgSceneNode until,
        SvgScenePaintPhase phase)
    {
        switch (phase)
        {
            case SvgScenePaintPhase.Fill:
                DrawNodeLocalFill(node, canvas);
                return true;
            case SvgScenePaintPhase.Stroke:
                DrawNodeLocalStroke(node, canvas);
                return true;
            case SvgScenePaintPhase.Markers:
                return RenderBackgroundMarkerChildrenToCanvas(sceneDocument, node, canvas, until);
            default:
                return true;
        }
    }

    private static void DrawNodeLocalFill(SvgSceneNode node, SKCanvas canvas)
    {
        if (!node.IsRenderable ||
            node.LocalPath is not { } localPath ||
            node.LocalFill is not { } localFill)
        {
            return;
        }

        canvas.DrawPath(localPath, localFill);
    }

    private static void DrawNodeLocalStroke(SvgSceneNode node, SKCanvas canvas)
    {
        if (!node.IsRenderable ||
            node.LocalPath is not { } localPath ||
            node.LocalStroke is not { } localStroke)
        {
            return;
        }

        canvas.DrawPath(localPath, localStroke);
    }

    internal static void ApplySourceMetadata(SKPicture picture, SvgSceneNode node, bool overwrite)
    {
        if (picture.Commands is null)
        {
            return;
        }

        for (var i = 0; i < picture.Commands.Count; i++)
        {
            ApplySourceMetadata(picture.Commands[i], node, overwrite);
        }
    }

    private static void ApplySourceMetadata(CanvasCommand command, SvgSceneNode node, bool overwrite)
    {
        if (overwrite ||
            (command.SourceElementId is null &&
             command.SourceElementAddress is null &&
             command.SourceElementTypeName is null))
        {
            command.SourceElementId = node.ElementId;
            command.SourceElementAddress = node.ElementAddressKey;
            command.SourceElementTypeName = node.ElementTypeName;
        }

        if (command is DrawPictureCanvasCommand { Picture: { } nestedPicture })
        {
            ApplySourceMetadata(nestedPicture, node, overwrite);
        }
    }

    private static bool RenderChildrenToCanvas(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKCanvas canvas,
        DrawAttributes ignoreAttributes,
        SvgSceneNode? until)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            if (!RenderNodeToCanvasCore(sceneDocument, node.Children[i], canvas, ignoreAttributes, until))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RenderMarkerChildrenToCanvas(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKCanvas canvas,
        DrawAttributes ignoreAttributes,
        SvgSceneNode? until)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            if (child.Kind != SvgSceneNodeKind.Marker)
            {
                continue;
            }

            if (!RenderNodeToCanvasCore(sceneDocument, child, canvas, ignoreAttributes, until))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RenderNonMarkerChildrenToCanvas(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKCanvas canvas,
        DrawAttributes ignoreAttributes,
        SvgSceneNode? until)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            if (child.Kind == SvgSceneNodeKind.Marker)
            {
                continue;
            }

            if (!RenderNodeToCanvasCore(sceneDocument, child, canvas, ignoreAttributes, until))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RenderBackgroundChildrenToCanvas(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKCanvas canvas,
        SvgSceneNode until)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            if (!RenderBackgroundToCanvasCore(sceneDocument, node.Children[i], canvas, until, enableTransform: true))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RenderBackgroundMarkerChildrenToCanvas(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKCanvas canvas,
        SvgSceneNode until)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            if (child.Kind != SvgSceneNodeKind.Marker)
            {
                continue;
            }

            if (!RenderBackgroundToCanvasCore(sceneDocument, child, canvas, until, enableTransform: true))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RenderBackgroundNonMarkerChildrenToCanvas(
        SvgSceneDocument sceneDocument,
        SvgSceneNode node,
        SKCanvas canvas,
        SvgSceneNode until)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            if (child.Kind == SvgSceneNodeKind.Marker)
            {
                continue;
            }

            if (!RenderBackgroundToCanvasCore(sceneDocument, child, canvas, until, enableTransform: true))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ShouldApplyPaintOrder(SvgSceneNode node)
    {
        if (node.LocalModel is not null ||
            node.Element is not SvgVisualElement visualElement)
        {
            return false;
        }

        var paintOrder = visualElement.PaintOrder;
        return paintOrder != SvgPaintOrder.Normal &&
               paintOrder != SvgPaintOrder.FillStrokeMarkers &&
               (node.LocalPath is not null || HasMarkerChildren(node));
    }

    private static SvgPaintOrder GetEffectivePaintOrder(SvgSceneNode node)
    {
        return node.Element is SvgVisualElement visualElement
            ? visualElement.PaintOrder
            : SvgPaintOrder.Normal;
    }

    private static bool HasMarkerChildren(SvgSceneNode node)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            if (node.Children[i].Kind == SvgSceneNodeKind.Marker)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsStateFreeNode(
        SvgSceneNode node,
        bool enableTransform,
        bool enableClip,
        bool enableMask,
        bool enableOpacity,
        bool enableFilter,
        bool enableBlendMode,
        bool enableIsolation)
    {
        return node.Overflow is null &&
               (!enableTransform || node.Transform.IsIdentity) &&
               node.Clip is null &&
               (node.ClipPath is null || !enableClip) &&
               node.InnerClip is null &&
               (node.MaskNode is null || !enableMask) &&
               (node.Opacity is null || !enableOpacity) &&
               (node.Filter is null || !enableFilter) &&
               !enableBlendMode &&
               !enableIsolation;
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

    private static bool SaveFilterLayerToCanvas(SvgSceneNode node, SKCanvas canvas, SKPaint filter)
    {
        if (node.FilterUsesGlobalLayer &&
            node.TotalTransform.TryInvert(out var inverseTotalTransform))
        {
            canvas.Save();
            canvas.SetMatrix(inverseTotalTransform);

            if (node.FilterGlobalClip is { } globalClip)
            {
                canvas.ClipRect(globalClip, SKClipOperation.Intersect);
                canvas.SaveLayer(globalClip, filter);
            }
            else
            {
                canvas.SaveLayer(filter);
            }
            canvas.SetMatrix(node.TotalTransform);
            return true;
        }

        if (node.FilterClip is { } filterClip)
        {
            canvas.ClipRect(filterClip, SKClipOperation.Intersect);
            canvas.SaveLayer(filterClip, filter);
        }
        else
        {
            canvas.SaveLayer(filter);
        }
        return false;
    }

    private static void RestoreNode(
        SKCanvas canvas,
        SvgSceneNode node,
        bool enableMask,
        bool enableOpacity,
        bool enableFilter,
        bool enableBlendMode,
        bool enableIsolation,
        bool enableGlobalFilterLayer = false)
    {
        if (node.Filter is not null && enableFilter)
        {
            if (enableGlobalFilterLayer &&
                node.TotalTransform.TryInvert(out var inverseTotalTransform))
            {
                canvas.SetMatrix(inverseTotalTransform);
            }

            canvas.Restore();
            if (enableGlobalFilterLayer)
            {
                canvas.Restore();
            }
        }

        if (node.Opacity is not null && enableOpacity)
        {
            canvas.Restore();
        }

        if (node.MaskNode is not null && enableMask)
        {
            canvas.Restore();
        }

        if (enableBlendMode)
        {
            canvas.Restore();
        }

        if (enableIsolation)
        {
            canvas.Restore();
        }

        canvas.Restore();
    }
}
