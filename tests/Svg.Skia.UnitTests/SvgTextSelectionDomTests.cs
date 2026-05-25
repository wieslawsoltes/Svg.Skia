using System.Linq;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Svg;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgTextSelectionDomTests
{
    [Fact]
    public void SelectSubString_RecordsLogicalSelectionExtents()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="80" viewBox="0 0 240 80"
                 onload="document.getElementById('label').selectSubString(1, 3)">
              <text id="label" x="10" y="40" font-family="sans-serif" font-size="20">ABCDE</text>
            </svg>
            """);

        var selection = Assert.Single(svg.TextSelections);
        Assert.Equal("label", selection.ElementId);
        Assert.Equal(1, selection.Charnum);
        Assert.Equal(3, selection.NChars);
        Assert.NotEmpty(selection.Extents);
        Assert.All(selection.Extents, extent => Assert.False(extent.IsEmpty));
    }

    [Fact]
    public void SelectSubString_RendersSelectionHighlightIntoModel()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.Settings.TextSelectionColor = new SkiaSharp.SKColor(10, 20, 30, 255);

        svg.FromSvg(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="80" viewBox="0 0 240 80"
                 onload="document.getElementById('label').selectSubString(1, 3)">
              <g transform="translate(4, 5)">
                <text id="label" x="10" y="40" font-family="sans-serif" font-size="20">ABCDE</text>
              </g>
            </svg>
            """);

        var model = Assert.IsType<SKPicture>(svg.Model);
        var selectionCommand = Assert.Single(
            model.FindCommands<DrawPathCanvasCommand>(),
            static command => command.SourceElementTypeName == "SvgTextSelection");

        Assert.Equal("label", selectionCommand.SourceElementId);
        Assert.False(selectionCommand.Path!.IsEmpty);
        Assert.Equal(new SKColor(10, 20, 30, 255), selectionCommand.Paint!.Color);
    }

    [Fact]
    public void SelectSubString_ClampsLargeCountWithoutOverflow()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="80" viewBox="0 0 240 80"
                 onload="document.getElementById('label').selectSubString(1, 2147483647)">
              <text id="label" x="10" y="40" font-family="sans-serif" font-size="20">ABCDE</text>
            </svg>
            """);

        var selection = Assert.Single(svg.TextSelections);
        Assert.Equal("label", selection.ElementId);
        Assert.Equal(1, selection.Charnum);
        Assert.Equal(int.MaxValue, selection.NChars);
        Assert.NotEmpty(selection.Extents);
    }

    [Fact]
    public void TextSelections_ReturnSnapshotExtents()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="80" viewBox="0 0 240 80"
                 onload="document.getElementById('label').selectSubString(1, 3)">
              <text id="label" x="10" y="40" font-family="sans-serif" font-size="20">ABCDE</text>
            </svg>
            """);

        var selection = Assert.Single(svg.TextSelections);
        var original = selection.Extents[0];
        if (selection.Extents is ShimSkiaSharp.SKRect[] mutableExtents)
        {
            mutableExtents[0] = ShimSkiaSharp.SKRect.Empty;
        }

        Assert.Equal(original, Assert.Single(svg.TextSelections).Extents[0]);
    }

    [Fact]
    public void SelectSubString_InvalidStartThrowsDomExceptionAndKeepsPreviousSelection()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="260" height="80" viewBox="0 0 260 80"
                 onload="runSelection()">
              <script><![CDATA[
                function runSelection() {
                  var text = document.getElementById('label');
                  text.selectSubString(0, 2);
                  try {
                    text.selectSubString(99, 1);
                  } catch (e) {
                    document.getElementById('status').firstChild.data = e.code;
                  }
                }
              ]]></script>
              <text id="label" x="10" y="40" font-family="sans-serif" font-size="20">ABCDE</text>
              <text id="status" x="10" y="70" font-family="sans-serif" font-size="10">0</text>
            </svg>
            """);
        var document = svg.SourceDocument!;

        var selection = Assert.Single(svg.TextSelections);
        Assert.Equal("label", selection.ElementId);
        Assert.Equal(0, selection.Charnum);
        Assert.Equal(2, selection.NChars);
        Assert.Equal("1", document.GetElementById("status")!.Nodes.OfType<SvgContentNode>().Single().Content);
    }
}
