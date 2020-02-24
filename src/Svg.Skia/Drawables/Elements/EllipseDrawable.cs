// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class EllipseDrawable : DrawablePath
    {
        public EllipseDrawable(SvgEllipse svgEllipse, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgEllipse, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgEllipse, IgnoreAttributes) && HasFeatures(svgEllipse, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            Path = svgEllipse.ToSKPath(svgEllipse.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgEllipse);

            TransformedBounds = Path.Bounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgEllipse.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgPaintingExtensions.IsValidFill(svgEllipse))
            {
                Fill = SvgPaintingExtensions.GetFillSKPaint(svgEllipse, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgEllipse, TransformedBounds))
            {
                Stroke = SvgPaintingExtensions.GetStrokeSKPaint(svgEllipse, TransformedBounds, ignoreAttributes, _disposable);
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
