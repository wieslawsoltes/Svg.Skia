using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using SkiaSharp;
using Xunit;

namespace Avalonia.Svg.Skia.UnitTests;

public class SvgOpacityVisualTests
{
    [AvaloniaFact]
    public void SvgImage_CaptureMatchesOpacitySampleSemantics()
    {
        using var source = SvgSource.LoadFromSvg(OpacitySvg);
        Assert.NotNull(source.Picture);
        Assert.True(source.Picture!.CullRect.Width > 0f);
        Assert.True(source.Picture.CullRect.Height > 0f);

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

        var artifact = SavePng(window, nameof(SvgImage_CaptureMatchesOpacitySampleSemantics));

        Assert.True(File.Exists(artifact.Path));
        Assert.True(artifact.Width > 0);
        Assert.True(artifact.Height > 0);
        Assert.True(artifact.Bytes > 0);

        using var bitmap = SKBitmap.Decode(artifact.Path);
        Assert.NotNull(bitmap);
        AssertOpacitySemantics(bitmap!);

        window.Close();
    }

    private static void AssertOpacitySemantics(SKBitmap bitmap)
    {
        var topLeftSingle = bitmap.GetPixel(15, 15);
        var topLeftOverlap = bitmap.GetPixel(35, 35);
        var topRightSingle = bitmap.GetPixel(95, 15);
        var topRightOverlap = bitmap.GetPixel(115, 35);
        var bottomLeftSingle = bitmap.GetPixel(15, 95);
        var bottomLeftOverlap = bitmap.GetPixel(35, 115);
        var bottomRightSingle = bitmap.GetPixel(95, 95);
        var bottomRightOverlap = bitmap.GetPixel(115, 115);

        Assert.True(topLeftSingle.Red < 220);
        Assert.True(topRightSingle.Red < 220);
        Assert.True(bottomLeftSingle.Red < 220);
        Assert.True(bottomRightSingle.Red < 220);

        Assert.True(topLeftOverlap.Red + 40 < topLeftSingle.Red);
        Assert.True(topRightOverlap.Red + 40 < topRightSingle.Red);
        Assert.True(bottomLeftOverlap.Red + 40 < bottomLeftSingle.Red);
        Assert.InRange(Math.Abs(bottomRightOverlap.Red - bottomRightSingle.Red), 0, 6);
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

    private const string OpacitySvg = """
        <?xml version="1.0" encoding="UTF-8" standalone="no"?>
        <svg height="200" xmlns="http://www.w3.org/2000/svg" version="1.1">
          <g transform="translate(0, 0)">
            <rect x="10" y="10" width="40" height="40" fill-opacity="0.5"/>
            <rect x="30" y="30" width="40" height="40" fill-opacity="0.5"/>
          </g>
          <g transform="translate(80, 0)" fill-opacity="0.5">
            <rect x="10" y="10" width="40" height="40"/>
            <rect x="30" y="30" width="40" height="40"/>
          </g>
          <g transform="translate(0, 80)">
            <rect x="10" y="10" width="40" height="40" opacity="0.5"/>
            <rect x="30" y="30" width="40" height="40" opacity="0.5"/>
          </g>
          <g transform="translate(80, 80)" opacity="0.5">
            <rect x="10" y="10" width="40" height="40"/>
            <rect x="30" y="30" width="40" height="40"/>
          </g>
          <text transform="translate(170,45)">fill-opacity</text>
          <text transform="translate(170,125)">opacity</text>
          <text transform="translate(10,175)">applied to</text>
          <text transform="translate(0,190)">each element</text>
          <text transform="translate(90,175)">applied to</text>
          <text transform="translate(103,190)">group</text>
        </svg>
        """;
}
