# Pretext Text Engine Feasibility And Refactor Plan

Analysis date: 2026-04-14

## Executive Summary

`PretextSharp` is not a drop-in replacement for the current `Svg.Skia` text engine.

It is a good candidate for a **narrow text preparation, line-breaking, and measurement-cache layer** after upstream and local integration work, but it is **not currently feasible** to use it as the core rendering engine for all SVG text in `Svg.Skia`.

The main blockers are:

- `Pretext` currently targets `net10.0` only, while `Svg.Skia` ships `netstandard2.0;net461;net6.0;net8.0;net10.0`.
- `Pretext` currently depends on `SkiaSharp 3.119.1`, while `Svg.Skia` is on `SkiaSharp 2.88.9`.
- `Pretext` does not draw text, shape glyphs for final output, build text paths, or model SVG-specific placement rules.
- `Pretext` measures via a CSS-like font string and a static global font cache; `Svg.Skia` measures through `SKPaint`, HarfBuzz shaping, browser-compatible font fallback, custom typeface providers, and embedded SVG fonts.

Recommendation:

- Do **not** try to replace `Svg.SceneGraph/SvgSceneTextCompiler.cs` wholesale with `Pretext`.
- Use `Pretext` only as a future **prepared text / line-fit / measurement-cache subsystem** behind a new abstraction.
- Keep SVG-specific rendering, shaping, text-path placement, per-glyph coordinates, vertical layout, decoration geometry, and SVG font outline rendering in `Svg.Skia`.

## PretextSharp Analysis

Analyzed repository:

- `/Users/wieslawsoltes/GitHub/PretextSharp`

Relevant package/runtime facts:

- `src/Pretext/Pretext.csproj`
  - target framework: `net10.0`
  - package id: `Pretext`
- `Directory.Build.props`
  - version: `0.1.0-preview.1`
- `Directory.Packages.props`
  - `SkiaSharp 3.119.1`

### What Pretext does well

Core type and API surface:

- `PretextLayout.Prepare(...)`
- `PretextLayout.PrepareWithSegments(...)`
- `PretextLayout.Layout(...)`
- `PretextLayout.LayoutWithLines(...)`
- `PretextLayout.LayoutNextLine(...)`
- `PretextLayout.LayoutNextLineRange(...)`
- `PretextLayout.WalkLineRanges(...)`
- `PretextLayout.MeasureLineStats(...)`
- `PretextLayout.MeasureNaturalWidth(...)`
- `PretextLayout.PrepareRichInline(...)`
- `PretextLayout.MeasureRichInlineStats(...)`
- `PretextLayout.ClearCache()`
- `PretextLayout.SetLocale(...)`

Capability summary:

- grapheme-aware preparation and line fitting
- reusable prepared text objects
- segment-level cached measurement
- whitespace normalization
- soft hyphen handling
- tab stop handling
- bidi segment levels
- locale-aware segmentation where ICU is available
- streamed line walking for custom layout engines

Architectural strengths:

- clean split between expensive preparation and cheap repeated layout
- deterministic line-walking API
- useful prepared-text abstraction for repeated measurement
- small, understandable core

### What Pretext does not do

From source and docs:

- it does not draw text
- it does not own baseline policy
- it does not expose hit testing or caret/selection behavior
- it does not shape final draw runs through HarfBuzz
- it does not build text outlines or paths
- it does not model SVG text placement attributes

Important implementation constraints:

- font input is a CSS-like string, not `SKPaint`
- `FontSpec.Parse(...)` only understands:
  - `px`
  - numeric weight / `bold`
  - `italic` / `oblique`
  - primary family after size
- unsupported font syntax falls back to `16px Arial`
- generic families are hard-mapped to:
  - `sans-serif` / `system-ui` -> `Arial`
  - `serif` -> `Times New Roman`
  - `monospace` / `ui-monospace` -> `Menlo`

Measurement model limitations for `Svg.Skia` integration:

- measurement is driven by `SKFont` / `SKTypeface.FromFamilyName(...)`
- there is no public production API for plugging in a custom measurement callback
- the only override hook is `SetMeasurementOverrideForTests(...)`, which is `internal`
- caches are static and global:
  - `FontStates`
  - `_segmentTextCaches`
- cache keys are based on the font string, not on `SKSvgSettings`, typeface providers, shaping options, or paint flags

### Practical meaning for Svg.Skia

`Pretext` is a **layout helper** and **measurement cache**, not a renderer. It can help answer:

- how wide is this prepared text?
- how many lines fit in this width?
- where are the line boundaries?

