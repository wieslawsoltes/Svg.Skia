// https://github.com/mono/SkiaSharp/blob/master/source/SkiaSharp.Views/SkiaSharp.Views.Shared/SKPaintSurfaceEventArgs.cs
using System;
using SkiaSharp;

namespace SvgToPng
{
    public class SKPaintSurfaceEventArgs : EventArgs
    {
        public SKPaintSurfaceEventArgs(SKSurface surface, SKImageInfo info)
        {
            Surface = surface;
            Info = info;
        }

        public SKSurface Surface { get; private set; }

        public SKImageInfo Info { get; private set; }
    }
}
