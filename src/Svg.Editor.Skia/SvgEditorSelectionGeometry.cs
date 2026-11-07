using System;
using System.Collections.Generic;
using SkiaSharp;
using Svg.Skia;
using Shim = ShimSkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>Owned native geometry for selection only; retained scene resources are never changed.</summary>
public static class SvgEditorSelectionGeometry
{
    public const int MaximumLassoPoints = 16384;

    public static SKPath CreateLasso(IReadOnlyList<SKPoint> points)
    {
        if (points is null)
            throw new ArgumentNullException(nameof(points));
        if (points.Count < 3 || points.Count > MaximumLassoPoints)
            throw new ArgumentOutOfRangeException(nameof(points), "A lasso requires 3–16,384 points.");
        using var builder = new SKPathBuilder { FillType = SKPathFillType.EvenOdd };
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            if (!Finite(point.X) || !Finite(point.Y))
                throw new ArgumentOutOfRangeException(nameof(points), "Lasso coordinates must be finite.");
            if (index == 0)
                builder.MoveTo(point);
            else
                builder.LineTo(point);
        }
        builder.Close();
        return builder.Detach();
    }

    public static bool Matches(SKPath artwork, SKPath lasso, bool fullyContained = false)
    {
        if (artwork is null)
            throw new ArgumentNullException(nameof(artwork));
        if (lasso is null)
            throw new ArgumentNullException(nameof(lasso));
        if (artwork.IsEmpty || lasso.IsEmpty)
            return false;
        using var result = artwork.Op(lasso, fullyContained ? SKPathOp.Difference : SKPathOp.Intersect)
            ?? throw new InvalidOperationException("The selection outline could not be evaluated.");
        return fullyContained ? result.IsEmpty : !result.IsEmpty;
    }

    internal static SKPath? CreateArtwork(SvgSceneNode node, SkiaModel model)
    {
        for (var ancestor = node; ancestor is not null; ancestor = ancestor.Parent)
            if (ancestor.IsDisplayNone || ancestor.SuppressSubtreeRendering)
                return null;
        if (!node.IsVisible)
            return null;
        SKPath? geometry = null;
        try
        {
            if (node.HitTestPath is { } source)
            {
                using var path = model.ToSKPath(source);
                var matrix = model.ToSKMatrix(node.TotalTransform);
                if (node.SupportsFillHitTest)
                {
                    using var fill = new SKPath(path);
                    fill.Transform(matrix);
                    Accumulate(ref geometry, fill, SKPathOp.Union);
                }
                if (node.SupportsStrokeHitTest && node.StrokeWidth > 0)
                {
                    using var strokeSource = new SKPath(path);
                    if (node.IsStrokeNonScaling)
                        strokeSource.Transform(matrix);
                    using var paint = new SKPaint
                    {
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = node.StrokeWidth,
                        StrokeCap = model.ToSKStrokeCap(node.Stroke?.StrokeCap ?? Shim.SKStrokeCap.Butt),
                        StrokeJoin = model.ToSKStrokeJoin(node.Stroke?.StrokeJoin ?? Shim.SKStrokeJoin.Miter),
                        StrokeMiter = node.Stroke?.StrokeMiter ?? 4
                    };
                    using var effect = model.ToSKPathEffect(node.Stroke?.PathEffect);
                    paint.PathEffect = effect;
                    using var builder = new SKPathBuilder();
                    if (paint.GetFillPath(strokeSource, builder))
                    {
                        using var outline = builder.Detach();
                        if (!node.IsStrokeNonScaling)
                            outline.Transform(matrix);
                        Accumulate(ref geometry, outline, SKPathOp.Union);
                    }
                }
            }
            else if (node.Kind == SvgSceneNodeKind.Use && node.Children.Count > 0)
            {
                foreach (var child in node.Children)
                {
                    using var childPath = CreateArtwork(child, model);
                    if (childPath is not null)
                        Accumulate(ref geometry, childPath, SKPathOp.Union);
                }
            }
            else if (!node.GeometryBounds.IsEmpty)
            {
                using var bounds = Rectangle(node.GeometryBounds, node.TotalTransform, model);
                Accumulate(ref geometry, bounds, SKPathOp.Union);
            }
            if (geometry is null)
                return null;
            for (var ancestor = node; ancestor is not null && !geometry.IsEmpty; ancestor = ancestor.Parent)
            {
                if (ancestor.Clip is { } clip)
                {
                    using var path = Rectangle(clip, ancestor.TotalTransform, model);
                    Accumulate(ref geometry, path, SKPathOp.Intersect);
                }
                if (ancestor.InnerClip is { } innerClip)
                {
                    using var path = Rectangle(innerClip, ancestor.TotalTransform, model);
                    Accumulate(ref geometry, path, SKPathOp.Intersect);
                }
                if (ancestor.ClipPath is { } clipPath)
                {
                    using var path = CreateClip(clipPath, model, 0);
                    path.Transform(model.ToSKMatrix(ancestor.TotalTransform));
                    Accumulate(ref geometry, path, SKPathOp.Intersect);
                }
            }
            var result = geometry;
            geometry = null;
            return result;
        }
        finally { geometry?.Dispose(); }
    }

    private static SKPath CreateClip(Shim.ClipPath clip, SkiaModel model, int depth)
    {
        if (depth >= 64)
            throw new InvalidOperationException("The selection clip nesting exceeds the supported depth.");
        SKPath? result = null;
        try
        {
            if (clip.Clips is { } entries)
            {
                foreach (var entry in entries)
                {
                    using var item = entry.Path is { } source ? model.ToSKPath(source) : new SKPath();
                    if (entry.Clip is { } itemClip && HasClipGeometry(itemClip, depth + 1))
                    {
                        using var nested = CreateClip(itemClip, model, depth + 1);
                        using var clipped = item.Op(nested, SKPathOp.Intersect)
                            ?? throw new InvalidOperationException("A selection clip could not be evaluated.");
                        if (entry.Transform is { } itemTransform)
                            clipped.Transform(model.ToSKMatrix(itemTransform));
                        Accumulate(ref result, clipped, SKPathOp.Union);
                    }
                    else
                    {
                        if (entry.Transform is { } itemTransform)
                            item.Transform(model.ToSKMatrix(itemTransform));
                        Accumulate(ref result, item, SKPathOp.Union);
                    }
                }
            }
            if (clip.Clip is { } outerClip && HasClipGeometry(outerClip, depth + 1))
            {
                using var nested = CreateClip(outerClip, model, depth + 1);
                Accumulate(ref result, nested, SKPathOp.Intersect);
            }
            result ??= new SKPath();
            if (clip.Transform is { } transform)
                result.Transform(model.ToSKMatrix(transform));
            var owned = result;
            result = null;
            return owned;
        }
        finally { result?.Dispose(); }
    }

    private static bool HasClipGeometry(Shim.ClipPath clip, int depth)
    {
        if (depth >= 64)
            throw new InvalidOperationException("The selection clip nesting exceeds the supported depth.");
        // The retained compiler uses empty nested ClipPath instances as optional
        // placeholders. Treating these as a real empty clip would hide every object.
        return clip.Clips is { Count: > 0 } || clip.Clip is { } nested && HasClipGeometry(nested, depth + 1);
    }

    private static SKPath Rectangle(Shim.SKRect rectangle, Shim.SKMatrix transform, SkiaModel model)
    {
        using var builder = new SKPathBuilder();
        builder.AddRect(new SKRect(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom));
        var path = builder.Detach();
        path.Transform(model.ToSKMatrix(transform));
        return path;
    }

    private static void Accumulate(ref SKPath? target, SKPath operand, SKPathOp operation)
    {
        if (target is null)
        { target = new SKPath(operand); return; }
        var combined = target.Op(operand, operation)
            ?? throw new InvalidOperationException("The selection geometry could not be evaluated.");
        target.Dispose();
        target = combined;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
