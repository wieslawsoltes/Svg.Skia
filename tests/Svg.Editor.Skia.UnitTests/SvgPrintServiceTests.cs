using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using SkiaSharp;
using Svg.Editor.Skia;
using Svg.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgPrintServiceTests
{
    [Fact]
    public void ActualSizeUsesPhysicalSvgUnitsAndIncludesBleedAndMarks()
    {
        var settings = new SvgPrintSettings { Layout = SvgPrintLayout.ActualSize, WidthMillimeters = 100, HeightMillimeters = 80, BleedMillimeters = 3, CropMarks = true };
        var page = Assert.Single(SvgPrintLayoutService.CreatePages(SKRect.Create(96, 96), settings));
        Assert.Equal(0.75, page.ArtworkTransform.ScaleX, 5);
        Assert.Equal(122 * 72 / 25.4, page.MediaSize.Width, 3);
        Assert.Equal(100 * 72 / 25.4, page.TrimBox.Width, 3);
        Assert.Equal(106 * 72 / 25.4, page.BleedBox.Width, 3);
        Assert.Equal(page.TrimBox.MidX, page.ArtworkTransform.MapPoint(new SKPoint(48, 48)).X, 3);
    }

    [Fact]
    public void FitHonorsMarginsAndNonzeroArtworkOrigin()
    {
        var settings = new SvgPrintSettings { WidthMillimeters = 100, HeightMillimeters = 100, MarginMillimeters = 10, BleedMillimeters = 0, CropMarks = false };
        var page = Assert.Single(SvgPrintLayoutService.CreatePages(SKRect.Create(50, 80, 200, 100), settings));
        var left = page.ArtworkTransform.MapPoint(new SKPoint(50, 80));
        var right = page.ArtworkTransform.MapPoint(new SKPoint(250, 180));
        Assert.Equal(10 * 72 / 25.4, left.X, 3);
        Assert.Equal(90 * 72 / 25.4, right.X, 3);
        Assert.Equal(30 * 72 / 25.4, left.Y, 3);
        Assert.Equal(70 * 72 / 25.4, right.Y, 3);
    }

    [Fact]
    public void TilingUsesExplicitOverlapAndRejectsUnboundedJobs()
    {
        var settings = new SvgPrintSettings { Layout = SvgPrintLayout.Tile, WidthMillimeters = 100, HeightMillimeters = 100, MarginMillimeters = 0, TileOverlapMillimeters = 10, BleedMillimeters = 0, CropMarks = false };
        var pages = SvgPrintLayoutService.CreatePages(SKRect.Create(800, 800), settings);
        Assert.Equal(9, pages.Count);
        Assert.Equal(90 * 72 / 25.4, pages[0].ArtworkTransform.TransX - pages[1].ArtworkTransform.TransX, 3);
        Assert.Equal(2, pages[8].Row);
        Assert.Throws<InvalidOperationException>(() => SvgPrintLayoutService.CreatePages(SKRect.Create(100000, 100000), settings));
        Assert.Throws<InvalidOperationException>(() => SvgPrintLayoutService.CreatePages(SKRect.Create(float.MaxValue, 10), settings));
        Assert.Throws<ArgumentException>(() => SvgPrintLayoutService.CreatePages(new SKRect(float.NaN, 0, 10, 10), settings));
    }

    [Fact]
    public void PrintSettingsRoundTripInSvgAndUndoAsOneEdit()
    {
        using var editor = new SvgEditorController();
        editor.NewDocument();
        var before = editor.GetSvg();
        var settings = new SvgPrintSettings { WidthMillimeters = 320, HeightMillimeters = 180, RegistrationMarks = true, ColorMode = SvgPrintColorMode.Cmyk, Scale = 1.25 };
        settings.Apply(editor);
        using var roundTrip = new SvgEditorController();
        roundTrip.LoadSvg(editor.GetSvg());
        var loaded = SvgPrintSettings.FromDocument(roundTrip.Document!);
        Assert.Equal(settings.Serialize(), loaded.Serialize());
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void VectorPdfContainsPageBoxesAndRetainsVectorArtwork()
    {
        using var renderer = Svg();
        using var output = new MemoryStream();
        SvgPrintService.ExportPdf(renderer.Picture!, output, SmallSettings());
        var text = Encoding.GetEncoding(28591).GetString(output.ToArray());
        Assert.StartsWith("%PDF-", text);
        Assert.Contains("/TrimBox", text);
        Assert.Contains("/BleedBox", text);
        Assert.Contains("/Prev", text);
        Assert.DoesNotContain("/Subtype /Image", text);
        Assert.EndsWith("%%EOF\n", text);
    }

    [Fact]
    public void CmykPdfUsesFourChannelImageAndEmbedsSelectedProfile()
    {
        using var renderer = Svg();
        using var output = new MemoryStream();
        var settings = SmallSettings();
        settings.ColorMode = SvgPrintColorMode.Cmyk;
        var profile = new SvgIccCmykProfile(IccProfile("mft2"));
        SvgPrintService.ExportPdf(renderer.Picture!, output, settings, profile);
        var text = Encoding.GetEncoding(28591).GetString(output.ToArray());
        Assert.Contains("/ICCBased", text);
        Assert.Contains("/N 4 /Alternate /DeviceCMYK", text);
        Assert.Contains("/OutputIntents", text);
        Assert.Contains("/TrimBox", text);
        Assert.Contains("/Subtype /Image", text);
    }

    [Fact]
    public void SeparationsEncodeActualInkCoverageAndResolution()
    {
        using var renderer = Svg();
        using var output = new MemoryStream();
        var settings = SmallSettings();
        SvgPrintService.ExportSeparations(renderer.Picture!, output, settings, svgSource: "<svg/>");
        output.Position = 0;
        using var zip = new ZipArchive(output, ZipArchiveMode.Read);
        Assert.Equal(6, zip.Entries.Count);
        foreach (var (channel, expected) in new[] { ("Cyan", (byte)255), ("Magenta", (byte)0), ("Yellow", (byte)0), ("Black", (byte)255) })
        {
            using var stream = zip.GetEntry("page-001-" + channel + ".png")!.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray();
            Assert.Contains("pHYs", Encoding.GetEncoding(28591).GetString(bytes));
            using var bitmap = SKBitmap.Decode(bytes);
            var center = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
            Assert.Equal(expected, center.Red);
            Assert.Equal(center.Red, center.Green);
        }
    }

    [Fact]
    public void RegistrationMarksPrintOnAllFourPlates()
    {
        using var renderer = Svg();
        using var output = new MemoryStream();
        var settings = SmallSettings();
        settings.CropMarks = true;
        settings.RegistrationMarks = true;
        SvgPrintService.ExportSeparations(renderer.Picture!, output, settings);
        output.Position = 0;
        using var zip = new ZipArchive(output, ZipArchiveMode.Read);
        foreach (var channel in new[] { "Cyan", "Magenta", "Yellow", "Black" })
        {
            using var stream = zip.GetEntry("page-001-" + channel + ".png")!.Open();
            using var bitmap = SKBitmap.Decode(stream);
            var found = false;
            for (var y = 0; y < 12; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    found |= bitmap.GetPixel(x, y).Red < 220;
            Assert.True(found, channel + " has no registration or crop marks.");
        }
    }

    [Fact]
    public void NamedInkPackageSeparatesCoverageAndKnocksOutProcessInk()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='96' height='96'><defs><linearGradient id='ink' data-unodraw-spot-name='../Gold'><stop stop-color='#D9A520'/><stop offset='1' stop-color='#D9A520'/></linearGradient></defs><rect width='96' height='96' fill='red'/><rect x='24' y='24' width='48' height='48' fill='url(#ink)'/></svg>");
        var before = editor.GetSvg();
        using var output = new MemoryStream();
        SvgPrintService.ExportSpotSeparations(editor.Document!, output, SmallSettings());
        Assert.Equal(before, editor.GetSvg());
        output.Position = 0;
        using var zip = new ZipArchive(output, ZipArchiveMode.Read);
        using var inkStream = zip.GetEntry("page-001-Spot-001.png")!.Open();
        using var ink = SKBitmap.Decode(inkStream);
        Assert.Equal((byte)0, ink.GetPixel(36, 36).Red);
        Assert.Equal((byte)255, ink.GetPixel(4, 4).Red);
        foreach (var channel in new[] { "Cyan", "Magenta", "Yellow", "Black" })
        {
            using var stream = zip.GetEntry("page-001-" + channel + ".png")!.Open();
            using var bitmap = SKBitmap.Decode(stream);
            Assert.Equal((byte)255, bitmap.GetPixel(36, 36).Red);
        }
        using var manifestStream = zip.GetEntry("print-job.xml")!.Open();
        using var reader = new StreamReader(manifestStream);
        Assert.Contains("../Gold", reader.ReadToEnd());
        Assert.All(zip.Entries, entry => Assert.DoesNotContain("..", entry.FullName));
    }

    [Theory]
    [InlineData("mft1")]
    [InlineData("mft2")]
    [InlineData("mBA ")]
    public void IccLutPipelinesInterpolatePcsAndHandleNeutralEndpoints(string type)
    {
        var profile = new SvgIccCmykProfile(IccProfile(type));
        Assert.Equal("B2A0", profile.TransformTag);
        var white = profile.Convert(SKColors.White);
        var black = profile.Convert(SKColors.Black);
        Assert.InRange(white.Cyan, 0.995, 1);
        Assert.InRange(white.Black, 0, 0.005);
        Assert.InRange(black.Cyan, 0, 0.001);
        Assert.InRange(black.Black, 0.999, 1);
        Assert.InRange(white.Magenta, 0.49, 0.51);
        var gray = profile.Convert(new SKColor(128, 128, 128));
        Assert.InRange(gray.Cyan, 0.53, 0.54);
        Assert.InRange(gray.Black, 0.46, 0.47);
    }

    [Fact]
    public void IccMalformedOffsetsAndUntrustedSizesAreRejected()
    {
        var bytes = IccProfile("mft2");
        bytes[139] = 1;
        Assert.Throws<InvalidDataException>(() => new SvgIccCmykProfile(bytes));
        bytes = IccProfile("mft2");
        bytes[138] = 255;
        Assert.Throws<InvalidDataException>(() => new SvgIccCmykProfile(bytes));
        Assert.Throws<InvalidDataException>(() => new SvgIccCmykProfile(new byte[20]));
    }

    [Fact]
    public void GenericSeparationPreservesPureBlackAndHonorsInkLimit()
    {
        var black = SvgCmykColor.FromSrgb(SKColors.Black);
        Assert.Equal(0, black.Cyan);
        Assert.Equal(1, black.Black);
        var rich = SvgCmykColor.FromSrgb(new SKColor(15, 20, 25), 0.4, 1.8);
        Assert.InRange(rich.TotalInk, 0, 1.800001);
        var transparent = SvgCmykColor.FromSrgb(SKColors.Transparent);
        Assert.Equal(0, transparent.TotalInk);
    }

    [Fact]
    public void PreflightFindsBrokenResourcesAnimationAndUnprofiledCmyk()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><rect id='r' width='100' height='100' fill='url(#missing)'><animate attributeName='opacity' values='0;1' dur='1s'/></rect></svg>");
        var issues = SvgPreflightService.Inspect(editor, new SvgPrintSettings { ColorMode = SvgPrintColorMode.Cmyk });
        Assert.Contains(issues, issue => issue.Code == "missing-resource" && issue.Severity == SvgPreflightSeverity.Error);
        Assert.Contains(issues, issue => issue.Code == "animated-content");
        Assert.Contains(issues, issue => issue.Code == "unprofiled-cmyk");
    }

    private static SKSvg Svg()
    {
        var renderer = new SKSvg();
        renderer.FromSvg("<svg xmlns='http://www.w3.org/2000/svg' width='96' height='96'><rect width='96' height='96' fill='red'/></svg>");
        return renderer;
    }
    private static SvgPrintSettings SmallSettings() => new()
    { WidthMillimeters = 25.4, HeightMillimeters = 25.4, MarginMillimeters = 0, BleedMillimeters = 0, CropMarks = false, Dpi = 72 };

    private static byte[] IccProfile(string type)
    {
        var sixteen = type != "mft1";
        var header = type == "mBA " ? 68 : sixteen ? 52 : 48;
        var tableEntries = sixteen ? 2 : 256;
        var inputBytes = type == "mBA " ? 20 : 3 * tableEntries * (sixteen ? 2 : 1);
        var outputBytes = type == "mBA " ? 0 : 4 * tableEntries * (sixteen ? 2 : 1);
        var tagLength = header + inputBytes + 32 * (sixteen ? 2 : 1) + outputBytes;
        var bytes = new byte[144 + tagLength];
        void U32(int at, uint value)
        { bytes[at] = (byte)(value >> 24); bytes[at + 1] = (byte)(value >> 16); bytes[at + 2] = (byte)(value >> 8); bytes[at + 3] = (byte)value; }
        void U16(int at, int value)
        { bytes[at] = (byte)(value >> 8); bytes[at + 1] = (byte)value; }
        void Sig(int at, string value) => Encoding.ASCII.GetBytes(value).CopyTo(bytes, at);
        U32(0, (uint)bytes.Length);
        bytes[8] = 4;
        Sig(12, "prtr");
        Sig(16, "CMYK");
        Sig(20, "Lab ");
        Sig(36, "acsp");
        U32(128, 1);
        Sig(132, "B2A0");
        U32(136, 144);
        U32(140, (uint)tagLength);
        const int start = 144;
        Sig(start, type);
        bytes[start + 8] = 3;
        bytes[start + 9] = 4;
        var at = start + header;
        if (type == "mBA ")
        {
            U32(start + 12, 32);
            U32(start + 24, 68);
            for (var i = 0; i < 3; i++)
                Sig(start + 32 + i * 12, "curv");
            bytes[at] = bytes[at + 1] = bytes[at + 2] = 2;
            bytes[at + 16] = 2;
            at += 20;
        }
        else
        {
            bytes[start + 10] = 2;
            U32(start + 12, 65536);
            U32(start + 28, 65536);
            U32(start + 44, 65536);
            if (sixteen)
            { U16(start + 48, tableEntries); U16(start + 50, tableEntries); }
            for (var channel = 0; channel < 3; channel++)
                for (var i = 0; i < tableEntries; i++)
                { if (sixteen) { U16(at, i * 65535); at += 2; } else bytes[at++] = (byte)i; }
        }
        for (var x = 0; x < 2; x++)
            for (var y = 0; y < 2; y++)
                for (var z = 0; z < 2; z++)
                    foreach (var value in new[] { x, y, z, 1 - x })
                    { if (sixteen) { U16(at, value * 65535); at += 2; } else bytes[at++] = (byte)(value * 255); }
        if (type != "mBA ")
            for (var channel = 0; channel < 4; channel++)
                for (var i = 0; i < tableEntries; i++)
                { if (sixteen) { U16(at, i * 65535); at += 2; } else bytes[at++] = (byte)i; }
        return bytes;
    }
}
