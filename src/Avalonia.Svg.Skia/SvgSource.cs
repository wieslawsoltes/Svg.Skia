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
using Svg;
using Svg.Skia;

namespace Avalonia.Svg.Skia;

/// <summary>
/// Represents a <see cref="SkiaSharp.SKPicture"/> based image.
/// </summary>
[TypeConverter(typeof(SvgSourceTypeConverter))]
public class SvgSource : SKSvg
{
    /// <summary>t
    /// Loads svg source from file or resource.
    /// </summary>
    /// <param name="path">The path to file or resource.</param>
    /// <param name="baseUri">The base uri.</param>
    /// <param name="entities">The svg entities.</param>
    /// <returns>The svg source.</returns>
    public static T? Load<T>(string path, Uri? baseUri, Dictionary<string, string>? entities = null) where T : SKSvg, new()
    {
        if (File.Exists(path))
        {
            var source = new T();
            source.Load(path, entities);
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
                    source.Load(stream, entities);
                    return source;
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
            var source = new T();
            source.Load(uri.LocalPath, entities);
            return source;
        }
        else
        {
            var stream = Platform.AssetLoader.Open(uri, baseUri);
            if (stream is null)
            {
                return default;
            }
            var source = new T();
            source.Load(stream, entities);
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
    /// <param name="entities">The svg entities.</param>
    /// <returns>The svg source.</returns>
    public static T? LoadFromStream<T>(Stream stream, Dictionary<string, string>? entities = null) where T : SKSvg, new()
    {
        var skSvg = new T();
        skSvg.Load(stream, entities);
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
}
