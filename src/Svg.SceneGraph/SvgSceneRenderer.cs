using System;
using System.Collections.Generic;
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

        if (TryRenderTransformOnlySimpleChildrenToCanvas(
                node,
                canvas,
                until,
                enableTransform,
                enableClip,
                enableMask,
                enableOpacity,
                enableFilter,
                enableBlendMode,
                enableIsolation))
        {
            return true;
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

        var needsLocalLayerBounds =
            enableIsolation ||
            enableBlendMode ||
            (node.MaskPaint is { } && node.MaskNode is not null && enableMask) ||
            (node.Opacity is { } && enableOpacity);
        SKRect layerBounds = default;
        var hasLayerBounds = needsLocalLayerBounds && TryGetLocalLayerBounds(node, out layerBounds);

        if (enableIsolation)
        {
            SaveLayerToCanvas(canvas, hasLayerBounds ? layerBounds : null, new SKPaint());
        }

        if (enableBlendMode)
        {
            SaveLayerToCanvas(canvas, hasLayerBounds ? layerBounds : null, node.BlendModePaint!);
        }

        if (node.MaskPaint is { } maskPaint && node.MaskNode is not null && enableMask)
        {
            SaveLayerToCanvas(canvas, hasLayerBounds ? layerBounds : null, maskPaint);
        }

        if (node.Opacity is { } opacity && enableOpacity)
        {
            SaveLayerToCanvas(canvas, hasLayerBounds ? layerBounds : null, opacity);
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
            SaveLayerToCanvas(canvas, hasLayerBounds ? layerBounds : null, maskDstIn);
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

        var needsLocalLayerBounds =
            enableIsolation ||
            enableBlendMode ||
            enableMask ||
            enableOpacity;
        SKRect layerBounds = default;
        var hasLayerBounds = needsLocalLayerBounds && TryGetLocalLayerBounds(node, out layerBounds);

        if (enableIsolation)
        {
            SaveLayerToCanvas(canvas, hasLayerBounds ? layerBounds : null, new SKPaint());
        }

        if (enableBlendMode)
        {
            SaveLayerToCanvas(canvas, hasLayerBounds ? layerBounds : null, node.BlendModePaint!);
        }

        if (enableMask)
        {
            SaveLayerToCanvas(canvas, hasLayerBounds ? layerBounds : null, node.MaskPaint!);
        }

        if (enableOpacity)
        {
            SaveLayerToCanvas(canvas, hasLayerBounds ? layerBounds : null, node.Opacity!);
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
            SaveLayerToCanvas(canvas, hasLayerBounds ? layerBounds : null, maskDstIn);
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
            if (!node.LocalModelSourceMetadataApplied)
            {
                ApplySourceMetadata(localModel, node, overwrite: false);
                node.LocalModelSourceMetadataApplied = true;
            }

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

    private static bool TryRenderTransformOnlySimpleChildrenToCanvas(
        SvgSceneNode node,
        SKCanvas canvas,
        SvgSceneNode? until,
        bool enableTransform,
        bool enableClip,
        bool enableMask,
        bool enableOpacity,
        bool enableFilter,
        bool enableBlendMode,
        bool enableIsolation)
    {
        if (until is not null ||
            !enableTransform ||
            node.Transform.IsIdentity ||
            node.HasLocalVisuals ||
            node.Children.Count == 0 ||
            node.Overflow is not null ||
            node.Clip is not null ||
            (node.ClipPath is not null && enableClip) ||
            node.InnerClip is not null ||
            (node.MaskNode is not null && enableMask) ||
            (node.Opacity is not null && enableOpacity) ||
            (node.Filter is not null && enableFilter) ||
            enableBlendMode ||
            enableIsolation)
        {
            return false;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            if (!CanRenderTransformedSimpleChild(node.Children[i], enableClip, enableMask, enableOpacity, enableFilter))
            {
                return false;
            }
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            RenderTransformedSimpleChild(node.Children[i], node.Transform, canvas, enableOpacity);
        }

        return true;
    }

    private static bool CanRenderTransformedSimpleChild(
        SvgSceneNode child,
        bool enableClip,
        bool enableMask,
        bool enableOpacity,
        bool enableFilter)
    {
        if (child.IsDisplayNone || child.SuppressSubtreeRendering)
        {
            return true;
        }

        if (!child.IsRenderable ||
            child.LocalPath is null ||
            child.LocalFill is null ||
            child.LocalStroke is not null ||
            child.LocalModel is not null ||
            child.Children.Count != 0 ||
            child.Overflow is not null ||
            child.Clip is not null ||
            (child.ClipPath is not null && enableClip) ||
            child.InnerClip is not null ||
            (child.MaskNode is not null && enableMask) ||
            !CanInlineSimpleLeafOpacity(child, enableOpacity) ||
            (child.Filter is not null && enableFilter) ||
            child.BlendModePaint is not null ||
            child.IsIsolationGroup ||
            !IsSimpleSolidFill(child.LocalFill) ||
            !CanCreateTransformedSimpleFillPath(child.LocalPath))
        {
            return false;
        }

        return true;
    }

    private static bool IsSimpleSolidFill(SKPaint paint)
    {
        return paint.Style == SKPaintStyle.Fill &&
               !paint.IsStrokeNonScaling &&
               paint.Color is not null &&
               paint.Shader is null &&
               paint.ColorFilter is null &&
               paint.ImageFilter is null &&
               paint.PathEffect is null &&
               paint.BlendMode == SKBlendMode.SrcOver;
    }

    private static bool CanInlineSimpleLeafOpacity(SvgSceneNode child, bool enableOpacity)
    {
        return !enableOpacity ||
               child.Opacity is null ||
               child.OpacityValue is >= 0f and <= 1f;
    }

    private static void RenderTransformedSimpleChild(
        SvgSceneNode child,
        SKMatrix parentTransform,
        SKCanvas canvas,
        bool enableOpacity)
    {
        if (child.IsDisplayNone || child.SuppressSubtreeRendering)
        {
            return;
        }

        var transform = parentTransform.PreConcat(child.Transform);
        var path = CreateTransformedSimpleFillPath(child.LocalPath!, transform)!;
        var fill = GetSimpleLeafFill(child, enableOpacity);
        using var commandSource = canvas.PushCommandSource(
            child.ElementId,
            child.ElementAddressKey,
            child.ElementTypeName);
        canvas.DrawPath(path, fill);
    }

    private static SKPaint GetSimpleLeafFill(SvgSceneNode child, bool enableOpacity)
    {
        var fill = child.LocalFill!;
        if (!enableOpacity || child.Opacity is null || child.OpacityValue >= 1f)
        {
            return fill;
        }

        var color = fill.Color!.Value;
        var alpha = (byte)Math.Round(color.Alpha * child.OpacityValue);
        var adjusted = fill.Clone();
        adjusted.Color = new SKColor(color.Red, color.Green, color.Blue, alpha);
        return adjusted;
    }

    private static bool CanCreateTransformedSimpleFillPath(SKPath sourcePath)
    {
        return sourcePath.Commands is { Count: > 0 } commands &&
               CanCreateTransformedSimpleFillPath(commands);
    }

    private static bool CanCreateTransformedSimpleFillPath(IList<PathCommand> commands)
    {
        if (commands.Count == 1)
        {
            return commands[0] is AddRectPathCommand or AddPolyPathCommand { Points.Count: >= 3 };
        }

        return IsClosedPolyline(commands);
    }

    private static SKPath? CreateTransformedSimpleFillPath(SKPath sourcePath, SKMatrix transform)
    {
        return sourcePath.Commands is { Count: > 0 } commands &&
               TryCreateTransformedSimpleFillPath(commands, transform, sourcePath.FillType, out var transformedPath)
            ? transformedPath
            : null;
    }

    private static bool TryCreateTransformedSimpleFillPath(
        IList<PathCommand> commands,
        SKMatrix transform,
        SKPathFillType fillType,
        out SKPath? transformedPath)
    {
        transformedPath = null;
        if (commands.Count == 1)
        {
            switch (commands[0])
            {
                case AddRectPathCommand addRect:
                    transformedPath = CreatePolygonPath(
                        fillType,
                        transform.MapPoint(new SKPoint(addRect.Rect.Left, addRect.Rect.Top)),
                        transform.MapPoint(new SKPoint(addRect.Rect.Right, addRect.Rect.Top)),
                        transform.MapPoint(new SKPoint(addRect.Rect.Right, addRect.Rect.Bottom)),
                        transform.MapPoint(new SKPoint(addRect.Rect.Left, addRect.Rect.Bottom)));
                    return true;
                case AddPolyPathCommand { Points: { Count: >= 3 } points } addPoly:
                    transformedPath = CreateTransformedPolygonPath(fillType, points, addPoly.Close, transform);
                    return true;
            }

            return false;
        }

        if (!IsClosedPolyline(commands))
        {
            return false;
        }

        transformedPath = CreateTransformedClosedPolylinePath(fillType, commands, transform);
        return true;
    }

    private static bool IsClosedPolyline(IList<PathCommand> commands)
    {
        if (commands.Count < 4 ||
            commands[0] is not MoveToPathCommand ||
            commands[commands.Count - 1] is not ClosePathCommand)
        {
            return false;
        }

        for (var i = 1; i < commands.Count - 1; i++)
        {
            if (commands[i] is not LineToPathCommand)
            {
                return false;
            }
        }

        return true;
    }

    private static SKPath CreatePolygonPath(SKPathFillType fillType, params SKPoint[] points)
    {
        var path = new SKPath { FillType = fillType };
        path.AddPoly(points, close: true);
        return path;
    }

    private static SKPath CreateTransformedClosedPolylinePath(
        SKPathFillType fillType,
        IList<PathCommand> commands,
        SKMatrix transform)
    {
        var transformed = new SKPoint[commands.Count - 1];
        if (commands[0] is MoveToPathCommand moveTo)
        {
            transformed[0] = transform.MapPoint(new SKPoint(moveTo.X, moveTo.Y));
        }

        for (var i = 1; i < commands.Count - 1; i++)
        {
            if (commands[i] is LineToPathCommand lineTo)
            {
                transformed[i] = transform.MapPoint(new SKPoint(lineTo.X, lineTo.Y));
            }
        }

        var path = new SKPath { FillType = fillType };
        path.AddPoly(transformed, close: true);
        return path;
    }

    private static SKPath CreateTransformedPolygonPath(
        SKPathFillType fillType,
        IList<SKPoint> points,
        bool close,
        SKMatrix transform)
    {
        var transformed = new SKPoint[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            transformed[i] = transform.MapPoint(points[i]);
        }

        var path = new SKPath { FillType = fillType };
        path.AddPoly(transformed, close);
        return path;
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

    internal static bool TryGetLocalLayerBounds(SvgSceneNode node, out SKRect bounds)
    {
        bounds = SKRect.Empty;

        if (node.IsDisplayNone || node.SuppressSubtreeRendering)
        {
            return true;
        }

        if (node.Filter is not null)
        {
            if (!TryGetLocalFilterBounds(node, out bounds))
            {
                return false;
            }

            ApplyLocalClipBounds(node, ref bounds);
            return true;
        }

        if (node.IsRenderable)
        {
            if (!TryGetLocalVisualBounds(node, out var localBounds))
            {
                return false;
            }

            bounds = SvgSceneNodeBoundsService.UnionNonEmpty(bounds, localBounds);
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            if (!TryGetLocalLayerBounds(child, out var childBounds))
            {
                return false;
            }

            if (HasPositiveArea(childBounds))
            {
                bounds = SvgSceneNodeBoundsService.UnionNonEmpty(bounds, child.Transform.MapRect(childBounds));
            }
        }

        ApplyLocalClipBounds(node, ref bounds);
        return true;
    }

    private static bool TryGetLocalFilterBounds(SvgSceneNode node, out SKRect bounds)
    {
        if (node.FilterClip is { } filterClip)
        {
            bounds = filterClip;
            return true;
        }

        if (node.FilterUsesGlobalLayer &&
            node.FilterGlobalClip is { } globalClip &&
            node.TotalTransform.TryInvert(out var inverseTotalTransform))
        {
            bounds = inverseTotalTransform.MapRect(globalClip);
            return true;
        }

        bounds = SKRect.Empty;
        return false;
    }

    private static bool TryGetLocalVisualBounds(SvgSceneNode node, out SKRect bounds)
    {
        bounds = node.GeometryBounds;
        if (!HasPositiveArea(bounds) && node.LocalPath is { } localPath)
        {
            bounds = localPath.Bounds;
        }

        if (!HasPositiveArea(bounds) || node.StrokeWidth <= 0f)
        {
            return true;
        }

        var inflation = node.StrokeWidth * 0.5f;
        if (node.IsStrokeNonScaling)
        {
            if (!node.TotalTransform.TryInvert(out var inverseTotalTransform))
            {
                bounds = SKRect.Empty;
                return false;
            }

            var scaleX = Math.Sqrt((inverseTotalTransform.ScaleX * inverseTotalTransform.ScaleX) + (inverseTotalTransform.SkewY * inverseTotalTransform.SkewY));
            var scaleY = Math.Sqrt((inverseTotalTransform.SkewX * inverseTotalTransform.SkewX) + (inverseTotalTransform.ScaleY * inverseTotalTransform.ScaleY));
            inflation = (float)(Math.Max(scaleX, scaleY) * inflation);
        }

        bounds.Left -= inflation;
        bounds.Top -= inflation;
        bounds.Right += inflation;
        bounds.Bottom += inflation;
        return true;
    }

    private static void ApplyLocalClipBounds(SvgSceneNode node, ref SKRect bounds)
    {
        if (TryIntersect(bounds, node.Overflow, out var clippedBounds))
        {
            bounds = clippedBounds;
        }

        if (TryIntersect(bounds, node.Clip, out clippedBounds))
        {
            bounds = clippedBounds;
        }

        if (TryIntersect(bounds, node.InnerClip, out clippedBounds))
        {
            bounds = clippedBounds;
        }
    }

    private static bool TryIntersect(SKRect bounds, SKRect? clip, out SKRect result)
    {
        result = bounds;
        if (clip is not { } clipRect || !HasPositiveArea(bounds))
        {
            return false;
        }

        var left = Math.Max(bounds.Left, clipRect.Left);
        var top = Math.Max(bounds.Top, clipRect.Top);
        var right = Math.Min(bounds.Right, clipRect.Right);
        var bottom = Math.Min(bounds.Bottom, clipRect.Bottom);
        result = right > left && bottom > top
            ? new SKRect(left, top, right, bottom)
            : SKRect.Empty;
        return true;
    }

    private static bool HasPositiveArea(SKRect bounds)
    {
        return bounds.Width > 0f && bounds.Height > 0f;
    }

    internal static void SaveLayerToCanvas(SKCanvas canvas, SKRect? bounds, SKPaint paint)
    {
        if (bounds is { } layerBounds && HasPositiveArea(layerBounds))
        {
            canvas.SaveLayer(layerBounds, paint);
        }
        else
        {
            canvas.SaveLayer(paint);
        }
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
