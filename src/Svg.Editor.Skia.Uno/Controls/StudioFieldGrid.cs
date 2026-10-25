using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Svg.Editor.Skia;
using Windows.Foundation;

namespace Svg.Editor.Skia.Uno;

/// <summary>
/// Equal-width form columns with content-sized rows. Narrow docks stack fields before
/// labels become unusable. Measure and arrange use the same pure metrics; child identity,
/// focus and bindings survive every breakpoint. Collapsed children consume no cell.
/// </summary>
public sealed class StudioFieldGrid : Panel
{
    public static readonly DependencyProperty MaximumColumnsProperty = DependencyProperty.Register(nameof(MaximumColumns), typeof(int), typeof(StudioFieldGrid), new PropertyMetadata(2, Changed));
    public static readonly DependencyProperty MinimumColumnWidthProperty = DependencyProperty.Register(nameof(MinimumColumnWidth), typeof(double), typeof(StudioFieldGrid), new PropertyMetadata(144d, Changed));
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(nameof(Spacing), typeof(double), typeof(StudioFieldGrid), new PropertyMetadata(8d, Changed));
    public int MaximumColumns { get => (int)GetValue(MaximumColumnsProperty); set => SetValue(MaximumColumnsProperty, value); }
    public double MinimumColumnWidth { get => (double)GetValue(MinimumColumnWidthProperty); set => SetValue(MinimumColumnWidthProperty, value); }
    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }
    public StudioFieldGrid() => HorizontalAlignment = HorizontalAlignment.Stretch;
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((StudioFieldGrid)sender).InvalidateMeasure();
    private SvgStudioGridMetrics Metrics(double width, int count)
        => SvgStudioGridMetrics.Create(Math.Max(0, width), count, MaximumColumns, MinimumColumnWidth, Spacing);
    protected override Size MeasureOverride(Size availableSize)
    {
        var children = Children.Where(child => child.Visibility != Visibility.Collapsed).ToArray();
        var metrics = Metrics(availableSize.Width, children.Length);
        var rows = new double[metrics.Rows];
        for (var index = 0; index < children.Length; index++)
        {
            children[index].Measure(new Size(metrics.ColumnWidth, double.PositiveInfinity));
            rows[index / metrics.Columns] = Math.Max(rows[index / metrics.Columns], children[index].DesiredSize.Height);
        }
        return new Size(metrics.Width, rows.Sum() + Math.Max(0, metrics.Rows - 1) * metrics.Gap);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = Children.Where(child => child.Visibility != Visibility.Collapsed).ToArray();
        var metrics = Metrics(finalSize.Width, children.Length);
        var heights = new double[metrics.Rows];
        for (var index = 0; index < children.Length; index++)
            heights[index / metrics.Columns] = Math.Max(heights[index / metrics.Columns], children[index].DesiredSize.Height);
        var top = 0d;
        for (var row = 0; row < metrics.Rows; row++)
        {
            for (var column = 0; column < metrics.Columns; column++)
            {
                var index = row * metrics.Columns + column;
                if (index >= children.Length) break;
                children[index].Arrange(new Rect(metrics.X(index), top, metrics.ColumnWidth, heights[row]));
            }
            top += heights[row] + metrics.Gap;
        }
        return finalSize;
    }
}
