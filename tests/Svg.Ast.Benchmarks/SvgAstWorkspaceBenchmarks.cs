using System;
using BenchmarkDotNet.Attributes;
using Microsoft.Language.Xml;
using Svg.Ast;
using Svg.Ast.Workspace;

[MemoryDiagnoser]
public class SvgAstWorkspaceBenchmarks
{
    private SvgSourceText _original = null!;
    private SvgSourceText _edited = null!;
    private TextChangeRange[] _changes = Array.Empty<TextChangeRange>();

    [GlobalSetup]
    public void Setup()
    {
        const string template = "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"80\" height=\"30\" fill=\"#ff0000\" /><circle cx=\"40\" cy=\"40\" r=\"12\" fill=\"#00ff00\" /></svg>";
        _original = SvgSourceText.FromString(template);

        var changeStart = template.IndexOf("80", StringComparison.Ordinal);
        var replacement = "120";
        var editedText = template[..changeStart] + replacement + template[(changeStart + 2)..];
        _edited = SvgSourceText.FromString(editedText);
        _changes = new[] { new TextChangeRange(new TextSpan(changeStart, 2), replacement.Length) };
    }

    [Benchmark]
    public void IncrementalUpdate()
    {
        var workspace = new SvgAstWorkspace();
        workspace.AddOrUpdateDocument("doc", _original);
        workspace.ApplyChanges("doc", _edited, _changes);
    }

    [Benchmark]
    public void FullReparseUpdate()
    {
        var workspace = new SvgAstWorkspace();
        workspace.AddOrUpdateDocument("doc", _original);
        workspace.ApplyChanges("doc", _edited, changes: null);
    }
}
