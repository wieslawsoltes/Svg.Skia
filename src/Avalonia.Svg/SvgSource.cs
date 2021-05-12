using System;
using System.ComponentModel;
using Avalonia.Platform;
using Svg.Model.Primitives;
using SP = Svg.Model;
using SM = Svg.Model;

namespace Avalonia.Svg
{
    /// <summary>
    /// Represents a Svg based image.
    /// </summary>
    [TypeConverter(typeof(SvgSourceTypeConverter))]
    public class SvgSource
    {
        private static readonly SM.IAssetLoader s_assetLoader = new AvaloniaAssetLoader();

        public Picture? Picture { get; set; }

        /// <summary>
        /// Loads svg source from file or resource.
        /// </summary>
        /// <param name="path">The path to file or resource.</param>
        /// <param name="baseUri">The base uri.</param>
        /// <returns>The svg source.</returns>
        public static SvgSource Load(string path, Uri baseUri)
        {
            var uri = path.StartsWith("/") ? new Uri(path, UriKind.Relative) : new Uri(path, UriKind.RelativeOrAbsolute);
            if (uri.IsAbsoluteUri && uri.IsFile)
            {
                var source = new SvgSource();
                var document = SM.SvgModelExtensions.Open(uri.LocalPath);
                if (document is { })
                {
                    source.Picture = SM.SvgModelExtensions.ToModel(document, s_assetLoader);
                }
                return source;
            }
            else
            {
                var source = new SvgSource();
                var loader = AvaloniaLocator.Current.GetService<IAssetLoader>();
                var stream = loader.Open(uri, baseUri);
                var document = SM.SvgModelExtensions.Open(stream);
                if (document is { })
                {
                    source.Picture = SM.SvgModelExtensions.ToModel(document, s_assetLoader);
                }
                return source;
            }
        }
    }
}
