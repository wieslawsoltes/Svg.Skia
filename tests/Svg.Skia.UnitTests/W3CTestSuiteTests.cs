using System.IO;
using Xunit;

namespace Svg.Skia.UnitTests;

public class W3CTestSuiteTests
{
    private string GetSvgPath(string name) 
        => Path.Combine("..", "..", "..", "..", "..", "externals", "SVG", "Tests", "W3CTestSuite", "svg", name);

    private string GetExpectedPngPath(string name) 
        => Path.Combine("..", "..", "..", "..", "..", "externals", "SVG", "Tests", "W3CTestSuite", "png", name);

    private string GetActualPngPath(string name)
        => Path.Combine("..", "..", "..", "..", "Tests", name);

    [Theory]
    [InlineData("paths-data-01-t", 0.074)]
    [InlineData("paths-data-02-t", 0.086)]
    [InlineData("paths-data-03-f", 0.076)]
    [InlineData("paths-data-04-t", 0.075)]
    [InlineData("paths-data-05-t", 0.069)]
    [InlineData("paths-data-06-t", 0.067)]
    [InlineData("paths-data-07-t", 0.065)]
    [InlineData("paths-data-08-t", 0.080)]
    [InlineData("paths-data-09-t", 0.076)]
    [InlineData("paths-data-10-t", 0.101)]
    [InlineData("paths-data-12-t", 0.062)]
    [InlineData("paths-data-13-t", 0.062)]
    [InlineData("paths-data-14-t", 0.064)]
    [InlineData("paths-data-15-t", 0.063)]
    [InlineData("paths-data-16-t", 0.069)]
    [InlineData("paths-data-17-f", 0.068)]
    [InlineData("paths-data-18-f", 0.059)]
    [InlineData("paths-data-19-f", 0.076)]
    [InlineData("paths-data-20-f", 0.063)]
    public void paths_data(string name, double errorThreshold)
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

    [WindowsTheory]
    [InlineData("__AJ_Digital_Camera", 0.027)]
    [InlineData("__Telefunken_FuBK_test_pattern", 0.022)]
    [InlineData("__tiger", 0.055)]
    public void Misc(string name, double errorThreshold)
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
