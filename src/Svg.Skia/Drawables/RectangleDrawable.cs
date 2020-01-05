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
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = CanDraw(svgRectangle, IgnoreDisplay);

            if (!IsDrawable)
            {
                return;
            }

            Path = SkiaUtil.ToSKPath(svgRectangle, svgRectangle.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SkiaUtil.IsAntialias(svgRectangle);

            TransformedBounds = Path.Bounds;

            Transform = SkiaUtil.GetSKMatrix(svgRectangle.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);

            PathClip = SkiaUtil.GetSvgVisualElementClipPath(svgRectangle, TransformedBounds, new HashSet<Uri>(), _disposable);
            PaintOpacity = SkiaUtil.GetOpacitySKPaint(svgRectangle, _disposable);
            PaintFilter = SkiaUtil.GetFilterSKPaint(svgRectangle, _disposable);

            if (SkiaUtil.IsValidFill(svgRectangle))
            {
                PaintFill = SkiaUtil.GetFillSKPaint(svgRectangle, TransformedBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgRectangle, TransformedBounds))
            {
                PaintStroke = SkiaUtil.GetStrokeSKPaint(svgRectangle, TransformedBounds, _disposable);
            }
        }
    }
}
