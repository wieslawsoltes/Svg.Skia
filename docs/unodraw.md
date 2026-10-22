# UnoDraw

UnoDraw is the Uno Platform successor to the AvalonDraw sample. It edits SVG documents through the repository's SVG DOM and renders them with Svg.Skia. The application lives in [`samples/UnoDraw`](../samples/UnoDraw); its reusable Uno workspace lives in [`src/Svg.Editor.Skia.Uno`](../src/Svg.Editor.Skia.Uno).

The application migration does not remove the reusable Avalonia editor packages. Existing consumers can continue using `Svg.Editor.Avalonia` and `Svg.Editor.Skia.Avalonia`.

## Workspace preview

These are native Linux captures of UnoDraw editing the bundled welcome SVG.

| Light theme | Dark theme |
| --- | --- |
| ![UnoDraw light workspace](images/unodraw-workspace-light.png) | ![UnoDraw dark workspace](images/unodraw-workspace-dark.png) |

## Build and run

Use the .NET 10 SDK selected by the repository's `global.json`. The UnoDraw project targets `net10.0-desktop`, supporting Windows, macOS and Linux/X11 through Uno's Skia desktop host. Android, iOS and WebAssembly are not application targets in this migration, so their workloads are not required to build UnoDraw.

```sh
git submodule update --init --recursive
dotnet restore samples/UnoDraw/UnoDraw.csproj
dotnet build samples/UnoDraw/UnoDraw.csproj -f net10.0-desktop -c Release
dotnet run --project samples/UnoDraw/UnoDraw.csproj -f net10.0-desktop
```

To open an existing document on launch:

```sh
dotnet run --project samples/UnoDraw/UnoDraw.csproj -f net10.0-desktop -- /path/to/drawing.svg
```

The initial document is [`Welcome.svg`](../samples/UnoDraw/Assets/Welcome.svg), an editable 1200 × 800 composition with gradients, text, clipping, symbol instances and grouped geometry. It uses no external images or font downloads. The existing tiger and camera samples remain available alongside it.

