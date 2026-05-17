using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Svg;
using Svg.DataTypes;
using Svg.FilterEffects;
using Svg.Model.Services;
using Svg.Skia.UnitTests.Common;
using Xunit;
using SkiaAlphaType = SkiaSharp.SKAlphaType;
using SkiaBitmap = SkiaSharp.SKBitmap;
using SkiaColor = SkiaSharp.SKColor;
using SkiaColors = SkiaSharp.SKColors;
using SkiaColorType = SkiaSharp.SKColorType;

namespace Svg.Skia.UnitTests;

public class SvgRetainedSceneGraphTests : SvgUnitTest
{
    private readonly record struct PathDrawInfo(SKRect Bounds, SKMatrix Matrix);

    [Fact]
    public void RetainedSceneGraph_BuildsIndexesForSimpleDocument()
    {
        using var svg = new SKSvg();
        svg.FromSvg(SimpleSvg);

        var scene = svg.RetainedSceneGraph;

        Assert.NotNull(scene);
        Assert.NotNull(scene!.Root);
        Assert.True(scene.TryGetNodeById("target", out var targetNode));
        Assert.NotNull(targetNode);
        Assert.Equal("target", targetNode!.ElementId);
        Assert.Contains(scene.Traverse(), static node => node.ElementTypeName.Contains("Rectangle", StringComparison.Ordinal));
    }

    [Fact]
    public void SvgSceneRuntime_CreatesModelForSimpleDocument()
    {
        using var svg = new SKSvg();
        var document = SvgService.FromSvg(SimpleSvg);

        var picture = SvgSceneRuntime.CreateModel(document, svg.AssetLoader);

        Assert.NotNull(picture);
        Assert.NotNull(picture!.Commands);
        Assert.NotEmpty(picture.Commands!);
    }

    [Fact]
    public void RetainedSceneGraph_CompilesDocumentRootWithDirectRetainedStrategy()
    {
        using var svg = new SKSvg();
        svg.FromSvg(SimpleSvg);

        var scene = svg.RetainedSceneGraph;

        Assert.NotNull(scene);
        Assert.Equal(SvgSceneCompilationStrategy.DirectRetained, scene!.Root.CompilationStrategy);
        Assert.True(scene.Root.IsRenderable);
    }

    [Fact]
    public void RetainedSceneGraph_CompilesCoreShapePrimitivesWithDirectRetainedStrategy()
    {
        using var svg = new SKSvg();
        svg.FromSvg(PrimitiveShapesSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);

        AssertCompilationStrategy(scene!, "shape-path", SvgSceneCompilationStrategy.DirectRetained);
        AssertCompilationStrategy(scene!, "shape-rect", SvgSceneCompilationStrategy.DirectRetained);
        AssertCompilationStrategy(scene!, "shape-circle", SvgSceneCompilationStrategy.DirectRetained);
        AssertCompilationStrategy(scene!, "shape-ellipse", SvgSceneCompilationStrategy.DirectRetained);
        AssertCompilationStrategy(scene!, "shape-line", SvgSceneCompilationStrategy.DirectRetained);
        AssertCompilationStrategy(scene!, "shape-polyline", SvgSceneCompilationStrategy.DirectRetained);
        AssertCompilationStrategy(scene!, "shape-polygon", SvgSceneCompilationStrategy.DirectRetained);
    }

    [Fact]
    public void RetainedSceneGraph_CreatesEquivalentPathsForShapePrimitives()
    {
        using var svg = new SKSvg();
        svg.FromSvg(PrimitiveShapesSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);

        AssertPathCommand<AddRectPathCommand>(scene!, "shape-rect");
        AssertPathCommand<AddCirclePathCommand>(scene, "shape-circle");
        AssertPathCommand<AddOvalPathCommand>(scene, "shape-ellipse");
        AssertPathCommands(scene, "shape-line", typeof(MoveToPathCommand), typeof(LineToPathCommand));
        AssertPathCommand<AddPolyPathCommand>(scene, "shape-polyline", static command => Assert.False(command.Close));
        AssertPathCommand<AddPolyPathCommand>(scene, "shape-polygon", static command => Assert.True(command.Close));
    }

    [Fact]
    public void RetainedSceneGraph_CompilesStructuralWrappersWithDirectRetainedStrategy()
    {
        using var svg = new SKSvg();
        svg.FromSvg(StructuralWrappersSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);

        AssertCompilationStrategy(scene!, "wrapper-group", SvgSceneCompilationStrategy.DirectRetained);
        AssertCompilationStrategy(scene!, "wrapper-anchor", SvgSceneCompilationStrategy.DirectRetained);
        AssertCompilationStrategy(scene!, "nested-fragment", SvgSceneCompilationStrategy.DirectRetained);
    }

    [Fact]
    public void RetainedSceneGraph_CompilesTextWithDirectRetainedStrategy()
    {
        using var svg = new SKSvg();
        svg.FromSvg(TextSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);

        AssertCompilationStrategy(scene!, "text-target", SvgSceneCompilationStrategy.DirectRetained);
        Assert.True(scene.TryGetNodeById("text-target", out var textNode));
        Assert.NotNull(textNode);
        Assert.NotNull(textNode!.LocalModel);
    }

