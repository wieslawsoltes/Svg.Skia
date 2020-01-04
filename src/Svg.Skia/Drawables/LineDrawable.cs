// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    internal class LineDrawable : PathBaseDrawable
    {
        public LineDrawable(SvgLine svgLine, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            _ignoreDisplay = ignoreDisplay;
            _canDraw = CanDraw(svgLine, _ignoreDisplay);

            if (!_canDraw)
            {
                return;
            }

            _skPath = SkiaUtil.ToSKPath(svgLine, svgLine.FillRule, skOwnerBounds, _disposable);
            if (_skPath == null || _skPath.IsEmpty)
            {
                _canDraw = false;
                return;
            }

            _antialias = SkiaUtil.IsAntialias(svgLine);

            _skBounds = _skPath.Bounds;

            // TODO: Transform _skBounds using _skMatrix.

            _skMatrix = SkiaUtil.GetSKMatrix(svgLine.Transforms);
            _skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgLine, _skBounds, new HashSet<Uri>(), _disposable);
            _skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgLine, _disposable);
            _skPaintFilter = SkiaUtil.GetFilterSKPaint(svgLine, _disposable);

            if (SkiaUtil.IsValidFill(svgLine))
            {
                _skPaintFill = SkiaUtil.GetFillSKPaint(svgLine, _skBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgLine, _skBounds))
            {
                _skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgLine, _skBounds, _disposable);
            }

            CreateMarkers(svgLine, _skPath, skOwnerBounds);
        }
    }
}
