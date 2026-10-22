using System;
using SkiaSharp;
using Svg.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgRenderSnapshotServiceTests
{
    [Fact]
    public void SnapshotIncludesAnimatedLayersAndRetainsItsCapturedFrame()
    {
        SKPicture snapshot;
        using (var renderer = new SKSvg())
        {
            renderer.FromSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='50'><rect x='0' y='0' width='100' height='50' fill='white'/><rect x='0' y='10' width='20' height='20' fill='red'><animate attributeName='x' values='0;80' dur='2s' fill='freeze'/></rect></svg>");
            renderer.SetAnimationTime(TimeSpan.FromSeconds(1));
            snapshot = SvgRenderSnapshotService.Capture(renderer);
            renderer.SetAnimationTime(TimeSpan.FromSeconds(2));
            using var later = SvgRenderSnapshotService.Capture(renderer);
            using var bitmap = Render(later);
            Assert.Equal(SKColors.Red, bitmap.GetPixel(90, 20));
            Assert.Equal(SKColors.White, bitmap.GetPixel(50, 20));
        }
        using (snapshot)
        using (var bitmap = Render(snapshot))
        {
            Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 20));
            Assert.Equal(SKColors.White, bitmap.GetPixel(90, 20));
        }
    }

    [Fact]
    public void SnapshotDoesNotChangeDocumentOrAnimationTime()
    {
        using var renderer = new SKSvg();
        renderer.FromSvg("<svg xmlns='http://www.w3.org/2000/svg' width='40' height='40'><circle cx='20' cy='20' r='10' fill='blue'><animate attributeName='opacity' values='0;1' dur='2s'/></circle></svg>");
        renderer.SetAnimationTime(TimeSpan.FromSeconds(1));
        var source = renderer.SourceDocument!.GetXML();
        using var snapshot = SvgRenderSnapshotService.Capture(renderer);
        Assert.Equal(source, renderer.SourceDocument.GetXML());
        Assert.Equal(TimeSpan.FromSeconds(1), renderer.AnimationTime);
        using var bitmap = Render(snapshot);
        Assert.InRange(bitmap.GetPixel(20, 20).Alpha, (byte)125, (byte)130);
    }

    private static SKBitmap Render(SKPicture picture)
    {
        var bitmap = new SKBitmap((int)picture.CullRect.Width, (int)picture.CullRect.Height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(picture);
        return bitmap;
    }
}
