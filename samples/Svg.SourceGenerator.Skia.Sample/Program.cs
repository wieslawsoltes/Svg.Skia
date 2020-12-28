using System;
using System.Diagnostics;
using System.IO;
using SkiaSharp;
using Svg.Skia;
using Svg.Generated;
using Svg.Sample;

namespace Svg.SourceGenerator.Skia.Sample
{
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
            Camera.Picture.ToImage(cameraStream, SKColors.Transparent, SKEncodedImageFormat.Png, 100, 1, 1, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul, SKColorSpace.CreateRgb(SKColorSpaceTransferFn.Srgb, SKColorSpaceXyz.Srgb));
            sw.Stop();
            Console.WriteLine($"Created __AJ_Digital_Camera.png in {sw.Elapsed.TotalMilliseconds}ms");

            sw.Reset();
            sw.Start();
            using var tigerStream = File.OpenWrite("__tiger.png");
            Tiger.Picture.ToImage(tigerStream, SKColors.Transparent, SKEncodedImageFormat.Png, 100, 1, 1, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul, SKColorSpace.CreateRgb(SKColorSpaceTransferFn.Srgb, SKColorSpaceXyz.Srgb));
            sw.Stop();
            Console.WriteLine($"Created __tiger.png in {sw.Elapsed.TotalMilliseconds}ms");

            sw.Reset();
            sw.Start();
            using var ellipseStream = File.OpenWrite("e-ellipse-001.png");
            Ellipse.Picture.ToImage(ellipseStream, SKColors.Transparent, SKEncodedImageFormat.Png, 100, 1, 1, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul, SKSvgSettings.s_srgb);
            sw.Stop();
            Console.WriteLine($"Created e-ellipse-001.png in {sw.Elapsed.TotalMilliseconds}ms");

            sw.Reset();
            sw.Start();
            using var rectStream = File.OpenWrite("e-rect-001.png");
            Rect.Picture.ToImage(rectStream, SKColors.Transparent, SKEncodedImageFormat.Png, 100, 1, 1, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul, SKSvgSettings.s_srgb);
            sw.Stop();
            Console.WriteLine($"Created e-rect-001.png in {sw.Elapsed.TotalMilliseconds}ms");

            sw.Reset();
            sw.Start();
            using var patternStream = File.OpenWrite("pservers-pattern-01-b.png");
            Svg_pservers_pattern_01_b.Picture.ToImage(patternStream, SKColors.Transparent, SKEncodedImageFormat.Png, 100, 1, 1, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul, SKSvgSettings.s_srgb);
            sw.Stop();
            Console.WriteLine($"Created pservers-pattern-01-b.png in {sw.Elapsed.TotalMilliseconds}ms");
        }
    }
}
