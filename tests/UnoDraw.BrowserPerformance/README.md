# Published UnoDraw browser performance

This suite compares complete published Release `InterpreterAndAOT` applications
through real browser input. It complements the existing
[browser correctness matrix](../UnoDraw.BrowserSmoke/README.md); it does not
replace that matrix, reduce its pixel thresholds or report the editor preparation
microprofile as a whole-application speedup.

## The three default revisions

| Role | Revision | Purpose |
| --- | --- | --- |
| Reference | `dbe9dd165f7eb63c1c6c9e6dac11091728062824` | Direct parent of the Uno 7 migration `905813a8c47e501091d29dd1d625a41a8c638a6a`; already includes EditorTheme and font preloading. |
| Baseline | `96e5fa9d84b694e34a626b8867f2466fc231b6a7` | Current Uno 7 regression baseline, including the previous preparation caches. |
| Candidate | PR head or the selected workflow revision | The proposed performance changes. |

The older deployed master `067901c6396ca4a9cca2b335424c961a86f3cdf9`
is a useful historical reference, but its interface differs. Selecting it as the
manual workflow's `reference_ref` measures the combined interface/runtime change.
The default `dbe9dd1` comparison isolates the Uno migration more closely. A
historical package must still contain the same welcome SVG and use the same .NET
SDK, workload set and AOT build settings; an incompatible fixture is rejected explicitly.

Uno 6 is measured on its actual Skia/WebGL renderer. Uno 7 is measured with both
WebGPU and `?renderer=skia`. An unavailable legacy WebGPU implementation is never
represented by a fabricated zero or by a different renderer.

## Workloads and controls

