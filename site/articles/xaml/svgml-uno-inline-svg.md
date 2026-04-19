---
title: "SvgML.Uno Inline SVG"
---

# SvgML.Uno Inline SVG

`SvgML.Uno` is the Uno Platform XAML-first path in this repository. It turns an inline tree of SVG-like controls into SVG markup, then feeds that markup through the shared `Svg.Skia` renderer.

## Setup

Install the package:

```bash
dotnet add package SvgML.Uno
```

Use the CLR namespace in Uno XAML:

```xml
xmlns:svgml="using:SvgML"
```

## Inline XAML example

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

## What maps cleanly from SVG

- Element names such as `svg`, `path`, `defs`, `filter`, and `text` stay close to authored SVG.
- CLR-safe attribute names such as `viewBox`, `d`, `fill`, and `opacity` stay close to the SVG vocabulary.
- Dash-named members use CLR-safe underscores in Uno XAML, for example `stroke_width` or `font_face`.
- Text or control bindings can feed attribute values directly, which makes small interactive diagrams practical inside Uno pages.

## Current shape of the Uno lane

- The package itself targets `net10.0` through `Uno.Sdk`.
- The current repository sample is desktop-focused: `samples/SvgML.Uno.Demo`.
- The sample emphasizes string-backed SVG attributes because those map most directly through the current Uno XAML compiler.

## When to use SvgML.Uno versus SvgSource

- Choose `SvgML.Uno` when the visual is authored inline and should stay near page markup or bindings.
- Choose `Uno.Svg.Skia` with external `.svg` assets when the source graphic already exists as a file or should be shared outside XAML.
