using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Svg;
using Xunit;

namespace Avalonia.Svg.UnitTests;

public class SvgResourceExtensionTests
{
    [AvaloniaFact]
    public void ToBrush_Creates_VisualBrush_With_SvgImage()
    {
        var assemblyName = typeof(SvgResourceExtensionTests).Assembly.GetName().Name;
        var path = $"avares://{assemblyName}/Assets/Icon.svg";
        var extension = new SvgResourceExtension(path);

        var brush = extension.ToBrush();

        var visualBrush = Assert.IsType<VisualBrush>(brush);
        var image = Assert.IsType<Image>(visualBrush.Visual);
        var svgImage = Assert.IsType<SvgImage>(image.Source);
        Assert.NotNull(svgImage.Source);
    }

    [AvaloniaFact]
    public void Implicit_Conversion_To_Brush_Returns_VisualBrush()
    {
        var assemblyName = typeof(SvgResourceExtensionTests).Assembly.GetName().Name;
        var path = $"avares://{assemblyName}/Assets/Icon.svg";
        var extension = new SvgResourceExtension(path);

        Brush brush = extension;

        var visualBrush = Assert.IsType<VisualBrush>(brush);
        var image = Assert.IsType<Image>(visualBrush.Visual);
        Assert.IsType<SvgImage>(image.Source);
    }
}
