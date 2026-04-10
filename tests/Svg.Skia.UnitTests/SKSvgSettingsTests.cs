using SkiaSharp;
using Svg.Skia.TypefaceProviders;
using Svg.Skia.UnitTests.Common;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SKSvgSettingsTests : SvgUnitTest
{
    [Fact]
    public void Defaults_EnableSvgFonts()
    {
        var settings = new SKSvgSettings();

        Assert.True(settings.EnableSvgFonts);
    }

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

    [Fact]
    public void Clone_PreservesEnableSvgFonts()
    {
        var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = true;

        var clone = svg.Clone();

        Assert.True(clone.Settings.EnableSvgFonts);
    }

    [Fact]
    public void DefaultTypefaceProvider_AllowsExplicitDefaultFamilyRequest()
    {
        var provider = new DefaultTypefaceProvider();
        var familyName = SKTypeface.Default.FamilyName;

        using var typeface = provider.FromFamilyName(familyName, SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

        Assert.NotNull(typeface);
        Assert.Equal(familyName, typeface!.FamilyName);
    }

    [Fact]
    public void FontManagerTypefaceProvider_AllowsExplicitDefaultFamilyRequest()
    {
        var provider = new FontManagerTypefaceProvider();
        var familyName = SKTypeface.Default.FamilyName;

        using var typeface = provider.FromFamilyName(familyName, SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

        Assert.NotNull(typeface);
        Assert.Equal(familyName, typeface!.FamilyName);
    }
}
