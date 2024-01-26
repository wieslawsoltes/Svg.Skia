using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
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
    /// <summary>
    /// Enable throw exception on missing resource.
    /// </summary>
    public static bool EnableThrowOnMissingResource { get; set; }

    /// <summary>t
    /// Loads svg source from file or resource.
    /// </summary>
    /// <param name="path">The path to file or resource.</param>
    /// <param name="baseUri">The base uri.</param>
    /// <param name="parameters">The svg parameters.</param>
    /// <returns>The svg source.</returns>
    public static T? Load<T>(string path, Uri? baseUri, SvgParameters? parameters = null) where T : SKSvg, new()
    {
        if (File.Exists(path))
        {
            var source = new T();
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
                    var source = new T();
                    source.Load(stream, parameters);
                    return source;
                }
            }
            catch (HttpRequestException e)
            {
                Debug.WriteLine("Failed to connect to " + uriHttp);
                Debug.WriteLine(e.ToString());
            }

            return ThrowOnMissingResource<T>(path);
        }
        
        var uri = path.StartsWith("/") ? new Uri(path, UriKind.Relative) : new Uri(path, UriKind.RelativeOrAbsolute);
        if (uri.IsAbsoluteUri && uri.IsFile)
        {
            var source = new T();
            source.Load(uri.LocalPath, parameters);
            return source;
        }
        else
        {
            var stream = Platform.AssetLoader.Open(uri, baseUri);
            if (stream is null)
            {
                return ThrowOnMissingResource<T>(path);
            }
            var source = new T();
            source.Load(stream, parameters);
            return source;
        }
    }

    /// <summary>
    /// Loads svg source from svg source.
    /// </summary>
    /// <param name="source">The svg source.</param>
    /// <returns>The svg source.</returns>
    public static T? LoadFromSvg<T>(string source) where T : SKSvg, new()
    {
        var skSvg = new T();
        skSvg.FromSvg(source);
        return skSvg;
    }

    /// <summary>t
    /// Loads svg source from stream.
    /// </summary>
    /// <param name="stream">The svg stream.</param>
    /// <param name="parameters">The svg parameters.</param>
    /// <returns>The svg source.</returns>
    public static T? LoadFromStream<T>(Stream stream, SvgParameters? parameters = null) where T : SKSvg, new()
    {
        var skSvg = new T();
        skSvg.Load(stream, parameters);
        return skSvg;
    }

    /// <summary>t
    /// Loads svg source from svg document.
    /// </summary>
    /// <param name="document">The svg document.</param>
    /// <returns>The svg source.</returns>
    public static T? LoadFromSvgDocument<T>(SvgDocument document) where T : SKSvg, new()
    {
        var skSvg = new T();
        skSvg.FromSvgDocument(document);
        return skSvg;
    }

    private static T? ThrowOnMissingResource<T>(string path) where T : SKSvg, new()
    {
        return EnableThrowOnMissingResource 
            ? throw new ArgumentException($"Invalid resource path: {path}") 
            : default;
    }
}
