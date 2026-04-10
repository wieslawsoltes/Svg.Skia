---
title: "Uno Svg Control"
---

# Uno Svg Control

## Namespace

```xml
xmlns:svg="using:Uno.Svg.Skia"
```

## Core control properties

The Uno `Svg` control keeps the control-facing API close to the Avalonia Skia package:

- `SvgSource`
- `Path`
- `Source`
- `Stretch`
- `StretchDirection`
- `EnableCache`
- `Wireframe`
- `DisableFilters`
- `Zoom`
- `PanX`
- `PanY`
- `Css`
- `CurrentCss`
- `AnimationBackend`
- `AnimationFrameInterval`
- `AnimationPlaybackRate`

## Reusable resource example

```xml
<Page.Resources>
  <svg:SvgSource x:Key="LogoSource" Path="/Assets/logo.svg" />
</Page.Resources>

<svg:Svg SvgSource="{StaticResource LogoSource}"
         Height="160"
         Stretch="Uniform" />
```

## Inline source example

```xml
<svg:Svg Source="{x:Bind InlineSvg}"
         CurrentCss=".accent { fill: #2563eb; }" />
```

## Hit testing

```csharp
var point = e.GetCurrentPoint(MySvg).Position;
var hits = MySvg.HitTestElements(point);
```

`HitTestElements(...)` accepts Uno control coordinates and maps them back into picture coordinates using the current stretch, zoom, and pan state.

## Animation playback

The Uno `Svg` control uses the shared `SKSvg` animation runtime and exposes the same resolved-backend diagnostics as the Avalonia Skia package:

- `ActualAnimationBackend`
- `AnimationBackendFallbackReason`
- `AnimationBackendResolution`
- `AnimationBackendCapabilities`

Uno currently falls back away from `NativeComposition`, so the practical playback backends are `DispatcherTimer`, `RenderLoop`, or `Manual`.

## Current v1 limits

- no `SvgImage`
- no brush/resource markup extension equivalent
- no native-renderer fallback
- no retained native-composition playback path

For those scenarios in Uno, keep the SVG in a reusable `SvgSource` and render it through one or more `Svg` controls.
