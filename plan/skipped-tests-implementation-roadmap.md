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
  - W3C `text-tselect-01/02/03` now run semantic host-selection assertions instead of a stale static raster comparison. Exact browser chrome selection UI remains host/runtime behavior, but logical `selectSubString`, retained highlight extents, and backward caret metadata are covered.

Verified probe findings from the remaining skipped W3C text rows on 2026-04-09:

- `text-align-05-b`, `text-align-06-b`, and `text-intro-03-b` do not enter any vertical placement branch in the browser-compatible text path today. The actual render stays horizontal, so these rows need a real vertical advance model, not threshold tuning.
- `text-align-08-b` is close in glyph selection but still lacks mixed-script dominant-baseline table handling across Latin, ideographic, and Devanagari glyphs.
- `text-intro-02-b` and `text-intro-09-b` still fail because mixed-direction Hebrew/Latin rows need browser-parity bidi reordering across fallback font spans. Wrapping each fallback span with bidi controls is insufficient.
- `text-intro-05-t` and `text-intro-10-f` still fail because Arabic shaping only stays correct when fallback spans are preserved, but preserving spans still leaves non-Chrome anchor/position parity. A probe to force single-run shaping produced tofu glyphs, which confirms the missing piece is mixed-font shaping/fallback support rather than a simple bidi wrapper.
- `text-altglyph-01/02/03-b` now exercise real SVG-font alternate glyph substitution. They remain sensitive to browser/font raster identity and should not be conflated with the remaining bidi/vertical text layout work.

Current resource-rendering tranche:

- The non-text lane has moved beyond the first resource-rendering parity slice into a dirty deeper static-resource pass. The pass is still not a full browser resource-policy or live-style completion pass.
- The slice covers gradient and pattern inheritance where explicitly authored attributes on referenced resources must override defaults without letting default values mask inherited values.
- The slice includes recursion guards for pattern picture compilation and referenced content so paint servers, markers, clips, masks, filters, and `use` expansions do not re-enter the same resource path indefinitely.
- The slice hardens existing Skia-backed filters by treating invalid `feColorMatrix` values, negative blur/morphology inputs, fractional morphology radii, and lighting parameter mapping as deterministic renderer behavior rather than fixture-specific failures.
- The slice covers clip/use placement where referenced clip content should preserve geometry and transforms while suppressing marker output that does not belong inside the clip resource.
- The slice covers image decode guards so empty, blocked, or malformed image data produces deterministic zero-size image metadata instead of null-image crashes.
- The green resvg resource fixture slice is tracked through already-green deterministic resource-adjacent families with no new thresholds or Chrome overrides. Renderer deltas outside that slice are covered by focused unit tests until their broader visual rows are ready.
- The deeper static-rendering pass now covers cascaded `enable-background`, `BackgroundImage`/`BackgroundAlpha` access from objectBoundingBox filter regions, nested background layer isolation, CSS `filter(...)` function parsing/composition for supported functions and ordered URL/function pipelines, custom-property and guarded CSS math expression resolution (`calc()`, `min()`, `max()`, and `clamp()`) for supported filter lengths/factors/angles, invalid computed-value fallback, modern drop-shadow color parsing in the guarded subset, invalid primitive-region guards, standards default-subregion union and input/output crop clipping for shared primitive-region handling, local/transformed/external `feImage` fragment/resource rows, non-axis `feImage` global layer bounds, mask-on-mask and self/mutual mask-cycle resource guards, explicit hidden/default pattern content clipping, visible pattern overflow bleed into neighboring tiles, inherited pattern viewBox/preserveAspectRatio and objectBoundingBox content units, repeated pattern tile coverage, and marker tangent/viewBox/multi-child placement coverage.
- Remaining deeper rows must not be hidden by broad thresholds: CSS `filter` expression parity outside the guarded static math subset, unsupported CSS value functions and color spaces, broader browser-raster primitive-region and color-management edge cases, pathological/exact browser-raster pattern tile-edge sampling beyond the bounded Skia picture-shader emulation, future/upstream `feImage` edge cases beyond the explicit current allow-list, exact marker browser-raster parity for uncovered path degenerates, mask browser-raster/luminance edge cases beyond the explicit cycle tests, and broader style selector/color parity in resource subtrees.
- A full `tests/filters/feImage/` enablement probe on 2026-05-27 kept the already-enabled 20 rows green and identified seven true implementation gaps. All seven are now enabled without thresholds or baseline swaps: `chained-feImage`, `link-on-an-element-with-complex-transform`, `link-on-an-element-with-transform`, `link-to-an-element-outside-defs-2`, `link-to-an-element-with-transform`, `svg`, and `with-subregion-5`.
- A documentation-only review on 2026-05-28 reconciled this roadmap with the dirty implementation state. Production code was not changed during that review.

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
- Validation is expected to include focused animation rows, any refreshed Chrome overrides, the broader W3C animation suite, and performance review for timing and interpolation changes before enabling more rows. Newly enabled W3C rows with Chrome overrides include `animate-elem-02-t` through `animate-elem-15-t`, `animate-elem-17-t`, `animate-elem-19-t`, `animate-elem-22-b`, `animate-elem-24-t` through `animate-elem-28-t`, `animate-elem-30-t` through `animate-elem-41-t`, `animate-elem-44-t`, `animate-elem-46-t`, `animate-elem-53-t`, `animate-elem-64-t` through `animate-elem-70-t`, `animate-elem-77-t`, `animate-elem-78-t`, `animate-elem-80-t` through `animate-elem-83-t`, `animate-elem-86-t` through `animate-elem-89-t`, `animate-elem-92-t`, `animate-pservers-grad-01-b`, and `filters-composite-05-f`. `animate-elem-90-b` and `animate-elem-91-t` are also enabled, but they intentionally compare against the legacy W3C pass images because current Chrome captures do not match the W3C discrete class/to-only non-interpolable pass criteria for those rows.
- The latest SMIL fixes include order-independent `animateMotion` plus `animateTransform` composition, cloned-document deferred paint-server rebinding for animated gradient resources, discrete accumulated end-value semantics, finite `max` constraints on indefinite set intervals, valid `min`/`max` pair handling, to-only non-interpolable value routing for class/reference/filter/unit attributes, exact midpoint switching for unsupported non-interpolable linear values, full compatibility style-state snapshots when selector reapplication has no tracked candidate set, selector-affecting frame attributes applied before other animated presentation attributes, and hardened Chrome override capture readiness so animation snapshots do not accidentally capture `about:blank` or a pre-seek frame.
- Remaining skipped `animate-*` rows are split between browser-runtime policy cases and deprecated-browser-parity policy cases. Attribute-routing classification now leaves `animate-elem-23-t`, `animate-elem-84-t`, and `animate-elem-85-t` skipped because modern Chrome captures deprecated `animateColor` as a no-op in those rows. Browser-runtime rows such as click-driven `indefinite`, access key, wallclock, pointer-event, and embedded animated image fixtures should stay skipped unless the harness explicitly simulates those runtime inputs.

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

