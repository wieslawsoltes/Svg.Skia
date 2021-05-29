using Svg.Model.Painting;
using Svg.Model.Primitives;

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
