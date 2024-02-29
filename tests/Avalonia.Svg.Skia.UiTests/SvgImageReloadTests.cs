using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Svg.Skia;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

public class SvgImageReloadTests
{
    Image test;
    private string css = ".Black { fill: #FF0000; }";
    Window window;
    [Fact]
    public async void SvgImage_ReLoad()
    {
        SKSvg.CacheOriginalStream = true;
        var uri = new Uri($"avares://Avalonia.Svg.Skia.UnitTests/Assets/__tiger.svg");
        var assetLoader = new StandardAssetLoader(); // AvaloniaLocator.Current.GetService<IAssetLoader>()

        var svgFile = assetLoader.Open(uri);

        var svgSource = SvgSource.LoadFromStream(svgFile);
        var svgImage = new SvgImage() { Source = svgSource };

        test = new Image();
        test.Source = svgImage;

        window = new Window();
        window.Content = test;
        window.Show();

        var timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromMilliseconds(10);
        timer.Tick += Timer_Tick;
        timer.Start();

        await Task.Delay(10000);
        
        timer?.Stop();
        window?.Close();
        SKSvg.CacheOriginalStream = false;
    }



    private void Timer_Tick(object sender, EventArgs e)
    {
        var image = (SvgImage)test.Source;
        (image.CurrentCss, css) = (css, image.CurrentCss);
        test.InvalidateVisual();
    }
}
