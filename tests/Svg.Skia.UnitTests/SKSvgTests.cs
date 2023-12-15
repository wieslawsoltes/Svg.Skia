/*
 * Svg.Skia SVG rendering library.
 * Copyright (C) 2023  Wiesław Šoltés
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
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