Status: implemented on `codex/css-styling-fidelity` as of 2026-05-27.

Target projects:

- `src/Svg.Custom`
- `src/Svg.SceneGraph`
- `src/Svg.Model`

Features:

- class selector behavior
- inline style vs presentation attribute precedence
- inheritance edge cases
- `use` instance-tree styling semantics where feasible without full DOM runtime

Implemented scope:

- Ordinary CSS declarations now honor `!important` for stylesheet and inline style declarations without leaking the priority marker into parsed SVG paint values.
- Static cascade storage no longer lets repeated low-specificity rules climb past higher-specificity rules through synthetic key increments.
- Selector-list branches are applied with the specificity of the matching branch, including custom-property and raw SVG static-property prepasses.
- Nested `@media` blocks are evaluated for normal declarations, custom properties, and raw static SVG properties against the same SVG viewport media context.
- Linked `<link rel="stylesheet">` and `xml-stylesheet` processing instructions now respect static `media` filters before loading.
- Root selectors such as `svg.theme`, `svg#root`, and `svg[...]` can match the document root for style and custom-property rules.
- Class and `[class~=...]` matching now uses CSS whitespace tokenization instead of splitting only on literal spaces.
- EOF-terminated `@import` rules are accepted where the import is otherwise in the valid leading import section.
- Retained direct `<use>`, `clipPath` `<use>`, and mask clip `<use>` rendering now evaluate referenced content under the `<use>` style parent without applying selectors to generated clones.
- Direct `<use>` rendering now runs a scoped stylesheet pass over the referenced subtree, so selector rules that depend on original ancestors or siblings are suppressed while simple and internal referenced-subtree selectors, inline styles, presentation attributes, and custom properties are preserved and restored after rendering.
- A resvg static CSS allow-list is enabled for rows that align with Svg.Skia's static/browser subset.

Primary test impact:

- W3C `styling-*`
- W3C `struct-use-10-f`, `struct-use-11-f`
- resvg attribute/style buckets

Validation:

- `SvgDocumentCompatibilityLoaderTests` focused CSS/stylesheet coverage passes.
- W3C `styling-*` focused rows pass.
- W3C `struct-use-10-f` and `struct-use-11-f` pass against the checked Chrome overrides.
- WPT SVG2 styling smoke row passes.
- `resvgTests.css_styling_fixtures` passes for the enabled static CSS allow-list.
- Retained scene graph `<use>` style-scope tests pass.

Remaining known gaps:

- resvg `structure/style/external-CSS` remains excluded because the upstream expected image treats the EOF `@import` as unsupported; Svg.Skia intentionally supports this static import path.
- resvg `structure/style/*/non-presentational-attribute` remains excluded because those fixtures expect SVG 1.1 geometry CSS to be ignored, while Svg.Skia keeps SVG2 geometry styling covered by WPT and direct regression tests.

Acceptance criteria:

- Styling rows that depend on static cascade and browser-compatible `<use>` styling semantics are enabled and passing.

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
- deeper static filter/resource parity:
  - cascaded `enable-background`
  - `BackgroundImage` and `BackgroundAlpha`
  - CSS `filter(...)` function and URL-list composition
  - guarded CSS filter `var()`, `calc()`, `min()`, `max()`, `clamp()`, nested fallback, and modern drop-shadow color support
  - local, transformed, external, recursive, subregion, and non-axis `feImage` rendering
  - primitive subregion invalid-value guards
  - pattern overflow clipping, inheritance, objectBoundingBox content units, and repeat coverage
  - marker tangent/orientation/viewBox/multi-child placement coverage
  - mask-on-mask plus self and mutual mask-cycle guards

