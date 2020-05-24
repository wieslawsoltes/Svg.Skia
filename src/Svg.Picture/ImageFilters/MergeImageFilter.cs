namespace Svg.Picture
{
    public class MergeImageFilter : ImageFilter
    {
        public ImageFilter[]? Filters { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
