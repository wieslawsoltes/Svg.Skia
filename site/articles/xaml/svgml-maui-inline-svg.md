---
title: "SvgML.Maui Inline SVG"
---

# SvgML.Maui Inline SVG

`SvgML.Maui` is the .NET MAUI XAML-first path in this repository. It turns an inline tree of SVG-like controls into SVG markup, then feeds that markup through the shared `Svg.Skia` renderer.

## Setup

Install the package:

```bash
dotnet add package SvgML.Maui
```

Register the MAUI integrations during app startup:

```csharp
using SkiaSharp.Views.Maui.Controls.Hosting;
using SvgML;

builder
    .UseMauiApp<App>()
    .UseSkiaSharp()
    .UseSvgML();
```

## Inline XAML example

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
      <svgml:circle cx="150" cy="50" r="40" fill="black" opacity="0.3" />
    </svgml:svg>

    <Entry x:Name="CircleFill" Text="red" />
  </VerticalStackLayout>
</ContentPage>
```

## What maps cleanly from SVG

- Element names such as `svg`, `rect`, `circle`, `defs`, `linearGradient`, and `stop` stay close to authored SVG.
- CLR-safe attribute names such as `viewBox`, `fill`, and `opacity` stay close to the SVG vocabulary.
- Dash-named members use CLR-safe underscores in MAUI XAML, for example `stroke_width`, `fill_opacity`, or `font_face`.
- Text or control bindings can feed attribute values directly, which makes small interactive diagrams practical inside MAUI pages.

## Namespace note

Use an explicit MAUI alias such as `xmlns:svgml="clr-namespace:SvgML;assembly=SvgML.Maui"` for inline SVG nodes. The protected MAUI default namespace cannot safely absorb `SvgML.Maui` when the library is referenced from source, so explicit aliasing is the reliable authoring path.

## Current package lane

The repository currently ships `SvgML.Maui` for:

- Android
- iOS
- Mac Catalyst

The MAUI-specific build and pack path lives under `src/SvgML.Maui`.

The runtime exposes authored-element hit testing and retained-scene mapping through `HitTestElements(...)`, `HitTestSceneNodes(...)`, `GetControlBounds(...)`, and `GetElementForSceneNode(...)`. Use `HitTestSvgElements(...)` when tooling needs the raw `SvgElement` model instances instead.

## When to use SvgML.Maui versus external SVG assets

- Choose `SvgML.Maui` when the visual is authored inline and should stay near page markup or bindings.
- Choose external `.svg` assets plus `Svg.Skia` when the source graphic already exists as a file or should be shared outside XAML.

The repository sample at `samples/SvgML.Maui.Demo` shows the current end-to-end setup.
