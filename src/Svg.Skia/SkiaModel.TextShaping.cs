// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Runtime.InteropServices;
using HarfBuzzSharp;
using Buffer = HarfBuzzSharp.Buffer;

namespace Svg.Skia;

public partial class SkiaModel
{
    private const int HarfBuzzFontScale = 512;

    private bool TryDrawShapedText(
        SkiaSharp.SKCanvas canvas,
        string text,
        float x,
        float y,
        SkiaSharp.SKPaint paint)
    {
        if (!TryShapeText(text, x, y, paint, out var result))
        {
            return false;
        }

        using var builder = new SkiaSharp.SKTextBlobBuilder();
        using var font = paint.ToFont();
        if (font is null)
        {
            return false;
        }

        var glyphs = new ushort[result.Codepoints.Length];
        for (var i = 0; i < result.Codepoints.Length; i++)
        {
            glyphs[i] = (ushort)result.Codepoints[i];
        }

        builder.AddPositionedRun(glyphs, font, result.Points);
        using var textBlob = builder.Build();
        if (textBlob is null)
        {
            return false;
        }

        var xOffset = paint.TextAlign switch
        {
            SkiaSharp.SKTextAlign.Center => -(result.Width * 0.5f),
            SkiaSharp.SKTextAlign.Right => -result.Width,
            _ => 0f
        };

        canvas.DrawText(textBlob, xOffset, 0, paint);
        return true;
    }

    internal float GetTextAdvance(string text, SkiaSharp.SKPaint paint)
    {
        if (TryShapeText(text, 0f, 0f, paint, out var result))
        {
            return result.Width;
        }

        return paint.MeasureText(text);
    }

    private bool TryShapeText(
        string text,
        float x,
        float y,
        SkiaSharp.SKPaint paint,
        out ShapedTextResult result)
    {
        if (string.IsNullOrEmpty(text) ||
            paint.Typeface is null)
        {
            result = default;
            return false;
        }

        using var font = paint.ToFont();
        if (font is null || font.Typeface is null)
        {
            result = default;
            return false;
        }

        if (!HarfBuzzTextShaper.TryCreate(font.Typeface, out var shaper))
        {
            result = default;
            return false;
        }

        using (shaper)
        {
            result = shaper.Shape(text, x, y, font);
        }

        return result.Codepoints.Length > 0;
    }

    private static Blob ToHarfBuzzBlob(SkiaSharp.SKStreamAsset asset)
    {
        if (asset is null)
        {
            throw new ArgumentNullException(nameof(asset));
        }

        var size = asset.Length;
        Blob blob;

        var memoryBase = asset.GetMemoryBase();
        if (memoryBase != IntPtr.Zero)
        {
            blob = new Blob(memoryBase, size, MemoryMode.ReadOnly, asset.Dispose);
        }
        else
        {
            var ptr = Marshal.AllocCoTaskMem(size);
            asset.Read(ptr, size);
            blob = new Blob(ptr, size, MemoryMode.ReadOnly, () =>
            {
                Marshal.FreeCoTaskMem(ptr);
                asset.Dispose();
            });
        }

        blob.MakeImmutable();
        return blob;
    }

    private readonly record struct ShapedTextResult(
        uint[] Codepoints,
        SkiaSharp.SKPoint[] Points,
        float Width);

    private sealed class HarfBuzzTextShaper : IDisposable
    {
        private readonly Font _font;

        private HarfBuzzTextShaper(SkiaSharp.SKTypeface typeface, Font font)
        {
            Typeface = typeface ?? throw new ArgumentNullException(nameof(typeface));
            _font = font ?? throw new ArgumentNullException(nameof(font));
        }

        public static bool TryCreate(SkiaSharp.SKTypeface typeface, out HarfBuzzTextShaper shaper)
        {
            if (typeface is null)
            {
                throw new ArgumentNullException(nameof(typeface));
            }

            int index;
            var stream = typeface.OpenStream(out index);
            if (stream is null)
            {
                shaper = null!;
                return false;
            }

            using var blob = ToHarfBuzzBlob(stream);
            using var face = new Face(blob, index);
            face.Index = index;
            face.UnitsPerEm = typeface.UnitsPerEm;

            var font = new Font(face);
            font.SetScale(HarfBuzzFontScale, HarfBuzzFontScale);
            font.SetFunctionsOpenType();

            shaper = new HarfBuzzTextShaper(typeface, font);
            return true;
        }

        public SkiaSharp.SKTypeface Typeface { get; }

        public void Dispose()
        {
            _font.Dispose();
        }

        public ShapedTextResult Shape(string text, float xOffset, float yOffset, SkiaSharp.SKFont font)
        {
            if (string.IsNullOrEmpty(text))
            {
                return default;
            }

            using var buffer = new Buffer();
            buffer.AddUtf8(text);
            buffer.GuessSegmentProperties();

            _font.Shape(buffer);

            var length = buffer.Length;
            var glyphInfos = buffer.GlyphInfos;
            var glyphPositions = buffer.GlyphPositions;

            var textSizeY = font.Size / HarfBuzzFontScale;
            var textSizeX = textSizeY * font.ScaleX;
            var startX = xOffset;

            var codepoints = new uint[length];
            var points = new SkiaSharp.SKPoint[length];

            for (var i = 0; i < length; i++)
            {
                codepoints[i] = glyphInfos[i].Codepoint;
                points[i] = new SkiaSharp.SKPoint(
                    xOffset + (glyphPositions[i].XOffset * textSizeX),
                    yOffset - (glyphPositions[i].YOffset * textSizeY));

                xOffset += glyphPositions[i].XAdvance * textSizeX;
                yOffset += glyphPositions[i].YAdvance * textSizeY;
            }

            return new ShapedTextResult(codepoints, points, xOffset - startX);
        }
    }
}
