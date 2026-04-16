using System.Linq;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using SkiaSharp;
using Svg;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SKSvgLayerBoundsTests
{
    [Fact]
    public void RetainedSceneMutation_RefreshesOpacityLayerBoundsAfterGeometryChange()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="20" viewBox="0 0 40 20">
              <g opacity="0.5">
                <rect id="target" x="0" y="0" width="10" height="20" fill="#ff0000" />
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        Assert.NotNull(svg.FromSvg(svgMarkup));

        using (var before = svg.Picture!.ToBitmap(
                   SKColors.Transparent,
                   1f,
                   1f,
                   SKColorType.Rgba8888,
                   SKAlphaType.Premul,
                   svg.Settings.Srgb))
        {
            Assert.NotNull(before);
            Assert.Equal((byte)0, before!.GetPixel(25, 10).Alpha);
        }

        var rect = Assert.IsType<SvgRectangle>(svg.SourceDocument!.GetElementById("target"));
        rect.Width = new SvgUnit(SvgUnitType.User, 30f);

        Assert.True(svg.TryApplyRetainedSceneMutationAndRender(rect, new[] { "width" }, out var result));
        Assert.True(result?.Succeeded);

        using var after = svg.Picture!.ToBitmap(
            SKColors.Transparent,
            1f,
            1f,
            SKColorType.Rgba8888,
            SKAlphaType.Premul,
            svg.Settings.Srgb);

        Assert.NotNull(after);
        var pixel = after!.GetPixel(25, 10);
        Assert.True(pixel.Red > 200);
        Assert.True(pixel.Alpha > 100);
    }

    [Fact]
    public void RetainedSceneGraph_UsesBoundedSaveLayerUnderSingularAncestorTransform()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="32" viewBox="0 0 64 32">
              <g transform="matrix(0 0 0 1 24 0)">
                <g opacity="0.5">
                  <rect x="2" y="3" width="20" height="18" fill="none" stroke="#ff0000" stroke-width="4" />
                </g>
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        var retainedModel = svg.CreateRetainedSceneGraphModel();

        Assert.NotNull(retainedModel);
        var saveLayers = retainedModel!.FindCommands<SaveLayerCanvasCommand>().ToArray();
        Assert.NotEmpty(saveLayers);
        Assert.All(saveLayers, saveLayer => Assert.True(saveLayer.Bounds is { } bounds && !bounds.IsEmpty));
    }
}
