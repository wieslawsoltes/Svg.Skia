// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
namespace ShimSkiaSharp;

public class PathClip
{
    public SKPath? Path { get; set; }

    public SKMatrix? Transform { get; set; }

    public ClipPath? Clip { get; set; }
}
