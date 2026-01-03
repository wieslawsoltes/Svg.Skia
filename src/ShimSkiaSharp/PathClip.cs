// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp;

public class PathClip : ICloneable, IDeepCloneable<PathClip>
{
    public SKPath? Path { get; set; }

    public SKMatrix? Transform { get; set; }

    public ClipPath? Clip { get; set; }

    public PathClip Clone()
    {
        return new PathClip
        {
            Path = Path?.Clone(),
            Transform = Transform,
            Clip = Clip?.Clone()
        };
    }

    public PathClip DeepClone() => Clone();

    object ICloneable.Clone() => Clone();
}
