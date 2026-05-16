# SkiaSharp 4 Preview Update Report

Date: 2026-05-13

## Package Set

Verified from NuGet on 2026-05-13:

| Package family | Previous | Updated |
| --- | --- | --- |
| SkiaSharp, native assets, MAUI views, HarfBuzz bridge | 3.119.2 | 4.147.0-preview.2.1 |
| HarfBuzzSharp and native assets | 8.3.1.3 | 8.3.1.6-preview.2.1 |
| Microsoft.Maui.Controls, Compatibility | 10.0.0 | 10.0.20 |

The MAUI package bump is required because `SkiaSharp.Views.Maui.Controls 4.147.0-preview.2.1`
depends on `Microsoft.Maui.Controls >= 10.0.20`; keeping `10.0.0` produces `NU1605`
package downgrade failures in the MAUI projects.

Primary references:

- https://www.nuget.org/packages/SkiaSharp/4.147.0-preview.2.1
- https://www.nuget.org/packages/SkiaSharp.HarfBuzz/4.147.0-preview.2.1
- https://devblogs.microsoft.com/dotnet/welcome-to-skia-sharp-40-preview1/

## Repository Changes

- `Directory.Packages.props` now defaults the SkiaSharp family to `4.147.0-preview.2.1`.
- `Directory.Packages.props` now defaults HarfBuzzSharp to `8.3.1.6-preview.2.1`.
- `Directory.Packages.props` now pins MAUI packages to `10.0.20` to satisfy the new SkiaSharp MAUI view dependency.
- `build/SkiaSharp.Avalonia.props` now intentionally overrides Avalonia's SkiaSharp dependency to the same SkiaSharp version property.
- The SkiaSharp/HarfBuzz versions are property-driven:
  - `SkiaSharpVersion`
  - `HarfBuzzSharpVersion`

This keeps the default build on SkiaSharp 4 while allowing a source build against the previous v3 set:

```sh
dotnet build src/Svg.Skia/Svg.Skia.csproj -c Release \
  -p:SkiaSharpVersion=3.119.2 \
  -p:HarfBuzzSharpVersion=8.3.1.3
```

## Build Results

Default SkiaSharp 4 build:

```sh
dotnet build Svg.Skia.slnx -c Release
```

Result:

- Succeeded
- `0 Error(s)`
- `10577 Warning(s)`

Full solution test run:

```sh
dotnet test Svg.Skia.slnx -c Release --no-build
```

Result:

- Failed
- `tests/Svg.Controls.Skia.Avalonia.UnitTests`: 38 failures during Avalonia/Skia teardown or setup.
  All sampled failures root at `SkiaSharp.SKFontManager` static initialization through
  `Avalonia.Skia.SkiaPlatform.Initialize`, matching the expected Avalonia runtime incompatibility.
- `tests/Svg.Skia.UnitTests`: 2 W3C raster failures:
  - `text-fonts-01-t`: image error `0.024473930618178694` vs threshold `0.022`
  - `text-fonts-02-t`: image error `0.031028349414410593` vs threshold `0.022`

The W3C failures are font-rendering differences from the SkiaSharp 4 engine/package set and should
be reviewed separately before any threshold or baseline change.

MAUI restore checks, using `net10.0-maccatalyst`, also succeeded for:

- `src/Svg.Controls.Skia.Maui/Svg.Controls.Skia.Maui.csproj`
- `src/SvgML.Maui/SvgML.Maui.csproj`
- `samples/MauiSvgSkiaSample/MauiSvgSkiaSample.csproj`
- `samples/SvgML.Maui.Demo/SvgML.Maui.Demo.csproj`

SkiaSharp 3 compatibility check:

```sh
dotnet build src/Svg.Skia/Svg.Skia.csproj -c Release \
  -p:SkiaSharpVersion=3.119.2 \
  -p:HarfBuzzSharpVersion=8.3.1.3
```

Result:

- Succeeded
- `0 Error(s)`
- `356 Warning(s)`

## API Change Analysis

SkiaSharp 4 preview did not create compile errors in the solution. The main migration impact is
obsolete API usage, especially around path construction and text APIs. SkiaSharp 4 keeps the old
APIs callable for compatibility, but marks them obsolete.

High-volume obsolete warnings:

| API family | Meaning |
| --- | --- |
| `SKPath.MoveTo`, `LineTo`, `CubicTo`, `QuadTo`, `ArcTo`, `Close`, `AddRect`, `AddOval`, `AddCircle`, `AddPoly`, `AddRoundRect` | Replace path construction with `SKPathBuilder` for SkiaSharp 4 forward compatibility. |
| `SKPathMeasure.GetSegment(..., SKPath, ...)` | Move to the `SKPathBuilder` overload. |
| `SKPaint.GetFillPath(SKPath, SKPath)` | Move to the `SKPathBuilder` overload. |
| `SKPaint.TextSize`, `Typeface`, `TextAlign`, `TextScaleX`, `TextSkewX`, `SubpixelText`, `FakeBoldText`, `LcdRenderText`, `TextEncoding` | Text shaping/drawing state is moving from `SKPaint` to `SKFont` plus explicit text overloads. |
| `SKPaint.MeasureText`, `GetFontMetrics`, `GetTextPath`, `ToFont` | Prefer direct `SKFont` APIs. |
| `SKCanvas.DrawText(..., SKPaint)` and `DrawTextOnPath(..., SKPaint)` | Prefer overloads accepting `SKTextAlign`, `SKFont`, and `SKPaint`. |

Most warning volume comes from generated SVG source in sample `obj` folders. The primary handwritten
areas to plan for are:

- `src/Svg.Skia/SkiaModel.cs`
- `src/Svg.Skia/SkiaSvgAssetLoader.cs`
- `src/Svg.Skia/SkiaModel.TextShaping.cs`
- `src/Svg.Skia/SkiaModel.Caching.cs`
- `src/Svg.Editor.Skia/PathService.cs`
- `src/Svg.Editor.Skia/RenderingService.cs`
- `src/Svg.SourceGenerator.Skia` output templates/generation logic

## Compatibility Assessment

Source compatibility with SkiaSharp 3 is still feasible because the current code continues to use
APIs present in both SkiaSharp 3 and SkiaSharp 4 preview. The new package defaults, however, mean
NuGet packages built from the default configuration will declare SkiaSharp 4 dependencies.

Recommended compatibility strategy:

1. Keep default CI/package validation on SkiaSharp 4 preview while this migration is active.
2. Add a separate source-compatibility lane that builds at least `src/Svg.Skia/Svg.Skia.csproj`
   with `SkiaSharpVersion=3.119.2` and `HarfBuzzSharpVersion=8.3.1.3`.
3. Do not switch handwritten code or generated output wholesale to `SKPathBuilder`/`SKFont` until
   the desired v3 package support boundary is decided.
4. If dual NuGet support is required, publish separate v3 and v4 package variants or keep the
   package dependency on v3 and validate that v4 consumers can override upward. A single default
   package cannot truthfully depend on both SkiaSharp 3 and SkiaSharp 4 for the same TFM.

## Risks

- Avalonia runtime issues are expected because `Avalonia.Skia` is being forced onto SkiaSharp 4
  before Avalonia has aligned its own dependency/runtime assumptions.
- Future SkiaSharp 4 previews may turn more obsolete APIs into stricter source or binary breaks.
- Moving generated output to `SKPathBuilder` would reduce warning volume but likely requires
  compatibility shims or conditional generation if SkiaSharp 3 support must remain.
