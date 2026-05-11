---
title: "Svg.Skia.JavaScript"
---

# Svg.Skia.JavaScript

`Svg.Skia.JavaScript` is the optional bridge that connects `SKSvg` to the Jint-backed `Svg.JavaScript` runtime. `Svg.Skia` does not reference this package by default, so regular rendering and NativeAOT builds do not pull in a JavaScript engine.

## Install

```bash
dotnet add package Svg.Skia.JavaScript
```

## Enable JavaScript

Register the bridge once during application startup, then enable JavaScript per `SKSvg` instance.

```csharp
using Svg.Skia;

SKSvgJavaScriptRuntime.Register();

using var svg = new SKSvg();
svg.Settings.EnableJavaScript = true;
svg.Load("Assets/interactive.svg");
```

If `EnableJavaScript` is set without a registered runtime, `SKSvg` throws an `InvalidOperationException` with registration guidance.
