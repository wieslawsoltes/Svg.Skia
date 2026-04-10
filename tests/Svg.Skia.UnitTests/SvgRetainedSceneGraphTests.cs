using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Svg;
using Svg.DataTypes;
using Svg.FilterEffects;
using Svg.Model.Services;
using Xunit;
using SkiaAlphaType = SkiaSharp.SKAlphaType;
using SkiaBitmap = SkiaSharp.SKBitmap;
using SkiaColors = SkiaSharp.SKColors;
using SkiaColorType = SkiaSharp.SKColorType;

namespace Svg.Skia.UnitTests;

public class SvgRetainedSceneGraphTests
{
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
        Assert.NotEmpty(picture!.Commands);
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

        var blobPoints = retainedModel!
            .FindCommands<DrawTextBlobCanvasCommand>()
            .Where(static cmd => cmd.TextBlob?.Points is { Length: > 0 })
            .SelectMany(static cmd => cmd.TextBlob!.Points!)
            .ToList();

        Assert.Equal(3, blobPoints.Count);
        Assert.Equal(new SKPoint(10f, 20f), blobPoints[0]);
        Assert.Equal(new SKPoint(30f, 40f), blobPoints[1]);
        Assert.Equal(new SKPoint(50f, 20f), blobPoints[2]);

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

        var blobPoints = retainedModel!
            .FindCommands<DrawTextBlobCanvasCommand>()
            .Where(static cmd => cmd.TextBlob?.Points is { Length: > 0 })
            .SelectMany(static cmd => cmd.TextBlob!.Points!)
            .ToList();

        Assert.Single(blobPoints);
        Assert.Equal(new SKPoint(10f, 20f), blobPoints[0]);

        var tailCommand = Assert.Single(retainedModel.FindCommands<DrawTextCanvasCommand>(),
            static cmd => cmd.X == 30f && cmd.Y == 20f);
        Assert.Equal("A", tailCommand.Text);
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
