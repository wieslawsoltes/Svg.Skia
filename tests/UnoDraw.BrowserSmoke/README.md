# Published browser runtime checks

The Docs workflow publishes UnoDraw once with Release `InterpreterAndAOT` settings
and runs this harness in four configurations against that exact artifact, under
the GitHub Pages project/application path `/Svg.Skia/unodraw/`. Deployment requires
every case to pass. No case changes the application package or mocks its
rendering APIs.

| Mode | Browser configuration | Required application renderer |
| --- | --- | --- |
| `webgpu` | SwiftShader Vulkan and ANGLE, with WebGPU enabled | `WebGpuGraphicsProvider` on `WebGpu` |
| `skia-fallback` | Chromium's `WebGPUService` feature disabled, with software WebGL available | `SkiaGraphicsProvider` on `WebGL` |
| `software-fallback` | `WebGPUService` and WebGL disabled, with Canvas2D available | `SkiaGraphicsProvider` on `Software` |
| `skia-preferred` | WebGPU and WebGL available, with the application URL's `?renderer=skia` option | `SkiaGraphicsProvider` on `WebGL` |

These are software graphics correctness checks. They do not measure hardware GPU
performance. The report records the Chromium version, launch arguments, adapter
information, and actual Uno renderer-negotiation messages. Adapter availability
alone cannot pass the gate: the application must select the expected renderer on
startup and again after reload, using fresh logs for each navigation.

The WebGPU case uses the WebGPU compositor for the interface. Existing Skia SVG
and ruler drawings reach it through Uno's software `SKCanvasElement` bitmap
bridge. The optional browser
`Graphics3DGL` bridge is excluded: its pinned 7.0.32 zero-size framebuffer cleanup
raised an unhandled context error during
[responsive-resize validation](https://github.com/wieslawsoltes/Svg.Skia/actions/runs/43122185452/job/130875560161).
Renderer assertions cover the main compositor; artwork and repaint assertions
cover the retained Skia bitmaps.

Both fallback cases require the secure browser origin to expose `navigator.gpu`
while `requestAdapter()` returns `null`. They also require Uno's precise no-adapter
negotiation error and a successful Skia selection. The WebGL fallback requires a
working WebGL context. The software fallback independently proves that neither
WebGL version can create a context, Canvas2D is available, and Uno reports its
precise unavailable-WebGL error before choosing software rendering. Probes use
separate detached canvases and release their WebGL contexts.

Expected negotiation errors are accepted only before the first successful
renderer selection in each navigation's startup log slice. The same error during
editing, after a winner, or outside those slices still fails. Other console
errors, page exceptions and failed application resources remain failures. A
WebGPU run that silently chooses Skia fails; a WebGL case that chooses software
also fails.

The direct-Skia case requires an available WebGPU adapter but selects Skia through
the startup URL option. It verifies that the option survives reload without
altering the PWA's folder scope or start URL. The option is case insensitive,
ignores surrounding whitespace, and is read once before host creation. An absent
or unrecognized value preserves the default WebGPU-first order. This is a manual
startup choice; Uno 7.0.32 does not renegotiate providers after WebGPU device loss.

Chromium documents the software graphics switches in its
[SwiftShader guide](https://chromium.googlesource.com/chromium/src/+/main/docs/gpu/swiftshader.md).
Its own
[WebGPU kill-switch tests](https://chromium.googlesource.com/chromium/src/+/master/content/test/gpu/gpu_tests/pixel_test_pages.py)
use `--disable-features=WebGPUService` and explain why `--enable-unsafe-webgpu`
must not accompany the disabling switch. Enabling Vulkan in the WebGPU case also
initializes the browser compositor path used to present canvas textures; the
rendered application checks verify that this path actually works.

## Coverage

All twelve existing runtime checks and pixel thresholds run in every mode:
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
Before sending either shortcut, the same restoration comparison waits for the
document crop and both ruler strips to match the original dark fitted image and saves
`02b-artwork-settled.png` as its baseline. A retained bitmap can update after the
surrounding chrome changes theme; this readiness check requires the canvas and
rulers to settle without adding a fixed delay or changing pixel tolerances.
Rulers use separate narrow comparisons at the same 0.1%
threshold; the document comparison region remains unchanged.
The compact panel-rail comparison reuses this settled desktop baseline so
intermediate ruler colors cannot distort its glyph-location search.
This verifies repaint through the canvas bridge without changing the SVG.

The four matrix jobs upload distinct `unodraw-browser-smoke-<mode>` artifacts.
Each contains `report.json`,
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
python tests/UnoDraw.BrowserSmoke/smoke.py --site artifacts/pages --base-path /Svg.Skia/unodraw/ --backend software-fallback --output artifacts/unodraw-browser-smoke-software-fallback
python tests/UnoDraw.BrowserSmoke/smoke.py --site artifacts/pages --base-path /Svg.Skia/unodraw/ --backend skia-preferred --output artifacts/unodraw-browser-smoke-skia-preferred
```

The local HTTP server deliberately omits COOP/COEP headers to match GitHub Pages.
Diagnostics must be outside its document root. The unit checks exercise missing
or stale renderer evidence, silent fallback, invalid browser contexts, narrowly
scoped expected errors, blank/frozen artwork, outline-only changes, incomplete
color-family repaint, failed camera restoration, and canvas/ruler frames that still
use the previous theme after their surrounding chrome updates. They supplement the
four published-runtime runs.
