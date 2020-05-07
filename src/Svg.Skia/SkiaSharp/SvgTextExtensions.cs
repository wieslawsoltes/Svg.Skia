// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;

namespace Svg.Skia
{
    public interface ITypefaceProvider
    {
        SKTypeface? FromFamilyName(string fontFamily, SKFontStyleWeight fontWeight, SKFontStyleWidth fontWidth, SKFontStyleSlant fontStyle);
    }

    public class DefaultTypefaceProvider : ITypefaceProvider
    {
        public static char[] s_fontFamilyTrim = new char[] { '\'' };

        public SKTypeface? FromFamilyName(string fontFamily, SKFontStyleWeight fontWeight, SKFontStyleWidth fontWidth, SKFontStyleSlant fontStyle)
        {
            var skTypeface = default(SKTypeface);
            var fontFamilyNames = fontFamily?.Split(',')?.Select(x => x.Trim().Trim(s_fontFamilyTrim))?.ToArray();
            if (fontFamilyNames != null && fontFamilyNames.Length > 0)
            {
                var defaultName = SKTypeface.Default.FamilyName;

                foreach (var fontFamilyName in fontFamilyNames)
                {
                    skTypeface = SKTypeface.FromFamilyName(fontFamilyName, fontWeight, fontWidth, fontStyle);
                    if (skTypeface != null)
                    {
                        if (!skTypeface.FamilyName.Equals(fontFamilyName, StringComparison.Ordinal)
                            && defaultName.Equals(skTypeface.FamilyName, StringComparison.Ordinal))
                        {
                            skTypeface.Dispose();
                            continue;
                        }
                        break;
                    }
                }
            }
            return skTypeface;
        }
    }

    public class CustomTypefaceProvider : ITypefaceProvider, IDisposable
    {
        public static char[] s_fontFamilyTrim = new char[] { '\'' };

        public SKTypeface? Typeface { get; set; }

        public string FamilyName { get; set; }

        public CustomTypefaceProvider(Stream stream, int index = 0)
        {
            Typeface = SKTypeface.FromStream(stream, index);
            FamilyName = Typeface.FamilyName;
        }

        public CustomTypefaceProvider(SKStreamAsset stream, int index = 0)
        {
            Typeface = SKTypeface.FromStream(stream, index);
            FamilyName = Typeface.FamilyName;
        }

        public CustomTypefaceProvider(string path, int index = 0)
        {
            Typeface = SKTypeface.FromFile(path, index);
            FamilyName = Typeface.FamilyName;
        }

        public CustomTypefaceProvider(SKData data, int index = 0)
        {
            Typeface = SKTypeface.FromData(data, index);
            FamilyName = Typeface.FamilyName;
        }

        public SKTypeface? FromFamilyName(string fontFamily, SKFontStyleWeight fontWeight, SKFontStyleWidth fontWidth, SKFontStyleSlant fontStyle)
        {
            var skTypeface = default(SKTypeface);
            var fontFamilyNames = fontFamily?.Split(',')?.Select(x => x.Trim().Trim(s_fontFamilyTrim))?.ToArray();
            if (fontFamilyNames != null && fontFamilyNames.Length > 0)
            {
                foreach (var fontFamilyName in fontFamilyNames)
                {
                    if (fontFamily == FamilyName)
                    {
                        skTypeface = Typeface;
                        break;
                    }
                }
            }
            return skTypeface;
        }

        public void Dispose()
        {
            if (Typeface != null)
            {
                Typeface.Dispose();
                Typeface = null;
            }
        }
    }

    public class FontManagerTypefacerovider : ITypefaceProvider
    {
        public static char[] s_fontFamilyTrim = new char[] { '\'' };

        public SKFontManager FontManager { get; set; }

        public FontManagerTypefacerovider()
        {
            FontManager = SKFontManager.Default;
        }

        public SKTypeface CreateTypeface(Stream stream, int index = 0)
        {
            return FontManager.CreateTypeface(stream, index);
        }

