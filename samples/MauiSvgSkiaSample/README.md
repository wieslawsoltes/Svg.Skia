# MauiSvgSkiaSample

Standalone .NET MAUI sample for `Svg.Controls.Skia.Maui`.

## Build

Android:

```bash
dotnet build -c Release -f net10.0-android
```

iOS on macOS:

```bash
dotnet build -c Release -f net10.0-ios
```

Mac Catalyst on macOS:

```bash
dotnet build -c Release -f net10.0-maccatalyst
```

## What the sample covers

- asset loading through `Path`
- inline SVG text through `Source`
- reusable `SvgSource` resources
- runtime CSS restyling
- zoom, pan, and hit testing
- wireframe and filter toggles
