using SkiaSharp;
using Svg.Model.Picture;

namespace Svg.Skia
{
    public sealed class SkiaPicture
    {
        private SkiaPicture()
        {
        }

        public static SKPicture? Record(Picture picture)
        {
            return picture.ToSKPicture();
        }

        public static void Draw(Picture picture, SKCanvas skCanvas)
        {
            picture.Draw(skCanvas);
        }
    }
}
