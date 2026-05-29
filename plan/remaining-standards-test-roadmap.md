# Remaining Standards Test Roadmap

Status date: 2026-05-29

This roadmap replaces the older broad skipped-test plan for active planning. It lists only the remaining work needed to remove current standards-suite skips or to broaden the currently sliced resvg coverage without hiding gaps behind thresholds or manufactured baselines.

## Current Test Surface

### W3C

Focused W3C command:

```sh
dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~W3CTestSuiteTests.Tests"
```

Last verified full W3C result on this branch after the seven lane edits:

- 523 passed
- 3 skipped
- 526 total

Current source-level W3C skip inventory on this branch:

- `struct-image-12-b`: explicit browser broken-image UI policy skip with semantic renderer coverage.
- `struct-use-08-b`: explicit Chrome recursive capture policy skip with semantic renderer coverage.
- `text-fonts-06-t`: fixture is missing from the bundled W3C checkout.

Rows promoted by lanes 1-4 and validated by the full W3C run:

- `text-align-08-b`
- `pservers-grad-08-b`
- `render-elems-06-t`
- `render-elems-07-t`
- `render-elems-08-t`
- `render-groups-01-b`
- `render-groups-03-t`
- `animate-elem-23-t`
- `animate-elem-84-t`
- `animate-elem-85-t`
- `color-prof-01-f`
- `color-prop-04-t`

### resvg

Fixture inventory in `externals/resvg/crates/resvg/tests`:

- 1730 total SVG fixtures
- 379 text fixtures
- 1351 non-text fixtures

Current resvg harness state:

- `text_fixtures` is enabled.
- `css_styling_fixtures` is enabled for the explicit CSS styling allow-list.
- `resource_rendering_fixtures` is enabled for the explicit resource rendering allow-list.
- The broad `non_text_fixtures` umbrella has been removed.
- Remaining non-text rows are explicit skipped inventory theories by feature area.

Non-text fixture groups by top-level area:

- enabled non-text rows: 466
- remaining explicit inventory rows: 885
- remaining `extra`: 15
- remaining `tests/filters`: 281
- remaining `tests/masking`: 92
- remaining `tests/paint-servers`: 148
- remaining `tests/painting`: 115
- remaining `tests/shapes`: 69
- remaining `tests/structure`: 165

## Lane 1: Mixed-Script Baseline Tables

Promoted row under Lane 1 validation:

- `text-align-08-b`

Current source status:

- The row has been promoted by the Lane 1 worker and is no longer marked skipped in `W3CTestSuiteTests`.
- The full W3C run confirms the promoted row and adjacent text rows pass on this branch.

Problem:

The renderer still lacks browser-grade mixed-script dominant-baseline behavior across Latin, ideographic, and Indic/Devanagari text. The row is close in glyph selection, but alignment is wrong because baseline tables are not modeled with enough script/font specificity.

Implementation plan:

1. Add an internal baseline table service in `Svg.SceneGraph` or `Svg.Skia` that resolves SVG dominant-baseline/alignment-baseline keywords against measured font metrics.
2. Classify text runs by script and writing mode before baseline resolution.
3. Use Skia font metrics for alphabetic/central/middle baselines and add explicit synthesized offsets for ideographic, hanging, and mathematical baselines when the font does not expose the baseline directly.
4. Keep baseline resolution in the shared text layout path so retained rendering, DOM metrics, and selection extents agree.
5. Add focused unit tests for Latin plus ideographic plus Devanagari baseline combinations, including horizontal and vertical writing-mode probes.
6. Enable `text-align-08-b` only after the W3C row visually aligns without a broad threshold.

Acceptance:

- `text-align-08-b` is enabled.
- Existing W3C text rows remain green.
- `SvgTextRegressionValidationBenchmarks` shows no material regression.

Lane status:

- Implementation complete for the current skipped-test roadmap.
- Follow-up benchmark validation remains part of PR readiness, not additional implementation debt for this lane.

## Lane 2: Webfont And Legacy SVG Font Raster Parity

Promoted rows:

- `pservers-grad-08-b`
- `render-elems-06-t`
- `render-elems-07-t`
- `render-elems-08-t`
- `render-groups-01-b`
- `render-groups-03-t`

Remaining related row:

- `text-fonts-06-t`, still missing from the bundled W3C checkout.

Problem:

These rows need exact SVG/WOFF webfont loading and Chrome-compatible glyph outlines/composition. Current fallback text rendering is not enough for W3C rows that compare precise rendered glyph shapes or text inside paint-server/group composition.

Implementation plan:

