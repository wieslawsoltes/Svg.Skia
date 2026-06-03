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
- Short shaped-text layout caching for repeated positioned glyph runs.
- Lazy child storage for leaf-heavy retained scene graphs.
- Compact retained-scene dependency-root storage for low-fanout mutation maps.
- Compact retained-scene node-address storage for low-fanout address indexes.
- Retained-scene child address-key pre-seeding during compile traversal.
- Compact retained-scene compilation-root lookup storage during document registration.
- Retained-scene child-list capacity hints during compile traversal.
- Lazy retained-scene compile-context storage for rarely used caches.
- Bounded typeface-span lookup caching for repeated short text runs.
- Lazy retained text hit-metric construction and text hit-cell materialization.
- Retained text fallback paint-clone trimming for simple typeface-span draws.
- Single-span resolved typeface fast path for simple retained text.
- Direct simple spacing text-DOM metrics for retained text.
- Direct simple unpositioned text-DOM metrics for simple retained text.
- Bounded rendered text local-bounds caching for repeated retained text metrics.
- Closed line-only SVG path conversion for generated path-heavy scenes.
- Versioned shim path-bounds caching for retained compile bounds scans.
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
- Added a direct native draw fast path for filled single-command rectangle, rounded-rectangle, oval, and circle shim paths, while preserving the existing path fallback for composite paths, strokes, and save-layer replay.
- Added a closed line-only SVG path conversion fast path that emits a single `AddPoly` command for one-move, line-only, closed subpaths while preserving generic conversion for open, curved, arc, and multi-subpath data.
- Added a bounded native path value cache for repeated three- and four-point `AddPoly` paths so generated path-heavy scenes can reuse equivalent native path objects during picture conversion without weakening mutation tracking.
- Cached local-model source metadata annotation per retained scene node so repeated retained model renders avoid walking already annotated nested command trees.
- Added a bounded short shaped-text layout cache for first-pass native picture conversion so repeated short glyph runs can reuse HarfBuzz shaping output while still creating correctly positioned `SKTextBlob` instances per draw.
- Changed retained scene nodes to allocate child lists lazily so leaf-heavy generated scenes avoid a per-node empty `List<T>` allocation while preserving stable `Children` enumeration semantics.
- Changed retained scene dependency maps to store one or two compilation-root keys inline before allocating overflow storage, avoiding per-dependent-address `HashSet<T>` allocation in low-fanout generated scenes.
- Changed retained scene node-address maps to store one or two nodes inline before allocating overflow storage, avoiding per-address `List<T>` allocation for generated scenes where most addresses resolve to a single node.
- Pre-seeded retained child address keys from known child indexes during scene compilation and kept tiny child-index lookups linear, avoiding avoidable lookup-dictionary allocation for leaf-heavy generated scenes.
- Reused compact inline compilation-root key storage while building the temporary registration lookup, avoiding per-element `List<T>` allocation for the common one- or two-root case.
- Preallocated retained scene node child lists when the compiler already knows the source child count, avoiding repeated `List<T>` growth in generated child-heavy scenes.
- Allocated retained scene compile-context gradient, fragment viewport, marker-reference, and resolved-reference caches only when those features are used; the active-document guard also keeps the common single-document case inline before allocating a `HashSet<T>`.
- Added a bounded per-loader typeface-span cache for repeated short text/paint lookups; cache hits return fresh mutable lists while the cached span arrays stay internal, and provider-state changes clear the cache with the existing typeface-provider caches.
- Changed retained scene nodes to keep a lazy text hit-metric source and resolve DOM hit-cell metrics only when text-cell hit testing first needs them; derived hit-cell arrays still materialize lazily, while extra cells for zero-width pointer-event cases remain preserved when metrics are resolved.
- Allowed simple ASCII retained text with explicit font families to use the sequential text compile path when typeface lookup resolves to one full-run span; multi-span fallback, per-glyph layout, spacing, and active text-length adjustment cases continue through the prepared text path.
- Added direct text-DOM metrics for simple horizontal ASCII letter/word-spacing runs by reusing aligned codepoint placements and natural advances, bypassing generic shaped cluster-source allocation while leaving SVG-font, bidi, synthetic small-caps, textLength, rotation/scale, vertical, and complex text on the existing paths.
- Added direct text-DOM metrics for simple unpositioned horizontal ASCII runs by creating per-codepoint metrics from natural advances and fallback bounds, bypassing shaped cluster-source construction while leaving SVG-font, bidi, spacing, textLength, rotations, vertical, and complex text on the existing paths.
- Added a bounded short rendered-text local-bounds cache keyed by asset loader and text paint/font signature so repeated text-DOM, prepared-text, and text-path bounds checks reuse successful glyph/path bounds while preserving precise hit extents for letter-spacing gaps.
- Trimmed retained text fallback paint cloning so single-span typeface fallback commands record one isolated paint clone and multi-span fallback loops skip the unused clone after the final span.
- Added versioned `SKPath.Bounds` caching for command sequences whose command data is stable, while continuing to recompute `AddPoly` paths whose point lists can be mutated by callers.
- Kept the shim path command storage concrete internally so bounds scans avoid interface enumeration allocation while preserving the public `IList<PathCommand>? Commands` surface.

