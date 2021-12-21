using System;
using System.ComponentModel;
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