Implementation status:

- Gradient `href` traversal now breaks any repeated server in the chain, not only direct self-reference cycles. Both retained scene graph painting and the legacy model painting service reject non-finite linear/radial geometry while preserving Skia/Chrome out-of-circle radial focal-point behavior for W3C parity.
- Pattern `href` traversal now breaks repeated servers in non-start cycles. Recursive pattern rendering keeps the active-pattern guard and falls back through the SVG paint fallback chain when a nested recursive paint server is suppressed.
- Filter hardening now treats non-finite `feColorMatrix`, `feGaussianBlur`, and `feMorphology` numeric values as invalid instead of forwarding NaN/Infinity into Skia filters. Fractional morphology radii continue to use ceil-based kernel radii for supported positive values.
- `feImage` decode handling returns a transparent filter input for zero-size decoded image data, keeping invalid or undecodable data from constructing image filters with empty source rectangles.
- Cascaded `enable-background` now participates in retained scene compilation, invalid/non-finite background clips are ignored, and background inputs can be sampled by objectBoundingBox filters instead of falling back to transparent black solely because of the filter units mode.
- `SKSvgSettings.EnableFilterBackgroundInputs` defaults to standards-oriented background input rendering. The W3C Chrome-reference harness disables it only for `filters-overview-01-b`, `filters-overview-02-b`, and `filters-overview-03-b`, where the checked Chrome captures intentionally preserve Chrome's blank-background behavior for those overview panels.
- Retained CSS `filter(...)` values now parse and compose a guarded static subset in authored order: `blur`, `brightness`, `contrast`, `grayscale`, `hue-rotate`, `invert`, `opacity`, `saturate`, `sepia`, `drop-shadow`, and local SVG filter `url(...)` references. Standalone function chains, function-before-URL, URL-before-function, multiple URL filters, repeated URL filters, missing-URL no-op list entries, physical length units, font-relative length units, custom-property substitution, nested variable fallbacks, invalid-variable computed-value fallback, modern drop-shadow colors, deterministic CSS Color 4 sRGB color syntaxes, and guarded CSS math expressions (`calc()`, `min()`, `max()`, and `clamp()`) for supported lengths/factors/angles are implemented for the static subset. Exact browser color-management/raster identity, broader invalid-at-computed-value-time behavior outside the guarded resource-color path, unsupported CSS value functions, wide-gamut color spaces, and relative color syntaxes remain deeper work.
- Filter primitive subregions now reject malformed, non-finite, and non-positive explicit regions before Skia filter construction, preserving the SVG default primitive subregion instead of creating unstable crop rectangles.
- Filter primitive default regions now use the standards union of referenced non-standard result subregions, including `feMerge` merge-node inputs, while standard inputs still default to the filter region. Shared input filters are cropped to the primitive subregion before the primitive runs, and the retained filter layer keeps the final output clipped to the filter region.
- Background inputs now stop at the current element when it establishes its own `enable-background:new` layer, so nested `BackgroundImage` and `BackgroundAlpha` stacks do not leak parent-layer pixels into the new background image.
- `feImage` rendering now preserves measured referenced-element placement for definition and recursion-suppressed paths, suppresses only the owning recursive filter instead of dropping the whole image, maps external raster/SVG image content through the primitive subregion in global target coordinates, and enables local fragment, transformed reference, complex-transform, subregion, chained, recursion, embedded PNG, and external SVG resvg rows. Single-URL non-axis `feImage` filters can opt into a global filter layer so inputless image output is not clipped to the skewed source shape.
- Clip/use coverage pins referenced clip geometry placement while suppressing marker expansion inside clip-resource compilation.
- Mask resources can now carry their own retained `mask` reference, so mask-on-mask composition is resolved through the same resource-key path as visual elements. Focused coverage now includes self-referenced masks, child self-references, mutual mask cycles, and mutual cycles on mask children, with recursive edges skipped rather than recursively drawing or dropping the whole non-recursive mask content.
- Pattern tiles now get an explicit retained content clip for default/hidden overflow, including viewBox patterns where the clip must be expressed in content coordinates. `overflow="visible"` avoids the explicit clip and uses a bounded neighboring-tile picture wrapper so overflow content can bleed across the repeated shader tile in both directions. Focused coverage also includes stylesheet and href-cascaded overflow, inherited pattern viewBox/preserveAspectRatio, objectBoundingBox `patternContentUnits`, and repeated tile sampling. Remaining pattern risk is exact browser-raster tile-edge identity for pathological large-overflow cases beyond the bounded Skia picture-shader emulation.
- Marker placement now has focused retained-scene coverage for quadratic endpoints, arc tangents, subpath boundaries, zero-length markers, angle wraparound, marker viewBox/preserveAspectRatio, and multi-child marker content. Exact browser raster parity for all path degenerates and marker clipping combinations remains broader visual work.
- `SvgResourceRenderingParityTests` covers W3C-compatible out-of-circle focal preservation, non-start gradient and pattern cycles, recursive pattern fallback, invalid filter numbers, zero-size `feImage` decode, non-axis `feImage` global filter-layer bounds, marker suppression inside clip-path `<use>`, background-input sampling, cascaded `enable-background`, self-background-layer `BackgroundImage`/`BackgroundAlpha` stack isolation, primitive default-region union and input/output clipping, CSS filter function composition, URL-plus-function post-processing, function-before-URL composition, multiple URL composition, physical/font-relative filter lengths, CSS filter custom-property/math expressions, nested variable fallbacks, modern drop-shadow colors including deterministic `hwb()`, invalid variable fallback, unitless length rejection, CSS filter clip inflation, CSS drop-shadow source/currentColor handling, CSS Color 4 paint/resource colors for modern `rgb()`/`hsl()` slash alpha, `hwb()`, `transparent`, and `currentColor`, retained mask-on-mask and cycle handling, retained pattern overflow/inheritance/repeat behavior, and retained marker placement.
- `SvgAllAreaRegressionValidationBenchmarks` now includes `resource-rendering-first-slice-regression`, a resource-heavy scenario for linked gradients, radial-gradient guards, patterns, mask/clip resources, markers, `<use>`, filter primitives, embedded images, and command/source coverage.

