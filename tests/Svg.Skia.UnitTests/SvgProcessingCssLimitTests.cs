using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgProcessingCssLimitTests
{
    private const int MaximumCssDepth = 64;
    private const int MaximumCssImportDepth = 32;

    public enum UnsafeStylesheet
    {
        NestedMedia,
        NestedFunctions,
        NestedPseudoClasses,
        CompoundSelector,
        ComplexSelector,
        BadStringFollowedByNesting,
        MismatchedDelimiters
    }

    public static TheoryData<UnsafeStylesheet, bool> UnsafeStylesheets
    {
        get
        {
            var data = new TheoryData<UnsafeStylesheet, bool>();
            foreach (var kind in Enum.GetValues<UnsafeStylesheet>())
            {
                data.Add(kind, false);
                data.Add(kind, true);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(UnsafeStylesheets))]
    public void Stylesheets_RejectExcessiveRecursionBeforeParsing(UnsafeStylesheet kind, bool compatibilityLoader)
    {
        var css = kind switch
        {
            UnsafeStylesheet.NestedMedia => NestedMedia(2048),
            UnsafeStylesheet.NestedFunctions => "#target { fill: " + Repeat("rgb(", 2048) + "0" + new string(')', 2048) + "; }",
            UnsafeStylesheet.NestedPseudoClasses => Repeat(":not(", 2048) + "rect" + new string(')', 2048) + " { fill: green; }",
            UnsafeStylesheet.CompoundSelector => Repeat(".a", 2048) + " { fill: green; }",
            UnsafeStylesheet.ComplexSelector => Repeat("g > ", 2048) + "rect { fill: green; }",
            UnsafeStylesheet.BadStringFollowedByNesting => "#target { font-family: \"bad\n" + new string('(', 2048) + "x; }",
            UnsafeStylesheet.MismatchedDelimiters => Repeat("@media all { ]", 2048) + "#target { fill: green; }",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        Assert.Throws<SvgProcessingLimitException>(() => LoadStylesheet(css, compatibilityLoader));

        // A rejected source must not leak a processing/import scope into the next load.
        AssertGreen(LoadStylesheet("#target { fill: green; }", compatibilityLoader));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InlineStyles_RejectNestedFunctionsBeforeConversion(bool compatibilityLoader)
    {
        var value = Repeat("calc(", 2048) + "1px" + new string(')', 2048);
        var svg = "<svg xmlns='http://www.w3.org/2000/svg'><rect style='width:" + value + "' /></svg>";

        Assert.Throws<SvgProcessingLimitException>(() => LoadSvg(svg, compatibilityLoader));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stylesheets_AcceptNestingAtTheBoundary(bool compatibilityLoader)
    {
        // The final style-rule block contributes one level in addition to @media.
        var document = LoadStylesheet(NestedMedia(MaximumCssDepth - 1), compatibilityLoader);

        Assert.NotNull(document.GetElementById("target"));
        if (compatibilityLoader)
        {
            AssertGreen(document);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stylesheets_RejectOneLevelPastTheBoundary(bool compatibilityLoader)
    {
        Assert.Throws<SvgProcessingLimitException>(() => LoadStylesheet(NestedMedia(MaximumCssDepth), compatibilityLoader));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stylesheets_DoNotCountQuotedCommentedOrEscapedDelimiters(bool compatibilityLoader)
    {
        var punctuation = Repeat("([{}])", 2048);
        var css = "/* " + punctuation + " */\n" +
                  "[data-label=\"" + punctuation + "\"] { fill: red; }\n" +
                  "#target { fill: green; }\n" +
                  ".escaped" + Repeat("\\(\\[\\{", 512) + " { fill: red; }";

        AssertGreen(LoadStylesheet(css, compatibilityLoader));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stylesheets_AcceptWideSelectorLists(bool compatibilityLoader)
    {
        var css = string.Join(",", Enumerable.Repeat("#target", 2048)) + " { fill: green; }";

        AssertGreen(LoadStylesheet(css, compatibilityLoader));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stylesheets_AcceptOrdinaryCompoundSelectors(bool compatibilityLoader)
    {
        var css = Repeat(".a", 64) + " { fill: green; }";

        AssertGreen(LoadStylesheet(css, compatibilityLoader));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stylesheets_DoNotCountTheSyntheticSelectorRootAsInputDepth(bool compatibilityLoader)
    {
        var svg = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg'><style>#target { fill: green; }</style>");
        svg.Append(Repeat("<g>", 254));
        svg.Append("<rect id='target' width='1' height='1' fill='red'/>");
        svg.Append(Repeat("</g>", 254));
        svg.Append("</svg>");

        AssertGreen(LoadSvg(svg.ToString(), compatibilityLoader));
    }

    [Fact]
    public void Imports_RejectLongAcyclicChainsAndRecover()
    {
        WithStylesheetDirectory(directory =>
        {
            WriteImportChain(directory, MaximumCssImportDepth + 1);
            var svgPath = WriteImportingSvg(directory, "@import '0.css';");

            Assert.Throws<SvgProcessingLimitException>(() => SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions()));

            File.WriteAllText(Path.Combine(directory, "0.css"), "#target { fill: green; }");
            AssertGreen(SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions()));
        });
    }

    [Fact]
    public void Imports_AcceptTheDocumentedDepthBoundary()
    {
        WithStylesheetDirectory(directory =>
        {
            WriteImportChain(directory, MaximumCssImportDepth);
            var svgPath = WriteImportingSvg(directory, "@import '0.css';");

            AssertGreen(SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions()));
        });
    }

    [Fact]
    public void Imports_DoNotCountSiblingsAsDepthOrLoseCascadeOrder()
    {
        WithStylesheetDirectory(directory =>
        {
            File.WriteAllText(Path.Combine(directory, "green.css"), "#target { fill: green; }");
            File.WriteAllText(Path.Combine(directory, "red.css"), "#target { fill: red; }");
            var svgPath = WriteImportingSvg(directory, Repeat("@import 'green.css'; @import 'red.css'; ", 64) + "@import 'green.css';");

            AssertGreen(SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions()));
        });
    }

    [Fact]
    public void Imports_PreserveCycleRecovery()
    {
        WithStylesheetDirectory(directory =>
        {
            File.WriteAllText(Path.Combine(directory, "first.css"), "@import 'second.css'; #target { fill: green; }");
            File.WriteAllText(Path.Combine(directory, "second.css"), "@import 'first.css'; #target { fill: red; }");
            var svgPath = WriteImportingSvg(directory, "@import 'first.css';");

            AssertGreen(SvgDocumentCompatibilityLoader.Open<SvgDocument>(svgPath, new SvgOptions()));
        });
    }

    private static SvgDocument LoadStylesheet(string css, bool compatibilityLoader)
    {
        return LoadSvg(CreateSvg(css), compatibilityLoader);
    }

    private static SvgDocument LoadSvg(string svg, bool compatibilityLoader)
    {
        return compatibilityLoader
            ? SvgDocumentCompatibilityLoader.FromSvg<SvgDocument>(svg)
            : SvgDocument.FromSvg<SvgDocument>(svg);
    }

    private static string CreateSvg(string css)
    {
        return "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><style><![CDATA[" + css +
               "]]></style><rect id='target' class='a' width='10' height='10' fill='red'/></svg>";
    }

    private static string NestedMedia(int count)
    {
        return Repeat("@media all {", count) + "#target { fill: green; }" + new string('}', count);
    }

    private static string Repeat(string value, int count)
    {
        var builder = new StringBuilder(value.Length * count);
        for (var index = 0; index < count; index++)
        {
            builder.Append(value);
        }

        return builder.ToString();
    }

    private static void AssertGreen(SvgDocument document)
    {
        var rectangle = Assert.IsType<SvgRectangle>(document.GetElementById("target"));
        var fill = Assert.IsType<SvgColourServer>(rectangle.Fill);
        Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
    }

    private static void WithStylesheetDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "svg-css-depth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            action(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void WriteImportChain(string directory, int count)
    {
        for (var index = 0; index < count; index++)
        {
            var css = index + 1 < count
                ? "@import '" + (index + 1) + ".css';"
                : "#target { fill: green; }";
            File.WriteAllText(Path.Combine(directory, index + ".css"), css);
        }
    }

    private static string WriteImportingSvg(string directory, string css)
    {
        var path = Path.Combine(directory, "source.svg");
        File.WriteAllText(path, CreateSvg(css));
        return path;
    }
}
