using System.ComponentModel;

namespace Svg
{
    [TypeConverter(typeof(SvgVectorEffectConverter))]
    public enum SvgVectorEffect
    {
        None,
        NonScalingStroke
    }

    public sealed class SvgVectorEffectConverter : EnumBaseConverter<SvgVectorEffect>
    {
        public SvgVectorEffectConverter() : base(CaseHandling.KebabCase)
        {
        }
    }

    public abstract partial class SvgVisualElement
    {
        [SvgAttribute("vector-effect")]
        public virtual SvgVectorEffect VectorEffect
        {
            get { return GetAttribute("vector-effect", false, SvgVectorEffect.None); }
            set { Attributes["vector-effect"] = value; }
        }
    }
}
