using System;

namespace Svg.Model
{
    public abstract class PathEffect : IDisposable
    {
        public static PathEffect CreateDash(float[] intervals, float phase)
        {
            return new DashPathEffect()
            {
                Intervals = intervals,
                Phase = phase
            };
        }

        public void Dispose()
        {
        }
    }
}
