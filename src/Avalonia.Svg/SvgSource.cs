using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
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
        var uri = path.StartsWith("/") ? new Uri(path, UriKind.Relative) : new Uri(path, UriKind.RelativeOrAbsolute);
        if (uri.IsAbsoluteUri && uri.IsFile)
        {
            var document = SM.SvgExtensions.Open(uri.LocalPath);
            if (document is { })
            {
                return SM.SvgExtensions.ToModel(document, s_assetLoader, out _, out _);
            }
            return default;
        }
        else
        {
            var loader = AvaloniaLocator.Current.GetService<IAssetLoader>();
            var stream = loader?.Open(uri, baseUri);
            if (stream is null)
            {
                return default;
            }
            var document = SM.SvgExtensions.Open(stream);
            if (document is { })
            {
                return SM.SvgExtensions.ToModel(document, s_assetLoader, out _, out _);
            }
            return default;
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
