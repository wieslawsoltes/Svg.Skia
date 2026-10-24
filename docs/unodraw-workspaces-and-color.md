# Saved workspaces and inline color editing

This continuation builds on the retained [Studio UI](unodraw-studio.md). It does not replace the SVG document model, Svg.Skia renderer, custom dock controls or existing advanced authoring studios.

## Named workspace workflow

Arrange panels, choose their active tabs and resize the dock groups. Run **Window > Save workspace as…** and enter a name. The snapshot appears beside Essentials, Drawing and Typography in the workspace picker. Right-click the picker for library actions; the same actions are available in the Window menu and command search on compact layouts.

A saved workspace captures panel placement/order, active dock tabs, preferred floating bounds, dock widths, bottom-panel height, right-side split, tool density and ruler visibility. It does not capture SVG artwork, document selection, camera position, application theme, undo history, or temporary focus-canvas/panel-hidden state. The last live layout remains stored separately from saved snapshots.

Editing a saved layout adds `*` beside its name. It does not overwrite the saved snapshot. **Restore saved workspace** restores that snapshot. **Manage saved workspaces…** supports adding the current arrangement, explicit snapshot replacement, rename and confirmed removal. Management is a detached draft: **Save library** persists it; **Cancel** abandons it. A checkbox optionally restores the selected snapshot after saving.

The library uses stable identities, Unicode-normalized case-insensitive names, and detached layout XML. Renaming does not change a snapshot or its identity. Each restore produces a fresh layout object. There are at most 32 named workspaces; names are limited to 64 UTF-16 code units. The XML budget is 524,288 characters, with each layout limited to 32,768 characters. DTDs, external entities, duplicate identities/names and unsupported library/snapshot versions are rejected.

**Import workspace library…** reads portable `.unoworkspaces` XML. Import is additive: it assigns new identities and adds numeric suffixes for name collisions. Existing snapshots are never replaced by an imported identity or name. The complete import is validated before it can be saved. **Export workspace library…** exports the named library, not the current unsaved arrangement or the SVG document.

### Persistence and integration

The library is stored as `UnoDraw/Workspaces.xml` under the same application data root used for recovery. Browser/mobile storage waits for the existing platform-storage initialization. Picker output goes through the existing provider-aware file-writing path.

Writes use a unique temporary file and same-directory replacement. An in-process semaphore serializes workspace windows. Transactions verify both their captured library instance and the disk source before replacement; stale transactions are rejected instead of silently overwriting newer data. This is optimistic conflict detection, not a filesystem lock against every possible external process. Existing malformed files are reported rather than silently replaced.

The active named identity is a small independent preference. Library contents are not stored in a large LocalSettings value. A failed persistence operation does not adopt its candidate library in memory. Switching built-in presets does not relabel them as saved workspaces merely because their layouts happen to match.

`SvgWorkspaceLibrary` and `SvgNamedWorkspace` are UI-independent reusable APIs:

```csharp
var library = new SvgWorkspaceLibrary();
var current = SvgWorkspaceLayout.Parse(workspace.CaptureWorkspaceLayout());
library = library.Add("Drawing desk", current);

var saved = library.Entries[0];
var detached = saved.Restore();
detached.Move("layers", SvgPanelDock.Left);
library = library.Update(saved.Id, detached);

var portableXml = library.Serialize();
var imported = library.Import(portableXml); // additive copies, never overwrites
workspace.RestoreWorkspaceLayout(saved.LayoutXml);
```

Panel activation also avoids rebuilding/reparenting all dock groups when the requested panel already belongs to its live group. Opening another tab preserves the other live panels and their control instances. Layout changes and compact overlay transitions still rebuild the affected workspace arrangement through the existing host.

## Inline Color mixer

Open **Window > Color mixer**, or expand **Color mixer** inside Appearance. The reusable `StudioColorEditor` combines Uno's color spectrum, channel/hexadecimal inputs, alpha controls, a draft paint chip, fill/stroke targeting, Apply, Reset and No paint actions. It remains beside the canvas rather than opening a modal color dialog.

Browsing colors changes only the draft preview. **Apply color** commits through the existing fill/stroke command, producing one undoable edit rather than a history item for every spectrum movement. **No paint** explicitly sets the chosen paint to `none`. **Reset draft** adopts the current selection's paint again. Switching the fill/stroke target starts a new draft for that target.

Mixed paints and linked/non-solid resources are called out. Applying a solid color deliberately replaces the selected paint assignments; merely opening the mixer does not rewrite a gradient/pattern or its resource definition. Color alpha is independent of existing `fill-opacity`/`stroke-opacity` and object opacity. Those opacities remain in place and combine during rendering.

A draft captures controller, document, ordered selected objects and document revision. A different selection, document reload, intervening document edit or previous application invalidates it. A stale draft remains visible but cannot write to a new target; Reset establishes a new target. Lock validation is repeated before application. The control detaches controller notifications when unloaded and exposes explicit disposal for reuse by other hosts.

```csharp
var draft = new SvgSolidPaintDraft(controller, stroke: false);
// Changing a host's spectrum or draft preview does not touch the controller.
if (draft.IsCurrent(controller))
    draft.Apply(controller, new SkiaSharp.SKColor(17, 83, 211, 128));
```

This is solid-paint editing, not an inline gradient-stop editor, a color-managed soft-proofing system or a replacement for shared swatch/ink authoring. Existing Gradient, Pattern, Swatches and Print studios retain their own workflows.

## Regression coverage

`SvgWorkspaceLibraryTests` adds 16 cases for immutable snapshots, rename/update identity, culture-invariant round trips, Unicode/case collisions, bounded additive imports, size/version/entity rejection and document-history independence.

`SvgSolidPaintDraftTests` adds nine cases for non-mutating draft creation, multi-object single-step undo, stale targets, lock checks, linked paint preservation until Apply, stroke-only/no-paint changes, culture-independent color formatting and repeated SVG reopening with independent color/paint/object opacity.

The native Studio smoke now checks document readiness through a successful production Save of the intended fixture, rather than relying solely on X11 title updates. It also saves a named layout, checks its persisted XML, verifies that restoring it does not rewrite its snapshot, opens the inline mixer and verifies that opening it does not change the SVG. Captures cover the mixer, named layout and existing wide/compact panel workflows.

The smoke's fixed screen coordinates are appropriate only for its controlled 1440×960 Xvfb configuration. Color application semantics are tested by the controller suite; the smoke does not yet automate every spectrum/channel gesture or every library import/export picker. Filesystem replacement and native picker behavior still require platform-specific runtime checks.

```sh
dotnet test tests/Svg.Editor.Skia.UnitTests/Svg.Editor.Skia.UnitTests.csproj -c Release
# The UnoDraw Studio UI workflow builds the actual native application and records screenshots.
```

CI outcomes must be associated with the exact commit. Added tests and a successful compile alone are not a runtime or visual-layout pass. Software Xvfb/WebGL checks do not establish physical GPU performance, mobile-device behavior or complete workflow parity.
