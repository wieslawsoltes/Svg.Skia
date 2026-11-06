using System;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Svg.Editor.Svg;
using Xunit;

namespace Svg.Editor.Svg.UnitTests;

public class SvgResourcePathServiceTests
{
    private static readonly Uri s_original = new("file:///projects/source/drawing.svg");
    private static readonly Uri s_target = new("file:///exports/final/drawing.svg");

    [Theory]
    [InlineData("file:///projects/source/drawing.svg", "file:///exports/final/drawing.svg", "../images/photo%20one.png?size=2#crop")]
    [InlineData("file:///C:/projects/source/drawing.svg", "file:///C:/exports/final/drawing.svg", "images/photo.png")]
    [InlineData("file:///C:/projects/source/drawing.svg", "file:///D:/exports/final/drawing.svg", "images/photo.png")]
    [InlineData("https://design.example/projects/drawing.svg", "https://assets.example/export/drawing.svg", "../images/photo.svg#art")]
    [InlineData("https://design.example/projects/drawing.svg", "https://design.example/export/drawing.svg", "/images/photo.svg#art")]
    [InlineData("file:///projects/source/drawing.svg", "file:///exports/final/drawing.svg", "images/日本語%20photo.svg#art")]
    public void Rebase_HrefResolvesToSameResource(string original, string target, string reference)
    {
        var originalUri = new Uri(original);
        var targetUri = new Uri(target);
        var source = new XElement("svg", new XElement("image", new XAttribute("href", reference))).ToString();

        var saved = XDocument.Parse(SvgResourcePathService.Rebase(source, originalUri, targetUri));
        var href = saved.Root!.Element("image")!.Attribute("href")!.Value;

        Assert.Equal(new Uri(originalUri, reference).AbsoluteUri, new Uri(targetUri, href).AbsoluteUri);
    }

    [Fact]
    public void Rebase_PreservesFragmentsAbsoluteUrisAndUnrelatedAttributes()
    {
        const string source = """
            <svg xmlns:xlink="http://www.w3.org/1999/xlink">
              <use href="#local" xlink:href="  #local  " />
              <image href="data:image/svg+xml;base64,PHN2Zy8+" />
              <image href="https://example.test/image.svg#logo" />
              <image href="file:///shared/image.png" />
              <path fill="url(#gradient)" style="filter: url('#shadow'); fill: #fff" data-note="url(relative.txt)" />
            </svg>
            """;

        var saved = XDocument.Parse(SvgResourcePathService.Rebase(source, s_original, s_target));
        var before = XDocument.Parse(source);

        Assert.Equal(before.Root!.Descendants().SelectMany(element => element.Attributes()).Select(attribute => attribute.Value),
            saved.Root!.Descendants().SelectMany(element => element.Attributes()).Select(attribute => attribute.Value));
    }

    [Fact]
    public void Rebase_XLinkHrefPreservesEscapedQueryAndFragment()
    {
        const string source = """
            <svg xmlns:xlink="http://www.w3.org/1999/xlink"><image xlink:href="../art/image.svg?size=2&amp;mode=dark#layer" /></svg>
            """;

        var saved = XDocument.Parse(SvgResourcePathService.Rebase(source, s_original, s_target));
        var href = saved.Root!.Element("image")!.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"))!.Value;

        Assert.Equal(new Uri(s_original, "../art/image.svg?size=2&mode=dark#layer").AbsoluteUri, new Uri(s_target, href).AbsoluteUri);
    }

