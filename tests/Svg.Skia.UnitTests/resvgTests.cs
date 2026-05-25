using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    public static IEnumerable<object[]> TextFixtureRows()
        => EnumerateFixtureRows("tests/text/");

    public static IEnumerable<object[]> NonTextFixtureRows()
        => EnumerateFixtureRows(excludePrefix: "tests/text/");

    [OSXTheory]
    [MemberData(nameof(TextFixtureRows))]
    public void text_fixtures(string relativeName, double errorThreshold)
        => TestImpl(relativeName, errorThreshold);

    [OSXTheory(Skip = "Non-text resvg fixtures are tracked by resvg_fixture_inventory and enabled by feature area.")]
    [MemberData(nameof(NonTextFixtureRows))]
    public void non_text_fixtures(string relativeName, double errorThreshold)
        => TestImpl(relativeName, errorThreshold);

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

    private static IEnumerable<string> EnumerateFixtureNames()
    {
        return EnumerateFixtureNames(GetResvgTestsRoot(), "tests")
            .Concat(EnumerateFixtureNames(GetResvgTestsRoot(), "extra"))
            .OrderBy(x => x, StringComparer.Ordinal);
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
}
