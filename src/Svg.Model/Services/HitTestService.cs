// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.Model.Drawables;

namespace Svg.Model.Services;

public static class HitTestService
{
    public static bool IntersectsWith(SKRect a, SKRect b)
    {
        return a.Left < b.Right && a.Right > b.Left &&
               a.Top < b.Bottom && a.Bottom > b.Top;
    }

    public static IEnumerable<DrawableBase> HitTest(DrawableBase drawable, SKPoint point)
    {
        if (drawable is DrawableContainer container)
        {
            foreach (var child in container.ChildrenDrawables)
            {
                foreach (var result in HitTest(child, point))
                {
                    yield return result;
                }
            }
        }

        if (drawable.HitTest(point))
        {
            yield return drawable;
        }
    }

    public static IEnumerable<DrawableBase> HitTest(DrawableBase drawable, SKRect rect)
    {
        if (drawable is DrawableContainer container)
        {
            foreach (var child in container.ChildrenDrawables)
            {
                foreach (var result in HitTest(child, rect))
                {
                    yield return result;
                }
            }
        }

        if (drawable.HitTest(rect))
        {
            yield return drawable;
        }
    }

    public static IEnumerable<SvgElement> HitTestElements(DrawableBase drawable, SKPoint point)
    {
        foreach (var d in HitTest(drawable, point))
        {
            if (d.Element is { } e)
            {
                yield return e;
            }
        }
    }

    public static IEnumerable<SvgElement> HitTestElements(DrawableBase drawable, SKRect rect)
    {
        foreach (var d in HitTest(drawable, rect))
        {
            if (d.Element is { } e)
            {
                yield return e;
            }
        }
    }
}

