---
title: "SvgML.Maui"
---

# SvgML.Maui

`SvgML.Maui` lets you author an SVG element tree directly in .NET MAUI XAML. Instead of loading an external `.svg` file, you declare `svg`, `rect`, `g`, gradients, filters, text, and related nodes inline, and the package serializes that tree back through the shared `Svg.Skia` loading pipeline.

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
| `SvgML.content` | Text-content wrapper used by text-related nodes |
| `SvgML.AppHostBuilderExtensions` | Registers the inline element types for MAUI startup |

## Inline example

The MAUI XAML surface stays close to authored SVG for CLR-safe names, but it should be brought in through an explicit XML alias instead of the protected MAUI default namespace:

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:svgml="clr-namespace:SvgML;assembly=SvgML.Maui"
             x:Class="SvgML.Maui.Demo.MainPage">

  <VerticalStackLayout Padding="30,0" Spacing="25">
    <svgml:svg viewBox="0 0 200 100">
      <svgml:defs>
        <svgml:linearGradient id="gradient" x1="0%" y1="0%" x2="0" y2="100%">
          <svgml:stop offset="0%" style="stop-color:skyblue;" />
          <svgml:stop offset="100%" style="stop-color:seagreen;" />
        </svgml:linearGradient>
      </svgml:defs>

      <svgml:rect x="0" y="0" width="100%" height="100%" fill="url(#gradient)" />
      <svgml:circle cx="50" cy="50" r="40"
                    fill="{Binding Source={x:Reference CircleFill}, Path=Text, Mode=TwoWay}" />
    </svgml:svg>

    <Entry x:Name="CircleFill" Text="red" />
  </VerticalStackLayout>
</ContentPage>
```

## Repository layout

- Library source: `src/SvgML.Maui`
- Shared manual serialization/loading layer: `src/SvgML.Avalonia/Manual`
- Demo app: `samples/SvgML.Maui.Demo`
- MAUI-specific SDK pin: `src/SvgML.Maui/global.json`

## Notes

- The package reuses the same `Svg.Skia` renderer as the Avalonia and Uno stacks.
- Use `xmlns:svgml="clr-namespace:SvgML;assembly=SvgML.Maui"` in page XAML. MAUI's protected default namespace cannot be extended safely when `SvgML.Maui` is consumed as a source project reference.
- Dash-named members use CLR-safe underscores in MAUI XAML, for example `stroke_width`, `fill_opacity`, or `font_face`.
- The MAUI runtime now keeps retained-scene mappings for the inline tree, so `HitTestElements(...)`, `HitTestSceneNodes(...)`, `GetControlBounds(...)`, and `GetElementForSceneNode(...)` can work against authored `SvgML.element` controls.
- Use `HitTestSvgElements(...)` when you need the underlying `SvgElement` instances instead of the XAML-authored controls.
- In this repository, `SvgML.Maui` builds in a dedicated MAUI lane rather than inside the main `Svg.Skia.slnx`.
- The imported MAUI sample is the best end-to-end reference for current usage.

## Related

- [SvgML.Maui Inline SVG](../xaml/svgml-maui-inline-svg)
- [Samples and Tools](../reference/samples-and-tools)
