# Solar Battery SVG Processing Performance Plan

Status date: 2026-04-16
Branch: `perf/text-fallback-benchmarks`
Target asset: `/Users/wieslawsoltes/Downloads/solar battery.svg`

## Purpose

This plan records the measured hot paths for the current `solar battery.svg` workload, defines the
implementation order for load-to-render performance work, and documents the repository boundary for
SVG-library customizations.

## Repository Boundary

Any parser or DOM-loading behavior that would normally require changes to the external SVG library
must be implemented in `src/Svg.Custom/**`.

Do not patch `externals/SVG/**` for runtime SVG loading behavior unless there is no viable local
override in `Svg.Custom`.

Current relevant ownership split:

- Parse and loader compatibility work:
  - `src/Svg.Custom/Compatibility/SvgDocumentCompatibilityLoader.cs`
  - `src/Svg.Custom/Compatibility/SvgElementFactory.cs`
  - `src/Svg.Custom/Compatibility/SvgCssCompatibilityProcessor.cs`
- Compile and retained-scene work:
  - `src/Svg.SceneGraph/SvgSceneCompiler.cs`
  - `src/Svg.SceneGraph/SvgSceneDocument.cs`
  - `src/Svg.SceneGraph/SvgSceneTextCompiler.cs`
  - `src/Svg.SceneGraph/SvgSceneTextCompiler.PreparedText.cs`
- Path conversion work:
  - `src/Svg.Model/Services/PathingService.cs`
- Native picture and raster work:
  - `src/Svg.SceneGraph/SvgSceneRenderer.cs`
  - `src/Svg.Skia/SkiaModel.cs`
  - `src/Svg.Skia/SKPictureExtensions.cs`

## Current Baselines

Focused BenchmarkDotNet runs for `file:solar battery.svg` on this branch:

- Parse:
  - `SvgLoadPipelineBenchmarks.ParseSvgDocumentFromString` -> `294.974 us`
  - `SvgCustomParsePhaseBenchmarks.LoadStructureOnly` -> `252.561 us`
  - `SvgCustomAttributeDispatchBenchmarks.CreateElementsOnly` -> `212.070 us`
  - `SvgCustomAttributeDispatchBenchmarks.ApplyHotUnprefixedAttributesOnly` -> `158.217 us`
  - `SvgCustomAttributeDispatchBenchmarks.ApplyRemainingGeometryAttributesOnly` -> `1.089 us`
  - `SvgCustomParsePhaseBenchmarks.FlushStylesOnlyAfterStructureBuild` -> `372.902 us`
  - `SvgCustomParsePhaseBenchmarks.FinalizeAfterStructureBuild` -> `343.407 us`
  - `SvgCustomParsePhaseBenchmarks.ApplyCssCompatibilityOnly` -> `427.375 ns`
- Compile:
  - `SvgLoadPipelineBenchmarks.CompileRetainedSceneFromParsedDocument` -> `926.734 us`
  - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime` -> `507.523 us`
  - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneCompiler` -> `463.215 us`
  - `SvgRetainedSceneCompileBenchmarks.CreateSceneDocumentFromCompiledTree` -> `78.691 us`
  - `SvgRetainedSceneCompileBenchmarks.RegisterDependenciesOnly` -> `148.985 us`
  - `SvgRetainedSceneCompileBenchmarks.ResolveRuntimePayloadsOnly` -> `46.381 us`
  - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationOnly` -> `58.465 us`
  - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationAndRender` -> `341.318 us`
  - `SvgTextCompileInternalsBenchmarks.CreateTextMetricsPaintAcrossFragments` -> `4.382 us`
  - `SvgTextCompileInternalsBenchmarks.CreateTextMetricsPaintAcrossFragmentsHotWithinCompileScope` -> `3.978 us`
  - `SvgTextCompileInternalsBenchmarks.MeasureLineStatsAcrossFragments` -> `5.300 us`
  - `SvgTextCompileInternalsBenchmarks.MeasureLineStatsAcrossFragmentsHotWithinCompileScope` -> `3.999 us`
  - `SvgTextCompileInternalsBenchmarks.MeasureLineStatsAcrossFragmentsWithFreshAssetLoader` -> `10.232 us`
  - `SvgTextCompileInternalsBenchmarks.MeasureNaturalTextAdvanceAcrossFragments` -> `6.551 us`
  - `SvgTextCompileInternalsBenchmarks.MeasureNaturalTextAdvanceAcrossFragmentsWithFreshAssetLoader` -> `11.698 us`
- Path conversion:
  - `SvgPathConversionBenchmarks.ConvertAllVisualPaths` -> `130.873 us`
- Native picture creation:
  - `SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModel` -> `145.392 us`
  - `SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModelWithFreshSkiaModel` -> `449.696 us`
  - `SvgNativeSkPictureBenchmarks.GetCachedRetainedPicture` -> `8.437 ns`
  - `SvgNativeSkPictureBenchmarks.CreateNativePictureDirectFromRetainedSceneGraph` -> `139.530 us`
  - `SvgNativeSkPictureBenchmarks.CreateNativePictureFromRetainedSceneGraphViaShimModel` -> `143.795 us`
  - `SvgNativeSkPictureBenchmarks.CreateNativePictureFromTopLevelNodeModel` -> `133.534 us`
  - `SvgNativeSkPictureBenchmarks.CreateNativePictureDirectFromTopLevelRetainedNode` -> `135.475 us`
  - `SvgNativeSkPictureBenchmarks.GetCachedTopLevelRetainedSceneNodePicture` -> `20.215 ns`
  - `SvgNativeSkPictureBenchmarks.CreateNativePictureDirectFromTopLevelRetainedNodeClipped` -> `133.579 us`
  - `SvgNativeSkPictureBenchmarks.GetCachedTopLevelClippedRetainedSceneNodePicture` -> `24.849 ns`
  - `SvgNativeSkPictureBenchmarks.CreateNativePictureFromLeafNodeModel` -> `1.112 us`
  - `SvgNativeSkPictureBenchmarks.CreateNativePictureDirectFromLeafRetainedNode` -> `1.153 us`
  - `SvgNativeSkPictureBenchmarks.GetCachedLeafRetainedSceneNodePicture` -> `20.136 ns`
  - `SvgNativeSkPictureBenchmarks.CreateNativePictureDirectFromLeafRetainedNodeClipped` -> `1.138 us`
  - `SvgNativeSkPictureBenchmarks.GetCachedLeafClippedRetainedSceneNodePicture` -> `29.208 ns`
  - `SvgNativeSkPictureBenchmarks.ReplayFullModelIntoRecorderCanvasUsingCurrentLoop` -> `138.385 us`
  - `SvgNativeSkPictureBenchmarks.ReplayFullModelIntoRecorderCanvasUsingForLoopPerCommandDispatch` -> `136.479 us`
  - `SvgNativeSkPictureBenchmarks.ReplayFullModelIntoRecorderCanvasUsingForeachLoop` -> `135.499 us`
- Render:
  - `SvgRenderBitmapBenchmarks.DrawNativePicture1x` -> `294.202 us`
  - `SvgRenderBitmapBenchmarks.RenderTransparentBitmap1x` -> `306.514 us`
  - `SvgRenderBitmapBenchmarks.RenderTransparentBitmap1xIntoReusableBitmap` -> `295.940 us`
  - `SvgRenderBitmapBenchmarks.EncodeTransparentBitmap1x` -> `14.719 ms`
  - `SvgRenderBitmapBenchmarks.EncodeTransparentBitmap1xViaReusableSurface` -> `10.311 ms`
- End-to-end load:
  - `SvgLoadPipelineBenchmarks.LoadViaSkSvg` -> `2.044 ms`
  - `SvgLoadPipelineBenchmarks.LoadViaSkSvgFromStringWithBaseUri` -> `1.675 ms`
  - `SvgLoadPipelineBenchmarks.LoadViaSkSvgAndAccessModel` -> `1.974 ms`
    - latest short-run focused reruns on this branch for `file:solar battery.svg`
    - `LoadViaSkSvgFromStringWithBaseUri` is the current control-like inline-source acceptance metric

Additional load-path microbenchmarks for this branch:

- `SvgAnimationLoadBenchmarks.DetectAnimationElementsInParsedDocument` -> `826.539 ns`
- `SvgAnimationLoadBenchmarks.CreateAnimationControllerForParsedDocument` -> `3.604 us`
- `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` -> `950.611 us`
  - latest short-run rerun after the shared native render-path cache phase
  - replaces the prior `975.994 us` measurement on this branch as the current focused startup
    acceptance point
- `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` -> `526.260 us`
- `SvgTextAssetLoaderBenchmarks.GetFontMetricsSequenceCold` -> `11.466 us`
- `SvgTextAssetLoaderBenchmarks.TryShapeGlyphRunSequenceCold` -> `18.208 us`
- `SvgTextCompileInternalsBenchmarks.MeasureNaturalCodepointAdvancesAcrossFragmentsWithFreshAssetLoader` -> `12.582 us`
- `SvgSaveBenchmarks.SaveTransparentPng1x` -> `9.884 ms`

Additional retained-scene layer-bounds microbenchmarks for this branch:

- `SvgLayerBoundsBenchmarks.CreateRetainedSceneGraphPicture`:
  - `WrapperCount=32` -> `11.316 us`
  - `WrapperCount=128` -> `44.013 us`
  - `WrapperCount=256` -> `87.466 us`
- `SvgSingularLayerBoundsBenchmarks.DrawNativePicture1x`:
  - `WrapperCount=32` -> `4.986 us`
  - `WrapperCount=128` -> `10.351 us`
  - `WrapperCount=256` -> `16.844 us`

Additional text fallback microbenchmarks already used to drive recent work:

- `SvgTextAssetLoaderBenchmarks.FindTypefacesSingleCold` -> `61.148 us`
- `SvgTextAssetLoaderBenchmarks.FindTypefacesSequenceCold` -> `59.735 us`

## Completed Work On This Branch

- `b7430c3d6` `Optimize text fallback resolution`
  - added warm typeface-span caching in `SkiaSvgAssetLoader`
  - added a single-typeface fast path for cold fallback resolution
  - changed distinct codepoint collection from repeated `List.Contains(...)` scans to `HashSet<int>`
- `b4501f380` `Add focused SVG perf benchmarks`
  - added cold `FindTypefaces` microbenchmarks
  - added `SVG_SKIA_BENCHMARK_SCENARIO_FILTER` so runs can target `file:solar battery.svg`
- `daceffe9d` `Reduce sequential text compile passes`
  - removed duplicated sequential compile measurement work by collapsing preparation and resolved
    line-stat collection into one pass
- uncommitted
  - extended sequential prepared-text reuse so bounds can reuse prepared relative line stats and
    non-left aligned draw can reuse prepared fallback draw data
  - added a focused `MeasureLineStatsAcrossFragments` microbenchmark for text compile internals
- uncommitted
  - added a compile-scope `MeasureLineStats` cache that is entered automatically by scene compile
    transactions and reused across text measure/draw passes
  - routed general text draw and bounds fallback paths through cached prepared line stats when a
    compile scope is active
  - added a hot compile-scope `MeasureLineStatsAcrossFragmentsHotWithinCompileScope` microbenchmark
- uncommitted
  - reduced retained-scene rebuild duplication in `SvgSceneDocument` by:
    - collapsing subtree dependent-address registration and referenced dependency registration into
      one source-document traversal
    - reusing compilation-root lookup data from `ReindexNodes()` instead of rebuilding it from a
      separate scene-tree pass
  - improved `SvgRetainedSceneCompileBenchmarks.RegisterDependenciesOnly` from `179.797 us` to
    `124.947 us`
  - moved `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime` to `875.472 us`
  - moved `SvgRetainedSceneCompileBenchmarks.CompileViaSceneCompiler` to `954.659 us`
- uncommitted
  - added `SvgCustomParsePhaseBenchmarks` so parse work can be measured as:
    - structure build
    - CSS compatibility application
    - style flush
    - final parse post-processing
  - changed `SvgDocumentCompatibilityLoader` finalization to:
    - skip the recursive style flush when no styles were staged
    - use a `Svg.Custom` fast flush path instead of the upstream LINQ-heavy flush loop
    - skip content aggregation scans for elements that never received text nodes
  - moved `SvgLoadPipelineBenchmarks.ParseSvgDocumentFromString` from `339.201 us` to `334.161 us`
  - confirmed that `solar battery.svg` parse cost is dominated by style flush, while CSS
    compatibility work is effectively zero for this asset
- uncommitted
  - added `SvgCustomAttributeDispatchBenchmarks` to isolate element creation plus
    `SvgElementFactory.SetAttributes(...)`
  - removed the extra `XmlReader.ReadAttributeValue()` walk from `SetAttributes(...)`
  - replaced namespace lookup-by-prefix with direct `reader.NamespaceURI` usage for prefixed
    attributes
  - moved `SvgCustomAttributeDispatchBenchmarks.CreateElementsOnly` from `239.717 us` to
    `229.096 us`
  - moved `SvgLoadPipelineBenchmarks.ParseSvgDocumentFromString` from `334.161 us` to
    `324.113 us`
- uncommitted
  - extended `SvgCustomAttributeDispatchBenchmarks` with `ApplyHotUnprefixedAttributesOnly` to
    replay the real `solar battery.svg` mix of `id`, `transform`, `d`, `x`, `y`, `width`, and
    `height` setters on fresh elements
  - added direct unprefixed property fast paths in `SvgElementFactory.SetPropertyValue(...)` for:
    - `id`
    - `transform`
    - `d` on `SvgPath`
    - `x` and `y` on `SvgTextBase`, `SvgRectangle`, and `SvgFragment`
    - `width` and `height` on `SvgRectangle` and `SvgFragment`
  - moved `SvgCustomAttributeDispatchBenchmarks.ApplyHotUnprefixedAttributesOnly` from
    `164.239 us` to `158.217 us`
  - moved `SvgCustomAttributeDispatchBenchmarks.CreateElementsOnly` from `216.539 us` to
    `212.070 us`
  - moved `SvgLoadPipelineBenchmarks.ParseSvgDocumentFromString` from `324.113 us` to
    `320.835 us`
- uncommitted
  - added subtree-aware staged-style tracking in `Svg.Custom` so style flush can skip clean
    branches instead of recursing through the full document tree
  - propagated staged-style dirtiness through inline styles, CSS compatibility, presentation
    attributes, and child attachment during DOM creation
  - kept the change entirely inside `Svg.Custom`, without modifying the external `Svg` library
  - moved `SvgLoadPipelineBenchmarks.ParseSvgDocumentFromString` from `320.835 us` to
    `303.818 us`
  - observed that isolated short-run style-flush/finalize subphase means became noisy after this
    change, so the acceptance signal for this step remains the full parse benchmark
- uncommitted
  - extended `SvgCustomAttributeDispatchBenchmarks` with
    `ApplyRemainingGeometryAttributesOnly` to replay the actual `viewBox`, `cx`, `cy`, `r`, `rx`,
    and `ry` mix used by `solar battery.svg`
  - added direct unprefixed geometry fast paths in `SvgElementFactory.SetPropertyValue(...)` for:
    - `viewBox` on `ISvgViewPort`, `SvgSymbol`, and `SvgPatternServer`
    - `cx` and `cy` on `SvgCircle`, `SvgEllipse`, and `SvgRadialGradientServer`
    - `r` on `SvgCircle` and `SvgRadialGradientServer`
    - `rx` and `ry` on `SvgRectangle` and `SvgEllipse`
  - moved `SvgCustomAttributeDispatchBenchmarks.ApplyRemainingGeometryAttributesOnly` from
    `1.314 us` to `1.089 us`
  - moved `SvgLoadPipelineBenchmarks.ParseSvgDocumentFromString` from `303.818 us` to
    `294.974 us`
