// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class MaskDrawable : DrawableContainer
    {
        public MaskDrawable(SvgMask svgMask, SKRect skOwnerBounds, Drawable? root, Drawable? parent, Attributes ignoreAttributes = Attributes.None)
            : base(svgMask, root, parent)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = true;

            if (!IsDrawable)
            {
                return;
            }
            var maskUnits = svgMask.MaskUnits;
            var maskContentUnits = svgMask.MaskContentUnits;
            var xUnit = svgMask.X;
            var yUnit = svgMask.Y;
            var widthUnit = svgMask.Width;
            var heightUnit = svgMask.Height;
            float x = xUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgMask, skOwnerBounds);
            float y = yUnit.ToDeviceValue(UnitRenderingType.Vertical, svgMask, skOwnerBounds);
            float width = widthUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgMask, skOwnerBounds);
            float height = heightUnit.ToDeviceValue(UnitRenderingType.Vertical, svgMask, skOwnerBounds);

            if (width <= 0 || height <= 0)
            {
                IsDrawable = false;
                return;
            }

            if (maskUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                if (xUnit.Type != SvgUnitType.Percentage)
                {
                    x *= skOwnerBounds.Width;
                }

                if (yUnit.Type != SvgUnitType.Percentage)
                {
                    y *= skOwnerBounds.Height;
                }

                if (widthUnit.Type != SvgUnitType.Percentage)
                {
                    width *= skOwnerBounds.Width;
                }

                if (heightUnit.Type != SvgUnitType.Percentage)
                {
                    height *= skOwnerBounds.Height;
                }

                x += skOwnerBounds.Left;
                y += skOwnerBounds.Top;
            }

            SKRect skRectTransformed = SKRect.Create(x, y, width, height);

            var skMatrix = SKMatrix.MakeIdentity();

            if (maskContentUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skBoundsTranslateTransform = SKMatrix.MakeTranslation(skOwnerBounds.Left, skOwnerBounds.Top);
                SKMatrix.PreConcat(ref skMatrix, ref skBoundsTranslateTransform);

                var skBoundsScaleTransform = SKMatrix.MakeScale(skOwnerBounds.Width, skOwnerBounds.Height);
                SKMatrix.PreConcat(ref skMatrix, ref skBoundsScaleTransform);
            }

            CreateChildren(svgMask, skOwnerBounds, root, this, ignoreAttributes);

            Overflow = skRectTransformed;

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgMask);

            TransformedBounds = skRectTransformed;

            Transform = skMatrix;

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }

        public override void PostProcess()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            var enableMask = !IgnoreAttributes.HasFlag(Attributes.Mask);

            ClipPath = null;

            if (enableMask == true)
            {
                MaskDrawable = SvgClippingExtensions.GetSvgElementMask(element, TransformedBounds, new HashSet<Uri>(), _disposable);
                if (MaskDrawable != null)
                {
                    CreateMaskPaints();
                }
            }
            else
            {
                MaskDrawable = null;
            }

            Opacity = null;
            Filter = null;
        }

    }
}
