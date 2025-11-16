# Svg.Ast Overview

Svg.Ast is a lightweight, high‑fidelity SVG abstract syntax tree designed to replace the heavyweight `SvgDocument` DOM from the legacy SVG library. It captures the raw XML structure (including trivia and spans) so downstream tools—renderers, analyzers, source generators—can operate on SVG markup without paying the cost of building and mutating a DOM.

## Goals

- **Full fidelity**: keep whitespace, trivia, entity references, namespace scopes, and element spans so diagnostics can map precisely to user code.
- **Rendering agnostic**: the AST has no dependency on Skia, GDI+, or Svg.Model. Renderers (e.g., `SvgAstSkiaEmitter`) consume it via visitors.
- **Incremental friendly**: based on `GuiLabs.Language.Xml` (Roslyn’s fast XML parser). `SvgSourceText` exposes helper methods for incremental parsing.

## Core Types

- `SvgSourceText`: wraps SVG text (file/stream/string) while preserving the original buffer. Provides slice helpers and incremental parsing entry points.
- `SvgAstBuilder`: converts the `Microsoft.Language.Xml` syntax tree into `SvgAstDocument`. Handles namespace scopes, `xml:space`, attributes, text nodes, comments, etc.
- `SvgAstDocument`: root node exposing `RootElement`, prolog/epilog trivia, and any diagnostics emitted while parsing.
- `SvgAstElement`, `SvgAstAttribute`, `SvgAstText`, `SvgAstComment`, `SvgAstCData`, `SvgAstProcessingInstruction`: immutable nodes describing the SVG tree.
- `SvgSymbolTable`: optional semantic helper that collects elements by `id` plus reusable resources (gradients, clipPaths, masks, patterns).
- `SvgMetadata`: generated SVG 1.1 tables describing element categories and attribute semantics.
- `SvgAstEmissionPipeline`, `SvgAstEmissionContext`, `SvgAstEmissionOptions`: infrastructure to traverse the AST via visitors and emit another representation (draw commands, code, etc.).
- `SvgAstRenderService` / `SvgAstSkiaEmitter`: sample emitter that translates the AST into `SKPicture` commands compatible with the existing Skia renderer.

## Typical Usage

### Parse an SVG file

```csharp
using Svg.Ast;

var source = SvgSourceText.FromFile("icon.svg");
var document = SvgAstBuilder.Build(source);

if (document.Diagnostics.Length > 0)
{
    foreach (var diag in document.Diagnostics)
    {
        Console.WriteLine($"[{diag.Severity}] {diag.Message} (Start={diag.Start})");
    }
}

var root = document.RootElement;
```

### Walk the AST

```csharp
void Print(SvgAstElement element, int indent = 0)
{
    Console.WriteLine(new string(' ', indent) + element.Name);
    foreach (var attribute in element.Attributes)
    {
        Console.WriteLine(new string(' ', indent + 2) + $"@{attribute.Name} = {attribute.GetValueText()}");
    }
    foreach (var child in element.Children)
    {
        if (child is SvgAstElement childElement)
            Print(childElement, indent + 2);
    }
}

if (document.RootElement is { } root)
    Print(root);
```

### Emit Skia draw commands via AST

```csharp
using Svg.Model.Ast;
using Svg.Skia;
using SkiaSharp;

var renderResult = SvgAstRenderService.Render(document);
if (renderResult.Output is null)
    return;

var skiaModel = new SkiaModel(new SKSvgSettings());
using var picture = skiaModel.ToSKPicture(renderResult.Output);
using var surface = SKSurface.Create(new SKImageInfo(256, 256));
surface.Canvas.DrawPicture(picture);
surface.Canvas.Flush();
using var data = surface.Snapshot().Encode(SKEncodedImageFormat.Png, 90);
data.SaveTo(File.OpenWrite("output.png"));
```

### Incremental Parsing

```csharp
var source = SvgSourceText.FromString(svgText);
var document = SvgAstBuilder.Build(source);

// Later, apply edits and reparse only deltas
var changes = new[] { new TextChangeRange(new TextSpan(10, 5), 3) };
var newSource = SvgSourceText.FromString(editedText);
var incrementalDoc = SvgAstBuilder.BuildIncremental(newSource, document.SourceText.ParseXmlDocument(), changes);
```

## CLI Playground

`samples/SvgAstPlayground` demonstrates parsing and rendering via the AST:

```bash
dotnet run --project samples/SvgAstPlayground/SvgAstPlayground.csproj -- diagram.svg --png output.png
```

It prints diagnostics/tree information and, when `--png` is specified, renders the SVG through `SvgAstRenderService` and saves a PNG using SkiaSharp.

## Diagnostics

`SvgAstBuilder` and `SvgAstValidator` emit structured diagnostics that include a severity, code, message, and source span. The most common codes are:

