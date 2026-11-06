using System;
using System.Collections.Generic;
using System.Linq;
using ShimSkiaSharp;
using Svg.Skia.TypefaceProviders;
using Svg.Skia.UnitTests.Common;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SyntheticFontStyleTests : SvgUnitTest
{
    private const string FontFamily = "SvgSkiaSyntheticStyle";

    [Theory]
    [InlineData(SKFontStyleSlant.Italic)]
    [InlineData(SKFontStyleSlant.Oblique)]
    public void ToSKFont_Paint_SynthesizesMissingSlant(SKFontStyleSlant slant)
    {
        using var typeface = OpenTypeface("NotoSans-Regular.ttf");
        var model = CreateModel(typeface);

        using var font = model.ToSKFont(CreatePaint(slant));
        using var uprightFont = model.ToSKFont(CreatePaint(SKFontStyleSlant.Upright));
        using var slantedPath = font.GetTextPath("H", SkiaSharp.SKPoint.Empty);
        using var uprightPath = uprightFont.GetTextPath("H", SkiaSharp.SKPoint.Empty);

        Assert.Equal(SkiaSharp.SKFontStyleSlant.Upright, font.Typeface!.FontSlant);
        Assert.Equal(-0.3f, font.SkewX);
        Assert.True(slantedPath.Bounds.Right > uprightPath.Bounds.Right + 5f);
        Assert.Equal(uprightPath.Bounds.Top, slantedPath.Bounds.Top, 3);
        Assert.Equal(uprightPath.Bounds.Bottom, slantedPath.Bounds.Bottom, 3);
    }

    [Theory]
    [InlineData(SKFontStyleSlant.Italic)]
    [InlineData(SKFontStyleSlant.Oblique)]
    public void ToSKFont_Font_SynthesizesMissingSlant(SKFontStyleSlant slant)
    {
        using var typeface = OpenTypeface("NotoSans-Regular.ttf");
        var model = CreateModel(typeface);
        var source = new SKFont(CreateTypeface(slant), 48f) { ScaleX = 1.25f };

        using var font = model.ToSKFont(source);

        Assert.NotNull(font);
        Assert.Equal(-0.3f, font.SkewX);
        Assert.Equal(1.25f, font.ScaleX);
        Assert.Equal(0f, source.SkewX);
    }

    [Theory]
    [InlineData(-0.15f)]
    [InlineData(0.4f)]
    public void ToSKFont_PreservesExplicitSkew(float skew)
    {
        using var typeface = OpenTypeface("NotoSans-Regular.ttf");
        var model = CreateModel(typeface);
        var source = new SKFont(CreateTypeface(SKFontStyleSlant.Italic), 48f, skewX: skew);

        using var font = model.ToSKFont(source);

        Assert.Equal(skew, font!.SkewX);
    }

    [Theory]
    [InlineData(SKFontStyleSlant.Italic)]
    [InlineData(SKFontStyleSlant.Oblique)]
    public void ToSKFont_DoesNotShearNativeItalicFaces(SKFontStyleSlant slant)
    {
        using var typeface = OpenTypeface("NotoSans-Italic.ttf");
        Assert.Equal(SkiaSharp.SKFontStyleSlant.Italic, typeface.FontSlant);
        var model = CreateModel(typeface);

        using var paintFont = model.ToSKFont(CreatePaint(slant));
        using var explicitFont = model.ToSKFont(new SKFont(CreateTypeface(slant), 48f));

        Assert.Equal(0f, paintFont.SkewX);
        Assert.Equal(0f, explicitFont!.SkewX);
    }

    [Fact]
    public void ToSKFont_DoesNotShearRequestedUprightFaces()
    {
        using var typeface = OpenTypeface("NotoSans-Regular.ttf");
        var model = CreateModel(typeface);

        using var paintFont = model.ToSKFont(CreatePaint(SKFontStyleSlant.Upright));
        using var explicitFont = model.ToSKFont(new SKFont(CreateTypeface(SKFontStyleSlant.Upright), 48f));

        Assert.Equal(0f, paintFont.SkewX);
        Assert.Equal(0f, explicitFont!.SkewX);
    }

    [Theory]
    [InlineData(SKFontStyleSlant.Italic)]
    [InlineData(SKFontStyleSlant.Oblique)]
    public void FindTypefaces_PreservesRequestedSlantForUprightFontData(SKFontStyleSlant slant)
    {
        using var typeface = OpenTypeface("NotoSans-Regular.ttf");
        var model = CreateModel(typeface);
        var loader = new SkiaSvgAssetLoader(model);

        var span = Assert.Single(loader.FindTypefaces("Slanted labels", CreatePaint(slant)));

        Assert.Equal(slant, span.Typeface!.FontSlant);
        using var font = model.ToSKFont(new SKFont(span.Typeface, 48f));
        Assert.Equal(-0.3f, font!.SkewX);
    }

    [Theory]
    [InlineData(SKFontStyleSlant.Italic)]
    [InlineData(SKFontStyleSlant.Oblique)]
    public void FindRunTypeface_PreservesRequestedSlantForUprightFontData(SKFontStyleSlant slant)
    {
        using var typeface = OpenTypeface("NotoSans-Regular.ttf");
        var model = CreateModel(typeface);
        var loader = new SkiaSvgAssetLoader(model);

        var runTypeface = loader.FindRunTypeface("Slanted labels", CreatePaint(slant));

        Assert.Equal(slant, runTypeface!.FontSlant);
        using var font = model.ToSKFont(new SKFont(runTypeface, 48f));
        Assert.Equal(-0.3f, font!.SkewX);
    }

    [Theory]
    [InlineData("italic")]
    [InlineData("oblique")]
    public void SvgRendering_SynthesizedSlantChangesGlyphPixels(string style)
    {
        using var typeface = OpenTypeface("NotoSans-Regular.ttf");
        using var upright = RenderSvg(typeface, "normal");
        using var slanted = RenderSvg(typeface, style);

        Assert.Contains(upright.Pixels, pixel => pixel.Alpha > 0);
        Assert.Contains(slanted.Pixels, pixel => pixel.Alpha > 0);
        Assert.False(upright.Bytes.SequenceEqual(slanted.Bytes));
    }

    private static SkiaSharp.SKBitmap RenderSvg(SkiaSharp.SKTypeface typeface, string style)
    {
        using var svg = new SKSvg();
        svg.Settings.TypefaceProviders = new List<ITypefaceProvider> { new FixedTypefaceProvider(typeface) };
        svg.FromSvg($$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="160" height="100">
              <text x="20" y="75" font-family="{{FontFamily}}" font-size="64" font-style="{{style}}">HH</text>
            </svg>
            """);
        Assert.NotNull(svg.Picture);

        var bitmap = new SkiaSharp.SKBitmap(160, 100);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        canvas.DrawPicture(svg.Picture);
        return bitmap;
    }

    private static SkiaModel CreateModel(SkiaSharp.SKTypeface typeface)
    {
        return new SkiaModel(new SKSvgSettings
        {
            TypefaceProviders = new List<ITypefaceProvider> { new FixedTypefaceProvider(typeface) }
        });
    }

    private static SKPaint CreatePaint(SKFontStyleSlant slant)
    {
        return new SKPaint { TextSize = 48f, Typeface = CreateTypeface(slant) };
    }

    private static SKTypeface CreateTypeface(SKFontStyleSlant slant)
    {
        return SKTypeface.FromFamilyName(FontFamily, SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, slant);
    }

    private static SkiaSharp.SKTypeface OpenTypeface(string filename)
    {
        var typeface = SkiaSharp.SKTypeface.FromFile(GetFontsPath(filename));
        Assert.NotNull(typeface);
        return typeface;
    }

    private sealed class FixedTypefaceProvider : ITypefaceProvider
    {
        private readonly SkiaSharp.SKTypeface _typeface;

        public FixedTypefaceProvider(SkiaSharp.SKTypeface typeface)
        {
            _typeface = typeface;
        }

        public SkiaSharp.SKTypeface? FromFamilyName(
            string fontFamily,
            SkiaSharp.SKFontStyleWeight fontWeight,
            SkiaSharp.SKFontStyleWidth fontWidth,
            SkiaSharp.SKFontStyleSlant fontStyle)
        {
            return string.Equals(fontFamily, FontFamily, StringComparison.Ordinal) ? _typeface : null;
        }
    }
}
