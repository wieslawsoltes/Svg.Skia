using ShimSkiaSharp.Painting;
using ShimSkiaSharp.Primitives;

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
