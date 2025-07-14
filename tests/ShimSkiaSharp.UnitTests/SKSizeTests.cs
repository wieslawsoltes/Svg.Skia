using Xunit;
using ShimSkiaSharp;

namespace ShimSkiaSharp.UnitTests;

public class SKSizeTests
{
    [Fact]
    public void Empty_IsEmpty()
    {
        Assert.True(SKSize.Empty.IsEmpty);
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        var s = new SKSize(5.5f, 6.5f);
        Assert.Equal(5.5f, s.Width);
        Assert.Equal(6.5f, s.Height);
        Assert.False(s.IsEmpty);
    }

    [Fact]
    public void ToString_ReturnsValues()
    {
        var s = new SKSize(2f, 3f);
        Assert.Equal("2, 3", s.ToString());
    }
}
