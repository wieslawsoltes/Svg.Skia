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

## Browser persistence ordering

A separate [pinned-runtime reproduction](benchmarks/unodraw-storage-ordering-2026-11-12.json)
identifies an IDBFS flush race: a recovery rename/delete can occur after the native
local path list is captured and before its asynchronous IndexedDB enumeration
completes. The browser startup hook now enumerates remote entries first, then
captures and reads the local tree synchronously through the original helpers.
Initial hydration, native reconciliation, transaction ownership and error
propagation remain intact. This is independent of the renderer optimization and
does not attribute the older anonymous `errno: 44` quality failure.

The actual extracted native helpers and the actual source installer pass 13 paired
scenarios (26 executions) and nine installation guards. A changed helper hash is
rejected for explicit review. The new C# hook compiles against Uno 7.0.32, and its
embedded script bytes and actual project resource item match. At the storage-only
checkpoint, the complete local solution test command passed with 2,868 assertions
and 44 existing skips. Formatting and the separate application build at that
checkpoint remained blocked by the recorded named-pipe /
Uno resource task-host restrictions. Fresh published-browser qualification is
still required; these source/helper checks do not replace it.

## Results

### Direct persistent-composition follow-up (`0ffaabff`, harness `6acf6dc`)

The complete [direct comparison record](benchmarks/unodraw-composite-baseline-2026-11-12.json)
compares the exact `96e5fa9` baseline with explicit `canvasBridge=composite` in
its original `0ffaabff` AOT package. All five jobs retain three alternating pairs:
30 primary samples, five excluded diagnostics, 35 independently recomputed raw
observer/CDP metric sets and 45 native document/ruler/font pairs. Every scored
region fraction is zero above luminance 12. This does not claim byte equality.

| Workload | Baseline median | Opt-in median | Elapsed change | Renderer-thread CPU change |
| --- | ---: | ---: | ---: | ---: |
| Pan | 173.613 s | 172.730 s | -0.51% | -7.83% |
| Zoom | 143.911 s | 146.983 s | +2.13% | -22.24% |
| Selection | 55.564 s | 94.293 s | +69.70% | -77.88% |
| Brush hover | 86.106 s | 80.667 s | -6.32% | -94.30% |
| Editing | 173.362 s | 171.240 s | -1.22% | -46.99% |

Selection is slower in every matched pair and by **69.70%** in the ratio of role
medians, rejecting recovery and default promotion. Lower renderer-thread CPU
does not resolve that elapsed regression. The remaining small wall-time changes
do not establish substantial recovery. All samples and paired ordering remain
in the record; no ratios are pooled across jobs. These SwiftShader observations
do not establish hardware-GPU throughput or input-to-new-pixels latency.

The [complete composite SVG record](benchmarks/unodraw-composite-svg-quality-2026-11-12.json)
also remains unqualified: 47/48 cases, 141/144 captured native states and 69/72
accepted pairs. DPR 1 and 2 pass completely. At DPR 1.25 the candidate image
case timed out in the screenshot font-readiness step while reopening command
search for the committed-source readback. The missing readback and three states
reject those pairs; diagnostic progress imagery cannot replace them. Every
accepted scored region fraction is zero. The retained evidence has no
failure-time font/request inventory, so the timeout cause remains unconfirmed.

### Original canvas topology control

The next [reviewed source candidate](benchmarks/unodraw-single-canvas-integration-2026-11-12.json)
adds explicit `canvasBridge=single-canvas`. It removes the interaction child
before first load, bypasses the custom island and uses the original Uno uploader
with the original document and interaction painters. The newer guide-query and
workspace optimizations remain enabled. Both painter bodies, input handling and
normal renderer negotiation remain unchanged; the new control adds no pixel cache.

The mode is configured per workspace before attaching it to the window. Its
first-paint callback is guarded against unload, disposal and reentrant completion.
Actual provider selection and native images remain separate from that painter
notification. Diagnostics require positive Document and Interaction paints, while
the primary input sequence, measurement window, metrics and original native
quality thresholds remain unchanged. Existing retained modes keep their
zero-Document feedback requirement. The default remains `persistent`.

Local validation includes 13 focused lifecycle/schedule tests, compilation of
33 production C# files against the actual pinned APIs, 137 performance-contract
and 61 quality-contract tests, and 2,874 passing assertions with 44 existing skips
across eleven completed solution test projects. Required solution commands still
returned failure because of named-pipe / Uno resource task-host connection
errors. Fresh application builds and browser performance/quality results are
pending; this implementation record does not qualify promotion.

### Direct original-uploader follow-up (`0ffaabff`)

The complete [direct baseline record](benchmarks/unodraw-legacy-composite-baseline-2026-11-12.json)
compares the exact `96e5fa9` baseline with explicit `canvasBridge=legacy-composite`
in the unchanged `0ffaabff` AOT application. All five matched jobs completed:
30 primary samples, five separate diagnostics, 35 independently recomputed raw
observer/CDP metric sets and 45 accepted native document/ruler/font pairs. Every
scored regional fraction is zero above the existing luminance-12 threshold;
this does not claim byte equality.

| Workload | Baseline median | Opt-in median | Elapsed change | Renderer-thread CPU change |
| --- | ---: | ---: | ---: | ---: |
| Pan | 85.583 s | 85.545 s | −0.04% | +0.12% |
| Zoom | 104.618 s | 103.036 s | −1.51% | +7.65% |
| Selection | 66.635 s | 105.063 s | **+57.67%** | −73.00% |
| Brush hover | 135.530 s | 133.787 s | −1.29% | −87.99% |
| Editing | 145.241 s | 147.949 s | +1.86% | −49.21% |

