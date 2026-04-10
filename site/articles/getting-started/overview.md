---
title: "Overview"
---

# Overview

Svg.Skia is a repository, not just a single package. The main entry points are:

| Package or tool | Use it when | Main output | Detailed guide |
| --- | --- | --- |
| `Svg.Skia` | You want to load SVG or Android VectorDrawable content and render with SkiaSharp. | `SKPicture`, bitmap, pdf, xps, svg, hit testing, animation runtime | [Svg.Skia](../packages/svg-skia) |
| `Svg.Model` | You need the intermediate picture-recording model or SVG-related helper types. | `ShimSkiaSharp` command model | [Svg.Model](../packages/svg-model) |
| `Svg.Custom` | You want the underlying SVG DOM used by the renderer. | `SvgDocument`, `SvgElement`, animation DOM, parser APIs | [Svg.Custom](../packages/svg-custom) |
| `ShimSkiaSharp` | You want the cloneable drawing-command model directly. | `SKPicture`, `SKCanvas`, `SKPath`, `SKPaint` | [ShimSkiaSharp](../packages/shim-skiasharp) |
| `Svg.Controls.Skia.Uno` | You want Uno controls that render through the Skia-backed pipeline. | `Svg`, `SvgSource`, hit testing, zoom/pan, animation playback | [Svg.Controls.Skia.Uno](../packages/svg-controls-skia-uno) |
| `Svg.Controls.Skia.Avalonia` | You want Avalonia controls that render through the Skia-backed pipeline. | `Svg`, `SvgImage`, `SvgSource`, `SvgResource`, animation playback, native composition | [Svg.Controls.Skia.Avalonia](../packages/svg-controls-skia-avalonia) |
| `Svg.Controls.Avalonia` | You want Avalonia controls without depending on the Skia-backed Avalonia renderer path. | `Svg`, `SvgImage`, `SvgSource`, `SvgResource` | [Svg.Controls.Avalonia](../packages/svg-controls-avalonia) |
| `Skia.Controls.Avalonia` | You need general-purpose `SKCanvas`, `SKPicture`, `SKBitmap`, or `SKPath` controls in Avalonia. | `SKCanvasControl`, `SKPictureImage`, and related controls | [Skia.Controls.Avalonia](../packages/skia-controls-avalonia) |
| `Svg.Editor.*` | You want reusable SVG editor components, from session/services up to a full Avalonia workspace. | `SvgEditorSession`, editor services, panels, `SvgEditorWorkspace` | [Editor](../editor) |
| `Svg.CodeGen.Skia` | You want to generate checked-in or pipeline-produced C# from the picture model. | C# source from `ShimSkiaSharp.SKPicture` | [Svg.CodeGen.Skia](../packages/svg-codegen-skia) |
| `Svg.SourceGenerator.Skia` | You want compile-time generated `SKPicture` classes from `.svg` assets. | generated `*.svg.cs` files | [Svg.SourceGenerator.Skia](../packages/svg-sourcegenerator-skia) |
| `svgc` sample tool | You want to generate C# source from SVG files outside Roslyn source generators. | C# files on disk | [Source Generator and svgc](../guides/source-generator-and-svgc) |
| `Svg.Skia.Converter` | You want a CLI or global tool that batch-converts files. | png, jpg, jpeg, webp, pdf, xps | [Samples and Tools](../reference/samples-and-tools) |

## Typical paths

### Runtime rendering

Start with `Svg.Skia` if your application already uses SkiaSharp or needs direct access to `SKPicture`.

### Avalonia application

Start with `Svg.Controls.Skia.Avalonia` when the app is already on Avalonia plus Skia and you want the richest interaction and animation surface, including native-composition playback where supported.

Choose `Svg.Controls.Avalonia` when you want the same SVG concepts exposed through the Avalonia drawing stack instead.

### Uno application

Start with `Svg.Controls.Skia.Uno` when the app is on Uno Platform and you want direct `SKCanvasElement` rendering plus async asset loading, hit testing, viewport controls, and host-driven animation playback.

### Embedded editor

Start with `Svg.Editor.Skia.Avalonia` when the target is an editable SVG workspace rather than a viewer-only control.

Move down to the other `Svg.Editor.*` packages when the host needs only panels, dialogs, SVG mutation services, or low-level interaction helpers.

### Static asset generation

Use `Svg.SourceGenerator.Skia` when SVG assets should be compiled into strongly named classes during build.

Use `svgc` when you want the same generated-code approach as a manual step or as part of a custom pipeline.

### Batch conversion

Use `Svg.Skia.Converter` when you need command-line automation for folders, patterns, or repeated exports.

## What this repository emphasizes

- SVG 1.1 DOM coverage with SkiaSharp output and shared animation playback support.
- Android VectorDrawable import and validation coverage.
- Model-level editing, interaction routing, and picture rebuild support.
- Avalonia and Uno controls with host animation backends and resolved-backend diagnostics.
- Verification through unit tests, UI tests, and W3C test-suite assets.

For extensive package-by-package coverage, continue with [Packages](../packages).
