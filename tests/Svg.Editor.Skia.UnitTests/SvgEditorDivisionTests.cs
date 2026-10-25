using System;
using System.Linq;
using System.Threading;
using SkiaSharp;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorDivisionTests
{
    private const string Overlap = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
          <rect id="back" x="10" y="10" width="40" height="40" fill="red"/>
          <rect id="front" x="30" y="10" width="40" height="40" fill="blue"/>
        </svg>
        """;

    [Fact]
    public void DivideCreatesThreePathsInOneUndoableTransaction()
    {
        using var editor = LoadAndSelect(Overlap, "back", "front");
        var before = editor.GetSvg();
        editor.DivideSelection();
        Assert.Equal(3, editor.SelectedElements.Count);
        Assert.All(editor.SelectedElements, element => Assert.IsType<SvgPath>(element));
        Assert.Equal(1, editor.Session.UndoCount);
        Assert.True(editor.IsDirty);
        Assert.Equal("Divide paths", editor.LastEditLabel);
        Assert.NotNull(editor.Document!.GetElementById("back"));
        Assert.NotNull(editor.Document.GetElementById("front"));
        var ids = editor.Document.Descendants().Select(element => element.ID)
            .Where(id => !string.IsNullOrEmpty(id)).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        var after = editor.GetSvg();
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.IsDirty);
        editor.Redo();
        Assert.Equal(after, editor.GetSvg());
    }

    [Fact]
    public void PaintOrderDoesNotDependOnSelectionOrder()
    {
        using var forward = LoadAndSelect(Overlap, "back", "front");
        using var reverse = LoadAndSelect(Overlap, "front", "back");
        forward.DivideSelection();
        reverse.DivideSelection();
        Assert.Equal(forward.GetSvg(), reverse.GetSvg());
        using var pixels = Render(reverse);
        Assert.Equal(SKColors.Red, pixels.GetPixel(20, 30));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(40, 30));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(60, 30));
    }

    [Fact]
    public void SourceAndParentTransformsSurviveDivideAndReopen()
    {
        using var editor = LoadAndSelect("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
              <g id="layer" transform="translate(10 10) scale(2)">
                <rect id="back" width="20" height="20" fill="red"/>
                <rect id="front" width="20" height="20" transform="translate(10 0)" fill="blue"/>
              </g>
            </svg>
            """, "back", "front");
        editor.DivideSelection();
        var parent = editor.Document!.GetElementById("layer");
        Assert.All(editor.SelectedElements, path => Assert.Same(parent, path.Parent));
        using var pixels = Render(editor);
        Assert.Equal(SKColors.Red, pixels.GetPixel(20, 30));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(40, 30));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(60, 30));
        using var reopened = new SvgEditorController();
        reopened.LoadSvg(editor.GetSvg());
        using var again = Render(reopened);
        Assert.Equal(pixels.Pixels, again.Pixels);
    }

    [Fact]
    public void CompoundHolesSurviveInheritedEvenOddAndRepeatedReopen()
    {
        using var editor = LoadAndSelect("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80" fill-rule="evenodd">
              <path id="ring" fill="red" d="M10 10H70V70H10Z M20 20H60V60H20Z"/>
              <rect id="bar" x="35" y="5" width="10" height="70" fill="blue"/>
            </svg>
            """, "ring", "bar");
        editor.DivideSelection();
        for (var pass = 0; pass < 4; pass++)
        {
            using var pixels = Render(editor);
            Assert.Equal((byte)0, pixels.GetPixel(25, 40).Alpha);
            Assert.Equal(SKColors.Blue, pixels.GetPixel(40, 40));
            Assert.Equal(SKColors.Red, pixels.GetPixel(15, 40));
            editor.LoadSvg(editor.GetSvg());
        }
    }

    [Fact]
    public void UnselectedSiblingsKeepTheirStackingPosition()
    {
        using var editor = LoadAndSelect("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
              <rect id="back" x="10" y="10" width="40" height="40" fill="red"/>
              <rect id="middle" x="10" y="20" width="60" height="10" fill="lime"/>
              <rect id="front" x="30" y="10" width="40" height="40" fill="blue"/>
            </svg>
            """, "back", "front");
        editor.DivideSelection();
        using var pixels = Render(editor);
        Assert.Equal(SKColors.Lime, pixels.GetPixel(20, 25));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(40, 25));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(60, 25));
    }

    [Fact]
    public void PieceBudgetFailureLeavesDocumentSelectionHistoryAndPictureUntouched()
    {
        using var editor = LoadAndSelect(Overlap, "back", "front");
        var before = editor.GetSvg();
        var selected = editor.SelectedElements.ToArray();
        var picture = editor.Renderer.Picture;
        var revision = editor.RenderRevision;
        Assert.Throws<InvalidOperationException>(() => editor.DivideSelection(2));
        Assert.Equal(before, editor.GetSvg());
        Assert.Equal(selected, editor.SelectedElements.ToArray());
        Assert.Same(picture, editor.Renderer.Picture);
        Assert.Equal(revision, editor.RenderRevision);
        Assert.False(editor.IsDirty);
        Assert.False(editor.CanUndo);
        editor.DivideSelection(3);
        Assert.Equal(3, editor.SelectedElements.Count);
    }

    [Fact]
    public void CancellationDoesNotCreateAHistoryEntry()
    {
        using var editor = LoadAndSelect(Overlap, "back", "front");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var before = editor.GetSvg();
        Assert.Throws<OperationCanceledException>(() =>
            editor.DivideSelection(cancellationToken: cancellation.Token));
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.CanUndo);
    }

    [Theory]
    [InlineData("<g><rect id='back' width='30' height='30'/></g><rect id='front' width='30' height='30'/>")]
    [InlineData("<rect id='back' width='30%' height='30'/><rect id='front' width='30' height='30'/>")]
    [InlineData("<rect id='back' width='30' height='30' transform='scale(0)'/><rect id='front' width='30' height='30'/>")]
    [InlineData("<rect id='back' width='30' height='30'><title>Retain metadata</title></rect><rect id='front' width='30' height='30'/>")]
    [InlineData("<defs><filter id='f'><feOffset dx='2'/></filter></defs><rect id='back' width='30' height='30' filter='url(#f)'/><rect id='front' width='30' height='30'/>")]
    public void UnsupportedOperandsFailBeforeMutation(string content)
    {
        using var editor = LoadAndSelect(
            "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'>" + content + "</svg>", "back", "front");
        var before = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.DivideSelection());
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.IsDirty);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void EmptyOrSingleSelectionDoesNotDiscardRedo()
    {
        using var editor = LoadAndSelect(Overlap, "back", "front");
        editor.MoveSelection(1, 0);
        editor.Undo();
        editor.ClearSelection();
        editor.DivideSelection();
        editor.Select(editor.Document!.GetElementById<SvgVisualElement>("back"));
        editor.DivideSelection();
        Assert.True(editor.CanRedo);
        Assert.False(editor.CanUndo);
        Assert.False(editor.IsDirty);
    }

    private static SvgEditorController LoadAndSelect(string xml, params string[] ids)
    {
        var editor = new SvgEditorController();
        editor.LoadSvg(xml);
        foreach (var id in ids)
            editor.Select(editor.Document!.GetElementById<SvgVisualElement>(id), true);
        return editor;
    }

    private static SKBitmap Render(SvgEditorController editor)
    {
        var bitmap = new SKBitmap(100, 80);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        editor.Renderer.Draw(canvas);
        return bitmap;
    }
}
