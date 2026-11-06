using System;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgStudioGridMetricsTests
{
    [Theory]
    [InlineData(220, 1, 220)]
    [InlineData(287, 1, 287)]
    [InlineData(295, 1, 295)]
    [InlineData(296, 2, 144)]
    [InlineData(400, 2, 196)]
    public void FieldsStackBeforeTheirMinimumWidthIsViolated(double width, int columns, double expected)
    {
        var layout = SvgStudioGridMetrics.Create(width, 4);
        Assert.Equal(columns, layout.Columns); Assert.Equal(expected, layout.ColumnWidth);
        Assert.Equal((4 + columns - 1) / columns, layout.Rows);
        Assert.Equal(width, layout.Width, 8);
    }

    [Fact]
    public void ThreeWayActionsShareColumnWidthsAndRepeatTheSameLeftEdges()
    {
        var layout = SvgStudioGridMetrics.Create(288, 6, 3, 72, 6);
        Assert.Equal(3, layout.Columns); Assert.Equal(2, layout.Rows); Assert.Equal(92, layout.ColumnWidth);
        Assert.Equal(0, layout.X(0)); Assert.Equal(98, layout.X(1)); Assert.Equal(196, layout.X(2));
        Assert.Equal(layout.X(0), layout.X(3)); Assert.Equal(layout.X(1), layout.X(4));
        Assert.Equal(288, layout.X(2) + layout.ColumnWidth);
    }

    [Fact]
    public void EmptyAndSingleControlFormsDoNotReservePhantomCells()
    {
        var empty = SvgStudioGridMetrics.Create(600, 0);
        Assert.Equal(0, empty.Columns); Assert.Equal(0, empty.Rows); Assert.Equal(0, empty.Width);
        var one = SvgStudioGridMetrics.Create(600, 1);
        Assert.Equal(1, one.Columns); Assert.Equal(600, one.ColumnWidth); Assert.Equal(1, one.Rows);
    }

    [Fact]
    public void UnconstrainedMeasureReturnsFinitePreferredWidth()
    {
        var layout = SvgStudioGridMetrics.Create(double.PositiveInfinity, 7, 3, 112, 8);
        Assert.Equal(3, layout.Columns); Assert.Equal(3, layout.Rows);
        Assert.Equal(352, layout.Width); Assert.Equal(112, layout.ColumnWidth);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(17.5)]
    public void VerySmallHostsNeverProduceNegativeWidths(double width)
    {
        var layout = SvgStudioGridMetrics.Create(width, 3);
        Assert.Equal(1, layout.Columns); Assert.Equal(width, layout.ColumnWidth); Assert.Equal(3, layout.Rows);
    }

    [Fact]
    public void PartialLastRowRetainsTheSameColumnRhythm()
    {
        var layout = SvgStudioGridMetrics.Create(320, 5, 3, 80, 8);
        Assert.Equal(3, layout.Columns); Assert.Equal(2, layout.Rows);
        Assert.Equal(layout.X(1), layout.X(4));
        Assert.Equal(320, layout.Width, 8);
    }

    [Fact]
    public void BadMeasurementsAndUnboundedConfigurationAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgStudioGridMetrics.Create(double.NaN, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgStudioGridMetrics.Create(-1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgStudioGridMetrics.Create(double.NegativeInfinity, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgStudioGridMetrics.Create(300, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgStudioGridMetrics.Create(300, 4097));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgStudioGridMetrics.Create(300, 2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgStudioGridMetrics.Create(300, 2, 17));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgStudioGridMetrics.Create(300, 2, 2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgStudioGridMetrics.Create(300, 2, 2, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgStudioGridMetrics.Create(300, 2, 2, 120, double.NaN));
    }

    [Fact]
    public void InvalidCellIndicesAreNotSilentlyWrapped()
    {
        var layout = SvgStudioGridMetrics.Create(300, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.X(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.X(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgStudioGridMetrics.Create(300, 0).X(0));
    }
}
