# UnoColorPickerSample

Standalone Uno Platform sample showcasing the reusable `Svg.Controls.ColorPicker.Uno` library.

## What it demonstrates

- direct hosting of `FigmaColorPicker` outside the editor shell
- custom swatches plus library-backed paint styles
- RGB, HSL, and HEX editing flows
- `PaintStyleRequested` and `CreateStyleRequested` event handling
- switching between fill, stroke, and shared style targets

## Run desktop

```bash
cd /Users/wieslawsoltes/GitHub/Svg.Skia/samples/UnoColorPickerSample
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
