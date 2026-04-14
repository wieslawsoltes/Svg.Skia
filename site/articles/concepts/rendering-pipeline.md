---
title: "Rendering Pipeline"
---

# Rendering Pipeline

At a high level, the render path is:

1. Parse markup into SVG document objects.
2. Compile the document into a retained `SvgSceneDocument`.
3. Render that retained scene into the intermediate `ShimSkiaSharp.SKPicture` model.
4. Materialize the shim model as a `SkiaSharp.SKPicture`.
5. Draw the picture, export it, or expose it through Avalonia and Uno.

## Parsing

`Svg.Custom` supplies the SVG document object model. `SvgService` is the main entry point used across the repository to open or parse documents.

## Retained-scene compilation

`Svg.SceneGraph` compiles a `SvgDocument` or `SvgFragment` into `SvgSceneDocument`. That retained scene graph stores:

- the compiled `SvgSceneNode` tree,
- resource dependency indexes,
- compilation-root boundaries for incremental mutation refresh,
- hit-test geometry and bounds,
- the document instance that backs the current scene.

`SvgSceneRuntime.TryCompile(...)` is the repository's main entry point for this phase.

## Shim-model generation

`SvgSceneDocument.CreateModel()` renders the retained scene into a `ShimSkiaSharp.SKPicture` command model. That model is still CPU-side data, which makes it safe to inspect, clone, or mutate before creating a native SkiaSharp picture.

## Picture materialization

`Svg.Skia.SKSvg` owns:

- the original source information,
- the current `SvgDocument`,
- the retained `SvgSceneDocument`,
- the `ShimSkiaSharp` model, and
- the lazily created `SkiaSharp.SKPicture`.

During a normal `Load(...)`, `FromSvg(...)`, or `FromSvgDocument(...)` call, `SKSvg` compiles the retained scene, renders a shim model from it, and refreshes the current `Picture`.

After that:

- `SourceDocument` exposes the authored DOM,
- `RetainedSceneGraph` exposes the compiled scene,
- `Model` exposes the shim picture model,
- `Picture` exposes the native `SkiaSharp.SKPicture`.

That separation is what allows the runtime to support DOM mutation, retained-scene refresh, hit testing, model editing, and export without reparsing source text on every operation.

## Avalonia integration

The Avalonia packages sit on top of the same conceptual steps:

- `SvgSource` loads and retains the source data.
- `SvgImage` exposes it as an `IImage`.
- `Svg` wraps it as a control.
- `SvgResourceExtension` turns it into a reusable brush.

## Output paths

Once you have an `SKPicture`, the repository exposes helpers for:

- drawing to `SKCanvas`,
- raster output through `ToBitmap()` and `ToImage()`,
- vector output through `ToSvg()`,
- document output through `ToPdf()` and `ToXps()`.

For the practical loading and mutation workflows built on top of this pipeline, see [Loading SVG, SvgDocument, and VectorDrawable](../guides/loading-svg-and-vectordrawable), [Retained Scene Graph Usage](../guides/retained-scene-graph-usage), and [Performance and Retained-Scene Refresh](../guides/performance-and-retained-scene-refresh).
