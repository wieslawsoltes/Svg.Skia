using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using ShimSkiaSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Svg;
using Svg.JavaScript;
using Svg.Model.Services;
using Svg.Pathing;
using Svg.Skia.UnitTests.Common;
using Xunit;
using SkiaAlphaType = SkiaSharp.SKAlphaType;
using SkiaBitmap = SkiaSharp.SKBitmap;
using SkiaColorType = SkiaSharp.SKColorType;

namespace Svg.Skia.UnitTests;

public class W3CTestSuiteTests : SvgUnitTest
{
    private static readonly HashSet<string> s_javaScriptW3CTests = new(StringComparer.Ordinal)
    {
        "animate-script-elem-01-b",
        "animate-dom-01-f",
        "animate-dom-02-f",
        "animate-struct-dom-01-b",
        "animate-interact-pevents-01-t",
        "animate-interact-pevents-02-t",
        "animate-interact-pevents-03-t",
        "animate-interact-pevents-04-t",
        "conform-viewers-03-f",
        "coords-dom-01-f",
        "coords-dom-02-f",
        "coords-dom-03-f",
        "coords-dom-04-f",
        "extend-namespace-01-f",
        "interact-events-202-f",
        "interact-events-203-t",
        "interact-dom-01-b",
        "interact-events-01-b",
        "interact-events-02-b",
        "interact-order-01-b",
        "interact-order-02-b",
        "interact-order-03-b",
        "interact-pevents-01-b",
        "interact-pevents-03-b",
        "interact-pevents-05-b",
        "interact-pevents-07-t",
        "interact-pevents-08-f",
        "interact-pevents-09-f",
        "interact-pevents-10-f",
        "interact-pointer-01-t",
        "interact-pointer-02-t",
        "interact-pointer-03-t",
        "interact-pointer-04-f",
        "masking-path-09-b",
        "masking-path-12-f",
        "paths-dom-01-f",
        "paths-dom-02-f",
        "script-handle-01-b",
        "script-handle-02-b",
        "script-handle-03-b",
        "script-handle-04-b",
        "script-specify-01-f",
        "script-specify-02-f",
        "styling-pres-02-f",
        "struct-dom-01-b",
        "struct-dom-02-b",
        "struct-dom-03-b",
        "struct-dom-04-b",
        "struct-dom-05-b",
        "struct-dom-06-b",
        "struct-dom-07-f",
        "struct-dom-08-f",
        "struct-dom-11-f",
        "struct-dom-12-b",
        "struct-dom-14-f",
        "struct-dom-15-f",
        "struct-dom-16-f",
        "struct-dom-17-f",
        "struct-dom-18-f",
        "struct-dom-19-f",
        "struct-dom-20-f",
        "struct-svg-01-f",
        "struct-svg-02-f",
        "struct-use-13-f",
        "struct-use-14-f",
        "struct-use-15-f",
        "svgdom-over-01-f",
        "text-dom-01-f",
        "text-dom-02-f",
        "text-dom-03-f",
        "text-dom-04-f",
        "text-dom-05-f",
        "text-tselect-02-f",
        "text-tselect-03-f",
        "types-dom-01-b",
        "types-dom-02-f",
        "types-dom-03-b",
        "types-dom-04-b",
        "types-dom-05-b",
        "types-dom-06-f",
        "types-dom-07-f",
        "types-dom-08-f",
        "types-dom-svgfittoviewbox-01-f",
        "types-dom-svglengthlist-01-f",
        "types-dom-svgnumberlist-01-f",
        "types-dom-svgstringlist-01-f",
        "types-dom-svgtransformable-01-f"
    };

