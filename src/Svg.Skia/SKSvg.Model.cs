﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using Svg.Model;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Svg.Model.Drawables;
using ShimSkiaSharp;
using Svg.Model.Drawables.Factories;
using Svg.Model.Services;

namespace Svg.Skia;

public partial class SKSvg : IDisposable
{
    public static bool CacheOriginalStream { get; set; }

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

    public static SkiaSharp.SKPicture? ToPicture(SvgFragment svgFragment, SkiaModel skiaModel, ISvgAssetLoader assetLoader)
    {
        var picture = SvgService.ToModel(svgFragment, assetLoader, out _, out _);
        return skiaModel.ToSKPicture(picture);
    }

    public static void Draw(SkiaSharp.SKCanvas skCanvas, SvgFragment svgFragment, SkiaModel skiaModel, ISvgAssetLoader assetLoader)
    {
        var references = new HashSet<Uri> {svgFragment.OwnerDocument.BaseUri};
        var size = SvgService.GetDimensions(svgFragment);
        var bounds = SKRect.Create(size);
        var drawable = DrawableFactory.Create(svgFragment, bounds, null, assetLoader, references);
        if (drawable is { })
        {
            drawable.PostProcess(bounds, SKMatrix.Identity);
            var picture = drawable.Snapshot(bounds);
            skiaModel.Draw(picture, skCanvas);
        }
    }

    public static void Draw(SkiaSharp.SKCanvas skCanvas, string path, SkiaModel skiaModel, ISvgAssetLoader assetLoader)
    {
        var svgDocument = SvgService.Open(path);
        if (svgDocument is { })
        {
            Draw(skCanvas, svgDocument, skiaModel, assetLoader);
        }
    }

    private SvgParameters? _originalParameters;
    private string? _originalPath;
    private System.IO.Stream? _originalStream;

    public object Sync { get; } = new ();

    public SKSvgSettings Settings { get; }

    public ISvgAssetLoader AssetLoader { get; }

    public SkiaModel SkiaModel { get; }

    public SKDrawable? Drawable { get; private set; }

    public SKPicture? Model { get; private set; }

    public virtual SkiaSharp.SKPicture? Picture { get; protected set; }

    public SkiaSharp.SKPicture? WireframePicture { get; protected set; }

    private bool _wireframe;
    public bool Wireframe
    {
        get => _wireframe;
        set
        {
            _wireframe = value;
            ClearWireframePicture();
        }
    }

    private DrawAttributes _ignoreAttributes;
    public DrawAttributes IgnoreAttributes
    {
        get => _ignoreAttributes;
        set => _ignoreAttributes = value;
    }

    public void ClearWireframePicture()
    {
        WireframePicture?.Dispose();
        WireframePicture = null;
    }

    public event EventHandler<SKSvgDrawEventArgs>? OnDraw;

    protected virtual void RaiseOnDraw(SKSvgDrawEventArgs e)
    {
        OnDraw?.Invoke(this, e);
    }

    public SvgParameters? Parameters => _originalParameters;

    public SKSvg()
    {
        Settings = new SKSvgSettings();
        SkiaModel = new SkiaModel(Settings);
        AssetLoader = new SkiaSvgAssetLoader(SkiaModel);
    }

    public SkiaSharp.SKPicture? Load(System.IO.Stream stream, SvgParameters? parameters = null)
    {
        SvgDocument? svgDocument;

        if (CacheOriginalStream)
        {
            if (_originalStream != stream)
            {
                _originalStream?.Dispose();
                _originalStream = new System.IO.MemoryStream();
                stream.CopyTo(_originalStream);
            }

            _originalPath = null;
            _originalParameters = parameters;
            _originalStream.Position = 0;

            svgDocument = SvgService.Open(_originalStream, parameters);
            if (svgDocument is null)
            {
                return null;
            }
        }
        else
        {
            svgDocument = SvgService.Open(stream, parameters);
            if (svgDocument is null)
            {
                return null;
            }
        }

        Model = SvgService.ToModel(svgDocument, AssetLoader, out var drawable, out _, _ignoreAttributes);
        Drawable = drawable;
        Picture = SkiaModel.ToSKPicture(Model);
        WireframePicture?.Dispose();
        WireframePicture = null;

        return Picture;
    }

