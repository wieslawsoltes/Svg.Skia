# Svg.Ast Implementation RFC

## 1. Motivation

The legacy Svg.Skia pipeline depends on the upstream `SvgDocument` DOM, which couples rendering with heavyweight XML objects, eager validation, and `System.Drawing`-style semantics. The DOM loses trivia (whitespace, entity references, exact spans) and can only be mutated wholesale. We needed:

- A fast, immutable AST that mirrors the original SVG source.
- A parser that can support incremental updates for editors and tooling.
- An emission pipeline capable of producing render commands or code without rebuilding the DOM.

## 2. Goals

1. **Full Fidelity** – preserve namespaces, xml:space, trivia, and source spans so diagnostics map precisely to the original file.
2. **Renderer Independence** – keep the AST free of Skia or Svg.Model dependencies. Renderers consume it via emitters.
3. **Incremental Parsing** – leverage `GuiLabs.Language.Xml` (Roslyn’s XML parser) so editors can reparse deltas.
4. **Composable Emission** – adopt a visitor/pipeline model that can produce different outputs (Skia commands, DOM builders, codegen) on top of the same AST.

## 3. Non-Goals

- Re-implementing the entire SVG spec (filter semantics, CSS cascade) inside the AST.
- Providing a mutable DOM replacement. Consumers are expected to treat nodes as immutable.
- Replacing the `Svg` reference library in existing runtimes until parity is proven.

## 4. Architecture Overview

```
SvgSourceText -> Microsoft.Language.Xml -> SvgAstBuilder -> SvgAstDocument
                                                |                 |
                                    SvgMetadata / SvgSymbolTable  |
                                                       \          /
                                                         SvgAstEmissionPipeline
                                                                 |
                                                           SvgAstEm itter(s)
```

### 4.1 Parsing stages

1. `SvgSourceText` loads text (file/stream/string) and normalizes line endings.
2. `SvgSourceText.ParseXmlDocument()` invokes `Microsoft.Language.Xml.Parser` to produce the green/red XML tree.
3. `SvgAstBuilder` walks the XML syntax and projects it into strongly-typed AST nodes while maintaining namespace scopes and `xml:space` information.
4. Diagnostics are recorded for unresolved prefixes, malformed attributes, or metadata violations.

### 4.2 AST Layer

- Nodes (`SvgAstElement`, `SvgAstAttribute`, etc.) capture start/length positions in the source buffer.
- `SvgMetadata` supplies SVG 1.1 tables (element categories, attribute data types, presentation attributes).
- `SvgSymbolTable` can be computed lazily to resolve references by ID (gradients, masks, clipPaths, etc.).

### 4.3 Emission Layer

- `SvgAstEmissionOptions` control traversal (text nodes, prolog/epilog, symbol table caching).
- `SvgAstEmissionPipeline` runs optional pre-processing stages and executes an `ISvgAstEmitter<TResult>`.
- `SvgAstVisitorEmitter<TResult>` provides a base class for visitor-style emitters.
- `SvgAstSkiaEmitter` demonstrates the pipeline by translating elements into Skia draw commands (`SvgAstRenderService` wraps it for easy consumption).

## 5. Key Decisions

1. **Immutable Nodes** – ensures AST can be shared across threads and reused between passes.
2. **Span-Based Text** – nodes refer back to `SvgSourceText` slices instead of duplicating strings.
3. **Metadata-Driven Validation** – diagnostics lean on generated tables to flag invalid attributes.
4. **Service Context** – `SvgAstEmissionContext` acts as a DI container for pipelines (symbol tables, caching, custom services).

## 6. Future Work

- Additional emitters (DOM builder for compatibility, code generators for source generators).
- Richer diagnostics and schema validation.
- CSS integration (link AST attributes with parsed style blocks).
- Public NuGet packaging once the API stabilizes.

See `docs/SvgAst-Spec.md` for the low-level technical specification and `docs/SvgAst.md` for user-facing samples.
