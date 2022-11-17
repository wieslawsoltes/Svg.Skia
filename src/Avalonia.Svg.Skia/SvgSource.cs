using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Avalonia.Platform;
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
    /// <returns>The svg source.</returns>
    public static T? Load<T>(string path, Uri? baseUri) where T : SKSvg, new()
    {
        if (File.Exists(path))
        {
            var source = new T();
            source.Load(path);
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
                    source.Load(stream);
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
            source.Load(uri.LocalPath);
            return source;
        }
        else
        {
            var loader = AvaloniaLocator.Current.GetService<IAssetLoader>();
            var stream = loader?.Open(uri, baseUri);
            if (stream is null)
            {
                return default;
            }
            var source = new T();
            source.Load(stream);
            return source;
        }
    }
}
