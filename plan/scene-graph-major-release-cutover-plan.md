# Scene Graph Major-Release Cutover Plan

## Goal

Make the retained scene graph the only runtime rendering model for Svg.Skia.

For the major release:

- `SvgDocument` remains the parsed DOM/source model.
- `Svg.SceneGraph` becomes the only renderable runtime representation.
- `Svg.Skia` becomes a scene-to-Skia adapter and host/runtime integration layer.
- drawable-based runtime conversion APIs are removed from the public surface.
- old compatibility seams are removed instead of preserved.

## Status

Implemented in the current branch:

1. `SKSvg` runtime rendering paths now compile and render through `Svg.SceneGraph`.
2. `SvgService.ToDrawable(...)` and `SvgService.ToModel(...)` are removed.
3. `Svg.Controls.Avalonia`, `Svg.SourceGenerator.Skia`, `svgc`, and `SvgToPng` now use retained-scene model creation.
4. `Svg.SceneGraph` has its own project and owns retained runtime compilation/model creation.
5. drawable-based editor hit-selection fallback is removed.
6. `Svg.Model.Drawables` and drawable-only helpers are demoted to internal implementation instead of public runtime API.

Remaining work after this cutover is optional follow-up, not required for the major-release architectural switch:

1. delete drawable-only tests if the repo no longer wants to carry internal drawable implementation coverage
2. optionally move the remaining internal drawable implementation out of `Svg.Model` entirely if the team wants a stricter package split
3. optionally evolve native composition from retained-layer hosting into true host-native animation object mapping where parity can be preserved

## Current Gap Summary

The retained scene graph is already the primary runtime for rendering, hit testing, editor integration, incremental animation work, and native composition. The remaining cutover gaps are concentrated in legacy convenience APIs and a small number of drawable-era helper paths.

The main seams to remove are:

1. `SKSvg` still exposes drawable-shaped runtime state and static drawable-based helper paths.
2. `SvgService` still publishes `ToDrawable(...)` and `ToModel(...)`.
3. Some tooling and samples still depend on `SvgService.ToModel(...)` or drawable snapshots.
4. The scene graph still carries a stale `DrawableBridge` compilation strategy marker even though retained compilation is now the only intended strategy.

## Required End State

### Public/runtime architecture

1. `Svg.Model.Drawables` is no longer part of the supported runtime API surface.
2. `SKSvg` no longer exposes `Drawable`.
3. `SvgService` no longer exposes drawable-based render conversion methods.
4. All runtime rendering paths use:

`DOM -> Svg.SceneGraph -> Shim picture -> SkiaSharp picture`

### Packaging split

1. `Svg.SceneGraph` owns retained compilation, retained resources, retained hit testing, retained rendering, and retained model creation.
2. `Svg.Skia` owns SkiaSharp conversion, host animation/composition integration, and framework-facing helpers.
3. Consumers that need shim pictures should use retained-scene APIs instead of drawable conversion APIs.

### Tooling/sample cutover

1. `Svg.Controls.Avalonia` uses retained scene model creation.
2. `Svg.SourceGenerator.Skia` uses retained scene model creation.
3. `svgc` uses retained scene model creation.
4. `SvgToPng` uses retained scene model creation.

## Implementation Plan

### Phase 1: Introduce retained-scene render helpers

1. Add a public retained-scene runtime helper in `Svg.SceneGraph` for:
   - compiling an `SvgFragment` or `SvgDocument`
   - creating a shim `SKPicture`
2. Ensure fragment rendering handles non-document fragments without requiring drawable fallback.

### Phase 2: Remove drawable-based runtime entry points

1. Reimplement `SKSvg` static helper paths on top of retained scene rendering.
2. Reimplement `SKSvg` document rendering on top of retained scene rendering only.
3. Remove `SKSvg.Drawable`.
4. Remove drawable assignments from animation-layer and render paths.

### Phase 3: Migrate remaining consumers

1. Update `Svg.Controls.Avalonia` to use retained-scene picture creation.
2. Update `Svg.SourceGenerator.Skia` to use retained-scene picture creation.
3. Update `svgc` to use retained-scene picture creation.
4. Update `SvgToPng` to use retained-scene picture creation and remove stored drawable state.

### Phase 4: Remove drawable conversion APIs

1. Delete `SvgService.ToDrawable(...)`.
2. Delete `SvgService.ToModel(...)`.
3. Remove unused drawable-related imports from `SvgService` and `SKSvg`.

### Phase 5: Final architecture cleanup

1. Remove the stale `DrawableBridge` compilation strategy state.
2. Keep only retained compilation strategy metadata.
3. Run full format/build/test validation and fix fallout.

## Benefits

1. One runtime truth instead of parallel drawable and retained render trees.
2. Lower memory overhead by avoiding duplicated intermediate render objects.
3. Faster loading by removing `DOM -> drawable -> picture` conversion.
4. Cleaner incremental updates because retained nodes/resources remain the only mutable render state.
5. Cleaner public API for the major release.
6. Better future portability because retained scene graph code stays SkiaSharp-independent.

## Completion Criteria

This cutover is complete when:

1. no runtime-facing API in `SKSvg` exposes drawable state
2. no public `SvgService` API returns drawable-based render models
3. all remaining repo consumers use retained-scene model creation
4. full solution build and tests pass
