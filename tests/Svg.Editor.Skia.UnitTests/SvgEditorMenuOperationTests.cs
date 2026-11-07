using System;
using System.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorMenuOperationTests
{
    private const string Artwork = """
        <svg xmlns="http://www.w3.org/2000/svg" width="300" height="200">
          <defs><g><rect id="resource" width="10" height="10" fill="red"/></g></defs>
          <g id="group" transform="translate(40 20)">
            <rect id="one" width="20" height="20" fill="red"/>
            <rect id="two" x="30" width="20" height="20" fill="blue"/>
          </g>
          <rect id="three" x="100" width="20" height="20" fill="red"/>
          <rect id="hidden" x="140" width="20" height="20" fill="red" display="none"/>
          <g id="locked" data-unodraw-locked="true"><rect id="locked-child" x="170" width="20" height="20" fill="red"/></g>
        </svg>
        """;

    [Fact]
    public void InverseKeepsGroupBoundariesAndExcludesResourcesHiddenAndLocked()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Artwork);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("one"));
        var before = editor.GetSvg();
        var revision = editor.RenderRevision;
        editor.InvertSelection();
        Assert.Equal(new[] { "two", "three" }, editor.SelectedElements.Select(element => element.ID));
        Assert.Equal(before, editor.GetSvg());
        Assert.Equal(revision, editor.RenderRevision);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void SelectSameFillIncludesNestedArtworkWithoutSelectingItsContainer()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Artwork);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("one"));
        editor.SelectMatching(SvgSelectionMatch.Fill);
        Assert.Equal(new[] { "one", "three" }, editor.SelectedElements.Select(element => element.ID));
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void UnlockAllIsAtomicAndRestoresBothLockFormatsOnUndo()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Artwork.Replace("id=\"group\"", "id=\"group\" data-lock=\"true\""));
        var before = editor.GetSvg();
        editor.UnlockAll();
        Assert.All(editor.Document!.Descendants(), element => Assert.False(SvgEditorController.IsLocked(element)));
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void ShowAllPreservesHiddenDefinitionsAndLockedArtwork()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Artwork.Replace("id=\"resource\"", "id=\"resource\" display=\"none\"")
            .Replace("id=\"locked-child\"", "id=\"locked-child\" display=\"none\""));
        editor.ShowAll();
        Assert.Equal("inline", editor.Document!.GetElementById<SvgRectangle>("hidden").Display);
        Assert.Equal("none", editor.Document.GetElementById<SvgRectangle>("resource").Display);
        Assert.Equal("none", editor.Document.GetElementById<SvgRectangle>("locked-child").Display);
        Assert.Equal(1, editor.Session.UndoCount);
    }

    [Theory]
    [InlineData(SvgPastePlacement.InFront, 1)]
    [InlineData(SvgPastePlacement.Behind, 0)]
    public void PasteRelativePreservesWorldLocationAndCreatesOneHistoryEntry(SvgPastePlacement placement, int expectedIndex)
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Artwork);
        var original = editor.Document!.GetElementById<SvgRectangle>("one");
        editor.Select(original);
        var bounds = editor.SelectionBounds!.Value;
        var clipboard = editor.CopySelection();
        var before = editor.GetSvg();
        editor.PasteAt(clipboard, placement);
        var pasted = Assert.Single(editor.SelectedElements);
        Assert.Same(original.Parent, pasted.Parent);
        Assert.Equal(expectedIndex, pasted.Parent!.Children.IndexOf(pasted));
        var actual = editor.SelectionBounds!.Value;
        Assert.InRange(Math.Abs(actual.Left - bounds.Left), 0, 0.001);
        Assert.InRange(Math.Abs(actual.Top - bounds.Top), 0, 0.001);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void PasteInPlaceHasNoLegacySixteenUnitOffset()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Artwork);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("three"));
        var bounds = editor.SelectionBounds;
        editor.PasteAt(editor.CopySelection(), SvgPastePlacement.InPlace);
        Assert.Equal(bounds, editor.SelectionBounds);
    }

    [Fact]
    public void RepeatTransformIgnoresCanceledPreviewAndCombinesCommittedGesture()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Artwork);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("three"));
        editor.BeginEdit("Move gesture");
        editor.MoveSelection(10, 0);
        editor.MoveSelection(0, 20);
        editor.CommitEdit();
        editor.BeginEdit("Canceled gesture");
        editor.TransformSelection(SKMatrix.CreateScale(5, 5));
        editor.CancelEdit();
        var before = editor.SelectionBounds!.Value;
        editor.RepeatTransform();
        var after = editor.SelectionBounds!.Value;
        Assert.Equal(before.Left + 10, after.Left, 3);
        Assert.Equal(before.Top + 20, after.Top, 3);
        Assert.Equal(before.Width, after.Width, 3);
        Assert.Equal(2, editor.Session.UndoCount);
    }

    [Fact]
    public void LoadingAnotherDocumentClearsRepeatTransform()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Artwork);
        editor.SelectAll();
        editor.MoveSelection(1, 1);
        Assert.True(editor.CanRepeatTransform);
        editor.LoadSvg(Artwork);
        editor.SelectAll();
        Assert.False(editor.CanRepeatTransform);
    }

    [Fact]
    public void TypeSelectionDoesNotTouchDocumentOrSelectHiddenObjects()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Artwork);
        editor.SelectObjectsOfType(typeof(SvgRectangle));
        Assert.Equal(new[] { "one", "two", "three" }, editor.SelectedElements.Select(element => element.ID));
        Assert.False(editor.IsDirty);
        Assert.Throws<ArgumentException>(() => editor.SelectObjectsOfType(typeof(SvgElement)));
    }
}
