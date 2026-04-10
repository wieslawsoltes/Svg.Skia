using System;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgAnimationClockTests
{
    [Fact]
    public void AdvanceBy_SaturatesWithoutOverflow()
    {
        var clock = new SvgAnimationClock();

        clock.Seek(TimeSpan.MaxValue - TimeSpan.FromTicks(1));
        clock.AdvanceBy(TimeSpan.MaxValue);
        Assert.Equal(TimeSpan.MaxValue, clock.CurrentTime);

        clock.AdvanceBy(TimeSpan.MinValue);
        Assert.Equal(TimeSpan.Zero, clock.CurrentTime);
    }
}
