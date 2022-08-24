namespace Svg.Skia.UnitTests;
using ShimSkiaSharp;
using Xunit;
public class SKSvgTests
{
    [Fact]
    public void Typeface_Splitting()
    {
        var text = "123一二三ຫນຶ່ງ​ສອງ​ສາມABC😊😃😄123😈4🚀";
        var typefaceRegions = new SkiaAssetLoader().FindTypefaces(text, new());
        Assert.All(typefaceRegions, region => {
            Assert.NotNull(region.typeface);
            Assert.Equal(SKFontStyleWeight.Normal, region.typeface!.FontWeight);
            Assert.Equal(SKFontStyleWidth.Normal, region.typeface.FontWidth);
            Assert.Equal(SKFontStyleSlant.Upright, region.typeface.Style);
        });
        Assert.Equal(new[] {
            "123",
            "一二三",
            "ຫນຶ່ງ",
            "​", // U+200B 	ZERO WIDTH SPACE
            "ສອງ",
            "​", // U+200B 	ZERO WIDTH SPACE
            "ສາມ",
            "ABC",
            "😊😃😄",
            "123",
            "😈",
            "4",
            "🚀"
        }, System.Linq.Enumerable.Select(typefaceRegions, region => region.text));
    }
}
