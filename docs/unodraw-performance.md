# UnoDraw rendering performance

UnoDraw retains the existing SVG DOM, controller history and Svg.Skia renderer.
The optimizations below remove repeated preparation and preview rendering while
preserving the document's editing and rendering contracts. Browser composition
and its fallback order are documented in [UnoDraw on Uno Platform 7](unodraw-uno7.md).

## Artboard and ruler preparation

`SvgEditorArtboardCache` stores an immutable artboard snapshot and its union for
each view. Its key includes controller and document identity, document and render
revisions, the current picture, its cull rectangle, and the root scene transform.
Selection and camera changes reuse the snapshot. Transactions, refreshed edit
previews, undo/redo, document replacement and renderer refresh invalidate the
appropriate key. Views clear borrowed references when replacing a controller or
releasing resources; the cache never owns or disposes the renderer.

The controller's public `GetArtboards()` method remains a live DOM query. Direct
DOM edits must still use the controller's transaction or `Refresh()` API to update
the displayed artwork. Cached rendering does not introduce a second document
model or change that refresh contract.

`SvgRulerTickCache` retains one bounded tick set for a specific origin, scale and
length. Pointer-only repaints reuse its ticks and major labels. A pan, zoom or
resize replaces the set. Label formatting remains culture invariant, and invalid
metrics cannot reuse a previous valid set. Canvas and ruler drawing use indexed
iteration over the retained lists to avoid interface-enumerator allocations.

### Reproducible preparation profile

The existing benchmark executable has an opt-in editor preparation profile:

```sh
dotnet run --project tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj \
  -c Release -- --profile-editor-frames \
  --profile-output artifacts/editor-frame-profile.json
```

It constructs deterministic documents, warms each path for 128 iterations, and
records seven samples of 2,048 iterations. It reports the median elapsed time and
current-thread managed allocation per iteration. Both paths consume the same
geometry and labels and must return matching checksums. Document loading occurs
before measurement. The profile is in process and uses the production methods;
it does not start a separate benchmark worker.

An illustrative Release run on .NET 10.0.14 / Debian 13 produced:

| Preparation workload | Live query, µs/iteration | Cached, µs/iteration | Live allocation, bytes/iteration | Cached allocation, bytes/iteration |
| --- | ---: | ---: | ---: | ---: |
| Implicit artboard; 10,000 artwork nodes | 68.543 | 0.359 | 568 | 0 |
| 128 named artboards; 1,000 artwork nodes | 69.507 | 1.544 | 49,992 | 0 |
| Stationary ruler; 1,200 DIP; pointer-only frames | 21.707 | 4.504 | 4,472 | 0 |

The [raw profile](performance/unodraw-frame-profile.json) records the exact
values, runtime, sample sizes and checksums. These are steady-state preparation
measurements. They exclude cache misses, document loading, SVG rasterization,
texture upload, layout and GPU composition, and must not be interpreted as
whole-frame timings or hardware GPU performance. Timing varies with the host;
zero measured steady-state managed allocation does not mean the editor as a
whole allocates nothing.

## Navigator camera updates

The Navigator overview remains an `SKCanvasElement` drawing the existing SVG
picture and artboards. Its camera indicator is now a separate native XAML
rectangle in a clipped, non-hit-test overlay. Moving or zooming the camera updates
that rectangle's bounds without invalidating the overview surface. Document,
active-artboard, palette and size changes still repaint the preview.

The indicator uses the same world-to-overview transform as the artwork, intersects
the visible world bounds with the document bounds, preserves the existing stroke
and translucent fill, and hides when the intersection is empty. The native shape
inherits the compositor's device scaling; fractional layout coordinates remain
available rather than rounding the camera position to whole DIPs.

This matters for both browser providers. Direct Skia avoids an unnecessary SVG
replay. With WebGPU composition, avoiding overview invalidation also avoids the
compatibility surface's raster draw, RGBA conversion, CPU mip generation and
texture upload. The pinned Uno runtime already retains unchanged recorded visual
resources; adding another application bitmap cache would duplicate that storage.

The native runtime harness enables the existing opt-in input trace and verifies
that real Navigator pan and zoom commands update the indicator without increasing
the overview render count. It independently recomputes the indicator geometry
from camera, artboard and preview dimensions and rejects stale bounds, nonfinite
values and deferred preview repaint. Trace formatting is guarded by
`EditorInputTrace.Enabled`, so normal operation performs no diagnostic string
construction.

## Backend choice and validation

The default browser startup order is WebGPU, Skia/WebGL2, Skia/WebGL1, then Skia
software rendering through Canvas2D. `?renderer=skia` selects direct Skia with the
same WebGL-to-software fallback. This gives SVG-heavy workloads a supported way
to compare providers on their actual devices. It is a startup preference, and
the pinned Uno runtime does not switch providers after device loss.

The browser CI matrix runs the same optimized AOT artifact in four configurations:
WebGPU, unavailable-WebGPU/WebGL fallback, unavailable-WebGPU-and-WebGL/software
fallback, and explicit Skia with WebGPU still available. Every configuration must
prove its actual renderer on startup and reload and pass the existing artwork,
camera repaint, font, theme, responsive-layout and PWA-scope checks. SwiftShader
provides deterministic software implementations of the GPU APIs in CI; these
runs establish rendering correctness rather than physical GPU throughput.

See the [browser validation guide](../tests/UnoDraw.BrowserSmoke/README.md) for
commands and diagnostic artifacts, and
[`SvgEditorRenderCacheTests`](../tests/Svg.Editor.Skia.UnitTests/SvgEditorRenderCacheTests.cs)
for cache invalidation and immutability coverage.
