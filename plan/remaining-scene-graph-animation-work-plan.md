# Remaining Scene Graph And Animation Work

## Current Summary

At this point, the core retained scene graph exists and the original six-phase animation plan is done. What is left is mostly removal of the remaining legacy seams and fallback paths.

**Scene Graph**
- Replace the last per-element drawable extraction bridge for unsupported visual/resource pipelines with direct retained display-list generation, as called out in [svg-retained-scene-graph-rewrite-spec.md](/Users/wieslawsoltes/GitHub/Svg.Skia/plan/svg-retained-scene-graph-rewrite-spec.md).
- Reduce load-time duplication where retained compilation still temporarily generates drawables only for compatibility payload extraction.
- Remove the remaining compatibility proxies at public/editor boundaries.
- Migrate the last editor commands that still lean on drawables:
  - selected-element export
  - align/distribute helpers
  - remaining path/shape manipulation helpers that still use drawable geometry/order
- After parity is proven on those edge cases, remove the older non-retained fallback paths entirely.

**Animation**
- The planned shared animation runtime is complete per [svg-interaction-animation-phased-implementation.md](/Users/wieslawsoltes/GitHub/Svg.Skia/plan/svg-interaction-animation-phased-implementation.md). What remains is follow-up work beyond that plan.
- Eliminate the remaining fallback full-frame rebuilds for non-drawable resource pipelines during animation:
  - gradients
  - clip-path resources
  - filter graphs
- Route those animation/resource changes fully through retained nodes/resources so animated frames do not fall back to the older drawable/picture rebuild path.
- Remove the transient animation-layer caching architecture once retained-scene mutations cover those cases well enough.
- Optional later step: map compatible SVG animation cases onto true host-native animation objects instead of only using host-native scheduling.

**Practical order**
1. Finish the remaining editor/public drawable-dependent commands.
2. Remove the last retained-compiler drawable bridges.
3. Move resource-driven animation updates fully onto retained node/resource mutations.
4. Delete the old fallback rebuild paths.
5. Only then consider deeper host-native animation object mapping.

So the short answer is: the remaining work is not “build a scene graph” anymore. It is “finish removing the old drawable-first assumptions everywhere they still leak through,” especially in resource-heavy animation and a few editor commands.

## Detailed Implementation Plan

### Phase 1: Editor And Public API Drawable-Seam Removal

Goals:
- make retained scene nodes the primary geometry/render source for editor commands
- keep drawable usage only as a temporary fallback
- preserve current user-visible behavior while removing drawable-first assumptions

Tasks:
1. Export selected element from retained scene state.
   - prefer `SvgSceneNode` plus `SKSvg.CreateRetainedSceneNodePicture(...)`
   - fall back to drawable snapshot only when no retained node is available
   - preserve current PNG export API and behavior
2. Move align/distribute helpers from drawable bounds to explicit retained bounds.
   - align/distribute should operate on `SKRect` bounds rather than `DrawableBase`
   - workspace should gather bounds from retained scene nodes first
3. Audit remaining editor commands that still depend on drawable geometry/order.
   - path/shape manipulation helpers
   - selection/export helpers
   - any command using `DrawableBase.TransformedBounds` directly
4. Add regression coverage for retained-scene-first editor operations.
   - export through retained node when drawable is absent
   - align/distribute translation updates driven by retained bounds

Exit criteria:
- editor export works with retained scene node only
- align/distribute no longer require drawable instances
- tests prove retained-scene-first behavior

### Phase 2: Final Retained Compiler Bridge Removal

Goals:
- eliminate remaining per-element drawable extraction during retained compilation
- reduce load duplication and stop using transient drawable generation for compatibility-only payload extraction

Tasks:
1. inventory remaining `DrawableBridge` compilation cases
2. replace each remaining bridge with direct retained display-list generation
3. move compatibility-only payload builders into retained compiler/runtime services
4. extend parity coverage for the previously bridged cases

Exit criteria:
- retained compiler does not require per-element drawable extraction for supported rendering/resource cases
- compilation strategy map shows only intentional temporary fallback categories, if any

### Phase 3: Retained Resource-Driven Animation Updates

Goals:
- animate resource-backed changes through retained nodes/resources directly
- stop falling back to full-frame drawable/picture rebuild for resource changes

Tasks:
1. route gradient, clip-path, and filter mutations through retained resource dependency updates
2. ensure animation frame deltas can invalidate and rebuild only affected retained resources/nodes
3. preserve hit testing and host extraction correctness under retained resource mutation
4. expand parity coverage for animated resource-backed documents

