// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class PolygonDrawable : DrawablePath
    {
        public PolygonDrawable(SvgPolygon svgPolygon, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(root, parent)
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

            SvgMarkerExtensions.CreateMarkers(svgPolygon, Path, skOwnerBounds, ref MarkerDrawables, _disposable);

            ClipPath = IgnoreAttributes.HasFlag(Attributes.ClipPath) ? null : SvgClippingExtensions.GetSvgVisualElementClipPath(svgPolygon, TransformedBounds, new HashSet<Uri>(), _disposable);
            MaskDrawable = IgnoreAttributes.HasFlag(Attributes.Mask) ? null : SvgClippingExtensions.GetSvgVisualElementMask(svgPolygon, TransformedBounds, new HashSet<Uri>(), _disposable);
            if (MaskDrawable != null)
            {
                CreateMaskPaints();
            }
            Opacity = IgnoreAttributes.HasFlag(Attributes.Opacity) ? null : SvgPaintingExtensions.GetOpacitySKPaint(svgPolygon, _disposable);
            Filter = IgnoreAttributes.HasFlag(Attributes.Filter) ? null : SvgFiltersExtensions.GetFilterSKPaint(svgPolygon, TransformedBounds, this, _disposable);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
