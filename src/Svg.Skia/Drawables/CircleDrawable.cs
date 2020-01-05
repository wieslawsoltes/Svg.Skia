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
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = CanDraw(svgCircle, IgnoreDisplay);

            if (!IsDrawable)
            {
                return;
            }

            Path = SkiaUtil.ToSKPath(svgCircle, svgCircle.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SkiaUtil.IsAntialias(svgCircle);

            TransformedBounds = Path.Bounds;

            Transform = SkiaUtil.GetSKMatrix(svgCircle.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);

            PathClip = SkiaUtil.GetSvgVisualElementClipPath(svgCircle, TransformedBounds, new HashSet<Uri>(), _disposable);
            PaintOpacity = SkiaUtil.GetOpacitySKPaint(svgCircle, _disposable);
            PaintFilter = SkiaUtil.GetFilterSKPaint(svgCircle, _disposable);

            if (SkiaUtil.IsValidFill(svgCircle))
            {
                PaintFill = SkiaUtil.GetFillSKPaint(svgCircle, TransformedBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgCircle, TransformedBounds))
            {
                PaintStroke = SkiaUtil.GetStrokeSKPaint(svgCircle, TransformedBounds, _disposable);
            }
        }
    }
}
