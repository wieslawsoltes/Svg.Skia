# UnoDraw old/new high-DPI rendering quality

This artifact-only check compares the original Uno 7 UI with the candidate at
DPR 1.25 and 2 on both WebGPU and direct Skia/WebGL. It closes the old/new ruler
coverage gap left by a same-candidate persistent/legacy bridge comparison: both
bridge paths otherwise use the candidate's new ruler marker.

The script consumes published packages; it does not compile or modify the app.
Both package manifests must identify Release `InterpreterAndAOT`, the same .NET
SDK, workload set and welcome SVG fixture, and distinct source commits. The
existing performance package validator enforces those build conditions. The
check additionally requires exactly one Uno 7 baseline and one Uno 7 candidate,
and verifies each published index against its recorded hash.

An explicit `--execution-mode Interpreter` enables earlier correctness feedback
from two matching Release Interpreter packages. Both packages must use that
mode; mixed modes and unrecognized modes are rejected. Every case and the report
record the selected mode. The native-pixel matrix and tolerances are identical,
and the final AOT gate remains required. Interpreter results never enter the
performance comparisons, whose validator still requires InterpreterAndAOT.

## Required matrix

Eight cases cover two builds, two graphics backends and two DPR values. Every
case must complete these five states:

1. Fitted desktop with the pointer moved through the same canvas position and
   then parked in the menu strip, retaining matching ruler markers.
2. Actual size through the editor's keyboard command.
3. A real middle-button pan from the fitted camera, including fractional CSS
   translation to exercise physical-pixel alignment.
4. Refit, with the original document and ruler pixels restored.
5. Selected artwork with visible selection feedback and ruler markers.

Each backend/DPR pair produces five old/new comparisons: twenty comparisons in
total. A failed case does not hide later independent cases. Missing, duplicate,
failed or mismatched build/backend/DPR states fail the overall gate. The original
baseline's zero-size resize bug is not exercised here; its corrected lifecycle
remains covered by the candidate bridge A/B matrix.

The actual connected canvas context and successful Uno provider negotiation must
match the requested backend. Candidate WebGPU cases additionally require a
successful persistent bridge upload and no bridge fallback through completion.
The browser uses the existing SwiftShader launch configuration. This validates
the WebGPU/WebGL API paths and pixels; it does not measure physical GPU speed.

## Pixel contract and artifacts

The script reuses the existing capture, renderer-evidence and raw comparison
functions. It retains their 0.1% changed-pixel limit above a luminance difference
of 12/255 for the complete frame, document, and each isolated ruler region.
Every quantitative comparison uses native device-pixel PNGs of identical size.
CSS-sized screenshots serve only as semantic readiness checks. No comparison or
exported ruler strip is resampled.

Every state saves the full native PNG and separate horizontal and vertical ruler
strips. Reports retain package/source provenance, browser/backend evidence,
runtime errors, all case/state outcomes, and changed-pixel fractions. The root
artifacts are `report.json` and `summary.md`.

```bash
python tests/UnoDraw.BrowserRenderQuality/render_quality.py \
  --packages /absolute/path/to/baseline-and-candidate-packages \
  --output /absolute/path/to/render-quality-artifacts

python -m unittest discover -s tests/UnoDraw.BrowserRenderQuality -p 'test_*.py'
```

Install the same pinned Playwright Chromium and Pillow dependencies used by the
browser performance workflow. The output directory must be outside both served
packages. The quality workflow separately verifies that cached application
artifacts remain applicable to the current source revision before invoking this
script. On a pull request, a valid but stale cached package produces an explicit
ineligible source audit and skips the cached suites; fresh builds and both
correctness modes run in the main performance workflow. A manual invocation with
stale artifacts fails. Invalid manifests or missing source history always fail.