The selection regression rejects default promotion. The remaining wall-time
changes do not establish broad recovery. Each ratio compares medians from one
runner; all samples, distributions and paired ordering remain in the record,
including baseline selection's 114.646 / 57.841 / 66.635 s spread. No trial was
discarded and no ratios are pooled across runners. Primary timings exclude the
managed diagnostics, preserve the full warmup and require all 120 inputs (121
for selection) with zero unobserved input tails.

The separate [same-candidate record](benchmarks/unodraw-legacy-composite-paired-2026-11-12.json)
compares explicit original upload with original-upload composition in that same
`0ffaabff` package. Hover takes 204.786 → 164.714 s (−19.57%); selection takes
125.129 → 102.517 s (−18.07%). Renderer-thread CPU rises 3.12% and 3.51%,
respectively. All 12 primary samples, four excluded diagnostics and 18 native
pairs qualify within this distinct scope. Both modes retain zero Document
paints and positive Interaction paints during feedback. The original-upload
candidate already includes the newer separate interaction surface; it is not
the baseline visual tree. These results favor combining those candidate surfaces,
but cannot be multiplied by the independently matched baseline ratios.

The [Welcome quality record](benchmarks/unodraw-legacy-composition-quality-2026-11-12.json)
now includes complete Interpreter and AOT results. Each execution mode retains
six cases, 60 native captures, 30 cross-mode comparisons and 12 Navigator/resize
restoration comparisons at DPR 1, 1.25 and 2. Every scored region has zero pixels
above luminance 12. Actual original-host copying and cached-pixel reuse occur
without fallback or runtime/resource errors. The AOT evidence binds the original
907-file package ZIP (artifact `13979671213`, SHA256
`7c97a858e081f36c03682d26c98d8dacfc6d4f225f455b0069f784b1b2c52ff9`).
Interpreter has source/tree/manifest provenance, but no separately retained
complete application ZIP.

The separate extended SVG run failed its runtime-error gate: 47/48 cases and
69/72 native pairs qualify across all three DPRs. The original legacy control's
DPR 2 clipping scene logged `Error synchronizing filesystem from IndexDB`
with `errno: 44`. Its three pixel comparisons also match diagnostically, but
remain rejected because matching images do not waive a runtime error. All
144 native states, every original shard file and the actual failed aggregate
are retained in the quality record. The error does not identify a file path or
synchronization direction, so its cause remains unconfirmed.

The [earlier unsharded attempt](benchmarks/unodraw-legacy-composition-svg-original-2026-11-12.json)
remains a separate failed result: 138 retained native states and 66 accepted
pairs, all with zero scored differences, but two DPR 2 cases failed before
committed-source/native-state proof. Legacy filters could not open command
search; legacy-composite gradients did not observe a completed startup frame.
Six required pairs remain unavailable/rejected. Its passing states are not
combined with another attempt to manufacture a complete matrix.

