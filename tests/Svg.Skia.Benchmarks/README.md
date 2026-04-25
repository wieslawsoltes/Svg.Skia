# Svg.Skia Benchmarks

This project contains BenchmarkDotNet harnesses for the shared `Svg.Skia` hot paths.

BenchmarkDotNet results are emitted under `artifacts/benchmarks/` as HTML and GitHub-flavored Markdown summaries so local comparisons can be saved and attached to PR work.

## Included benchmark sets

- `SvgAnimationFrameBenchmarks`
  - layered top-level animation updates that reuse cached static content
  - defs-backed animation updates that still fall back to full-document rebuilds
  - the same two scenarios with a draw pass included
- `SvgXmlDomParseBenchmarks`
  - string, stream, and `XmlReader` entry points
  - mixed-content aggregation and inline-style-heavy parsing scenarios
- `SvgRetainedSceneCompileBenchmarks`
  - full retained-scene compile
  - node-tree compile only
  - index/resource/dependency/runtime-payload rebuild sub-stages
- `SvgPathConversionBenchmarks`
  - direct `PathingService.ToPath(...)` conversion across real document paths
  - split between `<path>` geometry and primitive shape geometry
  - external SVG scenarios such as `solar battery.svg`
- `SvgTextAssetLoaderBenchmarks`
  - repeated and sequence `MeasureText(...)` runs
  - repeated and sequence `FindTypefaces(...)` runs
  - text-heavy external SVG scenarios such as `solar battery.svg`
- `SvgTextCompileInternalsBenchmarks`
  - `SplitCodepoints(...)` across real text fragments
  - `MeasureNaturalTextAdvance(...)` across real text fragments
  - `MeasureNaturalCodepointAdvances(...)` across real text fragments
- `SvgAlignedTextPlacementBenchmarks`
  - direct `TryCreateAlignedCodepointPlacements(...)` runs across aligned-spacing and text-length scenarios
- `SvgTextPathPlacementBenchmarks`
  - `textPath` geometry resolution
  - prepared `textPath` placement input creation
  - midpoint lookup over precomputed glyph offsets
  - final placement emission from resolved midpoints
  - direct `TryCreateTextPathCodepointPlacements(...)` runs from prebuilt path geometry
- `SvgDomFeatureBenchmarks`
  - JavaScript-enabled DOM style capture load path
  - class mutation with compatibility style reapply and picture refresh
  - SVG font text DOM first-query and cached-query metric access
- `SvgShimPictureModelBenchmarks`
  - full retained-scene model materialization
  - top-level node and leaf node model materialization
- `SvgNativeSkPictureBenchmarks`
  - native picture creation from full, top-level, leaf, and empty shim models
  - replay-loop comparison for recorder dispatch hot paths
- `SvgRenderBitmapBenchmarks`
  - bitmap/canvas allocation
  - clear-only and empty-picture render overhead
  - 1x and 2x picture draw paths
  - full `ToBitmap(...)` render paths
- `SvgLoadPipelineBenchmarks`
  - parse `SvgDocument` from a string
  - compile retained scene from a parsed document
  - create shim picture model
  - create native `SKPicture`
  - render native picture to bitmap
  - encode native picture to PNG
  - load via `SKSvg`

The benchmark project uses a tuned short-run BenchmarkDotNet job by default so it stays practical for local iteration while still producing stable exported summaries.

## Run animation benchmarks

```bash
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgAnimationFrameBenchmarks*"
```

## Run load-pipeline benchmarks

```bash
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgLoadPipelineBenchmarks*"
```

## Run individual phase benchmarks

```bash
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgXmlDomParseBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgPathConversionBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextAssetLoaderBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextCompileInternalsBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgAlignedTextPlacementBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextPathPlacementBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgDomFeatureBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgShimPictureModelBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgNativeSkPictureBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRenderBitmapBenchmarks*"
```

You can also filter by category when focusing on a single stage:

```bash
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --anyCategories Parse
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --anyCategories Compile
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --anyCategories Render
```

The load-pipeline benchmarks include generated built-in scenarios for:

- inline-style heavy parsing
- text-heavy retained-scene compilation
- large shape-heavy documents

To benchmark specific SVG files, provide a semicolon-separated list via `SVG_SKIA_BENCHMARK_SVG_PATHS`:

```bash
SVG_SKIA_BENCHMARK_SVG_PATHS="/Users/me/Downloads/solar battery.svg" \
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgLoadPipelineBenchmarks*"
```

## Manual profiler

For quick local stage-by-stage sampling outside BenchmarkDotNet, the project also keeps the ad hoc profiler:

```bash
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --profile-svg "/absolute/path/to/file.svg" 30
```
