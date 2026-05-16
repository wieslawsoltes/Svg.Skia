# SkiaSharp 3 vs 4 Large SVG Benchmarks

Date: 2026-05-13

## Scope

Compared the current SkiaSharp 4 preview package set against the previous SkiaSharp 3 package set
using the existing `tests/Svg.Skia.Benchmarks` profiler.

Package sets:

| Label | SkiaSharp | HarfBuzzSharp |
| --- | --- | --- |
| v3 | 3.119.2 | 8.3.1.3 |
| v4 | 4.147.0-preview.2.1 | 8.3.1.6-preview.2.1 |

Assets:

| Asset | Path | Size |
| --- | --- | --- |
| Camera demo | `samples/MauiSvgSkiaSample/Assets/__AJ_Digital_Camera.svg` | 132,619 bytes |
| Tiger demo | `samples/MauiSvgSkiaSample/Assets/__tiger.svg` | 103,573 bytes |

Methodology:

- `--profile-svg` profiler from `tests/Svg.Skia.Benchmarks`
- 20 measured iterations per asset/version
- 3 warmup iterations
- Same working tree and benchmark code for both package sets
- Positive delta means v4 is faster than v3; negative delta means v4 is slower

Commands:

```sh
dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj \
  -p:SkiaSharpVersion=3.119.2 \
  -p:HarfBuzzSharpVersion=8.3.1.3 \
  -- --profile-svg samples/MauiSvgSkiaSample/Assets/__AJ_Digital_Camera.svg 20

dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj \
  -p:SkiaSharpVersion=3.119.2 \
  -p:HarfBuzzSharpVersion=8.3.1.3 \
  -- --profile-svg samples/MauiSvgSkiaSample/Assets/__tiger.svg 20

dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj \
  -- --profile-svg samples/MauiSvgSkiaSample/Assets/__AJ_Digital_Camera.svg 20

dotnet run -c Release --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj \
  -- --profile-svg samples/MauiSvgSkiaSample/Assets/__tiger.svg 20
```

## Camera Demo

| Stage | v3 mean | v4 mean | v4 delta |
| --- | ---: | ---: | ---: |
| Parse SvgDocument from string | 8.24 ms | 7.93 ms | +3.8% |
| Compile retained scene (parsed doc) | 5.97 ms | 6.22 ms | -4.2% |
| Create shim picture model | 0.04 ms | 0.04 ms | +0.0% |
| Create native SKPicture | 0.54 ms | 0.84 ms | -55.6% |
| Render native picture to bitmap | 3.57 ms | 3.92 ms | -9.8% |
| Encode native picture to PNG | 11.28 ms | 11.68 ms | -3.5% |
| Load via SKSvg.FromSvg | 17.70 ms | 15.65 ms | +11.6% |
| Control-like source load | 14.07 ms | 14.62 ms | -3.9% |
| Mutate + full FromSvgDocument rebuild | 20.07 ms | 23.93 ms | -19.2% |
| Mutate + retained scene rebuild | 19.49 ms | 18.87 ms | +3.2% |

Allocation deltas were effectively flat. The only measured increases were small: native picture
creation moved from 0.24 MB to 0.25 MB and load paths from 5.44 MB to 5.45 MB.

## Tiger Demo

| Stage | v3 mean | v4 mean | v4 delta |
| --- | ---: | ---: | ---: |
| Parse SvgDocument from string | 10.78 ms | 8.77 ms | +18.6% |
| Compile retained scene (parsed doc) | 13.90 ms | 9.98 ms | +28.2% |
| Create shim picture model | 0.14 ms | 0.20 ms | -42.9% |
| Create native SKPicture | 1.27 ms | 1.65 ms | -29.9% |
| Render native picture to bitmap | 2.51 ms | 3.08 ms | -22.7% |
| Encode native picture to PNG | 15.03 ms | 16.79 ms | -11.7% |
| Load via SKSvg.FromSvg | 21.01 ms | 24.90 ms | -18.5% |
| Control-like source load | 17.58 ms | 21.18 ms | -20.5% |
| Mutate + full FromSvgDocument rebuild | 27.24 ms | 32.72 ms | -20.1% |
| Mutate + retained scene rebuild | 21.48 ms | 24.69 ms | -14.9% |

Allocation deltas were also close to flat. The largest observed change was retained mutation rebuild
allocating 10.01 MB on v4 versus 10.08 MB on v3.

## Takeaways

- SkiaSharp 4 is faster in the parser/retained-compile parts that are mostly managed-code or
  pre-render pipeline work for these two assets.
- SkiaSharp 4 is slower in native picture creation and bitmap/PNG rendering for both assets.
- The tiger demo regresses more consistently in the full control-like and mutation paths.
- The camera demo is mixed: `SKSvg.FromSvg` and retained mutation are faster, while native picture
  creation, bitmap render, and full document rebuild are slower.
- Allocations are not the issue in these runs; timing moved while memory stayed mostly stable.

## Follow-Up

For a commit-quality perf decision, rerun this with BenchmarkDotNet isolated jobs after adding an
external-only scenario switch, or run each asset in a clean process several times and compare medians.
The current result is enough to flag the likely SkiaSharp 4 native rendering cost increase on these
large demo SVGs.
