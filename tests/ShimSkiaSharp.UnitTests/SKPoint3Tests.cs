using ShimSkiaSharp;
using Xunit;

namespace ShimSkiaSharp.UnitTests;

public class SKPoint3Tests
{
    [Fact]
    public void Empty_IsEmpty()
    {
        Assert.True(SKPoint3.Empty.IsEmpty);
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        var p = new SKPoint3(1f, 2f, 3f);
        Assert.Equal(1f, p.X);
        Assert.Equal(2f, p.Y);
        Assert.Equal(3f, p.Z);
        Assert.False(p.IsEmpty);
    }

    [Fact]
    public void ToString_ReturnsValues()
    {
        var p = new SKPoint3(4f, 5f, 6f);
        Assert.Equal("4, 5, 6", p.ToString());
    }
}
