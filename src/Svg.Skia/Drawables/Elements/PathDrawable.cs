// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class PathDrawable : DrawablePath
    {
        public PathDrawable(SvgPath svgPath, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = CanDraw(svgPath, IgnoreDisplay);

            if (!IsDrawable)
            {
                return;
            }

            Path = SKUtil.ToSKPath(svgPath.PathData, svgPath.FillRule, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SKUtil.IsAntialias(svgPath);

            TransformedBounds = Path.Bounds;

            Transform = SKUtil.GetSKMatrix(svgPath.Transforms);

            PathClip = SKUtil.GetSvgVisualElementClipPath(svgPath, TransformedBounds, new HashSet<Uri>(), _disposable);
            PaintOpacity = SKUtil.GetOpacitySKPaint(svgPath, _disposable);
            PaintFilter = SKUtil.GetFilterSKPaint(svgPath, _disposable);

            if (SKUtil.IsValidFill(svgPath))
            {
                PaintFill = SKUtil.GetFillSKPaint(svgPath, TransformedBounds, _disposable);
                if (PaintFill == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            if (SKUtil.IsValidStroke(svgPath, TransformedBounds))
            {
                PaintStroke = SKUtil.GetStrokeSKPaint(svgPath, TransformedBounds, _disposable);
                if (PaintStroke == null)
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
