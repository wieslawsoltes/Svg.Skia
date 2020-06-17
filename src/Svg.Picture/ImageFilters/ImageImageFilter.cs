
namespace Svg.Picture
{
    public sealed class ImageImageFilter : ImageFilter
    {
        public Image? Image { get; set; }
        public Rect Src { get; set; }
        public Rect Dst { get; set; }
        public FilterQuality FilterQuality { get; set; }
    }
}