Deeper resource parity implementation tracks for the current pass:

- Franklin: transformed/external/local `feImage` is implemented for all seven known gap rows. `tests/filters/feImage/link-on-an-element-with-complex-transform` is now enabled through the single-URL non-axis global filter-layer path.
- Kierkegaard: filter primitive region and background stack parity is implemented for the guarded static subset. Covered scope includes objectBoundingBox/userSpaceOnUse primitive-region handling, `enable-background` layer lifetime, nested `BackgroundImage`/`BackgroundAlpha` isolation, invalid region fallback behavior, and background-input sampling without broad thresholds.
- Locke: CSS filter computed-value parity is implemented for the guarded static subset. Covered scope includes ordered URL/function pipelines, custom properties, nested fallbacks, deterministic math expression support, invalid computed-value fallback, and filter resource discovery through CSS declaration validation. Remaining CSS filter work is full browser computed-value parity outside that subset.
- Godel: pattern tiling and inheritance parity is implemented for the current static subset. Covered scope includes default/hidden overflow clipping, visible-overflow bleed into neighboring tiles, stylesheet and href-cascaded overflow, objectBoundingBox `patternContentUnits` scale from target bounds, inherited viewBox/preserveAspectRatio, repeat coverage, transform/invertibility guards, and recursion fallback. Remaining pattern risk is exact browser-raster tile-edge identity for future pathological rows, not a known first-slice blocker.
- Peirce: marker placement parity is implemented for the current static subset. Covered scope includes start/mid/end tangent selection, arcs, zero-length and subpath behavior, neighbor tangent fallback for zero-length endpoint markers, angle wraparound, hidden/default/style overflow clipping, visible overflow no-clip behavior, marker viewBox/preserveAspectRatio, multi-child marker content, and the explicit current resvg marker allow-list. Remaining marker work is exact browser raster parity for future uncovered rows, not a known first-slice blocker.
- Cicero: mask self-reference and cycle behavior is implemented for the current retained-resource subset. Covered scope includes mask-on-mask, mask attributes on mask resources, self-reference cycle detection, nested mask resource keys, child self-reference, mutual mask-cycle guards, luminance alpha multiplication, alpha-mask black-content behavior, userSpaceOnUse mask-region clipping, and objectBoundingBox mask-region clipping. Remaining mask work is exact browser raster parity for future uncovered rows, not a known first-slice blocker.
- CSS value/color parity is implemented for the deterministic resource-affecting sRGB subset. Covered scope includes modern space-separated `rgb()`/`hsl()` with slash alpha, `hwb()` conversion to sRGB, `transparent` as transparent black, `currentColor` through resource filter colors, CSS drop-shadow `hwb()` colors, and direct invalid CSS Color 4 paint declarations that must not override earlier valid declarations. Remaining style/color work is wide-gamut/relative CSS color functions, broader selector-resource cascade edge cases, and external/nested resource styling parity when row-specific probes show the renderer path is aligned with browser static rendering.
- This deeper pass must finish with focused resource tests, the resource resvg allow-list, W3C resource/filter rows, full solution build/test, and `SvgAllAreaRegressionValidationBenchmarks`. New broad visual thresholds or baseline swaps are not acceptance criteria for this lane.

Remaining browser-parity implementation pass:

1. CSS value/color parity.
   - Owner: CSS resource subagent.
   - Status: current scoped pass complete on 2026-05-28.
   - Scope completed: deterministic CSS Color 4 syntax that affects resource paints and filters, including modern space-separated `rgb()`/`hsl()` with slash alpha, `hwb()` conversion to sRGB, `transparent` as transparent black, `currentColor` behavior in resource filters, CSS drop-shadow `hwb()` color tokens, and direct invalid CSS Color 4 paint declarations that must not override earlier valid paint.
   - Acceptance: focused CSS/resource tests and the CSS/resource resvg fixture slice pass without broadening unsupported color spaces into silently wrong rendering. Unsupported wide-gamut or relative color syntaxes remain rejected/planned unless converted correctly.