Focused benchmark results for W3C-safe primitive fill replay:

- `DrawNativePicture1x | generated-inline-styles-512` remained effectively flat/slightly lower: `765.9 us` to `757.5 us`.
- `DrawNativePicture1x | generated-filtered-shapes-256` remained within benchmark noise after preserving save-layer fallback: `1,410.1 us` to `1,451.8 us`.
- `DrawNativePicture1x | generated-shapes-1024` remained flat: `2,994.3 us` to `3,010.5 us`.
- Final guarded `CreateNativePictureFromFullModel` measurements were `1,431.6 us / 51.68 KB` for `generated-filtered-shapes-256`, `243.7 us / 1.68 KB` for `generated-inline-styles-512`, and `2,718.8 us / 1.68 KB` for `generated-shapes-1024`.

Focused `SvgPathConversionBenchmarks` measurements for the closed line-only path conversion on `generated-shapes-1024`:

- `ConvertPrimitiveShapesOnly`: `290.0 us / 248 KB`.
- `ConvertSvgPathsOnly`: `551.4 us / 824 KB`.
- `ConvertAllVisualPaths`: `1,216.3 us / 1072 KB`.
- Timing had visible short-run noise; the benchmark is kept as a focused allocation and regression reference for the new path-conversion shape.

Focused native path value-cache measurement for `generated-shapes-1024`:

- `CreateNativePictureFromFullModel`: `1.107 ms / 1.68 KB`.
- `DrawNativePicture1x`: `4.386 ms` in a noisy short run, so native picture replay remains a follow-up hotspot rather than a claimed win for this change.

Focused local-model metadata-cache measurements:

- `CreateShimPictureModel | generated-aligned-letter-spacing-192`: `48.99 us / 33.76 KB` to `23.32 us / 33.76 KB`.
- `CreateShimPictureModel | generated-text-192`: `50.91 us / 59.26 KB` to `44.22 us / 59.26 KB`.
- `CreateShimPictureModel | generated-text-path-curves-96`: `124.36 us / 30.54 KB` to `23.51 us / 30.54 KB`.

Focused short shaped-layout cache measurements for `generated-text-path-curves-96`:

- Manual cold load profile `Create native SKPicture`: `208.39 ms / 12.45 MB` to `21.99 ms / 9.52 MB`.
- Manual cold load profile `Load via SKSvg.FromSvg`: `315.29 ms / 83.92 MB` to `100.78 ms / 80.54 MB`.
- Manual cold load profile `Mutate + retained scene rebuild`: `499.13 ms / 96.69 MB` to `105.86 ms / 90.32 MB`.
- Warmed `CreateNativePictureFromFullModel`: `698.2 us / 110.29 KB` to `517.75 us / 110.29 KB`.

Focused retained-scene child-list measurements for `generated-shapes-1024`:

