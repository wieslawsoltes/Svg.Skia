---
title: "Performance and Retained-Scene Refresh"
---

# Performance and Retained-Scene Refresh

The load and refresh cost of an SVG is not one number. In `Svg.Skia` the work is split across document parsing, retained-scene compilation, shim-model creation, native `SKPicture` materialization, and any later raster export.

This matters when a device looks "slow to load" because the bottleneck may be DOM parsing, scene compilation, PNG encoding, or a full rebuild after a small mutation.

## Measure the staged pipeline

The benchmark project includes a dedicated profiler for one SVG file:

```bash
dotnet run --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -c Release -- --profile-svg '/Users/wieslawsoltes/Downloads/solar battery.svg' 20
```

That command reports separate timings for:

- parsing text into `SvgDocument`,
- compiling the retained `SvgSceneDocument`,
- creating the shim `SKPicture` model,
- creating the native `SkiaSharp.SKPicture`,
- rendering to a bitmap,
- encoding to PNG,
- full `SKSvg.FromSvg(...)`,
- control-like source loading,
- full `FromSvgDocument(...)` rebuild after a mutation,
- retained-scene mutation refresh.

## Example profile

Example output from `osx-arm64`, `Release`, `20` iterations, for `/Users/wieslawsoltes/Downloads/solar battery.svg` (`29,974` bytes):

| Stage | Mean |
| --- | ---: |
| Parse `SvgDocument` from string | `2.46 ms` |
| Compile retained scene | `5.08 ms` |
| Create shim picture model | `0.04 ms` |
| Create native `SKPicture` | `0.53 ms` |
| Render native picture to bitmap | `3.77 ms` |
| Encode native picture to PNG | `13.85 ms` |
| Load via `SKSvg.FromSvg(...)` | `12.39 ms` |
| Control-like source load | `11.14 ms` |
| Mutate + full `FromSvgDocument(...)` rebuild | `16.11 ms` |
| Mutate + retained-scene refresh | `12.73 ms` |

Treat those numbers as a shape, not a promise. CM4, CM5, desktop ARM, and different output resolutions will change the absolute timings.

## Hot paths to watch

The staged profile above points at the current hot paths:

- retained-scene compilation is the main load-time cost inside `SKSvg`,
- native picture creation is comparatively small,
- PNG encoding can dominate end-to-end export time even when rendering itself is fast,
- retained-scene mutation refresh is cheaper than a full `FromSvgDocument(...)` rebuild for small localized edits.

Text-heavy documents can push more cost into text compilation and measurement than purely geometric assets, so profile representative files instead of relying on one benchmark.

## Retained-scene refresh APIs

`SKSvg` exposes three refresh helpers for localized DOM edits:

- `TryApplyRetainedSceneMutationAndRender(SvgElement element, IReadOnlyCollection<string>? changedAttributes, out SvgSceneMutationResult? result)`
- `TryApplyRetainedSceneMutationAndRender(string addressKey, IReadOnlyCollection<string>? changedAttributes, out SvgSceneMutationResult? result)`
- `TryApplyRetainedSceneMutationByIdAndRender(string id, IReadOnlyCollection<string>? changedAttributes, out SvgSceneMutationResult? result)`

Example:

```csharp
using System.Drawing;
using Svg;
using Svg.Skia;

using var svg = new SKSvg();
svg.FromSvg(
    "<svg width=\"80\" height=\"40\">" +
    "  <rect id=\"rect-a\" x=\"10\" y=\"8\" width=\"24\" height=\"12\" fill=\"red\" />" +
    "</svg>");

var scene = svg.RetainedSceneGraph!;
var rect = (SvgRectangle)scene.SourceDocument!.GetElementById("rect-a")!;
rect.Fill = new SvgColourServer(Color.BlueViolet);

if (!svg.TryApplyRetainedSceneMutationByIdAndRender("rect-a", new[] { "fill" }, out var result))
{
    svg.FromSvgDocument(scene.SourceDocument);
}
```

The returned `SvgSceneMutationResult` reports whether the refresh succeeded and how many compilation roots had to be recompiled.

## Picking the right refresh path

Use retained-scene refresh when:

- the change is localized to one element or a small subtree,
- you know which attributes changed,
- the renderer is already holding a retained scene graph,
- you want the current `Picture` refreshed without a full DOM-to-scene recompilation.

Use `FromSvgDocument(...)` when:

- the root element changed,
- structure was added or removed,
- the mutation touches a case that cannot be refreshed incrementally,
- you want the simplest and most predictable rebuild path.

Use `ReLoad(...)` when:

- the underlying source is unchanged,
- only `SvgParameters` changed,
- the original source path or stream should be reparsed with different CSS or entities.
