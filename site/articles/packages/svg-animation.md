---
title: "Svg.Animation"
---

# Svg.Animation

`Svg.Animation` contains the repository's shared SVG animation runtime. It is the package that evaluates SMIL timing, computes per-frame state, exposes host-backend contracts, and provides the controller surface used by the runtime and UI integrations.

## Install

```bash
dotnet add package Svg.Animation
```

## Choose this package when

- you need SVG timing and frame-state evaluation outside a UI-specific control package,
- you want host playback backend selection contracts without taking the full Avalonia or Uno control stack,
- you are integrating animation into a custom renderer or host,
- you need pointer-triggered SVG animation events and clock control at the runtime layer.

## Main types

| Type | Role |
| --- | --- |
| `SvgAnimationController` | Evaluates timing, creates animated documents, and applies frame state |
| `SvgAnimationFrameState` | Snapshot of resolved animation values for a point in time |
| `SvgAnimationClock` | Advances, seeks, and resets the SVG animation timeline |
| `SvgAnimationHostBackend` | Shared host-backend enum used by runtime/UI integrations |
| `SvgAnimationHostBackendResolver` | Chooses an actual playback backend from host capabilities |
| `SvgAnimationFrameChangedEventArgs` | Notifies hosts that a new frame was produced |

## What it provides

`Svg.Animation` separates animation concerns from the higher-level host packages:

- SMIL timing and repeat semantics
- event-triggered animations
- per-frame evaluation
- animated document application
- host playback backend capability negotiation

This lets `Svg.Skia`, `Svg.Controls.Skia.Avalonia`, and `Svg.Controls.Skia.Uno` share one animation engine instead of each reimplementing timing behavior.

## Typical workflow

1. Load or build an `SvgDocument`.
2. Create `SvgAnimationController`.
3. Evaluate a frame state at a given clock time.
4. Create or update an animated document from that frame state.
5. Hand the resulting document to a renderer or host-specific pipeline.

## Relationship to other packages

- [Svg.Custom](svg-custom) provides the underlying SVG DOM and animation elements.
- [Svg.Skia](svg-skia) uses `Svg.Animation` to drive runtime playback and frame invalidation.
- [Svg.Controls.Skia.Avalonia](svg-controls-skia-avalonia) and [Svg.Controls.Skia.Uno](svg-controls-skia-uno) use the shared host-backend contracts to hook animation into their UI loops.

## When not to choose `Svg.Animation`

- Choose [Svg.Skia](svg-skia) when you want the full runtime renderer and animation surface together.
- Choose [Svg.Controls.Skia.Avalonia](svg-controls-skia-avalonia) or [Svg.Controls.Skia.Uno](svg-controls-skia-uno) when your main integration point is a UI control.
- Choose [Svg.Model](svg-model) when the task is about the intermediate drawable/picture model rather than animation timing.

## Related docs

- [Interaction and Animation](../guides/interaction-and-animation)
- [Svg.Skia](svg-skia)
