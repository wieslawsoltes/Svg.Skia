
namespace ShimSkiaSharp.Painting.PathEffects
{
    public class DashPathEffect : SKPathEffect
    {
        public float[]? Intervals { get; set; }
        public float Phase { get; set; }
    }
}
