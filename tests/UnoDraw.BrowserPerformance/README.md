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
SDK and AOT build settings; an incompatible fixture is rejected explicitly.

Uno 6 is measured on its actual Skia/WebGL renderer. Uno 7 is measured with both
WebGPU and `?renderer=skia`. An unavailable legacy WebGPU implementation is never
represented by a fabricated zero or by a different renderer.

## Workloads and controls

Every independent trial gets a fresh browser process, context and application
startup. The workload's input path is warmed once outside measurement, then the
camera, artwork and selection are restored. The benchmark uses the unchanged
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
a substitute Uno 7 pixel baseline.

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
| Completed render callback count and duration | Successful application rAF callbacks that actually issued drawing or a submission to the visible main canvas. Both pinned runtimes synchronously call managed `RenderFrame` inside that callback. This includes synchronous CPU rendering/submission, not later GPU execution or physical presentation. |
| Input to completed render callback | Trusted input dispatch to the next completed callback containing graphics work. Multiple events may coalesce; this is not proof of per-event causality or input-to-photon latency. |
| Graphics-active animation intervals | Observer intervals containing main-canvas drawing/submission calls. Detached capability probes cannot satisfy the count. |
| WebGPU canvas submissions / WebGL flushes | Observed API calls. A WebGPU submission must follow acquisition of the application's canvas texture. Neither count is labeled as display presentation. |
| Drawing and texture-upload calls | Browser API counters. `supplied_upload_buffer_bytes` sums the sizes of supplied buffer views; it is **not** the actual consumed transfer extent because offsets, row pitch and subregions can consume less. Calls without a buffer size are counted separately. |
| CDP task/script/layout/style time | Main-thread CPU time, including a small amount of observer and automation overhead. Task time is not the sum of the other fields. |
| JavaScript used-heap delta | Signed difference in used JS heap; GC can make it negative. This is neither an allocation count nor the managed WASM heap. |

The report retains raw timestamp arrays, actual renderer evidence, browser
version/launch arguments, adapter/context details, every trial, .NET and Uno SDK
versions, source SHA, fixture hash and packaged font hashes. Native font asset
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
have occurred for the runtime to print its periodic sample. These counters do not
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
parallel. Published packages are cached by exact source SHA, .NET SDK, workload
version, platform and build flags. A new candidate can reuse the expensive
unchanged baseline packages. A newer push does not cancel a useful run in flight.
Build jobs for the same ref queue behind one another with `queue: max`, then
reuse the completed package cache. This also preserves pending builds instead of
replacing them; GitHub documents the queue behavior in its
[concurrency guide](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency).

All package/backend cases are then measured on **one runner**. Package order
rotates between trials and renderer order alternates. Results include a Markdown
summary, JSON observations, console/resource diagnostics and screenshots. The
normal browser-smoke workflow remains an independent visual/runtime gate.

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

CI uses the same pinned Playwright Chromium and SwiftShader switches as the
correctness matrix. These exercise WebGPU and WebGL APIs through software GPU
implementations. They measure software graphics and application CPU behavior,
**not physical GPU throughput**. A genuine WebGPU adapter and the expected Uno
provider must be observed; merely exposing `navigator.gpu` cannot pass.

## Local reproduction

Publish each revision in its own checkout with the same SDK and these settings:

```sh
dotnet workload install wasm-tools --skip-manifest-update
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

For a physical-device comparison use `--graphics hardware` and, if needed,
`--executable /path/to/chromium`. This removes the forced SwiftShader switches;
inspect the recorded actual WebGPU adapter and WebGL renderer before interpreting
the result as hardware performance. Use the same machine, browser, power state,
viewport, DPR, artifacts and trial ordering for both revisions. A local browser
that cannot start because of a host restriction is a blocked measurement, not a
zero-duration sample or justification for weakening the browser sandbox.
