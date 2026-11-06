using System;
using System.Drawing;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Svg.Model.Services;
using Xunit;
using SkiaAlphaType = SkiaSharp.SKAlphaType;
using SkiaBitmap = SkiaSharp.SKBitmap;
using SkiaCanvas = SkiaSharp.SKCanvas;
using SkiaColors = SkiaSharp.SKColors;
using SkiaColorType = SkiaSharp.SKColorType;
using SkiaImageInfo = SkiaSharp.SKImageInfo;

namespace Svg.Skia.UnitTests;

public class SvgPaintSerializationTests
{
    public static TheoryData<string, string> RoundTripCases => new()
    {
        { "fill", Document("<rect x='10' y='10' width='55' height='35' fill='rgba(220,40,70,0.5)'/>") },
        { "stroke", Document("<rect x='10' y='10' width='55' height='35' fill='none' stroke='#197FCC80' stroke-width='8'/>") },
        { "independent paint opacity", Document("<rect x='10' y='10' width='55' height='35' fill='#DE284680' fill-opacity='0.4' stroke='#197FCC80' stroke-opacity='0.6' stroke-width='8'/>") },
        { "stylesheet and group opacity", Document("""
            <style>.paint { fill: rgba(220,40,70,0.5); stroke: rgba(25,127,204,0.4); fill-opacity: 0.8; stroke-opacity: 0.6; stroke-width: 6; }</style>
            <g class='paint' opacity='0.75'><rect x='10' y='10' width='35' height='30'/><rect x='30' y='20' width='35' height='30'/></g>
            """) },
        { "root and nested inheritance", Document("""
            <rect x='10' y='10' width='35' height='30'/>
            <g fill-opacity='0.6'><rect x='30' y='20' width='35' height='30'/></g>
            """, "fill='#DE284680' fill-opacity='0.8'") },
        { "inline important paint", Document("""
            <rect x='10' y='10' width='55' height='35' fill='blue' style='fill:rgba(220,40,70,0.5)!important; fill-opacity:0.4!important; stroke:rgba(25,127,204,0.4)!important; stroke-opacity:0.6!important; stroke-width:8'/>
            """) },
        { "currentColor", Document("""
            <g fill='currentColor' color='#DE284680'><rect x='10' y='10' width='35' height='30'/><rect x='30' y='20' width='35' height='30' color='#197FCC80'/></g>
            """) },
        { "gradient stop alpha", Document("""
            <defs><linearGradient id='paint'><stop offset='0' stop-color='#DE284680' stop-opacity='0.6'/><stop offset='1' stop-color='#197FCC40'/></linearGradient></defs>
            <rect x='10' y='10' width='55' height='35' fill='url(#paint)' fill-opacity='0.8'/>
            """) },
        { "mask and pattern content", Document("""
            <defs>
              <pattern id='paint' width='10' height='10' patternUnits='userSpaceOnUse'><rect width='10' height='10' fill='#DE284680'/><circle cx='5' cy='5' r='3' fill='#197FCC80' fill-opacity='0.6'/></pattern>
              <mask id='mask' maskUnits='userSpaceOnUse' x='0' y='0' width='80' height='60'><rect width='80' height='60' fill='#FFFFFF80'/></mask>
            </defs>
            <rect x='10' y='10' width='55' height='35' fill='url(#paint)' mask='url(#mask)'/>
            """) },
        { "deferred fallback", Document("<rect x='10' y='10' width='55' height='35' fill='url(#missing) #DE284680' fill-opacity='0.8'/>") },
        { "fully transparent paint", Document("<rect x='10' y='10' width='55' height='35' fill='transparent' stroke='#197FCC00' stroke-width='8'/>") }
    };

    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public void NativeDocumentRenderingSurvivesTwoSaveReopenCycles(string scenario, string source)
    {
        var original = Open(source);
        var pixels = Render(original);
        var saved = original.GetXML();
        for (var cycle = 1; cycle <= 2; cycle++)
        {
            var reopened = Open(saved);
            Assert.True(pixels.AsSpan().SequenceEqual(Render(reopened)), $"Paint changed after save/reopen {cycle}: {scenario}.");
            saved = reopened.GetXML();
        }
    }

    [Fact]
    public void SerializingDocumentAndDetachedElementPreservesLivePaintInstancesAndOpacity()
    {
        var document = Open(Document("<rect id='shape' x='10' y='10' width='55' height='35' fill='#DE284680' fill-opacity='0.4' stroke='#197FCC80' stroke-opacity='0.6' stroke-width='8'/>"));
        var shape = Assert.IsType<SvgRectangle>(document.GetElementById("shape"));
        var fill = Assert.IsType<SvgColourServer>(shape.Fill);
        var stroke = Assert.IsType<SvgColourServer>(shape.Stroke);
        var pixels = Render(document);

        var saved = document.GetXML();
        var element = shape.GetXML();

        Assert.Same(fill, shape.Fill);
        Assert.Same(stroke, shape.Stroke);
        Assert.Equal((byte)128, fill.Colour.A);
        Assert.Equal((byte)128, stroke.Colour.A);
        Assert.Equal(0.4f, shape.FillOpacity);
        Assert.Equal(0.6f, shape.StrokeOpacity);
        Assert.Equal(saved, document.GetXML());
        Assert.Equal(pixels, Render(document));
        Assert.Equal(pixels, Render(Open(Document(XElement.Parse(element).ToString(SaveOptions.DisableFormatting)))));
    }

