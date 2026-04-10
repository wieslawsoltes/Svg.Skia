using ShimSkiaSharp;

namespace Svg.Skia;

internal interface ISvgSceneFilterSource
{
    SKPicture? SourceGraphic(SKRect? clip);
    SKPicture? BackgroundImage(SKRect? clip);
    SKPicture? FillPaint(SKRect? clip);
    SKPicture? StrokePaint(SKRect? clip);
}
