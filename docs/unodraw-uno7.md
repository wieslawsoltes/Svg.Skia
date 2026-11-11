# UnoDraw on Uno Platform 7

UnoDraw uses Uno Platform 7 on .NET 10 and prefers WebGPU for browser UI
composition. The editor continues to use the repository's SVG DOM, Svg.Skia
renderer and reusable EditorTheme controls.

## Version contract

| Component | Version | Source of the pin |
| --- | --- | --- |
| Uno SDK | `7.0.5` | [`global.json`](../global.json) |
| Uno runtime packages | `7.0.32` | Uno SDK's published package manifest |
| SkiaSharp and native assets | `4.151.1` | [`Directory.Packages.props`](../Directory.Packages.props) |
| HarfBuzzSharp and native assets | `14.4.0` | [`Directory.Packages.props`](../Directory.Packages.props) |

These versions match the manifest inside the published
[Uno.Sdk 7.0.5 package](https://www.nuget.org/packages/Uno.Sdk/7.0.5).
The runtime implementation referenced below is pinned to the
[Uno 7.0.32 source commit](https://github.com/unoplatform/uno/commit/78c746d28293975e64ba0f162af6f44f3c3daaab).
Keep managed SkiaSharp/HarfBuzzSharp and their native assets aligned across the
application and its project references.

Uno 6 libraries are binary-incompatible with Uno 7. Rebuild every Uno-dependent
library, including `Svg.Controls.Skia.Uno` and `Svg.Editor.Skia.Uno`, against the
new SDK; updating only the application is insufficient. Libraries using
`SKCanvasElement` explicitly declare the `Skia` feature. See the upstream
[migration guide][migration] for the framework and package changes.

## Browser backend selection

The browser target in [UnoDraw.csproj](../samples/UnoDraw/UnoDraw.csproj)
combines these features:

```xml
<UnoFeatures>Skia;WebGpu;GLCanvas</UnoFeatures>
```

`WebGpu` adds the WebGPU backend. Explicit `Skia` retains the existing drawing
API and the fallback backend, because selecting `WebGpu` suppresses implied
Skia. `GLCanvas` supplies Uno's OpenGL compatibility island. The additional
browser features are conditional on `net10.0-browserwasm`.

[Program.cs](../samples/UnoDraw/Platforms/WebAssembly/Program.cs) registers
providers in preference order:

```csharp
using Uno.UI.Composition.Skia;
using Uno.UI.Composition.WebGpu;
using Uno.UI.Hosting;

var host = UnoPlatformHostBuilder.Create()
    .App(() => new App())
    .GraphicsBackend(WebGpuBackend.CreateGraphicsProvider())
    .GraphicsBackend(SkiaBackend.CreateGraphicsProvider())
    .UseWebAssembly()
    .Build();

BrowserStartup.DeferInitialFrame();
await host.RunAsync();
```

Registering providers replaces Uno's implicit renderer preference. The second
registration is necessary: if WebGPU cannot initialize, the browser host tries
Skia with WebGL and then software rendering. This is startup negotiation;
later rendering or device-loss errors remain visible in the console. Uno marks
its WebGPU backend experimental; see the [backend documentation][webgpu].

Browser WebGPU requires a supported browser, an available adapter and a
[secure context][webgpu-spec]. Serve production builds over HTTPS; localhost
and loopback HTTP origins support local development under the
[Secure Contexts rules][secure-contexts]. Capability can still be disabled by
browser policy or driver restrictions. The published application keeps
`WasmEnableThreads=false` for GitHub Pages hosting without cross-origin
isolation headers.

The [browser startup hook](../samples/UnoDraw/Platforms/WebAssembly/App.WebAssembly.cs)
preloads the actual text and symbol font families before releasing the first
frame. It uses Uno 7's shared `Uno.UI.Runtime.BrowserRenderer` scheduler for
both providers.

## How SVG content reaches the screen

`SvgEditorCanvas` continues drawing with `SKCanvasElement` and `SKSvg.Draw`.
The selected UI backend determines how Uno composites that content:

| Selected UI backend | SVG drawing and composition |
| --- | --- |
| WebGPU, with a usable GL island | Skia draws into an offscreen WebGL framebuffer; Uno reads back its pixels and composites the image through WebGPU. |
| WebGPU, with Uno's raster fallback | Skia draws into a device-resolution raster `SKSurface`; Uno uploads the pixels as a backend texture. |
| Skia | The element draws into the host's `SKCanvas` directly. |

The implementation is in the pinned [`SKCanvasElement`][skcanvas],
[`SkiaGLIslandRenderer`][skia-island] and [`GLCanvasElement`][glcanvas] sources.
The GL path includes a readback/copy boundary. WebGPU therefore changes UI
composition while retaining the SVG engine; performance depends on the document,
device and compatibility path. Measure it on the target hardware before drawing
performance conclusions.

## Native host migration

Android targets API 24 or later. Its application derives from
`Uno.UI.Runtime.Android.NativeApplication`, passes the JNI reference to the
base constructor, and overrides `CreateHost()` using
`UnoPlatformHostBuilder.Create().App(() => new App()).UseAndroid().Build()`.
The activity derives from `Uno.UI.Runtime.Android.ApplicationActivity`.
These are required Uno 7 [startup changes][startup]; the
[platform setup guide][project-setup] defines the raised minimum OS versions.

The iOS host uses the `Uno.UI.Runtime.AppleUIKit` namespace and invokes
`Uno.Storage.ApplicationDataMigrator.MigrateSettings()` before constructing
`App`. This moves pre-7 `LocalSettings`/`RoamingSettings` into Uno's dedicated
settings suite, preserving shortcut profiles and saved workspace preferences.
Repeated calls retain existing destination values. The move is one-way, so a
downgrade does not recover those values from the old store; see the
[migration implementation][settings-migration].

Shared suspension handling uses
`Windows.ApplicationModel.Core.CoreApplication.Suspending`, retaining the
recovery-checkpoint deferral. Desktop close handling and the iOS background
save hook continue to handle their respective native lifecycles.

## Verifying the selected backend and artwork

The browser entry point enables Information logging for
`Uno.UI.Composition.Drawing` before host creation, including Release builds.
Uno's [graphics registry][graphics-registry] emits the winner only after
successful initialization:

```text
Graphics backend 'WebGpuGraphicsProvider' won negotiation on context kind 'WebGpu'.
```

Skia fallback instead reports `SkiaGraphicsProvider` with context `WebGL` or
`Software`. The GL compatibility island does not emit another registry winner.
A `navigator.gpu` property or a successful adapter probe alone cannot establish
which provider rendered the application.

The [Docs workflow](../.github/workflows/docs.yml) and
[published-browser harness](../tests/UnoDraw.BrowserSmoke/smoke.py) must validate
two separate runs of the same published package:

| Mode | Required evidence on startup and reload |
| --- | --- |
| `webgpu` | WebGPU registry winner, rendered workspace and SVG artwork, and successful editor interactions. |
| `skia-fallback` | WebGPU adapter unavailability, Skia registry winner, rendered workspace and SVG artwork, and successful editor interactions. |

The fallback run disables Chromium's `WebGPUService` feature so adapter creation
fails through the browser's normal implementation. Uno reports the expected
`WebGpuInit: navigator.gpu.requestAdapter returned null` message before selecting
Skia. The harness distinguishes that exact expected message from other runtime
errors. It retains the existing startup, theme-command, font/indicator, compact
layout, reload and PWA-scope checks, and checks canvas artwork separately from
the surrounding UI.

Headless CI uses a software GPU implementation to exercise the WebGPU API path.
Its results establish rendering and interaction behavior, not hardware
performance. Inspect the workflow's per-backend reports, screenshots and traces
for a particular commit; this guide does not constitute a completed test result.

[migration]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/doc/articles/migrating-to-uno-7.md
[webgpu]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/doc/articles/features/using-webgpu-rendering.md
[webgpu-spec]: https://www.w3.org/TR/webgpu/#navigator-gpu
[secure-contexts]: https://www.w3.org/TR/secure-contexts/#is-origin-trustworthy
[skcanvas]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/AddIns/Uno.WinUI.Graphics2DSK/SKCanvasElement.cs
[skia-island]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/AddIns/Uno.WinUI.Graphics2DSK/SkiaGLIslandRenderer.cs
[glcanvas]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/AddIns/Uno.WinUI.Graphics3DGL/GLCanvasElement.cs
[startup]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/doc/articles/migrating-to-uno-7/app-startup.md
[project-setup]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/doc/articles/migrating-to-uno-7/project-setup.md#minimum-os-versions-raised
[settings-migration]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/Uno.WinRT/Storage/ApplicationDataMigrator.UIKit.cs
[graphics-registry]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/Uno.UI.Composition.Drawing/GraphicsRegistry.cs