| Code          | Description                                                          |
|---------------|----------------------------------------------------------------------|
| `SVGASTATTR001` | Attribute not allowed on the current element (metadata mismatch).    |
| `SVGASTNS001`   | Undefined namespace prefix encountered while creating qualified names. |
| `SVGASTID001`   | Duplicate `id` attribute detected in the document.                   |
| `SVGASTREF001`  | Attribute references an undefined element id.                        |
| `SVGASTREF002`  | Attribute references its own element (self-reference).               |
| `SVGASTVAL001`  | Attribute value is not a valid length token.                         |
| `SVGASTVAL002`  | Attribute requires a non-negative numeric value (e.g., `width`, `r`).|

The playground prints diagnostics in the format `[Severity] CODE: message (Start, Length)` so issues can be mapped back to source text quickly. Renderers such as `SvgAstRenderService` include these diagnostics in their emission results, enabling tooling to block on fatal errors or surface warnings to the end user.

## Workspace & Editor Integration

`SvgAstWorkspace` provides a lightweight document manager that caches parsed syntax trees, AST nodes, and symbol tables. It exposes a `DocumentChanged` event that fires whenever a document is added or updated (incremental parsing is used automatically when `TextChangeRange[]` is provided). Consumers can subscribe to receive the new diagnostics and reuse symbol tables without reparsing.

```csharp
var workspace = new SvgAstWorkspace();
workspace.DocumentChanged += (_, args) =>
{
    Console.WriteLine($"Document {args.DocumentId} changed. Incremental: {args.UsedIncrementalParser}");
    foreach (var diag in args.Diagnostics)
    {
        Console.WriteLine($"  {diag.Code}: {diag.Message}");
    }
};

var doc = workspace.AddOrUpdateDocument("diagram", SvgSourceText.FromFile("diagram.svg"));
workspace.ApplyChanges("diagram", SvgSourceText.FromFile("diagram.edited.svg"));
```

For render-ready previews, `SvgAstWorkspaceRenderer` (in `Svg.Model.Ast`) listens to the workspace event and runs `SvgAstRenderService`. It raises its own `DocumentRendered` event that supplies the render result along with the originating workspace event arguments.

```csharp
using var renderer = new SvgAstWorkspaceRenderer(workspace);
renderer.DocumentRendered += (_, args) =>
{
    if (args.RenderResult.Output is { } picture)
    {
        Console.WriteLine($"Rendered {args.WorkspaceEventArgs.DocumentId}: commands={picture.Commands?.Count ?? 0}");
    }
};

workspace.AddOrUpdateDocument("diagram", SvgSourceText.FromFile("diagram.svg"));
```

These hooks allow editor integrations to react to changes immediately, display diagnostics inline, or trigger live previews without rebuilding the entire AST.

## Legacy DOM Emitter

Some consumers still rely on the traditional `SvgDocument` object model. `SvgAstDomEmitter` reconstructs that DOM without reparsing XML by walking the AST, instantiating the strongly-typed `SvgElement` classes, and replaying attributes/styles. `SvgAstDomService.CreateDocument(document)` wraps this emitter behind the same validation pipeline as the renderer. This lets you feed AST output into existing DOM-based workflows (e.g., custom renderers, code generators) while keeping the new parser as the single source of truth.

## Code Generation Emitters

`SvgAstSkiaPathCodeEmitter` shows how to build code-gen friendly emitters. It walks the AST and produces C# snippets that call into `SkiaSharp.SKPath` APIs (e.g., `AddRect`, `AddCircle`, `AddPoly`). This demonstrates how AST visitors can target entirely different outputs (geometry factories, XAML, drawing DSLs). To use it:

```csharp
var pipeline = new SvgAstEmissionPipeline(new[] { new SvgAstValidationStage() });
var result = pipeline.Emit(document, new SvgAstSkiaPathCodeEmitter("path"));
File.WriteAllText("generated-path.cs", result.Output);
```

Implementing additional emitters follows the same pattern—derive from `SvgAstVisitorEmitter<T>`, override `VisitElement`, and translate AST nodes into your runtime’s draw commands or code.

## Tests & Benchmarks

- `tests/Svg.Ast.UnitTests` exercises source text helpers, namespace/xml:space handling, metadata, and symbol table behavior.
- `tests/Svg.Ast.Benchmarks` uses BenchmarkDotNet to parse W3C SVG test files and track throughput/allocations. `SvgAstWorkspaceBenchmarks` compares incremental workspace updates versus full rebuilds to catch regressions in the editor loop. Run `dotnet run -c Release --project tests/Svg.Ast.Benchmarks` locally or in CI to capture the results (they are stored under `BenchmarkDotNet.Artifacts`). Use these numbers as a gate when touching parsing/workspace code.

## Next Steps

- Expand AST emitters (e.g., DOM builder, codegen) on top of `SvgAstEmissionPipeline`.
- Improve diagnostics with richer codes/spans.
- Publish `Svg.Ast` as its own NuGet once APIs stabilize.

### Further Reading

- [SvgAst Implementation RFC](SvgAst-RFC.md)
- [SvgAst Technical Specification](SvgAst-Spec.md)
