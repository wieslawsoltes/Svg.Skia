#pragma warning disable CS0618 // Shim paint keeps deprecated SKPaint text/typeface surface for compatibility

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShimSkiaSharp;
using Svg.Model.Services;
using Svg.Skia;
using Svg.Skia.TypefaceProviders;
using Xunit;
using NativeTypeface = SkiaSharp.SKTypeface;
using NativeTypefaceSlant = SkiaSharp.SKFontStyleSlant;
using NativeTypefaceWeight = SkiaSharp.SKFontStyleWeight;
using NativeTypefaceWidth = SkiaSharp.SKFontStyleWidth;

namespace Svg.Skia.UnitTests;

public class SkiaSvgAssetLoaderCachingTests
{
    [Fact]
    public void LoadImage_ReturnsZeroSizeImageForInvalidEncodedData()
    {
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        var image = assetLoader.LoadImage(stream);

        Assert.NotNull(image.Data);
        Assert.Equal(0, image.Width);
        Assert.Equal(0, image.Height);
    }

    [Fact]
    public void MeasureText_RecomputesAfterPaintMutation()
    {
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var paint = CreateTextPaint(12f);

        var firstBounds = default(SKRect);
        var firstAdvance = assetLoader.MeasureText("Scale Me", paint, ref firstBounds);

        var repeatedBounds = new SKRect(-1f, -1f, -1f, -1f);
        var repeatedAdvance = assetLoader.MeasureText("Scale Me", paint, ref repeatedBounds);

        Assert.Equal(firstAdvance, repeatedAdvance, 3);
        Assert.Equal(firstBounds.Left, repeatedBounds.Left, 3);
        Assert.Equal(firstBounds.Top, repeatedBounds.Top, 3);
        Assert.Equal(firstBounds.Right, repeatedBounds.Right, 3);
        Assert.Equal(firstBounds.Bottom, repeatedBounds.Bottom, 3);

        paint.TextSize = 36f;

        var mutatedBounds = default(SKRect);
        var mutatedAdvance = assetLoader.MeasureText("Scale Me", paint, ref mutatedBounds);

        Assert.True(mutatedAdvance > firstAdvance * 2f);
        Assert.True(mutatedBounds.Width > firstBounds.Width * 2f);
    }

    [Fact]
    public void FindTypefaces_ReturnsIndependentResultsAndRecomputesAfterPaintMutation()
    {
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var paint = CreateTextPaint(14f);
        const string text = "Bold Text 20px";

        var first = assetLoader.FindTypefaces(text, paint);
        var firstAdvance = first.Sum(static span => span.Advance);

        first.Clear();

        var repeated = assetLoader.FindTypefaces(text, paint);
        var repeatedAdvance = repeated.Sum(static span => span.Advance);

        Assert.NotEmpty(repeated);
        Assert.Equal(firstAdvance, repeatedAdvance, 3);

        paint.TextSize = 42f;

        var mutated = assetLoader.FindTypefaces(text, paint);
        var mutatedAdvance = mutated.Sum(static span => span.Advance);

        Assert.Equal(repeated.Count, mutated.Count);
        Assert.True(mutatedAdvance > repeatedAdvance * 2f);
    }

    [Fact]
    public void SharedCaches_DoNotBypassCustomTypefaceProvidersAcrossModels()
    {
        var firstProvider = new CountingTypefaceProvider();
        var secondProvider = new CountingTypefaceProvider();
        var requestedTypeface = SKTypeface.FromFamilyName(
            "Missing Custom Family",
            SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        var firstModel = new SkiaModel(new SKSvgSettings
        {
            TypefaceProviders = new List<ITypefaceProvider> { firstProvider }
        });
        var secondModel = new SkiaModel(new SKSvgSettings
        {
            TypefaceProviders = new List<ITypefaceProvider> { secondProvider }
        });

        firstModel.ToSKTypeface(requestedTypeface);
        secondModel.ToSKTypeface(requestedTypeface);

        Assert.True(firstProvider.CallCount > 0);
        Assert.True(secondProvider.CallCount > 0);
    }

    [Fact]
    public void Load_W3CWoffFontFaceRegistersDocumentTypeface()
    {
        var expectedFamily = GetNativeFontFamilyName(GetW3CResourcePath("Blocky.woff"));
        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 480f, 360f);

        using var _ = svg.Load(GetW3CSvgPath("pservers-grad-08-b"));
        var assetLoader = Assert.IsType<SkiaSvgAssetLoader>(svg.AssetLoader);
        using var fontScope = assetLoader.PushDocumentFonts(svg.SourceDocument!);

        AssertDocumentTypefaceFamily(svg, "Blocky", "Gradient", expectedFamily);
    }

    [Fact]
    public void Load_W3CRenderWoffFontFaceRegistersFallbackFamily()
    {
        var expectedFamily = GetNativeFontFamilyName(GetW3CResourcePath("Blocky.woff"));
        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 480f, 360f);

