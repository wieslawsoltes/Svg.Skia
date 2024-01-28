using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Avalonia.Metadata;
using SkiaSharp;
using Svg;
using Svg.Model;
using Svg.Skia;

namespace Avalonia.Svg.Skia;

/// <summary>
/// Represents a <see cref="SkiaSharp.SKPicture"/> based image.
/// </summary>
[TypeConverter(typeof(SvgSourceTypeConverter))]
public class SvgSource : SKSvg
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly Uri? _baseUri;
    private SKPicture? _picture;

    [Content]
    public string? Path { get; init; }

    public Dictionary<string, string>? Entities { get; init; }

    public string? Css { get; init; }

    public override SKPicture? Picture
    {
        get
        {
            if (_picture is null && Path is not null)
            {
                _picture = LoadImpl(this, Path, _baseUri, new SvgParameters(Entities, Css));
            }

            return _picture;
        }
        protected set => _picture = value;
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
        _serviceProvider = serviceProvider;
        _baseUri = serviceProvider.GetContextBaseUri();
    }
    
    /// <summary>
    /// Enable throw exception on missing resource.
    /// </summary>
    public static bool EnableThrowOnMissingResource { get; set; }

    private static SKPicture? LoadImpl(SvgSource source, string path, Uri? baseUri, SvgParameters? parameters = null)
    {
        if (File.Exists(path))
        {
            return source.Load(path, parameters);
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var uriHttp) && (uriHttp.Scheme == "http" || uriHttp.Scheme == "https"))
        {
            try
            {
                var response = new HttpClient().GetAsync(uriHttp).Result;
                if (response.IsSuccessStatusCode)
                {
                    var stream = response.Content.ReadAsStreamAsync().Result;
                    return source.Load(stream, parameters);
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
            return source.Load(uri.LocalPath, parameters);
        }
        else
        {
            var stream = AvaloniaLocator.Current.GetService<Avalonia.Platform.IAssetLoader>()?.Open(uri, baseUri);
            if (stream is null)
            {
                ThrowOnMissingResource(path);
                return null;
            }
            return source.Load(stream, parameters);
        }
    }

    /// <summary>t
    /// Loads svg source from file or resource.
    /// </summary>
    /// <param name="path">The path to file or resource.</param>
    /// <param name="baseUri">The base uri.</param>
    /// <param name="parameters">The svg parameters.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource? Load(string path, Uri? baseUri, SvgParameters? parameters = null)
    {
        if (File.Exists(path))
        {
            var source = new SvgSource(baseUri);
            source.Load(path, parameters);
            return source;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var uriHttp) && (uriHttp.Scheme == "http" || uriHttp.Scheme == "https"))
        {
            try
            {
                var response = new HttpClient().GetAsync(uriHttp).Result;
                if (response.IsSuccessStatusCode)
                {
                    var stream = response.Content.ReadAsStreamAsync().Result;
                    var source = new SvgSource(baseUri);
                    source.Load(stream, parameters);
                    return source;
                }
            }
            catch (HttpRequestException e)
            {
                Debug.WriteLine("Failed to connect to " + uriHttp);
                Debug.WriteLine(e.ToString());
            }

            return ThrowOnMissingResource(path);
        }
        
        var uri = path.StartsWith("/") ? new Uri(path, UriKind.Relative) : new Uri(path, UriKind.RelativeOrAbsolute);
        if (uri.IsAbsoluteUri && uri.IsFile)
        {
            var source = new SvgSource(baseUri);
            source.Load(uri.LocalPath, parameters);
            return source;
        }
        else
        {
            var stream = AvaloniaLocator.Current.GetService<Avalonia.Platform.IAssetLoader>()?.Open(uri, baseUri);
            if (stream is null)
            {
                return ThrowOnMissingResource(path);
            }
            var source = new SvgSource(baseUri);
            source.Load(stream, parameters);
            return source;
        }
    }

    /// <summary>
    /// Loads svg source from svg source.
    /// </summary>
    /// <param name="source">The svg source.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource? LoadFromSvg(string source)
    {
        var skSvg = new SvgSource(default(Uri));
        skSvg.FromSvg(source);
        return skSvg;
    }

    /// <summary>t
    /// Loads svg source from stream.
    /// </summary>
    /// <param name="stream">The svg stream.</param>
    /// <param name="parameters">The svg parameters.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource? LoadFromStream(Stream stream, SvgParameters? parameters = null)
    {
        var skSvg = new SvgSource(default(Uri));
        skSvg.Load(stream, parameters);
        return skSvg;
    }

    /// <summary>t
    /// Loads svg source from svg document.
    /// </summary>
    /// <param name="document">The svg document.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource? LoadFromSvgDocument(SvgDocument document)
    {
        var skSvg = new SvgSource(default(Uri));
        skSvg.FromSvgDocument(document);
        return skSvg;
    }

    private static SvgSource? ThrowOnMissingResource(string path)
    {
        return EnableThrowOnMissingResource 
            ? throw new ArgumentException($"Invalid resource path: {path}") 
            : default;
    }
}