1. Audit the W3C fixtures and referenced font resources for these rows.
2. Add a font resource loader path that can resolve local SVG fixture font references through `ISvgAssetLoader`.
3. Support `@font-face` and SVG font resources consistently between document loading, nested image documents, and paint-server/group rendering paths.
4. Route loaded font bytes into Skia typeface creation where supported.
5. Preserve existing SVG font/altGlyph handling and avoid duplicating glyph substitution logic.
6. Add deterministic fallback policy when a font format cannot be loaded by SkiaSharp.
7. Update or refresh the bundled W3C fixture checkout if `text-fonts-06-t` exists upstream and is still missing locally.
8. Add focused unit tests for font resource resolution, text inside gradients, and grouped text composition.

Acceptance:

- The six webfont-dependent W3C rows are enabled and pass against Chrome overrides generated through HTTP.
- `text-fonts-06-t` is documented as a local-fixture absence until the bundled W3C checkout is refreshed or repaired.
- No broad image thresholds or Chrome override swaps are added to hide font loading failures.

Lane status:

- Implementation complete for the available W3C rows.
- Remaining work is fixture-management only: restore `text-fonts-06-t` from a trusted W3C source if it exists in the intended suite version, then classify or enable it through the same font-loading path.

## Lane 3: Color Policy And Color Management

Promoted rows under Lane 3 validation:

- `color-prof-01-f`
- `color-prop-04-t`

Current source status:

- The rows have been promoted by the Lane 3 worker and are no longer marked skipped in `W3CTestSuiteTests`.
- `color-prof-01-f` is covered as an explicit optional-ICC unsupported policy assertion.
- `color-prop-04-t` is covered by deterministic system color resolution.
- The full W3C run and color-provider unit tests pass on this branch.

Problem:

`color-prof-01-f` depends on optional ICC color profile behavior that is not a stable Chrome-backed baseline today. `color-prop-04-t` depends on CSS system colors that vary by viewer/platform theme.

Implementation plan:

1. Decide whether Svg.Skia should implement ICC profile conversion in the static renderer, or keep ICC profile rows as explicitly unsupported optional behavior.
2. If implementing ICC, add profile resource loading, color-space conversion, and focused unit tests before enabling the row.
3. Add a host/system color provider abstraction for CSS system color keywords.
4. Provide a deterministic test provider for W3C comparisons without hardcoding platform UI colors into the renderer.
5. Keep default behavior stable across headless CI, macOS, Windows, and Linux.

Acceptance:

- `color-prof-01-f` is either enabled through real color-profile support or remains a documented optional-spec policy skip.
- `color-prop-04-t` is enabled only with a deterministic system-color provider and tests.
- Resource/color regression tests cover CSS Color 4 paths that already pass today.

Lane status:

- Implementation complete for current skipped-test debt.
- ICC conversion remains an optional future feature, not a current W3C skip after this branch because the row is semantically asserted as unsupported optional behavior.

## Lane 4: Deprecated Animation Reference Policy

Promoted rows under Lane 4 validation:

- `animate-elem-23-t`
- `animate-elem-84-t`
- `animate-elem-85-t`

Current source status:

- The rows have been promoted by the Lane 4 worker and are no longer marked skipped in `W3CTestSuiteTests`.
- Semantic assertions cover legacy `animateColor` interpolation and currentColor behavior.
- The full W3C run and animation unit tests pass on this branch.

Problem:

Modern Chrome captures deprecated `animateColor` as a no-op for these rows, so Chrome is not a useful static raster reference. The renderer should not enable these rows by copying a stale or contradictory baseline without a clear policy.

Implementation plan:

1. Inspect each fixture and determine the expected SVG 1.1 behavior independent of modern Chrome.
2. Add semantic tests for `animateColor` interpolation or no-op behavior, whichever policy is chosen.
3. If implementing legacy `animateColor`, route it through the same color interpolation path used by `animate` where possible.
4. If keeping Chrome-compatible no-op behavior, document the decision and replace row skips with semantic assertions that prove the intended behavior.
5. Avoid adding a fake PNG baseline for deprecated behavior.

Acceptance:

- The three rows are no longer simple raster skips.
- Each row has either enabled rendering or a semantic test that asserts the chosen policy.

Lane status:

- Implementation complete for current skipped-test debt.
- The remaining no-op guard is intentionally limited to inherited paint-server color state in `defs`, where browser snapshots keep referenced gradients stable.

## Lane 5: Browser UI And Recursive Capture Policy

Blocked skipped rows:

- `struct-image-12-b`
- `struct-use-08-b`

Problem:

