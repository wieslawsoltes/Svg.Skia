using System;
using System.Collections.Generic;
using System.Linq;
using ShimSkiaSharp;
using Svg;
using Svg.Model.Services;
using Svg.Pathing;
using Xunit;

namespace Svg.Model.UnitTests;

public class PathingServiceTests
{
    [Fact]
    public void ToPath_SmoothCubicSegment_ReflectsPreviousSecondControlPoint()
    {
        var shorthand = SvgPathBuilder.Parse("M0 0 C10 0 20 10 30 10 S40 20 50 10".AsSpan());
        var expanded = SvgPathBuilder.Parse("M0 0 C10 0 20 10 30 10 C40 10 40 20 50 10".AsSpan());

        var shorthandPath = Assert.IsType<SKPath>(shorthand.ToPath(SvgFillRule.NonZero));
        var expandedPath = Assert.IsType<SKPath>(expanded.ToPath(SvgFillRule.NonZero));

        AssertPathsEqual(expandedPath, shorthandPath);
    }

    [Fact]
    public void ToPath_SmoothQuadraticSegment_ReflectsPreviousControlPoint()
    {
        var shorthand = SvgPathBuilder.Parse("M0 0 Q10 10 20 0 T40 0".AsSpan());
        var expanded = SvgPathBuilder.Parse("M0 0 Q10 10 20 0 Q30 -10 40 0".AsSpan());

        var shorthandPath = Assert.IsType<SKPath>(shorthand.ToPath(SvgFillRule.NonZero));
        var expandedPath = Assert.IsType<SKPath>(expanded.ToPath(SvgFillRule.NonZero));

        AssertPathsEqual(expandedPath, shorthandPath);
    }

    private static void AssertPathsEqual(SKPath expected, SKPath actual)
    {
        Assert.Equal(expected.FillType, actual.FillType);
        var expectedCommands = Assert.IsAssignableFrom<IList<PathCommand>>(expected.Commands);
        var actualCommands = Assert.IsAssignableFrom<IList<PathCommand>>(actual.Commands);
        Assert.Equal(expectedCommands.Count, actualCommands.Count);

        for (var i = 0; i < expectedCommands.Count; i++)
        {
            Assert.Equal(expectedCommands[i], actualCommands[i]);
        }

        Assert.Equal(expected.Bounds.Left, actual.Bounds.Left, 3);
        Assert.Equal(expected.Bounds.Top, actual.Bounds.Top, 3);
        Assert.Equal(expected.Bounds.Right, actual.Bounds.Right, 3);
        Assert.Equal(expected.Bounds.Bottom, actual.Bounds.Bottom, 3);
    }
}
