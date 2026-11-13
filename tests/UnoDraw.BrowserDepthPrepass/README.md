# Supplemental WebGPU depth-pass probe

This is a separately instrumented configuration experiment. It reuses the original
`22d33286feccb2d53484744d9ecf7f9f6d17e72d` application and primary benchmark harness
and runs only explicit `canvasBridge=single-canvas`. The adapter's checkout and
source identities are recorded separately. The report makes no claim that the
current application source is equivalent to the pinned package.

The pinned application and harness predate the current Skia default. Their
original WebGPU selection remains part of this historical experiment; this probe
does not substitute the current application's default or navigation policy.

The two arms use one original Release AOT archive, one explicit canvas mode and
three alternating selection pairs. They reuse the committed benchmark's full
warmup, all 121 trusted selection inputs, raw observer/CDP metric calculation and
original selected/restored image checks. Nine cross-arm native image pairs retain
the existing whole-frame gate, DPR 1 and luminance-12 / 0.001 thresholds.

Only the HTTP response for `uno-config.js` receives an appendix, setting
`UNO_WEBGPU_NO_DEPTH_OCCLUSION` to `0` or `1` before managed startup. The original
configuration prefix, both complete modules and their hashes are retained. Every
application file remains checked against the original GitHub archive before and
after use. The harness is extracted from its explicit commit into an isolated
snapshot before import, with committed blob and final inventory checks.

The symmetric observer counts actual `beginRenderPass` calls that returned with
and without depth attachments. Counts cover the complete application context,
including startup, warmup and restoration. They do not measure a timed-window
pass count, asynchronous GPU validation, GPU duration or physical presentation.
Getter-based, inherited or ambiguous attachment evidence fails qualification.
The runtime's `prepass` statistic is not an activation gate: native cover-building
still occurs when depth encoding is disabled.

Any result is a separately instrumented configuration comparison on SwiftShader.
It cannot enter the unchanged-package primary aggregate or establish a hardware
default policy. A meaningful result would still require ordinary application
performance and complete native quality qualification before a production change.

## Immutable inputs and workflow scope

The package is artifact `13991596503` from performance run `43316652725`, with
archive SHA-256
`29cf773ff4c44261a9915091a308c03f765c9167f9cc9698328727989dae438d`.
The adapter additionally pins its byte count, complete manifest hash and the
inventory hash of all 907 served files. A replaced artifact, changed source or
expired download fails acquisition; this workflow does not rebuild the package.

The new workflow supports reusable and manual invocation. Its separate PR caller
accepts only a `labeled` event for the exact label `unodraw-depth-prepass`.
Applying that label explicitly requests one attempt. Keeping the label on a PR
does not repeat the probe on later source pushes; removing and reapplying it is
another explicit request. Unrelated label events skip the caller job.

The workflow first validates only the new Python, JavaScript and workflow contracts, then
extracts the committed primary harness before importing its package helper or
using its pinned browser requirements. Each execution writes a distinct
`supplemental_webgpu_depth_configuration_probe` report, configuration proofs,
original native captures, the package receipt and source snapshot.

There is no edit to the ordinary performance matrix, its observer, application
code or renderer default. The driver has no shortened workload option or alternate
canvas mode. It checks the original archive before and after every trial and
again at completion. The source snapshot is checked after the experiment.

## Local contract validation

```sh
python -m pip install PyYAML==6.0.3
python -m unittest discover -s tests/UnoDraw.BrowserDepthPrepass -p 'test_*.py'
node --test tests/UnoDraw.BrowserDepthPrepass/test_depth_observer.mjs
```

These synthetic contract checks do not execute a browser or qualify application
performance. Actual configuration observations, complete paired workloads and
native image results remain necessary for any diagnostic conclusion.
