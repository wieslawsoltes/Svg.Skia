# PR Summary: Update Svg.Skia to SkiaSharp 4 Preview

## Summary

This PR updates the repository's SkiaSharp package set to the latest SkiaSharp 4 preview available
from NuGet, while keeping a practical source-level compatibility path for building the core renderer
against the previous SkiaSharp 3 package set.

The default central package configuration now uses:

- `SkiaSharp` and related native/view packages: `4.147.0-preview.2.1`
- `SkiaSharp.HarfBuzz`: `4.147.0-preview.2.1`
- `HarfBuzzSharp` and native assets: `8.3.1.6-preview.2.1`
- `Microsoft.Maui.Controls` and Compatibility: `10.0.20`

The MAUI bump is required because `SkiaSharp.Views.Maui.Controls 4.147.0-preview.2.1` depends on
`Microsoft.Maui.Controls >= 10.0.20`; keeping `10.0.0` causes `NU1605` downgrade failures in MAUI
projects.

## What Changed

- Added `SkiaSharpVersion` and `HarfBuzzSharpVersion` properties in `Directory.Packages.props`.
- Updated all central SkiaSharp package versions to use `$(SkiaSharpVersion)`.
- Updated all central HarfBuzzSharp package versions to use `$(HarfBuzzSharpVersion)`.
- Raised the central MAUI package versions from `10.0.0` to `10.0.20`.
- Updated `build/SkiaSharp.Avalonia.props` to override Avalonia SkiaSharp references through
  `$(SkiaSharpVersion)`.
- Added a migration report covering package changes, API warnings, build/test results, risks, and
  v3 compatibility strategy.
- Added a SkiaSharp 3 vs 4 performance report using large sample SVG assets.

## Compatibility Notes

SkiaSharp 4 preview did not introduce compile errors in the solution. The migration impact is mostly
obsolete API usage:

- `SKPath` mutation APIs now point toward `SKPathBuilder`.
- text-related `SKPaint` APIs now point toward `SKFont` and explicit text drawing overloads.
- `SKPathMeasure.GetSegment` and `SKPaint.GetFillPath` now prefer `SKPathBuilder` overloads.

The current source still builds against SkiaSharp 3 by overriding the new version properties:

```sh
dotnet build src/Svg.Skia/Svg.Skia.csproj -c Release \
  -p:SkiaSharpVersion=3.119.2 \
  -p:HarfBuzzSharpVersion=8.3.1.3
```

This keeps source compatibility feasible while the default package set moves forward to SkiaSharp 4
preview.

## Validation

Ran:

```sh
dotnet build Svg.Skia.slnx -c Release
```

Result:

- Succeeded
- `0 Error(s)`
- `10577 Warning(s)`

Ran source compatibility check:

```sh
dotnet build src/Svg.Skia/Svg.Skia.csproj -c Release \
  -p:SkiaSharpVersion=3.119.2 \
  -p:HarfBuzzSharpVersion=8.3.1.3
```

Result:

- Succeeded
- `0 Error(s)`
- `356 Warning(s)`

Ran MAUI restore checks with `net10.0-maccatalyst` for:

- `src/Svg.Controls.Skia.Maui/Svg.Controls.Skia.Maui.csproj`
- `src/SvgML.Maui/SvgML.Maui.csproj`
- `samples/MauiSvgSkiaSample/MauiSvgSkiaSample.csproj`
- `samples/SvgML.Maui.Demo/SvgML.Maui.Demo.csproj`

Result: all restored successfully after the MAUI `10.0.20` update.

Ran:

```sh
dotnet test Svg.Skia.slnx -c Release --no-build
```

Result:

- Failed in expected Avalonia runtime paths because `Avalonia.Skia` is being forced onto SkiaSharp 4
  before Avalonia has aligned its runtime assumptions.
- Also found two non-Avalonia W3C text raster diffs:
  - `text-fonts-01-t`: image error `0.024473930618178694` vs threshold `0.022`
  - `text-fonts-02-t`: image error `0.031028349414410593` vs threshold `0.022`

## Performance Notes

Ran the existing `--profile-svg` benchmark profiler for the two largest sample/demo SVGs:

- `samples/MauiSvgSkiaSample/Assets/__AJ_Digital_Camera.svg`
- `samples/MauiSvgSkiaSample/Assets/__tiger.svg`

High-level result:

- Camera demo is mixed. SkiaSharp 4 is faster for `SKSvg.FromSvg` and retained mutation rebuild,
  but slower for native picture creation, bitmap render, PNG encode, and full document rebuild.
- Tiger demo shows faster parse and retained scene compile on SkiaSharp 4, but slower native picture
  creation, bitmap render, full load paths, and mutation rebuilds.
- Allocations stayed effectively flat, so the observed regressions appear CPU/render-path related
  rather than allocation-driven.

Detailed benchmark numbers are included in
`plan/skiasharp-3-vs-4-large-svg-benchmarks.md`.

## Risks

- Avalonia runtime behavior is expected to remain broken until Avalonia aligns with SkiaSharp 4.
- Future SkiaSharp 4 previews may make currently obsolete APIs stricter.
- Moving generated source output to `SKPathBuilder`/`SKFont` would reduce warnings but requires a
  clear decision on whether source generation must continue supporting SkiaSharp 3.

## Follow-Up

- Add a CI lane for the SkiaSharp 3 compatibility build if dual source compatibility remains a goal.
- Review the two W3C text-font raster differences before updating thresholds or baselines.
- Decide whether package output should move fully to SkiaSharp 4 or whether separate v3/v4 package
  variants are needed.
- Consider a focused `SKPathBuilder`/`SKFont` migration plan after the v3 support boundary is
  decided.
