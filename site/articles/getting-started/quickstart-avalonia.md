---
title: "Quickstart: Avalonia"
---

# Quickstart: Avalonia

## Skia-backed Avalonia controls

Install:

```bash
dotnet add package Svg.Controls.Skia.Avalonia
```

Render a file directly:

```xml
<Window
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:svg="clr-namespace:Avalonia.Svg.Skia;assembly=Svg.Controls.Skia.Avalonia">
  <svg:Svg Path="/Assets/__AJ_Digital_Camera.svg" />
</Window>
```

Use `SvgImage` in an `Image` control:

```xml
<Image Source="{SvgImage /Assets/__tiger.svg}" />
```

Apply CSS overrides:

```xml
<svg:Svg Path="/Assets/__tiger.svg"
         Css=".Black { fill: #FF0000; }" />
```

Use an SVG-backed brush:

```xml
<Border Background="{SvgResource /Assets/__tiger.svg}" />
```

## Browser and embedded fonts

WebAssembly cannot rely on desktop system fonts. SVG text is resolved by
SkiaSharp, separately from browser CSS fonts. `Svg.Controls.Skia.Avalonia`
automatically bridges Avalonia font collections through `AvaloniaTypefaceProvider`.
Register embedded fonts (for example with `.WithInterFont()`) before loading SVG
images, and use their family names in SVG `font-family`. In the browser, generic
families such as `sans-serif` use Avalonia's configured default font.

Use `SvgSource.ConfigureDefaultSettings(...)` to configure subsequent loads and
reloads. See [Avalonia Fonts and Default Settings](../guides/avalonia-fonts) for
setup, fallback rules, and missing-glyph troubleshooting.

## Avalonia drawing-stack controls

Install:

```bash
dotnet add package Svg.Controls.Avalonia
```

The XAML shape is similar, but the namespace changes:

```xml
<Window
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:svg="clr-namespace:Avalonia.Svg;assembly=Svg.Controls.Avalonia">
  <svg:Svg Path="/Assets/__AJ_Digital_Camera.svg" />
</Window>
```

## Previewer note

When the Avalonia previewer does not automatically load the custom controls, keep the SVG assemblies alive in `BuildAvaloniaApp()`:

```csharp
GC.KeepAlive(typeof(SvgImageExtension).Assembly);
GC.KeepAlive(typeof(Svg.Controls.Skia.Avalonia.Svg).Assembly);
```

## Next steps

- Use [XAML Overview](../xaml/overview) for the differences between the two Avalonia packages.
- Use [Svg Control and SvgImage](../xaml/svg-control-and-svgimage) for the important properties and behaviors.
- Use [SvgResource and Brushes](../xaml/svgresource-and-brushes) for resource dictionaries and code-behind brush creation.
