#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp.Painting;
using ShimSkiaSharp.Primitives;
#endif

namespace Svg.Model.Drawables
{
    internal interface IFilterSource
    {
        SKPicture? SourceGraphic();
        SKPicture? BackgroundImage();
        SKPaint? FillPaint();
        SKPaint? StrokePaint();
    }
}
