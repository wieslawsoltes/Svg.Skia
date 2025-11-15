# SVG AST Options

## Goals and Constraints
- Provide a **full SVG abstract syntax tree (AST)** that mirrors the XML source with high fidelity (whitespace, entity usage, spans, namespaces), similar to what Avalonia gets from the XamlX pipeline.
- Keep the AST layer independent from rendering so it can serve multiple consumers (runtime loading, analyzers, source generators such as `src/Svg.SourceGenerator.Skia/SvgSourceGenerator.cs`).
- Avoid the `SvgDocument` dependency on `System.Drawing`/`XmlDocument` to keep memory usage low and remove the implicit GDI+ requirement that currently sits in `externals/SVG/Source/SvgDocument.cs`.

## Current Parsing Pipeline
- `SvgService.Open(...)` (see `src/Svg.Model/Services/SvgService.cs`) simply forwards to `SvgDocument.Open<T>` and receives an object model built on top of `System.Xml.XmlReader`. The resulting DOM does not expose trivia, raw attribute text, or incremental update hooks.
- `SvgService.ToModel` walks the DOM and emits `SkiaModel` commands for drawing. Any consumer (runtime drawing or code generation) must always pay the cost of building the heavy DOM first.
- The DOM is tightly coupled to the `Svg` reference implementation located under `externals/SVG`, which brings in CSS parsing, entity lookup, ID management, and other behaviors that are unnecessary when we only need a syntax tree.

## What XamlX Already Gives Us
- XamlX front-ends XAML markup with `GuiLabsXamlParser`, which in turn uses `Microsoft.Language.Xml` from `/Users/wieslawsoltes/GitHub/XmlParser-Roslyn`. See `/Users/wieslawsoltes/GitHub/XamlX/src/XamlX.Parser.GuiLabs/GuiLabsXamlParser.cs` for the glue code that maps the XML syntax tree into `XamlAst` nodes.
- The AST layer in `/Users/wieslawsoltes/GitHub/XamlX/src/XamlX/Ast/Xaml.cs` is geared toward constructing CLR objects: every node eventually maps to an `IXamlType`, `IXamlProperty`, markup extensions, etc. Transform passes assume XAML concepts such as `[Content]` properties, type converters, and deferred content.
- `Microsoft.Language.Xml` (aka XmlParser-Roslyn) provides a **full-fidelity, immutable, incremental** syntax tree (`README.md` in `/Users/wieslawsoltes/GitHub/XmlParser-Roslyn`). It already captures everything needed for SVG—tokens, trivia, namespaces, character spans—and is the piece that makes XamlX fast.

## Option 1 – Adapt the whole XamlX AST/Visitor pipeline
**Approach.** Reuse `GuiLabsXamlParser` as-is, then repurpose the `XamlAst*` nodes to describe SVG elements/attributes by swapping the type system (`XamlLanguageTypeMappings`, `IXamlType`, `IXamlProperty`) with SVG-specific descriptors.

**Upsides**
- All plumbing for incremental parsing, namespace handling, directive handling, and visitor passes already exists; diagnostics and line information come for free.
- We could piggyback on Avalonia’s experience with AST visitors, code generation, and transformations (e.g., reuse the pipeline that currently converts `XamlAstObjectNode` into IL to instead convert into `Svg.Model`).

**Downsides**
- `XamlAst` is inherently object/CLR-centric. SVG is declarative XML without concepts such as constructors, `[Content]`, markup extensions, or property setters, so most `XamlAst` nodes would become “dead weight” or would need extensive rewrites.
- The type system abstraction expects there to be an `IXamlTypeSystem` to resolve CLR metadata. For SVG we would be mapping to spec-defined elements/attributes, not runtime types, so we would likely fight the abstraction at every step.
- Large dependency footprint: pulling XamlX into the core library introduces Cecil emitters, runtime helpers, and Avalonia-specific features we do not need.

**Implementation sketch**
1. Fork the `XamlX` AST so `XamlAstObjectNode`/`XamlAstXmlTypeReference` become `SvgAstElementNode`/`SvgAstQualifiedName`.
2. Replace the type system layer with a registry generated from the SVG 1.1 element/attribute tables.
3. Remove or stub features that have no SVG equivalent (markup extensions, deferred content, type converters).
4. Write visitors that translate the new AST into `Svg.Model` drawables.

**Risk level:** High. This effectively turns XamlX into a general XML AST, which it was never designed to be.

