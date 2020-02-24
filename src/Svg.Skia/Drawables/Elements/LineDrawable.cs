// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class LineDrawable : DrawablePath
    {
        public LineDrawable(SvgLine svgLine, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgLine, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = CanDraw(svgLine, IgnoreAttributes) && HasFeatures(svgLine, IgnoreAttributes);

            if (!IsDrawable)
            {
                return;
            }

            Path = svgLine.ToSKPath(svgLine.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgLine);

            TransformedBounds = Path.Bounds;

            Transform = SvgTransformsExtensions.ToSKMatrix(svgLine.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgPaintingExtensions.IsValidFill(svgLine))
            {
                Fill = SvgPaintingExtensions.GetFillSKPaint(svgLine, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgLine, TransformedBounds))
            {
                Stroke = SvgPaintingExtensions.GetStrokeSKPaint(svgLine, TransformedBounds, ignoreAttributes, _disposable);
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

            SvgMarkerExtensions.CreateMarkers(svgLine, Path, skOwnerBounds, ref MarkerDrawables, _disposable);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
