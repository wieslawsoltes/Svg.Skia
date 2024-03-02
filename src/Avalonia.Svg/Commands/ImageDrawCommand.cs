using System;
using A = Avalonia;
using AM = Avalonia.Media;
using AVMI = Avalonia.Media.Imaging;

namespace Avalonia.Svg.Commands;

public sealed class ImageDrawCommand(
    AM.IImage? source,
    A.Rect sourceRect,
    A.Rect destRect,
    AVMI.BitmapInterpolationMode bitmapInterpolationMode)
    : DrawCommand
{
    public AM.IImage? Source { get; } = source;
    public A.Rect SourceRect { get; } = sourceRect;
    public A.Rect DestRect { get; } = destRect;
    public AVMI.BitmapInterpolationMode BitmapInterpolationMode { get; } = bitmapInterpolationMode;

    public override void Dispose()
    {
        base.Dispose();

        if (Source is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
