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
- Pre-sized text-path path-sample storage for long sampled paths.
- Text-path placement allocation trimming for simple positioned text-path runs.
- Text-path geometry cache-hit allocation trimming for repeated referenced path data.
- Direct textPath-only retained compile fast path for positioned textPath nodes.
- Simple textPath recording guards for ASCII grapheme probes and empty decoration phases.
- TextPath offset measurement skip for left-aligned, left-side positioned textPath runs.
- Simple ASCII positioned textPath bounds and draw fast path using one full-run paint.
- Compact retained command recording for long simple positioned textPath runs.
- Fast metric bounds reuse for simple positioned textPath retained compile.
- SVG-font registry guard for positioned textPath bounds probes in documents without SVG-font entries.
- Whole-run natural text advance caching for repeated text measurement.
- Simple natural text advance cache-hit fast path for repeated prepared text measurement.
- Short shaped-text layout caching for repeated positioned glyph runs.
- Lazy child storage for leaf-heavy retained scene graphs.
- Compact retained-scene dependency-root storage for low-fanout mutation maps.
- Compact retained-scene node-address storage for low-fanout address indexes.
- Retained-scene child address-key pre-seeding during compile traversal.
- Compact retained-scene compilation-root lookup storage during document registration.
- Node-tree dependency registration for fully retained no-reference scenes.
- Retained-scene child-list capacity hints during compile traversal.
- Compact retained-scene node child storage for one- and two-child nodes.
- Lazy retained-scene compile-context storage for rarely used caches.
- Retained-scene address-key cache pre-sizing, cached small child-index strings, and lazy child-index lookup storage.
- Retained-scene document index capacity hints and no-reference dependency registration reuse.
- Bounded typeface-span lookup caching for repeated short text runs.
- Lazy retained text hit-metric construction and text hit-cell materialization.
- Source-free lazy retained text hit-metric materialization for text-cell hit testing.
- Retained text fallback paint-clone trimming for simple typeface-span draws.
- Single-span resolved typeface fast path for simple retained text.
- Direct simple spacing text-DOM metrics for retained text.
- Direct simple unpositioned text-DOM metrics for simple retained text.
- Bounded prepared line-stats caching for repeated retained sequential text runs.
- SVG-font-aware retained sequential text compile eligibility probe skip for non-SVG-font documents.
- SVG-font-aware retained text metric probe skip for simple Skia text paths in non-SVG-font documents.
- Simple aligned retained text compile fast path for horizontal spacing/textLength runs.
- Direct fixed-spacing positioned text-blob recording for simple retained aligned text runs.
- Direct positioned text-blob recording for simple root `lengthAdjust="spacing"` textLength runs.
- Positioned text-blob recording for simple unrotated retained text placements.
- Uniform scaled positioned text-blob recording for simple retained `spacingAndGlyphs` textLength placements.
- Font-scale encoded positioned text blobs for uniform retained `spacingAndGlyphs` textLength placements.
- Direct scaled text command recording for simple retained `spacingAndGlyphs` textLength runs.
- Single-span typeface resolution for simple scaled textLength command recording.
- Root retained compile fast path for simple scaled `spacingAndGlyphs` textLength runs.
- Aligned retained compile codepoint split reuse for positioned bounds measurement.
- ASCII codepoint string reuse, exact split-list capacity, natural-advance cache insertion copy trimming, and single-run aligned retained compile list elision.
- Lazy text reference seeding for retained text compile paths that actually build filtered textPath contexts.
- Flattened textLength run materialization allocation trimming for simple one-style retained textLength runs.
- Grouped flattened textLength natural-advance measurement for contiguous same-style retained textLength runs.
- Value-type flattened codepoint storage for flattened and wrapped textLength collection.
- Indexed shared inline-size layout grouping for retained text spans and compiler runs.
- Grouped wrapped inline-size textLength advance measurement and positioned-run materialization.
- Read-only flattened codepoint text view for shared and wrapped inline-size break planning.
- Range-backed flattened codepoint text view for grouped natural-advance measurement.
- Exact flattened textLength run storage for retained render, bounds, and text-DOM metric paths.
- Exact shared inline-size compiler-run storage and exact flattened text construction for shared/wrapped textLength paths.
- Exact wrapped inline-size textLength group storage, text construction, and direct placement materialization for DOM metric paths.
- Layer-depth-only native picture replay state tracking for save/restore-heavy scenes.
- Bounded rendered text local-bounds caching for repeated retained text metrics.
- Read-only codepoint split reuse for text-DOM and prepared-text read paths.
- Source-text reuse and simple natural codepoint advance caching for repeated prepared text measurement.
- Split-codepoint cache-hit fast path for repeated retained text splitting.
- Prepared-text no-op whitespace reuse and trailing-run spacing boundary guards for retained text.
- Unchanged-total-transform guard for retained structural finalization.
- Fill-only resolved sequential text recording for retained simple tspan runs.
- Closed line-only SVG path conversion for generated path-heavy scenes.
- Versioned shim path-bounds caching for retained compile bounds scans.
- Inline shim path command-list storage for one- and two-command paths.
- Small `AddPoly` native path revision-key reuse for generated path-heavy replay.
- Transform-only simple shape group flattening for retained command streams and native replay.
- Allocation-free transformed simple-child render eligibility for retained renderer fallback replay.
- Direct-matrix native replay for transformed retained positioned text runs.
- Rotation-scale native text blob replay for simple positioned text runs.
- Retained resource-key feature gates for documents without clip-path, mask, or filter declarations.
- Retained runtime resource-payload feature gates for documents without clip-path, mask, or filter declarations.
- On-demand retained compilation-root lookup for no-reference scene documents.
- Structural transform-origin reuse and direct path cull-bounds reuse for retained primitive/group compile.
- Direct visual fill/stroke validity reuse for retained shape compile.
- Retained paint-hit validity reuse during runtime payload refresh for direct path, shape, and text nodes.
- Runtime payload paint reuse and compact retained-node opacity state for direct shape nodes.
- Empty document-font scope fast path for no-font retained compile/render paths.
- Inline `SKPath` command storage without a per-path change-tracking wrapper.
- Inline small `AddPoly` point storage for internally generated triangle and quad paths.
- Own-`textLength` guard for retained textLength fast-path probes on ordinary text.
- Lazy retained-node visual/resource sidecar storage for rare cursor, background, blend, and resource-key state.
- Lazy retained-node effect sidecar storage for rare clip, mask, and filter state.
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
- Changed draw-picture replay state tracking from an eager save-stack to save-depth counters plus rare nested save-layer depth storage, avoiding ordinary save/restore stack work while preserving the save-layer guard for direct primitive replay.
- Cached local-model source metadata annotation per retained scene node so repeated retained model renders avoid walking already annotated nested command trees.
- Added a bounded short shaped-text layout cache for first-pass native picture conversion so repeated short glyph runs can reuse HarfBuzz shaping output while still creating correctly positioned `SKTextBlob` instances per draw.
- Changed retained scene nodes to allocate child lists lazily so leaf-heavy generated scenes avoid a per-node empty `List<T>` allocation while preserving stable `Children` enumeration semantics.
- Changed retained scene dependency maps to store one or two compilation-root keys inline before allocating overflow storage, avoiding per-dependent-address `HashSet<T>` allocation in low-fanout generated scenes.
- Changed retained scene node-address maps to store one or two nodes inline before allocating overflow storage, avoiding per-address `List<T>` allocation for generated scenes where most addresses resolve to a single node.
- Pre-seeded retained child address keys from known child indexes during scene compilation and kept tiny child-index lookups linear, avoiding avoidable lookup-dictionary allocation for leaf-heavy generated scenes.
- Reused compact inline compilation-root key storage while building the temporary registration lookup, avoiding per-element `List<T>` allocation for the common one- or two-root case.
- Preallocated retained scene node child lists when the compiler already knows the source child count, avoiding repeated `List<T>` growth in generated child-heavy scenes.
- Changed retained scene nodes to keep one or two children inline before allocating overflow list storage, preserving the public `IReadOnlyList<SvgSceneNode>` child surface while trimming low-fanout retained compile allocations.
- Allocated retained scene compile-context gradient, fragment viewport, marker-reference, and resolved-reference caches only when those features are used; the active-document guard also keeps the common single-document case inline before allocating a `HashSet<T>`.
- Pre-sized retained scene address-key caches from shallow source-tree counts, reused cached small child-index strings, and kept child-index lookup dictionaries lazy so generated retained scenes avoid dictionary growth and temporary index-string churn.
- Reused dependency-analysis element/id counts to pre-size retained scene-document indexes on modern targets, and let no-reference standalone dependency registration reuse node subtree metadata once reindexing has proven address coverage.
- Added a bounded per-loader typeface-span cache for repeated short text/paint lookups; cache hits return fresh mutable lists while the cached span arrays stay internal, and provider-state changes clear the cache with the existing typeface-provider caches.
- Changed retained scene nodes to keep a lazy text hit-metric source and resolve DOM hit-cell metrics only when text-cell hit testing first needs them; derived hit-cell arrays still materialize lazily, while extra cells for zero-width pointer-event cases remain preserved when metrics are resolved.
- Moved lazy retained text metric materialization to use the scene document's compilation viewport and asset loader during point hit testing, removing the per-text-node lazy source record while preserving first-hit DOM metric resolution.
- Allowed simple ASCII retained text with explicit font families to use the sequential text compile path when typeface lookup resolves to one full-run span; multi-span fallback, per-glyph layout, spacing, and active text-length adjustment cases continue through the prepared text path.
- Added direct text-DOM metrics for simple horizontal ASCII letter/word-spacing runs by reusing aligned codepoint placements and natural advances, bypassing generic shaped cluster-source allocation while leaving SVG-font, bidi, synthetic small-caps, textLength, rotation/scale, vertical, and complex text on the existing paths.
- Added direct text-DOM metrics for simple unpositioned horizontal ASCII runs by creating per-codepoint metrics from natural advances and fallback bounds, bypassing shaped cluster-source construction while leaving SVG-font, bidi, spacing, textLength, rotations, vertical, and complex text on the existing paths.
- Added a bounded prepared line-stats cache keyed by asset loader, document, text, font, bidi, and paint metric signature so repeated retained sequential text fragments reuse measured draw text, resolved typeface, advance, and local bounds while ignoring paint color differences that do not affect text metrics.
- Skipped the redundant per-run prepared-text eligibility probe for retained sequential text when none of the runs can use SVG-font layout, while keeping the exact `SvgFontTextRenderer.TryGetLayout` guard for documents that do contain SVG-font entries.
- Reused an SVG-font entry guard for simple retained text metric probes and positioned/scaled text-blob eligibility checks, avoiding metrics-paint or SVG-font layout work in documents without SVG-font entries while preserving the exact SVG-font fallback for documents that do contain entries.
- Added a guarded retained compile fast path for simple horizontal ASCII aligned runs with fixed letter/word spacing or non-relative textLength, resolving codepoint placements once and reusing them for both bounds and drawing while leaving rotations, baseline shifts, decorations, vertical text, relative spacing/textLength, SVG-font text, custom OpenType properties, and complex bidi text on the existing path.
- Added a narrower retained compile path for simple fixed non-negative letter/word-spacing runs that builds positioned text-blob points directly from natural advances and full-run local bounds, bypassing placement structs and per-codepoint bounds resolution while leaving percentages, negative spacing, textLength, SVG fonts, rotations, baseline shifts, decorations, vertical text, custom OpenType properties, and complex text on the existing aligned placement path.
- Added a root retained compile path for simple horizontal ASCII `lengthAdjust="spacing"` textLength runs that records one positioned text blob from natural advances plus the distributed textLength gap, bypassing flattened textLength layout, per-codepoint placement structs, and per-codepoint text commands while leaving inline-size, positioned descendants, nested textLength, rotations, baseline shifts, decorations, vertical text, SVG fonts, relative textLength units, custom OpenType properties, explicit spacing, and complex text on existing paths.
- Added guarded positioned text-blob recording for simple ASCII unrotated retained text placements, collapsing many per-codepoint text commands into one positioned blob command while leaving SVG-font text, synthetic small-caps, mixed typeface fallback, rotated glyphs, mixed-scale glyphs, and complex text on the existing per-codepoint renderer.
- Extended positioned text-blob recording to uniform positive horizontal scale, replacing per-glyph save/scale/text/restore sequences with one scaled positioned blob for simple retained `lengthAdjust="spacingAndGlyphs"` textLength runs.
- Encoded uniform positioned text-blob scale in the blob `SKFont` and pre-scaled glyph origins, removing save/set-matrix/restore command wrappers from simple retained `spacingAndGlyphs` textLength blob recording while preserving the existing fallback for rotated, mixed-scale, SVG-font, synthetic small-caps, and complex text.
- Recorded simple horizontal ASCII `lengthAdjust="spacingAndGlyphs"` textLength runs as one scaled `DrawText` command, avoiding positioned blob point materialization while leaving rotations, explicit spacing, vertical text, SVG-font text, synthetic small-caps, browser-compatible fallback runs, and complex typeface fallback on the existing positioned paths.
- Reused the cached horizontal natural-advance path and narrowed simple scaled textLength command typeface resolution to one verified full-run typeface span, avoiding the broader run-typeface resolver while falling back to positioned text for multi-span or complex cases.
- Added a root retained compile branch for simple horizontal ASCII `lengthAdjust="spacingAndGlyphs"` textLength runs, resolving scaled draw bounds once and recording the scaled text command directly instead of falling through to generic text bounds estimation plus draw traversal.
- Reused aligned placement codepoint splits during retained aligned bounds measurement, avoiding a second codepoint read/allocation pass for simple aligned retained runs.
- Reused immutable one-character ASCII codepoint strings during codepoint splitting, pre-sized uncached split lists, and stored freshly measured natural codepoint advances directly in the internal cache once callers had finished mutating reconciliation data.
- Added source-text side caching for read-only split codepoint arrays and a cheaper simple natural-codepoint advance cache keyed by text/font/bidi metrics before paint allocation, reducing repeated prepared-text measurement allocations while keeping mutable split callers on fresh lists.
- Bypassed one-item `List<SimplePositionedSequentialCompileRun>` materialization for single-run simple aligned retained spacing/textLength compilation by drawing the resolved positioned run directly.
- Deferred text reference-set allocation until filtered textPath rendering actually constructs a filter context, while preserving document URI seeding through `SvgService.ExtendImageReferences`.
- Trimmed flattened textLength run materialization by avoiding LINQ glyph-scale seeding, temporary spacing gap-index lists, chunk arrays for one-chunk textLength runs, and placement-list copies for simple one-style retained textLength runs.
- Grouped flattened textLength natural-advance measurement across contiguous same-style ranges, reusing the full-run codepoint-advance engine while keeping the per-codepoint fallback when measured advances do not match the flattened range.
- Changed private flattened codepoint storage from per-codepoint reference objects to value-type list elements, with explicit write-back for resolved `x`, `y`, `dx`, and `dy` values so flattened and wrapped textLength collection avoids one object allocation per flattened codepoint.
- Replaced shared inline-size layout rendered-placement materialization and repeated `Skip`/`Take`/`Select` slicing with index-based grouping helpers for positioned spans and compiler runs, preserving style/source-contiguity rules while reducing transient arrays and enumerators.
- Reused grouped flattened natural advances in wrapped inline-size textLength preparation, switched the mixed-direction guard to shared flattened-text construction, and built positioned run groups by source index so wrapped textLength DOM-metric paths avoid per-codepoint advance calls and transient LINQ materialization.
- Replaced the shared and wrapped inline-size break planner's flattened-codepoint `string[]` projection with a read-only text view over the existing flattened codepoint list, avoiding one reference-array allocation per layout pass while preserving the same line-break resolver behavior.
- Reused the flattened-codepoint text view for same-style natural-advance ranges, replacing per-range `string[]` materialization while keeping measured text construction and per-codepoint fallback behavior unchanged.
- Changed flattened textLength run creation to return exact run arrays only after eligibility succeeds, avoiding the eager `List<PositionedCodepointRun>` allocation on failed probes and the temporary list/internal-array pair for successful retained render, bounds, and text-DOM metric paths.
- Changed shared inline-size compiler-run creation to count rendered groups and fill an exact `PositionedCodepointRun[]`, and changed flattened text construction to use exact string creation on modern targets with a compatible exact char-array fallback on older targets.
- Changed wrapped inline-size textLength line-run grouping to count groups and fill an exact `PositionedCodepointRun[]`, changed grouped run text materialization to use exact string creation, and wrote computed placements directly into each group-owned placement array instead of staging a full line-run placement buffer.
- Added a guarded retained compile fast path for direct textPath-only text nodes, resolving positioned textPath runs once and reusing them for both retained bounds and local picture recording while leaving stretch textPath, mixed inline text, recursive or missing geometry, inline layout, vertical text, and textLength container barriers on the existing path.
- Skipped grapheme-cluster draw probing for simple ASCII positioned textPath runs and skipped empty decoration phases for positioned/stretch textPath recordings, while leaving complex clusters and decorated textPath runs on the existing paths.
- Skipped the eager textPath total-advance measurement when the resolved text anchor is left and the textPath is not rendered on the right side of a positive-length path; centered, right-aligned, and right-side textPath runs still measure advance before offset adjustment.
- Added a guarded simple ASCII positioned textPath bounds and draw path that resolves one full-run paint/typeface and reuses cached codepoint splits, while leaving SVG-font text, browser-compatible fallback substitutions, synthetic small-caps, mixed typeface fallback, and complex text on the existing per-codepoint resolver.
- Reused the SVG-font renderer's document registry cache to skip positioned per-codepoint SVG-font layout probes when a document has no SVG-font entries, avoiding paint clones during simple textPath bounds checks while preserving the existing SVG-font path for matching documents.
- Added a bounded short rendered-text local-bounds cache keyed by asset loader and text paint/font signature so repeated text-DOM, prepared-text, and text-path bounds checks reuse successful glyph/path bounds while preserving precise hit extents for letter-spacing gaps.
- Trimmed retained text fallback paint cloning so single-span typeface fallback commands record one isolated paint clone and multi-span fallback loops skip the unused clone after the final span.
- Reused cached read-only codepoint split arrays across text-DOM, prepared-text, and shared-layout read paths, leaving the lone mutable split at the reverse-by-codepoint call site.
- Returned cached read-only codepoint split arrays directly on split-cache hits, avoiding a redundant text-side cache lookup while preserving the source-text registration done on cache insertion.
- Reused already prepared text when whitespace normalization, trimming, discard, and space collapse are provably no-ops, and skipped trailing-run boundary codepoint work when no spacing is active or the trailing run is simple ASCII.
- Skipped descendant total-transform refresh during retained structural finalization when the finalized node's effective total transform is unchanged, while still updating the node's own bounds and transformed bounds.
- Recorded resolved sequential text runs through a direct fill-only path when stroke and text-decoration are inactive, avoiding the generic paint-order delegate path for simple retained tspan rows while leaving stroked/decorated text on the existing flow.
- Added versioned `SKPath.Bounds` caching for command sequences whose command data is stable, while continuing to recompute `AddPoly` paths whose point lists can be mutated by callers.
- Kept the shim path command storage concrete internally so bounds scans avoid interface enumeration allocation while preserving the public `IList<PathCommand>? Commands` surface.
- Changed shim path command lists to keep one- and two-command paths inline before allocating overflow list storage, preserving the mutable `IList<PathCommand>` command surface while trimming generated simple-path retained compile allocations.
- Moved the inline command-list storage directly onto `SKPath`, so `Commands` returns the path's own mutable `IList<PathCommand>` implementation and simple paths no longer allocate a separate change-tracking wrapper.
- Reused the small `AddPoly` native path value-cache key to derive one-command small-poly path revisions, avoiding a second point-list hash pass while preserving mutable-point invalidation.
- Added inline point storage to `AddPolyPathCommand` for internally generated three- and four-point polygons, while keeping array/list-backed `AddPoly` commands mutation-sensitive for existing public callers.
- Flattened transform-only retained groups whose children are simple solid-filled leaf paths by pre-transforming rectangle, polygon, and closed line-only path commands during retained recording; this removes save/set-matrix/restore command wrappers and simple leaf opacity save-layers while preserving source-element metadata and keeping clips, masks, filters, strokes, blend modes, isolation, nested children, shaders, and complex paths on the existing rendering path.
- Changed the transformed simple-child renderer fallback to inspect path command shapes during eligibility instead of creating a throwaway transformed path, and to map closed line-only command lists directly into the final transformed point array during recording.
- Replayed compact retained positioned text runs by scanning for untransformed fragments first, drawing those directly, and using direct canvas matrix resets for transformed fragments instead of per-fragment save/restore pairs.
- Added a guarded native `SKTextBlob.CreateRotationScale` replay path for left-aligned simple ASCII positioned text runs, collapsing eligible fragment runs into one native blob draw while keeping scaled, non-ASCII, non-left-aligned, and other complex runs on the existing per-fragment fallback.
- Reused the cached document-wide cascaded-style feature analysis to skip retained clip/mask/filter resource-key lookup attempts when the active document has no matching declarations, preserving precise lookup behavior for documents that do declare those resources.
- Reused the same document-wide feature analysis during retained runtime payload refresh so main-document nodes skip clip-path, mask, and filter resolver work entirely when the document has no matching declarations; non-root payload trees remain conservative for externally scoped resources.
- Switched no-reference retained scene documents from eager descendant-address dependency map construction to on-demand ancestor-root lookup during mutation, including parent-address fallback for non-rendered DOM children such as animation elements.
- Reused precomputed structural transforms when applying transform-origin during direct retained group, anchor, and switch finalization, and reused already computed direct path geometry bounds for local cull rectangles.
- Reused direct visual fill/stroke hit-test validity checks when creating retained local paints, avoiding duplicate paint-server and stroke-width validity work for every direct shape node.
- Reused retained fill/stroke hit-test flags while refreshing runtime paint payloads for direct path, shape, and text nodes, avoiding duplicate paint-validity checks after retained compile has already computed them.
- Reused direct-path local fill/stroke paints while refreshing retained runtime payloads, and split opacity into a compact retained-node sidecar so opacity-only leaf nodes no longer allocate the larger clip/mask/filter effect state.
- Skipped publishing and clearing empty document-font provider scopes when no document font provider was previously active, trimming no-font retained compile/render setup while preserving nested document masking when an outer provider exists.
- Skipped retained textLength-specific compile probes before run collection when a text element has no own `textLength`, avoiding duplicate sequential-run collection for ordinary retained text while preserving the specialized paths for real `textLength` rows.
- Moved rare retained-node visual and resource-key state into lazy sidecars, so ordinary retained nodes do not carry cursor, background-layer, isolation/blend, clip, mask, or filter fields directly.
- Moved rare retained-node clip/mask/filter effect state into a lazy effect sidecar, including overflow/clip rectangles, filter clips, mask paints, and mask child nodes.
- Added cursor and `enable-background` to cascaded-style feature analysis so retained visual-state probes are skipped when the element's own declarations cannot contain those features.

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

