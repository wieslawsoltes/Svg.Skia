using Xunit;
using ShimSkiaSharp;

namespace ShimSkiaSharp.UnitTests;

public class SKPointITests
{
    [Fact]
    public void Empty_IsEmpty()
    {
        Assert.True(SKPointI.Empty.IsEmpty);
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        var p = new SKPointI(7, 8);
        Assert.Equal(7, p.X);
        Assert.Equal(8, p.Y);
        Assert.False(p.IsEmpty);
    }

    [Fact]
    public void ToString_ReturnsValues()
    {
        var p = new SKPointI(1, 2);
        Assert.Equal("1, 2", p.ToString());
    }
}