For Linux host dependencies and graphics configuration, follow the [Uno Skia Desktop documentation](https://platform.uno/docs/articles/features/using-skia-desktop.html). A working graphical desktop is required to launch the app; command and geometry tests run without a window.

## Architecture

| Component | Responsibility |
| --- | --- |
| `Svg.Editor.Core` | Document session, selected IDs, outline models, settings and undo/redo snapshots. |
| `Svg.Editor.Svg` | SVG document services, resource-path rebasing, property conversion, tools, paints, layers, resources and appearance models. |
| `Svg.Editor.Skia` | `SvgEditorController`, geometry commands, retained scene hit testing, path services and `SvgEditorViewport`. |
| `Svg.Editor.Skia.Uno` | Uno workspace, `SvgEditorCanvas`, pointer routing, tool controls, inspector and document workflow. |
| `Svg.Skia` and its existing dependencies | SVG parsing, resource loading, scene compilation, text shaping, paint/filter/mask interpretation and Skia picture creation. |
| `samples/UnoDraw` | Desktop application startup, branding and sample assets. |

### Document ownership

`SvgDocument` is the editable source of truth. Commands mutate the DOM, retain its resource definitions and serialize native SVG for saving and history. An SVG save does not flatten the drawing into a rendered path collection or bitmap. Serialization can normalize XML formatting; it is not a byte-for-byte source preservation mechanism.

`SvgEditorController` owns the active renderer and exposes the retained picture and scene nodes for drawing and selection. The controller does not depend on Uno or Avalonia. This keeps geometry, selection, transaction and document tests independent of the windowing framework.

The public controller supports explicit transactions:

```csharp
using var editor = new SvgEditorController();
editor.LoadSvg(svgSource);
editor.Select(editor.Document!.GetElementById<SvgRectangle>("shape"));

editor.BeginEdit("Move shape");
editor.MoveSelection(24, 12);
editor.CommitEdit();

editor.Undo();
var editableSvg = editor.GetSvg();
```

Use `CancelEdit()` when a gesture is interrupted. `Execute(label, action)` supplies an atomic mutation boundary and restores the prior document if the mutation fails. Selection changes do not add undo entries. Dirty state is measured against the last saved XML checkpoint, so undoing back to that checkpoint clears the modified indicator.

`SvgResourcePathService.Rebase(xml, originalDocumentUri, targetDocumentUri)` preserves linked resource locations when saving into another directory. It rewrites relative `href`/`xlink:href` references, CSS `url()` values and stylesheet imports, including `xml-stylesheet` processing instructions. Nested relative `xml:base` values are rebased before their descendants are visited. Absolute URLs, embedded data URIs, and local `#fragment` references retain their spelling. CSS comments and ordinary quoted text are preserved. The helper uses absolute document URIs, does not download or copy resources, and disables external XML entity resolution. Referenced external stylesheets continue resolving their own relative resources from the same stylesheet location.

After successfully writing a captured SVG snapshot, hosts call `SvgEditorController.AcceptSave(savedXml, fileName)`. Saving to a new location updates the resource context of the current document and its undo/redo snapshots without adding a separate editing operation. The checkpoint represents the snapshot actually written; edits made while an asynchronous save was in progress remain dirty.

### Canvas and performance

`SvgEditorCanvas` derives from Uno's `SKCanvasElement`. It draws into the Skia surface already used by Uno and replays the cached Svg.Skia picture. It does not rasterize an intermediate image on every frame.

The desktop host normally selects an accelerated backend and may fall back to software. Using `SKCanvasElement` enables direct accelerated drawing when the host provides it; it does not prove that a particular machine is using a GPU. The workspace deliberately identifies the rendering path as a direct canvas rather than displaying an unconditional hardware status claim.

`SvgEditorViewport` keeps camera state separate from document state. Pan, zoom, fitting, selection and overlays do not serialize the SVG or recreate its retained picture. Zoom is anchored to the cursor and bounded between 1% and 12,800%; calculations use double precision before conversion to the Skia transform. The picture cache is invalidated by document edits.

Move, resize and rotate previews record a native picture of the selected scene once when a gesture begins. Pointer movement transforms and replays this cached picture with approximately 65% opacity over the original artwork. The snapshot retains paint order and ancestor transforms, clipping, masks, filters and opacity. It is released when the gesture commits, is cancelled, loses capture or the canvas unloads.

Document transactions currently use XML snapshots. They coalesce a gesture into one history operation, but this is not a persistent immutable document model or a fully incremental renderer. Large document serialization, complex filters and expensive font/resource loads remain performance considerations. Retained-scene mutation APIs in Svg.Skia are available for future measured optimizations.

### Geometry correctness

Selection geometry comes from Svg.Skia's retained scene. Movement and affine transforms are expressed in picture coordinates and mapped through the selected object's parent transform. A child inside a scaled or rotated group therefore follows the same world-space movement as a root object. Selecting both an ancestor and its descendant does not apply a transform twice.

Boolean operations transform the input paths into a common destination coordinate system before invoking Skia path operations. Grouping is restricted to a common parent. Ungrouping ordinary groups carries inherited presentation values and transforms to the children. Groups with compositing opacity, masks, clips or filters are rejected for ungrouping because distributing those effects across children would change the rendered result.

## Workspace and controls

The workspace uses document tabs, a tool rail, a searchable document outline, a contextual design inspector and a command search. The outline and inspector collapse at narrower window sizes; focus-canvas mode hides both panels. Light and dark themes share the same layout.

Open `.svg` or compressed `.svgz` files from the file picker, drag files onto the workspace, or supply a path at startup. Raster images placed into a document are embedded as data URIs. Export supports editable SVG, PNG, JPEG, WebP, PDF and XPS through the existing Svg.Skia export APIs. Selected-object export includes SVG resources needed by the copied content. The export scale is limited to 0.01–16× and oversized raster exports are rejected before allocation.

| Action | Keyboard or pointer control |
| --- | --- |
| New / open / save | `Ctrl+N`, `Ctrl+O`, `Ctrl+S`; `Ctrl+Shift+S` saves to a new destination. |
| Search every command | `Ctrl+K`. |
| Select / edit nodes | `V` / `A`. |
| Rectangle / ellipse / circle / line | `R` / `E` / `C` / `L`. |
| Bézier pen / quadratic pen / pencil | `P` / `Q` / `B`. |
| Polygon / polyline / text | `G` / `O` / `T`. |
| Add to selection | Shift-click or Control-click. |
| Pan | Hold Space and drag, middle-button drag, or select the hand tool with `H`. |
| Zoom / horizontal pan | Mouse wheel / Shift-wheel; two-finger pinch also changes the viewport. |
| Fit artboard / fit selection | `Shift+1` / `Shift+2`. |
| Actual size | `Ctrl+0`. |
| Nudge / larger nudge | Arrow keys / Shift+arrow keys. |
| Duplicate / group / ungroup | `Ctrl+D` / `Ctrl+G` / `Ctrl+Shift+G`. |
| Undo / redo | `Ctrl+Z` / `Ctrl+Shift+Z` or `Ctrl+Y`. |
| Finish a pen path | Enter, right-click or double-click; click the first node to close it. |
| Cancel a provisional gesture | Escape. |
| Focus the canvas / shortcut help | Tab / `F1`. |

Pen clicks add anchors; dragging an anchor while drawing a cubic path establishes its tangent. Shift constrains movement, shape aspect ratio, line direction and rotation as appropriate. Node editing operates on a provisional clone so selecting a path does not normalize or modify the saved SVG. The node tool also edits native polygon and polyline vertices without converting them to paths: double-click an edge to insert a vertex, or delete a selected vertex while retaining at least three polygon vertices or two polyline vertices. Move, resize and rotate gestures show a translucent full-color preview with selection handles and commit the actual document transform on release.

**Path → Variable width stroke** turns an open path into an ordinary filled SVG outline with a live preview, width presets, editable width points and flat or round ends. Widths interpolate along arc length in the path's local SVG coordinates. The original centerline and width profile remain in `data-unodraw-*` attributes so the dialog can adjust the profile later. Editing the generated outline's nodes invalidates that link; reopening then asks for an unchanged centerline instead of overwriting those edits. Multiple open contours are supported; closed contours and square caps are not. The outline replaces the original path's fill and stroke while retaining its ID, transforms and effects, and is one undoable edit.

**Object → Document swatches** creates or edits reusable solid-color SVG gradient resources. A swatch can remain in the document palette or be linked to the selection's fill or stroke. Editing a shared swatch recolors all linked artwork. The dialog preserves existing resource and stop IDs and excludes opacity gradients from solid-color editing.

Editing shortcuts are suppressed while a text-entry control has focus. The workspace also accepts the platform Windows/Command modifier where Uno reports it for the corresponding Control shortcuts.

## Feature coverage

Rendering an SVG construct, retaining it in the document, editing its attributes and providing a specialized visual authoring tool are separate capabilities. This distinction matters when comparing the new workspace with the previous sample.

### Shared document and rendering capabilities

UnoDraw continues to use the repository renderer for supported paths, shapes, text, gradients, patterns, images, clipping, masks, filters, symbols, CSS and SVG resource references. The application does not introduce a reduced SVG renderer. The precise SVG specification coverage and limitations remain those documented by the main [repository guide](../README.md) and renderer tests.

Keeping an SVG element in a document does not automatically expose its own dedicated Uno editing dialog. The following table records the migration boundaries.

| Area | Reusable engine and document support | Uno authoring coverage |
| --- | --- | --- |
| Native SVG document editing | SVG DOM, native serialization, undo/redo transactions and dirty checkpoints. | Multiple document tabs, SVG/SVGZ open/save, source and attribute editing. |
| Selection and arrangement | Hit testing, multiple selection, grouping, z-order, world transforms, alignment and distribution. | Canvas selection and arrangement commands. |
| Primitive drawing | Existing `ToolService` creates rectangles, circles, ellipses, lines, polygons, polylines, text, paths and freehand paths. | Tool rail and pointer gestures; specialized tools are documented by their exposed commands. |
| Path geometry | Existing path editing, curve segments, boolean operations, conversion and simplification. | Path nodes, native polygon/polyline vertices, geometry commands and cached transform previews; advanced drawing workflows remain under development. |
| Fill, stroke and typography | Existing SVG paint, stroke and text property model. | Contextual inspector and SVG attribute editing. |
| Gradients, swatches, patterns and symbols | SVG definitions and resource references remain available. | Resource list, creation commands, and shared document swatch editing; richer resource editing remains a separate authoring layer. |
| Variable-width strokes | `SvgVariableStrokeService` produces portable filled SVG outline paths with retained editable centerline metadata. | Live profile preview, taper/brush presets, width points, and flat/round caps for open paths. |
| Meshes and brush libraries | Existing experimental services and DOM data remain in the repository. | Dedicated mesh and brush-library editors are not exposed; the legacy mesh service is not a complete mesh renderer. |
| Layers and document hierarchy | SVG groups and the existing layer metadata model. | Document outline and layer commands; full hierarchy drag/reparent workflows remain separate work. |
| Masks, filters and effects | Existing Svg.Skia rendering and native SVG references. | Clipping and drop-shadow commands plus source/attribute editing; no complete visual effect graph or filter builder. |
| Animation and script-backed documents | Existing animation, composition and JavaScript libraries remain available. | SVG animation playback; a full timeline, keyframe editor and interactive preview authoring workflow are outside this migration. |
| Advanced typography and print production | Existing supported SVG text and vector/raster export APIs. | No complete paragraph composition, linked text-frame, color separation, preflight or production print workflow. |

This is a desktop migration and substantial editor refactor. The table does not claim complete professional vector-authoring parity.

### Previous workspace inventory

The prior Avalonia workspace remains the reference for additional UI migration. Its implemented surfaces include:

- Open/save, selected-element export, PDF/XPS export, image placement, drag/drop and a separate preview window.
- Shape and path tools, freehand drawing, polygon/polyline editing, path point editing, resize/rotate/skew, snapping and wireframe rendering.
- Selection, grouping, z-order, alignment, distribution, flipping, boolean operations, clipping masks, blending, offsets and simplification.
- Document-tree search, expansion state, tree drag/reorder/reparenting, artboard selection, layer visibility and layer locking.
- Text, generic content and property editors; gradients and stops; gradient mesh, pattern, swatch, symbol, CSS style and stroke-profile editors.

The legacy operation called “Divide” delegates to reverse difference; it is not a complete multi-region path division implementation. New commands should preserve the actual operation semantics when exposing these services.

## Validation

The focused desktop workflow is [`.github/workflows/unodraw.yml`](../.github/workflows/unodraw.yml). It builds the application and runs the framework-independent SVG editor tests on Linux, Windows and macOS. Its Linux startup check launches the built app with the welcome SVG under Xvfb, catches early exits and native loading failures, and uploads the startup log. It uses software graphics in CI, requires no mobile workloads, and does not claim to exercise physical GPU hardware.

```sh
dotnet test tests/Svg.Editor.Skia.UnitTests/Svg.Editor.Skia.UnitTests.csproj -c Release
dotnet test tests/Svg.Editor.Svg.UnitTests/Svg.Editor.Svg.UnitTests.csproj -c Release
```

Controller regressions cover transformed-parent movement, ancestor/descendant selection, retained-picture stability during selection, atomic rollback, dirty-state checkpoints, Save As history, and preservation of compositing references when converting, copying or clipping geometry. Viewport tests cover coordinate conversion, anchored zoom and invalid camera input. Resource-path tests verify that Save As references resolve to the same absolute resource across directories, file volumes and web origins, including nested XML bases, CSS escapes, stylesheet links and local fragments. Vertex tests preserve native polygon/polyline identity, presentation, metadata and point units; they exercise geometry limits, invalid input and undo under parent transforms. Retained-preview tests verify compositing and snapshot lifetime; path tests exercise node deletion and smooth shorthand segments. Variable-width stroke tests cover caps, tapers, curves, multiple contours, geometry limits, locks, one-step undo, metadata reopening, stale-node rejection and raster rendering after an SVG round trip. Existing service tests continue covering the underlying geometry and SVG editor helpers.

Repository-wide validation remains:

```sh
dotnet format Svg.Skia.slnx --no-restore
dotnet build Svg.Skia.slnx -c Release
dotnet test Svg.Skia.slnx -c Release
```

### Migration validation record

The following full-solution results were recorded on Linux with .NET SDK 10.0.402 at commit `a313cc429ee12d6f448f9d75f0ed0ef40a7780f7`:

| Test assembly | Passed | Skipped |
| --- | ---: | ---: |
| `Svg.Editor.Skia.UnitTests` | 90 | 0 |
| `Svg.Editor.Svg.UnitTests` | 20 | 0 |
| `Svg.Skia.UnitTests` | 1,112 | 44 |
| `Svg.Model.UnitTests` | 212 | 0 |
| `ShimSkiaSharp.UnitTests` | 141 | 0 |
| `Svg.JavaScript.UnitTests` | 27 | 0 |
| `Svg.Controls.Avalonia.UnitTests` | 19 | 0 |
| `Svg.Controls.Skia.Avalonia.InvariantGlobalization.UnitTests` | 3 | 0 |
| `Svg.Controls.Skia.Avalonia.UnitTests` | 56 | 0 |
| `Svg.Controls.Skia.Uno.UnitTests` | 28 | 0 |
| `Svg.Editor.Skia.Avalonia.UnitTests` | 18 | 0 |
| **Total: 11 test assemblies** | **1,726** | **44** |

The complete solution restore, Release build and test run all passed, with **1,726 passing tests, zero failures and 44 existing renderer skips**. The Release solution build completed with zero errors; existing dependency and obsolete-API warnings remain. The UnoDraw Release build and the reusable editor builds for `net461`, `netstandard2.0`, `net6.0`, `net8.0` and `net10.0` also passed.

The [UnoDraw desktop CI run](https://github.com/wieslawsoltes/Svg.Skia/actions/runs/39800904057) passed the application build and both editor suites on Windows, macOS and Linux, including the Linux native-startup check. The [documentation build](https://github.com/wieslawsoltes/Svg.Skia/actions/runs/39800903927) also passed.

Native Linux checks under Xvfb exercised startup, selection, drawing, undo, recovery, pending inspector edits, native SVG open and Save As, subsequent save after drawing, PNG export, shared swatch creation, keyboard command search, tab closure and both themes. The width-profile dialog rendered a live tapered curve, saved its portable outline and centerline metadata, and restored the original path with one undo. The saved drawing contained the new editable SVG rectangle, and the exported image retained the document dimensions. These checks validate runtime behavior on the virtual display, not physical GPU performance.

The renderer result includes all 272 retained-scene tests. Solution projects and test assemblies ran serially to fit the validation host. UI tests used Xvfb. Because the container runs as root, the test process dropped its read-bypass capabilities so `OpenPath_IgnoresUnreadableImportedStylesheets` exercised normal Unix file permissions; no test or reference image was changed for that environment. Tests ran against the completed Release build with `--no-build --no-restore`. The separate legacy `Avalonia.Svg.Skia.UiTests` project is outside `Svg.Skia.slnx` and was not part of this solution gate.

Existing NuGet audit warnings were recorded for pinned `Microsoft.Build.Tasks.Git`, `Tmds.DBus.Protocol` and `SixLabors.ImageSharp` dependencies. Broader dependency upgrades are outside this migration.

A desktop smoke pass should open the welcome and existing tiger documents, exercise save/reopen, verify transformed selections, cancel an active gesture, compare undo/redo output, and inspect both themes at different window sizes. GPU backend selection, frame timing and device-specific pointer behavior require a graphical host and must be reported independently of a successful compile or unit-test run.

## References

- [Uno SKCanvasElement](https://platform.uno/docs/articles/controls/SKCanvasElement.html)
- [Uno Skia Desktop](https://platform.uno/docs/articles/features/using-skia-desktop.html)
- [Uno SDK configuration](https://platform.uno/docs/articles/features/using-the-uno-sdk.html)
- [SVG 1.1 specification](https://www.w3.org/TR/SVG11/)
