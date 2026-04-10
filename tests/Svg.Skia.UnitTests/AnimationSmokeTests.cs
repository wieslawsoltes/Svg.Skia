using ShimSkiaSharp;
using SkiaSharp;
using Xunit;

namespace Svg.Skia.UnitTests;

public class AnimationSmokeTests
{
    [Fact]
    public void FromSvg_RendersStaticContentWhenAnimationElementsArePresent()
    {
        using var svg = new SKSvg();

        var picture = svg.FromSvg(AnimationSvg);

        Assert.NotNull(picture);
        Assert.NotNull(svg.Picture);

        using var bitmap = svg.Picture!.ToBitmap(
            SKColors.Transparent,
            1f,
            1f,
            SKColorType.Rgba8888,
            SKAlphaType.Unpremul,
            svg.Settings.Srgb);

        Assert.NotNull(bitmap);

        var center = bitmap!.GetPixel(10, 10);
        Assert.Equal((byte)255, center.Alpha);
        Assert.True(center.Green > 0);
    }

    private const string AnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="20"
             height="20"
             viewBox="0 0 20 20">
          <defs>
            <path id="motionPath" d="M0,0 L20,20" />
          </defs>
          <rect id="target" x="0" y="0" width="20" height="20" fill="#00ff00">
            <animate id="animate1" attributeName="x" from="0" to="5" dur="1s" />
            <set id="set1" attributeName="visibility" to="visible" begin="0s" />
            <animateColor id="animateColor1" attributeName="fill" from="#00ff00" to="#0000ff" dur="1s" />
            <animateTransform id="animateTransform1" attributeName="transform" type="rotate" from="0 10 10" to="90 10 10" dur="1s" />
            <animateMotion id="animateMotion1" dur="1s" path="M0,0 L5,5">
              <mpath id="mpath1" xlink:href="#motionPath" />
            </animateMotion>
          </rect>
        </svg>
        """;
}