## Option 2 – Build a dedicated SvgAst on top of Microsoft.Language.Xml
**Approach.** Use `Microsoft.Language.Xml` directly (the same parser XamlX uses) to obtain the green/red tree, then run a thin projection layer that converts `XmlNodeSyntax` into purpose-built `SvgAst*` records.

**Upsides**
- Keeps the **high-performance parser** (full-fidelity, immutable, incremental) without inheriting the CLR-centric baggage from XamlX. The parser is dependency-free, so it can live inside `Svg.Model`.
- We control the AST surface. Nodes can expose exactly what Svg.Skia needs: element name, attribute spans, raw text, resolved numeric tokens, references into the original `ReadOnlyMemory<char>`, etc.
- Easy to add domain-specific caches such as pre-parsed numeric values, presentation attribute “buckets”, or references to CSS blocks while keeping the original trivia for diagnostics or source generators.
- Incremental parsing falls out naturally: `Microsoft.Language.Xml` already supports tree reuse, so future editor or hot-reload scenarios can re-emit only the changed portion of the model.

**Downsides**
- Requires authoring a new AST plus visitors. We need to encode SVG semantics ourselves (element categories, attribute inheritance, xlink normalization).
- Some features still require other subsystems (CSS cascade, presentation attributes, animation timing) which means new infrastructure beyond pure parsing.

**Implementation sketch**
1. Add a `SvgAstDocument` that wraps the `XmlDocumentSyntax` root and stores the source buffer.
2. Define lightweight nodes (`SvgAstElement`, `SvgAstAttribute`, `SvgAstText`, `SvgAstComment`) that keep:
   - Qualified names (prefix/namespace/local name).
   - Offsets/lengths into the buffer for zero-copy string slices.
   - Flags describing whether whitespace was preserved, whether the attribute is presentation/style, etc.
3. Create projection visitors that walk the XmlParser tree and emit the SvgAst nodes. Because XmlParser already tokenizes attributes and text, the visitor can pool objects and avoid redundant allocations.
4. Build adapters that consume `SvgAstDocument` and materialize `SvgFragment`/`SvgElement` objects lazily when `Svg.Model` actually needs to draw.
5. Eventually expose the AST publicly so analyzers/source generators can reason about SVG without converting to the runtime DOM.

**Risk level:** Medium/Low. The parser is stable, and the remaining work is under our control.

## Option 3 – Hybrid streaming AST layered over SvgDocument
**Approach.** Keep `SvgDocument` to avoid regressing existing behavior, but add a fast `XmlReader`/`Utf8Parser`-based scanner that emits a lightweight AST in parallel while the DOM is materialized.

**Upsides**
- Minimal disruption to the current pipeline—rendering code can continue to use `SvgDocument` until the new AST is production-ready.
- We can specialize the streaming parser for performance-critical sections (paths, transforms) without committing to a fully new tree representation immediately.

**Downsides**
- Still pays the cost of building the full `SvgDocument`; we merely add *more* work instead of replacing it.
- Hard to keep the streaming AST and the DOM consistent (IDs, url() references, base URIs) because they are generated in different passes.
- Does not buy us incremental parsing or zero-copy spans; the underlying `XmlReader` already normalizes text and loses trivia.

**Risk level:** Medium. It is the safest migration path but offers the smallest long-term gain.

## Recommendation and Next Steps
1. **Prototype Option 2.** Start by referencing `/Users/wieslawsoltes/GitHub/XmlParser-Roslyn` from `Svg.Model` and building a `SvgAstDocument` wrapper. Measure parsing throughput against the current `SvgDocument.Open` path using `W3C_SVG_11_TestSuite`.
2. **Bridge to existing consumers.** Implement a `SvgAstToModelVisitor` that feeds today’s `DrawableFactory` to prove that the AST can drive actual rendering without the legacy DOM.
3. **Define public contracts.** Once stable, expose the AST (readonly structs/records) so other tools (source generators, analyzers) can consume it without pulling in rendering dependencies.
4. Keep Option 1 in mind only if we later decide to compile SVG into code or bytecode; at that point the rest of the XamlX pipeline (emitters, transformations) might become relevant. Until then, the simpler dedicated AST gives us high performance with less churn.

## Implementation Plan for Dedicated SvgAst
1. [ ] **Create standalone Svg.Ast project.**  
   1.1 [x] Add a new `src/Svg.Ast/Svg.Ast.csproj` library containing the AST implementation with zero references to other `Svg.Skia` projects.  
   1.2 [x] Reference the `GuiLabs.Language.Xml` NuGet package (which exposes `Microsoft.Language.Xml`) via `Directory.Packages.props`, keeping the project otherwise dependency-free.
