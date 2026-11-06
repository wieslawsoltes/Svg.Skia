---
title: "Uno Sample Publishing"
---

# Uno Sample Publishing

## UnoDraw on GitHub Pages

[Launch UnoDraw](https://wieslawsoltes.github.io/Svg.Skia/unodraw/) to use the SVG
editor in a browser. It shares the documentation's GitHub Pages site, with the
application under `/Svg.Skia/unodraw/` and the docs at `/Svg.Skia/`.

The `Docs` workflow builds the documentation and the Release AOT WebAssembly
package from the same commit. It tests the package at the nested deployment path
using a static server without cross-origin isolation headers, then publishes both
outputs together as a GitHub Pages artifact. The repository's Pages publishing
source must be **GitHub Actions**, rather than a branch, so large AOT files do not
need to be committed to Git. A failed build or browser smoke test leaves the
previous deployment in place. Pushes to the default branch deploy automatically;
`workflow_dispatch` also supports rebuilding that branch. Pull requests only
build and validate. Desktop and mobile UnoDraw checks remain in the `UnoDraw`
workflow.

The browser package uses relative asset URLs (`WasmShellWebAppBasePath=./`) and
disables WebAssembly threads for GitHub Pages hosting. Its manifest's start URL
and scope are also relative, keeping installation within the app subfolder.
Browser file saving uses a file picker or download according to browser support.

## Uno control sample

The Uno sample app lives at `samples/UnoSvgSkiaSample` and stays out of `Svg.Skia.slnx`, so the default repository build and test workflow remains workload-free.

## Prerequisite workloads

```bash
uno-check --target desktop --target web --target android --target ios
```

## Desktop

```bash
dotnet build samples/UnoSvgSkiaSample/UnoSvgSkiaSample.csproj -c Release -f net10.0-desktop
```

## WebAssembly

```bash
dotnet publish samples/UnoSvgSkiaSample/UnoSvgSkiaSample.csproj -c Release -f net10.0-browserwasm
```

## Android

```bash
dotnet build samples/UnoSvgSkiaSample/UnoSvgSkiaSample.csproj -c Release -f net10.0-android
```

## iOS on macOS

```bash
dotnet build samples/UnoSvgSkiaSample/UnoSvgSkiaSample.csproj -c Release -f net10.0-ios
```

## What to verify manually

- asset loading through `Path`
- inline SVG through `Source`
- shared `SvgSource` resource usage
- runtime CSS switching
- zoom, pan, and hit testing
- wireframe and filter toggle behavior
