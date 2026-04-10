using System;
using ShimSkiaSharp;

namespace Svg.Skia;

internal static class SvgSceneNodeBoundsService
{
    public static SKRect GetRenderableBounds(SvgSceneNode? node)
    {
        if (node is null)
        {
            return SKRect.Empty;
        }

        var bounds = node.IsRenderable ? node.TransformedBounds : SKRect.Empty;
        for (var i = 0; i < node.Children.Count; i++)
        {
            bounds = UnionNonEmpty(bounds, GetRenderableBounds(node.Children[i]));
        }

        return bounds;
    }

    public static SKRect GetRenderablePaintBounds(SvgSceneNode? node)
    {
        if (node is null)
        {
            return SKRect.Empty;
        }

        var bounds = node.IsRenderable
            ? GetInflatedBounds(node, node.TransformedBounds)
            : SKRect.Empty;

        for (var i = 0; i < node.Children.Count; i++)
        {
            bounds = UnionNonEmpty(bounds, GetRenderablePaintBounds(node.Children[i]));
        }

        return bounds;
    }

    public static SKRect GetPixelAlignedBounds(SKRect bounds)
    {
        if (bounds.IsEmpty)
        {
            return bounds;
        }

        var left = (float)Math.Floor(bounds.Left);
        var top = (float)Math.Floor(bounds.Top);
        var right = (float)Math.Ceiling(bounds.Right);
        var bottom = (float)Math.Ceiling(bounds.Bottom);

        if (right <= left || bottom <= top)
        {
            return bounds;
        }

        return SKRect.Create(left, top, right - left, bottom - top);
    }

    public static SKRect GetInflatedBounds(SvgSceneNode node, SKRect bounds)
    {
        if (bounds.IsEmpty || node.StrokeWidth <= 0f)
        {
            return bounds;
        }

        var scaleX = Math.Sqrt((node.TotalTransform.ScaleX * node.TotalTransform.ScaleX) + (node.TotalTransform.SkewY * node.TotalTransform.SkewY));
        var scaleY = Math.Sqrt((node.TotalTransform.SkewX * node.TotalTransform.SkewX) + (node.TotalTransform.ScaleY * node.TotalTransform.ScaleY));
        var inflation = (float)(Math.Max(scaleX, scaleY) * node.StrokeWidth / 2f);
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

    public static SKRect UnionNonEmpty(SKRect current, SKRect candidate)
    {
        if (candidate.IsEmpty)
        {
            return current;
        }

        return current.IsEmpty
            ? candidate
            : SKRect.Union(current, candidate);
    }
}
