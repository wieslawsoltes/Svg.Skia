
namespace ShimSkiaSharp.Painting.ColorFilters
{
    public sealed class TableColorFilter : SKColorFilter
    {
        public byte[]? TableA { get; set; }
        public byte[]? TableR { get; set; }
        public byte[]? TableG { get; set; }
        public byte[]? TableB { get; set; }
    }
}
