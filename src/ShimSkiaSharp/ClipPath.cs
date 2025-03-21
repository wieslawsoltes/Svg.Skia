﻿using System.Collections.Generic;
namespace ShimSkiaSharp;

public class ClipPath
{
    public IList<PathClip>? Clips { get; set; }

    public SKMatrix? Transform { get; set; }

    public ClipPath? Clip { get; set; }

    public bool IsEmpty => Clips is null || Clips.Count == 0;

    public ClipPath()
    {
        Clips = new List<PathClip>();
    }
}
