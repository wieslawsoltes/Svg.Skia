// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class MaskDrawable : DrawableContainer
    {
        public MaskDrawable(SvgMask svgMask, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes = IgnoreAttributes.None)
        {
            IgnoreAttributes = ignoreAttributes;
            IsDrawable = true;

            if (!IsDrawable)
            {
                return;
            }

            var maskUnits = SvgCoordinateUnits.ObjectBoundingBox;
            var maskContentUnits = SvgCoordinateUnits.UserSpaceOnUse;
            var xUnit = new SvgUnit(SvgUnitType.Percentage, -10f);
            var yUnit = new SvgUnit(SvgUnitType.Percentage, -10f);
            var widthUnit = new SvgUnit(SvgUnitType.Percentage, 120f);
            var heightUnit = new SvgUnit(SvgUnitType.Percentage, 120f);

            if (svgMask.CustomAttributes.TryGetValue("maskUnits", out string? maskUnitsString))
            {
                switch (maskUnitsString)
                {
                    case "userSpaceOnUse":
                        maskUnits = SvgCoordinateUnits.UserSpaceOnUse;
                        break;
                    default:
                    case "objectBoundingBox":
                        maskUnits = SvgCoordinateUnits.ObjectBoundingBox;
                        break;
                }
            }

            if (svgMask.CustomAttributes.TryGetValue("maskContentUnits", out string? maskContentUnitsString))
            {
                switch (maskContentUnitsString)
                {
                    default:
                    case "userSpaceOnUse":
                        maskContentUnits = SvgCoordinateUnits.UserSpaceOnUse;
                        break;
                    case "objectBoundingBox":
                        maskContentUnits = SvgCoordinateUnits.ObjectBoundingBox;
                        break;
                }
            }

            if (svgMask.CustomAttributes.TryGetValue("x", out string? xString))
            {
                if (new SvgUnitConverter().ConvertFromString(xString) is SvgUnit _x)
                {
                    xUnit = _x;
                }
            }

            if (svgMask.CustomAttributes.TryGetValue("y", out string? yString))
            {
                if (new SvgUnitConverter().ConvertFromString(yString) is SvgUnit _y)
                {
                    yUnit = _y;
                }
            }

            if (svgMask.CustomAttributes.TryGetValue("width", out string? _widthString))
            {
                if (new SvgUnitConverter().ConvertFromString(_widthString) is SvgUnit _width)
                {
                    widthUnit = _width;
                }
            }

            if (svgMask.CustomAttributes.TryGetValue("height", out string? heightString))
            {
                if (new SvgUnitConverter().ConvertFromString(heightString) is SvgUnit _height)
                {
                    heightUnit = _height;
                }
            }

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

            var skPictureTransform = SKMatrix.MakeIdentity();

            if (maskContentUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skBoundsTranslateTransform = SKMatrix.MakeTranslation(skOwnerBounds.Left, skOwnerBounds.Top);
                SKMatrix.PreConcat(ref skPictureTransform, ref skBoundsTranslateTransform);

                var skBoundsScaleTransform = SKMatrix.MakeScale(skOwnerBounds.Width, skOwnerBounds.Height);
                SKMatrix.PreConcat(ref skPictureTransform, ref skBoundsScaleTransform);
            }

            foreach (var svgElement in svgMask.Children)
            {
                var drawable = DrawableFactory.Create(svgElement, skOwnerBounds, ignoreAttributes);
                if (drawable != null)
                {
                    ChildrenDrawables.Add(drawable);
                    _disposable.Add(drawable);
                }
            }

            Clip = skRectTransformed;

            IsAntialias = SKPaintUtil.IsAntialias(svgMask);

            TransformedBounds = skRectTransformed;

            Transform = skPictureTransform;

            ClipPath = null;
            MaskDrawable = SvgMaskUtil.GetSvgVisualElementMask(svgMask, TransformedBounds, new HashSet<Uri>(), _disposable);
            CreateMaskPaints();
            Opacity = null;
            Filter = null;

            Fill = null;
            Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }
    }
}
