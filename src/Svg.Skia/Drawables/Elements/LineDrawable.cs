// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class LineDrawable : DrawablePath
    {
        public LineDrawable(SvgLine svgLine, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = CanDraw(svgLine, IgnoreDisplay);

            if (!IsDrawable)
            {
                return;
            }

            Path = SKUtil.ToSKPath(svgLine, svgLine.FillRule, skOwnerBounds, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SKUtil.IsAntialias(svgLine);

            TransformedBounds = Path.Bounds;

            Transform = SKUtil.GetSKMatrix(svgLine.Transforms);

            PathClip = SKUtil.GetSvgVisualElementClipPath(svgLine, TransformedBounds, new HashSet<Uri>(), _disposable);
            PaintOpacity = SKUtil.GetOpacitySKPaint(svgLine, _disposable);
            PaintFilter = SKUtil.GetFilterSKPaint(svgLine, _disposable);

            if (SKUtil.IsValidFill(svgLine))
            {
                PaintFill = SKUtil.GetFillSKPaint(svgLine, TransformedBounds, _disposable);
                if (PaintFill == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            if (SKUtil.IsValidStroke(svgLine, TransformedBounds))
            {
                PaintStroke = SKUtil.GetStrokeSKPaint(svgLine, TransformedBounds, _disposable);
                if (PaintStroke == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            CreateMarkers(svgLine, Path, skOwnerBounds);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
