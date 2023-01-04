#if USE_SKIASHARP
using System;
using Svg.Model;

namespace Svg.Skia;

public class SKSvg : IDisposable
{
    public static SkiaSharp.SKPicture? ToPicture(SvgFragment svgFragment, IAssetLoader assetLoader)
    {
        return SvgExtensions.ToPicture(svgFragment, assetLoader, out _, out _);
    }

    public SKSvgSettings Settings { get; }

    public IAssetLoader AssetLoader { get; }

    public SkiaSharp.SKPicture? Picture { get; private set; }

    public SKSvg()
    {
        Settings = new SKSvgSettings();
        AssetLoader = new SkiaAssetLoader();
    }

    public SkiaSharp.SKPicture? Load(System.IO.Stream stream)
    {
        Reset();
        var svgDocument = SvgExtensions.Open(stream);
        if (svgDocument is { })
        {
            Picture = SvgExtensions.ToPicture(svgDocument, AssetLoader, out var _, out _);
            return Picture;
        }
        return null;
    }

    public SkiaSharp.SKPicture? Load(string path)
    {
        Reset();
        var svgDocument = SvgExtensions.Open(path);
        if (svgDocument is { })
        {
            Picture = SvgExtensions.ToPicture(svgDocument, AssetLoader, out var _, out _);
            return Picture;
        }
        return null;
    }

    public SkiaSharp.SKPicture? FromSvg(string svg)
    {
        Reset();
        var svgDocument = SvgExtensions.FromSvg(svg);
        if (svgDocument is { })
        {
            Picture = SvgExtensions.ToPicture(svgDocument, AssetLoader, out var _, out _);
            return Picture;
        }
        return null;
    }

    public SkiaSharp.SKPicture? FromSvgDocument(SvgDocument? svgDocument)
    {
        Reset();
        if (svgDocument is { })
        {
            Picture = SvgExtensions.ToPicture(svgDocument, AssetLoader, out var _, out _);
            return Picture;
        }
        return null;
    }

    public bool Save(System.IO.Stream stream, SkiaSharp.SKColor background, SkiaSharp.SKEncodedImageFormat format = SkiaSharp.SKEncodedImageFormat.Png, int quality = 100, float scaleX = 1f, float scaleY = 1f)
    {
        if (Picture is { })
        {
            return Picture.ToImage(stream, background, format, quality, scaleX, scaleY, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul, Settings.Srgb);
        }
        return false;
    }

    public bool Save(string path, SkiaSharp.SKColor background, SkiaSharp.SKEncodedImageFormat format = SkiaSharp.SKEncodedImageFormat.Png, int quality = 100, float scaleX = 1f, float scaleY = 1f)
    {
        if (Picture is { })
        {
            using var stream = System.IO.File.OpenWrite(path);
            return Picture.ToImage(stream, background, format, quality, scaleX, scaleY, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul, Settings.Srgb);
        }
        return false;
    }

    private void Reset()
    {
        Picture?.Dispose();
        Picture = null;
    }

    public void Dispose()
    {
        Reset();
    }
}
#endif
