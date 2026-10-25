# UnoDraw inline paint and arrangement workflows

The October 25, 2026 continuation adds a retained gradient editor to **Appearance** and replaces the selection-only controls in **Align** with explicit reference and spacing controls. Both workflows use the existing Uno workspace, Svg.Skia renderer, SVG document and transaction history. They do not introduce a raster document or an additional scene model.

## Gradient editor

Select artwork and choose **Object > Gradient editor**, **Window > Gradient editor**, or search for **Gradient editor** with Ctrl+K. The existing Gradient buttons in the properties/paint panels now open this retained editor instead of a modal list of stop values. The advanced text-based creator remains available as **Create gradient from stop values…**.

The editor presents fill/stroke targeting, a stop ramp, a selected-stop list, precise position and opacity fields, native color/channel/hex controls, geometry and spread controls, optional artwork preview, and separate draft history.

### Direct and precise stop editing

Click the ramp to insert a stop sampled from its neighboring colors. Drag a stop handle to reposition it. The selected-stop list also makes coincident handles reachable without dragging another stop out of the way. The position field accepts exact percentages. Stop opacity remains separate from color alpha, fill/stroke opacity and object opacity.

A focused stop supports Left/Right for one percentage point, Shift+Left/Right for ten points, Home/End for the endpoints, and Delete to remove it. Escape cancels an active drag. At least two stops remain. **Duplicate**, **Reverse** and **Even spacing** preserve independently editable stops; equal-offset stops retain their order to support deliberate hard color transitions.

Each stop has a stable identity independent of its array position. Moving it through other stops does not transfer keyboard focus or pointer capture to a different handle. Ramp snapshots are immutable, and draft history is bounded to 64 steps. A handle drag is one draft edit rather than one history entry per pointer event.

### Apply versus document history

Stop/color/geometry changes affect only a detached draft. **Apply gradient** creates one undoable SVG edit. **Reset draft** abandons the draft and reloads the current selection. **New gradient** explicitly starts a static replacement rather than trying to reinterpret unsupported imported content. A dirty fill draft cannot silently turn into a stroke draft when the target selector is changed.

The command palette also exposes Apply, Reverse, Undo, Redo and Reset gradient-draft actions. Ctrl+Enter applies when keyboard focus is inside the gradient editor. Draft Undo/Redo does not operate on document history; the ordinary Undo command reverses an applied SVG edit.

A draft captures its controller, document identity, document revision and ordered selection. If any target changes before Apply, the draft becomes stale and is not written. Reset is required to adopt the new target. This avoids applying a delayed panel edit to the wrong artwork.

### Existing gradients and shared definitions

Opening a local linear or radial gradient reads its computed stop colors and opacity, including inherited stops through local gradient references. Decreasing imported offsets follow SVG's monotone offset rule instead of reordering the imported colors. Existing coordinate units, geometry, transforms and reference inheritance are retained by default.

Choosing **Linear · object bounds** or **Radial · object bounds** explicitly replaces that imported geometry with normalized object-bounds geometry. Linear gradients expose an angle. Radial replacement uses centered geometry; arbitrary imported radial coordinates remain preserved under **Keep imported geometry**. Pad, Reflect and Repeat are available.

Apply creates an isolated resource copy by default, with collision-free document IDs and retained stop/gradient metadata. Other consumers keep their original shared definition. The **Edit the shared definition** option retains resource identity and rejects locked consumers, including consumers reached through gradient inheritance. A shared edit that would introduce a document-wide ID collision is rejected rather than silently stealing that ID; isolated editing remains available. This includes inherited named stop subtrees whose original IDs belong to another template.

Inline editing does not silently remove SVG gradient animation: an animated gradient reports that it needs timeline/source editing or an explicit new static gradient. External gradient references are not fetched or flattened by this authoring workflow. Structural or ID-dependent CSS can change meaning when a resource is copied, so copy-on-write is not a promise of general CSS appearance flattening.

### Preview and performance

The stop bar uses a lightweight native gradient brush for immediate feedback. Inserting an interpolated stop samples unassociated sRGB channels and separate opacity; this is not a color-managed or linearRGB-exact insertion algorithm. Imported color-interpolation metadata is retained in the actual SVG.

Enable **Preview on artwork** to see the draft through a separately owned Svg.Skia renderer on `SKCanvasElement`. This optional full-document preview is debounced by 180 milliseconds, uses the current animation time, and leaves the live retained picture, selection, dirty state and undo history unchanged. Each replacement disposes the previous preview renderer, and unloading clears it. A preview rebuild is synchronous within its update; debounce is not a hard execution-time bound on complex SVGs.