- `CompileNodeTreeOnly`: `5.109 ms / 4,923,017 B` to `4.220 ms / 4,857,479 B`.
- `CompileViaSceneCompiler`: allocation `7,736,128 B` to `7,646,095 B`; short-run wall-clock timing was noisy.
- Load-pipeline retained-scene compile allocation recheck: about `7.737 MB` to `7,671,357 B`; the end-to-end timing row was too noisy to treat as a speed claim.

Focused retained-scene dependency-root storage measurements for `generated-shapes-1024`:

- `RegisterDependenciesOnly`: `1,422,104 B` to `880,424 B`.
- `CreateSceneDocumentFromCompiledTree`: `2,812,763 B` to `2,456,331 B`.
- `CompileViaSceneRuntime`: `7,646,714 B` to `7,315,008 B`.
- `CompileViaSceneCompiler`: `7,646,095 B` to `7,289,620 B`.
- `generated-filtered-shapes-256` stayed covered in the same benchmark run, with retained compile allocation around `2.14 MB` and dependency registration at `274,688 B`; short-run timings were noisy, so the dependency-root change is treated as an allocation improvement.

Focused retained-scene node-address storage measurements:

- `ReindexSceneNodesOnly | generated-shapes-1024`: `270,400 B` to about `1,072 B`.
- `ReindexSceneNodesOnly | generated-filtered-shapes-256`: about `67,736 B` to about `1,072 B`.
- `CreateSceneDocumentFromCompiledTree | generated-shapes-1024`: `2,456,331 B` to `2,346,035 B`.
- `CreateSceneDocumentFromCompiledTree | generated-filtered-shapes-256`: `1,009,824 B` to `982,953 B`.
- `CompileViaSceneRuntime | generated-shapes-1024`: `7,315,008 B` to `7,229,173 B`.
- `CompileViaSceneCompiler | generated-shapes-1024`: `7,289,620 B` to `7,228,311 B`.

Focused retained-scene child address-key measurements:

- `CompileNodeTreeOnly | generated-shapes-1024`: `4,857,477 B` to `4,474,078 B`.
- `CompileViaSceneRuntime | generated-shapes-1024`: `7,229,173 B` to `6,493,057 B`.
- `CompileViaSceneCompiler | generated-shapes-1024`: `7,228,311 B` to `6,492,623 B`.
- `CreateSceneDocumentFromCompiledTree | generated-shapes-1024`: `2,346,035 B` to `2,018,407 B`.
- `RegisterDependenciesOnly | generated-shapes-1024`: `880,424 B` to `528,888 B`.
- `CompileNodeTreeOnly | generated-filtered-shapes-256`: `1,125,272 B` to `1,032,930 B`.
- `CompileViaSceneRuntime | generated-filtered-shapes-256`: `2,108,113 B` to `1,937,862 B`.
- `CompileViaSceneCompiler | generated-filtered-shapes-256`: `2,107,501 B` to `1,937,233 B`.

Focused retained-scene compact compilation-root lookup measurements:

- `RegisterDependenciesOnly | generated-shapes-1024`: `528,888 B` to `525,968 B`.
- `RegisterDependenciesOnly | generated-filtered-shapes-256`: `274,976 B` to `271,104 B`.
- `CreateSceneDocumentFromCompiledTree | generated-shapes-1024`: `2,018,407 B` to `2,015,483 B`.
- Full compile allocation stayed effectively flat after the child address-key win: `CompileViaSceneRuntime | generated-shapes-1024` measured `6,490,253 B`, and `CompileViaSceneCompiler | generated-shapes-1024` measured `6,465,076 B`.

Focused shim path-bounds cache measurement:

- `CompileNodeTreeOnly | generated-shapes-1024`: allocation dropped from the prior compact-root lookup row of `4,474,041 B` to `4.09 MB`.
- The same short-run benchmark measured `5.476 ms`, so this change is treated as an allocation reduction and regression guard rather than a wall-clock speed claim.

Focused retained-scene child-list capacity and lazy compile-context measurements:

