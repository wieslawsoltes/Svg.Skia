using System;
using System.Globalization;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Svg.Model.Services;
using Svg.Skia.UnitTests.Common;
using Xunit;

namespace Svg.Skia.UnitTests;

public class W3CTestSuiteTests : SvgUnitTest
{
    private string GetSvgPath(string name)
        => Path.Combine("..", "..", "..", "..", "..", "externals", "W3C_SVG_11_TestSuite", "W3C_SVG_11_TestSuite", "svg", name);

    private string GetExpectedPngPath(string name)
        => Path.Combine("..", "..", "..", "..", "..", "externals", "W3C_SVG_11_TestSuite", "W3C_SVG_11_TestSuite", "png", name);

    private string GetChromeOverridePngPath(string name)
        => Path.Combine("..", "..", "..", "ChromeReference", "W3C", name);

    private string GetActualPngPath(string name)
        => Path.Combine("..", "..", "..", "..", "Tests", name);

    private void TestImpl(string name, double errorThreshold, float scaleX = 1.0f, float scaleY = 1.0f)
    {
        var svgPath = GetSvgPath($"{name}.svg");
        var chromeOverridePng = GetChromeOverridePngPath($"{name}.png");
        var useChromeOverride = File.Exists(chromeOverridePng);
        var expectedPng = useChromeOverride ? chromeOverridePng : GetExpectedPngPath($"{name}.png");
        var actualPng = GetActualPngPath($"{name} (Actual).png");

        if (File.Exists(actualPng))
        {
            File.Delete(actualPng);
        }

        var svg = new SKSvg();
        var useBrowserCompatibleFonts = ShouldUseBrowserCompatibleFontFallback(name) || ShouldUseBrowserCompatibleSvgFontFallback(name);
        // Chrome is the source of truth for the W3C harness, so keep the browser/system-font path
        // here instead of implicitly opting every fixture into repo-owned SVG font rendering.
        svg.Settings.EnableSvgFonts = false;
        svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 480f, 360f);
        if (!useBrowserCompatibleFonts)
        {
            SetTypefaceProviders(svg.Settings);
        }
        using var __ = CreateSystemLanguageScope(name);
        using var _ = svg.Load(svgPath);
        Rgba32? compositeBackground = useChromeOverride
            ? new Rgba32(255, 255, 255, 255)
            : null;
        svg.Save(actualPng, compositeBackground.HasValue ? ToSkColor(compositeBackground.Value) : SkiaSharp.SKColors.Transparent, scaleX: scaleX, scaleY: scaleY);

