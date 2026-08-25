#pragma warning disable CS0618 // Shim paint keeps deprecated SKPaint text/typeface surface for compatibility

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    public void MeasureText_IncludesStrokeExpansionInBounds()
    {
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var fillPaint = CreateTextPaint(48f);
        var strokePaint = CreateTextPaint(48f);
        strokePaint.Style = SKPaintStyle.Stroke;
        strokePaint.StrokeWidth = 18f;

        var fillBounds = default(SKRect);
        var fillAdvance = assetLoader.MeasureText("Stroke", fillPaint, ref fillBounds);
        var strokeBounds = default(SKRect);
        var strokeAdvance = assetLoader.MeasureText("Stroke", strokePaint, ref strokeBounds);

        Assert.Equal(fillAdvance, strokeAdvance, 3);
        Assert.True(strokeBounds.Left < fillBounds.Left);
        Assert.True(strokeBounds.Top < fillBounds.Top);
        Assert.True(strokeBounds.Right > fillBounds.Right);
        Assert.True(strokeBounds.Bottom > fillBounds.Bottom);
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
    public void FindTypefaces_SkipsCustomProviderTypefaceMissingRequestedGlyph()
    {
        const string family = "SvgSkiaGlyphFallback";
        using var latinTypeface = OpenNativeTypeface(GetResvgFontPath("NotoSans-Regular.ttf"));
        using var devanagariTypeface = OpenNativeTypeface(GetResvgFontPath("NotoSansDevanagari-Regular.ttf"));
        AssertGlyphCoverage(latinTypeface, 0x0915, expected: false);
        AssertGlyphCoverage(devanagariTypeface, 0x0915, expected: true);
        var latinProvider = new CountingTypefaceProvider(latinTypeface, family);
        var devanagariProvider = new CountingTypefaceProvider(devanagariTypeface, family);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings
        {
            TypefaceProviders = new List<ITypefaceProvider> { latinProvider, devanagariProvider }
        }));

        var spans = FindTypefaces(assetLoader, family, "\u0915");

        var span = Assert.Single(spans);
        Assert.Equal("\u0915", span.Text);
        Assert.True(latinProvider.CallCount > 0);
        Assert.True(devanagariProvider.CallCount > 0);
    }

    [Fact]
    public void FindTypefaces_KeepsNoBreakSpaceWithCurrentFontWhenGlyphExists()
    {
        const string family = "SvgSkiaGlyphGlue";
        using var latinTypeface = OpenNativeTypeface(GetResvgFontPath("NotoSans-Regular.ttf"));
        using var devanagariTypeface = OpenNativeTypeface(GetResvgFontPath("NotoSansDevanagari-Regular.ttf"));
        AssertGlyphCoverage(latinTypeface, 0x00A0, expected: true);
        AssertGlyphCoverage(latinTypeface, 0x0915, expected: false);
        AssertGlyphCoverage(devanagariTypeface, 0x00A0, expected: true);
        AssertGlyphCoverage(devanagariTypeface, 0x0915, expected: true);
        var latinProvider = new CountingTypefaceProvider(latinTypeface, family);
        var devanagariProvider = new CountingTypefaceProvider(devanagariTypeface, family);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings
        {
            TypefaceProviders = new List<ITypefaceProvider> { latinProvider, devanagariProvider }
        }));

        var spans = FindTypefaces(assetLoader, family, "\u0915\u00A0");

        var span = Assert.Single(spans);
        Assert.Equal("\u0915\u00A0", span.Text);
    }

    [Fact]
    public void FindRunTypeface_DoesNotReturnCandidateTypefaceMissingAnyRequestedGlyph()
    {
        const string partialFamily = "SvgSkiaRunPartial";
        const string latinFamily = "SvgSkiaRunLatin";
        using var partialTypeface = OpenNativeTypeface(GetResvgFontPath("CFF-and-SBIX.otf"));
        using var latinTypeface = OpenNativeTypeface(GetResvgFontPath("NotoSans-Regular.ttf"));
        AssertGlyphCoverage(partialTypeface, 'A', expected: true);
        AssertGlyphCoverage(partialTypeface, 'G', expected: false);
        AssertGlyphCoverage(latinTypeface, 'A', expected: true);
        AssertGlyphCoverage(latinTypeface, 'G', expected: true);
        var partialProvider = new CountingTypefaceProvider(partialTypeface, partialFamily);
        var latinProvider = new CountingTypefaceProvider(latinTypeface, latinFamily);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings
        {
            TypefaceProviders = new List<ITypefaceProvider> { partialProvider, latinProvider }
        }));

        var runTypeface = FindRunTypeface(assetLoader, $"{partialFamily}, {latinFamily}", "AG");

        Assert.NotNull(runTypeface);
        Assert.NotEqual(partialFamily, runTypeface!.FamilyName);
        Assert.True(partialProvider.CallCount > 0);
        Assert.True(latinProvider.CallCount > 0);
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
        var expectedFamily = GetDocumentFontFamilyName("Blocky", GetW3CResourcePath("Blocky.woff"), "G");
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
        var expectedFamily = GetDocumentFontFamilyName("BlockyWoff", GetW3CResourcePath("Blocky.woff"), "G");
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
        var expectedFamily = GetDocumentFontFamilyName("anglepoise", GetW3CResourcePath("anglepoi.woff"), "S");
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
        var blockyFamily = GetDocumentFontFamilyName(transientFamily, GetW3CResourcePath("Blocky.woff"), "G");
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
        var expectedFamily = GetDocumentFontFamilyName(family, blockyPath, "G");
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
    public void FromSvg_DataUriWoffFontFaceWithoutFormatRegistersDocumentTypeface()
    {
        const string family = "SvgSkiaDataUriNoFormatBlocky";
        var blockyPath = GetW3CResourcePath("Blocky.woff");
        var expectedFamily = GetDocumentFontFamilyName(family, blockyPath, "G");
        var fontData = Convert.ToBase64String(File.ReadAllBytes(blockyPath));
        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 40f, 40f);

        using var _ = svg.FromSvg($$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
              <style>
                @font-face {
                  font-family: {{family}};
                  src: url("data:font/woff;base64,{{fontData}}");
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
    public void FromSvg_PercentEncodedDataUriWoffFontFaceRegistersDocumentTypeface()
    {
        const string family = "SvgSkiaDataUriPercentBlocky";
        var blockyPath = GetW3CResourcePath("Blocky.woff");
        var expectedFamily = GetDocumentFontFamilyName(family, blockyPath, "G");
        var fontData = PercentEncodeDataBytes(File.ReadAllBytes(blockyPath));
        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 40f, 40f);

        using var _ = svg.FromSvg($$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
              <style>
                @font-face {
                  font-family: {{family}};
                  src: url("data:font/woff,{{fontData}}") format("woff");
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
    public void FindRunTypeface_PreservesDocumentFontFamilyOverride()
    {
        const string family = "SvgSkiaAliasBlocky";
        var document = CreateFontFaceDocument(family, GetW3CResourcePath("Blocky.woff"), "G");
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        using var fontScope = assetLoader.PushDocumentFonts(document);

        var spans = FindTypefaces(assetLoader, family, "G");
        Assert.Contains(
            spans,
            span => string.Equals(span.Typeface?.FamilyName, family, StringComparison.OrdinalIgnoreCase));

        var runTypeface = FindRunTypeface(assetLoader, family, "G");

        Assert.NotNull(runTypeface);
        Assert.Equal(family, runTypeface!.FamilyName, ignoreCase: true);
    }

    [Fact]
    public void FromSvg_FontFaceSrcFallbackUsesLaterSupportedSource()
    {
        const string family = "SvgSkiaFallbackSrcBlocky";
        var blockyPath = GetW3CResourcePath("Blocky.woff");
        var expectedFamily = GetDocumentFontFamilyName(family, blockyPath, "G");
        var blockyUri = new Uri(Path.GetFullPath(blockyPath)).AbsoluteUri;
        var unsupportedFontData = Convert.ToBase64String(new byte[] { 0, 1, 2, 3 });
        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 40f, 40f);

        using var _ = svg.FromSvg($$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
              <style>
                @font-face {
                  font-family: {{family}};
                  src: url("data:font/woff2;base64,{{unsupportedFontData}}") format("woff2"),
                       url("{{blockyUri}}") format("woff");
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
    public void OpenPath_ImportedStylesheetFontFaceRegistersDocumentTypeface()
    {
        const string family = "SvgSkiaImportedCssBlocky";
        var tempDirectory = Directory.CreateTempSubdirectory("SvgSkiaFontImport");

        try
        {
            var blockyPath = WriteTempBlockyFont(tempDirectory.FullName);
            var expectedFamily = GetDocumentFontFamilyName(family, blockyPath, "G");
            var cssPath = Path.Combine(tempDirectory.FullName, "fonts.css");
            var svgPath = Path.Combine(tempDirectory.FullName, "source.svg");
            File.WriteAllText(cssPath, $$"""
                @font-face {
                  font-family: {{family}};
                  src: url("Blocky.woff") format("woff");
                }
                """);
            File.WriteAllText(svgPath, $$"""
                <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
                  <style>@import url("fonts.css");</style>
                  <text x="0" y="32" font-family="{{family}}">G</text>
                </svg>
                """);

            using var svg = new SKSvg();
            svg.Settings.EnableSvgFonts = false;
            svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 40f, 40f);
            using var _ = svg.Load(svgPath);
            var assetLoader = Assert.IsType<SkiaSvgAssetLoader>(svg.AssetLoader);
            using var fontScope = assetLoader.PushDocumentFonts(svg.SourceDocument!);

            AssertDocumentTypefaceFamily(svg, family, "G", expectedFamily);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void OpenPath_LinkedStylesheetFontFaceRegistersDocumentTypeface()
    {
        const string family = "SvgSkiaLinkedCssBlocky";
        var tempDirectory = Directory.CreateTempSubdirectory("SvgSkiaFontLink");

        try
        {
            var blockyPath = WriteTempBlockyFont(tempDirectory.FullName);
            var expectedFamily = GetDocumentFontFamilyName(family, blockyPath, "G");
            var cssPath = Path.Combine(tempDirectory.FullName, "fonts.css");
            var svgPath = Path.Combine(tempDirectory.FullName, "source.svg");
            File.WriteAllText(cssPath, $$"""
                @font-face {
                  font-family: {{family}};
                  src: url("Blocky.woff") format("woff");
                }
                """);
            File.WriteAllText(svgPath, $$"""
                <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
                  <link rel="stylesheet" type="text/css" href="fonts.css" />
                  <text x="0" y="32" font-family="{{family}}">G</text>
                </svg>
                """);

            using var svg = new SKSvg();
            svg.Settings.EnableSvgFonts = false;
            svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 40f, 40f);
            using var _ = svg.Load(svgPath);
            var assetLoader = Assert.IsType<SkiaSvgAssetLoader>(svg.AssetLoader);
            using var fontScope = assetLoader.PushDocumentFonts(svg.SourceDocument!);

            AssertDocumentTypefaceFamily(svg, family, "G", expectedFamily);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("screen", true)]
    [InlineData("print", false)]
    public void OpenPath_LinkedStylesheetFontFaceRespectsMediaContext(string media, bool shouldApply)
    {
        var family = $"SvgSkiaMediaCssBlocky{media}";
        var tempDirectory = Directory.CreateTempSubdirectory("SvgSkiaFontMedia");

        try
        {
            var blockyPath = WriteTempBlockyFont(tempDirectory.FullName);
            var expectedFamily = GetDocumentFontFamilyName(family, blockyPath, "G");
            var cssPath = Path.Combine(tempDirectory.FullName, "fonts.css");
            var svgPath = Path.Combine(tempDirectory.FullName, "source.svg");
            File.WriteAllText(cssPath, $$"""
                @media {{media}} {
                  @font-face {
                    font-family: {{family}};
                    src: url("Blocky.woff") format("woff");
                  }
                }
                """);
            File.WriteAllText(svgPath, $$"""
                <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
                  <link rel="stylesheet" type="text/css" href="fonts.css" />
                  <text x="0" y="32" font-family="{{family}}">G</text>
                </svg>
                """);

            using var svg = new SKSvg();
            svg.Settings.EnableSvgFonts = false;
            svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 40f, 40f);
            using var _ = svg.Load(svgPath);
            var assetLoader = Assert.IsType<SkiaSvgAssetLoader>(svg.AssetLoader);
            using var fontScope = assetLoader.PushDocumentFonts(svg.SourceDocument!);

            if (shouldApply)
            {
                AssertDocumentTypefaceFamily(svg, family, "G", expectedFamily);
            }
            else
            {
                AssertDocumentTypefaceNotFamily(assetLoader, family, "G", expectedFamily);
            }
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void PushDocumentFonts_RestoresParentScopeAfterNestedDocument()
    {
        const string parentFamily = "SvgSkiaParentBlocky";
        const string childFamily = "SvgSkiaChildAnglepoise";
        var parentFontPath = GetW3CResourcePath("Blocky.woff");
        var childFontPath = GetW3CResourcePath("anglepoi.woff");
        var parentExpectedFamily = GetDocumentFontFamilyName(parentFamily, parentFontPath, "G");
        var childExpectedFamily = GetDocumentFontFamilyName(childFamily, childFontPath, "S");
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

    [Fact]
    public void FindTypefaces_KeepsAdvancesWhenNoFontClaimsTheCharacter()
    {
        // A private-use codepoint that no installed font is expected to claim, so character
        // fallback finds nothing. The span must still carry an advance: the callers position
        // consecutive spans by accumulating these values, so a zero advance draws every span
        // of the run at the same x. On a platform whose font manager matches nothing at all -
        // browser-wasm has a single embedded face and answers no family or character query -
        // every span of every label took that path and each label rendered as one dense mark.
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var paint = CreateTextPaint(24f);

        var spans = assetLoader.FindTypefaces("A\uE000B", paint);

        Assert.NotEmpty(spans);
        Assert.All(spans, span => Assert.True(
            span.Advance > 0f,
            $"span \"{span.Text}\" reported an advance of {span.Advance}"));
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
        var runTypeface = FindRunTypeface(assetLoader, familyName, text);
        Assert.NotNull(runTypeface);
        Assert.Equal(expectedFamily, runTypeface!.FamilyName, ignoreCase: true);
    }

    private static SKTypeface? FindRunTypeface(SkiaSvgAssetLoader assetLoader, string familyName, string text)
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

        return assetLoader.FindRunTypeface(text, paint);
    }

    private static string GetDocumentFontFamilyName(string familyName, string fontPath, string text)
    {
        var document = CreateFontFaceDocument(familyName, fontPath, text);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        using var fontScope = assetLoader.PushDocumentFonts(document);
        var typeface = FindRunTypeface(assetLoader, familyName, text);

        Assert.NotNull(typeface);
        Assert.False(string.IsNullOrWhiteSpace(typeface.FamilyName));
        return typeface.FamilyName;
    }

    private static string PercentEncodeDataBytes(byte[] bytes)
    {
        const string Hex = "0123456789ABCDEF";
        var builder = new StringBuilder(bytes.Length * 3);
        foreach (var value in bytes)
        {
            builder.Append('%');
            builder.Append(Hex[value >> 4]);
            builder.Append(Hex[value & 0x0F]);
        }

        return builder.ToString();
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

    private static string WriteTempBlockyFont(string directory)
    {
        var fontPath = Path.Combine(directory, "Blocky.woff");
        File.Copy(GetW3CResourcePath("Blocky.woff"), fontPath, overwrite: true);
        return fontPath;
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

    private static string GetResvgFontPath(string name)
        => Path.Combine(
            "..",
            "..",
            "..",
            "..",
            "..",
            "externals",
            "resvg",
            "crates",
            "resvg",
            "tests",
            "fonts",
            name);

    private static NativeTypeface OpenNativeTypeface(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Assert.True(File.Exists(fullPath), $"Font asset not found: {fullPath}");

        using var stream = File.OpenRead(fullPath);
        var typeface = NativeTypeface.FromStream(stream);
        Assert.True(typeface is not null, $"Font asset could not be decoded: {fullPath}");
        return typeface!;
    }

    private static void AssertGlyphCoverage(NativeTypeface typeface, int codepoint, bool expected)
    {
        using var font = new SkiaSharp.SKFont(typeface);
        Assert.Equal(expected, font.ContainsGlyph(codepoint));
    }

    private sealed class CountingTypefaceProvider : ITypefaceProvider
    {
        private static readonly char[] s_fontFamilyTrim = { '\'' };
        private readonly NativeTypeface? _typeface;
        private readonly HashSet<string>? _familyNames;

        public CountingTypefaceProvider()
        {
        }

        public CountingTypefaceProvider(NativeTypeface typeface, params string[] familyNames)
        {
            _typeface = typeface;
            _familyNames = new HashSet<string>(familyNames, StringComparer.OrdinalIgnoreCase);
        }

        public int CallCount { get; private set; }

        public NativeTypeface? FromFamilyName(
            string fontFamily,
            NativeTypefaceWeight fontWeight,
            NativeTypefaceWidth fontWidth,
            NativeTypefaceSlant fontStyle)
        {
            CallCount++;
            if (_typeface is null ||
                _typeface.Handle == IntPtr.Zero ||
                _typeface.FontStyle.Weight != (int)fontWeight ||
                _typeface.FontStyle.Width != (int)fontWidth ||
                _typeface.FontStyle.Slant != fontStyle)
            {
                return null;
            }

            if (_familyNames is null || _familyNames.Count == 0)
            {
                return _typeface;
            }

            var families = fontFamily.Split(',');
            for (var i = 0; i < families.Length; i++)
            {
                if (_familyNames.Contains(families[i].Trim().Trim(s_fontFamilyTrim)))
                {
                    return _typeface;
                }
            }

            return null;
        }
    }
}

#pragma warning restore CS0618
