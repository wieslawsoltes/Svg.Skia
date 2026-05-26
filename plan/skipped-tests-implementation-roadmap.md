# Skipped Tests Implementation Roadmap

## Scope

This document tracks the remaining skipped test surface in `Svg.Skia` and the implementation order required to remove those skips with real renderer/runtime support instead of threshold inflation or baseline workarounds.

Current skipped test concentration:

- `tests/Svg.Skia.UnitTests/W3CTestSuiteTests.cs`
  - 217 skipped rows
  - largest groups: `animate` 78, `struct` 30, `interact` 24, `text` 24, `styling` 18, `types` 15
- `tests/Svg.Skia.UnitTests/resvgTests.cs`
  - 481 skipped rows
  - largest groups: `a-font` 50, `a-text` 37, `a-writing` 23, `a-baseline` 22, `a-filter` 42
  - largest element groups: `e-text` 40, `e-textPath` 40, `e-tspan` 30, `e-image` 31, `e-marker` 20, `e-pattern` 12

## Principles

- Prefer renderer and parser fixes over test-specific workarounds.
- Keep Chrome captures as the source of truth for W3C rows that already use Chrome overrides.
- Do not fake browser-only behavior. Rows that require DOM mutation, JavaScript execution, selection APIs, or event dispatch should only be enabled after the corresponding runtime exists.
- If upstream SVG submodule changes would be required, implement the compatibility layer in `src/Svg.Custom` instead.

## Current State

Completed in the current text tranche:

- whitespace preservation for text parents via `src/Svg.Custom/Compatibility/SvgDocumentCompatibilityLoader.cs`
- `tref` opt-in control through `ISvgTextReferenceRenderingOptions`
- Chrome-backed enablement for the first W3C text block (`text-align-*`, `text-fonts-*`, `text-path-*`, `text-tref-*`, `text-tspan-*`, `text-ws-*`, selected `text-intro-*`, selected `text-text-*`)
- underline and line-through drawing
- positioned glyph rotation handling
- `textPath` arc sampling and `%` `startOffset` correction
- `tref` content extraction now uses referenced text data with the `tref` element's own style and positioning instead of replaying the referenced subtree as-is
- resvg rows enabled from probe + xUnit verification:
  - `e-text-001/002/003/004/005/009/015/018/019/020/021/022/023/025/026/031/039/040/041/042`
  - `e-textPath-003/006/012/017/018/023/042/043/044`
  - `e-tref-001/002/003/006/007/008/009/010/011`
  - `e-tspan-001/002/007/008/009/010/011/012/014/015/016/018/020/021/022/026/031`
  - `a-letter-spacing-002/003/006/007`
  - `a-word-spacing-005`
- root text positioning fixes landed in `SvgSceneTextCompiler`:
  - the sequential text fast path now honors a root text element's initial `dx`/`dy`
  - relative-only multi-value `dx`/`dy` runs now use the positioned-glyph path instead of falling back to contiguous text
  - retained-scene regressions now cover both the root `dx`/`dy` origin case and multi-value relative glyph origins
- Chrome validation confirmed two resvg `tref` reference mismatches that must remain skipped unless the suite changes baseline policy:
  - `e-tref-004`: Chrome omits the external-document `tref` content while the resvg PNG expects it
  - `e-tref-005`: Chrome omits nested `tref` chaining content while the resvg PNG expects it
- `textPath` current-position handling is now split into two renderer rules:
  - inherited current `x` seeds path distance for the initial chunk
  - a parent `dy` list entry is consumed only by the initial `textPath` chunk instead of being reapplied to every sibling `textPath`