2. Filter primitive region and color-management parity.
   - Owner: filter resource subagent.
   - Status: current scoped pass complete on 2026-05-28.
   - Scope completed: SVG/Filter Effects primitive subregion defaults, standard-input and result-input unions, objectBoundingBox/userSpaceOnUse math, cascaded `enable-background`, `BackgroundImage`/`BackgroundAlpha` input clipping, displacement-map color interpolation, CSS filter URL/function pipelines, guarded CSS filter math expressions, and non-finite primitive guards.
   - Acceptance: focused filter/resource and W3C filter rows pass; no footer exclusion regions or broad filter thresholds are added to hide region mistakes. Exact browser-raster identity for PNG gamma/profile handling, antialiasing, turbulence/noise, convolution, and lighting math remains a visual-parity limit.
3. Pattern visible-overflow and tile-edge parity.
   - Owner: pattern resource subagent.
   - Status: current scoped pass complete on 2026-05-28.
   - Scope completed: visible-overflow bleed into neighboring repeated tiles, tile-edge clipping for hidden/default overflow, pattern viewBox/content transforms, style/cascade/href overflow, non-invertible transform guards, and retained pattern shader parity for the current static subset.
   - Acceptance: focused pattern tests and enabled resource rows pass; exact browser-raster tile-edge gaps remain documented only for pathological large-overflow rows that exceed the bounded Skia picture-shader emulation.
4. Future/upstream `feImage` edge cases.
   - Owner: `feImage` resource subagent.
   - Status: current scoped pass complete on 2026-05-28.
   - Scope completed: nested external SVG/raster behavior, missing/blocked/zero-size/failed-resource transparent fallback, local and external recursion guards, preserveAspectRatio/subregion/transform parity for the current static subset, and per-filter `feImage` resource caching.
   - Acceptance: focused `feImage` and external-resource tests pass; all explicit current resvg `feImage` rows remain enabled. Remote CORS/network policy, MIME sniffing differences, SVG-as-image script/animation behavior, and unusual encoded-image color-management identity remain browser/runtime parity work.
5. Marker and mask browser-raster parity.
   - Owner: marker/mask resource subagent.
   - Status: current scoped pass complete on 2026-05-28.
   - Scope completed: uncovered marker endpoint degenerates, marker viewport clipping combinations, mask luminance plus source-alpha coverage, alpha-mask black-content behavior, userSpaceOnUse and objectBoundingBox mask-box clipping, mask resource self-reference, and nested mask raster cycle cases.
   - Acceptance: focused marker/mask tests and W3C/resource rows pass; browser-only UI/runtime behavior remains outside this static rendering lane.
6. Performance and integration validation.
   - Owner: parent integration pass.
   - Status: current integration pass complete on 2026-05-28.
   - Scope completed: `externals/SVG` kept clean, focused resource suites run, W3C rows run, full solution build/test run, and `SvgAllAreaRegressionValidationBenchmarks` compared against a master worktree for the common all-area scenario.
   - Acceptance: all validation commands are recorded here with exact pass counts and benchmark means. The benchmark comparison is a short guardrail run, not a statistically rigorous no-regression claim.

Primary test impact:

- resvg resource rendering fixture slice:
  - `tests/filters/feComponentTransfer/*`
  - `tests/filters/feDisplacementMap/*`
  - `tests/filters/feDistantLight/*`
  - `tests/filters/filter-functions/*`
  - `tests/filters/feTurbulence/*`
  - selected `tests/filters/feImage/*` rows covering embedded PNG data, empty/broken references, local fragment references, opacity, `<g>`, `<use>`, preserveAspectRatio, recursive references, subregions, and x/y placement
  - `tests/masking/clip-rule/*`
  - `tests/paint-servers/stop-color/*`
  - `tests/painting/color/*`
  - `tests/painting/fill-rule/*`
  - `tests/painting/image-rendering/*`
  - `tests/painting/isolation/*`
  - explicit current `tests/painting/marker/*` rows through the resource fixture name allow-list, rather than a broad future prefix
  - `tests/painting/mix-blend-mode/*`
  - `tests/painting/paint-order/*`
  - `tests/painting/shape-rendering/*`
  - `tests/painting/stroke*`
  - `tests/painting/visibility/*`
  - `tests/shapes/{circle,line,polygon,polyline,rect}/*`
  - `tests/structure/{a,defs,g,transform,use}/*`
- focused model/Skia unit tests:
  - gradient explicit default inheritance
  - radial focal preservation plus invalid geometry guards
  - pattern `preserveAspectRatio` explicit default inheritance
  - non-invertible pattern transform rejection
  - CSS `clip: rect(...)` parsing
  - invalid image decode guard
  - spot-lit specular code generation argument mapping
- future deeper resource rows:
  - resvg `e-image-*`, future/upstream `e-feImage-*`, `e-marker-*`, `e-pattern-*`, `e-linearGradient-*`, `e-radialGradient-*`, `e-mask-*`, and `e-filter-*` rows that require browser-only behavior, unsupported primitives, full CSS computed-value/style policy, exact resource color-management, or browser-raster identity beyond the explicit current allow-list

