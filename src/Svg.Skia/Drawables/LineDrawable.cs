// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    internal class LineDrawable : DrawablePath
    {
        public LineDrawable(SvgLine svgLine, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = CanDraw(svgLine, IgnoreDisplay);

            if (!IsDrawable)
            {
                return;
            }

            Path = SkiaUtil.ToSKPath(svgLine, svgLine.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SkiaUtil.IsAntialias(svgLine);

            TransformedBounds = Path.Bounds;

            Transform = SkiaUtil.GetSKMatrix(svgLine.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);

            PathClip = SkiaUtil.GetSvgVisualElementClipPath(svgLine, TransformedBounds, new HashSet<Uri>(), _disposable);
            PaintOpacity = SkiaUtil.GetOpacitySKPaint(svgLine, _disposable);
            PaintFilter = SkiaUtil.GetFilterSKPaint(svgLine, _disposable);

            if (SkiaUtil.IsValidFill(svgLine))
            {
                PaintFill = SkiaUtil.GetFillSKPaint(svgLine, TransformedBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgLine, TransformedBounds))
            {
                PaintStroke = SkiaUtil.GetStrokeSKPaint(svgLine, TransformedBounds, _disposable);
            }

            CreateMarkers(svgLine, Path, skOwnerBounds);
        }
    }
}
