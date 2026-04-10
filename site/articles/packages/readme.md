---
title: "Packages"
---

# Packages

This section gives package-by-package coverage for every shippable library NuGet in the repository.

Packaged tools such as `Svg.Skia.Converter` and `svgc` stay documented under [Samples and Tools](../reference/samples-and-tools).

## Runtime packages

| Package | Start here when | Guide |
| --- | --- | --- |
| `Svg.Skia` | You want the main SkiaSharp runtime renderer, export helpers, hit testing, shared interaction, animation playback, or Android VectorDrawable support. | [Svg.Skia](svg-skia) |
| `Svg.Animation` | You want the shared SMIL timing engine, animation state evaluation, or host playback backend contracts without taking the full UI package surface. | [Svg.Animation](svg-animation) |
| `Svg.SceneGraph` | You want the retained scene graph, scene nodes, or lower-level compilation layer that sits between the SVG model and final rendering. | [Svg.SceneGraph](svg-scenegraph) |
| `Svg.Model` | You need the intermediate drawable and picture model for inspection, mutation, or custom pipelines. | [Svg.Model](svg-model) |
| `Svg.Custom` | You want the underlying SVG DOM and parser that the renderer consumes, including animation elements. | [Svg.Custom](svg-custom) |
| `ShimSkiaSharp` | You need a cloneable command-model equivalent of key SkiaSharp drawing primitives. | [ShimSkiaSharp](shim-skiasharp) |

## UI packages

| Package | Start here when | Guide |
| --- | --- | --- |
| `Svg.Controls.Skia.Uno` | You want Uno Platform SVG controls backed by `Svg.Skia`, the live Skia canvas, and host-driven animation playback. | [Svg.Controls.Skia.Uno](svg-controls-skia-uno) |
| `Svg.Controls.Skia.Avalonia` | You want the richest Avalonia SVG integration, backed by `Svg.Skia`, real `SkiaSharp.SKPicture` output, and retained native-composition playback where supported. | [Svg.Controls.Skia.Avalonia](svg-controls-skia-avalonia) |
| `Svg.Controls.Avalonia` | You want the same high-level Avalonia SVG concepts but rendered through the Avalonia drawing stack. | [Svg.Controls.Avalonia](svg-controls-avalonia) |
| `Skia.Controls.Avalonia` | You want reusable Avalonia controls and `IImage` wrappers for raw SkiaSharp content, with or without SVG. | [Skia.Controls.Avalonia](skia-controls-avalonia) |

## Editor packages

| Package | Start here when | Guide |
| --- | --- | --- |
| `Svg.Editor.Skia.Avalonia` | You want the full interactive editor workspace and Skia-backed canvas extracted from AvalonDraw. | [Svg.Editor.Skia.Avalonia](svg-editor-skia-avalonia) |
| `Svg.Editor.Avalonia` | You want reusable side panels, standalone editor views, and dialog abstractions without the default workspace. | [Svg.Editor.Avalonia](svg-editor-avalonia) |
| `Svg.Editor.Skia` | You want selection math, path editing, align/distribute helpers, and editor overlay rendering for your own surface. | [Svg.Editor.Skia](svg-editor-skia) |
| `Svg.Editor.Svg` | You want SVG document mutation services, property models, and resource-browser data structures. | [Svg.Editor.Svg](svg-editor-svg) |
| `Svg.Editor.Core` | You want host-agnostic editor session, settings, outline nodes, artboards, clipboard, and history state. | [Svg.Editor.Core](svg-editor-core) |

## Generated-code packages

| Package | Start here when | Guide |
| --- | --- | --- |
| `Svg.CodeGen.Skia` | You want to turn the intermediate picture model into checked-in or pipeline-generated C# code. | [Svg.CodeGen.Skia](svg-codegen-skia) |
| `Svg.SourceGenerator.Skia` | You want `.svg` assets turned into generated `Picture` classes during the build. | [Svg.SourceGenerator.Skia](svg-sourcegenerator-skia) |

## Choosing quickly

- Choose `Svg.Skia` for direct runtime rendering, export, shared interaction, and animation playback.
- Choose `Svg.Animation` when the main task is SVG timing, host backend selection, or animation-controller integration rather than the full rendering surface.
- Choose `Svg.SceneGraph` when you need retained compiled scene nodes and bounds instead of only the intermediate model or the final Skia output.
- Choose `Svg.Controls.Skia.Uno` for Uno Platform usage on the Skia-backed path with host-driven animation playback.
- Choose `Svg.Controls.Skia.Avalonia` for interactive Avalonia usage on the Skia-backed path, especially when retained native composition matters.
- Choose `Svg.Editor.Skia.Avalonia` when you want a reusable SVG editor instead of only a viewer/control package.
- Choose `Svg.Editor.Avalonia`, `Svg.Editor.Skia`, `Svg.Editor.Svg`, and `Svg.Editor.Core` when you need only parts of that editor stack.
- Choose `Svg.Controls.Avalonia` for Avalonia drawing-context integration without the `SKSvg` runtime surface.
- Choose `Svg.Model` and `ShimSkiaSharp` when the main task is inspection, transformation, or code generation rather than direct display.
- Choose `Svg.CodeGen.Skia` or `Svg.SourceGenerator.Skia` when startup cost should move from runtime to build time.
