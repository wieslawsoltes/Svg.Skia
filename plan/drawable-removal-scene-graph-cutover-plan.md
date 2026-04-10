# Drawable Removal And Scene Graph Cutover Plan

Status date: 2026-04-06
Branch: `feature/svg-animation-runtime`

## Purpose

This document records the retained scene graph migration status, summarizes the major fixes already delivered on this branch, and captures both the removal plan and the completed implementation status for deleting the legacy drawable subsystem.

## Branch Progress So Far

The branch already landed a large amount of retained-scene and animation stabilization work before the drawable-removal pass:

- `4fd9761e` fixed root `viewBox` animation invalidation.
- `8beb0318` fixed Uno animated picture lifetime after animation rebuilds.
- `124c222a`, `25a833fe`, `60e4a19b` fixed embedded SVG image recursion and added W3C coverage.
- `92637cc7`, `c160db8f` fixed retained recursive mask traversal and added regression coverage.
- `68d97490`, `9b0c8a4b` extracted animation parsing helpers and added parsing regressions.
- `0a3e9d29`, `e226d8e9`, `e3504346`, `9e86d693` fixed CI regressions around embedded SVG identity, duplicate image state, hidden-node bounds, and unresolved retained filters.
- `8b7d52eb`, `17c72a04`, `9d701138`, `547c2b05` fixed animation event bridge, timing, playback-rate clamping, and parser edge cases raised during PR review.
- `b7ce74c2` fixed the `animate-elem-38-t.svg` root `viewBox` regression by disabling retained caching/native composition when document-root animation targets are present.
- `d6f4d3fd`, `3fe70d7e` restored retained positioned text rendering for Windows CI and added regression coverage.

These fixes establish the retained scene graph as the primary `SKSvg` rendering path and close the major correctness gaps found during the migration.

## Current Architecture

### Main runtime path

The main `SKSvg` path is retained-scene-based:

- `SvgSceneRuntime.TryCompile(...)`
- `SvgSceneDocument`
- `SvgSceneRenderer.Render(...)`
- `SkiaModel.ToSKPicture(...)`

Key entry points:

- `src/Svg.Skia/SKSvg.Model.cs`
- `src/Svg.Skia/SKSvg.SceneGraph.cs`
- `src/Svg.SceneGraph/SvgSceneCompiler.cs`
- `src/Svg.SceneGraph/SvgSceneDocument.cs`
- `src/Svg.SceneGraph/SvgSceneRenderer.cs`

### Legacy drawable path

The drawable subsystem existed at the start of this pass under:

- `src/Svg.Model/Drawables/**`
- `src/Svg.Model/Services/HitTestService.cs`
- `src/Svg.Model/Editing/DrawableWalker.cs`
- `src/Svg.Model/Editing/DrawableEditingExtensions.cs`

That legacy path has now been removed from production code. The retained scene graph is the only runtime renderer and hit-test implementation.

## Implementation Status After This Pass

This pass completed the drawable retirement plan:

- deleted `src/Svg.Model/Drawables/**`
- deleted drawable-only infrastructure:
  - `src/Svg.Model/Editing/DrawableWalker.cs`
  - `src/Svg.Model/Editing/DrawableEditingExtensions.cs`
  - `src/Svg.Model/Services/HitTestService.cs`
  - `src/Svg.Model/Services/MarkerService.cs`
  - `src/Svg.Model/SvgFilterContext.cs`
- removed drawable-only APIs from shared services:
  - `PaintingService.RecordPicture(...)`
  - `PaintingService.CreatePicture(...)`
  - `PaintingService.GetFillPaint(...)`
  - `PaintingService.GetStrokePaint(...)`
  - `PaintingService.GetOpacityPaint(SvgElement)`
  - `MaskingService.GetSvgElementMask(...)`
- deleted drawable-only unit tests and clone coverage
- tightened the architecture guard so production code now allows zero `Svg.Model.Drawables` references

Result:

- retained scene graph is the only runtime renderer
- retained scene graph is the only runtime hit-test implementation
- retained scene graph is the only runtime implementation for filters, masks, and marker generation
- `Svg.Model` now contains only shared helpers and non-drawable services that are still needed by retained code

### Important dependency constraint

`Svg.SceneGraph` references `Svg.Model`, not the other way around:

- `Svg.Model` cannot directly call retained-scene compiler/renderer code today.
- This prevents reusing retained-scene compiler entry points from inside `Svg.Model`.

However, the deeper analysis for full drawable removal changes the conclusion:

- full drawable deletion does **not** require `Svg.Model` to render through `Svg.SceneGraph`
- all drawable types are internal implementation details
- the public `SKSvg` runtime already renders, filters, hit-tests, masks, and generates markers through retained-scene code

That means the lowest-risk path is to retire the legacy drawable renderer instead of preserving it. The dependency boundary still matters for cleanup sequencing, but it is **not** a blocker to deleting drawables completely.

## Delivered Cleanup

This cutover also shipped the follow-up cleanup needed to remove lingering drawable-era terminology and duplication:

- `SvgPatternPaintStateResolver` centralizes pattern inheritance and transform resolution for both `Svg.Model` and the retained scene graph.
- retained-scene internals now use `IsRenderable` terminology consistently instead of `IsDrawable`.
- the zero-reference architecture guard prevents production code from depending on `Svg.Model.Drawables` again.

## Final State Summary

The repository is in the intended post-cutover state:

- retained scene graph is the only `SKSvg` runtime render path
- retained scene graph is the only runtime hit-test path
- retained scene graph is the only runtime implementation for filters, masks, and marker generation
- `Svg.Model` contains only shared helpers and non-drawable services needed by retained code
- production code contains zero `Svg.Model.Drawables` references

## Validation Status

The drawable-removal cutover is considered complete because all of the following are true:

- no production runtime path calls `DrawableFactory.Create(...)`
- no production `src/**` file references `Svg.Model.Drawables`
- build passes for `Svg.Skia.slnx`
- full test suite passes for `Svg.Skia.slnx`

## Optional Follow-Up

Any remaining work is cosmetic rather than architectural:

- continue renaming editor/private members that still use historical `Drawable` wording
- simplify historical notes in this document if later branch history makes them obsolete

## Validation Plan

For each removal phase:

1. Run targeted unit tests for touched subsystems.
2. Run `dotnet build Svg.Skia.slnx -c Release`.
3. Run `dotnet test Svg.Skia.slnx -c Release`.
4. Keep W3C and resvg retained-scene regressions green, especially:
   - animated root `viewBox`
   - embedded SVG image recursion
   - recursive mask payloads
   - positioned text
   - pattern resource mutation coverage

## Immediate Next Steps After This Change

1. Split shared retained helpers out of `PaintingService` so its drawable-only path can be deleted instead of preserved.
2. Delete drawable-only editing and hit-test helpers that have no non-drawable production consumers.
3. Remove `SvgFilterContext`, `MarkerService`, and `GetSvgElementMask(...)` together with `src/Svg.Model/Drawables/**`.
4. Tighten the architecture guard allow-list to zero.
