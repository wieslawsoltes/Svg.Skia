// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace ShimSkiaSharp;

public class PathClip : ICloneable, IDeepCloneable<PathClip>
{
    public SKPath? Path { get; set; }

    public SKMatrix? Transform { get; set; }

    public ClipPath? Clip { get; set; }

    public PathClip Clone() => DeepClone(new CloneContext());

    public PathClip DeepClone() => Clone();

    object ICloneable.Clone() => Clone();

    internal PathClip DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out PathClip existing))
        {
            return existing;
        }

        var clone = new PathClip();
        context.Add(this, clone);

        clone.Path = Path?.DeepClone(context);
        clone.Transform = Transform;
        clone.Clip = Clip?.DeepClone(context);

        return clone;
    }
}
