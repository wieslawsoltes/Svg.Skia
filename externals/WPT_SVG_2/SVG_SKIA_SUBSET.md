# Svg.Skia WPT SVG 2 Static Subset

This directory vendors a focused static subset from the Web Platform Tests
`svg/` corpus for Svg.Skia renderer regression coverage.

Source repository: https://github.com/web-platform-tests/wpt

Source commit:

```text
c05b447326585237713013c66341eab2cdf967b6
```

SVG 2 does not have a standalone W3C PNG-based test-suite release equivalent
to SVG 1.1 Second Edition. The SVG Working Group transitioned SVG 2 testing to
Web Platform Tests, where static rendering cases are primarily SVG/HTML
reftests. Svg.Skia checks these selected rows against Chrome-generated PNG
references in `tests/Svg.Skia.UnitTests/ChromeReference/WPT`.

The subset intentionally starts with static SVG files that map to Svg.Skia's
renderer-only scope. Rows that are active today live in
`WptSvg2StaticSubsetTests.StaticSubsetRows`; rows that expose known remaining
SVG 2 gaps live in `WptSvg2StaticSubsetTests.DeferredRows` with an explicit
skip reason.

The checked corpus covers:

- SVG 2 geometry properties on shapes.
- The SVG 2 `d` property.
- `pathLength` on basic shapes.
- SVG 2 context paint.
- SVG 2 paint-order and paint-server fallback behavior.
- SVG 2 radial gradient `fr`.
- SVG 2 `textPath path` and `textPath side`.
- SVG 2 `<use>` sizing semantics for referenced `svg` and `symbol` nodes.

The checked WPT license is preserved in `LICENSE.md`.