Execution order:

1. Lock the green resvg resource fixture slice as the first non-text resource harness.
2. Preserve gradient inheritance fixes and radial guard behavior for the paint-server rows.
3. Preserve pattern inheritance, transform invertibility checks, and pattern recursion guards.
4. Preserve filter hardening before adding the filter graph IR.
5. Preserve clip/use placement behavior so clip resources do not inherit marker output.
6. Preserve image decode guards before broadening external resource policy work.
7. Keep this slice free of new thresholds; add thresholds only in later visual-parity passes after row-specific review.
8. Keep the implemented deeper static subset green before enabling more visual rows.
9. Treat complex-transform `feImage`, mask self/mutual cycles, marker tangent/viewBox coverage, and pattern inheritance/repeat coverage as implemented current-subset work. Do not re-list them as open unless a focused row regresses.
10. Integrate the deeper parity tracks independently and keep their write scopes narrow: `feImage`, filter/backgrounds, CSS filter values, patterns, markers, masks, then cross-cutting selector/color fixes.
11. Keep the true remaining work explicit: CSS filter expression parity outside the guarded static math subset, unsupported CSS value functions and wide-gamut/relative color spaces, style selector/color parity in resource subtrees, exact browser-raster primitive-region/color-management behavior, pathological/exact pattern tile-edge identity beyond the bounded static subset, remote/MIME/scripted SVG-as-image `feImage` behavior, and any future browser-exact pattern/marker/mask raster rows that are not covered by the current focused static subset.
12. Update this roadmap after each track with enabled rows, still-skipped rows, and the exact validation command/results that justify the status.

Acceptance criteria:

- The resource rendering fixture slice is green without baseline swapping.
- No new resvg thresholds or Chrome baseline swaps are required for this first slice. The
  deeper W3C displacement-map validation uses one row-specific threshold for
  `filters-displace-01-f` after visual review showed the displaced grids line up with Chrome and
  the residual delta is PNG gamma/color raster plus labels.
- The implementation does not edit source fixtures or manufacture baselines for unsupported resource behavior.
- Completed deeper static subsets are covered by unit tests and the resource fixture allow-list; remaining deeper rows stay explicitly planned with accurate reasons for CSS filter expression parity outside the guarded static math subset, unsupported CSS value functions and wide-gamut/relative color spaces, style selector/color parity, exact browser-raster primitive-region/color-management behavior, pathological/exact pattern tile-edge identity beyond the bounded static subset, remote/MIME/scripted SVG-as-image `feImage` behavior, and exact browser parity for future uncovered pattern, marker, and mask raster cases.
- Resvg non-text skip count drops materially only after renderer fixes, not by baseline swapping.

Current validation:

- Parent integration pass on 2026-05-28 after all resource subagent slices:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgResourceRenderingParityTests"`: 87 passed.
  - `dotnet test tests/Svg.Model.UnitTests/Svg.Model.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~Svg2StaticStyleContractTests.ComputedStyle|FullyQualifiedName~SvgPatternPaintStateResolverTests|FullyQualifiedName~Svg2StaticPaintServerTests.PatternResolver"`: 27 passed.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~resvgTests.resource_rendering_fixtures|FullyQualifiedName~resvgTests.css_styling_fixtures"`: 466 passed.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~W3CTestSuiteTests.Tests"`: 463 passed, 63 skipped, 0 failed.
  - `git diff --check`: clean.
  - `dotnet format Svg.Skia.slnx --no-restore`: passed. Formatter touched `externals/SVG`; that generated submodule churn was reverted.
  - `git -C externals/SVG status --short`: clean.
  - `dotnet build Svg.Skia.slnx -c Release --no-restore`: passed with 289 existing package advisory, obsolete SkiaSharp API, and nullability warnings; 0 errors.
  - `dotnet test Svg.Skia.slnx -c Release --no-build`: passed. `Svg.Skia.UnitTests` reported 2335 passed, 64 skipped, 0 failed; all other test projects passed.
  - Current branch short benchmark guardrail: `dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj --framework net10.0 -- --filter "*SvgAllAreaRegressionValidationBenchmarks*" --job short --warmupCount 1 --iterationCount 1`: 12 benchmark cases executed.
    - `combined-all-area-regression`: command coverage 18.752 us, command model 55.596 us, render 8.288 ms, DOM metrics 10.942 ms, compile 32.731 ms, load/render/validate 36.089 ms.
    - `resource-rendering-first-slice-regression`: command coverage 4.500 us, command model 8.218 us, compile 555.110 us, DOM metrics 702.900 us, render 3.653 ms, load/render/validate 10.375 ms.
  - Master worktree short benchmark guardrail: `dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj --framework net10.0 -- --filter "*SvgAllAreaRegressionValidationBenchmarks*" --job short --warmupCount 1 --iterationCount 1` from `/tmp/svg-skiamaster`: 6 benchmark cases executed for the common `combined-all-area-regression` scenario.
    - Master `combined-all-area-regression`: command coverage 18.336 us, command model 56.593 us, render 8.328 ms, DOM metrics 10.452 ms, compile 37.457 ms, load/render/validate 65.316 ms.
    - Current branch versus master short-run comparison: compile and end-to-end are faster on the branch, render is essentially flat, command model is slightly faster, and command coverage/DOM metrics are within short-run noise. This does not show a broad performance regression, but a real statistical claim still requires a longer benchmark job.
- Pattern visible-overflow scoped pass on 2026-05-28:
  - `dotnet format Svg.Skia.slnx --no-restore --include src/Svg.Model/Services/SvgPatternPaintStateResolver.cs src/Svg.SceneGraph/SvgScenePaintingService.cs tests/Svg.Skia.UnitTests/SvgResourceRenderingParityTests.cs plan/skipped-tests-implementation-roadmap.md`: passed.
  - `dotnet test tests/Svg.Model.UnitTests/Svg.Model.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgPatternPaintStateResolverTests|FullyQualifiedName~Svg2StaticPaintServerTests.PatternResolver"`: 5 passed.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_Pattern|FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_RepeatsPattern|FullyQualifiedName~resvgTests.resource_rendering_fixtures"`: 460 passed.
- Marker/mask scoped pass on 2026-05-28:
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_Mask|FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_MutualMask|FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_LuminanceMask|FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_AlphaMask|FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_UserSpaceMask|FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_ObjectBoundingBoxMask|FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_Marker|FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_PlacesMarker|FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_UsesArcTangents|FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_DoesNotBridgeSubpath|FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_UsesNeighborTangents|FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_AveragesAutoMarker|FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_AppliesMarker|FullyQualifiedName~SvgResourceRenderingParityTests.RetainedSceneGraph_RendersMultipleMarker"`: 20 passed.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_RendersMaskTypeAlphaAndLuminanceCoverage|FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_StylesheetMaskTypeOverridesPresentationAttribute|FullyQualifiedName~SvgRetainedSceneGraphTests.RetainedSceneGraph_CompilesResvgSelfRecursiveMaskDocumentWithoutRecursing"`: 7 passed.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~resvgTests.resource_rendering_fixtures&DisplayName~marker|FullyQualifiedName~resvgTests.resource_rendering_fixtures&DisplayName~mask"`: 70 passed.
  - `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~W3CTestSuiteTests.Tests&DisplayName~painting-marker|FullyQualifiedName~W3CTestSuiteTests.Tests&DisplayName~masking-"`: 27 passed.

