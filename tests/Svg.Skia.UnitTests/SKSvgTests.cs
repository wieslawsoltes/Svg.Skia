using System.IO;
using Svg.Skia.UnitTests.Common;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SKSvgTests : SvgUnitTest
{
    private static string GetSvgPath(string name)
        => Path.Combine("..", "..", "..", "..", "Tests", name);

    private static string GetExpectedPngPath(string name)
        => Path.Combine("..", "..", "..", "..", "Tests", name);

    private static string GetActualPngPath(string name)
        => Path.Combine("..", "..", "..", "..", "Tests", name);

    [WindowsTheory]
    [InlineData("Sign in", 0.022)]
    public void Test(string name, double errorThreshold)
    {
        var svgPath = GetSvgPath($"{name}.svg");
        var expectedPng = GetExpectedPngPath($"{name}.png");
        var actualPng = GetActualPngPath($"{name} (Actual).png");

        var svg = new SKSvg();
        using var _ = svg.Load(svgPath);
        svg.Save(actualPng, SkiaSharp.SKColors.Transparent);

        ImageHelper.CompareImages(name, actualPng, expectedPng, errorThreshold);

        File.Delete(actualPng);
    }
}