    [Fact]
    public void RetainedSceneGraph_PreservesPerGlyphTextPositions()
    {
        const string positionedTextSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="80">
              <text id="positioned-root" x="10" y="20" font-size="16">
                <tspan id="positioned-run" x="10 30 50 70" y="20 40 20 40">ab😋c</tspan>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(positionedTextSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("positioned-root", out var textNode));
        Assert.NotNull(textNode);
        Assert.False(textNode!.GeometryBounds.IsEmpty);
        Assert.True(textNode.GeometryBounds.Width > 0f);
        Assert.True(textNode.GeometryBounds.Height > 0f);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var positionedPoints = retainedModel!
            .FindCommands<DrawTextBlobCanvasCommand>()
            .Where(static cmd => cmd.TextBlob?.Points is { Length: > 0 })
            .SelectMany(static cmd => cmd.TextBlob!.Points!)
            .Concat(retainedModel.FindCommands<DrawTextCanvasCommand>()
                .Where(static cmd => cmd.Text is "a" or "b" or "😋")
                .Select(static cmd => new SKPoint(cmd.X, cmd.Y)))
            .ToList();

        Assert.Equal(3, positionedPoints.Count);
        Assert.Contains(new SKPoint(10f, 20f), positionedPoints);
        Assert.Contains(new SKPoint(30f, 40f), positionedPoints);
        Assert.Contains(new SKPoint(50f, 20f), positionedPoints);

        var tailCommand = Assert.Single(retainedModel.FindCommands<DrawTextCanvasCommand>(),
            static cmd => cmd.X == 70f && cmd.Y == 40f);
        Assert.Equal("c", tailCommand.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetainedSceneGraph_PreservesPositionedTextPoints_ForSmallCapsFallbackWithoutCodepointExpansion(bool enableSvgFonts)
    {
        const string positionedTextSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="80">
              <text id="positioned-root" x="10" y="20" font-size="16" font-family="Times New Roman">
                <tspan id="positioned-run" x="10 30" y="20 20" font-variant="small-caps">ßa</tspan>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = enableSvgFonts;
        svg.FromSvg(positionedTextSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var leadingGlyphPosition = retainedModel!
            .FindCommands<DrawTextCanvasCommand>()
            .Single(static cmd => cmd.X == 10f && cmd.Y == 20f);

        var tailCommand = Assert.Single(retainedModel.FindCommands<DrawTextCanvasCommand>(),
            static cmd => cmd.X == 30f && cmd.Y == 20f);
        Assert.Equal("ß", leadingGlyphPosition.Text);
        Assert.Equal("A", tailCommand.Text);
    }

    [Fact]
    public void RetainedSceneGraph_AppliesRootDxDyToSequentialTextRunOrigin()
    {
        const string dxDySvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="80">
              <text dx="33" dy="20" font-size="16">Text</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(dxDySvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var origins = retainedModel!
            .FindCommands<DrawTextCanvasCommand>()
            .Where(static cmd => cmd.Text.Contains("Text", StringComparison.Ordinal))
            .Select(static cmd => new SKPoint(cmd.X, cmd.Y))
            .Concat(retainedModel.FindCommands<DrawTextBlobCanvasCommand>()
                .Where(static cmd => cmd.TextBlob is not null)
                .Select(static cmd => new SKPoint(cmd.X, cmd.Y)))
            .ToList();

        Assert.Contains(origins, static point => Math.Abs(point.X - 33f) < 1f && Math.Abs(point.Y - 20f) < 1f);
    }

    [Fact]
    public void RetainedSceneGraph_PreservesBaselineShiftForMixedDirectionSequentialRuns()
    {
        const string baselineShiftSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="160" height="80">
              <text x="10" y="40" font-size="16" font-family="Arial">abc<tspan baseline-shift="10">אבג</tspan></text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.FromSvg(baselineShiftSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var blobBaselineBands = retainedModel!
            .FindCommands<DrawTextBlobCanvasCommand>()
            .Where(static cmd => cmd.TextBlob?.Points is { Length: > 0 })
            .Select(cmd => (double)cmd.TextBlob!.Points!
                .Select(point => point.Y + cmd.Y)
                .Average())
            .ToList();

        var textBaselineBands = retainedModel
            .FindCommands<DrawTextCanvasCommand>()
            .Where(static cmd => !string.IsNullOrWhiteSpace(cmd.Text))
            .Select(static cmd => (double)cmd.Y)
            .ToList();

        var baselineBands = blobBaselineBands
            .Concat(textBaselineBands)
            .OrderBy(static y => y)
            .ToList();

        Assert.True(
            baselineBands.Count >= 2,
            $"Expected mixed-direction sequential text with baseline-shift to emit at least two baseline bands, but found {baselineBands.Count}: {string.Join(", ", baselineBands.Select(static y => y.ToString("F2", CultureInfo.InvariantCulture)))}");

        Assert.True(
            baselineBands.Zip(baselineBands.Skip(1), static (top, bottom) => bottom - top).Any(static delta => delta > 5d),
            $"Expected baseline-shift to separate the shifted run from the base line, but found bands: {string.Join(", ", baselineBands.Select(static y => y.ToString("F2", CultureInfo.InvariantCulture)))}");
    }

    [Fact]
    public void RetainedSceneGraph_PreservesInterTspanSpacesForNestedRotateFixture()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.Settings.EnableTextReferences = false;
        svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 480f, 360f);
        svg.Load(Path.Combine("..", "..", "..", "..", "..", "externals", "W3C_SVG_11_TestSuite", "W3C_SVG_11_TestSuite", "svg", "text-tspan-02-b.svg"));

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var secondLineSpaces = retainedModel!
            .FindCommands<DrawTextCanvasCommand>()
            .Where(static cmd =>
                cmd.Text == " "
                && Math.Abs(cmd.Y - 180f) < 0.5f
                && cmd.Paint?.Color is SKColor color
                && color.Equals(new SKColor(0x00, 0x80, 0x00, 0xFF)))
            .Select(static cmd => cmd.X)
            .OrderBy(static x => x)
            .ToList();

        var uniqueSecondLineSpaces = new List<float>();
        foreach (var x in secondLineSpaces)
        {
            if (uniqueSecondLineSpaces.Count == 0 || Math.Abs(uniqueSecondLineSpaces[^1] - x) > 0.5f)
            {
                uniqueSecondLineSpaces.Add(x);
            }
        }

        Assert.Equal(
            4,
            uniqueSecondLineSpaces.Count);
        Assert.True(
            uniqueSecondLineSpaces.Zip(uniqueSecondLineSpaces.Skip(1), static (left, right) => right - left).All(static gap => gap > 10f),
            $"Expected the nested tspan fixture to preserve four distinct visible second-line spaces, but found: {string.Join(", ", uniqueSecondLineSpaces.Select(static x => x.ToString("F3", CultureInfo.InvariantCulture)))}");
    }

    [Fact]
    public void RetainedSceneGraph_SvgFontNestedRotateFixture_AlignsReferenceAndNestedGlyphBounds()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = true;
        svg.Settings.EnableTextReferences = false;
        svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 480f, 360f);
        svg.Load(Path.Combine("..", "..", "..", "..", "..", "externals", "W3C_SVG_11_TestSuite", "W3C_SVG_11_TestSuite", "svg", "text-tspan-02-b.svg"));

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var redGlyphs = GetColoredPathDrawBounds(retainedModel!, new SKColor(0xFF, 0x00, 0x00, 0xFF));
        var greenGlyphs = GetColoredPathDrawBounds(retainedModel, new SKColor(0x00, 0x80, 0x00, 0xFF));

        Assert.Equal(redGlyphs.Count, greenGlyphs.Count);

        var mismatches = redGlyphs
            .Select((red, index) => (red, green: greenGlyphs[index], index))
            .Select(static pair =>
            {
                var redCenter = new SKPoint((pair.red.Bounds.Left + pair.red.Bounds.Right) * 0.5f, (pair.red.Bounds.Top + pair.red.Bounds.Bottom) * 0.5f);
                var greenCenter = new SKPoint((pair.green.Bounds.Left + pair.green.Bounds.Right) * 0.5f, (pair.green.Bounds.Top + pair.green.Bounds.Bottom) * 0.5f);
                var deltaX = Math.Abs(redCenter.X - greenCenter.X);
                var deltaY = Math.Abs(redCenter.Y - greenCenter.Y);
                return new
                {
                    pair.red,
                    pair.green,
                    pair.index,
                    DeltaX = deltaX,
                    DeltaY = deltaY,
                    Total = deltaX + deltaY
                };
            })
            .Where(static item => item.Total > 0.5f)
            .OrderByDescending(static item => item.Total)
            .Take(8)
            .ToList();

        var sequenceWindow = string.Join(
            " | ",
            redGlyphs
                .Select((red, index) => new { index, red, green = greenGlyphs[index] })
                .Where(static item => item.index >= 26 && item.index <= 34)
                .Select(static item => $"i={item.index},redMatrix={item.red.Matrix},greenMatrix={item.green.Matrix},redBounds={item.red.Bounds},greenBounds={item.green.Bounds}"));

        Assert.True(
            mismatches.Count == 0,
            $"Expected nested tspan SVG-font glyph bounds to align with the flat reference text. Largest deltas: {string.Join("; ", mismatches.Select(static item => $"index={item.index},dx={item.DeltaX:F2},dy={item.DeltaY:F2},redBounds={item.red.Bounds},greenBounds={item.green.Bounds},redMatrix={item.red.Matrix},greenMatrix={item.green.Matrix}"))}. Sequence window: {sequenceWindow}");
    }

    [Fact]
    public void RetainedSceneGraph_PreservesRootDxDyListGlyphOrigins()
    {
        const string dxDySvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200">
              <text dx="20 6 10 16" dy="100 10 15 20" font-family="Noto Sans" font-size="64">Text</text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(dxDySvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var positionedPoints = retainedModel!
            .FindCommands<DrawTextBlobCanvasCommand>()
            .Where(static cmd => cmd.TextBlob?.Points is { Length: > 0 })
            .SelectMany(static cmd => cmd.TextBlob!.Points!)
            .Concat(retainedModel.FindCommands<DrawTextCanvasCommand>()
                .Where(static cmd => cmd.Text is "T" or "e" or "x" or "t")
                .Select(static cmd => new SKPoint(cmd.X, cmd.Y)))
            .OrderBy(static point => point.X)
            .ToList();

        Assert.Equal(4, positionedPoints.Count);
        Assert.Contains(positionedPoints, static point => Math.Abs(point.X - 20f) < 1f && Math.Abs(point.Y - 100f) < 1f);
        Assert.True(positionedPoints.Select(static point => point.Y).Distinct().Count() > 1,
            "Expected root dx/dy lists to produce multiple glyph Y origins.");
    }

    [Fact]
    public void RetainedSceneGraph_TextPathPercentStartOffset_FollowsArcGeometry()
    {
        const string arcTextSvg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="120"
                 height="120"
                 viewBox="0 0 120 120">
              <defs>
                <path id="arc-path" d="M10,60 A50,50 0 0 1 110,60" />
              </defs>
              <text fill="#0055aa" font-size="12">
                <textPath xlink:href="#arc-path" startOffset="50%">A</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(arcTextSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var matrix = Assert.Single(retainedModel!.FindCommands<SetMatrixCanvasCommand>());
        var drawText = Assert.Single(retainedModel.FindCommands<DrawTextCanvasCommand>(), static cmd => cmd.Text == "A");
        var anchorPoint = matrix.TotalMatrix.MapPoint(new SKPoint(drawText.X, drawText.Y));

        Assert.InRange(anchorPoint.X, 55f, 65f);
        Assert.True(anchorPoint.Y < 30f,
            $"Expected 50% startOffset to land on the sampled arc midpoint instead of the straight chord, but anchor point was {anchorPoint} from matrix {matrix.DeltaMatrix}.");
    }

    [Fact]
    public void RetainedSceneGraph_TextPathInlinePathWinsOverHref()
    {
        const string inlinePathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <defs>
                <path id="href-path" d="M10,80 L110,80" />
              </defs>
              <text fill="#0055aa" font-size="12">
                <textPath href="#href-path" path="M10,20 L110,20">A</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(inlinePathSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var drawText = Assert.Single(retainedModel!.FindCommands<DrawTextCanvasCommand>(), static cmd => cmd.Text == "A");
        Assert.InRange(drawText.Y, 18f, 22f);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathHrefCanTargetBasicShape()
    {
        const string shapeTargetSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="100" viewBox="0 0 120 100">
              <defs>
                <line id="target-line" x1="10" y1="45" x2="110" y2="45" />
              </defs>
              <text fill="#0055aa" font-size="12">
                <textPath href="#target-line">B</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(shapeTargetSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var drawText = Assert.Single(retainedModel!.FindCommands<DrawTextCanvasCommand>(), static cmd => cmd.Text == "B");
        Assert.InRange(drawText.Y, 43f, 47f);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathSideRight_PreservesPathDirection()
    {
        const string sideRightSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="80" viewBox="0 0 140 80">
              <defs>
                <path id="line" d="M20,40 L120,40" />
              </defs>
              <text fill="#0055aa" font-size="12">
                <textPath href="#line" side="right">A</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(sideRightSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<SetMatrixCanvasCommand>());
        var drawText = Assert.Single(retainedModel.FindCommands<DrawTextCanvasCommand>(), static cmd => cmd.Text == "A");

        Assert.InRange(drawText.X, 18f, 24f);
        Assert.InRange(drawText.Y, 38f, 42f);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathSideLeftAndInvalid_MatchDefaultPlacement()
    {
        const string sideDefaultSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="120" viewBox="0 0 140 120">
              <defs>
                <path id="default-line" d="M20,25 L120,25" />
                <path id="left-line" d="M20,55 L120,55" />
                <path id="invalid-line" d="M20,85 L120,85" />
              </defs>
              <text fill="#0055aa" font-size="12">
                <textPath href="#default-line">A</textPath>
                <textPath href="#left-line" side="left">B</textPath>
                <textPath href="#invalid-line" side="sideways">C</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(sideDefaultSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var glyphs = retainedModel!
            .FindCommands<DrawTextCanvasCommand>()
            .Where(static command => command.Text is "A" or "B" or "C")
            .ToDictionary(static command => command.Text, static command => new SKPoint(command.X, command.Y), StringComparer.Ordinal);

        Assert.True(glyphs.TryGetValue("A", out var defaultGlyph), "Expected default side textPath glyph.");
        Assert.True(glyphs.TryGetValue("B", out var leftGlyph), "Expected side=left textPath glyph.");
        Assert.True(glyphs.TryGetValue("C", out var invalidGlyph), "Expected invalid side textPath glyph.");
        Assert.InRange(defaultGlyph.Y, 23f, 27f);
        Assert.InRange(leftGlyph.Y, 53f, 57f);
        Assert.InRange(invalidGlyph.Y, 83f, 87f);
        Assert.InRange(defaultGlyph.X, 18f, 24f);
        Assert.InRange(leftGlyph.X, 18f, 24f);
        Assert.InRange(invalidGlyph.X, 18f, 24f);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathPathLength_ScalesDistanceMapping()
    {
        const string pathLengthSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="80" viewBox="0 0 140 80">
              <defs>
                <path id="line" d="M10,40 L110,40" pathLength="50" />
              </defs>
              <text fill="#0055aa" font-size="4">
                <textPath href="#line" startOffset="25">A</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(pathLengthSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var drawText = Assert.Single(retainedModel!.FindCommands<DrawTextCanvasCommand>(), static cmd => cmd.Text == "A");
        Assert.InRange(drawText.X, 55f, 70f);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathClosedLoop_WrapsPastPathEnd()
    {
        const string closedLoopSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120">
              <defs>
                <path id="box" d="M20,20 L100,20 L100,100 L20,100 Z" />
              </defs>
              <text fill="#0055aa" font-size="8">
                <textPath href="#box" startOffset="325">A</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(closedLoopSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var drawText = Assert.Single(retainedModel!.FindCommands<DrawTextCanvasCommand>(), static cmd => cmd.Text == "A");
        Assert.InRange(drawText.X, 20f, 35f);
        Assert.InRange(drawText.Y, 18f, 22f);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathTspanDx_AdvancesAlongPath()
    {
        const string dxSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="160" viewBox="0 0 140 160">
              <defs>
                <path id="line" d="M60,20 L60,140" />
              </defs>
              <text fill="#0055aa" font-size="10">
                <textPath href="#line">A<tspan dx="20">B</tspan></textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.FromSvg(dxSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var glyphPositions = retainedModel!
            .FindCommands<DrawTextCanvasCommand>()
            .Where(static command => command.Text is "A" or "B")
            .ToDictionary(static command => command.Text, static command => new SKPoint(command.X, command.Y), StringComparer.Ordinal);

        Assert.True(glyphPositions.TryGetValue("A", out var a), "Expected textPath to render the first glyph.");
        Assert.True(glyphPositions.TryGetValue("B", out var b), "Expected textPath to render the second glyph.");
        Assert.True(
            Math.Abs(b.X - a.X) < 8f,
            $"Expected tspan dx to stay on the textPath instead of moving in user space, but A was {a} and B was {b}.");
        Assert.True(
            b.Y > a.Y + 15f,
            $"Expected tspan dx to advance the second textPath glyph along the path, but A was {a} and B was {b}.");
    }

    [Fact]
    public void RetainedSceneGraph_WhiteSpacePre_PreservesStaticTextRuns()
    {
        const string whiteSpaceSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="80" viewBox="0 0 180 80">
              <text x="10" y="25" font-size="12" white-space="normal">A   B</text>
              <text x="10" y="55" font-size="12" white-space="pre">A   B</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(whiteSpaceSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var renderedTexts = retainedModel!
            .FindCommands<DrawTextCanvasCommand>()
            .Select(static cmd => cmd.Text)
            .ToList();

        Assert.Contains("A B", renderedTexts);
        Assert.Contains("A   B", renderedTexts);
    }

    [Fact]
    public void RetainedSceneGraph_WhiteSpaceNormal_OverridesXmlSpacePreserve()
    {
        const string whiteSpaceSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="50" viewBox="0 0 180 50">
              <text x="10" y="25" font-size="12" xml:space="preserve" white-space="normal">A   B</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(whiteSpaceSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var renderedTexts = retainedModel!
            .FindCommands<DrawTextCanvasCommand>()
            .Select(static cmd => cmd.Text)
            .ToList();

        Assert.Contains("A B", renderedTexts);
        Assert.DoesNotContain("A   B", renderedTexts);
    }

    [Fact]
    public void RetainedSceneGraph_TextLengthSpacing_PositionsGlyphsAcrossRequestedAdvance()
    {
        const string textLengthSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 200 200">
              <text x="20" y="100" font-family="Noto Sans" font-size="48" textLength="150">Text</text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(textLengthSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var positions = retainedModel!
            .FindCommands<DrawTextCanvasCommand>()
            .Where(static cmd => cmd.Y == 100f)
            .Select(static cmd => cmd.X)
            .OrderBy(static x => x)
            .ToArray();

        Assert.Equal(4, positions.Length);
        Assert.Equal(20f, positions[0], 1);
        Assert.True(positions[^1] > 120f, $"Expected textLength spacing to spread the glyph origins, but got final X={positions[^1]}.");
    }

    [Fact]
    public void RetainedSceneGraph_LengthAdjustSpacingAndGlyphs_UsesHorizontalScaleTransform()
    {
        const string lengthAdjustSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 200 200">
              <text x="20" y="100" font-family="Noto Sans" font-size="48" textLength="150" lengthAdjust="spacingAndGlyphs">Text</text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(lengthAdjustSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var scaleMatrices = retainedModel!
            .FindCommands<SetMatrixCanvasCommand>()
            .Select(static cmd => cmd.DeltaMatrix)
            .Where(static matrix => matrix.ScaleX > 1.1f)
            .ToArray();

        Assert.NotEmpty(scaleMatrices);
    }

    [Fact]
    public void TextReferences_CanBeDisabled_ForBrowserCompatibleRendering()
    {
        const string textRefSvg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="120"
                 height="40"
                 viewBox="0 0 120 40">
              <defs>
                <text id="text-source">Source Text</text>
              </defs>
              <text x="4" y="20" font-size="12">
                <tref xlink:href="#text-source" />
              </text>
            </svg>
            """;

        using var enabledSvg = new SKSvg();
        enabledSvg.Settings.EnableTextReferences = true;
        enabledSvg.FromSvg(textRefSvg);

        using var disabledSvg = new SKSvg();
        disabledSvg.Settings.EnableTextReferences = false;
        disabledSvg.FromSvg(textRefSvg);

        Assert.NotNull(enabledSvg.Model);
        Assert.NotNull(disabledSvg.Model);
        Assert.True(
            enabledSvg.Model!.FindCommands<DrawTextCanvasCommand>().Any() ||
            enabledSvg.Model.FindCommands<DrawTextBlobCanvasCommand>().Any(),
            "Expected tref content to render when text references are enabled.");
        Assert.DoesNotContain(
            disabledSvg.Model!.FindCommands<DrawTextCanvasCommand>(),
            static cmd => !string.IsNullOrWhiteSpace(cmd.Text));
        Assert.DoesNotContain(
            disabledSvg.Model.FindCommands<DrawTextBlobCanvasCommand>(),
            static cmd => !string.IsNullOrWhiteSpace(cmd.TextBlob?.Text));
    }

    [Fact]
    public void TextReferences_RenderInlineReferencedContentBetweenSiblingTextRuns()
    {
        const string inlineTextRefSvg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="120"
                 height="40"
                 viewBox="0 0 120 40">
              <defs>
                <text id="text-source">Ref</text>
              </defs>
              <text x="4" y="20" font-size="12">A<tref xlink:href="#text-source" />B</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.Settings.EnableTextReferences = true;
        svg.FromSvg(inlineTextRefSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var renderedText = string.Concat(
            retainedModel!.FindCommands<DrawTextCanvasCommand>()
                .Select(static command => command.Text)
                .Concat(retainedModel.FindCommands<DrawTextBlobCanvasCommand>()
                    .Select(static command => command.TextBlob?.Text))
                .Where(static text => !string.IsNullOrEmpty(text)));

        Assert.Contains("ARefB", renderedText, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemTextRendering_IsStable_WhenSvgFontsSettingChanges()
    {
        const string systemTextSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="100">
              <text x="10" y="24" font-size="18" font-family="sans-serif" font-weight="bold">Bold Text 20px</text>
              <text x="10" y="54" font-size="18" font-family="Times New Roman" font-variant="small-caps">ßa</text>
              <text x="10" y="84" font-size="18" font-family="Missing Family, serif">Fallback serif sample</text>
            </svg>
            """;

        using var disabledSvg = new SKSvg();
        disabledSvg.Settings.EnableSvgFonts = false;
        disabledSvg.FromSvg(systemTextSvg);

        using var enabledSvg = new SKSvg();
        enabledSvg.Settings.EnableSvgFonts = true;
        enabledSvg.FromSvg(systemTextSvg);

        Assert.NotNull(disabledSvg.Picture);
        Assert.NotNull(enabledSvg.Picture);
        AssertPicturesEqual(disabledSvg, disabledSvg.Picture!, enabledSvg.Picture!);
    }

    [Fact]
    public void DefaultSettings_RenderSvgFontGlyphCommands()
    {
        const string svgFontGlyphSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120">
              <defs>
                <style type="text/css"><![CDATA[
                  @font-face {
                    font-family: 'DefaultFont';
                    src: url('#DefaultFontFace') format('svg');
                  }
                ]]></style>
                <font id="DefaultFontFace" horiz-adv-x="100">
                  <font-face font-family="DefaultFont" units-per-em="100" ascent="100" descent="0" />
                  <glyph unicode="A" horiz-adv-x="100" d="M10 0H30V100H10Z" />
                </font>
              </defs>
              <text x="10" y="110" fill="black" font-family="DefaultFont" font-size="100">A</text>
            </svg>
            """;

        using var defaultSvg = new SKSvg();
        defaultSvg.FromSvg(svgFontGlyphSvg);

        using var enabledSvg = new SKSvg();
        enabledSvg.Settings.EnableSvgFonts = true;
        enabledSvg.FromSvg(svgFontGlyphSvg);

        using var disabledSvg = new SKSvg();
        disabledSvg.Settings.EnableSvgFonts = false;
        disabledSvg.FromSvg(svgFontGlyphSvg);

        Assert.NotNull(defaultSvg.Model);
        Assert.NotNull(enabledSvg.Model);
        Assert.NotNull(disabledSvg.Model);
        Assert.NotNull(defaultSvg.Picture);
        Assert.NotNull(enabledSvg.Picture);

        AssertPicturesEqual(defaultSvg, defaultSvg.Picture!, enabledSvg.Picture!);
        Assert.NotEmpty(defaultSvg.Model!.FindCommands<DrawPathCanvasCommand>());
        Assert.Empty(defaultSvg.Model.FindCommands<DrawTextCanvasCommand>());
        Assert.Empty(defaultSvg.Model.FindCommands<DrawTextBlobCanvasCommand>());
        Assert.Empty(disabledSvg.Model!.FindCommands<DrawPathCanvasCommand>());
    }

    [Fact]
    public void RetainedSceneGraph_UsesArabicJoiningTypes_ForSvgFontArabicForms()
    {
        const string arabicFormSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="320" height="260" viewBox="0 0 320 260">
              <defs>
                <style type="text/css"><![CDATA[
                  @font-face {
                    font-family: 'TestArabic';
                    src: url('#TestArabicFont') format('svg');
                  }
                ]]></style>
                <font id="TestArabicFont" horiz-adv-x="100">
                  <font-face font-family="TestArabic" units-per-em="100" ascent="100" descent="0" />
                  <missing-glyph horiz-adv-x="100" />
                  <glyph unicode="ب" arabic-form="isolated" d="M0 0H20V100H0Z" />
                  <glyph unicode="ب" arabic-form="initial" d="M25 0H45V100H25Z" />
                  <glyph unicode="ب" arabic-form="medial" d="M50 0H70V100H50Z" />
                  <glyph unicode="ب" arabic-form="terminal" d="M75 0H95V100H75Z" />
                  <glyph unicode="ا" horiz-adv-x="100" d="M40 0H60V100H40Z" />
                  <glyph unicode="،" horiz-adv-x="100" d="M40 0H60V100H40Z" />
                </font>
              </defs>
              <g fill="black" font-family="TestArabic" font-size="100">
                <text x="10" y="110">ب،ب</text>
                <text x="10" y="240">باب</text>
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = true;
        svg.FromSvg(arabicFormSvg);

        Assert.NotNull(svg.Picture);

        using var bitmap = ToBitmap(svg, svg.Picture!);

        var punctuationLeft = bitmap.GetPixel(15, 60);
        var punctuationLeftInitialBand = bitmap.GetPixel(40, 60);
        var punctuationRight = bitmap.GetPixel(215, 60);
        var punctuationRightTerminalBand = bitmap.GetPixel(290, 60);
        var rightJoiningInitial = bitmap.GetPixel(40, 190);
        var rightJoiningFinal = bitmap.GetPixel(215, 190);
        var rightJoiningFinalTerminalBand = bitmap.GetPixel(290, 190);

        Assert.True(punctuationLeft.Alpha > 0, $"Expected punctuation-broken run to use the isolated form on the first glyph, but was {punctuationLeft}.");
        Assert.Equal(0, punctuationLeftInitialBand.Alpha);
        Assert.True(punctuationRight.Alpha > 0, $"Expected punctuation-broken run to keep the trailing glyph isolated, but was {punctuationRight}.");
        Assert.Equal(0, punctuationRightTerminalBand.Alpha);
        Assert.True(rightJoiningInitial.Alpha > 0, $"Expected the leading beh before alef to use the initial form, but was {rightJoiningInitial}.");
        Assert.True(rightJoiningFinal.Alpha > 0, $"Expected the trailing beh after alef to remain isolated, but was {rightJoiningFinal}.");
        Assert.Equal(0, rightJoiningFinalTerminalBand.Alpha);
    }

    [Fact]
    public void RetainedSceneGraph_PrefersLongestSvgFontUnicodeMatch()
    {
        const string ligatureSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="140" viewBox="0 0 220 140">
              <defs>
                <style type="text/css"><![CDATA[
                  @font-face {
                    font-family: 'LigatureFont';
                    src: url('#LigatureFont') format('svg');
                  }
                ]]></style>
                <font id="LigatureFont" horiz-adv-x="100">
                  <font-face font-family="LigatureFont" units-per-em="100" ascent="100" descent="0" />
                  <missing-glyph horiz-adv-x="100" />
                  <glyph unicode="ff" horiz-adv-x="100" d="M0 0H20V100H0Z" />
                  <glyph unicode="ffi" horiz-adv-x="100" d="M40 0H60V100H40Z" />
                  <glyph unicode="i" horiz-adv-x="100" d="M80 0H100V100H80Z" />
                </font>
              </defs>
              <text x="10" y="110" fill="black" font-family="LigatureFont" font-size="100">ffi</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = true;
        svg.FromSvg(ligatureSvg);

        Assert.NotNull(svg.Picture);

        using var bitmap = ToBitmap(svg, svg.Picture!);

        var ligaturePixel = bitmap.GetPixel(55, 60);
        var shorterPrefixPixel = bitmap.GetPixel(15, 60);
        var trailingGlyphPixel = bitmap.GetPixel(195, 60);

        Assert.True(ligaturePixel.Alpha > 0, $"Expected the longest ffi ligature glyph to render, but was {ligaturePixel}.");
        Assert.Equal(0, shorterPrefixPixel.Alpha);
        Assert.Equal(0, trailingGlyphPixel.Alpha);
    }

    [Fact]
    public void SvgFontLayout_AllowsMixedUnicodeRangeRuns_WithPerGlyphFallback()
    {
        const string actualSvgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="140" viewBox="0 0 240 140">
              <defs>
                <style type="text/css"><![CDATA[
                  @font-face {
                    font-family: 'MixedRange';
                    src: url('#MixedRangeFont') format('svg');
                  }
                ]]></style>
                <font id="MixedRangeFont" horiz-adv-x="100">
                  <font-face font-family="MixedRange" units-per-em="100" ascent="100" descent="0" unicode-range="U+0041" />
                  <glyph unicode="A" horiz-adv-x="100" d="M0 0H20V100H0Z" />
                  <glyph unicode="Ω" horiz-adv-x="100" d="M70 10H90V30H70Z" />
                </font>
              </defs>
              <text x="10" y="110" fill="black" font-family="MixedRange, serif" font-size="100">AΩ</text>
            </svg>
            """;

        const string expectedSvgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="140" viewBox="0 0 240 140">
              <defs>
                <style type="text/css"><![CDATA[
                  @font-face {
                    font-family: 'MixedRange';
                    src: url('#MixedRangeFont') format('svg');
                  }
                ]]></style>
                <font id="MixedRangeFont" horiz-adv-x="100">
                  <font-face font-family="MixedRange" units-per-em="100" ascent="100" descent="0" unicode-range="U+0041" />
                  <glyph unicode="A" horiz-adv-x="100" d="M0 0H20V100H0Z" />
                </font>
              </defs>
              <text x="10" y="110" fill="black" font-family="MixedRange, serif" font-size="100">AΩ</text>
            </svg>
            """;

        using var actualSvg = new SKSvg();
        actualSvg.Settings.EnableSvgFonts = true;
        actualSvg.FromSvg(actualSvgMarkup);

        using var expectedSvg = new SKSvg();
        expectedSvg.Settings.EnableSvgFonts = true;
        expectedSvg.FromSvg(expectedSvgMarkup);

        Assert.NotNull(actualSvg.Picture);
        Assert.NotNull(expectedSvg.Picture);
        AssertPicturesEqual(expectedSvg, expectedSvg.Picture!, actualSvg.Picture!);
    }

    [Fact]
    public void SvgFontLayout_PrefersSystemFallbackOverMissingGlyph_WhenSvgFontHasNoGlyphs()
    {
        const string actualSvgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="360" height="120" viewBox="0 0 360 120">
              <defs>
                <style type="text/css"><![CDATA[
                  @font-face {
                    font-family: 'MissingInAction';
                    src: url('#MissingInActionFont') format('svg');
                  }
                ]]></style>
                <font id="MissingInActionFont" horiz-adv-x="100">
                  <font-face font-family="MissingInAction" units-per-em="100" ascent="100" descent="0" />
                  <missing-glyph d="M10,30h20v20h-20z" />
                </font>
              </defs>
              <g fill="black" font-family="MissingInAction, sans-serif" font-size="24">
                <text x="10" y="40">Polish: Mogę jeść szkło.</text>
                <text x="10" y="80">Hebrew: אני יכול.</text>
              </g>
            </svg>
            """;

        const string expectedSvgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="360" height="120" viewBox="0 0 360 120">
              <g fill="black" font-family="sans-serif" font-size="24">
                <text x="10" y="40">Polish: Mogę jeść szkło.</text>
                <text x="10" y="80">Hebrew: אני יכול.</text>
              </g>
            </svg>
            """;

        using var actualSvg = new SKSvg();
        actualSvg.Settings.EnableSvgFonts = true;
        actualSvg.FromSvg(actualSvgMarkup);

        using var expectedSvg = new SKSvg();
        expectedSvg.Settings.EnableSvgFonts = true;
        expectedSvg.FromSvg(expectedSvgMarkup);

        Assert.NotNull(actualSvg.Picture);
        Assert.NotNull(expectedSvg.Picture);
        AssertPicturesEqual(expectedSvg, expectedSvg.Picture!, actualSvg.Picture!);
    }

    [Fact]
    public void LinkPseudoClass_StylesAnchorGlyphsInsideMixedTextRuns()
    {
        const string actualSvgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="220"
                 height="80"
                 viewBox="0 0 220 80">
              <style>a:link { fill: red; }</style>
              <text x="10" y="48" fill="black" font-family="sans-serif" font-size="24">pre <a xlink:href="#target">link</a> post</text>
            </svg>
            """;

        const string expectedSvgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 width="220"
                 height="80"
                 viewBox="0 0 220 80">
              <text x="10" y="48" fill="black" font-family="sans-serif" font-size="24">pre <tspan fill="red">link</tspan> post</text>
            </svg>
            """;

        using var actualSvg = new SKSvg();
        actualSvg.FromSvg(actualSvgMarkup);

        using var expectedSvg = new SKSvg();
        expectedSvg.FromSvg(expectedSvgMarkup);

        Assert.NotNull(actualSvg.Picture);
        Assert.NotNull(expectedSvg.Picture);
        AssertPicturesEqual(expectedSvg, expectedSvg.Picture!, actualSvg.Picture!);
    }

    private static List<PathDrawInfo> GetColoredPathDrawBounds(SKPicture picture, SKColor color)
    {
        var draws = new List<PathDrawInfo>();
        CollectColoredPathDrawBounds(picture.Commands, SKMatrix.Identity, new Stack<SKMatrix>(), color, draws);
        return draws;
    }

    private static void CollectColoredPathDrawBounds(
        IList<CanvasCommand>? commands,
        SKMatrix currentMatrix,
        Stack<SKMatrix> matrixStack,
        SKColor color,
        List<PathDrawInfo> draws)
    {
        if (commands is null)
        {
            return;
        }

        foreach (var command in commands)
        {
            switch (command)
            {
                case SaveCanvasCommand:
                    matrixStack.Push(currentMatrix);
                    break;

                case RestoreCanvasCommand:
                    if (matrixStack.Count > 0)
                    {
                        currentMatrix = matrixStack.Pop();
                    }

                    break;

                case SetMatrixCanvasCommand setMatrixCanvasCommand:
                    currentMatrix = setMatrixCanvasCommand.TotalMatrix;
                    break;

                case DrawPictureCanvasCommand { Picture: { } nestedPicture }:
                    var nestedStack = new Stack<SKMatrix>(matrixStack.Reverse());
                    CollectColoredPathDrawBounds(nestedPicture.Commands, currentMatrix, nestedStack, color, draws);
                    break;

                case DrawPathCanvasCommand { Path: { } path, Paint: { } paint }
                    when paint.Style == SKPaintStyle.Fill && paint.Color is SKColor paintColor && paintColor.Equals(color) && !path.IsEmpty:
                    draws.Add(new PathDrawInfo(currentMatrix.MapRect(path.Bounds), currentMatrix));
                    break;
            }
        }
    }

    [Fact]
    public void RetainedSceneGraph_ResolvesRetainedMaskPayloadsForDirectNodes()
    {
        using var svg = new SKSvg();
        svg.FromSvg(MaskedSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);

        AssertCompilationStrategy(scene!, "masked-target", SvgSceneCompilationStrategy.DirectRetained);
        Assert.True(scene.TryGetNodeById("masked-target", out var maskedNode));
        Assert.NotNull(maskedNode);
        Assert.NotNull(maskedNode!.MaskNode);
        Assert.NotNull(maskedNode.MaskPaint);
        Assert.NotNull(maskedNode.MaskDstIn);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("mask-type=\"alpha\"", true)]
    [InlineData("style=\"mask-type:alpha\"", true)]
    [InlineData("mask-type=\"unexpected\"", false)]
    [InlineData("style=\"mask-type:unexpected\"", false)]
    public void RetainedSceneGraph_RendersMaskTypeAlphaAndLuminanceCoverage(
        string maskTypeAttributes,
        bool shouldRevealBlackMaskArea)
    {
        var maskTypeSvg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <mask id="black-mask"
                      maskUnits="userSpaceOnUse"
                      maskContentUnits="userSpaceOnUse"
                      x="0" y="0" width="20" height="20"
                      {{maskTypeAttributes}}>
                  <rect x="0" y="0" width="20" height="20" fill="black" />
                </mask>
              </defs>
              <rect id="masked-target" x="0" y="0" width="20" height="20" fill="red" mask="url(#black-mask)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(maskTypeSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);
        var coveredPixel = bitmap.GetPixel(10, 10);

        if (shouldRevealBlackMaskArea)
        {
            Assert.True(
                coveredPixel.Red > 200 && coveredPixel.Alpha > 200,
                $"Expected alpha mask-type to reveal opaque black mask content but was {coveredPixel}.");
        }
        else
        {
            Assert.Equal(0, coveredPixel.Alpha);
        }
    }

    [Fact]
    public void RetainedSceneGraph_StylesheetMaskTypeOverridesPresentationAttribute()
    {
        const string maskTypeSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <style>#black-mask { mask-type: alpha; }</style>
              <defs>
                <mask id="black-mask"
                      maskUnits="userSpaceOnUse"
                      maskContentUnits="userSpaceOnUse"
                      x="0" y="0" width="20" height="20"
                      mask-type="luminance">
                  <rect x="0" y="0" width="20" height="20" fill="black" />
                </mask>
              </defs>
              <rect id="masked-target" x="0" y="0" width="20" height="20" fill="red" mask="url(#black-mask)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(maskTypeSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);
        var coveredPixel = bitmap.GetPixel(10, 10);

        Assert.True(
            coveredPixel.Red > 200 && coveredPixel.Alpha > 200,
            $"Expected stylesheet mask-type alpha to override presentation luminance but was {coveredPixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_ResolvesRetainedFilterPayloadsForDirectNodes()
    {
        using var svg = new SKSvg();
        svg.FromSvg(FilteredSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);

        AssertCompilationStrategy(scene!, "filtered-target", SvgSceneCompilationStrategy.DirectRetained);
        Assert.True(scene.TryGetNodeById("filtered-target", out var filteredNode));
        Assert.NotNull(filteredNode);
        Assert.NotNull(filteredNode!.Filter);
    }

    [Fact]
    public void RetainedSceneGraph_RendersFeDropShadowOutsideSourceBounds()
    {
        const string dropShadowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="80">
              <defs>
                <filter id="shadow" x="-100%" y="-100%" width="500%" height="300%">
                  <feDropShadow in="SourceGraphic" dx="30" dy="0" stdDeviation="0" flood-color="#123456" flood-opacity="1" color-interpolation-filters="sRGB" />
                </filter>
              </defs>
              <rect id="shadow-target" x="10" y="20" width="10" height="10" fill="red" filter="url(#shadow)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(dropShadowSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);
        var sourcePixel = bitmap.GetPixel(15, 25);
        var shadowPixel = bitmap.GetPixel(45, 25);

        Assert.True(sourcePixel.Red > 200 && sourcePixel.Alpha > 200, $"Expected source pixel to remain red but was {sourcePixel}.");
        Assert.True(
            shadowPixel.Red is > 0x08 and < 0x30 &&
            shadowPixel.Green is > 0x24 and < 0x50 &&
            shadowPixel.Blue is > 0x46 and < 0x70 &&
            shadowPixel.Alpha > 200,
            $"Expected offset shadow pixel to render outside source bounds but was {shadowPixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_FeTileUsesFilterRegionForStandardInput()
    {
        const string tileSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="40">
              <defs>
                <filter id="tile-filter"
                        filterUnits="userSpaceOnUse"
                        x="0" y="0" width="80" height="40"
                        color-interpolation-filters="sRGB">
                  <feTile in="SourceGraphic" x="30" y="0" width="40" height="20" />
                </filter>
              </defs>
              <rect id="tile-target" x="5" y="5" width="10" height="10" fill="red" filter="url(#tile-filter)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(tileSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("tile-target", out var targetNode));
        Assert.NotNull(targetNode);

        var tile = Assert.IsType<TileImageFilter>(targetNode!.Filter!.ImageFilter);
        Assert.Equal(0f, tile.Src.Left, 3);
        Assert.Equal(0f, tile.Src.Top, 3);
        Assert.Equal(80f, tile.Src.Right, 3);
        Assert.Equal(40f, tile.Src.Bottom, 3);
        Assert.Equal(30f, tile.Dst.Left, 3);
        Assert.Equal(0f, tile.Dst.Top, 3);
        Assert.Equal(70f, tile.Dst.Right, 3);
        Assert.Equal(20f, tile.Dst.Bottom, 3);
    }

    [Fact]
    public void RetainedSceneGraph_FeImageLocalFragmentCycleSuppressesImageInput()
    {
        const string cyclicFeImageSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="60" height="60">
              <defs>
                <filter id="self-filter" filterUnits="userSpaceOnUse" x="0" y="0" width="60" height="60">
                  <feImage href="#filtered-target" x="0" y="0" width="60" height="60" result="image" />
                  <feMerge>
                    <feMergeNode in="image" />
                    <feMergeNode in="SourceGraphic" />
                  </feMerge>
                </filter>
              </defs>
              <rect id="filtered-target" x="10" y="10" width="20" height="20" fill="red" filter="url(#self-filter)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(cyclicFeImageSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("filtered-target", out var targetNode));
        Assert.NotNull(targetNode);
        Assert.False(targetNode!.SuppressSubtreeRendering);
        Assert.NotNull(targetNode.Filter);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
    }

    [Fact]
    public void RetainedSceneGraph_FeDropShadowResolvesFloodColorFromPrimitiveContext()
    {
        const string dropShadowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="80">
              <defs>
                <filter id="shadow" x="-20%" y="-20%" width="160%" height="160%" color="blue">
                  <feDropShadow in="SourceGraphic" dx="4" dy="0" stdDeviation="0" flood-color="currentColor" color-interpolation-filters="sRGB" />
                </filter>
              </defs>
              <rect id="shadow-target" x="20" y="20" width="20" height="20" color="red" fill="green" filter="url(#shadow)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(dropShadowSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("shadow-target", out var targetNode));
        Assert.NotNull(targetNode);

        var merge = Assert.IsType<MergeImageFilter>(targetNode!.Filter!.ImageFilter);
        Assert.NotNull(merge.Filters);
        var filters = merge.Filters!;
        var shadowColor = Assert.IsType<ColorFilterImageFilter>(filters[0]);
        var colorMatrix = Assert.IsType<ColorMatrixColorFilter>(shadowColor.ColorFilter);
        Assert.NotNull(colorMatrix.Matrix);
        var matrix = colorMatrix.Matrix!;

        Assert.Equal(0f, matrix[3], 3);
        Assert.Equal(0f, matrix[8], 3);
        Assert.Equal(1f, matrix[13], 3);
    }

    [Fact]
    public void RetainedSceneGraph_FeDropShadowWithMissingExplicitInputDoesNotUseSourceGraphic()
    {
        const string dropShadowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="80">
              <defs>
                <filter id="shadow" x="-20%" y="-20%" width="160%" height="160%">
                  <feDropShadow in="missing-result" dx="3" dy="4" stdDeviation="2" flood-color="#123456" />
                </filter>
              </defs>
              <rect id="shadow-target" x="20" y="20" width="20" height="20" fill="red" filter="url(#shadow)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(dropShadowSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("shadow-target", out var targetNode));

        var colorFilter = Assert.IsType<ColorFilterImageFilter>(targetNode!.Filter!.ImageFilter);
        Assert.IsType<PictureImageFilter>(colorFilter.Input);
    }

    [Fact]
    public void RetainedSceneGraph_InheritsLinkedFilterRegion()
    {
        const string linkedFilterSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs>
                <filter id="alias" href="#base" />
                <filter id="base" filterUnits="userSpaceOnUse" x="5" y="6" width="70" height="60">
                  <feGaussianBlur stdDeviation="1" />
                </filter>
              </defs>
              <rect id="filtered-target" x="20" y="20" width="20" height="20" fill="#3366cc" filter="url(#alias)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(linkedFilterSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("filtered-target", out var targetNode));
        Assert.NotNull(targetNode);
        Assert.NotNull(targetNode!.FilterClip);
        Assert.Equal(5f, targetNode.FilterClip.Value.Left, 3);
        Assert.Equal(6f, targetNode.FilterClip.Value.Top, 3);
        Assert.Equal(75f, targetNode.FilterClip.Value.Right, 3);
        Assert.Equal(66f, targetNode.FilterClip.Value.Bottom, 3);
    }

    [Fact]
    public void RetainedSceneGraph_LinkedFilterCycleKeepsLocalFilterPrimitives()
    {
        const string cyclicFilterSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs>
                <filter id="a" href="#b">
                  <feGaussianBlur stdDeviation="1" />
                </filter>
                <filter id="b" href="#a" />
              </defs>
              <rect id="filtered-target" x="20" y="20" width="20" height="20" fill="#3366cc" filter="url(#a)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(cyclicFilterSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("filtered-target", out var targetNode));
        Assert.NotNull(targetNode);
        Assert.False(targetNode!.SuppressSubtreeRendering);
        Assert.NotNull(targetNode.Filter);
    }

    [Fact]
    public void RetainedSceneGraph_ApplyMutation_UpdatesLinkedFilterResourceDependents()
    {
        const string linkedFilterSvg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 width="24"
                 height="24"
                 viewBox="0 0 24 24">
              <defs>
                <filter id="alias-filter" href="#base-filter" />
                <filter id="base-filter" x="-25%" y="-25%" width="150%" height="150%">
                  <feGaussianBlur id="linked-blur-node" stdDeviation="1" />
                </filter>
              </defs>
              <rect id="filtered-target" x="4" y="4" width="16" height="16" fill="#3366cc" filter="url(#alias-filter)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(linkedFilterSvg);

        var sourceDocument = Assert.IsType<SvgDocument>(svg.RetainedSceneGraph!.SourceDocument);
        var blur = Assert.IsType<SvgGaussianBlur>(sourceDocument.GetElementById("linked-blur-node"));
        blur.StdDeviation = new SvgNumberCollection { 2f };

        var result = svg.ApplyRetainedSceneMutationById("linked-blur-node", new[] { "stdDeviation" });

        Assert.True(result.Succeeded);
        Assert.True(result.CompilationRootCount >= 1);
        Assert.True(result.ResourceCount >= 1);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        using var expectedSvg = new SKSvg();
        expectedSvg.FromSvgDocument((SvgDocument)sourceDocument.DeepCopy());

        Assert.NotNull(retainedPicture);
        Assert.NotNull(expectedSvg.Picture);
        AssertPicturesEqual(expectedSvg, expectedSvg.Picture!, retainedPicture!);
    }

    [Fact]
    public void RetainedSceneGraph_ResolvesRetainedFilterPayloadsForFeImageDocuments()
    {
        using var svg = new SKSvg();
        svg.FromSvg(FilteredImageSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);

        AssertCompilationStrategy(scene!, "filtered-image-target", SvgSceneCompilationStrategy.DirectRetained);
        Assert.True(scene.TryGetNodeById("filtered-image-target", out var filteredNode));
        Assert.NotNull(filteredNode);
        Assert.NotNull(filteredNode!.Filter);
    }

    [Fact]
    public void RetainedSceneGraph_AssignsRetainedResourceKeysForClipMaskAndFilter()
    {
        using var svg = new SKSvg();
        svg.FromSvg(ClippedMaskedAndFilteredSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);

        Assert.True(scene!.TryGetNodeById("clipped-target", out var clippedNode));
        Assert.True(scene.TryGetNodeById("masked-target", out var maskedNode));
        Assert.True(scene.TryGetNodeById("filtered-target", out var filteredNode));

        Assert.False(string.IsNullOrWhiteSpace(clippedNode!.ClipResourceKey));
        Assert.False(string.IsNullOrWhiteSpace(maskedNode!.MaskResourceKey));
        Assert.False(string.IsNullOrWhiteSpace(filteredNode!.FilterResourceKey));

        Assert.Contains(scene.ResourcesById.Values, static resource => resource.Id == "clip-a");
        Assert.Contains(scene.ResourcesById.Values, static resource => resource.Id == "mask-a");
        Assert.Contains(scene.ResourcesById.Values, static resource => resource.Id == "filter-a");
    }

    [Fact]
    public void RetainedSceneGraph_ResolvesRetainedClipPayloadsForDirectNodes()
    {
        using var svg = new SKSvg();
        svg.FromSvg(ClippedMaskedAndFilteredSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);

        AssertCompilationStrategy(scene!, "clipped-target", SvgSceneCompilationStrategy.DirectRetained);
        Assert.True(scene.TryGetNodeById("clipped-target", out var clippedNode));
        Assert.NotNull(clippedNode);
        Assert.NotNull(clippedNode!.ClipPath);
        Assert.NotNull(clippedNode.ClipResourceKey);
    }

    [Fact]
    public void RetainedSceneGraph_AssignsRetainedVisualInteractionAndBackgroundState()
    {
        using var svg = new SKSvg();
        svg.FromSvg(VisualStateSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);

        Assert.True(scene!.TryGetNodeById("interactive-target", out var interactiveNode));
        Assert.True(scene.TryGetNodeById("hidden-target", out var hiddenNode));
        Assert.True(scene.TryGetNodeById("background-root", out var backgroundNode));

        Assert.Equal(SvgPointerEvents.Stroke, interactiveNode!.PointerEvents);
        Assert.Equal("crosshair", interactiveNode.Cursor);
        Assert.True(hiddenNode!.IsDisplayNone);
        Assert.False(hiddenNode.IsVisible);
        Assert.True(backgroundNode!.CreatesBackgroundLayer);
        Assert.Equal(SKRect.Create(1, 2, 30, 10), backgroundNode.BackgroundClip);
    }

    [Fact]
    public void RetainedSceneGraph_AppliesInlineMixBlendModeToImageNode()
    {
        using var svg = new SKSvg();
        svg.FromSvg(InlineMixBlendModeImageSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("blend-image", out var imageNode));

        Assert.NotNull(imageNode!.BlendModePaint);
        Assert.Equal(SKBlendMode.Overlay, imageNode.BlendModePaint!.BlendMode);

        var model = SvgSceneRenderer.Render(scene);
        Assert.NotNull(model);
        Assert.Contains(
            model!.FindCommands<SaveLayerCanvasCommand>(),
            static command => command.Paint?.BlendMode == SKBlendMode.Overlay);
    }

    [Fact]
    public void RetainedSceneGraph_AppliesStylesheetMixBlendModeToShapeNode()
    {
        using var svg = new SKSvg();
        svg.FromSvg(StylesheetMixBlendModeSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("blend-target", out var blendNode));

        Assert.True(blendNode!.Element!.TryGetAttribute("mix-blend-mode", out var mixBlendMode));
        Assert.Equal("multiply", mixBlendMode);
        Assert.NotNull(blendNode.BlendModePaint);
        Assert.Equal(SKBlendMode.Multiply, blendNode.BlendModePaint!.BlendMode);
    }

    [Fact]
    public void RetainedSceneGraph_IgnoresPresentationMixBlendModeAttribute()
    {
        using var svg = new SKSvg();
        svg.FromSvg(PresentationMixBlendModeSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("blend-target", out var blendNode));

        Assert.False(blendNode!.Element!.TryGetAttribute("mix-blend-mode", out _));
        Assert.Null(blendNode.BlendModePaint);
    }

    [Fact]
    public void RetainedSceneGraph_AppliesStylesheetIsolationToContainerNode()
    {
        using var svg = new SKSvg();
        svg.FromSvg(StylesheetIsolationSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("isolated-group", out var isolationNode));
        Assert.True(scene.TryGetNodeById("blend-target", out var blendNode));

        Assert.True(isolationNode!.IsIsolationGroup);
        Assert.NotNull(blendNode!.BlendModePaint);
        Assert.Equal(SKBlendMode.Overlay, blendNode.BlendModePaint!.BlendMode);

        var model = SvgSceneRenderer.Render(scene);
        Assert.NotNull(model);
        Assert.Contains(
            model!.FindCommands<SaveLayerCanvasCommand>(),
            static command => command.Paint is { BlendMode: SKBlendMode.SrcOver });
    }

    [Fact]
    public void RetainedSceneGraph_CompilesUseSwitchAndImageWithDirectRetainedStrategy()
    {
        using var svg = new SKSvg();
        svg.FromSvg(UseSwitchAndImageSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);

        AssertCompilationStrategy(scene!, "use-target", SvgSceneCompilationStrategy.DirectRetained);
        AssertCompilationStrategy(scene, "switch-target", SvgSceneCompilationStrategy.DirectRetained);
        AssertCompilationStrategy(scene, "image-target", SvgSceneCompilationStrategy.DirectRetained);

        Assert.True(scene.TryGetNodeById("image-target", out var imageNode));
        Assert.NotNull(imageNode);
        Assert.Single(imageNode!.Children);
        Assert.Equal(SvgSceneCompilationStrategy.DirectRetained, imageNode.Children[0].CompilationStrategy);
    }

    [Fact]
    public void RetainedSceneGraph_ResolvesSvg2HrefForUseElements()
    {
        const string useHrefSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="40">
              <defs>
                <rect id="template" x="0" y="0" width="20" height="20" fill="red" />
              </defs>
              <use id="use-target" href="#template" x="10" y="8" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(useHrefSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("use-target", out var useNode));
        Assert.NotEmpty(useNode!.Children);

        Assert.NotEmpty(svg.Model!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("template"));
    }

    [Fact]
    public void RetainedSceneGraph_AlignsUseSymbolViewportToSymbolReferencePoint()
    {
        const string symbolRefSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64">
              <defs>
                <symbol id="icon" viewBox="0 0 20 20" refX="10" refY="10">
                  <rect id="symbol-shape" x="0" y="0" width="20" height="20" fill="#ff0000" />
                </symbol>
              </defs>
              <use id="use-target" href="#icon" x="30" y="30" width="20" height="20" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(symbolRefSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);

        using var bitmap = ToBitmap(svg, retainedPicture!);
        Assert.Equal(new SkiaColor(0xff, 0x00, 0x00, 0xff), bitmap.GetPixel(21, 21));
        Assert.Equal(new SkiaColor(0x00, 0x00, 0x00, 0x00), bitmap.GetPixel(45, 45));
    }

    [Fact]
    public void RetainedSceneGraph_UsesSvg2SymbolDimensionsWhenUseDimensionsAreOmitted()
    {
        const string symbolDimensionSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64">
              <defs>
                <symbol id="icon" width="20" height="10">
                  <rect id="symbol-shape" x="0" y="0" width="100%" height="100%" fill="#ff0000" />
                </symbol>
              </defs>
              <use id="use-target" href="#icon" x="5" y="5" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(symbolDimensionSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);

        using var bitmap = ToBitmap(svg, retainedPicture!);
        Assert.Equal(new SkiaColor(0xff, 0x00, 0x00, 0xff), bitmap.GetPixel(24, 14));
        Assert.Equal(new SkiaColor(0x00, 0x00, 0x00, 0x00), bitmap.GetPixel(30, 20));
    }

    [Fact]
    public void RetainedSceneGraph_UsesIntrinsicImageSizeWhenWidthAndHeightAreOmitted()
    {
        var autoImageSvg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
              <image id="image-target"
                     x="5"
                     y="6"
                     href="data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(EmbeddedImageSvg))}" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(autoImageSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("image-target", out var imageNode));
        Assert.NotNull(imageNode);
        Assert.False(imageNode!.GeometryBounds.IsEmpty);
        AssertApproximately(10f, imageNode.GeometryBounds.Width);
        AssertApproximately(10f, imageNode.GeometryBounds.Height);

        using var bitmap = ToBitmap(svg, svg.Picture!);
        Assert.NotEqual(SkiaColors.Transparent, bitmap.GetPixel(6, 7));
    }

    [Fact]
    public void RetainedSceneGraph_AppliesCssGeometryAndCssPathDataDuringCompilation()
    {
        const string cssGeometrySvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="80">
              <style>
                #rect-target { x: 10px; y: 12px; width: 30px; height: 14px; }
                #path-target { d: path('M 20 20 H 40 V 42 H 20 Z'); }
              </style>
              <rect id="rect-target" x="1" y="1" width="2" height="2" fill="red" />
              <path id="path-target" d="M 0 0 H 1 V 1 H 0 Z" fill="blue" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(cssGeometrySvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("rect-target", out var rectNode));
        Assert.True(scene.TryGetNodeById("path-target", out var pathNode));

        AssertApproximately(10f, rectNode!.GeometryBounds.Left);
        AssertApproximately(12f, rectNode.GeometryBounds.Top);
        AssertApproximately(30f, rectNode.GeometryBounds.Width);
        AssertApproximately(14f, rectNode.GeometryBounds.Height);
        AssertApproximately(20f, pathNode!.GeometryBounds.Left);
        AssertApproximately(20f, pathNode.GeometryBounds.Top);
        AssertApproximately(20f, pathNode.GeometryBounds.Width);
        AssertApproximately(22f, pathNode.GeometryBounds.Height);
    }

    [Fact]
    public void RetainedSceneGraph_CompilesMarkerChildrenWithDirectRetainedStrategy()
    {
        using var svg = new SKSvg();
        svg.FromSvg(UseAndMarkerSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);

        var markerNodes = scene!.Traverse()
            .Where(static node => node.Kind == SvgSceneNodeKind.Marker)
            .ToList();

        Assert.NotEmpty(markerNodes);
        Assert.All(markerNodes, static node => Assert.Equal(SvgSceneCompilationStrategy.DirectRetained, node.CompilationStrategy));
    }

    [Fact]
    public void RetainedSceneGraph_AppliesPaintOrderToLocalFillAndStroke()
    {
        const string paintOrderSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="80">
              <rect id="target" x="10" y="10" width="40" height="40" fill="red" stroke="blue" stroke-width="8" paint-order="stroke fill" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(paintOrderSvg);

        var commands = svg.Model!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("target")
            .Where(static command => command.Paint?.Style is SKPaintStyle.Fill or SKPaintStyle.Stroke)
            .ToList();

        Assert.Equal(2, commands.Count);
        Assert.Equal(SKPaintStyle.Stroke, commands[0].Paint!.Style);
        Assert.Equal(SKPaintStyle.Fill, commands[1].Paint!.Style);
    }

    [Fact]
    public void RetainedSceneGraph_AppliesPaintOrderToMarkersAndStroke()
    {
        const string paintOrderMarkerSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="40">
              <defs>
                <marker id="marker" markerWidth="10" markerHeight="10" refX="5" refY="5" orient="auto" markerUnits="userSpaceOnUse">
                  <path id="marker-shape" d="M0,0 L10,5 L0,10 Z" fill="lime" />
                </marker>
              </defs>
              <path id="target" d="M12 20 L78 20" fill="none" stroke="blue" stroke-width="6" marker-start="url(#marker)" paint-order="markers stroke fill" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(paintOrderMarkerSvg);

        var commands = svg.Model!
            .FindCommands<DrawPathCanvasCommand>()
            .Where(static command => command.SourceElementId is "marker-shape" or "target")
            .ToList();

        Assert.Equal(2, commands.Count);
        Assert.Equal("marker-shape", commands[0].SourceElementId);
        Assert.Equal(SKPaintStyle.Fill, commands[0].Paint!.Style);
        Assert.Equal("target", commands[1].SourceElementId);
        Assert.Equal(SKPaintStyle.Stroke, commands[1].Paint!.Style);
    }

    [Fact]
    public void RetainedSceneGraph_NormalizesDashDistancesWithPathLength()
    {
        const string pathLengthDashSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="20">
              <line id="target"
                    x1="10"
                    y1="10"
                    x2="210"
                    y2="10"
                    pathLength="100"
                    fill="none"
                    stroke="black"
                    stroke-width="2"
                    stroke-dasharray="10 5"
                    stroke-dashoffset="20" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(pathLengthDashSvg);

        var command = Assert.Single(svg.Model!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("target")
, static command => command.Paint?.Style == SKPaintStyle.Stroke);
        var dash = Assert.IsType<DashPathEffect>(command.Paint!.PathEffect);

        Assert.NotNull(dash.Intervals);
        Assert.Equal(2, dash.Intervals!.Length);
        AssertApproximately(20f, dash.Intervals[0]);
        AssertApproximately(10f, dash.Intervals[1]);
        AssertApproximately(40f, dash.Phase);
    }

    [Fact]
    public void RetainedSceneGraph_UsesFocalRadiusForRadialGradientShader()
    {
        const string radialGradientSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="80">
              <defs>
                <radialGradient id="grad"
                                gradientUnits="userSpaceOnUse"
                                cx="40"
                                cy="40"
                                r="30"
                                fx="35"
                                fy="35"
                                fr="8">
                  <stop offset="0" stop-color="red" />
                  <stop offset="1" stop-color="blue" />
                </radialGradient>
              </defs>
              <rect id="target" x="0" y="0" width="80" height="80" fill="url(#grad)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(radialGradientSvg);

        var command = Assert.Single(svg.Model!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("target"));
        var shader = Assert.IsType<TwoPointConicalGradientShader>(command.Paint!.Shader);
        AssertApproximately(8f, shader.StartRadius);
        AssertApproximately(30f, shader.EndRadius);
    }

    [Fact]
    public void RetainedSceneGraph_UnsupportedVectorEffectFallsBackToScalingStroke()
    {
        const string vectorEffectSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="40">
              <g transform="scale(2)">
                <line id="target"
                      x1="4"
                      y1="10"
                      x2="30"
                      y2="10"
                      stroke="black"
                      stroke-width="4"
                      vector-effect="non-scaling-size" />
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(vectorEffectSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var node));
        Assert.False(node!.IsStrokeNonScaling);

        var command = Assert.Single(svg.Model!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("target"));
        Assert.False(command.Paint!.IsStrokeNonScaling);
    }

    [Fact]
    public void RetainedSceneGraph_StrokeLinejoinArcsFallsBackToMiter()
    {
        const string lineJoinSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="40">
              <path id="target"
                    d="M 10 30 L 30 10 L 50 30"
                    fill="none"
                    stroke="black"
                    stroke-width="8"
                    stroke-linejoin="arcs" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(lineJoinSvg);

        var command = Assert.Single(svg.Model!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("target"));
        Assert.Equal(SKStrokeJoin.Miter, command.Paint!.StrokeJoin);
    }

    [Fact]
    public void RetainedSceneGraph_AddsMarkersForRectangleVertices()
    {
        const string rectMarkerSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="60">
              <defs>
                <marker id="marker" markerWidth="4" markerHeight="4" refX="2" refY="2" orient="auto" markerUnits="userSpaceOnUse">
                  <circle id="marker-dot" cx="2" cy="2" r="2" fill="red" />
                </marker>
              </defs>
              <rect id="target"
                    x="10"
                    y="10"
                    width="40"
                    height="30"
                    fill="none"
                    stroke="black"
                    marker-start="url(#marker)"
                    marker-mid="url(#marker)"
                    marker-end="url(#marker)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(rectMarkerSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);

        var markerNodes = scene!.Traverse()
            .Where(static node => node.Kind == SvgSceneNodeKind.Marker && node.HitTestTargetElement?.ID == "target")
            .ToList();

        Assert.Equal(5, markerNodes.Count);
    }

    [Theory]
    [InlineData("line", """<line id="target" x1="10" y1="10" x2="50" y2="10" />""", 2, 10f, 10f, 0f, 50f, 10f, 0f)]
    [InlineData("polyline", """<polyline id="target" points="10,10 50,10 50,30" />""", 3, 10f, 10f, 0f, 50f, 30f, 90f)]
    [InlineData("polygon", """<polygon id="target" points="10,10 50,10 50,30 10,30" />""", 5, 10f, 10f, 0f, 10f, 10f, 270f)]
    [InlineData("rect", """<rect id="target" x="10" y="10" width="40" height="20" />""", 5, 10f, 10f, 0f, 10f, 10f, 270f)]
    [InlineData("circle", """<circle id="target" cx="30" cy="30" r="20" />""", 5, 50f, 30f, 90f, 50f, 30f, 90f)]
    [InlineData("ellipse", """<ellipse id="target" cx="40" cy="30" rx="30" ry="15" />""", 5, 70f, 30f, 90f, 70f, 30f, 90f)]
    public void RetainedSceneGraph_AddsMarkersAtShapeVerticesWithAutoOrientation(
        string _,
        string shapeMarkup,
        int expectedMarkerCount,
        float expectedStartX,
        float expectedStartY,
        float expectedStartAngle,
        float expectedEndX,
        float expectedEndY,
        float expectedEndAngle)
    {
        var markerSvg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
              <defs>
                <marker id="marker" markerWidth="4" markerHeight="4" refX="0" refY="0" orient="auto" markerUnits="userSpaceOnUse">
                  <path id="marker-tick" d="M0,0 L4,0" fill="none" stroke="red" />
                </marker>
              </defs>
              {{shapeMarkup.Replace("/>", """
                    fill="none"
                    stroke="black"
                    marker-start="url(#marker)"
                    marker-mid="url(#marker)"
                    marker-end="url(#marker)" />
                """, StringComparison.Ordinal)}}
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(markerSvg);

        var markerNodes = GetTargetMarkerNodes(svg.RetainedSceneGraph, "target");

        Assert.Equal(expectedMarkerCount, markerNodes.Count);
        AssertMarkerTransform(markerNodes[0], expectedStartX, expectedStartY, expectedStartAngle);
        AssertMarkerTransform(markerNodes[markerNodes.Count - 1], expectedEndX, expectedEndY, expectedEndAngle);
    }

    [Fact]
    public void RetainedSceneGraph_ResolvesMarkerContextPaintFromReferencingElement()
    {
        const string contextPaintMarkerSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="20">
              <defs>
                <marker id="marker" markerWidth="10" markerHeight="10" refX="5" refY="5" orient="auto" markerUnits="userSpaceOnUse">
                  <g>
                    <rect id="marker-fill" x="1" y="1" width="8" height="8" fill="context-stroke" />
                    <path id="marker-stroke" d="M1 9 L9 1" fill="none" stroke="context-fill" stroke-width="2" />
                  </g>
                </marker>
              </defs>
              <path id="target" d="M10 10 L30 10" fill="#abcdef" stroke="#123456" stroke-width="1" marker-start="url(#marker)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(contextPaintMarkerSvg);

        var markerFill = Assert.Single(svg.Model!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("marker-fill"));
        Assert.Equal(SKPaintStyle.Fill, markerFill.Paint!.Style);
        Assert.Equal(new SKColor(0x12, 0x34, 0x56, 0xff), markerFill.Paint.Color.GetValueOrDefault());

        var markerStroke = Assert.Single(svg.Model!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("marker-stroke"));
        Assert.Equal(SKPaintStyle.Stroke, markerStroke.Paint!.Style);
        Assert.Equal(new SKColor(0xab, 0xcd, 0xef, 0xff), markerStroke.Paint.Color.GetValueOrDefault());
    }

    [Fact]
    public void RetainedSceneGraph_RendersMarkerContextStrokePixels()
    {
        const string contextPaintMarkerSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="20">
              <defs>
                <marker id="marker" markerWidth="10" markerHeight="10" refX="5" refY="5" orient="auto" markerUnits="userSpaceOnUse">
                  <rect id="marker-pixel" x="2" y="2" width="6" height="6" fill="context-stroke" />
                </marker>
              </defs>
              <path id="target" d="M10 10 L30 10" fill="none" stroke="#123456" stroke-width="1" marker-start="url(#marker)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(contextPaintMarkerSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);

        Assert.Equal(new SkiaColor(0x12, 0x34, 0x56, 0xff), bitmap.GetPixel(10, 7));
    }

    [Fact]
    public void RetainedSceneGraph_ResolvesUseContextPaintFromReferencingElement()
    {
        const string contextPaintUseSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="20">
              <defs>
                <g id="template">
                  <rect id="use-fill" x="0" y="0" width="10" height="10" fill="context-fill" />
                  <rect id="use-stroke" x="12" y="0" width="10" height="10" fill="context-stroke" />
                </g>
              </defs>
              <use id="instance" href="#template" x="4" y="4" color="#abcdef" fill="currentColor" stroke="#123456" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(contextPaintUseSvg);

        var retainedPicture = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedPicture);

        var fillCommands = retainedPicture!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("use-fill")
            .Where(static command => command.Paint?.Style == SKPaintStyle.Fill)
            .ToList();
        Assert.NotEmpty(fillCommands);
        Assert.All(fillCommands, static fill =>
            Assert.Equal(new SKColor(0xab, 0xcd, 0xef, 0xff), fill.Paint!.Color.GetValueOrDefault()));

        var strokeCommands = retainedPicture.FindCommandsBySourceElementId<DrawPathCanvasCommand>("use-stroke")
            .Where(static command => command.Paint?.Style == SKPaintStyle.Fill)
            .ToList();
        Assert.NotEmpty(strokeCommands);
        Assert.All(strokeCommands, static stroke =>
            Assert.Equal(new SKColor(0x12, 0x34, 0x56, 0xff), stroke.Paint!.Color.GetValueOrDefault()));

        using var retainedNativePicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedNativePicture);
        using var bitmap = ToBitmap(svg, retainedNativePicture!);
        Assert.Equal(new SkiaColor(0xab, 0xcd, 0xef, 0xff), bitmap.GetPixel(6, 6));
        Assert.Equal(new SkiaColor(0x12, 0x34, 0x56, 0xff), bitmap.GetPixel(18, 6));
    }

    [Fact]
    public void RetainedSceneGraph_ResolvesUseContextPaintForText()
    {
        const string contextPaintUseSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="32">
              <defs>
                <g id="template">
                  <text id="text-fill" x="0" y="14" font-family="sans-serif" font-size="12" fill="context-fill">A</text>
                  <text id="text-stroke" x="16" y="14" font-family="sans-serif" font-size="12" fill="context-stroke">B</text>
                </g>
              </defs>
              <use id="instance" href="#template" x="4" y="4" fill="#123456" stroke="#abcdef" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(contextPaintUseSvg);

        var retainedPicture = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedPicture);

        var fillCommands = retainedPicture!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("text-fill")
            .Where(static command => command.Paint?.Style == SKPaintStyle.Fill)
            .ToList();
        Assert.NotEmpty(fillCommands);
        Assert.All(fillCommands, static command =>
            Assert.Equal(new SKColor(0x12, 0x34, 0x56, 0xff), command.Paint!.Color.GetValueOrDefault()));

        var strokeCommands = retainedPicture.FindCommandsBySourceElementId<DrawTextCanvasCommand>("text-stroke")
            .Where(static command => command.Paint?.Style == SKPaintStyle.Fill)
            .ToList();
        Assert.NotEmpty(strokeCommands);
        Assert.All(strokeCommands, static command =>
            Assert.Equal(new SKColor(0xab, 0xcd, 0xef, 0xff), command.Paint!.Color.GetValueOrDefault()));
    }

    [Fact]
    public void RetainedSceneGraph_ResolvesNestedUseContextPaintThroughIntermediateUse()
    {
        const string contextPaintUseSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="32" height="12">
              <defs>
                <g id="template">
                  <rect id="nested-context-fill" x="0" y="0" width="10" height="10" fill="context-fill" />
                  <rect id="nested-context-stroke" x="12" y="0" width="10" height="10" fill="context-stroke" />
                </g>
                <use id="inner-instance" href="#template" fill="context-stroke" stroke="context-fill" />
              </defs>
              <use id="outer-instance" href="#inner-instance" x="2" y="1" fill="#112233" stroke="#445566" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(contextPaintUseSvg);

        var retainedPicture = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedPicture);

        var fillCommands = retainedPicture!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("nested-context-fill")
            .Where(static command => command.Paint?.Style == SKPaintStyle.Fill)
            .ToList();
        Assert.NotEmpty(fillCommands);
        Assert.All(fillCommands, static command =>
            Assert.Equal(new SKColor(0x44, 0x55, 0x66, 0xff), command.Paint!.Color.GetValueOrDefault()));

        var strokeCommands = retainedPicture.FindCommandsBySourceElementId<DrawPathCanvasCommand>("nested-context-stroke")
            .Where(static command => command.Paint?.Style == SKPaintStyle.Fill)
            .ToList();
        Assert.NotEmpty(strokeCommands);
        Assert.All(strokeCommands, static command =>
            Assert.Equal(new SKColor(0x11, 0x22, 0x33, 0xff), command.Paint!.Color.GetValueOrDefault()));
    }

    [Fact]
    public void RetainedSceneGraph_ResolvesContextPaintCurrentColorFromConsumerElement()
    {
        const string contextPaintUseSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="12">
              <defs>
                <g id="template" color="#010203">
                  <rect id="context-current-color" x="0" y="0" width="10" height="10" fill="context-fill" />
                </g>
              </defs>
              <g color="#abcdef">
                <use id="instance" href="#template" x="2" y="1" fill="currentColor" />
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(contextPaintUseSvg);

        var retainedPicture = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedPicture);

        var command = Assert.Single(retainedPicture!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("context-current-color")
, static command => command.Paint?.Style == SKPaintStyle.Fill);
        Assert.Equal(new SKColor(0x01, 0x02, 0x03, 0xff), command.Paint!.Color.GetValueOrDefault());
    }

    [Fact]
    public void RetainedSceneGraph_ResolvesContextPaintFromInheritedUseStroke()
    {
        const string contextPaintUseSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="12">
              <defs>
                <g id="template">
                  <rect id="inherited-context-stroke" x="0" y="0" width="10" height="10" fill="context-stroke" />
                </g>
              </defs>
              <g stroke="#123456">
                <use id="instance" href="#template" x="2" y="1" fill="#abcdef" />
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(contextPaintUseSvg);

        var retainedPicture = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedPicture);

        var command = Assert.Single(retainedPicture!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("inherited-context-stroke")
, static command => command.Paint?.Style == SKPaintStyle.Fill);
        Assert.Equal(new SKColor(0x12, 0x34, 0x56, 0xff), command.Paint!.Color.GetValueOrDefault());
    }

    [Fact]
    public void RetainedSceneGraph_ResolvesContextPaintThroughUrlFallbackChain()
    {
        const string fallbackSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="12">
              <defs>
                <linearGradient id="empty" />
                <g id="template">
                  <rect id="fallback-target" x="0" y="0" width="10" height="10" fill="url(#missing) url(#empty) context-stroke" />
                </g>
              </defs>
              <use id="instance" href="#template" x="2" y="1" fill="#abcdef" stroke="#123456" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(fallbackSvg);

        var retainedPicture = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedPicture);

        var commands = retainedPicture!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("fallback-target")
            .Where(static command => command.Paint?.Style == SKPaintStyle.Fill)
            .ToList();
        Assert.NotEmpty(commands);
        Assert.All(commands, static command =>
            Assert.Equal(new SKColor(0x12, 0x34, 0x56, 0xff), command.Paint!.Color.GetValueOrDefault()));

        using var retainedNativePicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedNativePicture);
        using var bitmap = ToBitmap(svg, retainedNativePicture!);
        Assert.Equal(new SkiaColor(0x12, 0x34, 0x56, 0xff), bitmap.GetPixel(4, 3));
    }

    [Fact]
    public void RetainedSceneGraph_ContextPaintWithoutContextDoesNotCrashOrPaint()
    {
        const string contextPaintSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" x="2" y="2" width="16" height="16" fill="context-stroke" stroke="none" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(contextPaintSvg);

        Assert.NotNull(svg.Picture);
        Assert.Empty(svg.Model!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("target"));
    }

    [Fact]
    public void RetainedSceneGraph_CompilesMarkersFromHiddenMarkerDefinitions()
    {
        var path = Path.Combine("..", "..", "..", "..", "..", "externals", "W3C_SVG_11_TestSuite", "W3C_SVG_11_TestSuite", "svg", "painting-marker-07-f.svg");

        using var svg = new SKSvg();
        svg.Settings.StandaloneViewport = new SkiaSharp.SKRect(0f, 0f, 480f, 360f);
        svg.Load(path);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);

        var markerNodes = scene!.Traverse()
            .Where(static node => node.Kind == SvgSceneNodeKind.Marker)
            .ToList();

        Assert.Equal(2, markerNodes.Count);
        Assert.All(markerNodes, static node =>
        {
            Assert.True(node.IsRenderable);
            Assert.False(node.IsDisplayNone);
            Assert.True(node.IsVisible);
        });
    }

    [Fact]
    public void RetainedSceneGraph_CompilesHardDocumentsWithoutDrawableBridge()
    {
        using var maskedSvg = new SKSvg();
        maskedSvg.FromSvg(MaskedSvg);

        using var filteredSvg = new SKSvg();
        filteredSvg.FromSvg(FilteredImageSvg);

        using var richTextSvg = new SKSvg();
        richTextSvg.FromSvg(RichTextSvg);

        using var complexSvg = new SKSvg();
        complexSvg.FromSvg(MaskedRichTextSvg);

        AssertNoDrawableBridge(maskedSvg.RetainedSceneGraph);
        AssertNoDrawableBridge(filteredSvg.RetainedSceneGraph);
        AssertNoDrawableBridge(richTextSvg.RetainedSceneGraph);
        AssertNoDrawableBridge(complexSvg.RetainedSceneGraph);
    }

    [Fact]
    public void RetainedSceneGraph_CompilesNonRenderingContainersWithoutDrawableBridge()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NonRenderingContainerSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        AssertNoDrawableBridge(scene);

        Assert.True(scene!.TryGetNodeById("sym", out var symbolNode));
        Assert.NotNull(symbolNode);
        Assert.Equal(SvgSceneNodeKind.Fragment, symbolNode!.Kind);
        Assert.False(symbolNode.IsRenderable);

        Assert.True(scene.TryGetNodeById("fo", out var foreignObjectNode));
        Assert.NotNull(foreignObjectNode);
        Assert.Equal(SvgSceneNodeKind.Container, foreignObjectNode!.Kind);
        Assert.False(foreignObjectNode.IsRenderable);
    }

    [Theory]
    [InlineData("animate-elem-32-t.svg")]
    [InlineData("animate-elem-88-t.svg")]
    [InlineData("paths-dom-02-f.svg")]
    [InlineData("shapes-circle-02-t.svg")]
    [InlineData("shapes-ellipse-02-t.svg")]
    [InlineData("shapes-intro-01-t.svg")]
    [InlineData("shapes-rect-02-t.svg")]
    [InlineData("struct-dom-17-f.svg")]
    [InlineData("struct-dom-19-f.svg")]
    public void RetainedSceneGraph_CompilesSelectedW3CDocumentsWithoutDrawableBridge(string fileName)
    {
        var svgPath = GetW3CTestSvgPath(fileName);
        if (!File.Exists(svgPath))
        {
            return;
        }

        using var svg = new SKSvg();
        using var _ = svg.Load(svgPath);

        AssertNoDrawableBridge(svg.RetainedSceneGraph);
    }

    [Fact]
    public void RetainedSceneGraph_CompilesW3CEmbeddedImageCycleDocumentWithoutRecursing()
    {
        var svgPath = GetW3CTestSvgPath("struct-image-12-b.svg");
        if (!File.Exists(svgPath))
        {
            return;
        }

        using var svg = new SKSvg();
        using var _ = svg.Load(svgPath);
        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(svg.RetainedSceneGraph);
        Assert.NotNull(svg.Picture);
        Assert.NotNull(retainedPicture);
        AssertNoDrawableBridge(svg.RetainedSceneGraph);
    }

    [Fact]
    public void RetainedSceneGraph_CompilesResvgSelfRecursiveMaskDocumentWithoutRecursing()
    {
        var svgPath = GetResvgSvgPath("e-mask-023.svg");
        if (!File.Exists(svgPath))
        {
            return;
        }

        using var svg = new SKSvg();
        using var _ = svg.Load(svgPath);
        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.NotNull(svg.Picture);
        Assert.NotNull(retainedPicture);
        Assert.True(scene!.TryGetNodeById("rect2", out var maskedNode));
        Assert.NotNull(maskedNode);
        Assert.NotNull(maskedNode!.MaskNode);

        var recursiveMaskContentNode = Assert.Single(maskedNode.MaskNode!.Children, static child => child.ElementId == "rect1");
        Assert.Null(recursiveMaskContentNode.MaskNode);
        AssertNoDrawableBridge(scene);
    }

    [Fact]
    public void CreateRetainedSceneGraphPicture_MatchesCurrentPicture_ForInheritedGroupMarkers()
    {
        using var svg = new SKSvg();
        svg.FromSvg(GroupMarkerSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(svg.Picture);
        Assert.NotNull(retainedPicture);
        AssertPicturesEqual(svg, svg.Picture!, retainedPicture!);
    }

    [Fact]
    public void CreateRetainedSceneGraphPicture_MatchesCurrentPicture_ForFeImageFilterDocument()
    {
        using var svg = new SKSvg();
        svg.FromSvg(FilteredImageSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(svg.Picture);
        Assert.NotNull(retainedPicture);
        AssertPicturesEqual(svg, svg.Picture!, retainedPicture!);
    }

    [Fact]
    public void RetainedSceneGraph_RendersBackgroundImageFromSameFilteredContainer()
    {
        using var svg = new SKSvg();
        svg.FromSvg(EnableBackgroundOnSameContainerSvg);

        Assert.NotNull(svg.Picture);

        using var bitmap = ToBitmap(svg, svg.Picture!);

        var sourcePixel = bitmap.GetPixel(30, 100);
        var backgroundPixel = bitmap.GetPixel(130, 100);

        Assert.True(
            sourcePixel.Alpha == 0,
            $"Expected source pixel to be transparent but was {sourcePixel} on {bitmap.Width}x{bitmap.Height} bitmap.");
        Assert.True(
            backgroundPixel.Alpha == 0,
            $"Expected background pixel to be transparent but was {backgroundPixel} on {bitmap.Width}x{bitmap.Height} bitmap.");
        Assert.True(
            sourcePixel == backgroundPixel,
            $"Expected source and background pixels to match transparent output but source was {sourcePixel} and background was {backgroundPixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_SuppressesVisualsWithInvalidFilterOutput()
    {
        using var svg = new SKSvg();
        svg.FromSvg(InvalidFilterSvg);

        Assert.NotNull(svg.Picture);

        using var bitmap = ToBitmap(svg, svg.Picture!);

        var framePixel = bitmap.GetPixel(40, 1);
        var filteredPixel = bitmap.GetPixel(40, 40);

        Assert.True(framePixel.Alpha > 0, $"Expected frame pixel to remain visible but was {framePixel}.");
        Assert.Equal(0, filteredPixel.Alpha);
    }

    [Fact]
    public void RetainedSceneGraph_HandlesConditionalAttributesWithChromeCompatibleBehavior()
    {
        using var svg = new SKSvg();
        svg.FromSvg(ConditionalReferenceSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("feature-group", out var featureGroup));
        Assert.True(scene.TryGetNodeById("extension-group", out var extensionGroup));
        Assert.True(scene.TryGetNodeById("language-group", out var languageGroup));
        Assert.False(featureGroup!.SuppressSubtreeRendering);
        Assert.True(extensionGroup!.SuppressSubtreeRendering);
        Assert.True(languageGroup!.SuppressSubtreeRendering);

        Assert.NotNull(svg.Picture);

        using var bitmap = ToBitmap(svg, svg.Picture!);

        var directFeaturePixel = bitmap.GetPixel(6, 6);
        Assert.Equal(0, bitmap.GetPixel(6, 22).Alpha);
        Assert.Equal(0, bitmap.GetPixel(6, 38).Alpha);

        var featurePixel = bitmap.GetPixel(22, 6);
        var extensionPixel = bitmap.GetPixel(22, 22);
        var languagePixel = bitmap.GetPixel(22, 38);

        Assert.True(directFeaturePixel.Red > 200 && directFeaturePixel.Alpha > 0, $"Expected requiredFeatures rect to render but was {directFeaturePixel}.");
        Assert.True(featurePixel.Red > 200 && featurePixel.Alpha > 0, $"Expected referenced feature rect to render but was {featurePixel}.");
        Assert.True(extensionPixel.Green > 200 && extensionPixel.Alpha > 0, $"Expected referenced extension rect to render but was {extensionPixel}.");
        Assert.True(languagePixel.Blue > 200 && languagePixel.Alpha > 0, $"Expected referenced language rect to render but was {languagePixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_AllowsVisibleChildInsideHiddenGroup()
    {
        using var svg = new SKSvg();
        svg.FromSvg(VisibilityOverrideSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("visible-group", out var visibleGroup));
        Assert.False(visibleGroup!.SuppressSubtreeRendering);

        Assert.NotNull(svg.Picture);

        using var bitmap = ToBitmap(svg, svg.Picture!);

        var topLeftPixel = bitmap.GetPixel(110, 110);
        var bottomRightPixel = bitmap.GetPixel(220, 220);

        Assert.True(topLeftPixel.Green > 200 && topLeftPixel.Alpha > 0, $"Expected top-left rect to stay green but was {topLeftPixel}.");
        Assert.True(bottomRightPixel.Green > 200 && bottomRightPixel.Alpha > 0, $"Expected visible child override to render green but was {bottomRightPixel}.");
        Assert.True(bottomRightPixel.Red < 50, $"Expected hidden parent red rect to be fully covered, but was {bottomRightPixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_IndexesSharedUseTargetsAsMultipleNodes()
    {
        using var svg = new SKSvg();
        svg.FromSvg(SharedUseSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);

        var templateNodes = scene!.Traverse()
            .Where(static node => string.Equals(node.ElementId, "template", StringComparison.Ordinal))
            .ToList();

        Assert.True(templateNodes.Count >= 2);
        var addressKey = Assert.Single(templateNodes.Select(static node => node.ElementAddressKey).Distinct());
        Assert.NotNull(addressKey);
        Assert.True(scene.TryGetNodes(addressKey!, out var indexedNodes));
        Assert.Equal(templateNodes.Count, indexedNodes.Count);
    }

    [Fact]
    public void RetainedSceneGraph_IndexesNestedSharedUseDescendantsAsMultipleNodes()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NestedSharedUseSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        var sourceDocument = Assert.IsType<SvgDocument>(scene!.SourceDocument);
        var leaf = Assert.IsType<SvgRectangle>(sourceDocument.GetElementById("leaf"));
        Assert.True(svg.TryGetRetainedSceneNodes(leaf, out var indexedNodes));
        Assert.Equal(2, indexedNodes.Count);
        Assert.All(indexedNodes, static node => Assert.False(string.IsNullOrWhiteSpace(node.ElementAddressKey)));
        Assert.Contains('/', Assert.IsType<string>(indexedNodes[0].ElementAddressKey));
        Assert.All(indexedNodes, static node => Assert.Equal("leaf", node.ElementId));
    }

    [Fact]
    public void CreateRetainedSceneGraphPicture_MatchesCurrentPicture_ForSimpleDocument()
    {
        using var svg = new SKSvg();
        svg.FromSvg(SimpleSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(svg.Picture);
        Assert.NotNull(retainedPicture);
        AssertPicturesEqual(svg, svg.Picture!, retainedPicture!);
    }

    [Fact]
    public void CreateRetainedSceneGraphPicture_MatchesCurrentPicture_ForPrimitiveShapesDocument()
    {
        using var svg = new SKSvg();
        svg.FromSvg(PrimitiveShapesSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(svg.Picture);
        Assert.NotNull(retainedPicture);
        AssertPicturesEqual(svg, svg.Picture!, retainedPicture!);
    }

    [Fact]
    public void CreateRetainedSceneGraphPicture_MatchesCurrentPicture_ForRootOpacityDocument()
    {
        using var svg = new SKSvg();
        svg.FromSvg(RootOpacitySvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(svg.Picture);
        Assert.NotNull(retainedPicture);
        AssertPicturesEqual(svg, svg.Picture!, retainedPicture!);
    }

    [Fact]
    public void CreateRetainedSceneGraphPicture_MatchesCurrentPicture_ForFilteredGroupDocument()
    {
        using var svg = new SKSvg();
        svg.FromSvg(FilteredGroupSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(svg.Picture);
        Assert.NotNull(retainedPicture);
        AssertPicturesEqual(svg, svg.Picture!, retainedPicture!);
    }

    [Fact]
    public void RetainedSceneGraph_AppliesBlendFilterToFilteredGroupsLikeEquivalentSingles()
    {
        using var svg = new SKSvg();
        svg.FromSvg(FilteredGroupComparisonSvg);

        Assert.NotNull(svg.Picture);

        using var bitmap = ToBitmap(svg, svg.Picture!);

        var singleBlue = bitmap.GetPixel(36, 9);
        var singleYellow = bitmap.GetPixel(36, 27);
        var groupBlue = bitmap.GetPixel(36, 45);
        var groupYellow = bitmap.GetPixel(36, 63);

        Assert.NotEqual(new SkiaColor(0, 0, 255, 255), groupBlue);
        Assert.NotEqual(new SkiaColor(255, 255, 0, 255), groupYellow);
        Assert.True(
            groupBlue.Green > singleBlue.Green &&
            groupBlue.Blue > singleBlue.Blue &&
            groupBlue.Red < singleBlue.Red,
            $"Expected grouped multiply row to shift away from the unfiltered blue source, but single={singleBlue} group={groupBlue}.");
        Assert.True(
            groupYellow.Green > singleYellow.Green &&
            groupYellow.Red < 200 &&
            groupYellow.Blue == 0,
            $"Expected grouped multiply row to shift away from the unfiltered yellow source, but single={singleYellow} group={groupYellow}.");
    }

    [Fact]
    public void RetainedSceneGraph_RendersFilteredGroupWithTransformedChild()
    {
        using var svg = new SKSvg();
        svg.FromSvg(FilteredTransformedChildGroupSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("a", out var groupNode));
        Assert.NotNull(groupNode);
        Assert.Equal(125f, groupNode!.GeometryBounds.Left, 3);
        Assert.Equal(125f, groupNode.GeometryBounds.Top, 3);
        Assert.Equal(375f, groupNode.GeometryBounds.Right, 3);
        Assert.Equal(375f, groupNode.GeometryBounds.Bottom, 3);
        Assert.NotNull(groupNode.FilterClip);
        Assert.Equal(100f, groupNode.FilterClip.Value.Left, 3);
        Assert.Equal(100f, groupNode.FilterClip.Value.Top, 3);
        Assert.Equal(400f, groupNode.FilterClip.Value.Right, 3);
        Assert.Equal(400f, groupNode.FilterClip.Value.Bottom, 3);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);

        var centerPixel = bitmap.GetPixel(250, 250);
        Assert.True(
            centerPixel.Alpha > 200 && centerPixel.Red < 20 && centerPixel.Green < 20 && centerPixel.Blue < 20,
            $"Expected transformed child inside filtered group to render at the center but was {centerPixel}.");
    }

    [Fact]
    public void CreateRetainedSceneGraphPicture_MatchesCurrentPicture_ForUseAndMarkerDocument()
    {
        using var svg = new SKSvg();
        svg.FromSvg(UseAndMarkerSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(svg.Picture);
        Assert.NotNull(retainedPicture);
        AssertPicturesEqual(svg, svg.Picture!, retainedPicture!);
    }

    [Fact]
    public void CreateRetainedSceneGraphPicture_MatchesCurrentPicture_ForUseSwitchAndImageDocument()
    {
        using var svg = new SKSvg();
        svg.FromSvg(UseSwitchAndImageSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(svg.Picture);
        Assert.NotNull(retainedPicture);
        AssertPicturesEqual(svg, svg.Picture!, retainedPicture!);
    }

    [Fact]
    public void RetainedSceneGraph_RebuildsAfterAnimationFrameUpdate()
    {
        using var svg = new SKSvg();
        svg.FromSvg(AnimatedSvg);

        var initialScene = svg.RetainedSceneGraph;
        using var initialRetainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(initialScene);
        Assert.NotNull(initialRetainedPicture);

        svg.SetAnimationTime(TimeSpan.FromSeconds(2));

        var updatedScene = svg.RetainedSceneGraph;
        using var updatedRetainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(updatedScene);
        Assert.NotNull(updatedRetainedPicture);
        Assert.Same(initialScene, updatedScene);
        AssertPicturesEqual(svg, svg.Picture!, updatedRetainedPicture!);

        using var initialBitmap = ToBitmap(svg, initialRetainedPicture!);
        using var updatedBitmap = ToBitmap(svg, updatedRetainedPicture!);
        Assert.NotEqual(initialBitmap.GetPixel(2, 2), updatedBitmap.GetPixel(2, 2));
        Assert.NotEqual(initialBitmap.GetPixel(12, 2), updatedBitmap.GetPixel(12, 2));
    }

    [Fact]
    public void RetainedSceneGraph_ApplyMutation_UpdatesUseDependents()
    {
        using var svg = new SKSvg();
        svg.FromSvg(SharedUseSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        var sourceDocument = scene!.SourceDocument;
        Assert.NotNull(sourceDocument);
        var template = Assert.IsType<SvgRectangle>(sourceDocument.GetElementById("template"));
        template.Fill = new SvgColourServer(Color.Purple);

        var result = scene.ApplyMutation(template, new[] { "fill" });

        Assert.True(result.Succeeded);
        Assert.True(result.CompilationRootCount >= 2);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        using var expectedSvg = new SKSvg();
        expectedSvg.FromSvgDocument((SvgDocument)sourceDocument.DeepCopy());

        Assert.NotNull(retainedPicture);
        Assert.NotNull(expectedSvg.Picture);
        AssertPicturesEqual(expectedSvg, expectedSvg.Picture!, retainedPicture!);
    }

    [Fact]
    public void RetainedSceneGraph_ApplyMutation_UpdatesNestedUseDescendantDependents()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NestedSharedUseSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        var sourceDocument = Assert.IsType<SvgDocument>(scene!.SourceDocument);
        var leaf = Assert.IsType<SvgRectangle>(sourceDocument.GetElementById("leaf"));
        leaf.Fill = new SvgColourServer(Color.Purple);

        var result = scene.ApplyMutation(leaf, new[] { "fill" });

        Assert.True(result.Succeeded);
        Assert.True(result.CompilationRootCount >= 2);
        Assert.True(svg.TryGetRetainedSceneNodes(leaf, out var indexedNodes));
        Assert.Equal(2, indexedNodes.Count);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        using var expectedSvg = new SKSvg();
        expectedSvg.FromSvgDocument((SvgDocument)sourceDocument.DeepCopy());

        Assert.NotNull(retainedPicture);
        Assert.NotNull(expectedSvg.Picture);
        AssertPicturesEqual(expectedSvg, expectedSvg.Picture!, retainedPicture!);
    }

    [Fact]
    public void RetainedSceneGraph_ApplyMutation_UpdatesAnimationChildDependents()
    {
        using var svg = new SKSvg();
        svg.FromSvg(
            "<svg width=\"40\" height=\"20\">" +
            "  <rect id=\"target\" x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"red\">" +
            "    <animate id=\"move-anim\" attributeName=\"x\" from=\"0\" to=\"8\" dur=\"1s\" fill=\"freeze\" />" +
            "  </rect>" +
            "</svg>");

        var sourceDocument = Assert.IsType<SvgDocument>(svg.RetainedSceneGraph!.SourceDocument);
        var animation = Assert.IsType<SvgAnimate>(sourceDocument.GetElementById("move-anim"));
        animation.To = "16";

        var result = svg.ApplyRetainedSceneMutationById("move-anim", new[] { "to" });

        Assert.True(result.Succeeded);
        Assert.True(result.CompilationRootCount >= 1);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        using var expectedSvg = new SKSvg();
        expectedSvg.FromSvgDocument((SvgDocument)sourceDocument.DeepCopy());

        Assert.NotNull(retainedPicture);
        Assert.NotNull(expectedSvg.Picture);
        AssertPicturesEqual(expectedSvg, expectedSvg.Picture!, retainedPicture!);
    }

    [Fact]
    public void CreateRetainedSceneGraphPicture_MatchesCurrentPicture_ForPatternDocument()
    {
        using var svg = new SKSvg();
        svg.FromSvg(PatternSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(svg.Picture);
        Assert.NotNull(retainedPicture);
        AssertPicturesEqual(svg, svg.Picture!, retainedPicture!);
    }

    [Fact]
    public void CreateRetainedSceneGraphPicture_MatchesCurrentPicture_ForRichTextDocument()
    {
        using var svg = new SKSvg();
        svg.FromSvg(RichTextSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(svg.Picture);
        Assert.NotNull(retainedPicture);
        AssertPicturesEqual(svg, svg.Picture!, retainedPicture!);
    }

    [Fact]
    public void CreateRetainedSceneGraphPicture_MatchesCurrentPicture_ForLongTextPathDocument()
    {
        const string longTextPathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="640"
                 height="120"
                 viewBox="0 0 640 120">
              <defs>
                <path id="wave-path"
                      d="M16,62
                         C40,38 64,86 88,62
                         C112,38 136,86 160,62
                         C184,38 208,86 232,62
                         C256,38 280,86 304,62
                         C328,38 352,86 376,62
                         C400,38 424,86 448,62
                         C472,38 496,86 520,62
                         C544,38 568,86 592,62" />
              </defs>
              <text fill="#1f2937"
                    font-family="Noto Sans"
                    font-size="18"
                    letter-spacing="0.75"
                    textLength="520">
                <textPath xlink:href="#wave-path" startOffset="4%">Retained scene graph text path parity benchmark</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(longTextPathSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(svg.Picture);
        Assert.NotNull(retainedPicture);
        AssertPicturesEqual(svg, svg.Picture!, retainedPicture!);
    }

    [Fact]
    public void RetainedSceneGraph_ApplyMutation_UpdatesGradientResourceDependents()
    {
        using var svg = new SKSvg();
        svg.FromSvg(GradientSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        var sourceDocument = scene!.SourceDocument;
        Assert.NotNull(sourceDocument);
        Assert.True(scene.TryGetResourceById("grad", out var gradientResource));
        Assert.NotNull(gradientResource);

        var stopA = Assert.IsType<SvgGradientStop>(sourceDocument.GetElementById("stop-a"));
        stopA.StopColor = new SvgColourServer(Color.Blue);

        var result = scene.ApplyMutation(stopA, new[] { "stop-color" });

        Assert.True(result.Succeeded);
        Assert.True(result.CompilationRootCount >= 1);
        Assert.True(result.ResourceCount >= 1);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        using var expectedSvg = new SKSvg();
        expectedSvg.FromSvgDocument((SvgDocument)sourceDocument.DeepCopy());

        Assert.NotNull(retainedPicture);
        Assert.NotNull(expectedSvg.Picture);
        AssertPicturesEqual(expectedSvg, expectedSvg.Picture!, retainedPicture!);
    }

    [Fact]
    public void RetainedSceneGraph_ApplyMutation_UpdatesPatternResourceDependents()
    {
        using var svg = new SKSvg();
        svg.FromSvg(PatternSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        var sourceDocument = scene!.SourceDocument;
        Assert.NotNull(sourceDocument);
        Assert.True(scene.TryGetResourceById("pat", out var patternResource));
        Assert.NotNull(patternResource);

        var patternDot = Assert.IsType<SvgCircle>(sourceDocument.GetElementById("pattern-dot"));
        patternDot.Fill = new SvgColourServer(Color.Crimson);

        var result = scene.ApplyMutation(patternDot, new[] { "fill" });

        Assert.True(result.Succeeded);
        Assert.True(result.CompilationRootCount >= 1);
        Assert.True(result.ResourceCount >= 1);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        using var expectedSvg = new SKSvg();
        expectedSvg.FromSvgDocument((SvgDocument)sourceDocument.DeepCopy());

        Assert.NotNull(retainedPicture);
        Assert.NotNull(expectedSvg.Picture);
        AssertPicturesEqual(expectedSvg, expectedSvg.Picture!, retainedPicture!);
    }

    [Fact]
    public void RetainedSceneGraph_ToolingApis_SupportLookupNodeRenderingAndMutation()
    {
        using var svg = new SKSvg();
        svg.FromSvg(GradientSvg);

        Assert.True(svg.TryGetRetainedSceneNodeById("gradient-target", out var targetNode));
        Assert.NotNull(targetNode);
        Assert.False(string.IsNullOrWhiteSpace(targetNode!.ElementAddressKey));
        Assert.True(svg.TryGetRetainedSceneNode(targetNode.ElementAddressKey!, out var targetNodeByAddress));
        Assert.Same(targetNode, targetNodeByAddress);

        Assert.True(svg.TryGetRetainedSceneResourceById("grad", out var gradientResource));
        Assert.NotNull(gradientResource);
        Assert.False(string.IsNullOrWhiteSpace(gradientResource!.AddressKey));
        Assert.True(svg.TryGetRetainedSceneResource(gradientResource.AddressKey!, out var gradientResourceByAddress));
        Assert.Same(gradientResource, gradientResourceByAddress);

        using var targetPicture = svg.CreateRetainedSceneNodePicture(targetNode);
        Assert.NotNull(targetPicture);

        var sourceDocument = Assert.IsType<SvgDocument>(svg.RetainedSceneGraph!.SourceDocument);
        var stopA = Assert.IsType<SvgGradientStop>(sourceDocument.GetElementById("stop-a"));
        stopA.StopColor = new SvgColourServer(Color.BlueViolet);

        var result = svg.ApplyRetainedSceneMutationById("stop-a", new[] { "stop-color" });

        Assert.True(result.Succeeded);
        Assert.True(result.CompilationRootCount >= 1);
        Assert.True(result.ResourceCount >= 1);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        using var expectedSvg = new SKSvg();
        expectedSvg.FromSvgDocument((SvgDocument)sourceDocument.DeepCopy());

        Assert.NotNull(retainedPicture);
        Assert.NotNull(expectedSvg.Picture);
        AssertPicturesEqual(expectedSvg, expectedSvg.Picture!, retainedPicture!);
    }

    [Fact]
    public void RetainedSceneGraph_ApplyMutation_UpdatesMaskResourceDependents()
    {
        using var svg = new SKSvg();
        svg.FromSvg(MaskedSvg);

        var sourceDocument = Assert.IsType<SvgDocument>(svg.RetainedSceneGraph!.SourceDocument);
        var maskHole = Assert.IsType<SvgCircle>(sourceDocument.GetElementById("mask-hole"));
        maskHole.Fill = new SvgColourServer(Color.White);

        var result = svg.ApplyRetainedSceneMutationById("mask-hole", new[] { "fill" });

        Assert.True(result.Succeeded);
        Assert.True(result.CompilationRootCount >= 1);
        Assert.True(result.ResourceCount >= 1);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        using var expectedSvg = new SKSvg();
        expectedSvg.FromSvgDocument((SvgDocument)sourceDocument.DeepCopy());

        Assert.NotNull(retainedPicture);
        Assert.NotNull(expectedSvg.Picture);
        AssertPicturesEqual(expectedSvg, expectedSvg.Picture!, retainedPicture!);
    }

    [Fact]
    public void RetainedSceneGraph_ApplyMutation_UpdatesFilterResourceDependents()
    {
        using var svg = new SKSvg();
        svg.FromSvg(FilteredSvg);

        var sourceDocument = Assert.IsType<SvgDocument>(svg.RetainedSceneGraph!.SourceDocument);
        var blur = Assert.IsType<SvgGaussianBlur>(sourceDocument.GetElementById("blur-node"));
        blur.StdDeviation = new SvgNumberCollection { 2f };

        var result = svg.ApplyRetainedSceneMutationById("blur-node", new[] { "stdDeviation" });

        Assert.True(result.Succeeded);
        Assert.True(result.CompilationRootCount >= 1);
        Assert.True(result.ResourceCount >= 1);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        using var expectedSvg = new SKSvg();
        expectedSvg.FromSvgDocument((SvgDocument)sourceDocument.DeepCopy());

        Assert.NotNull(retainedPicture);
        Assert.NotNull(expectedSvg.Picture);
        AssertPicturesEqual(expectedSvg, expectedSvg.Picture!, retainedPicture!);
    }

    [Fact]
    public void RetainedSceneGraph_ApplyMutation_UpdatesLinkedFilterDependents()
    {
        using var svg = new SKSvg();
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="40">
              <defs>
                <filter id="base-filter">
                  <feGaussianBlur id="linked-blur" stdDeviation="0" />
                </filter>
                <filter id="child-filter" href="#base-filter" />
              </defs>
              <rect id="filtered-target" x="10" y="8" width="24" height="12" fill="red" filter="url(#child-filter)" />
            </svg>
            """);

        var sourceDocument = Assert.IsType<SvgDocument>(svg.RetainedSceneGraph!.SourceDocument);
        var blur = Assert.IsType<SvgGaussianBlur>(sourceDocument.GetElementById("linked-blur"));
        blur.StdDeviation = new SvgNumberCollection { 2f };

        var result = svg.ApplyRetainedSceneMutationById("linked-blur", new[] { "stdDeviation" });

        Assert.True(result.Succeeded);
        Assert.True(result.CompilationRootCount >= 1);
        Assert.True(result.ResourceCount >= 1);
    }

    [Fact]
    public void TryApplyRetainedSceneMutationByIdAndRender_RefreshesCurrentPicture()
    {
        using var svg = new SKSvg();
        svg.FromSvg(
            "<svg width=\"80\" height=\"40\">" +
            "  <rect id=\"rect-a\" x=\"10\" y=\"8\" width=\"24\" height=\"12\" fill=\"red\" />" +
            "</svg>");

        var sourceDocument = Assert.IsType<SvgDocument>(svg.RetainedSceneGraph!.SourceDocument);
        var rect = Assert.IsType<SvgRectangle>(sourceDocument.GetElementById("rect-a"));
        rect.Fill = new SvgColourServer(Color.BlueViolet);

        var updated = svg.TryApplyRetainedSceneMutationByIdAndRender("rect-a", new[] { "fill" }, out var result);

        Assert.True(updated);
        Assert.NotNull(result);
        Assert.True(result!.Succeeded);
        Assert.Equal(1, result.CompilationRootCount);
        Assert.NotNull(svg.Picture);

        using var expectedSvg = new SKSvg();
        expectedSvg.FromSvgDocument((SvgDocument)sourceDocument.DeepCopy());

        Assert.NotNull(expectedSvg.Picture);
        AssertPicturesEqual(expectedSvg, expectedSvg.Picture!, svg.Picture!);
    }

    [Fact]
    public void CreateRetainedSceneGraphPicture_MatchesCurrentPicture_ForMaskedRichTextDocument()
    {
        using var svg = new SKSvg();
        svg.FromSvg(MaskedRichTextSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(svg.Picture);
        Assert.NotNull(retainedPicture);
        AssertPicturesEqual(svg, svg.Picture!, retainedPicture!);
    }

    [Fact]
    public void RetainedSceneGraph_ToolingApis_SupportElementLookupAndRetainedNodeHitTesting()
    {
        using var svg = new SKSvg();
        svg.FromSvg(
            "<svg width=\"80\" height=\"40\">" +
            "  <g id=\"layer-a\" data-layer=\"true\" data-name=\"Layer A\">" +
            "    <rect id=\"rect-a\" x=\"10\" y=\"8\" width=\"24\" height=\"12\" fill=\"red\" />" +
            "  </g>" +
            "</svg>");

        var layerElement = Assert.IsType<SvgGroup>(svg.SourceDocument!.GetElementById("layer-a"));
        var rectElement = Assert.IsType<SvgRectangle>(svg.SourceDocument!.GetElementById("rect-a"));

        Assert.True(svg.TryGetRetainedSceneNode(layerElement, out var layerNode));
        Assert.True(svg.TryGetRetainedSceneNode(rectElement, out var rectNode));
        Assert.Equal("layer-a", layerNode!.ElementId);
        Assert.Equal("rect-a", rectNode!.ElementId);

        using var retainedElementPicture = svg.CreateRetainedScenePicture(rectElement);
        Assert.NotNull(retainedElementPicture);

        var hitNodes = svg.HitTestSceneNodes(new SKPoint(16, 12)).ToList();
        Assert.Contains(hitNodes, static node => node.ElementId == "rect-a");

        var topmostNode = svg.HitTestTopmostSceneNode(new SKPoint(16, 12));
        Assert.NotNull(topmostNode);
        Assert.Equal("rect-a", topmostNode!.ElementId);

        var canvasMatrix = SKMatrix.CreateTranslation(4f, 3f);
        var transformedHitNodes = svg.HitTestSceneNodes(new SKPoint(20, 15), canvasMatrix).ToList();
        Assert.Contains(transformedHitNodes, static node => node.ElementId == "rect-a");

        var transformedTopmostNode = svg.HitTestTopmostSceneNode(new SKPoint(20, 15), canvasMatrix);
        Assert.NotNull(transformedTopmostNode);
        Assert.Equal("rect-a", transformedTopmostNode!.ElementId);
    }

    [Theory]
    [InlineData("text-tref-02-b.svg")]
    [InlineData("masking-path-13-f.svg")]
    [InlineData("filters-image-01-b.svg")]
    public void CreateRetainedSceneGraphPicture_MatchesCurrentPicture_ForSelectedW3CDocuments(string fileName)
    {
        var svgPath = GetW3CTestSvgPath(fileName);
        if (!File.Exists(svgPath))
        {
            return;
        }

        using var svg = new SKSvg();
        using var _ = svg.Load(svgPath);
        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(svg.Picture);
        Assert.NotNull(retainedPicture);
        AssertPicturesEqual(svg, svg.Picture!, retainedPicture!);
    }

    private static void AssertCompilationStrategy(
        SvgSceneDocument scene,
        string elementId,
        SvgSceneCompilationStrategy expectedStrategy)
    {
        Assert.True(scene.TryGetNodeById(elementId, out var node));
        Assert.NotNull(node);
        Assert.Equal(expectedStrategy, node!.CompilationStrategy);
    }

    private static void AssertPathCommand<TCommand>(
        SvgSceneDocument scene,
        string elementId,
        Action<TCommand>? assertCommand = null)
        where TCommand : PathCommand
    {
        Assert.True(scene.TryGetNodeById(elementId, out var node));
        Assert.NotNull(node);
        Assert.NotNull(node!.HitTestPath);

        var command = Assert.Single(node.HitTestPath!.Commands!.OfType<TCommand>());
        assertCommand?.Invoke(command);
    }

    private static void AssertPathCommands(SvgSceneDocument scene, string elementId, params Type[] commandTypes)
    {
        Assert.True(scene.TryGetNodeById(elementId, out var node));
        Assert.NotNull(node);
        Assert.NotNull(node!.HitTestPath);

        var actual = node.HitTestPath!.Commands!.Select(static command => command.GetType()).ToArray();
        Assert.Equal(commandTypes, actual);
    }

    private static List<SvgSceneNode> GetTargetMarkerNodes(SvgSceneDocument? scene, string elementId)
    {
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById(elementId, out var targetNode));
        Assert.NotNull(targetNode);

        return targetNode!.Children
            .Where(static node => node.Kind == SvgSceneNodeKind.Marker)
            .ToList();
    }

    private static void AssertMarkerTransform(SvgSceneNode markerNode, float expectedX, float expectedY, float expectedAngle)
    {
        var origin = markerNode.TotalTransform.MapPoint(new SKPoint(0f, 0f));
        var unitX = markerNode.TotalTransform.MapPoint(new SKPoint(1f, 0f));
        var actualAngle = NormalizeAngle((float)(Math.Atan2(unitX.Y - origin.Y, unitX.X - origin.X) * 180.0 / Math.PI));

        AssertApproximately(expectedX, origin.X);
        AssertApproximately(expectedY, origin.Y);
        AssertApproximately(NormalizeAngle(expectedAngle), actualAngle);
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        return angle < 0f ? angle + 360f : angle;
    }

    private static void AssertApproximately(float expected, float actual)
    {
        Assert.True(Math.Abs(expected - actual) <= 0.001f, $"Expected {expected} but found {actual}.");
    }

    private static void AssertNoDrawableBridge(SvgSceneDocument? scene)
    {
        Assert.NotNull(scene);
        Assert.All(scene!.Traverse(), static node => Assert.Equal(SvgSceneCompilationStrategy.DirectRetained, node.CompilationStrategy));
    }

    private static void AssertPicturesEqual(SKSvg svg, SkiaSharp.SKPicture expected, SkiaSharp.SKPicture actual)
    {
        using var expectedBitmap = ToBitmap(svg, expected);
        using var actualBitmap = ToBitmap(svg, actual);

        Assert.Equal(expectedBitmap.Width, actualBitmap.Width);
        Assert.Equal(expectedBitmap.Height, actualBitmap.Height);

        for (var y = 0; y < expectedBitmap.Height; y++)
        {
            for (var x = 0; x < expectedBitmap.Width; x++)
            {
                Assert.Equal(expectedBitmap.GetPixel(x, y), actualBitmap.GetPixel(x, y));
            }
        }
    }

    private static SkiaBitmap ToBitmap(SKSvg svg, SkiaSharp.SKPicture picture)
    {
        var bitmap = picture.ToBitmap(
            SkiaColors.Transparent,
            1f,
            1f,
            SkiaColorType.Rgba8888,
            SkiaAlphaType.Unpremul,
            svg.Settings.Srgb);

        return Assert.IsType<SkiaBitmap>(bitmap);
    }

    private static string GetW3CTestSvgPath(string fileName)
    {
        return Path.Combine("..", "..", "..", "..", "..", "externals", "W3C_SVG_11_TestSuite", "W3C_SVG_11_TestSuite", "svg", fileName);
    }

    private static string GetResvgSvgPath(string fileName)
    {
        return Path.Combine("..", "..", "..", "..", "..", "externals", "resvg", "tests", "svg", fileName);
    }

    private const string SimpleSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="24"
             height="24"
             viewBox="0 0 24 24">
          <g id="group">
            <rect id="target" x="2" y="2" width="8" height="8" fill="red" />
            <circle id="accent" cx="18" cy="6" r="4" fill="blue" />
          </g>
        </svg>
        """;

    private const string SharedUseSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="40"
             height="24"
             viewBox="0 0 40 24">
          <defs>
            <rect id="template" x="0" y="0" width="8" height="8" fill="green" />
          </defs>
          <use id="use-a" xlink:href="#template" x="2" y="2" />
          <use id="use-b" xlink:href="#template" x="18" y="10" />
        </svg>
        """;

    private const string NestedSharedUseSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="48"
             height="24"
             viewBox="0 0 48 24">
          <defs>
            <g id="template">
              <g id="inner">
                <rect id="leaf" x="0" y="0" width="8" height="8" fill="green" />
              </g>
            </g>
          </defs>
          <use id="use-a" xlink:href="#template" x="2" y="2" />
          <use id="use-b" xlink:href="#template" x="20" y="10" />
        </svg>
        """;

    private const string PrimitiveShapesSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="80"
             height="40"
             viewBox="0 0 80 40">
          <path id="shape-path" d="M2,4 L14,4 L8,14 z" fill="#cc0000" />
          <rect id="shape-rect" x="18" y="2" width="12" height="12" fill="#008800" />
          <circle id="shape-circle" cx="40" cy="8" r="6" fill="#0044cc" />
          <ellipse id="shape-ellipse" cx="58" cy="8" rx="8" ry="5" fill="#ff8800" />
          <line id="shape-line" x1="4" y1="24" x2="20" y2="34" stroke="#6600aa" stroke-width="2" />
          <polyline id="shape-polyline" points="26,34 32,22 38,30 44,20" fill="none" stroke="#00aaaa" stroke-width="2" />
          <polygon id="shape-polygon" points="54,22 68,22 74,34 60,36" fill="#ffaa00" />
        </svg>
        """;

    private const string RootOpacitySvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="32"
             height="32"
             viewBox="0 0 32 32"
             opacity="0.5">
          <rect x="2" y="2" width="28" height="28" fill="#008000" />
          <rect x="1" y="1" width="30" height="30" fill="none" stroke="#000000" />
        </svg>
        """;

    private const string FilteredGroupSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="72"
             height="28"
             viewBox="0 0 72 28">
          <defs>
            <linearGradient id="bg" x1="0" y1="0" x2="72" y2="0" gradientUnits="userSpaceOnUse">
              <stop offset="0%" stop-color="#ff0000" />
              <stop offset="100%" stop-color="#00ff00" />
            </linearGradient>
            <filter id="blend" x="0%" y="0%" width="100%" height="100%">
              <feFlood flood-color="#00ff00" flood-opacity="0.5" result="flood" />
              <feBlend in="SourceGraphic" in2="flood" mode="multiply" />
            </filter>
          </defs>
          <rect x="0" y="0" width="72" height="28" fill="url(#bg)" />
          <g filter="url(#blend)">
            <rect x="8" y="6" width="56" height="6" fill="#0000ff" opacity="0.5" />
            <rect x="8" y="16" width="56" height="6" fill="#ffff00" opacity="0.5" />
          </g>
        </svg>
        """;

    private const string FilteredGroupComparisonSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="72"
             height="72"
             viewBox="0 0 72 72">
          <defs>
            <linearGradient id="bg-compare" x1="0" y1="0" x2="72" y2="0" gradientUnits="userSpaceOnUse">
              <stop offset="0%" stop-color="#ff0000" />
              <stop offset="100%" stop-color="#00ff00" />
            </linearGradient>
            <filter id="blend-compare" x="0%" y="0%" width="100%" height="100%">
              <feFlood flood-color="#00ff00" flood-opacity="0.5" result="flood" />
              <feBlend in="SourceGraphic" in2="flood" mode="multiply" />
            </filter>
          </defs>
          <rect x="0" y="0" width="72" height="72" fill="url(#bg-compare)" />
          <rect x="8" y="6" width="56" height="6" fill="#0000ff" opacity="0.5" filter="url(#blend-compare)" />
          <rect x="8" y="24" width="56" height="6" fill="#ffff00" opacity="0.5" filter="url(#blend-compare)" />
          <g filter="url(#blend-compare)">
            <rect x="8" y="42" width="56" height="6" fill="#0000ff" opacity="0.5" />
            <rect x="8" y="60" width="56" height="6" fill="#ffff00" opacity="0.5" />
          </g>
        </svg>
        """;

    private const string FilteredTransformedChildGroupSvg = """
        <svg width="500" height="500" viewBox="0 0 500 500" xmlns="http://www.w3.org/2000/svg">
          <g id="a" filter="url(#f)">
            <circle cx="0" cy="0" r="125" fill="black" transform="translate(250 250)" />
          </g>
          <defs>
            <filter id="f">
              <feOffset dx="0" dy="0" />
            </filter>
          </defs>
        </svg>
        """;

    private const string MaskedSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="24"
             height="24"
             viewBox="0 0 24 24">
          <defs>
            <mask id="alpha-mask">
              <rect x="0" y="0" width="24" height="24" fill="white" />
              <circle id="mask-hole" cx="12" cy="12" r="5" fill="black" />
            </mask>
          </defs>
          <rect id="masked-target" x="2" y="2" width="20" height="20" fill="red" mask="url(#alpha-mask)" />
        </svg>
        """;

    private const string FilteredSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="24"
             height="24"
             viewBox="0 0 24 24">
          <defs>
            <filter id="blur-filter" x="-25%" y="-25%" width="150%" height="150%">
              <feGaussianBlur id="blur-node" stdDeviation="1" />
            </filter>
          </defs>
          <rect id="filtered-target" x="4" y="4" width="16" height="16" fill="#3366cc" filter="url(#blur-filter)" />
        </svg>
        """;

    private static readonly string FilteredImageSvg = $"""
        <svg xmlns="http://www.w3.org/2000/svg"
             width="24"
             height="24"
             viewBox="0 0 24 24">
          <defs>
            <filter id="image-filter" x="0" y="0" width="100%" height="100%">
              <feImage href="data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(EmbeddedImageSvg))}" result="pattern" />
              <feBlend in="SourceGraphic" in2="pattern" mode="multiply" />
            </filter>
          </defs>
          <rect id="filtered-image-target" x="4" y="4" width="16" height="16" fill="#3366cc" filter="url(#image-filter)" />
        </svg>
        """;

    private const string EnableBackgroundOnSameContainerSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="200"
             height="200"
             viewBox="0 0 200 200">
          <filter id="filter1" filterUnits="userSpaceOnUse" x="0" y="0" width="200" height="200">
            <feOffset in="BackgroundImage" dx="100" />
          </filter>
          <g id="background-filter-root" enable-background="new" filter="url(#filter1)">
            <rect id="background-source" x="20" y="70" width="60" height="60" fill="green" />
          </g>
        </svg>
        """;

    private const string InvalidFilterSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="80"
             height="80"
             viewBox="0 0 80 80">
          <defs>
            <filter id="invalid-filter" color-interpolation-filters="sRGB">
              <feDiffuseLighting />
            </filter>
          </defs>
          <rect id="filtered" x="10" y="10" width="60" height="60" fill="red" filter="url(#invalid-filter)" />
          <rect id="frame" x="1" y="1" width="78" height="78" fill="none" stroke="black" />
        </svg>
        """;

    private const string ClippedMaskedAndFilteredSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="48"
             height="16"
             viewBox="0 0 48 16">
          <defs>
            <clipPath id="clip-a">
              <rect x="0" y="0" width="10" height="10" />
            </clipPath>
            <mask id="mask-a">
              <rect x="0" y="0" width="16" height="16" fill="white" />
            </mask>
            <filter id="filter-a" x="-25%" y="-25%" width="150%" height="150%">
              <feGaussianBlur stdDeviation="1" />
            </filter>
          </defs>
          <rect id="clipped-target" x="0" y="0" width="16" height="16" fill="#2244cc" clip-path="url(#clip-a)" />
          <rect id="masked-target" x="16" y="0" width="16" height="16" fill="#aa2244" mask="url(#mask-a)" />
          <rect id="filtered-target" x="32" y="0" width="12" height="12" fill="#22aa44" filter="url(#filter-a)" />
        </svg>
        """;

    private const string StructuralWrappersSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="48"
             height="48"
             viewBox="0 0 48 48">
          <g id="wrapper-group" transform="translate(2,2)">
            <a id="wrapper-anchor" href="https://example.com">
              <svg id="nested-fragment" x="4" y="4" width="18" height="18" viewBox="0 0 18 18">
                <rect id="nested-rect" x="0" y="0" width="10" height="10" fill="#3366cc" />
              </svg>
            </a>
          </g>
        </svg>
        """;

    private const string VisualStateSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="40"
             height="20"
             viewBox="0 0 40 20">
          <g id="background-root" enable-background="new 1 2 30 10">
            <rect id="interactive-target"
                  x="2"
                  y="2"
                  width="12"
                  height="12"
                  fill="red"
                  stroke="black"
                  stroke-width="2"
                  pointer-events="stroke"
                  cursor="crosshair" />
            <rect id="hidden-target"
                  x="18"
                  y="2"
                  width="12"
                  height="12"
                  fill="blue"
                  display="none"
                  visibility="hidden" />
          </g>
        </svg>
        """;

    private static readonly string InlineMixBlendModeImageSvg = $"""
        <svg xmlns="http://www.w3.org/2000/svg"
             width="24"
             height="24"
             viewBox="0 0 24 24">
          <rect x="0" y="0" width="24" height="24" fill="#808080" />
          <image id="blend-image"
                 x="2"
                 y="2"
                 width="20"
                 height="20"
                 style="mix-blend-mode: overlay;"
                 href="data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(EmbeddedImageSvg))}" />
        </svg>
        """;

    private const string StylesheetMixBlendModeSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="24"
             height="24"
             viewBox="0 0 24 24">
          <style>
            #blend-target { mix-blend-mode: multiply; }
          </style>
          <rect x="0" y="0" width="24" height="24" fill="#808080" />
          <rect id="blend-target" x="2" y="2" width="20" height="20" fill="#cc3333" />
        </svg>
        """;

    private const string PresentationMixBlendModeSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="24"
             height="24"
             viewBox="0 0 24 24">
          <rect x="0" y="0" width="24" height="24" fill="#808080" />
          <rect id="blend-target" x="2" y="2" width="20" height="20" fill="#cc3333" mix-blend-mode="multiply" />
        </svg>
        """;

    private const string StylesheetIsolationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="24"
             height="24"
             viewBox="0 0 24 24">
          <style>
            #isolated-group { isolation: isolate; }
            #blend-target { mix-blend-mode: overlay; }
          </style>
          <rect x="0" y="0" width="24" height="24" fill="#008000" />
          <g id="isolated-group">
            <rect id="blend-target" x="2" y="2" width="20" height="20" fill="#ffcc00" />
          </g>
        </svg>
        """;

    private const string TextSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="64"
             height="24"
             viewBox="0 0 64 24">
          <text id="text-target" x="4" y="18" fill="#0055aa" font-size="16">Retained</text>
        </svg>
        """;

    private const string RichTextSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="96"
             height="36"
             viewBox="0 0 96 36">
          <defs>
            <path id="text-curve" d="M4,22 C20,8 36,8 52,22" />
            <text id="text-source">Source Text</text>
          </defs>
          <text id="text-rich-target" x="4" y="14" fill="#0055aa" font-size="10">
            <tspan>Retained </tspan>
            <tspan fill="#aa3300">Scene</tspan>
          </text>
          <text id="text-path-target" fill="#228833" font-size="7">
            <textPath xlink:href="#text-curve" startOffset="3">Curved Text</textPath>
          </text>
          <text id="text-ref-target" x="4" y="31" fill="#663399" font-size="8">
            <tref xlink:href="#text-source" />
          </text>
        </svg>
        """;

    private static readonly string UseSwitchAndImageSvg = $"""
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="72"
             height="28"
             viewBox="0 0 72 28">
          <defs>
            <rect id="use-template" x="0" y="0" width="10" height="10" fill="#00aa55" />
          </defs>
          <use id="use-target" xlink:href="#use-template" x="4" y="4" />
          <switch id="switch-target" transform="translate(22,4)">
            <rect id="switch-selected" x="0" y="0" width="12" height="10" fill="#aa3300" />
            <rect id="switch-fallback" x="0" y="0" width="12" height="10" fill="#000000" requiredFeatures="http://example.invalid/unsupported-feature" />
          </switch>
          <image id="image-target"
                 x="44"
                 y="2"
                 width="20"
                 height="20"
                 href="data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(EmbeddedImageSvg))}" />
        </svg>
        """;

    private const string EmbeddedImageSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 10 10">
          <rect x="0" y="0" width="10" height="10" fill="#2244cc" />
          <circle cx="5" cy="5" r="3" fill="#ffcc00" />
        </svg>
        """;

    private const string UseAndMarkerSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="48"
             height="24"
             viewBox="0 0 48 24">
          <defs>
            <marker id="arrow" markerWidth="6" markerHeight="6" refX="5" refY="3" orient="auto" markerUnits="strokeWidth">
              <path d="M0,0 L6,3 L0,6 z" fill="#ff00ff" />
            </marker>
            <path id="segment" d="M0,0 L14,0" stroke="#0055aa" stroke-width="2" marker-end="url(#arrow)" fill="none" />
          </defs>
          <use id="segment-a" xlink:href="#segment" x="4" y="8" />
          <use id="segment-b" xlink:href="#segment" x="24" y="16" />
        </svg>
        """;

    private const string GroupMarkerSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="48"
             height="24"
             viewBox="0 0 48 24">
          <defs>
            <marker id="arrow" markerWidth="6" markerHeight="6" refX="5" refY="3" orient="auto" markerUnits="strokeWidth">
              <path d="M0,0 L6,3 L0,6 z" fill="#ff00ff" />
            </marker>
          </defs>
          <g id="marker-group" marker-end="url(#arrow)">
            <path id="marker-line" d="M4,12 L28,12" stroke="#0055aa" stroke-width="2" fill="none" />
          </g>
        </svg>
        """;

    private const string NonRenderingContainerSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="40"
             height="20"
             viewBox="0 0 40 20">
          <defs>
            <linearGradient id="grad">
              <stop offset="0%" stop-color="red" />
            </linearGradient>
          </defs>
          <symbol id="sym" viewBox="0 0 10 10">
            <rect id="symbol-rect" x="0" y="0" width="10" height="10" fill="url(#grad)" />
          </symbol>
          <foreignObject id="fo" x="0" y="0" width="10" height="10" />
          <use id="instance" xlink:href="#sym" x="10" y="0" />
        </svg>
        """;

    private const string ConditionalReferenceSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="40"
             height="48"
             viewBox="0 0 40 48">
          <g id="feature-group" requiredFeatures="http://example.invalid/unsupported-feature">
            <rect id="feature-rect" x="0" y="0" width="12" height="12" fill="#ff0000" />
          </g>
          <use id="feature-use" xlink:href="#feature-rect" x="16" y="0" />
          <g id="extension-group" requiredExtensions="http://example.invalid/unsupported-extension">
            <rect id="extension-rect" x="0" y="16" width="12" height="12" fill="#00ff00" />
          </g>
          <use id="extension-use" xlink:href="#extension-rect" x="16" y="0" />
          <g id="language-group" systemLanguage="invalid-language-tag">
            <rect id="language-rect" x="0" y="32" width="12" height="12" fill="#0000ff" />
          </g>
          <use id="language-use" xlink:href="#language-rect" x="16" y="0" />
        </svg>
        """;

    private const string VisibilityOverrideSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="320"
             height="320"
             viewBox="0 0 320 320">
          <rect x="96" y="96" width="96" height="96" fill="lime" />
          <g visibility="hidden">
            <rect x="96" y="96" width="96" height="96" fill="red" />
          </g>
          <rect x="196.5" y="196.5" width="95" height="95" fill="red" />
          <g id="visible-group" visibility="hidden">
            <rect x="196" y="196" width="96" height="96" fill="lime" visibility="visible" />
          </g>
        </svg>
        """;

    private const string AnimatedSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="24"
             height="10"
             viewBox="0 0 24 10">
          <rect id="moving" x="0" y="0" width="4" height="4" fill="red">
            <animate attributeName="x" from="0" to="10" dur="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string GradientSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="24"
             height="24"
             viewBox="0 0 24 24">
          <defs>
            <linearGradient id="grad" x1="0%" y1="0%" x2="100%" y2="0%">
              <stop id="stop-a" offset="0%" stop-color="red" />
              <stop id="stop-b" offset="100%" stop-color="green" />
            </linearGradient>
          </defs>
          <rect id="gradient-target" x="2" y="2" width="20" height="20" fill="url(#grad)" />
        </svg>
        """;

    private const string PatternSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="28"
             height="28"
             viewBox="0 0 28 28">
          <defs>
            <pattern id="pat" x="0" y="0" width="8" height="8" patternUnits="userSpaceOnUse">
              <rect id="pattern-bg" x="0" y="0" width="8" height="8" fill="#ffeeaa" />
              <circle id="pattern-dot" cx="4" cy="4" r="2" fill="#2255aa" />
            </pattern>
          </defs>
          <rect id="pattern-target" x="2" y="2" width="24" height="24" fill="url(#pat)" />
        </svg>
        """;

    private const string MaskedRichTextSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="120"
             height="48"
             viewBox="0 0 120 48">
          <defs>
            <path id="complex-curve" d="M10,28 C30,8 50,8 70,28" />
            <mask id="text-mask">
              <rect x="0" y="0" width="120" height="48" fill="black" />
              <text x="8" y="18" fill="white" font-size="12">
                <tspan>Mask </tspan>
                <tspan>Text</tspan>
              </text>
              <text fill="white" font-size="8">
                <textPath xlink:href="#complex-curve">Path</textPath>
              </text>
            </mask>
          </defs>
          <g id="complex-target" mask="url(#text-mask)">
            <rect x="4" y="4" width="80" height="32" fill="#2255aa" />
            <text id="complex-text" x="8" y="18" fill="#ffeeaa" font-size="12">
              <tspan>Retained </tspan>
              <tspan fill="#ff8833">Mask</tspan>
            </text>
            <text id="complex-path-text" fill="#ffffff" font-size="8">
              <textPath xlink:href="#complex-curve">Scene Graph</textPath>
            </text>
          </g>
        </svg>
        """;
}
