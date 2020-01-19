// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class PathDrawable : DrawablePath
    {
        public PathDrawable(SvgPath svgPath, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes = IgnoreAttributes.None)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgPath, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            Path = svgPath.PathData?.ToSKPath(svgPath.FillRule, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgPath);

            TransformedBounds = Path.Bounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgPath.Transforms);

            ClipPath = SvgClippingExtensions.GetSvgVisualElementClipPath(svgPath, TransformedBounds, new HashSet<Uri>(), _disposable);
            MaskDrawable = SvgClippingExtensions.GetSvgVisualElementMask(svgPath, TransformedBounds, new HashSet<Uri>(), _disposable);
            if (MaskDrawable != null)
            {
                CreateMaskPaints();
            }
            Opacity = ignoreAttributes.HasFlag(IgnoreAttributes.Opacity) ? null : SvgPaintingExtensions.GetOpacitySKPaint(svgPath, _disposable);
            Filter = ignoreAttributes.HasFlag(IgnoreAttributes.Filter) ? null : SvgFiltersExtensions.GetFilterSKPaint(svgPath, TransformedBounds, _disposable);

            if (SvgPaintingExtensions.IsValidFill(svgPath))
            {
                Fill = SvgPaintingExtensions.GetFillSKPaint(svgPath, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgPath, TransformedBounds))
            {
                Stroke = SvgPaintingExtensions.GetStrokeSKPaint(svgPath, TransformedBounds, ignoreAttributes, _disposable);
                if (Stroke == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            CreateMarkers(svgPath, Path, skOwnerBounds);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
