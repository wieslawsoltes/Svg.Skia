// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Platform;
using SkiaSharp;
using Svg;
using Svg.Model;
using Svg.Skia;
using DrawingColor = System.Drawing.Color;

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
    private Uri? _originalBaseUri;
    private int _activeRenders;
    private readonly ThreadLocal<int> _renderDepth = new(() => 0);
    private List<ResourceDisposal>? _deferredDisposals;
    private bool _disposePending;
    private bool _disposed;

    [Content]
    public string? Path { get; init; }

    public Dictionary<string, string>? Entities { get; init; }

    public string? Css { get; init; }

    public Color? CurrentColor { get; init; }

    public SKSvg? Svg => Volatile.Read(ref _skSvg);

    public SvgParameters? Parameters => _originalParameters;

    public SKPicture? Picture
    {
        get
        {
            var skSvg = Volatile.Read(ref _skSvg);
            if (skSvg is { })
            {
                var currentPicture = skSvg.Picture;
                lock (Sync)
                {
                    _picture = currentPicture;
                }
                return currentPicture;
            }

            var picture = _picture;
            if (picture is not null)
            {
                return picture;
            }

            var path = Path;
            if (path is null)
            {
                return null;
            }

            var entitiesCopy = Entities is null ? null : new Dictionary<string, string>(Entities);
            return LoadImpl(this, path, _baseUri, new SvgParameters(entitiesCopy, Css, ToDrawingColor(CurrentColor)));
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
        SKPicture? picture = null;
        SKSvg? skSvg = null;
        Stream? originalStream = null;

        lock (Sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposePending = true;

            if (_activeRenders > 0)
            {
                if (_renderDepth.Value > 0)
                {
                    return;
                }

                while (_activeRenders > 0)
                {
                    Monitor.Wait(Sync);
                }

                if (_disposed)
                {
                    return;
                }
            }

            DisposeCoreLocked(out picture, out skSvg, out originalStream);
        }

        DisposeResources(picture, skSvg, originalStream);
    }

    /// <summary>
    /// Enable throw exception on missing resource.
    /// </summary>
    public static bool EnableThrowOnMissingResource { get; set; }

    public object Sync { get; } = new();

    private readonly struct ResourceDisposal
    {
        public ResourceDisposal(SKPicture? picture, SKSvg? skSvg, Stream? originalStream)
        {
            Picture = picture;
            SkSvg = skSvg;
            OriginalStream = originalStream;
        }

        public SKPicture? Picture { get; }

        public SKSvg? SkSvg { get; }

        public Stream? OriginalStream { get; }
    }

    static SvgSource()
    {
        s_skiaModel = new SkiaModel(new SKSvgSettings());
        s_assetLoader = new SkiaSvgAssetLoader(s_skiaModel);
    }

    private static SKPicture? Load(SvgSource source, string? path, SvgParameters? parameters)
    {
        SKPicture? oldPicture = null;
        SKSvg? oldSkSvg = null;
        Stream? oldOriginalStream = null;

        if (path is null)
        {
            if (source.ReplaceResources(
                    picture: null,
                    skSvg: null,
                    originalStream: null,
                    originalPath: null,
                    parameters: parameters,
                    originalBaseUri: null,
                    out oldPicture,
                    out oldSkSvg,
                    out oldOriginalStream))
            {
                DisposeResources(oldPicture, oldSkSvg, oldOriginalStream);
            }

            return null;
        }

        var skSvg = CreateSkSvg();
        skSvg.Load(path, parameters);
        var picture = skSvg.Picture;

        if (source.ReplaceResources(
                picture,
                skSvg,
                originalStream: null,
                originalPath: path,
                parameters: parameters,
                originalBaseUri: null,
                out oldPicture,
                out oldSkSvg,
                out oldOriginalStream))
        {
            DisposeResources(oldPicture, oldSkSvg, oldOriginalStream);
            return picture;
        }

        DisposeResources(picture, skSvg, originalStream: null);
        return null;
    }

    private static SKPicture? Load(SvgSource source, Stream stream, SvgParameters? parameters = null, Uri? baseUri = null)
    {
        var cachedStream = new MemoryStream();
        stream.CopyTo(cachedStream);
        return LoadFromCachedStream(source, cachedStream, parameters, baseUri);
    }

    private static SKPicture? LoadFromCachedStream(SvgSource source, MemoryStream cachedStream, SvgParameters? parameters, Uri? baseUri)
    {
        cachedStream.Position = 0;
        var skSvg = CreateSkSvg();
        skSvg.Load(cachedStream, parameters, baseUri);
        var picture = skSvg.Picture;
        SKPicture? oldPicture;
        SKSvg? oldSkSvg;
        Stream? oldOriginalStream;

        if (source.ReplaceResources(
                picture,
                skSvg,
                cachedStream,
                originalPath: null,
                parameters: parameters,
                originalBaseUri: baseUri,
                out oldPicture,
                out oldSkSvg,
                out oldOriginalStream))
        {
            DisposeResources(oldPicture, oldSkSvg, oldOriginalStream);
            return picture;
        }

        DisposeResources(picture, skSvg, cachedStream);
        return null;
    }

    private static MemoryStream CreateStream(string svg)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(svg));
    }

    private static MemoryStream CreateStream(SvgDocument document)
    {
        var stream = new MemoryStream();
        document.Write(stream, useBom: false);
        stream.Position = 0;
        return stream;
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
                    return Load(source, stream, parameters, uriHttp);
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
            return Load(source, stream, parameters, baseUri);
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
        LoadImpl(source, path, baseUri, parameters);
        return source;
    }

    /// <summary>
    /// Loads svg source from svg source.
    /// </summary>
    /// <param name="svg">The svg source.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource LoadFromSvg(string svg)
    {
        return LoadFromSvg(svg, null);
    }

    /// <summary>
    /// Loads svg source from svg source.
    /// </summary>
    /// <param name="svg">The svg source.</param>
    /// <param name="parameters">The svg parameters.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource LoadFromSvg(string svg, SvgParameters? parameters)
    {
        var source = new SvgSource(default(Uri));
        using var stream = CreateStream(svg);
        Load(source, stream, parameters);
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
        Load(source, stream, parameters);
        return source;
    }

    /// <summary>t
    /// Loads svg source from svg document.
    /// </summary>
    /// <param name="document">The svg document.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource LoadFromSvgDocument(SvgDocument document)
    {
        return LoadFromSvgDocument(document, null);
    }

    /// <summary>t
    /// Loads svg source from svg document.
    /// </summary>
    /// <param name="document">The svg document.</param>
    /// <param name="parameters">The svg parameters.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource LoadFromSvgDocument(SvgDocument document, SvgParameters? parameters)
    {
        var source = new SvgSource(default(Uri));
        if (parameters is null)
        {
            var originalStream = CreateStream(document);
            var skSvg = CreateSkSvg();
            skSvg.FromSvgDocument(document);
            var picture = skSvg.Picture;
            SKPicture? oldPicture;
            SKSvg? oldSkSvg;
            Stream? oldOriginalStream;

            if (source.ReplaceResources(
                    picture,
                    skSvg,
                    originalStream,
                    originalPath: null,
                    parameters: null,
                    originalBaseUri: document.BaseUri,
                    out oldPicture,
                    out oldSkSvg,
                    out oldOriginalStream))
            {
                DisposeResources(oldPicture, oldSkSvg, oldOriginalStream);
                return source;
            }

            DisposeResources(picture, skSvg, originalStream);
            return source;
        }

        using var stream = CreateStream(document);
        Load(source, stream, parameters, document.BaseUri);
        return source;
    }

    /// <summary>
    /// Rebuilds the <see cref="SvgSource"/> from its underlying model, refreshing its associated
    /// <see cref="SkiaSharp.SKPicture"/> representation if the <see cref="SKSvg"/> instance exists.
    /// </summary>
    public void RebuildFromModel()
    {
        SKSvg? skSvg;
        lock (Sync)
        {
            skSvg = _skSvg;
        }

        if (skSvg is null)
        {
            return;
        }

        skSvg.RebuildFromModel();

        lock (Sync)
        {
            if (ReferenceEquals(_skSvg, skSvg))
            {
                _picture = skSvg.Picture;
            }
        }
    }

    /// <summary>
    /// Creates a deep clone of this <see cref="SvgSource"/> with independent model data.
    /// </summary>
    /// <returns>A new <see cref="SvgSource"/> instance.</returns>
    public SvgSource Clone()
    {
        var clone = new SvgSource(_baseUri)
        {
            Path = Path,
            Entities = Entities is null ? null : new Dictionary<string, string>(Entities),
            Css = Css,
            CurrentColor = CurrentColor
        };

        SvgParameters? originalParameters;
        string? originalPath;
        Uri? originalBaseUri;
        SKSvg? skSvg;
        SKPicture? picture;
        MemoryStream? originalStreamCopy = null;
        SKPicture? clonedPicture = null;
        var canClonePicture = false;

        lock (Sync)
        {
            originalParameters = _originalParameters;
            originalPath = _originalPath;
            originalBaseUri = _originalBaseUri;
            skSvg = _skSvg;
            picture = _picture;

            if (_originalStream is { } originalStream)
            {
                originalStreamCopy = new MemoryStream();
                var position = originalStream.Position;
                originalStream.Position = 0;
                originalStream.CopyTo(originalStreamCopy);
                originalStreamCopy.Position = 0;
                originalStream.Position = position;
            }

            canClonePicture = picture is { }
                              && _originalStream is null
                              && _originalPath is null
                              && Path is null;

            if (canClonePicture && picture is { })
            {
                clonedPicture = ClonePicture(picture);
            }
        }

        clone._originalParameters = CloneParameters(originalParameters);
        clone._originalPath = originalPath;
        clone._originalBaseUri = originalBaseUri;
        clone._originalStream = originalStreamCopy;

        if (skSvg is { })
        {
            clone._skSvg = skSvg.Clone();
        }
        else if (canClonePicture)
        {
            clone._picture = clonedPicture;
        }

        return clone;
    }

    private static SvgParameters? CloneParameters(SvgParameters? parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        var entities = parameters.Value.Entities;
        var entitiesCopy = entities is null ? null : new Dictionary<string, string>(entities);
        return new SvgParameters(entitiesCopy, parameters.Value.Css, parameters.Value.CurrentColor);
    }

    private static DrawingColor? ToDrawingColor(Color? color)
    {
        return color is { } value
            ? DrawingColor.FromArgb(value.A, value.R, value.G, value.B)
            : null;
    }

    private static SKPicture? ClonePicture(SKPicture? picture)
    {
        if (picture is null)
        {
            return null;
        }

        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(picture.CullRect);
        canvas.DrawPicture(picture);
        return recorder.EndRecording();
    }

    private static SKSvg CreateSkSvg()
    {
        var skSvg = new SKSvg();
        s_skiaModel.Settings.CopyTo(skSvg.Settings);
        return skSvg;
    }

    public void ReLoad(SvgParameters? parameters)
    {
        MemoryStream? streamCopy = null;
        string? originalPath;
        string? path;
        Uri? originalBaseUri;
        Uri? baseUri;

        lock (Sync)
        {
            if (_originalStream is null && _originalPath is null && Path is null)
            {
                return;
            }

            if (_originalStream is { } originalStream)
            {
                streamCopy = new MemoryStream();
                var position = originalStream.Position;
                originalStream.Position = 0;
                originalStream.CopyTo(streamCopy);
                streamCopy.Position = 0;
                originalStream.Position = position;
            }
            originalPath = _originalPath;
            originalBaseUri = _originalBaseUri;
            path = Path;
            baseUri = _baseUri;
        }

        if (streamCopy is { })
        {
            LoadFromCachedStream(this, streamCopy, parameters, originalBaseUri);
            return;
        }

        if (originalPath is { })
        {
            Load(this, originalPath, parameters);
            return;
        }

        if (path is { })
        {
            LoadImpl(this, path, baseUri, parameters);
        }
    }

    private bool ReplaceResources(
        SKPicture? picture,
        SKSvg? skSvg,
        Stream? originalStream,
        string? originalPath,
        SvgParameters? parameters,
        Uri? originalBaseUri,
        out SKPicture? oldPicture,
        out SKSvg? oldSkSvg,
        out Stream? oldOriginalStream)
    {
        oldPicture = null;
        oldSkSvg = null;
        oldOriginalStream = null;

        lock (Sync)
        {
            if (_disposed || _disposePending)
            {
                return false;
            }

            oldPicture = _picture;
            oldSkSvg = _skSvg;
            oldOriginalStream = _originalStream;

            _picture = picture;
            _skSvg = skSvg;
            _originalStream = originalStream;
            _originalPath = originalPath;
            _originalParameters = parameters;
            _originalBaseUri = originalBaseUri;

            if (_activeRenders > 0)
            {
                QueueDeferredDisposalLocked(oldPicture, oldSkSvg, oldOriginalStream);
                oldPicture = null;
                oldSkSvg = null;
                oldOriginalStream = null;
            }
        }

        return true;
    }

    private void QueueDeferredDisposalLocked(SKPicture? picture, SKSvg? skSvg, Stream? originalStream)
    {
        if (picture is null && skSvg is null && originalStream is null)
        {
            return;
        }

        _deferredDisposals ??= new List<ResourceDisposal>();
        _deferredDisposals.Add(new ResourceDisposal(picture, skSvg, originalStream));
    }

    internal bool BeginRender()
    {
        lock (Sync)
        {
            if (_disposed || _disposePending)
            {
                return false;
            }

            _activeRenders++;
            _renderDepth.Value++;
            return true;
        }
    }

    internal void EndRender()
    {
        SKPicture? picture = null;
        SKSvg? skSvg = null;
        Stream? originalStream = null;
        List<ResourceDisposal>? deferredDisposals = null;

        lock (Sync)
        {
            if (_renderDepth.Value > 0)
            {
                _renderDepth.Value--;
            }

            if (_activeRenders > 0 && --_activeRenders == 0)
            {
                deferredDisposals = _deferredDisposals;
                _deferredDisposals = null;

                if (_disposePending)
                {
                    DisposeCoreLocked(out picture, out skSvg, out originalStream);
                }

                Monitor.PulseAll(Sync);
            }
        }

        if (picture is not null || skSvg is not null || originalStream is not null)
        {
            DisposeResources(picture, skSvg, originalStream);
        }

        DisposeDeferredResources(deferredDisposals);
    }

    private void DisposeCoreLocked(out SKPicture? picture, out SKSvg? skSvg, out Stream? originalStream)
    {
        picture = _picture;
        skSvg = _skSvg;
        originalStream = _originalStream;

        _picture = null;
        _skSvg = null;
        _originalPath = null;
        _originalParameters = null;
        _originalBaseUri = null;
        _originalStream = null;
        _disposePending = false;
        _disposed = true;
    }

    private static void DisposeResources(SKPicture? picture, SKSvg? skSvg, Stream? originalStream)
    {
        SKPicture? skSvgPicture = null;
        if (skSvg is { } && picture is { })
        {
            skSvgPicture = skSvg.Picture;
        }

        skSvg?.Dispose();

        if (picture is { } && !ReferenceEquals(picture, skSvgPicture))
        {
            picture.Dispose();
        }

        originalStream?.Dispose();
    }

    private static void DisposeDeferredResources(List<ResourceDisposal>? disposals)
    {
        if (disposals is null)
        {
            return;
        }

        foreach (var disposal in disposals)
        {
            DisposeResources(disposal.Picture, disposal.SkSvg, disposal.OriginalStream);
        }
    }
}
