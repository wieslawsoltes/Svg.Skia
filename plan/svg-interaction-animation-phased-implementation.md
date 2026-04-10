# SVG Interaction and Animation Phased Implementation Plan

## Goal

Add a shared, renderer-level interaction and animation architecture to `Svg.Skia` that:

- works across UI frameworks
- keeps SVG semantics in shared code
- uses framework-specific input, frame scheduling, invalidation, and optional native animation backends only at the edge

This plan is intentionally split into phases so interaction can ship before the full SVG animation runtime.

## Implementation status

- Phase 1 interaction foundation has been implemented.
- Phase 2 event routing has been implemented.
- Phase 3 hit-test correctness has been implemented.
- Phase 4 shared animation runtime core has been implemented.
- Phase 5 performance and incremental redraw has been implemented for the current shared-renderer scope.
- Phase 6 optional native host animation backends has been implemented for host-side scheduling and backend selection.
- Post-phase follow-up work is now tracked in this document, with the first incremental redraw follow-up slice and the initial benchmark harness both implemented.
- The first implementation slice is in place in:
  - `src/Svg.Skia/SKSvg.Interaction.cs`
  - `src/Svg.Skia/Interaction/SvgInteractionDispatcher.cs`
  - `src/Svg.Controls.Skia.Avalonia/Svg.cs`
  - `src/Svg.Controls.Skia.Uno/Svg.cs`
  - `tests/Svg.Skia.UnitTests/SvgInteractionDispatcherTests.cs`
- The shared animation runtime slice is now in place in:
  - `src/Svg.Custom/Animation/SvgDocument.Animation.cs`
  - `src/Svg.Skia/Animation/SvgAnimationClock.cs`
  - `src/Svg.Skia/Animation/SvgAnimationController.cs`
  - `src/Svg.Skia/Animation/SvgAnimationFrameState.cs`
  - `src/Svg.Skia/Animation/SvgAnimationHostBackend.cs`
  - `src/Svg.Skia/SKSvg.Model.cs`
  - `src/Svg.Controls.Skia.Avalonia/Svg.cs`
  - `src/Svg.Controls.Skia.Uno/Svg.cs`
  - `tests/Svg.Skia.UnitTests/SvgAnimationControllerTests.cs`
  - `tests/Svg.Skia.UnitTests/SvgAnimationHostBackendResolverTests.cs`
- Current behavior delivered by this slice:
  - topmost leaf hit testing for event targeting
  - shared pointer dispatcher
  - shared event stream independent of framework-specific event args
  - optional bridge into `SvgElement` mouse events for ID-bearing elements
  - Avalonia and Uno adapter wiring
  - tunnel, target, and bubble routing through the SVG ancestry path
  - pressed-target capture routing for shared move, wheel, and release dispatch until pointer release
  - routed event bubbling from target to SVG ancestors
  - handled-state short-circuiting for shared routed dispatch
  - cursor hint resolution from SVG `cursor` attributes through the shared contract
  - native cursor application in the Avalonia and Uno host wrappers
  - typed `SvgPointerEvents` parsing/inheritance in `Svg.Custom`
  - geometry-aware point hit testing for path-backed drawables instead of bounds-only checks
  - pointer-event-specific fill/stroke/all target resolution through shared hit-testing
  - clip-path and conservative mask-aware hit rejection during point targeting
  - manual shared animation clock and controller in `Svg.Skia`
  - DOM-clone-based animated document evaluation from the `Svg.Custom` animation object model
  - typed target-property application through the generated `Svg` property metadata
  - initial interpolation support for `set`, `animate`, `animateColor`, `animateTransform`, and `animateMotion`
  - event-based SMIL `begin` and `end` timing tied to shared pointer dispatch
  - `animateMotion` evaluation from `path`, `mpath`, `values`, and `from`/`to`/`by`-derived motion paths
  - additive and accumulate handling for scalar, transform, color, and motion animation cases covered by the shared runtime
  - `SKSvg` render-state rebuild on animation time changes
  - shared animation invalidation contract consumed by the Avalonia and Uno wrappers
  - cached frame-state evaluation in the shared animation controller
  - dirty-target diffing between the last rendered animation frame and the next evaluated frame
  - persistent animated-document reuse instead of per-frame DOM deep-copy churn
  - no-op render suppression when different clock times resolve to the same effective SVG state
  - explicit pending-frame and minimum-render-interval hooks for host-level throttling
  - shared requested/actual host animation backend resolution with capability and fallback reporting
  - Avalonia host playback backends using `DispatcherTimer` and `TopLevel.RequestAnimationFrame(...)`
  - Uno host playback backends using `DispatcherQueueTimer` and `CompositionTarget.Rendering`
  - animated-source cache isolation so replayed cached documents restart from a clean shared-runtime state
