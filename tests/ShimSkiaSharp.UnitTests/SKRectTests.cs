using ShimSkiaSharp;
using Xunit;

namespace ShimSkiaSharp.UnitTests;

public class SKRectTests
{
    [Fact]
    public void Create_Works()
    {
        var rect = SKRect.Create(5, 7, 10, 20);
        Assert.Equal(5, rect.Left);
        Assert.Equal(7, rect.Top);
        Assert.Equal(15, rect.Right);
        Assert.Equal(27, rect.Bottom);
        Assert.Equal(10, rect.Width);
        Assert.Equal(20, rect.Height);
    }

    [Fact]
    public void Contains_Point_Works()
    {
        var rect = SKRect.Create(0, 0, 10, 10);
        Assert.True(rect.Contains(new SKPoint(5, 5)));
        Assert.False(rect.Contains(new SKPoint(20, 20)));
    }

    [Fact]
    public void Contains_Rect_Works()
    {
        var outer = SKRect.Create(0, 0, 10, 10);
        var inner = SKRect.Create(2, 2, 5, 5);
        Assert.True(outer.Contains(inner));
        Assert.False(inner.Contains(outer));
    }

    [Fact]
    public void Union_Works()
    {
        var a = SKRect.Create(0, 0, 10, 10);
        var b = SKRect.Create(5, 5, 10, 10);
        var u = SKRect.Union(a, b);
        Assert.Equal(0, u.Left);
        Assert.Equal(0, u.Top);
        Assert.Equal(15, u.Right);
        Assert.Equal(15, u.Bottom);
    }

    [Fact]
    public void ToString_ReturnsExpected()
    {
        var rect = SKRect.Create(1, 2, 5, 5);
        Assert.Equal("1, 2, 5, 5", rect.ToString());
    }
}
