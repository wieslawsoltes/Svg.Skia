using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Svg.Skia.UnitTests.Common;

public static class ImageHelper
{
    private static double CompareImages(Image<Rgba32> actual, Image<Rgba32> expected, IReadOnlyCollection<Rectangle>? ignoredRegions = null, Rgba32? compositeBackground = null)
    {
        if (actual.Width != expected.Width || actual.Height != expected.Height)
        {
            throw new ArgumentException("Images have different resolutions");
        }

        var quantity = 0;
        double squaresError = 0;

        const double scale = 1 / 255d;
        var useCompositeBackground = compositeBackground.HasValue;
        var compositeColor = compositeBackground.GetValueOrDefault();
        var backgroundR = compositeColor.R * scale;
        var backgroundG = compositeColor.G * scale;
        var backgroundB = compositeColor.B * scale;

        for (var x = 0; x < actual.Width; x++)
        {
            double localError = 0;

            for (var y = 0; y < actual.Height; y++)
            {
                if (IsIgnored(x, y, ignoredRegions))
                {
                    continue;
                }

                var expectedPixel = expected[x, y];
                var actualPixel = actual[x, y];

                var expectedAlpha = expectedPixel.A * scale;
                var actualAlpha = actualPixel.A * scale;

                if (useCompositeBackground)
                {
                    var expectedR = backgroundR + expectedAlpha * (expectedPixel.R * scale - backgroundR);
                    var expectedG = backgroundG + expectedAlpha * (expectedPixel.G * scale - backgroundG);
                    var expectedB = backgroundB + expectedAlpha * (expectedPixel.B * scale - backgroundB);

                    var actualR = backgroundR + actualAlpha * (actualPixel.R * scale - backgroundR);
                    var actualG = backgroundG + actualAlpha * (actualPixel.G * scale - backgroundG);
                    var actualB = backgroundB + actualAlpha * (actualPixel.B * scale - backgroundB);

                    var deltaR = expectedR - actualR;
                    var deltaG = expectedG - actualG;
                    var deltaB = expectedB - actualB;
                    var compositeError = deltaR * deltaR + deltaG * deltaG + deltaB * deltaB;

                    localError += compositeError;
                    quantity += 3;
                    continue;
                }

                var r = scale * (expectedAlpha * expectedPixel.R - actualAlpha * actualPixel.R);
                var g = scale * (expectedAlpha * expectedPixel.G - actualAlpha * actualPixel.G);
                var b = scale * (expectedAlpha * expectedPixel.B - actualAlpha * actualPixel.B);
                var a = expectedAlpha - actualAlpha;

                var error = r * r + g * g + b * b + a * a;

                localError += error;
                quantity++;
            }

            squaresError += localError;
        }

        if (quantity == 0)
        {
            return 0d;
        }

        var meanSquaresError = squaresError / quantity;

        var channelCount = useCompositeBackground ? 3 : 4;

        meanSquaresError = meanSquaresError / channelCount;

        return Math.Sqrt(meanSquaresError);
    }

    public static void CompareImages(string name, string actualPath, string expectedPath, double errorThreshold, IReadOnlyCollection<Rectangle>? ignoredRegions = null, Rgba32? compositeBackground = null)
    {
        using var expected = Image.Load<Rgba32>(expectedPath);
        using var actual = Image.Load<Rgba32>(actualPath);
        var immediateError = CompareImages(actual, expected, ignoredRegions, compositeBackground);

        if (immediateError > errorThreshold)
        {
            Assert.Fail(name + ": Error = " + immediateError);
        }
    }

    private static bool IsIgnored(int x, int y, IReadOnlyCollection<Rectangle>? ignoredRegions)
    {
        if (ignoredRegions is null || ignoredRegions.Count == 0)
        {
            return false;
        }

        foreach (var region in ignoredRegions)
        {
            if (x >= region.Left && x < region.Right && y >= region.Top && y < region.Bottom)
            {
                return true;
            }
        }

        return false;
    }
}
