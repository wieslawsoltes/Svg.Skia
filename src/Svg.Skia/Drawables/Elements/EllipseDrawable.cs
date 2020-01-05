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

            Path = SKUtil.ToSKPath(svgEllipse, svgEllipse.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SKUtil.IsAntialias(svgEllipse);

            TransformedBounds = Path.Bounds;

            Transform = SKUtil.GetSKMatrix(svgEllipse.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);

            PathClip = SKUtil.GetSvgVisualElementClipPath(svgEllipse, TransformedBounds, new HashSet<Uri>(), _disposable);
            PaintOpacity = SKUtil.GetOpacitySKPaint(svgEllipse, _disposable);
            PaintFilter = SKUtil.GetFilterSKPaint(svgEllipse, _disposable);

            if (SKUtil.IsValidFill(svgEllipse))
            {
                PaintFill = SKUtil.GetFillSKPaint(svgEllipse, TransformedBounds, _disposable);
            }

            if (SKUtil.IsValidStroke(svgEllipse, TransformedBounds))
            {
                PaintStroke = SKUtil.GetStrokeSKPaint(svgEllipse, TransformedBounds, _disposable);
            }
        }
    }
}
