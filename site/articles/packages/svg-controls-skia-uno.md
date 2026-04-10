---
title: "Svg.Controls.Skia.Uno"
---

# Svg.Controls.Skia.Uno

`Svg.Controls.Skia.Uno` brings the Skia-backed SVG control model to Uno Platform. It wraps `Svg.Skia`, renders directly through `Uno.WinUI.Graphics2DSK.SKCanvasElement`, and keeps the same control-focused API shape as the Avalonia Skia package where that maps cleanly to Uno.

## Install

```bash
dotnet add package Svg.Controls.Skia.Uno
```

## Choose this package when

- your app uses Uno Platform and wants the fastest SVG path through the live Skia canvas,
- you want an `Svg` control with `Path`, `Source`, `SvgSource`, `Stretch`, `StretchDirection`, `EnableCache`, `Wireframe`, `DisableFilters`, `Zoom`, `PanX`, and `PanY`,
- you need control-coordinate hit testing through `TryGetPicturePoint(...)` and `HitTestElements(...)`,
- you want host-driven SVG animation playback backed by the shared `SKSvg` runtime,
- you want reusable `SvgSource` resources that can be cloned and restyled per control.

## Main types

| Type | Role |
| --- | --- |
| `Uno.Svg.Skia.Svg` | Uno control for direct SVG display on `SKCanvasElement` |
| `Uno.Svg.Skia.SvgSource` | Reusable, cloneable, reloadable source object |
| `Uno.Svg.Skia.StretchDirection` | Uno-side equivalent of the Avalonia stretch-direction API |
| `Svg.Skia.SvgInteractionDispatcher` | Routed pointer helper exposed through `Svg.Interaction` |

## Basic XAML usage

```xml
<Page
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:svg="using:Uno.Svg.Skia">
  <svg:Svg Path="/Assets/__tiger.svg"
           Stretch="Uniform"
           EnableCache="True" />
</Page>
```

## Reusable `SvgSource` resources

```xml
<Page.Resources>
  <svg:SvgSource x:Key="TigerSource" Path="/Assets/__tiger.svg" />
</Page.Resources>

<svg:Svg SvgSource="{StaticResource TigerSource}" Height="220" />
```

The control clones an external `SvgSource` before applying per-control CSS, wireframe, or filter settings, so one shared resource can safely back multiple controls with different runtime styling.

## Async path loading

Uno resource and network loading is async-first:

```csharp
var source = await SvgSource.LoadAsync(
    "/Assets/__tiger.svg",
    parameters: new SvgParameters(null, ".accent { fill: #2563eb; }"));

await source.ReLoadAsync(new SvgParameters(null, ".accent { fill: #ef4444; }"));
```

Use the synchronous loaders for inline SVG strings, `Stream`, and `SvgDocument` inputs.

## What differs from the Avalonia package

- `SvgImage`, `SvgResource`, and markup extensions are not part of the Uno package in v1.
- The Uno replacement for those scenarios is `Svg` plus reusable `SvgSource` resources.
- The package is Skia-renderer-only and does not add a native-renderer fallback.

## Animation playback

The Uno `Svg` control exposes the same host-driven animation surface as the Avalonia Skia package:

- `AnimationBackend`
- `AnimationFrameInterval`
- `AnimationPlaybackRate`
- `ActualAnimationBackend`
- `AnimationBackendFallbackReason`
- `AnimationBackendResolution`
- `AnimationBackendCapabilities`

The control shares the same backend enum:

- `Default`
- `Manual`
- `DispatcherTimer`
- `RenderLoop`
- `NativeComposition`

Uno currently falls back away from `NativeComposition` because this implementation does not have a working retained child-visual attachment path on the active package surface.

## Related docs

- [Quickstart: Uno](../getting-started/quickstart-uno)
- [Interaction and Animation](../guides/interaction-and-animation)
- [Uno Svg Control](../xaml/uno-svg-control)
- [Uno Sample Publishing](../advanced/uno-sample-publishing)
- [Svg.Controls.Skia.Avalonia](svg-controls-skia-avalonia)
