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
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = CanDraw(svgEllipse, IgnoreDisplay);

            if (!IsDrawable)
            {
                return;
            }

            Path = SkiaUtil.ToSKPath(svgEllipse, svgEllipse.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SkiaUtil.IsAntialias(svgEllipse);

            TransformedBounds = Path.Bounds;

            Transform = SkiaUtil.GetSKMatrix(svgEllipse.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);

            PathClip = SkiaUtil.GetSvgVisualElementClipPath(svgEllipse, TransformedBounds, new HashSet<Uri>(), _disposable);
            PaintOpacity = SkiaUtil.GetOpacitySKPaint(svgEllipse, _disposable);
            PaintFilter = SkiaUtil.GetFilterSKPaint(svgEllipse, _disposable);

            if (SkiaUtil.IsValidFill(svgEllipse))
            {
                PaintFill = SkiaUtil.GetFillSKPaint(svgEllipse, TransformedBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgEllipse, TransformedBounds))
            {
                PaintStroke = SkiaUtil.GetStrokeSKPaint(svgEllipse, TransformedBounds, _disposable);
            }
        }
    }
}
