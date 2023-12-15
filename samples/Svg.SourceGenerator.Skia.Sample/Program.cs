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
using System;
using System.Diagnostics;
using System.IO;
using Svg.Generated;
using Svg.Skia;

namespace Svg.SourceGenerator.Skia.Sample;

internal class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine($"Generated class {typeof(Camera)} from Svg file.");
        Console.WriteLine($"Generated class {typeof(Tiger)} from Svg file.");
        Console.WriteLine($"Generated class {typeof(Ellipse)} from Svg file.");
        Console.WriteLine($"Generated class {typeof(Rect)} from Svg file.");
        Console.WriteLine($"Generated class {typeof(Svg_pservers_pattern_01_b)} from Svg file.");

        var sw = new Stopwatch();

        sw.Start();
        using var cameraStream = File.OpenWrite("__AJ_Digital_Camera.png");
        Camera.Picture.ToImage(cameraStream, SkiaSharp.SKColors.Transparent, SkiaSharp.SKEncodedImageFormat.Png, 100, 1, 1, SkiaSharp.SKImageInfo.PlatformColorType, SkiaSharp.SKAlphaType.Unpremul, SkiaSharp.SKColorSpace.CreateRgb(SkiaSharp.SKColorSpaceTransferFn.Srgb, SkiaSharp.SKColorSpaceXyz.Srgb));
        sw.Stop();
        Console.WriteLine($"Created __AJ_Digital_Camera.png in {sw.Elapsed.TotalMilliseconds}ms");

        sw.Reset();
        sw.Start();
        using var tigerStream = File.OpenWrite("__tiger.png");
        Tiger.Picture.ToImage(tigerStream, SkiaSharp.SKColors.Transparent, SkiaSharp.SKEncodedImageFormat.Png, 100, 1, 1, SkiaSharp.SKImageInfo.PlatformColorType, SkiaSharp.SKAlphaType.Unpremul, SkiaSharp.SKColorSpace.CreateRgb(SkiaSharp.SKColorSpaceTransferFn.Srgb, SkiaSharp.SKColorSpaceXyz.Srgb));
        sw.Stop();
        Console.WriteLine($"Created __tiger.png in {sw.Elapsed.TotalMilliseconds}ms");

        sw.Reset();
        sw.Start();
        using var ellipseStream = File.OpenWrite("e-ellipse-001.png");
        Ellipse.Picture.ToImage(ellipseStream, SkiaSharp.SKColors.Transparent, SkiaSharp.SKEncodedImageFormat.Png, 100, 1, 1, SkiaSharp.SKImageInfo.PlatformColorType, SkiaSharp.SKAlphaType.Unpremul, SkiaSharp.SKColorSpace.CreateSrgb());
        sw.Stop();
        Console.WriteLine($"Created e-ellipse-001.png in {sw.Elapsed.TotalMilliseconds}ms");

        sw.Reset();
        sw.Start();
        using var rectStream = File.OpenWrite("e-rect-001.png");
        Rect.Picture.ToImage(rectStream, SkiaSharp.SKColors.Transparent, SkiaSharp.SKEncodedImageFormat.Png, 100, 1, 1, SkiaSharp.SKImageInfo.PlatformColorType, SkiaSharp.SKAlphaType.Unpremul, SkiaSharp.SKColorSpace.CreateSrgb());
        sw.Stop();
        Console.WriteLine($"Created e-rect-001.png in {sw.Elapsed.TotalMilliseconds}ms");

        sw.Reset();
        sw.Start();
        using var patternStream = File.OpenWrite("pservers-pattern-01-b.png");
        Svg_pservers_pattern_01_b.Picture.ToImage(patternStream, SkiaSharp.SKColors.Transparent, SkiaSharp.SKEncodedImageFormat.Png, 100, 1, 1, SkiaSharp.SKImageInfo.PlatformColorType, SkiaSharp.SKAlphaType.Unpremul, SkiaSharp.SKColorSpace.CreateSrgb());
        sw.Stop();
        Console.WriteLine($"Created pservers-pattern-01-b.png in {sw.Elapsed.TotalMilliseconds}ms");
    }
}
