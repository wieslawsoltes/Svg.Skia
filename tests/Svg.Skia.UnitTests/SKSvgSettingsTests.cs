using System.Collections.Generic;
using System.Reflection;
using SkiaSharp;
using Svg;
using Svg.Skia.TypefaceProviders;
using Svg.Skia.UnitTests.Common;
using Xunit;
using ShimPaint = ShimSkiaSharp.SKPaint;

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
    public void FontManagerTypefaceProvider_DoesNotLoadDefaultFontManagerInConstructor()
    {
        var provider = new FontManagerTypefaceProvider();

        Assert.Null(GetFontManager(provider));
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

    [Fact]
    public void ToSKPaint_WithoutTypeface_DoesNotResolveDefaultTypeface()
    {
        var model = new SkiaModel(new SKSvgSettings());

        using var paint = model.ToSKPaint(new ShimPaint());

        Assert.NotNull(paint);
#pragma warning disable CS0618 // SKPaint.Typeface is verified here to avoid loading the default platform typeface.
        Assert.Null(paint!.Typeface);
#pragma warning restore CS0618
    }

    [Fact]
    public void ToSKPaint_WithImplicitTypeface_DoesNotResolveDefaultTypeface()
    {
        var model = new SkiaModel(new SKSvgSettings());
        var source = new ShimPaint
        {
            Typeface = CreateImplicitTypeface()
        };

        using var paint = model.ToSKPaint(source);

        Assert.NotNull(paint);
#pragma warning disable CS0618 // SKPaint.Typeface is verified here to avoid loading the default platform typeface.
        Assert.Null(paint!.Typeface);
#pragma warning restore CS0618
    }

    [Fact]
    public void ToSKFont_WithoutTypeface_DoesNotResolveDefaultTypeface()
    {
        var model = new SkiaModel(new SKSvgSettings());

        using var font = model.ToSKFont(new ShimPaint());

        Assert.Null(font.Typeface);
    }

    [Fact]
    public void ToSKFont_FontWithoutTypeface_DoesNotResolveDefaultTypeface()
    {
        var model = new SkiaModel(new SKSvgSettings());

        using var font = model.ToSKFont(new ShimSkiaSharp.SKFont());

        Assert.NotNull(font);
        Assert.Null(font!.Typeface);
    }

    [Fact]
    public void ToSKFont_WithImplicitTypeface_DoesNotResolveDefaultTypeface()
    {
        var model = new SkiaModel(new SKSvgSettings());
        var source = new ShimPaint
        {
            Typeface = CreateImplicitTypeface()
        };

        using var font = model.ToSKFont(source);

        Assert.Null(font.Typeface);
    }

    [Fact]
    public void ToSKFont_FontWithImplicitTypeface_DoesNotResolveDefaultTypeface()
    {
        var model = new SkiaModel(new SKSvgSettings());
        var source = new ShimSkiaSharp.SKFont
        {
            Typeface = CreateImplicitTypeface()
        };

        using var font = model.ToSKFont(source);

        Assert.NotNull(font);
        Assert.Null(font!.Typeface);
    }

    [Fact]
    public void GetRenderPaint_WithoutTypeface_DoesNotResolveDefaultTypeface()
    {
        var model = new SkiaModel(new SKSvgSettings());
        var method = typeof(SkiaModel).GetMethod(
            "GetRenderPaint",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        using var paint = Assert.IsType<SKPaint>(method!.Invoke(model, new object?[] { new ShimPaint() }));

#pragma warning disable CS0618 // SKPaint.Typeface is verified here to avoid loading the default platform typeface.
        Assert.Null(paint.Typeface);
#pragma warning restore CS0618
    }

    [Fact]
    public void GetRenderPaint_WithImplicitTypeface_DoesNotResolveDefaultTypeface()
    {
        var model = new SkiaModel(new SKSvgSettings());
        var method = typeof(SkiaModel).GetMethod(
            "GetRenderPaint",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var source = new ShimPaint
        {
            Typeface = CreateImplicitTypeface()
        };

        using var paint = Assert.IsType<SKPaint>(method!.Invoke(model, new object?[] { source }));

#pragma warning disable CS0618 // SKPaint.Typeface is verified here to avoid loading the default platform typeface.
        Assert.Null(paint.Typeface);
#pragma warning restore CS0618
    }

    [Fact]
    public void ToWireframePaint_WithImplicitTypeface_DoesNotResolveDefaultTypeface()
    {
        var model = new SkiaModel(new SKSvgSettings());
        var method = typeof(SkiaModel).GetMethod(
            "ToWireframePaint",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var source = new ShimPaint
        {
            Typeface = CreateImplicitTypeface()
        };

        using var paint = Assert.IsType<SKPaint>(method!.Invoke(model, new object?[] { source }));

#pragma warning disable CS0618 // SKPaint.Typeface is verified here to avoid loading the default platform typeface.
        Assert.Null(paint.Typeface);
#pragma warning restore CS0618
    }

    [Fact]
    public void FindTypefaces_WithImplicitTypeface_DoesNotResolvePlatformTypeface()
    {
        var provider = new FontManagerTypefaceProvider();
        var settings = new SKSvgSettings
        {
            TypefaceProviders = new List<ITypefaceProvider> { provider }
        };
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(settings));
        var source = new ShimPaint
        {
            Typeface = CreateImplicitTypeface()
        };

        var span = Assert.Single(assetLoader.FindTypefaces("I L1", source));

        Assert.Equal("I L1", span.Text);
        Assert.Null(span.Typeface);
        Assert.Null(GetFontManager(provider));
    }

    [Fact]
    public void FindRunTypeface_WithImplicitTypeface_DoesNotResolvePlatformTypeface()
    {
        var provider = new FontManagerTypefaceProvider();
        var settings = new SKSvgSettings
        {
            TypefaceProviders = new List<ITypefaceProvider> { provider }
        };
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(settings));
        var source = new ShimPaint
        {
            Typeface = CreateImplicitTypeface()
        };

        var typeface = assetLoader.FindRunTypeface("I L1", source);

        Assert.Null(typeface);
        Assert.Null(GetFontManager(provider));
    }

    private static ShimSkiaSharp.SKTypeface CreateImplicitTypeface()
    {
        return ShimSkiaSharp.SKTypeface.FromFamilyName(
            null!,
            ShimSkiaSharp.SKFontStyleWeight.Normal,
            ShimSkiaSharp.SKFontStyleWidth.Normal,
            ShimSkiaSharp.SKFontStyleSlant.Upright);
    }

    private static object? GetFontManager(FontManagerTypefaceProvider provider)
    {
        var field = typeof(FontManagerTypefaceProvider).GetField(
            "_fontManager",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        return field!.GetValue(provider);
    }

    private sealed class TestJavaScriptRuntimeFactory : ISKSvgJavaScriptRuntimeFactory
    {
        public ISKSvgJavaScriptRuntime Create(SvgDocument document, SKSvgJavaScriptRuntimeSettings settings)
        {
            throw new System.NotSupportedException();
        }
    }
}
