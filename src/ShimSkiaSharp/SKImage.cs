// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.IO;

namespace ShimSkiaSharp;

public class SKImage
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
}
