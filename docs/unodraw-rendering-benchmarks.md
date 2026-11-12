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

The artifact-consuming verification workflow also replays the complete timing
matrix in twelve workload/backend jobs. Every matched comparison keeps all
revisions and all three repeats on one runner; different comparisons may use
different runners. This retains all 90 independently warmed primary trials and
12 separate diagnostic trials without comparing revisions across hosts. Each
job has a 60-minute budget. The merger rejects missing or inconsistent shards
and never pools their frame distributions. Original workloads and image checks
are preserved.

### Browser comparison

End-to-end measurements are in progress. No frame-rate improvement is claimed
until the corresponding candidate measurements and browser quality results are
recorded. See the [browser benchmark instructions](../tests/UnoDraw.BrowserPerformance/README.md)
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

Ruler pointer markers move as native composition shapes while retaining tick
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

## Diagnostic scope

Primary timing trials collect browser observations without enabling managed
paint counters or periodic runtime logs. A separate candidate diagnostic pass
uses `?renderDiagnostics=1&perfDiagnostics=1` to record document, interaction,
ruler and Navigator callback counts/times/allocations and Uno frame-phase logs.
Those callback scopes include synchronous native Skia work. Subsequent texture
transfer, compositor submission and physical GPU/display completion are separate
costs; a completed CPU render callback is not a presented-frame measurement.