- This was enough to enable `e-textPath-012` and `e-textPath-023` against checked Chrome captures, but not enough to close `e-textPath-035`
- Guarded inline-size textPath wrapping now supports root `textLength` distributed across mixed normal-text/textPath siblings for retained output and text DOM metrics. Browser-grade vertical, multiline, and tiny-coordinate textPath parity remains separate.
- Fresh Chrome captures on 2026-04-10 confirmed another safe enablement batch:
  - `e-text-031`, `e-text-042`, and `e-textPath-003` are now browser-aligned without special thresholds
  - `a-letter-spacing-002/003/006/007` and `a-word-spacing-005` are browser-aligned but still need narrow Chrome-backed thresholds for raster-level differences
  - `e-tref-009/010` and `e-textPath-025` are now enabled after renderer fixes:
    inline `tref` content is omitted when mixed with surrounding sibling text, and a missing `textPath` geometry now aborts the remaining sibling text in that container like Chrome
  - `e-textPath-040` is now enabled after removing blanket filter suppression from retained text local-model compilation and adding nested `textPath` filter application for child runs
  - `e-tspan-026` is now enabled after two bidi/shaping fixes:
    mixed-span shaped runs now preserve visual-order segment boundaries instead of collapsing back to one segment per logical span, and trailing neutrals at the end of an LTR paragraph now resolve to the paragraph base direction instead of being forced into the preceding RTL run

Still open inside the text tranche:

- Latest upstream resvg text fixtures are enabled and passing. Remaining resvg text work is no longer an enabled-failure list; it is parity hardening around Chrome/browser behavior and policy rows.
- Full browser-grade Unicode Bidi/CSS Text: nested `unicode-bidi`, isolates, overrides, `plaintext`, all weak/neutral edge cases, generated Unicode tables, UAX #9/#14/#29 conformance ingestion, CSS white-space trimming/hanging, and dictionary or orthographic segmentation providers.
- Browser-grade line breaking: generated line-break tables, CSS `line-break`/`word-break`/`overflow-wrap` tailoring, Thai/Lao/Khmer/Myanmar dictionary segmentation, Brahmic syllable boundaries, and evidence-backed browser fixtures.
- Complete vertical and RTL wrapping: vertical mixed-direction wrapping, vertical/RTL wrapped `textLength`, vertical overflow marker placement, and exact vertical DOM metrics.
- Positioned descendants plus `textLength` inside wrapping: remove remaining guarded fallbacks across vertical, RTL, shape, textPath, and complex shaped-cluster combinations.
- Full textPath-in-wrapping and `method="stretch"` parity: nested/mixed/multiline/vertical textPath layout, transformed referenced paths, exact tiny-coordinate/current-position parity, and browser-raster parity for fallback fonts, color fonts, emoji ZWJ clusters, and complex scripts.
- Complete CSS Shapes text semantics: exact shape-box and image semantics, arbitrary-path shape margin/padding contour offsets, floats where applicable, multiple same-line fragments, holes/fill rules, and browser line-fragment parity.
- Font, baseline, and `altGlyph` fidelity: mixed-script baseline tables, vertical metrics, browser-like font fallback/OpenType features, and exact W3C/browser raster identity for legacy SVG-font `altGlyph` rows.
- Text DOM selection and graphics effects: `selectSubString` now records immutable logical selection ranges with layout-backed extents for JavaScript hosts and can render a retained static highlight, but exact browser UI selection behavior, visual-order bidi selection painting, focus/caret policy, text paint/filter/mask/clip/decorations under all shaped and textPath paths, and exact graphics-effect raster parity remain host/runtime and rendering work.
- Every text-lane change must run focused text tests plus `SvgTextRegressionValidationBenchmarks`; cross-area text changes must also run `SvgAllAreaRegressionValidationBenchmarks` to catch non-text performance regressions.
- 2026-05-24 text pass:
  - `a-letter-spacing-007` now keeps pure Arabic/cursive runs out of scalar tracking and preserves shaping while ignoring cursive tracking.
  - `a-letter-spacing-008` now applies inter-run tracking across inline `tspan` boundaries, including the trailing spacing after the child run before following text.
  - `a-letter-spacing-009` now uses a narrow mixed-script path: the Latin prefix is tracked, the Arabic suffix is shaped as a run, and trailing neutral punctuation is kept visually after the RTL word. The remaining enabled threshold is a small Chrome-raster delta.
  - `a-letter-spacing-005` now uses a Chrome-aligned percentage spacing basis and remains enabled with a scoped raster/metric threshold.
  - `e-tref-004/005` now match resvg semantics: external `tref` and nested `tref` content are suppressed while direct same-document `tref` remains enabled.
  - Browser-compatible fallback routing is now limited to explicit RTL/unicode-bidi contexts or joining-script tracking runs, so Indic and other non-joining scripts avoid the shaped fallback path while Arabic/Syriac/NKo/Mongolian-style cursive runs keep shaping-safe letter-spacing behavior.
  - Final validation for this pass covered focused resvg text rows, the full resvg fixture matrix, W3C text/retained regression rows, full solution tests, `SvgTextRegressionValidationBenchmarks`, and `SvgAllAreaRegressionValidationBenchmarks`.
