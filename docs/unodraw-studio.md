# UnoDraw studio workspace

UnoDraw's studio shell is a dense, canvas-first editing workspace built with reusable Uno controls. SVG remains the source document, Svg.Skia remains the rendering engine, and the main canvas continues to draw retained pictures directly through Uno's compositor. Panel movement, tab selection, rulers and camera navigation do not become SVG edits.

## Workspace structure

The shell separates four concerns:

- A compact application menu and workspace selector, followed by a context control bar for selection, fill, stroke, weight, opacity and frequently used panel commands.
- A grouped drawing-tool rail with vector artwork for selection, direct selection, pen, pencil, shapes, text and images. Related tools are available through a right-click or long-press menu. Fill and stroke use overlapping compact paint selectors; `X` swaps them and `D` restores default paints when the canvas owns keyboard focus.
- Document tabs and a canvas bordered by adaptive rulers. Zoom and coordinate feedback live in the status bar rather than covering the artwork.
- Retained tabbed panel groups on the left, upper right, lower right and below the canvas, plus floating panels inside the workspace and a compact panel-launcher rail.

Use **Window** or command search to open panels, choose a preset, reset the workspace, hide/restore panels or enlarge tool targets. Essentials puts Properties above Layers on the right. Drawing moves Layers/Resources to the left and exposes geometry operations on the right. Typography prioritizes character controls and resources. The selector recognizes a preset's panel arrangement; changed arrangements are shown as a custom workspace.

## Retained panel workflows

| Panel | Editing role |
|---|---|
| Properties | Contextual document/object summary, transforms, appearance and quick actions, with collapsible sections. |
| Transform | Nine-point reference selector, world-space X/Y, width/height, constrained proportions, relative rotation and flips. |
| Appearance | Mixed-selection fill/stroke chips, opacity, paint swapping/defaults, and entry points for paints, filters and masks. |
| Stroke | Weight, cap, join, miter limit, dash pattern and existing width-profile/brush authoring. |
| Align | Selection-relative alignment, center distribution and stacking commands. |
| Geometry | Boolean shape operations, Divide, path conversion, offset, simplification, blend and node actions. |
| Layers | Searchable hierarchy with direct visibility and lock controls, type badges, selection, reordering and SVG ID renaming. |
| Resources | Existing gradients, patterns, symbols and other document resources, with paint-library commands. |
| Typography | Character properties and safe plain-text editing, plus rich text and paragraph/linked-frame workflows. |
| Effects | Filter Studio, shadow, filter removal, timeline, playback and frame baking. |
| SVG attributes | Native/custom attributes and source editing for features not represented by dedicated fields. |

Panels keep their actual view instances when switching tabs or docking. Their text fields, scroll positions and expanded content are not replaced with screenshot facsimiles. Contextual Properties still rebinds when the selected object or document changes; camera/theme/layout-only changes do not reconstruct it.

The advanced gradient, mesh, brush, rich-text, linked-frame, Filter Studio, timeline and print tools retain their existing transactional dialogs. Panel buttons open those workflows; this is not a claim that every advanced editor has been converted into an inline dockable surface.

## Docking and responsive behavior

Drag a panel tab between groups, reorder tabs, or use its context menu to dock left, upper/lower right, below the canvas, float or close. Dragging a group header far enough detaches its active panel into the workspace. Floating headers move the panel, and the lower-right grip resizes it. Dock splitters also support keyboard arrows.

Tabs use neutral studio chrome with an accent selection line. Left/Right and Home/End navigate focused tab groups, and the active tab is revealed when it would otherwise be outside the horizontal tab viewport. Explicit navigation commits pending editor fields first; invalid input prevents the transition rather than silently disappearing.

At narrow widths, the panel rail opens a nonmodal overlay without destroying the saved dock arrangement. Escape dismisses that temporary panel. Preferred floating bounds are projected into the available viewport for display, without overwriting the desktop size when the window temporarily shrinks. Explicit user drags/resizes update the preferred bounds.

Floating panels are **in-workspace overlays**, not independent operating-system windows. They do not support cross-monitor detached windows. The current layout has four named dock groups rather than an arbitrary recursive split tree. Dock-menu actions provide a keyboard alternative to pointer dragging.

## Custom controls

The reusable Uno library contains:

- `StudioIconButton`, `StudioToolButton` and `StudioTabButton`: studio templates, visual states, automation names and system focus visuals.
- `StudioToolIcon`: resolution-independent tool geometry rather than glyph-font substitutions for drawing tools.
- `StudioSection`: collapsible inspector sections that retain live content.
- `StudioNumericField`: compact labeled numeric editing, expressions through Uno's NumberBox, mixed values, programmatic synchronization guards and target validation.
- `StudioTextField`: explicit text commits, validation retention and Escape cancellation.
- `StudioPaintChip`: solid, transparent, none, linked and mixed paint states; compact and labeled presentations.
- `StudioReferencePoint`: a nine-point transform anchor selector.
- `StudioWrapPanel`, `StudioSplitter` and `StudioDockGroup`: responsive tool/action arrangement, pointer/keyboard resizing and retained panel hosting.
- `StudioRuler`: direct Skia ruler rendering, independent of SVG document serialization.
- `StudioLayerRow`: visibility/lock controls, element-type badge, ellipsized identity and SVG ID rename entry points.

