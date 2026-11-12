# SVG document bridge quality

The performance workflow compares the original canvas layout with the current
separate interaction surface in one published candidate package: `legacy`
versus `single-canvas`. Both use the real WebGPU application compositor, original
Uno texture uploader and original Skia painters. The single-canvas option removes
the interaction child before load and repaints both layers on the original canvas.
Matching preference and completed-painter evidence are required, alongside actual
provider selection and every native output state. Unexpected fallback or another
experimental mode fails the case. These are native-pixel correctness checks,
without timing or pixel-cache claims.

## Run

Install `tests/UnoDraw.BrowserSmoke/requirements.txt` and its pinned Playwright
Chromium, then run:

```sh
python tests/UnoDraw.BrowserRenderQuality/svg_bridge_quality.py \
  --package /path/to/candidate-package \
  --execution-mode InterpreterAndAOT \
  --candidate-bridge single-canvas \
  --output /path/to/svg-quality-artifact
```

The CLI retains `legacy-composite` as its default argument for the earlier
explicit cached-composition comparison against `legacy`. Both of those modes use
Uno's original texture uploader. All six sessions, eight source replacements per
session, 48 cases and 72 native comparisons remain required. Each composite case
must prove a completed copy into the original host. Source replacement and
camera changes repaint the document, so this matrix does not claim
interaction-only cache reuse. The separate Welcome, hover and selection gates
require actual reuse.

`--candidate-bridge composite` instead compares `persistent` with `composite`.
Both use the persistent RGBA texture uploader; the candidate must prove a
successful combined document/interaction upload. It retains the same complete
scene, device-scale and camera matrix, source-editor round trips and native
pixel gates. Its source/camera cases also leave overlay cache reuse to the
separate Welcome and feedback workloads. Reports and aggregates retain the
declared control/candidate pair and cannot substitute the original uploader.

The former WebGL document transport and direct Graphite document renderer are
retired from the application and live CLI/workflow after failed performance or
broader-scene quality experiments. Historical matrix, coverage, source and pixel
validators retain their archived protocols so all failures remain independently
reviewable using their identified immutable harness revisions. No retired query
can satisfy a requested native renderer initialization. The normal Skia/WebGL
application compositor and Uno's WebGPU compositor remain supported and measured
in the primary benchmark and cross-build quality matrix.

`--package` is a single directory containing `benchmark-package.json` and
`wwwroot/index.html`. The manifest must identify the candidate source commit,
Uno 7, Release configuration, exact SDK/workload and matching index hash.
`--execution-mode Interpreter` explicitly permits a correctness-only interpreter
artifact; it does not supply performance results. Output must be outside the
served package. The caller must also run the existing strict source-equivalence
audit when using cached artifacts.

The bounded matrix uses six browser sessions: two bridge modes at DPR 1, 1.25 and
2. Each session edits all eight scenes through the production **Edit SVG source**
dialog. There are 48 scene cases and 72 matched comparisons across fit,
fractional pan and fractional zoom. A failed scene remains failed; a fresh
context lets subsequent scenes continue without inheriting its modal or recovery
state. Missing, duplicated or failed cases/states make the whole gate fail.

## Complete quality through three DPR jobs

The optional `--dpr 1`, `--dpr 1.25` or `--dpr 2` selector divides this same
matrix at the browser-session boundary. Each job retains both declared modes,
all eight consecutive source replacements in each session, and all three camera
states. It produces **16 cases and 24 native comparisons**. The source editor,
input sequence, fixtures, viewport, image dimensions and quality thresholds are
unchanged. Omitting `--dpr` continues to run the original six-session matrix.

A single-DPR report has `report_kind: svg_document_quality_dpr_shard`,
`full_matrix: false` and `full_matrix_passed: false`, even when that portion
passes. It cannot qualify the complete renderer. This permits independent jobs
to finish within their time budget without dropping any scene or reducing the
existing eight-scene replacement/lifetime sequence.

The reusable `unodraw-svg-quality-shards.yml` workflow pins one immutable harness
commit and one existing candidate AOT package run. Each job verifies the exact
GitHub artifact archive, complete manifest and served-file inventory; retains
that portable proof as `package-artifact.json`; and runs the existing strict
production-source equivalence audit before opening a browser. Source eligibility
alone does not establish a pixel-quality pass. The same source/manifest/font and
archive identities must be present in all three results.

For example, run the existing source-equivalence audit into each output
directory, retain its verified package receipt there, then execute the same
command on three separate runners/checkouts of the identical harness commit:

```sh
python tests/UnoDraw.BrowserRenderQuality/svg_bridge_quality.py \
  --package /path/to/verified-candidate-package \
  --candidate-bridge single-canvas --dpr 1.25 \
  --output /path/to/svg-shards/artifact-dpr-1.25
```

Use `1` and `2` for the other two portions. Control/candidate pairs cannot be
pooled. The strict aggregate consumes the verified Release AOT artifact receipts
from the performance workflow; the original full runner continues to support
the separate Interpreter correctness mode. Historical renderer/coverage metadata
remains part of passive validation, without enabling retired production paths.

Download the three complete artifacts into separate immediate subdirectories
under one parent, then run:

