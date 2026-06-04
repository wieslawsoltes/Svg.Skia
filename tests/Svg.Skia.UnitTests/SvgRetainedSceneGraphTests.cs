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
using Svg.Model;
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

    private static Dictionary<string, SKPoint> GetPositionedGlyphPoints(SKPicture retainedModel, params string[] glyphs)
    {
        var result = new Dictionary<string, SKPoint>(StringComparer.Ordinal);
        var glyphSet = new HashSet<string>(glyphs, StringComparer.Ordinal);
        foreach (var command in retainedModel.FindCommands<DrawTextCanvasCommand>())
        {
            if (glyphSet.Contains(command.Text))
            {
                result.TryAdd(command.Text, new SKPoint(command.X, command.Y));
            }
        }

        foreach (var command in retainedModel.FindCommands<DrawTextBlobCanvasCommand>())
        {
            if (command.TextBlob?.Text is not { } text ||
                command.TextBlob.Points is not { } points ||
                text.Length != points.Length)
            {
                continue;
            }

            for (var i = 0; i < text.Length; i++)
            {
                var glyph = text[i].ToString();
                if (glyphSet.Contains(glyph))
                {
                    result.TryAdd(glyph, points[i]);
                }
            }
        }

        return result;
    }

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
    public void RetainedSceneGraph_FoldsSimpleTransformOnlyShapeGroups()
    {
        const string svgText = """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="80" viewBox="0 0 120 80">
              <g id="group" transform="translate(20 10) rotate(15)">
                <rect id="rect" x="0" y="0" width="14" height="14" fill="seagreen" opacity="0.85" />
                <path id="triangle" d="M 0 14 L 7 0 L 14 14 Z" fill="white" opacity="0.25" />
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgText);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<SaveCanvasCommand>());
        Assert.Empty(retainedModel.FindCommands<SetMatrixCanvasCommand>());
        Assert.Empty(retainedModel.FindCommands<RestoreCanvasCommand>());

        var rect = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawPathCanvasCommand>("rect"));
        var triangle = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawPathCanvasCommand>("triangle"));

        Assert.IsType<AddPolyPathCommand>(Assert.Single(rect.Path!.Commands!));
        Assert.IsType<AddPolyPathCommand>(Assert.Single(triangle.Path!.Commands!));
        Assert.True(rect.Path!.Bounds.Left > 15f, $"Expected transformed rect bounds, but got {rect.Path.Bounds}.");
        Assert.True(triangle.Path!.Bounds.Top > 9f, $"Expected transformed triangle bounds, but got {triangle.Path.Bounds}.");
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
    public void RetainedSceneGraph_TextPathHrefCanTargetRectangle()
    {
        const string shapeTargetSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="120" viewBox="0 0 140 120">
              <defs>
                <rect id="target-rect" x="30" y="30" width="80" height="60" />
              </defs>
              <text fill="#0055aa" font-size="8">
                <textPath href="#target-rect">C</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(shapeTargetSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var drawText = Assert.Single(retainedModel!.FindCommands<DrawTextCanvasCommand>(), static cmd => cmd.Text == "C");
        Assert.InRange(drawText.X, 28f, 38f);
        Assert.InRange(drawText.Y, 28f, 34f);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathHrefCanTargetRoundedRectangle()
    {
        const string shapeTargetSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="120" viewBox="0 0 140 120">
              <defs>
                <rect id="target-round-rect" x="30" y="30" width="80" height="60" rx="10" ry="15" />
              </defs>
              <text fill="#0055aa" font-size="8">
                <textPath href="#target-round-rect">R</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(shapeTargetSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var drawText = Assert.Single(retainedModel!.FindCommands<DrawTextCanvasCommand>(), static cmd => cmd.Text == "R");
        Assert.InRange(drawText.X, 38f, 46f);
        Assert.InRange(drawText.Y, 28f, 34f);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathHrefWinsOverXlinkHref()
    {
        const string hrefPrecedenceSvg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="120"
                 height="100"
                 viewBox="0 0 120 100">
              <defs>
                <path id="xlink-path" d="M10,80 L110,80" />
                <path id="href-path" d="M10,20 L110,20" />
              </defs>
              <text fill="#0055aa" font-size="12">
                <textPath href="#href-path" xlink:href="#xlink-path">D</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(hrefPrecedenceSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var drawText = Assert.Single(retainedModel!.FindCommands<DrawTextCanvasCommand>(), static cmd => cmd.Text == "D");
        Assert.InRange(drawText.Y, 18f, 22f);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathOpenPathStartOffsetOutsidePathClipsGlyphs()
    {
        const string openPathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="120" viewBox="0 0 220 120">
              <defs>
                <path id="line-before" d="M20,30 L120,30" />
                <path id="line-after" d="M20,80 L120,80" />
              </defs>
              <text fill="#0055aa" font-size="8">
                <textPath href="#line-before" startOffset="-4">ABCD</textPath>
              </text>
              <text fill="#0055aa" font-size="8">
                <textPath href="#line-after" startOffset="120">EFGH</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(openPathSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var renderedTexts = retainedModel!
            .FindCommands<DrawTextCanvasCommand>()
            .Where(static command => !string.IsNullOrWhiteSpace(command.Text))
            .Select(static command => command.Text)
            .ToList();

        Assert.Contains("B", renderedTexts);
        Assert.DoesNotContain("A", renderedTexts);
        Assert.DoesNotContain("E", renderedTexts);
        Assert.DoesNotContain("H", renderedTexts);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathSideRight_UsesOppositePathSide()
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

        Assert.NotEmpty(retainedModel!.FindCommands<SetMatrixCanvasCommand>());
        var drawText = Assert.Single(retainedModel.FindCommands<DrawTextCanvasCommand>(), static cmd => cmd.Text == "A");

        Assert.InRange(drawText.X, 24f, 32f);
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
    public void RetainedSceneGraph_TransformOriginAbsoluteFillBoxOffsetsReferenceBox()
    {
        const string transformOriginSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="120" viewBox="0 0 180 120">
              <rect id="target"
                    x="100"
                    y="20"
                    width="30"
                    height="10"
                    fill="#0055aa"
                    transform-box="fill-box"
                    transform-origin="10px 5px"
                    transform="rotate(90)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(transformOriginSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var targetNode));
        Assert.NotNull(targetNode);
        Assert.False(targetNode!.Transform.IsIdentity);

        var expectedOrigin = new SKPoint(110f, 25f);
        var actualOrigin = targetNode.Transform.MapPoint(expectedOrigin);

        AssertApproximately(expectedOrigin.X, actualOrigin.X);
        AssertApproximately(expectedOrigin.Y, actualOrigin.Y);
    }

    [Fact]
    public void RetainedSceneGraph_StructuralTransformOriginAppliesToGroup()
    {
        const string transformOriginSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">
              <g id="group" transform-origin="50 50" transform="rotate(90)">
                <rect id="target" x="10" y="20" width="10" height="10" fill="#0055aa" />
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(transformOriginSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("group", out var groupNode));
        Assert.True(scene.TryGetNodeById("target", out var targetNode));
        Assert.NotNull(groupNode);
        Assert.NotNull(targetNode);

        var expectedOrigin = new SKPoint(50f, 50f);
        var actualOrigin = groupNode!.Transform.MapPoint(expectedOrigin);

        AssertApproximately(expectedOrigin.X, actualOrigin.X);
        AssertApproximately(expectedOrigin.Y, actualOrigin.Y);
        AssertApproximately(expectedOrigin.X, targetNode!.TotalTransform.MapPoint(expectedOrigin).X);
        AssertApproximately(expectedOrigin.Y, targetNode.TotalTransform.MapPoint(expectedOrigin).Y);
    }

    [Fact]
    public void RetainedSceneGraph_UseTransformOriginUsesReferencedFillBox()
    {
        const string transformOriginSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100">
              <defs>
                <rect id="shape" x="20" y="20" width="20" height="10" fill="#0055aa" />
              </defs>
              <use id="use-target"
                   href="#shape"
                   transform-box="fill-box"
                   transform-origin="center"
                   transform="rotate(90)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(transformOriginSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("use-target", out var useNode));
        Assert.NotNull(useNode);

        var expectedOrigin = new SKPoint(30f, 25f);
        var actualOrigin = useNode!.Transform.MapPoint(expectedOrigin);

        AssertApproximately(expectedOrigin.X, actualOrigin.X);
        AssertApproximately(expectedOrigin.Y, actualOrigin.Y);
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
    public void RetainedSceneGraph_TextPathTspanAbsolutePosition_ResetsPathOffset()
    {
        const string absolutePositionSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="160" viewBox="0 0 140 160">
              <defs>
                <path id="line" d="M10,40 L130,40" />
              </defs>
              <text fill="#0055aa" font-size="10">
                <textPath href="#line">A<tspan x="70" y="120">B</tspan></textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.FromSvg(absolutePositionSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var glyphPositions = retainedModel!
            .FindCommands<DrawTextCanvasCommand>()
            .Where(static command => command.Text is "A" or "B")
            .ToDictionary(static command => command.Text, static command => new SKPoint(command.X, command.Y), StringComparer.Ordinal);

        Assert.True(glyphPositions.TryGetValue("A", out var a), "Expected textPath to render the first glyph.");
        Assert.True(glyphPositions.TryGetValue("B", out var b), "Expected textPath to render the absolute-positioned glyph.");
        Assert.True(
            b.X > a.X + 50f,
            $"Expected absolute x on textPath tspan to reset path distance, but A was {a} and B was {b}.");
        Assert.Equal(a.Y, b.Y, 3);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathRootDyIsIgnored()
    {
        const string dySvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="100" viewBox="0 0 140 100">
              <defs>
                <path id="line" d="M10,40 L130,40" />
              </defs>
              <text fill="#0055aa" font-size="10">
                <textPath id="path-run" href="#line" dy="25">A</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.FromSvg(dySvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var drawText = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("path-run"), static cmd => cmd.Text == "A");
        Assert.Equal(40f, drawText.Y, 3);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathRootCoordinatesAreIgnored()
    {
        const string positionSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="160" height="100" viewBox="0 0 160 100">
              <defs>
                <path id="line" d="M10,40 L150,40" />
              </defs>
              <text fill="#0055aa" font-size="10">
                <textPath id="path-run" href="#line" x="50" y="55" dy="7">A</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.FromSvg(positionSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var drawText = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("path-run"), static cmd => cmd.Text == "A");
        Assert.InRange(drawText.X, 5f, 20f);
        Assert.Equal(40f, drawText.Y, 3);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathDrawsGraphemeClusterAsSingleRun()
    {
        const string clusterSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="90" viewBox="0 0 180 90">
              <defs>
                <path id="line" d="M20,48 L160,48" />
              </defs>
              <text fill="#0055aa" font-size="20">
                <textPath id="cluster-path" href="#line">A&#x0301;B</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.FromSvg(clusterSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var commands = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("cluster-path")
            .ToList();

        Assert.Contains(commands, static command => command.Text == "A\u0301");
        Assert.DoesNotContain(commands, static command => command.Text == "\u0301");
    }

    [Fact]
    public void RetainedSceneGraph_TextPathDistributesTextPathLengthAcrossNestedSpans()
    {
        const string textPathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="90" viewBox="0 0 220 90">
              <defs>
                <path id="line" d="M20,48 L200,48" />
              </defs>
              <text fill="#0055aa" font-size="24">
                <textPath id="path-run" href="#line" textLength="120"><tspan>A</tspan><tspan>B</tspan></textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.FromSvg(textPathSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var glyphs = retainedModel!
            .FindCommands<DrawTextCanvasCommand>()
            .Where(static command => command.Text is "A" or "B")
            .ToDictionary(static command => command.Text, static command => command, StringComparer.Ordinal);

        Assert.True(glyphs.TryGetValue("A", out var a), "Expected nested textPath span A.");
        Assert.True(glyphs.TryGetValue("B", out var b), "Expected nested textPath span B.");
        Assert.Equal(a.Y, b.Y, 3);
        Assert.InRange(b.X - a.X, 45f, 95f);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathSpacingAuto_KeepsNaturalGlyphSpacing()
    {
        const string spacingAutoSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="60" viewBox="0 0 140 60">
              <defs>
                <path id="line" d="M10,30 L130,30" />
              </defs>
              <text fill="#0055aa" font-size="10">
                <textPath href="#line" spacing="auto">AB</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.Settings.EnableSvgFonts = false;
        svg.FromSvg(spacingAutoSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var positions = retainedModel!
            .FindCommands<DrawTextCanvasCommand>()
            .Where(static command => command.Text is "A" or "B")
            .ToDictionary(static command => command.Text, static command => command.X, StringComparer.Ordinal);

        Assert.True(positions.TryGetValue("A", out var a), "Expected spacing=auto textPath to render the first glyph.");
        Assert.True(positions.TryGetValue("B", out var b), "Expected spacing=auto textPath to render the second glyph.");
        Assert.True(
            b - a < 30f,
            $"Expected spacing=auto to keep natural textPath glyph spacing, but A was {a} and B was {b}.");
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_DrawsWarpedGlyphPath()
    {
        const string stretchSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="100" viewBox="0 0 180 100">
              <defs>
                <path id="curve" d="M20 60 C60 20 120 20 160 60" />
              </defs>
              <text font-size="24" fill="#0055aa">
                <textPath id="stretch-path" href="#curve" method="stretch">A</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(stretchSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var stretchedPaths = retainedModel!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("stretch-path").ToList();
        Assert.NotEmpty(stretchedPaths);
        Assert.Empty(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("stretch-path"));
        Assert.Contains(stretchedPaths, static command => command.Path is { IsEmpty: false });
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_AppliesSpacingAndGlyphsTextLength()
    {
        const string stretchSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="90" viewBox="0 0 220 90">
              <defs>
                <path id="line" d="M20 48 L200 48" />
              </defs>
              <text font-size="24" fill="#0055aa">
                <textPath id="stretch-path" href="#line" method="stretch" textLength="120" lengthAdjust="spacingAndGlyphs">A</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(stretchSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var stretchedPaths = retainedModel!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("stretch-path")
            .Where(static command => command.Path is { IsEmpty: false })
            .ToList();

        Assert.NotEmpty(stretchedPaths);
        Assert.Contains(stretchedPaths, static command => command.Path!.Bounds.Width > 60f);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_AppliesSpacingAndGlyphsTextLengthToGraphemeClusters()
    {
        const string stretchSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="260" height="90" viewBox="0 0 260 90">
              <defs>
                <path id="line" d="M20 48 L240 48" />
              </defs>
              <text font-size="28" fill="#0055aa">
                <textPath id="stretch-path" href="#line" method="stretch" textLength="150" lengthAdjust="spacingAndGlyphs">A&#x0301;B</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(stretchSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var stretchedBounds = retainedModel!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("stretch-path")
            .Where(static command => command.Path is { IsEmpty: false })
            .Select(static command => command.Path!.Bounds)
            .Aggregate(SKRect.Empty, static (accumulator, bounds) =>
                accumulator.IsEmpty ? bounds : SKRect.Union(accumulator, bounds));

        Assert.Empty(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("stretch-path"));
        Assert.False(stretchedBounds.IsEmpty);
        Assert.True(
            stretchedBounds.Width > 90f,
            $"Expected lengthAdjust=spacingAndGlyphs to stretch textPath grapheme clusters across the requested length, but bounds were {stretchedBounds}.");
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_AppliesSpacingTextLengthBetweenClusters()
    {
        const string stretchSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="90" viewBox="0 0 240 90">
              <defs>
                <path id="line" d="M20 48 L220 48" />
              </defs>
              <text font-size="28" fill="#0055aa">
                <textPath id="stretch-path" href="#line" method="stretch" textLength="150" lengthAdjust="spacing">AB</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(stretchSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var stretchedBounds = retainedModel!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("stretch-path")
            .Where(static command => command.Path is { IsEmpty: false })
            .Select(static command => command.Path!.Bounds)
            .Aggregate(SKRect.Empty, static (accumulator, bounds) =>
                accumulator.IsEmpty ? bounds : SKRect.Union(accumulator, bounds));

        Assert.False(stretchedBounds.IsEmpty);
        Assert.True(
            stretchedBounds.Width > 100f,
            $"Expected lengthAdjust=spacing to spread stretched textPath clusters across the requested length, but bounds were {stretchedBounds}.");
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_AppliesTextPathTextLengthToNestedSpan()
    {
        const string stretchSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="90" viewBox="0 0 220 90">
              <defs>
                <path id="line" d="M20 48 L200 48" />
              </defs>
              <text font-size="24" fill="#0055aa">
                <textPath id="stretch-path" href="#line" method="stretch" textLength="120" lengthAdjust="spacingAndGlyphs"><tspan fill="#aa5500">A</tspan></textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(stretchSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var stretchedPaths = retainedModel!
            .FindCommands<DrawPathCanvasCommand>()
            .Where(static command => command.Path is { IsEmpty: false })
            .ToList();

        Assert.NotEmpty(stretchedPaths);
        Assert.Contains(stretchedPaths, static command => command.Path!.Bounds.Width > 60f);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_DrawsComplexScriptAsWarpedPath()
    {
        const string stretchSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="100" viewBox="0 0 200 100">
              <defs>
                <path id="curve" d="M20 60 C70 20 130 20 180 60" />
              </defs>
              <text font-size="24" fill="#0055aa" font-family="Amiri" direction="rtl" unicode-bidi="embed">
                <textPath id="stretch-path" href="#curve" method="stretch">&#x0633;&#x0644;&#x0627;&#x0645;</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(stretchSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var stretchedPaths = retainedModel!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("stretch-path")
            .Where(static command => command.Path is { IsEmpty: false })
            .ToList();

        Assert.NotEmpty(stretchedPaths);
        Assert.Empty(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("stretch-path"));
        Assert.Contains(stretchedPaths, static command => command.Path!.Bounds.Width > 0f && command.Path.Bounds.Height > 0f);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_AppliesSpacingTextLengthToRtlClusters()
    {
        const string stretchSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="90" viewBox="0 0 240 90">
              <defs>
                <path id="line" d="M20 48 L220 48" />
              </defs>
              <text font-size="30" fill="#0055aa" font-family="Amiri" direction="rtl" unicode-bidi="embed">
                <textPath id="stretch-path" href="#line" method="stretch" textLength="150" lengthAdjust="spacing">&#x0633;&#x0644;&#x0627;&#x0645;</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(stretchSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var stretchedBounds = retainedModel!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("stretch-path")
            .Where(static command => command.Path is { IsEmpty: false })
            .Select(static command => command.Path!.Bounds)
            .Aggregate(SKRect.Empty, static (accumulator, bounds) =>
                accumulator.IsEmpty ? bounds : SKRect.Union(accumulator, bounds));

        Assert.Empty(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("stretch-path"));
        Assert.False(stretchedBounds.IsEmpty);
        Assert.True(
            stretchedBounds.Width > 25f,
            $"Expected RTL lengthAdjust=spacing to use renderable clusters instead of collapsing around bidi controls, but bounds were {stretchedBounds}.");
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_ShapesArabicIndicAndEmojiClusters()
    {
        const string stretchSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="320" height="130" viewBox="0 0 320 130">
              <defs>
                <path id="curve" d="M20 80 C90 15 230 15 300 80" />
              </defs>
              <text font-size="26" fill="#0055aa" font-family="Amiri, Noto Sans, Noto Emoji" direction="rtl" unicode-bidi="embed">
                <textPath id="stretch-path" href="#curve" method="stretch">&#x0633;&#x0644;&#x0627;&#x0645; &#x0915;&#x093f; &#x1F469;&#x200D;&#x1F467;</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(stretchSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var stretchedPaths = retainedModel!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("stretch-path")
            .Where(static command => command.Path is { IsEmpty: false })
            .Select(static command => command.Path!)
            .ToList();

        Assert.NotEmpty(stretchedPaths);
        Assert.Empty(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("stretch-path"));
        Assert.All(stretchedPaths, static path =>
        {
            Assert.True(float.IsFinite(path.Bounds.Left), $"Expected finite shaped stretch bounds, but got {path.Bounds}.");
            Assert.True(float.IsFinite(path.Bounds.Top), $"Expected finite shaped stretch bounds, but got {path.Bounds}.");
            Assert.True(float.IsFinite(path.Bounds.Right), $"Expected finite shaped stretch bounds, but got {path.Bounds}.");
            Assert.True(float.IsFinite(path.Bounds.Bottom), $"Expected finite shaped stretch bounds, but got {path.Bounds}.");
        });
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_AppliesLetterAndWordSpacingWithTextLength()
    {
        const string stretchSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="260" height="100" viewBox="0 0 260 100">
              <defs>
                <path id="line" d="M20 55 L240 55" />
              </defs>
              <text font-size="28" fill="#0055aa">
                <textPath id="stretch-path" href="#line" method="stretch"
                          textLength="190" lengthAdjust="spacing"
                          letter-spacing="8" word-spacing="24">A B</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(stretchSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var stretchedBounds = retainedModel!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("stretch-path")
            .Where(static command => command.Path is { IsEmpty: false })
            .Select(static command => command.Path!.Bounds)
            .Aggregate(SKRect.Empty, static (accumulator, bounds) =>
                accumulator.IsEmpty ? bounds : SKRect.Union(accumulator, bounds));

        Assert.False(stretchedBounds.IsEmpty);
        Assert.True(
            stretchedBounds.Width > 145f,
            $"Expected letter/word spacing plus textLength to spread stretched clusters, but bounds were {stretchedBounds}.");
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_IgnoresNestedTextPathChild()
    {
        const string stretchSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="260" height="120" viewBox="0 0 260 120">
              <path id="line1" d="M20 55 L240 55" />
              <path id="line2" d="M20 90 L240 90" />
              <text font-size="28" fill="#0055aa">
                <textPath id="outer" href="#line1" method="stretch">A<textPath id="nested" href="#line2" method="stretch">B</textPath>C</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(stretchSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.NotEmpty(retainedModel!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("outer"));
        Assert.Empty(retainedModel.FindCommandsBySourceElementId<DrawPathCanvasCommand>("nested"));
        Assert.Empty(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("nested"));
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_ClampsOverhangingOutlineToPathEnd()
    {
        const string stretchSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="80" viewBox="0 0 180 80">
              <defs>
                <path id="line" d="M20 42 L78 42" />
              </defs>
              <text font-size="28" fill="#0055aa">
                <textPath id="stretch-path" href="#line" method="stretch">OVERHANGING</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(stretchSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var stretchedPaths = retainedModel!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("stretch-path").ToList();
        Assert.NotEmpty(stretchedPaths);
        Assert.Contains(stretchedPaths, static command => command.Path is { IsEmpty: false });
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_AppliesFillStrokePaintOrder()
    {
        const string stretchSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="80" viewBox="0 0 180 80">
              <defs>
                <path id="line" d="M20 44 L160 44" />
              </defs>
              <text font-size="28">
                <textPath id="stretch-path" href="#line" method="stretch"
                          fill="#ff0000" stroke="#0000ff" stroke-width="2"
                          paint-order="stroke fill">A</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(stretchSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var commands = retainedModel!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("stretch-path")
            .Where(static command => command.Paint?.Style is SKPaintStyle.Fill or SKPaintStyle.Stroke)
            .ToList();

        Assert.Equal(2, commands.Count);
        Assert.Equal(SKPaintStyle.Stroke, commands[0].Paint!.Style);
        Assert.Equal(new SKColor(0, 0, 255, 255), commands[0].Paint!.Color);
        Assert.Equal(SKPaintStyle.Fill, commands[1].Paint!.Style);
        Assert.Equal(new SKColor(255, 0, 0, 255), commands[1].Paint!.Color);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_DrawsWarpedDecorations()
    {
        const string stretchSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="80" viewBox="0 0 180 80">
              <defs>
                <path id="line" d="M20 44 L160 44" />
              </defs>
              <text font-size="28" fill="#0055aa">
                <textPath id="stretch-path" href="#line" method="stretch"
                          text-decoration="underline">A</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(stretchSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var commands = retainedModel!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("stretch-path")
            .Where(static command => command.Path is { IsEmpty: false } && command.Paint?.Style == SKPaintStyle.Fill)
            .ToList();

        Assert.True(commands.Count >= 2, $"Expected glyph and decoration paths, but saw {commands.Count} path command(s).");
        Assert.Contains(commands, static command => command.Path!.Bounds.Width > 8f && command.Path.Bounds.Height < 5f);
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_DistributesTextPathLengthAcrossNestedSpans()
    {
        const string stretchSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="320" height="90" viewBox="0 0 320 90">
              <defs>
                <path id="line" d="M20 48 L300 48" />
              </defs>
              <text font-size="24" fill="#0055aa">
                <textPath id="stretch-path" href="#line" method="stretch" textLength="120" lengthAdjust="spacingAndGlyphs"><tspan>A</tspan><tspan>B</tspan></textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(stretchSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var stretchedBounds = retainedModel!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("stretch-path")
            .Where(static command => command.Path is { IsEmpty: false })
            .Select(static command => command.Path!.Bounds)
            .Aggregate(SKRect.Empty, static (accumulator, bounds) =>
                accumulator.IsEmpty ? bounds : SKRect.Union(accumulator, bounds));

        Assert.False(stretchedBounds.IsEmpty);
        Assert.True(stretchedBounds.Width > 80f, $"Expected two nested textPath spans to cover the requested textLength, but bounds were {stretchedBounds}.");
        Assert.True(stretchedBounds.Width < 180f, $"Expected textPath textLength to be distributed across child spans instead of applied once per span, but bounds were {stretchedBounds}.");
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_AppliesFilters()
    {
        const string stretchSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="90" viewBox="0 0 180 90">
              <defs>
                <path id="line" d="M20 48 L160 48" />
                <filter id="blur" x="-20%" y="-40%" width="140%" height="180%">
                  <feGaussianBlur stdDeviation="2" />
                </filter>
              </defs>
              <text font-size="28" fill="#0055aa">
                <textPath id="stretch-path" href="#line" method="stretch" filter="url(#blur)">A</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(stretchSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Contains(
            retainedModel!.FindCommandsBySourceElementId<SaveLayerCanvasCommand>("stretch-path"),
            static command => command.Paint?.ImageFilter is not null);
        Assert.Contains(
            retainedModel.FindCommandsBySourceElementId<DrawPathCanvasCommand>("stretch-path"),
            static command => command.Path is { IsEmpty: false });
    }

    [Fact]
    public void RetainedSceneGraph_TextPathTinyCoordinates_ProducesFiniteGlyphPlacement()
    {
        const string textPathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 1 1">
              <defs>
                <path id="tiny" d="M0.02 0.50 C0.25 0.05 0.75 0.95 0.98 0.50" />
              </defs>
              <text font-size="0.08" fill="#0055aa">
                <textPath id="tiny-path" href="#tiny" startOffset="0.01" dy="0.02">AB</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(textPathSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var glyphs = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("tiny-path")
            .Where(static command => command.Text is "A" or "B")
            .ToList();

        Assert.NotEmpty(glyphs);
        Assert.All(glyphs, static command =>
        {
            Assert.True(float.IsFinite(command.X), $"Expected finite tiny-coordinate textPath X, but got {command.X}.");
            Assert.True(float.IsFinite(command.Y), $"Expected finite tiny-coordinate textPath Y, but got {command.Y}.");
        });

        var mappedOrigins = retainedModel
            .FindCommands<SetMatrixCanvasCommand>()
            .Select(static command => command.TotalMatrix.MapPoint(new SKPoint(0f, 0f)))
            .ToList();
        Assert.NotEmpty(mappedOrigins);
        Assert.All(mappedOrigins, static point =>
        {
            Assert.True(float.IsFinite(point.X), $"Expected finite tiny-coordinate textPath matrix X, but got {point.X}.");
            Assert.True(float.IsFinite(point.Y), $"Expected finite tiny-coordinate textPath matrix Y, but got {point.Y}.");
        });
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_FilterAndDecorationParity()
    {
        const string stretchSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="120" viewBox="0 0 220 120">
              <defs>
                <path id="curve" d="M20 70 C80 10 140 10 200 70" />
                <filter id="shadow" x="-20%" y="-20%" width="140%" height="140%">
                  <feDropShadow dx="2" dy="3" stdDeviation="1" flood-color="#111827" flood-opacity="0.45" />
                </filter>
              </defs>
              <text font-size="24" fill="#0055aa" text-decoration="underline" filter="url(#shadow)">
                <textPath id="stretch-path" href="#curve" method="stretch">ABC</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(stretchSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);
        Assert.NotEmpty(retainedModel!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("stretch-path"));
        Assert.NotEmpty(retainedModel.FindCommands<SaveLayerCanvasCommand>());
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_FilterBoundsIncludeDecorationsAndStroke()
    {
        const string stretchSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="120" viewBox="0 0 240 120">
              <defs>
                <path id="line" d="M20 62 L220 62" />
                <filter id="blur" x="-10%" y="-50%" width="120%" height="200%">
                  <feGaussianBlur stdDeviation="2" />
                </filter>
              </defs>
              <text font-size="28" fill="#0055aa" stroke="#aa5500" stroke-width="10" text-decoration="underline">
                <textPath id="stretch-path" href="#line" method="stretch" filter="url(#blur)">ABC</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(stretchSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var pathBounds = retainedModel!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("stretch-path")
            .Where(static command => command.Path is { IsEmpty: false })
            .Select(static command => command.Path!.Bounds)
            .Aggregate(SKRect.Empty, static (accumulator, bounds) =>
                accumulator.IsEmpty ? bounds : SKRect.Union(accumulator, bounds));

        Assert.False(pathBounds.IsEmpty);
        Assert.Contains(
            retainedModel.FindCommandsBySourceElementId<SaveLayerCanvasCommand>("stretch-path"),
            static command => command.Paint?.ImageFilter is not null);
        Assert.True(pathBounds.Height > 10f, $"Expected stroke and decoration paths to participate in retained stretch bounds, but bounds were {pathBounds}.");
    }

    [Fact]
    public void RetainedSceneGraph_TextPathMethodStretch_TinyCoordinatesProduceFinitePath()
    {
        const string textPathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 1 1">
              <defs>
                <path id="tiny" d="M0.02 0.50 C0.25 0.05 0.75 0.95 0.98 0.50" />
              </defs>
              <text font-size="0.08" fill="#0055aa">
                <textPath id="tiny-path" href="#tiny" method="stretch" startOffset="0.01">AB</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(textPathSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var paths = retainedModel!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("tiny-path")
            .Where(static command => command.Path is { IsEmpty: false })
            .Select(static command => command.Path!)
            .ToList();

        Assert.NotEmpty(paths);
        Assert.All(paths, static path =>
        {
            Assert.True(float.IsFinite(path.Bounds.Left), $"Expected finite tiny-coordinate stretch path bounds, but got {path.Bounds}.");
            Assert.True(float.IsFinite(path.Bounds.Top), $"Expected finite tiny-coordinate stretch path bounds, but got {path.Bounds}.");
            Assert.True(float.IsFinite(path.Bounds.Right), $"Expected finite tiny-coordinate stretch path bounds, but got {path.Bounds}.");
            Assert.True(float.IsFinite(path.Bounds.Bottom), $"Expected finite tiny-coordinate stretch path bounds, but got {path.Bounds}.");
        });
    }

    [Fact]
    public void RetainedSceneGraph_TextPathCurrentPosition_EndsAtPathEndForFollowingText()
    {
        const string textPathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="260" height="80" viewBox="0 0 260 80">
              <path id="line" d="M20,40 L220,40" />
              <text id="label" font-size="20"><textPath href="#line">A</textPath><tspan id="after">B</tspan></text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(textPathSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var glyphs = retainedModel!
            .FindCommands<DrawTextCanvasCommand>()
            .Where(static command => command.Text is "A" or "B")
            .ToDictionary(static command => command.Text, static command => command.X, StringComparer.Ordinal);

        Assert.True(glyphs.TryGetValue("A", out var a), "Expected textPath glyph A.");
        Assert.True(glyphs.TryGetValue("B", out var b), "Expected following inline glyph B.");
        Assert.True(b > 200f, $"Expected following text to continue after the referenced path end for current suite parity, but B was at {b}.");
    }

    [Fact]
    public void RetainedSceneGraph_VerticalTextPathCurrentPosition_ContinuesAfterRenderedRun()
    {
        const string textPathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="220" viewBox="0 0 220 220">
              <path id="loop" d="M 50 110 A 60 60 0 0 1 110 50 60 60 0 0 1 170 110 60 60 0 0 1 110 170 60 60 0 0 1 50 110 Z" />
              <text id="label" font-family="Mplus 1p" font-size="22" writing-mode="tb">
                <textPath id="path-run" href="#loop" startOffset="-10">非常に長いテキ</textPath><tspan id="after">ス</tspan>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(textPathSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.NotEmpty(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("path-run"));
        var after = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("after"));

        Assert.True(
            after.X > 120f,
            $"Expected following vertical text to continue near the rendered textPath run end on the closed path, but X was {after.X}.");
    }

    [Fact]
    public void RetainedSceneGraph_NestedTextPath_IgnoresNestedChildAndKeepsOuterText()
    {
        const string textPathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="260" height="120" viewBox="0 0 260 120">
              <path id="line1" d="M20,35 L220,35" />
              <path id="line2" d="M20,80 L220,80" />
              <text font-size="20">
                <textPath id="outer" href="#line1">A<textPath id="nested" href="#line2">B</textPath>C</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(textPathSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var outer = retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("outer").ToList();
        Assert.Contains(outer, static command => command.Text == "A");
        Assert.Contains(outer, static command => command.Text == "C");
        Assert.DoesNotContain(retainedModel.FindCommands<DrawTextCanvasCommand>(), static command => command.Text == "B");
        Assert.All(outer, static command => Assert.InRange(command.Y, 20f, 50f));
    }

    [Fact]
    public void RetainedSceneGraph_TextPathParentDy_AppliesToIndependentSiblingChunks()
    {
        const string textPathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="260" height="120" viewBox="0 0 260 120">
              <path id="line1" d="M20,30 L220,30" />
              <path id="line2" d="M20,70 L220,70" />
              <text id="label" font-size="20" dy="12">
                <textPath id="first" href="#line1">A</textPath>
                <textPath id="second" href="#line2">B</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(textPathSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var first = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("first"));
        var second = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("second"));

        Assert.InRange(first.X, 15f, 35f);
        Assert.InRange(second.X, 15f, 35f);
        Assert.True(first.Y > 30f, $"Expected parent dy to move first textPath below path baseline, but Y was {first.Y}.");
        Assert.True(second.Y > 70f, $"Expected parent dy to move sibling textPath below path baseline, but Y was {second.Y}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeTextPathOnly_UsesInlineContentStart()
    {
        const string textPathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="90" viewBox="0 0 240 90">
              <path id="line" d="M0,45 L220,45" />
              <text id="label" x="90" y="45" font-size="20" inline-size="100" text-anchor="middle">
                <textPath id="path-run" href="#line">AB</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(textPathSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var firstGlyph = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("path-run"), static command => command.Text == "A");
        Assert.InRange(firstGlyph.X, 35f, 50f);
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeTextPath_AppliesTextPathOwnedTextLength()
    {
        const string textPathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="90" viewBox="0 0 240 90">
              <path id="line" d="M0,45 L220,45" />
              <text id="label" x="20" y="45" font-size="20" inline-size="120">
                <textPath id="path-run" href="#line" textLength="100">AB</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(textPathSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var glyphs = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("path-run")
            .Where(static command => command.Text is "A" or "B")
            .ToDictionary(static command => command.Text, static command => command, StringComparer.Ordinal);

        Assert.True(glyphs.TryGetValue("A", out var a), "Expected textPath glyph A.");
        Assert.True(glyphs.TryGetValue("B", out var b), "Expected textPath glyph B.");
        Assert.Equal(a.Y, b.Y, 3);
        Assert.True(b.X > a.X + 70f, $"Expected textPath-owned textLength to spread inline-size textPath glyphs, but A was at {a.X} and B was at {b.X}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeTextPathRootDx_OffsetsContentStart()
    {
        const string textPathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="90" viewBox="0 0 240 90">
              <path id="line" d="M0,45 L220,45" />
              <text id="label" x="90" y="45" dx="10" font-size="20" inline-size="100" text-anchor="middle">
                <textPath id="path-run" href="#line">AB</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(textPathSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var firstGlyph = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("path-run"), static command => command.Text == "A");
        Assert.InRange(firstGlyph.X, 45f, 60f);
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeTextPathWithMixedSiblings_WrapsTrailingText()
    {
        const string textPathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="130" viewBox="0 0 240 130">
              <path id="line" d="M0,45 L220,45" />
              <text id="label" x="10" y="45" font-size="20" inline-size="45">
                <tspan id="head">AA</tspan><textPath id="path-run" href="#line">A</textPath><tspan id="tail">BB</tspan>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(textPathSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var pathGlyph = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("path-run"), static command => command.Text == "A");
        var tail = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("tail"), static command => command.Text == "BB");
        Assert.True(pathGlyph.X > 25f, $"Expected textPath sibling to keep its inline slot after the leading text, but X was {pathGlyph.X}.");
        Assert.InRange(tail.X, 5f, 25f);
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeTextPathWithMixedSiblings_AppliesRootTextLength()
    {
        const string textPathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="280" height="120" viewBox="0 0 280 120">
              <path id="line" d="M0,45 L260,45" />
              <text id="label" x="10" y="45" font-size="20" inline-size="180" textLength="160">
                <tspan id="head">AA</tspan><textPath id="path-run" href="#line">A</textPath><tspan id="tail">B</tspan>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(textPathSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var head = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("head"), static command => command.Text == "AA");
        var pathGlyph = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("path-run"), static command => command.Text == "A");
        var tail = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("tail"), static command => command.Text == "B");
        Assert.True(pathGlyph.X > head.X + 50f, $"Expected root textLength to widen the plain-text slot before textPath, but head X was {head.X} and path X was {pathGlyph.X}.");
        Assert.True(tail.X > pathGlyph.X + 20f, $"Expected root textLength to widen the textPath slot before trailing text, but path X was {pathGlyph.X} and tail X was {tail.X}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeStyledWrapperTextPath_WrapsWithSiblings()
    {
        const string textPathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="260" height="120" viewBox="0 0 260 120">
              <path id="line" d="M0,45 L220,45" />
              <text id="label" x="10" y="45" font-size="20" inline-size="25">
                <tspan fill="red"><textPath id="path-run" href="#line">A</textPath></tspan><tspan id="tail">B</tspan>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(textPathSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var pathGlyph = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("path-run"), static command => command.Text == "A");
        var tail = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("tail"), static command => command.Text == "B");
        Assert.InRange(pathGlyph.X, 5f, 25f);
        Assert.InRange(tail.X, 5f, 25f);
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeVerticalTextPath_UsesExistingFallback()
    {
        const string textPathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="160" viewBox="0 0 220 160">
              <path id="line" d="M20,30 L20,130" />
              <text id="label" x="20" y="30" font-size="20" inline-size="80" writing-mode="vertical-rl">
                <textPath id="path-run" href="#line">A</textPath><tspan id="tail">B</tspan>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(textPathSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("path-run"), static command => command.Text == "A");
        Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("tail"), static command => command.Text == "B");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeTextPathTinyPathLength_KeepsFinitePlacement()
    {
        const string textPathSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="90" viewBox="0 0 120 90">
              <path id="line" d="M0,45 L0.000000001,45" pathLength="100" />
              <text id="label" x="10" y="45" font-size="20" inline-size="80">
                <textPath id="path-run" href="#line" startOffset="50">A</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(textPathSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var pathGlyph = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("path-run"), static command => command.Text == "A");
        Assert.False(float.IsNaN(pathGlyph.X));
        Assert.False(float.IsInfinity(pathGlyph.X));
        Assert.False(float.IsNaN(pathGlyph.Y));
        Assert.False(float.IsInfinity(pathGlyph.Y));
    }

    [Fact]
    public void RetainedSceneGraph_DominantBaselineMiddle_AdjustsTextBaseline()
    {
        const string baselineSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="100" viewBox="0 0 180 100">
              <text id="alphabetic" x="10" y="50" font-size="24">A</text>
              <text id="middle" x="60" y="50" font-size="24" dominant-baseline="middle">A</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(baselineSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var alphabetic = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("alphabetic"));
        var middle = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("middle"));

        Assert.True(
            middle.Y > alphabetic.Y + 4f,
            $"Expected dominant-baseline=middle to move the Skia alphabetic baseline down from {alphabetic.Y}, but got {middle.Y}.");
    }

    [Fact]
    public void RetainedSceneGraph_DominantBaselineFromStylesheet_AdjustsTextBaseline()
    {
        const string baselineSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="100" viewBox="0 0 180 100">
              <style>#middle { dominant-baseline: middle; }</style>
              <text id="alphabetic" x="10" y="50" font-size="24">A</text>
              <text id="middle" x="60" y="50" font-size="24">A</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(baselineSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var alphabetic = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("alphabetic"));
        var middle = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("middle"));

        Assert.True(
            middle.Y > alphabetic.Y + 4f,
            $"Expected stylesheet dominant-baseline=middle to move the Skia alphabetic baseline down from {alphabetic.Y}, but got {middle.Y}.");
    }

    [Fact]
    public void RetainedSceneGraph_AlignmentBaselineOverridesDominantBaseline()
    {
        const string baselineSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="100" viewBox="0 0 180 100">
              <text id="alphabetic" x="10" y="50" font-size="24">A</text>
              <text id="override" x="60" y="50" font-size="24" dominant-baseline="text-before-edge" alignment-baseline="alphabetic">A</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(baselineSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var alphabetic = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("alphabetic"));
        var baselineOverride = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("override"));

        Assert.Equal(alphabetic.Y, baselineOverride.Y, 3);
    }

    [Fact]
    public void RetainedSceneGraph_BaselineIdentifiersUseDistinctMetricOffsets()
    {
        const string baselineSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="140" viewBox="0 0 240 140">
              <text id="alphabetic" x="10" y="70" font-size="24">A</text>
              <text id="hanging" x="60" y="70" font-size="24" alignment-baseline="hanging">A</text>
              <text id="ideographic" x="110" y="70" font-size="24" alignment-baseline="ideographic">A</text>
              <text id="before" x="160" y="70" font-size="24" alignment-baseline="text-before-edge">A</text>
              <text id="after" x="210" y="70" font-size="24" alignment-baseline="text-after-edge">A</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(baselineSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var alphabetic = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("alphabetic"));
        var hanging = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("hanging"));
        var ideographic = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("ideographic"));
        var before = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("before"));
        var after = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("after"));

        Assert.True(hanging.Y > alphabetic.Y + 8f, $"Expected hanging to move below alphabetic, but was {hanging.Y} vs {alphabetic.Y}.");
        Assert.True(before.Y > hanging.Y, $"Expected text-before-edge to move below hanging, but was {before.Y} vs {hanging.Y}.");
        Assert.True(ideographic.Y < alphabetic.Y, $"Expected ideographic to move above alphabetic, but was {ideographic.Y} vs {alphabetic.Y}.");
        Assert.True(after.Y < ideographic.Y, $"Expected text-after-edge to move above ideographic, but was {after.Y} vs {ideographic.Y}.");
    }

    [Fact]
    public void RetainedSceneGraph_BaselineShiftPercentage_UsesFontSizeBasis()
    {
        const string baselineSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="100" viewBox="0 0 180 100">
              <text id="alphabetic" x="10" y="60" font-size="24">A</text>
              <text id="shifted" x="60" y="60" font-size="24" baseline-shift="50%">A</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(baselineSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var alphabetic = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("alphabetic"));
        var shifted = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("shifted"));

        Assert.True(shifted.Y < alphabetic.Y - 8f, $"Expected percentage baseline-shift to move text up from {alphabetic.Y}, but got {shifted.Y}.");
    }

    [Fact]
    public void RetainedSceneGraph_DominantBaselineUseScript_ChoosesIdeographicForCjkText()
    {
        const string baselineSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="100" viewBox="0 0 220 100">
              <text id="latin" x="10" y="60" font-size="24" dominant-baseline="use-script">ABC</text>
              <text id="cjk" x="90" y="60" font-size="24" dominant-baseline="use-script">漢字</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(baselineSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var latin = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("latin"));
        var cjk = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("cjk"));

        Assert.True(cjk.Y < latin.Y, $"Expected use-script CJK text to select an ideographic baseline above alphabetic, but was {cjk.Y} vs {latin.Y}.");
    }

    [Fact]
    public void RetainedSceneGraph_AltGlyph_RendersTextContentFallbackAndPreservesSource()
    {
        const string altGlyphSvg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="180" height="80" viewBox="0 0 180 80">
              <defs>
                <altGlyphDef id="alt">
                  <glyphRef xlink:href="#glyphA" glyphRef="glyphA" />
                </altGlyphDef>
              </defs>
              <text id="label" x="10" y="40" font-size="24">
                <altGlyph id="ag" xlink:href="#alt" glyphRef="glyphA">A</altGlyph>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(altGlyphSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draw = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("ag"));
        Assert.Equal("A", draw.Text);
    }

    [Fact]
    public void RetainedSceneGraph_FontFeatureProperties_FlowIntoTextPaint()
    {
        const string featureSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="80" viewBox="0 0 220 80">
              <text id="features"
                    x="10"
                    y="40"
                    font-size="24"
                    style="font-feature-settings: 'liga' 0, 'kern' 1; font-kerning: none; font-variant-ligatures: no-common-ligatures discretionary-ligatures">office</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(featureSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draw = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("features"));
        Assert.NotNull(draw.Paint);
        Assert.Equal("'liga' 0, 'kern' 1", draw.Paint!.FontFeatureSettings);
        Assert.Equal("none", draw.Paint.FontKerning);
        Assert.Equal("no-common-ligatures discretionary-ligatures", draw.Paint.FontVariantLigatures);
    }

    [Fact]
    public void RetainedSceneGraph_FontSizeFromStylesheet_FlowsIntoTextPaint()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="80" viewBox="0 0 180 80">
              <style>#styled { font-size: 32px; }</style>
              <text id="styled" x="10" y="40" font-family="sans-serif">A</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draw = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("styled"));
        Assert.Equal(32f, draw.Paint!.TextSize, 3);
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeTextOverflowClip_AddsInlineClip()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="80" viewBox="0 0 180 80">
              <text id="clip" x="10" y="40" font-size="20" inline-size="48" text-overflow="clip">ABCDEFG</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var clip = Assert.Single(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        Assert.Equal(10f, clip.Rect.Left, 3);
        Assert.Equal(58f, clip.Rect.Right, 3);

        var draw = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("clip"));
        Assert.Equal("ABCDEFG", draw.Text);
        AssertInlineClipCommandOrder(retainedModel, "clip");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeTextOverflowEllipsis_ReplacesTailWithMarker()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="80" viewBox="0 0 180 80">
              <text id="ellipsis" x="10" y="40" font-size="20" inline-size="48" text-overflow="ellipsis">ABCDEFG</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Single(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var draws = retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("ellipsis").ToList();

        Assert.Contains(draws, static command => command.Text == "\u2026");
        Assert.DoesNotContain(draws, static command => command.Text == "ABCDEFG");
        AssertInlineClipCommandOrder(retainedModel, "ellipsis");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeTextOverflowCustomMarker_UsesQuotedMarker()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="80" viewBox="0 0 180 80">
              <text id="marker" x="10" y="40" font-size="20" inline-size="52" text-overflow="'>>'">ABCDEFG</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Single(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var draws = retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("marker").ToList();

        Assert.Contains(draws, static command => command.Text == ">>");
        Assert.DoesNotContain(draws, static command => command.Text == "ABCDEFG");
        AssertInlineClipCommandOrder(retainedModel, "marker");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeTextOverflowMarkerUsesLastRunStyle()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="80" viewBox="0 0 220 80">
              <text id="styled-marker" x="10" y="40" font-size="20" inline-size="82" text-overflow="ellipsis" white-space="nowrap">
                <tspan fill="black">ABCDEFG</tspan><tspan fill="#ff0000"> tail</tspan>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var marker = Assert.Single(
            retainedModel!.FindCommands<DrawTextCanvasCommand>(),
            static command => command.Text == "\u2026");

        Assert.Equal(new SKColor(0xff, 0x00, 0x00, 0xff), marker.Paint!.Color);
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeTextOverflowMiddleAnchor_CentersInlineClip()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="80" viewBox="0 0 180 80">
              <text id="middle" x="90" y="40" font-size="20" inline-size="48" text-overflow="ellipsis" text-anchor="middle">ABCDEFG</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var clip = Assert.Single(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        Assert.Equal(66f, clip.Rect.Left, 3);
        Assert.Equal(114f, clip.Rect.Right, 3);

        var draws = retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("middle").ToList();
        Assert.Contains(draws, static command => command.Text == "\u2026");
        Assert.DoesNotContain(draws, static command => command.Text == "ABCDEFG");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeTextOverflowEndAnchor_EndsInlineClipAtAnchor()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="80" viewBox="0 0 180 80">
              <text id="end" x="120" y="40" font-size="20" inline-size="48" text-overflow="ellipsis" text-anchor="end">ABCDEFG</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var clip = Assert.Single(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        Assert.Equal(72f, clip.Rect.Left, 3);
        Assert.Equal(120f, clip.Rect.Right, 3);

        var draws = retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("end").ToList();
        Assert.Contains(draws, static command => command.Text == "\u2026");
        Assert.DoesNotContain(draws, static command => command.Text == "ABCDEFG");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeMiddleAnchorWithoutOverflow_AnchorsContentArea()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="80" viewBox="0 0 180 80">
              <text id="middle-fit" x="90" y="40" font-size="20" inline-size="100" text-overflow="ellipsis" text-anchor="middle">A</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var draw = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("middle-fit"));

        Assert.Equal("A", draw.Text.Replace("\u202B", string.Empty, StringComparison.Ordinal).Replace("\u202C", string.Empty, StringComparison.Ordinal));
        Assert.Equal(40f, draw.X, 3);
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeEndAnchorWithoutOverflow_AnchorsContentArea()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="80" viewBox="0 0 180 80">
              <text id="end-fit" x="120" y="40" font-size="20" inline-size="80" text-overflow="ellipsis" text-anchor="end">A</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var draw = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("end-fit"));

        Assert.Equal("A", draw.Text);
        Assert.Equal(40f, draw.X, 3);
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWithoutOverflow_UsesExistingSequentialPath()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="80" viewBox="0 0 220 80">
              <text id="fits" x="10" y="40" font-size="20" inline-size="200" text-overflow="ellipsis">ABC</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var draw = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("fits"));
        Assert.Equal("ABC", draw.Text);
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWhiteSpaceNormal_WrapsWordsIntoLines()
    {
        const string wrapSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="120" viewBox="0 0 180 120">
              <text id="wrap" x="10" y="25" font-size="20" inline-size="22">A B C</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(wrapSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var draws = retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("wrap").ToList();

        Assert.Equal(new[] { "A", "B", "C" }, draws.Select(static command => command.Text).ToArray());
        Assert.Equal(10f, draws[0].X, 3);
        Assert.True(draws[1].Y > draws[0].Y + 10f, $"Expected the second word to move to a later line, but Y was {draws[1].Y} vs {draws[0].Y}.");
        Assert.True(draws[2].Y > draws[1].Y + 10f, $"Expected the third word to move to a later line, but Y was {draws[2].Y} vs {draws[1].Y}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeVerticalWritingMode_WrapsInlineProgression()
    {
        const string wrapSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="160" viewBox="0 0 140 160">
              <text id="vertical-wrap" x="90" y="20" font-size="20" inline-size="42" writing-mode="tb">A B C</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(wrapSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draws = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("vertical-wrap")
            .Where(static command => command.Text.Contains('A') || command.Text.Contains('B') || command.Text.Contains('C'))
            .ToList();

        Assert.Contains(draws, static command => command.Text.Contains('A'));
        Assert.Contains(draws, static command => command.Text.Contains('B'));
        Assert.Contains(draws, static command => command.Text.Contains('C'));
        Assert.True(draws[1].Y > draws[0].Y, "Expected vertical inline progression before wrapping to the next column.");
        Assert.True(draws[2].X < draws[0].X, "Expected vertical inline-size wrapping to advance to the next line column.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeVerticalDirectionRightToLeft_KeepsColumnWrapping()
    {
        const string wrapSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="140" height="160" viewBox="0 0 140 160">
              <text id="vertical-rtl-wrap" x="90" y="20" font-size="20" inline-size="42" writing-mode="tb" direction="rtl" unicode-bidi="embed">A B C</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(wrapSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draws = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("vertical-rtl-wrap")
            .Where(static command => command.Text.Contains('A') || command.Text.Contains('B') || command.Text.Contains('C'))
            .ToList();

        Assert.Contains(draws, static command => command.Text.Contains('A'));
        Assert.Contains(draws, static command => command.Text.Contains('B'));
        Assert.Contains(draws, static command => command.Text.Contains('C'));
        Assert.True(draws[1].Y > draws[0].Y, "Expected vertical direction=rtl inline-size text to keep vertical inline progression before wrapping.");
        Assert.True(draws[2].X < draws[0].X, "Expected vertical direction=rtl inline-size wrapping to continue using right-to-left columns.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeVerticalRlDirectionRightToLeft_AdvancesBottomToTop()
    {
        const string wrapSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="160" height="180" viewBox="0 0 160 180">
              <text id="vertical-rl-rtl-wrap" x="110" y="120" font-size="20" inline-size="45" writing-mode="vertical-rl" direction="rtl" unicode-bidi="embed">AB CD</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(wrapSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draws = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("vertical-rl-rtl-wrap")
            .Where(static command => command.Text.Contains('A') || command.Text.Contains('C'))
            .ToList();

        Assert.Equal(2, draws.Count);
        var firstLine = draws[0];
        var secondLine = draws[1];
        Assert.True(firstLine.Y > 100f, $"Expected vertical-rl direction=rtl line start near the bottom edge of the inline area, but Y was {firstLine.Y}.");
        Assert.True(secondLine.X < firstLine.X - 10f, $"Expected vertical-rl wrapped columns to progress right-to-left, but X was {secondLine.X} vs {firstLine.X}.");
        Assert.True(secondLine.Y > 100f, $"Expected wrapped vertical-rl direction=rtl line start near the bottom edge of the inline area, but Y was {secondLine.Y}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeVerticalRlShapeInside_UsesResolvedBaselineColumnBand()
    {
        const string wrapSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="150" height="160" viewBox="0 0 150 160">
              <defs>
                <rect id="shape" x="80" y="20" width="26" height="105" />
              </defs>
              <text id="vertical-shape" font-size="20" shape-inside="url(#shape)" writing-mode="vertical-rl" direction="rtl" unicode-bidi="embed">A</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(wrapSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draw = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("vertical-shape"));
        Assert.Equal("A", draw.Text.Replace("\u202B", string.Empty, StringComparison.Ordinal).Replace("\u202C", string.Empty, StringComparison.Ordinal));
        Assert.InRange(draw.X, 90f, 98f);
        Assert.True(draw.Y > 120f, $"Expected vertical-rl direction=rtl shape text to start from the bottom of the shape interval, but Y was {draw.Y}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeRightToLeft_WrapsFromRightEdge()
    {
        const string wrapSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="120" viewBox="0 0 180 120">
              <text id="rtl-wrap" x="150" y="25" font-size="20" inline-size="42" direction="rtl" unicode-bidi="embed">A B C</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(wrapSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draws = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("rtl-wrap")
            .Where(static command => command.Text.Contains('A') || command.Text.Contains('B') || command.Text.Contains('C'))
            .ToList();

        Assert.Contains(draws, static command => command.Text.Contains('A'));
        Assert.Contains(draws, static command => command.Text.Contains('B'));
        Assert.Contains(draws, static command => command.Text.Contains('C'));
        Assert.All(draws, static command => Assert.True(command.X <= 150f, $"Expected RTL line start at or before the right edge, but X was {command.X}."));
        Assert.True(draws[2].Y > draws[0].Y + 10f, $"Expected RTL inline-size wrapping to advance lines, but draws were: {string.Join(", ", draws.Select(static command => $"[{command.Text}]@{command.X:F2},{command.Y:F2}"))}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeStyleDirectionRightToLeft_WrapsFromRightEdge()
    {
        const string wrapSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="120" viewBox="0 0 180 120">
              <g style="direction: rtl; unicode-bidi: embed">
                <text id="rtl-wrap" x="150" y="25" font-size="20" inline-size="42">A B C</text>
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(wrapSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draws = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("rtl-wrap")
            .Where(static command => command.Text.Contains('A') || command.Text.Contains('B') || command.Text.Contains('C'))
            .ToList();

        Assert.Contains(draws, static command => command.Text.Contains('A'));
        Assert.Contains(draws, static command => command.Text.Contains('B'));
        Assert.Contains(draws, static command => command.Text.Contains('C'));
        Assert.All(draws, static command => Assert.True(command.X <= 150f, $"Expected style-driven RTL line start at or before the right edge, but X was {command.X}."));
        Assert.True(draws[2].Y > draws[0].Y + 10f, $"Expected style-driven RTL inline-size wrapping to advance lines, but draws were: {string.Join(", ", draws.Select(static command => $"[{command.Text}]@{command.X:F2},{command.Y:F2}"))}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeRightToLeftMixedBidi_OrdersRunsPerLine()
    {
        const string wrapSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="120" viewBox="0 0 240 120">
              <text id="rtl-mixed-wrap" x="210" y="30" font-size="20" inline-size="180" direction="rtl" unicode-bidi="embed">ABC &#x05D0;&#x05D1;&#x05D2; DEF</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(wrapSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draws = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("rtl-mixed-wrap")
            .Select(static command => StripBidiControls(command.Text).Trim())
            .Where(static text => text.Length > 0)
            .ToList();

        var visualText = string.Concat(draws);
        var defIndex = visualText.IndexOf("DEF", StringComparison.Ordinal);
        var hebrewIndex = visualText.IndexOf("\u05D0\u05D1\u05D2", StringComparison.Ordinal);
        var abcIndex = visualText.IndexOf("ABC", StringComparison.Ordinal);

        Assert.True(defIndex >= 0, $"Expected DEF draw text, but got: {string.Join(", ", draws)}.");
        Assert.True(hebrewIndex >= 0, $"Expected Hebrew draw text, but got: {string.Join(", ", draws)}.");
        Assert.True(abcIndex >= 0, $"Expected ABC draw text, but got: {string.Join(", ", draws)}.");
        Assert.True(defIndex < hebrewIndex && hebrewIndex < abcIndex, $"Expected RTL visual order DEF, Hebrew, ABC, but got: {string.Join(", ", draws)}.");

        static string StripBidiControls(string text)
        {
            return text
                .Replace("\u061C", string.Empty, StringComparison.Ordinal)
                .Replace("\u200E", string.Empty, StringComparison.Ordinal)
                .Replace("\u200F", string.Empty, StringComparison.Ordinal)
                .Replace("\u202A", string.Empty, StringComparison.Ordinal)
                .Replace("\u202B", string.Empty, StringComparison.Ordinal)
                .Replace("\u202C", string.Empty, StringComparison.Ordinal)
                .Replace("\u202D", string.Empty, StringComparison.Ordinal)
                .Replace("\u202E", string.Empty, StringComparison.Ordinal)
                .Replace("\u2066", string.Empty, StringComparison.Ordinal)
                .Replace("\u2067", string.Empty, StringComparison.Ordinal)
                .Replace("\u2068", string.Empty, StringComparison.Ordinal)
                .Replace("\u2069", string.Empty, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeRightToLeftMixedBidiWithIsolate_UsesFallbackPath()
    {
        const string wrapSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="120" viewBox="0 0 240 120">
              <text id="rtl-isolate-wrap" x="210" y="30" font-size="20" inline-size="180" direction="rtl" unicode-bidi="embed">ABC &#x2067;&#x05D0;&#x05D1;&#x05D2;&#x2069; DEF</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(wrapSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draws = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("rtl-isolate-wrap")
            .Select(static command => command.Text)
            .ToList();
        var retainedText = string.Concat(draws);

        Assert.Contains("ABC", retainedText, StringComparison.Ordinal);
        Assert.Contains("DEF", retainedText, StringComparison.Ordinal);
        Assert.Contains("\u2067", retainedText, StringComparison.Ordinal);
        Assert.Contains("\u2069", retainedText, StringComparison.Ordinal);
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizePreLine_ForcesLineBreaks()
    {
        const string wrapSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="120" viewBox="0 0 180 120">
              <text id="preline" x="10" y="25" font-size="20" inline-size="150" white-space="pre-line">A
            B</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(wrapSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var draws = retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("preline").ToList();

        Assert.Equal(new[] { "A", "B" }, draws.Where(static command => !string.IsNullOrWhiteSpace(command.Text)).Select(static command => command.Text).ToArray());
        Assert.Equal(10f, draws[0].X, 3);
        Assert.Equal(10f, draws[1].X, 3);
        Assert.True(draws[1].Y > draws[0].Y + 10f, $"Expected pre-line newline to advance vertically, but Y was {draws[1].Y} vs {draws[0].Y}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWrappedOverflowMarkerUsesLineRunStyle()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="140" viewBox="0 0 240 140">
              <text id="wrapped-marker" x="10" y="35" font-size="20" inline-size="62" text-overflow="ellipsis">
                <tspan fill="#ff0000">ABCDEFGHIJK</tspan> <tspan id="tail" fill="#0000ff">tail</tspan>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var marker = Assert.Single(
            retainedModel!.FindCommands<DrawTextCanvasCommand>(),
            static command => command.Text == "\u2026");

        Assert.Equal(new SKColor(0xff, 0x00, 0x00, 0xff), marker.Paint!.Color);
        Assert.Contains(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("tail"), static command => command.Text == "tail");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWrap_DoesNotBreakWordAcrossTspans()
    {
        const string wrapSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="120" viewBox="0 0 180 120">
              <text id="wrap" x="10" y="25" font-size="20" inline-size="35"><tspan id="first">A</tspan><tspan id="second">B</tspan> C</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(wrapSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var first = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("first"));
        var second = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("second"));
        var rootDraws = retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("wrap").ToList();

        Assert.Equal(first.Y, second.Y, 3);
        Assert.Contains(rootDraws, command => command.Text == "C" && command.Y > first.Y + 10f);
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeMiddleAnchorWrap_AnchorsEveryLineToContentArea()
    {
        const string wrapSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="120" viewBox="0 0 180 120">
              <text id="middle-wrap" x="90" y="25" font-size="20" inline-size="22" text-anchor="middle">A B</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(wrapSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var draws = retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("middle-wrap").ToList();

        Assert.Equal(new[] { "A", "B" }, draws.Where(static command => !string.IsNullOrWhiteSpace(command.Text)).Select(static command => command.Text).ToArray());
        Assert.All(draws, static command => Assert.Equal(79f, command.X, 3));
        Assert.True(draws[1].Y > draws[0].Y + 10f, $"Expected the second line to advance vertically, but Y was {draws[1].Y} vs {draws[0].Y}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWithShapeInside_UsesShapeBoundsForWrappedLayout()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="140" viewBox="0 0 220 140">
              <defs>
                <rect id="shape" x="40" y="20" width="26" height="105" />
              </defs>
              <text id="shape-text" x="10" y="50" font-size="20" shape-inside="url(#shape)">A B C</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var draws = retainedModel
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("shape-text")
            .Where(static command => !string.IsNullOrWhiteSpace(command.Text))
            .ToList();

        Assert.Equal(new[] { "A", "B", "C" }, draws.Select(static command => command.Text).ToArray());
        Assert.All(draws, static command => Assert.Equal(40f, command.X, 3));
        Assert.True(draws[0].Y > 20f && draws[0].Y < 80f, $"Expected first shape-inside baseline inside the shape, but Y was {draws[0].Y}.");
        Assert.True(draws[1].Y > draws[0].Y + 10f, $"Expected shape-inside text to wrap inside the rectangle, but Y was {draws[1].Y} vs {draws[0].Y}.");
        Assert.True(draws[2].Y > draws[1].Y + 10f, $"Expected third word to wrap to a later shape-inside line, but Y was {draws[2].Y} vs {draws[1].Y}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWithRoundedInsetShapeInside_UsesCurvedLineFragment()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="160" height="140" viewBox="0 0 160 140">
              <text id="shape-text" font-size="20" shape-inside="inset(20px 60px 20px 20px round 40px)">A</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draw = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("shape-text"));
        Assert.Equal("A", draw.Text);
        Assert.InRange(draw.X, 50f, 65f);
        Assert.True(draw.Y > 20f, $"Expected rounded inset baseline to remain inside the shape, but Y was {draw.Y}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWithBasicShapeAndFillBox_UsesBasicShape()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="140" viewBox="0 0 180 140">
              <text id="shape-text" font-size="20" shape-inside="inset(20px 130px 20px 20px) fill-box">W W</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var draws = retainedModel
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("shape-text")
            .Where(static command => !string.IsNullOrWhiteSpace(command.Text))
            .ToList();

        Assert.Equal(new[] { "W", "W" }, draws.Select(static command => command.Text).ToArray());
        Assert.Equal(20f, draws[0].X, 3);
        Assert.All(draws, static command => Assert.InRange(command.X, 20f, 50f));
        Assert.True(draws[1].Y > draws[0].Y + 10f, $"Expected fill-box-qualified shape to wrap text, but Y values were {draws[0].Y} and {draws[1].Y}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWithImageShapeThreshold_UsesAlphaIntervals()
    {
        const string alphaPng = "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAYAAACp8Z5+AAAAE0lEQVR4nGNgYGD4D8UNUEyqAACVLwv51oy5YgAAAABJRU5ErkJggg==";
        var overflowSvg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="100" viewBox="0 0 80 100">
              <text id="shape-text" font-size="30" shape-inside="url(data:image/png;base64,{alphaPng}) view-box" shape-image-threshold="0.75">A B</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draws = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("shape-text")
            .Where(static command => !string.IsNullOrWhiteSpace(command.Text))
            .ToList();

        Assert.Equal(new[] { "A", "B" }, draws.Select(static command => command.Text).ToArray());
        Assert.True(draws[1].Y > draws[0].Y + 15f, $"Expected image-derived shape intervals to wrap the second word, but Y values were {draws[0].Y} and {draws[1].Y}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWithJpegImageShape_UsesSkiaDecodedAlpha()
    {
        var jpegUri = CreateOpaqueEncodedImageDataUri(SkiaSharp.SKEncodedImageFormat.Jpeg, "image/jpeg");
        var overflowSvg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="90" viewBox="0 0 120 90">
              <text id="shape-text" font-size="24" shape-inside="url({jpegUri}) view-box">A B</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draws = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("shape-text")
            .Where(static command => !string.IsNullOrWhiteSpace(command.Text))
            .ToList();

        Assert.NotEmpty(draws);
        Assert.All(draws, static command => Assert.True(command.Y > 10f, $"Expected JPEG shape text to use the shape-derived first baseline, but Y was {command.Y}."));
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWithShapeInsideRightToLeft_AnchorsLinesToRightEdge()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="140" viewBox="0 0 220 140">
              <defs>
                <rect id="shape" x="40" y="20" width="26" height="105" />
              </defs>
              <text id="shape-text" font-size="20" shape-inside="url(#shape)" direction="rtl" unicode-bidi="embed">A A</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var draws = retainedModel
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("shape-text")
            .Where(static command => !string.IsNullOrWhiteSpace(command.Text))
            .ToList();

        Assert.Equal(new[] { "A", "A" }, draws.Select(static command => StripBidiControls(command.Text)).ToArray());
        Assert.All(draws, static command => Assert.InRange(command.X, 40f, 66f));
        Assert.True(draws[0].X > 40f, $"Expected RTL shape-inside line to anchor against the right edge, but X was {draws[0].X}.");
        Assert.Equal(draws[0].X, draws[1].X, 3);
        Assert.True(draws[1].Y > draws[0].Y + 10f, $"Expected RTL shape-inside text to wrap inside the rectangle, but Y was {draws[1].Y} vs {draws[0].Y}.");

        static string StripBidiControls(string text)
        {
            return text
                .Replace("\u202A", string.Empty, StringComparison.Ordinal)
                .Replace("\u202B", string.Empty, StringComparison.Ordinal)
                .Replace("\u202C", string.Empty, StringComparison.Ordinal)
                .Replace("\u202D", string.Empty, StringComparison.Ordinal)
                .Replace("\u202E", string.Empty, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWithShapeSubtract_UsesRemainingLineFragment()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="120" viewBox="0 0 220 120">
              <defs>
                <rect id="shape" x="10" y="20" width="100" height="80" />
                <rect id="subtract" x="10" y="20" width="45" height="36" />
              </defs>
              <text id="shape-text" font-size="20" shape-inside="url(#shape)" shape-subtract="url(#subtract)">A B</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var draws = retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("shape-text").ToList();

        Assert.NotEmpty(draws);
        Assert.All(draws, static command => Assert.True(command.X >= 55f, $"Expected shape-subtract to move the line start after the exclusion rectangle, but X was {command.X}."));
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWithMultipleShapeSubtract_UsesWidestOpenFragment()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="120" viewBox="0 0 220 120">
              <defs>
                <rect id="shape" x="10" y="20" width="140" height="80" />
                <rect id="left" x="10" y="20" width="50" height="38" />
                <rect id="right" x="90" y="20" width="60" height="38" />
              </defs>
              <text id="shape-text" font-size="20" shape-inside="url(#shape)" shape-subtract="url(#left) url(#right)">A</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draw = Assert.Single(retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("shape-text"));
        Assert.Equal("A", draw.Text);
        Assert.True(draw.X >= 60f && draw.X <= 90f, $"Expected multiple shape-subtract regions to leave the center fragment, but X was {draw.X}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWithMultipleShapeInside_FlowsOverflowIntoNextShape()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="120" viewBox="0 0 220 120">
              <defs>
                <rect id="first" x="10" y="20" width="24" height="42" />
                <rect id="second" x="90" y="20" width="80" height="42" />
              </defs>
              <text id="shape-text" font-size="20" shape-inside="url(#first) url(#second)">A B</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draws = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("shape-text")
            .Where(static command => !string.IsNullOrWhiteSpace(command.Text))
            .ToList();

        Assert.Equal(new[] { "A", "B" }, draws.Select(static command => command.Text).ToArray());
        Assert.Equal(10f, draws[0].X, 3);
        Assert.Equal(90f, draws[1].X, 3);
        Assert.Equal(draws[0].Y, draws[1].Y, 3);
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWithNonRectangularShapeInside_UsesSampledLineFragments()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="160" height="140" viewBox="0 0 160 140">
              <defs>
                <path id="shape" d="M100 20 L130 20 L60 120 L20 120 Z" />
              </defs>
              <text id="shape-text" font-size="24" shape-inside="url(#shape)">W W</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draws = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("shape-text")
            .Where(static command => !string.IsNullOrWhiteSpace(command.Text))
            .ToList();

        Assert.Equal(new[] { "W", "W" }, draws.Select(static command => command.Text).ToArray());
        Assert.True(Math.Abs(draws[0].X - draws[1].X) > 10f, $"Expected sampled non-rectangular shape lines to use different line starts, but X values were {draws[0].X} and {draws[1].X}.");
        Assert.True(draws[1].Y > draws[0].Y + 10f, $"Expected non-rectangular shape-inside text to wrap to a later row, but Y values were {draws[0].Y} and {draws[1].Y}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWithFullWidthShapeSubtract_FlowsTextBelowExclusion()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="130" viewBox="0 0 220 130">
              <defs>
                <rect id="shape" x="10" y="20" width="100" height="90" />
                <rect id="subtract" x="10" y="20" width="100" height="38" />
              </defs>
              <text id="shape-text" font-size="20" shape-inside="url(#shape)" shape-subtract="url(#subtract)">A B</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draws = retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("shape-text").ToList();

        var visibleDraws = draws.Where(static command => !string.IsNullOrWhiteSpace(command.Text)).ToList();
        Assert.Equal(new[] { "A", "B" }, visibleDraws.Select(static command => command.Text).ToArray());
        Assert.Equal(10f, visibleDraws[0].X, 3);
        Assert.True(visibleDraws[1].X > visibleDraws[0].X, $"Expected the second word to remain on the available line fragment, but X was {visibleDraws[1].X} vs {visibleDraws[0].X}.");
        Assert.All(draws, static command => Assert.True(command.Y > 58f, $"Expected text to flow below the full-width exclusion, but Y was {command.Y}."));
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWithShapeInside_StopsAtShapeBottom()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="100" viewBox="0 0 220 100">
              <defs>
                <rect id="shape" x="40" y="20" width="26" height="42" />
              </defs>
              <text id="shape-text" font-size="20" shape-inside="url(#shape)">A B C D</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var draws = retainedModel!.FindCommandsBySourceElementId<DrawTextCanvasCommand>("shape-text").ToList();

        Assert.NotEmpty(draws);
        var occupiedLineCount = draws.Select(static command => MathF.Round(command.Y, 3)).Distinct().Count();
        Assert.True(occupiedLineCount <= 2, $"Expected shape-inside layout to stop at the shape bottom, but rendered {occupiedLineCount} lines.");
        Assert.All(draws, static command => Assert.True(command.Y <= 62f, $"Expected no shape-inside baseline below the rectangle bottom, but Y was {command.Y}."));
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWithShapeInsideAuto_UsesInlineOverflowPath()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="100" viewBox="0 0 220 100">
              <text id="shape-auto" x="10" y="50" font-size="20" inline-size="48" shape-inside="auto" text-overflow="ellipsis">ABCDEFG</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Single(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var draws = retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("shape-auto").ToList();
        Assert.Contains(draws, static command => command.Text == "\u2026");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWithDescendantShapeInside_FallsBack()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="100" viewBox="0 0 220 100">
              <defs>
                <rect id="shape" width="80" height="40" />
              </defs>
              <text id="root" x="10" y="50" font-size="20" inline-size="48" text-overflow="ellipsis"><tspan id="child-shape" shape-inside="url(#shape)">ABCDEFG</tspan></text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var draw = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("child-shape"));
        Assert.Equal("ABCDEFG", draw.Text);
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWithPositionedDescendant_FallsBackToPositionedLayout()
    {
        const string overflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="160" height="110" viewBox="0 0 160 110">
              <text id="root" x="10" y="25" font-size="20" inline-size="28" text-overflow="ellipsis">A<tspan id="positioned" x="90" y="70">B</tspan>C</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(overflowSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        Assert.DoesNotContain(retainedModel.FindCommands<DrawTextCanvasCommand>(), static command => command.Text == "\u2026");

        var positioned = Assert.Single(retainedModel.FindCommandsBySourceElementId<DrawTextCanvasCommand>("positioned"));
        Assert.Equal("B", positioned.Text);
        Assert.Equal(90f, positioned.X, 3);
        Assert.Equal(70f, positioned.Y, 3);

        var rootTexts = retainedModel
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("root")
            .Select(static command => command.Text)
            .ToList();
        Assert.Contains("A", rootTexts);
        Assert.Contains("C", rootTexts);
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
    public void RetainedSceneGraph_WhiteSpacePreserveSpacesShorthandPreservesStaticRuns()
    {
        const string whiteSpaceSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="50" viewBox="0 0 180 50">
              <text id="preserve-spaces" x="10" y="25" font-size="12" style="white-space: preserve-spaces wrap">A   B</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(whiteSpaceSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var renderedTexts = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("preserve-spaces")
            .Select(static cmd => cmd.Text)
            .ToList();

        Assert.Contains("A   B", renderedTexts);
    }

    [Fact]
    public void RetainedSceneGraph_WhiteSpaceCollapseDiscardRemovesDocumentWhitespace()
    {
        const string whiteSpaceSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="50" viewBox="0 0 180 50">
              <text id="discard" x="10" y="25" font-size="12" style="white-space-collapse: discard; text-wrap-mode: wrap">A   B</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(whiteSpaceSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var renderedTexts = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("discard")
            .Select(static cmd => cmd.Text)
            .ToList();

        Assert.Contains("AB", renderedTexts);
        Assert.DoesNotContain("A B", renderedTexts);
    }

    [Fact]
    public void RetainedSceneGraph_WhiteSpaceTrimDiscardInnerTrimsPreservedEdges()
    {
        const string whiteSpaceSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="180" height="50" viewBox="0 0 180 50">
              <text id="trim" x="10" y="25" font-size="12" style="white-space: preserve nowrap discard-inner">   A   </text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(whiteSpaceSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var renderedTexts = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("trim")
            .Select(static cmd => cmd.Text)
            .ToList();

        Assert.Contains("A", renderedTexts);
        Assert.DoesNotContain("   A   ", renderedTexts);
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
              <text id="spaced-length" x="20" y="100" font-family="Noto Sans" font-size="48" textLength="150">Text</text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(textLengthSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        AssertCompilationStrategy(scene!, "spaced-length", SvgSceneCompilationStrategy.DirectRetained);
        Assert.True(scene.TryGetNodeById("spaced-length", out var textNode));
        Assert.NotNull(textNode?.LocalModel);

        var blobs = textNode!.LocalModel!.FindCommands<DrawTextBlobCanvasCommand>().ToArray();
        Assert.Single(blobs);
        Assert.Equal("Text", blobs[0].TextBlob?.Text);
        var positions = Assert.IsType<SKPoint[]>(blobs[0].TextBlob?.Points);

        Assert.Equal(4, positions.Length);
        Assert.Equal(20f, positions[0].X, 1);
        Assert.Equal(100f, positions[0].Y, 1);
        Assert.True(positions[^1].X > 120f, $"Expected textLength spacing to spread the glyph origins, but got final X={positions[^1].X}.");
        Assert.Empty(textNode.LocalModel.FindCommands<DrawTextCanvasCommand>());
    }

    [Fact]
    public void RetainedSceneGraph_TextLengthSpacingAndGlyphs_RecordsScaledTextCommand()
    {
        const string textLengthSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="160" viewBox="0 0 220 160">
              <text id="scaled-length" x="20" y="90" font-family="Noto Sans" font-size="36" textLength="150" lengthAdjust="spacingAndGlyphs">Text</text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(textLengthSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        AssertCompilationStrategy(scene!, "scaled-length", SvgSceneCompilationStrategy.DirectRetained);
        Assert.True(scene.TryGetNodeById("scaled-length", out var textNode));
        Assert.NotNull(textNode?.LocalModel);

        var draws = textNode!.LocalModel!.FindCommands<DrawTextCanvasCommand>().ToArray();
        Assert.True(
            draws.Length == 1,
            $"Expected one scaled text command, but found {draws.Length}. Commands: {string.Join(", ", textNode.LocalModel.Commands!.Select(static command => command.GetType().Name))}");

        var draw = draws[0];
        Assert.Equal("Text", draw.Text);
        Assert.True(draw.Font?.ScaleX > 1.1f, $"Expected scaled text command font, but got {draw.Font?.ScaleX}.");
        Assert.Empty(textNode.LocalModel.FindCommands<SetMatrixCanvasCommand>());
        Assert.Empty(textNode.LocalModel.FindCommands<DrawTextBlobCanvasCommand>());
    }

    [Fact]
    public void RetainedSceneGraph_LetterAndWordSpacing_RecordsPositionedTextBlob()
    {
        const string spacedTextSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="120" viewBox="0 0 220 120">
              <text id="spaced-text" x="20" y="70" font-family="Noto Sans" font-size="28" letter-spacing="4" word-spacing="12">A B</text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(spacedTextSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        AssertCompilationStrategy(scene!, "spaced-text", SvgSceneCompilationStrategy.DirectRetained);
        Assert.True(scene.TryGetNodeById("spaced-text", out var textNode));
        Assert.NotNull(textNode?.LocalModel);

        var blobs = textNode!.LocalModel!.FindCommands<DrawTextBlobCanvasCommand>().ToArray();
        Assert.Single(blobs);
        Assert.Equal("A B", blobs[0].TextBlob?.Text);
        var points = Assert.IsType<SKPoint[]>(blobs[0].TextBlob?.Points);
        Assert.Equal(3, points.Length);
        Assert.Equal(20f, points[0].X, 1);
        Assert.True(points[1].X > points[0].X + 10f, $"Expected letter spacing after A, but points were {string.Join(", ", points.Select(static point => point.X.ToString(CultureInfo.InvariantCulture)))}.");
        Assert.True(points[2].X > points[1].X + 20f, $"Expected word spacing before B, but points were {string.Join(", ", points.Select(static point => point.X.ToString(CultureInfo.InvariantCulture)))}.");
        Assert.Empty(textNode.LocalModel.FindCommands<DrawTextCanvasCommand>());
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeWithTextLength_DoesNotWrapAdjustedGlyphs()
    {
        const string textLengthSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 200 200">
              <text id="text-length" x="20" y="100" font-family="Noto Sans" font-size="48" inline-size="40" textLength="150">Text</text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(textLengthSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var draws = retainedModel
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("text-length")
            .Where(static cmd => cmd.Y == 100f)
            .OrderBy(static cmd => cmd.X)
            .ToArray();

        Assert.Equal(4, draws.Length);
        Assert.All(draws, static command => Assert.Equal(100f, command.Y, 3));
        Assert.Equal(20f, draws[0].X, 1);
        Assert.True(draws[^1].X > 120f, $"Expected inline-size plus textLength to keep textLength spacing instead of wrapping, but final X was {draws[^1].X}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeTextLength_WrapsBeforeSpacingAdjustment()
    {
        const string textLengthSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="180" viewBox="0 0 220 180">
              <text id="wrapped-length" x="20" y="70" font-family="Noto Sans" font-size="20" inline-size="30" textLength="120">AB CD</text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(textLengthSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        var glyphs = retainedModel
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("wrapped-length")
            .Where(static command => command.Text is "A" or "B" or "C" or "D")
            .GroupBy(static command => command.Text, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);

        Assert.True(glyphs.TryGetValue("A", out var a), "Expected wrapped textLength glyph A.");
        Assert.True(glyphs.TryGetValue("B", out var b), "Expected wrapped textLength glyph B.");
        Assert.True(glyphs.TryGetValue("C", out var c), "Expected wrapped textLength glyph C.");
        Assert.True(glyphs.TryGetValue("D", out var d), "Expected wrapped textLength glyph D.");
        Assert.Equal(a.Y, b.Y, 3);
        Assert.Equal(c.Y, d.Y, 3);
        Assert.True(c.Y > a.Y + 8f, $"Expected textLength layout to wrap before spacing adjustment, but A was at {a.Y} and C was at {c.Y}.");
        Assert.True(b.X > a.X + 20f, $"Expected textLength spacing to spread first wrapped line, but A was at {a.X} and B was at {b.X}.");
        Assert.True(d.X > c.X + 20f, $"Expected textLength spacing to spread second wrapped line, but C was at {c.X} and D was at {d.X}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeTextLengthSpacingAndGlyphs_WrapsAndScalesLines()
    {
        const string textLengthSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="180" viewBox="0 0 220 180">
              <text id="wrapped-length" x="20" y="70" font-family="Noto Sans" font-size="20" inline-size="30" textLength="120" lengthAdjust="spacingAndGlyphs">AB CD</text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(textLengthSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var glyphs = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("wrapped-length")
            .Where(static command => command.Text is "A" or "B" or "C" or "D")
            .GroupBy(static command => command.Text, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        var scaleMatrices = retainedModel
            .FindCommands<SetMatrixCanvasCommand>()
            .Select(static command => command.DeltaMatrix)
            .Where(static matrix => matrix.ScaleX > 1.1f)
            .ToArray();

        Assert.NotEmpty(scaleMatrices);
        Assert.True(glyphs.TryGetValue("A", out var a), "Expected scaled wrapped textLength glyph A.");
        Assert.True(glyphs.TryGetValue("B", out var b), "Expected scaled wrapped textLength glyph B.");
        Assert.True(glyphs.TryGetValue("C", out var c), "Expected scaled wrapped textLength glyph C.");
        Assert.True(glyphs.TryGetValue("D", out var d), "Expected scaled wrapped textLength glyph D.");
        Assert.Equal(a.Y, b.Y, 3);
        Assert.Equal(c.Y, d.Y, 3);
        Assert.True(c.Y > a.Y + 8f, $"Expected spacingAndGlyphs layout to wrap before scaling, but A was at {a.Y} and C was at {c.Y}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeTextLengthWithRelativePositionedDescendant_WrapsShiftedLine()
    {
        const string textLengthSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="180" viewBox="0 0 220 180">
              <text id="wrapped-length" x="20" y="70" font-family="Noto Sans" font-size="20" inline-size="30" textLength="120">AB <tspan id="shifted-line" dx="8">CD</tspan></text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(textLengthSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var rootGlyphs = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("wrapped-length")
            .Where(static command => command.Text is "A" or "B")
            .ToDictionary(static command => command.Text, static command => command, StringComparer.Ordinal);
        var shiftedGlyphs = retainedModel
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("shifted-line")
            .Where(static command => command.Text is "C" or "D")
            .ToDictionary(static command => command.Text, static command => command, StringComparer.Ordinal);

        Assert.True(rootGlyphs.TryGetValue("A", out var a), "Expected wrapped textLength glyph A.");
        Assert.True(rootGlyphs.TryGetValue("B", out var b), "Expected wrapped textLength glyph B.");
        Assert.True(shiftedGlyphs.TryGetValue("C", out var c), "Expected shifted wrapped textLength glyph C.");
        Assert.True(shiftedGlyphs.TryGetValue("D", out var d), "Expected shifted wrapped textLength glyph D.");
        Assert.Equal(a.Y, b.Y, 3);
        Assert.Equal(c.Y, d.Y, 3);
        Assert.True(c.Y > a.Y + 8f, $"Expected shifted descendant to remain on the wrapped second line, but A was at {a.Y} and C was at {c.Y}.");
        Assert.True(c.X > a.X, $"Expected descendant dx to shift C on its wrapped line, but A was at {a.X} and C was at {c.X}.");
        Assert.True(d.X > c.X + 10f, $"Expected spacing after shifted C to continue through textLength layout, but C was at {c.X} and D was at {d.X}.");
    }

    [Fact]
    public void RetainedSceneGraph_InlineSizeTextLengthWithRelativePositionedDescendant_UsesFlattenedSpacing()
    {
        const string textLengthSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="180" viewBox="0 0 220 180">
              <text id="flattened" x="20" y="90" font-family="Noto Sans" font-size="36" inline-size="34" textLength="150">A<tspan id="shifted" dx="10">B</tspan>C</text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(textLengthSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        Assert.Empty(retainedModel!.FindCommands<ClipRectCanvasCommand>());
        Assert.DoesNotContain(retainedModel.FindCommands<DrawTextCanvasCommand>(), static command => command.Text == "\u2026");

        var glyphs = GetPositionedGlyphPoints(retainedModel, "A", "B", "C");

        Assert.True(glyphs.TryGetValue("A", out var a), "Expected flattened textLength glyph A.");
        Assert.True(glyphs.TryGetValue("B", out var b), "Expected flattened textLength glyph B.");
        Assert.True(glyphs.TryGetValue("C", out var c), "Expected flattened textLength glyph C.");
        Assert.Equal(a.Y, b.Y, 3);
        Assert.Equal(a.Y, c.Y, 3);
        Assert.True(b.X > a.X + 20f, $"Expected inline-size plus textLength to keep dx-positioned B in flattened spacing, but A was {a} and B was {b}.");
        Assert.True(c.X > b.X + 20f, $"Expected text after dx-positioned B to keep flattened textLength spacing, but B was {b} and C was {c}.");
    }

    [Fact]
    public void RetainedSceneGraph_TextLengthWithPositionedDescendant_IntegratesFlattenedSpacing()
    {
        const string textLengthSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="220" height="180" viewBox="0 0 220 180">
              <text id="flattened" x="20" y="90" font-family="Noto Sans" font-size="36" textLength="150">A<tspan id="shifted" dx="10">B</tspan>C</text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(textLengthSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        Assert.NotNull(retainedModel);

        var glyphs = GetPositionedGlyphPoints(retainedModel!, "A", "B", "C");

        Assert.True(glyphs.TryGetValue("A", out var a), "Expected flattened textLength glyph A.");
        Assert.True(glyphs.TryGetValue("B", out var b), "Expected flattened textLength glyph B.");
        Assert.True(glyphs.TryGetValue("C", out var c), "Expected flattened textLength glyph C.");
        Assert.Equal(a.Y, b.Y, 3);
        Assert.Equal(a.Y, c.Y, 3);
        Assert.True(b.X > a.X + 20f, $"Expected dx-positioned descendant to participate in flattened textLength spacing, but A was {a} and B was {b}.");
        Assert.True(c.X > b.X + 20f, $"Expected text after positioned descendant to continue through flattened textLength spacing, but B was {b} and C was {c}.");
    }

    [Fact]
    public void RetainedSceneGraph_SharedTextLayoutEngine_CombinesBidiShapeWrappingAndStretchPath()
    {
        const string sharedLayoutSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="360" height="240" viewBox="0 0 360 240">
              <defs>
                <rect id="shape" x="24" y="24" width="116" height="82" />
                <rect id="subtract" x="24" y="24" width="54" height="34" />
                <path id="stretch-curve" d="M178 172 C220 112 286 230 334 170" />
                <filter id="stretch-shadow" x="-20%" y="-30%" width="140%" height="160%">
                  <feDropShadow dx="2" dy="3" stdDeviation="1" flood-color="#0f172a" flood-opacity="0.35" />
                </filter>
              </defs>
              <text id="shared-wrap"
                    font-family="Noto Sans, Arial, sans-serif"
                    font-size="18"
                    fill="#111827"
                    shape-inside="url(#shape)"
                    shape-subtract="url(#subtract)"
                    direction="rtl"
                    unicode-bidi="embed"
                    line-break="anywhere">A-B &#x05D0;&#x05D1; C</text>
              <text id="shared-stretch"
                    font-family="Noto Sans, Arial, sans-serif"
                    font-size="24"
                    fill="#2563eb"
                    stroke="#0f172a"
                    stroke-width="0.4"
                    text-decoration="underline"
                    filter="url(#stretch-shadow)">
                <textPath id="shared-stretch-path" href="#stretch-curve" method="stretch" textLength="132" lengthAdjust="spacingAndGlyphs">stretch path</textPath>
              </text>
            </svg>
            """;

        using var svg = new SKSvg();
        SetTypefaceProviders(svg.Settings);
        svg.FromSvg(sharedLayoutSvg);

        var retainedModel = svg.CreateRetainedSceneGraphModel();
        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(svg.Picture);
        Assert.NotNull(retainedModel);
        Assert.NotNull(retainedPicture);
        AssertPicturesEqual(svg, svg.Picture!, retainedPicture!);

        var wrappedDraws = retainedModel!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("shared-wrap")
            .Where(static command => !string.IsNullOrWhiteSpace(StripBidiControls(command.Text)))
            .ToList();
        var wrappedText = string.Concat(wrappedDraws.Select(static command => StripBidiControls(command.Text)));
        Assert.Contains("A", wrappedText, StringComparison.Ordinal);
        Assert.Contains("B", wrappedText, StringComparison.Ordinal);
        Assert.Contains("C", wrappedText, StringComparison.Ordinal);
        Assert.Contains("\u05D0", wrappedText, StringComparison.Ordinal);
        Assert.Contains("\u05D1", wrappedText, StringComparison.Ordinal);
        Assert.All(wrappedDraws, static command =>
        {
            Assert.True(float.IsFinite(command.X), $"Expected finite shared-wrap X, but got {command.X}.");
            Assert.True(float.IsFinite(command.Y), $"Expected finite shared-wrap Y, but got {command.Y}.");
        });
        Assert.True(
            wrappedDraws.Select(static command => MathF.Round(command.Y, 2)).Distinct().Count() > 1,
            $"Expected shared wrapped text to span multiple shape lines, but draws were: {string.Join(", ", wrappedDraws.Select(static command => $"[{command.Text}]@{command.X:F2},{command.Y:F2}"))}.");

        var stretchPaths = retainedModel
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("shared-stretch-path")
            .ToList();
        Assert.NotEmpty(stretchPaths);
        Assert.All(stretchPaths, static command =>
        {
            Assert.NotNull(command.Path);
            Assert.False(command.Path!.IsEmpty);
            Assert.True(float.IsFinite(command.Path.Bounds.Left), $"Expected finite stretch path left bound, but bounds were {command.Path.Bounds}.");
            Assert.True(float.IsFinite(command.Path.Bounds.Top), $"Expected finite stretch path top bound, but bounds were {command.Path.Bounds}.");
            Assert.True(float.IsFinite(command.Path.Bounds.Right), $"Expected finite stretch path right bound, but bounds were {command.Path.Bounds}.");
            Assert.True(float.IsFinite(command.Path.Bounds.Bottom), $"Expected finite stretch path bottom bound, but bounds were {command.Path.Bounds}.");
        });

        static string StripBidiControls(string text)
        {
            return text
                .Replace("\u061C", string.Empty, StringComparison.Ordinal)
                .Replace("\u200E", string.Empty, StringComparison.Ordinal)
                .Replace("\u200F", string.Empty, StringComparison.Ordinal)
                .Replace("\u202A", string.Empty, StringComparison.Ordinal)
                .Replace("\u202B", string.Empty, StringComparison.Ordinal)
                .Replace("\u202C", string.Empty, StringComparison.Ordinal)
                .Replace("\u202D", string.Empty, StringComparison.Ordinal)
                .Replace("\u202E", string.Empty, StringComparison.Ordinal)
                .Replace("\u2066", string.Empty, StringComparison.Ordinal)
                .Replace("\u2067", string.Empty, StringComparison.Ordinal)
                .Replace("\u2068", string.Empty, StringComparison.Ordinal)
                .Replace("\u2069", string.Empty, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RetainedSceneGraph_LengthAdjustSpacingAndGlyphs_UsesScaledTextCommandFont()
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

        var scaledCommands = retainedModel!
            .FindCommands<DrawTextCanvasCommand>()
            .Where(static command => command.Font?.ScaleX > 1.1f)
            .ToArray();

        Assert.NotEmpty(scaledCommands);
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
    public void RetainedSceneGraph_ResolvesExternalFilterReference()
    {
        var previousResolveExternalElements = SvgDocument.ResolveExternalElements;
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            SvgDocument.ResolveExternalElements = ExternalType.Local | ExternalType.Remote;
            var filtersPath = Path.Combine(tempDirectory.FullName, "filters.svg");
            File.WriteAllText(filtersPath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
                  <defs>
                    <filter id="external-filter" filterUnits="userSpaceOnUse" x="1" y="2" width="40" height="30">
                      <feGaussianBlur stdDeviation="1" />
                    </filter>
                  </defs>
                </svg>
                """);

            var sourcePath = Path.Combine(tempDirectory.FullName, "source.svg");
            File.WriteAllText(sourcePath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64">
                  <rect id="filtered-target" x="4" y="4" width="16" height="16" fill="#3366cc" filter="filters.svg#external-filter" />
                </svg>
                """);

            using var svg = new SKSvg();
            svg.Load(
                sourcePath,
                new SvgParameters(
                    null,
                    null,
                    null,
                    new SvgDocumentLoadOptions
                    {
                        ExternalResources = SvgExternalResourcePolicy.SameOrigin
                    }));

            var scene = svg.RetainedSceneGraph;
            Assert.NotNull(scene);
            Assert.True(scene!.TryGetNodeById("filtered-target", out var targetNode));
            Assert.NotNull(targetNode);
            Assert.NotNull(targetNode!.Filter);
            Assert.NotNull(targetNode.FilterClip);
            Assert.Equal(1f, targetNode.FilterClip.Value.Left, 3);
            Assert.Equal(2f, targetNode.FilterClip.Value.Top, 3);
            Assert.Equal(41f, targetNode.FilterClip.Value.Right, 3);
            Assert.Equal(32f, targetNode.FilterClip.Value.Bottom, 3);
        }
        finally
        {
            SvgDocument.ResolveExternalElements = previousResolveExternalElements;
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void RetainedSceneGraph_ResolvesExternalLinkedFilterReference()
    {
        var previousResolveExternalElements = SvgDocument.ResolveExternalElements;
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            SvgDocument.ResolveExternalElements = ExternalType.Local | ExternalType.Remote;
            var filtersPath = Path.Combine(tempDirectory.FullName, "filters.svg");
            File.WriteAllText(filtersPath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
                  <defs>
                    <filter id="external-filter" filterUnits="userSpaceOnUse" x="3" y="4" width="42" height="28">
                      <feGaussianBlur stdDeviation="1" />
                    </filter>
                  </defs>
                </svg>
                """);

            var sourcePath = Path.Combine(tempDirectory.FullName, "source.svg");
            File.WriteAllText(sourcePath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64">
                  <defs>
                    <filter id="alias-filter" href="filters.svg#external-filter" />
                  </defs>
                  <rect id="filtered-target" x="4" y="4" width="16" height="16" fill="#3366cc" filter="url(#alias-filter)" />
                </svg>
                """);

            using var svg = new SKSvg();
            svg.Load(
                sourcePath,
                new SvgParameters(
                    null,
                    null,
                    null,
                    new SvgDocumentLoadOptions
                    {
                        ExternalResources = SvgExternalResourcePolicy.SameOrigin
                    }));

            var scene = svg.RetainedSceneGraph;
            Assert.NotNull(scene);
            Assert.True(scene!.TryGetNodeById("filtered-target", out var targetNode));
            Assert.NotNull(targetNode);
            Assert.NotNull(targetNode!.Filter);
            Assert.NotNull(targetNode.FilterClip);
            Assert.Equal(3f, targetNode.FilterClip.Value.Left, 3);
            Assert.Equal(4f, targetNode.FilterClip.Value.Top, 3);
            Assert.Equal(45f, targetNode.FilterClip.Value.Right, 3);
            Assert.Equal(32f, targetNode.FilterClip.Value.Bottom, 3);
        }
        finally
        {
            SvgDocument.ResolveExternalElements = previousResolveExternalElements;
            tempDirectory.Delete(recursive: true);
        }
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
    public void RetainedSceneGraph_AssignsResourceKeysForAbsoluteSameDocumentReferences()
    {
        var baseUri = new Uri("https://example.test/assets/source.svg");
        var document = SvgService.FromSvg($$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="20">
              <defs>
                <filter id="filter-a" x="-25%" y="-25%" width="150%" height="150%">
                  <feGaussianBlur stdDeviation="1" />
                </filter>
              </defs>
              <rect id="filtered-target" x="4" y="4" width="16" height="12" fill="#3366cc" />
            </svg>
            """);
        document!.BaseUri = baseUri;
        var target = Assert.IsType<SvgRectangle>(document.GetElementById("filtered-target"));
        target.Filter = new Uri(baseUri.AbsoluteUri + "#filter-a");

        using var svg = new SKSvg();
        svg.FromSvgDocument(document);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("filtered-target", out var filteredNode));
        Assert.False(string.IsNullOrWhiteSpace(filteredNode!.FilterResourceKey));
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
    public void Svg11_XLinkHrefOnly_StillResolvesUse()
    {
        const string xlinkHrefSvg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="20" height="20">
              <defs>
                <rect id="template" width="10" height="10" fill="red" />
              </defs>
              <use id="target" xlink:href="#template" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(xlinkHrefSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var useNode));
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
    public void RetainedSceneGraph_SymbolRefXDoesNotAdjustOmittedRefY()
    {
        const string symbolRefSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="80">
              <defs>
                <symbol id="icon" viewBox="10 20 20 20" refX="10">
                  <rect id="symbol-shape" x="10" y="20" width="20" height="20" fill="#ff0000" />
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
        Assert.Equal(new SkiaColor(0xff, 0x00, 0x00, 0xff), bitmap.GetPixel(35, 35));
        Assert.Equal(new SkiaColor(0x00, 0x00, 0x00, 0x00), bitmap.GetPixel(35, 55));
    }

    [Fact]
    public void RetainedSceneGraph_SymbolRefYDoesNotAdjustOmittedRefX()
    {
        const string symbolRefSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="80">
              <defs>
                <symbol id="icon" viewBox="10 20 20 20" refY="20">
                  <rect id="symbol-shape" x="10" y="20" width="20" height="20" fill="#ff0000" />
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
        Assert.Equal(new SkiaColor(0xff, 0x00, 0x00, 0xff), bitmap.GetPixel(35, 35));
        Assert.Equal(new SkiaColor(0x00, 0x00, 0x00, 0x00), bitmap.GetPixel(55, 35));
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

    [Theory]
    [InlineData("line", """<line id="target" x1="10" y1="10" x2="210" y2="10" />""")]
    [InlineData("polyline", """<polyline id="target" points="10,10 110,10 110,110" />""")]
    [InlineData("polygon", """<polygon id="target" points="10,10 60,10 60,60 10,60" />""")]
    [InlineData("rect", """<rect id="target" x="10" y="10" width="50" height="50" />""")]
    [InlineData("circle", """<circle id="target" cx="60" cy="60" r="31.8309886" />""")]
    [InlineData("ellipse", """<ellipse id="target" cx="60" cy="60" rx="31.8309886" ry="31.8309886" />""")]
    public void RetainedSceneGraph_NormalizesDashDistancesWithPathLengthOnBasicShapes(string _, string shapeMarkup)
    {
        var pathLengthDashSvg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="240" height="140">
              {{shapeMarkup.Replace("/>", """
                    pathLength="100"
                    fill="none"
                    stroke="black"
                    stroke-width="2"
                    stroke-dasharray="10 5"
                    stroke-dashoffset="20" />
                """, StringComparison.Ordinal)}}
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(pathLengthDashSvg);

        var command = Assert.Single(svg.Model!
            .FindCommandsBySourceElementId<DrawPathCanvasCommand>("target"),
            static command => command.Paint?.Style == SKPaintStyle.Stroke);
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
    [InlineData("marker-start:none")]
    [InlineData("marker:none")]
    public void RetainedSceneGraph_CssMarkerNoneSuppressesPresentationMarkers(string markerStyle)
    {
        var markerSvg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="20">
              <defs>
                <marker id="marker" markerWidth="4" markerHeight="4" refX="2" refY="2" markerUnits="userSpaceOnUse">
                  <circle id="marker-dot" cx="2" cy="2" r="2" fill="red" />
                </marker>
              </defs>
              <path id="target"
                    d="M10 10 L50 10"
                    fill="none"
                    stroke="black"
                    marker-start="url(#marker)"
                    style="{{markerStyle}}" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(markerSvg);

        var markerNodes = GetTargetMarkerNodes(svg.RetainedSceneGraph, "target");

        Assert.Empty(markerNodes);
    }

    [Fact]
    public void RetainedSceneGraph_DoesNotReuseComputedStylesAcrossTemporaryUseParents()
    {
        const string useMarkerInheritanceSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="60" height="44">
              <defs>
                <marker id="red-marker" markerWidth="8" markerHeight="8" refX="4" refY="4" markerUnits="userSpaceOnUse">
                  <rect x="0" y="0" width="8" height="8" fill="#ff0000" />
                </marker>
                <marker id="blue-marker" markerWidth="8" markerHeight="8" refX="4" refY="4" markerUnits="userSpaceOnUse">
                  <rect x="0" y="0" width="8" height="8" fill="#0000ff" />
                </marker>
                <path id="segment" d="M10 10 L34 10" fill="none" stroke="black" stroke-width="1" />
              </defs>
              <use id="red-use" href="#segment" style="marker-start:url(#red-marker)" />
              <use id="blue-use" href="#segment" y="20" style="marker-start:url(#blue-marker)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(useMarkerInheritanceSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);

        Assert.Equal(SkiaColors.Red, bitmap.GetPixel(10, 10));
        Assert.Equal(SkiaColors.Blue, bitmap.GetPixel(10, 30));
    }

    [Fact]
    public void RetainedSceneGraph_UseScopedCssIgnoresOriginalAncestorSelectors()
    {
        const string useScopedCssSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="24">
              <style>
                rect.target { fill: #00ff00; }
                .ancestor > rect { fill: #ff0000; }
              </style>
              <defs>
                <g class="ancestor">
                  <rect id="target" class="target" width="20" height="20" />
                </g>
              </defs>
              <use href="#target" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(useScopedCssSvg);

        using var bitmap = ToBitmap(svg, svg.Picture!);

        Assert.Equal(SkiaColors.Lime, bitmap.GetPixel(10, 10));
    }

    [Fact]
    public void RetainedSceneGraph_UseScopedCssPreservesInlineAndRestoresFollowingStyles()
    {
        const string useScopedCssSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="24">
              <style>
                .after-use { fill: #123456; }
                rect.target { fill: #ff0000; }
              </style>
              <defs>
                <rect id="target" class="target" width="20" height="20" style="fill:#00ff00" />
              </defs>
              <use href="#target" />
              <rect class="after-use" x="32" width="20" height="20" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(useScopedCssSvg);

        using var bitmap = ToBitmap(svg, svg.Picture!);

        Assert.Equal(SkiaColors.Lime, bitmap.GetPixel(10, 10));
        Assert.Equal(new SkiaColor(0x12, 0x34, 0x56, 0xff), bitmap.GetPixel(42, 10));
    }

    [Fact]
    public void RetainedSceneGraph_UseScopedCssResolvesUseInheritedCustomProperties()
    {
        const string useScopedCssSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="32" height="24">
              <style>
                use { --instance-fill: #00ff00; }
                rect.target { fill: var(--instance-fill); }
              </style>
              <defs>
                <rect id="target" class="target" width="20" height="20" fill="#ff0000" />
              </defs>
              <use href="#target" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(useScopedCssSvg);

        using var bitmap = ToBitmap(svg, svg.Picture!);

        Assert.Equal(SkiaColors.Lime, bitmap.GetPixel(10, 10));
    }

    [Fact]
    public void RetainedSceneGraph_ClipPathUseEvaluatesInheritedClipRuleFromUse()
    {
        const string clipUseSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24">
              <defs>
                <path id="donut" d="M0 0H20V20H0Z M5 5H15V15H5Z" />
                <clipPath id="clip">
                  <use id="clip-use" href="#donut" style="clip-rule:evenodd" />
                </clipPath>
              </defs>
              <rect width="20" height="20" fill="#ff0000" clip-path="url(#clip)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(clipUseSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);

        Assert.Equal(new SkiaColor(0xff, 0x00, 0x00, 0xff), bitmap.GetPixel(2, 2));
        Assert.Equal(new SkiaColor(0x00, 0x00, 0x00, 0x00), bitmap.GetPixel(10, 10));
    }

    [Fact]
    public void RetainedSceneGraph_ClipPathUseIgnoresOriginalParentVisibility()
    {
        const string clipUseSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24">
              <defs>
                <g visibility="hidden">
                  <rect id="clip-shape" width="20" height="20" />
                </g>
                <clipPath id="clip">
                  <use id="clip-use" href="#clip-shape" />
                </clipPath>
              </defs>
              <rect width="20" height="20" fill="#ff0000" clip-path="url(#clip)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(clipUseSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);

        Assert.Equal(new SkiaColor(0xff, 0x00, 0x00, 0xff), bitmap.GetPixel(10, 10));
    }

    [Fact]
    public void RetainedSceneGraph_MaskClipPathUseEvaluatesInheritedClipRuleFromUse()
    {
        const string maskClipUseSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24">
              <defs>
                <path id="donut" d="M0 0H20V20H0Z M5 5H15V15H5Z" />
                <clipPath id="mask-clip">
                  <use id="clip-use" href="#donut" style="clip-rule:evenodd" />
                </clipPath>
                <mask id="mask" maskUnits="userSpaceOnUse" maskContentUnits="userSpaceOnUse" x="0" y="0" width="20" height="20">
                  <rect width="20" height="20" fill="#ffffff" clip-path="url(#mask-clip)" />
                </mask>
              </defs>
              <rect width="20" height="20" fill="#ff0000" mask="url(#mask)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(maskClipUseSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);

        Assert.Equal(new SkiaColor(0xff, 0x00, 0x00, 0xff), bitmap.GetPixel(2, 2));
        Assert.Equal(new SkiaColor(0x00, 0x00, 0x00, 0x00), bitmap.GetPixel(10, 10));
    }

    [Theory]
    [InlineData("path", """<path id="target" d="M10,10 L50,10 L50,30" />""", 3, 10f, 10f, 0f, 50f, 30f, 90f)]
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
    public void RetainedSceneGraph_ReversesStartMarkerForAutoStartReverse()
    {
        const string markerSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="30">
              <defs>
                <marker id="marker" markerWidth="4" markerHeight="4" refX="0" refY="0" orient="auto-start-reverse" markerUnits="userSpaceOnUse">
                  <path id="marker-tick" d="M0,0 L4,0" fill="none" stroke="red" />
                </marker>
              </defs>
              <line id="target"
                    x1="10"
                    y1="10"
                    x2="50"
                    y2="10"
                    fill="none"
                    stroke="black"
                    marker-start="url(#marker)"
                    marker-end="url(#marker)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(markerSvg);

        var markerNodes = GetTargetMarkerNodes(svg.RetainedSceneGraph, "target");

        Assert.Equal(2, markerNodes.Count);
        AssertMarkerTransform(markerNodes[0], 10f, 10f, 180f);
        AssertMarkerTransform(markerNodes[1], 50f, 10f, 0f);
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
        var svgPath = GetResvgSvgPath("tests/masking/mask/self-recursive.svg");
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
    public void RetainedSceneGraph_HandlesConditionalAttributesWithStandardsBehavior()
    {
        using var svg = new SKSvg();
        svg.FromSvg(ConditionalReferenceSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("feature-group", out var featureGroup));
        Assert.True(scene.TryGetNodeById("extension-group", out var extensionGroup));
        Assert.True(scene.TryGetNodeById("language-group", out var languageGroup));
        Assert.True(featureGroup!.SuppressSubtreeRendering);
        Assert.True(extensionGroup!.SuppressSubtreeRendering);
        Assert.True(languageGroup!.SuppressSubtreeRendering);

        Assert.NotNull(svg.Picture);

        using var bitmap = ToBitmap(svg, svg.Picture!);

        Assert.Equal(0, bitmap.GetPixel(6, 6).Alpha);
        Assert.Equal(0, bitmap.GetPixel(6, 22).Alpha);
        Assert.Equal(0, bitmap.GetPixel(6, 38).Alpha);

        var featurePixel = bitmap.GetPixel(22, 6);
        var extensionPixel = bitmap.GetPixel(22, 22);
        var languagePixel = bitmap.GetPixel(22, 38);

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
    public void CreateRetainedSceneGraphPicture_MatchesCurrentPicture_ForFeDropShadowDocument()
    {
        const string dropShadowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="48" viewBox="0 0 80 48">
              <defs>
                <filter id="shadow" x="-30%" y="-30%" width="180%" height="180%" color-interpolation-filters="sRGB">
                  <feDropShadow dx="7" dy="5" stdDeviation="2" flood-color="#123456" flood-opacity="0.8" />
                </filter>
              </defs>
              <rect x="16" y="10" width="28" height="18" rx="3" fill="#ff6633" filter="url(#shadow)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(dropShadowSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();

        Assert.NotNull(svg.Picture);
        Assert.NotNull(retainedPicture);
        AssertPicturesEqual(svg, svg.Picture!, retainedPicture!);
    }

    [Fact]
    public void CreateRetainedSceneGraphPicture_MatchesCurrentPicture_ForLayerEffectStack()
    {
        const string layeredSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="72" height="72" viewBox="0 0 72 72">
              <style>
                #effect-layer { isolation: isolate; mix-blend-mode: multiply; }
              </style>
              <defs>
                <clipPath id="round-clip" clipPathUnits="userSpaceOnUse">
                  <circle cx="36" cy="34" r="24" />
                </clipPath>
                <mask id="alpha-mask"
                      maskUnits="userSpaceOnUse"
                      maskContentUnits="userSpaceOnUse"
                      x="8" y="8" width="56" height="56"
                      mask-type="alpha">
                  <rect x="8" y="8" width="56" height="56" fill="black" opacity="0.55" />
                  <circle cx="42" cy="30" r="18" fill="black" opacity="1" />
                </mask>
                <filter id="blur-shadow" x="-20%" y="-20%" width="140%" height="140%" color-interpolation-filters="sRGB">
                  <feDropShadow dx="3" dy="4" stdDeviation="1.5" flood-color="#112244" flood-opacity="0.7" />
                </filter>
              </defs>
              <rect x="0" y="0" width="72" height="72" fill="#88ccee" />
              <g id="effect-layer"
                 opacity="0.82"
                 clip-path="url(#round-clip)"
                 mask="url(#alpha-mask)"
                 filter="url(#blur-shadow)">
                <rect x="14" y="12" width="44" height="40" fill="#ffcc33" />
                <circle cx="30" cy="36" r="16" fill="#cc3366" />
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(layeredSvg);

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

    private static void AssertInlineClipCommandOrder(SKPicture picture, string sourceElementId)
    {
        var commands = picture
            .FindCommandsBySourceElementId(sourceElementId)
            .Where(static command => command is SaveCanvasCommand or ClipRectCanvasCommand or DrawTextCanvasCommand or RestoreCanvasCommand)
            .ToList();

        var saveIndex = commands.FindIndex(static command => command is SaveCanvasCommand);
        var clipIndex = commands.FindIndex(static command => command is ClipRectCanvasCommand);
        var drawIndex = commands.FindIndex(static command => command is DrawTextCanvasCommand);
        var restoreIndex = commands.FindLastIndex(static command => command is RestoreCanvasCommand);

        Assert.True(
            saveIndex >= 0 && saveIndex < clipIndex && clipIndex < drawIndex && drawIndex < restoreIndex,
            $"Expected Save/Clip/Draw/Restore ordering for '{sourceElementId}', but saw: {string.Join(", ", commands.Select(static command => command.GetType().Name))}.");
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

    private static string CreateOpaqueEncodedImageDataUri(SkiaSharp.SKEncodedImageFormat format, string mimeType)
    {
        using var bitmap = new SkiaBitmap(new SkiaSharp.SKImageInfo(4, 4, SkiaColorType.Rgba8888, SkiaAlphaType.Premul));
        bitmap.Erase(new SkiaColor(0x20, 0x80, 0xe0, 0xff));
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 100);
        return $"data:{mimeType};base64,{Convert.ToBase64String(data.ToArray())}";
    }

    private static string GetW3CTestSvgPath(string fileName)
    {
        return Path.Combine("..", "..", "..", "..", "..", "externals", "W3C_SVG_11_TestSuite", "W3C_SVG_11_TestSuite", "svg", fileName);
    }

    private static string GetResvgSvgPath(string fileName)
    {
        return Path.Combine("..", "..", "..", "..", "..", "externals", "resvg", "crates", "resvg", "tests", fileName);
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
