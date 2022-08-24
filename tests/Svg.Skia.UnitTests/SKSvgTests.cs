namespace Svg.Skia.UnitTests;
using ShimSkiaSharp;
using Xunit;
public class SKSvgTests
{
    [Fact]
    public void Typeface_Splitting()
    {
        var text = "123𓀀𓀀𓀀456";
        var typefaceRegions = new SkiaAssetLoader().FindTypefaces(text, new());
        Assert.All(typefaceRegions, region => {
            if (region.typeface is null)
                return;
            Assert.Equal(SKFontStyleWeight.Normal, region.typeface.FontWeight);
            Assert.Equal(SKFontStyleWidth.Normal, region.typeface.FontWidth);
            Assert.Equal(SKFontStyleSlant.Upright, region.typeface.Style);
        });
        Assert.Equal(new[] {
            "123",
            "𓀀𓀀𓀀",
            "456"
        }, System.Linq.Enumerable.Select(typefaceRegions, region => region.text));
    }
}
