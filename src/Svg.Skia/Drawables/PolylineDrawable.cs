// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    internal class PolylineDrawable : DrawablePath
    {
        public PolylineDrawable(SvgPolyline svgPolyline, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = CanDraw(svgPolyline, IgnoreDisplay);

            if (!IsDrawable)
            {
                return;
            }

            Path = SkiaUtil.ToSKPath(svgPolyline.Points, svgPolyline.FillRule, false, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SkiaUtil.IsAntialias(svgPolyline);

            TransformedBounds = Path.Bounds;

            Transform = SkiaUtil.GetSKMatrix(svgPolyline.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);

            PathClip = SkiaUtil.GetSvgVisualElementClipPath(svgPolyline, TransformedBounds, new HashSet<Uri>(), _disposable);
            PaintOpacity = SkiaUtil.GetOpacitySKPaint(svgPolyline, _disposable);
            PaintFilter = SkiaUtil.GetFilterSKPaint(svgPolyline, _disposable);

            if (SkiaUtil.IsValidFill(svgPolyline))
            {
                PaintFill = SkiaUtil.GetFillSKPaint(svgPolyline, TransformedBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgPolyline, TransformedBounds))
            {
                PaintStroke = SkiaUtil.GetStrokeSKPaint(svgPolyline, TransformedBounds, _disposable);
            }

            CreateMarkers(svgPolyline, Path, skOwnerBounds);
        }
    }
}
