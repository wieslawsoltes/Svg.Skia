// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public static class SvgTextExtensions
    {
        public static IList<ITypefaceProvider> s_typefaceProviders = new List<ITypefaceProvider>()
        {
            new SKTypefaceProvider()
        };

        public static SKFontStyleWeight SKFontStyleWeight(SvgFontWeight svgFontWeight)
        {
            var fontWeight = SkiaSharp.SKFontStyleWeight.Normal;

            switch (svgFontWeight)
            {
                case SvgFontWeight.Inherit:
                    // TODO: Implement SvgFontWeight.Inherit
                    break;
                case SvgFontWeight.Bolder:
                    // TODO: Implement SvgFontWeight.Bolder
                    break;
                case SvgFontWeight.Lighter:
                    // TODO: Implement SvgFontWeight.Lighter
                    break;
                case SvgFontWeight.W100:
                    fontWeight = SkiaSharp.SKFontStyleWeight.Thin;
                    break;
                case SvgFontWeight.W200:
                    fontWeight = SkiaSharp.SKFontStyleWeight.ExtraLight;
                    break;
                case SvgFontWeight.W300:
                    fontWeight = SkiaSharp.SKFontStyleWeight.Light;
                    break;
                case SvgFontWeight.W400: // SvgFontWeight.Normal:
                    fontWeight = SkiaSharp.SKFontStyleWeight.Normal;
                    break;
                case SvgFontWeight.W500:
                    fontWeight = SkiaSharp.SKFontStyleWeight.Medium;
                    break;
                case SvgFontWeight.W600:
                    fontWeight = SkiaSharp.SKFontStyleWeight.SemiBold;
                    break;
                case SvgFontWeight.W700: // SvgFontWeight.Bold:
                    fontWeight = SkiaSharp.SKFontStyleWeight.Bold;
                    break;
                case SvgFontWeight.W800:
                    fontWeight = SkiaSharp.SKFontStyleWeight.ExtraBold;
                    break;
                case SvgFontWeight.W900:
                    fontWeight = SkiaSharp.SKFontStyleWeight.Black;
                    break;
            }

            return fontWeight;
        }

        public static SKFontStyleWidth ToSKFontStyleWidth(string attributeFontStretch)
        {
            var fontWidth = SKFontStyleWidth.Normal;

            switch (attributeFontStretch?.ToLower())
            {
                case "inherit":
                    // TODO: Implement inherit
                    break;
                case "ultra-condensed":
                    fontWidth = SKFontStyleWidth.UltraCondensed;
                    break;
                case "extra-condensed":
                    fontWidth = SKFontStyleWidth.ExtraCondensed;
                    break;
                case "condensed":
                    fontWidth = SKFontStyleWidth.Condensed;
                    break;
                case "semi-condensed":
                    fontWidth = SKFontStyleWidth.SemiCondensed;
                    break;
                case "normal":
                    fontWidth = SKFontStyleWidth.Normal;
                    break;
                case "semi-expanded":
                    fontWidth = SKFontStyleWidth.SemiExpanded;
                    break;
                case "expanded":
                    fontWidth = SKFontStyleWidth.Expanded;
                    break;
                case "extra-expanded":
                    fontWidth = SKFontStyleWidth.ExtraExpanded;
                    break;
                case "ultra-expanded":
                    fontWidth = SKFontStyleWidth.UltraExpanded;
                    break;
                default:
                    break;
            }

            return fontWidth;
        }

        public static SKTextAlign ToSKTextAlign(SvgTextAnchor textAnchor)
        {
            return textAnchor switch
            {
                SvgTextAnchor.Middle => SKTextAlign.Center,
                SvgTextAnchor.End => SKTextAlign.Right,
                _ => SKTextAlign.Left,
            };
        }

        public static SKFontStyleSlant ToSKFontStyleSlant(SvgFontStyle fontStyle)
        {
            return fontStyle switch
            {
                SvgFontStyle.Oblique => SKFontStyleSlant.Oblique,
                SvgFontStyle.Italic => SKFontStyleSlant.Italic,
                _ => SKFontStyleSlant.Upright,
            };
        }

        private static void SetTypeface(SvgTextBase svgText, SKPaint skPaint, CompositeDisposable disposable)
        {
            var fontWeight = SKFontStyleWeight(svgText.FontWeight);

            // TODO: Use FontStretch property.
            svgText.TryGetAttribute("font-stretch", out string attributeFontStretch);
            var fontWidth = ToSKFontStyleWidth(attributeFontStretch);

            var fontStyle = ToSKFontStyleSlant(svgText.FontStyle);

            var fontFamily = svgText.FontFamily;

            if (s_typefaceProviders == null || s_typefaceProviders.Count <= 0)
            {
                return;
            }

            foreach (var typefaceProviders in s_typefaceProviders)
            {
                var skTypeface = typefaceProviders.FromFamilyName(fontFamily, fontWeight, fontWidth, fontStyle);
                if (skTypeface != null)
                {
                    disposable.Add(skTypeface);
                    skPaint.Typeface = skTypeface;
                    break;
                }
            }
        }

        public static void SetSKPaintText(SvgTextBase svgText, SKRect skBounds, SKPaint skPaint, CompositeDisposable disposable)
        {
            skPaint.LcdRenderText = true;
            skPaint.SubpixelText = true;
            skPaint.TextEncoding = SKTextEncoding.Utf16;

            skPaint.TextAlign = ToSKTextAlign(svgText.TextAnchor);

            if (svgText.TextDecoration.HasFlag(SvgTextDecoration.Underline))
            {
                // TODO: Implement SvgTextDecoration.Underline
            }

            if (svgText.TextDecoration.HasFlag(SvgTextDecoration.Overline))
            {
                // TODO: Implement SvgTextDecoration.Overline
            }

            if (svgText.TextDecoration.HasFlag(SvgTextDecoration.LineThrough))
            {
                // TODO: Implement SvgTextDecoration.LineThrough
            }

            float fontSize;
            var fontSizeUnit = svgText.FontSize;
            if (fontSizeUnit == SvgUnit.None || fontSizeUnit == SvgUnit.Empty)
            {
                // TODO: Do not use implicit float conversion from SvgUnit.ToDeviceValue
                //fontSize = new SvgUnit(SvgUnitType.Em, 1.0f);
                // NOTE: Use default SkPaint Font_Size
                fontSize = 12f;
            }
            else
            {
                fontSize = fontSizeUnit.ToDeviceValue(UnitRenderingType.Vertical, svgText, skBounds);
            }

            skPaint.TextSize = fontSize;

            SetTypeface(svgText, skPaint, disposable);
        }
    }
}
