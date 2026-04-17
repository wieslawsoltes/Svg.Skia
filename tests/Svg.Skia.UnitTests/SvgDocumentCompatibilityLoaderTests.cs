using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Svg.Pathing;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgDocumentCompatibilityLoaderTests
{
    [Fact]
    public void OpenXmlReader_MatchesStringOverload_WhenDtdProcessingIsEnabled()
    {
        const string svg = """
            <!DOCTYPE svg [
              <!ENTITY greet "HELLO">
            ]>
            <svg xmlns="http://www.w3.org/2000/svg">
              <text id="target">&greet;</text>
            </svg>
            """;

        var originalDisableDtdProcessing = SvgDocument.DisableDtdProcessing;
        try
        {
            SvgDocument.DisableDtdProcessing = false;

            var expected = CaptureLoad(() => SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg));
            var actual = CaptureLoad(() =>
            {
                using var stringReader = new StringReader(svg);
                using var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Parse
                });
                return SvgDocumentCompatibilityLoader.Open<SvgDocument>(xmlReader);
            });

            Assert.Equal(expected.Succeeded, actual.Succeeded);
            Assert.Equal(expected.ExceptionType, actual.ExceptionType);
            Assert.Equal(expected.Text, actual.Text);
        }
        finally
        {
            SvgDocument.DisableDtdProcessing = originalDisableDtdProcessing;
        }
    }

    [Fact]
    public void OpenXmlReader_RejectsParseEnabledReaders_WhenDtdProcessingIsDisabled()
    {
        const string svg = """
            <!DOCTYPE svg [
              <!ENTITY greet "HELLO">
            ]>
            <svg xmlns="http://www.w3.org/2000/svg">
              <text id="target">&greet;</text>
            </svg>
            """;

        var originalDisableDtdProcessing = SvgDocument.DisableDtdProcessing;
        try
        {
            SvgDocument.DisableDtdProcessing = true;

            var stringLoad = CaptureLoad(() => SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg));
            var xmlReaderLoad = CaptureLoad(() =>
            {
                using var stringReader = new StringReader(svg);
                using var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Parse
                });
                return SvgDocumentCompatibilityLoader.Open<SvgDocument>(xmlReader);
            });

            Assert.False(stringLoad.Succeeded);
            Assert.False(xmlReaderLoad.Succeeded);
            Assert.Equal(typeof(InvalidOperationException).FullName, xmlReaderLoad.ExceptionType);
        }
        finally
        {
            SvgDocument.DisableDtdProcessing = originalDisableDtdProcessing;
        }
    }

    [Fact]
    public void OpenPath_AppliesImportedStylesheets()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green; }");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg">
                  <style type="text/css"><![CDATA[
                    @import url("styles.css");
                  ]]></style>
                  <circle id="target" cx="10" cy="10" r="5" fill="red" />
                </svg>
                """);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var circle = document.Descendants().OfType<SvgCircle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(circle.Fill);

            Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void FromSvg_IgnoresEmptyStyleElements()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <style />
              <rect id="target" width="10" height="10" fill="red" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_DetachesSyntheticCssQueryRootAfterApplyingStyles()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <style>#target { fill: green; }</style>
              <rect id="target" width="10" height="10" fill="red" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Null(document.Parent);
        Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_AggregatesMixedTextAndChildContentInDocumentOrder()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <text id="target">start<tspan>mid</tspan>end</text>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var text = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "target");

        Assert.Equal("startmidend", text.Content);
        Assert.NotEmpty(text.Nodes);
    }

    [Fact]
    public void FromSvg_AggregatesChildFirstMixedContentInDocumentOrder()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <text id="target"><tspan>mid</tspan>end</text>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var text = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "target");

        Assert.Equal("midend", text.Content);
        Assert.Equal(2, text.Nodes.Count);
        Assert.IsType<SvgTextSpan>(text.Nodes[0]);
        Assert.IsType<SvgContentNode>(text.Nodes[1]);
    }

    [Fact]
    public void FromSvg_ClearsNodesWhenElementContainsOnlyChildElements()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <g id="target">
                <rect width="10" height="10" />
              </g>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var group = document.Descendants().OfType<SvgGroup>().Single(static element => element.ID == "target");

        Assert.Empty(group.Nodes);
        Assert.Single(group.Children);
    }

    [Fact]
    public void FromSvg_AggregatesSingleTextNodeContentAndPreservesContentNode()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <text id="target">hello</text>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var text = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "target");

        Assert.Equal("hello", text.Content);
        Assert.Single(text.Nodes);
        Assert.IsType<SvgContentNode>(text.Nodes[0]);
    }

    [Fact]
    public void OpenPath_AppliesImportedStylesheetsWhenMediaMatchesScreen()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green; }");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg">
                  <style type="text/css"><![CDATA[
                    @import url("styles.css") screen;
                  ]]></style>
                  <circle id="target" cx="10" cy="10" r="5" fill="red" />
                </svg>
                """);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var circle = document.Descendants().OfType<SvgCircle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(circle.Fill);

            Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void OpenPath_IgnoresImportedStylesheetsWhenMediaDoesNotMatch()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green; }");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg">
                  <style type="text/css"><![CDATA[
                    @import url("styles.css") print;
                  ]]></style>
                  <circle id="target" cx="10" cy="10" r="5" fill="red" />
                </svg>
                """);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var circle = document.Descendants().OfType<SvgCircle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(circle.Fill);

            Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void OpenPath_IgnoresImportedStylesheetsWhenMediaListContainsEmptyEntry()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green; }");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg">
                  <style type="text/css"><![CDATA[
                    @import url("styles.css") print,;
                  ]]></style>
                  <circle id="target" cx="10" cy="10" r="5" fill="red" />
                </svg>
                """);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var circle = document.Descendants().OfType<SvgCircle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(circle.Fill);

            Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void OpenPath_AppliesImportedStylesheetsWhenMediaFeatureMatchesStaticViewport()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green; }");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg">
                  <style type="text/css"><![CDATA[
                    @import url("styles.css") screen and (min-width: 100px) and (orientation: landscape);
                  ]]></style>
                  <circle id="target" cx="10" cy="10" r="5" fill="red" />
                </svg>
                """);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var circle = document.Descendants().OfType<SvgCircle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(circle.Fill);

            Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void OpenPath_AppliesImportedStylesheetsWhenMediaFeatureMatchesDocumentViewport()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green; }");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="1024" height="768">
                  <style type="text/css"><![CDATA[
                    @import url("styles.css") screen and (min-width: 800px) and (min-height: 700px);
                  ]]></style>
                  <circle id="target" cx="10" cy="10" r="5" fill="red" />
                </svg>
                """);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var circle = document.Descendants().OfType<SvgCircle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(circle.Fill);

            Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void OpenPath_IgnoresImportedStylesheetsWhenMediaFeatureDoesNotMatchStaticViewport()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green; }");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg">
                  <style type="text/css"><![CDATA[
                    @import url("styles.css") screen and (max-width: 1px);
                  ]]></style>
                  <circle id="target" cx="10" cy="10" r="5" fill="red" />
                </svg>
                """);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var circle = document.Descendants().OfType<SvgCircle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(circle.Fill);

            Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void OpenPath_IgnoresImportedStylesheetsWhenMediaFeatureDoesNotMatchDocumentViewport()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green; }");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="640" height="480">
                  <style type="text/css"><![CDATA[
                    @import url("styles.css") screen and (min-width: 800px);
                  ]]></style>
                  <circle id="target" cx="10" cy="10" r="5" fill="red" />
                </svg>
                """);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var circle = document.Descendants().OfType<SvgCircle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(circle.Fill);

            Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void OpenPath_IgnoresImportedStylesheetsWhenMediaQueryOmitsAndBeforeFeature()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green; }");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="1024" height="768">
                  <style type="text/css"><![CDATA[
                    @import url("styles.css") screen (min-width: 100px);
                  ]]></style>
                  <circle id="target" cx="10" cy="10" r="5" fill="red" />
                </svg>
                """);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var circle = document.Descendants().OfType<SvgCircle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(circle.Fill);

            Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void OpenPath_IgnoresUnreadableImportedStylesheets()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        var cssPath = Path.Combine(tempDirectory, "styles.css");
        var svgPath = Path.Combine(tempDirectory, "test.svg");

        try
        {
            File.WriteAllText(cssPath, "#target { fill: green; }");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg">
                  <style type="text/css"><![CDATA[
                    @import url("styles.css");
                  ]]></style>
                  <circle id="target" cx="10" cy="10" r="5" fill="red" />
                </svg>
                """);

            File.SetUnixFileMode(cssPath, UnixFileMode.None);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var circle = document.Descendants().OfType<SvgCircle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(circle.Fill);

            Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            if (File.Exists(cssPath))
            {
                File.SetUnixFileMode(cssPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void OpenPath_ReappliesImportedStylesheetsAcrossSeparateStyleBlocksInSourceOrder()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green; }");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg">
                  <style type="text/css"><![CDATA[
                    @import url("styles.css");
                    #target { fill: blue; }
                  ]]></style>
                  <style type="text/css"><![CDATA[
                    @import url("styles.css");
                  ]]></style>
                  <circle id="target" cx="10" cy="10" r="5" fill="red" />
                </svg>
                """);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var circle = document.Descendants().OfType<SvgCircle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(circle.Fill);

            Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void OpenPath_IgnoresImportedStylesheetsAfterStyleRules()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green !important; }");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg">
                  <style type="text/css"><![CDATA[
                    #target { fill: blue; }
                    @import url("styles.css");
                  ]]></style>
                  <circle id="target" cx="10" cy="10" r="5" fill="red" />
                </svg>
                """);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var circle = document.Descendants().OfType<SvgCircle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(circle.Fill);

            Assert.Equal(Color.Blue.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void OpenXmlReader_AppliesImportedStylesheetsUsingReaderBaseUri()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green; }");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg">
                  <style type="text/css"><![CDATA[
                    @import url("styles.css");
                  ]]></style>
                  <circle id="target" cx="10" cy="10" r="5" fill="red" />
                </svg>
                """);

            using var xmlReader = XmlReader.Create(svgPath, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit
            });

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(xmlReader);
            var circle = document.Descendants().OfType<SvgCircle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(circle.Fill);

            Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void OpenPath_IgnoresMalformedImportedStylesheetsWithoutSemicolon()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green; }");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg">
                  <style><![CDATA[
                    @import "styles.css"
                  ]]></style>
                  <circle id="target" cx="10" cy="10" r="5" fill="red" />
                </svg>
                """);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var circle = document.Descendants().OfType<SvgCircle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(circle.Fill);

            Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void FromSvg_IgnoresUnsupportedStyleElementTypes()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect id="default-style" width="10" height="10" fill="red" />
              <rect id="unsupported-style" y="20" width="10" height="10" fill="green" />
              <style>#default-style { fill: green; }</style>
              <style type="text/some-unknown-styling-language">#unsupported-style { fill: red; }</style>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var defaultRect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "default-style");
        var unsupportedRect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "unsupported-style");

        var defaultFill = Assert.IsType<SvgColourServer>(defaultRect.Fill);
        var unsupportedFill = Assert.IsType<SvgColourServer>(unsupportedRect.Fill);

        Assert.Equal(Color.Green.ToArgb(), defaultFill.Colour.ToArgb());
        Assert.Equal(Color.Green.ToArgb(), unsupportedFill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_LinkPseudoClassDoesNotStyleNonLinkTextSiblings()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink">
              <text id="label" fill="black">
                prefix
                <a id="cta" xlink:href="#target">link</a>
                suffix
              </text>
              <style>a#cta:link { fill: red; }</style>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var text = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var anchor = document.Descendants().OfType<SvgAnchor>().Single(static element => element.ID == "cta");

        var textFill = Assert.IsType<SvgColourServer>(text.Fill);
        var anchorFill = Assert.IsType<SvgColourServer>(anchor.Fill);

        Assert.Equal(Color.Black.ToArgb(), textFill.Colour.ToArgb());
        Assert.Equal(Color.Red.ToArgb(), anchorFill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_LinkPseudoClassStylesTextContainerWhenLinkOwnsEntireTextRun()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink">
              <text id="label">
                <a id="cta" xlink:href="#target">link</a>
              </text>
              <style>:link { fill: red; }</style>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var text = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var anchor = document.Descendants().OfType<SvgAnchor>().Single(static element => element.ID == "cta");

        var textFill = Assert.IsType<SvgColourServer>(text.Fill);
        var anchorFill = Assert.IsType<SvgColourServer>(anchor.Fill);

        Assert.Equal(Color.Red.ToArgb(), textFill.Colour.ToArgb());
        Assert.Equal(Color.Red.ToArgb(), anchorFill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_LinkPseudoClassDoesNotStylePreservedWhitespaceOutsideLink()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink">
              <text id="label" xml:space="preserve" fill="black"> <a id="cta" xlink:href="#target">link</a> </text>
              <style>:link { fill: red; }</style>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var text = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "label");
        var anchor = document.Descendants().OfType<SvgAnchor>().Single(static element => element.ID == "cta");

        var textFill = Assert.IsType<SvgColourServer>(text.Fill);
        var anchorFill = Assert.IsType<SvgColourServer>(anchor.Fill);

        Assert.Equal(Color.Black.ToArgb(), textFill.Colour.ToArgb());
        Assert.Equal(Color.Red.ToArgb(), anchorFill.Colour.ToArgb());
    }

    [Fact]
    public void ImportChain_DistinguishesUrisThatDifferOnlyByCase()
    {
        var processorType = typeof(SvgDocumentCompatibilityLoader).Assembly.GetType("Svg.SvgCssCompatibilityProcessor");
        Assert.NotNull(processorType);

        var createImportChain = processorType!.GetMethod(
            "CreateImportChain",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(createImportChain);

        var importChain = Assert.IsType<HashSet<string>>(createImportChain!.Invoke(null, null));

        Assert.True(importChain.Add("file:///tmp/A.css"));
        Assert.True(importChain.Add("file:///tmp/a.css"));
    }

    [Fact]
    public void FromSvg_AppliesCssStyleTypeWithParameters()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect id="target" width="10" height="10" fill="red" />
              <style type="text/css; charset=utf-8">#target { fill: green; }</style>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void OpenPath_IgnoresCommentedImportTokens()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green !important; }");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg">
                  <style type="text/css"><![CDATA[
                    /* @import url("styles.css"); */
                    #target { fill: red; }
                  ]]></style>
                  <circle id="target" cx="10" cy="10" r="5" fill="blue" />
                </svg>
                """);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var circle = document.Descendants().OfType<SvgCircle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(circle.Fill);

            Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void OpenPath_IgnoresImportTokensInsideStrings()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green !important; }");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg">
                  <text id="target" x="0" y="15" fill="blue">test</text>
                  <style type="text/css"><![CDATA[
                    #target {
                      font-family: "@import url('styles.css');";
                      fill: red;
                    }
                  ]]></style>
                </svg>
                """);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var text = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(text.Fill);

            Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void LoadStructure_WithoutStylesheet_EagerlyAppliesCompatibilityStyles()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect id="target" width="10" height="10" fill="red" stroke="blue" style="fill: orange" />
            </svg>
            """;

        var loadStructure = typeof(SvgDocumentCompatibilityLoader).GetMethod(
            "LoadStructure",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            new[] { typeof(string), typeof(string), typeof(Uri) },
            modifiers: null);
        Assert.NotNull(loadStructure);

        var loadResult = loadStructure!.MakeGenericMethod(typeof(SvgDocument)).Invoke(null, new object?[] { svg, null, null });
        Assert.NotNull(loadResult);

        var documentProperty = loadResult!.GetType().GetProperty("Document");
        var hasStagedStylesProperty = loadResult.GetType().GetProperty("HasStagedStyles");
        Assert.NotNull(documentProperty);
        Assert.NotNull(hasStagedStylesProperty);

        var document = Assert.IsType<SvgDocument>(documentProperty!.GetValue(loadResult));
        var hasStagedStyles = Assert.IsType<bool>(hasStagedStylesProperty!.GetValue(loadResult));

        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);
        var stroke = Assert.IsType<SvgColourServer>(rect.Stroke);

        Assert.Equal(Color.Orange.ToArgb(), fill.Colour.ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), stroke.Colour.ToArgb());
        Assert.False(hasStagedStyles);
    }

    [Fact]
    public void FromSvg_ParsesInlineStyleAttributesWithQuotedSemicolons()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <text id="target" x="0" y="15" style="font-family:'Semi;Colon'; fill: red">test</text>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var text = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(text.Fill);

        Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
        Assert.Contains("Semi;Colon", text.FontFamily, StringComparison.Ordinal);
    }

    [Fact]
    public void FromSvg_ParsesInlineStyleAttributesWithComments()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect id="target" width="10" height="10" style="/* lead */ fill: green; /* mid */ stroke: blue" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);
        var stroke = Assert.IsType<SvgColourServer>(rect.Stroke);

        Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), stroke.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_ParsesInlineStyleAttributesWithTrailingCommentsInsideDeclaration()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect id="target" width="10" height="10" style="/*lead*/fill:green/*tail*/" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_ParsesInlineStyleAttributesCaseInsensitively()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect id="target" width="10" height="10" fill="red" style="FiLl: oRaNgE" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.Orange.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_ParsesInlineStyleAttributesWithEmptyDeclarationsAndWhitespace()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect id="target" width="10" height="10" style=" ; ; fill: green ; ; stroke: blue ; " />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);
        var stroke = Assert.IsType<SvgColourServer>(rect.Stroke);

        Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), stroke.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_DoesNotShareMutablePathDataAcrossFreshDocuments()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <path id="target" d="M1 1 L9 1 L9 9 Z" />
            </svg>
            """;

        var firstDocument = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var secondDocument = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var firstPath = firstDocument.Descendants().OfType<SvgPath>().Single(static element => element.ID == "target");
        var secondPath = secondDocument.Descendants().OfType<SvgPath>().Single(static element => element.ID == "target");

        Assert.NotSame(firstPath.PathData, secondPath.PathData);
        Assert.NotNull(firstPath.PathData);
        Assert.NotNull(secondPath.PathData);
        Assert.NotSame(firstPath.PathData[0], secondPath.PathData[0]);

        firstPath.PathData[0] = new SvgMoveToSegment(false, new PointF(3f, 3f));

        var firstMove = Assert.IsType<SvgMoveToSegment>(firstPath.PathData[0]);
        var secondMove = Assert.IsType<SvgMoveToSegment>(secondPath.PathData[0]);
        Assert.Equal(new PointF(3f, 3f), firstMove.End);
        Assert.Equal(new PointF(1f, 1f), secondMove.End);
    }

    [Fact]
    public void FromSvg_StripsUnsupportedExternalCssFontFaceRulesFromStoredStyleContent()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <style type="text/css"><![CDATA[
                @font-face {
                  font-family: 'ExternalOnly';
                  src: url('../fonts/OpenGostTypeA-Regular.ttf') format('truetype');
                }
                #target { fill: red; }
              ]]></style>
              <text id="target" x="0" y="15" fill="blue">test</text>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var style = document.Descendants().OfType<SvgUnknownElement>().Single();
        var text = document.Descendants().OfType<SvgText>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(text.Fill);

        Assert.DoesNotContain("@font-face", style.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_PreservesFragmentBackedCssFontFaceRulesInStoredStyleContent()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <defs>
                <font id="DefaultFont" horiz-adv-x="100">
                  <font-face font-family="DefaultFont" units-per-em="100" ascent="100" descent="0" />
                  <glyph unicode="A" glyph-name="A" horiz-adv-x="100" d="M0 0 L50 100 L100 0 Z" />
                </font>
              </defs>
              <style type="text/css"><![CDATA[
                @font-face {
                  font-family: 'DefaultFont';
                  src: url(#DefaultFont);
                }
              ]]></style>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var style = document.Descendants().OfType<SvgUnknownElement>().Single();

        Assert.Contains("@font-face", style.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("url(#DefaultFont)", style.Content, StringComparison.OrdinalIgnoreCase);
    }

    private static LoadResult CaptureLoad(Func<SvgDocument> load)
    {
        try
        {
            var document = load();
            var text = document
                .Descendants()
                .OfType<SvgText>()
                .Single(static element => element.ID == "target")
                .Text;
            return new LoadResult(true, text, null);
        }
        catch (Exception ex)
        {
            return new LoadResult(false, null, ex.GetType().FullName);
        }
    }

    private readonly record struct LoadResult(bool Succeeded, string? Text, string? ExceptionType);
}