- 2026-05-24 text review hardening pass:
  - Reviewed the text lane around public text selection APIs, JavaScript text DOM metrics, shared wrapped-layout DOM metric reuse, and CSS Shapes image sampling.
  - `SKSvg.SvgTextSelectionRange.Extents` now snapshots incoming extents and exposes an immutable read-only list, so callers cannot mutate recorded selection geometry through retained array references.
  - `getSubStringLength` and `selectSubString` now clamp very large `nchars` values with long arithmetic before converting back to character indices, preventing integer overflow while preserving browser-style clamping to available text.
  - Shared layout DOM metrics keep the existing cluster-owned substring behavior for continuation characters while also letting large clamped counts include later owned clusters.
  - PNG alpha image shape sampling now rejects overflowing image dimensions before allocating buffers or computing scanline indexes.
  - Validation for this hardening pass covered focused text selection/CSS shape tests, full `SvgSceneTextCompilerTests`, focused text feature rows, `SvgTextRegressionValidationBenchmarks` with 72 executed benchmark cases, and `SvgAllAreaRegressionValidationBenchmarks` with 6 executed benchmark cases.
- 2026-05-24 legacy text parity pass:
  - `SKSvgSettings.EnableTextSelectionRendering` and `TextSelectionColor` now control retained static selection highlight painting for JavaScript `selectSubString`; post-load selection calls refresh the retained picture so event-driven selection is visible.
  - Empty-content `altGlyph` now resolves referenced SVG font glyphs instead of being skipped before SVG-font lookup. The resolver covers direct glyph references, `glyphRef`, `altGlyphDef`, and `altGlyphItem` sequences when they resolve to one SVG font entry.
  - W3C `text-altglyph-01/02/03-b` rows are enabled with scoped raster thresholds and an ignored draft-banner strip for `text-altglyph-03-b`; remaining deltas are browser/font raster identity, not missing substitution.
  - W3C `text-tselect-01/02/03` remain explicitly skipped because the legacy fixtures assert browser visual-order selection UI behavior beyond static logical selection highlighting.

Verified probe findings from the remaining skipped W3C text rows on 2026-04-09:

- `text-align-05-b`, `text-align-06-b`, and `text-intro-03-b` do not enter any vertical placement branch in the browser-compatible text path today. The actual render stays horizontal, so these rows need a real vertical advance model, not threshold tuning.
- `text-align-08-b` is close in glyph selection but still lacks mixed-script dominant-baseline table handling across Latin, ideographic, and Devanagari glyphs.
- `text-intro-02-b` and `text-intro-09-b` still fail because mixed-direction Hebrew/Latin rows need browser-parity bidi reordering across fallback font spans. Wrapping each fallback span with bidi controls is insufficient.
- `text-intro-05-t` and `text-intro-10-f` still fail because Arabic shaping only stays correct when fallback spans are preserved, but preserving spans still leaves non-Chrome anchor/position parity. A probe to force single-run shaping produced tofu glyphs, which confirms the missing piece is mixed-font shaping/fallback support rather than a simple bidi wrapper.
- `text-altglyph-01/02/03-b` now exercise real SVG-font alternate glyph substitution. They remain sensitive to browser/font raster identity and should not be conflated with the remaining bidi/vertical text layout work.

