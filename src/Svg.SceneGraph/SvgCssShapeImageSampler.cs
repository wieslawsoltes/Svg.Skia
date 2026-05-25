using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using ShimSkiaSharp;

namespace Svg.Skia;

internal static class SvgCssShapeImageSampler
{
    private static readonly byte[] s_pngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static bool TryCreateAlphaPath(byte[]? encodedData, SKRect referenceBox, float threshold, out SKPath path)
    {
        if (!TryDecodeAlpha(encodedData, out var width, out var height, out var alpha))
        {
            path = new SKPath();
            return false;
        }

        return TryCreateAlphaPath(width, height, alpha, referenceBox, threshold, out path);
    }

    public static bool TryCreateAlphaPath(int width, int height, byte[] alpha, SKRect referenceBox, float threshold, out SKPath path)
    {
        var alphaPath = new SKPath();
        path = alphaPath;
        if (referenceBox.Width <= 0f ||
            referenceBox.Height <= 0f ||
            width <= 0 ||
            height <= 0 ||
            !TryGetImageSampleSize(width, height, bytesPerPixel: 1, out var alphaLength) ||
            alpha.Length < alphaLength)
        {
            return false;
        }

        threshold = Math.Max(0f, Math.Min(1f, threshold));
        var thresholdAlpha = threshold * byte.MaxValue;
        var pixelWidth = referenceBox.Width / width;
        var pixelHeight = referenceBox.Height / height;
        var activeRuns = new List<AlphaRun>();
        for (var y = 0; y < height; y++)
        {
            var rowRuns = new List<AlphaRun>();
            var runStart = -1;
            for (var x = 0; x <= width; x++)
            {
                var included = x < width && alpha[(y * width) + x] > thresholdAlpha;
                if (included)
                {
                    if (runStart < 0)
                    {
                        runStart = x;
                    }

                    continue;
                }

                if (runStart < 0)
                {
                    continue;
                }

                rowRuns.Add(new AlphaRun(runStart, x, y));
                runStart = -1;
            }

            var nextActiveRuns = new List<AlphaRun>(rowRuns.Count);
            for (var rowRunIndex = 0; rowRunIndex < rowRuns.Count; rowRunIndex++)
            {
                var rowRun = rowRuns[rowRunIndex];
                var activeRunIndex = FindRun(activeRuns, rowRun.StartX, rowRun.EndX);
                nextActiveRuns.Add(activeRunIndex >= 0
                    ? activeRuns[activeRunIndex]
                    : rowRun);
            }

            for (var activeRunIndex = 0; activeRunIndex < activeRuns.Count; activeRunIndex++)
            {
                var activeRun = activeRuns[activeRunIndex];
                if (FindRun(rowRuns, activeRun.StartX, activeRun.EndX) < 0)
                {
                    AddRunRect(activeRun, y);
                }
            }

            activeRuns = nextActiveRuns;
        }

        for (var activeRunIndex = 0; activeRunIndex < activeRuns.Count; activeRunIndex++)
        {
            AddRunRect(activeRuns[activeRunIndex], height);
        }

        return !alphaPath.IsEmpty;

        void AddRunRect(AlphaRun run, int endY)
        {
            alphaPath.AddRect(new SKRect(
                referenceBox.Left + (run.StartX * pixelWidth),
                referenceBox.Top + (run.StartY * pixelHeight),
                referenceBox.Left + (run.EndX * pixelWidth),
                referenceBox.Top + (endY * pixelHeight)));
        }
    }

    private readonly record struct AlphaRun(int StartX, int EndX, int StartY);

    private static int FindRun(IReadOnlyList<AlphaRun> runs, int startX, int endX)
    {
        for (var i = 0; i < runs.Count; i++)
        {
            if (runs[i].StartX == startX && runs[i].EndX == endX)
            {
                return i;
            }
        }

        return -1;
    }

    public static bool TryDecodeAlpha(byte[]? encodedData, out int width, out int height, out byte[] alpha)
    {
        if (encodedData is null)
        {
            width = 0;
            height = 0;
            alpha = Array.Empty<byte>();
            return false;
        }

        return TryDecodePngAlpha(encodedData, out width, out height, out alpha);
    }

