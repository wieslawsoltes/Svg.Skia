using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using BenchmarkDotNet.Attributes;
using Svg;

namespace Svg.Skia.Benchmarks;

public class SvgCustomAttributeDispatchBenchmarks
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

    private readonly record struct AttributeOperation(SupportedElementKind ElementKind, string AttributeName, string AttributeValue);

    private string svgText = string.Empty;
    private AttributeOperation[] hotAttributeOperations = Array.Empty<AttributeOperation>();
    private AttributeOperation[] pathDataAttributeOperations = Array.Empty<AttributeOperation>();
    private AttributeOperation[] remainingGeometryAttributeOperations = Array.Empty<AttributeOperation>();

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgLoadPipelineBenchmarkScenarios.Names;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgLoadPipelineBenchmarkScenarios.Resolve(ScenarioName);
        svgText = scenario.SvgText;
        hotAttributeOperations = CollectAttributeOperations(svgText, IsHotAttribute);
        pathDataAttributeOperations = CollectAttributeOperations(svgText, IsPathDataAttribute);
        remainingGeometryAttributeOperations = CollectAttributeOperations(svgText, IsRemainingGeometryAttribute);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Parse", "Structure", "Attributes")]
    public int CreateElementsOnly()
    {
        return SvgDocumentCompatibilityLoader.CreateElementsOnly<SvgDocument>(svgText);
    }

    [Benchmark]
    [BenchmarkCategory("Parse", "Structure", "Attributes", "HotSetters")]
    public int ApplyHotUnprefixedAttributesOnly()
    {
        var appliedCount = 0;
        var document = new SvgDocument();
        for (var i = 0; i < hotAttributeOperations.Length; i++)
        {
            var operation = hotAttributeOperations[i];
            var element = CreateElement(operation.ElementKind);
            if (SvgElementFactory.SetPropertyValue(
                element,
                string.Empty,
                operation.AttributeName,
                operation.AttributeValue,
                document))
            {
                appliedCount++;
            }
        }

        return appliedCount;
    }

    [IterationSetup(Target = nameof(ApplyPathDataAttributesOnlyColdSharedCache))]
    public void SetupApplyPathDataAttributesOnlyColdSharedCache()
    {
        SvgElementFactory.ClearPathDataPrototypeCacheForBenchmarks();
    }

    [Benchmark]
    [BenchmarkCategory("Parse", "Structure", "Attributes", "PathData", "ColdSharedCache")]
    public int ApplyPathDataAttributesOnlyColdSharedCache()
    {
        return ApplyPathDataAttributesCore();
    }

    [Benchmark]
    [BenchmarkCategory("Parse", "Structure", "Attributes", "PathData", "WarmSharedCache")]
    public int ApplyPathDataAttributesOnlyWarmSharedCache()
    {
        return ApplyPathDataAttributesCore();
    }

    [Benchmark]
    [BenchmarkCategory("Parse", "Structure", "Attributes", "GeometrySetters")]
    public int ApplyRemainingGeometryAttributesOnly()
    {
        var appliedCount = 0;
        var document = new SvgDocument();
        for (var i = 0; i < remainingGeometryAttributeOperations.Length; i++)
        {
            var operation = remainingGeometryAttributeOperations[i];
            var element = CreateElement(operation.ElementKind);
            if (SvgElementFactory.SetPropertyValue(
                element,
                string.Empty,
                operation.AttributeName,
                operation.AttributeValue,
                document))
            {
                appliedCount++;
            }
        }

        return appliedCount;
    }

    private static AttributeOperation[] CollectAttributeOperations(string svg, Func<string, bool> shouldIncludeAttribute)
    {
        var operations = new List<AttributeOperation>();
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
                if (reader.Prefix.Length != 0 || !shouldIncludeAttribute(reader.LocalName))
                {
                    continue;
                }

                operations.Add(new AttributeOperation(elementKind, reader.LocalName, reader.Value));
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

    private static bool IsHotAttribute(string localName)
    {
        switch (localName)
        {
            case "id":
            case "d":
            case "x":
            case "y":
            case "width":
            case "height":
            case "transform":
                return true;
            default:
                return false;
        }
    }

    private static bool IsRemainingGeometryAttribute(string localName)
    {
        switch (localName)
        {
            case "viewBox":
            case "cx":
            case "cy":
            case "r":
            case "rx":
            case "ry":
                return true;
            default:
                return false;
        }
    }

    private static bool IsPathDataAttribute(string localName)
    {
        return localName == "d";
    }

    private int ApplyPathDataAttributesCore()
    {
        var appliedCount = 0;
        var document = new SvgDocument();
        for (var i = 0; i < pathDataAttributeOperations.Length; i++)
        {
            var operation = pathDataAttributeOperations[i];
            var element = CreateElement(operation.ElementKind);
            if (SvgElementFactory.SetPropertyValue(
                element,
                string.Empty,
                operation.AttributeName,
                operation.AttributeValue,
                document))
            {
                appliedCount++;
            }
        }

        return appliedCount;
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
