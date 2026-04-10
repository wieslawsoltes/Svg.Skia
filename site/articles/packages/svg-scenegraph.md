---
title: "Svg.SceneGraph"
---

# Svg.SceneGraph

`Svg.SceneGraph` owns the retained scene graph used by the repository's rendering pipeline. It compiles SVG content into scene nodes, resources, text runs, and filter/mask/clip structures that can later produce a `ShimSkiaSharp` model or participate in retained animation updates.

## Install

```bash
dotnet add package Svg.SceneGraph
```

## Choose this package when

- you need the retained scene graph rather than only the final `SkiaSharp.SKPicture`,
- you are building lower-level rendering, diagnostics, or scene-inspection tooling,
- you want to compile SVG content into reusable scene documents and nodes,
- you are working on retained animation or native-composition workflows.

## Main concepts

| Area | Main types |
| --- | --- |
| Scene compilation | `SvgSceneRuntime`, `SvgSceneCompiler` |
| Retained graph | `SvgSceneDocument`, `SvgSceneNode` |
| Text and resources | `SvgFontTextRenderer`, `SvgSceneResource` |
| Effects and masks | `SvgSceneFilterContext`, clip/mask/filter compilers |

## What it does

`Svg.SceneGraph` sits between the parsed/model layers and the final runtime renderer:

- consumes SVG input plus asset-loading services
- builds a retained tree of scene nodes with bounds and transforms
- resolves text, resources, paint servers, masks, clips, and filters
- produces model output that `Svg.Skia` can convert to real SkiaSharp objects

It is also the layer used for retained animation and native-composition analysis, where the runtime needs more structure than a flattened final picture.

## Relationship to other packages

- [Svg.Custom](svg-custom) provides the source SVG DOM.
- [Svg.Model](svg-model) provides the intermediate picture/drawable layer.
- [Svg.Skia](svg-skia) uses `Svg.SceneGraph` as part of its retained rendering and animation pipeline.
- [Svg.CodeGen.Skia](svg-codegen-skia) and related tooling work from the resulting model output rather than the raw scene graph itself.

## When not to choose `Svg.SceneGraph`

- Choose [Svg.Skia](svg-skia) when you want a simpler runtime API that already wraps scene compilation.
- Choose [Svg.Model](svg-model) when you only need the intermediate drawable/picture output.
- Choose [ShimSkiaSharp](shim-skiasharp) when you need only the command model and not the scene-graph compilation layer.

## Related docs

- [Rendering Pipeline](../concepts/rendering-pipeline)
- [Picture Model and Rebuild](../concepts/picture-model-and-rebuild)
- [Svg.Skia](svg-skia)
