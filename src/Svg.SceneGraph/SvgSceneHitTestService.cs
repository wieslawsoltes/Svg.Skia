using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.Model.Services;

namespace Svg.Skia;

internal static class SvgSceneHitTestService
{
    public static IEnumerable<SvgSceneNode> HitTest(SvgSceneDocument sceneDocument, SKPoint point)
    {
        return HitTest(sceneDocument.Root, point);
    }

    public static IEnumerable<SvgSceneNode> HitTest(SvgSceneDocument sceneDocument, SKRect rect)
    {
        return HitTest(sceneDocument.Root, rect);
    }

    public static SvgSceneNode? HitTestTopmostNode(SvgSceneDocument sceneDocument, SKPoint point)
    {
        return HitTestTopmostNode(sceneDocument.Root, point);
    }

    public static bool HitTestPointer(SvgSceneNode node, SKPoint point)
    {
        if (node.SuppressSubtreeRendering)
        {
            return false;
        }

        if (!node.IsRenderable)
        {
            return false;
        }

        if (node.IsDisplayNone)
        {
            return false;
        }

        return node.PointerEvents switch
        {
            SvgPointerEvents.None => false,
            SvgPointerEvents.VisiblePainted => node.IsVisible && HitTestPainted(node, point),
            SvgPointerEvents.VisibleFill => node.IsVisible && HitTestFill(node, point),
            SvgPointerEvents.VisibleStroke => node.IsVisible && HitTestStroke(node, point),
            SvgPointerEvents.Visible => node.IsVisible && (HitTestFill(node, point) || HitTestStroke(node, point)),
            SvgPointerEvents.Painted => HitTestPainted(node, point),
            SvgPointerEvents.Fill => HitTestFill(node, point),
            SvgPointerEvents.Stroke => HitTestStroke(node, point),
            SvgPointerEvents.All => HitTestFill(node, point) || HitTestStroke(node, point),
            _ => node.IsVisible && HitTestPainted(node, point)
        };
    }

    private static IEnumerable<SvgSceneNode> HitTest(SvgSceneNode node, SKPoint point)
    {
        if (!CanTraverseSubtree(node, point))
        {
            yield break;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            foreach (var childHit in HitTest(node.Children[i], point))
            {
                yield return childHit;
            }
        }

        if (HitTestPointer(node, point))
        {
            yield return node;
        }
    }

