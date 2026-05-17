# SVG 2 Static Subset Implementation Plan

## Status

Created: 2026-05-16
Updated: 2026-05-17

This is a living planning and API contract artifact. The first implementation tranche now covers project boundary guards, SVG 2 load/href contracts, the partial `SvgParameters` load-options bridge, document state, runtime image policy, style/property scaffolding, a computed-style cache for selected SVG 2 properties, selected SVG 2 element attributes, retained path/text `paint-order`, retained marker and focused use-context paint rendering, effective unnamespaced href resolution for common static resources, `mask-type` coverage, linked filter/`feImage` dependency handling, focused `feDropShadow` filter compilation fixes, shared geometry service coverage for focused shape/path consumers, CSS geometry percentages with viewBox, SVG 2 `use` dimension override semantics, focused context-paint marker/use parity, verified `radialGradient fr` rendering through conical shaders, focused textPath SVG 2 placement, and validation/spec-status coverage for preserve-only and deferred static-subset boundaries. The focused WPT SVG 2 static subset now contains 46 active SVG-only rows with no `DeferredRows` list; active failures are treated as implementation gaps, while browser-runtime WPT rows remain outside the static scope. Rows below still describe the full target state, not a claim that every SVG 2 static feature is complete.

The intent is to move SVG 2 feature work into `Svg.Custom` and the shared renderer stack rather than carrying local edits in `externals/SVG`. Existing examples of this direction are the animation element model, pointer-events, vector-effect, compatibility CSS handling, and paint-server overrides already under `src/Svg.Custom`.

## Current Progress Review

Reviewed: 2026-05-17

| Status | Area | Notes |
|---|---|---|
| Complete in current tranche | Project boundary guards | Added architecture tests to keep SVG 2 parser/model work in `Svg.Custom` and renderer work out of parser layers. |
| Partial | SVG 2 load/resource contracts | Added `SvgProcessingMode`, `SvgExternalResourcePolicy`, and `SvgDocumentLoadOptions` as `Svg.Custom` contracts. `SvgParameters` now partially bridges load options, document state, runtime image policy, nested SVG inheritance, CSS `@import` blocking for `Disabled` and `SameDocumentAndDataOnly`, and stricter same-origin file confinement under the document directory. Full host-control surface and broader external-resource policy coverage remain open. |
| Complete in current tranche | Effective `href` precedence | Added raw parsed href tracking so unnamespaced `href` wins over `xlink:href`, including empty/whitespace `href`; renderer call sites now use effective href for `use`, text refs/textPath, image, `feImage`, filters, clip paths, and masks. |
| Complete in current tranche | Programmatic href changes | Effective href detects typed `href` property changes after parse and uses the new value instead of stale parsed compatibility state. |
| Complete in current tranche | Gradient and pattern href templating | Gradient/pattern inheritance now uses effective SVG 2 href, so XML attribute order cannot make `xlink:href` override unnamespaced `href`. |
| Complete in current tranche | Validation/spec-status lane | SVG 1.1 and SVG 2 static-subset support docs now use supported/partial/deferred terminology consistently, and focused tests cover preserve-only/deferred behavior for unknown SVG elements, mesh/hatch/solidcolor, unsupported vector-effect values, `stroke-linejoin: arcs`, and dynamic/interactive content boundaries. |
| Complete in current tranche | SVG 2 style/model scaffolding | Added parser/style contracts for `paint-order`, compositing CSS properties, selected transform/text properties, `pathLength`, symbol geometry/ref attrs, and `feDropShadow`. |
| Complete in current tranche | CSS-only compositing API | `isolation` and `mix-blend-mode` remain CSS-only for bare presentation attributes, while typed getters now reflect CSS/custom values after style parsing. |
| Complete in current tranche | `orient=auto-start-reverse` | Existing parser/runtime support is covered by new model tests; start markers are flipped in scene compilation. |
| Complete in current tranche | Retained `paint-order` | Shape/path fill, stroke, and markers are ordered in the retained renderer; retained text now orders fill, stroke, and decoration-as-marker phases. |
| Partial | `feDropShadow` model and scene filter | Added `feDropShadow` model contract, retained filter expansion, explicit missing-input handling, deep-copy preservation for parsed/programmatic offsets, and a focused outside-source-bounds pixel fix. Broader Filter Effects parity remains open. |
| Partial | Processing modes and resource policies | Contracts exist and the `SvgParameters` bridge/resource policy path is partially implemented. CSS imports are blocked for `Disabled` and `SameDocumentAndDataOnly`, same-origin file access is confined under the document directory, and runtime image policy/document state/nested SVG inheritance are wired in part; secure static enforcement, linked stylesheet policy, fonts, and complete host exposure still need implementation. |
| Partial | Computed style foundation | A centralized computed style snapshot/cache now backs selected SVG 2 properties, including paint-order, white-space/text properties, marker refs, mask-type, isolation, and mix-blend-mode. Full migration of all geometry, paint, filter, text, and context-paint reads remains open. |
| Partial | `pathLength` | Parsed on selected path-based elements, and focused retained paths normalize dash distances and textPath distance mapping. Marker normalization and shared length services remain open. |
| Complete in current tranche | Symbol geometry/ref attrs | Parsed on `SvgSymbol`; retained `use`/symbol viewport sizing and `refX`/`refY` reference-point layout are now integrated for focused static paths. |
| Complete in current tranche | Focused WPT SVG 2 static subset | The active WPT SVG 2 manifest now covers CSS geometry percentages with viewBox, context paint marker/use cases, paint-server fallback/currentColor cases, path bearing commands, selected shape/style rows, `use` symbol/svg dimension overrides, and focused textPath rows. The manifest contains 46 active SVG-only rows and no `DeferredRows` list; active rows should remain enabled and any failures should be fixed as renderer/model gaps. |
| Partial | Text SVG 2 work | Retained text `paint-order`, inline textPath `path`, href-to-basic-shape textPath support, focused `white-space: pre`, `side=right`, closed-loop text, and textPath `pathLength` scaling are implemented. Wrapping properties, complete CSS Text integration, and broader textPath parity remain later work. |
| Partial | Masking/filter semantics | Effective href, `mask-type` coverage, linked filter/filter `feImage` dependency handling, and focused `feDropShadow` compilation fixes are implemented. Full `feDropShadow` parity, filter region/color-interpolation audits, and broader masking/filter behavior remain. |
| Partial | Context paint | `context-fill` and `context-stroke` parser/model support, retained marker rendering, focused use propagation, and selected fallback-chain behavior are implemented. Broader resource context-paint propagation, inheritance edge cases, and pixel parity remain open. |
| Partial | Shared geometry abstraction | `SvgGeometryService` now centralizes focused equivalent-path creation, CSS geometry reads, pathLength normalization, retained clipping/rendering paths, shape textPath targets, and selected marker extraction. BBox options, full hit testing, and marker-on-all-shapes parity remain open. |