2. [ ] **Create source text abstraction.**  
   2.1 [x] Implement a `SvgSourceText` type that stores the original SVG content as `ReadOnlyMemory<char>`, encoding info, and slicing helpers.  
   2.2 [x] Provide factory methods for file/stream/string inputs that normalize line endings and integrate with `Microsoft.Language.Xml.Parser.Parse`.
3. [ ] **Design SvgAst node contracts.**  
   3.1 [x] Add `SvgAstDocument`, `SvgAstElement`, `SvgAstAttribute`, `SvgAstText`, `SvgAstComment`, `SvgAstCData`, and `SvgAstProcessingInstruction` types capturing qualified names, offsets, xml:space state, and cached flags.  
   3.2 [x] Favor readonly structs/sealed records with immutable collections (e.g., `ImmutableArray<int>`) to keep allocations predictable.
4. [ ] **Implement AST builder.**  
   4.1 [x] Write a `SvgAstBuilder` that traverses `Microsoft.Language.Xml` syntax nodes and projects them into SvgAst types while maintaining parent links and namespace scopes.  
   4.2 [x] Borrow namespace resolution concepts from `XamlX.Parser.GuiLabs.GuiLabsXamlParser`, adapting them to SVG defaults (`xmlns`, `xmlns:xlink`, xml prefix handling).  
   4.3 [x] Expose hooks for incremental parsing by caching XmlParser green nodes and rebuilding only changed subtrees.
5. [ ] **Add semantic metadata tables.**  
   5.1 [x] Generate metadata (JSON or source) describing SVG element categories, attribute types, inheritance, and animatable flags per SVG 1.1.  
   5.2 [x] Extend `SvgAstElement`/`SvgAstAttribute` with helpers such as `TryGetLength(string name)` or `EnumeratePresentationAttributes()` that leverage the metadata.  
5.3 [x] Implement an automated generator that consumes `externals/SVG/Source/Resources/svg11.dtd` (or other machine-readable spec data) to produce the metadata, keeping it in sync with SVG 1.1.
6. [ ] **Diagnostics and validation utilities.**  
   6.1 [x] Implement error-reporting structures that capture parser diagnostics, unresolved namespaces, or invalid attribute usages with precise source spans.  
   6.2 [x] Provide validation helpers (e.g., schema checks, attribute normalization) operating purely on the AST.  
6.3 [x] Add basic semantic analysis (id/IRI tracking, duplicate ids, circular references) to mirror XamlX’s validation passes.
7. [ ] **Semantic and binding infrastructure.**  
   7.1 [x] Introduce symbol tables for IDs, gradients, clip-paths, and other referenced elements so AST consumers can resolve links without a DOM.  
   7.2 [x] Design an emission pipeline (e.g., visitor pattern) that can walk the AST and produce either runtime draw commands or codegen output, similar to XamlX’s IL emitters.  
       - Added a `SvgAstEmissionPipeline` orchestrator plus `ISvgAstEmissionStage`/`ISvgAstEmitter<TResult>` contracts so emitters can plug in pre-processing passes or target-specific outputs.  
       - Added `SvgAstEmissionContext`/`SvgAstEmissionOptions` to manage shared services, diagnostics, symbol tables, and traversal filters from a single place.  
       - Introduced `SvgAstNodeVisitor<TState>` and `SvgAstVisitorEmitter<TResult>` helpers so runtime and codegen emitters can walk the AST with consistent option-aware behavior.  
   7.3 [x] Implement concrete emitters that translate the AST into drawing commands compatible with the existing `Svg.Skia` rendering model (e.g., creating drawables/SKPicture pipelines) so the AST can power real rendering without the legacy DOM.
       - Added a first `SvgAstSkiaEmitter` plus `SvgAstRenderService` in `Svg.Model` that walk the AST, maintain a style/transform stack, and record `SKPicture` commands for common shapes without touching the legacy DOM.  
       - Built supporting infrastructure (render options, render state, color/transform/geometry helpers) so additional emitters or shape types can plug in without reimplementing parsing logic.  
