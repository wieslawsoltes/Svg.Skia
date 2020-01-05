// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using SkiaSharp;

namespace Svg.Skia
{
#if USE_SVG_ANCHOR
    internal class AnchorDrawable : DrawableContainer
    {
        public AnchorDrawable(SvgAnchor svgAnchor, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = true;

            if (!IsDrawable)
            {
                return;
            }

            foreach (var svgElement in svgAnchor.Children)
            {
                var drawable = DrawableFactory.Create(svgElement, skOwnerBounds, ignoreDisplay);
                if (drawable != null)
                {
                    ChildrenDrawables.Add(drawable);
                    _disposable.Add(drawable);
                }
            }

            IsAntialias = SkiaUtil.IsAntialias(svgAnchor);

            TransformedBounds = SKRect.Empty;

            foreach (var drawable in ChildrenDrawables)
            {
                if (TransformedBounds.IsEmpty)
                {
                    TransformedBounds = drawable.TransformedBounds;
                }
                else
                {
                    if (!drawable.TransformedBounds.IsEmpty)
                    {
                        TransformedBounds = SKRect.Union(TransformedBounds, drawable.TransformedBounds);
                    }
                }
            }

            Transform = SkiaUtil.GetSKMatrix(svgAnchor.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);

            PathClip = null;
            PaintOpacity = SkiaUtil.GetOpacitySKPaint(svgAnchor, _disposable);
            PaintFilter = null;

            PaintFill = null;
            PaintStroke = null;
        }
    }
#endif
}
