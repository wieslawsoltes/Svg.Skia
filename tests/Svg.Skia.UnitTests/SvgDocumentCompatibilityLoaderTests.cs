using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
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

    [Theory]
    [InlineData("screen", true)]
    [InlineData("print", false)]
    public void OpenPath_FiltersLinkedStylesheetByMedia(string media, bool shouldApply)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green; }");
            File.WriteAllText(svgPath, $$"""
                <svg xmlns="http://www.w3.org/2000/svg">
                  <link rel="stylesheet" href="styles.css" media="{{media}}" />
                  <rect id="target" width="10" height="10" fill="red" />
                </svg>
                """);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(rect.Fill);

            Assert.Equal((shouldApply ? Color.Green : Color.Red).ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("screen", true)]
    [InlineData("print", false)]
    public void OpenPath_FiltersXmlStylesheetProcessingInstructionByMedia(string media, bool shouldApply)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green; }");
            File.WriteAllText(svgPath, $$"""
                <?xml-stylesheet type="text/css" href="styles.css" media="{{media}}"?>
                <svg xmlns="http://www.w3.org/2000/svg">
                  <rect id="target" width="10" height="10" fill="red" />
                </svg>
                """);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(rect.Fill);

            Assert.Equal((shouldApply ? Color.Green : Color.Red).ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("screen and (width: 100px)", true)]
    [InlineData("screen and (width: 480px)", false)]
    public void OpenPath_FiltersPrologXmlStylesheetProcessingInstructionByDocumentViewport(string media, bool shouldApply)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var cssPath = Path.Combine(tempDirectory, "styles.css");
            var svgPath = Path.Combine(tempDirectory, "test.svg");

            File.WriteAllText(cssPath, "#target { fill: green; }");
            File.WriteAllText(svgPath, $$"""
                <?xml-stylesheet type="text/css" href="styles.css" media="{{media}}"?>
                <svg xmlns="http://www.w3.org/2000/svg" width="100" height="50">
                  <rect id="target" width="10" height="10" fill="red" />
                </svg>
                """);

            var document = SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions());
            var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
            var fill = Assert.IsType<SvgColourServer>(rect.Fill);

            Assert.Equal((shouldApply ? Color.Green : Color.Red).ToArgb(), fill.Colour.ToArgb());
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

    [Theory]
    [InlineData("#AABBCC80", 0xAA, 0xBB, 0xCC, 0x80)]
    [InlineData("#abc8", 0xAA, 0xBB, 0xCC, 0x88)]
    public void FromSvg_ParsesCssHexAlphaFillColors(string fillValue, int red, int green, int blue, int alpha)
    {
        var svg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg">
              <style>#target { fill: {{fillValue}}; }</style>
              <rect id="target" width="10" height="10" fill="red" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.FromArgb(alpha, red, green, blue).ToArgb(), fill.Colour.ToArgb());
    }

    [Theory]
    [InlineData("#AABBCC80", 0xAA, 0xBB, 0xCC, 0x80)]
    [InlineData("#abc8", 0xAA, 0xBB, 0xCC, 0x88)]
    public void FromSvg_ParsesAttributeHexAlphaFillColors(string fillValue, int red, int green, int blue, int alpha)
    {
        var svg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect id="target" width="10" height="10" fill="{{fillValue}}" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.FromArgb(alpha, red, green, blue).ToArgb(), fill.Colour.ToArgb());
    }

    [Theory]
    [InlineData("123")]
    [InlineData("#12345")]
    public void FromSvg_InvalidPaintValuesDoNotOverrideInheritedFill(string fillValue)
    {
        var svg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg">
              <g fill="lime">
                <rect id="target" width="10" height="10" fill="{{fillValue}}" />
              </g>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.Lime.ToArgb(), fill.Colour.ToArgb());
    }

    [Theory]
    [InlineData("style=\"fill: lime; fill: 123\"")]
    [InlineData("style=\"fill: lime; fill: #12345\"")]
    public void FromSvg_InvalidInlinePaintDeclarationsDoNotOverrideEarlierFill(string styleAttribute)
    {
        var svg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect id="target" width="10" height="10" {{styleAttribute}} />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.Lime.ToArgb(), fill.Colour.ToArgb());
    }

    [Theory]
    [InlineData("#target { fill: lime; } #target { fill: 123; }")]
    [InlineData("#target { fill: lime; } #target { fill: #12345; }")]
    [InlineData("rect { fill: lime; } #target { fill: 123; }")]
    [InlineData("rect { fill: lime; } #target { fill: #12345; }")]
    public void FromSvg_InvalidStylesheetPaintDeclarationsDoNotOverrideEarlierFill(string css)
    {
        var svg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg">
              <style>{{css}}</style>
              <rect id="target" width="10" height="10" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.Lime.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_StylesheetImportantOverridesInlineNormal()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <style>#target { fill: green !important; }</style>
              <rect id="target" width="10" height="10" style="fill: red" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_InlineImportantOverridesStylesheetImportant()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <style>#target { fill: red !important; }</style>
              <rect id="target" width="10" height="10" style="fill: green !important" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_SelectorListUsesMatchingBranchSpecificity()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <style>
                .low, #unused { fill: red; }
                .target { fill: green; }
              </style>
              <rect id="target" class="low target" width="10" height="10" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_CustomPropertySelectorListUsesMatchingBranchSpecificity()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <style>
                .low, #unused { --paint: red; }
                .target { --paint: green; }
                rect { fill: var(--paint); }
              </style>
              <rect id="target" class="low target" width="10" height="10" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_LowSpecificitySourceOrderDoesNotOvertakeHigherSpecificity()
    {
        var lowSpecificityRules = new System.Text.StringBuilder();
        for (var i = 0; i < 40; i++)
        {
            lowSpecificityRules.AppendLine("rect { fill: red; }");
        }

        var svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <style>
                g rect { fill: green; }
            """ + lowSpecificityRules + """
              </style>
              <g><rect id="target" width="10" height="10" /></g>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_RootSvgSelectorMatchesDocumentForCustomProperties()
    {
        const string svg = """
            <svg id="root" class="theme" xmlns="http://www.w3.org/2000/svg">
              <style>
                svg.theme { --paint: green; }
                rect { fill: var(--paint, red); }
              </style>
              <rect id="target" width="10" height="10" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_ClassSelectorsUseCssWhitespace()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <style>.bar { fill: green; }</style>
              <rect id="target" class="foo&#x9;bar" width="10" height="10" fill="red" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_DashMatchAttributeSelectorOnlyMatchesExactOrPrefix()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <style>rect[lang|="en"] { fill: lime; }</style>
              <rect id="exact" lang="en" width="10" height="10" fill="red" />
              <rect id="prefix" lang="en-US" x="10" width="10" height="10" fill="red" />
              <rect id="middle" lang="fr-en" x="20" width="10" height="10" fill="red" />
              <rect id="suffix" lang="x-en-US" x="30" width="10" height="10" fill="red" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rectangles = document
            .Descendants()
            .OfType<SvgRectangle>()
            .ToDictionary(static element => element.ID!);

        AssertFill(rectangles["exact"], Color.Lime);
        AssertFill(rectangles["prefix"], Color.Lime);
        AssertFill(rectangles["middle"], Color.Red);
        AssertFill(rectangles["suffix"], Color.Red);

        static void AssertFill(SvgRectangle rectangle, Color expected)
        {
            var fill = Assert.IsType<SvgColourServer>(rectangle.Fill);
            Assert.Equal(expected.ToArgb(), fill.Colour.ToArgb());
        }
    }

    [Fact]
    public void FromSvg_FixedPositionNthOfTypeSelectorsApplyWithoutSkippingRule()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <style>
                rect:nth-of-type(1) { fill: lime; }
                rect:nth-last-of-type(1) { stroke: blue; }
                circle:first-of-type { fill: green; }
              </style>
              <g>
                <rect id="first" width="10" height="10" fill="red" stroke="red" />
                <circle id="circle" cx="15" cy="5" r="5" fill="red" />
                <rect id="last" x="20" width="10" height="10" fill="red" stroke="red" />
              </g>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var first = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "first");
        var last = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "last");
        var circle = document.Descendants().OfType<SvgCircle>().Single(static element => element.ID == "circle");
        var firstFill = Assert.IsType<SvgColourServer>(first.Fill);
        var lastStroke = Assert.IsType<SvgColourServer>(last.Stroke);
        var circleFill = Assert.IsType<SvgColourServer>(circle.Fill);

        Assert.Equal(Color.Lime.ToArgb(), firstFill.Colour.ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), lastStroke.Colour.ToArgb());
        Assert.Equal(Color.Green.ToArgb(), circleFill.Colour.ToArgb());
    }

    [Theory]
    [InlineData("screen", true)]
    [InlineData("print", false)]
    public void FromSvg_FiltersNestedMediaStyleRules(string media, bool shouldApply)
    {
        var svg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg">
              <style>@media {{media}} { #target { fill: green; } }</style>
              <rect id="target" width="10" height="10" fill="red" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal((shouldApply ? Color.Green : Color.Red).ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_AppliesSvg2GeometryFromStylesheet()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <style>#target { x: 5; y: 6; width: 7; height: 8; fill: green; }</style>
              <rect id="target" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(5f, rect.X.Value);
        Assert.Equal(6f, rect.Y.Value);
        Assert.Equal(7f, rect.Width.Value);
        Assert.Equal(8f, rect.Height.Value);
        Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_AppliesSvg2GeometryFromInlineStyle()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect id="target" style="x: 5; y: 6; width: 7; height: 8; fill: green" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(5f, rect.X.Value);
        Assert.Equal(6f, rect.Y.Value);
        Assert.Equal(7f, rect.Width.Value);
        Assert.Equal(8f, rect.Height.Value);
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
    public void OpenPath_AppliesImportedStylesheetWithoutSemicolonAtEndOfFile()
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

            Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
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
    public void FromSvg_IgnoresInlineStyleDeclarationsWithoutValues()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="60">
              <rect id="top" width="20" height="20" fill="rgb(0,0,255)" stroke="rgb(0,0,0)" style="fill:;stroke:rgb(255,255,255);" />
              <rect id="middle" y="20" width="20" height="20" fill="rgb(0,0,255)" stroke="rgb(0,0,0)" style="fill:rgb(244,58,32);stroke:rgb(255,255,255);" />
              <rect id="bottom" y="40" width="20" height="20" fill="rgb(0,0,255)" stroke="rgb(0,0,0)" style="fill: ;stroke:rgb(255,255,255);" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var top = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "top");
        var middle = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "middle");
        var bottom = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "bottom");
        var topFill = Assert.IsType<SvgColourServer>(top.Fill);
        var middleFill = Assert.IsType<SvgColourServer>(middle.Fill);
        var bottomFill = Assert.IsType<SvgColourServer>(bottom.Fill);
        var topStroke = Assert.IsType<SvgColourServer>(top.Stroke);
        var middleStroke = Assert.IsType<SvgColourServer>(middle.Stroke);
        var bottomStroke = Assert.IsType<SvgColourServer>(bottom.Stroke);

        Assert.Equal(Color.Blue.ToArgb(), topFill.Colour.ToArgb());
        Assert.Equal(Color.FromArgb(244, 58, 32).ToArgb(), middleFill.Colour.ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), bottomFill.Colour.ToArgb());
        Assert.Equal(Color.White.ToArgb(), topStroke.Colour.ToArgb());
        Assert.Equal(Color.White.ToArgb(), middleStroke.Colour.ToArgb());
        Assert.Equal(Color.White.ToArgb(), bottomStroke.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_IgnoresFallbackInlineStyleDeclarationsWithoutValues()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect id="target" width="10" height="10" fill="rgb(0,0,255)" stroke="rgb(0,0,0)" style="invalid; fill:; stroke:rgb(255,255,255);" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);
        var stroke = Assert.IsType<SvgColourServer>(rect.Stroke);

        Assert.Equal(Color.Blue.ToArgb(), fill.Colour.ToArgb());
        Assert.Equal(Color.White.ToArgb(), stroke.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_PreservesEmptyInlineCustomPropertyDeclarations()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <g style="--paint:rgb(0,255,0)">
                <rect id="target" width="10" height="10" fill="rgb(255,0,0)" style="--paint: ; fill:var(--paint, rgb(0,0,255));" />
              </g>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.Black.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_PreservesFallbackEmptyInlineCustomPropertyDeclarations()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <g style="--paint:rgb(0,255,0)">
                <rect id="target" width="10" height="10" fill="rgb(255,0,0)" style="invalid; --paint: ; fill:var(--paint, rgb(0,0,255));" />
              </g>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);

        Assert.Equal(Color.Black.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void FromSvg_IgnoresStylesheetDeclarationsWithoutValues()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <style>
                #target {
                  fill:;
                  stroke: rgb(255,255,255);
                }
              </style>
              <rect id="target" width="10" height="10" fill="rgb(0,0,255)" stroke="rgb(0,0,0)" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);
        var stroke = Assert.IsType<SvgColourServer>(rect.Stroke);

        Assert.Equal(Color.Blue.ToArgb(), fill.Colour.ToArgb());
        Assert.Equal(Color.White.ToArgb(), stroke.Colour.ToArgb());
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
    public void FromSvg_TreatsOpacity100PercentPresentationAttributeAsDefaultOpacity()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" opacity="100%">
              <rect width="10" height="10" fill="green" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);

        Assert.Equal(1f, document.Opacity, 3);
    }

    [Fact]
    public void FromSvg_NormalizesPercentageOpacityInlineStyles()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" style="opacity: 0.1%">
              <rect id="target"
                    width="10"
                    height="10"
                    stroke="#000000"
                    style="fill-opacity: 50%; stroke-opacity: 25%" />
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");

        Assert.Equal(0.001f, document.Opacity, 3);
        Assert.Equal(0.5f, rect.FillOpacity, 3);
        Assert.Equal(0.25f, rect.StrokeOpacity, 3);
    }

    [Fact]
    public void FromSvg_NormalizesPercentagePaintOpacityAttributesAndOverridesInheritance()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <g fill-opacity="0.3" stroke-opacity="0.4">
                <rect id="target"
                      width="10"
                      height="10"
                      stroke="#000000"
                      fill-opacity="100%"
                      stroke-opacity="100%" />
              </g>
            </svg>
            """;

        var document = SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg);
        var rect = document.Descendants().OfType<SvgRectangle>().Single(static element => element.ID == "target");

        Assert.Equal(1f, rect.FillOpacity, 3);
        Assert.Equal(1f, rect.StrokeOpacity, 3);
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
