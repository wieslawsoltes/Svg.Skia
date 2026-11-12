# UnoDraw browser rendering regression

This investigation measures the interactive rendering regression reported after
the EditorTheme and Uno Platform 7 merge. The preparation-only measurements in
`unodraw-performance.md` do not establish browser frame rate or input latency.

## Comparison revisions

| Role | Revision |
| --- | --- |
| Previously deployed master | `067901c6396ca4a9cca2b335424c961a86f3cdf9` |
| EditorTheme immediately before the Uno 7 migration | `dbe9dd165f7eb63c1c6c9e6dac11091728062824` |
| Reported regression / merged Uno 7 build | `96e5fa9d84b694e34a626b8867f2466fc231b6a7` |
| Candidate | The exact commit recorded by the performance workflow |

The pre-migration EditorTheme revision isolates framework changes from the
larger workspace redesign. The previously deployed revision is a separate
historical reference; differences in its document, chrome or interaction model
must be reported rather than attributed to the renderer.

## Measurement contract

- Keep the same SVG input, browser version, viewport, device-pixel ratio,
  runtime execution mode and interaction sequence for comparable revisions.
- Measure idle, pan, zoom, selection and document-edit workloads separately.
- Separate animation-frame opportunities, graphics submissions, managed paint
  callbacks and input-to-submission timing. A requestAnimationFrame callback is
  not evidence that the editor rendered or that a frame reached the display.
- Record raw samples, percentiles, CPU/layout metrics, allocation information
  where available, actual provider selection, environment and revision IDs.
- Warm the runtime before sampling and rotate revision/backend execution order
  across repeated trials on the same runner.
- Treat headless software GPU measurements as comparative implementation
  evidence. Physical GPU performance requires a separate hardware run.

## Quality contract

Optimizations must retain device resolution, antialiasing, text, gradients,
filters, clipping, blend behavior and editing semantics. No lower-resolution
interaction mode or simplified artwork is introduced to improve a benchmark.
The existing browser artwork, zoom/refit, first-frame font, theme, indicator,
compact-layout, reload and renderer-negotiation checks remain unchanged.

## Results

### Guide-query profile

The [raw native Release measurements](benchmarks/unodraw-guides-2026-11-12.json)
compare the previous nine-anchor snap query with the indexed query in commit
`b5d82812fedb14b7ac9edd9cf8373fe435c97704`. Both use the same candidate geometry,
five-unit tolerance and existing smart-guide limit. Every sampled snapped point
and chosen guide ID is checked before timing. Five samples follow 128 warmup
iterations; each sample contains 256 nine-anchor queries, with alternating order.

| Candidate guides | Previous query | Indexed query | Query speedup |
| --- | ---: | ---: | ---: |
| 606 | 479.927 µs | 30.240 µs | 15.87× |
| 6,000 | 4,736.966 µs | 90.895 µs | 52.11× |
| 6,000 plus 128 diagonal guides | 4,805.778 µs | 208.988 µs | 23.00× |

The indexed steady-state query allocated zero managed bytes in this run on
.NET 10.0.14 / Debian 13. These are native algorithm measurements, excluding SVG
painting, browser interop, texture transfer and composition. They do not establish
browser frame rate or an application-wide speedup.

### Pixel preservation

The native quality gate uses the production `SkiaCanvasUploadBuffer`, linked into
the test assembly without a UI runtime. All 72 cases match the legacy pixels
exactly: eight SVG scenes, DPR 1/1.25/2, and three fractional camera transforms.
The scenes exercise gradients, filters, masks, clipping, patterns, text, an
embedded image with alpha, opacity and strokes. Comparisons use raw premultiplied
bytes, with zero tolerance and nonblank/partial-alpha assertions.

This gate rejected direct RGBA rasterization in 63 of 72 cases: Skia's RGBA and
BGRA painting paths can round filter and coverage intermediates differently.
The implementation therefore retains BGRA painting and uses native
`SKSwizzle.SwapRedBlue` to permute channels without changing alpha or colors.

