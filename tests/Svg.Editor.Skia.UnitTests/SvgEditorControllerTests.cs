using System;
using System.IO;
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
    public void LockedOutlineSelectionCannotMutateArtworkButCanUnlockOrDraw()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><g id='layer' data-lock='true'><rect id='shape' width='20' height='20'/></g></svg>");
        var layer = editor.Document!.GetElementById<SvgGroup>("layer");
        var shape = editor.Document.GetElementById<SvgRectangle>("shape");
        editor.Select(shape, allowLocked: true);
        var before = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.MoveSelection(10, 0));
        Assert.Throws<InvalidOperationException>(() => editor.DeleteSelection());
        Assert.Throws<InvalidOperationException>(() => editor.SetFill("red"));
        Assert.Throws<InvalidOperationException>(() => editor.ConvertSelectionToPaths());
        Assert.Throws<InvalidOperationException>(() => editor.RenameElementId(shape, "renamed"));
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.CanUndo);
        Assert.False(editor.IsDirty);
        Assert.NotEmpty(editor.CopySelection());
        editor.CreateElement(ToolService.Tool.Rect, new SKPoint(40, 40), new SKPoint(60, 60));
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Select(layer, allowLocked: true);
        editor.SetLocked(false);
        editor.Select(shape);
        editor.MoveSelection(10, 0);
        Assert.False(SvgEditorController.IsLocked(shape));
        Assert.Equal(3, editor.Session.UndoCount);
    }

    [Fact]
    public void EditingAParentCannotMoveItsLockedDescendant()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><g id='group'><rect id='shape' width='20' height='20' data-unodraw-locked='true'/></g></svg>");
        var group = editor.Document!.GetElementById<SvgGroup>("group");
        editor.Select(group);
        Assert.True(SvgEditorController.IsEditLocked(group));
        Assert.Throws<InvalidOperationException>(() => editor.MoveSelection(10, 0));
        Assert.Throws<InvalidOperationException>(() => editor.UngroupSelection());
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void TypographyOnlyChangesSuppliedFieldsAndPreservesRichText()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="200" height="200">
              <defs><path id="guide" d="M10 50H190"/></defs>
              <text id="first" font-size="12"><textPath xlink:href="#guide"><tspan fill="blue">Alpha</tspan></textPath><title>A label</title></text>
              <text id="second" x="10" y="100" font-size="18">Beta</text>
            </svg>
            """);
        var first = editor.Document!.GetElementById<SvgText>("first");
        var second = editor.Document.GetElementById<SvgText>("second");
        var descendants = first.Descendants().ToArray();
        editor.Select(first);
        editor.Select(second, true);
        editor.SetTypography(fontFamily: "serif");
        Assert.Equal(descendants, first.Descendants());
        Assert.Equal(12, first.FontSize.Value);
        Assert.Equal(18, second.FontSize.Value);
        Assert.Equal("Beta", second.Text);
        Assert.Contains("Alpha", editor.GetSvg());
        Assert.Contains("#guide", editor.GetSvg());
        Assert.Equal("serif", first.FontFamily);
        Assert.Equal("serif", second.FontFamily);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void ImportedPatternRemapsCollisionsAndRendersItsOwnGradient()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs><linearGradient id="paint"><stop offset="0" stop-color="blue"/></linearGradient></defs>
              <rect id="shape" width="80" height="80" fill="blue"/>
            </svg>
            """);
        var resourceDocument = new SvgDocumentService().FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg">
              <pattern id="texture" patternUnits="userSpaceOnUse" width="20" height="20">
                <defs><linearGradient id="paint"><stop offset="0" stop-color="red"/><stop offset="1" stop-color="red"/></linearGradient></defs>
                <rect id="tile" width="20" height="20" fill="url(#paint)"/>
              </pattern>
            </svg>
            """)!;
        var resource = resourceDocument.Children.Single();
        resourceDocument.Children.Remove(resource);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("shape"));
        editor.Execute("Apply pattern", () =>
        {
            var adopted = editor.AddResource(resource);
            Assert.Equal("texture", adopted.ID);
            editor.SetFill("url(#" + adopted.ID + ")");
        });
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        editor.Renderer.Draw(canvas);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(10, 10));
        Assert.NotNull(editor.Document.GetElementById("paint-copy"));
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void SymbolInstanceKeepsPositionAndRootPresentationUnderViewBoxScaling()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='400' height='200' viewBox='10 20 200 100' opacity='0.5'><rect id='shape' x='20' y='30' width='30' height='20' fill='red'/></svg>");
        var original = editor.Document!.GetElementById<SvgRectangle>("shape");
        editor.Select(original);
        var expectedBounds = editor.SelectionBounds;
        var instance = editor.CreateSymbol("component");
        Assert.NotNull(editor.Document.GetElementById<SvgSymbol>("component"));
        Assert.Equal(expectedBounds, editor.SelectionBounds);
        Assert.Same(instance, Assert.Single(editor.SelectedElements));
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Execute("Hide original", () => original.Display = "none");
        using var bitmap = new SKBitmap(400, 200);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        editor.Renderer.Draw(canvas);
        Assert.InRange(bitmap.GetPixel(30, 30).Alpha, (byte)127, (byte)129);
    }

    [Fact]
    public void OffsetUsesWorldDistanceAndPreservesAppearanceAndMetadata()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200">
              <defs><clipPath id="crop"><rect width="100" height="100"/></clipPath></defs>
              <g transform="translate(40 20) scale(2 3)">
                <rect id="shape" x="5" y="5" width="20" height="10" rx="2" fill="red" opacity="0.4" clip-path="url(#crop)" data-note="keep"><title id="label">Offset label</title></rect>
              </g>
            </svg>
            """);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("shape"));
        var before = editor.SelectionBounds!.Value;
        editor.OffsetSelection(5);
        var path = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        var after = editor.SelectionBounds!.Value;
        Assert.InRange(after.Left, before.Left - 5.01f, before.Left - 4.99f);
        Assert.InRange(after.Right, before.Right + 4.99f, before.Right + 5.01f);
        Assert.InRange(after.Top, before.Top - 5.01f, before.Top - 4.99f);
        Assert.InRange(after.Bottom, before.Bottom + 4.99f, before.Bottom + 5.01f);
        Assert.Equal(0.4f, path.Opacity);
        Assert.Equal(editor.Document.GetElementById<SvgRectangle>("shape").ClipPath, path.ClipPath);
        Assert.Equal("keep", path.CustomAttributes["data-note"]);
        Assert.NotNull(editor.Document.GetElementById("label-copy"));
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void RelativeStylesheetsResolveAgainstTheFileDuringParsing()
    {
        var directory = Path.Combine(Path.GetTempPath(), "unodraw-css-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "theme.css"), "#shape { fill: #00ff00; }");
            using var editor = new SvgEditorController();
            editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='50' height='50'><style>@import url('theme.css');</style><rect id='shape' width='40' height='40'/></svg>", Path.Combine(directory, "drawing.svg"));
            using var bitmap = new SKBitmap(50, 50);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            editor.Renderer.Draw(canvas);
            Assert.Equal(SKColors.Lime, bitmap.GetPixel(10, 10));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
    [Fact]
    public void SourceReplacementIsUndoableAndKeepsFileAndSavepoint()
    {
        using var editor = new SvgEditorController();
        var fileName = Path.Combine(Path.GetTempPath(), "artwork.svg");
        editor.LoadSvg(RectangleSvg, fileName);
        var before = editor.GetSvg();
        editor.ReplaceSvg(RectangleSvg.Replace("fill='red'", "fill='blue'"));
        Assert.Equal(fileName, editor.Session.CurrentFile);
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
        var directory = Path.Combine(Path.GetTempPath(), "unodraw-save");
        var originalFile = Path.Combine(directory, "source", "drawing.svg");
        var savedFile = Path.Combine(directory, "destination", "copy.svg");
        var originalResource = new Uri(Path.Combine(directory, "source", "assets", "page.svg"));
        var savedUri = new Uri(savedFile);
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="200" height="200">
              <a id="link" xlink:href="assets/page.svg"><rect id="shape" width="30" height="40"/></a>
            </svg>
            """, originalFile);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("shape"));
        editor.MoveSelection(10, 0);
        var saved = SvgResourcePathService.Rebase(editor.GetSvg(), editor.Document.BaseUri,
            savedUri);
        editor.MoveSelection(20, 0);
        editor.MoveSelection(30, 0);
        editor.Undo();
        var undoCount = editor.Session.UndoCount;
        var redoCount = editor.Session.RedoCount;
        editor.AcceptSave(saved, savedFile);

        void AssertResourceIdentity()
        {
            var anchor = editor.Document!.GetElementById<SvgAnchor>("link");
            Assert.Equal(originalResource, new Uri(editor.Document.BaseUri, anchor.Href));
            Assert.Equal(savedUri, editor.Document.BaseUri);
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
        var originalFile = Path.Combine(Path.GetTempPath(), "original.svg");
        var invalidFile = Path.Combine(Path.GetTempPath(), "another", "new.svg");
        editor.LoadSvg(RectangleSvg, originalFile);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("shape"));
        editor.MoveSelection(10, 0);
        var document = editor.Document;
        var picture = editor.Renderer.Picture;
        var xml = editor.GetSvg();
        Assert.Throws<XmlException>(() => editor.AcceptSave("<svg><broken></svg>", invalidFile));
        Assert.Same(document, editor.Document);
        Assert.Same(picture, editor.Renderer.Picture);
        Assert.Equal(xml, editor.GetSvg());
        Assert.Equal(originalFile, editor.Session.CurrentFile);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void FirstSaveDuringGesturePreservesCancellationAndAdoptsDestination()
    {
        using var editor = new SvgEditorController();
        var fileName = Path.Combine(Path.GetTempPath(), "new-artwork.svg");
        editor.NewDocument();
        editor.CreateElement(ToolService.Tool.Rect, new SKPoint(10, 10), new SKPoint(60, 70));
        var saved = editor.GetSvg();
        editor.BeginEdit("Drag during save");
        editor.MoveSelection(20, 30);
        editor.AcceptSave(saved, fileName);
        Assert.True(editor.IsEditing);
        Assert.True(editor.IsDirty);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.CancelEdit();
        Assert.Equal(saved, editor.GetSvg());
        Assert.False(editor.IsDirty);
        Assert.Equal(new Uri(fileName), editor.Document!.BaseUri);
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
    public void ClippingMaskRetainsInheritedRulesAndCssTransforms()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200">
              <g id="layer" transform="scale(2)" font-size="32" clip-rule="evenodd">
                <rect id="artwork" width="90" height="90" fill="red" style="transform: matrix(1, 0, 0, 1, 10, 0)"/>
                <path id="ring" d="M20 20H60V60H20Z M30 30H50V50H30Z" style="transform: matrix(1, 0, 0, 1, 5, 0)"/>
              </g>
            </svg>
            """);
        var original = editor.GetSvg();
        var artwork = editor.Document!.GetElementById<SvgRectangle>("artwork");
        var ring = editor.Document.GetElementById<SvgPath>("ring");
        Assert.Equal(10, editor.GetSceneNode(artwork)!.Transform.TransX);
        Assert.Equal(5, editor.GetSceneNode(ring)!.Transform.TransX);
        editor.Select(artwork);
        editor.Select(ring, additive: true);
        editor.CreateClippingMask();
        Assert.Equal(SvgClipRule.EvenOdd, ring.ClipRule);
        Assert.Equal(32, ring.FontSize.Value);
        Assert.Same(artwork, Assert.Single(editor.SelectedElements));
        using var bitmap = new SKBitmap(200, 200);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        editor.Renderer.Draw(canvas);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(54, 50));
        Assert.Equal(0, bitmap.GetPixel(80, 80).Alpha);
        Assert.Equal(0, bitmap.GetPixel(44, 50).Alpha);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
        Assert.False(editor.IsDirty);
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
