---
title: "Editor"
---

# Editor

The `Svg.Editor.*` packages provide a reusable SVG editor stack that applications can embed whole or in parts. UnoDraw is the desktop sample, built with Uno Platform and a direct Skia canvas. The existing Avalonia workspace and panels remain available for Avalonia integrations.

## Start here when

- you want the tabbed UnoDraw workspace inside an Uno desktop app,
- you want a ready-made editor surface and side-panel workspace inside an Avalonia app,
- you need reusable SVG mutation, layer, symbol, swatch, and property services,
- you want editor dialogs and inspectors without taking the full canvas implementation,
- you are integrating the editor into your own shell and need host-controlled menus, preview, and file pickers.

## Package map

| Package | Role | Guide |
| --- | --- | --- |
| `Svg.Editor.Core` | Shared editor session state, settings, selection ids, outline nodes, artboards, clipboard, and undo/redo history. | [Svg.Editor.Core](../packages/svg-editor-core) |
| `Svg.Editor.Svg` | SVG document mutation services and reusable models for layers, patterns, swatches, symbols, styles, and properties. | [Svg.Editor.Svg](../packages/svg-editor-svg) |
| `Svg.Editor.Skia` | Render-model editing helpers, selection math, path editing, alignment, and overlay rendering. | [Svg.Editor.Skia](../packages/svg-editor-skia) |
| `Svg.Editor.Skia.Uno` | Direct Skia canvas, tabbed documents, tool controls, inspector and command search used by UnoDraw. | [Svg.Editor.Skia.Uno](../packages/svg-editor-skia-uno) |
| `Svg.Editor.Avalonia` | Reusable Avalonia controls, standalone editor views, and dialog/file-dialog abstractions. | [Svg.Editor.Avalonia](../packages/svg-editor-avalonia) |
| `Svg.Editor.Skia.Avalonia` | Interactive Avalonia canvas and composed workspace. | [Svg.Editor.Skia.Avalonia](../packages/svg-editor-skia-avalonia) |

## Recommended reading order

1. [Architecture](architecture) for dependency order and package boundaries.
2. [Uno workspace](../packages/svg-editor-skia-uno) for UnoDraw, or [Embedding SvgEditorWorkspace](embedding-workspace) for the Avalonia workspace.
3. [Composing Panels and Dialogs](composing-panels-and-dialogs) if you are building your own shell.
4. [Rendering and Svg Services](rendering-and-svg-services) if you need lower-level editing services.
5. [API Reference](../../api) when you want the generated type-level surface for the editor assemblies.

## What the sample still owns

`samples/UnoDraw` provides the desktop window, branding, startup document and close lifecycle. It targets Windows, macOS and Linux/X11 through Uno's `net10.0-desktop` host. SVG parsing, document transactions, geometry and rendering stay in the reusable libraries.

The [UnoDraw guide](https://github.com/wieslawsoltes/Svg.Skia/blob/master/docs/unodraw.md) records keyboard controls, renderer versus authoring coverage, migration gaps and validation commands.

If you only need the editor package documentation per NuGet, continue with [Packages](../packages). If you want the exact generated API inputs, see [API Coverage Index](../reference/api-coverage-index).
