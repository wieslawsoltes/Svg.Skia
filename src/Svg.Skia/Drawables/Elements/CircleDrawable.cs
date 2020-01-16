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

            Path = SKPathUtil.ToSKPath(svgCircle, svgCircle.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SKPaintUtil.IsAntialias(svgCircle);

            TransformedBounds = Path.Bounds;

            Transform = SKMatrixUtil.GetSKMatrix(svgCircle.Transforms);

            ClipPath = SvgClipPathUtil.GetSvgVisualElementClipPath(svgCircle, TransformedBounds, new HashSet<Uri>(), _disposable);
            MaskDrawable = SvgMaskUtil.GetSvgVisualElementMask(svgCircle, TransformedBounds, new HashSet<Uri>(), _disposable);
            CreateMaskPaints();
            Opacity = ignoreAttributes.HasFlag(IgnoreAttributes.Opacity) ? null : SKPaintUtil.GetOpacitySKPaint(svgCircle, _disposable);
            Filter = ignoreAttributes.HasFlag(IgnoreAttributes.Filter) ? null : SKPaintUtil.GetFilterSKPaint(svgCircle, TransformedBounds, _disposable);

            if (SKPaintUtil.IsValidFill(svgCircle))
            {
                Fill = SKPaintUtil.GetFillSKPaint(svgCircle, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            if (SKPaintUtil.IsValidStroke(svgCircle, TransformedBounds))
            {
                Stroke = SKPaintUtil.GetStrokeSKPaint(svgCircle, TransformedBounds, ignoreAttributes, _disposable);
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
