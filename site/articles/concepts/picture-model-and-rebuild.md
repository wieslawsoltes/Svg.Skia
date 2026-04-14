---
title: "Picture Model and Rebuild"
---

# Picture Model and Rebuild

One of the more useful parts of `Svg.Skia` is that it does not stop at "load and draw". The runtime keeps several layers alive, and each layer has a different rebuild story.

## The available layers

After loading, `SKSvg` can expose:

- `SourceDocument` for authored SVG DOM work,
- `RetainedSceneGraph` for compiled-node inspection and incremental mutation refresh,
- `Model` for low-level `ShimSkiaSharp.SKPicture` command editing,
- `Picture` for the current native `SkiaSharp.SKPicture`.

Choose the layer that matches the kind of change you are making.

## Full document rebuild

When the DOM changes structurally or when the simplest path is good enough, mutate the document and call `FromSvgDocument(...)` again:

```csharp
using System.Drawing;
using Svg;
using Svg.Skia;

using var svg = new SKSvg();
svg.FromSvg("<svg width=\"10\" height=\"10\"><rect id=\"r\" width=\"10\" height=\"10\" fill=\"red\" /></svg>");

var rect = (SvgRectangle)svg.SourceDocument!.GetElementById("r")!;
rect.Fill = new SvgColourServer(Color.BlueViolet);

svg.FromSvgDocument(svg.SourceDocument);
```

This recompiles the retained scene and regenerates the current `Picture`.

## Retained-scene mutation refresh

For small localized DOM edits, update the scene-backed document and ask the retained scene to recompile only the affected compilation roots:

```csharp
using System.Drawing;
using Svg;
using Svg.Skia;

using var svg = new SKSvg();
svg.FromSvg("<svg width=\"10\" height=\"10\"><rect id=\"r\" width=\"10\" height=\"10\" fill=\"red\" /></svg>");

var scene = svg.RetainedSceneGraph!;
var rect = (SvgRectangle)scene.SourceDocument!.GetElementById("r")!;
rect.Fill = new SvgColourServer(Color.BlueViolet);

svg.TryApplyRetainedSceneMutationByIdAndRender("r", new[] { "fill" }, out var result);
```

This path keeps the current `Picture` refreshed without paying for a full rebuild every time.

## Accessing the model

`SKSvg.Model` exposes the `ShimSkiaSharp.SKPicture` command tree. This is the structure that `SkiaModel` later turns into `SkiaSharp.SKPicture`.

## Editing commands

Because the model is a plain command graph, you can inspect and mutate it before rebuilding the final picture:

```csharp
using System.Linq;
using ShimSkiaSharp;
using Svg.Skia;

var svg = new SKSvg();
svg.FromSvg("<svg width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"red\" /></svg>");

foreach (var cmd in svg.Model?.Commands?.OfType<DrawPathCanvasCommand>() ?? Enumerable.Empty<DrawPathCanvasCommand>())
{
    if (cmd.Paint?.Color is { } color)
    {
        cmd.Paint.Color = new SKColor(color.Red, color.Red, color.Red, color.Alpha);
    }
}

svg.RebuildFromModel();
```

## Avalonia rebuild flow

The same ideas exist in the Avalonia wrappers:

- `Avalonia.Svg.Skia.SvgSource.RebuildFromModel()`
- `Avalonia.Svg.Skia.SvgSource.ReLoad(...)`
- `Avalonia.Svg.Skia.SvgSource.LoadFromSvgDocument(...)`
- `Avalonia.Svg.SvgSource.RebuildFromModel()`

This makes it possible to keep an SVG-backed image source in XAML while still choosing between DOM rebuilds, parameter reloads, and low-level model mutation from code.

## Cloning

The Avalonia image and source types provide cloning helpers so you can keep one original asset and derive modified variants without mutating shared state:

- `SvgSource.Clone()`
- `SvgImage.Clone()`

## When to use each rebuild path

Use `FromSvgDocument(...)` when:

- you added or removed elements,
- the root document changed,
- the simplest rebuild path is more important than maximum refresh performance.

Use retained-scene mutation refresh when:

- one element or subtree changed,
- you know which attributes changed,
- you want to reuse the existing compiled scene as much as possible.

Use `Model` plus `RebuildFromModel()` when:

- CSS alone is not expressive enough,
- the asset must stay embedded but the commands need a runtime tweak,
- you want to derive monochrome or recolored variants from one source,
- you need testing hooks around specific command types.
