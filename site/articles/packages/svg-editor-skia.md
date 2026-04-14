---
title: "Svg.Editor.Skia"
---

# Svg.Editor.Skia

`Svg.Editor.Skia` contains the editing math and overlay-rendering helpers used by the interactive canvas.

## Install

```bash
dotnet add package Svg.Editor.Skia
```

## Choose this package when

- you are building your own canvas or surface control,
- you need reusable handle hit-testing, transform math, and path editing,
- you want align/distribute helpers for selected SVG elements,
- you want the editor overlay renderer without taking the Avalonia workspace.

## Main types

| Type | Role |
| --- | --- |
| `SvgEditorInteractionController` | Public adapter over selection and path-edit behavior |
| `SvgEditorOverlayRenderer` | Public adapter over overlay drawing |
| `SelectionService` | Bounds, transforms, resizing, skewing, flipping, and handle hit-testing |
| `PathService` | Editable path points and path operations |
| `AlignService` | Align and distribute commands |
| `RenderingService` | Grid, layer, selection, and path overlay drawing |
| `BoundsInfo` | Computed selection handles |
| `PathPoint` | Individual editable path point |

## Minimal setup

```csharp
using Svg.Editor.Skia;

var interaction = new SvgEditorInteractionController
{
    SnapToGrid = true,
    GridSize = 10
};

var overlays = new SvgEditorOverlayRenderer();
```

In a custom surface, the interaction controller is the entry point for:

- `GetBoundsInfo(...)`
- `HitHandle(...)`
- `SetRotation(...)`
- `SetTranslation(...)`
- `SetScale(...)`
- `SetSkew(...)`
- `ResizeElement(...)`
- path-edit operations such as `StartPathEditing(...)`, `MoveActivePathPoint(...)`, and `MakePathPointSmooth(...)`

## Relationship to the higher packages

`Svg.Editor.Skia.Avalonia` exposes these helpers on `SvgEditorSurface` through the `InteractionController` and `OverlayRenderer` properties. Use `Svg.Editor.Skia` directly only when the host owns the rest of the UI composition.

## Related docs

- [Rendering and Svg Services](../editor/rendering-and-svg-services)
- [Svg.Editor.Skia.Avalonia](svg-editor-skia-avalonia)
- [Hit Testing and Scene Inspection](../guides/hit-testing-and-model-editing)
