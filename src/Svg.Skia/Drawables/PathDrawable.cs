// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    internal class PathDrawable : DrawablePath
    {
        public PathDrawable(SvgPath svgPath, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = CanDraw(svgPath, IgnoreDisplay);

            if (!IsDrawable)
            {
                return;
            }

            Path = SkiaUtil.ToSKPath(svgPath.PathData, svgPath.FillRule, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SkiaUtil.IsAntialias(svgPath);

            TransformedBounds = Path.Bounds;

            Transform = SkiaUtil.GetSKMatrix(svgPath.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);

            PathClip = SkiaUtil.GetSvgVisualElementClipPath(svgPath, TransformedBounds, new HashSet<Uri>(), _disposable);
            PaintOpacity = SkiaUtil.GetOpacitySKPaint(svgPath, _disposable);
            PaintFilter = SkiaUtil.GetFilterSKPaint(svgPath, _disposable);

            if (SkiaUtil.IsValidFill(svgPath))
            {
                PaintFill = SkiaUtil.GetFillSKPaint(svgPath, TransformedBounds, _disposable);
            }

            if (SkiaUtil.IsValidStroke(svgPath, TransformedBounds))
            {
                PaintStroke = SkiaUtil.GetStrokeSKPaint(svgPath, TransformedBounds, _disposable);
            }

            CreateMarkers(svgPath, Path, skOwnerBounds);
        }
    }
}
