// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class CircleDrawable : DrawablePath
    {
        public CircleDrawable(SvgCircle svgCircle, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgCircle, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgCircle, IgnoreAttributes) && HasFeatures(svgCircle, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            Path = svgCircle.ToSKPath(svgCircle.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgCircle);

            TransformedBounds = Path.Bounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgCircle.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgPaintingExtensions.IsValidFill(svgCircle))
            {
                Fill = SvgPaintingExtensions.GetFillSKPaint(svgCircle, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgCircle, TransformedBounds))
            {
                Stroke = SvgPaintingExtensions.GetStrokeSKPaint(svgCircle, TransformedBounds, ignoreAttributes, _disposable);
                if (Stroke == null)
                {
                    canDrawStroke = false;
                }
            }

            if (canDrawFill && !canDrawStroke)
            {
                IsDrawable = false;
                return;
            }

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