Focused inline shim path command-list storage measurements for `generated-shapes-1024`:

- `SvgPathConversionBenchmarks.ConvertAllVisualPaths`: `1,216.3 us / 1072 KB` to `1.207 ms / 992 KB`.
- `SvgPathConversionBenchmarks.ConvertPrimitiveShapesOnly`: `290.0 us / 248 KB` to `367.2 us / 208 KB`.
- `SvgPathConversionBenchmarks.ConvertSvgPathsOnly`: `551.4 us / 824 KB` to `601.7 us / 784 KB`.
- `SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly`: local focused scan moved from `9.262 ms / 4050.05 KB` to `7.454 ms / 3.83 MB`.
- Timing remains short-run/noisy, so this is treated primarily as a managed-allocation cleanup for simple retained paths.

Focused native path value-cache measurement for `generated-shapes-1024`:

- `CreateNativePictureFromFullModel`: `1.107 ms / 1.68 KB`.
- `DrawNativePicture1x`: `4.386 ms` in a noisy short run, so native picture replay remains a follow-up hotspot rather than a claimed win for this change.

Focused small `AddPoly` revision-key reuse measurements:

- `ReplayFullModelIntoRecorderCanvasUsingCurrentLoop | generated-shapes-1024`: `1,993.8 us / 1.63 KB` to `1,149 us / 1.63 KB`.
- `ReplayFullModelIntoRecorderCanvasUsingCurrentLoop | generated-filtered-shapes-256`: `2,543.4 us / 51.69 KB` to `1,985 us / 51.69 KB`.

Focused transform-only simple shape group flattening measurements:

- `DrawNativePicture1x | generated-shapes-1024`: `4.985 ms` to `1.403 ms`.
- `DrawNativePicture1x | generated-filtered-shapes-256`: `3.054 ms` to `1.357 ms`.
- `DrawNativePicture1x | generated-text-path-curves-96`: `4.372 ms` to `2.322 ms`.
- `DrawNativePicture1x | generated-inline-styles-512`: `1.376 ms` to `751.7 us`.
- `DrawNativePicture1x | generated-flood-filters-256`: `339.6 us` to `192.3 us`.
- `CreateNativePictureFromFullModel | generated-shapes-1024`: latest focused run measured `185.5 us / 1.63 KB`.
- `CreateNativePictureFromFullModel | generated-filtered-shapes-256`: latest focused run measured `1.541 ms / 51.69 KB`.
- `CreateNativePictureFromFullModel | generated-text-path-curves-96`: latest focused run measured `132.5 us / 112.52 KB`.
- `CompileNodeTreeOnly | generated-shapes-1024`: `11.118 ms / 4168.41 KB` to `5.388 ms / 4168.43 KB`.
- `CompileNodeTreeOnly | generated-filtered-shapes-256`: `2.291 ms / 970.12 KB` to `851.3 us / 970.11 KB`.
- `CompileNodeTreeOnly | generated-text-path-curves-96`: `10.431 ms / 6934.63 KB` to `5.739 ms / 6934.46 KB` in a noisy short run.

Focused transformed simple-child renderer fallback measurements:

- `ReplayFullModelIntoRecorderCanvasUsingCurrentLoop | generated-shapes-1024`: current focused run measured `183.0 us / 1.63 KB`.
- `ReplayFullModelIntoRecorderCanvasUsingCurrentLoop | generated-filtered-shapes-256`: current focused run measured `1.503 ms / 51.69 KB`.
- `ReplayFullModelIntoRecorderCanvasUsingCurrentLoop | generated-inline-styles-512`: current focused run measured `217.9 us / 1.63 KB`.
- `ReplayFullModelIntoRecorderCanvasUsingCurrentLoop | generated-text-path-curves-96`: current focused run measured `135.5 us / 112.39 KB`.
- The generated scenarios stayed effectively flat against the existing post-flattening rows because they are already handled by compile-time transform-only group flattening; this change is retained as a no-allocation fallback cleanup rather than a new generated-scenario benchmark win.

Focused direct positioned text run replay measurements:

- `DrawNativePicture1x | generated-text-path-curves-96`: `4.543 ms` to `2.272 ms`.
- `DrawNativePicture1x | generated-aligned-letter-spacing-192`: latest focused run measured `521.3 us`.
- `DrawNativePicture1x | generated-aligned-text-length-192`: latest focused run measured `450.3 us`.
- `DrawNativePicture1x | generated-shapes-1024`: latest focused run measured `1.397 ms`.
- `DrawNativePicture1x | generated-filtered-shapes-256`: latest focused run measured `1.369 ms`.

