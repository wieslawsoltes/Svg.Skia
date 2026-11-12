using System;
using System.Collections.Generic;
using SkiaSharp;
using Svg.Skia;
using UnoDraw.Rendering;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

/// <summary>
/// Verifies the lossless BGRA-to-RGBA upload used by the browser's retained texture
/// bridge without browser composition or screenshot tolerances. Both surfaces
/// keep the existing BGRA SVG painter, scale and antialiasing; the upload uses
/// Skia's native channel permutation without color or alpha conversion.
/// </summary>
public class RgbaCanvasEquivalenceTests
{
    private const float CanvasWidth = 173.5f;
    private const float CanvasHeight = 129.25f;

    public static IEnumerable<object[]> RenderCases()
    {
        string[] scenes = ["gradients", "filters", "mask", "clip", "pattern", "text", "image", "opacity-and-strokes"];
        double[] scales = [1, 1.25, 2];
        (float Zoom, float PanX, float PanY)[] viewports =
        [
            (1, 0, 0),
            (1.375f, -13.625f, 7.375f),
            (0.625f, 33.125f, 19.875f)
        ];

        foreach (var scene in scenes)
            foreach (var scale in scales)
                foreach (var (zoom, panX, panY) in viewports)
                    yield return [scene, scale, zoom, panX, panY];
    }