## Review Findings Fixed

| Finding | Fix |
|---|---|
| Empty or whitespace `href` fell back to `xlink:href`. | Effective href lookup now treats present unnamespaced `href` as authoritative, while URI conversion returns false for blank values. |
| Gradient/pattern inheritance did not use SVG 2 href precedence. | `SvgGradientServer` and local `SvgPatternServer` now resolve `InheritGradient` through effective href. |
| Parsed href compatibility cache could become stale after programmatic property changes. | Effective href compares the current typed attribute with the parsed value and prefers changed typed properties. |
| `feDropShadow` deep copies lost explicit `dx`/`dy`. | `dx`/`dy` now live in the attribute collection, so `DeepCopy` preserves parsed and programmatic values. |
| CSS-only `isolation` and `mix-blend-mode` typed getters ignored CSS values. | Getters now read effective CSS/custom values before falling back to typed attributes/defaults. |
| `feDropShadow in="missing"` could shadow `SourceGraphic`. | Explicit unresolved inputs now produce transparent output for that primitive/result instead of implicit source fallback. |
| Retained text ignored `paint-order`. | Text compiler now runs fill, stroke, and decoration phases through the same paint-order decision path used by retained text commands. |
| `PreferSvg2Href` was not consistently honored across SVG 2 href paths. | Href resolution now routes through the effective SVG 2 href helper in the reviewed call sites. |
| Invalid href call sites used throwing URI paths or ambiguous checks. | Invalid href handling moved to `Try*` APIs so bad values fail closed and keep fallback behavior explicit. |
| Fragment href matching was case-insensitive in places. | Same-document href/id lookup now preserves case-sensitive SVG id semantics in the fixed paths. |
| CSS imports ignored the external resource policy. | CSS `@import` is now blocked for `Disabled` and `SameDocumentAndDataOnly` policies. |
| Invalid `paint-order` values could inherit incorrectly. | Invalid paint-order handling now follows fallback semantics instead of leaking inherited invalid state. |
| Context paint properties left stale or over-broad state behind. | Context paint property cleanup now keeps parsed/effective values scoped to the supported context-paint model. |
| `SameOrigin` policy allowed broad file-backed CSS imports. | CSS imports and shared file resource checks now confine same-origin file access under the document directory. |
| `SvgParameters` load-options overload made `new SvgParameters(null, null, null)` ambiguous. | Removed the competing 3-argument load-options overload; load options use the 4-argument or named-argument record form. |
| Linked SVG documents set `BaseUri` after parse, too late for CSS `@import` policy checks. | Stream-based nested SVG/SVGZ loads now pass the resource URI into the compatibility loader before parsing, and a same-origin nested SVG CSS import regression covers the behavior. |
| Preserve-only/deferred SVG 2 features were not covered by contract tests. | Added focused tests for mesh gradients, hatches, `solidcolor`, unknown SVG elements, unsupported vector-effect values, `stroke-linejoin: arcs`, and dynamic/interactive content preservation without static mutation. |

## Remaining Work Snapshot

- Finish secure/static resource policy enforcement beyond the current `SvgParameters` bridge: linked stylesheet and font policy, remaining external SVG/image policy parity, host-control exposure, and complete inheritance through nested SVG documents.
- Complete computed style consolidation so SVG 2 styleable geometry, text, marker, mask, filter, and context-paint reads do not depend on scattered raw-attribute access.
- Finish context paint beyond the retained marker and focused use paths: broader referenced-content propagation, inheritance edge cases, and additional browser-reference pixel coverage outside the focused WPT rows.
- Finish textPath SVG 2 behavior beyond focused retained paths: broader baseline/offset parity, graphics effects on text content, complete CSS Text integration, and later wrapping properties.
- Finish mask/filter audits: broader `feDropShadow` parity, filter primitive regions, `color-interpolation-filters`, alpha/luminance mask parity, and dependency/cycle coverage for linked filters and `feImage`.
- Expand the shared geometry abstraction for marker-on-all-shapes parity, bbox options, full hit testing, and remaining pathLength normalization consumers.
- Continue focused semantic and browser-reference tests for each completed slice before broad W3C/resvg runs.

## Primary References

- SVG 2 specification: https://www.w3.org/TR/SVG2/
- SVG 2 changes from SVG 1.1: https://www.w3.org/TR/SVG2/changes.html
- SVG 2 processing modes: https://www.w3.org/TR/SVG2/conform.html#processing-modes
- CSS Masking Level 1: https://www.w3.org/TR/css-masking-1/
- Filter Effects Level 1: https://www.w3.org/TR/filter-effects-1/
- CSS Transforms Level 1: https://www.w3.org/TR/css-transforms-1/
- CSS Color Level 3: https://www.w3.org/TR/css-color-3/
- CSS Text, CSS Fonts, CSS Writing Modes, CSS Inline Layout, CSS Images, CSS Compositing and Blending, and CSS Values as referenced by SVG 2.

## Static Subset Boundary

SVG 2 defines static and secure static processing modes. This plan targets static rendering of a loaded document into the existing retained shim picture model and Skia output.

In scope:

- parsing and preserving SVG 2 elements, attributes, and presentation properties that can affect static pixels
- CSS cascade/computed-value work needed for static SVG 2 rendering
- static resource resolution for same-document references, data URLs, and configured external resources
- shape, path, marker, paint server, text, clipping, masking, filter, transform, image, and compositing behavior that affects static output
- compatibility with existing SVG 1.1 and current `Svg.Custom` behavior
- tests at parser/model, scene graph, shim command, Skia adapter, and browser-reference levels

Out of scope for this plan:

