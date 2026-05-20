---
title: "SvgML.Avalonia"
---

# SvgML.Avalonia

`SvgML.Avalonia` lets you author an SVG element tree directly in Avalonia XAML. Instead of loading an external `.svg` file, you declare `svg`, `rect`, `g`, gradients, filters, text, `foreignObject`, and related nodes inline, and the package serializes that tree back through the shared `Svg.Skia` loading pipeline.

The NuGet package name is `SvgML.Avalonia`, while the CLR namespace exposed to Avalonia XAML remains `SvgML`.

## Install

```bash
dotnet add package SvgML.Avalonia
```

## Choose this package when

- the SVG should live in XAML resources, templates, or views instead of a separate asset file,
- you want SVG-like element and attribute names in Avalonia markup,
- you want SVG `foreignObject` to place real Avalonia controls inside text flow or scene geometry,
- you want Avalonia styles or animations to target the inline SVG element tree,
- you still want rendering, hit testing, and parsing to stay aligned with the shared `Svg.Skia` runtime.

## Main types

| Type | Role |
| --- | --- |
| `SvgML.svg` | Root Avalonia control that owns the inline SVG tree and renders it |
| `SvgML.element` | Base class for generated SVG element controls |
| `SvgML.elements` | Child collection for nested SVG nodes |
| `SvgML.content` | Text-content backing node used by text-related nodes; authored XAML can normally use literal text |
| `SvgML.foreignObject` | SVG-native host for an Avalonia `Control` inside text flow or scene geometry |

## Basic inline usage

```xml
<Window
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <svg Width="160"
       Height="160"
       viewBox="0 0 100 100">
    <rect width="100"
          height="100"
          fill="#F3F4F6" />
    <circle cx="50"
            cy="50"
            r="30"
            fill="#0EA5E9"
            stroke="#0F172A"
            stroke-width="4" />
  </svg>
</Window>
```

The element/property naming stays close to SVG authoring, so attributes such as `stroke-width`, `fill-opacity`, and `viewBox` can stay readable in XAML.

## SVG 2 static subset authoring

The generated SvgML surface is kept aligned with the shared `Svg.Skia` SVG 2 static subset. The root `svg` control also forwards load options into the document loader.

| Area | SvgML.Avalonia contract |
| --- | --- |
| Root load options | `ProcessingMode`, `ExternalResources`, `PreserveUnknownElements`, and `PreferSvg2Href` are Avalonia styled properties on `svg`. Changing them reloads the inline SVG tree. |
| Geometry | Basic shapes expose `pathLength`; `symbol` exposes SVG 2 `x`, `y`, `width`, `height`, `refX`, and `refY`. |
| Paint and transforms | Visual elements expose `paint-order`, `vector-effect`, `transform-box`, and `transform-origin`. |
| Text | `textPath` exposes inline `path` data and `side`; visual/text elements expose `white-space`, `text-overflow`, `inline-size`, `shape-inside`, and `shape-subtract` for supported or preserve-only text contracts. |
| Filters and masks | `mask` exposes `mask-type`, and the SVG 2 `feDropShadow` filter primitive is available for inline filter graphs. |
| CSS-only features | `mix-blend-mode`, `isolation`, CSS `d`, CSS geometry, and CSS custom properties should be authored through `style`, `Css`, or `CurrentCss` rather than as direct SvgML properties. |

## Runtime behavior

The root `svg` control:

- rebuilds the rendered picture when the inline tree changes,
- exposes `Picture` for direct `SKPicture` access,
- supports `HitTestElements(...)`, `HitTestSceneNodes(...)`, and `TryGetPicturePoint(...)`,
- maps rendered `SvgElement` and retained scene-node results back to the originating inline controls.

That makes the package useful for interactive icons, small diagrams, templated visuals, and editor-like overlays where authored markup and rendered output both need to stay available.

## Native hosted controls

`foreignObject` is the public hosted-control API. It can be used inline in `text`/`tspan` or as a normal SVG scene element under `svg` or `g`.

```xml
<svg Height="220" Stretch="Uniform" viewBox="0 0 360 160">
  <text x="24" y="48" fill="#334155" style="font-size:16px;">
    <tspan>Open </tspan>
    <foreignObject width="120" height="34">
      <Button Content="Preview" MinWidth="120" />
    </foreignObject>
    <tspan> in review mode.</tspan>
  </text>
</svg>
```

The native control is hosted by the Avalonia visual tree while the SVG surface continues to render through `Svg.Skia`. See [SvgML foreignObject Controls](../xaml/svgml-foreignobject-controls) for cross-platform sizing and layout rules.

## Styling and animation

Because the SVG tree is made of Avalonia controls, normal Avalonia selectors and animations can target those elements.

```xml
<Style Selector=":is(rect)[id=accent-bar]">
  <Style.Animations>
    <Animation Duration="0:0:1.2"
               IterationCount="Infinite"
               PlaybackDirection="Alternate">
      <KeyFrame Cue="0%">
        <Setter Property="x" Value="0%" />
      </KeyFrame>
      <KeyFrame Cue="100%">
        <Setter Property="x" Value="85%" />
      </KeyFrame>
    </Animation>
  </Style.Animations>
</Style>
```

For fuller examples, see [SvgML.Avalonia Inline SVG](../xaml/svgml-avalonia-inline-svg), [SvgML foreignObject Controls](../xaml/svgml-foreignobject-controls), and the `samples/SvgML.Avalonia.Demo` project in the repository.

## When not to choose this package

- Choose [Svg.Controls.Skia.Avalonia](svg-controls-skia-avalonia) when the SVG already exists as a file, stream, or `SvgSource`.
- Choose [Svg.Controls.Avalonia](svg-controls-avalonia) when you want the Avalonia drawing-stack implementation instead of the `Svg.Skia` runtime path.
- Choose [Svg.Skia](svg-skia) when you do not need Avalonia XAML integration at all.

## Related docs

- [XAML Overview](../xaml/overview)
- [SvgML.Avalonia Inline SVG](../xaml/svgml-avalonia-inline-svg)
- [SvgML foreignObject Controls](../xaml/svgml-foreignobject-controls)
- [Svg.Controls.Skia.Avalonia](svg-controls-skia-avalonia)
