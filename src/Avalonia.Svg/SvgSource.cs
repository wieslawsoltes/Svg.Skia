/*
 * Svg.Skia SVG rendering library.
 * Copyright (C) 2023  Wiesław Šoltés
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
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
    /// <param name="entities">The svg entities.</param>
    /// <returns>The svg picture.</returns>
    public static SKPicture? LoadPicture(string path, Uri? baseUri, Dictionary<string, string>? entities = null)
    {
        if (File.Exists(path))
        {
            var document = SM.SvgExtensions.Open(path, entities);
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
                    var document = SM.SvgExtensions.Open(stream, entities);
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
            var document = SM.SvgExtensions.Open(uri.LocalPath, entities);
            return document is { } ? SM.SvgExtensions.ToModel(document, s_assetLoader, out _, out _) : default;
        }
        else
        {
            var stream = AssetLoader.Open(uri, baseUri);
            if (stream is null)
            {
                return default;
            }
            var document = SM.SvgExtensions.Open(stream, entities);
            return document is { } ? SM.SvgExtensions.ToModel(document, s_assetLoader, out _, out _) : default;
        }
    }

    /// <summary>
    /// Loads svg source from file or resource.
    /// </summary>
    /// <param name="path">The path to file or resource.</param>
    /// <param name="baseUri">The base uri.</param>
    /// <param name="entities">The svg entities.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource Load(string path, Uri? baseUri, Dictionary<string, string>? entities = null)
    {
        return new() { Picture = LoadPicture(path, baseUri, entities) };
    }

    /// <summary>
    /// Loads svg picture from stream.
    /// </summary>
    /// <param name="stream">The svg stream.</param>
    /// <param name="entities">The svg entities.</param>
    /// <returns>The svg picture.</returns>
    public static SKPicture? LoadPicture(Stream stream, Dictionary<string, string>? entities = null)
    {
        var document = SM.SvgExtensions.Open(stream, entities);
        return document is { } ? SM.SvgExtensions.ToModel(document, s_assetLoader, out _, out _) : default;
    }

    /// <summary>
    /// Loads svg source from stream.
    /// </summary>
    /// <param name="stream">The svg stream.</param>
    /// <param name="entities">The svg entities.</param>
    /// <returns>The svg source.</returns>
    public static SvgSource Load(Stream stream, Dictionary<string, string>? entities = null)
    {
        return new() { Picture = LoadPicture(stream, entities) };
    }

    /// <summary>
    /// Loads svg picture from svg source.
    /// </summary>
    /// <param name="source">The svg source.</param>
    /// <returns>The svg picture.</returns>
    public static SKPicture? LoadPictureFromSvg(string source)
    {
        var document = SM.SvgExtensions.FromSvg(source);
        return document is { } ? SM.SvgExtensions.ToModel(document, s_assetLoader, out _, out _) : default;
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
}
