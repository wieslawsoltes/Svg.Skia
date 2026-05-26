// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Platform;

namespace Avalonia.Svg.Skia;

/// <summary>
/// Represents a <see cref="SvgSource"/> type converter.
/// </summary>
public class SvgSourceTypeConverter : TypeConverter
{
    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    /// <inheritdoc/>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string path)
        {
            return base.ConvertFrom(context, culture, value);
        }

        var baseUri = context?.GetContextBaseUri();
        return new SvgSource(baseUri) { Path = path };
    }
}
