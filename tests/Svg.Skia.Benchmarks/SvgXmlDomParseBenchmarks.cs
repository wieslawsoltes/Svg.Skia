using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using BenchmarkDotNet.Attributes;

namespace Svg.Skia.Benchmarks;

public class SvgXmlDomParseBenchmarks
{
    private string svgText = string.Empty;
    private byte[] svgBytes = Array.Empty<byte>();
    private Uri? baseUri;

    [ParamsSource(nameof(Scenarios))]
    public string ScenarioName { get; set; } = string.Empty;

    public IEnumerable<string> Scenarios => SvgLoadPipelineBenchmarkScenarios.Names;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var scenario = SvgLoadPipelineBenchmarkScenarios.Resolve(ScenarioName);
        svgText = scenario.SvgText;
        svgBytes = SvgBenchmarkHelpers.GetUtf8Bytes(svgText);
        baseUri = scenario.BaseUri;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Parse", "FromSvg")]
    public int ParseFromSvgString()
    {
        var document = SvgBenchmarkHelpers.ParseDocument(new SvgLoadPipelineBenchmarkScenario(ScenarioName, svgText, baseUri));
        return document.Children.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Parse", "Stream")]
    public int ParseFromStream()
    {
        using var stream = new MemoryStream(svgBytes, writable: false);
        var document = SvgBenchmarkHelpers.ParseDocument(stream, baseUri);
        return document.Children.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Parse", "XmlReader")]
    public int ParseFromXmlReader()
    {
        using var stringReader = new StringReader(svgText);
        using var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings
        {
            DtdProcessing = SvgDocument.DisableDtdProcessing ? DtdProcessing.Ignore : DtdProcessing.Parse,
            IgnoreWhitespace = false
        });
        var document = SvgBenchmarkHelpers.ParseDocument(xmlReader, baseUri);
        return document.Children.Count;
    }
}
