# SVG document bridge quality

This gate compares the same published UnoDraw candidate package with
`canvasBridge=persistent` and one selected GPU document painter: `webgl` or
`graphite`. Both cases use the real WebGPU application compositor, and auxiliary
surfaces keep the persistent CPU bridge. WebGL rasterizes through Ganesh and
transfers the document image; Graphite submits directly into the Uno texture on
the shared WebGPU queue. Matching preference and successful initialization
evidence are required, and any fallback or another experimental mode fails the
case. These are native-pixel correctness checks, without timing claims.

## Run

Install `tests/UnoDraw.BrowserSmoke/requirements.txt` and its pinned Playwright
Chromium, then run:

```sh
python tests/UnoDraw.BrowserRenderQuality/svg_bridge_quality.py \
  --package /path/to/candidate-package \
  --execution-mode InterpreterAndAOT \
  --candidate-bridge webgl \
  --output /path/to/svg-quality-artifact
```

Use `--candidate-bridge graphite` for the independent Graphite comparison. The
default remains `webgl` for existing callers. Each report compares persistent
CPU with only its selected GPU path; one path cannot substitute for another or
satisfy its coverage. CI runs the two selected-painter jobs independently.

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

Each CPU/selected-GPU pair uses the existing whole-frame, document and ruler comparisons
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
