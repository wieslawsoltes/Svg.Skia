using Svg.Transforms;

namespace Svg
{
    public interface ISvgTransformable
    {
        SvgTransformCollection Transforms { get; set; }
    }
}
