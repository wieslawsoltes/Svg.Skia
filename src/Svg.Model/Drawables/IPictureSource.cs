using Svg.Model.Primitives;

namespace Svg.Model.Drawables
{
    internal interface IPictureSource
    {
        void OnDraw(Canvas canvas, Attributes ignoreAttributes, DrawableBase? until);
        void Draw(Canvas canvas, Attributes ignoreAttributes, DrawableBase? until, bool enableTransform);
    }
}
