using System.IO;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SKSvgTests
{
    private static string GetPath(string name) => Path.Combine("..", "..", "..", "..", "Tests", name);

    [Theory]
    [InlineData("Sign in")]
    public void Test(string name)
    {
        var inSvgPath = GetPath($"{name}.svg");
        var expectedPng = GetPath($"{name}.png");
        var actualPng = GetPath($"{name} (Actual).png");

        var svg = new SKSvg();
        using var _ = svg.Load(inSvgPath);
        svg.Save(actualPng, SkiaSharp.SKColors.Transparent);

        using var expected = SkiaSharp.SKBitmap.Decode(expectedPng);
        using var actual = SkiaSharp.SKBitmap.Decode(actualPng);

        Assert.Equal(expected.Info, actual.Info);
        Assert.Equal(expected.Bytes, actual.Bytes);
    }
}
