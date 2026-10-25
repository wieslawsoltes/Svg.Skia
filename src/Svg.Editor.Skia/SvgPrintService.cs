using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>Physical-page vector PDF, profiled CMYK PDF and process-plate export.</summary>
public static partial class SvgPrintService
{
    public const long MaximumPagePixels = 40_000_000;
    public const long MaximumJobPixels = 160_000_000;

    public static void ExportPdf(SKPicture picture, Stream output, SvgPrintSettings settings,
        SvgIccCmykProfile? profile = null)
    {
        if (picture is null)
            throw new ArgumentNullException(nameof(picture));
        if (output is null)
            throw new ArgumentNullException(nameof(output));
        var pages = SvgPrintLayoutService.CreatePages(picture.CullRect, settings);
        if (settings.ColorMode == SvgPrintColorMode.Cmyk)
        {
            ValidateRasterJob(pages, settings);
            ExportCmykPdf(picture, output, settings, pages, profile);
            return;
        }
        using var buffer = new MemoryStream();
        using (var document = SKDocument.CreatePdf(buffer, (float)settings.Dpi))
        {
            if (document is null)
                throw new InvalidOperationException("PDF export is unavailable on this platform.");
            foreach (var page in pages)
            {
                var canvas = document.BeginPage(page.MediaSize.Width, page.MediaSize.Height);
                DrawPage(canvas, picture, page, settings);
                document.EndPage();
            }
            document.Close();
        }
        var pdf = SvgPdfPageBoxes.Apply(buffer.ToArray(), pages);
        output.Write(pdf, 0, pdf.Length);
    }

    public static void DrawPage(SKCanvas canvas, SKPicture picture, SvgPrintPage page, SvgPrintSettings settings)
    {
        canvas.Clear(SKColors.White);
        canvas.Save();
        canvas.ClipRect(page.BleedBox);
        var matrix = page.ArtworkTransform;
        canvas.Concat(in matrix);
        canvas.DrawPicture(picture);
        canvas.Restore();
        DrawMarks(canvas, page, settings);
    }

