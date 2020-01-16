// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;

namespace Svg.Skia
{
    [Flags]
    public enum IgnoreAttributes
    {
        None = 0,
        Opacity = 1,
        Display = 2,
        Filter = 4
    }
}
