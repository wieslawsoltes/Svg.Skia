using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Core;
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
    public void PointerSelectsTopmostNestedArtworkAndSkipsLockedAncestors()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200">
              <g><rect id="background" width="200" height="200" fill="white"/></g>
              <g transform="translate(20 10)">
                <rect id="middle" x="20" y="20" width="100" height="100" fill="blue"/>
                <g id="foreground"><circle id="sun" cx="70" cy="70" r="25" fill="orange"/></g>
              </g>
            </svg>
            """);
        var revision = editor.RenderRevision;
        editor.SelectAt(new SKPoint(90, 80));
        Assert.Equal("sun", Assert.Single(editor.SelectedElements).ID);
        Assert.Equal(revision, editor.RenderRevision);
        editor.Execute("Lock foreground", () =>
            editor.Document!.GetElementById("foreground").CustomAttributes["data-lock"] = "true");
        editor.SelectAt(new SKPoint(90, 80));
        Assert.Equal("middle", Assert.Single(editor.SelectedElements).ID);
        editor.SelectAt(new SKPoint(5, 5));
        Assert.Equal("background", Assert.Single(editor.SelectedElements).ID);
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
    public void ClipboardPreservesSharedAncestorCompositingAndRootPresentation()
    {
        using var source = new SvgEditorController();
        source.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" fill="red" opacity="0.8">
              <defs>
                <clipPath id="crop"><rect x="5" y="5" width="55" height="55"/></clipPath>
                <filter id="offset"><feOffset dx="3" dy="2"/></filter>
              </defs>
              <g id="shared" opacity="0.5" clip-path="url(#crop)" filter="url(#offset)" transform="translate(5 5)">
                <rect id="first" width="35" height="35"/>
                <rect id="second" x="20" y="20" width="35" height="35"/>
                <circle id="unselected" cx="80" cy="80" r="4"/>
              </g>
            </svg>
            """);
        source.Select(source.Document!.GetElementById<SvgRectangle>("first"));
        source.Select(source.Document.GetElementById<SvgRectangle>("second"), true);
        var original = source.GetSvg();
        using var destination = new SvgEditorController();
        destination.NewDocument(100, 100);
        destination.Paste(source.CopySelection(), 0, 0);
        Assert.Null(destination.Document!.GetElementById("unselected-copy"));
        var shared = destination.Document.GetElementById<SvgGroup>("shared-copy");
        Assert.Equal(2, shared.Children.OfType<SvgRectangle>().Count());
        Assert.Single(destination.SelectedElements);
        using var expected = new SKBitmap(100, 100);
        using var actual = new SKBitmap(100, 100);
        using (var canvas = new SKCanvas(expected))
        {
            canvas.Clear(SKColors.Transparent);
            source.Renderer.Draw(canvas);
        }
        using (var canvas = new SKCanvas(actual))
        {
            canvas.Clear(SKColors.Transparent);
            destination.Renderer.Draw(canvas);
        }
        Assert.InRange(actual.GetPixel(30, 30).Alpha, (byte)95, (byte)110);
        for (var y = 0; y < 70; y++)
            for (var x = 0; x < 70; x++)
                Assert.True(expected.GetPixel(x, y) == actual.GetPixel(x, y),
                    $"Pixel ({x}, {y}): expected {expected.GetPixel(x, y)}, actual {actual.GetPixel(x, y)}. {destination.GetSvg()}");
        Assert.Equal(original, source.GetSvg());
        Assert.False(source.IsDirty);
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

    [Fact]
    public void AcceptSaveRelocatesUndoRedoAndKeepsNewerEditsDirty()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="200" height="200">
              <a id="link" xlink:href="assets/page.svg"><rect id="shape" width="30" height="40"/></a>
            </svg>
            """, "/tmp/unodraw-save/source/drawing.svg");
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("shape"));
        editor.MoveSelection(10, 0);
        var saved = SvgResourcePathService.Rebase(editor.GetSvg(), editor.Document.BaseUri,
            new Uri("file:///tmp/unodraw-save/destination/copy.svg"));
        editor.MoveSelection(20, 0);
        editor.MoveSelection(30, 0);
        editor.Undo();
        var undoCount = editor.Session.UndoCount;
        var redoCount = editor.Session.RedoCount;
        editor.AcceptSave(saved, "/tmp/unodraw-save/destination/copy.svg");

        void AssertResourceIdentity()
        {
            var anchor = editor.Document!.GetElementById<SvgAnchor>("link");
            Assert.Equal(new Uri("file:///tmp/unodraw-save/source/assets/page.svg"),
                new Uri(editor.Document.BaseUri, anchor.Href));
            Assert.Equal(new Uri("file:///tmp/unodraw-save/destination/copy.svg"), editor.Document.BaseUri);
        }
        AssertResourceIdentity();
        Assert.Equal(undoCount, editor.Session.UndoCount);
        Assert.Equal(redoCount, editor.Session.RedoCount);
        Assert.Equal("shape", Assert.Single(editor.SelectedElements).ID);
        Assert.True(editor.IsDirty);
        editor.Undo();
        AssertResourceIdentity();
        Assert.False(editor.IsDirty);
        editor.Undo();
        AssertResourceIdentity();
        Assert.True(editor.IsDirty);
        editor.Redo();
        AssertResourceIdentity();
        Assert.False(editor.IsDirty);
        editor.Redo();
        editor.Redo();
        AssertResourceIdentity();
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public void InvalidSaveAcceptanceLeavesDocumentHistoryAndCheckpointUntouched()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(RectangleSvg, "/tmp/original.svg");
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("shape"));
        editor.MoveSelection(10, 0);
        var document = editor.Document;
        var picture = editor.Renderer.Picture;
        var xml = editor.GetSvg();
        Assert.Throws<XmlException>(() => editor.AcceptSave("<svg><broken></svg>", "/tmp/another/new.svg"));
        Assert.Same(document, editor.Document);
        Assert.Same(picture, editor.Renderer.Picture);
        Assert.Equal(xml, editor.GetSvg());
        Assert.Equal("/tmp/original.svg", editor.Session.CurrentFile);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void FirstSaveDuringGesturePreservesCancellationAndAdoptsDestination()
    {
        using var editor = new SvgEditorController();
        editor.NewDocument();
        editor.CreateElement(ToolService.Tool.Rect, new SKPoint(10, 10), new SKPoint(60, 70));
        var saved = editor.GetSvg();
        editor.BeginEdit("Drag during save");
        editor.MoveSelection(20, 30);
        editor.AcceptSave(saved, "/tmp/new-artwork.svg");
        Assert.True(editor.IsEditing);
        Assert.True(editor.IsDirty);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.CancelEdit();
        Assert.Equal(saved, editor.GetSvg());
        Assert.False(editor.IsDirty);
        Assert.Equal(new Uri("file:///tmp/new-artwork.svg"), editor.Document!.BaseUri);
    }

    [Fact]
    public void HistoryTransformationStagesBothStacksAndPreservesOrder()
    {
        var session = new SvgEditorSession();
        session.PushUndoState("a");
        session.PushUndoState("b");
        Assert.True(session.TryUndo("c", out var current));
        Assert.Equal("b", current);
        Assert.Throws<InvalidOperationException>(() => session.TransformHistory(xml =>
            xml == "c" ? throw new InvalidOperationException("Invalid relocation") : xml + "-changed"));
        Assert.True(session.TryUndo("b", out current));
        Assert.Equal("a", current);
        Assert.True(session.TryRedo("a", out current));
        Assert.Equal("b", current);
        Assert.True(session.TryRedo("b", out current));
        Assert.Equal("c", current);
        session.TransformHistory(xml => xml.ToUpperInvariant());
        Assert.True(session.TryUndo("C", out current));
        Assert.Equal("B", current);
        Assert.True(session.TryUndo("B", out current));
        Assert.Equal("A", current);
    }

    [Fact]
    public void RenamingIdsUpdatesReferencesAndSelectorsWithoutChangingHexColors()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="100" height="100">
              <style>#shape { fill: #fff; }</style>
              <rect id="shape" width="20" height="20"/>
              <use id="copy" xlink:href="#shape" x="30" data-ref="url(#shape)"/>
              <animate id="motion" attributeName="opacity" from="0" to="1" dur="1s" begin="shape.click"/>
            </svg>
            """);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("shape"));
        editor.SetAttribute("id", "renamed");
        var xml = XDocument.Parse(editor.GetSvg());
        var copy = xml.Descendants().Single(e => (string?)e.Attribute("id") == "copy");
        Assert.Contains(copy.Attributes(), a => a.Name.LocalName == "href" && a.Value == "#renamed");
        Assert.Equal("url(#renamed)", copy.Attribute("data-ref")!.Value);
        var stylesheet = xml.Descendants().Single(e => e.Name.LocalName == "style").Value;
        Assert.Contains("#renamed", stylesheet);
        Assert.Contains("#fff", stylesheet);
        Assert.Equal("renamed", Assert.Single(editor.SelectedElements).ID);
        Assert.Contains("renamed.click", editor.GetSvg());
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.NotNull(editor.Document!.GetElementById("shape"));
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void RenamingResourceUpdatesPaintReferencesAndRetainsArtworkSelection()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs><linearGradient id="paint"><stop offset="0" stop-color="red"/><stop offset="1" stop-color="blue"/></linearGradient></defs>
              <rect id="shape" width="40" height="40" fill="url(#paint)"/>
            </svg>
            """);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("shape"));
        editor.RenameElementId(editor.Document.GetElementById("paint"), "ink");
        Assert.NotNull(editor.Document!.GetElementById<SvgLinearGradientServer>("ink"));
        Assert.Contains("url(#ink)", editor.GetSvg());
        Assert.Equal("shape", Assert.Single(editor.SelectedElements).ID);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.NotNull(editor.Document!.GetElementById<SvgLinearGradientServer>("paint"));
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void DisablingFiltersChangesRenderingWithoutEditingTheDocument()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs><filter id="soft" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="5"/></filter></defs>
              <rect x="25" y="25" width="40" height="40" fill="red" filter="url(#soft)"/>
            </svg>
            """);
        var source = editor.GetSvg();
        var revision = editor.DocumentRevision;
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        editor.Renderer.Draw(canvas);
        var blurred = bitmap.GetPixel(20, 40).Alpha;
        editor.Session.Settings.FiltersDisabled = true;
        canvas.Clear(SKColors.Transparent);
        editor.Renderer.Draw(canvas);
        Assert.True(blurred > 0);
        Assert.Equal(0, bitmap.GetPixel(20, 40).Alpha);
        Assert.Equal(source, editor.GetSvg());
        Assert.Equal(revision, editor.DocumentRevision);
        Assert.False(editor.IsDirty);
        Assert.False(editor.CanUndo);
        editor.Session.Settings.FiltersDisabled = false;
        canvas.Clear(SKColors.Transparent);
        editor.Renderer.Draw(canvas);
        Assert.Equal(blurred, bitmap.GetPixel(20, 40).Alpha);
    }

    [Fact]
    public void RepeatedNudgesKeepTransformsCompactAndPreserveOriginalOrder()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='200'><rect id='shape' width='30' height='40' transform='rotate(30) scale(2)'/></svg>");
        var shape = editor.Document!.GetElementById<SvgRectangle>("shape");
        editor.Select(shape);
        var before = editor.GetSceneNode(shape)!.TransformedBounds;
        editor.BeginEdit("Nudge repeatedly");
        for (var i = 0; i < 20; i++)
            editor.MoveSelection(1, 2);
        editor.CommitEdit();
        var after = editor.GetSceneNode(shape)!.TransformedBounds;
        Assert.Equal(3, shape.Transforms.Count);
        Assert.InRange(after.Left - before.Left, 19.99f, 20.01f);
        Assert.InRange(after.Top - before.Top, 39.99f, 40.01f);
        Assert.Equal(1, editor.Session.UndoCount);
    }

}
