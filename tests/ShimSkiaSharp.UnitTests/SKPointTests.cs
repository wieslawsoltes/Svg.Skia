using Xunit;
using ShimSkiaSharp;

namespace ShimSkiaSharp.UnitTests;

public class SKPointTests
{
    [Fact]
    public void Empty_IsEmpty()
    {
        Assert.True(SKPoint.Empty.IsEmpty);
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        var p = new SKPoint(3.5f, 4.5f);
        Assert.Equal(3.5f, p.X);
        Assert.Equal(4.5f, p.Y);
        Assert.False(p.IsEmpty);
    }

    [Fact]
    public void ToString_ReturnsValues()
    {
        var p = new SKPoint(1f, 2f);
        Assert.Equal("1, 2", p.ToString());
    }
}
