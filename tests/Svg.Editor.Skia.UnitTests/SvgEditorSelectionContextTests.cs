using System;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorSelectionContextTests
{
    private const string Source = "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><rect id='a' x='10' y='10' width='20' height='20'/><rect id='b' x='40' y='10' width='20' height='20'/></svg>";

    [Fact]
    public void CapturingAndCheckingDoNotMutateDocumentHistoryOrPicture()
    {
        using var editor = Create();
        var before = editor.GetSvg();
        var picture = editor.Renderer.Picture;
        var context = new SvgEditorSelectionContext(editor);
        Assert.True(context.IsCurrent(editor));
        context.Validate(editor);
        Assert.Equal(before, editor.GetSvg());
        Assert.Same(picture, editor.Renderer.Picture);
        Assert.False(editor.CanUndo);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void GeometryEditsRetainTheTargetUntilUndoReplacesItsIdentity()
    {
        using var editor = Create();
        var context = new SvgEditorSelectionContext(editor);
        editor.MoveSelection(5, 0);
        Assert.True(context.IsCurrent(editor));
        editor.Undo();
        Assert.False(context.IsCurrent(editor));
        Assert.Throws<InvalidOperationException>(() => context.Validate(editor));
    }

    [Fact]
    public void AnotherControllerWithIdenticalSourceIsNotTheSameTarget()
    {
        using var first = Create();
        using var second = Create();
        var context = new SvgEditorSelectionContext(first);
        Assert.False(context.IsCurrent(second));
        Assert.False(context.IsCurrent(null));
        Assert.True(context.IsCurrent(first));
    }

    [Fact]
    public void SelectionReplacementAndDeletionInvalidateTheContext()
    {
        using var editor = Create();
        var context = new SvgEditorSelectionContext(editor);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("b"));
        Assert.False(context.IsCurrent(editor));
        context = new SvgEditorSelectionContext(editor);
        editor.DeleteSelection();
        Assert.False(context.IsCurrent(editor));
    }

    [Fact]
    public void IdenticalReloadDoesNotReuseBorrowedObjectIdentities()
    {
        using var editor = Create();
        var context = new SvgEditorSelectionContext(editor);
        editor.LoadSvg(Source);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("a"));
        Assert.False(context.IsCurrent(editor));
    }

    [Fact]
    public void SelectionOrderIsPartOfTheTargetContext()
    {
        using var editor = Create();
        var a = editor.Document!.GetElementById<SvgRectangle>("a");
        var b = editor.Document.GetElementById<SvgRectangle>("b");
        editor.Select(b, true);
        var context = new SvgEditorSelectionContext(editor);
        editor.ClearSelection();
        editor.Select(b);
        editor.Select(a, true);
        Assert.False(context.IsCurrent(editor));
    }

    [Fact]
    public void AnEditorWithoutADocumentCannotBeAnEditableTarget()
    {
        using var editor = new SvgEditorController();
        Assert.False(new SvgEditorSelectionContext(editor).IsCurrent(editor));
        Assert.Throws<ArgumentNullException>(() => new SvgEditorSelectionContext(null!));
    }

    private static SvgEditorController Create()
    {
        var editor = new SvgEditorController();
        editor.LoadSvg(Source);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("a"));
        return editor;
    }
}