Focused rotation-scale positioned text blob measurements:

- `DrawNativePicture1x | generated-text-path-curves-96`: latest post-direct-matrix scan `5.543 ms` to `2.320 ms`.
- `DrawNativePicture1x | generated-aligned-letter-spacing-192`: `1.053 ms` to `521.9 us`.
- `DrawNativePicture1x | generated-aligned-text-length-192`: `917.7 us` to `461.4 us`.
- `DrawNativePicture1x | generated-text-192`: `1.056 ms` to `512.9 us`.
- `CreateNativePictureFromFullModel | generated-text-path-curves-96`: current focused run measured `138.2 us / 112.39 KB`.
- `CreateNativePictureFromFullModel | generated-aligned-letter-spacing-192`: current focused run measured `154.4 us / 153.55 KB`.
- `CreateNativePictureFromFullModel | generated-aligned-text-length-192`: current focused run measured `97.46 us / 76.09 KB`.
- `CreateNativePictureFromFullModel | generated-text-192`: current focused run measured `64.32 us / 25.63 KB`.

Focused layer-depth replay-state measurements:

- `ReplayFullModelIntoRecorderCanvasUsingCurrentLoop | generated-shapes-1024`: `2,304.8 us / 1.68 KB` to `1,993.8 us / 1.63 KB`.
- `ReplayFullModelIntoRecorderCanvasUsingCurrentLoop | generated-filtered-shapes-256`: `2,894.1 us / 51.68 KB` to `2,543.4 us / 51.69 KB`.
- `CreateNativePictureFromFullModel | generated-filtered-shapes-256`: `3,512.0 us / 51.68 KB` to `2,982.8 us / 51.69 KB`.
- `CreateNativePictureFromFullModel | generated-shapes-1024`: `2,216.2 us / 1.68 KB` to `2,304.4 us / 1.63 KB`; this conversion row stayed within short-run noise, while the production replay row improved.

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

Focused no-reference node-tree dependency registration measurements:

- Before phase audit on `generated-shapes-1024`: `CreateSceneDocumentFromCompiledTree` measured `7.893 ms / 1,990,882 B`, `CompileViaSceneCompiler` measured `29.002 ms / 6,259,499 B`, and `CompileViaSceneRuntime` measured `30.226 ms / 6,285,190 B` in a noisy short run.
- Node-tree dependency registration for fully retained no-reference scenes measured `CreateSceneDocumentFromCompiledTree` at `2.002 ms / 1,424,403 B`, `CompileViaSceneCompiler` at `10.762 ms / 5,693,191 B`, and `CompileViaSceneRuntime` at `10.815 ms / 5,669,237 B`.
- `CompileNodeTreeOnly | generated-shapes-1024` stayed allocation-flat at about `4,268 KB`; this change targets scene-document dependency registration after the retained node tree is built.

Focused on-demand retained compilation-root lookup measurements:

- `CreateSceneDocumentFromCompiledTree | generated-shapes-1024`: `3.904 ms / 782866 B` to `1.629 ms / 764.34 KB`.
- `CreateSceneDocumentFromCompiledTree | generated-text-192`: `510.477 us / 698881 B` to `187.1 us / 582.7 KB`.
- `CompileViaSceneRuntime | generated-shapes-1024`: `15.839 ms / 4726099 B` to `9.532 ms / 4.53 MB`.
- `CompileViaSceneRuntime | generated-text-192`: `10.741 ms / 3992450 B` to `4.455 ms / 3.71 MB`.

Focused structural transform-origin and direct path bounds reuse measurements:

- `CompileNodeTreeOnly | generated-shapes-1024`: the shape phase audit measured `18.141 ms / 3,967,036 B`; the structural transform/path-bounds reuse run measured `13.724 ms / 3416.82 KB`.
- `CompileNodeTreeOnly | generated-filtered-shapes-256`: the retained hotspot scan measured `5.480 ms / 904.35 KB`; the structural transform/path-bounds reuse run measured `2.299 ms / 848.36 KB`.
- `CompileNodeTreeOnly | generated-text-192`: allocation stayed effectively flat at `2970.81 KB` to `2970.88 KB`.
- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: allocation stayed effectively flat/slightly lower at `3229.61 KB` to `3219.96 KB`.
- Short-run timings remained visibly noisy, so the retained shape allocation drop is the primary signal for this change.

Focused direct visual validity reuse measurements:

- `CompileNodeTreeOnly | generated-filtered-shapes-256`: the post-structural hotspot scan measured `848.37 KB`; reusing direct visual fill/stroke validity measured `818.36 KB` in the focused control rerun.
- `CompileNodeTreeOnly | generated-shapes-1024`: allocation stayed flat at `3416.79 KB` to `3416.81 KB`.
- `CompileNodeTreeOnly | generated-text-192`: allocation stayed flat at `2970.83 KB`.
- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: the focused control rerun measured `3230.5 KB`, effectively flat against the post-structural scan's `3220.44 KB`.
- Short-run timings were noisy, so the filtered-shape allocation drop is the primary signal for this small cleanup.

Focused retained runtime feature-gate and paint-hit flag reuse measurements:

- `ResolveRuntimePayloadsOnly | generated-shapes-1024`: current shape phase audit measured `4.015 ms / 363,160 B`; after retained clip/mask/filter runtime feature gates it measured `2.192 ms / 363,352 B`.
- `ResolveRuntimePayloadsOnly | generated-shapes-1024`: after runtime paint-hit flag reuse it measured `1.915 ms / 363,064 B`.
- `CreateSceneDocumentFromCompiledTree | generated-shapes-1024`: current shape phase audit measured `2.182 ms / 758,177 B`; after runtime paint-hit flag reuse it measured `1.405 ms / 758,133 B`.
- `CompileViaSceneRuntime | generated-shapes-1024`: current shape phase audit measured `12.856 ms / 4,282,457 B`; after runtime feature gates and paint-hit flag reuse it measured `7.817 ms / 4,282,551 B`.
- Short-run timings are phase-audit evidence for resolver work reduction; allocation stayed effectively flat because the change avoids lookups and paint-validity work rather than removing retained node storage.

Focused shim path-bounds cache measurement:

- `CompileNodeTreeOnly | generated-shapes-1024`: allocation dropped from the prior compact-root lookup row of `4,474,041 B` to `4.09 MB`.
- The same short-run benchmark measured `5.476 ms`, so this change is treated as an allocation reduction and regression guard rather than a wall-clock speed claim.

Focused `SKPath`-owned inline command storage measurements:

- `CompileNodeTreeOnly | generated-shapes-1024`: after the runtime feature-gate and paint-hit cleanup, the shape phase audit measured `3,498,792 B`; after moving inline command storage directly onto `SKPath`, it measured `3,285,771 B`.
- `CompileViaSceneCompiler | generated-shapes-1024`: after moving inline command storage directly onto `SKPath`, the focused phase scan measured `7.693 ms / 4,068,825 B`.
- `CompileViaSceneRuntime | generated-shapes-1024`: after moving inline command storage directly onto `SKPath`, the focused phase scan measured `7.844 ms / 4,069,568 B`, down from the pre-wrapper-removal allocation row around `4.28 MB`.
- This preserves the mutable `Commands` surface by making `SKPath` itself implement `IList<PathCommand>` while avoiding one command-list wrapper allocation per path.

Focused retained-scene child-list capacity and lazy compile-context measurements:

- Child-list capacity hints reduced `CompileNodeTreeOnly | generated-filtered-shapes-256` from `1,004,252 B` to `993,884 B`.
- Child-list capacity hints reduced `CompileNodeTreeOnly | generated-shapes-1024` from `4,293,800 B` to `4,269,028 B`.
- Full compile allocation also dropped: `CompileViaSceneCompiler | generated-filtered-shapes-256` from `1,898,560 B` to `1,888,166 B`, and `CompileViaSceneRuntime | generated-filtered-shapes-256` from `1,899,184 B` to `1,894,938 B`.
- Full compile allocation for `generated-shapes-1024` dropped from `6,309,427 B` to `6,260,019 B` in `CompileViaSceneCompiler`, and from `6,310,081 B` to `6,285,250 B` in `CompileViaSceneRuntime`.
- Final combined child-list capacity plus lazy compile-context measurement for `CompileNodeTreeOnly` was `970.11 KB` on `generated-filtered-shapes-256` and `4168.44 KB` on `generated-shapes-1024`; the lazy-context portion is a small constant allocation reduction on top of the child-list win.

Focused compact retained-node child storage measurements:

- `CompileNodeTreeOnly | generated-shapes-1024`: fresh retained hotspot scan allocation moved from `3922.03 KB` to `3874.03 KB`.
- `CompileNodeTreeOnly | generated-filtered-shapes-256`: fresh retained hotspot scan allocation moved from `916.32 KB` to `904.33 KB`.
- `CompileNodeTreeOnly | generated-text-192`: control row stayed essentially flat at `3215.36 KB`.
- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: control row stayed essentially flat at `3219.97 KB`.
- Short-run timings were mixed/noisy, so this is treated as a retained node allocation cleanup for low-fanout scene graphs.

Focused retained-node visual/resource/effect sidecar measurements:

- `CompileNodeTreeOnly | generated-shapes-1024`: fresh retained hotspot scan allocation moved from `3208.77 KB` to `3064.69 KB` after visual/resource sidecars, then to `2632.5 KB` after effect sidecars.
- `CompileNodeTreeOnly | generated-filtered-shapes-256`: fresh retained hotspot scan allocation moved from `766.37 KB` to `740.24 KB` after visual/resource sidecars, then to `631.94 KB` after effect sidecars.
- `CompileNodeTreeOnly | generated-text-192`: fresh retained hotspot scan allocation moved from `2765.34 KB` to `2756.27 KB` after visual/resource sidecars, then to `2728.96 KB` after effect sidecars.
- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: fresh retained hotspot scan allocation moved from `2811.57 KB` to `2802.48 KB` after visual/resource sidecars, then to `2775.2 KB` after effect sidecars.
- Post-sidecar hotspot scan retained `CompileNodeTreeOnly` allocations were `1935.11 KB` for `generated-aligned-text-length-192` and `1257.09 KB` for `generated-text-path-curves-96`, leaving ordinary/aligned text and generated shapes as the next retained compile allocation targets.
- Short-run timings were noisy, so the allocation drop is the primary signal for the lazy retained-node sidecars.

Focused post-sidecar text internals scan:

- `SplitCodepointsAcrossFragments | generated-text-192`: `18.636 us / 76.28 KB`.
- `MeasureNaturalCodepointAdvancesAcrossFragments | generated-text-192`: `429.774 us / 187.92 KB`.
- `MeasureNaturalTextAdvanceAcrossFragments | generated-text-192`: `714.979 us / 45 KB`.
- `SplitCodepointsAcrossFragments | generated-aligned-text-length-192`: `7.397 us / 53.14 KB`.
- `MeasureNaturalCodepointAdvancesAcrossFragments | generated-aligned-text-length-192`: `133.149 us / 67.5 KB`.
- `MeasureNaturalTextAdvanceAcrossFragments | generated-aligned-text-length-192`: `197.668 us / 12 KB`.
- This identified natural codepoint advance measurement as the next text-focused allocation target; natural full-text advance remained the larger timing hotspot.

Focused source-text/simple natural-codepoint cache text internals scan:

- `SplitCodepointsAcrossFragments | generated-aligned-letter-spacing-192`: `10.96 us / 57.64 KB`.
- `MeasureNaturalCodepointAdvancesAcrossFragments | generated-aligned-letter-spacing-192`: `161.82 us / 12 KB`.
- `MeasureNaturalTextAdvanceAcrossFragments | generated-aligned-letter-spacing-192`: `195.26 us / 12 KB`.
- `SplitCodepointsAcrossFragments | generated-aligned-text-length-192`: `10.72 us / 53.14 KB`.
- `MeasureNaturalCodepointAdvancesAcrossFragments | generated-aligned-text-length-192`: `159.53 us / 12 KB`.
- `MeasureNaturalTextAdvanceAcrossFragments | generated-aligned-text-length-192`: `196.02 us / 12 KB`.
- `SplitCodepointsAcrossFragments | generated-text-192`: `29.48 us / 76.28 KB`.
- `MeasureNaturalCodepointAdvancesAcrossFragments | generated-text-192`: `632.37 us / 45 KB`.
- `MeasureNaturalTextAdvanceAcrossFragments | generated-text-192`: `702.85 us / 45 KB`.
- Split-codepoint allocation stayed at the earlier read-only split-cache levels, while natural codepoint advance allocation dropped from `69 KB`/`67.5 KB`/`187.92 KB` to `12 KB`/`12 KB`/`45 KB` across the aligned letter-spacing, aligned textLength, and generated text rows.

Focused split-codepoint cache-hit fast-path measurements:

- `SplitCodepointsAcrossFragments | generated-aligned-letter-spacing-192`: `19.47 us / 57.64 KB` to `7.807 us / 57.64 KB`.
- `SplitCodepointsAcrossFragments | generated-aligned-text-length-192`: `19.43 us / 53.14 KB` to `7.242 us / 53.14 KB`.
- `SplitCodepointsAcrossFragments | generated-text-192`: `47.13 us / 76.28 KB` to `17.970 us / 76.28 KB`.
- Retained text compile follow-up rows measured `CompileNodeTreeOnly` at `1.372 ms / 1.78 MB` for `generated-aligned-text-length-192`, `2.348 ms / 2.58 MB` for `generated-aligned-letter-spacing-192`, and `2.846 ms / 2.49 MB` for `generated-text-192`; timings stayed short-run/noisy, while allocations remained aligned with the prior retained text scan.

Focused SVG-font metrics-guard retained/text benchmark scan:

