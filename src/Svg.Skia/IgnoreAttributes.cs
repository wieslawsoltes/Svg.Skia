// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;

namespace Svg.Skia
{
    [Flags]
    public enum IgnoreAttributes
    {
        None = 0,
        Display = 1,
        Opacity = 2,
        Filter = 4,
        Clip = 8,
        Mask = 16
    }
}
