#if USE_SKIASHARP
using SkiaSharp;
#else
using ShimSkiaSharp.Primitives;
#endif

namespace Svg.Model.Drawables
{
    internal interface IPictureSource
    {
        void OnDraw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until);
        void Draw(SKCanvas canvas, DrawAttributes ignoreAttributes, DrawableBase? until, bool enableTransform);
    }
}
