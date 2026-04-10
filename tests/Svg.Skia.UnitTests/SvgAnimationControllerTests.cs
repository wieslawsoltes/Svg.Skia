using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShimSkiaSharp;
using Svg;
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

        var initialPictures = GetAnimatedSubtreePictures(svg);

        svg.SetAnimationTime(TimeSpan.FromSeconds(1));

        var updatedPictures = GetAnimatedSubtreePictures(svg);
        Assert.Equal(2, initialPictures.Count);
        Assert.Equal(2, updatedPictures.Count);
        Assert.Equal(GetPictureSignature(initialPictures[0]), GetPictureSignature(updatedPictures[0]));
        Assert.NotEqual(GetPictureSignature(initialPictures[1]), GetPictureSignature(updatedPictures[1]));
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
        Assert.Equal("target", svg.HitTestTopmostElement(new SKPoint(25, 25))?.ID);
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

    private static System.Collections.Generic.List<ShimSkiaSharp.SKPicture> GetAnimatedSubtreePictures(SKSvg svg)
    {
        var compositePicture = svg.Model;
        Assert.NotNull(compositePicture);

        var layerPictures = compositePicture!.Commands!.OfType<DrawPictureCanvasCommand>().ToList();
        Assert.True(layerPictures.Count >= 2);

        var dynamicLayerPicture = layerPictures[layerPictures.Count - 1].Picture;
        Assert.NotNull(dynamicLayerPicture);
        return CollectLeafPictures(dynamicLayerPicture!);
    }

    private static System.Collections.Generic.List<ShimSkiaSharp.SKPicture> CollectLeafPictures(ShimSkiaSharp.SKPicture picture)
    {
        var nestedPictures = picture.Commands!
            .OfType<DrawPictureCanvasCommand>()
            .Select(static command => command.Picture)
            .Where(static nestedPicture => nestedPicture is not null)
            .Cast<ShimSkiaSharp.SKPicture>()
            .ToList();

        if (nestedPictures.Count == 0)
        {
            return new System.Collections.Generic.List<ShimSkiaSharp.SKPicture> { picture };
        }

        var leafPictures = new System.Collections.Generic.List<SKPicture>();
        for (var i = 0; i < nestedPictures.Count; i++)
        {
            leafPictures.AddRange(CollectLeafPictures(nestedPictures[i]));
        }

        return leafPictures;
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
            var command = commands[i];
            builder.Append('|').Append(command.GetType().Name);
            if (command is DrawPictureCanvasCommand drawPicture && drawPicture.Picture is { } nestedPicture)
            {
                AppendPictureSignature(builder, nestedPicture);
            }
        }
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
