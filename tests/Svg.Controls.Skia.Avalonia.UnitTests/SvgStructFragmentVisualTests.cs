using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using SkiaSharp;
using Svg.Skia;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

public class SvgStructFragmentVisualTests
{
    [AvaloniaFact]
    public void SvgImage_CaptureMatchesStructFragmentReferenceLayout()
    {
        var svgPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "externals",
            "W3C_SVG_11_TestSuite",
            "W3C_SVG_11_TestSuite",
            "svg",
            "struct-frag-04-t.svg"));

        using var svg = new SKSvg();
        svg.Settings.StandaloneViewport = SKRect.Create(0f, 0f, 480f, 360f);
        using var _ = svg.Load(svgPath);
        var source = new SvgSource(default(Uri)) { Picture = svg.Picture };
        Assert.NotNull(source.Picture);
        Assert.Equal(480f, source.Picture!.CullRect.Width);
        Assert.Equal(360f, source.Picture.CullRect.Height);

        var image = new Image
        {
            Source = new SvgImage { Source = source },
            Width = source.Picture.CullRect.Width,
            Height = source.Picture.CullRect.Height,
            Stretch = Stretch.None
        };

        var window = new Window
        {
            Width = source.Picture.CullRect.Width,
            Height = source.Picture.CullRect.Height,
            Background = Brushes.White,
            Content = image
        };

        window.Show();

        var artifact = SavePng(window, nameof(SvgImage_CaptureMatchesStructFragmentReferenceLayout));

        Assert.True(File.Exists(artifact.Path));
        Assert.Equal(480, artifact.Width);
        Assert.Equal(360, artifact.Height);
        Assert.True(artifact.Bytes > 0);

        using var bitmap = SKBitmap.Decode(artifact.Path);
        Assert.NotNull(bitmap);

        var background = bitmap!.GetPixel(260, 180);
        var crimson = bitmap.GetPixel(110, 110);
        var gold = bitmap.GetPixel(175, 125);

        Assert.True(background.Red > 240 && background.Green > 240 && background.Blue > 240);
        Assert.True(crimson.Red > 180 && crimson.Green < 80 && crimson.Blue < 120);
        Assert.True(gold.Red > 200 && gold.Green > 150 && gold.Blue < 80);

        window.Close();
    }

    private static ScreenshotArtifact SavePng(TopLevel topLevel, string fileName)
    {
        var frame = topLevel.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("No rendered frame was captured.");

        var outputRoot = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            outputRoot = Path.Combine(AppContext.BaseDirectory, "headless-screenshots");
        }

        Directory.CreateDirectory(outputRoot);

        var safeName = fileName.Replace(' ', '-').Replace('/', '-').Replace('\\', '-');
        var path = Path.GetFullPath(Path.Combine(outputRoot, $"{safeName}.png"));
        frame.Save(path);

        var info = new FileInfo(path);
        return new ScreenshotArtifact(path, frame.PixelSize.Width, frame.PixelSize.Height, info.Length);
    }

    private sealed record ScreenshotArtifact(string Path, int Width, int Height, long Bytes);
}
