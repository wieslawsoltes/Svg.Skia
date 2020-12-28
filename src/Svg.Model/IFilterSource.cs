namespace Svg.Model
{
    internal interface IFilterSource
    {
        Picture.Picture? SourceGraphic();
        Picture.Picture? BackgroundImage();
        Painting.Paint? FillPaint();
        Painting.Paint? StrokePaint();
    }
}