```sh
python tests/UnoDraw.BrowserRenderQuality/merge_svg_shards.py \
  --shards /path/to/svg-shards \
  --output /path/to/svg-aggregate \
  --shard-jobs-result success
```

The merger requires exactly DPR 1, 1.25 and 2, with matching complete package and
artifact identities, source-equivalence proofs, actual validation-file hashes,
harness commit, browser version/launch options, font observations and renderer
pair/coverage. It rejects duplicate or missing cases, states and comparisons;
failed or cancelled portions; changed source proofs; and artifact paths that
escape their directory. Local package paths may differ between runners; the
complete package manifest otherwise must match.

`--shard-jobs-result` must reflect the actual completed jobs. The workflow passes
GitHub's `needs.compare.result`; a failure, cancellation, skip or omitted/unknown
result prevents qualification even if a job wrote a passing report before it
stopped. Available native evidence is still retained and recalculated. The
merger checks and binds the saved source-equivalence proof; the preceding
`verify_source.py` workflow step performs the actual Git production-source audit.

It then reopens every canonical/input/committed SVG, checks the source-editor
readback proof, and inspects all **144 native PNG states** again. It recomputes
the calibration/color/body-readiness checks and both camera transitions, verifies
that each saved body crop is an exact crop of its original PNG, and invokes the
original `compare_scene` path for all **72 matched native comparisons**. Recorded
`passed` fields and readiness/comparison summaries cannot replace those checks.
Actual successful renderer negotiation, native canvas activity and mode-specific
initialization/fallback evidence remain required in every case.

Only a complete successful recalculation emits
`report_kind: svg_document_quality_dpr_aggregate`, `full_matrix: true` and
`full_matrix_passed: true`. A failed aggregate retains a failure decision and no
full-matrix qualification. Its output is self-contained: the original three
artifact trees, per-shard report/proof hashes, all native captures and source
readbacks accompany the recomputed aggregate. This is correctness evidence and
does not produce or combine timing results.

## Source and pixel evidence

The eight SVG files in `fixtures/svg-bridge` preserve the bodies from
`RgbaCanvasEquivalenceTests.CreateSvg`: gradients, filters, mask, clip, pattern,
text, image, and opacity/strokes. Their byte hashes and the originating C# source
hash are recorded in `manifest.json`. The embedded PNG was produced by the same
SkiaSharp premultiplied `SKBitmap.SetPixel`/PNG encoding code as the native test.
It was not reconstructed by a different image encoder.

For browser input, each unchanged body is placed in a named group inside a
200×140 document. A separate calibration strip to the right of the original
160×110 body contains two scene-specific opaque anchors and eight alpha/color
patches over white and dark mattes. Four patches use fill opacity and four use
group opacity. Their two-decimal opacity values survive the production SVG
serializer without a rounding rewrite.

The runner opens the command palette, invokes the source editor, focuses its
native multiline textarea, selects all text and uses real keyboard text input.
Before Apply, the public DOM textarea value must exactly match the inserted
document after newline normalization. After Apply, the runner reopens the source
dialog and verifies the serialized model's scene identity and complete element
tree. Known serialization changes such as presentation attributes moving into
`style`, percentage gradient offsets, and explicit zero rotation centers are
normalized. Changed geometry, paint, filters, opacity, text, image bytes, added
rendering attributes or extra elements fail the check. The source dialog uses
the observed 1440×960 native UI coordinates if an accessible Apply SVG button is
not exposed. Browser CI must verify this UI protocol; pure contracts alone do
not claim a successful browser edit.

Every scored image is a full native device-pixel screenshot. No scored image is
resized. The scene-specific anchor colors prove that the latest scene appeared
and determine its physical camera geometry. The pan must move that geometry by
the submitted fractional displacement; the wheel must produce a fractional zoom.
The original body must remain inside the editor canvas and contain substantial
chromatic artwork. Every pixel in each calibration center patch must be within
two levels of its independent expected RGB result.

Each declared bridge pair uses the existing whole-frame, document and ruler comparisons
at the unchanged maximum **0.1% of pixels above luminance difference 12**. The
original 160×110 scene body is also compared at that same threshold in a separate
physical crop. The calibration strip and surrounding UI cannot dilute a missing
or changed small scene. Full native PNGs, body crops, source input/readbacks,
per-case logs, package metadata and hashes of the actual test files are saved.

## Contract tests

```sh
python -m unittest discover -s tests/UnoDraw.BrowserRenderQuality -p 'test_*.py'
```

The scene tests verify exact fixture extraction and PNG provenance, legitimate
source serialization, rejection of mutated feature data and added transforms,
blank/stale scene rejection, native dimensions, channel and premultiplication
errors, the tighter body crop, actual camera changes, and complete failure
continuation, including rejection of a substituted GPU path or incomplete selected
pair. These tests validate the gate. Only a completed browser report
validates the two rendering paths.

The shard tests additionally exercise exact matrix coverage, immutable
source/package/browser identities, rejection of altered native images despite
passing report fields, source/camera/crop checks, safe artifact paths, and a full
synthetic 48-case/144-state/72-pair merge through the original native-image
comparison functions. Synthetic contracts qualify the merger, not a browser
renderer.
