# Pinned runtime command diagnostics

This experiment explains the command and composition differences behind the selection
regression. It consumes two immutable Release AOT packages from performance run
`43265614141`: baseline `96e5fa9d84b694e34a626b8867f2466fc231b6a7` and candidate
`d0dafea8b705bf5e706ee8f5c008663d2d71fa12`. It does not qualify the current source
tree or establish a speedup. The separate transport experiment measures unchanged
candidate packages; this probe intentionally changes served logging configuration.

`pinned_package.py` checks each GitHub artifact's ID, run, source-run head, archive
size and SHA256, then checks the complete package manifest, fixture, execution mode,
index and font identities. Before and after a run it hashes every served asset and
compares it with the contents of the SHA-verified archive. Modifying both an extracted
file and its receipt cannot satisfy this check.

## Supported configuration path

The baseline has no `perfDiagnostics` query or managed diagnostics export.
[Bootstrap 10.1.1 imports root uno-config.js and copies its environmentVariables into
MonoConfig before runtime initialization](https://github.com/unoplatform/Uno.Wasm.Bootstrap/blob/0452dfce12ed05ed4e5a4fb920b52f59973225fd/src/Uno.Wasm.Bootstrap/ts/Uno/WebAssembly/Bootstrapper.ts).
[The generated module declares and exports config](https://github.com/unoplatform/Uno.Wasm.Bootstrap/blob/0452dfce12ed05ed4e5a4fb920b52f59973225fd/src/Uno.Wasm.Bootstrap/ShellTask.cs).

A separate HTTP handler appends exactly these settings and one proof log to that
configuration response:

```javascript
config.environmentVariables["UNO_LOG_FRAME_PHASES"] = "1";
config.environmentVariables["UNO_WEBGPU_STATS"] = "1";
config.environmentVariables["UNO_WEBGPU_STATS_EVERY"] = "1";
```

The original bytes are retained as an exact prefix. The output stores both complete
configuration modules, their original/served SHA256 hashes and the appendix hash.
All other HTTP paths serve their original package files. Application binaries,
runtime files, fonts, native modules and source are unchanged. The served package
configuration **is changed**, so this result cannot enter the primary performance
merger. No private runtime object or late environment-variable mutation is used.

## Four observations on one runner

| Mode | Application source | Document transport |
| --- | --- | --- |
| baseline-original | 96e5 baseline | Original Uno raster upload |
| candidate-legacy | d0 candidate | Original Uno raster upload with the retained editor UI |
| candidate-persistent | d0 candidate | Persistent RGBA upload |
| candidate-composite | d0 candidate | Cached document and feedback composition |

Each observation reuses the existing browser instrumentation, full common warmup,
selection warmup, 121 trusted selection inputs, zero-tail checks, real renderer
negotiation and native-pixel readiness/restore checks. The driver records console
indices immediately around the measured workload. Startup, warmup and screenshot
validation logs remain available in the full console but cannot enter that window's
command summary.

Two additional observations follow the measured window: selected and cleared.
Each is matched with that mode's already recorded `after.png` or `before.png`
using the unchanged whole-frame native-pixel gate. Their final actual native
frame/stat lines are retained separately, so selected and cleared command counts
can be compared without guessing state from a sparse console sample.

Baseline-to-candidate comparisons retain the primary experiment's document, ruler
and chrome-font gates. Candidate legacy-to-persistent/composite comparisons retain
the stricter whole-frame native-pixel gate at luminance 12 and changed fraction 0.001.
Every incomplete or failed observation remains a failure.

## Interpreting native logs

[WebGpuFrame accumulates opsBuild/walk/rebuild/stamp/bake/encode counters until its
logging interval resets](https://github.com/unoplatform/uno/blob/78c746d28293975e64ba0f162af6f44f3c3daaab/src/Uno.UI.Composition.WebGpu/WebGpuFrame.cs).
The ordinary interval is 60 frames; those accumulated values must not be read as
one-frame costs. `UNO_WEBGPU_STATS_EVERY=1` makes this diagnostic's interval one
completed frame. `renderInto` and `finishSubmit` already describe the sampled frame.
The separate `frame-phases` line retains its 60-recording average. Its `gap` measures
time between managed recordings; it does not isolate GPU execution, browser
presentation, scheduling or automation delay.

The probe reports operation, emitted-operation and scissor-change distributions,
raw phase logs, workload observations and unchanged quality gates. Timing remains
instrumented context only; no throughput ratio or physical frame-rate claim is made.

```bash
python tests/UnoDraw.BrowserPerformance/pinned_package.py --role baseline --output artifacts/runtime-packages/baseline
python tests/UnoDraw.BrowserPerformance/pinned_package.py --role candidate --output artifacts/runtime-packages/candidate
python tests/UnoDraw.BrowserPerformance/runtime_diagnostics.py --packages artifacts/runtime-packages --output artifacts/unodraw-runtime-diagnostics --harness-sha "$(git rev-parse HEAD)"
```

The dedicated workflow consumes these archived packages directly and does not build
another AOT application. It requires the pinned artifacts to remain available and
fails if they expire or their identity differs.
