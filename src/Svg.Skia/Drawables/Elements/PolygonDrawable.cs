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
            : base(svgPolygon, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgPolygon, IgnoreAttributes) && HasFeatures(svgPolygon, IgnoreAttributes);

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

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgPaintingExtensions.IsValidFill(svgPolygon))
            {
                Fill = SvgPaintingExtensions.GetFillSKPaint(svgPolygon, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgPolygon, TransformedBounds))
            {
                Stroke = SvgPaintingExtensions.GetStrokeSKPaint(svgPolygon, TransformedBounds, ignoreAttributes, _disposable);
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

            SvgMarkerExtensions.CreateMarkers(svgPolygon, Path, skOwnerBounds, ref MarkerDrawables, _disposable);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
