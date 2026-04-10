# SVG Retained Scene-Graph Rewrite

## Status
- In progress
- Scope: shared `Svg.Skia` retained renderer architecture, loading pipeline rewrite, incremental updates, and host integration strategy
- This document defines the target architecture and the phased implementation plan for replacing the current transient `SvgElement -> Drawable -> SKPicture` pipeline with a retained scene graph

## Current Branch Status

The retained-scene rewrite is no longer only a proposed architecture. This branch now includes a working retained scene document, a DOM-first compiler path, retained resource indexing, and a mutation router that can update the live retained graph in place.

- `SvgSceneDocument`, `SvgSceneNode`, `SvgSceneCompiler`, and `SvgSceneRenderer` exist in shared `Svg.Skia`
- `SKSvg` exposes a lazy `RetainedSceneGraph` plus `CreateRetainedSceneGraphModel()` and `CreateRetainedSceneGraphPicture()`
- retained scene compilation is driven from the SVG DOM tree, not from the live root drawable tree
- retained resources are indexed and dependency-tracked for gradients, patterns, markers, masks, clip paths, paint servers, and related reference-style resources
- scene mutation routing can update the current retained graph in place from DOM element mutations and animation frame deltas
- animation frame integration now attempts retained-scene mutation first and only falls back to full retained invalidation when the mutation path cannot safely service the update
- parity tests cover indexing, one-to-many address mapping for `use`, retained rendering parity, retained-graph reuse across animation updates, and dependency-driven updates for `use` and resource-backed content

This is still not the final architecture. The retained graph now has the correct structural direction, but the runtime is still in transition:

- core path-based primitives (`path`, `rect`, `circle`, `ellipse`, `line`, `polyline`, `polygon`) now compile local retained visuals directly without transient per-element drawable generation
- structural wrappers such as `g`, `a`, and nested `svg` fragments now compile wrapper state directly as retained nodes without transient drawable extraction
- text, masked, and filtered visual nodes now compile and render as retained scene nodes without per-element transient drawable ownership in the scene graph
- retained text compilation now covers nested `tspan`, `textPath`, and `tref` content with retained geometry estimation and parity-tested retained playback
- retained resources are indexed, dependency-tracked, and now own clip, mask, and filter runtime payload generation directly inside `Svg.Skia`, with retained `feImage` evaluation and retained-source filter inputs instead of transient drawable snapshots
- retained paint runtime now owns fill, stroke, opacity, gradients, and patterns inside `Svg.Skia`, including retained pattern tile rendering instead of drawable snapshot recording
- direct retained marker expansion is now in place for path-based marker elements, including inherited group marker settings, without going through `MarkerService` and `MarkerDrawable`
- the retained renderer is available and update-aware, but it is not yet the single mandatory primary render path for every host feature
- hit testing and native host extraction are now driven from retained scene state, including retained visibility, pointer-event, background, transform, and opacity metadata, with lightweight drawable proxies only at legacy API boundaries
- the retained scene root now compiles as a direct retained fragment instead of relying on an implicit drawable-backed root bridge
- `SvgSceneDocument` and `SKSvg` now expose retained-scene-first lookup, mutation, node-rendering, and hit-testing APIs for editor/tooling consumers
- retained mutation coverage now explicitly includes mask-resource and filter-resource dependent updates, and parity coverage now includes masked rich-text documents

## Problem Statement

`Svg.Skia` currently renders by:

1. Parsing SVG into the mutable `SvgDocument` DOM
2. Converting DOM subtrees into transient `DrawableBase` trees
3. Recording transient drawables into shim `SKPicture` models
4. Converting shim pictures into `SkiaSharp.SKPicture`

That architecture works, but it has structural limits:

- loading cost is front-loaded into repeated object graph construction
- updates to the SVG model often rebuild too much state
- animation and interaction have to layer invalidation on top of a transient renderer
- resource resolution, draw ordering, and host-native composition have no single retained source of truth
- the fast path today is cache-oriented, not graph-oriented

The retained rewrite replaces the transient renderer with a retained scene graph that owns:

- renderable node topology
- resolved geometry, paint, clipping, masking, filter, and text state
- resource dependency tracking
- address-based lookup for granular model updates
- subtree invalidation and regeneration
- host-facing composition extraction

## Goals