    [Fact]
    public void Rebase_NestedXmlBaseKeepsStylesheetsImagesAndFragmentsInTheirOriginalContext()
    {
        const string source = """
            <svg xml:base="../assets/">
              <g xml:base="textures/">
                <image id="texture" href="paper.png" />
                <style><![CDATA[@import "palette.css"; .paper { fill: url('swatches.svg#paper'); }]]></style>
                <use href="#local" />
                <g xml:base="../symbols/"><use id="symbol" href="icons.svg#star" /></g>
              </g>
              <g xml:base="https://cdn.example.test/assets/"><image id="remote" href="photo.png" /></g>
            </svg>
            """;
        var before = XDocument.Parse(source);

        var saved = XDocument.Parse(SvgResourcePathService.Rebase(source, s_original, s_target));

        foreach (var id in new[] { "texture", "symbol", "remote" })
        {
            var originalElement = before.Descendants().Single(element => (string?)element.Attribute("id") == id);
            var savedElement = saved.Descendants().Single(element => (string?)element.Attribute("id") == id);
            Assert.Equal(ResolveHref(originalElement, s_original), ResolveHref(savedElement, s_target));
            Assert.Equal(originalElement.Attribute("href")!.Value, savedElement.Attribute("href")!.Value);
        }
        Assert.Equal(before.Descendants("style").Single().Value, saved.Descendants("style").Single().Value);
        Assert.Equal(EffectiveBase(before.Descendants("style").Single(), s_original), EffectiveBase(saved.Descendants("style").Single(), s_target));
        Assert.Equal("#local", saved.Descendants("use").First().Attribute("href")!.Value);
        Assert.Equal("https://cdn.example.test/assets/", saved.Descendants("g").Last().Attribute(XNamespace.Xml + "base")!.Value);
    }

    [Fact]
    public void Rebase_InlineCssAndCDataHandleMultipleUrlsWithoutChangingCommentsOrStrings()
    {
        const string source = """
            <svg>
              <style><![CDATA[
                /* url(leave-comment.png) */
                @import /* theme */ "css/theme.css" screen;
                .art { fill: URL( 'paints.svg#blue' ); filter: url(filters.svg#soft); }
                .label::after { content: "url(leave-string.png)"; }
                @font-face { src: url('fonts/Local.woff2') format('woff2'); }
              ]]></style>
              <path style="fill:url(images/paint.svg#gradient); filter: url(#shadow)" marker-start="url(markers.svg#arrow)" />
            </svg>
            """;

        var saved = XDocument.Parse(SvgResourcePathService.Rebase(source, s_original, s_target));
        var style = saved.Root!.Element("style")!;

        Assert.IsType<XCData>(style.FirstNode);
        Assert.Contains("/* url(leave-comment.png) */", style.Value);
        Assert.Contains("content: \"url(leave-string.png)\"", style.Value);
        Assert.Contains("@import /* theme */ \"../../projects/source/css/theme.css\" screen;", style.Value);
        Assert.Contains("URL( '../../projects/source/paints.svg#blue' )", style.Value);
        Assert.Contains("url(../../projects/source/filters.svg#soft)", style.Value);
        Assert.Contains("url('../../projects/source/fonts/Local.woff2')", style.Value);
        Assert.Equal("fill:url(../../projects/source/images/paint.svg#gradient); filter: url(#shadow)", saved.Root.Element("path")!.Attribute("style")!.Value);
        Assert.Equal("url(../../projects/source/markers.svg#arrow)", saved.Root.Element("path")!.Attribute("marker-start")!.Value);
    }

    [Fact]
    public void Rebase_CssEscapesAreDecodedAndReescapedWithoutBreakingUrlSyntax()
    {
        const string source = """
            <svg><style><![CDATA[
              .quoted { fill: url('images/it\'s (blue).svg#paint'); }
              .bare { fill: url(images/blue\(2\).svg#paint); }
              .hex { fill: url(\69mages/blue.svg#paint); }
              .fragment { fill: url(\23 gradient); }
              .custom { --myurl: "images/unchanged.svg"; }
            ]]></style></svg>
            """;

        var saved = XDocument.Parse(SvgResourcePathService.Rebase(source, s_original, s_target)).Root!.Element("style")!.Value;

        Assert.Contains("url('../../projects/source/images/it\\'s%20(blue).svg#paint')", saved);
        Assert.Contains("url(../../projects/source/images/blue\\(2\\).svg#paint)", saved);
        Assert.Contains("url(../../projects/source/images/blue.svg#paint)", saved);
        Assert.Contains("url(\\23 gradient)", saved);
        Assert.Contains("\"images/unchanged.svg\"", saved);
    }

