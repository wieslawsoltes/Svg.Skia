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

Uno's current XAML source generator still expects third-party controls to arrive through explicit CLR namespace mappings. The reliable prefix-free pattern is to scope `xmlns="using:SvgML"` on the inline SVG subtree itself.

## Inline XAML example

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

## What maps cleanly from SVG

- Element names such as `svg`, `path`, `defs`, `filter`, and `text` stay close to authored SVG.
- CLR-safe attribute names such as `viewBox`, `d`, `fill`, and `opacity` stay close to the SVG vocabulary.
- Dash-named members use CLR-safe underscores in Uno XAML, for example `stroke_width` or `font_face`.
- Dash-named SVG declarations can also be placed in `style`, for example `style="stroke-width:2;stroke-linecap:round;"`.
- Text or control bindings can feed attribute values directly, which makes small interactive diagrams practical inside Uno pages.

## Native controls with foreignObject

`foreignObject` hosts a native Uno `UIElement` child. It can reserve text flow inside `text` and `tspan`, or it can place native controls in the SVG scene:

```xml
<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="http://schemas.microsoft.com/winfx/2006/xaml/presentation">

  <svg xmlns="using:SvgML"
       Height="260"
       Stretch="Uniform"
       viewBox="0 0 420 180">
    <text x="24" y="46" fill="#334155" style="font-size:16px;">
      <tspan xml:space="preserve">Approve </tspan>
      <foreignObject>
        <ui:Button Content="Publish"
                   MinWidth="110"
                   Height="34" />
      </foreignObject>
      <tspan xml:space="preserve"> before release.</tspan>
    </text>

    <foreignObject transform="translate(24 92)">
      <ui:TextBox Text="Design systems"
                  Width="180"
                  Height="36" />
    </foreignObject>
  </svg>
</Page>
```

Uno currently works best when hosted-control size comes from the native child (`Width`, `Height`, `MinWidth`, and related properties). Use `transform` on `foreignObject` for scene placement when the Uno XAML compiler cannot convert a literal `SvgUnit` value for `x`, `y`, `width`, or `height`. See [SvgML foreignObject Controls](svgml-foreignobject-controls) for the shared layout model.

## Current shape of the Uno lane

- The package itself targets `net10.0` through `Uno.Sdk`.
- The current repository sample is desktop-focused: `samples/SvgML.Uno.Demo`.
- The sample emphasizes string-backed SVG attributes and `style` declarations where those map most directly through the current Uno XAML compiler.
- The runtime exposes authored-element hit testing and retained-scene mapping through `HitTestElements(...)`, `HitTestSceneNodes(...)`, `GetControlBounds(...)`, and `GetElementForSceneNode(...)`.
- Use `HitTestSvgElements(...)` when editor or diagnostics code needs the underlying `SvgElement` instances.

## When to use SvgML.Uno versus SvgSource

- Choose `SvgML.Uno` when the visual is authored inline and should stay near page markup or bindings.
- Choose `Uno.Svg.Skia` with external `.svg` assets when the source graphic already exists as a file or should be shared outside XAML.
