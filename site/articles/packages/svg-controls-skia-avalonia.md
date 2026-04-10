---
title: "Svg.Controls.Skia.Avalonia"
---

# Svg.Controls.Skia.Avalonia

`Svg.Controls.Skia.Avalonia` is the richest Avalonia integration in the repository. It wraps `Svg.Skia`, exposes the common XAML-friendly SVG primitives, and still gives you access to the underlying `SKSvg` runtime object when you need advanced behavior.

## Install

```bash
dotnet add package Svg.Controls.Skia.Avalonia
```

## Choose this package when

- your Avalonia app already uses the Skia-backed rendering path,
- you want `Svg`, `SvgImage`, `SvgSource`, and `SvgResource` in XAML,
- you need control-coordinate hit testing,
- you want zoom, pan, wireframe, filter toggles, source reload support, or routed interaction,
- you want host-driven SVG animation playback with optional retained native composition,
- you want direct access to the underlying `Svg.Skia.SKSvg`.

## Main types

| Type | Role |
| --- | --- |
| `Avalonia.Svg.Skia.Svg` | Control for direct SVG display |
| `Avalonia.Svg.Skia.SvgImage` | `IImage` wrapper for an `SvgSource` |
| `Avalonia.Svg.Skia.SvgSource` | Reusable, cloneable, reloadable source object |
| `SvgImageExtension` | Markup extension for concise XAML image usage |
| `SvgResourceExtension` | Brush-producing markup extension for backgrounds and fills |
| `Svg.Skia.SvgInteractionDispatcher` | Routed pointer helper exposed through `Svg.Interaction` |

## Basic XAML usage

```xml
<Window
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:svg="clr-namespace:Avalonia.Svg.Skia;assembly=Svg.Controls.Skia.Avalonia">
  <DockPanel>
    <svg:Svg Path="/Assets/__tiger.svg"
             Stretch="Uniform"
             EnableCache="True"
             Wireframe="False" />
  </DockPanel>
</Window>
```

For image targets, use `SvgImage` or the `{SvgImage ...}` markup extension:

```xml
<Image Source="{SvgImage /Assets/__AJ_Digital_Camera.svg}" />
```

## Reusable sources and runtime restyling

`SvgSource` is the reusable core of the package. It supports loading from paths, streams, strings, or `SvgDocument`, and it keeps enough information to reload with new parameters.

```csharp
using System;
using Avalonia.Svg.Skia;
using Svg.Model;

var source = SvgSource.Load(
    "avares://MyApp/Assets/icon.svg",
    new Uri("avares://MyApp/"));

source.ReLoad(new SvgParameters(null, ".accent { fill: #007ACC; }"));

var image = new SvgImage
{
    Source = source
};
```

That is the package to use when CSS overrides are part of application state.

## Control-specific features

The `Svg` control adds behavior that does not exist in the non-Skia Avalonia package:

- `EnableCache`
- `Wireframe`
- `DisableFilters`
- `Zoom`
- `PanX`
- `PanY`
- `ZoomToPoint(...)`
- `TryGetPicturePoint(...)`
- `HitTestElements(...)`
- `Interaction`
- `AnimationBackend`
- `AnimationFrameInterval`
- `AnimationPlaybackRate`
- `ActualAnimationBackend`
- `AnimationBackendFallbackReason`
- `SkSvg` access

Those features make this package the better choice for editors, diagram viewers, and interactive inspection tools.

## Animation playback and retained composition

The Avalonia `Svg` control now hosts the shared `SKSvg` animation runtime.

Available backends are:

- `Default`
- `Manual`
- `DispatcherTimer`
- `RenderLoop`
- `NativeComposition`

`Default` prefers `NativeComposition` when the host compositor and the loaded SVG can support the retained scene. If that path is unavailable, the control reports the resolved backend through `ActualAnimationBackend` and the reason through `AnimationBackendFallbackReason`.

Example:

```xml
<svg:Svg Path="/Assets/animated.svg"
         AnimationBackend="Default"
         AnimationPlaybackRate="1"
         AnimationFrameInterval="0:0:0.016" />
```

`NativeComposition` uses retained compositor child visuals for supported top-level SVG layers while still relying on the shared `SKSvg` animation runtime for timing and property evaluation.

## Routed interaction

The control exposes one shared `SvgInteractionDispatcher` instance through `Interaction`.

That surface is useful when a host wants:

- routed pointer notifications,
- capture-aware move and release handling,
- cursor hints,
- optional compatibility bridging back into `SvgElement` mouse events.

## Hit testing in Avalonia coordinates

```csharp
var point = e.GetPosition(MySvgControl);
var hits = MySvgControl.HitTestElements(point);
```

The control converts from Avalonia coordinates into picture coordinates for you, so you do not need to manage the stretch and pan math manually.

## Brushes and resources

Use `SvgResourceExtension` when an SVG should become a brush:

```xml
<Border Background="{SvgResource /Assets/__tiger.svg}" />
```

This is useful for icons, patterned surfaces, or backgrounds that should stay resolution-independent.

## When not to choose this package

- Choose [Svg.Controls.Avalonia](svg-controls-avalonia) when you want the Avalonia drawing-stack implementation instead of the `Svg.Skia` runtime path.
- Choose [Svg.Skia](svg-skia) when you do not need Avalonia controls at all.
- Choose [Skia.Controls.Avalonia](skia-controls-avalonia) when your content is already raw `SkiaSharp` data and not specifically SVG.

## Related docs

- [XAML Overview](../xaml/overview)
- [Svg Control and SvgImage](../xaml/svg-control-and-svgimage)
- [Interaction and Animation](../guides/interaction-and-animation)
- [SvgResource and Brushes](../xaml/svgresource-and-brushes)
- [Styling and Previewer](../xaml/styling-and-previewer)
