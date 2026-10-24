using System;
using System.Drawing;
using System.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Svg.Editor.Svg.Models;
using Svg.Pathing;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgVariableStrokeServiceTests
{
    [Fact]
    public void UniformStraightStrokeHasCorrectFlatAndRoundCapsWithoutChangingSource()
    {
        using var source = SKPath.ParseSvgPathData("M10 20 L110 20");
        var original = PathService.ToSvgPathData(source);
        var profile = Profile((0, 10), (1, 10));
        using var flat = SvgVariableStrokeService.CreateOutline(source, profile, SKStrokeCap.Butt);
        using var round = SvgVariableStrokeService.CreateOutline(source, profile);

        Assert.Equal(new SKRect(10, 15, 110, 25), flat.Bounds);
        Assert.Equal(new SKRect(5, 15, 115, 25), round.Bounds);
        Assert.True(round.Contains(7, 20));
        Assert.False(flat.Contains(7, 20));
        Assert.False(round.Contains(5, 15));
        Assert.Equal(original, PathService.ToSvgPathData(source));
        Assert.Equal("0,10;1,10", profile.ToString());
    }

    [Fact]
    public void WidthInterpolatesContinuouslyAndTapersToBothEndpoints()
    {
        using var source = SKPath.ParseSvgPathData("M0 0 L100 0");
        using var outline = SvgVariableStrokeService.CreateOutline(source, Profile((0, 0), (0.5, 20), (1, 0)));

        Assert.Equal(new SKRect(0, -10, 100, 10), outline.Bounds);
        Assert.True(outline.Contains(25, 4.5f));
        Assert.False(outline.Contains(25, 6));
        Assert.True(outline.Contains(50, 9));
        Assert.False(outline.Contains(1, 1));
    }

    [Fact]
    public void CurvedStrokeFollowsArcLengthAndAdaptiveOffsetEdges()
    {
        using var source = SKPath.ParseSvgPathData("M10 10 C10 110 110 110 110 10");
        using var outline = SvgVariableStrokeService.CreateOutline(source, Profile((0, 10), (1, 10)), tolerance: 0.1f);
        using var measure = new SKPathMeasure(source, false, 2);
        foreach (var fraction in new[] { 0.2f, 0.4f, 0.6f, 0.8f })
        {
            Assert.True(measure.GetPositionAndTangent(measure.Length * fraction, out var position, out var tangent));
            Assert.True(outline.Contains(position.X - tangent.Y * 4, position.Y + tangent.X * 4));
            Assert.False(outline.Contains(position.X - tangent.Y * 7, position.Y + tangent.X * 7));
        }
        Assert.True(outline.PointCount > 12);
    }

    [Fact]
    public void ShallowSCurveDoesNotCollapseAtItsCollinearMidpoint()
    {
        using var source = SKPath.ParseSvgPathData("M0 0 C25000 200 75000 -200 100000 0");
        using var outline = SvgVariableStrokeService.CreateOutline(source, Profile((0, 10), (1, 10)));
        using var measure = new SKPathMeasure(source, false, 2);
        foreach (var fraction in new[] { 0.25f, 0.75f })
        {
            Assert.True(measure.GetPositionAndTangent(measure.Length * fraction, out var position, out var tangent));
            Assert.True(outline.Contains(position.X, position.Y));
            Assert.True(outline.Contains(position.X - tangent.Y * 4, position.Y + tangent.X * 4));
            Assert.False(outline.Contains(position.X - tangent.Y * 7, position.Y + tangent.X * 7));
        }
        Assert.True(outline.Bounds.Top < -50);
        Assert.True(outline.Bounds.Bottom > 50);
    }

    [Fact]
    public void MultipleOpenContoursProduceSeparateClosedOutlines()
    {
        using var source = SKPath.ParseSvgPathData("M0 0 L100 0 M0 30 L100 30");
        using var outline = SvgVariableStrokeService.CreateOutline(source, Profile((0, 10), (1, 10)), SKStrokeCap.Butt);
        Assert.True(outline.Contains(50, 0));
        Assert.True(outline.Contains(50, 30));
        Assert.False(outline.Contains(50, 15));
        using var measure = new SKPathMeasure(outline, false);
        Assert.True(measure.IsClosed);
        Assert.True(measure.NextContour());
        Assert.True(measure.IsClosed);
        Assert.False(measure.NextContour());
    }

    [Theory]
    [InlineData(-0.1, 1, 1, 1)]
    [InlineData(0, -1, 1, 1)]
    [InlineData(0, 1, 1.1, 1)]
    [InlineData(0, 1, 0, 2)]
    [InlineData(0, 0, 1, 0)]
    [InlineData(double.NaN, 1, 1, 1)]
    [InlineData(0, double.PositiveInfinity, 1, 1)]
    public void InvalidProfilesAreRejected(double firstOffset, double firstWidth, double secondOffset, double secondWidth)
    {
        using var source = SKPath.ParseSvgPathData("M0 0 L100 0");
        Assert.Throws<ArgumentException>(() => SvgVariableStrokeService.CreateOutline(source,
            Profile((firstOffset, firstWidth), (secondOffset, secondWidth))));
    }

    [Fact]
    public void ZeroLengthCenterlineIsRejected()
    {
        using var source = SKPath.ParseSvgPathData("M10 10");
        Assert.Throws<ArgumentException>(() => SvgVariableStrokeService.CreateOutline(source, Profile((0, 10), (1, 10))));
    }

    [Fact]
    public void SquareCapsExtendByEachEndpointHalfWidth()
    {
        using var source = SKPath.ParseSvgPathData("M10 20 L110 20");
        using var outline = SvgVariableStrokeService.CreateOutline(source, Profile((0, 10), (1, 20)), SKStrokeCap.Square);
        Assert.Equal(new SKRect(5, 10, 120, 30), outline.Bounds);
        Assert.True(outline.Contains(6, 16));
        Assert.True(outline.Contains(119, 29));
        Assert.False(outline.Contains(4, 20));
    }

    [Fact]
    public void ClosedContourProducesAHollowBandAndIgnoresEndCaps()
    {
        using var source = SKPath.ParseSvgPathData("M100 50 A50 50 0 1 1 0 50 A50 50 0 1 1 100 50 Z");
        using var outline = SvgVariableStrokeService.CreateOutline(source, Profile((0, 10), (0.5, 20), (1, 10)), SKStrokeCap.Square, 0.1f);
        Assert.False(outline.Contains(50, 50));
        Assert.True(outline.Contains(100, 50));
        Assert.True(outline.Contains(-8, 50));
        Assert.False(outline.Contains(-12, 50));
        using var measure = new SKPathMeasure(outline, false);
        Assert.True(measure.IsClosed);
        Assert.True(measure.NextContour());
        Assert.True(measure.IsClosed);
    }

    [Fact]
    public void ClosedProfileRejectsDiscontinuousSeamBeforeMutatingDocument()
    {
        using var source = SKPath.ParseSvgPathData("M0 0 L100 0 L50 50 Z");
        Assert.Throws<ArgumentException>(() => SvgVariableStrokeService.CreateOutline(source, Profile((0, 10), (1, 20))));
    }

    [Fact]
    public void ApplyPreservesIdentityParentTransformsEffectsMetadataAndIsOneUndoStep()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="400" height="200">
              <defs><clipPath id="clip"><rect width="400" height="200"/></clipPath>
                <filter id="effect"><feGaussianBlur stdDeviation="0"/></filter></defs>
              <g opacity=".7"><path id="path" transform="translate(10 20) scale(2)" d="M10 20 L110 20"
                fill="none" stroke="red" stroke-width="8" stroke-opacity=".4" clip-path="url(#clip)" filter="url(#effect)"
                data-note="keep"><title>Original artwork</title></path></g>
            </svg>
            """);
        var path = editor.Document!.GetElementById<SvgPath>("path");
        var original = editor.GetSvg();
        var parent = path.Parent;
        var transforms = path.Transforms;
        var clip = path.ClipPath;
        var filter = path.Filter;
        editor.Select(path);
        var paint = new SvgColourServer(Color.Coral);

        editor.Execute("Variable width stroke", () => SvgVariableStrokeService.Apply(path, Profile((0, 0), (0.5, 20), (1, 0)), paint));

        Assert.Same(path, editor.Document.GetElementById("path"));
        Assert.Same(parent, path.Parent);
        Assert.Same(transforms, path.Transforms);
        Assert.Same(clip, path.ClipPath);
        Assert.Same(filter, path.Filter);
        Assert.Same(paint, path.Fill);
        Assert.Equal(SvgPaintServer.None, path.Stroke);
        Assert.Equal(0.4f, path.FillOpacity);
        Assert.Equal("keep", path.CustomAttributes["data-note"]);
        Assert.Equal("Original artwork", Assert.IsType<SvgTitle>(Assert.Single(path.Children)).Content);
        Assert.Equal("none", path.MarkerStart.ToString());
        Assert.Equal("none", path.MarkerMid.ToString());
        Assert.Equal("none", path.MarkerEnd.ToString());
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void ReapplyAndSvgRoundTripReuseOriginalCenterline()
    {
        using var editor = Load();
        var path = editor.Document!.GetElementById<SvgPath>("path");
        var source = path.PathData.ToString();
        SvgVariableStrokeService.Apply(path, Profile((0, 10), (1, 10)), new SvgColourServer(Color.Red), SKStrokeCap.Butt);
        var saved = editor.Document.GetXML();
        using var reloaded = new SvgEditorController();
        reloaded.LoadSvg(saved);
        path = reloaded.Document!.GetElementById<SvgPath>("path");
        path.FillOpacity = 0.6f;
        SvgVariableStrokeService.Apply(path, Profile((0, 20), (1, 20)), new SvgColourServer(Color.Blue), SKStrokeCap.Butt);
        using var outline = PathService.ElementToPath(path);
        using var centerline = SvgVariableStrokeService.GetCenterline(path);

        Assert.Equal(new SKRect(10, 40, 110, 60), outline!.Bounds);
        Assert.Equal(new SKRect(10, 50, 110, 50), centerline.Bounds);
        Assert.Equal(source, path.CustomAttributes[SvgVariableStrokeService.CenterlineAttribute]);
        Assert.Equal("0,20;1,20", path.CustomAttributes[SvgVariableStrokeService.ProfileAttribute]);
        Assert.Equal("butt", path.CustomAttributes[SvgVariableStrokeService.CapAttribute]);
        Assert.Equal(0.6f, path.FillOpacity);
    }

    [Fact]
    public void EditingGeneratedNodesPreventsAccidentalLossWhenReopeningProfile()
    {
        using var editor = Load();
        var path = editor.Document!.GetElementById<SvgPath>("path");
        SvgVariableStrokeService.Apply(path, Profile((0, 10), (1, 10)), new SvgColourServer(Color.Red));
        path.PathData[0].End = new PointF(100, 100);
        var changed = path.PathData.ToString();

        Assert.Throws<InvalidOperationException>(() => SvgVariableStrokeService.GetCenterline(path));
        Assert.Throws<InvalidOperationException>(() => SvgVariableStrokeService.Apply(path, Profile((0, 20), (1, 20)), new SvgColourServer(Color.Blue)));
        Assert.Equal(changed, path.PathData.ToString());
    }

    [Fact]
    public void LockedOrInvalidApplyLeavesDomUntouched()
    {
        using var editor = Load();
        var path = editor.Document!.GetElementById<SvgPath>("path");
        var original = editor.Document.GetXML();
        Assert.Throws<ArgumentException>(() => SvgVariableStrokeService.Apply(path, Profile((0, -1), (1, 1)), new SvgColourServer(Color.Blue)));
        Assert.Equal(original, editor.Document.GetXML());

        path.Parent.CustomAttributes["data-unodraw-locked"] = "true";
        var locked = editor.Document.GetXML();
        Assert.Throws<InvalidOperationException>(() => SvgVariableStrokeService.Apply(path, Profile((0, 20), (1, 20)), new SvgColourServer(Color.Blue)));
        Assert.Equal(locked, editor.Document.GetXML());
    }

    [Fact]
    public void SavedOutlineRendersAsOrdinaryFilledSvg()
    {
        using var editor = Load();
        var path = editor.Document!.GetElementById<SvgPath>("path");
        editor.Execute("Variable width stroke", () => SvgVariableStrokeService.Apply(path,
            Profile((0, 0), (0.5, 20), (1, 0)), new SvgColourServer(Color.Red)));
        using var reopened = new SvgEditorController();
        reopened.LoadSvg(editor.GetSvg());
        using var bitmap = new SKBitmap(140, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(reopened.Renderer.Picture!);

        Assert.Equal(SKColors.Red, bitmap.GetPixel(60, 57));
        Assert.Equal((byte)0, bitmap.GetPixel(15, 57).Alpha);
        Assert.Equal((byte)0, bitmap.GetPixel(60, 65).Alpha);
    }

    private static StrokeProfile Profile(params (double Offset, double Width)[] points)
    {
        var profile = new StrokeProfile();
        profile.Points.Clear();
        foreach (var point in points)
            profile.Points.Add(new StrokePointInfo { Offset = point.Offset, Width = point.Width });
        return profile;
    }

    private static SvgEditorController Load()
    {
        var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='140' height='100'><path id='path' d='M10 50 L110 50' fill='none' stroke='red' stroke-width='10'/></svg>");
        return editor;
    }
}