Every independent trial gets a fresh browser process, context and application
startup. A common untimed warmup sends at least 120 real middle-button pan moves
and observes at least 60 successfully completed graphics-producing callbacks.
If input coalesces, further batches of 60 real moves are allowed up to a total of
600; failure to produce enough actual callbacks is an error. This moves the
shared runtime's retention transition outside the measured window: both
[Uno 6](https://github.com/unoplatform/uno/blob/5f783231086f7cfd2200d10388664e05f4792181/src/Uno.UI.Composition/Composition/Visual.skia.cs)
and [Uno 7](https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/Uno.UI.Composition/Composition/Visual.skia.cs)
default eligible stable subtree retention to 50 paint frames and 100 visuals.
The report records offered moves, trusted events, completed callbacks and warmup
duration separately; those samples never enter the primary metrics.

The workload's own input path is then warmed, and camera, artwork and selection
are restored. Rulers retain the last pointer position after pointer exit, so both
baseline capture and restoration use the same real pointer movement through
(450, 430) before parking at (760, 16), in CSS pixels. This matches the pointer
state as well as the camera without excluding ruler pixels. Brush hover also
warms its post-fit combined-to-overlay transition: the actual preview must appear
and then clear in a local document crop before `before.png` is saved. The steady
diagnostic assertion remains zero Document callbacks and positive Interaction
callbacks; a transition repaint is never silently accepted inside that interval.

The benchmark uses the unchanged
embedded `Welcome.svg`, a 1440 × 960 viewport, DPR 1, dark mode, `en-US`, UTC,
and the normal motion setting. No application invalidation is injected.

| Workload | Actual input | Completion and quality evidence |
| --- | --- | --- |
| Idle | A separate two-second quiet window | Stable artwork; distinguish observed rAF callbacks from actual background graphics work. |
| Brush hover | Select Blob Brush with Shift+B, then move the pointer without pressing a button | A visible moving preview; the separate managed diagnostic pass requires zero Document paint callbacks and positive Interaction paint callbacks after setup. |
| Pan | Middle-button drag along a fixed smooth path | Changed interiors in all three artwork color families, followed by fit-to-artboard restoration. |
| Zoom | Wheel events along a bounded zoom path | Changed interiors in all three artwork color families, followed by camera restoration. |
| Selection | Alternate Ctrl+A and Ctrl+Shift+A | End with a visible selection, then restore the unselected document. |
| Edit | Select all and send one right-arrow nudge per operation | Changed artwork interiors after removing outlines; undo every edit and restore the original document. |

Active input is offered once per animation opportunity. The benchmark does not
sleep between inputs to improve a reported throughput. A slower rendering loop
takes longer to complete the same input sequence. Offered operation counts and
elapsed time are recorded so this interaction workload is not confused with an
unlimited input stress test. The idle quiet window is never pooled with active
workload throughput.

Screenshots and renderer capability probes run outside measured intervals.
Startup waits for observed artwork, rulers and fonts to settle rather than using
a fixed startup delay. Artwork restoration reuses the existing 0.1% changed-pixel
limit above a luminance difference of 12/255. Pan, zoom and edit reuse the existing
requirement for 500 changed interior pixels in every artwork color family after
7 × 7 erosion. Current-versus-candidate before/after/restored images must also
match at the original restoration threshold. Legacy glyph output is not used as
a substitute Uno 7 pixel baseline. The matched current/candidate states also
compare each ruler strip at the same 0.1% limit and the existing static chrome
font regions at their original 2% limit, including the ruler pointer at its
matched position.

The standard welcome file covers the normal editor workload, gradients, clipped
groups, text and the interface. It does not represent a thousands-of-elements
document. A claim about a larger document additionally needs an equivalent
browser run with that document; the separate large-document algorithm profile
cannot establish whole-editor frame time.

## What the measurements mean

The injected observer records work already requested by the application. It
does not subscribe to `CompositionTarget.Rendering`, because that subscription
would cause continuous application frames and corrupt idle observations.

| Measurement | Interpretation |
| --- | --- |
| rAF interval p50/p95/p99/max | Browser animation scheduling cadence. It is **not rendered FPS**. |
| Completed render callback count and duration | Successful application rAF callbacks that actually issued drawing or a submission to the visible main canvas. Both pinned runtimes synchronously call managed `RenderFrame` inside that callback. This covers its synchronous replay/submission, but **excludes earlier NativeDispatcher recording**, later GPU execution and physical presentation. |
| Input to completed render callback | Trusted input dispatch to the next completed callback containing graphics work. Multiple events may coalesce, and that callback can replay an older recording. This is **not proven input-to-new-pixels latency**, per-event causality or input-to-photon latency. |
| Graphics-active animation intervals | Observer intervals containing main-canvas drawing/submission calls. Detached capability probes cannot satisfy the count. |
| WebGPU canvas submissions / WebGL flushes | Observed API calls. A WebGPU submission must follow acquisition of the application's canvas texture. Neither count is labeled as display presentation. |
| Drawing and texture-upload calls | Browser API counters. `supplied_upload_buffer_bytes` sums the sizes of supplied buffer views; it is **not** the actual consumed transfer extent because offsets, row pitch and subregions can consume less. Calls without a buffer size are counted separately. |
| CDP task/script/layout/style duration | Elapsed duration on the renderer thread with the explicitly selected `timeTicks` domain, including time blocked inside browser calls and a small amount of observer/automation overhead. Task time is not the sum of the other fields and is **not active CPU time**. |
| CDP `ThreadTime` delta | Active CPU time on the renderer thread, using operating-system thread ticks. It excludes time while the thread is descheduled, including blocking waits, and excludes CPU work on GPU/worker threads or other processes. |
| JavaScript used-heap delta | Signed difference in used JS heap; GC can make it negative. This is neither an allocation count nor the managed WASM heap. |

The historical JSON keys `cpu_ms` and `cpu_task_ms_per_operation` retain their
original elapsed-duration values for compatibility. New `thread_cpu_ms` and
`thread_cpu_ms_per_operation` fields report the separate `ThreadTime` delta in
milliseconds. They are `null` if either counter snapshot is unavailable; the
per-operation value is also `null` for an idle workload. A measured zero remains
zero. Thread CPU comparison rows require every matched trial to provide that
counter, so incomplete data cannot silently become zero or an unmatched subset.
These distinctions follow the pinned Chromium 143
[performance agent](https://github.com/chromium/chromium/blob/143.0.7499.4/third_party/blink/renderer/core/inspector/inspector_performance_agent.cc)
and [thread clock definitions](https://github.com/chromium/chromium/blob/143.0.7499.4/base/time/time.h).

The report retains raw timestamp arrays, actual renderer evidence, browser
version/launch arguments, adapter/context details, every trial, .NET and Uno SDK
versions, exact workload set, original and effective `global.json` with their
byte hashes, source SHA, fixture hash and packaged font hashes. Native font asset
hashes are recorded, not blindly required to be identical across Uno 6 and Uno 7.
Effective browser font families/status are also recorded after startup.

Physical presented-frame counts and managed allocated bytes are `null` when not
measured. Missing data is not reported as zero. Synchronous render callback completion
does not prove scanout completion. Observer API wrappers add some overhead;
compare the same harness/browser/workload, retain the raw distributions, and
corroborate hardware conclusions with a physical-GPU run.

With `--diagnostics`, a **separate candidate-only pass** enables
`?renderDiagnostics=1&perfDiagnostics=1` after the primary comparison is complete.
It snapshots the application's opt-in Document/Interaction/Ruler/Navigator paint
callback counts, total/max elapsed duration and current-thread allocated bytes.
The callback stopwatch can include blocking calls; it does not measure active
thread CPU time. It also
retains Uno's `[frame-phases]` and `[webgpu-frame]` diagnostics when enough frames
have occurred for the runtime to print its periodic sample. Their periodic windows
can straddle warmup/workload boundaries; they are supporting diagnostics, not
exactly reset per-workload timing samples. These counters do not
subscribe to frame events or invalidate visuals. Main-thread managed allocation
differences include diagnostic serialization overhead; per-paint-callback
allocation counts have their narrower scope recorded separately. Logs and timed
diagnostic scopes perturb execution, so their samples are never merged into the
uninstrumented baseline/candidate ratios. A historical candidate without the
export is explicitly reported as unavailable.

Comparisons use the median of each trial's p95, task wall duration, active thread
CPU or throughput metric, preserving
the individual trial values. Frames from different trials, renderers or workloads
are not pooled. Runtime and quality failures fail CI. Timing ratios are reported
without a universal pass/fail threshold because shared-runner noise is not an
application contract and a single fast sample is not evidence of a speedup.

## CI

`UnoDraw performance` runs for relevant pull-request changes and supports manual
dispatch with custom baseline/candidate/reference refs. Leave `reference_ref`
empty in a manual run to omit the historical build. The three AOT builds run in
parallel. Every checkout uses **SDK 10.0.403 and workload set 10.0.403**.
`toolchain.py --pin` applies an explicit build-only `global.json` override:
`sdk.version`, `sdk.workloadVersion`, `sdk.rollForward: disable` and
`sdk.allowPrerelease: false`. It preserves each revision's `Uno.Sdk` and every
other configuration property. `actions/setup-dotnet` reads this effective file;
the workload install uses its exact set, and actual SDK/workload versions are
verified before cache lookup. The package manifest distinguishes the committed
source configuration from this override. This matters because `latestMinor`
allowed the first control run's runners to choose SDK 10.0.403 and 10.0.404;
the consistency check rejected that run before any browser measurement.

Microsoft documents the pin in its
[workload-set guide](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-sets);
the [10.0.403 set](https://www.nuget.org/packages/Microsoft.NET.Workloads.10.0.400/10.403.0)
is an exact published version. `--skip-manifest-update` alone is insufficient
because it can retain different preinstalled manifests on different runners.

Published packages are cached by exact source SHA, .NET SDK, workload
version, platform and build flags. A new candidate can reuse the expensive
unchanged baseline packages. A newer push does not cancel a useful run in flight.
Old packages whose manifests lack a recorded workload identity are not relabeled
as pinned builds; the exact workload-set cache key causes a one-time rebuild.
Build jobs for the same ref queue behind one another with `queue: max`, then
reuse the completed package cache. This also preserves pending builds instead of
replacing them; GitHub documents the queue behavior in its
[concurrency guide](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency).

CI divides work into twelve workload/backend jobs. **Every matched comparison
keeps all its revisions and all three repeats on one runner.** Package order
still rotates between trials. WebGPU jobs contain six primary trials; Skia jobs
contain nine when the Uno 6 reference is present. Every job also runs its
candidate diagnostic separately. Across the default matrix, all 90 primary
trials and 12 diagnostic trials are retained.

Different workload/backend pairs can run on different machines. Their absolute
times are not paired cross-backend or cross-workload measurements. Report ratios
within each matched job, retain its environment and trial variability, and do
not pool frame distributions across jobs. The merger requires all twelve shards,
consistent packages/settings/browser, complete trial plans and all matched pixel
checks; partial results cannot pass. Results retain raw observations, console
diagnostics, package provenance and screenshots. The normal browser-smoke workflow
remains an independent visual/runtime gate.

A separate `bridge-quality` job compares the candidate's default persistent
WebGPU bitmap bridge with the same candidate's `?canvasBridge=legacy` path at
DPR 1, 1.25 and 2. It requires explicit successful persistent upload evidence,
rejects fallback, and proves exclusive legacy selection in the control case.
Both cases exercise actual-size/refit camera commands, brush and selection
overlays, Navigator hide/show, compact resizing, an 80 × 80 window and restoration.
The original pixel tolerance is applied to **unresized device-pixel PNGs**, both
over the whole frame and separately over document, rulers and visible Navigator.
Screenshots must have the native pixel dimensions expected for their DPR. CSS
screenshots serve semantic/readiness checks only; they are never used to establish
the quantitative high-DPI A/B result. Unit checks explicitly reject both smaller
screenshots and same-size images that lost detail through downsampling.
Navigator readiness still requires at least 25 pixels from each fixture color
family. Green detection matches the welcome hills/ground and their expected
blue viewport tint, within two channel levels. This prevents the actual tinted
green interiors (for example RGB 115,161,156) from being rejected by a generic
hue inequality. The raw native-pixel comparisons retain their original tolerance.
All six bridge/DPR cases run independently, including after an earlier failure.
Every completed matching state is compared; missing states and pixel differences
are recorded explicitly while later comparisons continue. The overall gate still
requires every case, every required state and every comparison to pass. Partial
artifacts provide diagnostic evidence and cannot produce a successful result.

An additional `bridge-quality-interpreter` job starts as soon as the candidate
ref is selected, without waiting for AOT compilation. It publishes a separate
Release **Interpreter** package using the same pinned SDK/workload set, then
runs the identical bridge-quality script and pixel tolerances. Its manifest and
report explicitly record `execution_mode: Interpreter`; only its correctness
reports and screenshots are uploaded. This early feedback is separate from both
the final AOT quality gate and AOT performance measurements. Interpreter packages
cannot pass the performance harness's execution-mode validation.

The [old/new high-DPI check](../UnoDraw.BrowserRenderQuality/README.md) verifies
ruler UI changes that a same-candidate bridge A/B cannot establish.
The main performance workflow builds matching baseline and candidate Interpreter
packages for early correctness feedback, then runs the same comparison again on
the final AOT packages. Both modes require identical execution modes within the
pair, and retain the native-pixel matrix and tolerances. The Interpreter run
uploads only correctness reports and images; its timings are not performance
evidence.

The `UnoDraw rendering verification` workflow consumes existing AOT artifacts
from an explicitly selected performance run without rebuilding the application.
Its parallel suites rerun the complete persistent/legacy bridge matrix,
compare the old/new application at DPR 1.25 and 2 on both backends, and replay
the full performance matrix in the same twelve matched jobs. The replay includes
all three packages, 90 primary trials and 12 separate diagnostic trials. Each
matched performance job has a 60-minute budget; each native-quality suite has
25 minutes. A separate candidate-only pan diagnostic on each backend provides
paint-phase feedback sooner and is explicitly excluded from performance claims.
Workload counts, warmup, metrics and image thresholds are unchanged.
The candidate's recorded source
commit is compared with the harness/PR head: only changes under `tests/`, `docs/`
and `.github/` are allowed. Any production-source difference makes the cached
binary ineligible. On a pull request, the qualification job explicitly records
that a fresh pipeline is required and skips cached quality, timing and diagnostic
jobs. This source audit does not claim a quality or performance pass. Manual
dispatch with stale artifacts still fails; invalid manifests or missing history
always fail. Eligible workers each repeat the strict source audit. Reports
retain both SHAs and the changed paths. The workflow's PR path
trigger is limited to its own file and `UnoDraw.BrowserRenderQuality`; manual
dispatch selects a different immutable package run when needed. Its initial
default identifies the pinned packages for this investigation; expired or stale
artifacts require a new explicitly selected AOT run.

CI uses the same pinned Playwright Chromium and SwiftShader switches as the
correctness matrix. These exercise WebGPU and WebGL APIs through software GPU
implementations. They measure software graphics and application CPU behavior,
**not physical GPU throughput**. A genuine WebGPU adapter and the expected Uno
provider must be observed; merely exposing `navigator.gpu` cannot pass.

### Same-artifact document rasterizer experiment

`bridge_experiment.py` compares each supported candidate with its declared
control in **one identical AOT package**: `persistent` versus `graphite` for
pan/zoom, `persistent` versus `composite` for brush hover/selection, or `legacy`
versus `legacy-composite` for brush hover/selection. Each job keeps all three repetitions of
both modes on one runner, alternates their order, starts a fresh browser each
time, and reuses the full 120-input warmup and existing workload. Two separate
instrumented passes identify paint costs. Every primary pair compares
before/after/restored native images; all trusted inputs and zero unobserved input
tails are required. Selection retains its existing 121-command driver; the other
active experiments require 120 operations. Reports identify the source, browser,
selected bridge and individual trials. They use a separate
`same_candidate_bridge_experiment` report kind, except for the distinct
`same_candidate_legacy_composition_experiment` described below. Neither report
kind can satisfy the twelve-shard application comparison. The executable default
candidate is `graphite`.

The former `canvasBridge=webgl` document transport is retired from production and
the live CLI/workflow matrix. Its same-artifact pan medians did not improve,
zoom gains were small and varied between source snapshots, and seven broader SVG
comparisons remained outside the original quality gate. The measured active
main-thread CPU savings did not satisfy the requested interaction and quality
goals. The [investigation and historical evidence](../../docs/unodraw-rendering-benchmarks.md)
retain those results, source identities and original artifacts. Passive validators
and contamination checks still understand archived WebGL reports; executable
queries reject the retired option. Normal `renderer=skia` compositor/fallback
measurements and their WebGL API observers remain part of the full primary matrix.

The observer counts `copyExternalImageToTexture` separately from `writeTexture`.
Neither copy calls nor offscreen GL draws count as visible-canvas presentation,
and no exact transfer byte count is inferred. Chromium 143 on Linux explicitly
[uses readback for canvas-to-WebGPU copies](https://github.com/chromium/chromium/blob/143.0.7499.4/third_party/blink/renderer/modules/webgpu/gpu_queue.cc#L875-L916),
so the historical transport is not described as zero-copy or all-GPU transfer.
The surviving direct Graphite experiment requires zero external-image copies.

### Same-artifact cached document composition

`?canvasBridge=composite` restores full-resolution cached BGRA document pixels,
paints the unchanged interaction feedback, and uploads one combined canvas
texture. Its two independent workloads compare `persistent` versus `composite`
for brush hover and selection. Each workload retains three alternating pairs,
two separate diagnostic passes and the existing native before/after/restored
image comparisons in one AOT package.

Brush hover requires all 120 offered moves and trusted inputs. Selection uses
the existing driver unchanged: 120 configured steps become **121 real alternating
Ctrl+A / Ctrl+Shift+A commands**, ending with a visible selection. Both its
reported operations and trusted input count must be 121. The original proof
requires at least 50 changed document pixels, followed by restoration of the
document and rulers within the original 0.001 changed-pixel fraction. Missing or
stale readiness/restore evidence fails the experiment; no timing boundary or
pixel tolerance is changed. Both workloads require zero unobserved input tails,
actual cached-document reuse and no fallback. Each mode's diagnostic pass must
record **Document=0 and Interaction>0** after the existing setup and warmup.

```sh
python tests/UnoDraw.BrowserPerformance/bridge_experiment.py \
  --package /absolute/path/packages/candidate \
  --output /absolute/path/composite-hover-experiment \
  --candidate-bridge composite --workload brush-hover --repeats 3
python tests/UnoDraw.BrowserPerformance/bridge_experiment.py \
  --package /absolute/path/packages/candidate \
  --output /absolute/path/composite-selection-experiment \
  --candidate-bridge composite --workload selection --repeats 3
```

### Cached composition through the original uploader

`?canvasBridge=legacy-composite` combines the same cached BGRA document and
feedback through Uno's original `SKCanvasElement` software uploader. The control
is `legacy`, so both modes retain the original texture creation, opacity scan
and mip handling. The new path must prove a completed copy into the original
host and a completed copy that actually reused cached document pixels. A
preference message alone cannot qualify it; another initialized transport or
fallback fails the trial.

This distinct experiment keeps all six alternating primary trials, two separate
diagnostics, full common warmup, 120 hover inputs or 121 selection inputs, zero
input tails, and all nine original native image comparisons. Diagnostics still
require Document=0 and Interaction>0 in both modes. It records the exact harness
commit separately from the application source and reports `candidate_over_legacy`
ratios. Results compare these two modes within the candidate; recovery against
the older regression baseline requires its own direct comparison.

```sh
python tests/UnoDraw.BrowserPerformance/bridge_experiment.py \
  --package /absolute/path/packages/candidate \
  --output /absolute/path/legacy-composite-selection-experiment \
  --candidate-bridge legacy-composite --workload selection --repeats 3 \
  --harness-sha "$(git rev-parse HEAD)"
```

Use `--workload brush-hover` and a separate output directory for the second
workload. The unchanged Welcome gate runs independently in Interpreter and AOT;
the [SVG source/camera matrix](../UnoDraw.BrowserRenderQuality/SVG_SCENES.md)
retains all 48 cases and 72 native comparisons.

The performance workflow also starts a separate direct baseline comparison after
its AOT packages are available. It compares the exact `96e5fa9` baseline with
the explicit `legacy-composite` candidate for hover, selection, pan, zoom and
editing. Each workload keeps three alternating primary pairs on one runner and
one separate candidate diagnostic pass; the older baseline has no managed paint
counters. The original warmup, input, document, ruler and chrome-font gates are
unchanged. Camera and editing diagnostics require document painting; they do not
claim interaction-only cache reuse.

`legacy_baseline_packages.py` verifies fresh GitHub artifact descriptors, complete
archive digests and every served file before and after measurement. The driver
also verifies candidate production-source equivalence to the exact harness
commit. These distinct opt-in reports cannot enter the default-application
aggregate. They establish any recovery directly against the older source,
without multiplying ratios from different runners or rebuilding the packages.

### Direct Graphite document experiment

`?canvasBridge=graphite` is a separate opt-in path that records Skia document
commands into the Uno render texture on the shared WebGPU queue. Its
[browser targets](../../samples/UnoDraw/Platforms/WebAssembly/Rendering/UnoDraw.Graphite.targets)
align the managed/native Skia packages and retain one Dawn JavaScript owner.
The default remains the persistent CPU bridge. The same-package comparison
selects `persistent` versus `graphite`; each pan or zoom job still runs three
alternating pairs, two separate diagnostic passes, all 120 inputs, and the
unchanged before/after/restored pixel checks.

Every Graphite trial requires its preference and validated initialization logs,
no fallback, and a positive `graphite_document_submissions` delta during the
measurement window. The application increments this counter only after a real
document recording is accepted and its Graphite context submits successfully.
The observer reads it only at the existing start/stop boundaries. Missing data
remains `null`; a missing, reset or zero counter cannot prove this path ran.
External-image copy calls must be zero. Aggregate `writeTexture` calls can still
come from auxiliary CPU surfaces or resource uploads and are not labeled as
full-document transfers. Document submissions, visible-canvas submissions and
physical presentation remain distinct concepts.

```sh
python tests/UnoDraw.BrowserPerformance/bridge_quality.py \
  --package /absolute/path/packages/candidate \
  --output /absolute/path/graphite-quality --candidate-bridge graphite
python tests/UnoDraw.BrowserPerformance/bridge_experiment.py \
  --package /absolute/path/packages/candidate \
  --output /absolute/path/graphite-pan-experiment \
  --candidate-bridge graphite --workload pan --repeats 3
```

Run `--workload zoom` as its own matched job. The selected GPU painter also runs
the independent [eight-scene native SVG gate](../UnoDraw.BrowserRenderQuality/SVG_SCENES.md).

The optional `--graphite-coverage default` or `--graphite-coverage raster` selects
the Graphite candidate's native coverage strategy in each of these drivers.
The corresponding query is `graphiteCoverage=default` or
`graphiteCoverage=raster`, and the native context's `InternalMultisampleCount`
is respectively 0 or 1. This option does not disable paint antialiasing or
change any screenshot, fixture or comparison threshold. It applies only to the
Graphite side; the same-package legacy or persistent control receives no
coverage selector. For example:

```sh
python tests/UnoDraw.BrowserPerformance/bridge_quality.py \
  --package /absolute/path/packages/candidate \
  --output /absolute/path/graphite-raster-quality \
  --candidate-bridge graphite --graphite-coverage raster
python tests/UnoDraw.BrowserPerformance/bridge_experiment.py \
  --package /absolute/path/packages/candidate \
  --output /absolute/path/graphite-raster-pan-experiment \
  --candidate-bridge graphite --graphite-coverage raster --workload pan --repeats 3
```

An explicit option requires the matching `UnoDraw Graphite coverage initialized`
message emitted after native context creation; a missing, different or mixed
context option fails. Each report and candidate case/trial records the requested
`graphite_coverage`. Controls record `null`. Omitting the option preserves
compatibility with artifacts predating this selector and makes no explicit
coverage claim. Each coverage variant has its own complete 6-case/30-pair
Welcome gate, 48-case/72-pair SVG gate and separate 6-primary/2-diagnostic timing
experiment. Coverage variants are never combined into one trial set or used to
satisfy each other's quality requirements.
The `composite` experiment retains its separate brush-hover and selection scopes;
both require real cached-document reuse and zero steady-state document paints.
These experiments never replace the complete baseline/candidate matrix.

### Post-window backlog diagnostics

The primary clock still stops after the same two natural rAF opportunities.
That is not a GPU completion barrier. After primary CDP and managed snapshots,
an explicit Graphite trial must expose `__unoDrawGraphiteDrain()`. The harness
awaits its asynchronous shared-queue `onSubmittedWorkDone` operation, with a
30-second diagnostic timeout and no forced rendering or synchronous flush.
`graphite_queue_drain.result` retains completion, queue count, errors and the
in-browser `duration_ms`; `request_elapsed_ms_including_automation` also includes
the request overhead. Missing or unsuccessful drain evidence fails an explicit
Graphite trial. Other bridge modes record this diagnostic as unavailable.

Each trial separately records `quality.final_capture_validation_elapsed_ms` and
`quality.restore_validation_elapsed_ms`. The first includes final screenshot
readiness and native-image validation; the second includes real restore commands
and their screenshot validation. Both include automation and image analysis.
Graphite drains first, so its later screenshot starts from a different queue
state. Reports list these diagnostics individually and calculate no cross-mode
ratios from them. They help expose a large post-window backlog and do not measure
active renderer-thread CPU, per-input causality, or physical presentation.

## Local reproduction

Install SDK 10.0.403, then publish each revision in its own checkout using the
harness revision's pin script. Run the install and publish commands inside that
checkout; the script changes only its SDK/workload selection and refuses
unrelated local `global.json` edits:

```sh
python /absolute/path/harness/tests/UnoDraw.BrowserPerformance/toolchain.py \
  --source /absolute/path/baseline-checkout --pin
cd /absolute/path/baseline-checkout
dotnet workload install wasm-tools
python /absolute/path/harness/tests/UnoDraw.BrowserPerformance/toolchain.py --source .
BINARYEN_CORES=1 dotnet publish samples/UnoDraw/UnoDraw.csproj \
  -f net10.0-browserwasm -p:UnoDrawTargetFrameworks=net10.0-browserwasm \
  -c Release -p:WasmShellMonoRuntimeExecutionMode=InterpreterAndAOT \
  -m:1 -nr:false -p:BuildInParallel=false -p:UseSharedCompilation=false \
  -o /absolute/path/packages/baseline
```

Run the harness revision's manifest tool for each package, selecting `baseline`,
`candidate` or optional `reference` as its role:

```sh
python tests/UnoDraw.BrowserPerformance/package_manifest.py \
  --source /absolute/path/baseline-checkout \
  --package /absolute/path/packages/baseline --role baseline
python -m pip install -r tests/UnoDraw.BrowserSmoke/requirements.txt
python -m playwright install --with-deps chromium
python -m unittest discover -s tests/UnoDraw.BrowserPerformance -p 'test_*.py'
node --test tests/UnoDraw.BrowserPerformance/test_instrumentation.mjs
python tests/UnoDraw.BrowserPerformance/performance.py \
  --packages /absolute/path/packages --output /absolute/path/results \
  --repeats 3 --steps 120 --graphics swiftshader --diagnostics
python tests/UnoDraw.BrowserPerformance/bridge_quality.py \
  --package /absolute/path/packages/candidate \
  --output /absolute/path/bridge-quality
```

The local command without filters executes the complete matrix sequentially.
For one complete matched shard, add `--backend webgpu --workload pan` (or the
other backend/workload names). Preserve all repeats and diagnostics. Combine
the twelve output directories with:

```sh
python tests/UnoDraw.BrowserPerformance/merge_shards.py \
  --shards /absolute/path/shard-artifacts --output /absolute/path/merged \
  --repeats 3 --require-reference
```

`diagnose.py --packages ... --output ... --backend webgpu --workload pan`
executes only the candidate diagnostic, with the same warmup and input count.
Its report is typed separately and cannot satisfy any primary comparison shard.

For a physical-device comparison use `--graphics hardware` and, if needed,
`--executable /path/to/chromium`. This removes the forced SwiftShader switches;
inspect the recorded actual WebGPU adapter and WebGL renderer before interpreting
the result as hardware performance. Use the same machine, browser, power state,
viewport, DPR, artifacts and trial ordering for both revisions. A local browser
that cannot start because of a host restriction is a blocked measurement, not a
zero-duration sample or justification for weakening the browser sandbox.
