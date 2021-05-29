using ShimSkiaSharp.Painting.PathEffects;

namespace ShimSkiaSharp.Painting
{
    public abstract class SKPathEffect
    {
        public static SKPathEffect CreateDash(float[] intervals, float phase)
        {
            return new DashPathEffect
            {
                Intervals = intervals,
                Phase = phase
            };
        }
    }
}
