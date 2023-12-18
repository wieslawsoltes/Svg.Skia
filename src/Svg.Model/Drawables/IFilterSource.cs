using ShimSkiaSharp;

namespace Svg.Model.Drawables;

internal interface IFilterSource
{
    SKPicture? SourceGraphic(SKRect? clip);
    SKPicture? BackgroundImage(SKRect? clip);
    SKPaint? FillPaint();
    SKPaint? StrokePaint();
}