- Child-list capacity hints reduced `CompileNodeTreeOnly | generated-filtered-shapes-256` from `1,004,252 B` to `993,884 B`.
- Child-list capacity hints reduced `CompileNodeTreeOnly | generated-shapes-1024` from `4,293,800 B` to `4,269,028 B`.
- Full compile allocation also dropped: `CompileViaSceneCompiler | generated-filtered-shapes-256` from `1,898,560 B` to `1,888,166 B`, and `CompileViaSceneRuntime | generated-filtered-shapes-256` from `1,899,184 B` to `1,894,938 B`.
- Full compile allocation for `generated-shapes-1024` dropped from `6,309,427 B` to `6,260,019 B` in `CompileViaSceneCompiler`, and from `6,310,081 B` to `6,285,250 B` in `CompileViaSceneRuntime`.
- Final combined child-list capacity plus lazy compile-context measurement for `CompileNodeTreeOnly` was `970.11 KB` on `generated-filtered-shapes-256` and `4168.44 KB` on `generated-shapes-1024`; the lazy-context portion is a small constant allocation reduction on top of the child-list win.

Focused typeface-span cache measurements:

- `FindTypefacesSequence | generated-text-192`: `2,119.790 us / 838.07 KB` to `5.583 us / 7.5 KB`.
- `CompileViaSceneRuntime | generated-text-192`: `318.12 ms / 72.29 MB` to `114.85 ms / 53.26 MB`.
- `CompileViaSceneRuntime | generated-aligned-letter-spacing-192`: `191.49 ms / 71.92 MB` to `89.25 ms / 68.44 MB`.

Focused lazy retained text hit-metric measurements:

- `CompileNodeTreeOnly | generated-text-192`: about `24.39 MB` to `7.55 MB`.
- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: about `41.21 MB` to `15.6 MB`.
- `CompileNodeTreeOnly | generated-text-path-curves-96`: about `17.15 MB` to `15 MB`.
- The same change moved the focused short-run means to `7.708 ms` for `generated-text-192`, `18.650 ms` for `generated-aligned-letter-spacing-192`, and `17.903 ms` for `generated-text-path-curves-96`.
- A direct single-run text-content metrics builder bypass also removes an extra builder copy when metrics do need to be resolved for one-run retained text.
- The allocation deltas are the primary regression reference; text hit-testing and selection behavior remain covered by focused tests.

Focused single-span resolved typeface fast-path measurements:

- `CompileNodeTreeOnly | generated-text-192`: `89.492 ms / 51,542,843 B`.
- `CompileViaSceneRuntime | generated-text-192`: `83.629 ms / 52,280,667 B`.
- `CompileViaSceneCompiler | generated-text-192`: `85.570 ms / 52,280,475 B`.
- `generated-aligned-letter-spacing-192` stayed on the prepared text path as intended because letter-spacing requires per-glyph layout; short-run timings are treated as noisy, with the simple text rows kept as an allocation and regression reference.

Focused simple spacing text-DOM metrics measurements:

- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `71,663,296 B` to `64,763,888 B`.
- `CompileViaSceneRuntime | generated-aligned-letter-spacing-192`: `71,928,110 B` to `65,028,066 B`.
- `CompileViaSceneCompiler | generated-aligned-letter-spacing-192`: `71,983,736 B` to `65,129,384 B`.
- `generated-text-192` remained the control path, measuring `51,543,168 B` for node-tree compile and about `52.28 MB` for both runtime and scene-compiler compile rows in the same run.
- Short-run timings were noisy, so this change is treated as an allocation reduction and regression guard for retained text metrics.

Focused simple unpositioned text-DOM metrics measurements:

- `CompileNodeTreeOnly | generated-text-192`: `51,543,168 B` to `39,941,636 B`.
- `CompileViaSceneRuntime | generated-text-192`: `52,281,424 B` to `40,679,827 B`.
- `CompileViaSceneCompiler | generated-text-192`: `52,280,800 B` to `40,679,344 B`.
- `generated-aligned-letter-spacing-192` allocation stayed flat around `64.76 MB` for node-tree compile and `65.03 MB` for runtime/compiler compile rows because it remains covered by the spacing-specific metrics path.
- Short-run timings improved in this run, but the change is treated primarily as an allocation reduction and regression guard for simple retained text metrics.

