# Published browser runtime checks

The Docs workflow publishes UnoDraw once with Release `InterpreterAndAOT` settings
and runs this harness twice against that exact artifact, under the GitHub Pages
project/application path `/Svg.Skia/unodraw/`. Deployment requires both cases to
pass. Neither case changes the application package or mocks its rendering APIs.

| Mode | Browser configuration | Required application renderer |
| --- | --- | --- |
| `webgpu` | SwiftShader Vulkan and ANGLE, with WebGPU enabled | `WebGpuGraphicsProvider` on `WebGpu` |
| `skia-fallback` | Chromium's `WebGPUService` feature disabled, with software WebGL available | `SkiaGraphicsProvider` on `WebGL` or `Software` |

These are software graphics correctness checks. They do not measure hardware GPU
performance. The report records the Chromium version, launch arguments, adapter
information, and actual Uno renderer-negotiation messages. Adapter availability
alone cannot pass the gate: the application must select the expected renderer on
startup and again after reload, using fresh logs for each navigation.

The fallback case requires the secure browser origin to expose `navigator.gpu`
while `requestAdapter()` returns `null`. It also requires Uno's precise no-adapter
negotiation error and a successful Skia selection. Only that error is expected in
this mode; other console errors, page exceptions and failed application resources
remain failures. A WebGPU run that silently chooses Skia fails.

Chromium documents the software graphics switches in its
[SwiftShader guide](https://chromium.googlesource.com/chromium/src/+/main/docs/gpu/swiftshader.md).
Its own
[WebGPU kill-switch tests](https://chromium.googlesource.com/chromium/src/+/master/content/test/gpu/gpu_tests/pixel_test_pages.py)
use `--disable-features=WebGPUService` and explain why `--enable-unsafe-webgpu`
must not accompany the disabling switch. Enabling Vulkan in the WebGPU case also
initializes the browser compositor path used to present canvas textures; the
rendered application checks verify that this path actually works.

## Coverage

All twelve existing runtime checks and pixel thresholds run in both modes:
startup without cross-origin isolation, nonblank desktop rendering, desktop
indicators, keyboard command search and dark/light theme dispatch, indicator
contrast, initial font stability, compact rendering and indicators, panel-rail
glyph preservation, reload, and PWA folder scoping. Two additional checks prove
the renderer on startup/reload. Another inspects substantial indigo, green and
peach regions of the welcome SVG inside the document viewport. This catches a
blank Skia canvas bridge even when the surrounding interface renders correctly.

A sixteenth check sends the real Ctrl+1 (actual size) and Ctrl+0 (fit artboard)
shortcuts. Actual size must preserve all three artwork color families and change
at least 500 interior pixels in each, after a 7×7 erosion removes thin selection
outlines. Fit artboard must preserve those colors and restore the document crop
with at most 0.1% differing pixels above a luminance difference of 12/255. Both
initial and active-artboard fitting use the same 48px padding for the single
welcome artboard, so the camera returns to the same view. Screenshots
`02c-artwork-actual-size.png` and `02d-artwork-refitted.png` capture both states.
This verifies repaint through the canvas bridge without changing the SVG.

The two matrix jobs upload distinct `unodraw-browser-smoke-webgpu` and
`unodraw-browser-smoke-skia-fallback` artifacts. Each contains `report.json`,
screenshots and a Playwright trace, plus the final HTML/screenshot on failure.
The report preserves expected negotiation errors alongside other console output.

## Run locally

Use the same pinned browser dependencies and an already published application:

```sh
python -m pip install -r tests/UnoDraw.BrowserSmoke/requirements.txt
python -m playwright install --with-deps chromium
python -m unittest discover -s tests/UnoDraw.BrowserSmoke -p 'test_*.py'
mkdir -p artifacts/pages/Svg.Skia/unodraw
cp -a artifacts/unodraw-browser/wwwroot/. artifacts/pages/Svg.Skia/unodraw/
python tests/UnoDraw.BrowserSmoke/smoke.py --site artifacts/pages --base-path /Svg.Skia/unodraw/ --backend webgpu --output artifacts/unodraw-browser-smoke-webgpu
python tests/UnoDraw.BrowserSmoke/smoke.py --site artifacts/pages --base-path /Svg.Skia/unodraw/ --backend skia-fallback --output artifacts/unodraw-browser-smoke-skia-fallback
```

The local HTTP server deliberately omits COOP/COEP headers to match GitHub Pages.
Diagnostics must be outside its document root. The unit checks exercise missing
or stale renderer evidence, silent fallback, invalid browser contexts, narrowly
scoped expected errors, blank/frozen artwork, outline-only changes, incomplete
color-family repaint, and failed camera restoration. They supplement the
two published-runtime runs.
