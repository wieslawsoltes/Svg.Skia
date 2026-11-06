# UnoDraw effects and responsive geometry operations

This guide describes the October 24, 2026 continuation after `f3b7c43`. It supersedes the older statements that Filter Studio and a cancellable Divide interface are not implemented. The [main UnoDraw guide](unodraw.md) remains the reference for the earlier workspace, typography, animation, brush/mesh and print workflows.

## Filter Studio

Select artwork and open **Object > Filter Studio…** or search for **Filter Studio** in the command palette. The same command is available in the compact menu.

The studio displays the filter's ordered primitive graph and dependency wires. Select a node to edit its `in`, `in2`, `result`, filter region, color-interpolation space and primitive-specific parameters. Inputs can target a standard source or a preceding named result. Add, duplicate, reorder or remove nodes; removal can explicitly bypass the node and reconnect its consumers. Reordering that introduces a forward reference is rejected without changing the draft.

All 17 Filter Effects Level 1 primitive kinds have authoring metadata: blend, color matrix, component transfer, composite, convolution, diffuse/specular lighting, displacement, drop shadow, flood, Gaussian blur, image, merge, morphology, offset, tile and turbulence. Nested component-transfer channels, merge inputs and distant/point/spot lights are editable. These controls expose the existing Svg.Skia renderer; a catalog entry is not a new claim that every possible combination or extension renders identically to every browser.

### Drafts and preview

The dialog owns a detached XML graph. Its live preview uses a separately owned Svg.Skia renderer on Uno's `SKCanvasElement`, with the original document base URI and current animation time. Preview is debounced by 250 milliseconds. Live preview can be paused for an expensive graph. It does not change the live document, retained picture, selection, dirty state or undo history.

Draft undo/redo is separate from document history and bounded by snapshot count and memory. Apply validates and commits the complete result as one document edit. Cancel simply abandons the draft. A draft cannot overwrite a document or selection that changed while it was open.

The preview renders a snapshot at the current animation time rather than playing an independent timeline. Rendering remains synchronous within a preview update; debounce is not a hard execution-time bound for a large SVG or expensive filter.

### Shared definitions and source preservation

Editing an existing filter creates an isolated copy by default. An explicit checkbox instead edits its shared definition. Shared editing rejects locked consumers and rejects new IDs that collide with unrelated artwork. Isolated copies allocate unique XML IDs and rewrite internal fragment and animation timing references. Result labels are handled separately from document-wide XML IDs.

Imported graphs retain unknown attributes, namespaces, comments, child metadata and SVG animation elements. The XML expander supports direct source editing. Import/export uses portable `.svgfilter` XML files through platform file pickers and the application's provider-aware save path. Unknown extensions remain editable through source even when they have no dedicated field.

Graph opening does not normalize the imported source. Structural edits explicitly name implicit dependencies and preserve nearest-preceding bindings when result labels are repeated. Numeric validation applies only to recognized filter parameter fields, not to animation `values` lists or unrelated metadata. Document application is reparsed before adoption so newly created resources render immediately rather than only after saving and reopening.

**Object > Remove filters** explicitly overrides stylesheet and inline filters with `none`, while retaining definitions that other objects may still use. Unrelated inline declarations, quoted strings and data URLs are preserved.

### Bounds and limitations

Drafts support at most 128 primitive nodes, 1,024 descendant elements and 1 MiB of XML. Convolution authoring bounds each order dimension to 1–63 and requires matching kernel length; turbulence authoring bounds octaves to 1–16. These are authoring safeguards, not a complete resource sandbox or an exact restatement of all permissive SVG fallback rules. Invalid or forward inputs are reported rather than silently substituted.

Graph connections are edited through input selectors; direct cable-drag gestures and free-position node layout are not implemented. External filter URLs and CSS filter-function chains are not expanded into local editable primitive graphs. Choosing a new graph replaces the target's filter assignment. CSS selectors dependent on resource IDs or structural ancestry can change meaning when copying/reparenting resources; this is not general CSS appearance flattening.

## Alpha and luminance masks

Select the artwork first and its mask artwork second, within the same layer/group. Choose **Object > Create alpha mask** or **Create luminance mask**.

Mask construction keeps the original artwork and mask content as editable SVG. A neutral wrapper places them in the same parent coordinate system; neither geometry nor transforms are baked into pixels. The mask content stays under a local resource definition with its original IDs. The operation is undoable as one edit and immediately selects the masked wrapper.

