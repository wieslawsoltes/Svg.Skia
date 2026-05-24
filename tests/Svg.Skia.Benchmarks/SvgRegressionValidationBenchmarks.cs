using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;
using ShimSkiaSharp;
using Svg;
using Svg.Model;
using Svg.Model.Services;
using NativeAlphaType = SkiaSharp.SKAlphaType;
using NativeBitmap = SkiaSharp.SKBitmap;
using NativeCanvas = SkiaSharp.SKCanvas;
using NativeColors = SkiaSharp.SKColors;
using NativeColorType = SkiaSharp.SKColorType;
using NativeImageInfo = SkiaSharp.SKImageInfo;
using NativePicture = SkiaSharp.SKPicture;

namespace Svg.Skia.Benchmarks;

public class SvgTextRegressionValidationBenchmarks
{
    private string svgText = string.Empty;
    private SvgDocument? parsedDocument;
    private SkiaModel? skiaModel;
    private SkiaSvgAssetLoader? assetLoader;
    private SvgSceneDocument? sceneDocument;
    private SKPicture? shimPicture;
    private NativePicture? nativePicture;
    private NativeBitmap? bitmap;
    private NativeCanvas? canvas;

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgRegressionValidationScenarios.TextNames;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgRegressionValidationScenarios.ResolveText(ScenarioName);
        svgText = scenario.SvgText;
        skiaModel = new SkiaModel(new SKSvgSettings());
        assetLoader = new SkiaSvgAssetLoader(skiaModel);
        parsedDocument = SvgBenchmarkHelpers.ParseDocument(new SvgLoadPipelineBenchmarkScenario(scenario.Name, scenario.SvgText, null));
        sceneDocument = Compile(parsedDocument);
        shimPicture = CreateValidatedModel(sceneDocument, scenario);
        nativePicture = skiaModel.ToSKPicture(shimPicture) ?? throw new InvalidOperationException($"Failed to create native picture for '{ScenarioName}'.");
        bitmap = new NativeBitmap(CreateImageInfo(nativePicture));
        canvas = new NativeCanvas(bitmap);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        canvas?.Dispose();
        bitmap?.Dispose();
        nativePicture?.Dispose();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RegressionValidation", "Text", "Compile")]
    public int CompileTextLayoutRegressionScene()
    {
        var scene = Compile(parsedDocument!);
        using var modelScope = new ModelValidationScope(scene.CreateModel());
        return SvgRegressionValidationScenarios.CountTextCommands(modelScope.Model);
    }

    [Benchmark]
    [BenchmarkCategory("RegressionValidation", "Text", "Model")]
    public int ValidateTextLayoutCommandModel()
    {
        using var modelScope = new ModelValidationScope(sceneDocument!.CreateModel());
        return SvgRegressionValidationScenarios.GetTextLayoutCommandChecksum(modelScope.Model);
    }

    [Benchmark]
    [BenchmarkCategory("RegressionValidation", "Text", "Coverage")]
    public int ValidateTextLayoutCommandCoverage()
    {
        using var modelScope = new ModelValidationScope(sceneDocument!.CreateModel());
        return SvgRegressionValidationScenarios.GetCommandCoverageChecksum(
            modelScope.Model,
            SvgRegressionValidationScenarios.ResolveText(ScenarioName),
            "Text layout command coverage checksum");
    }

    [Benchmark]
    [BenchmarkCategory("RegressionValidation", "Text", "Render")]
    public int RenderTextLayoutRegressionBitmap()
    {
        canvas!.Clear(NativeColors.White);
        canvas.DrawPicture(nativePicture!);
        return SvgRegressionValidationScenarios.GetBitmapChecksum(bitmap!);
    }

    [Benchmark]
    [BenchmarkCategory("RegressionValidation", "Text", "EndToEnd")]
    public int LoadRenderAndValidateTextLayoutRegression()
    {
        using var svg = new SKSvg();
        using var picture = svg.FromSvg(svgText);
        using var renderedBitmap = picture!.ToBitmap(
            NativeColors.White,
            1f,
            1f,
            NativeColorType.Rgba8888,
            NativeAlphaType.Premul,
            skiaModel!.Settings.Srgb);

        return SvgRegressionValidationScenarios.GetBitmapChecksum(renderedBitmap!);
    }

    [Benchmark]
    [BenchmarkCategory("RegressionValidation", "Text", "DomMetrics")]
    public int ValidateTextContentDomMetrics()
    {
        var scenario = SvgRegressionValidationScenarios.ResolveText(ScenarioName);
        var viewport = SvgBenchmarkHelpers.GetDocumentViewport(parsedDocument!);
        var checksum = 31;
        var metricsCount = 0;
        var metricElementIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var textBase in parsedDocument!.Descendants().OfType<SvgTextBase>())
        {
            if (textBase is SvgTextPath)
            {
                continue;
            }

            if (!SvgSceneTextCompiler.TryCreateTextContentMetrics(textBase, viewport, assetLoader!, out var metrics))
            {
                continue;
            }

            metricsCount++;
            if (!string.IsNullOrEmpty(textBase.ID))
            {
                metricElementIds.Add(textBase.ID);
            }

            checksum = SvgRegressionValidationScenarios.CombineTextContentMetricsChecksum(checksum, metrics);
        }

        if (metricsCount == 0)
        {
            throw new InvalidOperationException($"Text regression validation scenario '{ScenarioName}' produced no DOM text metrics.");
        }

        if (scenario.RequiredDomMetricElementIds is { Length: > 0 })
        {
            foreach (var requiredId in scenario.RequiredDomMetricElementIds)
            {
                if (!metricElementIds.Contains(requiredId))
                {
                    throw new InvalidOperationException($"Text regression validation scenario '{ScenarioName}' did not produce DOM metrics for required text element '{requiredId}'.");
                }
            }
        }

        return SvgRegressionValidationScenarios.ValidateChecksum(checksum, "Text DOM metrics checksum");
    }

    private SvgSceneDocument Compile(SvgDocument document)
    {
        if (!SvgSceneRuntime.TryCompile(document, assetLoader!, DrawAttributes.None, out var compiledScene) ||
            compiledScene is null)
        {
            throw new InvalidOperationException($"Failed to compile text regression validation scene '{ScenarioName}'.");
        }

        return compiledScene;
    }

    private static SKPicture CreateValidatedModel(SvgSceneDocument scene, RegressionValidationScenario scenario)
    {
        var model = scene.CreateModel() ?? throw new InvalidOperationException("Failed to create text regression validation model.");
        var textCommandCount = SvgRegressionValidationScenarios.CountTextCommands(model);
        var pathCommandCount = SvgRegressionValidationScenarios.CountPathCommands(model);
        if (textCommandCount < scenario.RequiredTextCommandCount ||
            pathCommandCount < scenario.RequiredPathCommandCount)
        {
            throw new InvalidOperationException(
                FormattableString.Invariant($"Text regression model produced text={textCommandCount}, path={pathCommandCount}; expected at least text={scenario.RequiredTextCommandCount}, path={scenario.RequiredPathCommandCount}."));
        }

        SvgRegressionValidationScenarios.ValidateRequiredCommandSources(model, scenario);
        return model;
    }

    private static NativeImageInfo CreateImageInfo(NativePicture picture)
    {
        var width = Math.Max(1, (int)Math.Ceiling(picture.CullRect.Width));
        var height = Math.Max(1, (int)Math.Ceiling(picture.CullRect.Height));
        return new NativeImageInfo(width, height, NativeColorType.Rgba8888, NativeAlphaType.Premul);
    }
}

public class SvgAllAreaRegressionValidationBenchmarks
{
    private string svgText = string.Empty;
    private SvgDocument? parsedDocument;
    private SkiaModel? skiaModel;
    private SkiaSvgAssetLoader? assetLoader;
    private SvgSceneDocument? sceneDocument;
    private SKPicture? shimPicture;
    private NativePicture? nativePicture;
    private NativeBitmap? bitmap;
    private NativeCanvas? canvas;

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgRegressionValidationScenarios.AllAreaNames;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgRegressionValidationScenarios.ResolveAllArea(ScenarioName);
        svgText = scenario.SvgText;
        skiaModel = new SkiaModel(new SKSvgSettings());
        assetLoader = new SkiaSvgAssetLoader(skiaModel);
        parsedDocument = SvgBenchmarkHelpers.ParseDocument(new SvgLoadPipelineBenchmarkScenario(scenario.Name, scenario.SvgText, null));
        sceneDocument = Compile(parsedDocument);
        shimPicture = sceneDocument.CreateModel() ?? throw new InvalidOperationException($"Failed to create combined all-area model for '{ScenarioName}'.");
        SvgRegressionValidationScenarios.ValidateAllAreaModel(shimPicture, scenario);
        nativePicture = skiaModel.ToSKPicture(shimPicture) ?? throw new InvalidOperationException($"Failed to create native picture for '{ScenarioName}'.");
        bitmap = new NativeBitmap(CreateImageInfo(nativePicture));
        canvas = new NativeCanvas(bitmap);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        canvas?.Dispose();
        bitmap?.Dispose();
        nativePicture?.Dispose();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RegressionValidation", "AllArea", "Compile")]
    public int CompileCombinedAllAreaScene()
    {
        var scene = Compile(parsedDocument!);
        return scene.Traverse().Count();
    }

