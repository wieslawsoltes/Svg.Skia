namespace Svg.Skia.UnitTests;
using System.IO;
using Xunit;
public class SKSvgTests
{
    [Fact]
    public void Sign_in()
    {
        static string CaseFile(string name) =>
            Path.Combine("..", "..", "..", "Test cases", name);
        var svg = new SKSvg();
        using var _ = svg.Load(CaseFile("Sign in.svg"));
        svg.Save(CaseFile("Sign in (Actual).png"), SkiaSharp.SKColors.Transparent);

        using var expected = SkiaSharp.SKBitmap.Decode(CaseFile("Sign in.png"));
        using var actual = SkiaSharp.SKBitmap.Decode(CaseFile("Sign in (Actual).png"));
        Assert.Equal(expected.Info, actual.Info);
        Assert.Equal(expected.Bytes, actual.Bytes);
    }
}