- scripting execution and live DOM APIs
- interaction/event dispatch beyond attributes that must be parsed and preserved
- animation timelines, except ensuring animation elements remain represented by the current `Svg.Custom` animation model
- CSSOM, layout engine APIs, browser selection/editing behavior, and runtime media elements
- implementing removed SVG 1.1 features as new SVG 2 work, such as SVG Fonts and `tref`
- draft/deferred SVG 2 features removed before Candidate Recommendation, such as mesh gradients, hatches, and `solidcolor`, except preserving unknown markup

## Validation Lane Status

- [x] Align SVG 1.1 and SVG 2 support articles with supported/partial/deferred terminology for the current branch.
- [x] Add model tests for preserve-only/deferred SVG 2 elements and dynamic/interactive out-of-static-subset behavior.
- [x] Add renderer contract tests for unknown SVG element preservation, unsupported vector-effect fallback, and `stroke-linejoin: arcs` fallback.
- [ ] Add generated spec-status snapshots for every feature-matrix row; current coverage is still prose plus focused semantic tests.
- [ ] Run full docs/build/test gates after the shared worktree build blocker in `SvgGeometryService` is resolved.

## Generated Spec-Status Snapshot Proposal

Keep the first snapshot mechanism documentation-only until the active renderer
gaps stabilize. A low-risk path is a small generator that reads this plan's
feature matrix, `site/articles/reference/svg-2-static-subset-support.md`, and
`WptSvg2StaticSubsetTests.StaticSubsetRows`, then writes a checked
`plan/svg2-static-subset-spec-status.generated.md` file with:

- one row per feature-matrix item with the documented status and owning test signal
- the active WPT SVG 2 row count and row paths
- the explicit static-scope exclusions for scripting, live DOM, CSSOM,
  interaction, navigation, media, and broad animation timelines
- a stale-file check for CI or pre-commit use once the output is stable

The generator should not infer pass/fail status from skipped metadata because
the WPT SVG 2 static subset no longer carries a `DeferredRows` list. Test
results should come from the focused WPT test run, and any active-row failure
should remain visible as an implementation gap.

## Architecture Rule

SVG 2 work should use this ownership split:

| Layer | Owns | Must not own |
|---|---|---|
| `src/Svg.Custom` | SVG parser/model types, `[SvgElement]`, `[SvgAttribute]`, converters, style attribute recognition, DOM-state preservation, compatibility resource/href helpers, typed SVG 2 values. | SkiaSharp, ShimSkiaSharp, renderer command generation, UI-host behavior. |
| `src/Svg.Model` | Shared rendering math and services: path conversion, painting, gradients, masks, filters, hit testing, asset loading, feature flags, compatibility parameters. | SkiaSharp-native output and host controls. |
| `src/Svg.SceneGraph` | Retained scene construction from `Svg.Custom` DOM into shim IR, computed render tree, resource expansion, text layout orchestration. | SkiaSharp-native output and host controls. |
| `src/ShimSkiaSharp` | Renderer-neutral drawing IR, paths, paints, shaders, filters, commands, clone/edit contracts. | SVG DOM parsing, Svg.Custom references, SkiaSharp references. |
| `src/Svg.Skia` | Conversion from shim IR to real SkiaSharp, SKPicture lifecycle, text shaping through Skia/HarfBuzz, rendering caches. | SVG semantic parsing decisions. |
| Host controls | Input, scheduling, invalidation, dependency properties, source loading adapters. | Core SVG semantics. |

Any upstream SVG parser behavior that must diverge for Svg.Skia should be implemented as a local file under `src/Svg.Custom` and, when necessary, remove the upstream source from `Svg.Custom.csproj`. Do not modify `externals/SVG` for SVG 2 rollout work unless the task is explicitly to prepare an upstreamable patch.

## Existing State

| Area | Current implementation |
|---|---|
| Parser composition | `src/Svg.Custom/Svg.Custom.csproj` compiles most of `externals/SVG/Source` and overrides selected upstream files locally. |
| Element discovery | `[SvgElement]` and `[SvgAttribute]` metadata feed the SVG generator and `SvgElementFactory`. |
| Unknown attributes | `SvgElement.CustomAttributes` preserves unsupported attributes as strings. |
| CSS compatibility | `SvgStyleAttributeNames`, inline style parsing, CSS variable resolution, compatibility style capture, and presentation attribute handling live in `src/Svg.Custom/Compatibility`. |
| SVG 2 adjacent additions | `pointer-events`, `vector-effect: non-scaling-stroke`, `mix-blend-mode`, `isolation`, unnamespaced `href` handling in some runtime paths, animation elements, and compatibility marker shorthand behavior. |
| Renderer IR | `ShimSkiaSharp` has path, paint, shader, filter, save-layer, clip, image, picture, text, and text-on-path commands with source metadata. |
| Scene graph | `Svg.SceneGraph` compiles the DOM to retained shim nodes and commands. |
| Skia adapter | `Svg.Skia` converts shim commands and model objects into native SkiaSharp objects. |
| Browser references | W3C, resvg, and WPT SVG 2 Chrome reference capture scripts enforce HTTP-based/browser-compatible captures. |

## Proposed Cross-Cutting API Contracts

These contracts should be introduced early because most SVG 2 features need a shared vocabulary.