- uncommitted
  - added reusable raster APIs in `SKPictureExtensions`:
    - `TryGetImageInfo(...)` so callers can create correctly-sized reusable raster targets
    - `ToBitmap(...)` overloads that render into caller-supplied `SKBitmap` and optional
      reusable `SKCanvas`
    - `ToImage(...)` overload that encodes via a caller-supplied reusable `SKSurface`
  - added focused unit coverage for reusable bitmap and reusable surface rendering
  - extended `SvgRenderBitmapBenchmarks` with reusable bitmap and reusable surface encode paths
  - moved `SvgRenderBitmapBenchmarks.EncodeTransparentBitmap1x` from `14.719 ms` to
    `13.683 ms` via the reusable-surface path
  - confirmed that reusable bitmap rendering stays in the same cost band as direct draw on
    `solar battery.svg`, so raster allocation is no longer the dominant render-side limiter here
  - reduced retained-scene command emission in `SvgSceneRenderer` by skipping base
    `Save`/`Restore` pairs on nodes that do not mutate canvas state, and by letting opacity-only
    or mask-only wrappers rely on `SaveLayer(...)` without an extra base save
  - added retained-scene regression coverage for:
    - state-free structural wrappers
    - opacity-only wrappers
    - transform-scoped wrappers that still require save/restore
  - moved `SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModel` from `142.304 us` to
    `135.499 us`
  - moved `SvgRenderBitmapBenchmarks.DrawNativePicture1x` from `3.460 ms` to `3.415 ms`
  - moved `SvgRenderBitmapBenchmarks.RenderTransparentBitmap1x` from `3.541 ms` to `3.423 ms`
  - moved `SvgRenderBitmapBenchmarks.RenderTransparentBitmap1xIntoReusableBitmap` from
    `3.612 ms` to `3.389 ms`
  - moved `SvgRenderBitmapBenchmarks.EncodeTransparentBitmap1xViaReusableSurface` from
    `13.683 ms` to `13.256 ms`
  - changed retained-scene native picture APIs to record directly from `SvgSceneDocument`
    instead of always going through `CreateModel()` plus `ToSKPicture(model)`:
    - `SKSvg.CreateRetainedSceneGraphPicture()`
    - `SKSvg.CreateRetainedSceneNodePicture(...)`
    - `SKSvg.CreateRetainedScenePicture(...)`
  - added a direct-vs-shim native picture equivalence test for retained-scene output
  - added focused native benchmarks that compare:
    - direct retained-scene native recording
    - the older retained-scene shim-model-to-native route
    - replay from an already-built shim model
  - moved `SvgNativeSkPictureBenchmarks.CreateNativePictureFromRetainedSceneGraphViaShimModel`
    from `138.061 us` to `135.617 us` when using the new direct retained-scene API path
  - confirmed that direct retained-scene native recording is still slightly slower than replaying
    an already-built shim model (`134.179 us`), so the next native phase should target
    `SkiaModel.Draw(...)` replay itself rather than more scene traversal work
- uncommitted
  - optimized shim-picture replay in `SkiaModel.Draw(SKPicture, ...)` around restore-count replay and
    picture-level command dispatch, instead of routing every recorder command back through the public
    single-command API
  - added `SkiaModelReplayTests.Draw_OptimizedReplayMatchesPerCommandDispatch` to lock replay
    correctness against a manual per-command native recording path
  - extended `SvgNativeSkPictureBenchmarks` with per-command replay microbenchmarks:
    - `ReplayFullModelIntoRecorderCanvasUsingForLoopPerCommandDispatch`
    - `ReplayFullModelIntoRecorderCanvasUsingForeachLoop`
  - moved `SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModel` from `135.499 us` to
    `134.822 us`
  - the replay-loop microbenchmarks now sit in a tight `135-138 us` band on `solar battery.svg`,
    which suggests future native work should focus on reducing emitted command count or reusing
    native pictures instead of more loop-shape micro-ops
- uncommitted
  - added bounds-aware `SaveLayerCanvasCommand` recording and replay so shim pictures preserve
    resolved layer rectangles through cloning, C# code generation, and native recorder playback
  - bounded retained-scene opacity, mask, filter, and mask-composite layers in both
    `SvgSceneRenderer` and direct retained-scene native recording when subtree paint bounds are
    available in the current local coordinate space
  - added regression coverage for save-layer bounds cloning and retained-scene opacity wrappers
  - moved `SvgRenderBitmapBenchmarks.DrawNativePicture1x` from `3.395 ms` to `294.202 us`
  - moved `SvgRenderBitmapBenchmarks.RenderTransparentBitmap1x` from `3.474 ms` to `306.514 us`
  - moved `SvgRenderBitmapBenchmarks.RenderTransparentBitmap1xIntoReusableBitmap` from `3.415 ms`
    to `295.940 us`
  - moved `SvgRenderBitmapBenchmarks.EncodeTransparentBitmap1xViaReusableSurface` from
    `13.336 ms` to `10.311 ms`
  - kept `SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModel` essentially flat at
    `134.318 us`, which confirms the gain is coming from playback and raster work rather than
    recorder construction
- uncommitted
  - brought animation-layer static-model recording to parity with the retained-scene renderer by
    scoping base save/restore to nodes that actually mutate canvas state and by bounding mask,
    opacity, and filter layers to resolved subtree paint bounds
  - added a regression test that inspects `_staticAnimationLayerModel` and verifies cached static
    opacity wrappers now record bounded `SaveLayerCanvasCommand`s
  - moved `SvgAnimationFrameBenchmarks.AdvanceAndDrawLayeredFrame` on the cached layered path from:
    - `8.238 ms` to `295.892 us` for `StaticElementCount=64`, `AnimatedElementCount=4`
    - `9.136 ms` to `1.194 ms` for `StaticElementCount=64`, `AnimatedElementCount=16`
    - `32.836 ms` to `929.121 us` for `StaticElementCount=256`, `AnimatedElementCount=4`
    - `35.229 ms` to `3.410 ms` for `StaticElementCount=256`, `AnimatedElementCount=16`
  - spot-checked `SvgRenderBitmapBenchmarks.DrawNativePicture1x` on `file:solar battery.svg` at
    `292.559 us`, which keeps the non-animation render path in the same band as the prior
    `294.202 us` baseline
- uncommitted
  - bounded filtered `textPath` `SaveLayer(...)` recording in `SvgSceneTextCompiler` by reusing
    the resolved filter clip when present and falling back to pixel-aligned run bounds otherwise
  - added a filtered `textPath` render microbenchmark seam plus a generated
    `generated-filtered-text-path-curves-48` scenario to keep this path measurable
  - added a retained-scene regression test that verifies filtered `textPath` output now records
    non-empty `SaveLayerCanvasCommand.Bounds`
  - moved `SvgRenderBitmapBenchmarks.DrawNativePicture1x` on
    `generated-filtered-text-path-curves-48` from `996.811 us` to `946.315 us`
  - treated `file:solar battery.svg` as neutral for this phase because it does not materially use
    filtered `textPath` rendering, so the generated filter-heavy scenario is the acceptance signal
- uncommitted
  - added cached subtree renderable paint bounds on `SvgSceneNode` and refresh them during full
    retained-scene rebuilds plus targeted mutation refreshes, which turns repeated layer-bound
    resolution from repeated subtree walks toward hot cached lookup
  - fixed targeted mutation refresh to pass the current in-tree compilation roots back into
    `RefreshMutationSubtrees(...)` instead of detached replacement roots
  - added `SvgLayerBoundsBenchmarks` so wrapper-heavy retained-scene recording stays measurable
  - added `SKSvgLayerBoundsTests.RetainedSceneMutation_RefreshesOpacityLayerBoundsAfterGeometryChange`
    to lock the mutation path against stale opacity-layer clipping
  - moved `SvgLayerBoundsBenchmarks.CreateRetainedSceneGraphPicture` from:
    - `15.831 us` to `11.316 us` at `WrapperCount=32`
    - `125.512 us` to `44.013 us` at `WrapperCount=128`
    - `455.277 us` to `87.466 us` at `WrapperCount=256`
  - measured `SvgNativeSkPictureBenchmarks.CreateNativePictureDirectFromRetainedSceneGraph` at
    `139.530 us` on `file:solar battery.svg`, which keeps the real asset roughly in the prior band
    while preserving a lead over the shim-model route at `143.795 us`
  - measured `SvgSaveBenchmarks.SaveTransparentPng1x` at `9.884 ms`, which showed the public save
    path is already in the same encode band as the lower-level picture helper and did not justify a
    separate reusable-surface phase
- uncommitted
  - added cached local subtree renderable paint bounds on `SvgSceneNode` and switched retained
    scene, direct native recording, and animation-layer `ResolveCurrentLayerBounds(...)` to use the
    local cache instead of inverse-mapping world bounds
  - closed the remaining singular-transform opacity or mask `SaveLayer(...)` fallback path, so
    bounded layer recording no longer depends on `node.TotalTransform.TryInvert(...)`
  - added `SKSvgLayerBoundsTests.RetainedSceneGraph_UsesBoundedSaveLayerUnderSingularAncestorTransform`
    to lock this regression path
  - added `SvgSingularLayerBoundsBenchmarks` to keep singular-transform layer playback measurable
  - measured `SvgSingularLayerBoundsBenchmarks.DrawNativePicture1x` at:
    - `4.986 us` for `WrapperCount=32`
    - `10.351 us` for `WrapperCount=128`
    - `16.844 us` for `WrapperCount=256`
  - spot-checked `SvgRenderBitmapBenchmarks.DrawNativePicture1x` on `file:solar battery.svg` at
    `301.109 us`, which keeps the real asset in the same short-run render band while this phase
    targets the singular-transform fallback scenario directly
- uncommitted
  - added `SvgAnimationLoadBenchmarks` to separate:
    - static-document animation-element detection
    - full `SvgAnimationController` construction over a parsed document
  - changed `SKSvg.LoadSvgDocument(...)` to skip `SvgAnimationController` construction when the
    parsed document contains no animation elements
  - added `LoadStaticSvg_DoesNotCreateAnimationController` coverage so the static-document path
    stays explicitly locked to `AnimationController == null`
  - measured the new load-side animation work at:
    - `DetectAnimationElementsInParsedDocument` -> `826.539 ns`
    - `CreateAnimationControllerForParsedDocument` -> `3.604 us`
  - concluded that animation-controller setup is not the dominant `solar battery.svg` cold-load
    cost, so this change is a correctness-preserving micro-optimization rather than the main
    accepted load win for this phase
- uncommitted
  - added shared process-wide default-provider typeface caches in `SkiaSvgAssetLoader` for:
    - `MatchCharacter(...)`
    - provider-family lookup
    - `FindTypefaces(...)` span caching
  - added shared process-wide default-provider typeface caches in `SkiaModel` for:
    - `ToSKTypeface(...)`
    - resolved family/style matching
  - only use the shared caches when no custom `TypefaceProviders` are configured, so provider-local
    behavior still stays instance-scoped and invalidation-safe
  - improved `SvgTextAssetLoaderBenchmarks.FindTypefacesSequenceCold` from `64.547 us` to
    `59.735 us`
  - observed that `SvgLoadPipelineBenchmarks.LoadViaSkSvg` remains noisy in short-run BDN around
    `5.5 ms`, so the acceptance signal for this phase is the cold text-fallback microbenchmark plus
    profiler guidance rather than the short-run end-to-end load mean alone
- uncommitted
  - added `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` to keep the fresh static
    `SKSvg` startup path measurable without parse noise
  - changed `SKSvg.LoadSvgDocument(...)` to skip animation-state teardown and retained-scene
    invalidation when a fresh instance has no prior document, picture, retained-scene, or native
    composition state to clear
  - added fast no-op exits in `DisableAnimationLayerCaching()` and
    `InvalidateRetainedSceneGraph()` so fresh static loads do not pay reset or disposal machinery
    before the first compile
  - added `LoadStaticSvg_AfterAnimatedSvg_ClearsAnimationState` so reused `SKSvg` instances still
    clear animation-layer state correctly when switching from animated to static content
  - measured the fresh parsed-document static load path at:
    - `LoadParsedDocumentIntoFreshSkSvg` -> `3.235 ms`
  - reran the ad hoc profiler and short-run `LoadViaSkSvg` benchmark, but kept them as diagnostic
    only because they remained noisier than the new focused startup microbenchmark
- uncommitted
  - collapsed fresh-instance output publication and reset cleanup so the first retained-scene
    picture assignment skips cache-clear work when no prior picture state exists, and `Reset()`
    clears retained-scene state in one pass instead of clearing output caches and then calling a
    second retained-scene invalidation pass
  - reused `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` as the acceptance metric
    for this phase because it includes both fresh load and disposal cost without parse noise
  - improved `LoadParsedDocumentIntoFreshSkSvg` from `3.235 ms` to `3.023 ms`
- uncommitted
  - made default `SKSvgSettings` startup lazier:
    - defer `Srgb` and `SrgbLinear` color-space creation until the properties are first accessed
    - defer default typeface-provider list creation until `TypefaceProviders` is explicitly read or
      written
    - route internal runtime/font-cache code through `ConfiguredTypefaceProviders` so untouched
      fresh instances stay on the shared default-provider fast path instead of materializing a
      per-instance provider list
  - reused `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` as the acceptance metric
    for this phase because constructor and fresh-instance setup cost are part of that measurement
  - repeated short-run reruns landed in the `1.739-2.050 ms` band
  - latest rerun improved `LoadParsedDocumentIntoFreshSkSvg` from `3.023 ms` to `1.739 ms`
- uncommitted
  - added compile-scope text-metrics paint template caching so `CreateTextMetricsPaint(...)`
    reuses resolved text-size, direction, encoding, and shim typeface state across one compile
    transaction without sharing mutable `SKPaint` instances
  - added focused compile microbenchmarks for:
    - `CreateTextMetricsPaintAcrossFragments`
    - `CreateTextMetricsPaintAcrossFragmentsHotWithinCompileScope`
  - moved `SvgTextCompileInternalsBenchmarks.CreateTextMetricsPaintAcrossFragmentsHotWithinCompileScope`
    from `6.541 us` on the initial clone-based cache attempt to `3.978 us` with template reuse
  - moved `SvgTextCompileInternalsBenchmarks.MeasureLineStatsAcrossFragmentsHotWithinCompileScope`
    from `3.470 us` to `3.010 us`
  - moved `SvgLoadPipelineBenchmarks.CompileRetainedSceneFromParsedDocument` from `1.030 ms` to
    `926.734 us`
  - observed that the standalone cold `MeasureLineStatsAcrossFragments` benchmark stays roughly flat
    around `182 us`, which confirms this phase is specifically improving compile-scope reuse rather
    than changing the uncached measurement path

## Phase Analysis

## Parse

Measured cost is relatively small for this asset, but parser work still dominates first-load time
after the recent compile improvements.

Current likely hotspots:

- `SvgDocumentCompatibilityLoader.Create(...)`
  - full XML walk plus per-node stack management
  - current measured `LoadStructureOnly`: `252.561 us`
- `SvgElement.FlushStyles(true)` via the compatibility loader finalization path
  - last stable isolated measurement `FlushStylesOnlyAfterStructureBuild`: `372.902 us`
  - after subtree-dirty tracking, short-run isolated flush/finalize means became noisy, so the
    decision signal for this step is the full parse benchmark instead of the subphase-only mean
- `SvgElementFactory.SetAttributes(...)`
  - attribute-name branching and property dispatch
  - after the latest fast path, measured `CreateElementsOnly`: `212.070 us`
  - representative hot unprefixed setter replay is `158.217 us`
  - the remaining geometry-only setter replay is now `1.089 us` for this asset and is no longer a
    first-order parse concern
- `SvgCssCompatibilityProcessor.Apply(...)`
  - effectively negligible for `solar battery.svg` at `427.375 ns`

Likely optimization opportunities:

- add finer-grained parse microbenchmarks so parser time is split into:
  - XML read/tree creation
  - attribute/property application
  - stylesheet compatibility processing
  - recursive `FlushStyles(true)`
- keep the style-aware flush fast path and no-text-node aggregation skip in `Svg.Custom`
- track whether a subtree actually has staged styles so flush can skip clean child branches in
  O(k) over dirty subtrees instead of O(n) over the entire parsed tree
- add fast paths in `SvgElementFactory.SetAttributes(...)` for common presentation attributes to
  reduce repeated namespace/property dispatch overhead
- avoid temporary content aggregation work on nodes that can never surface `Content`
- avoid extra attribute reader state transitions when the raw `XmlReader.Value` is already enough
- only add more direct setter fast paths if remaining geometry attributes beat the maintenance cost
- stop parser-specific tuning on this asset once full parse drops below the current render/native
  cost tier

Implementation order:

1. Completed: benchmark subphases inside `Svg.Custom`.
2. Completed: add a style-aware fast path for post-parse style flushing.
3. Completed: skip content aggregation scans for elements that never stage text nodes.
4. Completed: add an attribute-dispatch microbenchmark and remove avoidable attribute-reader work.
5. Completed: benchmark and add direct fast paths for the hottest unprefixed property setters.
6. Completed: add subtree-dirty style tracking so post-parse flush can skip clean branches.
7. Completed: benchmark and fast-path the remaining geometry setters that still occur on this asset.
8. Next: stop parser-specific chasing on `solar battery.svg` unless a new benchmark moves parse
   back into the top cost tier.

Acceptance gate:

- `ParseSvgDocumentFromString` improves on `solar battery.svg` without changing
  `SvgDocumentCompatibilityLoader` regression behavior.

## Compile

Compile remains the highest-leverage controllable phase for this asset.

Current likely hotspots after the recent fixes:

- repeated line-stat and bounds work across draw/measure/compile sequential text paths
- mutation-specific scene-document rebuild stages after `RebuildIndexesAndDependencies()`:
  - targeted dirty-subtree/resource invalidation work
- repeated `CreateTextMetricsPaint(...)` and fallback resolution across nearby text code paths

Likely optimization opportunities:

- extend prepared sequential runs so draw and measure can reuse cached bounds and resolved typeface
  information, not just advance
