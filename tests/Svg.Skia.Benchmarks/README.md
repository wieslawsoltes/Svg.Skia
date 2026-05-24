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
  - JavaScript-enabled no-CSS presentation attribute load path
  - JavaScript-enabled seekable stream no-CSS presentation attribute load path
  - JavaScript-enabled compatibility style state clone path
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
- `Svg2StaticFeatureBenchmarks`
  - SVG 2 CSS geometry property parsing, retained-scene compilation, and end-to-end load
  - repeated `<use>` expansion with inherited marker styles through temporary parents
  - SVG 2 symbol `width` / `height` viewport dimensions
  - `textPath side="right"` placement
- `SvgTextRegressionValidationBenchmarks`
  - generated text layout regression scenes for positioned glyphs, anchors, spacing, textLength, `textPath`, mixed/nested textPath wrapping including guarded mixed-sibling root textLength, vertical text, anchored `inline-size` overflow, rectangular and shape-based `inline-size` wrapping, CSS Text break opportunities, tiny-coordinate `textPath`, vertical RTL layout, vertical/RTL shape layout, root `inline-size` plus flattened and guarded wrapped textLength, positioned descendants, baseline shifts, altGlyph SVG-font substitution/fallback output, complex-script stretch, a combined shared-layout engine integration scene, and pending vertical/RTL/stretch parity coverage
  - retained compile, required command counts including stretch-generated path output, required text/path/layer source element IDs for supported cases, required DOM-metric source IDs for supported gap scenarios, command-kind coverage checksums, and command/DOM/render/end-to-end checksum generation with non-zero sanity checks for BenchmarkDotNet regression tracking; command-model checksums include source IDs, textPath path geometry, clip-path geometry, save-layer paint signatures, and stretched glyph path geometry, and DOM checksums include vertical writing-mode, guarded CSS Text break opportunities, guarded shape layout, parent metrics for direct and mixed/nested inline-size textPath, baseline and altGlyph fallback metrics, shared bidi/line-break/shape/textPath integration, and flattened/wrapped inline-size textLength
  - remaining browser-parity scenes for vertical/RTL wrapped `textLength`, positioned-descendant edge cases, vertical/RTL shape input, vertical/multiline textPath wrapping beyond the guarded horizontal mixed-sibling subset, W3C altGlyph raster parity, and complex-script stretch stay in the benchmark surface but only assert the currently supported retained output while exact browser parity remains open
- `SvgAllAreaRegressionValidationBenchmarks`
  - one combined generated scene covering text, paths, gradients, patterns, clips, masks, filters, markers, symbols, grouped rendering, shared wrapped text, mixed-direction text, shape-wrapped text, normal `textPath`, and stretched `textPath`
  - retained compile, text/path/clip/layer command-count validation, command-kind coverage checksums, required text/path source element IDs, required all-area text DOM metric IDs, and command/DOM/render/end-to-end checksum generation with non-zero sanity checks for BenchmarkDotNet regression tracking

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
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*Svg2StaticFeatureBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgTextRegressionValidationBenchmarks*"
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -- --filter "*SvgAllAreaRegressionValidationBenchmarks*"
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
