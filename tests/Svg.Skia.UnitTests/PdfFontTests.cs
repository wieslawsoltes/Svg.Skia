using System.IO;
using SkiaSharp;
using Svg.Skia.UnitTests.Common;
using Xunit;

namespace Svg.Skia.UnitTests;

public class PdfFontTests : SvgUnitTest
{
    private static string GetSvgPath(string name)
        => Path.Combine("..", "..", "..", "..", "Tests", name);

    private static string GetPdfPath(string name)
        => Path.Combine("..", "..", "..", "..", "Tests", name);

    [WindowsTheory]
    [InlineData("EmbeddedFontText.svg", "EmbeddedFontText.pdf")]
    public void ConvertToPdf(string svgName, string pdfName)
    {
        var svgPath = GetSvgPath(svgName);
        var pdfPath = GetPdfPath(pdfName);

        var svg = new SKSvg();
        using var _ = svg.Load(svgPath);
        var picture = svg.Picture;
        Assert.NotNull(picture);

        using var stream = File.OpenWrite(pdfPath);
        picture!.ToPdf(stream, SKColors.White, 1f, 1f);
        stream.Flush();

        Assert.True(File.Exists(pdfPath));
        var length = new FileInfo(pdfPath).Length;
        Assert.True(length > 0);

        File.Delete(pdfPath);
    }
}
