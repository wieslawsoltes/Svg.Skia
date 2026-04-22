---
title: "SvgML.Maui"
---

# SvgML.Maui

`SvgML.Maui` lets you author an SVG element tree directly in .NET MAUI XAML. Instead of loading an external `.svg` file, you declare `svg`, `rect`, `g`, gradients, filters, text, `foreignObject`, and related nodes inline, and the package serializes that tree back through the shared `Svg.Skia` loading pipeline.

## Package

```bash
dotnet add package SvgML.Maui
```

Current packaged targets:

- `net10.0-android`
- `net10.0-ios`
- `net10.0-maccatalyst`

## Startup

`SvgML.Maui` depends on the MAUI SkiaSharp hosting extension plus the package's own builder extension:

```csharp
using SkiaSharp.Views.Maui.Controls.Hosting;
using SvgML;

builder
    .UseMauiApp<App>()
    .UseSkiaSharp()
    .UseSvgML();
```

## Main types

| Type | Role |
| --- | --- |
| `SvgML.svg` | Root MAUI view that owns the inline SVG tree and renders it |
| `SvgML.element` | Base class for generated SVG element controls |
| `SvgML.elements` | Child collection for nested SVG nodes |
| `SvgML.content` | Text-content backing node used by text-related nodes; authored XAML can normally use literal text |
| `SvgML.foreignObject` | SVG-native host for a MAUI `View` inside text flow or scene geometry |
| `SvgML.AppHostBuilderExtensions` | Registers the inline element types for MAUI startup |

## Inline example

The MAUI XAML surface stays close to authored SVG. Scope the inline SVG subtree to the SvgML XML namespace URL and author SVG nodes without a prefix:

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="SvgML.Maui.Demo.MainPage">

  <VerticalStackLayout Padding="30,0" Spacing="25">
    <svg xmlns="https://github.com/svgml"
         viewBox="0 0 200 100">
      <defs>
        <linearGradient id="gradient" x1="0%" y1="0%" x2="0" y2="100%">
          <stop offset="0%" style="stop-color:skyblue;" />
          <stop offset="100%" style="stop-color:seagreen;" />
        </linearGradient>
      </defs>

      <rect x="0" y="0" width="100%" height="100%" fill="url(#gradient)" />
      <circle cx="50" cy="50" r="40"
              fill="{Binding Source={x:Reference CircleFill}, Path=Text, Mode=TwoWay}" />
    </svg>

    <Entry x:Name="CircleFill" Text="red" />
  </VerticalStackLayout>
</ContentPage>
```

## Native hosted controls

`foreignObject` is the public hosted-control API. It can host one native MAUI `View` inside SVG text flow or at a rectangle in the SVG scene.

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:maui="http://schemas.microsoft.com/dotnet/2021/maui">

  <svg xmlns="https://github.com/svgml"
       HeightRequest="220"
       Stretch="Uniform"
       viewBox="0 0 360 160">
    <text x="24" y="48" fill="#334155" style="font-size:16px;">
      <tspan>Open </tspan>
      <foreignObject>
        <maui:Button Text="Preview"
                     WidthRequest="120"
                     HeightRequest="34" />
      </foreignObject>
      <tspan> in review mode.</tspan>
    </text>

    <path d="M24 112 H260"
          stroke="#CBD5E1"
          stroke-width="2"
          stroke-linecap="round" />
  </svg>
</ContentPage>
```

The MAUI package is self-weaved during normal builds so MAUI XamlC can resolve SVG dashed names such as `stroke-width`, `stroke-linecap`, `fill-opacity`, and `font-size`. Authored MAUI XAML should use SVG names, not underscore aliases.

See [SvgML foreignObject Controls](../xaml/svgml-foreignobject-controls) for sizing and layout rules.

## Repository layout

- Library source: `src/SvgML.Maui`
- Shared manual serialization/loading layer: `src/SvgML.Avalonia/Manual`
- Demo app: `samples/SvgML.Maui.Demo`
- MAUI-specific SDK pin: `src/SvgML.Maui/global.json`

## Notes

- The package reuses the same `Svg.Skia` renderer as the Avalonia and Uno stacks.
- Scope each SvgML subtree with `xmlns="https://github.com/svgml"` so SVG nodes can be authored without prefixes.
- Dash-named SVG members are authored with SVG names in MAUI XAML, for example `stroke-width`, `fill-opacity`, or `font-size`.
- The MAUI runtime now keeps retained-scene mappings for the inline tree, so `HitTestElements(...)`, `HitTestSceneNodes(...)`, `GetControlBounds(...)`, and `GetElementForSceneNode(...)` can work against authored `SvgML.element` controls.
- Use `HitTestSvgElements(...)` when you need the underlying `SvgElement` instances instead of the XAML-authored controls.
- In this repository, `SvgML.Maui` builds in a dedicated MAUI lane rather than inside the main `Svg.Skia.slnx`.
- The imported MAUI sample is the best end-to-end reference for current usage.

## Related

- [SvgML.Maui Inline SVG](../xaml/svgml-maui-inline-svg)
- [SvgML foreignObject Controls](../xaml/svgml-foreignobject-controls)
- [Samples and Tools](../reference/samples-and-tools)
