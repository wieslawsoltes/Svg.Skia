namespace Svg.Model
{
    public abstract class PathEffect
    {
        public static PathEffect CreateDash(float[] intervals, float phase)
        {
            return new DashPathEffect()
            {
                Intervals = intervals,
                Phase = phase
            };
        }
    }
}
