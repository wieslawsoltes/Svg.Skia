// Copyright (c) Wiesław Šoltés, Stefan Koell. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Avalonia.Metadata;
using Avalonia.Platform;
using SkiaSharp;
using Svg;
using Svg.Model;
using Svg.Skia;

namespace Avalonia.Svg.Skia;

/// <summary>
/// Represents a <see cref="SkiaSharp.SKPicture"/> based image.
/// </summary>
[TypeConverter(typeof(SvgSourceTypeConverter))]
public sealed class SvgSource : IDisposable
{
    public static readonly ISvgAssetLoader s_assetLoader;

    public static readonly SkiaModel s_skiaModel;

    private SKSvg? _skSvg;

    private readonly Uri? _baseUri;
    private SKPicture? _picture;
    private SvgParameters? _originalParameters;
    private string? _originalPath;
    private Stream? _originalStream;

    [Content]
    public string? Path { get; init; }

    public Dictionary<string, string>? Entities { get; init; }

    public string? Css { get; init; }

    public SKSvg? Svg
    {
        get
        {
            lock (Sync)
            {
                return _skSvg;
            }
        }
    }

    public SvgParameters? Parameters => _originalParameters;

    public SKPicture? Picture
    {
        get
        {
            if (_picture is null && Path is not null)
            {
                var entitiesCopy = Entities is null ? null : new Dictionary<string, string>(Entities);
                _picture = LoadImpl(this, Path, _baseUri, new SvgParameters(entitiesCopy, Css));
            }

            return _picture;
        }
        set => _picture = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SvgSource"/> class.
    /// </summary>
    /// <param name="baseUri">The base URL for the XAML context.</param>
    public SvgSource(Uri? baseUri)
    {
        _baseUri = baseUri;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SvgSource"/> class.
    /// </summary>
    /// <param name="serviceProvider">The XAML service provider.</param>
    public SvgSource(IServiceProvider serviceProvider)
    {
        _baseUri = serviceProvider.GetContextBaseUri();
    }

    public void Dispose()
    {
        lock (Sync)
        {
            _picture?.Dispose();

            _originalPath = null;
            _originalStream?.Dispose();
            _originalStream = null;
        }
    }

    /// <summary>
    /// Enable throw exception on missing resource.
    /// </summary>
    public static bool EnableThrowOnMissingResource { get; set; }

    public object Sync { get; } = new();

    static SvgSource()
    {
        s_skiaModel = new SkiaModel(new SKSvgSettings());
        s_assetLoader = new SkiaSvgAssetLoader(s_skiaModel);
    }

    private static SKPicture? Load(SvgSource source, string? path, SvgParameters? parameters)
    {
        source._originalPath = path;
        source._originalStream?.Dispose();
        source._originalStream = null;

        if (path is null)
        {
            return null;
        }

        var skSvg = new SKSvg();
        skSvg.Load(path, parameters);
        lock (source.Sync)
        {
            source._skSvg = skSvg;
        }
        return skSvg.Picture;
    }

    private static SKPicture? Load(SvgSource source, Stream stream, SvgParameters? parameters = null)
    {
        if (source._originalStream != stream)
        {
            source._originalStream?.Dispose();
            source._originalStream = new MemoryStream();
            stream.CopyTo(source._originalStream);
        }

        source._originalPath = null;
        source._originalParameters = parameters;
        source._originalStream.Position = 0;

        var skSvg = new SKSvg();
        skSvg.Load(source._originalStream, parameters);
        lock (source.Sync)
        {
            source._skSvg = skSvg;
        }
        return skSvg.Picture;
    }

    private static SKPicture? FromSvg(SvgSource source, string svg)
    {
        var skSvg = new SKSvg();
        skSvg.FromSvg(svg);
        lock (source.Sync)
        {
            source._skSvg = skSvg;
        }
        return skSvg.Picture;
    }

    private static SKPicture? FromSvgDocument(SvgSource source, SvgDocument? svgDocument)
    {
        if (svgDocument is { })
        {
            var skSvg = new SKSvg();
            skSvg.FromSvgDocument(svgDocument);
            lock (source.Sync)
            {
                source._skSvg = skSvg;
            }
            return skSvg.Picture;
        }
        return null;
    }

    private static SvgSource? ThrowOnMissingResource(string path)
    {
        return EnableThrowOnMissingResource
            ? throw new ArgumentException($"Invalid resource path: {path}")
            : null;
    }

    private static SKPicture? LoadImpl(SvgSource source, string path, Uri? baseUri, SvgParameters? parameters = null)
    {
        if (File.Exists(path))
        {
            return Load(source, path, parameters);
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var uriHttp) && (uriHttp.Scheme == "http" || uriHttp.Scheme == "https"))
        {
            try
            {
                var response = new HttpClient().GetAsync(uriHttp).Result;
                if (response.IsSuccessStatusCode)
                {
                    var stream = response.Content.ReadAsStreamAsync().Result;
                    return Load(source, stream, parameters);
                }
            }
            catch (HttpRequestException e)
            {
                Debug.WriteLine("Failed to connect to " + uriHttp);
                Debug.WriteLine(e.ToString());
            }

            ThrowOnMissingResource(path);
            return null;
        }

        var uri = path.StartsWith("/") ? new Uri(path, UriKind.Relative) : new Uri(path, UriKind.RelativeOrAbsolute);
        if (uri.IsAbsoluteUri && uri.IsFile)
        {
            return Load(source, uri.LocalPath, parameters);
        }
        else
        {
            var stream = AssetLoader.Open(uri, baseUri);
            if (stream is null)
            {
                ThrowOnMissingResource(path);
                return null;
            }
            return Load(source, stream, parameters);
        }
    }

    /// <summary>t
    /// Loads svg source from file or resource.
    /// </summary>
    /// <param name="path">The path to file or resource.</param>
    /// <param name="baseUri">The base uri.</param>
    /// <param name="parameters">The svg parameters.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource Load(string path, Uri? baseUri = default, SvgParameters? parameters = null)
    {
        var source = new SvgSource(baseUri);
        source._picture = LoadImpl(source, path, baseUri, parameters);
        return source;
    }

    /// <summary>
    /// Loads svg source from svg source.
    /// </summary>
    /// <param name="svg">The svg source.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource LoadFromSvg(string svg)
    {
        var source = new SvgSource(default(Uri));
        source._picture = FromSvg(source, svg);
        return source;
    }

    /// <summary>t
    /// Loads svg source from stream.
    /// </summary>
    /// <param name="stream">The svg stream.</param>
    /// <param name="parameters">The svg parameters.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource LoadFromStream(Stream stream, SvgParameters? parameters = null)
    {
        var source = new SvgSource(default(Uri));
        source._picture = Load(source, stream, parameters);
        return source;
    }

    /// <summary>t
    /// Loads svg source from svg document.
    /// </summary>
    /// <param name="document">The svg document.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource LoadFromSvgDocument(SvgDocument document)
    {
        var source = new SvgSource(default(Uri));
        source._picture = FromSvgDocument(source, document);
        return source;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="SvgSource"/> class that is a copy of the current instance.
    /// </summary>
    /// <returns>A new <see cref="SvgSource"/> object that is a clone of this instance.</returns>
    public SvgSource Clone()
    {
        lock (Sync)
        {
            var clone = new SvgSource(_baseUri)
            {
                Path = Path,
                Entities = Entities is null ? null : new Dictionary<string, string>(Entities),
                Css = Css
            };

            clone._originalParameters = _originalParameters;
            clone._originalPath = _originalPath;
            if (_originalStream is { })
            {
                clone._originalStream = new MemoryStream();
                _originalStream.Position = 0;
                _originalStream.CopyTo(clone._originalStream);
            }

            if (_skSvg is { })
            {
                var skSvgClone = _skSvg.Clone();
                clone._skSvg = skSvgClone;
            }

            return clone;
        }
    }

    /// <summary>
    /// Rebuilds the <see cref="SvgSource"/> from its underlying model, refreshing its associated
    /// <see cref="SkiaSharp.SKPicture"/> representation if the <see cref="SKSvg"/> instance exists.
    /// </summary>
    public void RebuildFromModel()
    {
        if (Svg is null) 
            return;
        lock (Sync)
        {
            Svg.Picture = Svg.SkiaModel.ToSKPicture(Svg.Model);
            _picture = Svg.Picture;
        }
    }

    /// <summary>
    /// Reloads the SVG from the original source using the provided parameters. Modifications on the <see cref="SKSvg.SkiaModel" /> are lost.
    /// </summary>
    /// <param name="parameters">The optional parameters that define entities and CSS to apply when reloading the SVG source.</param>
    public void ReLoad(SvgParameters? parameters)
    {
        lock (Sync)
        {
            _picture = null;
            _skSvg = null;

            _originalParameters = parameters;

            if (_originalStream == null)
            {
                _picture = Load(this, _originalPath, parameters);
                return;
            }

            _originalStream.Position = 0;

            _picture = Load(this, _originalStream, parameters);
        }
    }
}
