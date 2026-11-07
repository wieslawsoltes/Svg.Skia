using System;
using System.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgShapeBuilderToolTests
{
    private const string Overlap = "<svg xmlns='http://www.w3.org/2000/svg' width='180' height='120'><rect id='back' width='100' height='100' fill='red'/><rect id='front' x='50' width='100' height='100' fill='blue'/></svg>";

    [Fact]
    public void JoiningTouchedCellsCreatesOneNativePathWithFirstCellAppearanceAndOneUndo()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Overlap);
        editor.SelectAll();
        var original = editor.GetSvg();
        var firstPaint = editor.Document!.GetElementById<SvgRectangle>("back").Fill.ToString();
        using var draft = editor.BeginShapeBuilder();
        Assert.Equal(3, draft.Regions.Count);
        Assert.Equal(original, editor.GetSvg());
        Assert.False(editor.CanUndo);
        var regions = new[] { draft.HitTest(new SKPoint(25, 50)), draft.HitTest(new SKPoint(75, 50)), draft.HitTest(new SKPoint(125, 50)) };
        Assert.Equal(3, regions.Distinct().Count());
        Assert.True(editor.ApplyShapeBuilder(draft, regions));
        var path = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        Assert.Equal(firstPaint, path.Fill.ToString());
        Assert.Equal("back", path.ID);
        using var geometry = WorldPath(editor, path);
        Assert.True(geometry.Contains(25, 50));
        Assert.True(geometry.Contains(75, 50));
        Assert.True(geometry.Contains(125, 50));
        Assert.Equal(150, geometry.Bounds.Width, 4);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void RemovingAnOverlapCellErasesItFromEveryCoveringSource()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Overlap);
        editor.SelectAll();
        using var draft = editor.BeginShapeBuilder();
        Assert.True(editor.ApplyShapeBuilder(draft, new[] { draft.HitTest(new SKPoint(75, 50)) }, true));
        Assert.Equal(2, editor.SelectedElements.Count);
        foreach (var piece in editor.SelectedElements)
        {
            using var geometry = WorldPath(editor, piece);
            Assert.False(geometry.Contains(75, 50));
        }
        Assert.Equal(1, editor.Session.UndoCount);
    }

    [Fact]
    public void DisconnectedContoursAreIndependentlySelectableRegions()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='120' height='50'><path id='islands' fill='red' d='M0 0 H20 V20 H0 Z M80 0 H100 V20 H80 Z'/></svg>");
        editor.SelectAll();
        using var draft = editor.BeginShapeBuilder();
        Assert.Equal(2, draft.Regions.Count);
        Assert.NotEqual(draft.HitTest(new SKPoint(10, 10)), draft.HitTest(new SKPoint(90, 10)));
        Assert.True(editor.ApplyShapeBuilder(draft, new[] { draft.HitTest(new SKPoint(10, 10)) }, true));
        var remaining = Assert.Single(editor.SelectedElements);
        using var geometry = WorldPath(editor, remaining);
        Assert.False(geometry.Contains(10, 10));
        Assert.True(geometry.Contains(90, 10));
    }

    [Fact]
    public void HolesStayAttachedToTheirConnectedOuterRegion()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='120' height='120'><path id='ring' fill-rule='evenodd' d='M0 0 H100 V100 H0 Z M30 30 H70 V70 H30 Z'/></svg>");
        editor.SelectAll();
        var original = editor.GetSvg();
        using (var draft = editor.BeginShapeBuilder())
        {
            Assert.Single(draft.Regions);
            Assert.Equal(-1, draft.HitTest(new SKPoint(50, 50)));
            Assert.Equal(0, draft.HitTest(new SKPoint(10, 50)));
            Assert.False(editor.ApplyShapeBuilder(draft, Array.Empty<int>()));
        }
        Assert.Equal(original, editor.GetSvg());
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void SweptHitTestingCatchesThinCellsBetweenPointerEvents()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><rect x='20' y='10' width='1' height='60'/><rect x='45' y='10' width='1' height='60'/><rect x='70' y='10' width='1' height='60'/></svg>");
        editor.SelectAll();
        using var draft = editor.BeginShapeBuilder();
        Assert.Equal(3, draft.HitTestSegment(new SKPoint(0, 30), new SKPoint(90, 30), 0.5f).Count);
    }

    [Fact]
    public void BuilderPreservesNestedCoordinatesAndRejectsStaleOrDisposedDrafts()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='320' height='220'><g id='layer' transform='translate(10 10) scale(2)'><rect id='a' width='60' height='80'/><rect id='b' x='30' width='60' height='80'/></g></svg>");
        editor.Select(editor.Document!.GetElementById<SvgGroup>("layer"));
        using var draft = editor.BeginShapeBuilder();
        var regions = Enumerable.Range(0, draft.Regions.Count).ToArray();
        Assert.True(editor.ApplyShapeBuilder(draft, regions));
        var combined = Assert.Single(editor.SelectedElements);
        Assert.Equal("layer", combined.Parent.ID);
        using var geometry = WorldPath(editor, combined);
        Assert.Equal(10, geometry.Bounds.Left, 4);
        Assert.Equal(190, geometry.Bounds.Right, 4);
        using var stale = editor.BeginShapeBuilder();
        editor.MoveSelection(10, 0);
        var changed = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.ApplyShapeBuilder(stale, new[] { 0 }));
        Assert.Equal(changed, editor.GetSvg());
        var disposed = editor.BeginShapeBuilder();
        disposed.Dispose();
        Assert.Throws<InvalidOperationException>(() => editor.ApplyShapeBuilder(disposed, new[] { 0 }));
    }

    private static SKPath WorldPath(SvgEditorController editor, SvgVisualElement element)
    {
        var path = PathService.ElementToPath(element)!;
        var scene = editor.GetSceneNode(element);
        Assert.NotNull(scene);
        var matrix = scene!.TotalTransform;
        path.Transform(new SKMatrix(matrix.ScaleX, matrix.SkewX, matrix.TransX, matrix.SkewY, matrix.ScaleY, matrix.TransY, 0, 0, 1));
        return path;
    }
}
