// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Avalonia.Platform;
using ShimSkiaSharp;
using Svg.Model;
using Svg.Model.Services;
using SM = Svg.Model;

namespace Avalonia.Svg;

/// <summary>
/// Represents a Svg based image.
/// </summary>
[TypeConverter(typeof(SvgSourceTypeConverter))]
public class SvgSource
{
    private static readonly SM.ISvgAssetLoader s_assetLoader = new AvaloniaSvgAssetLoader();

    public SKPicture? Picture { get; set; }

    /// <summary>
    /// Loads svg picture from file or resource.
    /// </summary>
    /// <param name="path">The path to file or resource.</param>
    /// <param name="baseUri">The base uri.</param>
    /// <param name="parameters">The svg parameters.</param>
    /// <returns>The svg picture.</returns>
    public static SKPicture? LoadPicture(string path, Uri? baseUri, SvgParameters? parameters = null)
    {
        if (File.Exists(path))
        {
            var document = SvgService.Open(path, parameters);
            return document is { } ? SvgService.ToModel(document, s_assetLoader, out _, out _) : default;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var uriHttp) && (uriHttp.Scheme == "http" || uriHttp.Scheme == "https"))
        {
            try
            {
                var response = new HttpClient().GetAsync(uriHttp).Result;
                if (response.IsSuccessStatusCode)
                {
                    var stream = response.Content.ReadAsStreamAsync().Result;
                    var document = SvgService.Open(stream, parameters);
                    return document is { } ? SvgService.ToModel(document, s_assetLoader, out _, out _) : default;
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
            var document = SvgService.Open(uri.LocalPath, parameters);
            return document is { } ? SvgService.ToModel(document, s_assetLoader, out _, out _) : default;
        }
        else
        {
            var stream = AssetLoader.Open(uri, baseUri);
            if (stream is null)
            {
                return default;
            }
            var document = SvgService.Open(stream, parameters);
            return document is { } ? SvgService.ToModel(document, s_assetLoader, out _, out _) : default;
        }
    }

    /// <summary>
    /// Loads svg source from file or resource.
    /// </summary>
    /// <param name="path">The path to file or resource.</param>
    /// <param name="baseUri">The base uri.</param>
    /// <param name="parameters">The svg parameters.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource Load(string path, Uri? baseUri, SvgParameters? parameters = null)
    {
        return new() { Picture = LoadPicture(path, baseUri, parameters) };
    }

    /// <summary>
    /// Loads svg picture from stream.
    /// </summary>
    /// <param name="stream">The svg stream.</param>
    /// <param name="parameters">The svg parameters.</param>
    /// <returns>The svg picture.</returns>
    public static SKPicture? LoadPicture(Stream stream, SvgParameters? parameters = null)
    {
        var document = SvgService.Open(stream, parameters);
        return document is { } ? SvgService.ToModel(document, s_assetLoader, out _, out _) : default;
    }

    /// <summary>
    /// Loads svg source from stream.
    /// </summary>
    /// <param name="stream">The svg stream.</param>
    /// <param name="parameters">The svg parameters.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource Load(Stream stream, SvgParameters? parameters = null)
    {
        return new() { Picture = LoadPicture(stream, parameters) };
    }

    /// <summary>
    /// Loads svg picture from svg source.
    /// </summary>
    /// <param name="source">The svg source.</param>
    /// <returns>The svg picture.</returns>
    public static SKPicture? LoadPictureFromSvg(string source, SvgParameters? parameters = null)
    {
        var document = SvgService.FromSvg(source);
        return document is { } ? SvgService.ToModel(document, s_assetLoader, out _, out _) : default;
    }

    /// <summary>
    /// Loads svg source from svg source.
    /// </summary>
    /// <param name="source">The svg source.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource LoadFromSvg(string source)
    {
        return new() { Picture = LoadPictureFromSvg(source) };
    }

    /// <summary>
    /// Rebuilds the <see cref="SvgSource"/> from its underlying model, refreshing its associated picture.
    /// </summary>
    public void RebuildFromModel()
    {
        if (Picture is not { } picture)
        {
            return;
        }

        Picture = picture.DeepClone();
    }
}
