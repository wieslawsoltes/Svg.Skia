---
title: "SvgML.Uno"
---

# SvgML.Uno

`SvgML.Uno` lets you author an SVG element tree directly in Uno Platform XAML. Instead of loading an external `.svg` file, you declare `svg`, `path`, `g`, filters, text, and related nodes inline, and the package serializes that tree back through the shared `Svg.Skia` loading pipeline.

## Package

```bash
dotnet add package SvgML.Uno
```

Current package target:

- `net10.0` via `Uno.Sdk`

## Namespace setup

Uno's XAML source generator still resolves third-party controls through explicit CLR namespace mappings, so the no-prefix pattern is to scope the `SvgML` namespace directly on the inline SVG subtree:

## Main types

| Type | Role |
| --- | --- |
| `SvgML.svg` | Root Uno control that owns the inline SVG tree and renders it |
| `SvgML.element` | Base class for generated SVG element controls |
| `SvgML.elements` | Child collection for nested SVG nodes |
| `SvgML.content` | Text-content wrapper used by text-related nodes |

## Inline example

```xml
<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <svg xmlns="using:SvgML"
       Height="200"
       Stretch="Uniform"
       viewBox="0 0 220 120">
    <path d="M0 0 H220 V120 H0 Z" fill="#0f766e" />
    <path d="M72 32 a28 28 0 1 0 0 56 a28 28 0 1 0 0 -56"
          fill="{Binding ElementName=CircleFillInput, Path=Text, Mode=TwoWay}" />
    <path d="M132 88 L168 30 L196 88 Z"
          fill="#0f172a"
          opacity="0.35" />
  </svg>
</Page>
```

## Notes

- `SvgML.Uno` builds on `Uno.Sdk` with `SkiaRenderer`, so it stays aligned with the same rendering stack as `Svg.Controls.Skia.Uno`.
- CLR-safe SVG names such as `viewBox`, `d`, `fill`, and `id` map cleanly through the current Uno XAML compiler.
- Dash-named members use CLR-safe underscores in Uno XAML, for example `stroke_width` or `font_face`.
- The runtime now maps retained scene nodes back to authored `SvgML.element` controls, so `HitTestElements(...)`, `HitTestSceneNodes(...)`, `GetControlBounds(...)`, and `GetElementForSceneNode(...)` all work from the inline tree.
- Use `HitTestSvgElements(...)` when you need the underlying `SvgElement` model objects rather than the authored XAML controls.
- `xmlns:svgml="using:SvgML"` still works if you prefer an explicit namespace prefix at page scope.
- Avalonia-style assembly `XmlnsDefinition` mapping is not enough for Uno's current source-generator path, so the scoped `xmlns="using:SvgML"` pattern is the reliable prefix-free option today.
- The end-to-end sample lives in `samples/SvgML.Uno.Demo`.

## Related

- [SvgML.Uno Inline SVG](../xaml/svgml-uno-inline-svg)
- [Uno Svg Control](../xaml/uno-svg-control)
- [Samples and Tools](../reference/samples-and-tools)
