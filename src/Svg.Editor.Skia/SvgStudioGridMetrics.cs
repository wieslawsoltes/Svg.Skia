using System;

namespace Svg.Editor.Skia;

/// <summary>Framework-independent measurements for an equal-width responsive form grid.</summary>
public readonly struct SvgStudioGridMetrics
{
    private SvgStudioGridMetrics(int columns, int rows, double width, double gap)
    { Columns = columns; Rows = rows; ColumnWidth = width; Gap = gap; }
    public int Columns { get; }
    public int Rows { get; }
    public double ColumnWidth { get; }
    public double Gap { get; }
    public double Width => Columns == 0 ? 0 : Columns * ColumnWidth + (Columns - 1) * Gap;
    public double X(int index)
    {
        if (index < 0 || Columns == 0 || index >= Rows * Columns) throw new ArgumentOutOfRangeException(nameof(index));
        return (index % Columns) * (ColumnWidth + Gap);
    }
    public static SvgStudioGridMetrics Create(double availableWidth, int count, int maximumColumns = 2,
        double minimumColumnWidth = 144, double gap = 8)
    {
        if (double.IsNaN(availableWidth) || double.IsNegativeInfinity(availableWidth) || availableWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(availableWidth));
        if (count < 0 || count > 4096) throw new ArgumentOutOfRangeException(nameof(count));
        if (maximumColumns < 1 || maximumColumns > 16) throw new ArgumentOutOfRangeException(nameof(maximumColumns));
        if (!Finite(minimumColumnWidth) || minimumColumnWidth <= 0 || minimumColumnWidth > 1000000)
            throw new ArgumentOutOfRangeException(nameof(minimumColumnWidth));
        if (!Finite(gap) || gap < 0 || gap > 1000000) throw new ArgumentOutOfRangeException(nameof(gap));
        if (count == 0) return new SvgStudioGridMetrics(0, 0, 0, gap);
        var maximum = Math.Min(maximumColumns, count);
        var width = double.IsPositiveInfinity(availableWidth)
            ? maximum * minimumColumnWidth + (maximum - 1) * gap : availableWidth;
        // Compare before converting a potentially enormous double to int.
        var capacity = (width + gap) / (minimumColumnWidth + gap);
        var columns = capacity >= maximum ? maximum : Math.Max(1, (int)Math.Floor(capacity));
        var columnWidth = Math.Max(0, (width - (columns - 1) * gap) / columns);
        return new SvgStudioGridMetrics(columns, (count + columns - 1) / columns, columnWidth, gap);
    }
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