- add a scoped line-stats cache keyed by text/style/viewport for the current compile transaction
- cache `CreateTextMetricsPaint(...)` results for the current compile transaction using immutable
  paint templates instead of mutable paint reuse
- review `SvgSceneDocument` multi-pass rebuild work:
  - any resource-graph work that still scales with full-document traversal on mutation
- investigate whether the direct compiler path can avoid some runtime-payload work for text-only
  nodes during full compile

Implementation order:

1. Completed: reuse prepared sequential text data in draw and bounds paths.
2. Completed: add compile-scope `MeasureLineStats` caching.
3. Completed: reduce repeated `SvgSceneDocument` full-tree dependency-registration passes.
4. Completed: cache text-metrics paint templates across the compile scope.
5. Completed: gate retained-scene runtime paint payload materialization to filter-using nodes and
   resolve stroke width without building a full stroke paint.
6. Completed: share `MeasureLineStats(...)` results across fresh default-path loaders and compile
   runs using the existing text-measurement cache-key lane instead of only within a compile scope.
7. Revisit the `Pretext`-style prepared text abstraction only after the current engine’s reuse
   boundaries are exhausted.

Acceptance gate:

- direct retained-scene compile stays below the prior `1.316 ms` baseline while keeping load-pipeline
  compile within short-run noise.

## Paths

Path conversion is not the next priority for this asset.

Reasons:

- `ConvertAllVisualPaths` is `130.873 us`, well below compile and render
- `solar battery.svg` is text-heavy enough that path work is not the first-order driver

When to revisit:

- after parse and compile plateau
- or if a new workload is shape-heavy and moves path conversion into the top cost tier

Likely future opportunities:

- retained-node path caching with invalidation keyed by element mutation
- reuse of converted primitive paths for stable geometry
- avoid per-call temporary allocations in path conversion helpers only if node-level caching is not
  sufficient

## Native Picture, Render, Encode

These phases are now more expensive than compile for `solar battery.svg`, but most of the cost is
either Skia replay or allocation-heavy rasterization.

Current measured costs:

- native picture creation from a full shim model: `134.318 us`
- cached retained-scene native picture access: `8.437 ns`
- retained-scene native picture via direct scene traversal: `136.916 us`
- retained-scene native picture via shim model then native conversion: `140.114 us`
- top-level node native picture from a shim model: `133.534 us`
- top-level node direct retained-scene native picture: `135.475 us`
- cached top-level retained-node native picture access: `20.215 ns`
- leaf node native picture from a shim model: `1.112 us`
- leaf node direct retained-scene native picture: `1.153 us`
- cached leaf retained-node native picture access: `20.136 ns`
- shim replay via `Draw(SKPicture, ...)`: `138.385 us`
- shim replay via manual per-command `for` dispatch: `136.479 us`
- shim replay via manual per-command `foreach` dispatch: `135.499 us`
- picture draw to reusable bitmap: `294.202 us`
- `ToBitmap(...)` with internal allocation: `306.514 us`
- `ToBitmap(...)` into caller-supplied reusable bitmap/canvas: `295.940 us`
- `ToImage(...)` with internal surface allocation: `14.719 ms`
- `ToImage(...)` via caller-supplied reusable surface: `10.311 ms`
- full load via `SKSvg`: `6.325 ms`

Likely optimization opportunities:

- native replay-loop micro-ops are now clustered tightly on this asset, so further dispatch-shape
  tuning is unlikely to buy much more
- top-level retained-scene native-picture reuse is now effectively free on the warm path, so more
  work here should focus on node-scoped reuse or command-count reduction before replay rather than
  more top-level native picture generation work
- no-clip retained-node native-picture reuse is also effectively free on the warm path, which left
  clip-aware node exports and callers that require owned fresh pictures as the remaining reuse gaps
  before this phase
- clip-aware retained-node native-picture reuse is now effectively free on the warm path too, and
  the selection-export tooling path now uses that cache instead of forcing fresh clipped recording
- retained-scene opacity, mask, and filter layers now record with resolved local paint bounds when
  they are available, which cuts offscreen raster work from effectively full-canvas layer playback
  toward the actual painted subtree bounds on this asset
- retained-scene subtree paint bounds are now cached per node after compile and targeted mutation
  refresh, so wrapper-heavy layer-bound resolution no longer rescans the same descendants on every
  opacity or mask layer
- cached animation-layer static recording now uses the same bounded-layer and conditional base-save
  policy, which removes full-canvas offscreen work from the layered animation draw path too
- filtered `textPath` runs now reuse filter-region bounds when recording their filtered
  `SaveLayer(...)` path in `SvgSceneTextCompiler`, which removes another full-canvas fallback from
  filter-heavy text-path playback
- reusable bitmap/surface APIs still matter for repeated raster workloads, especially on the encode
  path where reusable surfaces already produced the clearest win

The retained-scene renderer now scopes save/restore work to nodes that actually mutate canvas
state. For wrapper-heavy trees, base state-frame emission moves from effectively `O(n)` over all
visited nodes toward `O(k)` over nodes that introduce clip, transform, or filter-clip state.

Implementation order:

1. Completed: add reusable raster APIs and benchmark them.
2. Completed: reduce command emission in retained-scene output where it does not change semantics.
3. Completed: evaluate direct native recording after the current command-emission reductions.
4. Completed: optimize `SkiaModel.Draw(...)` replay on the existing shim-model path.
5. Completed: add cached top-level retained-scene native-picture reuse for repeated access.
6. Completed: add cached no-clip retained-node native-picture reuse for repeated access.
7. Completed: add cached clip-aware retained-node native-picture reuse for repeated access.
8. Completed: bound retained-scene `SaveLayer(...)` recording to resolved subtree paint bounds for
   opacity, mask, and filter wrappers.
9. Completed: bring animation-layer static recording to parity with bounded retained-scene layer
   recording.
10. Completed: bound filtered `textPath` layer recording in `SvgSceneTextCompiler`.
11. Completed: cache subtree renderable paint bounds so repeated layer-bound resolution stops
    rescanning wrapper-heavy retained-scene subtrees.
12. Completed: cache subtree renderable paint bounds in local node space so singular-transform
    wrappers no longer fall back to unbounded `SaveLayer(...)` recording.
13. Next: if native work continues, prioritize only the remaining real-world `SaveLayer(...)`
    fallbacks where bounds are still unavailable after local-space caching, and only keep chasing
    them when a benchmarked scenario shows raster cost that matters on top of the current
    `solar battery.svg` profile.

Acceptance gate:

- render benchmarks improve without regressing API ergonomics or retained-scene correctness.

## Load-Path Startup Reuse

Cold load is now a separate concern from native render throughput on this asset.

Profiler guidance from `SvgLoadPipelineProfiler` for `/Users/wieslawsoltes/Downloads/solar battery.svg`
showed:

- parse from string: `1.51 ms`
- retained-scene compile from parsed document: `3.22 ms`
- create shim picture model: `0.03 ms`
- create native `SKPicture`: `0.69 ms`
- render native picture to bitmap: `0.61 ms`
- encode native picture to PNG: `12.06 ms`
- load via `SKSvg.FromSvg`: `9.92 ms`

Current likely hotspots and observations:

- static-document animation-controller setup is measurable but tiny on this asset:
  - animation-element detection is `826.539 ns`
  - controller construction is `3.604 us`
  - skipping controller creation on static documents is still worthwhile, but not a first-order
    load win for `solar battery.svg`
- fresh static `SKSvg` startup still includes non-trivial instance-reset work around document-load
  invalidation:
  - `LoadParsedDocumentIntoFreshSkSvg` now isolates that path at `3.023 ms`
  - on a fresh instance, animation-layer teardown and retained-scene invalidation should converge
    toward `O(1)` no-op checks instead of paying cleanup and disposal paths that only matter after a
    prior load
- publishing and disposing the first static picture also matters on the fresh-instance path:
  - first-picture publication and reset cleanup now stay closer to `O(1)` field assignment when no
    prior picture, model, wireframe, or retained-picture caches exist
  - the accepted signal for that step is the `LoadParsedDocumentIntoFreshSkSvg` reduction from
    `3.235 ms` to `3.023 ms`
- untouched `SKSvgSettings` construction also used to allocate default providers and color spaces on
  every fresh instance:
  - lazy default settings now avoid that upfront work unless font-provider or color-space access is
    actually needed
  - the accepted signal for that step is the repeated `LoadParsedDocumentIntoFreshSkSvg` band moving
    from `3.023 ms` down into the `1.739-2.050 ms` range
- default typeface and fallback resolution had remained instance-local:
  - fresh `SKSvg` / `SkiaModel` / `SkiaSvgAssetLoader` instances were rebuilding the same default
    family and glyph fallback answers across the same process
  - this kept repeated cold-start fallback work closer to per-instance `O(n)` cache fill instead of
    converging toward process-wide warm `O(1)` lookup after the first load
- native render-paint materialization had also remained identity-scoped per fresh model:
  - first native picture recording rebuilt equivalent simple `SKPaint` objects whenever a fresh
    `SkiaModel` saw the same shim paint values through new object identities
  - this kept first-record startup closer to per-instance `O(n)` paint conversion over repeated
    simple paints instead of converging toward warm template reuse across fresh instances
- the new shared-cache path only activates for the default-provider case:
  - custom provider behavior remains isolated per instance so provider mutation and invalidation
    semantics do not change

Likely optimization opportunities:

- keep using microbenchmarks to separate loader setup costs from parse/compile work before changing
  the end-to-end load path
- treat short-run `LoadViaSkSvg` numbers as directional only unless a change is large enough to
  dominate the noise band
- prefer shared caches only for process-stable default lookups and keep custom-provider state
  instance-scoped
- keep fresh-instance startup benchmarks separate from parse and compile so small loader-state
  improvements stay measurable
- if load remains the next priority, profile repeated fresh-instance load scenarios before chasing
  another sub-millisecond startup optimization

Implementation order:

1. Completed: benchmark animation-element detection and controller setup separately.
2. Completed: skip `SvgAnimationController` construction for static documents.
3. Completed: share default-provider typeface and fallback caches across fresh instances.
4. Completed: skip fresh-instance animation and retained-scene reset work when there is no prior
   state to clear, and benchmark that path directly with `LoadParsedDocumentIntoFreshSkSvg`.
5. Completed: skip fresh-instance output cache clearing when publishing the first recorded picture,
   and collapse `Reset()` to one retained-scene clear pass.
6. Completed: make default `SKSvgSettings` color-space and provider setup lazy, while keeping the
   internal runtime on the no-custom-provider fast path until callers opt in.
7. Completed: share simple render-paint templates across fresh default-path `SkiaModel` instances
   so first native picture recording can reuse warm paint setup without changing custom-provider
   semantics.
8. Completed: share default-path text advance cache keys across fresh asset loaders so
   `SvgSceneTextCompiler` can reuse simple and natural codepoint advance caches across fresh
   default-path `SKSvg` loads without sharing custom-provider state.
9. Next: only continue load-startup chasing if longer-run `LoadViaSkSvg` or profiler traces point
   to another stable hotspot larger than the current noise band; otherwise stop this lane on
   `solar battery.svg`.

Acceptance gate:

- a cold-load microbenchmark or profiler-derived hotspot improves without changing custom-provider
  semantics or static-document behavior.

## Execution Plan

## Phase 1: Stabilize Compile Wins

- keep the current text fallback and sequential compile improvements
- completed: extend sequential prepared-text reuse into draw and measure paths
- completed: add compile-scope `MeasureLineStats` caching
- completed: reduce repeated `SvgSceneDocument` rebuild passes
- completed: cache text-metrics paint templates across the compile scope
- rerun:
  - `*SvgLoadPipelineBenchmarks*CompileRetainedSceneFromParsedDocument*`
  - `*SvgRetainedSceneCompileBenchmarks*CompileViaSceneCompiler*`
  - `*SvgTextCompileInternalsBenchmarks*`
  - `*SvgRetainedSceneCompileBenchmarks*RegisterDependenciesOnly*`
  - `*SvgRetainedSceneCompileBenchmarks*CreateSceneDocumentFromCompiledTree*`

## Phase 2: Decompose Parse Cost In Svg.Custom

- completed:
  - added parser microbenchmarks that isolate:
    - structure build
    - CSS compatibility pass
    - recursive flush-styles pass
    - final parse post-processing
  - added a dedicated attribute-dispatch microbenchmark for `SvgElementFactory.SetAttributes(...)`
  - implemented only `Svg.Custom` changes for loader behavior
  - reduced the parse finalization path with style-aware flush and no-text-content aggregation
- completed:
  - reduced attribute-loop overhead in `SvgElementFactory.SetAttributes(...)`
- completed:
  - benchmarked and optimized the hottest unprefixed property setters
- completed:
  - added subtree-dirty staged-style tracking so `Svg.Custom` style flush skips clean branches
  - improved `SvgLoadPipelineBenchmarks.ParseSvgDocumentFromString` from `320.835 us` to
    `303.818 us`
  - treated isolated flush/finalize subphase means as diagnostic only after this change because
    short-run BDN variance was too high to use as a replacement baseline
- completed:
  - benchmarked and optimized the remaining unprefixed geometry setters still present on this asset
  - improved `SvgLoadPipelineBenchmarks.ParseSvgDocumentFromString` from `303.818 us` to
    `294.974 us`
  - confirmed that parse is no longer the highest-leverage local phase on `solar battery.svg`
- next:
  - pause parse-specific work on this asset and move to raster/native reuse
- rerun:
  - `*SvgXmlDomParseBenchmarks*`
  - `*SvgCustomParsePhaseBenchmarks*`
  - `*SvgCustomAttributeDispatchBenchmarks*`

## Phase 3: Scene-Document Rebuild Efficiency

- completed in current compile pass for initial full builds:
  - collapsed dependency registration into one source-document traversal
  - removed an extra scene-tree pass for compilation-root lookup
- completed:
  - folded the remaining `AnalyzeDependencyRequirements()` prepass into the real full-rebuild
    traversal by rebuilding resource graph state and compilation-root dependencies together
  - kept the isolated dependency-registration helpers intact for mutation and benchmark coverage
  - improved `SvgRetainedSceneCompileBenchmarks.CreateSceneDocumentFromCompiledTree` from
    `112.101 us` to `109.231 us`
  - kept `CompileViaSceneRuntime`/`CompileViaSceneCompiler` inside the existing short-run compile
    band for `solar battery.svg`
- completed:
  - profiled `ResolveRuntimePayloads()` and removed filter-only fill/stroke paint materialization
    from filter-free retained nodes
  - separated retained-node stroke-width resolution from stroke-paint construction so bounds and
    hit-testing can keep the scalar width without paying stroke-paint setup cost
  - improved `SvgRetainedSceneCompileBenchmarks.ResolveRuntimePayloadsOnly` from `133.833 us` to
    `46.381 us`
  - improved `SvgRetainedSceneCompileBenchmarks.CreateSceneDocumentFromCompiledTree` from
    `109.231 us` to `78.691 us`
  - kept filtered retained nodes safe with a dedicated paint-source filter regression test
- completed:
  - added targeted mutation-subtree index/dependency refresh for safe non-resource attribute
    updates instead of always forcing `RebuildIndexesAndDependencies()`
  - added focused hot mutation benchmarks for retained-scene mutation-only and mutation-plus-render
    paths on `file:solar battery.svg`
  - measured `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationOnly` at `58.465 us`
  - measured `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationAndRender` at `341.318 us`
  - rechecked the coarse `SvgLoadPipelineProfiler` mutation path and kept the note that the
    nested shared-`use` mutation test remains a pre-existing failure outside this phase
- next if compile remains a priority:
  - only revisit mutation compile work when a new profiler trace shows fallback full-scene rebuilds
    dominating a real workload
- rerun:
  - `*SvgRetainedSceneCompileBenchmarks*`
  - `*SvgRetainedSceneMutationBenchmarks*`
  - mutation scenarios in `SvgLoadPipelineProfiler`

## Phase 4: Raster And Native Reuse

- completed:
  - added reusable-bitmap and reusable-surface render helpers in `SKPictureExtensions`
  - benchmarked direct draw, reusable bitmap render, and encode paths separately
  - confirmed reusable-surface encode improves from `14.719 ms` to `13.683 ms`
  - confirmed reusable bitmap rendering stays within draw-noise on this asset, so command replay
    is the next higher-leverage target
- completed:
  - shared positioned `SKTextBlob` reuse across fresh default-path `SkiaModel` instances for
    stable shim-picture replay while keeping custom-provider models instance-scoped
  - added focused regression coverage in `SkiaModelReplayTests` for default-path reuse and
    custom-provider isolation
  - improved `SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModelWithFreshSkiaModel`
    from `493.923 us` to `487.027 us`
  - reran `SvgLoadPipelineProfiler`, but kept `Create native SKPicture` as diagnostic-only because
    the short profiler pass stayed noisy at `0.92 ms`
- next:
  - stop native-recording micro-ops on `solar battery.svg` unless a benchmark or profiler points to
    a larger retained-scene recording hotspot than the current low-single-digit fresh-model delta
