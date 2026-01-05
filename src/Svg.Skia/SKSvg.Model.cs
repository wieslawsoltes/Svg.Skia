// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;
using ShimSkiaSharp;
using Svg.Model;
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
        var references = new HashSet<Uri> { svgFragment.OwnerDocument.BaseUri };
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
    private Uri? _originalBaseUri;
    private int _activeDraws;

    public object Sync { get; } = new();

    public SKSvgSettings Settings { get; }

    public ISvgAssetLoader AssetLoader { get; }

    public SkiaModel SkiaModel { get; }

    public SKDrawable? Drawable { get; private set; }

    public SKPicture? Model { get; private set; }

    private SkiaSharp.SKPicture? _picture;
    public virtual SkiaSharp.SKPicture? Picture
    {
        get
        {
            SkiaSharp.SKPicture? picture;
            SKPicture? model;
            lock (Sync)
            {
                picture = _picture;
                if (picture is { })
                {
                    return picture;
                }

                model = Model;
                if (model is null)
                {
                    return null;
                }
            }

            var newPicture = SkiaModel.ToSKPicture(model);
            if (newPicture is null)
            {
                return null;
            }

            lock (Sync)
            {
                if (!ReferenceEquals(Model, model))
                {
                    newPicture.Dispose();
                    return _picture;
                }

                if (_picture is { } existing)
                {
                    newPicture.Dispose();
                    return existing;
                }

                _picture = newPicture;
                return newPicture;
            }
        }
        protected set => _picture = value;
    }

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
        lock (Sync)
        {
            WaitForDrawsLocked();
            WireframePicture?.Dispose();
            WireframePicture = null;
        }
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

    /// <summary>
    /// Rebuilds the SkiaSharp picture from the current model.
    /// </summary>
    /// <returns>The rebuilt SkiaSharp picture, or null when no model exists.</returns>
    public SkiaSharp.SKPicture? RebuildFromModel()
    {
        var model = Model;
        if (model is null)
        {
            lock (Sync)
            {
                WaitForDrawsLocked();
                _picture?.Dispose();
                _picture = null;
                WireframePicture?.Dispose();
                WireframePicture = null;
            }
            return null;
        }

        var rebuilt = SkiaModel.ToSKPicture(model);
        lock (Sync)
        {
            WaitForDrawsLocked();
            if (!ReferenceEquals(Model, model))
            {
                rebuilt?.Dispose();
                return _picture;
            }

            _picture?.Dispose();
            _picture = rebuilt;
            WireframePicture?.Dispose();
            WireframePicture = null;
        }
        return rebuilt;
    }

    /// <summary>
    /// Creates a deep clone of the current <see cref="SKSvg"/>, including model and reload data.
    /// </summary>
    /// <returns>A new <see cref="SKSvg"/> instance with independent state.</returns>
    public SKSvg Clone()
    {
        var clone = new SKSvg();

        clone.Settings.AlphaType = Settings.AlphaType;
        clone.Settings.ColorType = Settings.ColorType;
        clone.Settings.SrgbLinear = Settings.SrgbLinear;
        clone.Settings.Srgb = Settings.Srgb;
        clone.Settings.TypefaceProviders = Settings.TypefaceProviders is null
            ? null
            : new List<TypefaceProviders.ITypefaceProvider>(Settings.TypefaceProviders);

        clone.Wireframe = Wireframe;
        clone.IgnoreAttributes = IgnoreAttributes;

        clone._originalParameters = _originalParameters;
        clone._originalPath = _originalPath;
        clone._originalBaseUri = _originalBaseUri;

        if (_originalStream is { } originalStream)
        {
            clone._originalStream = new MemoryStream();
            var position = originalStream.Position;
            originalStream.Position = 0;
            originalStream.CopyTo(clone._originalStream);
            clone._originalStream.Position = 0;
            originalStream.Position = position;
        }

        if (Model is { } model)
        {
            clone.Model = model.DeepClone();
            clone.Drawable = Drawable?.DeepClone();
        }

        return clone;
    }

    public SkiaSharp.SKPicture? Load(System.IO.Stream stream, SvgParameters? parameters = null)
    {
        return LoadInternal(stream, parameters, null);
    }

    public SkiaSharp.SKPicture? Load(System.IO.Stream stream) => Load(stream, null);

    public SkiaSharp.SKPicture? Load(System.IO.Stream stream, SvgParameters? parameters, Uri? baseUri)
    {
        return LoadInternal(stream, parameters, baseUri);
    }

    public SkiaSharp.SKPicture? Load(string? path, SvgParameters? parameters = null)
    {
        _originalPath = path;
        _originalParameters = parameters;
        _originalBaseUri = null;
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
            _originalBaseUri = null;
            Model = SvgService.ToModel(svgDocument, AssetLoader, out var drawable, out _, _ignoreAttributes);
            Drawable = drawable;
            Picture = SkiaModel.ToSKPicture(Model);
            WireframePicture?.Dispose();
            WireframePicture = null;
            return Picture;
        }

        return null;
    }

    private SkiaSharp.SKPicture? LoadInternal(System.IO.Stream stream, SvgParameters? parameters, Uri? baseUri)
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
            _originalBaseUri = baseUri;
            _originalStream.Position = 0;

            svgDocument = SvgService.Open(_originalStream, parameters);
        }
        else
        {
            _originalBaseUri = baseUri;
            svgDocument = SvgService.Open(stream, parameters);
        }

        if (svgDocument is null)
        {
            return null;
        }

        if (baseUri is { })
        {
            svgDocument.BaseUri = baseUri;
        }

        Model = SvgService.ToModel(svgDocument, AssetLoader, out var drawable, out _, _ignoreAttributes);
        Drawable = drawable;
        Picture = SkiaModel.ToSKPicture(Model);
        WireframePicture?.Dispose();
        WireframePicture = null;

        return Picture;
    }

    public SkiaSharp.SKPicture? ReLoad(SvgParameters? parameters)
    {
        if (!CacheOriginalStream)
        {
            throw new ArgumentException($"Enable {nameof(CacheOriginalStream)} feature toggle to enable reload feature.");
        }

        string? originalPath;
        System.IO.Stream? originalStream;
        Uri? originalBaseUri;

        lock (Sync)
        {
            _originalParameters = parameters;
            originalPath = _originalPath;
            originalStream = _originalStream;
            originalBaseUri = _originalBaseUri;
        }

        Reset();

        if (originalStream == null)
        {
            return Load(originalPath, parameters);
        }

        originalStream.Position = 0;

        return Load(originalStream, parameters, originalBaseUri);
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
        BeginDraw();
        try
        {
        var picture = Picture;
        if (picture is null)
        {
            return;
        }

        canvas.Save();
        if (Wireframe && Model is { })
        {
            var wireframePicture = WireframePicture;
            if (wireframePicture is null && Model is { } model)
            {
                var newWireframe = SkiaModel.ToWireframePicture(model);
                lock (Sync)
                {
                    if (WireframePicture is null)
                    {
                        WireframePicture = newWireframe;
                    }
                    else
                    {
                        newWireframe?.Dispose();
                    }
                    wireframePicture = WireframePicture;
                }
            }

            if (wireframePicture is { })
            {
                canvas.DrawPicture(wireframePicture);
            }
        }
        else
        {
            canvas.DrawPicture(picture);
        }
            canvas.Restore();
        }
        finally
        {
            EndDraw();
        }

        RaiseOnDraw(new SKSvgDrawEventArgs(canvas));
    }

    private void Reset()
    {
        lock (Sync)
        {
            WaitForDrawsLocked();
            Model = null;
            Drawable = null;

            _picture?.Dispose();
            _picture = null;

            WireframePicture?.Dispose();
            WireframePicture = null;
        }
    }

    public void Dispose()
    {
        Reset();
        _originalStream?.Dispose();
    }

    private void BeginDraw()
    {
        lock (Sync)
        {
            _activeDraws++;
        }
    }

    private void EndDraw()
    {
        lock (Sync)
        {
            if (_activeDraws > 0 && --_activeDraws == 0)
            {
                Monitor.PulseAll(Sync);
            }
        }
    }

    private void WaitForDrawsLocked()
    {
        while (_activeDraws > 0)
        {
            Monitor.Wait(Sync);
        }
    }
}