These are not normal renderer gaps. `struct-image-12-b` compares Chrome native broken-image UI chrome, while Svg.Skia has a deterministic retained placeholder policy. `struct-use-08-b` cannot get a stable Chrome capture because of recursive loading behavior.

Implementation plan:

1. Keep deterministic renderer behavior covered by unit tests for broken, invalid, cyclic, and recursive image references.
2. Add optional host-facing hooks only if consumers need browser-like broken-image chrome; keep the renderer default independent of native browser UI.
3. For recursive `use`, keep recursion guards and add semantic tests proving stable output and bounded traversal.
4. Do not create a fake browser UI baseline.

Acceptance:

- Both rows remain explicit policy skips unless a real host/runtime visual policy is introduced.
- The corresponding renderer behavior is covered by semantic/unit tests.

Lane status:

- Implementation complete for current policy coverage.
- Remaining work is policy/product work only: introduce browser-like broken-image chrome or a recursive capture visualization only if Svg.Skia decides to expose that as a host-facing option.

## Lane 6: resvg Non-Text Expansion

Former broad skipped harness:

- `non_text_fixtures` in `tests/Svg.Skia.UnitTests/resvgTests.cs`

Problem:

The broad non-text theory covered 1351 fixture rows. Text, CSS styling, and the current resource subset are already enabled through targeted theories. The umbrella skip has now been replaced by explicit feature-area inventory theories so each future renderer slice has a concrete fixture pool.

Current source status:

- `non_text_fixtures` has been removed.
- `resvg_remaining_non_text_fixture_inventory` accounts for all 1730 resvg fixtures.
- `resvg_remaining_non_text_theories_are_explicit_feature_area_inventory` prevents a broad hardening or umbrella bucket from being reintroduced.
- The remaining explicit skipped row groups are `remaining_extra_fixtures`, `remaining_filter_fixtures`, `remaining_masking_fixtures`, `remaining_paint_server_fixtures`, `remaining_painting_fixtures`, `remaining_shape_fixtures`, and `remaining_structure_fixtures`.

Implementation plan:

1. Replace the broad umbrella skip with generated feature-area theories that can be enabled independently.
2. Keep the existing `css_styling_fixtures` and `resource_rendering_fixtures` theories as the first green slices.
3. Add explicit fixture inventories for the remaining groups:
   - filters not already in the resource allow-list
   - masking not already covered by clip/mask resource tests
   - paint servers not already covered by gradient/pattern resource tests
   - painting operations not already covered by stroke/fill/marker/color subsets
   - shapes edge cases not already covered by W3C and resource fixtures
   - structure/use/image cases not already covered by W3C semantic tests
   - `extra` fixtures
4. For each group, run probe mode, classify failures as implementation, raster-threshold, reference-policy, or unsupported browser/runtime behavior.
5. Enable only rows backed by renderer fixes or row-specific reference review.
6. Move unavoidable browser/runtime rows into explicit named skip lists with reasons rather than relying on the umbrella skip.

Acceptance:

- `non_text_fixtures` is deleted or converted into a non-skipped inventory assertion.
- All remaining skipped resvg rows are explicit by fixture name and reason.
- No new broad thresholds are added.

Lane status:

- Implementation complete for the roadmap split.
- Future resvg expansion should proceed one feature-area theory at a time, with failing fixtures promoted only when backed by a renderer fix, reference review, or explicit unsupported policy.

## Lane 7: Promotion Gate For Deeper Hardening

Lane 7 is not an implementation bucket. It is the guardrail that prevents broad browser-parity work from being counted as remaining skipped-test work unless there is concrete evidence.

Current source review:

- No current W3C skipped row maps directly to generic text/resource hardening.
- Remaining W3C skips are owned by Lane 2 fixture management (`text-fonts-06-t`) or Lane 5 policy coverage (`struct-image-12-b`, `struct-use-08-b`).
- Remaining resvg skips are owned by Lane 6 feature-area inventory theories.
- No deeper text/resource hardening item is promoted by this lane in the current checkout.

Promotion criteria:

An item may move from hardening risk to implementation only when one of these is attached to the task:

1. A named failing W3C row, resvg fixture, or newly added upstream fixture path.
2. A consumer-visible bug report with a minimal SVG reproducer and expected browser/reference behavior.
3. A reference-policy update that explains why the fixture should be enabled, skipped, or semantically asserted.

The promotion packet must include:

- exact fixture path, test row, or issue identifier
- observed Svg.Skia output and expected output
- owning implementation lane, such as text layout, font loading, filters, paint servers, masks, markers, or DOM/runtime
- focused test command to reproduce the failure
- acceptance command and benchmark requirements
- decision on whether the row is raster-enabled, semantically asserted, or explicitly skipped with a row-specific reason