- rerun:
  - `*SvgNativeSkPictureBenchmarks*`
  - `*SvgRenderBitmapBenchmarks*`
  - `*SvgLoadPipelineBenchmarks*LoadViaSkSvg*`

## Phase 5: Load Startup Reuse

- completed:
  - added `SvgAnimationLoadBenchmarks` to separate static-document animation detection from full
    controller construction
  - gated `SvgAnimationController` creation behind a parsed-document animation-element scan so
    static SVG loads do not allocate the controller path
  - added shared default-provider typeface caches across fresh `SkiaModel` and
    `SkiaSvgAssetLoader` instances
  - improved `SvgTextAssetLoaderBenchmarks.FindTypefacesSequenceCold` from `64.547 us` to
    `59.735 us`
- completed:
  - removed eager shim-model creation from the static `SKSvg` load path by recording the current
    native picture directly from the retained scene graph and lazily materializing `Model` only on
    first access
  - added `SvgLoadPipelineBenchmarks.LoadViaSkSvgAndAccessModel` plus deferred-model regression
    coverage in `SKSvgRebuildFromModelTests`
  - measured `SvgLoadPipelineBenchmarks.LoadViaSkSvg` at `5.581 ms`
  - measured `SvgLoadPipelineBenchmarks.LoadViaSkSvgAndAccessModel` at `5.745 ms`, which keeps the
    deferred shim-model access delta to about `0.164 ms` on `solar battery.svg`
  - treated the plain `LoadViaSkSvg` mean as directional only because it remains inside the current
    short-run noise band, and used the deferred-model delta as the acceptance signal for this step
- completed:
  - added `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` so fresh static `SKSvg`
    document load can be measured without parse noise or end-to-end `LoadViaSkSvg` variance
  - changed `SKSvg.LoadSvgDocument(...)` to skip state-reset work when a fresh instance has no
    prior animation, retained-scene, picture, or native composition state to clear
  - added fast no-op exits in `DisableAnimationLayerCaching()` and
    `InvalidateRetainedSceneGraph()` for the fresh-instance path
  - added `LoadStaticSvg_AfterAnimatedSvg_ClearsAnimationState` coverage to keep reused-instance
    cleanup behavior locked
  - measured `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` at `3.235 ms`
  - treated the rerun of `SvgLoadPipelineProfiler` and short-run `LoadViaSkSvg` as diagnostic only
    because they remained inside the current startup noise band
- completed:
  - skipped output-cache clearing when a fresh static `SKSvg` publishes its first recorded picture,
    because there is no prior picture/model/wireframe/retained-picture state to tear down
  - collapsed `Reset()` into one retained-scene clear pass instead of clearing output state and then
    calling a second retained-scene invalidation
  - improved `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` from `3.235 ms` to
    `3.023 ms`
- completed:
  - deferred default `SKSvgSettings` color-space and default typeface-provider allocation until the
    properties are actually accessed
  - kept runtime typeface resolution on the shared no-custom-provider fast path by switching
    internal font code to `ConfiguredTypefaceProviders`
  - repeated short-run reruns moved `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg`
    into the `1.739-2.050 ms` band, with the latest rerun at `1.739 ms`
- completed:
  - added `SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModelWithFreshSkiaModel` to
    isolate first-record native picture cost when a fresh `SkiaModel` sees a stable shim picture
  - shared simple render-paint templates across fresh default-path `SkiaModel` instances while
    keeping custom-provider text paints on the existing instance-scoped path
  - measured `SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModelWithFreshSkiaModel` at
    `493.923 us`
  - improved `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` from `1.739 ms` to
    `1.444 ms`
  - reran `SvgLoadPipelineProfiler`, but kept `Load via SKSvg.FromSvg` and control-like source load
    as diagnostic-only signals because they remained noisy at `9.03 ms` and `8.08 ms`
- completed:
  - added `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` to keep
    fresh default-path retained-scene compile measurable separately from fresh `SKSvg` state setup
  - added `SvgTextAssetLoaderBenchmarks.GetFontMetricsSequenceCold` and
    `SvgTextAssetLoaderBenchmarks.TryShapeGlyphRunSequenceCold` to expose cold asset-loader text
    paint reuse on `solar battery.svg`
  - routed `SkiaSvgAssetLoader.GetFontMetrics(...)` and `TryShapeGlyphRun(...)` through the
    existing native paint cache instead of rebuilding a native `SKPaint` on every call
  - added shared simple paint templates across fresh default-path `SkiaSvgAssetLoader` instances so
    cold loader metrics and shaping can reuse warm native paint setup without changing
    custom-provider semantics
  - improved `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` from `1.444 ms` to
    `1.351 ms`
  - measured `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` at
    `885.799 us`
  - measured `SvgTextAssetLoaderBenchmarks.GetFontMetricsSequenceCold` at `11.466 us`
  - measured `SvgTextAssetLoaderBenchmarks.TryShapeGlyphRunSequenceCold` at `18.208 us`
- next:
  - only continue load-startup chasing when a longer-run `LoadViaSkSvg` benchmark or profiler trace
    points to a hotspot larger than the current short-run noise band
- rerun:
  - `*SvgAnimationLoadBenchmarks*`
  - `*SvgTextAssetLoaderBenchmarks*`
  - `*SvgRetainedSceneCompileBenchmarks*CompileViaSceneRuntimeWithFreshAssetLoader*`
  - `*SvgLoadPipelineBenchmarks*LoadViaSkSvg*`
- completed:
  - added `ISvgTextMeasurementCacheKeyProvider` so text compile caches can opt into provider-stable
    keys instead of always using loader identity
  - shared simple and natural codepoint advance caches across fresh default-path
    `SkiaSvgAssetLoader` instances while keeping custom-provider loaders instance-scoped
  - added `SvgTextCompileInternalsBenchmarks.MeasureNaturalCodepointAdvancesAcrossFragmentsWithFreshAssetLoader`
    to isolate fresh-loader cache reuse without the rest of `SKSvg` startup
  - added unit coverage for compiler cache reuse across fresh loaders with matching keys and for
    default-vs-custom `SkiaSvgAssetLoader` cache-key semantics
  - reran `SvgLoadPipelineProfiler` and reduced `Compile retained scene (parsed doc)` from `6.63 ms`
    to `3.94 ms` mean on `/Users/wieslawsoltes/Downloads/solar battery.svg`
  - reran short-run BDN acceptance benchmarks and treated `LoadParsedDocumentIntoFreshSkSvg`
    (`1.495 ms`) plus `CompileViaSceneRuntimeWithFreshAssetLoader` (`1.031 ms`) as directional only
    because they did not move cleanly enough to replace the profiler signal
- completed:
  - shared `MeasureLineStats(...)` results across fresh default-path loaders by keying a process
    cache off the existing text-measurement cache lane plus resolved text-metrics paint state and
    bidi-affecting inherited text attributes
  - added `SvgTextCompileInternalsBenchmarks.MeasureLineStatsAcrossFragmentsWithFreshAssetLoader`
    to isolate fresh-loader line-stats reuse outside a compile scope
  - added unit coverage for shared line-stats cache reuse across fresh loaders with matching keys
    and for isolation when the cache keys differ
  - improved `SvgTextCompileInternalsBenchmarks.MeasureLineStatsAcrossFragmentsWithFreshAssetLoader`
    to `10.232 us`
  - improved `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` from
    `1.031 ms` to `553.290 us`
- completed:
  - used the longer-run profiler to pick the next remaining compile seam instead of adding another
    blind micro-optimization; the chosen target was uncached natural text-width measurement
  - shared `MeasureNaturalTextAdvance(...)` across fresh default-path loaders with a dedicated
    cache keyed by the existing text-measurement lane plus resolved text paint and bidi-affecting
    inherited text attributes
  - switched natural text/codepoint measurement setup to `CreateTextMetricsPaint(...)` so compile
    scopes reuse the existing text-metrics paint template cache instead of rebuilding paint state
  - added `SvgTextCompileInternalsBenchmarks.MeasureNaturalTextAdvanceAcrossFragmentsWithFreshAssetLoader`
    to isolate the fresh-loader path
  - added unit coverage for shared natural text-advance cache reuse across fresh loaders with
    matching keys and for isolation when the keys differ
  - improved `SvgTextCompileInternalsBenchmarks.MeasureNaturalTextAdvanceAcrossFragments` from
    `166.805 us` to `6.551 us`
  - measured `SvgTextCompileInternalsBenchmarks.MeasureNaturalTextAdvanceAcrossFragmentsWithFreshAssetLoader`
    at `11.698 us`
  - improved `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` from
    `553.290 us` to `527.647 us`
- completed:
  - reran `SvgLoadPipelineProfiler` after the recent compile and startup work to choose the next
    phase from longer-run stage data instead of another isolated microbenchmark
  - current profiler means on `/Users/wieslawsoltes/Downloads/solar battery.svg` were:
    - parse `1.43 ms`
    - compile retained scene `2.34 ms`
    - create native `SKPicture` `0.51 ms`
    - render native picture to bitmap `0.33 ms`
    - encode native picture to PNG `10.45 ms`
    - load via `SKSvg.FromSvg` `4.63 ms`
    - control-like source load `4.38 ms`
    - mutate plus retained rebuild `5.00 ms`
  - used the retained-scene compile benchmark breakdown to confirm the remaining compile cost is
    still dominated by node-tree compile (`415.904 us`) rather than scene-document rebuild
    bookkeeping (`98.641 us`)
  - added compile-scope solid stroke paint caching for direct path visuals so repeated solid-color,
    non-dashed strokes reuse cached `SKPaint` templates alongside the existing solid fill cache
  - the compile acceptance rerun moved:
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime` from `551.773 us` to `526.866 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` from
      `545.524 us` to `538.821 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly` from `415.904 us` to `418.588 us`
  - treated this as a modest keep rather than a new optimization lane: full runtime compile
    improved, but node-tree-only stayed in the short-run noise band, so further compile-local paint
    caching is not justified for `solar battery.svg` without a new profiler signal
- completed:
  - implemented a public save-path reuse cache on `SKSvg` keyed by the current native picture plus
    export parameters, so repeated identical `Save(...)` calls can write the previously encoded
    bytes instead of re-rasterizing and re-encoding on every call
  - kept the optimization scoped to warm repeated saves only: cache misses still use the normal
    render-plus-encode path, and picture replacement paths clear the cached payload before the next
    save
  - added regression coverage for repeated static saves producing identical bytes and for animated
    saves changing after the frame advances
  - improved `SvgSaveBenchmarks.SaveTransparentPng1x` on
    `/Users/wieslawsoltes/Downloads/solar battery.svg` from `9.884 ms` to `1.393 us`
- completed:
  - switched the cold `SKPictureExtensions.ToImage(...)` path from `SKSurface.Snapshot() +
    SKImage.Encode() + SKData.SaveTo(...)` to direct bitmap/pixmap stream encoding, so cold export
    no longer pays the extra snapshot and encoded-buffer copy step before writing to the caller
    stream
  - updated the blank-model `SKSvg.Save(...)` fallback to use `SKBitmap.Encode(Stream, ...)`
    directly for the same reason
  - added `SvgSaveBenchmarks.SaveTransparentPng1xAfterPictureRefresh` to keep cold public save
    misses measurable now that the warm repeated-save cache exists
  - added `SKPictureExtensionsTests.ToImage_WithAllocatedBitmapPath_WritesEncodedImage`
  - focused `solar battery.svg` acceptance reruns moved:
    - `SvgLoadPipelineBenchmarks.EncodeNativePictureToPng` from the last profiler baseline around
      `10.33 ms` to `9.986 ms`
    - `SvgRenderBitmapBenchmarks.EncodeTransparentBitmap1xViaReusableSurface` from `10.311 ms` to
      `9.983 ms`
    - new cold public save benchmark:
      `SvgSaveBenchmarks.SaveTransparentPng1xAfterPictureRefresh` -> `10.404 ms`
  - treated this as a bounded cold-export win rather than a new long lane: export is still
    dominated by PNG encoding itself, but the wrapper overhead around the encoder is now smaller and
    directly measurable
- completed:
  - collapsed the fresh static `SKSvg` publish path so a freshly compiled retained scene and its
    first native `SKPicture` are published together instead of taking the general replacement path
    through separate retained-scene assignment, first-picture publication checks, and an extra
    `Picture` lookup on return
  - skipped `ReplaceAnimationController(null)` on the static path when there is no existing
    controller to clear
  - added `SKSvgRebuildFromModelTests.FromSvgDocument_LeavesModelDeferredUntilRequested` so the
    parsed-document fast path stays locked to deferred shim-model materialization
  - improved `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` from `997.076 us` to
    `975.994 us` on `/Users/wieslawsoltes/Downloads/solar battery.svg`
  - treated the focused fresh-load benchmark as the acceptance signal for this step; a profiler
    rerun during parallel validation was discarded because concurrent build/test load polluted the
    timings
- completed:
  - threaded `baseUri` through the stream SVG parser path instead of only patching
    `SvgDocument.BaseUri` after parse, so stream loads now resolve relative stylesheet imports
    during document load the same way path and `XmlReader` loads already do
  - removed original-source bookkeeping from `SKSvg.Load(...)`/`Load(path)`/`Load(reader)` hot
    paths when `CacheOriginalStream` is disabled, keeping reload metadata only when reload support
    is actually enabled
  - added `SKSvgTests.Load_StreamWithBaseUri_AppliesImportedStylesheets` to lock the stream-load
    `baseUri` behavior
  - focused `solar battery.svg` load acceptance reruns moved:
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvg` from `2.187 ms` to `1.844 ms`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvgAndAccessModel` to `1.954 ms`
  - a clean profiler rerun stayed effectively flat on the longer stream-load stage at
    `Control-like source load` `4.46 ms` versus the prior `4.48 ms`, so this phase is kept as a
    measured focused load-entry improvement rather than a profiler-visible whole-pipeline shift
- completed:
  - added direct string SVG entry points with parameter and `baseUri` support in
    `SvgDocumentCompatibilityLoader`, `SvgService`, and `SKSvg`, so inline-source callers no
    longer need to round-trip `string -> UTF-8 bytes -> MemoryStream` just to preserve relative
    resource resolution
  - routed Avalonia and Uno inline-source loaders through the new direct string path and kept their
    clone/reload semantics by storing original inline SVG text alongside existing path/stream state
  - added `SKSvgTests.FromSvg_WithBaseUri_AppliesImportedStylesheets` to lock string-load
    stylesheet import resolution with `baseUri`
  - added `SvgLoadPipelineBenchmarks.LoadViaSkSvgFromStringWithBaseUri` and switched the profiler's
    `Control-like source load` stage to the real direct string path
  - focused `solar battery.svg` acceptance reruns moved:
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvgFromStringWithBaseUri` -> `1.806 ms`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvg` -> `2.044 ms`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvgAndAccessModel` -> `1.974 ms`
  - clean single-process profiler reruns moved:
    - `Control-like source load` from `4.46 ms` to `3.87 ms`
    - `Load via SKSvg.FromSvg` from `4.58 ms` to `4.14 ms`
  - this closes the control-like string-source conversion lane for `solar battery.svg`; the
    remaining end-to-end load time is now dominated by parse/compile work rather than string-entry
    wrapper overhead
- completed:
  - added a simple-static `SKSvg.Reset()` fast path for fresh instances that never entered
    animation/native-composition state, so short-lived static loads no longer pay the full
    animation/cache teardown path on dispose
  - kept the optimization scoped to instances with no animation controller, no retained native
    picture caches, no wireframe/model state, and no active draws; animated or tool-heavy
    instances still use the existing full reset path
  - targeted load-state regressions stayed green:
    - `LoadStaticSvg_DoesNotCreateAnimationController`
    - `LoadStaticSvg_AfterAnimatedSvg_ClearsAnimationState`
    - `SKSvgRebuildFromModelTests`
  - focused `solar battery.svg` acceptance reruns moved:
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvg` from `2.044 ms` to `1.595 ms`
  - supporting reruns were treated as directional only:
    - `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` measured `998.914 us`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvgAndAccessModel` measured `2.203 ms` with wide short-run variance
  - this keeps the phase as an end-to-end `SKSvg` instance-lifecycle win rather than a retained-scene
    compile win; the next target should still come from the remaining whole-pipeline hotspots, not
    another local compile micro-phase
