using System;
using A = Avalonia;
using AM = Avalonia.Media;
using AVMI = Avalonia.Visuals.Media.Imaging;

namespace Avalonia.Svg.Picture.Commands
{
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
}