- The scoped phased plan is fully implemented.

## Post-phase follow-up roadmap

The scoped six-phase plan is complete. The remaining animation work is follow-up optimization and native-integration exploration beyond the original phased scope.

### Order

1. subtree and incremental picture invalidation in `SKSvg`
2. benchmark and profiling harness for animation-frame cost
3. limited true host-native composition mapping only after the shared runtime and incremental renderer are stable

### Current follow-up slice

- Implemented:
  - incremental redraw in `SKSvg` using cached static content plus rebuilt animated top-level animated roots where the drawable/model pipeline can safely support it
  - recursive subtree-level shim and `SkiaSharp.SKPicture` invalidation within animated top-level roots, reusing unchanged descendant subtree pictures instead of re-recording the whole animated top-level root on every effective frame
  - benchmark and profiling harness for animation-frame cost in the shared `Svg.Skia` renderer
  - retained native-composition mapping for the supported Avalonia host path, using one composition child visual per top-level SVG child and updating animated layers without falling back to the regular `Render(...)` path
- Delivered in the follow-up slices:
  - stop rebuilding the full document picture on every effective animation frame in the common renderer path when the document can be safely split into static and animated top-level layers
  - stop rebuilding the full animated top-level subtree picture when only a descendant target changes, by caching descendant subtree pictures and rebuilding only the dirty path to the animated root
  - keep hit testing and interaction semantics aligned with the animated drawable state
  - preserve the current shared SVG runtime as the source of truth
  - add a local BenchmarkDotNet harness in `tests/Svg.Skia.Benchmarks` that compares layered animation-frame updates against defs-backed fallback rebuilds, with and without drawing
  - expose a shared native-composition scene/frame extraction contract from `SKSvg`
  - attach Avalonia retained visuals through the compositor child-visual API when the animated document can be represented as top-level native-composition layers
- Current limitation outside the shared runtime:
  - Uno still falls back to the shared render-loop or dispatcher backends because the restored Uno package surface does not currently provide a working child-visual attachment path on the active target platforms
- Explicit non-goals for the implemented follow-up slices:
  - no attempt to translate arbitrary SVG nodes into Avalonia or Uno composition objects
  - no broad retained-scene-graph rewrite of the existing drawable system
  - no full retained scene-graph rewrite of every drawable node; subtree invalidation remains layered on top of the existing drawable/picture pipeline
  - no platform-specific reimplementation of SVG timing semantics

## Scope

### In scope

- shared event contract driven by `SKSvg` hit testing
- framework adapters for Avalonia and Uno SVG controls
- shared animation runtime design for SVG 1.1 animation elements already modeled in `Svg.Custom`
- phased implementation strategy, validation plan, and performance gates

### Out of scope for the first implementation slice

- full SVG 1.1 bubbling/capturing event model
- DOM scripting execution from attribute strings
- full `pointer-events` paint/fill/stroke semantics
- SMIL timing engine completeness
- native-framework-specific accelerated animation backends

## Current State

### Event handling

- `SKSvg` already exposes renderer-space hit testing in `src/Svg.Skia/SKSvg.HitTest.cs`.
- `HitTestService` returns all matches in tree order, not a single event target, in `src/Svg.Model/Services/HitTestService.cs`.
- Most drawables still use bounds-only hit testing in `src/Svg.Model/Drawables/DrawableBase.cs`.
- `SvgElement` exposes mouse events and the dormant `ISvgEventCaller` registration contract in `externals/SVG/Source/SvgElement.cs`.
- The repo does not currently wire any framework pointer events into that contract.

### Rendering

- `SKSvg` is a static picture model and snapshot replay engine in `src/Svg.Skia/SKSvg.Model.cs`.
- `AnimatedVectorDrawable` is explicitly unsupported in `src/Svg.Model/Services/VectorDrawableConverter.cs`.
- The Avalonia and Uno controls each render the SVG as a single custom surface, not as a framework-native visual tree.

### Animation object model

- `Svg.Custom` now has the SVG 1.1 animation element object model.
- There is still no runtime timeline evaluator, target-property resolver, or frame invalidation loop.

## Architecture

## 1. Shared interaction layer

Add a shared interaction subsystem under `src/Svg.Skia/Interaction`:

- `SvgPointerInput`
- `SvgPointerEventArgs`
- `SvgPointerDeviceType`
- `SvgMouseButton`
- `SvgPointerEventType`
- `SvgInteractionDispatcher`

