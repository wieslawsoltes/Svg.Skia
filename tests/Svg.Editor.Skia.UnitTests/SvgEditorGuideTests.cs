using System;
using System.Linq;
using SkiaSharp;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorGuideTests
{
    [Fact]
    public void GuideMetadataRoundTripsWithoutVisibleArtwork()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100' viewBox='10 20 100 50'><rect id='r' x='20' y='25' width='10' height='10'/></svg>");
        var original = editor.GetSvg();
        var picture = editor.Renderer.Picture!.CullRect;
        var id = editor.AddGuide(new SKPoint(40, 30), new SKPoint(1, 0));
        var guide = Assert.Single(editor.GetGuides());
        Assert.Equal(id, guide.Id);
        Assert.Equal(40, guide.Origin.X, 4);
        Assert.Equal(30, guide.Origin.Y, 4);
        Assert.Equal(new SKPoint(1, 0), guide.Direction);
        var local = Assert.Single(SvgEditorGuideService.Read(editor.Document));
        Assert.Equal(30, local.Origin.X, 4);
        Assert.Equal(35, local.Origin.Y, 4);
        Assert.Equal(picture, editor.Renderer.Picture!.CullRect);
        Assert.Single(editor.Document!.Descendants().OfType<SvgVisualElement>());
        var saved = editor.GetSvg();
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
        Assert.Empty(editor.GetGuides());
        editor.Redo();
        Assert.Equal(saved, editor.GetSvg());
        editor.LoadSvg(saved);
        Assert.Equal(id, Assert.Single(editor.GetGuides()).Id);
    }

    [Fact]
    public void MoveDeleteAndBoundsGuidesHaveAtomicHistory()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><rect id='r' x='20' y='10' width='50' height='40'/></svg>");
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("r"));
        var selected = editor.SelectedElements.Single();
        editor.AddSelectionBoundsGuides();
        Assert.Equal(4, editor.GetGuides().Count);
        Assert.Equal(1, editor.Session.UndoCount);
        Assert.Same(selected, editor.SelectedElements.Single());
        var first = editor.GetGuides()[0];
        editor.MoveGuide(first.Id, new SKPoint(80, 20));
        Assert.Equal(80, editor.GetGuides()[0].Origin.X, 4);
        editor.Undo();
        Assert.Equal(20, editor.GetGuides()[0].Origin.X, 4);
        editor.RemoveGuide(first.Id);
        Assert.Equal(3, editor.GetGuides().Count);
        editor.ClearGuides();
        Assert.Empty(editor.GetGuides());
        editor.Undo();
        Assert.Equal(3, editor.GetGuides().Count);
    }

    [Fact]
    public void SnapUsesNearestLineAndFiniteNearbyIntersections()
    {
        var guides = new[]
        {
            new SvgEditorGuide("x", new SKPoint(20, 0), new SKPoint(0, 1)),
            new SvgEditorGuide("y", new SKPoint(0, 40), new SKPoint(1, 0)),
            new SvgEditorGuide("far", new SKPoint(100, 0), new SKPoint(0, 1))
        };
        var result = SvgEditorGuideService.Snap(new SKPoint(22, 43), 5, guides, out var first, out var second);
        Assert.Equal(new SKPoint(20, 40), result);
        Assert.NotNull(first);
        Assert.NotNull(second);
        result = SvgEditorGuideService.Snap(new SKPoint(22, 60), 5, guides, out first, out second);
        Assert.Equal(new SKPoint(20, 60), result);
        Assert.Equal("x", first);
        Assert.Null(second);
        Assert.Equal(new SKPoint(80, 90), SvgEditorGuideService.Snap(new SKPoint(80, 90), 5, guides, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgEditorGuideService.Snap(new SKPoint(float.NaN, 1), 5, guides, out _, out _));
    }

    [Fact]
    public void CorruptMetadataAndInvalidEditsDoNotDamageDocument()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' data-unodraw-guides='1;invalid,1,2,0,1'/>");
        Assert.Empty(editor.GetGuides());
        var before = editor.GetSvg();
        Assert.Throws<ArgumentException>(() => editor.AddGuide(new SKPoint(1, 2), default));
        Assert.Throws<ArgumentException>(() => editor.AddGuide(new SKPoint(float.PositiveInfinity, 2), new SKPoint(0, 1)));
        Assert.Equal(before, editor.GetSvg());
        Assert.Equal(0, editor.Session.UndoCount);
        var bad = new[] { new SvgEditorGuide("invalid", default, new SKPoint(1, 0)) };
        Assert.Throws<ArgumentException>(() => SvgEditorGuideService.Write(editor.Document!, bad));
        Assert.Equal(before, editor.GetSvg());
    }
}
