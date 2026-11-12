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

Uno 7 excludes development-server dependencies from optimized builds. When a
build or pack uses `--no-restore`, restore with the same configuration first:

```sh
dotnet restore Svg.Skia.slnx -p:Configuration=Release
dotnet build Svg.Skia.slnx -c Release --no-restore
```

A Debug restore followed by a Release build can retain development-only assets
and fails with `UNOB0019`. CI restores the Release graph explicitly; see the
[package migration notes][packages].

## Browser backend selection

The browser target in [UnoDraw.csproj](../samples/UnoDraw/UnoDraw.csproj)
combines these features:

```xml
<UnoFeatures>Skia;WebGpu</UnoFeatures>
```

`WebGpu` adds the WebGPU backend. Explicit `Skia` retains the existing drawing
API and the fallback backend, because selecting `WebGpu` suppresses implied
Skia. The additional WebGPU feature is conditional on the browser target.
The optional `GLCanvas` feature and `Uno.WinUI.Graphics3DGL` package are omitted;
the existing `SKCanvasElement` raster bridge supplies SVG content to WebGPU.

[Program.cs](../samples/UnoDraw/Platforms/WebAssembly/Program.cs) registers
providers in preference order:

```csharp
using Uno.UI.Composition.Drawing;
using Uno.UI.Composition.Skia;
using Uno.UI.Composition.WebGpu;
using Uno.UI.Hosting;

var host = UnoPlatformHostBuilder.Create()
    .App(() => new App())
    .GraphicsBackend(WebGpuBackend.CreateGraphicsProvider())
    .GraphicsBackend(SkiaBackend.CreateGraphicsProvider(
        GraphicsContextKind.WebGL, GraphicsContextKind.Software))
    .UseWebAssembly()
    .Build();

BrowserStartup.DeferInitialFrame();
await host.RunAsync();
```

Registering providers replaces Uno's implicit renderer preference. WebGPU itself
does not contain a WebGL renderer: the second provider supplies the fallback.
The explicit browser context order avoids probing native Vulkan, OpenGL, OpenGLES
and Metal contexts that the browser host cannot create.

The supported startup order is:

1. WebGPU compositor.
2. Skia compositor with WebGL2, then WebGL1 if context creation fails.
3. Skia software rendering, presented through Canvas2D.

The [graphics registry][graphics-registry] advances when a context or provider
cannot initialize. The [browser host][browser-renderer] creates only browser
context kinds, and its [WebGL helper][webgl-helper] performs the WebGL2-to-WebGL1
retry. A SwiftShader WebGPU/WebGL adapter still uses the corresponding GPU API;
it is distinct from Uno's `Software` context with Canvas2D presentation.

### Selecting direct Skia rendering

Open `/Svg.Skia/unodraw/?renderer=skia` to register only Skia, with the same
WebGL-to-software fallback. This provides a supported comparison for SVG-heavy
documents because it draws directly into the Skia frame instead of uploading
SVG raster textures to the WebGPU compositor. It does not promise that either
backend is faster for every document or device.

The preference is read once before host creation. The value is trimmed and
compared without case sensitivity; missing or unrecognized values retain the
default WebGPU-first order. It does not modify the SVG, workspace preferences,
or the PWA manifest's application scope. Reloading the same URL retains the
choice; removing the query restores the default for the next startup.

This negotiation happens during startup. The pinned runtime's
[device-loss handler][webgpu-init] reports a lost device but does not replace
device-bound resources with another provider. Later rendering errors remain
visible, and UnoDraw does not automatically reload a document to switch backends.
Save changes before manually reloading with another renderer preference.
Uno marks its WebGPU backend experimental; see the [backend documentation][webgpu].

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
| WebGPU | Skia draws into a device-resolution raster `SKSurface`; Uno uploads the pixels as a backend texture and composites the UI with WebGPU. |
| Skia | The element draws into the host's `SKCanvas` directly. |

The implementation is in the pinned [`SKCanvasElement`][skcanvas] source.
WebGPU changes UI composition while retaining the SVG engine. The raster surface
is reused at an unchanged device size. When an invalidated visual is recorded
again, it reruns Skia drawing and creates a texture. The pinned
[`WebGpuTexture.UploadBgra`][webgpu-texture] path allocates a converted RGBA buffer,
builds mip levels on the CPU, and uploads them. Avoiding unnecessary invalidation
therefore avoids both SVG replay and texture preparation. Unchanged retained
visuals already keep their recorded resources; another application bitmap cache
would duplicate that storage without removing the bridge.

SVG rasterization and upload remain part of this path; performance depends on the
document and device. Use the explicit Skia preference to compare the direct
WebGL path on target hardware before drawing performance conclusions.
The [performance guide](unodraw-performance.md) documents the editor's artboard
and ruler caches, the retained Navigator overview, and measured preparation costs.

The optional GL island is excluded because Uno 7.0.32's
[`GLCanvasElement.UpdateFramebuffer`][glcanvas] disposes a zero-sized framebuffer
before making its GL context current. A responsive layout can temporarily arrange
a surface to zero width. With WebGPU composition, that teardown has no current
WebGL context and throws from `deleteFramebuffer`, interrupting the layout.
The normal-size branch makes the context current correctly. This behavior was
confirmed in the published browser's resize trace and the pinned
[framebuffer-disposal implementation][framebuffer-details].

