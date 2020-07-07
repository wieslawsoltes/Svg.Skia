﻿using System;
using System.Collections.Generic;
#if !USE_PICTURE
using SkiaSharp;
using CropRect = SkiaSharp.SKImageFilter.CropRect;
#endif
#if USE_PICTURE
using SKMatrix = Svg.Picture.Matrix;
using SKRect = Svg.Picture.Rect;
#endif

namespace Svg.Skia
{
    internal sealed class MaskDrawable : DrawableContainer
    {
        private MaskDrawable()
            : base()
        {
        }

        public static MaskDrawable Create(SvgMask svgMask, SKRect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new MaskDrawable
            {
                Element = svgMask,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes,
                IsDrawable = true
            };

            if (!drawable.IsDrawable)
            {
                return drawable;
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
                drawable.IsDrawable = false;
                return drawable;
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

            var skMatrix = SKMatrix.CreateIdentity();

            if (maskContentUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skBoundsTranslateTransform = SKMatrix.CreateTranslation(skOwnerBounds.Left, skOwnerBounds.Top);
                skMatrix = skMatrix.PreConcat(skBoundsTranslateTransform);

                var skBoundsScaleTransform = SKMatrix.CreateScale(skOwnerBounds.Width, skOwnerBounds.Height);
                skMatrix = skMatrix.PreConcat(skBoundsScaleTransform);
            }

            drawable.CreateChildren(svgMask, skOwnerBounds, drawable, ignoreAttributes);

            drawable.Overflow = skRectTransformed;

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgMask);

            drawable.TransformedBounds = skRectTransformed;

            drawable.Transform = skMatrix;

            drawable.Fill = null;
            drawable.Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            return drawable;
        }

        public override void PostProcess()
        {
            var element = Element;
            if (element is null)
            {
                return;
            }

            var enableMask = !IgnoreAttributes.HasFlag(Attributes.Mask);

            ClipPath = null;

            if (enableMask)
            {
                MaskDrawable = SvgExtensions.GetSvgElementMask(element, TransformedBounds, new HashSet<Uri>(), Disposable);
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
