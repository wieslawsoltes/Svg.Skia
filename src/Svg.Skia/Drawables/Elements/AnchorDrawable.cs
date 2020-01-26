// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using SkiaSharp;

namespace Svg.Skia
{
    public class AnchorDrawable : DrawableContainer
    {
        public AnchorDrawable(SvgAnchor svgAnchor, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgAnchor, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = true;

            if (!IsDrawable)
            {
                return;
            }

            CreateChildren(svgAnchor, skOwnerBounds, root, this, ignoreAttributes);

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgAnchor);

            TransformedBounds = SKRect.Empty;

            CreateTransformedBounds();

            Transform = SvgTransformsExtensions.ToSKMatrix(svgAnchor.Transforms);

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);

            ClipPath = null;
            MaskDrawable = null;
            Opacity = IgnoreAttributes.HasFlag(Attributes.Opacity) ? null : SvgPaintingExtensions.GetOpacitySKPaint(svgAnchor, _disposable);
            Filter = null;
        }

        public override void PostProcess()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            var enableOpacity = !IgnoreAttributes.HasFlag(Attributes.Opacity);

            ClipPath = null;
            MaskDrawable = null;

            if (enableOpacity == true)
            {
                Opacity = SvgPaintingExtensions.GetOpacitySKPaint(element, _disposable);
            }
            else
            {
                Opacity = null;
            }

            Filter = null;

            foreach (var child in ChildrenDrawables)
            {
                child.PostProcess();
            }
        }
    }
}
