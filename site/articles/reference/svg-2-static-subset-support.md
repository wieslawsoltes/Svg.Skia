---
title: "SVG 2 Static Subset Support"
---

# SVG 2 Static Subset Support

This article documents the current SVG 2 static subset implemented in `Svg.Custom`, `Svg.Model`, `Svg.SceneGraph`, `ShimSkiaSharp`, and `Svg.Skia`. It focuses on features that are parsed, preserved, or rendered for static output, and it calls out SVG 2 features that are scaffolded or explicitly deferred.

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
| Parsed | Parsed/preserved as an API/model contract but not fully used by rendering. |
| Compatibility | Parsed or preserved for compatibility with older SVG content, but not a new SVG 2 rendering requirement. |
| Deferred | Explicitly not implemented in the current static subset. |
| Runtime | Belongs to scripting, animation, interaction, or host behavior rather than static rendering. |

## Processing And Resource Contracts

| Feature | Status | Current contract |
|---|---|---|
| Processing modes | Partial | `SvgProcessingMode` and `SvgDocumentLoadOptions` represent static, secure static, animated, secure animated, and dynamic interactive modes. `SvgParameters` carries load options into document state and resource loading paths. Secure-static enforcement and host exposure remain incomplete. |
| External resource policy | Partial | `SvgExternalResourcePolicy` supports `Enabled`, `SameOrigin`, `SameDocumentAndDataOnly`, and `Disabled`. Image, nested SVG/SVGZ, data URI, and CSS `@import` paths use policy checks in covered paths. |
| Unknown elements and attributes | Partial | `PreserveUnknownElements` exists on load options, custom attributes remain preserved, and unknown content is generally retained as compatibility data rather than rendered as SVG graphics. The option is a contract surface, not a complete policy switch across every load path yet. |
| Removed/deprecated SVG 1.1 switches | Compatibility | `version`, `baseProfile`, `requiredFeatures`, and `externalResourcesRequired` can be parsed or preserved for compatibility. SVG 2 static rendering is not blocked by `requiredFeatures`. |
| Host/load-option surface | Partial | Core loaders accept `SvgParameters` and `SvgDocumentLoadOptions`. Avalonia and other host wrappers preserve options through selected source/cache paths and expose CSS/current-color conveniences, but not every host control exposes the full load-option contract yet. |

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
| SVG 2 style recognition | Partial | `SvgStyleAttributeNames` includes SVG 2 static properties and keeps `isolation` and `mix-blend-mode` CSS-only. The CSS processor applies recognized static style properties; a centralized computed-style snapshot is still deferred. |
| CSS custom properties | Partial | CSS variables are applied during load for supported properties, including media-qualified rules in covered paths. Full CSS cascade/CSSOM behavior is out of scope. |
| `@media` and media imports | Partial | `@media` blocks and media-qualified `@import` rules are evaluated against a screen-like static SVG viewport context. Unsupported or non-matching media are ignored. |
| Static pseudo-classes | Partial | Static forms such as `:root`, `:link`, and `:lang()` have focused handling. Interactive pseudo-classes such as `:hover`, `:active`, `:focus`, and `:visited` never match because there is no live event state during loading. |
| CSS-only compositing properties | Partial | `SvgIsolation` and `SvgMixBlendMode` expose typed values from CSS style sources. Retained scene nodes create save layers and blend paints for supported CSS paths. Bare presentation-attribute forms are intentionally ignored. |
| `paint-order` | Partial | `SvgPaintOrder` parses `normal` and fill/stroke/markers orders and rejects invalid duplicates. Retained paths and text use paint order in focused renderer paths, but broader computed-style integration and pixel coverage remain open. |
| CSS transform properties | Parsed | `transform-box` and `transform-origin` are recognized/preserved as SVG 2 transform style properties. Attribute `transform` remains the reliable rendered path; full CSS transform integration is future work. |

## Geometry And Document Features

