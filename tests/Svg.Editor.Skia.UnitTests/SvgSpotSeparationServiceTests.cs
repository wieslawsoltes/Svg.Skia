using System;
using System.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Svg.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgSpotSeparationServiceTests
{
    [Fact]
    public void NamedInkMetadataIsImmutableAndRetainsAlternateColorsAndAliases()
    {
        var document = Load("<rect width='100' height='40' fill='url(#ink)'/>");
        var analysis = SvgSpotSeparationService.Analyze(document);
        Assert.True(analysis.CanSeparate);
        var ink = Assert.Single(analysis.Inks);
        Assert.Equal("Warm Gold", ink.Name);
        Assert.Equal("ink", Assert.Single(ink.ResourceIds));
        Assert.Equal(new SKColor(255, 204, 0), ink.AlternateColor);
    }

    [Fact]
    public void SpotPaintKnocksOutProcessAndOrdinaryShapesKnockOutTheSpotPlate()
    {
        var document = Load("<rect width='100' height='40' fill='blue'/><rect x='20' width='60' height='40' fill='url(#ink)'/><rect x='50' width='20' height='40' fill='red'/>");
        var before = document.GetXML();
        using var process = Render(SvgSpotSeparationService.CreateProcessDocument(document));
        using var plate = Render(SvgSpotSeparationService.CreatePlateDocument(document, "Warm Gold"));
        Assert.Equal(SKColors.Blue, process.GetPixel(10, 20));
        Assert.Equal(SKColors.White, process.GetPixel(30, 20));
        Assert.Equal(SKColors.Red, process.GetPixel(60, 20));
        Assert.Equal(SKColors.White, plate.GetPixel(10, 20));
        Assert.Equal(SKColors.Black, plate.GetPixel(30, 20));
        Assert.Equal(SKColors.White, plate.GetPixel(60, 20));
        Assert.Equal(before, document.GetXML());
    }

    [Fact]
    public void InheritedPaintAndAuthorImportantStylesAreSeparated()
    {
        var document = Load("<style>.spot { fill:url(#ink)!important; }</style><g class='spot' opacity='.5' transform='translate(10 0)'><rect width='40' height='40'/></g>");
        var derived = SvgSpotSeparationService.CreatePlateDocument(document, "Warm Gold");
        using var plate = Render(SvgDocument.FromSvg<SvgDocument>(derived.GetXML()));
        var pixel = plate.GetPixel(25, 20);
        Assert.InRange(pixel.Red, (byte)126, (byte)129);
        Assert.Equal(pixel.Red, pixel.Green);
        Assert.Equal(pixel.Red, pixel.Blue);
        Assert.Equal(SKColors.White, plate.GetPixel(5, 20));
    }

    [Fact]
    public void GlobalImportantStopStylesCannotRecolorGeneratedPlates()
    {
        var document = Load("<style>stop { stop-color:#ffcc00!important;stop-opacity:.5!important; }</style><rect width='100' height='40' fill='url(#ink)'/>");
        var plate = SvgSpotSeparationService.CreatePlateDocument(document, "Warm Gold");
        var reopened = SvgDocument.FromSvg<SvgDocument>(plate.GetXML());
        using var rendered = Render(reopened);
        var pixel = rendered.GetPixel(50, 20);
        Assert.InRange(pixel.Red, (byte)126, (byte)129);
        Assert.Equal(pixel.Red, pixel.Green);
        Assert.Equal(pixel.Red, pixel.Blue);
        using var process = Render(SvgSpotSeparationService.CreateProcessDocument(document));
        Assert.Equal(SKColors.White, process.GetPixel(50, 20));
    }

    [Fact]
    public void TransparentProcessColorsKeepTheirKnockoutOpacityAfterSaving()
    {
        var document = Load("<rect width='100' height='40' fill='url(#ink)'/><rect width='50' height='40' fill='rgba(255,0,0,.5)'/>");
        var derived = SvgSpotSeparationService.CreatePlateDocument(document, "Warm Gold");
        using var plate = Render(SvgDocument.FromSvg<SvgDocument>(derived.GetXML()));
        Assert.InRange(plate.GetPixel(25, 20).Red, (byte)126, (byte)129);
        Assert.Equal(SKColors.Black, plate.GetPixel(75, 20));
    }

    [Fact]
    public void DerivedSpotGradientPreservesItsTintGeometryAndUniqueResourceIds()
    {
        var document = Load("<defs><linearGradient id='fade' href='#ink' gradientUnits='userSpaceOnUse' x1='0' x2='100'><title id='gradient-title'>Tint</title><stop offset='0' stop-color='#ffcc00' stop-opacity='0'/><stop offset='1' stop-color='#ffcc00'/></linearGradient></defs><rect width='100' height='40' fill='url(#fade)'/>");
        var plate = SvgSpotSeparationService.CreatePlateDocument(document, "Warm Gold");
        var ids = plate.Descendants().Select(element => element.ID).Where(id => !string.IsNullOrEmpty(id)).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        using var rendered = Render(plate);
        Assert.InRange(rendered.GetPixel(25, 20).Red, (byte)187, (byte)195);
        Assert.InRange(rendered.GetPixel(75, 20).Red, (byte)60, (byte)69);
    }

    [Fact]
    public void SpotTintsRetainStopFillAndGroupOpacity()
    {
        var document = Load("<g opacity='.5'><rect width='100' height='40' fill='url(#ink)' fill-opacity='.5'/></g>", stopOpacity: 0.5);
        using var plate = Render(SvgSpotSeparationService.CreatePlateDocument(document, "Warm Gold"));
        Assert.InRange(plate.GetPixel(50, 20).Red, (byte)222, (byte)225);
    }

    [Fact]
    public void SharedMaskPaintIsNeverRecoloredAlongWithTheArtwork()
    {
        var document = SvgDocument.FromSvg<SvgDocument>("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="40">
            <defs><linearGradient id="ink" data-unodraw-spot-name="White ink"><stop offset="0" stop-color="white"/><stop offset="1" stop-color="white"/></linearGradient>
            <mask id="mask" maskUnits="userSpaceOnUse" x="0" y="0" width="100" height="40"><rect width="50" height="40" fill="url(#ink)"/></mask>
            <clipPath id="clip"><rect width="35" height="40"/></clipPath></defs>
            <rect width="100" height="40" fill="url(#ink)" mask="url(#mask)" clip-path="url(#clip)"/>
            </svg>
            """);
        using var plate = Render(SvgSpotSeparationService.CreatePlateDocument(document, "White ink"));
        Assert.Equal(SKColors.Black, plate.GetPixel(20, 20));
        Assert.Equal(SKColors.White, plate.GetPixel(45, 20));
        Assert.Equal(SKColors.White, plate.GetPixel(75, 20));
    }

    [Fact]
    public void ProcessGradientAlphaProducesContinuousPlateKnockout()
    {
        var document = Load("<defs><linearGradient id='fade'><stop offset='0' stop-color='red' stop-opacity='0'/><stop offset='1' stop-color='blue'/></linearGradient></defs><rect width='100' height='40' fill='url(#ink)'/><rect width='100' height='40' fill='url(#fade)'/>");
        using var plate = Render(SvgSpotSeparationService.CreatePlateDocument(document, "Warm Gold"));
        Assert.InRange(plate.GetPixel(25, 20).Red, (byte)61, (byte)69);
        Assert.InRange(plate.GetPixel(75, 20).Red, (byte)188, (byte)197);
    }

    [Fact]
    public void SpotStrokeAndOtherNamedInksRespectPaintOrder()
    {
        var document = Load("<defs><linearGradient id='other' data-unodraw-spot-name='Blue ink'><stop offset='0' stop-color='blue'/><stop offset='1' stop-color='blue'/></linearGradient></defs><path d='M0 20 L100 20' fill='none' stroke='url(#ink)' stroke-width='20'/><rect x='40' width='30' height='40' fill='url(#other)'/>");
        using var original = Render(document);
        Assert.Equal(new SKColor(255, 204, 0), original.GetPixel(20, 20));
        using var gold = Render(SvgSpotSeparationService.CreatePlateDocument(document, "Warm Gold"));
        using var blue = Render(SvgSpotSeparationService.CreatePlateDocument(document, "Blue ink"));
        Assert.Equal(SKColors.Black, gold.GetPixel(20, 20));
        Assert.Equal(SKColors.White, gold.GetPixel(50, 20));
        Assert.Equal(SKColors.White, blue.GetPixel(20, 20));
        Assert.Equal(SKColors.Black, blue.GetPixel(50, 20));
    }

    [Theory]
    [InlineData("<image href='data:image/png;base64,AA==' width='10' height='10'/>", "spot-image")]
    [InlineData("<use href='#shape'/>", "spot-instance")]
    [InlineData("<rect width='20' height='20' filter='url(#filter)'/>", "spot-filter")]
    [InlineData("<rect width='20' height='20' style='mix-blend-mode:multiply'/>", "spot-blend-mode")]
    [InlineData("<rect width='20' height='20'><animate attributeName='x' to='5' dur='1s'/></rect>", "spot-dynamic-content")]
    [InlineData("<defs><pattern id='pattern' width='10' height='10' patternUnits='userSpaceOnUse'><rect width='10' height='10' fill='url(#ink)'/></pattern></defs><rect width='20' height='20' fill='url(#pattern)'/>", "spot-paint-server")]
    [InlineData("<path d='M0 0L20 20' marker-end='url(#arrow)'/>", "spot-marker")]
    public void UnsupportedCompositingReportsExplicitDiagnosticsBeforeExport(string artwork, string code)
    {
        var document = Load(artwork);
        var analysis = SvgSpotSeparationService.Analyze(document);
        Assert.False(analysis.CanSeparate);
        Assert.Contains(analysis.Issues, issue => issue.Code == code);
        Assert.Throws<InvalidOperationException>(() => SvgSpotSeparationService.CreatePlateDocument(document, "Warm Gold"));
    }

    [Fact]
    public void ConflictingInkColorsAndUnknownInkNamesAreRejected()
    {
        var document = Load("<defs><linearGradient id='alias' data-unodraw-spot-name='Warm Gold'><stop offset='0' stop-color='blue'/></linearGradient></defs>");
        Assert.Contains(SvgSpotSeparationService.Analyze(document).Issues, issue => issue.Code == "spot-conflicting-alternate");
        Assert.Throws<ArgumentException>(() => SvgSpotSeparationService.CreatePlateDocument(Load(""), "Missing ink"));
    }

    [Fact]
    public void DocumentsWithoutNamedInksKeepTheOrdinaryProcessPipeline()
    {
        var document = SvgDocument.FromSvg<SvgDocument>("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='40'><rect width='100' height='40' fill='red'/></svg>");
        Assert.Empty(SvgSpotSeparationService.Analyze(document).Inks);
        using var process = Render(SvgSpotSeparationService.CreateProcessDocument(document));
        Assert.Equal(SKColors.Red, process.GetPixel(50, 20));
    }

    private static SvgDocument Load(string artwork, double stopOpacity = 1)
        => SvgDocument.FromSvg<SvgDocument>(FormattableString.Invariant($"<svg xmlns='http://www.w3.org/2000/svg' width='100' height='40'><defs><linearGradient id='ink' data-unodraw-spot-name='Warm Gold' gradientUnits='userSpaceOnUse' x2='100'><stop offset='0' stop-color='#ffcc00' stop-opacity='{stopOpacity}'/><stop offset='1' stop-color='#ffcc00' stop-opacity='{stopOpacity}'/></linearGradient></defs>{artwork}</svg>"));

    private static SKBitmap Render(SvgDocument document)
    {
        using var svg = new SKSvg();
        svg.FromSvgDocument(document);
        var bitmap = new SKBitmap(100, 40);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.DrawPicture(svg.Picture!);
        return bitmap;
    }
}
