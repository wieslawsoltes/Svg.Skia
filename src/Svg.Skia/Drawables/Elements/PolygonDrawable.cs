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

            Path = SKPathUtil.ToSKPath(svgPolygon.Points, svgPolygon.FillRule, true, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SKPaintUtil.IsAntialias(svgPolygon);

            TransformedBounds = Path.Bounds;

            Transform = SKMatrixUtil.GetSKMatrix(svgPolygon.Transforms);

            ClipPath = SvgClipPathUtil.GetSvgVisualElementClipPath(svgPolygon, TransformedBounds, new HashSet<Uri>(), _disposable);
            MaskDrawable = SvgMaskUtil.GetSvgVisualElementMask(svgPolygon, TransformedBounds, new HashSet<Uri>(), _disposable);
            CreateMaskPaints();
            Opacity = ignoreAttributes.HasFlag(IgnoreAttributes.Opacity) ? null : SKPaintUtil.GetOpacitySKPaint(svgPolygon, _disposable);
            Filter = ignoreAttributes.HasFlag(IgnoreAttributes.Filter) ? null : SKPaintUtil.GetFilterSKPaint(svgPolygon, TransformedBounds, _disposable);

            if (SKPaintUtil.IsValidFill(svgPolygon))
            {
                Fill = SKPaintUtil.GetFillSKPaint(svgPolygon, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            if (SKPaintUtil.IsValidStroke(svgPolygon, TransformedBounds))
            {
                Stroke = SKPaintUtil.GetStrokeSKPaint(svgPolygon, TransformedBounds, ignoreAttributes, _disposable);
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