`Themes/Generic.xaml` supplies light, dark and high-contrast resources and templates. The custom control layer reuses native Uno text, numeric, menu and focus behavior where appropriate. It does not replace text input, accessibility or keyboard semantics with a painted-only simulation. Larger tool targets are an explicit density option; this option does not enlarge every editor field or certify the complete workspace for every touch device.

## Editing correctness

### Nonmodal field targets

`SvgEditorSelectionContext` captures the controller, document identity and ordered selected object references. Geometry or style changes on those same objects remain valid. Document reload, undo that replaces the DOM, another controller or selection replacement invalidates the context. Text and numeric fields use this context so a delayed edit cannot be applied to a different target.

Programmatic numeric synchronization does not emit an edit. Focused numeric text is not overwritten by ordinary model refresh. Mixed paints and numeric values remain visibly mixed until the user supplies a value. A failed text commit retains the text for correction or Escape cancellation. Commands, document switches, selection changes and panel transitions flush pending fields through the existing editor boundary.

Unmodified canvas shortcuts are scoped to canvas/workspace focus. Arrow keys, Enter, Space and Tab remain native navigation/activation keys while panel widgets own focus. Application-level commands such as Save and command search remain available; text editing keeps its own clipboard, selection and undo keys. Temporary Space panning restores the prior persistent hand-tool state.

### Layer-row actions

`SetHierarchyVisibility` and `SetHierarchyLocked` target the requested object without replacing the current selection. They validate document ownership before mutation and use the controller's undo transaction. Repeated state assignments are no-ops. A lock inherited from a parent must be removed on that parent; a child row cannot silently unlock an entire layer.

Renaming edits the SVG ID, not a separate display-name field. The existing rename service validates XML names and uniqueness and updates document-local references in one edit. Imported style rules and external references retain the existing SVG authoring boundaries; visibility/paint commands are not a general CSS appearance-flattening system.

## Layout persistence and host API

`SvgWorkspaceLayout` is a UI-only, versioned model with bounded XML parsing. It stores panel placements/order, effective active tabs, dock extents, split ratio, floating preferences, ruler visibility and density. Unknown panel IDs or unsupported settings versions do not create arbitrary UI. DTDs, oversized settings and nonfinite dimensions are rejected or safely bounded as documented by the model tests.

The workspace saves local layout under `UnoDraw.Studio.Layout.v1`. Hosts can also capture and restore it independently from SVG save files:

```csharp
string layoutXml = workspace.CaptureWorkspaceLayout();
workspace.RestoreWorkspaceLayout(layoutXml);
```

Call these APIs on the UI thread. Restoring layout commits pending fields first and does not push document history. The current UI provides three presets and one locally persisted custom arrangement; named multi-workspace libraries and layout file-pickers are not implemented.

## Validation

The editor tests include workspace/preset/order/round-trip checks, ruler invariants, selection-context invalidation, non-mutating floating projections and direct layer-state transactions. The native studio workflow builds the real UnoDraw application and runs it under Xvfb with software graphics.

`tests/UnoDraw.StudioSmoke/smoke.sh` opens a disposable copy of a deterministic editable SVG, waits for its document title, and drives the production keyboard/menu/save paths. In addition to retaining screenshots, it checks written SVG for numeric opacity changes, default/swapped paints, undo restoration and the absence of document changes from workspace operations. It uses no special application test mode. The actual outcome must be read from the workflow run for the exact commit; test source alone is not evidence of success.

```sh
git submodule update --init --recursive
dotnet build samples/UnoDraw/UnoDraw.csproj -f net10.0-desktop -c Release
dotnet test tests/Svg.Editor.Skia.UnitTests/Svg.Editor.Skia.UnitTests.csproj -c Release

xvfb-run --auto-servernum --server-args='-screen 0 1600x1100x24 -nolisten tcp' \
  dbus-run-session -- bash tests/UnoDraw.StudioSmoke/smoke.sh \
  samples/UnoDraw/bin/Release/net10.0-desktop/UnoDraw.dll artifacts/unodraw-studio
```

Native smoke screenshots require visual inspection; the script is not a pixel-parity oracle. Software rendering does not establish physical-GPU frame rates, mobile device usability, screen-reader certification or optimized-browser runtime behavior. Those results are tracked separately in the PR and platform workflows.

## Remaining workspace work

Remaining areas include arbitrary nested docking, detached native panel windows, named workspace management, fully inline advanced authoring surfaces, richer resource/brush previews, broader touch and assistive-technology verification, and comprehensive large-document interaction benchmarks. The broader vector-editor product boundaries in [effects and responsive operations](unodraw-effects-and-operations.md) and [the main guide](unodraw.md) continue to apply.
