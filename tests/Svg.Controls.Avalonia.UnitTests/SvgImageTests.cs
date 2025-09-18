using Avalonia.Headless.XUnit;
using Avalonia.Controls;
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
    }
}