Focused rendered text local-bounds cache measurements:

- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `64,763,367 B` to `43,154,113 B`.
- `CompileViaSceneRuntime | generated-aligned-letter-spacing-192`: `65,028,174 B` to `43,418,877 B`.
- `CompileViaSceneCompiler | generated-aligned-letter-spacing-192`: `65,027,491 B` to `43,418,215 B`.
- `CompileNodeTreeOnly | generated-text-192`: `39,941,636 B` to `25,573,812 B`.
- `CompileViaSceneRuntime | generated-text-192`: `40,679,827 B` to `26,312,139 B`.
- `CompileViaSceneCompiler | generated-text-192`: `40,679,344 B` to `26,311,518 B`.
- Short-run timings improved in the same focused benchmark from about `47 ms` to `35-36 ms` for aligned letter-spacing rows and about `31-33 ms` to `23-24 ms` for simple text rows; the allocation deltas remain the primary regression reference.

Focused retained text paint-clone trim measurement:

- `CompileNodeTreeOnly | generated-text-192`: `8.317 ms / 7.55 MB`.
- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `16.147 ms / 15.6 MB`.
- `CompileNodeTreeOnly | generated-text-path-curves-96`: `16.965 ms / 15 MB`.
- Allocations stayed flat against the previous lazy text-hit-metric row, so this is treated as a small clone-churn cleanup and focused regression guard rather than a new allocation step change.

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
  - `Svg.Skia.UnitTests`: Passed 2594, skipped 40.
  - Other test projects in the solution passed.
- Focused natural text advance cache validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests.MeasureNaturalTextAdvance"`
  - Passed 2.
- Focused direct primitive path validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SKSvgRebuildFromModelTests.RebuildFromModel_ReflectsMutatedPathAfterInitialNativeBuild|FullyQualifiedName~SKSvgRebuildFromModelTests.RebuildFromModel_CanUpdateCommandsForSourceElementId"`
  - Passed 2.
- Focused closed line-only path conversion validation:
  - `dotnet test tests/Svg.Model.UnitTests/Svg.Model.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~PathingServiceTests"`
  - Passed 11.
- Focused native path value-cache validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SKSvgRebuildFromModelTests"`
  - Passed 20.
- Focused local-model metadata-cache validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SKSvgRebuildFromModelTests"`
  - Passed 20.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests&FullyQualifiedName~TextPath"`
  - Passed 50.
- Focused short shaped-layout cache validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~W3CTestSuiteTests.Tests|FullyQualifiedName~resvgTests.text_fixtures|FullyQualifiedName~SKSvgRebuildFromModelTests|FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgTextPathParityTests"`
  - Passed 1107, skipped 3.
- Focused retained-scene child-list validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SKSvgRebuildFromModelTests|FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 283.
- Focused retained-scene dependency-root storage validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SKSvgRebuildFromModelTests|FullyQualifiedName~SvgAnimationControllerTests.TryApplyRetainedSceneMutationByIdAndRender"`
  - Passed 284.
- Focused retained-scene node-address storage validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SKSvgRebuildFromModelTests|FullyQualifiedName~SvgAnimationControllerTests.TryApplyRetainedSceneMutationByIdAndRender"`
  - Passed 284.
- Focused retained-scene child address-key and compact compilation-root lookup validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SKSvgRebuildFromModelTests|FullyQualifiedName~SvgAnimationControllerTests.TryApplyRetainedSceneMutationByIdAndRender"`
  - Passed 284.
- Focused shim path-bounds cache validation:
  - `dotnet test tests/ShimSkiaSharp.UnitTests/ShimSkiaSharp.UnitTests.csproj -f net10.0 -c Release --no-restore`
  - Passed 137.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SKSvgRebuildFromModelTests|FullyQualifiedName~SvgAnimationControllerTests.TryApplyRetainedSceneMutationByIdAndRender"`
  - Passed 284.