Exit criteria:
- animated resource changes use retained mutation routing instead of fallback full-frame rebuild

### Phase 4: Legacy Fallback Retirement

Goals:
- remove the older non-retained fallback render/update paths after retained parity is proven

Tasks:
1. delete now-redundant drawable-first fallback paths in shared rendering/update code
2. remove temporary compatibility proxies where downstream consumers have retained replacements
3. update docs/specs to mark retained graph as the only authoritative runtime path

Exit criteria:
- common render, interaction, and animation paths run entirely from retained scene state

### Phase 5: Optional Native Animation Object Mapping

Goals:
- explore mapping compatible retained SVG animation cases onto host-native animation objects
- keep shared retained runtime as the authoritative semantics layer

Tasks:
1. identify safe subsets for native object mapping
2. prototype retained-node to host animation-object translation
3. add capability/fallback reporting without changing shared semantic ownership

Exit criteria:
- optional native mapping exists only where parity and fallback behavior are explicit

## Current Implementation Target

This turn should finish the remaining Phases 1 through 4:
- remove the remaining editor root-drawable seams and keep retained nodes as the primary editor selection/render source
- delete the retained compiler's dead drawable-bridge path
- move the remaining animation fallback render path onto retained scene state
- update validation and status documentation to match the retained-scene-first runtime

Validation for this slice:
- `dotnet format Svg.Skia.slnx --no-restore`
- `dotnet build Svg.Skia.slnx -c Release --no-restore`
- `dotnet test Svg.Skia.slnx -c Release --no-build`

## Implementation Status

Phases 1 through 4 are now effectively implemented in the current working tree for the shared runtime and editor workflows.

Completed:
- selected-element export now prefers retained scene node rendering and falls back to drawable snapshot export only if no retained node export is available
- align/distribute and additional editor selection workflows now gather bounds and transforms from retained scene nodes first instead of depending on root drawable lookup
- layer loading no longer walks the root drawable tree to resolve editor layer metadata; retained scene nodes are the authoritative layer geometry source
- the retained compiler no longer keeps the drawable-bridge compilation path alive for unsupported element fallback; non-rendering containers compile directly as retained nodes
- dead drawable-bridge helper code has been removed from the retained compiler, reducing load-time duplication and eliminating the last internal drawable extraction path there
- animation frames that fall outside animation-layer caching now render from the retained scene document instead of calling the older drawable-first `RenderSvgDocument(...)` path
- regression coverage now proves retained-scene animation fallback for paint-server-backed animation targets and direct retained compilation for non-rendering container nodes

Primary files:
- `/Users/wieslawsoltes/GitHub/Svg.Skia/src/Svg.Editor.Skia.Avalonia/SvgEditorWorkspace.axaml.cs`
- `/Users/wieslawsoltes/GitHub/Svg.Skia/src/Svg.Editor.Svg/LayerService.cs`
- `/Users/wieslawsoltes/GitHub/Svg.Skia/src/Svg.Skia/SKSvg.Model.cs`
- `/Users/wieslawsoltes/GitHub/Svg.Skia/src/Svg.Skia/SKSvg.SceneGraph.cs`
- `/Users/wieslawsoltes/GitHub/Svg.Skia/src/Svg.Skia/SceneGraph/SvgSceneCompiler.cs`
- `/Users/wieslawsoltes/GitHub/Svg.Skia/tests/Svg.Skia.UnitTests/SvgAnimationControllerTests.cs`
- `/Users/wieslawsoltes/GitHub/Svg.Skia/tests/Svg.Skia.UnitTests/SvgRetainedSceneGraphTests.cs`

What remains after this file's original scope:
- the deepest future work is broader host-native animation object translation beyond the currently safe retained subset
- retained-first public APIs are now primary, and drawable-returning helpers are explicit legacy compatibility shims

## Additional Status Update

Completed after the original write-up:
- editor path/selection APIs no longer keep drawable fallbacks; retained scene nodes are the only geometry/edit source for those workflows
- retained-node and retained-element picture/model helpers are now the primary public API for fragment extraction
- drawable-returning public helpers such as drawable hit testing and retained-scene drawable creation are now legacy compatibility shims and are marked obsolete in favor of retained scene node APIs
- canvas-space retained scene hit testing now has direct scene-node overloads, so callers no longer need drawable proxy APIs for transformed hit tests
- retained resource-backed animation documents now participate in animation-layer caching, and the benchmark suite reflects resource-backed retained caching instead of the older fallback expectation
- Avalonia native composition now keeps the existing retained visual content when only layer visual state changes and the picture instance is unchanged, reducing unnecessary compositor content re-submission for the safe retained-native subset
