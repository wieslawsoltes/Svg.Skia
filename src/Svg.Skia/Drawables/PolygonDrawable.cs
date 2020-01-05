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
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = CanDraw(svgPolygon, IgnoreDisplay);

            if (!IsDrawable)
            {
                return;
            }

            Path = SkiaUtil.ToSKPath(svgPolygon.Points, svgPolygon.FillRule, true, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SkiaUtil.IsAntialias(svgPolygon);

            TransformedBounds = Path.Bounds;

            Transform = SkiaUtil.GetSKMatrix(svgPolygon.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);

            PathClip = SkiaUtil.GetSvgVisualElementClipPath(svgPolygon, TransformedBounds, new HashSet<Uri>(), _disposable);
            PaintOpacity = SkiaUtil.GetOpacitySKPaint(svgPolygon, _disposable);
            PaintFilter = SkiaUtil.GetFilterSKPaint(svgPolygon, _disposable);

            if (SkiaUtil.IsValidFill(svgPolygon))
            {
                PaintFill = SkiaUtil.GetFillSKPaint(svgPolygon, TransformedBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgPolygon, TransformedBounds))
            {
                PaintStroke = SkiaUtil.GetStrokeSKPaint(svgPolygon, TransformedBounds, _disposable);
            }

            CreateMarkers(svgPolygon, Path, skOwnerBounds);
        }
    }
}
