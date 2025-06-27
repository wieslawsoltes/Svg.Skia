// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using A = Avalonia;
using AM = Avalonia.Media;
using AVMI = Avalonia.Media.Imaging;

namespace Avalonia.Svg.Commands;

public sealed class ImageDrawCommand : DrawCommand
{
    public AM.IImage? Source { get; }
    public A.Rect SourceRect { get; }
    public A.Rect DestRect { get; }
    public AVMI.BitmapInterpolationMode BitmapInterpolationMode { get; }

    public ImageDrawCommand(AM.IImage? source, A.Rect sourceRect, A.Rect destRect, AVMI.BitmapInterpolationMode bitmapInterpolationMode)
    {
        Source = source;
        SourceRect = sourceRect;
        DestRect = destRect;
        BitmapInterpolationMode = bitmapInterpolationMode;
    }

    public override void Dispose()
    {
        base.Dispose();

        if (Source is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
