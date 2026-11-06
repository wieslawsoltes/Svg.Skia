using System;
using System.IO;
using System.Linq;
using System.Xml;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorSaveContinuityTests
{
    private const string Source = "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><rect id='art' width='40' height='30' fill='red'/></svg>";
    private static string Location(string name = "drawing.svg") => Path.Combine(Path.GetTempPath(), "unodraw-save-continuity", name);

    [Fact]
    public void SameUriSavePreservesDocumentRendererSelectionAndRevisions()
    {
        using var editor = Create();
        editor.MoveSelection(4, 6);
        var document = editor.Document; var renderer = editor.Renderer; var picture = renderer.Picture;
        var selection = editor.SelectedElements.ToArray(); var revision = editor.DocumentRevision; var renderRevision = editor.RenderRevision;
        var undo = editor.Session.UndoCount; var xml = editor.GetSvg(); var notifications = 0;
        editor.Changed += (_, _) => notifications++;
        editor.AcceptSave(xml, Location());
        Assert.Same(document, editor.Document); Assert.Same(renderer, editor.Renderer); Assert.Same(picture, editor.Renderer.Picture);
        Assert.Equal(selection, editor.SelectedElements.ToArray()); Assert.Equal(revision, editor.DocumentRevision);
        Assert.Equal(renderRevision, editor.RenderRevision); Assert.Equal(undo, editor.Session.UndoCount);
        Assert.Equal(xml, editor.GetSvg()); Assert.False(editor.IsDirty); Assert.Equal(1, notifications);
    }

    [Fact]
    public void SavingLiveSourceDoesNotInvalidateAnUnappliedGradientDraft()
    {
        using var editor = Create();
        var before = editor.GetSvg(); var draft = editor.BeginGradientEdit();
        draft.Ramp = draft.Ramp.Reverse();
        editor.AcceptSave(before, Location());
        Assert.True(draft.IsCurrent(editor)); Assert.Equal(before, editor.GetSvg()); Assert.False(editor.CanUndo);
        editor.ApplyGradientEdit(draft);
        Assert.Equal(1, editor.Session.UndoCount); Assert.True(editor.IsDirty);
        editor.Undo(); Assert.Equal(before, editor.GetSvg()); Assert.False(editor.IsDirty);
    }

    [Fact]
    public void OlderCompletedSaveDoesNotDiscardEditsMadeDuringItsWrite()
    {
        using var editor = Create();
        var saved = editor.GetSvg(); editor.MoveSelection(5, 0);
        var newer = editor.GetSvg(); var document = editor.Document; var renderer = editor.Renderer;
        editor.AcceptSave(saved, Location());
        Assert.True(editor.IsDirty); Assert.Equal(newer, editor.GetSvg());
        Assert.Same(document, editor.Document); Assert.Same(renderer, editor.Renderer);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo(); Assert.Equal(saved, editor.GetSvg()); Assert.False(editor.IsDirty);
    }

    [Fact]
    public void SaveCompletionDoesNotCancelAnInFlightGesture()
    {
        using var editor = Create();
        var saved = editor.GetSvg(); editor.BeginEdit("Pending move"); editor.MoveSelection(8, 0);
        var document = editor.Document; var renderer = editor.Renderer; var preview = editor.GetSvg();
        editor.AcceptSave(saved, Location());
        Assert.True(editor.IsEditing); Assert.True(editor.IsDirty); Assert.Equal(preview, editor.GetSvg());
        Assert.Same(document, editor.Document); Assert.Same(renderer, editor.Renderer); Assert.False(editor.CanUndo);
        editor.CancelEdit(); Assert.Equal(saved, editor.GetSvg()); Assert.False(editor.IsDirty);
    }

    [Fact]
    public void SameUriSavePreservesRedoAndAnimationTime()
    {
        // Static renderers intentionally have no animation clock. Use a real native
        // SVG animation so this assertion proves playhead continuity rather than
        // requesting a nonzero time from a non-animated fixture.
        using var editor = Create("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
              <rect id="art" width="40" height="30" fill="red">
                <animate attributeName="opacity" values="1;0.5;1" dur="4s" repeatCount="indefinite"/>
              </rect>
            </svg>
            """);
        editor.MoveSelection(3, 0); editor.Undo();
        var time = TimeSpan.FromSeconds(1.25); editor.Renderer.SetAnimationTime(time);
        Assert.Equal(time, editor.Renderer.AnimationTime);
        var renderer = editor.Renderer;
        editor.AcceptSave(editor.GetSvg(), Location());
        Assert.True(editor.CanRedo); Assert.False(editor.CanUndo);
        Assert.Same(renderer, editor.Renderer); Assert.Equal(time, editor.Renderer.AnimationTime);
        editor.Redo(); Assert.True(editor.IsDirty);
    }

    [Fact]
    public void InvalidSavedSnapshotCannotChangeTheSavepointOrLiveReferences()
    {
        using var editor = Create(); editor.MoveSelection(2, 0);
        var document = editor.Document; var renderer = editor.Renderer; var before = editor.GetSvg();
        Assert.Throws<XmlException>(() => editor.AcceptSave("<not-svg/>", Location()));
        Assert.Same(document, editor.Document); Assert.Same(renderer, editor.Renderer);
        Assert.Equal(before, editor.GetSvg()); Assert.True(editor.IsDirty); Assert.Equal(1, editor.Session.UndoCount);
    }

    [Fact]
    public void DifferentDestinationStillRelocatesAndInvalidatesOldDraftContexts()
    {
        using var editor = Create();
        var document = editor.Document; var revision = editor.DocumentRevision; var draft = editor.BeginGradientEdit();
        var destination = Location(Path.Combine("elsewhere", "drawing.svg"));
        editor.AcceptSave(editor.GetSvg(), destination);
        Assert.NotSame(document, editor.Document); Assert.True(editor.DocumentRevision > revision);
        Assert.Equal(new Uri(Path.GetFullPath(destination)), editor.Document!.BaseUri);
        Assert.False(draft.IsCurrent(editor)); Assert.False(editor.IsDirty);
    }

    [Fact]
    public void SameUriSaveKeepsNodeEditingHandlesAndAnonymousSelection()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><path d='M0 0L30 0L30 30Z'/></svg>", Location());
        var path = editor.Document!.Children.OfType<SvgPath>().Single(); editor.Select(path);
        editor.Paths.Start(path, editor.GetSceneNode(path)!);
        editor.AcceptSave(editor.GetSvg(), Location());
        Assert.True(editor.Paths.IsEditing); Assert.Same(path, editor.Paths.EditPath);
        Assert.Same(path, Assert.Single(editor.SelectedElements));
    }

    private static SvgEditorController Create(string source = Source)
    {
        var editor = new SvgEditorController(); editor.LoadSvg(source, Location());
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("art")); return editor;
    }
}