Current resource-rendering tranche:

- The next recommended non-text lane is a first resource-rendering parity slice, not a full resource-policy or browser-style completion pass.
- The slice covers gradient and pattern inheritance where explicitly authored attributes on referenced resources must override defaults without letting default values mask inherited values.
- The slice includes recursion guards for pattern picture compilation and referenced content so paint servers, markers, clips, masks, filters, and `use` expansions do not re-enter the same resource path indefinitely.
- The slice hardens existing Skia-backed filters by treating invalid `feColorMatrix` values, negative blur/morphology inputs, fractional morphology radii, and lighting parameter mapping as deterministic renderer behavior rather than fixture-specific failures.
- The slice covers clip/use placement where referenced clip content should preserve geometry and transforms while suppressing marker output that does not belong inside the clip resource.
- The slice covers image decode guards so empty, blocked, or malformed image data produces deterministic zero-size image metadata instead of null-image crashes.
- The green resvg resource fixture slice is tracked through already-green deterministic resource-adjacent families with no new thresholds or Chrome overrides. Renderer deltas outside that slice are covered by focused unit tests until their broader visual rows are ready.
- Deeper rows remain planned and must not be hidden by broad thresholds: `enable-background`, `BackgroundImage`, `BackgroundAlpha`, `feImage`, CSS `filter` functions, style selector/color parity, full mask self-reference behavior, and exact browser parity for pattern tiling/inheritance and marker placement.

## Workstreams

### 1. Text Layout And Font Fidelity

Target projects:

- `src/Svg.SceneGraph`
- `src/Svg.Model`
- `src/Svg.Skia`
- `src/Svg.Custom`

Features:

- `letter-spacing`
- `word-spacing`
- `textLength`
- `lengthAdjust=spacing|spacingAndGlyphs`
- nested `tspan` rotate inheritance
- vertical `writing-mode`
- `glyph-orientation-vertical`
- mixed-direction `unicode-bidi`
- dominant/alignment baseline handling
- `altGlyph`
- webfont and SVG font fallback parity, including mixed-script and Arabic runs

Primary test impact:

- W3C `text-*`
- resvg `a-font-*`, `a-text-*`, `a-writing-*`, `a-baseline-*`, `e-text-*`, `e-textPath-*`, `e-tspan-*`, `e-tref-*`, plus scoped threshold review for enabled `a-textLength-*`

Execution order:

1. Unicode Bidi/CSS Text conformance and generated data
2. Browser-grade line breaking and boundary providers
3. Vertical/RTL wrapping and DOM metrics
4. Positioned descendants plus `textLength` inside wrapping
5. TextPath-in-wrapping and `method="stretch"` raster parity
6. CSS Shapes text layout semantics
7. Font, baseline, webfont fallback, OpenType, and `altGlyph`
8. Text DOM selection, graphics-effect parity, and text/all-area benchmark validation

Current implementation status:

- The eight text lanes have active shared-engine implementation coverage and focused regression tests. The text implementation now routes guarded bidi, line breaking, vertical/RTL wrapping, shape-inside/shape-subtract, textLength, positioned descendants, direct and mixed textPath wrapping, cluster-aware stretch smoke cases, baseline/font fallback, retained SVG-font altGlyph substitution, and JavaScript `selectSubString` extents/static highlights through the shared layout and DOM metric paths.
- Remaining text skips must stay explicit when they depend on browser-only runtime behavior or exact browser raster identity that the static renderer does not currently promise, including policy-skipped JavaScript visual selection fixtures and exact SVG font altGlyph/browser fixture raster parity.
- Text-lane changes require both the focused text regression benchmarks and the combined all-area benchmark before acceptance so text fixes do not hide cross-area performance regressions.