- State-of-the-art retained rendering architecture for SVG in shared `Svg.Skia`
- Fast initial load through staged compilation and reusable caches
- Very fast granular updates from SVG DOM mutations to scene graph mutations
- Eliminate full-picture rebuilds for common subtree updates
- Single canonical retained graph for:
  - static rendering
  - animation
  - hit testing
  - native composition extraction
  - editor/inspection tooling
- Preserve visual correctness relative to the current renderer during transition
- Keep the implementation framework-neutral, with Avalonia/Uno/native host adapters layered on top

## Non-Goals

- Rewriting the external SVG DOM library in the first wave
- Shipping a second permanent renderer beside the retained graph
- Cutting corners with host-specific rendering logic in the shared core
- Treating animation caching as the long-term architecture

## Target Architecture

### Layers

1. DOM Layer
- `SvgDocument` and `SvgElement` remain the authoring and compatibility object model

2. Compilation Layer
- Converts DOM and resolved resources into retained scene nodes and retained resources
- Tracks dependencies between DOM addresses and scene nodes/resources

3. Retained Scene Layer
- Mutable graph of retained render nodes and retained resources
- Owns node identity, invalidation, subtree rebuild, and render ordering

4. Rendering Layer
- Traverses retained scene nodes into:
  - shim `SKPicture`
  - `SkiaSharp.SKPicture`
  - native-composition layer extraction

5. Host Layer
- Avalonia, Uno, and future hosts consume retained scene outputs and invalidation signals

### Core Principles

- The retained graph is the source of truth, not a cache
- Resources are first-class graph objects
- Node identity is stable across updates
- Dirty propagation is structural and dependency-aware
- Subtree rebuilds are explicit and bounded
- Rendering is pure playback from retained state
- Host-native composition is an extraction of retained state, not a parallel animation system

## Retained Scene Data Model

### Retained Document

`SvgSceneDocument`

- root scene node
- cull bounds / viewport / viewBox metadata
- lookup by SVG element address key
- lookup by SVG id
- resource registry
- dependency graph
- revision counter
- invalidation queue

### Retained Nodes

`SvgSceneNode`

- stable scene node id
- source SVG element reference and address key
- node kind
- parent pointer
- ordered children
- optional local display list
- transform state
- clip/overflow state
- mask/filter/opacity state
- geometry bounds
- transformed bounds
- dirty flags
- version / generation counters

### Retained Resources

Resources become retained objects, not incidental data hidden inside drawables:

- clip paths
- masks
- gradients
- patterns
- paint servers
- filters and filter subgraphs
- resolved images
- resolved text assets / glyph runs

Each resource has:

- stable resource id
- dependency list
- reverse dependency list
- dirty flags
- resolved runtime payload

### Display Lists

Each scene node owns a local display list containing only node-local visuals.

- Parent wrapper state is not baked into child display lists
- Parent clip/mask/filter/opacity scopes are replayed structurally by the retained renderer
- Child ordering remains explicit in the scene graph

## Loading Pipeline Rewrite

### Current

`SvgDocument -> DrawableFactory -> Drawable tree -> Snapshot -> SKPicture`

### Target

`SvgDocument -> Scene compilation -> Retained scene/resource graph -> Render`

### Compilation Stages

1. Parse DOM
- existing SVG DOM parser remains the front door

2. Resolve document metadata
- viewport, viewBox, units, external asset resolution context

3. Pre-index DOM
- stable address keys
- id map
- element type map
- mutation routing tables

4. Compile retained resources
- gradients, patterns, clip paths, masks, filters, markers, symbol/use expansions

5. Compile retained nodes
- scene node tree with local display lists and structural visual state

6. Build dependency graph
- scene node -> resource dependencies
- resource -> dependent scene nodes
- DOM address -> scene node/resource mappings

7. Produce retained document
- immutable-at-boundary / mutable-internally retained graph

### Loading Optimizations

- parser-side pre-indexing of ids and addresses
- stable URI and asset resolution caches
- resource interning for repeated paint/filter structures
- local display list memoization for unchanged subtrees
- direct compilation to scene nodes without round-tripping through transient drawables in the final architecture

## Update Model

### Update Sources

- animation frame mutations
- interaction-driven mutations
- editor changes to DOM attributes
- resource changes
- external asset reloads

### Update Flow

1. DOM mutation arrives with element address and changed attribute set
2. mutation router maps changed addresses to retained nodes/resources
3. dependency graph expands affected region
4. dirty flags propagate to:
- directly affected nodes/resources
- dependent resources
- dependent ancestor layout/clip scopes as required
5. compiler regenerates only dirty subtrees/resources
6. renderer replays only dirty retained outputs

