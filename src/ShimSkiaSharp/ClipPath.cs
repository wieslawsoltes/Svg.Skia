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

    public ClipPath Clone()
    {
        return new ClipPath
        {
            Clips = CloneHelpers.CloneList(Clips, clip => clip.Clone()),
            Transform = Transform,
            Clip = Clip?.Clone()
        };
    }

    public ClipPath DeepClone() => Clone();

    object ICloneable.Clone() => Clone();
}
