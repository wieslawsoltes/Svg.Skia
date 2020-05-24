namespace Svg.Picture
{
    public class PictureImageFilter : ImageFilter
    {
        public Picture? Picture { get; set; }
        public Rect? CropRect { get; set; }
    }
}