Not valid as Lane 7 work:

- "full browser parity" without a failing fixture or consumer reproducer
- broad text/resource rewrites that do not enable or protect a named row
- new umbrella skips, broad thresholds, or undocumented Chrome/reference swaps
- counting the text/resource hardening risk list as current skipped-test debt

Hardening risk register:

These are real risks, but they stay outside the remaining skipped-test count until promoted by the criteria above.

Text risks:

- Full Unicode Bidi and CSS Text parity for isolates, overrides, plaintext, generated Unicode tables, weak/neutral edge cases, and UAX #9/#14/#29 conformance ingestion.
- Browser-grade line breaking with CSS `line-break`, `word-break`, `overflow-wrap`, and dictionary segmentation for Thai/Lao/Khmer/Myanmar.
- Complete vertical and RTL wrapping, including vertical/RTL wrapped `textLength`, overflow marker placement, DOM metrics, and shape interaction.
- Full textPath-in-wrapping and `method="stretch"` raster parity for multiline, nested, transformed, vertical, fallback-font, emoji, color-font, and complex-script cases.
- Complete CSS Shapes text semantics for shape boxes, image shapes, holes, fill rules, shape margin/padding offsets, floats, and multiple same-line fragments.
- Browser UI selection/focus/caret behavior beyond retained static selection highlights.

Resource risks:

- CSS filter expression parity outside the guarded static math subset.
- Unsupported CSS value functions, wide-gamut color spaces, and relative color syntaxes.
- Broader selector/color parity in resource subtrees and nested/external resources.
- Exact browser-raster primitive-region, color-management, turbulence/noise, convolution, and lighting edge cases.
- Pathological pattern tile-edge identity beyond the bounded Skia picture-shader emulation.
- Remote/MIME/scripted SVG-as-image behavior for `feImage`.
- Exact browser-raster pattern, marker, and mask edge cases beyond the current focused static subset.

Acceptance:

- No Lane 7 item is implemented without a promotion packet.
- Roadmap entries distinguish current skipped-test debt from unpromoted hardening risk.
- Test harness guardrails prevent broad resvg non-text or hardening buckets from reappearing.
- Performance validation for promoted text/resource work includes both focused-area benchmarks and `SvgAllAreaRegressionValidationBenchmarks`.

Lane status:

- Implementation complete for guardrails.
- The hardening risk register remains deliberately unpromoted until a named fixture, upstream addition, or consumer bug supplies a promotion packet.

## Required Validation Per Lane

Before opening a PR for any lane:

```sh
dotnet format Svg.Skia.slnx --no-restore --verify-no-changes
dotnet build Svg.Skia.slnx -c Release --no-restore
dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~W3CTestSuiteTests.Tests"
dotnet test Svg.Skia.slnx -c Release --no-build
```

For text-affecting changes:

```sh
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj --filter '*SvgTextRegressionValidationBenchmarks*'
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj --filter '*SvgAllAreaRegressionValidationBenchmarks*'
```

For resource-affecting changes:

```sh
dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~SvgResourceRenderingParityTests|FullyQualifiedName~resvgTests.resource_rendering_fixtures"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj --filter '*SvgAllAreaRegressionValidationBenchmarks*'
```

For resvg fixture expansion:

```sh
dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-build --filter "FullyQualifiedName~resvgTests"
```

For Lane 7 promotion-gate changes:

```sh
dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~resvgTests.resvg_remaining_non_text_fixture_inventory|FullyQualifiedName~resvgTests.resvg_remaining_non_text_theories_are_explicit_feature_area_inventory"
```

## Implementation Order

1. Mixed-script baseline tables (`text-align-08-b`) - complete.
2. Webfont/SVG font resource loading for W3C render and paint-server rows - complete for available fixtures.
3. Color policy decisions and deterministic system color provider - complete.
4. Deprecated `animateColor` semantic policy - complete.
5. Browser UI and recursive capture policy cleanup - complete as explicit policy coverage.
6. Split resvg non-text umbrella into explicit feature-area theories - complete.
7. Enforce the Lane 7 promotion gate; do not implement unbacked deeper text/resource hardening - complete.

Remaining current roadmap work after this branch:

- Restore or permanently document missing W3C `text-fonts-06-t`.
- Keep `struct-image-12-b` and `struct-use-08-b` as explicit policy skips unless the project chooses browser UI/recursive capture visual emulation.
- Expand resvg non-text coverage by enabling explicit feature-area theories in separate renderer slices.
