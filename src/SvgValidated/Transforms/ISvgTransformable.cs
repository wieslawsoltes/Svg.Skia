using SvgValidated.Transforms;

namespace SvgValidated
{
    public interface ISvgTransformable
    {
        SvgTransformCollection Transforms { get; set; }
    }
}
