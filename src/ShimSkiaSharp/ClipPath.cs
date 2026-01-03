// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
namespace ShimSkiaSharp;

public class ClipPath : ICloneable, IDeepCloneable<ClipPath>
{
    public IList<PathClip>? Clips { get; set; }

    public SKMatrix? Transform { get; set; }

    public ClipPath? Clip { get; set; }

    public bool IsEmpty => Clips is null || Clips.Count == 0;

    public ClipPath()
    {
        Clips = new List<PathClip>();
    }

    public ClipPath Clone() => DeepClone(new CloneContext());

    public ClipPath DeepClone() => Clone();

    object ICloneable.Clone() => Clone();

    internal ClipPath DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out ClipPath existing))
        {
            return existing;
        }

        var clone = new ClipPath();
        context.Add(this, clone);

        clone.Clips = CloneHelpers.CloneList(Clips, context, clip => clip.DeepClone(context));
        clone.Transform = Transform;
        clone.Clip = Clip?.DeepClone(context);

        return clone;
    }
}
