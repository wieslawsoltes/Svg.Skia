using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Microsoft.UI.Xaml.Markup;
using SkiaSharp;
using Svg;
using Svg.Model;
using Svg.Skia;
using Windows.Storage;

namespace Uno.Svg.Skia;

[TypeConverter(typeof(SvgSourceTypeConverter))]
[ContentProperty(Name = nameof(Path))]
public sealed class SvgSource : IDisposable
{
    public static readonly SkiaModel SkiaModel = new(new SKSvgSettings());

    private static readonly HttpClient s_httpClient = new();

    private readonly Uri? _baseUri;
    private SKSvg? _skSvg;
    private SKPicture? _picture;
    private SvgParameters? _originalParameters;
    private string? _originalPath;
    private Stream? _originalStream;
    private Uri? _originalBaseUri;
    private int _activeRenders;
    private readonly ThreadLocal<int> _renderDepth = new(() => 0);
    private bool _disposePending;
    private bool _disposed;

    public SvgSource()
        : this((Uri?)null)
    {
    }

    public SvgSource(Uri? baseUri)
    {
        _baseUri = baseUri;
    }

    public string? Path { get; set; }

    public Dictionary<string, string>? Entities { get; set; }

    public string? Css { get; set; }

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

            return picture;
        }
        set => _picture = value;
    }

    public static bool EnableThrowOnMissingResource { get; set; }

    internal bool HasPathSource
    {
        get
        {
            lock (Sync)
            {
                return _originalPath is not null || Path is not null;
            }
        }
    }

    internal bool HasLoadedSource
    {
        get
        {
            lock (Sync)
            {
                return _originalStream is not null || _originalPath is not null || _skSvg is not null;
            }
        }
    }

    public object Sync { get; } = new();

    public static Uri NormalizePath(string path, Uri? baseUri = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must not be null or empty.", nameof(path));
        }

        if (path.StartsWith("/Assets/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/Assets", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri($"ms-appx://{path}", UriKind.Absolute);
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri;
        }

        if (System.IO.Path.IsPathRooted(path))
        {
            return new Uri(System.IO.Path.GetFullPath(path));
        }

        if (baseUri is not null)
        {
            return new Uri(baseUri, path);
        }

        return new Uri($"ms-appx:///{path.TrimStart('/')}", UriKind.Absolute);
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

    public static async Task<SvgSource> LoadAsync(
        string path,
        Uri? baseUri = null,
        SvgParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var source = new SvgSource(baseUri) { Path = path };
        await LoadImplAsync(source, path, baseUri, parameters, cancellationToken).ConfigureAwait(false);
        return source;
    }

    public static SvgSource LoadFromSvg(string svg)
    {
        return LoadFromSvg(svg, null);
    }

    public static SvgSource LoadFromSvg(string svg, SvgParameters? parameters)
    {
        var source = new SvgSource();
        using var stream = CreateStream(svg);
        Load(source, stream, parameters);
        return source;
    }

    public static SvgSource LoadFromStream(Stream stream, SvgParameters? parameters = null)
    {
        var source = new SvgSource();
        Load(source, stream, parameters);
        return source;
    }

    public static SvgSource LoadFromSvgDocument(SvgDocument document)
    {
        return LoadFromSvgDocument(document, null);
    }

    public static SvgSource LoadFromSvgDocument(SvgDocument document, SvgParameters? parameters)
    {
        var source = new SvgSource();
        if (parameters is null)
        {
            var originalStream = CreateStream(document);
            var skSvg = new SKSvg();
            skSvg.FromSvgDocument(document);
            var picture = skSvg.Picture;

            lock (source.Sync)
            {
                source._originalStream?.Dispose();
                source._originalStream = originalStream;
                source._originalPath = null;
                source._originalParameters = null;
                source._originalBaseUri = document.BaseUri;
                source._skSvg = skSvg;
                source._picture = picture;
            }

            return source;
        }

        using var stream = CreateStream(document);
        Load(source, stream, parameters, document.BaseUri);
        return source;
    }

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

    public SvgSource Clone()
    {
        var clone = new SvgSource(_baseUri)
        {
            Path = Path,
            Entities = Entities is null ? null : new Dictionary<string, string>(Entities),
            Css = Css
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

    public void ReLoad(SvgParameters? parameters)
    {
        ReLoadAsync(parameters).GetAwaiter().GetResult();
    }

    public async Task ReLoadAsync(SvgParameters? parameters, CancellationToken cancellationToken = default)
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
                originalStream.Position = 0;
                originalStream.CopyTo(streamCopy);
                streamCopy.Position = 0;
            }

            originalPath = _originalPath;
            originalBaseUri = _originalBaseUri;
            path = Path;
            baseUri = _baseUri;
            _originalParameters = parameters;
        }

        if (streamCopy is { })
        {
            LoadFromCachedStream(this, streamCopy, parameters, originalBaseUri);
            return;
        }

        if (originalPath is { })
        {
            await LoadImplAsync(this, originalPath, originalBaseUri, parameters, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (path is { })
        {
            await LoadImplAsync(this, path, baseUri, parameters, cancellationToken).ConfigureAwait(false);
        }
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

        lock (Sync)
        {
            if (_renderDepth.Value > 0)
            {
                _renderDepth.Value--;
            }

            if (_activeRenders > 0 && --_activeRenders == 0)
            {
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
    }

    private static SKPicture? Load(SvgSource source, string? path, SvgParameters? parameters)
    {
        if (path is null)
        {
            lock (source.Sync)
            {
                source._originalPath = null;
                source._originalStream?.Dispose();
                source._originalStream = null;
                source._originalParameters = parameters;
                source._originalBaseUri = null;
                source._skSvg = null;
                source._picture = null;
            }

            return null;
        }

        var skSvg = new SKSvg();
        skSvg.Load(path, parameters);
        var picture = skSvg.Picture;

        lock (source.Sync)
        {
            source._originalPath = path;
            source._originalStream?.Dispose();
            source._originalStream = null;
            source._originalParameters = parameters;
            source._originalBaseUri = new Uri(path, UriKind.RelativeOrAbsolute);
            source._skSvg = skSvg;
            source._picture = picture;
        }

        return picture;
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
        var skSvg = new SKSvg();
        skSvg.Load(cachedStream, parameters, baseUri);
        var picture = skSvg.Picture;

        lock (source.Sync)
        {
            source._originalStream?.Dispose();
            source._originalStream = cachedStream;
            source._originalPath = null;
            source._originalParameters = parameters;
            source._originalBaseUri = baseUri;
            source._skSvg = skSvg;
            source._picture = picture;
        }

        return picture;
    }

    private static async Task<SKPicture?> LoadImplAsync(
        SvgSource source,
        string path,
        Uri? baseUri,
        SvgParameters? parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedUri = NormalizePath(path, baseUri);

        if (normalizedUri.IsFile && File.Exists(normalizedUri.LocalPath))
        {
            return Load(source, normalizedUri.LocalPath, parameters);
        }

        await using var stream = await OpenStreamAsync(normalizedUri, cancellationToken).ConfigureAwait(false);
        if (stream is null)
        {
            ThrowOnMissingResource(path);
            return null;
        }

        return Load(source, stream, parameters, normalizedUri);
    }

    private static async Task<Stream?> OpenStreamAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri.IsFile)
        {
            if (!File.Exists(uri.LocalPath))
            {
                return null;
            }

            return File.OpenRead(uri.LocalPath);
        }

        if (uri.Scheme is "http" or "https")
        {
            try
            {
                var response = await s_httpClient
                    .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException e)
            {
                Debug.WriteLine("Failed to connect to " + uri);
                Debug.WriteLine(e);
                return null;
            }
        }

        if (uri.Scheme == "ms-appx")
        {
            try
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
                var readStream = await file.OpenReadAsync();
                return readStream.AsStreamForRead();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to load app asset " + uri);
                Debug.WriteLine(e);
                return null;
            }
        }

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
            ? throw new ArgumentException($"Invalid resource path: {path}", nameof(path))
            : null;
    }

    private static SvgParameters? CloneParameters(SvgParameters? parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        var entities = parameters.Value.Entities;
        var entitiesCopy = entities is null ? null : new Dictionary<string, string>(entities);
        return new SvgParameters(entitiesCopy, parameters.Value.Css);
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
}
