// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using SkiaSharp;

namespace Svg.Skia
{
    public class AnchorDrawable : DrawableContainer
    {
        public AnchorDrawable(SvgAnchor svgAnchor, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes = IgnoreAttributes.None)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = true;

            if (!IsDrawable)
            {
                return;
            }

            CreateChildren(svgAnchor, skOwnerBounds, ignoreAttributes);

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgAnchor);

            TransformedBounds = SKRect.Empty;

            CreateTransformedBounds();

            Transform = SvgTransformsExtensions.ToSKMatrix(svgAnchor.Transforms);

            ClipPath = null;
            MaskDrawable = null;
            Opacity = ignoreAttributes.HasFlag(IgnoreAttributes.Opacity) ? null : SvgPaintingExtensions.GetOpacitySKPaint(svgAnchor, _disposable);
            Filter = null;

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
