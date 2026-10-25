using System;
using System.Linq;
using System.Xml.Linq;
using SkiaSharp;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorMaskTests
{
    private const string Source = "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><rect id='art' x='10' y='10' width='40' height='30' fill='red'/><rect id='masksource' x='20' y='10' width='20' height='30' fill='black'/></svg>";

    [Theory]
    [InlineData(false, 255)]
    [InlineData(true, 0)]
    public void AlphaAndLuminanceModesProduceDistinctExpectedCoverage(bool luminance, int alpha)
    {
        using var editor = Create(Source);
        editor.CreateOpacityMask(luminance);
        using var pixels = Render(editor);
        Assert.Equal((byte)alpha, pixels.GetPixel(25, 20).Alpha);
        Assert.Equal((byte)0, pixels.GetPixel(15, 20).Alpha);
        Assert.Single(editor.SelectedElements);
        Assert.IsType<SvgGroup>(editor.SelectedElements[0]);
        Assert.NotNull(editor.Document!.GetElementById<SvgRectangle>("masksource"));
        Assert.Equal("mask", XDocument.Parse(editor.GetSvg()).Descendants().Single(e => (string?)e.Attribute("id") == "masksource").Parent!.Name.LocalName);
    }

    [Fact]
    public void MaskApplicationIsOneUndoEntryAndImmediateRenderingMatchesReopen()
    {
        using var editor = Create(Source);
        var before = editor.GetSvg();
        editor.CreateOpacityMask(false);
        var after = editor.GetSvg();
        Assert.Equal(1, editor.Session.UndoCount);
        using var immediate = Render(editor);
        using var reopened = new SvgEditorController();
        reopened.LoadSvg(after);
        using var again = Render(reopened);
        Assert.Equal(immediate.Pixels, again.Pixels);
        editor.Undo();
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.IsDirty);
        editor.Redo();
        Assert.Equal(after, editor.GetSvg());
    }

    [Fact]
    public void MaskSourceAndParentTransformsRemainInTheirOriginalCoordinateSystem()
    {
        using var editor = Create("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><g transform='translate(10 5) scale(2)'><rect id='art' width='30' height='20' fill='red'/><rect id='masksource' width='10' height='20' transform='translate(10 0)' fill='white'/></g></svg>");
        editor.CreateOpacityMask();
        using var pixels = Render(editor);
        Assert.Equal(SKColors.Red, pixels.GetPixel(35, 15));
        Assert.Equal((byte)0, pixels.GetPixel(15, 15).Alpha);
        Assert.Equal((byte)0, pixels.GetPixel(55, 15).Alpha);
    }

    [Fact]
    public void ReleaseRestoresEditableArtworkWithoutDeletingSharedDefinition()
    {
        using var editor = Create(Source);
        editor.CreateOpacityMask(false);
        editor.MoveSelection(10, 0);
        editor.ReleaseOpacityMasks();
        var group = Assert.IsType<SvgGroup>(Assert.Single(editor.SelectedElements));
        Assert.Equal(2, group.Children.OfType<SvgRectangle>().Count());
        Assert.NotNull(editor.Document!.GetElementById("masksource"));
        Assert.NotNull(editor.Document.GetElementById("masksource-copy"));
        var ids = editor.Document.Descendants().Select(e => e.ID).Where(id => !string.IsNullOrEmpty(id)).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        using var pixels = Render(editor);
        Assert.Equal(SKColors.Red, pixels.GetPixel(25, 20));
        Assert.Equal(SKColors.Black, pixels.GetPixel(35, 20));
        Assert.Equal((byte)0, pixels.GetPixel(15, 20).Alpha);
    }

    [Fact]
    public void DisableKeepsOriginalMaskArtworkOnlyInItsResource()
    {
        using var editor = Create(Source);
        editor.CreateOpacityMask(false);
        editor.ReleaseOpacityMasks(false);
        var group = Assert.IsType<SvgGroup>(Assert.Single(editor.SelectedElements));
        Assert.Single(group.Children);
        using var pixels = Render(editor);
        Assert.Equal(SKColors.Red, pixels.GetPixel(15, 20));
        Assert.Equal(SKColors.Red, pixels.GetPixel(25, 20));
    }

    [Fact]
    public void SourceOriginallyBehindArtworkIsRestoredBehindIt()
    {
        using var editor = Create("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><rect id='masksource' x='20' y='10' width='20' height='30' fill='black'/><rect id='art' x='10' y='10' width='40' height='30' fill='red'/></svg>");
        editor.CreateOpacityMask(false);
        editor.ReleaseOpacityMasks();
        using var pixels = Render(editor);
        Assert.Equal(SKColors.Red, pixels.GetPixel(25, 20));
    }

    [Fact]
    public void DifferentParentsAreRejectedWithoutMutation()
    {
        using var editor = Create("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><g><rect id='art' width='40' height='30'/></g><rect id='masksource' width='20' height='30'/></svg>");
        var before = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.CreateOpacityMask());
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void ImportedMaskDetachesWithoutInventingAnArtworkRestore()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><defs><mask id='m'><rect width='10' height='10' fill='white'/></mask></defs><rect id='art' width='40' height='30' fill='red' mask='url(#m)'/></svg>");
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("art"));
        editor.ReleaseOpacityMasks();
        Assert.NotNull(editor.Document.GetElementById("m"));
        Assert.IsType<SvgRectangle>(Assert.Single(editor.SelectedElements));
        using var pixels = Render(editor);
        Assert.Equal(SKColors.Red, pixels.GetPixel(25, 20));
    }

    private static SvgEditorController Create(string xml)
    {
        var editor = new SvgEditorController(); editor.LoadSvg(xml);
        editor.Select(editor.Document!.GetElementById<SvgVisualElement>("art"));
        editor.Select(editor.Document.GetElementById<SvgVisualElement>("masksource"), true);
        return editor;
    }
    private static SKBitmap Render(SvgEditorController editor)
    {
        var bitmap = new SKBitmap(100, 80);
        using var canvas = new SKCanvas(bitmap); canvas.Clear(SKColors.Transparent); editor.Renderer.Draw(canvas); return bitmap;
    }
}
