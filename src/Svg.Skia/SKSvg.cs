using System;
using Svg.Model;
#if !USE_SKIASHARP
using System.Collections.Generic;
using Svg.Model.Drawables;
using ShimSkiaSharp;
#endif

namespace Svg.Skia;

public class SKSvg : IDisposable
{
#if USE_SKIASHARP
    public static SkiaSharp.SKPicture? ToPicture(SvgFragment svgFragment, IAssetLoader assetLoader)
    {
        return SvgExtensions.ToPicture(svgFragment, assetLoader, out _, out _);
    }
#else
    public static SkiaSharp.SKPicture? ToPicture(SvgFragment svgFragment, SkiaModel skiaModel, IAssetLoader assetLoader)
    {
        var picture = SvgExtensions.ToModel(svgFragment, assetLoader, out _, out _);
        return skiaModel.ToSKPicture(picture);
    }

    public static void Draw(SkiaSharp.SKCanvas skCanvas, SvgFragment svgFragment, SkiaModel skiaModel, IAssetLoader assetLoader)
    {
        var references = new HashSet<Uri> {svgFragment.OwnerDocument.BaseUri};
        var size = SvgExtensions.GetDimensions(svgFragment);
        var bounds = SKRect.Create(size);
        var drawable = DrawableFactory.Create(svgFragment, bounds, null, assetLoader, references);
        if (drawable is { })
        {
            drawable.PostProcess(bounds, SKMatrix.Identity);
            var picture = drawable.Snapshot(bounds);
            skiaModel.Draw(picture, skCanvas);
        }
    }

    public static void Draw(SkiaSharp.SKCanvas skCanvas, string path, SkiaModel skiaModel, IAssetLoader assetLoader)
    {
        var svgDocument = SvgExtensions.Open(path);
        if (svgDocument is { })
        {
            Draw(skCanvas, svgDocument, skiaModel, assetLoader);
        }
    }
#endif

    public SKSvgSettings Settings { get; }

    public IAssetLoader AssetLoader { get; }

#if !USE_SKIASHARP
    public SkiaModel SkiaModel { get; }

    public SKDrawable? Drawable { get; private set; }

    public SKPicture? Model { get; private set; }
#endif

    public SkiaSharp.SKPicture? Picture { get; private set; }

    public SKSvg()
    {
        Settings = new SKSvgSettings();
#if USE_SKIASHARP
        AssetLoader = new SkiaAssetLoader();
#else
        SkiaModel = new SkiaModel(Settings);
        AssetLoader = new SkiaAssetLoader(SkiaModel);
#endif
    }

    public SkiaSharp.SKPicture? Load(System.IO.Stream stream)
    {
        Reset();
        var svgDocument = SvgExtensions.Open(stream);
        if (svgDocument is { })
        {
#if USE_SKIASHARP
            Picture = SvgExtensions.ToPicture(svgDocument, AssetLoader, out var _, out _);
#else
            Model = SvgExtensions.ToModel(svgDocument, AssetLoader, out var drawable, out _);
            Drawable = drawable;
            Picture = SkiaModel.ToSKPicture(Model);
#endif
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
#if USE_SKIASHARP
            Picture = SvgExtensions.ToPicture(svgDocument, AssetLoader, out var _, out _);
#else
            Model = SvgExtensions.ToModel(svgDocument, AssetLoader, out var drawable, out _);
            Drawable = drawable;
            Picture = SkiaModel.ToSKPicture(Model);
#endif
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
#if USE_SKIASHARP
            Picture = SvgExtensions.ToPicture(svgDocument, AssetLoader, out var _, out _);
#else
            Model = SvgExtensions.ToModel(svgDocument, AssetLoader, out var drawable, out _);
            Drawable = drawable;
            Picture = SkiaModel.ToSKPicture(Model);
#endif
            return Picture;
        }
        return null;
    }

    public SkiaSharp.SKPicture? FromSvgDocument(SvgDocument? svgDocument)
    {
        Reset();
        if (svgDocument is { })
        {
#if USE_SKIASHARP
            Picture = SvgExtensions.ToPicture(svgDocument, AssetLoader, out var _, out _);
#else
            Model = SvgExtensions.ToModel(svgDocument, AssetLoader, out var drawable, out _);
            Drawable = drawable;
            Picture = SkiaModel.ToSKPicture(Model);
#endif
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
#if !USE_SKIASHARP
        Model = null;
        Drawable = null;
#endif
        Picture?.Dispose();
        Picture = null;
    }

    public void Dispose()
    {
        Reset();
    }
}
