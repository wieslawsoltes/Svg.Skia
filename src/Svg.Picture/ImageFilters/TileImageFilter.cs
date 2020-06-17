namespace Svg.Picture
{
    public sealed class TileImageFilter : ImageFilter
    {
        public Rect Src { get; set; }
        public Rect Dst { get; set; }
        public ImageFilter? Input { get; set; }
    }
}