        using var _ = svg.Load(GetW3CSvgPath("render-elems-06-t"));
        var assetLoader = Assert.IsType<SkiaSvgAssetLoader>(svg.AssetLoader);
        using var fontScope = assetLoader.PushDocumentFonts(svg.SourceDocument!);

        AssertDocumentTypefaceFamily(svg, "BlockyWoff", "G", expectedFamily);
        AssertDocumentTypefaceFamily(svg, "Blocky, BlockyWoff", "G", expectedFamily);
        AssertRunTypefaceFamily(svg, "Blocky, BlockyWoff", "G", expectedFamily);
    }

    [Fact]
    public void Load_W3CGroupWoffFontFaceRegistersDocumentTypeface()
    {
        var expectedFamily = GetNativeFontFamilyName(GetW3CResourcePath("anglepoi.woff"));
        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 480f, 360f);

        using var _ = svg.Load(GetW3CSvgPath("render-groups-01-b"));
        var assetLoader = Assert.IsType<SkiaSvgAssetLoader>(svg.AssetLoader);
        using var fontScope = assetLoader.PushDocumentFonts(svg.SourceDocument!);

        AssertDocumentTypefaceFamily(svg, "anglepoise", "SVG", expectedFamily);
    }

    [Fact]
    public void Load_ClearsDocumentFontFaceProvidersBetweenDocuments()
    {
        const string transientFamily = "SvgSkiaTransientBlocky";
        var blockyFamily = GetNativeFontFamilyName(GetW3CResourcePath("Blocky.woff"));
        var blockyUri = new Uri(Path.GetFullPath(GetW3CResourcePath("Blocky.woff"))).AbsoluteUri;
        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 480f, 360f);

        using (svg.FromSvg($$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <style>
                @font-face {
                  font-family: {{transientFamily}};
                  src: url("{{blockyUri}}") format("woff");
                }
              </style>
              <text x="0" y="16" font-family="{{transientFamily}}">G</text>
            </svg>
            """))
        {
            var assetLoader = Assert.IsType<SkiaSvgAssetLoader>(svg.AssetLoader);
            using var fontScope = assetLoader.PushDocumentFonts(svg.SourceDocument!);
            AssertDocumentTypefaceFamily(svg, transientFamily, "G", blockyFamily);
        }

        using var _ = svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <text x="0" y="16" font-family="SvgSkiaTransientBlocky">G</text>
            </svg>
            """);

        var spans = FindTypefaces(svg, transientFamily, "G");
        Assert.DoesNotContain(
            spans,
            span => string.Equals(span.Typeface?.FamilyName, blockyFamily, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FromSvg_DataUriWoffFontFaceRegistersDocumentTypeface()
    {
        const string family = "SvgSkiaDataUriBlocky";
        var blockyPath = GetW3CResourcePath("Blocky.woff");
        var expectedFamily = GetNativeFontFamilyName(blockyPath);
        var fontData = Convert.ToBase64String(File.ReadAllBytes(blockyPath));
        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 40f, 40f);

        using var _ = svg.FromSvg($$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
              <style>
                @font-face {
                  font-family: {{family}};
                  src: url("data:font/woff;base64,{{fontData}}") format("woff");
                }
              </style>
              <text x="0" y="32" font-family="{{family}}">G</text>
            </svg>
            """);
        var assetLoader = Assert.IsType<SkiaSvgAssetLoader>(svg.AssetLoader);
        using var fontScope = assetLoader.PushDocumentFonts(svg.SourceDocument!);

        AssertDocumentTypefaceFamily(svg, family, "G", expectedFamily);
    }

    [Fact]
    public void PushDocumentFonts_RestoresParentScopeAfterNestedDocument()
    {
        const string parentFamily = "SvgSkiaParentBlocky";
        const string childFamily = "SvgSkiaChildAnglepoise";
        var parentFontPath = GetW3CResourcePath("Blocky.woff");
        var childFontPath = GetW3CResourcePath("anglepoi.woff");
        var parentExpectedFamily = GetNativeFontFamilyName(parentFontPath);
        var childExpectedFamily = GetNativeFontFamilyName(childFontPath);
        var parentDocument = CreateFontFaceDocument(parentFamily, parentFontPath, "G");
        var childDocument = CreateFontFaceDocument(childFamily, childFontPath, "S");
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));

        using (assetLoader.PushDocumentFonts(parentDocument))
        {
            AssertDocumentTypefaceFamily(assetLoader, parentFamily, "G", parentExpectedFamily);
            AssertDocumentTypefaceNotFamily(assetLoader, childFamily, "S", childExpectedFamily);

            using (assetLoader.PushDocumentFonts(childDocument))
            {
                AssertDocumentTypefaceFamily(assetLoader, childFamily, "S", childExpectedFamily);
                AssertDocumentTypefaceNotFamily(assetLoader, parentFamily, "G", parentExpectedFamily);
            }

            AssertDocumentTypefaceFamily(assetLoader, parentFamily, "G", parentExpectedFamily);
            AssertDocumentTypefaceNotFamily(assetLoader, childFamily, "S", childExpectedFamily);
        }

        AssertDocumentTypefaceNotFamily(assetLoader, parentFamily, "G", parentExpectedFamily);
        AssertDocumentTypefaceNotFamily(assetLoader, childFamily, "S", childExpectedFamily);
    }

    private static SKPaint CreateTextPaint(float textSize)
    {
        return new SKPaint
        {
            TextSize = textSize,
            Typeface = SKTypeface.FromFamilyName(
                "sans-serif",
                SKFontStyleWeight.Bold,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright)
        };
    }

    private static List<Svg.Model.TypefaceSpan> FindTypefaces(SKSvg svg, string familyName, string text)
    {
        var assetLoader = Assert.IsType<SkiaSvgAssetLoader>(svg.AssetLoader);
        return FindTypefaces(assetLoader, familyName, text);
    }

    private static List<Svg.Model.TypefaceSpan> FindTypefaces(SkiaSvgAssetLoader assetLoader, string familyName, string text)
    {
        var paint = new SKPaint
        {
            TextSize = 48f,
            Typeface = SKTypeface.FromFamilyName(
                familyName,
                SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright)
        };

        return assetLoader.FindTypefaces(text, paint);
    }

    private static void AssertDocumentTypefaceFamily(SKSvg svg, string familyName, string text, string expectedFamily)
    {
        AssertDocumentTypefaceFamily(Assert.IsType<SkiaSvgAssetLoader>(svg.AssetLoader), familyName, text, expectedFamily);
    }

    private static void AssertDocumentTypefaceFamily(SkiaSvgAssetLoader assetLoader, string familyName, string text, string expectedFamily)
    {
        var spans = FindTypefaces(assetLoader, familyName, text);
        Assert.NotEmpty(spans);
        Assert.Contains(
            spans,
            span => string.Equals(span.Typeface?.FamilyName, expectedFamily, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(span.Typeface?.FamilyName, familyName, StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertDocumentTypefaceNotFamily(SkiaSvgAssetLoader assetLoader, string familyName, string text, string expectedFamily)
    {
        var spans = FindTypefaces(assetLoader, familyName, text);
        Assert.DoesNotContain(
            spans,
            span => string.Equals(span.Typeface?.FamilyName, expectedFamily, StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertRunTypefaceFamily(SKSvg svg, string familyName, string text, string expectedFamily)
    {
        var assetLoader = Assert.IsType<SkiaSvgAssetLoader>(svg.AssetLoader);
        var paint = new SKPaint
        {
            TextSize = 48f,
            Typeface = SKTypeface.FromFamilyName(
                familyName,
                SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright)
        };

        var runTypeface = assetLoader.FindRunTypeface(text, paint);
        Assert.NotNull(runTypeface);
        Assert.Equal(expectedFamily, runTypeface!.FamilyName, ignoreCase: true);
    }

    private static string GetNativeFontFamilyName(string path)
    {
        using var typeface = NativeTypeface.FromFile(path);
        Assert.NotNull(typeface);
        Assert.NotEqual(IntPtr.Zero, typeface!.Handle);
        Assert.False(string.IsNullOrWhiteSpace(typeface.FamilyName));
        return typeface.FamilyName;
    }

    private static SvgDocument CreateFontFaceDocument(string familyName, string fontPath, string text)
    {
        var fontUri = new Uri(Path.GetFullPath(fontPath)).AbsoluteUri;
        return SvgService.FromSvg(
            $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
              <style>
                @font-face {
                  font-family: {{familyName}};
                  src: url("{{fontUri}}") format("woff");
                }
              </style>
              <text x="0" y="32" font-family="{{familyName}}">{{text}}</text>
            </svg>
            """,
            null)!;
    }

    private static string GetW3CSvgPath(string name)
        => Path.Combine(
            "..",
            "..",
            "..",
            "..",
            "..",
            "externals",
            "W3C_SVG_11_TestSuite",
            "W3C_SVG_11_TestSuite",
            "svg",
            $"{name}.svg");

    private static string GetW3CResourcePath(string name)
        => Path.Combine(
            "..",
            "..",
            "..",
            "..",
            "..",
            "externals",
            "W3C_SVG_11_TestSuite",
            "W3C_SVG_11_TestSuite",
            "resources",
            name);

    private sealed class CountingTypefaceProvider : ITypefaceProvider
    {
        public int CallCount { get; private set; }

        public NativeTypeface? FromFamilyName(
            string fontFamily,
            NativeTypefaceWeight fontWeight,
            NativeTypefaceWidth fontWidth,
            NativeTypefaceSlant fontStyle)
        {
            CallCount++;
            return null;
        }
    }
}

#pragma warning restore CS0618
