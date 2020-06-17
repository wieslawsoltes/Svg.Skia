
namespace Svg.Picture
{
    public sealed class PictureImageFilter : ImageFilter
    {
        public Picture? Picture { get; set; }
        public Rect? CropRect { get; set; }
    }
}
