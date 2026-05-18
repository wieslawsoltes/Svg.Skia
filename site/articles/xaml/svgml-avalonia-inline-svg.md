---
title: "SvgML.Avalonia Inline SVG"
---

# SvgML.Avalonia Inline SVG

`SvgML.Avalonia` is the Avalonia XAML-first path in this repository. It turns an inline tree of SVG-like controls into SVG markup, then feeds that markup through the shared `Svg.Skia` renderer.

The NuGet package name is `SvgML.Avalonia`, while the CLR namespace used from XAML remains `SvgML`.

## Namespace setup

```xml
<Window
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
```

`SvgML.Avalonia` also maps `SvgML` into Avalonia's standard `https://github.com/avaloniaui` namespace, so the repository demo can author `svg`, `defs`, `rect`, `text`, and `foreignObject` without a prefix in normal Avalonia markup.

## Inline resource example

```xml
<Window.Resources>
  <svg x:Key="InlineIcon"
       x:Shared="False"
       viewBox="0 0 500 500">
    <defs>
      <filter id="noise"
              x="0%"
              y="0%"
              width="100%"
              height="100%">
        <feTurbulence type="fractalNoise"
                      result="NOISE"
                      baseFrequency="0 0.000001"
                      numOctaves="2" />
        <feDisplacementMap in="SourceGraphic"
                           in2="NOISE"
                           scale="30"
                           xChannelSelector="R"
                           yChannelSelector="R" />
      </filter>
    </defs>

    <rect width="100%"
          height="100%"
          fill="lightgray" />
    <rect id="r2"
          width="10%"
          height="10%"
          x="0%"
          y="45%"
          fill="green"
          filter="#noise" />
  </svg>
</Window.Resources>
```

That pattern is useful when the icon or effect graph should live next to the XAML that consumes it.

## Animating inline SVG elements

Avalonia style selectors can target the inline SVG controls directly:

```xml
<Window.Styles>
  <Style Selector=":is(rect)[id=r2]">
    <Style.Animations>
      <Animation Duration="0:0:1"
                 IterationCount="Infinite"
                 PlaybackDirection="Alternate">
        <KeyFrame Cue="0%">
          <Setter Property="x" Value="0%" />
        </KeyFrame>
        <KeyFrame Cue="100%">
          <Setter Property="x" Value="90%" />
        </KeyFrame>
      </Animation>
    </Style.Animations>
  </Style>
</Window.Styles>
```

The same approach works for filter elements such as `feTurbulence`, which makes `SvgML.Avalonia` a good fit for small procedural effects or animated UI accents.

## SVG 2 authoring notes

SvgML.Avalonia exposes the supported SVG 2 static subset directly on the inline tree. The root `svg` control can set loader options, and generated elements include the SVG 2 properties used by the shared renderer.

```xml
<svg ProcessingMode="SecureStatic"
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

## Rendering and hit testing

The root `svg` control exposes:

- `Picture` for direct `SKPicture` access,
- `HitTestElements(...)` and `HitTestSceneNodes(...)`,
- `TryGetPicturePoint(...)` and `TryGetPictureRect(...)`,
- `GetElementForSceneNode(...)` for mapping retained-scene results back to the authored controls.

This is more than a markup serializer. The authored control tree and renderer state stay connected.

## Native controls with foreignObject

SvgML uses SVG `foreignObject` as the native-control host. The same element works inside text flow and as a normal scene element:

```xml
<svg Height="260"
     Stretch="Uniform"
     viewBox="0 0 420 180">
  <text x="24" y="46" fill="#334155" style="font-size:16px;">
    <tspan>Approve </tspan>
    <foreignObject width="110" height="34">
      <Button Content="Publish" MinWidth="110" />
    </foreignObject>
    <tspan> before release.</tspan>
  </text>

  <g transform="translate(24 90)">
    <rect x="0" y="0" width="240" height="56" fill="#F8FAFC" stroke="#CBD5E1" />
    <foreignObject x="16" y="12" width="180" height="32">
      <TextBox Text="Design systems" />
    </foreignObject>
  </g>
</svg>
```

Inline `foreignObject` reserves text advance from explicit `width`/`height` or from the measured native control. Scene `foreignObject` uses SVG `x`, `y`, `width`, `height`, and inherited transforms. See [SvgML foreignObject Controls](svgml-foreignobject-controls) for the cross-platform layout details.

## When to use SvgML.Avalonia versus SvgSource

- Choose `SvgML.Avalonia` when the visual is authored inline and should be styleable as part of the Avalonia view.
- Choose `Avalonia.Svg.Skia` or `Avalonia.Svg` when the source is already an external SVG asset or a reusable `SvgSource`.

The repository sample at `samples/SvgML.Avalonia.Demo` shows reusable resources, selector-driven animation, inline hosted controls, and scene-level hosted controls.
