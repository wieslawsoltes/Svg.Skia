#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp;
#endif

namespace Svg.Model.Drawables
{
    internal interface IFilterSource
    {
        SKPicture? SourceGraphic(SKRect? clip);
        SKPicture? BackgroundImage(SKRect? clip);
        SKPaint? FillPaint();
        SKPaint? StrokePaint();
    }
}
