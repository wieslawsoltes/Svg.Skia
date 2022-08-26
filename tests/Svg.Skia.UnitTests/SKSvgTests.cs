using System.IO;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SKSvgTests
{
    private string GetSvgPath(string name) 
        => Path.Combine("..", "..", "..", "..", "Tests", name);

    private string GetExpectedPngPath(string name) 
        => Path.Combine("..", "..", "..", "..", "Tests", name);

    private string GetActualPngPath(string name) 
        => Path.Combine("..", "..", "..", "..", "Tests", name);

    [WindowsTheory]
    [InlineData("Sign in", 0.022)]
    [InlineData("__AJ_Digital_Camera", 0.027)]
    [InlineData("__Telefunken_FuBK_test_pattern", 0.022)]
    [InlineData("__tiger", 0.055)]
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