It cannot directly answer:

- which fallback typeface span should draw this substring?
- what HarfBuzz cluster advances should be used?
- how should `x/y/dx/dy/rotate` lists alter placement?
- how should `textLength` and `lengthAdjust` modify final glyph origins?
- how should text follow a path?
- how should vertical writing mode and glyph rotation work?
- how should embedded SVG `<font>` outlines render?

## Svg.Skia Text Pipeline Analysis

The current text system is distributed across a few critical files.

### Primary text engine

- `src/Svg.SceneGraph/SvgSceneTextCompiler.cs`

This is the real text engine today. It handles:

- text subtree traversal
- whitespace normalization and content flattening
- root and child `x/y/dx/dy`
- `rotate`
- `baseline-shift`
- `letter-spacing`
- `word-spacing`
- `textLength`
- `lengthAdjust`
- `text-anchor`
- bidi helpers
- sequential fast paths
- per-codepoint placement
- vertical writing mode
- `textPath`
- `tspan`
- `tref`
- clip-path text path generation
- decoration geometry
- measurement and bounds estimation

This file is not just drawing code. It mixes:

- text extraction
- shaping selection
- measurement
- cache lookups
- placement rules
- rendering
- clip-path path generation

### SVG font rendering

- `src/Svg.SceneGraph/SvgFontTextRenderer.cs`

This file provides repo-owned support for SVG `<font>` glyph outlines. `Pretext` has no equivalent capability.

### Font fallback and text measurement bridge

- `src/Svg.Skia/SkiaSvgAssetLoader.cs`

This file provides:

- `FindTypefaces(...)`
- `FindRunTypeface(...)`
- `GetFontMetrics(...)`
- `MeasureText(...)`
- `TryShapeGlyphRun(...)`
- `GetTextPath(...)`

It also owns:

- browser-compatible fallback scanning
- custom typeface-provider cache usage
- bridge from SVG text code to native Skia measurement/path APIs

### HarfBuzz shaping and stable measurement

- `src/Svg.Skia/SkiaModel.TextShaping.cs`

This file provides:

- HarfBuzz-based shaping
- stable measurement for small text sizes
- `GetTextAdvance(...)`
- glyph run extraction

This is important because `Pretext` currently measures through `SKFont.MeasureText(...)`, while `Svg.Skia` already compensates for cases where direct `MeasureText` is not stable enough.

### Native and text blob caches

- `src/Svg.Skia/SkiaModel.Caching.cs`
- `src/Svg.Skia/SkiaModel.cs`

Current cache coverage includes:

- typeface cache
- resolved typeface cache
- positioned text blob cache
- reusable native object caches

### Existing tests and benchmarks

High-signal text coverage already exists in:

- `tests/Svg.Skia.UnitTests/SvgSceneTextCompilerTests.cs`
- `tests/Svg.Skia.UnitTests/SvgRetainedSceneGraphTests.cs`
- `tests/Svg.Skia.UnitTests/W3CTestSuiteTests.cs`
- `tests/Svg.Skia.UnitTests/resvgTests.cs`
- `tests/Svg.Skia.Benchmarks/SvgAlignedTextPlacementBenchmarks.cs`
- `tests/Svg.Skia.Benchmarks/SvgTextPathPlacementBenchmarks.cs`
- `tests/Svg.Skia.Benchmarks/SvgTextAssetLoaderBenchmarks.cs`
- `tests/Svg.Skia.Benchmarks/SvgTextCompileInternalsBenchmarks.cs`

This is enough coverage to safely refactor behind an abstraction, but not enough to justify a full engine swap without staged gates.

## Feature Fit Matrix

### Strong fit

Use `Pretext` later for these areas:

- repeated measurement cache for flat text runs
- line fitting for future wrapped-text features
- prepared rich-inline flows in editor/UI features outside strict SVG text rendering
- cheap line-count / natural-width probes
- reusable segment-based measurement where text, font, whitespace, and locale remain stable

### Partial fit

Possible only with additional integration work:

- unpositioned sequential SVG text measurement
- logical-run preparation before final SVG placement
- cache of flat text measurement in `Svg.SceneGraph` fast paths

Requirements before this becomes safe:

- injectable measurement backend
- font resolution alignment with `SkiaSvgAssetLoader`
- cache keys that include `SKSvgSettings` / provider context
- target/framework/version alignment

### No fit today

Do not try to replace these with `Pretext`:

