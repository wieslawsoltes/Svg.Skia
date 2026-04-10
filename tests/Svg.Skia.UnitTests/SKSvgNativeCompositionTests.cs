using System;
using System.Reflection;
using Svg;
using Svg.Transforms;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SKSvgNativeCompositionTests
{
    [Fact]
    public void TryCreateNativeCompositionScene_PreservesTopLevelOrderAndExtractsVisualState()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NativeCompositionSceneSvg);

        Assert.True(svg.HasAnimations);
        Assert.True(svg.SupportsNativeComposition);
        Assert.True(svg.TryCreateNativeCompositionScene(out var scene));
        Assert.NotNull(scene);

        Assert.Collection(
            scene!.Layers,
            staticLayer =>
            {
                Assert.Equal(0, staticLayer.DocumentChildIndex);
                Assert.False(staticLayer.IsAnimated);
                Assert.True(staticLayer.IsVisible);
                Assert.NotNull(staticLayer.Picture);
            },
            animatedLayer =>
            {
                Assert.Equal(1, animatedLayer.DocumentChildIndex);
                Assert.True(animatedLayer.IsAnimated);
                Assert.True(animatedLayer.IsVisible);
                Assert.NotNull(animatedLayer.Picture);
                Assert.Equal(4f, animatedLayer.Offset.X, 3);
                Assert.Equal(6f, animatedLayer.Offset.Y, 3);
                Assert.Equal(10f, animatedLayer.Size.Width, 3);
                Assert.Equal(20f, animatedLayer.Size.Height, 3);
                Assert.Equal(0.5f, animatedLayer.Opacity, 3);
            });
    }

    [Fact]
    public void TryCreateNativeCompositionFrame_ReturnsAnimatedLayersOnly()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NativeCompositionFrameSvg);

        Assert.True(svg.HasAnimations);
        svg.SetAnimationTime(TimeSpan.FromSeconds(1));

        Assert.True(svg.TryCreateNativeCompositionFrame(out var frame));
        Assert.NotNull(frame);
        var animatedLayer = Assert.Single(frame!.Layers);
        Assert.Equal(0, animatedLayer.DocumentChildIndex);
        Assert.True(animatedLayer.IsAnimated);
        Assert.True(animatedLayer.IsVisible);
        Assert.NotNull(animatedLayer.Picture);
        Assert.Equal(12f, animatedLayer.Offset.X, 3);
        Assert.Equal(8f, animatedLayer.Offset.Y, 3);
        Assert.Equal(10f, animatedLayer.Size.Width, 3);
        Assert.Equal(10f, animatedLayer.Size.Height, 3);
    }

    [Fact]
    public void TryCreateNativeCompositionFrame_ReusesCachedSourceSceneAcrossFrames()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NativeCompositionSceneSvg);

        Assert.True(svg.TryCreateNativeCompositionScene(out _));

        var sourceScene = GetPrivateField<SvgSceneDocument>(svg, "_nativeCompositionSourceScene");
        var animatedChildIndexes = GetPrivateField<int[]>(svg, "_nativeCompositionAnimatedChildIndexes");
        var animatedTargetKeys = GetPrivateField<string[]>(svg, "_nativeCompositionAnimatedTargetKeys");

        svg.SetAnimationTime(TimeSpan.FromSeconds(1));

        Assert.True(svg.TryCreateNativeCompositionFrame(out _));

        Assert.Same(sourceScene, GetPrivateField<SvgSceneDocument>(svg, "_nativeCompositionSourceScene"));
        Assert.Same(animatedChildIndexes, GetPrivateField<int[]>(svg, "_nativeCompositionAnimatedChildIndexes"));
        Assert.Same(animatedTargetKeys, GetPrivateField<string[]>(svg, "_nativeCompositionAnimatedTargetKeys"));
    }

    [Fact]
    public void TryCreateNativeCompositionScene_UsesTransformedDescendantBounds()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NativeCompositionTransformedDescendantSvg);

        Assert.True(svg.HasAnimations);
        Assert.True(svg.SupportsNativeComposition);
        Assert.True(svg.TryCreateNativeCompositionScene(out var scene));
        Assert.NotNull(scene);

        var animatedLayer = Assert.Single(scene!.Layers);
        Assert.Equal(0, animatedLayer.DocumentChildIndex);
        Assert.True(animatedLayer.IsAnimated);
        Assert.True(animatedLayer.IsVisible);
        Assert.NotNull(animatedLayer.Picture);
        Assert.Equal(0f, animatedLayer.Offset.X, 3);
        Assert.Equal(0f, animatedLayer.Offset.Y, 3);
        Assert.Equal(50f, animatedLayer.Size.Width, 3);
        Assert.Equal(10f, animatedLayer.Size.Height, 3);
    }

    [Fact]
    public void SupportsNativeComposition_SupportsDefsBackedAnimatedUseTargets()
    {
        using var svg = new SKSvg();
        svg.FromSvg(DefsBackedUseAnimationSvg);

        Assert.True(svg.HasAnimations);
        Assert.True(svg.SupportsNativeComposition);
        Assert.True(svg.TryCreateNativeCompositionScene(out var scene));
        Assert.NotNull(scene);

        svg.SetAnimationTime(TimeSpan.FromSeconds(1));

        Assert.True(svg.TryCreateNativeCompositionFrame(out var frame));
        Assert.NotNull(frame);
        var animatedLayer = Assert.Single(frame!.Layers);
        Assert.Equal(1, animatedLayer.DocumentChildIndex);
        Assert.True(animatedLayer.IsAnimated);
        Assert.True(animatedLayer.IsVisible);
        Assert.NotNull(animatedLayer.Picture);
    }

    [Fact]
    public void TryCreateNativeCompositionScene_UsesRetainedSceneState_NotLiveDomMutation()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NativeCompositionSceneSvg);

        var retainedScene = svg.RetainedSceneGraph;
        Assert.NotNull(retainedScene);

        var sourceDocument = retainedScene!.SourceDocument;
        Assert.NotNull(sourceDocument);

        var animated = Assert.IsType<SvgRectangle>(sourceDocument.GetElementById("animated"));
        animated.Opacity = 0.9f;
        animated.Transforms = new SvgTransformCollection
        {
            new SvgTranslate(20, 30)
        };

        Assert.True(svg.TryCreateNativeCompositionScene(out var scene));
        Assert.NotNull(scene);

        var animatedLayer = scene!.Layers[1];
        Assert.Equal(4f, animatedLayer.Offset.X, 3);
        Assert.Equal(6f, animatedLayer.Offset.Y, 3);
        Assert.Equal(0.5f, animatedLayer.Opacity, 3);
    }

    [Fact]
    public void TryCreateNativeCompositionScene_PreservesDescendantOpacityLayersWhenRootOpacityIsExtracted()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NativeCompositionNestedOpacitySvg);

        Assert.True(svg.HasAnimations);
        Assert.True(svg.SupportsNativeComposition);
        Assert.True(svg.TryCreateNativeCompositionScene(out var scene));
        Assert.NotNull(scene);

        var animatedLayer = Assert.Single(scene!.Layers);
        Assert.Equal(0.5f, animatedLayer.Opacity, 3);
        Assert.NotNull(animatedLayer.Picture);

        using var renderedPicture = svg.SkiaModel.ToSKPicture(animatedLayer.Picture!);
        Assert.NotNull(renderedPicture);

        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(40, 30));
        Assert.NotNull(surface);

        surface!.Canvas.Clear(SkiaSharp.SKColors.Transparent);
        surface.Canvas.DrawPicture(renderedPicture);

        using var image = surface.Snapshot();
        using var bitmap = SkiaSharp.SKBitmap.FromImage(image);
        var pixel = bitmap.GetPixel(6, 12);
        Assert.InRange(pixel.Alpha, (byte)120, (byte)136);
    }

    private const string NativeCompositionSceneSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="40" height="30">
          <rect id="static" x="0" y="0" width="4" height="4" fill="red" />
          <rect id="animated" x="1" y="2" width="10" height="20" fill="blue" opacity="0.5" transform="translate(3,4)">
            <animate attributeName="x" from="1" to="11" dur="2s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string NativeCompositionNestedOpacitySvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="40" height="30">
          <g id="animated-root" opacity="0.5">
            <rect id="child" x="1" y="2" width="10" height="20" fill="blue" opacity="0.5">
              <animate attributeName="x" from="1" to="11" dur="2s" fill="freeze" />
            </rect>
          </g>
        </svg>
        """;

    private const string NativeCompositionFrameSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="40" height="30">
          <rect id="animated" x="2" y="3" width="10" height="10" fill="blue">
            <animateTransform attributeName="transform" type="translate" from="0 0" to="10 5" dur="1s" fill="freeze" />
          </rect>
        </svg>
        """;

    private const string NativeCompositionTransformedDescendantSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="50">
          <g id="animated-root">
            <rect x="0" y="0" width="10" height="10" fill="red">
              <animate attributeName="width" from="10" to="12" dur="1s" fill="freeze" />
            </rect>
            <g transform="translate(40,0)">
              <rect x="0" y="0" width="10" height="10" fill="blue" />
            </g>
          </g>
        </svg>
        """;

    private const string DefsBackedUseAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="40"
             height="20"
             viewBox="0 0 40 20">
          <defs>
            <rect id="template" x="0" y="0" width="8" height="8" fill="red">
              <animate attributeName="x" from="0" to="10" dur="2s" fill="freeze" />
            </rect>
          </defs>
          <use id="instance" xlink:href="#template" />
        </svg>
        """;

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(instance));
    }
}
