// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    internal class PolylineDrawable : DrawablePath
    {
        public PolylineDrawable(SvgPolyline svgPolyline, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = CanDraw(svgPolyline, IgnoreDisplay);

            if (!IsDrawable)
            {
                return;
            }

            Path = SKUtil.ToSKPath(svgPolyline.Points, svgPolyline.FillRule, false, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SKUtil.IsAntialias(svgPolyline);

            TransformedBounds = Path.Bounds;

            Transform = SKUtil.GetSKMatrix(svgPolyline.Transforms);

            PathClip = SKUtil.GetSvgVisualElementClipPath(svgPolyline, TransformedBounds, new HashSet<Uri>(), _disposable);
            PaintOpacity = SKUtil.GetOpacitySKPaint(svgPolyline, _disposable);
            PaintFilter = SKUtil.GetFilterSKPaint(svgPolyline, _disposable);

            if (SKUtil.IsValidFill(svgPolyline))
            {
                PaintFill = SKUtil.GetFillSKPaint(svgPolyline, TransformedBounds, _disposable);
            }

            if (SKUtil.IsValidStroke(svgPolyline, TransformedBounds))
            {
                PaintStroke = SKUtil.GetStrokeSKPaint(svgPolyline, TransformedBounds, _disposable);
            }

            CreateMarkers(svgPolyline, Path, skOwnerBounds);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
