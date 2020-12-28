
using Svg.Model.Painting;

namespace Svg.Model.PathEffects
{
    public class DashPathEffect : PathEffect
    {
        public float[]? Intervals { get; set; }
        public float Phase { get; set; }
    }
}
