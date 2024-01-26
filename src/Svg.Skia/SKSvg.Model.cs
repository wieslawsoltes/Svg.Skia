using System;
using Svg.Model;
using System.Collections.Generic;
using System.Xml;
using Svg.Model.Drawables;
using ShimSkiaSharp;

namespace Svg.Skia;

public class SKSvg : IDisposable
{
    public static SKSvg CreateFromStream(System.IO.Stream stream, SvgParameters? parameters = null)
    {
        var skSvg = new SKSvg();
        skSvg.Load(stream, parameters);
        return skSvg;
    }

    public static SKSvg CreateFromStream(System.IO.Stream stream) => CreateFromStream(stream, null);

    public static SKSvg CreateFromFile(string path, SvgParameters? parameters = null)
    {
        var skSvg = new SKSvg();
        skSvg.Load(path, parameters);
        return skSvg;
    }

    public static SKSvg CreateFromFile(string path) => CreateFromFile(path, null);

    public static SKSvg CreateFromXmlReader(XmlReader reader)
    {
        var skSvg = new SKSvg();
        skSvg.Load(reader);
        return skSvg;
    }

    public static SKSvg CreateFromSvg(string svg)
    {
        var skSvg = new SKSvg();
        skSvg.FromSvg(svg);
        return skSvg;
    }

    public static SKSvg CreateFromSvgDocument(SvgDocument svgDocument)
    {
        var skSvg = new SKSvg();
        skSvg.FromSvgDocument(svgDocument);
        return skSvg;
    }

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

    private SvgParameters? _originalParameters;
    private string? _originalPath;
    private System.IO.Stream? _originalStream;

    public SKSvgSettings Settings { get; }

    public IAssetLoader AssetLoader { get; }

    public SkiaModel SkiaModel { get; }

    public SKDrawable? Drawable { get; private set; }

    public SKPicture? Model { get; private set; }

    public SkiaSharp.SKPicture? Picture { get; private set; }

    public string? Style => _originalParameters?.Style;

    public Dictionary<string, string>? Entities => _originalParameters?.Entities;

    public SKSvg()
    {
        Settings = new SKSvgSettings();
        SkiaModel = new SkiaModel(Settings);
        AssetLoader = new SkiaAssetLoader(SkiaModel);
    }

    public SkiaSharp.SKPicture? Load(System.IO.Stream stream, SvgParameters? parameters = null)
    {
        Reset();

        if (_originalStream != stream)
        {
            _originalStream?.Dispose();
            _originalStream = new System.IO.MemoryStream();
            stream.CopyTo(_originalStream);
        }

        _originalPath = null;
        _originalParameters = parameters;
        _originalStream.Position = 0;

        var svgDocument = SvgExtensions.Open(_originalStream, parameters);
        if (svgDocument is null)
        {
            return null;
        }

        Model = SvgExtensions.ToModel(svgDocument, AssetLoader, out var drawable, out _);
        Drawable = drawable;
        Picture = SkiaModel.ToSKPicture(Model);

        return Picture;
    }

    public SkiaSharp.SKPicture? Load(System.IO.Stream stream) => Load(stream, null);

    public SkiaSharp.SKPicture? Load(string path, SvgParameters? parameters = null)
    {
        Reset();

        _originalPath = path;
        _originalStream?.Dispose();
        _originalStream = null;

        var svgDocument = SvgExtensions.Open(path, parameters);
        if (svgDocument is null)
        {
            return null;
        }

        Model = SvgExtensions.ToModel(svgDocument, AssetLoader, out var drawable, out _);
        Drawable = drawable;
        Picture = SkiaModel.ToSKPicture(Model);

        return Picture;
    }

    public SkiaSharp.SKPicture? Load(string path) => Load(path, null);

    public SkiaSharp.SKPicture? Load(XmlReader reader)
    {
        Reset();
        var svgDocument = SvgExtensions.Open(reader);
        if (svgDocument is { })
        {
            Model = SvgExtensions.ToModel(svgDocument, AssetLoader, out var drawable, out _);
            Drawable = drawable;
            Picture = SkiaModel.ToSKPicture(Model);
            return Picture;
        }
        return null;
    }

    public SkiaSharp.SKPicture? ReLoad(SvgParameters? parameters)
    {
        Reset();

        _originalParameters = parameters;

        if (_originalStream == null)
        {
            return Load(_originalPath, parameters);
        }

        _originalStream.Position = 0;
        
        return Load(_originalStream, parameters);
    }

    public SkiaSharp.SKPicture? FromSvg(string svg)
    {
        Reset();
        var svgDocument = SvgExtensions.FromSvg(svg);
        if (svgDocument is { })
        {
            Model = SvgExtensions.ToModel(svgDocument, AssetLoader, out var drawable, out _);
            Drawable = drawable;
            Picture = SkiaModel.ToSKPicture(Model);
            return Picture;
        }
        return null;
    }

    public SkiaSharp.SKPicture? FromSvgDocument(SvgDocument? svgDocument)
    {
        Reset();
        if (svgDocument is { })
        {
            Model = SvgExtensions.ToModel(svgDocument, AssetLoader, out var drawable, out _);
            Drawable = drawable;
            Picture = SkiaModel.ToSKPicture(Model);
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
        Model = null;
        Drawable = null;
        Picture?.Dispose();
        Picture = null;
       
    }

    public void Dispose()
    {
        Reset();
		_originalStream?.Dispose();
    }
}
