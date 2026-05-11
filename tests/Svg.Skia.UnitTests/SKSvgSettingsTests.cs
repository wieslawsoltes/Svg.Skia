using System.Collections.Generic;
using SkiaSharp;
using Svg;
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

    [Fact]
    public void Defaults_EnableTextReferences()
    {
        var settings = new SKSvgSettings();

        Assert.True(settings.EnableTextReferences);
    }

    [Fact]
    public void Defaults_DisableJavaScript()
    {
        var settings = new SKSvgSettings();

        Assert.False(settings.EnableJavaScript);
    }

    [Fact]
    public void CopyTo_CopiesRenderingAndJavaScriptSettings()
    {
        var provider = new DefaultTypefaceProvider();
        var factory = new TestJavaScriptRuntimeFactory();
        var source = new SKSvgSettings
        {
            AlphaType = SKAlphaType.Premul,
            ColorType = SKColorType.Rgba8888,
            TypefaceProviders = new List<ITypefaceProvider> { provider },
            StandaloneViewport = new SKRect(1, 2, 3, 4),
            EnableSvgFonts = false,
            EnableTextReferences = false,
            EnableJavaScript = true,
            EnableExternalJavaScript = false,
            JavaScriptTimeoutMilliseconds = 123,
            JavaScriptMaxStatements = 456,
            ThrowOnJavaScriptError = true,
            JavaScriptRuntimeFactory = factory
        };
        var target = new SKSvgSettings();

        source.CopyTo(target);

        Assert.Equal(source.AlphaType, target.AlphaType);
        Assert.Equal(source.ColorType, target.ColorType);
        Assert.Same(source.SrgbLinear, target.SrgbLinear);
        Assert.Same(source.Srgb, target.Srgb);
        Assert.Equal(source.StandaloneViewport, target.StandaloneViewport);
        Assert.False(target.EnableSvgFonts);
        Assert.False(target.EnableTextReferences);
        Assert.True(target.EnableJavaScript);
        Assert.False(target.EnableExternalJavaScript);
        Assert.Equal(123, target.JavaScriptTimeoutMilliseconds);
        Assert.Equal(456, target.JavaScriptMaxStatements);
        Assert.True(target.ThrowOnJavaScriptError);
        Assert.Same(factory, target.JavaScriptRuntimeFactory);
        Assert.NotSame(source.TypefaceProviders, target.TypefaceProviders);
        Assert.Same(provider, Assert.Single(target.TypefaceProviders!));
    }

    [Fact]
    public void Clone_CopiesJavaScriptSettings()
    {
        var factory = new TestJavaScriptRuntimeFactory();
        var settings = new SKSvgSettings
        {
            EnableJavaScript = true,
            EnableExternalJavaScript = false,
            JavaScriptTimeoutMilliseconds = 250,
            JavaScriptMaxStatements = 789,
            ThrowOnJavaScriptError = true,
            JavaScriptRuntimeFactory = factory
        };

        var clone = settings.Clone();

        Assert.NotSame(settings, clone);
        Assert.True(clone.EnableJavaScript);
        Assert.False(clone.EnableExternalJavaScript);
        Assert.Equal(250, clone.JavaScriptTimeoutMilliseconds);
        Assert.Equal(789, clone.JavaScriptMaxStatements);
        Assert.True(clone.ThrowOnJavaScriptError);
        Assert.Same(factory, clone.JavaScriptRuntimeFactory);
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
    [InlineData("Noto Sans", SKFontStyleWeight.Thin, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)]
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
    public void Clone_PreservesEnableTextReferences()
    {
        var svg = new SKSvg();
        svg.Settings.EnableTextReferences = false;

        var clone = svg.Clone();

        Assert.False(clone.Settings.EnableTextReferences);
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

    private sealed class TestJavaScriptRuntimeFactory : ISKSvgJavaScriptRuntimeFactory
    {
        public ISKSvgJavaScriptRuntime Create(SvgDocument document, SKSvgJavaScriptRuntimeSettings settings)
        {
            throw new System.NotSupportedException();
        }
    }
}