| Contract | Project | Proposed shape | Purpose |
|---|---|---|---|
| Processing mode | `Svg.Custom` | `public enum SvgProcessingMode { Static, SecureStatic, Animated, SecureAnimated, DynamicInteractive }` | Lets parser/model know whether scripts, animation, interaction, and external resources are allowed. |
| Load options | `Svg.Custom` plus `Svg.Model` adapter | `public sealed class SvgDocumentLoadOptions { SvgProcessingMode ProcessingMode; SvgExternalResourcePolicy ExternalResources; bool PreserveUnknownElements; bool PreferSvg2Href; }` and `SvgParameters` maps into it. | Keeps SVG 2 mode and resource policy explicit without encoding it in renderer settings. Partially implemented through the current `SvgParameters` bridge. |
| External resource policy | `Svg.Custom` or `Svg.Model` | `public enum SvgExternalResourcePolicy { Enabled, SameOrigin, SameDocumentAndDataOnly, Disabled }` | Static vs secure static resource boundaries. Partially enforced for runtime image policy, CSS import blocking under `Disabled` and `SameDocumentAndDataOnly`, and same-origin file confinement under the document directory; full linked-resource coverage remains open. |
| Effective href helper | `Svg.Custom` | `public static bool TryGetEffectiveHref(this SvgElement element, out Uri href)` and optional `ISvgHrefElement`. | SVG 2 `href` wins over deprecated `xlink:href`, while preserving legacy compatibility. |
| Computed style snapshot | `Svg.Custom` | `public sealed class SvgComputedStyle` plus internal resolver. | Stable way to ask for geometry, paint, text, mask, filter, and transform properties after cascade. |
| CSS property registration | `Svg.Custom` | `SvgStyleAttributeNames` expanded and grouped by property category, with CSS-only exclusions for properties such as `isolation` and `mix-blend-mode`. | Avoids per-feature parser hacks while keeping only valid presentation attributes in the presentation-attribute cascade. |
| Static feature flags | `Svg.Model` | `public sealed class SvgStaticFeatureOptions` on `SvgParameters` or settings bridge. | Allows staged rollout and compatibility toggles for high-risk text/filter features. |
| Scene render context | `Svg.SceneGraph` | `SvgSceneCompileContext` carries processing mode, resource policy, computed style cache, inherited context paint, viewport stack. | Avoids recomputing style/resource state and keeps renderer semantics centralized. Partially present through document state, runtime image policy, nested SVG inheritance, and retained marker context-paint state. |
| Context paint | `Svg.Custom` plus `Svg.SceneGraph` | `SvgContextPaintServer`, `SvgContextPaintKind.Fill/Stroke`, and scene context values. | Parser/model and retained marker rendering are implemented; reused-content propagation and fallback-chain parity remain open. |
| Paint order | `Svg.Custom` plus `Svg.SceneGraph` | `SvgPaintOrder` enum or value object with ordered phases `Fill`, `Stroke`, `Markers`. | Allows rendering shapes and text in SVG 2 paint order. |
| Geometry path abstraction | `Svg.Model` | `ISvgGeometrySource` or internal `SvgGeometryPath` with path, length, markers, bbox options. | Unifies basic shapes, path, textPath, clipping, markers, and `getBBox` style APIs. |
| Shim capability markers | `ShimSkiaSharp` | Add only when needed: extra shader/filter/paint records, command metadata, or bbox flags. | Keep renderer-neutral commands explicit and cloneable. |

## Feature Contract Matrix

Legend:

- `Keep`: feature already exists and needs conformance hardening.
- `Partial`: feature has landed in at least one parser/model/rendering path, but remaining rows or parity work are still open.
- `Add`: new model/rendering work.
- `Compat`: parse/preserve for compatibility, but not part of SVG 2 static rendering.
- `Defer`: explicitly not part of the first SVG 2 static subset implementation.

