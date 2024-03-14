using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Svg.Skia;
using Xunit;

namespace Avalonia.Svg.Skia.UiTests;

public class SvgImageReloadTests
{
    private Image _test;
    private string _css = ".Black { fill: #FF0000; }";
    private Window _window;

    [Fact]
    public async void SvgImage_ReLoad()
    {
        SKSvg.CacheOriginalStream = true;
        var uri = new Uri($"avares://Avalonia.Svg.Skia.UiTests/Assets/__tiger.svg");
        var assetLoader = new StandardAssetLoader();
        var svgFile = assetLoader.Open(uri);
        var svgSource = SvgSource.LoadFromStream(svgFile);
        var svgImage = new SvgImage() { Source = svgSource };

        _test = new Image {Source = svgImage};

        _window = new Window {Content = _test};
        _window.Show();

        var timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromMilliseconds(10);
        timer.Tick += Timer_Tick;
        timer.Start();

        await Task.Delay(10000);
        
        timer?.Stop();
        _window?.Close();
        SKSvg.CacheOriginalStream = false;
    }

    private void Timer_Tick(object sender, EventArgs e)
    {
        var image = (SvgImage)_test.Source;
        (image.CurrentCss, _css) = (_css, image.CurrentCss);
        _test.InvalidateVisual();
    }
}
