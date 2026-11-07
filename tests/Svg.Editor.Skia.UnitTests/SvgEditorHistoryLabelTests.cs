using System;
using Svg.Editor.Core;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorHistoryLabelTests
{
    [Fact]
    public void LabelsFollowSnapshotsThroughUndoRedoAndBranching()
    {
        var session = new SvgEditorSession();
        session.PushUndoState("zero", "Draw rectangle");
        session.PushUndoState("one", "Change fill");
        Assert.Equal(new[] { "Change fill", "Draw rectangle" }, session.UndoLabels);
        Assert.True(session.TryUndo("two", out var state));
        Assert.Equal("one", state);
        Assert.Equal(new[] { "Draw rectangle" }, session.UndoLabels);
        Assert.Equal(new[] { "Change fill" }, session.RedoLabels);
        Assert.True(session.TryUndo("one", out state));
        Assert.Equal("zero", state);
        Assert.Equal(new[] { "Draw rectangle", "Change fill" }, session.RedoLabels);
        Assert.True(session.TryRedo("zero", out state));
        Assert.Equal("one", state);
        Assert.Equal(new[] { "Draw rectangle" }, session.UndoLabels);
        session.PushUndoState("one", "Move rectangle");
        Assert.Empty(session.RedoLabels);
        Assert.Equal(0, session.RedoCount);
        Assert.Equal(new[] { "Move rectangle", "Draw rectangle" }, session.UndoLabels);
    }

    [Fact]
    public void SnapshotRelocationPreservesLabelsAndClearRemovesThem()
    {
        var session = new SvgEditorSession();
        session.PushUndoState("zero", "Create");
        session.PushUndoState("one", "Paint");
        session.TryUndo("two", out _);
        session.TransformHistory(value => "relocated-" + value);
        Assert.Equal(new[] { "Create" }, session.UndoLabels);
        Assert.Equal(new[] { "Paint" }, session.RedoLabels);
        Assert.True(session.TryRedo("relocated-one", out var state));
        Assert.Equal("relocated-two", state);
        session.ClearHistory();
        Assert.Empty(session.UndoLabels);
        Assert.Empty(session.RedoLabels);
    }

    [Fact]
    public void LegacySnapshotApiHasSafeLabelsAndRedoWithoutSnapshotStaysAligned()
    {
        var session = new SvgEditorSession();
        session.PushUndoState("before");
        Assert.Equal(new[] { "Edit" }, session.UndoLabels);
        session.TryUndo("after", out _);
        Assert.True(session.TryRedo(null, out _));
        Assert.Empty(session.UndoLabels);
        Assert.Empty(session.RedoLabels);
        Assert.Equal(0, session.UndoCount);
        Assert.Equal(0, session.RedoCount);
    }

    [Fact]
    public void ControllerRecordsOnlyCommittedOperationsAndLoadResetsHistory()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg'><rect id='r' width='20' height='10'/></svg>");
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("r"));
        editor.Execute("No change", () => { });
        Assert.Empty(editor.Session.UndoLabels);
        editor.BeginEdit("Reposition artwork");
        editor.MoveSelection(4, 5);
        editor.MoveSelection(1, 2);
        editor.CommitEdit();
        Assert.Equal(new[] { "Reposition artwork" }, editor.Session.UndoLabels);
        editor.BeginEdit("Cancelled gesture");
        editor.MoveSelection(100, 100);
        editor.CancelEdit();
        Assert.Equal(new[] { "Reposition artwork" }, editor.Session.UndoLabels);
        Assert.Throws<InvalidOperationException>(() => editor.Execute("Failed edit", () => throw new InvalidOperationException()));
        Assert.Equal(new[] { "Reposition artwork" }, editor.Session.UndoLabels);
        editor.Undo();
        Assert.Equal(new[] { "Reposition artwork" }, editor.Session.RedoLabels);
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg'/>");
        Assert.Empty(editor.Session.UndoLabels);
        Assert.Empty(editor.Session.RedoLabels);
    }
}
