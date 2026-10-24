using System;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorLayerStateTests
{
    private const string Source = "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><rect id='a' x='10' y='10' width='20' height='20'/><g id='layer'><rect id='b' x='40' y='10' width='20' height='20'/></g></svg>";

    [Fact]
    public void VisibilityChangesTheRequestedObjectWithoutSelectingIt()
    {
        using var editor = Create();
        var before = editor.GetSvg();
        var selected = Assert.Single(editor.SelectedElements);
        editor.SetHierarchyVisibility(editor.Document!.GetElementById<SvgRectangle>("b"), false);
        Assert.Same(selected, Assert.Single(editor.SelectedElements));
        Assert.Equal("none", editor.Document.GetElementById<SvgRectangle>("b").Display);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
        Assert.Equal("a", Assert.Single(editor.SelectedElements).ID);
    }

    [Fact]
    public void LockAndUnlockAreUndoableWithoutChangingSelection()
    {
        using var editor = Create();
        var target = editor.Document!.GetElementById<SvgRectangle>("b");
        editor.SetHierarchyLocked(target, true);
        Assert.True(SvgEditorController.IsLocked(target));
        Assert.Equal("a", Assert.Single(editor.SelectedElements).ID);
        editor.SetHierarchyLocked(target, false);
        Assert.False(SvgEditorController.IsLocked(target));
        Assert.Equal(2, editor.Session.UndoCount);
        editor.Undo();
        Assert.True(SvgEditorController.IsLocked(editor.Document.GetElementById<SvgRectangle>("b")));
    }

    [Fact]
    public void InheritedLockIsNotSilentlyClearedOnTheParent()
    {
        using var editor = Create();
        var layer = editor.Document!.GetElementById<SvgGroup>("layer");
        editor.SetHierarchyLocked(layer, true);
        var before = editor.GetSvg();
        var child = editor.Document.GetElementById<SvgRectangle>("b");
        Assert.Throws<InvalidOperationException>(() => editor.SetHierarchyLocked(child, false));
        Assert.Throws<InvalidOperationException>(() => editor.SetHierarchyVisibility(child, false));
        Assert.Equal(before, editor.GetSvg());
        Assert.Equal(1, editor.Session.UndoCount);
    }

    [Fact]
    public void StaleAndForeignTargetsFailBeforeHistoryMutation()
    {
        using var editor = Create();
        using var other = Create();
        var stale = editor.Document!.GetElementById<SvgRectangle>("b");
        editor.LoadSvg(Source);
        Assert.Throws<ArgumentException>(() => editor.SetHierarchyVisibility(stale, false));
        Assert.Throws<ArgumentException>(() => editor.SetHierarchyLocked(other.Document!.GetElementById("layer"), true));
        Assert.Throws<ArgumentException>(() => editor.SetHierarchyLocked(editor.Document!, true));
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void RepeatedStateAssignmentsDoNotCreateExtraHistory()
    {
        using var editor = Create();
        var target = editor.Document!.GetElementById<SvgRectangle>("b");
        editor.SetHierarchyVisibility(target, false);
        editor.SetHierarchyVisibility(target, false);
        editor.SetHierarchyVisibility(target, true);
        editor.SetHierarchyVisibility(target, true);
        editor.SetHierarchyLocked(target, true);
        editor.SetHierarchyLocked(target, true);
        editor.SetHierarchyLocked(target, false);
        editor.SetHierarchyLocked(target, false);
        Assert.Equal(4, editor.Session.UndoCount);
    }

    [Fact]
    public void LegacyOwnLockCanBeReleasedFromItsOutlineRow()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Source.Replace("id='layer'", "id='layer' data-lock='true'"));
        var layer = editor.Document!.GetElementById<SvgGroup>("layer");
        editor.SetHierarchyLocked(layer, false);
        Assert.False(SvgEditorController.IsLocked(layer));
        Assert.Equal("false", layer.CustomAttributes["data-lock"]);
        Assert.Empty(editor.SelectedElements);
    }

    private static SvgEditorController Create()
    {
        var editor = new SvgEditorController();
        editor.LoadSvg(Source);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("a"));
        return editor;
    }
}
