using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SkiaSharp;
using Svg;
using Svg.Skia.UnitTests.Common;
using Xunit;

namespace Svg.Skia.UnitTests;

public class resvgTests : SvgUnitTest
{
    private const double DefaultThreshold = 0.12;
    private const int ExpectedTotalFixtureCount = 1730;
    private const int ExpectedTextFixtureCount = 379;
    private const int ExpectedNonTextFixtureCount = 1351;
    private const int ExpectedResourceRenderingFixtureCount = 447;
    private const int ExpectedCssStylingFixtureCount = 19;
    private const int ExpectedEnabledNonTextFixtureCount = 466;
    private const int ExpectedRemainingNonTextFixtureCount = 885;
    private const string RemainingExtraFixtureSkipReason =
        "Remaining resvg extra fixtures are explicit inventory rows (15); enable individual rows when backed by a tracked renderer bug or parity lane.";
    private const string RemainingFilterFixtureSkipReason =
        "Remaining resvg filter fixtures are explicit inventory rows (281); filter primitive parity is tracked by feature-specific renderer lanes.";
    private const string RemainingMaskingFixtureSkipReason =
        "Remaining resvg masking fixtures are explicit inventory rows (92); clip/mask parity is tracked by feature-specific renderer lanes.";
    private const string RemainingPaintServerFixtureSkipReason =
        "Remaining resvg paint-server fixtures are explicit inventory rows (148); gradient/pattern parity is tracked by feature-specific renderer lanes.";
    private const string RemainingPaintingFixtureSkipReason =
        "Remaining resvg painting fixtures are explicit inventory rows (115); paint operation parity is tracked by feature-specific renderer lanes.";
    private const string RemainingShapeFixtureSkipReason =
        "Remaining resvg shape fixtures are explicit inventory rows (69); shape/path geometry parity is tracked by feature-specific renderer lanes.";
    private const string RemainingStructureFixtureSkipReason =
        "Remaining resvg structure fixtures are explicit inventory rows (165); structure/use/image parity is tracked by feature-specific renderer lanes.";

    public static IEnumerable<object[]> TextFixtureRows()
        => EnumerateFixtureRows("tests/text/");

    public static IEnumerable<object[]> ResourceRenderingFixtureRows()
        => EnumerateFixtureRows()
            .Where(static row => IsResourceRenderingFixture((string)row[0]));

    public static IEnumerable<object[]> CssStylingFixtureRows()
        => EnumerateFixtureRows()
            .Where(static row => IsCssStylingFixture((string)row[0]));

