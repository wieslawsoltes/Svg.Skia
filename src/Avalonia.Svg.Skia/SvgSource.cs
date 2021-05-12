using System;
using System.ComponentModel;
using Avalonia.Platform;
using SkiaSharp;
using Svg.Skia;

namespace Avalonia.Svg.Skia
{
    /// <summary>
    /// Represents a <see cref="SKPicture"/> based image.
    /// </summary>
    [TypeConverter(typeof(SvgSourceTypeConverter))]
    public class SvgSource : SKSvg
    {
        /// <summary>
        /// Loads svg source from file or resource.
        /// </summary>
        /// <param name="path">The path to file or resource.</param>
        /// <param name="baseUri">The base uri.</param>
        /// <returns>The svg source.</returns>
        public static SvgSource Load(string path, Uri? baseUri)
        {
            var uri = path.StartsWith("/") ? new Uri(path, UriKind.Relative) : new Uri(path, UriKind.RelativeOrAbsolute);
            if (uri.IsAbsoluteUri && uri.IsFile)
            {
                var source = new SvgSource();
                source.Load(uri.LocalPath);
                return source;
            }
            else
            {
                var loader = AvaloniaLocator.Current.GetService<IAssetLoader>();
                var stream = loader.Open(uri, baseUri);
                var source = new SvgSource();
                source.Load(stream);
                return source;
            }
        }
    }
}
