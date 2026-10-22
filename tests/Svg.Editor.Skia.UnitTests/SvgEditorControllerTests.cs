using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Svg.Editor.Svg;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorControllerTests
{
    private const string RectangleSvg = "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='200'><rect id='shape' x='10' y='15' width='30' height='40' fill='red'/></svg>";

    [Fact]
    public void GestureCreatesOneHistoryEntryAndCancelRestoresStart()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(RectangleSvg);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("shape"));
        var before = editor.GetSvg();
        editor.BeginEdit("Drag");
        editor.MoveSelection(10, 0);
        editor.MoveSelection(0, 20);
        editor.CommitEdit();
        Assert.Equal(1, editor.Session.UndoCount);
        var moved = editor.GetSvg();
        editor.BeginEdit("Drag again");
        editor.MoveSelection(100, 100);
        editor.CancelEdit();
        Assert.Equal(moved, editor.GetSvg());
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void InvalidSourceNeverReplacesCurrentDocument()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(RectangleSvg);
        var document = editor.Document;
        var picture = editor.Renderer.Picture;
        Assert.Throws<XmlException>(() => editor.LoadSvg("<svg><broken></svg>"));
        Assert.Throws<XmlException>(() => editor.LoadSvg("<html/>"));
        Assert.Same(document, editor.Document);
        Assert.Same(picture, editor.Renderer.Picture);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void EmptyTransactionPreservesRedoAndDirtyCheckpoint()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(RectangleSvg);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("shape"));
        editor.MoveSelection(10, 0);
        editor.Undo();
        editor.Execute("No change", () => { });
        Assert.False(editor.IsDirty);
        Assert.True(editor.CanRedo);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void DuplicateRemapsSubtreeIdsAndInternalReferences()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="200" height="200">
              <g id="group">
                <defs><linearGradient id="paint"><stop offset="0" stop-color="red"/><stop offset="1" stop-color="blue"/></linearGradient></defs>
                <rect id="shape" width="30" height="40" fill="url(#paint)"/>
                <use id="instance" xlink:href="#shape" x="50"/>
              </g>
            </svg>
            """);
        editor.Select(editor.Document!.GetElementById<SvgGroup>("group"));
        editor.DuplicateSelection();
        var ids = editor.Document.Descendants().Select(e => e.ID).Where(id => !string.IsNullOrEmpty(id)).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        var xml = XDocument.Parse(editor.GetSvg());
        var clone = xml.Descendants().Single(e => (string?)e.Attribute("id") == "group-copy");
        Assert.Contains("url(#paint-copy)", clone.Descendants().Single(e => (string?)e.Attribute("id") == "shape-copy").ToString());
        Assert.Contains(clone.Descendants().Single(e => (string?)e.Attribute("id") == "instance-copy").Attributes(),
            a => a.Name.LocalName == "href" && a.Value == "#shape-copy");
        Assert.Equal("group-copy", Assert.Single(editor.SelectedElements).ID);
        Assert.Equal(1, editor.Session.UndoCount);
    }

    [Fact]
    public void ClipboardCarriesPaintResourcesAcrossDocuments()
    {
        using var source = new SvgEditorController();
        source.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200">
              <defs><linearGradient id="paint"><stop offset="0" stop-color="red"/><stop offset="1" stop-color="blue"/></linearGradient></defs>
              <g transform="translate(20 30)" fill="url(#paint)"><rect id="shape" width="30" height="40"/></g>
            </svg>
            """);
        source.Select(source.Document!.GetElementById<SvgRectangle>("shape"));
        var clipboard = source.CopySelection();
        using var destination = new SvgEditorController();
        destination.NewDocument(200, 200);
        destination.Paste(clipboard, 0, 0);
        Assert.NotNull(destination.Document!.GetElementById<SvgLinearGradientServer>("paint-copy"));
        var pasted = Assert.Single(destination.SelectedElements);
        var scene = destination.GetSceneNode(pasted)!;
        Assert.InRange(scene.TransformedBounds.Left, 19.99f, 20.01f);
        Assert.InRange(scene.TransformedBounds.Top, 29.99f, 30.01f);
        Assert.Contains("url(#paint-copy)", destination.GetSvg());
        Assert.Equal(1, destination.Session.UndoCount);
    }

    [Fact]
    public void UngroupPreservesInheritedPaintAndWorldTransform()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="300" height="200">
              <g id="group" transform="translate(30 20) scale(2)" fill="blue" stroke="red" stroke-width="3">
                <rect id="shape" x="10" y="5" width="30" height="40"/>
              </g>
            </svg>
            """);
        var shape = editor.Document!.GetElementById<SvgRectangle>("shape");
        var bounds = editor.GetSceneNode(shape)!.TransformedBounds;
        var fill = shape.Fill.ToString();
        editor.Select(editor.Document.GetElementById<SvgGroup>("group"));
        editor.UngroupSelection();
        Assert.Same(editor.Document, shape.Parent);
        Assert.Equal(fill, shape.Fill.ToString());
        Assert.Equal(3, shape.StrokeWidth.Value);
        Assert.Equal(bounds, editor.GetSceneNode(shape)!.TransformedBounds);
    }

    [Fact]
    public void GroupCompositingIsNotSilentlyLostByUngroup()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><g id='group' opacity='0.5'><rect width='20' height='20'/></g></svg>");
        editor.Select(editor.Document!.GetElementById<SvgGroup>("group"));
        var before = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.UngroupSelection());
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void BooleanUnionUsesWorldGeometryOfTransformedOperands()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="300" height="200">
              <g transform="translate(40 20) scale(2)"><rect id="a" width="20" height="20"/></g>
              <rect id="b" x="70" y="20" width="30" height="40"/>
            </svg>
            """);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("a"));
        editor.Select(editor.Document.GetElementById<SvgRectangle>("b"), true);
        editor.ApplyBoolean(SKPathOp.Union);
        var path = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        var bounds = editor.GetSceneNode(path)!.TransformedBounds;
        Assert.InRange(bounds.Left, 39.99f, 40.01f);
        Assert.InRange(bounds.Right, 99.99f, 100.01f);
        Assert.InRange(bounds.Top, 19.99f, 20.01f);
        Assert.InRange(bounds.Bottom, 59.99f, 60.01f);
        Assert.Null(editor.Document.GetElementById<SvgRectangle>("b"));
        editor.Undo();
        Assert.NotNull(editor.Document.GetElementById<SvgRectangle>("b"));
    }

    [Fact]
    public void CreatedObjectsHaveUniqueIdsAndUndoRestoresDocument()
    {
        using var editor = new SvgEditorController();
        editor.NewDocument();
        var original = editor.GetSvg();
        editor.CreateElement(ToolService.Tool.Rect, new SKPoint(10, 10), new SKPoint(60, 70));
        editor.CreateElement(ToolService.Tool.Rect, new SKPoint(20, 20), new SKPoint(70, 80));
        var objects = editor.Document!.Children.OfType<SvgRectangle>().ToArray();
        Assert.Equal(2, objects.Length);
        Assert.NotEqual(objects[0].ID, objects[1].ID);
        Assert.Equal(50, objects[0].Width.Value);
        Assert.Equal(60, objects[0].Height.Value);
        editor.Undo();
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void OutlineCanSelectLockedLegacyObjectsForUnlocking()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><g data-lock='true'><rect id='shape' width='20' height='20'/></g></svg>");
        var shape = editor.Document!.GetElementById<SvgRectangle>("shape");
        editor.Select(shape);
        Assert.Empty(editor.SelectedElements);
        editor.Select(shape, allowLocked: true);
        Assert.Same(shape, Assert.Single(editor.SelectedElements));
    }
    [Fact]
    public void SourceReplacementIsUndoableAndKeepsFileAndSavepoint()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(RectangleSvg, "/tmp/artwork.svg");
        var before = editor.GetSvg();
        editor.ReplaceSvg(RectangleSvg.Replace("fill='red'", "fill='blue'"));
        Assert.Equal("/tmp/artwork.svg", editor.Session.CurrentFile);
        Assert.True(editor.IsDirty);
        Assert.Equal(1, editor.Session.UndoCount);
        var current = editor.Document;
        Assert.Throws<XmlException>(() => editor.ReplaceSvg("<svg><broken></svg>"));
        Assert.Same(current, editor.Document);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.IsDirty);
        editor.MarkUnsaved();
        Assert.True(editor.IsDirty);
    }

}
