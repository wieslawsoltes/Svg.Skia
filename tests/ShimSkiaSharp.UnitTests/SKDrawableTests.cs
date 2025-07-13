using Xunit;
using ShimSkiaSharp;

namespace ShimSkiaSharp.UnitTests;

public class SKDrawableTests
{
    private class TestDrawable : SKDrawable
    {
        public int DrawCalls { get; private set; }
        protected override void OnDraw(SKCanvas canvas)
        {
            DrawCalls++;
            canvas.Save();
        }
        protected override SKRect OnGetBounds() => SKRect.Create(1, 2, 3, 4);
    }

    [Fact]
    public void Snapshot_Uses_OnGetBounds()
    {
        var drawable = new TestDrawable();
        var picture = drawable.Snapshot();
        Assert.Equal(SKRect.Create(1, 2, 3, 4), picture.CullRect);
        Assert.Equal(1, drawable.DrawCalls);
        Assert.Single(picture.Commands!);
        Assert.IsType<SaveCanvasCommand>(picture.Commands![0]);
    }

    [Fact]
    public void Snapshot_WithBounds_UsesProvidedBounds()
    {
        var drawable = new TestDrawable();
        var bounds = SKRect.Create(10, 10, 5, 5);
        var picture = drawable.Snapshot(bounds);
        Assert.Equal(bounds, picture.CullRect);
        Assert.Equal(1, drawable.DrawCalls);
    }
}
