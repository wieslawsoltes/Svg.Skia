﻿using System;
using Avalonia.Platform;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

public class SvgImageTests
{
    [Fact]
    public void SvgImage_Load()
    {
        var uri = new Uri($"avares://Avalonia.Svg.Skia.UnitTests/Assets/Icon.svg");
        var assetLoader = AvaloniaLocator.Current.GetService<IAssetLoader>();

        var svgFile = assetLoader.Open(uri);
        Assert.NotNull(svgFile);

        var svgSource = new SvgSource(default(Uri));
        var picture = svgSource.Load(svgFile);
        Assert.NotNull(picture);

        var svgImage = new SvgImage() { Source = svgSource };
        Assert.NotNull(svgImage);
    }
}
