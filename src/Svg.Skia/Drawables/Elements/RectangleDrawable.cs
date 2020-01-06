// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class RectangleDrawable : DrawablePath
    {
        public RectangleDrawable(SvgRectangle svgRectangle, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = CanDraw(svgRectangle, IgnoreDisplay);

            if (!IsDrawable)
            {
                return;
            }

            Path = SKUtil.ToSKPath(svgRectangle, svgRectangle.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SKUtil.IsAntialias(svgRectangle);

            TransformedBounds = Path.Bounds;

            Transform = SKUtil.GetSKMatrix(svgRectangle.Transforms);

            PathClip = SKUtil.GetSvgVisualElementClipPath(svgRectangle, TransformedBounds, new HashSet<Uri>(), _disposable);
            PaintOpacity = SKUtil.GetOpacitySKPaint(svgRectangle, _disposable);
            PaintFilter = SKUtil.GetFilterSKPaint(svgRectangle, _disposable);

            if (SKUtil.IsValidFill(svgRectangle))
            {
                PaintFill = SKUtil.GetFillSKPaint(svgRectangle, TransformedBounds, _disposable);
            }

            if (SKUtil.IsValidStroke(svgRectangle, TransformedBounds))
            {
                PaintStroke = SKUtil.GetStrokeSKPaint(svgRectangle, TransformedBounds, _disposable);
            }

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
