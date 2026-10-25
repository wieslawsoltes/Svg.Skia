using System;
using System.Linq;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorFilterTests
{
    private const string Source = "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><rect id='shape' x='10' y='10' width='20' height='20' fill='red'/></svg>";
    private const string Offset = "<filter xmlns='http://www.w3.org/2000/svg' x='-100%' y='-100%' width='400%' height='400%'><feOffset dx='30' dy='0'/></filter>";

    [Fact]
    public void PreviewAndAbandonNeverMutateLiveEditor()
    {
        using var editor = Load(Source, "shape");
        var before = editor.GetSvg();
        var picture = editor.Renderer.Picture;
        var revision = editor.RenderRevision;
        var edit = editor.BeginFilterEdit();
        edit.Graph = SvgFilterGraph.Parse(Offset);
        using var preview = editor.CreateFilterPreviewRenderer(edit);
        using var pixels = Render(preview);
        Assert.Equal(SKColors.Red, pixels.GetPixel(45, 15));
        Assert.Equal((byte)0, pixels.GetPixel(15, 15).Alpha);
        Assert.Equal(before, editor.GetSvg());
        Assert.Same(picture, editor.Renderer.Picture);
        Assert.Equal(revision, editor.RenderRevision);
        Assert.False(editor.IsDirty);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void AppliedFilterRendersBeforeSaveAndMatchesReopenedDocument()
    {
        using var editor = Load(Source, "shape");
        var before = editor.GetSvg();
        var edit = editor.BeginFilterEdit();
        edit.Graph = SvgFilterGraph.Parse(Offset);
        editor.ApplyFilterEdit(edit);
        using var immediate = Render(editor.Renderer);
        Assert.Equal(SKColors.Red, immediate.GetPixel(45, 15));
        Assert.Equal((byte)0, immediate.GetPixel(15, 15).Alpha);
        Assert.Equal(1, editor.Session.UndoCount);
        Assert.Equal("shape", Assert.Single(editor.SelectedElements).ID);
        using var reopened = new SvgEditorController();
        reopened.LoadSvg(editor.GetSvg());
        using var again = Render(reopened.Renderer);
        Assert.Equal(immediate.Pixels, again.Pixels);
        var after = editor.GetSvg();
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.IsDirty);
        editor.Redo();
        Assert.Equal(after, editor.GetSvg());
    }

    [Fact]
    public void DefaultEditClonesSharedResourceAndRemapsNestedIds()
    {
        using var editor = Load("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
              <defs><filter id="shared"><feOffset id="node" dx="1"/></filter></defs>
              <rect id="a" width="20" height="20" filter="url(#shared)"/>
              <rect id="b" x="50" width="20" height="20" filter="url(#shared)"/>
            </svg>
            """, "a");
        var edit = editor.BeginFilterEdit();
        Assert.Equal("shared", edit.OriginalFilterId);
        edit.Graph.SetAttribute(0, "dx", "10");
        editor.ApplyFilterEdit(edit);
        var tree = XDocument.Parse(editor.GetSvg());
        var ids = tree.Descendants().Attributes("id").Select(id => id.Value).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Equal("1", (string?)tree.Descendants().Single(e => (string?)e.Attribute("id") == "node").Attribute("dx"));
        Assert.Contains("#shared", tree.Descendants().Single(e => (string?)e.Attribute("id") == "b").ToString());
        Assert.Contains("#" + edit.ResourceId, tree.Descendants().Single(e => (string?)e.Attribute("id") == "a").ToString());
        Assert.Contains("node-copy", ids);
    }

    [Fact]
    public void SharedResourceEditingUpdatesDefinitionWithoutDuplicatingIt()
    {
        using var editor = Load("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><defs><filter id='shared'><feOffset dx='1'/></filter></defs><rect id='shape' width='20' height='20' filter='url(#shared)'/></svg>", "shape");
        var edit = editor.BeginFilterEdit();
        edit.Graph.SetAttribute(0, "dx", "10");
        editor.ApplyFilterEdit(edit, true);
        var filter = Assert.Single(XDocument.Parse(editor.GetSvg()).Descendants(SvgFilterCatalog.Namespace + "filter"));
        Assert.Equal("shared", (string?)filter.Attribute("id"));
        Assert.Equal("10", (string?)filter.Elements().Single().Attribute("dx"));
    }

    [Fact]
    public void SourceAndInlineCssFiltersAreOverriddenWithoutDroppingOtherStyle()
    {
        using var editor = Load("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><style>#shape{filter:none!important}</style><rect id='shape' x='10' y='10' width='20' height='20' style='filter:none!important;fill:red;stroke:none'/></svg>", "shape");
        var edit = editor.BeginFilterEdit();
        edit.Graph = SvgFilterGraph.Parse(Offset);
        editor.ApplyFilterEdit(edit);
        using var pixels = Render(editor.Renderer);
        Assert.Equal(SKColors.Red, pixels.GetPixel(45, 15));
        Assert.Contains("stroke:none", editor.GetSvg());
        editor.RemoveSelectionFilters();
        using var removed = Render(editor.Renderer);
        Assert.Equal(SKColors.Red, removed.GetPixel(15, 15));
        Assert.Equal((byte)0, removed.GetPixel(45, 15).Alpha);
    }

    [Fact]
    public void StaleDraftAndChangedSelectionCannotOverwriteCurrentDocument()
    {
        using var editor = Load(Source, "shape");
        var edit = editor.BeginFilterEdit();
        editor.MoveSelection(5, 0);
        var moved = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.ApplyFilterEdit(edit));
        Assert.Equal(moved, editor.GetSvg());
        edit = editor.BeginFilterEdit();
        editor.ClearSelection();
        Assert.Throws<InvalidOperationException>(() => editor.ApplyFilterEdit(edit));
    }

    [Fact]
    public void InvalidGraphFailsBeforeDocumentOrHistoryMutation()
    {
        using var editor = Load(Source, "shape");
        var edit = editor.BeginFilterEdit();
        edit.Graph.SetAttribute(0, "in", "missing");
        var before = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.ApplyFilterEdit(edit));
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void SharedEditRejectsLockedConsumers()
    {
        using var editor = Load("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><defs><filter id='shared'><feOffset dx='1'/></filter></defs><rect id='a' width='20' height='20' filter='url(#shared)'/><rect id='b' x='50' width='20' height='20' filter='url(#shared)' data-lock='true'/></svg>", "a");
        var edit = editor.BeginFilterEdit();
        edit.Graph.SetAttribute(0, "dx", "10");
        var before = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.ApplyFilterEdit(edit, true));
        Assert.Equal(before, editor.GetSvg());
        editor.ApplyFilterEdit(edit, false);
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public void ApplyingToAnonymousTargetsPreservesSelection()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><rect x='10' y='10' width='20' height='20'/></svg>");
        editor.Select(editor.Document!.Children.OfType<SvgRectangle>().Single());
        editor.ApplyFilterEdit(editor.BeginFilterEdit());
        Assert.IsType<SvgRectangle>(Assert.Single(editor.SelectedElements));
    }

    private static SvgEditorController Load(string xml, string id)
    {
        var editor = new SvgEditorController();
        editor.LoadSvg(xml);
        editor.Select(editor.Document!.GetElementById<SvgVisualElement>(id));
        return editor;
    }
    private static SKBitmap Render(SKSvg renderer)
    {
        var bitmap = new SKBitmap(100, 80);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        renderer.Draw(canvas);
        return bitmap;
    }
}
