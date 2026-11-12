using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorRenderCacheTests
{
    private const string Document = "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100' viewBox='0 0 200 100'><rect id='shape' width='20' height='20'/><view id='board' data-unodraw-artboard='true' data-unodraw-name='Original' viewBox='10 20 40 30'/></svg>";

    [Fact]
    public void ArtboardSnapshotSurvivesSelectionAndCannotBeModifiedByConsumers()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Document);
        var cache = new SvgEditorArtboardCache();
        var original = cache.GetArtboards(editor);
        editor.Select((SvgVisualElement)editor.Document!.GetElementById("shape"));
        Assert.Same(original, cache.GetArtboards(editor));
        Assert.Equal(editor.GetArtboardsBounds(), cache.GetBounds(editor));
        Assert.Throws<NotSupportedException>(() => ((IList<SvgEditorArtboard>)original)[0] = default);
        Assert.Equal("Original", Assert.Single(cache.GetArtboards(editor)).Name);
    }

    [Fact]
    public void RenameBoundsUndoAndRedoRefreshSnapshotsWithoutChangingOldValues()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Document);
        var cache = new SvgEditorArtboardCache();
        var original = cache.GetArtboards(editor);
        editor.RenameArtboard("board", "Renamed");
        Assert.Equal("Renamed", Assert.Single(cache.GetArtboards(editor)).Name);
        editor.SetArtboardBoundsInRootCoordinates("board", new SKRect(30, 10, 80, 40));
        Assert.Equal(editor.GetArtboards()[0].Bounds, Assert.Single(cache.GetArtboards(editor)).Bounds);
        editor.Undo();
        Assert.Equal(new SKRect(10, 20, 50, 50), Assert.Single(cache.GetArtboards(editor)).Bounds);
        editor.Undo();
        Assert.Equal("Original", Assert.Single(cache.GetArtboards(editor)).Name);
        editor.Redo();
        Assert.Equal("Renamed", Assert.Single(cache.GetArtboards(editor)).Name);
        Assert.Equal("Original", Assert.Single(original).Name);
        Assert.Equal(new SKRect(10, 20, 50, 50), original[0].Bounds);
    }

    [Fact]
    public void LiveDomReadAndRefreshedTransactionPreviewRemainDistinct()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Document);
        var cache = new SvgEditorArtboardCache();
        var original = cache.GetArtboards(editor);
        var documentRevision = editor.DocumentRevision;
        editor.BeginEdit("Preview artboard");
        editor.Document!.GetElementById("board").CustomAttributes[SvgEditorArtboardService.NameAttribute] = "Preview";
        Assert.Equal("Preview", Assert.Single(editor.GetArtboards()).Name);
        editor.Refresh();
        Assert.Equal(documentRevision, editor.DocumentRevision);
        Assert.Equal("Preview", Assert.Single(cache.GetArtboards(editor)).Name);
        editor.CancelEdit();
        Assert.Equal("Original", Assert.Single(cache.GetArtboards(editor)).Name);
        Assert.Equal("Original", Assert.Single(original).Name);
    }

    [Fact]
    public void RendererRefreshRootTransformAndDocumentReplacementInvalidateGeometry()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Document);
        var cache = new SvgEditorArtboardCache();
        cache.GetArtboards(editor);
        var revision = editor.RenderRevision;
        editor.Document!.ViewBox = new SvgViewBox(0, 0, 100, 50);
        editor.Renderer.RefreshFromSourceDocument();
        Assert.Equal(revision, editor.RenderRevision);
        Assert.Equal(new SKRect(20, 40, 100, 100), Assert.Single(cache.GetArtboards(editor)).Bounds);
        editor.NewDocument(400, 300);
        var implicitBoard = Assert.Single(cache.GetArtboards(editor));
        Assert.True(implicitBoard.IsImplicit);
        Assert.Equal(new SKRect(0, 0, 400, 300), implicitBoard.Bounds);
        Assert.Equal(implicitBoard.Bounds, cache.GetBounds(editor));
    }

    [Fact]
    public void CacheReleaseDoesNotDisposeBorrowedRendererAndDisposedSourcesClear()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Document);
        var cache = new SvgEditorArtboardCache();
        cache.GetArtboards(editor);
        var picture = editor.Renderer.Picture;
        cache.Clear();
        Assert.Same(picture, editor.Renderer.Picture);
        Assert.Equal("Original", Assert.Single(cache.GetArtboards(editor)).Name);
        Assert.Empty(cache.GetArtboards(null));
        Assert.Equal(SKRect.Empty, cache.GetBounds(null));
        cache.GetArtboards(editor);
        editor.Dispose();
        Assert.Empty(cache.GetArtboards(editor));
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(128)]
    public void RulerCameraChangesMatchUncachedTicksAndPointerFramesReuseLabels(double scale)
    {
        var cache = new SvgRulerTickCache();
        var ticks = cache.GetTicks(137, scale, 1200);
        Assert.Same(ticks, cache.GetTicks(137, scale, 1200));
        var expected = SvgRulerMetrics.Create(137, scale, 1200);
        Assert.Equal(expected.Select(tick => (tick.Pixel, tick.Value, tick.IsMajor, tick.Label)),
            ticks.Select(tick => (tick.Pixel, tick.Value, tick.IsMajor, tick.Label)));
        var moved = cache.GetTicks(-35, scale * 1.25, 390);
        expected = SvgRulerMetrics.Create(-35, scale * 1.25, 390);
        Assert.Equal(expected.Select(tick => (tick.Pixel, tick.Value, tick.IsMajor, tick.Label)),
            moved.Select(tick => (tick.Pixel, tick.Value, tick.IsMajor, tick.Label)));
        Assert.Throws<NotSupportedException>(() => ((IList<SvgRulerTick>)moved)[0] = default);
        Assert.InRange(moved.Count, 1, 512);
    }

    [Fact]
    public void RulerLabelsRemainInvariantAndInvalidMetricsCannotReuseOldTicks()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var cache = new SvgRulerTickCache();
            var ticks = cache.GetTicks(137, 128, 1200);
            Assert.All(ticks, tick => Assert.DoesNotContain(',', tick.Label));
            Assert.Equal("0", default(SvgRulerTick).Label);
            Assert.Empty(cache.GetTicks(double.NaN, 128, 1200));
            Assert.Empty(cache.GetTicks(0, 0, 1200));
            Assert.Empty(cache.GetTicks(0, 1, 0));
            Assert.NotEmpty(cache.GetTicks(137, 128, 1200));
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }
}
