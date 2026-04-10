---
title: "Interaction and Animation"
---

# Interaction and Animation

This repository now documents four related layers for interactive or animated SVG:

- the SVG DOM layer in `Svg.Custom`,
- pointer-aware hit testing and routing in `Svg.Skia`,
- the shared animation runtime in `SKSvg`,
- host playback backends in the Avalonia and Uno controls.

## SVG animation DOM in `Svg.Custom`

`Svg.Custom` now includes the SVG 1.1 animation element family in the `Svg` namespace:

- `SvgAnimationElement`
- `SvgAnimationAttributeElement`
- `SvgAnimationValueElement`
- `SvgAnimate`
- `SvgSet`
- `SvgAnimateMotion`
- `SvgAnimateColor`
- `SvgAnimateTransform`
- `SvgMPath`

Typed enums and converters cover the stable attribute surface, including:

- `attributeType`
- `restart`
- `fill`
- `calcMode`
- `additive`
- `accumulate`
- `type` on `animateTransform`

The DOM layer also now exposes typed `pointer-events` values through `SvgPointerEvents`.

That makes `Svg.Custom` suitable for tooling that needs to parse, inspect, clone, or rewrite animation elements even when rendering or playback happens elsewhere.

## Pointer-aware hit testing

`SKSvg` still exposes the broad hit-test APIs:

- `HitTestElements(...)`
- `HitTestDrawables(...)`
- `TryGetPicturePoint(...)`
- `TryGetPictureRect(...)`

For interaction routing, the runtime now also exposes `HitTestTopmostElement(...)`, which resolves the topmost routed target instead of returning every matching element.

The hit-test path is now aware of:

- typed `pointer-events` values,
- geometry-aware point hit testing for higher-impact drawable types,
- clip and mask rejection where the shared drawable pipeline can evaluate it.

Use the broad `HitTestElements(...)` helpers when you need inspection. Use `HitTestTopmostElement(...)` when you need a routed input target.

## Shared interaction dispatcher

`Svg.Skia` now includes `SvgInteractionDispatcher`, a framework-neutral pointer-routing layer. It accepts `SvgPointerInput` and raises routed `SvgPointerEventArgs` through these phases:

- `Tunnel`
- `Target`
- `Bubble`

The dispatcher handles:

- hover enter and leave tracking,
- press and release routing,
- click generation,
- wheel routing,
- capture to the pressed target,
- shared cursor resolution,
- optional compatibility bridging back into `SvgElement` mouse events.

The Avalonia and Uno controls expose one shared dispatcher instance through their `Interaction` property.

## Shared animation runtime in `SKSvg`

`SKSvg` now owns the shared animation runtime. The document model stays shared across hosts, while the UI package only provides timing and invalidation.

Core runtime members include:

- `HasAnimations`
- `AnimationTime`
- `SetAnimationTime(...)`
- `AdvanceAnimation(...)`
- `ResetAnimation()`
- `AnimationInvalidated`
- `AnimationMinimumRenderInterval`
- `HasPendingAnimationFrame`
- `FlushPendingAnimationFrame()`
- `LastAnimationDirtyTargetCount`

The current runtime supports the repository's shared playback surface for:

- `set`
- `animate`
- `animateColor`
- `animateTransform`
- `animateMotion`
- event-driven `begin` and `end` timing supported by the shared dispatcher

## Host playback backends

`SvgAnimationHostBackend` is the shared host selection enum:

- `Default`
- `Manual`
- `DispatcherTimer`
- `RenderLoop`
- `NativeComposition`

### Avalonia

`Svg.Controls.Skia.Avalonia` exposes:

- `AnimationBackend`
- `AnimationFrameInterval`
- `AnimationPlaybackRate`
- `ActualAnimationBackend`
- `AnimationBackendFallbackReason`
- `AnimationBackendResolution`
- `AnimationBackendCapabilities`

On supported hosts, `Default` prefers `NativeComposition`, then falls back to `RenderLoop`, `DispatcherTimer`, or `Manual`.

The retained `NativeComposition` path uses one composition child visual per top-level SVG child and updates animated layers from `SKSvg.TryCreateNativeCompositionScene(...)` and `SKSvg.TryCreateNativeCompositionFrame(...)`.

### Uno

`Svg.Controls.Skia.Uno` exposes the same playback-property surface and the same resolved-backend diagnostics.

Uno currently falls back away from `NativeComposition` because the active package surface does not provide a working retained child-visual attachment path for this implementation.

## Benchmarks and sample host

The repository also adds two practical validation paths for this work:

- `tests/Svg.Skia.Benchmarks` measures the shared animation renderer and layered redraw behavior.
- `samples/TestApp` exposes backend selection, playback rate, clock display, play, pause, and restart controls so you can exercise the runtime manually.

For related lower-level guidance, see [Hit Testing and Model Editing](hit-testing-and-model-editing), [Svg.Skia](../packages/svg-skia), [Svg.Controls.Skia.Avalonia](../packages/svg-controls-skia-avalonia), and [Svg.Controls.Skia.Uno](../packages/svg-controls-skia-uno).