Omitting that optional bridge selects Uno's existing raster-surface path and
retains WebGPU composition, the complete SVG renderer and automatic Skia startup
fallback. No runtime internals are patched and no artificial minimum size is
imposed on editor controls. A future runtime upgrade can reevaluate the GL path
against the same camera, compact-layout and reload tests.

Uno 7's [`SKCanvasVisual`][skcanvas-visual] uses a
[`BorderVisual`][border-visual] whose pointer hit testing depends on background
or border geometry. Interactive drawing surfaces set a transparent
background so their complete bounds receive input while preserving their Skia
painting. This applies to the editor canvas, document rulers, mesh editor,
timeline ruler and reusable SVG control; passive previews retain their existing
input behavior. Native interaction tests exercise actual drawing and ruler
gestures, including their saved SVG results.

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
`Software`. This reports the application's UI compositor; the SVG compatibility
surface does not negotiate another UI backend. A `navigator.gpu` property or a
successful adapter probe alone cannot establish which provider rendered the
application.

The [Docs workflow](../.github/workflows/docs.yml) and
[published-browser harness](../tests/UnoDraw.BrowserSmoke/smoke.py) must validate
four separate runs of the same published package:

| Mode | Required evidence on startup and reload |
| --- | --- |
| `webgpu` | WebGPU registry winner, rendered workspace and SVG artwork, and successful editor interactions. |
| `skia-fallback` | WebGPU adapter unavailability, usable WebGL, and a Skia/WebGL winner. |
| `software-fallback` | WebGPU adapter and WebGL1/2 unavailability, usable Canvas2D, and a Skia/Software winner. |
| `skia-preferred` | `?renderer=skia`, an available WebGPU adapter, usable WebGL, and a Skia/WebGL winner. |

The fallback run disables Chromium's `WebGPUService` feature so adapter creation
fails through the browser's normal implementation. Uno reports the expected
`WebGpuInit: navigator.gpu.requestAdapter returned null` message before selecting
Skia. The software case also disables both WebGL versions with Chromium's
`--disable-webgl` switch and requires the exact known WebGL initialization error.
Fresh detached canvases corroborate availability without replacing the app's
canvas. The preferred-Skia case leaves WebGPU available and proves that the URL
choice bypasses it.

Expected initialization errors are admitted only before the successful
negotiation within that navigation's log slice. The same messages during editing
remain failures, and reload must supply fresh evidence. All four cases retain
the existing startup, theme-command, font/indicator, compact
layout, reload and PWA-scope checks, and check canvas artwork separately from
the surrounding UI. Actual-size and fit-artboard keyboard commands must repaint
the SVG's three color families and restore the fitted document pixels, checking
the compatibility bridge beyond its first frame.

Before the camera sequence, the harness waits for the returned dark canvas and
both rulers to match their initial fitted pixels. Window controls can update
before a surface publishes its replacement pixels, so dark chrome alone does
not establish that the whole frame has settled. The readiness comparison retains
the same pixel tolerances as camera restoration and requires no additional input
or fixed delay. The panel-rail comparison uses this same settled desktop frame.

Command search synchronizes its results with the current native text before
Enter, list navigation or Run can consume a selection. Uno queues `TextChanged`,
so relying only on that event can execute a previous result when input arrives
before the notification. Duplicate notifications for an already filtered query
preserve the user's arrow-key selection. The repeated light/dark command-search
checks exercise this ordering without inserting an extra typing delay.

Headless CI uses a software GPU implementation to exercise the WebGPU API path.
Its results establish rendering and interaction behavior, not hardware
performance. Inspect the workflow's per-backend reports, screenshots and traces
for a particular commit; this guide does not constitute a completed test result.

[packages]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/doc/articles/migrating-to-uno-7/packages.md
[migration]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/doc/articles/migrating-to-uno-7.md
[webgpu]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/doc/articles/features/using-webgpu-rendering.md
[webgpu-spec]: https://www.w3.org/TR/webgpu/#navigator-gpu
[secure-contexts]: https://www.w3.org/TR/secure-contexts/#is-origin-trustworthy
[skcanvas]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/AddIns/Uno.WinUI.Graphics2DSK/SKCanvasElement.cs
[skcanvas-visual]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/AddIns/Uno.WinUI.Graphics2DSK/SKCanvasVisual.cs
[border-visual]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/Uno.UI.Composition/Composition/BorderVisual.skia.cs
[glcanvas]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/AddIns/Uno.WinUI.Graphics3DGL/GLCanvasElement.cs
[framebuffer-details]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/AddIns/Uno.WinUI.Graphics3DGL/GLCanvasElement.FrameBufferDetails.cs
[startup]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/doc/articles/migrating-to-uno-7/app-startup.md
[project-setup]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/doc/articles/migrating-to-uno-7/project-setup.md#minimum-os-versions-raised
[settings-migration]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/Uno.WinRT/Storage/ApplicationDataMigrator.UIKit.cs
[graphics-registry]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/Uno.UI.Composition.Drawing/GraphicsRegistry.cs
[browser-renderer]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/Uno.UI.Runtime.WebAssembly.Browser/Rendering/BrowserRenderer.cs
[webgl-helper]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/Uno.UI.Runtime.WebAssembly.Browser/ts/Runtime/EmscriptenWebGL.ts
[webgpu-init]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/Uno.UI.Composition.WebGpu/ts/Runtime/WebGpuInit.ts
[webgpu-texture]: https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/Uno.UI.Composition.WebGpu/WebGpuDrawingFactory.cs
