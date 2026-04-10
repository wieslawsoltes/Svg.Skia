---
title: "Svg.Skia"
---

# Svg.Skia

`Svg.Skia` is the main runtime rendering package in this repository. It loads SVG content into a `SkiaSharp.SKPicture`, preserves the intermediate model and drawable tree, and adds export, interaction, hit-testing, and animation helpers around that workflow.

## Install

```bash
dotnet add package Svg.Skia
```

## Choose this package when

- your application already uses `SkiaSharp`,
- you need direct access to `SkiaSharp.SKPicture`,
- you want runtime export to bitmap, pdf, or xps formats,
- you need hit testing or model rebuild after editing,
- you need shared pointer routing or animation playback outside a UI-specific package,
- you want Android `VectorDrawable` input support.

## Main types

| Type | Role |
| --- | --- |
| `SKSvg` | Main load, render, save, hit-test, and rebuild entry point |
| `SkiaModel` | Converts the intermediate `ShimSkiaSharp` model to real SkiaSharp objects |
| `SKSvgSettings` | Controls color-space and font-resolution behavior |
| `ITypefaceProvider` | Plug-in point for custom typeface lookup |
| `SkiaSvgAssetLoader` | `Svg.Model` asset-loader implementation for images and fonts |
| `SvgInteractionDispatcher` | Framework-neutral pointer routing and cursor-resolution helper |
| `SvgAnimationHostBackend` | Shared host playback backend enum used by the UI packages |

## Typical workflow

1. Create `SKSvg`.
2. Load from file, stream, XML, string, or `SvgDocument`.
3. Read `Picture` for drawing.
4. Optionally inspect `Model` and `Drawable`.
5. Save or rebuild after edits.

## Load and draw

```csharp
using SkiaSharp;
using Svg.Skia;

using var svg = new SKSvg();

if (svg.Load("Assets/icon.svg") is { } picture)
{
    using var surface = SKSurface.Create(new SKImageInfo(256, 256));
    surface.Canvas.Clear(SKColors.Transparent);
    surface.Canvas.DrawPicture(picture);
}
```

`SKSvg.Load(...)` auto-detects `.svg`, `.svgz`, and Android `VectorDrawable` XML when loading from a file path.

## Exporting

`Svg.Skia` is the package to choose when rendering should end in a file or stream.

```csharp
using SkiaSharp;
using Svg.Skia;

using var svg = new SKSvg();

if (svg.Load("Assets/icon.svg") is not null)
{
    svg.Save("artifacts/icon.png", SKColors.Transparent, SKEncodedImageFormat.Png, 100, 2f, 2f);
    svg.Picture?.ToPdf("artifacts/icon.pdf", SKColors.White, 1f, 1f);
    svg.Picture?.ToXps("artifacts/icon.xps", SKColors.White, 1f, 1f);
}
```

Use this package instead of a UI package when the output target is not an Avalonia control.

## Hit testing and rebuild

`SKSvg` keeps both the intermediate model and the drawable tree, which makes it the best runtime package for inspection-oriented scenarios.

```csharp
using System.Linq;
using ShimSkiaSharp;
using Svg.Skia;

using var svg = new SKSvg();

if (svg.Load("Assets/icon.svg") is not null)
{
    var hit = svg.HitTestElements(new SKPoint(24, 24)).FirstOrDefault();
    if (hit is not null)
    {
        Console.WriteLine(hit.ID);
    }

    var rebuilt = svg.RebuildFromModel();
}
```

Use `TryGetPicturePoint` or `TryGetPictureRect` when the pointer coordinates come from a transformed canvas rather than picture space.

For routed input targets, use `HitTestTopmostElement(...)` instead of `HitTestElements(...)`.

## Shared interaction

`Svg.Skia` now includes a shared input-routing surface through `SvgInteractionDispatcher`.

Use it when a host wants:

- topmost-target routing instead of broad inspection-only hit testing,
- tunnel, target, and bubble phases,
- pointer capture to the pressed target,
- cursor resolution,
- optional compatibility dispatch back into `SvgElement` mouse events.

The dispatcher accepts `SvgPointerInput` and raises `SvgPointerEventArgs` through its `Dispatched` event.

## Shared animation runtime

`SKSvg` now owns the shared animation runtime for the repository.

The main members are:

- `HasAnimations`
- `AnimationTime`
- `SetAnimationTime(...)`
- `AdvanceAnimation(...)`
- `ResetAnimation()`
- `AnimationInvalidated`
- `AnimationMinimumRenderInterval`
- `HasPendingAnimationFrame`
- `FlushPendingAnimationFrame()`
- `LastAnimationDirtyTargetCount`

The runtime also exposes `SupportsNativeComposition`, `TryCreateNativeCompositionScene(...)`, and `TryCreateNativeCompositionFrame(...)` so host packages can attach retained playback paths without reimplementing SVG timing or property evaluation.

## Animation performance follow-up

The shared renderer now includes layered redraw support for animation frames and a local benchmark harness under `tests/Svg.Skia.Benchmarks`.

That benchmark project compares:

- layered top-level animation updates that reuse cached static content,
- defs-backed animation updates that still require full-document rebuilds,
- the same paths with a draw pass included.

## Android VectorDrawable

`Svg.Skia` also handles Android drawable XML directly:

```csharp
using Svg.Skia;

using var svg = new SKSvg();

if (svg.LoadVectorDrawable("Assets/icon.xml") is not null)
{
    svg.Save("artifacts/icon.png", SkiaSharp.SKColors.Transparent);
}
```

If you only need the parsed SVG document produced from a VectorDrawable, see [Svg.Model](svg-model).

## Fonts and settings

`SKSvgSettings` controls the rendering conversion layer. The default configuration includes:

- platform image color type,
- sRGB and linear-sRGB color spaces,
- a font-manager provider,
- a generic fallback typeface provider.

Add custom `ITypefaceProvider` implementations when your application resolves fonts from embedded assets, custom directories, or a separate font registry.

## When not to choose `Svg.Skia`

- Choose [Svg.Controls.Skia.Avalonia](svg-controls-skia-avalonia) when the main target is Avalonia XAML and you want controls, images, and brushes.
- Choose [Svg.Model](svg-model) when the task is model inspection or mutation and you do not need the runtime `SkiaSharp.SKPicture` wrapper yet.
- Choose [Svg.SourceGenerator.Skia](svg-sourcegenerator-skia) when parsing should happen at build time instead of application startup.

## Related docs

- [Loading SVG and VectorDrawable](../guides/loading-svg-and-vectordrawable)
- [Exporting Images, PDF, and XPS](../guides/exporting-images-pdf-and-xps)
- [Hit Testing and Model Editing](../guides/hit-testing-and-model-editing)
- [Interaction and Animation](../guides/interaction-and-animation)
- [Android VectorDrawable Support](../advanced/android-vectordrawable-support)