- `CompileNodeTreeOnly | generated-aligned-text-length-192`: `1.83 MB`.
- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `2.65 MB`.
- `CompileNodeTreeOnly | generated-text-192`: `2.66 MB`.
- `CompileNodeTreeOnly | generated-text-path-curves-96`: `1.23 MB`.
- Expanded `SvgTextCompileInternalsBenchmarks` scenario selection now includes generated spacing rows; `generated-aligned-letter-spacing-192` measured `57.64 KB`, `69 KB`, and `12 KB` across split, natural-codepoint, and natural-text advance rows.
- Before the source-text/simple natural-codepoint cache work, `generated-text-192` remained a text-focused allocation target at `187.92 KB` for natural codepoint advance measurement and `45 KB` for natural text advance.

Focused retained-scene address-key cache measurements:

- `CompileNodeTreeOnly | generated-shapes-1024`: `4,268,450 B` to `4,147,255 B`.
- `CompileNodeTreeOnly | generated-text-192`: `3,295,716 B` to `3,291,042 B`.
- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `3,300,412 B` to `3,295,755 B`.
- The allocation win comes from pre-sized address-key dictionaries, cached child-index text for small indexes, and lazy child-index lookup dictionaries; no retained path sharing was introduced because `HitTestPath` remains publicly observable and mutable.

Focused retained scene-document index and dependency registration measurements:

- `RegisterDependenciesOnly | generated-shapes-1024`: `526,048 B` to effectively no managed allocation in the summary table.
- `CreateSceneDocumentFromCompiledTree | generated-shapes-1024`: `1,399,769 B` to `782,792 B` in the focused phase audit, with the broader create-document scan measuring `764.36 KB`.
- `CompileViaSceneRuntime | generated-shapes-1024`: `5,572,716 B` to `4,906,712 B`.
- `CompileViaSceneCompiler | generated-shapes-1024`: `5,547,262 B` to `4,905,699 B`.
- The broader create-document scan measured `202.82 KB` for both aligned text scenarios, `203.75 KB` for text-path curves, `682.51 KB` for generated text, `764.69 KB` for filtered shapes, and `1994.45 KB` for flood filters.

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

Focused source-free lazy retained text metric measurements:

- `CompileNodeTreeOnly | generated-text-192`: `7,913,332 B` to about `7,902,530 B`.
- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: about `16,359,636 B` to about `16,348,747 B`.
- `CompileNodeTreeOnly | generated-text-path-curves-96`: about `15,726,484 B` to about `15,721,074 B`.
- Short-run timings were noisy, so this is treated as a small retained compile allocation cleanup.

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

Focused aligned retained compile fast-path measurements:

- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: allocation moved from the current retained compile scan's `15,975.29 KB` to `12.26 MB`, with the focused short-run mean at `13.67 ms`.
- `CompileNodeTreeOnly | generated-aligned-text-length-192`: allocation moved from `12,223.97 KB` to `11.94 MB`, with the focused short-run mean at `14.93 ms`.

Focused retained text paint-clone trim measurement:

- `CompileNodeTreeOnly | generated-text-192`: `8.317 ms / 7.55 MB`.
- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `16.147 ms / 15.6 MB`.
- `CompileNodeTreeOnly | generated-text-path-curves-96`: `16.965 ms / 15 MB`.
- Allocations stayed flat against the previous lazy text-hit-metric row, so this is treated as a small clone-churn cleanup and focused regression guard rather than a new allocation step change.

Focused read-only codepoint split measurements:

- `ValidateTextContentDomMetrics | text-regression-positioned-layout`: `325.68 KB` to `324.84 KB`.
- `ValidateTextContentDomMetrics | text-regression-vertical-rtl-shape-layout`: `4943.46 KB` to `4942.09 KB`.
- `ValidateTextContentDomMetrics | text-regression-wrapped-textlength-positioned-descendants`: `2728.31 KB` to `2725.19 KB`.
- `ValidateTextContentDomMetrics | text-regression-vertical-rtl-layout` stayed allocation-flat at `11807.13 KB`.
- Short-run timings were mixed and noisy, so this is treated as a small managed-allocation cleanup for read-only text internals rather than a wall-clock speed claim.

Focused ASCII codepoint and single-run aligned retained compile measurements:

- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: current retained hotspot scan allocation moved from `3265.44 KB` to `3223.03 KB`.
- `CompileNodeTreeOnly | generated-aligned-text-length-192`: current retained hotspot scan allocation moved from `1980.55 KB` to `1965.55 KB`.
- `CreateAlignedPlacementsAcrossFragments` stayed allocation-flat at `574.27 KB` and `213.43 KB`, which confirms the retained compile reduction comes from compile-path collection/cache cleanup rather than the standalone placement benchmark's warmed-cache loop.
- Updated retained hotspot scan after this change: `generated-shapes-1024` remains the largest retained compile allocation at `4168.46 KB`; `generated-text-192` is `3218.43 KB`; `generated-aligned-letter-spacing-192` is `3223.03 KB`; `generated-text-path-curves-96` is `1305.75 KB`; `generated-aligned-text-length-192` is `1965.55 KB`; `generated-filtered-shapes-256` is `970.11 KB`.
- After moving inline command storage directly onto `SKPath`, the focused `generated-shapes-1024` retained compile row measured `3,285,771 B`; plain and aligned text scenarios remain the next retained compile allocation targets.
- `generated-text-192` phase audit shows remaining plain-text allocation is still dominated by `CompileNodeTreeOnly` (`3.996 ms / 3,295,669 B`), while scene-document creation is `737,417 B`, dependency registration is `95,168 B`, and runtime payload resolution is `57,416 B`.

Focused own-`textLength` probe guard measurements:

- `CompileNodeTreeOnly | generated-text-192`: current focused benchmark measured `2.943 ms / 2.70 MB` after skipping textLength-only run collection for ordinary text.
- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: current focused benchmark measured `2.016 ms / 2.75 MB`; short-run timing was noisy, but allocation stays below the prior post-text cleanup rows.
- `CompileNodeTreeOnly | generated-aligned-text-length-192`: current focused benchmark measured `1.357 ms / 1.93 MB`, confirming real `textLength` rows still use the specialized branch.
- `CompileNodeTreeOnly | generated-shapes-1024`: current focused control row measured `3.026 ms / 3.13 MB`, so shape allocation remains the largest retained compile row after the text probe cleanup.

### Text Path Performance

- Added a document-scoped fallback codepoint resolver with bounded caching for fallback text, resolved paint, advance, and optional local bounds.
- Reused fallback resolution across text-path draw, clip, measurement, decoration, filtered-run, and inline-size measurement paths.
- Cached fallback local glyph bounds on demand so repeated text-path bounds measurement avoids repeated native glyph path creation.
- Added fast paths for single-codepoint grapheme clusters in placement and text-DOM fallback cluster generation.
- Pre-sized text-path path-sample lists from the existing command and sampling-step logic so long cubic paths avoid repeated `List<PathSample>` backing-array growth while preserving the generated sample points.
- Reused measured natural advances for simple ASCII text-path placement and replaced the final LINQ placement projection with an indexed copy, avoiding an advance-copy array and iterator allocation while leaving complex grapheme cluster redistribution on the existing path.
- Emitted positioned text-path placements directly from the compiler once advances and spacing are resolved, preserving the planner's path clipping and tangent math while removing the intermediate cluster-input and placement-plan arrays from the hot path.
- Resolved same-document text-path `#id` geometry references without reparsing a `Uri` on cache hits, and hashed raw typed `SvgPathSegmentList` data for referenced path signatures so repeated cache-key creation does not serialize parsed `d` attributes back to strings.

Focused benchmark results for `svg2-textpath-side-right-128` retained-scene compilation:

- Earlier post-cache baseline: `105.433 ms / 67.11 MB`.
- After document-scoped fallback cache: `71.149 ms / 44 MB`.
- After fallback bounds cache: `53.589 ms / 29.2 MB`.

Focused text-path sample capacity measurements for `generated-text-path-curves-96`:

- `ResolveTextPathGeometryAcrossFragments`: `6.912 ms / 26.46 MB` to `668.745 us / 1.05 MB`.
- `PrepareTextPathPlacementInputsAcrossFragments`: `7.498 ms / 26.71 MB` to `1.116 ms / 1.31 MB`.
- `CreateTextPathPlacementsFromPrebuiltGeometryAcrossFragments`: `3.097 ms / 12.69 MB` to `561.275 us / 868.55 KB`.
- `CompileNodeTreeOnly | generated-text-path-curves-96`: `15.622 ms` to `13.204 ms`, with whole retained-compile allocation effectively flat at `14.99 MB`.

Focused text-path placement allocation trim measurements for `generated-text-path-curves-96`:

- `CreateTextPathPlacementsFromPrebuiltGeometryAcrossFragments`: `868.55 KB` to `673,793 B`.
- `CompileNodeTreeOnly | generated-text-path-curves-96`: retained compile allocation moved from `14.99 MB` to `14.63 MB`; short-run timings were noisy, so this is treated as an allocation cleanup.
- After direct compiler-side placement emission, `CreateTextPathPlacementsFromPrebuiltGeometryAcrossFragments` allocation dropped again from `673,793 B` to `144,912 B`.
- The same direct emission pass moved `CompileNodeTreeOnly | generated-text-path-curves-96` retained compile allocation from `14.63 MB` to `13.86 MB`; short-run timings were noisy, so this is treated as an allocation cleanup.

Focused text-path geometry cache-hit measurements for `generated-text-path-curves-96`:

- `ResolveTextPathGeometryAcrossFragments`: `1,053,326 B` to `3,840 B`, with the focused short-run mean moving to `30.32 us`.
- `PrepareTextPathPlacementInputsAcrossFragments`: `1,310,739 B` to `261,255 B`.
- `CreateTextPathPlacementsFromPrebuiltGeometryAcrossFragments` stayed at the direct-emission allocation level of `144,913 B`.
- `CompileNodeTreeOnly | generated-text-path-curves-96`: retained compile allocation moved from `13.86 MB` to `11.61 MB`; short-run timings remained noisy, so this is treated primarily as an allocation cleanup.

Focused direct textPath retained compile fast-path measurement for `generated-text-path-curves-96`:

- `CompileNodeTreeOnly`: the post-aligned retained scan measured `17.350 ms / 11,889.16 KB`; the direct textPath compile fast path measured `13.504 ms / 9.89 MB`.

Focused simple textPath recording guard measurements:

- `CreateTextPathPlacementsFromPrebuiltGeometryAcrossFragments | generated-text-path-curves-96`: audit control measured `1.582 ms / 144,913 B`, confirming placement creation is no longer the dominant retained compile allocation after the direct emission pass.
- `CompileNodeTreeOnly | generated-text-path-curves-96`: line-stats control measured `12.251 ms / 9.92 MB`; simple ASCII grapheme-probe and empty-decoration recording guards measured `12.049 ms / 9.75 MB`.

Focused textPath offset measurement skip for `generated-text-path-curves-96`:

- `CompileNodeTreeOnly`: the post-scaled-textLength retained scan measured `67.316 ms / 10,288.11 KB`; skipping the redundant textPath advance pre-measure for left-aligned, left-side paths measured `36.090 ms / 8.86 MB` in the focused retained compile run.

Focused simple positioned textPath run measurements for `generated-text-path-curves-96`:

- `CompileNodeTreeOnly`: the post-offset retained scan measured `42.771 ms / 9,155.47 KB`; the guarded simple ASCII positioned textPath bounds/draw path measured `10.603 ms / 10.65 MB` in the focused retained compile run.

Focused compact positioned textPath command measurements for `generated-text-path-curves-96`:

- `CompileNodeTreeOnly`: the latest retained textPath scan before compaction measured `15.821 ms / 10,905.51 KB`; compact long simple positioned textPath command recording measured `6.492 ms / 9.16 MB` in the focused retained compile run.

Focused fast positioned textPath bounds measurements for `generated-text-path-curves-96`:

- Fresh retained hotspot scan: `27.579 ms / 9403.67 KB`.
- Fast metric bounds reuse for simple positioned textPath runs: `4.542 ms / 6.77 MB`.

Focused SVG-font registry guard measurements for `generated-text-path-curves-96`:

- `CreateTextPathPlacementsFromPrebuiltGeometryAcrossFragments`: allocation dropped from the current retained placement audit's `3,281,936 B` to `400,657 B`; the focused short-run mean measured `2.137 ms`.
- `CompileNodeTreeOnly`: the current post-RSXForm text-path retained compile scan measured `9.794 ms / 7,115,064 B`; skipping per-codepoint SVG-font probes in non-SVG-font documents measured `2.207 ms / 1.28 MB`.

Focused positioned text-blob retained compile measurements:

- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: the post-direct-textPath retained scan measured `73.072 ms / 12.26 MB`; positioned text-blob recording measured `10.44 ms / 11.17 MB`.
- `CompileNodeTreeOnly | generated-aligned-text-length-192`: the post-direct-textPath retained scan measured `41.362 ms / 11.94 MB`; positioned text-blob recording measured `13.48 ms / 11.94 MB`.
- Allocation still points at textLength layout preparation as a follow-up hotspot; the win here is retained command recording and draw-path simplification for unrotated/unscaled placements.

Focused uniform scaled positioned text-blob retained compile measurements:

- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `12.21 ms / 12.31 MB` in the same focused control run after the scaled-path extension.
- `CompileNodeTreeOnly | generated-aligned-text-length-192`: `16.79 ms / 12.84 MB` after replacing simple `spacingAndGlyphs` per-glyph scale/draw recordings with one scaled positioned text blob.
- Allocation still points at textLength layout preparation as a follow-up hotspot; this change targets retained command count and draw-path simplification for uniformly scaled placements.

Focused font-scale positioned text-blob command cleanup measurements:

