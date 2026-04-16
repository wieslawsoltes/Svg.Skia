# Svg.Skia Benchmarks

This project contains BenchmarkDotNet harnesses for the shared `Svg.Skia` hot paths.

BenchmarkDotNet results are emitted under `artifacts/benchmarks/` as HTML and GitHub-flavored Markdown summaries so local comparisons can be saved and attached to PR work.

## Included benchmark sets

- `SvgAnimationFrameBenchmarks`
  - layered top-level animation updates that reuse cached static content
  - defs-backed animation updates that still fall back to full-document rebuilds
  - cached layered draw paths that are sensitive to static-model `SaveLayer(...)` bounds
  - the same two scenarios with a draw pass included
- `SvgAnimationLoadBenchmarks`
  - non-animated document detection before `SvgAnimationController` construction
  - full animation-controller setup cost on parsed documents
  - fresh-instance static `SKSvg.FromSvgDocument(...)` load cost over a parsed document
- `SvgXmlDomParseBenchmarks`
  - string, stream, and `XmlReader` entry points
  - mixed-content aggregation and inline-style-heavy parsing scenarios
- `SvgCustomParsePhaseBenchmarks`
  - isolated `Svg.Custom` structure-build runs
  - CSS compatibility application over a prebuilt document
  - style-flush cost over a prebuilt document
  - final parse post-processing after structure build
- `SvgCustomAttributeDispatchBenchmarks`
  - isolated `Svg.Custom` element-creation and attribute-dispatch runs
  - scenario-driven measurement for `SvgElementFactory.SetAttributes(...)`
  - hot unprefixed property-set replay for `id`, `transform`, `d`, and common geometry fields
  - cold-vs-warm shared path-data prototype cache replay for `<path d="...">` attributes
  - remaining geometry-setter replay for `viewBox`, `cx`, `cy`, `r`, `rx`, and `ry`
- `SvgInlineStyleAttributeBenchmarks`
  - isolated inline `style="..."` replay over real document style strings
  - cold-vs-warm shared inline-style declaration cache measurements
- `SvgRetainedSceneCompileBenchmarks`
  - full retained-scene compile
  - full retained-scene compile with a fresh `SkiaModel` and `SkiaSvgAssetLoader`
  - full retained-scene compile with a fresh parsed document per invocation
  - node-tree compile only
  - index/resource/dependency/runtime-payload rebuild sub-stages
- `SvgRetainedSceneMutationBenchmarks`
  - hot retained-scene mutation-only cost on already-built scenes
  - mutation-plus-render cost for `TryApplyRetainedSceneMutationAndRender(...)`
  - mutation-plus-full-rebuild cost for `SKSvg.FromSvgDocument(...)` on a live static document
  - external SVG scenarios such as `solar battery.svg`
- `SvgPathConversionBenchmarks`
  - direct `PathingService.ToPath(...)` conversion across real document paths
  - split between `<path>` geometry and primitive shape geometry
  - external SVG scenarios such as `solar battery.svg`
- `SvgTextAssetLoaderBenchmarks`
  - repeated and sequence `MeasureText(...)` runs
  - repeated and cold sequence `GetFontMetrics(...)` runs
  - repeated and sequence `FindTypefaces(...)` runs
  - repeated and cold sequence `TryShapeGlyphRun(...)` runs
  - text-heavy external SVG scenarios such as `solar battery.svg`
- `SvgTextCompileInternalsBenchmarks`
  - `CreateTextMetricsPaint(...)` across real text fragments
  - hot `CreateTextMetricsPaint(...)` runs inside a compile-scope cache
  - `SplitCodepoints(...)` across real text fragments
  - `MeasureLineStats(...)` across real text fragments
  - hot `MeasureLineStats(...)` runs inside a compile-scope cache
  - `MeasureLineStats(...)` across real text fragments with a fresh asset loader per invocation
  - `MeasureNaturalTextAdvance(...)` across real text fragments
  - `MeasureNaturalTextAdvance(...)` across real text fragments with a fresh asset loader per invocation
  - `MeasureNaturalCodepointAdvances(...)` across real text fragments
  - `MeasureNaturalCodepointAdvances(...)` across real text fragments with a fresh asset loader per invocation
- `SvgAlignedTextPlacementBenchmarks`
  - direct `TryCreateAlignedCodepointPlacements(...)` runs across aligned-spacing and text-length scenarios
- `SvgTextPathPlacementBenchmarks`
  - `textPath` geometry resolution
  - prepared `textPath` placement input creation
  - midpoint lookup over precomputed glyph offsets
  - final placement emission from resolved midpoints
  - direct `TryCreateTextPathCodepointPlacements(...)` runs from prebuilt path geometry