    private static bool TryDecodePngAlpha(byte[] data, out int width, out int height, out byte[] alpha)
    {
        width = 0;
        height = 0;
        alpha = Array.Empty<byte>();
        if (data.Length < s_pngSignature.Length + 25)
        {
            return false;
        }

        for (var i = 0; i < s_pngSignature.Length; i++)
        {
            if (data[i] != s_pngSignature[i])
            {
                return false;
            }
        }

        var bitDepth = 0;
        var colorType = 0;
        var interlace = 0;
        byte[]? palette = null;
        byte[]? transparency = null;
        using var idat = new MemoryStream();
        var offset = s_pngSignature.Length;
        while (offset + 12 <= data.Length)
        {
            var length = ReadBigEndianInt32(data, offset);
            if (length < 0 || offset + 12 + length > data.Length)
            {
                return false;
            }

            var typeOffset = offset + 4;
            var dataOffset = offset + 8;
            if (IsChunkType(data, typeOffset, "IHDR"))
            {
                if (length != 13)
                {
                    return false;
                }

                width = ReadBigEndianInt32(data, dataOffset);
                height = ReadBigEndianInt32(data, dataOffset + 4);
                bitDepth = data[dataOffset + 8];
                colorType = data[dataOffset + 9];
                interlace = data[dataOffset + 12];
            }
            else if (IsChunkType(data, typeOffset, "IDAT"))
            {
                idat.Write(data, dataOffset, length);
            }
            else if (IsChunkType(data, typeOffset, "PLTE"))
            {
                palette = new byte[length];
                Buffer.BlockCopy(data, dataOffset, palette, 0, length);
            }
            else if (IsChunkType(data, typeOffset, "tRNS"))
            {
                transparency = new byte[length];
                Buffer.BlockCopy(data, dataOffset, transparency, 0, length);
            }
            else if (IsChunkType(data, typeOffset, "IEND"))
            {
                break;
            }

            offset += 12 + length;
        }

        if (width <= 0 ||
            height <= 0 ||
            bitDepth != 8 ||
            interlace != 0 ||
            idat.Length == 0 ||
            !TryGetBytesPerPixel(colorType, out var bytesPerPixel))
        {
            return false;
        }

        if (colorType == 3 &&
            (palette is null || palette.Length == 0 || palette.Length % 3 != 0))
        {
            return false;
        }

        var compressed = idat.ToArray();
        if (compressed.Length <= 6)
        {
            return false;
        }

        byte[] raw;
        try
        {
            using var compressedStream = new MemoryStream(compressed, 2, compressed.Length - 6);
            using var deflate = new DeflateStream(compressedStream, CompressionMode.Decompress);
            using var rawStream = new MemoryStream();
            deflate.CopyTo(rawStream);
            raw = rawStream.ToArray();
        }
        catch
        {
            return false;
        }

        if (!TryGetImageSampleSize(width, height, bytesPerPixel, out var alphaLength, out var rowLength, out var rawLength) ||
            raw.Length < rawLength)
        {
            return false;
        }

        alpha = new byte[alphaLength];
        var previous = new byte[rowLength];
        var current = new byte[rowLength];
        var source = 0;
        for (var y = 0; y < height; y++)
        {
            var filter = raw[source++];
            for (var i = 0; i < rowLength; i++)
            {
                var value = raw[source++];
                var left = i >= bytesPerPixel ? current[i - bytesPerPixel] : 0;
                var up = previous[i];
                var upLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
                current[i] = filter switch
                {
                    0 => value,
                    1 => (byte)((value + left) & 0xff),
                    2 => (byte)((value + up) & 0xff),
                    3 => (byte)((value + ((left + up) >> 1)) & 0xff),
                    4 => (byte)((value + Paeth(left, up, upLeft)) & 0xff),
                    _ => value
                };
            }

            for (var x = 0; x < width; x++)
            {
                if (!TryGetPixelAlpha(colorType, bytesPerPixel, current, x, palette, transparency, out var pixelAlpha))
                {
                    return false;
                }

                alpha[(y * width) + x] = pixelAlpha;
            }

            var swap = previous;
            previous = current;
            current = swap;
            Array.Clear(current, 0, current.Length);
        }

        return true;
    }

    private static bool TryGetBytesPerPixel(int colorType, out int bytesPerPixel)
    {
        bytesPerPixel = colorType switch
        {
            0 => 1,
            2 => 3,
            3 => 1,
            4 => 2,
            6 => 4,
            _ => 0
        };
        return bytesPerPixel > 0;
    }

