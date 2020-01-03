// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    internal class PathDrawable : PathBaseDrawable
    {
        public PathDrawable(SvgPath svgPath, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            _ignoreDisplay = ignoreDisplay;
            _canDraw = CanDraw(svgPath, _ignoreDisplay);

            if (!_canDraw)
            {
                return;
            }

            skPath = SkiaUtil.ToSKPath(svgPath.PathData, svgPath.FillRule, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                _canDraw = false;
                return;
            }

            _antialias = SkiaUtil.IsAntialias(svgPath);

            _skBounds = skPath.Bounds;

            // TODO: Transform _skBounds using _skMatrix.

            _skMatrix = SkiaUtil.GetSKMatrix(svgPath.Transforms);
            _skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgPath, _skBounds, new HashSet<Uri>(), _disposable);
            _skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgPath, _disposable);
            _skPaintFilter = SkiaUtil.GetFilterSKPaint(svgPath, _disposable);

            if (SkiaUtil.IsValidFill(svgPath))
            {
                _skPaintFill = SkiaUtil.GetFillSKPaint(svgPath, _skBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgPath))
            {
                _skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgPath, _skBounds, _disposable);
            }
        }
    }
}