- The retained `spacingAndGlyphs` textLength regression now records one positioned text blob with `SKFont.ScaleX > 1.1` and no local `SetMatrixCanvasCommand` wrapper for the simple scaled run.
- `CreateNativePictureFromFullModel | generated-aligned-letter-spacing-192`: `375.4 us / 149.2 KB` in the control scenario.
- `CreateNativePictureFromFullModel | generated-aligned-text-length-192`: `251.8 us / 137.95 KB` after encoding scale in the blob font instead of replaying save/set-matrix/restore around the blob.
- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `36.70 ms / 12.18 MB` in a noisy control run.
- `CompileNodeTreeOnly | generated-aligned-text-length-192`: `54.21 ms / 12.94 MB`; retained compile timings remained noisy and this cleanup is treated as command-stream simplification rather than an allocation win.

Focused direct scaled text command measurements:

- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `13.21 ms / 12.18 MB` in the same short control run.
- `CompileNodeTreeOnly | generated-aligned-text-length-192`: `18.56 ms / 12.68 MB`, down from the focused audit row's `45.41 ms / 12.84 MB`; the control timing also shifted substantially, so the cleaner signal is the retained textLength allocation drop after avoiding positioned blob point materialization for simple scaled runs.

Focused single-span scaled textLength command measurements:

- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `12.91 ms / 12.18 MB` in the same focused control run.
- `CompileNodeTreeOnly | generated-aligned-text-length-192`: `13.01 ms / 10.65 MB`, down from the direct scaled command row's `18.56 ms / 12.68 MB`, after reusing cached natural advance and resolving the simple scaled command paint through one verified typeface span instead of the broader run-typeface resolver.

Focused simple fixed-spacing positioned text-blob measurements:

- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `1.766 ms / 2.79 MB`, down from the fresh retained hotspot scan's `57.08 ms / 12,569.67 KB`, after building positioned text-blob points directly and reusing full-run local bounds for simple fixed spacing.
- `CompileNodeTreeOnly | generated-aligned-text-length-192`: `12.866 ms / 10.65 MB` as the textLength control, allocation-flat against the single-span scaled textLength row.

Focused simple root textLength spacing positioned text-blob measurements:

- `CompileNodeTreeOnly | generated-aligned-text-length-192`: `7.591 ms / 6 MB`, down from the fresh retained hotspot scan's `71.45 ms / 11,102.6 KB`, after recording simple root `lengthAdjust="spacing"` rows as one positioned text blob and keeping the existing scaled-command path for `spacingAndGlyphs`.
- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `2.427 ms / 3.19 MB` as the fixed-spacing control row.

Focused simple root scaled textLength retained compile measurements:

- `CompileNodeTreeOnly | generated-aligned-text-length-192`: `2.822 ms / 1.93 MB`, down from the current post-textPath retained hotspot scan's `68.703 ms / 6.4 MB`, after compiling simple root `lengthAdjust="spacingAndGlyphs"` rows directly to scaled text commands instead of using the generic measure-record traversal.

Focused retained sequential line-stats cache measurements:

- `CompileNodeTreeOnly | generated-text-192`: `3.829 ms / 3.14 MB`, down from the fresh retained hotspot scan's `66.16 ms / 9.49 MB`, after caching repeated simple sequential line stats across the generated `Item `, ` sample `, and `glyphs` fragments.
- `CompileNodeTreeOnly | generated-aligned-text-length-192`: `7.199 ms / 6 MB` and `CompileNodeTreeOnly | generated-text-path-curves-96`: `12.251 ms / 9.92 MB` in the same control run.

Focused SVG-font sequential eligibility probe measurements:

- Fresh retained hotspot scan: `CompileNodeTreeOnly | generated-text-192` measured `24.857 ms / 4059.38 KB`.
- Skipping the redundant SVG-font layout eligibility probe for non-SVG-font documents measured `CompileNodeTreeOnly | generated-text-192` at `12.758 ms / 2.9 MB`.
- Control rows in the same focused run measured `CompileNodeTreeOnly | generated-aligned-letter-spacing-192` at `10.979 ms / 3.14 MB` and `CompileNodeTreeOnly | generated-text-path-curves-96` at `7.794 ms / 1.27 MB`; short-run timings remained noisy, so the allocation drop on `generated-text-192` is the primary signal.

Focused aligned compile codepoint-bound reuse measurements:

- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `12.65 ms / 12.18 MB`, down from the recent retained compile scan's `12.32 MB` and the prior scaled text-blob control run's `12.31 MB`.
- `CompileNodeTreeOnly | generated-aligned-text-length-192`: `18.11 ms / 12.84 MB`, allocation-flat against the prior scaled text-blob row and kept as a textLength control.
- Short-run timings remained noisy, so this is treated as a small retained compile allocation cleanup.

Focused lazy text reference-seed measurements:

- Baseline `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `56.62 ms / 12.19 MB` in a noisy cold short run.
- Current `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `14.52 ms / 12.18 MB`; allocation is effectively flat/slightly lower and timing is treated as noise-sensitive.
- Baseline `CompileNodeTreeOnly | generated-aligned-text-length-192`: `64.44 ms / 12.85 MB` in the same noisy cold short run.
- Current `CompileNodeTreeOnly | generated-aligned-text-length-192`: `22.32 ms / 12.80 MB`, a small managed allocation trim from removing eager per-text reference-set creation.
- `SvgTextCompileInternalsBenchmarks` confirmed split/advance work is not the remaining large allocation source for this scenario: `MeasureNaturalCodepointAdvancesAcrossFragments | generated-aligned-text-length-192` measured `265.02 us / 67.5 KB`.

Focused flattened textLength materialization measurements:

- Before `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `50.43 ms / 12472.22 KB` in the same noisy current hotspot scan.
- Current `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `12.20 ms / 12.18 MB`; this is a control row because the change targets flattened textLength.
- Before `CompileNodeTreeOnly | generated-aligned-text-length-192`: `72.18 ms / 13253.13 KB` in the current hotspot scan.
- Current `CompileNodeTreeOnly | generated-aligned-text-length-192`: `16.95 ms / 12.8 MB`, trimming temporary flattened textLength materialization allocation while timing remains treated as noise-sensitive.

Focused grouped flattened textLength advance measurement:

- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `12.266 ms / 12.18 MB` as the control scenario.
- `CompileNodeTreeOnly | generated-aligned-text-length-192`: `16.771 ms / 12.8 MB`, allocation-flat and slightly lower than the prior `16.95 ms / 12.8 MB` textLength row; this is treated as timing-noise-sensitive reuse of the full-run advance engine rather than a managed allocation claim.

Focused value-type flattened codepoint measurements:

- `ValidateTextContentDomMetrics | text-regression-wrapped-textlength-positioned-descendants`: `3.591 ms / 2.63 MB`, a small allocation cleanup against the prior documented `2725.19 KB` row for the same direct flattened textLength DOM-metrics regression.
- `CompileNodeTreeOnly | generated-text-path-curves-96`: `10.561 ms / 9.89 MB` in the retained compile control run.
- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `11.787 ms / 12.18 MB` as the non-flattened aligned text control.
- `CompileNodeTreeOnly | generated-aligned-text-length-192`: `16.715 ms / 12.8 MB` as the aligned textLength control.

Focused indexed shared inline-size grouping measurements:

- `CompileTextLayoutRegressionScene | text-regression-shared-layout-engine-integration`: `1.658 ms / 1.43 MB`, down from the prior documented `7.360 ms / 4188.16 KB` shared-layout regression row.
- `CompileTextLayoutRegressionScene | text-regression-wrapped-textlength-positioned-descendants`: `6.662 ms / 4.26 MB`, down from the prior documented `19.298 ms / 17791.28 KB` wrapped textLength retained compile row.
- `ValidateTextContentDomMetrics | text-regression-shared-layout-engine-integration`: `955.0 us / 807.03 KB`, down from the prior documented `5.897 ms / 2.23 MB` shared-layout DOM-metrics row.
- `ValidateTextContentDomMetrics | text-regression-wrapped-textlength-positioned-descendants`: `3.777 ms / 2636.85 KB`, allocation-flat against the prior value-type flattened codepoint row and treated as a regression guard for wrapped textLength metrics.

Focused grouped wrapped inline-size textLength measurements:

- `CompileTextLayoutRegressionScene | text-regression-wrapped-textlength-positioned-descendants`: `6.696 ms / 4.26 MB`, allocation-flat against the prior indexed shared inline-size grouping row and kept as a compile regression guard.
- `ValidateTextContentDomMetrics | text-regression-wrapped-textlength-positioned-descendants`: `3.531 ms / 2.58 MB`, down from the prior indexed shared inline-size grouping row's `3.777 ms / 2636.85 KB` and the value-type flattened codepoint row's `3.591 ms / 2.63 MB`.

Focused flattened codepoint text-view measurements:

- `CompileTextLayoutRegressionScene | text-regression-shared-layout-engine-integration`: `1.697 ms / 1.43 MB`, allocation-flat against the indexed shared inline-size grouping row.
- `CompileTextLayoutRegressionScene | text-regression-wrapped-textlength-positioned-descendants`: `6.876 ms / 4.25 MB`, a small allocation trim from the prior grouped wrapped textLength row's `4.26 MB`; short-run timing stayed noise-sensitive.
- `ValidateTextContentDomMetrics | text-regression-shared-layout-engine-integration`: `1.780 ms / 807.02 KB`, allocation-flat against the prior `807.03 KB` row with noisy timing.
- `ValidateTextContentDomMetrics | text-regression-wrapped-textlength-positioned-descendants`: `3.658 ms / 2635.08 KB`, a small allocation trim from the prior `2636.85 KB` indexed shared-layout guard and effectively flat against the grouped wrapped textLength row.

Focused flattened advance range-view measurements:

- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `15.873 ms / 12.18 MB` as the aligned spacing control.
- `CompileNodeTreeOnly | generated-aligned-text-length-192`: `26.047 ms / 12.8 MB`, allocation-flat in the MB-rounded retained-scene row after removing the per-range projection array.
- `CompileTextLayoutRegressionScene | text-regression-wrapped-textlength-positioned-descendants`: `7.575 ms / 4.25 MB`, retained as the focused wrapped textLength compile regression guard.

Focused flattened textLength run-storage measurements:

- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `38.73 ms / 12.18 MB` as the non-textLength aligned control; short-run timing was noisy.
- `CompileNodeTreeOnly | generated-aligned-text-length-192`: `45.04 ms / 12.79 MB`, a small allocation trim from the prior MB-rounded `12.8 MB` row after removing transient run-list storage.
- `ValidateTextContentDomMetrics | text-regression-wrapped-textlength-positioned-descendants`: `3.793 ms / 2,698,181 B`, effectively flat but slightly lower than the prior `2635.08 KB` wrapped textLength DOM guard.
- `CompileTextLayoutRegressionScene | text-regression-wrapped-textlength-positioned-descendants`: `13.903 ms / 4,466,148 B`, kept as the wrapped textLength compile regression guard with timing treated as noise-sensitive.

Focused shared inline-size exact-run/text construction measurements:

- `CompileTextLayoutRegressionScene | text-regression-shared-layout-engine-integration`: `2.287 ms / 1.43 MB`, allocation-flat against the prior focused shared-layout compile row.
- `CompileTextLayoutRegressionScene | text-regression-wrapped-textlength-positioned-descendants`: `11.800 ms / 4.26 MB`, allocation-flat against the prior wrapped inline-size textLength compile rows; short-run timing remained noise-sensitive.
- `ValidateTextContentDomMetrics | text-regression-shared-layout-engine-integration`: `1.493 ms / 807.58 KB`, effectively flat against the prior `807.02 KB` shared-layout DOM guard.
- `ValidateTextContentDomMetrics | text-regression-wrapped-textlength-positioned-descendants`: `4.690 ms / 2631.05 KB`, a small allocation trim from the prior `2635.08 KB` wrapped textLength DOM guard after removing transient compiler-run storage and `StringBuilder` flattened-text construction.

Focused wrapped inline-size exact group/text and direct placement measurements:

- `CompileTextLayoutRegressionScene | text-regression-wrapped-textlength-positioned-descendants`: `40,880.56 us / 4,461,270 B` to `9,405.57 us / 4,452,432 B`.
- `ValidateTextContentDomMetrics | text-regression-wrapped-textlength-positioned-descendants`: `31,438.29 us / 2,996,603 B` to `5,207.85 us / 2,694,080 B`.
- `LoadRenderAndValidateTextLayoutRegression | text-regression-wrapped-textlength-positioned-descendants`: `53,401.49 us / 5,545,963 B` to `21,146.62 us / 5,463,785 B`.
- `RenderTextLayoutRegressionBitmap | text-regression-wrapped-textlength-positioned-descendants`: `6,132.97 us` to `3,409.48 us`.

### Natural Text Advance Performance

- Added a bounded whole-run natural text advance cache keyed by document, asset loader, paint/font signature, SVG text style, bidi mode, language, and SVG-font-sensitive state.
- Added a guarded simple natural text advance cache-hit path that skips repeated `SKPaint` and `SKTypeface` setup when font-size resolution is direct and no inherited OpenType text paint properties are active; relative font sizes, custom OpenType settings, and `altGlyph` continue through the full cache key path.
- Kept the cache wired through the prepared-text cache clear path and added regression coverage for same-style reuse and font-size-sensitive recomputation.

Focused benchmark results for `SvgTextCompileInternalsBenchmarks.MeasureNaturalTextAdvanceAcrossFragments`:

- `generated-aligned-text-length-192`: `7.332 ms / 3349.78 KB` to `222.031 us / 45 KB`.
- `generated-text-192`: `10.008 ms / 4592.33 KB` to `824.006 us / 144.01 KB`.
- `generated-text-path-curves-96`: `6.974 ms / 2884.8 KB` to `119.345 us / 22.5 KB`.

