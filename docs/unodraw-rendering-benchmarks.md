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

End-to-end measurements and changes are in progress. This document does not
claim a rendering improvement until the corresponding candidate measurements
and quality results are recorded.