- completed:
  - added a shared parsed inline-style declaration cache in
    `SvgInlineStyleAttributeParser`, kept entirely inside `Svg.Custom`, so repeated identical
    `style="..."` strings no longer pay full tokenization and normalization on every reuse
  - added `SvgInlineStyleAttributeBenchmarks` plus a benchmark-only cache reset seam to measure
    the cold-vs-warm inline-style path directly on real style strings from
    `/Users/wieslawsoltes/Downloads/solar battery.svg`
  - targeted inline-style regressions stayed green:
    - `SvgDocumentCompatibilityLoaderTests.FromSvg_ParsesInlineStyleAttributesWithQuotedSemicolons`
    - `SvgDocumentCompatibilityLoaderTests.FromSvg_ParsesInlineStyleAttributesWithComments`
    - `SvgDocumentCompatibilityLoaderTests.FromSvg_ParsesInlineStyleAttributesWithTrailingCommentsInsideDeclaration`
    - `SvgDocumentCompatibilityLoaderTests.FromSvg_ParsesInlineStyleAttributesCaseInsensitively`
    - `SvgDocumentCompatibilityLoaderTests.FromSvg_ParsesInlineStyleAttributesWithEmptyDeclarationsAndWhitespace`
  - focused `solar battery.svg` microbenchmark reruns moved:
    - `SvgInlineStyleAttributeBenchmarks.ApplyInlineStylesOnlyColdCache` -> `255.494 us`
    - `SvgInlineStyleAttributeBenchmarks.ApplyInlineStylesOnlyWarmSharedCache` -> `55.558 us`
  - supporting parser reruns moved:
    - `SvgCustomParsePhaseBenchmarks.LoadStructureOnly` -> `234.445 us`
    - `SvgLoadPipelineBenchmarks.ParseSvgDocumentFromString` -> `295.422 us`
  - kept this as a parser-internal microbenchmark win rather than a new full-parse acceptance
    win: the isolated inline-style lane improved sharply and structure-build moved down, but the
    full parse benchmark stayed in the prior `~295 us` band for this asset
- completed:
  - tightened the source-document retained-scene dependency walk in `SvgSceneDocument` so
    `RegisterCompilationRootDependencies(...)` now skips address-key creation and
    `VisitReferencedElements(...)` when no compilation root is active for the current subtree, and
    also skips the reference visitor entirely for element types that cannot reference other
    elements
  - rejected a larger batched dependency-registration attempt during this phase because it improved
    `CreateSceneDocumentFromCompiledTree` but regressed `RegisterDependenciesOnly`; the kept change
    is only the cheap branch-level skip above
  - targeted retained-scene dependency regressions stayed green:
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_ApplyMutation_UpdatesUseDependents`
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_ApplyMutation_UpdatesGradientResourceDependents`
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_ApplyMutation_UpdatesFilterResourceDependents`
  - focused `solar battery.svg` compile reruns moved:
    - `SvgRetainedSceneCompileBenchmarks.RegisterDependenciesOnly` from `295.762 us` to `208.553 us`
    - `SvgRetainedSceneCompileBenchmarks.CreateSceneDocumentFromCompiledTree` from `109.231 us` to `95.916 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneCompiler` from `509.366 us` to `480.950 us`
  - supporting reruns stayed mixed but acceptable:
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime` measured `519.920 us`, still in the
      existing short-run variance band
    - the 30-sample manual profiler kept `Compile retained scene (parsed doc)` around `3.03 ms`, so
      this phase is being kept as a retained-scene dependency microbenchmark win rather than a new
      profiler-visible whole-pipeline compile reduction
    - the same profiler rerun moved `Load via SKSvg.FromSvg` from the prior `6.06 ms` mean to
      `5.72 ms`, but that trace is still too noisy to treat as the acceptance metric for this phase
- completed:
  - reduced cold public `SKSvg.Save(...)` wrapper overhead by reusing the encoded `MemoryStream`
    backing buffer directly when available instead of always forcing
    `MemoryStream -> ToArray() -> stream.Write(...)` on the first save miss
  - targeted save regressions stayed green:
    - `SKSvgTests.Save_RepeatedStaticCalls_ProduceIdenticalBytes`
    - `SKSvgTests.Save_EmptyRootDocument_WritesBlankViewportPng`
    - `SvgAnimationControllerTests.Save_SucceedsWhenAnimationLayerCachingIsActive`
  - focused `solar battery.svg` export reruns moved:
    - `SvgSaveBenchmarks.SaveTransparentPng1xAfterPictureRefresh` from `10.404 ms` to `9.958 ms`
    - `SvgLoadPipelineBenchmarks.EncodeNativePictureToPng` measured `9.847 ms`
    - warm repeated `SvgSaveBenchmarks.SaveTransparentPng1x` stayed effectively free at `1.390 us`
  - this is being kept as a cold public-save wrapper win rather than a codec win: the remaining
    export cost is now much closer to raw `EncodeNativePictureToPng`, which means the dominant
    cost is again the PNG codec and raster path, not the extra managed copy in `SKSvg.Save(...)`
- completed:
  - added a stylesheet-free eager compatibility-style path for string SVG loads in `Svg.Custom` so
    inline `style="..."` declarations and presentation-style attributes are applied during element
    creation instead of always staging them and paying a second full-tree flush pass at finalize
    time
  - kept the eager path narrow on purpose: it only activates when the string load has no external
    CSS input and the source text does not appear to contain a `<style>` element, so stylesheet
    documents stay on the existing deferred cascade path
  - targeted regressions stayed green:
    - `SvgDocumentCompatibilityLoaderTests.LoadStructure_WithoutStylesheet_EagerlyAppliesCompatibilityStyles`
    - `SvgDocumentCompatibilityLoaderTests.FromSvg_ParsesInlineStyleAttributes*`
  - focused `solar battery.svg` parser/load reruns moved:
    - `SvgCustomParsePhaseBenchmarks.FlushStylesOnlyAfterStructureBuild` from `351.976 us` to `34.510 us`
    - `SvgCustomParsePhaseBenchmarks.FinalizeAfterStructureBuild` from `356.906 us` to `38.391 us`
    - `SvgLoadPipelineBenchmarks.ParseSvgDocumentFromString` from `295.738 us` to `292.366 us`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvgFromStringWithBaseUri` from `1.742 ms` to `1.714 ms`
  - supporting reruns were positive but noisier:
    - `SvgCustomParsePhaseBenchmarks.LoadStructureOnly` measured `297.465 us` because structure
      build now includes the eager style application work that used to be deferred to the flush
      pass
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvg` measured `1.847 ms` versus the prior `1.948 ms`,
      but that benchmark still has short-run outlier noise on this asset
  - this phase is being kept as a parser-finalization win rather than a large whole-load shift:
    the wasted deferred flush pass is mostly gone on stylesheet-free SVG strings, while the
    remaining parser cost on `solar battery.svg` is again concentrated in structure build itself
- completed:
  - broadened the stylesheet-free eager compatibility-style path from string-only entry points to
    all stylesheet-free `Svg.Custom` loads, and removed the extra whole-source `<style` pre-scan
    that string loads previously paid before parsing
  - kept the cascade behavior unchanged:
    - eager application still only activates when there is no external CSS input
    - unsupported style values still fall back to the staged compatibility path
    - real `<style>` elements still flow through the existing post-structure CSS compatibility pass
  - targeted regressions stayed green:
    - `SvgDocumentCompatibilityLoaderTests.OpenXmlReader_MatchesStringOverload_WhenDtdProcessingIsEnabled`
    - `SvgDocumentCompatibilityLoaderTests.OpenPath_AppliesImportedStylesheets`
    - `SvgDocumentCompatibilityLoaderTests.LoadStructure_WithoutStylesheet_EagerlyAppliesCompatibilityStyles`
    - `SKSvgTests.Load_StreamWithBaseUri_AppliesImportedStylesheets`
  - controlled `solar battery.svg` parse-entry reruns moved:
    - `SvgXmlDomParseBenchmarks.ParseFromSvgString` from `292.786 us` to `290.955 us`
    - `SvgXmlDomParseBenchmarks.ParseFromStream` from `300.822 us` to `289.053 us`
    - `SvgXmlDomParseBenchmarks.ParseFromXmlReader` from `298.542 us` to `279.494 us`
  - supporting `solar battery.svg` reruns stayed positive but noisier on the end-to-end load path:
    - `SvgLoadPipelineBenchmarks.ParseSvgDocumentFromString` measured `277.647 us`
    - `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` measured `944.202 us`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvg` and `LoadViaSkSvgFromStringWithBaseUri` remained in
      the existing short-run variance band, so they were treated as directional only for this phase
  - this phase is being kept as a parser/load entry-path win: stylesheet-free stream and
    `XmlReader` loads now stay in the same parse-cost band as the direct string path instead of
    paying the old deferred-flush overhead, and string loads no longer do an extra pre-scan before
    the real XML parse begins
- completed:
  - reduced duplicate direct-visual node checks in `SvgSceneCompiler` by computing fill validity,
    stroke validity, and stroke width once per geometry node and reusing those results for both
    hit-test support and local paint creation instead of recomputing them in
    `CreateDirectPathVisual(...)`
  - kept the stroke-path change narrow: it threads the already-resolved stroke width into the
    retained-scene solid-stroke cache key and direct stroke-paint creation path, rather than adding
    another broad compile cache or changing retained-scene output structure
  - focused `solar battery.svg` retained-scene compile reruns moved:
    - `SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly` from `426.463 us` to `358.856 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime` from `531.764 us` to `513.944 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` from
      `522.907 us` to `492.102 us`
  - supporting reruns stayed acceptable:
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneCompiler` reran at `515.394 us`, effectively
      flat versus the prior `515.028 us` baseline
    - `SvgRetainedSceneCompileBenchmarks.CreateSceneDocumentFromCompiledTree` reran at `93.219 us`,
      still in the existing low-`90 us` band
    - the retained-scene graph safety pass only reproduced the known unrelated branch failure
      `SvgRetainedSceneGraphTests.RetainedSceneGraph_ApplyMutation_UpdatesNestedUseDescendantDependents`
      while `93` other rows passed
  - this phase is being kept as a direct-visual node-tree microbenchmark win: the compiler no
    longer pays duplicate stroke-width and fill/stroke-validity checks across the dominant geometry
    nodes in `solar battery.svg`, while broader direct-compiler totals stayed flat to modestly
    positive
- completed:
  - shared native render-path materialization across fresh `SkiaModel` instances in
    `SkiaModel.Caching` by keeping the existing per-object weak cache and adding a bounded static
    cache keyed by immutable shim path command content
  - kept the path-sharing change narrow: it only targets native `SKPath` creation, leaves the
    existing per-instance weak cache in place for mutation tracking via path revision, and rejects
    stale entries automatically when the source shim path mutates
  - targeted regressions stayed green:
    - `SkiaModelReplayTests.RenderPathCache_ReusesAcrossFreshModelsForEquivalentPaths`
    - `SkiaModelReplayTests.RenderPathCache_InvalidatesWhenPathMutates`
    - `SkiaModelReplayTests.Draw_OptimizedReplayMatchesPerCommandDispatch`
  - focused `solar battery.svg` native/load reruns moved:
    - `SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModelWithFreshSkiaModel` from
      `487.027 us` to `449.696 us`
    - `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` from `975.994 us` to
      `950.611 us`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvgFromStringWithBaseUri` from `1.714 ms` to `1.675 ms`
  - supporting reruns stayed acceptable:
    - `SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModel` reran at `145.392 us`, so the
      warm already-cached model path remains in the same low-hundreds-of-microseconds band while
      this phase specifically improves fresh-instance recording
    - the clean 30-iteration manual profiler rerun was rejected as an acceptance signal because all
      measured stages shifted upward together, indicating environment noise rather than a stable
      phase-specific regression or improvement
  - this phase is being kept as a fresh-native-picture and startup win: repeated fresh `SKSvg`
    loads no longer rebuild identical native paths for every model instance on this asset
- completed:
  - tightened retained-scene dependency classification in `SvgSceneCompiler` so concrete
    non-resource paint servers no longer flow through the dependency/reference path, and plain
    visual nodes skip retained clip/mask/filter key resolution entirely when they do not declare
    any of those references
  - kept the compile-side change narrow:
    - `GetResolvedPaintServerElement(...)` now returns early for `SvgColourServer` and other
      detached non-deferred paint servers that cannot resolve to addressable document resources
    - `AssignRetainedResourceKeys(...)` now returns immediately for visuals with no `clipPath`,
      `filter`, or `mask` reference instead of still paying the null-path lookup work
  - targeted regressions stayed green:
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_AssignsRetainedResourceKeysForClipMaskAndFilter`
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_LeavesRetainedResourceKeysNullForPlainDirectNodes`
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_ResolvesRetainedClipPayloadsForDirectNodes`
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_ResolvesRetainedMaskPayloadsForDirectNodes`
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_ResolvesRetainedFilterPayloadsForDirectNodes`
  - focused `solar battery.svg` compile reruns moved:
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneCompiler` from `537.724 us` to `463.215 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime` from `497.259 us` to `507.523 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` from
      `523.522 us` to `526.260 us`
  - supporting dependency-walk reruns stayed acceptable:
    - `SvgRetainedSceneCompileBenchmarks.RegisterDependenciesOnly` measured `148.985 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly` measured `367.365 us` versus the
      prior `364.640 us`, so the direct node-tree path stayed in the same short-run band
  - this phase is being kept as a direct-compiler and dependency-classification win rather than a
    broad runtime-compile win: the `solar battery.svg` direct compiler now avoids a large amount of
      non-resource fill/stroke reference work, while the full runtime wrapper totals stayed inside
      short-run noise on this asset
- completed:
  - stopped cloning compile-scope cached solid fill and stroke paints back into every matching
    direct retained node in `SvgSceneCompiler`; matching direct-path visuals now reuse the cached
    immutable paint template instance instead
  - kept the change narrow:
    - only compile-scope solid direct paints share references
    - non-solid paints and runtime-resolved paint-server paths still materialize per node as before
    - retained-scene rendering continues to treat local direct paints as immutable after compile
  - targeted regression stayed green:
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_ReusesCachedSolidDirectPaintTemplatesAcrossMatchingNodes`
  - focused `solar battery.svg` retained-scene compile reruns moved:
    - `SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly` from `365.136 us` to `363.760 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime` from `519.152 us` to `500.296 us`
  - supporting reruns stayed acceptable but noisy:
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` reran at
      `506.622 us`, still in the same short-run band as the prior `500.108 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneCompiler` reran at `530.156 us`, so it was
      not used as the acceptance signal for this phase
  - this phase is being kept as a narrow direct-paint template reuse win: the dominant node-tree
    compile path no longer pays `SKPaint.Clone()` for every identical solid direct visual on this
    asset, while broader compile totals remain mostly in the existing short-run variance band
- completed:
  - removed a redundant direct-path bounds walk in `SvgSceneCompiler.CreateDirectPathVisual(...)`
    by reusing the already-computed node geometry bounds for the local-cull check instead of
    calling `path.Bounds` again after paint creation
  - kept the change narrow:
    - direct retained geometry nodes still compute bounds once at path creation time
    - the local-cull guard now runs before fill/stroke paint materialization, so degenerate direct
      paths bail out without extra paint work
    - no retained-scene output format or cache keys changed
  - focused `solar battery.svg` retained-scene compile reruns moved:
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime` from `500.296 us` to `486.618 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` from
      `506.622 us` to `483.612 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneCompiler` from `530.156 us` to `500.216 us`
  - supporting reruns were noisier:
    - `SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly` reran at `482.736 us`, so it was not
      used as the acceptance signal for this phase
    - the confirming 30-iteration profiler rerun was rejected as an acceptance signal because
      `Compile retained scene (parsed doc)` and the native/export stages all shifted together
  - this phase is being kept as a direct-path compile cleanup win: path-heavy direct nodes on
    `solar battery.svg` no longer pay a second bounds traversal before entering the retained-scene
    runtime pipeline
- completed:
  - folded duplicate direct renderability checks in `SvgSceneCompiler` onto the retained visual
    state that is already assigned for direct visuals, `use`, and `image` nodes, so those paths no
    longer read visibility/display state once through `MaskingService.CanDraw(...)` and then again
    through `AssignRetainedVisualState(...)`
  - kept the change narrow:
    - direct retained nodes still compute `HasFeatures(...)` in the same place as before
    - the new helper only derives drawability from the already-assigned retained flags
      `IsVisible` / `IsDisplayNone`
    - no retained-scene output structure, resource keys, or paint-selection behavior changed
  - targeted regressions stayed green:
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_HandlesConditionalAttributesWithChromeCompatibleBehavior`
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_AllowsVisibleChildInsideHiddenGroup`
  - a fresh 30-iteration profiler trace still showed compile as one of the top stable
    non-encode costs on `solar battery.svg`, with `Compile retained scene (parsed doc)` at
    `2.82 ms`, so this phase was accepted against focused compile reruns rather than as a blind
    microbenchmark chase
  - focused `solar battery.svg` retained-scene compile reruns moved:
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime` from `486.618 us` to `455.763 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` reran at
      `484.236 us`, effectively flat versus the prior `483.612 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly` reran at `337.908 us`
  - supporting reruns were noisier:
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneCompiler` reran at `515.461 us`, so it was
      not used as the acceptance signal for this phase
    - `SvgRetainedSceneCompileBenchmarks.CreateSceneDocumentFromCompiledTree` reran at `83.259 us`
      and `RegisterDependenciesOnly` at `158.084 us`, both still inside the current low-hundreds
      microsecond band
  - this phase is being kept as a retained-runtime compile win: path-heavy direct nodes on
    `solar battery.svg` now reuse already-assigned retained visibility/display state instead of
    re-reading it in the hot direct-visual compile path, while the broader direct-compiler totals
    stayed too noisy to use as the primary decision signal
