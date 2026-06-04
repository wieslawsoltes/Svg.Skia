using System.Linq;
using ShimSkiaSharp;
using Xunit;

namespace ShimSkiaSharp.UnitTests;

public class SKPathTests
{
    [Fact]
    public void NewPath_IsEmpty()
    {
        var path = new SKPath();
        Assert.True(path.IsEmpty);
        var commands = path.Commands!;
        Assert.NotNull(commands);
        Assert.Empty(commands);
        Assert.Equal(SKRect.Empty, path.Bounds);
    }

    [Fact]
    public void AddRect_AddsCommandAndUpdatesBounds()
    {
        var rect = SKRect.Create(0, 0, 10, 20);
        var path = new SKPath();
        path.AddRect(rect);
        Assert.False(path.IsEmpty);
        var commands = path.Commands!;
        Assert.NotNull(commands);
        Assert.Single(commands);
        var cmd = Assert.IsType<AddRectPathCommand>(commands.First());
        Assert.Equal(rect, cmd.Rect);
        Assert.Equal(rect, path.Bounds);
    }

    [Fact]
    public void Commands_SupportInlineAndOverflowMutations()
    {
        var path = new SKPath();
        var commands = path.Commands!;
        var first = new AddRectPathCommand(SKRect.Create(0, 0, 10, 10));
        var second = new AddRectPathCommand(SKRect.Create(20, 20, 5, 5));
        var inserted = new AddRectPathCommand(SKRect.Create(-5, -5, 2, 2));

        commands.Add(first);
        commands.Add(second);
        Assert.Equal(2, commands.Count);
        Assert.Equal(new SKRect(0, 0, 25, 25), path.Bounds);

        commands.Insert(1, inserted);
        Assert.Equal(new PathCommand[] { first, inserted, second }, commands.ToArray());
        Assert.Equal(new SKRect(-5, -5, 25, 25), path.Bounds);

        commands.RemoveAt(0);
        commands[0] = new AddRectPathCommand(SKRect.Create(2, 3, 4, 5));
        Assert.Equal(new SKRect(2, 3, 25, 25), path.Bounds);

        Assert.True(commands.Remove(second));
        Assert.Single(commands);

        commands.Clear();
        Assert.Empty(commands);
        Assert.True(path.IsEmpty);
        Assert.Equal(SKRect.Empty, path.Bounds);
    }

    [Fact]
    public void Bounds_RecomputesAfterPathMutation()
    {
        var path = new SKPath();
        path.AddRect(SKRect.Create(0, 0, 10, 10));

        _ = path.Bounds;

        path.AddRect(SKRect.Create(20, 20, 5, 5));

        Assert.Equal(new SKRect(0, 0, 25, 25), path.Bounds);
    }

    [Fact]
    public void AddPolyBounds_ReflectPointMutations()
    {
        var points = new[]
        {
            new SKPoint(0, 0),
            new SKPoint(10, 10),
            new SKPoint(0, 10)
        };
        var path = new SKPath();
        path.AddPoly(points);

        _ = path.Bounds;

        points[1] = new SKPoint(50, 50);

        Assert.Equal(new SKRect(0, 0, 50, 50), path.Bounds);
    }

    [Fact]
    public void AddPolyInlinePoints_ReflectPointListMutations()
    {
        var path = new SKPath();
        path.AddPoly(
            new SKPoint(0, 0),
            new SKPoint(10, 10),
            new SKPoint(0, 10));

        var poly = Assert.IsType<AddPolyPathCommand>(Assert.Single(path.Commands!));
        Assert.Same(poly, poly.Points);

        _ = path.Bounds;

        poly.Points![1] = new SKPoint(50, 50);

        Assert.Equal(new SKRect(0, 0, 50, 50), path.Bounds);
    }

    [Fact]
    public void MoveTo_LineTo_UpdatesBounds()
    {
        var path = new SKPath();
        path.MoveTo(1, 2);
        path.LineTo(3, 4);
        Assert.Equal(new SKRect(1, 2, 3, 4), path.Bounds);
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
