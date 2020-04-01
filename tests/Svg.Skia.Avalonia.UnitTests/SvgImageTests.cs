using System;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Shared.PlatformSupport;
using Svg.Skia.Avalonia;
using Xunit;

namespace Svg.Skia.Avalonia.UnitTests
{
    public class SvgImageTests
    {
        [Fact]
        public void SvgImage_Load()
        {
            var uri = new Uri($"avares://Svg.Skia.Avalonia.UnitTests/Assets/Icon.svg");
            var assetLoader = new AssetLoader(); // AvaloniaLocator.Current.GetService<IAssetLoader>()

            var svgFile = assetLoader.Open(uri);
            Assert.NotNull(svgFile);

            var svgSkia = new SvgSkia();
            var picture = svgSkia.Load(svgFile);
            Assert.NotNull(picture);

            var image = new SvgImage() { Source = svgSkia };
            Assert.NotNull(image);
        }
    }
}