### 5. SVG DOM / Script / Interaction Runtime

Target projects:

- existing runtime surface centered around `src/Svg.JavaScript`, `src/Svg.Skia` interaction dispatch, `src/Svg.Custom` namespace parsing, and W3C harness integration

Features:

- DOM objects and live lists: implemented for document/element `childNodes`, `children`, `getElementsByTagName`, and `getElementsByTagNameNS`; parsed SVG and foreign-namespace element names now report DOM local names instead of CLR fallback names.
- Script execution: existing Jint-backed inline/external script execution remains the runtime base; `contentScriptType` default-script gating is covered by `script-specify-01-f`.
- Event dispatch: implemented element-wide post-order non-bubbling load dispatch, image load dispatch, event bubbling/stop-propagation coverage, namespace-correct dynamic image creation through `setAttributeNS`, SVG mouse-event routing into SMIL eventbase timing, and source-document normalization for events that originate from retained/animated scene elements.
- SVG DOM metrics: implemented viewport-relative `SVGLength.value` / `valueInSpecifiedUnits` conversion for percentage lengths, including nested `<svg>` reparenting behavior.
- Selection APIs: static `selectSubString` support remains covered by text DOM tests; browser UI selection painting remains intentionally out of scope for static rendering.
- Mutation-driven rerendering: existing mutation version invalidation and `SKSvg.RefreshFromSourceDocument()` integration is preserved and now covers dynamically created namespaced image/path/text nodes.
- Interactive pointer and zoom behavior: pointer dispatch, bubbling, stop propagation, pointer-events text hit testing, animated pointer-events state changes, direct viewer zoom/pan transform APIs, and deterministic viewer transform notifications are covered for static harness events. Browser viewer cursor chrome, hyperlink navigation, visual text selection UI, and full browser `SVGElementInstance` event targeting remain policy/runtime-host features.
- Static DOM/types TODO triage: `struct-defs-01-t`, `types-basic-01-f`, and `types-basic-02-f` are not runtime-host work. `struct-defs-01-t` is defs non-rendering, `types-basic-01-f` is number/scientific-notation parsing, and `types-basic-02-f` is CSS-vs-presentation length unit case handling. These rows now use semantic assertions where legacy W3C PNGs are stale or contradict the pass criteria.
- Resource/DOM crossover: nested SVG image loading now resolves implicit SVG image viewports from the containing `<image>` viewport, so `struct-image-16-f` renders the W3C green pass state. The Chrome override for that row was removed because current Chrome captures the spec-failing red state; the row compares against the W3C reference with a scoped full-frame threshold for the SVG/PNG revision-text mismatch only.
- Resource/DOM crossover: invalid and cyclic SVG image references now use the deterministic retained broken-image placeholder policy when placeholders are enabled, including recursive embedded SVG-as-image edges discovered during nested document compilation. This preserves non-recursive nested SVG content and surrounding sibling content while avoiding infinite recursion. W3C `struct-image-12-b` remains a semantic/unit-test-covered policy skip because exact Chrome broken-image icon chrome is browser UI, not Svg.Skia renderer output.

