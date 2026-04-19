---
title: "SvgML.Avalonia"
---

# SvgML.Avalonia

`SvgML.Avalonia` lets you author an SVG element tree directly in Avalonia XAML. Instead of loading an external `.svg` file, you declare `svg`, `rect`, `g`, gradients, filters, text, and related nodes inline, and the package serializes that tree back through the shared `Svg.Skia` loading pipeline.

The NuGet package name is `SvgML.Avalonia`, while the CLR namespace exposed to Avalonia XAML remains `SvgML`.

## Install

```bash
dotnet add package SvgML.Avalonia
```

## Choose this package when

- the SVG should live in XAML resources, templates, or views instead of a separate asset file,
- you want SVG-like element and attribute names in Avalonia markup,
- you want Avalonia styles or animations to target the inline SVG element tree,
- you still want rendering, hit testing, and parsing to stay aligned with the shared `Svg.Skia` runtime.

## Main types

| Type | Role |
| --- | --- |
| `SvgML.svg` | Root Avalonia control that owns the inline SVG tree and renders it |
| `SvgML.element` | Base class for generated SVG element controls |
| `SvgML.elements` | Child collection for nested SVG nodes |
| `SvgML.content` | Text-content wrapper used by text-related nodes |

## Basic inline usage

```xml
<Window
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:svgml="clr-namespace:SvgML;assembly=SvgML.Avalonia">
  <svgml:svg Width="160"
             Height="160"
             viewBox="0 0 100 100">
    <svgml:rect width="100"
                height="100"
                fill="#F3F4F6" />
    <svgml:circle cx="50"
                  cy="50"
                  r="30"
                  fill="#0EA5E9"
                  stroke="#0F172A"
                  stroke-width="4" />
  </svgml:svg>
</Window>
```

The element/property naming stays close to SVG authoring, so attributes such as `stroke-width`, `fill-opacity`, and `viewBox` can stay readable in XAML.

## Runtime behavior

The root `svg` control:

- rebuilds the rendered picture when the inline tree changes,
- exposes `Picture` for direct `SKPicture` access,
- supports `HitTestElements(...)`, `HitTestSceneNodes(...)`, and `TryGetPicturePoint(...)`,
- maps rendered `SvgElement` and retained scene-node results back to the originating inline controls.

That makes the package useful for interactive icons, small diagrams, templated visuals, and editor-like overlays where authored markup and rendered output both need to stay available.

## Styling and animation

Because the SVG tree is made of Avalonia controls, normal Avalonia selectors and animations can target those elements.

```xml
<Style Selector="svgml|rect#accent-bar">
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

For a fuller example, see [SvgML.Avalonia Inline SVG](../xaml/svgml-avalonia-inline-svg) and the `samples/SvgML.Avalonia.Demo` project in the repository.

## When not to choose this package

- Choose [Svg.Controls.Skia.Avalonia](svg-controls-skia-avalonia) when the SVG already exists as a file, stream, or `SvgSource`.
- Choose [Svg.Controls.Avalonia](svg-controls-avalonia) when you want the Avalonia drawing-stack implementation instead of the `Svg.Skia` runtime path.
- Choose [Svg.Skia](svg-skia) when you do not need Avalonia XAML integration at all.

## Related docs

- [XAML Overview](../xaml/overview)
- [SvgML.Avalonia Inline SVG](../xaml/svgml-avalonia-inline-svg)
- [Svg.Controls.Skia.Avalonia](svg-controls-skia-avalonia)
