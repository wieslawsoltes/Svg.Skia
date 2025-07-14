// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;
using Svg.Model.Drawables;
using ShimSkiaSharp;
using Svg.Model.Services;

namespace Svg.Skia;

public partial class SKSvg
{
    /// <summary>
    /// Returns drawables that hit-test against a point in picture coordinates.
    /// </summary>
    /// <param name="point">Point in picture coordinate space.</param>
    /// <returns>Enumerable of drawables containing the point.</returns>
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

    /// <summary>
    /// Returns drawables that intersect with a rectangle in picture coordinates.
    /// </summary>
    /// <param name="rect">Rectangle in picture coordinate space.</param>
    /// <returns>Enumerable of drawables intersecting the rectangle.</returns>
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

    /// <summary>
    /// Returns SVG elements that hit-test against a point in picture coordinates.
    /// </summary>
    /// <param name="point">Point in picture coordinate space.</param>
    /// <returns>Enumerable of elements containing the point.</returns>
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

    /// <summary>
    /// Returns SVG elements that intersect with a rectangle in picture coordinates.
    /// </summary>
    /// <param name="rect">Rectangle in picture coordinate space.</param>
    /// <returns>Enumerable of elements intersecting the rectangle.</returns>
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

    /// <summary>
    /// Converts a point from canvas coordinates to picture coordinates.
    /// </summary>
    /// <param name="point">The point in canvas coordinate space.</param>
    /// <param name="canvasMatrix">Current canvas transform.</param>
    /// <param name="picturePoint">Resulting point in picture coordinates.</param>
    /// <returns><c>true</c> if conversion succeeded.</returns>
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

    /// <summary>
    /// Converts a rectangle from canvas coordinates to picture coordinates.
    /// </summary>
    /// <param name="rect">The rectangle in canvas coordinate space.</param>
    /// <param name="canvasMatrix">Current canvas transform.</param>
    /// <param name="pictureRect">Resulting rectangle in picture coordinates.</param>
    /// <returns><c>true</c> if conversion succeeded.</returns>
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

    /// <summary>
    /// Returns drawables that hit-test against a point in canvas coordinates.
    /// </summary>
    /// <param name="point">Point in canvas coordinate space.</param>
    /// <param name="canvasMatrix">Current canvas transform.</param>
    /// <returns>Enumerable of drawables containing the point.</returns>
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

    /// <summary>
    /// Returns drawables that intersect with a rectangle in canvas coordinates.
    /// </summary>
    /// <param name="rect">Rectangle in canvas coordinate space.</param>
    /// <param name="canvasMatrix">Current canvas transform.</param>
    /// <returns>Enumerable of drawables intersecting the rectangle.</returns>
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

    /// <summary>
    /// Returns SVG elements that hit-test against a point in canvas coordinates.
    /// </summary>
    /// <param name="point">Point in canvas coordinate space.</param>
    /// <param name="canvasMatrix">Current canvas transform.</param>
    /// <returns>Enumerable of elements containing the point.</returns>
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

    /// <summary>
    /// Returns SVG elements that intersect with a rectangle in canvas coordinates.
    /// </summary>
    /// <param name="rect">Rectangle in canvas coordinate space.</param>
    /// <param name="canvasMatrix">Current canvas transform.</param>
    /// <returns>Enumerable of elements intersecting the rectangle.</returns>
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
