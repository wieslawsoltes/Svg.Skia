using ShimSkiaSharp.Primitives;

namespace Svg.Model.Drawables
{
    internal interface IPictureSource
    {
        void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until);
        void Draw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until, bool enableTransform);
    }
}