- completed:
  - removed the second structural child-bounds walk for direct retained fragments, groups,
    anchors, switches, and `use` nodes by accumulating local child geometry bounds when compiled
    children attach to the parent `SvgSceneNode`, then letting
    `FinalizeDirectStructuralBounds(...)` only publish `TotalTransform` / `TransformedBounds`
  - kept the change narrow:
    - only structural retained nodes that already relied on `FinalizeDirectStructuralBounds(...)`
      participate in the new accumulation path
    - non-structural parents such as direct path/marker visuals still keep their existing local
      geometry behavior
    - child bounds are still ignored when the child is `display:none` or has empty geometry, so the
      retained bounds semantics stay aligned with the old finalize pass
  - targeted regressions stayed green:
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_CompilesUseSwitchAndImageWithDirectRetainedStrategy`
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_HandlesConditionalAttributesWithChromeCompatibleBehavior`
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_AllowsVisibleChildInsideHiddenGroup`
  - this phase was chosen from the current clean profiler state where retained-scene compile was
    still one of the top stable non-encode costs on `solar battery.svg`; acceptance used focused
    compile/load reruns because the immediately prior whole-pipeline profiler pass shifted every
    stage upward together and was rejected as environment noise
  - focused `solar battery.svg` retained-scene compile reruns moved:
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime` from `455.763 us` to `438.889 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` from
      `484.236 us` to `448.492 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneCompiler` from `515.461 us` to `472.136 us`
  - supporting reruns stayed acceptable:
    - `SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly` reran at `342.591 us` versus the
      prior `337.908 us`, so the node-tree-only slice stayed in the same narrow band while the
      broader runtime/compiler wrappers improved
    - `SvgRetainedSceneCompileBenchmarks.CreateSceneDocumentFromCompiledTree` reran at `82.643 us`
      and `RegisterDependenciesOnly` at `151.573 us`, both still inside the current low-hundreds
      microsecond band
    - the load-facing rerun `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` measured
      `820.027 us`, well below the current plan baseline `950.611 us`
  - this phase is being kept as a structural compile/load win: direct retained structural parents
    on `solar battery.svg` now reuse child bounds as the tree is built instead of paying a second
    per-parent child union pass before the picture-recording phase
- completed:
  - reduced duplicate transform work in the retained-scene compile walk by computing the effective
    child parent transform once per compiled node in `CompileElementNode(...)` and by letting
    `FinalizeDirectStructuralBounds(...)` reuse the already-assigned `node.TotalTransform` instead
    of recomputing the same `PreConcat(...)` for every direct structural parent
  - kept the change narrow:
    - it only touches the direct retained compile path in `SvgSceneCompiler`
    - no retained-scene mutation/index/resource behavior changed
    - all structural bounds semantics still flow through the same accumulated `GeometryBounds` and
      published `TransformedBounds`
  - targeted regressions stayed green:
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_CompilesUseSwitchAndImageWithDirectRetainedStrategy`
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_HandlesConditionalAttributesWithChromeCompatibleBehavior`
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_AllowsVisibleChildInsideHiddenGroup`
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_ApplyMutation_UpdatesUseDependents`
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_ApplyMutation_UpdatesGradientResourceDependents`
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_ApplyMutation_UpdatesFilterResourceDependents`
    - `SKSvgLayerBoundsTests`
  - a broader `SvgSceneNode.AddChild(...)` cache-invalidation suppression experiment was rejected
    during this phase because it improved only the isolated tree-build slice while regressing the
    broader retained-scene runtime/compiler benchmarks
  - focused `solar battery.svg` retained-scene compile reruns moved:
    - `SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly` from `342.591 us` to `333.282 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` from
      `448.492 us` to `432.490 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneCompiler` from `472.136 us` to `459.450 us`
  - supporting reruns stayed acceptable:
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime` reran at `458.139 us`; that is
      above the previous short-run `438.889 us`, so it was treated as noise rather than the primary
      keep signal because the fresh-loader/direct-compiler slices both improved and the end-to-end
      parsed-document load also improved
    - `SvgRetainedSceneCompileBenchmarks.CreateSceneDocumentFromCompiledTree` reran at `80.730 us`
      and `RegisterDependenciesOnly` at `152.542 us`, both still inside the current low-hundreds
      microsecond band
  - the end-to-end acceptance signal for this phase was
    `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg`, which moved from `820.027 us`
    to `808.397 us`
  - this phase is being kept as a compile/load win: retained-scene compile on `solar battery.svg`
    now does less duplicate matrix work in the hot direct-retained structural walk while keeping
    the same retained-scene output model
- completed:
  - switched the cold PNG export path in `SKPictureExtensions` to encode directly from bitmap or
    surface `SKPixmap` memory instead of going through the older snapshot/image encode route, and
    initially kept a PNG-specific encoder path with `SKPngEncoderOptions(AllFilters, 1)` for the
    accepted version at that stage
  - kept the change narrow:
    - it only affects the native PNG export path in `SKPictureExtensions`
    - non-PNG formats still use the existing generic `SKPixmap.Encode(stream, format, quality)`
      path
    - the reusable-surface and allocated-bitmap export paths now share the same pixmap encoder
      helper, so their cold export totals stay aligned
  - targeted regressions stayed green:
    - `SKPictureExtensionsTests`
    - `SKSvgTests.Save_RepeatedStaticCalls_ProduceIdenticalBytes`
    - `SKSvgTests.Save_EmptyRootDocument_WritesBlankViewportPng`
    - `SvgAnimationControllerTests.Save_SucceedsWhenAnimationLayerCachingIsActive`
  - an earlier `SKPngEncoderFilterFlags.NoFilters` variant was rejected during this phase because
    it improved the raw encode microbenchmarks but made
    `SvgSaveBenchmarks.SaveTransparentPng1xAfterPictureRefresh` noisy and worse at `12.585 ms`;
    the accepted `AllFilters, 1` variant at that stage kept the same raw encode gain while also
    improving the cold public save path
  - focused `solar battery.svg` cold export reruns moved:
    - `SvgLoadPipelineBenchmarks.EncodeNativePictureToPng` from `9.847 ms` to `7.287 ms`
    - `SvgRenderBitmapBenchmarks.EncodeTransparentBitmap1x` from `9.955 ms` to `7.347 ms`
    - `SvgRenderBitmapBenchmarks.EncodeTransparentBitmap1xViaReusableSurface` from `9.983 ms` to
      `7.315 ms`
    - `SvgSaveBenchmarks.SaveTransparentPng1xAfterPictureRefresh` from `9.958 ms` to `7.553 ms`
  - this phase is being kept as a cold export win: `solar battery.svg` no longer pays the extra
    snapshot/image encode path on first PNG save, and the remaining export cost is now more
    directly the codec itself rather than wrapper overhead
- completed:
  - made the fresh-instance local caches inside `SkiaSvgAssetLoader` lazy so default-path `SKSvg`
    startup no longer allocates non-shared typeface dictionaries, paint weak tables, weak-ref
    tracking lists, or the instance paint-cache lock until a path actually needs them
  - kept the change narrow:
    - it only affects instance-owned caches in `SkiaSvgAssetLoader`
    - the shared default-path typeface caches and shared paint-template caches stay unchanged
    - provider-sensitive behavior is still instance-scoped; only the eager constructor allocation
      moved to first-use lazy initialization
  - targeted regressions stayed green:
    - `SkiaSvgAssetLoaderCachingTests`
  - focused `solar battery.svg` fresh-instance reruns moved:
    - `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` from `808.397 us` to
      `794.639 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` from
      `499.629 us` to `474.321 us`
  - supporting broad load reruns stayed noisy:
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvg` reran at `2.096 ms`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvgFromStringWithBaseUri` reran at `2.209 ms`
    - both were treated as directional only, not the acceptance signal for this phase
  - this phase is being kept as a fresh-instance startup win: `solar battery.svg` no longer pays
    for several unused loader-local caches on every new `SKSvg`, and the acceptance signal comes
    from the parsed-document fresh-load plus fresh-asset-loader compile slices rather than the
    noisier whole-load wrapper benchmarks
- completed:
  - rejected a broader shared parsed-document prototype cache in `Svg.Custom` after it regressed the
    clean profiler and the raw `ParseSvgDocumentFromString` benchmark on `solar battery.svg`; the
    extra `DeepCopy()` cost on the parser-wide fast path was not justified even though some
    short-run repeated-load means looked better
  - kept the narrower load-entry change instead:
    - added default-options overloads in `SvgDocumentCompatibilityLoader` for string and stream
      entry points with no `Entities` or CSS
    - updated `SvgService` to detect the real null/empty-parameter hot path and route it to those
      overloads instead of allocating `new SvgOptions(parameters?.Entities, parameters?.Css)` for
      every default `SKSvg.FromSvg(...)` or `SKSvg.Load(...)` call
    - kept all parser behavior inside `Svg.Custom`; no external `Svg` sources were changed
  - targeted load regressions stayed green:
    - `SKSvgTests.Load_StreamWithBaseUri_AppliesImportedStylesheets`
    - `SKSvgRebuildFromModelTests`
    - `LoadStaticSvg_DoesNotCreateAnimationController`
    - `LoadStaticSvg_AfterAnimatedSvg_ClearsAnimationState`
    - `SKPictureExtensionsTests`
  - focused `solar battery.svg` reruns moved:
    - `SvgLoadPipelineBenchmarks.ParseSvgDocumentFromString` from the plan baseline
      `294.974 us` to `278.895 us`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvg` from the last accepted plan baseline `2.044 ms` to
      `986.736 us`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvgFromStringWithBaseUri` from the plan baseline
      `1.675 ms` to `1.059 ms`
  - the clean 30-iteration profiler stayed in the same overall load band while the repeated string
    load lanes remained below the earlier accepted profiler results:
    - `Load via SKSvg.FromSvg` `3.88 ms`
    - `Control-like source load` `3.63 ms`
  - this phase is being kept as a real default-load entry win: repeated string and stream loads no
    longer pay avoidable default `SvgOptions` setup on the hot path, and unlike the rejected
    prototype-cache experiment it improves load means without pushing extra work into the parser
- completed:
  - made the fresh-instance native caches inside `SkiaModel` lazy so new `SkiaModel` instances no
    longer allocate typeface dictionaries, native-object weak tables, positioned-text state, or
    picture-cache dictionaries until a code path actually uses them
  - kept the change narrow:
    - it only affects instance-owned caches in `SkiaModel`
    - shared cross-instance caches for typefaces, render-paint templates, render paths, and
      positioned text remain unchanged
    - replay/render semantics stay the same; only constructor-time cache allocation moved to
      first-use lazy initialization
  - targeted regressions stayed green:
    - `SkiaModelReplayTests`
  - focused `solar battery.svg` fresh-native reruns moved:
    - `SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModelWithFreshSkiaModel` from
      `449.696 us` to `376.582 us`
    - `SvgNativeSkPictureBenchmarks.CreateNativePictureFromFullModel` reran at `148.017 us`, which
      stayed in the existing steady-state band and showed the warm/shared path did not regress
  - the fresh parsed-document load rerun was noisy and not used as the primary acceptance metric:
    - `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` reran at `820.931 us`
  - the clean 30-iteration profiler confirmed the selected hotspot moved:
    - `Create native SKPicture` from `1.02 ms` to `0.73 ms`
    - `Load via SKSvg.FromSvg` from `7.47 ms` to `4.79 ms`
    - `Control-like source load` from `4.18 ms` to `3.92 ms`
  - the same compile-breakdown rerun also showed where compile work now concentrates if another
    compile phase is needed:
    - `CompileNodeTreeOnly` `332.285 us`
    - `RegisterDependenciesOnly` `150.774 us`
    - `CreateSceneDocumentFromCompiledTree` `80.018 us`
    - `RebuildResourceGraphOnly` `45.089 us`
    - `ResolveRuntimePayloadsOnly` `46.328 us`
    - `ReindexSceneNodesOnly` `29.828 us`
  - this phase is being kept as a fresh-native-picture win: `solar battery.svg` now pays less
    eager per-instance cache setup before the first native recording, and any later compile work
    should go after node-tree build directly rather than the smaller scene-document passes
- completed:
  - removed repeated renderable-paint-bounds invalidation while building fresh retained-scene node
    trees by letting compile-time child attachment skip ancestor cache invalidation and by keeping a
    single invalidation at the end of `ReplaceWith(...)`
  - kept the change narrow:
    - it only changes `SvgSceneNode.AddChild(...)` usage during fresh compile-time tree assembly
      and subtree replacement
    - retained-scene rendering semantics stay unchanged; the change only avoids redundant upward
      cache clears before any renderable-paint-bounds cache exists
    - mutation safety is preserved because `ReplaceWith(...)` still performs the final upward
      invalidation after the replacement subtree is attached
  - targeted regressions stayed green:
    - `RetainedSceneGraph_CompilesUseSwitchAndImageWithDirectRetainedStrategy`
    - `RetainedSceneGraph_HandlesConditionalAttributesWithChromeCompatibleBehavior`
    - `RetainedSceneGraph_AllowsVisibleChildInsideHiddenGroup`
    - `SKSvgLayerBoundsTests`
  - focused `solar battery.svg` compile reruns moved:
    - `SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly` reran at `342.412 us`, which keeps
      the node-tree slice inside the prior low-`340 us` band rather than opening a new regression
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime` from `458.139 us` to `434.270 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` from
      `474.321 us` to `469.237 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneCompiler` reran at `487.391 us`
    - supporting parsed-document fresh load reran at `849.453 us`
  - this phase is being kept as a compile-tree assembly win: fresh retained-scene construction no
    longer pays repeated ancestor cache invalidation during child attachment, and the acceptance
    signal is the lower runtime compile path on `solar battery.svg` rather than another
    scene-document phase
- completed:
  - added cross-compile direct-visual `SKPath` reuse for parsed `SvgPath`, `SvgRectangle`,
    `SvgCircle`, `SvgEllipse`, `SvgLine`, `SvgPolyline`, and `SvgPolygon` nodes by caching direct
    path materialization per source element, dirty version, fill rule, and viewport inside
    `SvgSceneCompiler`
  - kept ownership clean by adding a narrow scene-graph path dirtiness/version seam on
    `Svg.Custom` `SvgElement`; no external `Svg` sources were changed
  - targeted mutation safety stayed green with
    `RetainedSceneGraph_ApplyMutation_UpdatesDirectRectGeometryAfterCachedCompile`
  - focused `solar battery.svg` compile reruns moved:
    - `SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly` from `342.412 us` to `169.670 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneCompiler` from `487.391 us` to `291.126 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime` from `434.270 us` to `274.587 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` from
      `469.237 us` to `272.048 us`
    - supporting `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` from `849.453 us`
      to `562.849 us`
  - this phase is being kept as a node-tree direct-path-materialization win: repeated compiles of
    the same parsed document no longer rebuild equivalent direct visual `SKPath` objects for
    unchanged shapes, and both runtime and direct-compiler paths moved enough to justify the cache