    [Fact]
    public void Rebase_StylesheetInstructionUsesXmlAttributeSemantics()
    {
        const string source = """
            <?xml version="1.0" encoding="utf-8"?>
            <?xml-stylesheet type="text/css" title="example href='not-a-resource'" href="styles/editor.css?v=1&amp;theme=dark"?>
            <svg />
            """;

        var saved = XDocument.Parse(SvgResourcePathService.Rebase(source, s_original, s_target));
        var instruction = saved.Nodes().OfType<XProcessingInstruction>().Single();
        var attributes = XElement.Parse("<stylesheet " + instruction.Data + " />");

        Assert.Equal("example href='not-a-resource'", attributes.Attribute("title")!.Value);
        Assert.Equal(new Uri(s_original, "styles/editor.css?v=1&theme=dark").AbsoluteUri,
            new Uri(s_target, attributes.Attribute("href")!.Value).AbsoluteUri);
        Assert.Equal("utf-8", saved.Declaration!.Encoding);
    }

    [Fact]
    public void Rebase_DoesNotFetchExternalDtd()
    {
        const string source = """
            <!DOCTYPE svg SYSTEM "file:///a-file-that-must-not-exist/unodraw-external.dtd">
            <svg><image href="photo.png" /></svg>
            """;

        var result = SvgResourcePathService.Rebase(source, s_original, s_target);

        Assert.Contains("<!DOCTYPE svg SYSTEM", result);
        Assert.Contains("../../projects/source/photo.png", result);
    }

    [Fact]
    public void Rebase_UsesCultureIndependentCssKeywords()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            const string source = "<svg><style>@IMPORT 'theme.css'; .art { fill: URL(paint.svg#blue); }</style></svg>";

            var result = SvgResourcePathService.Rebase(source, s_original, s_target);

            Assert.Contains("../../projects/source/theme.css", result);
            Assert.Contains("../../projects/source/paint.svg#blue", result);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void Rebase_SameUriReturnsOriginalStringExactly()
    {
        const string source = "<svg >\n <image href='photo.png'/>\n</svg>";

        Assert.Same(source, SvgResourcePathService.Rebase(source, s_original, new Uri(s_original.AbsoluteUri)));
    }

    [Fact]
    public void Rebase_RejectsRelativeDocumentUrisAndMalformedXml()
    {
        Assert.Throws<ArgumentNullException>(() => SvgResourcePathService.Rebase(null!, s_original, s_target));
        Assert.Throws<ArgumentNullException>(() => SvgResourcePathService.Rebase("<svg />", null!, s_target));
        Assert.Throws<ArgumentException>(() => SvgResourcePathService.Rebase("<svg />", new Uri("drawing.svg", UriKind.Relative), s_target));
        Assert.Throws<ArgumentException>(() => SvgResourcePathService.Rebase("<svg />", s_original, new Uri("drawing.svg", UriKind.Relative)));
        Assert.Throws<XmlException>(() => SvgResourcePathService.Rebase("<svg>", s_original, s_target));
    }

    private static Uri EffectiveBase(XElement element, Uri documentUri)
    {
        var baseUri = documentUri;
        foreach (var ancestor in element.AncestorsAndSelf().Reverse())
        {
            if (ancestor.Attribute(XNamespace.Xml + "base") is { } xmlBase)
                baseUri = new Uri(baseUri, xmlBase.Value);
        }
        return baseUri;
    }

    private static string ResolveHref(XElement element, Uri documentUri)
        => new Uri(EffectiveBase(element, documentUri), element.Attribute("href")!.Value).AbsoluteUri;
}
