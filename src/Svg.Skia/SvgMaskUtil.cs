// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public static class SvgMaskUtil
    {
        public static SKPicture RecordPicture(SvgElementCollection svgElementCollection, SKRect skRectTransformed, SKRect skBounds, SKMatrix skMatrix)
        {
            var ignoreAttributes = IgnoreAttributes.None; // IgnoreAttributes.Opacity | IgnoreAttributes.Display | IgnoreAttributes.Filter;

            using var skPictureRecorder = new SKPictureRecorder();
            using var skCanvas = skPictureRecorder.BeginRecording(skRectTransformed);

            skCanvas.Clear(new SKColor(0, 0, 0, 0));
            skCanvas.ClipRect(skRectTransformed, SKClipOperation.Intersect);
            skCanvas.SetMatrix(skMatrix);

            foreach (var svgElement in svgElementCollection)
            {
                using var drawable = DrawableFactory.Create(svgElement, skRectTransformed, ignoreAttributes);
                drawable?.Draw(skCanvas, 0f, 0f);
            }

            return skPictureRecorder.EndRecording();
        }

        public static SKPicture? GetMask(SvgMask svgMask, SKRect skBounds, CompositeDisposable disposable)
        {
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

            float x = xUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgMask, skBounds);
            float y = yUnit.ToDeviceValue(UnitRenderingType.Vertical, svgMask, skBounds);
            float width = widthUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgMask, skBounds);
            float height = heightUnit.ToDeviceValue(UnitRenderingType.Vertical, svgMask, skBounds);

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            if (maskUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                if (xUnit.Type != SvgUnitType.Percentage)
                {
                    x *= skBounds.Width;
                }

                if (yUnit.Type != SvgUnitType.Percentage)
                {
                    y *= skBounds.Height;
                }

                if (widthUnit.Type != SvgUnitType.Percentage)
                {
                    width *= skBounds.Width;
                }

                if (heightUnit.Type != SvgUnitType.Percentage)
                {
                    height *= skBounds.Height;
                }

                x += skBounds.Left;
                y += skBounds.Top;
            }

            SKRect skRectTransformed = SKRect.Create(x, y, width, height);

            var skPictureTransform = SKMatrix.MakeIdentity();

            if (maskContentUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skBoundsTranslateTransform = SKMatrix.MakeTranslation(skBounds.Left, skBounds.Top);
                SKMatrix.PreConcat(ref skPictureTransform, ref skBoundsTranslateTransform);

                var skBoundsScaleTransform = SKMatrix.MakeScale(skBounds.Width, skBounds.Height);
                SKMatrix.PreConcat(ref skPictureTransform, ref skBoundsScaleTransform);
            }

            var skPicture = RecordPicture(svgMask.Children, skRectTransformed, skBounds, skPictureTransform);
            disposable.Add(skPicture);

            return skPicture;
        }

        public static SKPicture? GetSvgVisualElementMask(SvgVisualElement svgVisualElement, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable)
        {
            var svgMaskRef = svgVisualElement.GetUriElementReference<SvgMask>("mask", uris);
            if (svgMaskRef == null || svgMaskRef.Children == null)
            {
                return null;
            }

            // TODO: Handle mask set on mask.

            return GetMask(svgMaskRef, skBounds, disposable);
        }
    }
}
