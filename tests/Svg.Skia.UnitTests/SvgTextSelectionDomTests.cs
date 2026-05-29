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
        Assert.Equal(1, selection.StartCharnum);
        Assert.Equal(4, selection.EndCharnum);
        Assert.Equal(3, selection.SelectedNChars);
        Assert.Equal(1, selection.AnchorCharnum);
        Assert.Equal(3, selection.FocusCharnum);
        Assert.Equal(SKSvg.SvgTextSelectionDirection.Forward, selection.Direction);
        Assert.True(selection.HasCaret);
        Assert.NotEmpty(selection.Extents);
        Assert.NotEmpty(selection.VisualExtents);
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
    public void TextSelections_ExposeVisualOrderExtents()
    {
        using var svg = new SKSvg();
        svg.FromSvg(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="80" viewBox="0 0 140 80">
              <text id="label" font-family="sans-serif" font-size="20">
                <tspan x="90" y="40">A</tspan><tspan x="10" y="40">B</tspan>
              </text>
            </svg>
            """);

        svg.SelectTextSubString(GetTextElement(svg, "label"), 0, 2);

        var selection = Assert.Single(svg.TextSelections);
        Assert.Equal(2, selection.Extents.Count);
        Assert.Equal(2, selection.VisualExtents.Count);
        Assert.True(selection.Extents[0].Left > selection.Extents[1].Left);
        Assert.True(selection.VisualExtents[0].Left < selection.VisualExtents[1].Left);
    }

    [Fact]
    public void SelectSubString_BidiLogicalRangeRendersDiscontiguousVisualHighlights()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="480" height="180" viewBox="0 0 480 180"
                 onload="document.getElementById('text').selectSubString(0, 9)">
              <text id="text" x="10" y="100" font-family="sans-serif" font-size="48">abc אבג 123 דהו def</text>
            </svg>
            """);

        var selection = Assert.Single(svg.TextSelections);
        Assert.Equal("text", selection.ElementId);
        Assert.Equal(0, selection.Charnum);
        Assert.Equal(9, selection.NChars);
        Assert.Equal(3, selection.Extents.Count);
        Assert.Equal(3, selection.VisualExtents.Count);
        Assert.True(selection.Extents[1].Left > selection.Extents[2].Left);
        Assert.True(selection.VisualExtents[0].Right < selection.VisualExtents[1].Left);
        Assert.True(selection.VisualExtents[1].Right < selection.VisualExtents[2].Left);

        var command = Assert.Single(FindSelectionCommands(Assert.IsType<SKPicture>(svg.Model)));
        var rects = command.Path!.Commands!.OfType<AddRectPathCommand>().Select(static command => command.Rect).ToArray();
        Assert.Equal(3, rects.Length);
        Assert.Equal(selection.VisualExtents.Select(static extent => extent.Left).ToArray(), rects.Select(static rect => rect.Left).ToArray());
    }

    [Fact]
    public void SelectTextSubString_MultilineTspanRangeRendersOneHighlightPerLine()
    {
        using var svg = new SKSvg();
        svg.FromSvg(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="320" height="140" viewBox="0 0 320 140">
              <text id="text" font-family="sans-serif" font-size="20">
                <tspan x="20" y="40">First line</tspan>
                <tspan x="20" y="75">Second line</tspan>
              </text>
            </svg>
            """);

        var text = GetTextElement(svg, "text");
        svg.SelectTextSubString(text, 0, text.Text.Length);

        var selection = Assert.Single(svg.TextSelections);
        Assert.Equal(2, selection.Extents.Count);
        Assert.True(selection.VisualExtents[0].Bottom < selection.VisualExtents[1].Top);

        var command = Assert.Single(FindSelectionCommands(Assert.IsType<SKPicture>(svg.Model)));
        var rects = command.Path!.Commands!.OfType<AddRectPathCommand>().ToArray();
        Assert.Equal(2, rects.Length);
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

    [Fact]
    public void SelectSubString_ReplacesPreviousSelection()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="260" height="100" viewBox="0 0 260 100"
                 onload="runSelection()">
              <script><![CDATA[
                function runSelection() {
                  document.getElementById('first').selectSubString(0, 2);
                  document.getElementById('second').selectSubString(1, 3);
                }
              ]]></script>
              <text id="first" x="10" y="35" font-family="sans-serif" font-size="20">ABCDE</text>
              <text id="second" x="10" y="75" font-family="sans-serif" font-size="20">VWXYZ</text>
            </svg>
            """);

        var selection = Assert.Single(svg.TextSelections);
        Assert.Equal("second", selection.ElementId);
        Assert.Equal(1, selection.Charnum);
        Assert.Equal(3, selection.NChars);
        var command = Assert.Single(FindSelectionCommands(Assert.IsType<SKPicture>(svg.Model)));
        Assert.Equal("second", command.SourceElementId);
    }

    [Fact]
    public void SelectSubString_ZeroLengthClearsPreviousSelection()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="80" viewBox="0 0 240 80"
                 onload="runSelection()">
              <script><![CDATA[
                function runSelection() {
                  var text = document.getElementById('label');
                  text.selectSubString(0, 2);
                  text.selectSubString(1, 0);
                }
              ]]></script>
              <text id="label" x="10" y="40" font-family="sans-serif" font-size="20">ABCDE</text>
            </svg>
            """);

        Assert.Empty(svg.TextSelections);
        Assert.Empty(FindSelectionCommands(Assert.IsType<SKPicture>(svg.Model)));
    }

    [Fact]
    public void SelectTextSubString_ReplacesPreviousSelectionAndHighlight()
    {
        using var svg = CreateTwoLabelSvg();
        var first = GetTextElement(svg, "first");
        var second = GetTextElement(svg, "second");

        svg.SelectTextSubString(first, 0, 2);

        var firstSelection = Assert.Single(svg.TextSelections);
        Assert.Equal("first", firstSelection.ElementId);
        var firstCommand = Assert.Single(FindSelectionCommands(Assert.IsType<SKPicture>(svg.Model)));
        Assert.Equal("first", firstCommand.SourceElementId);

        svg.SelectTextSubString(second, 1, 3);

        var secondSelection = Assert.Single(svg.TextSelections);
        Assert.Equal("second", secondSelection.ElementId);
        Assert.Equal(1, secondSelection.Charnum);
        Assert.Equal(3, secondSelection.NChars);
        var secondCommand = Assert.Single(FindSelectionCommands(Assert.IsType<SKPicture>(svg.Model)));
        Assert.Equal("second", secondCommand.SourceElementId);
    }

    [Fact]
    public void SelectTextSubString_ZeroLengthClearsSelectionAndInvalidatesHighlight()
    {
        using var svg = CreateTwoLabelSvg();
        var first = GetTextElement(svg, "first");

        svg.SelectTextSubString(first, 0, 2);
        var modelWithSelection = Assert.IsType<SKPicture>(svg.Model);
        var pictureWithSelection = svg.Picture;
        Assert.NotNull(pictureWithSelection);
        Assert.Single(FindSelectionCommands(modelWithSelection));

        svg.SelectTextSubString(first, 1, 0);

        Assert.Empty(svg.TextSelections);
        Assert.Empty(FindSelectionCommands(Assert.IsType<SKPicture>(svg.Model)));
        Assert.NotSame(modelWithSelection, svg.Model);
        Assert.NotSame(pictureWithSelection, svg.Picture);
    }

    [Fact]
    public void ClearTextSelection_RemovesHighlightAndInvalidatesCachedPicture()
    {
        using var svg = CreateTwoLabelSvg();
        var first = GetTextElement(svg, "first");

        svg.SelectTextSubString(first, 0, 2);
        var modelWithSelection = Assert.IsType<SKPicture>(svg.Model);
        var pictureWithSelection = svg.Picture;
        Assert.NotNull(pictureWithSelection);
        Assert.Single(FindSelectionCommands(modelWithSelection));

        svg.ClearTextSelection();

        Assert.Empty(svg.TextSelections);
        Assert.Empty(FindSelectionCommands(Assert.IsType<SKPicture>(svg.Model)));
        Assert.NotSame(modelWithSelection, svg.Model);
        Assert.NotSame(pictureWithSelection, svg.Picture);
    }

    [Fact]
    public void TryBeginTextSelection_CreatesQueryableCollapsedCaretWithoutHighlight()
    {
        using var svg = CreateTwoLabelSvg();
        var first = GetTextElement(svg, "first");

        Assert.True(svg.TryBeginTextSelection(first, 2));

        var selection = Assert.Single(svg.TextSelections);
        Assert.True(svg.HasTextSelection);
        Assert.True(svg.TryGetTextSelection(out var activeSelection));
        Assert.Equal(selection.Charnum, activeSelection.Charnum);
        Assert.Equal("first", selection.ElementId);
        Assert.Equal(2, selection.Charnum);
        Assert.Equal(0, selection.NChars);
        Assert.Equal(2, selection.AnchorCharnum);
        Assert.Equal(2, selection.FocusCharnum);
        Assert.Equal(SKSvg.SvgTextSelectionDirection.None, selection.Direction);
        Assert.True(selection.IsCollapsed);
        Assert.True(selection.HasCaret);
        Assert.Empty(selection.Extents);
        Assert.Empty(FindSelectionCommands(Assert.IsType<SKPicture>(svg.Model)));
    }

    [Fact]
    public void TryExtendTextSelection_UsesStoredAnchorAndRefreshesHighlight()
    {
        using var svg = CreateTwoLabelSvg();
        var first = GetTextElement(svg, "first");

        Assert.True(svg.TryBeginTextSelection(first, 1));
        Assert.True(svg.TryExtendTextSelection(first, 4));

        var selection = Assert.Single(svg.TextSelections);
        Assert.Equal("first", selection.ElementId);
        Assert.Equal(1, selection.Charnum);
        Assert.Equal(4, selection.NChars);
        Assert.Equal(1, selection.AnchorCharnum);
        Assert.Equal(4, selection.FocusCharnum);
        Assert.Equal(SKSvg.SvgTextSelectionDirection.Forward, selection.Direction);
        Assert.False(selection.IsCollapsed);
        Assert.NotEmpty(selection.VisualExtents);
        Assert.Single(FindSelectionCommands(Assert.IsType<SKPicture>(svg.Model)));
    }

    [Fact]
    public void TryExtendTextSelection_BackToAnchorReturnsCollapsedCaretAndClearsHighlight()
    {
        using var svg = CreateTwoLabelSvg();
        var first = GetTextElement(svg, "first");

        Assert.True(svg.TryBeginTextSelection(first, 3));
        Assert.True(svg.TryExtendTextSelection(first, 1));
        Assert.NotEmpty(FindSelectionCommands(Assert.IsType<SKPicture>(svg.Model)));

        Assert.True(svg.TryExtendTextSelection(first, 3));

        var selection = Assert.Single(svg.TextSelections);
        Assert.True(selection.IsCollapsed);
        Assert.Equal(3, selection.AnchorCharnum);
        Assert.Equal(3, selection.FocusCharnum);
        Assert.Empty(FindSelectionCommands(Assert.IsType<SKPicture>(svg.Model)));
    }

    [Fact]
    public void TryGetTextSelection_CanQueryByElement()
    {
        using var svg = CreateTwoLabelSvg();
        var first = GetTextElement(svg, "first");
        var second = GetTextElement(svg, "second");

        Assert.True(svg.TrySelectTextRange(first, 4, 1));

        Assert.True(svg.TryGetTextSelection(first, out var firstSelection));
        Assert.False(svg.TryGetTextSelection(second, out _));
        Assert.Equal("first", firstSelection.ElementId);
        Assert.Equal(SKSvg.SvgTextSelectionDirection.Backward, firstSelection.Direction);
    }

    [Fact]
    public void JavaScriptTextSelectionHelpers_ExposeCaretRangeAndDocumentClear()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="280" height="110" viewBox="0 0 280 110"
                 onload="runSelection()">
              <script><![CDATA[
                function runSelection() {
                  var text = document.getElementById('label');
                  var began = text.beginTextSelection(2);
                  var caret = text.getTextSelection();
                  var extended = text.extendTextSelection(4);
                  var range = document.getTextSelection();
                  document.getElementById('status').firstChild.data = [
                    began,
                    caret.isCollapsed,
                    caret.anchorCharnum,
                    caret.focusCharnum,
                    extended,
                    range.elementId,
                    range.charnum,
                    range.nchars,
                    range.extents.length,
                    range.direction
                  ].join('|');
                  document.clearTextSelection();
                  document.getElementById('cleared').firstChild.data = text.getTextSelection() === null ? 'cleared' : 'active';
                }
              ]]></script>
              <text id="label" x="10" y="40" font-family="sans-serif" font-size="20">ABCDE</text>
              <text id="status" x="10" y="75" font-family="sans-serif" font-size="10">pending</text>
              <text id="cleared" x="10" y="95" font-family="sans-serif" font-size="10">pending</text>
            </svg>
            """);

        var document = svg.SourceDocument!;
        Assert.Equal(
            "true|true|2|2|true|label|2|3|1|forward",
            document.GetElementById("status")!.Nodes.OfType<SvgContentNode>().Single().Content);
        Assert.Equal("cleared", document.GetElementById("cleared")!.Nodes.OfType<SvgContentNode>().Single().Content);
        Assert.Empty(svg.TextSelections);
        Assert.False(svg.HasTextSelection);
    }

    [Fact]
    public void SelectTextSubString_InvalidRangeKeepsPreviousSelection()
    {
        using var svg = CreateTwoLabelSvg();
        var first = GetTextElement(svg, "first");
        var second = GetTextElement(svg, "second");

        svg.SelectTextSubString(first, 0, 2);

        Assert.Throws<System.ArgumentOutOfRangeException>(() => svg.SelectTextSubString(second, 99, 1));

        var selection = Assert.Single(svg.TextSelections);
        Assert.Equal("first", selection.ElementId);
        Assert.Equal(0, selection.Charnum);
        Assert.Equal(2, selection.NChars);
        var command = Assert.Single(FindSelectionCommands(Assert.IsType<SKPicture>(svg.Model)));
        Assert.Equal("first", command.SourceElementId);
    }

    [Fact]
    public void TrySelectTextSubString_InvalidRangeReturnsFalseAndKeepsPreviousSelection()
    {
        using var svg = CreateTwoLabelSvg();
        var first = GetTextElement(svg, "first");
        var second = GetTextElement(svg, "second");

        Assert.True(svg.TrySelectTextSubString(first, 0, 2));

        Assert.False(svg.TrySelectTextSubString(second, 99, 1));

        var selection = Assert.Single(svg.TextSelections);
        Assert.Equal("first", selection.ElementId);
        Assert.Equal(0, selection.Charnum);
        Assert.Equal(2, selection.NChars);
    }

    [Fact]
    public void TrySelectTextRange_ComposesBackwardRangeWithCaretMetadata()
    {
        using var svg = CreateTwoLabelSvg();
        var first = GetTextElement(svg, "first");

        Assert.True(svg.TrySelectTextRange(first, 4, 1));

        var selection = Assert.Single(svg.TextSelections);
        Assert.Equal("first", selection.ElementId);
        Assert.Equal(1, selection.Charnum);
        Assert.Equal(4, selection.NChars);
        Assert.Equal(1, selection.StartCharnum);
        Assert.Equal(5, selection.EndCharnum);
        Assert.Equal(4, selection.AnchorCharnum);
        Assert.Equal(1, selection.FocusCharnum);
        Assert.Equal(SKSvg.SvgTextSelectionDirection.Backward, selection.Direction);
        Assert.True(selection.HasCaret);
        Assert.False(selection.CaretExtent.IsEmpty);
    }

    [Fact]
    public void TrySelectTextRange_FromPointsUsesHitCharacters()
    {
        using var svg = CreateTwoLabelSvg();
        var first = GetTextElement(svg, "first");

        Assert.True(svg.TrySelectTextRange(first, new SKPoint(12, 30), new SKPoint(48, 30)));

        var selection = Assert.Single(svg.TextSelections);
        Assert.Equal("first", selection.ElementId);
        Assert.Equal(0, selection.Charnum);
        Assert.True(selection.NChars >= 2);
        Assert.True(selection.HasCaret);
    }

    [Fact]
    public void EventDrivenMutation_RecomputesSelectionExtentsBeforeHighlightRendering()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="80" viewBox="0 0 180 80"
                 onload="document.getElementById('label').selectSubString(0, 2)">
              <rect id="button" x="0" y="0" width="30" height="30" fill="blue"
                    onclick="document.getElementById('label').setAttribute('x', '80')" />
              <text id="label" x="10" y="60" font-family="sans-serif" font-size="20">ABCDE</text>
            </svg>
            """);

        var beforeSelection = Assert.Single(svg.TextSelections);
        var beforeLeft = beforeSelection.Extents[0].Left;
        var beforeModel = Assert.IsType<SKPicture>(svg.Model);

        var dispatcher = new SvgInteractionDispatcher();
        var input = CreateInput(10, 10);
        dispatcher.DispatchPointerPressed(svg, input);
        dispatcher.DispatchPointerReleased(svg, input);

        var afterSelection = Assert.Single(svg.TextSelections);
        Assert.True(afterSelection.Extents[0].Left > beforeLeft + 40f);
        Assert.Single(FindSelectionCommands(Assert.IsType<SKPicture>(svg.Model)));
        Assert.NotSame(beforeModel, svg.Model);
    }

    private static SKSvg CreateTwoLabelSvg()
    {
        var svg = new SKSvg();
        svg.FromSvg(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="260" height="100" viewBox="0 0 260 100">
              <text id="first" x="10" y="35" font-family="sans-serif" font-size="20">ABCDE</text>
              <text id="second" x="10" y="75" font-family="sans-serif" font-size="20">VWXYZ</text>
            </svg>
            """);
        return svg;
    }

    private static SvgTextBase GetTextElement(SKSvg svg, string elementId)
    {
        return Assert.IsAssignableFrom<SvgTextBase>(svg.SourceDocument!.GetElementById(elementId));
    }

    private static DrawPathCanvasCommand[] FindSelectionCommands(SKPicture model)
    {
        return model
            .FindCommands<DrawPathCanvasCommand>()
            .Where(static command => command.SourceElementTypeName == "SvgTextSelection")
            .ToArray();
    }

    private static SvgPointerInput CreateInput(float x, float y)
    {
        return new SvgPointerInput(
            new SKPoint(x, y),
            SvgPointerDeviceType.Mouse,
            SvgMouseButton.Left,
            clickCount: 1,
            wheelDelta: 0,
            altKey: false,
            shiftKey: false,
            ctrlKey: false,
            sessionId: "test");
    }
}
