---
title: "Overview"
---

# Overview

## UI SVG stacks

### `Uno.Svg.Skia`

This package wraps the `Svg.Skia` runtime renderer for Uno Platform. Use it when:

- you want Uno XAML integration through `SKCanvasElement`,
- you need `SvgSource` resources with async asset loading,
- you want `HitTestElements(...)`, `TryGetPicturePoint(...)`, zoom, pan, wireframe, filter toggles, or animation playback.

### `Avalonia.Svg.Skia`

This package wraps the `Svg.Skia` runtime renderer. Use it when:

- you already depend on Skia-backed rendering,
- you want `SKSvg` features such as hit testing or explicit model rebuild access,
- you want the Skia-backed `SvgSource` behavior and reload support,
- you want host-driven animation playback with `RenderLoop`, `DispatcherTimer`, or retained `NativeComposition` when available.

### `Avalonia.Svg`

This package exposes a similar surface but draws through Avalonia's own drawing context. Use it when:

- you want SVG-backed controls without depending on the Skia-backed Avalonia layer,
- the Avalonia drawing model is the preferred integration point,
- you need a lighter integration around the same source concepts.

## Shared concepts

The Uno and Avalonia Skia-backed packages all provide an `Svg` control, reusable `SvgSource`, shared hit testing, shared interaction dispatch, and the same animation-backend selection model.

The Avalonia packages additionally provide `SvgImage`, markup extensions, and brush helpers.

The Avalonia packages provide:

- `Svg` control,
- `SvgImage`,
- `SvgImageExtension`,
- `SvgSource`,
- `SvgResourceExtension`.

The namespaces differ:

| Package | Namespace |
| --- | --- |
| `Svg.Controls.Skia.Uno` | `Uno.Svg.Skia` |
| `Svg.Controls.Skia.Avalonia` | `Avalonia.Svg.Skia` |
| `Svg.Controls.Avalonia` | `Avalonia.Svg` |

## General-purpose Skia controls

`Skia.Controls.Avalonia` complements the SVG packages with:

- `SKCanvasControl`
- `SKBitmapControl`
- `SKPathControl`
- `SKPictureControl`
- `SKPictureImage`

Those controls are useful even when the source picture did not come from SVG.