    [Theory]
    [MemberData(nameof(RenderCases))]
    public void RgbaUploadPixelsExactlyMatchLegacyBgraPixels(
        string scene, double deviceScale, float zoom, float panX, float panY)
    {
        using var svg = new SKSvg();
        svg.FromSvg(CreateSvg(scene));
        Assert.NotNull(svg.Picture);

        var width = checked((int)Math.Ceiling(CanvasWidth * deviceScale));
        var height = checked((int)Math.Ceiling(CanvasHeight * deviceScale));
        using var legacy = RenderLegacy(svg, width, height, deviceScale, zoom, panX, panY);
        using var candidate = new SkiaCanvasUploadBuffer(width, height);
        Draw(svg, candidate.Canvas, deviceScale, zoom, panX, panY);
        candidate.UpdateUpload();
        using var oldPixels = legacy.PeekPixels();
        Assert.NotNull(oldPixels);
        Assert.Equal(SKAlphaType.Premul, oldPixels.AlphaType);
        Assert.Equal(SKAlphaType.Premul, candidate.UploadInfo.AlphaType);

        var oldBytes = oldPixels.GetPixelSpan();
        var newBytes = candidate.UploadSpan;
        var painted = 0;
        var translucent = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var oldOffset = y * oldPixels.RowBytes + x * 4;
                var newOffset = y * candidate.UploadRowBytes + x * 4;
                var oldAlpha = oldBytes[oldOffset + 3];
                if (oldAlpha != 0)
                    painted++;
                if (oldAlpha is > 0 and < 255)
                    translucent++;

                // Compare premultiplied storage directly. SKColor/GetPixel would
                // unpremultiply and could hide a channel or alpha rounding error.
                var equal = oldBytes[oldOffset + 2] == newBytes[newOffset] &&
                            oldBytes[oldOffset + 1] == newBytes[newOffset + 1] &&
                            oldBytes[oldOffset] == newBytes[newOffset + 2] &&
                            oldAlpha == newBytes[newOffset + 3];
                if (!equal)
                {
                    Assert.Fail(
                        $"{scene}, DPR {deviceScale}, zoom {zoom}, pan ({panX}, {panY}), pixel ({x}, {y}): " +
                        $"legacy RGBA ({oldBytes[oldOffset + 2]}, {oldBytes[oldOffset + 1]}, {oldBytes[oldOffset]}, {oldAlpha}) " +
                        $"!= candidate RGBA ({newBytes[newOffset]}, {newBytes[newOffset + 1]}, {newBytes[newOffset + 2]}, {newBytes[newOffset + 3]}).");
                }
            }
        }

        Assert.True(painted > 32, $"The {scene} scene did not render enough visible pixels to exercise the painter.");
        Assert.True(translucent > 8, $"The {scene} scene did not exercise premultiplied transparency or antialiasing.");
    }

    private static SKSurface RenderLegacy(SKSvg svg, int width, int height,
        double deviceScale, float zoom, float panX, float panY)
    {
        var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        Assert.NotNull(surface);
        Draw(svg, surface.Canvas, deviceScale, zoom, panX, panY);
        surface.Canvas.Flush();
        return surface;
    }

    private static void Draw(SKSvg svg, SKCanvas canvas, double deviceScale, float zoom, float panX, float panY)
    {
        canvas.Clear(SKColors.Transparent);
        canvas.Save();
        canvas.Scale((float)deviceScale);
        canvas.ClipRect(new SKRect(0, 0, CanvasWidth, CanvasHeight));
        canvas.Translate(panX, panY);
        canvas.Scale(zoom);
        svg.Draw(canvas);
        canvas.Restore();
    }

    private static string CreateSvg(string scene)
    {
        var content = scene switch
        {
            "gradients" => """
                <defs>
                  <linearGradient id="linear" x1="0.1" y1="0.2" x2="0.9" y2="0.85" spreadMethod="reflect">
                    <stop offset="0" stop-color="#f32b17" stop-opacity="0.23"/>
                    <stop offset="0.37" stop-color="#1785e9" stop-opacity="0.81"/>
                    <stop offset="1" stop-color="#e6c936" stop-opacity="0.57"/>
                  </linearGradient>
                  <radialGradient id="radial" cx="0.6" cy="0.4" r="0.68" fx="0.35" fy="0.2">
                    <stop offset="0" stop-color="#f3dbfa"/>
                    <stop offset="0.53" stop-color="#8b23d9" stop-opacity="0.63"/>
                    <stop offset="1" stop-color="#105a34" stop-opacity="0.08"/>
                  </radialGradient>
                </defs>
                <rect x="7.25" y="5.5" width="141.5" height="88.75" rx="13.25" fill="url(#linear)"/>
                <ellipse cx="83.75" cy="57.25" rx="54.5" ry="39.25" fill="url(#radial)"/>
                """,
            "filters" => """
                <defs>
                  <filter id="effect" x="-0.4" y="-0.4" width="1.8" height="1.8" color-interpolation-filters="sRGB">
                    <feGaussianBlur in="SourceGraphic" stdDeviation="2.35" result="blur"/>
                    <feOffset in="blur" dx="3.25" dy="1.75" result="shift"/>
                    <feColorMatrix in="shift" type="matrix"
                      values="0.7 0.1 0.2 0 0.03  0.1 0.8 0.1 0 0.02  0.2 0.1 0.7 0 0.01  0 0 0 0.73 0" result="tint"/>
                    <feBlend in="SourceGraphic" in2="tint" mode="multiply"/>
                  </filter>
                </defs>
                <g filter="url(#effect)" opacity="0.83">
                  <path d="M17 84 C24 2 87 3 142 74 L100 97 Z" fill="#d34528"/>
                  <circle cx="92.75" cy="41.25" r="27.5" fill="#28a8ce" fill-opacity="0.61"/>
                </g>
                """,
            "mask" => """
                <defs>
                  <linearGradient id="fade"><stop stop-color="white"/><stop offset="1" stop-color="#183c85"/></linearGradient>
                  <mask id="mask" maskUnits="userSpaceOnUse" x="0" y="0" width="160" height="110">
                    <rect x="4.25" y="5.75" width="145.5" height="92.5" fill="url(#fade)"/>
                    <ellipse cx="74.5" cy="53.25" rx="38.5" ry="27.75" fill="#724624" fill-opacity="0.7"/>
                  </mask>
                </defs>
                <g mask="url(#mask)"><rect x="2.5" y="3.5" width="151" height="99" fill="#e83c91"/>
                  <path d="M10 90 Q45 -5 142 73" fill="none" stroke="#1ea653" stroke-width="17.25"/>
                </g>
                """,
            "clip" => """
                <defs><clipPath id="clip"><path d="M9.25 53 C24 -6 133 -2 151 56 C126 118 36 112 9.25 53 Z"/></clipPath></defs>
                <g clip-path="url(#clip)" opacity="0.87">
                  <rect width="160" height="110" fill="#197ec8"/>
                  <path d="M-10 15 L163 89 M-10 85 L163 22" stroke="#f3b52c" stroke-width="19.75"/>
                  <circle cx="79.5" cy="54.25" r="32.75" fill="#a63ad6" fill-opacity="0.43"/>
                </g>
                """,
            "pattern" => """
                <defs><pattern id="tiles" patternUnits="userSpaceOnUse" width="17.5" height="13.25" patternTransform="rotate(13)">
                  <rect width="17.5" height="13.25" fill="#225b9c" fill-opacity="0.46"/>
                  <circle cx="4.75" cy="4.25" r="3.5" fill="#e9a83c"/>
                  <path d="M9 13 L17 2" stroke="#d62681" stroke-width="2.25" stroke-opacity="0.72"/>
                </pattern></defs>
                <path d="M10 91 L21 11 L135 5 L151 96 Z" fill="url(#tiles)" stroke="#236b2f" stroke-width="3.5"/>
                """,
            "text" => """
                <text x="8.25" y="37.5" font-family="sans-serif" font-size="25.75" fill="#b92673" fill-opacity="0.79">Editor 7</text>
                <text x="13.5" y="75.25" font-family="serif" font-size="19.25" font-style="italic"
                      fill="#216bc4" stroke="#32a458" stroke-width="0.45">SVG canvas</text>
                <text x="20.25" y="99.5" font-family="monospace" font-size="13.5" letter-spacing="0.35" fill="#af7421">Aa 012345</text>
                """,
            "image" => $"""
                <image x="9.25" y="8.5" width="126.75" height="89.25" opacity="0.87"
                       preserveAspectRatio="none" xlink:href="data:image/png;base64,{CreateEmbeddedImage()}"/>
                <path d="M6 80 Q72 10 150 89" stroke="#d93d57" stroke-width="5.75" stroke-opacity="0.63" fill="none"/>
                """,
            "opacity-and-strokes" => """
                <g opacity="0.67" transform="translate(2.25 1.75)">
                  <rect x="8.5" y="12.25" width="111.75" height="71.5" rx="11.25" fill="#3275cd" fill-opacity="0.73"/>
                  <circle cx="95.75" cy="57.5" r="35.25" fill="#ec4b28" fill-opacity="0.61"/>
                  <path d="M11.25 96.5 C14.5 3.25 91.75 -8.5 147.25 88.5" fill="none"
                        stroke="#38a85c" stroke-width="4.35" stroke-linecap="round" stroke-linejoin="round"
                        stroke-dasharray="11.25 6.5 2.75 4.25" stroke-dashoffset="1.375"/>
                </g>
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(scene))
        };
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="160" height="110" viewBox="0 0 160 110">
            {content}
            </svg>
            """;
    }

    private static string CreateEmbeddedImage()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(4, 4, SKColorType.Rgba8888, SKAlphaType.Premul));
        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                bitmap.SetPixel(x, y, new SKColor((byte)(31 + 57 * x), (byte)(19 + 61 * y),
                    (byte)(229 - 23 * x - 17 * y), (byte)(37 + 29 * x + 31 * y)));
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return Convert.ToBase64String(png.ToArray());
    }
}