    [Benchmark]
    [BenchmarkCategory("RegressionValidation", "AllArea", "Model")]
    public int ValidateCombinedAllAreaCommandModel()
    {
        using var modelScope = new ModelValidationScope(sceneDocument!.CreateModel());
        SvgRegressionValidationScenarios.ValidateAllAreaModel(modelScope.Model, SvgRegressionValidationScenarios.ResolveAllArea(ScenarioName));
        return SvgRegressionValidationScenarios.GetCommandModelChecksum(modelScope.Model);
    }

    [Benchmark]
    [BenchmarkCategory("RegressionValidation", "AllArea", "Coverage")]
    public int ValidateCombinedAllAreaCommandCoverage()
    {
        using var modelScope = new ModelValidationScope(sceneDocument!.CreateModel());
        return SvgRegressionValidationScenarios.GetCommandCoverageChecksum(
            modelScope.Model,
            SvgRegressionValidationScenarios.ResolveAllArea(ScenarioName),
            "Combined all-area command coverage checksum");
    }

    [Benchmark]
    [BenchmarkCategory("RegressionValidation", "AllArea", "DomMetrics")]
    public int ValidateCombinedAllAreaTextContentDomMetrics()
    {
        var scenario = SvgRegressionValidationScenarios.ResolveAllArea(ScenarioName);
        var viewport = SvgBenchmarkHelpers.GetDocumentViewport(parsedDocument!);
        var checksum = 41;
        var metricsCount = 0;
        var metricElementIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var textBase in parsedDocument!.Descendants().OfType<SvgTextBase>())
        {
            if (textBase is SvgTextPath)
            {
                continue;
            }

            if (!SvgSceneTextCompiler.TryCreateTextContentMetrics(textBase, viewport, assetLoader!, out var metrics))
            {
                continue;
            }

            metricsCount++;
            if (!string.IsNullOrEmpty(textBase.ID))
            {
                metricElementIds.Add(textBase.ID);
            }

            checksum = SvgRegressionValidationScenarios.CombineTextContentMetricsChecksum(checksum, metrics);
        }

        if (metricsCount == 0)
        {
            throw new InvalidOperationException($"Combined all-area regression validation scenario '{ScenarioName}' produced no DOM text metrics.");
        }

        if (scenario.RequiredDomMetricElementIds is { Length: > 0 })
        {
            foreach (var requiredId in scenario.RequiredDomMetricElementIds)
            {
                if (!metricElementIds.Contains(requiredId))
                {
                    throw new InvalidOperationException($"Combined all-area regression validation scenario '{ScenarioName}' did not produce DOM metrics for required text element '{requiredId}'.");
                }
            }
        }

        return SvgRegressionValidationScenarios.ValidateChecksum(checksum, "Combined all-area text DOM metrics checksum");
    }

    [Benchmark]
    [BenchmarkCategory("RegressionValidation", "AllArea", "Render")]
    public int RenderCombinedAllAreaRegressionBitmap()
    {
        canvas!.Clear(NativeColors.White);
        canvas.DrawPicture(nativePicture!);
        return SvgRegressionValidationScenarios.GetBitmapChecksum(bitmap!);
    }

    [Benchmark]
    [BenchmarkCategory("RegressionValidation", "AllArea", "EndToEnd")]
    public int LoadRenderAndValidateCombinedAllAreaRegression()
    {
        using var svg = new SKSvg();
        using var picture = svg.FromSvg(svgText);
        using var renderedBitmap = picture!.ToBitmap(
            NativeColors.White,
            1f,
            1f,
            NativeColorType.Rgba8888,
            NativeAlphaType.Premul,
            skiaModel!.Settings.Srgb);

        return SvgRegressionValidationScenarios.GetBitmapChecksum(renderedBitmap!);
    }

    private SvgSceneDocument Compile(SvgDocument document)
    {
        if (!SvgSceneRuntime.TryCompile(document, assetLoader!, DrawAttributes.None, out var compiledScene) ||
            compiledScene is null)
        {
            throw new InvalidOperationException($"Failed to compile combined all-area validation scene '{ScenarioName}'.");
        }

        return compiledScene;
    }

    private static NativeImageInfo CreateImageInfo(NativePicture picture)
    {
        var width = Math.Max(1, (int)Math.Ceiling(picture.CullRect.Width));
        var height = Math.Max(1, (int)Math.Ceiling(picture.CullRect.Height));
        return new NativeImageInfo(width, height, NativeColorType.Rgba8888, NativeAlphaType.Premul);
    }
}

internal static class SvgRegressionValidationScenarios
{
    private static readonly IReadOnlyList<RegressionValidationScenario> TextScenarios =
    [
        new(
            "text-regression-positioned-layout",
            BuildPositionedTextLayoutScene(),
            RequiredTextCommandCount: 18,
            RequiredTextSourceElementIds: new[] { "absolute-glyphs", "delta-glyphs", "nested-runs", "rtl-run" }),
        new(
            "text-regression-anchor-length-spacing",
            BuildAnchorLengthSpacingScene(),
            RequiredTextCommandCount: 12,
            RequiredTextSourceElementIds: new[] { "anchor-start", "anchor-middle", "anchor-end", "spacing-length", "spacing-glyphs" }),
        new(
            "text-regression-path-and-vertical-layout",
            BuildTextPathAndVerticalScene(),
            RequiredTextCommandCount: 16,
            RequiredDomMetricElementIds: new[] { "inline-text-path", "inline-text-path-length", "inline-mixed-text-path", "inline-mixed-text-path-length", "inline-nested-text-path" },
            RequiredTextSourceElementIds: new[] { "text-path-wave", "text-path-side-run", "inline-text-path-length-run", "inline-mixed-path-run", "inline-mixed-length-path-run", "vertical-mix" }),
        new(
            "text-regression-inline-overflow",
            BuildInlineSizeOverflowScene(),
            RequiredTextCommandCount: 11,
            RequiredDomMetricElementIds: new[] { "wrapped-prewrap", "wrapped-breakspaces", "wrapped-nbsp", "shape-inside-subtract" },
            RequiredTextSourceElementIds: new[] { "overflow-clip", "overflow-ellipsis", "overflow-marker", "wrapped-normal", "wrapped-prewrap", "wrapped-breakspaces", "wrapped-nbsp", "shape-inside-subtract", "overflow-middle-anchor", "overflow-end-anchor", "fit-middle-anchor", "fit-end-anchor" }),
        new(
            "text-regression-shape-exclusions-dom",
            BuildShapeExclusionDomMetricsScene(),
            RequiredTextCommandCount: 6,
            RequiredDomMetricElementIds: new[] { "multi-subtract", "triangle-flow", "circle-subtract-flow", "multi-inside-flow", "shape-box-flow", "dom-baseline" },
            RequiredTextSourceElementIds: new[] { "multi-subtract", "triangle-flow", "circle-subtract-flow", "multi-inside-flow", "shape-box-flow", "dom-baseline" }),
        new(
            "text-regression-baseline-altglyph",
            BuildBaselineAltGlyphScene(),
            RequiredTextCommandCount: 8,
            RequiredDomMetricElementIds: new[] { "baseline-alphabetic", "baseline-middle", "baseline-shift-percent", "altglyph-root" },
            RequiredTextSourceElementIds: new[] { "baseline-alphabetic", "baseline-middle", "baseline-before", "baseline-after", "baseline-shift-percent", "baseline-shift-run", "baseline-use-script-cjk", "ag" }),
        new(
            "text-regression-vertical-rtl-layout",
            BuildVerticalRtlTextLayoutScene(),
            RequiredTextCommandCount: 2,
            RequiredDomMetricElementIds: new[] { "vertical-rtl-inline", "vertical-rtl-arabic", "vertical-rtl-anchor", "vertical-rtl-control" },
            RequiredTextSourceElementIds: new[] { "vertical-rtl-inline", "vertical-rtl-arabic", "vertical-rtl-anchor", "vertical-rtl-positioned", "vertical-rtl-control" }),
        new(
            "text-regression-vertical-rtl-shape-layout",
            BuildVerticalRtlShapeLayoutScene(),
            RequiredTextCommandCount: 1,
            RequiredDomMetricElementIds: new[] { "vertical-shape-rtl", "horizontal-shape-rtl", "shape-layout-control" },
            RequiredTextSourceElementIds: new[] { "vertical-shape-rtl", "horizontal-shape-rtl", "shape-layout-control" }),
        new(
            "text-regression-wrapped-textlength-positioned-descendants",
            BuildTextLengthPositionedWrappedLayoutScene(),
            RequiredTextCommandCount: 3,
            RequiredDomMetricElementIds: new[] { "wrapped-textlength-spacing", "wrapped-textlength-glyphs", "wrapped-textlength-control" },
            RequiredTextSourceElementIds: new[] { "wrapped-textlength-spacing", "wrapped-textlength-glyphs", "wrapped-positioned-descendant", "wrapped-descendant-deltas", "wrapped-textlength-control" }),
        new(
            "text-regression-complex-script-stretch",
            BuildComplexScriptStretchScene(),
            RequiredTextCommandCount: 1,
            RequiredPathCommandCount: 4,
            RequiredTextSourceElementIds: new[] { "stretch-complex-control" },
            RequiredPathSourceElementIds: new[] { "stretch-arabic-path", "stretch-devanagari-path" }),
        new(
            "text-regression-shared-layout-engine-integration",
            BuildSharedTextLayoutEngineIntegrationScene(),
            RequiredTextCommandCount: 5,
            RequiredPathCommandCount: 2,
            RequiredDomMetricElementIds: new[] { "shared-wrap", "shared-control" },
            RequiredTextSourceElementIds: new[] { "shared-wrap", "shared-control" },
            RequiredPathSourceElementIds: new[] { "shared-stretch-run" }),
        new(
            "text-regression-pending-text-gap-coverage",
            BuildPendingTextGapCoverageScene(),
            RequiredTextCommandCount: 4,
            RequiredPathCommandCount: 1,
            RequiredTextSourceElementIds: new[] { "vertical-inline-pending", "rtl-inline-pending", "tiny-path-run" },
            RequiredPathSourceElementIds: new[] { "stretch-filter-path" },
            RequiredLayerSourceElementIds: new[] { "stretch-filter-pending" })
    ];