- `textPath`
- per-glyph `x/y/dx/dy`
- `rotate`
- `baseline-shift`
- vertical writing mode
- `glyph-orientation-vertical`
- SVG `<font>` outline rendering
- text decorations tied to font metrics and final glyph placement
- clip-path text path generation
- HarfBuzz shaping for final draw blobs
- browser-compatible fallback run selection
- `GetTextPath(...)` / outline generation

## Feasibility Verdict

### Can Pretext be the core text rendering engine?

No.

It should not replace the rendering core in `Svg.Skia`.

Reasons:

- it does not render
- it does not build outlines
- it does not own shaping for final draw output
- it does not know about SVG placement semantics
- it cannot represent the current fallback/typeface-provider model

### Can Pretext be the core text layout and measurement-cache layer?

Yes, but only for a constrained sub-problem and only after prerequisites are met.

The realistic adoption boundary is:

- prepared measurement and line-fit cache for flat text
- optional helper for future wrapped or editor-oriented text
- possibly a net10-only experimental path first

It is not realistic to replace "most" of the text engine without preserving a large SVG-specific layer above it.

The correct architecture is:

- `Pretext` for preparation / reusable measurement / line fitting
- `Svg.Skia` for SVG semantics, shaping, fallback resolution, placement, drawing, and path generation

## Hard Blockers

### 1. Target framework mismatch

Current state:

- `Pretext`: `net10.0`
- `Svg.Skia`: `netstandard2.0;net461;net6.0;net8.0;net10.0`

Impact:

- any direct integration would be net10-only
- package consumers on the other targets would need fallback code anyway

Required resolution:

- either multi-target `Pretext`
- or isolate all `Pretext` usage behind net10-only optional code paths

### 2. SkiaSharp major-version mismatch

Current state:

- `Pretext`: `SkiaSharp 3.119.1`
- `Svg.Skia`: `SkiaSharp 2.88.9`

Impact:

- direct package/reference integration is not safe
- this is a real adoption blocker, not a cleanup detail

Required resolution:

- either migrate `Svg.Skia` to SkiaSharp 3.x
- or produce a `Pretext` build compatible with SkiaSharp 2.88.x

### 3. Measurement backend mismatch

Current state:

- `Pretext` measures from font strings and `SKFont.MeasureText(...)`
- `Svg.Skia` measures through `SkiaSvgAssetLoader`, HarfBuzz shaping, stable small-size measurement, and custom fallback/typeface-provider logic

Impact:

- same font string can still produce different effective glyph coverage and advance behavior
- `Pretext` cache keys are not rich enough for `Svg.Skia` settings

Required resolution:

- public pluggable measurement backend in `Pretext`
- cache keys that include provider context or a caller-supplied engine identity

### 4. SVG semantic mismatch

`Pretext` does not model:

- SVG text tree flattening rules
- `tspan` inheritance and nested positioning
- `textPath`
- `textLength`
- `letter-spacing` / `word-spacing` behavior as currently implemented
- baseline adjustments
- vertical glyph rotation rules

Impact:

- `Pretext` can only sit underneath a retained SVG-specific placement layer

## Recommended Refactor Direction

Do this as a layered refactor, not a rewrite.

### Target architecture

Split the current text engine into five explicit layers:

1. **Content extraction**
   - flatten SVG text nodes into logical text runs
   - preserve style-source boundaries
   - preserve SVG semantics for whitespace and references

2. **Text preparation / cached measurement**
   - prepare flat logical text with a reusable cache entry
   - this is the future `Pretext` insertion point

3. **SVG placement**
   - apply `text-anchor`, `textLength`, spacing, `x/y/dx/dy`, `rotate`, vertical layout, `textPath`
   - this remains `Svg.Skia`-owned

4. **Rendering / outline generation**
   - shape final runs
   - resolve fallback typefaces
   - draw text or build paths
   - preserve SVG font support

5. **Bounds / decorations / hit-test support**
   - derive bounds from final placement
   - keep decoration geometry based on final metrics

### Immediate rule

Only layer 2 is a valid `Pretext` integration target.

## Concrete Migration Plan

### Phase 0. Freeze invariants before refactor

Status: required before any engine swap.

Actions:

- treat current text tests as compatibility gates
- add more focused tests only if a refactor needs tighter invariants around:
  - sequential mixed-direction runs
  - `textLength`
  - positioned codepoint placement
  - `textPath`
  - SVG font fallback

Acceptance:

- existing text suites stay green
- no threshold-only regressions introduced

### Phase 1. Extract a text preparation abstraction

Goal:

- separate preparation/cache concerns from placement/rendering concerns

Create an internal abstraction similar to:

