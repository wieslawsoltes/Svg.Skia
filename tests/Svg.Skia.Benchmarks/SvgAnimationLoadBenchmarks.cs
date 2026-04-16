using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Svg;

namespace Svg.Skia.Benchmarks;

public class SvgAnimationLoadBenchmarks
{
    private SvgDocument? parsedDocument;

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgLoadPipelineBenchmarkScenarios.Names;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgLoadPipelineBenchmarkScenarios.Resolve(ScenarioName);
        parsedDocument = SvgBenchmarkHelpers.ParseDocument(scenario);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Load", "Animation", "Detection")]
    public bool DetectAnimationElementsInParsedDocument()
    {
        return ContainsAnimationElements(parsedDocument!);
    }

    [Benchmark]
    [BenchmarkCategory("Load", "Animation", "Controller")]
    public bool CreateAnimationControllerForParsedDocument()
    {
        using var controller = new SvgAnimationController(parsedDocument!);
        return controller.HasAnimations;
    }

    [Benchmark]
    [BenchmarkCategory("Load", "Animation", "StaticLoad", "FreshInstance")]
    public float LoadParsedDocumentIntoFreshSkSvg()
    {
        using var svg = new SKSvg();
        using var picture = svg.FromSvgDocument(parsedDocument!);
        return picture?.CullRect.Width ?? 0f;
    }

    private static bool ContainsAnimationElements(SvgElement root)
    {
        var stack = new Stack<SvgElement>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current is SvgAnimationElement)
            {
                return true;
            }

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }

        return false;
    }
}
