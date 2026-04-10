using System.IO;
using System.Threading.Tasks;
using SkiaSharp;
using Svg.Skia;

namespace TestApp.Services;

public static class TestAppExportService
{
    public static Task ExportAsync(
        Stream stream,
        string name,
        SKPicture? picture,
        string backgroundColor = "#00FFFFFF",
        float scaleX = 1f,
        float scaleY = 1f)
    {
        if (picture is null)
        {
            return Task.CompletedTask;
        }

        if (!SKColor.TryParse(backgroundColor, out var skBackgroundColor))
        {
            return Task.CompletedTask;
        }

        switch (Path.GetExtension(name).ToLowerInvariant())
        {
            case ".png":
                picture.ToImage(
                    stream,
                    skBackgroundColor,
                    SKEncodedImageFormat.Png,
                    100,
                    scaleX,
                    scaleY,
                    SKColorType.Rgba8888,
                    SKAlphaType.Premul,
                    SKColorSpace.CreateSrgb());
                break;
            case ".jpg":
            case ".jpeg":
                picture.ToImage(
                    stream,
                    skBackgroundColor,
                    SKEncodedImageFormat.Jpeg,
                    100,
                    scaleX,
                    scaleY,
                    SKColorType.Rgba8888,
                    SKAlphaType.Premul,
                    SKColorSpace.CreateSrgb());
                break;
            case ".pdf":
                picture.ToPdf(stream, skBackgroundColor, scaleX, scaleY);
                break;
            case ".xps":
                picture.ToXps(stream, skBackgroundColor, scaleX, scaleY);
                break;
        }

        return Task.CompletedTask;
    }
}