Acceptance criteria:

- Remaining text skips have explicit unsupported-runtime reasons or are enabled and green.
- W3C Chrome-backed rows use refreshed Chrome captures where needed.
- Latest upstream resvg text rows remain enabled and green.
- `SvgTextRegressionValidationBenchmarks` and `SvgAllAreaRegressionValidationBenchmarks` are run and reviewed for performance regressions before accepting text-lane changes.

### 2. SMIL Snapshot Rendering

Target projects:

- `src/Svg.Animation`
- `src/Svg.Custom`
- `src/Svg.Skia`
- `tests/Svg.Skia.UnitTests`
- `scripts/capture_w3c_chrome_overrides.mjs`

Features:

- per-fixture animation snapshot times in W3C tests
- matching Chrome capture timing
- repeat-count and `repeat(n)` eventbase timing for seeked snapshots
- half-open active interval boundaries, restart truncation, repeat callbacks, and future syncbase DOM query timing
- number-list and path interpolation for paced and explicit animation values
- discrete midpoint fallback when interpolation is unsupported or non-additive
- routing for custom attributes, namespaced referenced resources, `currentColor`/`inherit`, and style-backed animated values
- by-only `animateTransform` handling
- motion and transform composition across `animateMotion`, `animateTransform`, and base transforms
- correct snapshot rendering for `animate`, `set`, `animateTransform`, `animateMotion`, `animateColor`, style, reference, and filter animation cases

Primary test impact:

- W3C `animate-*`
- `filters-composite-05-f`

Execution order:

1. Lock W3C animation rows to explicit seek times and regenerate matching Chrome overrides through the HTTP capture script, not `file://`.
2. Preserve eventbase timing semantics for `begin`, `end`, repeat events, `repeat(n)`, and self-synchronizing recurrence without letting cycles drift across snapshot seeks.
3. Finish interpolation coverage for scalar values, number lists, transform lists, path data, and paced motion where the static runtime can compute a deterministic value.
4. Route animated values through the same custom-attribute, referenced-resource, presentation-attribute, and inline-style paths used by static rendering so snapshots do not bypass cascade/resource semantics.
5. Preserve discrete midpoint fallback for unsupported interpolation modes, mismatched value shapes, non-additive values, and browser-runtime-only cases.
6. Complete by-only transform handling and motion/transform composition so base transforms, additive transforms, and motion transforms apply in browser order.
7. Keep JavaScript/DOM/event-loop-dependent rows skipped with explicit runtime reasons instead of manufacturing baselines.
8. Run focused W3C animation rows before broad W3C and all-area validation, then review benchmark output for timing-path or interpolation regressions.

Current implementation status:

