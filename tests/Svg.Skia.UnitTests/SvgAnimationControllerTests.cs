using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShimSkiaSharp;
using Svg;
using Svg.Model;
using Svg.Model.Services;
using Svg.Transforms;
using Xunit;
using SkiaAlphaType = SkiaSharp.SKAlphaType;
using SkiaBitmap = SkiaSharp.SKBitmap;
using SkiaColors = SkiaSharp.SKColors;
using SkiaColorType = SkiaSharp.SKColorType;

namespace Svg.Skia.UnitTests;

public class SvgAnimationControllerTests
{
    [Fact]
    public void CreateAnimatedDocument_AppliesCoreAnimationTypes()
    {
        var document = SvgService.FromSvg(AnimationRuntimeSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        Assert.True(controller.HasAnimations);

        var frameChangedCount = 0;
        controller.FrameChanged += (_, args) =>
        {
            frameChangedCount++;
            Assert.Equal(TimeSpan.FromSeconds(1), args.Time);
        };

        controller.Clock.Seek(TimeSpan.FromSeconds(1));
        Assert.Equal(1, frameChangedCount);

        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));

        var move = animated.GetElementById<SvgRectangle>("move");
        Assert.NotNull(move);
        Assert.Equal(10f, move!.X.Value, 3);

        var color = animated.GetElementById<SvgRectangle>("color");
        Assert.NotNull(color);
        var fill = Assert.IsType<SvgColourServer>(color!.Fill);
        Assert.Equal((byte)0, fill.Colour.R);
        Assert.InRange(fill.Colour.G, (byte)127, (byte)128);
        Assert.InRange(fill.Colour.B, (byte)127, (byte)128);

        var transform = animated.GetElementById<SvgRectangle>("transform");
        Assert.NotNull(transform);
        var translate = Assert.IsType<SvgTranslate>(Assert.Single(transform!.Transforms));
        Assert.Equal(5f, translate.X, 3);
        Assert.Equal(0f, translate.Y, 3);

        var set = animated.GetElementById<SvgRectangle>("set");
        Assert.NotNull(set);
        Assert.Equal("visible", set!.Visibility);

        var motion = animated.GetElementById<SvgCircle>("motion");
        Assert.NotNull(motion);
        var motionTranslate = Assert.IsType<SvgTranslate>(Assert.Single(motion!.Transforms));
        Assert.Equal(5f, motionTranslate.X, 3);
        Assert.Equal(30f, motionTranslate.Y, 3);
    }

    [Fact]
    public void SetAnimationTime_RebuildsRenderedState()
    {
        using var svg = new SKSvg();
        svg.FromSvg(HitTestAnimationSvg);

        Assert.True(svg.HasAnimations);
        Assert.NotNull(svg.AnimationController);
        Assert.Equal("target", svg.HitTestTopmostElement(new SKPoint(2, 2))?.ID);

        var invalidatedCount = 0;
        svg.AnimationInvalidated += (_, args) =>
        {
            invalidatedCount++;
            Assert.Equal(TimeSpan.FromSeconds(2), args.Time);
        };

        svg.SetAnimationTime(TimeSpan.FromSeconds(2));

        Assert.Equal(1, invalidatedCount);
        Assert.Null(svg.HitTestTopmostElement(new SKPoint(2, 2)));
        Assert.Equal("target", svg.HitTestTopmostElement(new SKPoint(22, 2))?.ID);
    }

    [Fact]
    public void SetAnimationTime_UsesAnimationLayerCachingForSupportedTopLevelAnimations()
    {
        using var svg = new SKSvg();
        svg.FromSvg(TopLevelLayeredAnimationSvg);

        Assert.True(svg.HasAnimations);
        Assert.True(svg.UsesAnimationLayerCaching);
        Assert.Equal("static", svg.HitTestTopmostElement(new SKPoint(2, 2))?.ID);
        Assert.Equal("moving", svg.HitTestTopmostElement(new SKPoint(2, 14))?.ID);

        svg.SetAnimationTime(TimeSpan.FromSeconds(2));

        Assert.True(svg.UsesAnimationLayerCaching);
        Assert.Equal("static", svg.HitTestTopmostElement(new SKPoint(2, 2))?.ID);
        Assert.Null(svg.HitTestTopmostElement(new SKPoint(2, 14)));
        Assert.Equal("moving", svg.HitTestTopmostElement(new SKPoint(12, 14))?.ID);
    }

    [Fact]
    public void SetAnimationTime_PreservesEquivalentUnchangedAnimatedSubtreeContent()
    {
        using var svg = new SKSvg();
        svg.FromSvg(SubtreeLayeredAnimationSvg);

        Assert.True(svg.HasAnimations);
        Assert.True(svg.UsesAnimationLayerCaching);

        var initialSignatures = GetAnimatedSubtreeSignatures(svg);

        svg.SetAnimationTime(TimeSpan.FromSeconds(1));

        var updatedSignatures = GetAnimatedSubtreeSignatures(svg);
        Assert.Equal(2, initialSignatures.Count);
        Assert.Equal(2, updatedSignatures.Count);
        Assert.Equal(initialSignatures[0], updatedSignatures[0]);
        Assert.NotEqual(initialSignatures[1], updatedSignatures[1]);
    }

    [Fact]
    public void SetAnimationTime_UsesAnimationLayerCachingForDefsBackedUseTargets()
    {
        using var svg = new SKSvg();
        svg.FromSvg(DefsBackedAnimationSvg);

        Assert.True(svg.HasAnimations);
        Assert.True(svg.UsesAnimationLayerCaching);
        Assert.Equal("instance", svg.HitTestTopmostElement(new SKPoint(2, 2))?.ID);

        svg.SetAnimationTime(TimeSpan.FromSeconds(2));

        Assert.True(svg.UsesAnimationLayerCaching);
        Assert.Null(svg.HitTestTopmostElement(new SKPoint(2, 2)));
        Assert.Equal("instance", svg.HitTestTopmostElement(new SKPoint(12, 2))?.ID);
    }

    [Fact]
    public void SetAnimationTime_UsesAnimationLayerCachingForPaintServerTargets()
    {
        using var svg = new SKSvg();
        svg.FromSvg(PaintServerAnimationSvg);

        Assert.True(svg.HasAnimations);
        Assert.True(svg.UsesAnimationLayerCaching);
        Assert.NotNull(svg.RetainedSceneGraph);

        svg.SetAnimationTime(TimeSpan.FromSeconds(1));

        Assert.True(svg.UsesAnimationLayerCaching);
        Assert.NotNull(svg.Picture);
        Assert.NotNull(svg.RetainedSceneGraph);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
    }

    [Fact]
    public void Save_SucceedsWhenAnimationLayerCachingIsActive()
    {
        using var svg = new SKSvg();
        svg.FromSvg(TopLevelLayeredAnimationSvg);

        Assert.True(svg.HasAnimations);
        Assert.True(svg.UsesAnimationLayerCaching);

        using var initialStream = new MemoryStream();
        Assert.True(svg.Save(initialStream, SkiaColors.Transparent));
        Assert.True(initialStream.Length > 0);

        svg.SetAnimationTime(TimeSpan.FromSeconds(1));

        using var updatedStream = new MemoryStream();
        Assert.True(svg.Save(updatedStream, SkiaColors.Transparent));
        Assert.True(updatedStream.Length > 0);
    }

    [Fact]
    public void TryApplyRetainedSceneMutationByIdAndRender_DisablesAnimationLayerCachingBeforeDraw()
    {
        using var svg = new SKSvg();
        svg.FromSvg(TopLevelLayeredAnimationSvg);

        Assert.True(svg.HasAnimations);
        Assert.True(svg.UsesAnimationLayerCaching);

        var sourceDocument = GetRenderedDocument(svg);
        var staticRect = sourceDocument.GetElementById<SvgRectangle>("static");
        Assert.NotNull(staticRect);
        staticRect!.Fill = new SvgColourServer(System.Drawing.Color.Lime);

        var updated = svg.TryApplyRetainedSceneMutationByIdAndRender("static", new[] { "fill" }, out var result);

        Assert.True(updated);
        Assert.NotNull(result);
        Assert.True(result!.Succeeded);
        Assert.False(svg.UsesAnimationLayerCaching);

        using var bitmap = DrawBitmap(svg);
        var pixel = bitmap.GetPixel(2, 2);
        Assert.True(pixel.Alpha > 200);
        Assert.True(pixel.Red < 80);
        Assert.True(pixel.Green > 200);
        Assert.True(pixel.Blue < 80);
    }

    [Fact]
    public void SetAnimationTime_RebuildsInheritedStrokeAnimationsUnderLayerCaching()
    {
        using var svg = new SKSvg();
        svg.FromSvg(InheritedStrokeAnimationSvg);

        Assert.True(svg.HasAnimations);
        Assert.True(svg.UsesAnimationLayerCaching);

        using var initialBitmap = RenderBitmap(svg);
        var initialStroke = initialBitmap.GetPixel(20, 8);
        Assert.True(initialStroke.Red > 180);
        Assert.True(initialStroke.Green > 180);
        Assert.True(initialStroke.Blue < 80);

        svg.SetAnimationTime(TimeSpan.FromSeconds(4));

        using var updatedBitmap = RenderBitmap(svg);
        var updatedStroke = updatedBitmap.GetPixel(20, 8);
        Assert.True(updatedStroke.Alpha > 200);
        Assert.True(updatedStroke.Red < 80);
        Assert.True(updatedStroke.Green < 80);
        Assert.True(updatedStroke.Blue < 80);
    }

    [Fact]
    public void SetAnimationTime_RebuildsInheritedFontSizeAndFillAnimationsUnderLayerCaching()
    {
        using var svg = new SKSvg();
        svg.FromSvg(InheritedFontSizeAnimationSvg);

        Assert.True(svg.HasAnimations);
        Assert.True(svg.UsesAnimationLayerCaching);

        using var initialBitmap = RenderBitmap(svg);
        var initialInside = initialBitmap.GetPixel(5, 5);
        var initialExpandedArea = initialBitmap.GetPixel(15, 5);
        Assert.True(initialInside.Alpha > 200);
        Assert.True(initialInside.Blue > 180);
        Assert.True(initialInside.Green < 80);
        Assert.True(initialExpandedArea.Alpha < 32);

        svg.SetAnimationTime(TimeSpan.FromSeconds(2));

        using var updatedBitmap = RenderBitmap(svg);
        var updatedInside = updatedBitmap.GetPixel(5, 5);
        var updatedExpandedArea = updatedBitmap.GetPixel(15, 5);
        Assert.True(updatedInside.Alpha > 200);
        Assert.True(updatedInside.Green > updatedInside.Blue);
        Assert.True(updatedExpandedArea.Alpha > 200);
        Assert.True(updatedExpandedArea.Green > updatedExpandedArea.Blue);
    }

    [Fact]
    public void CreateAnimatedDocument_RendersInheritedGradientStopOpacityAnimations()
    {
        var document = SvgService.FromSvg(InheritedGradientStopOpacityAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2));
        foreach (var animation in animated.Descendants().OfType<SvgAnimationElement>().ToArray())
        {
            animation.Parent?.Children.Remove(animation);
        }

        using var svg = SKSvg.CreateFromSvgDocument(animated);
        using var updatedBitmap = RenderBitmap(svg);
        var updatedRight = updatedBitmap.GetPixel(18, 5);
        Assert.True(updatedRight.Alpha > 200, $"Expected opaque pixel, got {updatedRight}.");
        Assert.True(updatedRight.Red < 80, $"Expected low red channel, got {updatedRight}.");
        Assert.True(updatedRight.Green > 100, $"Expected green channel, got {updatedRight}.");
        Assert.True(updatedRight.Blue < 80, $"Expected low blue channel, got {updatedRight}.");
    }

    [Fact]
    public void CreateAnimatedDocument_RebindsRootDeferredPaintServers()
    {
        var document = SvgService.FromSvg(RootDeferredPaintServerAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2));

        var fill = Assert.IsType<SvgDeferredPaintServer>(animated.Fill);