All browser timings here use SwiftShader software graphics. Callback completion
and canvas submission do not prove input-to-new-pixels latency or physical GPU
throughput. The completed direct `composite` comparison and its separate full SVG matrix
are recorded above. Both that mode and `legacy-composite` remain unqualified for
default promotion.

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
The subsequent glyph-preserving candidate `523c1ed5e7507d26288e08255b86a2e14994367c`
passes its [Interpreter browser matrix](https://github.com/wieslawsoltes/Svg.Skia/actions/runs/43253533111):
all 30 native-size comparisons pass at DPR 1/1.25/2. The previously failing DPR 2
actual-size document region now has a changed-pixel fraction of 0.0000651 under
the original luminance-12 gate, versus the unchanged 0.001 limit. Its independent
composite matrix also passes 30/30 comparisons with zero pixels above that
luminance threshold in every compared region. Its AOT composite job
[`131331896439`](https://github.com/wieslawsoltes/Svg.Skia/actions/runs/43253533111/job/131331896439)
independently passes all six cases and 30 comparisons, with actual cache reuse
and no fallback or page/resource errors. Recomputing the native PNG comparisons
finds zero pixels above luminance 12 in every region; the maximum individual RGB
channel difference is 6/255, so browser output is not claimed byte-identical.
The first AOT WebGL attempt completes five of six cases and 20 of 30 comparisons
before a DPR 2 startup screenshot times out. Its diagnostic screenshot contains
the full artwork, but does not replace the missing accepted lifecycle states.
The [single-job retry](https://github.com/wieslawsoltes/Svg.Skia/actions/runs/43253533111/job/131362727208)
uses the identical AOT package and accepts all six cases, 60 native captures and
30 comparisons. Independent PNG recomputation finds a maximum document changed
fraction of 0.000622807, below the unchanged 0.001/luminance-12 gate. Both renderer
initializations are proven, with no fallback, page or resource errors. This
resolves the missing Welcome lifecycle evidence; the eight-scene and throughput
gates remain independent. Software GPU measurements do not establish hardware
throughput.

The retained-panel candidate `a44ec76199b7dd2ab3fb13032a730353907b7d82`
provides a measured improvement for editing and selection on Skia/WebGL. Its
[matched application trials](https://github.com/wieslawsoltes/Svg.Skia/actions/runs/43244676229)
keep the regression baseline and candidate on the same runner, with three
independently warmed trials per revision and unchanged native-pixel gates.
All twelve timing shards and their aggregate pass: 90 primary trials, twelve
separate diagnostics and 108 matched baseline/candidate image pairs. The
[audited summary](benchmarks/unodraw-retained-ui-2026-11-12.json) retains source
and artifact identities, individual samples, counts, quality maxima and
diagnostic allocation/paint totals.

| Provider / workload | Baseline median elapsed | Candidate median elapsed | Input throughput change | Active main-thread CPU change |
| --- | ---: | ---: | ---: | ---: |
| Skia/WebGL edit, 120 inputs | 27.656 s | 16.406 s | +68.6% | −69.2% |
| Skia/WebGL selection, 121 inputs | 14.392 s | 11.283 s | +27.6% | −46.2% |
| WebGPU edit, 120 inputs | 82.254 s | 80.549 s | +2.1% | −47.2% |
| WebGPU selection, 121 inputs | 52.290 s | 98.694 s | −47.0% | −80.0% |

Every Skia edit candidate trial finishes before every matched baseline trial;
the same is true for Skia selection. Their image comparisons pass and no trusted
input is left without a later graphics callback. Skia pan, zoom and brush hover,
and WebGPU pan and zoom remain approximately unchanged in elapsed time. WebGPU
brush hover takes 23.8% longer despite its large CPU reduction. These WebGPU
regressions show why CPU savings alone cannot qualify an optimization: the separate
interaction surface still incurs composition costs. The single-surface and
direct Graphite experiments address those remaining costs and require their own
quality and throughput evidence.

The same recorded Skia workloads also include the themed Uno 6 reference,
`dbe9dd165f7eb63c1c6c9e6dac11091728062824`, on each comparison's runner:

| Skia workload, 120 inputs | Uno 6 reference | Uno 7 regression baseline | Retained-UI candidate |
| --- | ---: | ---: | ---: |
| Pan median elapsed | 88.574 s | 32.174 s | 31.839 s |
| Zoom median elapsed | 100.178 s | 37.949 s | 38.312 s |

These measurements do not reproduce a Skia pan/zoom slowdown against that
pre-migration reference. Most of the difference is already present in the Uno 7
baseline; it must not be attributed to this PR's retained-panel changes. They
also do not measure the older, differently structured deployed editor at
`067901c`. The native image gate above compares the regression baseline with
the candidate, while the historical reference supplies timing context.

The next full application matrix, `523c1ed5` attempt 1, independently repeats all
90 primary trials, 12 diagnostics and 108 accepted native-state comparisons.
Its same-runner WebGPU selection median still takes 62.38% longer than the
regression baseline despite 82.07% less active thread CPU. The default rendering
regression therefore persists across both complete application measurements.

The third complete default-path matrix, `d0dafea`, independently verifies all
twelve shard archives, 90 primary trials, 12 separate diagnostics and 108 native
image pairs. Its [audited follow-up](benchmarks/unodraw-primary-followup-2026-11-12.json)
retains every sample and source/package/artifact identity. It repeats the Skia
improvement and the unresolved WebGPU regression:

| Provider / workload | Baseline median elapsed | Candidate median elapsed |
| --- | ---: | ---: |
| Skia/WebGL edit, 120 inputs | 27.179 s | 16.645 s |
| Skia/WebGL selection, 121 inputs | 19.666 s | 14.956 s |
| WebGPU hover, 120 inputs | 172.653 s | 206.158 s |
| WebGPU selection, 121 inputs | 57.943 s | 111.642 s |

WebGPU selection takes 92.68% longer despite 80.20% less renderer-thread CPU;
hover takes 19.41% longer despite 96.31% less renderer-thread CPU. Each ratio
belongs to its own matched runner. All input-tail and idle-zero-graphics checks
pass. A complete passing correctness matrix does not establish an improvement
in every workload.

The fourth complete matrix, `cbfeb0c`, also retains all 90 primary trials,
12 diagnostics and 108 accepted native image pairs. Its
[source-specific audit](benchmarks/unodraw-primary-lifetime-followup-2026-11-12.json)
matches every aggregate section against an independent strict remerge of the
twelve shard archives. Skia editing takes 35.259 → 21.642 seconds and selection
16.126 → 14.172 seconds. The default WebGPU path still regresses: selection takes
123.533 → 184.080 seconds and hover 106.907 → 123.354 seconds. WebGPU pan and
zoom are within 0.4% of their matched baselines. These are further independent
same-runner comparisons, not pooled estimates or evidence for the newer
original-uploader composition path.

The later, same-AOT `523c1ed5` [cached-composition experiment](https://github.com/wieslawsoltes/Svg.Skia/actions/runs/43253533111/job/131331896015)
measures a WebGPU hover improvement with all nine paired native images accepted.
Its [audited evidence](benchmarks/unodraw-composite-2026-11-12.json) keeps all six
primary trials, two separate diagnostics and the independent 30-pair AOT
lifecycle gate. Both modes process 120 trusted pointer moves with no input tail.

| Hover metric | Persistent bridge | Cached composite bridge |
| --- | ---: | ---: |
| Median workload elapsed | 166.309 s | 129.963 s |
| Median active renderer-thread CPU | 1.204 s | 1.198 s |
| Median task elapsed per input | 16.855 ms | 17.289 ms |
| Median observed canvas submissions | 222 | 221 |
| Texture uploads in every trial | 120 | 120 |

Elapsed time falls by 21.85%, equivalent to 27.97% greater offered-input
throughput. All composite trials, 129.402–132.230 s, finish before all controls,
164.417–166.385 s. CPU time and submission counts are essentially unchanged;
both diagnostics record zero document paints and 120 interaction paints. This
measures lower elapsed time for the offered-input workload with similar observed
callback/submission counts; it does not isolate compositor cost per new recording,
GPU execution or presentation. The source has no post-window queue-drain,
screenshot or restore-duration metric, so it does not measure unfinished GPU
work or physical presentation. It also does not compare directly with the
regression baseline on this runner or establish the selection result.

The independent `d0dafea` same-AOT hover run repeats that improvement:
145.015 → 114.568 s median elapsed, or 21.0% less time and 26.6% greater
offered-input throughput. All three composite samples, 113.809–115.504 s,
finish before all three persistent samples, 144.378–145.847 s. Both modes
retain 120 inputs and uploads, with zero input tails; median observed callbacks
and canvas submissions are both 222. All nine native image pairs pass. Its
[separate follow-up evidence](benchmarks/unodraw-composite-followup-2026-11-12.json)
also records post-window capture and restoration durations. This repetition
supports the hover result without pooling runners or claiming selection or
regression-baseline recovery from it.

The same source's independent selection experiment also passes all nine image
pairs and measures 199.482 → 167.619 s median elapsed: 15.97% less time and
19.01% greater offered-input throughput. Every composite sample,
166.229–168.003 s, finishes before every persistent sample, 190.398–205.292 s.
Both modes process exactly 121 trusted inputs without a tail and have a median
124 completed callbacks and observed submissions. Thread CPU remains essentially
unchanged, 7.516 → 7.506 s. The selected-state images contain the same 14,380
changed pixels, and document/ruler restoration is exact. These samples compare
two bridge modes of the candidate, not the older regression baseline. They
therefore establish a selection improvement but cannot close the previously
measured baseline regression by combining ratios from separate runners.

The later `cbfeb0c` [paired experiment audit](benchmarks/unodraw-paired-lifetime-followup-2026-11-12.json)
repeats the existing composite improvement: hover takes 166.179 → 131.434
seconds, and selection 247.823 → 208.572 seconds. Each experiment retains six
primary trials, two separate diagnostics and nine accepted native image pairs.
Selection thread CPU is essentially unchanged, 8.990 → 8.980 seconds. As before,
these compare the candidate's two persistent-upload modes on their own runners;
they do not establish recovery against the older application baseline.

See the [browser benchmark instructions](../tests/UnoDraw.BrowserPerformance/README.md)
for exact workloads, metric definitions, controls and reproduction commands.

### Isolating the remaining transport cost

Two dedicated workflows consume the immutable `d0dafea` AOT package rather than
rebuilding an application. The original `SKCanvasElement` transport and cached
composition run through the same retained editor UI, SVG painter and WebGPU
compositor. Six independently warmed primary selection trials and two separate
diagnostic trials preserve the original 121-input workload and nine native image
pairs. Their distinct report kind cannot replace the baseline/current-source
matrix or combine ratios measured on different runners.

The [audited primary transport run](benchmarks/unodraw-pinned-transport-2026-11-12.json)
completes that comparison with the unchanged `d0dafea` application and the
`f8c8fff` harness. Median selection elapsed falls from 105.023 seconds with
`legacy` to 69.513 seconds with the existing `composite` path: 33.81% less time,
or 51.08% greater offered-input throughput. The three composite samples are
69.513, 44.650 and 91.416 seconds; all remain below the three legacy samples,
104.941, 109.206 and 105.023 seconds, but their substantial spread is retained.
Renderer-thread CPU falls from 6.293 to 4.430 seconds. Median visible-canvas
submissions increase from 124 to 127, so this result is not explained by fewer
observed submissions. All nine native image pairs pass, and both separate
diagnostics record zero Document paints with positive Interaction paints.
This comparison tests the existing persistent composite texture, not the newer
`legacy-composite` implementation, and has no direct regression-baseline arm.

The [runtime command probe](../tests/UnoDraw.BrowserPerformance/RUNTIME_DIAGNOSTICS.md)
separately compares the original baseline with the candidate's legacy, persistent
and composite modes. It explicitly appends logging flags to the served generated
configuration module, retaining original/served hashes and exact byte identity of
all application binaries, runtime files, fonts and other assets. Its modified
served configuration disqualifies it from primary timing claims. Per-frame logs
and post-window selected/cleared captures distinguish transport differences from
different sampled UI states. Every package asset is checked against its verified
original archive before and after execution.

The completed [runtime diagnostic audit](benchmarks/unodraw-runtime-diagnostics-2026-11-12.json)
verifies all four cases and independently recomputes all 23 native image pairs.
Every compared region has zero pixels above the existing luminance threshold;
each mode's selection feedback changes the same 14,380 document pixels. The
canonical post-window captures distinguish the selected and cleared recordings:

| Mode | Selected / cleared draw operations | Measured offscreen replay logs | Visible-canvas submissions |
| --- | ---: | ---: | ---: |
| Baseline original | 423 / 363 | 73 | 139 |
| Candidate legacy | 426 / 366 | 62 | 125 |
| Candidate persistent | 426 / 366 | 119 | 123 |
| Candidate composite | 425 / 365 | 119 | 123 |

The 60-operation difference therefore corresponds to the selected and cleared
UI states. It does not establish a transport penalty. The pinned runtime can
present its retained offscreen image without replaying the scene when damage
has been consumed; recording and presentation are scheduled separately. These
logs count offscreen replays, not every visible-canvas submission, and do not
prove that every intermediate input state reached the canvas. The higher replay
counts cannot be classified as redundant work from these observations alone.
Each `frame-phases` line aggregates 60 recordings and may straddle measurement
boundaries. The logging-enabled diagnostic also reverses the primary run's
legacy/composite elapsed-time ordering. Its timings remain diagnostic only;
neither ratios nor causal performance conclusions are imported from it.

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
supply this persistent adapter without requiring a runtime fork or a Skia upgrade.
The optional Graphite path below selects a newer browser Skia package. The persistent adapter creates no GL
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

### Evaluated WebGL document bridge (retired)

The separate instrumented pan pass for application commit `7211000` recorded
121 Document callbacks totaling 41,066.5 ms, versus 44,008.8 ms of main-thread
task time. This locates the WebGPU pan hotspot inside document painting;
transfer and compositor costs remain separate. These instrumented values are
diagnostic attribution, not the uninstrumented baseline/candidate timing ratio.
The [recorded run](https://github.com/wieslawsoltes/Svg.Skia/actions/runs/43234253807)
retains the raw observations and Uno phase logs. Native stage attribution of the
same welcome scene also identifies SVG replay, especially its gradients, as the
largest cost; no gradient simplification is used.

The [historical `canvasBridge=webgl` implementation](https://github.com/wieslawsoltes/Svg.Skia/blob/cbfeb0c603a8f729b4b1d489aecf99324c73c169/samples/UnoDraw/Platforms/WebAssembly/Rendering/WebGlSkiaCanvas.cs)
created one app-owned WebGL2 context for the document viewport and reused the
existing Skia/Ganesh painter at full device resolution. Public Emscripten context
APIs and Uno's public WebGPU object bridge connected that canvas to the retained
composition target. Auxiliary surfaces kept the CPU bridge. Loss, invalid
dimensions or transfer failure fell back to that path; its experiments rejected
fallback when attributing GPU results.

This removes document CPU rasterization, not necessarily browser readback:
[Chromium 143 on Linux forces readback](https://github.com/chromium/chromium/blob/143.0.7499.4/third_party/blink/renderer/modules/webgpu/gpu_queue.cc#L875-L916)
inside `copyExternalImageToTexture`. Separate same-package CPU/GPU pan and zoom
trials measure the resulting tradeoff. Their report kind is distinct from the
three-revision application benchmark and cannot be merged into its coverage.

The experiment is rejected for promotion and removed from the active application.
The [audited rejection record](benchmarks/unodraw-rejected-webgl-2026-11-12.json)
retains all 36 primary samples, 12 separate diagnostics, 54 independently
recomputed timing image pairs and the independent lifecycle/scene evidence.
Across the six audited `a44ec76`/`523c1ed5` paired pan repeats, every WebGL trial
takes longer than its matched CPU control, by 2.14%–6.87%. Zoom has no repeatable
elapsed-time improvement. Seven extended SVG comparisons also fail in both
versions with identical crop pairs. Lower executed CPU time does not meet the
combined quality and throughput requirement. Historical source, complete
measurements and failures remain available; removing the transport does not turn
its failed or incomplete matrices into passes. The normal `renderer=skia`
compositor and its WebGL/software fallback remain supported. An old
`canvasBridge=webgl` request receives an explicit retirement diagnostic and uses
the persistent CPU bridge.

The later `d0dafea` package adds modest, mixed timing results rather than a
uniform slowdown: pan median 79.9704 → 80.7652 s, and zoom median
68.3957 → 65.6327 s. One of three pan pairs and two of three zoom pairs favor the
GPU bridge. These favorable samples remain part of the evidence; they do not
establish a substantial repeatable gain or resolve the broader quality failure.
The conclusion applies to the measured transport/browser configurations, not
every GPU, browser or future Skia release.

### Native text coverage in the retired GPU experiments

The document GPU transports described here are historical implementations. Their
exclusive text/model replay helpers, native interop and dedicated tests have
been removed after the browser quality and timing results rejected them. The
unused model-painter and editor-canvas callback seams have also been removed;
the supported CPU paths continue to call the original `SKSvg.Draw` directly.
The following observations preserve the implementation rationale and failed
experiments rather than enabling a current renderer choice.

The first GPU experiment exposed a large-text quality failure caused by a
different GPU glyph representation at large device sizes. Its helper recorded
the existing model commands into a live `SKDrawable`; each text leaf was painted
through its unchanged BGRA text picture at the live device matrix, then composed
at one image pixel per device pixel. Text shaping, font selection, hinting, paint
effects and gamma remained with the original renderer. Tight native R-tree paint
bounds sized each tile, and equal phase/scale/size reused its immutable image.
Shapes continued through the existing GPU model painter.

The optional model callback ran inside `SKSvg.Draw`'s protected draw lifetime
and viewer transform, after its original wireframe/animation handling. Callback
state was restored on success, decline or failure. The canvas callback replaced
only the SVG draw within the existing artboard and feedback order. Native
callbacks captured errors before transferring the GPU image. Unsupported
document semantics and exhausted tile budgets used the original CPU frame.
These safeguards and native prototype comparisons did not replace the browser
quality or total-throughput gates.

### Direct Graphite WebGPU experiment (retired)

An old `canvasBridge=graphite` query now emits an explicit retirement diagnostic
and uses the persistent CPU bridge. The live CLI and CI matrix reject the
retired candidate. The records below describe the archived implementation and
retain its favorable samples, failures and incomplete coverage. Uno's WebGPU
compositor, the normal Skia/WebGL fallback and the browser's aligned native
package/link configuration remain supported independently.

`?canvasBridge=graphite` uses the released SkiaSharp 4.156.0 Graphite/Dawn APIs to
paint directly into Uno's WebGPU target. It borrows the original Uno instance,
device, queue and RGBA8 texture; it does not create another browser graphics
device. A retained recorder wraps that target in `SKSurface`, replays the existing
SVG model with the same glyph-preserving painter, and submits asynchronously on
the shared queue before Uno samples the texture. This removes the WebGL external
image copy and the full-document CPU raster/upload from eligible document frames.
Auxiliary canvases retain the existing persistent CPU bridge.

Only the UnoDraw browser head selects SkiaSharp 4.156.0 and the matching
HarfBuzzSharp 14.4.0.100 packages. Other heads and shared project package pins
retain their existing versions. Uno 7.0.32 and this Skia release contain the same
Dawn `v20260624.223603` C ABI. Uno adds its device/texture import exports to the
port's JavaScript, so the browser build selects Uno as the sole JavaScript port
owner and supplies the matching Skia native archive through the normal
`NativeFileReference` extension point. Loading both port copies fails with
duplicate generated ABI tables; the build must not do that.

The [dependency and native link probe](benchmarks/unodraw-graphite-dependencies-2026-11-12.json)
verifies the matching headers/generated tables and a successful standalone
Emscripten link with one native Dawn implementation. A separate old/new native
CPU comparison preserves all 81 full-resolution BGRA images byte-for-byte:
eight canonical scenes and the welcome document, three DPRs and three camera
transforms, with identical renderer assemblies and fonts. These are dependency
and native pixel checks; the real Uno browser build and GPU output remain
separate required gates.

The first actual Graphite browser job at `5d392b3` links and initializes the
shared target successfully, but its startup artwork gate fails: Skia's Graphite
device implements canvas
[`drawDrawable` as an empty operation](https://github.com/mono/skia/blob/d1717dc22ef2203b7fc9bf0e01078b37b1a4af6b/src/gpu/graphite/Device.h#L254).
The artboard
paints while the SVG drawable disappears. The corrected Graphite path replays
the same validated model commands through the existing converter and invokes
each retained text leaf's `SKDrawable.Draw` directly. It preserves active clips,
layers and the outer device/viewport/viewer matrix; the recorded Ganesh path is
unchanged. Native replay equivalence is checked separately from the mandatory
new Graphite browser captures.

The corrected `d0dafea` [Interpreter capture](https://github.com/wieslawsoltes/Svg.Skia/actions/runs/43265614141/job/131348332244)
passes all six startup/lifecycle cases, proves real Graphite initialization and
submissions, and has no fallback, validation, page or resource error. Its pixel
gate still fails: 20 of 30 comparisons pass. Nine failures are at DPR 1 and one
is the DPR 1.25 actual-size document. At DPR 1, all 1,307 pixels above luminance
12 lie on the artwork, particularly the landscape and arch edges; text, chrome
and rulers do not contribute. Uniform interior colors match. The restore from
the 80-pixel viewport is pixel-identical within each mode, so these failures
concern native coverage rather than missing redraws or stale texture uploads.
The independent [AOT gate](https://github.com/wieslawsoltes/Svg.Skia/actions/runs/43265614141/job/131366769975)
reproduces the same ten failing fractions, with all six lifecycle cases and 60
captures complete. All 54 desktop document crops, including both bridge modes,
are byte-identical between Interpreter and AOT. Whole-frame differences outside
those crops remain below luminance 12, so the AOT check isolates the same native
coverage issue rather than introducing a new execution-mode failure.

`?canvasBridge=graphite&graphiteCoverage=raster` isolates one public native
coverage choice in the same application binary. It requests
[`SKGraphiteContextOptions.InternalMultisampleCount = 1`](https://github.com/mono/SkiaSharp/blob/v4.156.0/binding/SkiaSharp/Gpu/Graphite/SKGraphiteContext.cs#L166);
an absent selector or `graphiteCoverage=default` retains the native default of
zero. The query is frozen before the first shared context, and the accepted
mode and requested count are logged only after native context creation succeeds.
Invalid or duplicate selectors cannot silently run a different experiment.

In the pinned non-Vello WASM build, Skia's
[`RendererProvider`](https://github.com/mono/skia/blob/d1717dc22ef2203b7fc9bf0e01078b37b1a4af6b/src/gpu/graphite/RendererProvider.cpp#L88)
uses its raster path atlas when MSAA is avoided. Its
[`RasterPathUtils`](https://github.com/mono/skia/blob/d1717dc22ef2203b7fc9bf0e01078b37b1a4af6b/src/gpu/graphite/RasterPathUtils.cpp#L64)
keeps antialiasing enabled and rasterizes coverage into A8; GPU shading, blend
and layer order remain native. Specialized analytic shapes and eligible clips
can still bypass that atlas. The requested count therefore does not establish
pixel equality: the same native-size Welcome and complete SVG scene gates must
accept the actual browser output before the option can be promoted.

The `cbfeb0c` Interpreter jobs provide a controlled browser check of this option.
Both publish the same source/configuration and have identical complete build
manifests, entry-page hashes, font manifests and browser arguments. Complete
application archives have not been compared byte-for-byte. The
[audited coverage evidence](benchmarks/unodraw-graphite-coverage-2026-11-12.json)
preserves every comparison, artifact digest and successful-context log:

| Welcome Interpreter gate | Native default coverage | Raster path coverage |
| --- | ---: | ---: |
| Complete lifecycle cases | 6/6 | 6/6 |
| Accepted native captures | 60 | 60 |
| Passing native image pairs | 20/30 | 30/30 |
| Maximum changed-pixel fraction in any checked region | 0.001910819 | 0.000366959 |

The original luminance-12/fraction-0.001 gate is unchanged. All 60 pair outcomes
were independently recomputed from the PNGs. Both modes initialize Graphite
with the requested coverage count, without fallback or runtime errors. Their
27 matched desktop CPU-control document crops are byte-identical; all 30
CPU-control whole frames have zero pixels above the luminance threshold. At
DPR 1, the document's above-threshold count falls from 1,307 to 251 and the long
arch-edge discrepancies disappear. Smaller analytic-shape/clip differences
remain, so this is accepted native-pixel quality, not byte equality.

The separate [AOT coverage evidence](benchmarks/unodraw-graphite-coverage-aot-2026-11-12.json)
reproduces all 60 Interpreter pass/fail outcomes and fractions: raster coverage
passes 30/30 Welcome pairs, while native default coverage passes 20/30. Both AOT
jobs download application artifact `13971214019` and verify its complete
175,806,733-byte ZIP against SHA256
`a6522fbf7abf8ac947096a02871b5d8ca7ac86e12b02d9a004ee23a2f9ab0ec3`.
This establishes complete application archive identity between the two AOT jobs;
it does not retroactively establish byte identity for the independently built
Interpreter archives. All 108 desktop document crops across both coverage
options and both bridge modes are byte-identical between execution modes. None
of the 120 whole-frame comparisons has a pixel above luminance 12. The Welcome
quality result therefore applies to both execution modes, with the same limits.

The completed [native-default SVG evidence](benchmarks/unodraw-graphite-svg-default-2026-11-12.json)
at `d0dafea` includes all 48 source/lifecycle cases, 144 native captures and 72
image pairs. Only 41 pairs pass: filters fail six, masks six, clips nine, patterns
nine, and the image scene one. Gradients, text, and opacity/strokes pass all nine
pairs each. The image-scene failure is localized to its curved semitransparent
stroke and butt caps; its sampled bitmap interior differs by at most one RGB
level. All 72 outcomes were independently recomputed, and no fallback or runtime
error explains the failures. These results reject native default coverage.

The later [paired SVG coverage audit](benchmarks/unodraw-graphite-svg-coverage-2026-11-12.json)
uses the same complete `cbfeb0c` AOT archive for both coverage modes. Native
default coverage again completes all 48 cases and reproduces all 31 failures.
The raster-coverage job reaches its 90-minute limit after 45 cases and 135
captures, leaving nine comparisons unavailable. Of its 63 available native
pairs, 62 pass and the mask scene at DPR 1.25 after fractional panning fails.
That failure affects 485 of 441,351 body pixels, a fraction of 0.001098899,
above the unchanged 0.001 limit. All but one failed pixel lie on two horizontal
edges of the masked gradient rectangle; the remaining pixel lies on an ellipse
boundary. This localization suggests fractional-edge coverage or sampling,
without establishing a native implementation cause. Neither incomplete
coverage nor a narrow edge difference qualifies the failed renderer.

Raster coverage's [first AOT pan comparison](https://github.com/wieslawsoltes/Svg.Skia/actions/runs/43272580441/job/131397550441)
passes all nine native image pairs with six primary trials and two separate
diagnostic trials. Median elapsed time nevertheless rises from 88.681 seconds
for persistent upload to 104.417 seconds for Graphite, a 17.74% regression on that
SwiftShader runner. Renderer-thread CPU time falls 90.75%, but that reduction
does not establish better total throughput. All inputs, actual native document
submissions, and separately timed queue drains are accounted for. The matching
source's separate zoom comparison also passes all nine native pairs, but takes
145.122 → 184.356 seconds, 27.03% longer, despite 89.26% less renderer-thread CPU.
The [paired audit](benchmarks/unodraw-paired-lifetime-followup-2026-11-12.json)
retains both experiments' complete samples and native checks. Together with the
paired SVG failures, these results reject promotion of direct Graphite and
support retiring that experimental document path. Uno's WebGPU compositor and
the normal Skia/WebGL fallback are independent of this opt-in renderer.

One Graphite context is shared per Uno device. Each document canvas owns its
recorder, image provider and target wrapper. Image caching is bounded, and failed
image conversion rejects the whole Graphite frame instead of silently dropping
content. Unsupported document semantics discard an unsubmitted recording and
use the existing CPU painter. An insertion or submission failure disables reuse
of the shared context because partially encoded commands must never be submitted
after a CPU fallback has overwritten the target.

Discarding a recording also clears its image-provider cache. Dawn image
conversion [queues uploads inside that recording](https://github.com/mono/skia/blob/d1717dc22ef2203b7fc9bf0e01078b37b1a4af6b/src/gpu/graphite/TextureUtils.cpp#L318-L342)
and can return a texture image
before the upload executes. Retaining such an image after discarding its upload
would reuse uninitialized pixels on the next frame. Cache invalidation keeps the
same recorder-owned provider and its sticky failure state; it does not replace
the native callback target.

Native cleanup waits asynchronously for the shared queue and then polls Skia's
completion callbacks before releasing the recorder and context. Synchronous
waiting would block the browser event loop. The pinned Dawn port does not settle
its native completion future when the JavaScript queue promise rejects; native
context destruction with outstanding work would abort. Such an unprovable
retirement disables Graphite for the session and retains only native resources
that cannot safely be destroyed, while releasing managed document/text caches.
It cannot create additional Graphite contexts until the page is reloaded.

The benchmark requires actual successful document submissions during the timed
window and zero `copyExternalImageToTexture` calls. It reports auxiliary texture
writes separately; those writes are not evidence of a full-document upload.
After timing stops, an explicit shared-queue drain and final screenshot validation
are measured separately to expose unfinished GPU work. Neither a queue submission
nor a requestAnimationFrame callback proves physical display presentation.
Those safeguards did not resolve the observed quality and elapsed-time failures.
The direct document path is retired; its diagnostic protocols remain readable
for independent validation of the archived results.

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

Synchronous SVG draw callbacks can unload or reconfigure their host before the
caller's canvas restore scopes unwind. The CPU bridge therefore owns each paint
through a generation-stamped scope: cancellation immediately rejects cache
updates, feedback and uploads, but native buffer destruction waits for scope
exit. A changed renderer or layer lease also rejects the frame. This fixes a
reproduced native crash in both persistent and composite modes without extending
the lifetime of a discarded frame's GPU submissions. Regression tests invoke the
actual `SKSvg.OnDraw` callback, including callback exceptions, repeated unload,
lease replacement and reload, while retaining the unchanged pixel-cache tests.

Those public callbacks can also retain a snapshot of the private raster surface.
A permanently retained pixmap then becomes stale after Skia performs copy-on-write;
a direct cache restore can otherwise mutate an existing snapshot. The buffer now
refreshes its reusable pixmap before each read, copy or swizzle, and calls the
public `SKCanvas.Discard` before a complete raw restore to notify Skia and detach
snapshot storage. The native regression cases reproduce both stale reads and
snapshot mutation before the fix. The shared layer painter restores its entry
save count even when a callback throws after adding another save scope.

The actual cache/upload helper passes 72 scene/DPR/camera rows with six changing
or removed-feedback frames compared byte-for-byte in every row, plus native
premultiplied-byte, row-stride/format and resize/release cases. Browser acceptance
still requires the unchanged native-pixel matrix. A distinct same-AOT hover
experiment alternates persistent/composite trials and requires actual cache
reuse, no fallback, 120 trusted inputs, no trailing inputs, and zero Document
paints with positive Interaction paints in the separate diagnostic pass.

The selection comparison uses the existing 121-toggle workload, including its
final visible selection. It keeps the same timing boundaries, real input counts,
before/after/restore pixel gates and zero-document/positive-interaction diagnostic
requirements. Its results must be evaluated independently from hover.

### Cached composition through the original texture uploader

`?canvasBridge=legacy-composite` isolates the texture transport while retaining
the same exact document cache and feedback painter. Its island owns the public
layer lease and receives full host invalidations, including calls through the
nonvirtual `SKCanvasElement.Invalidate` API. A software child deliberately does
not implement `ISkiaCanvasRenderer`; its one-time island attempt reports
`OnUnavailable` before attaching another child. Uno then uses its original
`PaintSoftware` and texture creation path, including opacity analysis, immutable
per-recording textures and mip generation. Auxiliary painters also retain the
original uploader. The application compositor remains WebGPU.

The shared painter renders into its owned BGRA surface and copies complete
premultiplied rows into the original host's already-cleared raster. The host's
actual size, uniform device matrix and clip determine cache geometry; no second
rounding calculation chooses the destination dimensions. Borrowed host surfaces
and canvases are neither retained strongly nor disposed. Callback cancellation
rejects the copy, and incompatible surfaces or transforms use the existing host
fallback. This path never reads back a GPU surface. Its unused RGBA upload buffer
is allocated only if a transport later requests it.

Initialization is logged after a completed host copy; cache reuse is logged only
after a completed copy that restored cached document pixels. The new experiment
compares `legacy` with `legacy-composite` in one AOT package, keeping six primary
trials, two separate diagnostics and all nine original native image pairs per
workload. Welcome and the complete eight-scene SVG matrix remain independent
browser gates. This opt-in implementation supplies no speedup or baseline
recovery claim until those actual measurements complete.

The `0ffaabff` [Interpreter quality audit](benchmarks/unodraw-legacy-composition-quality-2026-11-12.json)
accepts all six cases and 60 native captures at DPR 1, 1.25 and 2. Independent
recomputation accepts all 30 cross-mode pairs and 12 Navigator/resize restoration
pairs, with zero pixels above luminance 12 in every compared region. All three
candidate cases prove completed original-host copying and actual document-cache
reuse, without fallback or page/resource errors. Brush and selection feedback,
compact layout, and restoration from an 80-pixel window are included. The source,
full harness Git tree, package manifest and diagnostic archive are verified.
This early job does not retain its complete Interpreter application archive;
it is not an independently downloaded binary snapshot or a performance
measurement.

The independent AOT Welcome audit at the same `0ffaabff` source also accepts all
six cases, 60 native captures, 30 cross-mode pairs and 12 Navigator/resize
restoration pairs. Every scored region again has zero pixels above luminance
12, and every DPR proves completed original-host copying and actual cache
reuse without fallback or runtime/resource errors. Unlike the early Interpreter
job, this audit verifies the complete original AOT application ZIP, its manifest
and all 907 served assets. It binds the result to artifact `13979671213`, SHA256
`7c97a858e081f36c03682d26c98d8dacfc6d4f225f455b0069f784b1b2c52ff9`.
This completes the Welcome gate for both execution modes. The full SVG matrix
and direct baseline timing gates remain independent requirements.

A separate reusable workflow directly compares the same run's immutable
`96e5fa9` baseline package with the explicit `legacy-composite` candidate. It
starts after package publication and covers hover, selection, pan, zoom and
editing, with three alternating primary pairs and one separate candidate
diagnostic per workload. Archive-bound asset checks and candidate source
equivalence accompany the original native document/ruler/font gates. Its report
kind identifies the opt-in candidate and cannot qualify the current default.
This direct comparison is required before claiming baseline recovery; timing
ratios from different runners cannot substitute for it.

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

The strict macOS allocation gate exposed a further resource-classification
problem: `GetType().Name` can allocate again after collection, because the
[runtime reflection cache is weakly held](https://github.com/dotnet/runtime/blob/v10.0.0/src/coreclr/System.Private.CoreLib/src/System/RuntimeType.CoreCLR.cs).
The picker and snapshot now share a concrete `SvgFilter` type check. Genuine
filters and their subclasses qualify; unrelated elements with the same simple
type name do not. The regression test keeps its original warmup and zero-byte
assertion, then forces full collection and requires zero allocated bytes again.

Hidden color/gradient editors also defer automatic reconstruction. In particular,
building a clean gradient draft serializes and parses the document, so two hidden
editors must not repeat that work on every selection notification. Pending drafts
retain their existing ownership checks; explicit commands materialize a clean
current draft when needed. The diagnostic figures in this section locate the old
cost; the separate matched application measurements above establish the measured
Skia editing/selection improvements and the remaining WebGPU regression.

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

The earlier `a44ec76` WebGL scene job reached its 45-minute job limit after 38 of
48 cases. All 48 available paired comparisons at DPR 1/1.25 were recomputed:
41 pass and seven filter, mask or pattern comparisons exceed the unchanged
threshold. These failures concern non-text shape/stroke edges, not blank
documents or fallback. Reconstructed native camera probes reproduce several
differences with both Skia 4.151.1 and 4.156.0, while the shared document helper
matches the original painter byte-for-byte on the same native backend. Those
probes do not establish the exact browser clip phase or predict Graphite output.
The newer `523c1ed5` job also reaches its old 45-minute limit, after 35 of 48
cases. All seven failing legacy/WebGL crop pairs are byte-identical to their
`a44ec76` counterparts: the glyph-preserving helper does not resolve these
non-text edge differences. Its 41 passing comparisons out of 48 available pairs
are partial evidence, not a complete quality pass.
The full scene job now has a 90-minute budget to preserve all 48 cases and 72
comparisons; missing cases and above-threshold pixels remain failures.
