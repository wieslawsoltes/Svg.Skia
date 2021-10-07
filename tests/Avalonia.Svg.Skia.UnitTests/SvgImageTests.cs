using System;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Shared.PlatformSupport;
using Avalonia.Svg.Skia;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

public class SvgImageTests
{
    [Fact]
    public void SvgImage_Load()
    {
        var uri = new Uri($"avares://Avalonia.Svg.Skia.UnitTests/Assets/Icon.svg");
        var assetLoader = new AssetLoader(); // AvaloniaLocator.Current.GetService<IAssetLoader>()

        var svgFile = assetLoader.Open(uri);
        Assert.NotNull(svgFile);

        var svgSource = new SvgSource();
        var picture = svgSource.Load(svgFile);
        Assert.NotNull(picture);

        var svgImage = new SvgImage() { Source = svgSource };
        Assert.NotNull(svgImage);
    }
}
