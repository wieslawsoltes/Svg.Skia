using System;
using System.ComponentModel;
using Avalonia.Platform;
using Svg.Model.Primitives;
using SM = Svg.Model;
using SP = Svg.Model;

namespace Avalonia.Svg
{
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
                var document = SM.SvgModelExtensions.Open(uri.LocalPath);
                if (document is { })
                {
                    return SM.SvgModelExtensions.ToModel(document, s_assetLoader);
                }
                return default;
            }
            else
            {
                var loader = AvaloniaLocator.Current.GetService<IAssetLoader>();
                var stream = loader.Open(uri, baseUri);
                var document = SM.SvgModelExtensions.Open(stream);
                if (document is { })
                {
                    return SM.SvgModelExtensions.ToModel(document, s_assetLoader);
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
}
