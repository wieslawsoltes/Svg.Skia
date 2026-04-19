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

Uno uses the CLR namespace directly:

```xml
xmlns:svgml="using:SvgML"
```

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
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:svgml="using:SvgML">

  <svgml:svg Height="200"
             Stretch="Uniform"
             viewBox="0 0 220 120">
    <svgml:path d="M0 0 H220 V120 H0 Z" fill="#0f766e" />
    <svgml:path d="M72 32 a28 28 0 1 0 0 56 a28 28 0 1 0 0 -56"
                fill="{Binding ElementName=CircleFillInput, Path=Text, Mode=TwoWay}" />
    <svgml:path d="M132 88 L168 30 L196 88 Z"
                fill="#0f172a"
                opacity="0.35" />
  </svgml:svg>
</Page>
```

## Notes

- `SvgML.Uno` builds on `Uno.Sdk` with `SkiaRenderer`, so it stays aligned with the same rendering stack as `Svg.Controls.Skia.Uno`.
- The current demo focuses on string-backed SVG attributes such as `viewBox`, `d`, `fill`, and `id`, which map cleanly through the current Uno XAML compiler.
- The end-to-end sample lives in `samples/SvgML.Uno.Demo`.

## Related

- [SvgML.Uno Inline SVG](../xaml/svgml-uno-inline-svg)
- [Uno Svg Control](../xaml/uno-svg-control)
- [Samples and Tools](../reference/samples-and-tools)