Focused simple natural text advance cache-hit measurements:

- `MeasureNaturalTextAdvanceAcrossFragments | generated-aligned-text-length-192`: `222.031 us / 45 KB` to `202.074 us / 12 KB`.
- `MeasureNaturalTextAdvanceAcrossFragments | generated-text-192`: `824.006 us / 144.01 KB` to `732.645 us / 45 KB`.
- `MeasureNaturalTextAdvanceAcrossFragments | generated-text-path-curves-96`: `119.345 us / 22.5 KB` to `102.129 us / 6 KB`.
- `CompileNodeTreeOnly | generated-text-192`: `7.587 ms` to `6.264 ms`, with retained compile allocation effectively flat at `7.54 MB`.
- `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: `34.229 ms` to `15.006 ms`, with retained compile allocation effectively flat at `15.59 MB`.
- `CompileNodeTreeOnly | generated-text-path-curves-96`: `27.097 ms` to `15.622 ms`, with retained compile allocation effectively flat at `14.99 MB`.
- Short-run timings still have visible noise, but the internals allocation drop and retained compile timing movement are both favorable.

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
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
- `dotnet build Svg.Skia.slnx -c Release`
  - Succeeded with 297 existing warnings.
- `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2599, skipped 40.
  - Other test projects in the solution passed.
- Focused natural text advance cache validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests.MeasureNaturalTextAdvance"`
  - Passed 2.
- Focused simple natural text advance cache validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests|FullyQualifiedName~SvgTextPathParityTests|FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 492.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192,generated-text-path-curves-96,generated-aligned-letter-spacing-192,generated-aligned-text-length-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-simple-natural-advance-cache dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextCompileInternalsBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192,generated-aligned-letter-spacing-192,generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-simple-natural-advance-cache-retained dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
- Focused text-path sample capacity validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with no warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgTextPathParityTests"`
  - Passed 185.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-text-path-sample-capacity dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextPathPlacementBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-text-path-sample-capacity-retained dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
- Focused text-path placement allocation trim validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgTextPathParityTests"`
  - Passed 185.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-text-path-placement-trim dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextPathPlacementBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-text-path-placement-trim-retained dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
- Focused direct text-path placement emission validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgTextPathParityTests"`
  - Passed 185.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-text-path-direct-placement dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextPathPlacementBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-text-path-direct-placement-retained dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
- Focused text-path geometry cache-hit validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgTextPathParityTests|FullyQualifiedName~SvgSceneTextCompilerTests"`
  - Passed 185.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-text-path-raw-pathdata-full-placement dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextPathPlacementBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-retained-text-path-raw-pathdata-signature dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
- Focused direct primitive path validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SKSvgRebuildFromModelTests.RebuildFromModel_ReflectsMutatedPathAfterInitialNativeBuild|FullyQualifiedName~SKSvgRebuildFromModelTests.RebuildFromModel_CanUpdateCommandsForSourceElementId"`
  - Passed 2.
- Focused closed line-only path conversion validation:
  - `dotnet test tests/Svg.Model.UnitTests/Svg.Model.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~PathingServiceTests"`
  - Passed 11.
- Focused native path value-cache validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SKSvgRebuildFromModelTests"`
  - Passed 20.
- Focused small `AddPoly` revision-key validation:
  - `dotnet build src/Svg.Skia/Svg.Skia.csproj -c Release --no-restore`
  - Build passed with existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SKSvgRebuildFromModelTests"`
  - Passed 20.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256 SVG_SKIA_BENCHMARK_RUN_LABEL=current-small-poly-revision-key dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgNativeSkPictureBenchmarks.ReplayFullModelIntoRecorderCanvasUsingCurrentLoop*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused layer-depth replay-state validation:
  - `dotnet build src/Svg.Skia/Svg.Skia.csproj -c Release --no-restore`
  - Build passed with existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgResourceRenderingParityTests|FullyQualifiedName~W3CTestSuiteTests.Tests"`
  - Passed 894, skipped 3.
  - Before: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256 SVG_SKIA_BENCHMARK_RUN_LABEL=current-before-layer-depth-state dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgNativeSkPictureBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - After: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256 SVG_SKIA_BENCHMARK_RUN_LABEL=current-layer-depth-state dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgNativeSkPictureBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
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
- Focused retained-scene address-key cache validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with existing Svg.Custom obsolete warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgResourceRenderingParityTests|FullyQualifiedName~SKSvgRebuildFromModelTests"`
  - Passed 393.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-text-192,generated-aligned-letter-spacing-192 SVG_SKIA_BENCHMARK_RUN_LABEL=address-key-cache-capacity-retained-compile dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
- Focused retained scene-document index and dependency registration validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed across target frameworks with no warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgResourceRenderingParityTests|FullyQualifiedName~SKSvgRebuildFromModelTests|FullyQualifiedName~HitTestTests"`
  - Passed 414.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024 SVG_SKIA_BENCHMARK_RUN_LABEL=document-index-capacity-shapes-phase-audit dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256,generated-flood-filters-256,generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=document-index-capacity-create-doc-scan dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CreateSceneDocumentFromCompiledTree*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - `dotnet build Svg.Skia.slnx -c Release`
  - Passed with existing package vulnerability, obsolete API, nullable, and ref/in warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - Passed; `Svg.Skia.UnitTests`: passed 2598, skipped 40.
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
- Focused source-free lazy retained text metrics validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests|FullyQualifiedName~SvgTextPathParityTests|FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 492.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192,generated-aligned-letter-spacing-192,generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-lazy-text-metrics-source-free dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2594, skipped 40; other test projects passed.
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
- Focused aligned retained compile fast-path validation:
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with no warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests"`
  - Passed 487.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-letter-spacing-192,generated-aligned-text-length-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-aligned-sequential-fastpath dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2594, skipped 40; other test projects passed.
- Focused positioned text-blob retained compile validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with no warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests"`
  - Passed 487.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-letter-spacing-192,generated-aligned-text-length-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-positioned-textblob-compile dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused uniform scaled positioned text-blob retained compile validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~RetainedSceneGraph_TextLengthSpacingAndGlyphs_RecordsScaledTextBlob"`
  - Passed 1.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests"`
  - Passed 488.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-letter-spacing-192,generated-aligned-text-length-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-scaled-positioned-textblob dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2595, skipped 40; other test projects passed.
- Focused font-scale positioned text-blob command cleanup validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~RetainedSceneGraph_TextLengthSpacingAndGlyphs_RecordsScaledTextBlob|FullyQualifiedName~RetainedSceneGraph_LengthAdjustSpacingAndGlyphs_UsesScaledTextBlobFont|FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 264.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests"`
  - Passed 488.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-letter-spacing-192,generated-aligned-text-length-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-scaled-textblob-font-scale dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-letter-spacing-192,generated-aligned-text-length-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-scaled-textblob-font-scale-native dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModel*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with 277 existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2595, skipped 40; other test projects passed.
- Focused direct scaled text command validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_TextLengthSpacingAndGlyphs|FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_LengthAdjustSpacingAndGlyphs|FullyQualifiedName~SvgSceneTextCompilerTests"`
  - Passed 182.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-text-length-192,generated-aligned-letter-spacing-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-scaled-textlength-command dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
- Focused single-span scaled textLength command validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with no warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_TextLengthSpacingAndGlyphs|FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_LengthAdjustSpacingAndGlyphs|FullyQualifiedName~SvgSceneTextCompilerTests"`
  - Passed 182.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-text-length-192,generated-aligned-letter-spacing-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-scaled-textlength-single-span dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
- Focused simple fixed-spacing positioned text-blob validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_LetterAndWordSpacing|FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_TextLengthSpacingAndGlyphs|FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_LengthAdjustSpacingAndGlyphs|FullyQualifiedName~SvgSceneTextCompilerTests"`
  - Passed 183.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-letter-spacing-192,generated-aligned-text-length-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-simple-spacing-positioned-blob dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
- Focused simple root textLength spacing positioned text-blob validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_TextLengthSpacing|FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_TextLengthSpacingAndGlyphs|FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_LengthAdjustSpacingAndGlyphs|FullyQualifiedName~SvgSceneTextCompilerTests"`
  - Passed 183.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-text-length-192,generated-aligned-letter-spacing-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-simple-textlength-spacing-blob dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
- Focused simple root scaled textLength retained compile validation:
  - `dotnet build Svg.Skia.slnx -c Release --no-restore`
  - Build passed with existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 265.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-text-length-192 SVG_SKIA_BENCHMARK_RUN_LABEL=scaled-textlength-retained-compile-fastpath dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2597, skipped 40; other test projects passed.
- Focused retained sequential line-stats cache validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests.TryCompileSequentialText_ReusesLineStatsForRepeatedStyleRuns|FullyQualifiedName~SvgSceneTextCompilerTests.TryCompileSequentialText_Succeeds_ForDirectedAsciiRuns|FullyQualifiedName~SvgSceneTextCompilerTests.TryCompileSequentialText_FallsBack"`
  - Passed 5.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192,generated-text-path-curves-96,generated-aligned-text-length-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-line-stats-cache dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
- Focused aligned compile codepoint-bound reuse validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests"`
  - Passed 488.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-letter-spacing-192,generated-aligned-text-length-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-aligned-codepoint-bound-reuse dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with 277 existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2595, skipped 40; other test projects passed.
- Focused lazy text reference-seed validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgTextPathParityTests|FullyQualifiedName~SvgResourceRenderingParityTests"`
  - Passed 556.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-letter-spacing-192,generated-aligned-text-length-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-next-aligned-baseline dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-letter-spacing-192,generated-aligned-text-length-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-lazy-text-reference-seed dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-letter-spacing-192,generated-aligned-text-length-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-next-text-internals-baseline dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextCompileInternalsBenchmarks*" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with 277 existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2595, skipped 40; other test projects passed.
- Focused flattened textLength materialization validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed for all target frameworks.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests"`
  - Passed 488.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256,generated-flood-filters-256,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-next-retained-hotspot-scan dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-letter-spacing-192,generated-aligned-text-length-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-flattened-textlength-onechunk-trim dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with 277 existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2595, skipped 40; other test projects passed.
- Focused grouped flattened textLength advance validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 444.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-text-length-192,generated-aligned-letter-spacing-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-flattened-textlength-grouped-advances dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with 277 existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2595, skipped 40; other test projects passed.
- Focused value-type flattened codepoint validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests"`
  - Passed 488.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-text-length-192,generated-aligned-letter-spacing-192,generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-flattened-codepoint-struct dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=text-regression-wrapped-textlength-positioned-descendants SVG_SKIA_BENCHMARK_RUN_LABEL=current-flattened-codepoint-struct-dom-metrics dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextRegressionValidationBenchmarks.ValidateTextContentDomMetrics*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with 277 existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2595, skipped 40; other test projects passed.
- Focused indexed shared inline-size grouping validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 264.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests"`
  - Passed 180.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=text-regression-shared-layout-engine-integration,text-regression-wrapped-textlength-positioned-descendants SVG_SKIA_BENCHMARK_RUN_LABEL=current-shared-layout-indexed-groups dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextRegressionValidationBenchmarks.CompileTextLayoutRegressionScene*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=text-regression-shared-layout-engine-integration,text-regression-wrapped-textlength-positioned-descendants SVG_SKIA_BENCHMARK_RUN_LABEL=current-shared-layout-indexed-groups-dom-metrics dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextRegressionValidationBenchmarks.ValidateTextContentDomMetrics*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with 277 existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2595, skipped 40; other test projects passed.
- Focused grouped wrapped inline-size textLength validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 444.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=text-regression-wrapped-textlength-positioned-descendants SVG_SKIA_BENCHMARK_RUN_LABEL=current-wrapped-textlength-grouped-advances dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextRegressionValidationBenchmarks.CompileTextLayoutRegressionScene*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=text-regression-wrapped-textlength-positioned-descendants SVG_SKIA_BENCHMARK_RUN_LABEL=current-wrapped-textlength-grouped-advances-dom-metrics dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextRegressionValidationBenchmarks.ValidateTextContentDomMetrics*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with 277 existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2595, skipped 40; other test projects passed.
- Focused flattened codepoint text-view validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with 24 existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 444.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=text-regression-shared-layout-engine-integration,text-regression-wrapped-textlength-positioned-descendants SVG_SKIA_BENCHMARK_RUN_LABEL=current-flattened-codepoint-text-view dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextRegressionValidationBenchmarks.ValidateTextContentDomMetrics*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=text-regression-shared-layout-engine-integration,text-regression-wrapped-textlength-positioned-descendants SVG_SKIA_BENCHMARK_RUN_LABEL=current-flattened-codepoint-text-view-compile dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextRegressionValidationBenchmarks.CompileTextLayoutRegressionScene*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused flattened advance range-view validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with 24 existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 444.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-text-length-192,generated-aligned-letter-spacing-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-flattened-advance-range-view dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=text-regression-wrapped-textlength-positioned-descendants SVG_SKIA_BENCHMARK_RUN_LABEL=current-flattened-advance-range-view-regression dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextRegressionValidationBenchmarks.CompileTextLayoutRegressionScene*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused flattened textLength run-storage validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with 24 existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~SvgCssShapeTextLayoutTests"`
  - Passed 473.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-flattened-textlength-run-array dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=text-regression-wrapped-textlength-positioned-descendants SVG_SKIA_BENCHMARK_RUN_LABEL=current-flattened-textlength-run-array-regression dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextRegressionValidationBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with 277 existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2595, skipped 40; other test projects passed.
