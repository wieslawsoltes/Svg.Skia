// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;

namespace Svg.Skia
{
    [Flags]
    public enum Attributes
    {
        None = 0,
        Display = 1,
        Visibility = 2,
        Opacity = 4,
        Filter = 8,
        ClipPath = 16,
        Mask = 32,
        RequiredFeatures = 64,
        RequiredExtensions = 128,
        SystemLanguage = 256
    }
}
