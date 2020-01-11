// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace Svg.Skia.Avalonia
{
    /// <inheritdoc/>
    public class SvgTypeConverter : TypeConverter
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

            var svg = new SvgSkia();
            if (uri.IsAbsoluteUri && uri.IsFile)
            {
                svg.Load(uri.LocalPath);
                return svg;
            }
            else
            {
                var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                svg.Load(assets.Open(uri, context.GetContextBaseUri()));
            }
            return svg;
        }
    }
}