Primary test impact:

- W3C `coords-dom-*`, `text-dom-*`, `types-dom-*`, `struct-dom-*`, `struct-svg-*`, `script-*`, `interact-*`, `text-tselect-*`

Acceptance criteria:

- Runtime-backed rows are enabled only when the DOM/script state is asserted directly or the raster is stable; no fake baselines or broad thresholds are used for browser UI behavior.
- Newly covered rows include `animate-interact-pevents-01/02/03/04-t`, `conform-viewers-03-f`, `extend-namespace-01-f`, `interact-events-01/02-b`, `interact-events-202-f`, `interact-events-203-t`, `interact-order-01/02/03-b`, `interact-pevents-01-b`, `interact-pevents-07/08/09/10`, `interact-pointer-01/02/03/04`, `interact-zoom-01/02/03`, `script-specify-01-f`, `struct-defs-01-t`, `struct-image-07-t`, `struct-image-16-f`, `struct-image-17-b`, `struct-svg-02-f`, `types-basic-01-f`, and `types-basic-02-f`.
- 2026-05-29 interaction/runtime completion pass:
  - `animate-interact-events-01-t` is enabled with a semantic assertion. Pointer dispatch now carries the retained generated `<use>` hit node into SMIL event recording so referenced instance content and ancestor listeners receive `mouseover`/`mouseout`/press events before the normal referencing-element route.
  - `interact-pevents-03/04/05` are enabled with semantic assertions over text character-cell hit testing. Compiled text scene nodes now retain text DOM metrics with separate hit extents, allowing visible glyphs and SVG-font space cells to hit while letter-spacing gaps stay non-targetable.
  - `text-tselect-01/02/03` are enabled as semantic host-selection rows. The tests assert layout-backed logical substring selection, retained extents, visual extents, and backward range metadata without pretending Svg.Skia owns browser selection chrome.
  - `conform-viewers-02-f` is enabled through the existing gzipped nested SVG data URI semantic assertion, with focused resource tests covering W3C-style `image/svg+xml` gzip payloads and compressed SVG image MIME aliases.
  - `struct-image-12-b` is covered by focused resource tests for deterministic invalid/cyclic image placeholders and sibling-content preservation, but the W3C raster row stays skipped because Chrome paints native broken-image UI chrome that Svg.Skia intentionally does not emulate.
  - Focused validation covered the newly enabled W3C rows plus the hit-test/text-selection/viewer/resource unit slices. Final validation also passed `dotnet build Svg.Skia.slnx -c Release --no-restore`, `dotnet test Svg.Skia.slnx -c Release --no-build`, and `dotnet format Svg.Skia.slnx --no-restore --verify-no-changes`.
- Remaining browser-host behavior after this pass is limited to exact viewer/UI chrome parity, such as native text selection painting/focus policy beyond retained host highlights and external navigation chrome. No W3C interaction/runtime row in this lane remains skipped for missing Svg.Skia engine support.

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
7. Preserve the implemented deeper static subset.
   Scope: cascaded `enable-background`, `BackgroundImage`, `BackgroundAlpha`, standalone CSS `filter(...)` chains, ordered CSS function/URL filter pipelines, guarded CSS filter math expressions, primitive-region guards, all explicit current `feImage` rows, mask-on-mask plus self/mutual cycle guards, marker placement subset coverage, and pattern overflow/inheritance/repeat coverage.
   Acceptance: the focused resource unit tests and resource fixture allow-list pass without thresholds or baseline swaps.
8. Keep remaining deeper resource rows explicit.
   Scope: CSS filter expressions outside the guarded static math subset, unsupported CSS value functions/color spaces, style selector/color parity in resource subtrees, exact browser-raster primitive-region and color-management behavior, pathological/exact pattern tile-edge identity beyond the bounded static subset, remote/MIME/scripted SVG-as-image `feImage` behavior, and exact pattern/marker/mask browser parity beyond the explicit current unit/allow-list coverage.
   Acceptance: these rows remain skipped/planned with accurate reasons until their actual renderer/runtime support exists.
9. Run broader resvg, W3C, and combined standards-area validation only after the focused resource slice is stable.
   Scope: full resvg fixture matrix, W3C resource/filter rows, and `SvgAllAreaRegressionValidationBenchmarks` for paint servers, filters, masks/clips, images, and resource recursion.
   Acceptance: no unrelated text/runtime changes are required to accept this resource-rendering slice.

## Runtime-Gated Groups

The following groups should not be enabled by changing thresholds or inventing baselines. Enable rows only through actual runtime support plus focused semantic assertions when the legacy PNG is stale:

- W3C `text-dom-*`
- W3C `text-tselect-*`
- W3C `interact-*`
- W3C `script-*`
- W3C `types-dom-*`
- W3C `struct-dom-*`
- W3C `struct-svg-*`

Most DOM/script rows now have a static runtime path. Remaining skipped rows in these groups are browser-host or deeper browser-DOM features outside Svg.Skia's static runtime contract, such as native cursor/hyperlink chrome and exact visual text-selection UI/focus policy beyond retained host highlights.

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
