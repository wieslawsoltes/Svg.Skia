---
title: "Overview"
---

# Overview

## UI SVG stacks

### `SvgML.Avalonia`

This package brings the SVG element tree itself into Avalonia XAML. Use it when:

- you want inline `svg`, `rect`, `g`, filter, or text elements inside resources or templates,
- you want SVG attribute names such as `fill-opacity`, `stroke-width`, or `viewBox` to stay close to authored markup,
- you want SVG `foreignObject` to host native Avalonia controls inside text flow or scene geometry,
- you want Avalonia style selectors and animations to target the inline SVG tree,
- you are happy to render through the shared `Svg.Skia` backend behind the scenes.

### `SvgML.Maui`

This package brings the SVG element tree itself into .NET MAUI XAML. Use it when:

- you want inline `svg`, `rect`, `g`, filter, or text elements directly inside a MAUI page,
- you want SVG attribute names such as `fill-opacity`, `stroke-width`, or `viewBox` to stay close to authored markup,
- you want SVG `foreignObject` to host native MAUI controls inside text flow or scene geometry,
- you want the inline tree to render through the shared `Svg.Skia` backend without loading an external asset file,
- you are targeting the current MAUI package lane: Android, iOS, or Mac Catalyst.

### `Maui.Svg.Skia`

This package wraps the `Svg.Skia` runtime renderer for .NET MAUI. Use it when:

- you want MAUI XAML integration through `SKCanvasView`,
- the SVG already exists as an app package asset, file, URL, stream, or source string,
- you need `SvgSource` resources with async asset loading,
- you want `HitTestElements(...)`, `TryGetPicturePoint(...)`, zoom, pan, wireframe, filter toggles, or animation playback.

### `SvgML.Uno`

This package brings the SVG element tree itself into Uno Platform XAML. Use it when:

- you want inline `svg`, `path`, `g`, filter, or text elements directly inside a Uno page,
- you want SVG attribute names such as `fill`, `opacity`, or `viewBox` to stay close to authored markup, with dash-named declarations available through `style` or Uno-safe member names,
- you want SVG `foreignObject` to host native Uno controls inside text flow or scene geometry,
- you want the inline tree to render through the shared `Svg.Skia` backend without loading an external asset file,
- you are already on the Uno `SkiaRenderer` path and want the authored markup to stay in XAML instead of a separate asset file.

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

The Uno, MAUI, and Avalonia Skia-backed packages all provide an `Svg` control, reusable `SvgSource`, shared hit testing, and the same animation-backend selection model.

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
| `Svg.Controls.Skia.Maui` | `Maui.Svg.Skia` |
| `Svg.Controls.Skia.Avalonia` | `Avalonia.Svg.Skia` |
| `Svg.Controls.Avalonia` | `Avalonia.Svg` |
| `SvgML.Avalonia` | `SvgML` |
| `SvgML.Maui` | `SvgML` |
| `SvgML.Uno` | `SvgML` |

## General-purpose Skia controls

`Skia.Controls.Avalonia` complements the SVG packages with:

- `SKCanvasControl`
- `SKBitmapControl`
- `SKPathControl`
- `SKPictureControl`
- `SKPictureImage`

Those controls are useful even when the source picture did not come from SVG.
