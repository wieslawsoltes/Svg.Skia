---
title: "Svg.Controls.Skia.Maui"
---

# Svg.Controls.Skia.Maui

`Svg.Controls.Skia.Maui` brings the Skia-backed SVG control model to .NET MAUI. It wraps `Svg.Skia`, renders through `SkiaSharp.Views.Maui.Controls.SKCanvasView`, and matches the reusable-source control surface used by the Uno Skia package where that maps cleanly to MAUI.

## Install

```bash
dotnet add package Svg.Controls.Skia.Maui
```

The control uses SkiaSharp's MAUI host integration. Register it during app startup:

```csharp
using SkiaSharp.Views.Maui.Controls.Hosting;

builder
    .UseMauiApp<App>()
    .UseSkiaSharp();
```

## Choose this package when

- your app uses .NET MAUI and wants to display external `.svg` assets or inline SVG strings through a live Skia canvas,
- you want an `Svg` control with `Path`, `Source`, `SvgSource`, `Stretch`, `StretchDirection`, `EnableCache`, `Wireframe`, `DisableFilters`, `Zoom`, `PanX`, and `PanY`,
- you need control-coordinate hit testing through `TryGetPicturePoint(...)` and `HitTestElements(...)`,
- you want host-driven SVG animation playback backed by the shared `SKSvg` runtime,
- you want reusable `SvgSource` resources that can be cloned and restyled per control.

Use `SvgML.Maui` instead when the SVG element tree itself should be authored inline as MAUI XAML and when SVG `foreignObject` should host native MAUI controls inside the authored tree.

## Main types

| Type | Role |
| --- | --- |
| `Maui.Svg.Skia.Svg` | MAUI view for direct SVG display on `SKCanvasView` |
| `Maui.Svg.Skia.SvgSource` | Reusable, cloneable, reloadable source object |
| `Maui.Svg.Skia.StretchDirection` | MAUI-side equivalent of the Avalonia and Uno stretch-direction API |

## Basic XAML usage

Add SVG files as MAUI package assets, for example:

```xml
<MauiAsset Include="Resources\Raw\**" LogicalName="%(RecursiveDir)%(Filename)%(Extension)" />
```

Then reference the logical package-asset name from XAML:

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:svg="https://github.com/svgskia/maui">
  <svg:Svg Path="Icons/tiger.svg"
           HeightRequest="220"
           Stretch="Uniform"
           EnableCache="True" />
</ContentPage>
```

Relative paths and leading-slash paths resolve through `FileSystem.OpenAppPackageFileAsync(...)`. Absolute file paths and `http` or `https` URLs are also supported.

## Reusable `SvgSource` resources

```xml
<ContentPage.Resources>
  <svg:SvgSource x:Key="TigerSource" Path="Icons/tiger.svg" />
</ContentPage.Resources>

<svg:Svg SvgSource="{StaticResource TigerSource}" HeightRequest="220" />
```

The control clones an external `SvgSource` before applying per-control CSS, wireframe, or filter settings, so one shared resource can safely back multiple controls with different runtime styling.

## Inline source strings

```xml
<svg:Svg HeightRequest="120"
         Source="&lt;svg width=&quot;100&quot; height=&quot;100&quot;&gt;&lt;circle cx=&quot;50&quot; cy=&quot;50&quot; r=&quot;40&quot; fill=&quot;#0284C7&quot; /&gt;&lt;/svg&gt;" />
```

For larger authored trees, prefer `SvgML.Maui` so the SVG nodes stay readable in XAML.

## Async path loading

```csharp
using Maui.Svg.Skia;
using Svg.Model;

var source = await SvgSource.LoadAsync(
    "Icons/tiger.svg",
    parameters: new SvgParameters(null, ".accent { fill: #2563eb; }"));

await source.ReLoadAsync(new SvgParameters(null, ".accent { fill: #ef4444; }"));
```

Use the synchronous loaders for inline SVG strings, `Stream`, and `SvgDocument` inputs.

## Animation playback

The MAUI `Svg` control exposes the same host-driven animation surface as the other Skia-backed UI packages:

- `AnimationBackend`
- `AnimationFrameInterval`
- `AnimationPlaybackRate`
- `ActualAnimationBackend`
- `AnimationBackendFallbackReason`
- `AnimationBackendResolution`
- `AnimationBackendCapabilities`

The control shares the same backend enum:

- `Default`
- `Manual`
- `DispatcherTimer`
- `RenderLoop`
- `NativeComposition`

MAUI currently resolves automatic playback to `DispatcherTimer`. `RenderLoop` and `NativeComposition` requests fall back to `DispatcherTimer` or `Manual` depending on host availability.

## Related docs

- [Samples and Tools](../reference/samples-and-tools)
- [SvgML.Maui](svgml-maui)
- [SvgML.Maui Inline SVG](../xaml/svgml-maui-inline-svg)
- [Interaction and Animation](../guides/interaction-and-animation)
- [Svg.Controls.Skia.Uno](svg-controls-skia-uno)
- [Svg.Controls.Skia.Avalonia](svg-controls-skia-avalonia)
