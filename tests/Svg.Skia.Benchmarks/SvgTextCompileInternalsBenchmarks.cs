using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using ShimSkiaSharp;
using Svg.Model;

namespace Svg.Skia.Benchmarks;

public class SvgTextCompileInternalsBenchmarks
{
    private delegate List<string> SplitCodepointsDelegate(string text);
    private delegate IReadOnlyList<string> SplitCodepointsReadOnlyDelegate(string text);
    private delegate float MeasureNaturalTextAdvanceDelegate(SvgTextBase svgTextBase, string text, SKRect geometryBounds, ISvgAssetLoader assetLoader);
    private delegate float[] MeasureNaturalCodepointAdvancesDelegate(SvgTextBase svgTextBase, IReadOnlyList<string> codepoints, SKRect geometryBounds, ISvgAssetLoader assetLoader);

    private sealed record TextFragment(SvgTextBase StyleSource, string Text);

    private static readonly SplitCodepointsDelegate s_splitCodepoints =
        CreateDelegate<SplitCodepointsDelegate>("SplitCodepoints");

    private static readonly SplitCodepointsReadOnlyDelegate s_splitCodepointsReadOnly =
        CreateDelegate<SplitCodepointsReadOnlyDelegate>("SplitCodepointsReadOnly");

    private static readonly MeasureNaturalTextAdvanceDelegate s_measureNaturalTextAdvance =
        CreateDelegate<MeasureNaturalTextAdvanceDelegate>("MeasureNaturalTextAdvance");

    private static readonly MeasureNaturalCodepointAdvancesDelegate s_measureNaturalCodepointAdvances =
        CreateDelegate<MeasureNaturalCodepointAdvancesDelegate>("MeasureNaturalCodepointAdvances");

    private SkiaSvgAssetLoader? assetLoader;
    private SKRect geometryBounds;
    private TextFragment[] textFragments = Array.Empty<TextFragment>();
    private IReadOnlyList<string>[] splitCodepoints = Array.Empty<IReadOnlyList<string>>();

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgLoadPipelineBenchmarkScenarios.Names
        .Where(static name =>
            name.Contains("text", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("spacing", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("file:", StringComparison.OrdinalIgnoreCase));

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgLoadPipelineBenchmarkScenarios.Resolve(ScenarioName);
        var document = SvgBenchmarkHelpers.ParseDocument(scenario);
        geometryBounds = SvgBenchmarkHelpers.GetDocumentViewport(document);
        assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));

        var fragments = new List<TextFragment>();
        foreach (var text in document.Descendants().OfType<SvgText>())
        {
            if (!string.IsNullOrWhiteSpace(text.Content))
            {
                fragments.Add(new TextFragment(text, text.Content));
            }
        }

        foreach (var span in document.Descendants().OfType<SvgTextSpan>())
        {
            if (!string.IsNullOrWhiteSpace(span.Content))
            {
                fragments.Add(new TextFragment(span, span.Content));
            }
        }

        if (fragments.Count == 0)
        {
            throw new InvalidOperationException($"Scenario '{ScenarioName}' did not contain any text fragments.");
        }

        textFragments = fragments.ToArray();
        splitCodepoints = textFragments
            .Select(static fragment => s_splitCodepointsReadOnly(fragment.Text))
            .ToArray();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Compile", "Text", "Internals", "SplitCodepoints")]
    public int SplitCodepointsAcrossFragments()
    {
        var totalCount = 0;
        for (var i = 0; i < textFragments.Length; i++)
        {
            totalCount += s_splitCodepoints(textFragments[i].Text).Count;
        }

        return totalCount;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "Internals", "MeasureNaturalTextAdvance")]
    public float MeasureNaturalTextAdvanceAcrossFragments()
    {
        var totalAdvance = 0f;
        for (var i = 0; i < textFragments.Length; i++)
        {
            var fragment = textFragments[i];
            totalAdvance += s_measureNaturalTextAdvance(fragment.StyleSource, fragment.Text, geometryBounds, assetLoader!);
        }

        return totalAdvance;
    }

    [Benchmark]
    [BenchmarkCategory("Compile", "Text", "Internals", "MeasureNaturalCodepointAdvances")]
    public int MeasureNaturalCodepointAdvancesAcrossFragments()
    {
        var totalCount = 0;
        for (var i = 0; i < textFragments.Length; i++)
        {
            totalCount += s_measureNaturalCodepointAdvances(
                textFragments[i].StyleSource,
                splitCodepoints[i],
                geometryBounds,
                assetLoader!).Length;
        }

        return totalCount;
    }

    private static TDelegate CreateDelegate<TDelegate>(string methodName)
        where TDelegate : Delegate
    {
        var invoke = typeof(TDelegate).GetMethod(nameof(Action.Invoke))
            ?? throw new InvalidOperationException($"Could not locate {typeof(TDelegate).Name}.Invoke.");
        var parameterTypes = invoke.GetParameters()
            .Select(static parameter => parameter.ParameterType)
            .ToArray();
        var method = typeof(SvgSceneTextCompiler).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        if (method is null)
        {
            throw new InvalidOperationException($"Could not locate SvgSceneTextCompiler.{methodName}.");
        }

        if (method.ReturnType != invoke.ReturnType)
        {
            throw new InvalidOperationException(
                $"SvgSceneTextCompiler.{methodName} return type '{method.ReturnType}' does not match delegate return type '{invoke.ReturnType}'.");
        }

        return method.CreateDelegate<TDelegate>();
    }
}
