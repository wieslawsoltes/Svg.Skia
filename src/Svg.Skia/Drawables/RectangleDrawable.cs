// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    internal class RectangleDrawable : DrawablePath
    {
        public RectangleDrawable(SvgRectangle svgRectangle, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            _ignoreDisplay = ignoreDisplay;
            _canDraw = CanDraw(svgRectangle, _ignoreDisplay);

            if (!_canDraw)
            {
                return;
            }

            _skPath = SkiaUtil.ToSKPath(svgRectangle, svgRectangle.FillRule, skOwnerBounds, _disposable);
            if (_skPath == null || _skPath.IsEmpty)
            {
                _canDraw = false;
                return;
            }

            _antialias = SkiaUtil.IsAntialias(svgRectangle);

            _skBounds = _skPath.Bounds;

            // TODO: Transform _skBounds using _skMatrix.

            _skMatrix = SkiaUtil.GetSKMatrix(svgRectangle.Transforms);
            _skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgRectangle, _skBounds, new HashSet<Uri>(), _disposable);
            _skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgRectangle, _disposable);
            _skPaintFilter = SkiaUtil.GetFilterSKPaint(svgRectangle, _disposable);

            if (SkiaUtil.IsValidFill(svgRectangle))
            {
                _skPaintFill = SkiaUtil.GetFillSKPaint(svgRectangle, _skBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgRectangle, _skBounds))
            {
                _skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgRectangle, _skBounds, _disposable);
            }
        }
    }
}
