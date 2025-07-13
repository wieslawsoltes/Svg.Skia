using System;
using ShimSkiaSharp;

namespace Svg.Skia;

public class SKSvgDrawEventArgs : EventArgs
{
    public SKCanvas Canvas { get; }

    internal SKSvgDrawEventArgs(SKCanvas canvas)
    {
        Canvas = canvas;
    }
}