These tests establish CPU pixel equivalence. The separate browser gate compares
the persistent and legacy paths at native screenshot resolution for DPR
1/1.25/2, including selection/brush overlays, Navigator hide/show, actual-size
zoom, refit and compact/near-zero/desktop resizing. Browser composition results
remain a required gate.

A [separate old/new high-DPI gate](../tests/UnoDraw.BrowserRenderQuality/README.md)
compares both WebGPU and Skia/WebGL at DPR 1.25 and 2. This checks the changed
ruler UI against the regression baseline, whereas both sides of the bridge A/B
use the candidate's ruler implementation. It reuses existing AOT artifacts and
rejects them if the current checkout differs in production source. Complete
native PNGs and unresampled ruler strips cover fitted, actual-size, fractionally
panned, refitted and selected states. Missing cases cannot produce a passing
matrix; the same image thresholds apply.

This comparison found a real fractional-DPI defect in the initial retained ruler
marker: at DPR 1.25, Uno's WebGPU path atlas quantized its one-unit Shape rectangle
by 0.125 physical pixels. The marker now uses a zero-corner, zero-border `Border`
background, which reaches Uno's analytic rectangle primitive with the same
bounds, translation and coverage. The correction requires the original old/new
browser gate; it does not relax its ruler threshold. Matching Interpreter builds
provide earlier correctness feedback, with the AOT comparison still required.

The artifact-consuming verification workflow also replays the complete timing
matrix in twelve workload/backend jobs. Every matched comparison keeps all
revisions and all three repeats on one runner; different comparisons may use
different runners. This retains all 90 independently warmed primary trials and
12 separate diagnostic trials without comparing revisions across hosts. Each
job has a 60-minute budget. The merger rejects missing or inconsistent shards
and never pools their frame distributions. Original workloads and image checks
are preserved.

### Browser comparison

The complete candidate matrix remains a required gate. The first full matrix
at `721100083ecba804303ed738afee29bf2d7d265c` retained all 90 primary trials;
11 workload/backend shards passed, while the WebGPU edit shard failed its image
gate because capture preceded completion of the deselection UI. It is recorded
as a failed aggregate. The capture now waits for the native selection label and
stable edited artwork outside the timed interval, with the original thresholds.

