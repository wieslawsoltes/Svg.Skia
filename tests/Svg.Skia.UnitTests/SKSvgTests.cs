using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Xunit.Abstractions;

namespace Svg.Skia.UnitTests;

public class SKSvgTests
{
    private readonly ITestOutputHelper _output;

    private string GetPath(string name) => Path.Combine("..", "..", "..", "..", "Tests", name);

    private void CompareImages(string name, string actualPath, string expectedPath, double errorThreshold)
    {
        using var expected = Image.Load<Rgba32>(expectedPath);
        using var actual = Image.Load<Rgba32>(actualPath);
        var immediateError = CompareImages(actual, expected);

        _output.WriteLine($"[{name}] {immediateError}");

        if (immediateError > errorThreshold)
        {
            Assert.True(false, name + ": Error = " + immediateError);
        }
    }

    private double CompareImages(Image<Rgba32> actual, Image<Rgba32> expected)
    {
        if (actual.Width != expected.Width || actual.Height != expected.Height)
        {
            throw new ArgumentException("Images have different resolutions");
        }

        var quantity = actual.Width * actual.Height;
        double squaresError = 0;

        const double scale = 1 / 255d;
        
        for (var x = 0; x < actual.Width; x++)
        {
            double localError = 0;
            
            for (var y = 0; y < actual.Height; y++)
            {
                var expectedAlpha = expected[x, y].A * scale;
                var actualAlpha = actual[x, y].A * scale;
                
                var r = scale * (expectedAlpha * expected[x, y].R - actualAlpha * actual[x, y].R);
                var g = scale * (expectedAlpha * expected[x, y].G - actualAlpha * actual[x, y].G);
                var b = scale * (expectedAlpha * expected[x, y].B - actualAlpha * actual[x, y].B);
                var a = expectedAlpha - actualAlpha;

                var error = r * r + g * g + b * b + a * a;

                localError += error;
            }

            squaresError += localError;
        }

        var meanSquaresError = squaresError / quantity;

        const int channelCount = 4;
        
        meanSquaresError = meanSquaresError / channelCount;
        
        return Math.Sqrt(meanSquaresError);
    }

    public SKSvgTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [WindowsTheory]
    [InlineData("Sign in", 0.022)]
    [InlineData("__AJ_Digital_Camera", 0.027)]
    [InlineData("__Telefunken_FuBK_test_pattern", 0.022)]
    [InlineData("__tiger", 0.055)]
    public void Test(string name, double errorThreshold)
    {
        var inSvgPath = GetPath($"{name}.svg");
        var expectedPng = GetPath($"{name}.png");
        var actualPng = GetPath($"{name} (Actual).png");

        var svg = new SKSvg();
        using var _ = svg.Load(inSvgPath);
        svg.Save(actualPng, SkiaSharp.SKColors.Transparent);

        CompareImages(name, actualPng, expectedPng, errorThreshold);

        File.Delete(actualPng);
    }
}