    public static SKBitmap RenderPage(SKPicture picture, SvgPrintPage page, SvgPrintSettings settings, double? dpi = null)
    {
        var resolution = dpi ?? settings.Dpi;
        if (double.IsNaN(resolution) || double.IsInfinity(resolution) || resolution <= 0 || resolution > 2400)
            throw new ArgumentOutOfRangeException(nameof(dpi));
        var width = checked((int)Math.Ceiling(page.MediaSize.Width * resolution / 72));
        var height = checked((int)Math.Ceiling(page.MediaSize.Height * resolution / 72));
        if (width <= 0 || height <= 0 || (long)width * height > MaximumPagePixels)
            throw new InvalidOperationException("This print page exceeds the raster limit. Reduce print resolution or page size.");
        using var space = SKColorSpace.CreateSrgb();
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque, space));
        try
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Scale((float)(resolution / 72));
            DrawPage(canvas, picture, page, settings);
            canvas.Flush();
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
    }

    public static void ExportSeparations(SKPicture picture, Stream output, SvgPrintSettings settings,
        SvgIccCmykProfile? profile = null, string? svgSource = null)
    {
        var pages = SvgPrintLayoutService.CreatePages(picture.CullRect, settings);
        ValidateRasterJob(pages, settings);
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        var manifest = new XElement("unodraw-print-job", new XAttribute("version", 1),
            new XAttribute("dpi", settings.Dpi), new XAttribute("color", "CMYK"),
            new XAttribute("profile", profile?.Description ?? "Generic arithmetic CMYK"), XElement.Parse(settings.Serialize()));
        WriteProcessPlates(zip, picture, settings, pages, profile, manifest);
        AddEntry(zip, "print-job.xml", Encoding.UTF8.GetBytes(manifest.ToString()));
        if (profile is not null)
            AddEntry(zip, "output-profile.icc", profile.GetProfileBytes());
        if (svgSource is not null)
            AddEntry(zip, "artwork.svg", Encoding.UTF8.GetBytes(svgSource));
    }

    private static void WriteProcessPlates(ZipArchive zip, SKPicture picture, SvgPrintSettings settings,
        IReadOnlyList<SvgPrintPage> pages, SvgIccCmykProfile? profile, XElement manifest)
    {
        var names = new[] { "Cyan", "Magenta", "Yellow", "Black" };
        foreach (var page in pages)
        {
            using var bitmap = RenderPage(picture, page, settings);
            var cmyk = ConvertPixels(bitmap, page, settings, profile);
            manifest.Add(new XElement("page", new XAttribute("number", page.Number), new XAttribute("column", page.Column),
                new XAttribute("row", page.Row), new XAttribute("width", bitmap.Width), new XAttribute("height", bitmap.Height)));
            using var plate = new SKBitmap(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Gray8, SKAlphaType.Opaque));
            var rowBytes = plate.RowBytes;
            var gray = new byte[checked(rowBytes * plate.Height)];
            for (var channel = 0; channel < 4; channel++)
            {
                for (var y = 0; y < bitmap.Height; y++)
                    for (var x = 0; x < bitmap.Width; x++)
                        gray[y * rowBytes + x] = (byte)(255 - cmyk[(y * bitmap.Width + x) * 4 + channel]);
                System.Runtime.InteropServices.Marshal.Copy(gray, 0, plate.GetPixels(), gray.Length);
                using var image = SKImage.FromBitmap(plate);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                AddEntry(zip, $"page-{page.Number:000}-{names[channel]}.png", AddPngResolution(data.ToArray(), settings.Dpi));
            }
        }
    }

    private static void ValidateRasterJob(IReadOnlyList<SvgPrintPage> pages, SvgPrintSettings settings)
    {
        long total = 0;
        foreach (var page in pages)
        {
            var pixels = Math.Ceiling(page.MediaSize.Width * settings.Dpi / 72) * Math.Ceiling(page.MediaSize.Height * settings.Dpi / 72);
            if (pixels > MaximumPagePixels || (total += (long)pixels) > MaximumJobPixels)
                throw new InvalidOperationException("This raster print job exceeds the pixel limit. Reduce resolution, scale, or page count.");
        }
    }

    private static byte[] ConvertPixels(SKBitmap bitmap, SvgPrintPage page, SvgPrintSettings settings, SvgIccCmykProfile? profile)
    {
        var rgba = bitmap.Bytes;
        var result = new byte[checked(bitmap.Width * bitmap.Height * 4)];
        var cache = new Dictionary<uint, SvgCmykColor>();
        var unitsPerPixel = 72 / settings.Dpi;
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var at = y * bitmap.RowBytes + x * 4;
                var color = new SKColor(rgba[at], rgba[at + 1], rgba[at + 2], rgba[at + 3]);
                var key = (uint)color;
                if (!cache.TryGetValue(key, out var cmyk))
                {
                    cmyk = profile is not null ? profile.Convert(color) : SvgCmykColor.FromSrgb(color, settings.BlackGeneration, settings.TotalInkLimit);
                    if (cache.Count < 65536)
                        cache.Add(key, cmyk);
                }
                // Printer's marks are registration color: identical coverage on every plate.
                // Artwork conversion remains profile-controlled, including its black generation.
                if ((settings.CropMarks || settings.RegistrationMarks) && !page.BleedBox.Contains((float)((x + 0.5) * unitsPerPixel), (float)((y + 0.5) * unitsPerPixel)))
                {
                    var coverage = 1 - Math.Min(color.Red, Math.Min(color.Green, color.Blue)) / 255.0;
                    cmyk = new SvgCmykColor(coverage, coverage, coverage, coverage);
                }
                var destination = (y * bitmap.Width + x) * 4;
                for (var channel = 0; channel < 4; channel++)
                    result[destination + channel] = cmyk.Channel(channel);
            }
        return result;
    }

    private static void ExportCmykPdf(SKPicture picture, Stream output, SvgPrintSettings settings,
        IReadOnlyList<SvgPrintPage> pages, SvgIccCmykProfile? profile)
    {
        using var writer = new SvgPrintPdfWriter(output);
        var catalog = writer.Reserve();
        var pageTree = writer.Reserve();
        var profileId = profile is null ? 0 : writer.Reserve();
        var intentId = profile is null ? 0 : writer.Reserve();
        if (profile is not null)
        {
            writer.WriteStream(profileId, "/N 4 /Alternate /DeviceCMYK", profile.GetProfileBytes());
            writer.Write(intentId, "<< /Type /OutputIntent /S /GTS_PDFX /OutputConditionIdentifier " + SvgPrintPdfWriter.String(profile.Description) +
                $" /DestOutputProfile {profileId} 0 R >>");
        }
        var pageIds = new List<int>();
        foreach (var page in pages)
        {
            var pageId = writer.Reserve();
            var imageId = writer.Reserve();
            var contentId = writer.Reserve();
            pageIds.Add(pageId);
            using var bitmap = RenderPage(picture, page, settings);
            var samples = ConvertPixels(bitmap, page, settings, profile);
            var colorSpace = profileId == 0 ? "/DeviceCMYK" : $"[/ICCBased {profileId} 0 R]";
            writer.WriteStream(imageId, $"/Type /XObject /Subtype /Image /Width {bitmap.Width} /Height {bitmap.Height} /ColorSpace {colorSpace} /BitsPerComponent 8", samples);
            var content = FormattableString.Invariant($"q {page.MediaSize.Width:0.#####} 0 0 {page.MediaSize.Height:0.#####} 0 0 cm /Artwork Do Q\n");
            writer.WriteStream(contentId, string.Empty, Encoding.ASCII.GetBytes(content));
            writer.Write(pageId, FormattableString.Invariant($"<< /Type /Page /Parent {pageTree} 0 R /MediaBox [0 0 {page.MediaSize.Width:0.#####} {page.MediaSize.Height:0.#####}] ") +
                SvgPdfPageBoxes.BoxDictionary(page) + $" /Resources << /XObject << /Artwork {imageId} 0 R >> >> /Contents {contentId} 0 R >>");
        }
        writer.Write(pageTree, $"<< /Type /Pages /Count {pageIds.Count} /Kids [{string.Join(" ", pageIds.Select(id => id + " 0 R"))}] >>");
        writer.Write(catalog, $"<< /Type /Catalog /Pages {pageTree} 0 R" + (intentId == 0 ? string.Empty : $" /OutputIntents [{intentId} 0 R]") + " >>");
        writer.Complete(catalog);
    }

    private static void DrawMarks(SKCanvas canvas, SvgPrintPage page, SvgPrintSettings settings)
    {
        using var paint = new SKPaint { Color = SKColors.Black, StrokeWidth = 0.25f, IsAntialias = true, Style = SKPaintStyle.Stroke };
        var gap = (float)(2 * SvgPrintLayoutService.PointsPerMillimeter);
        var length = (float)(5 * SvgPrintLayoutService.PointsPerMillimeter);
        if (settings.CropMarks)
        {
            foreach (var x in new[] { page.TrimBox.Left, page.TrimBox.Right })
            {
                canvas.DrawLine(x, page.BleedBox.Top - gap, x, page.BleedBox.Top - gap - length, paint);
                canvas.DrawLine(x, page.BleedBox.Bottom + gap, x, page.BleedBox.Bottom + gap + length, paint);
            }
            foreach (var y in new[] { page.TrimBox.Top, page.TrimBox.Bottom })
            {
                canvas.DrawLine(page.BleedBox.Left - gap, y, page.BleedBox.Left - gap - length, y, paint);
                canvas.DrawLine(page.BleedBox.Right + gap, y, page.BleedBox.Right + gap + length, y, paint);
            }
        }
        if (!settings.RegistrationMarks)
            return;
        foreach (var point in new[] { new SKPoint(page.TrimBox.MidX, page.BleedBox.Top - gap - length / 2),
            new SKPoint(page.TrimBox.MidX, page.BleedBox.Bottom + gap + length / 2),
            new SKPoint(page.BleedBox.Left - gap - length / 2, page.TrimBox.MidY),
            new SKPoint(page.BleedBox.Right + gap + length / 2, page.TrimBox.MidY) })
        {
            var radius = length / 3;
            canvas.DrawCircle(point, radius, paint);
            canvas.DrawLine(point.X - radius * 1.4f, point.Y, point.X + radius * 1.4f, point.Y, paint);
            canvas.DrawLine(point.X, point.Y - radius * 1.4f, point.X, point.Y + radius * 1.4f, paint);
        }
    }

    private static void AddEntry(ZipArchive archive, string name, byte[] bytes)
    {
        using var stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
        stream.Write(bytes, 0, bytes.Length);
    }

    private static byte[] AddPngResolution(byte[] png, double dpi)
    {
        if (png.Length < 33)
            throw new InvalidDataException("PNG encoder produced a truncated image.");
        var chunk = new byte[21];
        void U32(int at, uint value)
        { chunk[at] = (byte)(value >> 24); chunk[at + 1] = (byte)(value >> 16); chunk[at + 2] = (byte)(value >> 8); chunk[at + 3] = (byte)value; }
        U32(0, 9);
        Encoding.ASCII.GetBytes("pHYs").CopyTo(chunk, 4);
        var ppm = (uint)Math.Round(dpi / 0.0254);
        U32(8, ppm);
        U32(12, ppm);
        chunk[16] = 1;
        uint crc = 0xffffffff;
        for (var i = 4; i < 17; i++)
        {
            crc ^= chunk[i];
            for (var bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320 : 0);
        }
        U32(17, crc ^ 0xffffffff);
        var result = new byte[png.Length + chunk.Length];
        Buffer.BlockCopy(png, 0, result, 0, 33);
        Buffer.BlockCopy(chunk, 0, result, 33, chunk.Length);
        Buffer.BlockCopy(png, 33, result, 33 + chunk.Length, png.Length - 33);
        return result;
    }
}

