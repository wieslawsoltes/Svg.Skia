---
title: "Svg.Model"
---

# Svg.Model

`Svg.Model` is the intermediate layer between the parsed SVG DOM and the final SkiaSharp output. It owns the drawable tree, the command-model picture representation, load parameters, hit-testing services, and editing helpers used by the rest of the repository.

## Install

```bash
dotnet add package Svg.Model
```

## Choose this package when

- you need the repository's intermediate model without taking a dependency on the full runtime renderer,
- you want to inspect or mutate the drawable tree before rebuilding output,
- you are implementing a custom asset loader,
- you are building tooling, tests, or code-generation workflows around the model.

## What it contains

| Area | Main types |
| --- | --- |
| Input and parsing | `SvgService`, `SvgParameters` |
| Intermediate drawables | `DrawableBase` and the `Drawables.Elements.*` types |
| Model output | `ShimSkiaSharp.SKPicture`, `SKDrawable` |
| Hit testing | `HitTestService` |
| Editing | `DrawableWalker`, `DrawableEditingExtensions` |
| Asset lookup abstraction | `ISvgAssetLoader` |

## Load a document and build the model

`Svg.Model` can parse content and produce either a drawable tree or a `ShimSkiaSharp.SKPicture`. Asset resolution still requires an `ISvgAssetLoader` implementation, which usually comes from `Svg.Skia` or an application-specific host.

```csharp
using ShimSkiaSharp;
using Svg.Model.Drawables;
using Svg.Model.Services;
using Svg.Skia;

var skiaModel = new SkiaModel(new SKSvgSettings());
var assetLoader = new SkiaSvgAssetLoader(skiaModel);

var document = SvgService.Open("Assets/icon.svg");
var model = SvgService.ToModel(document!, assetLoader, out var drawable, out var bounds);

if (drawable is DrawableBase root)
{
    foreach (var element in HitTestService.HitTestElements(root, new SKPoint(24, 24)))
    {
        Console.WriteLine(element.ID);
    }
}
```

This is the right level when you want to work on the model before choosing a rendering backend.

## Traversing and editing drawables

The editing helpers operate on the drawable tree, not on the final `SkiaSharp.SKPicture`.

```csharp
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Svg.Model.Drawables;
using Svg.Model.Editing;

if (drawable is DrawableBase root)
{
    root.UpdateFills(
        paint => paint.Color.Alpha != 0,
        paint => paint.Color = new SKColor(0x00, 0x7A, 0xCC, paint.Color.Alpha),
        EditMode.CloneOnWrite);

    var updatedModel = root.Snapshot(root.Bounds);
    var updatedPicture = skiaModel.ToSKPicture(updatedModel);
}
```

Common uses:

- theme recoloring,
- removing or simplifying effects,
- precomputing a subset of elements,
- writing diagnostics over the drawable tree.

## `SvgParameters`

`SvgParameters` carries the parsing-time knobs that most applications care about:

- XML entities,
- CSS overrides supplied at load or reload time.

The UI packages use the same type to implement runtime restyling of SVG sources.

## `ISvgAssetLoader`

`Svg.Model` does not hard-code how referenced raster images or font metrics are resolved. `ISvgAssetLoader` abstracts:

- image loading,
- text measurement,
- font metrics,
- text-to-path conversion,
- typeface lookup.

Use this interface directly when integrating the model layer into a new host environment.

## How it fits with the rest of the repo

- [Svg.Custom](svg-custom) provides the parsed SVG DOM.
- `Svg.Model` turns that DOM into drawables and a command-model picture.
- [Svg.Skia](svg-skia) converts the command model into a real `SkiaSharp.SKPicture`.
- [Svg.CodeGen.Skia](svg-codegen-skia) turns the same command model into generated C#.

## When not to choose `Svg.Model`

- Choose [Svg.Skia](svg-skia) when you want the simplest runtime renderer.
- Choose [Svg.Controls.Skia.Avalonia](svg-controls-skia-avalonia) or [Svg.Controls.Avalonia](svg-controls-avalonia) when the main integration point is Avalonia XAML.
- Choose [ShimSkiaSharp](shim-skiasharp) when you need the command-model primitives directly, outside SVG parsing concerns.

## Related docs

- [Package Architecture](../concepts/package-architecture)
- [Picture Model and Rebuild](../concepts/picture-model-and-rebuild)
- [Hit Testing and Scene Inspection](../guides/hit-testing-and-model-editing)
