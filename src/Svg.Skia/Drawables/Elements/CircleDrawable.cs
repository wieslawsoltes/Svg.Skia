// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class CircleDrawable : DrawablePath
    {
        public CircleDrawable(SvgCircle svgCircle, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes = IgnoreAttributes.None)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgCircle, IgnoreAttributes);

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

            IsAntialias = SKPaintExtensions.IsAntialias(svgCircle);

            TransformedBounds = Path.Bounds;

            Transform = SKMatrixExtensions.ToSKMatrix(svgCircle.Transforms);

            ClipPath = SvgClipPathExtensions.GetSvgVisualElementClipPath(svgCircle, TransformedBounds, new HashSet<Uri>(), _disposable);
            MaskDrawable = SvgMaskExtensions.GetSvgVisualElementMask(svgCircle, TransformedBounds, new HashSet<Uri>(), _disposable);
            if (MaskDrawable != null)
            {
                CreateMaskPaints();
            }
            Opacity = ignoreAttributes.HasFlag(IgnoreAttributes.Opacity) ? null : SKPaintExtensions.GetOpacitySKPaint(svgCircle, _disposable);
            Filter = ignoreAttributes.HasFlag(IgnoreAttributes.Filter) ? null : SvgFiltersExtensions.GetFilterSKPaint(svgCircle, TransformedBounds, _disposable);

            if (SKPaintExtensions.IsValidFill(svgCircle))
            {
                Fill = SKPaintExtensions.GetFillSKPaint(svgCircle, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            if (SKPaintExtensions.IsValidStroke(svgCircle, TransformedBounds))
            {
                Stroke = SKPaintExtensions.GetStrokeSKPaint(svgCircle, TransformedBounds, ignoreAttributes, _disposable);
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