### Dirty Granularity Rules

- inherited style changes dirty dependent descendant nodes
- transform changes dirty subtree bounds and composition extraction
- resource mutations dirty all dependents
- text mutations dirty text shaping and affected layout nodes
- filter changes dirty dependent filter graph and target nodes

## Rendering Model

### Retained Renderer Responsibilities

- replay node-local display lists
- manage transform stack
- manage clip/overflow stack
- manage filter/opacity/mask scopes
- preserve draw order
- produce subtree pictures on demand
- support incremental render targets

### Render Targets

- full shim `SKPicture`
- full `SkiaSharp.SKPicture`
- subtree shim picture
- subtree native picture
- extracted host-native composition layer list

### Native Composition

Native composition becomes a direct extraction pass over retained nodes:

- select renderable retained layer roots
- extract stable bounds, opacity, transforms, and local display lists
- keep scene-node identity stable across frames
- reissue only changed retained layers

## Hit Testing and Interaction

The retained graph becomes the authoritative hit-test graph:

- geometry-aware hit testing per retained node
- retained clip and mask constraints
- z-order and event route derivation directly from retained node order
- pointer-events and visibility logic evaluated against retained node state

## Animation Integration

Animation does not rebuild transient drawables.

Instead:

1. animation runtime resolves attribute values
2. mutation router applies updates to retained scene state
3. dirty regions propagate through retained nodes/resources
4. only impacted retained outputs are regenerated

## Transition Strategy

The rewrite must be staged, but the target is a full replacement.

### Stage A
- land retained scene core types
- land compiler bridge from current drawable output
- land retained renderer
- wire `SKSvg` to build and expose retained scene documents

### Stage B
- use retained scene as the primary render path behind a feature toggle
- validate image parity against current output
- feed native composition and hit testing from retained nodes

### Stage C
- remove transient animation-layer caching architecture
- route animation invalidation through retained nodes/resources

### Stage D
- remove drawable-tree-first rendering path
- compile retained scene directly from DOM/resources

## Implementation Status Against Plan

### Phase 1: Retained Scene Foundation

Implemented in this branch:

- retained node, resource, and document types
- retained scene renderer
- `SKSvg` retained graph creation and exposure
- indexing by address and id
- scene-node identity and root replacement support

### Phase 2: Renderer Parity Path

Implemented for representative coverage:

- retained-render parity tests for full-scene playback
- retained parity coverage for `use`, markers, generated drawable children, and animation-frame refresh
- retained parity coverage for rich text (`tspan`, `textPath`, `tref`) and resource-backed pattern paints
- retained parity coverage for masked rich-text documents

Still open:

- broader parity expansion around the hardest combined filter-plus-text edge cases

### Phase 3: Incremental Static Updates

Implemented in this branch:

- address-based mutation routing
- retained dependency graph
- in-place subtree recompilation and node replacement
- id-based and resource-based reverse dependency expansion
- public retained-scene lookup and mutation entry points for address-key, id, and node-targeted tooling flows

### Phase 4: Animation Rewrite

Implemented for retained-scene integration:

- animation frame deltas are routed into retained scene mutations
- retained scene documents are reused across animation updates when routing succeeds

Still open:

- fully remove the older non-retained fallback paths once retained coverage is proven across all remaining edge cases

### Phase 5: Resource Graph Rewrite

Implemented in this branch for the retained runtime path:

- retained resource indexing
- resource dependency tracking
- mutation expansion from resource changes to dependent scene roots
- retained-owned clip, mask, and filter runtime payload resolution
- retained-owned fill/stroke/opacity paint resolution
- retained-owned gradient and pattern paint server evaluation, including retained pattern tile playback
- retained runtime payload refresh for temporary mask subtrees

Still open:

- eliminate remaining fallback full rebuilds for the most complex resource pipelines
- continue replacing legacy helper usage in residual utility paths where retained-owned evaluators are not yet complete

### Phase 6: Direct DOM-to-Scene Compilation

Implemented structurally in this branch:

- retained graph traversal is DOM-first
- compilation roots and resource roots are derived from DOM element identity and addresses
- direct retained local-visual compilation is in place for core path-based primitives
- retained nodes now record compilation strategy so the shrinking drawable bridge is explicit and testable
- the retained document root now compiles directly instead of entering the retained graph through the drawable bridge
- degenerate path-based visual elements now stay on the direct retained path instead of dropping back to drawable-backed placeholder nodes
- retained no-bridge coverage now includes selected W3C shape and DOM documents that previously exposed the last direct-compilation seam

