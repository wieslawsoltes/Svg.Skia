---
title: "Svg.Skia Documentation"
---

# Documentation

Svg.Skia spans a few related areas:

- `Svg.Skia` and `Svg.Model` handle SVG parsing, picture-model generation, SkiaSharp output, and model mutation.
- `Svg.Controls.Avalonia` and `Svg.Controls.Skia.Avalonia` expose Avalonia controls, images, and brush helpers.
- `Svg.Controls.Skia.Uno` exposes a Skia-backed Uno control and reusable `SvgSource` resources.
- `Svg.Controls.Skia.Maui` exposes a Skia-backed .NET MAUI control and reusable `SvgSource` resources.
- `SvgML.Maui` exposes .NET MAUI XAML-authored SVG trees and `foreignObject` control hosting.
- `Svg.Editor.*` exposes the reusable AvalonDraw editor stack, from document/session services up to the interactive Avalonia workspace.
- `Skia.Controls.Avalonia` hosts general-purpose Skia controls for Avalonia.
- `Svg.CodeGen.Skia`, `Svg.SourceGenerator.Skia`, `svgc`, and `Svg.Skia.Converter` cover generated code and CLI workflows.
- [Packages](packages) gives dedicated coverage for every shippable library NuGet in the repo.

## Suggested reading order

1. [Getting Started](getting-started) for package selection and the first render.
2. [Packages](packages) for library-by-library installation, responsibilities, and usage patterns.
3. [Editor](editor) when the goal is embedding or composing the reusable SVG editor stack.
4. [Concepts](concepts) to understand how files, models, pictures, and Avalonia resources relate.
5. [Guides](guides) for scenario-focused tasks such as exporting images, hit testing, interaction, animation playback, or generating code.
6. [XAML Usage](xaml) when the primary integration point is Avalonia, .NET MAUI, or Uno.
7. [Reference](reference) for package maps, SVG support matrices, samples, licensing, and the docs pipeline.

## SVG Support

- [SVG 1.1 Static Subset Support](reference/svg-11-static-subset-support) documents the current static renderer surface against SVG 1.1 spec areas.
- [SVG 2 Static Subset Support](reference/svg-2-static-subset-support) documents implemented, partial, parsed, and deferred SVG 2 static-subset features.

## Generated API

- Use [API Reference](../api) for the generated surface area across the documented assemblies, including the `Svg.Editor.*` packages.
- Use [API Coverage Index](reference/api-coverage-index) to see which projects feed the API site.
