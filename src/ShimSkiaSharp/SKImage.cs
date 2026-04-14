// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.IO;

namespace ShimSkiaSharp;

public class SKImage : ICloneable, IDeepCloneable<SKImage>
{
    private byte[]? _data;
    private float _width;
    private float _height;
    private int _version;

    internal int Version => _version;

    public byte[]? Data
    {
        get => _data;
        set
        {
            if (ReferenceEquals(_data, value))
            {
                return;
            }

            _data = value;
            _version++;
        }
    }

    public float Width
    {
        get => _width;
        set
        {
            if (_width.Equals(value))
            {
                return;
            }

            _width = value;
            _version++;
        }
    }

    public float Height
    {
        get => _height;
        set
        {
            if (_height.Equals(value))
            {
                return;
            }

            _height = value;
            _version++;
        }
    }

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
