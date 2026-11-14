# UnoDraw

A native SVG design workspace built with Uno Platform 7, using the Svg.Skia document and rendering pipeline throughout.

[Open UnoDraw in your browser](https://wieslawsoltes.github.io/Svg.Skia/unodraw/).

The `Docs` workflow builds the Release WebAssembly package, smoke-tests its default
Skia renderer and explicit WebGPU option under `/Svg.Skia/unodraw/` without cross-origin
isolation headers, and publishes it alongside the documentation in one GitHub Pages deployment.
Pushes to the default branch and manual `Docs` runs on that branch deploy only after
both builds and all browser backend checks pass. Pull requests validate the site
without publishing it.

```sh
git submodule update --init --recursive
dotnet run --project samples/UnoDraw/UnoDraw.csproj -f net10.0-desktop
```

Pass an SVG path after `--` to open it at startup. The default desktop target runs on Windows, macOS and Linux/X11 and requires no mobile workloads.

The same workspace also has opt-in WebAssembly, Android and iOS targets. For example:

```sh
dotnet workload install wasm-tools
dotnet publish samples/UnoDraw/UnoDraw.csproj -c Release -f net10.0-browserwasm -p:UnoDrawTargetFrameworks=net10.0-browserwasm
```

The browser host uses **Skia by default**, preferring WebGL and falling back to
software rendering when WebGL is unavailable. The SVG canvas uses Svg.Skia directly
through the selected Skia backend.

Use `?renderer=webgpu` to opt into Uno's experimental WebGPU compositor. That option
still falls back to Skia/WebGL and then software if WebGPU is unavailable. The SVG
canvas continues using the original Svg.Skia painter and its raster-upload bridge;
it is not a direct WebGPU implementation of the SVG engine.

| Browser query | Renderer preference |
| --- | --- |
| No `renderer`, an empty value, or an unrecognized value | Skia: WebGL, then software |
| `?renderer=skia` | Skia: WebGL, then software |
| `?renderer=webgpu` | WebGPU, then Skia/WebGL, then software |

Renderer values ignore surrounding whitespace and case. Canvas bridge controls
apply only when WebGPU is explicitly selected; for example,
`?renderer=webgpu&canvasBridge=single-canvas` selects the original one-canvas upload.
The renderer preference is read at startup and does not change document settings.

See the [Uno 7 and browser backend guide](../../docs/unodraw-uno7.md) for the package
versions, host migration and provider verification.

The editor guide includes mobile build commands and required SDKs. Browser file uploads save through a new destination or download; Android and iOS use native file-provider capabilities. The responsive menu exposes layers, the inspector and every editor command at narrow widths.

On Linux, use a desktop session with D-Bus and an XDG desktop portal backend for
native Open and Save dialogs (for example, `xdg-desktop-portal-gtk` or the KDE
backend). The renderer uses the host's accelerated backend when available and
falls back to software when necessary.

See [the editor guide](../../docs/unodraw.md) for architecture, controls, validation and feature coverage.
