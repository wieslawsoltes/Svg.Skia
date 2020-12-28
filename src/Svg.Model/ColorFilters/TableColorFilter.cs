
using Svg.Model.Paint;

namespace Svg.Model.ColorFilters
{
    public sealed class TableColorFilter : ColorFilter
    {
        public byte[]? TableA { get; set; }
        public byte[]? TableR { get; set; }
        public byte[]? TableG { get; set; }
        public byte[]? TableB { get; set; }
    }
}