internal sealed class SvgPrintPdfWriter : IDisposable
{
    private readonly Stream _stream;
    private readonly List<long> _offsets = new() { 0 };
    private long _position;
    public SvgPrintPdfWriter(Stream stream) { _stream = stream; Raw("%PDF-1.7\n%\u00e2\u00e3\u00cf\u00d3\n"); }
    public int Reserve() { _offsets.Add(-1); return _offsets.Count - 1; }
    public void Write(int id, string content)
    {
        _offsets[id] = _position;
        Raw($"{id} 0 obj\n{content}\nendobj\n");
    }
    public void WriteStream(int id, string dictionary, byte[] data)
    {
        var compressed = Compress(data);
        _offsets[id] = _position;
        Raw($"{id} 0 obj\n<< {dictionary} /Filter /FlateDecode /Length {compressed.Length} >>\nstream\n");
        _stream.Write(compressed, 0, compressed.Length);
        _position += compressed.Length;
        Raw("\nendstream\nendobj\n");
    }
    public void Complete(int root)
    {
        if (_offsets.Skip(1).Any(offset => offset < 0))
            throw new InvalidOperationException("A PDF object was not written.");
        var xref = _position;
        Raw($"xref\n0 {_offsets.Count}\n0000000000 65535 f \n");
        foreach (var offset in _offsets.Skip(1))
            Raw(offset.ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
        Raw($"trailer\n<< /Size {_offsets.Count} /Root {root} 0 R >>\nstartxref\n{xref.ToString(CultureInfo.InvariantCulture)}\n%%EOF\n");
        _stream.Flush();
    }
    private void Raw(string value)
    {
        var bytes = Encoding.GetEncoding(28591).GetBytes(value);
        _stream.Write(bytes, 0, bytes.Length);
        _position += bytes.Length;
    }
    public static string String(string text) => "<FEFF" + string.Concat(Encoding.BigEndianUnicode.GetBytes(text).Select(value => value.ToString("X2", CultureInfo.InvariantCulture))) + ">";
    private static byte[] Compress(byte[] data)
    {
        using var buffer = new MemoryStream();
        buffer.WriteByte(0x78);
        buffer.WriteByte(0x9c);
        using (var deflate = new DeflateStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(data, 0, data.Length);
        uint a = 1, b = 0;
        for (var at = 0; at < data.Length;)
        {
            var end = Math.Min(data.Length, at + 5552);
            while (at < end)
            { a += data[at++]; b += a; }
            a %= 65521;
            b %= 65521;
        }
        var checksum = b << 16 | a;
        for (var shift = 24; shift >= 0; shift -= 8)
            buffer.WriteByte((byte)(checksum >> shift));
        return buffer.ToArray();
    }
    public void Dispose() { }
}