| Feature | SVG 2 static status | Svg.Custom contract | Svg.Model / Svg.SceneGraph contract | ShimSkiaSharp contract | Svg.Skia / host contract | Priority |
|---|---|---|---|---|---|---|
| Static and secure static processing modes | Partial | `SvgProcessingMode`, `SvgDocumentLoadOptions`, and resource policy contracts exist. | Compile context has partial document state/resource policy wiring; full secure-static enforcement remains open. | No direct change. | `SvgParameters` partially bridges processing mode/resource policy; host controls expose opt-in properties later. | P0 |
| CSS value parsing for SVG attrs | Add | Centralize CSS-compatible parsing for lengths, percentages, angles, colors, transform lists, paint values. Invalid values fall back to initial/computed behavior. | Consume typed/computed values only. | No direct change. | No direct change. | P0 |
| Expanded style attribute list | Partial | Extend `SvgStyleAttributeNames` with SVG 2/CSS properties: `paint-order`, `vector-effect`, `transform-box`, `transform-origin`, `white-space`, `text-overflow`, `inline-size`, `shape-inside`, `shape-subtract`, `mask-type`, `clip-path` CSS forms, and marker group properties. Keep `context-fill` and `context-stroke` in the `<paint>` grammar rather than style attribute names. | Scene compiler asks computed style resolver instead of raw attributes. | No direct change. | No direct change. | P0 |
| UA stylesheet | Add | Provide internal default stylesheet constants and property defaults. | Render tree uses UA defaults for never-rendered elements, overflow, root/nested `svg`, image, marker, pattern, symbol. | No direct change. | No direct change. | P0 |
| Presentation attributes on SVG elements | Keep/Add | Treat recognized SVG 2 presentation attributes as author-level style with presentation specificity. Keep current marker shorthand and CSS-only compositing-property exceptions documented. | Use computed style cache. | No direct change. | No direct change. | P0 |
| `href` replacing `xlink:href` | Partial | Effective href helper and typed unnamespaced `href` partials exist for common static resource paths; invalid hrefs use `Try*` APIs and id lookup is case-sensitive in fixed paths. | Resource resolver calls effective href in reviewed paths, including use, text refs/textPath, image, gradients, patterns, filters, `feImage`, clip paths, and masks. Cycle/fallback coverage still needs broad audit. | No direct change. | Asset loader receives effective URI and base URI in the implemented paths. | P0 |
| Removed/deprecated `version`, `baseProfile`, `requiredFeatures`, `externalResourcesRequired` | Compat | Preserve as custom/legacy attributes where current API expects them; do not gate SVG 2 rendering on `requiredFeatures`. | Existing behavior already ignores requiredFeatures for Chrome parity; document and keep. | No direct change. | No direct change. | P0 |
| `defs`, `title`, `desc`, `metadata`, `style`, resource elements never render | Keep/Add | UA stylesheet and typed metadata attributes including `lang`. | Render tree excludes direct painting while preserving children/resources for references. | No direct change. | No direct change. | P0 |
| Unknown SVG elements | Add but at-risk | Preserve as `SvgUnknownElement` with namespace, children, attributes, and style. | Treat as non-rendering container by default for first tranche; optional later renderable container behavior for browser parity. | No direct change. | No direct change. | P2 |
| Nested links | Add but at-risk | Allow `<a>` inside `<a>` and preserve attributes. | Static render unaffected; hit/link metadata can choose innermost link later. | Command source metadata already enough. | Host link activation remains outside static subset. | P3 |
| `switch`, `requiredExtensions`, `systemLanguage` | Keep/Add | Keep condition attrs as strings; remove `requiredFeatures` dependence in SVG 2 mode. | Shared condition evaluator selects first matching child before render tree expansion. | No direct change. | Existing system language override remains test hook. | P1 |
| Geometry properties for shapes | Add | Treat `x`, `y`, `cx`, `cy`, `r`, `rx`, `ry`, `width`, `height` as styleable geometry where SVG 2 allows it. | Resolve geometry after cascade and viewport percentages. | Existing paths are enough. | No direct change. | P0 |
| `svg` width/height initial `auto` | Add | Add `auto`-capable unit/value support for geometry properties. | Intrinsic size and nested viewport resolution must distinguish root, outer, and nested SVG. | No direct change. | `SKSvg` standalone viewport continues to supply explicit viewport when configured. | P1 |
| `image` width/height `auto` and intrinsic sizing | Add | Add auto units and `crossorigin` parsing. | Asset loader result carries intrinsic size and aspect ratio; layout resolves auto dimensions. | No direct change. | `SkiaSvgAssetLoader` exposes intrinsic metadata if not already derivable. | P1 |
| `foreignObject` geometry properties | Keep/Add | Geometry style attrs and `requiredExtensions` preserved. | Static renderer should provide explicit unsupported fallback policy first; later optional hosted/static HTML rendering. | Could require DrawPicture/image fallback if rasterized. | Host-specific rendering stays out of core static subset unless separately planned. | P3 |
| `symbol` geometry, `refX`, `refY` | Add | Add `x`, `y`, `width`, `height`, `refX`, `refY` typed properties to `SvgSymbol`. | Use/symbol expansion applies viewport and reference point semantics. | Existing save/matrix/draw commands enough. | No direct change. | P1 |
| `use` shadow tree and inherited style | Keep/Add | Effective href, style inheritance into instance tree, preserve original referenced DOM. | Scene graph expands use into an instance subtree with inherited context, cycle guard, and source metadata. | Existing command metadata enough; optional source-address extension. | Native composition keeps source mapping. | P1 |
| `d` as presentation property | Add | Treat `d` as styleable on `path`; parse via existing path builder after cascade. | Scene geometry reads computed `d`, not only raw property. | Existing SKPath enough. | No direct change. | P1 |
| Path syntax and error handling clarifications | Keep/Add | Harden path parser for SVG 2 grammar, implicit commands, invalid token stop behavior. | Geometry service exposes empty/non-rendering path state. | Existing SKPath enough. | No direct change. | P1 |
| Empty path/polygon/polyline behavior | Keep/Add | Parser preserves empty values. | Scene graph emits no draw commands for empty geometry while preserving element metadata. | No change. | No change. | P1 |
| `pathLength` on all basic shapes | Add | Add typed `pathLength` to `SvgPathBasedElement` or shape partials. | Geometry service normalizes distances for dash arrays, textPath, markers, and path APIs. | Existing path and path-effect may need normalized dash helper only. | No direct change. | P1 |
| Basic shapes as equivalent paths | Keep/Add | No public API change beyond `pathLength`. | Shared geometry abstraction returns equivalent path, markers, length, bbox for every shape. | Existing SKPath enough. | No direct change. | P1 |
| `ellipse rx/ry=auto` | Add | Add auto-capable radius values or preserve raw auto in computed style. | Resolve one auto radius from the other; both auto become 0. | Existing SKPath oval commands enough. | No direct change. | P1 |
| `transform` as CSS property | Add | Parse presentation attr and CSS property through one transform converter; add `transform-origin`, `transform-box`. | Transform stack resolves after geometry boxes are known. | Existing matrix command enough. | No direct change. | P1 |
| `viewBox` / `preserveAspectRatio` without `defer` | Keep/Add | Keep parser but ignore/compat-preserve removed `defer`. | Viewport transform service owns SVG 2 behavior. | Existing matrix/clip commands enough. | No direct change. | P1 |
| Object bounding box units | Keep/Add | No public API change. | Centralize bbox resolution for gradients, patterns, masks, filters, markers, clip paths. | Existing commands enough. | No direct change. | P1 |
| `vector-effect: non-scaling-stroke` | Keep | Existing `SvgVectorEffect` partial remains in `Svg.Custom`. | Ensure scene graph sets paint non-scaling stroke and hit/bounds account for it. | Existing `SKPaint.IsStrokeNonScaling`; verify clone/Skia conversion. | `SkiaModel` maps to Skia behavior or path transform fallback. | P0 |
| Other `vector-effect` values | Defer | Add enum values only if preserved as unknown/custom or feature flag. | Do not implement first; spec marks as at-risk. | No change. | No change. | P3 |
| Render tree and painters model | Add | No direct public API beyond computed style. | Explicit render-tree builder filters display/conditions/resources and orders painting. | Existing commands mostly enough. | No direct change. | P0 |
| Stacking contexts, opacity, isolation, blend mode | Keep/Add | Typed `SvgIsolation`, `SvgMixBlendMode` values for code contracts; parser accepts `isolation` and `mix-blend-mode` from CSS style sources only, not bare presentation attributes. | SaveLayer decisions centralized; group opacity/isolation/blend handled at scene nodes. | Existing `SaveLayerCanvasCommand` and `SKPaint.BlendMode`; maybe add isolation metadata if needed. | `SkiaModel` maps blend modes and layer paints. | P1 |
| `display`, `visibility`, `overflow` | Keep/Add | Ensure SVG 2 defaults through UA stylesheet. | Render tree inclusion and clipping semantics centralized. | Existing clip/save commands enough. | No direct change. | P0 |
| `<paint>` grammar and fallback | Keep/Add | Extend paint parser for `context-fill`, `context-stroke`, `currentColor`, URL fallback chains, `none`. | Paint resolver receives context paint and fallback chain. | Existing shader/color plus maybe context paint record is compile-time only. | No direct change. | P0 |
| `context-fill`, `context-stroke` | Partial | Parser/model support exists through context paint server/property handling. | Retained marker rendering and focused `use` cases supply context fill/stroke from the referencing element; broader resource propagation and fallback-chain parity remain open. | No new command if resolved before paint creation. | No direct change. | P0 |
| `paint-order` | Partial | `SvgPaintOrder` converter/property is implemented for retained rendering paths; invalid inheritance handling has been fixed. | Shape/path/text compiler emits fill, stroke, and marker phases in computed order in current retained paths. Broader computed-style integration and pixel coverage remain open. | Existing draw commands enough. | No direct change. | P0 |
| Stroke algorithm clarifications | Keep/Add | Accept SVG 2 value grammar, non-negative miterlimit. | Normalize stroke joins, dashes, zero-length subpaths, caps, marker positions. | Existing path effect maybe enough; add command tests. | Skia differences documented with focused thresholds. | P1 |
| `stroke-linejoin: arcs` | Defer | Preserve raw value in custom attributes if encountered. | Treat unsupported as fallback per CSS parsing rules. | No change. | No change. | P3 |
| Markers on all shapes | Add | Ensure `SvgMarkerElement` base covers shape classes or add marker partial properties to all applicable shapes. | Geometry service computes marker vertices/orientations for line, polyline, polygon, path, rect, circle, ellipse. | Existing draw commands enough. | No direct change. | P0 |
| `orient=auto-start-reverse` | Add | Extend `SvgOrient` converter/value type. | Marker placement flips only start marker in auto mode. | Existing matrix command enough. | No direct change. | P0 |
| Marker `marker` shorthand | Keep | Keep current documented distinction between raw presentation attr and CSS shorthand unless browser parity changes. | CSS marker shorthand expands through style path. | No direct change. | No direct change. | P0 |
| Gradient and pattern `href` templating | Keep/Add | Effective href on gradient/pattern/filter; preserve template inheritance rules. | Resource resolver handles template chains, child inheritance, cycle detection, external templates. | Existing shaders/picture shaders enough. | Asset loader resolves external SVG resources only when policy allows. | P0 |
| `radialGradient fr` | Keep/Add | Upstream already has `fr`; verify unnamespaced href and CSS parsing. | Gradient service uses two-point conical gradient when focal radius is non-zero. | Existing `TwoPointConicalGradientShader`. | `SkiaModel` already maps conical shaders; verify. | P0 |
| Mesh gradients, hatches, `solidcolor` | Defer | Preserve as unknown/custom elements and attributes. | No static SVG 2 core rendering; existing `GradientMesh` is not an SVG 2 commitment. | No change. | No change. | P3 |
| CSS Masking integration | Partial | `mask-type` coverage and href fallback paths are implemented in the current tranche; CSS `clip-path` grammar still needs audit. | Masking service has alpha/luminance coverage additions; object bbox, nested resource, and clip path parity still need focused tests. | Existing saveLayer/filter/clip commands likely enough; add mask metadata only if needed. | Skia adapter maps image filters/layers. | P1 |
| Filter Effects externalized from SVG 2 | Partial | `feDropShadow` and effective href on `filter`/`feImage` are implemented in reviewed paths. | Linked filter/filter `feImage` dependency handling is implemented; filter regions, color interpolation, result chaining, and cycle/dependency tests still need audit. | May need `DropShadowImageFilter` or compile to existing blur/offset/flood/composite sequence. | Skia adapter maps to image filters or software fallback. | P1 |
| `feDropShadow` | Partial | `[SvgElement("feDropShadow")]` and attrs `dx`, `dy`, `stdDeviation`, `flood-color`, `flood-opacity` are implemented. | Current retained expansion handles explicit missing inputs and outside-source-bounds colorization; broader Filter Effects parity remains open. | Add direct image filter record only if chain is inadequate. | Map direct record or chain to Skia. | P1 |
| CSS Color | Keep/Add | Color parser accepts CSS Color 3 forms required by SVG 2; future Color 4 behind flag. | Paint resolver uses normalized sRGB/linear RGB. | Existing `SKColor`, `SKColorF`, `SKColorSpace`. | Existing Skia conversion enough. | P1 |
| `image crossorigin` | Add | Parse and preserve `crossorigin`. | Asset loader policy receives cross-origin request metadata. | No direct change. | `SkiaSvgAssetLoader` can ignore by default but expose hook. | P2 |
| Embedded HTML/media elements | Defer | Preserve `foreignObject` children as `NonSvgElement`. | No core static rendering except possible rasterized plugin later. | Existing image/picture commands if raster fallback is supplied. | Host/plugin-specific only. | P3 |
| Text elements as graphics elements | Keep/Add | Ensure text/tspan/textPath accept graphics attrs like filter, mask, clip, opacity. | Text compiler wraps text draws in same effects pipeline as shapes. | Existing text commands plus saveLayer/clip. | Skia text shaping unchanged. | P1 |
| `white-space` replacing `xml:space` | Add | Add `SvgWhiteSpace` parser/property and map legacy `xml:space` into computed whitespace. | Text compiler performs CSS Text whitespace collapse/preserve behavior before shaping. | No direct change. | No direct change. | P1 |
| Auto-wrapped text: `inline-size`, `shape-inside`, `shape-subtract` | Add in later tranche | Add typed properties and shape references. | Requires text layout area/wrapping engine and possibly shape exclusion paths. | Existing text commands may be enough after line layout; no new primitive. | Skia text shaping used per line. | P2 |
| `text-overflow` | Add later | Add property and converter. | Needs clipped inline layout and ellipsis insertion. | Existing clip/text commands enough. | No direct change. | P2 |
| `textPath path` attribute | Partial | Inline `path` property on `SvgTextPath` is implemented. | Text compiler uses inline path before href path in the implemented retained path. | Existing `DrawTextOnPathCanvasCommand`. | Existing adapter if command supports path. | P1 |
| `textPath` href to basic shape | Partial | Effective href may target basic shapes, not only `SvgPath`. | Implemented for basic-shape targets; shared geometry service and pathLength normalization remain open. | Existing text-on-path command. | No direct change. | P1 |
| `textPath side` | Partial | `SvgTextPathSide` accepts `left` and `right`. | Focused static placement now preserves the path direction for `side=right` and passes the standalone WPT row; broader normal-side/baseline offset parity remains open. | Existing text commands remain sufficient for current retained path. | Skia adapter may still need baseline offset fallback for broader parity. | P2 |
| Closed-path text loop and `startOffset` clarification | Add | Existing `startOffset`; no public API change. | Measure one closed loop and normalize start offsets with pathLength. | Existing command enough if positions precomputed. | No direct change. | P2 |
| Removed text/font elements: `tref`, `altGlyph*`, SVG Fonts | Compat | Keep legacy parser support where present; mark not SVG 2 core. | Existing SVG font support remains compatibility setting, not SVG 2 requirement. | Existing path/text commands. | `EnableSvgFonts` remains compatibility flag. | P3 |
| WOFF/CSS font loading | Add later | Parse `@font-face` URLs through CSS compatibility processor. | Asset loader/font provider resolves WOFF when policy allows. | No new command. | Typeface providers load/register fonts if supported. | P2 |
| `getBBox(options)` and geometry measurement | Add internal first | Optional public helper later: `SvgElement.GetStaticBounds(SvgBBoxOptions)`. | Shared bounds service computes fill/stroke/markers/clipped boxes. | No change unless command-level bbox cache is added. | Expose through `SKSvg` only after service is stable. | P2 |
| Removed DOM APIs and `animVal` behavior | Compat | Do not add old path segment APIs as SVG 2 work; static `animVal` can alias base value if DOM facade needs it. | No rendering impact. | No change. | JavaScript facade remains separate. | P3 |