- `SvgFilteredTextPathRenderBenchmarks`
  - filtered `textPath` draw/record paths over pre-positioned runs
  - render-sensitive `SaveLayer(...)` behavior for filter-heavy text-path scenes
- `SvgLayerBoundsBenchmarks`
  - wrapper-heavy retained-scene native recording
  - sensitivity to subtree paint-bound reuse for opacity-layer bounds
- `SvgSingularLayerBoundsBenchmarks`
  - render-sensitive retained-scene playback with opacity wrappers under singular ancestor transforms
  - coverage for local-space layer-bound reuse when inverse mapping is unavailable
- `SvgShimPictureModelBenchmarks`
  - full retained-scene model materialization
  - top-level node and leaf node model materialization
- `SvgNativeSkPictureBenchmarks`
  - native picture creation from full, top-level, leaf, and empty shim models
  - direct retained-scene native recorder path versus the older shim-model-to-native route
  - fresh-`SkiaModel` native picture creation from a stable full shim model to isolate first-record startup cost
    and default-path positioned-text reuse
  - warm cached retained-scene native picture access for repeated tooling or draw setup
  - warm cached retained-node native picture access for repeated no-clip and clip-aware node exports
  - replay-loop comparison for recorder dispatch hot paths, including picture replay versus
    per-command `for` and `foreach` dispatch
- `SvgRenderBitmapBenchmarks`
  - bitmap/canvas allocation
  - clear-only and empty-picture render overhead
  - 1x and 2x picture draw paths
  - opacity, mask, and filter-heavy retained-scene playback that is sensitive to `SaveLayer(...)`
    bounds
  - filtered `textPath` playback scenarios that are sensitive to bounded filter layers
  - full `ToBitmap(...)` render paths
  - `ToBitmap(...)` into caller-supplied reusable bitmap/canvas
  - PNG encode via allocating and reusable-surface paths
- `SvgLoadPipelineBenchmarks`
  - parse `SvgDocument` from a string
  - compile retained scene from a parsed document
  - create shim picture model
  - create native `SKPicture`
  - render native picture to bitmap
  - encode native picture to PNG
  - load via `SKSvg`
  - load via `SKSvg.FromSvg(string, ..., baseUri)` for control-like inline source paths
  - load via `SKSvg` and then materialize the deferred shim model
- `SvgSaveBenchmarks`
  - repeated public `SKSvg.Save(...)` PNG export cost over an already-loaded document
  - cold public `SKSvg.Save(...)` PNG export cost after forcing a fresh native picture publish

The benchmark project uses a tuned short-run BenchmarkDotNet job by default so it stays practical for local iteration while still producing stable exported summaries.

## Run animation benchmarks

```bash
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgAnimationFrameBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgAnimationLoadBenchmarks*"
```

## Run load-pipeline benchmarks

```bash
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgLoadPipelineBenchmarks*"
```

## Run individual phase benchmarks

```bash
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgXmlDomParseBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgCustomParsePhaseBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgCustomAttributeDispatchBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneCompileBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRetainedSceneMutationBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgPathConversionBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextAssetLoaderBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextCompileInternalsBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgAlignedTextPlacementBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextPathPlacementBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgFilteredTextPathRenderBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgLayerBoundsBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgSingularLayerBoundsBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgShimPictureModelBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgNativeSkPictureBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgRenderBitmapBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgSaveBenchmarks*"
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
- filter-heavy curved `textPath` rendering
- large shape-heavy documents

To benchmark specific SVG files, provide a semicolon-separated list via `SVG_SKIA_BENCHMARK_SVG_PATHS`:

```bash
SVG_SKIA_BENCHMARK_SVG_PATHS="/Users/me/Downloads/solar battery.svg" \
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgLoadPipelineBenchmarks*"
```

To restrict a run to specific scenario names, set `SVG_SKIA_BENCHMARK_SCENARIO_FILTER`:

```bash
SVG_SKIA_BENCHMARK_SVG_PATHS="/Users/me/Downloads/solar battery.svg" \
SVG_SKIA_BENCHMARK_SCENARIO_FILTER="file:solar battery.svg" \
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*CompileRetainedSceneFromParsedDocument*"
```

## Manual profiler

For quick local stage-by-stage sampling outside BenchmarkDotNet, the project also keeps the ad hoc profiler:

```bash
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --profile-svg "/absolute/path/to/file.svg" 30
```