#pragma warning disable CS0618
        Assert.Same(animated, fill.Document);
#pragma warning restore CS0618
    }

    [Fact]
    public void CreateAnimatedDocument_AppliesInheritedCssAnimationsWhenWhitespaceCssParameterIsProvided()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(InheritedFontSizeAnimationSvg));
        var document = SvgService.Open(stream, new SvgParameters(null, " "));
        Assert.NotNull(document);
        Assert.Null(document!.Parent);

        using var controller = new SvgAnimationController(document);
        Assert.True(controller.HasAnimations);

        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));
        var root = animated.GetElementById<SvgGroup>("animated-root");
        Assert.NotNull(root);

        Assert.Equal(15f, root!.FontSize.Value, 3);

        var fill = Assert.IsType<SvgColourServer>(root.Fill);
        Assert.Equal((byte)0, fill.Colour.R);
        Assert.InRange(fill.Colour.G, (byte)84, (byte)86);
        Assert.InRange(fill.Colour.B, (byte)127, (byte)128);
    }

    [Fact]
    public void CreateAnimatedDocument_PreservesAlphaWhenInterpolatingHexAlphaPaint()
    {
        var document = SvgService.FromSvg(HexAlphaPaintAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));
        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);

        var fill = Assert.IsType<SvgColourServer>(target!.Fill);
        Assert.Equal((byte)0, fill.Colour.R);
        Assert.Equal((byte)0, fill.Colour.G);
        Assert.Equal((byte)0, fill.Colour.B);
        Assert.Equal((byte)64, fill.Colour.A);
    }

    [Fact]
    public void SetAnimationTime_RebuildsRootViewBoxAnimations()
    {
        using var svg = new SKSvg();
        svg.FromSvg(RootViewBoxAnimationSvg);

        Assert.True(svg.HasAnimations);
        Assert.Equal("target", svg.HitTestTopmostElement(new SKPoint(65, 65))?.ID);
        Assert.Null(svg.HitTestTopmostElement(new SKPoint(25, 25)));

        svg.SetAnimationTime(TimeSpan.FromSeconds(2));

        var renderedDocument = GetRenderedDocument(svg);
        Assert.Equal(50f, renderedDocument.ViewBox.MinX, 3);
        Assert.Equal(50f, renderedDocument.ViewBox.MinY, 3);
        Assert.Equal(50f, renderedDocument.ViewBox.Width, 3);
        Assert.Equal(50f, renderedDocument.ViewBox.Height, 3);
        Assert.Null(svg.HitTestTopmostElement(new SKPoint(65, 65)));
        Assert.Equal("target", svg.HitTestTopmostElement(new SKPoint(15, 15))?.ID);
    }

    [Fact]
    public void SetAnimationTime_RebuildsRetainedSceneCullRectForRootViewBoxSizeChanges()
    {
        using var svg = new SKSvg();
        svg.FromSvg(RootViewBoxSizeAnimationSvg);

        Assert.True(svg.HasAnimations);
        Assert.NotNull(svg.Model);
        Assert.NotNull(svg.RetainedSceneGraph);
        Assert.Equal(100f, svg.Model!.CullRect.Width, 3);
        Assert.Equal(100f, svg.Model.CullRect.Height, 3);
        Assert.Equal(100f, svg.RetainedSceneGraph!.CullRect.Width, 3);
        Assert.Equal(100f, svg.RetainedSceneGraph.CullRect.Height, 3);

        svg.SetAnimationTime(TimeSpan.FromSeconds(2));

        Assert.NotNull(svg.Model);
        Assert.NotNull(svg.RetainedSceneGraph);
        Assert.Equal(50f, svg.Model!.CullRect.Width, 3);
        Assert.Equal(50f, svg.Model.CullRect.Height, 3);
        Assert.Equal(50f, svg.RetainedSceneGraph!.CullRect.Width, 3);
        Assert.Equal(50f, svg.RetainedSceneGraph.CullRect.Height, 3);
    }

    [Fact]
    public void SetAnimationTime_RebuildsW3CRootViewBoxFixture()
    {
        var path = GetW3CTestSvgPath("animate-elem-38-t.svg");
        Assert.True(File.Exists(path));

        using var svg = new SKSvg();
        using var _ = svg.Load(path);

        Assert.True(svg.HasAnimations);
        Assert.False(svg.UsesAnimationLayerCaching);
        Assert.False(svg.SupportsNativeComposition);

        using var initialBitmap = RenderBitmap(svg);
        var initialSignature = GetBitmapSignature(initialBitmap);

        svg.SetAnimationTime(TimeSpan.FromSeconds(9));

        var renderedDocument = GetRenderedDocument(svg);
        Assert.Equal(100f, renderedDocument.ViewBox.MinX, 3);
        Assert.Equal(0f, renderedDocument.ViewBox.MinY, 3);
        Assert.Equal(200f, renderedDocument.ViewBox.Width, 3);
        Assert.Equal(200f, renderedDocument.ViewBox.Height, 3);
        Assert.False(svg.UsesAnimationLayerCaching);
        Assert.False(svg.SupportsNativeComposition);

        using var updatedBitmap = RenderBitmap(svg);
        var updatedSignature = GetBitmapSignature(updatedBitmap);
        Assert.NotEqual(initialSignature, updatedSignature);
    }

    [Fact]
    public void CreateAnimatedDocument_ParsesColonClockValues()
    {
        var document = SvgService.FromSvg(ColonClockAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        var beforeBegin = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));
        var beforeTarget = beforeBegin.GetElementById<SvgRectangle>("target");
        Assert.NotNull(beforeTarget);
        Assert.Equal(0f, beforeTarget!.X.Value, 3);

        var duringAnimation = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(3));
        var duringTarget = duringAnimation.GetElementById<SvgRectangle>("target");
        Assert.NotNull(duringTarget);
        Assert.Equal(5f, duringTarget!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_ComposesConcurrentAdditiveAnimationsFromCurrentFrameState()
    {
        var document = SvgService.FromSvg(ConcurrentAdditiveAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));
        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(7.5f, target!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_ComposesMotionWithEarlierTransformAnimation()
    {
        var document = SvgService.FromSvg(MotionAfterTransformAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));
        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(2, target!.Transforms.Count);

        var motionTranslate = Assert.IsType<SvgTranslate>(target.Transforms[0]);
        var transformTranslate = Assert.IsType<SvgTranslate>(target.Transforms[1]);
        Assert.Equal(10f, motionTranslate.X, 3);
        Assert.Equal(0f, motionTranslate.Y, 3);
        Assert.Equal(0f, transformTranslate.X, 3);
        Assert.Equal(5f, transformTranslate.Y, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_TreatsRepeatDurIndefiniteAsUnbounded()
    {
        var document = SvgService.FromSvg(RepeatDurationIndefiniteAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(4.5));
        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(5f, target!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_PreservesFiniteRepeatCountWhenRepeatDurIsIndefinite()
    {
        var document = SvgService.FromSvg(FiniteRepeatCountWithIndefiniteRepeatDurationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2.5));
        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(10f, target!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_RejectsNonFiniteClockValues()
    {
        var document = SvgService.FromSvg(NonFiniteClockAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2));
        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(0f, target!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_RejectsNonFiniteRepeatCountValues()
    {
        var document = SvgService.FromSvg(NonFiniteRepeatCountAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1.5));
        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(0f, target!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_IgnoresMalformedTransformValueLists()
    {
        var document = SvgService.FromSvg(MalformedTransformValuesSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        var exception = Record.Exception(() => controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1)));
        Assert.Null(exception);

        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));
        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        var translate = Assert.IsType<SvgTranslate>(Assert.Single(target!.Transforms));
        Assert.Equal(0f, translate.X, 3);
        Assert.Equal(0f, translate.Y, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_EnforcesMaximumActiveDuration()
    {
        var document = SvgService.FromSvg(MaximumDurationAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(3));
        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(4f, target!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_EnforcesMinimumActiveDuration()
    {
        var document = SvgService.FromSvg(MinimumDurationAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2.5));
        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(5f, target!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_ConstrainsIndefiniteSetWithValidMaximumDuration()
    {
        var document = SvgService.FromSvg(IndefiniteSetMaxDurationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        var during = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));
        var duringTarget = during.GetElementById<SvgRectangle>("capped");
        Assert.NotNull(duringTarget);
        Assert.Equal(10f, duringTarget!.X.Value, 3);

        var afterMax = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(3));
        var capped = afterMax.GetElementById<SvgRectangle>("capped");
        Assert.NotNull(capped);
        Assert.Equal(0f, capped!.X.Value, 3);

        var invalidPair = afterMax.GetElementById<SvgRectangle>("invalid-pair");
        Assert.NotNull(invalidPair);
        Assert.Equal(10f, invalidPair!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_AppliesZeroDurationAnimateImmediately()
    {
        var document = SvgService.FromSvg(ZeroDurationAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        var animated = controller.CreateAnimatedDocument(TimeSpan.Zero);
        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(10f, target!.X.Value, 3);
    }

    [Fact]
    public void Dispatcher_ClickOffsetEvent_StartsAnimationAtScheduledTime()
    {
        using var svg = new SKSvg();
        svg.FromSvg(EventBeginSvg);

        Assert.True(svg.HasAnimations);
        Assert.NotNull(svg.AnimationController);

        svg.SetAnimationTime(TimeSpan.FromSeconds(1));

        var dispatcher = new SvgInteractionDispatcher();
        var clickInput = new SvgPointerInput(
            new SKPoint(20, 20),
            SvgPointerDeviceType.Mouse,
            SvgMouseButton.Left,
            1,
            0,
            false,
            false,
            false,
            "pointer-1");

        _ = dispatcher.DispatchPointerPressed(svg, clickInput);
        _ = dispatcher.DispatchPointerReleased(svg, clickInput);

        var beforeStart = svg.AnimationController!.CreateAnimatedDocument(TimeSpan.FromSeconds(1.5));
        var beforeStartTarget = beforeStart.GetElementById<SvgRectangle>("target");
        Assert.NotNull(beforeStartTarget);
        Assert.Equal(0f, beforeStartTarget!.X.Value, 3);

        var duringAnimation = svg.AnimationController.CreateAnimatedDocument(TimeSpan.FromSeconds(3));
        var animatedTarget = duringAnimation.GetElementById<SvgRectangle>("target");
        Assert.NotNull(animatedTarget);
        Assert.Equal(5f, animatedTarget!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_ParsesEventTimingWithDottedIds()
    {
        using var svg = new SKSvg();
        svg.FromSvg(DottedIdEventBeginSvg);

        Assert.True(svg.HasAnimations);
        Assert.NotNull(svg.AnimationController);

        svg.SetAnimationTime(TimeSpan.FromSeconds(1));

        var dispatcher = new SvgInteractionDispatcher();
        var moveInput = new SvgPointerInput(
            new SKPoint(20, 20),
            SvgPointerDeviceType.Mouse,
            SvgMouseButton.None,
            0,
            0,
            false,
            false,
            false,
            "pointer-1");

        _ = dispatcher.DispatchPointerMoved(svg, moveInput);

        var animated = svg.AnimationController!.CreateAnimatedDocument(TimeSpan.FromSeconds(2));
        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(5f, target!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_ParsesNegativeEventOffsetsWithoutWhitespace()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NegativeEventOffsetBeginSvg);

        Assert.True(svg.HasAnimations);
        Assert.NotNull(svg.AnimationController);

        svg.SetAnimationTime(TimeSpan.FromSeconds(1));

        var dispatcher = new SvgInteractionDispatcher();
        var clickInput = new SvgPointerInput(
            new SKPoint(20, 20),
            SvgPointerDeviceType.Mouse,
            SvgMouseButton.Left,
            1,
            0,
            false,
            false,
            false,
            "pointer-1");

        _ = dispatcher.DispatchPointerPressed(svg, clickInput);
        _ = dispatcher.DispatchPointerReleased(svg, clickInput);

        var animated = svg.AnimationController!.CreateAnimatedDocument(TimeSpan.FromSeconds(1.5));
        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(7.5f, target!.X.Value, 3);
    }

    [Fact]
    public void RecordPointerEvent_PrunesObsoletePointerEventInstances()
    {
        var document = SvgService.FromSvg(MoveTriggeredAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        var trigger = document!.GetElementById("trigger");
        Assert.NotNull(trigger);

        for (var second = 0; second < 20; second++)
        {
            controller.Clock.Seek(TimeSpan.FromSeconds(second));
            Assert.True(controller.RecordPointerEvent(trigger, SvgPointerEventType.Move));
        }

        controller.Clock.Seek(TimeSpan.FromSeconds(19.5));
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(19.5));
        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(5f, target!.X.Value, 3);

        var instancesField = typeof(SvgAnimationController).GetField("_pointerEventInstances", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(instancesField);
        var instances = Assert.IsAssignableFrom<System.Collections.IDictionary>(instancesField!.GetValue(controller));
        Assert.Single(instances.Keys);

        var eventTimes = Assert.IsAssignableFrom<System.Collections.IList>(instances.Values.Cast<object>().Single());
        Assert.Single(eventTimes);
    }

    [Fact]
    public void CreateAnimatedDocument_RespectsEventBasedEndTiming()
    {
        var document = SvgService.FromSvg(EventEndSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        controller.Clock.Seek(TimeSpan.FromSeconds(2));

        var trigger = document!.GetElementById("trigger");
        Assert.NotNull(trigger);
        Assert.True(controller.RecordPointerEvent(trigger, SvgPointerEventType.Click));

        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(3));
        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(2f, target!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_UsesAnimateMotionValuesPath()
    {
        var document = SvgService.FromSvg(MotionValuesSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1.5));

        var motion = animated.GetElementById<SvgCircle>("motion");
        Assert.NotNull(motion);
        var translate = Assert.IsType<SvgTranslate>(Assert.Single(motion!.Transforms));
        Assert.Equal(10f, translate.X, 3);
        Assert.Equal(5f, translate.Y, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_ParsesWhitespaceHeavyTransformValues()
    {
        var document = SvgService.FromSvg(WhitespaceTransformValuesSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1.5));

        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        var translate = Assert.IsType<SvgTranslate>(Assert.Single(target!.Transforms));
        Assert.Equal(10f, translate.X, 3);
        Assert.Equal(5f, translate.Y, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_ParsesWhitespaceHeavyMotionValuesAndAutoReverseRotation()
    {
        var document = SvgService.FromSvg(WhitespaceMotionValuesSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1.5));

        var motion = animated.GetElementById<SvgCircle>("motion");
        Assert.NotNull(motion);
        Assert.Equal(2, motion!.Transforms.Count);

        var translate = Assert.IsType<SvgTranslate>(motion.Transforms[0]);
        var rotate = Assert.IsType<SvgRotate>(motion.Transforms[1]);
        Assert.Equal(10f, translate.X, 3);
        Assert.Equal(5f, translate.Y, 3);
        Assert.Equal(270f, rotate.Angle, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_HonorsSplineCalcMode()
    {
        var document = SvgService.FromSvg(SplineAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));

        var linear = animated.GetElementById<SvgRectangle>("linear");
        var spline = animated.GetElementById<SvgRectangle>("spline");
        Assert.NotNull(linear);
        Assert.NotNull(spline);
        Assert.Equal(5f, linear!.X.Value, 3);
        Assert.True(spline!.X.Value > linear.X.Value + 1f);

        var motionLinear = animated.GetElementById<SvgCircle>("motion-linear");
        var motionSpline = animated.GetElementById<SvgCircle>("motion-spline");
        Assert.NotNull(motionLinear);
        Assert.NotNull(motionSpline);

        var motionLinearTranslate = Assert.IsType<SvgTranslate>(Assert.Single(motionLinear!.Transforms));
        var motionSplineTranslate = Assert.IsType<SvgTranslate>(Assert.Single(motionSpline!.Transforms));
        Assert.Equal(5f, motionLinearTranslate.X, 3);
        Assert.True(motionSplineTranslate.X > motionLinearTranslate.X + 1f);
    }

    [Fact]
    public void CreateAnimatedDocument_UsesPacedSegmentTimingForAnimateValues()
    {
        var document = SvgService.FromSvg(PacedValuesAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));

        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(55f, target!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_UsesPacedSegmentTimingForAnimateTransform()
    {
        var document = SvgService.FromSvg(PacedTransformAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));

        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        var translate = Assert.IsType<SvgTranslate>(Assert.Single(target!.Transforms));
        Assert.Equal(55f, translate.X, 3);
        Assert.Equal(0f, translate.Y, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_PreservesBaseTransformForByOnlyAnimateTransform()
    {
        var document = SvgService.FromSvg(ByOnlyTransformWithBaseSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));

        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        var translate = Assert.IsType<SvgTranslate>(Assert.Single(target!.Transforms));
        Assert.Equal(12.5f, translate.X, 3);
        Assert.Equal(0f, translate.Y, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_SaturatesLargeRepeatIterationCounts()
    {
        var document = SvgService.FromSvg(LargeRepeatIterationAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromDays(30));

        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.True(target!.X.Value > 1_000_000f);
    }

    [Fact]
    public void CreateAnimatedDocument_ResolvesSyncbaseRepeatTiming()
    {
        var document = SvgService.FromSvg(SyncbaseRepeatTimingSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        var beforeRepeat = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(0.75));
        var beforeTarget = beforeRepeat.GetElementById<SvgRectangle>("target");
        Assert.NotNull(beforeTarget);
        Assert.Equal(0f, beforeTarget!.X.Value, 3);

        var afterRepeat = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1.5));
        var afterTarget = afterRepeat.GetElementById<SvgRectangle>("target");
        Assert.NotNull(afterTarget);
        Assert.Equal(5f, afterTarget!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_GeneratesSelfEndBeginIntervals()
    {
        var document = SvgService.FromSvg(SelfEndBeginTimingSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        var firstActive = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(0.5));
        Assert.Equal(10f, firstActive.GetElementById<SvgRectangle>("pulse")!.X.Value, 3);

        var firstGap = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1.5));
        Assert.Equal(0f, firstGap.GetElementById<SvgRectangle>("pulse")!.X.Value, 3);

        var secondActive = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2.5));
        Assert.Equal(10f, secondActive.GetElementById<SvgRectangle>("pulse")!.X.Value, 3);

        var followerActive = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(4.5));
        Assert.Equal(10f, followerActive.GetElementById<SvgRectangle>("follower")!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_UsesHalfOpenActiveIntervals()
    {
        var document = SvgService.FromSvg(HalfOpenIntervalAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));

        var removed = animated.GetElementById<SvgRectangle>("removed");
        Assert.NotNull(removed);
        Assert.Equal(0f, removed!.X.Value, 3);

        var frozen = animated.GetElementById<SvgRectangle>("frozen");
        Assert.NotNull(frozen);
        Assert.Equal(10f, frozen!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_TruncatesRestartedIntervalsForSyncbaseEnd()
    {
        var document = SvgService.FromSvg(RestartTruncationSyncbaseSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(0.75));

        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(2.5f, target!.Y.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_UsesSelfBeginTimingForEndInstances()
    {
        var document = SvgService.FromSvg(SelfBeginEndTimingSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(3));

        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(2f, target!.X.Value, 3);
    }

    [Fact]
    public void TryGetStartTime_ResolvesFutureSyncbaseIntervals()
    {
        var document = SvgService.FromSvg(FutureSyncbaseStartTimeSvg);
        Assert.NotNull(document);
        var animation = document!.GetElementById<SvgAnimate>("dependent");
        Assert.NotNull(animation);

        using var controller = new SvgAnimationController(document);
        var method = typeof(SvgAnimationController).GetMethod(
            "TryGetStartTime",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            new[] { typeof(SvgAnimationElement), typeof(TimeSpan), typeof(TimeSpan).MakeByRefType() },
            modifiers: null);
        Assert.NotNull(method);

        var args = new object?[] { animation, TimeSpan.Zero, default(TimeSpan) };
        var resolved = Assert.IsType<bool>(method!.Invoke(controller, args));

        Assert.True(resolved);
        Assert.Equal(TimeSpan.FromSeconds(2), Assert.IsType<TimeSpan>(args[2]));
    }

    [Fact]
    public void GetTimelineCallbacks_IncludesRepeatEvents()
    {
        var document = SvgService.FromSvg(RepeatTimelineCallbackSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var callbacks = InvokeTimelineCallbacks(controller, TimeSpan.FromSeconds(1), TimeSpan.Zero);

        Assert.Contains(callbacks, callback => callback.EventType == "repeatEvent" && callback.AttributeName == "onrepeat");
    }

    [Fact]
    public void CreateAnimatedDocument_IgnoresRepeatZeroTiming()
    {
        var document = SvgService.FromSvg(RepeatZeroTimingSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2));

        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(0f, target!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_IgnoresNonProgressingSelfBeginTiming()
    {
        var document = SvgService.FromSvg(NonProgressingSelfBeginTimingSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1.5));

        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.Equal(10f, target!.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_InterpolatesNumberListsAndPathData()
    {
        var document = SvgService.FromSvg(NumberListAndPathDataAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));

        var polygon = animated.GetElementById<SvgPolygon>("polygon");
        Assert.NotNull(polygon);
        Assert.Equal(10f, polygon!.Points[2].Value, 3);
        Assert.Equal(10f, polygon.Points[5].Value, 3);

        var path = animated.GetElementById<SvgPath>("path");
        Assert.NotNull(path);
        Assert.Contains("10", path!.PathData.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CreateAnimatedDocument_UsesDiscreteFallbackForNonInterpolableLinearValues()
    {
        var document = SvgService.FromSvg(NonInterpolableLinearAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        var beforeMidpoint = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(0.999));
        var beforeMidpointTarget = beforeMidpoint.GetElementById<SvgRectangle>("target");
        Assert.NotNull(beforeMidpointTarget);
        Assert.Equal("hidden", beforeMidpointTarget!.Visibility);

        var midpoint = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));
        var midpointTarget = midpoint.GetElementById<SvgRectangle>("target");
        Assert.NotNull(midpointTarget);
        Assert.Equal("visible", midpointTarget!.Visibility);

        var endpoint = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2));
        var endpointTarget = endpoint.GetElementById<SvgRectangle>("target");
        Assert.NotNull(endpointTarget);
        Assert.Equal("visible", endpointTarget!.Visibility);
    }

    [Fact]
    public void CreateAnimatedDocument_AppliesToOnlyNonInterpolableAttributesImmediately()
    {
        var document = SvgService.FromSvg(ToOnlyNonInterpolableAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(3));

        Assert.Equal(SvgCoordinateUnits.UserSpaceOnUse, animated.GetElementById<SvgClipPath>("clip")!.ClipPathUnits);
        Assert.Equal("off", animated.GetElementById<Svg.FilterEffects.SvgComposite>("composite")!.Input);
        Assert.Equal(SvgPreserveAspectRatio.xMinYMin, animated.GetElementById<SvgFragment>("fragment")!.AspectRatio.Align);
        Assert.Equal(SvgGradientSpreadMethod.Pad, animated.GetElementById<SvgLinearGradientServer>("gradient")!.SpreadMethod);

        var use = animated.GetElementById<SvgUse>("use");
        Assert.NotNull(use);
        Assert.True(use!.TryGetEffectiveHrefString(out var href));
        Assert.Equal("#target-b", href);

        var classTarget = animated.GetElementById<SvgRectangle>("class-target");
        Assert.NotNull(classTarget);
        Assert.True(classTarget!.TryGetAttribute("class", out var className));
        Assert.Equal("off", className);
    }

    [Fact]
    public void CreateAnimatedDocument_AnimatesHrefUsingNormalizedAttributeName()
    {
        var document = SvgService.FromSvg(HrefNameNormalizationAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2));

        var target = animated.GetElementById<SvgUse>("target");
        Assert.NotNull(target);
        Assert.True(target!.TryGetEffectiveHrefString(out var href));
        Assert.Equal("#template-b", href);
    }

    [Fact]
    public void CreateAnimatedDocument_AnimatesDirectXLinkHrefAttributeName()
    {
        var document = SvgService.FromSvg(XLinkHrefAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2));

        var target = animated.GetElementById<SvgUse>("target");
        Assert.NotNull(target);
        Assert.True(target!.TryGetEffectiveHrefString(out var href));
        Assert.Equal("#template-b", href);
    }

    [Fact]
    public void CreateAnimatedDocument_PreservesCustomNamespacePrefixAttributeNames()
    {
        var document = SvgService.FromSvg(CustomNamespaceAttributeAnimationSvg);
        Assert.NotNull(document);

        var animation = document!.Descendants().OfType<SvgAnimate>().Single();
        var resolveAttributeName = typeof(SvgAnimationController).GetMethod(
            "ResolveAttributeName",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(resolveAttributeName);
        Assert.Equal("foo:flag", resolveAttributeName!.Invoke(null, new object?[] { animation }));

        using var controller = new SvgAnimationController(document);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2));

        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.True(target!.TryGetAttribute("urn:example:flag", out var flag));
        Assert.Equal("on", flag);
    }

    [Fact]
    public void CreateAnimatedDocument_AnimatesHrefUsingNamespaceAlias()
    {
        var document = SvgService.FromSvg(AliasedXLinkHrefAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2));

        var target = animated.GetElementById<SvgUse>("target");
        Assert.NotNull(target);
        Assert.True(target!.TryGetEffectiveHrefString(out var href));
        Assert.Equal("#template-b", href);
    }

    [Fact]
    public void CreateAnimatedDocument_ReappliesClassSelectorsAfterClassAnimation()
    {
        var document = SvgService.FromSvg(ClassSelectorAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2));

        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.True(target!.TryGetAttribute("class", out var className));
        Assert.Equal("off", className);
        var fill = Assert.IsType<SvgColourServer>(target.Fill);
        Assert.Equal((byte)255, fill.Colour.R);
        Assert.Equal((byte)0, fill.Colour.G);
        Assert.Equal((byte)0, fill.Colour.B);
    }

    [Fact]
    public void CreateAnimatedDocument_UsesToValueAtNonInterpolableMidpoint()
    {
        var document = SvgService.FromSvg(NonInterpolableMidpointClassAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(5));

        var target = animated.GetElementById<SvgCircle>("target");
        Assert.NotNull(target);
        Assert.True(target!.TryGetAttribute("class", out var className));
        Assert.Equal("final midway", className);

        var fill = Assert.IsType<SvgColourServer>(target.Fill);
        Assert.Equal((byte)128, fill.Colour.R);
        Assert.Equal((byte)0, fill.Colour.G);
        Assert.Equal((byte)0, fill.Colour.B);
    }

    [Fact]
    public void CreateAnimatedDocument_AppliesSelectorMutationsBeforeOtherFrameAttributes()
    {
        var document = SvgService.FromSvg(SelectorMutationOrderingAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(3));

        var guide = animated.GetElementById<SvgRectangle>("guide");
        Assert.NotNull(guide);
        var guideFill = Assert.IsType<SvgColourServer>(guide!.Fill);
        Assert.Equal((byte)204, guideFill.Colour.R);
        Assert.Equal((byte)204, guideFill.Colour.G);
        Assert.Equal((byte)204, guideFill.Colour.B);

        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        var targetFill = Assert.IsType<SvgColourServer>(target!.Fill);
        Assert.Equal((byte)255, targetFill.Colour.R);
        Assert.Equal((byte)0, targetFill.Colour.G);
        Assert.Equal((byte)0, targetFill.Colour.B);
    }

    [Fact]
    public void CreateAnimatedDocument_AppliesAnimatedInlineStyle()
    {
        var document = SvgService.FromSvg(StyleAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2));

        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        var fill = Assert.IsType<SvgColourServer>(target!.Fill);
        Assert.Equal((byte)0, fill.Colour.R);
        Assert.Equal((byte)128, fill.Colour.G);
        Assert.Equal((byte)0, fill.Colour.B);
    }

    [Fact]
    public void CreateAnimatedDocument_PreservesClassCustomAttributeDuringAnimation()
    {
        var document = SvgService.FromSvg(ClassPreservationAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));

        var target = animated.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.True(target!.TryGetAttribute("class", out var className));
        Assert.Equal("base highlighted", className);
        Assert.Equal(10f, target.X.Value, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_AnimatesInheritedGradientStopColorAndOpacity()
    {
        var document = SvgService.FromSvg(InheritedStopAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));

        var gradient = animated.GetElementById<SvgLinearGradientServer>("gradient");
        Assert.NotNull(gradient);
        var gradientColor = Assert.IsType<SvgColourServer>(gradient!.StopColor);
        Assert.Equal((byte)128, gradientColor.Colour.R);
        Assert.Equal((byte)0, gradientColor.Colour.G);
        Assert.Equal((byte)128, gradientColor.Colour.B);
        Assert.Equal(0.5f, gradient.StopOpacity, 3);

        var inheritedStop = animated.GetElementById<SvgGradientStop>("inherited-stop");
        Assert.NotNull(inheritedStop);
        var inheritedColor = Assert.IsType<SvgColourServer>(inheritedStop!.StopColor);
        Assert.Equal(gradientColor.Colour, inheritedColor.Colour);
        Assert.Equal(0.5f, inheritedStop.StopOpacity, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_AnimatesInheritedStopOpacityFromParentScope()
    {
        var document = SvgService.FromSvg(W3CParentScopedStopOpacityAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(5));

        var scope = animated.GetElementById<SvgGroup>("scope");
        Assert.NotNull(scope);
        Assert.True(scope!.TryGetAttribute("stop-opacity", out var animatedOpacity));
        Assert.Equal("1", animatedOpacity);
        Assert.True(scope.TryGetAttribute("stop-color", out var stopColor));
        Assert.Equal("yellow", Convert.ToString(stopColor), ignoreCase: true);
        Assert.True(scope.TryGetAttribute("color", out var color));
        Assert.Equal("yellow", Convert.ToString(color), ignoreCase: true);

        var gradient = animated.GetElementById<SvgLinearGradientServer>("gradient");
        Assert.NotNull(gradient);
        Assert.Equal(1f, gradient!.StopOpacity, 3);

        var inheritedStop = gradient.Children.OfType<SvgGradientStop>().Last();
        Assert.Equal(1f, inheritedStop.StopOpacity, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_ResolvesCurrentColorAndInheritColorEndpoints()
    {
        var document = SvgService.FromSvg(ColorKeywordAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2));

        var currentColorTarget = animated.GetElementById<SvgRectangle>("current-color");
        Assert.NotNull(currentColorTarget);
        var currentColorFill = Assert.IsType<SvgColourServer>(currentColorTarget!.Fill);
        Assert.Equal((byte)0, currentColorFill.Colour.R);
        Assert.Equal((byte)128, currentColorFill.Colour.G);
        Assert.Equal((byte)0, currentColorFill.Colour.B);

        var inheritTarget = animated.GetElementById<SvgRectangle>("inherit-color");
        Assert.NotNull(inheritTarget);
        var inheritFill = Assert.IsType<SvgColourServer>(inheritTarget!.Fill);
        Assert.Equal(currentColorFill.Colour, inheritFill.Colour);
    }

    [Fact]
    public void CreateAnimatedDocument_UsesSameFrameAnimatedColorForCurrentColor()
    {
        var document = SvgService.FromSvg(SameFrameCurrentColorAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2));

        AssertAnimatedFill(animated, "color-first", Color.Cyan);
        AssertAnimatedFill(animated, "fill-first", Color.Cyan);
    }

    [Fact]
    public void CreateAnimatedDocument_InterpolatesFeCompositeArithmeticCoefficients()
    {
        var document = SvgService.FromSvg(FeCompositeCoefficientAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));

        var composite = animated.GetElementById<Svg.FilterEffects.SvgComposite>("composite");
        Assert.NotNull(composite);
        Assert.Equal(0.5f, composite!.K2, 3);
        Assert.Equal(0.5f, composite.K3, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_FreezesFeCompositeArithmeticCoefficientsAtEndpoint()
    {
        var document = SvgService.FromSvg(FeCompositeCoefficientAnimationSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2));

        var composite = animated.GetElementById<Svg.FilterEffects.SvgComposite>("composite");
        Assert.NotNull(composite);
        Assert.Equal(0f, composite!.K2, 3);
        Assert.Equal(1f, composite.K3, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_ResolvesAnimateMotionPercentagesAgainstViewportWithoutViewBox()
    {
        var document = SvgService.FromSvg(MotionViewportPercentageSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2));

        var motion = animated.GetElementById<SvgCircle>("motion");
        Assert.NotNull(motion);
        var translate = Assert.IsType<SvgTranslate>(Assert.Single(motion!.Transforms));
        Assert.Equal(100f, translate.X, 3);
        Assert.Equal(50f, translate.Y, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_AppliesAdditiveAndAccumulateSemantics()
    {
        var document = SvgService.FromSvg(AdditiveAndAccumulateSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);

        var additiveFrame = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(1));
        var additiveTarget = additiveFrame.GetElementById<SvgRectangle>("additive");
        Assert.NotNull(additiveTarget);
        Assert.Equal(10f, additiveTarget!.X.Value, 3);

        var accumulatedFrame = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(2.5));

        var numericTarget = accumulatedFrame.GetElementById<SvgRectangle>("numeric");
        Assert.NotNull(numericTarget);
        Assert.Equal(12.5f, numericTarget!.X.Value, 3);

        var transformTarget = accumulatedFrame.GetElementById<SvgRectangle>("transformAccum");
        Assert.NotNull(transformTarget);
        var accumulatedTransform = Assert.IsType<SvgTranslate>(Assert.Single(transformTarget!.Transforms));
        Assert.Equal(12.5f, accumulatedTransform.X, 3);
        Assert.Equal(0f, accumulatedTransform.Y, 3);

        var motionTarget = accumulatedFrame.GetElementById<SvgCircle>("motionAccum");
        Assert.NotNull(motionTarget);
        var accumulatedMotion = Assert.IsType<SvgTranslate>(Assert.Single(motionTarget!.Transforms));
        Assert.Equal(12.5f, accumulatedMotion.X, 3);
        Assert.Equal(0f, accumulatedMotion.Y, 3);
    }

    [Fact]
    public void CreateAnimatedDocument_AccumulatesDiscreteEndValuesPerIteration()
    {
        var document = SvgService.FromSvg(DiscreteAccumulateSvg);
        Assert.NotNull(document);

        using var controller = new SvgAnimationController(document!);
        var animated = controller.CreateAnimatedDocument(TimeSpan.FromSeconds(7));

        var accumulated = animated.GetElementById<SvgRectangle>("accumulated");
        Assert.NotNull(accumulated);
        Assert.Equal(40f, accumulated!.Height.Value, 3);

        var additive = animated.GetElementById<SvgRectangle>("additive");
        Assert.NotNull(additive);
        Assert.Equal(60f, additive!.Height.Value, 3);
    }

    [Fact]
    public void SetAnimationTime_SkipsEquivalentFrameRebuilds()
    {
        using var svg = new SKSvg();
        svg.FromSvg(DelayedAnimationSvg);

        Assert.True(svg.HasAnimations);
        Assert.NotNull(svg.Model);
        Assert.NotNull(svg.Picture);

        var initialModel = svg.Model;
        var initialPicture = svg.Picture;
        var invalidatedCount = 0;
        svg.AnimationInvalidated += (_, _) => invalidatedCount++;

        svg.SetAnimationTime(TimeSpan.FromSeconds(1));

        Assert.Equal(0, invalidatedCount);
        Assert.Same(initialModel, svg.Model);
        Assert.Same(initialPicture, svg.Picture);
        Assert.False(svg.HasPendingAnimationFrame);
        Assert.Equal(0, svg.LastAnimationDirtyTargetCount);
    }

    [Fact]
    public void SetAnimationTime_QueuesPendingFrameWhenThrottled()
    {
        using var svg = new SKSvg();
        svg.FromSvg(HitTestAnimationSvg);
        svg.AnimationMinimumRenderInterval = TimeSpan.FromSeconds(1);

        Assert.True(svg.HasAnimations);
        Assert.NotNull(svg.Model);

        var initialModel = svg.Model;
        var invalidatedCount = 0;
        svg.AnimationInvalidated += (_, args) =>
        {
            invalidatedCount++;
            Assert.Equal(TimeSpan.FromSeconds(0.5), args.Time);
        };

        svg.SetAnimationTime(TimeSpan.FromSeconds(0.5));

        Assert.Equal(0, invalidatedCount);
        Assert.True(svg.HasPendingAnimationFrame);
        Assert.Same(initialModel, svg.Model);
        Assert.True(svg.LastAnimationDirtyTargetCount > 0);

        Assert.True(svg.FlushPendingAnimationFrame());
        Assert.Equal(1, invalidatedCount);
        Assert.False(svg.HasPendingAnimationFrame);
        Assert.NotSame(initialModel, svg.Model);
        Assert.Equal("target", svg.HitTestTopmostElement(new SKPoint(7, 2))?.ID);
    }

    [Fact]
    public void SetAnimationTime_RevertsRemovedAnimatedStateWhenAnimationStops()
    {
        using var svg = new SKSvg();
        svg.FromSvg(TransientAnimationSvg);

        Assert.True(svg.HasAnimations);

        svg.SetAnimationTime(TimeSpan.FromSeconds(0.5));
        Assert.Equal("target", svg.HitTestTopmostElement(new SKPoint(7, 2))?.ID);
        Assert.Null(svg.HitTestTopmostElement(new SKPoint(2, 2)));

        svg.SetAnimationTime(TimeSpan.FromSeconds(2));
        Assert.Equal("target", svg.HitTestTopmostElement(new SKPoint(2, 2))?.ID);
        Assert.Null(svg.HitTestTopmostElement(new SKPoint(7, 2)));
    }

    [Fact]
    public void SetAnimationTime_RemovesAnimatedAttributeWhenBaseAttributeWasImplicit()
    {
        using var svg = new SKSvg();
        svg.FromSvg(TransientImplicitAttributeSvg);

        svg.SetAnimationTime(TimeSpan.FromSeconds(0.5));

        var activeDocument = GetRenderedDocument(svg);
        var activeTarget = activeDocument.GetElementById<SvgRectangle>("target");
        Assert.NotNull(activeTarget);
        Assert.True(activeTarget!.TryGetAttribute("x", out _));

        svg.SetAnimationTime(TimeSpan.FromSeconds(2));

        var renderedDocument = GetRenderedDocument(svg);
        var target = renderedDocument.GetElementById<SvgRectangle>("target");
        Assert.NotNull(target);
        Assert.False(target!.TryGetAttribute("x", out _));
    }

    [Fact]
    public void ResetAnimation_AtZeroClearsEventDrivenRenderedState()
    {
        using var svg = new SKSvg();
        svg.FromSvg(ImmediateEventSetSvg);

        Assert.True(svg.HasAnimations);

        var dispatcher = new SvgInteractionDispatcher();
        var clickInput = new SvgPointerInput(
            new SKPoint(20, 20),
            SvgPointerDeviceType.Mouse,
            SvgMouseButton.Left,
            1,
            0,
            false,
            false,
            false,
            "pointer-1");

        _ = dispatcher.DispatchPointerPressed(svg, clickInput);
        _ = dispatcher.DispatchPointerReleased(svg, clickInput);

        Assert.Null(svg.HitTestTopmostElement(new SKPoint(2, 2)));
        Assert.Equal("target", svg.HitTestTopmostElement(new SKPoint(12, 2))?.ID);

        var invalidatedCount = 0;
        svg.AnimationInvalidated += (_, args) =>
        {
            invalidatedCount++;
            Assert.Equal(TimeSpan.Zero, args.Time);
        };

        svg.ResetAnimation();

        Assert.Equal(1, invalidatedCount);
        Assert.Equal("target", svg.HitTestTopmostElement(new SKPoint(2, 2))?.ID);
        Assert.Null(svg.HitTestTopmostElement(new SKPoint(12, 2)));
    }

    private static SvgDocument GetRenderedDocument(SKSvg svg)
    {
        var sceneDocument = svg.RetainedSceneGraph;
        Assert.NotNull(sceneDocument);
        return Assert.IsType<SvgDocument>(sceneDocument!.SourceDocument);
    }

    private static string GetW3CTestSvgPath(string name)
    {
        return Path.GetFullPath(Path.Combine(
            "..",
            "..",
            "..",
            "..",
            "..",
            "externals",
            "W3C_SVG_11_TestSuite",
            "W3C_SVG_11_TestSuite",
            "svg",
            name));
    }

    private const string AnimationRuntimeSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="60"
             height="40"
             viewBox="0 0 60 40">
          <defs>
            <path id="motionPath" d="M0,30 L10,30" />
          </defs>
          <rect id="move" x="0" y="0" width="5" height="5" fill="red">
            <animate attributeName="x" from="0" to="20" dur="2s" fill="freeze" />
          </rect>
          <rect id="color" x="0" y="10" width="5" height="5" fill="#00ff00">
            <animateColor attributeName="fill" from="#00ff00" to="#0000ff" dur="2s" fill="freeze" />
          </rect>
          <rect id="transform" x="0" y="20" width="5" height="5" fill="blue">
            <animateTransform attributeName="transform" type="translate" from="0 0" to="10 0" dur="2s" fill="freeze" />
          </rect>
          <rect id="set" x="20" y="20" width="5" height="5" visibility="hidden" fill="black">
            <set attributeName="visibility" to="visible" begin="1s" fill="freeze" />
          </rect>
          <circle id="motion" cx="0" cy="0" r="2" fill="purple">
            <animateMotion dur="2s" fill="freeze">
              <mpath xlink:href="#motionPath" />
            </animateMotion>
          </circle>
        </svg>
        """;

    private const string HitTestAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="40"
             height="10"
             viewBox="0 0 40 10">
          <rect id="target" x="0" y="0" width="5" height="5" fill="red">
            <animate attributeName="x" from="0" to="20" dur="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string EventBeginSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="40"
             height="40"
             viewBox="0 0 40 40">
          <rect id="target" x="0" y="0" width="5" height="5" fill="red">
            <animate attributeName="x" from="0" to="10" begin="trigger.click+1s" dur="2s" fill="freeze" />
          </rect>
          <circle id="trigger" cx="20" cy="20" r="4" fill="blue" />
        </svg>
        """;

    private const string DottedIdEventBeginSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="40"
             height="40"
             viewBox="0 0 40 40">
          <rect id="target" x="0" y="0" width="5" height="5" fill="red">
            <animate attributeName="x" from="0" to="10" begin="trigger.dot.mousemove" dur="2s" fill="freeze" />
          </rect>
          <circle id="trigger.dot" cx="20" cy="20" r="4" fill="blue" />
        </svg>
        """;

    private const string NegativeEventOffsetBeginSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="40"
             height="40"
             viewBox="0 0 40 40">
          <rect id="target" x="0" y="0" width="5" height="5" fill="red">
            <animate attributeName="x" from="0" to="10" begin="trigger.click-1s" dur="2s" fill="freeze" />
          </rect>
          <circle id="trigger" cx="20" cy="20" r="4" fill="blue" />
        </svg>
        """;

    private const string MoveTriggeredAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="40"
             height="40"
             viewBox="0 0 40 40">
          <rect id="target" x="0" y="0" width="5" height="5" fill="red">
            <animate attributeName="x" from="0" to="10" begin="trigger.mousemove" dur="1s" fill="freeze" />
          </rect>
          <circle id="trigger" cx="20" cy="20" r="4" fill="blue" />
        </svg>
        """;

    private const string DelayedAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="40"
             height="10"
             viewBox="0 0 40 10">
          <rect id="target" x="0" y="0" width="5" height="5" fill="red">
            <animate attributeName="x" from="0" to="20" begin="2s" dur="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string HexAlphaPaintAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="10"
             height="10"
             viewBox="0 0 10 10">
          <rect id="target" x="0" y="0" width="10" height="10" fill="#00000000">
            <animate attributeName="fill" from="#00000000" to="#00000080" dur="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string TopLevelLayeredAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="40"
             height="20"
             viewBox="0 0 40 20">
          <rect id="static" x="0" y="0" width="10" height="8" fill="navy" />
          <g id="animated-root">
            <rect id="moving" x="0" y="12" width="10" height="6" fill="crimson">
              <animate attributeName="x" from="0" to="10" dur="2s" fill="freeze" />
            </rect>
          </g>
        </svg>
        """;

    private const string SubtreeLayeredAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="80"
             height="40"
             viewBox="0 0 80 40">
          <rect id="static" x="0" y="0" width="6" height="6" fill="black" />
          <g id="animated-root">
            <g id="stable-subtree">
              <rect x="0" y="12" width="10" height="10" fill="red" />
            </g>
            <g id="changing-subtree">
              <rect id="moving" x="20" y="12" width="10" height="10" fill="blue">
                <animate attributeName="x" from="20" to="30" dur="1s" fill="freeze" />
              </rect>
            </g>
          </g>
        </svg>
        """;

    private const string DefsBackedAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="40"
             height="20"
             viewBox="0 0 40 20">
          <defs>
            <rect id="template" x="0" y="0" width="10" height="8" fill="forestgreen">
              <animate attributeName="x" from="0" to="10" dur="2s" fill="freeze" />
            </rect>
          </defs>
          <use id="instance" xlink:href="#template" />
        </svg>
        """;

    private const string PaintServerAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="40"
             height="20"
             viewBox="0 0 40 20">
          <defs>
            <linearGradient id="gradient">
              <stop id="gradient-stop" offset="0%" stop-color="red">
                <animate attributeName="stop-color" from="red" to="blue" dur="2s" fill="freeze" />
              </stop>
              <stop offset="100%" stop-color="white" />
            </linearGradient>
          </defs>
          <rect id="target" x="0" y="0" width="20" height="10" fill="url(#gradient)" />
        </svg>
        """;

    private const string InheritedStrokeAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="40"
             height="40"
             viewBox="0 0 40 40">
          <g id="animated-root"
             stroke="#f1e900"
             fill="#ffffff"
             stroke-width="6">
            <circle cx="20" cy="20" r="12" />
            <animate attributeName="stroke" attributeType="CSS" begin="0s" dur="4s" fill="freeze" from="#f1e900" to="#000000" />
          </g>
        </svg>
        """;

    private const string InheritedFontSizeAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="40"
             height="24"
             viewBox="0 0 40 24">
          <g id="animated-root" fill="#0000ff" font-size="10">
            <rect x="0" y="0" width="1em" height="1em" />
            <animate attributeName="font-size" attributeType="CSS" begin="0s" dur="2s" fill="freeze" from="10" to="20" />
            <animate attributeName="fill" attributeType="CSS" begin="0s" dur="2s" fill="freeze" from="#0000ff" to="#00aa00" />
          </g>
        </svg>
        """;

    private const string InheritedGradientStopOpacityAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="10"
             viewBox="0 0 20 10">
          <rect width="20" height="10" fill="white" />
          <defs>
            <g id="gradient-scope" stop-opacity="0.2">
              <animate attributeName="stop-opacity" begin="0s" dur="2s" fill="freeze" from="0.2" to="1" />
              <linearGradient id="gradient" stop-opacity="inherit">
                <stop offset="0" stop-color="green" stop-opacity="1" />
                <stop offset="1" stop-color="green" stop-opacity="inherit" />
              </linearGradient>
            </g>
          </defs>
          <rect x="0" y="0" width="20" height="10" fill="url(#gradient)" />
        </svg>
        """;

    private const string RootDeferredPaintServerAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="10"
             viewBox="0 0 20 10"
             fill="url(#gradient)">
          <defs>
            <linearGradient id="gradient">
              <stop id="animated-stop" offset="0" stop-color="red">
                <animate attributeName="stop-color" begin="0s" dur="2s" fill="freeze" from="red" to="green" />
              </stop>
              <stop offset="1" stop-color="green" />
            </linearGradient>
          </defs>
          <rect x="0" y="0" width="20" height="10" />
        </svg>
        """;

    private const string ColonClockAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red">
            <animate attributeName="x" begin="00:00:02" dur="00:00:02" from="0" to="10" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string ConcurrentAdditiveAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red">
            <animate attributeName="x" from="0" to="10" dur="2s" fill="freeze" />
            <animate attributeName="x" from="0" to="5" dur="2s" additive="sum" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string MotionAfterTransformAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red">
            <animateTransform attributeName="transform"
                              type="translate"
                              from="0 0"
                              to="0 10"
                              dur="2s"
                              fill="freeze" />
            <animateMotion values="0,0;20,0" dur="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string RepeatDurationIndefiniteAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red">
            <animate attributeName="x" from="0" to="10" dur="1s" repeatDur="indefinite" />
          </rect>
        </svg>
        """;

    private const string FiniteRepeatCountWithIndefiniteRepeatDurationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red">
            <animate attributeName="x" from="0" to="10" dur="1s" repeatCount="2" repeatDur="indefinite" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string ZeroDurationAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red">
            <animate attributeName="x" from="0" to="10" dur="0s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string NonFiniteClockAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red">
            <animate attributeName="x" from="0" to="10" dur="NaNs" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string NonFiniteRepeatCountAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red">
            <animate attributeName="x" from="0" to="10" dur="1s" repeatCount="NaN" />
          </rect>
        </svg>
        """;

    private const string MaximumDurationAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red">
            <animate attributeName="x" from="0" to="10" dur="5s" max="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string MalformedTransformValuesSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red">
            <animateTransform attributeName="transform" type="translate" values="0 0;bad" dur="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string MinimumDurationAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red">
            <animate attributeName="x" from="0" to="10" dur="1s" min="3s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string IndefiniteSetMaxDurationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="30"
             height="20"
             viewBox="0 0 30 20">
          <rect id="capped" x="0" y="0" width="4" height="4" fill="red">
            <set attributeName="x" to="10" dur="indefinite" max="2s" />
          </rect>
          <rect id="invalid-pair" x="0" y="8" width="4" height="4" fill="green">
            <set attributeName="x" to="10" end="4s" min="5s" max="2s" />
          </rect>
        </svg>
        """;

    private const string EventEndSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="40"
             height="10"
             viewBox="0 0 40 10">
          <rect id="target" x="0" y="0" width="5" height="5" fill="red">
            <animate attributeName="x" from="0" to="10" begin="0s" dur="10s" end="trigger.click" fill="freeze" />
          </rect>
          <circle id="trigger" cx="20" cy="5" r="3" fill="blue" />
        </svg>
        """;

    private const string MotionValuesSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="30"
             height="30"
             viewBox="0 0 30 30">
          <circle id="motion" cx="0" cy="0" r="2" fill="purple">
            <animateMotion values="0,0;10,0;10,10" calcMode="linear" keyTimes="0;0.5;1" dur="2s" fill="freeze" />
          </circle>
        </svg>
        """;

    private const string WhitespaceTransformValuesSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="30"
             height="30"
             viewBox="0 0 30 30">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red">
            <animateTransform attributeName="transform"
                              type="translate"
                              values="  0 0 ;   10 0 ;  10 10  "
                              keyTimes=" 0 ; 0.5 ; 1 "
                              dur=" 2s "
                              fill="freeze" />
          </rect>
        </svg>
        """;

    private const string WhitespaceMotionValuesSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="30"
             height="30"
             viewBox="0 0 30 30">
          <circle id="motion" cx="0" cy="0" r="2" fill="purple">
            <animateMotion values=" 0,0 ; 10 0 ; 10,
                                    10 "
                           calcMode="linear"
                           keyTimes=" 0 ; 0.5 ; 1 "
                           dur=" 2s "
                           rotate=" auto-reverse "
                           fill="freeze" />
          </circle>
        </svg>
        """;

    private const string RootViewBoxAnimationSvg = """
        <svg id="svg-root"
             xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="100%"
             height="100%"
             viewBox="0 0 100 100"
             preserveAspectRatio="none">
          <rect id="target" x="60" y="60" width="10" height="10" fill="red" />
          <animate xlink:href="#svg-root"
                   attributeName="viewBox"
                   from="0 0 100 100"
                   to="50 50 50 50"
                   dur="2s"
                   fill="freeze" />
        </svg>
        """;

    private const string RootViewBoxSizeAnimationSvg = """
        <svg id="svg-root"
             xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="100%"
             height="100%"
             viewBox="0 0 100 100"
             preserveAspectRatio="none">
          <rect id="target" x="10" y="10" width="10" height="10" fill="red" />
          <animate xlink:href="#svg-root"
                   attributeName="viewBox"
                   from="0 0 100 100"
                   to="0 0 50 50"
                   dur="2s"
                   fill="freeze" />
        </svg>
        """;

    private const string SplineAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="30"
             height="30"
             viewBox="0 0 30 30">
          <rect id="linear" x="0" y="0" width="4" height="4" fill="red">
            <animate attributeName="x" values="0;10" calcMode="linear" dur="2s" fill="freeze" />
          </rect>
          <rect id="spline" x="0" y="8" width="4" height="4" fill="blue">
            <animate attributeName="x" values="0;10" calcMode="spline" keyTimes="0;1" keySplines="0.25 1 0.75 1" dur="2s" fill="freeze" />
          </rect>
          <circle id="motion-linear" cx="0" cy="20" r="2" fill="purple">
            <animateMotion values="0,0;10,0" calcMode="linear" dur="2s" fill="freeze" />
          </circle>
          <circle id="motion-spline" cx="0" cy="26" r="2" fill="green">
            <animateMotion values="0,0;10,0" calcMode="spline" keyTimes="0;1" keySplines="0.25 1 0.75 1" dur="2s" fill="freeze" />
          </circle>
        </svg>
        """;

    private const string MotionViewportPercentageSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="200"
             height="100">
          <circle id="motion" cx="0" cy="0" r="2" fill="purple">
            <animateMotion values="0%,0%;50%,50%" dur="2s" fill="freeze" />
          </circle>
        </svg>
        """;

    private const string TransientAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="40"
             height="10"
             viewBox="0 0 40 10">
          <rect id="target" x="0" y="0" width="5" height="5" fill="red">
            <animate attributeName="x" from="0" to="10" dur="1s" />
          </rect>
        </svg>
        """;

    private const string TransientImplicitAttributeSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="40"
             height="10"
             viewBox="0 0 40 10">
          <rect id="target" y="0" width="5" height="5" fill="red">
            <animate attributeName="x" from="0" to="10" dur="1s" />
          </rect>
        </svg>
        """;

    private const string ImmediateEventSetSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="40"
             height="40"
             viewBox="0 0 40 40">
          <rect id="target" y="0" width="5" height="5" fill="red">
            <set attributeName="x" to="10" begin="trigger.click" dur="1s" fill="freeze" />
          </rect>
          <circle id="trigger" cx="20" cy="20" r="4" fill="blue" />
        </svg>
        """;

    private const string AdditiveAndAccumulateSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="50"
             height="30"
             viewBox="0 0 50 30">
          <rect id="additive" x="5" y="0" width="5" height="5" fill="red">
            <animate attributeName="x" from="0" to="10" dur="2s" additive="sum" fill="freeze" />
          </rect>
          <rect id="numeric" x="0" y="10" width="5" height="5" fill="green">
            <animate attributeName="x" from="0" to="10" dur="2s" repeatCount="3" accumulate="sum" fill="freeze" />
          </rect>
          <rect id="transformAccum" x="0" y="20" width="5" height="5" fill="blue">
            <animateTransform attributeName="transform" type="translate" from="0 0" to="10 0" dur="2s" repeatCount="3" accumulate="sum" fill="freeze" />
          </rect>
          <circle id="motionAccum" cx="0" cy="0" r="2" fill="purple">
            <animateMotion dur="2s" repeatCount="3" accumulate="sum" fill="freeze" path="M0,0 L10,0" />
          </circle>
        </svg>
        """;

    private const string DiscreteAccumulateSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="50"
             height="80"
             viewBox="0 0 50 80">
          <rect id="accumulated" x="0" y="0" width="10" height="20" fill="red">
            <animate attributeName="height"
                     calcMode="discrete"
                     from="200"
                     to="20"
                     dur="4s"
                     repeatCount="2"
                     accumulate="sum"
                     fill="freeze" />
          </rect>
          <rect id="additive" x="20" y="0" width="10" height="20" fill="green">
            <animate attributeName="height"
                     calcMode="discrete"
                     additive="sum"
                     from="200"
                     to="20"
                     dur="4s"
                     repeatCount="2"
                     accumulate="sum"
                     fill="freeze" />
          </rect>
        </svg>
        """;

    private const string PacedValuesAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="120"
             height="20"
             viewBox="0 0 120 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red">
            <animate attributeName="x" values="0;10;110" calcMode="paced" dur="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private static List<(string EventType, string AttributeName)> InvokeTimelineCallbacks(
        SvgAnimationController controller,
        TimeSpan currentTime,
        TimeSpan? previousTime)
    {
        var method = typeof(SvgAnimationController).GetMethod(
            "GetTimelineCallbacks",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = (System.Collections.IEnumerable)method!.Invoke(controller, new object?[] { currentTime, previousTime })!;
        var callbacks = new List<(string EventType, string AttributeName)>();
        foreach (var callback in result)
        {
            callbacks.Add((
                Assert.IsType<string>(callback.GetType().GetProperty("EventType")!.GetValue(callback)),
                Assert.IsType<string>(callback.GetType().GetProperty("AttributeName")!.GetValue(callback))));
        }

        return callbacks;
    }

    private const string PacedTransformAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="120"
             height="20"
             viewBox="0 0 120 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red">
            <animateTransform attributeName="transform" type="translate" values="0 0;10 0;110 0" calcMode="paced" dur="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string ByOnlyTransformWithBaseSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red" transform="translate(10 0)">
            <animateTransform attributeName="transform" type="translate" by="5 0" dur="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string LargeRepeatIterationAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red">
            <animate attributeName="x" from="0" to="1" dur="1ms" repeatCount="indefinite" accumulate="sum" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string SyncbaseRepeatTimingSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="base" x="0" y="0" width="4" height="4" fill="green">
            <animate id="repeater" attributeName="width" from="4" to="8" dur="1s" repeatCount="3" />
          </rect>
          <rect id="target" x="0" y="10" width="4" height="4" fill="blue">
            <animate attributeName="x" from="0" to="10" begin="repeater.repeat(1)" dur="1s" />
          </rect>
        </svg>
        """;

    private const string HalfOpenIntervalAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="removed" x="0" y="0" width="4" height="4" fill="green">
            <animate attributeName="x" from="0" to="10" dur="1s" />
          </rect>
          <rect id="frozen" x="0" y="10" width="4" height="4" fill="blue">
            <animate attributeName="x" from="0" to="10" dur="1s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string RestartTruncationSyncbaseSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="green">
            <animate id="driver" attributeName="x" from="0" to="10" begin="0s; 0.5s" dur="1s" restart="always" />
            <animate attributeName="y" from="0" to="10" begin="driver.end" dur="1s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string SelfBeginEndTimingSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="green">
            <animate id="bounded" attributeName="x" from="0" to="10" begin="0s" dur="10s" end="bounded.begin + 2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string FutureSyncbaseStartTimeSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="driver-target" x="0" y="0" width="4" height="4" fill="green">
            <animate id="driver" attributeName="x" from="0" to="10" begin="0s" dur="1s" />
          </rect>
          <rect id="target" x="0" y="10" width="4" height="4" fill="blue">
            <animate id="dependent" attributeName="x" from="0" to="10" begin="driver.end + 1s" dur="1s" />
          </rect>
        </svg>
        """;

    private const string RepeatTimelineCallbackSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="green">
            <animate id="repeater" attributeName="x" from="0" to="10" dur="1s" repeatCount="3" onrepeat="window.__repeat = true;" />
          </rect>
        </svg>
        """;

    private const string RepeatZeroTimingSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="base" x="0" y="0" width="4" height="4" fill="green">
            <animate id="repeater" attributeName="width" from="4" to="8" dur="1s" repeatCount="3" />
          </rect>
          <rect id="target" x="0" y="10" width="4" height="4" fill="blue">
            <animate attributeName="x" from="0" to="10" begin="repeater.repeat(0)" dur="1s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string SelfEndBeginTimingSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="pulse" x="0" y="0" width="4" height="4" fill="green">
            <set id="pulse-set" attributeName="x" to="10" begin="0s; pulse-set.end + 1s" dur="1s" />
          </rect>
          <rect id="driver" x="0" y="10" width="4" height="4" fill="blue">
            <set id="driver-set" attributeName="x" to="10" begin="0s; driver-set.end + 1s" dur="1s" />
          </rect>
          <rect id="follower" x="0" y="15" width="4" height="4" fill="purple">
            <set attributeName="x" to="10" begin="driver-set.end + 1s" dur="1s" />
          </rect>
        </svg>
        """;

    private const string NonProgressingSelfBeginTimingSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="green">
            <animate id="pulse" attributeName="x" from="0" to="10" begin="0s; pulse.begin" dur="1s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string NumberListAndPathDataAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="30"
             height="30"
             viewBox="0 0 30 30">
          <polygon id="polygon" points="0,0 20,0 0,20" fill="green">
            <animate attributeName="points" values="0,0 20,0 0,20; 0,0 0,20 20,0" dur="2s" fill="freeze" />
          </polygon>
          <path id="path" d="M0 0 L20 0 L0 20 Z" fill="none" stroke="blue">
            <animate attributeName="d" values="M0 0 L20 0 L0 20 Z; M0 0 L0 20 L20 0 Z" dur="2s" fill="freeze" />
          </path>
        </svg>
        """;

    private const string NonInterpolableLinearAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red" visibility="hidden">
            <animate attributeName="visibility" values="hidden;visible" calcMode="linear" dur="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string ToOnlyNonInterpolableAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <defs>
            <style>.on { fill: red; } .off { fill: #ccc; }</style>
            <clipPath id="clip" clipPathUnits="objectBoundingBox">
              <rect width="1" height="1" />
              <animate attributeName="clipPathUnits" to="userSpaceOnUse" begin="2s" dur="2s" fill="freeze" />
            </clipPath>
            <filter id="filter">
              <feFlood result="off" flood-color="#ccc" />
              <feFlood result="on" flood-color="red" />
              <feComposite id="composite" in="on" in2="SourceGraphic">
                <animate attributeName="in" to="off" begin="2s" dur="2s" fill="freeze" />
              </feComposite>
            </filter>
            <linearGradient id="gradient" spreadMethod="reflect">
              <animate attributeName="spreadMethod" to="pad" begin="2s" dur="2s" fill="freeze" />
            </linearGradient>
            <rect id="target-a" width="4" height="4" fill="red" />
            <rect id="target-b" width="4" height="4" fill="blue" />
          </defs>
          <svg id="fragment" width="10" height="10" viewBox="0 0 20 20" preserveAspectRatio="none">
            <animate attributeName="preserveAspectRatio" to="xMinYMin" begin="2s" dur="2s" fill="freeze" />
          </svg>
          <use id="use" xlink:href="#target-a">
            <animate attributeName="xlink:href" to="#target-b" begin="2s" dur="2s" fill="freeze" />
          </use>
          <rect id="class-target" class="on" width="4" height="4">
            <animate attributeName="class" to="off" begin="2s" dur="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string HrefNameNormalizationAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <defs>
            <rect id="template-a" width="4" height="4" fill="red" />
            <rect id="template-b" width="4" height="4" fill="blue" />
          </defs>
          <use id="target" xlink:href="#template-a">
            <animate attributeName="href" from="#template-a" to="#template-b" dur="2s" fill="freeze" />
          </use>
        </svg>
        """;

    private const string XLinkHrefAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <defs>
            <rect id="template-a" width="4" height="4" fill="red" />
            <rect id="template-b" width="4" height="4" fill="blue" />
          </defs>
          <use id="target" xlink:href="#template-a">
            <animate attributeName="xlink:href" from="#template-a" to="#template-b" dur="2s" fill="freeze" />
          </use>
        </svg>
        """;

    private const string CustomNamespaceAttributeAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:foo="urn:example"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" foo:flag="off">
            <animate attributeName="foo:flag" from="off" to="on" dur="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string AliasedXLinkHrefAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xl="http://www.w3.org/1999/xlink"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <defs>
            <rect id="template-a" width="4" height="4" fill="red" />
            <rect id="template-b" width="4" height="4" fill="blue" />
          </defs>
          <use id="target" xl:href="#template-a">
            <animate attributeName="xl:href" from="#template-a" to="#template-b" dur="2s" fill="freeze" />
          </use>
        </svg>
        """;

    private const string ClassSelectorAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <style type="text/css">
            .on { fill: #008000; }
            .off { fill: #ff0000; }
          </style>
          <rect id="target" class="on" x="0" y="0" width="4" height="4">
            <animate attributeName="class" from="on" to="off" dur="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string NonInterpolableMidpointClassAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <style type="text/css">
            .start { visibility: hidden; }
            .midway { visibility: visible; fill: #0000ff; }
            #body .final { fill: rgb(128,0,0); }
          </style>
          <g id="body">
            <circle id="target" cx="10" cy="10" r="5" class="start">
              <set attributeName="class" to="midway" begin="2s" dur="2s" fill="freeze" />
              <animate attributeName="class" from="midway" to="final midway" begin="3s" dur="4s" fill="freeze" />
            </circle>
          </g>
        </svg>
        """;

    private const string SelectorMutationOrderingAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <style type="text/css">
            .on { fill: #0000ff; }
            .off { fill: #ff0000; }
          </style>
          <rect id="guide" x="0" y="0" width="4" height="4" fill="rgb(204,0,102)">
            <set attributeName="fill" to="#cccccc" begin="2s" dur="2s" fill="freeze" />
          </rect>
          <rect id="target" class="on" x="10" y="0" width="4" height="4">
            <animate attributeName="class" to="off" begin="2s" dur="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string StyleAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" x="0" y="0" width="4" height="4" fill="red">
            <set attributeName="style" to="fill: #008000" begin="0s" dur="1s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string ClassPreservationAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="target" class="base highlighted" x="0" y="0" width="4" height="4" fill="red">
            <animate attributeName="x" from="0" to="20" dur="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string ColorKeywordAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="current-color" color="#008000" fill="#000000" x="0" y="0" width="4" height="4">
            <animateColor attributeName="fill" from="#000000" to="currentColor" dur="2s" fill="freeze" />
          </rect>
          <g fill="#008000">
            <rect id="inherit-color" fill="#000000" x="10" y="0" width="4" height="4">
              <animateColor attributeName="fill" from="#000000" to="inherit" dur="2s" fill="freeze" />
            </rect>
          </g>
        </svg>
        """;

    private const string SameFrameCurrentColorAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <rect id="color-first" color="#008000" fill="#000000" x="0" y="0" width="4" height="4">
            <animate attributeName="color" from="#008000" to="#00ffff" dur="2s" fill="freeze" />
            <animateColor attributeName="fill" from="#000000" to="currentColor" dur="2s" fill="freeze" />
          </rect>
          <rect id="fill-first" color="#008000" fill="#000000" x="10" y="0" width="4" height="4">
            <animateColor attributeName="fill" from="#000000" to="currentColor" dur="2s" fill="freeze" />
            <animate attributeName="color" from="#008000" to="#00ffff" dur="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string InheritedStopAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <defs>
            <linearGradient id="gradient" stop-color="red" stop-opacity="0.2">
              <stop id="inherited-stop" offset="0" stop-color="inherit" stop-opacity="inherit" />
              <stop offset="1" stop-color="white" stop-opacity="1" />
              <animate attributeName="stop-color" from="red" to="blue" dur="2s" fill="freeze" />
              <animate attributeName="stop-opacity" from="0.2" to="0.8" dur="2s" fill="freeze" />
            </linearGradient>
          </defs>
          <rect id="target" x="0" y="0" width="20" height="20" fill="url(#gradient)" />
        </svg>
        """;

    private const string W3CParentScopedStopOpacityAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <defs>
            <g id="scope" stop-color="yellow" stop-opacity="0" color="yellow">
              <animateColor attributeName="stop-color" from="red" to="green" dur="5s" fill="freeze" />
              <animateColor attributeName="color" from="yellow" to="green" dur="5s" fill="freeze" />
              <animate attributeName="stop-opacity" from="0.5" to="1" dur="5s" fill="freeze" />
              <linearGradient id="gradient" stop-opacity="inherit">
                <stop offset="0" stop-color="green" stop-opacity="1" />
                <stop offset="1" stop-color="green" stop-opacity="inherit" />
              </linearGradient>
            </g>
          </defs>
          <rect id="target" x="0" y="0" width="20" height="20" fill="url(#gradient)" />
        </svg>
        """;

    private const string FeCompositeCoefficientAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <defs>
            <filter id="filter">
              <feComposite id="composite" operator="arithmetic" in="SourceGraphic" in2="BackgroundImage" k1="0" k2="1" k3="0" k4="0">
                <animate attributeName="k2" values="1;0" dur="2s" fill="freeze" />
                <animate attributeName="k3" values="0;1" dur="2s" fill="freeze" />
              </feComposite>
            </filter>
          </defs>
          <rect id="target" x="0" y="0" width="20" height="20" fill="red" filter="url(#filter)" />
        </svg>
        """;

    private static System.Collections.Generic.List<string> GetAnimatedSubtreeSignatures(SKSvg svg)
    {
        var compositePicture = svg.Model;
        Assert.NotNull(compositePicture);

        var layerPictures = compositePicture!.Commands!.OfType<DrawPictureCanvasCommand>().ToList();
        Assert.True(layerPictures.Count >= 2);

        var dynamicLayerPicture = layerPictures[layerPictures.Count - 1].Picture;
        Assert.NotNull(dynamicLayerPicture);
        return CollectLeafPictureSignatures(dynamicLayerPicture!);
    }

    private static System.Collections.Generic.List<string> CollectLeafPictureSignatures(ShimSkiaSharp.SKPicture picture)
    {
        var nestedPictures = picture.Commands!
            .OfType<DrawPictureCanvasCommand>()
            .Select(static command => command.Picture)
            .Where(static nestedPicture => nestedPicture is not null)
            .Cast<ShimSkiaSharp.SKPicture>()
            .ToList();

        if (nestedPictures.Count > 0)
        {
            var leafSignatures = new System.Collections.Generic.List<string>();
            for (var i = 0; i < nestedPictures.Count; i++)
            {
                leafSignatures.AddRange(CollectLeafPictureSignatures(nestedPictures[i]));
            }

            return leafSignatures;
        }

        var commandRangeSignatures = CollectTopLevelCommandRangeSignatures(picture);
        if (commandRangeSignatures.Count > 0)
        {
            return commandRangeSignatures;
        }

        var leafCommandSignatures = CollectLeafCommandSignatures(picture);
        if (leafCommandSignatures.Count > 0)
        {
            return leafCommandSignatures;
        }

        return new System.Collections.Generic.List<string> { GetPictureSignature(picture) };
    }

    private static System.Collections.Generic.List<string> CollectLeafCommandSignatures(ShimSkiaSharp.SKPicture picture)
    {
        if (picture.Commands is not { Count: > 0 } commands)
        {
            return new System.Collections.Generic.List<string>();
        }

        var signatures = new System.Collections.Generic.List<string>();
        for (var i = 0; i < commands.Count; i++)
        {
            if (IsLeafDrawCommand(commands[i]))
            {
                signatures.Add(GetCommandRangeSignature(picture, commands, i, i));
            }
        }

        return signatures;
    }

    private static bool IsLeafDrawCommand(CanvasCommand command)
    {
        return command is DrawPathCanvasCommand or
            DrawTextCanvasCommand or
            DrawTextBlobCanvasCommand or
            DrawTextOnPathCanvasCommand or
            DrawImageCanvasCommand;
    }

    private static System.Collections.Generic.List<string> CollectTopLevelCommandRangeSignatures(ShimSkiaSharp.SKPicture picture)
    {
        if (picture.Commands is not { Count: > 0 } commands)
        {
            return new System.Collections.Generic.List<string>();
        }

        var signatures = new System.Collections.Generic.List<string>();
        var depth = 0;
        var segmentStart = -1;

        for (var i = 0; i < commands.Count; i++)
        {
            switch (commands[i])
            {
                case SaveCanvasCommand:
                case SaveLayerCanvasCommand:
                    if (depth == 1)
                    {
                        segmentStart = i;
                    }

                    depth++;
                    break;
                case RestoreCanvasCommand:
                    depth--;
                    if (depth == 1 && segmentStart >= 0)
                    {
                        signatures.Add(GetCommandRangeSignature(picture, commands, segmentStart, i));
                        segmentStart = -1;
                    }
                    break;
            }
        }

        return signatures;
    }

    private static string GetCommandRangeSignature(ShimSkiaSharp.SKPicture picture, IList<CanvasCommand> commands, int start, int end)
    {
        var builder = new StringBuilder();

        for (var i = start; i <= end; i++)
        {
            AppendCommandSignature(builder, commands[i]);
        }

        return builder.ToString();
    }

    private static string GetPictureSignature(ShimSkiaSharp.SKPicture picture)
    {
        var builder = new StringBuilder();
        AppendPictureSignature(builder, picture);
        return builder.ToString();
    }

    private static void AppendPictureSignature(StringBuilder builder, ShimSkiaSharp.SKPicture picture)
    {
        builder
            .Append('[')
            .Append(picture.CullRect.Left).Append(',')
            .Append(picture.CullRect.Top).Append(',')
            .Append(picture.CullRect.Right).Append(',')
            .Append(picture.CullRect.Bottom)
            .Append(']');

        if (picture.Commands is not { Count: > 0 } commands)
        {
            return;
        }

        for (var i = 0; i < commands.Count; i++)
        {
            AppendCommandSignature(builder, commands[i]);
        }
    }

    private static void AppendCommandSignature(StringBuilder builder, CanvasCommand command)
    {
        builder.Append('|').Append(command.GetType().Name);
        switch (command)
        {
            case DrawPictureCanvasCommand drawPicture when drawPicture.Picture is { } nestedPicture:
                AppendPictureSignature(builder, nestedPicture);
                break;
            case DrawPathCanvasCommand drawPath:
                AppendRectSignature(builder, drawPath.Path?.Bounds ?? SKRect.Empty);
                AppendPaintSignature(builder, drawPath.Paint);
                break;
            case DrawTextCanvasCommand drawText:
                builder
                    .Append('(')
                    .Append(drawText.Text)
                    .Append('@')
                    .Append(drawText.X)
                    .Append(',')
                    .Append(drawText.Y)
                    .Append(')');
                AppendPaintSignature(builder, drawText.Paint);
                break;
            case DrawTextBlobCanvasCommand drawTextBlob:
                builder
                    .Append('(')
                    .Append(drawTextBlob.X)
                    .Append(',')
                    .Append(drawTextBlob.Y)
                    .Append(')');
                AppendPaintSignature(builder, drawTextBlob.Paint);
                break;
            case DrawImageCanvasCommand drawImage:
                AppendRectSignature(builder, drawImage.Source);
                AppendRectSignature(builder, drawImage.Dest);
                AppendPaintSignature(builder, drawImage.Paint);
                break;
            case ClipRectCanvasCommand clipRect:
                AppendRectSignature(builder, clipRect.Rect);
                break;
            case SetMatrixCanvasCommand setMatrix:
                builder
                    .Append('(')
                    .Append(setMatrix.DeltaMatrix.ScaleX).Append(',')
                    .Append(setMatrix.DeltaMatrix.SkewX).Append(',')
                    .Append(setMatrix.DeltaMatrix.TransX).Append(',')
                    .Append(setMatrix.DeltaMatrix.SkewY).Append(',')
                    .Append(setMatrix.DeltaMatrix.ScaleY).Append(',')
                    .Append(setMatrix.DeltaMatrix.TransY)
                    .Append(')');
                break;
        }
    }

    private static void AppendRectSignature(StringBuilder builder, SKRect rect)
    {
        builder
            .Append('[')
            .Append(rect.Left).Append(',')
            .Append(rect.Top).Append(',')
            .Append(rect.Right).Append(',')
            .Append(rect.Bottom)
            .Append(']');
    }

    private static void AppendPaintSignature(StringBuilder builder, SKPaint? paint)
    {
        if (paint is null)
        {
            builder.Append("(null)");
            return;
        }

        builder
            .Append('(')
            .Append(paint.Style).Append(',')
            .Append(paint.Color?.Red ?? 0).Append(',')
            .Append(paint.Color?.Green ?? 0).Append(',')
            .Append(paint.Color?.Blue ?? 0).Append(',')
            .Append(paint.Color?.Alpha ?? 0).Append(',')
            .Append(paint.StrokeWidth)
            .Append(')');
    }

    private static SkiaBitmap RenderBitmap(SKSvg svg)
    {
        Assert.NotNull(svg.Picture);
        var bitmap = svg.Picture!.ToBitmap(
            SkiaColors.Transparent,
            1f,
            1f,
            SkiaColorType.Rgba8888,
            SkiaAlphaType.Unpremul,
            svg.Settings.Srgb);

        return Assert.IsType<SkiaBitmap>(bitmap);
    }

    private static void AssertAnimatedFill(SvgDocument document, string elementId, Color expectedColor)
    {
        var target = document.GetElementById<SvgRectangle>(elementId);
        Assert.NotNull(target);
        var fill = Assert.IsType<SvgColourServer>(target!.Fill);
        Assert.Equal(expectedColor.A, fill.Colour.A);
        Assert.Equal(expectedColor.R, fill.Colour.R);
        Assert.Equal(expectedColor.G, fill.Colour.G);
        Assert.Equal(expectedColor.B, fill.Colour.B);
    }

    private static SkiaBitmap DrawBitmap(SKSvg svg)
    {
        Assert.NotNull(svg.Picture);
        var width = Math.Max(1, (int)Math.Ceiling(svg.Picture!.CullRect.Width));
        var height = Math.Max(1, (int)Math.Ceiling(svg.Picture!.CullRect.Height));
        var bitmap = new SkiaBitmap(new SkiaSharp.SKImageInfo(width, height, SkiaColorType.Rgba8888, SkiaAlphaType.Unpremul, svg.Settings.Srgb));
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SkiaColors.Transparent);
        svg.Draw(canvas);
        return bitmap;
    }

    private static string GetBitmapSignature(SkiaBitmap bitmap)
    {
        var builder = new StringBuilder();
        builder.Append(bitmap.Width).Append('x').Append(bitmap.Height);

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                builder
                    .Append('|')
                    .Append(pixel.Alpha)
                    .Append(',')
                    .Append(pixel.Red)
                    .Append(',')
                    .Append(pixel.Green)
                    .Append(',')
                    .Append(pixel.Blue);
            }
        }

        return builder.ToString();
    }
}
