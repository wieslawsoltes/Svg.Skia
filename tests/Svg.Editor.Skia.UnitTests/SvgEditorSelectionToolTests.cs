using System;
using System.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorSelectionToolTests
{
    [Fact]
    public void WandResolvesInheritedCurrentColorAndVariablesWithNumericTolerance()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="500" height="100">
              <g fill="#ff0000"><rect id="inherited" width="20" height="20"/></g>
              <rect id="near" x="30" width="20" height="20" fill="#e81412"/>
              <rect id="far" x="60" width="20" height="20" fill="#c00000"/>
              <g color="red"><rect id="current" x="90" width="20" height="20" fill="currentColor"/></g>
              <g style="--ink:#ff0000"><rect id="variable" x="120" width="20" height="20" fill="var(--ink)"/></g>
              <defs><rect id="resource" width="20" height="20" fill="red"/></defs>
              <g display="none"><rect id="hidden" width="20" height="20" fill="red"/></g>
              <g data-unodraw-locked="true"><rect id="locked" width="20" height="20" fill="red"/></g>
            </svg>
            """);
        var source = editor.Document!.GetElementById<SvgRectangle>("inherited");
        var original = editor.GetSvg();
        var revision = editor.RenderRevision;
        editor.SelectByAppearance(source);
        Assert.Equal(new[] { "inherited", "near", "current", "variable" }, Ids(editor));
        editor.SelectByAppearance(source, new SvgMagicWandOptions { ColorTolerance = 0 });
        Assert.Equal(new[] { "inherited", "current", "variable" }, Ids(editor));
        Assert.Equal(original, editor.GetSvg());
        Assert.Equal(revision, editor.RenderRevision);
        Assert.False(editor.IsDirty);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void WandUsesInheritedPaintWithEachConsumersCurrentColor()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="50">
              <g fill="currentColor" color="red">
                <rect id="red" width="20" height="20"/>
                <rect id="blue" x="30" width="20" height="20" color="blue"/>
              </g>
              <rect id="other-blue" x="60" width="20" height="20" fill="#0000ff"/>
            </svg>
            """);
        editor.SelectByAppearance(editor.Document!.GetElementById<SvgRectangle>("blue"), new SvgMagicWandOptions { ColorTolerance = 0 });
        Assert.Equal(new[] { "blue", "other-blue" }, Ids(editor));
    }

    [Fact]
    public void WandDistinguishesNoFillFromDefaultBlackAndPaintServerIdentity()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="300" height="100">
              <defs><linearGradient id="gradient"><stop offset="0" stop-color="red"/><stop offset="1" stop-color="blue"/></linearGradient></defs>
              <rect id="default" width="20" height="20"/>
              <rect id="black" x="30" width="20" height="20" fill="black"/>
              <rect id="none" x="60" width="20" height="20" fill="none" stroke="black"/>
              <rect id="gradient-one" x="90" width="20" height="20" fill="url(#gradient)"/>
              <g fill="url(#gradient)"><rect id="gradient-two" x="120" width="20" height="20"/></g>
            </svg>
            """);
        editor.SelectByAppearance(editor.Document!.GetElementById<SvgRectangle>("default"));
        Assert.Equal(new[] { "default", "black" }, Ids(editor));
        editor.SelectByAppearance(editor.Document.GetElementById<SvgRectangle>("gradient-one"));
        Assert.Equal(new[] { "gradient-one", "gradient-two" }, Ids(editor));
    }

    [Fact]
    public void WandCombinesStrokeWidthAndCompositedOpacityCriteria()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="300" height="100">
              <g stroke="blue" stroke-width="4" opacity="0.5">
                <rect id="source" width="20" height="20" fill="red"/>
              </g>
              <rect id="match" x="30" width="20" height="20" fill="green" stroke="#0000ff" stroke-width="4.25" opacity="0.5"/>
              <rect id="wide" x="60" width="20" height="20" stroke="blue" stroke-width="8" opacity="0.5"/>
              <rect id="opaque" x="90" width="20" height="20" stroke="blue" stroke-width="4"/>
              <rect id="red-stroke" x="120" width="20" height="20" stroke="red" stroke-width="4" opacity="0.5"/>
            </svg>
            """);
        var options = new SvgMagicWandOptions
        {
            MatchFill = false,
            MatchStroke = true,
            MatchStrokeWidth = true,
            MatchOpacity = true,
            ColorTolerance = 0,
            StrokeWidthTolerance = 0.5f,
            OpacityTolerance = 0
        };
        editor.SelectByAppearance(editor.Document!.GetElementById<SvgRectangle>("source"), options);
        Assert.Equal(new[] { "source", "match" }, Ids(editor));
    }

    [Fact]
    public void WandPointSelectionSkipsLockedCoverAndBackgroundHasPredictableBehavior()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <rect id="back" x="10" y="10" width="20" height="20" fill="red"/>
              <rect id="locked" x="10" y="10" width="20" height="20" fill="blue" data-lock="true"/>
            </svg>
            """);
        Assert.True(editor.SelectByAppearanceAt(new SKPoint(20, 20)));
        Assert.Equal(new[] { "back" }, Ids(editor));
        Assert.False(editor.SelectByAppearanceAt(new SKPoint(90, 90), behavior: SvgEditorSelectionBehavior.Add));
        Assert.Equal(new[] { "back" }, Ids(editor));
        Assert.False(editor.SelectByAppearanceAt(new SKPoint(90, 90)));
        Assert.Empty(editor.SelectedElements);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void LassoFollowsItsConcaveOutlineInsteadOfItsBoundingRectangle()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <rect id="upper" x="10" y="10" width="10" height="10"/>
              <rect id="lower" x="10" y="70" width="10" height="10"/>
              <rect id="outside" x="70" y="70" width="10" height="10"/>
              <rect id="hidden" x="15" y="15" width="10" height="10" visibility="hidden"/>
              <rect id="locked" x="15" y="15" width="10" height="10" data-lock="true"/>
              <defs><rect id="resource" width="10" height="10"/></defs>
            </svg>
            """);
        var original = editor.GetSvg();
        var revision = editor.RenderRevision;
        editor.SelectInLasso(new[] { new SKPoint(0, 0), new SKPoint(100, 0), new SKPoint(100, 30), new SKPoint(30, 30), new SKPoint(30, 100), new SKPoint(0, 100) });
        Assert.Equal(new[] { "upper", "lower" }, Ids(editor));
        Assert.Equal(original, editor.GetSvg());
        Assert.Equal(revision, editor.RenderRevision);
        Assert.False(editor.CanUndo);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void LassoUsesTransformedRetainedGeometryAndImportedViewBox()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="100" viewBox="10 20 100 50">
              <g transform="translate(20 25) scale(2 1)"><rect id="target" width="10" height="10"/></g>
              <rect id="outside" x="70" y="30" width="10" height="10"/>
            </svg>
            """);
        editor.SelectInLasso(Box(19, 9, 61, 31), fullyContained: true);
        Assert.Equal(new[] { "target" }, Ids(editor));
        editor.SelectInLasso(Box(20, 25, 30, 35));
        Assert.Equal(new[] { "target" }, Ids(editor));
    }

    [Fact]
    public void LassoRespectsEvenOddHolesAndStrokeOutline()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="100">
              <path id="ring" fill-rule="evenodd" d="M0 0H100V100H0Z M25 25H75V75H25Z"/>
              <path id="line" d="M110 50H190" stroke="blue" stroke-width="10" fill="none"/>
            </svg>
            """);
        editor.SelectInLasso(Box(40, 40, 60, 60));
        Assert.Empty(editor.SelectedElements);
        editor.SelectInLasso(Box(140, 54, 150, 56));
        Assert.Equal(new[] { "line" }, Ids(editor));
        editor.SelectInLasso(Box(140, 56, 150, 58));
        Assert.Empty(editor.SelectedElements);
    }

    [Fact]
    public void LassoExcludesGeometryOutsideAncestorClip()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100">
              <defs><clipPath id="clip"><rect width="20" height="20"/></clipPath></defs>
              <g clip-path="url(#clip)" transform="translate(10 10)">
                <rect id="clipped" width="80" height="80"/>
              </g>
            </svg>
            """);
        editor.SelectInLasso(Box(40, 40, 60, 60));
        Assert.Empty(editor.SelectedElements);
        editor.SelectInLasso(Box(12, 12, 18, 18));
        Assert.Equal(new[] { "clipped" }, Ids(editor));
    }

    [Fact]
    public void LassoSelectsUseInstancesByTheirGeometryWithoutEnteringDefinitions()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="150" height="120">
              <defs><circle id="resource" cx="50" cy="50" r="40"/></defs>
              <use id="instance" href="#resource" x="20" y="10"/>
            </svg>
            """);
        editor.SelectInLasso(Box(32, 22, 36, 26));
        Assert.Empty(editor.SelectedElements);
        editor.SelectInLasso(Box(65, 55, 75, 65));
        Assert.Equal(new[] { "instance" }, Ids(editor));
    }

    [Fact]
    public void LassoHonorsNonScalingStrokeWidthAndDashGaps()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="120">
              <path id="fixed" d="M10 10H40" transform="scale(3)" vector-effect="non-scaling-stroke" stroke="red" stroke-width="4" fill="none"/>
              <path id="dashed" d="M10 80H100" stroke="blue" stroke-width="4" stroke-dasharray="10 10" fill="none"/>
            </svg>
            """);
        editor.SelectInLasso(Box(60, 33, 65, 34));
        Assert.Empty(editor.SelectedElements);
        editor.SelectInLasso(Box(60, 30, 65, 31));
        Assert.Equal(new[] { "fixed" }, Ids(editor));
        editor.SelectInLasso(Box(24, 79, 26, 81));
        Assert.Empty(editor.SelectedElements);
        editor.SelectInLasso(Box(14, 79, 16, 81));
        Assert.Equal(new[] { "dashed" }, Ids(editor));
    }

    [Fact]
    public void CrossingContainmentAdditionSubtractionAndToggleRemainDistinct()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='50'><rect id='left' width='20' height='20'/><rect id='right' x='40' width='20' height='20'/></svg>");
        editor.SelectInLasso(Box(10, 10, 30, 30), fullyContained: true);
        Assert.Empty(editor.SelectedElements);
        editor.SelectInLasso(Box(10, 10, 30, 30));
        Assert.Equal(new[] { "left" }, Ids(editor));
        editor.SelectInLasso(Box(35, 0, 65, 25), SvgEditorSelectionBehavior.Add);
        Assert.Equal(new[] { "left", "right" }, Ids(editor));
        editor.SelectInLasso(Box(0, 0, 25, 25), SvgEditorSelectionBehavior.Subtract);
        Assert.Equal(new[] { "right" }, Ids(editor));
        editor.SelectInLasso(Box(0, 0, 65, 25), SvgEditorSelectionBehavior.Toggle);
        Assert.Equal(new[] { "left" }, Ids(editor));
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void InvalidSelectionInputsDoNotChangeSelectionDocumentOrHistory()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><rect id='target' width='20' height='20'/></svg>");
        var target = editor.Document!.GetElementById<SvgRectangle>("target");
        editor.Select(target);
        var original = editor.GetSvg();
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SelectInLasso(new[] { new SKPoint(0, 0) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SelectInLasso(new[] { new SKPoint(0, 0), new SKPoint(float.NaN, 5), new SKPoint(10, 10) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SelectInLasso(Box(0, 0, 20, 20), (SvgEditorSelectionBehavior)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SelectByAppearance(target, new SvgMagicWandOptions { ColorTolerance = float.NaN }));
        Assert.Throws<ArgumentException>(() => editor.SelectByAppearance(target, new SvgMagicWandOptions { MatchFill = false }));
        Assert.Throws<ArgumentException>(() => editor.SelectByAppearance(new SvgRectangle()));
        Assert.Same(target, Assert.Single(editor.SelectedElements));
        Assert.Equal(original, editor.GetSvg());
        Assert.False(editor.CanUndo);
    }

    private static string[] Ids(SvgEditorController editor) => editor.SelectedElements.Select(element => element.ID).ToArray();
    private static SKPoint[] Box(float left, float top, float right, float bottom)
        => new[] { new SKPoint(left, top), new SKPoint(right, top), new SKPoint(right, bottom), new SKPoint(left, bottom) };
}