- completed:
  - fixed stale null direct-visual path reuse in `SvgSceneCompiler` and kept the leaf-opacity
    direct-draw fast path now that valid `SvgPath` nodes no longer arrive without local geometry
  - kept the change narrow:
    - `SvgSceneCompiler.TryGetCachedDirectVisualPath(...)` now refreshes cached null entries for
      `SvgPath` elements that already have real `PathData`, instead of trusting the stale null
      result forever
    - leaf direct paths with simple opacity can now fold that opacity into adjusted local paints in
      the retained-scene renderer, direct native recorder path, and animation-layer static recorder
      instead of paying `SaveLayer(...)` replay cost
    - no external `Svg` sources were changed; the parser-side work remains inside `Svg.Custom`
  - added focused coverage:
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_FoldsLeafOpacityIntoSingleDirectPaintWithoutSaveLayer`
    - existing retained-scene wrapper/save tests
    - `SvgAnimationControllerTests.SetAnimationTime_AnimationLayerCachingRecordsBoundedSaveLayersForStaticOpacityWrappers`
    - `SKSvgLayerBoundsTests`
    - `OpacityRenderingTests`
  - direct retained-model inspection on `solar battery.svg` moved:
    - command count `126` -> `136`
    - `SaveLayerCanvasCommand` count `17` -> `3`
  - focused `solar battery.svg` reruns moved:
    - `SvgRenderBitmapBenchmarks.DrawNativePicture1x` from `3.246 ms` to `269.826 us`
    - `SvgRenderBitmapBenchmarks.RenderTransparentBitmap1x` from `3.351 ms` to `275.744 us`
    - `SvgRenderBitmapBenchmarks.RenderTransparentBitmap1xIntoReusableBitmap` from `3.269 ms` to
      `265.317 us`
    - `SvgRenderBitmapBenchmarks.EncodeTransparentBitmap1x` from `10.193 ms` to `7.241 ms`
    - `SvgRenderBitmapBenchmarks.EncodeTransparentBitmap1xViaReusableSurface` from `10.081 ms` to
      `7.136 ms`
    - `SvgLoadPipelineBenchmarks.EncodeNativePictureToPng` measured `7.253 ms`
  - the clean 30-iteration profiler was used as the acceptance signal for this phase because the
    plan called for choosing the next pass from longer-run end-to-end data:
    - `Render native picture to bitmap` `5.30 ms` -> `0.78 ms`
    - `Encode native picture to PNG` `12.96 ms` -> `10.48 ms`
    - `Load via SKSvg.FromSvg` `3.59 ms` -> `4.80 ms`
    - `Control-like source load` `3.36 ms` -> `3.71 ms`
  - this phase is being kept as a render-path command-count win: the retained-scene replay stream
    now avoids most opacity `SaveLayer(...)` work on `solar battery.svg`, so render cost dropped by
    roughly an order of magnitude and encode time fell with it because replay is now a much smaller
    portion of the cold PNG path
- completed:
  - added shared `SvgPathSegmentList -> SKPath` reuse in `PathingService` so fresh parsed
    documents can reuse identical `<path d="...">` geometry across loads instead of only reusing
    direct-path materialization inside one parsed document instance
  - kept the change narrow:
    - it only targets `SvgPathSegmentList.ToPath(...)`, which is the path-heavy seam on
      `solar battery.svg`
    - it does not change the external `Svg` parser or ownership model; the cache key is derived
      from parsed segment content plus fill rule inside `Svg.Model`
    - same-document direct-visual caching in `SvgSceneCompiler` stays in place; this new layer is
      specifically for fresh-document load/compile paths
  - added focused coverage:
    - `SvgRetainedSceneGraphTests.RetainedSceneGraph_ReusesSharedPathDataAcrossFreshParsedDocuments`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshParsedDocument`
  - focused short-run reruns were mixed and treated as directional only:
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvg` reran from `992.976 us` to `1.075 ms`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvgFromStringWithBaseUri` reran from `1.041 ms` to
      `1.034 ms`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshParsedDocument` measured
      `734.659 us`
  - the clean 30-iteration profiler was used as the acceptance signal for this phase because the
    new cache targets repeated fresh-document paths and the short-run end-to-end BDN numbers stayed
    noisy:
    - `Parse SvgDocument from string` `2.04 ms` -> `1.37 ms`
    - `Compile retained scene (parsed doc)` `2.89 ms` -> `1.97 ms`
    - `Load via SKSvg.FromSvg` `3.88 ms` -> `3.58 ms`
    - `Control-like source load` `3.63 ms` -> `3.59 ms`
  - this phase is being kept as a fresh-document path-materialization win: `solar battery.svg`
    remains path-heavy, and longer-run profiler data shows the shared path cache lowering the real
    compile/load slices even though the short-run wrapper benchmark means stayed noisy
- completed:
  - added a narrow shared parsed-path-data prototype cache in `Svg.Custom` `SvgElementFactory` for
    raw `<path d="...">` attributes, so fresh parses can reuse identical `SvgPathSegmentList`
    prototypes while still cloning per document to avoid mutable cross-document sharing
  - kept ownership clean:
    - the cache lives entirely inside `Svg.Custom`
    - no external `Svg` parser sources were changed
    - mutation safety is covered by
      `SvgDocumentCompatibilityLoaderTests.FromSvg_DoesNotShareMutablePathDataAcrossFreshDocuments`
  - added focused measurement for the new seam in
    `SvgCustomAttributeDispatchBenchmarks.ApplyPathDataAttributesOnlyColdSharedCache` and
    `SvgCustomAttributeDispatchBenchmarks.ApplyPathDataAttributesOnlyWarmSharedCache`
  - focused `solar battery.svg` reruns moved:
    - `SvgCustomAttributeDispatchBenchmarks.ApplyPathDataAttributesOnlyColdSharedCache` measured
      `1.290 ms`
    - `SvgCustomAttributeDispatchBenchmarks.ApplyPathDataAttributesOnlyWarmSharedCache` measured
      `26.106 us`
    - `SvgLoadPipelineBenchmarks.ParseSvgDocumentFromString` from `295.177 us` to `187.746 us`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvgFromStringWithBaseUri` from `990.683 us` to
      `904.536 us`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvg` reran noisy from `994.101 us` to `1.039 ms`
  - this phase is being kept as a parser-side path-data reuse win: on `solar battery.svg`, the
    accepted signal is the much lower raw parse cost plus the lower control-like base-URI load
    path, while the general `LoadViaSkSvg` wrapper benchmark stayed in the expected short-run noise
    band
- completed:
  - revisited the cold PNG codec choice in `SKPictureExtensions` and switched the PNG-specific
    encoder options from `SKPngEncoderOptions(AllFilters, 1)` to
    `SKPngEncoderOptions(None, 1)` after re-probing the direct pixmap encode path on
    `solar battery.svg`
  - kept the change narrow:
    - it only affects the PNG-specific branch inside `SKPictureExtensions.EncodePixmap(...)`
    - non-PNG formats still use the generic `SKPixmap.Encode(stream, format, quality)` path
    - the reusable-surface, allocated-bitmap, load-pipeline, and public `SKSvg.Save(...)` PNG
      paths all share the same helper, so one codec-choice change moves the whole cold export lane
  - targeted regressions stayed green:
    - `SKPictureExtensionsTests`
    - `SKSvgTests.Save_RepeatedStaticCalls_ProduceIdenticalBytes`
    - `SKSvgTests.Save_EmptyRootDocument_WritesBlankViewportPng`
    - `SvgAnimationControllerTests.Save_SucceedsWhenAnimationLayerCachingIsActive`
  - the direct local pixmap probe used to pick the filter mode on `/Users/wieslawsoltes/Downloads/solar battery.svg`
    came out as:
    - `AllFilters-1` `9.283 ms`, `63284` bytes
    - `None-1` `3.700 ms`, `54113` bytes
    - `None-2` `4.028 ms`, `52831` bytes
    - `None-0` `2.378 ms`, `1544262` bytes
  - focused `solar battery.svg` cold export reruns moved:
    - `SvgLoadPipelineBenchmarks.EncodeNativePictureToPng` from `7.253 ms` to `2.540 ms`
    - `SvgRenderBitmapBenchmarks.EncodeTransparentBitmap1x` from `7.241 ms` to `2.531 ms`
    - `SvgRenderBitmapBenchmarks.EncodeTransparentBitmap1xViaReusableSurface` from `7.136 ms` to
      `2.469 ms`
    - `SvgSaveBenchmarks.SaveTransparentPng1xAfterPictureRefresh` from `7.553 ms` to `2.868 ms`
  - the clean 30-iteration profiler acceptance signal moved:
    - `Encode native picture to PNG` from `10.48 ms` to `6.77 ms`
  - this phase is being kept as a cold PNG codec-choice win: `solar battery.svg` raster output now
    stays on the faster no-filter PNG path without ballooning file size, so the remaining export
    work is more clearly the fixed raster and stream-write cost rather than the previous filter
    search overhead
- completed:
  - batched compilation-root dependency registration in `SvgSceneDocument`, so the retained-scene
    dependency walk accumulates subtree addresses and referenced resources first and only updates
    the root dependency maps once per root instead of churning the dictionaries and hash sets on
    every discovered edge
  - kept the change narrow:
    - it only touches the retained-scene dependency-registration path in `Svg.SceneGraph`
    - it improves both full retained-scene rebuilds and targeted mutation subtree refreshes because
      `RefreshMutationSubtrees(...)` reuses the same subtree registration helper
    - it does not change parser ownership, retained-node structure, or runtime payload semantics
  - targeted regressions stayed green:
    - `RetainedSceneGraph_ApplyMutation_UpdatesUseDependents`
    - `RetainedSceneGraph_ApplyMutation_UpdatesGradientResourceDependents`
    - `RetainedSceneGraph_ApplyMutation_UpdatesFilterResourceDependents`
  - focused `solar battery.svg` reruns moved:
    - `SvgRetainedSceneCompileBenchmarks.RegisterDependenciesOnly` from `265.635 us` to
      `158.735 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` from
      `324.426 us` to `314.643 us`
    - `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` from `788.323 us` to
      `686.840 us`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvgFromStringWithBaseUri` from `1.689 ms` to
      `1.252 ms`
    - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationOnly` measured `57.453 us`
    - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationAndRender` measured `401.005 us`
  - the clean 30-iteration profiler stayed mixed on broad load totals and was treated as a
    retained-scene acceptance signal rather than a blanket end-to-end load win:
    - `Compile retained scene (parsed doc)` `2.53 ms` -> `2.36 ms`
    - `Mutate + retained scene rebuild` `5.53 ms` -> `4.67 ms`
    - `Load via SKSvg.FromSvg` reran noisy at `4.96 ms` versus the prior `4.90 ms`
    - `Control-like source load` reran noisy at `5.29 ms` versus the prior `4.40 ms`
  - this phase is being kept as a retained-scene dependency-registration and mutation-path win:
    `solar battery.svg` now pays materially less map churn in dependency registration, but the next
    phase should come from a clean whole-load or full-rebuild trace rather than another blind pass
    over the same registration seam
- completed:
  - added a same-document static reload fast path in `SKSvg.LoadSvgDocument(...)`, so
    `FromSvgDocument(...)` on an already-loaded static `SKSvg` instance no longer clears the
    retained-scene state up front before recompiling the same `SvgDocument` reference
  - kept the change narrow:
    - it only applies when the incoming `SvgDocument` reference is the current `SourceDocument`
    - animated/native-composition/layer-caching cases still go through the existing generic reset
      path
    - picture/model/save-cache replacement still happens through `RenderRetainedSceneDocument(...)`,
      so publish semantics stay unchanged and deferred-model behavior is preserved
  - added focused coverage:
    - `SKSvgRebuildFromModelTests.FromSvgDocument_SameStaticDocumentReload_LeavesModelDeferredUntilRequested`
    - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationAndFullRebuild`
  - focused `solar battery.svg` mutation reruns came out as:
    - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationOnly` -> `49.661 us`
    - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationAndRender` -> `485.369 us`
    - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationAndFullRebuild` -> `1.647 ms`
  - the clean 30-iteration profiler was used as the keep signal for this phase because it measures
    the exact same end-to-end edit flow:
    - `Mutate + full FromSvgDocument rebuild` `6.79 ms` -> `6.39 ms`
    - `Load via SKSvg.FromSvg` reran at `5.72 ms` versus the prior `6.38 ms`, but that broader
      load number is still being treated as directional only for this phase
    - `Control-like source load` reran at `5.37 ms` versus the prior `5.18 ms`, so no separate
      control-like load win is being claimed here
  - this phase is being kept as a same-document full-rebuild win for static editing/tooling flows:
    the live-document `FromSvgDocument(...)` path now avoids unnecessary retained-scene teardown
    before the replacement scene is ready, but the next phase should still come from a fresh clean
    whole-load or mutation trace rather than another narrow reload micro-pass
- next:
  - stop another compile-local micro-optimization on `solar battery.svg` unless a fresh longer-run
    profiler trace still shows `Compile retained scene (parsed doc)` as one of the top stable
    costs after this same-document plus fresh-document path-materialization pass
  - stop repeated-save-path micro-optimization on `solar battery.svg`; the remaining export work is
    now cold first-save encode cost, not warm repeated saves
  - stop cold-export wrapper micro-optimization on `solar battery.svg`; the first-save PNG path is
    now in the `7.3-7.6 ms` band and further wins should come from codec choices or broader load
    behavior, not more wrapper-only changes
  - stop fresh-static `SKSvg` startup micro-optimization on `solar battery.svg` unless a clean
    single-process longer-run load trace points at a stable remaining startup hotspot above the
    current sub-millisecond focused benchmark band
  - stop control-like string-source entry tuning on `solar battery.svg` unless a clean profiler
    rerun shows `Control-like source load` materially diverging from `Load via SKSvg.FromSvg`
    again; the direct string path removed the previous stream-conversion overhead and the remaining
    gap is now mostly the same parse/compile work shared by both stages
  - stop fresh native-picture micro-optimization on `solar battery.svg` unless a clean profiler or
    focused fresh-load benchmark exposes another stable path/materialization hotspot above the
    current shared-paint and shared-path cache band
  - stop parser-local inline-style and deferred-flush chasing on `solar battery.svg` unless
    `ParseSvgDocumentFromString` or a clean whole-load trace moves again; the shared inline-style
    cache and the new eager stylesheet-free finalize path have already removed most of that lane,
    so remaining parser work is mostly structure build rather than style finalization
  - stop parser-wide prototype caching on `solar battery.svg`; the rejected `DeepCopy()`-based
    document cache improved some short-run repeated-load numbers but regressed the clean profiler
    and the raw parse benchmark, so any future repeated-load cache needs to live higher in the load
    stack or avoid parser-wide clone cost entirely
  - stop narrower parser-side path-data prototype chasing on `solar battery.svg` unless a fresh
    profiler trace lifts raw parse cost back into the top stable hotspots; this kept phase removed
    the repeated `d`-attribute parse lane without reintroducing whole-document clone overhead, so
    the next win should come from the current full-pipeline trace rather than another blind parser
    cache
  - stop another PNG filter-mode micro-optimization on `solar battery.svg` unless a clean
    longer-run profiler trace or a cross-asset export sweep exposes a new stable codec hotspot; the
    cold first-save PNG path is now in the `2.5-2.9 ms` focused benchmark band and the profiler
    export slice is down to `6.77 ms`, so the next likely win should come from load or
    retained-mutation behavior rather than another encoder-filter pass
  - stop another dependency-registration micro-pass on `solar battery.svg` unless a clean
    mutation/rebuild trace points back at that seam; `RegisterDependenciesOnly` is now down in the
    `~159 us` band, so the next retained-scene win is more likely to come from the broader
    full-rebuild or mutation pipeline than from more dictionary churn reduction here
  - stop another same-document `FromSvgDocument(...)` reload micro-pass on `solar battery.svg`
    unless a clean editor-style mutation trace still shows that exact lane as one of the top stable
    costs after this keep; the current focused full-rebuild benchmark is already in the
    `~1.65 ms` band
  - pick the next phase from the current clean longer-run end-to-end load trace, with the largest
    remaining stable costs now centered on `Load via SKSvg.FromSvg`, `Control-like source load`,
    `Mutate + retained scene rebuild`, and the still-noisy retained-scene compile trace rather than
    another retained-scene render command-count or PNG filter phase
- completed:
  - cached `SvgPath` path-data hashes across fresh parsed documents so retained-scene compile no
    longer rescans every `<path d="...">` segment list just to hit the shared `SKPath` cache
  - kept the change narrow:
    - the hash computation lives in `Svg.Custom`, not `externals/SVG`
    - parser-side shared path-data prototypes now carry a reusable path-data hash alongside the
      cloned `SvgPathSegmentList`
    - compile still falls back to recomputing the hash if a path is created programmatically or its
      segment count changed after mutation
  - targeted mutation safety is covered by
    - `RetainedSceneGraph_ApplyMutation_UpdatesDirectPathGeometryAfterCachedCompile`
  - focused `solar battery.svg` reruns moved:
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshParsedDocument`
      `1.210 ms` -> `850.854 us`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvg` `2.026 ms` -> `1.257 ms`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvgAndAccessModel` measured `2.198 ms`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvgFromStringWithBaseUri` reran noisy at `2.098 ms`, so
      that row is being treated as directional only rather than the acceptance metric for this
      phase
  - this phase is being kept as a fresh parsed-document compile reuse win: the stable acceptance
    signal is the lower `CompileViaSceneRuntimeWithFreshParsedDocument` result, with the generic
    `LoadViaSkSvg` rerun also moving down, but the direct string-base-URI load path stayed too
    noisy to use as the main keep signal
- completed:
  - replaced the compatibility loader's per-element heap `ParseFrame` objects with an indexed
    value-type frame list in `Svg.Custom`, so structure build no longer allocates one extra object
    for every XML element while walking the document tree
  - kept the change narrow:
    - the parser/tree change stays inside `Svg.Custom`; no `externals/SVG` sources were modified
    - XML node handling and text-content aggregation semantics stay the same, only the internal
      frame stack representation changed
  - targeted parser regression coverage:
    - `SvgDocumentCompatibilityLoaderTests` (`36` rows passed)
  - focused `solar battery.svg` reruns moved:
    - `SvgCustomParsePhaseBenchmarks.LoadStructureOnly` `252.561 us` -> `180.375 us`
    - `SvgLoadPipelineBenchmarks.ParseSvgDocumentFromString` `187.746 us` -> `185.793 us`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvg` `1.257 ms` -> `1.253 ms`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvgFromStringWithBaseUri` reran noisy at `1.907 ms`, so
      that row is being treated as directional only rather than the acceptance metric for this
      phase
  - this phase is being kept as a parser structure-build allocation win: the stable acceptance
    signal is the much lower `LoadStructureOnly` result, while the full parse benchmark moved down
    slightly and the generic `LoadViaSkSvg` row stayed in the same lower `~1.25 ms` band
