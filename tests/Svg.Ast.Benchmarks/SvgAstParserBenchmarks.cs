using System;
using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using Svg.Ast;

[MemoryDiagnoser]
public class SvgAstParserBenchmarks
{
    private readonly List<string> _svgFiles = new();

    [GlobalSetup]
    public void Setup()
    {
        var testSuite = LocateTestSuite();

        foreach (var file in Directory.EnumerateFiles(testSuite, "*.svg", SearchOption.AllDirectories))
        {
            _svgFiles.Add(file);
            if (_svgFiles.Count >= 20)
            {
                break;
            }
        }
    }

    [Benchmark]
    public void ParseFiles()
    {
        foreach (var file in _svgFiles)
        {
            var source = SvgSourceText.FromFile(file);
            _ = SvgAstBuilder.Build(source);
        }
    }
    private static string LocateTestSuite()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "externals", "W3C_SVG_11_TestSuite");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }

            dir = parent.FullName;
        }

        throw new DirectoryNotFoundException("Unable to locate externals/W3C_SVG_11_TestSuite relative to benchmark execution directory.");
    }
}
