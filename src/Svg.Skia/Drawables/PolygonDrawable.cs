// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    internal class PolygonDrawable : DrawablePath
    {
        public PolygonDrawable(SvgPolygon svgPolygon, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            _ignoreDisplay = ignoreDisplay;
            _canDraw = CanDraw(svgPolygon, _ignoreDisplay);

            if (!_canDraw)
            {
                return;
            }

            _skPath = SkiaUtil.ToSKPath(svgPolygon.Points, svgPolygon.FillRule, true, skOwnerBounds, _disposable);
            if (_skPath == null || _skPath.IsEmpty)
            {
                _canDraw = false;
                return;
            }

            _antialias = SkiaUtil.IsAntialias(svgPolygon);

            _skBounds = _skPath.Bounds;

            // TODO: Transform _skBounds using _skMatrix.

            _skMatrix = SkiaUtil.GetSKMatrix(svgPolygon.Transforms);
            _skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgPolygon, _skBounds, new HashSet<Uri>(), _disposable);
            _skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgPolygon, _disposable);
            _skPaintFilter = SkiaUtil.GetFilterSKPaint(svgPolygon, _disposable);

            if (SkiaUtil.IsValidFill(svgPolygon))
            {
                _skPaintFill = SkiaUtil.GetFillSKPaint(svgPolygon, _skBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgPolygon, _skBounds))
            {
                _skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgPolygon, _skBounds, _disposable);
            }

            CreateMarkers(svgPolygon, _skPath, skOwnerBounds);
        }
    }
}