    public SkiaSharp.SKPicture? Load(System.IO.Stream stream) => Load(stream, null);

    public SkiaSharp.SKPicture? Load(string path, SvgParameters? parameters = null)
    {
        _originalPath = path;
        _originalStream?.Dispose();
        _originalStream = null;

        var svgDocument = SvgService.Open(path, parameters);
        if (svgDocument is null)
        {
            return null;
        }

        Model = SvgService.ToModel(svgDocument, AssetLoader, out var drawable, out _, _ignoreAttributes);
        Drawable = drawable;
        Picture = SkiaModel.ToSKPicture(Model);
        WireframePicture?.Dispose();
        WireframePicture = null;

        return Picture;
    }

    public SkiaSharp.SKPicture? Load(string path) => Load(path, null);

    public SkiaSharp.SKPicture? Load(XmlReader reader)
    {
        var svgDocument = SvgService.Open(reader);
        if (svgDocument is { })
        {
            Model = SvgService.ToModel(svgDocument, AssetLoader, out var drawable, out _, _ignoreAttributes);
            Drawable = drawable;
            Picture = SkiaModel.ToSKPicture(Model);
            WireframePicture?.Dispose();
            WireframePicture = null;
            return Picture;
        }
        return null;
    }

    public SkiaSharp.SKPicture? ReLoad(SvgParameters? parameters)
    {
        lock (Sync)
        {
            if (!CacheOriginalStream)
            {
                throw new ArgumentException($"Enable {nameof(CacheOriginalStream)} feature toggle to enable reload feature.");
            }

            Reset();

            _originalParameters = parameters;

            if (_originalStream == null)
            {
                return Load(_originalPath, parameters);
            }

            _originalStream.Position = 0;

            return Load(_originalStream, parameters);
        }
    }

    public SkiaSharp.SKPicture? FromSvg(string svg)
    {
        var svgDocument = SvgService.FromSvg(svg);
        if (svgDocument is { })
        {
            Model = SvgService.ToModel(svgDocument, AssetLoader, out var drawable, out _, _ignoreAttributes);
            Drawable = drawable;
            Picture = SkiaModel.ToSKPicture(Model);
            WireframePicture?.Dispose();
            WireframePicture = null;
            return Picture;
        }
        return null;
    }

    public SkiaSharp.SKPicture? FromSvgDocument(SvgDocument? svgDocument)
    {
        if (svgDocument is { })
        {
            Model = SvgService.ToModel(svgDocument, AssetLoader, out var drawable, out _, _ignoreAttributes);
            Drawable = drawable;
            Picture = SkiaModel.ToSKPicture(Model);
            WireframePicture?.Dispose();
            WireframePicture = null;
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

    public void Draw(SkiaSharp.SKCanvas canvas)
    {
        var picture = Picture;
        if (picture is null)
        {
            return;
        }

        canvas.Save();
        if (Wireframe && Model is { })
        {
            WireframePicture ??= SkiaModel.ToWireframePicture(Model);
            if (WireframePicture is { })
            {
                canvas.DrawPicture(WireframePicture);
            }
        }
        else
        {
            canvas.DrawPicture(picture);
        }
        canvas.Restore();

        RaiseOnDraw(new SKSvgDrawEventArgs(canvas));
    }

    private void Reset()
    {
        lock (Sync)
        {
            Model = null;
            Drawable = null;

            Picture?.Dispose();
            Picture = null;

            WireframePicture?.Dispose();
            WireframePicture = null;
        }
    }

    public void Dispose()
    {
        Reset();
        _originalStream?.Dispose();
    }
}
