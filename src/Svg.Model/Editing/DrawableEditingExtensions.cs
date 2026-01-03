// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Svg.Model.Drawables;

namespace Svg.Model.Editing;

public static class DrawableEditingExtensions
{
    public static int UpdateFills(
        this DrawableBase root,
        Func<SKPaint, bool> predicate,
        Action<SKPaint> update,
        EditMode mode = EditMode.InPlace)
    {
        return UpdatePaints(root, predicate, update, mode, drawable => drawable.Fill, (drawable, paint) => drawable.Fill = paint);
    }

    public static int UpdateStrokes(
        this DrawableBase root,
        Func<SKPaint, bool> predicate,
        Action<SKPaint> update,
        EditMode mode = EditMode.InPlace)
    {
        return UpdatePaints(root, predicate, update, mode, drawable => drawable.Stroke, (drawable, paint) => drawable.Stroke = paint);
    }

    public static int UpdateOpacity(
        this DrawableBase root,
        Func<SKPaint, bool> predicate,
        Action<SKPaint> update,
        EditMode mode = EditMode.InPlace)
    {
        return UpdatePaints(root, predicate, update, mode, drawable => drawable.Opacity, (drawable, paint) => drawable.Opacity = paint);
    }

    private static int UpdatePaints(
        DrawableBase root,
        Func<SKPaint, bool> predicate,
        Action<SKPaint> update,
        EditMode mode,
        Func<DrawableBase, SKPaint?> selector,
        Action<DrawableBase, SKPaint?> assign)
    {
        if (root is null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        if (update is null)
        {
            throw new ArgumentNullException(nameof(update));
        }

        var count = 0;
        Dictionary<SKPaint, SKPaint>? clones = null;
        var visited = new HashSet<SKPaint>(ReferenceEqualityComparer.Instance);

        foreach (var drawable in DrawableWalker.Traverse(root))
        {
            var paint = selector(drawable);
            if (paint is null || !predicate(paint))
            {
                continue;
            }

            if (mode == EditMode.CloneOnWrite)
            {
                clones ??= new Dictionary<SKPaint, SKPaint>(ReferenceEqualityComparer.Instance);
                if (!clones.TryGetValue(paint, out var clone))
                {
                    clone = paint.DeepClone();
                    clones[paint] = clone;
                }

                assign(drawable, clone);
                if (visited.Add(paint))
                {
                    update(clone);
                    count++;
                }
            }
            else if (visited.Add(paint))
            {
                update(paint);
                count++;
            }
        }

        return count;
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<SKPaint>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public bool Equals(SKPaint? x, SKPaint? y) => ReferenceEquals(x, y);

        public int GetHashCode(SKPaint obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