- completed:
  - deferred `Svg.Custom` mixed-content node tracking until content actually appears, so the
    compatibility loader no longer appends every child element into `Nodes` during structure build
    when that element never needs mixed-content ordering
  - kept the change narrow:
    - the parser/tree change stays inside `Svg.Custom`; no `externals/SVG` sources were modified
    - child element order is still preserved for mixed text content by backfilling prior children
      into `Nodes` the first time a real content node is seen
  - targeted regression coverage:
    - `SvgDocumentCompatibilityLoaderTests` (`37` rows passed)
    - added a child-first mixed-content order regression for
      `<text><tspan>...</tspan>tail</text>`
  - focused `solar battery.svg` reruns moved:
    - `SvgCustomParsePhaseBenchmarks.LoadStructureOnly` `180.375 us` -> `168.446 us`
    - `SvgLoadPipelineBenchmarks.ParseSvgDocumentFromString` `185.793 us` -> `178.472 us`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvg` reran noisy at `1.400 ms`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvgFromStringWithBaseUri` reran noisy at `1.658 ms`
  - the selecting 30-iteration profiler trace for this step still pointed at parse/compile work as
    top cold-load cost, but the confirming profiler rerun after the change was too noisy to use as
    an acceptance metric, so this phase is being kept as a parser structure-build win based on the
    stable focused parse benchmarks rather than the whole-load mean
- completed:
  - added a shared shape-path cache in `PathingService` for direct rectangle, circle, ellipse, and
    line path materialization across fresh parsed documents, so repeated retained-scene compiles no
    longer rebuild the same simple geometry paths every time a new `SvgDocument` instance is parsed
  - kept the change narrow:
    - the reuse lives in `Svg.Model/Services/PathingService.cs`; no `externals/SVG` sources were
      modified
    - the cache keys are based on resolved device-space geometry plus `fill-rule`, so it only
      shares paths when the final emitted shape is identical for the current viewport
  - targeted regression coverage:
    - `ReusesSharedPathDataAcrossFreshParsedDocuments`
    - `RetainedSceneGraph_ApplyMutation_UpdatesDirectPathGeometryAfterCachedCompile`
  - focused `solar battery.svg` reruns moved:
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshParsedDocument`
      `850.854 us` -> `822.819 us`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvg` `1.257 ms` -> `1.124 ms`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime` reran at `306.214 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader` reran at
      `310.915 us`
    - `SvgLoadPipelineBenchmarks.LoadViaSkSvgAndAccessModel` and
      `LoadViaSkSvgFromStringWithBaseUri` stayed noisy in short-run BDN, so they are directional
      only for this phase
  - this phase is being kept as a fresh parsed-document compile/load reuse win: the stable
    acceptance signal is the lower `CompileViaSceneRuntimeWithFreshParsedDocument` result, with the
    generic `LoadViaSkSvg` rerun also moving down
- completed:
  - added same-document retained compile metadata caching for `SvgSceneCompiler`, with invalidation
    in `Svg.Custom` on attribute changes so repeated compiles of the same parsed SVG no longer
    recompute transforms, antialias, feature gates, and retained visual-state parsing for every
    element on every pass
  - kept the change narrow:
    - the invalidation seam lives in `Svg.Custom/SceneGraph/SvgElement.SceneCompileCache.cs`, and
      the reuse itself stays inside `Svg.SceneGraph/SvgSceneCompiler.cs`; no `externals/SVG`
      sources were modified
    - this is intentionally a same-document compile optimization, not a fresh-parse optimization,
      so fresh parsed-document rows are only expected to stay flat
  - targeted regression coverage:
    - `RetainedSceneGraph_ApplyMutation_UpdatesDirectRectGeometryAfterCachedCompile`
    - `RetainedSceneGraph_ApplyMutation_UpdatesDirectPathGeometryAfterCachedCompile`
    - `RetainedSceneGraph_ApplyMutation_UpdatesDirectVisualTransformAfterCachedCompile`
  - focused `solar battery.svg` reruns moved:
    - `SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly` `203.194 us` -> `189.559 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime` `299.740 us` -> `297.035 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader`
      `299.036 us` -> `284.988 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshParsedDocument` reran at
      `849.717 us`, which is within noise for this phase and confirms the win is not coming from a
      fresh-parse path
    - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationAndFullRebuild` measured `1.506 ms`
  - clean 30-iteration profiler rerun moved:
    - `Compile retained scene (parsed doc)` `2.46 ms` -> `1.82 ms`
    - `Mutate + retained scene rebuild` `4.92 ms` -> `4.52 ms`
  - this phase is being kept as a same-document retained-scene compile win: the stable acceptance
    signal is the much lower profiler `Compile retained scene (parsed doc)` lane, with the focused
    node-tree/runtime compile rows moving in the same direction while fresh parsed-document compile
    stayed effectively flat
- completed:
  - added a narrow simple-static republish fast path in `SKSvg.RenderRetainedSceneDocument(...)`,
    so same-document static `FromSvgDocument(...)` reloads and retained-scene mutation renders no
    longer go through the full rendered-picture teardown path when the only live published state is
    the current native picture plus save-cache bytes
  - kept the change narrow:
    - the fast path only activates for static, non-animation, non-native-composition cases with no
      deferred shim model, wireframe picture, retained picture cache, or cached retained node
      pictures
    - it still clears cached save bytes before swapping the picture, so public `Save(...)` output
      stays correct after mutation or same-document reload
  - targeted regression coverage:
    - `SKSvgRebuildFromModelTests.FromSvgDocument_SameStaticDocumentReload_LeavesModelDeferredUntilRequested`
    - `SKSvgRebuildFromModelTests.TryApplyRetainedSceneMutationAndRender_LeavesModelDeferredUntilRequested`
  - focused `solar battery.svg` reruns moved:
    - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationAndRender` `382.137 us` -> `376.054 us`
    - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationAndFullRebuild` `1.529 ms` -> `1.503 ms`
    - `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` reran noisy at `701.170 us`, so
      that row is being treated as directional only rather than the acceptance metric for this
      phase
  - clean 30-iteration profiler reruns stayed in the lower mutation/load band:
    - `Mutate + full FromSvgDocument rebuild` `7.59 ms` -> `5.92 ms`, then `6.12 ms` on confirming
      rerun
    - `Mutate + retained scene rebuild` `6.10 ms` -> `4.89 ms`, then `4.08 ms` on confirming
      rerun
    - broader load totals also reran lower, but they are still being treated as supporting context
      rather than the main keep signal for this phase
  - this phase is being kept as a retained-mutation and same-document static reload publish win:
    the focused mutation benchmarks moved down modestly, and the clean longer-run mutation
    profiler lanes stayed materially below the immediate pre-change baseline on both reruns
- completed:
  - added versioned bounds caching in `src/ShimSkiaSharp/SKPath.cs`, so repeated `SKPath.Bounds`
    reads no longer rescan every command list when the path has not changed
  - kept the change narrow:
    - the cache is entirely local to the shim `SKPath` type; no external `SVG` sources were
      modified
    - bounds invalidate automatically through the existing path version tracking, so any command or
      fill-type mutation still forces a fresh bounds recomputation on the next read
    - because shared path instances from `PathingService` are reused across fresh parsed documents,
      this helps cold load, fresh retained-scene compile, same-document compile, and native record
      paths without another compile-local cache layer
  - targeted regression coverage:
    - `ShimSkiaSharp.UnitTests.SKPathTests`
  - focused `solar battery.svg` reruns moved:
    - `SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly` `190.412 us` -> `180.216 us`
    - `SvgRetainedSceneCompileBenchmarks.RegisterDependenciesOnly` `157.037 us` -> `150.249 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime` `293.336 us` -> `273.921 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneCompiler` `294.160 us` -> `277.268 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader`
      `300.760 us` -> `292.521 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshParsedDocument`
      `920.888 us` -> `876.395 us`
    - `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` `618.560 us` -> `655.316 us`
      on the short-run rerun, which is noisy and not the acceptance signal for this phase
    - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationOnly` measured `51.349 us`
    - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationAndRender` measured `380.720 us`
    - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationAndFullRebuild` measured `1.507 ms`
  - clean 30-iteration standalone profiler rerun moved:
    - `Parse SvgDocument from string` `1.29 ms` -> `0.97 ms`
    - `Compile retained scene (parsed doc)` `1.76 ms` -> `1.54 ms`
    - `Create native SKPicture` `0.47 ms` -> `0.44 ms`
    - `Render native picture to bitmap` `0.46 ms` -> `0.35 ms`
    - `Encode native picture to PNG` `3.39 ms` -> `3.06 ms`
    - `Load via SKSvg.FromSvg` `4.32 ms` -> `3.66 ms`
    - `Control-like source load` `4.17 ms` -> `3.46 ms`
    - `Mutate + full FromSvgDocument rebuild` `6.06 ms` -> `5.47 ms`
    - `Mutate + retained scene rebuild` `4.69 ms` -> `4.79 ms`, which is being treated as noise
      because the focused mutation benchmarks stayed in the same band
  - this phase is being kept as a cross-cutting path-bounds reuse win: the stable acceptance
    signal is the lower standalone profiler load/compile lanes plus the lower compile breakdown
    rows, not the noisy short-run fresh-load rerun
- completed:
  - rejected a narrow `SvgElementFactory` common-tag switch fast path after focused `solar
    battery.svg` reruns showed only a small `CreateElementsOnly` win while structure-build and load
    stayed flat or noisy, so that experiment was dropped instead of being recorded as a kept phase
  - added same-document scene-graph address-key reuse in `Svg.Custom`, with cache validation
    against the current parent, parent address key, and child index so repeated compiles of the
    same parsed document no longer rebuild element address strings from scratch while structural
    DOM edits still force the cache to recompute on demand
  - kept the change narrow:
    - the reusable address-key state lives on `Svg.Custom` elements in
      `src/Svg.Custom/SceneGraph/SvgElement.AddressKeyCache.cs`
    - the compile/runtime consumers stay in `Svg.SceneGraph`, mainly
      `src/Svg.SceneGraph/SvgElementAddressKeyCache.cs` and
      `src/Svg.SceneGraph/SvgSceneCompiler.cs`
    - no `externals/SVG` sources were modified
  - targeted regression coverage:
    - `SceneRuntime_RecompileAfterSiblingInsertion_UsesCurrentElementAddressKeys`
    - `RetainedSceneGraph_ApplyMutation_UpdatesDirectRectGeometryAfterCachedCompile`
    - `RetainedSceneGraph_ApplyMutation_UpdatesDirectPathGeometryAfterCachedCompile`
    - `RetainedSceneGraph_ApplyMutation_UpdatesDirectVisualTransformAfterCachedCompile`
  - focused `solar battery.svg` reruns moved:
    - `SvgRetainedSceneCompileBenchmarks.CompileNodeTreeOnly` `184.939 us` -> `169.340 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntime` `275.022 us` -> `264.504 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneCompiler` `271.676 us` -> `266.293 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshAssetLoader`
      `282.516 us` -> `269.303 us`
    - `SvgRetainedSceneCompileBenchmarks.CompileViaSceneRuntimeWithFreshParsedDocument`
      `863.551 us` -> `860.460 us`, effectively flat as expected for a same-document cache
    - `SvgRetainedSceneCompileBenchmarks.CreateSceneDocumentFromCompiledTree`
      `95.876 us` -> `85.517 us`
    - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationOnly` `51.349 us` -> `47.724 us`
    - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationAndFullRebuild`
      `1.503 ms` -> `1.396 ms`
  - clean longer-run profiler reruns after this phase stayed too noisy to use as the acceptance
    signal for compile; the first rerun inflated nearly every lane together, and the confirming
    rerun still disagreed with the stable focused compile benchmarks, so this phase is being kept
    on the consistent retained-scene compile and same-document full-rebuild benchmark wins instead
    of the profiler mean
- completed:
  - added a same-document attribute-mutation reload fast path in `src/Svg.Skia/SKSvg.Model.cs`,
    using pending changed-attribute tracking from `src/Svg.Custom/SceneGraph/SvgElement.SceneCompileCache.cs`
    so `SKSvg.FromSvgDocument(svg.SourceDocument)` can reuse the retained-scene mutation pipeline
    instead of recompiling the full document when the edit came through tracked attribute changes
  - kept the change narrow:
    - the change tracking stays in `Svg.Custom` rather than `externals/SVG`
    - mutation tracking is armed lazily when `SKSvg.SourceDocument` is accessed, which matches the
      editing/tooling workflow that mutates the live document and avoids paying a whole-document
      setup cost on cold loads that never touch `SourceDocument`
    - if no pending tracked attribute changes are available, or if retained-scene mutation cannot
      apply them cleanly, the existing full `FromSvgDocument(...)` rebuild path still runs
  - targeted regression coverage:
    - `FromSvgDocument_SameStaticDocumentReload_LeavesModelDeferredUntilRequested`
    - `TryApplyRetainedSceneMutationAndRender_LeavesModelDeferredUntilRequested`
  - focused `solar battery.svg` reruns moved:
    - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationAndFullRebuild`
      `1.503 ms` -> `431.266 us`
    - `SvgRetainedSceneMutationBenchmarks.ApplyFillMutationAndRender` stayed in the existing
      sub-`0.5 ms` band via the unchanged retained-scene mutation path
    - `SvgAnimationLoadBenchmarks.LoadParsedDocumentIntoFreshSkSvg` reran noisy at `874.702 us`,
      so it is not being used as the acceptance signal for this phase
  - clean 30-iteration standalone profiler rerun moved:
    - `Mutate + full FromSvgDocument rebuild` `7.12 ms` -> `5.44 ms`
    - `Mutate + retained scene rebuild` `5.02 ms` -> `4.30 ms`
    - broader load/compile totals stayed mixed enough that they are being treated as supporting
      context rather than the main keep signal for this phase
  - this phase is being kept as a same-document editing/tooling win: the stable acceptance signal
    is the much lower focused `ApplyFillMutationAndFullRebuild` benchmark, backed by the lower
    standalone mutation profiler lanes, while broader cold-load rows remain too noisy to use as
    the primary keep metric

## Validation Rules

For each phase:

1. Run targeted benchmarks against `file:solar battery.svg`.
2. Run `dotnet build tests/Svg.Skia.Benchmarks/Svg.Skia.Benchmarks.csproj -c Release`.
3. When behavior changes, run the smallest relevant unit/regression subset before broader validation.
4. Keep parser changes inside `Svg.Custom` instead of `externals/SVG`.

## Immediate Next Steps

1. Stop local compile and fresh-startup chasing on `solar battery.svg` unless a clean single-process
   end-to-end benchmark or profiler trace points at a stable new hotspot; the latest kept
   same-document retained-scene runtime compile path is now in the mid-`260 us` band,
   `CompileNodeTreeOnly` is down into the high-`160 us` band, and the same-document full rebuild
   edit lane is now down in the low-`400 us` band, so another blind startup micro-phase is unlikely to beat a
   hotspot chosen from a fresh profiler or focused mutation trace.
2. If a new pass is needed anyway, choose it from clean longer-run end-to-end data instead of
   another isolated microbenchmark, and prefer only hotspots that are clearly above the current
   parse/render tier; the warm repeated-save path is now effectively removed, the retained-scene
   replay path is down into the sub-millisecond profiler band for `solar battery.svg`, the cold PNG
   codec path is in the low-`3 ms` profiler band, and the next target should now come from load or
   retained-mutation behavior rather than another render-command or PNG filter micro-phase.
3. If compile work resumes again, start from a fresh retained-scene breakdown before changing code;
   after the kept same-document address-key cache phase, node-tree build is still the largest
   compile-local slice at about `169 us` while `RegisterDependenciesOnly` is around `175 us`, so
   another compile-local change should only be kept if it moves the runtime compile or full rebuild
   benchmark as well, not just an isolated internal micro-slice.
4. Prefer real control-like source-load paths over parser-internal style-flush experiments unless a
   focused parse benchmark proves otherwise; the staged-style winner cache regressed
   `ParseSvgDocumentFromString`, while the stream/base-URI cleanup and the direct string source path
   both produced measurable end-to-end load wins and removed duplicated entry-wrapper overhead.
5. Deprioritize another same-document `FromSvgDocument(...)` reload phase unless a fresh editing
   trace pushes it back to the top; the kept attribute-tracked reload path has moved that lane
   close to the direct retained-scene mutation render path, so the next phase should come from a
   fresh clean load/control-like or codec trace instead of assuming there is still easy headroom in
   same-document full rebuilds.
