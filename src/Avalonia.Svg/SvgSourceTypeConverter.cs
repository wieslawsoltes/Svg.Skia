using System;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Platform;
using SM = Svg.Model;

namespace Avalonia.Svg
{
    /// <summary>
    /// Represents a <see cref="SvgSource"/> type converter.
    /// </summary>
    public class SvgSourceTypeConverter : TypeConverter
    {
        private static readonly SM.IAssetLoader _assetLoader = new AvaloniaAssetLoader();

        /// <inheritdoc/>
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        /// <inheritdoc/>
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var s = (string)value;
            var uri = s.StartsWith("/")
                ? new Uri(s, UriKind.Relative)
                : new Uri(s, UriKind.RelativeOrAbsolute);
            var svg = new SvgSource();
            if (uri.IsAbsoluteUri && uri.IsFile)
            {
                var document = SM.SvgModelExtensions.Open(uri.LocalPath);
                if (document is { })
                {
                    svg.Picture = SM.SvgModelExtensions.ToModel(document, _assetLoader);
                }

                return svg;
            }
            else
            {
                var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                var document = SM.SvgModelExtensions.Open(assets.Open(uri, context.GetContextBaseUri()));
                if (document is { })
                {
                    svg.Picture = SM.SvgModelExtensions.ToModel(document, _assetLoader);
                }
            }

            return svg;
        }
    }
}
