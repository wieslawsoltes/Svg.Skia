// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public static class SvgExtensions
    {
        public static double DegreeToRadian(this double degrees)
        {
            return Math.PI * degrees / 180.0;
        }

        public static double RadianToDegree(this double radians)
        {
            return radians * (180.0 / Math.PI);
        }

        public static bool IsNone(this Uri uri)
        {
            return string.Equals(uri.ToString(), "none", StringComparison.OrdinalIgnoreCase);
        }

        public static void GetOptionalNumbers(this SvgNumberCollection svgNumberCollection, float defaultValue1, float defaultValue2, out float value1, out float value2)
        {
            value1 = defaultValue1;
            value2 = defaultValue2;
            if (svgNumberCollection == null)
            {
                return;
            }
            if (svgNumberCollection.Count == 1)
            {
                value1 = svgNumberCollection[0];
                value2 = value1;
            }
            else if (svgNumberCollection.Count == 2)
            {
                value1 = svgNumberCollection[0];
                value2 = svgNumberCollection[1];
            }
        }

        public static float CalculateOtherPercentageValue(this SKRect skBounds)
        {
            return (float)(Math.Sqrt((skBounds.Width * skBounds.Width) + (skBounds.Width * skBounds.Height)) / Math.Sqrt(2.0));
        }

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
            //if (value == 0.0f)
            //{
            //    _deviceValue = 0.0f;
            //    return _deviceValue.Value;
            //}

            float points;

            switch (type)
            {
                case SvgUnitType.Em:
                    points = value * 9;
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
                    points = value * 9;
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
                    _deviceValue = (value / cmInInch) * ppi;
                    break;
                case SvgUnitType.Inch:
                    _deviceValue = value * ppi;
                    break;
                case SvgUnitType.Millimeter:
                    _deviceValue = (value / 10) / cmInInch * ppi;
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
                        default:
                        case UnitRenderingType.Other:
                            // Calculate a percentage value of the normalized viewBox diagonal length. 
                            if (owner?.OwnerDocument != null && owner.OwnerDocument.ViewBox != null && owner.OwnerDocument.ViewBox.Width != 0 && owner.OwnerDocument.ViewBox.Height != 0)
                            {
                                _deviceValue = (float)(Math.Sqrt(Math.Pow(owner.OwnerDocument.ViewBox.Width, 2) + Math.Pow(owner.OwnerDocument.ViewBox.Height, 2)) / Math.Sqrt(2) * value / 100.0);
                            }
                            else
                            {
                                _deviceValue = (float)(Math.Sqrt(Math.Pow(size.Width, 2) + Math.Pow(size.Height, 2)) / Math.Sqrt(2) * value / 100.0);
                            }

                            break;
                    }
                    break;
                default:
                    _deviceValue = value;
                    break;
            }
            return _deviceValue.Value;
        }

        public static SvgUnit Normalize(this SvgUnit svgUnit, SvgCoordinateUnits svgCoordinateUnits)
        {
            return svgUnit.Type == SvgUnitType.Percentage && svgCoordinateUnits == SvgCoordinateUnits.ObjectBoundingBox ?
                    new SvgUnit(SvgUnitType.User, svgUnit.Value / 100) : svgUnit;
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
                // NOTE: Pass bounds as SKRect.Empty because percentage case is handled before.
                w = svgFragment.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgFragment, SKRect.Empty);
            }
            if (isHeightperc)
            {
                h = (bounds.Height + bounds.Top) * (svgFragment.Height.Value * 0.01f);
            }
            else
            {
                // NOTE: Pass bounds as SKRect.Empty because percentage case is handled before.
                h = svgFragment.Height.ToDeviceValue(UnitRenderingType.Vertical, svgFragment, SKRect.Empty);
            }

            return new SKSize(w, h);
        }

        public static T? GetReference<T>(this SvgElement svgElement, Uri uri) where T : SvgElement
        {
            if (uri == null)
            {
                return null;
            }

            var svgElementById = svgElement.OwnerDocument?.GetElementById(uri.ToString());
            if (svgElementById != null)
            {
                return svgElementById as T;
            }

            return null;
        }

        public static bool ElementReferencesUri<T>(this T svgElement, Func<T, Uri?> getUri, HashSet<Uri> uris, SvgElement? svgReferencedElement) where T : SvgElement
        {
            if (svgReferencedElement == null)
            {
                return false;
            }

            if (svgReferencedElement is T svgElementT)
            {
                var referencedElementUri = getUri(svgElementT);

                if (referencedElementUri == null)
                {
                    return false;
                }

                if (uris.Contains(referencedElementUri))
                {
                    return true;
                }

                if (GetReference<T>(svgElement, referencedElementUri) != null)
                {
                    uris.Add(referencedElementUri);
                }

                return ElementReferencesUri(
                    svgElementT,
                    getUri,
                    uris,
                    GetReference<SvgElement>(svgElementT, referencedElementUri));
            }

            foreach (var svgChildElement in svgReferencedElement.Children)
            {
                if (ElementReferencesUri(svgElement, getUri, uris, svgChildElement))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasRecursiveReference<T>(this T svgElement, Func<T, Uri?> getUri, HashSet<Uri> uris) where T : SvgElement
        {
            var referencedElementUri = getUri(svgElement);
            if (referencedElementUri == null)
            {
                return false;
            }
            var svgReferencedElement = GetReference<SvgElement>(svgElement, referencedElementUri);
            if (uris.Contains(referencedElementUri))
            {
                return true;
            }
            uris.Add(referencedElementUri);
            return ElementReferencesUri<T>(svgElement, getUri, uris, svgReferencedElement);
        }

        public static Uri? GetUri(this SvgElement svgElement, string name)
        {
            if (svgElement.TryGetAttribute(name, out string uriString))
            {
                return new Uri(uriString, UriKind.RelativeOrAbsolute);
            }
            return null;
        }

        public static bool GetAttribute(this SvgElement svgElement, string name, out string value)
        {
            if (svgElement.TryGetAttribute(name, out value))
            {
                return true;
            }
            return false;
        }

        public static T? GetUriElementReference<T>(this SvgElement svgOwnerElement, string name, HashSet<Uri> uris) where T : SvgElement
        {
            var uri = svgOwnerElement.GetUri(name);
            if (uri != null)
            {
                if (HasRecursiveReference(svgOwnerElement, (e) => e.GetUri(name), uris))
                {
                    return null;
                }

                var svgElement = GetReference<T>(svgOwnerElement, uri);
                if (svgElement == null)
                {
                    return null;
                }
                return svgElement;
            }
            return null;
        }
    }
}
