// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    internal class RectangleDrawable : PathBaseDrawable
    {
        public RectangleDrawable(SvgRectangle svgRectangle, SKSize sKSize, bool ignoreDisplay)
        {
            _ignoreDisplay = ignoreDisplay;
            _canDraw = CanDraw(svgRectangle, _ignoreDisplay);

            if (!_canDraw)
            {
                return;
            }

            skPath = SkiaUtil.ToSKPath(svgRectangle, svgRectangle.FillRule, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                _canDraw = false;
                return;
            }

            _antialias = SkiaUtil.IsAntialias(svgRectangle);

            _skBounds = skPath.Bounds;

            _skMatrix = SkiaUtil.GetSKMatrix(svgRectangle.Transforms);
            _skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgRectangle, _skBounds, new HashSet<Uri>(), _disposable);
            _skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgRectangle, _disposable);
            _skPaintFilter = SkiaUtil.GetFilterSKPaint(svgRectangle, _disposable);

            if (SkiaUtil.IsValidFill(svgRectangle))
            {
                _skPaintFill = SkiaUtil.GetFillSKPaint(svgRectangle, sKSize, _skBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgRectangle))
            {
                _skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgRectangle, sKSize, _skBounds, _disposable);
            }
        }
    }
}
