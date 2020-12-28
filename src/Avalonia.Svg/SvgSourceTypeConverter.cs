﻿using System;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Platform;

namespace Avalonia.Svg
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
                var document = global::Svg.Skia.SKSvg.Open(uri.LocalPath);
                if (document != null)
                {
                    svg.Picture = global::Svg.Skia.SKSvg.ToModel(document);
                }

                return svg;
            }
            else
            {
                var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                var document = global::Svg.Skia.SKSvg.Open(assets.Open(uri, context.GetContextBaseUri()));
                if (document != null)
                {
                    svg.Picture = global::Svg.Skia.SKSvg.ToModel(document);
                }
            }

            return svg;
        }
    }
}