Responsibilities:

- map picture-space pointer input to a single topmost SVG target
- maintain hover and pressed state
- emit a shared cross-framework event stream
- optionally bridge into `SvgElement.RegisterEvents(...)` for existing CLR event subscribers

This layer must not reference Avalonia or Uno.

## 2. Renderer-side target resolution

Extend `SKSvg` with topmost hit testing:

- `HitTestTopmostDrawable(SKPoint)`
- `HitTestTopmostElement(SKPoint)`
- canvas-matrix overloads

Rules:

- reverse child traversal to match draw order
- prefer the deepest painted leaf over ancestor containers
- honor `display`, `visibility`, and `pointer-events:none`
- leave full SVG `pointer-events` semantics for a later phase

## 3. Framework adapters

Each host framework keeps raw input and invalidation at the control layer:

- Avalonia: override pointer methods on `Avalonia.Svg.Skia.Svg`
- Uno: subscribe to control pointer events on `Uno.Svg.Skia.Svg` because the current host base type does not expose the same virtual pointer override surface

Adapters translate framework pointer events into `SvgPointerInput` and feed the shared dispatcher.

They remain responsible for:

- pointer capture
- coordinate conversion from control to picture space
- invalidation
- framework cursor integration later

## 4. Shared animation runtime

The animation runtime should live in shared code, not inside Avalonia or Uno:

- `SvgAnimationClock`
- `SvgAnimationController`
- target resolution from animation element to animated element/property
- timing state evaluation
- typed interpolation pipeline
- animated-value overlay over the static DOM model

The framework host only supplies:

- current time
- frame scheduling
- invalidation

This keeps SVG timing semantics portable and testable.

## 5. Optional framework-native animation backends

Later, specific animation classes may map onto host-native engines when the target is compatible:

- Avalonia transitions/composition
- Uno XAML storyboards
- Uno/WinUI composition where supported

This is an optimization layer only. The shared SVG runtime remains the source of truth.

## Phase Breakdown

## Phase 1: Interaction foundation

### Deliverables

- shared pointer contract
- topmost element hit testing in `SKSvg`
- shared dispatcher with hover/press/click tracking
- bridge into `SvgElement.RegisterEvents(...)` for ID-bearing elements
- Avalonia and Uno control integration at the host control layer
- unit tests for target resolution and dispatcher behavior

### Behavioral constraints

- leaf-target only, no bubbling yet
- `pointer-events:none` honored
- no full SVG `pointer-events` painted/fill/stroke matrix yet
- one active pointer state per dispatcher instance
- mouse-style event bridging only, matching current upstream `SvgElement` events

### Implementation details

- use reverse traversal of `DrawableContainer.ChildrenDrawables`
- do not target ancestor containers directly in phase 1
- register upstream `SvgElement` event actions only for elements with non-empty `ID`
- surface a shared dispatcher event stream even when no SVG `ID` exists
- reset interaction state when source content changes

### Validation

- topmost hit target for overlapping siblings
- child beats ancestor container
- `pointer-events:none` skips the front element
- move enters/leaves correctly
- press/release same target produces click
- existing `SvgElement.Click`, `MouseDown`, `MouseUp`, `MouseOver`, `MouseOut`, `MouseMove`, `MouseScroll` can fire through the bridge for ID-bearing elements

## Phase 2: Event routing semantics

### Deliverables

- ancestor event path building
- bubble route
- optional tunnel/capture route if needed
- shared event cancellation model
- cursor contract

### Status

- Implemented:
  - target-to-root ancestry path routing
  - bubble routing for shared events and the compatibility `SvgElement` bridge
  - tunnel routing for shared routed events ahead of target/bubble dispatch
  - pressed-target capture routing in the shared dispatcher until pointer release
  - `Handled` short-circuiting in shared routed events
  - shared cursor hint resolution via inherited `cursor` attributes
  - framework-native cursor application in the Avalonia and Uno wrappers

### Implementation details

- compute ancestry from target element to document root
- preserve leaf target while routing through ancestors
- add `Handled` support to shared event args
- expose cursor hints without hard-coding framework cursor types into shared code

### Validation

- group/anchor ancestors receive routed events
- handled state stops downstream propagation where configured

## Phase 3: Hit-test correctness

### Deliverables

- geometry-aware hit testing beyond bounds for more drawable types
- better stroke/fill hit rules
- clip and mask awareness where practical
- typed `pointer-events` model in `Svg.Custom`

### Implementation details

