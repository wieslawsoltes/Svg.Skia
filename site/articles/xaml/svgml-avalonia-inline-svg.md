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
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:svgml="clr-namespace:SvgML;assembly=SvgML.Avalonia">
```

## Inline resource example

```xml
<Window.Resources>
  <svgml:svg x:Key="InlineIcon"
             x:Shared="False"
             viewBox="0 0 500 500">
    <svgml:defs>
      <svgml:filter id="noise"
                    x="0%"
                    y="0%"
                    width="100%"
                    height="100%">
        <svgml:feTurbulence type="fractalNoise"
                            result="NOISE"
                            baseFrequency="0 0.000001"
                            numOctaves="2" />
        <svgml:feDisplacementMap in="SourceGraphic"
                                 in2="NOISE"
                                 scale="30"
                                 xChannelSelector="R"
                                 yChannelSelector="R" />
      </svgml:filter>
    </svgml:defs>

    <svgml:rect width="100%"
                height="100%"
                fill="lightgray" />
    <svgml:rect id="r2"
                width="10%"
                height="10%"
                x="0%"
                y="45%"
                fill="green"
                filter="#noise" />
  </svgml:svg>
</Window.Resources>
```

That pattern is useful when the icon or effect graph should live next to the XAML that consumes it.

## Animating inline SVG elements

Avalonia style selectors can target the inline SVG controls directly:

```xml
<Window.Styles>
  <Style Selector="svgml|rect#r2">
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

## Rendering and hit testing

The root `svg` control exposes:

- `Picture` for direct `SKPicture` access,
- `HitTestElements(...)` and `HitTestSceneNodes(...)`,
- `TryGetPicturePoint(...)` and `TryGetPictureRect(...)`,
- `GetElementForSceneNode(...)` for mapping retained-scene results back to the authored controls.

This is more than a markup serializer. The authored control tree and renderer state stay connected.

## When to use SvgML.Avalonia versus SvgSource

- Choose `SvgML.Avalonia` when the visual is authored inline and should be styleable as part of the Avalonia view.
- Choose `Avalonia.Svg.Skia` or `Avalonia.Svg` when the source is already an external SVG asset or a reusable `SvgSource`.

The repository sample at `samples/SvgML.Avalonia.Demo` shows both the reusable-resource pattern and selector-driven animation.