The separate [same-artifact experiment](https://github.com/wieslawsoltes/Svg.Skia/actions/runs/43238627133)
at `c2b7c2dc8eb6e44a68b9851d4d5f44978b2e67ba` completed three alternating
trials per bridge for each workload, with 120 trusted inputs and no input tail
in every trial. Both sides use the same Release AOT package and SwiftShader
runner. [Audited measurements](benchmarks/unodraw-bridge-experiment-2026-11-12.json)
preserve input/callback counts, exact source, per-mode medians and separate
instrumented diagnostics.

| Workload | Metric | Persistent CPU bridge | WebGL document bridge |
| --- | --- | ---: | ---: |
| Pan | Median elapsed time, 120 inputs | 73.604 s | 76.695 s |
| Pan | Median task duration per input | 213.924 ms | 539.410 ms |
| Pan | Median active main-thread CPU, entire trial | 21.062 s | 1.472 s |
| Zoom | Median elapsed time, 120 inputs | 70.809 s | 68.954 s |
| Zoom | Median task duration per input | 193.371 ms | 124.686 ms |
| Zoom | Median active main-thread CPU, entire trial | 19.062 s | 2.179 s |

Task duration can include blocking inside a browser task and is not equivalent
to executed thread CPU. The instrumented pan document-paint total fell from
24,427 ms to 236.5 ms, while primary trial task duration increased and overall
cadence did not improve. Browser readback or synchronization outside the painter
is a plausible explanation; these metrics do not isolate the duration of each
external image copy. External copies also have no measured supplied-buffer byte
count, so smaller counted upload buffers do not establish lower total bandwidth.

The experiment passed all nine paired DPR 1 images per workload, but its separate
native-size AOT quality matrix passed only 29/30 pairs: DPR 2 actual-size text
exceeded the unchanged luminance-12/fraction-0.001 limit. This version is neither
a quality-qualified speedup nor a reason to select the GPU bridge by default.
Later glyph-preserving changes and the single-surface experiment require new
browser evidence. Software GPU measurements do not establish hardware throughput.

See the [browser benchmark instructions](../tests/UnoDraw.BrowserPerformance/README.md)
for exact workloads, metric definitions, controls and reproduction commands.

## Implementation under measurement

### Retained interaction surfaces

Full and camera redraws paint document and interaction feedback together, in the
original order, into one surface. The retained interaction child is hidden while
keeping its rendering resources. The first subsequent interaction-only change
repaints the document once to remove its embedded feedback and makes the child
visible; later feedback updates reuse that document drawing. The transition is
committed at the document paint boundary, so reentrant or canceled transitions
cannot expose duplicate or stale feedback. Startup and reload first initialize
both surfaces before allowing combined paints.

Selection-only controller notifications compare a render-content stamp instead
of invalidating the SVG. The stamp includes document/renderer/picture/model
identity, content revisions, root and viewer transforms, and bounds; the canvas
also checks its camera, grid and appearance. Including the viewer transform
preserves changes made through the renderer's public pan/zoom APIs even when
the picture, model and document revisions remain unchanged. Calls made through
the original nonvirtual
`SKCanvasElement.Invalidate()` still refresh all visible content. Seven focused
scheduling tests include 2,000 randomized invalidation batches checked against
an independent retained-content model.

Ruler pointer markers move as native analytic rectangles while retaining tick
labels. The Navigator retains its overview through selection/save-state changes,
and still redraws for document changes, animation, palette changes and resize.
Pointer-coordinate labels coalesce bursts of updates, and panning no longer
reassigns an unchanged zoom label.

Browser lifecycle checks also exposed a pre-existing camera displacement through
zero-sized layouts. After its first positive two-axis size, the viewport now
preserves the current world center across every accepted resize, including zero
width, zero height and restoration. Pan and zoom while collapsed remain part of
that current camera. Initial layout behavior and invalid-input rejection are
preserved; focused tests include 100 mixed-axis collapse/restore cycles.

### Persistent WebGPU uploads

Uno already retains unchanged canvas recordings. On a dirty canvas, the pinned
[Uno 7.0.32 software fallback](https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/AddIns/Uno.WinUI.Graphics2DSK/SKCanvasElement.cs)
paints into a retained BGRA CPU surface, then creates a new backend texture.
Its [WebGPU texture factory](https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/Uno.UI.Composition.WebGpu/WebGpuDrawingFactory.cs)
creates a managed RGBA array, swaps channels and scans opacity, allocates a GPU
texture and complete mip chain, builds lower mip arrays on the CPU, and uploads
each level. The app adapter replaces that transfer path for editor surfaces:

| Stage | Persistent path |
| --- | --- |
| SVG painting | Existing Skia painter, BGRA8888 premultiplied, full device resolution |
| Channel ordering | Native byte permutation into a reusable native RGBA buffer |
| GPU target | Uno-owned WebGPU texture, retained until size/context changes |
| Transfer | One `QueueWriteTexture` upload for a dirty surface |
| Canvas display | One texture pixel per device pixel; zoom is rerasterized by Skia |

The public island extension and
[retained WebGPU canvas target](https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/AddIns/Uno.WinUI.Graphics3DWebGpu/WebGpuCanvasElement.cs)
supply the adapter without a runtime fork or a new Skia version. It creates no GL
context and performs no GPU readback. Physical-size transport children compensate
for the pinned WebGPU canvas control's missing DPR factor. Non-translation ancestor
transforms and projections retain Uno's original mipmapped path rather than
silently changing minification quality. Zero size, unload, resize and failed
initialization release resources through the normal control/context lifetime.

SVG painting still runs on the CPU with this WebGPU compositor. Direct Skia/WebGL
continues using its existing GPU renderer. The upload optimization does not claim
to provide native WebGPU SVG rendering.

`?canvasBridge=legacy` selects the original texture path in the same candidate
artifact for quality comparison. `?renderer=skia` selects the existing Skia
provider preference. Normal startup still negotiates WebGPU, then Skia/WebGL,
then software; an individual bridge failure uses Uno's original canvas fallback.
The shared compositor's device recovery remains the host's responsibility.

### Opt-in GPU document raster experiment

The separate instrumented pan pass for application commit `7211000` recorded
121 Document callbacks totaling 41,066.5 ms, versus 44,008.8 ms of main-thread
task time. This locates the WebGPU pan hotspot inside document painting;
transfer and compositor costs remain separate. These instrumented values are
diagnostic attribution, not the uninstrumented baseline/candidate timing ratio.
The [recorded run](https://github.com/wieslawsoltes/Svg.Skia/actions/runs/43234253807)
retains the raw observations and Uno phase logs. Native stage attribution of the
same welcome scene also identifies SVG replay, especially its gradients, as the
largest cost; no gradient simplification is used.

The `canvasBridge=webgl` experiment creates one app-owned WebGL2 context for the
document viewport and reuses the existing Skia/Ganesh painter at full device
resolution. Public Emscripten context APIs and Uno's public WebGPU object bridge
connect the browser canvas to the retained composition target. Auxiliary
surfaces keep the current CPU bridge. Loss, invalid dimensions or transfer
failure fall back to that path; the experimental tests reject fallback when
attributing GPU results. The default remains unchanged until the complete
quality and repeated timing evidence supports selecting the new path.

This removes document CPU rasterization, not necessarily browser readback:
[Chromium 143 on Linux forces readback](https://github.com/chromium/chromium/blob/143.0.7499.4/third_party/blink/renderer/modules/webgpu/gpu_queue.cc#L875-L916)
inside `copyExternalImageToTexture`. Separate same-package CPU/GPU pan and zoom
trials measure the resulting tradeoff. Their report kind is distinct from the
three-revision application benchmark and cannot be merged into its coverage.

The large-text quality failure is caused by the GPU painter selecting a different
glyph representation at large device sizes. The glyph-preserving experiment
records the existing model commands into a live `SKDrawable`; each text leaf is
painted through its unchanged BGRA text picture at the live device matrix, then
composed at one image pixel per device pixel. Text shaping, font selection,
hinting, paint effects and gamma remain with the original renderer. Tight native
R-tree paint bounds size each tile, and equal phase/scale/size reuse its immutable
image. Shapes continue through the existing GPU model painter.

The optional model callback runs inside `SKSvg.Draw`'s protected draw lifetime
and viewer transform, after its original wireframe/animation handling. Callback
state is restored on success, decline or failure; the default path adds no
delegate or disposable allocation. The canvas callback replaces only the SVG
draw within the existing artboard and feedback order. Native callbacks capture
errors for the managed caller to handle before transferring the GPU image.
Unsupported document semantics and exhausted tile budgets use the original CPU
frame. These safeguards and native prototype comparisons do not replace the
browser quality or total-throughput gates.

### Single-surface cached document experiment

`?canvasBridge=composite` keeps the CPU painter and persistent upload, but stores
the exact last BGRA document pixels. Interaction-only frames restore those pixels
with a native memory copy and draw the original feedback painter over them. One
full-resolution image reaches the compositor, avoiding the second full-viewport
transparent image used by the separated interaction surface. There is no image
resampling, raster-scale reduction, gamma change or substituted SVG painter.

An exclusive public canvas-layer lease carries the original document/interaction
paint order and current content/camera stamp. Ordinary full host invalidation
still discards the cached document, including calls through the base canvas type.
Logical size, DPR and physical size changes, zero size, unload, context loss and
bridge fallback discard the retained pixels. Unload and bridge fallback also
release the painter lease. The experiment uses one extra BGRA document bitmap;
it trades a native pixel copy for the second composition layer and must be
evaluated by measured total throughput.

The actual cache/upload helper passes 72 scene/DPR/camera rows with six changing
or removed-feedback frames compared byte-for-byte in every row, plus native
premultiplied-byte, row-stride/format and resize/release cases. Browser acceptance
still requires the unchanged native-pixel matrix. A distinct same-AOT hover
experiment alternates persistent/composite trials and requires actual cache
reuse, no fallback, 120 trusted inputs, no trailing inputs, and zero Document
paints with positive Interaction paints in the separate diagnostic pass.

### Retained panel and inspector work

The same `7211000` AOT artifact's separate Skia diagnostic passes identify a
second bottleneck outside document painting. The 121-input selection sample
allocated 420,834,576 managed bytes and spent 19,498.9 ms in main-thread tasks;
its Document callback count was zero. The 120-input edit sample allocated
974,387,328 bytes and spent 22,465.6 ms in tasks, while its 82 Document callbacks
totaled 12.0 ms. These allocation deltas include diagnostic observers. Combined
Document/Interaction scopes account for less than 0.5 MB in each sample, so the
large allocation total cannot be attributed to those paint callbacks. The
[sharded run](https://github.com/wieslawsoltes/Svg.Skia/actions/runs/43234253807)
retains reset counters and Uno's internal layout phase records; browser DOM
layout counters do not measure Uno's canvas-based XAML layout.

Properties now retains its native NumberBoxes and sections across selection,
geometry and document changes. Synchronization changes model values and section
visibility without reconstructing templates. Focused input keeps its original
target until the native commit boundary. Hidden attribute forms, resource lists,
brushes, swatches, history and document summaries refresh when visible. Independent
panel stamps only record work actually performed, so tab activation, expansion,
responsive restoration and document replacement cannot consume unseen updates.

Outline and resource snapshots compare exact element identities, hierarchy,
labels, visibility and lock inputs. Geometry-only edits retain their existing
rows; structure, rename, lock, visibility and document replacement invalidate
them. Their unchanged checks allocate zero bytes in focused warmed native tests.
Canceled native tree drags explicitly discard the outline snapshot because the
control may have reordered rows before the model accepted the operation.

Hidden color/gradient editors also defer automatic reconstruction. In particular,
building a clean gradient draft serializes and parses the document, so two hidden
editors must not repeat that work on every selection notification. Pending drafts
retain their existing ownership checks; explicit commands materialize a clean
current draft when needed. This implementation requires fresh application
benchmarks: the diagnostic figures above locate the old cost and are not a
measured speedup claim for the retained-panel changes.

## Diagnostic scope

Primary timing trials collect browser observations without enabling managed
paint counters or periodic runtime logs. A separate candidate diagnostic pass
uses `?renderDiagnostics=1&perfDiagnostics=1` to record document, interaction,
ruler and Navigator callback counts/times/allocations and Uno frame-phase logs.
Those callback scopes include synchronous native Skia work. Subsequent texture
transfer, compositor submission and physical GPU/display completion are separate
costs; a completed CPU render callback is not a presented-frame measurement.

The separate [SVG document quality gate](https://github.com/wieslawsoltes/Svg.Skia/blob/master/tests/UnoDraw.BrowserRenderQuality/SVG_SCENES.md)
edits eight canonical SVG scenes through the production source dialog. It checks
gradients, filters, masks, clipping, patterns, text, images and opacity/strokes
for both rasterizers at DPR 1, 1.25 and 2. Scene-specific anchors, semantic source
readback and alpha-over-matte color checks reject blank, stale or incorrectly
premultiplied output before the unchanged native-pixel comparison is scored.