## Detailed Implementation Plan

### Phase 0: Guardrails and Baseline Inventory

1. Add project boundary guard tests, preferably in a new `ProjectBoundaryGuardTests` beside `tests/Svg.Model.UnitTests/ArchitectureGuardTests.cs`.
2. Guard these boundaries:
   - `Svg.Custom` must not reference `SkiaSharp`, `ShimSkiaSharp`, `Svg.Skia`, or host UI packages.
   - `ShimSkiaSharp` must not reference `SkiaSharp`, `Svg.Custom`, `Svg.Model`, `Svg.SceneGraph`, or `Svg.Skia`.
   - `Svg.Model` must not reference `Svg.Skia` or `SkiaSharp`.
   - `Svg.SceneGraph` must not reference `Svg.Skia` or `SkiaSharp`.
   - `Svg.Skia` remains the SkiaSharp adapter boundary.
3. Add a checked coverage document or generated test snapshot that records current support for every row in the feature matrix.
4. Create focused SVG 2 fixture naming convention:
   - parser/model: `Svg2Static*Tests`
   - scene graph: `SvgSceneGraphSvg2StaticTests`
   - pixel fixtures: `svg2-static-*.svg` only when a browser reference is available.

Acceptance:

- guard tests pass
- no SVG 2 feature code is added under `externals/SVG`
- current W3C/resvg suites still behave as before

