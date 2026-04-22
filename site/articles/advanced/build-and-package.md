---
title: "Build and Package"
---

# Build and Package

## Local repository workflow

The expected local sequence is:

```bash
git submodule update --init --recursive
dotnet format --no-restore
dotnet build Svg.Skia.slnx -c Release
dotnet test Svg.Skia.slnx -c Release
```

The Uno sample app is intentionally not part of `Svg.Skia.slnx`, so those default commands do not require Uno workloads.
The MAUI projects are validated and packed by the dedicated MAUI workflow jobs because they require the .NET MAUI workload.

## CI workflows

The repository now has dedicated workflows for:

- build and test,
- release packaging,
- docs publishing.

The docs workflow builds the Lunet site and publishes `site/.lunet/build/www` to GitHub Pages on pushes to `main` or `master`.

## Packaging notes

The repository ships more than one NuGet package. The main runtime packages are:

- `Svg.Skia`
- `Svg.Model`
- `Svg.Controls.Skia.Uno`
- `Svg.Controls.Skia.Maui`
- `Svg.Controls.Avalonia`
- `Svg.Controls.Skia.Avalonia`
- `Skia.Controls.Avalonia`
- `SvgML.Maui`
- `Svg.SourceGenerator.Skia`
- `Svg.CodeGen.Skia`
- `Svg.Custom`
- `ShimSkiaSharp`

There are also sample or tool packages such as `Svg.Skia.Converter` and `svgc`.

## Release path

The `release.yml` workflow:

- builds and tests a release,
- packs the NuGet artifacts,
- pushes packages to NuGet,
- creates a GitHub release with the packaged artifacts attached.

## Uno sample publishing

Use the standalone sample project when validating the Uno control package:

```bash
uno-check --target desktop --target web --target android --target ios
dotnet build samples/UnoSvgSkiaSample/UnoSvgSkiaSample.csproj -c Release -f net10.0-desktop
dotnet publish samples/UnoSvgSkiaSample/UnoSvgSkiaSample.csproj -c Release -f net10.0-browserwasm
dotnet build samples/UnoSvgSkiaSample/UnoSvgSkiaSample.csproj -c Release -f net10.0-android
dotnet build samples/UnoSvgSkiaSample/UnoSvgSkiaSample.csproj -c Release -f net10.0-ios
```
