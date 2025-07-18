﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.ComponentModel;
using System.Globalization;
using SM = Svg.Model;

namespace Avalonia.Svg;

/// <summary>
/// Represents a <see cref="SvgSource"/> type converter.
/// </summary>
public class SvgSourceTypeConverter : TypeConverter
{
    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string);
    }

    /// <inheritdoc/>
    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        var path = (string)value;
        var baseUri = context?.GetContextBaseUri();
        return SvgSource.Load(path, baseUri);
    }
}
