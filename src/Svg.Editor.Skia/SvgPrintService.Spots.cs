using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Skia;

namespace Svg.Editor.Skia;

public static partial class SvgPrintService
{
    /// <summary>Exports CMYK process plates and separate named-ink plates with source-over knockouts.</summary>
    public static void ExportSpotSeparations(SvgDocument document, Stream output, SvgPrintSettings settings,
        SvgIccCmykProfile? profile = null)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));
        if (output is null)
            throw new ArgumentNullException(nameof(output));
        var analysis = SvgSpotSeparationService.Analyze(document);
        if (!analysis.CanSeparate)
            throw new InvalidOperationException(string.Join(Environment.NewLine, analysis.Issues.Select(issue => issue.Message)));
        if (analysis.Inks.Count > 32)
            throw new InvalidOperationException("A separation package supports up to 32 named inks. Split this job into smaller documents.");
        using var process = new SKSvg();
        process.FromSvgDocument(SvgSpotSeparationService.CreateProcessDocument(document));
        var picture = process.Picture ?? throw new InvalidOperationException("The document contains no printable artwork.");
        var pages = SvgPrintLayoutService.CreatePages(picture.CullRect, settings);
        ValidateRasterJob(pages, settings);
        var totalPixels = pages.Sum(page => Math.Ceiling(page.MediaSize.Width * settings.Dpi / 72)
            * Math.Ceiling(page.MediaSize.Height * settings.Dpi / 72));
        if (totalPixels * (4 + analysis.Inks.Count) > MaximumJobPixels * 4)
            throw new InvalidOperationException("The named-ink separation job exceeds the plate limit. Reduce resolution or page count.");
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        var manifest = new XElement("unodraw-print-job", new XAttribute("version", 1),
            new XAttribute("dpi", settings.Dpi), new XAttribute("color", "CMYK+spot"),
            new XAttribute("compositing", "source-over-knockout"),
            new XAttribute("profile", profile?.Description ?? "Generic arithmetic CMYK"), XElement.Parse(settings.Serialize()));
        WriteProcessPlates(zip, picture, settings, pages, profile, manifest);
        for (var index = 0; index < analysis.Inks.Count; index++)
        {
            var ink = analysis.Inks[index];
            var inkEntry = new XElement("spot-ink", new XAttribute("number", index + 1), new XAttribute("name", ink.Name),
                new XAttribute("alternate-rgb", $"#{ink.AlternateColor.Red:X2}{ink.AlternateColor.Green:X2}{ink.AlternateColor.Blue:X2}"),
                ink.ResourceIds.Select(id => new XElement("resource", new XAttribute("id", id))));
            manifest.Add(inkEntry);
            using var renderer = new SKSvg();
            renderer.FromSvgDocument(SvgSpotSeparationService.CreatePlateDocument(document, ink.Name));
            var inkPicture = renderer.Picture ?? throw new InvalidOperationException("The named ink could not be rendered: " + ink.Name);
            foreach (var page in pages)
            {
                using var bitmap = RenderPage(inkPicture, page, settings);
                using var gray = new SKBitmap(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Gray8, SKAlphaType.Opaque));
                var source = bitmap.Bytes;
                var pixels = new byte[checked(gray.RowBytes * gray.Height)];
                for (var y = 0; y < bitmap.Height; y++)
                    for (var x = 0; x < bitmap.Width; x++)
                        pixels[y * gray.RowBytes + x] = source[y * bitmap.RowBytes + x * 4];
                Marshal.Copy(pixels, 0, gray.GetPixels(), pixels.Length);
                using var image = SKImage.FromBitmap(gray);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                // Filenames are ordinal; ink names remain Unicode in the manifest and cannot become paths.
                var file = $"page-{page.Number:000}-Spot-{index + 1:000}.png";
                AddEntry(zip, file, AddPngResolution(data.ToArray(), settings.Dpi));
                inkEntry.Add(new XElement("plate", new XAttribute("page", page.Number), new XAttribute("file", file)));
            }
        }
        AddEntry(zip, "print-job.xml", Encoding.UTF8.GetBytes(manifest.ToString()));
        AddEntry(zip, "artwork.svg", Encoding.UTF8.GetBytes(document.GetXML()));
        if (profile is not null)
            AddEntry(zip, "output-profile.icc", profile.GetProfileBytes());
    }
}