    [Theory]
    [InlineData("#ABABAB", "#ABABAB")]
    [InlineData("#808080", "#808080")]
    [InlineData("#FF7F50", "#FF7F50")]
    [InlineData("#FF0000", "#FF0000")]
    [InlineData("#F00", "#FF0000")]
    [InlineData("#000", "#000000")]
    [InlineData("#fFfF", "#FFFFFF")]
    [InlineData("#1234", "#11223344")]
    [InlineData("#11223344", "#11223344")]
    [InlineData("#FF0000FF", "#FF0000")]
    public void NumericColorsKeepPortableChannelsAndStableSerialization(string input, string expectedPaint)
    {
        var document = Open(Document($"<rect id='shape' x='10' y='10' width='55' height='35' fill='{input}'/>"));
        var shape = Assert.IsType<SvgRectangle>(document.GetElementById("shape"));
        var color = Assert.IsType<SvgColourServer>(shape.Fill).Colour;
        Assert.False(color.IsKnownColor);
        Assert.False(color.IsSystemColor);
        var pixels = Render(document);
        var saved = document.GetXML();
        Assert.Contains($"fill:{expectedPaint}", saved, StringComparison.Ordinal);

        for (var cycle = 0; cycle < 2; cycle++)
        {
            document = Open(saved);
            shape = Assert.IsType<SvgRectangle>(document.GetElementById("shape"));
            Assert.Equal(color.ToArgb(), Assert.IsType<SvgColourServer>(shape.Fill).Colour.ToArgb());
            Assert.Equal(pixels, Render(document));
            Assert.Equal(saved, document.GetXML());
        }
    }

    public static TheoryData<KnownColor> SystemColors
    {
        get
        {
            var colors = new TheoryData<KnownColor>();
            foreach (var known in Enum.GetValues<KnownColor>())
                if (Color.FromKnownColor(known).IsSystemColor)
                    colors.Add(known);
            return colors;
        }
    }

    [Theory]
    [MemberData(nameof(SystemColors))]
    public void ProgrammaticSystemColorIsSavedAsPortableRgb(KnownColor known)
    {
        var document = Open(Document("<rect id='shape' x='10' y='10' width='55' height='35'/>"));
        var shape = Assert.IsType<SvgRectangle>(document.GetElementById("shape"));
        var color = Color.FromKnownColor(known);
        Assert.True(color.IsSystemColor);
        shape.Fill = new SvgColourServer(color);
        var pixels = Render(document);
        var saved = document.GetXML();
        Assert.Contains($"fill:#{color.R:X2}{color.G:X2}{color.B:X2}", saved, StringComparison.Ordinal);
        Assert.DoesNotContain($"fill:{color.Name}", saved, StringComparison.OrdinalIgnoreCase);

        for (var cycle = 0; cycle < 2; cycle++)
        {
            document = Open(saved);
            shape = Assert.IsType<SvgRectangle>(document.GetElementById("shape"));
            Assert.Equal(color.ToArgb(), Assert.IsType<SvgColourServer>(shape.Fill).Colour.ToArgb());
            Assert.Equal(pixels, Render(document));
            Assert.Equal(saved, document.GetXML());
        }
    }

    [Fact]
    public void NamedCssColorsRemainSupported()
    {
        var document = Open(Document("<rect id='shape' x='10' y='10' width='55' height='35' fill='red'/>"));
        var saved = document.GetXML();
        Assert.Contains("fill:red", saved, StringComparison.Ordinal);
        Assert.Equal(Render(document), Render(Open(saved)));
    }

    private static string Document(string content, string attributes = "")
    {
        return $"<svg xmlns='http://www.w3.org/2000/svg' width='80' height='60' {attributes}>{content}</svg>";
    }

    private static SvgDocument Open(string source)
    {
        using var text = new StringReader(source);
        using var reader = XmlReader.Create(text, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
        return Assert.IsType<SvgDocument>(SvgService.Open(reader));
    }

    private static byte[] Render(SvgDocument document)
    {
        using var svg = SKSvg.CreateFromSvgDocument(document);
        Assert.NotNull(svg.Picture);
        using var bitmap = new SkiaBitmap(new SkiaImageInfo(80, 60, SkiaColorType.Rgba8888, SkiaAlphaType.Premul));
        using var canvas = new SkiaCanvas(bitmap);
        canvas.Clear(SkiaColors.Transparent);
        canvas.DrawPicture(svg.Picture);
        canvas.Flush();
        return bitmap.Bytes;
    }
}
