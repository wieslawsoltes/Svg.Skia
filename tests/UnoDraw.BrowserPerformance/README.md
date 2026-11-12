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
| CDP task/script/layout/style time | Main-thread CPU time, including a small amount of observer and automation overhead. Task time is not the sum of the other fields. |
| JavaScript used-heap delta | Signed difference in used JS heap; GC can make it negative. This is neither an allocation count nor the managed WASM heap. |

The report retains raw timestamp arrays, actual renderer evidence, browser
version/launch arguments, adapter/context details, every trial, .NET and Uno SDK
versions, exact workload set, original and effective `global.json` with their
byte hashes, source SHA, fixture hash and packaged font hashes. Native font asset
hashes are recorded, not blindly required to be identical across Uno 6 and Uno 7.
Effective browser font families/status are also recorded after startup.

Physical presented-frame counts and managed allocated bytes are `null` when not
measured. Missing data is not reported as zero. CPU render callback completion
does not prove scanout completion. Observer API wrappers add some overhead;
compare the same harness/browser/workload, retain the raw distributions, and
corroborate hardware conclusions with a physical-GPU run.

With `--diagnostics`, a **separate candidate-only pass** enables
`?renderDiagnostics=1&perfDiagnostics=1` after the primary comparison is complete.
It snapshots the application's opt-in Document/Interaction/Ruler/Navigator paint
callback counts, total/max CPU time and current-thread allocated bytes. It also
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

Comparisons use the median of each trial's p95/CPU/throughput metric, preserving
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