- `ISvgPreparedTextEngine`
  - `TryPrepareFlatRun(...)`
  - `MeasureNaturalWidth(...)`
  - `MeasureLineStats(...)`
  - `ClearCaches(...)`

Important:

- the default implementation must wrap the current `Svg.Skia` measurement behavior
- do not reference `Pretext` directly in the first extraction

Acceptance:

- `SvgSceneTextCompiler` no longer owns raw measurement-cache policy directly
- existing behavior remains unchanged

### Phase 2. Move flat sequential measurement behind the abstraction

Scope:

- sequential unpositioned text only
- no `textPath`
- no per-glyph coordinate lists
- no vertical mode
- no `textLength`
- no SVG fonts

Good first call sites:

- sequential fast-path width probes
- repeated measurement in aligned/unpositioned runs

Acceptance:

- no output change
- lower code duplication between:
  - `MeasureSequentialTextRuns(...)`
  - `MeasureNaturalTextAdvance(...)`
  - sequential compile fast path

### Phase 3. Upstream Pretext changes required before real adoption

These changes should land in `PretextSharp` before `Svg.Skia` depends on it.

Required upstream work:

- multi-target beyond `net10.0`
- align SkiaSharp version with `Svg.Skia`, or vice versa
- public measurement backend injection, not test-only internal override
- explicit cache identity / context support
- public API that can accept a richer font descriptor than the current CSS-like string, or a caller-supplied font resolver

Strongly recommended upstream additions:

- non-static engine/context instance instead of only global static caches
- option to expose prepared grapheme advances in a way suitable for caller-owned placement engines
- documented behavior around thread safety and cache invalidation

Acceptance:

- `Svg.Skia` can measure through `Pretext` without losing custom provider behavior

### Phase 4. Add an experimental Pretext-backed implementation

Scope:

- opt-in only
- likely `net10.0` only at first unless blockers are removed
- flat, unpositioned, horizontal text only

Use cases:

- prepared cache for repeated width measurement
- future wrapped text experiments

Must not handle:

- `textPath`
- `x/y/dx/dy`
- `rotate`
- `baseline-shift`
- vertical layout
- SVG fonts

Acceptance:

- exact-match or near-exact parity on the approved flat-text subset
- measurable benchmark win on repeated measurement scenarios

### Phase 5. Decide the long-term boundary

If the experimental path is successful, keep the long-term split as:

- `Pretext`: preparation / line-fit / cached flat-text metrics
- `Svg.Skia`: shaping, placement, fallback resolution, drawing, path generation

Do not attempt to make `Pretext` the owner of the full SVG text renderer unless `Pretext` itself grows into a materially different library.

## What Should Not Be Rewritten

Keep these systems in `Svg.Skia`:

- `SvgFontTextRenderer`
- `SkiaSvgAssetLoader`
- HarfBuzz shaping in `SkiaModel.TextShaping.cs`
- `textPath` placement logic
- positioned-codepoint placement logic
- decoration geometry creation
- bounds expansion based on final draw placement

These are not accidental complexity. They encode SVG behavior that `Pretext` does not own.

## What Can Eventually Move Behind Pretext

Only these areas are realistic:

- cached preparation of flat text runs
- natural-width measurement cache
- line statistics for future wrapped content
- rich-inline preparation for editor/UI features outside strict SVG rendering

## Proposed Acceptance Matrix For Future Implementation

Before promoting any `Pretext` path from experimental to default on its supported subset, require:

- all existing `SvgSceneTextCompilerTests` still pass
- all existing retained scene graph text tests still pass
- no regression in `W3CTestSuiteTests`
- no regression in `resvgTests` text categories
- benchmark win in repeated-measurement scenarios

Suggested benchmark focus:

- repeated measure of identical flat runs across many frames
- repeated measure of a small set of recurring labels
- repeated line-stat probes for stable text with changing width

## Final Recommendation

The right move is a **selective refactor**, not a replacement.

Use `Pretext` as a future prepared-text and measurement-cache engine only after:

- framework alignment
- SkiaSharp alignment
- pluggable measurement backend support
- cache-context support

Until those prerequisites are met, the best immediate work in `Svg.Skia` is:

1. extract a preparation/cache abstraction out of `SvgSceneTextCompiler`
2. keep the current renderer and placement engine intact
3. integrate `Pretext` only for a narrow flat-text subset
4. leave SVG-specific text behavior in `Svg.Skia`

That path has a realistic chance of improving maintainability and repeated-measure performance without destabilizing the current SVG text feature set.
