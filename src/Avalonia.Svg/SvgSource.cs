using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Avalonia.Platform;
using ShimSkiaSharp;
using SM = Svg.Model;
using SP = Svg.Model;

namespace Avalonia.Svg;

/// <summary>
/// Represents a Svg based image.
/// </summary>
[TypeConverter(typeof(SvgSourceTypeConverter))]
public class SvgSource
{
    private static readonly SM.IAssetLoader s_assetLoader = new AvaloniaAssetLoader();

    public SKPicture? Picture { get; set; }

    /// <summary>
    /// Loads svg picture from file or resource.
    /// </summary>
    /// <param name="path">The path to file or resource.</param>
    /// <param name="baseUri">The base uri.</param>
    /// <returns>The svg picture.</returns>
    public static SKPicture? LoadPicture(string path, Uri? baseUri)
    {
        if (File.Exists(path))
        {
            var document = SM.SvgExtensions.Open(path);
            return document is { } ? SM.SvgExtensions.ToModel(document, s_assetLoader, out _, out _) : default;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var uriHttp) && (uriHttp.Scheme == "http" || uriHttp.Scheme == "https"))
        {
            try
            {
                var response = new HttpClient().GetAsync(uriHttp).Result;
                if (response.IsSuccessStatusCode)
                {
                    var stream = response.Content.ReadAsStreamAsync().Result;
                    var document = SM.SvgExtensions.Open(stream);
                    return document is { } ? SM.SvgExtensions.ToModel(document, s_assetLoader, out _, out _) : default;
                }
            }
            catch (HttpRequestException e)
            {
                Debug.WriteLine("Failed to connect to " + uriHttp);
                Debug.WriteLine(e.ToString());
            }

            return default;
        }

        var uri = path.StartsWith("/") ? new Uri(path, UriKind.Relative) : new Uri(path, UriKind.RelativeOrAbsolute);
        if (uri.IsAbsoluteUri && uri.IsFile)
        {
            var document = SM.SvgExtensions.Open(uri.LocalPath);
            return document is { } ? SM.SvgExtensions.ToModel(document, s_assetLoader, out _, out _) : default;
        }
        else
        {
            var stream = AssetLoader.Open(uri, baseUri);
            if (stream is null)
            {
                return default;
            }
            var document = SM.SvgExtensions.Open(stream);
            return document is { } ? SM.SvgExtensions.ToModel(document, s_assetLoader, out _, out _) : default;
        }
    }

    /// <summary>
    /// Loads svg source from file or resource.
    /// </summary>
    /// <param name="path">The path to file or resource.</param>
    /// <param name="baseUri">The base uri.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource Load(string path, Uri? baseUri)
    {
        return new() { Picture = LoadPicture(path, baseUri) };
    }
}
