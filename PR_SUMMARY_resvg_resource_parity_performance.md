# Improve SVG Resource Parity And Rendering Performance

## Summary

This pull request continues the resvg/W3C enablement work by fixing resource-rendering and style-parity gaps, splitting broad skipped-resource coverage into explicit feature families, and optimizing expensive retained-scene, filter, picture-conversion, and text-path paths.

The branch focuses on cases found while validating the resource parity lane:

- Paint server and color resolution parity.
- Conditional-processing, clip-path, geometry, path, and transform reference-box behavior.
- CSS/SVG filter resource behavior and result/input handling.
- Resource dependency tracking for retained scene rebuilds.
- Bounded save-layer and picture-conversion optimizations for filtered scenes.
- Text-path fallback and bounds caching for large positioned text-path runs.
- Whole-run natural text advance caching for repeated text measurement.
- Benchmark and profiling support for focused performance regression checks.
- Explicit resvg non-text fixture grouping so remaining disabled rows are easier to audit by feature area.

## Implementation Details

### Paint Servers And Style Resolution

- Added stricter paint-server handling for currentColor fallback, percentage opacity normalization, legacy `icc-color(...)` fallback parsing, and deferred paint-server references.
- Improved stop-color inheritance and inline style handling for gradient stops.
- Added focused model/rendering tests for paint-server parsing and rendering behavior.
- Regenerated Chrome reference overrides for the updated W3C gradient rows:
  - `pservers-grad-13-b`
  - `pservers-grad-21-b`

### Structure, Conditional Processing, And Geometry

- Restored requiredFeatures handling and added deterministic default system-language behavior.
- Added shared conditional-processing checks for scene compilation and clip compilation.
- Expanded clip-path compilation to cover conditional descendants, basic shapes, nested geometry, fallback geometry, and dependency tracking.
- Improved path handling for move-only paths, zero-radius arcs, same-point arcs, and ellipse geometry conversion.
- Added transform-box reference box mapping for content-box and border-box behavior.
- Added scene-document resource, clip, filter, marker, mask, paint-server, image, and font dependency tracking to avoid stale retained-scene assumptions.
- Regenerated Chrome reference overrides for the enabled W3C conditional-processing rows:
  - `struct-cond-overview-02-f`
  - `struct-cond-overview-03-f`
  - `struct-cond-overview-04-f`
  - `struct-cond-overview-05-f`

### Filter And Resource Parity

- Improved CSS filter URI reference detection and composition behavior.
- Added pass-through handling when a primitive with a `result` has no new image filter but should alias the previous input.
- Added Gaussian blur handling that keeps explicit source input semantics and passes through zero-radius blur inputs.
- Added transparent-black filter caching and alpha derivation from existing source/background image filter results.
- Added `feImage` recursion/reference guards that include the initial document reference.
- Reduced CSS filter step allocations by reusing shared no-op parsing paths.
- Added compatibility/resource tests for external-document and filter-context behavior.

### Rendering Performance

- Added bounded `SaveLayer` command support through the ShimSkiaSharp command model and Skia code generation.
- Applied bounded filter save layers in scene rendering, animation static-layer recording, and filtered textPath rendering.
- Updated native blur construction to use Decal tile mode while preserving crop behavior.
- Added deterministic retained-resource hashing and guarded reusable render-object caches for native picture, shader, image-filter, path, and paint conversion.
- Added per-picture shaped text blob caching and text shaping helpers to reduce repeated native conversion work.
- Reused the configured sRGB color space when creating paint image filters.
- Added a generated flood-filter benchmark scenario to keep filter-heavy picture conversion measurable.

### Text Path Performance

- Added a document-scoped fallback codepoint resolver with bounded caching for fallback text, resolved paint, advance, and optional local bounds.
- Reused fallback resolution across text-path draw, clip, measurement, decoration, filtered-run, and inline-size measurement paths.
- Cached fallback local glyph bounds on demand so repeated text-path bounds measurement avoids repeated native glyph path creation.
- Added fast paths for single-codepoint grapheme clusters in placement and text-DOM fallback cluster generation.

Focused benchmark results for `svg2-textpath-side-right-128` retained-scene compilation:

- Earlier post-cache baseline: `105.433 ms / 67.11 MB`.
- After document-scoped fallback cache: `71.149 ms / 44 MB`.
- After fallback bounds cache: `53.589 ms / 29.2 MB`.

### Natural Text Advance Performance

- Added a bounded whole-run natural text advance cache keyed by document, asset loader, paint/font signature, SVG text style, bidi mode, language, and SVG-font-sensitive state.
- Kept the cache wired through the prepared-text cache clear path and added regression coverage for same-style reuse and font-size-sensitive recomputation.

Focused benchmark results for `SvgTextCompileInternalsBenchmarks.MeasureNaturalTextAdvanceAcrossFragments`:

- `generated-aligned-text-length-192`: `7.332 ms / 3349.78 KB` to `222.031 us / 45 KB`.
- `generated-text-192`: `10.008 ms / 4592.33 KB` to `824.006 us / 144.01 KB`.
- `generated-text-path-curves-96`: `6.974 ms / 2884.8 KB` to `119.345 us / 22.5 KB`.

### Benchmark And Profiling Workflow

- Added serialized benchmark run locking and configurable artifact roots/run labels.
- Added `SVG_SKIA_BENCHMARK_SCENARIOS` filtering for generated, external, and regression-validation scenario sets.
- Added manual load-pipeline profiler output routing through `SVG_SKIA_PROFILE_OUTPUT` or `--profile-output`.
- Updated benchmark docs with comparison, profiling, and focused scenario workflows.
- Updated benchmark checksum/delegate lookup coverage for the new save-layer and command payloads.

### Test Inventory And Suite Enablement

- Split resvg non-text remaining fixtures into explicit feature-area theory families for filters, masking, paint servers, painting, shapes, and structure.
- Added family probe/inventory validation so new fixture groups do not silently drift.
- Expanded the enabled fixture allowlist for resource/style/paint rows now covered by the implementation.
- Updated W3C thresholds and retained-scene test coverage where the implementation now has matching behavior.

## Validation

- `dotnet format Svg.Skia.slnx --no-restore`
- `dotnet build Svg.Skia.slnx -c Release`
  - Succeeded with existing warnings only.
- `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2593, skipped 40.
  - Other test projects in the solution passed.
- Focused natural text advance cache validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests.MeasureNaturalTextAdvance"`
  - Passed 2.
- Focused text internals benchmark comparison:
  - Before: `SVG_SKIA_BENCHMARK_RUN_LABEL="current-text-internals-before-next-advance-cache" SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192,generated-text-path-curves-96,generated-aligned-letter-spacing-192,generated-aligned-text-length-192" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextCompileInternalsBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - After: `SVG_SKIA_BENCHMARK_RUN_LABEL="current-text-internals-after-natural-advance-cache" SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192,generated-text-path-curves-96,generated-aligned-letter-spacing-192,generated-aligned-text-length-192" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextCompileInternalsBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused text/resource validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgTextPathParityTests|FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgResourceRenderingParityTests"`
  - Passed 290.
- Focused W3C retained-suite check:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~W3CTestSuiteTests.Tests"`
  - Passed 632, skipped 3.

The release build currently reports existing warnings only, including package vulnerability warnings and existing nullable/obsolete API warnings. No build errors were reported.
