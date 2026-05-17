---
title: "SVG 1.1 Static Subset Support"
---

# SVG 1.1 Static Subset Support

This article documents the SVG 1.1 Second Edition features supported by the current static rendering pipeline. The scope follows a document from `Svg.Custom` parsing, through shared services in `Svg.Model`, retained compilation in `Svg.SceneGraph`, shim output in `ShimSkiaSharp`, and final Skia output in `Svg.Skia`.

The normative reference is the W3C [SVG 1.1 Second Edition Recommendation](https://www.w3.org/TR/SVG11/).

## Scope Notes

| Topic | Contract |
|---|---|
| Static SVG | This page covers markup that affects pixels, resources, metadata, or retained inspection in a loaded SVG document. |
| SVG 2 and CSS extensions | Unnamespaced `href`, CSS custom properties, `paint-order`, context paint, `vector-effect`, `mix-blend-mode`, and `isolation` are compatibility extensions and are documented in [SVG 2 Static Subset Support](svg-2-static-subset-support). |
| Runtime behavior | Pointer dispatch, cursor resolution, scripts, and animation playback are runtime APIs. Static loading does not provide a browser DOM, CSSOM, event loop, or navigation UI. |
| Retained scene graph | `Svg.SceneGraph` is the implementation/API surface used to render, inspect, hit-test, and incrementally refresh static SVG content. It is not a separate SVG language feature. |
| Non-SVG inputs | Android VectorDrawable XML is supported through separate import/conversion APIs. Its Android attribute contract is not part of SVG 1.1 static support. |

## Status Legend

| Status | Meaning |
|---|---|
| Supported | Implemented in parser/model and exercised by static renderer paths. |
| Partial | Implemented for common or focused cases, with known conformance gaps. |
| Deferred | Preserved or skipped because the static Skia pipeline does not currently render the feature. |
| Compatibility | Parsed or preserved for SVG compatibility, but not a primary static rendering feature. |
| Runtime | Available only through runtime/interactive APIs or opt-in settings. |

## Core SVG 1.1 Matrix

| SVG 1.1 area | Status | Static subset contract |
|---|---|---|
| Document structure | Partial | Supports `<svg>`, nested fragments, `<g>`, `<defs>`, `<symbol>`, `<use>`, `<switch>`, `<a>`, `<image>`, `<title>`, `<desc>`, and metadata containers. `requiredExtensions` and `systemLanguage` are evaluated; `requiredFeatures` is kept for compatibility and does not gate rendering. |
| Styling | Partial | Supports presentation attributes, inline `style`, `<style>`, `xml-stylesheet`, CSS `@import`, media-qualified imports, `@media` blocks, and common selector forms. The loader treats the static stylesheet context as screen-like and does not implement CSSOM. |
| CSS selector runtime boundary | Partial | Static selector handling covers common structural/class/id selectors plus static forms such as `:root`, `:link`, and `:lang()`. Interactive pseudo-classes such as `:hover`, `:active`, `:focus`, and `:visited` never match during static load. |
| Coordinate systems and units | Supported | Supports `transform`, transform lists, viewBox transforms, nested viewports, object bounding box units, user units, percentages, `em`, `ex`, `px`, `in`, `cm`, `mm`, `pt`, and `pc`. Physical units follow the repository SVG pixel assumptions. |
| Paths | Supported | Supports moveto, lineto, horizontal/vertical lineto, cubic/smooth cubic curves, quadratic/smooth quadratic curves, elliptical arcs, closepath, relative commands, fill rules, and marker point extraction. |
| Basic shapes | Supported | Supports `<rect>`, `<circle>`, `<ellipse>`, `<line>`, `<polyline>`, `<polygon>`, rounded rectangles, point lists, and shape-to-path conversion. |
| Text | Partial | Supports `<text>`, `<tspan>`, `<tref>`, `<textPath>`, x/y/dx/dy/rotate positioning, `textLength`, `lengthAdjust`, bidi direction, writing mode, text decoration, and fill/stroke text painting. Browser-complete baseline, vertical layout, textPath, shaping, and glyph fallback behavior remain partial. |
| SVG 1.1 altGlyph text | Deferred | `altGlyph`, `altGlyphDef`, `altGlyphItem`, and `glyphRef` are not implemented. W3C `text-altglyph-*` rows remain skipped with explicit reasons. |
| Fill and stroke painting | Supported | Supports `fill`, `stroke`, opacity values, fill and clip rules, stroke width, dash array, dash offset, line cap, line join, miter limit, visibility, display, `currentColor`, and `shape-rendering`. |
| Markers | Partial | Supports `marker-start`, `marker-mid`, `marker-end`, marker geometry, marker units, and orientation for common path-like content. Broader marker-on-all-shapes behavior belongs to the SVG 2 workstream. |
| Color | Partial | Supports CSS color names, hex colors, rgb forms, opacity, `currentColor`, `color-interpolation`, and `color-interpolation-filters` in supported paint/filter paths. ICC `color-profile` behavior is compatibility-only and not a static target. |
| Gradients and patterns | Supported | Supports `<linearGradient>`, `<radialGradient>`, `<stop>`, `<pattern>`, gradient units, pattern units, pattern content units, transforms, spread methods, stop color/opacity, and linked paint servers. |
| Clipping, masking, compositing | Partial | Supports `<clipPath>`, `clipPathUnits`, `clip: rect(...)`, nested/referenced clips, path/basic-shape/text/use clipping, masks with focused alpha/luminance behavior, opacity, and layer compositing. Object-bounding-box and referenced-content edge cases still need focused coverage. |
| Filter effects | Partial | Supports many SVG 1.1 primitives, filter regions, primitive units, primitive chaining, light sources, linked filters, and selected color-interpolation behavior. Exact browser parity varies by primitive and input chain. |
| Linking | Partial | Supports same-document references, data URLs, external image references, `xlink:href`, `xml:base`, and references from use, gradients, patterns, filters, filter images, text, masks, and clips. Host navigation from `<a>` is not part of static rendering. |
| Interactivity | Runtime | `pointer-events` can affect hit testing and `cursor` is captured for retained/runtime dispatch and Avalonia/Uno host cursor mapping. Static rendering does not dispatch browser events. |
| Scripting | Runtime | Script elements and script attributes are runtime concerns. JavaScript support lives in the optional JavaScript package and is disabled by default. |
| Animation | Runtime | Animation elements are represented in `Svg.Custom`, and runtime APIs can seek or advance animation state. SMIL timeline playback is not part of static rendering. |
| Fonts | Partial | Supports system fonts, typeface provider hooks, family/style/weight/stretch, relative weights, and optional SVG font compatibility through settings. `@font-face`/WOFF policy and full browser font fallback remain limited. |
| Extensibility | Partial | Unknown SVG elements/attributes and non-SVG nodes can be preserved. `foreignObject` has bounds handling and SvgML host-control integrations, but core Skia rendering is not an HTML/CSS layout engine. |

## Element Support Matrix

| Element group | Status | Elements and notes |
|---|---|---|
| Root and containers | Partial | `svg`, `g`, `defs`, `symbol`, `switch`, and `a` render or preserve expected static structure. `symbol` and `use` cover common static references; advanced reference-point semantics are SVG 2 work. |
| Descriptive and metadata | Supported | `title`, `desc`, and `metadata` are parsed and preserved as non-rendering content. |
| Graphics shapes | Supported | `path`, `rect`, `circle`, `ellipse`, `line`, `polyline`, and `polygon` render through retained path/shape compilation. |
| Text | Partial | `text`, `tspan`, `tref`, and `textPath` render for many static cases. `altGlyph`, `altGlyphDef`, `altGlyphItem`, and `glyphRef` are deferred. |
| Paint servers | Supported | `linearGradient`, `radialGradient`, `stop`, and `pattern` support template inheritance, transforms, and current static paint paths. |
| Markers | Partial | `marker` renders for common path-like content. Complete SVG 2 marker broadening is separate. |
| Clipping and masking | Partial | `clipPath` and `mask` support common static paths; object-bounding-box and referenced-content edge cases remain partial. |
| Filters | Partial | `filter`, filter primitive children, and light source children are implemented for many practical static paths. |
| Images | Partial | `image` supports raster, SVG, SVGZ, and data URI paths subject to resource policy and asset loader behavior. |
| Scripting and animation | Runtime | `script`, `animate`, `set`, `animateMotion`, `animateColor`, `animateTransform`, and `mpath` are runtime features, not static feature coverage. |
| Legacy SVG fonts | Partial | `font`, `font-face`, `glyph`, `missing-glyph`, `hkern`, and related SVG font nodes are optional compatibility support through `EnableSvgFonts`. |
| Extensibility | Partial | `foreignObject`, unknown SVG elements, and non-SVG nodes can be preserved or hosted through platform-specific integrations. |

## Attribute And Property Matrix

| Category | Status | Supported examples and caveats |
|---|---|---|
| Geometry | Supported | `x`, `y`, `width`, `height`, `cx`, `cy`, `r`, `rx`, `ry`, `x1`, `y1`, `x2`, `y2`, `points`, and `d` are supported as SVG 1.1 attributes. SVG 2 styleable geometry is documented separately. |
| Viewport and alignment | Supported | `viewBox`, `preserveAspectRatio`, and `overflow` are used by root SVG, nested SVG, patterns, symbols, images, and viewports where implemented. |
| Paint | Supported | `fill`, `stroke`, `color`, `fill-opacity`, `stroke-opacity`, `opacity`, `fill-rule`, and `clip-rule` include `currentColor` and paint-server references. |
| Stroke | Supported | `stroke-width`, `stroke-linecap`, `stroke-linejoin`, `stroke-miterlimit`, `stroke-dasharray`, and `stroke-dashoffset` render through Skia; tiny antialiasing differences from browser engines are expected. |
| Markers | Partial | `marker-start`, `marker-mid`, `marker-end`, marker geometry, and marker orientation attributes render for common path marker cases. |
| Text | Partial | `font-family`, `font-size`, `font-style`, `font-weight`, `font-stretch`, `text-anchor`, `dominant-baseline`, `baseline-shift`, `direction`, `writing-mode`, `text-decoration`, `letter-spacing`, `word-spacing`, `textLength`, and `lengthAdjust` are supported for practical static text paths. |
| Resources | Partial | `xlink:href`, `xml:base`, data URLs, and local/file/HTTP resources are controlled by load options, `SvgParameters`, asset loaders, and resolver settings. SVG 2 unnamespaced `href` precedence is documented in the SVG 2 article. |
| Styling | Partial | `class`, `style`, presentation attributes, style sheets, `@import`, media-qualified imports, and `@media` are supported for common static cases. CSS custom properties are a compatibility extension documented in the SVG 2 article. |
| Transforms | Supported | `transform`, `gradientTransform`, and `patternTransform` map to retained matrices. |
| Filters | Partial | Filter primitive attributes, `filter`, `filterUnits`, `primitiveUnits`, `result`, `in`, `in2`, `color-interpolation`, and `color-interpolation-filters` are used by supported filter paths. |
| Conditional processing | Partial | `requiredExtensions` and `systemLanguage` are evaluated. `requiredFeatures` is preserved but intentionally does not block static rendering. |
| Events, cursor, and scripts | Runtime | Event attributes and script href/type are runtime concerns. `cursor` is available to retained interaction dispatch and host controls, not to static pixels. |

## Implementation Evidence

| Layer | Main contracts used by SVG 1.1 support |
|---|---|
| `Svg.Custom` | Compatibility document loading, CSS/style preprocessing, element preservation, local paint-server overrides, animation and script representation, and SVG 1.1 parser/model integration. |
| `Svg.Model` | `SvgParameters`, unit/transform/path/paint/filter services, image loading, resource resolution, typeface provider hooks, and optional SVG font settings. |
| `Svg.SceneGraph` | Retained structural compilation, shape/text/image/resource nodes, clip/mask/filter composition, hit testing, cursor metadata, dependency tracking, and subtree refresh. |
| `ShimSkiaSharp` | Renderer-neutral path, paint, shader, image, filter, save-layer, clip, and text command surface. |
| `Svg.Skia` | SkiaSharp picture/model lifecycle, static drawing, interaction dispatcher, and host-facing `SKSvg` APIs. |
| Host controls | Avalonia and Uno controls expose selected runtime surfaces such as hit testing, pointer routing, cursor mapping, source caches, and current-color/CSS conveniences. |

## Testing And Reference Coverage

| Test source | What it covers | Notes |
|---|---|---|
| W3C SVG 1.1 suite | Broad rendering, styling, paths, shapes, filters, text, linking, and interaction fixtures. | Browser-only rows remain skipped with explicit reasons. Chrome overrides are used where the repository tracks browser behavior instead of legacy references. |
| resvg fixture suite | Large static rendering corpus for SVG features and edge cases. | Some rows are skipped for features outside the current renderer scope. |
| Model unit tests | Parser, resource policy, CSS, paint, pattern, href, VectorDrawable, and contract behavior. | VectorDrawable coverage belongs to separate import/conversion behavior, not SVG 1.1 static support. |
| Retained scene graph tests | Static rendering behavior, mutation/dependency refresh, retained text, filters, masks, interaction metadata, and resource expansion. | Used to verify renderer behavior without relying only on image diff baselines. |

## Host And Load Options

| Surface | Current support |
|---|---|
| Core loaders | `SvgParameters` and `SvgDocumentLoadOptions` carry processing mode, external resource policy, unknown-element preservation, and SVG 2 href preference into document loading paths. |
| Host wrappers | Avalonia, Uno, MAUI, and SvgML wrappers preserve options through selected source/cache paths and expose host-specific conveniences. Not every host control exposes the full load-option contract yet. |
| Resource security | Static resource behavior depends on load options, resolver settings, asset loaders, and the current path being exercised. Secure-static parity is broader than the current implementation. |

## Known Non-Goals

| Area | Reason |
|---|---|
| Full browser DOM and CSSOM | The renderer is a document-to-picture pipeline, not a browser engine. |
| Browser navigation from `<a>` | Hosts can consume link metadata, but static rendering does not navigate. |
| Script execution by default | JavaScript is optional and disabled by default. |
| SMIL as static rendering | Animation can be driven through runtime APIs; static loading renders the selected document state. |
| HTML/CSS layout inside `foreignObject` | SvgML host-control integrations are platform features, not core Skia SVG static rendering. |

## Related Articles

- [SVG 2 Static Subset Support](svg-2-static-subset-support)
- [Rendering Pipeline](../concepts/rendering-pipeline)
- [Source Formats and Assets](../concepts/source-formats-and-assets)
- [Testing and W3C Suite](../advanced/testing-and-w3c-suite)
