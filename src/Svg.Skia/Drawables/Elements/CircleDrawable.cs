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

            Path = SKUtil.ToSKPath(svgCircle, svgCircle.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SKUtil.IsAntialias(svgCircle);

            TransformedBounds = Path.Bounds;

            Transform = SKUtil.GetSKMatrix(svgCircle.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);

            PathClip = SKUtil.GetSvgVisualElementClipPath(svgCircle, TransformedBounds, new HashSet<Uri>(), _disposable);
            PaintOpacity = SKUtil.GetOpacitySKPaint(svgCircle, _disposable);
            PaintFilter = SKUtil.GetFilterSKPaint(svgCircle, _disposable);

            if (SKUtil.IsValidFill(svgCircle))
            {
                PaintFill = SKUtil.GetFillSKPaint(svgCircle, TransformedBounds, _disposable);
            }

            if (SKUtil.IsValidStroke(svgCircle, TransformedBounds))
            {
                PaintStroke = SKUtil.GetStrokeSKPaint(svgCircle, TransformedBounds, _disposable);
            }
        }
    }
}
