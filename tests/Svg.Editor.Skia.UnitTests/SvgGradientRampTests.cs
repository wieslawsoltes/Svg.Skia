using System;
using System.Linq;
using SkiaSharp;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgGradientRampTests
{
    [Fact]
    public void ChangesDoNotMutateEarlierSnapshots()
    {
        var ramp = SvgGradientRamp.Default;
        var id = ramp.Stops[0].Id;
        var moved = ramp.SetOffset(id, 0.75).SetColor(id, SKColors.Lime).SetOpacity(id, 0.4);
        Assert.Equal(0, ramp.Find(id).Offset);
        Assert.NotEqual(SKColors.Lime, ramp.Find(id).Color);
        Assert.Equal(1, ramp.Find(id).Opacity);
        Assert.Equal(0.75, moved.Find(id).Offset);
        Assert.Equal(SKColors.Lime, moved.Find(id).Color);
        Assert.Equal(0.4, moved.Find(id).Opacity);
    }

    [Fact]
    public void EqualOffsetsRetainExplicitOrderAndReverseReversesHardEdges()
    {
        var ramp = new SvgGradientRamp(new[] { (0d, SKColors.Red, 1d), (0.5, SKColors.Red, 1d), (0.5, SKColors.Blue, 1d), (1d, SKColors.Blue, 1d) });
        Assert.Equal(SKColors.Blue, ramp.Sample(0.5).Color);
        var reversed = ramp.Reverse();
        Assert.Equal(SKColors.Red, reversed.Sample(0.5).Color);
        Assert.Equal(ramp.Stops.Select(stop => stop.Id), reversed.Reverse().Stops.Select(stop => stop.Id));
        Assert.Equal(ramp.Stops.Select(stop => stop.Offset), reversed.Reverse().Stops.Select(stop => stop.Offset));
    }

    [Fact]
    public void MovingAcrossOtherStopsRetainsIdentity()
    {
        var ramp = SvgGradientRamp.Default.Add(0.25, SKColors.Lime, 1, out var id);
        var moved = ramp.SetOffset(id, 0.9);
        Assert.Equal(id, moved.Stops[1].Id);
        Assert.Equal(0.9, moved.Stops[1].Offset);
        Assert.Equal(0.25, ramp.Find(id).Offset);
    }

    [Fact]
    public void DuplicateIsAdjacentEvenAtAHardEdge()
    {
        var ramp = new SvgGradientRamp(new[] { (0d, SKColors.Red, 1d), (0.5, SKColors.Red, 1d), (0.5, SKColors.Blue, 1d), (1d, SKColors.Blue, 1d) });
        var copy = ramp.Duplicate(ramp.Stops[1].Id, out var id);
        Assert.Equal(id, copy.Stops[2].Id);
        Assert.Equal(SKColors.Red, copy.Stops[2].Color);
        Assert.Equal(SKColors.Blue, copy.Stops[3].Color);
        Assert.Equal(4, ramp.Stops.Count);
    }

    [Fact]
    public void EvenDistributionPreservesColorsOpacityAndIdentity()
    {
        var ramp = SvgGradientRamp.Default.Add(0.1, SKColors.Lime, 0.3, out var id);
        var even = ramp.DistributeEvenly();
        Assert.Equal(0.5, even.Find(id).Offset);
        Assert.Equal(0.3, even.Find(id).Opacity);
        Assert.Equal(SKColors.Lime, even.Find(id).Color);
    }

    [Fact]
    public void SamplingClampsToEndStopsAndKeepsAlphaSeparate()
    {
        var ramp = new SvgGradientRamp(new[] { (0.2, new SKColor(255, 0, 0, 128), 0.5), (0.8, SKColors.Blue, 1d) });
        Assert.Equal(ramp.Stops[0].Color, ramp.Sample(0).Color);
        Assert.Equal(ramp.Stops[1].Color, ramp.Sample(1).Color);
        var midpoint = ramp.Sample(0.5);
        Assert.InRange(midpoint.Color.Red, (byte)127, (byte)128);
        Assert.InRange(midpoint.Color.Blue, (byte)127, (byte)128);
        Assert.Equal(0.75, midpoint.Opacity, 8);
        Assert.Equal((byte)64, ramp.Stops[0].PreviewColor.Alpha);
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(-0.1)] [InlineData(1.1)]
    public void InvalidValuesCannotCreatePartiallyModifiedRamps(double value)
    {
        var ramp = SvgGradientRamp.Default;
        Assert.Throws<ArgumentOutOfRangeException>(() => ramp.SetOffset(ramp.Stops[0].Id, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => ramp.SetOpacity(ramp.Stops[0].Id, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => ramp.Add(value, SKColors.Red, 1, out _));
        Assert.Equal(0, ramp.Stops[0].Offset);
        Assert.Equal(2, ramp.Stops.Count);
    }

    [Fact]
    public void MinimumAndMaximumStopBudgetsAreEnforced()
    {
        Assert.Throws<ArgumentException>(() => new SvgGradientRamp(Array.Empty<(double, SKColor, double)>()));
        var two = SvgGradientRamp.Default;
        Assert.Throws<InvalidOperationException>(() => two.Remove(two.Stops[0].Id));
        var maximum = new SvgGradientRamp(Enumerable.Range(0, SvgGradientRamp.MaximumStops).Select(index => (index / 255d, SKColors.Red, 1d)));
        Assert.Throws<InvalidOperationException>(() => maximum.Add(0.5, SKColors.Red, 1, out _));
        Assert.Throws<InvalidOperationException>(() => maximum.Duplicate(maximum.Stops[0].Id, out _));
        Assert.Throws<ArgumentException>(() => new SvgGradientRamp(Enumerable.Repeat((0d, SKColors.Red, 1d), SvgGradientRamp.MaximumStops + 1)));
    }

    [Fact]
    public void UnknownIdentityIsRejectedAndNoopChangesReuseSnapshot()
    {
        var ramp = SvgGradientRamp.Default;
        Assert.Throws<ArgumentException>(() => ramp.Remove(Guid.NewGuid()));
        Assert.Same(ramp, ramp.SetOffset(ramp.Stops[0].Id, ramp.Stops[0].Offset));
        Assert.Same(ramp, ramp.SetColor(ramp.Stops[0].Id, ramp.Stops[0].Color));
    }
}