    public static IEnumerable<object[]> RemainingExtraFixtureRows()
        => EnumerateRemainingNonTextRows(ResvgFixtureArea.Extra, RemainingExtraFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFixtureRows()
        => EnumerateRemainingNonTextRows(ResvgFixtureArea.Filters, RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingMaskingFixtureRows()
        => EnumerateRemainingNonTextRows(ResvgFixtureArea.Masking, RemainingMaskingFixtureSkipReason);

    public static IEnumerable<object[]> RemainingPaintServerFixtureRows()
        => EnumerateRemainingNonTextRows(ResvgFixtureArea.PaintServers, RemainingPaintServerFixtureSkipReason);

    public static IEnumerable<object[]> RemainingPaintingFixtureRows()
        => EnumerateRemainingNonTextRows(ResvgFixtureArea.Painting, RemainingPaintingFixtureSkipReason);

    public static IEnumerable<object[]> RemainingShapeFixtureRows()
        => EnumerateRemainingNonTextRows(ResvgFixtureArea.Shapes, RemainingShapeFixtureSkipReason);

    public static IEnumerable<object[]> RemainingStructureFixtureRows()
        => EnumerateRemainingNonTextRows(ResvgFixtureArea.Structure, RemainingStructureFixtureSkipReason);

    [OSXTheory]
    [MemberData(nameof(TextFixtureRows))]
    public void text_fixtures(string relativeName, double errorThreshold)
        => TestImpl(relativeName, errorThreshold);

    [OSXTheory]
    [MemberData(nameof(ResourceRenderingFixtureRows))]
    public void resource_rendering_fixtures(string relativeName, double errorThreshold)
        => TestImpl(relativeName, errorThreshold);

    [OSXTheory]
    [MemberData(nameof(CssStylingFixtureRows))]
    public void css_styling_fixtures(string relativeName, double errorThreshold)
        => TestImpl(relativeName, errorThreshold);

    [OSXTheory(Skip = RemainingExtraFixtureSkipReason)]
    [MemberData(nameof(RemainingExtraFixtureRows))]
    public void remaining_extra_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFixtureRows))]
    public void remaining_filter_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingMaskingFixtureSkipReason)]
    [MemberData(nameof(RemainingMaskingFixtureRows))]
    public void remaining_masking_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingPaintServerFixtureSkipReason)]
    [MemberData(nameof(RemainingPaintServerFixtureRows))]
    public void remaining_paint_server_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingPaintingFixtureSkipReason)]
    [MemberData(nameof(RemainingPaintingFixtureRows))]
    public void remaining_painting_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingShapeFixtureSkipReason)]
    [MemberData(nameof(RemainingShapeFixtureRows))]
    public void remaining_shape_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingStructureFixtureSkipReason)]
    [MemberData(nameof(RemainingStructureFixtureRows))]
    public void remaining_structure_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [Fact]
    public void resvg_fixture_inventory()
    {
        var fixtures = EnumerateFixtureNames().ToArray();

        Assert.NotEmpty(fixtures);
        Assert.Equal(
            fixtures,
            fixtures.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray());

        foreach (var fixture in fixtures)
        {
            Assert.True(File.Exists(GetSvgPath(fixture)), $"Missing SVG fixture: {fixture}");
            Assert.True(File.Exists(GetExpectedPngPath(fixture)), $"Missing PNG fixture: {fixture}");
        }
    }

    [Fact]
    public void resvg_remaining_non_text_fixture_inventory()
    {
        var fixtures = EnumerateFixtureNames().ToArray();
        var textFixtures = fixtures
            .Where(static fixture => fixture.StartsWith("tests/text/", StringComparison.Ordinal))
            .ToArray();
        var nonTextFixtures = fixtures
            .Where(static fixture => !fixture.StartsWith("tests/text/", StringComparison.Ordinal))
            .ToArray();
        var resourceRenderingFixtures = fixtures
            .Where(static fixture => IsResourceRenderingFixture(fixture))
            .ToArray();
        var cssStylingFixtures = fixtures
            .Where(static fixture => IsCssStylingFixture(fixture))
            .ToArray();
        var enabledNonTextFixtures = resourceRenderingFixtures
            .Concat(cssStylingFixtures)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static fixture => fixture, StringComparer.Ordinal)
            .ToArray();
        var remainingNonTextFixtures = EnumerateRemainingNonTextFixtureNames().ToArray();
        var accountedFixtures = textFixtures
            .Concat(enabledNonTextFixtures)
            .Concat(remainingNonTextFixtures)
            .OrderBy(static fixture => fixture, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedTotalFixtureCount, fixtures.Length);
        Assert.Equal(ExpectedTextFixtureCount, textFixtures.Length);
        Assert.Equal(ExpectedNonTextFixtureCount, nonTextFixtures.Length);
        Assert.Equal(ExpectedResourceRenderingFixtureCount, resourceRenderingFixtures.Length);
        Assert.Equal(ExpectedCssStylingFixtureCount, cssStylingFixtures.Length);
        Assert.Equal(ExpectedEnabledNonTextFixtureCount, enabledNonTextFixtures.Length);
        Assert.Equal(ExpectedRemainingNonTextFixtureCount, remainingNonTextFixtures.Length);
        Assert.Equal(fixtures, accountedFixtures);

        foreach (var (area, expectedCount) in ExpectedRemainingFixtureAreaCounts)
        {
            var actualCount = remainingNonTextFixtures.Count(fixture => GetNonTextFixtureArea(fixture) == area);
            Assert.Equal(expectedCount, actualCount);
        }
    }

    [Fact]
    public void resvg_remaining_non_text_theories_are_explicit_feature_area_inventory()
    {
        var remainingTheoryNames = typeof(resvgTests)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(static method => method.GetCustomAttributes(typeof(OSXTheory), inherit: false).Length > 0)
            .Where(static method => method.Name.StartsWith("remaining_", StringComparison.Ordinal))
            .Select(static method => method.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "remaining_extra_fixtures",
                "remaining_filter_fixtures",
                "remaining_masking_fixtures",
                "remaining_paint_server_fixtures",
                "remaining_painting_fixtures",
                "remaining_shape_fixtures",
                "remaining_structure_fixtures"
            },
            remainingTheoryNames);
        Assert.DoesNotContain(
            typeof(resvgTests).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            static method => string.Equals(method.Name, "non_text_fixtures", StringComparison.Ordinal));

        var skipReasons = new[]
        {
            RemainingExtraFixtureSkipReason,
            RemainingFilterFixtureSkipReason,
            RemainingMaskingFixtureSkipReason,
            RemainingPaintServerFixtureSkipReason,
            RemainingPaintingFixtureSkipReason,
            RemainingShapeFixtureSkipReason,
            RemainingStructureFixtureSkipReason
        };

        Assert.All(skipReasons, static reason =>
        {
            Assert.Contains("explicit inventory rows", reason, StringComparison.Ordinal);
            Assert.DoesNotContain("hardening", reason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("browser-parity", reason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("umbrella", reason, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static IEnumerable<object[]> EnumerateFixtureRows(string? includePrefix = null, string? excludePrefix = null)
    {
        foreach (var fixture in EnumerateFixtureNames())
        {
            if (includePrefix is { } && !fixture.StartsWith(includePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (excludePrefix is { } && fixture.StartsWith(excludePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            yield return new object[] { fixture, GetEffectiveThreshold(fixture, DefaultThreshold) };
        }
    }

    private static IEnumerable<object[]> EnumerateRemainingNonTextRows(ResvgFixtureArea area, string skipReason)
    {
        foreach (var fixture in EnumerateRemainingNonTextFixtureNames())
        {
            if (GetNonTextFixtureArea(fixture) != area)
            {
                continue;
            }

            yield return new object[] { fixture, GetEffectiveThreshold(fixture, DefaultThreshold), skipReason };
        }
    }

    private static IEnumerable<string> EnumerateFixtureNames()
    {
        return EnumerateFixtureNames(GetResvgTestsRoot(), "tests")
            .Concat(EnumerateFixtureNames(GetResvgTestsRoot(), "extra"))
            .OrderBy(x => x, StringComparer.Ordinal);
    }

    private static IEnumerable<string> EnumerateRemainingNonTextFixtureNames()
    {
        foreach (var fixture in EnumerateFixtureNames())
        {
            if (fixture.StartsWith("tests/text/", StringComparison.Ordinal) ||
                IsResourceRenderingFixture(fixture) ||
                IsCssStylingFixture(fixture))
            {
                continue;
            }

            yield return fixture;
        }
    }

    private static ResvgFixtureArea GetNonTextFixtureArea(string relativeName)
    {
        return relativeName switch
        {
            var fixture when fixture.StartsWith("extra/", StringComparison.Ordinal) => ResvgFixtureArea.Extra,
            var fixture when fixture.StartsWith("tests/filters/", StringComparison.Ordinal) => ResvgFixtureArea.Filters,
            var fixture when fixture.StartsWith("tests/masking/", StringComparison.Ordinal) => ResvgFixtureArea.Masking,
            var fixture when fixture.StartsWith("tests/paint-servers/", StringComparison.Ordinal) => ResvgFixtureArea.PaintServers,
            var fixture when fixture.StartsWith("tests/painting/", StringComparison.Ordinal) => ResvgFixtureArea.Painting,
            var fixture when fixture.StartsWith("tests/shapes/", StringComparison.Ordinal) => ResvgFixtureArea.Shapes,
            var fixture when fixture.StartsWith("tests/structure/", StringComparison.Ordinal) => ResvgFixtureArea.Structure,
            _ => throw new InvalidOperationException($"Unclassified resvg fixture: {relativeName}")
        };
    }

    private static IEnumerable<string> EnumerateFixtureNames(string root, string directoryName)
    {
        var directory = Path.Combine(root, directoryName);

        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var svgPath in Directory.EnumerateFiles(directory, "*.svg", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
        {
            var relativePath = Path.GetRelativePath(directory, svgPath);
            yield return $"{directoryName}/{Path.ChangeExtension(relativePath, null)}".Replace(Path.DirectorySeparatorChar, '/');
        }
    }

    private static string GetSvgPath(string relativeName)
        => Path.Combine(GetResvgTestsRoot(), ToLocalPath($"{relativeName}.svg"));

    private static string GetExpectedPngPath(string relativeName)
        => Path.Combine(GetResvgTestsRoot(), ToLocalPath($"{relativeName}.png"));

    private static string GetChromeOverridePngPath(string relativeName)
        => Path.Combine("..", "..", "..", "ChromeReference", "resvg", ToLocalPath($"{relativeName}.png"));

    private static string GetActualPngPath(string relativeName)
        => Path.Combine("..", "..", "..", "..", "Tests", $"resvg {GetSafeName(relativeName)} (Actual).png");

    private void TestImpl(string relativeName, double errorThreshold)
    {
        var svgPath = GetSvgPath(relativeName);
        var chromeOverridePng = GetChromeOverridePngPath(relativeName);
        var useChromeOverride = File.Exists(chromeOverridePng);
        var expectedPng = useChromeOverride ? chromeOverridePng : GetExpectedPngPath(relativeName);
        var actualPng = GetActualPngPath(relativeName);

        if (File.Exists(actualPng))
        {
            File.Delete(actualPng);
        }

        var svg = new SKSvg();
        svg.Settings.EnableTextReferences = !useChromeOverride;

        SetTypefaceProviders(svg.Settings);

        using var _ = svg.Load(svgPath);
        using var expectedImage = Image.Load<Rgba32>(expectedPng);
        var cullRect = svg.Picture?.CullRect ?? SKRect.Create(200f, 200f);
        var scaleX = cullRect.Width > 0f ? expectedImage.Width / cullRect.Width : 1.5f;
        var scaleY = cullRect.Height > 0f ? expectedImage.Height / cullRect.Height : 1.5f;
        Rgba32? compositeBackground = useChromeOverride
            ? new Rgba32(255, 255, 255, 255)
            : null;
        svg.Save(actualPng, compositeBackground.HasValue ? ToSkColor(compositeBackground.Value) : SKColors.Transparent, scaleX: scaleX, scaleY: scaleY);

        ImageHelper.CompareImages(
            relativeName,
            actualPng,
            expectedPng,
            errorThreshold,
            compositeBackground: compositeBackground);

        if (File.Exists(actualPng))
        {
            File.Delete(actualPng);
        }
    }

    private void TestSkippedFixtureImpl(string relativeName, double errorThreshold, string skipReason)
    {
        Assert.NotEmpty(skipReason);
        TestImpl(relativeName, errorThreshold);
    }

    private static SKColor ToSkColor(Rgba32 color)
        => new(color.R, color.G, color.B, color.A);

    private static double GetEffectiveThreshold(string relativeName, double defaultThreshold)
    {
        return relativeName switch
        {
            "tests/text/baseline-shift/inheritance-1" => 0.134,
            "tests/text/baseline-shift/inheritance-3" => 0.137,
            "tests/text/baseline-shift/nested-with-baseline-2" => 0.162,
            "tests/text/color-font/cbdt" => 0.130,
            "tests/text/lengthAdjust/vertical" => 0.124,
            "tests/text/letter-spacing/filter-bbox" => 0.161,
            "tests/text/textLength/on-text-and-tspan" => 0.124,
            "tests/text/tref/position-attributes" => 0.128,
            "tests/text/writing-mode/tb-with-dx-on-second-tspan" => 0.148,
            _ => defaultThreshold
        };
    }

    private static bool IsResourceRenderingFixture(string relativeName)
        => !relativeName.StartsWith("tests/text/", StringComparison.Ordinal) &&
           (ResourceRenderingFixturePrefixes.Any(prefix => relativeName.StartsWith(prefix, StringComparison.Ordinal)) ||
            ResourceRenderingFixtureNames.Contains(relativeName, StringComparer.Ordinal));

    private static bool IsCssStylingFixture(string relativeName)
        => CssStylingFixtureNames.Contains(relativeName, StringComparer.Ordinal);

    private static readonly (ResvgFixtureArea Area, int Count)[] ExpectedRemainingFixtureAreaCounts =
    {
        (ResvgFixtureArea.Extra, 15),
        (ResvgFixtureArea.Filters, 281),
        (ResvgFixtureArea.Masking, 92),
        (ResvgFixtureArea.PaintServers, 148),
        (ResvgFixtureArea.Painting, 115),
        (ResvgFixtureArea.Shapes, 69),
        (ResvgFixtureArea.Structure, 165)
    };

    private static readonly string[] ResourceRenderingFixturePrefixes =
    {
        "tests/filters/feComponentTransfer/",
        "tests/filters/feDisplacementMap/",
        "tests/filters/feDistantLight/",
        "tests/filters/filter-functions/",
        "tests/filters/feTurbulence/",
        "tests/masking/clip-rule/",
        "tests/paint-servers/stop-color/",
        "tests/painting/color/",
        "tests/painting/fill-rule/",
        "tests/painting/image-rendering/",
        "tests/painting/isolation/",
        "tests/painting/mix-blend-mode/",
        "tests/painting/paint-order/",
        "tests/painting/shape-rendering/",
        "tests/painting/stroke/",
        "tests/painting/stroke-dasharray/",
        "tests/painting/stroke-dashoffset/",
        "tests/painting/stroke-linecap/",
        "tests/painting/stroke-linejoin/",
        "tests/painting/stroke-miterlimit/",
        "tests/painting/stroke-width/",
        "tests/painting/visibility/",
        "tests/shapes/circle/",
        "tests/shapes/line/",
        "tests/shapes/polygon/",
        "tests/shapes/polyline/",
        "tests/shapes/rect/",
        "tests/structure/a/",
        "tests/structure/defs/",
        "tests/structure/g/",
        "tests/structure/transform/",
        "tests/structure/use/"
    };

    private static readonly string[] ResourceRenderingFixtureNames =
    {
        "tests/filters/feImage/chained-feImage",
        "tests/filters/feImage/embedded-png",
        "tests/filters/feImage/empty",
        "tests/filters/feImage/link-on-an-element-with-complex-transform",
        "tests/filters/feImage/link-on-an-element-with-transform",
        "tests/filters/feImage/link-to-an-element",
        "tests/filters/feImage/link-to-an-element-outside-defs-1",
        "tests/filters/feImage/link-to-an-element-outside-defs-2",
        "tests/filters/feImage/link-to-an-element-with-transform",
        "tests/filters/feImage/link-to-an-element-with-opacity",
        "tests/filters/feImage/link-to-an-invalid-element",
        "tests/filters/feImage/link-to-g",
        "tests/filters/feImage/link-to-use",
        "tests/filters/feImage/preserveAspectRatio=none",
        "tests/filters/feImage/recursive-links-1",
        "tests/filters/feImage/recursive-links-2",
        "tests/filters/feImage/self-recursive",
        "tests/filters/feImage/simple-case",
        "tests/filters/feImage/svg",
        "tests/filters/feImage/with-subregion-1",
        "tests/filters/feImage/with-subregion-2",
        "tests/filters/feImage/with-subregion-3",
        "tests/filters/feImage/with-subregion-4",
        "tests/filters/feImage/with-subregion-5",
        "tests/filters/feImage/with-x-y",
        "tests/filters/feImage/with-x-y-and-protruding-subregion-1",
        "tests/filters/feImage/with-x-y-and-protruding-subregion-2",
        "tests/painting/marker/default-clip",
        "tests/painting/marker/empty",
        "tests/painting/marker/inheritance-1",
        "tests/painting/marker/inheritance-2",
        "tests/painting/marker/invalid-child",
        "tests/painting/marker/marker-on-circle",
        "tests/painting/marker/marker-on-line",
        "tests/painting/marker/marker-on-polygon",
        "tests/painting/marker/marker-on-polyline",
        "tests/painting/marker/marker-on-rect",
        "tests/painting/marker/marker-on-rounded-rect",
        "tests/painting/marker/marker-on-text",
        "tests/painting/marker/marker-with-a-negative-size",
        "tests/painting/marker/nested",
        "tests/painting/marker/no-stroke-on-target",
        "tests/painting/marker/on-ArcTo",
        "tests/painting/marker/only-marker-end",
        "tests/painting/marker/only-marker-mid",
        "tests/painting/marker/only-marker-start",
        "tests/painting/marker/orient=-45",
        "tests/painting/marker/orient=0.25turn",
        "tests/painting/marker/orient=1.5rad",
        "tests/painting/marker/orient=30",
        "tests/painting/marker/orient=40grad",
        "tests/painting/marker/orient=9999",
        "tests/painting/marker/orient=auto-on-M-C-C-1",
        "tests/painting/marker/orient=auto-on-M-C-C-2",
        "tests/painting/marker/orient=auto-on-M-C-C-3",
        "tests/painting/marker/orient=auto-on-M-C-C-4",
        "tests/painting/marker/orient=auto-on-M-C-C-5",
        "tests/painting/marker/orient=auto-on-M-C-C-6",
        "tests/painting/marker/orient=auto-on-M-C-C-7",
        "tests/painting/marker/orient=auto-on-M-C-C-8",
        "tests/painting/marker/orient=auto-on-M-C-L",
        "tests/painting/marker/orient=auto-on-M-C-M-L",
        "tests/painting/marker/orient=auto-on-M-L-C",
        "tests/painting/marker/orient=auto-on-M-L-L-Z-Z-Z",
        "tests/painting/marker/orient=auto-on-M-L-L",
        "tests/painting/marker/orient=auto-on-M-L-M-C",
        "tests/painting/marker/orient=auto-on-M-L-Z",
        "tests/painting/marker/orient=auto-on-M-L",
        "tests/painting/marker/orient=auto-start-reverse",
        "tests/painting/marker/percent-values",
        "tests/painting/marker/recursive-1",
        "tests/painting/marker/recursive-2",
        "tests/painting/marker/recursive-3",
        "tests/painting/marker/recursive-4",
        "tests/painting/marker/recursive-5",
        "tests/painting/marker/target-with-subpaths-1",
        "tests/painting/marker/target-with-subpaths-2",
        "tests/painting/marker/the-marker-property-in-CSS",
        "tests/painting/marker/the-marker-property",
        "tests/painting/marker/with-a-large-stroke",
        "tests/painting/marker/with-a-text-child",
        "tests/painting/marker/with-an-image-child",
        "tests/painting/marker/with-invalid-markerUnits",
        "tests/painting/marker/with-markerUnits=userSpaceOnUse",
        "tests/painting/marker/with-viewBox-1",
        "tests/painting/marker/with-viewBox-2",
        "tests/painting/marker/zero-length-path-1",
        "tests/painting/marker/zero-length-path-2",
        "tests/painting/marker/zero-sized-stroke",
        "tests/painting/marker/zero-sized"
    };

    private static readonly string[] CssStylingFixtureNames =
    {
        "tests/structure/style-attribute/comments",
        "tests/structure/style-attribute/simple-case",
        "tests/structure/style-attribute/transform",
        "tests/structure/style/attribute-selector",
        "tests/structure/style/class-selector",
        "tests/structure/style/combined-selectors",
        "tests/structure/style/current-color-fill-before-color",
        "tests/structure/style/current-color-stroke-before-color",
        "tests/structure/style/iD-selector",
        "tests/structure/style/important",
        "tests/structure/style/invalid-type",
        "tests/structure/style/resolve-order",
        "tests/structure/style/rule-specificity",
        "tests/structure/style/style-after-usage",
        "tests/structure/style/style-inside-CDATA",
        "tests/structure/style/transform",
        "tests/structure/style/type-selector",
        "tests/structure/style/universal-selector",
        "tests/structure/style/unresolved-class-selector"
    };

    private static string GetResvgTestsRoot()
        => Path.GetFullPath(Path.Combine("..", "..", "..", "..", "..", "externals", "resvg", "crates", "resvg", "tests"));

    private static string ToLocalPath(string relativePath)
        => relativePath.Replace('/', Path.DirectorySeparatorChar);

    private static string GetSafeName(string relativeName)
    {
        var safeName = relativeName
            .Replace('/', '_')
            .Replace('\\', '_')
            .Replace('=', '-')
            .Replace('%', 'p');

        return string.Join("_", safeName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
    }

    private enum ResvgFixtureArea
    {
        Extra,
        Filters,
        Masking,
        PaintServers,
        Painting,
        Shapes,
        Structure
    }
}
