using Svg.Model.Painting;
using Svg.Model.Primitives;

namespace Svg.Model.Drawables
{
    internal interface IFilterSource
    {
        Picture? SourceGraphic();
        Picture? BackgroundImage();
        Paint? FillPaint();
        Paint? StrokePaint();
    }
}
