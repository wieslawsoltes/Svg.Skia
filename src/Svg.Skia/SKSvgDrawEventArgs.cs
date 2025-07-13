using System;

namespace Svg.Skia;

public class SKSvgDrawEventArgs : EventArgs
{
    public SkiaSharp.SKCanvas Canvas { get; }

    internal SKSvgDrawEventArgs(SkiaSharp.SKCanvas canvas)
    {
        Canvas = canvas;
    }
}
