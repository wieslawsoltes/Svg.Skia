---
title: "Hit Testing and Scene Inspection"
---

# Hit Testing and Scene Inspection

`SKSvg` exposes both broad inspection-oriented hit testing and topmost routed-target hit testing. The retained scene graph powers both APIs.

## Hit testing elements in picture coordinates

Use `HitTestElements(...)` when you want every matching authored element, not just the routed target:

```csharp
using System.Linq;
using ShimSkiaSharp;
using Svg.Skia;

using var svg = new SKSvg();
svg.Load("image.svg");

var element = svg.HitTestElements(new SKPoint(10, 10)).FirstOrDefault();
var elementsInBox = svg.HitTestElements(new SKRect(0, 0, 40, 40)).ToList();
var topmost = svg.HitTestTopmostElement(new SKPoint(10, 10));
```

## Inspecting retained scene nodes

Use the scene-node APIs when you want compiled bounds, hit-test targets, or node metadata instead of just the authored `SvgElement`:

```csharp
using System.Linq;
using ShimSkiaSharp;
using Svg.Skia;

using var svg = new SKSvg();
svg.Load("image.svg");

var node = svg.HitTestTopmostSceneNode(new SKPoint(10, 10));
var nodesInBox = svg.HitTestSceneNodes(new SKRect(0, 0, 40, 40)).ToList();

var targetId = node?.HitTestTargetElement?.ID;
var bounds = node?.TransformedBounds;
```

## Hit testing on transformed canvases

When the picture is drawn through a transformed canvas, convert the pointer or selection rectangle back into picture space first:

```csharp
using ShimSkiaSharp;
using Svg.Skia;

using var svg = new SKSvg();
svg.Load("image.svg");

var canvasMatrix = SKMatrix.CreateScale(2f, 2f);
if (svg.TryGetPicturePoint(new SKPoint(50, 50), canvasMatrix, out var picturePoint))
{
    var hits = svg.HitTestElements(picturePoint);
}
```

There are also overloads that take the canvas matrix directly:

- `HitTestElements(SKPoint point, SKMatrix canvasMatrix)`
- `HitTestElements(SKRect rect, SKMatrix canvasMatrix)`
- `HitTestSceneNodes(SKPoint point, SKMatrix canvasMatrix)`
- `HitTestSceneNodes(SKRect rect, SKMatrix canvasMatrix)`
- `HitTestTopmostSceneNode(SKPoint point, SKMatrix canvasMatrix)`
- `HitTestTopmostElement(SKPoint point, SKMatrix canvasMatrix)`

## Avalonia control hit testing

The Skia-backed `Avalonia.Svg.Skia.Svg` control exposes `HitTestElements(Point point)` in control coordinates, so the control handles the coordinate transform for you.

The shared hit-test path now also respects typed `pointer-events` values, geometry-aware element bounds, and topmost-target routing used by `SvgInteractionDispatcher`.

Use `HitTestElements(...)` when you want all matches. Use `HitTestTopmostElement(...)` when you need the routed pointer target. Use `HitTestSceneNodes(...)` when diagnostics or editor tooling need access to retained-scene metadata.

For retained-scene lookup helpers, continue with [Retained Scene Graph Usage](retained-scene-graph-usage). For the higher-level routed input and animation playback surface, continue with [Interaction and Animation](interaction-and-animation).
