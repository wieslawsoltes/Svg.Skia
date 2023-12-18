using SkiaSharp;

namespace Avalonia.Controls.Skia;

/// <summary>
/// 
/// </summary>
public class SKCanvasEventArgs
{
    /// <summary>
    /// 
    /// </summary>
    public SKCanvas Canvas { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="canvas"></param>
    internal SKCanvasEventArgs(SKCanvas canvas)
    {
        Canvas = canvas;
    }
}