**Release mask and restore artwork** disables the mask and places an editable copy of the retained mask artwork inside the wrapper, preserving the source's original front/back relationship to the artwork. The wrapper stays in place so transformations or compositing added after mask creation are not discarded. Restored copies receive unique IDs and remapped internal references, including when several selected wrappers share the same resource.

**Disable mask** removes only the selected mask assignment without placing a visible copy of the mask artwork. Resource definitions remain available to other consumers. Imported masks without UnoDraw authoring metadata are detached without guessing how to restore their geometry. A generated resource whose content coordinate system was changed from `userSpaceOnUse` must be disabled without restoration or converted back before restoration.

Mask regions initially use the standard -10% / 120% object-bounding-box expansion. Arbitrary mask-region editing remains available through native/source attributes. As with grouping generally, inserting or removing structural ancestry can affect imported CSS selectors; the command does not flatten all possible stylesheet relationships.

## Responsive Divide

**Path > Divide…** now opens a progress dialog instead of running the arrangement directly inside command dispatch. Desktop/native hosts calculate on a worker; a single-threaded browser uses cooperative slices. The UI reports completed operands and region counts. Cancel leaves the document unchanged; Apply commits the prepared result as one undoable edit.

The controller captures geometry, world matrices, document identity, XML and selection before calculation. Computation reads only owned native geometry snapshots. Native paths are disposed on success, cancellation, limit failures and throwing progress observers. A successful preparation returns managed path data, not native handles that callers must dispose.

Before Apply, the controller checks that the original document identity, source and selection still match. Reloading even identical XML, switching selection, editing the document or attempting to apply the result twice causes a stale-result error rather than overwriting newer work.

The synchronous `Divide` API and asynchronous API share the same incremental arrangement engine. Cooperative slices yield through a timer after approximately eight milliseconds of accumulated work; cancellation is checked between cells and operands. A single running native path operation cannot be interrupted, so eight milliseconds is not a guaranteed maximum pause. Final DOM cloning and renderer adoption still occur on the editor thread.

Default bounds remain 4,096 pieces and at most 128 operands; asynchronous serialization additionally limits generated path data to 16 MiB. Existing [Divide geometry semantics](unodraw-path-division.md) still apply: this is a filled-outline operation, not appearance flattening, and disconnected regions with identical membership remain compound paths. The current controller retains its sibling/static/resolved-unit operand restrictions.

### Reusable API

```csharp
// Capture on the editor thread. Native hosts normally use the worker path.
var prepared = await controller.PrepareDivisionAsync(
    cancellationToken: cancellationToken,
    progress: progress,
    useWorkerThread: !OperatingSystem.IsBrowser());

// Back on the editor thread, after explicit user acceptance:
controller.ApplyPreparedDivision(prepared);

// Detached filter editing:
var edit = controller.BeginFilterEdit();
edit.Graph.Add("feGaussianBlur");
using (var preview = controller.CreateFilterPreviewRenderer(edit))
{
    // Draw with the host's Skia canvas. Preview does not mutate the controller.
}
controller.ApplyFilterEdit(edit); // isolated definition by default
```

## Regression suites and validation scope

The continuation adds `SvgFilterGraphTests`, `SvgEditorFilterTests`, `SvgFilterResourceIsolationTests`, `SvgPreparedDivisionTests`, `SvgEditorMaskTests` and `SvgEditorEffectRegressionTests`. They cover graph edges and mutation atomicity, nested authoring fields, source preservation, immediately rendered effects, CSS overrides, resource isolation, animation references, masks, cancellation, stale results, native ownership and save/reopen/undo behavior.

```sh
git submodule update --init --recursive
dotnet build samples/UnoDraw/UnoDraw.csproj -f net10.0-desktop -c Release
dotnet test tests/Svg.Editor.Skia.UnitTests/Svg.Editor.Skia.UnitTests.csproj -c Release
dotnet test tests/Svg.Editor.Svg.UnitTests/Svg.Editor.Svg.UnitTests.csproj -c Release
```

The PR records actual CI results by exact commit. A list of tests is not evidence that a particular run passed. The native startup check uses software graphics. This continuation does not establish physical GPU frame rates, interactive mobile-device behavior or runtime validation of the optimized WebAssembly package.

## Remaining broader product work

The entire vector-editor product goal is not complete. Separate remaining areas include vector CMYK/spot-color PDF output, PDF/X conformance, overprint/trapping workflows, non-rectangular linked text flow and dictionary hyphenation, broader Divide operand/paint preservation, direct graph wiring gestures, and systematic cross-device visual/input/performance validation. Existing RGB vector PDF and flattened ICC CMYK export are unchanged. No feature boundary is silently converted into a parity claim by the additions in this guide.
