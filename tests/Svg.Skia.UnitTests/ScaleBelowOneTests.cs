using System.IO;
using SkiaSharp;
using Xunit;

namespace Svg.Skia.UnitTests;

public class ScaleBelowOneTests
{
    [Fact]
    public void BitmapScale_BelowOne_ShouldNotBeEmpty()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"none\" stroke=\"black\"/></svg>";
        var skSvg = SKSvg.CreateFromSvg(svg);

        using var ms = new MemoryStream();
        var saved = skSvg.Save(ms, SKColors.Transparent, scaleX: 0.05f, scaleY: 0.05f);
        Assert.True(saved);

        ms.Position = 0;
        using var bitmap = SKBitmap.Decode(ms);
        Assert.True(bitmap.Width > 0);
        Assert.True(bitmap.Height > 0);

        var visible = false;
        for (var x = 0; x < bitmap.Width && !visible; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                if (bitmap.GetPixel(x, y).Alpha != 0)
                {
                    visible = true;
                    break;
                }
            }
        }

        Assert.True(visible);
    }
}

