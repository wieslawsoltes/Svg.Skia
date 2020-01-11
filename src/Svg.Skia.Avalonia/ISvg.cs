// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using SkiaSharp;

namespace Svg.Skia.Avalonia
{
    /// <summary>
    /// Represents a <see cref="SKPicture"/> image.
    /// </summary>
    [TypeConverter(typeof(SvgTypeConverter))]
    public interface ISvg : IDisposable
    {
        /// <summary>
        /// Gets or sets picture.
        /// </summary>
        SKPicture? Picture { get; set; }
    }
}
