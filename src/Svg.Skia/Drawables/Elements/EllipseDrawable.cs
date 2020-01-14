// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class EllipseDrawable : DrawablePath
    {
        public EllipseDrawable(SvgEllipse svgEllipse, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = CanDraw(svgEllipse, IgnoreDisplay);

            if (!IsDrawable)
            {
                return;
            }

            Path = SKPathUtil.ToSKPath(svgEllipse, svgEllipse.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SKPaintUtil.IsAntialias(svgEllipse);

            TransformedBounds = Path.Bounds;

            Transform = SKMatrixUtil.GetSKMatrix(svgEllipse.Transforms);

            PathClip = SvgClipPathUtil.GetSvgVisualElementClipPath(svgEllipse, TransformedBounds, new HashSet<Uri>(), _disposable);
            PictureMask = SKPaintUtil.GetSvgVisualElementMask(svgEllipse, TransformedBounds, new HashSet<Uri>(), _disposable);
            CreateMaskPaints();
            PaintOpacity = SKPaintUtil.GetOpacitySKPaint(svgEllipse, _disposable);
            PaintFilter = SKPaintUtil.GetFilterSKPaint(svgEllipse, TransformedBounds, _disposable);

            if (SKPaintUtil.IsValidFill(svgEllipse))
            {
                PaintFill = SKPaintUtil.GetFillSKPaint(svgEllipse, TransformedBounds, _disposable);
                if (PaintFill == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            if (SKPaintUtil.IsValidStroke(svgEllipse, TransformedBounds))
            {
                PaintStroke = SKPaintUtil.GetStrokeSKPaint(svgEllipse, TransformedBounds, _disposable);
                if (PaintStroke == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
