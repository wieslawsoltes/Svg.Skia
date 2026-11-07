using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorToolTests
{
    private const string Artwork = "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><rect id='target' x='10' y='10' width='40' height='40' fill='red' stroke='blue'/><rect id='source' x='100' y='10' width='60' height='60' fill='#008000' stroke='#000000' stroke-width='4' stroke-dasharray='8 4'/></svg>";

    [Fact]
    public void EmptySelectionPaintCommandsUpdateCreationDefaultsWithoutDocumentUndo()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Artwork);
        var original = editor.GetSvg();
        editor.ResetPaints();
        Assert.Equal("#FFFFFF", editor.CurrentFill);
        Assert.Equal("#000000", editor.CurrentStroke);
        editor.SwapFillAndStroke();
        Assert.Equal("#000000", editor.CurrentFill);
        Assert.Equal("#FFFFFF", editor.CurrentStroke);
        Assert.Equal(original, editor.GetSvg());
        Assert.False(editor.CanUndo);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void SwapAndDefaultsAreEachAtomicAndLockedTargetsRejectBeforeChangingDefaults()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Artwork);
        editor.SelectAll();
        var original = editor.GetSvg();
        editor.SwapFillAndStroke();
        Assert.Equal(1, editor.Session.UndoCount);
        var swapped = editor.GetSvg();
        editor.ResetPaints();
        Assert.Equal(2, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(swapped, editor.GetSvg());
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("target"));
        editor.SetLocked(true);
        var fill = editor.CurrentFill;
        var stroke = editor.CurrentStroke;
        Assert.Throws<InvalidOperationException>(editor.SwapFillAndStroke);
        Assert.Equal(fill, editor.CurrentFill);
        Assert.Equal(stroke, editor.CurrentStroke);
    }

    [Fact]
    public void EyedropperRetainsTargetsAndCopiesIndependentDashCollections()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Artwork);
        var source = editor.Document!.GetElementById<SvgRectangle>("source");
        var target = editor.Document.GetElementById<SvgRectangle>("target");
        editor.Select(target);
        var original = editor.GetSvg();
        Assert.True(editor.SampleAppearanceAt(new SKPoint(120, 30)));
        Assert.Same(target, Assert.Single(editor.SelectedElements));
        Assert.Equal(source.Fill.ToString(), target.Fill.ToString());
        Assert.Equal(4, target.StrokeWidth.Value);
        Assert.NotSame(source.StrokeDashArray, target.StrokeDashArray);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
        Assert.False(editor.SampleAppearanceAt(new SKPoint(190, 90)));
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void EyedropperWithNoSelectionDoesNotMutateDocument()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Artwork);
        var original = editor.GetSvg();
        Assert.True(editor.SampleAppearanceAt(new SKPoint(120, 30)));
        Assert.Equal(4, editor.CurrentStrokeWidth);
        Assert.Equal(original, editor.GetSvg());
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void GradientDragSharesPictureCoordinatesAcrossTransformedAndAnonymousTargets()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs><linearGradient id="g"><stop offset="0" stop-color="red"/><stop offset="1" stop-color="blue"/></linearGradient></defs>
              <rect width="100" height="40" fill="url(#g)"/>
              <g transform="translate(0 50) scale(2 1)"><rect id="scaled" width="50" height="40" fill="url(#g)"/></g>
            </svg>
            """);
        var rectangles = editor.Document!.Descendants().OfType<SvgRectangle>().ToArray();
        editor.Select(rectangles[0]);
        editor.Select(rectangles[1], true);
        var original = editor.GetSvg();
        editor.ApplyGradientLine(new SKPoint(0, 0), new SKPoint(100, 0));
        Assert.Equal(2, editor.SelectedElements.Count);
        Assert.Equal(1, editor.Session.UndoCount);
        Assert.Equal(3, XDocument.Parse(editor.GetSvg()).Descendants(XName.Get("linearGradient", "http://www.w3.org/2000/svg")).Count());
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        editor.Renderer.Draw(canvas);
        var a = bitmap.GetPixel(25, 20);
        var b = bitmap.GetPixel(25, 70);
        Assert.InRange(Math.Abs(a.Red - b.Red), 0, 1);
        Assert.InRange(Math.Abs(a.Blue - b.Blue), 0, 1);
        Assert.True(a.Red > a.Blue);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void GradientDragPreservesStrokeTargetAndUsesInvariantCoordinates()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
            using var editor = new SvgEditorController();
            editor.LoadSvg(Artwork);
            editor.Select(editor.Document!.GetElementById<SvgRectangle>("target"));
            editor.ApplyGradientLine(new SKPoint(10.5f, 10), new SKPoint(50.5f, 30), true);
            var target = Assert.Single(editor.SelectedElements);
            Assert.IsType<SvgColourServer>(target.Fill);
            Assert.Contains("gradient", target.Stroke.ToString());
            Assert.Contains("x1=\"10.5\"", editor.GetSvg());
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void ArtboardCropAccountsForImportedViewBoxAndUndoRestoresOriginal()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100' viewBox='10 20 100 50'><rect id='art' x='20' y='25' width='20' height='20'/></svg>");
        var original = editor.GetSvg();
        editor.SetArtboardBounds(new SKRect(20, 10, 120, 70));
        Assert.Equal(100, editor.Document!.Width.Value);
        Assert.Equal(60, editor.Document.Height.Value);
        Assert.Equal(20, editor.Document.ViewBox.MinX, 4);
        Assert.Equal(25, editor.Document.ViewBox.MinY, 4);
        Assert.Equal(50, editor.Document.ViewBox.Width, 4);
        Assert.Equal(30, editor.Document.ViewBox.Height, 4);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Theory]
    [InlineData(SvgEditorInteractionMode.RoundedRectangle)]
    [InlineData(SvgEditorInteractionMode.RegularPolygon)]
    [InlineData(SvgEditorInteractionMode.Star)]
    [InlineData(SvgEditorInteractionMode.RectangularGrid)]
    [InlineData(SvgEditorInteractionMode.PolarGrid)]
    [InlineData(SvgEditorInteractionMode.Spiral)]
    public void DragShapesAreFiniteEditablePathsWithSingleUndo(SvgEditorInteractionMode mode)
    {
        using var editor = new SvgEditorController();
        editor.NewDocument();
        var original = editor.GetSvg();
        var path = editor.AddEditorShape(mode, new SKPoint(140, 120), new SKPoint(20, 30), 5);
        Assert.NotEmpty(path.PathData);
        Assert.Same(path, Assert.Single(editor.SelectedElements));
        Assert.Equal(1, editor.Session.UndoCount);
        using var outline = SKPath.ParseSvgPathData(path.PathData.ToString());
        Assert.All(outline.Points, point => { Assert.True(float.IsFinite(point.X)); Assert.True(float.IsFinite(point.Y)); });
        Assert.InRange(outline.Bounds.Left, 10, 80);
        Assert.InRange(outline.Bounds.Right, 80, 150);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void TransformToolsKeepPivotAndConstrainAnglesWithoutSingularScale()
    {
        var bounds = new SKRect(0, 0, 100, 100);
        var center = new SKPoint(50, 50);
        var rotate = SvgEditorToolGeometry.CreateTransform(SvgEditorInteractionMode.Rotate, bounds, new SKPoint(100, 50), new SKPoint(50, 100), true);
        Assert.Equal(center.X, rotate.MapPoint(center).X, 4);
        Assert.Equal(center.Y, rotate.MapPoint(center).Y, 4);
        Assert.Equal(50, rotate.MapPoint(new SKPoint(100, 50)).X, 4);
        Assert.Equal(100, rotate.MapPoint(new SKPoint(100, 50)).Y, 4);
        var scale = SvgEditorToolGeometry.CreateTransform(SvgEditorInteractionMode.Scale, bounds, new SKPoint(100, 100), center, false);
        Assert.True(scale.TryInvert(out _));
        Assert.Equal(center.X, scale.MapPoint(center).X, 4);
        Assert.Equal(center.Y, scale.MapPoint(center).Y, 4);
        var reflect = SvgEditorToolGeometry.CreateTransform(SvgEditorInteractionMode.Reflect, bounds, new SKPoint(50, 0), new SKPoint(50, 100), true);
        Assert.Equal(center.X, reflect.MapPoint(center).X, 4);
        Assert.Equal(center.Y, reflect.MapPoint(center).Y, 4);
        Assert.Equal(0, reflect.MapPoint(new SKPoint(100, 50)).X, 4);
    }

    [Fact]
    public void TypeOnPathKeepsNestedCoordinatesAndCreatesAResolvableReferenceWithOneUndo()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='300' height='100'><g id='layer' transform='translate(20 10)'><path d='M10 50 C50 0 130 0 170 50' stroke='black' fill='none'/></g></svg>");
        var path = Assert.Single(editor.Document!.Descendants().OfType<SvgPath>());
        editor.Select(path);
        var original = editor.GetSvg();
        var text = editor.AddTextOnSelectedPath("Curved type");
        Assert.Same(path.Parent, text.Parent);
        var textPath = Assert.Single(text.Children.OfType<SvgTextPath>());
        Assert.Equal("#" + path.ID, textPath.ReferencedPath.OriginalString);
        Assert.Equal("Curved type", textPath.Text);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }
}