- The active SMIL snapshot work is in the runtime path rather than in baseline policy. The lane now covers repeat-event timing, `repeat(n)` eventbase resolution, self-sync recurrence, future syncbase start-time queries, restart truncation, half-open interval boundaries, list/path interpolation, fallback discreteness, and composed motion/transform output as renderer behavior.
- W3C seek times and Chrome capture alignment are part of the implementation contract: a row should compare Svg.Skia at the same snapshot time used for its Chrome override, and fixture captures must keep the parent harness and SVG on the same HTTP origin.
- Style, custom attribute, namespaced `href`/`xlink:href`, `currentColor`, `inherit`, and referenced-resource animation routing remains part of the lane because snapshot correctness depends on animated values reaching the same property/resource resolution path as static values.
- Validation is expected to include focused animation rows, any refreshed Chrome overrides, the broader W3C animation suite, and performance review for timing and interpolation changes before enabling more rows. Newly enabled W3C rows with Chrome overrides include `animate-elem-02-t` through `animate-elem-15-t`, `animate-elem-17-t`, `animate-elem-19-t`, `animate-elem-22-b`, `animate-elem-24-t` through `animate-elem-28-t`, `animate-elem-30-t` through `animate-elem-41-t`, `animate-elem-44-t`, `animate-elem-46-t`, `animate-elem-53-t`, `animate-elem-64-t` through `animate-elem-70-t`, `animate-elem-77-t`, `animate-elem-78-t`, `animate-elem-80-t` through `animate-elem-83-t`, `animate-elem-86-t` through `animate-elem-89-t`, `animate-elem-92-t`, `animate-pservers-grad-01-b`, and `filters-composite-05-f`.
- The latest SMIL fixes include order-independent `animateMotion` plus `animateTransform` composition, cloned-document deferred paint-server rebinding for animated gradient resources, discrete accumulated end-value semantics, finite `max` constraints on indefinite set intervals, valid `min`/`max` pair handling, to-only non-interpolable value routing for class/reference/filter/unit attributes, and hardened Chrome override capture readiness so animation snapshots do not accidentally capture `about:blank` or a pre-seek frame.
- Remaining skipped `animate-*` rows are split between browser-runtime policy cases and deeper static parity cases. Attribute-routing classification now leaves `animate-elem-23-t`, `animate-elem-84-t`, and `animate-elem-85-t` skipped because modern Chrome captures deprecated `animateColor` as a no-op in those rows, `animate-elem-90-b` because animated class routing changes the target state but selector recalc still disturbs the static guide fills, and `animate-elem-91-t` because to-only non-interpolable routing is covered while full display/use/resource rendering parity remains incomplete. Browser-runtime rows such as click-driven `indefinite`, access key, wallclock, pointer-event, and embedded animated image fixtures should stay skipped unless the harness explicitly simulates those runtime inputs.

Acceptance criteria:

- W3C animation rows no longer default to time zero when the Chrome baseline captures an advanced frame.
- `repeat(n)` eventbase timing and self-sync recurrence produce deterministic snapshot values at the configured seek time.
- Half-open active intervals, `restart="always"` truncation, repeat timeline callbacks, and future syncbase start-time lookups are covered by focused regression tests.
- Scalar, number-list, path, transform, and motion interpolation either match browser snapshot behavior or fall back discretely at the midpoint with an explicit unsupported-runtime reason.
- Custom attributes, namespaced referenced resources, presentation attributes, `currentColor`/`inherit`, and inline styles receive animated values through the same routing used by static rendering.
- By-only transform animation and motion/transform composition preserve browser transform order for static snapshots.
- New or refreshed W3C Chrome overrides are generated with `node scripts/capture_w3c_chrome_overrides.mjs` and stay aligned with the W3C seek times.
- Focused W3C animation validation and relevant all-area benchmark checks are reviewed before accepting runtime changes.

### 3. CSS And Styling Fidelity

Target projects:

- `src/Svg.Custom`
- `src/Svg.SceneGraph`
- `src/Svg.Model`

Features:

- class selector behavior
- inline style vs presentation attribute precedence
- inheritance edge cases
- `use` instance-tree styling semantics where feasible without full DOM runtime

Primary test impact:

- W3C `styling-*`
- W3C `struct-use-10-f`, `struct-use-11-f`
- resvg attribute/style buckets

Acceptance criteria:

- Styling rows that only depend on static cascade semantics are enabled.

### 4. Resource Rendering Parity First Slice

Target projects:

- `src/Svg.SceneGraph`
- `src/Svg.Model`
- `src/Svg.Skia`
- `src/Svg.Custom`

Features:

- gradient inheritance across `href` chains:
  - `spreadMethod`
  - `gradientUnits`
  - `gradientTransform`
  - radial focal point and radius guards
- pattern inheritance and recursion guards:
  - `patternUnits`
  - `patternContentUnits`
  - `patternTransform`
  - `viewBox`
  - `preserveAspectRatio`
  - paint-server fallback when recursive pattern rendering is suppressed
- filter hardening for already-supported primitives:
  - invalid or empty `feColorMatrix` values
  - negative `feGaussianBlur` values
  - negative and fractional `feMorphology` radii
  - lighting filter parameter mapping
