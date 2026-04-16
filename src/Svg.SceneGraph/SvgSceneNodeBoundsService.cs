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

        var bounds = GetSelfRenderableBounds(node);
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

        return node.HasRenderablePaintBounds
            ? node.RenderablePaintBounds
            : ComputeRenderablePaintBounds(node);
    }

    public static SKRect GetLocalRenderablePaintBounds(SvgSceneNode? node)
    {
        if (node is null)
        {
            return SKRect.Empty;
        }

        return node.HasLocalRenderablePaintBounds
            ? node.LocalRenderablePaintBounds
            : ComputeLocalRenderablePaintBounds(node);
    }

    internal static SKRect CacheRenderablePaintBounds(SvgSceneNode? node)
    {
        if (node is null)
        {
            return SKRect.Empty;
        }

        CacheRenderablePaintBounds(node, out var worldBounds, out _);
        return worldBounds;
    }

    internal static void RefreshRenderablePaintBoundsUpward(SvgSceneNode? node)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            current.RenderablePaintBounds = ComputeRenderablePaintBounds(current);
            current.HasRenderablePaintBounds = true;
            current.LocalRenderablePaintBounds = ComputeLocalRenderablePaintBounds(current);
            current.HasLocalRenderablePaintBounds = true;
        }
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

    private static void CacheRenderablePaintBounds(SvgSceneNode node, out SKRect worldBounds, out SKRect localBounds)
    {
        worldBounds = GetSelfRenderablePaintBounds(node);
        localBounds = GetSelfLocalRenderablePaintBounds(node);

        for (var i = 0; i < node.Children.Count; i++)
        {
            CacheRenderablePaintBounds(node.Children[i], out var childWorldBounds, out var childLocalBounds);
            worldBounds = UnionNonEmpty(worldBounds, childWorldBounds);
            localBounds = UnionNonEmpty(localBounds, MapToParentLocalSpace(node.Children[i], childLocalBounds));
        }

        node.RenderablePaintBounds = worldBounds;
        node.HasRenderablePaintBounds = true;
        node.LocalRenderablePaintBounds = localBounds;
        node.HasLocalRenderablePaintBounds = true;
    }

    private static SKRect ComputeRenderablePaintBounds(SvgSceneNode node)
    {
        var bounds = GetSelfRenderablePaintBounds(node);

        for (var i = 0; i < node.Children.Count; i++)
        {
            bounds = UnionNonEmpty(bounds, GetRenderablePaintBounds(node.Children[i]));
        }

        return bounds;
    }

    private static SKRect ComputeLocalRenderablePaintBounds(SvgSceneNode node)
    {
        var bounds = GetSelfLocalRenderablePaintBounds(node);

        for (var i = 0; i < node.Children.Count; i++)
        {
            bounds = UnionNonEmpty(bounds, MapToParentLocalSpace(node.Children[i], GetLocalRenderablePaintBounds(node.Children[i])));
        }

        return bounds;
    }

    private static SKRect GetSelfRenderableBounds(SvgSceneNode node)
    {
        var bounds = node.IsRenderable ? node.TransformedBounds : SKRect.Empty;
        if (node.StandaloneFilterModel is { } standaloneFilterModel && !standaloneFilterModel.CullRect.IsEmpty)
        {
            bounds = UnionNonEmpty(bounds, node.TotalTransform.MapRect(standaloneFilterModel.CullRect));
        }

        return bounds;
    }

    private static SKRect GetSelfRenderablePaintBounds(SvgSceneNode node)
    {
        var bounds = node.IsRenderable
            ? GetInflatedBounds(node, node.TransformedBounds)
            : SKRect.Empty;
        if (node.StandaloneFilterModel is { } standaloneFilterModel && !standaloneFilterModel.CullRect.IsEmpty)
        {
            bounds = UnionNonEmpty(bounds, node.TotalTransform.MapRect(standaloneFilterModel.CullRect));
        }

        return bounds;
    }

    private static SKRect GetSelfLocalRenderablePaintBounds(SvgSceneNode node)
    {
        var bounds = node.IsRenderable
            ? GetLocalInflatedBounds(node, node.GeometryBounds)
            : SKRect.Empty;
        if (node.StandaloneFilterModel is { } standaloneFilterModel && !standaloneFilterModel.CullRect.IsEmpty)
        {
            bounds = UnionNonEmpty(bounds, standaloneFilterModel.CullRect);
        }

        return bounds;
    }

    private static SKRect GetLocalInflatedBounds(SvgSceneNode node, SKRect bounds)
    {
        if (bounds.IsEmpty || node.StrokeWidth <= 0f)
        {
            return bounds;
        }

        var inflation = node.StrokeWidth / 2f;
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

    private static SKRect MapToParentLocalSpace(SvgSceneNode child, SKRect bounds)
    {
        if (bounds.IsEmpty)
        {
            return bounds;
        }

        return child.Transform.IsIdentity
            ? bounds
            : child.Transform.MapRect(bounds);
    }
}