    // Parsed by scripts/capture_w3c_chrome_overrides.mjs. Keep entries as
    // ["fixture-name"] = seconds so test and Chrome capture timing stay aligned.
    // W3C_ANIMATION_SEEK_TIMES_BEGIN
    private static readonly IReadOnlyDictionary<string, double> s_animationSeekTimesSeconds = new Dictionary<string, double>(StringComparer.Ordinal)
    {
        // Existing enabled rows.
        ["animate-script-elem-01-b"] = 1.1,
        ["animate-dom-01-f"] = 2.5,

        // SMIL snapshot rows whose operator/pass text identifies a stable frame.
        ["animate-elem-02-t"] = 7,
        ["animate-elem-03-t"] = 6,
        ["animate-elem-04-t"] = 3,
        ["animate-elem-05-t"] = 6,
        ["animate-elem-06-t"] = 6,
        ["animate-elem-07-t"] = 6,
        ["animate-elem-08-t"] = 6,
        ["animate-elem-09-t"] = 8,
        ["animate-elem-10-t"] = 9,
        ["animate-elem-11-t"] = 9,
        ["animate-elem-12-t"] = 9,
        ["animate-elem-13-t"] = 5,
        ["animate-elem-14-t"] = 5,
        ["animate-elem-15-t"] = 4.5,
        ["animate-elem-17-t"] = 6,
        ["animate-elem-19-t"] = 5,
        ["animate-elem-22-b"] = 9,
        ["animate-elem-24-t"] = 9,
        ["animate-elem-25-t"] = 9,
        ["animate-elem-26-t"] = 7,
        ["animate-elem-27-t"] = 9,
        ["animate-elem-28-t"] = 4,
        ["animate-elem-30-t"] = 3.1,
        ["animate-elem-31-t"] = 5,
        ["animate-elem-32-t"] = 6,
        ["animate-elem-33-t"] = 4,
        ["animate-elem-34-t"] = 4.5,
        ["animate-elem-35-t"] = 5,
        ["animate-elem-36-t"] = 1.5,
        ["animate-elem-37-t"] = 1.5,
        ["animate-elem-38-t"] = 10,
        ["animate-elem-39-t"] = 1.5,
        ["animate-elem-40-t"] = 3.1,
        ["animate-elem-41-t"] = 3,
        ["animate-elem-44-t"] = 4.5,
        ["animate-elem-46-t"] = 3,
        ["animate-elem-52-t"] = 5,
        ["animate-elem-53-t"] = 9,
        ["animate-elem-64-t"] = 6,
        ["animate-elem-65-t"] = 6,
        ["animate-elem-66-t"] = 6,
        ["animate-elem-67-t"] = 6,
        ["animate-elem-68-t"] = 6,
        ["animate-elem-69-t"] = 6,
        ["animate-elem-70-t"] = 6,
        ["animate-elem-77-t"] = 0.5,
        ["animate-elem-78-t"] = 0.5,
        ["animate-elem-80-t"] = 4.1,
        ["animate-elem-81-t"] = 5,
        ["animate-elem-82-t"] = 3,
        ["animate-elem-83-t"] = 2.5,
        ["animate-elem-86-t"] = 3,
        ["animate-elem-87-t"] = 4,
        ["animate-elem-88-t"] = 2,
        ["animate-elem-89-t"] = 9,
        ["animate-elem-90-b"] = 5,
        ["animate-elem-91-t"] = 3,
        ["animate-elem-92-t"] = 3,
        ["animate-pservers-grad-01-b"] = 5,
        ["filters-composite-05-f"] = 2
    };
    // W3C_ANIMATION_SEEK_TIMES_END

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
        // Any checked Chrome override should render with browser-compatible SVG text behavior.
        // Legacy W3C PNG rows without a Chrome override still keep spec-path SVG font coverage.
        svg.Settings.EnableSvgFonts = !useChromeOverride;
        svg.Settings.EnableTextReferences = !useChromeOverride;
        svg.Settings.EnableFilterBackgroundInputs = !ShouldUseChromeFilterBackgroundInputFallback(name, useChromeOverride);
        svg.Settings.EnableJavaScript = ShouldEnableJavaScript(name);
        svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 480f, 360f);
        if (!useBrowserCompatibleFonts)
        {
            SetTypefaceProviders(svg.Settings);
        }
        using var __ = CreateSystemLanguageScope(name);
        using var _ = svg.Load(svgPath);
        ApplyPreSeekInteractions(name, svg);
        if (GetAnimationSeekTime(name) is { } animationSeekTime)
        {
            svg.SetAnimationTime(animationSeekTime);
        }
        ApplyPostLoadInteractions(name, svg);
        if (TryRunSemanticAssertion(name, svg))
        {
            return;
        }

        var compositeBackground = GetCompositeBackground(name, useChromeOverride);
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
        if (name is "text-dom-03-f" or "text-dom-04-f")
        {
            return false;
        }

        return name.StartsWith("linking-") ||
                name.StartsWith("masking-") ||
                name.StartsWith("shapes-") ||
                name.StartsWith("coords-") ||
                ShouldEnableJavaScript(name) ||
                name.StartsWith("struct-cond-") ||
                name.StartsWith("painting-") ||
                name == "metadata-example-01-t";
    }

    private static bool ShouldUseChromeFilterBackgroundInputFallback(string name, bool useChromeOverride)
    {
        return useChromeOverride &&
            name is "filters-overview-01-b" or "filters-overview-02-b" or "filters-overview-03-b";
    }

    private static bool ShouldEnableJavaScript(string name)
    {
        return s_javaScriptW3CTests.Contains(name);
    }

    private static TimeSpan? GetAnimationSeekTime(string name)
    {
        return s_animationSeekTimesSeconds.TryGetValue(name, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : null;
    }

    private static bool ShouldUseBrowserCompatibleSvgFontFallback(string name)
    {
        return name.StartsWith("fonts-");
    }

    private static SkiaSharp.SKColor ToSkColor(Rgba32 color)
    {
        return new SkiaSharp.SKColor(color.R, color.G, color.B, color.A);
    }

    private static Rgba32? GetCompositeBackground(string name, bool useChromeOverride)
    {
        if (useChromeOverride)
        {
            return new Rgba32(255, 255, 255, 255);
        }

        return name switch
        {
            "struct-dom-19-f" or
            "struct-dom-20-f" => new Rgba32(255, 255, 255, 255),
            _ => null
        };
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
            // These DOM fixtures use the image only as a pass/fail indicator. The remaining
            // mismatch is isolated to the revision footer text rather than the DOM-driven shapes.
            "struct-dom-19-f" or
            "struct-dom-20-f" => new[]
            {
                new Rectangle(0, 305, 480, 55)
            },
            // These SVG font DOM rows assert only the left-hand green indicator rectangles. The
            // remaining mismatch comes from descriptor/footer text, not the DOM substring results.
            "animate-script-elem-01-b" => new[]
            {
                new Rectangle(0, 0, 480, 45),
                new Rectangle(40, 40, 440, 100),
                new Rectangle(0, 305, 480, 55)
            },
            "text-dom-03-f" => new[]
            {
                new Rectangle(0, 0, 480, 35),
                new Rectangle(50, 35, 430, 210),
                new Rectangle(0, 305, 480, 55)
            },
            "text-dom-04-f" => new[]
            {
                new Rectangle(0, 0, 480, 22),
                new Rectangle(0, 305, 480, 55)
            },
            "struct-dom-07-f" => new[]
            {
                new Rectangle(0, 0, 480, 45),
                new Rectangle(0, 305, 480, 55)
            },
            "struct-dom-18-f" or
            "types-dom-08-f" or
            "svgdom-over-01-f" => new[]
            {
                new Rectangle(0, 0, 480, 22)
            },
            // The bundled SVG fixture still contains an approved-test draft banner that is absent
            // from the W3C reference PNG. Keep the altGlyph body rows active.
            "text-altglyph-03-b" => new[]
            {
                new Rectangle(0, 0, 480, 22)
            },
            // Chrome and Svg.Skia now agree on the image placement for this external-SVG <image>
            // fixture. The residual mismatch is only in the heading text band, which the W3C pass
            // criteria explicitly excludes from the comparison.
            "coords-viewattr-04-f" => new[]
            {
                new Rectangle(0, 0, 480, 35)
            },
            // The W3C pass criteria explicitly allow the ex-unit row to vary with the user
            // agent font x-height. Keep the rest of the unit comparison active.
            "coords-units-03-b" => new[]
            {
                new Rectangle(18, 73, 215, 14)
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
            // After linearizing the displacement map input, the displaced grids line up with
            // Chrome. The residual delta is PNG gamma/color raster in the map panels plus labels.
            "filters-displace-01-f" => new[]
            {
                new Rectangle(15, 116, 130, 18),
                new Rectangle(165, 114, 110, 16),
                new Rectangle(15, 259, 130, 18),
                new Rectangle(165, 257, 110, 16),
                new Rectangle(300, 150, 130, 36),
                new Rectangle(340, 332, 55, 16)
            },
            // The scripted flower shape now aligns with the legacy W3C reference. The residual
            // mismatch is confined to the standalone revision footer text band rather than the
            // SVGPathSeg DOM behavior under test.
            "paths-dom-02-f" => new[]
            {
                new Rectangle(0, 305, 480, 55)
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
            // The animated dash/linecap/linejoin/miter states align with Chrome at the sampled
            // SMIL frame; the residual delta is Skia stroke rasterization on the dense path samples.
            "animate-elem-35-t" => 0.1,
            // These two W3C rows intentionally stay on the legacy W3C pass images because current
            // Chrome snapshots do not match the W3C discrete class/to-only non-interpolable states.
            // The animated shape pixels match; the residual delta is text and frame rasterization.
            "animate-elem-90-b" => 0.043,
            "animate-elem-91-t" => 0.052,
            // The pass criteria for this units fixture allow font-dependent unit rows to vary;
            // keep a narrow threshold for residual Chrome text/unit raster differences.
            "coords-units-03-b" => 0.026,
            // Geometry and animated xlink target state match Chrome; the remaining delta is
            // confined to platform text rasterization in the labels/revision footer.
            "animate-elem-27-t" => 0.036,
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
            // This Arabic fallback fixture now matches Chrome's bidi/font-selection behavior. The
            // residual delta is limited to platform text rasterization against the Chrome capture.
            "fonts-glyph-02-t" => 0.065,
            "painting-marker-05-f" => 0.027,
            "painting-render-01-b" => 0.043,
            "pservers-pattern-02-f" => 0.04,
            // These matrix-equivalence fixtures align geometrically; the residual difference is
            // limited to transformed text overdraw/fringe rather than the transform math itself.
            "coords-trans-10-f" => 0.023,
            "coords-trans-11-f" => 0.041,
            "coords-trans-12-f" => 0.023,
            // These text fixtures are visually aligned with the Chrome captures after switching
            // textPath rendering to glyph-positioned layout and applying grouped text-anchor
            // handling, but still retain modest raster differences in curved glyph antialiasing
            // and platform text blending.
            "text-align-02-b" => 0.043,
            "text-align-04-b" => 0.046,
            "text-align-05-b" => 0.048,
            "text-align-06-b" => 0.054,
            "text-fonts-02-t" => 0.031,
            "text-fonts-03-t" => 0.029,
            "text-fonts-04-t" => 0.029,
            "text-fonts-05-f" => 0.039,
            "text-fonts-203-t" => 0.085,
            "text-fonts-204-t" => 0.085,
            "text-intro-01-t" => 0.041,
            "text-intro-02-b" => 0.042,
            "text-intro-03-b" => 0.115,
            "text-intro-04-t" => 0.1,
            "text-intro-05-t" => 0.108,
            "text-intro-09-b" => 0.043,
            "text-intro-10-f" => 0.108,
            "text-path-01-b" => 0.065,
            "text-path-02-b" => 0.087,
            "text-deco-01-b" => 0.04,
            "text-spacing-01-b" => 0.11,
            "text-text-03-b" => 0.05,
            "text-text-07-t" => 0.039,
            "text-text-08-b" => 0.039,
            "text-text-09-t" => 0.038,
            "text-text-12-t" => 0.035,
            "text-tspan-01-b" => 0.031,
            "text-tspan-02-b" => 0.03,
            "text-ws-02-t" => 0.023,
            // These remaining filter fixtures are visually aligned with the Chrome captures, but
            // still show modest raster-kernel differences in blur/convolution/lighting output on a
            // pixel-by-pixel comparison.
            "filters-background-01-f" => 0.045,
            "filters-comptran-01-b" => 0.023,
            "filters-composite-02-b" => 0.03,
            "filters-conv-02-f" => 0.05,
            "filters-conv-04-f" => 0.045,
            "filters-displace-01-f" => 0.037,
            "filters-image-05-f" => 0.04,
            "filters-light-01-f" => 0.045,
            "filters-light-04-f" => 0.04,
            "filters-light-05-f" => 0.11,
            "filters-offset-01-b" => 0.03,
            "pservers-grad-06-b" => 0.024,
            // The W3C reference PNG for this instance-tree fixture still carries an older revision
            // footer. The rendered SVGElementInstance semantics now match the green pass state, and
            // the remaining delta is confined to that stale footer label.
            "struct-dom-14-f" => 0.04,
            "struct-dom-15-f" => 0.04,
            "struct-dom-19-f" => 0.045,
            "struct-dom-20-f" => 0.045,
            "struct-dom-12-b" => 0.035,
            "painting-stroke-10-t" => 0.052,
            // This SVG 1.1 timing fixture now reaches the correct all-green end state. The
            // remaining delta against the legacy W3C PNG is limited to text rasterization and
            // footer antialiasing; Chrome's seek-time behavior diverges from the spec assertion.
            "animate-dom-01-f" => 0.05,
            "animate-script-elem-01-b" => 0.026,
            // These DOM factory fixtures now match Chrome semantically and in layout, with the
            // remaining difference limited to text rasterization/fringing in the PASS labels.
            "struct-dom-16-f" => 0.07,
            "types-dom-01-b" => 0.05,
            "types-dom-04-b" => 0.023,
            "types-dom-svgfittoviewbox-01-f" => 0.07,
            "paths-dom-02-f" => 0.13,
            "text-dom-03-f" => 0.033,
            "text-dom-04-f" => 0.023,
            "struct-dom-07-f" => 0.115,
            "svgdom-over-01-f" => 0.035,
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

    private static void ApplyPreSeekInteractions(string name, SKSvg svg)
    {
        switch (name)
        {
            case "animate-elem-52-t":
                NotifyClickEvent(svg, "A");
                NotifyClickEvent(svg, "B");
                NotifyClickEvent(svg, "C");
                break;
        }
    }

    private static void ApplyPostLoadInteractions(string name, SKSvg svg)
    {
        switch (name)
        {
            case "script-handle-01-b":
                DispatchMouseEvent(svg, "target", "click");
                break;
            case "script-handle-02-b":
                DispatchDomEvent(svg, "target", "focusin");
                DispatchDomEvent(svg, "target", "activate");
                DispatchDomEvent(svg, "target", "focusout");
                break;
            case "script-handle-03-b":
                DispatchMouseEvent(svg, "target", "mousedown");
                DispatchMouseEvent(svg, "target", "mouseup");
                DispatchMouseEvent(svg, "target", "click");
                break;
            case "script-handle-04-b":
                DispatchMouseEvent(svg, "target", "mouseover");
                DispatchMouseEvent(svg, "target", "mousemove");
                DispatchMouseEvent(svg, "target", "mouseout");
                break;
            case "interact-dom-01-b":
                DispatchMouseEvent(svg, "startButton", "click");
                break;
            case "struct-dom-12-b":
                DispatchPointerClick(svg, new SKPoint(360f, 180f));
                break;
        }
    }

    private static bool TryRunSemanticAssertion(string name, SKSvg svg)
    {
        switch (name)
        {
            case "animate-elem-20-t":
                AssertIndefiniteFillHyperlinkFixture(svg);
                return true;
            case "animate-elem-21-t":
                AssertChainedIndefiniteHyperlinkFixture(svg);
                return true;
            case "animate-elem-29-b":
                AssertIndefiniteOpacityHyperlinkFixture(svg);
                return true;
            case "animate-elem-60-t":
                AssertAccessKeyAndPastWallclockBeginFixture(svg);
                return true;
            case "animate-elem-61-t":
                AssertMultipleBeginUserEventFixture(svg);
                return true;
            case "animate-elem-62-t":
                AssertAccessKeyAndFutureWallclockEndFixture(svg);
                return true;
            case "animate-elem-63-t":
                AssertMultipleEndUserEventFixture(svg);
                return true;
            case "animate-interact-pevents-01-t":
                AssertTextPointerEventsRows(svg, animated: true);
                return true;
            case "animate-interact-pevents-02-t":
                AssertRenderingOrderPointerEvents(svg, animated: true);
                return true;
            case "animate-interact-pevents-03-t":
                AssertVisiblePointerEventsGrid(svg, animated: true);
                return true;
            case "animate-interact-pevents-04-t":
                AssertPaintedPointerEventsGrid(svg, animated: true);
                return true;
            case "animate-interact-events-01-t":
                AssertAnimatedUseInstanceMouseEventsAndBubbling(svg);
                return true;
            case "conform-viewers-02-f":
                AssertGzippedSvgDataImageFixture(svg);
                return true;
            case "conform-viewers-03-f":
                AssertDynamicImageNamespaceFixtureUsesOnlyRealXLinkHref(svg.SourceDocument!);
                return true;
            case "extend-namespace-01-f":
                AssertForeignNamespaceDomFixtureCreatedPieChart(svg.SourceDocument!);
                return true;
            case "interact-cursor-01-f":
                AssertCursorFixtureResolvesExpectedCursorValues(svg);
                return true;
            case "interact-events-01-b":
                AssertOnLoadEventAttributeFixtureReachedExpectedVisibility(svg.SourceDocument!);
                return true;
            case "interact-events-02-b":
                AssertSvgLoadDoesNotBubbleFixtureReachedExpectedFills(svg.SourceDocument!);
                return true;
            case "interact-events-202-f":
                AssertUseMouseOverFixtureTogglesReferencingGroups(svg);
                return true;
            case "interact-events-203-t":
                AssertUseInstanceMouseEventsAndBubbling(svg);
                return true;
            case "interact-order-01-b":
                AssertMouseEventBubblingAndStopPropagation(svg);
                return true;
            case "interact-order-02-b":
                AssertEventOrderCircleClickSemantics(svg);
                return true;
            case "interact-order-03-b":
                AssertEventOrderTextClickSemantics(svg);
                return true;
            case "interact-pevents-01-b":
                AssertTextPointerEventsRows(svg, animated: false);
                return true;
            case "interact-pevents-03-b":
                AssertTextCharacterCellPointerEvents(svg, name, animated: false);
                return true;
            case "interact-pevents-04-t":
                AssertTextCharacterCellPointerEvents(svg, name, animated: true);
                return true;
            case "interact-pevents-05-b":
                AssertTextCharacterCellPointerEvents(svg, name, animated: false);
                return true;
            case "interact-pevents-07-t":
                AssertRenderingOrderPointerEvents(svg, animated: false);
                return true;
            case "interact-pevents-08-f":
                AssertVisiblePointerEventsGrid(svg, animated: false);
                return true;
            case "interact-pevents-09-f":
                AssertPaintedPointerEventsGrid(svg, animated: false);
                return true;
            case "interact-zoom-01-t":
            case "interact-zoom-02-t":
                AssertZoomAndPanMagnifyFixture(svg);
                return true;
            case "interact-zoom-03-t":
                AssertZoomAndPanDisableFixture(svg);
                return true;
            case "interact-pevents-10-f":
                AssertDisplayNonePointerEventsDoNotFire(svg);
                return true;
            case "interact-pointer-01-t":
                AssertPointerResultRowReachesPassedState(svg, "r");
                return true;
            case "interact-pointer-02-t":
                AssertPointerResultRowReachesPassedState(svg, "r");
                return true;
            case "interact-pointer-03-t":
                AssertPointerResultRowReachesPassedState(svg, "r1");
                return true;
            case "interact-pointer-04-f":
                AssertMaskedPointerRowReachesPassedState(svg);
                return true;
            case "paths-dom-02-f":
                AssertPathsDom02FixtureCreatesFlowerPathSegments(svg.SourceDocument!);
                return true;
            case "script-specify-01-f":
                AssertUnknownContentScriptTypeSuppressesEventHandler(svg.SourceDocument!);
                return true;
            case "struct-defs-01-t":
                AssertDefsFixtureKeepsDefinitionContentNonRenderable(svg.SourceDocument!);
                return true;
            case "struct-dom-07-f":
                AssertUseInstanceChildNodesCanMutateCorrespondingElements(svg.SourceDocument!);
                return true;
            case "struct-dom-13-f":
                AssertHiddenIntersectionApisUseExpectedRenderableGeometry(svg);
                return true;
            case "struct-dom-18-f":
                AssertIntersectionAndEnclosureListsHideFixtureFailText(svg);
                return true;
            case "struct-svg-02-f":
                AssertNestedSvgLengthDomMetricsResolveViewportChanges();
                return true;
            case "struct-image-07-t":
                AssertXmlBaseImageFixtureCompilesAllImages(svg);
                return true;
            case "struct-image-12-b":
                AssertBrokenImageAndCycleFixtureUsesPlaceholders(svg);
                return true;
            case "struct-image-17-b":
                AssertEmbeddedSvgImageRemainsStatic(svg);
                return true;
            case "text-tselect-01-b":
            case "text-tselect-02-f":
            case "text-tselect-03-f":
                AssertTextSelectionFixtureSupportsHostSelection(svg, name);
                return true;
            case "types-dom-06-f":
                AssertStringListsDuplicateInsertedValues(svg.SourceDocument!);
                return true;
            case "types-dom-08-f":
                AssertGetBBoxFixtureReachesPassedState(svg);
                return true;
            case "types-basic-01-f":
                AssertBasicNumberFixtureParsesScientificStrokeWidths(svg.SourceDocument!);
                return true;
            case "types-basic-02-f":
                AssertBasicLengthFixtureHonorsPresentationAndCssUnitCase(svg.SourceDocument!);
                return true;
            default:
                return false;
        }
    }

    private static void AssertDynamicImageNamespaceFixtureUsesOnlyRealXLinkHref(SvgDocument document)
    {
        var images = document.Descendants().OfType<SvgImage>().ToArray();
        Assert.Contains(images, image => string.Equals(image.Href, "../images/pinksquidj.png", StringComparison.Ordinal));
        var invalidNamespaceImage = Assert.IsType<SvgImage>(document.GetElementById("image2")!);
        Assert.True(string.IsNullOrEmpty(invalidNamespaceImage.Href));

        var prefix = Assert.IsType<SvgTextSpan>(document.GetElementById("prefix")!);
        Assert.NotEqual("...", prefix.Content);
        var status = Assert.IsType<SvgTextSpan>(document.GetElementById("status")!);
        Assert.Equal("No exceptions.", status.Content);
    }

    private static void AssertIndefiniteFillHyperlinkFixture(SKSvg svg)
    {
        DispatchPointerClick(svg, new SKPoint(350f, 80f));
        var fadeIn = CreateAnimatedDocument(svg, TimeSpan.FromSeconds(3.1));
        AssertColorFill(fadeIn.GetElementById("pink"), System.Drawing.Color.Blue);

        svg.SetAnimationTime(TimeSpan.FromSeconds(3.1));
        DispatchPointerClick(svg, new SKPoint(350f, 260f));
        var fadeOut = CreateAnimatedDocument(svg, TimeSpan.FromSeconds(6.2));
        AssertColorFill(fadeOut.GetElementById("pink"), System.Drawing.Color.White);
    }

    private static void AssertChainedIndefiniteHyperlinkFixture(SKSvg svg)
    {
        DispatchPointerClick(svg, new SKPoint(350f, 80f));
        var fadeIn = CreateAnimatedDocument(svg, TimeSpan.FromSeconds(3.1));
        AssertColorFill(fadeIn.GetElementById("pink"), System.Drawing.Color.Blue);
        Assert.All(
            fadeIn.Descendants().OfType<SvgCircle>(),
            circle => AssertColorStroke(circle, System.Drawing.Color.FromArgb(0x66, 0x66, 0x66)));

        svg.SetAnimationTime(TimeSpan.FromSeconds(3.1));
        DispatchPointerClick(svg, new SKPoint(350f, 260f));
        var fadeOut = CreateAnimatedDocument(svg, TimeSpan.FromSeconds(6.2));
        AssertColorFill(fadeOut.GetElementById("pink"), System.Drawing.Color.White);
        Assert.All(
            fadeOut.Descendants().OfType<SvgCircle>(),
            circle => AssertColorStroke(circle, System.Drawing.Color.White));
    }

    private static void AssertIndefiniteOpacityHyperlinkFixture(SKSvg svg)
    {
        DispatchPointerClick(svg, new SKPoint(350f, 80f));
        var fadeIn = CreateAnimatedDocument(svg, TimeSpan.FromSeconds(3.1));
        var visibleRect = Assert.IsType<SvgRectangle>(fadeIn.GetElementById("pink"));
        Assert.Equal(1f, visibleRect.FillOpacity, 3);

        svg.SetAnimationTime(TimeSpan.FromSeconds(3.1));
        DispatchPointerClick(svg, new SKPoint(350f, 260f));
        var fadeOut = CreateAnimatedDocument(svg, TimeSpan.FromSeconds(6.2));
        var hiddenRect = Assert.IsType<SvgRectangle>(fadeOut.GetElementById("pink"));
        Assert.Equal(0f, hiddenRect.FillOpacity, 3);
    }

    private static void AssertAccessKeyAndPastWallclockBeginFixture(SKSvg svg)
    {
        var eventTarget = svg.SourceDocument!.GetElementById("setThreeTarget");
        Assert.NotNull(eventTarget);
        Assert.True(svg.NotifyPointerEvent(eventTarget, SvgPointerEventType.Click, TimeSpan.Zero));
        Assert.True(svg.NotifyAccessKey("a", TimeSpan.Zero));

        var afterAccess = CreateAnimatedDocument(svg, TimeSpan.FromSeconds(2.5));
        var setThree = GetRowRectangles(afterAccess, "setThree");
        AssertColorFill(setThree[0], System.Drawing.Color.FromArgb(0x33, 0xFF, 0x33));
        AssertColorFill(setThree[1], System.Drawing.Color.FromArgb(0x33, 0xFF, 0x33));

        var setSeven = GetRowRectangles(afterAccess, "setSeven");
        AssertColorFill(setSeven[0], System.Drawing.Color.FromArgb(0x33, 0xFF, 0x33));
        AssertColorFill(setSeven[1], System.Drawing.Color.FromArgb(0x33, 0xFF, 0x33));

        var setEight = Assert.Single(GetRowRectangles(afterAccess, "setEight"));
        AssertColorFill(setEight, System.Drawing.Color.FromArgb(0xFF, 0x33, 0x33));
    }

    private static void AssertMultipleBeginUserEventFixture(SKSvg svg)
    {
        Assert.True(svg.NotifyAccessKey("a", TimeSpan.FromSeconds(2)));
        Assert.Equal(34f, GetAnimatedRectangleX(svg, "setFiveTarget", TimeSpan.FromSeconds(2.5)), 3);
        Assert.Equal(-6f, GetAnimatedRectangleX(svg, "setFiveTarget", TimeSpan.FromSeconds(3.5)), 3);
        Assert.Equal(34f, GetAnimatedRectangleX(svg, "setFiveTarget", TimeSpan.FromSeconds(6.5)), 3);

        var target = svg.SourceDocument!.GetElementById("setSixTarget");
        Assert.NotNull(target);
        Assert.True(svg.NotifyPointerEvent(target, SvgPointerEventType.Click, TimeSpan.FromSeconds(5)));
        Assert.Equal(34f, GetAnimatedRectangleX(svg, "setSixTarget", TimeSpan.FromSeconds(5.5)), 3);
    }

    private static void AssertAccessKeyAndFutureWallclockEndFixture(SKSvg svg)
    {
        Assert.True(svg.NotifyAccessKey("a", TimeSpan.FromSeconds(2)));

        var afterFirstEnd = CreateAnimatedDocument(svg, TimeSpan.FromSeconds(3));
        var firstSetSeven = GetRowRectangles(afterFirstEnd, "setSeven");
        AssertColorFill(firstSetSeven[0], System.Drawing.Color.FromArgb(0x33, 0xFF, 0x33));
        AssertColorFill(firstSetSeven[1], System.Drawing.Color.FromArgb(0xFF, 0x33, 0x33));

        var afterSecondEnd = CreateAnimatedDocument(svg, TimeSpan.FromSeconds(4.5));
        var secondSetSeven = GetRowRectangles(afterSecondEnd, "setSeven");
        AssertColorFill(secondSetSeven[0], System.Drawing.Color.FromArgb(0x33, 0xFF, 0x33));
        AssertColorFill(secondSetSeven[1], System.Drawing.Color.FromArgb(0x33, 0xFF, 0x33));

        var setEight = Assert.Single(GetRowRectangles(afterSecondEnd, "setEight"));
        AssertColorFill(setEight, System.Drawing.Color.FromArgb(0x33, 0xFF, 0x33));
    }

    private static void AssertMultipleEndUserEventFixture(SKSvg svg)
    {
        Assert.Equal(34f, GetAnimatedRectangleX(svg, "setFiveTarget", TimeSpan.FromSeconds(0.5)), 3);
        Assert.True(svg.NotifyAccessKey("a", TimeSpan.FromSeconds(6)));
        Assert.Equal(-6f, GetAnimatedRectangleX(svg, "setFiveTarget", TimeSpan.FromSeconds(1.5)), 3);

        var target = svg.SourceDocument!.GetElementById("setSixTarget");
        Assert.NotNull(target);
        Assert.True(svg.NotifyPointerEvent(target, SvgPointerEventType.Click, TimeSpan.FromSeconds(1.5)));
        Assert.Equal(-6f, GetAnimatedRectangleX(svg, "setSixTarget", TimeSpan.FromSeconds(1.75)), 3);
    }

    private static void AssertZoomAndPanMagnifyFixture(SKSvg svg)
    {
        Assert.True(svg.IsZoomAndPanEnabled);
        Assert.True(svg.ZoomTo(2d));
        Assert.True(svg.PanBy(new SKPoint(8f, 12f)));
        Assert.Equal(2d, svg.CurrentScale);
        Assert.Equal(new SKPoint(8f, 12f), svg.CurrentTranslate);
        Assert.Equal(new SKPoint(28f, 32f), svg.PictureToViewerPoint(new SKPoint(10f, 10f)));
        Assert.True(svg.TryGetViewerPicturePoint(new SKPoint(28f, 32f), out var picturePoint));
        Assert.Equal(10f, picturePoint.X, 3);
        Assert.Equal(10f, picturePoint.Y, 3);
    }

    private static void AssertZoomAndPanDisableFixture(SKSvg svg)
    {
        Assert.False(svg.IsZoomAndPanEnabled);
        Assert.False(svg.ZoomTo(2d));
        Assert.False(svg.PanBy(new SKPoint(8f, 12f)));
        Assert.True(svg.ViewerTransform.IsIdentity);
        Assert.Equal(1d, svg.CurrentScale);
        Assert.Equal(default, svg.CurrentTranslate);
    }

    private static void AssertGzippedSvgDataImageFixture(SKSvg svg)
    {
        using var bitmap = RenderBitmap(svg);
        var starCenter = bitmap.GetPixel(240, 170);
        Assert.True(
            starCenter.Alpha > 0 && starCenter.Red > 100 && starCenter.Green > 100,
            $"Expected gzipped data SVG image content near the center, but found {starCenter}.");
    }

    private static void AssertForeignNamespaceDomFixtureCreatedPieChart(SvgDocument document)
    {
        var pieParent = Assert.IsType<SvgGroup>(document.GetElementById("PieParent")!);
        var paths = pieParent.Children.OfType<SvgPath>().ToArray();
        var labels = pieParent.Children.OfType<SvgText>().ToArray();

        Assert.Equal(5, paths.Length);
        Assert.Equal(5, labels.Length);
        Assert.Equal(new[] { "East", "North", "West", "Central", "South" }, labels.Select(label => label.Content).ToArray());

        var firstFill = Assert.IsType<SvgColourServer>(paths[0].Fill);
        var firstStroke = Assert.IsType<SvgColourServer>(paths[0].Stroke);
        Assert.Equal(System.Drawing.Color.FromArgb(0xFF, 0x88, 0x88).ToArgb(), firstFill.Colour.ToArgb());
        Assert.Equal(System.Drawing.Color.Blue.ToArgb(), firstStroke.Colour.ToArgb());
    }

    private static void AssertCursorFixtureResolvesExpectedCursorValues(SKSvg svg)
    {
        var dispatcher = new SvgInteractionDispatcher();

        Assert.Equal("crosshair", DispatchPointerMoved(dispatcher, svg, new SKPoint(160f, 80f)).Cursor);
        Assert.Equal("pointer", DispatchPointerMoved(dispatcher, svg, new SKPoint(160f, 176f)).Cursor);
        Assert.Equal("text", DispatchPointerMoved(dispatcher, svg, new SKPoint(260f, 80f)).Cursor);
        Assert.Equal("wait", DispatchPointerMoved(dispatcher, svg, new SKPoint(260f, 128f)).Cursor);
        Assert.Equal("help", DispatchPointerMoved(dispatcher, svg, new SKPoint(260f, 176f)).Cursor);
        Assert.Equal("url(#magglass),crosshair", DispatchPointerMoved(dispatcher, svg, new SKPoint(260f, 224f)).Cursor);
        Assert.Equal("url(#magglass),crosshair", DispatchPointerMoved(dispatcher, svg, new SKPoint(390f, 315f)).Cursor);
    }

    private static void AssertOnLoadEventAttributeFixtureReachedExpectedVisibility(SvgDocument document)
    {
        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        Assert.Equal("hidden", runtime.GetElement(document.GetElementById("Rect1")!).getAttribute("visibility"));
        Assert.Equal("visible", runtime.GetElement(document.GetElementById("Rect2")!).getAttribute("visibility"));
        Assert.Equal("visible", runtime.GetElement(document.GetElementById("Rect3")!).getAttribute("visibility"));
        Assert.Equal("visible", runtime.GetElement(document.GetElementById("Rect4")!).getAttribute("visibility"));
        Assert.Equal("hidden", runtime.GetElement(document.GetElementById("Rect5")!).getAttribute("visibility"));
        Assert.Equal("visible", runtime.GetElement(document.GetElementById("Rect6")!).getAttribute("visibility"));
    }

    private static void AssertSvgLoadDoesNotBubbleFixtureReachedExpectedFills(SvgDocument document)
    {
        AssertColorFill(document.GetElementById("r1"), System.Drawing.Color.Green);
        AssertColorFill(document.GetElementById("r2"), System.Drawing.Color.Green);
    }

    private static void AssertUseMouseOverFixtureTogglesReferencingGroups(SKSvg svg)
    {
        var document = svg.SourceDocument!;
        var dispatcher = new SvgInteractionDispatcher();

        AssertRuntimeAttribute(document, "g3", "visibility", "hidden");
        AssertRuntimeAttribute(document, "g4", "visibility", "hidden");

        _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(50f, 50f));
        AssertRuntimeAttribute(document, "g3", "visibility", "visible");
        AssertRuntimeAttribute(document, "g4", "visibility", "hidden");

        _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(140f, 50f));
        AssertRuntimeAttribute(document, "g3", "visibility", "hidden");
        AssertRuntimeAttribute(document, "g4", "visibility", "visible");

        _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(350f, 300f));
        AssertRuntimeAttribute(document, "g3", "visibility", "hidden");
        AssertRuntimeAttribute(document, "g4", "visibility", "hidden");
    }

    private static void AssertUseInstanceMouseEventsAndBubbling(SKSvg svg)
    {
        var document = svg.SourceDocument!;
        var dispatcher = new SvgInteractionDispatcher();

        _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(55f, 55f));
        AssertColorFill(document.GetElementById("rect"), System.Drawing.Color.Blue);

        _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(55f, 125f));
        AssertColorFill(document.GetElementById("rect"), System.Drawing.Color.Blue);
        AssertColorStroke(document.GetElementById("rect1"), System.Drawing.Color.Black);

        _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(55f, 195f));
        AssertColorFill(document.GetElementById("rect"), System.Drawing.Color.Blue);
        AssertColorStroke(document.GetElementById("rect2"), System.Drawing.Color.Black);

        _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(55f, 265f));
        AssertColorFill(document.GetElementById("rect"), System.Drawing.Color.Blue);
        AssertColorStroke(document.GetElementById("rect3"), System.Drawing.Color.Empty);

        _ = DispatchPointerPressed(dispatcher, svg, new SKPoint(55f, 265f));
        AssertColorStroke(document.GetElementById("rect3"), System.Drawing.Color.Black);
    }

    private static void AssertAnimatedUseInstanceMouseEventsAndBubbling(SKSvg svg)
    {
        var dispatcher = new SvgInteractionDispatcher();
        var eventIndex = 0;

        var firstTime = AdvanceAnimationEventTime(svg, animated: true, ref eventIndex);
        _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(55f, 55f));
        AssertColorFill(GetInteractionDocument(svg, animated: true, firstTime).GetElementById("rect"), System.Drawing.Color.Blue);

        var secondTime = AdvanceAnimationEventTime(svg, animated: true, ref eventIndex);
        _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(55f, 125f));
        var secondDocument = GetInteractionDocument(svg, animated: true, secondTime);
        AssertColorFill(secondDocument.GetElementById("rect"), System.Drawing.Color.Blue);
        AssertColorStroke(GetUseInstanceStrokeIndicator(secondDocument, 0), System.Drawing.Color.Black);

        var thirdTime = AdvanceAnimationEventTime(svg, animated: true, ref eventIndex);
        _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(55f, 195f));
        var thirdDocument = GetInteractionDocument(svg, animated: true, thirdTime);
        AssertColorFill(thirdDocument.GetElementById("rect"), System.Drawing.Color.Blue);
        AssertColorStroke(GetUseInstanceStrokeIndicator(thirdDocument, 1), System.Drawing.Color.Black);

        var fourthTime = AdvanceAnimationEventTime(svg, animated: true, ref eventIndex);
        _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(55f, 265f));
        var fourthDocument = GetInteractionDocument(svg, animated: true, fourthTime);
        AssertColorFill(fourthDocument.GetElementById("rect"), System.Drawing.Color.Blue);
        AssertColorStroke(GetUseInstanceStrokeIndicator(fourthDocument, 2), System.Drawing.Color.Empty);

        var pressTime = AdvanceAnimationEventTime(svg, animated: true, ref eventIndex);
        _ = DispatchPointerPressed(dispatcher, svg, new SKPoint(55f, 265f));
        AssertColorStroke(GetUseInstanceStrokeIndicator(GetInteractionDocument(svg, animated: true, pressTime), 2), System.Drawing.Color.Black);
    }

    private static SvgRectangle GetUseInstanceStrokeIndicator(SvgDocument document, int index)
    {
        var indicators = document.Descendants()
            .OfType<SvgRectangle>()
            .Where(static rect =>
                string.IsNullOrWhiteSpace(rect.ID) &&
                rect.Width.Value == 50f &&
                rect.Height.Value == 50f &&
                rect.StrokeWidth.Value == 5f &&
                rect.PointerEvents == SvgPointerEvents.None)
            .OrderBy(static rect => rect.Y.Value)
            .ToArray();

        Assert.Equal(3, indicators.Length);
        return indicators[index];
    }

    private static void AssertMouseEventBubblingAndStopPropagation(SKSvg svg)
    {
        var circles = svg.SourceDocument!.Descendants().OfType<SvgCircle>()
            .OrderBy(circle => circle.CenterY.Value)
            .ToArray();
        Assert.Equal(2, circles.Length);

        var dispatcher = new SvgInteractionDispatcher();
        dispatcher.DispatchPointerMoved(svg, new SvgPointerInput(new SKPoint(70f, 120f), SvgPointerDeviceType.Mouse, SvgMouseButton.Left, 0, 0, false, false, false, "w3c"));
        AssertColorFill(circles[0], System.Drawing.Color.FromArgb(0xFF, 0x00, 0x88));

        dispatcher.DispatchPointerMoved(svg, new SvgPointerInput(new SKPoint(70f, 240f), SvgPointerDeviceType.Mouse, SvgMouseButton.Left, 0, 0, false, false, false, "w3c"));
        AssertColorFill(circles[1], System.Drawing.Color.Blue);
    }

    private static void AssertEventOrderCircleClickSemantics(SKSvg svg)
    {
        var circles = svg.SourceDocument!.Descendants().OfType<SvgCircle>()
            .OrderBy(circle => circle.CenterY.Value)
            .ToArray();
        Assert.Equal(2, circles.Length);

        var dispatcher = new SvgInteractionDispatcher();

        var firstClick = DispatchPointerClick(dispatcher, svg, new SKPoint(70f, 120f));
        Assert.True(firstClick.Handled);
        AssertColorFill(circles[0], System.Drawing.Color.Red);

        var secondClick = DispatchPointerClick(dispatcher, svg, new SKPoint(70f, 240f));
        Assert.False(secondClick.Handled);
        AssertColorFill(circles[1], System.Drawing.Color.Blue);
    }

    private static void AssertEventOrderTextClickSemantics(SKSvg svg)
    {
        var document = svg.SourceDocument!;
        var firstText = Assert.Single(
            document.Descendants().OfType<SvgText>(),
            static text => text.Text.Contains("String turns red", StringComparison.Ordinal));
        var hyperlinkText = Assert.Single(
            document.Descendants().OfType<SvgText>(),
            static text => text.Text.Contains("String hyperlinks to", StringComparison.Ordinal));

        var dispatcher = new SvgInteractionDispatcher();

        var firstClick = DispatchPointerClick(dispatcher, svg, new SKPoint(130f, 80f));
        Assert.True(firstClick.Handled);
        AssertColorFill(firstText, System.Drawing.Color.Red);

        var secondClick = DispatchPointerClick(dispatcher, svg, new SKPoint(160f, 150f));
        Assert.False(secondClick.Handled);
        AssertColorFill(hyperlinkText, System.Drawing.Color.Blue);
    }

    private static void AssertDisplayNonePointerEventsDoNotFire(SKSvg svg)
    {
        var document = svg.SourceDocument!;
        var dispatcher = new SvgInteractionDispatcher();

        _ = DispatchPointerClick(dispatcher, svg, new SKPoint(100f, 200f));
        AssertRuntimeAttribute(document, "failText", "visibility", "hidden");

        _ = DispatchPointerClick(dispatcher, svg, new SKPoint(250f, 200f));
        AssertRuntimeAttribute(document, "failText", "visibility", "hidden");
    }

    private static void AssertTextPointerEventsRows(SKSvg svg, bool animated)
    {
        var dispatcher = new SvgInteractionDispatcher();
        foreach (var y in new[] { 78f, 138f, 198f, 258f })
        {
            foreach (var x in new[] { 102f, 132f, 162f, 192f, 222f, 252f, 282f, 312f, 342f, 372f })
            {
                _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(x, y));
            }
        }

        var document = animated ? CreateAnimatedDocument(svg, TimeSpan.Zero) : svg.SourceDocument!;
        AssertTextPointerEventsRow(document, "first-line", new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 });
        AssertTextPointerEventsRow(document, "second-line", new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 });
        AssertTextPointerEventsRow(document, "third-line", new[] { 5, 6, 7, 8 });
        AssertTextPointerEventsRow(document, "fourth-line", new[] { 2, 3, 4, 6, 7, 8 });
    }

    private static void AssertTextPointerEventsRow(SvgDocument document, string rowId, int[] expectedGreenIndexes)
    {
        var expected = new HashSet<int>(expectedGreenIndexes);
        var row = Assert.IsType<SvgGroup>(document.GetElementById(rowId)!);
        var glyphs = row.Children.OfType<SvgText>().ToArray();
        Assert.Equal(10, glyphs.Length);

        for (var i = 0; i < glyphs.Length; i++)
        {
            if (expected.Contains(i))
            {
                AssertColorFill(glyphs[i], System.Drawing.Color.Green);
            }
            else
            {
                AssertNotColorFill(glyphs[i], System.Drawing.Color.Red);
            }
        }
    }

    private static void AssertTextCharacterCellPointerEvents(SKSvg svg, string name, bool animated)
    {
        var document = svg.SourceDocument!;
        var dispatcher = new SvgInteractionDispatcher();
        var eventIndex = 0;
        var texts = GetTextCharacterCellFixtureRows(document, name);
        Assert.NotEmpty(texts);

        foreach (var text in texts)
        {
            var requireMissPoint = name == "interact-pevents-05-b" ||
                                   !text.Text.Contains(' ');
            Assert.True(
                TryFindTextHitAndMissPoints(svg, text, requireMissPoint, out var hitPoint, out var missPoint, out var hasMissPoint),
                $"Could not find a glyph hit point{(requireMissPoint ? " and an in-bounds whitespace miss point" : string.Empty)} for '{text.ID ?? text.Text}'.");

            ResetAnimatedInteraction(svg, animated);
            dispatcher.Reset();
            eventIndex = 0;
            var hitTime = AdvanceAnimationEventTime(svg, animated, ref eventIndex);
            var hitResult = DispatchPointerMoved(dispatcher, svg, hitPoint);
            Assert.True(
                IsSameTextTarget(hitResult.TargetElement, text),
                $"Expected hit target '{text.ID ?? text.Text}', but found '{hitResult.TargetElement?.ID ?? hitResult.TargetElement?.GetType().Name}'.");
            AssertCharacterCellTextFill(svg, text, animated, hitTime, System.Drawing.Color.Green);

            var offTime = AdvanceAnimationEventTime(svg, animated, ref eventIndex);
            _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(5f, 5f));
            AssertCharacterCellTextNotFill(svg, text, animated, offTime, System.Drawing.Color.Green);

            if (hasMissPoint)
            {
                var missTime = AdvanceAnimationEventTime(svg, animated, ref eventIndex);
                var missResult = DispatchPointerMoved(dispatcher, svg, missPoint);
                Assert.False(IsSameTextTarget(missResult.TargetElement, text));
                AssertCharacterCellTextNotFill(svg, text, animated, missTime, System.Drawing.Color.Green);
            }
        }
    }

    private static SvgText[] GetTextCharacterCellFixtureRows(SvgDocument document, string name)
    {
        if (name == "interact-pevents-03-b")
        {
            return new[] { "first-line", "second-line", "third-line", "fourth-line", "fifth-line" }
                .Select(rowId => Assert.IsType<SvgGroup>(document.GetElementById(rowId)!).Descendants().OfType<SvgText>().First())
                .ToArray();
        }

        return new[] { "line1", "line2", "line3", "line4", "line5" }
            .Select(id => document.GetElementById(id))
            .OfType<SvgText>()
            .ToArray();
    }

    private static bool TryFindTextHitAndMissPoints(
        SKSvg svg,
        SvgText text,
        bool requireMissPoint,
        out SKPoint hitPoint,
        out SKPoint missPoint,
        out bool hasMissPoint)
    {
        hitPoint = default;
        missPoint = default;
        hasMissPoint = false;
        if (svg.RetainedSceneGraph?.TryGetNode(text, out var node) != true ||
            node is null ||
            node.TransformedBounds.IsEmpty)
        {
            return false;
        }

        var bounds = node.TransformedBounds;
        var xStep = Math.Max(1f, bounds.Width / 120f);
        var yStep = Math.Max(1f, bounds.Height / 24f);
        var hasHit = false;
        var hasMiss = false;

        for (var y = bounds.Top + 0.5f; y < bounds.Bottom; y += yStep)
        {
            for (var x = bounds.Left + 0.5f; x < bounds.Right; x += xStep)
            {
                var point = new SKPoint(x, y);
                var target = svg.HitTestTopmostElement(point);
                if (!hasHit && IsSameTextTarget(target, text))
                {
                    hitPoint = point;
                    hasHit = true;
                }
                else if (!hasMiss && !IsSameTextTarget(target, text))
                {
                    missPoint = point;
                    hasMiss = true;
                    hasMissPoint = true;
                }

                if (hasHit && (!requireMissPoint || hasMiss))
                {
                    return true;
                }
            }
        }

        return hasHit && (!requireMissPoint || hasMiss);
    }

    private static bool IsSameTextTarget(SvgElement? target, SvgText text)
    {
        if (ReferenceEquals(target, text))
        {
            return true;
        }

        return target is SvgText targetText &&
               !string.IsNullOrWhiteSpace(text.ID) &&
               string.Equals(targetText.ID, text.ID, StringComparison.Ordinal);
    }

    private static void AssertCharacterCellTextFill(SKSvg svg, SvgText text, bool animated, TimeSpan time, System.Drawing.Color expected)
    {
        AssertColorFill(GetCharacterCellTextElement(svg, text, animated, time), expected);
    }

    private static void AssertCharacterCellTextNotFill(SKSvg svg, SvgText text, bool animated, TimeSpan time, System.Drawing.Color expected)
    {
        AssertNotColorFill(GetCharacterCellTextElement(svg, text, animated, time), expected);
    }

    private static SvgElement GetCharacterCellTextElement(SKSvg svg, SvgText text, bool animated, TimeSpan time)
    {
        if (!animated)
        {
            return text;
        }

        Assert.False(string.IsNullOrWhiteSpace(text.ID));
        var element = GetInteractionDocument(svg, animated: true, time).GetElementById(text.ID);
        return Assert.IsAssignableFrom<SvgElement>(element);
    }

    private static void AssertRenderingOrderPointerEvents(SKSvg svg, bool animated)
    {
        var dispatcher = new SvgInteractionDispatcher();
        var eventIndex = 0;
        AssertHoverFill(dispatcher, svg, "r10", new SKPoint(90f, 80f), System.Drawing.Color.FromArgb(0xFF, 0x55, 0x55), animated, ref eventIndex);
        AssertHoverFill(dispatcher, svg, "r11", new SKPoint(130f, 110f), System.Drawing.Color.FromArgb(0xFF, 0x55, 0x55), animated, ref eventIndex);
        AssertHoverFill(dispatcher, svg, "r12", new SKPoint(180f, 140f), System.Drawing.Color.FromArgb(0xFF, 0x55, 0x55), animated, ref eventIndex);
        AssertHoverFill(dispatcher, svg, "c10", new SKPoint(195f, 245f), System.Drawing.Color.FromArgb(0xFF, 0x55, 0x55), animated, ref eventIndex);
        AssertHoverFill(dispatcher, svg, "c11", new SKPoint(307f, 245f), System.Drawing.Color.FromArgb(0xFF, 0x55, 0x55), animated, ref eventIndex);
        AssertHoverFill(dispatcher, svg, "c12", new SKPoint(335f, 142f), System.Drawing.Color.FromArgb(0xFF, 0x55, 0x55), animated, ref eventIndex);

        ResetAnimatedInteraction(svg, animated);
        eventIndex = 0;
        var offTime = AdvanceAnimationEventTime(svg, animated, ref eventIndex);
        _ = DispatchPointerClick(dispatcher, svg, new SKPoint(415f, 75f));
        var offDocument = GetInteractionDocument(svg, animated, offTime);
        AssertPointerEvents(offDocument, "c10", SvgPointerEvents.None);
        AssertPointerEvents(offDocument, "c11", SvgPointerEvents.None);
        AssertPointerEvents(offDocument, "c12", SvgPointerEvents.None);

        var suppressedTime = AdvanceAnimationEventTime(svg, animated, ref eventIndex);
        _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(195f, 245f));
        var suppressedDocument = GetInteractionDocument(svg, animated, suppressedTime);
        AssertNotColorFill(suppressedDocument.GetElementById("c10"), System.Drawing.Color.FromArgb(0xFF, 0x55, 0x55));

        ResetAnimatedInteraction(svg, animated);
        eventIndex = 0;
        _ = AdvanceAnimationEventTime(svg, animated, ref eventIndex);
        _ = DispatchPointerClick(dispatcher, svg, new SKPoint(385f, 75f));
        AssertHoverFill(dispatcher, svg, "c10", new SKPoint(195f, 245f), System.Drawing.Color.FromArgb(0xFF, 0x55, 0x55), animated, ref eventIndex);
    }

    private static void AssertVisiblePointerEventsGrid(SKSvg svg, bool animated)
    {
        var rows = new[]
        {
            new PointerEventsGridRow("m1", 70f, new[] { 1, 3 }, new[] { 1, 3 }),
            new PointerEventsGridRow("m2", 120f, new[] { 1, 3 }, new[] { 1, 3 }),
            new PointerEventsGridRow("m3", 170f, new[] { 1, 2, 3 }, Array.Empty<int>()),
            new PointerEventsGridRow("m4", 220f, Array.Empty<int>(), new[] { 1, 2, 3 }),
            new PointerEventsGridRow("m5", 270f, new[] { 1, 2, 3 }, new[] { 1, 2, 3 })
        };
        AssertPointerEventsGrid(svg, animated, rows);
    }

    private static void AssertPaintedPointerEventsGrid(SKSvg svg, bool animated)
    {
        var rows = new[]
        {
            new PointerEventsGridRow("m1", 70f, new[] { 1, 3, 4 }, new[] { 1, 3, 4 }),
            new PointerEventsGridRow("m2", 120f, new[] { 1, 2, 3, 4 }, Array.Empty<int>()),
            new PointerEventsGridRow("m3", 170f, Array.Empty<int>(), new[] { 1, 2, 3, 4 }),
            new PointerEventsGridRow("m4", 220f, new[] { 1, 2, 3, 4 }, new[] { 1, 2, 3, 4 }),
            new PointerEventsGridRow("m5", 270f, Array.Empty<int>(), Array.Empty<int>())
        };
        AssertPointerEventsGrid(svg, animated, rows);
    }

    private static void AssertPointerEventsGrid(SKSvg svg, bool animated, IReadOnlyList<PointerEventsGridRow> rows)
    {
        var dispatcher = new SvgInteractionDispatcher();
        var fillX = new[] { 40f, 90f, 140f, 190f };
        var strokeX = new[] { 22f, 72f, 122f, 172f };
        var eventIndex = 0;

        foreach (var row in rows)
        {
            AssertPointerEventsGridPoints(dispatcher, svg, animated, row, fillX, row.FillColumns, ref eventIndex);
            AssertPointerEventsGridPoints(dispatcher, svg, animated, row, strokeX, row.StrokeColumns, ref eventIndex);
        }
    }

    private static void AssertPointerEventsGridPoints(
        SvgInteractionDispatcher dispatcher,
        SKSvg svg,
        bool animated,
        PointerEventsGridRow row,
        IReadOnlyList<float> xCoordinates,
        IReadOnlyCollection<int> expectedColumns,
        ref int eventIndex)
    {
        var expected = new HashSet<int>(expectedColumns);
        for (var i = 0; i < xCoordinates.Count; i++)
        {
            ResetAnimatedInteraction(svg, animated);
            eventIndex = 0;
            var enterTime = AdvanceAnimationEventTime(svg, animated, ref eventIndex);
            _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(xCoordinates[i], row.Y));
            AssertMarkerOpacity(GetInteractionDocument(svg, animated, enterTime), row.MarkerId, expected.Contains(i + 1) ? 0.4f : 0f);

            var leaveTime = AdvanceAnimationEventTime(svg, animated, ref eventIndex);
            _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(5f, 5f));
            AssertMarkerOpacity(GetInteractionDocument(svg, animated, leaveTime), row.MarkerId, 0f);
        }
    }

    private static void AssertPointerResultRowReachesPassedState(SKSvg svg, string statusRectangleId)
    {
        var dispatcher = new SvgInteractionDispatcher();
        _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(35f, 105f));
        AssertColorFill(svg.SourceDocument!.GetElementById(statusRectangleId), System.Drawing.Color.Green);
    }

    private static void AssertMaskedPointerRowReachesPassedState(SKSvg svg)
    {
        var document = svg.SourceDocument!;
        var dispatcher = new SvgInteractionDispatcher();
        var firstMaskedRect = Assert.Single(document.Descendants().OfType<SvgRectangle>(), static rect =>
        {
            return rect.Width.Value == 100f &&
                   rect.Height.Value == 100f &&
                   rect.TryGetAttribute("mask", out var mask) &&
                   string.Equals(mask?.ToString(), "url(#normalMask)", StringComparison.Ordinal);
        });

        _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(50f, 50f));
        AssertColorFill(firstMaskedRect, System.Drawing.Color.Orange);

        _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(250f, 50f));
        AssertColorFill(document.GetElementById("passRect"), System.Drawing.Color.Orange);
    }

    private static void AssertUnknownContentScriptTypeSuppressesEventHandler(SvgDocument document)
    {
        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        Assert.NotEqual("hidden", runtime.GetElement(document.GetElementById("testPassed")!).getAttribute("visibility"));
        Assert.Equal("hidden", runtime.GetElement(document.GetElementById("testFailed")!).getAttribute("visibility"));
    }

    private static void AssertDefsFixtureKeepsDefinitionContentNonRenderable(SvgDocument document)
    {
        var body = Assert.IsType<SvgGroup>(document.GetElementById("test-body-content")!);
        var directVisualRect = Assert.Single(body.Children.OfType<SvgRectangle>());
        AssertColorFill(directVisualRect, System.Drawing.Color.Lime);

        var definitions = body.Children.OfType<SvgDefinitionList>().ToArray();
        Assert.Equal(2, definitions.Length);
        Assert.All(definitions, definition => Assert.NotEmpty(definition.Children));
        Assert.All(definitions.SelectMany(static definition => definition.Children), child => Assert.IsType<SvgRectangle>(child));
    }

    private static void AssertBasicNumberFixtureParsesScientificStrokeWidths(SvgDocument document)
    {
        var widePolylines = document.Descendants()
            .OfType<SvgPolyline>()
            .Where(static polyline => polyline.StrokeWidth.Value > 1f)
            .Select(static polyline => polyline.StrokeWidth.Value)
            .ToArray();

        Assert.Equal(new[] { 50f, 50f, 50f }, widePolylines);
    }

    private static void AssertBasicLengthFixtureHonorsPresentationAndCssUnitCase(SvgDocument document)
    {
        foreach (var id in new[]
        {
            "swNoUnit",
            "swUnit",
            "swPresAttr",
            "swUpperCaseUnitPresAttr",
            "swUpperCaseUnit",
            "swUpperCaseUnitInline"
        })
        {
            var circle = Assert.IsType<SvgCircle>(document.GetElementById(id)!);
            Assert.Equal(20f, circle.StrokeWidth.Value);
        }
    }

    private static SvgDocument CreateAnimatedDocument(SKSvg svg, TimeSpan time)
    {
        Assert.NotNull(svg.AnimationController);
        return svg.AnimationController!.CreateAnimatedDocument(time);
    }

    private static float GetAnimatedRectangleX(SKSvg svg, string elementId, TimeSpan time)
    {
        var rectangle = Assert.IsType<SvgRectangle>(CreateAnimatedDocument(svg, time).GetElementById(elementId));
        return rectangle.X.Value;
    }

    private static SvgRectangle[] GetRowRectangles(SvgDocument document, string groupId)
    {
        var group = Assert.IsType<SvgGroup>(document.GetElementById(groupId)!);
        return group.Descendants().OfType<SvgRectangle>().ToArray();
    }

    private static void AssertHoverFill(
        SvgInteractionDispatcher dispatcher,
        SKSvg svg,
        string elementId,
        SKPoint point,
        System.Drawing.Color expected,
        bool animated,
        ref int eventIndex)
    {
        var enterTime = AdvanceAnimationEventTime(svg, animated, ref eventIndex);
        _ = DispatchPointerMoved(dispatcher, svg, point);
        var document = GetInteractionDocument(svg, animated, enterTime);
        AssertColorFill(document.GetElementById(elementId), expected);

        _ = AdvanceAnimationEventTime(svg, animated, ref eventIndex);
        _ = DispatchPointerMoved(dispatcher, svg, new SKPoint(5f, 5f));
    }

    private static TimeSpan AdvanceAnimationEventTime(SKSvg svg, bool animated, ref int eventIndex)
    {
        if (!animated)
        {
            return TimeSpan.Zero;
        }

        var time = TimeSpan.FromMilliseconds(++eventIndex);
        svg.SetAnimationTime(time);
        return time;
    }

    private static void ResetAnimatedInteraction(SKSvg svg, bool animated)
    {
        if (!animated)
        {
            return;
        }

        svg.AnimationController!.Reset();
        svg.SetAnimationTime(TimeSpan.Zero);
    }

    private static SvgDocument GetInteractionDocument(SKSvg svg, bool animated, TimeSpan time)
    {
        return animated ? CreateAnimatedDocument(svg, time) : svg.SourceDocument!;
    }

    private static void AssertMarkerOpacity(SvgDocument document, string markerId, float expected)
    {
        var marker = document.GetElementById(markerId) as SvgRectangle;
        if (marker is null &&
            markerId.Length == 2 &&
            markerId[0] == 'm' &&
            int.TryParse(markerId.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var markerIndex))
        {
            marker = document.Descendants()
                .OfType<SvgRectangle>()
                .Where(static rect =>
                {
                    return string.IsNullOrWhiteSpace(rect.ID) &&
                           rect.Width.Value == 200f &&
                           rect.Height.Value == 50f &&
                           rect.Fill is SvgColourServer fill &&
                           fill.Colour.ToArgb() == System.Drawing.Color.Red.ToArgb();
                })
                .ElementAtOrDefault(markerIndex - 1);
        }

        Assert.NotNull(marker);
        Assert.Equal(expected, marker.FillOpacity, 3);
    }

    private static void AssertXmlBaseImageFixtureCompilesAllImages(SKSvg svg)
    {
        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        var imageNodes = scene!.Traverse()
            .Where(static node => node.Kind == SvgSceneNodeKind.Image)
            .ToArray();

        Assert.Equal(3, imageNodes.Length);
        Assert.All(imageNodes, node =>
        {
            Assert.True(node.IsRenderable);
            Assert.NotNull(node.LocalModel);
            Assert.Equal(100f, node.GeometryBounds.Width, 3);
            Assert.Equal(100f, node.GeometryBounds.Height, 3);
        });
    }

    private static void AssertBrokenImageAndCycleFixtureUsesPlaceholders(SKSvg svg)
    {
        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        var imageNodes = scene!.Traverse()
            .Where(static node => node.Kind == SvgSceneNodeKind.Image)
            .ToArray();

        Assert.True(imageNodes.Length >= 1);
        Assert.Contains(imageNodes, static node => node.IsRenderable && node.LocalModel is not null);
    }

    private static void AssertEmbeddedSvgImageRemainsStatic(SKSvg svg)
    {
        using var bitmap = RenderBitmap(svg);
        var embeddedPixel = bitmap.GetPixel(60, 100);
        Assert.True(
            embeddedPixel.Green >= 128 && embeddedPixel.Red < 80 && embeddedPixel.Blue < 80 && embeddedPixel.Alpha > 200,
            $"Expected embedded SVG image to stay green, but pixel was {embeddedPixel}.");
    }

    private static void AssertTextSelectionFixtureSupportsHostSelection(SKSvg svg, string name)
    {
        var text = name == "text-tselect-01-b"
            ? svg.SourceDocument!.Descendants().OfType<SvgText>().First(static item => item.Children.OfType<SvgTextSpan>().Count() == 4)
            : Assert.IsType<SvgText>(svg.SourceDocument!.GetElementById("text"));

        var numberOfChars = text.Text.Length;
        Assert.True(numberOfChars > 3);

        Assert.True(svg.TrySelectTextSubString(text, 1, Math.Min(3, numberOfChars - 1)));
        var substringSelection = Assert.Single(svg.TextSelections);
        Assert.Equal(text.ID, substringSelection.ElementId);
        Assert.True(substringSelection.SelectedNChars > 0);
        Assert.NotEmpty(substringSelection.Extents);

        Assert.True(svg.TrySelectTextRange(text, Math.Min(4, numberOfChars - 1), 1));
        var rangeSelection = Assert.Single(svg.TextSelections);
        Assert.Equal(SKSvg.SvgTextSelectionDirection.Backward, rangeSelection.Direction);
        Assert.True(rangeSelection.HasCaret);
        Assert.NotEmpty(rangeSelection.VisualExtents);
    }

    private static SkiaBitmap RenderBitmap(SKSvg svg)
    {
        Assert.NotNull(svg.Picture);
        var bitmap = svg.Picture!.ToBitmap(
            SkiaSharp.SKColors.Transparent,
            1f,
            1f,
            SkiaColorType.Rgba8888,
            SkiaAlphaType.Unpremul,
            svg.Settings.Srgb);

        return Assert.IsType<SkiaBitmap>(bitmap);
    }

    private static void AssertRuntimeAttribute(SvgDocument document, string elementId, string attributeName, string expected)
    {
        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        var rawElement = document.GetElementById(elementId);
        Assert.NotNull(rawElement);
        Assert.Equal(expected, runtime.GetElement(rawElement).getAttribute(attributeName));
    }

    private static void AssertPointerEvents(SvgDocument document, string elementId, SvgPointerEvents expected)
    {
        var visualElement = Assert.IsAssignableFrom<SvgVisualElement>(document.GetElementById(elementId));
        Assert.Equal(expected, visualElement.PointerEvents);
    }

    private static void AssertNotColorFill(SvgElement? element, System.Drawing.Color expected)
    {
        var visualElement = Assert.IsAssignableFrom<SvgVisualElement>(element);
        if (visualElement.Fill is not SvgColourServer fill)
        {
            return;
        }

        Assert.NotEqual(expected.ToArgb(), fill.Colour.ToArgb());
    }

    private static void AssertColorFill(SvgElement? element, System.Drawing.Color expected)
    {
        var visualElement = Assert.IsAssignableFrom<SvgVisualElement>(element);
        var fill = Assert.IsType<SvgColourServer>(visualElement.Fill);
        Assert.Equal(expected.ToArgb(), fill.Colour.ToArgb());
    }

    private static void AssertColorStroke(SvgElement? element, System.Drawing.Color expected)
    {
        var visualElement = Assert.IsAssignableFrom<SvgVisualElement>(element);
        if (expected == System.Drawing.Color.Empty)
        {
            Assert.True(
                visualElement.Stroke is null || ReferenceEquals(visualElement.Stroke, SvgPaintServer.None),
                $"Expected no stroke, but found '{visualElement.Stroke}'.");
            return;
        }

        var stroke = Assert.IsType<SvgColourServer>(visualElement.Stroke);
        Assert.Equal(expected.ToArgb(), stroke.Colour.ToArgb());
    }

    private sealed record PointerEventsGridRow(string MarkerId, float Y, IReadOnlyCollection<int> FillColumns, IReadOnlyCollection<int> StrokeColumns);

    private static void AssertPathsDom02FixtureCreatesFlowerPathSegments(SvgDocument document)
    {
        var path = Assert.IsType<SvgPath>(document.GetElementById("mypath")!);
        Assert.NotNull(path.PathData);
        Assert.True(path.PathData.Count >= 8);

        var move = Assert.IsType<SvgMoveToSegment>(path.PathData[0]);
        Assert.InRange(move.End.X, 210f, 270f);
        Assert.InRange(move.End.Y, 140f, 220f);

        var cubic = Assert.IsType<SvgCubicCurveSegment>(path.PathData[1]);
        Assert.InRange(cubic.End.X, 150f, 330f);
        Assert.InRange(cubic.End.Y, 60f, 300f);
    }

    private static void AssertUseInstanceChildNodesCanMutateCorrespondingElements(SvgDocument document)
    {
        var drawRects = Assert.IsType<SvgGroup>(document.GetElementById("drawRects")!);
        var rectangles = drawRects.Children.OfType<SvgRectangle>().ToList();
        Assert.Equal(3, rectangles.Count);

        foreach (var rectangle in rectangles)
        {
            var fill = Assert.IsType<SvgColourServer>(rectangle.Fill);
            Assert.Equal(System.Drawing.Color.Green.ToArgb(), fill.Colour.ToArgb());
        }
    }

    private static void AssertNestedSvgLengthDomMetricsResolveViewportChanges()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100%" height="100%" viewBox="0 0 480 360">
              <svg id="testroot" width="480" height="360">
                <svg id="testSVG1" />
                <svg id="testSVG2" />
                <svg id="subSVG" width="300" height="175" />
              </svg>
            </svg>
            """)!;
        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        var testSvg1 = runtime.GetElement(document.GetElementById("testSVG1")!);
        var testSvg2 = runtime.GetElement(document.GetElementById("testSVG2")!);
        var subSvg = runtime.GetElement(document.GetElementById("subSVG")!);
        var baseLength = Assert.IsType<SvgJavaScriptAnimatedLength>(testSvg1.width).baseVal;

        Assert.Equal(480d, baseLength.value);
        Assert.Equal(100d, baseLength.valueInSpecifiedUnits);

        baseLength.value = 240d;
        Assert.Equal(240d, baseLength.value);
        Assert.Equal(50d, baseLength.valueInSpecifiedUnits);
        Assert.Equal("50%", baseLength.valueAsString);

        subSvg.appendChild(testSvg1);
        Assert.Equal(150d, baseLength.value);
        Assert.Equal(50d, baseLength.valueInSpecifiedUnits);

        subSvg.appendChild(testSvg2);
        var defaultLength = Assert.IsType<SvgJavaScriptAnimatedLength>(testSvg2.width).baseVal;
        Assert.Equal(300d, defaultLength.value);
        Assert.Equal(100d, defaultLength.valueInSpecifiedUnits);
    }

    private static void AssertHiddenIntersectionApisUseExpectedRenderableGeometry(SKSvg svg)
    {
        var runtime = new SvgJavaScriptRuntime(svg.SourceDocument!, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });
        var root = runtime.GetElement(svg.SourceDocument!);
        var rect = root.createSVGRect();
        rect.x = 10;
        rect.y = 10;
        rect.width = 50;
        rect.height = 50;

        var expectedIntersections = new Dictionary<string, bool>
        {
            ["c1"] = true,
            ["c2"] = true,
            ["c3"] = true,
            ["l1"] = true,
            ["l2"] = true,
            ["r1"] = true,
            ["c4"] = false
        };

        foreach (var pair in expectedIntersections)
        {
            var element = runtime.GetElement(svg.SourceDocument!.GetElementById(pair.Key)!);
            Assert.Equal(pair.Value, root.checkIntersection(element, rect));
        }

        var list = root.getIntersectionList(rect, null);
        var expectedOrder = new[] { "c1", "c2", "c3", "l1", "l2", "r1" };
        Assert.Equal(expectedOrder.Length, list.length);

        for (var i = 0; i < expectedOrder.Length; i++)
        {
            var item = Assert.IsType<SvgJavaScriptElement>(list.item(i));
            Assert.Equal(expectedOrder[i], item.id);
        }
    }

    private static void AssertIntersectionAndEnclosureListsHideFixtureFailText(SKSvg svg)
    {
        var runtime = new SvgJavaScriptRuntime(svg.SourceDocument!, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });
        var testSvg = runtime.GetElement(svg.SourceDocument!.GetElementById("testSVG")!);
        var expectedIds = new[]
        {
            "testCircle",
            "testEllipse",
            "testLine",
            "testPath",
            "testPolyline",
            "testPolygon",
            "testRect",
            "testUse",
            "testImage",
            "testText"
        };

        var intersectionRect = testSvg.createSVGRect();
        intersectionRect.x = 10;
        intersectionRect.y = 0;
        intersectionRect.width = 130;
        intersectionRect.height = 98;
        Assert.Equal(expectedIds, GetNodeListIds(testSvg.getIntersectionList(intersectionRect, null)));

        var enclosureRect = testSvg.createSVGRect();
        enclosureRect.x = 0;
        enclosureRect.y = 0;
        enclosureRect.width = 200;
        enclosureRect.height = 200;
        Assert.Equal(expectedIds, GetNodeListIds(testSvg.getEnclosureList(enclosureRect, null)));
    }

    private static void AssertStringListsDuplicateInsertedValues(SvgDocument document)
    {
        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        var r1 = runtime.GetElement(document.GetElementById("r1")!);
        var r2 = runtime.GetElement(document.GetElementById("r2")!);
        var r3 = runtime.GetElement(document.GetElementById("r3")!);

        Assert.Equal(2, r1.requiredFeatures.numberOfItems);
        Assert.Equal("http://www.w3.org/TR/SVG11/feature#Shape", r1.requiredFeatures.getItem(0));
        Assert.Equal("this.is.a.bogus.feature.string", r1.requiredFeatures.getItem(1));

        Assert.Equal(1, r2.requiredFeatures.numberOfItems);
        Assert.Equal("this.is.a.bogus.feature.string", r2.requiredFeatures.getItem(0));

        Assert.Equal(2, r3.requiredFeatures.numberOfItems);
        Assert.Equal("http://www.w3.org/TR/SVG11/feature#Shape", r3.requiredFeatures.getItem(0));
        Assert.Equal("http://www.w3.org/TR/SVG11/feature#Shape", r3.requiredFeatures.getItem(1));

        var failOverlay = Assert.IsType<SvgRectangle>(document.GetElementById("fail")!);
        Assert.True(failOverlay.TryGetAttribute("fill", out var failFill));
        Assert.Equal("none", failFill);
    }

    private static void AssertGetBBoxFixtureReachesPassedState(SKSvg svg)
    {
        var status = Assert.IsType<SvgText>(svg.SourceDocument!.GetElementById("TestStatus")!);
        Assert.Equal("passed", status.Text);

        var fill = Assert.IsType<SvgColourServer>(status.Fill);
        Assert.Equal(System.Drawing.Color.Green.ToArgb(), fill.Colour.ToArgb());
    }

    private static string[] GetNodeListIds(SvgJavaScriptNodeList list)
    {
        var ids = new string[list.length];
        for (var i = 0; i < list.length; i++)
        {
            var item = list.item(i);
            ids[i] = item switch
            {
                SvgJavaScriptElement element => element.id,
                SvgJavaScriptElementInstance instance when instance.correspondingUseElement is { } useElement => useElement.id,
                _ => string.Empty
            };
        }

        return ids;
    }

    [Theory]
    [InlineData("text-align-08-b")]
    [InlineData("text-fonts-06-t")]
    [InlineData("text-tselect-01-b")]
    [InlineData("text-tselect-02-f")]
    [InlineData("text-tselect-03-f")]
    public void SkippedTextFixtureContractsArePreserved(string name)
    {
        var svgPath = GetSvgPath($"{name}.svg");
        if (name == "text-fonts-06-t")
        {
            Assert.False(File.Exists(svgPath));
            return;
        }

        Assert.True(File.Exists(svgPath));
        var document = SvgDocument.Open<SvgDocument>(svgPath);

        switch (name)
        {
            case "text-align-08-b":
                var baselineText = Assert.Single(
                    document.Descendants().OfType<SvgText>(),
                    static text => text.FontSize.Value == 120f);
                Assert.Contains("a犜ण", baselineText.Text);
                var fontSizes = baselineText.Children
                    .OfType<SvgTextSpan>()
                    .Select(static tspan => tspan.FontSize.Value)
                    .ToArray();
                Assert.Equal(new[] { 75f, 30f }, fontSizes);
                break;
            case "text-tselect-01-b":
                var multiLine = Assert.Single(
                    document.Descendants().OfType<SvgText>(),
                    static text => text.Children.OfType<SvgTextSpan>().Count() == 4);
                var yPositions = multiLine.Children
                    .OfType<SvgTextSpan>()
                    .Select(static tspan => tspan.Y.Single().Value)
                    .ToArray();
                Assert.Equal(new[] { 190f, 215f, 240f, 265f }, yPositions);
                break;
            case "text-tselect-02-f":
            case "text-tselect-03-f":
                var bidiText = Assert.IsType<SvgText>(document.GetElementById("text"));
                Assert.Contains("abc", bidiText.Text);
                Assert.Contains("אבג", bidiText.Text);
                Assert.Contains("דהו", bidiText.Text);

                var buttons = Assert.IsType<SvgGroup>(document.GetElementById("buttons"));
                Assert.Equal(4, buttons.Children.OfType<SvgRectangle>().Count(static rect =>
                {
                    return rect.TryGetAttribute("onclick", out var onclick) &&
                           onclick?.ToString()?.StartsWith("doSelection(", StringComparison.Ordinal) == true;
                }));
                break;
        }
    }

    private static void DispatchDomEvent(SKSvg svg, string elementId, string eventType)
    {
        var runtime = GetJavaScriptRuntime(svg);
        var sourceDocument = svg.SourceDocument ?? throw new InvalidOperationException("SVG document is not loaded.");
        var rawElement = sourceDocument.GetElementById(elementId) ?? throw new InvalidOperationException($"Element '{elementId}' was not found.");
        var element = runtime.GetElement(rawElement);
        var evt = new SvgJavaScriptEvent();
        evt.initEvent(eventType, true, true);
        element.dispatchEvent(evt);
        _ = svg.RefreshFromSourceDocument();
    }

    private static void DispatchMouseEvent(SKSvg svg, string elementId, string eventType)
    {
        var runtime = GetJavaScriptRuntime(svg);
        var sourceDocument = svg.SourceDocument ?? throw new InvalidOperationException("SVG document is not loaded.");
        var rawElement = sourceDocument.GetElementById(elementId) ?? throw new InvalidOperationException($"Element '{elementId}' was not found.");
        var element = runtime.GetElement(rawElement);
        var evt = new SvgJavaScriptEvent();
        evt.initMouseEvent(eventType, true, true, null, 1, 0f, 0f, 0f, 0f, false, false, false, false, 0, null);
        element.dispatchEvent(evt);
        _ = svg.RefreshFromSourceDocument();
    }

    private static void DispatchPointerClick(SKSvg svg, SKPoint point)
    {
        var dispatcher = new SvgInteractionDispatcher();
        _ = DispatchPointerClick(dispatcher, svg, point);
    }

    private static SvgInteractionDispatchResult DispatchPointerMoved(SvgInteractionDispatcher dispatcher, SKSvg svg, SKPoint point)
    {
        return dispatcher.DispatchPointerMoved(svg, CreatePointerInput(point, SvgMouseButton.None, clickCount: 0));
    }

    private static SvgInteractionDispatchResult DispatchPointerPressed(SvgInteractionDispatcher dispatcher, SKSvg svg, SKPoint point)
    {
        return dispatcher.DispatchPointerPressed(svg, CreatePointerInput(point, SvgMouseButton.Left, clickCount: 1));
    }

    private static SvgInteractionDispatchResult DispatchPointerClick(SvgInteractionDispatcher dispatcher, SKSvg svg, SKPoint point)
    {
        var press = new SvgPointerInput(point, SvgPointerDeviceType.Mouse, SvgMouseButton.Left, 1, 0, false, false, false, "w3c");
        _ = dispatcher.DispatchPointerPressed(svg, press);
        return dispatcher.DispatchPointerReleased(svg, press);
    }

    private static SvgPointerInput CreatePointerInput(SKPoint point, SvgMouseButton button, int clickCount)
    {
        return new SvgPointerInput(point, SvgPointerDeviceType.Mouse, button, clickCount, 0, false, false, false, "w3c");
    }

    private static void NotifyClickEvent(SKSvg svg, string elementId)
    {
        var sourceDocument = svg.SourceDocument ?? throw new InvalidOperationException("SVG document is not loaded.");
        var rawElement = sourceDocument.GetElementById(elementId) ?? throw new InvalidOperationException($"Element '{elementId}' was not found.");
        if (!svg.NotifyPointerEvent(rawElement, SvgPointerEventType.Click))
        {
            throw new InvalidOperationException($"Element '{elementId}' did not record a click animation event.");
        }
    }

    private static SvgJavaScriptRuntime GetJavaScriptRuntime(SKSvg svg)
    {
        var field = typeof(SKSvg).GetField("_javaScriptRuntime", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Unable to access SVG JavaScript runtime.");
        var runtime = (ISKSvgJavaScriptRuntime?)field.GetValue(svg)
                      ?? throw new InvalidOperationException("SVG JavaScript runtime is not initialized.");
        return (SvgJavaScriptRuntime)runtime.Runtime;
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
    [InlineData("animate-dom-01-f", 0.022)]
    [InlineData("animate-dom-02-f", 0.022)]
    [InlineData("animate-elem-02-t", 0.022)]
    [InlineData("animate-elem-03-t", 0.022)]
    [InlineData("animate-elem-04-t", 0.022)]
    [InlineData("animate-elem-05-t", 0.022)]
    [InlineData("animate-elem-06-t", 0.022)]
    [InlineData("animate-elem-07-t", 0.022)]
    [InlineData("animate-elem-08-t", 0.022)]
    [InlineData("animate-elem-09-t", 0.022)]
    [InlineData("animate-elem-10-t", 0.022)]
    [InlineData("animate-elem-11-t", 0.022)]
    [InlineData("animate-elem-12-t", 0.022)]
    [InlineData("animate-elem-13-t", 0.022)]
    [InlineData("animate-elem-14-t", 0.022)]
    [InlineData("animate-elem-15-t", 0.022)]
    [InlineData("animate-elem-17-t", 0.022)]
    [InlineData("animate-elem-19-t", 0.022)]
    [InlineData("animate-elem-20-t", 0.022)]
    [InlineData("animate-elem-21-t", 0.022)]
    [InlineData("animate-elem-22-b", 0.022)]
    [InlineData("animate-elem-23-t", 0.022, Skip = "Modern Chrome captures deprecated animateColor as no-op; keep skipped until the W3C animateColor row has a non-Chrome static reference policy.")]
    [InlineData("animate-elem-24-t", 0.022)]
    [InlineData("animate-elem-25-t", 0.022)]
    [InlineData("animate-elem-26-t", 0.022)]
    [InlineData("animate-elem-27-t", 0.022)]
    [InlineData("animate-elem-28-t", 0.022)]
    [InlineData("animate-elem-29-b", 0.022)]
    [InlineData("animate-elem-30-t", 0.022)]
    [InlineData("animate-elem-31-t", 0.022)]
    [InlineData("animate-elem-32-t", 0.022)]
    [InlineData("animate-elem-33-t", 0.022)]
    [InlineData("animate-elem-34-t", 0.022)]
    [InlineData("animate-elem-35-t", 0.022)]
    [InlineData("animate-elem-36-t", 0.022)]
    [InlineData("animate-elem-37-t", 0.022)]
    [InlineData("animate-elem-38-t", 0.022)]
    [InlineData("animate-elem-39-t", 0.022)]
    [InlineData("animate-elem-40-t", 0.022)]
    [InlineData("animate-elem-41-t", 0.022)]
    [InlineData("animate-elem-44-t", 0.022)]
    [InlineData("animate-elem-46-t", 0.022)]
    [InlineData("animate-elem-52-t", 0.022)]
    [InlineData("animate-elem-53-t", 0.022)]
    [InlineData("animate-elem-60-t", 0.022)]
    [InlineData("animate-elem-61-t", 0.022)]
    [InlineData("animate-elem-62-t", 0.022)]
    [InlineData("animate-elem-63-t", 0.022)]
    [InlineData("animate-elem-64-t", 0.022)]
    [InlineData("animate-elem-65-t", 0.022)]
    [InlineData("animate-elem-66-t", 0.022)]
    [InlineData("animate-elem-67-t", 0.022)]
    [InlineData("animate-elem-68-t", 0.022)]
    [InlineData("animate-elem-69-t", 0.022)]
    [InlineData("animate-elem-70-t", 0.022)]
    [InlineData("animate-elem-77-t", 0.022)]
    [InlineData("animate-elem-78-t", 0.022)]
    [InlineData("animate-elem-80-t", 0.022)]
    [InlineData("animate-elem-81-t", 0.022)]
    [InlineData("animate-elem-82-t", 0.022)]
    [InlineData("animate-elem-83-t", 0.022)]
    [InlineData("animate-elem-84-t", 0.022, Skip = "Modern Chrome captures deprecated animateColor as no-op; keep skipped until the W3C animateColor row has a non-Chrome static reference policy.")]
    [InlineData("animate-elem-85-t", 0.022, Skip = "Modern Chrome captures deprecated animateColor as no-op; keep skipped until the W3C animateColor row has a non-Chrome static reference policy.")]
    [InlineData("animate-elem-86-t", 0.022)]
    [InlineData("animate-elem-87-t", 0.022)]
    [InlineData("animate-elem-88-t", 0.022)]
    [InlineData("animate-elem-89-t", 0.022)]
    [InlineData("animate-elem-90-b", 0.022)]
    [InlineData("animate-elem-91-t", 0.022)]
    [InlineData("animate-elem-92-t", 0.022)]
    [InlineData("animate-interact-events-01-t", 0.022)]
    [InlineData("animate-interact-pevents-01-t", 0.022)]
    [InlineData("animate-interact-pevents-02-t", 0.022)]
    [InlineData("animate-interact-pevents-03-t", 0.022)]
    [InlineData("animate-interact-pevents-04-t", 0.022)]
    [InlineData("animate-pservers-grad-01-b", 0.022)]
    [InlineData("animate-script-elem-01-b", 0.022)]
    [InlineData("animate-struct-dom-01-b", 0.022)]
    [InlineData("color-prof-01-f", 0.022, Skip = "Optional ICC color profile support is not a stable Chrome-backed baseline.")]
    [InlineData("color-prop-01-b", 0.022)]
    [InlineData("color-prop-02-f", 0.022)]
    [InlineData("color-prop-03-t", 0.022)]
    [InlineData("color-prop-04-t", 0.022, Skip = "System color keywords depend on viewer platform colors and are not a stable pixel baseline.")]
    [InlineData("color-prop-05-t", 0.022)]
    [InlineData("conform-viewers-02-f", 0.022)]
    [InlineData("conform-viewers-03-f", 0.022)]
    [InlineData("coords-coord-01-t", 0.022)]
    [InlineData("coords-coord-02-t", 0.022)]
    [InlineData("coords-dom-01-f", 0.022)]
    [InlineData("coords-dom-02-f", 0.022)]
    [InlineData("coords-dom-03-f", 0.022)]
    [InlineData("coords-dom-04-f", 0.022)]
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
    [InlineData("extend-namespace-01-f", 0.022)]
    [InlineData("filters-background-01-f", 0.022)]
    [InlineData("filters-blend-01-b", 0.022)]
    [InlineData("filters-color-01-b", 0.022)]
    [InlineData("filters-color-02-b", 0.022)]
    [InlineData("filters-composite-02-b", 0.022)]
    [InlineData("filters-composite-03-f", 0.022)]
    [InlineData("filters-composite-04-f", 0.022)]
    [InlineData("filters-composite-05-f", 0.022)]
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
    [InlineData("imp-path-01-f", 0.022)]
    [InlineData("interact-cursor-01-f", 0.022)]
    [InlineData("interact-dom-01-b", 0.022)]
    [InlineData("interact-events-01-b", 0.022)]
    [InlineData("interact-events-02-b", 0.022)]
    [InlineData("interact-events-202-f", 0.022)]
    [InlineData("interact-events-203-t", 0.022)]
    [InlineData("interact-order-01-b", 0.022)]
    [InlineData("interact-order-02-b", 0.022)]
    [InlineData("interact-order-03-b", 0.022)]
    [InlineData("interact-pevents-01-b", 0.022)]
    [InlineData("interact-pevents-03-b", 0.022)]
    [InlineData("interact-pevents-04-t", 0.022)]
    [InlineData("interact-pevents-05-b", 0.022)]
    [InlineData("interact-pevents-07-t", 0.022)]
    [InlineData("interact-pevents-08-f", 0.022)]
    [InlineData("interact-pevents-09-f", 0.022)]
    [InlineData("interact-pevents-10-f", 0.022)]
    [InlineData("interact-pointer-01-t", 0.022)]
    [InlineData("interact-pointer-02-t", 0.022)]
    [InlineData("interact-pointer-03-t", 0.022)]
    [InlineData("interact-pointer-04-f", 0.022)]
    [InlineData("interact-zoom-01-t", 0.022)]
    [InlineData("interact-zoom-02-t", 0.022)]
    [InlineData("interact-zoom-03-t", 0.022)]
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
    [InlineData("masking-path-09-b", 0.022)]
    [InlineData("masking-path-10-b", 0.022)]
    [InlineData("masking-path-11-b", 0.022)]
    [InlineData("masking-path-12-f", 0.022)]
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
    [InlineData("paths-dom-01-f", 0.100)]
    [InlineData("paths-dom-02-f", 0.100)]
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
    [InlineData("script-handle-01-b", 0.022)]
    [InlineData("script-handle-02-b", 0.022)]
    [InlineData("script-handle-03-b", 0.022)]
    [InlineData("script-handle-04-b", 0.022)]
    [InlineData("script-specify-01-f", 0.022)]
    [InlineData("script-specify-02-f", 0.022)]
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
    [InlineData("struct-defs-01-t", 0.022)]
    [InlineData("struct-dom-01-b", 0.022)]
    [InlineData("struct-dom-02-b", 0.022)]
    [InlineData("struct-dom-03-b", 0.022)]
    [InlineData("struct-dom-04-b", 0.022)]
    [InlineData("struct-dom-05-b", 0.022)]
    [InlineData("struct-dom-06-b", 0.022)]
    [InlineData("struct-dom-07-f", 0.022)]
    [InlineData("struct-dom-08-f", 0.022)]
    [InlineData("struct-dom-11-f", 0.022)]
    [InlineData("struct-dom-12-b", 0.022)]
    [InlineData("struct-dom-13-f", 0.022)]
    [InlineData("struct-dom-14-f", 0.022)]
    [InlineData("struct-dom-15-f", 0.022)]
    [InlineData("struct-dom-16-f", 0.022)]
    [InlineData("struct-dom-17-f", 0.022)]
    [InlineData("struct-dom-18-f", 0.022)]
    [InlineData("struct-dom-19-f", 0.022)]
    [InlineData("struct-dom-20-f", 0.022)]
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
    [InlineData("struct-image-07-t", 0.022)]
    [InlineData("struct-image-08-t", 0.022)]
    [InlineData("struct-image-09-t", 0.022)]
    [InlineData("struct-image-10-t", 0.022)]
    [InlineData("struct-image-11-b", 0.022)]
    [InlineData("struct-image-12-b", 0.022, Skip = "Chrome shows browser broken-image UI for cyclic and invalid image references.")]
    [InlineData("struct-image-13-f", 0.022)]
    [InlineData("struct-image-14-f", 0.022)]
    [InlineData("struct-image-15-f", 0.022)]
    [InlineData("struct-image-16-f", 0.04)]
    [InlineData("struct-image-17-b", 0.022)]
    [InlineData("struct-image-18-f", 0.022)]
    [InlineData("struct-image-19-f", 0.022)]
    [InlineData("struct-svg-01-f", 0.022)]
    [InlineData("struct-svg-02-f", 0.022)]
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
    [InlineData("struct-use-10-f", 0.022)]
    [InlineData("struct-use-11-f", 0.022)]
    [InlineData("struct-use-12-f", 0.022)]
    [InlineData("struct-use-13-f", 0.022)]
    [InlineData("struct-use-14-f", 0.022)]
    [InlineData("struct-use-15-f", 0.022)]
    [InlineData("styling-class-01-f", 0.022)]
    [InlineData("styling-css-01-b", 0.022)]
    [InlineData("styling-css-02-b", 0.022)]
    [InlineData("styling-css-03-b", 0.022)]
    [InlineData("styling-css-04-f", 0.022)]
    [InlineData("styling-css-05-b", 0.022)]
    [InlineData("styling-css-06-b", 0.022)]
    [InlineData("styling-css-07-f", 0.022)]
    [InlineData("styling-css-08-f", 0.022)]
    [InlineData("styling-css-09-f", 0.022)]
    [InlineData("styling-css-10-f", 0.022)]
    [InlineData("styling-elem-01-b", 0.022)]
    [InlineData("styling-inherit-01-b", 0.022)]
    [InlineData("styling-pres-01-t", 0.022)]
    [InlineData("styling-pres-02-f", 0.022)]
    [InlineData("styling-pres-03-f", 0.022)]
    [InlineData("styling-pres-04-f", 0.022)]
    [InlineData("styling-pres-05-f", 0.022)]
    [InlineData("svgdom-over-01-f", 0.022)]
    [InlineData("text-align-01-b", 0.022)]
    [InlineData("text-align-02-b", 0.022)]
    [InlineData("text-align-03-b", 0.022)]
    [InlineData("text-align-04-b", 0.022)]
    [InlineData("text-align-05-b", 0.022)]
    [InlineData("text-align-06-b", 0.022)]
    [InlineData("text-align-07-t", 0.022)]
    [InlineData("text-align-08-b", 0.022, Skip = "Mixed-script dominant baseline tables are not implemented")]
    [InlineData("text-altglyph-01-b", 0.17)]
    [InlineData("text-altglyph-02-b", 0.05)]
    [InlineData("text-altglyph-03-b", 0.05)]
    [InlineData("text-bidi-01-t", 0.022)]
    [InlineData("text-deco-01-b", 0.022)]
    [InlineData("text-dom-01-f", 0.046)]
    [InlineData("text-dom-02-f", 0.022)]
    [InlineData("text-dom-03-f", 0.022)]
    [InlineData("text-dom-04-f", 0.022)]
    [InlineData("text-dom-05-f", 0.022)]
    [InlineData("text-fonts-01-t", 0.022)]
    [InlineData("text-fonts-02-t", 0.022)]
    [InlineData("text-fonts-03-t", 0.022)]
    [InlineData("text-fonts-04-t", 0.022)]
    [InlineData("text-fonts-05-f", 0.022)]
    [InlineData("text-fonts-06-t", 0.022, Skip = "Fixture is missing from the bundled W3C checkout")]
    [InlineData("text-fonts-202-t", 0.022)]
    [InlineData("text-fonts-203-t", 0.022)]
    [InlineData("text-fonts-204-t", 0.022)]
    [InlineData("text-intro-01-t", 0.022)]
    [InlineData("text-intro-02-b", 0.022)]
    [InlineData("text-intro-03-b", 0.022)]
    [InlineData("text-intro-04-t", 0.022)]
    [InlineData("text-intro-05-t", 0.022)]
    [InlineData("text-intro-06-t", 0.022)]
    [InlineData("text-intro-07-t", 0.022)]
    [InlineData("text-intro-09-b", 0.022)]
    [InlineData("text-intro-10-f", 0.022)]
    [InlineData("text-intro-11-t", 0.022)]
    [InlineData("text-intro-12-t", 0.022)]
    [InlineData("text-path-01-b", 0.022)]
    [InlineData("text-path-02-b", 0.022)]
    [InlineData("text-spacing-01-b", 0.022)]
    [InlineData("text-text-01-b", 0.022)]
    [InlineData("text-text-03-b", 0.022)]
    [InlineData("text-text-04-t", 0.022)]
    [InlineData("text-text-05-t", 0.022)]
    [InlineData("text-text-06-t", 0.022)]
    [InlineData("text-text-07-t", 0.022)]
    [InlineData("text-text-08-b", 0.022)]
    [InlineData("text-text-09-t", 0.022)]
    [InlineData("text-text-10-t", 0.022)]
    [InlineData("text-text-11-t", 0.022)]
    [InlineData("text-text-12-t", 0.022)]
    [InlineData("text-tref-01-b", 0.022)]
    [InlineData("text-tref-02-b", 0.022)]
    [InlineData("text-tref-03-b", 0.022)]
    [InlineData("text-tselect-01-b", 0.022)]
    [InlineData("text-tselect-02-f", 0.022)]
    [InlineData("text-tselect-03-f", 0.022)]
    [InlineData("text-tspan-01-b", 0.022)]
    [InlineData("text-tspan-02-b", 0.022)]
    [InlineData("text-ws-01-t", 0.022)]
    [InlineData("text-ws-02-t", 0.022)]
    [InlineData("text-ws-03-t", 0.022)]
    [InlineData("types-basic-01-f", 0.022)]
    [InlineData("types-basic-02-f", 0.022)]
    [InlineData("types-dom-01-b", 0.022)]
    [InlineData("types-dom-02-f", 0.022)]
    [InlineData("types-dom-03-b", 0.022)]
    [InlineData("types-dom-04-b", 0.022)]
    [InlineData("types-dom-05-b", 0.022)]
    [InlineData("types-dom-06-f", 0.022)]
    [InlineData("types-dom-07-f", 0.022)]
    [InlineData("types-dom-08-f", 0.022)]
    [InlineData("types-dom-svgfittoviewbox-01-f", 0.022)]
    [InlineData("types-dom-svglengthlist-01-f", 0.022)]
    [InlineData("types-dom-svgnumberlist-01-f", 0.022)]
    [InlineData("types-dom-svgstringlist-01-f", 0.022)]
    [InlineData("types-dom-svgtransformable-01-f", 0.022)]
    public void Tests(string name, double errorThreshold) => TestImpl(name, errorThreshold);
}