8. [ ] **Testing and performance validation.**  
   8.1 [x] Write unit tests within the `Svg.Ast` project covering namespace handling, entity preservation, whitespace control, metadata helpers, incremental updates, and diagnostics.  
       - Added a dedicated `tests/Svg.Ast.UnitTests` project targeting net9.0 with coverage for `SvgSourceText`, namespace resolution/xml:space propagation, metadata helpers, and `SvgSymbolTable`.  
       - `dotnet test Svg.Skia.sln -c Release --filter Svg.Ast.UnitTests` now passes, ensuring the AST layer is validated independently.  
 8.2 [x] Benchmark the parser on representative SVG files (e.g., `externals/W3C_SVG_11_TestSuite`) to track allocations and latency; include regression tests for large assets.
       - Added `tests/Svg.Ast.Benchmarks/Svg.Ast.Benchmarks.csproj` using BenchmarkDotNet to parse a subset of `externals/W3C_SVG_11_TestSuite` files via `SvgAstBuilder` while tracking allocations.
       - Running `dotnet run -c Release --project tests/Svg.Ast.Benchmarks/Svg.Ast.Benchmarks.csproj` now produces summary artifacts under `BenchmarkDotNet.Artifacts` (current run: ~3.75 ms per file, ~8.6 MB allocations on M3 Pro/net9.0).
9. [ ] **Documentation, tooling, and packaging.**  
 9.1 [x] Document the public API surface (README plus XML docs) explaining how to construct and traverse `SvgAstDocument`, run validations, and emit code.  
       - Added `docs/SvgAst.md` covering the AST goals, core types, and sample code for parsing, traversing, incremental updates, and rendering through the AST emitter.
       - Updated the root `README.md` with a short “Svg.Ast” section pointing to the new doc and listing the unit tests/benchmarks/playground entry points.
 9.2 [x] Prepare packaging instructions (NuGet metadata, sample usage) so the AST library can be consumed independently once stabilized.  
       - Updated `src/Svg.Ast/Svg.Ast.csproj` with NuGet-friendly metadata (package description/readme, XML docs) and documented pack steps.
       - Added `docs/SvgAst-Packaging.md` with end-to-end instructions for running tests, packing, verifying the nupkg, publishing, and consuming `Svg.Ast`.
 9.3 [x] Provide CLI tooling (e.g., to dump AST, run validations, or generate metadata) akin to XamlX’s analyzers for developer workflows.
       - Added `tools/SvgAst.Cli` (System.CommandLine-based) with `parse`, `dump`, `validate`, and `render` subcommands that operate entirely on the AST pipeline.
       - README and `docs/SvgAst.md` now reference the CLI alongside the playground with example commands.

## Next Phases

### Phase 10 – Broaden Emission Coverage
1. Extend `SvgAstSkiaEmitter` with gradient, pattern, mask, and text layout support (glyph runs, alignment, `xml:space` handling).
2. Add clip-path/mask stack helpers to `SvgAstRenderService` so recursive emitters can reuse consistent state transitions.
3. Capture regression assets (e.g., gradients, complex text, filters) and add `SvgAstRenderServiceTests` that compare against golden PNG hashes.

### Phase 11 – Semantic Validation & Diagnostics
1. Introduce `SvgAstValidator` (new stage in `SvgAstEmissionPipeline`) that walks the tree, resolves IRIs/IDs, and verifies attribute ranges/enums.
2. Define diagnostic codes (`SVGASTVALxxx`) for duplicate IDs, unresolved references, invalid numeric ranges, etc., and surface them in the playground.
3. Add focused unit tests under `tests/Svg.Ast.UnitTests/Diagnostics` covering namespace errors, attribute bounds, and symbol resolution failures.

### Phase 12 – Incremental/Workspace API
1. Prototype `SvgAstWorkspace` that manages documents, caches symbol tables, and exposes incremental updates (`ApplyChanges` returning new diagnostics + render deltas).
2. Provide adapters for editor integrations (event hooks for diagnostics/render-ready picture) and document in `SvgAst.md`.
3. Benchmark incremental updates vs. full rebuilds using `tests/Svg.Ast.Benchmarks` and add regression gates.

### Phase 13 – Alternate Emitters
1. Implement a DOM emitter that reconstructs `SvgDocument`/`SvgElement` instances purely from the AST to keep legacy consumers working without XML parsing.
2. Add codegen emitters (e.g., Skia `SKPath` builder, Avalonia `Geometry` factory) demonstrating how the AST can target other runtimes; document extension points.
3. Create sample projects showing mixed emitter usage (AST → DOM + AST → Skia) to validate plug-and-play scenarios.

### Phase 14 – Tooling & Benchmarks
1. Upgrade `samples/SvgAstPlayground` into an interactive GUI/web target with side-by-side AST, diagnostics, and render preview panes.
2. Expand `tests/Svg.Ast.Benchmarks` with W3C suite coverage, large icon sets, and incremental-edit simulations (track allocations + latency trendlines).
3. Add CI checks (GitHub Actions/Azure Pipelines) that run the benchmarks in comparison mode and fail on regressions beyond tolerated thresholds.