| Feature | Status | Current contract |
|---|---|---|
| `symbol` geometry | Partial | `SvgSymbol` has `x`, `y`, `width`, `height`, `refX`, and `refY`. Direct symbol reference paths use width/height in retained compilation; full `refX`/`refY` reference-point semantics remain open. |
| Basic shape `pathLength` | Parsed | `pathLength` is available on circle, ellipse, line, polygon, and rectangle partials. The model preserves it, but dash, marker, textPath, and length normalization are not centralized yet. |
| Styleable geometry properties | Deferred | Parser/style names are partially represented, but SVG 1.1 attribute geometry remains the reliable rendered path. CSS geometry properties need computed-style consolidation. |
| `d` as a CSS property | Deferred | Path `d` attributes render normally. A complete computed `d` pipeline, geometry invalidation, and CSS parsing contract are not finished. |
| Equivalent path geometry for shapes | Partial | Shape-to-path conversion exists in shared services and is used by rendering, clipping, hit testing, markers in focused paths, and selected textPath shape support. A shared geometry abstraction for all consumers is not complete. |
| Marker support on all shapes | Partial | Marker properties exist and common path-like cases render. Complete marker vertices/orientation for every basic shape remains open. |
| Marker `orient="auto-start-reverse"` | Supported | Retained marker compilation applies SVG 2 start-marker reversal for supported marker paths. |
| `ellipse` auto radii | Deferred | Numeric SVG 1.1 radii render. SVG 2 `auto` radius resolution is not complete. |
| `svg` and `image` auto sizing | Deferred | Explicit sizes and viewBox behavior are supported. SVG 2 `auto` intrinsic sizing semantics remain open. |
| `foreignObject` geometry | Partial | Geometry is preserved and SvgML packages provide platform integrations. Core Skia rendering does not lay out browser HTML/CSS. |

## Text Features

| Feature | Status | Current contract |
|---|---|---|
| `paint-order` on text | Partial | Text elements inherit/use `SvgPaintOrder`, and retained text orders fill, stroke, and decorations in focused paths. More pixel coverage for complex styled runs is still useful. |
| `textPath path` | Partial | `SvgTextPath.PathData` parses inline path data, and inline path data wins over href in retained textPath rendering. PathLength scaling and closed-loop behavior remain open. |
| `textPath` href to basic shapes | Partial | Effective href can resolve direct visual shape targets, and focused support converts basic shape targets for text-on-path rendering. Complete coverage needs the shared geometry abstraction. |
| `textPath side` | Parsed | `SvgTextPathSide` supports `left` and `right`; `side=right` rendering is deferred. |
| `white-space` | Parsed | `SvgWhiteSpace` parses SVG 2 whitespace values. Existing SVG 1.1 text whitespace behavior remains active; full CSS Text integration remains open. |
| `inline-size`, `shape-inside`, `shape-subtract` | Parsed | Typed properties exist on `SvgElement` and are preserved for model/style contracts. Auto-wrapped text and shape exclusion layout are deferred. |
| `text-overflow` | Parsed | Typed property exists and is preserved for model/style contracts. Ellipsis/clipped inline layout is deferred. |
| Removed SVG 1.1 text features | Compatibility | `tref` and SVG font-related features can remain for compatibility where already supported. They are not new SVG 2 core requirements. |

## Paint, Masking, And Filter Features