- Focused retained-scene child-list capacity and lazy compile-context validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SKSvgRebuildFromModelTests|FullyQualifiedName~SvgAnimationControllerTests.TryApplyRetainedSceneMutationByIdAndRender|FullyQualifiedName~SvgResourceRenderingParityTests"`
  - Passed 391.
- Focused typeface-span cache validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SkiaSvgAssetLoaderCachingTests|FullyQualifiedName~Issue405Tests|FullyQualifiedName~Issue462Tests|FullyQualifiedName~SKSvgSettingsTests.FindTypefaces_WithImplicitItalicTypeface_MatchesResolvedTextTypeface"`
  - Passed 27.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-text-typeface-span-cache dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextAssetLoaderBenchmarks.FindTypefacesSequence*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 10`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192,generated-aligned-letter-spacing-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-retained-typeface-span-cache-text dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime*" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256 SVG_SKIA_BENCHMARK_RUN_LABEL=current-retained-lazy-compile-context-single-doc dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused lazy retained text hit-metric validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests|FullyQualifiedName~SvgTextPathParityTests"`
  - Passed 229.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~W3CTestSuiteTests.Tests"`
  - Passed 523, skipped 3.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-letter-spacing-192,generated-text-192,generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-retained-lazy-text-hit-metrics dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
- Focused single-span resolved typeface fast-path validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests|FullyQualifiedName~SvgTextPathParityTests"`
  - Passed 229.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~W3CTestSuiteTests.Tests"`
  - Passed 523, skipped 3.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192,generated-aligned-letter-spacing-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-retained-text-single-span-fastpath dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
- Focused simple spacing text-DOM metrics validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests|FullyQualifiedName~SvgTextPathParityTests"`
  - Passed 229.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~W3CTestSuiteTests.Tests"`
  - Passed 523, skipped 3.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-letter-spacing-192,generated-text-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-retained-simple-spacing-metrics-reused-advances dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
- Focused simple unpositioned text-DOM metrics validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests|FullyQualifiedName~SvgTextPathParityTests"`
  - Passed 229.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~W3CTestSuiteTests.Tests"`
  - Passed 523, skipped 3.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192,generated-aligned-letter-spacing-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-retained-simple-unpositioned-text-metrics dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
- Focused rendered text local-bounds cache validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests|FullyQualifiedName~SvgTextPathParityTests"`
  - Passed 229.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~W3CTestSuiteTests.Tests"`
  - Passed 523, skipped 3.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-letter-spacing-192,generated-text-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-retained-rendered-bounds-cache dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
