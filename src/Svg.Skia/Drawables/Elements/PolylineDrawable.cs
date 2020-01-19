// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class PolylineDrawable : DrawablePath
    {
        public PolylineDrawable(SvgPolyline svgPolyline, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes = IgnoreAttributes.None)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgPolyline, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            Path = svgPolyline.Points?.ToSKPath(svgPolyline.FillRule, false, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SKPaintExtensions.IsAntialias(svgPolyline);

            TransformedBounds = Path.Bounds;

            Transform = SKMatrixExtensions.ToSKMatrix(svgPolyline.Transforms);

            ClipPath = SvgClipPathExtensions.GetSvgVisualElementClipPath(svgPolyline, TransformedBounds, new HashSet<Uri>(), _disposable);
            MaskDrawable = SvgMaskExtensions.GetSvgVisualElementMask(svgPolyline, TransformedBounds, new HashSet<Uri>(), _disposable);
            if (MaskDrawable != null)
            {
                CreateMaskPaints();
            }
            Opacity = ignoreAttributes.HasFlag(IgnoreAttributes.Opacity) ? null : SKPaintExtensions.GetOpacitySKPaint(svgPolyline, _disposable);
            Filter = ignoreAttributes.HasFlag(IgnoreAttributes.Filter) ? null : SvgFiltersExtensions.GetFilterSKPaint(svgPolyline, TransformedBounds, _disposable);

            if (SKPaintExtensions.IsValidFill(svgPolyline))
            {
                Fill = SKPaintExtensions.GetFillSKPaint(svgPolyline, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            if (SKPaintExtensions.IsValidStroke(svgPolyline, TransformedBounds))
            {
                Stroke = SKPaintExtensions.GetStrokeSKPaint(svgPolyline, TransformedBounds, ignoreAttributes, _disposable);
                if (Stroke == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            CreateMarkers(svgPolyline, Path, skOwnerBounds);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
