using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.DataTypes;
using Svg.Model.Services;

namespace Svg.Skia;

internal static class SvgSceneHitTestService
{
    public static IEnumerable<SvgSceneNode> HitTest(SvgSceneDocument sceneDocument, SKPoint point)
    {
        return HitTest(sceneDocument.Root, point, new HitTestContext());
    }

    public static IEnumerable<SvgSceneNode> HitTest(SvgSceneDocument sceneDocument, SKRect rect)
    {
        return HitTest(sceneDocument.Root, rect);
    }

    public static SvgSceneNode? HitTestTopmostNode(SvgSceneDocument sceneDocument, SKPoint point)
    {
        return HitTestTopmostNode(sceneDocument.Root, point, new HitTestContext());
    }

    public static bool HitTestPointer(SvgSceneNode node, SKPoint point)
    {
        if (node.SuppressSubtreeRendering)
        {
            return false;
        }

        if (!CanHitTestNode(node))
        {
            return false;
        }

        if (node.IsDisplayNone)
        {
            return false;
        }

        if (node.Kind == SvgSceneNodeKind.Text)
        {
            return HitTestTextPointer(node, point);
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

    private static bool HitTestTextPointer(SvgSceneNode node, SKPoint point)
    {
        return node.PointerEvents switch
        {
            SvgPointerEvents.None => false,
            SvgPointerEvents.VisiblePainted => node.IsVisible && IsTextPainted(node) && HitTestTextCell(node, point),
            SvgPointerEvents.VisibleFill => node.IsVisible && HitTestTextCell(node, point),
            SvgPointerEvents.VisibleStroke => node.IsVisible && HitTestTextCell(node, point),
            SvgPointerEvents.Visible => node.IsVisible && HitTestTextCell(node, point),
            SvgPointerEvents.Painted => IsTextPainted(node) && HitTestTextCell(node, point),
            SvgPointerEvents.Fill => HitTestTextCell(node, point),
            SvgPointerEvents.Stroke => HitTestTextCell(node, point),
            SvgPointerEvents.All => HitTestTextCell(node, point),
            _ => node.IsVisible && IsTextPainted(node) && HitTestTextCell(node, point)
        };
    }

    private static bool IsTextPainted(SvgSceneNode node)
    {
        return node.SupportsFillHitTest || node.SupportsStrokeHitTest;
    }

    private static IEnumerable<SvgSceneNode> HitTest(SvgSceneNode node, SKPoint point, HitTestContext context)
    {
        if (!CanTraverseSubtree(node, point, context))
        {
            yield break;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            foreach (var childHit in HitTest(node.Children[i], point, context))
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

    private static SvgSceneNode? HitTestTopmostNode(SvgSceneNode node, SKPoint point, HitTestContext context)
    {
        if (!CanTraverseSubtree(node, point, context))
        {
            return null;
        }

        for (var index = node.Children.Count - 1; index >= 0; index--)
        {
            var childHit = HitTestTopmostNode(node.Children[index], point, context);
            if (childHit is not null)
            {
                return childHit;
            }
        }

        return HitTestPointer(node, point)
            ? node
            : null;
    }

    private static bool CanTraverseSubtree(SvgSceneNode node, SKPoint point, HitTestContext context)
    {
        return !node.IsDisplayNone &&
               !node.SuppressSubtreeRendering &&
               (CanHitTestPoint(node, point) || CanTraverseHitTestPoint(node, point, context));
    }

    private static bool CanTraverseHitTestPoint(SvgSceneNode node, SKPoint point, HitTestContext context)
    {
        if (!context.GetSubtreeHitTestBounds(node).Contains(point))
        {
            return false;
        }

        if (node.Clip is null && node.ClipPath is null && node.InnerClip is null)
        {
            return true;
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

        return true;
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

        if (!CanHitTestNode(node))
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

        if (!CanHitTestNode(node))
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

    private static bool HitTestTextCell(SvgSceneNode node, SKPoint point)
    {
        if (node.GetTextContentMetrics() is { } metrics)
        {
            if (!GetTextCellBounds(node).Contains(point) ||
                !TryGetLocalPoint(node, point, out var metricPoint) ||
                !metrics.HitTestCharacterCell(metricPoint))
            {
                return false;
            }

            return PassesLocalClips(node, metricPoint);
        }

        if (!GetTextCellBounds(node).Contains(point))
        {
            return false;
        }

        if (node.Clip is null && node.ClipPath is null && node.InnerClip is null)
        {
            return true;
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

        return true;
    }

    private static bool PassesLocalClips(SvgSceneNode node, SKPoint localPoint)
    {
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

        return true;
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
        var strokeWidth = GetStrokeHitTestWidth(node);
        if (node.HitTestPath is { } hitTestPath)
        {
            return GeometryHitTestService.ContainsStroke(
                hitTestPath,
                point,
                node.TotalTransform,
                strokeWidth,
                node.IsStrokeNonScaling);
        }

        return strokeWidth > 0f && GetDirectStrokeBounds(node).Contains(point);
    }

    private static bool CanHitTestPoint(SvgSceneNode node, SKPoint point)
    {
        if (!GetRectHitBounds(node).Contains(point))
        {
            return false;
        }

        if (node.Clip is null && node.ClipPath is null && node.InnerClip is null)
        {
            return true;
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

        return true;
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

    private static bool CanHitTestNode(SvgSceneNode node)
    {
        if (node.Kind == SvgSceneNodeKind.Text)
        {
            return HasHitTestGeometry(node) && node.PointerEvents != SvgPointerEvents.None;
        }

        return node.IsRenderable ||
               (HasHitTestGeometry(node) &&
                (!node.IsVisible || UsesGeometryWithoutPaint(node.PointerEvents)));
    }

    private static bool HasHitTestGeometry(SvgSceneNode node)
    {
        return node.HitTestPath is not null ||
               !node.TransformedBounds.IsEmpty;
    }

    private static bool UsesGeometryWithoutPaint(SvgPointerEvents pointerEvents)
    {
        return pointerEvents is
            SvgPointerEvents.VisibleFill or
            SvgPointerEvents.VisibleStroke or
            SvgPointerEvents.Visible or
            SvgPointerEvents.Fill or
            SvgPointerEvents.Stroke or
            SvgPointerEvents.All;
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
            ? SvgSceneNodeBoundsService.GetRenderablePaintBounds(node)
            : GetDirectFillBounds(node);

        return GetInflatedHitBounds(node, bounds);
    }

    private static SKRect GetStructuralBounds(SvgSceneNode node)
    {
        return SvgSceneNodeBoundsService.GetRenderableBounds(node);
    }

    private static SKRect GetTextCellBounds(SvgSceneNode node)
    {
        if (!node.TransformedBounds.IsEmpty)
        {
            return node.TransformedBounds;
        }

        return SvgSceneNodeBoundsService.GetRenderableBounds(node);
    }

    private static SKRect GetDirectFillBounds(SvgSceneNode node)
    {
        return node.TransformedBounds;
    }

    private static SKRect GetDirectStrokeBounds(SvgSceneNode node)
    {
        return GetInflatedHitBounds(node, node.TransformedBounds);
    }

    private static SKRect GetInflatedHitBounds(SvgSceneNode node, SKRect bounds)
    {
        var strokeWidth = GetStrokeHitTestWidth(node);
        if (bounds.IsEmpty || strokeWidth <= 0f)
        {
            return bounds;
        }

        var inflation = strokeWidth / 2f;
        if (!node.IsStrokeNonScaling)
        {
            var scaleX = Math.Sqrt(
                (node.TotalTransform.ScaleX * node.TotalTransform.ScaleX) +
                (node.TotalTransform.SkewY * node.TotalTransform.SkewY));
            var scaleY = Math.Sqrt(
                (node.TotalTransform.SkewX * node.TotalTransform.SkewX) +
                (node.TotalTransform.ScaleY * node.TotalTransform.ScaleY));
            inflation = (float)(Math.Max(scaleX, scaleY) * inflation);
        }

        if (inflation <= 0f)
        {
            return bounds;
        }

        bounds.Left -= inflation;
        bounds.Top -= inflation;
        bounds.Right += inflation;
        bounds.Bottom += inflation;
        return bounds;
    }

    private static float GetStrokeHitTestWidth(SvgSceneNode node)
    {
        if (node.StrokeWidth > 0f)
        {
            return node.StrokeWidth;
        }

        return node.Element is SvgVisualElement visualElement
            ? visualElement.StrokeWidth.ToDeviceValue(UnitRenderingType.Other, visualElement, node.GeometryBounds)
            : 0f;
    }

    private sealed class HitTestContext
    {
        private readonly Dictionary<SvgSceneNode, SKRect> _subtreeHitTestBounds = new();

        public SKRect GetSubtreeHitTestBounds(SvgSceneNode node)
        {
            if (_subtreeHitTestBounds.TryGetValue(node, out var cachedBounds))
            {
                return cachedBounds;
            }

            var bounds = GetInflatedHitBounds(node, node.TransformedBounds);
            for (var i = 0; i < node.Children.Count; i++)
            {
                bounds = SvgSceneNodeBoundsService.UnionNonEmpty(bounds, GetSubtreeHitTestBounds(node.Children[i]));
            }

            _subtreeHitTestBounds.Add(node, bounds);
            return bounds;
        }
    }
}
