using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Svg;
using Avalonia.Svg.UnitTests.Views;
using Xunit;

namespace Avalonia.Svg.UnitTests;

public class SvgImageTests
{
    [AvaloniaFact]
    public void SvgImageExtension_Returns_VisualBrush_For_Brush_Property()
    {
        var view = new SvgImageBackgroundView();
        var host = Assert.IsType<Border>(view.BackgroundHost);

        var brush = Assert.IsType<VisualBrush>(host.Background);
        var image = Assert.IsType<Image>(brush.Visual);
        var svgImage = Assert.IsType<SvgImage>(image.Source);
        Assert.NotNull(svgImage.Source);
    }

    [AvaloniaFact]
    public void SvgBrushResource_Returns_VisualBrush()
    {
        var view = new SvgBrushBackgroundView();
        var host = Assert.IsType<Border>(view.BackgroundHost);

        var brush = Assert.IsType<VisualBrush>(host.Background);
        var image = Assert.IsType<Image>(brush.Visual);
        var svgImage = Assert.IsType<SvgImage>(image.Source);
        Assert.NotNull(svgImage.Source);
        Assert.Equal(Stretch.UniformToFill, brush.Stretch);
        Assert.Equal(AlignmentX.Center, brush.AlignmentX);
        Assert.Equal(AlignmentY.Bottom, brush.AlignmentY);
        Assert.Equal(TileMode.Tile, brush.TileMode);
        Assert.Equal(RelativeUnit.Absolute, brush.DestinationRect.Unit);
        Assert.Equal(RelativeUnit.Absolute, brush.SourceRect.Unit);
        Assert.Equal(0.1, brush.DestinationRect.Rect.X, 6);
        Assert.Equal(0.2, brush.DestinationRect.Rect.Y, 6);
        Assert.Equal(0.6, brush.DestinationRect.Rect.Width, 6);
        Assert.Equal(0.7, brush.DestinationRect.Rect.Height, 6);
        Assert.Equal(0.05, brush.SourceRect.Rect.X, 6);
        Assert.Equal(0.05, brush.SourceRect.Rect.Y, 6);
        Assert.Equal(0.9, brush.SourceRect.Rect.Width, 6);
        Assert.Equal(0.9, brush.SourceRect.Rect.Height, 6);
        Assert.Equal(0.5, brush.Opacity);
        var matrixTransform = Assert.IsType<MatrixTransform>(brush.Transform);
        Assert.Equal(new Matrix(1, 0, 0, 1, 10, 20), matrixTransform.Matrix);
        Assert.Equal(RelativeUnit.Absolute, brush.TransformOrigin.Unit);
        Assert.Equal(0.25, brush.TransformOrigin.Point.X, 6);
        Assert.Equal(0.75, brush.TransformOrigin.Point.Y, 6);
    }
}
