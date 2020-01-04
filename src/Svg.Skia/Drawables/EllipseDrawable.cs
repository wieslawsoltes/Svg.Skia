// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    internal class EllipseDrawable : DrawablePath
    {
        public EllipseDrawable(SvgEllipse svgEllipse, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            _ignoreDisplay = ignoreDisplay;
            _canDraw = CanDraw(svgEllipse, _ignoreDisplay);

            if (!_canDraw)
            {
                return;
            }

            _skPath = SkiaUtil.ToSKPath(svgEllipse, svgEllipse.FillRule, skOwnerBounds, _disposable);
            if (_skPath == null || _skPath.IsEmpty)
            {
                _canDraw = false;
                return;
            }

            _antialias = SkiaUtil.IsAntialias(svgEllipse);

            _skBounds = _skPath.Bounds;

            // TODO: Transform _skBounds using _skMatrix.

            _skMatrix = SkiaUtil.GetSKMatrix(svgEllipse.Transforms);
            _skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgEllipse, _skBounds, new HashSet<Uri>(), _disposable);
            _skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgEllipse, _disposable);
            _skPaintFilter = SkiaUtil.GetFilterSKPaint(svgEllipse, _disposable);

            if (SkiaUtil.IsValidFill(svgEllipse))
            {
                _skPaintFill = SkiaUtil.GetFillSKPaint(svgEllipse, _skBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgEllipse, _skBounds))
            {
                _skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgEllipse, _skBounds, _disposable);
            }
        }
    }
}