    private static readonly IReadOnlyList<RegressionValidationScenario> AllAreaScenarios =
    [
        new(
            "combined-all-area-regression",
            BuildCombinedAllAreaScene(),
            RequiredTextCommandCount: 8,
            RequiredPathCommandCount: 16,
            RequiredClipCommandCount: 2,
            RequiredLayerCommandCount: 1,
            RequiredDomMetricElementIds: new[] { "combined-inline-overflow", "combined-shared-wrap", "combined-shape-wrap" },
            RequiredTextSourceElementIds: new[] { "combined-title", "combined-anchor", "combined-inline-overflow", "combined-shared-wrap", "combined-shape-wrap", "combined-caption-path-text" },
            RequiredPathSourceElementIds: new[] { "combined-stretch-path" })
    ];

    public static IEnumerable<string> TextNames => TextScenarios.Select(static scenario => scenario.Name);

    public static IEnumerable<string> AllAreaNames => AllAreaScenarios.Select(static scenario => scenario.Name);

    public static RegressionValidationScenario ResolveText(string name)
        => TextScenarios.First(scenario => string.Equals(scenario.Name, name, StringComparison.Ordinal));

    public static RegressionValidationScenario ResolveAllArea(string name)
        => AllAreaScenarios.First(scenario => string.Equals(scenario.Name, name, StringComparison.Ordinal));

    public static int CountTextCommands(SKPicture? picture)
        => EnumerateCommands(picture).Count(static command =>
            command is DrawTextCanvasCommand or DrawTextBlobCanvasCommand or DrawTextOnPathCanvasCommand);

    public static int CountPathCommands(SKPicture? picture)
        => EnumerateCommands(picture).Count(static command => command is DrawPathCanvasCommand);

    public static int GetTextLayoutCommandChecksum(SKPicture? picture)
    {
        var checksum = 17;
        foreach (var command in EnumerateCommands(picture))
        {
            checksum = CombineCommandSource(checksum, command);
            checksum = command switch
            {
                DrawTextCanvasCommand drawText => CombineTextCommand(checksum, drawText.Text, drawText.X, drawText.Y),
                DrawTextBlobCanvasCommand drawTextBlob => CombineTextBlobCommand(checksum, drawTextBlob),
                DrawTextOnPathCanvasCommand drawTextOnPath => CombineTextOnPathCommand(checksum, drawTextOnPath),
                DrawPathCanvasCommand drawPath => CombinePathCommand(checksum, drawPath),
                ClipPathCanvasCommand clipPath => CombineClipPathCommand(checksum, clipPath),
                ClipRectCanvasCommand clipRect => CombineRectCommand(checksum, clipRect.Rect),
                SaveLayerCanvasCommand saveLayer => CombineSaveLayerCommand(checksum, saveLayer),
                _ => checksum
            };
        }

        ValidateChecksum(checksum, "Text layout command checksum");
        return checksum;
    }

    public static int GetCommandModelChecksum(SKPicture? picture)
    {
        var checksum = 23;
        foreach (var command in EnumerateCommands(picture))
        {
            checksum = Combine(checksum, command.GetType().Name.Length);
            checksum = CombineCommandSource(checksum, command);

            if (command is DrawPathCanvasCommand drawPath)
            {
                checksum = CombinePathCommand(checksum, drawPath);
            }
            else if (command is DrawTextCanvasCommand drawText)
            {
                checksum = CombineTextCommand(checksum, drawText.Text, drawText.X, drawText.Y);
            }
            else if (command is DrawTextBlobCanvasCommand drawTextBlob)
            {
                checksum = CombineTextBlobCommand(checksum, drawTextBlob);
            }
            else if (command is DrawTextOnPathCanvasCommand drawTextOnPath)
            {
                checksum = CombineTextOnPathCommand(checksum, drawTextOnPath);
            }
            else if (command is ClipPathCanvasCommand clipPath)
            {
                checksum = CombineClipPathCommand(checksum, clipPath);
            }
            else if (command is ClipRectCanvasCommand clipRect)
            {
                checksum = CombineRectCommand(checksum, clipRect.Rect);
            }
            else if (command is SaveLayerCanvasCommand saveLayer)
            {
                checksum = CombineSaveLayerCommand(checksum, saveLayer);
            }
        }

        ValidateChecksum(checksum, "Command model checksum");
        return checksum;
    }

