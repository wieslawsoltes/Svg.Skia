// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using SkiaSharp;

namespace Svg.Skia
{
    internal static class SvgExtensions
    {
        /// <summary>
        /// Converts the current unit to one that can be used at render time.
        /// </summary>
        /// <returns>The representation of the current unit in a device value (usually pixels).</returns>
        public static float ToDeviceValue(this SvgUnit svgUnit, UnitRenderingType renderType, SvgElement? owner, SKRect skBounds)
        {
            /*
            // If it's already been calculated
            if (this._deviceValue.HasValue)
            {
                return this._deviceValue.Value;
            }

            if (this._value == 0.0f)
            {
                this._deviceValue = 0.0f;
                return this._deviceValue.Value;
            }
            */
            // http://www.w3.org/TR/CSS21/syndata.html#values
            // http://www.w3.org/TR/SVG11/coords.html#Units

            const float cmInInch = 2.54f;
            int ppi = SvgDocument.PointsPerInch;

            var type = svgUnit.Type;
            var value = svgUnit.Value;


            float? _deviceValue = null;
            if (value == 0.0f)
            {
                _deviceValue = 0.0f;
                return _deviceValue.Value;
            }

            float points;

            switch (type)
            {
                case SvgUnitType.Em:
                    points = (float)(value * 9);
                    _deviceValue = (points / 72.0f) * ppi;
                    // TODO: Implement GetFont for Skia.
                    //using (var currFont = GetFont(renderer, owner))
                    //{
                    //    if (currFont == null)
                    //    {
                    //        points = (float)(value * 9);
                    //        _deviceValue = (points / 72.0f) * ppi;
                    //    }
                    //    else
                    //    {
                    //        _deviceValue = value * (currFont.SizeInPoints / 72.0f) * ppi;
                    //    }
                    //}
                    break;
                case SvgUnitType.Ex:
                    points = (float)(value * 9);
                    _deviceValue = (points * 0.5f / 72.0f) * ppi;
                    // TODO: Implement GetFont for Skia.
                    //using (var currFont = GetFont(renderer, owner))
                    //{
                    //    if (currFont == null)
                    //    {
                    //        points = (float)(value * 9);
                    //        _deviceValue = (points * 0.5f / 72.0f) * ppi;
                    //    }
                    //    else
                    //    {
                    //        _deviceValue = value * 0.5f * (currFont.SizeInPoints / 72.0f) * ppi;
                    //    }
                    //    break;
                    //}
                    break;
                case SvgUnitType.Centimeter:
                    _deviceValue = (float)((value / cmInInch) * ppi);
                    break;
                case SvgUnitType.Inch:
                    _deviceValue = value * ppi;
                    break;
                case SvgUnitType.Millimeter:
                    _deviceValue = (float)((value / 10) / cmInInch) * ppi;
                    break;
                case SvgUnitType.Pica:
                    _deviceValue = ((value * 12) / 72) * ppi;
                    break;
                case SvgUnitType.Point:
                    _deviceValue = (value / 72) * ppi;
                    break;
                case SvgUnitType.Pixel:
                    _deviceValue = value;
                    break;
                case SvgUnitType.User:
                    _deviceValue = value;
                    break;
                case SvgUnitType.Percentage:
                    var size = skBounds.Size;

                    switch (renderType)
                    {
                        case UnitRenderingType.Horizontal:
                            _deviceValue = (size.Width / 100) * value;
                            break;
                        case UnitRenderingType.HorizontalOffset:
                            _deviceValue = (size.Width / 100) * value + skBounds.Location.X;
                            break;
                        case UnitRenderingType.Vertical:
                            _deviceValue = (size.Height / 100) * value;
                            break;
                        case UnitRenderingType.VerticalOffset:
                            _deviceValue = (size.Height / 100) * value + skBounds.Location.Y;
                            break;
                        case UnitRenderingType.Other:
                            // Calculate a percentage value of the normalized viewBox diagonal length. 
                            if (owner?.OwnerDocument != null && owner.OwnerDocument.ViewBox != null && owner.OwnerDocument.ViewBox.Width != 0 && owner.OwnerDocument.ViewBox.Height != 0)
                            {
                                _deviceValue = (float)(Math.Sqrt(Math.Pow(owner.OwnerDocument.ViewBox.Width, 2) + Math.Pow(owner.OwnerDocument.ViewBox.Height, 2)) / Math.Sqrt(2) * value / 100.0);
                            }
                            else
                                _deviceValue = (float)(Math.Sqrt(Math.Pow(size.Width, 2) + Math.Pow(size.Height, 2)) / Math.Sqrt(2) * value / 100.0);
                            break;
                    }
                    break;
                default:
                    _deviceValue = value;
                    break;
            }
            return _deviceValue.Value;
        }

        public static SKSize GetDimensions(SvgFragment svgFragment)
        {
            float w, h;
            var isWidthperc = svgFragment.Width.Type == SvgUnitType.Percentage;
            var isHeightperc = svgFragment.Height.Type == SvgUnitType.Percentage;

            var bounds = new SKRect();
            if (isWidthperc || isHeightperc)
            {
                if (svgFragment.ViewBox.Width > 0 && svgFragment.ViewBox.Height > 0)
                {
                    bounds = new SKRect(svgFragment.ViewBox.MinX, svgFragment.ViewBox.MinY, svgFragment.ViewBox.Width, svgFragment.ViewBox.Height);
                }
                else
                {
                    // TODO: Calculate correct bounds using Children bounds.
                }
            }

            if (isWidthperc)
            {
                w = (bounds.Width + bounds.Left) * (svgFragment.Width.Value * 0.01f);
            }
            else
            {
                // NOTE: // Pass bounds as SKRect.Empty because percentage case is handled before.
                w = svgFragment.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgFragment, SKRect.Empty);
            }
            if (isHeightperc)
            {
                h = (bounds.Height + bounds.Top) * (svgFragment.Height.Value * 0.01f);
            }
            else
            {
                // NOTE: // Pass bounds as SKRect.Empty because percentage case is handled before.
                h = svgFragment.Height.ToDeviceValue(UnitRenderingType.Vertical, svgFragment, SKRect.Empty);
            }

            return new SKSize(w, h);
        }
    }
}
