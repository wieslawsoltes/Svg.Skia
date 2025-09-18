using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Svg.Skia.UnitTests.Common;

public static class ImageHelper
{
    private static double CompareImages(Image<Rgba32> actual, Image<Rgba32> expected)
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

    public static void CompareImages(string name, string actualPath, string expectedPath, double errorThreshold)
    {
        using var expected = Image.Load<Rgba32>(expectedPath);
        using var actual = Image.Load<Rgba32>(actualPath);
        var immediateError = CompareImages(actual, expected);

        if (immediateError > errorThreshold)
        {
            Assert.Fail(name + ": Error = " + immediateError);
        }
    }
}
