// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class RectangleDrawable : DrawablePath
    {
        public RectangleDrawable(SvgRectangle svgRectangle, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgRectangle, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgRectangle, IgnoreAttributes) && HasFeatures(svgRectangle, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            Path = svgRectangle.ToSKPath(svgRectangle.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgRectangle);

            TransformedBounds = Path.Bounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgRectangle.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgPaintingExtensions.IsValidFill(svgRectangle))
            {
                Fill = SvgPaintingExtensions.GetFillSKPaint(svgRectangle, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgRectangle, TransformedBounds))
            {
                Stroke = SvgPaintingExtensions.GetStrokeSKPaint(svgRectangle, TransformedBounds, ignoreAttributes, _disposable);
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
