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

            Path = SKPathUtil.ToSKPath(svgPath.PathData, svgPath.FillRule, _disposable);
            if (Path == null || Path.IsEmpty)
            {
                IsDrawable = false;
                return;
            }

            IsAntialias = SKPaintUtil.IsAntialias(svgPath);

            TransformedBounds = Path.Bounds;

            Transform = SKMatrixUtil.GetSKMatrix(svgPath.Transforms);

            ClipPath = SvgClipPathUtil.GetSvgVisualElementClipPath(svgPath, TransformedBounds, new HashSet<Uri>(), _disposable);
            MaskDrawable = SvgMaskUtil.GetSvgVisualElementMask(svgPath, TransformedBounds, new HashSet<Uri>(), _disposable);
            CreateMaskPaints();
            Opacity = ignoreAttributes.HasFlag(IgnoreAttributes.Opacity) ? null : SKPaintUtil.GetOpacitySKPaint(svgPath, _disposable);
            Filter = ignoreAttributes.HasFlag(IgnoreAttributes.Filter) ? null : SKPaintUtil.GetFilterSKPaint(svgPath, TransformedBounds, _disposable);

            if (SKPaintUtil.IsValidFill(svgPath))
            {
                Fill = SKPaintUtil.GetFillSKPaint(svgPath, TransformedBounds, ignoreAttributes, _disposable);
                if (Fill == null)
                {
                    IsDrawable = false;
                    return;
                }
            }

            if (SKPaintUtil.IsValidStroke(svgPath, TransformedBounds))
            {
                Stroke = SKPaintUtil.GetStrokeSKPaint(svgPath, TransformedBounds, ignoreAttributes, _disposable);
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
