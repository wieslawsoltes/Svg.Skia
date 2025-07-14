using System.Linq;
using Xunit;
using ShimSkiaSharp;

namespace ShimSkiaSharp.UnitTests;

public class SKPathTests
{
    [Fact]
    public void NewPath_IsEmpty()
    {
        var path = new SKPath();
        Assert.True(path.IsEmpty);
        Assert.Empty(path.Commands);
        Assert.Equal(SKRect.Empty, path.Bounds);
    }

    [Fact]
    public void AddRect_AddsCommandAndUpdatesBounds()
    {
        var rect = SKRect.Create(0, 0, 10, 20);
        var path = new SKPath();
        path.AddRect(rect);
        Assert.False(path.IsEmpty);
        Assert.Single(path.Commands);
        var cmd = Assert.IsType<AddRectPathCommand>(path.Commands.First());
        Assert.Equal(rect, cmd.Rect);
        Assert.Equal(rect, path.Bounds);
    }

    [Fact]
    public void MoveTo_LineTo_UpdatesBounds()
    {
        var path = new SKPath();
        path.MoveTo(1, 2);
        path.LineTo(3, 4);
        Assert.Equal(new SKRect(1,2,3,4), path.Bounds);
    }

    [Fact]
    public void QuadTo_UpdatesBoundsPrecisely()
    {
        var path = new SKPath();
        path.MoveTo(0, 0);
        path.QuadTo(0.5f, 1f, 1f, 0f);
        var expected = new SKRect(0f, 0f, 1f, 0.5f);
        Assert.Equal(expected.Left, path.Bounds.Left, 3);
        Assert.Equal(expected.Top, path.Bounds.Top, 3);
        Assert.Equal(expected.Right, path.Bounds.Right, 3);
        Assert.Equal(expected.Bottom, path.Bounds.Bottom, 3);
    }
}
