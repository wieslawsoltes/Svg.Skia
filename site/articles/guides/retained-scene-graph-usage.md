---
title: "Retained Scene Graph Usage"
---

# Retained Scene Graph Usage

`SKSvg` keeps a compiled retained scene graph in `RetainedSceneGraph`. That graph is represented by `SvgSceneDocument` and is the bridge between authored SVG DOM and the low-level `ShimSkiaSharp.SKPicture` command model.

## What the retained scene graph contains

`SvgSceneDocument` stores:

- the compiled root `SvgSceneNode`,
- node lookups by element address and id,
- resource lookups for clip paths, masks, filters, gradients, patterns, and markers,
- compilation-root boundaries used for incremental refresh,
- the document instance that backs the compiled scene.

That makes it the right surface for inspection, subtree rendering, incremental mutation, and hit testing.

## Ensuring the scene graph exists

`SKSvg` builds and caches the scene graph during normal rendering, and `TryEnsureRetainedSceneGraph(...)` lets callers force that state explicitly:

```csharp
using Svg.Skia;

using var svg = new SKSvg();
svg.Load("diagram.svg");

if (svg.TryEnsureRetainedSceneGraph(out var scene) && scene is not null)
{
    var root = scene.Root;
    var revision = scene.Revision;
}
```

You can also check `HasRetainedSceneGraph` when a boolean is enough.

## Looking up nodes and resources

The most common lookup helpers are:

- `TryGetRetainedSceneNode(SvgElement element, out SvgSceneNode? node)`
- `TryGetRetainedSceneNode(string addressKey, out SvgSceneNode? node)`
- `TryGetRetainedSceneNodeById(string id, out SvgSceneNode? node)`
- `TryGetRetainedSceneNodes(...)`
- `TryGetRetainedSceneResource(...)`
- `TryGetRetainedSceneResourceById(...)`

```csharp
using Svg.Skia;

using var svg = new SKSvg();
svg.Load("diagram.svg");

if (svg.TryGetRetainedSceneNodeById("layer-a", out var node) && node is not null)
{
    var elementId = node.ElementId;
    var hitTargetId = node.HitTestTargetElement?.ID;
    var bounds = node.TransformedBounds;
}
```

## Rendering the whole scene or one subtree

The retained-scene helpers can render the full compiled scene or a single node without going back through the original source text:

```csharp
using Svg.Skia;

using var svg = new SKSvg();
svg.Load("diagram.svg");

using var scenePicture = svg.CreateRetainedSceneGraphPicture();

if (svg.TryGetRetainedSceneNodeById("layer-a", out var node) && node is not null)
{
    using var layerPicture = svg.CreateRetainedSceneNodePicture(node);
}
```

This is useful for previews, editor overlays, diagnostics, or selective export flows.

## Mutating through the retained scene graph

For retained-scene refresh, resolve and mutate the element from the scene graph's own document instance:

```csharp
using System.Drawing;
using Svg;
using Svg.Skia;

using var svg = new SKSvg();
svg.FromSvg(
    "<svg width=\"80\" height=\"40\">" +
    "  <rect id=\"rect-a\" x=\"10\" y=\"8\" width=\"24\" height=\"12\" fill=\"red\" />" +
    "</svg>");

var scene = svg.RetainedSceneGraph!;
var rect = (SvgRectangle)scene.SourceDocument!.GetElementById("rect-a")!;
rect.Fill = new SvgColourServer(Color.BlueViolet);

svg.TryApplyRetainedSceneMutationByIdAndRender("rect-a", new[] { "fill" }, out _);
```

That guarantees the mutation is applied against the same document instance the current retained scene was compiled from.

## When the retained path is not enough

Not every mutation can stay incremental. If a call to `ApplyRetainedSceneMutation(...)` or `TryApplyRetainedSceneMutation...` returns an unsuccessful result, rebuild from the document instead:

```csharp
if (!svg.TryApplyRetainedSceneMutationByIdAndRender("root", new[] { "viewBox" }, out _))
{
    svg.FromSvgDocument(svg.SourceDocument);
}
```

Structural edits and root-scene changes are the common cases that still require a full rebuild.
