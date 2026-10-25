using System;
using System.Linq;
using SkiaSharp;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgArrangementTests
{
    [Theory]
    [InlineData(SvgAlignment.Left, -10, 0)]
    [InlineData(SvgAlignment.HorizontalCenter, 30, 0)]
    [InlineData(SvgAlignment.Right, 70, 0)]
    [InlineData(SvgAlignment.Top, 0, -20)]
    [InlineData(SvgAlignment.VerticalCenter, 0, 10)]
    [InlineData(SvgAlignment.Bottom, 0, 40)]
    public void AlignmentPlannerUsesTheRequestedEdgeOrCenter(SvgAlignment alignment, float x, float y)
    {
        var deltas = SvgArrangementService.Align(new[] { SKRect.Create(10, 20, 20, 20) }, SKRect.Create(0, 0, 100, 80), alignment);
        Assert.Equal(new SKPoint(x, y), Assert.Single(deltas));
    }

    [Fact]
    public void KeyObjectReceivesNoTranslation()
    {
        var bounds = new[] { SKRect.Create(0, 0, 10, 10), SKRect.Create(50, 30, 20, 20) };
        var result = SvgArrangementService.Align(bounds, bounds[1], SvgAlignment.Left, 1);
        Assert.Equal(new SKPoint(50, 0), result[0]); Assert.Equal(default, result[1]);
        Assert.Equal(0, bounds[0].Left);
    }

    [Fact]
    public void AutomaticGapsAccountForDifferentObjectWidths()
    {
        var bounds = new[] { SKRect.Create(0, 0, 10, 10), SKRect.Create(20, 0, 20, 10), SKRect.Create(100, 0, 30, 10) };
        var result = SvgArrangementService.DistributeGaps(bounds, SvgArrangementAxis.Horizontal);
        Assert.Equal(0, result[0].X); Assert.Equal(25, result[1].X); Assert.Equal(0, result[2].X);
        Assert.Equal(35, bounds[1].Left + result[1].X - bounds[0].Right);
        Assert.Equal(35, bounds[2].Left - (bounds[1].Right + result[1].X));
    }

    [Fact]
    public void ExplicitSpacingWorksOnBothSidesOfTheFixedKeyAndSupportsOverlap()
    {
        var bounds = new[] { SKRect.Create(100, 0, 30, 10), SKRect.Create(0, 0, 10, 10), SKRect.Create(50, 0, 20, 10) };
        var result = SvgArrangementService.DistributeGaps(bounds, SvgArrangementAxis.Horizontal, 5, 2);
        Assert.Equal(-25, result[0].X); Assert.Equal(35, result[1].X); Assert.Equal(0, result[2].X);
        result = SvgArrangementService.DistributeGaps(bounds, SvgArrangementAxis.Horizontal, -5, 2);
        Assert.Equal(-35, result[0].X); Assert.Equal(45, result[1].X);
    }

    [Fact]
    public void CenterDistributionPreservesBothExtremeCenters()
    {
        var bounds = new[] { SKRect.Create(0, 0, 10, 10), SKRect.Create(20, 0, 20, 10), SKRect.Create(100, 0, 30, 10) };
        var result = SvgArrangementService.DistributeCenters(bounds, SvgArrangementAxis.Horizontal);
        Assert.Equal(0, result[0].X); Assert.Equal(30, result[1].X); Assert.Equal(0, result[2].X);
    }

    [Fact]
    public void EqualSpatialPositionsUseStableInputOrder()
    {
        var bounds = new[] { SKRect.Create(10, 0, 10, 10), SKRect.Create(10, 0, 20, 10), SKRect.Create(10, 0, 30, 10) };
        var result = SvgArrangementService.DistributeGaps(bounds, SvgArrangementAxis.Horizontal, 5);
        Assert.Equal(0, result[0].X); Assert.Equal(15, result[1].X); Assert.Equal(40, result[2].X);
    }

    [Fact]
    public void InvalidBoundsDirectionsAndSpacingAreRejected()
    {
        var bounds = new[] { SKRect.Create(0, 0, 10, 10) };
        Assert.Throws<ArgumentException>(() => SvgArrangementService.Align(new[] { new SKRect(float.NaN, 0, 10, 10) }, bounds[0], SvgAlignment.Left));
        Assert.Throws<ArgumentException>(() => SvgArrangementService.Align(new[] { new SKRect(10, 0, 0, 10) }, bounds[0], SvgAlignment.Left));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgArrangementService.DistributeGaps(bounds, SvgArrangementAxis.Horizontal, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgArrangementService.DistributeCenters(bounds, (SvgArrangementAxis)99));
        Assert.Throws<ArgumentException>(() => SvgArrangementService.DistributeGaps(bounds, SvgArrangementAxis.Horizontal, null, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgArrangementService.Align(bounds, bounds[0], SvgAlignment.Left, 2));
    }

    [Fact]
    public void SingleObjectAlignsToRenderedArtboardDespiteViewBoxMapping()
    {
        using var editor = Load("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100' viewBox='0 0 100 50'><rect id='a' width='10' height='10' x='10' y='5'/></svg>", "a");
        editor.AlignSelection(SvgAlignment.Right, SvgAlignmentTarget.Artboard);
        Assert.Equal(200, Bounds(editor, "a").Right, 3);
        Assert.Equal(1, editor.Session.UndoCount);
    }

    [Fact]
    public void KeyAlignmentPreservesKeyAndWorksAcrossTransformedParents()
    {
        using var editor = Load("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><g transform='translate(10 5) scale(2)'><rect id='a' width='10' height='10'/></g><rect id='b' x='100' y='50' width='20' height='20'/></svg>", "a", "b");
        var key = editor.Document!.GetElementById<SvgRectangle>("b"); var keyBefore = Bounds(editor, "b"); var before = editor.GetSvg();
        editor.AlignSelection(SvgAlignment.Left, SvgAlignmentTarget.KeyObject, key);
        Assert.Equal(keyBefore, Bounds(editor, "b")); Assert.Equal(100, Bounds(editor, "a").Left, 3);
        Assert.Equal(5, Bounds(editor, "a").Top, 3); Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo(); Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void ExactSpacingIsOneUndoStepAndSurvivesSaveReopen()
    {
        using var editor = Load("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><rect id='a' width='10' height='10'/><rect id='b' x='50' width='20' height='10'/><rect id='c' x='100' width='30' height='10'/></svg>", "a", "b", "c");
        var before = editor.GetSvg(); var key = editor.Document!.GetElementById<SvgRectangle>("b");
        editor.SpaceSelection(SvgArrangementAxis.Horizontal, 5, key);
        Assert.Equal(35, Bounds(editor, "a").Left, 3); Assert.Equal(50, Bounds(editor, "b").Left, 3); Assert.Equal(75, Bounds(editor, "c").Left, 3);
        Assert.Equal(1, editor.Session.UndoCount);
        using var reopened = Load(editor.GetSvg(), "a", "b", "c"); Assert.Equal(Bounds(editor, "a"), Bounds(reopened, "a"));
        editor.Undo(); Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void SelectedDescendantsAreNotTranslatedTwice()
    {
        using var editor = Load("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><g id='group'><rect id='a' width='10' height='10'/></g><rect id='b' x='50' width='10' height='10'/></svg>", "group", "a", "b");
        Assert.Equal(2, editor.GetArrangementElements().Length);
        editor.AlignSelection(SvgAlignment.Left, SvgAlignmentTarget.KeyObject, editor.Document!.GetElementById<SvgRectangle>("b"));
        Assert.Equal(50, Bounds(editor, "a").Left, 3);
    }

    [Fact]
    public void CenteringSelectionPreservesRelativePositionsAndOneStepUndo()
    {
        using var editor = Load("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><rect id='a' width='10' height='10'/><rect id='b' x='20' y='10' width='10' height='10'/></svg>", "a", "b");
        var before = editor.GetSvg(); editor.CenterSelectionOnArtboard();
        Assert.Equal(35, Bounds(editor, "a").Left, 3); Assert.Equal(30, Bounds(editor, "a").Top, 3);
        Assert.Equal(55, Bounds(editor, "b").Left, 3); Assert.Equal(40, Bounds(editor, "b").Top, 3);
        Assert.Equal(1, editor.Session.UndoCount); editor.Undo(); Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void AlreadyAlignedObjectsDoNotDiscardRedoOrCreateHistory()
    {
        using var editor = Load("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><rect id='a' width='10' height='10'/><rect id='b' x='20' width='10' height='10'/></svg>", "a", "b");
        editor.MoveSelection(5, 0); editor.Undo(); var before = editor.GetSvg(); var picture = editor.Renderer.Picture;
        editor.AlignSelection(SvgAlignment.Top);
        Assert.Equal(before, editor.GetSvg()); Assert.Same(picture, editor.Renderer.Picture); Assert.False(editor.CanUndo); Assert.True(editor.CanRedo);
    }

    [Fact]
    public void InvalidKeyCannotPartiallyModifySelection()
    {
        using var editor = Load("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><rect id='a' width='10' height='10'/><rect id='b' x='50' width='10' height='10'/></svg>", "a", "b");
        var before = editor.GetSvg();
        Assert.Throws<ArgumentException>(() => editor.AlignSelection(SvgAlignment.Right, SvgAlignmentTarget.KeyObject, new SvgRectangle()));
        Assert.Equal(before, editor.GetSvg()); Assert.False(editor.CanUndo);
    }

    [Fact]
    public void SingularParentAndLockedSelectionFailBeforeAnyMutation()
    {
        using var editor = Load("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><rect id='a' x='10' width='10' height='10'/><g transform='scale(0)'><rect id='b' width='10' height='10'/></g><rect id='c' x='50' width='10' height='10'/></svg>", "a", "b", "c");
        var before = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.AlignSelection(SvgAlignment.Right));
        Assert.Equal(before, editor.GetSvg()); Assert.False(editor.CanUndo);
    }

    private static SvgEditorController Load(string xml, params string[] ids)
    {
        var editor = new SvgEditorController(); editor.LoadSvg(xml);
        foreach (var id in ids) editor.Select(editor.Document!.GetElementById<SvgVisualElement>(id), true);
        return editor;
    }
    private static SKRect Bounds(SvgEditorController editor, string id)
    {
        var node = editor.GetSceneNode(editor.Document!.GetElementById(id))!; var bounds = node.TransformedBounds;
        return new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
    }
}
