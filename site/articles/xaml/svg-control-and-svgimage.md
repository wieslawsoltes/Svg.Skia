---
title: "Svg Control and SvgImage"
---

# Svg Control and SvgImage

## `Svg` control

The `Svg` control is the simplest way to render an SVG in XAML:

```xml
<svg:Svg Path="/Assets/__tiger.svg" />
```

Common properties include:

- `Path`
- `Source`
- `Stretch`
- `StretchDirection`

The Skia-backed control also adds:

- `EnableCache`
- `Wireframe`
- `DisableFilters`
- `Zoom`
- `PanX`
- `PanY`
- `ZoomToPoint(...)`
- `Interaction`
- `AnimationBackend`
- `AnimationFrameInterval`
- `AnimationPlaybackRate`
- `ActualAnimationBackend`
- `AnimationBackendFallbackReason`

That makes it a better fit when you need interaction or viewport behavior.

For animated SVG, use the host playback properties directly on the control:

```xml
<svg:Svg Path="/Assets/animated.svg"
         AnimationBackend="Default"
         AnimationPlaybackRate="1"
         AnimationFrameInterval="0:0:0.016" />
```

On Avalonia, `Default` prefers the retained `NativeComposition` path when it is supported by both the host and the loaded SVG scene.

## `SvgImage`

Use `SvgImage` when the target property expects `IImage`, for example on an Avalonia `Image` control:

```xml
<Image Source="{SvgImage /Assets/__AJ_Digital_Camera.svg}" />
```

Or through explicit object syntax:

```xml
<Image>
  <Image.Source>
    <svg:SvgImage Source="/Assets/__AJ_Digital_Camera.svg" />
  </Image.Source>
</Image>
```

## `SvgSource`

`SvgSource` is the reusable source object behind `SvgImage`.

Use it when:

- the asset should be created once and reused,
- you need access to the loaded picture or model from code,
- you want to rebuild or reload the content explicitly.

## Control coordinate hit testing

The Skia-backed `Svg` control exposes:

```csharp
var hits = svgControl.HitTestElements(new Point(x, y));
```

That method accepts control coordinates, not picture coordinates.

For routed pointer events and animation playback details, see [Interaction and Animation](../guides/interaction-and-animation).
