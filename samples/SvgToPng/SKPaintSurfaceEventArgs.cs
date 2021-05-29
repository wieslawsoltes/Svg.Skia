// https://github.com/mono/SkiaSharp/blob/master/source/SkiaSharp.Views/SkiaSharp.Views.Shared/SKPaintSurfaceEventArgs.cs
using System;

namespace SvgToPng
{
    public class SKPaintSurfaceEventArgs : EventArgs
    {
        public SKPaintSurfaceEventArgs(SkiaSharp.SKSurface surface, SkiaSharp.SKImageInfo info)
        {
            Surface = surface;
            Info = info;
        }

        public SkiaSharp.SKSurface Surface { get; private set; }

        public SkiaSharp.SKImageInfo Info { get; private set; }
    }
}
