// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class MaskDrawable : DrawableContainer
    {
        public static SvgCoordinateUnits GetMaskUnits(SvgMask svgMask)
        {
            svgMask.CustomAttributes.TryGetValue("maskUnits", out string? maskUnitsString);
            return maskUnitsString switch
            {
                "userSpaceOnUse" => SvgCoordinateUnits.UserSpaceOnUse,
                "objectBoundingBox" => SvgCoordinateUnits.ObjectBoundingBox,
                _ => SvgCoordinateUnits.ObjectBoundingBox,
            };
        }

        public static SvgCoordinateUnits GetMaskContentUnits(SvgMask svgMask)
        {
            svgMask.CustomAttributes.TryGetValue("maskContentUnits", out string? maskContentUnitsString);
            return maskContentUnitsString switch
            {
                "userSpaceOnUse" => SvgCoordinateUnits.UserSpaceOnUse,
                "objectBoundingBox" => SvgCoordinateUnits.ObjectBoundingBox,
                _ => SvgCoordinateUnits.UserSpaceOnUse,
            };
        }

        public static SvgUnit GetX(SvgMask svgMask)
        {
            if (svgMask.CustomAttributes.TryGetValue("x", out string? xString))
            {
                if (new SvgUnitConverter().ConvertFromString(xString) is SvgUnit _x)
                {
                    return _x;
                }
            }
            return new SvgUnit(SvgUnitType.Percentage, -10f);
        }

        public static SvgUnit GetY(SvgMask svgMask)
        {
            if (svgMask.CustomAttributes.TryGetValue("y", out string? yString))
            {
                if (new SvgUnitConverter().ConvertFromString(yString) is SvgUnit y)
                {
                    return y;
                }
            }
            return new SvgUnit(SvgUnitType.Percentage, -10f);
        }

        public static SvgUnit GetWidth(SvgMask svgMask)
        {
            if (svgMask.CustomAttributes.TryGetValue("width", out string? _widthString))
            {
                if (new SvgUnitConverter().ConvertFromString(_widthString) is SvgUnit width)
                {
                    return width;
                }
            }
            return new SvgUnit(SvgUnitType.Percentage, 120f);
        }

        public static SvgUnit GetHeight(SvgMask svgMask)
        {
            if (svgMask.CustomAttributes.TryGetValue("height", out string? heightString))
            {
                if (new SvgUnitConverter().ConvertFromString(heightString) is SvgUnit height)
                {
                    return height;
                }
            }
            return new SvgUnit(SvgUnitType.Percentage, 120f);
        }

        public MaskDrawable(SvgMask svgMask, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes = IgnoreAttributes.None)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = true;

            if (!IsDrawable)
            {
                return;
            }

            var maskUnits = GetMaskUnits(svgMask);
            var maskContentUnits = GetMaskContentUnits(svgMask);
            var xUnit = GetX(svgMask);
            var yUnit = GetY(svgMask);
            var widthUnit = GetWidth(svgMask);
            var heightUnit = GetHeight(svgMask);

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

            CreateChildren(svgMask, skOwnerBounds, ignoreAttributes);

            Clip = skRectTransformed;

            IsAntialias = SvgPaintingExtensions.IsAntialias(svgMask);

            TransformedBounds = skRectTransformed;

            Transform = skMatrix;

            ClipPath = null;
            MaskDrawable = IgnoreAttributes.HasFlag(IgnoreAttributes.Mask) ? null : SvgClippingExtensions.GetSvgVisualElementMask(svgMask, TransformedBounds, new HashSet<Uri>(), _disposable);
            if (MaskDrawable != null)
            {
                CreateMaskPaints();
            }
            Opacity = null;
            Filter = null;

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