Still open:

- replace the remaining per-element drawable extraction bridge for the last unsupported visual/resource pipelines with direct retained display-list generation
- reduce load-time duplication caused by temporary drawable generation for compatibility-only payload extraction paths

### Phase 7: Host and Tooling Convergence

Implemented for runtime plumbing:

- point hit testing and topmost hit testing are evaluated against retained scene metadata rather than live DOM state
- native composition extraction is driven from retained scene nodes and retained node state rather than drawable-tree traversal
- retained-scene-first node/resource lookup, node rendering, and mutation APIs are available on `SvgSceneDocument` and `SKSvg`
- retained picture parity coverage now includes selected external W3C text, masking, and filter documents in addition to the synthetic in-repo stress cases
- editor-facing layer/tooling entry points now bind retained scene nodes directly for layer inspection and retained bounds overlays instead of depending only on drawable lookup
- `SKSvg` now exposes retained-scene element lookup and retained-node hit-test wrappers so downstream tools can stay on retained state instead of rebuilding legacy proxies first
- editor selection bounds, resize/skew/rotate handle setup, path-edit transforms, polygon/polyline edit transforms, and related workspace selection state now resolve retained scene nodes first and only fall back to drawables when retained nodes are unavailable
- editor path-selection activation and document-tree path/poly edit entry points now start from retained scene node transforms instead of drawable transforms, with regression coverage for retained edit matrices

Still ahead in the plan:

- remove the remaining compatibility proxies at legacy public API boundaries once downstream callers can move to retained-scene APIs
- migrate remaining editor commands that still require drawable snapshots or drawable ordering helpers, such as selected-element export, align/distribute layout helpers, and path/shape manipulation helpers that still depend on drawable-specific geometry extraction

## Full Implementation Plan

### Phase 1: Retained Scene Foundation

- add retained node/resource/document types
- add scene compiler bridge from drawables
- add retained renderer that replays structural state and local display lists
- expose retained scene on `SKSvg`
- add lookup and dirty-marking infrastructure

### Phase 2: Renderer Parity Path

- add parity tests comparing retained renderer output to current output
- make retained renderer available for static full-document rendering
- verify masks, filters, text, markers, `use`, nested fragments, and clipping

### Phase 3: Incremental Static Updates

- add DOM mutation router
- add dependency graph
- add subtree recompilation and retained output regeneration
- add address-based and id-based scene updates

### Phase 4: Animation Rewrite

- route animation runtime into retained mutations
- remove drawable rebuilds from animated frame generation
- replace top-level layer caching with retained subtree invalidation

### Phase 5: Resource Graph Rewrite

- compile gradients/patterns/filters/masks into retained resources
- add granular dependency invalidation
- remove fallback full rebuilds for resource changes

### Phase 6: Direct DOM-to-Scene Compilation

- bypass transient drawable tree for retained compilation
- keep drawables only as compatibility/editor helpers if still needed
- optimize load time and memory footprint

### Phase 7: Host and Tooling Convergence

- feed native composition from retained graph only
- feed hit testing from retained graph only
- expose scene graph inspection hooks for editor/tooling

## Risks

- visual parity regressions in mask/filter/text edge cases
- scene node local-display-list decomposition mistakes
- accidental duplication of wrapper state between node and local display list
- temporary memory pressure during transition while both pipelines exist

## Success Criteria

- retained scene graph is the primary shared renderer
- static and animated updates use granular subtree invalidation
- no full transient drawable rebuild for common attribute animations
- native composition extraction is based on retained scene nodes
- model updates are address-routed and subtree-bounded
- load time and steady-state frame time improve relative to the current pipeline

## Implemented Rewrite Slice In This Branch

This branch now contains the first meaningful retained-scene rewrite tranche:

- retained scene graph core types
- retained scene renderer for full-scene playback
- DOM-first retained compiler with generated-child bridging where required
- retained resource registry and reverse dependency graph
- in-place mutation routing for DOM edits and animation frame updates
- focused parity/update tests for representative static, generated, `use`, and resource-backed cases

That means the rewrite has moved past a pure bridge prototype. The current implementation is already exercising the retained graph as a live mutable rendering structure, while still keeping selective compatibility bridges where the final direct retained compiler is not finished yet.
