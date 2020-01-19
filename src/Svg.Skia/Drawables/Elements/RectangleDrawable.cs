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

            IsAntialias = SKPaintExtensions.IsAntialias(svgRectangle);

            TransformedBounds = Path.Bounds;

            Transform = SKMatrixExtensions.ToSKMatrix(svgRectangle.Transforms);

            ClipPath = SvgClipPathExtensions.GetSvgVisualElementClipPath(svgRectangle, TransformedBounds, new HashSet<Uri>(), _disposable);
            MaskDrawable = SvgMaskExtensions.GetSvgVisualElementMask(svgRectangle, TransformedBounds, new HashSet<Uri>(), _disposable);
            if (MaskDrawable != null)
            {
                CreateMaskPaints();
            }
            Opacity = ignoreAttributes.HasFlag(IgnoreAttributes.Opacity) ? null : SKPaintExtensions.GetOpacitySKPaint(svgRectangle, _disposable);
            Filter = ignoreAttributes.HasFlag(IgnoreAttributes.Filter) ? null : SvgFiltersExtensions.GetFilterSKPaint(svgRectangle, TransformedBounds, _disposable);

            if (SKPaintExtensions.IsValidFill(svgRectangle))
            {
                Fill = SKPaintExtensions.GetFillSKPaint(svgRectangle, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            if (SKPaintExtensions.IsValidStroke(svgRectangle, TransformedBounds))
            {
                Stroke = SKPaintExtensions.GetStrokeSKPaint(svgRectangle, TransformedBounds, ignoreAttributes, _disposable);
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