    private static IEnumerable<SvgSceneNode> HitTest(SvgSceneNode node, SKRect rect)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            foreach (var childHit in HitTest(node.Children[i], rect))
            {
                yield return childHit;
            }
        }

        if (HitTestNode(node, rect))
        {
            yield return node;
        }
    }

    private static SvgSceneNode? HitTestTopmostNode(SvgSceneNode node, SKPoint point)
    {
        if (!CanTraverseSubtree(node, point))
        {
            return null;
        }

        for (var index = node.Children.Count - 1; index >= 0; index--)
        {
            var childHit = HitTestTopmostNode(node.Children[index], point);
            if (childHit is not null)
            {
                return childHit;
            }
        }

        return HitTestPointer(node, point)
            ? node
            : null;
    }

    private static bool CanTraverseSubtree(SvgSceneNode node, SKPoint point)
    {
        return !node.IsDisplayNone && !node.SuppressSubtreeRendering && CanHitTestPoint(node, point);
    }

    private static bool IntersectsWith(SKRect a, SKRect b)
    {
        return a.Left < b.Right && a.Right > b.Left &&
               a.Top < b.Bottom && a.Bottom > b.Top;
    }

    private static bool HitTestNode(SvgSceneNode node, SKPoint point)
    {
        if (node.SuppressSubtreeRendering)
        {
            return false;
        }

        if (!node.IsRenderable)
        {
            return false;
        }

        return UsesStructuralBounds(node)
            ? CanHitTestPoint(node, point) && GetStructuralBounds(node).Contains(point)
            : HitTestPainted(node, point);
    }

    private static bool HitTestNode(SvgSceneNode node, SKRect rect)
    {
        if (node.SuppressSubtreeRendering)
        {
            return false;
        }

        if (!node.IsRenderable)
        {
            return false;
        }

        return IntersectsWith(GetRectHitBounds(node), rect);
    }

    private static bool HitTestPainted(SvgSceneNode node, SKPoint point)
    {
        if (!CanHitTestPoint(node, point))
        {
            return false;
        }

        return (node.SupportsFillHitTest && HitTestFillCore(node, point)) ||
               (node.SupportsStrokeHitTest && HitTestStrokeCore(node, point));
    }

    private static bool HitTestFill(SvgSceneNode node, SKPoint point)
    {
        return CanHitTestPoint(node, point) && HitTestFillCore(node, point);
    }

    private static bool HitTestStroke(SvgSceneNode node, SKPoint point)
    {
        return CanHitTestPoint(node, point) && HitTestStrokeCore(node, point);
    }

    private static bool HitTestFillCore(SvgSceneNode node, SKPoint point)
    {
        if (node.HitTestPath is { } hitTestPath)
        {
            return GeometryHitTestService.ContainsFill(hitTestPath, point, node.TotalTransform);
        }

        return GetDirectFillBounds(node).Contains(point);
    }

    private static bool HitTestStrokeCore(SvgSceneNode node, SKPoint point)
    {
        if (node.HitTestPath is { } hitTestPath)
        {
            return GeometryHitTestService.ContainsStroke(hitTestPath, point, node.TotalTransform, node.StrokeWidth);
        }

        return node.StrokeWidth > 0f && GetDirectStrokeBounds(node).Contains(point);
    }

    private static bool CanHitTestPoint(SvgSceneNode node, SKPoint point)
    {
        if (!GetRectHitBounds(node).Contains(point))
        {
            return false;
        }

        if (node.Clip is null && node.ClipPath is null && node.InnerClip is null)
        {
            return !HasMask(node) || IsPointInMask(node.MaskNode, point);
        }

        if (!TryGetLocalPoint(node, point, out var localPoint))
        {
            return false;
        }

        if (node.Clip is { } clip && !clip.Contains(localPoint))
        {
            return false;
        }

        if (node.InnerClip is { } innerClip && !innerClip.Contains(localPoint))
        {
            return false;
        }

        if (node.ClipPath is { } clipPath && !GeometryHitTestService.Contains(clipPath, localPoint))
        {
            return false;
        }

        return !HasMask(node) || IsPointInMask(node.MaskNode, point);
    }

    private static bool TryGetLocalPoint(SvgSceneNode node, SKPoint point, out SKPoint localPoint)
    {
        if (node.TotalTransform.IsIdentity)
        {
            localPoint = point;
            return true;
        }

        if (!node.TotalTransform.TryInvert(out var inverse))
        {
            localPoint = default;
            return false;
        }

        localPoint = inverse.MapPoint(point);
        return true;
    }

    private static bool HasMask(SvgSceneNode node)
    {
        return node.MaskNode is { IsRenderable: true };
    }

    private static bool IsPointInMask(SvgSceneNode? maskNode, SKPoint point)
    {
        if (maskNode is null || !maskNode.IsRenderable)
        {
            return true;
        }

        return HasRenderedMaskCoverage(maskNode, point);
    }

    private static bool HasRenderedMaskCoverage(SvgSceneNode node, SKPoint point)
    {
        if (node.IsDisplayNone || !node.IsVisible)
        {
            return false;
        }

        if (!CanRenderAtPoint(node, point))
        {
            return false;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            if (HasRenderedMaskCoverage(node.Children[i], point))
            {
                return true;
            }
        }

        return node.IsRenderable &&
               ((node.SupportsFillHitTest && HitTestFillCore(node, point)) ||
                (node.SupportsStrokeHitTest && HitTestStrokeCore(node, point)));
    }

    private static bool CanRenderAtPoint(SvgSceneNode node, SKPoint point)
    {
        if (!GetStructuralBounds(node).Contains(point))
        {
            return false;
        }

        if (node.Clip is null && node.ClipPath is null && node.InnerClip is null)
        {
            return !HasMask(node) || IsPointInMask(node.MaskNode, point);
        }

        if (!TryGetLocalPoint(node, point, out var localPoint))
        {
            return false;
        }

        if (node.Clip is { } clip && !clip.Contains(localPoint))
        {
            return false;
        }

        if (node.InnerClip is { } innerClip && !innerClip.Contains(localPoint))
        {
            return false;
        }

        if (node.ClipPath is { } clipPath && !GeometryHitTestService.Contains(clipPath, localPoint))
        {
            return false;
        }

        return !HasMask(node) || IsPointInMask(node.MaskNode, point);
    }

    private static bool UsesStructuralBounds(SvgSceneNode node)
    {
        return node.HitTestPath is null &&
               !node.SupportsFillHitTest &&
               !node.SupportsStrokeHitTest;
    }

    private static SKRect GetRectHitBounds(SvgSceneNode node)
    {
        var bounds = UsesStructuralBounds(node)
            ? GetStructuralBounds(node)
            : GetDirectFillBounds(node);

        return SvgSceneNodeBoundsService.GetInflatedBounds(node, bounds);
    }

    private static SKRect GetStructuralBounds(SvgSceneNode node)
    {
        return SvgSceneNodeBoundsService.GetRenderableBounds(node);
    }

    private static SKRect GetDirectFillBounds(SvgSceneNode node)
    {
        return node.TransformedBounds;
    }

    private static SKRect GetDirectStrokeBounds(SvgSceneNode node)
    {
        return SvgSceneNodeBoundsService.GetInflatedBounds(node, node.TransformedBounds);
    }
}
