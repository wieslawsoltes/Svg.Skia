using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.Skia.TypefaceProviders;
using Svg.Skia.UnitTests.Common;
using Xunit;
using ShimPaint = ShimSkiaSharp.SKPaint;
using ShimFont = ShimSkiaSharp.SKFont;
using NativeTypeface = SkiaSharp.SKTypeface;

namespace Svg.Skia.UnitTests;

public class SyntheticItalicTests : SvgUnitTest
{
    private const float SyntheticObliqueSkewX = -0.25f;

    [Fact]
    public void ToSKFont_WithItalicStyle_NoItalicFace_AppliesSyntheticSkew()
    {
        using var provider = new RegularOnlyTypefaceProvider(GetFontsPath("SourceSansPro-Regular.ttf"));
        var model = CreateModel(provider);
        var source = new ShimPaint
        {
            TextSize = 48f,
            Typeface = SKTypeface.FromFamilyName(
                provider.FamilyName,
                SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Italic)
        };

        using var font = model.ToSKFont(source);

        Assert.NotNull(font.Typeface);
        Assert.Equal(SkiaSharp.SKFontStyleSlant.Upright, font.Typeface!.FontSlant);
        Assert.Equal(SyntheticObliqueSkewX, font.SkewX);
    }

    [Fact]
    public void ToSKFont_WithUprightStyle_DoesNotApplySkew()
    {
        using var provider = new RegularOnlyTypefaceProvider(GetFontsPath("SourceSansPro-Regular.ttf"));
        var model = CreateModel(provider);
        var source = new ShimPaint
        {
            TextSize = 48f,
            Typeface = SKTypeface.FromFamilyName(
                provider.FamilyName,
                SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright)
        };

        using var font = model.ToSKFont(source);

        Assert.Equal(0f, font.SkewX);
    }

    [Fact]
    public void ToSKFont_WithExplicitItalicFace_DoesNotDoubleSkew()
    {
        using var provider = new RegularOnlyTypefaceProvider(GetFontsPath("NotoSans-Italic.ttf"));
        var model = CreateModel(provider);
        var source = new ShimPaint
        {
            TextSize = 48f,
            Typeface = SKTypeface.FromFamilyName(
                provider.FamilyName,
                SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Italic)
        };

        using var font = model.ToSKFont(source);

        Assert.NotNull(font.Typeface);
        Assert.Equal(SkiaSharp.SKFontStyleSlant.Italic, font.Typeface!.FontSlant);
        Assert.Equal(0f, font.SkewX);
    }

    [Fact]
    public void ToSKFont_ShimFontOverload_NoItalicFace_AppliesSyntheticSkew()
    {
        using var provider = new RegularOnlyTypefaceProvider(GetFontsPath("SourceSansPro-Regular.ttf"));
        var model = CreateModel(provider);
        var source = new ShimFont(
            SKTypeface.FromFamilyName(
                provider.FamilyName,
                SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Italic),
            48f);

        using var font = model.ToSKFont(source);

        Assert.NotNull(font);
        Assert.Equal(SyntheticObliqueSkewX, font!.SkewX);
    }

    [Fact]
    public void ToSKFont_ShimFontOverload_WithExplicitSourceSkew_IsPreserved()
    {
        using var provider = new RegularOnlyTypefaceProvider(GetFontsPath("SourceSansPro-Regular.ttf"));
        var model = CreateModel(provider);
        const float explicitSkew = 0.15f;
        var source = new ShimFont(
            SKTypeface.FromFamilyName(
                provider.FamilyName,
                SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Italic),
            48f,
            1f,
            explicitSkew);

        using var font = model.ToSKFont(source);

        Assert.NotNull(font);
        Assert.Equal(explicitSkew, font!.SkewX);
    }

    private static SkiaModel CreateModel(ITypefaceProvider provider)
    {
        var settings = new SKSvgSettings
        {
            TypefaceProviders = new List<ITypefaceProvider> { provider }
        };
        return new SkiaModel(settings);
    }

    private sealed class RegularOnlyTypefaceProvider : ITypefaceProvider, IDisposable
    {
        private readonly NativeTypeface _typeface;

        public RegularOnlyTypefaceProvider(string path)
        {
            _typeface = NativeTypeface.FromFile(path);
            FamilyName = _typeface.FamilyName;
        }

        public string FamilyName { get; }

        public NativeTypeface? FromFamilyName(
            string fontFamily,
            SkiaSharp.SKFontStyleWeight fontWeight,
            SkiaSharp.SKFontStyleWidth fontWidth,
            SkiaSharp.SKFontStyleSlant fontStyle)
        {
            if (string.IsNullOrEmpty(fontFamily))
            {
                return null;
            }

            var requestedFamilies = fontFamily.Split(',');
            foreach (var requested in requestedFamilies)
            {
                if (string.Equals(requested.Trim().Trim('"', '\''), FamilyName, StringComparison.OrdinalIgnoreCase))
                {
                    return _typeface;
                }
            }

            return null;
        }

        public void Dispose() => _typeface.Dispose();
    }
}