        public SKTypeface CreateTypeface(SKStreamAsset stream, int index = 0)
        {
            return FontManager.CreateTypeface(stream, index);
        }

        public SKTypeface CreateTypeface(string path, int index = 0)
        {
            return FontManager.CreateTypeface(path, index);
        }

        public SKTypeface CreateTypeface(SKData data, int index = 0)
        {
            return FontManager.CreateTypeface(data, index);
        }

        public SKTypeface? FromFamilyName(string fontFamily, SKFontStyleWeight fontWeight, SKFontStyleWidth fontWidth, SKFontStyleSlant fontStyle)
        {
            var skTypeface = default(SKTypeface);
            var fontFamilyNames = fontFamily?.Split(',')?.Select(x => x.Trim().Trim(s_fontFamilyTrim))?.ToArray();
            if (fontFamilyNames != null && fontFamilyNames.Length > 0)
            {
                var defaultName = SKTypeface.Default.FamilyName;
                var skFontManager = FontManager;
                var skFontStyle = new SKFontStyle(fontWeight, fontWidth, fontStyle);

                foreach (var fontFamilyName in fontFamilyNames)
                {
                    var skFontStyleSet = skFontManager.GetFontStyles(fontFamilyName);
                    if (skFontStyleSet.Count > 0)
                    {
                        skTypeface = skFontManager.MatchFamily(fontFamilyName, skFontStyle);
                        if (skTypeface != null)
                        {
                            if (!defaultName.Equals(fontFamilyName, StringComparison.Ordinal)
                                && defaultName.Equals(skTypeface.FamilyName, StringComparison.Ordinal))
                            {
                                skTypeface.Dispose();
                                skTypeface = null;
                                continue;
                            }
                            break;
                        }
                    }
                }
            }
            return skTypeface;
        }
    }

    public static class SvgTextExtensions
    {
        public static IList<ITypefaceProvider> s_typefaceProviders = new List<ITypefaceProvider>()
        {
            new FontManagerTypefacerovider(),
            new DefaultTypefaceProvider()
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

        public static SKFontStyleWidth ToSKFontStyleWidth(SvgFontStretch svgFontStretch)
        {
            var fontWidth = SKFontStyleWidth.Normal;

            switch (svgFontStretch)
            {
                case SvgFontStretch.Inherit:
                    // TODO: Implement SvgFontStretch.Inherit
                    break;
                case SvgFontStretch.Normal:
                    fontWidth = SKFontStyleWidth.Normal;
                    break;
                case SvgFontStretch.Wider:
                    // TODO: Implement SvgFontStretch.Wider
                    break;
                case SvgFontStretch.Narrower:
                    // TODO: Implement SvgFontStretch.Narrower
                    break;
                case SvgFontStretch.UltraCondensed:
                    fontWidth = SKFontStyleWidth.UltraCondensed;
                    break;
                case SvgFontStretch.ExtraCondensed:
                    fontWidth = SKFontStyleWidth.ExtraCondensed;
                    break;
                case SvgFontStretch.Condensed:
                    fontWidth = SKFontStyleWidth.Condensed;
                    break;
                case SvgFontStretch.SemiCondensed:
                    fontWidth = SKFontStyleWidth.SemiCondensed;
                    break;
                case SvgFontStretch.SemiExpanded:
                    fontWidth = SKFontStyleWidth.SemiExpanded;
                    break;
                case SvgFontStretch.Expanded:
                    fontWidth = SKFontStyleWidth.Expanded;
                    break;
                case SvgFontStretch.ExtraExpanded:
                    fontWidth = SKFontStyleWidth.ExtraExpanded;
                    break;
                case SvgFontStretch.UltraExpanded:
                    fontWidth = SKFontStyleWidth.UltraExpanded;
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
            var fontWidth = ToSKFontStyleWidth(svgText.FontStretch);
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
