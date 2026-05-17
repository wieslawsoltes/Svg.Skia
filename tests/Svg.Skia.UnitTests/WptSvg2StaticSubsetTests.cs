using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp.PixelFormats;
using SkiaSharp;
using Svg.Skia.TypefaceProviders;
using Svg.Skia.UnitTests.Common;
using Xunit;

namespace Svg.Skia.UnitTests;

public class WptSvg2StaticSubsetTests : SvgUnitTest
{
    public static IEnumerable<object[]> StaticSubsetRows()
    {
        yield return Row("geometry/reftests/rect-001.svg", 0.022, 300, 200);
        yield return Row("geometry/reftests/circle-001.svg", 0.026, 340, 140);
        yield return Row("geometry/reftests/ellipse-001.svg", 0.022, 300, 200);
        yield return Row("geometry/reftests/percentage.svg", 0.022, 310, 170);
        yield return Row("path/property/d-none.svg", 0.022, 300, 150);
        yield return Row("path/property/priority.svg", 0.03, 100, 100);
        yield return Row("path/property/marker-path.svg", 0.05, 300, 300);
        yield return Row("shapes/reftests/pathlength-001.svg", 0.03, 480, 360);
        yield return Row("shapes/reftests/pathlength-002.svg", 0.03, 480, 360);
        yield return Row("shapes/reftests/pathlength-003.svg", 0.08, 480, 360);
        yield return Row("painting/reftests/paint-context-001.svg", 0.03, 480, 360);
        yield return Row("painting/reftests/paint-context-002.svg", 0.03, 480, 360);
        yield return Row("painting/reftests/paint-context-003.svg", 0.03, 480, 360);
        yield return Row("painting/reftests/paint-context-004.svg", 0.03, 120, 90);
        yield return Row("painting/reftests/paint-order-001.svg", 0.06, 480, 360);
        yield return Row("pservers/reftests/fill-fallback-invalid-uri.svg", 0.022, 300, 150);
        yield return Row("pservers/reftests/fill-fallback-currentcolor-1.svg", 0.022, 300, 150);
        yield return Row("pservers/reftests/stroke-fallback-invalid-uri.svg", 0.022, 300, 150);
        yield return Row("pservers/reftests/radialgradient-basic-002.svg", 0.03, 480, 360);
        yield return Row("struct/reftests/use-symbol-dimensions-override-001.svg", 0.022, 800, 600);
        yield return Row("struct/reftests/use-svg-dimensions-override-001.svg", 0.022, 800, 600);
        yield return Row("text/reftests/textpath-path-attr.svg", 0.07, 400, 200);
        yield return Row("text/reftests/textpath-side-001.svg", 0.08, 400, 300);
        yield return Row("text/reftests/textpath-side-002.svg", 0.08, 400, 200);
        yield return Row("text/reftests/textpath-side-003.svg", 0.08, 400, 200);
        yield return Row("text/reftests/textpath-side-004.svg", 0.08, 400, 200);
        yield return Row("text/reftests/textpath-side-005.svg", 0.08, 400, 500);
    }

    [OSXTheory]
    [MemberData(nameof(StaticSubsetRows))]
    public void Tests(string relativeSvgPath, double errorThreshold, int viewportWidth, int viewportHeight)
    {
        var svgPath = GetSvgPath(relativeSvgPath);
        var expectedPng = GetExpectedPngPath(relativeSvgPath);
        var actualPng = GetActualPngPath(relativeSvgPath);

        if (File.Exists(actualPng))
        {
            File.Delete(actualPng);
        }

        var svg = new SKSvg();
        svg.Settings.EnableTextReferences = false;
        svg.Settings.StandaloneViewport = SKRect.Create(0f, 0f, viewportWidth, viewportHeight);
        SetTypefaceProviders(svg.Settings);
        SetWptTypefaceProviders(svg.Settings);

        using var _ = svg.Load(svgPath);
        svg.Save(actualPng, SKColors.White);

        ImageHelper.CompareImages(
            relativeSvgPath,
            actualPng,
            expectedPng,
            errorThreshold,
            compositeBackground: new Rgba32(255, 255, 255, 255));

        if (File.Exists(actualPng))
        {
            File.Delete(actualPng);
        }
    }

    private static object[] Row(string relativeSvgPath, double errorThreshold, int viewportWidth, int viewportHeight)
        => new object[] { relativeSvgPath, errorThreshold, viewportWidth, viewportHeight };

    private static string GetSvgPath(string relativeSvgPath)
        => Path.Combine("..", "..", "..", "..", "..", "externals", "WPT_SVG_2", "svg", ToLocalPath(relativeSvgPath));

    private static string GetExpectedPngPath(string relativeSvgPath)
        => Path.Combine("..", "..", "..", "ChromeReference", "WPT", "svg", ToLocalPath(Path.ChangeExtension(relativeSvgPath, ".png")));

    private static string GetActualPngPath(string relativeSvgPath)
        => Path.Combine("..", "..", "..", "..", "Tests", $"WPT SVG2 {GetSafeName(relativeSvgPath)} (Actual).png");

    private static string GetWptFontPath(string name)
        => Path.Combine("..", "..", "..", "..", "..", "externals", "WPT_SVG_2", "fonts", name);

    private static void SetWptTypefaceProviders(SKSvgSettings settings)
    {
        if (settings.TypefaceProviders is { })
        {
            settings.TypefaceProviders.Insert(0, new CustomTypefaceProvider(GetWptFontPath("Ahem.ttf")));
        }
    }

    private static string ToLocalPath(string relativePath)
        => relativePath.Replace('/', Path.DirectorySeparatorChar);

    private static string GetSafeName(string relativeSvgPath)
        => relativeSvgPath.Replace('/', '_').Replace(".svg", string.Empty);
}
