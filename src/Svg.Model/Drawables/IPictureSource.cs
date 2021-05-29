using Svg.Model.Primitives;

namespace Svg.Model.Drawables
{
    internal interface IPictureSource
    {
        void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, DrawableBase? until);
        void Draw(SKCanvas canvas, Attributes ignoreAttributes, DrawableBase? until, bool enableTransform);
    }
}