- clip/use placement:
  - referenced clip geometry keeps placement and transforms
  - marker rendering is suppressed inside clip-resource compilation
  - referenced content avoids marker/pattern recursion through nested resource paths
- image decode guards:
  - empty data
  - undecodable data
  - blocked or missing resource data that reaches image decode
- focused unit coverage for implemented resource deltas that are not yet part of the green resvg visual slice

Primary test impact:

- resvg resource rendering fixture slice:
  - `tests/filters/feComponentTransfer/*`
  - `tests/filters/feDisplacementMap/*`
  - `tests/filters/feDistantLight/*`
  - `tests/filters/feTurbulence/*`
  - `tests/masking/clip-rule/*`
  - `tests/paint-servers/stop-color/*`
  - `tests/painting/color/*`
  - `tests/painting/fill-rule/*`
  - `tests/painting/image-rendering/*`
  - `tests/painting/isolation/*`
  - `tests/painting/marker/*`
  - `tests/painting/mix-blend-mode/*`
  - `tests/painting/paint-order/*`
  - `tests/painting/shape-rendering/*`
  - `tests/painting/stroke*`
  - `tests/painting/visibility/*`
  - `tests/shapes/{circle,line,polygon,polyline,rect}/*`
  - `tests/structure/{a,defs,g,transform,use}/*`
- focused model/Skia unit tests:
  - gradient explicit default inheritance
  - radial focal projection
  - pattern `preserveAspectRatio` explicit default inheritance
  - non-invertible pattern transform rejection
  - CSS `clip: rect(...)` parsing
  - invalid image decode guard
  - spot-lit specular code generation argument mapping
- future deeper resource rows:
  - resvg `e-image-*`, `e-marker-*`, `e-pattern-*`, `e-linearGradient-*`, `e-radialGradient-*`, `e-mask-*`, `e-filter-*`, `e-fe*` rows that require browser-only behavior or unsupported primitives

Execution order:

1. Lock the green resvg resource fixture slice as the first non-text resource harness.
2. Preserve gradient inheritance fixes and radial guard behavior for the paint-server rows.
3. Preserve pattern inheritance, transform invertibility checks, and pattern recursion guards.
4. Preserve filter hardening before adding the filter graph IR.
5. Preserve clip/use placement behavior so clip resources do not inherit marker output.
6. Preserve image decode guards before broadening external resource policy work.
7. Keep this slice free of new thresholds; add thresholds only in later visual-parity passes after row-specific review.
8. Start deeper resource rows only after this slice stays green in focused and full resvg runs.

Acceptance criteria:

- The resource rendering fixture slice is green without baseline swapping.
- No new thresholds or Chrome baseline swaps are required for this first slice.
- The implementation does not edit source fixtures or manufacture baselines for unsupported resource behavior.
- Deeper rows remain explicitly planned for `enable-background`, `BackgroundImage`, `BackgroundAlpha`, `feImage`, CSS `filter` functions, style selector/color parity, full mask self-reference behavior, and exact browser parity for pattern tiling/inheritance and marker placement.
- Resvg non-text skip count drops materially only after renderer fixes, not by baseline swapping.

### 5. SVG DOM / Script / Interaction Runtime

Target projects:

- new runtime surface, likely centered around `src/Svg.Custom`, `src/Svg.Model`, and test harness integration

Features:

- DOM objects and live lists
- script execution
- event dispatch
- selection APIs
- mutation-driven rerendering
- interactive pointer and zoom behavior

Primary test impact:

- W3C `coords-dom-*`, `text-dom-*`, `types-dom-*`, `struct-dom-*`, `struct-svg-*`, `script-*`, `interact-*`, `text-tselect-*`

Acceptance criteria:

- These rows remain skipped until the runtime exists.
- Once started, this should be tracked as a dedicated milestone because it is not a text-rendering-only task.

## Immediate Implementation Order

