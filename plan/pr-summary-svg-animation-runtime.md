# PR Summary: SVG Animation Runtime, Retained Scene Graph, and Rendering Cutover

## Overview

This branch started as shared SVG animation and interaction work, then grew into a larger rendering-architecture change.

It adds SVG 1.1 animation DOM support in `Svg.Custom`, shared pointer interaction and geometry-aware hit testing, a shared animation runtime in `SKSvg`, host playback backends for Avalonia and Uno, and an Avalonia retained `NativeComposition` path for supported scenes.

On top of that original scope, the branch introduces a retained scene-graph runtime, migrates rendering/editor/animation flows onto that retained runtime, splits the retained scene graph into its own `Svg.SceneGraph` project, and then splits the shared animation runtime into a new `Svg.Animation` project. The branch also removes several unshipped compatibility shims and moves the rendering pipeline toward the retained scene graph as the single runtime representation for the next major release.

Base branch: `master`  
Merge-base: `076e0a22e3fe0220db951710d7799dc92b42576d`

Branch diff vs `master`:

- `163` files changed
- about `25,165` insertions
- about `624` deletions

## Commit Progression After The Last PR Summary Update

The earlier PR summary stopped at the first animation/native-composition/doc pass. Since then the branch added:

- retained scene graph foundation and mutation routing
- retained-scene-first editor workflows
- retained-scene rendering fallback for animation
- removal of the retained compiler drawable bridge
- retained hit-testing and filter/mask parity fixes
- scene-graph major-release cutover work that removes drawable-first public rendering flows
- `Svg.SceneGraph` split into a separate project
- reflection removal from animation and scene compilation helpers
- removal of unshipped obsolete compatibility APIs
- `Svg.Animation` split into a separate project

Relevant later commits include:

- `f106e50a` Implement subtree animation invalidation
- `a736db88` Prefer default animation backends
- `20058d0a` Fix animation timing and additive state
- `9e5c315d` Gate native composition to renderable roots
- `7c3b5edf` Add retained scene graph foundation
- `9e8bccf1` Migrate editor workflows to retained nodes
- `65c23848` Route interaction through retained scene
- `796370f3` Use retained scenes for animation fallback
- `6e86c57a` Remove retained compiler drawable bridge
- `41f4b71e` Fix retained mask hit testing
- `537eb2ef` Fix event timing instance handling
- `7865aab6` Prune hidden retained hit-test subtrees
- `1cf14385` Remove drawable editor fallbacks
- `aaa16665` Migrate retained animation runtime APIs
- `0bba357f` Remove animation reflection access
- `60767e4d` Drop obsolete retained API shims
- `5fc313de` Split retained scene graph project
- `754035ca` Cut over rendering to retained scenes
- `eb990cc4` Fix retained filter parity and hit testing
- `e06ca06f` Split animation runtime into `Svg.Animation`

## Main Functional Changes

### 1. SVG animation DOM and shared runtime

The branch adds local SVG 1.1 animation element support in `Svg.Custom` and a shared animation runtime in `SKSvg`.

This includes:

- `set`, `animate`, `animateColor`, `animateTransform`, and `animateMotion`
- event-driven begin/end timing
- additive and accumulate handling
- animation clock/time control APIs
- host playback backend resolution for Avalonia and Uno
- animation invalidation and frame-state tracking

The runtime was later cleaned up by:

- removing reflection-based animated attribute access in favor of explicit runtime bridges
- fixing timing edge cases such as dotted event ids, repeat handling, spline handling, and zero-duration cases
- moving the shared runtime into a dedicated `Svg.Animation` project

Key files:

- `src/Svg.Custom/Animation/*`
- `src/Svg.Animation/Animation/*`
- `src/Svg.Animation/SvgAnimationInvalidation.cs`
- `src/Svg.Skia/SKSvg.AnimationLayers.cs`
- `src/Svg.Controls.Skia.Avalonia/Svg.cs`
- `src/Svg.Controls.Skia.Uno/Svg.cs`

### 2. Shared interaction and geometry-aware hit testing

The branch adds typed `pointer-events` support, routed pointer dispatch, geometry-aware hit testing, and retained-scene-first hit testing.

This includes:

- topmost-element and topmost-scene-node targeting
- clip-path and mask-aware hit testing
- routed tunnel/target/bubble dispatch
- pressed-target capture behavior
- cursor resolution
- retained-scene hit-test pruning for hidden or suppressed subtrees

Key files:

- `src/Svg.Custom/Interaction/SvgPointerEvents.cs`
- `src/Svg.Skia/Interaction/SvgInteractionDispatcher.cs`
- `src/Svg.SceneGraph/SvgSceneHitTestService.cs`
- `tests/Svg.Skia.UnitTests/HitTestTests.cs`

### 3. Avalonia/Uno host playback and native composition

The host controls now expose:

- `AnimationBackend`
- `AnimationFrameInterval`
- `AnimationPlaybackRate`
- `ActualAnimationBackend`
- fallback/capability reporting

Avalonia additionally gained a retained native-composition path for supported scenes, plus TestApp support for selecting and exercising it.

This work also includes follow-up fixes for:

- stale/disposed picture usage
- initial retained visual activation
- clipping of translated retained layers
- descendant opacity preservation
- fallback from unsupported native-composition scenes

Key files:

- `src/Svg.Controls.Skia.Avalonia/Svg.cs`
- `src/Svg.Controls.Skia.Avalonia/Composition/SvgCompositionVisualScene.cs`
- `src/Svg.Controls.Skia.Uno/Svg.cs`
- `samples/TestApp/Views/MainView.axaml`
- `samples/TestApp/Views/MainView.axaml.cs`

