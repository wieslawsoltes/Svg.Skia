using SkiaSharp;

namespace Svg.Picture.Skia
{
    public class SkiaPicture
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
