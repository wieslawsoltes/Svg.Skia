using System;
using A = Avalonia;
using AM = Avalonia.Media;
using AVMI = Avalonia.Visuals.Media.Imaging;

namespace Svg.Picture.Avalonia
{
    public sealed class ImageDrawCommand : DrawCommand
    {
        public readonly AM.IImage? Source;
        public readonly A.Rect SourceRect;
        public readonly A.Rect DestRect;
        public readonly AVMI.BitmapInterpolationMode BitmapInterpolationMode;

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