### 4. Retained scene graph foundation

The branch introduces a retained scene graph as a real runtime representation, not just a transient export format.

The retained layer now covers:

- scene compilation from SVG DOM
- retained node/resource indexing
- retained rendering
- retained mutation routing
- retained resource ownership for clip, mask, filter, paint, and text payloads
- retained-node hit testing
- retained-scene-based tooling helpers

The compiler progressively moved away from drawable-bridge fallbacks and toward direct retained compilation for core shapes, structural wrappers, text, masks, filters, `use`, `switch`, and image-backed scenarios.

Key files:

- `src/Svg.SceneGraph/SvgSceneCompiler.cs`
- `src/Svg.SceneGraph/SvgSceneDocument.cs`
- `src/Svg.SceneGraph/SvgSceneNode.cs`
- `src/Svg.SceneGraph/SvgSceneResource.cs`
- `src/Svg.SceneGraph/SvgSceneRenderer.cs`
- `src/Svg.SceneGraph/SvgSceneRuntime.cs`

### 5. Major-release rendering cutover away from drawable-first public rendering APIs

Because this work targets a future major release, the branch starts removing drawable-first public rendering flows instead of preserving backward-compatible shims.

This cutover includes:

- routing `SKSvg` render/model creation through retained scene compilation
- removing `SvgService.ToDrawable(...)` / `SvgService.ToModel(...)`
- switching `Svg.Controls.Avalonia`, source generation, and CLI/sample consumers to retained-scene model generation
- shrinking drawable usage down to remaining internal compatibility seams instead of public rendering APIs

Key files:

- `src/Svg.Skia/SKSvg.Model.cs`
- `src/Svg.Model/Services/SvgService.cs`
- `src/Svg.Controls.Avalonia/SvgSource.cs`
- `src/Svg.SourceGenerator.Skia/SvgSourceGenerator.cs`
- `samples/SvgToPng/ViewModels/MainWindowViewModel.cs`
- `samples/svgc/Program.cs`

### 6. Animation performance and invalidation follow-up

The branch adds layered animation redraw and then pushes further toward retained-scene incremental behavior.

This includes:

- static/animated layer caching
- subtree animation invalidation
- retained-scene-driven animation fallback rendering
- fixes for inherited animated attributes and resource-driven invalidation
- benchmark coverage for frame advancement

Key files:

- `src/Svg.Skia/SKSvg.AnimationLayers.cs`
- `tests/Svg.Skia.Benchmarks/SvgAnimationFrameBenchmarks.cs`

### 7. Project structure cleanup

The branch splits the new architecture into dedicated projects:

- `src/Svg.SceneGraph/Svg.SceneGraph.csproj`
- `src/Svg.Animation/Svg.Animation.csproj`

This keeps:

- retained-scene runtime/compiler/resource logic in `Svg.SceneGraph`
- shared animation runtime in `Svg.Animation`
- SkiaSharp conversion, host integration, and `SKSvg` façade code in `Svg.Skia`

It also removes unshipped obsolete compatibility APIs and replaces reflection-based internal access with explicit runtime bridges where possible.

## Documentation And Planning Artifacts

The branch adds and updates implementation docs and plans for the new architecture, including:

- `plan/svg-interaction-animation-phased-implementation.md`
- `plan/svg-retained-scene-graph-rewrite-spec.md`
- `plan/remaining-scene-graph-animation-work-plan.md`
- `plan/scene-graph-major-release-cutover-plan.md`
- `plan/animation-project-split-plan.md`

User-facing docs were also refreshed earlier in the branch, including:

- `README.md`
- `CHANGELOG.md`
- `site/articles/guides/interaction-and-animation.md`
- package docs for `Svg.Custom`, `Svg.Skia`, `Svg.Controls.Skia.Avalonia`, and `Svg.Controls.Skia.Uno`

## Validation

Branch work included repeated validation with:

- `dotnet format Svg.Skia.slnx --no-restore`
- `dotnet build Svg.Skia.slnx -c Release`
- `dotnet test Svg.Skia.slnx -c Release`

Recent validation specifically covered:

- successful build of the new `Svg.SceneGraph` split
- successful build of the new `Svg.Animation` split
- retained-scene regression coverage for background-image filters, invalid filter suppression, and hit testing
- targeted resvg regression slices for `e-feDiffuseLighting` and malformed `e-feConvolveMatrix`

Expanded tests include work in:

- `tests/Svg.Model.UnitTests/*`
- `tests/Svg.Skia.UnitTests/HitTestTests.cs`
- `tests/Svg.Skia.UnitTests/SvgAnimationControllerTests.cs`
- `tests/Svg.Skia.UnitTests/SvgRetainedSceneGraphTests.cs`
- `tests/Svg.Skia.UnitTests/SKSvgNativeCompositionTests.cs`
- `tests/Svg.Controls.Avalonia.UnitTests/SvgSourceTests.cs`
- `tests/Svg.Controls.Skia.Avalonia.UnitTests/SvgSourceTests.cs`

## Architectural Outcome

The branch is no longer just “animation support.”

It now delivers:

- shared SVG animation and interaction support
- retained native composition support in Avalonia
- a retained scene graph runtime with direct DOM compilation
- major-release migration away from drawable-first rendering APIs
- separate `Svg.SceneGraph` and `Svg.Animation` projects

The remaining long-term direction is clear:

- `SvgDocument` remains the DOM/source model
- `Svg.SceneGraph` is the retained render/runtime representation
- `Svg.Animation` owns shared animation timing/evaluation/runtime behavior
- `Svg.Skia` becomes the Skia/host integration layer on top of those shared runtimes
