using ShimSkiaSharp;
using Xunit;

namespace ShimSkiaSharp.UnitTests;

public class SKSizeITests
{
    [Fact]
    public void Empty_IsEmpty()
    {
        Assert.True(SKSizeI.Empty.IsEmpty);
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        var s = new SKSizeI(9, 10);
        Assert.Equal(9, s.Width);
        Assert.Equal(10, s.Height);
        Assert.False(s.IsEmpty);
    }

    [Fact]
    public void ToString_ReturnsValues()
    {
        var s = new SKSizeI(3, 4);
        Assert.Equal("3, 4", s.ToString());
    }
}
