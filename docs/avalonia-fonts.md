# Avalonia fonts in SVG images

SVG text is resolved by Svg.Skia through SkiaSharp and its typeface providers. Fonts registered with Avalonia, including `WithInterFont()` and `avares://` font collections, are not automatically registered with SkiaSharp's font manager. Browser WebAssembly applications have particularly limited system font access, so unresolved text can appear as missing-glyph boxes even when ordinary Avalonia text renders correctly.

## Issue #565 implementation

This change will provide:

- An Avalonia-backed typeface provider that resolves registered application fonts and preserves requested weight, style, and stretch.
- A supported API for configuring the default `SKSvgSettings` used by `SvgSource`, without reaching into `s_skiaModel`.
- Browser font guidance, regression tests, and a runnable browser validation case using embedded fonts.

Implementation and validation details will be completed in this document as the pull request progresses.
