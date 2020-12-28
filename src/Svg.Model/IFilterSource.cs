namespace Svg.Model
{
    internal interface IFilterSource
    {
        Picture.Picture? SourceGraphic();

        Picture.Picture? BackgroundImage();

        Paint.Paint? FillPaint();

        Paint.Paint? StrokePaint();
    }
}
