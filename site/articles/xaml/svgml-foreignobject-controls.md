---
title: "SvgML foreignObject Controls"
---

# SvgML foreignObject Controls

`foreignObject` is the SvgML host element for native UI controls. It follows the SVG vocabulary instead of introducing a custom host element, and it works in the Avalonia, Uno, and .NET MAUI SvgML packages.

Use it when part of an inline SVG scene should be a real platform control: a button, text editor, slider, checkbox, or any other native view/control.

## Where it can be used

`foreignObject` follows SVG graphical-element placement for normal scene hosting, and SvgML also accepts it in text-related nodes for native inline-control slots:

- directly under `svg`,
- inside `g` and other transformed containers,
- inside `text`, `tspan`, or `textPath` in SvgML text layout to reserve an inline text slot.

In normal scene placement, the SVG `x`, `y`, `width`, and `height` attributes define the hosted-control rectangle. In text placement, the hosted control participates in text layout by reserving inline advance before the native control is arranged over the rendered SVG.

## Sizing and layout

SvgML applies the SVG sizing rules to the host slot:

- `x` and `y` default to `0`.
- Positive `width` and `height` create an explicit slot.
- Explicit non-positive `width` or `height` disables the hosted slot.
- Missing `width` or `height` falls back to the measured native control size.
- SVG units are supported for host geometry, including user units, `px`, `%`, `em`, `ex`, `in`, `cm`, `mm`, `pt`, and `pc`.
- Percentages resolve against the root `viewBox` when available, otherwise against the rendered picture bounds.
- `em` and `ex` resolve from the inherited SVG font size, including `font-size` set through a `style` declaration.
- Group transforms and `foreignObject` transforms are applied before mapping picture-space bounds into platform UI coordinates.

The native child is runtime UI interop. It is hosted above the Skia-rendered SVG surface and is not painted into the `SKPicture` or exported as vector content.

## Avalonia example

`SvgML.Avalonia` maps the SvgML namespace into both `https://github.com/svgml` and the standard Avalonia namespace. The sample below uses unprefixed SvgML elements with normal Avalonia controls as `foreignObject` children:

```xml
<Panel xmlns="https://github.com/avaloniaui"
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <svg Height="340"
       Stretch="Uniform"
       viewBox="0 0 520 260">
    <rect x="0" y="0" width="520" height="260" fill="#F8FAFC" />

    <text x="32" y="94" fill="#334155" style="font-size:16px;">
      <tspan>Approve </tspan>
      <foreignObject width="110" height="34">
        <Button Content="Publish"
                MinWidth="110"
                Padding="16,6" />
      </foreignObject>
      <tspan> to send the revision live.</tspan>
    </text>

    <g transform="translate(54 150)">
      <rect x="0" y="0" width="230" height="80" fill="#F8FAFC" stroke="#CBD5E1" />
      <foreignObject x="20" y="22" width="168" height="42">
        <TextBox Text="northwind.csv" />
      </foreignObject>
    </g>
  </svg>
</Panel>
```

Avalonia hosts the native child as part of the root `svg` control's visual/logical tree, so focus, input, and normal control styling continue to work.

## .NET MAUI example

`SvgML.Maui` exposes `https://github.com/svgml`. Use a MAUI namespace alias for the hosted child controls:

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:maui="http://schemas.microsoft.com/dotnet/2021/maui">

  <svg xmlns="https://github.com/svgml"
       HeightRequest="340"
       Stretch="Uniform"
       viewBox="0 0 520 260">
    <rect x="0" y="0" width="520" height="260" fill="#F8FAFC" />

    <text x="32" y="94" fill="#334155" style="font-size:16px;">
      <tspan>Approve </tspan>
      <foreignObject>
        <maui:Button Text="Publish"
                     WidthRequest="112"
                     HeightRequest="34"
                     Padding="16,8" />
      </foreignObject>
      <tspan> to send the revision live.</tspan>
    </text>

    <path d="M82 210 H438"
          stroke="#CBD5E1"
          stroke-width="2"
          stroke-linecap="round" />
  </svg>
</ContentPage>
```

Normal MAUI builds include a package weaving step so XamlC can see SVG dashed CLR names such as `stroke-width`, `stroke-linecap`, and `fill-opacity`. Do not use underscore aliases such as `stroke_width` in authored MAUI XAML.

## Uno example

Uno uses the `using:SvgML` namespace form. Keep native Uno controls in the normal presentation namespace:

```xml
<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="http://schemas.microsoft.com/winfx/2006/xaml/presentation">

  <svg xmlns="using:SvgML"
       Height="340"
       Stretch="Uniform"
       viewBox="0 0 520 260">
    <path d="M0 0 H520 V260 H0 Z" fill="#F8FAFC" />

    <text x="32" y="94" fill="#334155" style="font-size:16px;">
      <tspan xml:space="preserve">Approve </tspan>
      <foreignObject>
        <ui:Button Content="Publish"
                   MinWidth="110"
                   Height="34"
                   Padding="16,6" />
      </foreignObject>
      <tspan xml:space="preserve"> to send the revision live.</tspan>
    </text>

    <foreignObject transform="translate(214 180)">
      <ui:Slider Minimum="0"
                 Maximum="100"
                 Value="62"
                 Width="230"
                 Height="46" />
    </foreignObject>
  </svg>
</Page>
```

Uno currently works best when hosted-control size is provided by the native child (`Width`, `Height`, `MinWidth`, and related properties) or by a transform-positioned `foreignObject`. For dash-named SVG attributes outside `style`, use the existing Uno-safe member names where required by the Uno XAML compiler, or put the SVG declaration in `style`.

## Samples

The repository demos contain end-to-end pages for the hosted-control cases:

- `samples/SvgML.Avalonia.Demo`: `Hosted Controls` and `Scene Controls` tabs.
- `samples/SvgML.Maui.Demo`: `Hosted Controls` and `Scene Controls` shell pages.
- `samples/SvgML.Uno.Demo`: `Hosted Controls` and `Scene Controls` pivot items.
