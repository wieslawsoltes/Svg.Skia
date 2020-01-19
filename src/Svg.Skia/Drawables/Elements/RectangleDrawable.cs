// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class RectangleDrawable : DrawablePath
    {
        public RectangleDrawable(SvgRectangle svgRectangle, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes = IgnoreAttributes.None)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgRectangle, IgnoreAttributes);

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

            ClipPath = SvgClippingExtensions.GetSvgVisualElementClipPath(svgRectangle, TransformedBounds, new HashSet<Uri>(), _disposable);
            MaskDrawable = SvgClippingExtensions.GetSvgVisualElementMask(svgRectangle, TransformedBounds, new HashSet<Uri>(), _disposable);
            if (MaskDrawable != null)
            {
                CreateMaskPaints();
            }
            Opacity = ignoreAttributes.HasFlag(IgnoreAttributes.Opacity) ? null : SvgPaintingExtensions.GetOpacitySKPaint(svgRectangle, _disposable);
            Filter = ignoreAttributes.HasFlag(IgnoreAttributes.Filter) ? null : SvgFiltersExtensions.GetFilterSKPaint(svgRectangle, TransformedBounds, _disposable);

            if (SvgPaintingExtensions.IsValidFill(svgRectangle))
            {
                Fill = SvgPaintingExtensions.GetFillSKPaint(svgRectangle, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgRectangle, TransformedBounds))
            {
                Stroke = SvgPaintingExtensions.GetStrokeSKPaint(svgRectangle, TransformedBounds, ignoreAttributes, _disposable);
                if (Stroke == null)
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