### Phase 1: Parser and Style Foundation

1. Add `SvgProcessingMode`, `SvgExternalResourcePolicy`, `SvgDocumentLoadOptions`, and effective href helpers in `src/Svg.Custom`.
2. Extend `SvgStyleAttributeNames` for SVG 2 static properties.
3. Add typed/converter contracts:
   - `SvgPaintOrder`
   - `SvgContextPaintServer` or equivalent deferred paint identifiers
   - `SvgWhiteSpace`
   - `SvgTextPathSide`
   - `SvgIsolation`
   - `SvgMixBlendMode`
   - `SvgAutoLength` or a minimally invasive auto-capable unit wrapper where needed
4. Add local partial classes for SVG 2 attributes:
   - unnamespaced `href` on all href-bearing elements
   - `paint-order`, `white-space`, `transform-origin`, `transform-box`
   - `pathLength` on all basic shape families
   - `orient=auto-start-reverse`
   - `symbol` geometry/ref properties
   - `feDropShadow`
5. Add parser tests that assert:
   - `href` wins over `xlink:href`
   - unknown SVG 2 attrs are preserved
   - style/presentation attrs flow through the cascade path
   - SVG 2 mode ignores removed `requiredFeatures` render gating

Acceptance:

- `dotnet test tests/Svg.Model.UnitTests/Svg.Model.UnitTests.csproj -c Release --no-restore`
- no renderer behavior changes except intentional parser/computed-style snapshots

### Phase 2: Computed Style and Resource Resolution

Progress:

- Partial: `SvgParameters` now bridges load options/resource policy into the current load path.
- Partial: document state, runtime image policy, and nested SVG inheritance are wired for implemented resource paths.
- Partial: CSS `@import` is blocked when policy is `Disabled` or `SameDocumentAndDataOnly`, and same-origin file imports are confined under the document directory.
- Complete in current paths: effective href fixes include `PreferSvg2Href`, invalid href `Try*` APIs, and case-sensitive same-document href/id lookup.

1. Introduce a computed style cache in `Svg.Custom` or `Svg.SceneGraph` with a narrow public surface and internal fast path.
2. Move scene compilation call sites from raw attribute reads to computed values for:
   - paint
   - display/visibility/overflow
   - transform
   - geometry properties
   - text properties
   - marker properties
3. Centralize URL resolution:
   - effective `href`
   - base URI
   - same-document fragments
   - data URL handling
   - cycle detection
   - resource policy
4. Finish resource policy enforcement for:
   - same-origin checks
   - linked stylesheets beyond blocked CSS imports
   - fonts and `@font-face`
   - external SVG/image resources
   - host-control option exposure
5. Add UA stylesheet defaults required for SVG 2 rendering tree behavior.

Acceptance:

- existing CSS variable/currentColor tests continue to pass
- W3C rows with Chrome overrides continue to use browser-compatible behavior
- resource cycle tests cover gradients, patterns, filters, masks, clip paths, markers, use, and textPath

### Phase 3: Geometry, Paint, and Markers

Progress:

- Complete in retained shape/text paths: `paint-order` command ordering and invalid inheritance handling.
- Partial: `context-fill` and `context-stroke` parser/model support and retained marker rendering.
- Complete in current marker path: `orient=auto-start-reverse` model coverage and retained start-marker flip.

1. Add a shared geometry abstraction over paths and basic shapes.
2. Use it for:
   - shape path creation
   - shape length
   - `pathLength` scaling
   - markers
   - textPath targets
   - bbox options
   - hit testing
3. Finish `paint-order` integration through the centralized computed-style path and browser-reference coverage.
4. Finish `context-fill` and `context-stroke` in reused content, fallback chains, and inheritance edge cases.
5. Implement markers on all shape types and `auto-start-reverse`.
6. Harden `vector-effect: non-scaling-stroke` in bounds, hit testing, and Skia conversion.

Acceptance:

- semantic tests inspect shim command order for paint-order
- marker tests cover path, line, polyline, polygon, rect, circle, ellipse
- focused W3C marker and painting rows pass or retain documented browser-reference deltas

### Phase 4: Gradients, Patterns, Masks, and Filters

Progress:

