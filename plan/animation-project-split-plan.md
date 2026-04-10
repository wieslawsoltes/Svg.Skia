# Animation Project Split Plan

## Goal

Split the reusable animation runtime out of `src/Svg.Skia` into a dedicated project, similar to `src/Svg.SceneGraph`, while keeping `SKSvg`-specific render orchestration inside `Svg.Skia`.

## Why

- reduce `Svg.Skia` assembly scope to Skia/render-host integration
- make the animation runtime reusable by controls and future backends without pulling in all of `SKSvg`
- isolate trim/AOT-sensitive animation logic behind a smaller assembly boundary
- align the architecture with the retained-scene split already introduced by `Svg.SceneGraph`

## Clean Boundary

### Move to `src/Svg.Animation`

- `SvgAnimationClock`
- `SvgAnimationController`
- `SvgAnimationFrameState`
- `SvgAnimationHostBackend` and resolver/capability types
- `SvgNativeCompositionScene`, `SvgNativeCompositionFrame`, `SvgNativeCompositionLayer`
- shared animation invalidation policy extracted from `SKSvg.AnimationLayers.cs`

### Keep in `src/Svg.Skia`

- `SKSvg.Model.cs` animation orchestration
- `SKSvg.AnimationLayers.cs`
- `SKSvg.NativeComposition.cs`
- `SKSvg.SceneGraph.cs` retained-scene mutation/render preparation
- SkiaSharp picture conversion, picture registration/disposal, and drawing

Reason: those files are partial `SKSvg` implementation details and depend directly on `SKSvg` state, `SkiaModel`, and render lifecycle synchronization.

## Dependency Rules

### `Svg.Animation`

- may reference:
  - `Svg.Custom`
  - `Svg.Model`
  - `ShimSkiaSharp`
  - `SkiaSharp`
- must not depend on:
  - `SKSvg`
  - `Svg.Controls.*`
  - retained-scene orchestration partials

### `Svg.Skia`

- references:
  - `Svg.Animation`
  - `Svg.SceneGraph`
  - existing model/custom projects

## Required Supporting Changes

1. Add `InternalsVisibleTo` from `Svg.Custom` to `Svg.Animation`.
2. Add `src/Svg.Animation/Properties/AssemblyInfo.cs` with `InternalsVisibleTo` for `Svg.Skia`.
3. Add the new project to `Svg.Skia.slnx`.
4. Update `Svg.Skia.csproj` to reference `Svg.Animation.csproj`.
5. Physically move animation runtime files from `src/Svg.Skia/Animation` to `src/Svg.Animation`.
6. Replace the inherited-animation-attribute table in `SKSvg.AnimationLayers.cs` with a shared helper owned by `Svg.Animation`.

## Validation

1. `dotnet format Svg.Skia.slnx --no-restore`
2. `dotnet build Svg.Skia.slnx -c Release`
3. `dotnet test Svg.Skia.slnx -c Release`

## Success Criteria

- `src/Svg.Skia/Animation` no longer contains the reusable runtime files
- `Svg.Skia` builds against the new project boundary
- Avalonia/Uno host backends and tests continue to compile against the moved types
- `SKSvg.AnimationLayers.cs` retains only `SKSvg`-bound logic
