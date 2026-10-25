# UnoDraw path division

UnoDraw exposes **Path > Divide** through the desktop menu, compact menu and command search. Select at least two sibling paths or basic shapes. The command replaces them with independently editable SVG paths in one undoable transaction.

This builds on the existing Uno Platform workspace and retained Svg.Skia canvas. It does not introduce another document model, a raster editing surface, or a dependency on Avalonia in the Uno workspace.

## Geometry and ordering

`SvgPathDivisionService.Divide` accepts Skia paths in a common coordinate space, ordered back to front. It incrementally partitions the union of their filled outlines. Existing cells are split into intersection and difference; the uncovered portion of the incoming operand becomes another cell. Every result cell records the frontmost input that covers it. Choosing objects in a different selection order therefore does not change the result.

The engine respects winding and even-odd fills, retains holes and self-intersection semantics through native path normalization, and does not polygonize curves. A disconnected portion of one membership cell remains part of the same compound path; this command is not a connected-contour explosion tool. Touching boundaries do not create zero-area fragments.

The controller obtains world transforms from retained scene nodes, performs the arrangement in that common space, then maps each cell back into its owning source's local coordinates. Each source's outputs replace it at its own sibling position. Unselected siblings are not moved. A source's first surviving piece retains its ID; additional pieces receive unique IDs. Fully occluded sources have no surviving piece and are removed.

## Editing semantics and explicit boundaries

Divide is a **filled-geometry operation**, not rendered-appearance flattening. It copies the frontmost covering source's presentation onto a cell. Consequently:

- A stroke is applied to the newly created edges. Translucent overlap is not baked into an opaque composite.
- Object-bounding-box gradients and patterns are evaluated against each new path's bounds; this is not a promise of appearance-preserving gradient subdivision.
- Only sibling `path`, `rect`, `circle`, `ellipse`, `polygon` and `polyline` objects are accepted. Move objects into one layer/group or convert other object types first.
- Per-object clips, masks and filters, and objects containing child elements, are rejected before document mutation. This includes animated shapes and shapes carrying child metadata; make a plain static path copy rather than silently discarding those children.
- Basic-shape coordinates must use SVG user units or pixels. Nonzero percentage, physical and font-relative geometry units must first be resolved. Singular transforms and non-finite coordinates are rejected.

The low-level geometry service is reusable independently of these editor document restrictions. It rejects inverse fill types because their unbounded exterior cannot be represented by a finite SVG arrangement.

## Transactions, cancellation and resource ownership

All validation, path operations and output-path serialization complete before the live document is changed. A cancelled operation, an unsupported operand or a piece-budget failure leaves the document, selection, renderer revision and undo history untouched. A successful division enters the existing editor transaction mechanism once.

The service defaults to a maximum of **4,096 cells**, with at most **128 operands**. The API can choose a smaller piece budget. These limits bound arrangement growth, not the complexity or execution time of one native path operation. Cancellation is checked between native operations; an individual native call cannot be interrupted. The current menu command executes synchronously; worker-thread scheduling and interactive progress/cancellation UI are not implemented by this change.

`SvgPathDivisionResult` owns its `SKPath` instances and must be disposed. Inputs are borrowed and are never mutated or disposed. Copy a result path before retaining it beyond the result's lifetime. Temporary native paths are released on success, cancellation and failure.

## Weighted-conic serialization

The shared `PathService.ToSvgPathData` writer previously emitted a weighted conic as an ordinary quadratic `Q` command. A rational conic generally cannot be represented by the same three points with unit weight. This changed the curvature of circles, elliptical arcs and some native path-operation results.

Paths containing conics now use Skia's SVG writer, which respects their weights and approximates them with ordinary quadratic segments (the native writer uses a 1/1024-unit subdivision tolerance). Paths without conics keep their existing textual representation. SVG cannot store a general rational-quadratic verb directly, so this is a controlled vector approximation, not an exact rational encoding or raster conversion.

## Regression coverage

The new test files contain 33 test cases covering the geometry engine, controller integration and conic serialization:

- `SvgPathDivisionServiceTests`: overlap partitioning and ownership, containment, holes, three operands, coincident/touching paths, native curves, empty paths, budget failure, cancellation, invalid inputs and disposal.
- `SvgEditorDivisionTests`: paint-order independence, source/parent transforms, stacking among unselected siblings, unique IDs, atomic history, untouched state after rejected edits, and repeated SVG reopening.
- `PathServiceConicSerializationTests`: small/large circles, non-unit weights, rotated ellipses, culture independence, non-conic text stability and circle-to-path save/reopen behavior.

Run these together with the existing editor suite:

```sh
dotnet test tests/Svg.Editor.Skia.UnitTests/Svg.Editor.Skia.UnitTests.csproj -c Release
```

The UnoDraw workflow also builds the application and exercises editor/document suites on supported desktop runners. CI results must be associated with their exact commit; these test descriptions alone are not a statement that a run passed. The current change does not establish physical GPU performance, browser runtime behavior or mobile-device runtime compatibility.