- Complete in current paths: gradient/pattern effective href templating honors SVG 2 href precedence.
- Partial: linked filter/filter `feImage` dependency handling is implemented.
- Partial: `mask-type` coverage is added.
- Partial: `feDropShadow` model and retained filter behavior are implemented for the focused missing-input and outside-source-bounds cases; broader parity remains open.

1. Complete effective href template chains and dependency/cycle tests for gradients, patterns, filters, and `feImage`.
2. Verify `radialGradient fr` maps to two-point conical gradients.
3. Finish `mask-type` luminance/alpha parity and object-bounding-box mask semantics from CSS Masking.
4. Finish `feDropShadow` behavior as either:
   - an expansion to blur/offset/flood/composite/merge, preferred for shim portability, or
   - a direct shim image filter only if expansion is incorrect or too expensive.
5. Audit filter primitive regions and color-interpolation-filters against Filter Effects.

Acceptance:

- focused filter/mask unit tests assert filter graph behavior, not only pixels
- `resvgTests` filter/mask rows remain stable
- Chrome override generation is used only when upstream references diverge from browser behavior

### Phase 5: Text SVG 2 Static Work

Progress:

- Complete in current retained path: text `paint-order`.
- Complete in focused retained paths: inline textPath `path`, href-to-basic-shape support, closed-loop placement, `pathLength` scaling, focused `white-space: pre`, and the WPT `side=right` standalone row.

1. Add `white-space` handling and map legacy `xml:space`.
2. Ensure text, tspan, and textPath can receive graphics effects through the same scene pipeline as shapes.
3. Finish broader textPath SVG 2 parity:
   - baseline/offset parity across curved and transformed paths
   - additional side/normal placement coverage beyond the standalone WPT row
   - graphics effects and computed-style integration on textPath content
4. Add auto-wrapped text in a later tranche:
   - `inline-size`
   - `shape-inside`
   - `shape-subtract`
   - `text-overflow`

Acceptance:

- resvg skipped textPath SVG 2 rows are reviewed one-by-one
- text tests prefer semantic layout assertions before pixel thresholds
- browser captures are created through HTTP only

### Phase 6: Images, Fonts, and Embedded Content

1. Add intrinsic image sizing for auto width/height.
2. Preserve and pass `crossorigin` to the asset loader policy hook without enforcing browser network policy in core.
3. Add WOFF loading through typeface providers only after a CSS `@font-face` resolver is in place.
4. Keep SVG Fonts as a compatibility feature behind existing settings, not SVG 2 static support.
5. Keep `foreignObject` static rendering deferred unless a separate host/static HTML renderer is designed.

Acceptance:

- asset loader tests cover intrinsic dimensions, data URLs, external policy, and blocked secure-static refs
- no host UI dependency enters `Svg.Custom`, `Svg.Model`, or `Svg.SceneGraph`

### Phase 7: Public Measurement APIs

1. Introduce internal `SvgBBoxOptions` equivalent first:
   - fill
   - stroke
   - markers
   - clipped
2. Use the same service for tests, editor selection, hit testing, and optional public API.
3. Expose public `SKSvg` helper only after geometry service is stable:
   - `TryGetElementBounds(string id, SvgBoundsOptions options, out SKRect bounds)`
   - `TryGetElementGeometryPath(string id, out SKPath path)` if needed by editor tooling

Acceptance:

- no duplicate bbox logic between editor, hit testing, and renderer
- non-scaling stroke and markers are included when requested

## Test Strategy

Use semantic tests first, browser pixels second.

| Gate | Command |
|---|---|
| Parser/model focused | `dotnet test tests/Svg.Model.UnitTests/Svg.Model.UnitTests.csproj -c Release --no-restore` |
| SVG 2 static focused | `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~Svg2Static"` |
| Scene graph focused | `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~SvgRetainedSceneGraphTests"` |
| W3C focused | `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~W3CTestSuiteTests.Tests"` |
| resvg focused | `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~resvgTests"` |
| WPT SVG 2 focused | `dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj -f net10.0 -c Release --no-restore --filter "FullyQualifiedName~WptSvg2StaticSubsetTests"` |
| Full pre-merge | `dotnet format Svg.Skia.slnx --no-restore && dotnet build Svg.Skia.slnx -c Release && dotnet test Svg.Skia.slnx -c Release` |

Chrome override policy:

- Generate W3C overrides with `node scripts/capture_w3c_chrome_overrides.mjs <comma-separated-tests>`.
- Generate resvg overrides with `node scripts/capture_resvg_chrome_overrides.mjs <comma-separated-tests>`.
- Generate WPT SVG 2 references with `node scripts/capture_wpt_svg2_chrome_references.mjs <comma-separated-svg-paths>`.
- Do not use `file://` for W3C fixtures.
- Do not reintroduce W3C footer exclusion regions.
- Keep skipped rows skipped when they require scripting, live DOM, browser runtime behavior, or unsupported dynamic media.

## Rollout Order

1. Land guardrails and parser contracts.
2. Land effective href and computed style foundation.
3. Land paint-order, context paint, marker expansion, and basic-shape geometry.
4. Land gradient/pattern/filter/mask conformance slices.
5. Land textPath and whitespace slices.
6. Land image/font policy slices.
7. Land optional bbox/public measurement APIs.

Each phase should end with:

- focused semantic tests
- minimal pixel tests for the changed behavior
- updated implementation status in this file or a follow-up status document
- no edits in `externals/SVG` unless the PR explicitly targets the upstream submodule

## Open Decisions

| Decision | Default recommendation |
|---|---|
| Where should `SvgDocumentLoadOptions` live? | Put the core type in `Svg.Custom`; let `Svg.Model.SvgParameters` carry or translate it so controls do not depend on renderer details. |
| Should unknown SVG elements render as containers? | Preserve first, render later behind a browser-compatibility feature flag because SVG 2 marks `SVGUnknownElement` behavior at-risk. |
| Should auto-wrapped text be in the first release? | No. Ship `white-space` and textPath SVG 2 deltas first; auto-wrapping is a separate text layout project. |
| Should `feDropShadow` be a direct shim primitive? | Prefer expansion to existing filter primitives first; add direct shim support only if expansion is incorrect or too slow. |
| Should mesh gradients/hatches be implemented? | No for SVG 2 static core. Preserve markup and keep existing `GradientMesh` separate from SVG 2 conformance. |
| Should SVG Fonts be removed? | No. Keep existing compatibility behavior behind settings, but do not count it as SVG 2 support. |
