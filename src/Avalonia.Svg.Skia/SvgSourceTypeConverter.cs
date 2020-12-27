using System;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Platform;

namespace Avalonia.Svg.Skia
{
    /// <summary>
    /// Represents a <see cref="SvgSource"/> type converter.
    /// </summary>
    public class SvgSourceTypeConverter : TypeConverter
    {
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
                svg.Load(uri.LocalPath);
                return svg;
            }
            var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
            svg.Load(assets.Open(uri, context.GetContextBaseUri()));
            return svg;
        }
    }
}
