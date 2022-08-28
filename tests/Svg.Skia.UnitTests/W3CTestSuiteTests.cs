using System.IO;
using Svg.Skia.UnitTests.Common;
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

    private void TestImpl(string name, double errorThreshold, float scaleX = 1.0f, float scaleY = 1.0f)
    {
        var svgPath = GetSvgPath($"{name}.svg");
        var expectedPng = GetExpectedPngPath($"{name}.png");
        var actualPng = GetActualPngPath($"{name} (Actual).png");

        var svg = new SKSvg();
        using var _ = svg.Load(svgPath);
        svg.Save(actualPng, SkiaSharp.SKColors.Transparent, scaleX: scaleX, scaleY: scaleY);

        ImageHelper.CompareImages(name, actualPng, expectedPng, errorThreshold);

        if (File.Exists(actualPng))
        {
            File.Delete(actualPng);
        }
    }

    [WindowsAndOSXTheory(Skip = "TODO")]
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
    public void coords_trans(string name, double errorThreshold) => TestImpl(name, errorThreshold);
    
    [WindowsAndOSXTheory(Skip = "TODO")]
    [InlineData("coords-transformattr-01-f", 0.022)]
    [InlineData("coords-transformattr-02-f", 0.022)]
    [InlineData("coords-transformattr-03-f", 0.022)]
    [InlineData("coords-transformattr-04-f", 0.022)]
    [InlineData("coords-transformattr-05-f", 0.022)]
    public void coords_transformattr(string name, double errorThreshold) => TestImpl(name, errorThreshold);
    

    [WindowsAndOSXTheory]
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
    public void paths_data(string name, double errorThreshold) => TestImpl(name, errorThreshold);

    [WindowsTheory]
    [InlineData("__AJ_Digital_Camera", 0.027)]
    [InlineData("__Telefunken_FuBK_test_pattern", 0.022)]
    [InlineData("__tiger", 0.055)]
    public void Misc(string name, double errorThreshold) => TestImpl(name, errorThreshold);
}