The next implementation tranche should be:

1. Keep the resvg resource rendering fixture slice green before broadening non-text enablement.
   Scope: paint servers, masking, marker resources, and deterministic Skia-backed filter families.
   Acceptance: the focused resource fixture harness passes without new thresholds or baseline swaps.
2. Stabilize gradient inheritance across referenced linear/radial gradients.
   Scope: explicit `spreadMethod`, `gradientUnits`, and `gradientTransform` inheritance, negative radial radius handling, non-negative focal radius handling, and focal point projection into the outer circle.
   Acceptance: paint-server gradient rows stay green without fallback baseline changes.
3. Stabilize pattern inheritance and recursion guards.
   Scope: explicit pattern attribute inheritance, non-invertible transform rejection, nested pattern picture compilation with an active-pattern stack, and fallback paint-server behavior when recursive pattern rendering is suppressed.
   Acceptance: pattern rows in the resource fixture slice stay green and recursive pattern cases fail deterministically.
4. Harden the existing Skia-backed filter primitive path.
   Scope: invalid `feColorMatrix` values, negative `feGaussianBlur`, negative/fractional `feMorphology`, and lighting argument mapping.
   Acceptance: Skia-backed filter rows in the resource fixture slice stay green; deeper filter graph work is not required for this slice.
5. Preserve clip/use placement behavior.
   Scope: clip resources keep referenced geometry placement and transforms while suppressing markers and avoiding marker/pattern recursion through referenced content.
   Acceptance: clip/masking resource rows stay green without reintroducing marker output into clip resources.
6. Preserve image decode guards.
   Scope: empty, missing, blocked, or undecodable image data returns deterministic zero-size image metadata instead of crashing.
   Acceptance: image-backed resource rows either render, skip for explicit unsupported behavior, or fail deterministically without null-image crashes.
7. Keep deeper resource rows explicit.
   Scope: `enable-background`, `BackgroundImage`, `BackgroundAlpha`, `feImage`, CSS `filter` functions, style selector/color parity, full mask self-reference, and exact pattern/marker browser parity.
   Acceptance: these rows remain skipped/planned with accurate reasons until their actual renderer/runtime support exists.
8. Run broader resvg and combined standards-area validation only after the focused resource slice is stable.
   Scope: full resvg fixture matrix and `SvgAllAreaRegressionValidationBenchmarks` for paint servers, filters, masks/clips, images, and resource recursion.
   Acceptance: no unrelated text/runtime changes are required to accept this resource-rendering slice.

## Runtime-Gated Groups

The following groups should not be enabled by changing thresholds or inventing baselines:

- W3C `text-dom-*`
- W3C `text-tselect-*`
- W3C `interact-*`
- W3C `script-*`
- W3C `types-dom-*`
- W3C `struct-dom-*`
- W3C `struct-svg-*`

They require a DOM, script, or interaction runtime rather than renderer-only fixes.

## Reference-Suite Constraints

- resvg documents several `textLength` and `lengthAdjust` combinations as unsupported in `externals/resvg/docs/unsupported.md`; rows that now have checked Chrome references are enabled against those browser captures instead of the upstream resvg PNGs.
- As of 2026-05-24, the previously documented deeper resvg text rows are enabled: color-font/emoji clusters, positioned Arabic coordinate lists, Arabic rotate shaping, vertical textPath, complex vertical textPath, tiny-coordinate textPath sampling, and `a-lengthAdjust-001`.
- Remaining text fidelity risk is no longer expressed as skipped resvg rows. It is tracked by scoped Chrome-backed raster thresholds for the rows where Skia and Chrome differ at antialiasing, font metric, decoration, vertical textPath, or path-sampling level.

## Out Of Scope For A Single Renderer Patch

These are not paint-only fixes and should not be misclassified:

- browser selection behavior
- JavaScript execution
- live DOM mutation APIs
- pointer event dispatch
- interactive zoom runtime
- DOM type inspection APIs
