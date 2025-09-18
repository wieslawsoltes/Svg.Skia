using ShimSkiaSharp;
using Xunit;

namespace ShimSkiaSharp.UnitTests;

public class SKColorTests
{
    [Fact]
    public void Implicit_To_SKColorF_Works()
    {
        var color = new SKColor(255, 128, 64, 32);
        SKColorF colorF = color;
        Assert.Equal(1f, colorF.Red, 6);
        Assert.Equal(128f / 255f, colorF.Green, 6);
        Assert.Equal(64f / 255f, colorF.Blue, 6);
        Assert.Equal(32f / 255f, colorF.Alpha, 6);
    }

    [Fact]
    public void ToString_Returns_CommaSeparatedValues()
    {
        var color = new SKColor(1, 2, 3, 4);
        Assert.Equal("1, 2, 3, 4", color.ToString());
    }
}
