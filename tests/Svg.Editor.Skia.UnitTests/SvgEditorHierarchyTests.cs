using System;
using System.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorHierarchyTests
{
    [Fact]
    public void ReparentRetainsWorldBoundsPaintIdentityAndUndo()
    {
        using var editor = Load("<g id='old' transform='translate(40 20) scale(2)' fill='red'><rect id='shape' x='5' y='8' width='20' height='30'/></g><g id='target' transform='translate(150 90) rotate(30)' fill='blue'/>");
        var shape = editor.Document!.GetElementById<SvgRectangle>("shape");
        var target = editor.Document.GetElementById("target");
        editor.Select(shape);
        var bounds = editor.SelectionBounds!.Value;
        var before = editor.GetSvg();
        editor.ReparentSelection(target);
        Assert.Same(shape, editor.Document.GetElementById("shape"));
        Assert.Same(target, shape.Parent);
        Assert.Equal("Red", ((SvgColourServer)shape.Fill).Colour.Name);
        AssertBounds(bounds, editor.SelectionBounds!.Value);
        Assert.Equal(1, editor.Session.UndoCount);
        var after = editor.GetSvg();
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
        editor.Redo();
        Assert.Equal(after, editor.GetSvg());
    }

    [Fact]
    public void MultipleBranchesKeepDocumentPainterOrderAndDeduplicateDescendants()
    {
        using var editor = Load("<g id='a'><rect id='child' width='5' height='5'/></g><rect id='b' width='10' height='10'/><g id='target'/>");
        var doc = editor.Document!;
        var a = doc.GetElementById("a");
        var b = doc.GetElementById("b");
        var child = doc.GetElementById("child");
        var target = doc.GetElementById("target");
        editor.MoveHierarchyElements(new[] { b, child, a, a }, target);
        Assert.Equal(new[] { "a", "b" }, target.Children.Select(element => element.ID));
        Assert.Same(a, child.Parent);
        Assert.Equal(2, editor.SelectedElements.Count);
    }

    [Fact]
    public void ReorderIndexUsesDestinationAfterRemovalAndNoOpHasNoHistory()
    {
        using var editor = Load("<rect id='a'/><rect id='b'/><rect id='c'/><rect id='d'/>");
        var doc = editor.Document!;
        editor.MoveHierarchyElements(new[] { doc.GetElementById("b"), doc.GetElementById("d") }, doc, 0);
        Assert.Equal(new[] { "b", "d", "a", "c" }, doc.Children.Select(element => element.ID));
        editor.MoveHierarchyElements(new[] { doc.GetElementById("b"), doc.GetElementById("d") }, doc, 0);
        Assert.Equal(1, editor.Session.UndoCount);
    }

    [Theory]
    [InlineData("self")]
    [InlineData("descendant")]
    [InlineData("locked-source")]
    [InlineData("locked-target")]
    [InlineData("singular")]
    [InlineData("invalid-container")]
    public void InvalidMovesAreAtomic(string scenario)
    {
        using var editor = Load("<g id='group'><rect id='shape' width='20' height='20'/><g id='child'/></g><g id='target'/>");
        var doc = editor.Document!;
        var group = doc.GetElementById("group");
        var target = doc.GetElementById("target");
        switch (scenario)
        {
            case "self":
                target = group;
                break;
            case "descendant":
                target = doc.GetElementById("child");
                break;
            case "locked-source":
                group.CustomAttributes["data-unodraw-locked"] = "true";
                break;
            case "locked-target":
                target.CustomAttributes["data-unodraw-locked"] = "true";
                break;
            case "singular":
                ((SvgGroup)target).Transforms = new global::Svg.Transforms.SvgTransformCollection { new global::Svg.Transforms.SvgScale(0) };
                editor.Refresh();
                break;
            case "invalid-container":
                target = doc.GetElementById("shape");
                break;
        }
        var before = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.MoveHierarchyElements(new[] { group }, target));
        Assert.Equal(before, editor.GetSvg());
        Assert.Equal(0, editor.Session.UndoCount);
    }

    [Fact]
    public void HiddenBranchCanMoveWithoutBecomingVisible()
    {
        using var editor = Load("<g id='old' transform='translate(40 20)'><rect id='hidden' display='none' width='20' height='10'/></g><g id='target' transform='scale(2)'/>");
        var hidden = editor.Document!.GetElementById<SvgRectangle>("hidden");
        editor.MoveHierarchyElements(new[] { hidden }, editor.Document.GetElementById("target"));
        Assert.Equal("none", hidden.Display);
        Assert.Equal("target", hidden.Parent.ID);
        Assert.Contains("matrix", hidden.Transforms.ToString());
    }

    [Fact]
    public void ResourcesReorderWithoutRenamingReferences()
    {
        using var editor = Load("<defs id='resources'><linearGradient id='a'/><linearGradient id='b'/></defs><rect id='shape' width='20' height='20' fill='url(#a)'/>");
        var doc = editor.Document!;
        var a = doc.GetElementById("a");
        var defs = doc.GetElementById("resources");
        editor.MoveHierarchyElements(new[] { a }, defs);
        Assert.Equal(new[] { "b", "a" }, defs.Children.Select(element => element.ID));
        Assert.Contains("url(#a)", editor.GetSvg());
    }

    private static SvgEditorController Load(string content)
    {
        var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='400' height='300'>" + content + "</svg>");
        return editor;
    }

    private static void AssertBounds(SKRect expected, SKRect actual)
    {
        Assert.InRange(Math.Abs(expected.Left - actual.Left), 0, 0.001);
        Assert.InRange(Math.Abs(expected.Top - actual.Top), 0, 0.001);
        Assert.InRange(Math.Abs(expected.Right - actual.Right), 0, 0.001);
        Assert.InRange(Math.Abs(expected.Bottom - actual.Bottom), 0, 0.001);
    }
}