- Focused retained text paint-clone trim validation:
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2594, skipped 40; other test projects passed.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192,generated-aligned-letter-spacing-192,generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-retained-text-paint-clone-trim-final dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
- Focused text internals benchmark comparison:
  - Before: `SVG_SKIA_BENCHMARK_RUN_LABEL="current-text-internals-before-next-advance-cache" SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192,generated-text-path-curves-96,generated-aligned-letter-spacing-192,generated-aligned-text-length-192" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextCompileInternalsBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - After: `SVG_SKIA_BENCHMARK_RUN_LABEL="current-text-internals-after-natural-advance-cache" SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192,generated-text-path-curves-96,generated-aligned-letter-spacing-192,generated-aligned-text-length-192" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextCompileInternalsBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused direct primitive path benchmark comparison:
  - Before draw: `SVG_SKIA_BENCHMARK_RUN_LABEL="current-render-draw-hotspots-after-text-cache" SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024,generated-flood-filters-256,generated-filtered-shapes-256,generated-inline-styles-512,generated-text-path-curves-96" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRenderBitmapBenchmarks.DrawNativePicture1x*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - After native conversion: `SVG_SKIA_BENCHMARK_RUN_LABEL="current-native-after-direct-primitive-fill-paths" SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024,generated-inline-styles-512,generated-filtered-shapes-256" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModel*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - After draw: `SVG_SKIA_BENCHMARK_RUN_LABEL="current-render-after-direct-primitive-fill-paths" SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024,generated-inline-styles-512,generated-filtered-shapes-256" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRenderBitmapBenchmarks.DrawNativePicture1x*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused closed line-only path conversion benchmark:
  - `SVG_SKIA_BENCHMARK_RUN_LABEL="current-closed-line-poly-paths" SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgPathConversionBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused native path value-cache benchmarks:
  - `SVG_SKIA_BENCHMARK_RUN_LABEL="current-native-after-small-poly-path-cache" SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModel*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `SVG_SKIA_BENCHMARK_RUN_LABEL="current-render-after-small-poly-path-cache" SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRenderBitmapBenchmarks.DrawNativePicture1x*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused local-model metadata-cache benchmark comparison:
  - Before: `SVG_SKIA_BENCHMARK_RUN_LABEL="baseline-model-localmetadata-cache" SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-path-curves-96,generated-text-192,generated-aligned-letter-spacing-192" dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgLoadPipelineBenchmarks.CreateShimPictureModel*"`
  - After: `SVG_SKIA_BENCHMARK_RUN_LABEL="current-model-localmetadata-cache-exact" SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-path-curves-96,generated-text-192,generated-aligned-letter-spacing-192" dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgLoadPipelineBenchmarks.CreateShimPictureModel*"`
- Focused short shaped-layout cache profiling and benchmark checks:
  - Manual profile: `dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --profile-svg "/var/folders/6f/snf59k7x0ns_dv9l8f0_qgz00000gn/T/svg-skia-generated-profiles/generated-text-path-curves-96.svg" 30 --profile-output "/var/folders/6f/snf59k7x0ns_dv9l8f0_qgz00000gn/T/svg-skia-generated-profiles/generated-text-path-curves-96-profile-short-shaped-layout-cache.md"`
  - Warmed native conversion: `SVG_SKIA_BENCHMARK_RUN_LABEL="current-short-shaped-layout-cache-text-suite" SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-text-path-curves-96" dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModel*"`
- Focused retained-scene child-list benchmark checks:
  - Before: `SVG_SKIA_BENCHMARK_RUN_LABEL="current-retained-compile-shapes-phase-audit" SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024" dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - After: `SVG_SKIA_BENCHMARK_RUN_LABEL="current-retained-compile-shapes-lazy-node-children" SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024" dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - Load-pipeline recheck: `SVG_SKIA_BENCHMARK_RUN_LABEL="current-load-pipeline-lazy-node-children" SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024,generated-filtered-shapes-256" dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgLoadPipelineBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused retained-scene dependency-root storage benchmark check:
  - `SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024,generated-filtered-shapes-256" SVG_SKIA_BENCHMARK_RUN_LABEL="current-retained-compile-small-dependency-set" dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused retained-scene node-address storage benchmark check:
  - `SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024,generated-filtered-shapes-256" SVG_SKIA_BENCHMARK_RUN_LABEL="current-retained-compile-compact-node-addresses" dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused retained-scene child address-key benchmark check:
  - `SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024,generated-filtered-shapes-256" SVG_SKIA_BENCHMARK_RUN_LABEL="current-retained-compile-child-address-cache" dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused retained-scene compact compilation-root lookup benchmark check:
  - `SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024,generated-filtered-shapes-256" SVG_SKIA_BENCHMARK_RUN_LABEL="current-retained-compile-compact-root-lookup" dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused shim path-bounds cache benchmark check:
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024 SVG_SKIA_BENCHMARK_RUN_LABEL=current-retained-compile-path-bounds-cache-final dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused text/resource validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgTextPathParityTests|FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgResourceRenderingParityTests"`
  - Passed 290.
- Focused W3C retained-suite check:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~W3CTestSuiteTests.Tests"`
  - Passed 523, skipped 3.

The release build currently reports existing warnings only, including package vulnerability warnings and existing nullable/obsolete API warnings. No build errors were reported.
