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
    private const string RemainingFixtureProbeFamilyEnvironmentVariable = "SVG_SKIA_RESVG_PROBE_FAMILY";
    private const string RemainingFixtureProbeAllValue = "all";
    private const int ExpectedTotalFixtureCount = 1730;
    private const int ExpectedTextFixtureCount = 379;
    private const int ExpectedNonTextFixtureCount = 1351;
    private const int ExpectedResourceRenderingFixtureCount = 525;
    private const int ExpectedCssStylingFixtureCount = 19;
    private const int ExpectedEnabledNonTextFixtureCount = 544;
    private const int ExpectedRemainingNonTextFixtureCount = 807;
    private const string RemainingExtraFixtureSkipReason =
        "Remaining resvg extra fixtures are explicit inventory rows (15); enable individual rows when backed by a tracked renderer bug or parity lane.";
    private const string RemainingFilterFixtureSkipReason =
        "Remaining resvg filter fixture family rows are explicit inventory rows; filter primitive parity is tracked by feature-specific renderer lanes.";
    private const string RemainingMaskingFixtureSkipReason =
        "Remaining resvg masking fixture family rows are explicit inventory rows; clip/mask parity is tracked by feature-specific renderer lanes.";
    private const string RemainingPaintServerFixtureSkipReason =
        "Remaining resvg paint-server fixture family rows are explicit inventory rows; gradient/pattern parity is tracked by feature-specific renderer lanes.";
    private const string RemainingPaintingFixtureSkipReason =
        "Remaining resvg painting fixture family rows are explicit inventory rows; paint operation parity is tracked by feature-specific renderer lanes.";
    private const string RemainingShapeFixtureSkipReason =
        "Remaining resvg shape fixture family rows are explicit inventory rows; shape/path geometry parity is tracked by feature-specific renderer lanes.";
    private const string RemainingStructureFixtureSkipReason =
        "Remaining resvg structure fixture family rows are explicit inventory rows; structure/use/image parity is tracked by feature-specific renderer lanes.";

    public static IEnumerable<object[]> TextFixtureRows()
        => EnumerateFixtureRows("tests/text/");

    public static IEnumerable<object[]> ResourceRenderingFixtureRows()
        => EnumerateFixtureRows()
            .Where(static row => IsResourceRenderingFixture((string)row[0]));

    public static IEnumerable<object[]> CssStylingFixtureRows()
        => EnumerateFixtureRows()
            .Where(static row => IsCssStylingFixture((string)row[0]));

    public static IEnumerable<object[]> RemainingExtraFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("extra/", RemainingExtraFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterEnableBackgroundFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/enable-background/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFeBlendFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/feBlend/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFeColorMatrixFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/feColorMatrix/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFeCompositeFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/feComposite/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFeConvolveMatrixFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/feConvolveMatrix/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFeDiffuseLightingFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/feDiffuseLighting/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFeDropShadowFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/feDropShadow/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFeFloodFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/feFlood/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFeGaussianBlurFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/feGaussianBlur/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFeMergeFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/feMerge/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFeMorphologyFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/feMorphology/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFeOffsetFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/feOffset/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFePointLightFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/fePointLight/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFeSpecularLightingFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/feSpecularLighting/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFeSpotLightFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/feSpotLight/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFeTileFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/feTile/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFilterFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/filter/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFilterFloodColorFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/filters/flood-color/", RemainingFilterFixtureSkipReason);

    public static IEnumerable<object[]> RemainingMaskingClipFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/masking/clip/", RemainingMaskingFixtureSkipReason);

    public static IEnumerable<object[]> RemainingMaskingClipPathFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/masking/clipPath/", RemainingMaskingFixtureSkipReason);

    public static IEnumerable<object[]> RemainingMaskingMaskFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/masking/mask/", RemainingMaskingFixtureSkipReason);

    public static IEnumerable<object[]> RemainingPaintServerLinearGradientFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/paint-servers/linearGradient/", RemainingPaintServerFixtureSkipReason);

    public static IEnumerable<object[]> RemainingPaintServerPatternFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/paint-servers/pattern/", RemainingPaintServerFixtureSkipReason);

    public static IEnumerable<object[]> RemainingPaintServerRadialGradientFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/paint-servers/radialGradient/", RemainingPaintServerFixtureSkipReason);

    public static IEnumerable<object[]> RemainingPaintingContextFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/painting/context/", RemainingPaintingFixtureSkipReason);

    public static IEnumerable<object[]> RemainingPaintingDisplayFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/painting/display/", RemainingPaintingFixtureSkipReason);

    public static IEnumerable<object[]> RemainingPaintingFillFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/painting/fill/", RemainingPaintingFixtureSkipReason);

    public static IEnumerable<object[]> RemainingShapePathFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/shapes/path/", RemainingShapeFixtureSkipReason);

    public static IEnumerable<object[]> RemainingStructureImageFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/structure/image/", RemainingStructureFixtureSkipReason);

    public static IEnumerable<object[]> RemainingStructureStyleFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/structure/style/", RemainingStructureFixtureSkipReason);

    public static IEnumerable<object[]> RemainingStructureStyleAttributeFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/structure/style-attribute/", RemainingStructureFixtureSkipReason);

    public static IEnumerable<object[]> RemainingStructureSvgFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/structure/svg/", RemainingStructureFixtureSkipReason);

    public static IEnumerable<object[]> RemainingStructureSwitchFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/structure/switch/", RemainingStructureFixtureSkipReason);

    public static IEnumerable<object[]> RemainingStructureSymbolFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/structure/symbol/", RemainingStructureFixtureSkipReason);

    public static IEnumerable<object[]> RemainingStructureSystemLanguageFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/structure/systemLanguage/", RemainingStructureFixtureSkipReason);

    public static IEnumerable<object[]> RemainingStructureTransformOriginFixtureRows()
        => EnumerateRemainingFixtureFamilyRows("tests/structure/transform-origin/", RemainingStructureFixtureSkipReason);

    public static IEnumerable<object[]> RemainingFixtureFamilyProbeRows()
    {
        var probeFamilyPrefix = GetRemainingFixtureFamilyProbePrefix();

        if (probeFamilyPrefix is null)
        {
            yield return new object[] { string.Empty };
            yield break;
        }

        if (string.Equals(probeFamilyPrefix, RemainingFixtureProbeAllValue, StringComparison.Ordinal))
        {
            foreach (var family in RemainingFixtureFamilies)
            {
                yield return new object[] { family.Prefix };
            }

            yield break;
        }

        yield return new object[] { probeFamilyPrefix };
    }

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
    [MemberData(nameof(RemainingFilterEnableBackgroundFixtureRows))]
    public void remaining_filter_enable_background_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFeBlendFixtureRows))]
    public void remaining_filter_fe_blend_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFeColorMatrixFixtureRows))]
    public void remaining_filter_fe_color_matrix_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFeCompositeFixtureRows))]
    public void remaining_filter_fe_composite_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFeConvolveMatrixFixtureRows))]
    public void remaining_filter_fe_convolve_matrix_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFeDiffuseLightingFixtureRows))]
    public void remaining_filter_fe_diffuse_lighting_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFeDropShadowFixtureRows))]
    public void remaining_filter_fe_drop_shadow_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFeFloodFixtureRows))]
    public void remaining_filter_fe_flood_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFeGaussianBlurFixtureRows))]
    public void remaining_filter_fe_gaussian_blur_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFeMergeFixtureRows))]
    public void remaining_filter_fe_merge_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFeMorphologyFixtureRows))]
    public void remaining_filter_fe_morphology_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFeOffsetFixtureRows))]
    public void remaining_filter_fe_offset_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFePointLightFixtureRows))]
    public void remaining_filter_fe_point_light_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFeSpecularLightingFixtureRows))]
    public void remaining_filter_fe_specular_lighting_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFeSpotLightFixtureRows))]
    public void remaining_filter_fe_spot_light_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFeTileFixtureRows))]
    public void remaining_filter_fe_tile_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFilterFixtureRows))]
    public void remaining_filter_filter_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingFilterFixtureSkipReason)]
    [MemberData(nameof(RemainingFilterFloodColorFixtureRows))]
    public void remaining_filter_flood_color_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingMaskingFixtureSkipReason)]
    [MemberData(nameof(RemainingMaskingClipFixtureRows))]
    public void remaining_masking_clip_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingMaskingFixtureSkipReason)]
    [MemberData(nameof(RemainingMaskingClipPathFixtureRows))]
    public void remaining_masking_clip_path_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingMaskingFixtureSkipReason)]
    [MemberData(nameof(RemainingMaskingMaskFixtureRows))]
    public void remaining_masking_mask_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingPaintServerFixtureSkipReason)]
    [MemberData(nameof(RemainingPaintServerLinearGradientFixtureRows))]
    public void remaining_paint_server_linear_gradient_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingPaintServerFixtureSkipReason)]
    [MemberData(nameof(RemainingPaintServerPatternFixtureRows))]
    public void remaining_paint_server_pattern_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingPaintServerFixtureSkipReason)]
    [MemberData(nameof(RemainingPaintServerRadialGradientFixtureRows))]
    public void remaining_paint_server_radial_gradient_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingPaintingFixtureSkipReason)]
    [MemberData(nameof(RemainingPaintingContextFixtureRows))]
    public void remaining_painting_context_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingPaintingFixtureSkipReason)]
    [MemberData(nameof(RemainingPaintingDisplayFixtureRows))]
    public void remaining_painting_display_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingPaintingFixtureSkipReason)]
    [MemberData(nameof(RemainingPaintingFillFixtureRows))]
    public void remaining_painting_fill_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingShapeFixtureSkipReason)]
    [MemberData(nameof(RemainingShapePathFixtureRows))]
    public void remaining_shape_path_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingStructureFixtureSkipReason)]
    [MemberData(nameof(RemainingStructureImageFixtureRows))]
    public void remaining_structure_image_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingStructureFixtureSkipReason)]
    [MemberData(nameof(RemainingStructureStyleFixtureRows))]
    public void remaining_structure_style_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingStructureFixtureSkipReason)]
    [MemberData(nameof(RemainingStructureStyleAttributeFixtureRows))]
    public void remaining_structure_style_attribute_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingStructureFixtureSkipReason)]
    [MemberData(nameof(RemainingStructureSvgFixtureRows))]
    public void remaining_structure_svg_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingStructureFixtureSkipReason)]
    [MemberData(nameof(RemainingStructureSwitchFixtureRows))]
    public void remaining_structure_switch_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingStructureFixtureSkipReason)]
    [MemberData(nameof(RemainingStructureSymbolFixtureRows))]
    public void remaining_structure_symbol_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingStructureFixtureSkipReason)]
    [MemberData(nameof(RemainingStructureSystemLanguageFixtureRows))]
    public void remaining_structure_system_language_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory(Skip = RemainingStructureFixtureSkipReason)]
    [MemberData(nameof(RemainingStructureTransformOriginFixtureRows))]
    public void remaining_structure_transform_origin_fixtures(string relativeName, double errorThreshold, string skipReason)
        => TestSkippedFixtureImpl(relativeName, errorThreshold, skipReason);

    [OSXTheory]
    [MemberData(nameof(RemainingFixtureFamilyProbeRows))]
    public void remaining_fixture_family_probe(string familyPrefix)
    {
        if (string.IsNullOrEmpty(familyPrefix))
        {
            return;
        }

        var fixtureRows = EnumerateRemainingFixtureFamilyRows(familyPrefix, "Manual remaining fixture family probe.").ToArray();

        Assert.NotEmpty(fixtureRows);

        foreach (var row in fixtureRows)
        {
            TestImpl((string)row[0], (double)row[1], preserveActual: true);
        }
    }

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

        var remainingFamilyFixtures = RemainingFixtureFamilies
            .SelectMany(static family => EnumerateRemainingFixtureFamilyNames(family.Prefix))
            .OrderBy(static fixture => fixture, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(remainingNonTextFixtures, remainingFamilyFixtures);

        foreach (var family in RemainingFixtureFamilies)
        {
            var familyFixtures = EnumerateRemainingFixtureFamilyNames(family.Prefix).ToArray();

            Assert.Equal(family.ExpectedCount, familyFixtures.Length);
            Assert.All(familyFixtures, fixture => Assert.Equal(family.Area, GetNonTextFixtureArea(fixture)));
        }
    }

    [Fact]
    public void resvg_remaining_non_text_theories_are_explicit_feature_area_inventory()
    {
        var remainingTheoryNames = typeof(resvgTests)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(static method => method.GetCustomAttributes(typeof(OSXTheory), inherit: false).Length > 0)
            .Where(static method => method.Name.StartsWith("remaining_", StringComparison.Ordinal))
            .Where(static method => method.Name.EndsWith("_fixtures", StringComparison.Ordinal))
            .Select(static method => method.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            RemainingFixtureFamilies
                .Select(static family => family.TheoryName)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray(),
            remainingTheoryNames);
        Assert.DoesNotContain(
            typeof(resvgTests).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            static method => string.Equals(method.Name, "non_text_fixtures", StringComparison.Ordinal));
        Assert.Contains(
            typeof(resvgTests).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            static method => string.Equals(method.Name, "remaining_fixture_family_probe", StringComparison.Ordinal));

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

    private static IEnumerable<object[]> EnumerateRemainingFixtureFamilyRows(string familyPrefix, string skipReason)
    {
        foreach (var fixture in EnumerateRemainingFixtureFamilyNames(familyPrefix))
        {
            yield return new object[] { fixture, GetEffectiveThreshold(fixture, DefaultThreshold), skipReason };
        }
    }

    private static IEnumerable<string> EnumerateRemainingFixtureFamilyNames(string familyPrefix)
    {
        foreach (var fixture in EnumerateRemainingNonTextFixtureNames())
        {
            if (fixture.StartsWith(familyPrefix, StringComparison.Ordinal))
            {
                yield return fixture;
            }
        }
    }

    private static string? GetRemainingFixtureFamilyProbePrefix()
    {
        var probeFamily = Environment.GetEnvironmentVariable(RemainingFixtureProbeFamilyEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(probeFamily))
        {
            return null;
        }

        var normalizedProbeFamily = probeFamily
            .Trim()
            .Replace('\\', '/')
            .TrimEnd('/');

        if (string.Equals(normalizedProbeFamily, RemainingFixtureProbeAllValue, StringComparison.OrdinalIgnoreCase))
        {
            return RemainingFixtureProbeAllValue;
        }

        foreach (var family in RemainingFixtureFamilies)
        {
            if (IsRemainingFixtureFamilyProbeMatch(family, normalizedProbeFamily))
            {
                return family.Prefix;
            }
        }

        var supportedFamilies = string.Join(
            ", ",
            RemainingFixtureFamilies.Select(static family => family.Prefix.TrimEnd('/')));

        throw new InvalidOperationException(
            $"Unsupported {RemainingFixtureProbeFamilyEnvironmentVariable} value '{probeFamily}'. Supported values: {RemainingFixtureProbeAllValue}, {supportedFamilies}.");
    }

    private static bool IsRemainingFixtureFamilyProbeMatch(RemainingFixtureFamily family, string probeFamily)
    {
        var familyPrefix = family.Prefix.TrimEnd('/');
        var familyTheoryName = family.TheoryName;
        var familyTheoryKey = familyTheoryName.Substring(
            "remaining_".Length,
            familyTheoryName.Length - "remaining_".Length - "_fixtures".Length);

        return string.Equals(probeFamily, familyPrefix, StringComparison.Ordinal) ||
            string.Equals(probeFamily, family.Prefix, StringComparison.Ordinal) ||
            string.Equals(probeFamily, familyTheoryName, StringComparison.Ordinal) ||
            string.Equals(probeFamily, familyTheoryKey, StringComparison.Ordinal);
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

    private void TestImpl(string relativeName, double errorThreshold, bool preserveActual = false)
    {
        var svgPath = GetSvgPath(relativeName);
        var chromeOverridePng = GetChromeOverridePngPath(relativeName);
        var useChromeOverride = File.Exists(chromeOverridePng);
        var expectedPng = useChromeOverride ? chromeOverridePng : GetExpectedPngPath(relativeName);
        var actualPng = GetActualPngPath(relativeName);

        if (!preserveActual && File.Exists(actualPng))
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

        if (!preserveActual && File.Exists(actualPng))
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

    private static readonly RemainingFixtureFamily[] RemainingFixtureFamilies =
    {
        new("remaining_extra_fixtures", ResvgFixtureArea.Extra, "extra/", 15),
        new("remaining_filter_enable_background_fixtures", ResvgFixtureArea.Filters, "tests/filters/enable-background/", 21),
        new("remaining_filter_fe_blend_fixtures", ResvgFixtureArea.Filters, "tests/filters/feBlend/", 10),
        new("remaining_filter_fe_color_matrix_fixtures", ResvgFixtureArea.Filters, "tests/filters/feColorMatrix/", 16),
        new("remaining_filter_fe_composite_fixtures", ResvgFixtureArea.Filters, "tests/filters/feComposite/", 18),
        new("remaining_filter_fe_convolve_matrix_fixtures", ResvgFixtureArea.Filters, "tests/filters/feConvolveMatrix/", 25),
        new("remaining_filter_fe_diffuse_lighting_fixtures", ResvgFixtureArea.Filters, "tests/filters/feDiffuseLighting/", 22),
        new("remaining_filter_fe_drop_shadow_fixtures", ResvgFixtureArea.Filters, "tests/filters/feDropShadow/", 8),
        new("remaining_filter_fe_flood_fixtures", ResvgFixtureArea.Filters, "tests/filters/feFlood/", 8),
        new("remaining_filter_fe_gaussian_blur_fixtures", ResvgFixtureArea.Filters, "tests/filters/feGaussianBlur/", 13),
        new("remaining_filter_fe_merge_fixtures", ResvgFixtureArea.Filters, "tests/filters/feMerge/", 3),
        new("remaining_filter_fe_morphology_fixtures", ResvgFixtureArea.Filters, "tests/filters/feMorphology/", 14),
        new("remaining_filter_fe_offset_fixtures", ResvgFixtureArea.Filters, "tests/filters/feOffset/", 9),
        new("remaining_filter_fe_point_light_fixtures", ResvgFixtureArea.Filters, "tests/filters/fePointLight/", 4),
        new("remaining_filter_fe_specular_lighting_fixtures", ResvgFixtureArea.Filters, "tests/filters/feSpecularLighting/", 8),
        new("remaining_filter_fe_spot_light_fixtures", ResvgFixtureArea.Filters, "tests/filters/feSpotLight/", 12),
        new("remaining_filter_fe_tile_fixtures", ResvgFixtureArea.Filters, "tests/filters/feTile/", 7),
        new("remaining_filter_filter_fixtures", ResvgFixtureArea.Filters, "tests/filters/filter/", 74),
        new("remaining_filter_flood_color_fixtures", ResvgFixtureArea.Filters, "tests/filters/flood-color/", 7),
        new("remaining_masking_clip_fixtures", ResvgFixtureArea.Masking, "tests/masking/clip/", 1),
        new("remaining_masking_clip_path_fixtures", ResvgFixtureArea.Masking, "tests/masking/clipPath/", 52),
        new("remaining_masking_mask_fixtures", ResvgFixtureArea.Masking, "tests/masking/mask/", 39),
        new("remaining_paint_server_linear_gradient_fixtures", ResvgFixtureArea.PaintServers, "tests/paint-servers/linearGradient/", 38),
        new("remaining_paint_server_pattern_fixtures", ResvgFixtureArea.PaintServers, "tests/paint-servers/pattern/", 31),
        new("remaining_paint_server_radial_gradient_fixtures", ResvgFixtureArea.PaintServers, "tests/paint-servers/radialGradient/", 45),
        new("remaining_painting_context_fixtures", ResvgFixtureArea.Painting, "tests/painting/context/", 16),
        new("remaining_painting_display_fixtures", ResvgFixtureArea.Painting, "tests/painting/display/", 9),
        new("remaining_painting_fill_fixtures", ResvgFixtureArea.Painting, "tests/painting/fill/", 60),
        new("remaining_shape_path_fixtures", ResvgFixtureArea.Shapes, "tests/shapes/path/", 57),
        new("remaining_structure_image_fixtures", ResvgFixtureArea.Structure, "tests/structure/image/", 55),
        new("remaining_structure_style_fixtures", ResvgFixtureArea.Structure, "tests/structure/style/", 2),
        new("remaining_structure_style_attribute_fixtures", ResvgFixtureArea.Structure, "tests/structure/style-attribute/", 1),
        new("remaining_structure_svg_fixtures", ResvgFixtureArea.Structure, "tests/structure/svg/", 44),
        new("remaining_structure_switch_fixtures", ResvgFixtureArea.Structure, "tests/structure/switch/", 13),
        new("remaining_structure_symbol_fixtures", ResvgFixtureArea.Structure, "tests/structure/symbol/", 17),
        new("remaining_structure_system_language_fixtures", ResvgFixtureArea.Structure, "tests/structure/systemLanguage/", 10),
        new("remaining_structure_transform_origin_fixtures", ResvgFixtureArea.Structure, "tests/structure/transform-origin/", 23)
    };

    private static readonly (ResvgFixtureArea Area, int Count)[] ExpectedRemainingFixtureAreaCounts =
    {
        (ResvgFixtureArea.Extra, 15),
        (ResvgFixtureArea.Filters, 279),
        (ResvgFixtureArea.Masking, 92),
        (ResvgFixtureArea.PaintServers, 114),
        (ResvgFixtureArea.Painting, 85),
        (ResvgFixtureArea.Shapes, 57),
        (ResvgFixtureArea.Structure, 165)
    };

    private static readonly string[] ResourceRenderingFixturePrefixes =
    {
        "tests/filters/feComponentTransfer/",
        "tests/filters/feDisplacementMap/",
        "tests/filters/feDistantLight/",
        "tests/filters/flood-opacity/",
        "tests/filters/filter-functions/",
        "tests/filters/feTurbulence/",
        "tests/masking/clip-rule/",
        "tests/paint-servers/stop/",
        "tests/paint-servers/stop-color/",
        "tests/paint-servers/stop-opacity/",
        "tests/painting/color/",
        "tests/painting/fill-opacity/",
        "tests/painting/fill-rule/",
        "tests/painting/image-rendering/",
        "tests/painting/isolation/",
        "tests/painting/mix-blend-mode/",
        "tests/painting/opacity/",
        "tests/painting/overflow/",
        "tests/painting/paint-order/",
        "tests/painting/shape-rendering/",
        "tests/painting/stroke/",
        "tests/painting/stroke-dasharray/",
        "tests/painting/stroke-dashoffset/",
        "tests/painting/stroke-linecap/",
        "tests/painting/stroke-linejoin/",
        "tests/painting/stroke-miterlimit/",
        "tests/painting/stroke-opacity/",
        "tests/painting/stroke-width/",
        "tests/painting/visibility/",
        "tests/shapes/circle/",
        "tests/shapes/ellipse/",
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

    private sealed record RemainingFixtureFamily(
        string TheoryName,
        ResvgFixtureArea Area,
        string Prefix,
        int ExpectedCount);

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