    public static int GetBitmapChecksum(NativeBitmap bitmap)
    {
        var checksum = 29;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                checksum = Combine(checksum, bitmap.GetPixel(x, y).GetHashCode());
            }
        }

        ValidateChecksum(checksum, "Bitmap checksum");
        return checksum;
    }

    public static int GetCommandCoverageChecksum(SKPicture? picture, RegressionValidationScenario scenario, string label)
    {
        var textCount = 0;
        var drawTextCount = 0;
        var drawTextBlobCount = 0;
        var drawTextOnPathCount = 0;
        var pathCount = 0;
        var clipCount = 0;
        var clipPathCount = 0;
        var clipRectCount = 0;
        var layerCount = 0;
        var commandCount = 0;

        foreach (var command in EnumerateCommands(picture))
        {
            commandCount++;
            switch (command)
            {
                case DrawTextCanvasCommand:
                    textCount++;
                    drawTextCount++;
                    break;
                case DrawTextBlobCanvasCommand:
                    textCount++;
                    drawTextBlobCount++;
                    break;
                case DrawTextOnPathCanvasCommand:
                    textCount++;
                    drawTextOnPathCount++;
                    break;
                case DrawPathCanvasCommand:
                    pathCount++;
                    break;
                case ClipPathCanvasCommand:
                    clipCount++;
                    clipPathCount++;
                    break;
                case ClipRectCanvasCommand:
                    clipCount++;
                    clipRectCount++;
                    break;
                case SaveLayerCanvasCommand:
                    layerCount++;
                    break;
            }
        }

        ValidateCommandCounts(scenario, textCount, pathCount, clipCount, layerCount);
        ValidateRequiredCommandSources(picture, scenario);

        var checksum = 37;
        checksum = Combine(checksum, commandCount);
        checksum = Combine(checksum, textCount);
        checksum = Combine(checksum, drawTextCount);
        checksum = Combine(checksum, drawTextBlobCount);
        checksum = Combine(checksum, drawTextOnPathCount);
        checksum = Combine(checksum, pathCount);
        checksum = Combine(checksum, clipCount);
        checksum = Combine(checksum, clipPathCount);
        checksum = Combine(checksum, clipRectCount);
        checksum = Combine(checksum, layerCount);
        return ValidateChecksum(checksum, label);
    }

    public static void ValidateAllAreaModel(SKPicture? picture, RegressionValidationScenario scenario)
    {
        var textCount = 0;
        var pathCount = 0;
        var clipCount = 0;
        var layerCount = 0;

        foreach (var command in EnumerateCommands(picture))
        {
            switch (command)
            {
                case DrawTextCanvasCommand:
                case DrawTextBlobCanvasCommand:
                case DrawTextOnPathCanvasCommand:
                    textCount++;
                    break;
                case DrawPathCanvasCommand:
                    pathCount++;
                    break;
                case ClipPathCanvasCommand:
                case ClipRectCanvasCommand:
                    clipCount++;
                    break;
                case SaveLayerCanvasCommand:
                    layerCount++;
                    break;
            }
        }

        ValidateCommandCounts(scenario, textCount, pathCount, clipCount, layerCount);
        ValidateRequiredCommandSources(picture, scenario);
    }

    private static void ValidateCommandCounts(
        RegressionValidationScenario scenario,
        int textCount,
        int pathCount,
        int clipCount,
        int layerCount)
    {
        if (textCount >= scenario.RequiredTextCommandCount &&
            pathCount >= scenario.RequiredPathCommandCount &&
            clipCount >= scenario.RequiredClipCommandCount &&
            layerCount >= scenario.RequiredLayerCommandCount)
        {
            return;
        }

        throw new InvalidOperationException(
            FormattableString.Invariant(
                $"Regression validation scenario '{scenario.Name}' produced text={textCount}, path={pathCount}, clip={clipCount}, layer={layerCount}; expected at least text={scenario.RequiredTextCommandCount}, path={scenario.RequiredPathCommandCount}, clip={scenario.RequiredClipCommandCount}, layer={scenario.RequiredLayerCommandCount}."));
    }

    public static void ValidateRequiredCommandSources(SKPicture? picture, RegressionValidationScenario scenario)
    {
        var textSourceIds = new HashSet<string>(StringComparer.Ordinal);
        var pathSourceIds = new HashSet<string>(StringComparer.Ordinal);
        var clipSourceIds = new HashSet<string>(StringComparer.Ordinal);
        var layerSourceIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var command in EnumerateCommands(picture))
        {
            if (string.IsNullOrWhiteSpace(command.SourceElementId))
            {
                continue;
            }

            switch (command)
            {
                case DrawTextCanvasCommand:
                case DrawTextBlobCanvasCommand:
                case DrawTextOnPathCanvasCommand:
                    textSourceIds.Add(command.SourceElementId);
                    break;
                case DrawPathCanvasCommand:
                    pathSourceIds.Add(command.SourceElementId);
                    break;
                case ClipPathCanvasCommand:
                case ClipRectCanvasCommand:
                    clipSourceIds.Add(command.SourceElementId);
                    break;
                case SaveLayerCanvasCommand:
                    layerSourceIds.Add(command.SourceElementId);
                    break;
            }
        }

        ValidateRequiredSourceIds(scenario.RequiredTextSourceElementIds, textSourceIds, scenario.Name, "text");
        ValidateRequiredSourceIds(scenario.RequiredPathSourceElementIds, pathSourceIds, scenario.Name, "path");
        ValidateRequiredSourceIds(scenario.RequiredClipSourceElementIds, clipSourceIds, scenario.Name, "clip");
        ValidateRequiredSourceIds(scenario.RequiredLayerSourceElementIds, layerSourceIds, scenario.Name, "layer");
    }

    private static void ValidateRequiredSourceIds(string[]? requiredIds, HashSet<string> actualIds, string scenarioName, string commandKind)
    {
        if (requiredIds is not { Length: > 0 })
        {
            return;
        }

        foreach (var requiredId in requiredIds)
        {
            if (!actualIds.Contains(requiredId))
            {
                throw new InvalidOperationException($"Regression validation scenario '{scenarioName}' did not produce a required {commandKind} command from source element '{requiredId}'.");
            }
        }
    }

    private static IEnumerable<CanvasCommand> EnumerateCommands(SKPicture? picture)
    {
        if (picture?.Commands is null)
        {
            yield break;
        }

        for (var i = 0; i < picture.Commands.Count; i++)
        {
            var command = picture.Commands[i];
            yield return command;

            if (command is DrawPictureCanvasCommand { Picture: { } nestedPicture })
            {
                foreach (var nestedCommand in EnumerateCommands(nestedPicture))
                {
                    yield return nestedCommand;
                }
            }
        }
    }

    private static int CombineTextCommand(int checksum, string text, float x, float y)
    {
        checksum = Combine(checksum, text.Length);
        checksum = Combine(checksum, StringComparer.Ordinal.GetHashCode(text));
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(x));
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(y));
        return checksum;
    }

    private static int CombineTextBlobCommand(int checksum, DrawTextBlobCanvasCommand command)
    {
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(command.X));
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(command.Y));
        checksum = Combine(checksum, command.TextBlob?.Text?.Length ?? 0);
        checksum = Combine(checksum, command.TextBlob?.Glyphs?.Length ?? 0);

        if (command.TextBlob?.Points is { } points)
        {
            checksum = Combine(checksum, points.Length);
            for (var i = 0; i < points.Length; i++)
            {
                checksum = Combine(checksum, BitConverter.SingleToInt32Bits(points[i].X));
                checksum = Combine(checksum, BitConverter.SingleToInt32Bits(points[i].Y));
            }
        }

        return checksum;
    }

    private static int CombineTextOnPathCommand(int checksum, DrawTextOnPathCanvasCommand command)
    {
        checksum = CombineTextCommand(checksum, command.Text, command.HOffset, command.VOffset);
        checksum = CombinePathGeometry(checksum, command.Path);
        checksum = Combine(checksum, command.TextAlign?.GetHashCode() ?? 0);
        checksum = CombinePaint(checksum, command.Paint);

        if (command.Font is { } font)
        {
            checksum = Combine(checksum, BitConverter.SingleToInt32Bits(font.Size));
            checksum = Combine(checksum, BitConverter.SingleToInt32Bits(font.ScaleX));
            checksum = Combine(checksum, BitConverter.SingleToInt32Bits(font.SkewX));
            checksum = Combine(checksum, font.Subpixel ? 1 : 0);
            checksum = Combine(checksum, font.Embolden ? 1 : 0);
            checksum = Combine(checksum, font.Edging.GetHashCode());
        }

        return checksum;
    }

    private static int CombineRectCommand(int checksum, SKRect rect)
    {
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(rect.Left));
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(rect.Top));
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(rect.Right));
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(rect.Bottom));
        return checksum;
    }

    private static int CombinePathCommand(int checksum, DrawPathCanvasCommand command)
    {
        checksum = CombineRectCommand(checksum, command.Path?.Bounds ?? SKRect.Empty);
        checksum = CombinePathGeometry(checksum, command.Path);
        checksum = CombinePaint(checksum, command.Paint);
        return checksum;
    }

    private static int CombineClipPathCommand(int checksum, ClipPathCanvasCommand command)
    {
        checksum = Combine(checksum, command.Operation.GetHashCode());
        checksum = Combine(checksum, command.Antialias ? 1 : 0);
        checksum = CombineClipPath(checksum, command.ClipPath);
        return checksum;
    }

    private static int CombineSaveLayerCommand(int checksum, SaveLayerCanvasCommand command)
    {
        checksum = Combine(checksum, command.Count);
        checksum = CombinePaint(checksum, command.Paint);
        return checksum;
    }

    private static int CombineClipPath(int checksum, ClipPath? clipPath)
    {
        if (clipPath is null)
        {
            return Combine(checksum, 0);
        }

        checksum = Combine(checksum, 1);
        checksum = CombineMatrix(checksum, clipPath.Transform);
        checksum = Combine(checksum, clipPath.Clips?.Count ?? 0);
        if (clipPath.Clips is { } clips)
        {
            for (var i = 0; i < clips.Count; i++)
            {
                checksum = CombinePathGeometry(checksum, clips[i].Path);
                checksum = CombineMatrix(checksum, clips[i].Transform);
                checksum = CombineClipPath(checksum, clips[i].Clip);
            }
        }

        checksum = CombineClipPath(checksum, clipPath.Clip);
        return checksum;
    }

    private static int CombinePaint(int checksum, SKPaint? paint)
    {
        if (paint is null)
        {
            return Combine(checksum, 0);
        }

        checksum = Combine(checksum, 1);
        checksum = Combine(checksum, paint.Color?.GetHashCode() ?? 0);
        checksum = Combine(checksum, paint.Style.GetHashCode());
        checksum = Combine(checksum, paint.IsAntialias ? 1 : 0);
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(paint.StrokeWidth));
        checksum = Combine(checksum, paint.StrokeCap.GetHashCode());
        checksum = Combine(checksum, paint.StrokeJoin.GetHashCode());
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(paint.TextSize));
        checksum = Combine(checksum, paint.TextAlign.GetHashCode());
        checksum = Combine(checksum, paint.LcdRenderText ? 1 : 0);
        checksum = Combine(checksum, paint.SubpixelText ? 1 : 0);
        checksum = Combine(checksum, paint.TextEncoding.GetHashCode());
        checksum = Combine(checksum, paint.BlendMode.GetHashCode());
        checksum = Combine(checksum, paint.FilterQuality.GetHashCode());
        checksum = Combine(checksum, paint.Shader?.GetType().Name.GetHashCode(StringComparison.Ordinal) ?? 0);
        checksum = Combine(checksum, paint.ColorFilter?.GetType().Name.GetHashCode(StringComparison.Ordinal) ?? 0);
        checksum = Combine(checksum, paint.ImageFilter?.GetType().Name.GetHashCode(StringComparison.Ordinal) ?? 0);
        checksum = Combine(checksum, paint.PathEffect?.GetType().Name.GetHashCode(StringComparison.Ordinal) ?? 0);
        return checksum;
    }

    private static int CombineMatrix(int checksum, SKMatrix? matrix)
    {
        if (!matrix.HasValue)
        {
            return Combine(checksum, 0);
        }

        var value = matrix.Value;
        checksum = Combine(checksum, 1);
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(value.ScaleX));
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(value.SkewX));
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(value.TransX));
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(value.ScaleY));
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(value.SkewY));
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(value.TransY));
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(value.Persp0));
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(value.Persp1));
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(value.Persp2));
        return checksum;
    }

    private static int CombinePathGeometry(int checksum, SKPath? path)
    {
        if (path?.Commands is null)
        {
            return checksum;
        }

        checksum = Combine(checksum, path.FillType.GetHashCode());
        checksum = Combine(checksum, path.Commands.Count);
        for (var i = 0; i < path.Commands.Count; i++)
        {
            var command = path.Commands[i];
            checksum = Combine(checksum, StringComparer.Ordinal.GetHashCode(command.GetType().Name));
            checksum = command switch
            {
                MoveToPathCommand moveTo => CombinePoint(checksum, moveTo.X, moveTo.Y),
                LineToPathCommand lineTo => CombinePoint(checksum, lineTo.X, lineTo.Y),
                QuadToPathCommand quadTo => CombinePoint(CombinePoint(checksum, quadTo.X0, quadTo.Y0), quadTo.X1, quadTo.Y1),
                CubicToPathCommand cubicTo => CombinePoint(CombinePoint(CombinePoint(checksum, cubicTo.X0, cubicTo.Y0), cubicTo.X1, cubicTo.Y1), cubicTo.X2, cubicTo.Y2),
                ArcToPathCommand arcTo => CombinePoint(CombinePoint(CombinePoint(checksum, arcTo.Rx, arcTo.Ry), arcTo.XAxisRotate, arcTo.X), arcTo.Y, arcTo.LargeArc.GetHashCode() + arcTo.Sweep.GetHashCode()),
                AddCirclePathCommand circle => CombinePoint(CombinePoint(checksum, circle.X, circle.Y), circle.Radius, 0f),
                AddOvalPathCommand oval => CombineRectCommand(checksum, oval.Rect),
                AddRectPathCommand rect => CombineRectCommand(checksum, rect.Rect),
                AddRoundRectPathCommand roundRect => CombinePoint(CombineRectCommand(checksum, roundRect.Rect), roundRect.Rx, roundRect.Ry),
                AddPolyPathCommand poly => CombinePolyPath(checksum, poly.Points, poly.Close),
                ClosePathCommand => checksum,
                _ => checksum
            };
        }

        return checksum;
    }

    private static int CombinePolyPath(int checksum, IList<SKPoint>? points, bool close)
    {
        checksum = Combine(checksum, close ? 1 : 0);
        checksum = Combine(checksum, points?.Count ?? 0);
        if (points is null)
        {
            return checksum;
        }

        for (var i = 0; i < points.Count; i++)
        {
            checksum = CombinePoint(checksum, points[i].X, points[i].Y);
        }

        return checksum;
    }

    private static int CombinePoint(int checksum, float x, float y)
    {
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(x));
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(y));
        return checksum;
    }

    private static int CombinePoint(int checksum, float x, float y, float z)
    {
        checksum = CombinePoint(checksum, x, y);
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(z));
        return checksum;
    }

    private static int CombineCommandSource(int checksum, CanvasCommand command)
    {
        checksum = Combine(checksum, command.SourceElementId?.Length ?? 0);
        if (!string.IsNullOrEmpty(command.SourceElementId))
        {
            checksum = Combine(checksum, StringComparer.Ordinal.GetHashCode(command.SourceElementId));
        }

        return checksum;
    }

    private static int Combine(int checksum, int value)
    {
        unchecked
        {
            return (checksum * 16777619) ^ value;
        }
    }

    public static int CombineTextContentMetricsChecksum(int checksum, SvgSceneTextCompiler.SvgTextContentMetrics metrics)
    {
        checksum = Combine(checksum, metrics.NumberOfChars);
        checksum = Combine(checksum, BitConverter.SingleToInt32Bits(metrics.ComputedTextLength));
        if (metrics.NumberOfChars > 0)
        {
            var firstStart = metrics.GetStartPositionOfChar(0);
            var lastEnd = metrics.GetEndPositionOfChar(metrics.NumberOfChars - 1);
            checksum = Combine(checksum, BitConverter.SingleToInt32Bits(firstStart.X));
            checksum = Combine(checksum, BitConverter.SingleToInt32Bits(firstStart.Y));
            checksum = Combine(checksum, BitConverter.SingleToInt32Bits(lastEnd.X));
            checksum = Combine(checksum, BitConverter.SingleToInt32Bits(lastEnd.Y));
        }

        return checksum;
    }

    public static int ValidateChecksum(int checksum, string label)
    {
        if (checksum == 0)
        {
            throw new InvalidOperationException($"{label} unexpectedly resolved to zero.");
        }

        return checksum;
    }

    private static string BuildPositionedTextLayoutScene()
    {
        var builder = CreateSvgBuilder(760, 260);
        builder.AppendLine("""
          <rect width="760" height="260" fill="#ffffff" />
          <g font-family="Noto Sans, Arial, sans-serif" font-size="22" fill="#111827">
            <text id="absolute-glyphs" x="36 68 106 146 190 238" y="58 58 58 58 58 58" rotate="0 8 -8 0 6 -6">VECTOR</text>
            <text id="delta-glyphs" x="36" y="108" dx="0 7 -3 10 -4 6" dy="0 -7 7 0 -5 5">layout</text>
            <text id="nested-runs" x="36" y="162">Start <tspan fill="#2563eb" dx="8 3 3">blue</tspan><tspan x="300 335 370" y="162 150 162" fill="#dc2626">XYZ</tspan></text>
            <text id="rtl-run" x="700" y="214" direction="rtl" unicode-bidi="embed">ABC<tspan font-weight="700">DEF</tspan>GHI</text>
          </g>
        """);
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildAnchorLengthSpacingScene()
    {
        var builder = CreateSvgBuilder(820, 300);
        builder.AppendLine("""
          <rect width="820" height="300" fill="#ffffff" />
          <g font-family="Noto Sans, Arial, sans-serif" font-size="24" fill="#111827">
            <line x1="410" y1="20" x2="410" y2="285" stroke="#94a3b8" stroke-width="1" />
            <text id="anchor-start" x="410" y="62" text-anchor="start">start anchor</text>
            <text id="anchor-middle" x="410" y="112" text-anchor="middle">middle anchor</text>
            <text id="anchor-end" x="410" y="162" text-anchor="end">end anchor</text>
            <text id="spacing-length" x="44" y="224" letter-spacing="2.5" word-spacing="8" textLength="420" lengthAdjust="spacing">spread words across length</text>
            <text id="spacing-glyphs" x="44" y="268" textLength="360" lengthAdjust="spacingAndGlyphs">scaled glyph spacing</text>
          </g>
        """);
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildTextPathAndVerticalScene()
    {
        var builder = CreateSvgBuilder(920, 380);
        builder.AppendLine("""
          <defs>
            <path id="wave" d="M40 145 C120 45 200 245 280 145 S440 45 520 145 S680 245 820 145" />
            <path id="arc" d="M90 286 C220 206 360 206 490 286" />
            <path id="wideArc" d="M40 286 C220 206 520 206 900 286" />
          </defs>
          <rect width="920" height="380" fill="#ffffff" />
          <path d="M40 145 C120 45 200 245 280 145 S440 45 520 145 S680 245 820 145" fill="none" stroke="#cbd5e1" />
          <path d="M90 286 C220 206 360 206 490 286" fill="none" stroke="#cbd5e1" />
          <path d="M40 286 C220 206 520 206 900 286" fill="none" stroke="#e2e8f0" />
          <g font-family="Noto Sans, Arial, sans-serif" font-size="20" fill="#111827">
            <text id="text-path-default"><textPath id="text-path-wave" href="#wave" startOffset="4%">default path placement with visible glyph offsets</textPath></text>
            <text id="text-path-side" fill="#2563eb"><textPath id="text-path-side-run" href="#arc" side="right" startOffset="12%">right side path placement</textPath></text>
            <text id="inline-text-path" x="120" y="286" inline-size="180"><textPath id="inline-text-path-run" href="#arc" startOffset="0%">AB</textPath></text>
            <text id="inline-text-path-length" x="300" y="286" inline-size="160"><textPath id="inline-text-path-length-run" href="#arc" textLength="120">AB</textPath></text>
            <text id="inline-mixed-text-path" x="520" y="286" inline-size="62"><tspan id="mixed-path-head">AA</tspan><textPath id="inline-mixed-path-run" href="#wideArc" startOffset="0%">A</textPath><tspan id="mixed-path-tail">BB</tspan></text>
            <text id="inline-mixed-text-path-length" x="520" y="332" inline-size="180" textLength="150"><tspan id="mixed-length-head">AA</tspan><textPath id="inline-mixed-length-path-run" href="#wideArc" startOffset="0%">A</textPath><tspan id="mixed-length-tail">B</tspan></text>
            <text id="inline-nested-text-path" x="650" y="286" inline-size="24"><tspan fill="#b91c1c"><textPath id="inline-nested-path-run" href="#wideArc" startOffset="0%">A</textPath></tspan><tspan id="nested-path-tail">B</tspan></text>
            <text id="vertical-mix" x="730" y="36" writing-mode="tb" glyph-orientation-vertical="0">Vertical123</text>
          </g>
        """);
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildBaselineAltGlyphScene()
    {
        var builder = new StringBuilder();
        builder.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="720" height="260" viewBox="0 0 720 260">""");
        builder.AppendLine("""
          <defs>
            <altGlyphDef id="alt-benchmark-def">
              <glyphRef xlink:href="#glyphA" glyphRef="glyphA" />
            </altGlyphDef>
          </defs>
          <rect width="720" height="260" fill="#ffffff" />
          <g font-family="Noto Sans, Arial, sans-serif" font-size="24" fill="#111827">
            <text id="baseline-alphabetic" x="42" y="64">A</text>
            <text id="baseline-middle" x="104" y="64" dominant-baseline="middle">A</text>
            <text id="baseline-before" x="166" y="64" alignment-baseline="text-before-edge">A</text>
            <text id="baseline-after" x="228" y="64" alignment-baseline="text-after-edge">A</text>
            <text id="baseline-shift-percent" x="42" y="126">base <tspan id="baseline-shift-run" baseline-shift="50%" fill="#2563eb">shift</tspan></text>
            <text id="baseline-use-script-cjk" x="42" y="188" dominant-baseline="use-script">ABC 漢字</text>
            <text id="altglyph-root" x="360" y="126">fallback <altGlyph id="ag" xlink:href="#alt-benchmark-def" glyphRef="glyphA" fill="#dc2626">A</altGlyph> glyph</text>
          </g>
        """);
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildInlineSizeOverflowScene()
    {
        var builder = CreateSvgBuilder(720, 420);
        builder.AppendLine("""
          <rect width="720" height="420" fill="#ffffff" />
          <defs>
            <rect id="shape" x="390" y="112" width="118" height="86" />
            <rect id="subtract" x="390" y="112" width="48" height="34" />
          </defs>
          <g font-family="Noto Sans, Arial, sans-serif" font-size="24" fill="#111827">
            <text id="overflow-clip" x="40" y="58" inline-size="116" text-overflow="clip">clippedinlinesizetextsample</text>
            <text id="overflow-ellipsis" x="40" y="112" inline-size="132" text-overflow="ellipsis">ellipsizedinlinesizetextsample</text>
            <text id="overflow-marker" x="40" y="166" inline-size="144" text-overflow="'>>'">custommarkerinlineoverflowsample</text>
            <text id="overflow-fits" x="40" y="220" inline-size="420" text-overflow="ellipsis">short label</text>
            <text id="wrapped-normal" x="390" y="58" inline-size="58">A B C D</text>
            <text id="wrapped-prewrap" x="540" y="58" inline-size="80" white-space="pre-wrap"> A</text>
            <text id="wrapped-breakspaces" x="540" y="112" inline-size="42" white-space="break-spaces">A  B</text>
            <text id="wrapped-nbsp" x="540" y="166" inline-size="40">A&#160;B</text>
            <text id="shape-inside-subtract" shape-inside="url(#shape)" shape-subtract="url(#subtract)">shape text wraps around exclusion</text>
            <text id="overflow-middle-anchor" x="460" y="220" inline-size="150" text-overflow="ellipsis" text-anchor="middle">middle anchored overflow sample</text>
            <text id="overflow-end-anchor" x="650" y="290" inline-size="150" text-overflow="ellipsis" text-anchor="end">end anchored overflow sample</text>
            <text id="fit-middle-anchor" x="170" y="346" inline-size="220" text-overflow="ellipsis" text-anchor="middle">middle fit</text>
            <text id="fit-end-anchor" x="650" y="346" inline-size="220" text-overflow="ellipsis" text-anchor="end">end fit</text>
          </g>
        """);
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildShapeExclusionDomMetricsScene()
    {
        var builder = CreateSvgBuilder(760, 360);
        builder.AppendLine("""
          <rect width="760" height="360" fill="#ffffff" />
          <defs>
            <rect id="shape-wide" x="40" y="42" width="180" height="116" />
            <rect id="subtract-left" x="40" y="42" width="58" height="44" />
            <rect id="subtract-right" x="164" y="42" width="56" height="44" />
            <path id="triangle-shape" d="M430 42 L548 238 L300 238 Z" />
            <circle id="circle-subtract" cx="612" cy="92" r="40" />
            <rect id="circle-flow" x="552" y="42" width="150" height="130" />
            <rect id="shape-column-a" x="40" y="188" width="32" height="42" />
            <rect id="shape-column-b" x="116" y="188" width="120" height="42" />
          </defs>
          <g font-family="Noto Sans, Arial, sans-serif" font-size="22" fill="#111827">
            <text id="multi-subtract" shape-inside="url(#shape-wide)" shape-subtract="url(#subtract-left) url(#subtract-right)">A B C</text>
            <text id="triangle-flow" shape-inside="url(#triangle-shape)">triangle shaped text wraps through sampled fragments</text>
            <text id="circle-subtract-flow" shape-inside="url(#circle-flow)" shape-subtract="url(#circle-subtract)">circle exclusion text wraps after sampled subtract intervals</text>
            <text id="multi-inside-flow" shape-inside="url(#shape-column-a) url(#shape-column-b)">A B</text>
            <text id="shape-box-flow" shape-inside="fill-box inset(262px 318px 42px 260px)">shape box flow</text>
            <text id="dom-baseline" x="40" y="310" inline-size="180">DOM metrics benchmark source line</text>
          </g>
        """);
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildVerticalRtlTextLayoutScene()
    {
        var builder = CreateSvgBuilder(760, 420);
        builder.AppendLine("""
          <rect width="760" height="420" fill="#ffffff" />
          <g fill="none" stroke="#cbd5e1" stroke-width="1">
            <line x1="700" y1="34" x2="700" y2="330" />
            <line x1="560" y1="34" x2="560" y2="330" />
            <line x1="410" y1="76" x2="410" y2="342" />
            <line x1="260" y1="46" x2="260" y2="230" />
          </g>
          <g font-family="'Noto Sans Arabic', Noto Sans, Arial, sans-serif" font-size="24" fill="#111827">
            <text id="vertical-rtl-inline" x="700" y="40" writing-mode="vertical-rl" direction="rtl" unicode-bidi="embed" inline-size="150">A B C D E F G H</text>
            <text id="vertical-rtl-arabic" x="560" y="46" writing-mode="vertical-rl" direction="rtl" unicode-bidi="embed" inline-size="136">&#x0645;&#x0631;&#x062D;&#x0628;&#x0627; &#x0628;&#x0627;&#x0644;&#x0639;&#x0627;&#x0644;&#x0645; 123</text>
            <text id="vertical-rtl-anchor" x="410" y="212" writing-mode="vertical-rl" direction="rtl" unicode-bidi="embed" text-anchor="middle" inline-size="132">anchor wraps right to left</text>
            <text id="vertical-rtl-positioned" x="260 260 260 260 260 260" y="58 88 118 148 178 208" writing-mode="tb" direction="rtl" unicode-bidi="embed" rotate="0 90 0 -90 0 90">ABCDEF</text>
            <text id="vertical-rtl-control" x="42" y="372" inline-size="260">vertical rtl benchmark control text</text>
          </g>
        """);
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildVerticalRtlShapeLayoutScene()
    {
        var builder = CreateSvgBuilder(820, 460);
        builder.AppendLine("""
          <rect width="820" height="460" fill="#ffffff" />
          <defs>
            <rect id="vertical-shape" x="578" y="48" width="128" height="250" />
            <rect id="vertical-subtract" x="616" y="112" width="76" height="86" />
            <path id="rtl-shape" d="M68 56 L306 56 L274 238 L98 238 Z" />
            <circle id="rtl-subtract" cx="198" cy="128" r="46" />
          </defs>
          <use href="#vertical-shape" fill="none" stroke="#94a3b8" stroke-width="2" />
          <use href="#vertical-subtract" fill="#fee2e2" stroke="#fca5a5" stroke-width="1" />
          <use href="#rtl-shape" fill="none" stroke="#94a3b8" stroke-width="2" />
          <use href="#rtl-subtract" fill="#dbeafe" stroke="#93c5fd" stroke-width="1" />
          <g font-family="Noto Sans, Arial, sans-serif" font-size="22" fill="#111827">
            <text id="vertical-shape-rtl" shape-inside="url(#vertical-shape)" shape-subtract="url(#vertical-subtract)" writing-mode="vertical-rl" direction="rtl" unicode-bidi="embed">A B C D E F G H I J K</text>
            <text id="horizontal-shape-rtl" shape-inside="url(#rtl-shape)" shape-subtract="url(#rtl-subtract)" direction="rtl" unicode-bidi="embed">A B C D E F G H I J K L</text>
            <text id="shape-layout-control" x="52" y="394" inline-size="240">shape layout metrics control wraps normally</text>
          </g>
        """);
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildTextLengthPositionedWrappedLayoutScene()
    {
        var builder = CreateSvgBuilder(860, 420);
        builder.AppendLine("""
          <rect width="860" height="420" fill="#ffffff" />
          <g font-family="Noto Sans, Arial, sans-serif" font-size="22" fill="#111827">
            <rect x="38" y="34" width="180" height="122" fill="none" stroke="#cbd5e1" />
            <rect x="38" y="180" width="250" height="112" fill="none" stroke="#cbd5e1" />
            <rect x="448" y="34" width="220" height="138" fill="none" stroke="#cbd5e1" />
            <text id="wrapped-textlength-spacing" x="40" y="62" inline-size="180" textLength="300" lengthAdjust="spacing">wrapped textLength spacing sample should stay measured</text>
            <text id="wrapped-textlength-glyphs" x="450" y="66" inline-size="220" textLength="340" lengthAdjust="spacingAndGlyphs">spacing and glyphs across wrapped content</text>
            <text id="wrapped-positioned-descendant" x="40" y="210" inline-size="250">prefix <tspan x="228 258 288" y="210 236 210" fill="#dc2626">XYZ</tspan><tspan dx="8 3 -2 4"> tail wraps after positioned descendant</tspan></text>
            <text id="wrapped-descendant-deltas" x="450" y="220" inline-size="220">delta <tspan fill="#2563eb" dx="0 10 -4 6" dy="0 -6 6 0">move</tspan> layout continues</text>
            <text id="wrapped-textlength-control" x="40" y="360" inline-size="300">control wrapped line keeps DOM metrics active</text>
          </g>
        """);
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildComplexScriptStretchScene()
    {
        var builder = CreateSvgBuilder(900, 420);
        builder.AppendLine("""
          <defs>
            <path id="arabic-stretch-curve" d="M66 124 C210 42 346 206 492 116 S746 46 838 146" />
            <path id="devanagari-stretch-curve" d="M70 274 C188 196 326 350 452 270 S696 198 830 292" />
          </defs>
          <rect width="900" height="420" fill="#ffffff" />
          <path d="M66 124 C210 42 346 206 492 116 S746 46 838 146" fill="none" stroke="#cbd5e1" />
          <path d="M70 274 C188 196 326 350 452 270 S696 198 830 292" fill="none" stroke="#cbd5e1" />
          <g font-family="'Noto Sans Arabic', 'Noto Sans Devanagari', Noto Sans, Arial, sans-serif" fill="#111827">
            <text id="stretch-arabic-complex" font-size="34" direction="rtl" unicode-bidi="embed" fill="#2563eb" stroke="#0f172a" stroke-width="0.35"><textPath id="stretch-arabic-path" href="#arabic-stretch-curve" method="stretch" startOffset="4%">&#x0645;&#x0631;&#x062D;&#x0628;&#x0627; &#x0628;&#x0627;&#x0644;&#x0639;&#x0627;&#x0644;&#x0645; &#x0648;&#x0627;&#x0644;&#x0646;&#x0635;</textPath></text>
            <text id="stretch-devanagari-complex" font-size="30" fill="#b91c1c" stroke="#7f1d1d" stroke-width="0.3"><textPath id="stretch-devanagari-path" href="#devanagari-stretch-curve" method="stretch" startOffset="2%">&#x0928;&#x092E;&#x0938;&#x094D;&#x0924;&#x0947; &#x092D;&#x093E;&#x0930;&#x0924; &#x0915;&#x094D;&#x0937;&#x0947;&#x0924;&#x094D;&#x0930;</textPath></text>
            <text id="stretch-complex-control" x="54" y="370" font-size="22">complex script stretch benchmark control</text>
          </g>
        """);
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildSharedTextLayoutEngineIntegrationScene()
    {
        var builder = CreateSvgBuilder(920, 460);
        builder.AppendLine("""
          <defs>
            <rect id="shared-shape" x="54" y="48" width="180" height="126" />
            <rect id="shared-subtract" x="54" y="48" width="74" height="48" />
            <path id="shared-inline-line" d="M326 120 C420 62 520 178 620 118" />
            <path id="shared-stretch-curve" d="M90 326 C220 212 370 430 520 318 S742 208 852 336" />
            <filter id="shared-stretch-shadow" x="-20%" y="-30%" width="140%" height="160%">
              <feDropShadow dx="3" dy="4" stdDeviation="2" flood-color="#0f172a" flood-opacity="0.35" />
            </filter>
          </defs>
          <rect width="920" height="460" fill="#ffffff" />
          <use href="#shared-shape" fill="none" stroke="#94a3b8" />
          <use href="#shared-subtract" fill="#fee2e2" stroke="#fca5a5" />
          <path d="M326 120 C420 62 520 178 620 118" fill="none" stroke="#cbd5e1" />
          <path d="M90 326 C220 212 370 430 520 318 S742 208 852 336" fill="none" stroke="#cbd5e1" />
          <g font-family="'Noto Sans Arabic', Noto Sans, Arial, sans-serif" font-size="24" fill="#111827">
            <text id="shared-wrap"
                  shape-inside="url(#shared-shape)"
                  shape-subtract="url(#shared-subtract)"
                  direction="rtl"
                  unicode-bidi="embed"
                  line-break="anywhere">A-B &#x05D0;&#x05D1; C D E F</text>
            <text id="shared-inline-path" x="326" y="120" inline-size="180">
              <textPath id="shared-inline-path-run" href="#shared-inline-line" textLength="132" lengthAdjust="spacingAndGlyphs">A&#x0301;B path</textPath>
            </text>
            <text id="shared-stretch" font-size="32" fill="#2563eb" stroke="#0f172a" stroke-width="0.5" text-decoration="underline" filter="url(#shared-stretch-shadow)">
              <textPath id="shared-stretch-run" href="#shared-stretch-curve" method="stretch" textLength="560" lengthAdjust="spacingAndGlyphs">&#x0645;&#x0631;&#x062D;&#x0628;&#x0627; stretch &#x0928;&#x092E;&#x0938;&#x094D;&#x0924;&#x0947;</textPath>
            </text>
            <text id="shared-control" x="54" y="424" inline-size="300">shared text layout engine regression control</text>
          </g>
        """);
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildPendingTextGapCoverageScene()
    {
        var builder = CreateSvgBuilder(820, 420);
        builder.AppendLine("""
          <defs>
            <path id="tiny-path" d="M0.02 0.50 C0.25 0.05 0.75 0.95 0.98 0.50" transform="translate(70 250) scale(220)" />
            <path id="stretch-curve" d="M420 260 C500 190 600 190 700 260" />
            <filter id="stretch-shadow" x="-20%" y="-30%" width="140%" height="160%">
              <feDropShadow dx="3" dy="4" stdDeviation="2" flood-color="#0f172a" flood-opacity="0.35" />
            </filter>
          </defs>
          <rect width="820" height="420" fill="#ffffff" />
          <g font-family="Noto Sans, Arial, sans-serif" font-size="22" fill="#111827">
            <text id="vertical-inline-pending" x="120" y="42" inline-size="52" writing-mode="tb">A B C D</text>
            <text id="rtl-inline-pending" x="360" y="88" inline-size="92" direction="rtl" unicode-bidi="embed">A B C D</text>
            <text id="tiny-path-text" font-size="18"><textPath id="tiny-path-run" href="#tiny-path" startOffset="4" dy="3">tiny coordinate path sampling coverage</textPath></text>
            <text id="stretch-filter-pending" font-size="26" fill="#2563eb" stroke="#0f172a" stroke-width="0.5" text-decoration="underline" filter="url(#stretch-shadow)">
              <textPath id="stretch-filter-path" href="#stretch-curve" method="stretch" textLength="220" lengthAdjust="spacingAndGlyphs">stretch filter decoration coverage</textPath>
            </text>
          </g>
        """);
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string BuildCombinedAllAreaScene()
    {
        var builder = CreateSvgBuilder(960, 540);
        builder.AppendLine("""
          <defs>
            <linearGradient id="panel-gradient" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0" stop-color="#1d4ed8" />
              <stop offset="0.55" stop-color="#22c55e" />
              <stop offset="1" stop-color="#f59e0b" />
            </linearGradient>
            <radialGradient id="spot-gradient" cx="50%" cy="50%" r="60%">
              <stop offset="0" stop-color="#ffffff" stop-opacity="0.95" />
              <stop offset="1" stop-color="#0f172a" stop-opacity="0.2" />
            </radialGradient>
            <pattern id="tile-pattern" width="18" height="18" patternUnits="userSpaceOnUse">
              <rect width="18" height="18" fill="#f8fafc" />
              <path d="M0 18 L18 0" stroke="#93c5fd" stroke-width="2" />
            </pattern>
            <clipPath id="rounded-clip">
              <rect x="36" y="34" width="408" height="196" rx="24" />
            </clipPath>
            <mask id="fade-mask" maskUnits="userSpaceOnUse" x="500" y="40" width="380" height="180">
              <rect x="500" y="40" width="380" height="180" fill="black" />
              <circle cx="615" cy="128" r="82" fill="white" />
              <rect x="660" y="76" width="174" height="92" fill="white" opacity="0.78" />
            </mask>
            <filter id="soft-shadow" x="-20%" y="-20%" width="140%" height="140%">
              <feDropShadow dx="5" dy="7" stdDeviation="4" flood-color="#0f172a" flood-opacity="0.28" />
            </filter>
            <marker id="arrow" markerWidth="9" markerHeight="9" refX="8" refY="4.5" orient="auto" markerUnits="strokeWidth">
              <path d="M0 0 L9 4.5 L0 9 Z" fill="#dc2626" />
            </marker>
            <symbol id="symbol-badge" viewBox="0 0 64 40" width="64" height="40">
              <rect width="64" height="40" rx="8" fill="#0f766e" />
              <path d="M8 28 L24 12 L36 24 L48 10 L58 28 Z" fill="#ccfbf1" opacity="0.9" />
            </symbol>
            <rect id="combined-text-shape" x="544" y="318" width="170" height="96" />
            <circle id="combined-text-subtract" cx="604" cy="356" r="28" />
            <path id="combined-stretch-curve" d="M540 430 C630 376 738 500 884 424" />
            <path id="caption-path" d="M72 464 C192 390 314 526 438 452 S684 386 850 458" />
          </defs>
          <rect width="960" height="540" fill="#ffffff" />
          <g id="paint-server-area" clip-path="url(#rounded-clip)" filter="url(#soft-shadow)">
            <rect x="36" y="34" width="408" height="196" fill="url(#panel-gradient)" />
            <circle cx="142" cy="118" r="86" fill="url(#spot-gradient)" opacity="0.75" />
            <path d="M50 198 C126 62 250 265 430 78" fill="none" stroke="#ffffff" stroke-width="12" opacity="0.55" />
          </g>
          <g id="mask-area" mask="url(#fade-mask)">
            <rect x="500" y="40" width="380" height="180" fill="url(#tile-pattern)" />
            <circle cx="622" cy="128" r="98" fill="#2563eb" opacity="0.9" />
            <rect x="662" y="78" width="178" height="96" rx="16" fill="#f97316" opacity="0.85" />
          </g>
          <g id="shape-area" transform="translate(48 266)">
            <path d="M0 90 C60 0 122 180 190 62 S310 6 380 116" fill="none" stroke="#111827" stroke-width="4" marker-end="url(#arrow)" />
            <polygon points="430,18 486,82 458,146 384,132 370,54" fill="#7c3aed" opacity="0.8" />
            <polyline points="520,146 556,26 604,138 646,34 704,126" fill="none" stroke="#059669" stroke-width="6" stroke-linejoin="round" />
            <use href="#symbol-badge" x="748" y="48" width="96" height="60" />
          </g>
          <g id="text-area" font-family="Noto Sans, Arial, sans-serif" fill="#111827">
            <text id="combined-title" x="60" y="284" font-size="28">Combined <tspan fill="#2563eb" font-weight="700">all-area</tspan> regression</text>
            <text id="combined-anchor" x="880" y="276" font-size="22" text-anchor="end" letter-spacing="1.5" textLength="310">anchored text layout</text>
            <text id="combined-inline-overflow" x="60" y="332" font-size="20" inline-size="230" text-overflow="ellipsis">inline-size overflow keeps clipping in the combined regression scene</text>
            <text id="combined-shared-wrap" x="544" y="282" font-size="18" inline-size="190" textLength="230" direction="rtl" unicode-bidi="embed">ABC &#x05D0;&#x05D1; <tspan x="544" dy="22" direction="ltr" unicode-bidi="isolate">positioned child</tspan></text>
            <text id="combined-shape-wrap" font-size="18" shape-inside="url(#combined-text-shape)" shape-subtract="url(#combined-text-subtract)" textLength="260" direction="rtl" unicode-bidi="embed" line-break="anywhere">shape A B &#x05D0;&#x05D1; wrap sample</text>
            <text id="combined-stretch-text" font-size="20" fill="#2563eb" stroke="#0f172a" stroke-width="0.35"><textPath id="combined-stretch-path" href="#combined-stretch-curve" method="stretch" textLength="300" lengthAdjust="spacingAndGlyphs">stretch shared all area text</textPath></text>
            <text id="combined-caption-text" font-size="20" fill="#b91c1c"><textPath id="combined-caption-path-text" href="#caption-path" startOffset="5%">curved text path validates glyph placement while all areas render together</textPath></text>
          </g>
        """);
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static StringBuilder CreateSvgBuilder(int width, int height)
    {
        var builder = new StringBuilder();
        builder.AppendLine(FormattableString.Invariant($"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">"""));
        return builder;
    }
}

internal sealed record RegressionValidationScenario(
    string Name,
    string SvgText,
    int RequiredTextCommandCount,
    int RequiredPathCommandCount = 0,
    int RequiredClipCommandCount = 0,
    int RequiredLayerCommandCount = 0,
    string[]? RequiredDomMetricElementIds = null,
    string[]? RequiredTextSourceElementIds = null,
    string[]? RequiredPathSourceElementIds = null,
    string[]? RequiredClipSourceElementIds = null,
    string[]? RequiredLayerSourceElementIds = null);

internal readonly struct ModelValidationScope : IDisposable
{
    public ModelValidationScope(SKPicture? model)
    {
        Model = model ?? throw new InvalidOperationException("Expected a non-null validation model.");
    }

    public SKPicture Model { get; }

    public void Dispose()
    {
    }
}
