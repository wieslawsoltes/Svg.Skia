using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using BenchmarkDotNet.Attributes;
using Svg;

namespace Svg.Skia.Benchmarks;

public class SvgInlineStyleAttributeBenchmarks
{
    private enum SupportedElementKind
    {
        Document,
        Group,
        Path,
        Rectangle,
        Text,
        TextSpan,
        Circle,
    }

    private readonly record struct InlineStyleOperation(SupportedElementKind ElementKind, string StyleText);

    private InlineStyleOperation[] inlineStyleOperations = Array.Empty<InlineStyleOperation>();

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgLoadPipelineBenchmarkScenarios.Names;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgLoadPipelineBenchmarkScenarios.Resolve(ScenarioName);
        inlineStyleOperations = CollectInlineStyleOperations(scenario.SvgText);
    }

    [IterationSetup(Target = nameof(ApplyInlineStylesOnlyWarmSharedCache))]
    public void SetupWarmSharedCache()
    {
        SvgInlineStyleAttributeParser.ClearSharedCacheForBenchmarks();
        _ = ApplyInlineStyles();
    }

    [Benchmark(Baseline = true)]
    [InvocationCount(1)]
    [BenchmarkCategory("Parse", "Structure", "InlineStyle", "Cold")]
    public int ApplyInlineStylesOnlyColdCache()
    {
        SvgInlineStyleAttributeParser.ClearSharedCacheForBenchmarks();
        return ApplyInlineStyles();
    }

    [Benchmark]
    [InvocationCount(1)]
    [BenchmarkCategory("Parse", "Structure", "InlineStyle", "Warm")]
    public int ApplyInlineStylesOnlyWarmSharedCache()
    {
        return ApplyInlineStyles();
    }

    private int ApplyInlineStyles()
    {
        var parser = new SvgInlineStyleAttributeParser();
        var appliedCount = 0;
        for (var i = 0; i < inlineStyleOperations.Length; i++)
        {
            var operation = inlineStyleOperations[i];
            if (parser.ApplyStyles(CreateElement(operation.ElementKind), operation.StyleText))
            {
                appliedCount++;
            }
        }

        return appliedCount;
    }

    private static InlineStyleOperation[] CollectInlineStyleOperations(string svg)
    {
        var operations = new List<InlineStyleOperation>();
        using var stringReader = new StringReader(svg);
        using var reader = XmlReader.Create(stringReader, new XmlReaderSettings
        {
            DtdProcessing = SvgDocument.DisableDtdProcessing ? DtdProcessing.Ignore : DtdProcessing.Parse,
            IgnoreWhitespace = false,
        });

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || !TryMapElementKind(reader.LocalName, out var elementKind))
            {
                continue;
            }

            while (reader.MoveToNextAttribute())
            {
                if (reader.Prefix.Length == 0 &&
                    reader.LocalName.Equals("style", StringComparison.Ordinal))
                {
                    operations.Add(new InlineStyleOperation(elementKind, reader.Value));
                }
            }
        }

        return operations.ToArray();
    }

    private static bool TryMapElementKind(string localName, out SupportedElementKind elementKind)
    {
        switch (localName)
        {
            case "svg":
                elementKind = SupportedElementKind.Document;
                return true;
            case "g":
                elementKind = SupportedElementKind.Group;
                return true;
            case "path":
                elementKind = SupportedElementKind.Path;
                return true;
            case "rect":
                elementKind = SupportedElementKind.Rectangle;
                return true;
            case "text":
                elementKind = SupportedElementKind.Text;
                return true;
            case "tspan":
                elementKind = SupportedElementKind.TextSpan;
                return true;
            case "circle":
                elementKind = SupportedElementKind.Circle;
                return true;
            default:
                elementKind = default;
                return false;
        }
    }

    private static SvgElement CreateElement(SupportedElementKind elementKind)
    {
        switch (elementKind)
        {
            case SupportedElementKind.Document:
                return new SvgDocument();
            case SupportedElementKind.Group:
                return new SvgGroup();
            case SupportedElementKind.Path:
                return new SvgPath();
            case SupportedElementKind.Rectangle:
                return new SvgRectangle();
            case SupportedElementKind.Text:
                return new SvgText();
            case SupportedElementKind.TextSpan:
                return new SvgTextSpan();
            case SupportedElementKind.Circle:
                return new SvgCircle();
            default:
                throw new ArgumentOutOfRangeException(nameof(elementKind), elementKind, null);
        }
    }
}