    private static bool TryGetImageSampleSize(int width, int height, int bytesPerPixel, out int alphaLength)
    {
        return TryGetImageSampleSize(width, height, bytesPerPixel, out alphaLength, out _, out _);
    }

    private static bool TryGetImageSampleSize(
        int width,
        int height,
        int bytesPerPixel,
        out int alphaLength,
        out int rowLength,
        out int rawLength)
    {
        alphaLength = 0;
        rowLength = 0;
        rawLength = 0;
        if (width <= 0 || height <= 0 || bytesPerPixel <= 0)
        {
            return false;
        }

        var alphaLength64 = (long)width * height;
        var rowLength64 = (long)width * bytesPerPixel;
        var rawLength64 = (rowLength64 + 1L) * height;
        if (alphaLength64 > int.MaxValue ||
            rowLength64 > int.MaxValue ||
            rawLength64 > int.MaxValue)
        {
            return false;
        }

        alphaLength = (int)alphaLength64;
        rowLength = (int)rowLength64;
        rawLength = (int)rawLength64;
        return true;
    }

    private static bool TryGetPixelAlpha(
        int colorType,
        int bytesPerPixel,
        byte[] row,
        int x,
        byte[]? palette,
        byte[]? transparency,
        out byte alpha)
    {
        var offset = x * bytesPerPixel;
        alpha = byte.MaxValue;

        switch (colorType)
        {
            case 0:
                if (TryGetTransparentGray(transparency, out var transparentGray) &&
                    row[offset] == transparentGray)
                {
                    alpha = 0;
                }

                return true;
            case 2:
                if (TryGetTransparentColor(transparency, out var transparentRed, out var transparentGreen, out var transparentBlue) &&
                    row[offset] == transparentRed &&
                    row[offset + 1] == transparentGreen &&
                    row[offset + 2] == transparentBlue)
                {
                    alpha = 0;
                }

                return true;
            case 3:
                if (palette is null)
                {
                    return false;
                }

                var paletteIndex = row[offset];
                if (paletteIndex >= palette.Length / 3)
                {
                    return false;
                }

                alpha = transparency is not null && paletteIndex < transparency.Length
                    ? transparency[paletteIndex]
                    : byte.MaxValue;
                return true;
            case 4:
                alpha = row[offset + 1];
                return true;
            case 6:
                alpha = row[offset + 3];
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetTransparentGray(byte[]? transparency, out byte gray)
    {
        gray = 0;
        if (transparency is null || transparency.Length < 2)
        {
            return false;
        }

        var value = ReadBigEndianUInt16(transparency, 0);
        if (value > byte.MaxValue)
        {
            return false;
        }

        gray = (byte)value;
        return true;
    }

    private static bool TryGetTransparentColor(byte[]? transparency, out byte red, out byte green, out byte blue)
    {
        red = 0;
        green = 0;
        blue = 0;
        if (transparency is null || transparency.Length < 6)
        {
            return false;
        }

        var redValue = ReadBigEndianUInt16(transparency, 0);
        var greenValue = ReadBigEndianUInt16(transparency, 2);
        var blueValue = ReadBigEndianUInt16(transparency, 4);
        if (redValue > byte.MaxValue ||
            greenValue > byte.MaxValue ||
            blueValue > byte.MaxValue)
        {
            return false;
        }

        red = (byte)redValue;
        green = (byte)greenValue;
        blue = (byte)blueValue;
        return true;
    }

    private static int ReadBigEndianInt32(byte[] data, int offset)
    {
        return (data[offset] << 24) |
               (data[offset + 1] << 16) |
               (data[offset + 2] << 8) |
               data[offset + 3];
    }

    private static int ReadBigEndianUInt16(byte[] data, int offset)
    {
        return (data[offset] << 8) | data[offset + 1];
    }

    private static bool IsChunkType(byte[] data, int offset, string type)
    {
        return data[offset] == type[0] &&
               data[offset + 1] == type[1] &&
               data[offset + 2] == type[2] &&
               data[offset + 3] == type[3];
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        var p = left + up - upLeft;
        var pa = Math.Abs(p - left);
        var pb = Math.Abs(p - up);
        var pc = Math.Abs(p - upLeft);
        if (pa <= pb && pa <= pc)
        {
            return left;
        }

        return pb <= pc ? up : upLeft;
    }
}
