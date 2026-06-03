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
    public void TryCreateEquivalentPath_EllipseWithOneNegativeRadius_UsesAbsoluteRadius()
    {
        var ellipse = new SvgEllipse
        {
            CenterX = 100,
            CenterY = 100,
            RadiusX = -80,
            RadiusY = 80
        };

        var created = SvgGeometryService.TryCreateEquivalentPath(ellipse, SKRect.Create(0, 0, 200, 200), out var path);

        Assert.True(created);
        var actualPath = Assert.IsType<SKPath>(path);
        Assert.Equal(20, actualPath.Bounds.Left, 3);
        Assert.Equal(20, actualPath.Bounds.Top, 3);
        Assert.Equal(180, actualPath.Bounds.Right, 3);
        Assert.Equal(180, actualPath.Bounds.Bottom, 3);
    }

    [Fact]
    public void TryCreateEquivalentPath_EllipseWithBothNegativeRadii_DoesNotRender()
    {
        var ellipse = new SvgEllipse
        {
            CenterX = 100,
            CenterY = 100,
            RadiusX = -80,
            RadiusY = -80
        };

        var created = SvgGeometryService.TryCreateEquivalentPath(ellipse, SKRect.Create(0, 0, 200, 200), out var path);

        Assert.False(created);
        Assert.Null(path);
    }

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

    [Fact]
    public void ToPath_LineAfterClosePath_ContinuesFromClosedSubpathStart()
    {
        var segments = SvgPathBuilder.Parse("M 10 10 L 10 20 L 20 10 z L 100 100 L 120 100".AsSpan());

        var path = Assert.IsType<SKPath>(segments.ToPath(SvgFillRule.NonZero));
        var commands = Assert.IsAssignableFrom<IList<PathCommand>>(path.Commands);

        Assert.Collection(
            commands,
            command => Assert.Equal(new MoveToPathCommand(10f, 10f), command),
            command => Assert.Equal(new LineToPathCommand(10f, 20f), command),
            command => Assert.Equal(new LineToPathCommand(20f, 10f), command),
            command => Assert.IsType<ClosePathCommand>(command),
            command => Assert.Equal(new LineToPathCommand(100f, 100f), command),
            command => Assert.Equal(new LineToPathCommand(120f, 100f), command));
    }

    [Fact]
    public void ToPath_ClosedLineOnlyPath_UsesPolyCommand()
    {
        var segments = SvgPathBuilder.Parse("M 0 14 L 7 0 L 14 14 Z".AsSpan());

        var path = Assert.IsType<SKPath>(segments.ToPath(SvgFillRule.NonZero));
        var command = Assert.Single(Assert.IsAssignableFrom<IList<PathCommand>>(path.Commands));
        var poly = Assert.IsType<AddPolyPathCommand>(command);
        var points = Assert.IsAssignableFrom<IList<SKPoint>>(poly.Points);

        Assert.True(poly.Close);
        Assert.Equal(
            new[]
            {
                new SKPoint(0f, 14f),
                new SKPoint(7f, 0f),
                new SKPoint(14f, 14f)
            },
            points);
    }

    [Fact]
    public void ToPath_ClosedRelativeLineOnlyPath_UsesAbsolutePolyPoints()
    {
        var segments = SvgPathBuilder.Parse("M 5 5 l 10 0 v 10 h -10 z".AsSpan());

        var path = Assert.IsType<SKPath>(segments.ToPath(SvgFillRule.EvenOdd));
        Assert.Equal(SKPathFillType.EvenOdd, path.FillType);
        var command = Assert.Single(Assert.IsAssignableFrom<IList<PathCommand>>(path.Commands));
        var poly = Assert.IsType<AddPolyPathCommand>(command);
        var points = Assert.IsAssignableFrom<IList<SKPoint>>(poly.Points);

        Assert.True(poly.Close);
        Assert.Equal(
            new[]
            {
                new SKPoint(5f, 5f),
                new SKPoint(15f, 5f),
                new SKPoint(15f, 15f),
                new SKPoint(5f, 15f)
            },
            points);
    }

    [Fact]
    public void ToPath_ArcAfterClosePath_ContinuesFromClosedSubpathStart()
    {
        var segments = SvgPathBuilder.Parse("M 10 50 L 10 10 L 50 10 z A 5 5 0 0 1 150 150".AsSpan());

        var path = Assert.IsType<SKPath>(segments.ToPath(SvgFillRule.NonZero));
        var commands = Assert.IsAssignableFrom<IList<PathCommand>>(path.Commands);

        Assert.IsType<ClosePathCommand>(commands[3]);
        Assert.Equal(new ArcToPathCommand(5f, 5f, 0f, SKPathArcSize.Small, SKPathDirection.Clockwise, 150f, 150f), commands[4]);
    }

    [Fact]
    public void ToPath_ZeroRadiusArc_ConvertsToLine()
    {
        var segments = SvgPathBuilder.Parse("M 0 0 A 0 10 0 0 1 20 20".AsSpan());

        var path = Assert.IsType<SKPath>(segments.ToPath(SvgFillRule.NonZero));
        var commands = Assert.IsAssignableFrom<IList<PathCommand>>(path.Commands);

        Assert.Collection(
            commands,
            command => Assert.Equal(new MoveToPathCommand(0f, 0f), command),
            command => Assert.Equal(new LineToPathCommand(20f, 20f), command));
    }

    [Fact]
    public void ToPath_EllipseMissingRy_MirrorsComputedRxLength()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="100">
              <ellipse id="target" cx="100" cy="50" rx="25%" />
            </svg>
            """);
        var ellipse = Assert.IsType<SvgEllipse>(document!.GetElementById("target"));

        var path = Assert.IsType<SKPath>(ellipse.ToPath(SvgFillRule.NonZero, SKRect.Create(0f, 0f, 200f, 100f)));
        var command = Assert.Single(Assert.IsAssignableFrom<IList<PathCommand>>(path.Commands));
        var oval = Assert.IsType<AddOvalPathCommand>(command);

        Assert.Equal(SKRect.Create(50f, 0f, 100f, 100f), oval.Rect);
    }

    [Fact]
    public void ToPath_EllipseWithOneNegativeRadius_UsesAbsoluteRadius()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <ellipse id="target" cx="50" cy="50" rx="-20" ry="10" />
            </svg>
            """);
        var ellipse = Assert.IsType<SvgEllipse>(document!.GetElementById("target"));

        var path = Assert.IsType<SKPath>(ellipse.ToPath(SvgFillRule.NonZero, SKRect.Create(0f, 0f, 100f, 100f)));
        var command = Assert.Single(Assert.IsAssignableFrom<IList<PathCommand>>(path.Commands));
        var oval = Assert.IsType<AddOvalPathCommand>(command);

        Assert.Equal(SKRect.Create(30f, 40f, 40f, 20f), oval.Rect);
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
