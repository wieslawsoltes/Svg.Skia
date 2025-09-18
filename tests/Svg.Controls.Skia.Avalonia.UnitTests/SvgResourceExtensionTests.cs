using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

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

    [AvaloniaFact]
    public void CreateBrush_From_Path_Applies_Options()
    {
        var assemblyName = typeof(SvgResourceExtensionTests).Assembly.GetName().Name;
        var path = $"avares://{assemblyName}/Assets/Icon.svg";

        var transform = new MatrixTransform(new Matrix(1, 0, 0, 1, 5, 10));
        var destination = new RelativeRect(0, 0, 1, 1, RelativeUnit.Relative);
        var source = new RelativeRect(0, 0, 0.5, 0.5, RelativeUnit.Relative);
        var transformOrigin = new RelativePoint(0.25, 0.75, RelativeUnit.Relative);

        var brush = SvgResourceExtension.CreateBrush(
            path,
            css: ".root { fill: red; }",
            currentCss: ".current { stroke: blue; }",
            stretch: Stretch.Fill,
            alignmentX: AlignmentX.Right,
            alignmentY: AlignmentY.Bottom,
            tileMode: TileMode.FlipX,
            destinationRect: destination,
            sourceRect: source,
            opacity: 0.75,
            transform: transform,
            transformOrigin: transformOrigin);

        var visualBrush = Assert.IsType<VisualBrush>(brush);
        Assert.Equal(Stretch.Fill, visualBrush.Stretch);
        Assert.Equal(AlignmentX.Right, visualBrush.AlignmentX);
        Assert.Equal(AlignmentY.Bottom, visualBrush.AlignmentY);
        Assert.Equal(TileMode.FlipX, visualBrush.TileMode);
        Assert.Equal(destination, visualBrush.DestinationRect);
        Assert.Equal(source, visualBrush.SourceRect);
        Assert.Equal(0.75, visualBrush.Opacity, 6);
        Assert.Same(transform, visualBrush.Transform);
        Assert.Equal(transformOrigin, visualBrush.TransformOrigin);

        var image = Assert.IsType<Image>(visualBrush.Visual);
        var svgImage = Assert.IsType<SvgImage>(image.Source);
        Assert.Equal(".root { fill: red; }", svgImage.Css);
        Assert.Equal(".current { stroke: blue; }", svgImage.CurrentCss);
    }
}
