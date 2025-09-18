using Avalonia.Headless.XUnit;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using Avalonia.Svg.Skia.UnitTests.Views;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

public class SvgImageTests
{
    [AvaloniaFact]
    public void SvgImage_Load()
    {
        var uri = new System.Uri($"avares://{typeof(SvgImageTests).Assembly.GetName().Name}/Assets/Icon.svg");
        using var stream = Avalonia.Platform.AssetLoader.Open(uri);
        var svgSource = SvgSource.LoadFromStream(stream);
        Assert.NotNull(svgSource);

        var svgImage = new SvgImage() { Source = svgSource };
        Assert.NotNull(svgImage);
    }

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
