# Svg.Ast Technical Specification

## 1. Source Text Handling

### 1.1 SvgSourceText
- Stores the original SVG buffer as `ReadOnlyMemory<char>` plus optional string backing.
- Provides `Slice(int start, int length)` and `SliceToString` ensuring bounds checks.
- Factory helpers: `FromString`, `FromFile`, `FromStream` (with optional encoding + newline normalization).
- Incremental API: `ParseXmlDocumentIncremental(previousXml, TextChangeRange[])` delegates to `Microsoft.Language.Xml.Parser.ParseIncremental`.

### 1.2 Span Semantics
- All AST nodes refer back to the original `SvgSourceText`; no node stores its own substring except computed diagnostics.
- `Start` + `Length` follow the red-tree spans from `Microsoft.Language.Xml`.

## 2. AST Nodes

### 2.1 SvgAstDocument
- Properties: `SourceText`, `SvgAstElement? RootElement`, `ImmutableArray<SvgAstNode> PrologNodes`, `ImmutableArray<SvgAstNode> EpilogNodes`, `ImmutableArray<SvgDiagnostic> Diagnostics`.
- Prolog/Epilog capture comments, PI, text outside the root element.

### 2.2 SvgAstElement
- Immutable list of `SvgAstAttribute` and `SvgAstNode` children.
- `SvgQualifiedName Name` includes optional prefix and namespace URI resolved by `SvgNamespaceResolver`.
- `SvgXmlSpace XmlSpace` propagates from parents; explicit `xml:space` attributes override it.
- `SvgAstElementFlags` include `SelfClosing`, `HasPresentationAttributes`, `HasStyleAttribute`, `HasId`.
- Helper methods: `TryGetAttribute`, `EnumeratePresentationAttributes` (consults `SvgMetadata`).

### 2.3 SvgAstAttribute
- Stores `Name`, `ValueStart`, `ValueLength`, `SvgAstAttributeFlags` (Presentation, Animatable, CssStyle).
- `GetValueSpan` / `GetValueText` return the raw attribute value (with surrounding quotes stripped).
- `IsLength(out double)` leverages `SvgMetadata` to parse numeric lengths.

### 2.4 Other Nodes
- `SvgAstText`: `bool IsWhitespace` plus inherited xml:space.
- `SvgAstComment`, `SvgAstCData`, `SvgAstProcessingInstruction` (PI stores `Target` + value span).

## 3. Builder Behavior

### 3.1 Namespace Resolution
- `SvgNamespaceResolver` maintains a stack of prefix→URI maps.
- Default scopes include `"" -> http://www.w3.org/2000/svg`, `xml`, and `xlink`.
- Undefined prefixes emit `SvgDiagnostic` with code `SVGASTNS001`.

### 3.2 xml:space Handling
- When encountering `xml:space`, value is normalized to `SvgXmlSpace.Preserve` or `Default`.
- Attribute detection is tolerant of missing namespace info (checks prefix and fully qualified name string).
- Child nodes inherit the last effective xml:space value.

### 3.3 Diagnostics
- `SvgDiagnostic` captures Code, Message, Severity, Start, Length.
- Builder emits diagnostics for undefined namespaces, invalid attributes (based on metadata), missing quotes, etc. Additional validation layers can append diagnostics.

## 4. Metadata Tables

### 4.1 SvgMetadata
- Generated from SVG 1.1 definitions (supplemented by overrides in `SvgMetadata.cs`).
- `Attributes` dictionary includes data type, inheritance flag, and `Animatable` bool.
- `Elements` dictionary lists category, container flag, presentation attributes, additional allowed attributes.

### 4.2 SvgSymbolTable
- Built on demand through `SvgSymbolTable.Build(SvgAstDocument)`.
- Collects:
  - `Ids`: element ID → element
  - `Gradients`: linear/radial gradients
  - `ClipPaths`, `Masks`, `Patterns`
- Builder normalizes ID values by trimming quotes and whitespace.

## 5. Emission Pipeline

### 5.1 Options
- `SvgAstEmissionOptions`: control visitation (`IncludeTextNodes`, `IncludeWhitespaceTextNodes`, `IncludeComments`, etc.), symbol table creation, prolog/epilog traversal, custom items dictionary.

### 5.2 Context
- `SvgAstEmissionContext` holds `Document`, `Options`, `CancellationToken`, `Diagnostics`, and lazily-created services (symbol table, caches, user-defined services).
- Provides helper methods: `ReportDiagnostic`, `GetSymbolTable()`, `TryGetSymbolTable()`, `GetOrAddService<T>()`.

### 5.3 Pipeline + Emitters
- `SvgAstEmissionPipeline` accepts optional pre-processing stages (implementations of `ISvgAstEmissionStage`).
- Emits a `SvgAstEmissionResult<TResult>` containing the document, output, combined diagnostics, and captured symbol table.
- `SvgAstNodeVisitor<TState>` is the base visitor; `SvgAstVisitorEmitter<TResult>` integrates visitor traversal with emission options (respects text/prolog/epilog configuration).

### 5.4 Skia Emitter
- `SvgAstSkiaEmitter` traverses nodes, applies styles via `SvgAstRenderState`, resolves gradients/masks with `SvgAstPaintServerResolver`, and records `SKCanvas` commands using ShimSkiaSharp.
- `SvgAstRenderService.Render()` runs the pipeline and returns `SvgAstEmissionResult<SKPicture?>`.
- Consumers (e.g., `samples/SvgAstPlayground`) convert the resulting picture to SkiaSharp `SKPicture` via `SkiaModel`.

## 6. CLI Playground

`samples/SvgAstPlayground` demonstrates parsing and AST-driven rendering. Usage:

```
dotnet run --project samples/SvgAstPlayground/SvgAstPlayground.csproj -- demo.svg --png out.png
```

Features:

- Prints diagnostics and AST structure.
- Optional `--png` writes an AST-rendered PNG via `SvgAstRenderService`.

## 7. Testing & Benchmarks

- `tests/Svg.Ast.UnitTests` cover source text helpers, namespace handling, xml:space propagation, metadata lookups, and symbol table behavior.
- `tests/Svg.Ast.Benchmarks` parses sample files from `externals/W3C_SVG_11_TestSuite` to capture median parsing time and allocation metrics using BenchmarkDotNet.

## 8. Extensibility Checklist

To add a new AST feature or emitter:

1. Update `SvgMetadata` (generated + overrides) if new elements/attributes are needed.
2. Adjust `SvgAstBuilder` to emit the new node/flag.
3. Add unit tests in `tests/Svg.Ast.UnitTests` for parsing and symbol table behavior.
4. Extend `SvgAstPaintServerResolver` / emitters as needed for rendering.
5. Document the change in `docs/SvgAst.md` and link to additional specs if necessary.