The ramp supports 2–256 stops. Gradient inheritance is limited to 32 definitions and rejects cycles. Position, angle, opacity and resulting arrangement coordinates are validated for finiteness. These bounds are authoring safeguards, not an untrusted-content resource sandbox.

## Align and spacing

Open **Window > Align panel**, the panel rail's Align button, or the context bar's Align action. The reference selector explicitly distinguishes:

- **Selection bounds:** align selected objects to their collective bounds.
- **Artboard:** align to the rendered SVG artboard in picture coordinates. A single object is supported.
- **Key object:** choose a top-level selected object that remains fixed; other selected objects align to it.

The labels and availability explain which operations need two or three objects. Unavailable actions are disabled instead of producing empty edits. Already arranged objects report that no undo entry was added.

**Distribute centers** equalizes center positions while preserving the two outer centers. **Space horizontally/vertically** equalizes edge-to-edge gaps, accounting for each object's width or height. Automatic gap calculation uses the span between the first and last objects in stable start-coordinate order; it is not an overlap solver for nested/contained objects. The two ordered endpoints remain fixed.

Enable **Use an exact gap** to enter a canvas-unit distance. The first spatial object anchors the arrangement unless a key object is selected. Key-object spacing works on both sides of that fixed key. Negative gaps deliberately overlap objects. Spacing always acts on the selected objects; choosing Artboard as the alignment reference does not stretch spacing across the page.

**Center selection on artboard** is a separate command-palette/menu action. It moves the selection as one arrangement, preserving relative positions, rather than moving every object onto the same center. It creates one undo step.

### Transform and transaction guarantees

Arrangement planning is a framework-independent pure service returning one world-space translation per input bound. Sorting is stable, input bounds are not mutated, selected descendants are excluded when their selected ancestor already moves them, and parent transforms are accounted for.

The controller validates every target and local transform before mutating any object. A singular parent or invalid key cannot partially move an earlier object. A zero-translation operation leaves the retained picture, document history and redo stack intact. Successful operations preserve selection and commit once through the existing transaction mechanism.

Bounds come from retained Svg.Skia scene nodes. The feature does not introduce an independent geometric-versus-visible-bounds selector, an arbitrary page/artboard manager, or support for moving locked artwork.

## Reusable components

| Component | Role |
|---|---|
| `SvgGradientRamp` / `SvgGradientColorStop` | Immutable bounded stop editing, identities and sampling |
| `SvgGradientEdit` | Detached target/geometry/resource draft |
| `SvgEditorController.GradientEditing` | SVG resource application, preview and stale-target protection |
| `StudioGradientStopBar` | Retained pointer/keyboard stop interaction |
| `StudioGradientEditor` | Nonmodal paint editing and draft history |
| `StudioSvgPreview` | Owned retained-SVG preview surface |
| `SvgArrangementService` | Pure alignment/center/gap planning |
| `SvgEditorController.Arrangement` | Validated world-to-parent application |
| `StudioArrangeEditor` | Selection-aware reference and exact-spacing UI |

The previous Color mixer, Layers, Transform, Geometry, Effects, typography, saved workspaces and docking controls remain available. Advanced operations are progressively disclosed in their panels rather than replacing the canvas with a new modal workflow.

## Validation

The regression suites are `SvgGradientRampTests`, `SvgGradientEditingTests` and `SvgArrangementTests`. They cover geometry/resource preservation, immediate rendering, save/reopen, undo, independent alpha/opacity, shared-resource locks, stale drafts, world-space arrangement, key anchors, no-op behavior and atomic failures.

`tests/UnoDraw.StudioSmoke/advanced.sh` runs the existing native Studio suite first, then exercises the production gradient command/panel, draft reverse/undo/redo, saved SVG stops and assignments, document undo, the Align panel, equal-gap arrangement and a compact gradient panel. It keeps screenshots and saved source alongside assertion records. Its workflow syntax-checks both scripts and still runs the existing SVG-assertion tests.

CI pass/fail records must be associated with their exact commit. The presence of a test or workflow is not a validation result. Native Xvfb captures use software rendering; they are not physical GPU benchmarks. Browser/mobile packages and device interaction require their own validation. Remaining broader product work includes additional inline studios, on-canvas gradient handles, richer resource previews, recursive/native-window docking, advanced typography/print boundaries and systematic accessibility/device testing.