- move from bounds-only to geometry-aware checks incrementally per drawable type
- support `visiblePainted`, `visibleFill`, `visibleStroke`, `painted`, `fill`, `stroke`, `all`
- keep a conservative fallback path for unsupported shapes

### Validation

- path stroke hit precision
- fill-only vs stroke-only cases
- clipped shapes not receiving hits outside clip

### Status

- Implemented:
  - typed `SvgPointerEvents` enum/converter/property in `Svg.Custom`
  - shared pointer-event-aware target evaluation in `HitTestService.HitTestPointer(...)`
  - geometry-aware point hit testing for `DrawablePath`-based shapes
  - `use` and marker wrappers delegating hit testing to their referenced drawables
  - clip-path rejection and conservative mask rejection for point targeting
  - focused model and renderer tests for `pointer-events`, stroke-only hits, and clipped hits
- Current limitations within the phase:
  - text hit testing still uses conservative bounds instead of glyph-outline geometry
  - rectangle hit-test APIs remain bounds-based; the geometry-aware work in this phase is for point targeting
  - mask handling is geometry-based, not alpha-precise
  - hidden elements that would require `pointer-events` modes ignoring visibility are still limited by the current drawable-creation path

## Phase 4: Shared animation runtime core

### Deliverables

- animation clock abstraction
- animation timeline evaluator
- target-property resolution
- animated-value overlay state
- invalidation contract back into controls/render hosts

### Implementation details

- treat the DOM as base state and runtime animation values as overlays
- start with discrete, numeric, color, transform, and motion interpolation
- parse shared timing fields from animation elements already added in `Svg.Custom`
- rebuild or refresh render state from animated overlays per frame

### Validation

- `set`
- `animate`
- `animateColor`
- `animateTransform`
- `animateMotion`
- timing attributes: `begin`, `dur`, `end`, `repeatCount`, `repeatDur`, `fill`

### Status

- Implemented:
  - manual `SvgAnimationClock`
  - shared `SvgAnimationController`
  - source-document cloning via `SvgDocument.DeepCopy()`
  - animated target resolution by cloned DOM address
  - runtime application through the generated typed attribute setters
  - `SKSvg.SetAnimationTime(...)`, `AdvanceAnimation(...)`, and `AnimationInvalidated`
  - host invalidation wiring in the Avalonia and Uno controls
  - event-based `begin` and `end` timing support using the shared interaction event stream
  - `animateMotion` support for `path`, `mpath`, `values`, and synthesized `from`/`to`/`by` motion paths
  - additive and accumulate handling for the phase-4 scalar, color, transform, and motion scope
- Phase result:
  - Phase 4 is complete for the planned shared-runtime scope.

## Phase 5: Performance and incremental redraw

### Deliverables

- dirty-target tracking
- selective rebuild strategy
- cached interpolation state
- animation throttling hooks

### Implementation details

- separate target/property evaluation from full picture rebuild when possible
- keep frame scheduling outside the shared runtime
- add profiling around rebuild cost, hit testing, and animation tick cost

### Validation

- frame-to-frame allocations
- large SVG animation scenarios
- host invalidation cadence

### Status

- Implemented:
  - cached `SvgAnimationFrameState` evaluation and reuse in `SvgAnimationController`
  - dirty-target counting and diff-based attribute application
  - persistent animated `SvgDocument` reuse inside `SKSvg`
  - no-op rebuild suppression for equivalent animation states
  - `AnimationMinimumRenderInterval`, `HasPendingAnimationFrame`, `FlushPendingAnimationFrame()`, and `LastAnimationDirtyTargetCount`
  - recursive subtree-level picture invalidation for animated top-level roots, with nested shim-picture composition and cached descendant `SkiaSharp.SKPicture` reuse
  - benchmark and profiling harness for shared animation-frame cost
  - unit coverage for equivalent-frame suppression, pending throttled frames, and reversion when an animation becomes inactive
- Remaining limitations within the phase:
  - subtree invalidation now covers top-level animated roots, nested animated subtrees within those roots, and rendered wrapper/generated drawables such as `use`, `switch`, and marker-hosted drawables, but it is still not a general retained scene-graph renderer
  - the renderer now stops dirty-path rebuilds at the selected cache root inside the animated scope instead of always walking back to the animated top-level root, but it still recomposes the touched top-level animated scope on top of the static layer
  - non-drawable resource pipelines such as gradients, clip-path resources, and filter graphs still use the fallback full-frame rebuild path because they do not map cleanly onto retained drawable subtrees

### Follow-up implementation direction

