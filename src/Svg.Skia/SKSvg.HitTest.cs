// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;
using Svg.Model.Drawables;
using ShimSkiaSharp;
using Svg.Model.Services;

namespace Svg.Skia;

public partial class SKSvg
{
    public IEnumerable<DrawableBase> HitTestDrawables(SKPoint point)
    {
        if (Drawable is DrawableBase drawable)
        {
            foreach (var d in HitTestService.HitTest(drawable, point))
            {
                yield return d;
            }
        }
    }

    public IEnumerable<DrawableBase> HitTestDrawables(SKRect rect)
    {
        if (Drawable is DrawableBase drawable)
        {
            foreach (var d in HitTestService.HitTest(drawable, rect))
            {
                yield return d;
            }
        }
    }

    public IEnumerable<SvgElement> HitTestElements(SKPoint point)
    {
        if (Drawable is DrawableBase drawable)
        {
            foreach (var e in HitTestService.HitTestElements(drawable, point))
            {
                yield return e;
            }
        }
    }

    public IEnumerable<SvgElement> HitTestElements(SKRect rect)
    {
        if (Drawable is DrawableBase drawable)
        {
            foreach (var e in HitTestService.HitTestElements(drawable, rect))
            {
                yield return e;
            }
        }
    }

    public bool TryGetPicturePoint(SKPoint point, SKMatrix canvasMatrix, out SKPoint picturePoint)
    {
        if (!canvasMatrix.TryInvert(out var inverse))
        {
            picturePoint = default;
            return false;
        }

        picturePoint = inverse.MapPoint(point);
        return true;
    }

    public bool TryGetPictureRect(SKRect rect, SKMatrix canvasMatrix, out SKRect pictureRect)
    {
        if (!canvasMatrix.TryInvert(out var inverse))
        {
            pictureRect = default;
            return false;
        }

        pictureRect = rect;
        inverse.MapRect(ref pictureRect);
        return true;
    }

    public IEnumerable<DrawableBase> HitTestDrawables(SKPoint point, SKMatrix canvasMatrix)
    {
        if (TryGetPicturePoint(point, canvasMatrix, out var pp))
        {
            foreach (var d in HitTestDrawables(pp))
            {
                yield return d;
            }
        }
    }

    public IEnumerable<DrawableBase> HitTestDrawables(SKRect rect, SKMatrix canvasMatrix)
    {
        if (TryGetPictureRect(rect, canvasMatrix, out var pr))
        {
            foreach (var d in HitTestDrawables(pr))
            {
                yield return d;
            }
        }
    }

    public IEnumerable<SvgElement> HitTestElements(SKPoint point, SKMatrix canvasMatrix)
    {
        if (TryGetPicturePoint(point, canvasMatrix, out var pp))
        {
            foreach (var e in HitTestElements(pp))
            {
                yield return e;
            }
        }
    }

    public IEnumerable<SvgElement> HitTestElements(SKRect rect, SKMatrix canvasMatrix)
    {
        if (TryGetPictureRect(rect, canvasMatrix, out var pr))
        {
            foreach (var e in HitTestElements(pr))
            {
                yield return e;
            }
        }
    }
}
