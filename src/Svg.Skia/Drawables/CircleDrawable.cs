// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    internal class CircleDrawable : DrawablePath
    {
        public CircleDrawable(SvgCircle svgCircle, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            _ignoreDisplay = ignoreDisplay;
            _canDraw = CanDraw(svgCircle, _ignoreDisplay);

            if (!_canDraw)
            {
                return;
            }

            _skPath = SkiaUtil.ToSKPath(svgCircle, svgCircle.FillRule, skOwnerBounds, _disposable);
            if (_skPath == null || _skPath.IsEmpty)
            {
                _canDraw = false;
                return;
            }

            _antialias = SkiaUtil.IsAntialias(svgCircle);

            _skBounds = _skPath.Bounds;

            // TODO: Transform _skBounds using _skMatrix.

            _skMatrix = SkiaUtil.GetSKMatrix(svgCircle.Transforms);
            _skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgCircle, _skBounds, new HashSet<Uri>(), _disposable);
            _skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgCircle, _disposable);
            _skPaintFilter = SkiaUtil.GetFilterSKPaint(svgCircle, _disposable);

            if (SkiaUtil.IsValidFill(svgCircle))
            {
                _skPaintFill = SkiaUtil.GetFillSKPaint(svgCircle, _skBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgCircle, _skBounds))
            {
                _skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgCircle, _skBounds, _disposable);
            }
        }
    }
}
