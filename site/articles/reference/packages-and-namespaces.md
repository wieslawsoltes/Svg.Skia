---
title: "Packages and Namespaces"
---

# Packages and Namespaces

## Runtime and UI packages

| Package | Main namespace | Purpose |
| --- | --- | --- |
| [Svg.Skia](../packages/svg-skia) | `Svg.Skia` | Core runtime renderer and export helpers |
| [Svg.Animation](../packages/svg-animation) | `Svg.Skia` | Shared SVG animation runtime and host playback contracts |
| [Svg.SceneGraph](../packages/svg-scenegraph) | `Svg.Model.Drawables` | Retained scene graph compilation and scene-node model |
| [Svg.Model](../packages/svg-model) | `Svg.Model` | Picture model, parameters, and services |
| [Svg.Custom](../packages/svg-custom) | `Svg` | Vendored SVG document model package |
| [Svg.Controls.Skia.Uno](../packages/svg-controls-skia-uno) | `Uno.Svg.Skia` | Skia-backed Uno control and reusable `SvgSource` |
| [Svg.Controls.Skia.Avalonia](../packages/svg-controls-skia-avalonia) | `Avalonia.Svg.Skia` | Skia-backed Avalonia controls, images, resources |
| [Svg.Controls.Avalonia](../packages/svg-controls-avalonia) | `Avalonia.Svg` | Avalonia drawing-stack controls, images, resources |
| [Skia.Controls.Avalonia](../packages/skia-controls-avalonia) | `Avalonia.Controls.Skia` | General-purpose Avalonia Skia controls |
| [ShimSkiaSharp](../packages/shim-skiasharp) | `ShimSkiaSharp` | Intermediate picture-recorder command model |

## Editor packages

| Package | Main namespace | Purpose |
| --- | --- | --- |
| [Svg.Editor.Core](../packages/svg-editor-core) | `Svg.Editor.Core` | Editor session, settings, outline, artboard, clipboard, and history primitives |
| [Svg.Editor.Svg](../packages/svg-editor-svg) | `Svg.Editor.Svg` | SVG mutation services and property/resource models |
| [Svg.Editor.Skia](../packages/svg-editor-skia) | `Svg.Editor.Skia` | Selection math, path editing, align/distribute, and overlay rendering |
| [Svg.Editor.Avalonia](../packages/svg-editor-avalonia) | `Svg.Editor.Avalonia` | Reusable Avalonia panels, editor views, and dialog abstractions |
| [Svg.Editor.Skia.Avalonia](../packages/svg-editor-skia-avalonia) | `Svg.Editor.Skia.Avalonia` | Interactive editor surface and composed workspace |

## Generated-code packages

| Package | Main namespace | Purpose |
| --- | --- | --- |
| [Svg.CodeGen.Skia](../packages/svg-codegen-skia) | `Svg.CodeGen.Skia` | Direct C# generation from the picture model |
| [Svg.SourceGenerator.Skia](../packages/svg-sourcegenerator-skia) | `Svg.SourceGenerator.Skia` | Incremental generator package for `.svg` additional files |

## Tools

| Tool | Project path | Purpose |
| --- | --- | --- |
| `Svg.Skia.Converter` | `samples/Svg.Skia.Converter` | File and directory conversion CLI |
| `svgc` | `samples/svgc` | Manual SVG-to-C# generator CLI |

## Generated API reference

See [API Coverage Index](api-coverage-index) for the exact projects included in the generated Lunet API site.
