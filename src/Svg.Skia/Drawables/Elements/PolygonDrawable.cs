// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class PolygonDrawable : DrawablePath
    {
        public PolygonDrawable(SvgPolygon svgPolygon, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = CanDraw(svgPolygon, IgnoreDisplay);

            if (!IsDrawable)
            {
                return;
            }

            Path = SKUtil.ToSKPath(svgPolygon.Points, svgPolygon.FillRule, true, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SKUtil.IsAntialias(svgPolygon);

            TransformedBounds = Path.Bounds;

            Transform = SKUtil.GetSKMatrix(svgPolygon.Transforms);

            PathClip = SKUtil.GetSvgVisualElementClipPath(svgPolygon, TransformedBounds, new HashSet<Uri>(), _disposable);
            PaintOpacity = SKUtil.GetOpacitySKPaint(svgPolygon, _disposable);
            PaintFilter = SKUtil.GetFilterSKPaint(svgPolygon, _disposable);

            if (SKUtil.IsValidFill(svgPolygon))
            {
                PaintFill = SKUtil.GetFillSKPaint(svgPolygon, TransformedBounds, _disposable);
                if (PaintFill == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            if (SKUtil.IsValidStroke(svgPolygon, TransformedBounds))
            {
                PaintStroke = SKUtil.GetStrokeSKPaint(svgPolygon, TransformedBounds, _disposable);
                if (PaintStroke == null)
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
