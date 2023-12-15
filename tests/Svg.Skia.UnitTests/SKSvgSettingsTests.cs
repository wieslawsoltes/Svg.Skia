/*
 * Svg.Skia SVG rendering library.
 * Copyright (C) 2023  Wiesław Šoltés
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using SkiaSharp;
using Svg.Skia.UnitTests.Common;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SKSvgSettingsTests : SvgUnitTest
{
    [Theory]
    [InlineData("Amiri", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)]
    [InlineData("Mplus 1p", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)]
    [InlineData("Noto Emoji", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)]
    [InlineData("Noto Mono", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)]
    [InlineData("Noto Sans", SKFontStyleWeight.Black, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)]
    [InlineData("Noto Sans", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)]
    [InlineData("Noto Sans", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Italic)]
    [InlineData("Noto Sans", SKFontStyleWeight.Light, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)]
    [InlineData("Noto Sans", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)]
    [InlineData("Noto Sans", SKFontStyleWeight.Thin, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright, Skip = "TODO")]
    [InlineData("Noto Serif", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)]
    [InlineData("Sedgwick Ave Display", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)]
    [InlineData("Source Sans Pro", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)]
    [InlineData("Yellowtail", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)]
    public void Fonts(string fontFamily, SKFontStyleWeight fontWeight, SKFontStyleWidth fontWidth, SKFontStyleSlant fontStyle)
    {
        var expectedTypeface = default(SKTypeface);

        var settings = new SKSvgSettings();
        
        SetTypefaceProviders(settings);

        if (settings.TypefaceProviders is { } && settings.TypefaceProviders.Count > 0)
        {
            foreach (var typefaceProviders in settings.TypefaceProviders)
            {
                var skTypeface = typefaceProviders.FromFamilyName(fontFamily, fontWeight, fontWidth, fontStyle);
                if (skTypeface is { })
                {
                    expectedTypeface = skTypeface;
                    break;
                }
            }
        }

        Assert.NotNull(expectedTypeface);

        if (expectedTypeface is { })
        {
            Assert.Equal(fontFamily, expectedTypeface.FamilyName);
            Assert.Equal((int)fontWeight, expectedTypeface.FontWeight);
            Assert.Equal((int)fontWidth, expectedTypeface.FontWidth);
            Assert.Equal(fontStyle, expectedTypeface.FontSlant);
        }
    }
}