- first reduce animation-frame work by caching static document content and re-recording only the animated document regions that contain active animation targets
- then refine the animated-layer path by caching descendant subtree pictures and only rebuilding the dirty path through the existing drawable graph
- keep the animated drawable tree authoritative for hit testing so pointer routing follows animated geometry
- treat the first redraw optimization as top-down and conservative; correctness takes priority over the finest possible invalidation granularity

## Phase 6: Optional native host animation backends

### Deliverables

- Avalonia adapter experiment
- Uno/WinUI adapter experiment
- capability matrix and fallback rules

### Implementation details

- host-native backends in this implementation only provide scheduling and invalidation; SVG timing/value evaluation stays in the shared `SKSvg` runtime
- the exposed contract is shared across wrappers:
  - `SvgAnimationHostBackend`
  - `SvgAnimationHostBackendCapabilities`
  - `SvgAnimationHostBackendResolution`
  - `SvgAnimationHostBackendResolver`
- Avalonia uses:
  - `DispatcherTimer` for timer-driven playback
  - `TopLevel.RequestAnimationFrame(...)` for render-loop playback when attached to a `TopLevel`
- Uno uses:
  - `DispatcherQueueTimer`
  - `CompositionTarget.Rendering`
- wrapper defaults remain non-animated until opted in:
  - requested backend defaults to `Manual`
  - `Default` picks `RenderLoop`, then `DispatcherTimer`, then `Manual`
- wrappers expose requested backend, frame interval, playback rate, actual backend, fallback reason, and capability matrix
- render-loop availability is attachment-sensitive; detached controls fall back to `Manual` rather than running timers against an inactive host
- animated cache entries are cloned per playback session so cached SVG content does not retain advanced animation time between reloads
- this phase does not map SVG elements onto framework-native visual/composition trees; that remains a future optimization layer if ever needed

### Validation

- build and test coverage for backend resolution and fallback rules in shared tests
- identical animated rendering semantics versus the shared runtime because all backends drive the same `SKSvg.AdvanceAnimation(...)` pipeline
- safe fallback to `Manual` or `DispatcherTimer` when the requested backend is unavailable

### Status

- Implemented:
  - shared backend-selection contract in `src/Svg.Skia/Animation/SvgAnimationHostBackend.cs`
  - Avalonia host playback integration in `src/Svg.Controls.Skia.Avalonia/Svg.cs`
  - Uno host playback integration in `src/Svg.Controls.Skia.Uno/Svg.cs`
  - backend fallback-rule coverage in `tests/Svg.Skia.UnitTests/SvgAnimationHostBackendResolverTests.cs`
- Remaining limitations within the phase:
  - host-native backends currently schedule the shared runtime; they do not translate SVG nodes into framework-native animation objects
  - wrapper-level capability reporting is attachment-time state, not a static platform-wide capability table

## File and Type Plan

### Shared interaction

- `src/Svg.Skia/SKSvg.Interaction.cs`
- `src/Svg.Skia/Interaction/SvgInteractionDispatcher.cs`

### Host adapters

- `src/Svg.Controls.Skia.Avalonia/Svg.cs`
- `src/Svg.Controls.Skia.Uno/Svg.cs`

### Shared animation runtime

- `src/Svg.Skia/Animation/` new folder in later phases

### Tests

- `tests/Svg.Skia.UnitTests/SvgInteractionDispatcherTests.cs`
- later: animation runtime tests in `tests/Svg.Skia.UnitTests`
- later: host smoke tests where practical

## Public API Direction

### Phase 1 public additions

- `SKSvg.HitTestTopmostDrawable(...)`
- `SKSvg.HitTestTopmostElement(...)`
- `SvgInteractionDispatcher`
- `SvgPointerInput`
- `SvgPointerEventArgs`
- `SvgPointerDeviceType`
- `SvgMouseButton`
- `SvgPointerEventType`
- `Avalonia.Svg.Skia.Svg.Interaction`
- `Uno.Svg.Skia.Svg.Interaction`

### Later public additions

- routed event path APIs
- cursor contract
- animation clock/controller/runtime types
- host invalidation/tick abstraction

## Risk Areas

- current hit test precision is not yet good enough for full `pointer-events` parity
- `SvgElement.RegisterEvents(...)` assumes IDs and a mouse-centric event surface
- `Use`, markers, clipping, masks, and text spans need targeted semantic decisions
- a full animation engine can become expensive if every tick forces a whole-picture rebuild

## Immediate Implementation Slice

Implement phase 1 now:

1. add the plan document
2. add topmost hit testing to `SKSvg`
3. add the shared interaction dispatcher and event bridge
4. wire Avalonia and Uno controls to feed the dispatcher
5. add focused unit tests
6. build and test the solution