| Feature | Status | Current contract |
|---|---|---|
| `<paint>` grammar additions | Partial | `context-fill` and `context-stroke` parse as paint servers. Marker context paint resolves in retained marker rendering; URL fallback-chain parity and general context propagation remain open. |
| `currentColor` | Supported | Paint server and color handling support inherited current color in fill/stroke/paint paths. Continued coverage with CSS variables and inherited color combinations is useful. |
| URL paint fallback | Partial | Deferred and fallback paint-server types exist, and common URL paint references work. Complex SVG 2 fallback-chain behavior still needs audit. |
| `radialGradient fr` | Parsed | The property is exposed by generated/model surfaces. Non-zero focal-radius rendering remains unaudited in retained/model gradient creation and should not be treated as full static support yet. |
| `mask-type` | Partial | `MaskType` supports alpha and luminance, and retained masks choose alpha or luminance conversion in focused paths. Broader CSS Masking parity and object-bounding-box tests remain open. |
| Linked filters | Partial | Effective href applies to filters, and retained resource dependency tracking includes linked filters. Cycle/dependency tests and full region inheritance need more coverage. |
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
| Central computed style cache | Not finished. Some typed getters read effective CSS values, but style resolution remains scattered. |
| Shared geometry service | Needed for marker-on-all-shapes, bbox options, hit testing, complete textPath targets, and pathLength normalization. |
| Full SVG 2 text layout | Wrapping, `side=right`, closed-loop textPath, `pathLength` text scaling, ellipsis, and shape exclusion are not complete. |
| Full symbol reference-point semantics | `refX` and `refY` are parsed but not fully rendered. |
| Complete context paint | Marker context paint works; general reuse/resource context paint and fallback chains remain open. |
| Full masking/filter parity | Focused `mask-type`, `feDropShadow`, and `feImage` paths exist; full CSS Masking and Filter Effects parity is not complete. |
| Mesh gradients, hatches, `solidcolor` | Deferred or preserve-only. |
| `stroke-linejoin: arcs` | Deferred; current rendering falls back to existing line-join behavior. |
| Other at-risk `vector-effect` values | Deferred; unsupported values fall back to existing stroke behavior. |
| Dynamic interactive behavior | Scripting, live DOM APIs, CSSOM, event processing, selection/editing, runtime media, and broad animation timelines are outside this static subset. |

## Implementation Evidence

| Layer | Main contracts |
|---|---|
| `Svg.Custom` | `SvgDocumentLoadOptions`, `SvgProcessingMode`, `SvgExternalResourcePolicy`, `SvgElementHrefExtensions`, `SvgPaintOrder`, `SvgContextPaintServer`, `SvgDropShadow`, `SvgText2Properties`, `SvgTransform2Properties`, `SvgSymbol.Svg2`, `SvgPathBasedElement.Svg2`, and expanded style attribute recognition. |
| `Svg.Model` | `SvgParameters` load-option bridge, resource policy checks, URI/image loading, path/paint/mask/filter helpers, generated SvgML property surfaces, and `MaskType`. |
| `Svg.SceneGraph` | Retained use/resource expansion, effective href dependency tracking, textPath additions, paint-order rendering, context-paint marker state, retained masks, retained filters, `feDropShadow`, linked filter and `feImage` dependency handling. |
| `ShimSkiaSharp` | Renderer-neutral path, paint, shader, image, filter, save-layer, clip, and text commands used by current SVG 2 slices. |
| `Svg.Skia` | Converts shim commands to SkiaSharp, hosts `SKSvg` picture/model lifecycle, and provides interaction dispatch around retained static content. |
| Host controls | Preserve and clone load options through current source/cache paths and expose selected CSS/current-color/runtime conveniences; broader public host exposure remains planned. |

## Test Coverage

| Test source | Coverage signal |
|---|---|
| `Svg2StaticHrefTests` | Effective href precedence, empty href behavior, invalid URI handling, and programmatic href updates. |
| `Svg2StaticResourcePolicyTests` | External resource policies for covered image, nested SVG, SVGZ, data URI, and CSS import paths. |
| `Svg2StaticStyleContractTests` | SVG 2 style recognition, CSS-only property behavior, and style contract expectations. |
| `Svg2StaticSubsetAttributeTests` | SVG 2 parser/model attributes such as text, transform, paint, mask, and geometry additions. |
| `SvgContextPaintServerTests` and retained scene tests | Context paint, paint order, marker behavior, masks, filters, `feDropShadow`, `feImage`, and dependency tracking in focused renderer paths. |
| W3C/resvg rows | Broader static rendering references. Browser-only, dynamic, or not-yet-supported rows remain skipped with explicit reasons. |

## Related Articles

- [SVG 1.1 Static Subset Support](svg-11-static-subset-support)
- [Rendering Pipeline](../concepts/rendering-pipeline)
- [Source Formats and Assets](../concepts/source-formats-and-assets)
- [API Coverage Index](api-coverage-index)
