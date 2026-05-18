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
      <circle cx="150" cy="50" r="40" fill="black" opacity="0.3" />
    </svg>

    <Entry x:Name="CircleFill" Text="red" />
  </VerticalStackLayout>
</ContentPage>
```

## What maps cleanly from SVG

- Element names such as `svg`, `rect`, `circle`, `defs`, `linearGradient`, and `stop` stay close to authored SVG.
- CLR-safe attribute names such as `viewBox`, `fill`, and `opacity` stay close to the SVG vocabulary.
- Dash-named SVG attributes such as `stroke-width`, `fill-opacity`, and `font-size` are supported by the built MAUI package. The assembly is self-weaved before MAUI XamlC compiles consuming XAML, so authored markup should use the SVG names rather than underscore aliases.
- Text or control bindings can feed attribute values directly, which makes small interactive diagrams practical inside MAUI pages.

## SVG 2 authoring notes

SvgML.Maui exposes the supported SVG 2 static subset directly on the inline tree. The root `svg` control can set loader options, and generated elements include the SVG 2 properties used by the shared renderer.

```xml
<svg xmlns="https://github.com/svgml"
     ProcessingMode="SecureStatic"
     ExternalResources="SameDocumentAndDataOnly"
     PreferSvg2Href="True"
     viewBox="0 0 160 90">
  <defs>
    <filter id="shadow">
      <feDropShadow dx="2"
                    dy="2"
                    stdDeviation="2"
                    flood-color="#0F172A"
                    flood-opacity="0.35" />
    </filter>
  </defs>

  <rect x="16"
        y="14"
        width="128"
        height="44"
        pathLength="400"
        fill="#E0F2FE"
        stroke="#0369A1"
        stroke-width="4"
        paint-order="stroke fill"
        vector-effect="non-scaling-stroke"
        transform-box="fill-box"
        transform-origin="50% 50%"
        filter="url(#shadow)" />

  <text fill="#0F172A" white-space="pre">
    <textPath path="M18 76 H142" side="right">SvgML SVG 2</textPath>
  </text>
</svg>
```

CSS-only SVG 2 features such as `mix-blend-mode`, `isolation`, CSS `d`, and CSS geometry should be authored through `style`, `Css`, or `CurrentCss`.

## Namespace note

Scope each inline SVG subtree with `xmlns="https://github.com/svgml"` so SVG nodes can be authored without prefixes. Keep native MAUI controls in the MAUI namespace when they are hosted by `foreignObject`.

## Native controls with foreignObject

`foreignObject` hosts a single native MAUI `View` child. It can reserve a slot inside SVG text or place controls by scene geometry:

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:maui="http://schemas.microsoft.com/dotnet/2021/maui">

  <svg xmlns="https://github.com/svgml"
       HeightRequest="260"
       Stretch="Uniform"
       viewBox="0 0 420 180">
    <text x="24" y="46" fill="#334155" style="font-size:16px;">
      <tspan>Approve </tspan>
      <foreignObject>
        <maui:Button Text="Publish"
                     WidthRequest="112"
                     HeightRequest="34" />
      </foreignObject>
      <tspan> before release.</tspan>
    </text>

    <foreignObject x="24" y="92" width="180" height="36">
      <maui:Entry Text="Design systems" />
    </foreignObject>
  </svg>
</ContentPage>
```

For inline text, explicit `width`/`height` or the measured native view size controls the reserved advance. For scene placement, `x`, `y`, `width`, and `height` define the SVG slot. See [SvgML foreignObject Controls](svgml-foreignobject-controls) for sizing and unit details.

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

The repository sample at `samples/SvgML.Maui.Demo` shows bound SVG properties, inline hosted controls, and scene-level hosted controls.
