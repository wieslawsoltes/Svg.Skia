// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class PolygonDrawable : DrawablePath
    {
        public PolygonDrawable(SvgPolygon svgPolygon, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes = IgnoreAttributes.None)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgPolygon, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            Path = svgPolygon.Points?.ToSKPath(svgPolygon.FillRule, true, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgPolygon);

            TransformedBounds = Path.Bounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgPolygon.Transforms);

            ClipPath = SvgClippingExtensions.GetSvgVisualElementClipPath(svgPolygon, TransformedBounds, new HashSet<Uri>(), _disposable);
            MaskDrawable = SvgClippingExtensions.GetSvgVisualElementMask(svgPolygon, TransformedBounds, new HashSet<Uri>(), _disposable);
            if (MaskDrawable != null)
            {
                CreateMaskPaints();
            }
            Opacity = ignoreAttributes.HasFlag(IgnoreAttributes.Opacity) ? null : SvgPaintingExtensions.GetOpacitySKPaint(svgPolygon, _disposable);
            Filter = ignoreAttributes.HasFlag(IgnoreAttributes.Filter) ? null : SvgFiltersExtensions.GetFilterSKPaint(svgPolygon, TransformedBounds, _disposable);

            if (SvgPaintingExtensions.IsValidFill(svgPolygon))
            {
                Fill = SvgPaintingExtensions.GetFillSKPaint(svgPolygon, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgPolygon, TransformedBounds))
            {
                Stroke = SvgPaintingExtensions.GetStrokeSKPaint(svgPolygon, TransformedBounds, ignoreAttributes, _disposable);
                if (Stroke == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            CreateMarkers(svgPolygon, Path, skOwnerBounds);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
