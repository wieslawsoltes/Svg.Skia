using System;
using Svg.Model.Painting.PathEffects;

namespace Svg.Model.Painting
{
    public abstract class PathEffect
    {
        public static PathEffect CreateDash(float[] intervals, float phase)
        {
            return new DashPathEffect
            {
                Intervals = intervals,
                Phase = phase
            };
        }
    }
}
