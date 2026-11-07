using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorArtboardTests
{
    private const string Artwork = "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><rect id='art' x='20' y='10' width='40' height='30' fill='#c53a51'/><circle cx='130' cy='40' r='20' fill='#2966ba'/></svg>";

    [Fact]
    public void ImplicitRootAndActivationDoNotChangeDocumentOrHistory()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Artwork);
        var original = editor.GetSvg();
        var board = Assert.Single(editor.GetArtboards());
        Assert.True(board.IsImplicit);
        Assert.Equal(string.Empty, board.Id);
        Assert.Equal(new SKRect(0, 0, 200, 100), board.Bounds);
        Assert.True(editor.ActivateArtboardAt(new SKPoint(20, 20)));
        Assert.False(editor.ActivateArtboardAt(new SKPoint(300, 20)));
        Assert.Equal(original, editor.GetSvg());
        Assert.False(editor.CanUndo);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void FirstNamedRegionRetainsRootAndUsesOrdinaryNonprintingViews()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Artwork);
        var original = editor.GetSvg();
        var before = Pixels(editor, 200, 100);
        var id = editor.AddArtboard(new SKRect(90, 10, 180, 90), "Detail & preview");
        var boards = editor.GetArtboards();
        Assert.Equal(2, boards.Count);
        Assert.False(boards[0].IsImplicit);
        Assert.Equal(new SKRect(0, 0, 200, 100), boards[0].Bounds);
        Assert.Equal(id, editor.ActiveArtboardId);
        Assert.Equal("Detail & preview", boards[1].Name);
        var xml = XDocument.Parse(editor.GetSvg());
        var views = xml.Root!.Elements(XName.Get("view", "http://www.w3.org/2000/svg")).ToArray();
        Assert.Equal(2, views.Length);
        Assert.All(views, view => Assert.Equal("true", (string?)view.Attribute(SvgEditorArtboardService.Attribute)));
        Assert.Equal(before, Pixels(editor, 200, 100));
        Assert.Equal(1, editor.Session.UndoCount);
        var saved = editor.GetSvg();
        using var reopened = new SvgEditorController();
        reopened.LoadSvg(saved);
        Assert.Equal(boards.Select(board => board.Name), reopened.GetArtboards().Select(board => board.Name));
        Assert.Equal(boards.Select(board => board.Bounds), reopened.GetArtboards().Select(board => board.Bounds));
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
        Assert.True(Assert.Single(editor.GetArtboards()).IsImplicit);
        editor.Redo();
        Assert.Equal(saved, editor.GetSvg());
    }

    [Fact]
    public void NamedArtboardsRoundTripImportedViewportWithoutDoubleTransform()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100' viewBox='10 20 100 50'><rect id='art' x='20' y='25' width='20' height='20'/></svg>");
        var original = editor.GetSvg();
        var id = editor.AddArtboard(new SKRect(20, 10, 120, 70), "Inset");
        var local = SvgEditorArtboardService.Read(editor.Document).Single(board => board.Id == id);
        Assert.Equal(new SKRect(20, 25, 70, 55), local.Bounds);
        Assert.Equal(new SKRect(20, 10, 120, 70), editor.GetArtboards().Single(board => board.Id == id).Bounds);
        editor.SetArtboardBoundsInRootCoordinates(id, new SKRect(30, 30, 70, 50));
        Assert.Equal(new SKRect(40, 20, 120, 60), editor.ActiveArtboard!.Value.Bounds);
        Assert.Equal(2, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(new SKRect(20, 10, 120, 70), editor.ActiveArtboard!.Value.Bounds);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void ExpansionKeepsImportedScaleArtworkCoordinatesAndSingleTransaction()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100' viewBox='10 20 100 50'><rect id='art' x='20' y='25' width='20' height='20'/></svg>");
        var original = editor.GetSvg();
        var id = editor.AddArtboard(new SKRect(-120, -30, -20, 50), "Left");
        Assert.Equal(320, editor.Document!.Width.Value);
        Assert.Equal(130, editor.Document.Height.Value);
        Assert.Equal(-50, editor.Document.ViewBox.MinX);
        Assert.Equal(5, editor.Document.ViewBox.MinY);
        Assert.Equal(160, editor.Document.ViewBox.Width);
        Assert.Equal(65, editor.Document.ViewBox.Height);
        var art = editor.Document.GetElementById<SvgRectangle>("art");
        Assert.Equal(20, art.X.Value);
        Assert.Equal(25, art.Y.Value);
        Assert.Equal(20, art.Width.Value);
        editor.Select(art);
        var oldBoard = editor.GetArtboards()[0].Bounds;
        var selected = editor.SelectionBounds!.Value;
        Assert.Equal(20, selected.Left - oldBoard.Left, 4);
        Assert.Equal(10, selected.Top - oldBoard.Top, 4);
        Assert.Equal(40, selected.Width, 4);
        Assert.Equal(new SKRect(0, 0, 100, 80), editor.GetArtboards().Single(board => board.Id == id).Bounds);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void LetterboxedRootIsRetainedAsItsWholeVisibleViewport()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100' viewBox='0 0 100 100'><rect id='art' x='10' y='20' width='20' height='20'/></svg>");
        editor.AddArtboard(new SKRect(230, 0, 330, 100), "Right");
        var boards = SvgEditorArtboardService.Read(editor.Document);
        Assert.Equal(new SKRect(-50, 0, 150, 100), boards[0].Bounds);
        Assert.Equal(new SKRect(180, 0, 280, 100), boards[1].Bounds);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("art"));
        Assert.Equal(60, editor.SelectionBounds!.Value.Left, 4);
        Assert.Equal(20, editor.SelectionBounds.Value.Width, 4);
        Assert.Equal(330, editor.Renderer.Picture!.CullRect.Width, 4);
    }

    [Theory]
    [InlineData("100%", "100%")]
    [InlineData("50%", "200%")]
    public void PercentageRootExpandsOnceWithoutScalingArtwork(string width, string height)
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='10 20 100 50'><rect id='art' x='20' y='25' width='20' height='20'/></svg>");
        var original = editor.GetSvg();
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("art"));
        var before = editor.SelectionBounds!.Value;
        var viewport = editor.Renderer.Picture!.CullRect;
        editor.AddArtboard(new SKRect(viewport.Right + 20, viewport.Top, viewport.Right + 120, viewport.Bottom), "Right");
        Assert.Equal(viewport.Width + 120, editor.Renderer.Picture!.CullRect.Width, 4);
        Assert.Equal(viewport.Height, editor.Renderer.Picture.CullRect.Height, 4);
        Assert.Equal(before.Left, editor.SelectionBounds!.Value.Left, 4);
        Assert.Equal(before.Top, editor.SelectionBounds.Value.Top, 4);
        Assert.Equal(before.Width, editor.SelectionBounds.Value.Width, 4);
        Assert.Equal(before.Height, editor.SelectionBounds.Value.Height, 4);
        Assert.Equal(SvgUnitType.Percentage, editor.Document.Width.Type);
        Assert.Equal(SvgUnitType.Percentage, editor.Document.Height.Type);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Theory]
    [InlineData("200", "100")]
    [InlineData("100%", "100%")]
    public void ExplicitRootTransformKeepsGeometryAndRelativeBoardMapping(string width, string height)
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='10 20 100 50' transform='translate(15 8) scale(1.5 .75)'><rect id='art' x='20' y='25' width='20' height='20'/></svg>");
        var original = editor.GetSvg();
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("art"));
        var before = editor.SelectionBounds!.Value;
        var viewport = editor.Renderer.Picture!.CullRect;
        var id = editor.AddArtboard(new SKRect(-80, -40, viewport.Right + 60, viewport.Bottom + 20), "Expanded");
        var after = editor.SelectionBounds!.Value;
        var retainedBoard = editor.GetArtboards()[0].Bounds;
        Assert.Equal(before.Left, after.Left - retainedBoard.Left, 3);
        Assert.Equal(before.Top, after.Top - retainedBoard.Top, 3);
        Assert.Equal(before.Width, after.Width, 3);
        Assert.Equal(before.Height, after.Height, 3);
        Assert.Equal(viewport.Width + 140, editor.Renderer.Picture!.CullRect.Width, 3);
        Assert.Equal(viewport.Height + 60, editor.Renderer.Picture.CullRect.Height, 3);
        var region = editor.GetArtboards().Single(board => board.Id == id).Bounds;
        Assert.Equal(0, region.Left, 3);
        Assert.Equal(0, region.Top, 3);
        Assert.Equal(viewport.Width + 140, region.Width, 3);
        Assert.Equal(viewport.Height + 60, region.Height, 3);
        Assert.Equal(20, editor.Document.GetElementById<SvgRectangle>("art").X.Value);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void RenameDuplicateReorderDeleteRetainArtworkAndIndependentUndoSteps()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Artwork);
        editor.RenameArtboard(string.Empty, "Cover");
        var first = editor.ActiveArtboardId!;
        var renamed = editor.GetSvg();
        var second = editor.DuplicateArtboard(first, new SKPoint(220, 0));
        Assert.Equal("Cover copy", editor.ActiveArtboard!.Value.Name);
        Assert.Equal(new SKRect(220, 0, 420, 100), editor.ActiveArtboard.Value.Bounds);
        editor.ReorderArtboard(second, 0);
        Assert.Equal(second, editor.GetArtboards()[0].Id);
        editor.RemoveArtboard(first);
        Assert.Equal(second, Assert.Single(editor.GetArtboards()).Id);
        Assert.NotNull(editor.Document!.GetElementById("art"));
        Assert.Single(editor.Document.Descendants().OfType<SvgCircle>());
        Assert.Equal(4, editor.Session.UndoCount);
        Assert.Throws<InvalidOperationException>(() => editor.RemoveArtboard(second));
        editor.Undo();
        editor.Undo();
        editor.Undo();
        Assert.Equal(renamed, editor.GetSvg());
    }

    [Fact]
    public void ReorderingViewsKeepsAnonymousArtworkPainterOrderAndAddresses()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><view id='a' viewBox='0 0 50 100' data-unodraw-artboard='true'/><rect x='0' y='0' width='60' height='60' fill='red'/><view id='b' viewBox='50 0 50 100' data-unodraw-artboard='true'/><rect x='20' y='20' width='60' height='60' fill='blue'/><view id='external' viewBox='0 0 25 25'/></svg>");
        var before = Pixels(editor, 100, 100);
        var art = editor.Document!.Children.OfType<SvgRectangle>().ToArray();
        editor.Select(art[1]);
        editor.ReorderArtboard("b", 0);
        Assert.Same(art[0], editor.Document.Children[1]);
        Assert.Same(art[1], editor.Document.Children[3]);
        Assert.Equal(before, Pixels(editor, 100, 100));
        Assert.NotNull(editor.Document.GetElementById("external"));
        editor.Undo();
        Assert.Equal(20, Assert.IsType<SvgRectangle>(Assert.Single(editor.SelectedElements)).X.Value);
    }

    [Fact]
    public void InvalidGeometryAndLockedDocumentRejectWithoutPartialMetadata()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Artwork);
        var original = editor.GetSvg();
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.AddArtboard(new SKRect(0, 0, float.NaN, 100)));
        Assert.Throws<ArgumentException>(() => editor.RenameArtboard(string.Empty, " "));
        Assert.Equal(original, editor.GetSvg());
        Assert.False(editor.CanUndo);
        editor.Document!.CustomAttributes["data-unodraw-locked"] = "true";
        Assert.Throws<InvalidOperationException>(() => editor.AddArtboard(new SKRect(0, 0, 20, 20)));
        Assert.Empty(SvgEditorArtboardService.Read(editor.Document));
    }

    [Fact]
    public void MetadataUsesInvariantNumbersAndIgnoresInvalidOrUnownedViews()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
            using var editor = new SvgEditorController();
            editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><view id='valid' viewBox='10.5,20.5,30,40' data-unodraw-artboard='true'/><view id='invalid' viewBox='0 0 NaN 10' data-unodraw-artboard='true'/><view id='external' viewBox='0 0 30 40'/></svg>");
            Assert.Equal(new SKRect(10.5f, 20.5f, 40.5f, 60.5f), Assert.Single(editor.GetArtboards()).Bounds);
            editor.SetArtboardBoundsInRootCoordinates("valid", new SKRect(5.5f, 6.5f, 25.5f, 46.5f));
            Assert.Contains("viewBox=\"5.5 6.5 20 40\"", editor.GetSvg());
            Assert.NotNull(editor.Document!.GetElementById("external"));
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    private static SKColor[] Pixels(SvgEditorController editor, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        editor.Renderer.Draw(canvas);
        return bitmap.Pixels;
    }
}
