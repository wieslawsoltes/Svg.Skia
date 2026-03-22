# UnoSvgEditorSample

Standalone Uno Platform SVG editor sample built on `Svg.Controls.Skia.Uno`, `Svg.Editor.Core`, `Svg.Editor.Svg`, `Svg.Editor.Skia`, and the reusable `Svg.Editor.Skia.Uno` shell library.

## What it demonstrates

- Figma-inspired shell with a vertical tool rail, AST object outline, center stage, and property inspector
- live SVG AST editing against a shared `SvgDocument`
- hit testing through `Svg.Controls.Skia.Uno.Svg`
- selection overlays, move, resize, and rotation handles through `Svg.Editor.Skia`
- vector creation tools for select, pen line, rectangle, ellipse, line, text, polygon, and freehand
- zoom and pan directly on the Uno stage
- property editing through reflected SVG attributes
- reusable editor shell composition through `Svg.Editor.Skia.Uno.Controls.SvgEditorShell`
- smaller reusable editor parts:
  - `SvgEditorTopBar`
  - `SvgEditorUtilityRail`
  - `SvgEditorSidebar`
  - `SvgEditorStagePanel`
  - `SvgEditorInspectorPanel`
  - `SvgEditorOverlayCanvas`

## Prerequisites

```bash
uno-check --target desktop --target web --target android --target ios
```

## Run desktop

```bash
cd /Users/wieslawsoltes/GitHub/Svg.Skia/samples/UnoSvgEditorSample
dotnet build -c Release -f net10.0-desktop
dotnet run -c Release -f net10.0-desktop
```

## Build other targets

WebAssembly:

```bash
dotnet publish -c Release -f net10.0-browserwasm
```

Android:

```bash
dotnet build -c Release -f net10.0-android
```

iOS on macOS:

```bash
dotnet build -c Release -f net10.0-ios
```