- Focused shared inline-size exact-run/text construction validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~SvgCssShapeTextLayoutTests"`
  - Passed 473.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=text-regression-shared-layout-engine-integration,text-regression-wrapped-textlength-positioned-descendants SVG_SKIA_BENCHMARK_RUN_LABEL=current-shared-layout-exact-runs-string-create-compile dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextRegressionValidationBenchmarks.CompileTextLayoutRegressionScene*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=text-regression-shared-layout-engine-integration,text-regression-wrapped-textlength-positioned-descendants SVG_SKIA_BENCHMARK_RUN_LABEL=current-shared-layout-exact-runs-string-create-dom-metrics dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextRegressionValidationBenchmarks.ValidateTextContentDomMetrics*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with 277 existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2595, skipped 40; other test projects passed.
- Focused wrapped inline-size exact group/text construction validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~SvgCssShapeTextLayoutTests"`
  - Passed 473.
  - Baseline: `SVG_SKIA_BENCHMARK_SCENARIOS=text-regression-wrapped-textlength-positioned-descendants SVG_SKIA_BENCHMARK_RUN_LABEL=current-before-wrapped-textlength-exact-groups dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextRegressionValidationBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - Current: `SVG_SKIA_BENCHMARK_SCENARIOS=text-regression-wrapped-textlength-positioned-descendants SVG_SKIA_BENCHMARK_RUN_LABEL=current-wrapped-textlength-exact-groups-final dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextRegressionValidationBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - Direct placement: `SVG_SKIA_BENCHMARK_SCENARIOS=text-regression-wrapped-textlength-positioned-descendants SVG_SKIA_BENCHMARK_RUN_LABEL=current-wrapped-textlength-direct-group-placements dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextRegressionValidationBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused retained text paint-clone trim validation:
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2594, skipped 40; other test projects passed.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192,generated-aligned-letter-spacing-192,generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-retained-text-paint-clone-trim-final dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
- Focused read-only codepoint split validation:
  - `dotnet format Svg.Skia.slnx --no-restore`
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests|FullyQualifiedName~SvgTextPathParityTests|FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 492.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2594, skipped 40; other test projects passed.
- Focused direct textPath retained compile fast-path validation:
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgTextPathParityTests|FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 448.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~W3CTestSuiteTests.Tests"`
  - Passed 523, skipped 3.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-direct-textpath-compile-fastpath dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2594, skipped 40; other test projects passed.
- Focused simple textPath recording guard validation:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~TextPath"`
  - Passed 73.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-textpath-next-audit dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextPathPlacementBenchmarks*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=textpath-decoration-grapheme-guards dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~TextPath|FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 453.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-textpath-offset-advance-skip dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed cleanly.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~TextPath|FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 453.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-textpath-simple-positioned-run-final dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with 277 existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2597, skipped 40; other test projects passed.
- Focused compact positioned textPath command validation:
  - `dotnet test tests/ShimSkiaSharp.UnitTests/ShimSkiaSharp.UnitTests.csproj -f net10.0 -c Release --no-build --filter "CloneCommandTests|EditingHelpersTests"`
  - Passed 34.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 265.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=compact-positioned-text-run dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2597, skipped 40; other test projects passed.
- Focused fast positioned textPath bounds validation:
  - Fresh scan: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-text-path-curves-96,generated-shapes-1024,generated-filtered-shapes-256 SVG_SKIA_BENCHMARK_RUN_LABEL=current-next-retained-hotspot-scan dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Placement audit: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=current-textpath-placement-after-compact dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextPathPlacementBenchmarks*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Current: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=textpath-fast-placement-bounds dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~TextPath|FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 288.
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with 293 existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2597, skipped 40; other test projects passed.
- Focused no-reference node-tree dependency registration validation:
  - Fresh retained hotspot scan: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-text-path-curves-96,generated-shapes-1024,generated-filtered-shapes-256 SVG_SKIA_BENCHMARK_RUN_LABEL=current-next-hotspot-after-textpath-bounds dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Baseline phase audit: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024 SVG_SKIA_BENCHMARK_RUN_LABEL=current-shapes-retained-phase-audit dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Current phase audit: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024 SVG_SKIA_BENCHMARK_RUN_LABEL=node-subtree-dependency-registration dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SKSvgRebuildFromModelTests|FullyQualifiedName~SvgAnimationControllerTests.TryApplyRetainedSceneMutationByIdAndRender"`
  - Passed 286.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgResourceRenderingParityTests|FullyQualifiedName~SKSvgRebuildFromModelTests"`
  - Passed 392.
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with 293 existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2597, skipped 40; other test projects passed.
- Focused read-only codepoint DOM-metrics validation:
  - Current: `SVG_SKIA_BENCHMARK_SCENARIOS=text-regression-positioned-layout,text-regression-vertical-rtl-layout,text-regression-vertical-rtl-shape-layout,text-regression-wrapped-textlength-positioned-descendants SVG_SKIA_BENCHMARK_RUN_LABEL=current-readonly-codepoint-dom-metrics dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextRegressionValidationBenchmarks.ValidateTextContentDomMetrics" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
  - Control: `SVG_SKIA_BENCHMARK_SCENARIOS=text-regression-positioned-layout,text-regression-vertical-rtl-layout,text-regression-vertical-rtl-shape-layout,text-regression-wrapped-textlength-positioned-descendants SVG_SKIA_BENCHMARK_RUN_LABEL=control-codepoint-dom-metrics dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextRegressionValidationBenchmarks.ValidateTextContentDomMetrics" --warmupCount 2 --minIterationCount 3 --maxIterationCount 5`
- Focused ASCII codepoint and single-run aligned compile validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with no warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 447.
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-letter-spacing-192,generated-aligned-text-length-192 SVG_SKIA_BENCHMARK_RUN_LABEL=ascii-codepoint-cache-aligned-placement dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgAlignedTextPlacementBenchmarks*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-aligned-letter-spacing-192,generated-aligned-text-length-192 SVG_SKIA_BENCHMARK_RUN_LABEL=ascii-codepoint-single-run-retained-compile dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256,generated-text-path-curves-96,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-text-192 SVG_SKIA_BENCHMARK_RUN_LABEL=ascii-codepoint-single-run-retained-hotspot-scan dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192 SVG_SKIA_BENCHMARK_RUN_LABEL=ascii-codepoint-single-run-text-phase-audit dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
- Focused text internals benchmark comparison:
  - Before: `SVG_SKIA_BENCHMARK_RUN_LABEL="current-text-internals-before-next-advance-cache" SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192,generated-text-path-curves-96,generated-aligned-letter-spacing-192,generated-aligned-text-length-192" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextCompileInternalsBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - After: `SVG_SKIA_BENCHMARK_RUN_LABEL="current-text-internals-after-natural-advance-cache" SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192,generated-text-path-curves-96,generated-aligned-letter-spacing-192,generated-aligned-text-length-192" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextCompileInternalsBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - Simple cache hit: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192,generated-text-path-curves-96,generated-aligned-letter-spacing-192,generated-aligned-text-length-192 SVG_SKIA_BENCHMARK_RUN_LABEL=current-simple-natural-advance-cache dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextCompileInternalsBenchmarks*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
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
- Focused transform-only simple shape group validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with no warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --filter "FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_FoldsSimpleTransformOnlyShapeGroups"`
  - Passed 1.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgResourceRenderingParityTests|FullyQualifiedName~SKSvgRebuildFromModelTests"`
  - Passed 393.
  - Before draw: `SVG_SKIA_BENCHMARK_RUN_LABEL=current-render-draw-hotspots-after-node-dependencies SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-flood-filters-256,generated-filtered-shapes-256,generated-inline-styles-512,generated-text-path-curves-96 dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRenderBitmapBenchmarks.DrawNativePicture1x*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - After draw: `SVG_SKIA_BENCHMARK_RUN_LABEL=transform-only-simple-shape-fold-draw SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-flood-filters-256,generated-filtered-shapes-256,generated-inline-styles-512,generated-text-path-curves-96 dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRenderBitmapBenchmarks.DrawNativePicture1x*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - Retained compile: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256,generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=transform-only-simple-shape-fold-retained-compile dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Native picture: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256,generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=transform-only-simple-shape-fold-native-picture dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModel*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with 293 existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2598, skipped 40; other test projects passed.
- Focused direct positioned text run replay validation:
  - `dotnet build src/Svg.Skia/Svg.Skia.csproj -c Release --no-restore`
  - Build passed with existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgTextPathParityTests|FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SKSvgRebuildFromModelTests"`
  - Passed 472.
  - `SVG_SKIA_BENCHMARK_RUN_LABEL=positioned-text-run-direct-matrix-draw SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96,generated-shapes-1024,generated-filtered-shapes-256,generated-aligned-letter-spacing-192,generated-aligned-text-length-192 dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRenderBitmapBenchmarks.DrawNativePicture1x*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
- Focused rotation-scale positioned text blob replay validation:
  - `dotnet build src/Svg.Skia/Svg.Skia.csproj -c Release --no-restore`
  - Build passed with existing warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgTextPathParityTests|FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SKSvgRebuildFromModelTests"`
  - Passed 472.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~WptSvg2StaticSubsetTests"`
  - Passed 46.
  - `SVG_SKIA_BENCHMARK_RUN_LABEL=positioned-run-rsxform-blob-draw SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-text-192,generated-shapes-1024,generated-filtered-shapes-256 dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRenderBitmapBenchmarks.DrawNativePicture1x*" --warmupCount 3 --minIterationCount 6 --maxIterationCount 12`
  - `SVG_SKIA_BENCHMARK_RUN_LABEL=positioned-run-rsxform-blob-native-picture-create SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-path-curves-96,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-text-192 dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModel*" --warmupCount 2 --minIterationCount 4 --maxIterationCount 8`
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with 193 existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2598, skipped 40; other test projects passed.
- Focused compact retained-node child storage validation:
  - Fresh retained hotspot scan: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-text-path-curves-96,generated-shapes-1024,generated-filtered-shapes-256 SVG_SKIA_BENCHMARK_RUN_LABEL=next-hotspot-scan-after-inline-path-list dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Current compact storage run: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256,generated-text-192,generated-aligned-letter-spacing-192 SVG_SKIA_BENCHMARK_RUN_LABEL=compact-retained-node-children-retained-compile dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore`
  - Build passed with no warnings.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests"`
  - Passed 267.
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2599, skipped 40; other test projects passed.
- Focused retained resource-key feature gate validation:
  - Baseline clean-head retained compile: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024 SVG_SKIA_BENCHMARK_RUN_LABEL=baseline-shapes-compile-clean-head dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Current retained compile: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024 SVG_SKIA_BENCHMARK_RUN_LABEL=resource-key-feature-gate-shapes-compile dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `CompileNodeTreeOnly | generated-shapes-1024`: `4.552 ms / 3.78 MB` to `4.327 ms / 3.78 MB`.
  - Compile smoke: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256,generated-text-192,generated-inline-styles-512 SVG_SKIA_BENCHMARK_RUN_LABEL=resource-key-feature-gate-compile-smoke dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Smoke results: `generated-shapes-1024` `4.363 ms / 3874.03 KB`, `generated-filtered-shapes-256` `782.3 us / 904.33 KB`, `generated-text-192` `3.824 ms / 3215.36 KB`, `generated-inline-styles-512` `741.7 us / 1069.47 KB`.
  - Focused resource-key tests: `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_AssignsRetainedResourceKeysForClipMaskAndFilter|FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_AssignsResourceKeysForAbsoluteSameDocumentReferences|FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_CssFilterComposesUrlAndFunctionChain|FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_MaskElementCanBeMaskedByAnotherMask|FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_RootSvgClipPathClipsDocumentContent"` passed 5.
  - `dotnet format Svg.Skia.slnx --no-restore`
  - Completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release`
  - Build passed with existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2599, skipped 40; other test projects passed.
- Focused on-demand retained compilation-root lookup validation:
  - Baseline phase scan: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256,generated-text-192,generated-inline-styles-512 SVG_SKIA_BENCHMARK_RUN_LABEL=next-retained-phase-scan dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Constructor benchmark: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256,generated-text-192,generated-inline-styles-512 SVG_SKIA_BENCHMARK_RUN_LABEL=on-demand-roots-scenedoc-create dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CreateSceneDocumentFromCompiledTree*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Full compile benchmark: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256,generated-text-192,generated-inline-styles-512 SVG_SKIA_BENCHMARK_RUN_LABEL=on-demand-roots-full-retained-compile dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Focused mutation tests: `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgAnimationControllerTests"` passed 351.
  - `dotnet format Svg.Skia.slnx --no-restore` completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release` passed with existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2599, skipped 40; other test projects passed.
- Focused SVG-font sequential eligibility probe validation:
  - Hotspot scan: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256,generated-flood-filters-256,generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=next-optimization-retained-hotspot-scan dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Text internals scan: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192 SVG_SKIA_BENCHMARK_RUN_LABEL=next-optimization-text-internals-scan dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextCompileInternalsBenchmarks*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Current retained compile: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-text-192,generated-aligned-letter-spacing-192,generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=skip-sequential-svgfont-probe-retained-compile dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore` passed with existing warnings.
  - Focused text/retained tests: `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests|FullyQualifiedName~WptSvg2StaticSubsetTests"` passed 538.
  - `dotnet format Svg.Skia.slnx --no-restore` completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release` passed with existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release`
  - `Svg.Skia.UnitTests`: Passed 2599, skipped 40; other test projects passed.
