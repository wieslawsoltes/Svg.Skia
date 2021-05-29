using Svg.Model.Painting.PathEffects;

namespace Svg.Model.Painting
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