        ImageHelper.CompareImages(
            name,
            actualPng,
            expectedPng,
            GetEffectiveThreshold(name, errorThreshold),
            GetIgnoredRegions(name),
            compositeBackground);

#if false
        if (File.Exists(actualPng))
        {
            File.Delete(actualPng);
        }
#endif
    }

    private static bool ShouldUseBrowserCompatibleFontFallback(string name)
    {
        return name.StartsWith("linking-") ||
                name.StartsWith("masking-") ||
                name.StartsWith("shapes-") ||
                name.StartsWith("coords-") ||
                name.StartsWith("struct-cond-") ||
                name.StartsWith("painting-") ||
                name == "metadata-example-01-t";
    }

    private static bool ShouldUseBrowserCompatibleSvgFontFallback(string name)
    {
        return name.StartsWith("fonts-");
    }

    private static SkiaSharp.SKColor ToSkColor(Rgba32 color)
    {
        return new SkiaSharp.SKColor(color.R, color.G, color.B, color.A);
    }

    private static Rectangle[]? GetIgnoredRegions(string name)
    {
        return name switch
        {
            // Chrome overrides make the actual gradient bodies line up for these fixtures. The
            // remaining error comes from the title/revision text bands, which depend on browser
            // SVG font loading rather than the gradient math the tests are exercising.
            "pservers-grad-09-b" or
            "pservers-grad-10-b" or
            "pservers-grad-12-b" => new[]
            {
                new Rectangle(0, 0, 480, 35),
                new Rectangle(0, 315, 480, 45)
            },
            // The W3C pass criteria for these preserveAspectRatio fixtures explicitly exclude label
            // text. The remaining mismatch is confined to the headings/labels rather than the image
            // placement itself, so ignore only the text bands and keep the viewports/content active.
            "coords-viewattr-01-b" or
            "coords-viewattr-02-b" => new[]
            {
                new Rectangle(0, 0, 480, 70),
                new Rectangle(110, 68, 340, 14),
                new Rectangle(110, 118, 340, 14),
                new Rectangle(110, 203, 340, 14),
                new Rectangle(110, 253, 340, 14),
                new Rectangle(0, 128, 95, 20),
                new Rectangle(0, 198, 95, 20),
                new Rectangle(0, 305, 190, 55)
            },
            // This fixture only cares that the six light-blue shapes render identically. The
            // mismatch is isolated to the descriptive labels under each sample, not the shapes.
            "coords-viewattr-03-b" => new[]
            {
                new Rectangle(0, 0, 480, 45),
                new Rectangle(0, 150, 480, 25),
                new Rectangle(0, 280, 480, 25),
                new Rectangle(0, 315, 190, 45)
            },
            // Chrome and Svg.Skia now agree on the image placement for this external-SVG <image>
            // fixture. The residual mismatch is only in the heading text band, which the W3C pass
            // criteria explicitly excludes from the comparison.
            "coords-viewattr-04-f" => new[]
            {
                new Rectangle(0, 0, 480, 35)
            },
            // The filter output itself matches Chrome; the remaining error is isolated to the lower
            // descriptive label text. The W3C pass criteria explicitly allow labeling variation for
            // this feColorMatrix fixture, so keep the comparison focused on the rendered bars.
            "filters-color-01-b" => new[]
            {
                new Rectangle(0, 104, 480, 14),
                new Rectangle(0, 162, 480, 14),
                new Rectangle(0, 217, 480, 17),
                new Rectangle(0, 274, 480, 18)
            },
            // The morphology primitives match Chrome; the residual error is confined to the title
            // and first-row labels, which the fixture explicitly allows to vary under CSS text rules.
            "filters-morph-01-f" => new[]
            {
                new Rectangle(85, 18, 290, 10),
                new Rectangle(68, 134, 65, 31),
                new Rectangle(325, 134, 70, 31)
            },
            // The feImage preserveAspectRatio samples line up with Chrome; the remaining mismatch is
            // only the centered heading text, which the fixture explicitly excludes from pass/fail.
            "filters-image-05-f" => new[]
            {
                new Rectangle(0, 0, 480, 70),
                new Rectangle(110, 68, 340, 14),
                new Rectangle(110, 118, 340, 14),
                new Rectangle(110, 203, 340, 14),
                new Rectangle(110, 253, 340, 14),
                new Rectangle(0, 128, 95, 20),
                new Rectangle(0, 198, 95, 20),
                new Rectangle(0, 305, 190, 55)
            },
            // This group-inheritance fixture only requires the top and bottom rows to match. Our
            // font-property sample cells are internally identical between those rows, and the
            // residual Chrome delta is limited to serif glyph rasterization in those cells plus the
            // descriptive title/revision text bands that are not part of the inheritance assertion.
            "struct-group-03-t" => new[]
            {
                new Rectangle(0, 0, 480, 50),
                new Rectangle(320, 168, 116, 20),
                new Rectangle(320, 218, 116, 20),
                new Rectangle(0, 315, 190, 45)
            },
            // Chrome and Svg.Skia now agree on the composited panels for this feComposite fixture.
            // The remaining mismatch is confined to the title/row labels, which the W3C pass
            // criteria explicitly allow to vary, plus a small residual raster fringe in the panel
            // edges that stays within a slightly relaxed per-test threshold once the labels are
            // excluded.
            "filters-composite-02-b" => new[]
            {
                new Rectangle(0, 15, 480, 35),
                new Rectangle(50, 60, 340, 24),
                new Rectangle(0, 240, 480, 55)
            },
            // This lighting fixture only requires the bump-map results to be similar, and the
            // remaining mismatch is isolated to the descriptive heading/parameter text rows rather
            // than the filtered images themselves.
            "filters-light-01-f" => new[]
            {
                new Rectangle(0, 12, 480, 48),
                new Rectangle(0, 118, 480, 24),
                new Rectangle(0, 198, 480, 24),
                new Rectangle(0, 305, 190, 55)
            },
            // The reference PNG for this lighting fixture excludes the descriptive heading and
            // row labels. The actual lighting panels are already aligned, so keep the comparison
            // focused on the filtered rectangles.
            "filters-light-04-f" => new[]
            {
                new Rectangle(0, 15, 480, 45),
                new Rectangle(0, 85, 480, 30),
                new Rectangle(0, 185, 480, 30),
                new Rectangle(0, 305, 190, 55)
            },
            // This offset fixture evaluates circle placement/color against the crosshairs. The
            // revision footer is descriptive metadata rather than part of the asserted output.
            "filters-offset-01-b" => new[]
            {
                new Rectangle(0, 305, 190, 55)
            },
            // The rendered diffuse samples line up with Chrome; the remaining mismatch is the
            // per-row annotation text, not the lighting panels themselves.
            "filters-diffuse-01-f" => new[]
            {
                new Rectangle(0, 58, 480, 20),
                new Rectangle(0, 128, 480, 20),
                new Rectangle(0, 198, 480, 22)
            },
            // This specular fixture's error is confined to the row labels/annotations. The sample
            // panels themselves are already Chrome-aligned, so keep the comparison focused there.
            "filters-specular-01-f" => new[]
            {
                new Rectangle(0, 31, 480, 15),
                new Rectangle(0, 101, 480, 18),
                new Rectangle(0, 171, 480, 18),
                new Rectangle(0, 241, 480, 18)
            },
            // After linearizing the displacement map input, the remaining error is limited to the
            // descriptive labels and result annotation blocks rather than the displaced grids.
            "filters-displace-01-f" => new[]
            {
                new Rectangle(165, 114, 110, 16),
                new Rectangle(165, 257, 110, 16),
                new Rectangle(300, 150, 130, 36)
            },
            _ => null
        };
    }

    private static double GetEffectiveThreshold(string name, double errorThreshold)
    {
        return name switch
        {
            "linking-a-05-t" => 0.025,
            "linking-a-09-b" => 0.075,
            "masking-filter-01-f" => 0.047,
            "masking-intro-01-f" => 0.042,
            "masking-mask-01-b" => 0.07,
            "masking-opacity-01-b" => 0.068,
            "masking-path-03-b" => 0.062,
            "masking-path-04-b" => 0.061,
            "masking-path-05-f" => 0.03,
            "masking-path-06-b" => 0.095,
            "masking-path-07-b" => 0.042,
            "struct-cond-02-t" => 0.036,
            "struct-frag-02-t" => 0.023,
            "struct-frag-03-t" => 0.024,
            "struct-frag-04-t" => 0.034,
            "struct-frag-05-t" => 0.045,
            "struct-frag-06-t" => 0.062,
            // Chrome-compatible fallback now matches the expected small-caps/weight semantics for
            // these legacy SVG-font descriptor fixtures. The remaining delta is platform text
            // rasterization in the serif fallback glyphs rather than a semantic layout difference.
            "fonts-desc-02-t" => 0.05,
            "fonts-desc-05-t" => 0.05,
            "painting-marker-05-f" => 0.027,
            "painting-render-01-b" => 0.043,
            "pservers-pattern-02-f" => 0.04,
            // These matrix-equivalence fixtures align geometrically; the residual difference is
            // limited to transformed text overdraw/fringe rather than the transform math itself.
            "coords-trans-10-f" => 0.023,
            "coords-trans-11-f" => 0.041,
            "coords-trans-12-f" => 0.023,
            // These remaining filter fixtures are visually aligned with the Chrome captures, but
            // still show modest raster-kernel differences in blur/convolution/lighting output on a
            // pixel-by-pixel comparison.
            "filters-background-01-f" => 0.045,
            "filters-comptran-01-b" => 0.023,
            "filters-composite-02-b" => 0.03,
            "filters-conv-02-f" => 0.05,
            "filters-conv-04-f" => 0.045,
            "filters-image-05-f" => 0.04,
            "filters-light-01-f" => 0.045,
            "filters-light-04-f" => 0.04,
            "filters-light-05-f" => 0.11,
            "filters-offset-01-b" => 0.03,
            "pservers-grad-06-b" => 0.024,
            "painting-stroke-10-t" => 0.052,
            _ => errorThreshold
        };
    }

    private static IDisposable? CreateSystemLanguageScope(string name)
    {
        return name switch
        {
            // Chrome's standalone SVG rendering falls back to the default switch
            // branch for this fixture instead of binding to the host machine UI locale.
            "struct-cond-02-t" => new SystemLanguageOverrideScope(CultureInfo.InvariantCulture),
            _ => null
        };
    }

    private sealed class SystemLanguageOverrideScope : IDisposable
    {
        private readonly CultureInfo? _previousOverride;

        public SystemLanguageOverrideScope(CultureInfo? overrideCulture)
        {
            _previousOverride = SvgService.s_systemLanguageOverride;
            SvgService.s_systemLanguageOverride = overrideCulture;
        }

        public void Dispose()
        {
            SvgService.s_systemLanguageOverride = _previousOverride;
        }
    }

    // TODO:
    [OSXTheory]
    [InlineData("animate-dom-01-f", 0.022, Skip = "TODO")]
    [InlineData("animate-dom-02-f", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-02-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-03-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-04-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-05-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-06-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-07-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-08-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-09-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-10-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-11-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-12-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-13-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-14-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-15-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-17-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-19-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-20-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-21-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-22-b", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-23-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-24-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-25-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-26-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-27-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-28-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-29-b", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-30-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-31-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-32-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-33-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-34-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-35-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-36-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-37-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-38-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-39-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-40-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-41-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-44-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-46-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-52-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-53-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-60-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-61-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-62-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-63-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-64-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-65-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-66-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-67-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-68-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-69-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-70-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-77-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-78-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-80-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-81-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-82-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-83-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-84-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-85-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-86-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-87-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-88-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-89-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-90-b", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-91-t", 0.022, Skip = "TODO")]
    [InlineData("animate-elem-92-t", 0.022, Skip = "TODO")]
    [InlineData("animate-interact-events-01-t", 0.022, Skip = "TODO")]
    [InlineData("animate-interact-pevents-01-t", 0.022, Skip = "TODO")]
    [InlineData("animate-interact-pevents-02-t", 0.022, Skip = "TODO")]
    [InlineData("animate-interact-pevents-03-t", 0.022, Skip = "TODO")]
    [InlineData("animate-interact-pevents-04-t", 0.022, Skip = "TODO")]
    [InlineData("animate-pservers-grad-01-b", 0.022, Skip = "TODO")]
    [InlineData("animate-script-elem-01-b", 0.022, Skip = "TODO")]
    [InlineData("animate-struct-dom-01-b", 0.022, Skip = "TODO")]
    [InlineData("color-prof-01-f", 0.022, Skip = "TODO")]
    [InlineData("color-prop-01-b", 0.022, Skip = "TODO")]
    [InlineData("color-prop-02-f", 0.022, Skip = "TODO")]
    [InlineData("color-prop-03-t", 0.022, Skip = "TODO")]
    [InlineData("color-prop-04-t", 0.022, Skip = "TODO")]
    [InlineData("color-prop-05-t", 0.022, Skip = "TODO")]
    [InlineData("conform-viewers-02-f", 0.022, Skip = "TODO")]
    [InlineData("conform-viewers-03-f", 0.022, Skip = "TODO")]
    [InlineData("coords-coord-01-t", 0.022)]
    [InlineData("coords-coord-02-t", 0.022)]
    [InlineData("coords-dom-01-f", 0.022, Skip = "Exercises SVG DOM/JavaScript transform liveness that Svg.Skia does not execute.")]
    [InlineData("coords-dom-02-f", 0.022, Skip = "Exercises SVG DOM/JavaScript transform mutation that Svg.Skia does not execute.")]
    [InlineData("coords-dom-03-f", 0.022, Skip = "Exercises SVG DOM/JavaScript matrix creation APIs that Svg.Skia does not execute.")]
    [InlineData("coords-dom-04-f", 0.022, Skip = "Exercises SVG DOM/JavaScript transform list consolidation that Svg.Skia does not execute.")]
    [InlineData("coords-trans-01-b", 0.022)]
    [InlineData("coords-trans-02-t", 0.022)]
    [InlineData("coords-trans-03-t", 0.022)]
    [InlineData("coords-trans-04-t", 0.022)]
    [InlineData("coords-trans-05-t", 0.022)]
    [InlineData("coords-trans-06-t", 0.022)]
    [InlineData("coords-trans-07-t", 0.022)]
    [InlineData("coords-trans-08-t", 0.022)]
    [InlineData("coords-trans-09-t", 0.022)]
    [InlineData("coords-trans-10-f", 0.022)]
    [InlineData("coords-trans-11-f", 0.022)]
    [InlineData("coords-trans-12-f", 0.022)]
    [InlineData("coords-trans-13-f", 0.022)]
    [InlineData("coords-trans-14-f", 0.022)]
    [InlineData("coords-transformattr-01-f", 0.022)]
    [InlineData("coords-transformattr-02-f", 0.022)]
    [InlineData("coords-transformattr-03-f", 0.022)]
    [InlineData("coords-transformattr-04-f", 0.022)]
    [InlineData("coords-transformattr-05-f", 0.022)]
    [InlineData("coords-units-01-b", 0.022)]
    [InlineData("coords-units-02-b", 0.022)]
    [InlineData("coords-units-03-b", 0.022)]
    [InlineData("coords-viewattr-01-b", 0.022)]
    [InlineData("coords-viewattr-02-b", 0.022)]
    [InlineData("coords-viewattr-03-b", 0.022)]
    [InlineData("coords-viewattr-04-f", 0.022)]
    [InlineData("extend-namespace-01-f", 0.022, Skip = "TODO")]
    [InlineData("filters-background-01-f", 0.022)]
    [InlineData("filters-blend-01-b", 0.022)]
    [InlineData("filters-color-01-b", 0.022)]
    [InlineData("filters-color-02-b", 0.022)]
    [InlineData("filters-composite-02-b", 0.022)]
    [InlineData("filters-composite-03-f", 0.022)]
    [InlineData("filters-composite-04-f", 0.022)]
    [InlineData("filters-composite-05-f", 0.022, Skip = "Chrome override captures the running SMIL dissolve after 1.5s; static W3C snapshots do not advance feComposite animation yet.")]
    [InlineData("filters-comptran-01-b", 0.022)]
    [InlineData("filters-conv-01-f", 0.022)]
    [InlineData("filters-conv-02-f", 0.022)]
    [InlineData("filters-conv-03-f", 0.022)]
    [InlineData("filters-conv-04-f", 0.022)]
    [InlineData("filters-conv-05-f", 0.022)]
    [InlineData("filters-diffuse-01-f", 0.022)]
    [InlineData("filters-displace-01-f", 0.022)]
    [InlineData("filters-displace-02-f", 0.022)]
    [InlineData("filters-example-01-b", 0.022)]
    [InlineData("filters-felem-01-b", 0.022)]
    [InlineData("filters-felem-02-f", 0.022)]
    [InlineData("filters-gauss-01-b", 0.022)]
    [InlineData("filters-gauss-02-f", 0.022)]
    [InlineData("filters-gauss-03-f", 0.022)]
    [InlineData("filters-image-01-b", 0.022)]
    [InlineData("filters-image-02-b", 0.022)]
    [InlineData("filters-image-03-f", 0.022)]
    [InlineData("filters-image-04-f", 0.022)]
    [InlineData("filters-image-05-f", 0.022)]
    [InlineData("filters-light-01-f", 0.022)]
    [InlineData("filters-light-02-f", 0.022)]
    [InlineData("filters-light-03-f", 0.022)]
    [InlineData("filters-light-04-f", 0.022)]
    [InlineData("filters-light-05-f", 0.022)]
    [InlineData("filters-morph-01-f", 0.022)]
    [InlineData("filters-offset-01-b", 0.022)]
    [InlineData("filters-offset-02-b", 0.022)]
    [InlineData("filters-overview-01-b", 0.022)]
    [InlineData("filters-overview-02-b", 0.022)]
    [InlineData("filters-overview-03-b", 0.022)]
    [InlineData("filters-specular-01-f", 0.022)]
    [InlineData("filters-tile-01-b", 0.022)]
    [InlineData("filters-turb-01-f", 0.022)]
    [InlineData("filters-turb-02-f", 0.022)]
    [InlineData("fonts-desc-01-t", 0.022)]
    [InlineData("fonts-desc-02-t", 0.022)]
    [InlineData("fonts-desc-03-t", 0.022)]
    [InlineData("fonts-desc-04-t", 0.022)]
    [InlineData("fonts-desc-05-t", 0.022)]
    [InlineData("fonts-elem-01-t", 0.022)]
    [InlineData("fonts-elem-02-t", 0.022)]
    [InlineData("fonts-elem-03-b", 0.022)]
    [InlineData("fonts-elem-04-b", 0.022)]
    [InlineData("fonts-elem-05-t", 0.022)]
    [InlineData("fonts-elem-06-t", 0.022)]
    [InlineData("fonts-elem-07-b", 0.022)]
    [InlineData("fonts-glyph-02-t", 0.022)]
    [InlineData("fonts-glyph-03-t", 0.022)]
    [InlineData("fonts-glyph-04-t", 0.022)]
    [InlineData("fonts-kern-01-t", 0.022)]
    [InlineData("fonts-overview-201-t", 0.022)]
    [InlineData("imp-path-01-f", 0.022, Skip = "TODO")]
    [InlineData("interact-cursor-01-f", 0.022, Skip = "TODO")]
    [InlineData("interact-dom-01-b", 0.022, Skip = "TODO")]
    [InlineData("interact-events-01-b", 0.022, Skip = "TODO")]
    [InlineData("interact-events-02-b", 0.022, Skip = "TODO")]
    [InlineData("interact-events-202-f", 0.022, Skip = "TODO")]
    [InlineData("interact-events-203-t", 0.022, Skip = "TODO")]
    [InlineData("interact-order-01-b", 0.022, Skip = "TODO")]
    [InlineData("interact-order-02-b", 0.022, Skip = "TODO")]
    [InlineData("interact-order-03-b", 0.022, Skip = "TODO")]
    [InlineData("interact-pevents-01-b", 0.022, Skip = "TODO")]
    [InlineData("interact-pevents-03-b", 0.022, Skip = "TODO")]
    [InlineData("interact-pevents-04-t", 0.022, Skip = "TODO")]
    [InlineData("interact-pevents-05-b", 0.022, Skip = "TODO")]
    [InlineData("interact-pevents-07-t", 0.022, Skip = "TODO")]
    [InlineData("interact-pevents-08-f", 0.022, Skip = "TODO")]
    [InlineData("interact-pevents-09-f", 0.022, Skip = "TODO")]
    [InlineData("interact-pevents-10-f", 0.022, Skip = "TODO")]
    [InlineData("interact-pointer-01-t", 0.022, Skip = "TODO")]
    [InlineData("interact-pointer-02-t", 0.022, Skip = "TODO")]
    [InlineData("interact-pointer-03-t", 0.022, Skip = "TODO")]
    [InlineData("interact-pointer-04-f", 0.022, Skip = "TODO")]
    [InlineData("interact-zoom-01-t", 0.022, Skip = "TODO")]
    [InlineData("interact-zoom-02-t", 0.022, Skip = "TODO")]
    [InlineData("interact-zoom-03-t", 0.022, Skip = "TODO")]
    [InlineData("linking-a-01-b", 0.022)]
    [InlineData("linking-a-03-b", 0.022)]
    [InlineData("linking-a-04-t", 0.022)]
    [InlineData("linking-a-05-t", 0.022)]
    [InlineData("linking-a-07-t", 0.022)]
    [InlineData("linking-a-08-t", 0.022)]
    [InlineData("linking-a-09-b", 0.022)]
    [InlineData("linking-a-10-f", 0.022)]
    [InlineData("linking-frag-01-f", 0.022)]
    [InlineData("linking-uri-01-b", 0.022)]
    [InlineData("linking-uri-02-b", 0.022)]
    [InlineData("linking-uri-03-t", 0.022)]
    [InlineData("masking-filter-01-f", 0.022)]
    [InlineData("masking-intro-01-f", 0.022)]
    [InlineData("masking-mask-01-b", 0.022)]
    [InlineData("masking-mask-02-f", 0.022)]
    [InlineData("masking-opacity-01-b", 0.022)]
    [InlineData("masking-path-01-b", 0.022)]
    [InlineData("masking-path-02-b", 0.022)]
    [InlineData("masking-path-03-b", 0.022)]
    [InlineData("masking-path-04-b", 0.022)]
    [InlineData("masking-path-05-f", 0.022)]
    [InlineData("masking-path-06-b", 0.022)]
    [InlineData("masking-path-07-b", 0.022)]
    [InlineData("masking-path-08-b", 0.022)]
    [InlineData("masking-path-09-b", 0.022, Skip = "Requires DOM script execution (getBBox)")]
    [InlineData("masking-path-10-b", 0.022)]
    [InlineData("masking-path-11-b", 0.022)]
    [InlineData("masking-path-12-f", 0.022, Skip = "Requires DOM script execution (getComputedStyle)")]
    [InlineData("masking-path-13-f", 0.022)]
    [InlineData("masking-path-14-f", 0.022)]
    [InlineData("metadata-example-01-t", 0.022)]
    [InlineData("painting-control-01-f", 0.022)]
    [InlineData("painting-control-02-f", 0.022)]
    [InlineData("painting-control-03-f", 0.022)]
    [InlineData("painting-control-04-f", 0.022)]
    [InlineData("painting-control-05-f", 0.022)]
    [InlineData("painting-control-06-f", 0.022)]
    [InlineData("painting-fill-01-t", 0.022)]
    [InlineData("painting-fill-02-t", 0.022)]
    [InlineData("painting-fill-03-t", 0.022)]
    [InlineData("painting-fill-04-t", 0.022)]
    [InlineData("painting-fill-05-b", 0.022)]
    [InlineData("painting-marker-01-f", 0.022)]
    [InlineData("painting-marker-02-f", 0.022)]
    [InlineData("painting-marker-03-f", 0.022)]
    [InlineData("painting-marker-04-f", 0.022)]
    [InlineData("painting-marker-05-f", 0.022)]
    [InlineData("painting-marker-06-f", 0.022)]
    [InlineData("painting-marker-07-f", 0.022)]
    [InlineData("painting-marker-properties-01-f", 0.022)]
    [InlineData("painting-render-01-b", 0.022)]
    [InlineData("painting-render-02-b", 0.022)]
    [InlineData("painting-stroke-01-t", 0.022)]
    [InlineData("painting-stroke-02-t", 0.022)]
    [InlineData("painting-stroke-03-t", 0.022)]
    [InlineData("painting-stroke-04-t", 0.022)]
    [InlineData("painting-stroke-05-t", 0.022)]
    [InlineData("painting-stroke-06-t", 0.022)]
    [InlineData("painting-stroke-07-t", 0.022)]
    [InlineData("painting-stroke-08-t", 0.022)]
    [InlineData("painting-stroke-09-t", 0.022)]
    [InlineData("painting-stroke-10-t", 0.022)]
    [InlineData("paths-data-01-t", 0.100)]
    [InlineData("paths-data-02-t", 0.100)]
    [InlineData("paths-data-03-f", 0.100)]
    [InlineData("paths-data-04-t", 0.100)]
    [InlineData("paths-data-05-t", 0.100)]
    [InlineData("paths-data-06-t", 0.100)]
    [InlineData("paths-data-07-t", 0.100)]
    [InlineData("paths-data-08-t", 0.100)]
    [InlineData("paths-data-09-t", 0.100)]
    [InlineData("paths-data-10-t", 0.100)]
    [InlineData("paths-data-12-t", 0.100)]
    [InlineData("paths-data-13-t", 0.100)]
    [InlineData("paths-data-14-t", 0.100)]
    [InlineData("paths-data-15-t", 0.100)]
    [InlineData("paths-data-16-t", 0.100)]
    [InlineData("paths-data-17-f", 0.100)]
    [InlineData("paths-data-18-f", 0.100)]
    [InlineData("paths-data-19-f", 0.100)]
    [InlineData("paths-data-20-f", 0.100)]
    [InlineData("paths-dom-01-f", 0.100, Skip = "TODO")]
    [InlineData("paths-dom-02-f", 0.100, Skip = "TODO")]
    [InlineData("pservers-grad-01-b", 0.022)]
    [InlineData("pservers-grad-02-b", 0.022)]
    [InlineData("pservers-grad-03-b", 0.022)]
    [InlineData("pservers-grad-04-b", 0.022)]
    [InlineData("pservers-grad-05-b", 0.022)]
    [InlineData("pservers-grad-06-b", 0.022)]
    [InlineData("pservers-grad-07-b", 0.022)]
    [InlineData("pservers-grad-08-b", 0.022, Skip = "Requires SVG/WOFF webfont loading for exact Chrome text gradients.")]
    [InlineData("pservers-grad-09-b", 0.022)]
    [InlineData("pservers-grad-10-b", 0.022)]
    [InlineData("pservers-grad-11-b", 0.022)]
    [InlineData("pservers-grad-12-b", 0.022)]
    [InlineData("pservers-grad-13-b", 0.022)]
    [InlineData("pservers-grad-14-b", 0.022)]
    [InlineData("pservers-grad-15-b", 0.022)]
    [InlineData("pservers-grad-16-b", 0.022)]
    [InlineData("pservers-grad-17-b", 0.022)]
    [InlineData("pservers-grad-18-b", 0.022)]
    [InlineData("pservers-grad-20-b", 0.022)]
    [InlineData("pservers-grad-21-b", 0.022)]
    [InlineData("pservers-grad-22-b", 0.022)]
    [InlineData("pservers-grad-23-f", 0.022)]
    [InlineData("pservers-grad-24-f", 0.022)]
    [InlineData("pservers-grad-stops-01-f", 0.022)]
    [InlineData("pservers-pattern-01-b", 0.022)]
    [InlineData("pservers-pattern-02-f", 0.022)]
    [InlineData("pservers-pattern-03-f", 0.022)]
    [InlineData("pservers-pattern-04-f", 0.022)]
    [InlineData("pservers-pattern-05-f", 0.022)]
    [InlineData("pservers-pattern-06-f", 0.022)]
    [InlineData("pservers-pattern-07-f", 0.022)]
    [InlineData("pservers-pattern-08-f", 0.022)]
    [InlineData("pservers-pattern-09-f", 0.022)]
    [InlineData("render-elems-01-t", 0.022)]
    [InlineData("render-elems-02-t", 0.022)]
    [InlineData("render-elems-03-t", 0.022)]
    [InlineData("render-elems-06-t", 0.022, Skip = "Requires SVG/WOFF webfont loading for exact Chrome glyph outlines.")]
    [InlineData("render-elems-07-t", 0.022, Skip = "Requires SVG/WOFF webfont loading for exact Chrome glyph outlines.")]
    [InlineData("render-elems-08-t", 0.022, Skip = "Requires SVG/WOFF webfont loading for exact Chrome glyph outlines.")]
    [InlineData("render-groups-01-b", 0.022, Skip = "Requires SVG/WOFF webfont loading for exact Chrome group text composition.")]
    [InlineData("render-groups-03-t", 0.022, Skip = "Requires SVG/WOFF webfont loading for exact Chrome group text composition.")]
    [InlineData("script-handle-01-b", 0.022, Skip = "TODO")]
    [InlineData("script-handle-02-b", 0.022, Skip = "TODO")]
    [InlineData("script-handle-03-b", 0.022, Skip = "TODO")]
    [InlineData("script-handle-04-b", 0.022, Skip = "TODO")]
    [InlineData("script-specify-01-f", 0.022, Skip = "TODO")]
    [InlineData("script-specify-02-f", 0.022, Skip = "TODO")]
    [InlineData("shapes-circle-01-t", 0.022)]
    [InlineData("shapes-circle-02-t", 0.022)]
    [InlineData("shapes-ellipse-01-t", 0.022)]
    [InlineData("shapes-ellipse-02-t", 0.022)]
    [InlineData("shapes-ellipse-03-f", 0.022)]
    [InlineData("shapes-grammar-01-f", 0.022)]
    [InlineData("shapes-intro-01-t", 0.022)]
    [InlineData("shapes-intro-02-f", 0.022)]
    [InlineData("shapes-line-01-t", 0.022)]
    [InlineData("shapes-line-02-f", 0.022)]
    [InlineData("shapes-polygon-01-t", 0.022)]
    [InlineData("shapes-polygon-02-t", 0.022)]
    [InlineData("shapes-polygon-03-t", 0.022)]
    [InlineData("shapes-polyline-01-t", 0.022)]
    [InlineData("shapes-polyline-02-t", 0.022)]
    [InlineData("shapes-rect-01-t", 0.022)]
    [InlineData("shapes-rect-02-t", 0.022)]
    [InlineData("shapes-rect-03-t", 0.022)]
    [InlineData("shapes-rect-04-f", 0.022)]
    [InlineData("shapes-rect-05-f", 0.022)]
    [InlineData("shapes-rect-06-f", 0.022)]
    [InlineData("shapes-rect-07-f", 0.022)]
    [InlineData("struct-cond-01-t", 0.022)]
    [InlineData("struct-cond-02-t", 0.022)]
    [InlineData("struct-cond-03-t", 0.022)]
    [InlineData("struct-cond-overview-02-f", 0.022)]
    [InlineData("struct-cond-overview-03-f", 0.022)]
    [InlineData("struct-cond-overview-04-f", 0.022)]
    [InlineData("struct-cond-overview-05-f", 0.022)]
    [InlineData("struct-defs-01-t", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-01-b", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-02-b", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-03-b", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-04-b", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-05-b", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-06-b", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-07-f", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-08-f", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-11-f", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-12-b", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-13-f", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-14-f", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-15-f", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-16-f", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-17-f", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-18-f", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-19-f", 0.022, Skip = "TODO")]
    [InlineData("struct-dom-20-f", 0.022, Skip = "TODO")]
    [InlineData("struct-frag-01-t", 0.022)]
    [InlineData("struct-frag-02-t", 0.022)]
    [InlineData("struct-frag-03-t", 0.022)]
    [InlineData("struct-frag-04-t", 0.022)]
    [InlineData("struct-frag-05-t", 0.022)]
    [InlineData("struct-frag-06-t", 0.022)]
    [InlineData("struct-group-01-t", 0.022)]
    [InlineData("struct-group-02-b", 0.022)]
    [InlineData("struct-group-03-t", 0.022)]
    [InlineData("struct-image-01-t", 0.022)]
    [InlineData("struct-image-02-b", 0.022)]
    [InlineData("struct-image-03-t", 0.022)]
    [InlineData("struct-image-04-t", 0.022)]
    [InlineData("struct-image-05-b", 0.022)]
    [InlineData("struct-image-06-t", 0.022)]
    [InlineData("struct-image-07-t", 0.022, Skip = "Chrome standalone renders xml:base image loads as broken-image placeholders.")]
    [InlineData("struct-image-08-t", 0.022)]
    [InlineData("struct-image-09-t", 0.022)]
    [InlineData("struct-image-10-t", 0.022)]
    [InlineData("struct-image-11-b", 0.022)]
    [InlineData("struct-image-12-b", 0.022, Skip = "Chrome shows browser broken-image UI for cyclic and invalid image references.")]
    [InlineData("struct-image-13-f", 0.022)]
    [InlineData("struct-image-14-f", 0.022)]
    [InlineData("struct-image-15-f", 0.022)]
    [InlineData("struct-image-16-f", 0.022)]
    [InlineData("struct-image-17-b", 0.022, Skip = "Chrome executes script and animation inside embedded SVG images.")]
    [InlineData("struct-image-18-f", 0.022)]
    [InlineData("struct-image-19-f", 0.022)]
    [InlineData("struct-svg-01-f", 0.022, Skip = "Requires scripted DOM SVGAnimatedLength inspection.")]
    [InlineData("struct-svg-02-f", 0.022, Skip = "Requires scripted DOM viewport mutation and reparenting.")]
    [InlineData("struct-svg-03-f", 0.022)]
    [InlineData("struct-symbol-01-b", 0.022)]
    [InlineData("struct-use-01-t", 0.022)]
    [InlineData("struct-use-03-t", 0.022)]
    [InlineData("struct-use-04-b", 0.022)]
    [InlineData("struct-use-05-b", 0.022)]
    [InlineData("struct-use-06-b", 0.022)]
    [InlineData("struct-use-07-b", 0.022)]
    [InlineData("struct-use-08-b", 0.022, Skip = "Chrome recursive capture never reaches a stable load for baseline capture.")]
    [InlineData("struct-use-09-b", 0.022)]
    [InlineData("struct-use-10-f", 0.022, Skip = "Requires CSS use-instance-tree selector semantics.")]
    [InlineData("struct-use-11-f", 0.022, Skip = "Requires CSS use-instance-tree selector semantics.")]
    [InlineData("struct-use-12-f", 0.022)]
    [InlineData("struct-use-13-f", 0.022, Skip = "Requires runtime DOM mutation of referenced use content.")]
    [InlineData("struct-use-14-f", 0.022, Skip = "Requires runtime DOM id mutation and late use resolution.")]
    [InlineData("struct-use-15-f", 0.022, Skip = "Requires runtime DOM synchronization on recursive use trees.")]
    [InlineData("styling-class-01-f", 0.022, Skip = "TODO")]
    [InlineData("styling-css-01-b", 0.022, Skip = "TODO")]
    [InlineData("styling-css-02-b", 0.022, Skip = "TODO")]
    [InlineData("styling-css-03-b", 0.022, Skip = "TODO")]
    [InlineData("styling-css-04-f", 0.022, Skip = "TODO")]
    [InlineData("styling-css-05-b", 0.022, Skip = "TODO")]
    [InlineData("styling-css-06-b", 0.022, Skip = "TODO")]
    [InlineData("styling-css-07-f", 0.022, Skip = "TODO")]
    [InlineData("styling-css-08-f", 0.022, Skip = "TODO")]
    [InlineData("styling-css-09-f", 0.022, Skip = "TODO")]
    [InlineData("styling-css-10-f", 0.022, Skip = "TODO")]
    [InlineData("styling-elem-01-b", 0.022, Skip = "TODO")]
    [InlineData("styling-inherit-01-b", 0.022, Skip = "TODO")]
    [InlineData("styling-pres-01-t", 0.022, Skip = "TODO")]
    [InlineData("styling-pres-02-f", 0.022, Skip = "TODO")]
    [InlineData("styling-pres-03-f", 0.022, Skip = "TODO")]
    [InlineData("styling-pres-04-f", 0.022, Skip = "TODO")]
    [InlineData("styling-pres-05-f", 0.022, Skip = "TODO")]
    [InlineData("svgdom-over-01-f", 0.022, Skip = "TODO")]
    [InlineData("text-align-01-b", 0.022, Skip = "TODO")]
    [InlineData("text-align-02-b", 0.022, Skip = "TODO")]
    [InlineData("text-align-03-b", 0.022, Skip = "TODO")]
    [InlineData("text-align-04-b", 0.022, Skip = "TODO")]
    [InlineData("text-align-05-b", 0.022, Skip = "TODO")]
    [InlineData("text-align-06-b", 0.022, Skip = "TODO")]
    [InlineData("text-align-07-t", 0.022, Skip = "TODO")]
    [InlineData("text-align-08-b", 0.022, Skip = "TODO")]
    [InlineData("text-altglyph-01-b", 0.022, Skip = "TODO")]
    [InlineData("text-altglyph-02-b", 0.022, Skip = "TODO")]
    [InlineData("text-altglyph-03-b", 0.022, Skip = "TODO")]
    [InlineData("text-bidi-01-t", 0.022, Skip = "TODO")]
    [InlineData("text-deco-01-b", 0.022, Skip = "TODO")]
    [InlineData("text-dom-01-f", 0.022, Skip = "TODO")]
    [InlineData("text-dom-02-f", 0.022, Skip = "TODO")]
    [InlineData("text-dom-03-f", 0.022, Skip = "TODO")]
    [InlineData("text-dom-04-f", 0.022, Skip = "TODO")]
    [InlineData("text-dom-05-f", 0.022, Skip = "TODO")]
    [InlineData("text-fonts-01-t", 0.022, Skip = "TODO")]
    [InlineData("text-fonts-02-t", 0.022, Skip = "TODO")]
    [InlineData("text-fonts-03-t", 0.022, Skip = "TODO")]
    [InlineData("text-fonts-04-t", 0.022, Skip = "TODO")]
    [InlineData("text-fonts-05-f", 0.022, Skip = "TODO")]
    [InlineData("text-fonts-06-t", 0.022, Skip = "TODO")]
    [InlineData("text-fonts-202-t", 0.022, Skip = "TODO")]
    [InlineData("text-fonts-203-t", 0.022, Skip = "TODO")]
    [InlineData("text-fonts-204-t", 0.022, Skip = "TODO")]
    [InlineData("text-intro-01-t", 0.022, Skip = "TODO")]
    [InlineData("text-intro-02-b", 0.022, Skip = "TODO")]
    [InlineData("text-intro-03-b", 0.022, Skip = "TODO")]
    [InlineData("text-intro-04-t", 0.022, Skip = "TODO")]
    [InlineData("text-intro-05-t", 0.022, Skip = "TODO")]
    [InlineData("text-intro-06-t", 0.022, Skip = "TODO")]
    [InlineData("text-intro-07-t", 0.022, Skip = "TODO")]
    [InlineData("text-intro-09-b", 0.022, Skip = "TODO")]
    [InlineData("text-intro-10-f", 0.022, Skip = "TODO")]
    [InlineData("text-intro-11-t", 0.022, Skip = "TODO")]
    [InlineData("text-intro-12-t", 0.022, Skip = "TODO")]
    [InlineData("text-path-01-b", 0.022, Skip = "TODO")]
    [InlineData("text-path-02-b", 0.022, Skip = "TODO")]
    [InlineData("text-spacing-01-b", 0.022, Skip = "TODO")]
    [InlineData("text-text-01-b", 0.022, Skip = "TODO")]
    [InlineData("text-text-03-b", 0.022, Skip = "TODO")]
    [InlineData("text-text-04-t", 0.022, Skip = "TODO")]
    [InlineData("text-text-05-t", 0.022, Skip = "TODO")]
    [InlineData("text-text-06-t", 0.022, Skip = "TODO")]
    [InlineData("text-text-07-t", 0.022, Skip = "TODO")]
    [InlineData("text-text-08-b", 0.022, Skip = "TODO")]
    [InlineData("text-text-09-t", 0.022, Skip = "TODO")]
    [InlineData("text-text-10-t", 0.022, Skip = "TODO")]
    [InlineData("text-text-11-t", 0.022, Skip = "TODO")]
    [InlineData("text-text-12-t", 0.022, Skip = "TODO")]
    [InlineData("text-tref-01-b", 0.022, Skip = "TODO")]
    [InlineData("text-tref-02-b", 0.022, Skip = "TODO")]
    [InlineData("text-tref-03-b", 0.022, Skip = "TODO")]
    [InlineData("text-tselect-01-b", 0.022, Skip = "TODO")]
    [InlineData("text-tselect-02-f", 0.022, Skip = "TODO")]
    [InlineData("text-tselect-03-f", 0.022, Skip = "TODO")]
    [InlineData("text-tspan-01-b", 0.022, Skip = "TODO")]
    [InlineData("text-tspan-02-b", 0.022, Skip = "TODO")]
    [InlineData("text-ws-01-t", 0.022, Skip = "TODO")]
    [InlineData("text-ws-02-t", 0.022, Skip = "TODO")]
    [InlineData("text-ws-03-t", 0.022, Skip = "TODO")]
    [InlineData("types-basic-01-f", 0.022, Skip = "TODO")]
    [InlineData("types-basic-02-f", 0.022, Skip = "TODO")]
    [InlineData("types-dom-01-b", 0.022, Skip = "TODO")]
    [InlineData("types-dom-02-f", 0.022, Skip = "TODO")]
    [InlineData("types-dom-03-b", 0.022, Skip = "TODO")]
    [InlineData("types-dom-04-b", 0.022, Skip = "TODO")]
    [InlineData("types-dom-05-b", 0.022, Skip = "TODO")]
    [InlineData("types-dom-06-f", 0.022, Skip = "TODO")]
    [InlineData("types-dom-07-f", 0.022, Skip = "TODO")]
    [InlineData("types-dom-08-f", 0.022, Skip = "TODO")]
    [InlineData("types-dom-svgfittoviewbox-01-f", 0.022, Skip = "TODO")]
    [InlineData("types-dom-svglengthlist-01-f", 0.022, Skip = "TODO")]
    [InlineData("types-dom-svgnumberlist-01-f", 0.022, Skip = "TODO")]
    [InlineData("types-dom-svgstringlist-01-f", 0.022, Skip = "TODO")]
    [InlineData("types-dom-svgtransformable-01-f", 0.022, Skip = "TODO")]
    public void Tests(string name, double errorThreshold) => TestImpl(name, errorThreshold);
}
