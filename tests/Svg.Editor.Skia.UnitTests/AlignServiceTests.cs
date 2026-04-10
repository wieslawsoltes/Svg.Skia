using System.Linq;
using Svg;
using Svg.Editor.Skia;
using Svg.Transforms;
using Xunit;
using SK = SkiaSharp;

namespace Svg.Editor.Skia.UnitTests;

public class AlignServiceTests
{
    [Fact]
    public void Align_UsesExplicitBoundsInsteadOfDrawableInstances()
    {
        var service = new AlignService();
        var first = new SvgRectangle { Width = 10, Height = 10 };
        var second = new SvgRectangle { Width = 10, Height = 10 };

        service.Align(new[]
        {
            (first as SvgVisualElement, new SK.SKRect(10, 0, 20, 10)),
            (second as SvgVisualElement, new SK.SKRect(30, 0, 40, 10))
        }, AlignService.AlignType.Left);

        var firstTranslation = Assert.Single(first.Transforms!.OfType<SvgTranslate>());
        Assert.Equal(0f, firstTranslation.X);
        Assert.Equal(0f, firstTranslation.Y);
        var translation = Assert.Single(second.Transforms!.OfType<SvgTranslate>());
        Assert.Equal(-20f, translation.X);
        Assert.Equal(0f, translation.Y);
    }

    [Fact]
    public void Distribute_UsesExplicitBoundsInsteadOfDrawableInstances()
    {
        var service = new AlignService();
        var first = new SvgRectangle { Width = 10, Height = 10 };
        var middle = new SvgRectangle { Width = 10, Height = 10 };
        var last = new SvgRectangle { Width = 10, Height = 10 };

        service.Distribute(new[]
        {
            (first as SvgVisualElement, new SK.SKRect(0, 0, 10, 10)),
            (middle as SvgVisualElement, new SK.SKRect(40, 0, 50, 10)),
            (last as SvgVisualElement, new SK.SKRect(100, 0, 110, 10))
        }, AlignService.DistributeType.Horizontal);

        Assert.Null(first.Transforms);
        var translation = Assert.Single(middle.Transforms!.OfType<SvgTranslate>());
        Assert.Equal(10f, translation.X);
        Assert.Equal(0f, translation.Y);
        Assert.Null(last.Transforms);
    }
}