- Focused structural transform-origin and direct path bounds reuse validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore` passed with existing warnings.
  - Focused retained/text hit-test validation: `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgAnimationControllerTests|FullyQualifiedName~HitTestTests"` passed 372.
  - Focused transform-origin resource parity validation: `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~SvgStructureResourceParityTests.TransformOrigin_ContentBox_UsesElementGeometryReferenceBox|FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_StructuralTransformOriginAppliesToGroup|FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_TransformOriginAbsoluteFillBoxOffsetsReferenceBox|FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_UseTransformOriginUsesReferencedFillBox"` passed 4.
  - Hotspot scan: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256,generated-flood-filters-256,generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-text-path-curves-96,generated-inline-styles-512 SVG_SKIA_BENCHMARK_RUN_LABEL=next-hotspot-scan-after-svgfont-probe-skip dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Shape phase audit: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024 SVG_SKIA_BENCHMARK_RUN_LABEL=next-shape-phase-scan-after-svgfont-probe-skip dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Current retained compile: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256,generated-text-192,generated-aligned-letter-spacing-192 SVG_SKIA_BENCHMARK_RUN_LABEL=structural-transform-bounds-reuse-retained-compile dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
- Focused transformed simple-child renderer fallback validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore` passed with no warnings.
  - Focused retained/rebuild validation: `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_FoldsSimpleTransformOnlyShapeGroups|FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph|FullyQualifiedName~SKSvgRebuildFromModelTests"` passed 260.
  - Draw replay check: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256,generated-inline-styles-512,generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=transformed-simple-child-render-check dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRenderBitmapBenchmarks.DrawNativePicture1x*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Recorder replay check: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256,generated-inline-styles-512,generated-text-path-curves-96 SVG_SKIA_BENCHMARK_RUN_LABEL=transformed-simple-child-replay-check dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgNativeSkPictureBenchmarks.ReplayFullModelIntoRecorderCanvasUsingCurrentLoop*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
- Focused direct visual validity reuse validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore` passed with no warnings.
  - Focused retained/resource/hit-test validation: `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgResourceRenderingParityTests|FullyQualifiedName~HitTestTests|FullyQualifiedName~SKSvgRebuildFromModelTests"` passed 415.
  - Current retained compile: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-shapes-1024,generated-filtered-shapes-256,generated-text-192,generated-aligned-letter-spacing-192 SVG_SKIA_BENCHMARK_RUN_LABEL=direct-visual-validity-reuse-retained-compile dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Control rerun: `SVG_SKIA_BENCHMARK_SCENARIOS=generated-filtered-shapes-256,generated-aligned-letter-spacing-192,generated-text-192 SVG_SKIA_BENCHMARK_RUN_LABEL=direct-visual-validity-reuse-control-rerun dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
- Focused retained-node visual/resource/effect sidecar validation:
  - Fresh retained hotspot scan: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-text-path-curves-96,generated-shapes-1024,generated-filtered-shapes-256" SVG_SKIA_BENCHMARK_RUN_LABEL="next-after-published-retained-hotspot-scan" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Visual/resource sidecar retained compile: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024,generated-filtered-shapes-256,generated-text-192,generated-aligned-letter-spacing-192" SVG_SKIA_BENCHMARK_RUN_LABEL="lazy-node-state-retained-compile" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Effect sidecar retained compile: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024,generated-filtered-shapes-256,generated-text-192,generated-aligned-letter-spacing-192" SVG_SKIA_BENCHMARK_RUN_LABEL="lazy-node-effect-state-retained-compile" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Post-sidecar hotspot scan: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-text-path-curves-96,generated-shapes-1024,generated-filtered-shapes-256" SVG_SKIA_BENCHMARK_RUN_LABEL="post-node-sidecars-retained-hotspot-scan" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Post-sidecar text internals scan: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192" SVG_SKIA_BENCHMARK_RUN_LABEL="post-node-sidecars-text-internals-scan" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextCompileInternalsBenchmarks*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `dotnet format Svg.Skia.slnx --no-restore` completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore` passed with existing `SvgDeferredPaintServer` obsolete warnings and no errors.
  - Focused retained/resource/text validation: `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgInteractionDispatcherTests|FullyQualifiedName~SvgResourceRenderingParityTests|FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SKSvgRebuildFromModelTests"` passed 613.
- Focused prepared-text and boundary-spacing validation:
  - Fresh retained hotspot scan after the natural-codepoint cache: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-text-path-curves-96,generated-shapes-1024,generated-filtered-shapes-256" SVG_SKIA_BENCHMARK_RUN_LABEL="next-hotspot-after-codepoint-cache" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Current retained text compile: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192" SVG_SKIA_BENCHMARK_RUN_LABEL="preparetext-boundary-spacing-retained-text" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Fresh post-change hotspot scan: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-text-path-curves-96,generated-shapes-1024,generated-filtered-shapes-256" SVG_SKIA_BENCHMARK_RUN_LABEL="post-preparetext-boundary-spacing-hotspot-scan" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `CompileNodeTreeOnly | generated-text-192`: allocation moved from `2728.97 KB` to `2.56 MB`; short-run mean measured `4.738 ms`.
  - `CompileNodeTreeOnly | generated-aligned-letter-spacing-192`: allocation moved from `2682.19 KB` to `2.58 MB`; short-run mean measured `2.695 ms`.
  - `CompileNodeTreeOnly | generated-aligned-text-length-192`: allocation moved from `1858.61 KB` to `1.78 MB`; short-run mean measured `2.256 ms`.
  - Latest retained hotspot scan after the text guards measured `generated-aligned-letter-spacing-192` at `2640.1 KB`, `generated-shapes-1024` at `2632.5 KB`, `generated-text-192` at `2623.93 KB`, `generated-aligned-text-length-192` at `1818.82 KB`, `generated-text-path-curves-96` at `1213.6 KB`, and `generated-filtered-shapes-256` at `631.94 KB`.
  - Timing remains short-run/noisy, so these rows are kept as allocation regression evidence for the no-op prepared-text and boundary-spacing guards.
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore` passed with no warnings.
  - Focused text/retained tests: `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests|FullyQualifiedName~WptSvg2StaticSubsetTests"` passed 538.
- Focused unchanged structural transform refresh validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore` passed with no warnings.
  - Focused retained/text hit-test validation: `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~HitTestTests|FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgTextSelectionDomTests"` passed 492.
  - Current retained shape compile: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024" SVG_SKIA_BENCHMARK_RUN_LABEL="structural-transform-refresh-guard-shapes" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `CompileNodeTreeOnly | generated-shapes-1024` measured `3.085 ms / 2.57 MB`, versus the prior focused shape phase row of `3.230 ms / 2,695,680 B`; allocation stayed flat, as expected, and the guard trims redundant traversal work.
  - Fresh retained hotspot scan: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-text-path-curves-96,generated-shapes-1024,generated-filtered-shapes-256" SVG_SKIA_BENCHMARK_RUN_LABEL="post-transform-refresh-guard-hotspot-scan" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Latest retained hotspot scan measured `generated-aligned-letter-spacing-192` at `2640.08 KB`, `generated-shapes-1024` at `2632.5 KB`, `generated-text-192` at `2623.93 KB`, `generated-aligned-text-length-192` at `1818.82 KB`, `generated-text-path-curves-96` at `1213.6 KB`, and `generated-filtered-shapes-256` at `631.94 KB`.
- Focused resolved sequential fill-only validation:
  - Current hotspot scan before this change: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192,generated-shapes-1024,generated-filtered-shapes-256" SVG_SKIA_BENCHMARK_RUN_LABEL="next-optimization-current-hotspot-scan" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - The pre-change scan measured `CompileNodeTreeOnly | generated-text-192` at `9.101 ms / 2624.44 KB`, with `generated-aligned-letter-spacing-192` at `2640.12 KB` and `generated-shapes-1024` at `2632.5 KB`.
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore` passed with 18 existing `SvgDeferredPaintServer` obsolete warnings and no errors.
  - Focused text/retained validation: `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgTextSelectionDomTests|FullyQualifiedName~HitTestTests"` passed 492.
  - Current retained text compile: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192" SVG_SKIA_BENCHMARK_RUN_LABEL="resolved-sequential-fill-only-retained-text" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `CompileNodeTreeOnly | generated-text-192` measured `5.297 ms / 2.49 MB`; timing remains short-run evidence, while allocation moved below the previous `2624.44 KB` row by avoiding the generic paint-order path for fill-only resolved sequential runs.
- Focused runtime payload paint reuse and compact opacity-state validation:
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore` passed with no warnings.
  - Focused retained/resource/hit-test validation: `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgResourceRenderingParityTests|FullyQualifiedName~HitTestTests|FullyQualifiedName~SKSvgRebuildFromModelTests"` passed 415.
  - Baseline shape phase scan: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024" SVG_SKIA_BENCHMARK_RUN_LABEL="next-shape-phase-scan-post-fill-only" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Baseline rows: `ResolveRuntimePayloadsOnly` `5.506 ms / 363064 B`, `CreateSceneDocumentFromCompiledTree` `4.398 ms / 782748 B`, `CompileViaSceneCompiler` `16.690 ms / 3839219 B`, `CompileViaSceneRuntime` `18.887 ms / 3839769 B`, and `CompileNodeTreeOnly` `8.003 ms / 2695720 B`.
  - Current shape phase scan: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024" SVG_SKIA_BENCHMARK_RUN_LABEL="split-opacity-payload-shape-phase" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Current rows: `ResolveRuntimePayloadsOnly` `807.327 us / 116304 B`, `CreateSceneDocumentFromCompiledTree` `1.081 ms / 634236 B`, `CompileViaSceneCompiler` `6.289 ms / 3420428 B`, `CompileViaSceneRuntime` `6.803 ms / 3421054 B`, and `CompileNodeTreeOnly` `3.166 ms / 2720267 B`.
  - Timing remains short-run evidence, while the main allocation win is from reusing already-created local paints and avoiding the large effect sidecar for opacity-only retained leaf nodes.
- Focused empty document-font scope validation:
  - `dotnet build src/Svg.Skia/Svg.Skia.csproj -c Release --no-restore` passed with existing warnings.
  - Focused retained/font/source validation: `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~SvgRetainedSceneGraphTests|FullyQualifiedName~SvgFont|FullyQualifiedName~SKSvgSettingsTests|FullyQualifiedName~SKSvgTests"` passed 369, `dotnet test tests/Svg.Controls.Skia.Avalonia.UnitTests/Svg.Controls.Skia.Avalonia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~SvgSourceTests"` passed 19, and `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~SkiaSvgAssetLoaderCachingTests"` passed 18.
  - Current retained compile: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024,generated-filtered-shapes-256,generated-text-192,generated-text-path-curves-96" SVG_SKIA_BENCHMARK_RUN_LABEL="empty-document-font-scope-retained-compile" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Short-run allocation rows measured `generated-filtered-shapes-256` at `637.27 KB`, `generated-shapes-1024` at `2655.82 KB`, `generated-text-192` at `2552.81 KB`, and `generated-text-path-curves-96` at `1235.85 KB`; timing remained noisy, so this is kept as a small no-font setup allocation trim and behavior guard.
- Focused inline small `AddPoly` point storage validation:
  - `dotnet build src/ShimSkiaSharp/ShimSkiaSharp.csproj -c Release --no-restore` passed with no warnings.
  - `dotnet build src/Svg.SceneGraph/Svg.SceneGraph.csproj -c Release --no-restore` passed with existing `SvgDeferredPaintServer` obsolete warnings and no errors.
  - Focused shim/model/retained validation passed: `ShimSkiaSharp.UnitTests` path/clone filters passed 45, `Svg.Model.UnitTests` `PathingServiceTests` passed 11, and `Svg.Skia.UnitTests` retained/rebuild/resource/hit-test filters passed 415.
  - Path conversion benchmark: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024" SVG_SKIA_BENCHMARK_RUN_LABEL="inline-small-poly-path-conversion" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgPathConversionBenchmarks*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Current path conversion rows measured `ConvertPrimitiveShapesOnly` at `530.8 us / 104 KB`, `ConvertSvgPathsOnly` at `979.5 us / 664 KB`, and `ConvertAllVisualPaths` at `2.443 ms / 768.01 KB`; compared with the prior focused path-conversion scan, SVG-path and all-visual path allocations moved from `680 KB` to `664 KB` and from `784.01 KB` to `768.01 KB`.
  - Retained compile benchmark: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-shapes-1024" SVG_SKIA_BENCHMARK_RUN_LABEL="inline-small-poly-retained-compile" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - `CompileNodeTreeOnly | generated-shapes-1024` measured `7.024 ms / 2.58 MB`; timing remains short-run/noisy, while allocation is slightly below the previous `2655.82 KB` generated-shapes row.
- Focused split-codepoint cache-hit fast-path validation:
  - Focused retained text tests: `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests"` passed 267.
  - Focused text compiler/path/paint-order tests: `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgSceneTextCompilerTests|FullyQualifiedName~SvgTextPathParityTests|FullyQualifiedName~SvgTextPaintOrderTests"` passed 188.
  - Text internals benchmark: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192" SVG_SKIA_BENCHMARK_RUN_LABEL="skip-split-cache-cwt-hit" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextCompileInternalsBenchmarks*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
  - Retained text benchmark: `SVG_SKIA_BENCHMARK_SCENARIOS="generated-text-192,generated-aligned-letter-spacing-192,generated-aligned-text-length-192" SVG_SKIA_BENCHMARK_RUN_LABEL="skip-split-cache-cwt-hit-retained-text" dotnet run -c Release -f net10.0 --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly*" --warmupCount 1 --minIterationCount 2 --maxIterationCount 3`
- Pre-publish validation for the current stack:
  - `dotnet format Svg.Skia.slnx --no-restore` completed; formatter-only `externals/SVG` submodule changes were restored.
  - `dotnet build Svg.Skia.slnx -c Release` passed with 297 existing warnings.
  - `dotnet test Svg.Skia.slnx -c Release` passed; `Svg.Skia.UnitTests` reported 2599 passed and 40 skipped, and the other test projects passed.

The release build currently reports 297 existing warnings only, including package vulnerability warnings and existing nullable/obsolete API warnings. No build errors were reported.
