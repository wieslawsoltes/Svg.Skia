# W3C DOM Animation/Text Performance Plan

## Goal

Enable the remaining DOM-driven W3C rows without introducing render, animation, or text-layout regressions. Performance and memory usage are hard constraints, not secondary goals.

Target rows:

- `animate-dom-01-f`
- `animate-dom-02-f`
- `text-dom-01-f`
- `text-dom-02-f`
- `text-dom-03-f`
- `text-dom-04-f`
- `text-dom-05-f`
- `struct-dom-07-f`
- `struct-dom-13-f`
- `struct-dom-18-f`

## Current State

### Animation DOM

- The animation controller already resolves SMIL timing and frame state efficiently for rendered attributes.
- JavaScript currently only sees `getCurrentTime()` / `setCurrentTime()`.
- Timing-spec parsing handles pointer events but does not fully expose syncbase `.begin` / `.end` behavior into the DOM surface.
- Timeline callbacks (`onbegin`, `onend`, `onrepeat`) are not dispatched from frame progression.
- `SVGAnimationElement.getStartTime()` and `ElementTimeControl` methods are not implemented.

### Text DOM

- Real shaping already exists in the Skia path via HarfBuzz and the retained text compiler.
- The current standalone JavaScript geometry path uses estimated text metrics and is not suitable for `SVGTextContentElement`.
- Per-character DOM APIs are not exposed from the real layout pipeline.
- There is no reusable snapshot for:
  - UTF-16 code-unit indexing
  - ligature / cluster mapping
  - surrogate-pair behavior
  - per-character positions / extents / rotations
  - text hit-testing
  - DOM-driven text selection

### Remaining struct-dom rows

- `struct-dom-07-f`, `struct-dom-13-f`, and `struct-dom-18-f` already have direct runtime coverage.
- The remaining blocker is fixture/harness stability, not missing core DOM behavior.

## Non-Negotiable Performance Rules

1. No extra cost on normal render paths when these DOM APIs are unused.
2. No permanent per-character payload on every `SvgSceneNode`.
3. No duplicate text layout engine inside `Svg.JavaScript`.
4. No full-document recompilation solely to answer repeated text DOM queries.
5. Animation DOM work must not add per-frame global scans for documents that do not use animation DOM callbacks or syncbase event behavior.
6. Any cache introduced for DOM text queries must be invalidated by mutation/version keys and reused across repeated calls.

## Benchmarks and Perf Gates

Before and after each major phase, run:

- `dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgAnimationFrameBenchmarks*"`
- `dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextCompileInternalsBenchmarks*"`
- `dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextAssetLoaderBenchmarks*"`

Acceptance rule:

- no statistically meaningful slowdown on baseline animation frame advancement/draw
- no statistically meaningful slowdown on baseline text compile / measure benchmarks

If a phase regresses these, the phase is incomplete.

## Architecture

### 1. Keep `Svg.JavaScript` reusable

Do not move Skia layout code into `Svg.JavaScript`.

Instead, extend host abstractions:

- `ISvgJavaScriptAnimationHost`
  - current time
  - seek
  - imperative timing control (`beginElement`, `beginElementAt`, `endElement`, `endElementAt`)
  - `getStartTime()` / `getSimpleDuration()` support
  - pause state if needed later

- `ISvgJavaScriptTextLayoutHost`
  - exact text metrics and UTF-16-aware layout queries
  - hit-testing
  - selection range support

`Svg.Skia` implements these interfaces against the real animation controller and the real text layout pipeline.

### 2. Animation DOM design

#### Phase A: timing-spec and imperative control foundations

- Generalize animation timing parsing so `.begin` / `.end` syncbase values are represented explicitly.
- Store imperative begin/end instance lists per animation element.
- Resolve `getStartTime()` from:
  - current active interval, or
  - earliest future resolvable interval
- Return DOM exceptions for invalid states.

#### Phase B: timeline callbacks

- Dispatch `onbegin` / `onend` from timeline transitions.
- Track only bindings that actually need timeline callback delivery or syncbase event support.
- Do not add whole-document callback scans for documents that do not use these features.

#### Phase C: repeat events

- Add `onrepeat` only after the begin/end path is correct and benchmarked.

### 3. Text DOM design

#### Core rule

All `SVGTextContentElement` queries must be derived from the real shaped layout path, not the lightweight JS asset loader.

#### Snapshot model

Add a lazy `TextDomSnapshot` layer that is built only when a text DOM API is called.

Snapshot contents should include:

- source element address/version key
- UTF-16 code-unit map
- codepoint map
- shaped cluster spans
- per-code-unit logical advances
- per-code-unit start/end positions
- per-code-unit extents
- per-code-unit rotations
- hit-test segments

Do not attach this to every scene node. Build it on demand and cache it by:

- document mutation version
- element address key
- layout signature (font / viewport / writing mode / relevant text attributes)

#### API rollout order

1. `getNumberOfChars`
2. `getSubStringLength`
3. error handling for out-of-range indices
4. surrogate-pair and ligature semantics
5. `getStartPositionOfChar`
6. `getEndPositionOfChar`
7. `getExtentOfChar`
8. `getRotationOfChar`
9. `getCharNumAtPosition`
10. `getComputedTextLength`
11. `selectSubString`

### 4. Harness / fixture cleanup

For `struct-dom-07-f`, `struct-dom-13-f`, `struct-dom-18-f`:

- do not distort renderer logic to match stale baselines
- prefer:
  - refreshed Chrome HTTP captures, or
  - explicit semantic verification where the image oracle is unstable

## Delivery Order

### Track 1: Animation DOM

1. Host interface expansion
2. imperative timing control
3. syncbase `.begin` / `.end`
4. `getStartTime()`
5. `animate-dom-02-f`
6. `animate-dom-01-f`
7. benchmark pass

### Track 2: Text DOM

1. text layout host abstraction
2. lazy snapshot cache
3. substring / count semantics
4. surrogate / ligature correctness
5. geometry / rotation / hit-testing
6. selection
7. W3C enablement
8. benchmark pass

### Track 3: struct-dom harness cleanup

1. re-evaluate the three skipped rows against current runtime behavior
2. refresh Chrome overrides when the image oracle is stable
3. keep direct runtime tests as the source of truth where the fixture remains stale

## Validation Matrix

For each phase:

- focused unit tests for new DOM behavior
- focused W3C row enablement where applicable
- `dotnet format Svg.Skia.slnx --no-restore`
- `dotnet build Svg.Skia.slnx -c Release`
- `dotnet test Svg.Skia.slnx -c Release`
- benchmark comparison against the pre-phase baseline

## Immediate Work

Start with Track 1.

Reason:

- smaller surface than text DOM
- directly unlocks two skipped W3C rows
- provides reusable host abstractions needed later by the text DOM work
- lower memory risk than per-character layout exposure
