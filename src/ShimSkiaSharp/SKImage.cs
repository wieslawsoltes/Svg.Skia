// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.IO;

namespace ShimSkiaSharp;

public class SKImage : ICloneable, IDeepCloneable<SKImage>
{
    public byte[]? Data { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    public static byte[] FromStream(Stream sourceStream)
    {
        using var memoryStream = new MemoryStream();
        sourceStream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    public SKImage Clone() => DeepClone(new CloneContext());

    public SKImage DeepClone() => Clone();

    object ICloneable.Clone() => Clone();

    internal SKImage DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKImage existing))
        {
            return existing;
        }

        var clone = new SKImage();
        context.Add(this, clone);

        clone.Data = CloneHelpers.CloneArray(Data, context);
        clone.Width = Width;
        clone.Height = Height;

        return clone;
    }
}
