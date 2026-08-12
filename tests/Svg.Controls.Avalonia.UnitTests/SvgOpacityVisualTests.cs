using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Svg;
using Xunit;

namespace Avalonia.Svg.UnitTests;

public class SvgOpacityVisualTests
{
    [AvaloniaFact]
    public void SvgImage_AppliesOpacityToIndividualElement()
    {
        using var bitmap = RenderSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="40">
              <rect x="0" y="0" width="40" height="40" fill="black" opacity="0"/>
              <rect x="40" y="0" width="40" height="40" fill="black" opacity="0.5"/>
            </svg>
            """);

        AssertColor(GetPixel(bitmap, 20, 20), 255, 6);
        AssertColor(GetPixel(bitmap, 60, 20), 127, 6);
    }

    [AvaloniaFact]
    public void SvgImage_CompositesGroupBeforeApplyingOpacity()
    {
        using var bitmap = RenderSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="40">
              <g opacity="0.5">
                <rect x="0" y="0" width="60" height="40" fill="black"/>
                <rect x="20" y="0" width="60" height="40" fill="black"/>
              </g>
            </svg>
            """);

        var single = GetPixel(bitmap, 10, 20);
        var overlap = GetPixel(bitmap, 40, 20);

        AssertColor(single, 127, 6);
        AssertColor(overlap, 127, 6);
    }

    private static WriteableBitmap RenderSvg(string svg)
    {
        var source = SvgSource.LoadFromSvg(svg);
        Assert.NotNull(source.Picture);

        var image = new Image
        {
            Source = new SvgImage { Source = source },
            Width = source.Picture!.CullRect.Width,
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
        var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("No rendered frame was captured.");
        window.Close();
        return frame;
    }

    private static Pixel GetPixel(WriteableBitmap bitmap, int x, int y)
    {
        using var framebuffer = bitmap.Lock();
        var offset = y * framebuffer.RowBytes + x * framebuffer.Format.BitsPerPixel / 8;
        if (framebuffer.Format == PixelFormat.Bgra8888)
        {
            return new Pixel(
                Marshal.ReadByte(framebuffer.Address, offset + 2),
                Marshal.ReadByte(framebuffer.Address, offset + 1),
                Marshal.ReadByte(framebuffer.Address, offset));
        }

        Assert.Equal(PixelFormat.Rgba8888, framebuffer.Format);
        return new Pixel(
            Marshal.ReadByte(framebuffer.Address, offset),
            Marshal.ReadByte(framebuffer.Address, offset + 1),
            Marshal.ReadByte(framebuffer.Address, offset + 2));
    }

    private static void AssertColor(Pixel color, byte expected, byte tolerance)
    {
        Assert.InRange(color.Red, expected - tolerance, expected + tolerance);
        Assert.InRange(color.Green, expected - tolerance, expected + tolerance);
        Assert.InRange(color.Blue, expected - tolerance, expected + tolerance);
    }

    private readonly record struct Pixel(byte Red, byte Green, byte Blue);
}
