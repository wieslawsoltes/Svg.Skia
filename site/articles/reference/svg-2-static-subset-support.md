---
title: "SVG 2 Static Subset Support"
---

# SVG 2 Static Subset Support

This article documents the current SVG 2 static subset implemented in `Svg.Custom`, `Svg.Model`, `Svg.SceneGraph`, `ShimSkiaSharp`, `Svg.Skia`, and the inline `SvgML.Avalonia`, `SvgML.Maui`, and `SvgML.Uno` authoring packages. It focuses on features that are parsed, preserved, authored, or rendered for static output, and it calls out SVG 2 features that are scaffolded or explicitly deferred.

The normative reference is the W3C [SVG 2 Candidate Recommendation](https://www.w3.org/TR/SVG2/). SVG 2 builds on [SVG 1.1 Second Edition](https://www.w3.org/TR/SVG11/) and changes several areas relevant to static rendering, including processing modes, href handling, geometry properties, painting, text, and the split of masking/filter behavior into related CSS specifications.

## Scope Notes

| Topic | Contract |
|---|---|
| Static SVG 2 | This page covers SVG 2 additions that affect static pixels, resource loading, metadata, or retained inspection in a loaded SVG document. |
| SVG 1.1 baseline | SVG 1.1 support remains documented in [SVG 1.1 Static Subset Support](svg-11-static-subset-support). This page only records deltas and compatibility behavior that matter for SVG 2. |
| Runtime behavior | Dynamic interactive processing, live DOM APIs, CSSOM, selection/editing, browser navigation, script execution, media playback, and broad animation timelines are outside the static subset. |
| Retained scene graph | `Svg.SceneGraph` is the implementation/API surface used to render, inspect, hit-test, and incrementally refresh static SVG content. It is not a separate SVG language feature. |
| Non-SVG inputs | Android VectorDrawable XML is supported through separate import/conversion APIs. Its Android attribute contract is not part of SVG 2 static support. |

## Status Legend

| Status | Meaning |
|---|---|
| Supported | Implemented and used by static rendering paths. |
| Partial | Implemented for selected paths or parser/model contracts, with known gaps. |
| Compatibility | Parsed or preserved for compatibility with older SVG content, but not a new SVG 2 rendering requirement. |
| Deferred | Explicitly not implemented in the current static subset. |
| Runtime | Belongs to scripting, animation, interaction, or host behavior rather than static rendering. |

## Processing And Resource Contracts

| Feature | Status | Current contract |
|---|---|---|
| Processing modes | Supported for static subset | `SvgProcessingMode` and `SvgDocumentLoadOptions` represent static, secure static, animated, secure animated, and dynamic interactive modes. `SvgParameters` carries load options into document state and resource loading paths. Secure static caps broad external-resource policies to same-document/data behavior in covered static paths. |
| External resource policy | Supported for static subset | `SvgExternalResourcePolicy` supports `Enabled`, `SameOrigin`, `SameDocumentAndDataOnly`, and `Disabled`. Image, nested SVG/SVGZ, data URI, CSS `@import`, and external SVG reference paths use policy checks in covered paths. |
| Unknown elements and attributes | Partial | `PreserveUnknownElements` exists on load options, custom attributes remain preserved, and unknown SVG content is retained as model data rather than rendered as SVG graphics. The option is a contract surface, not a complete policy switch across every load path yet. |
| Removed/deprecated SVG 1.1 switches | Compatibility | `version`, `baseProfile`, `requiredFeatures`, and `externalResourcesRequired` can be parsed or preserved for compatibility. SVG 2 static rendering is not blocked by `requiredFeatures`. |
| Host/load-option surface | Partial | Core loaders accept `SvgParameters` and `SvgDocumentLoadOptions`. `SvgML.Avalonia`, `SvgML.Maui`, and `SvgML.Uno` root controls expose `ProcessingMode`, `ExternalResources`, `PreserveUnknownElements`, and `PreferSvg2Href`, and pass them through the shared load pipeline. Avalonia and other source-based host wrappers preserve options through selected source/cache paths and expose CSS/current-color conveniences, but not every host control exposes the full load-option contract yet. |

## Href And Linking

| Feature | Status | Current contract |
|---|---|---|
| Effective `href` | Supported | `TryGetEffectiveHref` and `TryGetEffectiveHrefString` make unnamespaced `href` win over `xlink:href` by default. Reviewed static paths use effective href for use, text refs/textPath, images, gradients, patterns, filters, filter images, clips, and masks. |
| Empty and invalid `href` | Supported | Empty unnamespaced `href` remains authoritative and does not fall back to `xlink:href`. Invalid URI conversion uses `Try*` APIs and reviewed paths fail closed instead of throwing. |
| Programmatic href changes | Supported | Parsed raw href state is compared against typed property state, and typed property edits after parse are reflected by effective href lookup. |
| Link navigation | Runtime | `<a>` can preserve link metadata and group content, but navigation is host-owned and not part of static rendering. |

## CSS And Style Features

| Feature | Status | Current contract |
|---|---|---|
| SVG 2 style recognition | Partial | `SvgStyleAttributeNames` includes SVG 2 static properties and keeps `isolation` and `mix-blend-mode` CSS-only. The CSS processor applies recognized static style properties, and the centralized computed-style snapshot backs selected SVG 2 properties used by the static renderer. |
| CSS custom properties | Partial | CSS variables are applied during load for supported properties, including media-qualified rules in covered paths. Full CSS cascade/CSSOM behavior is out of scope. |
| `@media` and media imports | Partial | `@media` blocks and media-qualified `@import` rules are evaluated against a screen-like static SVG viewport context. Unsupported or non-matching media are ignored. |
| Static pseudo-classes | Partial | Static forms such as `:root`, `:link`, and `:lang()` have focused handling. Interactive pseudo-classes such as `:hover`, `:active`, `:focus`, and `:visited` never match because there is no live event state during loading. |
| CSS-only compositing properties | Partial | `SvgIsolation` and `SvgMixBlendMode` expose typed values from CSS style sources. Retained scene nodes create save layers and blend paints for supported CSS paths. Bare presentation-attribute forms are intentionally ignored. |
| `paint-order` | Supported for static subset | `SvgPaintOrder` parses `normal` and fill/stroke/markers orders and rejects invalid duplicates. Retained paths, shapes, markers, and text use paint order in covered static renderer paths. |
| CSS transform properties | Partial | `transform-box` and `transform-origin` are recognized/preserved as SVG 2 transform style properties. Origin-aware transforms are applied for covered retained leaf nodes, text nodes, structural nodes, symbol references, and `<use>` references when geometry/reference bounds are available. Full standalone CSS transform property integration is future work. |

## Geometry And Document Features

| Feature | Status | Current contract |
|---|---|---|
| `symbol` geometry | Partial | `SvgSymbol` has `x`, `y`, `width`, `height`, `refX`, and `refY`. Direct symbol reference paths use width/height in retained compilation; full `refX`/`refY` reference-point semantics remain open. |
| Basic shape `pathLength` | Supported for static subset | `pathLength` is available on path-based shapes. Covered retained paths normalize dash distances for paths/basic shapes and use author path lengths for textPath distance mapping. |
| Styleable geometry properties | Supported for covered geometry | Computed `x`, `y`, `width`, `height`, `cx`, `cy`, `r`, `rx`, `ry`, and line coordinate properties feed covered retained geometry and image sizing paths. Invalid declarations are ignored so attribute fallbacks remain in effect. |
| `d` as a CSS property | Supported for covered paths | CSS `d` values, including `d: none`, are treated as authoritative when authored. Invalid computed `d` values fail closed instead of falling back to the attribute. |
| Equivalent path geometry for shapes | Partial | Shape-to-path conversion exists in shared services and is used by rendering, clipping, hit testing, focused marker paths, and selected textPath shape support. A shared geometry abstraction for all consumers is not complete. |
| Marker support on all shapes | Partial | Marker properties exist and common path-like cases render. Focused retained coverage includes path and basic-shape equivalent geometry; exhaustive marker vertices/orientation for every shape edge case remains open. |
| Marker `orient="auto-start-reverse"` | Supported | Retained marker compilation applies SVG 2 start-marker reversal for supported marker paths. |
| `ellipse` auto radii | Deferred | Numeric SVG 1.1 radii render. SVG 2 `auto` radius resolution is not complete. |
| `svg` and `image` auto sizing | Partial | Explicit sizes, viewBox behavior, CSS-authored image dimensions, and intrinsic fallback are supported in covered paths. Full SVG 2 `auto` sizing semantics for every viewport/resource variant remain open. |
| `foreignObject` geometry | Partial | Geometry is preserved and SvgML packages provide platform native-control integrations. Core Skia rendering does not lay out browser HTML/CSS. |

## Text Features

| Feature | Status | Current contract |
|---|---|---|
| `paint-order` on text | Partial | Text elements inherit/use `SvgPaintOrder`, and retained text orders fill, stroke, and decorations in focused paths. More pixel coverage for complex styled runs is still useful. |
| `textPath path` | Supported for static subset | `SvgTextPath.PathData` parses inline path data, and inline path data wins over href in retained textPath rendering. Covered retained paths handle pathLength distance scaling, closed-loop start offsets, and open-path clipping. |
| `textPath` href to basic shapes | Supported for static subset | Effective href can resolve direct visual shape targets, and covered retained support converts basic shape targets for text-on-path rendering. |
| `textPath side` | Supported for static subset | `SvgTextPathSide` supports `left` and `right`; retained draw, clip, and measure paths reverse sampled geometry and mirror offsets for `side="right"`. |
| `textPath method` | Partial | `method="align"` uses positioned glyph placement. `method="stretch"` has a guarded static retained path that converts text outlines to paths and warps outline points along the sampled textPath, including HarfBuzz-shaped glyph-outline extraction when the Skia asset loader is available, open-path endpoint clamping for overhanging outlines, `lengthAdjust="spacingAndGlyphs"` outline scaling, cluster-distributed `lengthAdjust="spacing"` over shaped/fallback clusters, letter/word spacing in stretched cluster offsets, textPath-owned textLength applied through nested span content, fill/stroke paint-order, warped decorations, and retained filter save-layer support. Exact browser raster parity for all fallback/color-font/complex-script combinations remains open. |
| `white-space` | Partial | `SvgWhiteSpace` parses SVG 2 whitespace values, focused retained text preserves `white-space: pre` runs, and guarded inline-size layout resolves whitespace per text run, preserves forced breaks for `pre-line`, leading/trailing spaces for `pre-wrap`, break opportunities between preserved spaces for `break-spaces`, recognizes zero-width space, soft hyphen, CJK, dash and common punctuation break opportunities, honors bounded `overflow-wrap`, `word-break`, `line-break`, and `line-height` inputs, and suppresses wrapping at NBSP, figure space, narrow NBSP, word joiner, BOM, explicit bidi formatting spans, and no-break format controls. Full UAX #14 and dictionary-based CSS Text line breaking remains open. |
| `inline-size` | Partial | Sequential text supports a guarded static subset: single-line content-area anchoring for start/middle/end, `white-space: normal`, `pre-line`, `pre-wrap`, and `break-spaces` wrapping inputs, selected CSS Text break opportunities, horizontal LTR/RTL line placement with reusable Unicode-bidi run resolution for inherited CSS `direction`, `unicode-bidi: embed`, `isolate`, `bidi-override`, `isolate-override`, `plaintext`, explicit bidi controls, and mixed-direction DOM metrics, legacy `tb`/`vertical-rl` leftward column wrapping, `vertical-lr` rightward column wrapping, CSS vertical `direction=rtl` bottom-to-top inline advance, matching text DOM metrics for supported wrapped lines without generated overflow markers, and fallback to the existing text path for unsupported cases. Root horizontal LTR `inline-size` plus `textLength` supports guarded flattened placement and line-wrapped `lengthAdjust=spacing`/`spacingAndGlyphs` cases, including cumulative relative `dx`/`dy` descendants, absolute positioned descendant composition for the enabled horizontal cases, preserved explicit-line textLength spacing, nested textLength ownership, natural-line-proportional root textLength distribution, and DOM metrics. Direct, styled-wrapper, and guarded mixed-sibling `textPath` content can use the inline content start with matching DOM metrics, a single root `dx`, textPath-owned `textLength`, root `textLength` distributed across mixed normal-text/textPath siblings, and wrapping of following sibling text. URL-referenced and basic-shape `shape-inside` regions can also provide ordered content areas and take precedence over `inline-size` when both are present. Full browser-grade Unicode Bidi/CSS Text parity across nested element embeddings, all neutral/weak-class edge cases, vertical mixed-direction wrapping, vertical/RTL wrapped textLength, vertical `textPath` wrapping, multiline browser textPath parity, and browser-grade layout parity remain open. |
| `shape-inside`, `shape-subtract` | Partial | Typed `shape-inside`, `shape-subtract`, `shape-padding`, `shape-margin`, and `shape-image-threshold` properties exist on `SvgElement` and are preserved for model/style contracts. Retained rendering supports a guarded sampled subset: ordered URL references and mixed/repeated basic CSS shapes (`inset`, rounded `inset`, `circle`, `ellipse`, `polygon`) provide content intervals, overflow moves to the next shape when the current shape is exhausted, finite shape bounds stop wrapped lines, and valid `shape-subtract` references or basic shapes remove sampled exclusion fragments while invalid subtract entries are ignored. `shape-padding` and `shape-margin` are non-inherited non-negative lengths, off-center `closest-side`/`farthest-side` circle/ellipse radii are resolved, rounded inset radii use CSS border-radius shorthand including `/`, polygon fill-rule prefixes are accepted, referenced path fill-rules can create sampled holes, shape-box keywords use SVG text geometry with `content-box`/`padding-box` mapping to fill geometry and `border-box`/`margin-box`/`stroke-box` mapping to stroke geometry, viewport fallback remains for oversized basic shapes, image `url(...)` shapes can derive alpha intervals with `shape-image-threshold` through Skia-backed decoded images or the PNG fallback sampler including indexed palette transparency, and same-line exclusion fragments remain available to shared wrapped `textLength` layout. Matching text DOM metrics cover the guarded shape subset, including vertical and RTL line-area resolution. Floats, exact CSS image-rendering semantics, non-SVG CSS layout-box parity, exact browser line breaking, and complete CSS Shapes parity remain open. |
| `text-overflow` | Partial | Sequential inline-size text supports `clip`, `ellipsis`, and quoted custom marker strings in the guarded path, including overflowing wrapped lines and the supported horizontal shape text area. Text DOM metrics keep source text semantics by avoiding the generated-marker layout path. Full CSS Text overflow semantics, exact RTL marker placement, and vertical marker parity remain open. |
| `selectSubString` selection painting | Partial | JavaScript text selection APIs validate through the text DOM metrics path, record immutable logical selection extents, and can bake a static selection highlight path into retained output when `SKSvgSettings.EnableTextSelectionRendering` is enabled. `TextSelectionColor` controls the highlight fill, and post-load selection calls refresh the retained picture. Exact browser UI selection behavior, visual-order bidi selection painting, interactive selection handles, and browser focus/caret policy remain outside the static subset. |
| Removed SVG 1.1 text features | Compatibility | `tref`, SVG fonts, and focused `altGlyph`/`glyphRef`/`altGlyphDef` substitution are retained for compatibility where supported. They are not new SVG 2 core requirements. Exact browser raster identity for legacy SVG-font `altGlyph` rows remains a parity target rather than a core SVG 2 requirement. |

## Paint, Masking, And Filter Features

| Feature | Status | Current contract |
|---|---|---|
| `<paint>` grammar additions | Partial | `context-fill` and `context-stroke` parse as paint servers. Retained marker and focused `<use>` paths resolve context paint, including selected URL fallback chains; broader referenced-content parity remains open. |
| `currentColor` | Supported | Paint server and color handling support inherited current color in fill/stroke/paint paths. Continued coverage with CSS variables and inherited color combinations is useful. |
| URL paint fallback | Partial | Deferred and fallback paint-server types exist, common URL paint references work, and selected context-paint fallback chains are covered. Complex SVG 2 fallback-chain behavior still needs audit. |
| `radialGradient fr` | Partial | The property is exposed by generated/model surfaces, and retained/model gradient creation maps non-zero focal radius to two-point conical shaders in verified Chrome-backed rows. Broader inheritance, transform, and CSS coverage still needs audit before marking full support. |
| `mask-type` | Partial | `MaskType` supports alpha and luminance, retained masks choose alpha or luminance conversion in focused paths, and stylesheet `mask-type` can override presentation attributes. Broader CSS Masking parity and object-bounding-box tests remain open. |
| Linked filters | Partial | Effective href applies to filters, retained resource dependency tracking includes linked filters, and focused coverage includes region inheritance, dependency refresh, and cycle handling. Full filter parity still needs audit. |
| `feImage` href and dependencies | Partial | Focused retained `feImage` paths cover data SVG and selected local/external raster or SVG inputs with nested scene compilation and cycle guards. Broader `feImage` parity remains incomplete, with multiple resvg cases intentionally skipped. |
| `feDropShadow` | Partial | `SvgDropShadow` models `dx`, `dy`, `stdDeviation`, `flood-color`, `flood-opacity`, and inherited primitive attributes. Retained filters expand drop shadow through alpha, blur, offset, colorization, and merge in supported paths. |
| CSS compositing and blending | Partial | `mix-blend-mode` and `isolation` parse from CSS style sources. Retained rendering uses save layers and blend modes in supported paths. |
| CSS Color beyond SVG 1.1 | Partial | Current color parsing covers practical SVG/CSS color forms and sRGB/linear RGB behavior in supported paint/filter paths. CSS Color 4 and full color management are not part of this tranche. |

## Resource Loading Policies

| Policy | Current behavior |
|---|---|
| `Enabled` | Allows supported external resources subject to existing resolver and asset loader behavior. This is the default. |
| `SameOrigin` | Allows same-document references, data URIs, same-origin network resources, and local file resources under the SVG document directory. Local file confinement avoids treating all `file:` URLs as one broad origin. |
| `SameDocumentAndDataOnly` | Allows fragment references and data URIs; blocks external files/network resources in covered paths, including CSS imports and image loads before asset loading. |
| `Disabled` | Allows same-document references; blocks data/external fetches in covered paths. Secure-static parity is broader than the current implementation. |

## Deferred SVG 2 Areas

| Area | Current disposition |
|---|---|
| Secure static processing completeness | Contracts exist; linked stylesheets, fonts, host exposure, and complete nested-resource inheritance need more work. |
| Central computed style cache | Active for selected static properties. Broader cleanup can still consolidate remaining geometry, paint, filter, and text reads. |
| Shared geometry service | Expanded for CSS geometry, equivalent paths, marker placement, textPath targets, and pathLength normalization. Bbox options and exhaustive geometry consumers remain future work. |
| Full SVG 2 text layout | Full browser-grade Unicode Bidi/CSS Text integration, browser-grade line breaking, complete positioned descendants inside wrapping, mixed/nested/vertical textPath-in-wrapping, complete vertical/RTL shape layout, exact shape DOM metrics, baseline/offset parity, exact browser raster parity, and graphics effects on text content are not complete. The shared text layout engine is active for guarded `inline-size`, shape, textPath, textLength, bidi, line-break, and DOM-metric paths. Focused guarded `inline-size` wrapping now covers horizontal LTR/RTL and vertical column progression, selected CSS Text controls, reusable bidi run resolution for inherited CSS `direction`/`unicode-bidi` root modes, simple wrapped `textLength` including `spacingAndGlyphs` and forced pre-line breaks, relative positioned descendants, direct-child and mixed-sibling `textPath` starts with guarded textPath-owned/root `textLength`, and cluster-aware complex-script stretch smoke coverage. `side=right`, closed-loop textPath, and `pathLength` textPath rows are covered separately. `selectSubString` records a logical text selection range with layout-backed extents for JavaScript hosts and can render a configurable retained static highlight; exact browser visual-order selection UI remains runtime parity work. Benchmark scenes track the current retained output of vertical RTL, vertical/RTL shape layout, wrapped `textLength` plus positioned descendants, and complex-script stretch so regressions are visible while exact browser parity remains open. |
| Full symbol reference-point semantics | `refX` and `refY` are parsed but not fully rendered. |
| Complete context paint | Marker, `<use>`, text, currentColor, and selected fallback chains work in covered static paths; broader resource/context inheritance edge cases remain open. |
| Full masking/filter parity | Focused `mask-type`, `feDropShadow`, and `feImage` paths exist; full CSS Masking and Filter Effects parity is not complete. |
| Mesh gradients, hatches, `solidcolor` | Deferred or preserve-only. |
| `stroke-linejoin: arcs` | Deferred; current rendering falls back to existing line-join behavior. |
| Other at-risk `vector-effect` values | Deferred; unsupported values fall back to existing stroke behavior. |
| Dynamic interactive behavior | Scripting, live DOM APIs, CSSOM, event processing, selection/editing, runtime media, and broad animation timelines are outside this static subset. |

## Implementation Evidence

| Layer | Main contracts |
|---|---|
| `Svg.Custom` | `SvgDocumentLoadOptions`, `SvgProcessingMode`, `SvgExternalResourcePolicy`, `SvgElementHrefExtensions`, `SvgPaintOrder`, `SvgContextPaintServer`, `SvgDropShadow`, `SvgText2Properties`, `SvgTransform2Properties`, `SvgSymbol.Svg2`, `SvgPathBasedElement.Svg2`, and expanded style attribute recognition. |
| `Svg.Model` | `SvgParameters` load-option bridge, resource policy checks, URI/image loading, path/paint/mask/filter helpers, and `MaskType`. |
| `Svg.SceneGraph` | Retained use/resource expansion, effective href dependency tracking, textPath additions, SVG 2 inline-size/text-overflow rendering, guarded sampled shape-inside/shape-subtract text layout, paint-order rendering, context-paint marker state, retained masks, retained filters, `feDropShadow`, linked filter and `feImage` dependency handling. |
| `ShimSkiaSharp` | Renderer-neutral path, paint, shader, image, filter, save-layer, clip, and text commands used by current SVG 2 slices. |
| `Svg.Skia` | Converts shim commands to SkiaSharp, hosts `SKSvg` picture/model lifecycle, and provides interaction dispatch around retained static content. |
| SvgML packages | `SvgML.Avalonia`, `SvgML.Maui`, and `SvgML.Uno` expose inline XAML authoring for the SVG 2 static subset, including root load options, shape `pathLength`, symbol geometry, `paint-order`, `vector-effect`, transform properties, `white-space`, preserve-only text properties, `mask-type`, `feDropShadow`, `textPath path`, and `textPath side`. |
| Host controls | Preserve and clone load options through current source/cache paths and expose selected CSS/current-color/runtime conveniences; broader public host exposure remains planned. |

## Test Coverage

| Test source | Coverage signal |
|---|---|
| `Svg2StaticHrefTests` | Effective href precedence, empty href behavior, invalid URI handling, and programmatic href updates. |
| `Svg2StaticResourcePolicyTests` | External resource policies for covered image, nested SVG, SVGZ, data URI, and CSS import paths. |
| `Svg2StaticStyleContractTests` | SVG 2 style recognition, CSS-only property behavior, and style contract expectations. |
| `Svg2StaticSubsetAttributeTests` | SVG 2 parser/model attributes, preserve-only/deferred element contracts, dynamic/interactive preservation boundaries, unsupported vector-effect fallback, and stroke-linejoin compatibility. |
| `SvgContextPaintServerTests` and retained scene tests | Context paint, paint order, marker behavior, masks, filters, `feDropShadow`, `feImage`, and dependency tracking in focused renderer paths. |
| `SvgRetainedSceneGraphTests` SVG 2 text rows | Focused retained coverage for guarded horizontal LTR/RTL and vertical `inline-size`, vertical RTL column behavior, `text-overflow`, normal/pre-line/pre-wrap/break-spaces wrapping inputs, middle/end content-area anchoring, direct inline-size textPath starts/root `dx`/textPath-owned `textLength`, mixed-sibling inline-size textPath root `textLength`, `inline-size` plus root `textLength` with flattened and wrapped horizontal LTR spacing/scaling cases including relative positioned descendants, tspan word grouping, nested textPath ignored-child/fallback behavior, sampled `shape-inside`/`shape-subtract`, basic CSS shape functions and shape-box keywords, PNG alpha image-shape thresholding, shape padding/margin, multiple exclusions, non-rectangular and vertical/RTL shape intervals, `method="stretch"` paint/filter/decoration and `spacingAndGlyphs` scaling paths, tiny-coordinate textPath stability, and explicit skipped rows for full browser stretch parity. |
| `SvgSceneTextCompilerTests` SVG 2 text rows | Focused compiler coverage for resolved `tref` text content metrics, wrapped/pre-line/pre-wrap/break-spaces horizontal LTR/RTL and vertical `inline-size` text DOM metrics, direct, styled-wrapper, and guarded mixed-sibling inline-size textPath DOM metrics with root `dx`, textPath-owned/root `textLength`, and root `textLength` across mixed normal-text/textPath siblings, no-break/figure-space wrapping suppression, CJK, zero-width/soft-hyphen, dash, and explicit bidi/no-break-control wrapping behavior, CSS vertical RTL bottom-to-top positions with absolute X anchoring, descendant/empty `pre-line` and descendant `break-spaces` behavior, root `textLength` DOM metrics under `inline-size` with flattened and wrapped horizontal LTR spacing/scaling plus relative and absolute positioned descendant coverage, source-character DOM metrics for generated overflow marker cases, conservative vertical writing-mode DOM positions/rotation, and guarded shape layout DOM metrics including mixed basic shape lists, off-center basic-shape radii, invalid subtract entries, non-inherited shape spacing, fill-rule holes, shape-box keyword geometry/fallback behavior, PNG alpha image-shape thresholding, and style-applied shape spacing. |
| `SvgTextRegressionValidationBenchmarks` | Generated text benchmark scenes cover existing text layout paths plus remaining-gap inputs for vertical RTL, vertical/RTL shape layout, wrapped `textLength` with positioned descendants, and complex-script `textPath method="stretch"`; validation checks retained command counts, command-kind coverage, required text/path/layer source element IDs, stretch path output from the intended textPath elements, and required DOM-metric source IDs for supported gap scenarios, while recording and non-zero checking command, DOM, render, and end-to-end load/render checksums for BenchmarkDotNet regression tracking. Text command checksums include source IDs, textPath path geometry, clip-path geometry, save-layer paint signatures, and stretched glyph path geometry. |
| `SvgAllAreaRegressionValidationBenchmarks` | Generated combined standards-area scene covers text, paths, gradients, patterns, clips, masks, filters, markers, symbols, and grouped rendering; validation checks retained command counts and command-kind coverage for text/path/clip/layer output and required text source element IDs, while recording and non-zero checking command-model checksums that include source IDs, clip-path geometry, save-layer paint signatures, render checksums, and end-to-end checksums. |
| `Svg2StaticSubsetRenderingContractTests` | Renderer fallback contracts for unknown SVG elements, unsupported vector-effect values, and deferred `stroke-linejoin: arcs`. |
| WPT SVG 2 static subset | Focused Web Platform Tests rows for geometry properties, the `d` property, `pathLength`, context paint, paint order, paint-server fallbacks, radial gradient `fr`, textPath additions, `use` sizing, path bearing commands, and selected style-origin rendering. The active SVG-only rows compare against checked Chrome references and stay enabled; unsupported browser-runtime rows are outside the static subset instead of being tracked as skipped WPT rows. |
| W3C/resvg rows | Broader static rendering references. Browser-only, dynamic, or not-yet-supported rows remain skipped with explicit reasons. |

## Related Articles

- [SVG 1.1 Static Subset Support](svg-11-static-subset-support)
- [Rendering Pipeline](../concepts/rendering-pipeline)
- [Source Formats and Assets](../concepts/source-formats-and-assets)
- [API Coverage Index](api-coverage-index)
