---
title: "SvgML.Uno"
---

# SvgML.Uno

`SvgML.Uno` lets you author an SVG element tree directly in Uno Platform XAML. Instead of loading an external `.svg` file, you declare `svg`, `path`, `g`, filters, text, `foreignObject`, and related nodes inline, and the package serializes that tree back through the shared `Svg.Skia` loading pipeline.

## Package

```bash
dotnet add package SvgML.Uno
```

Current package target:

- `net10.0` via `Uno.Sdk`

The current Uno `6.5.x` packages used in this repository only ship `net9.0` and `net10.0` assets, so `net8.0` is not available on the Uno lane without a broader Uno stack change.

## Namespace setup

Uno's XAML source generator still resolves third-party controls through explicit CLR namespace mappings, so the no-prefix pattern is to scope the `SvgML` namespace directly on the inline SVG subtree:

```xml
<svg xmlns="using:SvgML"
     Height="200"
     Stretch="Uniform"
     viewBox="0 0 220 120">
  <path d="M0 0 H220 V120 H0 Z" fill="#0f766e" />
</svg>
```

## Main types

| Type | Role |
| --- | --- |
| `SvgML.svg` | Root Uno control that owns the inline SVG tree and renders it |
| `SvgML.element` | Base class for generated SVG element controls |
| `SvgML.elements` | Child collection for nested SVG nodes |
| `SvgML.content` | Text-content backing node used by text-related nodes; authored XAML can normally use literal text |
| `SvgML.foreignObject` | SVG-native host for a Uno `UIElement` inside text flow or scene geometry |

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

## Native hosted controls

`foreignObject` is the public hosted-control API. It can reserve text flow inside `text`/`tspan` and can place native Uno controls in the SVG scene:

```xml
<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="http://schemas.microsoft.com/winfx/2006/xaml/presentation">

  <svg xmlns="using:SvgML"
       Height="220"
       Stretch="Uniform"
       viewBox="0 0 360 160">
    <text x="24" y="48" fill="#334155" style="font-size:16px;">
      <tspan xml:space="preserve">Open </tspan>
      <foreignObject>
        <ui:Button Content="Preview"
                   MinWidth="120"
                   Height="34" />
      </foreignObject>
      <tspan xml:space="preserve"> in review mode.</tspan>
    </text>
  </svg>
</Page>
```

Uno currently works best when hosted-control size is supplied by the native child (`Width`, `Height`, `MinWidth`, and related properties). See [SvgML foreignObject Controls](../xaml/svgml-foreignobject-controls) for the shared layout model and Uno-specific authoring notes.

## SVG 2 static subset authoring

The generated Uno SvgML surface is kept aligned with the shared `Svg.Skia` SVG 2 static subset. CLR-safe SVG names are available directly; dash-named members use underscore property names in Uno XAML or can be written through `style`.

| Area | SvgML.Uno contract |
| --- | --- |
| Root load options | `ProcessingMode`, `ExternalResources`, `PreserveUnknownElements`, and `PreferSvg2Href` are dependency properties on `svg`. Changing them reloads the inline SVG tree. |
| Geometry | Basic shapes expose `pathLength`; `symbol` exposes SVG 2 `x`, `y`, `width`, `height`, `refX`, and `refY`. |
| Paint and transforms | Visual elements expose `paint_order`, `vector_effect`, `transform_box`, and `transform_origin`, with serialized SVG names preserved as `paint-order`, `vector-effect`, `transform-box`, and `transform-origin`. |
| Text | `textPath` exposes inline `path` data and `side`; visual/text elements expose `white_space`, `text_overflow`, `inline_size`, `shape_inside`, and `shape_subtract` for supported or preserve-only text contracts. |
| Filters and masks | `mask` exposes `mask_type`, and the SVG 2 `feDropShadow` filter primitive is available for inline filter graphs. |
| CSS-only features | `mix-blend-mode`, `isolation`, CSS `d`, CSS geometry, and CSS custom properties should be authored through `style`, `Css`, or `CurrentCss` rather than as direct SvgML properties. |

## Notes

- `SvgML.Uno` builds on `Uno.Sdk` with `SkiaRenderer`, so it stays aligned with the same rendering stack as `Svg.Controls.Skia.Uno`.
- CLR-safe SVG names such as `viewBox`, `d`, `fill`, and `id` map cleanly through the current Uno XAML compiler.
- Dash-named members use CLR-safe underscores in Uno XAML, for example `stroke_width` or `font_face`.
- Dash-named SVG declarations can be authored through `style` when that is more reliable for Uno XAML, for example `style="stroke-width:2;stroke-linecap:round;"`.
- The runtime now maps retained scene nodes back to authored `SvgML.element` controls, so `HitTestElements(...)`, `HitTestSceneNodes(...)`, `GetControlBounds(...)`, and `GetElementForSceneNode(...)` all work from the inline tree.
- Use `HitTestSvgElements(...)` when you need the underlying `SvgElement` model objects rather than the authored XAML controls.
- Avalonia-style assembly `XmlnsDefinition` mapping is not enough for Uno's current source-generator path, so the scoped `xmlns="using:SvgML"` pattern is the reliable prefix-free option today.
- The end-to-end sample lives in `samples/SvgML.Uno.Demo`.

## Related

- [SvgML.Uno Inline SVG](../xaml/svgml-uno-inline-svg)
- [SvgML foreignObject Controls](../xaml/svgml-foreignobject-controls)
- [Uno Svg Control](../xaml/uno-svg-control)
- [Samples and Tools](../reference/samples-and-tools)
